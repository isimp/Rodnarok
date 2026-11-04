using System;

namespace Rodnarok
{
    /// <summary>
    /// The Trollfish's sign as it is seen from the shore: a hump of water standing up from the
    /// surface, facing the one looking, a bright crest over a darker band. It surges every
    /// <see cref="SurgeSeconds"/> from <see cref="Low"/> to <see cref="High"/> metres, quickly up and
    /// slowly settling, throwing spray off its front as it rises; it is long across the view and
    /// short seen end-on, higher toward its front, and a fish barely moving barely raises it.
    /// </summary>
    internal static class TrollHump
    {
        /// <summary>Seconds from one surge to the next.</summary>
        public const float SurgeSeconds = 0.9f;

        /// <summary>The hump's height at rest between surges and at the top of a surge, in metres, for a moving fish.</summary>
        public const float Low = 0.13f;
        public const float High = 0.3f;

        /// <summary>The share of a surge spent rising; the rest settles.</summary>
        private const float Rise = 0.25f;

        /// <summary>The share of a surge during which spray flies.</summary>
        private const float SprayFor = 0.35f;

        /// <summary>How high the hump stands at a share <paramref name="phase"/> through a surge, for a fish moving at a share of full speed.</summary>
        public static float Height(float phase, float moving)
        {
            var p = phase - (float)Math.Floor(phase);
            float surge;
            if (p < Rise) surge = (float)Math.Sin(p / Rise * Math.PI / 2);
            else
            {
                var settle = (float)Math.Cos((p - Rise) / (1f - Rise) * Math.PI / 2);
                surge = settle * settle;
            }
            return (Low + (High - Low) * surge) * (0.35f + 0.65f * Clamp01(moving));
        }

        /// <summary>How strongly spray flies off the hump's front at a share through a surge: only as it rises and just after.</summary>
        public static float Spray(float phase)
        {
            var p = phase - (float)Math.Floor(phase);
            return p < SprayFor ? (float)Math.Sin(p / SprayFor * Math.PI) : 0f;
        }

        /// <summary>The hump's length in metres by how much the fish swims across the view, 1 across to 0 toward or away from the one looking.</summary>
        public static float Length(float across) => 0.7f + 1f * Clamp01(Math.Abs(across));

        /// <summary>
        /// The hump's shape along its length, <paramref name="u"/> from -1 at one end to 1 at the other:
        /// 1 at its middle, meeting the water at its ends, the half toward <paramref name="front"/>
        /// (1 or -1) standing higher than the half behind.
        /// </summary>
        public static float Arch(float u, float front)
        {
            if (u <= -1f || u >= 1f) return 0f;
            var round = (float)Math.Sqrt(1f - u * u);
            return u * front >= 0f ? round : round * 0.8f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
