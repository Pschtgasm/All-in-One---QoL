using System;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>Hilfsfunktionen. Felder/Methoden per Name: fehlt etwas in einer Valheim-Version, passiert einfach nichts.</summary>
    internal static class Util
    {
        /// <summary>Ist im Inventar Fertigkeiten, Kompendium, Trophäen oder Erfolge offen?</summary>
        public static bool InventoryDialogOpen()
        {
            var gui = InventoryGui.instance;
            if (gui == null) return false;
            try { if (gui.IsSkillsPanelOpen || gui.IsTextPanelOpen || gui.IsTrophisPanelOpen) return true; } catch { }
            try { if (gui.IsAchievementsPanelOpen) return true; } catch { }
            return false;
        }

        public static bool SetField(object obj, string name, object value)
        {
            if (obj == null) return false;
            var f = AccessTools.Field(obj.GetType(), name);
            if (f == null) return false;
            try
            {
                object v = (value != null && f.FieldType.IsPrimitive && value.GetType() != f.FieldType)
                    ? Convert.ChangeType(value, f.FieldType)
                    : value;
                f.SetValue(obj, v);
                return true;
            }
            catch { return false; }
        }

        public static object GetField(object obj, string name)
        {
            if (obj == null) return null;
            var f = AccessTools.Field(obj.GetType(), name);
            if (f == null) return null;
            try { return f.GetValue(obj); } catch { return null; }
        }

        public static object Call(object obj, string name, params object[] args)
        {
            if (obj == null) return null;
            var m = AccessTools.Method(obj.GetType(), name);
            if (m == null) return null;
            try { return m.Invoke(obj, args); } catch { return null; }
        }

        /// <summary>
        /// Rasterposition eines Inventar-Feldes. Valheim 1.0: InventoryElement.Position (Property),
        /// ältere Versionen: Feld m_pos.
        /// </summary>
        public static bool ElementPos(object element, out Vector2i pos)
        {
            pos = new Vector2i(-1, -1);
            if (element == null) return false;
            var prop = AccessTools.Property(element.GetType(), "Position");
            object v = null;
            if (prop != null) { try { v = prop.GetValue(element, null); } catch { } }
            if (!(v is Vector2i)) v = GetField(element, "m_pos");
            if (v is Vector2i p) { pos = p; return true; }
            return false;
        }

        /// <summary>GameObject eines Inventar-Feldes (Valheim 1.0: das Feld ist selbst eine Komponente, früher: m_go).</summary>
        public static GameObject ElementGo(object element)
        {
            if (element is Component c) return c.gameObject;
            return GetField(element, "m_go") as GameObject;
        }

        /// <summary>Darf der Spieler diese Kiste benutzen? (private Kisten anderer Spieler: nein)</summary>
        public static bool CanAccess(Container c, long playerId)
        {
            if (c == null) return false;
            if (Call(c, "CheckAccess", playerId) is bool ok) return ok;   // falls es die Methode (noch) gibt
            var privacy = GetField(c, "m_privacy");
            if (privacy != null && privacy.ToString() == "Private")
            {
                var piece = c.GetComponentInParent<Piece>();
                if (piece != null && piece.GetCreator() != 0L && piece.GetCreator() != playerId) return false;
            }
            return true;
        }

        public static string PrefabName(GameObject go)
        {
            if (!go) return "";
            string n = go.name;
            int i = n.IndexOf("(Clone)", StringComparison.Ordinal);
            return i >= 0 ? n.Substring(0, i).Trim() : n;
        }

        /// <summary>true, wenn gerade getippt wird (Chat, Konsole, Textfeld) oder ein Menue offen ist.</summary>
        public static bool InputBlocked()
        {
            try
            {
                if (Chat.instance != null && Chat.instance.HasFocus()) return true;
                if (global::Console.IsVisible()) return true;
                if (TextInput.IsVisible()) return true;
                if (Menu.IsVisible()) return true;
                if (Minimap.IsOpen()) return true;
            }
            catch { }
            return false;
        }

        public static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return (int)t.TotalHours + "h " + t.Minutes.ToString("D2") + "m";
            return t.Minutes + "m " + t.Seconds.ToString("D2") + "s";
        }
    }
}
