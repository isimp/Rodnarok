using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>The bait on the hook decides which fish comes, and every fish of the game comes to some bait.</summary>
    public class BaitTests
    {
        [Fact]
        public void EveryFishOfTheGameComesToSomeBait()
        {
            var table = GameFish.Table();
            var reachable = table.Baits.SelectMany(table.FishFor).Distinct().ToList();
            Assert.Equal(GameFish.All.Select(f => f.Prefab).OrderBy(p => p, System.StringComparer.Ordinal), reachable.OrderBy(p => p, System.StringComparer.Ordinal));
        }

        [Fact]
        public void TheCaveFishComesToColdBaitThoughNoWaterOutsideCavesHoldsIt()
        {
            Assert.Equal(new[] { "Fish4_cave" }, GameFish.Table().FishFor("FishingBaitCave"));
        }

        [Theory]
        [InlineData("FishingBaitForest", "Fish5")]
        [InlineData("FishingBaitSwamp", "Fish6")]
        [InlineData("FishingBaitPlains", "Fish7")]
        [InlineData("FishingBaitAshlands", "Fish11")]
        [InlineData("FishingBaitDeepNorth", "Fish10")]
        public void ABaitBringsOnlyItsOwnFish(string bait, string fish)
        {
            var table = GameFish.Table();
            Assert.Equal(new[] { fish }, table.FishFor(bait));
            for (var roll = 0.0; roll < 1.0; roll += 0.05) Assert.Equal(fish, table.Choose(bait, roll));
        }

        [Theory]
        [InlineData("FishingBait", "Fish1", "Fish2")]
        [InlineData("FishingBaitOcean", "Fish3", "Fish8")]
        [InlineData("FishingBaitMistlands", "Fish9", "Fish12")]
        public void ABaitTwoFishTakeBringsEachOfThemInEqualShare(string bait, string one, string other)
        {
            var table = GameFish.Table();
            var comes = Enumerable.Range(0, 100).Select(i => table.Choose(bait, (i + 0.5) / 100)).ToList();
            Assert.Equal(50, comes.Count(f => f == one));
            Assert.Equal(50, comes.Count(f => f == other));
        }

        [Theory]
        [InlineData("ArrowWood")]
        [InlineData("")]
        [InlineData(null)]
        public void WhatNoFishTakesBringsNoFish(string bait)
        {
            var table = GameFish.Table();
            Assert.Empty(table.FishFor(bait));
            Assert.Null(table.Choose(bait, 0.5));
        }

        [Fact]
        public void ARollOfOneStillBringsAFish()
        {
            Assert.Equal("Fish2", GameFish.Table().Choose("FishingBait", 1.0));
        }
    }
}
