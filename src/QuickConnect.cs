using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pscht_QoL
{
    /// <summary>
    /// Schnellzugriff im Hauptmenü: ein Knopf "Server: Name" direkt unter "Spiel starten".
    /// Klick → Charakter wählen → Start verbindet sofort mit der eingetragenen Adresse (wie bei einer Einladung).
    /// Das Passwort fragt Valheim selbst ab, es wird nicht gespeichert.
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), "Awake")]
    internal static class FejdStartup_Awake_QuickConnect
    {
        private const string ButtonName = "PschtQoL_QuickConnect";

        private static void Postfix(FejdStartup __instance)
        {
            ZNet_ClientHandshake_QuickPassword.Pending = false;
            try { Add(__instance); } catch (System.Exception e) { Plugin.LogWarn("Schnellzugriff: " + e.Message); }
        }

        private static void Add(FejdStartup fs)
        {
            if (!Cfg.QuickEnabled.Value) return;
            string addr = (Cfg.QuickAddress.Value ?? "").Trim();
            if (addr.Length == 0 || fs.m_menuList == null) return;
            if (fs.m_menuList.transform.Find(ButtonName) != null) return;

            var buttons = fs.m_menuList.GetComponentsInChildren<Button>(true);
            if (buttons.Length == 0) return;
            var template = buttons[0];

            var go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = ButtonName;
            go.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
            // Übersetzungs-Komponenten entfernen, sonst überschreibt Valheim den Text wieder
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
                if (comp != null && comp.GetType().Name == "Localize") Object.Destroy(comp);

            string name = string.IsNullOrWhiteSpace(Cfg.QuickName.Value) ? addr : Cfg.QuickName.Value.Trim();
            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = L.T("Server: ", "Server: ") + name;

            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();
            btn.onClick.AddListener(() => Join(fs, addr));

            // in Valheims Menü-Liste aufnehmen (Tastatur/Controller-Auswahl)
            var arr = Util.GetField(fs, "m_menuButtons") as Button[];
            if (arr != null && !arr.Contains(btn))
            {
                var list = arr.ToList();
                int i = list.IndexOf(template);
                list.Insert(i >= 0 ? i + 1 : list.Count, btn);
                Util.SetField(fs, "m_menuButtons", list.ToArray());
            }
            Plugin.LogInfo("Schnellzugriff: Knopf für " + addr + " angelegt");
        }

        private static void Join(FejdStartup fs, string addr)
        {
            ServerJoinDataUtils.GetAddressAndPortFromString(addr, out var ip, out var _);
            if (string.IsNullOrEmpty(ip))
            {
                Plugin.LogWarn("Schnellzugriff: Adresse ungültig: " + addr);
                return;
            }
            var data = new ServerJoinData(new ServerJoinDataDedicated(addr));
            ZNet_ClientHandshake_QuickPassword.Pending = true;   // gespeichertes Passwort nur für diesen Beitritt
            // wie eine Einladung: Server merken, Charakter-Auswahl zeigen, "Start" verbindet
            if (Util.Call(fs, "ProceedJoinRequest", data) == null && AccessTools.Method(typeof(FejdStartup), "ProceedJoinRequest") == null)
            {
                fs.SetServerToJoin(data);
                fs.JoinServer();
            }
        }
    }

    /// <summary>
    /// Fragt der Server nach dem Passwort und ist eins gespeichert, wird es automatisch eingegeben
    /// (nur beim Beitritt über den Schnellzugriff-Knopf). Ohne gespeichertes Passwort fragt Valheim wie gewohnt.
    /// </summary>
    [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
    internal static class ZNet_ClientHandshake_QuickPassword
    {
        internal static bool Pending;

        private static void Postfix(ZNet __instance, object[] __args)
        {
            if (!Pending) return;
            Pending = false;
            bool needPassword = __args != null && __args.Length > 1 && __args[1] is bool b && b;
            string pwd = Cfg.QuickPassword.Value;
            if (!needPassword || string.IsNullOrEmpty(pwd)) return;
            Util.Call(__instance, "OnPasswordEntered", pwd);
            Plugin.LogInfo("Schnellzugriff: gespeichertes Passwort verwendet");
        }
    }
}
