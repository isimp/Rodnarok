using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// Every fish coming to a float tells itself on the way in by a sign of its own, so a player
    /// learns to read what comes: a Perch's small rings, a Giant herring's flicker of tiny rings, a
    /// Pike's single swirl by the float, a Trollfish's heavy bulge, a Grouper's single big bubbles,
    /// a Coral cod's fizz, a Pufferfish's swelling domes, an Anglerfish's lure glow under the water,
    /// a Magmafish's steam, a Tuna's surface boil, a Northern salmon's fin and a Tetra's zigzag of
    /// glints. A fish another mod adds shows the Perch's rings. The signs come more often and
    /// stronger as the school nears the float. Only the Tuna and the Northern salmon leap on the
    /// way in, and as the game's own fish do, mostly at dawn and dusk: by the game's weight, full at
    /// a quarter and three quarters of the day, none at noon and midnight.
    /// </summary>
    public class ArrivalSignTests
    {
        [Theory]
        [InlineData("Fish1", "Rings")]
        [InlineData("Fish6", "Flicker")]
        [InlineData("Fish2", "Swirl")]
        [InlineData("Fish5", "Bulge")]
        [InlineData("Fish7", "Bloop")]
        [InlineData("Fish8", "Fizz")]
        [InlineData("Fish12", "Dome")]
        [InlineData("Fish9", "Glow")]
        [InlineData("Fish11", "Steam")]
        [InlineData("Fish3", "Boil")]
        [InlineData("Fish10", "Fin")]
        [InlineData("Fish4_cave", "Zigzag")]
        public void EachFishOfTheGameTellsItselfByItsOwnSign(string fish, string sign)
        {
            Assert.Equal(sign, ArrivalSign.Of(fish).ToString());
        }

        [Fact]
        public void NoTwoFishOfTheGameShareASign()
        {
            var signs = GameFish.All.Select(f => ArrivalSign.Of(f.Prefab)).ToList();
            Assert.Equal(signs.Count, signs.Distinct().Count());
        }

        [Theory]
        [InlineData("SomeModsFish")]
        [InlineData("")]
        [InlineData(null)]
        public void AFishAnotherModAddsShowsThePerchsRings(string fish)
        {
            Assert.Equal(ArrivalSignKind.Rings, ArrivalSign.Of(fish));
        }

        [Fact]
        public void SignsComeMoreOftenAndStrongerAsTheSchoolNears()
        {
            Assert.True(ArrivalSign.Every(0f) > ArrivalSign.Every(0.5f));
            Assert.True(ArrivalSign.Every(0.5f) > ArrivalSign.Every(1f));
            Assert.True(ArrivalSign.Strength(0f) < ArrivalSign.Strength(1f));
            Assert.InRange(ArrivalSign.Strength(0f), 0.3f, 0.6f);
            Assert.Equal(1f, ArrivalSign.Strength(1f), 3);
            Assert.Equal(ArrivalSign.Every(1f), ArrivalSign.Every(5f), 3);
            Assert.Equal(ArrivalSign.Every(0f), ArrivalSign.Every(-1f), 3);
        }

        [Theory]
        [InlineData(30f, 0f)]
        [InlineData(40f, 0f)]
        [InlineData(4f, 1f)]
        [InlineData(1f, 1f)]
        [InlineData(17f, 0.5f)]
        public void HowNearASchoolIsGoesByItsNearestFishsDistanceFromTheFloat(float metres, float nearness)
        {
            Assert.Equal(nearness, ArrivalSign.Nearness(metres), 3);
        }

        [Fact]
        public void APikeSwirlsOnlyOnceItIsCloseToTheFloat()
        {
            Assert.False(ArrivalSign.Shows(ArrivalSignKind.Swirl, 0.5f));
            Assert.True(ArrivalSign.Shows(ArrivalSignKind.Swirl, 0.85f));
            Assert.True(ArrivalSign.Shows(ArrivalSignKind.Rings, 0f));
            Assert.True(ArrivalSign.Shows(ArrivalSignKind.Glow, 0f));
        }

        [Fact]
        public void OnlyTheTunaAndTheNorthernSalmonLeapOnTheWayIn()
        {
            var leapers = GameFish.All.Where(f => ArrivalSign.Leaps(ArrivalSign.Of(f.Prefab))).Select(f => f.Prefab).OrderBy(f => f);
            Assert.Equal(new[] { "Fish10", "Fish3" }, leapers);
        }

        [Theory]
        [InlineData(0.25f, 1f)]
        [InlineData(0.75f, 1f)]
        [InlineData(0.5f, 0f)]
        [InlineData(0f, 0f)]
        [InlineData(1f, 0f)]
        [InlineData(0.375f, 0.5f)]
        [InlineData(0.125f, 0.5f)]
        public void LeapsGoByTheGamesDawnAndDuskWeight(float dayFraction, float weight)
        {
            Assert.Equal(weight, ArrivalSign.DawnDusk(dayFraction), 3);
        }

        /// <summary>At night the signs are drawn dimmer and moonlit, as much as the game's own night light weighs at the hour.</summary>
        [Theory]
        [InlineData(0.5f, 0f)]
        [InlineData(0.3f, 0f)]
        [InlineData(0f, 1f)]
        [InlineData(1f, 1f)]
        [InlineData(0.2f, 0.4472f)]
        [InlineData(0.8f, 0.4472f)]
        public void AtNightSignsDimByTheGamesNightCurve(float dayFraction, float darkness)
        {
            Assert.Equal(darkness, ArrivalSign.Darkness(dayFraction), 3);
        }
    }
}
