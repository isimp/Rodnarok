using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The rod's free secondary attack heaves the fish. In a rest, a heave lifts the hooked fish and its
    /// float three metres toward the rod in a moment, faster than any reel, for the rod's own secondary
    /// attack stamina as the game charges an attack; the line stays as long, so it hangs slack, and slack
    /// winds in three times as fast as a loaded reel. A struggle starting with slack still out jolts
    /// the line by the slack, a whole heave's slack nearly snapping it. Heaving into a struggle lifts
    /// nothing and only strains the line. The
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
        public void AHeaveCostsTheRodsOwnSecondaryAttackAsTheGameChargesOne()
        {
            // The fishing rod's m_secondaryAttack.m_attackStamina is 20, as an iron sword's or mace's secondary;
            // the game takes a third off at full skill in the weapon's skill (Attack.GetAttackStamina).
            Assert.Equal(20f, Heave.Cost(20f, 0f), 3);
            Assert.Equal(20f * (1f - 0.33f), Heave.Cost(20f, 1f), 3);
            Assert.Equal(20f * (1f - 0.33f * 0.5f), Heave.Cost(20f, 0.5f), 3);
            Assert.Equal(0f, Heave.Cost(0f, 0.3f), 3);
            Assert.Equal(Heave.Cost(20f, 1f), Heave.Cost(20f, 5f), 3);
        }

        [Fact]
        public void WithSlackStillOutAHeaveLiftsNothingSoEachMustBeWoundIn()
        {
            Assert.Equal(Heave.Pressed.Nothing, Heave.OnPress(true, true, false, false, true, true, slackOut: true));
            Assert.Equal(Heave.Pressed.Lifts, Heave.OnPress(true, true, false, false, true, true, slackOut: false));
            // Into a struggle a heave strains the line, slack or not.
            Assert.Equal(Heave.Pressed.Strains, Heave.OnPress(true, true, true, false, true, true, slackOut: true));
        }

        [Fact]
        public void EachHeaveBringsTheFishsNextStruggleTwoSecondsSooner()
        {
            Assert.Equal(2f, Heave.RestCut, 3);
            Assert.Equal(13f, Heave.ShortenRest(15f, 10f), 3);
        }

        [Fact]
        public void AHeaveInTheLastTwoSecondsOfARestHasTheFishBoltAsTheLiftEndsWithTheSlackOut()
        {
            // Pressed at 10 s, the lift ends at 10.3 s.
            var liftEnds = 10f + Heave.LiftSeconds;
            Assert.Equal(liftEnds, Heave.ShortenRest(11f, liftEnds), 3);
            Assert.Equal(liftEnds, Heave.ShortenRest(10.5f, liftEnds), 3);
            // A struggle already due before the lift ends comes no later for the heave.
            Assert.Equal(10.1f, Heave.ShortenRest(10.1f, liftEnds), 3);
        }

        [Fact]
        public void ALongRestTakesAFewPumpsAndAShortOneNone()
        {
            // The game's rests run from 1.25 to 5 s (a fish's m_escapeWaitMin to m_escapeWaitMax).
            Assert.Equal(2, Pumps(5f));
            Assert.Equal(1, Pumps(3f));
            Assert.Equal(0, Pumps(1.25f));
        }

        /// <summary>How many heaves a rest this long takes before the next would bring the struggle at once; counted up to 100.</summary>
        private static int Pumps(float rest)
        {
            var now = 0f;
            var next = rest;
            var heaves = 0;
            while (heaves < 100)
            {
                var after = Heave.ShortenRest(next, now);
                if (after <= now) return heaves;
                heaves++;
                next = after;
            }
            return heaves;
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
            Assert.Equal(0.1f + (1.5f - Heave.SlackFrom) * 0.35f, Heave.Jolt(0.1f, 1.5f), 3);
            Assert.Equal(0.1f, Heave.Jolt(0.1f, Heave.SlackFrom), 3);
            Assert.Equal(0.1f, Heave.Jolt(0.1f, 0f), 3);
            Assert.Equal(1f, Heave.Jolt(0.8f, 6f), 3);
            Assert.True(Heave.Jolt(0f, 1f) < Heave.Jolt(0f, 2f));
        }

        [Fact]
        public void AWholeHeavesSlackLeftOutNearlySnapsAnUnstrainedLine()
        {
            var jolt = Heave.Jolt(0f, Heave.Metres);
            Assert.True(jolt >= 0.9f && jolt < 1f, "a heave's slack jolts an unstrained line to " + jolt);
            Assert.Equal(1f, Heave.Jolt(0.1f, Heave.Metres), 3);
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
        public void TheRodSnapsUpForTheLiftAndComesDownAfter()
        {
            Assert.True(Heave.RodRaised(holding: false, since: 0f));
            Assert.True(Heave.RodRaised(false, Heave.LiftSeconds - 0.01f));
            Assert.False(Heave.PoseOver(false, Heave.LiftSeconds - 0.01f));
            Assert.True(Heave.PoseOver(false, Heave.LiftSeconds));
        }

        [Fact]
        public void HeldUpAlreadyTheRodDipsAndComesBackUp()
        {
            Assert.False(Heave.RodRaised(holding: true, since: 0f));
            Assert.False(Heave.RodRaised(true, Heave.DipSeconds - 0.01f));
            Assert.True(Heave.RodRaised(true, Heave.DipSeconds));
            Assert.True(Heave.PoseOver(true, Heave.DipSeconds));
            Assert.True(Heave.DipSeconds < Heave.LiftSeconds);
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
