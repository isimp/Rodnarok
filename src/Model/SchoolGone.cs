namespace Rodnarok
{
    /// <summary>Why a school was scared away for good.</summary>
    internal enum GoneWhy
    {
        Noise,
        Reeled,
        ShyClose,
        ShyShallow,
        Shot,
    }

    /// <summary>
    /// A school scared away for good tells the one fishing why: steps heard (their own, or another
    /// player's, named), the float reeled through it, too shy to bite close in or in shallow water,
    /// or a shot at one of its fish. A school that only darts off and comes back says nothing.
    /// </summary>
    internal static class SchoolGone
    {
        /// <summary>The line for the one fishing; <paramref name="noiseBy"/> names another player whose steps it was, null or empty for their own.</summary>
        public static string Text(GoneWhy why, string noiseBy)
        {
            switch (why)
            {
                case GoneWhy.Noise:
                    return string.IsNullOrEmpty(noiseBy) ? "Your steps scared the school away" : noiseBy + "'s steps scared the school away";
                case GoneWhy.Reeled:
                    return "Reeling through the school scared it away";
                case GoneWhy.ShyClose:
                    return "The school was too shy to bite so close to you";
                case GoneWhy.ShyShallow:
                    return "The school was too shy to bite in such shallow water";
                default:
                    return "The shot scared the school away";
            }
        }

        /// <summary>Why a school too shy to bite gave up: the shyer of how close the one fishing stands and how shallow the water is.</summary>
        public static GoneWhy Shy(float anglerDistance, float depth) =>
            Wary.Shyness(anglerDistance, depth: float.MaxValue) >= Wary.Shyness(anglerDistance: float.MaxValue, depth) ? GoneWhy.ShyClose : GoneWhy.ShyShallow;

        /// <summary>Why the fright that drove a school off did: steps heard, or else the float moved quickly through it.</summary>
        public static GoneWhy ByFright(bool heard) => heard ? GoneWhy.Noise : GoneWhy.Reeled;
    }
}
