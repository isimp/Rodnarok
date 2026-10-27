using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// With bumps on, the fish that takes the bait bumps the float lightly once or twice before it
    /// goes for the hook, never right as it is picked and never right before it bites, and two
    /// bumps far enough apart to be told apart. With bumps off, or too little time before the bite,
    /// there are none.
    /// </summary>
    public class BumpsTests
    {
        private const float Lead = 3f;

        [Fact]
        public void BumpsAreOffUnlessTheServerSwitchesThemOn()
        {
            Assert.False(new FishingRules().Bumps);
        }

        [Fact]
        public void OffThereAreNoBumps()
        {
            Assert.Empty(Bumps.Times(on: false, lead: Lead, countRoll: 0.9, first: 0.5, second: 0.5));
        }

        [Fact]
        public void OnAFishBumpsOnceOrTwice()
        {
            Assert.Single(Bumps.Times(true, Lead, countRoll: 0.2, first: 0.5, second: 0.5));
            Assert.Equal(2, Bumps.Times(true, Lead, countRoll: 0.8, first: 0.1, second: 0.9).Count);
        }

        [Fact]
        public void BumpsComeNeitherAsTheFishIsPickedNorRightBeforeItBites()
        {
            foreach (var roll in new[] { 0.0, 0.5, 0.999 })
            {
                foreach (var at in Bumps.Times(true, Lead, 0.8, roll, 1.0 - roll))
                {
                    Assert.InRange(at, Bumps.FirstAfter, Lead - Bumps.LastBefore);
                }
            }
        }

        [Fact]
        public void TwoBumpsAreFarEnoughApartToBeToldApart()
        {
            var times = Bumps.Times(true, Lead, countRoll: 0.8, first: 0.5, second: 0.5);
            Assert.True(times.Count < 2 || times[1] - times[0] >= Bumps.Apart);
        }

        [Fact]
        public void BumpsComeInOrder()
        {
            var times = Bumps.Times(true, Lead, countRoll: 0.8, first: 0.9, second: 0.1);
            Assert.Equal(times.OrderBy(t => t), times);
        }

        [Fact]
        public void WithRoomForOneBumpOnlyTheFishBumpsOnceWithinTheTime()
        {
            var lead = Bumps.FirstAfter + Bumps.LastBefore + Bumps.Apart / 2f;
            var times = Bumps.Times(true, lead, countRoll: 0.8, first: 0.5, second: 0.5);
            Assert.Single(times);
            Assert.InRange(times[0], Bumps.FirstAfter, lead - Bumps.LastBefore);
        }

        [Fact]
        public void TooLittleTimeBeforeTheBiteLeavesNoBump()
        {
            Assert.Empty(Bumps.Times(true, lead: Bumps.FirstAfter + Bumps.LastBefore - 0.01f, countRoll: 0.8, first: 0.5, second: 0.5));
        }

        [Fact]
        public void ABumpIsLighterAndShorterThanTheBite()
        {
            Assert.True(Bumps.PullSeconds < Bite.PullSeconds);
            Assert.True(Bumps.Pull < Bite.Pull);
        }
    }
}
