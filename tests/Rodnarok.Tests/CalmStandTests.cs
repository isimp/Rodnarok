using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A Serpent trophy on one of the game's item stands calms the waters around it once a player
    /// taps the use key on that stand, and stops when tapped again or the trophy is taken. Holding
    /// the key still takes the trophy and the turn key still turns it, as in the game; a boss's
    /// stone and a stand holding anything else are left alone.
    /// </summary>
    public class CalmStandTests
    {
        [Fact]
        public void ASerpentTrophySwitchedOnCalmsTheWaters()
        {
            Assert.True(CalmStand.Calms(CalmStand.Trophy, switchedOn: true));
        }

        [Fact]
        public void ASerpentTrophyPutUpCalmsNothingUntilSwitchedOn()
        {
            Assert.False(CalmStand.Calms(CalmStand.Trophy, switchedOn: false));
        }

        [Fact]
        public void AStandHoldingAnythingElseCalmsNothing()
        {
            Assert.False(CalmStand.Calms("TrophyDeer", switchedOn: true));
            Assert.False(CalmStand.Calms(null, switchedOn: true));
        }

        [Fact]
        public void ATapOnAStandHoldingTheSerpentTrophySwitchesTheCalm()
        {
            Assert.True(CalmStand.TapSwitches(hold: false, alt: false, holds: CalmStand.Trophy, takesItemsOff: true));
        }

        [Fact]
        public void HoldingTheKeyStillTakesTheTrophy()
        {
            Assert.False(CalmStand.TapSwitches(hold: true, alt: false, holds: CalmStand.Trophy, takesItemsOff: true));
        }

        [Fact]
        public void TheTurnKeyStillTurnsTheTrophy()
        {
            Assert.False(CalmStand.TapSwitches(hold: false, alt: true, holds: CalmStand.Trophy, takesItemsOff: true));
        }

        [Fact]
        public void ABossStoneIsLeftAlone()
        {
            Assert.False(CalmStand.TapSwitches(hold: false, alt: false, holds: CalmStand.Trophy, takesItemsOff: false));
        }

        [Fact]
        public void AStandWithAnotherTrophyIsLeftAlone()
        {
            Assert.False(CalmStand.TapSwitches(hold: false, alt: false, holds: "TrophyDeer", takesItemsOff: true));
        }

        [Fact]
        public void TheHoverOffersToCalmTheWatersWhileOff()
        {
            Assert.Equal(new[] { "[<color=yellow><b>$KEY_Use</b></color>] Calm the waters" }, CalmStand.HoverLines(switchedOn: false, gamepad: false));
        }

        [Fact]
        public void TheHoverSaysTheWatersAreCalmedAndOffersToLetThemBe()
        {
            Assert.Equal(new[] { "Calming the waters", "[<color=yellow><b>$KEY_Use</b></color>] Let the waters be" }, CalmStand.HoverLines(switchedOn: true, gamepad: false));
        }

        [Fact]
        public void OnAGamepadTheKeyIsNamedAsTheGameNamesIt()
        {
            Assert.Equal(new[] { "<b>$KEY_Use</b> Calm the waters" }, CalmStand.HoverLines(switchedOn: false, gamepad: true));
        }
    }
}
