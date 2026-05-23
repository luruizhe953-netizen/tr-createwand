using System;
using CreateWandPatch.Content;
using CreateWandPatch.Gameplay;
using CreateWandPatch.Infrastructure;
using HarmonyLib;

namespace CreateWandPatch;

/// <summary>
/// Entry point called from native bootstrapper via il2cpp_runtime_invoke.
/// Initializes Harmony patches, content systems, and diagnostics.
/// Touch UI is initialized on first frame by the HotkeysPatch.
/// </summary>
public static class Bootstrap
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized)
            return;
        _initialized = true;

        try
        {
            CreateWandMpDebugLog.ClearOnInject();
            CreateWandOutgoingNetTrace.ClearCachedPath();
            CreateWandIncomingNetTrace.ClearCachedPath();
            CreateWandPngLibrary.EnsureReload();

            var harmony = new Harmony("com.terraria.createwand.android");

            foreach (var type in typeof(Bootstrap).Assembly.GetTypes())
            {
                if (type.Namespace == null || !type.Namespace.StartsWith("CreateWandPatch.Patches"))
                    continue;

                try
                {
                    var proc = new PatchClassProcessor(harmony, type);
                    proc.Patch();
                    UnityEngine.Debug.Log($"[CW] OK {type.Name}");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[CW] FAIL {type.Name}: {ex}");
                }
            }

            var logSb = new System.Text.StringBuilder();
            ItemIdCompatBootstrap.Apply(logSb);
            UnityEngine.Debug.Log($"[CW] ItemIdCompat: {logSb}");
            UnityEngine.Debug.Log("[CW] Bootstrap.Init() done");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[CW] Bootstrap FATAL: {ex}");
        }
    }
}
