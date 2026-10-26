using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>A fish comes to the float within the configured wait, short by default.</summary>
    public class ArrivalTests
    {
        [Theory]
        [InlineData(0.0, 10f)]
        [InlineData(0.5, 20f)]
        [InlineData(0.999, 29.98f)]
        public void AFishComesWithinTheConfiguredWait(double roll, float seconds)
        {
            Assert.Equal(seconds, Arrival.Seconds(10f, 30f, roll), 2);
        }

        [Fact]
        public void TheWaitIsTenToThirtySecondsByDefault()
        {
            Assert.Equal(10f, FishingRules.Default.WaitMin);
            Assert.Equal(30f, FishingRules.Default.WaitMax);
        }

        [Fact]
        public void BoundsSetTheWrongWayRoundStillGiveAWaitBetweenThem()
        {
            var wait = Arrival.Seconds(30f, 10f, 0.25);
            Assert.InRange(wait, 10f, 30f);
            Assert.Equal(15f, wait, 2);
        }

        [Fact]
        public void ANegativeBoundMeansAtOnce()
        {
            Assert.Equal(0f, Arrival.Seconds(-5f, -1f, 0.5));
        }
    }
}
