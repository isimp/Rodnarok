using System;
using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>The sign a fish coming to a float shows on the water.</summary>
    internal enum ArrivalSignKind
    {
        /// <summary>Small rings where a fish rises.</summary>
        Rings,

        /// <summary>Many tiny rings at once, a school flickering near the surface.</summary>
        Flicker,

        /// <summary>Nothing until close, then one swirl by the float.</summary>
        Swirl,

        /// <summary>A heavy bulge on the surface with a wake behind it.</summary>
        Bulge,

        /// <summary>Single big bubbles that burst into a ring.</summary>
        Bloop,

        /// <summary>A fizz of tiny bubbles.</summary>
        Fizz,

        /// <summary>Slow domes that swell on the surface and pop.</summary>
        Dome,

        /// <summary>A lure's glow drifting under the water.</summary>
        Glow,

        /// <summary>Steam puffs off the water.</summary>
        Steam,

        /// <summary>A boil of short splashes where the school chases.</summary>
        Boil,

        /// <summary>A fin cutting the surface with a thin wake.</summary>
        Fin,

        /// <summary>A fast zigzag of glints in shallow water.</summary>
        Zigzag,
    }

    /// <summary>
    /// How a fish coming to a float tells itself: its sign, how often and how strongly it shows as
    /// the school nears, and whether it leaps on the way, as the game's own fish do, mostly at dawn
    /// and dusk.
    /// </summary>
    internal static class ArrivalSign
    {
        private static readonly Dictionary<string, ArrivalSignKind> Signs = new Dictionary<string, ArrivalSignKind>(StringComparer.Ordinal)
        {
            ["Fish1"] = ArrivalSignKind.Rings,
            ["Fish6"] = ArrivalSignKind.Flicker,
            ["Fish2"] = ArrivalSignKind.Swirl,
            ["Fish5"] = ArrivalSignKind.Bulge,
            ["Fish7"] = ArrivalSignKind.Bloop,
            ["Fish8"] = ArrivalSignKind.Fizz,
            ["Fish12"] = ArrivalSignKind.Dome,
            ["Fish9"] = ArrivalSignKind.Glow,
            ["Fish11"] = ArrivalSignKind.Steam,
            ["Fish3"] = ArrivalSignKind.Boil,
            ["Fish10"] = ArrivalSignKind.Fin,
            ["Fish4_cave"] = ArrivalSignKind.Zigzag,
        };

        /// <summary>How far from the float a school counts as far off, and as there, across the water in metres.</summary>
        public const float FarOff = 30f;
        public const float There = 4f;

        /// <summary>How near the school counts at which the Pike's swirl shows.</summary>
        public const float SwirlFrom = 0.8f;

        /// <summary>The sign of a fish by its prefab; the Perch's rings for a fish not of the game.</summary>
        public static ArrivalSignKind Of(string prefab) =>
            prefab != null && Signs.TryGetValue(prefab, out var sign) ? sign : ArrivalSignKind.Rings;

        /// <summary>How near the school is, 0 far off to 1 at the float, by its nearest fish's distance from the float across the water.</summary>
        public static float Nearness(float metres) => 1f - Clamp01((metres - There) / (FarOff - There));

        /// <summary>Seconds between two signs at a nearness.</summary>
        public static float Every(float nearness) => 1.9f - 1.3f * Clamp01(nearness);

        /// <summary>How strongly a sign shows at a nearness, 1 at its strongest.</summary>
        public static float Strength(float nearness) => 0.45f + 0.55f * Clamp01(nearness);

        /// <summary>Whether a sign shows at a nearness: the Pike's swirl only close to the float, every other always.</summary>
        public static bool Shows(ArrivalSignKind sign, float nearness) => sign != ArrivalSignKind.Swirl || nearness >= SwirlFrom;

        /// <summary>Whether a fish of a sign leaps on its way in: the Tuna's boil and the Northern salmon's fin.</summary>
        public static bool Leaps(ArrivalSignKind sign) => sign == ArrivalSignKind.Boil || sign == ArrivalSignKind.Fin;

        /// <summary>
        /// The game's weight for a fish to leap at a time of day, as a share from 0 to 1 of the day:
        /// full at a quarter and three quarters of the day, none at noon and midnight.
        /// </summary>
        public static float DawnDusk(float dayFraction) => 1f - Math.Abs(Math.Abs(dayFraction * 2f - 1f) - 0.5f) * 2f;

        /// <summary>
        /// How dark it is at a time of day, as a share from 0 to 1 of the day, by the game's own night
        /// light: none from a quarter to three quarters of the day, then the square root of the night's
        /// share, full at midnight.
        /// </summary>
        public static float Darkness(float dayFraction)
        {
            var night = Math.Max(1f - dayFraction / 0.25f, (dayFraction - 0.75f) / 0.25f);
            return night <= 0f ? 0f : (float)Math.Sqrt(Math.Min(1f, night));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
