using System;
using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// 22 - Erkunden: eigener Skill „Erkunden“ (über Jotunn). Neu aufgedeckte Kartenstücke geben XP,
    /// der Aufdeck-Radius wächst mit dem Skill von „Radius bei Skill 0“ bis „Radius bei Skill 100“.
    /// </summary>
    internal static class Exploration
    {
        internal static Skills.SkillType Skill = Skills.SkillType.None;
        internal static bool Counting;
        internal static int NewPixels;
        private static bool _changed;
        private const float VanillaRadius = 100f;
        private const float XpPerPixel = 0.03f;

        internal static void Init()
        {
            try { Register(); }
            catch (Exception e) { Plugin.LogWarn("Skill Erkunden nicht angelegt (Jotunn fehlt?): " + e.Message); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register()
        {
            // Skill immer anlegen (auch wenn aus), damit der erreichte Wert nicht verloren geht
            Skill = Jotunn.Managers.SkillManager.Instance.AddSkill("pscht.qol.erkunden", L.T("Erkunden", "Exploring"),
                L.T("Deckt die Karte weiter um dich herum auf. Steigt, wenn du neues Land erkundest.", "Uncovers the map farther around you. Rises when you explore new land."), 1f, MakeIcon());
        }

        internal static void Before(Minimap map, Player player)
        {
            if (map == null || player == null || player != Player.m_localPlayer) return;
            if (!Cfg.ExploreEnabled.Value || Skill == Skills.SkillType.None)
            {
                if (_changed) { map.m_exploreRadius = VanillaRadius; _changed = false; }
                return;
            }
            float f = Mathf.Clamp01(player.GetSkillFactor(Skill));
            map.m_exploreRadius = Mathf.Lerp(Cfg.ExploreRadiusMin.Value, Cfg.ExploreRadiusMax.Value, f);
            _changed = true;
            Counting = true;
            NewPixels = 0;
        }

        internal static void After(Player player)
        {
            if (!Counting) return;
            Counting = false;
            if (NewPixels > 0 && player != null)
                player.RaiseSkill(Skill, NewPixels * XpPerPixel * Cfg.ExploreXp.Value);
            NewPixels = 0;
        }

        /// <summary>Kleines Kompass-Symbol für die Skill-Liste.</summary>
        private static Sprite MakeIcon()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            var ring = new Color(0.85f, 0.72f, 0.45f, 1f);
            var red = new Color(0.85f, 0.2f, 0.15f, 1f);
            var white = new Color(0.95f, 0.92f, 0.85f, 1f);
            float c = (S - 1) / 2f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color col = clear;
                    if (d <= 30f && d >= 25f) col = ring;
                    else if (d < 25f)
                    {
                        // Nadel: Raute, oben rot, unten weiß
                        float w = 7f * (1f - Mathf.Abs(dy) / 22f);
                        if (Mathf.Abs(dy) <= 22f && Mathf.Abs(dx) <= w) col = dy > 0 ? red : white;
                    }
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
    }

    [HarmonyPatch(typeof(Minimap), "UpdateExplore")]
    internal static class Minimap_UpdateExplore_Skill
    {
        private static void Prefix(Minimap __instance, object[] __args)
        {
            try { Exploration.Before(__instance, __args.Length > 1 ? __args[1] as Player : null); }
            catch (Exception e) { Plugin.LogWarn("Erkunden: " + e.Message); }
        }

        private static void Finalizer(object[] __args)
        {
            try { Exploration.After(__args.Length > 1 ? __args[1] as Player : null); }
            catch (Exception e) { Exploration.Counting = false; Plugin.LogWarn("Erkunden: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Minimap), "Explore", new[] { typeof(int), typeof(int) })]
    internal static class Minimap_ExplorePixel_Skill
    {
        private static void Postfix(bool __result)
        {
            if (__result && Exploration.Counting) Exploration.NewPixels++;
        }
    }

    // ===================== Haltbarkeit farbig =====================

    internal static class DurabilityColor
    {
        private static readonly Color Green = new Color(0.30f, 0.85f, 0.25f, 1f);
        private static readonly Color Yellow = new Color(1.00f, 0.85f, 0.15f, 1f);
        private static readonly Color Red = new Color(0.95f, 0.20f, 0.15f, 1f);

        internal static Color For(float pct) =>
            pct >= 0.5f ? Color.Lerp(Yellow, Green, (pct - 0.5f) * 2f) : Color.Lerp(Red, Yellow, pct * 2f);

        /// <summary>Kaputte Items blinken weiter rot wie im Spiel – nur heile färben.</summary>
        internal static void Apply(GuiBar bar, ItemDrop.ItemData it)
        {
            if (bar == null || it == null || !it.m_shared.m_useDurability) return;
            float max = it.GetMaxDurability();
            if (it.m_durability <= 0f || it.m_durability >= max) return;
            bar.SetColor(For(it.GetDurabilityPercentage()));
        }

        internal static void Grid(InventoryGrid grid)
        {
            if (!Cfg.DurabilityColors.Value || grid == null) return;
            var inv = grid.GetInventory();
            var els = Util.GetField(grid, "m_elements") as IList;
            if (inv == null || els == null) return;
            foreach (var o in els)
            {
                var el = o as InventoryElement;
                if (el == null || el.m_durability == null || !Util.ElementPos(el, out var pos)) continue;
                Apply(el.m_durability, inv.GetItemAt(pos.x, pos.y));
            }
        }

        private static AccessTools.FieldRef<object, GuiBar> _barRef;

        internal static void Hotbar(HotkeyBar bar)
        {
            if (!Cfg.DurabilityColors.Value || bar == null) return;
            var els = Util.GetField(bar, "m_elements") as IList;
            var items = Util.GetField(bar, "m_items") as IList;
            if (els == null || items == null || els.Count == 0) return;
            if (_barRef == null) _barRef = AccessTools.FieldRefAccess<GuiBar>(els[0].GetType(), "m_durability");
            foreach (var o in items)
            {
                var it = o as ItemDrop.ItemData;
                if (it == null || it.m_gridPos.x < 0 || it.m_gridPos.x >= els.Count) continue;
                Apply(_barRef(els[it.m_gridPos.x]), it);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class InventoryGrid_UpdateGui_Durability
    {
        private static void Postfix(InventoryGrid __instance)
        {
            try { DurabilityColor.Grid(__instance); }
            catch (Exception e) { Plugin.LogWarn("Haltbarkeit: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(HotkeyBar), "UpdateIcons")]
    internal static class HotkeyBar_UpdateIcons_Durability
    {
        private static void Postfix(HotkeyBar __instance)
        {
            try { DurabilityColor.Hotbar(__instance); }
            catch (Exception e) { Plugin.LogWarn("Haltbarkeit: " + e.Message); }
        }
    }
}
