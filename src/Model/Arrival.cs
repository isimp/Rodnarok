using System;

namespace Rodnarok
{
    /// <summary>How long a fish takes to come to the float: a roll between the rules' shortest and longest wait.</summary>
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
    }
}
