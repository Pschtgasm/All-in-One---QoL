using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pscht_QoL
{
    /// <summary>
    /// An Herstellstationen:
    /// - Anzahl-Feld rechts neben dem (gekürzten) Herstellen-Knopf: [−] [Anzahl] [+] [Max] – stellt so viele auf einmal her.
    ///   Nutzt Valheims eigenes Mehrfach-Herstellen (sonst Shift = 5 Stück), nur mit frei wählbarer Anzahl.
    /// - Hinter jedem Rezept in der Liste: wie oft du es mit deinem Material (inkl. Kisten in der Nähe) herstellen kannst.
    /// </summary>
    internal static class CraftAmount
    {
        internal static int Amount = 1;
        private const int VanillaMulti = 5;

        private static readonly FieldInfo TouchMulti = AccessTools.Field(typeof(InventoryGui), "m_touchMultiCrafting");
        private static readonly FieldInfo CraftTimer = AccessTools.Field(typeof(InventoryGui), "m_craftTimer");
        private static readonly FieldInfo Selected = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
        private static readonly FieldInfo AvailableField = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");

        private static GameObject _row;
        private static TMP_Text _amountText;
        private static bool _craftSaved;
        private static Vector2 _craftSize, _craftPos, _craftAnchorMin, _craftAnchorMax, _craftPivot;
        private static Rect _craftRect;

        /// <summary>Herstellen-Knopf wieder auf Originalgröße, wenn das Anzahl-Feld weg ist.</summary>
        private static void RestoreCraft(InventoryGui gui)
        {
            if (!_craftSaved || gui == null || gui.m_craftButton == null) return;
            var craft = gui.m_craftButton.transform as RectTransform;
            if (craft == null) return;
            craft.anchorMin = _craftAnchorMin; craft.anchorMax = _craftAnchorMax; craft.pivot = _craftPivot;
            craft.sizeDelta = _craftSize;
            craft.anchoredPosition = _craftPos;
            _craftSaved = false;
        }

        private static bool AtStation(Player p) => p != null && p.GetCurrentCraftingStation() != null;

        private static object SelectedPair(InventoryGui gui) => Selected != null ? Selected.GetValue(gui) : null;

        private static Recipe RecipeOf(object pair) => pair?.GetType().GetProperty("Recipe")?.GetValue(pair, null) as Recipe;
        private static ItemDrop.ItemData DataOf(object pair) => pair?.GetType().GetProperty("ItemData")?.GetValue(pair, null) as ItemDrop.ItemData;

        /// <summary>Wie oft kann der Spieler das Rezept (neu, Stufe 1) herstellen? -1 = kein Material nötig.</summary>
        internal static int Craftable(Player p, Recipe r)
        {
            if (p == null || r == null || r.m_resources == null) return -1;
            var inv = p.GetInventory();
            bool onlyOne = r.m_requireOnlyOneIngredient;
            int best = onlyOne ? 0 : int.MaxValue;
            bool any = false;
            foreach (var req in r.m_resources)
            {
                if (req == null || req.m_resItem == null) continue;
                int need = req.GetAmount(1);
                if (need <= 0) continue;
                any = true;
                int have = inv.CountItems(req.m_resItem.m_itemData.m_shared.m_name);
                int n = have / need;
                best = onlyOne ? Math.Max(best, n) : Math.Min(best, n);
            }
            return any ? best : -1;
        }

        // ---------- Valheims Mehrfach-Herstellen steuern ----------

        internal static void BeforeUpdateRecipe(InventoryGui gui)
        {
            if (gui == null) return;
            bool on = Cfg.CraftAmountField.Value && AtStation(Player.m_localPlayer);
            bool crafting = CraftTimer != null && CraftTimer.GetValue(gui) is float t && t >= 0f;
            if (crafting) return;   // während des Herstellens nichts ändern
            if (on && Amount > 1)
            {
                gui.m_multiCraftAmount = Amount;
                TouchMulti?.SetValue(gui, true);
            }
            else
            {
                gui.m_multiCraftAmount = VanillaMulti;
                TouchMulti?.SetValue(gui, false);
            }
        }

        internal static void AfterUpdateRecipe(InventoryGui gui)
        {
            var p = Player.m_localPlayer;
            bool show = Cfg.CraftAmountField.Value && AtStation(p) && gui.InCraftTab();
            var pair = SelectedPair(gui);
            var recipe = RecipeOf(pair);
            show = show && recipe != null && DataOf(pair) == null;   // nur beim Neu-Herstellen, nicht beim Aufwerten
            if (!show)
            {
                if (_row != null && _row.activeSelf) _row.SetActive(false);
                RestoreCraft(gui);
                return;
            }
            if (_row == null) Build(gui);
            if (_row == null) return;
            if (!_row.activeSelf) _row.SetActive(true);
            bool crafting = CraftTimer != null && CraftTimer.GetValue(gui) is float t && t >= 0f;
            foreach (var b in _row.GetComponentsInChildren<Button>(true)) b.interactable = !crafting;
            if (_amountText != null) _amountText.text = Amount.ToString();
            Position(gui);
        }

        private static void Change(int delta, bool max)
        {
            var gui = InventoryGui.instance;
            if (gui == null) return;
            if (max)
            {
                int n = Craftable(Player.m_localPlayer, RecipeOf(SelectedPair(gui)));
                Amount = n > 0 ? Math.Min(n, 999) : 1;
            }
            else
            {
                int step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1;
                Amount = Mathf.Clamp(Amount + delta * step, 1, 999);
            }
            if (_amountText != null) _amountText.text = Amount.ToString();
        }

        // ---------- Anzeige ----------

        private static void Build(InventoryGui gui)
        {
            var craft = gui.m_craftButton;
            if (craft == null) return;
            _craftSaved = false;
            var parent = craft.transform.parent;
            _row = new GameObject("PQ_CraftAmount", typeof(RectTransform));
            _row.transform.SetParent(parent, false);

            MakeButton(craft, _row.transform, "−", () => Change(-1, false));
            var mid = MakeButton(craft, _row.transform, "1", null);
            _amountText = mid.GetComponentInChildren<TMP_Text>();
            var midBtn = mid.GetComponent<Button>();
            if (midBtn != null) { midBtn.transition = Selectable.Transition.None; midBtn.onClick.AddListener(() => { Amount = 1; Change(0, false); }); }
            MakeButton(craft, _row.transform, "+", () => Change(1, false));
            MakeButton(craft, _row.transform, "Max", () => Change(0, true));
        }

        private static GameObject MakeButton(Button template, Transform parent, string text, Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            go.name = "PQ_CraftAmount_" + text;
            // Nichts vom Herstellen-Knopf übernehmen außer dem Aussehen
            foreach (var et in go.GetComponentsInChildren<EventTrigger>(true)) UnityEngine.Object.Destroy(et);
            foreach (var tip in go.GetComponentsInChildren<UITooltip>(true)) UnityEngine.Object.Destroy(tip);
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick = new Button.ButtonClickedEvent();
                if (onClick != null) btn.onClick.AddListener(() => onClick());
                btn.interactable = true;
            }
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                t.text = text;
                t.enableAutoSizing = true;
                t.fontSizeMin = 8f;
            }
            go.SetActive(true);
            return go;
        }

        private static void Position(InventoryGui gui)
        {
            var craft = gui.m_craftButton.transform as RectTransform;
            var row = _row.transform as RectTransform;
            var parent = craft != null ? craft.parent as RectTransform : null;
            if (craft == null || row == null || parent == null) return;
            // Original des Herstellen-Knopfs einmal merken (Lage in Eltern-Koordinaten über die echten Ecken)
            if (!_craftSaved)
            {
                _craftAnchorMin = craft.anchorMin; _craftAnchorMax = craft.anchorMax; _craftPivot = craft.pivot;
                _craftSize = craft.sizeDelta; _craftPos = craft.anchoredPosition;
                var corners = new Vector3[4];
                craft.GetWorldCorners(corners);
                Vector3 a = parent.InverseTransformPoint(corners[0]), b = parent.InverseTransformPoint(corners[2]);
                _craftRect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                _craftSaved = true;
            }
            // Herstellen bleibt links wie im Spiel und wird rechts kürzer; rechts daneben [−] [Anzahl] [+] [Max].
            // So bleiben die Material-Bilder darüber frei.
            Rect full = _craftRect;
            float h = full.height, gapMid = 6f;
            float craftW = full.width * 0.5f;
            float w = full.width - craftW - gapMid;
            Place(parent, craft, new Rect(full.xMin, full.yMin, craftW, h));
            Place(parent, row, new Rect(full.xMin + craftW + gapMid, full.yMin, w, h));
            row.SetAsLastSibling();

            // [−] [Anzahl] [+] [Max]: Anteile 1 : 1.3 : 1 : 1.5
            float[] parts = { 1f, 1.3f, 1f, 1.5f };
            float sum = 0f; foreach (var f in parts) sum += f;
            float gap = 4f, unit = (w - gap * (parts.Length - 1)) / sum, x = 0f;
            for (int i = 0; i < row.childCount && i < parts.Length; i++)
            {
                var c = row.GetChild(i) as RectTransform;
                if (c == null) continue;
                c.anchorMin = c.anchorMax = new Vector2(0f, 0.5f);
                c.pivot = new Vector2(0f, 0.5f);
                c.sizeDelta = new Vector2(parts[i] * unit, h);
                c.anchoredPosition = new Vector2(x, 0f);
                x += parts[i] * unit + gap;
            }
        }

        /// <summary>RectTransform genau auf ein Rechteck in Eltern-Koordinaten setzen (Anker in der Eltern-Mitte).</summary>
        private static void Place(RectTransform parent, RectTransform rt, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector3 s = rt.localScale;   // falls skaliert: Größe in eigenen Einheiten
            rt.sizeDelta = new Vector2(r.width / (Mathf.Abs(s.x) > 0.001f ? s.x : 1f), r.height / (Mathf.Abs(s.y) > 0.001f ? s.y : 1f));
            rt.anchoredPosition = r.center - parent.rect.center;
        }

        // ---------- "×N" hinter jedem Rezept ----------

        internal static void ShowCounts(InventoryGui gui)
        {
            if (gui == null || !Cfg.CraftShowCounts.Value) return;
            var p = Player.m_localPlayer;
            if (!AtStation(p) || !gui.InCraftTab()) return;
            var list = AvailableField != null ? AvailableField.GetValue(gui) as IList : null;
            if (list == null || list.Count == 0) return;
            var t = list[0].GetType();
            PropertyInfo rp = t.GetProperty("Recipe"), ip = t.GetProperty("ItemData"), ep = t.GetProperty("InterfaceElement");
            if (rp == null || ep == null) return;

            foreach (var pair in list)
            {
                var el = ep.GetValue(pair, null) as GameObject;
                if (el == null) continue;
                var old = el.transform.Find("PQ_Count");
                if (ip != null && ip.GetValue(pair, null) != null) { if (old != null) old.gameObject.SetActive(false); continue; }   // Aufwerten
                int n = Craftable(p, rp.GetValue(pair, null) as Recipe);
                var label = old != null ? old.GetComponent<TMP_Text>() : MakeCountLabel(el);
                if (label == null) continue;
                label.gameObject.SetActive(n > 0);
                label.text = n > 999 ? "×999+" : "×" + n;
            }
        }

        private const float CountW = 36f;

        private static TMP_Text MakeCountLabel(GameObject el)
        {
            var name = el.transform.Find("name");
            var src = name != null ? name.GetComponent<TMP_Text>() : null;
            if (src == null) return null;
            var go = UnityEngine.Object.Instantiate(src.gameObject, el.transform, false);
            go.name = "PQ_Count";
            foreach (Transform child in go.transform) UnityEngine.Object.Destroy(child.gameObject);
            var rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(CountW, 0f);
            rt.anchoredPosition = new Vector2(-4f, 0f);
            // Rezeptname endet vor der Zahl (bricht lieber um, statt darunter zu laufen)
            var nrt = name as RectTransform;
            if (nrt != null) nrt.offsetMax = new Vector2(nrt.offsetMax.x - (CountW + 4f), nrt.offsetMax.y);
            var txt = go.GetComponent<TMP_Text>();
            txt.alignment = TextAlignmentOptions.MidlineRight;
            txt.enableAutoSizing = false;
            txt.fontSize = Mathf.Max(11f, src.fontSize * 0.8f);
            txt.color = new Color(0.85f, 0.78f, 0.6f, 0.9f);
            txt.raycastTarget = false;
            return txt;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    internal static class InventoryGui_UpdateRecipe_CraftAmount
    {
        private static void Prefix(InventoryGui __instance)
        {
            try { CraftAmount.BeforeUpdateRecipe(__instance); } catch { }
        }

        private static void Postfix(InventoryGui __instance)
        {
            try { CraftAmount.AfterUpdateRecipe(__instance); }
            catch (Exception e) { Plugin.LogWarn("Anzahl-Feld: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
    internal static class InventoryGui_UpdateRecipeList_CraftCounts
    {
        private static void Postfix(InventoryGui __instance)
        {
            try { CraftAmount.ShowCounts(__instance); }
            catch (Exception e) { Plugin.LogWarn("Herstell-Anzahl: " + e.Message); }
        }
    }
}
