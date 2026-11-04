using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float: a short twitch toward the rod. A fish
    /// waiting at the float, the one picked to bite as it hovers or a shy school as it hesitates, comes
    /// sooner for each jig. One jig moves the float too gently to frighten a wary school; two in quick
    /// succession move it as fast as a dragged float, and the school flinches by its own rule. The
    /// server switches it on or off; a difficulty level leaves it as set.
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
        }

        [Fact]
        public void OneJigIsTooGentleForAWarySchoolsOwnSpeedRule()
        {
            // A wary school measures its float's speed over each half second (Schools.Startled).
            Assert.True(Jig.Metres / 0.5f < Wary.FloatSpeed, "one jig " + Jig.Metres / 0.5f);
        }

        [Fact]
        public void JiggingAgainTooSoonStartlesAWarySchoolAndDrawsNoFish()
        {
            Assert.False(Jig.TooSoon(float.PositiveInfinity));
            Assert.False(Jig.TooSoon(1f));
            Assert.False(Jig.TooSoon(Jig.TooSoonSeconds));
            Assert.True(Jig.TooSoon(Jig.TooSoonSeconds - 0.01f));
            Assert.True(Jig.TooSoon(0.1f));
            // A twitch is over before the next may come without startling.
            Assert.True(Jig.Seconds < Jig.TooSoonSeconds);
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
        public void APickedFishHoveringByTheFloatMayBiteSoonerForAJig()
        {
            Assert.Equal(10f + 3f - Jig.Cut, Jig.SoonerFree(10f + 3f, 10f), 3);
            // Never before now: a jig brings the bite to now at the soonest.
            Assert.Equal(10f, Jig.SoonerFree(10.5f, 10f), 3);
            // No fish picked yet, or one already free to bite: nothing changes.
            Assert.Equal(float.MaxValue, Jig.SoonerFree(float.MaxValue, 10f));
            Assert.Equal(9f, Jig.SoonerFree(9f, 10f), 3);
        }

        [Fact]
        public void AHesitatingSchoolMakesUpItsMindSoonerForAJig()
        {
            Assert.Equal(20f - Jig.Cut, Jig.SoonerReady(20f), 3);
            // A school not yet ready to bite has no hesitation to cut.
            Assert.Equal(-1f, Jig.SoonerReady(-1f), 3);
        }

        [Fact]
        public void AJigCutsLessThanAPickedFishsShortestHover()
        {
            // A picked fish hovers 2 to 4 s before it may bite (Schools.Advance), so one jig never makes it bite at once.
            Assert.True(Jig.Cut < 2f && Jig.Cut >= 1f, "cut " + Jig.Cut);
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
