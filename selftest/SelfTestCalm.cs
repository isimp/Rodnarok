using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The serpent post as a player builds it: it is on the hammer for the darkwood pole's cost
    /// and a Serpent trophy, and a post standing beside the water calms the water tiles' corners
    /// within its reach to their share, alike on every tile meeting at a corner, so the waves seen
    /// and the waves floated on fall together; taken down, every corner is as the game made it.
    /// Needs a character beside water; the post is taken down again.
    /// </summary>
    internal static class SelfTestCalm
    {
        private static readonly AccessTools.FieldRef<WaterVolume, float[]> Corners = AccessTools.FieldRefAccess<WaterVolume, float[]>("m_normalizedDepth");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ASerpentPostCalmsTheWaterNearItAndOnlyThere", PostCalms);
        }

        private static IEnumerable PostCalms(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var post = ZNetScene.instance.GetPrefab(SerpentPost.PrefabName);
            check.That(post != null, "the serpent post is a prefab of the game");
            if (post == null) yield break;
            var hammer = ObjectDB.instance.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
            check.That(hammer != null && hammer.m_pieces.Contains(post), "the serpent post is on the hammer");
            var cost = post.GetComponent<Piece>().m_resources.Where(r => r.m_resItem != null).ToDictionary(r => r.m_resItem.name, r => r.m_amount);
            var pole = ZNetScene.instance.GetPrefab("darkwood_pole").GetComponent<Piece>().m_resources.Where(r => r.m_resItem != null).ToList();
            check.That(cost.TryGetValue("TrophySerpent", out var trophies) && trophies == 1, "it costs a Serpent trophy");
            foreach (var r in pole) check.That(cost.TryGetValue(r.m_resItem.name, out var n) && n == r.m_amount, "it costs the darkwood pole's " + r.m_amount.ToString(CultureInfo.InvariantCulture) + " " + r.m_resItem.name);

            var water = SelfTestArrival.DeepWaterBeside(player.transform.position, 1.5f);
            check.That(water.HasValue, "water within 6 to 20 m of the character");
            if (!water.HasValue) yield break;

            var rules = PlayRules.Now;
            var tiles = WaterVolume.Instances.Where(w => w != null && w.m_heightmap != null).ToList();
            var before = tiles.ToDictionary(w => w, w => (float[])Corners(w).Clone());
            var waves = new List<float>();
            for (var waited = 0f; waited < 2f; waited += Time.deltaTime)
            {
                waves.Add(SurfaceAt(water.Value));
                yield return null;
            }
            var wavesBefore = StdDev(waves);
            SelfTestPictures.Take("water-before-post");
            yield return null;

            GameObject placed = null;
            try
            {
                // On dry land beside the character, where a player would build it, out of the camera's way.
                var at = SelfTestPlaces.DryLandBeside(player.transform.position) ?? player.transform.position + FlatForward(player) * 3f;
                placed = Object.Instantiate(post, at, Quaternion.identity);
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;

                // The corners within reach keep their share, those beyond are as they were.
                var postSpot = new Spot(at.x, at.y, at.z);
                var wrong = new List<string>();
                foreach (var tile in tiles.Where(t => t != null))
                {
                    var centre = tile.transform.position;
                    var expected = CalmWater.Calmed(before[tile], new Spot(centre.x, centre.y, centre.z), new[] { postSpot }, rules.CalmReach, rules.CalmKeep);
                    var now = Corners(tile);
                    for (var i = 0; i < 4; i++)
                        if (Mathf.Abs(now[i] - expected[i]) > 0.001f) wrong.Add(centre.x.ToString("0", CultureInfo.InvariantCulture) + "," + centre.z.ToString("0", CultureInfo.InvariantCulture) + " corner " + i.ToString(CultureInfo.InvariantCulture));
                }
                check.That(wrong.Count == 0, "every loaded tile's corners are calmed as the post's reach says; off: " + string.Join("; ", wrong.Take(8)));
                check.That(tiles.Any(t => t != null && Corners(t).Where((v, i) => v < before[t][i] - 0.001f).Any()), "the post calmed at least one corner of the water beside it");

                // What floats rides lower waves there; where the water was still to begin with there is nothing to compare.
                waves.Clear();
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime)
                {
                    waves.Add(SurfaceAt(water.Value));
                    yield return null;
                }
                var wavesAfter = StdDev(waves);
                if (wavesBefore > 0.05f) check.That(wavesAfter < wavesBefore * 0.5f, "the waves beside the post fell from " + wavesBefore.ToString("0.00", CultureInfo.InvariantCulture) + " m to " + wavesAfter.ToString("0.00", CultureInfo.InvariantCulture) + " m");
                SelfTestPictures.Take("water-with-post");
                yield return null;
            }
            finally
            {
                if (placed != null) ZNetScene.instance.Destroy(placed);
            }

            // Taken down, every corner is as the game made it.
            for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
            var left = tiles.Where(t => t != null && !Corners(t).SequenceEqual(before[t])).ToList();
            check.That(left.Count == 0, left.Count.ToString(CultureInfo.InvariantCulture) + " tiles kept calmed corners after the post was taken down");
        }

        /// <summary>The water surface the game floats things on at a place, waves included.</summary>
        private static float SurfaceAt(Vector3 place)
        {
            foreach (var tile in WaterVolume.Instances)
            {
                if (tile == null) continue;
                var c = tile.transform.position;
                if (Mathf.Abs(place.x - c.x) <= CalmWater.Tile / 2f && Mathf.Abs(place.z - c.z) <= CalmWater.Tile / 2f) return tile.GetWaterSurface(place);
            }
            return place.y;
        }

        private static float StdDev(List<float> values)
        {
            if (values.Count < 2) return 0f;
            var mean = values.Average();
            return Mathf.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
        }

        private static Vector3 FlatForward(Player player)
        {
            var f = player.transform.forward;
            f.y = 0f;
            return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
        }
    }
}
