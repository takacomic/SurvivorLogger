using MelonLoader;
using SurvivorModMenu;

namespace SurvivorLogger;

public enum SurvivorLogTimingMode
{
    Time,
    Frame
}

public enum SurvivorLogLevel
{
    Trace = 1,
    Debug = 2,
    Verbose = 4,
    Info = 8,
    Warning = 16,
    Error = 32,
    Critical = 64,
    Exception = 128
}

internal static class SurvivorFrameClock
{
    private static long _frameCounter;

    internal static long CurrentFrame => Interlocked.Read(ref _frameCounter);

    internal static void AdvanceFrame()
    {
        Interlocked.Increment(ref _frameCounter);
    }
}

public sealed class SurvivorLog<T> where T : MelonBase
{
    private const double DefaultTimeIntervalSeconds = 5d;
    private const int DefaultFrameInterval = 300;

    private readonly MelonLogger.Instance? _logger;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ThrottleState> _throttleStates = new(StringComparer.Ordinal);
    private readonly MelonPreferences_Entry<bool> _loggingEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _traceEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _debugEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _verboseEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _infoEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _warningEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _errorEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _criticalEnabledEntry;
    private readonly MelonPreferences_Entry<bool> _exceptionEnabledEntry;

    private SurvivorLogTimingMode _timingMode = SurvivorLogTimingMode.Time;
    private double _timeIntervalSeconds = DefaultTimeIntervalSeconds;
    private int _frameInterval = DefaultFrameInterval;

    public SurvivorLog(string modId, bool registerInModMenu = true)
    {
        if (string.IsNullOrWhiteSpace(modId))
            throw new ArgumentException($"{nameof(modId)} cannot be null or empty", nameof(modId));

        ModId = modId;
        _logger = Melon<T>.Logger;

        var categoryIdentifier = $"SurvivorLogger.{modId}";
        var categoryDisplayName = $"Survivor Logger ({modId})";
        var category = MelonPreferences.CreateCategory(categoryIdentifier, categoryDisplayName);
        _loggingEnabledEntry = category.CreateEntry("Enable Logging", true);
        _traceEnabledEntry = category.CreateEntry("Enable Trace", false);
        _debugEnabledEntry = category.CreateEntry("Enable Debug", false);
        _verboseEnabledEntry = category.CreateEntry("Enable Verbose", false);
        _infoEnabledEntry = category.CreateEntry("Enable Info", true);
        _warningEnabledEntry = category.CreateEntry("Enable Warning", true);
        _errorEnabledEntry = category.CreateEntry("Enable Error", true);
        _criticalEnabledEntry = category.CreateEntry("Enable Critical", true);
        _exceptionEnabledEntry = category.CreateEntry("Enable Exception", true);

        if (registerInModMenu)
            ModMenuRegistry.RegisterSupplement(ModId, "SurvivorLogger", BuildLogOptions);
    }

    public string ModId { get; }

    public SurvivorLogTimingMode TimingMode
    {
        get
        {
            lock (_syncRoot)
                return _timingMode;
        }
    }

    public double TimeIntervalSeconds
    {
        get
        {
            lock (_syncRoot)
                return _timeIntervalSeconds;
        }
    }

    public int FrameInterval
    {
        get
        {
            lock (_syncRoot)
                return _frameInterval;
        }
    }

    public void Msg(string message)
    {
        Log(SurvivorLogLevel.Info, message);
    }

    public void Msg(string scope, string message)
    {
        Log(SurvivorLogLevel.Info, message, scope);
    }

    public void Msg(object message)
    {
        if (message == null)
            return;

        Msg(message.ToString()!);
    }

    public void Trace(string message)
    {
        Log(SurvivorLogLevel.Trace, message);
    }

    public void Trace(string scope, string message)
    {
        Log(SurvivorLogLevel.Trace, message, scope);
    }

    public void Debug(string message)
    {
        Log(SurvivorLogLevel.Debug, message);
    }

    public void Debug(string scope, string message)
    {
        Log(SurvivorLogLevel.Debug, message, scope);
    }

    public void Verbose(string message)
    {
        Log(SurvivorLogLevel.Verbose, message);
    }

    public void Verbose(string scope, string message)
    {
        Log(SurvivorLogLevel.Verbose, message, scope);
    }

    public void Info(string message)
    {
        Log(SurvivorLogLevel.Info, message);
    }

