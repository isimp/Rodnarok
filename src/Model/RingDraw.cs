namespace Rodnarok
{
    /// <summary>
    /// How the crosshair ring is drawn for a <see cref="Ring"/>: shown or not, its scale, how much
    /// of its arc is drawn, its colour, and how strongly the full track behind the arc shows.
    /// </summary>
    internal readonly struct RingDraw
    {
        private RingDraw(bool shown, float scale, float fill, float r, float g, float b, float a, float trackA,
            float trackFill = 1f, float underA = 0f, bool arcCounter = false)
        {
            TrackFill = trackFill;
            UnderA = underA;
            ArcCounter = arcCounter;
            Shown = shown;
            Scale = scale;
            Fill = fill;
            R = r;
            G = g;
            B = b;
            A = a;
            TrackA = trackA;
        }

        public bool Shown { get; }
        public float Scale { get; }

        /// <summary>The share of the ring's arc drawn, clockwise from the top: 1 the whole ring.</summary>
        public float Fill { get; }

        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        /// <summary>The opacity of the full track drawn behind the arc in the arc's colour; 0 for none.</summary>
        public float TrackA { get; }

        /// <summary>The share of the track drawn, counter-clockwise from the top: in a rest, how far the last heave's slack is wound in; 1 the whole track.</summary>
        public float TrackFill { get; }

        /// <summary>The track is always drawn counter-clockwise, against the strain's arc.</summary>
        public bool TrackClockwise => false;

        /// <summary>Whether the arc fills counter-clockwise: without tension, a rest's whole ring refills so after a heave.</summary>
        public bool ArcCounter { get; }

        /// <summary>The opacity of the fainter ring where the refilling track or arc has not come back yet; 0 for none.</summary>
        public float UnderA { get; }

        /// <summary>The share of that fainter ring, clockwise from the top: what the refill leaves.</summary>
        public float UnderFill => 1f - (ArcCounter ? Fill : TrackFill);

        /// <summary>
        /// The ring's look: one size throughout. After a bite the arc drains from full white toward
        /// yellow. In the fight it is red while the fish struggles and yellow while it rests; with
        /// tension the arc is the strain building clockwise over a faint full track, without it the
        /// whole ring shows.
        /// </summary>
        public static RingDraw For(Ring ring)
        {
            switch (ring.Look)
            {
                case RingLook.Closing:
                    var left = Clamp01(ring.Open);
                    var spent = 1f - left;
                    return new RingDraw(true, Size, left, 1f, Lerp(1f, YellowG, spent), Lerp(1f, YellowB, spent), 1f, 0f);
                case RingLook.Struggle:
                    return Fight(ring, 1f, RedG, RedB);
                case RingLook.Rest:
                    return Rest(ring);
                case RingLook.Waiting:
                    // White as at the bite and its size, growing stronger as the fish comes closer, over a faint track.
                    var strength = ApproachRing.Strength(ring.Open);
                    return new RingDraw(true, Size, 1f, 1f, 1f, 1f, strength, Track * strength);
                default:
                    return default;
            }
        }

        /// <summary>
        /// A rest: yellow, and after a heave the track, or without tension the whole ring, empties and
        /// refills counter-clockwise as the slack is wound in, over a fainter ring; full, as ever.
        /// </summary>
        private static RingDraw Rest(Ring ring)
        {
            var wound = Clamp01(ring.Wound);
            var refilling = wound < 1f;
            if (ring.Strain.HasValue)
                return new RingDraw(true, Size, Clamp01(ring.Strain.Value), 1f, YellowG, YellowB, 1f, Track, wound, refilling ? Under : 0f);
            return new RingDraw(true, Size, wound, 1f, YellowG, YellowB, 1f, 0f, 1f, refilling ? Track : 0f, refilling);
        }

        private static RingDraw Fight(Ring ring, float r, float g, float b) =>
            ring.Strain.HasValue
                ? new RingDraw(true, Size, Clamp01(ring.Strain.Value), r, g, b, 1f, Track)
                : new RingDraw(true, Size, 1f, r, g, b, 1f, 0f);

        /// <summary>The ring's one size, as a share of the game's bow ring undrawn.</summary>
        private const float Size = 0.4f;

        /// <summary>How strongly the track behind a strain shows.</summary>
        private const float Track = 0.25f;

        /// <summary>How strongly the fainter ring shows where a rest's track has not refilled yet.</summary>
        private const float Under = 0.06f;

        /// <summary>Unity's yellow, as the game's bow ring turns.</summary>
        internal const float YellowG = 0.92f;
        internal const float YellowB = 0.016f;

        /// <summary>The struggle's red.</summary>
        private const float RedG = 0.25f;
        private const float RedB = 0.22f;

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
