using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Zerstörte oder abgebaute Schiffe/Karren hinterlassen ein Wrack (aus der "Zerstört"-Effektliste),
    /// das sehr lange liegen bleibt. Hier wird es nach X Sekunden entfernt.
    /// </summary>
    [HarmonyPatch(typeof(WearNTear), "Destroy")]
    internal static class WearNTear_Destroy_Wreck
    {
        private static void Prefix(WearNTear __instance, out (Vector3 pos, HashSet<string> names)? __state)
        {
            __state = null;
            float sec = Cfg.WreckSeconds.Value;
            if (sec <= 0f || __instance == null) return;
            if (__instance.GetComponent<Ship>() == null && __instance.GetComponent<Vagon>() == null) return;

            var names = new HashSet<string>();
            if (__instance.m_destroyedEffect?.m_effectPrefabs != null)
                foreach (var e in __instance.m_destroyedEffect.m_effectPrefabs)
                    if (e != null && e.m_prefab != null) names.Add(e.m_prefab.name);
            __state = (__instance.transform.position, names);
        }

        private static void Postfix((Vector3 pos, HashSet<string> names)? __state)
        {
            if (__state == null || Plugin.Instance == null) return;
            var (pos, names) = __state.Value;
            // Ein Frame warten, bis alles erzeugt ist
            Plugin.Instance.StartCoroutine(Cleanup(pos, names, Cfg.WreckSeconds.Value));
        }

        private static IEnumerator Cleanup(Vector3 pos, HashSet<string> names, float seconds)
        {
            yield return null;
            var found = new List<GameObject>();
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go == null || Vector3.Distance(go.transform.position, pos) > 30f) continue;
                string n = go.name.Replace("(Clone)", "").Trim();
                if (names.Contains(n) || n.ToLowerInvariant().Contains("wreck") || n.ToLowerInvariant().Contains("destruction"))
                    found.Add(go);
            }
            Plugin.LogInfo("Wrack: " + found.Count + " Teile werden nach " + seconds + " s entfernt" +
                           (found.Count > 0 ? " (" + string.Join(", ", found.Select(g => g.name)) + ")"
                                            : " – Effekte: " + string.Join(", ", names)));
            if (found.Count == 0) yield break;

            yield return new WaitForSeconds(seconds);
            foreach (var go in found)
            {
                if (go == null) continue;
                var nv = go.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid())
                {
                    if (!nv.IsOwner()) nv.ClaimOwnership();
                    ZNetScene.instance?.Destroy(go);
                }
                else Object.Destroy(go);
            }
        }
    }
}
