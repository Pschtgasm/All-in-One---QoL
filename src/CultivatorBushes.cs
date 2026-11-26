using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Büsche und Pilze (Himbeere, Blaubeere, Moltebeere, Pilze …) mit dem Kultivator pflanzen.
    /// Kosten: X Stück des jeweiligen Rohstoffs. Kein Raster, kein Mindestabstand.
    /// Die Original-Prefabs bekommen dafür eine Piece-Komponente – andere Spieler ohne Mod sehen
    /// ganz normale Büsche/Pilze.
    /// </summary>
    internal static class CultivatorBushes
    {
        private static ZNetScene _doneFor;
        private static readonly List<GameObject> _mine = new List<GameObject>();
        private static float _nextCheck;

        internal static void Tick()
        {
            if (!Cfg.PlantsEnabled.Value || !Cfg.BushesEnabled.Value) return;
            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null || ObjectDB.instance == null) return;
            if (_doneFor == scene) { Recheck(player); return; }
            _doneFor = scene;

            try { Register(player, scene); }
            catch (Exception e) { Plugin.LogWarn("Büsche/Pilze: " + e.Message); }
        }

        private static void Register(Player player, ZNetScene scene)
        {
            var cultivator = ObjectDB.instance.GetItemPrefab("Cultivator");
            var table = cultivator ? cultivator.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
            if (table == null) { Plugin.LogWarn("Büsche/Pilze: Kultivator nicht gefunden"); return; }

            var wanted = Cfg.BushItems.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

            // Was der Kultivator schon von Haus aus pflanzen kann (z.B. Jotunnpuffs, Magierkappe), nicht doppelt anbieten
            var vanilla = new HashSet<string>();
            foreach (var go in table.m_pieces)
            {
                var pl = go ? go.GetComponent<Plant>() : null;
                if (pl == null || pl.m_grownPrefabs == null) continue;
                foreach (var g in pl.m_grownPrefabs)
                {
                    var pk = g ? g.GetComponent<Pickable>() : null;
                    if (pk != null && pk.m_itemPrefab != null) vanilla.Add(pk.m_itemPrefab.name);
                }
            }
            foreach (var item in wanted.Where(vanilla.Contains).ToList())
            {
                Plugin.LogInfo("Büsche/Pilze: " + item + " kann der Kultivator schon – übersprungen");
                wanted.Remove(item);
            }

            // Pro Rohstoff das passende Pickable-Prefab suchen (kürzester Name gewinnt, z.B. "RaspberryBush")
            var byItem = new Dictionary<string, GameObject>();
            foreach (var go in scene.m_prefabs)
            {
                if (!go || go.GetComponent<Plant>() != null) continue;
                var pick = go.GetComponent<Pickable>();
                if (pick == null || pick.m_itemPrefab == null) continue;
                string item = pick.m_itemPrefab.name;
                if (!wanted.Contains(item)) continue;
                if (!byItem.TryGetValue(item, out var old) || go.name.Length < old.name.Length) byItem[item] = go;
            }

            int added = 0;
            foreach (var item in wanted)
            {
                if (!byItem.TryGetValue(item, out var prefab))
                {
                    Plugin.LogWarn("Büsche/Pilze: kein Busch/Pilz für \"" + item + "\" gefunden");
                    continue;
                }
                var pick = prefab.GetComponent<Pickable>();
                var drop = pick.m_itemPrefab.GetComponent<ItemDrop>();
                if (drop == null) continue;

                var piece = prefab.GetComponent<Piece>() ?? prefab.AddComponent<Piece>();
                piece.m_name = string.IsNullOrEmpty(pick.m_overrideName) ? drop.m_itemData.m_shared.m_name : pick.m_overrideName;
                piece.m_description = L.T("Pflanzt ", "Plants ") + drop.m_itemData.m_shared.m_name;
                piece.m_icon = drop.m_itemData.GetIcon();
                piece.m_category = Piece.PieceCategory.Misc;
                piece.m_resources = new[]
                {
                    new Piece.Requirement { m_resItem = drop, m_amount = Mathf.Max(1, Cfg.BushCost.Value), m_recover = false }
                };
                piece.m_groundOnly = true;
                piece.m_clipEverything = true;          // direkt am Zielpunkt, ganz nah aneinander möglich
                piece.m_randomInitBuildRotation = true;
                piece.m_canBeRemoved = false;           // wilde Büsche nicht mit dem Hammer abreißbar
                piece.m_onlyInBiome = Heightmap.Biome.None;
                piece.m_cultivatedGroundOnly = false;
                piece.m_enabled = true;                  // andere Mods (z.B. More Vanilla Build Prefabs) schalten es sonst ab

                // immer wieder ernten: nachwachsen lassen, falls das Original das nicht tut
                if (pick.m_respawnTimeMinutes <= 0f) pick.m_respawnTimeMinutes = 240f;

                if (!table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
                if (!_mine.Contains(prefab)) _mine.Add(prefab);
                added++;
            }

            Util.Call(player, "UpdateKnownRecipesList");
            Util.Call(player, "UpdateAvailablePiecesList");
            Plugin.LogInfo("Büsche/Pilze im Kultivator: " + added + " (" + string.Join(", ", byItem.Values.Select(g => g.name)) + ")");
        }

        /// <summary>
        /// Alle paar Sekunden prüfen, ob ein anderer Mod unsere Büsche wieder abgeschaltet, umgebaut
        /// oder aus dem Kultivator entfernt hat – dann zurückholen.
        /// </summary>
        private static void Recheck(Player player)
        {
            if (Time.time < _nextCheck || _mine.Count == 0) return;
            _nextCheck = Time.time + 5f;
            var cultivator = ObjectDB.instance.GetItemPrefab("Cultivator");
            var table = cultivator ? cultivator.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
            if (table == null) return;
            bool changed = false;
            foreach (var prefab in _mine)
            {
                if (!prefab) continue;
                var piece = prefab.GetComponent<Piece>();
                var pick = prefab.GetComponent<Pickable>();
                var drop = pick?.m_itemPrefab ? pick.m_itemPrefab.GetComponent<ItemDrop>() : null;
                if (piece == null || drop == null) continue;
                int cost = Mathf.Max(1, Cfg.BushCost.Value);
                bool costOk = piece.m_resources != null && piece.m_resources.Length == 1 && piece.m_resources[0].m_resItem == drop && piece.m_resources[0].m_amount == cost;
                if (piece.m_enabled && costOk && table.m_pieces.Contains(prefab)) continue;
                piece.m_enabled = true;
                piece.m_resources = new[] { new Piece.Requirement { m_resItem = drop, m_amount = cost, m_recover = false } };
                if (!table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
                changed = true;
            }
            if (changed)
            {
                Util.Call(player, "UpdateAvailablePiecesList");
                Plugin.LogInfo("Büsche/Pilze: von einem anderen Mod geändert – wiederhergestellt");
            }
        }
    }
}

