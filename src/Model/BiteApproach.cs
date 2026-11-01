using System;

namespace Rodnarok
{
    /// <summary>
    /// How fast a fish going for the bait swims as it closes on the hook. The game steers a fish at
    /// its own speed and turning rate, so a fast fish circles wider than the reach it must bite
    /// from; within <see cref="From"/> of the hook it swims no faster than lets it turn inside half
    /// its distance to the hook, and at the hook inside half that reach.
    /// </summary>
    internal static class BiteApproach
    {
        /// <summary>How near the hook, in metres, a fish going for the bait starts to slow.</summary>
        public const float From = 3f;

        /// <summary>The speed of a fish going for the bait, from its own speed, its turning rate in degrees a second, the reach it bites from and its distance from the hook.</summary>
        public static float Speed(float own, float turnDegrees, float reach, float metresToHook)
        {
            if (metresToHook > From || turnDegrees <= 0f) return own;
            var turning = turnDegrees * (float)Math.PI / 180f;
            return Math.Min(own, turning * Math.Max(metresToHook, reach) / 2f);
        }
    }
}
