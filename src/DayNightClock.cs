using System;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    // ===================== Tag- und Nachtlaenge =====================

    internal static class DayNight
    {
        /// <summary>Anteil des Tages am ganzen Zyklus (Vanilla ca. 0.7).</summary>
        internal static float DayShare =>
            Mathf.Clamp(Cfg.DayMinutes.Value / (Cfg.DayMinutes.Value + Cfg.NightMinutes.Value), 0.05f, 0.95f);

        internal static void Apply(EnvMan env)
        {
            if (env == null || !Cfg.DayNightEnabled.Value) return;
            long total = (long)((Cfg.DayMinutes.Value + Cfg.NightMinutes.Value) * 60f);
            Util.SetField(env, "m_dayLengthSec", Math.Max(60L, total));
        }
    }

    [HarmonyPatch(typeof(EnvMan), "Awake")]
    internal static class EnvMan_Awake_DayLength
    {
        private static void Postfix(EnvMan __instance) => DayNight.Apply(__instance);
    }

    /// <summary>
    /// Verteilt den Zyklus auf Tag und Nacht. Valheim rechnet intern mit einem "Tagesanteil":
    /// 0.25 = Morgen, 0.5 = Mittag, 0.75 = Abend. Hier wird festgelegt, wie viel echte Zeit jeweils vergeht.
    /// </summary>
    [HarmonyPatch(typeof(EnvMan), "RescaleDayFraction")]
    internal static class EnvMan_RescaleDayFraction
    {
        private static bool Prefix(object[] __args, ref float __result)
        {
            if (!Cfg.DayNightEnabled.Value || __args.Length == 0 || !(__args[0] is float fraction)) return true;
            float d = DayNight.DayShare;
            float halfNight = (1f - d) / 2f;

            if (fraction >= halfNight && fraction <= 1f - halfNight)
                __result = 0.25f + (fraction - halfNight) / d * 0.5f;          // Tag
            else if (fraction < 0.5f)
                __result = fraction / halfNight * 0.25f;                       // Nacht bis Morgen
            else
                __result = 0.75f + (fraction - (1f - halfNight)) / halfNight * 0.25f; // Abend bis Mitternacht
            return false;
        }
    }

    // ===================== Uhr =====================

    internal static class Clock
    {
        private static GUIStyle _style, _shadow;
        private static int _styleSize;

        internal static void HandleInput()
        {
            if (Player.m_localPlayer == null || Util.InputBlocked()) return;
            if (Cfg.ClockToggleKey.Value.IsDown()) Cfg.ClockEnabled.Value = !Cfg.ClockEnabled.Value;
        }

        internal static void OnGUI()
        {
            if (!Cfg.ClockEnabled.Value || Player.m_localPlayer == null || EnvMan.instance == null) return;
            if (Hud.instance != null && Util.Call(Hud.instance, "IsVisible") is bool vis && !vis) return;
            if (Cfg.ClockHideInInventory.Value && (InventoryGui.IsVisible() || Minimap.IsOpen())) return;

            string text = BuildText();
            if (text.Length == 0) return;

            if (_style == null || _styleSize != Cfg.ClockFontSize.Value)
            {
                _styleSize = Cfg.ClockFontSize.Value;
                _style = new GUIStyle(GUI.skin.label) { fontSize = _styleSize, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.UpperCenter };
                _style.normal.textColor = Color.white;
                _shadow = new GUIStyle(_style);
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }

            var content = new GUIContent(text);
            Vector2 size = _style.CalcSize(content);
            float m = 12f;
            float x, y;
            string pos = Cfg.ClockPosition.Value ?? "";
            if (pos.EndsWith("Links")) x = m;
            else if (pos.EndsWith("Rechts")) x = Screen.width - size.x - m;
            else x = (Screen.width - size.x) / 2f;
            y = pos.StartsWith("Unten") ? Screen.height - size.y - m : m;

            GUI.Label(new Rect(x + 2, y + 2, size.x, size.y), text, _shadow);
            GUI.Label(new Rect(x, y, size.x, size.y), text, _style);
        }

        private static string BuildText()
        {
            var env = EnvMan.instance;
            string day = null, real = null, game = null;

            if (Cfg.ClockShowDay.Value)
            {
                int d = -1;
                if (Util.Call(env, "GetCurrentDay") is int d1) d = d1;
                else if (Util.Call(env, "GetDay") is int d2) d = d2;
                if (d >= 0) day = L.T("Tag ", "Day ") + d;
            }

            if (Cfg.ClockShowRealTime.Value)
            {
                var now = DateTime.Now;
                real = "<color=#ffffff>Real Life " + (Cfg.ClockRealTime24h.Value ? now.ToString("HH:mm") : now.ToString("h:mm tt")) + "</color>";
            }

            if (Cfg.ClockShowGameTime.Value)
            {
                float fraction;
                if (Util.GetField(env, "m_smoothDayFraction") is float f) fraction = f;
                else if (Util.Call(env, "GetDayFraction") is float g) fraction = g;
                else fraction = -1f;

                if (fraction >= 0f)
                {
                    int minutes = Mathf.FloorToInt(fraction * 24f * 60f) % (24 * 60);
                    bool night = fraction < 0.25f || fraction > 0.75f;
                    game = (night ? "<color=#9db4ff>" : "<color=#ffc93c>") + "Valheim " +
                           (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00") + "</color>";
                }
            }

            // Reihenfolge: Tag, echte Uhrzeit, Valheim-Zeit
            var parts = new System.Collections.Generic.List<string>();
            if (day != null) parts.Add(day);
            if (real != null) parts.Add(real);
            if (game != null) parts.Add(game);
            return string.Join("\n", parts.ToArray());   // immer untereinander
        }
    }
}
