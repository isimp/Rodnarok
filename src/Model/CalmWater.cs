using System;
using System.Collections.Generic;
using System.Linq;

namespace Rodnarok
{
    /// <summary>
    /// Which water a calming item stand calms. The game shapes the waves of each 64 m water tile by
    /// four corner values, blended across the tile, and draws its water from the same values, so
    /// lowering a corner calms what is seen and what floats alike. A corner's value here depends on
    /// where it is alone, so two tiles meeting at a corner always lower it alike and no seam shows.
    /// </summary>
    internal static class CalmWater
    {
        /// <summary>The side of a water tile, a zone of the world.</summary>
        public const float Tile = 64f;

        /// <summary>
        /// A tile's corners in the order the game blends them: 0 at -x,+z, 1 at +x,+z, 2 at +x,-z,
        /// 3 at -x,-z, at the tile centre's height.
        /// </summary>
        public static Spot[] Corners(Spot tileCentre)
        {
            const float half = Tile / 2f;
            return new[]
            {
                new Spot(tileCentre.X - half, tileCentre.Y, tileCentre.Z + half),
                new Spot(tileCentre.X + half, tileCentre.Y, tileCentre.Z + half),
                new Spot(tileCentre.X + half, tileCentre.Y, tileCentre.Z - half),
                new Spot(tileCentre.X - half, tileCentre.Y, tileCentre.Z - half),
            };
        }

        /// <summary>
        /// A tile's four corner values with every corner within <paramref name="reach"/> of a calming stand,
        /// measured across the ground, keeping only the <paramref name="keep"/> share of its waves.
        /// Calming never raises a wave, whatever the share.
        /// </summary>
        public static float[] Calmed(float[] depth, Spot tileCentre, IEnumerable<Spot> stands, float reach, float keep)
        {
            var share = Math.Max(0f, Math.Min(1f, keep));
            var all = (stands ?? Enumerable.Empty<Spot>()).ToList();
            var corners = Corners(tileCentre);
            var result = (float[])depth.Clone();
            for (var i = 0; i < result.Length && i < corners.Length; i++)
            {
                var corner = corners[i];
                if (all.Exists(piece => piece.Across(corner) <= reach)) result[i] = depth[i] * share;
            }
            return result;
        }
    }
}
