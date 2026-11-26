using System;
using BepInEx;
using HarmonyLib;

namespace Pscht_QoL
{
    [BepInPlugin(Guid, ModName, Version)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "pscht.allinoneqol";
        public const string ModName = "All in One QoL";
        public const string Version = "1.1.0";
        /// <summary>Konfig-Datei vor 1.0 (BepInEx\config\pscht.qol.cfg) – wird beim ersten Start übernommen.</summary>
        internal const string OldGuid = "pscht.qol";

        internal static Plugin Instance;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            TakeOverOldConfigFile();
            Cfg.Bind(Config);
            InventorySize.Hook();
            RemoveOldConfigEntries();
            Exploration.Init();
            CustomSkills.Init();

            _harmony = new Harmony(Guid);

            // Jede Patch-Klasse einzeln anwenden: Wenn ein Patch nach einem
            // Valheim-Update nicht mehr passt, laufen alle anderen trotzdem weiter.
            int ok = 0, failed = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    Logger.LogWarning("Patch " + type.Name + " fehlgeschlagen: " + e.Message);
                }
            }

            Logger.LogInfo(ModName + " " + Version + " geladen (" + ok + " Patches OK, " + failed + " fehlgeschlagen)");
        }

        /// <summary>
        /// Neuer Name ab 1.0: alte Einstellungen (pscht.qol.cfg / pscht.qol.local.txt) einmalig übernehmen,
        /// solange es die neue Datei noch nicht gibt. Die alte Datei bleibt als Sicherung liegen.
        /// </summary>
        private void TakeOverOldConfigFile()
        {
            try
            {
                string dir = Paths.ConfigPath;
                string oldCfg = System.IO.Path.Combine(dir, OldGuid + ".cfg");
                if (!System.IO.File.Exists(Config.ConfigFilePath) && System.IO.File.Exists(oldCfg))
                {
                    System.IO.File.Copy(oldCfg, Config.ConfigFilePath);
                    Config.Reload();
                    Logger.LogInfo("Konfig: Einstellungen aus " + OldGuid + ".cfg übernommen");
                }
                string oldLocal = System.IO.Path.Combine(dir, OldGuid + ".local.txt");
                string newLocal = System.IO.Path.Combine(dir, Guid + ".local.txt");
                if (!System.IO.File.Exists(newLocal) && System.IO.File.Exists(oldLocal))
                    System.IO.File.Copy(oldLocal, newLocal);
            }
            catch (Exception e) { Logger.LogWarning("Alte Konfig übernehmen fehlgeschlagen: " + e.Message); }
        }

        /// <summary>
        /// Entfernt Einträge aus älteren Versionen (z.B. "1 - Zaehmen"), die es nicht mehr gibt.
        /// BepInEx behält solche "verwaisten" Einträge sonst für immer in der Datei.
        /// </summary>
        private void RemoveOldConfigEntries()
        {
            try
            {
                var prop = AccessTools.Property(typeof(BepInEx.Configuration.ConfigFile), "OrphanedEntries");
                var orphans = prop?.GetValue(Config, null) as System.Collections.IDictionary;
                if (orphans == null || orphans.Count == 0) return;
                // Werte aus umbenannten / zusammengelegten Einträgen übernehmen, bevor sie gelöscht werden
                int taken = Cfg.Migrate(Config, orphans);
                if (taken > 0) Logger.LogInfo("Konfig: " + taken + " Werte aus alten Einträgen übernommen");
                int n = orphans.Count;
                orphans.Clear();
                Config.Save();
                Logger.LogInfo("Konfig aufgeräumt: " + n + " alte Einträge entfernt");
            }
            catch (Exception e)
            {
                Logger.LogWarning("Konfig aufräumen fehlgeschlagen: " + e.Message);
            }
        }

        private void Update()
        {
            try { ChestFeatures.HandleInput(); }
            catch (Exception e) { Logger.LogWarning("Tastenfehler: " + e.Message); }
            try { ExtraSlots.HandleInput(); ExtraSlots.Tick(); BeltSlot.Tick(); }
            catch (Exception e) { Logger.LogWarning("Extra-Slots: " + e.Message); }
            try { TamesExtra.HandleInput(); TamesExtra.Tick(); }
            catch (Exception e) { Logger.LogWarning("Tiere: " + e.Message); }
            try { Clock.HandleInput(); }
            catch (Exception e) { Logger.LogWarning("Uhr: " + e.Message); }
            try { PortalMenu.Update(); PortalUI.Update(); }
            catch (Exception e) { Logger.LogWarning("Portalmenü: " + e.Message); }
            try { CultivatorBushes.Tick(); CultivatorGrid.Tick(); }
            catch (Exception e) { Logger.LogWarning("Kultivator-Raster: " + e.Message); }
            try { Radar.Tick(); }
            catch (Exception e) { Logger.LogWarning("Radar: " + e.Message); }
            try { PauseSync.Tick(); }
            catch (Exception e) { Logger.LogWarning("Pause: " + e.Message); }
            try { InventoryHelpers.HandleInput(); InventoryHelpers.Tick(); }
            catch (Exception e) { Logger.LogWarning("Favoriten: " + e.Message); }
            try { CustomSkills.Tick(); }
            catch (Exception e) { Logger.LogWarning("Skills: " + e.Message); }
        }

        private void OnGUI()
        {
            try { PortalMenu.OnGUI(); }
            catch (Exception e) { Logger.LogWarning("Portalmenü: " + e.Message); }
            try { Clock.OnGUI(); }
            catch (Exception e) { Logger.LogWarning("Uhr: " + e.Message); }
            try { Radar.OnGUI(); }
            catch (Exception e) { Logger.LogWarning("Radar: " + e.Message); }
            try { PauseSync.OnGUI(); }
            catch (Exception e) { Logger.LogWarning("Pause: " + e.Message); }
            try { CultivatorGrid.OnGUI(); }
            catch (Exception e) { Logger.LogWarning("Raster: " + e.Message); }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        internal static void LogInfo(string msg) => Instance?.Logger.LogInfo(msg);
        internal static void LogWarn(string msg) => Instance?.Logger.LogWarning(msg);
    }
}