namespace Pscht_QoL
{
    /// <summary>
    /// Büsche, Pilze, Disteln aus der Kultivator-Liste wachsen nach dem Ernten nach – auch die, die schon
    /// vor dem Mod-Start in der Welt geladen waren. Pilze und Disteln haben im Spiel kein "leeres" Aussehen:
    /// sie bleiben als kleiner Stumpf stehen, bis sie nachgewachsen sind.
    /// </summary>
    internal static class BushRegrow
    {
        internal const float RespawnMinutes = 240f;
        private const float StumpScale = 0.4f;   // deutlich kleiner als die volle Pflanze
        private static readonly Color StumpColor = new Color(0.35f, 0.25f, 0.15f, 1f);   // braun-dunkel = abgeerntet

        private static string _listFor;
        private static readonly HashSet<string> _items = new HashSet<string>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pickable, ScaleBox> _scale =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Pickable, ScaleBox>();
        private class ScaleBox { public Transform Target; public Vector3 Scale; public bool Skip; }

        internal static bool IsOurs(Pickable p)
        {
            if (p == null || p.m_itemPrefab == null || !Cfg.PlantsEnabled.Value || !Cfg.BushesEnabled.Value) return false;
            if (p.GetComponent<Plant>() != null) return false;
            string list = Cfg.BushItems.Value ?? "";
            if (_listFor != list)
            {
                _listFor = list;
                _items.Clear();
                foreach (var s in list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    if (s.Trim().Length > 0) _items.Add(s.Trim());
            }
            return _items.Contains(p.m_itemPrefab.name);
        }

        /// <summary>Kleiner Stumpf, solange geerntet (nur Pickables ohne eigenes "leeres" Modell).</summary>
        internal static void UpdateStump(Pickable p)
        {
            if (p == null || p.m_respawnTimeMinutes <= 0f || !IsOurs(p)) return;
            if (!_scale.TryGetValue(p, out var box))
            {
                box = new ScaleBox();
                var hide = p.m_hideWhenPicked;
                if (hide == null) box.Target = p.transform;
                else
                {
                    // Pilze/Disteln: "beim Ernten verstecken" ist die ganze Pflanze -> sie wäre komplett weg.
                    // Büsche: nur die Beeren werden versteckt, der Busch bleibt -> nichts tun.
                    bool whole = true;
                    foreach (var rend in p.GetComponentsInChildren<Renderer>(true))
                        if (rend != null && !rend.transform.IsChildOf(hide.transform)) { whole = false; break; }
                    box.Skip = !whole;
                    box.Target = p.transform;   // ganze Pflanze verkleinern (sicher, egal wie das Modell aufgebaut ist)
                }
                box.Scale = box.Target.localScale;
                _scale.Add(p, box);
            }
            if (box.Skip || box.Target == null) return;
            bool picked = p.GetPicked();
            if (p.m_hideWhenPicked != null && picked && p.GetEnabled != 0 && !p.m_hideWhenPicked.activeSelf)
                p.m_hideWhenPicked.SetActive(true);   // als Stumpf sichtbar lassen
            box.Target.localScale = picked ? box.Scale * StumpScale : box.Scale;
            if (MaterialMan.instance != null)
            {
                if (picked) MaterialMan.instance.SetValue(box.Target.gameObject, ShaderProps._Color, StumpColor);
                else MaterialMan.instance.ResetValue(box.Target.gameObject, ShaderProps._Color);
            }
        }

        internal static bool IsStump(Pickable p) => _scale.TryGetValue(p, out var box) && !box.Skip;

        /// <summary>Restzeit bis zum Nachwachsen als Text, z.B. "1 Std. 20 Min.".</summary>
        internal static string Remaining(Pickable p)
        {
            var nview = Util.GetField(p, "m_nview") as ZNetView;
            if (nview == null || !nview.IsValid() || ZNet.instance == null) return null;
            long ticks = nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L);
            if (ticks <= 1) return null;
            double left = p.m_respawnTimeMinutes - (ZNet.instance.GetTime() - new DateTime(ticks)).TotalMinutes;
            if (left < 0) left = 0;
            int h = (int)(left / 60), m = (int)Math.Ceiling(left % 60);
            if (m == 60) { h++; m = 0; }
            return h > 0 ? h + L.T(" Std. ", " h ") + m + L.T(" Min.", " min") : m + L.T(" Min.", " min");
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Pickable), "Awake")]
    internal static class Pickable_Awake_BushRegrow
    {
        // Vor Awake: sonst löscht Valheim bereits geerntete Pilze/Disteln ohne Nachwachszeit
        private static void Prefix(Pickable __instance)
        {
            if (!BushRegrow.IsOurs(__instance)) return;
            float set = Cfg.BushRegrowMinutes.Value;
            if (set > 0f) __instance.m_respawnTimeMinutes = set;   // eigene Zeit aus der Konfig
            else if (__instance.m_respawnTimeMinutes <= 0f) __instance.m_respawnTimeMinutes = BushRegrow.RespawnMinutes;
        }

        private static void Postfix(Pickable __instance) => BushRegrow.UpdateStump(__instance);
    }

    [HarmonyLib.HarmonyPatch(typeof(Pickable), "SetPicked")]
    internal static class Pickable_SetPicked_BushRegrow
    {
        private static void Postfix(Pickable __instance, object[] __args)
        {
            BushRegrow.UpdateStump(__instance);
            if (__args != null && __args.Length > 0 && __args[0] is bool picked && picked && BushRegrow.IsOurs(__instance))
                Plugin.LogInfo("Büsche/Pilze: " + __instance.name + " geerntet, wächst nach in " + __instance.m_respawnTimeMinutes + " Min." +
                               (BushRegrow.IsStump(__instance) ? " (Stumpf)" : ""));
        }
    }

    /// <summary>Geerntete Büsche/Pilze zeigen beim Anschauen, wann sie wieder erntbar sind.</summary>
    [HarmonyLib.HarmonyPatch(typeof(Pickable), "GetHoverText")]
    internal static class Pickable_GetHoverText_Timer
    {
        private static void Postfix(Pickable __instance, ref string __result)
        {
            if (!string.IsNullOrEmpty(__result) || __instance.m_respawnTimeMinutes <= 0f || !__instance.GetPicked()) return;
            if (__instance.GetEnabled == 0) return;
            string left = BushRegrow.Remaining(__instance);
            if (left == null) return;
            __result = Localization.instance.Localize(__instance.GetHoverName()) + "\n" + L.T("Wächst nach in ", "Regrows in ") + left;
        }
    }
}
