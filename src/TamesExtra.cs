using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>Tierbefehle, Herholen, Portale, Schutz, Fressen aus Kisten.</summary>
    internal static class TamesExtra
    {
        private static float _nextLostCheck;
        private static float _nextFeedCheck;

        // ---------- Hilfen ----------

        private static IEnumerable<Character> Tames(Player player, float range)
        {
            Vector3 pos = player.transform.position;
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c == player || c.IsPlayer() || !c.IsTamed()) continue;
                if (Vector3.Distance(pos, c.transform.position) > range) continue;
                if (c.GetComponent<Tameable>() == null) continue;
                yield return c;
            }
        }

        private static IEnumerable<Character> Followers(Player player, float range)
        {
            foreach (var c in Tames(player, range))
            {
                var ai = c.GetComponent<MonsterAI>();
                if (ai != null && ai.GetFollowTarget() == player.gameObject) yield return c;
            }
        }

        /// <summary>Ruft Tameable.Command(user, ...) mit Standardwerten für weitere Parameter auf.</summary>
        private static void Command(Tameable t, Player player)
        {
            var m = AccessTools.Method(typeof(Tameable), "Command");
            if (m == null) return;
            var ps = m.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (i == 0) args[i] = player;
                else if (ps[i].ParameterType == typeof(bool)) args[i] = false; // keine Meldung pro Tier
                else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
            }
            try { m.Invoke(t, args); } catch { }
        }

        internal static void MoveTo(Character c, Vector3 pos)
        {
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            if (!nv.IsOwner()) nv.ClaimOwnership();
            c.transform.position = pos;
            var body = c.GetComponent<Rigidbody>();
            if (body) { body.position = pos; body.linearVelocity = Vector3.zero; }
            nv.GetZDO().SetPosition(pos);
        }

        private static Vector3 Around(Vector3 center, int i)
        {
            float angle = i * 1.3f;
            float radius = 2f + (i % 4) * 0.7f;
            return center + new Vector3(Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);
        }

        // ---------- Tasten (aus Plugin.Update) ----------

        internal static void HandleInput()
        {
            if (!Cfg.TamingEnabled.Value) return;
            var player = Player.m_localPlayer;
            if (player == null || Util.InputBlocked()) return;

            if (Cfg.FollowAllKey.Value.IsDown())
            {
                int n = 0;
                foreach (var c in Tames(player, Cfg.CommandRange.Value).ToList())
                {
                    var ai = c.GetComponent<MonsterAI>();
                    if (ai == null || ai.GetFollowTarget() == player.gameObject) continue;
                    Command(c.GetComponent<Tameable>(), player);
                    n++;
                }
                player.Message(MessageHud.MessageType.Center, n + L.T(" Tiere folgen dir", " animals follow you"));
            }

            if (Cfg.StayAllKey.Value.IsDown())
            {
                int n = 0;
                foreach (var c in Followers(player, Cfg.CommandRange.Value).ToList())
                {
                    Command(c.GetComponent<Tameable>(), player);
                    n++;
                }
                player.Message(MessageHud.MessageType.Center, n + L.T(" Tiere bleiben hier", " animals stay here"));
            }

            if (Cfg.TeleportTamesKey.Value.IsDown())
            {
                int i = 0;
                foreach (var c in Followers(player, 10000f).ToList())
                    MoveTo(c, Around(player.transform.position, i++));
                player.Message(MessageHud.MessageType.Center, i + L.T(" Tiere hergeholt", " animals called to you"));
            }
        }

        // ---------- Regelmaessig (aus Plugin.Update) ----------

        internal static void Tick()
        {
            if (!Cfg.TamingEnabled.Value) return;
            var player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting()) return;

            if (Cfg.AutoTeleportLostTames.Value && Time.time >= _nextLostCheck)
            {
                _nextLostCheck = Time.time + 2f;
                int i = 0;
                foreach (var c in Followers(player, 10000f).ToList())
                    if (Vector3.Distance(c.transform.position, player.transform.position) > Cfg.LostTameDistance.Value)
                        MoveTo(c, Around(player.transform.position, i++));
            }

            if ((Cfg.TamesEatFromChests.Value || Cfg.WildEatFromChests.Value) && Time.time >= _nextFeedCheck)
            {
                _nextFeedCheck = Time.time + 5f;
                FeedFromChests(player);
            }
        }

        private static void FeedFromChests(Player player)
        {
            Vector3 pos = player.transform.position;
            foreach (var c in Character.GetAllCharacters().ToList())
            {
                if (c == null || c.IsPlayer() || c.IsDead()) continue;
                if (Vector3.Distance(pos, c.transform.position) > 60f) continue;

                bool tamed = c.IsTamed();
                if (tamed && !Cfg.TamesEatFromChests.Value) continue;
                if (!tamed && !Cfg.WildEatFromChests.Value) continue;

                var tameable = c.GetComponent<Tameable>();
                var ai = c.GetComponent<MonsterAI>();
                var nv = c.GetComponent<ZNetView>();
                if (tameable == null || ai == null || nv == null || !nv.IsValid() || !nv.IsOwner()) continue;
                if (!(Util.Call(tameable, "IsHungry") is bool hungry) || !hungry) continue;
                if (ai.m_consumeItems == null || ai.m_consumeItems.Count == 0) continue;

                // Wilde Tiere nur, wenn sie gerade nicht kaempfen
                if (!tamed && Util.Call(ai, "IsAlerted") is bool alerted && alerted) continue;

                foreach (var chest in ContainerTracker.All.ToList())
                {
                    if (chest == null) continue;
                    if (Vector3.Distance(chest.transform.position, c.transform.position) > Cfg.FeedChestRange.Value) continue;
                    var inv = chest.GetInventory();
                    if (inv == null || chest.IsInUse()) continue;

                    ItemDrop food = null;
                    ItemDrop.ItemData stack = null;
                    foreach (var f in ai.m_consumeItems)
                    {
                        if (f == null) continue;
                        stack = inv.GetAllItems().FirstOrDefault(i => i.m_shared.m_name == f.m_itemData.m_shared.m_name);
                        if (stack != null) { food = f; break; }
                    }
                    if (food == null) continue;

                    ContainerTracker.ClaimOwnership(chest);
                    inv.RemoveItem(stack, 1);
                    var onConsumed = Util.GetField(ai, "m_onConsumedItem") as Action<ItemDrop>;
                    onConsumed?.Invoke(food);
                    break;
                }
            }
        }

        // ---------- Nachwuchs mit Stern ----------

        [ThreadStatic] internal static bool InProcreate;
    }

    [HarmonyPatch(typeof(Tameable), "Awake")]
    internal static class Tameable_Awake_Commandable
    {
        private static void Postfix(Tameable __instance)
        {
            if (Cfg.TamingEnabled.Value && Cfg.AllTamesCommandable.Value) __instance.m_commandable = true;
        }
    }

    [HarmonyPatch(typeof(Procreation), "Procreate")]
    internal static class Procreation_Procreate
    {
        private static void Prefix() => TamesExtra.InProcreate = true;
        private static void Finalizer() => TamesExtra.InProcreate = false;
    }

    /// <summary>Beim Erzeugen des Nachwuchses: Level evtl. um 1 erhoehen.</summary>
    [HarmonyPatch(typeof(Character), "SetLevel")]
    internal static class Character_SetLevel_Offspring
    {
        // "ref int level": Änderungen über __args werden in dieser Harmony-Version nicht übernommen
        private static void Prefix(ref int level)
        {
            if (!TamesExtra.InProcreate || !Cfg.TamingEnabled.Value) return;
            int before = level;
            if (UnityEngine.Random.Range(0, 100) < Cfg.OffspringStarChance.Value)
                level++;
            level = Mathf.Max(before, Mathf.Min(level, Cfg.OffspringMaxLevel.Value));
            Plugin.LogInfo("Nachwuchs: Mutter " + (before - 1) + " Sterne -> Jungtier " + (level - 1) + " Sterne");
        }
    }

    /// <summary>
    /// Das Spiel zeigt Sterne nur für Level 2 (1 Stern) und Level 3 (2 Sterne). Ab Level 4
    /// würde gar kein Stern mehr angezeigt – hier werden zusätzliche Stern-Symbole (Kopien der
    /// Original-Sterne) rechts angehängt: 3, 4, 5 … Sterne wie im Spiel.
    /// </summary>
    [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
    internal static class EnemyHud_HighLevelStars
    {
        private const string ExtraPrefix = "PschtQoL_Star";

        private static void Postfix(EnemyHud __instance)
        {
            if (!(Util.GetField(__instance, "m_huds") is System.Collections.IDictionary huds)) return;
            foreach (var data in huds.Values)
            {
                var c = Util.GetField(data, "m_character") as Character;
                if (c == null || !(Util.GetField(data, "m_level3") is RectTransform l3)) continue;
                int stars = c.GetLevel() - 1;
                try { UpdateStars(l3, Util.GetField(data, "m_level2") as RectTransform, stars); } catch { }
            }
        }

        private static bool _logged;

        private static void UpdateStars(RectTransform l3, RectTransform l2, int stars)
        {
            // Original-Sterne (die Bilder in level_3, auch tiefer verschachtelt), ohne unsere Kopien
            var originals = new List<RectTransform>();
            foreach (var g in l3.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                if (g.transform == l3 || g.transform.parent != l3 || g.name.StartsWith(ExtraPrefix)) continue;
                originals.Add((RectTransform)g.transform);
            }
            // Rückfall: level_3 ist ein einziges Bild -> einzelnen Stern aus level_2 kopieren
            if (originals.Count == 0 && l2 != null && l2.childCount > 0 && l2.GetChild(0) is RectTransform s2)
                originals.Add(s2);
            if (!_logged && stars >= 3)
            {
                _logged = true;
                Plugin.LogInfo("Sterne: level_3 hat " + originals.Count + " Stern-Bilder, zeige " + stars + " Sterne");
            }

            int extra = stars >= 3 ? stars - 2 : 0;
            if (extra > 0) l3.gameObject.SetActive(true);

            // Kopien anlegen/anpassen
            for (int i = 0; ; i++)
            {
                var t = l3.Find(ExtraPrefix + i) as RectTransform;
                if (i >= extra)
                {
                    if (t == null) break;
                    if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
                    continue;
                }
                if (originals.Count == 0) break;
                if (t == null)
                {
                    var src = originals[originals.Count - 1];
                    var go = UnityEngine.Object.Instantiate(src.gameObject, l3);
                    go.name = ExtraPrefix + i;
                    t = (RectTransform)go.transform;
                }
                // Abstand wie zwischen den beiden Original-Sternen (sonst Breite des Sterns)
                var last = originals[originals.Count - 1];
                Vector2 step = originals.Count >= 2
                    ? last.anchoredPosition - originals[originals.Count - 2].anchoredPosition
                    : new Vector2(last.rect.width, 0f);
                t.anchoredPosition = last.anchoredPosition + step * (i + 1);
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>Tiere folgen durch Portale: beim Teleport werden Begleiter mit ans Ziel versetzt.</summary>
    [HarmonyPatch(typeof(Player), "TeleportTo")]
    internal static class Player_TeleportTo_Tames
    {
        private static void Postfix(Player __instance, object[] __args, bool __result)
        {
            if (!__result || !Cfg.TamingEnabled.Value || !Cfg.TamesThroughPortals.Value) return;
            if (__instance != Player.m_localPlayer || __args.Length == 0 || !(__args[0] is Vector3 dest)) return;

            int i = 0;
            foreach (var c in Character.GetAllCharacters().ToList())
            {
                if (c == null || c.IsPlayer() || !c.IsTamed()) continue;
                var ai = c.GetComponent<MonsterAI>();
                if (ai == null || ai.GetFollowTarget() != __instance.gameObject) continue;
                if (Vector3.Distance(c.transform.position, __instance.transform.position) > 50f) continue;
                float angle = i * 1.3f, radius = 2f + (i % 4) * 0.7f;
                TamesExtra.MoveTo(c, dest + new Vector3(Mathf.Cos(angle) * radius, 1f, Mathf.Sin(angle) * radius));
                i++;
            }
        }
    }

    /// <summary>Gezähmte Tiere vor dem Tod schuetzen (nicht bei Toetung durch Spieler).</summary>
    [HarmonyPatch]
    internal static class Character_OnDeath_Protect
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // GetDeclaredMethods statt DeclaredMethod: keine HarmonyX-Warnung, wenn Humanoid kein eigenes OnDeath hat
            foreach (var t in new[] { typeof(Character), typeof(Humanoid) })
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                    if (m.Name == "OnDeath" && m.GetParameters().Length == 0) yield return m;
        }

        private static bool Prefix(Character __instance)
        {
            if (!Cfg.TamingEnabled.Value || !Cfg.ProtectTames.Value) return true;
            if (__instance == null || __instance.IsPlayer() || !__instance.IsTamed()) return true;

            var lastHit = Util.GetField(__instance, "m_lastHit") as HitData;
            var attacker = lastHit?.GetAttacker();
            if (attacker != null && attacker.IsPlayer()) return true; // vom Spieler getoetet -> normal sterben

            __instance.SetHealth(__instance.GetMaxHealth() * Cfg.ProtectHealPercent.Value / 100f);
            string msg = Localization.instance.Localize(__instance.GetHoverName()) + L.T(" wurde gerettet (", " was rescued (") +
                         Mathf.RoundToInt(Cfg.ProtectHealPercent.Value) + L.T(" % Leben)", " % health)");
            // Groß in der Bildmitte für alle Spieler in der Nähe (OnDeath läuft nur beim Besitzer des Tieres)
            foreach (var p in Player.GetAllPlayers())
                if (p != null && Vector3.Distance(p.transform.position, __instance.transform.position) < 60f)
                    p.Message(MessageHud.MessageType.Center, msg);
            if (Player.m_localPlayer != null &&
                Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) >= 60f)
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, msg);
            return false;
        }
    }
}
