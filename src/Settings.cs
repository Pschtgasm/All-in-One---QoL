using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;

namespace Pscht_QoL
{
    /// <summary>
    /// Fester Wert, der NICHT in der Konfig steht (wurde aus der Datei entfernt).
    /// Hat ".Value" wie ein Konfig-Eintrag, damit der übrige Code gleich bleibt.
    /// </summary>
    internal sealed class Fixed<T>
    {
        public T Value { get; set; }
        public Fixed(T value) { Value = value; }
    }

    /// <summary>
    /// Wert, der im Spiel eingestellt wird (z.B. im Radar-Fenster F7 oder mit Strg+Pfeiltasten) und in einer
    /// eigenen kleinen Datei gespeichert wird – nicht in der Konfig: BepInEx\config\pscht.allinoneqol.local.txt
    /// </summary>
    internal sealed class Stored<T>
    {
        private readonly string _key;
        private readonly T _default;
        public Stored(string key, T def) { _key = key; _default = def; }

        public T Value
        {
            get => LocalSettings.Get(_key, _default);
            set => LocalSettings.Set(_key, value);
        }

        /// <summary>Wert aus alter Konfig übernehmen, aber nur wenn noch nichts gespeichert ist.</summary>
        internal void MigrateFrom(string serialized)
        {
            if (!LocalSettings.Has(_key) && serialized != null) LocalSettings.SetRaw(_key, serialized.Trim());
        }
    }

    internal static class LocalSettings
    {
        private static Dictionary<string, string> _values;
        private static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".local.txt");

        private static void Load()
        {
            if (_values != null) return;
            _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0 || line.StartsWith("#")) continue;
                    _values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch (Exception e) { Plugin.LogWarn("Lokale Einstellungen lesen: " + e.Message); }
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string> { "# All in One QoL – Einstellungen aus dem Spiel (Radar-Fenster F7, Kultivator-Raster). Nicht von Hand nötig." };
                foreach (var kv in _values) lines.Add(kv.Key + " = " + kv.Value);
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception e) { Plugin.LogWarn("Lokale Einstellungen speichern: " + e.Message); }
        }

        internal static bool Has(string key) { Load(); return _values.ContainsKey(key); }

        internal static void SetRaw(string key, string value) { Load(); _values[key] = value; Save(); }

        internal static T Get<T>(string key, T def)
        {
            Load();
            if (!_values.TryGetValue(key, out var s)) return def;
            try
            {
                object v;
                if (typeof(T) == typeof(bool)) v = s.Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (typeof(T) == typeof(int)) v = int.Parse(s, CultureInfo.InvariantCulture);
                else if (typeof(T) == typeof(float)) v = float.Parse(s, CultureInfo.InvariantCulture);
                else v = s;
                return (T)v;
            }
            catch { return def; }
        }

        internal static void Set<T>(string key, T value)
        {
            Load();
            string s = value is bool b ? (b ? "true" : "false")
                     : value is float f ? f.ToString(CultureInfo.InvariantCulture)
                     : value is int i ? i.ToString(CultureInfo.InvariantCulture)
                     : value?.ToString() ?? "";
            if (_values.TryGetValue(key, out var old) && old == s) return;
            _values[key] = s;
            Save();
        }
    }
}