    public void Info(string scope, string message)
    {
        Log(SurvivorLogLevel.Info, message, scope);
    }

    public void Warning(string message)
    {
        Log(SurvivorLogLevel.Warning, message);
    }

    public void Warning(string scope, string message)
    {
        Log(SurvivorLogLevel.Warning, message, scope);
    }

    public void Warning(object message)
    {
        if (message == null)
            return;

        Warning(message.ToString()!);
    }

    public void Error(string message)
    {
        Log(SurvivorLogLevel.Error, message);
    }

    public void Error(string scope, string message)
    {
        Log(SurvivorLogLevel.Error, message, scope);
    }

    public void Error(object message)
    {
        if (message == null)
            return;

        Error(message.ToString()!);
    }

    public void Exception(Exception exception, string? scope = null)
    {
        if (exception == null)
            return;

        Log(SurvivorLogLevel.Exception, exception.ToString(), scope);
    }

    public void Critical(string message)
    {
        Log(SurvivorLogLevel.Critical, message);
    }

    public void Critical(string scope, string message)
    {
        Log(SurvivorLogLevel.Critical, message, scope);
    }

    public void ConfigureTiming(SurvivorLogTimingMode mode = SurvivorLogTimingMode.Time, double? timeIntervalSeconds = null, int? frameInterval = null)
    {
        lock (_syncRoot)
        {
            _timingMode = mode;

            if (timeIntervalSeconds.HasValue)
                _timeIntervalSeconds = Math.Max(0.01d, timeIntervalSeconds.Value);

            if (frameInterval.HasValue)
                _frameInterval = Math.Max(1, frameInterval.Value);
        }
    }

    public void SetTimingMode(SurvivorLogTimingMode mode)
    {
        ConfigureTiming(mode);
    }

    public void SetTimeInterval(double timeIntervalSeconds)
    {
        ConfigureTiming(timeIntervalSeconds: timeIntervalSeconds);
    }

    public void SetFrameInterval(int frameInterval)
    {
        ConfigureTiming(frameInterval: frameInterval);
    }

    public void ResetTimingState()
    {
        lock (_syncRoot)
            _throttleStates.Clear();
    }

    public void LogThrottled(SurvivorLogLevel level, string message, string? scope = null, string? messageKey = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!ShouldLogThrottled(level, scope, message, messageKey))
            return;

