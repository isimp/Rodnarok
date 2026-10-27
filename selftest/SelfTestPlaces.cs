using UnityEngine;

namespace Rodnarok
{
    /// <summary>Places beside the character a scenario puts things: dry land for a chair or an item stand.</summary>
    internal static class SelfTestPlaces
    {
        /// <summary>How far above the water dry land must lie, and how level it must be across a metre.</summary>
        private const float AboveWater = 0.5f;
        private const float MostRise = 0.4f;

        /// <summary>A spot of dry, fairly level ground 1.5 to 4 m from the character, the nearest found; none where there is none.</summary>
        public static Vector3? DryLandBeside(Vector3 from) => DryLandBeside(from, null);

        /// <summary>A spot of dry, fairly level ground 1.5 to 4 m from the character that also suits the scenario, the nearest found; none where there is none.</summary>
        public static Vector3? DryLandBeside(Vector3 from, System.Func<Vector3, bool> suits)
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
                    var spot = new Vector3(p.x, ground, p.z);
                    if (suits != null && !suits(spot)) continue;
                    return spot;
                }
            }
            return null;
        }

        /// <summary>The layers a thrown or shot projectile hits, as the game's projectiles have them, characters left out.</summary>
        private static readonly int Solids = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "terrain", "vehicle");

        /// <summary>How far ahead of the thrower a throw must be clear, in metres: what it meets so near ends the throw at once.</summary>
        private const float ClearThrow = 8f;

        /// <summary>What a throw from a seated thrower at this spot toward the target meets within its first metres; null when clear.</summary>
        public static string ThrowBlockedBy(Vector3 spot, Vector3 target)
        {
            var eye = spot + Vector3.up * 1.2f;
            var dir = (target + Vector3.up * 3f - eye).normalized;
            return Physics.Raycast(eye, dir, out var hit, ClearThrow, Solids, QueryTriggerInteraction.Ignore)
                ? hit.collider.name + " at " + hit.distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m"
                : null;
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
