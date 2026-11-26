using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    // ===================== Prefab-Aenderungen beim Laden der Welt =====================

    /// <summary>Karren/Schiffe abbaubar, Werkbank-Erweiterungen kompakter.</summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetScene_Awake_Comfort
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (__instance.m_prefabs == null) return;
            int vehicles = 0, extensions = 0;

            foreach (var go in __instance.m_prefabs)
            {
                if (!go) continue;
                var piece = go.GetComponent<Piece>();

                if (Cfg.DeconstructVehicles.Value && piece &&
                    (go.GetComponent<Ship>() != null || go.GetComponent<Vagon>() != null))
                {
                    piece.m_canBeRemoved = true;
                    vehicles++;
                }

                var ext = go.GetComponent<StationExtension>();
                if (Cfg.ExtensionsEnabled.Value && ext)
                {
                    Util.SetField(ext, "m_maxStationDistance", Cfg.ExtensionDistance.Value);
                    if (piece) Util.SetField(piece, "m_spaceRequirement", Cfg.ExtensionSpacing.Value);
                    extensions++;
                }
            }
            Plugin.LogInfo("Fahrzeuge abbaubar: " + vehicles + ", Erweiterungen angepasst: " + extensions);
        }
    }

    // ===================== Nebel =====================

    [HarmonyPatch(typeof(ParticleMist), "Awake")]
    internal static class ParticleMist_Awake
    {
        private static void Postfix(ParticleMist __instance)
        {
            if (!Cfg.NoMist.Value) return;
            var ps = __instance.GetComponent<ParticleSystem>();
            if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            __instance.enabled = false;
        }
    }

    // ===================== Rauch =====================

    [HarmonyPatch]
    internal static class SmokeSpawner_NoSmoke
    {
        private static MethodBase TargetMethod()
        {
            var methods = AccessTools.GetDeclaredMethods(typeof(SmokeSpawner));
            return methods.FirstOrDefault(m => m.Name == "Spawn")
                ?? methods.First(m => m.Name == "Update");
        }

        private static bool Prefix() => !Cfg.NoSmoke.Value;
    }

    // ===================== Ballisten =====================

    [HarmonyPatch(typeof(Turret), "Awake")]
    internal static class Turret_Awake
    {
        private static void Postfix(Turret __instance)
        {
            if (Cfg.BallistaIgnorePlayers.Value) Util.SetField(__instance, "m_targetPlayers", false);
            if (Cfg.BallistaIgnoreTames.Value) Util.SetField(__instance, "m_targetTamed", false);
        }
    }

    // ===================== Tueren =====================

    [HarmonyPatch(typeof(Door), "Interact")]
    internal static class Door_Interact
    {
        private static void Postfix(Door __instance)
        {
            if (!Cfg.AutoCloseDoors.Value || __instance.m_keyItem != null || Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(CloseLater(__instance));
        }

        private static IEnumerator CloseLater(Door door)
        {
            yield return new WaitForSeconds(Cfg.AutoCloseDelay.Value);

            while (door)
            {
                var nv = door.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) yield break;
                if (nv.GetZDO().GetInt("state", 0) == 0) yield break; // schon zu

                bool someoneNear = false;
                foreach (var p in Player.GetAllPlayers())
                {
                    if (p && Vector3.Distance(p.transform.position, door.transform.position) < Cfg.AutoCloseDistance.Value)
                    {
                        someoneNear = true;
                        break;
                    }
                }

                if (!someoneNear)
                {
                    if (!nv.IsOwner()) nv.ClaimOwnership();
                    nv.GetZDO().Set("state", 0);
                    yield break;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    // ===================== Timer beim Drueberschauen =====================

    [HarmonyPatch(typeof(Plant), "GetHoverText")]
    internal static class Plant_HoverTimer
    {
        private static void Postfix(Plant __instance, ref string __result)
        {
            if (!Cfg.HoverTimers.Value || !Cfg.TimerPlants.Value) return;
            var since = Util.Call(__instance, "TimeSincePlanted");
            var grow = Util.Call(__instance, "GetGrowTime");
            if (since is double s && grow is float g)
            {
                double left = g - s;
                if (left > 0) __result += "\n<color=#9fe39f>" + L.T("Fertig in ", "Ready in ") + Util.FormatTime(left) + "</color>";
            }
        }
    }

    [HarmonyPatch(typeof(Fermenter), "GetHoverText")]
    internal static class Fermenter_HoverTimer
    {
        private static void Postfix(Fermenter __instance, ref string __result)
        {
            if (!Cfg.HoverTimers.Value || !Cfg.TimerFermenter.Value) return;
            var t = Util.Call(__instance, "GetFermentationTime");
            if (t is double d && d > 0)
            {
                double left = __instance.m_fermentationDuration - d;
                if (left > 0) __result += "\n<color=#9fe39f>" + L.T("Fertig in ", "Ready in ") + Util.FormatTime(left) + "</color>";
            }
        }
    }

    /// <summary>Zähmen, Liebe/Traechtigkeit und Erwachsenwerden beim Drueberschauen.</summary>
    [HarmonyPatch(typeof(Tameable), "GetHoverText")]
    internal static class Tameable_HoverTimer
    {
        private const string Col = "<color=#9fe39f>";

        private static void Postfix(Tameable __instance, ref string __result)
        {
            if (!Cfg.HoverTimers.Value || !Cfg.TimerTames.Value) return;
            var nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            var zdo = nv.GetZDO();
            var character = __instance.GetComponent<Character>();
            string add = "";

            if (character != null && !character.IsTamed())
            {
                float left = zdo.GetFloat("TameTimeLeft", __instance.m_tamingTime);
                bool hungry = Util.Call(__instance, "IsHungry") is bool b && b;
                if (left >= __instance.m_tamingTime && hungry)
                    add += "\n" + Col + L.T("Füttern, um das Zähmen zu starten", "Feed it to start taming") + "</color>";
                else
                    add += "\n" + Col + L.T("Gezähmt in ", "Tamed in ") + Util.FormatTime(left) + "</color>" +
                           (hungry ? " <color=orange>" + L.T("(hungrig - füttern!)", "(hungry - feed it!)") + "</color>" : "");
            }
            else
            {
                var proc = __instance.GetComponent<Procreation>();
                if (proc != null && ZNet.instance != null)
                {
                    long pregnant = zdo.GetLong("pregnant", 0L);
                    if (pregnant != 0L)
                    {
                        double since = (ZNet.instance.GetTime() - new System.DateTime(pregnant)).TotalSeconds;
                        add += "\n" + Col + L.T("Nachwuchs in ", "Offspring in ") + Util.FormatTime(proc.m_pregnancyDuration - since) + "</color>";
                    }
                    else
                    {
                        int love = zdo.GetInt("lovePoints", 0);
                        add += "\n" + Col + L.T("Liebe: ", "Love: ") + love + " / " + proc.m_requiredLovePoints + "</color>";
                    }
                }
            }

            add += GrowText(__instance.gameObject);
            add += StarText(character);

            if (add.Length > 0) __result = (__result ?? "") + add;
        }

        /// <summary>"Sterne: N" für zahme Tiere – zeigt die echte Zahl, auch über 2 Sterne.</summary>
        internal static string StarText(Character c)
        {
            if (c == null || !c.IsTamed()) return "";
            int stars = c.GetLevel() - 1;
            return stars > 0 ? "\n<color=#ffd84a>" + L.T("Sterne: ", "Stars: ") + stars + "</color>" : "";
        }

        /// <summary>"Erwachsen in …" für Jungtiere (leer, wenn kein Jungtier).</summary>
        internal static string GrowText(GameObject go)
        {
            var grow = go.GetComponent<Growup>();
            var ai = go.GetComponent<BaseAI>();
            if (grow == null || ai == null) return "";
            double left = grow.m_growTime - ai.GetTimeSinceSpawned().TotalSeconds;
            return "\n" + Col + L.T("Erwachsen in ", "Grown up in ") + Util.FormatTime(System.Math.Max(0.0, left)) + "</color>";
        }
    }

    /// <summary>
    /// Jungtiere (z.B. Ferkel) haben oft keine Tameable-Komponente – dann liefert das Spiel
    /// gar keinen Hover-Text. Hier kommt der Erwachsen-Timer trotzdem dazu.
    /// </summary>
    [HarmonyPatch(typeof(Character), "GetHoverText")]
    internal static class Character_HoverGrowTimer
    {
        private static void Postfix(Character __instance, ref string __result)
        {
            if (!Cfg.HoverTimers.Value || !Cfg.TimerTames.Value) return;
            if (__instance.GetComponent<Tameable>() != null) return;   // macht Tameable_HoverTimer
            string add = Tameable_HoverTimer.GrowText(__instance.gameObject) + Tameable_HoverTimer.StarText(__instance);
            if (add.Length == 0) return;
            if (string.IsNullOrEmpty(__result))
                __result = Localization.instance.Localize(__instance.GetHoverName());
            __result += add;
        }
    }

    [HarmonyPatch(typeof(Beehive), "GetHoverText")]
    internal static class Beehive_HoverTimer
    {
        private static void Postfix(Beehive __instance, ref string __result)
        {
            if (!Cfg.HoverTimers.Value || !Cfg.TimerBeehive.Value) return;
            var nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            var zdo = nv.GetZDO();
            int level = zdo.GetInt("level", 0);
            if (level >= __instance.m_maxHoney)
            {
                __result += "\n<color=#9fe39f>Voll</color>";
                return;
            }
            float product = zdo.GetFloat("product", 0f);
            __result += "\n<color=#9fe39f>" + L.T("Nächster Honig in ", "Next honey in ") + Util.FormatTime(__instance.m_secPerUnit - product) + "</color>";
        }
    }

    // ===================== Massenernte =====================

    [HarmonyPatch(typeof(Pickable), "Interact")]
    internal static class Pickable_BulkHarvest
    {
        private static bool _busy;

        private static void Postfix(Pickable __instance, object[] __args)
        {
            if (_busy || !Cfg.HarvestEnabled.Value) return;
            var character = __args.Length > 0 ? __args[0] as Humanoid : null;
            bool repeat = __args.Length > 1 && __args[1] is bool r && r;
            if (repeat || character == null || character != Player.m_localPlayer) return;

            _busy = true;
            try
            {
                string name = Util.PrefabName(__instance.gameObject);
                int count = 0;
                var done = new System.Collections.Generic.HashSet<Pickable> { __instance };

                foreach (var col in Physics.OverlapSphere(__instance.transform.position, Cfg.HarvestRadius.Value))
                {
                    if (count >= Cfg.HarvestMax.Value) break;
                    var other = col ? col.GetComponentInParent<Pickable>() : null;
                    if (other == null || !done.Add(other)) continue;
                    if (Cfg.HarvestSameType.Value && Util.PrefabName(other.gameObject) != name) continue;
                    if (other.Interact(character, false, false)) count++;
                }
            }
            finally { _busy = false; }
        }
    }

    [HarmonyPatch(typeof(Beehive), "Interact")]
    internal static class Beehive_BulkHarvest
    {
        private static bool _busy;

        private static void Postfix(Beehive __instance, object[] __args)
        {
            if (_busy || !Cfg.HarvestEnabled.Value || !Cfg.HarvestBeehives.Value) return;
            var character = __args.Length > 0 ? __args[0] as Humanoid : null;
            bool repeat = __args.Length > 1 && __args[1] is bool r && r;
            if (repeat || character == null || character != Player.m_localPlayer) return;

            _busy = true;
            try
            {
                // Alle Bienenstöcke im Umkreis direkt suchen (nicht über Collider – die
                // liegen je nach Version auf Ebenen, die OverlapSphere nicht zuverlässig trifft).
                int count = 0;
                Vector3 center = __instance.transform.position;
                float radius = Cfg.HarvestRadius.Value;
                foreach (var other in Object.FindObjectsByType<Beehive>(FindObjectsSortMode.None))
                {
                    if (count >= Cfg.HarvestMax.Value) break;
                    if (other == null || other == __instance) continue;
                    if (Vector3.Distance(other.transform.position, center) > radius) continue;
                    var nv = other.GetComponent<ZNetView>();
                    if (nv == null || !nv.IsValid() || nv.GetZDO().GetInt(ZDOVars.s_level) <= 0) continue;
                    other.Interact(character, false, false);
                    count++;
                }
                if (count > 0)
                    character.Message(MessageHud.MessageType.TopLeft, count + L.T(" weitere Bienenstöcke geleert", " more beehives emptied"));
            }
            finally { _busy = false; }
        }
    }
}
