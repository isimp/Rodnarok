using System;

namespace Rodnarok
{
    /// <summary>How the float's light shows: whether it is lit, how bright and how far it reaches, and its colour.</summary>
    internal readonly struct GlowLook
    {
        public GlowLook(bool on, float intensity, float range, float r, float g, float b)
        {
            On = on;
            Intensity = intensity;
            Range = range;
            R = r;
            G = g;
            B = b;
        }

        public bool On { get; }
        public float Intensity { get; }

        /// <summary>How far the light reaches, in metres.</summary>
        public float Range { get; }

        public float R { get; }
        public float G { get; }
        public float B { get; }
    }

    /// <summary>
    /// The float's glow at night, so it can be found and its bite seen in the dark. It is a soft
    /// light in the ring's resting yellow, as strong as the game's night light is in the sky: none
    /// by day, rising after sunset to full at midnight and falling again before sunrise. A bite
    /// flashes it white and brighter, fading back to the glow over the hook window as the ring
    /// drains. Through the fight it stays the soft glow; the ring shows struggle and rest.
    /// </summary>
    internal static class FloatGlow
    {
        /// <summary>Whether a player sees floats glow before choosing otherwise.</summary>
        public const bool OnByDefault = true;

        /// <summary>The soft glow's brightness and reach, in metres, at full night.</summary>
        public const float GlowIntensity = 0.8f;
        public const float GlowRange = 2.5f;

        /// <summary>The flash's brightness and reach, in metres, the moment a fish bites.</summary>
        public const float FlashIntensity = 3f;
        public const float FlashRange = 5f;

        /// <summary>How dark it is by the time of day, from 0 by day to 1 at midnight: the weight the game gives its night light.</summary>
        public static float Darkness(float dayFraction)
        {
            var night = Math.Max(1f - Clamp01(dayFraction / 0.25f), Clamp01((dayFraction - 0.75f) / 0.25f));
            return (float)Math.Sqrt(Clamp01(night));
        }

        /// <summary>The float's light at this darkness, this long after a bite not yet hooked.</summary>
        public static GlowLook Look(float darkness, float sinceBite, float hookWindow)
        {
            darkness = Clamp01(darkness);
            if (darkness <= 0f) return default;
            var flash = Bite.InHookWindow(sinceBite, hookWindow) && hookWindow > 0f ? 1f - sinceBite / hookWindow : 0f;
            return new GlowLook(true,
                Lerp(GlowIntensity, FlashIntensity, flash) * darkness,
                Lerp(GlowRange, FlashRange, flash),
                1f,
                Lerp(RingDraw.YellowG, 1f, flash),
                Lerp(RingDraw.YellowB, 1f, flash));
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp01(float v) => float.IsNaN(v) || v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
