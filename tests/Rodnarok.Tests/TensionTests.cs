using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Line tension, Rodnarök's own rule on top of the game's fight: holding the line through a
    /// struggle strains it, and held too long it snaps. Easing off lets the strain go, but a
    /// struggling fish takes line out meanwhile. A resting fish never strains the line. The skill
    /// is the rhythm: reel while it rests, ease off in time while it struggles.
    /// </summary>
    public class TensionTests
    {
        private static readonly FishingRules Rules = FishingRules.Default;

        private static float Hold(float strain, bool struggling, bool holding, float seconds, FishingRules rules = null)
        {
            for (var t = 0f; t < seconds - 0.0001f; t += 0.02f) strain = Tension.Step(strain, struggling, holding, 0.02f, rules ?? Rules);
            return strain;
        }

        [Fact]
        public void HoldingThroughAStruggleSnapsTheLineAfterOneSecond()
        {
            Assert.Equal(1f, Rules.SnapSeconds);
            Assert.False(Tension.Snaps(Hold(0f, struggling: true, holding: true, seconds: 0.9f)));
            Assert.True(Tension.Snaps(Hold(0f, struggling: true, holding: true, seconds: 1.02f)));
        }

        [Fact]
        public void HalfTheSnapTimeHeldStrainsTheLineHalfway()
        {
            Assert.Equal(0.5f, Hold(0f, struggling: true, holding: true, seconds: Rules.SnapSeconds / 2f), 2);
        }

        [Fact]
        public void EasingOffLetsTheFullStrainGoWithinASecond()
        {
            Assert.Equal(1f, Rules.EaseSeconds);
            Assert.Equal(0.5f, Hold(1f, struggling: true, holding: false, seconds: 0.5f), 2);
            Assert.Equal(0f, Hold(0.99f, struggling: true, holding: false, seconds: 1f));
        }

        [Fact]
        public void AStrainedLineRecoversWhileTheFishRestsHeldOrNot()
        {
            Assert.Equal(0f, Hold(0.8f, struggling: false, holding: true, seconds: 1f));
            Assert.Equal(0f, Hold(0.8f, struggling: false, holding: false, seconds: 1f));
        }

        [Fact]
        public void EasingOffBetweenHoldsCarriesAStruggleLongerThanTheSnapTime()
        {
            var strain = Hold(0f, struggling: true, holding: true, seconds: Rules.SnapSeconds * 0.75f);
            strain = Hold(strain, struggling: true, holding: false, seconds: Rules.EaseSeconds * 0.4f);
            strain = Hold(strain, struggling: true, holding: true, seconds: Rules.SnapSeconds * 0.6f);
            Assert.False(Tension.Snaps(strain), "1.35 snap times held in one struggle with a short ease between");
        }

        [Fact]
        public void WhileEasedOffAStrugglingFishTakesLineOut()
        {
            Assert.Equal(Rules.RunSpeed, Tension.LineOut(struggling: true, holding: false, seconds: 1f, Rules), 3);
            Assert.Equal(2f, Rules.RunSpeed);
        }

        [Fact]
        public void HeldOrRestingTheFishTakesNoLine()
        {
            Assert.Equal(0f, Tension.LineOut(struggling: true, holding: true, seconds: 1f, Rules));
            Assert.Equal(0f, Tension.LineOut(struggling: false, holding: false, seconds: 1f, Rules));
        }

        [Fact]
        public void WithTensionOffHoldingNeverStrainsAndNoLineGoesOut()
        {
            var off = Rules.With(tension: false);
            Assert.Equal(0f, Hold(0f, struggling: true, holding: true, seconds: 10f, off));
            Assert.Equal(0f, Tension.LineOut(struggling: true, holding: false, seconds: 1f, off));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        public void ASnapTimeOfZeroOrLessSnapsAtOnceRatherThanNever(float snap)
        {
            var rules = Rules.With(snapSeconds: snap);
            Assert.True(Tension.Snaps(Tension.Step(0f, struggling: true, holding: true, seconds: 0.02f, rules)));
        }

        [Fact]
        public void AnEaseTimeOfZeroLetsTheStrainGoAtOnce()
        {
            var rules = Rules.With(easeSeconds: 0f);
            Assert.Equal(0f, Tension.Step(0.9f, struggling: true, holding: false, seconds: 0.02f, rules));
        }

        [Fact]
        public void TheStrainStaysBetweenNoneAndSnapped()
        {
            Assert.Equal(0f, Tension.Step(0f, struggling: false, holding: false, seconds: 5f, Rules));
            Assert.Equal(1f, Tension.Step(0.9f, struggling: true, holding: true, seconds: 5f, Rules));
            Assert.Equal(0f, Tension.Step(float.NaN, struggling: false, holding: false, seconds: 0.02f, Rules));
        }
    }
}
