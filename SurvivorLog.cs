using MelonLoader;

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

public static class SurvivorLog
{
    private const string CategoryIdentifier = "SurvivorLogger";
    private const string CategoryDisplayName = "Survivor Logger";
    private const double DefaultTimeIntervalSeconds = 5d;
    private const int DefaultFrameInterval = 300;
    private static MelonLogger.Instance? _logger;
    private static bool _preferencesInitialized;
    private static long _frameCounter;
    private static MelonPreferences_Entry<bool>? _loggingEnabledEntry;
    private static MelonPreferences_Entry<bool>? _traceEnabledEntry;
    private static MelonPreferences_Entry<bool>? _debugEnabledEntry;
    private static MelonPreferences_Entry<bool>? _verboseEnabledEntry;
    private static MelonPreferences_Entry<bool>? _infoEnabledEntry;
    private static MelonPreferences_Entry<bool>? _warningEnabledEntry;
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, ModTimingSettings> ModTimingSettingsByMod = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, ThrottleState>> ThrottleStatesByMod = new(StringComparer.Ordinal);

    public static bool IsInitialized => _logger != null;

    internal static void Initialize(MelonLogger.Instance logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        InitializePreferences();
    }

    internal static void AdvanceFrame()
    {
        lock (SyncRoot)
            _frameCounter++;
    }

    public static void Msg(string message)
    {
        Log(SurvivorLogLevel.Info, message);
    }

    public static void Msg(string scope, string message)
    {
        Log(SurvivorLogLevel.Info, message, scope);
    }

    public static void Msg(object message)
    {
        if (message == null)
            return;

        Msg(message.ToString()!);
    }

    public static void Trace(string message)
    {
        Log(SurvivorLogLevel.Trace, message);
    }

    public static void Trace(string scope, string message)
    {
        Log(SurvivorLogLevel.Trace, message, scope);
    }

    public static void Debug(string message)
    {
        Log(SurvivorLogLevel.Debug, message);
    }

    public static void Debug(string scope, string message)
    {
        Log(SurvivorLogLevel.Debug, message, scope);
    }

    public static void Verbose(string message)
    {
        Log(SurvivorLogLevel.Verbose, message);
    }

    public static void Verbose(string scope, string message)
    {
        Log(SurvivorLogLevel.Verbose, message, scope);
    }

    public static void Info(string message)
    {
        Log(SurvivorLogLevel.Info, message);
    }

    public static void Info(string scope, string message)
    {
        Log(SurvivorLogLevel.Info, message, scope);
    }

    public static void Warning(string message)
    {
        Log(SurvivorLogLevel.Warning, message);
    }

    public static void Warning(string scope, string message)
    {
        Log(SurvivorLogLevel.Warning, message, scope);
    }

    public static void Warning(object message)
    {
        if (message == null)
            return;

        Warning(message.ToString()!);
    }

    public static void Error(string message)
    {
        Log(SurvivorLogLevel.Error, message);
    }

    public static void Error(string scope, string message)
    {
        Log(SurvivorLogLevel.Error, message, scope);
    }

    public static void Error(object message)
    {
        if (message == null)
            return;

        Error(message.ToString()!);
    }

    public static void Exception(Exception exception, string? scope = null)
    {
        if (exception == null)
            return;

        Log(SurvivorLogLevel.Exception, exception.ToString(), scope);
    }

    public static void Critical(string message)
    {
        Log(SurvivorLogLevel.Critical, message);
    }

    public static void Critical(string scope, string message)
    {
        Log(SurvivorLogLevel.Critical, message, scope);
    }

    public static void ConfigureModTiming(string modId, SurvivorLogTimingMode mode = SurvivorLogTimingMode.Time, double? timeIntervalSeconds = null, int? frameInterval = null)
    {
        if (string.IsNullOrWhiteSpace(modId))
            throw new ArgumentException($"{nameof(modId)} cannot be null or empty", nameof(modId));

        lock (SyncRoot)
        {
            var settings = GetOrCreateModTimingSettings(modId);
            settings.Mode = mode;

            if (timeIntervalSeconds.HasValue)
                settings.TimeIntervalSeconds = Math.Max(0.01d, timeIntervalSeconds.Value);

            if (frameInterval.HasValue)
                settings.FrameInterval = Math.Max(1, frameInterval.Value);
        }
    }

    public static void SetModTimingMode(string modId, SurvivorLogTimingMode mode)
    {
        ConfigureModTiming(modId, mode);
    }

    public static void SetModTimeInterval(string modId, double timeIntervalSeconds)
    {
        ConfigureModTiming(modId, timeIntervalSeconds: timeIntervalSeconds);
    }

    public static void SetModFrameInterval(string modId, int frameInterval)
    {
        ConfigureModTiming(modId, frameInterval: frameInterval);
    }

    public static void ResetModTimingState(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return;

        lock (SyncRoot)
        {
            if (!ThrottleStatesByMod.TryGetValue(modId, out var states))
                return;

            states.Clear();
        }
    }

    public static void LogThrottled(string modId, SurvivorLogLevel level, string message, string? scope = null, string? messageKey = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!ShouldLogThrottled(modId, level, scope, message, messageKey))
            return;

