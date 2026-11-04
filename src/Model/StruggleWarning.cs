using System;

namespace Rodnarok
{
    /// <summary>
    /// A hooked fish shows its next struggle coming. The game decides when the next struggle starts as
    /// one ends (<c>Fish.m_nextEscape</c>, a rest of <c>m_escapeWaitMin..Max</c>), so the warning runs on
    /// that time: the float lies still until <see cref="RestlessFrom"/> before it, then trembles toward the
    /// fish more and more (the bite's <see cref="FloatTremble"/>, its closeness standing for the
    /// restlessness); the line goes taut <see cref="TautSeconds"/> before it, shorter for a faster fish by
    /// its pull (<see cref="Tension.Pull"/>) and for each star, and quivers. Through the struggle the line
    /// stays taut and quivers by the strain on it.
    /// </summary>
    internal static class StruggleWarning
    {
        /// <summary>Seconds before a struggle the float starts growing restless: a still float has at least this long.</summary>
        public const float RestlessFrom = 1.5f;

        /// <summary>Seconds before a Perch's struggle the line goes taut.</summary>
        public const float TautBefore = 0.6f;

        /// <summary>The share of the taut moment each star of the fish takes off.</summary>
        public const float StarShare = 0.1f;

        /// <summary>The shortest taut moment, in seconds, for the fastest starred fish.</summary>
        public const float ShortestTaut = 0.25f;

        /// <summary>How strongly a line taut before the struggle quivers, a share of a struggle's at full strain.</summary>
        public const float WarnQuiver = 0.2f;

        /// <summary>How far, in metres, the middle of the line swings at the strongest quiver.</summary>
        public const float MaxQuiverMetres = 0.25f;

        /// <summary>How many times a second the line swings.</summary>
        public const float QuiverHz = 9f;

        /// <summary>How restless the float is, from 0 still to 1 at the struggle, with this many seconds to go; still when none is known.</summary>
        public static float Restlessness(float untilStruggle)
        {
            if (float.IsNaN(untilStruggle) || untilStruggle >= RestlessFrom) return 0f;
            var x = Clamp01((RestlessFrom - untilStruggle) / RestlessFrom);
            // Eased in so it starts gently but shows at once, and keeps growing to the end.
            return x * (2f - x);
        }

        /// <summary>Seconds before this fish's struggle the line goes taut: a Perch's, shortened by its pull and its stars (quality 1 for none).</summary>
        public static float TautSeconds(float fishSpeed, int quality, FishingRules rules)
        {
            var stars = Math.Max(0, quality - 1);
            var share = Math.Max(0f, 1f - StarShare * stars);
            return Math.Max(ShortestTaut, TautBefore / Tension.Pull(fishSpeed, rules) * share);
        }

        /// <summary>Whether the line is taut: through a struggle, and from the taut moment before one.</summary>
        public static bool Taut(bool struggling, float untilStruggle, float tautSeconds) =>
            struggling || (!float.IsNaN(untilStruggle) && untilStruggle <= tautSeconds);

        /// <summary>How strongly the line quivers, 0 to 1: a little once taut before a struggle, through one by the strain, whole at snapping.</summary>
        public static float Quiver(bool struggling, bool taut, float strain)
        {
            if (struggling) return WarnQuiver + (1f - WarnQuiver) * Clamp01(strain);
            return taut ? WarnQuiver : 0f;
        }

        /// <summary>
        /// How far, in metres, the line swings aside at a share of the way along it (0 at the rod, 1 at
        /// the float) at a time in seconds: both ends held, the middle swinging most.
        /// </summary>
        public static float QuiverOffset(float quiver, float along, float seconds)
        {
            var shape = (float)Math.Sin(Math.PI * Clamp01(along));
            var swing = (float)Math.Sin(2 * Math.PI * QuiverHz * seconds);
            return MaxQuiverMetres * Clamp01(quiver) * shape * swing;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
