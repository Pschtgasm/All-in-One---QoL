using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Schild auf der Kiste: zeigt den Namen (Taste K) und ein Item-Bild (Taste G in der offenen Kiste)
    /// direkt auf einer Seite der Kiste. Taste L wechselt die Seite: vorne → rechts → hinten → links.
    /// Gespeichert wird alles im Kisten-Objekt der Welt, sieht also jeder Spieler.
    /// </summary>
    internal class ChestSign : MonoBehaviour
    {
        internal const string SideKey = "aioqol_side";
        internal const string IconKey = "aioqol_icon";
        internal static string[] SideNames => L.De ? new[] { "vorne", "rechts", "hinten", "links" } : new[] { "front", "right", "back", "left" };

        private ZNetView _nv;
        private Transform _root;
        private float _next;
        private string _shownTag, _shownIcon, _shownStyle;
        private int _shownSide = -1;
        private float _shownScale = -1f;
        private bool _shownEnabled;
        private Bounds _bounds;
        private bool _hasBounds;

        private static TMP_FontAsset _font;

        // Feste Werte (auf Wunsch nicht mehr in der Konfig): Größe 1, fett, dünner Rand,
        // Block (Text/Bild) immer mittig in der Höhe, Bild unter dem Text.
        private const float TextHeight = 0f;
        private const float OutlineWidth = 0.12f;

        private void Start()
        {
            _nv = GetComponentInParent<ZNetView>();
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.5f + Random.value * 0.2f;
            if (_nv == null || !_nv.IsValid()) return;

            var zdo = _nv.GetZDO();
            string tag = zdo.GetString(ChestFeatures.TagKey, "");
            string icon = zdo.GetString(IconKey, "");
            int side = ((zdo.GetInt(SideKey, 0) % 4) + 4) % 4;
            bool enabled = Cfg.ChestSignEnabled.Value;
            const float scale = 1f;   // Größe fest (auf Wunsch nicht mehr einstellbar)

            string style = Cfg.ChestTextColor.Value + "|" + Cfg.ChestOutlineColor.Value;
            if (tag == _shownTag && icon == _shownIcon && side == _shownSide && enabled == _shownEnabled &&
                Mathf.Approximately(scale, _shownScale) && style == _shownStyle)
                return;

            _shownTag = tag; _shownIcon = icon; _shownSide = side; _shownEnabled = enabled; _shownScale = scale; _shownStyle = style;
            Rebuild(tag, icon, side, scale, enabled);
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        // ---------- Aufbau ----------

        private void Rebuild(string tag, string icon, int side, float scale, bool enabled)
        {
            if (_root != null) { Destroy(_root.gameObject); _root = null; }
            if (!enabled || (tag.Length == 0 && icon.Length == 0)) return;

            if (!_hasBounds) { _bounds = LocalBounds(); _hasBounds = true; }

            // Seite: 0 vorne (+Z), 1 rechts (+X), 2 hinten (-Z), 3 links (-X)
            Vector3 normal = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.left;
            float depth = Mathf.Abs(Vector3.Dot(_bounds.extents, normal));
            float faceWidth = (side % 2 == 0) ? _bounds.size.x : _bounds.size.z;
            float faceHeight = _bounds.size.y;

            var rootGo = new GameObject("PschtQoL_ChestSign");
            _root = rootGo.transform;
            _root.SetParent(transform, false);
            _root.localPosition = _bounds.center + normal * (depth + 0.015f) + Vector3.up * (faceHeight * TextHeight);
            _root.localRotation = Quaternion.LookRotation(-normal, Vector3.up); // Vorderseite zeigt nach aussen

            Sprite sprite = icon.Length > 0 ? IconFor(icon) : null;
            bool hasText = tag.Length > 0;
            float sizeScale = Mathf.Clamp(scale, 0.5f, 1.5f);
            TextMeshPro tmp = null;

            if (hasText)
            {
                var textGo = new GameObject("Text");
                textGo.transform.SetParent(_root, false);
                tmp = textGo.AddComponent<TextMeshPro>();
                var font = GetFont();
                if (font != null) tmp.font = font;
                tmp.text = tag;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = true;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.3f;
                tmp.fontSizeMax = 2.4f * scale;
                tmp.color = ParseColor(Cfg.ChestTextColor.Value, new Color(1f, 0.9f, 0.66f));
                tmp.fontStyle = FontStyles.Bold;
                tmp.outlineWidth = OutlineWidth;   // TextMeshPro legt dafür selbst eine eigene Material-Kopie an
                tmp.outlineColor = ParseColor(Cfg.ChestOutlineColor.Value, new Color(0.1f, 0.05f, 0f));
                tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                tmp.lineSpacing = -10f;   // mehrere Zeilen etwas enger
                tmp.rectTransform.sizeDelta = new Vector2(faceWidth * 0.9f, faceHeight * 0.45f * sizeScale);
            }

            // Zeilen zählen: Das Bild rutscht nur dann tiefer, wenn der Text mehrere Zeilen hat
            int lines = 1;
            if (tmp != null)
            {
                try { tmp.ForceMeshUpdate(); lines = Mathf.Max(1, tmp.textInfo.lineCount); } catch { }
            }

            float textH = hasText
                ? faceHeight * (sprite != null ? Mathf.Min(0.16f + 0.1f * (lines - 1), 0.4f) : 0.45f) * sizeScale
                : 0f;
            float iconSize = Mathf.Min(faceHeight, faceWidth) * (hasText ? (lines >= 3 ? 0.24f : 0.3f) : 0.55f) * scale;
            float gap = faceHeight * 0.03f;
            float iconY = 0f, textY = 0f;
            if (sprite != null && hasText)
            {
                float total = textH + gap + iconSize;
                float top = total / 2f, bottom = -total / 2f;
                const bool below = true;   // Bild immer unter dem Text
                textY = below ? top - textH / 2f : bottom + textH / 2f;
                iconY = below ? bottom + iconSize / 2f : top - iconSize / 2f;
            }

            if (tmp != null)
            {
                tmp.rectTransform.sizeDelta = new Vector2(faceWidth * 0.9f, textH);
                tmp.rectTransform.localPosition = new Vector3(0f, textY, 0f);
            }

            if (sprite != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(_root, false);
                iconGo.transform.localPosition = new Vector3(0f, iconY, 0f);
                var sr = iconGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                float spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
                float s = iconSize / spriteHeight;
                iconGo.transform.localScale = new Vector3(s, s, s);
            }
        }

        /// <summary>
        /// Umriss der GESCHLOSSENEN Kiste im eigenen Koordinatensystem (aus den sichtbaren Meshes).
        /// Nicht mitgezählt: der offene Deckel (Container.m_open, ragt nach hinten), ausgeblendete
        /// Varianten (z.B. verschlissen/kaputt) und gröbere LOD-Stufen – sonst verrutscht das Schild
        /// an den Seiten nach hinten und die Fläche wirkt zu breit.
        /// </summary>
        private Bounds LocalBounds()
        {
            var cont = GetComponent<Container>() ?? GetComponentInParent<Container>();
            Transform open = cont != null && cont.m_open != null ? cont.m_open.transform : null;
            Transform closed = cont != null && cont.m_closed != null ? cont.m_closed.transform : null;

            var notLod0 = new System.Collections.Generic.HashSet<Renderer>();
            foreach (var lg in GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                for (int l = 1; l < lods.Length; l++)
                    foreach (var r in lods[l].renderers) if (r != null) notLod0.Add(r);
            }

            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var tf = mf.transform;
                if (_root != null && tf.IsChildOf(_root)) continue;
                if (open != null && tf.IsChildOf(open)) continue;
                bool inClosed = closed != null && tf.IsChildOf(closed);
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || notLod0.Contains(mr)) continue;
                if (!inClosed && (!mf.gameObject.activeInHierarchy || !mr.enabled)) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                    else b.Encapsulate(local);
                }
            }
            return any ? b : new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one);
        }

        /// <summary>Farben für die Auswahlliste im Config-Editor.</summary>
        internal static readonly string[] ColorNames =
        {
            "Hellgelb", "Weiß", "Gold", "Orange", "Rot", "Hellgrün", "Grün", "Hellblau", "Blau",
            "Lila", "Grau", "Holzbraun", "Dunkelbraun", "Schwarz"
        };

        private static readonly string[] ColorHex =
        {
            "#FFE6A8", "#FFFFFF", "#FFC832", "#FF9628", "#E03C32", "#8CF07A", "#32A032", "#8CD2FF", "#3C78DC",
            "#B478E6", "#A0A0A0", "#8C5A2D", "#1A0D00", "#000000"
        };

        internal static Color ColorByName(string value, Color fallback) => ParseColor(value, fallback);

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = value.Trim();
            int i = System.Array.IndexOf(ColorNames, value);
            string hex = i >= 0 ? ColorHex[i] : (value.StartsWith("#") ? value : "#" + value);
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
        }

        internal static Sprite IconFor(string prefabName)
        {
            if (ObjectDB.instance == null || string.IsNullOrEmpty(prefabName)) return null;
            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            try { return drop != null ? drop.m_itemData.GetIcon() : null; } catch { return null; }
        }

        private static TMP_FontAsset GetFont()
        {
            if (_font != null) return _font;
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            _font = all.FirstOrDefault(f => f.name.IndexOf("Averia", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                            f.name.IndexOf("Bold", System.StringComparison.OrdinalIgnoreCase) >= 0)
                 ?? all.FirstOrDefault(f => f.name.IndexOf("Averia", System.StringComparison.OrdinalIgnoreCase) >= 0)
                 ?? all.FirstOrDefault();
            return _font;
        }
    }

    /// <summary>Jede normale Kiste bekommt ein Schild (nicht auf dem Server, nicht auf Grabsteinen, Schiffen, Karren).</summary>
    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class Container_Awake_Sign
    {
        private static void Postfix(Container __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            if (__instance.GetComponent<TombStone>() != null) return;
            if (__instance.GetComponentInParent<Ship>() != null || __instance.GetComponentInParent<Vagon>() != null) return;
            if (__instance.GetComponent<ChestSign>() == null) __instance.gameObject.AddComponent<ChestSign>();
        }
    }
}
