using System;

namespace Rodnarok
{
    /// <summary>
    /// How a school comes in: it starts loose out in the water and its home creeps to the float over
    /// a while, its fish cruising at one slow pace and wandering widely around it, so the school
    /// gathers around the float only at the end. Once in, each fish swims at its own speed again.
    /// </summary>
    internal static class Drift
    {
        /// <summary>How far out a school starts, in metres.</summary>
        public const float From = 14f;

        /// <summary>How long the school's home takes to reach the float, in seconds.</summary>
        public const float Seconds = 12f;

        /// <summary>
        /// How fast every fish swims on the way in, in metres a second: half again the home's pace
        /// and more, so none falls behind, and no faster than the game's slowest fish.
        /// </summary>
        public const float Cruise = 2f;

        /// <summary>How far apart the school starts: each fish within this many metres of its centre.</summary>
        public const float Spread = 4f;

        /// <summary>How widely a fish wanders around the school's home as the way in starts, in metres.</summary>
        public const float Loose = 6f;

        /// <summary>How much of the way in the school's home has come, from 0 out in the water to 1 at the float.</summary>
        public static float Share(float sinceCame)
        {
            if (float.IsNaN(sinceCame) || sinceCame <= 0f) return 0f;
            return Math.Min(1f, sinceCame / Seconds);
        }

        /// <summary>How fast a fish of this speed swims this far along the way in.</summary>
        public static float Speed(float own, float share) => share >= 1f ? own : Math.Min(own, Cruise);

        /// <summary>How widely a fish wanders around the school's home this far along the way in, narrowing to the circle around the float.</summary>
        public static float SwimRange(float share, float circle)
        {
            var t = Math.Min(1f, Math.Max(0f, share));
            return Loose + (circle - Loose) * t;
        }
    }
}
