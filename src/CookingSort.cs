using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pscht_QoL
{
    /// <summary>
    /// Herstellmenü mit aufklappbaren Gruppen und 3 Sortier-Knöpfen oben rechts.
    /// - Essen (Kessel, Zubereitungstisch, Metkessel, andere Stationen mit Essen):
    ///   Gruppen Gesundheit / Ausdauer / Eitr / Tränke und Met / Sonstiges, Knöpfe Gesundheit / Ausdauer / Eitr.
    ///   Rohe Gerichte werden nach dem fertig gekochten Essen eingeordnet.
    /// - Ausrüstung (Werkbank, Schmiede, Schwarzmetall-Schmiede, Galdrtisch, Handwerkstisch, andere):
    ///   Gruppen nach Waffenart, Schilde, Rüstung, Werkzeuge, Munition, Sonstiges; Knöpfe Schaden / Rüstung / Blocken.
    /// </summary>
    internal static class CookingSort
    {
        /// <summary>Eine Art von Sortierung (Essen oder Ausrüstung) mit eigenem Zustand und eigenen Knöpfen.</summary>
        private sealed class Profile
        {
            public string Name;
            public string[] GroupNames;
            public bool[] Open;
            public int SortMode;                       // 0 = normal, 1–3 = Knopf
            public string[] Labels, Icons;
            public Color[] Fallback;
            public Func<object, int> Classify;         // Gruppe, -1 = gehört nicht dazu (zählt nicht für "aktiv")
            public Func<object, float> Stat;           // Wert für den aktiven Knopf
            public Func<bool> GroupsOn, SortOn;

            public GameObject Bar;
            public Image[] Images;
            public GameObject[] BarLabels;
        }

        private static readonly List<GameObject> Headers = new List<GameObject>();

        private static readonly FieldInfo AvailableField = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
        private static readonly FieldInfo BaseSizeField = AccessTools.Field(typeof(InventoryGui), "m_recipeListBaseSize");
        private static readonly MethodInfo UpdatePanel = AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel", new[] { typeof(bool) });

        private static PropertyInfo _rp, _ip, _ep;   // Recipe, ItemData, InterfaceElement von InventoryGui.RecipeDataPair

        private static Recipe RecipeOf(object pair) => _rp != null ? _rp.GetValue(pair, null) as Recipe : null;
        private static ItemDrop.ItemData DataOf(object pair) => _ip != null ? _ip.GetValue(pair, null) as ItemDrop.ItemData : null;

        // =====================================================================
        // Essen
        // =====================================================================

        // Roh → fertig: was Kochstelle, Ofen oder Gärbottich aus einem Item machen (z.B. rohe Fleischplatte → Fleischplatte)
        private static readonly Dictionary<string, ItemDrop> Cooked = new Dictionary<string, ItemDrop>();

        private static void BuildCookedMap()
        {
            if (Cooked.Count > 0 || ZNetScene.instance == null) return;
            foreach (var go in ZNetScene.instance.m_prefabs)
            {
                if (go == null) continue;
                var cs = go.GetComponent<CookingStation>();
                if (cs != null && cs.m_conversion != null)
                    foreach (var c in cs.m_conversion)
                        if (c != null && c.m_from != null && c.m_to != null && !Cooked.ContainsKey(c.m_from.name)) Cooked[c.m_from.name] = c.m_to;
                var f = go.GetComponent<Fermenter>();
                if (f != null && f.m_conversion != null)
                    foreach (var c in f.m_conversion)
                        if (c != null && c.m_from != null && c.m_to != null && !Cooked.ContainsKey(c.m_from.name)) Cooked[c.m_from.name] = c.m_to;
            }
        }

        /// <summary>Werte des fertigen Essens/Getränks: das Item selbst oder, wenn es roh ist, was daraus gekocht wird.</summary>
        private static ItemDrop.ItemData.SharedData FoodShared(Recipe r)
        {
            var drop = r != null ? r.m_item : null;
            var s = drop != null ? drop.m_itemData?.m_shared : null;
            if (s == null) return null;
            BuildCookedMap();
            for (int i = 0; i < 3 && s.m_itemType != ItemDrop.ItemData.ItemType.Consumable; i++)
            {
                if (!Cooked.TryGetValue(drop.name, out var next) || next == null || next.m_itemData?.m_shared == null) break;
                drop = next;
                s = next.m_itemData.m_shared;
            }
            return s;
        }

        private static int FoodGroup(object pair)
        {
            var s = FoodShared(RecipeOf(pair));
            if (s == null || s.m_itemType != ItemDrop.ItemData.ItemType.Consumable) return -1;   // Sonstiges
            if (s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f)
            {
                if (s.m_foodEitr > 0f && s.m_foodEitr >= s.m_food && s.m_foodEitr >= s.m_foodStamina) return 2;
                return s.m_foodStamina > s.m_food ? 1 : 0;
            }
            return 3;
        }

        private static readonly Profile Food = new Profile
        {
            Name = "Essen",
            GroupNames = L.De ? new[] { "Gesundheit", "Ausdauer", "Eitr", "Tränke und Met", "Zutaten und Sonstiges" } : new[] { "Health", "Stamina", "Eitr", "Potions and mead", "Ingredients and other" },
            Open = new[] { true, true, true, true, true },
            Labels = L.De ? new[] { "Gesundheit", "Ausdauer", "Eitr" } : new[] { "Health", "Stamina", "Eitr" },
            Icons = new[] { "MeadHealthMinor", "MeadStaminaMinor", "MeadEitrMinor" },
            Fallback = new[] { new Color(0.8f, 0.2f, 0.2f), new Color(0.9f, 0.75f, 0.2f), new Color(0.3f, 0.5f, 0.95f) },
            Classify = FoodGroup,
            Stat = pair =>
            {
                var s = FoodShared(RecipeOf(pair));
                if (s == null) return 0f;
                switch (Food.SortMode) { case 1: return s.m_food; case 2: return s.m_foodStamina; case 3: return s.m_foodEitr; default: return 0f; }
            },
            GroupsOn = () => Cfg.CookGroups.Value,
            SortOn = () => Cfg.CookSortButtons.Value,
        };

        // =====================================================================
        // Ausrüstung
        // =====================================================================

        private static int GearGroup(object pair)
        {
            var r = RecipeOf(pair);
            var s = r != null && r.m_item != null ? r.m_item.m_itemData?.m_shared : null;
            if (s == null) return -1;
            switch (s.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable: return 13;
                case ItemDrop.ItemData.ItemType.Shield:
                    return (r.m_item.name ?? "").IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0 ? 10 : 9;
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Hands: return 11;
                case ItemDrop.ItemData.ItemType.Tool: return 12;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    switch (s.m_skillType)
                    {
                        case Skills.SkillType.Swords: return 0;
                        case Skills.SkillType.Axes: return 1;
                        case Skills.SkillType.Clubs: return 2;
                        case Skills.SkillType.Knives: return 3;
                        case Skills.SkillType.Spears: return 4;
                        case Skills.SkillType.Polearms: return 5;
                        case Skills.SkillType.Unarmed: return 6;
                        case Skills.SkillType.Bows:
                        case Skills.SkillType.Crossbows: return 7;
                        case Skills.SkillType.ElementalMagic:
                        case Skills.SkillType.BloodMagic: return 8;
                        case Skills.SkillType.Pickaxes:
                        case Skills.SkillType.Fishing: return 12;
                        default: return -1;
                    }
                default: return -1;
            }
        }

        /// <summary>Stufe, die hergestellt wird: neu = 1, Aufwerten = aktuelle Stufe + 1.</summary>
        private static int CraftQuality(object pair, ItemDrop.ItemData item)
        {
            var have = DataOf(pair);
            int q = have != null ? have.m_quality + 1 : 1;
            return Mathf.Clamp(q, 1, Mathf.Max(1, item.m_shared.m_maxQuality));
        }

        /// <summary>Kampfschaden: alle Schadensarten außer Holzfällen und Abbau.</summary>
        private static float CombatDamage(HitData.DamageTypes d) =>
            d.m_damage + d.m_blunt + d.m_slash + d.m_pierce + d.m_fire + d.m_frost + d.m_lightning + d.m_poison + d.m_spirit;

        private static readonly Profile Gear = new Profile
        {
            Name = "Ausrüstung",
            GroupNames = L.De ? new[] { "Schwerter", "Äxte", "Keulen", "Messer", "Speere", "Stangenwaffen", "Faustwaffen",
                                 "Bögen und Armbrüste", "Stäbe", "Schilde", "Turmschilde", "Rüstung", "Werkzeuge", "Munition", "Sonstiges" }
                             : new[] { "Swords", "Axes", "Clubs", "Knives", "Spears", "Polearms", "Fists",
                                 "Bows and crossbows", "Staffs", "Shields", "Tower shields", "Armor", "Tools", "Ammo", "Other" },
            Open = new[] { true, true, true, true, true, true, true, true, true, true, true, true, true, true, true },
            Labels = L.De ? new[] { "Schaden", "Rüstung", "Blocken" } : new[] { "Damage", "Armor", "Block" },
            Icons = new[] { "SwordIron", "ArmorIronChest", "ShieldBanded" },
            Fallback = new[] { new Color(0.75f, 0.75f, 0.8f), new Color(0.55f, 0.45f, 0.35f), new Color(0.6f, 0.4f, 0.2f) },
            Classify = GearGroup,
            Stat = pair =>
            {
                var r = RecipeOf(pair);
                var item = r != null && r.m_item != null ? r.m_item.m_itemData : null;
                if (item?.m_shared == null) return 0f;
                int q = CraftQuality(pair, item);
                switch (Gear.SortMode)
                {
                    case 1: return CombatDamage(item.GetDamage(q, 0f));
                    case 2: return item.GetArmor(q, 0f);
                    case 3: return item.GetBaseBlockPower(q);
                    default: return 0f;
                }
            },
            GroupsOn = () => Cfg.GearGroups.Value,
            SortOn = () => Cfg.GearSortButtons.Value,
        };

        // =====================================================================
        // Stationen
        // =====================================================================

        private static bool NameIs(CraftingStation cs, string prefab) =>
            cs.gameObject.name.StartsWith(prefab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(cs.m_name, "$" + prefab, StringComparison.OrdinalIgnoreCase);

        /// <summary>Welche Sortierung gilt an dieser Station? null = keine. "andere" Stationen erst nach Blick in die Liste.</summary>
        private static Profile ProfileFor(CraftingStation cs, out bool needsCheck)
        {
            needsCheck = false;
            if (cs == null || cs.m_name == "$vfp_station") return null;   // Station mit eigener Sortierung aus einem anderen Mod
            if (NameIs(cs, "piece_MeadCauldron")) return Cfg.CookAtMeadCauldron.Value ? Food : null;
            if (NameIs(cs, "piece_cauldron")) return Cfg.CookAtCauldron.Value ? Food : null;
            if (NameIs(cs, "piece_preptable")) return Cfg.CookAtPrepTable.Value ? Food : null;
            if (NameIs(cs, "blackforge")) return Cfg.GearAtBlackForge.Value ? Gear : null;
            if (NameIs(cs, "forge") || NameIs(cs, "piece_forge")) return Cfg.GearAtForge.Value ? Gear : null;
            if (NameIs(cs, "piece_workbench")) return Cfg.GearAtWorkbench.Value ? Gear : null;
            if (NameIs(cs, "piece_magetable")) return Cfg.GearAtMageTable.Value ? Gear : null;
            if (NameIs(cs, "piece_artisanstation")) return Cfg.GearAtArtisan.Value ? Gear : null;
            needsCheck = true;
            return null;
        }

        private static bool Any(IList list, Profile p)
        {
            foreach (var it in list) if (p.Classify(it) >= 0 && (p != Food || p.Classify(it) <= 3)) return true;
            return false;
        }

        // =====================================================================
        // Hauptteil
        // =====================================================================

        internal static void Apply(InventoryGui gui)
        {
            foreach (var h in Headers) if (h != null) UnityEngine.Object.Destroy(h);
            Headers.Clear();
            if (gui == null) return;

            var player = Player.m_localPlayer;
            var cs = player != null ? player.GetCurrentCraftingStation() : null;
            IList list = AvailableField != null ? AvailableField.GetValue(gui) as IList : null;

            Profile p = null;
            if (cs != null && gui.InCraftTab() && list != null && list.Count > 0)
            {
                var t = list[0].GetType();
                _rp = t.GetProperty("Recipe");
                _ip = t.GetProperty("ItemData");
                _ep = t.GetProperty("InterfaceElement");
                if (_rp != null && _ep != null)
                {
                    p = ProfileFor(cs, out bool needsCheck);
                    if (p != null && !Any(list, p)) p = null;
                    if (needsCheck)
                    {
                        if (Cfg.CookAtOther.Value && Any(list, Food)) p = Food;
                        else if (Cfg.GearAtOther.Value && Any(list, Gear)) p = Gear;
                    }
                }
            }
            if (p != null && !p.GroupsOn() && !p.SortOn()) p = null;

            foreach (var other in new[] { Food, Gear })
                if (other != p || !p.SortOn())
                    if (other.Bar != null) other.Bar.SetActive(false);
            if (p == null) return;

            bool groups = p.GroupsOn(), sortButtons = p.SortOn();
            if (sortButtons) { try { UpdateSortBar(gui, p); } catch { } }
            bool sort = sortButtons && p.SortMode > 0;
            if (!groups && !sort) return;

            int n = p.GroupNames.Length;
            var buckets = new List<object>[n];
            for (int i = 0; i < n; i++) buckets[i] = new List<object>();
            foreach (var it in list)
            {
                int g = p.Classify(it);
                buckets[groups ? (g < 0 ? n - 1 : g) : 0].Add(it);
            }

            if (sort)
            {
                Comparison<object> cmp = (a, b) =>
                {
                    int c = p.Stat(b).CompareTo(p.Stat(a));
                    return c != 0 ? c : string.Compare(RecipeName(RecipeOf(a)), RecipeName(RecipeOf(b)), StringComparison.CurrentCulture);
                };
                foreach (var b in buckets) b.Sort(cmp);
            }

            float space = gui.m_recipeListSpace;
            int row = 0;
            list.Clear();
            for (int g = 0; g < n; g++)
            {
                var bucket = buckets[g];
                if (bucket.Count == 0) continue;
                bool open = !groups || p.Open[g];
                if (groups)
                {
                    int gi = g;
                    Headers.Add(MakeHeader(gui, (open ? "▼ " : "▶ ") + p.GroupNames[g] + " (" + bucket.Count + ")", row++, () =>
                    {
                        p.Open[gi] = !p.Open[gi];
                        Refresh(gui);
                    }));
                }
                foreach (var it in bucket)
                {
                    var el = _ep.GetValue(it, null) as GameObject;
                    if (open)
                    {
                        list.Add(it);
                        Place(el, row++, space);
                    }
                    else if (el != null) UnityEngine.Object.Destroy(el);
                }
            }

            float baseSize = BaseSizeField != null ? (float)BaseSizeField.GetValue(gui) : 0f;
            gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(baseSize, row * space));
        }

        private static string RecipeName(Recipe r)
        {
            if (r == null || r.m_item == null) return "";
            string n = r.m_item.m_itemData.m_shared.m_name;
            return Localization.instance != null ? Localization.instance.Localize(n) : n;
        }

        private static void Refresh(InventoryGui gui)
        {
            try { if (UpdatePanel != null && gui != null) UpdatePanel.Invoke(gui, new object[] { false }); }
            catch { }
        }

        private static void Place(GameObject element, int row, float space)
        {
            if (element == null) return;
            if (element.transform is RectTransform rt) rt.anchoredPosition = new Vector2(0f, -row * space);
        }

        private static GameObject MakeHeader(InventoryGui gui, string text, int row, Action onClick)
        {
            var go = UnityEngine.Object.Instantiate(gui.m_recipeElementPrefab, gui.m_recipeListRoot);
            go.name = "PQ_RecipeGroupHeader";
            go.SetActive(true);
            Place(go, row, gui.m_recipeListSpace);
            var icon = go.transform.Find("icon");
            if (icon != null) { var img = icon.GetComponent<Image>(); if (img != null) img.enabled = false; }
            foreach (var n in new[] { "Durability", "QualityLevel", "selected" })
            {
                var t = go.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }
            var name = go.transform.Find("name");
            if (name != null)
            {
                SetText(name.gameObject, text);
                var gr = name.GetComponent<Graphic>();
                if (gr != null) gr.color = new Color(1f, 0.82f, 0.45f);
            }
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onClick());
            }
            return go;
        }

        private static void SetText(GameObject go, string text)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                var pr = c != null ? c.GetType().GetProperty("text", typeof(string)) : null;
                if (pr != null && pr.CanWrite) { pr.SetValue(c, text, null); return; }
            }
        }

        // =====================================================================
        // Sortier-Knöpfe
        // =====================================================================

        private static void UpdateSortBar(InventoryGui gui, Profile p)
        {
            RectTransform crafting = gui.m_crafting;
            if (crafting == null || gui.m_tabCraft == null) return;

            if (p.Bar == null)
            {
                p.Bar = new GameObject("PQ_SortBar_" + p.Name, typeof(RectTransform));
                p.Bar.transform.SetParent(crafting, false);
                p.Images = new Image[3];
                p.BarLabels = new GameObject[3];
                for (int i = 0; i < 3; i++)
                {
                    int mode = i + 1;
                    var b = new GameObject("PQ_Sort" + mode, typeof(RectTransform), typeof(Image), typeof(Button));
                    b.transform.SetParent(p.Bar.transform, false);
                    var img = b.GetComponent<Image>();
                    img.sprite = ItemIcon(p.Icons[i]);
                    if (img.sprite == null) img.color = p.Fallback[i];
                    img.preserveAspect = true;
                    var btn = b.GetComponent<Button>();
                    btn.targetGraphic = img;
                    btn.onClick.AddListener(() =>
                    {
                        p.SortMode = p.SortMode != mode ? mode : 0;
                        Refresh(gui);
                    });
                    p.Images[i] = img;
                    try
                    {
                        p.BarLabels[i] = MakeLabel(gui, b.transform, p.Labels[i]);
                        if (p.BarLabels[i] != null)
                        {
                            p.BarLabels[i].SetActive(false);
                            b.AddComponent<SortHover>().Label = p.BarLabels[i];
                        }
                    }
                    catch { }
                }
            }

            p.Bar.SetActive(true);
            p.Bar.transform.SetAsLastSibling();
            var tab = gui.m_tabCraft.transform as RectTransform;
            float size = tab != null ? Mathf.Clamp(tab.rect.height, 24f, 48f) : 32f;
            float y = tab != null ? crafting.InverseTransformPoint(tab.position).y - crafting.rect.yMax : -60f;
            var bar = (RectTransform)p.Bar.transform;
            bar.anchorMin = bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            float step = size + 4f;
            bar.sizeDelta = new Vector2(2f * step + size, size);
            bar.anchoredPosition = new Vector2(-22f, y);
            for (int i = 0; i < 3; i++)
            {
                var rt = (RectTransform)p.Images[i].transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(i * step, 0f);
                bool on = p.SortMode == i + 1;
                if (p.Images[i].sprite != null)
                    p.Images[i].color = on ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                if (p.BarLabels[i] != null)
                {
                    if (p.BarLabels[i].transform is RectTransform lt)
                    {
                        lt.anchorMin = lt.anchorMax = new Vector2(0.5f, 1f);
                        lt.pivot = new Vector2(0.5f, 0f);
                        lt.sizeDelta = new Vector2(120f, 16f);
                        lt.anchoredPosition = new Vector2(0f, 1f);
                    }
                    var gr = p.BarLabels[i].GetComponent<Graphic>();
                    if (gr != null) gr.color = on ? new Color(1f, 0.82f, 0.45f) : new Color(0.85f, 0.78f, 0.65f, 0.8f);
                }
            }
        }

        private static GameObject MakeLabel(InventoryGui gui, Transform parent, string text)
        {
            var src = gui.m_recipeElementPrefab != null ? gui.m_recipeElementPrefab.transform.Find("name") : null;
            if (src == null) return null;
            var go = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
            go.name = "PQ_SortLabel";
            go.SetActive(true);
            foreach (Transform child in go.transform) UnityEngine.Object.Destroy(child.gameObject);
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var t = c.GetType();
                var pr = t.GetProperty("text", typeof(string));
                if (pr == null || !pr.CanWrite) continue;
                pr.SetValue(c, text, null);
                var fs = t.GetProperty("fontSize");
                if (fs != null && fs.CanWrite)
                {
                    if (fs.PropertyType == typeof(float)) fs.SetValue(c, 13f, null);
                    else if (fs.PropertyType == typeof(int)) fs.SetValue(c, 13, null);
                }
                foreach (var n in new[] { "enableAutoSizing", "resizeTextForBestFit", "enableWordWrapping" })
                {
                    var bp = t.GetProperty(n, typeof(bool));
                    if (bp != null && bp.CanWrite) bp.SetValue(c, false, null);
                }
                var al = t.GetProperty("alignment");
                if (al != null && al.CanWrite && al.PropertyType.IsEnum)
                    foreach (var v in new[] { "Center", "MiddleCenter", "Bottom", "LowerCenter" })
                        if (Enum.IsDefined(al.PropertyType, v)) { al.SetValue(c, Enum.Parse(al.PropertyType, v), null); break; }
                break;
            }
            var g = go.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = false;
            return go;
        }

        private static Sprite ItemIcon(string prefab)
        {
            try
            {
                var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                return drop != null ? drop.m_itemData.GetIcon() : null;
            }
            catch { return null; }
        }
    }

    /// <summary>Zeigt den Namen eines Sortier-Knopfs, solange die Maus darauf ist.</summary>
    internal class SortHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject Label;
        public void OnPointerEnter(PointerEventData e) { if (Label != null) Label.SetActive(true); }
        public void OnPointerExit(PointerEventData e) { if (Label != null) Label.SetActive(false); }
        private void OnDisable() { if (Label != null) Label.SetActive(false); }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipeList")]
    internal static class InventoryGui_UpdateRecipeList_CookingSort
    {
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(InventoryGui __instance)
        {
            try { CookingSort.Apply(__instance); }
            catch (Exception e) { Plugin.LogWarn("Rezepte sortieren: " + e.Message); }
        }
    }
}
