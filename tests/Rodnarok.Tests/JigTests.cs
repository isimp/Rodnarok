using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float, and the crosshair ring tells when.
    /// While a fish waits at the float, the one picked to bite as it hovers or a shy school as it
    /// hesitates, the ring keeps the bite's size and fades in as the fish comes closer across the water:
    /// faint at the edge of its hover, whole white within reach. The waiting fish makes a steady run in
    /// to the float now and then and holds there a moment, so the ring fades in on a rhythm that can be
    /// read ahead. The first jig of a wait with the fish in reach draws it 1.5 s sooner; any other jig
    /// is off cue, and a wary school near the float flinches. Every jig flicks the rod up a moment.
    /// The server switches it on or off; a difficulty level leaves it as set.
    /// </summary>
    public class JigTests
    {
        [Fact]
        public void AJigTwitchesTheFloatAQuarterMetreTowardTheRodInAMoment()
        {
            Assert.Equal(0.25f, Jig.Metres, 3);
            Assert.True(Jig.Seconds <= 0.3f);
            Assert.Equal(Jig.Metres / Jig.Seconds, Jig.Speed, 3);
            // Slower than the game's hook-by-motion: a float moving over 2 m/s sideways hooks a nibbling fish.
            Assert.True(Jig.Speed < 2f);
            // Too gentle for a wary school's own speed rule over its half second.
            Assert.True(Jig.Metres / 0.5f < Wary.FloatSpeed);
        }

        [Fact]
        public void TheRingIsWholeWithTheFishInReachAcrossTheWater()
        {
            Assert.Equal(0.6f, Jig.Reach, 3);
            Assert.Equal(1f, Jig.RingStrength(0.2f), 3);
            Assert.Equal(1f, Jig.RingStrength(Jig.Reach), 3);
            Assert.True(Jig.InReach(Jig.Reach));
            Assert.False(Jig.InReach(Jig.Reach + 0.01f));
        }

        [Fact]
        public void TheRingIsFaintAtTheEdgeOfTheHoverAndBeyond()
        {
            // The picked fish hovers within 1.5 m of the float (Bumps.Hover).
            Assert.Equal(1.5f, Jig.Hover, 3);
            Assert.Equal(Jig.Faint, Jig.RingStrength(Jig.Hover), 3);
            Assert.Equal(Jig.Faint, Jig.RingStrength(4f), 3);
            Assert.True(Jig.Faint > 0f && Jig.Faint <= 0.2f, "faint " + Jig.Faint);
        }

        [Fact]
        public void TheRingFadesInSmoothlyAsTheFishComesCloser()
        {
            var strength = Jig.RingStrength(Jig.Hover);
            for (var d = Jig.Hover - 0.05f; d > Jig.Reach; d -= 0.05f)
            {
                Assert.True(Jig.RingStrength(d) > strength, "at " + d + " m");
                strength = Jig.RingStrength(d);
            }
        }

        [Fact]
        public void TheFirstJigOfEachHoldWithTheFishInReachIsGood()
        {
            Assert.Equal(Jig.Judged.Good, Jig.Judge(usedThisHold: false, inWindow: true));
            Assert.Equal(Jig.Judged.OffCue, Jig.Judge(usedThisHold: false, inWindow: false));
            Assert.Equal(Jig.Judged.OffCue, Jig.Judge(usedThisHold: true, inWindow: true));
            Assert.Equal(Jig.Judged.OffCue, Jig.Judge(usedThisHold: true, inWindow: false));
        }

        [Fact]
        public void TheWindowIsTheHoldWithTheFishInReachAndNothingElse()
        {
            Assert.True(Jig.InWindow(Jig.Nose.Hold, Jig.HoldAt));
            Assert.False(Jig.InWindow(Jig.Nose.Hold, Jig.Reach + 0.1f));
            Assert.False(Jig.InWindow(Jig.Nose.Run, Jig.HoldAt));
            Assert.False(Jig.InWindow(Jig.Nose.Free, 0.1f));
        }

        [Fact]
        public void ALongerWaitHoldsMoreThanOnceSoMoreThanOneJigCanCount()
        {
            // A picked fish hovers up to 4 s: two holds, each its own window.
            var holds = 0;
            var wasHold = false;
            for (var t = 0f; t < 4f; t += 0.02f)
            {
                var hold = Jig.NoseAt(t, 4f - t).Phase == Jig.Nose.Hold;
                if (hold && !wasHold) holds++;
                wasHold = hold;
            }
            Assert.Equal(2, holds);
            Assert.NotEqual(Jig.HoldIndex(Jig.FirstNose + Jig.RunSeconds + 0.1f), Jig.HoldIndex(Jig.FirstNose + Jig.NoseEvery + Jig.RunSeconds + 0.1f));
            Assert.Equal(Jig.HoldIndex(Jig.FirstNose + Jig.RunSeconds + 0.1f), Jig.HoldIndex(Jig.FirstNose + Jig.RunSeconds + 0.4f));
        }

        [Fact]
        public void TheWaitingFishRunsInSteadilyThenHoldsInReachThenGoesFree()
        {
            Assert.Equal(Jig.Nose.Free, Jig.NoseAt(0f, 10f).Phase);
            var run = Jig.NoseAt(Jig.FirstNose + Jig.RunSeconds * 0.5f, 10f);
            Assert.Equal(Jig.Nose.Run, run.Phase);
            Assert.Equal(0.5f, run.Progress, 2);
            Assert.Equal(Jig.Nose.Hold, Jig.NoseAt(Jig.FirstNose + Jig.RunSeconds + 0.05f, 10f).Phase);
            Assert.Equal(Jig.Nose.Free, Jig.NoseAt(Jig.FirstNose + Jig.RunSeconds + Jig.HoldSeconds + 0.05f, 10f).Phase);
            Assert.Equal(Jig.Nose.Run, Jig.NoseAt(Jig.FirstNose + Jig.NoseEvery + 0.05f, 10f).Phase);
            Assert.True(Jig.RunSeconds + Jig.HoldSeconds < Jig.NoseEvery);
        }

        [Fact]
        public void EveryWaitHoldsTheFishInReachAtLeastOnce()
        {
            // The picked fish hovers 2 to 4 s; within the shortest it holds by the float once, before it may bite.
            var held = false;
            for (var t = 0f; t < 2f; t += 0.02f) held |= Jig.NoseAt(t, 2f - t).Phase == Jig.Nose.Hold;
            Assert.True(held);
            // Not in the last moment before it goes for the bait, which the bite's own approach takes over.
            Assert.Equal(Jig.Nose.Free, Jig.NoseAt(Jig.FirstNose + 0.1f, Jig.NoseLeaveBefore - 0.01f).Phase);
        }

        [Fact]
        public void TheRunIsSteadyAndEndsWithinReach()
        {
            Assert.True(Jig.HoldAt < Jig.Reach, "holds at " + Jig.HoldAt);
            Assert.Equal((1.5f - Jig.HoldAt) / Jig.RunSeconds, Jig.RunSpeed(1.5f), 3);
            Assert.Equal((2.4f - Jig.HoldAt) / Jig.RunSeconds, Jig.RunSpeed(2.4f), 3);
            Assert.Equal(0f, Jig.RunSpeed(Jig.HoldAt - 0.1f), 3);
        }

        [Fact]
        public void AJigOnlyTwitchesAFloatOnTheWaterWithNothingHooked()
        {
            Assert.True(Jig.OnPress(on: true, onWater: true, hooked: false));
            Assert.False(Jig.OnPress(true, onWater: false, false));
            Assert.False(Jig.OnPress(true, true, hooked: true));
            Assert.False(Jig.OnPress(on: false, true, false));
        }

        [Fact]
        public void AGoodJigDrawsAHoveringFishSooner()
        {
            Assert.Equal(10f + 3f - Jig.Cut, Jig.SoonerFree(10f + 3f, 10f), 3);
            Assert.Equal(10f, Jig.SoonerFree(10.5f, 10f), 3);
            Assert.Equal(float.MaxValue, Jig.SoonerFree(float.MaxValue, 10f));
            Assert.Equal(9f, Jig.SoonerFree(9f, 10f), 3);
            Assert.True(Jig.Cut < 2f && Jig.Cut >= 1f, "cut " + Jig.Cut);
        }

        [Fact]
        public void AGoodJigHurriesAHesitatingSchool()
        {
            Assert.Equal(20f - Jig.Cut, Jig.SoonerReady(20f), 3);
            Assert.Equal(-1f, Jig.SoonerReady(-1f), 3);
        }

        [Fact]
        public void EveryJigFlicksTheRodUpAMoment()
        {
            Assert.True(Jig.FlickSeconds < Heave.LiftSeconds);
            Assert.True(Jig.RodRaised(holding: false, since: 0f));
            Assert.False(Jig.RodRaised(false, Jig.FlickSeconds));
            Assert.True(Jig.PoseOver(false, Jig.FlickSeconds));
            Assert.False(Jig.PoseOver(false, Jig.FlickSeconds - 0.01f));
            Assert.False(Jig.RodRaised(holding: true, since: 0f));
            Assert.True(Jig.RodRaised(true, Heave.DipSeconds));
        }

        [Fact]
        public void TheJigIsOnUnlessTheServerSwitchesItOff()
        {
            Assert.True(FishingRules.Default.Jig);
            var settings = FishingRules.Default.With(jig: false);
            Assert.False(settings.Jig);
            foreach (var level in new[] { Difficulty.VeryEasy, Difficulty.Normal, Difficulty.VeryHard })
                Assert.False(DifficultyLevels.Rules(level, settings).Jig);
        }
    }
}
