using System;
using HarmonyLib;

namespace Pscht_QoL
{
    /// <summary>
    /// Aendert die Pflanzen-Prefabs direkt, sobald die Welt geladen wird.
    /// Dadurch gilt die Aenderung für die Platzier-Vorschau UND für das Wachstum.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetScene_Awake_Plants
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!Cfg.PlantsEnabled.Value || __instance.m_prefabs == null) return;

            Heightmap.Biome all = Heightmap.Biome.None;
            foreach (Heightmap.Biome b in Enum.GetValues(typeof(Heightmap.Biome))) all |= b;

            int count = 0;
            foreach (var go in __instance.m_prefabs)
            {
                if (!go) continue;
                var plant = go.GetComponent<Plant>();
                if (!plant) continue;
                var piece = go.GetComponent<Piece>();

                if (Cfg.PlantAnyBiome.Value)
                {
                    plant.m_biome = all;
                    if (piece) piece.m_onlyInBiome = Heightmap.Biome.None;
                }

                if (!Cfg.CropsRequireCultivation.Value)
                {
                    plant.m_needCultivatedGround = false;
                    if (piece) piece.m_cultivatedGroundOnly = false;
                }
                count++;
            }
            Plugin.LogInfo("Pflanzen angepasst: " + count);
        }
    }

    [HarmonyPatch(typeof(Plant), "Awake")]
    internal static class Plant_Awake
    {
        private static void Postfix(Plant __instance)
        {
            if (!Cfg.PlantsEnabled.Value) return;
            float sec = Cfg.PlantGrowSeconds.Value;
            if (sec <= 0f) return;   // 0 = wie im normalen Spiel
            __instance.m_growTime = sec;
            __instance.m_growTimeMax = sec;
        }
    }
}
