using System;
using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>
    /// The float's tremble as the fish going for the bait closes its last <see cref="From"/>: twitches
    /// that nose the float quickly toward the fish and ease it back, growing to
    /// <see cref="MaxDegrees"/> and coming quicker as the fish reaches <see cref="At"/>, the reach a
    /// fish bites from. Only the float's look tilts; the game's float itself is not moved.
    /// </summary>
    internal static class FloatTremble
    {
        /// <summary>How near the hook, across the water in metres, the fish going for the bait starts the float trembling.</summary>
        public const float From = 1.5f;

        /// <summary>How near the hook the tremble is at its strongest: the reach a fish bites from.</summary>
        public const float At = 0.6f;

        /// <summary>The most the float tilts, in degrees.</summary>
        public const float MaxDegrees = 12f;

        /// <summary>The share of each twitch spent nosing the float over; the rest eases it back.</summary>
        private const float Nose = 0.2f;

        /// <summary>How close the fish is, 0 at <see cref="From"/> or farther to 1 at <see cref="At"/> or nearer.</summary>
        public static float Closeness(float metres) => Clamp01((From - metres) / (From - At));

        /// <summary>How long one twitch takes at a closeness, in seconds.</summary>
        public static float Seconds(float closeness) => 0.6f - 0.3f * Clamp01(closeness);

        /// <summary>The float's tilt in degrees at a closeness, a share from 0 to 1 through the twitch.</summary>
        public static float Degrees(float closeness, float phase)
        {
            if (closeness <= 0f) return 0f;
            var p = phase - (float)Math.Floor(phase);
            float pulse;
            if (p < Nose) pulse = (float)Math.Sin(p / Nose * Math.PI / 2);
            else
            {
                var back = (float)Math.Cos((p - Nose) / (1f - Nose) * Math.PI / 2);
                pulse = back * back;
            }
            return MaxDegrees * (0.25f + 0.75f * Clamp01(closeness)) * pulse;
        }

        /// <summary>
        /// The axis across the water to tilt the float about, x and z, so its top leans toward a fish
        /// at an offset from it: up crossed with the way to the fish.
        /// </summary>
        public static (float X, float Z) Axis(float fishX, float fishZ)
        {
            var length = (float)Math.Sqrt(fishX * fishX + fishZ * fishZ);
            if (length < 0.0001f) return (1f, 0f);
            // Up (0, 1, 0) crossed with (x, 0, z) is (z, 0, -x).
            return (fishZ / length, -fishX / length);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// A fish fleeing a hooked school or a fright darts off on the water: a short V of a wake that
    /// runs <see cref="Metres"/> away from what frightened it over <see cref="Seconds"/>, fast at
    /// first, and fades. Only a flee marked within the last moment darts, so a fish leaving the
    /// float any other way just goes.
    /// </summary>
    internal static class ScatterBurst
    {
        public const float Metres = 2.5f;
        public const float Seconds = 0.8f;

        /// <summary>How long after a fish's flee was marked it still darts, in seconds.</summary>
        public const float FreshFor = 1.5f;

        /// <summary>The way across the water away from what frightened a fish, x and z.</summary>
        public static (float X, float Z) Away(float fishX, float fishZ, float fromX, float fromZ)
        {
            var dx = fishX - fromX;
            var dz = fishZ - fromZ;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            return length < 0.0001f ? (0f, 1f) : (dx / length, dz / length);
        }

        /// <summary>How far the dart has run at a share from 0 to 1 of its time.</summary>
        public static float Run(float q)
        {
            var left = 1f - Clamp01(q);
            return Metres * (1f - left * left);
        }

        /// <summary>How strongly the dart shows at a share of its time.</summary>
        public static float Alpha(float q) => (1f - Clamp01(q)) * Math.Min(1f, Clamp01(q) * 8f);

        /// <summary>Whether a flee marked a number of seconds ago is the one happening now.</summary>
        public static bool Fresh(float secondsAgo) => secondsAgo >= 0f && secondsAgo <= FreshFor;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// How a starred fish shows in the signs, by the game's item quality (1 plain, one more per
    /// star): a school holding one shows a little bigger and bolder on the way in, the same however
    /// many stars, a hint only; a fish going for the bait shows by its own stars, plain if it has none.
    /// </summary>
    internal static class SignStars
    {
        /// <summary>How much bigger a sign shows than a plain fish's.</summary>
        public static float Size(int schoolBest, bool going, int goingQuality)
        {
            if (going) return 1f + 0.15f * (Math.Min(4, Math.Max(1, goingQuality)) - 1);
            return schoolBest > 1 ? 1.1f : 1f;
        }

        /// <summary>How much bolder a sign shows than a plain fish's.</summary>
        public static float Boldness(int schoolBest, bool going, int goingQuality)
        {
            if (going) return goingQuality > 1 ? 1.2f : 1f;
            return schoolBest > 1 ? 1.15f : 1f;
        }
    }

    /// <summary>
    /// Parts kept to be used again: a part given back is put away and kept, up to
    /// <c>keep</c> spare, and taken again before a new one is made.
    /// </summary>
    internal sealed class PartPool<T> where T : class
    {
        private readonly Func<T> _make;
        private readonly Action<T> _putAway;
        private readonly Action<T> _drop;
        private readonly int _keep;
        private readonly Stack<T> _spare = new Stack<T>();

        public PartPool(Func<T> make, Action<T> putAway, Action<T> drop, int keep)
        {
            _make = make;
            _putAway = putAway;
            _drop = drop;
            _keep = keep;
        }

        /// <summary>How many parts this pool has made.</summary>
        public int Made { get; private set; }

        /// <summary>How many parts lie spare.</summary>
        public int Spare => _spare.Count;

        public T Take()
        {
            if (_spare.Count > 0) return _spare.Pop();
            Made++;
            return _make();
        }

        public void Return(T part)
        {
            if (part == null) return;
            _putAway(part);
            if (_spare.Count < _keep) _spare.Push(part);
            else _drop(part);
        }

        /// <summary>Drops every spare part.</summary>
        public void Clear()
        {
            while (_spare.Count > 0) _drop(_spare.Pop());
        }
    }
}
