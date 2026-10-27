using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A seated player's hips, spine and legs stay as the seat has them while the game plays its
    /// own standing throw or pull on the whole body, or sits the player down again: the sitting
    /// pose is kept only once the game has shown it settled for a while, and put back whenever the
    /// game shows anything else, or a sitting pose not yet settled, while the player still sits.
    /// The seat's switch blinking off for a moment keeps the pose; standing up lets the game's
    /// animation be.
    /// </summary>
    public class SeatedBodyTests
    {
        private const float Settled = SeatedBody.SettleSeconds + 0.1f;

        [Fact]
        public void TheSettledSittingPoseIsKept()
        {
            Assert.True(SeatedBody.Keeps(seated: true, sittingPose: true, crossing: false, inPose: Settled));
        }

        [Fact]
        public void APoseCrossingOverIsNotKept()
        {
            Assert.False(SeatedBody.Keeps(seated: true, sittingPose: true, crossing: true, inPose: Settled));
        }

        [Fact]
        public void SittingDownIsNotKeptAsTheSittingPose()
        {
            Assert.False(SeatedBody.Keeps(seated: true, sittingPose: true, crossing: false, inPose: SeatedBody.SettleSeconds - 0.1f));
        }

        [Fact]
        public void TheThrowWhileSeatedGetsTheSittingPoseBack()
        {
            Assert.True(SeatedBody.PutsBack(seated: true, sittingPose: false, crossing: false, inPose: Settled, kept: true));
        }

        [Fact]
        public void CrossingBackToTheSeatGetsTheSittingPoseBack()
        {
            Assert.True(SeatedBody.PutsBack(seated: true, sittingPose: true, crossing: true, inPose: Settled, kept: true));
        }

        [Fact]
        public void SittingDownAgainWhileSeatedGetsTheSittingPoseBack()
        {
            Assert.True(SeatedBody.PutsBack(seated: true, sittingPose: true, crossing: false, inPose: 0.2f, kept: true));
        }

        [Fact]
        public void TheSettledSittingPoseIsLeftAlone()
        {
            Assert.False(SeatedBody.PutsBack(seated: true, sittingPose: true, crossing: false, inPose: Settled, kept: true));
        }

        [Fact]
        public void StandingUpLetsTheGamesAnimationBe()
        {
            Assert.False(SeatedBody.PutsBack(seated: false, sittingPose: false, crossing: false, inPose: Settled, kept: true));
            Assert.False(SeatedBody.Keeps(seated: false, sittingPose: true, crossing: false, inPose: Settled));
        }

        [Fact]
        public void NothingIsPutBackBeforeTheSittingPoseWasSeen()
        {
            Assert.False(SeatedBody.PutsBack(seated: true, sittingPose: false, crossing: false, inPose: Settled, kept: false));
        }

        [Fact]
        public void TheSeatsSwitchBlinkingOffForAMomentKeepsThePose()
        {
            Assert.False(SeatedBody.Forgets(switchOffFor: SeatedBody.SwitchGrace - 0.1f));
            Assert.True(SeatedBody.Forgets(switchOffFor: SeatedBody.SwitchGrace));
        }
    }
}
