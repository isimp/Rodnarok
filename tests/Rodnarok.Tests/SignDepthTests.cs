using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A fish's sign tells how deep it swims: from as deep as the bait hangs (1 m under the float)
    /// up to the surface it shows as made, small and sharp; deeper it shows broader and fainter, at
    /// its broadest from the 4 m a school out of sight dives at, about as deep as a Tuna swims.
    /// Most fish swim 1.5 to 3 m deep by their prefabs, so their sign shows a little spread and
    /// tightens as the one going for the bait rises to the hook.
    /// </summary>
    public class SignDepthTests
    {
        [Fact]
        public void AtTheBaitsDepthOrShallowerTheSignShowsAsMade()
        {
            foreach (var under in new[] { -0.5f, 0f, 0.5f, SignDepth.Crisp })
            {
                Assert.Equal(1f, SignDepth.Size(under), 3);
                Assert.Equal(1f, SignDepth.Boldness(under), 3);
            }
            Assert.Equal(1f, SignDepth.Crisp, 3);
        }

        [Fact]
        public void DeeperTheSignShowsBroaderAndFainter()
        {
            var last = (Size: 1f, Bold: 1f);
            for (var under = SignDepth.Crisp + 0.25f; under <= SignDepth.Broadest; under += 0.25f)
            {
                var now = (Size: SignDepth.Size(under), Bold: SignDepth.Boldness(under));
                Assert.True(now.Size > last.Size && now.Bold < last.Bold, "at " + under + " m: " + now);
                last = now;
            }
        }

        [Fact]
        public void FromTheDiveDepthOnTheSignIsAtItsBroadestYetStillSeen()
        {
            Assert.Equal(FishingRules.Default.DiveDepth, SignDepth.Broadest, 3);
            Assert.Equal(SignDepth.Size(SignDepth.Broadest), SignDepth.Size(20f), 3);
            Assert.Equal(SignDepth.Boldness(SignDepth.Broadest), SignDepth.Boldness(20f), 3);
            Assert.InRange(SignDepth.Size(SignDepth.Broadest), 1.4f, 1.8f);
            Assert.InRange(SignDepth.Boldness(SignDepth.Broadest), 0.45f, 0.65f);
        }

        [Fact]
        public void APerchAtItsOwnDepthShowsALittleSpreadThatGoesAsItRisesToTheHook()
        {
            // A Perch swims 1.5 m deep and more (its prefab's m_minDepth); a school's fish keep to 1.75 m.
            var swimming = SignDepth.Size(1.75f);
            Assert.InRange(swimming, 1.1f, 1.25f);
            Assert.True(SignDepth.Boldness(1.75f) < 0.95f);
            Assert.Equal(1f, SignDepth.Size(SignDepth.Crisp), 3);
        }

        [Fact]
        public void NoDepthReadShowsTheSignAsMade()
        {
            Assert.Equal(1f, SignDepth.Size(float.NaN), 3);
            Assert.Equal(1f, SignDepth.Boldness(float.NaN), 3);
        }
    }
}
