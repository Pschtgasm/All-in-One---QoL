using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Größeres Inventar: zusätzliche Reihen aus der Konfig (04 - Spieler).
    /// Valheim 1.0 kennt selbst eine Reihenzahl ("invrows", beim Händler kaufbar) – die bleibt im Spielstand
    /// unverändert, unsere Reihen kommen nur obendrauf. Ohne Mod hat der Charakter wieder seine normale Größe.
    /// </summary>
    internal static class InventorySize
    {
        internal const int MaxRows = 9;

        internal static int Extra => Cfg.PlayerEnabled.Value ? Mathf.Clamp(Cfg.InvExtraRows.Value, 0, 5) : 0;

        /// <summary>Gekaufte/normale Reihen laut Spielstand (Standard 4).</summary>
        internal static int VanillaRows(Player p)
        {
            return p != null && p.TryGetUniqueKeyValue("invrows", out var s) && int.TryParse(s, out int rows) ? rows : 4;
        }

        /// <summary>Nach einer Konfig-Änderung sofort neu setzen.</summary>
        internal static void Apply()
        {
            var p = Player.m_localPlayer;
            if (p == null || InventoryGui.instance == null) return;
            try { p.SetInventorySize(VanillaRows(p)); }
            catch (System.Exception e) { Plugin.LogWarn("Inventargröße: " + e.Message); }
        }

        internal static void Hook()
        {
            Cfg.InvExtraRows.SettingChanged += (s, e) => Apply();
            Cfg.PlayerEnabled.SettingChanged += (s, e) => Apply();
        }
    }

    /// <summary>
    /// Player.SetInventorySize(rows): rows = Reihen laut Spielstand. Wir setzen das Inventar auf rows + Extra,
    /// speichern aber weiter nur rows (sonst würde es bei jedem Einloggen größer).
    /// </summary>
    [HarmonyPatch(typeof(Player), "SetInventorySize")]
    internal static class Player_SetInventorySize_Bigger
    {
        private static bool Prefix(Player __instance, object[] __args)
        {
            if (__args == null || __args.Length == 0 || !(__args[0] is int rows)) return true;
            int extra = InventorySize.Extra;
            if (extra <= 0) return true;
            rows = Mathf.Clamp(rows, 0, InventorySize.MaxRows);
            int total = Mathf.Clamp(rows + extra, 1, InventorySize.MaxRows);
            __instance.GetInventory().SetHeight(total);   // Extra-Slots hängen ihre Reihen selbst an
            __instance.AddUniqueKeyValue("invrows", rows.ToString());
            if (InventoryGui.instance != null) InventoryGui.instance.SetInventorySize(total);
            __instance.DropInvalidItems();
            return false;
        }
    }
}
