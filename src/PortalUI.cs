using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Pscht_QoL
{
    /// <summary>Zeigt in der Portal-Auswahlliste den runden Punkt, solange die Maus über dem Eintrag ist.</summary>
    internal class PortalHoverDot : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public GameObject Dot;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (Dot) Dot.SetActive(true); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (Dot) Dot.SetActive(false); }
        private void OnDisable() { if (Dot) Dot.SetActive(false); }
    }

    /// <summary>
    /// Portal-Fenster im Valheim-Look (Holzpanel), gebaut mit Jotunn:
    ///
    ///   Sei gegruesst, Reisender!
    ///   Name            [ Home                    ]
    ///   Ziel            [ (Keins)               v ]
    ///   Standard-Portal [x]
    ///                          [Abbrechen] [ OK ]
    ///
    /// Ist Jotunn nicht installiert oder passt dessen Version nicht, wird automatisch
    /// das einfache Fenster (PortalMenu) benutzt.
    /// </summary>
    internal static class PortalUI
    {
        private static bool _unavailable;

        private static GameObject _panel;
        private static GameObject _nameGo, _dropGo;
        private static Toggle _toggle;

        private static TeleportWorld _portal;
        private static bool _wasDefault;
        private static List<PortalSystem.Entry> _local = new List<PortalSystem.Entry>();
        private static List<PortalSystem.Entry> _server;
        private static readonly List<PortalSystem.Entry> _options = new List<PortalSystem.Entry>(); // [0] = null = (Keins)

        internal static bool IsOpen => _panel != null && _panel.activeSelf;

        // ---------- Oeffnen / Schließen ----------

        internal static bool TryOpen(TeleportWorld portal)
        {
            if (_unavailable || !Cfg.PortalValheimStyle.Value) return false;
            try
            {
                return OpenInternal(portal);
            }
            catch (Exception e)
            {
                _unavailable = true;
                Plugin.LogWarn("Valheim-Portalfenster nicht verfügbar (Jotunn fehlt oder passt nicht) - nutze einfaches Fenster. " + e.Message);
                try { if (_panel != null) UnityEngine.Object.Destroy(_panel); } catch { }
                _panel = null;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool OpenInternal(TeleportWorld portal)
        {
            if (GUIManager.CustomGUIFront == null) return false;   // GUI noch nicht bereit
            if (_panel == null) Build();

            _portal = portal;
            _local = PortalSystem.ScanLocal();
            _server = null;
            _wasDefault = PortalSystem.IsDefault(portal);

            SetInputText(portal.GetText() ?? "");
            _toggle.isOn = _wasDefault;
            FillDropdown();

            _panel.SetActive(true);
            GUIManager.BlockInput(true);
            PortalSystem.RequestFromServer();
            return true;
        }

        internal static void Close()
        {
            if (_panel == null) return;
            _panel.SetActive(false);
            _portal = null;
            try { Unblock(); } catch { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Unblock() => GUIManager.BlockInput(false);

        internal static void Update()
        {
            if (!IsOpen) return;
            // Mauszeiger sichtbar und frei halten (Valheim 1.0 blendet ihn sonst aus)
            try { ZCursor.LockState = CursorLockMode.None; ZCursor.Show(); }
            catch { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            var p = Player.m_localPlayer;
            if (_portal == null || p == null || p.IsDead() ||
                Vector3.Distance(p.transform.position, _portal.transform.position) > Cfg.PortalMenuCloseDistance.Value ||
                Input.GetKeyDown(KeyCode.Escape))
                Close();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                Save();
        }

        internal static void OnServerList(List<PortalSystem.Entry> list)
        {
            _server = list;
            if (IsOpen) FillDropdown();
        }

        // ---------- Aufbau ----------

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Build()
        {
            var gui = GUIManager.Instance;
            var c = new Vector2(0.5f, 0.5f);
            Transform root = GUIManager.CustomGUIFront.transform;
            Color orange = gui.ValheimOrange;

            _panel = gui.CreateWoodpanel(root, c, c, Vector2.zero, 800f, 330f, false);
            _panel.name = "Pscht_QoL_PortalPanel";

            // Ueberschrift
            var title = gui.CreateText(L.T("Sei gegrüßt, Reisender!", "Greetings, traveler!"), _panel.transform, c, c, new Vector2(0f, 120f),
                gui.AveriaSerifBold, 32, orange, true, Color.black, 760f, 50f, false);
            SetAlignment(title, TextAnchor.MiddleCenter);

            // Zeile 1: Name
            Label(gui, "Name", 55f, orange);
            _nameGo = gui.CreateInputField(_panel.transform, c, c, new Vector2(95f, 55f),
                InputField.ContentType.Standard, "Name", 18, 560f, 32f);
            SetProp(FindComp(_nameGo, "InputField"), "characterLimit", Cfg.PortalNameLength.Value);

            // Zeile 2: Ziel
            Label(gui, L.T("Ziel", "Target"), 0f, orange);
            _dropGo = gui.CreateDropDown(_panel.transform, c, c, new Vector2(20f, 0f), 18, 410f, 32f);
            StyleDropdown(_dropGo, orange);
            var ping = gui.CreateButton(L.T("Ziel pingen", "Ping target"), _panel.transform, c, c, new Vector2(315f, 0f), 150f, 36f);
            ping.GetComponent<Button>().onClick.AddListener(PingTarget);

            // Zeile 3: Standard-Portal
            Label(gui, L.T("Standard-Portal", "Default portal"), -55f, orange);
            var toggleGo = gui.CreateToggle(_panel.transform, 30f, 30f);
            var trt = (RectTransform)toggleGo.transform;
            trt.anchorMin = c;
            trt.anchorMax = c;
            trt.anchoredPosition = new Vector2(-170f, -55f);
            _toggle = toggleGo.GetComponent<Toggle>();

            // Buttons
            var cancel = gui.CreateButton(L.T("Abbrechen", "Cancel"), _panel.transform, c, c, new Vector2(185f, -125f), 130f, 45f);
            cancel.GetComponent<Button>().onClick.AddListener(Close);
            var ok = gui.CreateButton("OK", _panel.transform, c, c, new Vector2(320f, -125f), 120f, 45f);
            ok.GetComponent<Button>().onClick.AddListener(Save);

            _panel.SetActive(false);
        }

        /// <summary>
        /// Auswahlliste besser treffbar: höhere Einträge, größere Schrift, Eintrag unter der Maus
        /// orange hinterlegt mit Rahmen.
        /// </summary>
        private static void StyleDropdown(GameObject dropGo, Color accent)
        {
            try
            {
                var dd = dropGo != null ? dropGo.GetComponentInChildren<Dropdown>(true) : null;
                if (dd == null || dd.template == null) return;

                // Größe wie vorher lassen, nur den Eintrag unter der Maus hell umrahmen
                var item = dd.template.GetComponentInChildren<Toggle>(true);
                if (item != null)
                {
                    var bg = item.targetGraphic as Graphic;
                    if (bg == null)
                    {
                        bg = item.GetComponentInChildren<Image>(true);
                        item.targetGraphic = bg;
                    }
                    if (bg != null)
                    {
                        var outline = bg.GetComponent<Outline>() ?? bg.gameObject.AddComponent<Outline>();
                        outline.effectColor = new Color(1f, 0.95f, 0.8f, 1f);
                        outline.effectDistance = new Vector2(2f, -2f);
                        outline.useGraphicAlpha = true;   // Rahmen nur sichtbar, wenn der Eintrag hervorgehoben ist
                    }

                    // Runder Punkt links (wie beim gewählten Eintrag) schon beim Drüberfahren zeigen
                    var check = item.graphic as Graphic;
                    if (check != null && item.GetComponent<PortalHoverDot>() == null)
                    {
                        var dot = UnityEngine.Object.Instantiate(check.gameObject, check.transform.parent);
                        dot.name = "PschtQoL_HoverDot";
                        var g = dot.GetComponent<Graphic>();
                        if (g != null) { g.color = new Color(1f, 0.95f, 0.8f, 0.75f); g.raycastTarget = false; }
                        dot.SetActive(false);
                        item.gameObject.AddComponent<PortalHoverDot>().Dot = dot;
                    }

                    // Hell hinterlegtes Kästchen unter der Maus, sonst unsichtbar
                    var colors = item.colors;
                    colors.normalColor = new Color(1f, 1f, 1f, 0f);
                    colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 0.35f);
                    colors.selectedColor = new Color(1f, 0.95f, 0.8f, 0.2f);
                    colors.pressedColor = new Color(accent.r, accent.g, accent.b, 0.6f);
                    colors.fadeDuration = 0.05f;
                    item.colors = colors;
                }
            }
            catch (Exception e)
            {
                Plugin.LogWarn("Portal-Auswahlliste: " + e.Message);
            }
        }

        private static void Label(GUIManager gui, string text, float y, Color color)
        {
            var c = new Vector2(0.5f, 0.5f);
            var go = gui.CreateText(text, _panel.transform, c, c, new Vector2(-260f, y),
                gui.AveriaSerifBold, 18, color, true, Color.black, 220f, 32f, false);
            SetAlignment(go, TextAnchor.MiddleLeft);
        }

        // ---------- Dropdown ----------

        private static List<PortalSystem.Entry> Entries()
        {
            var all = new List<PortalSystem.Entry>();
            if (_server != null) all.AddRange(_server);
            foreach (var l in _local)
                if (!all.Any(a => Vector3.Distance(a.Pos, l.Pos) < 1f)) all.Add(l);

            Vector3 own = _portal.transform.position;
            return all.Where(e => Vector3.Distance(e.Pos, own) > 1f)
                      .OrderBy(e => string.IsNullOrEmpty(e.Name) ? 1 : 0)
                      .ThenBy(e => e.Name)
                      .ToList();
        }

        private static void FillDropdown()
        {
            if (_portal == null) return;
            var dd = FindComp(_dropGo, "Dropdown");
            if (dd == null) return;

            // Aktuelle Auswahl merken: erst das, was gerade im Dropdown steht, sonst das gespeicherte Ziel
            PortalSystem.Entry selected = null;
            int cur = GetProp(dd, "value") is int v ? v : 0;
            if (IsOpen && cur > 0 && cur < _options.Count) selected = _options[cur];
            else if (!IsOpen || _options.Count == 0) selected = PortalSystem.GetTarget(_portal);

            var entries = Entries();
            _options.Clear();
            _options.Add(null);
            _options.AddRange(entries);

            var names = new List<string> { "(Keins)" };
            Vector3 own = _portal.transform.position;
            foreach (var e in entries)
            {
                string n = PortalSystem.Safe(e.Name);
                bool duplicate = entries.Count(x => PortalSystem.Safe(x.Name) == n) > 1;
                names.Add(duplicate ? n + "  (" + Mathf.RoundToInt(Vector3.Distance(own, e.Pos)) + " m)" : n);
            }

            Call(dd, "ClearOptions");
            var add = dd.GetType().GetMethod("AddOptions", new[] { typeof(List<string>) });
            add?.Invoke(dd, new object[] { names });

            int index = 0;
            if (selected != null)
            {
                int found = _options.FindIndex(o => o != null && Vector3.Distance(o.Pos, selected.Pos) < 1f);
                if (found < 0)
                {
                    // Ziel ist (noch) nicht in der Liste - trotzdem anzeigen
                    _options.Add(selected);
                    add?.Invoke(dd, new object[] { new List<string> { PortalSystem.Safe(selected.Name) } });
                    found = _options.Count - 1;
                }
                index = found;
            }
            SetProp(dd, "value", index);
            Call(dd, "RefreshShownValue");
        }

        // ---------- Ziel pingen ----------

        /// <summary>Gewähltes Ziel auf der großen Karte zeigen und dort einen Ping setzen.</summary>
        private static void PingTarget()
        {
            var dd = FindComp(_dropGo, "Dropdown");
            int idx = dd != null && GetProp(dd, "value") is int v ? v : 0;
            var target = idx > 0 && idx < _options.Count ? _options[idx] : null;
            if (target == null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, L.T("Erst ein Ziel auswählen", "Choose a target first"));
                return;
            }

            Close();   // Fenster zu, sonst liegt es über der Karte (nicht gespeicherte Änderungen gehen verloren)
            try { Minimap.instance?.ShowPointOnMap(target.Pos); } catch { }
            try { Chat.instance?.SendPing(target.Pos); } catch { }
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Ping: " + PortalSystem.Safe(target.Name));
        }

        // ---------- Speichern ----------

        private static void Save()
        {
            var portal = _portal;
            if (portal == null) { Close(); return; }

            // 1. Name
            string newName = (GetInputText() ?? "").Trim();
            if (newName != (portal.GetText() ?? "")) portal.SetText(newName);

            // 2. Ziel
            var dd = FindComp(_dropGo, "Dropdown");
            int idx = dd != null && GetProp(dd, "value") is int v ? v : 0;
            PortalSystem.SetTarget(portal, idx > 0 && idx < _options.Count ? _options[idx] : null);

            // 3. Standard-Portal
            bool isDefault = _toggle != null && _toggle.isOn;
            if (isDefault != _wasDefault)
                PortalSystem.SendSetDefault(isDefault ? portal.transform.position : Vector3.zero);

            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, L.T("Portal gespeichert", "Portal saved"));
            Close();
        }

        // ---------- Reflection-Hilfen (funktionieren mit altem UI und TextMeshPro) ----------

        private static Component FindComp(GameObject go, string typeNamePart)
        {
            if (go == null) return null;
            return go.GetComponentsInChildren<Component>(true)
                     .FirstOrDefault(c => c != null && c.GetType().Name.Contains(typeNamePart));
        }

        private static object GetProp(object o, string name)
        {
            if (o == null) return null;
            try { return o.GetType().GetProperty(name)?.GetValue(o, null); } catch { return null; }
        }

        private static void SetProp(object o, string name, object value)
        {
            if (o == null) return;
            try
            {
                var p = o.GetType().GetProperty(name);
                if (p != null && p.CanWrite && (value == null || p.PropertyType.IsInstanceOfType(value))) p.SetValue(o, value, null);
            }
            catch { }
        }

        private static void Call(object o, string method)
        {
            if (o == null) return;
            try { o.GetType().GetMethod(method, Type.EmptyTypes)?.Invoke(o, null); } catch { }
        }

        private static void SetAlignment(GameObject go, TextAnchor anchor)
        {
            var text = go != null ? go.GetComponent<Text>() : null;
            if (text != null) text.alignment = anchor;
        }

        private static string GetInputText() => GetProp(FindComp(_nameGo, "InputField"), "text") as string;
        private static void SetInputText(string s) => SetProp(FindComp(_nameGo, "InputField"), "text", s);
    }
}
