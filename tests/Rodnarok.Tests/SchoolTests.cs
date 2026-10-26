using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The bait draws a school of its fish, as big as the game's own groups of that fish. Any fish
    /// of it may take the bait, and when one is hooked the rest scatter. Rodnarök's fish leave
    /// once the float is gone, and no fish of the world's own is ever taken away.
    /// </summary>
    public class SchoolTests
    {
        private static readonly GroupSize Perch = new GroupSize(2, 6);

        [Fact]
        public void ASchoolIsAsBigAsTheGamesOwnGroupsOfThatFishEachSizeAsLikely()
        {
            var sizes = Enumerable.Range(0, 100).Select(i => School.Size(Perch, FishingRules.Default, (i + 0.5) / 100)).ToList();
            Assert.Equal(2, sizes.Min());
            Assert.Equal(6, sizes.Max());
            foreach (var size in Enumerable.Range(2, 5)) Assert.Equal(20, sizes.Count(s => s == size));
        }

        [Fact]
        public void ARollOfOneStillGivesTheLargestSchool()
        {
            Assert.Equal(6, School.Size(Perch, FishingRules.Default, 1.0));
        }

        [Fact]
        public void WithSchoolsOffOneFishComes()
        {
            var rules = FishingRules.Default.With(schools: false);
            Assert.Equal(1, School.Size(Perch, rules, 0.9));
            Assert.Equal(1, School.Size(GroupSize.Unknown, rules, 0.9));
        }

        [Fact]
        public void AFishTheWorldNeverSpawnsInGroupsComesInASmallSchool()
        {
            // The cave fish has no group of its own in the world's spawns.
            var sizes = Enumerable.Range(0, 30).Select(i => School.Size(GroupSize.Unknown, FishingRules.Default, (i + 0.5) / 30)).Distinct().OrderBy(s => s);
            Assert.Equal(new[] { 2, 3, 4 }, sizes);
        }

        [Theory]
        [InlineData(3, 1, 1, 3)]
        [InlineData(0, 2, 1, 2)]
        public void AGroupSetTheWrongWayRoundOrFromNoneStillGivesASchoolOfAtLeastOne(int min, int max, int smallest, int largest)
        {
            var sizes = Enumerable.Range(0, 30).Select(i => School.Size(new GroupSize(min, max), FishingRules.Default, (i + 0.5) / 30)).ToList();
            Assert.Equal(smallest, sizes.Min());
            Assert.Equal(largest, sizes.Max());
        }

        [Fact]
        public void AnyFishOfTheSchoolMayTakeTheBaitEachAsLikely()
        {
            var biters = Enumerable.Range(0, 100).Select(i => School.Biter(5, (i + 0.5) / 100)).ToList();
            foreach (var fish in Enumerable.Range(0, 5)) Assert.Equal(20, biters.Count(b => b == fish));
            Assert.Equal(4, School.Biter(5, 1.0));
        }

        [Fact]
        public void WhenOneIsHookedTheRestScatter()
        {
            Assert.Equal(new[] { 0, 1, 3, 4 }, School.Scatter(5, hooked: 2));
            Assert.Empty(School.Scatter(1, hooked: 0));
        }

        [Fact]
        public void RodnaroksFishLeaveOnceTheFloatHasBeenGoneAWhile()
        {
            Assert.False(School.Remove(ours: true, floatAlive: true, sinceFloatGone: 100f, leaveSeconds: 10f));
            Assert.False(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 9.9f, leaveSeconds: 10f));
            Assert.True(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 10f, leaveSeconds: 10f));
        }

        [Fact]
        public void AFishOfTheWorldsOwnIsNeverTakenAway()
        {
            Assert.False(School.Remove(ours: false, floatAlive: false, sinceFloatGone: 1000f, leaveSeconds: 10f));
        }

        [Fact]
        public void SchoolsAreOnBumpsOffAndFishLeaveTenSecondsAfterTheFloatByDefault()
        {
            var rules = FishingRules.Default;
            Assert.True(rules.Schools);
            Assert.False(rules.Bumps);
            Assert.Equal(10f, rules.LeaveSeconds);
        }
    }
}
