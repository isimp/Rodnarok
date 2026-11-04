using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// The fish whose sign is followed in is the fish that touches the bait. While a school or a
    /// called fish is coming to a float, the float is claimed: the world's other fish leave its bait
    /// alone, so no unannounced fish bites while the sign is elsewhere; once the fish is hooked the
    /// float is free again. The fish picked to take the bait hovers by the float until it may go
    /// for the hook, and bumps the float only while it is there; a bump still due when it may bite
    /// is dropped, as no bump comes right before a bite. Meanwhile the sign stays quiet, its slow
    /// showing by the float where the picked fish hovers, and follows that fish in once it may bite.
    /// </summary>
    public class BiterShowsTests
    {
        [Fact]
        public void TheSignStaysQuietWhileThePickedFishHoversAndFollowsItInOnceItMayBite()
        {
            Assert.False(Bumps.FollowedIn(picked: true, mayBite: false));
            Assert.True(Bumps.FollowedIn(picked: true, mayBite: true));
            Assert.False(Bumps.FollowedIn(picked: false, mayBite: true));
        }

        [Fact]
        public void ThePickedFishHoversWhereTheSignIsQuiet()
        {
            // The sign goes quiet with the school within There of the float; the picked fish hovers well inside that.
            Assert.True(Bumps.Hover < ArrivalSign.There / 2f);
        }

        [Fact]
        public void TheWorldsOtherFishLeaveAClaimedFloatAlone()
        {
            Assert.False(Arrival.WildFishMayTake(claimed: true));
            Assert.True(Arrival.WildFishMayTake(claimed: false));
        }

        [Fact]
        public void AFloatIsClaimedFromTheCastUntilAFishIsHooked()
        {
            Assert.True(Arrival.Claimed(cast: true, hooked: false));
            Assert.False(Arrival.Claimed(cast: true, hooked: true));
            Assert.False(Arrival.Claimed(cast: false, hooked: false));
        }

        [Fact]
        public void APickedFishHoversCloseToTheFloat()
        {
            Assert.InRange(Bumps.Hover, 0.8f, 2f);
            Assert.True(Bumps.Hover < Bumps.Reach);
        }

        [Fact]
        public void ABumpComesOnlyWhileThePickedFishIsByTheFloat()
        {
            Assert.True(Bumps.Fires(now: 1f, due: 0.9f, metresFromFloat: 1.2f, freeAt: 3f));
            Assert.False(Bumps.Fires(now: 1f, due: 0.9f, metresFromFloat: Bumps.Reach + 0.5f, freeAt: 3f));
            Assert.False(Bumps.Fires(now: 0.8f, due: 0.9f, metresFromFloat: 1.2f, freeAt: 3f));
        }

        [Fact]
        public void ABumpStillDueWhenTheFishMayBiteIsDropped()
        {
            Assert.False(Bumps.Fires(now: 2.6f, due: 1f, metresFromFloat: 1f, freeAt: 3f));
            Assert.True(Bumps.Dropped(now: 2.6f, freeAt: 3f));
            Assert.False(Bumps.Dropped(now: 1f, freeAt: 3f));
        }
    }
}
