namespace Rodnarok
{
    /// <summary>
    /// While a school is at the float and no fish bites yet, the crosshair ring tells how close the fish
    /// is: at the bite's size, faint (<see cref="Faint"/>) with the fish at the edge of the picked fish's
    /// hover (<see cref="Hover"/>) or beyond, growing stronger as it comes closer across the water, whole
    /// within <see cref="Reach"/>, the reach a fish bites from.
    /// </summary>
    internal static class ApproachRing
    {
        /// <summary>The reach a fish bites from, in metres (as <c>Bites.Reach</c>).</summary>
        public const float Reach = 0.6f;

        /// <summary>How far from the float the picked fish hovers, in metres (as <see cref="Bumps.Hover"/>).</summary>
        public const float Hover = Bumps.Hover;

        /// <summary>How strongly the ring shows with the fish at the hover's edge or beyond.</summary>
        public const float Faint = 0.15f;

        /// <summary>How strongly the ring shows with the fish this far from the float across the water: whole in reach, <see cref="Faint"/> at the hover's edge or beyond.</summary>
        public static float Strength(float distance)
        {
            var t = (distance - Reach) / (Hover - Reach);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return 1f - (1f - Faint) * t;
        }
    }
}
