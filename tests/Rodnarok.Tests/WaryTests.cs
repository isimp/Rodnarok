using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A school is wary of the one fishing, without vanishing at every step. Under water it hears
    /// only part of the game's noise: a player walking or running close to it startles it, one sneaking,
    /// sitting or standing still never does; startled, it darts a few metres off and comes back once
    /// all is quiet a moment. A float moved quickly, as a short reel does, only makes it flinch: a
    /// little sidestep, back as soon as the float is still. Frights add up, a startle counting twice
    /// a flinch, and only many of them make it leave; so does a float dragged through it for a
    /// while, as reeling in does. Close to the one fishing and in shallow
    /// water it hesitates long before biting and often gives up and swims off; far out over deep
    /// water it bites as before.
    /// </summary>
    public class WaryTests
    {
        private const float Walking = 15f;
        private const float Running = 30f;

        [Fact]
        public void WalkingCloseToTheSchoolIsHeard()
        {
            Assert.True(Wary.Heard(distance: 5f, noise: Walking));
        }

        [Fact]
        public void UnderWaterOnlyPartOfTheGamesNoiseIsHeard()
        {
            Assert.InRange(Wary.Hearing, 0.25f, 0.5f);
            Assert.False(Wary.Heard(distance: 8f, noise: Walking));
            Assert.False(Wary.Heard(distance: Walking * Wary.Hearing, noise: Walking));
        }

        [Fact]
        public void RunningIsHeardFartherOut()
        {
            Assert.True(Wary.Heard(distance: 9f, noise: Running));
            Assert.False(Wary.Heard(distance: 16f, noise: Running));
        }

        [Fact]
        public void SittingSneakingOrStandingStillIsNotHeard()
        {
            Assert.False(Wary.Heard(distance: 2f, noise: 0f));
        }

        [Fact]
        public void AScaredSchoolComesBackOnlyOnceAllIsQuietAWhile()
        {
            Assert.False(Wary.ComesBack(quietFor: Wary.QuietBeforeComingBack - 1f, flinched: false));
            Assert.True(Wary.ComesBack(quietFor: Wary.QuietBeforeComingBack, flinched: false));
        }

        [Fact]
        public void AFlinchingSchoolComesBackAsSoonAsTheFloatIsStill()
        {
            Assert.False(Wary.ComesBack(quietFor: Wary.StillBeforeComingBack - 0.5f, flinched: true));
            Assert.True(Wary.ComesBack(quietFor: Wary.StillBeforeComingBack, flinched: true));
            Assert.True(Wary.StillBeforeComingBack < Wary.QuietBeforeComingBack);
        }

        [Fact]
        public void AFlinchIsALittleSidestepAndAStartleAShortDart()
        {
            Assert.InRange(Wary.FlinchDistance, 2f, 5f);
            Assert.InRange(Wary.DartDistance, Wary.FlinchDistance, 10f);
        }

        [Fact]
        public void ManyShortReelsAreOnlyFlinchesBeforeTheSchoolLeaves()
        {
            var fright = 0;
            for (var i = 0; i < 6; i++) fright += Wary.Fright(heard: false);
            Assert.False(Wary.Leaves(fright));
            Assert.True(Wary.Leaves(Wary.FrightToLeave));
        }

        [Fact]
        public void ThreeStartlesByNoiseDoNotDriveTheSchoolOff()
        {
            Assert.False(Wary.Leaves(Wary.Fright(heard: true) * 3));
            Assert.True(Wary.Leaves(Wary.Fright(heard: true) * 4));
        }

        [Fact]
        public void AStartleCountsTwiceAFlinch()
        {
            Assert.Equal(2 * Wary.Fright(heard: false), Wary.Fright(heard: true));
        }

        [Fact]
        public void AFloatKeptReelingAfterTheSchoolFlinchedAsideStillDragsThroughIt()
        {
            Assert.True(Wary.Dragging(speed: 1f, schoolNear: false, schoolAway: true));
            Assert.True(Wary.Dragging(speed: 1f, schoolNear: true, schoolAway: false));
            Assert.False(Wary.Dragging(speed: 1f, schoolNear: false, schoolAway: false));
            Assert.False(Wary.Dragging(speed: 0.3f, schoolNear: true, schoolAway: true));
        }

        [Fact]
        public void AFloatDraggedThroughTheSchoolAWhileDrivesItOff()
        {
            Assert.False(Wary.Dragged(fastFor: Wary.DraggedSeconds - 0.1f));
            Assert.True(Wary.Dragged(fastFor: Wary.DraggedSeconds));
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
            Assert.True(Wary.GivesUp(1f, roll: Wary.MostGivingUp - 0.01));
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
