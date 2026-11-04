using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// While a school is at the float and no fish bites yet, the crosshair ring tells how close the fish
    /// is: it keeps the bite's size and grows stronger as the fish comes closer across the water, faint
    /// at the edge of the picked fish's hover and beyond, whole white within reach of the hook.
    /// </summary>
    public class ApproachRingTests
    {
        [Fact]
        public void TheRingIsWholeWithTheFishInReach()
        {
            // The reach a fish bites from (Bites.Reach).
            Assert.Equal(0.6f, ApproachRing.Reach, 3);
            Assert.Equal(1f, ApproachRing.Strength(0.2f), 3);
            Assert.Equal(1f, ApproachRing.Strength(ApproachRing.Reach), 3);
        }

        [Fact]
        public void TheRingIsFaintAtTheEdgeOfTheHoverAndBeyond()
        {
            // The picked fish hovers within 1.5 m of the float (Bumps.Hover).
            Assert.Equal(1.5f, ApproachRing.Hover, 3);
            Assert.Equal(ApproachRing.Faint, ApproachRing.Strength(ApproachRing.Hover), 3);
            Assert.Equal(ApproachRing.Faint, ApproachRing.Strength(20f), 3);
            Assert.True(ApproachRing.Faint > 0f && ApproachRing.Faint <= 0.2f, "faint " + ApproachRing.Faint);
        }

        [Fact]
        public void TheRingGrowsStrongerSmoothlyAsTheFishComesCloser()
        {
            var strength = ApproachRing.Strength(ApproachRing.Hover);
            for (var d = ApproachRing.Hover - 0.05f; d > ApproachRing.Reach; d -= 0.05f)
            {
                Assert.True(ApproachRing.Strength(d) > strength, "at " + d + " m");
                strength = ApproachRing.Strength(d);
            }
        }
    }
}
