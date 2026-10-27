using System;

namespace Rodnarok
{
    /// <summary>
    /// A clip read once as still frames, evenly from its start to its end, and the pose for any
    /// time between two of them: a motion played once stops at its last frame, a looping one
    /// starts over.
    /// </summary>
    internal static class ClipFrames
    {
        /// <summary>How many frames a second a clip is read at, at the least.</summary>
        public const float PerSecond = 30f;

        /// <summary>How many frames a clip of the given length is read as, its start and its end included.</summary>
        public static int Count(float length) => Math.Max(2, (int)Math.Ceiling(Math.Max(0f, length) * PerSecond - 1e-4f) + 1);

        /// <summary>The two frames a time lies between, and how far from the first toward the second.</summary>
        public static void At(float time, float length, int count, bool loop, out int from, out int to, out float between)
        {
            var spans = Math.Max(1, count - 1);
            var share = length > 0f ? time / length : 0f;
            share = loop ? share - (float)Math.Floor(share) : Math.Max(0f, Math.Min(1f, share));
            var at = share * spans;
            from = Math.Min((int)Math.Floor(at), spans - 1);
            to = from + 1;
            between = at - from;
        }
    }
}
