using System;
using System.Collections.Generic;
using System.Linq;

namespace Rodnarok
{
    /// <summary>How many of a fish the world spawns together, as its spawn entry sets them; unknown for a fish the world never spawns in groups.</summary>
    internal readonly struct GroupSize
    {
        public GroupSize(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }

        public static GroupSize Unknown => default;

        public bool Known => Max > 0 || Min > 0;
    }

    /// <summary>
    /// The school a bait draws: as many of its fish as the world spawns together, each size as
    /// likely, and a small school for a fish the world never spawns in groups. Any fish of it may
    /// take the bait, each as likely, so the stars of the catch follow the game's odds; when one
    /// is hooked the rest scatter. Rodnarök's fish leave once the float has been gone a while,
    /// and a fish of the world's own is never taken away.
    /// </summary>
    internal static class School
    {
        /// <summary>The smallest and largest school of a fish without a group of its own.</summary>
        private const int SmallSchoolMin = 2;
        private const int SmallSchoolMax = 4;

        /// <summary>How many fish come, for a roll from 0 to 1.</summary>
        public static int Size(GroupSize group, FishingRules rules, double roll)
        {
            if (!rules.Schools) return 1;
            var low = group.Known ? Math.Max(1, Math.Min(group.Min, group.Max)) : SmallSchoolMin;
            var high = group.Known ? Math.Max(1, Math.Max(group.Min, group.Max)) : SmallSchoolMax;
            return low + Pick(high - low + 1, roll);
        }

        /// <summary>Which fish of a school this big takes the bait, counted from 0, for a roll from 0 to 1.</summary>
        public static int Biter(int size, double roll) => Pick(Math.Max(1, size), roll);

        /// <summary>The fish of a school that scatter when one is hooked: all the others.</summary>
        public static IReadOnlyList<int> Scatter(int size, int hooked) => Enumerable.Range(0, Math.Max(0, size)).Where(i => i != hooked).ToList();

        /// <summary>Whether a fish is taken away: only Rodnarök's own, once its float has been gone this long.</summary>
        public static bool Remove(bool ours, bool floatAlive, float sinceFloatGone, float leaveSeconds) =>
            ours && !floatAlive && sinceFloatGone >= leaveSeconds;

        /// <summary>One of <paramref name="count"/> choices, each taking an equal share of the rolls.</summary>
        private static int Pick(int count, double roll)
        {
            var index = (int)Math.Floor(Math.Max(0.0, roll) * count);
            return Math.Min(index, count - 1);
        }
    }
}
