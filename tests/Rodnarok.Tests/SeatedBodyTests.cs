using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A seated player's hips, spine and legs stay as the seat has them while the game plays its
    /// own standing throw or pull on the whole body: the sitting pose is kept whenever the game
    /// shows it, and put back whenever the game shows anything else while the player still sits.
    /// Standing up lets the game's own animation be.
    /// </summary>
    public class SeatedBodyTests
    {
        [Fact]
        public void TheSittingPoseIsKeptWhileTheGameShowsIt()
        {
            Assert.True(SeatedBody.Keeps(seated: true, sittingPose: true, crossing: false));
        }

        [Fact]
        public void APoseCrossingOverIsNotKept()
        {
            Assert.False(SeatedBody.Keeps(seated: true, sittingPose: true, crossing: true));
        }

        [Fact]
        public void TheThrowWhileSeatedGetsTheSittingPoseBack()
        {
            Assert.True(SeatedBody.PutsBack(seated: true, sittingPose: false, crossing: false, kept: true));
        }

        [Fact]
        public void CrossingBackToTheSeatGetsTheSittingPoseBack()
        {
            Assert.True(SeatedBody.PutsBack(seated: true, sittingPose: true, crossing: true, kept: true));
        }

        [Fact]
        public void ThePlainSittingPoseIsLeftAlone()
        {
            Assert.False(SeatedBody.PutsBack(seated: true, sittingPose: true, crossing: false, kept: true));
        }

        [Fact]
        public void StandingUpLetsTheGamesAnimationBe()
        {
            Assert.False(SeatedBody.PutsBack(seated: false, sittingPose: false, crossing: false, kept: true));
            Assert.False(SeatedBody.Keeps(seated: false, sittingPose: true, crossing: false));
        }

        [Fact]
        public void NothingIsPutBackBeforeTheSittingPoseWasSeen()
        {
            Assert.False(SeatedBody.PutsBack(seated: true, sittingPose: false, crossing: false, kept: false));
        }
    }
}
