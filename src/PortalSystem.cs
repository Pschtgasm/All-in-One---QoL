using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Portale mit Namen, frei waehlbarem Ziel und einem Standard-Ziel für neue Portale.
    /// E am Portal -> Fenster mit drei Kaestchen:
    ///   1. Name des Portals
    ///   2. Ziel (Dropdown mit allen anderen Portalen)
    ///   3. Standard-Ziel (Dropdown) - neu gebaute Portale verbinden sich automatisch damit
    /// Die Portal-Liste kommt vom Server (nur der kennt alle Portale der Welt).
    /// </summary>
    internal static class PortalSystem
    {
        internal const string RpcRequest = "DQoL_PortalRequest";
        internal const string RpcList = "DQoL_PortalList";
        internal const string RpcSetDefault = "DQoL_PortalSetDefault";
        internal const string KeyTargetName = "dqol_tname";
        internal const string KeyTargetPos = "dqol_tpos";
        internal const string KeyTargetRot = "dqol_trot";
        internal const string KeyIsDefault = "dqol_default";

        internal class Entry
        {
            public string Name;
            public Vector3 Pos;
            public Quaternion Rot;
            public bool IsDefault;
        }

        /// <summary>Neu gebautes Portal, das auf die Server-Liste wartet, um das Standard-Ziel zu bekommen.</summary>
        internal static TeleportWorld PendingNewPortal;

        private static HashSet<int> _portalHashes;

        private static HashSet<int> PortalHashes()
        {
            if (_portalHashes != null && _portalHashes.Count > 0) return _portalHashes;
            _portalHashes = new HashSet<int>();
            if (ZNetScene.instance != null && ZNetScene.instance.m_prefabs != null)
                foreach (var go in ZNetScene.instance.m_prefabs)
                    if (go && go.GetComponent<TeleportWorld>() != null)
                        _portalHashes.Add(go.name.GetStableHashCode());
            return _portalHashes;
        }

        private static IEnumerable<ZDO> AllPortalZdos()
        {
            if (ZDOMan.instance == null) yield break;
            var dict = Util.GetField(ZDOMan.instance, "m_objectsByID") as IDictionary;
            if (dict == null) yield break;
            var hashes = PortalHashes();
            foreach (var v in dict.Values)
            {
                var zdo = v as ZDO;
                if (zdo != null && hashes.Contains(zdo.GetPrefab())) yield return zdo;
            }
        }

        /// <summary>Alle Portale, die diese Instanz (Server oder Client) kennt.</summary>
        internal static List<Entry> ScanLocal()
        {
            return AllPortalZdos().Select(z => new Entry
            {
                Name = z.GetString("tag", ""),
                Pos = z.GetPosition(),
                Rot = z.GetRotation(),
                IsDefault = z.GetBool(KeyIsDefault, false)
            }).ToList();
        }

        // ---------- Netzwerk ----------

        internal static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(RpcRequest, OnRequest);
            ZRoutedRpc.instance.Register<ZPackage>(RpcList, OnList);
            ZRoutedRpc.instance.Register<ZPackage>(RpcSetDefault, OnSetDefault);
        }

        internal static void RequestFromServer()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcRequest, new ZPackage());
        }

        /// <summary>Standard-Ziel setzen. pos = Vector3.zero entfernt das Standard-Ziel.</summary>
        internal static void SendSetDefault(Vector3 pos)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(pos);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcSetDefault, pkg);
        }

        // Server: Liste schicken
        private static void OnRequest(long sender, ZPackage _)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var list = ScanLocal();
            var pkg = new ZPackage();
            pkg.Write(list.Count);
            foreach (var e in list)
            {
                pkg.Write(e.Name ?? "");
                pkg.Write(e.Pos);
                pkg.Write(e.Rot);
                pkg.Write(e.IsDefault);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcList, pkg);
        }

        // Server: Standard-Ziel markieren (genau ein Portal)
        private static void OnSetDefault(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            Vector3 pos = pkg.ReadVector3();
            bool clear = pos == Vector3.zero;
            foreach (var zdo in AllPortalZdos().ToList())
            {
                bool shouldBe = !clear && Vector3.Distance(zdo.GetPosition(), pos) < 1f;
                if (zdo.GetBool(KeyIsDefault, false) != shouldBe) zdo.Set(KeyIsDefault, shouldBe);
            }
        }

        // Spieler: Liste angekommen
        private static void OnList(long sender, ZPackage pkg)
        {
            var list = new List<Entry>();
            int n = pkg.ReadInt();
            for (int i = 0; i < n; i++)
                list.Add(new Entry
                {
                    Name = pkg.ReadString(),
                    Pos = pkg.ReadVector3(),
                    Rot = pkg.ReadQuaternion(),
                    IsDefault = pkg.ReadBool()
                });

            PortalMenu.SetServerList(list);
            PortalUI.OnServerList(list);
            ApplyDefaultToPending(list);
        }

        private static void ApplyDefaultToPending(List<Entry> list)
        {
            var portal = PendingNewPortal;
            PendingNewPortal = null;
            if (portal == null) return;
            var def = list.FirstOrDefault(e => e.IsDefault && Vector3.Distance(e.Pos, portal.transform.position) > 1f);
            if (def == null) return;
            SetTarget(portal, def);
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, L.T("Neues Portal verbunden mit: ", "New portal connected to: ") + Safe(def.Name));
        }

        // ---------- Ziel im Portal ----------

        internal static ZDO GetZdo(TeleportWorld portal)
        {
            var nv = portal ? portal.GetComponent<ZNetView>() : null;
            return (nv != null && nv.IsValid()) ? nv.GetZDO() : null;
        }

        internal static bool IsDefault(TeleportWorld portal)
        {
            var zdo = GetZdo(portal);
            return zdo != null && zdo.GetBool(KeyIsDefault, false);
        }

        internal static string GetTargetName(TeleportWorld portal)
        {
            var zdo = GetZdo(portal);
            return zdo != null ? zdo.GetString(KeyTargetName, "") : "";
        }

        internal static Entry GetTarget(TeleportWorld portal)
        {
            var zdo = GetZdo(portal);
            if (zdo == null) return null;
            string name = zdo.GetString(KeyTargetName, "");
            if (name.Length == 0) return null;
            return new Entry
            {
                Name = name,
                Pos = zdo.GetVec3(KeyTargetPos, Vector3.zero),
                Rot = zdo.GetQuaternion(KeyTargetRot, Quaternion.identity)
            };
        }

        /// <summary>e = null -> normal verbinden (Portal mit gleichem Namen).</summary>
        internal static void SetTarget(TeleportWorld portal, Entry e)
        {
            var nv = portal ? portal.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid()) return;
            if (!nv.IsOwner()) nv.ClaimOwnership();
            var zdo = nv.GetZDO();
            if (e == null)
            {
                zdo.Set(KeyTargetName, "");
                return;
            }
            zdo.Set(KeyTargetName, Safe(e.Name));
            zdo.Set(KeyTargetPos, e.Pos);
            zdo.Set(KeyTargetRot, e.Rot);
        }

        internal static string Safe(string s) => string.IsNullOrEmpty(s) ? L.T("(ohne Namen)", "(no name)") : s;
    }

    /// <summary>Das Portal-Fenster (Unity IMGUI): Name, Ziel, Standard-Ziel.</summary>
    internal static class PortalMenu
    {
        private enum Dropdown { None, Target, Default }

        private static TeleportWorld _portal;
        private static List<PortalSystem.Entry> _local = new List<PortalSystem.Entry>();
        private static List<PortalSystem.Entry> _server;
        private static float _openedAt;

        // Eingaben im Fenster
        private static string _name = "";
        private static PortalSystem.Entry _target;     // null = normal
        private static bool _isDefault;                // Haken "Standard-Portal"
        private static bool _wasDefault;
        private static Dropdown _open = Dropdown.None;
        private static string _filter = "";
        private static Vector2 _scroll;

        internal static bool IsOpen { get; private set; }

        internal static void Open(TeleportWorld portal)
        {
            if (PortalUI.TryOpen(portal)) return;   // Valheim-Fenster (Jotunn), sonst einfaches Fenster
            _portal = portal;
            _local = PortalSystem.ScanLocal();
            _server = null;
            _openedAt = Time.time;
            _name = portal.GetText() ?? "";
            _target = PortalSystem.GetTarget(portal);
            _wasDefault = PortalSystem.IsDefault(portal);
            _isDefault = _wasDefault;
            _open = Dropdown.None;
            _filter = "";
            _scroll = Vector2.zero;
            IsOpen = true;
            PortalSystem.RequestFromServer();
        }

        internal static void Close()
        {
            IsOpen = false;
            _portal = null;
            _open = Dropdown.None;
        }

        internal static void SetServerList(List<PortalSystem.Entry> list)
        {
            _server = list;
        }

        internal static void Update()
        {
            if (!IsOpen) return;
            var p = Player.m_localPlayer;
            if (_portal == null || p == null || p.IsDead() ||
                Vector3.Distance(p.transform.position, _portal.transform.position) > Cfg.PortalMenuCloseDistance.Value)
            {
                Close();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_open != Dropdown.None) _open = Dropdown.None; // erst Dropdown zu, dann Fenster
                else Close();
            }
        }

        /// <summary>Server-Liste + lokale Liste, doppelte zusammengefasst, eigenes Portal raus.</summary>
        private static List<PortalSystem.Entry> Entries()
        {
            var all = new List<PortalSystem.Entry>();
            if (_server != null) all.AddRange(_server);
            foreach (var l in _local)
                if (!all.Any(a => Vector3.Distance(a.Pos, l.Pos) < 1f)) all.Add(l);

            Vector3 own = _portal.transform.position;
            return all
                .Where(e => Vector3.Distance(e.Pos, own) > 1f)
                .OrderBy(e => string.IsNullOrEmpty(e.Name) ? 1 : 0)
                .ThenBy(e => e.Name)
                .ToList();
        }

        internal static void OnGUI()
        {
            if (!IsOpen || _portal == null) return;
            try { ZCursor.LockState = CursorLockMode.None; ZCursor.Show(); }
            catch { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            const float w = 460f, h = 600f;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.depth = -1000;
            GUILayout.Window(948271, rect, Draw, "Portal");
        }

        private static void Draw(int id)
        {
            if (_portal == null) return;
            var entries = Entries();
            bool save = false, cancel = false;

            // ---------- 1. Name ----------
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>" + L.T("Name des Portals", "Portal name") + "</b>");
            _name = GUILayout.TextField(_name ?? "", Cfg.PortalNameLength.Value);
            GUILayout.EndVertical();
            GUILayout.Space(6);

            // ---------- 2. Ziel ----------
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>" + L.T("Ziel", "Target") + "</b>");
            string targetText = _target == null ? L.T("Normal (Portal mit gleichem Namen)", "Normal (portal with the same name)") : PortalSystem.Safe(_target.Name);
            if (GUILayout.Button(targetText + (_open == Dropdown.Target ? "   [-]" : "   [+]")))
                Toggle(Dropdown.Target);
            if (_open == Dropdown.Target)
            {
                var pick = DrawList(entries, L.T("Normal (Portal mit gleichem Namen)", "Normal (portal with the same name)"), out bool pickedNone);
                if (pickedNone) { _target = null; _open = Dropdown.None; }
                else if (pick != null) { _target = pick; _open = Dropdown.None; }
            }
            GUILayout.EndVertical();
            GUILayout.Space(6);

            // ---------- 3. Standard-Portal ----------
            GUILayout.BeginVertical("box");
            _isDefault = GUILayout.Toggle(_isDefault, "  <b>" + L.T("Standard-Portal", "Default portal") + "</b>  <color=grey>" + L.T("(neue Portale verbinden sich automatisch hiermit)", "(new portals connect to this one automatically)") + "</color>");
            GUILayout.EndVertical();

            // ---------- Status & Buttons ----------
            GUILayout.FlexibleSpace();
            if (_server == null && Time.time - _openedAt < 3f)
                GUILayout.Label("<color=grey>" + L.T("Lade Portale vom Server ...", "Loading portals from the server ...") + "</color>");
            else if (_server == null)
                GUILayout.Label("<color=orange>" + L.T("Keine Antwort vom Server - nur Portale in der Nähe. Ist der Mod auf dem Server?", "No answer from the server - only portals nearby. Is the mod installed on the server?") + "</color>");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(L.T("Speichern", "Save"), GUILayout.Height(32))) save = true;
            if (GUILayout.Button(L.T("Abbrechen", "Cancel"), GUILayout.Height(32))) cancel = true;
            GUILayout.EndHorizontal();

            // Aktionen erst nach dem Zeichnen ausfuehren
            if (save) Save();
            else if (cancel) Close();
        }

        private static void Toggle(Dropdown d)
        {
            _open = _open == d ? Dropdown.None : d;
            _filter = "";
            _scroll = Vector2.zero;
        }

        /// <summary>Aufgeklappte Liste mit Suchfeld. Gibt den angeklickten Eintrag zurueck.</summary>
        private static PortalSystem.Entry DrawList(List<PortalSystem.Entry> entries, string noneText, out bool pickedNone)
        {
            pickedNone = false;
            PortalSystem.Entry picked = null;
            Vector3 me = _portal.transform.position;

            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Suchen:", "Search:"), GUILayout.Width(55));
            _filter = GUILayout.TextField(_filter ?? "");
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            if (GUILayout.Button("<i>" + noneText + "</i>")) pickedNone = true;

            foreach (var e in entries)
            {
                if (_filter.Length > 0 && (e.Name ?? "").IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                int dist = Mathf.RoundToInt(Vector3.Distance(me, e.Pos));
                string label = PortalSystem.Safe(e.Name) + (dist > 1 ? "   <color=grey>" + dist + " m</color>" : "");
                if (GUILayout.Button(label)) picked = e;
            }
            if (entries.Count == 0) GUILayout.Label(L.T("Keine anderen Portale gefunden.", "No other portals found."));
            GUILayout.EndScrollView();
            return picked;
        }

        private static void Save()
        {
            var portal = _portal;
            var player = Player.m_localPlayer;

            // 1. Name (laeuft ueber das normale Portal-RPC, wird mit allen synchronisiert)
            string newName = (_name ?? "").Trim();
            if (newName != (portal.GetText() ?? "")) portal.SetText(newName);

            // 2. Ziel
            PortalSystem.SetTarget(portal, _target);

            // 3. Standard-Ziel (macht der Server, weil nur er alle Portale kennt)
            if (_isDefault != _wasDefault)
                PortalSystem.SendSetDefault(_isDefault ? portal.transform.position : Vector3.zero);

            player?.Message(MessageHud.MessageType.Center, L.T("Portal gespeichert", "Portal saved"));
            Close();
        }
    }

    // ===================== Patches =====================

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class Game_Start_RegisterRpc
    {
        private static void Postfix() => PortalSystem.Register();
    }

    /// <summary>Solange das Portal-Fenster offen ist, gilt es als "Menue offen": Maus frei, Spieler steht still.</summary>
    [HarmonyPatch(typeof(Menu), "IsVisible")]
    internal static class Menu_IsVisible
    {
        private static void Postfix(ref bool __result)
        {
            if (PortalMenu.IsOpen || PortalUI.IsOpen || Radar.PanelOpen) __result = true;
        }
    }

    /// <summary>
    /// Valheim 1.0 entscheidet über den Mauszeiger mit Menu.IsActive (GameCamera.UpdateMouseCapture).
    /// Ohne diesen Patch wird der Zeiger im Portal-Fenster versteckt und festgehalten.
    /// </summary>
    [HarmonyPatch]
    internal static class Menu_IsActive
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Menu), "IsActive");

        private static void Postfix(ref bool __result)
        {
            if (PortalMenu.IsOpen || PortalUI.IsOpen || Radar.PanelOpen) __result = true;
        }
    }

    /// <summary>E am Portal oeffnet das Portal-Fenster statt des normalen Umbenennen-Dialogs.</summary>
    [HarmonyPatch(typeof(TeleportWorld), "Interact")]
    internal static class TeleportWorld_Interact
    {
        private static bool Prefix(TeleportWorld __instance, object[] __args, ref bool __result)
        {
            if (!Cfg.PortalsEnabled.Value) return true;
            bool hold = __args.Length > 1 && __args[1] is bool h && h;
            if (hold) return true;

            if (PrivateArea.CheckAccess(__instance.transform.position, 0f, true, false))
                PortalMenu.Open(__instance);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(TeleportWorld), "GetHoverText")]
    internal static class TeleportWorld_HoverText
    {
        private static void Postfix(TeleportWorld __instance, ref string __result)
        {
            if (!Cfg.PortalsEnabled.Value) return;
            string target = PortalSystem.GetTargetName(__instance);
            if (!string.IsNullOrEmpty(target))
                __result += "\n<color=#7fd4ff>" + L.T("Ziel: ", "Target: ") + target + "</color>";
            var zdo = PortalSystem.GetZdo(__instance);
            if (zdo != null && zdo.GetBool(PortalSystem.KeyIsDefault, false))
                __result += "\n<color=#7fd4ff>" + L.T("Standard-Ziel für neue Portale", "Default target for new portals") + "</color>";
        }
    }

    /// <summary>Neu gebautes Portal: Standard-Ziel vom Server holen und automatisch verbinden.</summary>
    [HarmonyPatch(typeof(Piece), "SetCreator")]
    internal static class Piece_SetCreator_DefaultPortal
    {
        private static void Postfix(Piece __instance)
        {
            if (!Cfg.PortalsEnabled.Value || Player.m_localPlayer == null) return;
            var portal = __instance.GetComponent<TeleportWorld>();
            if (portal == null) return;
            PortalSystem.PendingNewPortal = portal;
            PortalSystem.RequestFromServer();
        }
    }

    /// <summary>Portal leuchtet, wenn ein eigenes Ziel gesetzt ist.</summary>
    [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
    internal static class TeleportWorld_TargetFound
    {
        private static void Postfix(TeleportWorld __instance, ref bool __result)
        {
            if (!__result && Cfg.PortalsEnabled.Value && PortalSystem.GetTargetName(__instance).Length > 0)
                __result = true;
        }
    }

    /// <summary>Teleportiert zum gewaehlten Ziel statt zum Partner-Portal.</summary>
    [HarmonyPatch(typeof(TeleportWorld), "Teleport")]
    internal static class TeleportWorld_Teleport
    {
        private static bool Prefix(TeleportWorld __instance, object[] __args)
        {
            if (!Cfg.PortalsEnabled.Value) return true;
            var player = __args.Length > 0 ? __args[0] as Player : null;
            var zdo = PortalSystem.GetZdo(__instance);
            if (player == null || zdo == null) return true;
            if (zdo.GetString(PortalSystem.KeyTargetName, "").Length == 0) return true;

            bool allowAll = Util.GetField(__instance, "m_allowAllItems") is bool aa && aa;
            if (!player.IsTeleportable(allowAll))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return false;
            }

            Vector3 pos = zdo.GetVec3(PortalSystem.KeyTargetPos, Vector3.zero);
            Quaternion rot = zdo.GetQuaternion(PortalSystem.KeyTargetRot, Quaternion.identity);
            if (pos == Vector3.zero) return true;

            Vector3 exit = pos + rot * Vector3.forward * __instance.m_exitDistance + Vector3.up;
            player.TeleportTo(exit, rot, true);
            return false;
        }
    }
}
