using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A seated player casts where they look, as a standing one does. The game throws the rod's
    /// cast along the way the body faces and turns a standing body toward the aim while drawing;
    /// a seated body is not turned. Sitting on the ground the body turns freely, so the cast goes
    /// wherever the player looks. A chair holds the body facing its own way, so the cast follows
    /// the aim across the chair's front, up to 60 degrees to either side, and an aim further round
    /// casts at the edge of that.
    /// </summary>
    public class SeatedAimTests
    {
        [Theory]
        [InlineData(0f, 90f)]
        [InlineData(0f, 180f)]
        [InlineData(45f, 300f)]
        public void SittingOnTheGroundTheCastGoesWhereThePlayerLooks(float body, float look)
        {
            Assert.Equal(look, SeatedAim.ThrowYaw(body, look, onChair: false), 3);
        }

        [Theory]
        [InlineData(0f, 30f)]
        [InlineData(0f, -45f)]
        [InlineData(90f, 145f)]
        [InlineData(0f, 60f)]
        public void OnAChairAnAimAcrossItsFrontIsWhereTheCastGoes(float seat, float look)
        {
            Assert.Equal(Yaw(look), SeatedAim.ThrowYaw(seat, Yaw(look), onChair: true), 3);
        }

        [Theory]
        [InlineData(0f, 90f, 60f)]
        [InlineData(0f, -100f, -60f)]
        [InlineData(90f, 200f, 150f)]
        [InlineData(10f, 260f, -50f)]
        public void OnAChairAnAimFurtherRoundCastsAtTheEdgeOfItsFront(float seat, float look, float cast)
        {
            Assert.Equal(Yaw(cast), SeatedAim.ThrowYaw(seat, Yaw(look), onChair: true), 3);
        }

        [Fact]
        public void OnAChairAnAimStraightBehindCastsAtAnEdgeNotStraightAhead()
        {
            var cast = SeatedAim.ThrowYaw(0f, 180f, onChair: true);
            Assert.True(System.Math.Abs(cast - 60f) < 0.01f || System.Math.Abs(cast - 300f) < 0.01f, "cast at " + cast);
        }

        [Fact]
        public void TheChairsFrontReachesSixtyDegreesToEitherSide()
        {
            Assert.Equal(60f, SeatedAim.ChairArc);
        }

        [Fact]
        public void AnAimAcrossNorthOnAChairStaysOnTheSideLookedAt()
        {
            Assert.Equal(20f, SeatedAim.ThrowYaw(350f, 20f, onChair: true), 3);
            Assert.Equal(340f, SeatedAim.ThrowYaw(10f, 340f, onChair: true), 3);
        }

        private static float Yaw(float degrees) => ((degrees % 360f) + 360f) % 360f;
    }
}
