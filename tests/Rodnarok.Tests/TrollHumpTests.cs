using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A Trollfish coming to a float shows as a hump of water standing up from the surface, read from
    /// the shore where a flat mark would be seen edge-on: a bright crest over a darker band of the
    /// water it pushes up, surging about once a second as something heavy shoving through, each surge
    /// throwing a little spray off its front. It is long when it swims across the view and short when
    /// it swims toward the one looking, higher at its front, and a fish barely moving barely raises it.
    /// </summary>
    public class TrollHumpTests
    {
        [Fact]
        public void TheHumpSurgesAboutOnceASecond()
        {
            Assert.InRange(TrollHump.SurgeSeconds, 0.7f, 1.2f);
            var heights = Enumerable.Range(0, 100).Select(i => TrollHump.Height(i / 100f, moving: 1f)).ToList();
            var peakAt = heights.IndexOf(heights.Max());
            Assert.InRange(peakAt, 10, 35);
            Assert.Equal(TrollHump.Low, TrollHump.Height(0f, 1f), 3);
            Assert.True(TrollHump.Height(0.99f, 1f) < TrollHump.Low + 0.01f);
        }

        [Fact]
        public void ASurgeRaisesTheHumpWellAboveItsRest()
        {
            var peak = Enumerable.Range(0, 100).Max(i => TrollHump.Height(i / 100f, 1f));
            Assert.InRange(peak, TrollHump.High - 0.01f, TrollHump.High + 0.001f);
            Assert.True(TrollHump.High > TrollHump.Low * 2f);
        }

        [Fact]
        public void AFishBarelyMovingBarelyRaisesIt()
        {
            var peakStill = Enumerable.Range(0, 100).Max(i => TrollHump.Height(i / 100f, 0f));
            var peakMoving = Enumerable.Range(0, 100).Max(i => TrollHump.Height(i / 100f, 1f));
            Assert.True(peakStill < peakMoving * 0.5f, "still " + peakStill + " against moving " + peakMoving);
            Assert.True(peakStill > 0f);
        }

        [Fact]
        public void SprayFliesOnlyAsTheHumpSurges()
        {
            Assert.True(TrollHump.Spray(0.15f) > 0.5f);
            Assert.Equal(0f, TrollHump.Spray(0f), 3);
            Assert.Equal(0f, TrollHump.Spray(0.5f), 3);
            Assert.Equal(0f, TrollHump.Spray(0.9f), 3);
        }

        [Fact]
        public void TheHumpIsLongAcrossTheViewAndShortTowardTheOneLooking()
        {
            Assert.True(TrollHump.Length(across: 1f) > TrollHump.Length(across: 0f) * 2f);
            Assert.InRange(TrollHump.Length(0f), 0.5f, 0.9f);
            Assert.InRange(TrollHump.Length(1f), 1.5f, 2f);
            Assert.Equal(TrollHump.Length(1f), TrollHump.Length(-1f), 3);
        }

        [Fact]
        public void TheHumpRisesHighestTowardItsFrontAndMeetsTheWaterAtItsEnds()
        {
            Assert.True(TrollHump.Arch(0.5f, front: 1f) > TrollHump.Arch(-0.5f, front: 1f));
            Assert.True(TrollHump.Arch(-0.5f, front: -1f) > TrollHump.Arch(0.5f, front: -1f));
            Assert.Equal(1f, TrollHump.Arch(0f, 1f), 3);
            Assert.Equal(0f, TrollHump.Arch(1f, 1f), 3);
            Assert.Equal(0f, TrollHump.Arch(-1f, 1f), 3);
        }
    }
}
