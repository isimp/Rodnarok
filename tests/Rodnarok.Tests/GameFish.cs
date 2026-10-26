using System.Collections.Generic;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The fish of Valheim 1.0.20 and the bait each takes, as the game's prefabs set them. The
    /// in-game self-test checks the installed game still agrees.
    /// </summary>
    internal static class GameFish
    {
        public static readonly IReadOnlyList<FishKind> All = new[]
        {
            new FishKind("Fish1", new[] { "FishingBait" }),
            new FishKind("Fish2", new[] { "FishingBait" }),
            new FishKind("Fish3", new[] { "FishingBaitOcean" }),
            new FishKind("Fish4_cave", new[] { "FishingBaitCave" }),
            new FishKind("Fish5", new[] { "FishingBaitForest" }),
            new FishKind("Fish6", new[] { "FishingBaitSwamp" }),
            new FishKind("Fish7", new[] { "FishingBaitPlains" }),
            new FishKind("Fish8", new[] { "FishingBaitOcean" }),
            new FishKind("Fish9", new[] { "FishingBaitMistlands" }),
            new FishKind("Fish10", new[] { "FishingBaitDeepNorth" }),
            new FishKind("Fish11", new[] { "FishingBaitAshlands" }),
            new FishKind("Fish12", new[] { "FishingBaitMistlands" }),
        };

        public static BaitTable Table() => new BaitTable(All);
    }
}
