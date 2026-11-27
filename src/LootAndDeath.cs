using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    // ===================== 23 Trophäen =====================

    /// <summary>Trophäen-Chance jedes Monsters: fester Wert aus der Konfig (0–100 %).</summary>
    [HarmonyPatch(typeof(CharacterDrop), "Start")]
    internal static class CharacterDrop_Start_TrophyChance
    {
        private static void Postfix(CharacterDrop __instance)
        {
            if (!Cfg.TrophyEnabled.Value || __instance.m_drops == null) return;
            float chance = Mathf.Clamp01(Cfg.TrophyFactor.Value / 100f);
            foreach (var d in __instance.m_drops)
            {
                var item = d?.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>() : null;
                if (item?.m_itemData?.m_shared == null || item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy) continue;
                d.m_chance = chance;
            }
        }
    }

    // ===================== 24 Tod =====================

    /// <summary>Skill-Verlust beim Tod × Faktor.</summary>
    [HarmonyPatch(typeof(Skills), "OnDeath")]
    internal static class Skills_OnDeath_Loss
    {
        private static bool Prefix(Skills __instance)
        {
            if (!Cfg.DeathEnabled.Value || Mathf.Approximately(Cfg.DeathSkillLoss.Value, 1f)) return true;
            float f = __instance.m_DeathLowerFactor * Game.m_skillReductionRate * Cfg.DeathSkillLoss.Value;
            if (f > 0f) __instance.LowerAllSkills(f);
            return false;
        }
    }

    /// <summary>Grabstein: angelegte Ausrüstung und/oder Schnellleiste bleiben am Spieler.</summary>
    [HarmonyPatch(typeof(Player), "CreateTombStone")]
    internal static class Player_CreateTombStone_Keep
    {
        private static bool Prefix(Player __instance, out List<ItemDrop.ItemData> __state)
        {
            __state = null;
            if (!Cfg.DeathEnabled.Value || __instance != Player.m_localPlayer) return true;
            var inv = __instance.GetInventory();
            var list = Util.GetField(inv, "m_inventory") as List<ItemDrop.ItemData>;
            if (inv == null || list == null) return true;

            // Schnellleiste (oberste Reihe) vorübergehend aus dem Inventar nehmen
            if (Cfg.DeathKeepHotbar.Value)
            {
                __state = list.FindAll(i => i != null && i.m_gridPos.y == 0);
                list.RemoveAll(i => i != null && i.m_gridPos.y == 0);
            }
            if (!Cfg.DeathKeepEquipped.Value) return true;   // Rest macht Valheim

            // wie Valheim, nur ohne "alles ablegen": Angelegtes bleibt im Inventar (MoveInventoryToGrave nimmt es nicht mit)
            var zs = ZoneSystem.instance;
            if (zs != null && zs.GetGlobalKey(GlobalKeys.DeathKeepInventory)) return false;
            if (zs != null && (zs.GetGlobalKey(GlobalKeys.DeathDeleteItems) || zs.GetGlobalKey(GlobalKeys.DeathDeleteUnequipped)))
                inv.RemoveUnequipped();
            bool anyForGrave = list.Exists(i => i != null && !i.m_equipped && !i.m_shared.m_questItem);
            if (!anyForGrave || __instance.m_tombstone == null) return false;
            var go = Object.Instantiate(__instance.m_tombstone, __instance.GetCenterPoint(), __instance.transform.rotation);
            go.GetComponent<Container>().GetInventory().MoveInventoryToGrave(inv);
            var profile = Game.instance.GetPlayerProfile();
            go.GetComponent<TombStone>()?.Setup(profile.GetName(), profile.GetPlayerID());
            return false;
        }

        private static void Postfix(Player __instance, List<ItemDrop.ItemData> __state)
        {
            if (__state == null || __state.Count == 0) return;
            var inv = __instance.GetInventory();
            var list = Util.GetField(inv, "m_inventory") as List<ItemDrop.ItemData>;
            if (list == null) return;
            list.AddRange(__state);
            Util.Call(inv, "Changed", false, false);
        }
    }

    /// <summary>Essen behalten: Liste vor dem Tod merken und danach zurücklegen.</summary>
    [HarmonyPatch(typeof(Player), "OnDeath")]
    internal static class Player_OnDeath_KeepFood
    {
        private static void Prefix(Player __instance, out List<Player.Food> __state)
        {
            __state = null;
            if (!Cfg.DeathEnabled.Value || !Cfg.DeathKeepFood.Value) return;
            if (Util.GetField(__instance, "m_foods") is List<Player.Food> foods) __state = new List<Player.Food>(foods);
        }

        private static void Postfix(Player __instance, List<Player.Food> __state)
        {
            if (__state == null) return;
            if (Util.GetField(__instance, "m_foods") is List<Player.Food> foods && foods.Count == 0) foods.AddRange(__state);
        }
    }

    // ===================== 25 Seeschlangen =====================

    /// <summary>Spawn-Chance, Höchstzahl, Tageszeit und Wetter der Seeschlangen. Die Spawn-Listen sind geteilt, daher Originalwerte merken.</summary>
    [HarmonyPatch(typeof(SpawnSystem), "Awake")]
    internal static class SpawnSystem_Awake_Serpents
    {
        private class Org { public float Chance; public int Max; public bool Day, Night; public List<string> Env; }
        private static readonly Dictionary<SpawnSystem.SpawnData, Org> Orig = new Dictionary<SpawnSystem.SpawnData, Org>();

        private static void Postfix(SpawnSystem __instance)
        {
            if (__instance.m_spawnLists == null) return;
            foreach (var l in __instance.m_spawnLists)
            {
                if (l?.m_spawners == null) continue;
                foreach (var d in l.m_spawners)
                {
                    if (d?.m_prefab == null || d.m_prefab.name != "Serpent") continue;
                    if (!Orig.TryGetValue(d, out var o))
                    {
                        Orig[d] = o = new Org
                        {
                            Chance = d.m_spawnChance, Max = d.m_maxSpawned, Day = d.m_spawnAtDay, Night = d.m_spawnAtNight,
                            Env = d.m_requiredEnvironments != null ? new List<string>(d.m_requiredEnvironments) : new List<string>()
                        };
                        Plugin.LogInfo("Seeschlangen: normale Chance " + d.m_spawnChance + " %, max. " + d.m_maxSpawned
                            + ", Tag " + d.m_spawnAtDay + ", Nacht " + d.m_spawnAtNight + ", Wetter: " + (o.Env.Count > 0 ? string.Join(",", o.Env) : "alle"));
                    }
                    bool on = Cfg.SerpentEnabled.Value;
                    d.m_spawnChance = on ? Mathf.Clamp(Cfg.SerpentChance.Value, 0f, 100f) : o.Chance;
                    d.m_maxSpawned = on && Cfg.SerpentMax.Value > 0 ? Cfg.SerpentMax.Value : o.Max;
                    d.m_spawnAtDay = o.Day || (on && Cfg.SerpentDay.Value);
                    d.m_spawnAtNight = o.Night || (on && Cfg.SerpentDay.Value);
                    d.m_requiredEnvironments = on && Cfg.SerpentAnyWeather.Value ? new List<string>() : new List<string>(o.Env);
                }
            }
        }
    }

    // ===================== 26 Steam-Erfolge =====================

    /// <summary>Valheims eigener Schalter "Cheat-Prüfungen überspringen": dann zählen Erfolge auch mit Mods und Cheats.</summary>
    [HarmonyPatch(typeof(PlayerProfile), "s_bypassCheatChecks", MethodType.Getter)]
    internal static class PlayerProfile_BypassCheatChecks
    {
        private static void Postfix(ref bool __result)
        {
            if (Cfg.AchievementsEnabled != null && Cfg.AchievementsEnabled.Value) __result = true;
        }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class Player_OnSpawned_Achievements
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !Cfg.AchievementsEnabled.Value || Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            yield return new WaitForSeconds(5f);
            var p = Player.m_localPlayer;
            if (p == null) yield break;

            if (Cfg.AchievementsClearItems.Value)
            {
                int cleared = 0;
                foreach (var item in p.GetInventory().GetAllItems())
                    if (item != null && item.m_cheated) { item.m_cheated = false; cleared++; }
                if (cleared > 0) Plugin.LogInfo("Steam-Erfolge: Cheat-Markierung bei " + cleared + " Items entfernt");
            }

            if (Cfg.AchievementsSync.Value && Achievements.m_instance != null && Game.instance != null)
            {
                int unlocked = 0;
                foreach (var list in Achievements.m_instance.m_achievementLists)
                {
                    if (list?.m_achievements == null) continue;
                    foreach (var a in list.m_achievements)
                    {
                        bool ok = false;
                        try { ok = a != null && !a.m_unlocked && a.CheckUnlocked(); } catch { }
                        if (!ok) continue;
                        try { Achievements.AchievementEvent(a); unlocked++; } catch (System.Exception e) { Plugin.LogWarn("Steam-Erfolge: " + e.Message); }
                    }
                }
                Plugin.LogInfo("Steam-Erfolge: " + unlocked + " Erfolge nachgeholt");
                if (unlocked > 0)
                    p.Message(MessageHud.MessageType.TopLeft, L.T("Steam-Erfolge: ", "Steam achievements: ") + unlocked + L.T(" nachgeholt", " unlocked"));
            }
        }
    }
}
