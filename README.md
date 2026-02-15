# SurvivorLogger

`SurvivorLogger` is a MelonLoader plugin that provides a reusable logging API for mods.

It adds:
- leveled logs (`Trace`, `Debug`, `Verbose`, `Info`, `Warning`, `Error`, `Critical`, `Exception`)
- scoped messages (`[Scope] message`)
- per-mod throttled logging with immediate first message
- per-mod timing mode (time-based or frame-based)
- automatic log-toggle registration into `SurvivorModMenu` under the implementing mod entry

## License

This project is licensed under the GNU Lesser General Public License v3.0. See `LICENSE`.

## Install

1. Build `SurvivorLogger`.
2. Copy `SurvivorLogger.dll` to `<GameRoot>/Plugins`.
3. Ensure your mod can reference `SurvivorLogger.dll` at compile time.
4. For in-game log toggles, also install `SurvivorModMenu.dll`.

## Add To Your Mod Project

Add a reference to `SurvivorLogger.dll` in your mod `.csproj`.

```xml
<ItemGroup>
  <Reference Include="SurvivorLogger">
    <HintPath>$(VSDir)/Plugins/SurvivorLogger.dll</HintPath>
  </Reference>
</ItemGroup>
```

Optional: declare an optional dependency in your mod assembly so load order is explicit.

```csharp
[assembly: MelonOptionalDependencies("SurvivorLogger")]
[assembly: MelonOptionalDependencies("SurvivorModMenu")]
```

## Basic Usage In A Mod

```csharp
using MelonLoader;
using SurvivorLogger;

public class MyMod : MelonMod
{
    private SurvivorLog<MyMod> _log = null!;

    public override void OnInitializeMelon()
    {
        _log = new SurvivorLog<MyMod>("MyMod");
        _log.Info("Init", "Initialized");
    }

    public override void OnUpdate()
    {
        _log.Debug("Loop", "Tick");
    }
}
```

`SurvivorLog` registers its toggle section under the same mod id (`"MyMod"` above), so the log settings appear in that mod's menu entry, not under `SurvivorLogger`.

## Configure Timing

Defaults per `SurvivorLog` instance:
- timing mode: `Time`
- interval: `5` seconds
- frame interval (when using frame mode): `300` frames

### Time-based (default)

```csharp
_log.ConfigureTiming();
// or explicitly
_log.ConfigureTiming(SurvivorLogTimingMode.Time, timeIntervalSeconds: 5);
```

### Frame-based

```csharp
_log.ConfigureTiming(SurvivorLogTimingMode.Frame, frameInterval: 300);
// or split calls
_log.SetTimingMode(SurvivorLogTimingMode.Frame);
_log.SetFrameInterval(300);
```

## Throttled Logging

Use throttled methods to limit repeated logs.

```csharp
_log.MsgThrottled("Scanning entities", "Spawner");
_log.WarningThrottled("Config missing key", "Config");
_log.ErrorThrottled("Failed to load asset", "Assets");
```

Behavior:
- first call for a message key logs immediately
- subsequent calls are throttled by the configured timing rule

### Stable Keys For Dynamic Messages

If message text includes changing values, provide `messageKey` so throttling still groups correctly.

```csharp
_log.MsgThrottled(
    $"Enemy count: {enemyCount}",
    scope: "Runtime",
    messageKey: "enemy-count");
```

## Log Levels And Preferences

Each logger instance creates a Melon preferences category:
- category id: `SurvivorLogger.<modId>`
- category name: `Survivor Logger (<modId>)`

Preferences include:
- `Enable Logging`
- `Enable Trace`
- `Enable Debug`
- `Enable Verbose`
- `Enable Info`
- `Enable Warning`
- `Enable Error`
- `Enable Critical`
- `Enable Exception`

## Public API Summary

- `new SurvivorLog<T>(string modId, bool registerInModMenu = true) where T : MelonBase`
- `logger.Log(SurvivorLogLevel level, string message, string? scope = null)`
- `logger.Trace/Debug/Verbose/Info/Warning/Error/Critical(...)`
- `logger.Exception(Exception exception, string? scope = null)`
- `logger.ConfigureTiming(...)`
- `logger.SetTimingMode(...)`
- `logger.SetTimeInterval(...)`
- `logger.SetFrameInterval(...)`
- `logger.ResetTimingState()`
- `logger.LogThrottled(...)`
- `logger.MsgThrottled(...)`
- `logger.WarningThrottled(...)`
- `logger.ErrorThrottled(...)`
