namespace Rodnarok
{
    /// <summary>How the crosshair ring is drawn for a <see cref="Ring"/>: shown or not, its scale, how much of its arc is drawn, and its colour.</summary>
    internal readonly struct RingDraw
    {
        private RingDraw(bool shown, float scale, float fill, float r, float g, float b, float a)
        {
            Shown = shown;
            Scale = scale;
            Fill = fill;
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public bool Shown { get; }
        public float Scale { get; }

        /// <summary>The share of the ring's arc drawn, clockwise from the top: 1 the whole ring.</summary>
        public float Fill { get; }

        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        /// <summary>The ring's look: one size throughout; after a bite the arc drains from full white toward yellow, in the fight the whole ring is red or yellow.</summary>
        public static RingDraw For(Ring ring)
        {
            switch (ring.Look)
            {
                case RingLook.Closing:
                    var left = Clamp01(ring.Open);
                    var spent = 1f - left;
                    return new RingDraw(true, Size, left, 1f, Lerp(1f, YellowG, spent), Lerp(1f, YellowB, spent), 1f);
                case RingLook.Struggle:
                    return new RingDraw(true, Size, 1f, 1f, 0.25f, 0.22f, 1f);
                case RingLook.Rest:
                    return new RingDraw(true, Size, 1f, 1f, YellowG, YellowB, 1f);
                default:
                    return default;
            }
        }

        /// <summary>The ring's one size, as a share of the game's bow ring undrawn.</summary>
        private const float Size = 0.4f;

        /// <summary>Unity's yellow, as the game's bow ring turns.</summary>
        private const float YellowG = 0.92f;
        private const float YellowB = 0.016f;

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
