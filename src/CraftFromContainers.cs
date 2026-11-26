using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>Merkt sich alle geladenen Kisten und liefert die nutzbaren in Reichweite.</summary>
    internal static class ContainerTracker
    {
        internal static readonly List<Container> All = new List<Container>();

        private static readonly AccessTools.FieldRef<Container, ZNetView> NView =
            AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

        private static List<Container> _cache = new List<Container>();
        private static float _cacheTime = -1f;

        internal static List<Container> Nearby(Player player)
        {
            // Kurz zwischenspeichern, weil CountItems sehr oft pro Frame aufgerufen wird
            if (Time.time - _cacheTime < 0.5f) return _cache;
            _cacheTime = Time.time;
            _cache = Query(player, Cfg.CfcRange.Value);
            return _cache;
        }

        /// <summary>Alle nutzbaren Kisten im Umkreis (ohne Zwischenspeicher).</summary>
        internal static List<Container> Query(Player player, float range)
        {
            var result = new List<Container>();
            All.RemoveAll(c => c == null);

            Vector3 pos = player.transform.position;
            long playerId = player.GetPlayerID();
            Inventory own = player.GetInventory();

            foreach (var c in All)
            {
                try
                {
                    var nv = NView(c);
                    if (nv == null || !nv.IsValid()) continue;
                    if (Vector3.Distance(pos, c.transform.position) > range) continue;
                    if (c.GetComponent<TombStone>() != null) continue;
                    if (Cfg.CfcIgnoreVehicles.Value && (c.GetComponentInParent<Ship>() != null || c.GetComponentInParent<Vagon>() != null)) continue;

                    var inv = c.GetInventory();
                    if (inv == null || inv == own) continue;
                    if (c.IsInUse()) continue;                 // von jemandem geoeffnet
                    if (!Util.CanAccess(c, playerId)) continue;    // private Kiste eines anderen
                    if (!PrivateArea.CheckAccess(c.transform.position, 0f, false, false)) continue; // fremder Schutzstein

                    result.Add(c);
                }
                catch { /* kaputte Kiste ueberspringen */ }
            }
            return result;
        }

        internal static void ClaimOwnership(Container c)
        {
            var nv = NView(c);
            if (nv != null && nv.IsValid() && !nv.IsOwner()) nv.ClaimOwnership();
        }

        internal static void Invalidate() => _cacheTime = -1f;
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class Container_Awake
    {
        private static void Postfix(Container __instance) => ContainerTracker.All.Add(__instance);
    }

    /// <summary>
    /// Inventory.CountItems(string name, ...) - beim Spieler-Inventar werden die Kisten mitgezaehlt.
    /// Dadurch zeigt das Crafting-Menue die Mengen inkl. Kisten an und "hast du genug?" stimmt.
    /// </summary>
    [HarmonyPatch]
    internal static class Inventory_CountItems
    {
        internal static MethodInfo Target;
        [ThreadStatic] internal static bool Suppress;

        private static MethodBase TargetMethod()
        {
            Target = AccessTools.GetDeclaredMethods(typeof(Inventory)).First(m =>
                m.Name == "CountItems" &&
                m.GetParameters().Length > 0 &&
                m.GetParameters()[0].ParameterType == typeof(string));
            return Target;
        }

        private static void Postfix(Inventory __instance, object[] __args, ref int __result)
        {
            if (Suppress || !Cfg.CfcEnabled.Value) return;
            var player = Player.m_localPlayer;
            if (player == null || __instance != player.GetInventory()) return;

            foreach (var c in ContainerTracker.Nearby(player))
            {
                try { __result += (int)Target.Invoke(c.GetInventory(), __args); }
                catch { }
            }
        }
    }

    /// <summary>
    /// Inventory.RemoveItem(string name, int amount, ...) - wenn der Spieler zu wenig hat,
    /// wird der Rest aus den Kisten genommen.
    /// </summary>
    [HarmonyPatch]
    internal static class Inventory_RemoveItemByName
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(Inventory)).First(m =>
                m.Name == "RemoveItem" &&
                m.GetParameters().Length >= 2 &&
                m.GetParameters()[0].ParameterType == typeof(string) &&
                m.GetParameters()[1].ParameterType == typeof(int));
        }

        private static void Prefix(Inventory __instance, MethodBase __originalMethod, object[] __args)
        {
            if (!Cfg.CfcEnabled.Value || Inventory_CountItems.Target == null) return;
            var player = Player.m_localPlayer;
            if (player == null || __instance != player.GetInventory()) return;

            const int amountIdx = 1;
            int amount = (int)__args[amountIdx];
            object[] countArgs = BuildCountArgs(__originalMethod, __args);

            int have;
            Inventory_CountItems.Suppress = true;
            try { have = (int)Inventory_CountItems.Target.Invoke(__instance, countArgs); }
            finally { Inventory_CountItems.Suppress = false; }

            if (have >= amount) return;

            int missing = amount - have;
            __args[amountIdx] = have; // Spieler gibt nur ab, was er hat

            foreach (var c in ContainerTracker.Nearby(player))
            {
                if (missing <= 0) break;
                var inv = c.GetInventory();
                int inChest;
                try { inChest = (int)Inventory_CountItems.Target.Invoke(inv, countArgs); }
                catch { continue; }
                if (inChest <= 0) continue;

                int take = Math.Min(inChest, missing);
                ContainerTracker.ClaimOwnership(c);

                var removeArgs = (object[])__args.Clone();
                removeArgs[amountIdx] = take;
                try
                {
                    __originalMethod.Invoke(inv, removeArgs);
                    missing -= take;
                }
                catch (Exception e)
                {
                    Plugin.LogWarn("Konnte nicht aus Kiste entnehmen: " + e.Message);
                }
            }
            ContainerTracker.Invalidate();
        }

        /// <summary>Baut aus den RemoveItem-Argumenten passende CountItems-Argumente (Name, Qualitaet, Weltlevel).</summary>
        private static object[] BuildCountArgs(MethodBase remove, object[] removeArgs)
        {
            var rps = remove.GetParameters();
            var cps = Inventory_CountItems.Target.GetParameters();

            string name = null;
            var ints = new List<object>();
            var bools = new List<object>();
            bool amountSkipped = false;

            for (int i = 0; i < rps.Length; i++)
            {
                Type t = rps[i].ParameterType;
                if (t == typeof(string)) name = (string)removeArgs[i];
                else if (t == typeof(int))
                {
                    if (!amountSkipped) { amountSkipped = true; continue; }
                    ints.Add(removeArgs[i]);
                }
                else if (t == typeof(bool)) bools.Add(removeArgs[i]);
            }

            var result = new object[cps.Length];
            int ii = 0, bi = 0;
            for (int i = 0; i < cps.Length; i++)
            {
                Type t = cps[i].ParameterType;
                if (t == typeof(string)) result[i] = name;
                else if (t == typeof(int)) result[i] = ii < ints.Count ? ints[ii++] : cps[i].DefaultValue;
                else if (t == typeof(bool)) result[i] = bi < bools.Count ? bools[bi++] : cps[i].DefaultValue;
                else result[i] = cps[i].DefaultValue;
            }
            return result;
        }
    }
}
