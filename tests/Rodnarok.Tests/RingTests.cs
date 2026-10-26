using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The crosshair ring tells the bite and the fight without words: it closes over the hook
    /// window, and in the fight it shows whether the fish struggles or rests.
    /// </summary>
    public class RingTests
    {
        [Fact]
        public void TheRingIsFullyOpenTheMomentTheFishBites()
        {
            var ring = Ring.ForBite(sinceBite: 0f, window: 1.5f);
            Assert.Equal(RingLook.Closing, ring.Look);
            Assert.Equal(1f, ring.Open, 3);
        }

        [Fact]
        public void TheRingClosesOverTheHookWindow()
        {
            Assert.Equal(0.5f, Ring.ForBite(0.75f, 1.5f).Open, 3);
            Assert.Equal(0f, Ring.ForBite(1.5f, 1.5f).Open, 3);
        }

        [Fact]
        public void OnceTheWindowHasPassedTheRingIsGone()
        {
            Assert.Equal(RingLook.Hidden, Ring.ForBite(1.6f, 1.5f).Look);
        }

        [Fact]
        public void InTheFightTheRingTellsAStruggleFromARest()
        {
            Assert.Equal(RingLook.Struggle, Ring.ForFight(struggling: true).Look);
            Assert.Equal(RingLook.Rest, Ring.ForFight(struggling: false).Look);
        }
    }
}
