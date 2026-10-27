using System;

namespace Rodnarok
{
    /// <summary>
    /// How a school comes in: it appears loose and out of sight the moment the float lands, and the
    /// wait is its swim in. Its home creeps to the float over that time, its fish cruising slowly
    /// and wandering widely around it, a school that starts deep rising as it comes, so it gathers
    /// around the float only at the end. The swim takes the rolled wait, or as long as the school's
    /// fish need to keep up with its home on a long way in. Once in, each fish swims at its own
    /// speed again.
    /// </summary>
    internal static class Drift
    {
        /// <summary>The fastest a fish cruises on the way in, in metres a second; a slower fish cruises at its own speed.</summary>
        public const float MaxCruise = 3f;

        /// <summary>How much faster than the school's home its fish cruise, so none falls behind.</summary>
        private const float Lead = 1.5f;

        /// <summary>How far apart the school starts: each fish within this many metres of its centre.</summary>
        public const float Spread = 4f;

        /// <summary>How widely a fish wanders around the school's home as the way in starts, in metres.</summary>
        public const float Loose = 6f;

        /// <summary>How fast a fish of this speed swims this far along the way in.</summary>
        public static float Speed(float own, float share) => share >= 1f ? own : Math.Min(own, MaxCruise);

        /// <summary>
        /// Seconds the swim in takes over this way: the rolled wait, or longer where the school's fish
        /// could not keep up with its home in that time.
        /// </summary>
        public static float Swim(float distance, float wait, float ownSpeed)
        {
            var least = Math.Max(0f, wait);
            var cruise = Speed(ownSpeed, 0f);
            if (cruise <= 0f || distance <= 0f) return least;
            return Math.Max(least, distance * Lead / cruise);
        }

        /// <summary>How much of the way in the school's home has come, from 0 where it appeared to 1 at the float.</summary>
        public static float Share(float sinceCame, float seconds)
        {
            if (float.IsNaN(sinceCame) || sinceCame <= 0f) return 0f;
            if (seconds <= 0f) return 1f;
            return Math.Min(1f, sinceCame / seconds);
        }

        /// <summary>How far under the surface a fish of a school that started deep swims this far along the way in.</summary>
        public static float Depth(float share, float startUnder, float swimUnder)
        {
            var t = Math.Min(1f, Math.Max(0f, share));
            return startUnder + (swimUnder - startUnder) * t;
        }

        /// <summary>How widely a fish wanders around the school's home this far along the way in, narrowing to the circle around the float.</summary>
        public static float SwimRange(float share, float circle)
        {
            var t = Math.Min(1f, Math.Max(0f, share));
            return Loose + (circle - Loose) * t;
        }
    }
}
