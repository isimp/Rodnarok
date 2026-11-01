using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Every fish coming to a float tells itself by its sign, as a player meets it. A float with
    /// plain bait in deep water brings a school of Perch, every fish of it marked as coming to the
    /// float for every game; the float must show the Perch's rings on the way in, more often as the
    /// school nears, and once a fish bites the marks must be gone and no more signs show. Then one
    /// fish of each of the game's fish is put a few metres from a float and marked as coming: each
    /// must show its own sign, and a picture of each is taken, the character looking at it. The
    /// rules, the rod, the floats and the fish are put back after.
    /// </summary>
    internal static class SelfTestSigns
    {
        private const string NoBait = "Stone";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");
        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");

        private static readonly (string Prefab, string Name)[] GameFish =
        {
            ("Fish1", "perch"), ("Fish6", "herring"), ("Fish2", "pike"), ("Fish5", "trollfish"), ("Fish7", "grouper"), ("Fish8", "coralcod"),
            ("Fish12", "pufferfish"), ("Fish9", "anglerfish"), ("Fish11", "magmafish"), ("Fish3", "tuna"), ("Fish10", "salmon"), ("Fish4_cave", "tetra"),
        };

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("EachFishComingToAFloatShowsItsOwnSign", Shows);
        }

        private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        private static IEnumerable Shows(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            if (!check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)")) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(BaitTable.PlainBait);
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            check.That(floatPrefab != null && baitPrefab != null && noBait != null, "the float and the bait prefabs are there");
            if (floatPrefab == null || baitPrefab == null || noBait == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            var placed = new List<GameObject>();
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // A real school of Perch on its way in.
                Schools.RollForTest = 0f;
                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, baitLost: false, bumps: false);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                List<Fish> school = null;
                for (var waited = 0f; waited < 15f && (school == null || school.Count == 0); waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    yield return null;
                }
                check.That(school != null && school.Count > 0, "plain bait brought a school within 15 s");
                if (school == null || school.Count == 0) yield break;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(school.Where(f => f != null).All(f => Schools.ComingTo(f) == floatId), "every fish of the school is marked for every game as coming to the float");

                var showsFar = new List<float>();
                var showsNear = new List<float>();
                var lastShown = 0;
                var bit = false;
                for (var waited = 0f; waited < 90f && !bit; waited += Time.deltaTime)
                {
                    var (kind, shown, nearness) = ArrivalSigns.ForTest(floatId);
                    if (shown > lastShown)
                    {
                        (nearness < 0.4f ? showsFar : nearness > 0.7f ? showsNear : new List<float>()).Add(Time.time);
                        lastShown = shown;
                    }
                    bit = Nibbler(ff) != null && NibbleTime(ff) > 0f;
                    yield return null;
                }
                var (signKind, signShown, _) = ArrivalSigns.ForTest(floatId);
                check.That(signKind == ArrivalSignKind.Rings && signShown >= 3, "the Perch coming shows its rings on the way in: " + signKind + ", " + signShown + " times");
                var farEvery = showsFar.Count > 1 ? (showsFar.Last() - showsFar.First()) / (showsFar.Count - 1) : -1f;
                var nearEvery = showsNear.Count > 1 ? (showsNear.Last() - showsNear.First()) / (showsNear.Count - 1) : -1f;
                if (farEvery > 0f && nearEvery > 0f)
                    check.That(nearEvery < farEvery, "the rings come more often near the float, every " + F(nearEvery) + " s against " + F(farEvery) + " s far off");
                else
                    check.Note("the school was not seen both far off and near, so how often the rings came was not compared");
                check.That(bit, "a Perch bit within 90 s");
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(school.Where(f => f != null).All(f => Schools.ComingTo(f).IsNone()), "after the bite no fish of the school is marked as coming any more");
                var afterBite = ArrivalSigns.ForTest(floatId).Shown;
                for (var waited = 0f; waited < 3f; waited += Time.deltaTime) yield return null;
                check.That(ArrivalSigns.ForTest(floatId).Shown == 0 || ArrivalSigns.ForTest(floatId).Shown == afterBite, "after the bite no more signs show");
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                Schools.RollForTest = null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // One of each fish, a few metres off a bare float, marked as coming.
                var stone = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                stone.m_dropPrefab = noBait;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, stone);
                floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                var away = (spot.Value - player.transform.position);
                away.y = 0f;
                away = away.normalized;
                var wrong = new List<string>();
                foreach (var (prefab, name) in GameFish)
                {
                    var fishPrefab = ZNetScene.instance.GetPrefab(prefab);
                    if (fishPrefab == null)
                    {
                        wrong.Add(prefab + " not in the game");
                        continue;
                    }
                    var at = spot.Value + away * 6f + Vector3.down * 1.5f;
                    var go = Object.Instantiate(fishPrefab, at, Quaternion.LookRotation(-away));
                    placed.Add(go);
                    var fish = go.GetComponent<Fish>();
                    yield return null;
                    Schools.MarkComingForTest(fish, floatId);
                    var expected = ArrivalSign.Of(prefab);
                    var shownBy = 0;
                    var kindSeen = ArrivalSignKind.Rings;
                    for (var waited = 0f; waited < 3f; waited += Time.deltaTime)
                    {
                        player.SetLookDir(Quaternion.Euler(0f, 10f, 0f) * (at - player.m_eye.position));
                        var seen = ArrivalSigns.ForTest(floatId);
                        kindSeen = seen.Kind;
                        shownBy = seen.Shown;
                        if (waited > 2.2f && shownBy > 0) break;
                        yield return null;
                    }
                    if (kindSeen != expected || shownBy == 0) wrong.Add(name + " showed " + kindSeen + " " + shownBy + " times, " + expected + " wanted");
                    var trouble = SelfTestPictures.Take("sign-" + name);
                    if (trouble != null) wrong.Add("no picture of the " + name + ": " + trouble);
                    Schools.MarkComingForTest(fish, ZDOID.None);
                    ZNetScene.instance.Destroy(go);
                    placed.Remove(go);
                    // The float's signs end with the last fish coming to it, so the next fish starts afresh.
                    for (var waited = 0f; waited < 0.3f; waited += Time.deltaTime) yield return null;
                }
                check.That(wrong.Count == 0, "each fish of the game coming to a float shows its own sign: " + string.Join("; ", wrong));
                check.Note("sign pictures taken at " + (EnvMan.instance != null ? (EnvMan.instance.GetDayFraction() * 24f).ToString("00.0", CultureInfo.InvariantCulture) : "?") + " o'clock in game");
            }
            finally
            {
                Schools.RollForTest = null;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                foreach (var go in placed)
                    if (go != null) ZNetScene.instance.Destroy(go);
                gear.PutBack();
                if (player != null)
                {
                    LookYaw(player) = yaw;
                    LookPitch(player) = pitch;
                }
            }
        }
    }
}
