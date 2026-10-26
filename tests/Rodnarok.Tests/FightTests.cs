using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Holding the line while the fish rests reels it in at the game's pace and cost. Holding it
    /// while the fish struggles stalls: no line is gained, and it costs a little stamina, the same
    /// for every fish and far below what the game charges, so a struggle never drains you empty.
    /// </summary>
    public class FightTests
    {
        private static readonly FishPull Perch = new FishPull(restStamina: 3f, struggleStamina: 10f, quality: 1);
        private static readonly FishPull Salmon = new FishPull(restStamina: 20f, struggleStamina: 60f, quality: 3);

        [Fact]
        public void HoldingWhileItRestsReelsInAtTheGamesPace()
        {
            var step = Fight.Hold(struggling: false, seconds: 1f, skill: 0f, Perch, FishingRules.Default);
            Assert.Equal(2f, step.Line, 3);
            Assert.Equal(3f, step.Stamina, 3);
        }

        [Fact]
        public void FullSkillReelsThreeTimesFasterForAFifthOfTheCost()
        {
            var step = Fight.Hold(struggling: false, seconds: 1f, skill: 1f, Salmon, FishingRules.Default);
            Assert.Equal(6f, step.Line, 3);
            Assert.Equal(20f * 3 * 0.2f, step.Stamina, 3);
        }

        [Fact]
        public void HoldingWhileItStrugglesGainsNoLine()
        {
            Assert.Equal(0f, Fight.Hold(struggling: true, seconds: 1f, skill: 0.5f, Salmon, FishingRules.Default).Line);
        }

        [Fact]
        public void HoldingThroughAStruggleCostsALittleTheSameForEveryFish()
        {
            var rules = FishingRules.Default;
            var perch = Fight.Hold(struggling: true, seconds: 1f, skill: 0f, Perch, rules);
            var salmon = Fight.Hold(struggling: true, seconds: 1f, skill: 0f, Salmon, rules);
            Assert.Equal(rules.StallStaminaPerSecond, perch.Stamina, 3);
            Assert.Equal(perch.Stamina, salmon.Stamina, 3);
            Assert.True(perch.Stamina > 0f);
        }

        [Fact]
        public void AStruggleCostsLessHeldThroughThanTheGameChargesForAnyFish()
        {
            var stall = Fight.Hold(struggling: true, seconds: 1f, skill: 0f, Perch, FishingRules.Default).Stamina;
            var gameCheapest = new[] { 10f, 15f, 20f, 25f, 28f, 30f, 32f, 34f, 38f, 40f, 50f, 60f }.Min();
            Assert.True(stall < gameCheapest);
        }

        [Fact]
        public void SkillLowersTheStallCostAsItLowersReeling()
        {
            var unskilled = Fight.Hold(struggling: true, seconds: 1f, skill: 0f, Perch, FishingRules.Default).Stamina;
            var master = Fight.Hold(struggling: true, seconds: 1f, skill: 1f, Perch, FishingRules.Default).Stamina;
            Assert.Equal(unskilled * 0.2f, master, 3);
        }

        [Fact]
        public void ACostOfZeroMakesStallingFree()
        {
            var rules = FishingRules.Default.With(stallStaminaPerSecond: 0f);
            Assert.Equal(0f, Fight.Hold(struggling: true, seconds: 1f, skill: 0f, Salmon, rules).Stamina);
        }

        [Fact]
        public void AStepScalesWithItsLength()
        {
            var whole = Fight.Hold(struggling: false, seconds: 1f, skill: 0f, Perch, FishingRules.Default);
            var part = Fight.Hold(struggling: false, seconds: 0.02f, skill: 0f, Perch, FishingRules.Default);
            Assert.Equal(whole.Line * 0.02f, part.Line, 4);
            Assert.Equal(whole.Stamina * 0.02f, part.Stamina, 4);
        }
    }
}
