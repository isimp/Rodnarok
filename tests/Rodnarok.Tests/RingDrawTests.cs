using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// How the crosshair ring is drawn, on the game's own bow ring: a bite shows it wide and white,
    /// shrinking to the game's tightest draw and turning yellow as the hook window runs out; a
    /// struggle shows it wide and red, pulsing; a rest shows it small and steady yellow.
    /// </summary>
    public class RingDrawTests
    {
        [Fact]
        public void NothingToTellDrawsNoRing()
        {
            Assert.False(RingDraw.For(Ring.Hidden, 0f).Shown);
        }

        [Fact]
        public void AtTheBiteTheRingIsWideAndClearlyWhite()
        {
            var draw = RingDraw.For(Ring.ForBite(0f, 1.5f), 0f);
            Assert.True(draw.Shown);
            Assert.Equal(1f, draw.Scale, 3);
            Assert.True(draw.R > 0.95f && draw.G > 0.95f && draw.B > 0.95f, "white");
            Assert.True(draw.A >= 0.6f, "clearly seen, not the bow's transparent start");
        }

        [Fact]
        public void AsTheWindowRunsOutTheRingShrinksToTheGamesTightestDrawAndTurnsYellow()
        {
            var end = RingDraw.For(Ring.ForBite(1.5f, 1.5f), 0f);
            Assert.Equal(0.15f, end.Scale, 3);
            Assert.True(end.R > 0.95f && end.G > 0.85f && end.B < 0.05f, "yellow");
            var half = RingDraw.For(Ring.ForBite(0.75f, 1.5f), 0f);
            Assert.InRange(half.Scale, 0.5f, 0.65f);
            Assert.True(half.B > end.B && half.B < 1f, "between white and yellow");
        }

        [Fact]
        public void AStruggleDrawsAWideRedRingThatPulses()
        {
            var draws = Enumerable.Range(0, 40).Select(i => RingDraw.For(Ring.ForFight(struggling: true), i * 0.05f)).ToList();
            Assert.All(draws, d => Assert.True(d.Shown && d.R > 0.8f && d.G < 0.4f && d.B < 0.4f, "red"));
            Assert.All(draws, d => Assert.InRange(d.Scale, 0.6f, 0.9f));
            Assert.True(draws.Max(d => d.Scale) - draws.Min(d => d.Scale) > 0.08f, "it pulses");
        }

        [Fact]
        public void ARestDrawsASmallSteadyYellowRing()
        {
            var draws = Enumerable.Range(0, 40).Select(i => RingDraw.For(Ring.ForFight(struggling: false), i * 0.05f)).ToList();
            Assert.All(draws, d => Assert.True(d.Shown && d.R > 0.95f && d.G > 0.85f && d.B < 0.05f, "yellow"));
            Assert.All(draws, d => Assert.InRange(d.Scale, 0.25f, 0.35f));
            Assert.Equal(draws.Min(d => d.Scale), draws.Max(d => d.Scale));
        }

        [Fact]
        public void AStruggleRingIsWiderThanARestRingAtEveryMoment()
        {
            for (var t = 0f; t < 2f; t += 0.05f)
                Assert.True(RingDraw.For(Ring.ForFight(true), t).Scale > RingDraw.For(Ring.ForFight(false), t).Scale + 0.2f);
        }
    }
}
