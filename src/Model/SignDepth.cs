using System;

namespace Rodnarok
{
    /// <summary>
    /// How deep a fish swims shows in its sign: from the bait's depth (the hook hangs 1 m under the
    /// float, <c>Bites.Hook</c>) up to the surface the sign shows as made; deeper it shows broader and
    /// fainter, as a swirl spreads before it reaches the surface, at its broadest from the depth a
    /// school out of sight dives at (<see cref="FishingRules.DiveDepth"/>, 4 m), about a Tuna's
    /// (its prefab's <c>m_minDepth</c> is 4). Most of the game's fish swim 1.5 to 3 m deep, so their
    /// sign shows a little spread and tightens as the one going for the bait rises to the hook.
    /// </summary>
    internal static class SignDepth
    {
        /// <summary>How deep under the surface a sign still shows as made, in metres: the bait's depth.</summary>
        public const float Crisp = 1f;

        /// <summary>How deep a sign is at its broadest, in metres: the dive depth of a school out of sight.</summary>
        public const float Broadest = 4f;

        /// <summary>How much bigger, and how much fainter, a sign shows at its broadest.</summary>
        public const float MostSpread = 0.6f;
        public const float MostFaded = 0.45f;

        /// <summary>How much bigger a sign shows for a fish this deep under the surface, in metres.</summary>
        public static float Size(float under) => 1f + MostSpread * Share(under);

        /// <summary>How bold a sign shows for a fish this deep under the surface, 1 as made.</summary>
        public static float Boldness(float under) => 1f - MostFaded * Share(under);

        /// <summary>How far between the bait's depth and the broadest a depth lies, 0 to 1; 0 for none read.</summary>
        private static float Share(float under)
        {
            if (float.IsNaN(under)) return 0f;
            return Math.Min(1f, Math.Max(0f, (under - Crisp) / (Broadest - Crisp)));
        }
    }
}
