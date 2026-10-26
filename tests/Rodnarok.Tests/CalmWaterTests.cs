using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The calm-water piece calms the waves around it. The game shapes waves by four corner values
    /// on each 64 m water tile, the same values its water is drawn with, so the piece lowers the
    /// corners within its reach: what is seen and what floats stay calm together, and two tiles
    /// meeting at a corner always agree on it, so no seam shows.
    /// </summary>
    public class CalmWaterTests
    {
        private static readonly Spot[] NoPieces = new Spot[0];

        [Fact]
        public void ATilesCornersComeInTheOrderTheGameBlendsThem()
        {
            // Index 0 is the -x,+z corner, 1 +x,+z, 2 +x,-z, 3 -x,-z, as the game's depth blend reads them.
            var corners = CalmWater.Corners(new Spot(64f, 0f, 128f));
            Assert.Equal(new Spot(32f, 0f, 160f), corners[0]);
            Assert.Equal(new Spot(96f, 0f, 160f), corners[1]);
            Assert.Equal(new Spot(96f, 0f, 96f), corners[2]);
            Assert.Equal(new Spot(32f, 0f, 96f), corners[3]);
        }

        [Fact]
        public void WaterFarFromAnyPieceKeepsItsWaves()
        {
            var depth = new[] { 1f, 0.8f, 0.6f, 0.4f };
            var pieces = new[] { new Spot(1000f, 0f, 1000f) };
            Assert.Equal(depth, CalmWater.Calmed(depth, new Spot(0f, 0f, 0f), pieces, reach: 40f, keep: 0.1f));
        }

        [Fact]
        public void ACornerWithinReachOfAPieceKeepsOnlyItsShareOfTheWaves()
        {
            var depth = new[] { 1f, 1f, 1f, 1f };
            var pieces = new[] { new Spot(30f, 0f, 30f) };
            var calmed = CalmWater.Calmed(depth, new Spot(0f, 0f, 0f), pieces, reach: 40f, keep: 0.1f);
            Assert.Equal(0.1f, calmed[1], 3);
            Assert.Equal(1f, calmed[3], 3);
        }

        [Fact]
        public void TwoTilesMeetingAtACornerAgreeOnItAfterCalming()
        {
            var pieces = new[] { new Spot(40f, 0f, 20f) };
            var left = CalmWater.Calmed(new[] { 0.7f, 0.9f, 0.9f, 0.7f }, new Spot(0f, 0f, 0f), pieces, 40f, 0.1f);
            var right = CalmWater.Calmed(new[] { 0.9f, 0.5f, 0.5f, 0.9f }, new Spot(64f, 0f, 0f), pieces, 40f, 0.1f);
            // The left tile's +x corners are the right tile's -x corners.
            Assert.Equal(left[1], right[0], 5);
            Assert.Equal(left[2], right[3], 5);
        }

        [Fact]
        public void WithNoPieceTheWaterIsLeftAsTheGameMadeIt()
        {
            var depth = new[] { 0.3f, 0.6f, 0.9f, 1f };
            Assert.Equal(depth, CalmWater.Calmed(depth, new Spot(0f, 0f, 0f), NoPieces, 40f, 0.1f));
        }

        [Fact]
        public void CalmingNeverRaisesTheWaves()
        {
            var depth = new[] { 0.3f, 0.6f, 0.9f, 1f };
            var calmed = CalmWater.Calmed(depth, new Spot(0f, 0f, 0f), new[] { new Spot(0f, 0f, 0f) }, 100f, keep: 2f);
            Assert.True(calmed.Zip(depth, (c, d) => c <= d).All(x => x));
        }

        [Fact]
        public void ThePieceReachesFortyMetresAndKeepsATenthOfTheWavesByDefault()
        {
            Assert.Equal(40f, FishingRules.Default.CalmReach);
            Assert.Equal(0.1f, FishingRules.Default.CalmKeep);
        }
    }
}
