using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The signs on the water are lit as the world around them is: by the game's own sun or moon
    /// and ambient light at the hour and in the weather, warm at dusk, dull in rain, dim and cool at
    /// night, though never so dark they cannot be read. Each line of a sign is a bright crest with a
    /// darker trough under it, the crest brightest on the side facing the light; far signs fade into
    /// the game's fog as the world does, and a ring or wake thins as it spreads.
    /// </summary>
    public class SignLightTests
    {
        private static readonly Shade Noon = new Shade(1f, 0.96f, 0.88f);
        private static readonly Shade Dusk = new Shade(1f, 0.52f, 0.28f);
        private static readonly Shade DayAir = new Shade(0.45f, 0.5f, 0.56f);
        private static readonly Shade NightAir = new Shade(0.07f, 0.09f, 0.14f);

        [Fact]
        public void TheCrestTakesTheColourOfTheLightAtTheHour()
        {
            var noon = SignLight.Crest(Noon, intensity: 1.2f, sunUp: 0.9f, ambient: DayAir);
            var dusk = SignLight.Crest(Dusk, intensity: 1.2f, sunUp: 0.2f, ambient: DayAir);
            Assert.True(dusk.R - dusk.B > noon.R - noon.B, "dusk " + dusk + " is no warmer than noon " + noon);
            Assert.True(noon.Luminance > dusk.Luminance);
        }

        [Fact]
        public void ALowerSunLightsTheCrestLess()
        {
            var high = SignLight.Crest(Noon, 1.2f, 0.9f, DayAir);
            var low = SignLight.Crest(Noon, 1.2f, 0.2f, DayAir);
            var under = SignLight.Crest(Noon, 1.2f, -0.5f, DayAir);
            Assert.True(high.Luminance > low.Luminance && low.Luminance > under.Luminance);
            Assert.Equal(SignLight.Crest(Noon, 0f, 0.9f, DayAir), under);
        }

        private static readonly Shade DawnSun = new Shade(1f, 0.698f, 0.437f);
        private static readonly Shade DawnAir = new Shade(0.385f, 0.423f, 0.544f);
        private static readonly Shade DawnSky = new Shade(0.62f, 0.5f, 0.42f);

        [Fact]
        public void AtDawnTheCrestStaysBrighterThanTheSkyTheWaterReflects()
        {
            // A low, weak sun lights foam to about the water's own look; the crest must still read as foam on it.
            var crest = SignLight.Crest(DawnSun, intensity: 0.5f, sunUp: 0.1f, ambient: DawnAir, sky: DawnSky);
            Assert.True(crest.Luminance >= DawnSky.Luminance * SignLight.SkyMargin - 0.001f, "crest " + crest + " against sky " + DawnSky);
            Assert.True(crest.Luminance > SignLight.Crest(DawnSun, 0.5f, 0.1f, DawnAir).Luminance);
        }

        [Fact]
        public void BrightenedForTheSkyTheCrestKeepsTheHoursTint()
        {
            var lit = SignLight.Crest(DawnSun, 0.5f, 0.1f, DawnAir);
            var crest = SignLight.Crest(DawnSun, 0.5f, 0.1f, DawnAir, DawnSky);
            Assert.Equal(lit.R / lit.B, crest.R / crest.B, 2);
            Assert.Equal(lit.G / lit.B, crest.G / crest.B, 2);
        }

        [Fact]
        public void WhereFoamIsAlreadyBrighterThanTheSkyTheSkyChangesNothing()
        {
            var sky = new Shade(0.66f, 0.76f, 0.84f);
            Assert.Equal(SignLight.Crest(Noon, 1.2f, 0.9f, DayAir), SignLight.Crest(Noon, 1.2f, 0.9f, DayAir, sky));
            var night = new Shade(0.22f, 0.28f, 0.44f);
            var nightSky = new Shade(0.03f, 0.04f, 0.07f);
            Assert.Equal(SignLight.Crest(night, 0f, 0.5f, NightAir), SignLight.Crest(night, 0f, 0.5f, NightAir, nightSky));
        }

        [Fact]
        public void ABrightSkyNeverPushesTheCrestPastWhite()
        {
            var crest = SignLight.Crest(DawnSun, 0.5f, 0.1f, DawnAir, new Shade(1f, 1f, 1f));
            Assert.True(crest.R <= 1f && crest.G <= 1f && crest.B <= 1f);
        }

        [Fact]
        public void TheCrestIsNeverBrighterThanWhite()
        {
            var c = SignLight.Crest(new Shade(2f, 2f, 2f), 3f, 1f, new Shade(1f, 1f, 1f));
            Assert.True(c.R <= 1f && c.G <= 1f && c.B <= 1f);
        }

        [Fact]
        public void AtNightTheCrestStaysReadable()
        {
            var night = SignLight.Crest(new Shade(0.22f, 0.28f, 0.44f), intensity: 0f, sunUp: 0.5f, ambient: NightAir);
            Assert.True(night.Luminance >= SignLight.MinLight - 0.001f, "night crest " + night);
            // Kept to the night's own hue: still cool, not grey.
            Assert.True(night.B > night.R);
            Assert.True(night.Luminance < SignLight.Crest(Noon, 1.2f, 0.9f, DayAir).Luminance);
        }

        [Fact]
        public void ATroughIsDarkerThanItsCrestAndTakesTheAirsHue()
        {
            var crest = SignLight.Crest(Noon, 1.2f, 0.9f, DayAir);
            var trough = SignLight.Trough(DayAir);
            // Darker than the air's own light on the water, so it reads as a dip, not a glow.
            Assert.True(trough.Luminance < DayAir.Luminance * 0.5f, "trough " + trough);
            Assert.True(trough.Luminance < crest.Luminance * 0.5f);
            Assert.True(trough.B >= trough.R);
            Assert.True(SignLight.Trough(NightAir).Luminance < trough.Luminance);
        }

        [Fact]
        public void TheSideFacingTheLightShinesMost()
        {
            Assert.Equal(1f, SignLight.SunSide(1f), 3);
            Assert.True(SignLight.SunSide(0f) < SignLight.SunSide(0.5f));
            Assert.Equal(SignLight.SunSide(0f), SignLight.SunSide(-1f), 3);
            Assert.InRange(SignLight.SunSide(-1f), 0.4f, 0.7f);
        }

        [Theory]
        [InlineData("Off", 0f)]
        [InlineData("Exponential", 0.3297f)]
        [InlineData("ExponentialSquared", 0.1479f)]
        [InlineData("Linear", 0.25f)]
        public void FarSignsSinkIntoTheGamesFogByItsOwnRule(string mode, float share)
        {
            // Density 0.02 at 20 m, linear fog from 10 to 50 m, as Unity counts each.
            Assert.Equal(share, SignLight.FogShare((SignFog)System.Enum.Parse(typeof(SignFog), mode), 0.02f, 10f, 50f, 20f), 3);
        }

        [Fact]
        public void FogThickensWithDistanceAndNeverPassesWhole()
        {
            Assert.True(SignLight.FogShare(SignFog.ExponentialSquared, 0.02f, 0f, 0f, 40f) > SignLight.FogShare(SignFog.ExponentialSquared, 0.02f, 0f, 0f, 10f));
            Assert.InRange(SignLight.FogShare(SignFog.Exponential, 1f, 0f, 0f, 1000f), 0.99f, 1f);
            Assert.Equal(1f, SignLight.FogShare(SignFog.Linear, 0f, 10f, 50f, 80f), 3);
            Assert.Equal(0f, SignLight.FogShare(SignFog.Linear, 0f, 10f, 50f, 5f), 3);
            Assert.Equal(0f, SignLight.FogShare(SignFog.Linear, 0f, 50f, 50f, 60f), 3);
        }

        [Fact]
        public void ThroughFogASignTakesTheFogsColour()
        {
            var fog = new Shade(0.5f, 0.53f, 0.56f);
            Assert.Equal(fog, SignLight.Through(new Shade(1f, 1f, 1f), fog, 1f));
            Assert.Equal(new Shade(1f, 1f, 1f), SignLight.Through(new Shade(1f, 1f, 1f), fog, 0f));
        }

        [Fact]
        public void ARingOrWakeThinsAsItSpreads()
        {
            Assert.Equal(1f, SignLight.Taper(0f), 3);
            Assert.True(SignLight.Taper(1f) < SignLight.Taper(0.5f));
            Assert.InRange(SignLight.Taper(1f), 0.3f, 0.5f);
        }
    }
}
