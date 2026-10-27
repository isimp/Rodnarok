using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A seated player's fishing motions flow into one another: a change of motion crosses over
    /// rather than cutting, the wind-up is played from the start up to where it is held, the throw
    /// goes on from the wind-up, and the motions as a whole fade in and out.
    /// </summary>
    public class SeatedBlendTests
    {
        private const float Frame = 1f / 60f;
        private const float ThrowLength = 1.2f;

        private static SeatedBlend Run(SeatedBlend blend, SeatedPose pose, float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame) blend.Step(pose, Frame, ThrowLength);
            return blend;
        }

        [Fact]
        public void ReelingFromTheHeldRodCrossesOverRatherThanCutting()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Hold, 1f);
            blend.Step(SeatedPose.Reel, Frame, ThrowLength);

            Assert.InRange(blend.Reel, 0.01f, 0.2f);
            Assert.InRange(blend.Hold, 0.8f, 0.99f);
        }

        [Fact]
        public void AfterTheCrossOverOnlyTheNewMotionShows()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Hold, 1f);
            Run(blend, SeatedPose.Reel, SeatedBlend.CrossSeconds + Frame);

            Assert.Equal(1f, blend.Reel, 3);
            Assert.Equal(0f, blend.Hold, 3);
        }

        [Fact]
        public void TheMotionsShareTheArmsInFullThroughAChange()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Hold, 1f);
            for (var i = 0; i < 20; i++)
            {
                blend.Step(i < 10 ? SeatedPose.Reel : SeatedPose.Draw, Frame, ThrowLength);
                Assert.Equal(1f, blend.Throw + blend.Reel + blend.Hold, 3);
            }
        }

        [Fact]
        public void TheWindUpIsPlayedFromTheStartRatherThanJumpedTo()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Hold, 1f);
            blend.Step(SeatedPose.Draw, Frame, ThrowLength);

            Assert.InRange(blend.ThrowShare, 0f, 0.05f);
        }

        [Fact]
        public void TheWindUpIsHeldAtItsPointWhileDrawing()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Draw, 3f);

            Assert.Equal(SeatedBlend.WindUp, blend.ThrowShare, 3);
        }

        [Fact]
        public void TheThrowGoesOnFromTheWindUp()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Draw, 3f);
            blend.Step(SeatedPose.Throw, Frame, ThrowLength);

            Assert.InRange(blend.ThrowShare, SeatedBlend.WindUp, SeatedBlend.WindUp + 0.05f);
        }

        [Fact]
        public void TheThrowStopsAtItsEnd()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Throw, 3f);

            Assert.Equal(1f, blend.ThrowShare, 3);
        }

        [Fact]
        public void ANewDrawAfterACastWindsUpFromTheStartAgain()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Throw, 3f);
            Run(blend, SeatedPose.Hold, 1f);
            blend.Step(SeatedPose.Draw, Frame, ThrowLength);

            Assert.InRange(blend.ThrowShare, 0f, 0.05f);
        }

        [Fact]
        public void TheMotionsFadeInRatherThanAppearing()
        {
            var blend = new SeatedBlend();
            blend.Step(SeatedPose.Hold, Frame, ThrowLength);

            Assert.InRange(blend.Overlay, 0.01f, 0.2f);
            Assert.Equal(1f, Run(blend, SeatedPose.Hold, SeatedBlend.FadeSeconds).Overlay, 3);
        }

        [Fact]
        public void TheMotionsFadeOutWithTheLastStillShowing()
        {
            var blend = Run(new SeatedBlend(), SeatedPose.Reel, 1f);
            blend.Step(SeatedPose.None, Frame, ThrowLength);

            Assert.InRange(blend.Overlay, 0.8f, 0.99f);
            Assert.Equal(1f, blend.Reel, 3);
            Assert.Equal(0f, Run(blend, SeatedPose.None, SeatedBlend.FadeSeconds).Overlay, 3);
        }

        [Fact]
        public void TheFirstMotionShowsAloneAsTheMotionsFadeIn()
        {
            var blend = new SeatedBlend();
            blend.Step(SeatedPose.Reel, Frame, ThrowLength);

            Assert.Equal(1f, blend.Reel, 3);
        }
    }
}
