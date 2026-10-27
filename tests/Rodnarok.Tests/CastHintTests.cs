using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// With cast hints on, a cast says in a short line which of the bait's fish can come to the water
    /// here, and which would need bigger or deeper water; a fish of the world's own called to the
    /// float is named instead.
    /// </summary>
    public class CastHintTests
    {
        [Fact]
        public void EveryFishThatFitsTheWaterIsNamed()
        {
            Assert.Equal("Here: Perch, Pike.", CastHint.Text(new[] { "Perch", "Pike" }, new string[0], null));
        }

        [Fact]
        public void AFishTheWaterIsTooLittleForIsNamedApart()
        {
            Assert.Equal("Here: Coral Cod. Needs bigger or deeper water: Tuna.", CastHint.Text(new[] { "Coral Cod" }, new[] { "Tuna" }, null));
        }

        [Fact]
        public void WhereNoneFitsTheLineSaysSo()
        {
            Assert.Equal("Here: none. Needs bigger or deeper water: Perch, Pike.", CastHint.Text(new string[0], new[] { "Perch", "Pike" }, null));
        }

        [Fact]
        public void ACalledFishOfTheWorldsOwnIsNamedInstead()
        {
            Assert.Equal("A Perch nearby is coming to the float.", CastHint.Text(new[] { "Perch", "Pike" }, new string[0], "Perch"));
        }

        [Fact]
        public void CastHintsAreOffByDefault()
        {
            Assert.False(CastHint.OnByDefault);
        }
    }
}
