using System;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A hooked fish shows its next struggle coming. Through a rest the float lies still while there is
    /// time; in the last moments before the struggle it grows restless, trembling toward the fish more and
    /// more, and the line goes taut and quivers a moment before the fish bolts. A faster or starred fish
    /// gives less of that last moment. Through the struggle the line stays taut and quivers by the strain
    /// on it, so a line close to snapping shakes hard. The server switches the warning on or off for
    /// everyone; a difficulty level leaves it as set.
    /// </summary>
    public class StruggleWarningTests
    {
        private const float PerchSpeed = 3f;
        private const float TunaSpeed = 10f;
        private const float AnglerfishSpeed = 2f;

        [Fact]
        public void AStillFloatPromisesTimeBeforeTheNextStruggle()
        {
            Assert.Equal(1.5f, StruggleWarning.RestlessFrom, 3);
            Assert.Equal(0f, StruggleWarning.Restlessness(StruggleWarning.RestlessFrom));
            Assert.Equal(0f, StruggleWarning.Restlessness(4f));
            Assert.True(StruggleWarning.Restlessness(StruggleWarning.RestlessFrom - 0.1f) > 0f);
        }

        [Fact]
        public void TheFloatGrowsRestlessAsTheStruggleNears()
        {
            var before = 0f;
            for (var until = 1.4f; until >= 0f; until -= 0.1f)
            {
                var now = StruggleWarning.Restlessness(until);
                Assert.True(now > before, "at " + until + " s " + now + " after " + before);
                before = now;
            }
            Assert.Equal(1f, StruggleWarning.Restlessness(0f), 3);
            Assert.Equal(1f, StruggleWarning.Restlessness(-0.2f), 3);
        }

        [Fact]
        public void WithNoStruggleKnownTheFloatStaysStill()
        {
            Assert.Equal(0f, StruggleWarning.Restlessness(float.PositiveInfinity));
            Assert.Equal(0f, StruggleWarning.Restlessness(float.NaN));
        }

        [Fact]
        public void TheLineGoesTautAMomentBeforeTheStruggle()
        {
            var taut = StruggleWarning.TautSeconds(PerchSpeed, 1, FishingRules.Default);
            Assert.Equal(0.6f, taut, 3);
            Assert.False(StruggleWarning.Taut(false, taut + 0.1f, taut));
            Assert.True(StruggleWarning.Taut(false, taut - 0.1f, taut));
            Assert.False(StruggleWarning.Taut(false, float.PositiveInfinity, taut));
            Assert.False(StruggleWarning.Taut(false, float.NaN, taut));
            // The float is restless before the line goes taut, so the taut line is the last word.
            Assert.True(taut < StruggleWarning.RestlessFrom);
        }

        [Fact]
        public void TheLineStaysTautThroughTheStruggle()
        {
            Assert.True(StruggleWarning.Taut(true, float.PositiveInfinity, 0.6f));
            Assert.True(StruggleWarning.Taut(true, 3f, 0.6f));
        }

        [Fact]
        public void AFasterFishWarnsLaterAndASlowerOneSooner()
        {
            var rules = FishingRules.Default;
            var tuna = StruggleWarning.TautSeconds(TunaSpeed, 1, rules);
            var perch = StruggleWarning.TautSeconds(PerchSpeed, 1, rules);
            var anglerfish = StruggleWarning.TautSeconds(AnglerfishSpeed, 1, rules);
            Assert.True(tuna < perch && perch < anglerfish, "Tuna " + tuna + ", Perch " + perch + ", Anglerfish " + anglerfish);
            Assert.Equal(0.6f / Tension.Pull(TunaSpeed, rules), tuna, 3);
        }

        [Fact]
        public void AStarredFishWarnsLaterStillButNeverTooLateToSee()
        {
            var rules = FishingRules.Default;
            var plain = StruggleWarning.TautSeconds(PerchSpeed, 1, rules);
            var twoStars = StruggleWarning.TautSeconds(PerchSpeed, 3, rules);
            Assert.True(twoStars < plain, "two stars " + twoStars + " against plain " + plain);
            Assert.Equal(0.6f * 0.8f, twoStars, 3);
            Assert.True(StruggleWarning.TautSeconds(TunaSpeed, 5, rules) >= StruggleWarning.ShortestTaut);
            Assert.True(StruggleWarning.TautSeconds(TunaSpeed, 99, rules) >= StruggleWarning.ShortestTaut);
        }

        [Fact]
        public void WithPullBySpeedOffEveryFishWarnsAsAPerch()
        {
            var rules = FishingRules.Default.With(pullBySpeed: false);
            Assert.Equal(StruggleWarning.TautSeconds(PerchSpeed, 1, rules), StruggleWarning.TautSeconds(TunaSpeed, 1, rules), 3);
        }

        [Fact]
        public void TheTautLineQuiversByTheStrainOnIt()
        {
            Assert.Equal(0f, StruggleWarning.Quiver(struggling: false, taut: false, strain: 0f));
            var warned = StruggleWarning.Quiver(struggling: false, taut: true, strain: 0f);
            Assert.True(warned > 0f && warned < 0.5f, "warned " + warned);
            Assert.Equal(warned, StruggleWarning.Quiver(struggling: true, taut: true, strain: 0f), 3);
            Assert.Equal(1f, StruggleWarning.Quiver(struggling: true, taut: true, strain: 1f), 3);
            Assert.Equal(1f, StruggleWarning.Quiver(struggling: true, taut: true, strain: 5f), 3);
            var before = warned;
            for (var strain = 0.1f; strain <= 1f; strain += 0.1f)
            {
                var now = StruggleWarning.Quiver(true, true, strain);
                Assert.True(now > before, "at strain " + strain);
                before = now;
            }
        }

        [Fact]
        public void TheQuiverHoldsBothEndsOfTheLineAndStaysSmall()
        {
            for (var t = 0f; t < 1f; t += 0.013f)
            {
                Assert.Equal(0f, StruggleWarning.QuiverOffset(1f, 0f, t), 4);
                Assert.Equal(0f, StruggleWarning.QuiverOffset(1f, 1f, t), 4);
                Assert.True(Math.Abs(StruggleWarning.QuiverOffset(1f, 0.5f, t)) <= StruggleWarning.MaxQuiverMetres + 0.0001f);
            }
            var biggest = 0f;
            for (var t = 0f; t < 1f; t += 0.005f) biggest = Math.Max(biggest, Math.Abs(StruggleWarning.QuiverOffset(1f, 0.5f, t)));
            Assert.True(biggest > StruggleWarning.MaxQuiverMetres * 0.9f, "the middle swings " + biggest);
            Assert.Equal(0f, StruggleWarning.QuiverOffset(0f, 0.5f, 0.3f), 4);
        }

        [Fact]
        public void TheWarningIsOnUnlessTheServerSwitchesItOff()
        {
            Assert.True(FishingRules.Default.StruggleWarning);
            Assert.False(FishingRules.Default.With(struggleWarning: false).StruggleWarning);
        }

        [Fact]
        public void ADifficultyLevelLeavesTheWarningAsTheServerSetIt()
        {
            var settings = FishingRules.Default.With(struggleWarning: false);
            foreach (var level in new[] { Difficulty.VeryEasy, Difficulty.Normal, Difficulty.VeryHard })
                Assert.False(DifficultyLevels.Rules(level, settings).StruggleWarning);
        }
    }
}
