using System;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A fish going for the bait slows as it closes on the hook, so it can meet it. The game steers
    /// a fish at its own speed and turning rate and counts it at the hook with two of the three
    /// axes; Rodnarök counts it only with its mouth within reach through all three. A Pike, 5 m/s
    /// turning 100 degrees a second, circles 2.9 m wide and a Tuna 7 m, so at their own speed they
    /// would circle the hook and never bite. Within 3 m of the hook a fish swims no faster than lets
    /// it turn inside half the reach, its own speed where that is slower already.
    /// </summary>
    public class BiteApproachTests
    {
        private const float Reach = 0.6f;

        [Theory]
        [InlineData(5f, 100f)]
        [InlineData(10f, 80f)]
        [InlineData(3f, 100f)]
        [InlineData(2f, 110f)]
        public void CloseToTheHookAFishTurnsInsideHalfTheReach(float own, float turn)
        {
            var speed = BiteApproach.Speed(own, turn, Reach, metresToHook: 1f);
            var radius = speed / (turn * (float)Math.PI / 180f);
            Assert.True(radius <= Reach / 2f + 0.001f, "circles " + radius + " m wide");
            Assert.True(speed > 0.3f, "crawls at " + speed);
        }

        [Fact]
        public void APikeAtItsOwnSpeedWouldCircleTheHookWiderThanItsReach()
        {
            Assert.True(5f / (100f * (float)Math.PI / 180f) > Reach);
            Assert.True(10f / (80f * (float)Math.PI / 180f) > Reach);
        }

        [Fact]
        public void FarFromTheHookAFishSwimsAtItsOwnSpeed()
        {
            Assert.Equal(5f, BiteApproach.Speed(5f, 100f, Reach, metresToHook: 3.5f), 3);
            Assert.Equal(5f, BiteApproach.Speed(5f, 100f, Reach, metresToHook: 20f), 3);
        }

        [Fact]
        public void AFishAlreadySlowEnoughKeepsItsOwnSpeed()
        {
            Assert.Equal(0.4f, BiteApproach.Speed(0.4f, 100f, Reach, metresToHook: 1f), 3);
        }

        [Fact]
        public void TheSlowingStartsThreeMetresFromTheHook()
        {
            Assert.Equal(3f, BiteApproach.From);
            Assert.True(BiteApproach.Speed(5f, 100f, Reach, 2.9f) < 1f);
        }
    }
}
