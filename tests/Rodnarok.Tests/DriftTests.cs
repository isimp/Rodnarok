using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A school drifts in rather than rushing: it starts loose out in the water, its home creeps to
    /// the float over a while, its fish cruise slowly and wander on the way, and it gathers around
    /// the float only at the end. No fish takes the bait before the school is in.
    /// </summary>
    public class DriftTests
    {
        [Fact]
        public void ASchoolTakesAboutTwelveSecondsToComeInNotAFewSeconds()
        {
            Assert.Equal(0f, Drift.Share(0f));
            Assert.Equal(0.5f, Drift.Share(Drift.Seconds / 2f), 3);
            Assert.Equal(1f, Drift.Share(Drift.Seconds));
            Assert.Equal(1f, Drift.Share(Drift.Seconds * 3f));
            Assert.InRange(Drift.Seconds, 10f, 15f);
        }

        [Fact]
        public void TheSchoolsHomeMovesInSteadilyNeverBackOut()
        {
            var shares = Enumerable.Range(0, 50).Select(i => Drift.Share(i * Drift.Seconds / 40f)).ToList();
            for (var i = 1; i < shares.Count; i++) Assert.True(shares[i] >= shares[i - 1]);
        }

        [Theory]
        [InlineData(-3f)]
        [InlineData(float.NaN)]
        public void ATimeBeforeTheSchoolCameCountsAsItsStart(float since)
        {
            Assert.Equal(0f, Drift.Share(since));
        }

        [Fact]
        public void OnTheWayInEveryFishCruisesAtOnePaceNoFasterThanItsOwn()
        {
            Assert.Equal(Drift.Cruise, Drift.Speed(10f, 0.3f));
            Assert.Equal(Drift.Cruise, Drift.Speed(3f, 0.99f));
            Assert.Equal(1.5f, Drift.Speed(1.5f, 0.3f));
        }

        [Fact]
        public void OnceInEachFishSwimsAtItsOwnSpeedAgain()
        {
            Assert.Equal(10f, Drift.Speed(10f, 1f));
            Assert.Equal(3f, Drift.Speed(3f, 1f));
        }

        [Fact]
        public void NoFishFallsBehindTheSchoolOnTheWayIn()
        {
            var homePace = Drift.From / Drift.Seconds;
            Assert.True(homePace * 1.5f <= Drift.Cruise, "the home moves at " + homePace + " m/s, the fish cruise at " + Drift.Cruise);
            Assert.True(Drift.Cruise <= GameFish.SlowestSpeed, "the slowest fish of the game swims " + GameFish.SlowestSpeed + " m/s");
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
            Assert.False(School.ReadyToBite(Drift.Seconds - 0.5f, near: 6, size: 6));
        }

        [Fact]
        public void OnceInAFishTakesTheBaitWhenHalfTheSchoolIsNear()
        {
            Assert.True(School.ReadyToBite(Drift.Seconds + 0.5f, near: 3, size: 6));
            Assert.False(School.ReadyToBite(Drift.Seconds + 0.5f, near: 2, size: 6));
        }

        [Fact]
        public void AStragglingSchoolStillGetsABiteAWhileAfterItCame()
        {
            Assert.True(School.ReadyToBite(20.5f, near: 0, size: 6));
        }
    }
}
