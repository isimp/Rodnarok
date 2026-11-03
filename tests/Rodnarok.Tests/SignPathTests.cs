using System;
using System.Linq;
using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A float's arrival sign walks one path the eye can follow: the school's way in, smoothed, not
    /// whichever fish happens to be nearest or picked at random, each turning every second or two as
    /// the game steers it to a new place. Every showing appears on that path, and a sign that
    /// travels (the Trollfish's bulge, the Anglerfish's glow, the salmon's fin) faces the way the
    /// path moves and draws its wake where it has been. Once the school mills about at the float
    /// the signs go quiet: the travelling sign fades and one slow showing comes by the float every
    /// 2 s. When a fish goes for the bait the sign follows that fish in on a clear beat, so the
    /// moment before the bite stands out.
    /// </summary>
    public class SignPathTests
    {
        private const float Frame = 1f / 60f;
        private static readonly Spot Float = new Spot(0f, 30f, 0f);

        private static SignPath Run(SignPath path, Func<int, Spot> target, int frames, bool going = false)
        {
            for (var i = 0; i < frames; i++) path.Step(target(i), going, Float, Frame);
            return path;
        }

        [Fact]
        public void ThePathStartsAtTheSchoolAndDoesNotSlideInFromElsewhere()
        {
            var path = new SignPath();
            path.Step(new Spot(20f, 30f, 5f), false, Float, Frame);
            Assert.Equal(0f, path.At.Across(new Spot(20f, 30f, 5f)), 3);
        }

        [Fact]
        public void ThePathFollowsTheSchoolsCentreNotTheFishItJumpsBetween()
        {
            // Two fish 4 m apart swap places, so which one is nearest the float flips every frame: the path stays between them.
            Spot Centre(int i) => SignPath.Centre(i % 2 == 0
                ? new[] { new Spot(17.9f, 30f, -2f), new Spot(18.1f, 30f, 2f) }
                : new[] { new Spot(18.1f, 30f, -2f), new Spot(17.9f, 30f, 2f) });
            var path = Run(new SignPath(), Centre, 5);
            var moves = new float[60];
            for (var i = 0; i < moves.Length; i++)
            {
                var was = path.At;
                path.Step(Centre(i), false, Float, Frame);
                moves[i] = path.At.Across(was);
            }
            Assert.True(moves.Max() < 0.01f, "the path jumped " + moves.Max() + " m in one frame");
            Assert.Equal(0f, path.At.Z, 3);
        }

        [Fact]
        public void ASuddenJumpOfTheSchoolsCentreMovesThePathOnlyALittleEachFrame()
        {
            var path = Run(new SignPath(), i => new Spot(18f, 30f, 0f), 5);
            var was = path.At;
            path.Step(new Spot(18f, 30f, 4f), false, Float, Frame);
            Assert.InRange(path.At.Across(was), 0.01f, 0.1f);
        }

        [Fact]
        public void TheSchoolsCentreIsTheMeanOfItsFishAcrossTheWater()
        {
            var centre = SignPath.Centre(new[] { new Spot(0f, 29f, 0f), new Spot(4f, 28f, 0f), new Spot(2f, 30f, 6f) });
            Assert.Equal(2f, centre.X, 3);
            Assert.Equal(2f, centre.Z, 3);
        }

        [Fact]
        public void AsTheSchoolComesInThePathComesWithItTowardTheFloat()
        {
            // The school's centre swims to the float at 2 m/s from 20 m out.
            var path = Run(new SignPath(), i => new Spot(20f - 2f * i * Frame, 30f, 0f), 300);
            var centre = new Spot(20f - 2f * 300 * Frame, 30f, 0f);
            Assert.True(path.At.Across(centre) < 2.5f, "the path lags " + path.At.Across(centre) + " m behind the school");
            Assert.True(path.At.X < 12f, "the path is still " + path.At.X + " m out");
        }

        [Fact]
        public void ATravellingSignFacesTheWayThePathMovesNotTheFloat()
        {
            // The school passes the float sideways, 10 m off, swimming toward +z.
            var path = Run(new SignPath(), i => new Spot(10f, 30f, -5f + 2f * i * Frame), 120);
            Assert.True(path.HeadZ > 0.95f, "heading " + path.HeadX + ", " + path.HeadZ);
        }

        [Fact]
        public void APathThatStopsKeepsItsLastHeading()
        {
            var path = Run(new SignPath(), i => new Spot(10f + 2f * i * Frame, 30f, 0f), 60);
            Run(path, i => new Spot(12f, 30f, 0f), 600);
            Assert.True(path.HeadX > 0.95f, "heading " + path.HeadX + ", " + path.HeadZ);
            Assert.True(path.Speed < 0.05f);
        }

        [Fact]
        public void TheWakeLiesWhereThePathHasBeenAndSpreadsWithAge()
        {
            var path = Run(new SignPath(), i => new Spot(20f - 2f * i * Frame, 30f, 0f), 180);
            Assert.NotEmpty(path.Trail);
            Assert.True(path.Trail.All(p => p.Age <= SignPath.WakeSeconds));
            Assert.True(path.Trail.Count <= (int)(SignPath.WakeSeconds / SignPath.TrailStep) + 2, path.Trail.Count + " trail points");
            // Newest first, each older point further back along the way the path came.
            for (var i = 1; i < path.Trail.Count; i++)
            {
                Assert.True(path.Trail[i].Age > path.Trail[i - 1].Age);
                Assert.True(path.Trail[i].At.X >= path.Trail[i - 1].At.X);
            }
            Assert.True(SignPath.WakeSide(1f) > SignPath.WakeSide(0f));
            Assert.InRange(SignPath.WakeSide(0f), 0.1f, 0.4f);
        }

        [Fact]
        public void AStillPathShowsNoWake()
        {
            Assert.Equal(0f, SignPath.Moving(0f), 3);
            Assert.Equal(1f, SignPath.Moving(2f), 3);
            Assert.True(SignPath.Moving(0.6f) > 0f && SignPath.Moving(0.6f) < 1f);
        }

        [Fact]
        public void AtTheFloatTheSchoolGoesQuiet()
        {
            var path = Run(new SignPath(), i => new Spot(2f, 30f, 1f), 120);
            Assert.True(path.Quiet);
            Assert.Equal(SignPath.QuietEvery, path.Every(1f), 3);
            Assert.True(SignPath.QuietEvery >= 2f);
            Assert.Equal(0f, path.ShowAt(Float).Across(Float), 3);
            Assert.True(path.Showing < 0.1f, "the travelling sign still shows " + path.Showing);
        }

        [Fact]
        public void OnTheWayInTheSignIsNotQuietAndComesAsTheSchoolNears()
        {
            var path = Run(new SignPath(), i => new Spot(15f, 30f, 0f), 60);
            Assert.False(path.Quiet);
            Assert.Equal(ArrivalSign.Every(0.5f), path.Every(0.5f), 3);
            Assert.Equal(0f, path.ShowAt(Float).Across(path.At), 3);
            Assert.Equal(1f, path.Showing, 3);
        }

        [Fact]
        public void TheTravellingSignFadesOutAtTheFloatRatherThanVanishing()
        {
            var path = Run(new SignPath(), i => new Spot(2f, 30f, 0f), 3);
            Assert.True(path.Showing > 0.5f, "faded at once: " + path.Showing);
        }

        [Fact]
        public void AFishGoingForTheBaitIsFollowedInOnAClearBeat()
        {
            var path = Run(new SignPath(), i => new Spot(2f, 30f, 1f), 120);
            Assert.True(path.Quiet);
            // The fish going for the bait is 3 m off the school's centre, at the float.
            Run(path, i => new Spot(3f, 30f, -2f), 30, going: true);
            Assert.False(path.Quiet);
            Assert.True(path.Going);
            Assert.Equal(SignPath.GoingEvery, path.Every(1f), 3);
            Assert.True(path.At.Across(new Spot(3f, 30f, -2f)) < 0.6f, "the path is " + path.At.Across(new Spot(3f, 30f, -2f)) + " m from the fish after 0.5 s");
            Assert.True(path.Showing > 0.5f);
        }

        [Fact]
        public void TheBeatGoingForTheBaitIsSlowerThanTheOldCloseBeatAndFasterThanQuiet()
        {
            Assert.True(SignPath.GoingEvery > ArrivalSign.Every(1f));
            Assert.True(SignPath.GoingEvery < SignPath.QuietEvery);
        }
    }
}
