using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The server decides how strongly the signs on the water show for everyone, from half as strong
    /// to twice as strong as made, with wider lines when stronger, for a screen or a water where they
    /// read faint. Calm water still reads clearest at any contrast, and a difficulty level leaves the
    /// contrast as the server set it.
    /// </summary>
    public class SignContrastTests
    {
        [Fact]
        public void SignsShowAsMadeUnlessTheServerSaysOtherwise()
        {
            Assert.Equal(1f, FishingRules.Default.SignContrast, 3);
            Assert.Equal(0.4f, SignLight.Shown(0.4f, 1f), 3);
            Assert.Equal(1f, SignLight.LineWidth(1f), 3);
        }

        [Fact]
        public void AStrongerContrastShowsSignsStrongerAndTheirLinesWider()
        {
            Assert.Equal(0.8f, SignLight.Shown(0.4f, 2f), 3);
            Assert.Equal(1f, SignLight.Shown(0.7f, 2f), 3);
            Assert.Equal(1.5f, SignLight.LineWidth(2f), 3);
            Assert.True(SignLight.LineWidth(1.5f) > 1f && SignLight.LineWidth(1.5f) < 1.5f);
        }

        [Fact]
        public void AWeakerContrastShowsSignsFainterOnLinesAsWide()
        {
            Assert.Equal(0.2f, SignLight.Shown(0.4f, 0.5f), 3);
            Assert.Equal(1f, SignLight.LineWidth(0.5f), 3);
        }

        [Fact]
        public void ContrastStaysBetweenHalfAndTwice()
        {
            Assert.Equal(SignLight.Shown(0.3f, 0.5f), SignLight.Shown(0.3f, 0f), 3);
            Assert.Equal(SignLight.Shown(0.3f, 2f), SignLight.Shown(0.3f, 9f), 3);
            Assert.Equal(1.5f, SignLight.LineWidth(9f), 3);
            Assert.Equal(0.5f, SignLight.MinContrast, 3);
            Assert.Equal(2f, SignLight.MaxContrast, 3);
        }

        [Fact]
        public void CalmWaterStillReadsClearestAtAnyContrast()
        {
            foreach (var contrast in new[] { 0.5f, 1f, 2f })
            {
                var calm = SignLight.Shown(0.6f * WaterMood.Clarity(0f), contrast);
                var rough = SignLight.Shown(0.6f * WaterMood.Clarity(1f), contrast);
                Assert.True(calm > rough, "at " + contrast + " calm " + calm + " against rough " + rough);
            }
        }

        [Fact]
        public void ADifficultyLevelLeavesTheContrastAsTheServerSetIt()
        {
            var settings = FishingRules.Default.With(signContrast: 1.6f);
            foreach (var level in new[] { Difficulty.VeryEasy, Difficulty.Normal, Difficulty.VeryHard })
                Assert.Equal(1.6f, DifficultyLevels.Rules(level, settings).SignContrast, 3);
        }
    }
}
