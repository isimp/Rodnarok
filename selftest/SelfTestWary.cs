using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A wary school, as a player meets one: while it swims in to a float, the character makes
    /// noise as the game makes it for walking or running, and the school darts off; all quiet, it
    /// comes back; then the float is reeled in through it, and the school leaves for good. Needs a
    /// character holding nothing in particular beside water at least 3 m deep; the rod is brought
    /// for the run and put back after.
    /// </summary>
    internal static class SelfTestWary
    {
        private const string Bait = "FishingBait";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AWarySchoolDartsOffAtNoiseComesBackWhenQuietAndLeavesAtAFastFloat", Wariness);
        }

        private static IEnumerable Wariness(ScenarioCheck check)
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

                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: true);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                // On its way in, before it is ready to bite, the school is within earshot of the float.
                List<Fish> school = null;
                for (var waited = 0f; waited < 60f; waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    if (school.Count > 0 && Nearest(school, ff) < 14f) break;
                    yield return null;
                }
                check.That(school != null && school.Count > 0 && Nearest(school, ff) < 14f, "a school swam to within 14 m of the float within 60 s");
                if (school == null || school.Count == 0) yield break;

                // Noise as the game makes it for a step or a run, reaching past the float.
                var startledBefore = Schools.StartledForTest;
                var before = Average(school, ff);
                player.AddNoise(Vector3.Distance(player.transform.position, ff.transform.position) + 10f);
                var away = false;
                for (var waited = 0f; waited < 1f && !away; waited += Time.deltaTime)
                {
                    away = Schools.AwayForTest(ff);
                    yield return null;
                }
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
                var after = Average(school, ff);
                check.That(away && Schools.StartledForTest == startledBefore + 1, "noise reaching the float startles the school; startled " + (Schools.StartledForTest - startledBefore).ToString(CultureInfo.InvariantCulture) + " times");
                check.That(after > before + 2f, "startled, the school darts off from the float, from " + before.ToString("0.0", CultureInfo.InvariantCulture) + " m to " + after.ToString("0.0", CultureInfo.InvariantCulture) + " m on average");

                // All quiet again: it comes back, not before the quiet has lasted.
                var quietFrom = Time.time;
                for (var waited = 0f; waited < Wary.QuietBeforeComingBack + 10f && Schools.AwayForTest(ff); waited += Time.deltaTime) yield return null;
                var quiet = Time.time - quietFrom;
                check.That(!Schools.AwayForTest(ff) && quiet >= Wary.QuietBeforeComingBack - 2.6f, "all quiet, the school comes back after the quiet lasts "
                    + Wary.QuietBeforeComingBack.ToString("0", CultureInfo.InvariantCulture) + " s, the noise fading first; back after " + quiet.ToString("0.0", CultureInfo.InvariantCulture) + " s");

                // Reeled in through the school, the float startles it a second time and it leaves.
                for (var waited = 0f; waited < 40f && Nearest(SelfTestArrival.SchoolOf(floatId), ff) >= 6f; waited += Time.deltaTime) yield return null;
                var leavingBefore = Schools.LeavingForTest;
                SelfTestDraw.Reeling = true;
                for (var waited = 0f; waited < 2f && Schools.StartledForTest < startledBefore + 2; waited += Time.deltaTime) yield return null;
                SelfTestDraw.Reeling = false;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(Schools.StartledForTest == startledBefore + 2, "the float reeled in through the school startles it again; startled " + (Schools.StartledForTest - startledBefore).ToString(CultureInfo.InvariantCulture) + " times");
                check.That(Schools.LeavingForTest > leavingBefore && SelfTestArrival.SchoolOf(floatId).All(f => f == null || !f.IsHooked()), "startled a second time, the school leaves; "
                    + (Schools.LeavingForTest - leavingBefore).ToString(CultureInfo.InvariantCulture) + " fish leaving");
            }
            finally
            {
                SelfTestDraw.Reeling = false;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        private static float Nearest(List<Fish> school, FishingFloat ff) =>
            school == null || ff == null || !school.Any(f => f != null) ? float.MaxValue : school.Where(f => f != null).Min(f => Across(f.transform.position, ff.transform.position));

        private static float Average(List<Fish> school, FishingFloat ff) =>
            !school.Any(f => f != null) ? 0f : school.Where(f => f != null).Average(f => Across(f.transform.position, ff.transform.position));

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
