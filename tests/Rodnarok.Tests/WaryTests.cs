using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A school is wary of the one fishing. It hears the game's noise: a player walking or running
    /// near the float startles it, one sneaking, sitting or standing still does not. A float dragged
    /// quickly through the school startles it too. Startled, it darts off and comes back once all is
    /// quiet a while; startled a second time, it leaves. Close to the one fishing and in shallow
    /// water it hesitates long before biting and often gives up and swims off; far out over deep
    /// water it bites as before.
    /// </summary>
    public class WaryTests
    {
        private const float Walking = 15f;
        private const float Running = 30f;

        [Fact]
        public void WalkingNearTheFloatIsHeard()
        {
            Assert.True(Wary.Heard(distance: 10f, noise: Walking));
        }

        [Fact]
        public void WalkingFarFromTheFloatIsNotHeard()
        {
            Assert.False(Wary.Heard(distance: 20f, noise: Walking));
        }

        [Fact]
        public void AtTheEdgeOfTheNoiseNothingIsHeardAsInTheGame()
        {
            Assert.False(Wary.Heard(distance: Walking, noise: Walking));
        }

        [Fact]
        public void RunningIsHeardFartherOut()
        {
            Assert.True(Wary.Heard(distance: 20f, noise: Running));
        }

        [Fact]
        public void SittingSneakingOrStandingStillIsNotHeard()
        {
            Assert.False(Wary.Heard(distance: 2f, noise: 0f));
        }

        [Fact]
        public void AStartledSchoolComesBackOnlyOnceAllIsQuietAWhile()
        {
            Assert.False(Wary.ComesBack(quietFor: Wary.QuietBeforeComingBack - 1f));
            Assert.True(Wary.ComesBack(quietFor: Wary.QuietBeforeComingBack));
        }

        [Fact]
        public void TheFirstStartleIsDartingOffTheSecondIsLeaving()
        {
            Assert.False(Wary.Leaves(startles: 1));
            Assert.True(Wary.Leaves(startles: 2));
        }

        [Fact]
        public void AFloatDraggedQuicklyThroughTheSchoolStartlesIt()
        {
            Assert.True(Wary.FloatStartles(speed: 1f, schoolNear: true));
        }

        [Fact]
        public void AFloatBarelyMovedLetsTheSchoolFollow()
        {
            Assert.False(Wary.FloatStartles(speed: 0.3f, schoolNear: true));
        }

        [Fact]
        public void AFloatReeledInBeforeTheSchoolIsNearStartlesNothing()
        {
            Assert.False(Wary.FloatStartles(speed: 2f, schoolNear: false));
        }

        [Fact]
        public void FarOutOverDeepWaterAFishIsNotShy()
        {
            Assert.Equal(0f, Wary.Shyness(anglerDistance: 20f, depth: 5f), 3);
        }

        [Fact]
        public void CloseInAFishIsShyestWhateverTheDepth()
        {
            Assert.Equal(1f, Wary.Shyness(anglerDistance: 3f, depth: 5f), 3);
        }

        [Fact]
        public void InShallowWaterAFishIsShyestWhateverTheDistance()
        {
            Assert.Equal(1f, Wary.Shyness(anglerDistance: 20f, depth: 0.5f), 3);
        }

        [Fact]
        public void HalfwayInAFishIsHalfShy()
        {
            Assert.Equal(0.5f, Wary.Shyness(anglerDistance: (Wary.CloseIn + Wary.FarOut) / 2f, depth: 5f), 3);
        }

        [Fact]
        public void AShyFishHesitatesLongBeforeBiting()
        {
            Assert.Equal(0f, Wary.Hesitation(0f), 3);
            Assert.Equal(Wary.LongestHesitation, Wary.Hesitation(1f), 3);
        }

        [Fact]
        public void AShyFishOftenGivesUpAndANotShyOneNever()
        {
            Assert.True(Wary.GivesUp(1f, roll: 0.3));
            Assert.False(Wary.GivesUp(1f, roll: Wary.MostGivingUp + 0.01));
            Assert.False(Wary.GivesUp(0f, roll: 0.0));
        }

        [Fact]
        public void FishAreWaryUnlessTheServerSaysOtherwise()
        {
            Assert.True(new FishingRules().Wary);
        }
    }
}
