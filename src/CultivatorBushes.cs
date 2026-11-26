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

        internal static void Tick()
        {
            if (!Cfg.PlantsEnabled.Value || !Cfg.BushesEnabled.Value) return;
            var player = Player.m_localPlayer;
            var scene = ZNetScene.instance;
            if (player == null || scene == null || ObjectDB.instance == null || _doneFor == scene) return;
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

                if (!table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
                added++;
            }

            Util.Call(player, "UpdateKnownRecipesList");
            Util.Call(player, "UpdateAvailablePiecesList");
            Plugin.LogInfo("Büsche/Pilze im Kultivator: " + added + " (" + string.Join(", ", byItem.Values.Select(g => g.name)) + ")");
        }
    }
}
