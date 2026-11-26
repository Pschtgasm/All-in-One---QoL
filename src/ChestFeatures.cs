using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>Kisten beschriften, Quick Stack, Sortieren, Kistengroessen.</summary>
    internal static class ChestFeatures
    {
        internal const string TagKey = "aioqol_tag";

        // Wird jede Frame aus Plugin.Update aufgerufen
        internal static void HandleInput()
        {
            var player = Player.m_localPlayer;
            if (player == null || Util.InputBlocked()) return;

            if (Cfg.ChestTagEnabled.Value && Cfg.ChestTagKey.Value.IsDown()) TryTag(player);
            if (Cfg.QuickStackEnabled.Value && Cfg.QuickStackKey.Value.IsDown()) QuickStack(player);
            if (Cfg.QuickStackEnabled.Value && Cfg.SortKey.Value.IsDown()) Sort(player);
            if (Cfg.DeleteEnabled.Value && Cfg.DeleteKey.Value.IsDown()) TryDelete(player);
            if (Cfg.ChestSignEnabled.Value && Cfg.ChestSideKey.Value.IsDown()) TryChangeSide(player);
            if (Cfg.ChestSignEnabled.Value && Cfg.ChestIconKey.Value.IsDown()) TrySetIcon(player);
        }

        // ---------- Schild: Seite wechseln (Kiste anschauen) ----------

        private static void TryChangeSide(Player player)
        {
            if (InventoryGui.IsVisible()) return;
            var hover = player.GetHoverObject();
            var container = hover ? hover.GetComponentInParent<Container>() : null;
            if (container == null || !Util.CanAccess(container, player.GetPlayerID())) return;
            var nv = container.GetComponentInParent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;

            var zdo = nv.GetZDO();
            if (zdo.GetString(TagKey, "").Length == 0 && zdo.GetString(ChestSign.IconKey, "").Length == 0)
            {
                player.Message(MessageHud.MessageType.Center, L.T("Erst beschriften (", "First add a label (") + Cfg.ChestTagKey.Value + L.T(") oder ein Bild setzen (", ") or set a picture (") + Cfg.ChestIconKey.Value + ")");
                return;
            }
            if (!nv.IsOwner()) nv.ClaimOwnership();
            int side = (((zdo.GetInt(ChestSign.SideKey, 0) + 1) % 4) + 4) % 4;
            zdo.Set(ChestSign.SideKey, side);
            player.Message(MessageHud.MessageType.Center, L.T("Schild: ", "Sign: ") + ChestSign.SideNames[side]);
        }

        // ---------- Schild: Bild setzen (in der offenen Kiste auf ein Item zeigen) ----------

        private static void TrySetIcon(Player player)
        {
            var gui = InventoryGui.instance;
            if (gui == null || !InventoryGui.IsVisible()) return;
            var container = Util.GetField(gui, "m_currentContainer") as Container;
            if (container == null) return;
            var nv = container.GetComponentInParent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;

            var item = HoveredItem(gui, out _);
            if (item == null)
            {
                player.Message(MessageHud.MessageType.Center, L.T("Mit der Maus auf ein Item zeigen und ", "Point the mouse at an item and press ") + Cfg.ChestIconKey.Value + L.T(" drücken", ""));
                return;
            }
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (prefab.Length == 0) return;

            if (!nv.IsOwner()) nv.ClaimOwnership();
            var zdo = nv.GetZDO();
            string name = Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
            if (zdo.GetString(ChestSign.IconKey, "") == prefab)
            {
                zdo.Set(ChestSign.IconKey, "");
                player.Message(MessageHud.MessageType.Center, L.T("Bild entfernt", "Picture removed"));
            }
            else
            {
                zdo.Set(ChestSign.IconKey, prefab);
                player.Message(MessageHud.MessageType.Center, L.T("Bild auf der Kiste: ", "Picture on the chest: ") + name);
            }
        }

        /// <summary>Item unter dem Mauszeiger (eigenes Inventar oder offene Kiste).</summary>
        internal static ItemDrop.ItemData HoveredItem(InventoryGui gui, out Inventory inventory)
        {
            inventory = null;

            // 1) Was liegt wirklich unter der Maus? (UI-Raycast – unabhängig von internen Feldnamen)
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                {
                    var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.mousePosition };
                    var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(data, hits);
                    foreach (var h in hits)
                    {
                        if (h.gameObject == null) continue;
                        var el = h.gameObject.GetComponentInParent<InventoryElement>();
                        var grid = el != null ? el.GetComponentInParent<InventoryGrid>() : null;
                        if (grid == null || !Util.ElementPos(el, out var p)) continue;
                        var inv = grid.GetInventory();
                        var it = inv?.GetItemAt(p.x, p.y);
                        if (it == null) continue;
                        inventory = inv;
                        return it;
                    }
                }
            }
            catch { }

            // 2) Rückfall: InventoryGrid.GetHoveredElement
            foreach (var gridName in new[] { "m_containerGrid", "m_playerGrid" })
            {
                var grid = Util.GetField(gui, gridName) as InventoryGrid;
                if (grid == null) continue;
                var element = Util.Call(grid, "GetHoveredElement");
                if (element == null) continue;
                if (!Util.ElementPos(element, out var pos)) continue;
                var inv = grid.GetInventory();
                var item = inv?.GetItemAt(pos.x, pos.y);
                if (item == null) continue;
                inventory = inv;
                return item;
            }
            return null;
        }

        // ---------- Item loeschen ----------

        private static void TryDelete(Player player)
        {
            var gui = InventoryGui.instance;
            if (gui == null || !InventoryGui.IsVisible()) return;

            // 1) Item, das gerade mit der Maus festgehalten wird
            var drag = Util.GetField(gui, "m_dragItem") as ItemDrop.ItemData;
            var dragInv = Util.GetField(gui, "m_dragInventory") as Inventory;
            if (drag != null && dragInv != null)
            {
                if (IsProtected(player, drag)) return;
                Delete(player, dragInv, drag);
                Util.Call(gui, "SetupDragItem", null, null, 1);
                return;
            }

            // Löschen nur für das festgehaltene Item (Löschen per Zeigen wurde auf Wunsch entfernt)
        }

        private static bool IsProtected(Player player, ItemDrop.ItemData item)
        {
            if (Cfg.DeleteProtectEquipped.Value && item.m_equipped)
            {
                player.Message(MessageHud.MessageType.Center, L.T("Ausgerüstete Items werden nicht gelöscht", "Equipped items are not deleted"));
                return true;
            }
            return false;
        }

        private static void Delete(Player player, Inventory inv, ItemDrop.ItemData item)
        {
            string name = Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
            int amount = item.m_stack;
            if (item.m_equipped) player.UnequipItem(item, false);
            inv.RemoveItem(item);
            player.Message(MessageHud.MessageType.Center, amount + "x " + name + L.T(" gelöscht", " deleted"));
        }

        // ---------- Beschriften ----------

        private static void TryTag(Player player)
        {
            var hover = player.GetHoverObject();
            if (!hover) return;
            var container = hover.GetComponentInParent<Container>();
            if (!container) return;
            if (!Util.CanAccess(container, player.GetPlayerID())) return;
            var nv = container.GetComponentInParent<ZNetView>();
            if (nv == null || !nv.IsValid() || TextInput.instance == null) return;
            TextInput.instance.RequestText(new ChestTagReceiver(nv), L.T("Kiste beschriften", "Label chest"), 40);
        }

        internal static string GetTag(Component c)
        {
            var nv = c ? c.GetComponentInParent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) return "";
            return nv.GetZDO().GetString(TagKey, "");
        }

        // ---------- Quick Stack ----------

        private static void QuickStack(Player player)
        {
            var inv = player.GetInventory();
            var targets = ContainerTracker.Query(player, Cfg.QuickStackRange.Value);
            if (targets.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, L.T("Keine Kiste in der Nähe", "No chest nearby"));
                return;
            }

            int moved = 0;
            foreach (var item in inv.GetAllItems().ToList())
            {
                if (item == null || item.m_equipped) continue;
                if (Cfg.IgnoreHotbar.Value && item.m_gridPos.y == 0) continue;
                if (ExtraSlots.IsExtraRow(inv, item.m_gridPos.y)) continue; // Rüstungs-/Essensslots nicht anfassen

                foreach (var c in targets)
                {
                    var target = c.GetInventory();
                    if (!target.HaveItem(item.m_shared.m_name)) continue;
                    if (!target.CanAddItem(item, item.m_stack)) continue;

                    ContainerTracker.ClaimOwnership(c);
                    target.MoveItemToThis(inv, item);
                    if (!inv.ContainsItem(item)) { moved++; break; }
                }
            }
            ContainerTracker.Invalidate();
            player.Message(MessageHud.MessageType.Center, moved + L.T(" Stapel verstaut", " stacks stored"));
        }

        // ---------- Sortieren ----------

        private static void Sort(Player player)
        {
            int startRow = Cfg.IgnoreHotbar.Value ? 1 : 0;
            SortInventory(player.GetInventory(), startRow);

            if (InventoryGui.IsVisible() && InventoryGui.instance != null)
            {
                var container = Util.GetField(InventoryGui.instance, "m_currentContainer") as Container;
                if (container != null)
                {
                    ContainerTracker.ClaimOwnership(container);
                    SortInventory(container.GetInventory(), 0);
                }
            }
            player.Message(MessageHud.MessageType.Center, L.T("Sortiert", "Sorted"));
        }

        private static void SortInventory(Inventory inv, int startRow)
        {
            if (inv == null) return;
            var items = inv.GetAllItems().Where(i => i != null && i.m_gridPos.y >= startRow && !ExtraSlots.IsExtraRow(inv, i.m_gridPos.y)).ToList();

            items = items
                .OrderBy(i => (int)i.m_shared.m_itemType)
                .ThenBy(i => Localization.instance != null ? Localization.instance.Localize(i.m_shared.m_name) : i.m_shared.m_name)
                .ThenByDescending(i => i.m_quality)
                .ThenByDescending(i => i.m_stack)
                .ToList();

            // Stapel zusammenlegen
            for (int a = 0; a < items.Count; a++)
            {
                var A = items[a];
                if (A.m_stack <= 0 || A.m_shared.m_maxStackSize <= 1) continue;
                for (int b = a + 1; b < items.Count && A.m_stack < A.m_shared.m_maxStackSize; b++)
                {
                    var B = items[b];
                    if (B.m_stack <= 0 || B.m_shared.m_name != A.m_shared.m_name ||
                        B.m_quality != A.m_quality || B.m_variant != A.m_variant) continue;
                    int move = Math.Min(B.m_stack, A.m_shared.m_maxStackSize - A.m_stack);
                    A.m_stack += move;
                    B.m_stack -= move;
                }
            }

            foreach (var empty in items.Where(i => i.m_stack <= 0).ToList())
            {
                inv.RemoveItem(empty);
                items.Remove(empty);
            }

            // Neu anordnen
            int width = inv.GetWidth();
            for (int idx = 0; idx < items.Count; idx++)
                items[idx].m_gridPos = new Vector2i(idx % width, startRow + idx / width);

            Util.Call(inv, "Changed");
        }

        // ---------- Kistengroessen ----------

        private static string _parsedFrom;
        private static Dictionary<string, int[]> _sizes = new Dictionary<string, int[]>();

        internal static bool TryGetSize(string prefab, out int w, out int h)
        {
            w = h = 0;
            string raw = string.Join(",", Cfg.ChestSizeEntries.Select(kv => kv.Key + "=" + kv.Value.Value));
            if (raw != _parsedFrom)
            {
                _parsedFrom = raw;
                _sizes = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in raw.Split(','))
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2) continue;
                    var wh = kv[1].ToLowerInvariant().Split('x');
                    if (wh.Length == 2 && int.TryParse(wh[0].Trim(), out int pw) && int.TryParse(wh[1].Trim(), out int ph) && pw > 0 && ph > 0)
                        _sizes[kv[0].Trim()] = new[] { pw, ph };
                }
            }
            if (!_sizes.TryGetValue(prefab, out var s)) return false;
            w = s[0]; h = s[1];
            return true;
        }
    }

    internal class ChestTagReceiver : TextReceiver
    {
        private readonly ZNetView _nv;
        public ChestTagReceiver(ZNetView nv) { _nv = nv; }

        public string GetText() => (_nv != null && _nv.IsValid()) ? _nv.GetZDO().GetString(ChestFeatures.TagKey, "") : "";

        public void SetText(string text)
        {
            if (_nv == null || !_nv.IsValid()) return;
            if (!_nv.IsOwner()) _nv.ClaimOwnership();
            _nv.GetZDO().Set(ChestFeatures.TagKey, text ?? "");
        }
    }

    [HarmonyPatch(typeof(Container), "GetHoverText")]
    internal static class Container_HoverTag
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            if (!Cfg.ChestTagEnabled.Value) return;
            string tag = ChestFeatures.GetTag(__instance);
            if (!string.IsNullOrEmpty(tag))
                __result = "<color=#FFD700>" + tag + "</color>\n" + __result;
            __result += "\n[<color=yellow><b>" + Cfg.ChestTagKey.Value + "</b></color>] " + L.T("Beschriften", "Label");
            if (Cfg.ChestSignEnabled.Value)
                __result += "  [<color=yellow><b>" + Cfg.ChestSideKey.Value + "</b></color>] " + L.T("Schild-Seite", "Sign side");
        }
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class Container_Awake_Size
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Container __instance)
        {
            if (!Cfg.BiggerChests.Value) return;
            if (ChestFeatures.TryGetSize(Util.PrefabName(__instance.gameObject), out int w, out int h))
            {
                __instance.m_width = w;
                __instance.m_height = h;
            }
        }
    }
}
