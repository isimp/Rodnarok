namespace Rodnarok
{
    /// <summary>
    /// How the crosshair ring is drawn for a <see cref="Ring"/>: shown or not, its scale, how much
    /// of its arc is drawn, its colour, and how strongly the full track behind the arc shows.
    /// </summary>
    internal readonly struct RingDraw
    {
        private RingDraw(bool shown, float scale, float fill, float r, float g, float b, float a, float trackA)
        {
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
                    return Fight(ring, 1f, YellowG, YellowB);
                default:
                    return default;
            }
        }

        private static RingDraw Fight(Ring ring, float r, float g, float b) =>
            ring.Strain.HasValue
                ? new RingDraw(true, Size, Clamp01(ring.Strain.Value), r, g, b, 1f, Track)
                : new RingDraw(true, Size, 1f, r, g, b, 1f, 0f);

        /// <summary>The ring's one size, as a share of the game's bow ring undrawn.</summary>
        private const float Size = 0.4f;

        /// <summary>How strongly the track behind a strain shows.</summary>
        private const float Track = 0.25f;

        /// <summary>Unity's yellow, as the game's bow ring turns.</summary>
        private const float YellowG = 0.92f;
        private const float YellowB = 0.016f;

        /// <summary>The struggle's red.</summary>
        private const float RedG = 0.25f;
        private const float RedB = 0.22f;

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
