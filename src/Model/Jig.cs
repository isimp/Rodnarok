using System;

namespace Rodnarok
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float: it twitches <see cref="Metres"/> toward
    /// the rod over <see cref="Seconds"/>. A fish waiting at the float comes <see cref="Cut"/> sooner for
    /// each jig: the fish picked to bite as it hovers, or a shy school as it hesitates. One jig is too
    /// gentle for a wary school's own speed rule (<see cref="Wary.FloatSpeed"/> over half a second); a jig
    /// within <see cref="TooSoonSeconds"/> of the last startles a wary school near the float as a quickly
    /// moved float does, and draws no fish.
    /// </summary>
    internal static class Jig
    {
        /// <summary>How far a jig twitches the float toward the rod, in metres.</summary>
        public const float Metres = 0.25f;

        /// <summary>How long the twitch takes, in seconds.</summary>
        public const float Seconds = 0.25f;

        /// <summary>How much sooner, in seconds, a waiting fish comes for each jig.</summary>
        public const float Cut = 1.5f;

        /// <summary>How fast the float twitches, in metres a second.</summary>
        public static float Speed => Metres / Seconds;

        /// <summary>How soon after the last jig, in seconds, another one startles a wary school near the float instead of drawing a fish.</summary>
        public const float TooSoonSeconds = 0.6f;

        /// <summary>Whether a jig this long after the last comes too soon; the first of a cast never does.</summary>
        public static bool TooSoon(float sinceLast) => sinceLast < TooSoonSeconds;

        /// <summary>Whether a press jigs: a float on the water with nothing hooked, with jigging on. A press while the float still twitches starts the twitch over.</summary>
        public static bool OnPress(bool on, bool onWater, bool hooked) => on && onWater && !hooked;

        /// <summary>When the hovering picked fish may bite after a jig: <see cref="Cut"/> sooner, never before now; unchanged with no fish waiting.</summary>
        public static float SoonerFree(float freeAt, float now)
        {
            if (freeAt >= float.MaxValue || freeAt <= now) return freeAt;
            return Math.Max(now, freeAt - Cut);
        }

        /// <summary>When a hesitating school counts its hesitation from after a jig: <see cref="Cut"/> earlier; unchanged for one not yet ready (negative).</summary>
        public static float SoonerReady(float readyAt) => readyAt < 0f ? readyAt : readyAt - Cut;
    }
}
