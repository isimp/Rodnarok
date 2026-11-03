using System;
using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>
    /// How the water a sign is on shapes it. The game makes its waves as strong as the water's depth
    /// setting at the place (lowered by a Serpent trophy's calm) times the wind, 0 to 1, and that is
    /// the water's roughness here: calm water shows a sign whole, rough water fainter, down to
    /// <see cref="MinClarity"/>, and broken in two places along a ring. Signs drift downwind and,
    /// within <see cref="Near"/> of the camera, rise and fall with the waves, sampled at a few points
    /// and followed between them.
    /// </summary>
    internal static class WaterMood
    {
        /// <summary>How faint the roughest water makes a sign, as a share of its strength.</summary>
        public const float MinClarity = 0.45f;

        /// <summary>The roughness under which a ring stays whole.</summary>
        public const float WholeUnder = 0.2f;

        /// <summary>How deep a break goes in the roughest water, as a share of the ring's strength taken away.</summary>
        public const float BreakDepth = 0.85f;

        /// <summary>How fast a sign drifts downwind in the strongest wind, in metres a second.</summary>
        public const float DriftSpeed = 0.35f;

        /// <summary>How far from the camera, in metres, a sign follows the waves; farther ones lie flat.</summary>
        public const float Near = 40f;

        /// <summary>How rough the water is, as the game's own wave strength there: depth setting times wind, 0 to 1.</summary>
        public static float Roughness(float depth, float wind) => Clamp01(Clamp01(depth) * Clamp01(wind));

        /// <summary>How clearly a sign shows on water of a roughness, 1 on calm water down to <see cref="MinClarity"/>.</summary>
        public static float Clarity(float roughness) => 1f - (1f - MinClarity) * Clamp01(roughness);

        /// <summary>
        /// How much of a ring shows at a share <paramref name="t"/> round it on water of a roughness:
        /// whole under <see cref="WholeUnder"/>, broken in two places beyond, the breaks deeper the
        /// rougher, each ring's breaks turned by its own <paramref name="seed"/>.
        /// </summary>
        public static float Broken(float roughness, float t, float seed)
        {
            var depth = BreakDepth * Clamp01((roughness - WholeUnder) / (1f - WholeUnder));
            if (depth <= 0f) return 1f;
            var wave = (float)Math.Cos(2.0 * Math.PI * (2f * t + seed));
            var gap = wave > 0f ? wave * wave : 0f;
            return Clamp01(1f - depth * gap);
        }

        /// <summary>How far downwind a sign has drifted after a number of seconds in a wind of a strength.</summary>
        public static float Drift(float wind, float seconds) => DriftSpeed * Clamp01(wind) * Math.Max(0f, seconds);

        /// <summary>Whether a sign at a distance from the camera follows the waves.</summary>
        public static bool FollowsWaves(float metres) => metres <= Near;

        /// <summary>A height round a loop at a share <paramref name="t"/> of the way, between evenly spaced samples, the last joining the first.</summary>
        public static float AroundLoop(IReadOnlyList<float> samples, float t)
        {
            var n = samples.Count;
            if (n == 0) return 0f;
            var x = (t - (float)Math.Floor(t)) * n;
            var i = (int)x % n;
            var f = x - (float)Math.Floor(x);
            return samples[i] + (samples[(i + 1) % n] - samples[i]) * f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
