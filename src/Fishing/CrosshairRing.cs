using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The crosshair ring tells the bite and the fight on the game's own bow ring: after a bite it
    /// shrinks from wide white to tight yellow as the hook window runs out; while a hooked fish
    /// struggles it is wide, red and pulsing; while it rests, small and steady yellow. While a bow
    /// or the rod is drawn the ring stays the game's own.
    /// </summary>
    internal static class CrosshairRing
    {
        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        /// <summary>After the game set the crosshair for this frame, draws the ring for the player's own float.</summary>
        public static void Draw(Hud hud, float bowDrawPercentage)
        {
            if (bowDrawPercentage > 0f || hud == null || hud.m_crosshairBow == null) return;
            var draw = RingDraw.For(RingFor(OwnFloat()), Time.time);
            if (!draw.Shown) return;
            var ring = hud.m_crosshairBow;
            ring.gameObject.SetActive(true);
            ring.transform.localScale = new Vector3(draw.Scale, draw.Scale, draw.Scale);
            ring.color = new Color(draw.R, draw.G, draw.B, draw.A);
        }

        /// <summary>The ring a float calls for: the fight while a fish is on the line, else the bite while its window lasts.</summary>
        private static Ring RingFor(FishingFloat ff)
        {
            if (ff == null) return Ring.Hidden;
            var fish = ff.GetCatch();
            if (fish != null) return Ring.ForFight(fish.IsEscaping());
            if (Nibbler(ff) == null) return Ring.Hidden;
            return Ring.ForBite(Time.time - NibbleTime(ff), PlayRules.Now.HookWindow);
        }

        /// <summary>The float this game cast, if one is out.</summary>
        private static FishingFloat OwnFloat()
        {
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null) continue;
                var view = ff.GetComponent<ZNetView>();
                if (view != null && view.IsValid() && view.IsOwner()) return ff;
            }
            return null;
        }
    }
}
