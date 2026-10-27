using Xunit;

namespace Rodnarok.Tests
{
    /// <summary>
    /// A clip of the game's read once as still frames, from its start to its end, gives the pose
    /// for any time between two frames: a motion played once stops at its last frame, a looping
    /// one starts over.
    /// </summary>
    public class ClipFramesTests
    {
        [Fact]
        public void AClipIsReadFromItsStartToItsEndAtLeastThirtyTimesASecond()
        {
            Assert.Equal(31, ClipFrames.Count(1f));
            Assert.Equal(2, ClipFrames.Count(0f));
        }

        [Fact]
        public void TheStartIsTheFirstFrame()
        {
            ClipFrames.At(0f, 1f, 31, loop: false, out var from, out var to, out var between);

            Assert.Equal(0, from);
            Assert.Equal(1, to);
            Assert.Equal(0f, between, 3);
        }

        [Fact]
        public void ATimeBetweenFramesLiesBetweenThem()
        {
            ClipFrames.At(0.05f, 1f, 31, loop: false, out var from, out var to, out var between);

            Assert.Equal(1, from);
            Assert.Equal(2, to);
            Assert.Equal(0.5f, between, 3);
        }

        [Fact]
        public void AMotionPlayedOnceStopsAtItsLastFrame()
        {
            ClipFrames.At(5f, 1f, 31, loop: false, out var from, out var to, out var between);

            Assert.Equal(29, from);
            Assert.Equal(30, to);
            Assert.Equal(1f, between, 3);
        }

        [Fact]
        public void ALoopingMotionStartsOver()
        {
            ClipFrames.At(1.05f, 1f, 31, loop: true, out var from, out var to, out var between);

            Assert.Equal(1, from);
            Assert.Equal(2, to);
            Assert.Equal(0.5f, between, 3);
        }
    }
}
