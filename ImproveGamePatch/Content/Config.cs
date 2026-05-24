using System;
using System.Collections.Generic;
using System.IO;
using Terraria;

namespace ImproveGamePatch.Content
{
    /// <summary>
    /// Unified configuration. Reads/writes JSON at:
    ///   {SavePath}/ImproveGamePatch/config.json
    /// Call Config.Load() once; all Get() calls auto-load.
    /// Call Config.SetAndSave(key, value) to persist changes.
    /// </summary>
    public static class Config
    {
        private static Dictionary<string, object> _values;
        private static bool _loaded;
        private static string _path;

        public static string ConfigPath
        {
            get
            {
                if (_path == null)
                {
                    string save = Main.SavePath;
                    if (string.IsNullOrEmpty(save))
                        save = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "My Games", "Terraria");
                    _path = Path.Combine(save, "ImproveGamePatch", "config.json");
                }
                return _path;
            }
        }

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _values = GetDefaults();

            try
            {
                string p = ConfigPath;
                if (File.Exists(p))
                {
                    string json = File.ReadAllText(p);
                    var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (loaded != null)
                        foreach (var kv in loaded)
                            _values[kv.Key] = kv.Value;
                }
            }
            catch { }
        }

        public static bool Get(string key, bool defaultVal)
        {
            Load();
            if (_values.TryGetValue(key, out object val))
            {
                if (val is bool b) return b;
                if (val is string s && bool.TryParse(s, out bool sb)) return sb;
            }
            return defaultVal;
        }

        /// <summary>
        /// Set a config value and persist to disk immediately.
        /// </summary>
        public static void SetAndSave(string key, bool value)
        {
            Load();
            _values[key] = value;
            Save();
        }

        /// <summary>
        /// Get all config keys for UI display.
        /// </summary>
        public static IEnumerable<string> GetAllKeys()
        {
            Load();
            return _values.Keys;
        }

        public static void Save()
        {
            try
            {
                string p = ConfigPath;
                string dir = Path.GetDirectoryName(p);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_values, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(p, json);
            }
            catch { }
        }

        private static Dictionary<string, object> GetDefaults()
        {
            return new Dictionary<string, object>
            {
                ["VeinMiner"] = true,
                ["AutoPiggyBank"] = true,
                ["HigherTree"] = true,
                ["BannerPatch"] = true,
                ["FasterExtractinator"] = true,
                ["PortableStation"] = true,
                ["InfiniteBuff"] = true,
                ["HomeTeleport"] = true,
                ["RefreshTravelShop"] = true,
                ["WeatherControl"] = true,
                ["ForceFestival"] = true,
                ["AutoTrash"] = true,
                ["BigBag"] = true,
            };
        }
    }
}
