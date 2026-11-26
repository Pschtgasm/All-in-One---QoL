using HarmonyLib;
using UnityEngine;

namespace Pscht_QoL
{
    /// <summary>
    /// Eigener Gürtel-Slot: Der Gürtel (Megingiard) wirkt ZUSÄTZLICH zu Wünschelrute oder Irrlicht.
    /// Valheim erlaubt nur ein Hilfsmittel gleichzeitig – deshalb wird der Gürtel nicht normal
    /// "angelegt", sondern liegt im Gürtel-Slot und der Mod schaltet seine Wirkung (Statuseffekt) ein.
    /// </summary>
    internal static class BeltSlot
    {
        private static StatusEffect _active;     // Vorlage des Effekts, den wir eingeschaltet haben
        private static float _next;

        internal static ItemDrop.ItemData BeltInSlot(Player p)
        {
            var inv = p.GetInventory();
            int b = ExtraSlots.BaseHeight(inv);
            if (b < 0) return null;
            var it = inv.GetItemAt(ExtraSlots.BeltX, b + 1);
            return it != null && ExtraSlots.IsBelt(it) ? it : null;
        }

        /// <summary>Gürtel in den Gürtel-Slot legen (tauscht mit dem, was dort liegt).</summary>
        internal static bool MoveIntoSlot(Player p, ItemDrop.ItemData belt)
        {
            var inv = p.GetInventory();
            int b = ExtraSlots.BaseHeight(inv);
            if (b < 0 || !inv.ContainsItem(belt)) return false;
            var target = new Vector2i(ExtraSlots.BeltX, b + 1);
            var occ = inv.GetItemAt(target.x, target.y);
            if (occ != null && occ != belt) occ.m_gridPos = belt.m_gridPos;
            belt.m_gridPos = target;
            Util.Call(inv, "Changed");
            return true;
        }

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.3f;
            var p = Player.m_localPlayer;
            if (p == null) { _active = null; return; }

            var seman = p.GetSEMan();
            ItemDrop.ItemData belt = ExtraSlots.BeltOn ? BeltInSlot(p) : null;

            // Gürtel noch "normal" angelegt (alter Spielstand)? -> ablegen und in den Slot
            if (ExtraSlots.BeltOn && Util.GetField(p, "m_utilityItem") is ItemDrop.ItemData util && ExtraSlots.IsBelt(util))
            {
                p.UnequipItem(util, false);
                MoveIntoSlot(p, util);
                belt = util;
            }

            var want = belt?.m_shared?.m_equipStatusEffect;
            if (_active != null && (want == null || want.NameHash() != _active.NameHash()))
            {
                seman.RemoveStatusEffect(_active.NameHash(), true);
                _active = null;
            }
            if (want != null && !seman.HaveStatusEffect(want.NameHash()))
            {
                seman.AddStatusEffect(want, false, belt.m_quality, 0f);
                _active = want;
            }
            else if (want != null) _active = want;
        }
    }

    /// <summary>Gürtel "anlegen" (Rechtsklick/Schnellleiste) = in den Gürtel-Slot legen bzw. wieder heraus.</summary>
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    internal static class Humanoid_EquipItem_Belt
    {
        private static bool Prefix(Humanoid __instance, object[] __args, ref bool __result)
        {
            if (!ExtraSlots.BeltOn || __args.Length == 0) return true;
            if (!(__instance is Player p) || p != Player.m_localPlayer) return true;
            if (!(__args[0] is ItemDrop.ItemData item) || !ExtraSlots.IsBelt(item)) return true;
            if (ExtraSlots.BaseHeight(p.GetInventory()) < 0) return true;

            if (BeltSlot.BeltInSlot(p) == item)
            {
                // Schon im Slot -> wieder ins Inventar (ablegen)
                var free = ExtraSlots.FindFreeNormal(p.GetInventory());
                if (free.x >= 0) { item.m_gridPos = free; Util.Call(p.GetInventory(), "Changed"); }
            }
            else BeltSlot.MoveIntoSlot(p, item);

            __result = true;
            return false;   // nicht als normales Hilfsmittel anlegen
        }
    }
}
