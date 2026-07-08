using System.Reflection;
using BepInEx;
using HarmonyLib;
using ReflexCLI;

namespace ACS;

internal static class ModInfo
{
    internal const string Guid = "mctrr.bettercustomsprites";
    internal const string Name = "Better Custom Sprites";
    internal const string Version = "2.3.0";
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal class AcsMod : BaseUnityPlugin
{
    internal static AcsMod? Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        var harmony = new Harmony(ModInfo.Guid);
        harmony.PatchAll();

        CommandRegistry.assemblies.Add(Assembly.GetExecutingAssembly());
    }

    internal static void Log(object payload)
    {
        Instance!.Logger.LogInfo(payload);
    }

    internal static void Warn(object payload)
    {
        Instance!.Logger.LogWarning(payload);
    }

    internal static void Error(object payload)
    {
        Instance!.Logger.LogError(payload);
    }
}