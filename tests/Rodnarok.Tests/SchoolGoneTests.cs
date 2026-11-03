using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A school scared away for good tells the one fishing why: their own steps or another player's,
    /// named, reeling the float through it, being too shy to bite so close in or in such shallow
    /// water, or a shot at one of its fish. A school that only darts off and comes back says nothing.
    /// </summary>
    public class SchoolGoneTests
    {
        [Fact]
        public void YourOwnStepsSaySo()
        {
            Assert.Equal("Your steps scared the school away", SchoolGone.Text(GoneWhy.Noise, noiseBy: null));
        }

        [Fact]
        public void AnotherPlayersStepsNameThem()
        {
            Assert.Equal("Dom's steps scared the school away", SchoolGone.Text(GoneWhy.Noise, noiseBy: "Dom"));
        }

        [Fact]
        public void AnEmptyNameCountsAsYourOwnSteps()
        {
            Assert.Equal("Your steps scared the school away", SchoolGone.Text(GoneWhy.Noise, noiseBy: ""));
        }

        [Fact]
        public void ReelingThroughTheSchoolSaysSo()
        {
            Assert.Equal("Reeling through the school scared it away", SchoolGone.Text(GoneWhy.Reeled, noiseBy: null));
        }

        [Fact]
        public void TooShySoCloseToYouSaysSo()
        {
            Assert.Equal("The school was too shy to bite so close to you", SchoolGone.Text(GoneWhy.ShyClose, noiseBy: null));
        }

        [Fact]
        public void TooShyInShallowWaterSaysSo()
        {
            Assert.Equal("The school was too shy to bite in such shallow water", SchoolGone.Text(GoneWhy.ShyShallow, noiseBy: null));
        }

        [Fact]
        public void AShotSaysSo()
        {
            Assert.Equal("The shot scared the school away", SchoolGone.Text(GoneWhy.Shot, noiseBy: null));
        }

        [Fact]
        public void AShyFishCloseInOverDeepWaterIsShyOfYou()
        {
            Assert.Equal(GoneWhy.ShyClose, SchoolGone.Shy(anglerDistance: 3f, depth: 5f));
        }

        [Fact]
        public void AShyFishFarOutInShallowWaterIsShyOfTheShallows()
        {
            Assert.Equal(GoneWhy.ShyShallow, SchoolGone.Shy(anglerDistance: 20f, depth: 0.5f));
        }

        [Fact]
        public void TheShyerOfTheTwoNamesTheReason()
        {
            // Within the shy bands both count: 6 m out is 75% shy of the angler, 2 m deep 50% shy of the shallows.
            Assert.Equal(GoneWhy.ShyClose, SchoolGone.Shy(anglerDistance: 6f, depth: 2f));
            Assert.Equal(GoneWhy.ShyShallow, SchoolGone.Shy(anglerDistance: 10f, depth: 1.5f));
        }

        [Fact]
        public void TheLastFrightNamesTheReason()
        {
            Assert.Equal(GoneWhy.Noise, SchoolGone.ByFright(heard: true));
            Assert.Equal(GoneWhy.Reeled, SchoolGone.ByFright(heard: false));
        }
    }
}
