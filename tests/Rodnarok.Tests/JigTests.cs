using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float, and the crosshair ring tells when.
    /// While a fish waits at the float, the one picked to bite as it hovers or a shy school as it
    /// hesitates, the ring breathes with the fish: at the bite's size and whole white with it in reach
    /// of the hook, twice the size and faint at the edge of its hover, and the fish noses in now and
    /// then so every wait has its moments. The first jig of a wait with the fish in reach draws it
    /// 1.5 s sooner; any other jig is off cue, and a wary school near the float flinches. Every jig
    /// flicks the rod up a moment. The server switches it on or off; a difficulty level leaves it.
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
        public void TheRingIsAtTheBitesSizeAndWholeWithTheFishInReach()
        {
            Assert.Equal(0.6f, Jig.Reach, 3);
            Assert.Equal(1f, Jig.RingScale(0.3f), 3);
            Assert.Equal(1f, Jig.RingScale(Jig.Reach), 3);
            Assert.Equal(1f, Jig.RingStrength(Jig.Reach), 3);
            Assert.True(Jig.InReach(Jig.Reach));
            Assert.False(Jig.InReach(Jig.Reach + 0.01f));
        }

        [Fact]
        public void TheRingIsTwiceTheSizeAndFaintAtTheEdgeOfTheHover()
        {
            // The picked fish hovers within 1.5 m of the float (Bumps.Hover).
            Assert.Equal(1.5f, Jig.Hover, 3);
            Assert.Equal(2f, Jig.RingScale(Jig.Hover), 3);
            Assert.Equal(2f, Jig.RingScale(4f), 3);
            Assert.Equal(0.25f, Jig.RingStrength(Jig.Hover), 3);
        }

        [Fact]
        public void TheRingBreathesSmoothlyWithTheFishsDistance()
        {
            var size = Jig.RingScale(Jig.Hover);
            var strength = Jig.RingStrength(Jig.Hover);
            for (var d = Jig.Hover - 0.05f; d > Jig.Reach; d -= 0.05f)
            {
                Assert.True(Jig.RingScale(d) < size && Jig.RingStrength(d) > strength, "at " + d + " m");
                size = Jig.RingScale(d);
                strength = Jig.RingStrength(d);
            }
        }

        [Fact]
        public void OnlyTheFirstJigOfAWaitWithTheFishInReachIsGood()
        {
            Assert.Equal(Jig.Judged.Good, Jig.Judge(usedThisWait: false, inReach: true));
            Assert.Equal(Jig.Judged.OffCue, Jig.Judge(usedThisWait: false, inReach: false));
            Assert.Equal(Jig.Judged.OffCue, Jig.Judge(usedThisWait: true, inReach: true));
            Assert.Equal(Jig.Judged.OffCue, Jig.Judge(usedThisWait: true, inReach: false));
        }

        [Fact]
        public void TheWaitingFishNosesInNowAndThenSoEveryWaitHasItsMoment()
        {
            // The picked fish hovers 2 to 4 s; within the shortest it noses in at least once, well before it may bite.
            Assert.False(Jig.Nosing(0f, 2f));
            var nosed = false;
            for (var t = 0f; t < 2f; t += 0.05f) nosed |= Jig.Nosing(t, 2f - t);
            Assert.True(nosed);
            // Not in the last moment before it goes for the bait, which the bite's own approach takes over.
            Assert.False(Jig.Nosing(1.0f, Jig.NoseLeaveBefore - 0.01f));
            // And not without pause: there are times it is out too.
            var outToo = false;
            for (var t = Jig.FirstNose; t < 6f; t += 0.05f) outToo |= !Jig.Nosing(t, 10f);
            Assert.True(outToo);
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
            // A picked fish hovers 2 to 4 s, so one jig never makes it bite at once.
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
            // Held up already, it dips as a heave's does.
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