        Log(level, message, scope);
    }

    public static void MsgThrottled(string modId, string message, string? scope = null, string? messageKey = null)
    {
        LogThrottled(modId, SurvivorLogLevel.Info, message, scope, messageKey);
    }

    public static void WarningThrottled(string modId, string message, string? scope = null, string? messageKey = null)
    {
        LogThrottled(modId, SurvivorLogLevel.Warning, message, scope, messageKey);
    }

    public static void ErrorThrottled(string modId, string message, string? scope = null, string? messageKey = null)
    {
        LogThrottled(modId, SurvivorLogLevel.Error, message, scope, messageKey);
    }

    public static void Log(SurvivorLogLevel level, string message, string? scope = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!IsLevelEnabled(level))
            return;

        var scopedMessage = BuildScopedMessage(scope, message);
        var output = BuildLevelMessage(level, scopedMessage);
        Write(level, output);
    }

    public static bool IsLevelEnabled(SurvivorLogLevel level)
    {
        if (IsAlwaysEnabled(level))
            return true;

        if (!_preferencesInitialized)
            return true;

        if (_loggingEnabledEntry != null && !_loggingEnabledEntry.Value)
            return false;

        var entry = GetEntryForLevel(level);
        if (entry == null)
            return true;

        return entry.Value;
    }

    public static void SetLevelEnabled(SurvivorLogLevel level, bool enabled)
    {
        if (!_preferencesInitialized)
            return;

        var entry = GetEntryForLevel(level);
        if (entry == null)
            return;

        entry.Value = enabled;
        MelonPreferences.Save();
    }

    private static void InitializePreferences()
    {
        if (_preferencesInitialized)
            return;

        var category = MelonPreferences.CreateCategory(CategoryIdentifier, CategoryDisplayName);
        _loggingEnabledEntry = category.CreateEntry("Enable Logging", true);
        _traceEnabledEntry = category.CreateEntry("Enable Trace", false);
        _debugEnabledEntry = category.CreateEntry("Enable Debug", false);
        _verboseEnabledEntry = category.CreateEntry("Enable Verbose", false);
        _infoEnabledEntry = category.CreateEntry("Enable Info", true);
        _warningEnabledEntry = category.CreateEntry("Enable Warning", true);
        _preferencesInitialized = true;
    }

    private static MelonPreferences_Entry<bool>? GetEntryForLevel(SurvivorLogLevel level)
    {
        return level switch
        {
            SurvivorLogLevel.Trace => _traceEnabledEntry,
            SurvivorLogLevel.Debug => _debugEnabledEntry,
            SurvivorLogLevel.Verbose => _verboseEnabledEntry,
            SurvivorLogLevel.Info => _infoEnabledEntry,
            SurvivorLogLevel.Warning => _warningEnabledEntry,
            _ => null
        };
    }

    private static bool IsAlwaysEnabled(SurvivorLogLevel level)
    {
        return level is SurvivorLogLevel.Error or SurvivorLogLevel.Critical or SurvivorLogLevel.Exception;
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

    private static void Write(SurvivorLogLevel level, string output)
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

    private static bool ShouldLogThrottled(string modId, SurvivorLogLevel level, string? scope, string message, string? messageKey)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return true;

        var key = BuildThrottleKey(level, scope, message, messageKey);
        var utcNow = DateTime.UtcNow;

        lock (SyncRoot)
        {
            var settings = GetOrCreateModTimingSettings(modId);
            var modStates = GetOrCreateModThrottleStates(modId);
            if (!modStates.TryGetValue(key, out var state))
            {
                state = new ThrottleState();
                modStates[key] = state;
            }

            if (!state.HasLogged)
            {
                state.HasLogged = true;
                state.LastLoggedUtc = utcNow;
                state.LastLoggedFrame = _frameCounter;
                return true;
            }

            if (settings.Mode == SurvivorLogTimingMode.Frame)
            {
                var frameDelta = _frameCounter - state.LastLoggedFrame;
                if (frameDelta < settings.FrameInterval)
                    return false;

                state.LastLoggedFrame = _frameCounter;
                state.LastLoggedUtc = utcNow;
                return true;
            }

            var elapsed = utcNow - state.LastLoggedUtc;
            if (elapsed.TotalSeconds < settings.TimeIntervalSeconds)
                return false;

            state.LastLoggedUtc = utcNow;
            state.LastLoggedFrame = _frameCounter;
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

    private static ModTimingSettings GetOrCreateModTimingSettings(string modId)
    {
        if (ModTimingSettingsByMod.TryGetValue(modId, out var settings))
            return settings;

        settings = new ModTimingSettings();
        ModTimingSettingsByMod[modId] = settings;
        return settings;
    }

    private static Dictionary<string, ThrottleState> GetOrCreateModThrottleStates(string modId)
    {
        if (ThrottleStatesByMod.TryGetValue(modId, out var states))
            return states;

        states = new Dictionary<string, ThrottleState>(StringComparer.Ordinal);
        ThrottleStatesByMod[modId] = states;
        return states;
    }

    private sealed class ModTimingSettings
    {
        internal SurvivorLogTimingMode Mode { get; set; } = SurvivorLogTimingMode.Time;
        internal double TimeIntervalSeconds { get; set; } = DefaultTimeIntervalSeconds;
        internal int FrameInterval { get; set; } = DefaultFrameInterval;
    }

    private sealed class ThrottleState
    {
        internal bool HasLogged { get; set; }
        internal DateTime LastLoggedUtc { get; set; }
        internal long LastLoggedFrame { get; set; }
    }
}
