using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>Beim Benutzen einer Werkbank: Ausruestung und Gebaeude reparieren.</summary>
    [HarmonyPatch(typeof(CraftingStation), "Interact")]
    internal static class CraftingStation_Interact_Repair
    {
        private static void Postfix(CraftingStation __instance, object[] __args)
        {
            var user = __args.Length > 0 ? __args[0] as Player : null;
            bool repeat = __args.Length > 1 && __args[1] is bool r && r;
            if (user == null || user != Player.m_localPlayer || repeat) return;

            if (Cfg.AutoRepairItems.Value)
            {
                int n = 0;
                foreach (var item in user.GetInventory().GetAllItems())
                {
                    if (item == null || !item.m_shared.m_useDurability) continue;
                    float max = item.GetMaxDurability();
                    if (item.m_durability < max)
                    {
                        item.m_durability = max;
                        n++;
                    }
                }
                if (n > 0) user.Message(MessageHud.MessageType.TopLeft, n + L.T(" Gegenstände repariert", " items repaired"));
            }

            if (Cfg.AreaRepair.Value)
            {
                var all = AccessTools.Field(typeof(WearNTear), "s_allInstances")?.GetValue(null) as IList;
                if (all == null) return;
                float radius = Cfg.AreaRepairRadius.Value;
                Vector3 pos = __instance.transform.position;
                int n = 0;
                foreach (var o in all)
                {
                    var w = o as WearNTear;
                    if (w == null || Vector3.Distance(w.transform.position, pos) > radius) continue;
                    if (w.GetHealthPercentage() >= 1f) continue;
                    if (Util.Call(w, "Repair") is bool ok && ok) n++;
                }
                if (n > 0) user.Message(MessageHud.MessageType.TopLeft, n + L.T(" Bauteile repariert", " building pieces repaired"));
            }
        }
    }

    /// <summary>Bauen in Dungeons und an Orten mit Bauverbot.</summary>
    [HarmonyPatch(typeof(Location), "IsInsideNoBuildLocation")]
    internal static class Location_NoBuild
    {
        private static void Postfix(ref bool __result)
        {
            if (Cfg.BuildInDungeons.Value) __result = false;
        }
    }

    /// <summary>
    /// Valheim 1.0: In Innenräumen (Dungeons) prüft das Bauen zusätzlich
    /// EnvMan.CheckInteriorBuildingOverride – hier "erlaubt" melden.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), "CheckInteriorBuildingOverride")]
    internal static class EnvMan_InteriorBuild
    {
        private static void Postfix(ref bool __result)
        {
            if (Cfg.BuildInDungeons.Value) __result = true;
        }
    }

    /// <summary>Boden um Portale schuetzen: Bodenveraenderungen in der Nähe eines Portals werden verworfen.</summary>
    [HarmonyPatch(typeof(TerrainOp), "Awake")]
    internal static class TerrainOp_PortalProtection
    {
        private static bool Prefix(TerrainOp __instance)
        {
            if (!Cfg.PortalTerrainProtection.Value) return true;
            Vector3 pos = __instance.transform.position;
            float r = Cfg.PortalTerrainRadius.Value;
            foreach (var col in Physics.OverlapSphere(pos, r))
            {
                if (col && col.GetComponentInParent<TeleportWorld>() != null)
                {
                    Object.Destroy(__instance.gameObject);
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Waffe und Schild beim Schwimmen nicht wegstecken.</summary>
    [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
    internal static class Humanoid_HideHandItems_Swim
    {
        private static bool Prefix(Humanoid __instance)
        {
            if (!Cfg.KeepEquipmentSwimming.Value) return true;
            return !(__instance == Player.m_localPlayer && __instance.IsSwimming());
        }
    }
}
