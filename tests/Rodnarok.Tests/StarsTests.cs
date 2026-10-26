using System.Collections.Generic;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Each fish of a school rolls its quality as the world's spawns roll a fish's: from the
    /// lowest level up, one more for every roll of 0 to 100 at or under the level-up chance,
    /// stopping at the first roll over it or at the highest level.
    /// </summary>
    public class StarsTests
    {
        [Fact]
        public void ARollOverTheChanceStopsTheClimb()
        {
            Assert.Equal(2, Stars.Roll(1, 5, 10f, Rolls(5f, 50f, 1f)));
        }

        [Fact]
        public void RollsAtOrUnderTheChanceClimbToTheHighestLevelAndNoFurther()
        {
            Assert.Equal(5, Stars.Roll(1, 5, 10f, Rolls(10f, 0f, 3f, 9f, 1f, 1f)));
        }

        [Fact]
        public void AFirstRollOverTheChanceLeavesTheLowestLevel()
        {
            Assert.Equal(1, Stars.Roll(1, 5, 10f, Rolls(10.5f)));
        }

        [Fact]
        public void TheLowestLevelIsWhereTheClimbStarts()
        {
            Assert.Equal(3, Stars.Roll(3, 5, 10f, Rolls(99f)));
        }

        private static System.Func<float> Rolls(params float[] rolls)
        {
            var queue = new Queue<float>(rolls);
            return () => queue.Count > 0 ? queue.Dequeue() : 100f;
        }
    }
}
