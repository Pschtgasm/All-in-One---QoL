using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pscht_QoL
{
    /// <summary>
    /// Zweite Schnellleiste: eigene 8 Slots (Extra-Reihe, im Inventar unter den Extra-Slots), oben unter der normalen Leiste angezeigt.
    /// Benutzen mit Alt+1 … Alt+8.
    /// </summary>
    internal static class Hotbar2
    {
        internal const int Count = 8;
        /// <summary>Reihe der zweiten Schnellleiste: eigene Extra-Reihe (unter den Extra-Slots), sonst 2. Inventarreihe.</summary>
        internal static int RowOf(Inventory inv)
        {
            int b = ExtraSlots.BaseHeight(inv);
            return b >= 0 ? b + ExtraSlots.QuickRow : 1;
        }

        private class Slot
        {
            public GameObject Go;
            public Image Icon;
            public GuiBar Durability;
            public TMP_Text Amount;
            public GameObject Equipped, Queued, Selected;
            public int StackText = -1;
        }

        private static readonly List<Slot> _slots = new List<Slot>();
        private static RectTransform _root;

        internal static bool ModifierHeld()
        {
            if (!Cfg.Hotbar2Enabled.Value) return false;
            var k = Cfg.Hotbar2Modifier.Value;
            if (k == KeyCode.LeftAlt || k == KeyCode.RightAlt)
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            return Input.GetKey(k);
        }

        internal static string KeyName()
        {
            switch (Cfg.Hotbar2Modifier.Value)
            {
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                case KeyCode.LeftControl: case KeyCode.RightControl: return L.T("Strg", "Ctrl");
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                default: return Cfg.Hotbar2Modifier.Value.ToString();
            }
        }

        /// <summary>Wird von HotkeyBar.Update aufgerufen: Leiste anlegen und Icons aktualisieren.</summary>
        internal static void Update(HotkeyBar bar)
        {
            var player = Player.m_localPlayer;
            bool show = Cfg.Hotbar2Enabled.Value && player != null && !player.IsDead() && !Util.InventoryDialogOpen();
            if (!show || bar.m_elementPrefab == null)
            {
                if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }
            var inv = player.GetInventory();
            if (inv == null) return;
            int row = RowOf(inv);
            if (inv.GetHeight() <= row) return;

            if (_root == null) Build(bar);
            if (_root == null) return;
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            if (_group != null) _group.alpha = Mathf.Clamp(Cfg.Hotbar2Opacity.Value, 0.1f, 1f);

            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                var item = inv.GetItemAt(i, row);
                if (item == null)
                {
                    s.Icon.gameObject.SetActive(false);
                    s.Durability.gameObject.SetActive(false);
                    s.Equipped.SetActive(false);
                    s.Queued.SetActive(false);
                    s.Amount.gameObject.SetActive(false);
                    continue;
                }
                s.Icon.gameObject.SetActive(true);
                s.Icon.sprite = item.GetIcon();
                bool dur = item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability();
                s.Durability.gameObject.SetActive(dur);
                if (dur)
                {
                    if (item.m_durability <= 0f)
                    {
                        s.Durability.SetValue(1f);
                        s.Durability.SetColor(Mathf.Sin(Time.time * 10f) > 0f ? Color.red : new Color(0f, 0f, 0f, 0f));
                    }
                    else
                    {
                        s.Durability.SetValue(item.GetDurabilityPercentage());
                        if (Cfg.DurabilityColors.Value) s.Durability.SetColor(DurabilityColor.For(item.GetDurabilityPercentage()));
                        else s.Durability.ResetColor();
                    }
                }
                s.Equipped.SetActive(item.m_equipped);
                s.Queued.SetActive(player.IsEquipActionQueued(item));
                if (item.m_shared.m_maxStackSize > 1)
                {
                    s.Amount.gameObject.SetActive(true);
                    if (s.StackText != item.m_stack)
                    {
                        s.Amount.text = item.m_stack + " / " + item.m_shared.m_maxStackSize;
                        s.StackText = item.m_stack;
                    }
                }
                else s.Amount.gameObject.SetActive(false);
            }
        }

        private static CanvasGroup _group;

        private static void Build(HotkeyBar bar)
        {
            _slots.Clear();
            var go = new GameObject("PschtQoL_Hotbar2", typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(bar.transform, false);
            _root.localScale = Vector3.one;
            float space = bar.m_elementSpace;
            // eine Reihe tiefer als die normale Leiste
            _root.localPosition = new Vector3(0f, -space, 0f);
            // Durchsichtigkeit regeln, Klicks gehen durch
            _group = go.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = true;   // false würde die Felder in der "gesperrt"-Farbe zeigen

            for (int i = 0; i < Count; i++)
            {
                var el = UnityEngine.Object.Instantiate(bar.m_elementPrefab, _root);
                el.name = "PschtQoL_Hotbar2_" + (i + 1);
                el.transform.localPosition = new Vector3(i * space, 0f, 0f);
                var s = new Slot { Go = el };
                var binding = el.transform.Find("binding")?.GetComponent<TMP_Text>();
                if (binding != null)
                {
                    // Klein oben links im Slot, wie "Alt + 1" – passt in die Slotbreite
                    binding.text = KeyName() + "+" + (i + 1);
                    binding.enableWordWrapping = false;
                    binding.enableAutoSizing = true;
                    binding.overflowMode = TextOverflowModes.Overflow;
                    float fs = Mathf.Max(10f, binding.fontSize * 0.6f);
                    binding.fontSizeMax = fs; binding.fontSizeMin = 7f; binding.fontSize = fs;
                    binding.alignment = TextAlignmentOptions.TopLeft;
                    var brt = binding.rectTransform;
                    brt.anchorMin = new Vector2(0f, 1f);
                    brt.anchorMax = new Vector2(1f, 1f);
                    brt.pivot = new Vector2(0f, 1f);
                    brt.anchoredPosition = new Vector2(4f, -2f);
                    brt.sizeDelta = new Vector2(-4f, 18f);
                }
                s.Icon = el.transform.Find("icon").GetComponent<Image>();
                s.Durability = el.transform.Find("durability").GetComponent<GuiBar>();
                s.Amount = el.transform.Find("amount").GetComponent<TMP_Text>();
                s.Equipped = el.transform.Find("equiped").gameObject;
                s.Queued = el.transform.Find("queued").gameObject;
                s.Selected = el.transform.Find("selected")?.gameObject;
                if (s.Selected != null) s.Selected.SetActive(false);
                // Klicks sollen nichts auslösen
                var btn = el.GetComponent<Button>();
                // Knopf NICHT abschalten oder sperren: Unity färbt den Hintergrund dann weiß bzw. deckend.
                // Er bleibt wie in der normalen Leiste, Klicks blockiert die CanvasGroup (blocksRaycasts = false).
                if (btn != null) btn.onClick.RemoveAllListeners();
                _slots.Add(s);
            }
            Plugin.LogInfo("Zweite Schnellleiste angelegt (" + Count + " Plätze, Alt+1-8)");
        }

        /// <summary>Item aus der zweiten Leiste benutzen (index 1-8).</summary>
        internal static void Use(Player player, int index)
        {
            var inv = player.GetInventory();
            if (inv == null) return;
            var item = inv.GetItemAt(index - 1, RowOf(inv));
            if (item != null) player.UseItem(null, item, false);
        }
    }

    [HarmonyPatch(typeof(HotkeyBar), "Update")]
    internal static class HotkeyBar_Update_Hotbar2
    {
        private static void Postfix(HotkeyBar __instance)
        {
            try { Hotbar2.Update(__instance); }
            catch (Exception e) { Plugin.LogWarn("Zweite Schnellleiste: " + e.Message); }
        }
    }

    /// <summary>Alt + Zahl: statt der normalen Leiste die zweite benutzen.</summary>
    [HarmonyPatch(typeof(Player), "UseHotbarItem")]
    internal static class Player_UseHotbarItem_Hotbar2
    {
        private static bool Prefix(Player __instance, int index)
        {
            if (!Hotbar2.ModifierHeld()) return true;
            try { Hotbar2.Use(__instance, index); }
            catch (Exception e) { Plugin.LogWarn("Zweite Schnellleiste: " + e.Message); }
            return false;
        }
    }
}
