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
        public void WhileAFishWaitsTheRingKeepsTheBitesSizeAndFadesInWithItsDistance()
        {
            var bite = RingDraw.For(Ring.ForBite(0f, 1.5f));
            var near = RingDraw.For(Ring.ForWaiting(Jig.Reach));
            var far = RingDraw.For(Ring.ForWaiting(Jig.Hover));
            Assert.True(near.Shown && far.Shown);
            Assert.Equal(bite.Scale, near.Scale, 3);
            Assert.Equal(bite.Scale, far.Scale, 3);
            Assert.True(near.R > 0.95f && near.G > 0.95f && near.B > 0.95f, "white as at the bite");
            Assert.Equal(1f, near.Fill, 3);
            Assert.Equal(1f, near.A, 3);
            Assert.Equal(Jig.Faint, far.A, 3);
            Assert.True(near.TrackA > 0f && near.TrackA < near.A, "a faint track behind it");
        }

        [Fact]
        public void WhileTheFishHoldsTheRingIsAWhiteArcDrainingAsAfterABite()
        {
            var bite = RingDraw.For(Ring.ForBite(0f, 1.5f));
            var start = RingDraw.For(Ring.ForWindow(0f));
            var half = RingDraw.For(Ring.ForWindow(0.5f));
            Assert.True(start.Shown && start.Scale == bite.Scale);
            Assert.Equal(1f, start.Fill, 3);
            Assert.Equal(0.5f, half.Fill, 3);
            Assert.Equal(1f, half.A, 3);
            Assert.True(half.R > 0.95f && half.G > 0.95f && half.B > 0.95f, "white");
            Assert.True(half.TrackA > 0f, "the emptied part shows as a faint track");
        }

        [Fact]
        public void AJigThatTookFlashesWhiteThenShowsFaintYellow()
        {
            var bite = RingDraw.For(Ring.ForBite(0f, 1.5f));
            var flash = RingDraw.For(Ring.ForTook(0f));
            var after = RingDraw.For(Ring.ForTook(Jig.TookFlashSeconds + 0.05f));
            Assert.True(flash.Shown && flash.A >= 0.99f && flash.R > 0.95f && flash.B > 0.95f, "a white flash");
            Assert.True(after.Shown && after.Scale == bite.Scale);
            Assert.True(after.R > 0.95f && after.G > 0.9f && after.B < 0.1f, "yellow, as the ring at rest");
            Assert.True(after.A > 0.2f && after.A < 0.6f, "faint, " + after.A);
        }

        [Fact]
        public void AJigOffCueBlinksTheRingRedAtItsSize()
        {
            var bite = RingDraw.For(Ring.ForBite(0f, 1.5f));
            var struggle = RingDraw.For(Ring.ForFight(struggling: true));
            var off = RingDraw.For(Ring.ForOffCue());
            Assert.True(off.Shown && off.Scale == bite.Scale && off.Fill == 1f);
            Assert.Equal(struggle.R, off.R, 3);
            Assert.Equal(struggle.G, off.G, 3);
            Assert.Equal(struggle.B, off.B, 3);
            Assert.True(off.A > 0.8f);
            Assert.True(Jig.OffCueSeconds > 0.2f && Jig.OffCueSeconds <= 0.4f);
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

        [Fact]
        public void WithTensionTheArcIsTheStrainBuildingOverAFaintFullTrack()
        {
            var draw = RingDraw.For(Ring.ForFight(struggling: true, strain: 0.5f));
            Assert.Equal(0.5f, draw.Fill, 3);
            Assert.True(draw.R > 0.8f && draw.G < 0.4f && draw.B < 0.4f, "red in a struggle");
            Assert.InRange(draw.TrackA, 0.15f, 0.4f);
        }

        [Fact]
        public void AnUnstrainedLineShowsOnlyTheTrack()
        {
            var draw = RingDraw.For(Ring.ForFight(struggling: true, strain: 0f));
            Assert.True(draw.Shown);
            Assert.Equal(0f, draw.Fill, 3);
            Assert.True(draw.TrackA > 0f);
        }

        [Fact]
        public void AtRestTheStrainStillShowsAsItEasesInYellow()
        {
            var draw = RingDraw.For(Ring.ForFight(struggling: false, strain: 0.3f));
            Assert.Equal(0.3f, draw.Fill, 3);
            Assert.True(draw.R > 0.95f && draw.G > 0.85f && draw.B < 0.05f, "yellow");
            Assert.True(draw.TrackA > 0f);
        }

        [Fact]
        public void TheStrainedRingKeepsTheBitesSize()
        {
            Assert.Equal(RingDraw.For(Ring.ForBite(0f, 1.5f)).Scale, RingDraw.For(Ring.ForFight(true, 0.7f)).Scale);
        }

        [Fact]
        public void TheBiteAndAFightWithoutTensionHaveNoTrack()
        {
            Assert.Equal(0f, RingDraw.For(Ring.ForBite(0.3f, 1.5f)).TrackA);
            Assert.Equal(0f, RingDraw.For(Ring.ForFight(struggling: true)).TrackA);
        }
    }
}
