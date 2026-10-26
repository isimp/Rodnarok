using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// How the crosshair ring is drawn: one ring of one size from the bite to the end of the fight,
    /// so it never looks like drawing the rod. After a bite its arc is full and white and drains as
    /// the hook window runs out, turning toward yellow; in the fight the whole ring shows red while
    /// the fish struggles and yellow while it rests.
    /// </summary>
    public class RingDrawTests
    {
        private static readonly Ring[] Shown =
        {
            Ring.ForBite(0f, 1.5f), Ring.ForBite(0.75f, 1.5f), Ring.ForBite(1.5f, 1.5f),
            Ring.ForFight(struggling: true), Ring.ForFight(struggling: false),
        };

        [Fact]
        public void NothingToTellDrawsNoRing()
        {
            Assert.False(RingDraw.For(Ring.Hidden).Shown);
        }

        [Fact]
        public void TheRingIsOneSizeFromTheBiteToTheEndOfTheFight()
        {
            var sizes = Shown.Select(r => RingDraw.For(r).Scale).Distinct().ToList();
            Assert.Single(sizes);
            Assert.All(Shown, r => Assert.True(RingDraw.For(r).Shown));
        }

        [Fact]
        public void AtTheBiteTheArcIsFullAndWhite()
        {
            var draw = RingDraw.For(Ring.ForBite(0f, 1.5f));
            Assert.Equal(1f, draw.Fill, 3);
            Assert.True(draw.R > 0.95f && draw.G > 0.95f && draw.B > 0.95f, "white");
            Assert.True(draw.A >= 0.8f, "clearly seen");
        }

        [Fact]
        public void TheArcDrainsOverTheHookWindowTurningTowardYellow()
        {
            var half = RingDraw.For(Ring.ForBite(0.75f, 1.5f));
            var end = RingDraw.For(Ring.ForBite(1.5f, 1.5f));
            Assert.Equal(0.5f, half.Fill, 3);
            Assert.Equal(0f, end.Fill, 3);
            Assert.True(half.B < 0.95f && end.B < half.B, "toward yellow as it drains");
        }

        [Fact]
        public void AStruggleShowsTheWholeRingRed()
        {
            var draw = RingDraw.For(Ring.ForFight(struggling: true));
            Assert.Equal(1f, draw.Fill, 3);
            Assert.True(draw.R > 0.8f && draw.G < 0.4f && draw.B < 0.4f, "red");
        }

        [Fact]
        public void ARestShowsTheWholeRingYellow()
        {
            var draw = RingDraw.For(Ring.ForFight(struggling: false));
            Assert.Equal(1f, draw.Fill, 3);
            Assert.True(draw.R > 0.95f && draw.G > 0.85f && draw.B < 0.05f, "yellow");
        }
    }
}
