using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Pscht_QoL
{
    /// <summary>
    /// Smarte Kisten: Legst du ein Item in die offene Kiste, in der es (bzw. seine Gruppe) noch nicht liegt,
    /// wandert es automatisch in eine Kiste in der Nähe, die es schon hat.
    /// </summary>
    internal static class SmartContainers
    {
        internal static bool Busy;

        internal static Container OpenContainer()
        {
            if (InventoryGui.instance == null || !InventoryGui.IsVisible()) return null;
            return Util.GetField(InventoryGui.instance, "m_currentContainer") as Container;
        }

        internal static void Route(Player player, Container source, string itemName)
        {
            var srcInv = source.GetInventory();
            var items = srcInv.GetAllItems().Where(i => i != null && i.m_shared.m_name == itemName).ToList();
            if (items.Count == 0) return;
            var sample = items[0];
            int total = items.Sum(i => i.m_stack);

            var candidates = ContainerTracker.Query(player, Cfg.SmartRange.Value)
                .Where(c => c != source && c.GetInventory() != srcInv)
                .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude)
                .ToList();

            // 1) Kiste, die genau dieses Item schon hat
            Container target = candidates.FirstOrDefault(c =>
                c.GetInventory().HaveItem(itemName) && c.GetInventory().CanAddItem(sample, total));

            // 2) Kiste mit derselben Gruppe - aber nur, wenn die offene Kiste diese Gruppe nicht selbst schon hat
            if (target == null && Cfg.SmartGroups.Value)
            {
                string group = ItemGroups.Of(sample);
                if (group != null && !srcInv.GetAllItems().Any(i => i != null && i.m_shared.m_name != itemName && ItemGroups.Of(i) == group))
                {
                    target = candidates.FirstOrDefault(c =>
                        c.GetInventory().GetAllItems().Any(i => i != null && ItemGroups.Of(i) == group) &&
                        c.GetInventory().CanAddItem(sample, total));
                }
            }

            if (target == null) return;

            Busy = true;
            try
            {
                ContainerTracker.ClaimOwnership(target);
                var tInv = target.GetInventory();
                foreach (var it in items) tInv.MoveItemToThis(srcInv, it);
            }
            finally { Busy = false; }
            ContainerTracker.Invalidate();

            ChestHighlight.Flash(target);   // Ziel-Kiste kurz aufleuchten lassen

            string name = Localization.instance != null ? Localization.instance.Localize(sample.m_shared.m_name) : sample.m_shared.m_name;
            string tag = ChestFeatures.GetTag(target);
            player.Message(MessageHud.MessageType.Center, name + " -> " + (string.IsNullOrEmpty(tag) ? L.T("passende Kiste", "matching chest") : tag));
        }
    }

    /// <summary>Gruppen aus der Konfig (Holz, Erze, ...) plus automatische Gruppen nach Item-Typ.</summary>
    internal static class ItemGroups
    {
        private static string _parsedFrom;
        private static Dictionary<string, string> _map = new Dictionary<string, string>();

        internal static string Of(ItemDrop.ItemData item)
        {
            if (item == null) return null;
            string raw = Cfg.SmartGroupList.Value ?? "";
            if (raw != _parsedFrom)
            {
                _parsedFrom = raw;
                _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var grp in raw.Split(';'))
                {
                    int colon = grp.IndexOf(':');
                    if (colon <= 0) continue;
                    string gName = grp.Substring(0, colon).Trim();
                    foreach (var prefab in grp.Substring(colon + 1).Split(','))
                    {
                        string p = prefab.Trim();
                        if (p.Length > 0) _map[p] = gName;
                    }
                }
            }

            string prefabName = item.m_dropPrefab ? item.m_dropPrefab.name : null;
            if (prefabName != null && _map.TryGetValue(prefabName, out var g)) return g;

            if (Cfg.SmartAutoGroups.Value)
            {
                // Ungekochte Zwischenprodukte (Teig, ungebackene Pasteten …) zu den Zutaten
                if (prefabName != null && (prefabName.EndsWith("Uncooked") || prefabName.EndsWith("Dough")))
                    return "Zutaten";
                // Alles, was der Kultivator als Pflanze setzen kann, zu den Samen
                if (prefabName != null && Plantables().Contains(prefabName))
                    return "Samen";

                // Spitzhacken und Angeln zählen im Spiel als Waffe – hier als Werkzeug
                string skill = item.m_shared.m_skillType.ToString();
                string type = item.m_shared.m_itemType.ToString();
                if ((skill == "Pickaxes" || skill == "Fishing") && type.Contains("Weapon"))
                    return "#Schmuck, Hilfsmittel und Werkzeuge";

                switch (item.m_shared.m_itemType.ToString())
                {
                    case "Tool":
                        return "#Schmuck, Hilfsmittel und Werkzeuge";
                    case "OneHandedWeapon": case "TwoHandedWeapon": case "TwoHandedWeaponLeft": case "Bow":
                    case "Shield": case "Torch": case "Attach_Atgeir":
                    case "Helmet": case "Chest": case "Legs": case "Shoulder": case "Hands":
                        return "#Rüstung und Waffen";
                    case "Utility": case "Trinket":
                        return "#Schmuck, Hilfsmittel und Werkzeuge";
                    case "Trophy": return "#Trophäen";
                    case "Ammo": return "#Munition";
                    case "Fish": return "#Fisch";
                    case "Consumable":
                        // Fertige Gerichte geben Leben/Ausdauer/Eitr, Tränke und Met nicht
                        var sh = item.m_shared;
                        return sh.m_food > 0f || sh.m_foodStamina > 0f || sh.m_foodEitr > 0f ? "#Gerichte" : "#Tränke";
                }
            }
            return null;
        }

        internal const string DefaultSeeds =
            "Samen:CarrotSeeds,TurnipSeeds,OnionSeeds,Acorn,BeechSeeds,BirchSeeds,FirCone,PineCone,VineberrySeeds,Flax;";

        internal const string DefaultIngredients =
            "Zutaten:Honey,Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomJotunPuffs," +
            "MushroomMagecap,MushroomSmokePuff,Carrot,Turnip,Onion,Barley,BarleyFlour,BreadDough,Dandelion,Thistle," +
            "ChickenEgg,Fiddleheadfern,Vineberry,RoyalJelly,Sap;";

        private static HashSet<string> _plantables;
        private static ObjectDB _plantablesFor;

        /// <summary>Item-Namen, die im Kultivator eine Pflanze setzen (z.B. Samen, Zapfen, Eicheln).</summary>
        private static HashSet<string> Plantables()
        {
            if (_plantables != null && _plantablesFor == ObjectDB.instance) return _plantables;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var cult = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Cultivator") : null;
                var table = cult ? cult.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                if (table != null)
                    foreach (var go in table.m_pieces)
                    {
                        if (!go || go.GetComponent<Plant>() == null) continue;
                        var piece = go.GetComponent<Piece>();
                        if (piece?.m_resources == null) continue;
                        foreach (var r in piece.m_resources)
                            if (r?.m_resItem != null) set.Add(r.m_resItem.gameObject.name);
                    }
            }
            catch { }
            _plantables = set;
            _plantablesFor = ObjectDB.instance;
            return set;
        }
    }

    /// <summary>Alle Varianten von Inventory.MoveItemToThis (Ziehen, Shift-Klick, ...).</summary>
    [HarmonyPatch]
    internal static class Inventory_MoveItemToThis_Smart
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Inventory)).Where(m => m.Name == "MoveItemToThis").Cast<MethodBase>();
        }

        private static void Prefix(Inventory __instance, object[] __args, out string __state)
        {
            __state = null;
            if (!Cfg.SmartEnabled.Value || SmartContainers.Busy) return;
            var item = __args.OfType<ItemDrop.ItemData>().FirstOrDefault();
            if (item == null) return;
            // Nur merken, wenn die Kiste dieses Item vorher NICHT hatte
            if (!__instance.HaveItem(item.m_shared.m_name)) __state = item.m_shared.m_name;
        }

        private static void Postfix(Inventory __instance, object[] __args, string __state)
        {
            if (__state == null || SmartContainers.Busy) return;
            var player = Player.m_localPlayer;
            if (player == null) return;

            var from = __args.OfType<Inventory>().FirstOrDefault();
            if (from != player.GetInventory()) return;          // nur vom Spieler in eine Kiste

            var open = SmartContainers.OpenContainer();
            if (open == null || open.GetInventory() != __instance) return;

            if (Cfg.SmartBypassKey.Value.IsPressed()) return;   // Taste gehalten = normal ablegen

            try { SmartContainers.Route(player, open, __state); }
            catch (Exception e) { Plugin.LogWarn("Smarte Kisten: " + e.Message); }
        }
    }
}
