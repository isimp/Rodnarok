using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The arrival as a player meets it: a float with Fishing Bait lands in deep water beside the
    /// character, and the school the bait draws must come, match the bait and the world's group
    /// size, wear its stars as the game sizes them, have one of it take the bait, scatter when
    /// that one is hooked, and leave once the float is gone, while no fish of the world's own is
    /// taken away. Needs a character beside water at least 3.5 m deep. A fishing rod is put in
    /// its hands for the run (<see cref="SelfTestGear"/>) and its gear put back after; the float,
    /// the hook and everything the school was are taken away again.
    /// </summary>
    internal static class SelfTestArrival
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        /// <summary>The fish's and the float's methods whose working the arrival relies on. A school is made by Rodnarök itself, not through the world's spawns, so the many mods that change those spawns are no matter here.</summary>
        private static readonly (Type Type, string Method)[] FishingMethods =
        {
            (typeof(Fish), "FindFloat"),
            (typeof(Fish), "TestBate"),
            (typeof(Fish), "RandomizeWaypoint"),
            (typeof(Fish), nameof(Fish.CustomFixedUpdate)),
            (typeof(Fish), nameof(Fish.OnHooked)),
            (typeof(FishingFloat), "RPC_Nibble"),
            (typeof(FishingFloat), "TryToHook"),
            (typeof(FishingFloat), "FixedUpdate"),
        };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("NoOtherModChangesHowFishBite", NoOtherFishingPatches);
            yield return new Scenario("SchoolSizesComeFromTheWorldsSpawns", GroupsFromTheWorld);
            yield return new Scenario("ABaitDrawsASchoolThatTakesItScattersAndLeaves", SchoolComesAndGoes);
        }

        /// <summary>No other mod patches the methods the arrival relies on; one that does is named with them, since a result taken with it says little about Rodnarök.</summary>
        private static IEnumerable NoOtherFishingPatches(ScenarioCheck check)
        {
            foreach (var (type, name) in FishingMethods)
            {
                var method = AccessTools.Method(type, name);
                check.That(method != null, type.Name + "." + name + " is there");
                if (method == null) continue;
                var info = Harmony.GetPatchInfo(method);
                var others = info?.Owners.Where(o => o != About.Guid).ToList() ?? new List<string>();
                check.That(others.Count == 0, type.Name + "." + name + " is also patched by " + string.Join(", ", others));
            }
            yield return null;
        }

        /// <summary>Every fish the world spawns has its group size read from its spawn entry, and the cave fish, which the world never spawns in groups, has none.</summary>
        private static IEnumerable GroupsFromTheWorld(ScenarioCheck check)
        {
            var book = FishBook.Current;
            check.That(book != null, "the fish book is read in a world");
            if (book == null) yield break;
            foreach (var prefab in new[] { "Fish1", "Fish2", "Fish3", "Fish5", "Fish6", "Fish7", "Fish8", "Fish9", "Fish10", "Fish11", "Fish12" })
            {
                var group = book.Group(prefab);
                check.That(group.Known && group.Min >= 1 && group.Max >= group.Min, prefab + " has a group size from its spawn entry, read " + group.Min.ToString(CultureInfo.InvariantCulture) + "-" + group.Max.ToString(CultureInfo.InvariantCulture));
            }
            check.That(book.Group("Fish1").Min == 2 && book.Group("Fish1").Max == 6, "Perch come in twos to sixes as in 1.0.20");
            check.That(!book.Group("Fish4_cave").Known, "the cave fish has no group of its own");
            yield return null;
        }

        private static IEnumerable SchoolComesAndGoes(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = DeepWaterBeside(player.transform.position, 3.5f);
            check.That(spot.HasValue, "water at least 3.5 m deep within 6 to 14 m of the character (stand at a deep shore or on a dock)");
            if (!spot.HasValue) yield break;

            var wildBefore = WildFish().ToList();
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            check.That(floatPrefab != null && baitPrefab != null, "the float and the bait prefabs are there");
            if (floatPrefab == null || baitPrefab == null) yield break;

            // The game's float stays only while its caster holds a rod, so one is put in hand and everything put back after.
            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                var tipShown = false;
                for (var waited = 0f; waited < 3f && !tipShown; waited += Time.deltaTime)
                {
                    tipShown = Utils.FindChild(player.transform, "_RodTop") != null;
                    yield return null;
                }
                check.That(tipShown, "the rod in hand shows its tip within 3 s");
                if (!tipShown) yield break;

                Schools.RulesForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, leaveSeconds: 2f);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = UnityEngine.Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                // The school comes once the float lies in the water and the wait is over.
                List<Fish> school = null;
                for (var waited = 0f; waited < 15f; waited += Time.deltaTime)
                {
                    school = SchoolOf(floatId);
                    if (school.Count > 0) break;
                    yield return null;
                }
                check.That(school != null && school.Count > 0, "a school came within 15 s of the float landing");
                if (school == null || school.Count == 0) yield break;
                yield return null;
                school = SchoolOf(floatId);

                var kinds = school.Select(PrefabName).Distinct().ToList();
                check.That(kinds.Count == 1, "a school is one kind of fish, came " + string.Join(", ", kinds));
                var kind = kinds[0];
                check.That(FishBook.Current.Baits.FishFor(Bait).Contains(kind), kind + " is a fish Fishing Bait brings");
                var group = FishBook.Current.Group(kind);
                check.That(school.Count >= group.Min && school.Count <= group.Max, school.Count.ToString(CultureInfo.InvariantCulture) + " fish came, the world's groups of " + kind + " are " + group.Min.ToString(CultureInfo.InvariantCulture) + "-" + group.Max.ToString(CultureInfo.InvariantCulture));
                foreach (var fish in school)
                {
                    var quality = fish.GetComponent<ItemDrop>().m_itemData.m_quality;
                    var size = 1f + (quality - 1) * fish.GetComponent<ItemDrop>().m_itemData.m_shared.m_scaleByQuality;
                    check.That(quality >= 1 && quality <= 5 && Mathf.Abs(fish.transform.localScale.x - size) < 0.01f, "a fish of quality " + quality.ToString(CultureInfo.InvariantCulture) + " is drawn " + size.ToString("0.00", CultureInfo.InvariantCulture) + " times its size");
                }

                // It swims to the float and one of it takes the bait.
                var near = false;
                for (var waited = 0f; waited < 45f && !near; waited += Time.deltaTime)
                {
                    near = school.Any(f => f != null && Across(f.transform.position, ff.transform.position) < 6f);
                    yield return null;
                }
                check.That(near, "the school reached the float within 45 s");
                var nibbled = false;
                for (var waited = 0f; waited < 45f && !nibbled; waited += Time.deltaTime)
                {
                    // A float moving fast as the fish bites hooks it at once, as the game has it.
                    nibbled = NibbleTime(ff) > 0f && (school.Contains(Nibbler(ff)) || school.Contains(ff.GetCatch()));
                    yield return null;
                }
                check.That(nibbled, "a fish of the school took the bait within 45 s");
                if (!nibbled) yield break;

                // Hooked, the rest scatter.
                var biter = school.Contains(Nibbler(ff)) ? Nibbler(ff) : ff.GetCatch();
                var others = school.Where(f => f != null && f != biter).ToList();
                var before = others.Select(f => Across(f.transform.position, ff.transform.position)).ToList();
                biter.OnHooked(ff);
                for (var waited = 0f; waited < 4f; waited += Time.deltaTime) yield return null;
                if (others.Count > 0)
                {
                    var after = others.Select(f => f == null ? before.Max() + 10f : Across(f.transform.position, ff.transform.position)).ToList();
                    check.That(after.Average() > before.Average() + 3f, "the rest of the school scattered, from " + before.Average().ToString("0.0", CultureInfo.InvariantCulture) + " m to " + after.Average().ToString("0.0", CultureInfo.InvariantCulture) + " m from the float");
                }
                biter.OnHooked(null);

                // The float gone, the school leaves and is taken away.
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                var gone = false;
                for (var waited = 0f; waited < 10f && !gone; waited += Time.deltaTime)
                {
                    gone = SchoolOf(floatId).Count == 0;
                    yield return null;
                }
                check.That(gone, "the school was taken away within 10 s of the float going, leave time 2 s");
                var lost = wildBefore.Where(id => ZDOMan.instance.GetZDO(id) == null).ToList();
                check.That(lost.Count == 0, lost.Count.ToString(CultureInfo.InvariantCulture) + " fish of the world's own were taken away");
            }
            finally
            {
                Schools.RulesForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        /// <summary>The fish marked as drawn to this float, in the loaded world.</summary>
        private static List<Fish> SchoolOf(ZDOID floatId) =>
            Fish.Instances.OfType<Fish>().Where(f => f != null && Schools.FloatOf(f) == floatId).ToList();

        /// <summary>The fish of the world's own in the loaded world, by their ids.</summary>
        private static IEnumerable<ZDOID> WildFish() =>
            Fish.Instances.OfType<Fish>().Where(f => f != null && Schools.FloatOf(f).IsNone()).Select(f => f.GetZDOID());

        private static string PrefabName(Fish fish) => Utils.GetPrefabName(fish.gameObject);

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>A place on the water 6 to 14 m from the character where the water is at least this deep.</summary>
        private static Vector3? DeepWaterBeside(Vector3 from, float depth)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            for (var distance = 6f; distance <= 14f; distance += 2f)
            {
                for (var step = 0; step < 16; step++)
                {
                    var angle = step * Mathf.PI / 8f;
                    var p = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (ZoneSystem.instance.GetSolidHeight(p, out var ground) && water - ground >= depth) return new Vector3(p.x, water + 0.3f, p.z);
                }
            }
            return null;
        }
    }
}
