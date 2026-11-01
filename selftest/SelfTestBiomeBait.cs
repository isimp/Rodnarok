using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Plain bait brings the fish that live where the float lands, as a player meets it: the
    /// installed game's spawn lists must place the Trollfish in the Black Forest and the Tetra in no
    /// biome; a float with plain Fishing Bait in water taken for the Black Forest must bring a
    /// school of Trollfish, the last of the bait's fish there, and one of it must bite the plain
    /// bait; with the server's BiomeFishOnBait off the same cast must bring a Pike, the last of
    /// plain bait's own fish, and the game's bite test must turn the Trollfish away from it. The
    /// water stands in for the Black Forest wherever the character is, so the run tries a biome's
    /// fish even in the Meadows; the place's own biome is noted. The rules, the rod and the float
    /// are put back after.
    /// </summary>
    internal static class SelfTestBiomeBait
    {
        private const int BlackForest = (int)Heightmap.Biome.BlackForest;
        private const string Trollfish = "Fish5";
        private const string Pike = "Fish2";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly System.Reflection.MethodInfo TestBate = AccessTools.Method(typeof(Fish), "TestBate");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("PlainBaitBringsTheFishThatLiveWhereTheFloatLands", Brings);
        }

        private static IEnumerable Brings(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var book = FishBook.Current;
            check.That(book != null, "the fish book is read in a world");
            if (book == null) yield break;
            check.That(book.Baits.Takes(Trollfish, BaitTable.PlainBait, BlackForest, local: true), "the game's spawn lists place the Trollfish in the Black Forest");
            check.That(!book.Baits.Takes("Fish4_cave", BaitTable.PlainBait, (int)Heightmap.Biome.Mountain, local: true), "the Tetra lives in no biome of the world and keeps to its own bait");
            check.That(TestBate != null, "the game's bite test is there");

            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            if (!check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)")) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(BaitTable.PlainBait);
            check.That(floatPrefab != null && baitPrefab != null, "the float and the plain bait prefabs are there");
            if (floatPrefab == null || baitPrefab == null) yield break;
            check.Note("the water here is " + (Heightmap.Biome)FishBook.BiomeAt(spot.Value) + " and stood in for the Black Forest");

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                FishBook.BiomeForTest = BlackForest;
                // The last roll picks the last of the bait's fish that fit: the Trollfish among Perch, Pike and Trollfish.
                Schools.RollForTest = 0.999f;
                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, baitLost: false, bumps: false, biomeFishOnBait: true);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                ff = Cast(floatPrefab, spot.Value, player, bait);
                var floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                List<Fish> school = null;
                for (var waited = 0f; waited < 15f && (school == null || school.Count == 0); waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    yield return null;
                }
                var kinds = school == null ? "none" : string.Join(", ", school.Where(f => f != null).Select(f => Utils.GetPrefabName(f.gameObject)).Distinct());
                check.That(school != null && school.Count > 0 && school.All(f => f != null && Utils.GetPrefabName(f.gameObject) == Trollfish),
                    "plain bait in the Black Forest brings a school of Trollfish: " + kinds);
                if (school == null || school.Count == 0) yield break;

                var bit = false;
                for (var waited = 0f; waited < 90f && !bit; waited += Time.deltaTime)
                {
                    bit = NibbleTime(ff) > 0f && (school.Contains(Nibbler(ff)) || school.Contains(ff.GetCatch()));
                    yield return null;
                }
                check.That(bit, "a Trollfish of the school bites the plain bait within 90 s");
                var trollfish = school.FirstOrDefault(f => f != null);
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // The server's setting off: plain bait brings its own fish only, and a Trollfish turns away from it.
                PlayRules.ForTest = PlayRules.Now.With(biomeFishOnBait: false);
                var tooLittle = Schools.TooLittleWaterForTest;
                ff = Cast(floatPrefab, spot.Value, player, bait);
                floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                school = null;
                for (var waited = 0f; waited < 30f && (school == null || school.Count == 0); waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    yield return null;
                }
                kinds = school == null || school.Count == 0
                    ? "none within 30 s, " + Schools.LeavingForTest.ToString(CultureInfo.InvariantCulture) + " fish still leaving, " + (Schools.TooLittleWaterForTest - tooLittle).ToString(CultureInfo.InvariantCulture) + " casts found too little water"
                    : string.Join(", ", school.Where(f => f != null).Select(f => Utils.GetPrefabName(f.gameObject)).Distinct());
                check.That(school != null && school.Count > 0 && school.All(f => f != null && Utils.GetPrefabName(f.gameObject) == Pike),
                    "with BiomeFishOnBait off the same cast brings a school of Pike: " + kinds);
                var test = trollfish != null ? trollfish : school?.FirstOrDefault(f => f != null);
                if (test != null && TestBate != null && Utils.GetPrefabName(test.gameObject) == Trollfish)
                {
                    var took = Enumerable.Range(0, 30).Count(_ => (bool)TestBate.Invoke(test, new object[] { ff }));
                    check.That(took == 0, "with BiomeFishOnBait off a Trollfish turns away from plain bait, " + took.ToString(CultureInfo.InvariantCulture) + " of 30 taken");
                }
            }
            finally
            {
                FishBook.BiomeForTest = null;
                Schools.RollForTest = null;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        private static FishingFloat Cast(GameObject floatPrefab, Vector3 at, Player player, ItemDrop.ItemData bait)
        {
            var ff = Object.Instantiate(floatPrefab, at, Quaternion.identity).GetComponent<FishingFloat>();
            ff.Setup(player, Vector3.zero, 0f, null, null, bait);
            return ff;
        }
    }
}
