using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Pause im Mehrspieler: Haben ALLE Spieler das Esc-Menü offen, hält das Spiel an (auch die Zeit auf dem Server).
    /// Sobald einer das Menü schließt oder ein neuer Spieler kommt, geht es weiter.
    /// Der Server (mit diesem Mod) entscheidet und schickt den Zustand an alle. Im Einzelspieler pausiert Valheim selbst.
    /// </summary>
    internal static class PauseSync
    {
        private const string RpcWant = "PQ_PauseWant";
        private const string RpcState = "PQ_PauseState";

        internal static bool Paused;
        private static int _want, _total;
        private static float _lastState;          // wann zuletzt ein Zustand vom Server kam (echte Zeit)
        private static bool _lastSentWant, _sentOnce;
        private static float _nextSend, _nextEval;
        private static bool _forced;

        // Server: wer will Pause?
        private static readonly Dictionary<long, bool> Wants = new Dictionary<long, bool>();

        internal static void Register()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<bool>(RpcWant, OnWant);
            ZRoutedRpc.instance.Register<bool, int, int>(RpcState, OnState);
        }

        internal static void Reset()
        {
            Paused = false; _want = 0; _total = 0; _sentOnce = false;
            Wants.Clear();
            if (_forced) { Time.timeScale = 1f; _forced = false; }
        }

        private static bool Active => Cfg.PauseWhenAllInMenu.Value && ZNet.instance != null && !ZNet.IsSinglePlayer && ZRoutedRpc.instance != null;

        /// <summary>Jedes Bild (Plugin.Update): eigenen Menü-Zustand melden, als Server entscheiden.</summary>
        internal static void Tick()
        {
            if (!Active) { if (Paused) Reset(); return; }
            float now = Time.realtimeSinceStartup;

            // Spieler: Menü offen?
            if (Player.m_localPlayer != null)
            {
                bool want = Menu.IsVisible();
                if (!_sentOnce || want != _lastSentWant || now >= _nextSend)
                {
                    _sentOnce = true;
                    _lastSentWant = want;
                    _nextSend = now + 2f;
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcWant, want);
                }
            }

            // Server: alle 0,5 s entscheiden
            if (ZNet.instance.IsServer() && now >= _nextEval)
            {
                _nextEval = now + 0.5f;
                Evaluate();
            }

            // Kein Zustand mehr vom Server (Verbindung weg)? Nicht ewig pausiert bleiben
            if (Paused && !ZNet.instance.IsServer() && now - _lastState > 6f) Paused = false;

            Apply();
        }

        private static void Evaluate()
        {
            var ids = new List<long>();
            foreach (var peer in ZNet.instance.GetPeers())
                if (peer != null && peer.m_uid != 0) ids.Add(peer.m_uid);
            if (Player.m_localPlayer != null) ids.Add(ZNet.GetUID());   // Host spielt selbst mit

            // Weg sind weg
            foreach (var id in new List<long>(Wants.Keys)) if (!ids.Contains(id)) Wants.Remove(id);

            int want = 0;
            foreach (var id in ids) if (Wants.TryGetValue(id, out bool w) && w) want++;
            bool paused = ids.Count > 0 && want == ids.Count;
            bool changed = paused != Paused || want != _want || ids.Count != _total;
            Paused = paused; _want = want; _total = ids.Count;
            _lastState = Time.realtimeSinceStartup;
            if (changed) Plugin.LogInfo("Pause: " + want + "/" + ids.Count + " im Menü" + (paused ? " → angehalten" : ""));
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcState, paused, want, ids.Count);
        }

        private static void OnWant(long sender, bool want)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            Wants[sender] = want;
        }

        private static void OnState(long sender, bool paused, int want, int total)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer()) return;
            Paused = paused && Active; _want = want; _total = total;
            _lastState = Time.realtimeSinceStartup;
        }

        /// <summary>Zeit anhalten / weiterlaufen lassen.</summary>
        internal static void Apply()
        {
            if (Paused) { Time.timeScale = 0f; _forced = true; }
            else if (_forced) { Time.timeScale = 1f; _forced = false; }
        }

        internal static void OnGUI()
        {
            if (!Active || Player.m_localPlayer == null) return;
            string text = null;
            if (Paused) text = L.T("Pausiert – alle Spieler sind im Menü", "Paused – all players are in the menu");
            else if (Menu.IsVisible() && _total > 1) text = L.T("Pause, sobald alle im Menü sind (", "Pause as soon as everyone is in the menu (") + _want + "/" + _total + ")";
            if (text == null) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height / 30f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            var rect = new Rect(0, Screen.height * 0.82f, Screen.width, Screen.height * 0.08f);
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, style);
            style.normal.textColor = Paused ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.9f, 0.9f);
            GUI.Label(rect, text, style);
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class Game_Start_PauseSync
    {
        private static void Postfix() => PauseSync.Register();
    }

    /// <summary>Valheim setzt die Zeit jedes Bild neu – danach wieder anhalten, solange pausiert.</summary>
    [HarmonyPatch(typeof(Game), "UpdatePause")]
    internal static class Game_UpdatePause_PauseSync
    {
        private static void Postfix() { if (PauseSync.Paused) Time.timeScale = 0f; }
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    internal static class ZNet_OnDestroy_PauseSync
    {
        private static void Postfix() => PauseSync.Reset();
    }
}
