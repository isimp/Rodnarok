using System;

namespace Rodnarok
{
    /// <summary>A colour as red, green and blue from 0 to 1, without Unity's types so the model builds anywhere.</summary>
    internal readonly struct Shade : IEquatable<Shade>
    {
        public Shade(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }

        /// <summary>How bright the colour looks, by the weights of the screen's own colours.</summary>
        public float Luminance => 0.2126f * R + 0.7152f * G + 0.0722f * B;

        public Shade Times(float k) => new Shade(R * k, G * k, B * k);

        public bool Equals(Shade other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B);
        public override bool Equals(object obj) => obj is Shade shade && Equals(shade);
        public override int GetHashCode() => (R.GetHashCode() * 397 ^ G.GetHashCode()) * 397 ^ B.GetHashCode();
        public static bool operator ==(Shade left, Shade right) => left.Equals(right);
        public static bool operator !=(Shade left, Shade right) => !left.Equals(right);
        public override string ToString() => FormattableString.Invariant($"({R:0.###}, {G:0.###}, {B:0.###})");
    }

    /// <summary>The fog the game draws the world in, as Unity counts it.</summary>
    internal enum SignFog
    {
        Off,
        Linear,
        Exponential,
        ExponentialSquared,
    }

    /// <summary>
    /// How the signs on the water are lit, from the light the game puts on the world each frame: its
    /// sun, or the moon it turns that light into at night, by colour and strength and how high it
    /// stands, and its ambient light. A sign's line is a bright crest of foam lit so, at least
    /// <see cref="MinLight"/> bright so a night sign still reads, over a darker trough in the air's
    /// hue; the crest shines most on the side facing the light, the lines thin as they spread, and a
    /// far sign takes the colour of the game's fog as the world does.
    /// </summary>
    internal static class SignLight
    {
        /// <summary>How much of the light falling on it foam gives back.</summary>
        public const float Foam = 0.9f;

        /// <summary>The least brightness a crest has, so a sign reads on night water.</summary>
        public const float MinLight = 0.2f;

        /// <summary>How much of the ambient light a trough keeps.</summary>
        public const float TroughShare = 0.3f;

        /// <summary>
        /// The colour of a crest of foam under a light of a colour and strength, standing
        /// <paramref name="sunUp"/> high (the up part of the direction toward it, 1 overhead, 0 or
        /// less at or under the horizon), and the ambient light.
        /// </summary>
        public static Shade Crest(Shade sun, float intensity, float sunUp, Shade ambient)
        {
            var falls = Math.Max(0f, intensity) * Math.Max(0f, sunUp);
            var lit = new Shade(
                Foam * (sun.R * falls + ambient.R),
                Foam * (sun.G * falls + ambient.G),
                Foam * (sun.B * falls + ambient.B));
            var luminance = lit.Luminance;
            if (luminance < MinLight) lit = luminance > 0.0001f ? lit.Times(MinLight / luminance) : new Shade(MinLight, MinLight, MinLight);
            return new Shade(Math.Min(1f, lit.R), Math.Min(1f, lit.G), Math.Min(1f, lit.B));
        }

        /// <summary>The colour of the trough under a crest: the ambient light's hue, dark.</summary>
        public static Shade Trough(Shade ambient) => ambient.Times(TroughShare);

        /// <summary>How brightly a part of a crest shines by how much it faces the light across the water, 1 facing it, -1 facing away.</summary>
        public static float SunSide(float facing) => 0.55f + 0.45f * Math.Max(0f, Math.Min(1f, facing));

        /// <summary>How much of the fog a sign at a distance in metres shows through, by the game's fog mode, density, and linear start and end.</summary>
        public static float FogShare(SignFog mode, float density, float start, float end, float metres)
        {
            var d = Math.Max(0f, metres);
            switch (mode)
            {
                case SignFog.Linear:
                    return end > start ? Clamp01((d - start) / (end - start)) : 0f;
                case SignFog.Exponential:
                    return 1f - (float)Math.Exp(-Math.Max(0f, density) * d);
                case SignFog.ExponentialSquared:
                    var x = Math.Max(0f, density) * d;
                    return 1f - (float)Math.Exp(-x * x);
                default:
                    return 0f;
            }
        }

        /// <summary>A colour seen through a share of the fog.</summary>
        public static Shade Through(Shade colour, Shade fog, float share)
        {
            var t = Clamp01(share);
            return new Shade(colour.R * (1f - t) + fog.R * t, colour.G * (1f - t) + fog.G * t, colour.B * (1f - t) + fog.B * t);
        }

        /// <summary>The weakest and strongest contrast the server can set for the signs.</summary>
        public const float MinContrast = 0.5f;
        public const float MaxContrast = 2f;

        /// <summary>How strongly a part of a sign shows at a contrast: its strength times the contrast, never past whole.</summary>
        public static float Shown(float alpha, float contrast) => Clamp01(alpha * Contrast(contrast));

        /// <summary>How wide the lines of the signs are at a contrast, as a share of their width as made: wider when stronger, up to half again at the strongest; a weaker contrast keeps them as wide.</summary>
        public static float LineWidth(float contrast) => 1f + 0.5f * Math.Max(0f, Contrast(contrast) - 1f) / (MaxContrast - 1f);

        private static float Contrast(float contrast) => contrast < MinContrast ? MinContrast : contrast > MaxContrast ? MaxContrast : contrast;

        /// <summary>How wide a ring or wake line is, as a share of its first width, as it spreads from 0 to 1 of its life.</summary>
        public static float Taper(float q) => 1f - 0.6f * Clamp01(q);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
