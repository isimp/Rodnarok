using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Missed bites, as a player meets them: a school comes to a float, a fish bites, the line is
    /// not held; the first miss only nibbles the bait, and the fish bites again until the last miss
    /// takes it: the float is bare, nothing bites it again, the school moves on, and reeling the
    /// float in gives no bait back. Needs a character
    /// beside water at least 3 m deep; the rod is brought for the run and put back after.
    /// </summary>
    internal static class SelfTestBaitLost
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AMissedBiteTakesTheBaitAndReelingInGivesNoneBack", MissedBite);
        }

        private static IEnumerable MissedBite(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            if (floatPrefab == null || baitPrefab == null) yield break;
            var baitName = baitPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                var rules = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, bumps: false, baitLost: true);
                PlayRules.ForTest = rules;
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                // A bite, left unheld.
                var bitAt = -1f;
                for (var waited = 0f; waited < 60f && bitAt < 0f; waited += Time.deltaTime)
                {
                    if (Nibbler(ff) != null) bitAt = NibbleTime(ff);
                    yield return null;
                }
                check.That(bitAt > 0f, "a fish bites within 60 s of the cast");
                if (bitAt < 0f) yield break;
                check.That(!Bites.BareForTest(ff), "within the hook window the bait is still there");

                var leavingBefore = Schools.LeavingForTest;
                for (var waited = 0f; waited < rules.HookWindow + 1f; waited += Time.deltaTime) yield return null;
                check.That(Bites.MissesForTest(ff) == 1 && !Bites.BareForTest(ff), "the first missed bite only nibbles the bait; misses " + Bites.MissesForTest(ff).ToString(CultureInfo.InvariantCulture));

                // Left unheld, the fish bites again until the last miss takes the bait.
                for (var waited = 0f; waited < 40f && !Bites.BareForTest(ff); waited += Time.deltaTime) yield return null;
                check.That(Bites.BareForTest(ff) && Bites.MissesForTest(ff) == BaitLoss.MissesToTake && Bites.BaitUsedForTest(ff), "the " + BaitLoss.MissesToTake.ToString(CultureInfo.InvariantCulture)
                    + "th missed bite takes the bait: the float is bare and the game counts its bait as used; misses " + Bites.MissesForTest(ff).ToString(CultureInfo.InvariantCulture));
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
                check.That(Schools.LeavingForTest > leavingBefore || SelfTestArrival.SchoolOf(floatId).Count == 0, "the school moves on once the bait is gone");

                // Nothing bites the bare float again.
                var lastBite = NibbleTime(ff);
                for (var waited = 0f; waited < 5f; waited += Time.deltaTime) yield return null;
                check.That(Mathf.Approximately(NibbleTime(ff), lastBite) && Nibbler(ff) == null, "nothing bites the bare float again");

                // Reeled all the way in, it gives no bait back.
                var carried = player.GetInventory().CountItems(baitName);
                SelfTestDraw.Reeling = true;
                for (var waited = 0f; waited < 60f && ff != null; waited += Time.deltaTime) yield return null;
                SelfTestDraw.Reeling = false;
                for (var waited = 0f; waited < 0.5f; waited += Time.deltaTime) yield return null;
                check.That(ff == null, "the float reels all the way in within 60 s");
                check.That(player.GetInventory().CountItems(baitName) == carried, "reeling the bare float in gives no bait back; "
                    + carried.ToString(CultureInfo.InvariantCulture) + " carried before, " + player.GetInventory().CountItems(baitName).ToString(CultureInfo.InvariantCulture) + " after");
            }
            finally
            {
                SelfTestDraw.Reeling = false;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }
    }
}
