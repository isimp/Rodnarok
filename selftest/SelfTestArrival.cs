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
    /// size, wear its stars as the game sizes them, drift in loose and gather at the float only
    /// after a while, have one of it take the bait only then, scatter when
    /// that one is hooked, and leave once the float is gone, while no fish of the world's own is
    /// taken away. Needs a character beside water at least 3 m deep. A fishing rod is put in
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
            var spot = DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;

            var wildBefore = WildFish().ToList();
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            check.That(floatPrefab != null && baitPrefab != null, "the float and the bait prefabs are there");
            if (floatPrefab == null || baitPrefab == null) yield break;

            // The game's float stays only while its caster holds a rod, so one is put in hand and everything put back after.
            var landedBefore = Schools.LandedForTest;
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

                // The school of a scenario before may still be leaving, and a new one waits for it.
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, baitLost: false);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = UnityEngine.Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                // The school appears the moment the float lies in the water, out of sight of the character.
                List<Fish> school = null;
                for (var waited = 0f; waited < 15f; waited += Time.deltaTime)
                {
                    school = SchoolOf(floatId);
                    if (school.Count > 0) break;
                    yield return null;
                }
                check.That(school != null && school.Count > 0, "a school came within 15 s of the float landing");
                if (school == null || school.Count == 0) yield break;
                var nowRules = PlayRules.Now;
                var appearedInSight = school.Where(f => f != null
                    && !Arrival.StartOutOfSight(Vector3.Distance(f.transform.position, player.transform.position) + 0.5f, FishWater(f) - f.transform.position.y + 0.3f, nowRules)).ToList();
                check.That(appearedInSight.Count == 0, "every fish of the school appeared out of sight, 40 m from the character or 4 m under the surface; "
                    + string.Join(", ", appearedInSight.Select(f => Vector3.Distance(f.transform.position, player.transform.position).ToString("0", CultureInfo.InvariantCulture) + " m off, "
                        + (FishWater(f) - f.transform.position.y).ToString("0.0", CultureInfo.InvariantCulture) + " m under")));
                var swim = Schools.SwimSecondsForTest(ff);
                check.That(swim >= nowRules.WaitMin, "the school swims in over at least the shortest wait, " + swim.ToString("0.0", CultureInfo.InvariantCulture) + " s");
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

                // It drifts in loose, taking its time, and gathers at the float only at the end.
                var cameAt = Time.time;
                var spreads = new List<float>();
                var halfNearAfter = -1f;
                var nibbledEarly = false;
                var pictured = false;
                for (var waited = 0f; waited < swim + 30f && halfNearAfter < 0f; waited += Time.deltaTime)
                {
                    var here = school.Where(f => f != null).ToList();
                    var sinceCame = Time.time - cameAt;
                    if (here.Count > 1 && sinceCame < swim * 0.75f) spreads.Add(Spread(here));
                    if (here.Count(f => Across(f.transform.position, ff.transform.position) < 6f) * 2 >= here.Count) halfNearAfter = sinceCame;
                    if (sinceCame < swim && NibbleTime(ff) > 0f && school.Contains(Nibbler(ff))) nibbledEarly = true;
                    if (!pictured && sinceCame >= swim * 0.4f)
                    {
                        pictured = true;
                        SelfTestPictures.Take("arrival-drifting");
                    }
                    yield return null;
                }
                check.That(halfNearAfter >= 0f, "half the school reached the float within " + (swim + 30f).ToString("0", CultureInfo.InvariantCulture) + " s");
                check.That(halfNearAfter < 0f || halfNearAfter >= swim * 0.6f, "the school swims in rather than rushing, half of it at the float after " + halfNearAfter.ToString("0.0", CultureInfo.InvariantCulture) + " s of a " + swim.ToString("0", CultureInfo.InvariantCulture) + " s swim"); 
                check.That(school.Count < 2 || spreads.Count > 0 && spreads.Average() >= 2f, "the school is loose on its way in, its fish on average " + (spreads.Count > 0 ? spreads.Average() : 0f).ToString("0.0", CultureInfo.InvariantCulture) + " m from its middle");
                check.That(!nibbledEarly, "no fish of the school took the bait before the school was in");
                SelfTestPictures.Take("arrival-in");
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

                // Coming, circling, biting and scattering, no fish of the school ever reached land.
                check.That(Schools.LandedForTest == landedBefore, (Schools.LandedForTest - landedBefore).ToString(CultureInfo.InvariantCulture) + " fish of the school reached land on their own, the last " + Schools.LastLandingForTest);

                // One put on land beside the character is taken away before anyone can pick it up.
                var stray = others.FirstOrDefault(f => f != null);
                if (stray != null)
                {
                    var name = stray.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
                    var carried = Carried(player, name);
                    // Dry land beside the character, where a fish could only have flopped ashore.
                    var dry = SelfTestPlaces.DryLandBeside(player.transform.position);
                    check.That(dry.HasValue, "dry land beside the character to put a fish on");
                    var shore = (dry ?? player.transform.position) + Vector3.up * 0.3f;
                    stray.transform.position = shore;
                    var body = stray.GetComponent<Rigidbody>();
                    if (body != null) body.position = shore;
                    for (var waited = 0f; waited < 2f && stray != null; waited += Time.deltaTime) yield return null;
                    check.That(stray == null, "a school fish on land beside the character was taken away within 2 s");
                    check.That(Carried(player, name) == carried, "the fish on land was not picked up, " + carried.ToString(CultureInfo.InvariantCulture) + " carried before and " + Carried(player, name).ToString(CultureInfo.InvariantCulture) + " after");
                }

                // The float gone, the school leaves, and each fish is taken away only out of sight:
                // far from the character or deep under the surface, never where it was plainly seen.
                var rules = PlayRules.Now;
                var leaving = SchoolOf(floatId);
                var last = leaving.ToDictionary(f => f, f => (Distance: 0f, Under: 0f));
                var away = leaving.Count > 0 ? leaving.Average(f => Vector3.Distance(f.transform.position, player.transform.position)) : 0f;
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                var inSight = new List<string>();
                var since = 0f;
                for (; since < 90f && last.Count > 0; since += Time.deltaTime)
                {
                    foreach (var fish in last.Keys.ToList())
                    {
                        if (fish == null)
                        {
                            var seen = last[fish];
                            if (seen.Distance < rules.OutOfSight && seen.Under < rules.DiveDepth && since < rules.LeaveBackstop)
                                inSight.Add(seen.Distance.ToString("0", CultureInfo.InvariantCulture) + " m off, " + seen.Under.ToString("0.0", CultureInfo.InvariantCulture) + " m under");
                            last.Remove(fish);
                            continue;
                        }
                        last[fish] = (Vector3.Distance(fish.transform.position, player.transform.position), ZoneSystem.instance.m_waterLevel - fish.transform.position.y);
                    }
                    yield return null;
                }
                check.That(inSight.Count == 0, inSight.Count.ToString(CultureInfo.InvariantCulture) + " leaving fish vanished in plain sight: " + string.Join("; ", inSight));
                var stillHere = last.Keys.Where(f => f != null).ToList();
                if (stillHere.Count > 0)
                {
                    var now = stillHere.Average(f => Vector3.Distance(f.transform.position, player.transform.position));
                    check.That(now > away + 10f, stillHere.Count.ToString(CultureInfo.InvariantCulture) + " fish still about after 90 s are swimming off, from " + away.ToString("0", CultureInfo.InvariantCulture) + " m to " + now.ToString("0", CultureInfo.InvariantCulture) + " m away; the rest go out of sight later");
                }
                var lost = wildBefore.Where(id => ZDOMan.instance.GetZDO(id) == null).ToList();
                check.That(lost.Count == 0, lost.Count.ToString(CultureInfo.InvariantCulture) + " fish of the world's own were taken away");

                // The character is as it was: its hands, its inventory and what it knows.
                gear.PutBack();
                check.That(gear.AsBefore(), "the character's gear and what it knows are as before the run: " + gear.Difference());
            }
            finally
            {
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        private static int Carried(Player player, string sharedName) =>
            player.GetInventory().GetAllItems().Where(i => i.m_shared.m_name == sharedName).Sum(i => i.m_stack);

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }

        /// <summary>The fish marked as drawn to this float, in the loaded world.</summary>
        /// <summary>Waits, a frame at a time and at most 40 s, until no school of this game is leaving any more.</summary>
        internal static IEnumerable UntilNoSchoolLeaves()
        {
            for (var waited = 0f; waited < 40f && Schools.LeavingForTest > 0; waited += Time.deltaTime) yield return null;
        }

        /// <summary>The water level at a fish, as the game keeps it.</summary>
        private static readonly AccessTools.FieldRef<Fish, float> FishWater = AccessTools.FieldRefAccess<Fish, float>("m_inWater");

        internal static List<Fish> SchoolOf(ZDOID floatId) =>
            Fish.Instances.OfType<Fish>().Where(f => f != null && Schools.FloatOf(f) == floatId).ToList();

        /// <summary>The fish of the world's own in the loaded world, by their ids.</summary>
        private static IEnumerable<ZDOID> WildFish() =>
            Fish.Instances.OfType<Fish>().Where(f => f != null && Schools.FloatOf(f).IsNone()).Select(f => f.GetZDOID());

        private static string PrefabName(Fish fish) => Utils.GetPrefabName(fish.gameObject);

        /// <summary>How far the fish are on average from their middle, across the ground.</summary>
        private static float Spread(List<Fish> fish)
        {
            var middle = Vector3.zero;
            foreach (var f in fish) middle += f.transform.position;
            middle /= fish.Count;
            return fish.Average(f => Across(f.transform.position, middle));
        }

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>A place on the water 6 to 20 m from the character where the water is at least this deep, as far out as found, as a cast throws it.</summary>
        internal static Vector3? DeepWaterBeside(Vector3 from, float depth)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            for (var distance = 20f; distance >= 6f; distance -= 2f)
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
