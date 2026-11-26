using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;

namespace Pscht_QoL
{
    /// <summary>
    /// Server-Sync: Beim Einloggen holt sich jeder Spieler die Werte vom Server (bzw. vom Host).
    /// Die eigene Konfig-Datei wird dabei NICHT veraendert; beim Ausloggen gelten wieder die eigenen Werte.
    /// Tasten werden nicht uebertragen.
    /// </summary>
    internal static class ConfigSync
    {
        private const string RpcRequest = "DQoL_CfgRequest";
        private const string RpcData = "DQoL_CfgData";
        private const string SyncSection = Cfg.SyncSection;

        private static Dictionary<ConfigEntryBase, string> _backup;
        internal static bool Synced => _backup != null;

        private static ConfigFile File => Plugin.Instance != null ? Plugin.Instance.Config : null;

        private static bool Syncable(ConfigEntryBase e) =>
            e != null && e.SettingType != typeof(KeyboardShortcut) && e.SettingType != typeof(UnityEngine.KeyCode)
            && e.Definition.Section != SyncSection
            && !e.Definition.Key.StartsWith("Uhr")              // Uhr-Anzeige stellt jeder selbst ein
            && !e.Definition.Key.StartsWith("Anzeige")
            && !e.Definition.Key.StartsWith("Raster")           // Kultivator-Raster stellt jeder selbst ein
            && e.Definition.Section != Cfg.ExtraSlotsSection    // Extra-Slots: Inventar jedes Spielers
            && e.Definition.Section != Cfg.RadarSection         // Radar: stellt jeder selbst ein
            && e.Definition.Section != Cfg.HelpersSection       // Inventar-Hilfen: stellt jeder selbst ein
            && e.Definition.Section != Cfg.LanguageSection      // Sprache: stellt jeder selbst ein
            && e.Definition.Section != Cfg.AchievementSection;  // Steam-Erfolge: Sache jedes Spielers

        internal static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(RpcRequest, OnRequest);
            ZRoutedRpc.instance.Register<ZPackage>(RpcData, OnData);
        }

        internal static void RequestIfClient()
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null) return;
            if (!Cfg.SyncReceive.Value) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcRequest, new ZPackage());
        }

        // Server: Werte schicken
        private static void OnRequest(long sender, ZPackage _)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !Cfg.SyncSend.Value || File == null) return;
            var entries = File.Where(kv => Syncable(kv.Value)).ToList();
            var pkg = new ZPackage();
            pkg.Write(entries.Count);
            foreach (var kv in entries)
            {
                pkg.Write(kv.Key.Section);
                pkg.Write(kv.Key.Key);
                pkg.Write(kv.Value.GetSerializedValue());
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcData, pkg);
        }

        // Spieler: Werte uebernehmen
        private static void OnData(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || !Cfg.SyncReceive.Value || File == null) return;
            var file = File;
            file.SaveOnConfigSet = false; // Server-Werte nicht in die eigene Datei schreiben
            if (_backup == null) _backup = new Dictionary<ConfigEntryBase, string>();

            int n = pkg.ReadInt(), applied = 0;
            for (int i = 0; i < n; i++)
            {
                string section = pkg.ReadString(), key = pkg.ReadString(), value = pkg.ReadString();
                var def = new ConfigDefinition(section, key);
                if (!file.ContainsKey(def)) continue;
                var entry = file[def];
                if (!Syncable(entry)) continue;
                if (!_backup.ContainsKey(entry)) _backup[entry] = entry.GetSerializedValue();
                try { entry.SetSerializedValue(value); applied++; } catch { }
            }

            Reapply();
            Plugin.LogInfo("Server-Sync: " + applied + " Einstellungen übernommen");
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, L.T("All in One QoL: Einstellungen vom Server übernommen", "All in One QoL: settings received from the server"));
        }

        /// <summary>Beim Ausloggen: eigene Werte zurueck.</summary>
        internal static void Restore()
        {
            var file = File;
            if (_backup == null || file == null) return;
            foreach (var kv in _backup)
            {
                try { kv.Key.SetSerializedValue(kv.Value); } catch { }
            }
            _backup = null;
            file.SaveOnConfigSet = true;
        }

        /// <summary>Werte, die sonst nur beim Laden greifen, sofort neu anwenden.</summary>
        private static void Reapply()
        {
            if (ObjectDB.instance != null) ItemTweaks.Apply(ObjectDB.instance);
            if (Player.m_localPlayer != null) Player_Awake.Apply(Player.m_localPlayer);
            if (EnvMan.instance != null) DayNight.Apply(EnvMan.instance);
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class Game_Start_RegisterConfigSync
    {
        private static void Postfix() => ConfigSync.Register();
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class Player_OnSpawned_ConfigSync
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer) ConfigSync.RequestIfClient();
        }
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    internal static class ZNet_OnDestroy_ConfigSync
    {
        private static void Postfix() => ConfigSync.Restore();
    }
}
