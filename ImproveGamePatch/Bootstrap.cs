using System;
using System.IO;
using System.Text;
using HarmonyLib;

namespace ImproveGamePatch
{
    public static class Bootstrap
    {
        private static bool _initialized;
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "ImproveGamePatch-harmony.txt");

        public static void Init()
        {
            if (_initialized)
                return;
            _initialized = true;

            var sb = new StringBuilder();
            sb.AppendLine(DateTime.Now + " ImproveGamePatch.Bootstrap.Init() start");

            try
            {
                var harmony = new Harmony("com.local.terraria.improvegame");

                foreach (var type in typeof(Bootstrap).Assembly.GetTypes())
                {
                    if (type.Namespace == null || !type.Namespace.StartsWith("ImproveGamePatch.Patches"))
                        continue;
                    try
                    {
                        var proc = new PatchClassProcessor(harmony, type);
                        var result = proc.Patch();
                        sb.AppendLine("  OK   " + type.Name + " -> " + (result == null ? "null" : result.Count + " patch(es)"));
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine("  FAIL " + type.Name + ": " + ex.ToString());
                    }
                }

                sb.AppendLine(DateTime.Now + " ImproveGamePatch.Bootstrap.Init() done");
            }
            catch (Exception ex)
            {
                sb.AppendLine(DateTime.Now + " FATAL: " + ex);
            }
            finally
            {
                try { AppendLog(LogPath, sb.ToString()); } catch { }
            }
        }

        private static void AppendLog(string path, string text)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(path, text, Encoding.UTF8);
        }
    }
}
