using System.Collections;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Lässt eine Kiste kurz farbig aufleuchten (z.B. die Ziel-Kiste der smarten Kisten),
    /// damit man sieht, wohin ein Item gewandert ist. Nutzt dieselbe Technik wie das Spiel
    /// beim Hammer-Hervorheben (MaterialMan, Farbe + Leuchten).
    /// </summary>
    internal static class ChestHighlight
    {
        internal static void Flash(Container c)
        {
            if (c == null || Plugin.Instance == null || !Cfg.SmartHighlight.Value) return;
            Plugin.Instance.StartCoroutine(Run(c.gameObject, Cfg.SmartHighlightSeconds.Value));
        }

        private static IEnumerator Run(GameObject go, float seconds)
        {
            Color col = ChestSign.ColorByName(Cfg.SmartHighlightColor.Value, new Color(1f, 0.15f, 0.1f));
            float end = Time.time + Mathf.Max(0.2f, seconds);
            while (Time.time < end && go != null)
            {
                // sanftes Pulsieren
                float t = 0.6f + 0.4f * Mathf.Sin(Time.time * 10f);
                try
                {
                    MaterialMan.instance.SetValue(go, ShaderProps._EmissionColor, col * (0.5f * t));
                    MaterialMan.instance.SetValue(go, ShaderProps._Color, Color.Lerp(Color.white, col, 0.6f * t));
                }
                catch { yield break; }
                yield return null;
            }
            if (go != null)
            {
                try
                {
                    MaterialMan.instance.ResetValue(go, ShaderProps._Color);
                    MaterialMan.instance.ResetValue(go, ShaderProps._EmissionColor);
                }
                catch { }
            }
        }
    }
}
