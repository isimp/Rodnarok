using System;

namespace Rodnarok
{
    /// <summary>
    /// Missed bites wear the bait down, more likely each time. The game lets a fish bite the same
    /// bait again a second after a missed bite, for ever, and gives the bait back when the empty
    /// float is reeled in. With this rule a bite neither hooked nor any longer within the hook window
    /// is a miss: the first miss on a float very seldom takes the bait, the second more often, the
    /// third almost always, and later ones almost surely. A taken bait leaves the float bare:
    /// nothing bites it again and reeling it in gives no bait back. A fish that missed has learned
    /// and bites that float no more; once every fish of the school has learned, it swims off,
    /// leaving whatever bait is left.
    /// </summary>
    internal static class BaitLoss
    {
        /// <summary>The chance that the first, second, third and later miss on a float takes its bait.</summary>
        private static readonly double[] Chances = { 0.1, 0.4, 0.9, 0.98 };

        /// <summary>Whether a bite is a miss now: neither hooked nor any longer within the hook window.</summary>
        public static bool Missed(bool bitten, bool hooked, float sinceBite, float window) =>
            bitten && !hooked && sinceBite > window;

        /// <summary>The chance that this miss on a float, counted from one, takes its bait.</summary>
        public static double TakeChance(int misses) => misses < 1 ? 0.0 : Chances[Math.Min(misses, Chances.Length) - 1];

        /// <summary>Whether this miss takes the bait, for a roll from 0 to 1.</summary>
        public static bool Taken(bool on, int misses, double roll) => on && roll < TakeChance(misses);

        /// <summary>Whether every fish of a school has missed the bait and learned, so the school swims off.</summary>
        public static bool SchoolLearned(int learned, int size) => learned >= size;
    }
}
