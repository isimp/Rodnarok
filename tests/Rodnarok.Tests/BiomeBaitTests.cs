using System.Linq;
using Xunit;
using static Rodnarok.Tests.GameFish;

namespace Rodnarok.Tests
{
    /// <summary>
    /// With the setting on, the game's plain Fishing Bait also brings the fish that live where the
    /// float lands, as the world's spawn lists place them: in the Black Forest a Trollfish as well
    /// as a Perch or a Pike, at sea a Tuna, a Coral cod or a Pufferfish. Each fish that fits takes
    /// an equal share, as a bait's fish always do. A biome's own bait still brings only its fish,
    /// in any water, so it stays the way to pick one. A fish that lives in no biome of the world,
    /// as the Tetra of the Mountain caves, keeps to its own bait. With the setting off, plain bait
    /// brings what it always has.
    /// </summary>
    public class BiomeBaitTests
    {
        private const string Plain = "FishingBait";

        [Fact]
        public void InTheBlackForestPlainBaitAlsoBringsTheTrollfish()
        {
            Assert.Equal(new[] { "Fish1", "Fish2", "Fish5" }, Table().FishFor(Plain, BlackForest, local: true).OrderBy(f => f, System.StringComparer.Ordinal));
        }

        /// <summary>The Perch and the Pike take plain bait in any water, as ever; each biome adds its own fish to them.</summary>
        [Theory]
        [InlineData(Swamp, new[] { "Fish1", "Fish2", "Fish6" })]
        [InlineData(Plains, new[] { "Fish1", "Fish2", "Fish7" })]
        [InlineData(Ocean, new[] { "Fish1", "Fish2", "Fish3", "Fish8", "Fish12" })]
        [InlineData(Mistlands, new[] { "Fish1", "Fish2", "Fish9", "Fish12" })]
        [InlineData(DeepNorth, new[] { "Fish1", "Fish2", "Fish10" })]
        [InlineData(AshLands, new[] { "Fish1", "Fish2", "Fish11" })]
        [InlineData(Meadows, new[] { "Fish1", "Fish2" })]
        [InlineData(Mountain, new[] { "Fish1", "Fish2" })]
        public void PlainBaitBringsTheFishOfTheBiomeTheFloatLandsIn(int biome, string[] fish)
        {
            Assert.Equal(fish.OrderBy(f => f, System.StringComparer.Ordinal), Table().FishFor(Plain, biome, local: true).OrderBy(f => f, System.StringComparer.Ordinal));
        }

        [Fact]
        public void EachFishThatFitsTakesAnEqualShareOfTheRolls()
        {
            var table = Table();
            var counts = Enumerable.Range(0, 300).Select(i => table.Choose(Plain, (i + 0.5) / 300.0, null, BlackForest, local: true)).GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());
            Assert.Equal(new[] { "Fish1", "Fish2", "Fish5" }, counts.Keys.OrderBy(f => f));
            Assert.All(counts.Values, c => Assert.Equal(100, c));
        }

        [Theory]
        [InlineData(BlackForest)]
        [InlineData(Swamp)]
        [InlineData(Ocean)]
        public void ABiomesOwnBaitStillBringsOnlyItsFishInAnyWater(int biome)
        {
            Assert.Equal(new[] { "Fish5" }, Table().FishFor("FishingBaitForest", biome, local: true));
        }

        [Fact]
        public void TheTetraOfTheMountainCavesKeepsToItsColdBait()
        {
            var table = Table();
            Assert.DoesNotContain("Fish4_cave", table.FishFor(Plain, Mountain, local: true));
            Assert.False(table.Takes("Fish4_cave", Plain, Mountain, local: true));
            Assert.True(table.Takes("Fish4_cave", "FishingBaitCave", Mountain, local: true));
        }

        [Fact]
        public void AFishTakesPlainBaitOnlyWhereItLives()
        {
            var table = Table();
            Assert.True(table.Takes("Fish5", Plain, BlackForest, local: true));
            Assert.True(table.Takes("Fish5", Plain, BlackForest | Meadows, local: true));
            Assert.False(table.Takes("Fish5", Plain, Swamp, local: true));
            Assert.True(table.Takes("Fish5", "FishingBaitForest", Swamp, local: true));
            Assert.False(table.Takes("Fish5", "FishingBaitSwamp", BlackForest, local: true));
            Assert.True(table.Takes("Fish1", Plain, Swamp, local: true));
        }

        [Fact]
        public void WithTheSettingOffPlainBaitBringsWhatItAlwaysHas()
        {
            var table = Table();
            Assert.Equal(new[] { "Fish1", "Fish2" }, table.FishFor(Plain, BlackForest, local: false));
            Assert.False(table.Takes("Fish5", Plain, BlackForest, local: false));
        }

        [Fact]
        public void AFishTheWaterDoesNotFitIsNeverChosen()
        {
            var table = Table();
            for (var roll = 0.0; roll < 1.0; roll += 0.01)
                Assert.NotEqual("Fish5", table.Choose(Plain, roll, f => f != "Fish5", BlackForest, local: true));
        }

        [Fact]
        public void PlainBaitIsTheGamesFishingBait()
        {
            Assert.Equal(Plain, BaitTable.PlainBait);
        }

        [Fact]
        public void TheBiomesFishTakePlainBaitUnlessTheServerTurnsItOff()
        {
            Assert.True(new FishingRules().BiomeFishOnBait);
            Assert.False(new FishingRules().With(biomeFishOnBait: false).BiomeFishOnBait);
        }
    }
}
