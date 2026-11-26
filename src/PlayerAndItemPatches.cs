using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    [HarmonyPatch(typeof(Player), "Awake")]
    internal static class Player_Awake
    {
        private static void Postfix(Player __instance) => Apply(__instance);

        internal static void Apply(Player __instance)
        {
            if (Cfg.PlayerEnabled.Value)
            {
                __instance.m_maxCarryWeight = Cfg.CarryWeight.Value;
                __instance.m_maxPlaceDistance = Cfg.BuildDistance.Value;
                __instance.m_maxInteractDistance = Cfg.InteractDistance.Value;
                __instance.m_autoPickupRange = Cfg.AutoPickupRange.Value;
            }

            // Damit der Hammer Karren und Schiffe "treffen" kann
            if (Cfg.DeconstructVehicles.Value)
            {
                var mask = Util.GetField(__instance, "m_removeRayMask");
                if (mask is int m) Util.SetField(__instance, "m_removeRayMask", m | LayerMask.GetMask("vehicle"));
            }
        }
    }

    [HarmonyPatch(typeof(Skills), "RaiseSkill")]
    internal static class Skills_RaiseSkill
    {
        // "ref float factor": Änderungen über __args werden in dieser Harmony-Version nicht übernommen
        private static void Prefix(Skills.SkillType skillType, ref float factor)
        {
            if (!Cfg.SkillsEnabled.Value) return;
            factor *= Cfg.SkillFactor(skillType);   // eigener Faktor pro Skill
        }
    }

    /// <summary>Stapelgroesse, Gewicht, Bewegungsstrafe, Schwimmen. Originalwerte werden gemerkt.</summary>
    internal static class ItemTweaks
    {
        private class Orig { public int Stack; public float Weight; public float Move; }
        private static readonly Dictionary<string, Orig> Originals = new Dictionary<string, Orig>();

        internal static void Apply(ObjectDB db)
        {
            if (db == null || db.m_items == null) return;

            var noFloat = new HashSet<string>(
                (Cfg.NoFloatItems.Value ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
            int floatAdded = 0;

            foreach (var go in db.m_items)
            {
                if (!go) continue;
                var drop = go.GetComponent<ItemDrop>();
                if (!drop || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                var s = drop.m_itemData.m_shared;

                if (!Originals.TryGetValue(go.name, out var o))
                {
                    o = new Orig { Stack = s.m_maxStackSize, Weight = s.m_weight, Move = s.m_movementModifier };
                    Originals[go.name] = o;
                }

                // Stapel & Gewicht
                if (Cfg.ItemsEnabled.Value)
                {
                    s.m_maxStackSize = o.Stack > 1 ? Mathf.Max(1, Mathf.RoundToInt(o.Stack * Cfg.StackMultiplier.Value)) : o.Stack;
                    s.m_weight = o.Weight * Cfg.WeightMultiplier.Value;
                }
                else
                {
                    s.m_maxStackSize = o.Stack;
                    s.m_weight = o.Weight;
                }

                // Bewegungsstrafe
                s.m_movementModifier = (Cfg.NoMovementPenalty.Value && o.Move < 0f) ? 0f : o.Move;

                // Schwimmen
                if (Cfg.FloatingItems.Value && !noFloat.Contains(go.name)
                    && go.GetComponent<Floating>() == null && go.GetComponent<Rigidbody>() != null)
                {
                    try
                    {
                        var fl = go.AddComponent<Floating>();
                        Util.SetField(fl, "m_waterLevelOffset", 0.5f);
                        floatAdded++;
                    }
                    catch { }
                }
            }
            if (floatAdded > 0) Plugin.LogInfo("Schwimmfähige Items hinzugefügt: " + floatAdded);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class ObjectDB_Awake
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ObjectDB __instance) => ItemTweaks.Apply(__instance);
    }

    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    internal static class ObjectDB_CopyOtherDB
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ObjectDB __instance) => ItemTweaks.Apply(__instance);
    }
}
