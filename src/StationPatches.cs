using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    // ===================== Verarbeitung =====================

    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class Smelter_Awake
    {
        private static void Postfix(Smelter __instance)
        {
            if (!Cfg.ProcessingEnabled.Value) return;

            // Sekunden pro Stueck und Kapazitaets-Faktor je Station
            float sec, cap; bool known = true;
            switch (Util.PrefabName(__instance.gameObject))
            {
                case "smelter":             sec = Cfg.SecSmelter.Value;       cap = Cfg.CapSmelter.Value; break;
                case "blastfurnace":        sec = Cfg.SecBlastFurnace.Value;  cap = Cfg.CapBlastFurnace.Value; break;
                case "charcoal_kiln":       sec = Cfg.SecKiln.Value;          cap = Cfg.CapKiln.Value; break;
                case "piece_spinningwheel": sec = Cfg.SecSpinningWheel.Value; cap = Cfg.CapSpinningWheel.Value; break;
                case "windmill":            sec = Cfg.SecWindmill.Value;      cap = Cfg.CapWindmill.Value; break;
                case "eitrrefinery":        sec = Cfg.SecEitrRefinery.Value;  cap = Cfg.CapEitrRefinery.Value; break;
                default:
                    known = false; sec = 0f; cap = Cfg.SmelterCapacity.Value; break;
            }

            if (known)
            {
                if (sec > 0f) __instance.m_secPerProduct = sec;
            }
            else
            {
                __instance.m_secPerProduct /= Mathf.Max(0.01f, Cfg.SmelterSpeed.Value);
            }

            __instance.m_maxOre = Mathf.Max(1, Mathf.RoundToInt(__instance.m_maxOre * cap));
            if (__instance.m_maxFuel > 0)
                __instance.m_maxFuel = Mathf.Max(1, Mathf.RoundToInt(__instance.m_maxFuel * cap));

            string prefab = Util.PrefabName(__instance.gameObject);
            if (prefab == "piece_spinningwheel" && Cfg.SpinningWheelNoRoof.Value)
                __instance.m_requiresRoof = false;

            if (prefab == "eitrrefinery")
            {
                EitrDamage.Refineries.Add(__instance.transform);
                foreach (var aoe in __instance.GetComponentsInChildren<Aoe>(true)) EitrDamage.Apply(aoe);
                EitrDamage.LogOnce(__instance.gameObject);
            }
        }
    }

    /// <summary>Umgebungsschaden der Eitr-Raffinerie (Aoe-Komponenten an/bei der Raffinerie) skalieren.</summary>
    internal static class EitrDamage
    {
        internal static readonly System.Collections.Generic.List<Transform> Refineries = new System.Collections.Generic.List<Transform>();
        private static bool _logged;

        internal static void Apply(Aoe aoe)
        {
            if (aoe == null) return;
            float f = Mathf.Clamp01(Cfg.EitrDamagePercent.Value / 100f);
            if (f >= 1f) return;
            if (f <= 0f) { aoe.enabled = false; return; }
            aoe.m_damage.Modify(f);
            aoe.m_damagePerLevel.Modify(f);
        }

        internal static bool NearRefinery(Vector3 pos)
        {
            Refineries.RemoveAll(t => t == null);
            foreach (var t in Refineries)
                if (Vector3.Distance(t.position, pos) < 5f) return true;
            return false;
        }

        /// <summary>Einmal ins Log, welche Teile die Raffinerie hat (hilft, falls der Schaden woanders herkommt).</summary>
        internal static void LogOnce(GameObject go)
        {
            if (_logged) return;
            _logged = true;
            var types = new System.Collections.Generic.HashSet<string>();
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null) types.Add(c.GetType().Name);
            Plugin.LogInfo("Eitr-Raffinerie Bauteile: " + string.Join(", ", types));
        }
    }

    [HarmonyPatch(typeof(Aoe), "Awake")]
    internal static class Aoe_Awake_Eitr
    {
        private static void Postfix(Aoe __instance)
        {
            if (!Cfg.ProcessingEnabled.Value || Cfg.EitrDamagePercent.Value >= 100f) return;
            if (__instance.GetComponentInParent<Smelter>() != null) return;   // schon in Smelter_Awake erledigt
            if (EitrDamage.NearRefinery(__instance.transform.position)) EitrDamage.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(Windmill), "GetPowerOutput")]
    internal static class Windmill_GetPowerOutput
    {
        private static void Postfix(ref float __result)
        {
            if (Cfg.ProcessingEnabled.Value && Cfg.WindmillIgnoreWind.Value) __result = 1f;
        }
    }

    [HarmonyPatch(typeof(Fermenter), "Awake")]
    internal static class Fermenter_Awake
    {
        private static void Postfix(Fermenter __instance)
        {
            if (!Cfg.ProcessingEnabled.Value) return;
            __instance.m_fermentationDuration = Cfg.FermenterSeconds.Value;
        }
    }

    [HarmonyPatch(typeof(Beehive), "Awake")]
    internal static class Beehive_Awake
    {
        private static void Postfix(Beehive __instance)
        {
            if (!Cfg.ProcessingEnabled.Value) return;
            __instance.m_secPerUnit = Cfg.BeehiveSeconds.Value;
        }
    }

    // ===================== Feuer =====================

    /// <summary>Haelt den Brennstoff von Feuern immer voll (nur der Besitzer des Objekts schreibt).</summary>
    [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
    internal static class Fireplace_UpdateFireplace
    {
        /// <summary>Welche Feuerart ist das? -> passender Schalter aus "07 - Feuer".</summary>
        internal static bool NoFuelFor(Fireplace fire)
        {
            string n = Util.PrefabName(fire.gameObject).ToLowerInvariant();
            if (n.Contains("torch")) return Cfg.FireOther.Value;
            if (n.Contains("brazier")) return Cfg.FireBraziers.Value;
            if (n.Contains("bathtub") || n.Contains("hottub")) return Cfg.FireOther.Value;
            if (n.Contains("hearth")) return Cfg.FireOther.Value;
            if (n.Contains("fire_pit") || n.Contains("firepit") || n.Contains("bonfire") || n.Contains("campfire")) return Cfg.FireCampfire.Value;
            return Cfg.FireOther.Value;
        }

        private static void Prefix(Fireplace __instance)
        {
            if (!NoFuelFor(__instance)) return;
            var nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            var zdo = nv.GetZDO();
            if (zdo.GetFloat("fuel", 0f) < __instance.m_maxFuel) zdo.Set("fuel", __instance.m_maxFuel);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
    internal static class CookingStation_UpdateCooking
    {
        private static void Prefix(CookingStation __instance)
        {
            if (!Cfg.FireOther.Value || !__instance.m_useFuel) return;   // Steinofen gehört zu "Andere Feuer"
            var nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            var zdo = nv.GetZDO();
            if (zdo.GetFloat("fuel", 0f) < __instance.m_maxFuel) zdo.Set("fuel", (float)__instance.m_maxFuel);
        }
    }

    // ===================== Bauen =====================

    [HarmonyPatch(typeof(WearNTear), "HaveRoof")]
    internal static class WearNTear_HaveRoof
    {
        private static void Postfix(ref bool __result)
        {
            if (Cfg.BuildingEnabled.Value && Cfg.NoWeatherDamage.Value) __result = true;
        }
    }

    [HarmonyPatch(typeof(WearNTear), "IsUnderWater")]
    internal static class WearNTear_IsUnderWater
    {
        private static void Postfix(ref bool __result)
        {
            if (Cfg.BuildingEnabled.Value && Cfg.NoWaterDamage.Value) __result = false;
        }
    }

    /// <summary>Werkbank-Reichweite und Dach. Patcht Start (oder Awake, je nach Valheim-Version).</summary>
    [HarmonyPatch]
    internal static class CraftingStation_Init
    {
        private static MethodBase TargetMethod()
        {
            var methods = AccessTools.GetDeclaredMethods(typeof(CraftingStation));
            return methods.FirstOrDefault(m => m.Name == "Start" && m.GetParameters().Length == 0)
                ?? methods.First(m => m.Name == "Awake" && m.GetParameters().Length == 0);
        }

        private static void Postfix(CraftingStation __instance)
        {
            if (!Cfg.BuildingEnabled.Value) return;
            __instance.m_rangeBuild = Cfg.WorkbenchRange.Value;
            if (Cfg.NoRoofRequired.Value) __instance.m_craftRequireRoof = false;

            if (__instance.m_areaMarker)
            {
                var cp = __instance.m_areaMarker.GetComponent<CircleProjector>();
                if (cp) cp.m_radius = Cfg.WorkbenchRange.Value;
            }
        }
    }
}
