using System;

namespace Rodnarok
{
    /// <summary>
    /// A school is wary of the one fishing. It hears the game's own noise, which a player makes
    /// walking (15 m) or running and jumping (30 m) and fades at 4 m a second, while sneaking,
    /// sitting or standing still make none: a player heard at the float or the school startles it.
    /// A float dragged quickly through the school startles it too, while one barely moved lets it
    /// follow. Startled, a school darts off and comes back once all has been quiet a while;
    /// startled a second time, it leaves. Close to the one fishing and in shallow water it hesitates
    /// long before biting and often gives up and swims off; far out over deep water it bites as before.
    /// </summary>
    internal static class Wary
    {
        /// <summary>How long all must be quiet before a startled school comes back, in seconds.</summary>
        public const float QuietBeforeComingBack = 8f;

        /// <summary>How many startles a school takes before it leaves.</summary>
        public const int StartlesToLeave = 2;

        /// <summary>How fast a float may move through the school, in metres a second, before it startles it.</summary>
        public const float FloatSpeed = 0.6f;

        /// <summary>Within how far of the one fishing a fish is shyest, and from how far out it is not shy, in metres.</summary>
        public const float CloseIn = 5f;
        public const float FarOut = 15f;

        /// <summary>Under how much water a fish is shyest, and from how deep it is not shy, in metres.</summary>
        public const float Shallow = 1f;
        public const float Deep = 3f;

        /// <summary>How long the shyest fish hesitates before biting, in seconds.</summary>
        public const float LongestHesitation = 20f;

        /// <summary>The share of schools of the shyest fish that give up and swim off without biting.</summary>
        public const double MostGivingUp = 0.5;

        /// <summary>Whether a player's noise reaches a place: the game's own rule, its noise range against the distance.</summary>
        public static bool Heard(float distance, float noise) => distance < noise;

        /// <summary>Whether a startled school comes back, all quiet for this long.</summary>
        public static bool ComesBack(float quietFor) => quietFor >= QuietBeforeComingBack;

        /// <summary>Whether a school startled this many times leaves.</summary>
        public static bool Leaves(int startles) => startles >= StartlesToLeave;

        /// <summary>Whether a float moving this fast startles a school, near it or not.</summary>
        public static bool FloatStartles(float speed, bool schoolNear) => schoolNear && speed > FloatSpeed;

        /// <summary>How shy a fish is at the float, from 0 to 1: the shyer of how close the one fishing stands and how shallow the water is.</summary>
        public static float Shyness(float anglerDistance, float depth)
        {
            var close = 1f - Clamp01((anglerDistance - CloseIn) / (FarOut - CloseIn));
            var shallow = 1f - Clamp01((depth - Shallow) / (Deep - Shallow));
            return Math.Max(close, shallow);
        }

        /// <summary>How long a fish this shy hesitates before biting, in seconds.</summary>
        public static float Hesitation(float shyness) => Clamp01(shyness) * LongestHesitation;

        /// <summary>Whether a school this shy gives up and swims off without biting, for a roll from 0 to 1.</summary>
        public static bool GivesUp(float shyness, double roll) => roll < Clamp01(shyness) * MostGivingUp;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
