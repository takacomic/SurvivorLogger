using MelonLoader;

[assembly: MelonInfo(typeof(SurvivorLogger.SurvivorLoggerPlugin), SurvivorLogger.BuildInfo.Name, SurvivorLogger.BuildInfo.Version, SurvivorLogger.BuildInfo.Author, SurvivorLogger.BuildInfo.Download)]
[assembly: MelonGame("poncle", "Vampire Survivors")]

namespace SurvivorLogger;

internal static class BuildInfo
{
    internal const string Name = "SurvivorLogger";
    internal const string Author = "Takacomic";
    internal const string Version = "1.0.0";
    internal const string Download = "https://github.com/takacomic";
}

public sealed class SurvivorLoggerPlugin : MelonPlugin
{
    private SurvivorLog? _pluginLog;

    public override void OnInitializeMelon()
    {
        _pluginLog = new SurvivorLog(BuildInfo.Name, LoggerInstance);
        _pluginLog.Msg($"{BuildInfo.Name} {BuildInfo.Version} initialized");
    }

    public override void OnUpdate()
    {
        SurvivorFrameClock.AdvanceFrame();
    }
}
