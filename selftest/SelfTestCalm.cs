using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A Serpent trophy on one of the game's item stands, as a player does it: the stand is built
    /// on dry land beside the water, the trophy put up from the character's inventory, and the
    /// stand tapped with the use key. Put up, the trophy calms nothing; tapped, the stand calms the
    /// water tiles' corners within its reach to their share, alike on every tile meeting at a
    /// corner, so the waves seen and the waves floated on fall together; the trophy taken off with
    /// the held use key, the calm switches off and every corner is as the game made it. Needs a
    /// character beside water; the stand and the dropped trophy are taken away again.
    /// </summary>
    internal static class SelfTestCalm
    {
        /// <summary>The size of the game's water tiles, in metres: a stand calms the corners of the tiles its reach touches.</summary>
        private const float TileReach = 64f;

        private const string StandPrefab = "itemstandh";

        private static readonly AccessTools.FieldRef<WaterVolume, float[]> Corners = AccessTools.FieldRefAccess<WaterVolume, float[]>("m_normalizedDepth");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ASerpentTrophyOnAStandCalmsTheWaterNearItOnceSwitchedOn", StandCalms);
        }

        private static IEnumerable StandCalms(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var standPrefab = ZNetScene.instance.GetPrefab(StandPrefab);
            var trophyPrefab = ObjectDB.instance.GetItemPrefab(CalmStand.Trophy);
            check.That(standPrefab != null && standPrefab.GetComponentInChildren<ItemStand>() != null, "the game's horizontal item stand is a prefab of the game");
            check.That(trophyPrefab != null, "the Serpent trophy is an item of the game");
            if (standPrefab == null || trophyPrefab == null) yield break;
            NoteOtherMods(check);

            var water = SelfTestArrival.DeepWaterBeside(player.transform.position, 1.5f);
            check.That(water.HasValue, "water within 6 to 20 m of the character");
            if (!water.HasValue) yield break;

            var rules = PlayRules.Now;

            // A stand already calming where it reaches the water here leaves nothing for the test's
            // stand to show: its calm is measured only where no other reaches the tiles around it.
            var near = CalmWaters.CalmingForTest.Where(p => Vector3.Distance(p, water.Value) < rules.CalmReach + 2f * TileReach).ToList();
            if (near.Count > 0)
            {
                check.Note("a stand already calms the water " + near.Min(p => Vector3.Distance(p, water.Value)).ToString("0", CultureInfo.InvariantCulture)
                    + " m from it here, so the calm was not measured; run it where no stand calms within "
                    + (rules.CalmReach + 2f * TileReach).ToString("0", CultureInfo.InvariantCulture) + " m");
                yield break;
            }

            var tiles = WaterVolume.Instances.Where(w => w != null && w.m_heightmap != null).ToList();
            var before = tiles.ToDictionary(w => w, w => (float[])Corners(w).Clone());
            var waves = new List<float>();
            for (var waited = 0f; waited < 2f; waited += Time.deltaTime)
            {
                waves.Add(SurfaceAt(water.Value));
                yield return null;
            }
            var wavesBefore = StdDev(waves);
            SelfTestPictures.Take("water-before-stand");
            yield return null;

            GameObject placed = null;
            var at = SelfTestPlaces.DryLandBeside(player.transform.position) ?? player.transform.position + FlatForward(player) * 3f;
            try
            {
                // Built on dry land beside the character, where a player would put it, out of the camera's way.
                placed = Object.Instantiate(standPrefab, at, Quaternion.identity);
                for (var waited = 0f; waited < 0.5f; waited += Time.deltaTime) yield return null;
                var stand = placed.GetComponentInChildren<ItemStand>();

                // The trophy put up from the inventory, as a player puts one up.
                var inventory = player.GetInventory();
                var trophyName = trophyPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
                var had = inventory.CountItems(trophyName);
                var trophy = inventory.AddItem(trophyPrefab, 1) ? inventory.GetItem(trophyName) : null;
                var put = trophy != null && stand.UseItem(player, trophy);
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(put && stand.HaveAttachment(), "the Serpent trophy goes up on the stand; put up " + put);
                if (!stand.HaveAttachment())
                {
                    if (inventory.CountItems(trophyName) > had) inventory.RemoveItem(trophyName, 1);
                    yield break;
                }
                check.That(stand.GetHoverText().Contains("Calm the waters"), "the stand offers to calm the waters: " + stand.GetHoverText().Replace("\n", " | "));
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(!CalmWaters.CalmingForTest.Any(), "put up, the trophy calms nothing yet");
                check.That(tiles.All(t => t == null || Corners(t).SequenceEqual(before[t])), "put up, the trophy leaves every corner as the game made it");

                // Tapped with the use key, as a player taps it.
                var took = stand.Interact(player, false, false);
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
                check.That(took && CalmStands.SwitchedOnForTest(stand), "a tap of the use key switches the stand's calm on; the tap taken " + took);
                check.That(stand.GetHoverText().Contains("Calming the waters"), "the stand says it calms the waters: " + stand.GetHoverText().Replace("\n", " | "));

                // The corners within reach keep their share, those beyond are as they were.
                var standSpot = new Spot(stand.transform.position.x, stand.transform.position.y, stand.transform.position.z);
                var wrong = new List<string>();
                foreach (var tile in tiles.Where(t => t != null))
                {
                    var centre = tile.transform.position;
                    var expected = CalmWater.Calmed(before[tile], new Spot(centre.x, centre.y, centre.z), new[] { standSpot }, rules.CalmReach, rules.CalmKeep);
                    var now = Corners(tile);
                    for (var i = 0; i < 4; i++)
                        if (Mathf.Abs(now[i] - expected[i]) > 0.001f) wrong.Add(centre.x.ToString("0", CultureInfo.InvariantCulture) + "," + centre.z.ToString("0", CultureInfo.InvariantCulture) + " corner " + i.ToString(CultureInfo.InvariantCulture));
                }
                check.That(wrong.Count == 0, "every loaded tile's corners are calmed as the stand's reach says; off: " + string.Join("; ", wrong.Take(8)));
                check.That(tiles.Any(t => t != null && Corners(t).Where((v, i) => v < before[t][i] - 0.001f).Any()), "the stand calmed at least one corner of the water beside it, "
                    + Vector3.Distance(stand.transform.position, water.Value).ToString("0", CultureInfo.InvariantCulture) + " m off");

                // What floats rides lower waves there; where the water was still to begin with there is nothing to compare.
                waves.Clear();
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime)
                {
                    waves.Add(SurfaceAt(water.Value));
                    yield return null;
                }
                var wavesAfter = StdDev(waves);
                // Compared only where the calm reaches the measured place strongly: the tiles' corners lie
                // 64 m apart, so a place a few metres off the stand may lie mostly between corners beyond its reach.
                var leftHere = ShareLeftAt(water.Value, before);
                if (wavesBefore > 0.05f && leftHere <= StrongCalm)
                    check.That(wavesAfter < wavesBefore * 0.5f, "the waves beside the stand fell from " + wavesBefore.ToString("0.00", CultureInfo.InvariantCulture) + " m to " + wavesAfter.ToString("0.00", CultureInfo.InvariantCulture)
                        + " m, the calm leaving " + (leftHere * 100f).ToString("0", CultureInfo.InvariantCulture) + "% of them there");
                else if (wavesBefore > 0.05f)
                    check.Note("the calm leaves " + (leftHere * 100f).ToString("0", CultureInfo.InvariantCulture) + "% of the waves where they are measured, too much to tell apart from the wind's own change; waves "
                        + wavesBefore.ToString("0.00", CultureInfo.InvariantCulture) + " m before, " + wavesAfter.ToString("0.00", CultureInfo.InvariantCulture) + " m after");
                SelfTestPictures.Take("water-with-stand");
                yield return null;

                // The trophy taken off with the held use key, as a player takes it.
                stand.Interact(player, true, false);
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
                check.That(!stand.HaveAttachment(), "holding the use key still takes the trophy off");
                check.That(!CalmStands.SwitchedOnForTest(stand), "with the trophy taken, the stand's calm is switched off, so the next trophy put up starts off");
                check.That(!CalmWaters.CalmingForTest.Any(), "with the trophy taken, nothing calms the water");
            }
            finally
            {
                foreach (var drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
                {
                    if (drop != null && drop.name.StartsWith(CalmStand.Trophy, System.StringComparison.Ordinal) && Vector3.Distance(drop.transform.position, at) < 4f)
                        ZNetScene.instance.Destroy(drop.gameObject);
                }
                if (placed != null) ZNetScene.instance.Destroy(placed);
            }

            // Taken down, every corner is as the game made it.
            for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
            var left = tiles.Where(t => t != null && !Corners(t).SequenceEqual(before[t])).ToList();
            check.That(left.Count == 0, left.Count.ToString(CultureInfo.InvariantCulture) + " tiles kept calmed corners after the trophy was taken");
        }

        /// <summary>Names the other mods that change how an item stand takes a key or tells what it holds, as they may meet the calm's tap and lines.</summary>
        private static void NoteOtherMods(ScenarioCheck check)
        {
            var others = new[] { AccessTools.Method(typeof(ItemStand), nameof(ItemStand.Interact)), AccessTools.Method(typeof(ItemStand), nameof(ItemStand.GetHoverText)) }
                .Select(m => (m, Harmony.GetPatchInfo(m)))
                .Where(p => p.Item2 != null)
                .SelectMany(p => p.Item2.Owners.Where(o => o != About.Guid).Select(o => "ItemStand." + p.m.Name + " by " + o))
                .ToList();
            if (others.Count > 0) check.Note("other mods change the item stand too: " + string.Join("; ", others));
        }

        /// <summary>The water surface the game floats things on at a place, waves included.</summary>
        /// <summary>At most how much of its waves the calm may leave at the measured place for them to be compared.</summary>
        private const float StrongCalm = 0.35f;

        /// <summary>
        /// The share of its own waves the calm leaves at a place: the corner values of its tile now
        /// against before, blended across the tile as the game blends them; the waves there follow
        /// that blend.
        /// </summary>
        private static float ShareLeftAt(Vector3 place, Dictionary<WaterVolume, float[]> before)
        {
            foreach (var pair in before)
            {
                var tile = pair.Key;
                if (tile == null) continue;
                var c = tile.transform.position;
                if (Mathf.Abs(place.x - c.x) > CalmWater.Tile / 2f || Mathf.Abs(place.z - c.z) > CalmWater.Tile / 2f) continue;
                var corners = CalmWater.Corners(new Spot(c.x, c.y, c.z));
                var now = Corners(tile);
                float after = 0f, was = 0f;
                for (var i = 0; i < 4 && i < corners.Length; i++)
                {
                    var weight = (1f - Mathf.Abs(place.x - corners[i].X) / CalmWater.Tile) * (1f - Mathf.Abs(place.z - corners[i].Z) / CalmWater.Tile);
                    after += weight * now[i];
                    was += weight * pair.Value[i];
                }
                return was > 0f ? after / was : 1f;
            }
            return 1f;
        }

        internal static float SurfaceAt(Vector3 place)
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
