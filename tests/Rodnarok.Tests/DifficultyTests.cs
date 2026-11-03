using System;
using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// One setting picks how hard the fishing is, in the five steps of the game's own combat
    /// setting, from Very easy to Very hard; Normal plays as Rodnarok did before the setting. A level
    /// sets the hook window, the line's strain and run, the stamina a held struggle costs, how wary a
    /// school is, how fast missed bites wear the bait down and whether fish bump the float first.
    /// Custom takes those from the separate settings instead. A level leaves the feature switches,
    /// the waits and the calm water to their own settings.
    /// </summary>
    public class DifficultyTests
    {
        private static readonly Difficulty[] Levels = { Difficulty.VeryEasy, Difficulty.Easy, Difficulty.Normal, Difficulty.Hard, Difficulty.VeryHard };

        private static FishingRules At(Difficulty level) => DifficultyLevels.Rules(level, FishingRules.Default);

        [Fact]
        public void TheLevelsAreTheGamesOwnFiveCombatStepsAndCustom()
        {
            Assert.Equal(new[] { "VeryEasy", "Easy", "Normal", "Hard", "VeryHard", "Custom" }, Enum.GetNames(typeof(Difficulty)));
        }

        [Fact]
        public void TheDefaultLevelIsNormal()
        {
            Assert.Equal(Difficulty.Normal, DifficultyLevels.Default);
        }

        [Fact]
        public void NormalPlaysAsTheRulesDidBefore()
        {
            var normal = At(Difficulty.Normal);
            var before = FishingRules.Default;
            Assert.Equal(before.HookWindow, normal.HookWindow);
            Assert.Equal(before.SnapSeconds, normal.SnapSeconds);
            Assert.Equal(before.EaseSeconds, normal.EaseSeconds);
            Assert.Equal(before.RunSpeed, normal.RunSpeed);
            Assert.Equal(before.StallStaminaPerSecond, normal.StallStaminaPerSecond);
            Assert.Equal(before.Wary, normal.Wary);
            Assert.Equal(before.Hearing, normal.Hearing);
            Assert.Equal(before.FrightToLeave, normal.FrightToLeave);
            Assert.Equal(before.MostGivingUp, normal.MostGivingUp);
            Assert.Equal(before.BaitLost, normal.BaitLost);
            Assert.Equal(before.BaitTakeChances, normal.BaitTakeChances);
            Assert.Equal(before.Bumps, normal.Bumps);
            Assert.Equal(before.ReelStaminaShare, normal.ReelStaminaShare);
        }

        [Fact]
        public void NormalKeepsTheNumbersRodnarokFishedByBefore()
        {
            var normal = At(Difficulty.Normal);
            Assert.Equal(1.5f, normal.HookWindow);
            Assert.Equal(1f, normal.SnapSeconds);
            Assert.Equal(1f, normal.EaseSeconds);
            Assert.Equal(2f, normal.RunSpeed);
            Assert.Equal(2f, normal.StallStaminaPerSecond);
            Assert.Equal(0.35f, normal.Hearing);
            Assert.Equal(8, normal.FrightToLeave);
            Assert.Equal(0.3, normal.MostGivingUp);
            Assert.Equal(new[] { 0.1, 0.4, 0.9, 0.98 }, normal.BaitTakeChances);
            Assert.False(normal.Bumps);
        }

        [Fact]
        public void EachLevelGivesLessTimeToHookThanTheOneBelow()
        {
            for (var i = 1; i < Levels.Length; i++) Assert.True(At(Levels[i]).HookWindow < At(Levels[i - 1]).HookWindow, Levels[i].ToString());
        }

        [Fact]
        public void EachLevelSnapsAHeldLineSoonerAndUnstrainsItSlower()
        {
            for (var i = 1; i < Levels.Length; i++)
            {
                Assert.True(At(Levels[i]).SnapSeconds < At(Levels[i - 1]).SnapSeconds, Levels[i].ToString());
                Assert.True(At(Levels[i]).EaseSeconds > At(Levels[i - 1]).EaseSeconds, Levels[i].ToString());
            }
        }

        [Fact]
        public void EachLevelLetsAStrugglingFishTakeMoreLineAndCostsMoreStaminaToHold()
        {
            for (var i = 1; i < Levels.Length; i++)
            {
                Assert.True(At(Levels[i]).RunSpeed > At(Levels[i - 1]).RunSpeed, Levels[i].ToString());
                Assert.True(At(Levels[i]).StallStaminaPerSecond > At(Levels[i - 1]).StallStaminaPerSecond, Levels[i].ToString());
            }
        }

        [Fact]
        public void VeryEasyHalvesTheGamesReelStaminaAndEasyTakesAQuarterOff()
        {
            Assert.Equal(0.5f, At(Difficulty.VeryEasy).ReelStaminaShare);
            Assert.Equal(0.75f, At(Difficulty.Easy).ReelStaminaShare);
        }

        [Fact]
        public void FromNormalOnReelingCostsTheGamesOwnStamina()
        {
            Assert.Equal(1f, At(Difficulty.Normal).ReelStaminaShare);
            Assert.Equal(1f, At(Difficulty.Hard).ReelStaminaShare);
            Assert.Equal(1f, At(Difficulty.VeryHard).ReelStaminaShare);
        }

        [Fact]
        public void OnVeryEasyReelingARestingFishCostsHalfAsMuch()
        {
            var anglerfish = new FishPull(restStamina: 14f, struggleStamina: 38f, quality: 1);
            var normal = Fight.Hold(struggling: false, seconds: 1f, skill: 0f, anglerfish, At(Difficulty.Normal)).Stamina;
            var easiest = Fight.Hold(struggling: false, seconds: 1f, skill: 0f, anglerfish, At(Difficulty.VeryEasy)).Stamina;
            Assert.Equal(normal / 2f, easiest, 4);
        }

        [Fact]
        public void TheGameIsToldTheSmallerReelCostOfAnEasyLevel()
        {
            Assert.Equal(7f, Fight.StaminaFigureWhileResting(At(Difficulty.VeryEasy), restStamina: 14f), 4);
            Assert.Equal(14f, Fight.StaminaFigureWhileResting(At(Difficulty.Normal), restStamina: 14f), 4);
        }

        [Fact]
        public void VeryEasyFishAreNotWaryTakeNoBaitAndNeverBump()
        {
            var easiest = At(Difficulty.VeryEasy);
            Assert.False(easiest.Wary);
            Assert.False(easiest.BaitLost);
            Assert.False(easiest.Bumps);
        }

        [Fact]
        public void FromEasyOnEachLevelsSchoolsHearFartherLeaveSoonerAndGiveUpMoreOften()
        {
            for (var i = 2; i < Levels.Length; i++)
            {
                var level = At(Levels[i]);
                var below = At(Levels[i - 1]);
                Assert.True(level.Wary && below.Wary, Levels[i].ToString());
                Assert.True(level.Hearing > below.Hearing, Levels[i].ToString());
                Assert.True(level.FrightToLeave < below.FrightToLeave, Levels[i].ToString());
                Assert.True(level.MostGivingUp > below.MostGivingUp, Levels[i].ToString());
            }
        }

        [Fact]
        public void FromEasyOnEachLevelsMissedBitesTakeTheBaitMoreOften()
        {
            for (var i = 2; i < Levels.Length; i++)
            {
                var level = At(Levels[i]);
                var below = At(Levels[i - 1]);
                Assert.True(level.BaitLost && below.BaitLost, Levels[i].ToString());
                Assert.True(BaitLoss.TakeChance(1, level.BaitTakeChances) > BaitLoss.TakeChance(1, below.BaitTakeChances), Levels[i].ToString());
                Assert.True(BaitLoss.TakeChance(2, level.BaitTakeChances) > BaitLoss.TakeChance(2, below.BaitTakeChances), Levels[i].ToString());
            }
        }

        [Fact]
        public void OnHardAndVeryHardTheFishBumpsTheFloatFirst()
        {
            Assert.False(At(Difficulty.Easy).Bumps);
            Assert.False(At(Difficulty.Normal).Bumps);
            Assert.True(At(Difficulty.Hard).Bumps);
            Assert.True(At(Difficulty.VeryHard).Bumps);
        }

        [Fact]
        public void VeryHardGivesTheGamesOwnHookWindowAndHearing()
        {
            var hardest = At(Difficulty.VeryHard);
            Assert.Equal(0.5f, hardest.HookWindow);
            Assert.Equal(1f, hardest.Hearing);
        }

        [Fact]
        public void OnVeryHardTheThirdMissAlwaysTakesTheBait()
        {
            Assert.Equal(1.0, BaitLoss.TakeChance(3, At(Difficulty.VeryHard).BaitTakeChances));
        }

        [Fact]
        public void OnHardAWalkerIsHeardWhereOnNormalTheyAreNot()
        {
            const float walking = 15f;
            Assert.False(Wary.Heard(distance: 6f, noise: walking, hearing: At(Difficulty.Normal).Hearing));
            Assert.True(Wary.Heard(distance: 6f, noise: walking, hearing: At(Difficulty.Hard).Hearing));
        }

        [Fact]
        public void OnVeryHardFourStartlesDriveTheSchoolOff()
        {
            var hardest = At(Difficulty.VeryHard);
            Assert.False(Wary.Leaves(Wary.Fright(heard: false) * 3, hardest.FrightToLeave));
            Assert.True(Wary.Leaves(Wary.Fright(heard: true) * 2, hardest.FrightToLeave));
        }

        [Fact]
        public void EveryLevelStaysWithinTheSettingsOwnRanges()
        {
            foreach (var level in Levels.Select(At))
            {
                Assert.InRange(level.HookWindow, 0.5f, 5f);
                Assert.InRange(level.SnapSeconds, 0.5f, 10f);
                Assert.InRange(level.EaseSeconds, 0.1f, 10f);
                Assert.InRange(level.RunSpeed, 0f, 10f);
                Assert.InRange(level.StallStaminaPerSecond, 0f, 20f);
                Assert.InRange(level.ReelStaminaShare, 0.1f, 2f);
            }
        }

        [Fact]
        public void CustomTakesTheSeparateSettingsAsTheyStand()
        {
            var settings = FishingRules.Default.With(hookWindow: 4f, snapSeconds: 7f, easeSeconds: 0.2f, runSpeed: 9f, stallStaminaPerSecond: 15f, reelStaminaShare: 0.3f, wary: false, baitLost: false, bumps: true);
            var custom = DifficultyLevels.Rules(Difficulty.Custom, settings);
            Assert.Equal(0.3f, custom.ReelStaminaShare);
            Assert.Equal(4f, custom.HookWindow);
            Assert.Equal(7f, custom.SnapSeconds);
            Assert.Equal(0.2f, custom.EaseSeconds);
            Assert.Equal(9f, custom.RunSpeed);
            Assert.Equal(15f, custom.StallStaminaPerSecond);
            Assert.False(custom.Wary);
            Assert.False(custom.BaitLost);
            Assert.True(custom.Bumps);
        }

        [Fact]
        public void ALevelOverridesTheSeparateNumberSettings()
        {
            var settings = FishingRules.Default.With(hookWindow: 4f, snapSeconds: 7f, wary: false, bumps: false);
            var hard = DifficultyLevels.Rules(Difficulty.Hard, settings);
            Assert.Equal(At(Difficulty.Hard).HookWindow, hard.HookWindow);
            Assert.Equal(At(Difficulty.Hard).SnapSeconds, hard.SnapSeconds);
            Assert.True(hard.Wary);
            Assert.True(hard.Bumps);
        }

        [Fact]
        public void ALevelKeepsTheFeatureSwitchesWaitsAndCalmWaterOfTheSettings()
        {
            var settings = FishingRules.Default.With(waitMin: 50f, waitMax: 90f, calmReach: 10f, calmKeep: 0.5f, schools: false, tension: false, pullBySpeed: false, biomeFishOnBait: false, callWorldFish: false);
            var hard = DifficultyLevels.Rules(Difficulty.Hard, settings);
            Assert.Equal(50f, hard.WaitMin);
            Assert.Equal(90f, hard.WaitMax);
            Assert.Equal(10f, hard.CalmReach);
            Assert.Equal(0.5f, hard.CalmKeep);
            Assert.False(hard.Schools);
            Assert.False(hard.Tension);
            Assert.False(hard.PullBySpeed);
            Assert.False(hard.BiomeFishOnBait);
            Assert.False(hard.CallWorldFish);
        }
    }
}
