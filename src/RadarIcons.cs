using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Eigene Kartensymbole für das Radar (in die DLL eingebaute PNGs) und Einteilung der Orte in Gruppen.
    /// </summary>
    internal static class RadarIcons
    {
        internal enum Group { Dungeon, Village, Ruin, TarPit, Runestone, Shipwreck, Other }

        private static readonly Dictionary<Group, Sprite> _sprites = new Dictionary<Group, Sprite>();
        private static readonly HashSet<string> _logged = new HashSet<string>();

        internal static string Label(Group g)
        {
            switch (g)
            {
                case Group.Dungeon: return "Dungeon";
                case Group.Village: return L.T("Dorf/Lager", "Village/camp");
                case Group.Ruin: return L.T("Ruine", "Ruin");
                case Group.TarPit: return L.T("Teergrube", "Tar pit");
                case Group.Runestone: return L.T("Runenstein", "Runestone");
                case Group.Shipwreck: return L.T("Schiffswrack", "Shipwreck");
                default: return null;   // Sonstiges: eigener Name des Orts
            }
        }

        /// <summary>Ort anhand des Prefab-Namens einer Gruppe zuordnen.</summary>
        internal static Group Classify(Location loc)
        {
            string n = Clean(loc.gameObject.name).ToLowerInvariant();
            Group g;
            if (loc.m_hasInterior) g = Group.Dungeon;
            // Boss-Altäre, Händler, Startplatz -> Sonstiges (vor "camp", sonst wäre Hildirs Lager ein Dorf)
            else if (Any(n, "eikthyr", "gdking", "bonemass", "dragonqueen", "goblinking", "seekerqueen", "fader",
                         "vendor", "hildir", "bogwitch", "haldor", "starttemple", "bossentrance")) g = Group.Other;
            else if (Any(n, "shipwreck", "wreck", "shipsetting")) g = Group.Shipwreck;
            else if (n.Contains("tarpit")) g = Group.TarPit;
            else if (n.Contains("runestone")) g = Group.Runestone;
            else if (Any(n, "goblincamp", "village", "camp", "farm", "dvergr", "settlement")) g = Group.Village;
            else if (Any(n, "ruin", "tower", "house", "hut", "henge", "fortress", "castle", "wall", "abandoned",
                         "temple", "dolmen", "grave", "stonecircle", "cabin", "excavation", "harbour", "harbor")) g = Group.Ruin;
            else g = Group.Other;

            // Einmal pro Ort-Name ins Log, damit man sieht, was in welcher Gruppe landet
            string key = Clean(loc.gameObject.name);
            if (_logged.Add(key)) Plugin.LogInfo("Radar: Ort \"" + key + "\" -> " + g);
            return g;
        }

        private static bool Any(string n, params string[] parts)
        {
            foreach (var p in parts) if (n.Contains(p)) return true;
            return false;
        }

        private static string Clean(string name)
        {
            int i = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return (i >= 0 ? name.Substring(0, i) : name).Trim();
        }

        internal static Sprite Get(Group g)
        {
            if (_sprites.TryGetValue(g, out var s)) return s;
            s = Load(FileName(g));
            _sprites[g] = s;   // auch null merken, damit nicht jedes Mal neu versucht wird
            return s;
        }

        private static string FileName(Group g)
        {
            switch (g)
            {
                case Group.Dungeon: return "dungeon";
                case Group.Village: return "village";
                case Group.Ruin: return "ruin";
                case Group.TarPit: return "tarpit";
                case Group.Runestone: return "runestone";
                case Group.Shipwreck: return "shipwreck";
                default: return "other";
            }
        }

        private static Sprite Load(string name)
        {
            try
            {
                var asm = typeof(RadarIcons).Assembly;
                using (var st = asm.GetManifestResourceStream("Pscht_QoL.icons." + name + ".png"))
                {
                    if (st == null) { Plugin.LogWarn("Radar: Symbol " + name + " nicht gefunden"); return null; }
                    var ms = new MemoryStream();
                    st.CopyTo(ms);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(tex, ms.ToArray())) return null;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.name = "PschtQoL_" + name;
                    var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sp.name = tex.name;
                    return sp;
                }
            }
            catch (Exception e) { Plugin.LogWarn("Radar: Symbol " + name + " laden fehlgeschlagen: " + e.Message); return null; }
        }
    }
}
