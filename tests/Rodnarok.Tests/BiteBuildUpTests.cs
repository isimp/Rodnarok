using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The last moments before a bite and the moment after read on the water: as the fish going for
    /// the bait closes the last 1.5 m, the float twitches in growing tilts toward it, nosed and let
    /// go; once a fish is hooked or a school scared away, the fish that flee dart off, each with a
    /// short V of a wake away from what frightened them. A starred fish tells itself: a school
    /// holding one shows a little bolder on the way in, and a starred fish going for the bait shows
    /// clearly bigger the more stars it has; a plain fish going for it shows plain, whatever else
    /// the school holds.
    /// </summary>
    public class BiteBuildUpTests
    {
        // The float's tremble.

        [Fact]
        public void TheFloatIsStillUntilTheFishIsWithinItsLastMetreAndAHalf()
        {
            Assert.Equal(0f, FloatTremble.Closeness(FloatTremble.From + 0.1f), 3);
            Assert.Equal(0f, FloatTremble.Closeness(10f), 3);
            Assert.True(FloatTremble.Closeness(1.2f) > 0f);
            Assert.Equal(1.5f, FloatTremble.From, 3);
        }

        [Fact]
        public void TheTiltsGrowAsTheFishCloses()
        {
            Assert.True(FloatTremble.Closeness(0.8f) > FloatTremble.Closeness(1.2f));
            Assert.Equal(1f, FloatTremble.Closeness(FloatTremble.At), 3);
            Assert.Equal(1f, FloatTremble.Closeness(0.1f), 3);
            var peakFar = Enumerable.Range(0, 100).Max(i => FloatTremble.Degrees(FloatTremble.Closeness(1.3f), i / 100f));
            var peakNear = Enumerable.Range(0, 100).Max(i => FloatTremble.Degrees(FloatTremble.Closeness(0.7f), i / 100f));
            Assert.True(peakNear > peakFar);
            Assert.InRange(peakNear, FloatTremble.MaxDegrees * 0.8f, FloatTremble.MaxDegrees + 0.001f);
        }

        [Fact]
        public void EachTwitchNosesQuicklyAndEasesBack()
        {
            // The tilt peaks early in each twitch and is back to upright as the next begins.
            var tilts = Enumerable.Range(0, 100).Select(i => FloatTremble.Degrees(1f, i / 100f)).ToList();
            var peakAt = tilts.IndexOf(tilts.Max());
            Assert.InRange(peakAt, 10, 35);
            Assert.Equal(0f, FloatTremble.Degrees(1f, 0f), 3);
            Assert.True(FloatTremble.Degrees(1f, 0.99f) < FloatTremble.MaxDegrees * 0.05f);
            Assert.True(tilts.All(t => t >= 0f));
        }

        [Fact]
        public void TheTwitchesComeQuickerAsTheFishCloses()
        {
            Assert.True(FloatTremble.Seconds(1f) < FloatTremble.Seconds(0.1f));
            Assert.InRange(FloatTremble.Seconds(0f), 0.4f, 0.8f);
        }

        [Fact]
        public void TheFloatTiltsTowardTheFish()
        {
            // The axis to tilt about lies across the line to the fish, so the top leans toward it.
            var (x, z) = FloatTremble.Axis(fishX: 3f, fishZ: 0f);
            Assert.Equal(0f, x, 3);
            Assert.Equal(-1f, z, 3);
            var (x2, z2) = FloatTremble.Axis(0f, 0f);
            Assert.Equal(0f, x2 * x2 + z2 * z2 - 1f, 3);
        }

        // The scatter.

        [Fact]
        public void AFleeingFishDartsAwayFromWhatFrightenedIt()
        {
            var (x, z) = ScatterBurst.Away(fishX: 2f, fishZ: 2f, fromX: 0f, fromZ: 0f);
            Assert.Equal(0.7071f, x, 3);
            Assert.Equal(0.7071f, z, 3);
            var (x2, z2) = ScatterBurst.Away(1f, 1f, 1f, 1f);
            Assert.Equal(1f, x2 * x2 + z2 * z2, 3);
        }

        [Fact]
        public void TheDartRunsFastThenSlowsAndFades()
        {
            Assert.Equal(0f, ScatterBurst.Run(0f), 3);
            Assert.Equal(ScatterBurst.Metres, ScatterBurst.Run(1f), 3);
            Assert.True(ScatterBurst.Run(0.3f) > ScatterBurst.Metres * 0.45f);
            Assert.True(ScatterBurst.Alpha(0.9f) < ScatterBurst.Alpha(0.2f));
            Assert.Equal(0f, ScatterBurst.Alpha(1f), 3);
            Assert.InRange(ScatterBurst.Seconds, 0.6f, 1.2f);
        }

        [Fact]
        public void OnlyAFleeJustNowDarts()
        {
            Assert.True(ScatterBurst.Fresh(secondsAgo: 0.2f));
            Assert.False(ScatterBurst.Fresh(secondsAgo: 3f));
            Assert.False(ScatterBurst.Fresh(secondsAgo: -1f));
        }

        // The stars.

        [Fact]
        public void APlainSchoolShowsPlain()
        {
            Assert.Equal(1f, SignStars.Size(schoolBest: 1, going: false, goingQuality: 1), 3);
            Assert.Equal(1f, SignStars.Boldness(schoolBest: 1, going: false, goingQuality: 1), 3);
        }

        [Fact]
        public void ASchoolHoldingAStarredFishShowsALittleBolderOnTheWayIn()
        {
            Assert.InRange(SignStars.Size(schoolBest: 3, going: false, goingQuality: 1), 1.05f, 1.15f);
            Assert.True(SignStars.Boldness(3, false, 1) > 1f);
            // A hint only: the same however many stars, so it does not say which fish or how many.
            Assert.Equal(SignStars.Size(2, false, 1), SignStars.Size(4, false, 1), 3);
        }

        [Fact]
        public void AStarredFishGoingForTheBaitShowsBiggerTheMoreStarsItHas()
        {
            Assert.True(SignStars.Size(4, true, 2) > SignStars.Size(4, false, 1));
            Assert.True(SignStars.Size(4, true, 3) > SignStars.Size(4, true, 2));
            Assert.True(SignStars.Size(4, true, 4) > SignStars.Size(4, true, 3));
            Assert.InRange(SignStars.Size(4, true, 4), 1.35f, 1.6f);
            Assert.Equal(SignStars.Size(4, true, 4), SignStars.Size(4, true, 9), 3);
        }

        [Fact]
        public void APlainFishGoingForTheBaitShowsPlainWhateverTheSchoolHolds()
        {
            Assert.Equal(1f, SignStars.Size(schoolBest: 4, going: true, goingQuality: 1), 3);
            Assert.Equal(1f, SignStars.Boldness(4, true, 1), 3);
        }
    }

    /// <summary>
    /// The parts of the signs are kept and used again rather than made and thrown away with every
    /// showing, so a Coral cod's fizz or several floats at once make no stream of new objects;
    /// only so many of each are kept spare.
    /// </summary>
    public class PartPoolTests
    {
        private sealed class Part
        {
        }

        [Fact]
        public void APartGivenBackIsUsedAgain()
        {
            var pool = new PartPool<Part>(() => new Part(), _ => { }, _ => { }, keep: 8);
            var first = pool.Take();
            pool.Return(first);
            Assert.Same(first, pool.Take());
            Assert.Equal(1, pool.Made);
        }

        [Fact]
        public void ManyShowingsMakeOnlyAsManyPartsAsShowAtOnce()
        {
            var pool = new PartPool<Part>(() => new Part(), _ => { }, _ => { }, keep: 8);
            for (var round = 0; round < 100; round++)
            {
                var a = pool.Take();
                var b = pool.Take();
                var c = pool.Take();
                pool.Return(a);
                pool.Return(b);
                pool.Return(c);
            }
            Assert.Equal(3, pool.Made);
        }

        [Fact]
        public void OnlySoManySparePartsAreKept()
        {
            var dropped = 0;
            var pool = new PartPool<Part>(() => new Part(), _ => { }, _ => dropped++, keep: 2);
            var parts = Enumerable.Range(0, 5).Select(_ => pool.Take()).ToList();
            foreach (var p in parts) pool.Return(p);
            Assert.Equal(3, dropped);
            Assert.Equal(2, pool.Spare);
        }

        [Fact]
        public void APartIsPutAwayWhenGivenBackAndAllAreDroppedOnClear()
        {
            var away = 0;
            var dropped = 0;
            var pool = new PartPool<Part>(() => new Part(), _ => away++, _ => dropped++, keep: 8);
            var p = pool.Take();
            pool.Return(p);
            Assert.Equal(1, away);
            pool.Clear();
            Assert.Equal(1, dropped);
            Assert.Equal(0, pool.Spare);
        }
    }
}
