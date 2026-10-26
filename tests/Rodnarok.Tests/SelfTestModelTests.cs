using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The in-game self-test's runner and command, which need no game: <c>/rodnarok selftest</c>
    /// runs every scenario a frame step at a time, tells each one's result in its log, and goes
    /// on past a scenario that fails or breaks.
    /// </summary>
    public class SelfTestModelTests
    {
        [Theory]
        [InlineData("selftest", "Run", "")]
        [InlineData("SelfTest  bait ", "Run", "bait")]
        [InlineData("selftest stop", "Stop", "")]
        [InlineData("selftest list", "List", "")]
        public void TheSelfTestAnswersItsOwnWords(string typed, string ask, string words)
        {
            var request = SelfTestCommand.Read(typed);
            Assert.NotNull(request);
            Assert.Equal(ask, request.Ask.ToString());
            Assert.Equal(words, request.Words);
        }

        [Theory]
        [InlineData("")]
        [InlineData("help")]
        [InlineData("selftests")]
        [InlineData(null)]
        public void OtherWordsAreLeftToRodnarok(string typed) => Assert.Null(SelfTestCommand.Read(typed));

        [Fact]
        public void EveryScenarioRunsAndEachResultIsTold()
        {
            var log = new List<string>();
            var runner = new ScenarioRunner(new[]
            {
                new Scenario("Passes", check => Frames(2, () => check.That(true, "fine"))),
                new Scenario("Fails", check => Frames(1, () => check.That(false, "the float stayed up"))),
            }, log.Add);
            RunOut(runner);
            Assert.Equal(1, runner.Passed);
            Assert.Equal(1, runner.Failed);
            Assert.Contains(log, l => l.StartsWith("PASS Passes", StringComparison.Ordinal));
            Assert.Contains(log, l => l.StartsWith("FAIL Fails", StringComparison.Ordinal) && l.Contains("the float stayed up", StringComparison.Ordinal));
        }

        [Fact]
        public void AScenarioThatBreaksFailsAndTheRunGoesOn()
        {
            var log = new List<string>();
            var runner = new ScenarioRunner(new[]
            {
                new Scenario("Breaks", check => Throwing()),
                new Scenario("After", check => Frames(1, () => check.That(true, "ran"))),
            }, log.Add);
            RunOut(runner);
            Assert.Equal(1, runner.Failed);
            Assert.Equal(1, runner.Passed);
            Assert.Contains(log, l => l.StartsWith("FAIL Breaks", StringComparison.Ordinal) && l.Contains("broken on purpose", StringComparison.Ordinal));
        }

        [Fact]
        public void AScenarioTakesOneFrameStepAtATime()
        {
            var ran = 0;
            var runner = new ScenarioRunner(new[] { new Scenario("Slow", check => Frames(3, () => ran++)) }, _ => { });
            runner.Step();
            Assert.True(runner.Running);
            RunOut(runner);
            Assert.False(runner.Running);
            Assert.Equal(3, ran);
        }

        [Fact]
        public void WordsPickTheScenariosWhoseNamesHoldThem()
        {
            var all = new[] { new Scenario("BaitsOfTheGame", c => Frames(1, () => { })), new Scenario("WaterCorners", c => Frames(1, () => { })) };
            Assert.Equal(new[] { "WaterCorners" }, ScenarioRunner.Pick(all, "water").Select(s => s.Name));
            Assert.Equal(2, ScenarioRunner.Pick(all, "").Count());
        }

        [Fact]
        public void StoppingEndsTheRunAndSaysSo()
        {
            var log = new List<string>();
            var runner = new ScenarioRunner(new[] { new Scenario("Long", check => Frames(100, () => { })) }, log.Add);
            runner.Step();
            runner.Stop();
            Assert.False(runner.Running);
            Assert.Contains(log, l => l.StartsWith("STOPPED", StringComparison.Ordinal));
        }

        [Fact]
        public void AStoppedScenarioStillPutsBackWhatItChanged()
        {
            var putBack = false;
            var runner = new ScenarioRunner(new[] { new Scenario("ChangesGear", check => Restoring(100, () => putBack = true)) }, _ => { });
            runner.Step();
            runner.Step();
            runner.Stop();
            Assert.True(putBack);
        }

        [Fact]
        public void AScenarioThatEndsEarlyStillPutsBackWhatItChanged()
        {
            var putBack = false;
            var runner = new ScenarioRunner(new[] { new Scenario("GivesUp", check => GivingUp(() => putBack = true)) }, _ => { });
            RunOut(runner);
            Assert.True(putBack);
        }

        private static IEnumerable Restoring(int frames, Action putBack)
        {
            try
            {
                for (var i = 0; i < frames; i++) yield return null;
            }
            finally
            {
                putBack();
            }
        }

        private static IEnumerable GivingUp(Action putBack)
        {
            try
            {
                yield return null;
                yield break;
            }
            finally
            {
                putBack();
            }
        }

        private static IEnumerable Frames(int count, Action each)
        {
            for (var i = 0; i < count; i++)
            {
                each();
                yield return null;
            }
        }

        private static IEnumerable Throwing()
        {
            yield return null;
            throw new InvalidOperationException("broken on purpose");
        }

        private static void RunOut(ScenarioRunner runner)
        {
            for (var i = 0; i < 1000 && runner.Running; i++) runner.Step();
        }
    }
}
