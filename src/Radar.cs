using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Radar: zeigt Fundstellen in der Nähe als Pins auf Minikarte und großer Karte.
    /// - Dungeon-Eingänge (Standard an), auf Wunsch andere Orte (Ruinen, Lager, Teergruben …)
    /// - Items, die du auswählst: Erzvorkommen, Zerstörbares (Knochenhaufen, Nester …), Pflanzen/Büsche
    /// Auswahl über ein Fenster (Taste, Standard F7). Pins werden NICHT im Spielstand gespeichert.
    /// Eigene Umsetzung für Pscht QoL (Idee wie der Mod "Lookout").
    /// </summary>
    internal static class Radar
    {
        // ---------- Katalog: alles, was die Welt an Items liefern kann ----------

        internal class Entry
        {
            public string Prefab;      // z.B. "CopperOre"
            public string Token;       // z.B. "$item_copperore"
            public string Name;        // übersetzt
            public Sprite Icon;
            public string Kinds;       // "Vorkommen, Pflanze" …
        }

        internal static readonly List<Entry> Catalog = new List<Entry>();
        private static readonly Dictionary<string, Entry> _byPrefab = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static ZNetScene _catalogFor;

        private static void BuildCatalog()
        {
            if (ZNetScene.instance == null || ObjectDB.instance == null || _catalogFor == ZNetScene.instance) return;
            _catalogFor = ZNetScene.instance;
            Catalog.Clear(); _byPrefab.Clear();
            var kinds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            void Add(GameObject item, string kind)
            {
                if (item == null) return;
                if (!kinds.TryGetValue(item.name, out var set)) kinds[item.name] = set = new HashSet<string>();
                set.Add(kind);
            }

            foreach (var go in ZNetScene.instance.m_prefabs)
            {
                if (!go) continue;
                foreach (var d in Drops(go, out string kind)) Add(d, kind);
            }

            foreach (var kv in kinds)
            {
                var drop = ObjectDB.instance.GetItemPrefab(kv.Key)?.GetComponent<ItemDrop>();
                if (drop == null) continue;
                var e = new Entry
                {
                    Prefab = kv.Key,
                    Token = drop.m_itemData.m_shared.m_name,
                    Name = Localization.instance != null ? Localization.instance.Localize(drop.m_itemData.m_shared.m_name) : kv.Key,
                    Kinds = string.Join(", ", kv.Value.OrderBy(k => k))
                };
                try { e.Icon = drop.m_itemData.GetIcon(); } catch { }
                Catalog.Add(e);
                _byPrefab[e.Prefab] = e;
            }
            Catalog.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            Plugin.LogInfo("Radar: " + Catalog.Count + " Items im Katalog");
        }

        /// <summary>Welche Items liefert dieses Objekt (Prefab oder Instanz)?</summary>
        private static IEnumerable<GameObject> Drops(GameObject go, out string kind)
        {
            kind = null;
            var list = new List<GameObject>();
            var mr = go.GetComponent<MineRock>();
            var mr5 = go.GetComponent<MineRock5>();
            if (mr != null) { kind = L.T("Vorkommen", "Deposit"); AddTable(mr.m_dropItems, list); }
            else if (mr5 != null) { kind = L.T("Vorkommen", "Deposit"); AddTable(mr5.m_dropItems, list); }
            else
            {
                var pick = go.GetComponent<Pickable>();
                if (pick != null && go.GetComponent<Plant>() == null)
                {
                    kind = L.T("Pflanze", "Plant");
                    if (pick.m_itemPrefab != null) list.Add(pick.m_itemPrefab);
                    AddTable(pick.m_extraDrops, list);
                }
                else if (go.GetComponent<Destructible>() != null && go.GetComponent<Piece>() == null)
                {
                    var dod = go.GetComponent<DropOnDestroyed>();
                    if (dod != null) { kind = L.T("Zerstörbar", "Destructible"); AddTable(dod.m_dropWhenDestroyed, list); }
                }
            }
            return list;
        }

        private static void AddTable(DropTable t, List<GameObject> into)
        {
            if (t?.m_drops == null) return;
            foreach (var d in t.m_drops) if (d.m_item != null) into.Add(d.m_item);
        }

        // ---------- Auswahl ----------

        private static HashSet<string> _wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _wantedFrom;

        internal static HashSet<string> Wanted()
        {
            string raw = Cfg.RadarItems.Value ?? "";
            if (raw != _wantedFrom)
            {
                _wantedFrom = raw;
                _wanted = new HashSet<string>(raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => s.Length > 0), StringComparer.OrdinalIgnoreCase);
            }
            return _wanted;
        }

        internal static void SetWanted(string prefab, bool on)
        {
            var set = new HashSet<string>(Wanted(), StringComparer.OrdinalIgnoreCase);
            if (on) set.Add(prefab); else set.Remove(prefab);
            Cfg.RadarItems.Value = string.Join(",", set.OrderBy(s => s));
            _next = 0f;   // sofort neu scannen
        }

        private static bool Discovered(Entry e)
        {
            if (!Cfg.RadarOnlyDiscovered.Value) return true;
            var p = Player.m_localPlayer;
            return p != null && (p.IsKnownMaterial(e.Token) || p.IsMaterialKnown(e.Token));
        }

        // ---------- Scannen + Pins ----------

        private struct Hit { public Vector3 Pos; public string Label; public Sprite Icon; public Minimap.PinType Type; }

        private static readonly Dictionary<int, Minimap.PinData> _pins = new Dictionary<int, Minimap.PinData>();
        private static Minimap _pinsFor;
        internal static readonly Dictionary<string, int> NearbyCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static float _next;
        private static string _look;
        private static FieldInfo _fLocations;
        private static MethodInfo _mIsExplored, _mAddPin;

        internal static void Tick()
        {
            HandleInput();
            if (Minimap.instance != _pinsFor) { _pins.Clear(); _pinsFor = Minimap.instance; }
            var player = Player.m_localPlayer;
            if (!Cfg.RadarEnabled.Value || player == null || Minimap.instance == null) { ClearPins(); return; }
            // Namen/Größe geändert -> alle Pins neu
            string look = Cfg.RadarShowLabels.Value + "|" + Cfg.RadarLargeIcons.Value;
            if (look != _look) { _look = look; ClearPins(); _next = 0f; }

            if (Time.time < _next) return;
            _next = Time.time + Mathf.Max(0.25f, Cfg.RadarRefreshSeconds.Value);

            BuildCatalog();
            var hits = new Dictionary<int, Hit>();
            Vector3 me = player.transform.position;
            float maxDist = Cfg.RadarMaxDistance.Value;
            bool InRange(Vector3 p) => maxDist <= 0f || Vector3.Distance(me, p) <= maxDist;

            // Orte (Dungeons, andere)
            if (Cfg.RadarDungeons.Value || Cfg.RadarOtherLocations.Value)
            {
                if (_fLocations == null) _fLocations = AccessTools.Field(typeof(Location), "s_allLocations");
                if (_fLocations?.GetValue(null) is System.Collections.IList locs)
                    foreach (var o in locs)
                    {
                        var loc = o as Location;
                        if (loc == null) continue;
                        bool dungeon = loc.m_hasInterior;
                        if (dungeon ? !Cfg.RadarDungeons.Value : !Cfg.RadarOtherLocations.Value) continue;
                        var pos = loc.transform.position;
                        if (!InRange(pos) || !ExploredOk(pos)) continue;
                        var group = RadarIcons.Classify(loc);
                        hits[loc.GetInstanceID()] = new Hit
                        {
                            Pos = pos,
                            Label = RadarIcons.Label(group) ?? PrettyName(loc.gameObject.name),
                            Icon = RadarIcons.Get(group),   // eigenes Symbol (null = Valheim-Symbol)
                            Type = dungeon ? Minimap.PinType.Icon3 : Minimap.PinType.Icon4
                        };
                    }
            }

            // Items
            NearbyCount.Clear();
            var wanted = Wanted();
            ScanType<MineRock>(hits, wanted, InRange, true);
            ScanType<MineRock5>(hits, wanted, InRange, true);
            ScanType<Pickable>(hits, wanted, InRange, false);
            ScanType<Destructible>(hits, wanted, InRange, false);

            // Pins abgleichen
            foreach (var id in _pins.Keys.ToList())
                if (!hits.ContainsKey(id)) { RemovePin(_pins[id]); _pins.Remove(id); }
            foreach (var kv in hits)
            {
                if (_pins.ContainsKey(kv.Key)) continue;
                var pin = AddPin(kv.Value);
                if (pin != null) _pins[kv.Key] = pin;
            }
        }

        private static void ScanType<T>(Dictionary<int, Hit> hits, HashSet<string> wanted, Func<Vector3, bool> inRange, bool ore)
            where T : Component
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (c == null) continue;
                var go = c.gameObject;
                if (c is Pickable pk && Util.GetField(pk, "m_picked") is bool picked && picked) continue;
                var drops = Drops(go, out string kind);
                Entry match = null;
                foreach (var d in drops)
                {
                    if (!_byPrefab.TryGetValue(d.name, out var e) || !Discovered(e)) continue;
                    NearbyCount[e.Prefab] = NearbyCount.TryGetValue(e.Prefab, out int n) ? n + 1 : 1;
                    if (match == null && wanted.Contains(e.Prefab)) match = e;
                }
                if (match == null) continue;
                var pos = go.transform.position;
                if (!inRange(pos) || !ExploredOk(pos)) continue;
                if (ore && !Cfg.RadarShowBuried.Value && IsBuried(go)) continue;
                hits[go.GetInstanceID()] = new Hit { Pos = pos, Label = match.Name, Icon = match.Icon, Type = Minimap.PinType.Icon3 };
            }
        }

        /// <summary>Komplett unter der Erde? (oberster Punkt des Vorkommens liegt unter dem Boden)</summary>
        private static bool IsBuried(GameObject go)
        {
            try
            {
                float top = float.MinValue;
                foreach (var col in go.GetComponentsInChildren<Collider>())
                    if (col != null && col.enabled) top = Mathf.Max(top, col.bounds.max.y);
                if (top == float.MinValue || ZoneSystem.instance == null) return false;
                return top < ZoneSystem.instance.GetGroundHeight(go.transform.position) - 0.1f;
            }
            catch { return false; }
        }

        private static bool ExploredOk(Vector3 pos)
        {
            if (!Cfg.RadarOnlyExplored.Value || Minimap.instance == null) return true;
            if (_mIsExplored == null) _mIsExplored = AccessTools.Method(typeof(Minimap), "IsExplored", new[] { typeof(Vector3) });
            try { return _mIsExplored == null || (bool)_mIsExplored.Invoke(Minimap.instance, new object[] { pos }); }
            catch { return true; }
        }

        private static Minimap.PinData AddPin(Hit h)
        {
            try
            {
                if (_mAddPin == null) _mAddPin = AccessTools.Method(typeof(Minimap), "AddPin");
                var ps = _mAddPin.GetParameters();
                var args = new object[ps.Length];
                args[0] = h.Pos; args[1] = h.Type; args[2] = Cfg.RadarShowLabels.Value ? h.Label : "";
                args[3] = false; args[4] = false;   // nicht speichern, nicht abgehakt
                for (int i = 5; i < ps.Length; i++)
                    args[i] = ps[i].HasDefaultValue && ps[i].DefaultValue != null ? ps[i].DefaultValue
                            : ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
                var pin = _mAddPin.Invoke(Minimap.instance, args) as Minimap.PinData;
                if (pin != null)
                {
                    if (h.Icon != null) pin.m_icon = h.Icon;
                    pin.m_doubleSize = Cfg.RadarLargeIcons.Value;
                }
                return pin;
            }
            catch (Exception e) { Plugin.LogWarn("Radar-Pin: " + e.Message); return null; }
        }

        private static void RemovePin(Minimap.PinData pin)
        {
            try { if (Minimap.instance != null && pin != null) Minimap.instance.RemovePin(pin); } catch { }
        }

        internal static void ClearPins()
        {
            foreach (var p in _pins.Values) RemovePin(p);
            _pins.Clear();
        }

        /// <summary>Alle Pins neu setzen (z.B. nach Ändern von Namen/Größe).</summary>
        internal static void Refresh() { ClearPins(); _next = 0f; }

        private static string PrettyName(string prefab)
        {
            string n = Utils.GetPrefabName(prefab);
            return n.Replace("_", " ");
        }

        // ---------- Fenster ----------

        internal static bool PanelOpen;
        private static Vector2 _scroll;
        private static string _filter = "";
        private static Rect _win = new Rect(80, 80, 520, 640);

        private static void HandleInput()
        {
            if (!Cfg.RadarEnabled.Value) { PanelOpen = false; return; }
            if (Player.m_localPlayer == null) { PanelOpen = false; return; }
            if (Cfg.RadarKey.Value.IsDown() && (PanelOpen || !Util.InputBlocked()))
            {
                PanelOpen = !PanelOpen;
                if (PanelOpen) BuildCatalog();
            }
            if (PanelOpen && Input.GetKeyDown(KeyCode.Escape)) PanelOpen = false;
        }

        internal static void OnGUI()
        {
            if (!PanelOpen) return;
            try { ZCursor.LockState = CursorLockMode.None; ZCursor.Show(); } catch { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            _win = GUILayout.Window(0x5053524, _win, DrawWindow, L.T("Radar – was soll auf die Karte?", "Radar – what should go on the map?"));
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            Toggle(Cfg.RadarDungeons, "Dungeons");
            Toggle(Cfg.RadarOtherLocations, L.T("Andere Orte", "Other places"));
            Toggle(Cfg.RadarShowBuried, L.T("Vergrabene Erze", "Buried ores"));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Toggle(Cfg.RadarOnlyDiscovered, L.T("Nur entdeckte Items", "Only discovered items"));
            Toggle(Cfg.RadarOnlyExplored, L.T("Nur erkundete Karte", "Only explored map"));
            Toggle(Cfg.RadarLargeIcons, L.T("Große Symbole", "Large icons"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(45));
            _filter = GUILayout.TextField(_filter ?? "");
            if (GUILayout.Button(L.T("Alle aus", "All off"), GUILayout.Width(80))) { Cfg.RadarItems.Value = ""; _next = 0f; }
            GUILayout.EndHorizontal();

            var wanted = Wanted();
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var e in Catalog)
            {
                if (!Discovered(e)) continue;
                if (_filter.Length > 0 && e.Name.IndexOf(_filter, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                bool on = wanted.Contains(e.Prefab);
                NearbyCount.TryGetValue(e.Prefab, out int n);
                GUILayout.BeginHorizontal();
                DrawIcon(e.Icon);
                bool now = GUILayout.Toggle(on, " " + e.Name + "   (" + e.Kinds + ")" + (n > 0 ? "   – " + n + L.T(" in der Nähe", " nearby") : ""));
                GUILayout.EndHorizontal();
                if (now != on) SetWanted(e.Prefab, now);
            }
            GUILayout.EndScrollView();

            GUILayout.Label(L.T("Taste ", "Key ") + Cfg.RadarKey.Value + L.T(" oder Esc schließt das Fenster. Pins werden nicht gespeichert.", " or Esc closes the window. Pins are not saved."));
            GUI.DragWindow();
        }

        /// <summary>Nur den Ausschnitt des Item-Bilds zeichnen (Bilder liegen oft in einer Sammelgrafik).</summary>
        private static void DrawIcon(Sprite s)
        {
            var r = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
            if (s == null || s.texture == null) return;
            var tr = s.textureRect;
            var uv = new Rect(tr.x / s.texture.width, tr.y / s.texture.height, tr.width / s.texture.width, tr.height / s.texture.height);
            GUI.DrawTextureWithTexCoords(r, s.texture, uv);
        }

        private static void Toggle(BepInEx.Configuration.ConfigEntry<bool> e, string label)
        {
            bool now = GUILayout.Toggle(e.Value, " " + label);
            if (now != e.Value) { e.Value = now; _next = 0f; }
        }

        /// <summary>Schalter, der nur im Fenster eingestellt und lokal gespeichert wird.</summary>
        private static void Toggle(Stored<bool> e, string label)
        {
            bool now = GUILayout.Toggle(e.Value, " " + label);
            if (now != e.Value) { e.Value = now; _next = 0f; }
        }
    }
}
