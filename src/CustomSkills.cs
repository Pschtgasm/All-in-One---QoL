using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// 10 - Eigene Skills: eigene Skills „Segeln“ und „Zähmen“ (über Jotunn). Jeder einzeln abschaltbar.
    /// </summary>
    internal static class CustomSkills
    {
        internal static Skills.SkillType Sail = Skills.SkillType.None;
        internal static Skills.SkillType Tame = Skills.SkillType.None;

        internal static void Init()
        {
            try { Register(); }
            catch (Exception e) { Plugin.LogWarn("Skills Segeln/Zähmen nicht angelegt (Jotunn fehlt?): " + e.Message); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register()
        {
            // Immer anlegen (auch wenn aus), damit erreichte Werte nicht verloren gehen
            var sm = Jotunn.Managers.SkillManager.Instance;
            Sail = sm.AddSkill("pscht.qol.segeln", L.T("Segeln", "Sailing"),
                L.T("Dein Schiff fährt schneller. Steigt, solange du am Steuer stehst und fährst.", "Your ship sails faster. Rises while you stand at the helm and sail."), 1f, Icon(SailPixel));
            Tame = sm.AddSkill("pscht.qol.zaehmen", L.T("Zähmen", "Taming"),
                L.T("Zahme Tiere in deiner Nähe nehmen weniger Schaden und machen mehr Schaden. Steigt beim Zähmen, Füttern und Verlieben.", "Tamed animals near you take less damage and deal more damage. Rises when taming, feeding and breeding."), 1f, Icon(PawPixel));
        }

        // ---------- Segeln ----------

        private const float SailXpPerSecond = 0.3f;
        private static float _sailAcc;

        internal static void Tick()
        {
            if (!Cfg.SailEnabled.Value || Sail == Skills.SkillType.None) return;
            var p = Player.m_localPlayer;
            var ship = p != null ? p.GetControlledShip() : null;
            if (ship == null || Mathf.Abs(ship.GetSpeed()) < 1.5f) return;
            _sailAcc += Time.deltaTime;
            if (_sailAcc < 1f) return;
            p.RaiseSkill(Sail, _sailAcc * SailXpPerSecond * Cfg.SailXp.Value);
            _sailAcc = 0f;
        }

        /// <summary>Tempo-Faktor für ein Schiff, das du gerade steuerst (sonst 1).</summary>
        internal static float SailMult(Ship ship)
        {
            if (!Cfg.SailEnabled.Value || Sail == Skills.SkillType.None || ship == null) return 1f;
            var p = Player.m_localPlayer;
            if (p == null || p.GetControlledShip() != ship) return 1f;
            return 1f + Cfg.SailBonus.Value / 100f * Mathf.Clamp01(p.GetSkillFactor(Sail));
        }

        // ---------- Zähmen ----------

        internal static void TameXp(Vector3 pos, float amount)
        {
            if (!Cfg.TameSkillEnabled.Value || Tame == Skills.SkillType.None) return;
            var p = Player.m_localPlayer;
            if (p == null || Vector3.Distance(p.transform.position, pos) > 30f) return;
            p.RaiseSkill(Tame, amount * Cfg.TameXp.Value);
        }

        internal static float TameFactor(Vector3 pos)
        {
            if (!Cfg.TameSkillEnabled.Value || Tame == Skills.SkillType.None) return 0f;
            var p = Player.m_localPlayer;
            if (p == null || Vector3.Distance(p.transform.position, pos) > 50f) return 0f;
            return Mathf.Clamp01(p.GetSkillFactor(Tame));
        }

        // ---------- Symbole ----------

        private static Sprite Icon(Func<float, float, Color> pixel)
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    tex.SetPixel(x, y, pixel((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f));   // -1 … 1, y nach oben
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        private static readonly Color Clear = new Color(0, 0, 0, 0);
        private static readonly Color Wood = new Color(0.55f, 0.36f, 0.2f, 1f);
        private static readonly Color Cloth = new Color(0.93f, 0.88f, 0.75f, 1f);
        private static readonly Color Tan = new Color(0.85f, 0.72f, 0.45f, 1f);

        /// <summary>Segel über einem Rumpf.</summary>
        private static Color SailPixel(float x, float y)
        {
            if (Mathf.Abs(x + 0.05f) < 0.05f && y > -0.45f && y < 0.85f) return Wood;                // Mast
            if (x > 0.02f && y > -0.3f && y < 0.8f && x < 0.75f * (0.8f - y) / 1.1f + 0.02f) return Cloth;   // Segel
            if (y < -0.45f && y > -0.75f && Mathf.Abs(x) < 0.85f - (-0.45f - y) * 0.9f) return Wood; // Rumpf
            return Clear;
        }

        /// <summary>Pfote: großer Ballen und vier Zehen.</summary>
        private static Color PawPixel(float x, float y)
        {
            bool In(float cx, float cy, float rx, float ry) => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1f;
            if (In(0f, -0.35f, 0.42f, 0.34f)) return Tan;
            if (In(-0.55f, 0.15f, 0.16f, 0.2f) || In(-0.2f, 0.5f, 0.16f, 0.21f) ||
                In(0.2f, 0.5f, 0.16f, 0.21f) || In(0.55f, 0.15f, 0.16f, 0.2f)) return Tan;
            return Clear;
        }
    }

    /// <summary>Eigene Skills gleich beim Einloggen in der Skill-Liste zeigen (auch bei Stufe 0).</summary>
    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class Player_OnSpawned_ShowSkills
    {
        private static void Postfix(Player __instance)
        {
            try
            {
                if (__instance != Player.m_localPlayer) return;
                var skills = __instance.GetSkills();
                if (skills == null) return;
                if (Cfg.ExploreEnabled.Value && Exploration.Skill != Skills.SkillType.None) Util.Call(skills, "GetSkill", Exploration.Skill);
                if (Cfg.SailEnabled.Value && CustomSkills.Sail != Skills.SkillType.None) Util.Call(skills, "GetSkill", CustomSkills.Sail);
                if (Cfg.TameSkillEnabled.Value && CustomSkills.Tame != Skills.SkillType.None) Util.Call(skills, "GetSkill", CustomSkills.Tame);
            }
            catch (Exception e) { Plugin.LogWarn("Skills anzeigen: " + e.Message); }
        }
    }

    // ===================== Segeln =====================

    [HarmonyPatch(typeof(Ship), "GetSailForce")]
    internal static class Ship_GetSailForce_Skill
    {
        private static void Postfix(Ship __instance, ref Vector3 __result)
        {
            float m = CustomSkills.SailMult(__instance);
            if (m != 1f) __result *= m;
        }
    }

    /// <summary>Rudern (langsam vor/zurück) ebenfalls schneller.</summary>
    [HarmonyPatch(typeof(Ship), "CustomFixedUpdate")]
    internal static class Ship_CustomFixedUpdate_Skill
    {
        private static void Prefix(Ship __instance, out float __state)
        {
            __state = __instance.m_backwardForce;
            float m = CustomSkills.SailMult(__instance);
            if (m != 1f) __instance.m_backwardForce *= m;
        }

        private static void Finalizer(Ship __instance, float __state) => __instance.m_backwardForce = __state;
    }

    // ===================== Zähmen =====================

    [HarmonyPatch(typeof(Tameable), "Tame")]
    internal static class Tameable_Tame_Skill
    {
        private static void Postfix(Tameable __instance) => CustomSkills.TameXp(__instance.transform.position, 60f);
    }

    [HarmonyPatch(typeof(Tameable), "OnConsumedItem")]
    internal static class Tameable_OnConsumedItem_Skill
    {
        private static void Postfix(Tameable __instance) => CustomSkills.TameXp(__instance.transform.position, 2f);
    }

    [HarmonyPatch(typeof(Procreation), "MakePregnant")]
    internal static class Procreation_MakePregnant_Skill
    {
        private static void Postfix(Procreation __instance) => CustomSkills.TameXp(__instance.transform.position, 10f);
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class Character_RPC_Damage_TameSkill
    {
        private static void Prefix(Character __instance, object[] __args)
        {
            try
            {
                if (!Cfg.TameSkillEnabled.Value || __args.Length < 2 || !(__args[1] is HitData hit)) return;
                if (__instance.IsTamed())
                {
                    float f = CustomSkills.TameFactor(__instance.transform.position);
                    if (f > 0f) hit.ApplyModifier(1f - Cfg.TameProtect.Value / 100f * f);
                }
                var att = hit.GetAttacker();
                if (att != null && att != __instance && att.IsTamed() && !__instance.IsTamed())
                {
                    float f = CustomSkills.TameFactor(att.transform.position);
                    if (f > 0f) hit.ApplyModifier(1f + Cfg.TameDamage.Value / 100f * f);
                    CustomSkills.TameXp(att.transform.position, 0.5f);
                }
            }
            catch (Exception e) { Plugin.LogWarn("Zähmen-Skill: " + e.Message); }
        }
    }

}
