using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The signs belong to the water they are on: they rise and fall with its waves, drift with the
    /// wind, and read by how rough it is. The game makes its waves as strong as the water's depth
    /// setting times the wind, the same figure a Serpent trophy's calm lowers, so calm water shows a
    /// sign crisp and whole, and rough water fainter and broken, never so faint it is gone.
    /// </summary>
    public class WaterMoodTests
    {
        [Theory]
        [InlineData(1f, 1f, 1f)]
        [InlineData(0.5f, 0.6f, 0.3f)]
        [InlineData(0f, 1f, 0f)]
        [InlineData(1f, 0f, 0f)]
        [InlineData(2f, 1f, 1f)]
        public void RoughnessIsTheGamesOwnWaveStrength(float depth, float wind, float roughness)
        {
            Assert.Equal(roughness, WaterMood.Roughness(depth, wind), 3);
        }

        [Fact]
        public void CalmWaterShowsASignWhole()
        {
            Assert.Equal(1f, WaterMood.Clarity(0f), 3);
            for (var i = 0; i < 8; i++) Assert.Equal(1f, WaterMood.Broken(0f, i / 8f, 0.3f), 3);
            Assert.Equal(1f, WaterMood.Broken(0.15f, 0.25f, 0f), 3);
        }

        [Fact]
        public void RoughWaterShowsASignFainterButNeverGone()
        {
            Assert.True(WaterMood.Clarity(0.5f) < WaterMood.Clarity(0.1f));
            Assert.Equal(WaterMood.MinClarity, WaterMood.Clarity(1f), 3);
            Assert.InRange(WaterMood.MinClarity, 0.35f, 0.6f);
            Assert.Equal(WaterMood.MinClarity, WaterMood.Clarity(3f), 3);
        }

        [Fact]
        public void RoughWaterBreaksARingInPlaces()
        {
            var along = Enumerable.Range(0, 64).Select(i => WaterMood.Broken(1f, i / 64f, 0f)).ToList();
            Assert.True(along.Min() < 0.35f, "the ring is never broken: " + along.Min());
            Assert.True(along.Max() > 0.95f, "the ring is broken all round: " + along.Max());
            // Two breaks, not one: half the ring further on looks the same.
            Assert.Equal(WaterMood.Broken(1f, 0.1f, 0f), WaterMood.Broken(1f, 0.6f, 0f), 3);
            Assert.True(along.All(a => a >= 0f && a <= 1f));
        }

        [Fact]
        public void BreaksFallInADifferentPlaceForEachRing()
        {
            Assert.NotEqual(WaterMood.Broken(1f, 0f, 0f), WaterMood.Broken(1f, 0f, 0.25f), 3);
        }

        [Fact]
        public void SignsDriftDownwindWithItsStrength()
        {
            Assert.Equal(0f, WaterMood.Drift(wind: 0f, seconds: 2f), 3);
            Assert.True(WaterMood.Drift(1f, 1f) > WaterMood.Drift(0.5f, 1f));
            Assert.True(WaterMood.Drift(1f, 2f) > WaterMood.Drift(1f, 1f));
            Assert.InRange(WaterMood.Drift(1f, 1f), 0.2f, 0.6f);
            Assert.Equal(WaterMood.Drift(1f, 1f), WaterMood.Drift(4f, 1f), 3);
        }

        [Fact]
        public void ARingFollowsTheWavesBetweenItsSamples()
        {
            var samples = new[] { 0f, 1f, 0f, -1f, 0f, 1f, 0f, -1f };
            Assert.Equal(0f, WaterMood.AroundLoop(samples, 0f), 3);
            Assert.Equal(1f, WaterMood.AroundLoop(samples, 1f / 8f), 3);
            Assert.Equal(0.5f, WaterMood.AroundLoop(samples, 1f / 16f), 3);
            // Past the last sample it closes the loop back to the first.
            Assert.Equal(-0.5f, WaterMood.AroundLoop(samples, 15f / 16f), 3);
        }

        [Fact]
        public void OnlySignsNearYouFollowTheWaves()
        {
            Assert.True(WaterMood.FollowsWaves(39f));
            Assert.False(WaterMood.FollowsWaves(41f));
        }
    }
}
