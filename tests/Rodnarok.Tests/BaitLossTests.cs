using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Missed bites wear the bait down: a bite not hooked in time is a miss, and the third miss on a
    /// float takes the bait: the float is bare, nothing bites it again, and reeling it in gives no
    /// bait back. A hooked bite, or one still within the hook window, is no miss, and with the
    /// setting off misses cost nothing, as in the game.
    /// </summary>
    public class BaitLossTests
    {
        private const float Window = 1.5f;

        [Fact]
        public void ABiteNotHookedPastTheHookWindowIsAMiss()
        {
            Assert.True(BaitLoss.Missed(bitten: true, hooked: false, sinceBite: Window + 0.1f, window: Window));
        }

        [Fact]
        public void ABiteStillWithinTheHookWindowIsNoMissYet()
        {
            Assert.False(BaitLoss.Missed(bitten: true, hooked: false, sinceBite: Window - 0.1f, window: Window));
        }

        [Fact]
        public void AHookedBiteIsNoMiss()
        {
            Assert.False(BaitLoss.Missed(bitten: true, hooked: true, sinceBite: Window + 5f, window: Window));
        }

        [Fact]
        public void NoBiteIsNoMiss()
        {
            Assert.False(BaitLoss.Missed(bitten: false, hooked: false, sinceBite: Window + 5f, window: Window));
        }

        [Fact]
        public void TheFirstMissesOnlyNibbleTheBait()
        {
            for (var misses = 1; misses < BaitLoss.MissesToTake; misses++) Assert.False(BaitLoss.Taken(on: true, misses));
            Assert.InRange(BaitLoss.MissesToTake, 2, 4);
        }

        [Fact]
        public void TheLastMissTakesTheBait()
        {
            Assert.True(BaitLoss.Taken(on: true, BaitLoss.MissesToTake));
        }

        [Fact]
        public void WithTheSettingOffMissesCostNothingAsInTheGame()
        {
            Assert.False(BaitLoss.Taken(on: false, misses: 10));
        }

        [Fact]
        public void MissesWearTheBaitUnlessTheServerSaysOtherwise()
        {
            Assert.True(new FishingRules().BaitLost);
        }
    }
}
