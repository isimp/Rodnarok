using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A self-test run need not take every scenario each time. A session that changed something
    /// writes a plan, <c>BepInEx/Rodnarok-selftest.plan</c>: why, and the scenarios it asks for by
    /// name or by words, and whether it starts by itself once a character stands in a world. A
    /// scenario comes off the plan once it passes, and the plan goes once nothing is left.
    /// <c>/rodnarok selftest failed</c> runs again what failed or was skipped last, and words pick
    /// every scenario whose name holds all of them, in any order.
    /// </summary>
    public class SelfTestPlanTests
    {
        private static readonly string[] All =
        {
            "ABaitDrawsASchoolThatTakesItScattersAndLeaves",
            "EachFishComingToAFloatShowsItsOwnSign",
            "ASerpentTrophyOnAStandCalmsTheWaterNearItOnceSwitchedOn",
            "ARodCastsAndReelsWhileSittingAndTheCharacterStaysSeated",
        };

        private const string PlanText =
            "why\tThe Trollfish's sign stands up from the water\n" +
            "words\tsign fish\n" +
            "part\taRodCastsAndReelsWhileSittingAndTheCharacterStaysSeated\n" +
            "words\tno such scenario\n" +
            "run\ton-load\n";

        [Fact]
        public void APlanNamesScenariosByNameOrByWordsInTheRunsOrderAndSaysWhatMatchedNothing()
        {
            var plan = SelfTestPlan.Load(PlanText);
            Assert.Equal("The Trollfish's sign stands up from the water", plan.Why);
            Assert.True(plan.OnLoad);
            Assert.Equal(new[] { All[1], All[3] }, plan.Parts(All));
            Assert.Equal(new[] { "no such scenario" }, plan.Unmatched(All));
        }

        [Fact]
        public void AScenarioThatPassedComesOffAndThePlanGoesOnceAllHave()
        {
            var plan = SelfTestPlan.Load(PlanText);
            var left = plan.After(All, new[] { (All[1], ScenarioOutcome.Passed), (All[3], ScenarioOutcome.Failed) });
            Assert.Equal(new[] { All[3] }, left.Parts(All));
            Assert.Equal(plan.Why, left.Why);
            Assert.True(left.OnLoad);
            var none = left.After(All, new[] { (All[3], ScenarioOutcome.Passed) });
            Assert.True(none.Done);
            Assert.True(SelfTestPlan.Load("").Done);
            Assert.True(SelfTestPlan.Load(null).Done);
        }

        [Fact]
        public void ASkippedScenarioStaysOnThePlanAsItWasNotChecked()
        {
            var left = SelfTestPlan.Load(PlanText).After(All, new[] { (All[1], ScenarioOutcome.Skipped), (All[3], ScenarioOutcome.Passed) });
            Assert.Equal(new[] { All[1] }, left.Parts(All));
        }

        [Fact]
        public void APlanWrittenAgainReadsTheSame()
        {
            var left = SelfTestPlan.Load(PlanText).After(All, new[] { (All[3], ScenarioOutcome.Passed) });
            var again = SelfTestPlan.Load(left.Save());
            Assert.Equal(left.Why, again.Why);
            Assert.Equal(left.OnLoad, again.OnLoad);
            Assert.Equal(left.Parts(All), again.Parts(All));
        }

        [Fact]
        public void APlanWithoutOnLoadWaitsToBeAskedFor()
        {
            Assert.False(SelfTestPlan.Load("words\tsign\n").OnLoad);
        }

        [Fact]
        public void FailedRunsAgainWhatFailedOrWasSkippedLastAndKeepsWhatWasNotRunAgain()
        {
            var before = SelfTestLeft.Save(new[] { All[0], All[2] });
            var after = SelfTestLeft.After(SelfTestLeft.Load(before), new[] { (All[2], ScenarioOutcome.Passed), (All[1], ScenarioOutcome.Failed), (All[3], ScenarioOutcome.Skipped) });
            Assert.Equal(new[] { All[0], All[1], All[3] }, after.OrderBy(n => System.Array.IndexOf(All, n)));
            Assert.Empty(SelfTestLeft.Load(null));
            Assert.Equal(new[] { All[0], All[2] }, SelfTestLeft.Load(before));
        }

        [Theory]
        [InlineData("selftest failed", "Failed")]
        [InlineData("selftest plan", "Plan")]
        [InlineData("SelfTest  FAILED ", "Failed")]
        public void TheSelfTestAnswersFailedAndPlan(string typed, string ask)
        {
            Assert.Equal(ask, SelfTestCommand.Read(typed).Ask.ToString());
        }

        [Fact]
        public void WordsPickEveryScenarioWhoseNameHoldsAllOfThemInAnyOrder()
        {
            var all = All.Select(n => new Scenario(n, c => Nothing())).ToList();
            Assert.Equal(new[] { All[1] }, ScenarioRunner.Pick(all, "sign fish").Select(s => s.Name));
            Assert.Equal(new[] { All[1] }, ScenarioRunner.Pick(all, "fish sign").Select(s => s.Name));
            Assert.Equal(new[] { All[2] }, ScenarioRunner.Pick(all, "stand water").Select(s => s.Name));
            Assert.Empty(ScenarioRunner.Pick(all, "sign serpent"));
        }

        [Fact]
        public void TheRunnerTellsEachScenariosOutcome()
        {
            var runner = new ScenarioRunner(new[]
            {
                new Scenario("Good", c => Nothing()),
                new Scenario("Bad", c => Fails(c)),
                new Scenario("Elsewhere", c => Needs(c)),
            }, _ => { });
            for (var i = 0; i < 20 && runner.Running; i++) runner.Step();
            Assert.Equal(new[] { ("Good", ScenarioOutcome.Passed), ("Bad", ScenarioOutcome.Failed), ("Elsewhere", ScenarioOutcome.Skipped) }, runner.Outcomes);
        }

        private static IEnumerable Nothing()
        {
            yield break;
        }

        private static IEnumerable Fails(ScenarioCheck check)
        {
            check.That(false, "broken");
            yield break;
        }

        private static IEnumerable Needs(ScenarioCheck check)
        {
            check.Needs(false, "deep water");
            yield break;
        }
    }
}
