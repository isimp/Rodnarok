using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The game's item stands holding a Serpent trophy (<see cref="CalmStand"/>). A tap of the use
    /// key on such a stand asks the stand's owner to switch its calm, kept in the stand's own saved
    /// data, so every player sees it and it lasts; the stand's ward is asked first, as the game asks
    /// it. Taking the trophy off switches it off. Every game looks over the loaded stands twice a
    /// second and hands the places of those calming to <see cref="CalmWaters"/>.
    /// </summary>
    internal static class CalmStands
    {
        /// <summary>The key of the switch in a stand's saved data, and the call that asks its owner to flip it.</summary>
        private static readonly int SwitchKey = "rodnarok_calm".GetStableHashCode();
        private const string SwitchCall = "Rodnarok_SwitchCalm";

        private static readonly int TrophyHash = CalmStand.Trophy.GetStableHashCode();

        /// <summary>How often the loaded stands are looked over, in seconds.</summary>
        private const float LookEvery = 0.5f;

        private static readonly AccessTools.FieldRef<ItemStand, ZNetView> View = AccessTools.FieldRefAccess<ItemStand, ZNetView>("m_nview");

        private static readonly List<ItemStand> Stands = new List<ItemStand>();
        private static float _nextLook;

        /// <summary>Whether a stand's calm is switched on, for the self-test.</summary>
        [ForTest]
        public static bool SwitchedOnForTest(ItemStand stand) => View(stand) is ZNetView view && view.IsValid() && view.GetZDO().GetBool(SwitchKey);

        /// <summary>A stand woke: it answers the call to switch its calm and is looked over from now on.</summary>
        public static void Woke(ItemStand stand)
        {
            var view = View(stand);
            if (view == null || !view.IsValid()) return;
            view.Register(SwitchCall, _ => Switch(stand));
            Stands.Add(stand);
        }

        /// <summary>
        /// The game's use key on a stand: a tap on one holding the Serpent trophy switches its calm
        /// instead; false when the switch took the press, so the game's own work is left out.
        /// </summary>
        public static bool Interact(ItemStand stand, Humanoid user, bool hold, bool alt, ref bool result)
        {
            var view = View(stand);
            if (!(user is Player) || view == null || !view.IsValid()) return true;
            if (!CalmStand.TapSwitches(hold, alt, Holds(view), stand.m_canBeRemoved)) return true;
            // Without the ward's leave the game's own answer comes, its flash included.
            if (!PrivateArea.CheckAccess(stand.transform.position, 0f, flash: false)) return true;
            var on = !view.GetZDO().GetBool(SwitchKey);
            view.InvokeRPC(SwitchCall);
            user.Message(MessageHud.MessageType.Center, on ? "The waters calm" : "The waters move again");
            result = true;
            return false;
        }

        /// <summary>The stand's hover text with the calm's lines after its name, for a stand holding the Serpent trophy.</summary>
        public static string Hover(ItemStand stand, string text)
        {
            var view = View(stand);
            if (string.IsNullOrEmpty(text) || view == null || !view.IsValid() || Player.m_localPlayer == null) return text;
            if (!stand.m_canBeRemoved || Holds(view) != CalmStand.Trophy) return text;
            if (!PrivateArea.CheckAccess(stand.transform.position, 0f, flash: false)) return text;
            var lines = string.Join("\n", CalmStand.HoverLines(view.GetZDO().GetBool(SwitchKey), ZInput.IsGamepadActive()));
            var added = Localization.instance.Localize(lines);
            var cut = text.IndexOf('\n');
            return cut < 0 ? text + "\n" + added : text.Substring(0, cut + 1) + added + text.Substring(cut);
        }

        /// <summary>The trophy came off the stand: its calm is switched off, so the next trophy put up starts off.</summary>
        public static void Taken(ItemStand stand)
        {
            var view = View(stand);
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            if (view.GetZDO().GetBool(SwitchKey)) view.GetZDO().Set(SwitchKey, false);
        }

        /// <summary>Called every frame in a world: twice a second, hands the places of the calming stands to the water.</summary>
        public static void Tick()
        {
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + LookEvery;
            Stands.RemoveAll(s => s == null);
            var calming = new List<Vector3>();
            foreach (var stand in Stands)
            {
                var view = View(stand);
                if (view == null || !view.IsValid()) continue;
                if (CalmStand.Calms(Holds(view), view.GetZDO().GetBool(SwitchKey))) calming.Add(stand.transform.position);
            }
            CalmWaters.CalmAround(calming);
        }

        /// <summary>Lets go of every stand on leaving a world.</summary>
        public static void Forget()
        {
            Stands.Clear();
            _nextLook = 0f;
        }

        /// <summary>The owner flips the switch a player asked for, while the stand still holds the Serpent trophy.</summary>
        private static void Switch(ItemStand stand)
        {
            var view = View(stand);
            if (stand == null || view == null || !view.IsValid() || !view.IsOwner()) return;
            if (Holds(view) != CalmStand.Trophy) return;
            var zdo = view.GetZDO();
            zdo.Set(SwitchKey, !zdo.GetBool(SwitchKey));
        }

        /// <summary>What the stand holds as far as the calm cares: the Serpent trophy by name, anything else as something.</summary>
        private static string Holds(ZNetView view)
        {
            var item = view.GetZDO().GetInt(ZDOVars.s_item);
            return item == 0 ? null : item == TrophyHash ? CalmStand.Trophy : "something";
        }
    }
}
