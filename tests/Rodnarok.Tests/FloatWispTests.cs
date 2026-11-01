using System;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A player wearing a Wisplight has its wisp guard the float: while the float waits it drifts
    /// round it, searching, and now and then dips low as if peering into the water; at a bite it
    /// dives in close over the float and flares, the flare fading over the hook window; through
    /// the fight it follows the float close; once the float is gone it drifts back to the player
    /// and the game's own following takes over again without a jump. It moves as the game's wisp
    /// does: the Wisplight's acceleration 7, top speed 80 and friction 0.03 a step. Its mist
    /// clearing stays with the player, and the wisp clears a pocket of its own round the float as
    /// wide as the game's clearing where wisps gather, 6 m.
    /// </summary>
    public class FloatWispTests
    {
        private const float Acceleration = 7f;
        private const float TopSpeed = 80f;
        private const float Friction = 0.03f;
        private const float Step = 0.02f;
        private const float Window = 1.5f;

        [Fact]
        public void WithoutAFloatOutTheWispStaysWithThePlayer()
        {
            Assert.Equal(WispTask.Home, FloatWisp.Task(floatOut: false, biting: false, hooked: false));
            Assert.Equal(WispTask.Home, FloatWisp.Task(floatOut: false, biting: true, hooked: true));
        }

        [Fact]
        public void WhileTheFloatWaitsTheWispSearchesRoundItOnEverySide()
        {
            Assert.Equal(WispTask.Search, FloatWisp.Task(floatOut: true, biting: false, hooked: false));
            var lowest = float.MaxValue;
            var highest = float.MinValue;
            var sides = new bool[8];
            for (var t = 0f; t < 30f; t += Step)
            {
                var o = FloatWisp.Offset(WispTask.Search, t);
                Assert.True(Math.Sqrt(o.X * o.X + o.Z * o.Z) <= FloatWisp.SearchRadius + 0.01f, "too far out at " + t);
                lowest = Math.Min(lowest, o.Y);
                highest = Math.Max(highest, o.Y);
                sides[(int)((Math.Atan2(o.Z, o.X) + Math.PI) / (2 * Math.PI) * 8) % 8] = true;
            }
            Assert.All(sides, Assert.True);
            Assert.InRange(lowest, 0.3f, 0.6f);
            Assert.InRange(highest, 1.5f, 2f);
        }

        [Fact]
        public void NowAndThenTheSearchingWispDipsLowToPeerIntoTheWater()
        {
            var lowTime = 0f;
            for (var start = 0f; start < 40f; start += FloatWisp.DipEvery)
            {
                var dipped = false;
                for (var t = start; t < start + FloatWisp.DipEvery; t += Step)
                {
                    var y = FloatWisp.Offset(WispTask.Search, t).Y;
                    dipped |= y < 0.6f;
                    if (y < 1f) lowTime += Step;
                }
                Assert.True(dipped, "no dip between " + start + " and " + (start + FloatWisp.DipEvery) + " s");
            }
            Assert.True(lowTime < 40f * 0.25f, "low for " + lowTime + " s of 40");
        }

        [Fact]
        public void ABiteBringsTheWispInCloseOverTheFloatAndItFlares()
        {
            Assert.Equal(WispTask.Focus, FloatWisp.Task(floatOut: true, biting: true, hooked: false));
            var o = FloatWisp.Offset(WispTask.Focus, 3.7f);
            Assert.InRange(o.Y, FloatWisp.FocusHeight - 0.05f, FloatWisp.FocusHeight + 0.05f);
            Assert.True(Math.Sqrt(o.X * o.X + o.Z * o.Z) <= 0.1f);
            Assert.True(FloatWisp.Pull(WispTask.Focus) > FloatWisp.Pull(WispTask.Search));
            Assert.True(FloatWisp.Brightness(WispTask.Focus, 0f, Window) >= 2f * FloatWisp.Brightness(WispTask.Search, float.PositiveInfinity, Window));
        }

        [Fact]
        public void TheFlareFadesOverTheHookWindowToTheFightsGlow()
        {
            var start = FloatWisp.Brightness(WispTask.Focus, 0f, Window);
            var half = FloatWisp.Brightness(WispTask.Focus, Window / 2f, Window);
            var end = FloatWisp.Brightness(WispTask.Focus, Window, Window);
            Assert.True(start > half && half > end);
            Assert.Equal(FloatWisp.Brightness(WispTask.Follow, 0f, Window), end, 3);
            Assert.Equal(end, FloatWisp.Brightness(WispTask.Focus, 10f, Window), 3);
        }

        [Fact]
        public void HookedTheWispFollowsTheFloatClose()
        {
            Assert.Equal(WispTask.Follow, FloatWisp.Task(floatOut: true, biting: false, hooked: true));
            Assert.Equal(WispTask.Follow, FloatWisp.Task(floatOut: true, biting: true, hooked: true));
            for (var t = 0f; t < 10f; t += 0.1f)
            {
                var o = FloatWisp.Offset(WispTask.Follow, t);
                Assert.True(Math.Sqrt(o.X * o.X + o.Z * o.Z) <= 0.3f);
                Assert.InRange(o.Y, FloatWisp.FollowHeight - 0.01f, FloatWisp.FollowHeight + 0.01f);
            }
            Assert.True(FloatWisp.Brightness(WispTask.Follow, 0f, Window) > FloatWisp.Brightness(WispTask.Search, 0f, Window));
        }

        [Fact]
        public void WithTheGamesOwnMotionTheWispReachesAFloatTwentyMetresOutAndKeepsToItsSearch()
        {
            var wisp = new WispMotion(0f, 3.5f, 0f);
            float t = 0f;
            for (; t < 8f; t += Step) Move(ref wisp, WispTask.Search, 20f, t);
            var farthest = 0f;
            for (; t < 30f; t += Step)
            {
                Move(ref wisp, WispTask.Search, 20f, t);
                farthest = Math.Max(farthest, Distance(wisp, 20f, 0f, 0f));
            }
            Assert.True(farthest < 3.5f, "strayed " + farthest + " m from the float");
        }

        [Fact]
        public void AtABiteTheWispIsCloseOverTheFloatWithinASecondAndStaysThere()
        {
            var wisp = new WispMotion(20f, 1.4f, 0f);
            float t = 0f;
            for (; t < 10f; t += Step) Move(ref wisp, WispTask.Search, 20f, t);
            for (var since = 0f; since < 1f; since += Step, t += Step) Move(ref wisp, WispTask.Focus, 20f, t);
            var farthest = 0f;
            for (var since = 1f; since < Window; since += Step, t += Step)
            {
                Move(ref wisp, WispTask.Focus, 20f, t);
                farthest = Math.Max(farthest, Distance(wisp, 20f, FloatWisp.FocusHeight, 0f));
            }
            Assert.True(farthest < 0.6f, "focused " + farthest + " m off over the float");
        }

        [Fact]
        public void BackFromTheFloatTheWispNeverJumpsAndReachesThePlayerWithinAFewSeconds()
        {
            var wisp = new WispMotion(20f, 1.4f, 0f);
            var home = (X: 0f, Y: 3.5f, Z: 0f);
            var took = 0f;
            for (; took < 20f && Math.Sqrt((wisp.X - home.X) * (wisp.X - home.X) + (wisp.Y - home.Y) * (wisp.Y - home.Y)) > FloatWisp.HandBack; took += Step)
            {
                var (x, y, z) = (wisp.X, wisp.Y, wisp.Z);
                wisp.Step(home.X, home.Y, home.Z, Acceleration, TopSpeed, Friction, FloatWisp.Pull(WispTask.Return), Step);
                var moved = Math.Sqrt((wisp.X - x) * (wisp.X - x) + (wisp.Y - y) * (wisp.Y - y) + (wisp.Z - z) * (wisp.Z - z));
                Assert.True(moved <= 10f * Step, "jumped " + moved + " m in a step");
            }
            Assert.True(took < 8f, "took " + took + " s to come back");
        }

        [Fact]
        public void TheGamesOwnFollowingTakesOverWithinItsLeash()
        {
            Assert.Equal(6f, FloatWisp.HandBack);
        }

        /// <summary>The game's place drifts 1.5 m round the noiseless one, so the wisp is handed back within 4.5 m and never dragged.</summary>
        [Fact]
        public void TheWispIsHandedBackWithinTheLeashLessTheGamesDrift()
        {
            Assert.Equal(4.5f, FloatWisp.HandBackWithin(6f, 1.5f), 3);
            Assert.True(FloatWisp.HandBackWithin(6f, 1.5f) + 1.5f <= 6f);
            Assert.Equal(0.5f, FloatWisp.HandBackWithin(1f, 3f), 3);
        }

        [Fact]
        public void TheWispsPocketRoundTheFloatIsAsWideAsTheGamesClearingWhereWispsGather()
        {
            Assert.Equal(6f, FloatWisp.PocketRange);
        }

        [Fact]
        public void TheWispGuardsTheFloatUnlessAPlayerTurnsItOff()
        {
            Assert.True(FloatWisp.OnByDefault);
        }

        private static void Move(ref WispMotion wisp, WispTask task, float floatX, float t)
        {
            var o = FloatWisp.Offset(task, t);
            wisp.Step(floatX + o.X, o.Y, o.Z, Acceleration, TopSpeed, Friction, FloatWisp.Pull(task), Step);
        }

        private static float Distance(WispMotion wisp, float x, float y, float z) =>
            (float)Math.Sqrt((wisp.X - x) * (wisp.X - x) + (wisp.Y - y) * (wisp.Y - y) + (wisp.Z - z) * (wisp.Z - z));
    }
}
