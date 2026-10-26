using System;

namespace Rodnarok
{
    /// <summary>
    /// A fish's quality, rolled as the world's spawns roll it: from the lowest level up, one more
    /// for each roll of 0 to 100 at or under the level-up chance, stopping at the first roll over
    /// it or at the highest level. The game draws a fish larger for each level above the first.
    /// </summary>
    internal static class Stars
    {
        public static int Roll(int minLevel, int maxLevel, float chancePercent, Func<float> roll0To100)
        {
            var level = minLevel;
            while (level < maxLevel && roll0To100() <= chancePercent) level++;
            return level;
        }
    }
}
