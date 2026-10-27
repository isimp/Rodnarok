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

        /// <summary>How far apart two bumps are at the least, in seconds.</summary>
        public const float Apart = 0.5f;

        /// <summary>How long a bump pulls the float, in seconds, and how hard, in metres a second each second.</summary>
        public const float PullSeconds = 0.2f;
        public const float Pull = 8f;

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
