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

        private static readonly FishingRules Rules = FishingRules.Default;

        [Fact]
        public void WhileItsFloatIsOutNoFishOfTheSchoolIsTakenAway()
        {
            Assert.False(School.Remove(ours: true, floatAlive: true, sinceFloatGone: 1000f, nearestPlayer: 500f, underSurface: 20f, Rules));
        }

        [Fact]
        public void ALeavingFishNearAPlayerAndNearTheSurfaceIsNotTakenAwayInSightBeforeTheBackstop()
        {
            Assert.False(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 20f, nearestPlayer: 15f, underSurface: 1.5f, Rules));
            Assert.False(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 20f, nearestPlayer: 39.9f, underSurface: 3.9f, Rules));
        }

        [Fact]
        public void ALeavingFishIsTakenAwayOnceFortyMetresFromEveryPlayer()
        {
            Assert.True(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 5f, nearestPlayer: 40f, underSurface: 0.5f, Rules));
        }

        [Fact]
        public void ALeavingFishIsTakenAwayOnceFourMetresDeep()
        {
            Assert.True(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 5f, nearestPlayer: 10f, underSurface: 4f, Rules));
        }

        [Fact]
        public void AFishStuckNearbyIsTakenAwayAfterThirtySeconds()
        {
            Assert.False(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 29f, nearestPlayer: 5f, underSurface: 1f, Rules));
            Assert.True(School.Remove(ours: true, floatAlive: false, sinceFloatGone: 30f, nearestPlayer: 5f, underSurface: 1f, Rules));
        }

        [Fact]
        public void AFishOfTheWorldsOwnIsNeverTakenAway()
        {
            Assert.False(School.Remove(ours: false, floatAlive: false, sinceFloatGone: 1000f, nearestPlayer: 500f, underSurface: 20f, Rules));
        }

        [Fact]
        public void AFishABaitDrewIsCaughtOnlyWithTheRod()
        {
            Assert.True(School.Catchable(drawnByBait: true, byRod: true));
            Assert.False(School.Catchable(drawnByBait: true, byRod: false));
        }

        [Fact]
        public void AFishOfTheWorldsOwnIsLeftToEveryWayOfCatchingIt()
        {
            Assert.True(School.Catchable(drawnByBait: false, byRod: false));
            Assert.True(School.Catchable(drawnByBait: false, byRod: true));
        }

        [Fact]
        public void ASpookedSchoolNeverTakesTheBait()
        {
            Assert.False(School.ReadyToBite(sinceCame: 21f, swimSeconds: 20f, near: 6, size: 6, spooked: true));
            Assert.False(School.ReadyToBite(sinceCame: 100f, swimSeconds: 20f, near: 0, size: 6, spooked: true));
        }

        [Fact]
        public void SchoolsAreOnBumpsOffAndLeavingFishVanishOutOfSightByDefault()
        {
            Assert.True(Rules.Schools);
            Assert.False(Rules.Bumps);
            Assert.Equal(40f, Rules.OutOfSight);
            Assert.Equal(4f, Rules.DiveDepth);
            Assert.Equal(30f, Rules.LeaveBackstop);
        }
    }
}
