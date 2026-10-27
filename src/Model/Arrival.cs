using System;

namespace Rodnarok
{
    /// <summary>What comes to a float once its wait is over.</summary>
    internal enum ArrivalChoice
    {
        /// <summary>A fish of the world's own that takes the bait is called to the float; nothing is spawned.</summary>
        Call,

        /// <summary>A school of the bait's fish comes.</summary>
        School,

        /// <summary>The caster's last school is still leaving; the new one comes once it is gone.</summary>
        Wait,

        /// <summary>The water is too small for a school, which is said once; fish of the world's own still bite.</summary>
        TooLittleWater,
    }

    /// <summary>
    /// How long a fish takes to come to the float, a roll between the rules' shortest and longest
    /// wait, and what comes then: a fish of the world's own that takes the bait, called from where
    /// it swims, before a school is spawned; a school only where the water gives it a place out of
    /// sight to come from and a way out again, and only once the caster's last school is gone.
    /// </summary>
    internal static class Arrival
    {
        /// <summary>Seconds until the fish comes, for a roll from 0 to 1; bounds the wrong way round are read the right way, and below zero as at once.</summary>
        public static float Seconds(float min, float max, double roll)
        {
            var low = Math.Max(0f, Math.Min(min, max));
            var high = Math.Max(0f, Math.Max(min, max));
            var t = Math.Min(1.0, Math.Max(0.0, roll));
            return (float)(low + (high - low) * t);
        }

        /// <summary>What comes to the float once the wait is over.</summary>
        public static ArrivalChoice Choose(bool worldFishToCall, bool waterAllowsSchool, bool lastSchoolGone, FishingRules rules)
        {
            if (worldFishToCall && rules.CallWorldFish) return ArrivalChoice.Call;
            if (!waterAllowsSchool) return ArrivalChoice.TooLittleWater;
            return lastSchoolGone ? ArrivalChoice.School : ArrivalChoice.Wait;
        }

        /// <summary>
        /// Whether a school appearing here is out of sight, by the same measure a leaving fish is taken
        /// away by: far from every player, or deep under the surface.
        /// </summary>
        public static bool StartOutOfSight(float nearestPlayer, float underSurface, FishingRules rules) =>
            nearestPlayer >= rules.OutOfSight || underSurface >= rules.DiveDepth;

        /// <summary>Whether the water allows a school: a place out of sight to come from, and a way out again to dive or swim out of sight.</summary>
        public static bool WaterAllowsSchool(bool placeToComeFrom, bool wayOut) => placeToComeFrom && wayOut;
    }
}
