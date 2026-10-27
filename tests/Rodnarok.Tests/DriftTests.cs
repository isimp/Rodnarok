using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A school swims in rather than rushing, and appears where nobody sees it appear: it starts
    /// loose and out of sight, 40 m from every player or 4 m under the surface, the moment the
    /// float lands, and the wait is its swim in. Its home creeps to the float over that time, its
    /// fish cruise slowly and wander on the way, a deep start rises as it comes, and it gathers
    /// around the float only at the end. No fish takes the bait before the school is in.
    /// </summary>
    public class DriftTests
    {
        private static readonly FishingRules Rules = FishingRules.Default;

        [Fact]
        public void AStartFortyMetresFromEveryPlayerIsOutOfSight()
        {
            Assert.True(Arrival.StartOutOfSight(nearestPlayer: 40f, underSurface: 1.75f, Rules));
            Assert.False(Arrival.StartOutOfSight(nearestPlayer: 39f, underSurface: 1.75f, Rules));
        }

        [Fact]
        public void AStartFourMetresUnderTheSurfaceIsOutOfSightHoweverNear()
        {
            Assert.True(Arrival.StartOutOfSight(nearestPlayer: 8f, underSurface: 4f, Rules));
            Assert.False(Arrival.StartOutOfSight(nearestPlayer: 8f, underSurface: 3.5f, Rules));
        }

        [Fact]
        public void TheSwimTakesTheWaitWhenTheFishCoverTheWayInThatFast()
        {
            Assert.Equal(20f, Drift.Swim(distance: 10f, wait: 20f, ownSpeed: 3f), 3);
            Assert.Equal(25f, Drift.Swim(distance: 14f, wait: 25f, ownSpeed: 2f), 3);
        }

        [Fact]
        public void AFarStartTakesAsLongAsItsFishNeedToStayTogether()
        {
            Assert.Equal(20f, Drift.Swim(distance: 40f, wait: 10f, ownSpeed: 3f), 3);
            Assert.Equal(30f, Drift.Swim(distance: 40f, wait: 10f, ownSpeed: 2f), 3);
            Assert.Equal(20f, Drift.Swim(distance: 40f, wait: 10f, ownSpeed: 10f), 3);
        }

        [Theory]
        [InlineData(2f)]
        [InlineData(3f)]
        [InlineData(3.5f)]
        [InlineData(10f)]
        public void NoFishFallsBehindItsSchoolOnTheWayIn(float ownSpeed)
        {
            foreach (var distance in new[] { 6f, 14f, 40f, 60f })
            {
                var pace = distance / Drift.Swim(distance, 10f, ownSpeed);
                Assert.True(pace * 1.5f <= Drift.Speed(ownSpeed, 0.5f) + 0.001f, "home " + pace + " m/s, fish " + Drift.Speed(ownSpeed, 0.5f) + " m/s");
            }
        }

        [Fact]
        public void TheSchoolsHomeMovesInSteadilyNeverBackOut()
        {
            var shares = Enumerable.Range(0, 50).Select(i => Drift.Share(i * 0.5f, 20f)).ToList();
            for (var i = 1; i < shares.Count; i++) Assert.True(shares[i] >= shares[i - 1]);
            Assert.Equal(0f, Drift.Share(0f, 20f));
            Assert.Equal(0.5f, Drift.Share(10f, 20f), 3);
            Assert.Equal(1f, Drift.Share(25f, 20f));
        }

        [Theory]
        [InlineData(-3f)]
        [InlineData(float.NaN)]
        public void ATimeBeforeTheSchoolCameCountsAsItsStart(float since)
        {
            Assert.Equal(0f, Drift.Share(since, 20f));
        }

        [Fact]
        public void ASwimOfNoTimeIsInAtOnce()
        {
            Assert.Equal(1f, Drift.Share(0.1f, 0f));
        }

        [Fact]
        public void OnTheWayInEveryFishCruisesSlowlyAndNoFasterThanItsOwn()
        {
            Assert.Equal(Drift.MaxCruise, Drift.Speed(10f, 0.3f));
            Assert.Equal(2f, Drift.Speed(2f, 0.3f));
            Assert.True(Drift.MaxCruise <= 3f);
        }

        [Fact]
        public void OnceInEachFishSwimsAtItsOwnSpeedAgain()
        {
            Assert.Equal(10f, Drift.Speed(10f, 1f));
            Assert.Equal(3f, Drift.Speed(3f, 1f));
        }

        [Fact]
        public void ADeepStartRisesToItsSwimmingDepthAsItComesIn()
        {
            Assert.Equal(4f, Drift.Depth(0f, startUnder: 4f, swimUnder: 1.75f), 3);
            Assert.Equal(1.75f, Drift.Depth(1f, startUnder: 4f, swimUnder: 1.75f), 3);
            var depths = Enumerable.Range(0, 11).Select(i => Drift.Depth(i / 10f, 4f, 1.75f)).ToList();
            for (var i = 1; i < depths.Count; i++) Assert.True(depths[i] <= depths[i - 1]);
        }

        [Fact]
        public void TheSchoolStartsLooseAndGathersAroundTheFloatAtTheEnd()
        {
            const float circle = 4f;
            Assert.True(Drift.Spread >= 3f, "the school starts spread over " + Drift.Spread + " m");
            Assert.True(Drift.SwimRange(0f, circle) > circle);
            Assert.Equal(circle, Drift.SwimRange(1f, circle));
            var ranges = Enumerable.Range(0, 11).Select(i => Drift.SwimRange(i / 10f, circle)).ToList();
            for (var i = 1; i < ranges.Count; i++) Assert.True(ranges[i] <= ranges[i - 1]);
        }

        [Fact]
        public void NoFishTakesTheBaitBeforeTheSchoolIsIn()
        {
            Assert.False(School.ReadyToBite(sinceCame: 19.5f, swimSeconds: 20f, near: 6, size: 6));
        }

        [Fact]
        public void OnceInAFishTakesTheBaitWhenHalfTheSchoolIsNear()
        {
            Assert.True(School.ReadyToBite(sinceCame: 20.5f, swimSeconds: 20f, near: 3, size: 6));
            Assert.False(School.ReadyToBite(sinceCame: 20.5f, swimSeconds: 20f, near: 2, size: 6));
        }

        [Fact]
        public void AStragglingSchoolStillGetsABiteAWhileAfterItIsIn()
        {
            Assert.False(School.ReadyToBite(sinceCame: 27f, swimSeconds: 20f, near: 0, size: 6));
            Assert.True(School.ReadyToBite(sinceCame: 28.5f, swimSeconds: 20f, near: 0, size: 6));
        }
    }
}
