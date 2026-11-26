using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Extra-Slots neben dem Inventar:
    ///   Spalte 1: Helm, Brust, Beine, Umhang  (angelegte Rüstung liegt hier, nicht im Inventar)
    ///   Spalte 2: Essen 1–3                    (Strg+Y / Strg+X / Strg+C essen daraus)
    ///
    /// Technik: Das Spieler-Inventar bekommt eine zusätzliche, unsichtbare Reihe. Ihre Felder werden im
    /// Inventarfenster an die Stelle rechts neben dem Inventar verschoben. Normale Items landen nie
    /// automatisch dort (FindEmptySlot/HaveEmptySlot/GetEmptySlots zählen nur die normalen Reihen).
    ///
    ///   x = 0..3  Rüstung (Helm, Brust, Beine, Umhang)
    ///   x = 4..6  Essen 1..3
    ///   x >= 7    unbenutzt (ausgeblendet)
    /// </summary>
    internal static class ExtraSlots
    {
        private class Info { public int BaseHeight; }
        private static readonly ConditionalWeakTable<Inventory, Info> Extended = new ConditionalWeakTable<Inventory, Info>();

        internal const int ArmorCount = 4;
        internal const int FoodStart = 4;
        internal const int FoodCount = 3;
        internal const int UtilityX = 7;   // Reihe 0, Feld 8: Wünschelrute ODER Irrlicht (normales Hilfsmittel)
        internal const int TrinketX = 0;   // Reihe 1, Feld 1: Schmuckstück (Trinket)
        internal const int BeltX = 1;      // Reihe 1, Feld 2: Gürtel (wirkt zusätzlich, Mod schaltet die Wirkung ein)
        internal const int ShieldX = 2;    // Reihe 1, Feld 3: Schild (wird bei Einhandwaffen automatisch angelegt)
        internal const int KeyX = 3;       // Reihe 1, Feld 4: Schlüssel (Krypta-Schlüssel usw.)
        internal const int QuickRow = 2;   // Extra-Reihe 3: zweite Schnellleiste (x = 0..7, Alt+1..8)
        internal const int QuickCount = 8;
        internal const int ExtraRows = 3;
        internal static string[] ArmorNames => L.De ? new[] { "Helm", "Brust", "Beine", "Umhang" } : new[] { "Helmet", "Chest", "Legs", "Cape" };

        internal enum Kind { None, Armor, Food, Utility, Trinket, Belt, Quick, Shield, Key }

        private static float _nextCheck;

        internal static bool ArmorOn => Cfg.ArmorSlotsEnabled.Value;
        internal static bool FoodOn => Cfg.FoodSlotsEnabled.Value;
        internal static bool UtilOn => Cfg.UtilitySlotsEnabled.Value;
        internal static bool BeltOn => Cfg.BeltSlotEnabled.Value;
        internal static bool QuickOn => Cfg.Hotbar2Enabled.Value;
        internal static bool ShieldOn => Cfg.ShieldSlotEnabled.Value;
        internal static bool KeyOn => Cfg.KeySlotEnabled.Value;
        internal static bool AnyEnabled => ArmorOn || FoodOn || UtilOn || BeltOn || QuickOn || ShieldOn || KeyOn;

        /// <summary>Gürtel (Megingiard): Prefab "BeltStrength" oder ein Hilfsmittel, das Tragkraft gibt.</summary>
        internal static bool IsBelt(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null || TypeOf(item) != "Utility") return false;
            if (item.m_dropPrefab != null && item.m_dropPrefab.name == "BeltStrength") return true;
            return item.m_shared.m_equipStatusEffect is SE_Stats st && st.m_addMaxCarryWeight > 0f;
        }

        private static HashSet<string> _keys;
        private static ZNetScene _keysFor;

        /// <summary>Schlüssel = jedes Item, das irgendeine Tür als Schlüssel braucht (auch aus anderen Mods).</summary>
        internal static bool IsKey(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return false;
            var scene = ZNetScene.instance;
            if (_keys == null || (scene != null && _keysFor != scene))
            {
                _keys = new HashSet<string> { "$item_cryptkey" };
                _keysFor = scene;
                if (scene != null)
                    foreach (var go in scene.m_prefabs)
                    {
                        if (!go) continue;
                        foreach (var door in go.GetComponentsInChildren<Door>(true))
                            if (door != null && door.m_keyItem != null && door.m_keyItem.m_itemData?.m_shared != null)
                                _keys.Add(door.m_keyItem.m_itemData.m_shared.m_name);
                    }
                Plugin.LogInfo("Extra-Slots: Schlüssel erkannt: " + string.Join(", ", _keys));
            }
            return _keys.Contains(item.m_shared.m_name);
        }

        /// <summary>Welche Art Slot liegt an x in Extra-Reihe dy (0 = erste Extra-Reihe)?</summary>
        internal static Kind KindAt(int x, int dy)
        {
            if (dy == 0)
            {
                if (x < ArmorCount) return Kind.Armor;
                if (x < FoodStart + FoodCount) return Kind.Food;
                if (x == UtilityX) return Kind.Utility;
            }
            if (dy == 1 && x == TrinketX) return Kind.Trinket;
            if (dy == 1 && x == BeltX) return Kind.Belt;
            if (dy == 1 && x == ShieldX) return Kind.Shield;
            if (dy == 1 && x == KeyX) return Kind.Key;
            if (dy == QuickRow && x < QuickCount) return Kind.Quick;
            return Kind.None;
        }

        internal static bool KindOn(Kind k) =>
            k == Kind.Armor ? ArmorOn : k == Kind.Food ? FoodOn : k == Kind.Belt ? BeltOn : k == Kind.Quick ? QuickOn : k == Kind.Shield ? ShieldOn : k == Kind.Key ? KeyOn :
            (k == Kind.Utility || k == Kind.Trinket) && UtilOn;

        private static string TypeOf(ItemDrop.ItemData item) =>
            item?.m_shared != null ? item.m_shared.m_itemType.ToString() : "";

        /// <summary>Passt das Item in den Slot (Art + Position)?</summary>
        private static bool Fits(ItemDrop.ItemData item, Kind k, int x)
        {
            switch (k)
            {
                case Kind.Armor: return ArmorSlot(item) == x;
                case Kind.Food: return IsFood(item);
                case Kind.Utility: return TypeOf(item) == "Utility" && !(BeltOn && IsBelt(item));
                case Kind.Trinket: return TypeOf(item) == "Trinket";
                case Kind.Belt: return IsBelt(item);
                case Kind.Quick: return true;   // Schnellleiste: alles erlaubt
                case Kind.Shield: return TypeOf(item) == "Shield";
                case Kind.Key: return IsKey(item);
            }
            return false;
        }

        /// <summary>Slot für ein anlegbares Item (Rüstung, Hilfsmittel, Schmuck): x und Extra-Reihe dy.</summary>
        internal static bool EquipSlot(ItemDrop.ItemData item, out int x, out int dy)
        {
            x = -1; dy = 0;
            int a = ArmorSlot(item);
            if (a >= 0 && ArmorOn) { x = a; return true; }
            string t = TypeOf(item);
            if (BeltOn && IsBelt(item)) return false;   // Gürtel läuft über den eigenen Slot (BeltSlot)
            if (t == "Utility" && UtilOn) { x = UtilityX; return true; }
            if (t == "Trinket" && UtilOn) { x = TrinketX; dy = 1; return true; }
            if (t == "Shield" && ShieldOn) { x = ShieldX; dy = 1; return true; }
            return false;
        }

        // ---------- Inventar-Reihen ----------

        internal static void Extend(Inventory inv)
        {
            if (inv == null || Extended.TryGetValue(inv, out _)) return;
            int h = inv.GetHeight();
            if (inv.GetWidth() <= UtilityX) return;   // zu schmal, nichts tun
            Extended.Add(inv, new Info { BaseHeight = h });
            Util.SetField(inv, "m_height", h + ExtraRows);
        }

        /// <summary>
        /// Valheim 1.0 setzt die Inventarhöhe beim Spawnen neu (Player.SetInventorySize → Inventory.SetHeight)
        /// und wirft danach alles außerhalb auf den Boden. Hier bleibt die Extra-Reihe erhalten.
        /// </summary>
        internal static void OnSetHeight(Inventory inv, ref int height)
        {
            if (inv == null || !Extended.TryGetValue(inv, out var info)) return;
            int old = info.BaseHeight;
            info.BaseHeight = height;
            if (old != height) ShiftRows(inv, old, height);
            height += ExtraRows;
        }

        /// <summary>
        /// Normale Reihenzahl hat sich geändert (größeres Inventar, beim Händler gekauft): Extra-Slots wandern mit.
        /// Items aus weggefallenen normalen Reihen kommen auf freie Plätze, sonst fallen sie auf den Boden.
        /// </summary>
        private static void ShiftRows(Inventory inv, int oldBase, int newBase)
        {
            var extras = new List<ItemDrop.ItemData>();
            var overflow = new List<ItemDrop.ItemData>();
            foreach (var it in inv.GetAllItems())
            {
                if (it == null) continue;
                if (it.m_gridPos.y >= oldBase) extras.Add(it);
                else if (it.m_gridPos.y >= newBase) overflow.Add(it);
            }
            foreach (var it in extras) it.m_gridPos = new Vector2i(it.m_gridPos.x, it.m_gridPos.y - oldBase + newBase);
            foreach (var it in overflow) it.m_gridPos = new Vector2i(-1, -1);
            foreach (var it in overflow)
            {
                var free = FindFreeNormal(inv);
                if (free.x >= 0) it.m_gridPos = free;   // sonst bleibt (-1,-1): Valheim wirft es auf den Boden
            }
            if (extras.Count > 0 || overflow.Count > 0)
                Plugin.LogInfo("Extra-Slots: Inventar " + oldBase + " -> " + newBase + " Reihen, " + extras.Count + " Slot-Items verschoben");
        }

        private const string BaseKey = "aioqol_base";

        /// <summary>Nach dem Laden: Mit welcher Reihenzahl wurden die Items gespeichert? Dann passt das Verschieben.</summary>
        internal static void SetLayoutBase(Player p, Inventory inv)
        {
            if (inv == null || !Extended.TryGetValue(inv, out var info)) return;
            int saved = p.m_customData.TryGetValue(BaseKey, out var s) && int.TryParse(s, out int v) ? v : InventorySize.VanillaRows(p);
            saved = Mathf.Clamp(saved, 1, InventorySize.MaxRows);
            info.BaseHeight = saved;
            Util.SetField(inv, "m_height", saved + ExtraRows);
        }

        /// <summary>Beim Speichern: aktuelle Reihenzahl merken.</summary>
        internal static void SaveLayoutBase(Player p)
        {
            int b = BaseHeight(p.GetInventory());
            if (b > 0) p.m_customData[BaseKey] = b.ToString();
        }

        /// <summary>Normale Höhe (ohne Extra-Reihe) oder -1, wenn das Inventar nicht erweitert ist.</summary>
        internal static int BaseHeight(Inventory inv) =>
            inv != null && Extended.TryGetValue(inv, out var info) ? info.BaseHeight : -1;

        internal static bool IsExtraRow(Inventory inv, int y)
        {
            int b = BaseHeight(inv);
            return b >= 0 && y >= b;
        }

        internal static int CountNormalItems(Inventory inv)
        {
            int b = BaseHeight(inv), n = 0;
            foreach (var it in inv.GetAllItems()) if (it != null && it.m_gridPos.y < b) n++;
            return n;
        }

        internal static Vector2i FindFreeNormal(Inventory inv, bool topFirst = true)
        {
            int b = BaseHeight(inv);
            if (b < 0) b = inv.GetHeight();
            int w = inv.GetWidth();
            if (topFirst)
            {
                for (int y = 0; y < b; y++) for (int x = 0; x < w; x++) if (inv.GetItemAt(x, y) == null) return new Vector2i(x, y);
            }
            else
            {
                for (int y = b - 1; y >= 0; y--) for (int x = 0; x < w; x++) if (inv.GetItemAt(x, y) == null) return new Vector2i(x, y);
            }
            return new Vector2i(-1, -1);
        }

        private static void Changed(Inventory inv) => Util.Call(inv, "Changed");

        // ---------- Rüstung ----------

        internal static int ArmorSlot(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return -1;
            switch (item.m_shared.m_itemType.ToString())
            {
                case "Helmet": return 0;
                case "Chest": return 1;
                case "Legs": return 2;
                case "Shoulder": return 3;
            }
            return -1;
        }

        private static bool IsFood(ItemDrop.ItemData item) =>
            item != null && item.m_shared != null && item.m_shared.m_itemType.ToString() == "Consumable";

        /// <summary>Angelegte Rüstung / Hilfsmittel / Schmuck in den Slot legen (tauscht mit dem, was dort liegt).</summary>
        internal static void PlaceEquipped(Player p, ItemDrop.ItemData item)
        {
            var inv = p.GetInventory();
            int b = BaseHeight(inv);
            if (b < 0 || !inv.ContainsItem(item) || !EquipSlot(item, out int sx, out int sdy)) return;
            var target = new Vector2i(sx, b + sdy);
            if (item.m_gridPos.x == target.x && item.m_gridPos.y == target.y) return;

            var occupant = inv.GetItemAt(target.x, target.y);
            if (occupant != null && occupant != item) occupant.m_gridPos = item.m_gridPos; // Plätze tauschen
            item.m_gridPos = target;
            Changed(inv);
        }

        /// <summary>Abgelegte Rüstung aus dem Slot zurück ins Inventar.</summary>
        internal static void PlaceUnequipped(Player p, ItemDrop.ItemData item)
        {
            var inv = p.GetInventory();
            if (!inv.ContainsItem(item) || !IsExtraRow(inv, item.m_gridPos.y)) return;
            // Waffen/Werkzeuge in der zweiten Schnellleiste bleiben dort, auch wenn sie abgelegt werden
            var k0 = KindAt(item.m_gridPos.x, item.m_gridPos.y - BaseHeight(inv));
            if (k0 == Kind.Quick || k0 == Kind.Shield) return;   // Schild bleibt im Schild-Slot
            var free = FindFreeNormal(inv);
            if (free.x < 0) return;   // Inventar voll: bleibt im Slot
            item.m_gridPos = free;
            Changed(inv);
        }

        // ---------- Regelmäßig: Ordnung halten ----------

        internal static void Tick()
        {
            if (!ExtraSlots.AnyEnabled || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.3f;
            var p = Player.m_localPlayer;
            if (p == null) return;
            var inv = p.GetInventory();
            int b = BaseHeight(inv);
            if (b < 0) return;

            bool changed = RestoreSlots(p, inv, b);
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                if (item == null) continue;

                if (item.m_gridPos.y < b)
                {
                    // Angelegte Rüstung / Hilfsmittel / Schmuck gehört in seinen Slot
                    if (item.m_equipped && EquipSlot(item, out int sx, out int sdy))
                    {
                        var occupant = inv.GetItemAt(sx, b + sdy);
                        if (occupant == null || !occupant.m_equipped)
                        {
                            if (occupant != null) occupant.m_gridPos = item.m_gridPos;
                            item.m_gridPos = new Vector2i(sx, b + sdy);
                            changed = true;
                        }
                    }
                    // Schlüssel wandert von selbst in den leeren Schlüssel-Slot
                    else if (KeyOn && IsKey(item) && inv.GetItemAt(KeyX, b + 1) == null)
                    {
                        item.m_gridPos = new Vector2i(KeyX, b + 1);
                        changed = true;
                    }
                    continue;
                }

                int x = item.m_gridPos.x;
                var kind = KindAt(x, item.m_gridPos.y - b);
                bool ok = kind != Kind.None && KindOn(kind) && Fits(item, kind, x);
                // In einen Ausrüstungs-Slot gezogen = anlegen (Gürtel: Wirkung macht BeltSlot.Tick)
                if (ok && kind != Kind.Food && kind != Kind.Belt && kind != Kind.Quick && kind != Kind.Shield && kind != Kind.Key && !item.m_equipped) p.EquipItem(item);
                // Schild in den Schild-Slot gelegt, während eine Einhandwaffe in der Hand ist: gleich anlegen
                if (ok && kind == Kind.Shield && !item.m_equipped && p.LeftItem == null && p.RightItem != null)
                    AutoShield(p, p.RightItem);

                if (!ok)
                {
                    var free = FindFreeNormal(inv);
                    if (free.x >= 0)
                    {
                        Plugin.LogInfo("Extra-Slots: " + item.m_shared.m_name + " (Typ " + item.m_shared.m_itemType +
                                       ") passt nicht in Slot " + x + " -> ins Inventar");
                        item.m_gridPos = free;
                        changed = true;
                    }
                }
            }
            if (changed) Changed(inv);
        }

        // ---------- Nach dem Tod: zurück in die Slots ----------

        private const string MemKey = "aioqol_slotmem";

        private static string ItemId(ItemDrop.ItemData it) => it.m_shared.m_name + "|" + it.m_quality + "|" + it.m_variant;

        [HarmonyPatch(typeof(Player), "Save")]
    internal static class Player_Save_ExtraSlots
    {
        private static void Prefix(Player __instance) => ExtraSlots.SaveLayoutBase(__instance);
    }

    /// <summary>Vor dem Grabstein: merken, welches Item in welchem Extra-Slot lag (im Spielstand gespeichert).</summary>
        internal static void RememberSlots(Player p, Inventory inv, int b)
        {
            if (!Cfg.SlotsAfterDeath.Value) return;
            var parts = new List<string>();
            foreach (var it in inv.GetAllItems())
            {
                if (it?.m_shared == null || it.m_gridPos.y < b) continue;
                parts.Add(ItemId(it) + "|" + it.m_gridPos.x + "|" + (it.m_gridPos.y - b));
            }
            if (parts.Count == 0) return;
            p.m_customData[MemKey] = string.Join(";", parts);
            Plugin.LogInfo("Extra-Slots: " + parts.Count + " Slot-Plätze für den Grabstein gemerkt");
        }

        /// <summary>Items aus dem Grabstein wieder in ihre gemerkten Slots legen.</summary>
        private static bool RestoreSlots(Player p, Inventory inv, int b)
        {
            if (!Cfg.SlotsAfterDeath.Value || !p.m_customData.TryGetValue(MemKey, out var mem) || string.IsNullOrEmpty(mem)) return false;
            var entries = new List<string>(mem.Split(';'));
            bool changed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var f = entries[i].Split('|');
                if (f.Length < 5 || !int.TryParse(f[3], out int x) || !int.TryParse(f[4], out int dy)) { entries.RemoveAt(i); continue; }
                var kind = KindAt(x, dy);
                if (kind == Kind.None || !KindOn(kind)) { entries.RemoveAt(i); continue; }
                if (inv.GetItemAt(x, b + dy) != null) continue;   // Slot schon belegt: später nochmal
                string id = f[0] + "|" + f[1] + "|" + f[2];
                foreach (var it in inv.GetAllItems())
                {
                    if (it?.m_shared == null || it.m_gridPos.y >= b || ItemId(it) != id || !Fits(it, kind, x)) continue;
                    it.m_gridPos = new Vector2i(x, b + dy);
                    entries.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
            if (entries.Count == 0) p.m_customData.Remove(MemKey);
            else p.m_customData[MemKey] = string.Join(";", entries);
            return changed;
        }

        /// <summary>Nach dem Laden mit ausgeschalteten Slots: Items aus der alten Extra-Reihe zurück ins Inventar.</summary>
        internal static void RescueItems(Inventory inv, int minRows = 0)
        {
            int h = Mathf.Max(inv.GetHeight(), minRows), w = inv.GetWidth();
            bool changed = false;
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                if (item == null || (item.m_gridPos.y < h && item.m_gridPos.x < w)) continue;
                var free = FindFreeNormal(inv);
                if (free.x < 0) break;
                item.m_gridPos = free;
                changed = true;
            }
            if (changed) Changed(inv);
        }

        // ---------- Schild automatisch ----------

        /// <summary>Einhandwaffe in die Hand genommen und linke Hand frei: Schild aus dem Schild-Slot anlegen.
        /// Bei Zweihandwaffen und Bögen nimmt Valheim den Schild selbst ab; er bleibt dann im Slot liegen.</summary>
        internal static void AutoShield(Player p, ItemDrop.ItemData item)
        {
            if (!ShieldOn || item?.m_shared == null) return;
            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon) return;
            if (p.LeftItem != null) return;
            var inv = p.GetInventory();
            int b = BaseHeight(inv);
            if (b < 0) return;
            var shield = inv.GetItemAt(ShieldX, b + 1);
            if (shield == null || shield.m_equipped || TypeOf(shield) != "Shield") return;
            p.EquipItem(shield, true);
        }

        /// <summary>Einhandwaffe weggelegt: Schild aus dem Schild-Slot ebenfalls ablegen (bleibt im Slot liegen).
        /// Beim Wechsel auf eine andere Einhandwaffe legt AutoShield ihn gleich wieder an.</summary>
        internal static void AutoUnshield(Player p, ItemDrop.ItemData item)
        {
            if (!ShieldOn || item?.m_shared == null) return;
            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon) return;
            var shield = p.LeftItem;
            if (shield == null || TypeOf(shield) != "Shield") return;
            var inv = p.GetInventory();
            int b = BaseHeight(inv);
            if (b < 0 || inv.GetItemAt(ShieldX, b + 1) != shield) return;
            p.UnequipItem(shield, true);
        }

        // ---------- Essen per Taste ----------

        internal static void HandleInput()
        {
            if (!FoodOn) return;
            var p = Player.m_localPlayer;
            if (p == null || Util.InputBlocked()) return;
            if (Cfg.FoodKey1.Value.IsDown()) Eat(p, 0);
            if (Cfg.FoodKey2.Value.IsDown()) Eat(p, 1);
            if (Cfg.FoodKey3.Value.IsDown()) Eat(p, 2);
        }

        private static void Eat(Player p, int index)
        {
            var inv = p.GetInventory();
            int b = BaseHeight(inv);
            if (b < 0) return;
            var item = inv.GetItemAt(FoodStart + index, b);
            if (item == null)
            {
                p.Message(MessageHud.MessageType.Center, L.T("Essensslot ", "Food slot ") + (index + 1) + L.T(" ist leer", " is empty"));
                return;
            }
            p.UseItem(inv, item, false);
        }

        /// <summary>"Y + LeftControl" → "Strg+Y".</summary>
        internal static string Pretty(BepInEx.Configuration.KeyboardShortcut k)
        {
            var parts = new List<string>();
            foreach (var m in k.Modifiers)
            {
                switch (m)
                {
                    case KeyCode.LeftControl: case KeyCode.RightControl: parts.Add(L.T("Strg", "Ctrl")); break;
                    case KeyCode.LeftShift: case KeyCode.RightShift: parts.Add("Shift"); break;
                    case KeyCode.LeftAlt: case KeyCode.RightAlt: parts.Add("Alt"); break;
                    default: parts.Add(m.ToString()); break;
                }
            }
            string main = k.MainKey.ToString();
            if (main.StartsWith("Alpha")) main = main.Substring(5);
            parts.Add(main);
            return string.Join("+", parts.ToArray());
        }

        // ---------- Anzeige im Inventarfenster ----------

        private static FieldInfo _fElements, _fSpace, _fAmount;
        private const string LabelName = "PschtQoL_SlotLabel";

        private const string HolderName = "PschtQoL_ExtraSlots";
        private static bool _holderLogged;

        /// <summary>
        /// Halter rechts oben neben dem Spieler-Inventarfenster. Er hängt NICHT im Fenster selbst
        /// (das schneidet in Valheim 1.0 alles außerhalb ab), sondern eine Ebene höher im Inventar-Root
        /// und wird jedes Mal an die rechte obere Ecke des Fensters gesetzt.
        /// </summary>
        private static RectTransform Holder(InventoryGrid grid)
        {
            var gui = InventoryGui.instance;
            if (gui == null) return null;
            var panel = Util.GetField(gui, "m_player") as RectTransform;
            var root = Util.GetField(gui, "m_inventoryRoot") as Transform;
            if (panel == null || root == null) return null;

            var t = root.Find(HolderName) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(HolderName, typeof(RectTransform));
                t = (RectTransform)go.transform;
                t.SetParent(root, false);
                t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
                t.pivot = new Vector2(0f, 1f);
                t.sizeDelta = Vector2.zero;
                Plugin.LogInfo("Extra-Slots: Halter im Inventar-Root angelegt");
            }

            // An die rechte obere Ecke des Spieler-Fensters setzen, gleiche Skalierung wie das Fenster
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);   // [2] = oben rechts
            Vector3 ps = panel.lossyScale, rs = t.parent.lossyScale;
            t.localScale = new Vector3(rs.x != 0 ? ps.x / rs.x : 1f, rs.y != 0 ? ps.y / rs.y : 1f, 1f);
            t.position = corners[2] + new Vector3(Cfg.ExtraSlotsOffsetX.Value * ps.x, -Cfg.ExtraSlotsOffsetY.Value * ps.y, 0f);
            t.SetAsLastSibling();
            // Fertigkeiten, Kompendium, Trophäen, Erfolge: Extra-Slots und zweite Leiste würden darüber liegen
            bool hide = Util.InventoryDialogOpen();
            if (t.gameObject.activeSelf == hide) t.gameObject.SetActive(!hide);
            EnsureBackground(t, grid);
            return t;
        }

        /// <summary>Dunkler Hintergrund mit hellem Rahmen hinter den Extra-Slots, damit man sie gut erkennt.</summary>
        // Drei Spalten: links Rüstung, Mitte Rute/Irrlicht + Gürtel + Schmuck, rechts Essen.
        // Ausgeschaltete Spalten fallen weg, die anderen rücken nach links.
        private static int ArmorColCount() => ArmorOn ? ArmorCount : 0;
        private static int MidColCount() => (UtilOn ? 2 : 0) + (BeltOn ? 1 : 0) + (KeyOn ? 1 : 0);
        private static int FoodColCount() => (FoodOn ? FoodCount : 0) + (ShieldOn ? 1 : 0);
        private static int MidCol() => ArmorColCount() > 0 ? 1 : 0;
        private static int FoodCol() => MidCol() + (MidColCount() > 0 ? 1 : 0);

        private const float Pad = 10f;        // Rand im Holzfenster
        private const float PanelGap = 14f;   // Abstand zwischen Extra-Slots-Fenster und Schnellleisten-Fenster

        /// <summary>Wie viele Reihen hat das obere Fenster (Rüstung / Zusatz / Essen)?</summary>
        private static int MainRows() => Mathf.Max(ArmorColCount(), Mathf.Max(MidColCount(), FoodColCount()));
        private static int MainCols() =>
            (ArmorColCount() > 0 ? 1 : 0) + (MidColCount() > 0 ? 1 : 0) + (FoodColCount() > 0 ? 1 : 0);

        /// <summary>Oberkante der zweiten Schnellleiste (unter dem oberen Fenster).</summary>
        private static float QuickTop(float space) => MainRows() > 0 ? MainRows() * space + Pad * 2f + PanelGap : 0f;

        private static void EnsureBackground(RectTransform holder, InventoryGrid grid)
        {
            float space = _fSpace?.GetValue(grid) is float f ? f : 70f;
            int rows = MainRows(), cols = MainCols();
            Panel(holder, "PschtQoL_ExtraSlotsBg", rows > 0, new Vector2(-Pad, Pad),
                  new Vector2(cols * space + Pad * 2f, rows * space + Pad * 2f));
            Panel(holder, "PschtQoL_Hotbar2Bg", QuickOn, new Vector2(-Pad, -QuickTop(space) + Pad),
                  new Vector2(QuickCount * space + Pad * 2f, space + Pad * 2f));
        }

        /// <summary>Holzfenster wie das Inventar (gleiches Bild). Ohne Bild: dunkler Kasten mit hellem Rahmen.</summary>
        private static void Panel(RectTransform holder, string name, bool show, Vector2 pos, Vector2 size)
        {
            var bg = holder.Find(name) as RectTransform;
            if (bg == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                bg = (RectTransform)go.transform;
                bg.SetParent(holder, false);
                bg.anchorMin = bg.anchorMax = new Vector2(0f, 1f);
                bg.pivot = new Vector2(0f, 1f);
                var img = go.AddComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
                if (!CopyWood(img))
                {
                    img.color = new Color(0.08f, 0.05f, 0.02f, 0.8f);
                    var ol = go.AddComponent<UnityEngine.UI.Outline>();
                    ol.effectColor = new Color(0.85f, 0.65f, 0.3f, 0.9f);
                    ol.effectDistance = new Vector2(2f, -2f);
                }
            }
            if (bg.gameObject.activeSelf != show) bg.gameObject.SetActive(show);
            bg.anchoredPosition = pos;
            bg.sizeDelta = size;
            bg.SetAsFirstSibling();   // hinter den Slots
        }

        private static bool _woodLogged;

        /// <summary>Holz-Hintergrund vom Spieler-Inventarfenster übernehmen (Bild, Art, Farbe).</summary>
        private static bool CopyWood(UnityEngine.UI.Image target)
        {
            var gui = InventoryGui.instance;
            var panel = gui != null ? Util.GetField(gui, "m_player") as RectTransform : null;
            if (panel == null) return false;
            UnityEngine.UI.Image src = null;
            foreach (var img in panel.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img == null || img.sprite == null) continue;
                string n = img.gameObject.name.ToLowerInvariant();
                if (n.Contains("bkg") || n.Contains("background") || n == panel.name.ToLowerInvariant())
                {
                    src = img;
                    break;
                }
            }
            if (src == null) src = panel.GetComponent<UnityEngine.UI.Image>();
            if (src == null || src.sprite == null) return false;
            target.sprite = src.sprite;
            target.type = src.type;
            target.color = src.color;
            target.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
            target.material = src.material;
            if (!_woodLogged)
            {
                _woodLogged = true;
                Plugin.LogInfo("Extra-Slots: Holz-Hintergrund von \"" + src.gameObject.name + "\" (Bild " + src.sprite.name + ")");
            }
            return true;
        }

        private static void LogLayoutOnce(Inventory inv, int moved)
        {
            if (_holderLogged) return;
            _holderLogged = true;
            Plugin.LogInfo("Extra-Slots: Inventar " + inv.GetWidth() + "x" + inv.GetHeight() + ", normale Reihen " +
                           BaseHeight(inv) + ", Extra-Felder verschoben: " + moved);
        }

        internal static void Layout(InventoryGrid grid, Inventory inv)
        {
            int b = BaseHeight(inv);
            if (b < 0) return;

            if (_fElements == null) _fElements = AccessTools.Field(typeof(InventoryGrid), "m_elements");
            if (_fSpace == null) _fSpace = AccessTools.Field(typeof(InventoryGrid), "m_elementSpace");
            var list = _fElements?.GetValue(grid) as IList;
            if (list == null) return;
            float space = _fSpace?.GetValue(grid) is float f ? f : 70f;
            int width = inv.GetWidth();
            float baseX = width * space + Cfg.ExtraSlotsOffsetX.Value;
            float baseY = -Cfg.ExtraSlotsOffsetY.Value;

            // Valheim 1.0: Das Spieler-Raster liegt in einem Scroll-Bereich mit Maske – alles außerhalb wird
            // abgeschnitten. Deshalb hängen die Extra-Felder an einem eigenen Halter rechts am Inventarfenster.
            var holder = Holder(grid);
            int moved = 0;
            try { LayoutElements(list, inv, b, space, baseX, baseY, holder, ref moved); }
            finally { LogLayoutOnce(inv, moved); }
        }

        private static void LayoutElements(IList list, Inventory inv, int b, float space, float baseX, float baseY,
                                           RectTransform holder, ref int moved)
        {
            foreach (var el in list)
            {
                if (el == null) continue;
                if (_fAmount == null) _fAmount = AccessTools.Field(el.GetType(), "m_amount");
                if (!Util.ElementPos(el, out var pos) || pos.y < b) continue;
                var go = Util.ElementGo(el);
                if (go == null) continue;

                int x = pos.x;
                var kind = KindAt(x, pos.y - b);
                bool food = kind == Kind.Food || kind == Kind.Quick;   // Beschriftung oben links (Taste)
                if (kind == Kind.None || !KindOn(kind)) { if (go.activeSelf) go.SetActive(false); continue; }
                if (!go.activeSelf) go.SetActive(true);

                var rt = go.transform as RectTransform;
                if (rt != null)
                {
                    // Links Rüstung, Mitte (von oben) Rute/Irrlicht, Gürtel, Schmuck, rechts Essen.
                    // Darunter in eigenem Fenster: zweite Schnellleiste (8 nebeneinander).
                    int col, row;
                    float top = 0f;
                    int r = 0;
                    int utilRow = r; if (UtilOn) r++;
                    int beltRow = r; if (BeltOn) r++;
                    int trinketRow = r; if (UtilOn) r++;
                    int keyRow = r;
                    int mid = MidCol();
                    switch (kind)
                    {
                        case Kind.Armor: col = 0; row = x; break;
                        case Kind.Utility: col = mid; row = utilRow; break;
                        case Kind.Belt: col = mid; row = beltRow; break;
                        case Kind.Trinket: col = mid; row = trinketRow; break;
                        case Kind.Key: col = mid; row = keyRow; break;
                        case Kind.Quick: col = x; row = 0; top = QuickTop(space); break;
                        case Kind.Shield: col = FoodCol(); row = FoodOn ? FoodCount : 0; break;
                        default: col = FoodCol(); row = x - FoodStart; break;
                    }
                    if (holder != null)
                    {
                        if (rt.parent != holder)
                        {
                            rt.SetParent(holder, false);
                            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                            rt.pivot = new Vector2(0f, 1f);
                        }
                        rt.anchoredPosition = new Vector2(col * space, -row * space - top);
                    }
                    else
                    {
                        rt.anchoredPosition = new Vector2(baseX + col * space, baseY - row * space - top);
                    }
                    moved++;
                }

                // Beschriftung
                var label = go.transform.Find(LabelName);
                if (label == null)
                {
                    var amount = _fAmount?.GetValue(el) as TMP_Text;
                    if (amount == null) continue;
                    var copy = UnityEngine.Object.Instantiate(amount.gameObject, go.transform);
                    copy.name = LabelName;
                    label = copy.transform;
                    var lrt = copy.transform as RectTransform;
                    var txt = copy.GetComponent<TMP_Text>();
                    if (food)
                    {
                        // Wie die Zahlen in der Schnellleiste: oben links IM Feld (immer sichtbar,
                        // nicht vom Feld darüber verdeckt)
                        lrt.anchorMin = new Vector2(0f, 1f);
                        lrt.anchorMax = new Vector2(0f, 1f);
                        lrt.pivot = new Vector2(0f, 1f);
                        lrt.anchoredPosition = new Vector2(4f, -2f);
                        lrt.sizeDelta = new Vector2(space, 18f);
                        txt.alignment = TextAlignmentOptions.TopLeft;
                        txt.fontSize = Mathf.Max(11f, amount.fontSize * 0.8f);
                        txt.color = new Color(1f, 0.85f, 0.4f, 1f);
                    }
                    else
                    {
                        lrt.anchorMin = new Vector2(0f, 0f);
                        lrt.anchorMax = new Vector2(1f, 1f);
                        lrt.pivot = new Vector2(0.5f, 0.5f);
                        lrt.anchoredPosition = Vector2.zero;
                        lrt.sizeDelta = Vector2.zero;
                        txt.alignment = TextAlignmentOptions.Center;
                        txt.fontSize = Mathf.Max(12f, amount.fontSize * 0.85f);
                        txt.color = new Color(0.95f, 0.85f, 0.65f, 0.85f);   // gut erkennbar
                    }
                    txt.enableAutoSizing = kind == Kind.Quick;   // "Shift+1" usw. passt sich der Slotbreite an
                    if (kind == Kind.Quick) { txt.fontSizeMax = Mathf.Max(10f, amount.fontSize * 0.7f); txt.fontSizeMin = 7f; txt.fontSize = txt.fontSizeMax; }
                    txt.raycastTarget = false;
                    txt.outlineWidth = 0.2f;
                    txt.outlineColor = new Color32(0, 0, 0, 255);
                }

                var labelText = label.GetComponent<TMP_Text>();
                // Die Kopie stammt von der Mengenanzeige, die das Spiel bei leeren Feldern ausschaltet
                if (labelText != null && !labelText.enabled) labelText.enabled = true;
                label.SetAsLastSibling();   // über dem Item-Bild
                bool show = Cfg.ExtraSlotsLabels.Value;
                if (kind == Kind.Quick)
                {
                    if (labelText != null) labelText.text = Hotbar2.KeyName() + "+" + (x + 1);
                }
                else if (food)
                {
                    var key = x == FoodStart ? Cfg.FoodKey1.Value : x == FoodStart + 1 ? Cfg.FoodKey2.Value : Cfg.FoodKey3.Value;
                    if (labelText != null) labelText.text = Pretty(key);
                }
                else
                {
                    string name = kind == Kind.Armor ? ArmorNames[x]
                                : kind == Kind.Utility ? L.T("Rute /\nIrrlicht", "Wishbone /\nWisp")
                                : kind == Kind.Belt ? L.T("Gürtel", "Belt") : kind == Kind.Shield ? L.T("Schild", "Shield") : kind == Kind.Key ? L.T("Schlüssel", "Key") : L.T("Schmuck", "Trinket");
                    if (labelText != null) labelText.text = name;
                    show = show && inv.GetItemAt(x, pos.y) == null;   // Name nur im leeren Slot
                }
                if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            }
        }
    }

    // ===================== Patches =====================

    /// <summary>Spieler-Inventar um eine Reihe erweitern (vor dem Laden).</summary>
    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class Player_Awake_ExtraSlots
    {
        private static void Postfix(Player __instance)
        {
            if (ExtraSlots.AnyEnabled) ExtraSlots.Extend(__instance.GetInventory());
        }
    }

    /// <summary>Inventarhöhe neu gesetzt (beim Spawnen): Extra-Reihe behalten, sonst fällt Rüstung auf den Boden.</summary>
    [HarmonyPatch(typeof(Inventory), "SetHeight")]
    internal static class Inventory_SetHeight_ExtraSlots
    {
        // "ref int height": Änderungen über __args werden in dieser Harmony-Version nicht übernommen
        private static void Prefix(Inventory __instance, ref int height)
        {
            ExtraSlots.OnSetHeight(__instance, ref height);
        }
    }

    /// <summary>Nach dem Laden: Rüstung in die Slots bzw. (Slots aus) Items aus der alten Reihe retten.</summary>
    [HarmonyPatch(typeof(Player), "Load")]
    internal static class Player_Load_ExtraSlots
    {
        private static void Postfix(Player __instance)
        {
            var inv = __instance.GetInventory();
            if (!ExtraSlots.AnyEnabled) { ExtraSlots.RescueItems(inv, Mathf.Min(InventorySize.MaxRows, InventorySize.VanillaRows(__instance) + InventorySize.Extra)); return; }
            ExtraSlots.SetLayoutBase(__instance, inv);
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
                if (item != null && item.m_equipped) ExtraSlots.PlaceEquipped(__instance, item);
        }
    }

    /// <summary>Vor dem Grabstein: Items aus den Extra-Slots ins normale Inventar legen, damit nichts verloren geht.</summary>
    [HarmonyPatch(typeof(Player), "CreateTombStone")]
    internal static class Player_CreateTombStone_ExtraSlots
    {
        private static void Prefix(Player __instance)
        {
            var inv = __instance.GetInventory();
            int b = ExtraSlots.BaseHeight(inv);
            if (b < 0) return;
            ExtraSlots.RememberSlots(__instance, inv, b);
            bool changed = false;
            foreach (var item in new List<ItemDrop.ItemData>(inv.GetAllItems()))
            {
                if (item == null || item.m_gridPos.y < b) continue;
                var free = ExtraSlots.FindFreeNormal(inv);
                if (free.x < 0) break;
                item.m_gridPos = free;
                changed = true;
            }
            if (changed) Util.Call(inv, "Changed");
        }
    }

    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    internal static class Humanoid_EquipItem_ExtraSlots
    {
        private static void Postfix(Humanoid __instance, object[] __args, bool __result)
        {
            if (!__result || !ExtraSlots.AnyEnabled || __args.Length == 0) return;
            if (__instance is Player p && p == Player.m_localPlayer && __args[0] is ItemDrop.ItemData item)
            {
                ExtraSlots.PlaceEquipped(p, item);
                ExtraSlots.AutoShield(p, item);
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), "UnequipItem")]
    internal static class Humanoid_UnequipItem_ExtraSlots
    {
        // War die Waffe wirklich in der rechten Hand? (UnequipItem wird auch für nicht angelegte Items aufgerufen)
        private static void Prefix(Humanoid __instance, object[] __args, out bool __state)
        {
            __state = __args.Length > 0 && __args[0] is ItemDrop.ItemData it && it == __instance.RightItem;
        }

        private static void Postfix(Humanoid __instance, object[] __args, bool __state)
        {
            if (!ExtraSlots.AnyEnabled || __args.Length == 0) return;
            if (__instance is Player p && p == Player.m_localPlayer && __args[0] is ItemDrop.ItemData item)
            {
                ExtraSlots.PlaceUnequipped(p, item);
                if (__state) ExtraSlots.AutoUnshield(p, item);
            }
        }
    }

    /// <summary>Neue Items landen nie automatisch in den Extra-Slots.</summary>
    [HarmonyPatch(typeof(Inventory), "FindEmptySlot")]
    internal static class Inventory_FindEmptySlot_ExtraSlots
    {
        private static void Postfix(Inventory __instance, object[] __args, ref Vector2i __result)
        {
            if (ExtraSlots.BaseHeight(__instance) < 0 || __result.y < 0) return;
            if (!ExtraSlots.IsExtraRow(__instance, __result.y)) return;
            bool topFirst = __args.Length == 0 || !(__args[0] is bool tf) || tf;
            __result = ExtraSlots.FindFreeNormal(__instance, topFirst);
        }
    }

    [HarmonyPatch(typeof(Inventory), "HaveEmptySlot")]
    internal static class Inventory_HaveEmptySlot_ExtraSlots
    {
        private static void Postfix(Inventory __instance, ref bool __result)
        {
            int b = ExtraSlots.BaseHeight(__instance);
            if (b < 0) return;
            __result = ExtraSlots.CountNormalItems(__instance) < __instance.GetWidth() * b;
        }
    }

    [HarmonyPatch(typeof(Inventory), "GetEmptySlots")]
    internal static class Inventory_GetEmptySlots_ExtraSlots
    {
        private static void Postfix(Inventory __instance, ref int __result)
        {
            int b = ExtraSlots.BaseHeight(__instance);
            if (b < 0) return;
            __result = Math.Max(0, __instance.GetWidth() * b - ExtraSlots.CountNormalItems(__instance));
        }
    }

    /// <summary>Extra-Reihe im Inventarfenster rechts neben das Inventar setzen.</summary>
    [HarmonyPatch(typeof(InventoryGrid), "UpdateInventory")]
    internal static class InventoryGrid_UpdateInventory_ExtraSlots
    {
        private static void Postfix(InventoryGrid __instance, object[] __args)
        {
            if (!ExtraSlots.AnyEnabled || __args.Length == 0) return;
            var inv = __args[0] as Inventory;
            if (inv == null || Player.m_localPlayer == null || inv != Player.m_localPlayer.GetInventory()) return;
            try { ExtraSlots.Layout(__instance, inv); }
            catch (Exception e) { Plugin.LogWarn("Extra-Slots Anzeige: " + e.Message); }
        }
    }
}