        Log(level, message, scope);
    }

    public void MsgThrottled(string message, string? scope = null, string? messageKey = null)
    {
        LogThrottled(SurvivorLogLevel.Info, message, scope, messageKey);
    }

    public void WarningThrottled(string message, string? scope = null, string? messageKey = null)
    {
        LogThrottled(SurvivorLogLevel.Warning, message, scope, messageKey);
    }

    public void ErrorThrottled(string message, string? scope = null, string? messageKey = null)
    {
        LogThrottled(SurvivorLogLevel.Error, message, scope, messageKey);
    }

    public void Log(SurvivorLogLevel level, string message, string? scope = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!IsLevelEnabled(level))
            return;

        var scopedMessage = BuildScopedMessage(scope, message);
        var output = BuildLevelMessage(level, scopedMessage);
        Write(level, output);
    }

    public bool IsLevelEnabled(SurvivorLogLevel level)
    {
        if (!_loggingEnabledEntry.Value)
            return false;

        var entry = GetEntryForLevel(level);
        if (entry == null)
            return true;

        return entry.Value;
    }

    public void SetLevelEnabled(SurvivorLogLevel level, bool enabled)
    {
        var entry = GetEntryForLevel(level);
        if (entry == null)
            return;

        entry.Value = enabled;
        MelonPreferences.Save();
    }

    private MelonPreferences_Entry<bool>? GetEntryForLevel(SurvivorLogLevel level)
    {
        return level switch
        {
            SurvivorLogLevel.Trace => _traceEnabledEntry,
            SurvivorLogLevel.Debug => _debugEnabledEntry,
            SurvivorLogLevel.Verbose => _verboseEnabledEntry,
            SurvivorLogLevel.Info => _infoEnabledEntry,
            SurvivorLogLevel.Warning => _warningEnabledEntry,
            SurvivorLogLevel.Error => _errorEnabledEntry,
            SurvivorLogLevel.Critical => _criticalEnabledEntry,
            SurvivorLogLevel.Exception => _exceptionEnabledEntry,
            _ => null
        };
    }

    private static string BuildScopedMessage(string? scope, string message)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return message;

        return $"[{scope}] {message}";
    }

    private static string BuildLevelMessage(SurvivorLogLevel level, string message)
    {
        var prefix = level switch
        {
            SurvivorLogLevel.Trace => "[TRACE]",
            SurvivorLogLevel.Debug => "[DEBUG]",
            SurvivorLogLevel.Verbose => "[VERBOSE]",
            SurvivorLogLevel.Info => "[INFO]",
            SurvivorLogLevel.Warning => "[WARNING]",
            SurvivorLogLevel.Error => "[ERROR]",
            SurvivorLogLevel.Critical => "[CRITICAL]",
            SurvivorLogLevel.Exception => "[EXCEPTION]",
            _ => "[LOG]"
        };

        return $"{prefix} {message}";
    }

    private void Write(SurvivorLogLevel level, string output)
    {
        if (_logger == null)
        {
            WriteDefault(level, output);
            return;
        }

        if (level == SurvivorLogLevel.Warning)
        {
            _logger.Warning(output);
            return;
        }

        if (level is SurvivorLogLevel.Error or SurvivorLogLevel.Critical or SurvivorLogLevel.Exception)
        {
            _logger.Error(output);
            return;
        }

        _logger.Msg(output);
    }

    private static void WriteDefault(SurvivorLogLevel level, string output)
    {
        if (level == SurvivorLogLevel.Warning)
        {
            MelonLogger.Warning(output);
            return;
        }

        if (level is SurvivorLogLevel.Error or SurvivorLogLevel.Critical or SurvivorLogLevel.Exception)
        {
            MelonLogger.Error(output);
            return;
        }

        MelonLogger.Msg(output);
    }

    private bool ShouldLogThrottled(SurvivorLogLevel level, string? scope, string message, string? messageKey)
    {
        var key = BuildThrottleKey(level, scope, message, messageKey);
        var utcNow = DateTime.UtcNow;
        var frameNow = SurvivorFrameClock.CurrentFrame;

        lock (_syncRoot)
        {
            if (!_throttleStates.TryGetValue(key, out var state))
            {
                state = new ThrottleState();
                _throttleStates[key] = state;
            }

            if (!state.HasLogged)
            {
                state.HasLogged = true;
                state.LastLoggedUtc = utcNow;
                state.LastLoggedFrame = frameNow;
                return true;
            }

            if (_timingMode == SurvivorLogTimingMode.Frame)
            {
                var frameDelta = frameNow - state.LastLoggedFrame;
                if (frameDelta < _frameInterval)
                    return false;

                state.LastLoggedFrame = frameNow;
                state.LastLoggedUtc = utcNow;
                return true;
            }

            var elapsed = utcNow - state.LastLoggedUtc;
            if (elapsed.TotalSeconds < _timeIntervalSeconds)
                return false;

            state.LastLoggedUtc = utcNow;
            state.LastLoggedFrame = frameNow;
            return true;
        }
    }

    private static string BuildThrottleKey(SurvivorLogLevel level, string? scope, string message, string? messageKey)
    {
        var key = messageKey;
        if (string.IsNullOrWhiteSpace(key))
            key = message;

        return $"{level}|{scope ?? string.Empty}|{key}";
    }

    private void BuildLogOptions(IModMenuSectionBuilder builder)
    {
        if (builder == null)
            return;

        builder.AddLabel("Logger");
        builder.AddToggle("Enable Logging", () => _loggingEnabledEntry.Value, value =>
        {
            _loggingEnabledEntry.Value = value;
            MelonPreferences.Save();
        });
        builder.AddToggle("Trace", () => _traceEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Trace, value));
        builder.AddToggle("Debug", () => _debugEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Debug, value));
        builder.AddToggle("Verbose", () => _verboseEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Verbose, value));
        builder.AddToggle("Info", () => _infoEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Info, value));
        builder.AddToggle("Warning", () => _warningEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Warning, value));
        builder.AddToggle("Error", () => _errorEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Error, value));
        builder.AddToggle("Critical", () => _criticalEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Critical, value));
        builder.AddToggle("Exception", () => _exceptionEnabledEntry.Value, value => SetLevelEnabled(SurvivorLogLevel.Exception, value));
    }

    private sealed class ThrottleState
    {
        internal bool HasLogged { get; set; }
        internal DateTime LastLoggedUtc { get; set; }
        internal long LastLoggedFrame { get; set; }
    }
}
