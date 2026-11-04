using System;
using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>
    /// Bumps before the bite, with the setting on: the fish picked to take the bait bumps the float
    /// lightly once or twice before it goes for the hook. A bump is a short, light pull on the
    /// float, far lighter than the bite's, and never a nibble the game knows of, so it can never be
    /// hooked. No bump comes as the fish is picked or right before it bites, and two bumps are far
    /// enough apart to be told apart.
    /// </summary>
    internal static class Bumps
    {
        /// <summary>How soon after the fish is picked the first bump may come, and how long before it goes for the hook the last may, in seconds.</summary>
        public const float FirstAfter = 0.4f;
        public const float LastBefore = 0.6f;

        /// <summary>How close to the float, across the water in metres, the picked fish hovers until it may go for the hook, and how close it must be for a bump.</summary>
        public const float Hover = 1.5f;
        public const float Reach = 2f;

        /// <summary>Whether a bump due at <paramref name="due"/> comes now: once due, with the picked fish by the float, and not in the last moment before it may bite.</summary>
        public static bool Fires(float now, float due, float metresFromFloat, float freeAt) =>
            now >= due && metresFromFloat <= Reach && !Dropped(now, freeAt);

        /// <summary>Whether the sign follows the picked fish in: only once it may bite; while it hovers and bumps the sign stays quiet by the float, where it is.</summary>
        public static bool FollowedIn(bool picked, bool mayBite) => picked && mayBite;

        /// <summary>Whether bumps still due are dropped: in the last moment before the picked fish may bite, as no bump comes right before a bite.</summary>
        public static bool Dropped(float now, float freeAt) => now > freeAt - LastBefore;

        /// <summary>How far apart two bumps are at the least, in seconds.</summary>
        public const float Apart = 0.5f;

        /// <summary>How long a bump pulls the float, in seconds, and how hard, in metres a second each second.</summary>
        public const float PullSeconds = 0.25f;
        public const float Pull = 12f;

        /// <summary>
        /// When the bumps come, in seconds after the fish is picked, in order, for a fish that goes for
        /// the hook <paramref name="lead"/> seconds after: one or two by the count roll, placed by the
        /// two other rolls; none when off or with too little time.
        /// </summary>
        public static IReadOnlyList<float> Times(bool on, float lead, double countRoll, double first, double second)
        {
            var from = FirstAfter;
            var to = lead - LastBefore;
            if (!on || to < from) return Array.Empty<float>();
            var span = to - from;
            if (countRoll < 0.5 || span < Apart) return new[] { from + (float)Clamp01(first) * span };
            var one = from + (float)Clamp01(first) * (span - Apart);
            var two = one + Apart + (float)Clamp01(second) * (to - one - Apart);
            return new[] { one, two };
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
    }
}
