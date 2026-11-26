using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Kultivator-Raster: Pflanzen werden in einem Raster (Breite x Länge) gesetzt.
    /// Strg + Pfeil links/rechts = schmaler/breiter, Strg + Pfeil hoch/runter = länger/kürzer.
    /// Das Raster läuft vom Spieler weg (nach vorne) und ist an den Himmelsrichtungen ausgerichtet,
    /// damit mehrere Raster sauber aneinanderpassen.
    /// </summary>
    internal static class CultivatorGrid
    {
        private static readonly List<GameObject> _ghosts = new List<GameObject>();
        private static string _ghostFor;
        private static int _spaceMask;

        private static int SpaceMask
        {
            get
            {
                if (_spaceMask == 0)
                    _spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
                return _spaceMask;
            }
        }

        private static bool Active => Cfg.PlantsEnabled.Value && Cfg.GridEnabled.Value;

        /// <summary>Nur normale Pflanzen/Setzlinge (keine Ranken, die an etwas hängen).</summary>
        internal static Plant GridPlant(Piece piece)
        {
            if (!piece) return null;
            var plant = piece.GetComponent<Plant>();
            if (!plant) return null;
            // Ranken (wachsen an Wänden/Gittern) nicht im Raster
            if (piece.GetComponentInChildren<Vine>(true) != null || piece.name.IndexOf("vine", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return null;
            return plant;
        }

        private static float Spacing(Plant plant) =>
            Cfg.GridSpacing.Value > 0f ? Cfg.GridSpacing.Value : Mathf.Max(0.5f, plant.m_growRadius * 2f);

        /// <summary>Alle Rasterpunkte außer dem Hauptpunkt (dort steht die normale Vorschau).</summary>
        private static IEnumerable<Vector3> Cells(Player player, Vector3 origin, Plant plant)
        {
            int w = Mathf.Max(1, Cfg.GridWidth.Value), l = Mathf.Max(1, Cfg.GridLength.Value);
            float sp = Spacing(plant);

            Vector3 f = player.transform.forward;
            f = Mathf.Abs(f.x) > Mathf.Abs(f.z) ? new Vector3(Mathf.Sign(f.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(f.z));
            Vector3 r = new Vector3(f.z, 0f, -f.x);
            int c0 = (w - 1) / 2;

            for (int row = 0; row < l; row++)
                for (int col = 0; col < w; col++)
                {
                    if (row == 0 && col == c0) continue;
                    Vector3 p = origin + f * (row * sp) + r * ((col - c0) * sp);
                    if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetGroundHeight(p);
                    yield return p;
                }
        }

        private static bool CanPlant(Piece piece, Plant plant, Vector3 pos) => CanPlant(piece, plant, pos, out _);

        private static bool CanPlant(Piece piece, Plant plant, Vector3 pos, out string reason)
        {
            reason = null;
            var hm = Heightmap.FindHeightmap(pos);
            if (hm == null) { reason = "kein Boden"; return false; }
            if (piece.m_cultivatedGroundOnly && !hm.IsCultivated(pos)) { reason = "nicht bearbeitet"; return false; }
            if (ZoneSystem.instance != null && pos.y < ZoneSystem.instance.m_waterLevel) { reason = "Wasser"; return false; }
            if (piece.m_onlyInBiome != Heightmap.Biome.None && (Heightmap.FindBiome(pos) & piece.m_onlyInBiome) == 0) { reason = "Biom"; return false; }
            if (Location.IsInsideNoBuildLocation(pos)) { reason = "Bauverbot"; return false; }
            if (!PrivateArea.CheckAccess(pos, 0f, false, false)) { reason = "Schutzstein"; return false; }
            // wie Plant.HaveGrowSpace: alles außer gesunden Pflanzen blockiert (Trigger zählen nicht)
            foreach (var col in Physics.OverlapSphere(pos, plant.m_growRadius, SpaceMask, QueryTriggerInteraction.Ignore))
            {
                if (col == null) continue;
                reason = "blockiert durch " + col.gameObject.name + " (" + LayerMask.LayerToName(col.gameObject.layer) + ")";
                return false;
            }
            return true;
        }

        /// <summary>Einmal pro gewählter Pflanze ins Log schreiben, warum Rasterplätze fehlen.</summary>
        private static void LogDiagnose(Player player, Piece piece, Plant plant, Vector3 origin)
        {
            int ok = 0, total = 0;
            var reasons = new Dictionary<string, int>();
            foreach (var p in Cells(player, origin, plant))
            {
                total++;
                if (CanPlant(piece, plant, p, out string r)) { ok++; continue; }
                reasons[r] = reasons.TryGetValue(r, out int n) ? n + 1 : 1;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in reasons) sb.Append(" | ").Append(kv.Key).Append(" x").Append(kv.Value);
            Plugin.LogInfo("Raster " + piece.name + ": Wuchsradius " + plant.m_growRadius.ToString("0.##") +
                           " m, Abstand " + Spacing(plant).ToString("0.##") + " m, frei " + ok + "/" + total + sb);
        }

        private static bool HaveMaterial(Player player, Piece piece, int count)
        {
            var inv = player.GetInventory();
            if (inv == null) return false;
            foreach (var req in piece.m_resources)
            {
                if (req == null || !req.m_resItem || req.m_amount <= 0) continue;
                if (inv.CountItems(req.m_resItem.m_itemData.m_shared.m_name) < req.m_amount * count) return false;
            }
            return true;
        }

        // ---------------- Vorschau ----------------

        /// <summary>
        /// Nur das Aussehen der Vorschau kopieren (Meshes + Materialien), ohne Spiel-Komponenten.
        /// Eine echte Kopie würde Plant.Awake usw. auslösen (NullReferenceException im Log).
        /// </summary>
        private static GameObject MakeVisualCopy(GameObject ghost)
        {
            var root = new GameObject("PschtGridGhost");
            int layer = ghost.layer;
            root.layer = layer;
            var gt = ghost.transform;

            // Bei LOD-Gruppen nur die feinste Stufe, sonst lägen alle Stufen übereinander
            HashSet<Renderer> lod0 = null;
            var lodGroup = ghost.GetComponentInChildren<LODGroup>();
            if (lodGroup != null)
            {
                var lods = lodGroup.GetLODs();
                if (lods.Length > 0) lod0 = new HashSet<Renderer>(lods[0].renderers);
            }

            foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (lod0 != null && r.GetComponentInParent<LODGroup>() == lodGroup && !lod0.Contains(r)) continue;

                Mesh mesh = null;
                if (r is MeshRenderer) mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                else if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                if (mesh == null) continue;

                var child = new GameObject("r") { layer = layer };
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = gt.InverseTransformPoint(r.transform.position);
                child.transform.localRotation = Quaternion.Inverse(gt.rotation) * r.transform.rotation;
                Vector3 gs = gt.lossyScale, rs = r.transform.lossyScale;
                child.transform.localScale = new Vector3(
                    gs.x != 0 ? rs.x / gs.x : rs.x, gs.y != 0 ? rs.y / gs.y : rs.y, gs.z != 0 ? rs.z / gs.z : rs.z);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.transform.localScale = gt.lossyScale;
            return root;
        }

        internal static void Clear()
        {
            foreach (var g in _ghosts) if (g) Object.Destroy(g);
            _ghosts.Clear();
            _ghostFor = null;
        }

        internal static void UpdateGhosts(Player player, GameObject ghost)
        {
            Piece piece = ghost ? ghost.GetComponent<Piece>() : null;
            Plant plant = GridPlant(piece);
            if (!Active || plant == null || !ghost.activeSelf || Cfg.GridWidth.Value * Cfg.GridLength.Value <= 1)
            {
                if (_ghosts.Count > 0) Clear();
                return;
            }

            var cells = new List<Vector3>(Cells(player, ghost.transform.position, plant));
            if (_ghostFor != ghost.name)
            {
                try { LogDiagnose(player, piece, plant, ghost.transform.position); } catch { }
            }
            if (_ghostFor != ghost.name || _ghosts.Count != cells.Count) Clear();

            while (_ghosts.Count < cells.Count) _ghosts.Add(MakeVisualCopy(ghost));
            _ghostFor = ghost.name;

            for (int i = 0; i < cells.Count; i++)
            {
                var g = _ghosts[i];
                if (!g) continue;
                bool ok = CanPlant(piece, plant, cells[i]);
                g.SetActive(ok);
                if (ok) g.transform.SetPositionAndRotation(cells[i], ghost.transform.rotation);
            }
        }

        // ---------------- Pflanzen ----------------

        internal static void PlaceRest(Player player, Piece piece, GameObject ghost)
        {
            Plant plant = GridPlant(piece);
            if (!Active || plant == null || !ghost || Cfg.GridWidth.Value * Cfg.GridLength.Value <= 1) return;

            bool free = player.PlacementCostDisabled
                        || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()));
            Vector3 origin = ghost.transform.position;
            Quaternion rot = ghost.transform.rotation;
            int placed = 0;
            bool missing = false;

            foreach (var pos in Cells(player, origin, plant))
            {
                if (!CanPlant(piece, plant, pos)) continue;
                // 2 = diese Pflanze + die Hauptpflanze (die zieht das Spiel erst danach ab)
                if (!free && !HaveMaterial(player, piece, 2)) { missing = true; break; }

                Quaternion r = piece.m_randomInitBuildRotation
                    ? Quaternion.Euler(0f, Random.Range(0, 16) * 22.5f, 0f)
                    : rot;
                player.PlacePiece(piece, pos, r, false);
                if (!free) player.ConsumeResources(piece.m_resources, 0);
                placed++;
            }

            if (missing)
                player.Message(MessageHud.MessageType.TopLeft, L.T("Raster: nicht genug Material für alle Pflanzen", "Grid: not enough material for all plants"));
        }

        // ---------------- Tasten ----------------

        internal static void Tick()
        {
            var player = Player.m_localPlayer;
            var ghost = player ? Util.GetField(player, "m_placementGhost") as GameObject : null;
            if (!player || !player.InPlaceMode() || !ghost || !ghost.activeSelf)
            {
                if (_ghosts.Count > 0) Clear();
                return;
            }
            if (!Active || GridPlant(ghost.GetComponent<Piece>()) == null || Util.InputBlocked()) return;

            int w = Cfg.GridWidth.Value, l = Cfg.GridLength.Value;
            if (Cfg.GridWiderKey.Value.IsDown()) w++;
            if (Cfg.GridNarrowerKey.Value.IsDown()) w--;
            if (Cfg.GridLongerKey.Value.IsDown()) l++;
            if (Cfg.GridShorterKey.Value.IsDown()) l--;
            w = Mathf.Clamp(w, 1, 10);
            l = Mathf.Clamp(l, 1, 10);
            if (w == Cfg.GridWidth.Value && l == Cfg.GridLength.Value) return;

            Cfg.GridWidth.Value = w;
            Cfg.GridLength.Value = l;
            player.Message(MessageHud.MessageType.TopLeft, L.T("Raster: ", "Grid: ") + w + L.T(" breit × ", " wide × ") + l + L.T(" lang", " long"));
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class Player_UpdatePlacementGhost_Grid
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            CultivatorGrid.UpdateGhosts(__instance, Util.GetField(__instance, "m_placementGhost") as GameObject);
        }
    }

    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    internal static class Player_TryPlacePiece_Grid
    {
        private static void Postfix(Player __instance, object[] __args, bool __result)
        {
            if (!__result || __instance != Player.m_localPlayer || __args.Length == 0) return;
            CultivatorGrid.PlaceRest(__instance, __args[0] as Piece, Util.GetField(__instance, "m_placementGhost") as GameObject);
        }
    }
}
