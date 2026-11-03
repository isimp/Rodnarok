using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A school scared away for good tells the one fishing why, as a player meets it. A school swims
    /// in to a float, the character makes noise as the game makes it for a step, and with a school
    /// that leaves at its first startle the line says the character's steps scared it away. Then a
    /// float lands within 12 m of the character over deep water, where a fish is shy of the one
    /// fishing, and a school that always gives up there swims off without biting, the line saying it
    /// was too shy so close. Each time exactly one line is told and the school leaves. Needs a
    /// character beside water at least 3 m deep within 6 to 10 m; the rod is brought for the run and
    /// put back after.
    /// </summary>
    internal static class SelfTestGone
    {
        private const string Bait = "FishingBait";

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ASchoolScaredAwayByYourStepsOrTooShyToBiteSaysWhy", SaysWhy);
        }

        private static string F(float value, string format = "0.0") => value.ToString(format, CultureInfo.InvariantCulture);

        private static IEnumerable SaysWhy(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = CloseDeepWater(player.transform.position, 3f);
            check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 10 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            check.That(floatPrefab != null && baitPrefab != null, "the float and the bait are there");
            if (floatPrefab == null || baitPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // Steps: a school that leaves at its first startle, none giving up meanwhile.
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: true, baitLost: false, bumps: false, frightToLeave: Wary.StartleFright, mostGivingUp: 0.0);
                ff = Cast(floatPrefab, baitPrefab, spot.Value, player, out var floatId);
                List<Fish> school = null;
                for (var waited = 0f; waited < 60f; waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    if (school.Count > 0 && Nearest(school, ff) < 14f) break;
                    yield return null;
                }
                check.That(school != null && school.Count > 0 && Nearest(school, ff) < 14f, "a school swam to within 14 m of the float within 60 s");
                if (school == null || school.Count == 0) yield break;
                var told = Schools.GoneToldForTest;
                var leaving = Schools.LeavingForTest;
                player.AddNoise(Vector3.Distance(player.transform.position, ff.transform.position) / PlayRules.Now.Hearing + 10f);
                for (var waited = 0f; waited < 2f && Schools.GoneToldForTest == told; waited += Time.deltaTime) yield return null;
                var steps = SchoolGone.Text(GoneWhy.Noise, null);
                check.That(Schools.LeavingForTest > leaving, "the school startled by the character's steps leaves; " + (Schools.LeavingForTest - leaving).ToString(CultureInfo.InvariantCulture) + " fish leaving");
                check.That(Schools.GoneToldForTest == told + 1 && Schools.LastGoneForTest == steps, "the one fishing is told once \"" + steps + "\"; told "
                    + (Schools.GoneToldForTest - told).ToString(CultureInfo.InvariantCulture) + " times, last \"" + Schools.LastGoneForTest + "\"");
                check.Note("another player's steps name that player; that needs a second player and is checked outside the game");
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                // Quiet again before the next cast, so the steps' noise has faded.
                for (var waited = 0f; waited < 10f && player.GetNoiseRange() > 0.5f; waited += Time.deltaTime) yield return null;

                // Too shy: close to the character every ready school gives up.
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: true, baitLost: false, bumps: false, mostGivingUp: 1.0);
                Schools.GiveUpRollForTest = 0f;
                told = Schools.GoneToldForTest;
                leaving = Schools.LeavingForTest;
                ff = Cast(floatPrefab, baitPrefab, spot.Value, player, out _);
                for (var waited = 0f; waited < 60f && Schools.GoneToldForTest == told; waited += Time.deltaTime) yield return null;
                var at = ff != null ? ff.transform.position : spot.Value;
                var distance = Across(player.transform.position, at);
                var depth = ZoneSystem.instance.GetSolidHeight(at, out var ground) ? ZoneSystem.instance.m_waterLevel - ground : 0f;
                var shy = SchoolGone.Text(SchoolGone.Shy(distance, depth), null);
                check.That(Schools.LeavingForTest > leaving, "the shy school swims off without biting; " + (Schools.LeavingForTest - leaving).ToString(CultureInfo.InvariantCulture) + " fish leaving");
                check.That(Schools.GoneToldForTest == told + 1 && Schools.LastGoneForTest == shy, "the float " + F(distance) + " m from the character over " + F(depth) + " m of water, the one fishing is told once \""
                    + shy + "\"; told " + (Schools.GoneToldForTest - told).ToString(CultureInfo.InvariantCulture) + " times, last \"" + Schools.LastGoneForTest + "\"");
            }
            finally
            {
                Schools.GiveUpRollForTest = null;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        private static FishingFloat Cast(GameObject floatPrefab, GameObject baitPrefab, Vector3 at, Player player, out ZDOID floatId)
        {
            var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
            bait.m_dropPrefab = baitPrefab;
            var go = Object.Instantiate(floatPrefab, at, Quaternion.identity);
            var ff = go.GetComponent<FishingFloat>();
            ff.Setup(player, Vector3.zero, 0f, null, null, bait);
            floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;
            return ff;
        }

        /// <summary>A place on the water 6 to 10 m from the character where the water is at least this deep, close enough that a fish is shy of the one fishing.</summary>
        private static Vector3? CloseDeepWater(Vector3 from, float depth)
        {
            var water = ZoneSystem.instance.m_waterLevel;
            for (var distance = 10f; distance >= 6f; distance -= 1f)
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

        private static float Nearest(List<Fish> school, FishingFloat ff) =>
            school == null || ff == null || !school.Any(f => f != null) ? float.MaxValue : school.Where(f => f != null).Min(f => Across(f.transform.position, ff.transform.position));

        private static float Across(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
