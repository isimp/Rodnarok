using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Missed bites wear the bait down, more likely each time: a bite not hooked in time is a miss;
    /// the first miss on a float very seldom takes the bait, the second more often, the third almost
    /// always, and so on. A fish that missed has learned and bites that float no more; once every
    /// fish of the school has learned, the school swims off. A hooked bite, or one still within the
    /// hook window, is no miss, and with the setting off misses cost nothing, as in the game.
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
        public void TheFirstMissVerySeldomTakesTheBait()
        {
            Assert.InRange(BaitLoss.TakeChance(1), 0.01, 0.15);
        }

        [Fact]
        public void EachMissIsMoreLikelyToTakeTheBaitThanTheOneBefore()
        {
            for (var misses = 1; misses < 4; misses++) Assert.True(BaitLoss.TakeChance(misses + 1) > BaitLoss.TakeChance(misses));
        }

        [Fact]
        public void TheThirdMissAlmostAlwaysTakesTheBait()
        {
            Assert.InRange(BaitLoss.TakeChance(3), 0.85, 0.95);
        }

        [Fact]
        public void LaterMissesKeepTakingItAlmostSurely()
        {
            Assert.InRange(BaitLoss.TakeChance(10), 0.95, 1.0);
        }

        [Fact]
        public void TheRollDecidesWithinTheChance()
        {
            Assert.True(BaitLoss.Taken(on: true, misses: 2, roll: BaitLoss.TakeChance(2) - 0.01));
            Assert.False(BaitLoss.Taken(on: true, misses: 2, roll: BaitLoss.TakeChance(2) + 0.01));
        }

        [Fact]
        public void WithTheSettingOffMissesCostNothingAsInTheGame()
        {
            Assert.False(BaitLoss.Taken(on: false, misses: 10, roll: 0.0));
        }

        [Fact]
        public void ASchoolWhoseEveryFishMissedSwimsOff()
        {
            Assert.False(BaitLoss.SchoolLearned(learned: 2, size: 3));
            Assert.True(BaitLoss.SchoolLearned(learned: 3, size: 3));
            Assert.True(BaitLoss.SchoolLearned(learned: 1, size: 1));
        }

        [Fact]
        public void MissesWearTheBaitUnlessTheServerSaysOtherwise()
        {
            Assert.True(new FishingRules().BaitLost);
        }
    }
}
