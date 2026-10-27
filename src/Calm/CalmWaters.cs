using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The water calmed around the item stands holding a switched-on Serpent trophy
    /// (<see cref="CalmStands"/>). The game shapes each water tile's waves by its four corner
    /// values and draws the tile's water from the same values, so the corners within a stand's
    /// reach are lowered to their share (<see cref="CalmWater"/>) and handed to the drawing again.
    /// Each tile's own values are kept, and given back when no stand reaches it any more. A tile
    /// reads its values as it starts, so it is calmed only from then on.
    /// </summary>
    internal static class CalmWaters
    {
        /// <summary>How far from a tile's centre its farthest corner lies, across the ground.</summary>
        private static readonly float TileReach = CalmWater.Tile / 2f * Mathf.Sqrt(2f);

        private static readonly AccessTools.FieldRef<WaterVolume, float[]> Corners = AccessTools.FieldRefAccess<WaterVolume, float[]>("m_normalizedDepth");
        private static readonly AccessTools.FieldRef<WaterVolume, float> OneDepth = AccessTools.FieldRefAccess<WaterVolume, float>("m_oneDepth");
        private static readonly System.Reflection.MethodInfo SetupMaterial = AccessTools.Method(typeof(WaterVolume), "SetupMaterial");

        /// <summary>How far a calming place must move before the water is calmed anew, in metres.</summary>
        private const float Moved = 0.5f;

        private static List<Vector3> _calming = new List<Vector3>();
        private static readonly HashSet<WaterVolume> Started = new HashSet<WaterVolume>();
        private static readonly Dictionary<WaterVolume, (float[] Corners, float One)> Own = new Dictionary<WaterVolume, (float[], float)>();
        private static bool _changed;
        private static float _reach = -1f;
        private static float _keep = -1f;

        /// <summary>Where the water is calmed around in the loaded world, for the self-test.</summary>
        [ForTest]
        public static IEnumerable<Vector3> CalmingForTest => _calming;

        /// <summary>The places the water is calmed around now; the tiles are brought up to date when they changed.</summary>
        public static void CalmAround(List<Vector3> places)
        {
            var same = places.Count == _calming.Count && places.All(p => _calming.Any(c => Vector3.Distance(c, p) < Moved));
            if (same) return;
            _calming = places;
            _changed = true;
        }

        /// <summary>A tile has read its corners; it is calmed at once if a stand reaches it.</summary>
        public static void TileStarted(WaterVolume tile)
        {
            Started.Add(tile);
            _changed = true;
        }

        /// <summary>Called every frame: brings the tiles up to date once the calming places, the tiles or the settings changed.</summary>
        public static void Tick()
        {
            var rules = PlayRules.Now;
            if (!Mathf.Approximately(rules.CalmReach, _reach) || !Mathf.Approximately(rules.CalmKeep, _keep))
            {
                _reach = rules.CalmReach;
                _keep = rules.CalmKeep;
                _changed = true;
            }
            if (!_changed) return;
            _changed = false;
            Apply(rules);
        }

        private static void Apply(FishingRules rules)
        {
            Started.RemoveWhere(t => t == null);
            foreach (var gone in Own.Keys.Where(t => t == null).ToList()) Own.Remove(gone);
            var places = _calming.Select(p => new Spot(p.x, p.y, p.z)).ToList();

            foreach (var tile in Started)
            {
                if (tile.m_heightmap == null) continue;
                var c = tile.transform.position;
                var centre = new Spot(c.x, c.y, c.z);
                var near = places.Where(p => p.Across(centre) <= rules.CalmReach + TileReach).ToList();
                if (near.Count == 0)
                {
                    if (Own.TryGetValue(tile, out var own))
                    {
                        Write(tile, own.Corners, own.One);
                        Own.Remove(tile);
                    }
                    continue;
                }
                if (!Own.TryGetValue(tile, out var kept))
                {
                    kept = ((float[])Corners(tile).Clone(), OneDepth(tile));
                    Own[tile] = kept;
                }
                var calmed = CalmWater.Calmed(kept.Corners, centre, near, rules.CalmReach, rules.CalmKeep);
                Write(tile, calmed, calmed.All(v => Mathf.Approximately(v, calmed[0])) ? calmed[0] : -1f);
            }
        }

        /// <summary>Gives a tile its corner values, and the one value it uses when they are all alike, and hands them to its drawing.</summary>
        private static void Write(WaterVolume tile, float[] corners, float one)
        {
            var target = Corners(tile);
            for (var i = 0; i < target.Length && i < corners.Length; i++) target[i] = corners[i];
            OneDepth(tile) = one;
            SetupMaterial?.Invoke(tile, null);
        }

        /// <summary>Lets go of everything on leaving a world.</summary>
        public static void Forget()
        {
            _calming = new List<Vector3>();
            Started.Clear();
            Own.Clear();
        }
    }
}
