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

        /// <summary>The speed of the game's slowest fish, the Anglerfish and the Tetra, in metres a second.</summary>
        public const float SlowestSpeed = 2f;

        /// <summary>Each fish's swimming speed in metres a second, the prefabs' m_speed in Valheim 1.0.21.</summary>
        public static readonly IReadOnlyDictionary<string, float> Speeds = new Dictionary<string, float>
        {
            ["Fish1"] = 3f,
            ["Fish2"] = 5f,
            ["Fish3"] = 10f,
            ["Fish4_cave"] = 2f,
            ["Fish5"] = 4f,
            ["Fish6"] = 3f,
            ["Fish7"] = 3f,
            ["Fish8"] = 3f,
            ["Fish9"] = 2f,
            ["Fish10"] = 4f,
            ["Fish11"] = 3.5f,
            ["Fish12"] = 3f,
        };

        /// <summary>
        /// Each fish's struggle and rest in seconds, the prefabs' m_escapeMin, m_escapeMax,
        /// m_escapeMaxPerLevel, m_escapeWaitMin and m_escapeWaitMax in Valheim 1.0.21.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (float Min, float Max, float PerStar, float RestMin, float RestMax)> Struggles =
            new Dictionary<string, (float, float, float, float, float)>
            {
                ["Fish1"] = (0.5f, 3f, 1.5f, 1.5f, 5f),
                ["Fish2"] = (0.5f, 3f, 1.5f, 1.5f, 5f),
                ["Fish3"] = (0.5f, 3f, 1.5f, 1.5f, 5f),
                ["Fish4_cave"] = (0.5f, 3f, 1.5f, 1.5f, 5f),
                ["Fish5"] = (0.5f, 3f, 1f, 1.5f, 5f),
                ["Fish6"] = (0.5f, 3f, 1.5f, 1.5f, 5f),
                ["Fish7"] = (0.5f, 3f, 1.5f, 1.5f, 5f),
                ["Fish8"] = (1f, 4f, 1.5f, 1.25f, 4f),
                ["Fish9"] = (1f, 4f, 1.5f, 1.25f, 4f),
                ["Fish10"] = (1f, 4f, 1.5f, 1.25f, 4f),
                ["Fish11"] = (1f, 4f, 1.5f, 1.25f, 4f),
                ["Fish12"] = (1f, 4f, 1.5f, 1.25f, 4f),
            };

        public static BaitTable Table() => new BaitTable(All);
    }
}
