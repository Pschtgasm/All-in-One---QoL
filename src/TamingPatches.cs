using HarmonyLib;

namespace Pscht_QoL
{
    [HarmonyPatch(typeof(Tameable), "Awake")]
    internal static class Tameable_Awake
    {
        private static void Postfix(Tameable __instance)
        {
            if (!Cfg.TamingEnabled.Value) return;
            __instance.m_tamingTime = Cfg.TamingTime.Value;
        }
    }

    [HarmonyPatch(typeof(Procreation), "Awake")]
    internal static class Procreation_Awake
    {
        private static void Postfix(Procreation __instance)
        {
            if (!Cfg.TamingEnabled.Value) return;
            __instance.m_requiredLovePoints = Cfg.LovePoints.Value;
            __instance.m_pregnancyDuration = Cfg.PregnancyDuration.Value;
            __instance.m_maxCreatures = Cfg.MaxCreatures.Value;
        }
    }

    [HarmonyPatch(typeof(Growup), "Start")]
    internal static class Growup_Start
    {
        private static void Postfix(Growup __instance)
        {
            if (!Cfg.TamingEnabled.Value) return;
            __instance.m_growTime = Cfg.GrowTime.Value;
        }
    }
}
