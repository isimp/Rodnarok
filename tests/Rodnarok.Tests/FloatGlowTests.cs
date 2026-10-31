using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The float glows at night so a player can find it and see the bite in the dark: a soft light
    /// in the ring's resting yellow that fades in after sunset and out before sunrise, as the
    /// game's own night light does. At a bite it flashes white and brighter and fades back to the
    /// yellow glow over the hook window, as the ring drains. Through the fight it stays the soft
    /// glow. In daylight there is none.
    /// </summary>
    public class FloatGlowTests
    {
        private const float Window = 1.5f;
        private const float NoBite = float.PositiveInfinity;

        [Fact]
        public void ThereIsNoGlowInDaylight()
        {
            Assert.Equal(0f, FloatGlow.Darkness(0.5f));
            Assert.Equal(0f, FloatGlow.Darkness(0.3f));
            Assert.False(FloatGlow.Look(FloatGlow.Darkness(0.5f), NoBite, Window).On);
            Assert.False(FloatGlow.Look(FloatGlow.Darkness(0.5f), 0f, Window).On);
        }

        [Fact]
        public void AtMidnightTheFloatGlowsFullyInTheRingsRestingYellow()
        {
            Assert.Equal(1f, FloatGlow.Darkness(0f));
            Assert.Equal(1f, FloatGlow.Darkness(1f));
            var glow = FloatGlow.Look(1f, NoBite, Window);
            Assert.True(glow.On);
            Assert.Equal(1f, glow.R);
            Assert.Equal(RingDraw.YellowG, glow.G);
            Assert.Equal(RingDraw.YellowB, glow.B);
            Assert.Equal(FloatGlow.GlowIntensity, glow.Intensity, 3);
            Assert.Equal(FloatGlow.GlowRange, glow.Range, 3);
        }

        [Fact]
        public void TheGlowFadesInAfterSunsetAndOutBeforeSunriseAsTheGamesNightLightDoes()
        {
            Assert.Equal(0f, FloatGlow.Darkness(0.75f));
            Assert.Equal(0f, FloatGlow.Darkness(0.25f));
            var dusk = FloatGlow.Darkness(0.8f);
            var late = FloatGlow.Darkness(0.9f);
            Assert.InRange(dusk, 0.01f, 0.99f);
            Assert.True(late > dusk);
            Assert.Equal(FloatGlow.Darkness(0.2f), dusk, 4);
            Assert.Equal(System.Math.Sqrt(0.2), dusk, 3);
            Assert.True(FloatGlow.Look(dusk, NoBite, Window).Intensity < FloatGlow.Look(late, NoBite, Window).Intensity);
        }

        [Fact]
        public void ABiteFlashesTheFloatWhiteAndBrighterThanItsGlow()
        {
            var flash = FloatGlow.Look(1f, 0f, Window);
            Assert.True(flash.On);
            Assert.Equal(1f, flash.R);
            Assert.Equal(1f, flash.G);
            Assert.Equal(1f, flash.B);
            Assert.True(flash.Intensity >= FloatGlow.GlowIntensity * 2f);
            Assert.True(flash.Range > FloatGlow.GlowRange);
        }

        [Fact]
        public void TheFlashFadesBackToTheGlowOverTheHookWindowAsTheRingDrains()
        {
            var half = FloatGlow.Look(1f, Window / 2f, Window);
            var flash = FloatGlow.Look(1f, 0f, Window);
            var glow = FloatGlow.Look(1f, NoBite, Window);
            Assert.Equal((flash.Intensity + glow.Intensity) / 2f, half.Intensity, 3);
            Assert.Equal((1f + RingDraw.YellowB) / 2f, half.B, 3);
            var ring = RingDraw.For(Ring.ForBite(Window / 2f, Window));
            Assert.Equal(ring.G, half.G, 3);
            Assert.Equal(ring.B, half.B, 3);
            var after = FloatGlow.Look(1f, Window, Window);
            Assert.Equal(glow.Intensity, after.Intensity, 3);
            Assert.Equal(glow.B, after.B, 3);
            Assert.True(FloatGlow.Look(1f, 0.3f, Window).Intensity > FloatGlow.Look(1f, 0.9f, Window).Intensity);
        }

        [Theory]
        [InlineData(float.PositiveInfinity)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(10f)]
        public void WithoutABiteInTheHookWindowTheFloatShowsItsGlow(float sinceBite)
        {
            var look = FloatGlow.Look(1f, sinceBite, Window);
            Assert.Equal(FloatGlow.GlowIntensity, look.Intensity, 3);
            Assert.Equal(RingDraw.YellowB, look.B, 3);
        }

        [Fact]
        public void AtDuskTheFlashIsDimmedLikeTheGlow()
        {
            var dusk = FloatGlow.Darkness(0.8f);
            Assert.Equal(FloatGlow.Look(1f, 0f, Window).Intensity * dusk, FloatGlow.Look(dusk, 0f, Window).Intensity, 3);
            Assert.Equal(FloatGlow.Look(1f, 0f, Window).Range, FloatGlow.Look(dusk, 0f, Window).Range, 3);
        }

        [Fact]
        public void AHookWindowOfZeroShowsNoFlash()
        {
            Assert.Equal(FloatGlow.GlowIntensity, FloatGlow.Look(1f, 0f, 0f).Intensity, 3);
        }

        [Fact]
        public void TheGlowIsOnUnlessAPlayerTurnsItOff()
        {
            Assert.True(FloatGlow.OnByDefault);
        }
    }
}
