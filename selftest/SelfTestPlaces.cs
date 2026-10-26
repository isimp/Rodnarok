using UnityEngine;

namespace Rodnarok
{
    /// <summary>Places beside the character a scenario puts things: dry land for a chair or a post.</summary>
    internal static class SelfTestPlaces
    {
        /// <summary>How far above the water dry land must lie, and how level it must be across a metre.</summary>
        private const float AboveWater = 0.5f;
        private const float MostRise = 0.4f;

        /// <summary>A spot of dry, fairly level ground 1.5 to 4 m from the character, the nearest found; none where there is none.</summary>
        public static Vector3? DryLandBeside(Vector3 from)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            for (var distance = 1.5f; distance <= 4f; distance += 0.5f)
            {
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * Mathf.PI / 8f;
                    var p = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (!ZoneSystem.instance.GetSolidHeight(p, out var ground) || ground < water + AboveWater) continue;
                    if (!Level(p, ground)) continue;
                    return new Vector3(p.x, ground, p.z);
                }
            }
            return null;
        }

        private static bool Level(Vector3 p, float ground)
        {
            foreach (var offset in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                if (!ZoneSystem.instance.GetSolidHeight(p + offset * 0.5f, out var h) || Mathf.Abs(h - ground) > MostRise) return false;
            }
            return true;
        }
    }
}
