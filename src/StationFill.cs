using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Befüllen von Schmelzofen, Hochofen, Meiler, Spinnrad, Windmühle, Eitr-Raffinerie usw. (alles "Smelter"):
    /// - Material und Brennstoff auch aus Kisten in der Nähe
    /// - Meiler (und alles, was Holz nimmt): nur normales Holz, kein Feinholz/Kernholz
    /// - Taste halten (Standard: linke Strg) + E = bis zur Max-Füllmenge auffüllen
    /// </summary>
    internal static class StationFill
    {
        private const string WoodPrefab = "Wood";

        private static bool FillToMax => Input.GetKey(Cfg.FillMaxKey.Value);

        // ---------- Zählen / Entnehmen (Inventar zuerst, dann Kisten) ----------

        private static List<Container> Chests(Player player) =>
            Cfg.FillFromChests.Value ? ContainerTracker.Query(player, Cfg.FillChestRange.Value) : new List<Container>();

        private static int OwnCount(Inventory inv, string name)
        {
            Inventory_CountItems.Suppress = true;
            try { return inv.CountItems(name); }
            finally { Inventory_CountItems.Suppress = false; }
        }

        internal static int Count(Player player, string name)
        {
            int n = OwnCount(player.GetInventory(), name);
            foreach (var c in Chests(player))
            {
                try { n += c.GetInventory().CountItems(name); } catch { }
            }
            return n;
        }

        internal static void Take(Player player, string name, int amount)
        {
            var inv = player.GetInventory();
            int own = Mathf.Min(amount, OwnCount(inv, name));
            if (own > 0) { inv.RemoveItem(name, own); amount -= own; }
            if (amount <= 0) return;

            foreach (var c in Chests(player))
            {
                if (amount <= 0) break;
                var ci = c.GetInventory();
                int have;
                try { have = ci.CountItems(name); } catch { continue; }
                if (have <= 0) continue;
                int t = Mathf.Min(have, amount);
                ContainerTracker.ClaimOwnership(c);
                ci.RemoveItem(name, t);
                amount -= t;
            }
            ContainerTracker.Invalidate();
        }

        // ---------- Material (Erz, Holz, Flachs, Gerste …) ----------

        internal static bool AddOre(Smelter s, Player player)
        {
            var nv = Util.GetField(s, "m_nview") as ZNetView;
            if (nv == null || !nv.IsValid()) return false;

            int free = s.m_maxOre - nv.GetZDO().GetInt(ZDOVars.s_queued);
            if (free <= 0) { player.Message(MessageHud.MessageType.Center, "$msg_itsfull"); return false; }
            int want = FillToMax ? free : 1;

            // Welche Rohstoffe nimmt die Station? Holz-Stationen: erst nur normales Holz,
            // anderes Holz nur, wenn "Anderes Holz wenn normales leer" an ist.
            var first = new List<Smelter.ItemConversion>();
            var rest = new List<Smelter.ItemConversion>();
            bool woodOnly = false;
            foreach (var conv in s.m_conversion)
                if (conv != null && conv.m_from != null && conv.m_from.gameObject.name == WoodPrefab && Cfg.FillOnlyWood.Value)
                { first.Add(conv); woodOnly = true; break; }
            foreach (var conv in s.m_conversion)
                if (conv != null && conv.m_from != null && !first.Contains(conv)) (woodOnly ? rest : first).Add(conv);

            int added = 0, woodAdded = 0;
            string lastName = null;
            void Fill(List<Smelter.ItemConversion> list)
            {
                foreach (var conv in list)
                {
                    if (added >= want) break;
                    string name = conv.m_from.m_itemData.m_shared.m_name;
                    int have = Count(player, name);
                    if (have <= 0) continue;
                    int n = Mathf.Min(have, want - added);
                    Take(player, name, n);
                    for (int i = 0; i < n; i++) nv.InvokeRPC("RPC_AddOre", conv.m_from.gameObject.name, false);
                    added += n;
                    lastName = name;
                }
            }

            Fill(first);
            woodAdded = added;
            bool woodEmpty = woodOnly && added < want;
            if (woodEmpty && Cfg.FillOtherWood.Value) Fill(rest);

            if (added == 0)
            {
                player.Message(MessageHud.MessageType.Center, woodOnly ? L.T("Kein normales Holz da (Inventar und Kisten)", "No normal wood (inventory and chests)") : "$msg_noprocessableitems");
                return false;
            }

            Util.SetField(s, "m_addedOreTime", Time.time);
            if (woodEmpty && added > woodAdded)
                player.Message(MessageHud.MessageType.Center, L.T("Normales Holz leer – ", "Out of normal wood – ") + woodAdded + L.T(" Holz + ", " wood + ") + (added - woodAdded) + L.T(" anderes Holz eingefüllt", " other wood added"));
            else if (woodEmpty)
                player.Message(MessageHud.MessageType.Center, L.T("Normales Holz leer – ", "Out of normal wood – ") + added + L.T(" eingefüllt", " added"));
            else
                player.Message(MessageHud.MessageType.Center, "$msg_added " + lastName + (added > 1 ? " ×" + added : ""));
            return true;
        }

        // ---------- Brennstoff (Kohle, Holz, Harz …) ----------

        internal static bool AddFuel(Smelter s, Player player)
        {
            var nv = Util.GetField(s, "m_nview") as ZNetView;
            if (nv == null || !nv.IsValid() || s.m_fuelItem == null) return false;

            float fuel = nv.GetZDO().GetFloat(ZDOVars.s_fuel);
            int free = Mathf.FloorToInt(s.m_maxFuel - fuel);
            if (free <= 0) { player.Message(MessageHud.MessageType.Center, "$msg_itsfull"); return false; }
            int want = FillToMax ? free : 1;

            string name = s.m_fuelItem.m_itemData.m_shared.m_name;
            int have = Count(player, name);
            if (have <= 0) { player.Message(MessageHud.MessageType.Center, "$msg_donthaveany " + name); return false; }

            int n = Mathf.Min(have, want);
            Take(player, name, n);
            for (int i = 0; i < n; i++) nv.InvokeRPC("RPC_AddFuel");

            bool isWood = s.m_fuelItem.gameObject.name == WoodPrefab;
            if (isWood && n < want && FillToMax)
                player.Message(MessageHud.MessageType.Center, L.T("Normales Holz leer – ", "Out of normal wood – ") + n + L.T(" eingefüllt", " added"));
            else
                player.Message(MessageHud.MessageType.Center, "$msg_added " + name + (n > 1 ? " ×" + n : ""));
            return true;
        }
    }

    /// <summary>Beim Anschauen: zweite Zeile "[Shift links + E] Max. befüllen".</summary>
    [HarmonyPatch]
    internal static class Smelter_Hover_FillHint
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "OnHoverAddOre", "OnHoverAddFuel" })
            {
                var m = AccessTools.Method(typeof(Smelter), name);
                if (m != null) yield return m;
            }
        }

        internal static string KeyName(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftShift: return L.T("Shift links", "Left Shift");
                case KeyCode.RightShift: return L.T("Shift rechts", "Right Shift");
                case KeyCode.LeftControl: return L.T("Strg links", "Left Ctrl");
                case KeyCode.RightControl: return L.T("Strg rechts", "Right Ctrl");
                case KeyCode.LeftAlt: return L.T("Alt links", "Left Alt");
                case KeyCode.RightAlt: return "Alt Gr";
                default: return k.ToString();
            }
        }

        private static void Postfix(ref string __result)
        {
            if (!Cfg.ProcessingEnabled.Value || !Cfg.FillEnabled.Value || string.IsNullOrEmpty(__result)) return;
            string hint = "[<color=yellow><b>" + KeyName(Cfg.FillMaxKey.Value) + " + $KEY_Use</b></color>] " + L.T("Max. befüllen", "Fill to max");
            __result += "\n" + Localization.instance.Localize(hint);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class Smelter_OnAddOre_Fill
    {
        private static bool Prefix(Smelter __instance, object[] __args, ref bool __result)
        {
            if (!Cfg.ProcessingEnabled.Value || !Cfg.FillEnabled.Value || __args.Length < 3) return true;
            if (__args[2] != null) return true;   // Item aus der Schnellleiste benutzt -> Vanilla
            var player = __args[1] as Player;
            if (player == null || player != Player.m_localPlayer) return true;
            __result = StationFill.AddOre(__instance, player);
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class Smelter_OnAddFuel_Fill
    {
        private static bool Prefix(Smelter __instance, object[] __args, ref bool __result)
        {
            if (!Cfg.ProcessingEnabled.Value || !Cfg.FillEnabled.Value || __args.Length < 3) return true;
            if (__args[2] != null) return true;
            var player = __args[1] as Player;
            if (player == null || player != Player.m_localPlayer || __instance.m_fuelItem == null) return true;
            __result = StationFill.AddFuel(__instance, player);
            return false;
        }
    }
}
