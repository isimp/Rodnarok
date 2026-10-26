namespace Rodnarok
{
    /// <summary>How the game's bow ring is drawn for a <see cref="Ring"/>: shown or not, its scale, and its colour.</summary>
    internal readonly struct RingDraw
    {
        private RingDraw(bool shown, float scale, float r, float g, float b, float a)
        {
            Shown = shown;
            Scale = scale;
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public bool Shown { get; }
        public float Scale { get; }
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        /// <summary>The ring's look, <paramref name="time"/> in seconds driving a struggle's pulse.</summary>
        public static RingDraw For(Ring ring, float time)
        {
            switch (ring.Look)
            {
                case RingLook.Closing:
                    var spent = 1f - Clamp01(ring.Open);
                    return new RingDraw(true, Lerp(Wide, Tightest, spent), 1f, Lerp(1f, YellowG, spent), Lerp(1f, YellowB, spent), Lerp(StartAlpha, 1f, spent));
                case RingLook.Struggle:
                    var pulse = (float)System.Math.Sin(2.0 * System.Math.PI * PulsesPerSecond * time);
                    return new RingDraw(true, StruggleScale + PulseDepth * pulse, 1f, 0.25f, 0.22f, 1f);
                case RingLook.Rest:
                    return new RingDraw(true, RestScale, 1f, YellowG, YellowB, 1f);
                default:
                    return default;
            }
        }

        /// <summary>The game's bow ring: drawn at full size untensed and at 0.15 fully drawn, turning to Unity's yellow.</summary>
        private const float Wide = 1f;
        private const float Tightest = 0.15f;
        private const float YellowG = 0.92f;
        private const float YellowB = 0.016f;

        /// <summary>The bow ring starts transparent; a bite's starts clearly seen.</summary>
        private const float StartAlpha = 0.8f;

        private const float StruggleScale = 0.75f;
        private const float PulseDepth = 0.07f;
        private const float PulsesPerSecond = 2f;
        private const float RestScale = 0.3f;

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
