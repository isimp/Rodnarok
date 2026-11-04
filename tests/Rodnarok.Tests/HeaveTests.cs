using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The rod's free secondary attack heaves the fish. In a rest, a heave lifts the hooked fish and its
    /// float three metres toward the rod in a moment, faster than any reel, for what the game charges
    /// for pulling that fish while it struggles; the line stays as long, so it hangs slack, and slack
    /// winds in three times as fast as a loaded reel. A struggle starting with slack still out jolts
    /// the line by the slack. Heaving into a struggle lifts nothing and only strains the line. The
    /// server switches it on or off for everyone; a difficulty level leaves it as set.
    /// </summary>
    public class HeaveTests
    {
        [Fact]
        public void AHeaveInARestLiftsTheFishThreeMetresInAMoment()
        {
            Assert.Equal(Heave.Pressed.Lifts, Heave.OnPress(on: true, hooked: true, struggling: false, lifting: false, tension: true, staminaEnough: true));
            Assert.Equal(3f, Heave.LiftSpeed * Heave.LiftSeconds, 3);
            Assert.True(Heave.LiftSeconds <= 0.5f);
            // Faster than the game's reel at its best (the float's m_pullLineSpeedMaxSkill, 6 m/s).
            Assert.True(Heave.LiftSpeed > 6f, "lift " + Heave.LiftSpeed);
        }

        [Fact]
        public void AHeaveCostsWhatPullingTheStrugglingFishCostsForTheLift()
        {
            // A Perch's m_escapeStaminaUse is 10, a Northern Salmon's 60; the game lowers reeling costs with Fishing skill by the float's share.
            Assert.Equal(10f * Heave.LiftSeconds, Heave.Cost(10f, 1, 0f, 0.2f), 3);
            Assert.Equal(60f * 3f * Heave.LiftSeconds, Heave.Cost(60f, 3, 0f, 0.2f), 3);
            Assert.Equal(10f * Heave.LiftSeconds * 0.2f, Heave.Cost(10f, 1, 1f, 0.2f), 3);
            Assert.Equal(Heave.Cost(10f, 1, 0f, 0.2f), Heave.Cost(10f, 0, 0f, 0.2f), 3);
        }

        [Fact]
        public void WithoutTheStaminaForItAHeaveLiftsNothing()
        {
            Assert.Equal(Heave.Pressed.Nothing, Heave.OnPress(true, true, false, false, true, staminaEnough: false));
        }

        [Fact]
        public void NothingHappensWithNoFishOnDuringALiftOrSwitchedOff()
        {
            Assert.Equal(Heave.Pressed.Nothing, Heave.OnPress(true, hooked: false, false, false, true, true));
            Assert.Equal(Heave.Pressed.Nothing, Heave.OnPress(true, true, false, lifting: true, true, true));
            Assert.Equal(Heave.Pressed.Nothing, Heave.OnPress(on: false, true, false, false, true, true));
        }

        [Fact]
        public void HeavingIntoAStruggleLiftsNothingAndOnlyStrainsTheLine()
        {
            Assert.Equal(Heave.Pressed.Strains, Heave.OnPress(true, true, struggling: true, false, tension: true, staminaEnough: true));
            Assert.Equal(Heave.Pressed.Strains, Heave.OnPress(true, true, struggling: true, false, tension: true, staminaEnough: false));
            Assert.Equal(Heave.Pressed.Nothing, Heave.OnPress(true, true, struggling: true, false, tension: false, staminaEnough: true));
            Assert.Equal(0.75f, Heave.StruggleHeave(0.5f), 3);
            Assert.Equal(1f, Heave.StruggleHeave(0.9f), 3);
        }

        [Fact]
        public void TheLineHangsSlackOnlyWhereItIsLongerThanTheWayToTheFloat()
        {
            Assert.Equal(3f, Heave.Slack(18f, 15f), 3);
            Assert.Equal(0f, Heave.Slack(15f, 15.5f), 3);
            Assert.False(Heave.HasSlack(Heave.SlackFrom));
            Assert.True(Heave.HasSlack(Heave.SlackFrom + 0.1f));
        }

        [Fact]
        public void SlackWindsInThreeTimesAsFastAsALoadedReelButNoFurther()
        {
            // The game reels 2 m/s at Fishing 0 (m_pullLineSpeed); slack adds twice that again.
            Assert.Equal(2f * 2f * 0.1f, Heave.WindExtra(2f, 2f, 0.1f), 3);
            Assert.Equal(0.5f, Heave.WindExtra(0.5f, 2f, 1f), 3);
            Assert.Equal(0f, Heave.WindExtra(Heave.SlackFrom, 2f, 0.1f), 3);
            Assert.Equal(3f, Heave.WindFactor, 3);
        }

        [Fact]
        public void AStruggleStartingWithSlackOutJoltsTheLineByTheSlack()
        {
            Assert.Equal(0.1f + (3f - Heave.SlackFrom) * 0.2f, Heave.Jolt(0.1f, 3f), 3);
            Assert.Equal(0.1f, Heave.Jolt(0.1f, Heave.SlackFrom), 3);
            Assert.Equal(0.1f, Heave.Jolt(0.1f, 0f), 3);
            Assert.Equal(1f, Heave.Jolt(0.8f, 6f), 3);
            Assert.True(Heave.Jolt(0f, 1f) < Heave.Jolt(0f, 2f));
        }

        [Fact]
        public void HeavingAndWindingInARestGainsLineFasterThanReeling()
        {
            // The game's reel runs from 2 m/s at Fishing 0 to 6 m/s at 100 (m_pullLineSpeed, m_pullLineSpeedMaxSkill).
            foreach (var (reel, atLeast) in new[] { (2f, 1.4f), (4f, 1.4f), (6f, 1f) })
            {
                var windTime = (Heave.LiftSpeed * Heave.LiftSeconds - Heave.SlackFrom) / (reel * Heave.WindFactor);
                var heaved = Heave.LiftSpeed * Heave.LiftSeconds / (Heave.LiftSeconds + windTime);
                Assert.True(heaved >= reel * atLeast, "at a reel of " + reel + " m/s a heave and wind gains " + heaved + " m/s");
            }
        }

        [Fact]
        public void TheHeaveIsOnUnlessTheServerSwitchesItOff()
        {
            Assert.True(FishingRules.Default.Heave);
            Assert.False(FishingRules.Default.With(heave: false).Heave);
            var settings = FishingRules.Default.With(heave: false);
            foreach (var level in new[] { Difficulty.VeryEasy, Difficulty.Normal, Difficulty.VeryHard })
                Assert.False(DifficultyLevels.Rules(level, settings).Heave);
        }
    }
}
