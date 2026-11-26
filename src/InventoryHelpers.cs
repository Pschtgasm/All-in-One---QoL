using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Pscht_QoL
{
    /// <summary>
    /// 21 - Inventar-Hilfen:
    /// - Favoriten: Mausrad-Klick auf ein Item = Favorit mit der aktuellen Menge (gelber Rahmen).
    ///   Wird davon etwas verbraucht, füllt der Mod aus Kisten in der Nähe wieder auf diese Menge auf.
    /// - Vergleich: im Tooltip von Waffe/Schild/Rüstung der Unterschied zum Angelegten.
    /// </summary>
    internal static class InventoryHelpers
    {
        // ===================== Favoriten =====================

        /// <summary>Zielmenge, gespeichert direkt am Item (wird mit dem Charakter gespeichert).</summary>
        internal const string FavKey = "aioqol_fav";

        /// <summary>> 0, solange Valheim gerade etwas verbraucht (Essen, Pfeil, Wurfwaffe, kaputte Fackel).</summary>
        internal static int Consuming;

        /// <summary>Ganz aufgebrauchte Favoriten: Stelle merken und neu auffüllen.</summary>
        private class Gone
        {
            public ItemDrop.ItemData Item;
            public Vector2i Pos;
            public int Amount;
            public float Since;
        }
        private static readonly List<Gone> Pending = new List<Gone>();
        private static float _next;

        internal static int FavAmount(ItemDrop.ItemData it)
        {
            if (it?.m_customData == null) return 0;
            return it.m_customData.TryGetValue(FavKey, out var s) && int.TryParse(s, out int n) ? n : 0;
        }

        private static bool Mine(Inventory inv) => inv != null && Player.m_localPlayer != null && Player.m_localPlayer.GetInventory() == inv;

        /// <summary>Mausrad-Klick (einstellbar) im Inventar: Favorit an/aus.</summary>
        internal static void HandleInput()
        {
            if (!Cfg.FavoritesEnabled.Value || !InventoryGui.IsVisible()) return;
            if (!Input.GetKeyDown(Cfg.FavoriteKey.Value)) return;
            var p = Player.m_localPlayer;
            var gui = InventoryGui.instance;
            if (p == null || gui == null) return;
            var item = ChestFeatures.HoveredItem(gui, out var inv);
            if (item == null || inv != p.GetInventory()) return;
            string name = Localization.instance.Localize(item.m_shared.m_name);
            if (FavAmount(item) > 0)
            {
                item.m_customData.Remove(FavKey);
                p.Message(MessageHud.MessageType.Center, L.T("Kein Favorit mehr: ", "No longer a favorite: ") + name);
            }
            else
            {
                item.m_customData[FavKey] = item.m_stack.ToString();
                p.Message(MessageHud.MessageType.Center, L.T("Favorit: ", "Favorite: ") + item.m_stack + "× " + name + L.T(" – wird aus Kisten aufgefüllt", " – refilled from chests"));
            }
            Util.Call(inv, "Changed");
        }

        // ---------- Verbrauch erkennen (für ganz aufgebrauchte Stapel) ----------

        internal static bool WillVanish(Inventory inv, ItemDrop.ItemData item, bool onlyOne)
        {
            if (!Cfg.FavoritesEnabled.Value || item == null || !Mine(inv) || FavAmount(item) <= 0) return false;
            return !onlyOne || item.m_stack <= 1;
        }

        internal static void Vanished(Inventory inv, ItemDrop.ItemData item)
        {
            if (item == null || inv.ContainsItem(item)) return;
            Pending.RemoveAll(g => g.Pos.x == item.m_gridPos.x && g.Pos.y == item.m_gridPos.y);
            Pending.Add(new Gone { Item = item, Pos = item.m_gridPos, Amount = FavAmount(item), Since = Time.time });
            _next = 0f;   // gleich auffüllen
        }

        internal static List<ItemDrop.ItemData> FavsNamed(Inventory inv, string name)
        {
            if (!Cfg.FavoritesEnabled.Value || !Mine(inv) || name == null) return null;
            List<ItemDrop.ItemData> list = null;
            foreach (var it in inv.GetAllItems())
                if (it != null && it.m_shared.m_name == name && FavAmount(it) > 0)
                    (list ?? (list = new List<ItemDrop.ItemData>())).Add(it);
            return list;
        }

        // ---------- Auffüllen aus Kisten ----------

        /// <summary>Jede Sekunde (Plugin.Update): Favoriten unter ihrer Menge aus Kisten in der Nähe auffüllen.</summary>
        internal static void Tick()
        {
            if (!Cfg.FavoritesEnabled.Value) { Pending.Clear(); return; }
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if (Time.time < _next) return;
            _next = Time.time + 1f;
            if (InventoryGui.IsVisible()) return;     // nicht während du im Inventar räumst

            var inv = p.GetInventory();
            List<Container> chests = null;
            bool changed = false;

            foreach (var it in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                int target = FavAmount(it);
                if (target <= 0) continue;
                int want = Math.Min(target, it.m_shared.m_maxStackSize) - it.m_stack;
                if (want <= 0) continue;
                if (chests == null) chests = ContainerTracker.Query(p, Cfg.FavoriteRange.Value);
                int got = Take(chests, it, want);
                if (got <= 0) continue;
                it.m_stack += got;
                changed = true;
                Notify(p, it, got);
            }

            foreach (var g in new List<Gone>(Pending))
            {
                var there = inv.GetItemAt(g.Pos.x, g.Pos.y);
                if (there != null) { Pending.Remove(g); continue; }        // Platz anders belegt
                if (Time.time - g.Since > 120f) { Pending.Remove(g); continue; }
                if (chests == null) chests = ContainerTracker.Query(p, Cfg.FavoriteRange.Value);
                int got = Take(chests, g.Item, Math.Min(g.Amount, g.Item.m_shared.m_maxStackSize));
                if (got <= 0) continue;
                var n = g.Item.Clone();
                n.m_stack = got;
                n.m_gridPos = g.Pos;
                n.m_equipped = false;
                n.m_customData[FavKey] = g.Amount.ToString();
                var list = Util.GetField(inv, "m_inventory") as List<ItemDrop.ItemData>;
                if (list == null) { Pending.Remove(g); continue; }
                list.Add(n);
                Pending.Remove(g);
                changed = true;
                Notify(p, n, got);
            }

            if (changed) Util.Call(inv, "Changed");
        }

        private static int Take(List<Container> chests, ItemDrop.ItemData like, int want)
        {
            int got = 0;
            string name = like.m_shared.m_name;
            foreach (var c in chests)
            {
                if (got >= want) break;
                if (c == null) continue;
                var ci = c.GetInventory();
                if (ci == null) continue;
                int have = ci.CountItems(name, like.m_quality);
                if (have <= 0) continue;
                int take = Math.Min(have, want - got);
                ContainerTracker.ClaimOwnership(c);
                ci.RemoveItem(name, take, like.m_quality);
                got += take;
            }
            if (got > 0) ContainerTracker.Invalidate();
            return got;
        }

        private static void Notify(Player p, ItemDrop.ItemData it, int got)
        {
            try { p.Message(MessageHud.MessageType.TopLeft, L.T("Aus Kiste: ", "From chest: ") + Localization.instance.Localize(it.m_shared.m_name) + " +" + got, 0, it.GetIcon()); }
            catch { }
        }

        // ---------- Gelber Rahmen ----------

        private static readonly Color FavColor = new Color(1f, 0.82f, 0.1f, 1f);

        internal static void UpdateBorders(InventoryGrid grid)
        {
            var inv = grid != null ? grid.GetInventory() : null;
            if (!Mine(inv)) return;
            var els = Util.GetField(grid, "m_elements") as IList;
            if (els == null) return;
            bool on = Cfg.FavoritesEnabled.Value;
            foreach (var o in els)
            {
                var el = o as InventoryElement;
                if (el == null || !Util.ElementPos(el, out var pos)) continue;
                var it = on ? inv.GetItemAt(pos.x, pos.y) : null;
                bool fav = it != null && FavAmount(it) > 0;
                var t = el.transform.Find("PQ_Fav");
                if (!fav) { if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                if (t == null) t = MakeBorder(el.transform);
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                t.SetAsLastSibling();
            }
        }

        private static Transform MakeBorder(Transform parent)
        {
            var go = new GameObject("PQ_Fav", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            const float T = 2.5f;
            Side(go.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, T));
            Side(go.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, T));
            Side(go.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(T, 0));
            Side(go.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(T, 0));
            return go.transform;
        }

        private static void Side(Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject("side", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = FavColor;
            img.raycastTarget = false;
        }

        // ===================== Vergleich im Tooltip =====================

        private static float Damage(HitData.DamageTypes d) =>
            d.m_damage + d.m_blunt + d.m_slash + d.m_pierce + d.m_fire + d.m_frost + d.m_lightning + d.m_poison + d.m_spirit;

        private static bool IsWeapon(ItemDrop.ItemData it)
        {
            if (it?.m_shared == null) return false;
            var t = it.m_shared.m_itemType;
            return t == ItemDrop.ItemData.ItemType.OneHandedWeapon || t == ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
                   t == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || t == ItemDrop.ItemData.ItemType.Bow;
        }

        private static ItemDrop.ItemData HiddenLeft(Player p) => Util.GetField(p, "m_hiddenLeftItem") as ItemDrop.ItemData;
        private static ItemDrop.ItemData HiddenRight(Player p) => Util.GetField(p, "m_hiddenRightItem") as ItemDrop.ItemData;

        internal static string Compare(ItemDrop.ItemData item, int quality, float worldLevel)
        {
            var p = Player.m_localPlayer;
            if (p == null || item?.m_shared == null) return null;
            ItemDrop.ItemData cur = null;
            string label;
            int mode;   // 1 Schaden, 2 Rüstung, 3 Blocken
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet: cur = Util.GetField(p, "m_helmetItem") as ItemDrop.ItemData; mode = 2; label = L.T("Rüstung", "Armor"); break;
                case ItemDrop.ItemData.ItemType.Chest: cur = Util.GetField(p, "m_chestItem") as ItemDrop.ItemData; mode = 2; label = L.T("Rüstung", "Armor"); break;
                case ItemDrop.ItemData.ItemType.Legs: cur = Util.GetField(p, "m_legItem") as ItemDrop.ItemData; mode = 2; label = L.T("Rüstung", "Armor"); break;
                case ItemDrop.ItemData.ItemType.Shoulder: cur = Util.GetField(p, "m_shoulderItem") as ItemDrop.ItemData; mode = 2; label = L.T("Rüstung", "Armor"); break;
                case ItemDrop.ItemData.ItemType.Shield:
                {
                    var left = p.LeftItem ?? HiddenLeft(p);   // an Stationen steckt Valheim die Waffen weg
                    cur = left != null && left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield ? left : null;
                    mode = 3; label = L.T("Blocken", "Block"); break;
                }
                default:
                {
                    if (!IsWeapon(item)) return null;
                    ItemDrop.ItemData r = p.RightItem, l = p.LeftItem;
                    if (r == null && l == null) { r = HiddenRight(p); l = HiddenLeft(p); }
                    cur = IsWeapon(r) ? r : IsWeapon(l) ? l : null;
                    mode = 1; label = L.T("Schaden", "Damage"); break;
                }
            }
            if (cur == null) return null;
            if (ReferenceEquals(cur, item) && quality == cur.m_quality) return null;   // ist das Angelegte selbst

            float mine, theirs;
            switch (mode)
            {
                case 1: mine = Damage(item.GetDamage(quality, worldLevel)); theirs = Damage(cur.GetDamage(cur.m_quality, cur.m_worldLevel)); break;
                case 2: mine = item.GetArmor(quality, worldLevel); theirs = cur.GetArmor(cur.m_quality, cur.m_worldLevel); break;
                default: mine = item.GetBaseBlockPower(quality); theirs = cur.GetBaseBlockPower(cur.m_quality); break;
            }
            float diff = Mathf.Round(mine - theirs);
            string color = diff > 0 ? "#7CFC00" : diff < 0 ? "#FF6060" : "#B0B0B0";
            string sign = diff > 0 ? "+" : diff < 0 ? "−" : "±";
            var sb = new StringBuilder();
            sb.Append("\n\n<color=#D8C89A>").Append(L.T("Vergleich mit ", "Compared to ")).Append(cur.m_shared.m_name);
            if (cur.m_shared.m_maxQuality > 1) sb.Append(L.T(" (Stufe ", " (level ")).Append(cur.m_quality).Append(')');
            sb.Append(":</color>\n").Append(label).Append(": <color=").Append(color).Append('>')
              .Append(sign).Append(Mathf.Abs(diff).ToString("0")).Append("</color>");
            return sb.ToString();
        }
    }

    // ---------- Verbrauchen erkennen ----------

    [HarmonyPatch(typeof(Attack), "UseAmmo")]
    internal static class Attack_UseAmmo_Fav
    {
        private static void Prefix() => InventoryHelpers.Consuming++;
        private static void Finalizer() => InventoryHelpers.Consuming--;
    }

    [HarmonyPatch(typeof(Attack), "ConsumeItem")]
    internal static class Attack_ConsumeItem_Fav
    {
        private static void Prefix() => InventoryHelpers.Consuming++;
        private static void Finalizer() => InventoryHelpers.Consuming--;
    }

    [HarmonyPatch(typeof(Humanoid), "DrainEquipedItemDurability")]
    internal static class Humanoid_DrainDurability_Fav
    {
        private static void Prefix() => InventoryHelpers.Consuming++;
        private static void Finalizer() => InventoryHelpers.Consuming--;
    }

    // ---------- Entfernen aus dem Inventar ----------

    /// <summary>Essen, Trinken, auf Ständer legen usw. – zählt immer als Verbrauch.</summary>
    [HarmonyPatch(typeof(Inventory), "RemoveOneItem", new[] { typeof(ItemDrop.ItemData) })]
    internal static class Inventory_RemoveOneItem_Fav
    {
        private static void Prefix(Inventory __instance, object[] __args, out bool __state) =>
            __state = InventoryHelpers.WillVanish(__instance, __args[0] as ItemDrop.ItemData, true);

        private static void Postfix(Inventory __instance, object[] __args, bool __state)
        {
            if (__state) InventoryHelpers.Vanished(__instance, __args[0] as ItemDrop.ItemData);
        }
    }

    /// <summary>Ganzes Item entfernen – nur beim Verbrauchen (Pfeile, Wurfwaffen), nicht beim Wegräumen in Kisten.</summary>
    [HarmonyPatch(typeof(Inventory), "RemoveItem", new[] { typeof(ItemDrop.ItemData) })]
    internal static class Inventory_RemoveItem_Fav
    {
        private static void Prefix(Inventory __instance, object[] __args, out bool __state) =>
            __state = InventoryHelpers.Consuming > 0 && InventoryHelpers.WillVanish(__instance, __args[0] as ItemDrop.ItemData, false);

        private static void Postfix(Inventory __instance, object[] __args, bool __state)
        {
            if (__state) InventoryHelpers.Vanished(__instance, __args[0] as ItemDrop.ItemData);
        }
    }

    /// <summary>Nach Name entfernen = Bauen, Herstellen, Öfen befüllen – zählt immer als Verbrauch.</summary>
    [HarmonyPatch(typeof(Inventory), "RemoveItem", new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
    internal static class Inventory_RemoveItemByName_Fav
    {
        private static void Prefix(Inventory __instance, object[] __args, out List<ItemDrop.ItemData> __state) =>
            __state = InventoryHelpers.FavsNamed(__instance, __args[0] as string);

        private static void Postfix(Inventory __instance, List<ItemDrop.ItemData> __state)
        {
            if (__state == null) return;
            foreach (var it in __state) InventoryHelpers.Vanished(__instance, it);
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class InventoryGrid_UpdateGui_Fav
    {
        private static void Postfix(InventoryGrid __instance)
        {
            try { InventoryHelpers.UpdateBorders(__instance); }
            catch (Exception e) { Plugin.LogWarn("Favoriten-Rahmen: " + e.Message); }
        }
    }

    // ---------- Waffen an Stationen in der Hand behalten ----------

    [HarmonyPatch(typeof(Player), "SetCraftingStation")]
    internal static class Player_SetCraftingStation_KeepWeapons
    {
        internal static bool Active;
        private static void Prefix() => Active = Cfg.KeepWeaponsAtStation.Value;
        private static void Finalizer() => Active = false;
    }

    [HarmonyPatch(typeof(Humanoid), "HideHandItems")]
    internal static class Humanoid_HideHandItems_KeepWeapons
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Player_SetCraftingStation_KeepWeapons.Active) return true;
            __result = false;
            return false;   // Waffen nicht wegstecken
        }
    }

    // ---------- Tooltip ----------

    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip",
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class ItemData_GetTooltip_Compare
    {
        private static void Postfix(object[] __args, ref string __result)
        {
            try
            {
                if (__args.Length < 6) return;
                if (__args[5] is bool appending && appending) return;
                var item = __args[0] as ItemDrop.ItemData;
                if (Cfg.TooltipCompare.Value)
                {
                    int quality = __args[1] is int q ? q : 1;
                    float world = __args[3] is float w ? w : 0f;
                    string extra = InventoryHelpers.Compare(item, quality, world);
                    if (extra != null) __result += extra;
                }
                int fav = Cfg.FavoritesEnabled.Value ? InventoryHelpers.FavAmount(item) : 0;
                if (fav > 0) __result += "\n\n<color=#FFD21A>" + L.T("Favorit: wird aus Kisten auf ", "Favorite: refilled from chests to ") + fav + L.T(" aufgefüllt", "") + "</color>";
            }
            catch (Exception e) { Plugin.LogWarn("Vergleich: " + e.Message); }
        }
    }
}
