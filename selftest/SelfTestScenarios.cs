using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The self-test's scenarios. Each checks, in the running game, something Rodnarok relies on
    /// that no test outside the game can see: the game's fish and baits, its fishing float's
    /// numbers, and how its water tiles blend their corners.
    /// </summary>
    internal static class SelfTestScenarios
    {
        private static readonly AccessTools.FieldRef<WaterVolume, float[]> NormalizedDepth = AccessTools.FieldRefAccess<WaterVolume, float[]>("m_normalizedDepth");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("EveryFishOfTheGameComesToABaitTheRodCasts", EveryFishComesToABait);
            yield return new Scenario("TheFishingFloatReelsAsTheRulesAssume", FloatAsTheRulesAssume);
            yield return new Scenario("WaterTilesBlendTheirCornersInTheModelsOrder", WaterCornersInOrder);
            foreach (var scenario in SelfTestArrival.All()) yield return scenario;
            foreach (var scenario in SelfTestBite.All()) yield return scenario;
            foreach (var scenario in SelfTestCalm.All()) yield return scenario;
        }

        /// <summary>Every fish prefab takes some bait, the bait table built from them reaches every one, and each bait is ammunition the fishing rod casts.</summary>
        private static IEnumerable EveryFishComesToABait(ScenarioCheck check)
        {
            var fish = ZNetScene.instance.m_prefabs.Where(p => p != null && p.GetComponent<Fish>() != null).ToList();
            check.That(fish.Count >= 12, "the game has at least the 12 fish of 1.0.20, found " + fish.Count.ToString(CultureInfo.InvariantCulture));
            var kinds = fish.Select(p => new FishKind(p.name, p.GetComponent<Fish>().m_baits.Where(b => b.m_bait != null).Select(b => b.m_bait.name).ToList())).ToList();
            foreach (var kind in kinds) check.That(kind.Baits.Count > 0, kind.Prefab + " takes some bait");
            var table = new BaitTable(kinds);
            var reached = new HashSet<string>(table.Baits.SelectMany(table.FishFor));
            foreach (var kind in kinds) check.That(reached.Contains(kind.Prefab), kind.Prefab + " comes to some bait");

            var rod = ZNetScene.instance.GetPrefab("FishingRod")?.GetComponent<ItemDrop>();
            check.That(rod != null, "the fishing rod prefab is there");
            var ammo = rod?.m_itemData.m_shared.m_ammoType;
            foreach (var bait in table.Baits)
            {
                var item = ZNetScene.instance.GetPrefab(bait)?.GetComponent<ItemDrop>();
                check.That(item != null && item.m_itemData.m_shared.m_ammoType == ammo, bait + " is bait the rod casts");
            }
            yield return null;
        }

        /// <summary>The game's float reels at the speeds and skill share the rules take as the game's.</summary>
        private static IEnumerable FloatAsTheRulesAssume(ScenarioCheck check)
        {
            var rules = FishingRules.Default;
            var prefab = ZNetScene.instance.m_prefabs.FirstOrDefault(p => p != null && p.GetComponent<FishingFloat>() != null);
            check.That(prefab != null, "a fishing float prefab is there");
            if (prefab == null) yield break;
            var ff = prefab.GetComponent<FishingFloat>();
            check.That(Mathf.Approximately(ff.m_pullLineSpeed, rules.ReelSpeed), "reel speed at Fishing 0 is " + rules.ReelSpeed.ToString(CultureInfo.InvariantCulture) + ", the game has " + ff.m_pullLineSpeed.ToString(CultureInfo.InvariantCulture));
            check.That(Mathf.Approximately(ff.m_pullLineSpeedMaxSkill, rules.ReelSpeedMaxSkill), "reel speed at Fishing 100 is " + rules.ReelSpeedMaxSkill.ToString(CultureInfo.InvariantCulture) + ", the game has " + ff.m_pullLineSpeedMaxSkill.ToString(CultureInfo.InvariantCulture));
            check.That(Mathf.Approximately(ff.m_pullStaminaUseMaxSkillMultiplier, rules.SkillCostShare), "the cost share at Fishing 100 is " + rules.SkillCostShare.ToString(CultureInfo.InvariantCulture) + ", the game has " + ff.m_pullStaminaUseMaxSkillMultiplier.ToString(CultureInfo.InvariantCulture));
            yield return null;
        }

        /// <summary>
        /// On a loaded water tile whose corners differ, the game's own depth read just inside each
        /// corner the model names gives that corner's value, so the calm-water piece lowers the
        /// corner it means to.
        /// </summary>
        private static IEnumerable WaterCornersInOrder(ScenarioCheck check)
        {
            var tile = WaterVolume.Instances.FirstOrDefault(w => w != null && w.m_heightmap != null && Varied(NormalizedDepth(w)));
            check.That(tile != null, "a loaded water tile with corners of different depth (stand near a shore)");
            if (tile == null) yield break;
            var depth = NormalizedDepth(tile);
            var centre = tile.transform.position;
            var corners = CalmWater.Corners(new Spot(centre.x, centre.y, centre.z));
            for (var i = 0; i < corners.Length; i++)
            {
                // A tenth of a metre inside the corner, toward the centre.
                var inside = new Vector3(corners[i].X + Mathf.Sign(centre.x - corners[i].X) * 0.1f, centre.y, corners[i].Z + Mathf.Sign(centre.z - corners[i].Z) * 0.1f);
                var read = tile.Depth(inside);
                check.That(Mathf.Abs(read - depth[i]) < 0.01f, "corner " + i.ToString(CultureInfo.InvariantCulture) + " reads " + depth[i].ToString("0.000", CultureInfo.InvariantCulture) + ", the game blends " + read.ToString("0.000", CultureInfo.InvariantCulture) + " there");
            }
            yield return null;
        }

        private static bool Varied(float[] depth) => depth != null && depth.Length == 4 && depth.Any(d => Mathf.Abs(d - depth[0]) > 0.05f);
    }
}
