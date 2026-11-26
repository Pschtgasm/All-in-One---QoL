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
        private static readonly List<bool> _ghostRed = new List<bool>();
        private static readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private static Material _lineMat;
        private static string _info;
        private static bool _infoOk;
        private static readonly Color LineOk = new Color(0.35f, 1f, 0.35f, 0.9f);
        private static readonly Color LineRed = new Color(1f, 0.2f, 0.15f, 0.95f);
        private static readonly Color LineGrey = new Color(0.6f, 0.6f, 0.6f, 0.45f);
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

        /// <summary>
        /// Richtung des Rasters: vom Spieler weg (an den Himmelsrichtungen ausgerichtet), gedreht um die
        /// eigene Raster-Drehung (Mausrad ohne Taste) – das ganze Raster dreht sich um die Hauptpflanze.
        /// </summary>
        private static void Axes(Player player, out Vector3 f, out Vector3 r)
        {
            Vector3 pf = player.transform.forward;
            pf.y = 0f;
            Vector3 snap = Mathf.Abs(pf.x) > Mathf.Abs(pf.z) ? new Vector3(Mathf.Sign(pf.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(pf.z));
            // Erst umschwenken, wenn der Blick deutlich (mehr als 70°) von der bisherigen Richtung abweicht –
            // sonst springt das Raster beim Laufen an der 45°-Grenze ständig hin und her.
            if (_dir == Vector3.zero || Vector3.Angle(pf, _dir) > 70f) _dir = snap;
            f = _dir;
            f = Quaternion.Euler(0f, GridYaw, 0f) * f;
            f.y = 0f;
            f.Normalize();
            r = new Vector3(f.z, 0f, -f.x);
        }

        /// <summary>Alle Rasterpunkte außer dem Hauptpunkt (dort steht die normale Vorschau).</summary>
        private static IEnumerable<Vector3> Cells(Player player, Vector3 origin, Plant plant, bool withMain = false)
        {
            int w = Mathf.Max(1, Cfg.GridWidth.Value), l = Mathf.Max(1, Cfg.GridLength.Value);
            float sp = Spacing(plant);

            Axes(player, out Vector3 f, out Vector3 r);
            int c0 = (w - 1) / 2;

            for (int row = 0; row < l; row++)
                for (int col = 0; col < w; col++)
                {
                    if (row == 0 && col == c0 && !withMain) continue;
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

        /// <summary>Für wie viele Pflanzen das Material reicht (kleinster Wert über alle Rohstoffe).</summary>
        private static int AvailableSets(Player player, Piece piece)
        {
            var inv = player.GetInventory();
            if (inv == null) return 0;
            int sets = int.MaxValue;
            foreach (var req in piece.m_resources)
            {
                if (req == null || !req.m_resItem || req.m_amount <= 0) continue;
                sets = Mathf.Min(sets, inv.CountItems(req.m_resItem.m_itemData.m_shared.m_name) / req.m_amount);
            }
            return sets == int.MaxValue ? 9999 : sets;
        }

        private static bool FreeBuild(Player player, Piece piece) =>
            player.PlacementCostDisabled
            || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()));

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
            _ghostRed.Clear();
            _ghostFor = null;
            ClearLines();
            _info = null;
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

            while (_ghosts.Count < cells.Count) { _ghosts.Add(MakeVisualCopy(ghost)); _ghostRed.Add(false); }
            _ghostFor = ghost.name;

            bool free = FreeBuild(player, piece);
            int sets = free ? int.MaxValue : AvailableSets(player, piece);
            var state = new List<int>(cells.Count);   // 0 = pflanzbar, 1 = zu wenig Samen, 2 = belegt
            int okCount = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                var g = _ghosts[i];
                bool ok = CanPlant(piece, plant, cells[i]);
                // Hauptpflanze braucht 1 Satz, die k-te Rasterpflanze (ab 0) braucht k+2
                bool enough = ok && (free || sets >= okCount + 2);
                if (ok) okCount++;
                state.Add(!ok ? 2 : enough ? 0 : 1);
                if (!g) continue;
                g.SetActive(ok);
                if (!ok) continue;
                g.transform.SetPositionAndRotation(cells[i], ghost.transform.rotation);
                bool red = !enough && Cfg.GridSeedInfo.Value;
                if (_ghostRed[i] != red) { SetRed(g, red); _ghostRed[i] = red; }
            }

            UpdateLines(player, ghost.transform.position, plant, state, free || sets >= 1);
            UpdateInfo(piece, free, okCount + 1);
        }

        private static void SetRed(GameObject g, bool red)
        {
            if (MaterialMan.instance == null) return;
            if (red)
            {
                MaterialMan.instance.SetValue(g, ShaderProps._Color, Color.red);
                MaterialMan.instance.SetValue(g, ShaderProps._EmissionColor, Color.red * 0.7f);
            }
            else
            {
                MaterialMan.instance.ResetValue(g, ShaderProps._Color);
                MaterialMan.instance.ResetValue(g, ShaderProps._EmissionColor);
            }
        }

        // ---------------- Rasterlinien ----------------

        private static void ClearLines()
        {
            foreach (var l in _lines) if (l) Object.Destroy(l.gameObject);
            _lines.Clear();
        }

        private static LineRenderer NewLine()
        {
            if (_lineMat == null)
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                _lineMat = new Material(sh);
            }
            var go = new GameObject("PschtGridLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = 0.04f;
            lr.numCornerVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// <summary>Ein Quadrat pro Rasterplatz, folgt dem Boden. Farbe nach Zustand.</summary>
        private static void UpdateLines(Player player, Vector3 origin, Plant plant, List<int> state, bool mainOk)
        {
            if (!Cfg.GridLines.Value) { if (_lines.Count > 0) ClearLines(); return; }
            var all = new List<Vector3>(Cells(player, origin, plant, true));
            while (_lines.Count < all.Count) _lines.Add(NewLine());
            while (_lines.Count > all.Count)
            {
                var l = _lines[_lines.Count - 1];
                if (l) Object.Destroy(l.gameObject);
                _lines.RemoveAt(_lines.Count - 1);
            }

            float half = Spacing(plant) * 0.5f * 0.92f;
            Axes(player, out Vector3 f, out Vector3 r);
            int c0 = (Mathf.Max(1, Cfg.GridWidth.Value) - 1) / 2;
            const int seg = 4;
            var pts = new Vector3[seg * 4];

            for (int i = 0, k = 0; i < all.Count; i++)
            {
                // Hauptpunkt ist in "all" die Zelle c0 der ersten Reihe, in "state" fehlt er
                bool isMain = i == c0;
                int st = isMain ? (mainOk ? 0 : 1) : (k < state.Count ? state[k++] : 2);
                var lr = _lines[i];
                if (!lr) continue;
                Color col = st == 0 ? LineOk : st == 1 ? LineRed : LineGrey;
                lr.startColor = col;
                lr.endColor = col;

                Vector3 c = all[i];
                Vector3[] corners = { c - f * half - r * half, c - f * half + r * half, c + f * half + r * half, c + f * half - r * half };
                for (int e = 0; e < 4; e++)
                    for (int s = 0; s < seg; s++)
                    {
                        Vector3 p = Vector3.Lerp(corners[e], corners[(e + 1) % 4], s / (float)seg);
                        if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetGroundHeight(p);
                        p.y += 0.06f;
                        pts[e * seg + s] = p;
                    }
                lr.positionCount = pts.Length;
                lr.SetPositions(pts);
            }
        }

        // ---------------- Samen-Anzeige ----------------

        private static void UpdateInfo(Piece piece, bool free, int plants)
        {
            _info = null;
            if (!Cfg.GridSeedInfo.Value || free) return;
            var inv = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
            if (inv == null) return;
            var sb = new System.Text.StringBuilder();
            _infoOk = true;
            foreach (var req in piece.m_resources)
            {
                if (req == null || !req.m_resItem || req.m_amount <= 0) continue;
                string n = req.m_resItem.m_itemData.m_shared.m_name;
                int have = inv.CountItems(n), need = req.m_amount * plants;
                if (have < need) _infoOk = false;
                if (sb.Length > 0) sb.Append("   ");
                string name = Localization.instance != null ? Localization.instance.Localize(n) : n;
                sb.Append(name).Append(": ").Append(have).Append(" / ").Append(need);
            }
            if (sb.Length == 0) return;
            if (!_infoOk) sb.Append(L.T("  – zu wenig!", "  – not enough!"));
            _info = sb.ToString();
        }

        private static GUIStyle _style;

        internal static void OnGUI()
        {
            if (_info == null || _ghosts.Count == 0 || Player.m_localPlayer == null || !Player.m_localPlayer.InPlaceMode()) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _style.fontSize = Mathf.RoundToInt(Screen.height / 40f);
            var rect = new Rect(0, Screen.height * 0.70f, Screen.width, Screen.height * 0.05f);
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), _info, _style);
            _style.normal.textColor = _infoOk ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.4f, 0.35f);
            GUI.Label(rect, _info, _style);
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

        /// <summary>Eigene Drehung des Rasters (Mausrad ohne Taste), startet bei 0.</summary>
        internal static float GridYaw;
        private static Vector3 _dir;   // aktuelle Himmelsrichtung des Rasters

        private static bool WheelHeld(KeyCode k) => k != KeyCode.None && Input.GetKey(k);

        /// <summary>Hält der Spieler eine Mausrad-Taste fürs Raster (und hat eine Rasterpflanze in der Vorschau)?</summary>
        internal static bool WheelModifierActive(Player player)
        {
            if (!Active || player == null) return false;
            var ghost = Util.GetField(player, "m_placementGhost") as GameObject;
            if (!ghost || GridPlant(ghost.GetComponent<Piece>()) == null) return false;
            // mit Taste: Größe ändern; ohne Taste bei mehr als einem Feld: ganzes Raster drehen
            return WheelHeld(Cfg.GridWheelWidthKey.Value) || WheelHeld(Cfg.GridWheelLengthKey.Value)
                   || Cfg.GridWidth.Value * Cfg.GridLength.Value > 1;
        }

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
            // Mausrad: Strg = Breite, Alt = Länge (das Drehen der Pflanze wird dann unterdrückt)
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel != 0f)
            {
                int step = wheel > 0f ? 1 : -1;
                if (WheelHeld(Cfg.GridWheelWidthKey.Value)) w += step;
                else if (WheelHeld(Cfg.GridWheelLengthKey.Value)) l += step;
                else if (w * l > 1) GridYaw = Mathf.Repeat(GridYaw + step * 22.5f, 360f);   // ganzes Raster drehen
            }
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

    /// <summary>Mit gehaltener Raster-Taste dreht das Mausrad nicht die Pflanze, sondern ändert das Raster.</summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    internal static class Player_UpdatePlacement_GridWheel
    {
        private static void Prefix(Player __instance, out int __state)
        {
            __state = int.MinValue;
            if (__instance != Player.m_localPlayer || !CultivatorGrid.WheelModifierActive(__instance)) return;
            if (Util.GetField(__instance, "m_placeRotation") is int r) __state = r;
        }

        private static void Postfix(Player __instance, int __state)
        {
            if (__state != int.MinValue) Util.SetField(__instance, "m_placeRotation", __state);
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
