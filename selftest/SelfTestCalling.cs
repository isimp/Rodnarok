using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// What comes to a float besides a school. A fish of the world's own that takes the bait near
    /// the float is called to it and nothing is spawned: a Perch is put in the water a few metres
    /// off, with no float's mark, and must be the one called, take the bait, and leave no school
    /// behind. A second cast right after a school was left must wait for that school to be gone
    /// before its own comes. Needs a character beside water at least 3 m deep; the rod, the floats,
    /// the Perch put there and the schools are taken away again.
    /// </summary>
    internal static class SelfTestCalling
    {
        private const string Bait = "FishingBait";
        private const string WorldFishPrefab = "Fish1";

        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AWorldFishNearTheFloatIsCalledAndNoSchoolIsSpawned", WorldFishCalled);
            yield return new Scenario("ANewSchoolWaitsUntilTheLastHasLeft", NewSchoolWaits);
        }

        private static IEnumerable WorldFishCalled(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            var fishPrefab = ZNetScene.instance.GetPrefab(WorldFishPrefab);
            check.That(floatPrefab != null && baitPrefab != null && fishPrefab != null, "the float, the bait and the Perch prefabs are there");
            if (floatPrefab == null || baitPrefab == null || fishPrefab == null) yield break;
            var near = DeepNear(spot.Value, 3f);
            check.That(near.HasValue, "water at least 3 m deep 5 to 9 m from the float, reached through deep water, for the Perch");
            if (!near.HasValue) yield break;

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            GameObject perch = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // A Perch of the world's own in the water a few metres off: no float's mark on it.
                perch = Object.Instantiate(fishPrefab, near.Value, Quaternion.identity);
                yield return null;
                var put = perch.GetComponent<Fish>();
                check.That(put != null && Schools.FloatOf(put).IsNone(), "the Perch put in the water carries no float's mark");

                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: true);
                var called = Schools.CalledForTest;
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                Fish calledFish = null;
                for (var waited = 0f; waited < 10f && calledFish == null; waited += Time.deltaTime)
                {
                    calledFish = Schools.CalledFishForTest(ff);
                    yield return null;
                }
                check.That(Schools.CalledForTest == called + 1 && calledFish != null, "a fish of the world's own was called to the float");
                if (calledFish == null) yield break;
                check.That(calledFish.m_baits.Any(b => b.m_bait != null && b.m_bait.name == Bait), "the called fish takes Fishing Bait");
                check.That(calledFish == put, "the Perch put a few metres off was the one called" + (calledFish == put ? "" : ", another nearer one was: " + Utils.GetPrefabName(calledFish.gameObject)));

                var bit = false;
                for (var waited = 0f; waited < 45f && !bit; waited += Time.deltaTime)
                {
                    bit = Nibbler(ff) == calledFish || ff.GetCatch() == calledFish;
                    yield return null;
                }
                check.That(bit, "the called fish took the bait within 45 s");
                check.That(SelfTestArrival.SchoolOf(floatId).Count == 0, "no school was spawned for the float");
            }
            finally
            {
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                if (perch != null) ZNetScene.instance.Destroy(perch);
                gear.PutBack();
            }
        }

        private static IEnumerable NewSchoolWaits(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            if (floatPrefab == null || baitPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false);

                // A first cast brings a school; the float is taken in before a bite, and the school leaves.
                ff = Cast(player, floatPrefab, baitPrefab, spot.Value);
                var firstId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                var first = new List<Fish>();
                for (var waited = 0f; waited < 15f && first.Count == 0; waited += Time.deltaTime)
                {
                    first = SelfTestArrival.SchoolOf(firstId);
                    yield return null;
                }
                check.That(first.Count > 0, "the first cast brought a school within 15 s");
                if (first.Count == 0) yield break;
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                for (var waited = 0f; waited < 0.5f; waited += Time.deltaTime) yield return null;
                check.That(Schools.LeavingForTest > 0, "the first school is leaving");

                // A second cast at once: no school of its own while the first still leaves.
                ff = Cast(player, floatPrefab, baitPrefab, spot.Value);
                var secondId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                var early = false;
                var leftAfter = -1f;
                var started = Time.time;
                for (var waited = 0f; waited < 50f; waited += Time.deltaTime)
                {
                    var school = SelfTestArrival.SchoolOf(secondId);
                    if (school.Count > 0)
                    {
                        early = Schools.LeavingForTest > 0;
                        break;
                    }
                    if (leftAfter < 0f && Schools.LeavingForTest == 0) leftAfter = Time.time - started;
                    yield return null;
                }
                var came = SelfTestArrival.SchoolOf(secondId).Count > 0;
                check.That(!early, "no school came for the second cast while the first was still leaving");
                check.That(leftAfter >= 0f, "the first school was gone within 50 s, the 30 s backstop included");
                check.That(came, "once the first school was gone, the second cast's school came, " + (leftAfter < 0f ? "never gone" : "gone after " + leftAfter.ToString("0.0", CultureInfo.InvariantCulture) + " s"));
            }
            finally
            {
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        private static FishingFloat Cast(Player player, GameObject floatPrefab, GameObject baitPrefab, Vector3 at)
        {
            var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
            bait.m_dropPrefab = baitPrefab;
            var ff = Object.Instantiate(floatPrefab, at, Quaternion.identity).GetComponent<FishingFloat>();
            ff.Setup(player, Vector3.zero, 0f, null, null, bait);
            return ff;
        }

        /// <summary>A place 5 to 9 m from the float, at least this deep and as deep every metre of the way to the float, 2 m under the surface; none where there is none.</summary>
        private static Vector3? DeepNear(Vector3 at, float depth)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            for (var distance = 5f; distance <= 9f; distance += 2f)
            {
                for (var step = 0; step < 16; step++)
                {
                    var dir = new Vector3(Mathf.Cos(step * Mathf.PI / 8f), 0f, Mathf.Sin(step * Mathf.PI / 8f));
                    var clear = true;
                    for (var d = 1f; d <= distance && clear; d += 1f)
                        clear = ZoneSystem.instance.GetSolidHeight(at + dir * d, out var ground) && water - ground >= depth;
                    if (clear) return new Vector3(at.x + dir.x * distance, water - 2f, at.z + dir.z * distance);
                }
            }
            return null;
        }
    }
}
