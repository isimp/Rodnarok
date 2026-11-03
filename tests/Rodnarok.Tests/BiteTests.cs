using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A bite happens only with the fish at the hook, seen from every side, and the reel hooks it
    /// for as long as the hook window lasts.
    /// </summary>
    public class BiteTests
    {
        private const float Reach = 0.3f;

        [Fact]
        public void AFishAtTheHookBites()
        {
            Assert.True(Bite.FishAtHook(new Spot(10f, -1f, 20f), new Spot(10.1f, -1.1f, 20.1f), Reach));
        }

        [Fact]
        public void AFishLevelWithTheHookButMetresAwayAlongTheShoreDoesNotBite()
        {
            // Same sideways place and depth, three metres off on the other ground axis.
            Assert.False(Bite.FishAtHook(new Spot(10f, -1f, 23f), new Spot(10f, -1f, 20f), Reach));
        }

        [Fact]
        public void AFishBelowTheHookDoesNotBite()
        {
            Assert.False(Bite.FishAtHook(new Spot(10f, -3f, 20f), new Spot(10f, -1f, 20f), Reach));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.8f)]
        [InlineData(1.5f)]
        public void ReelingWithinTheHookWindowHooksTheFish(float sinceBite)
        {
            Assert.True(Bite.InHookWindow(sinceBite, 1.5f));
        }

        [Theory]
        [InlineData(1.51f)]
        [InlineData(5f)]
        [InlineData(-0.1f)]
        public void ReelingOutsideTheHookWindowHooksNothing(float sinceBite)
        {
            Assert.False(Bite.InHookWindow(sinceBite, 1.5f));
        }

        [Fact]
        public void TheHookWindowIsThreeTimesTheGamesHalfSecondByDefault()
        {
            Assert.Equal(1.5f, FishingRules.Default.HookWindow);
        }

        [Fact]
        public void AFloatWithNoBiteTakesOne()
        {
            Assert.True(Bite.TakesNibble(bitten: false, sinceBite: 0.2f, window: 1.5f));
        }

        [Fact]
        public void ABiteStillWithinItsHookWindowTakesNoSecondOne()
        {
            // The game takes a new nibble a second after the last, inside Rodnarok's longer hook window.
            Assert.False(Bite.TakesNibble(bitten: true, sinceBite: 1.1f, window: 1.5f));
            Assert.False(Bite.TakesNibble(bitten: true, sinceBite: 1.5f, window: 1.5f));
        }

        [Fact]
        public void OnceTheHookWindowIsOverTheFloatTakesABiteAgain()
        {
            Assert.True(Bite.TakesNibble(bitten: true, sinceBite: 1.6f, window: 1.5f));
        }

        [Fact]
        public void ALongerWindowHoldsOffASecondBiteLonger()
        {
            Assert.False(Bite.TakesNibble(bitten: true, sinceBite: 2.2f, window: 2.5f));
        }
    }
}
