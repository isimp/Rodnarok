using System;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Each fish pulls the line by its own speed in the game: a fast one strains the line sooner and
    /// takes more line while it is eased off, a slow one later and less. The snap time and the run
    /// speed set the pull of a Perch, which fights as it always did, and every other fish is
    /// scaled from it by the square root of its speed against the Perch's, so a Tuna is the
    /// hardest pull and still one a player can land.
    /// </summary>
    public class FishPullTests
    {
        private static readonly FishingRules Rules = FishingRules.Default;

        private const float Perch = 3f;
        private const float Pike = 5f;
        private const float Tuna = 10f;
        private const float Anglerfish = 2f;

        private static float HeldUntilSnapped(float fishSpeed, FishingRules rules = null)
        {
            float strain = 0f, held = 0f;
            while (!Tension.Snaps(strain) && held < 20f)
            {
                strain = Tension.Step(strain, struggling: true, holding: true, 0.01f, fishSpeed, rules ?? Rules);
                held += 0.01f;
            }
            return held;
        }

        [Fact]
        public void APerchStrainsAndTakesLineExactlyAsEveryFishDidBefore()
        {
            Assert.Equal(Rules.SnapSeconds, Tension.SnapSeconds(Perch, Rules), 3);
            Assert.Equal(Rules.RunSpeed, Tension.RunSpeed(Perch, Rules), 3);
            Assert.Equal(1f, HeldUntilSnapped(Perch), 1);
        }

        [Fact]
        public void AFasterFishSnapsAHeldLineSoonerAndTakesMoreLineEasedOff()
        {
            Assert.True(HeldUntilSnapped(Pike) < HeldUntilSnapped(Perch));
            Assert.True(HeldUntilSnapped(Tuna) < HeldUntilSnapped(Pike));
            Assert.True(Tension.LineOut(0f, struggling: true, holding: false, seconds: 1f, Pike, Rules) > Rules.RunSpeed);
            Assert.True(Tension.LineOut(0f, true, false, 1f, Tuna, Rules) > Tension.LineOut(0f, true, false, 1f, Pike, Rules));
        }

        [Fact]
        public void ASlowerFishSnapsAHeldLineLaterAndTakesLessLineEasedOff()
        {
            Assert.True(HeldUntilSnapped(Anglerfish) > HeldUntilSnapped(Perch));
            Assert.True(Tension.LineOut(0f, struggling: true, holding: false, seconds: 1f, Anglerfish, Rules) < Rules.RunSpeed);
        }

        [Theory]
        [InlineData("Fish1", 1f, 2f)]
        [InlineData("Fish2", 0.775f, 2.582f)]
        [InlineData("Fish3", 0.548f, 3.651f)]
        [InlineData("Fish4_cave", 1.2247f, 1.633f)]
        [InlineData("Fish5", 0.866f, 2.309f)]
        [InlineData("Fish9", 1.2247f, 1.633f)]
        [InlineData("Fish10", 0.866f, 2.309f)]
        [InlineData("Fish11", 0.926f, 2.160f)]
        public void EachFishPullsByTheSquareRootOfItsGameSpeedAgainstThePerchs(string fish, float snapSeconds, float runSpeed)
        {
            var speed = GameFish.Speeds[fish];
            Assert.Equal(snapSeconds, Tension.SnapSeconds(speed, Rules), 2);
            Assert.Equal(runSpeed, Tension.RunSpeed(speed, Rules), 2);
        }

        [Fact]
        public void TheSettingsScaleEveryFishFromThePerchsPull()
        {
            var rules = Rules.With(snapSeconds: 2f, runSpeed: 4f);
            Assert.Equal(2f, Tension.SnapSeconds(Perch, rules), 3);
            Assert.Equal(4f, Tension.RunSpeed(Perch, rules), 3);
            Assert.Equal(Tension.SnapSeconds(Tuna, Rules) * 2f, Tension.SnapSeconds(Tuna, rules), 3);
            Assert.Equal(Tension.RunSpeed(Tuna, Rules) * 2f, Tension.RunSpeed(Tuna, rules), 3);
        }

        [Theory]
        [InlineData(0f, 3f)]
        [InlineData(0.5f, 3f)]
        [InlineData(0.9f, 10f)]
        [InlineData(0.3f, 2f)]
        public void AnEasedOffFishSwimsExactlyAsFastAsTheLineGoesOut(float strain, float speed)
        {
            // The fish's run and the line paid out are one speed, so the float is never dragged
            // beyond the line to where the game breaks it.
            Assert.Equal(Tension.LineOut(strain, struggling: true, holding: false, seconds: 1f, speed, Rules), Tension.Running(strain, speed, Rules), 4);
            Assert.Equal(Tension.RunSpeed(speed, Rules) * (1f - strain), Tension.Running(strain, speed, Rules), 4);
        }

        [Fact]
        public void WithPullBySpeedOffEveryFishPullsLikeAPerch()
        {
            var off = Rules.With(pullBySpeed: false);
            Assert.True(Rules.PullBySpeed);
            Assert.Equal(off.SnapSeconds, Tension.SnapSeconds(Tuna, off), 3);
            Assert.Equal(off.RunSpeed, Tension.RunSpeed(Anglerfish, off), 3);
            Assert.Equal(1f, HeldUntilSnapped(Tuna, off), 1);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void AFishOfUnknownSpeedPullsLikeAPerch(float speed)
        {
            Assert.Equal(Rules.SnapSeconds, Tension.SnapSeconds(speed, Rules), 3);
            Assert.Equal(Rules.RunSpeed, Tension.RunSpeed(speed, Rules), 3);
        }

        [Theory]
        [InlineData("Fish1")]
        [InlineData("Fish2")]
        [InlineData("Fish3")]
        [InlineData("Fish4_cave")]
        [InlineData("Fish5")]
        [InlineData("Fish6")]
        [InlineData("Fish7")]
        [InlineData("Fish8")]
        [InlineData("Fish9")]
        [InlineData("Fish10")]
        [InlineData("Fish11")]
        [InlineData("Fish12")]
        public void EveryOneStarFishCanBeLandedAtFishingZeroByTappingThroughItsStruggles(string fish)
        {
            // One average fight round with the game's struggle and rest times: the player taps to keep
            // the strain between a fifth and two fifths while it struggles, and reels at the Fishing 0
            // speed while it rests. A round must win back more line than the struggle took.
            var speed = GameFish.Speeds[fish];
            var times = GameFish.Struggles[fish];
            var struggle = (times.Min + times.Max + times.PerStar) / 2f;
            var rest = (times.RestMin + times.RestMax) / 2f;

            float strain = 0f, lineOut = 0f;
            var holding = true;
            for (var t = 0f; t < struggle; t += 0.01f)
            {
                holding = strain >= 0.4f ? false : strain <= 0.2f || holding;
                strain = Tension.Step(strain, struggling: true, holding, 0.01f, speed, Rules);
                Assert.False(Tension.Snaps(strain), fish + " snapped while tapped");
                lineOut += Tension.LineOut(strain, struggling: true, holding, 0.01f, speed, Rules);
            }
            var reeledIn = Rules.ReelSpeed * rest;
            Assert.True(reeledIn > lineOut, fish + ": reeled " + reeledIn.ToString("0.0") + " m, took " + lineOut.ToString("0.0") + " m");
        }

        [Fact]
        public void ATunaEasedRightOffThroughEveryStruggleOutrunsAFishingZeroReel()
        {
            // The hardest pull asks for the rhythm: letting go through its struggles loses more line than
            // a beginner's reel wins back while it rests.
            var times = GameFish.Struggles["Fish3"];
            var struggle = (times.Min + times.Max + times.PerStar) / 2f;
            var rest = (times.RestMin + times.RestMax) / 2f;
            var lineOut = Tension.LineOut(0f, struggling: true, holding: false, struggle, Tuna, Rules);
            Assert.True(lineOut > Rules.ReelSpeed * rest, "took " + lineOut.ToString("0.0") + " m");
            Assert.True(Math.Abs(Tension.Running(0f, Tuna, Rules) - Tension.RunSpeed(Tuna, Rules)) < 0.001f);
        }
    }
}
