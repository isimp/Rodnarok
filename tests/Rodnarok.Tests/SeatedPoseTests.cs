using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A seated player with a rod shows the game's own fishing motions on the arms and chest while
    /// the legs stay seated: the throw's wind-up while drawing, the throw while casting, the pull
    /// while reeling, and the stance of holding the rod while the float is out. Without a rod in
    /// hand, or standing, the game's own animation is left alone.
    /// </summary>
    public class SeatedPoseTests
    {
        [Fact]
        public void DrawingWhileSeatedShowsTheThrowsWindUp()
        {
            Assert.Equal(SeatedPose.Draw, SeatedPoses.For(seatedWithRod: true, drawing: true, casting: false, reeling: false, floatOut: false));
        }

        [Fact]
        public void CastingWhileSeatedShowsTheThrow()
        {
            Assert.Equal(SeatedPose.Throw, SeatedPoses.For(seatedWithRod: true, drawing: false, casting: true, reeling: false, floatOut: false));
        }

        [Fact]
        public void ReelingWhileSeatedShowsThePull()
        {
            Assert.Equal(SeatedPose.Reel, SeatedPoses.For(seatedWithRod: true, drawing: false, casting: false, reeling: true, floatOut: true));
        }

        [Fact]
        public void WithTheFloatOutTheRodIsHeldOut()
        {
            Assert.Equal(SeatedPose.Hold, SeatedPoses.For(seatedWithRod: true, drawing: false, casting: false, reeling: false, floatOut: true));
        }

        [Fact]
        public void SeatedWithNoFloatOutTheGamesPoseStays()
        {
            Assert.Equal(SeatedPose.None, SeatedPoses.For(seatedWithRod: true, drawing: false, casting: false, reeling: false, floatOut: false));
        }

        [Fact]
        public void StandingOrWithoutARodTheGamesAnimationIsLeftAlone()
        {
            Assert.Equal(SeatedPose.None, SeatedPoses.For(seatedWithRod: false, drawing: true, casting: true, reeling: true, floatOut: true));
        }

        [Fact]
        public void TheCastShowsOverAReelHeldAtTheSameTime()
        {
            Assert.Equal(SeatedPose.Throw, SeatedPoses.For(seatedWithRod: true, drawing: false, casting: true, reeling: true, floatOut: true));
        }
    }
}
