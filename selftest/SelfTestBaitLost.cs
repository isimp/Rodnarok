using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Missed bites, as a player meets them: a school comes to a float and its fish bite, the line
    /// never held. Each fish bites once and has learned; each miss may take the bait, until it is
    /// taken or every fish has missed and the school swam off. Reeled in, a bare float gives no bait
    /// back and one whose bait is left gives it back. Needs a character beside water at least 3 m
    /// deep; the rod is brought for the run and put back after.
    /// </summary>
    internal static class SelfTestBaitLost
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("MissedBitesWearTheBaitDownAndEachFishBitesOnce", MissedBites);
        }

        private static IEnumerable MissedBites(ScenarioCheck check)
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

                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, bumps: false, baitLost: true);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                var floatId = go.GetComponent<ZNetView>().GetZDO().m_uid;

                // The school comes; its bites are left unheld until the bait is taken or the school swam off.
                List<Fish> school = null;
                for (var waited = 0f; waited < 60f && (school == null || school.Count == 0); waited += Time.deltaTime)
                {
                    school = SelfTestArrival.SchoolOf(floatId);
                    yield return null;
                }
                check.That(school != null && school.Count > 0, "a school comes within 60 s of the cast");
                if (school == null || school.Count == 0) yield break;
                var size = school.Count;

                var biters = new List<Fish>();
                var bitTwice = new List<string>();
                var leavingBefore = Schools.LeavingForTest;
                var lastBite = -1f;
                var swamOff = false;
                for (var waited = 0f; waited < 120f && !Bites.BareForTest(ff) && !swamOff; waited += Time.deltaTime)
                {
                    var nibbler = Nibbler(ff);
                    if (nibbler != null && NibbleTime(ff) > lastBite)
                    {
                        lastBite = NibbleTime(ff);
                        if (biters.Contains(nibbler)) bitTwice.Add(Utils.GetPrefabName(nibbler.gameObject));
                        biters.Add(nibbler);
                    }
                    swamOff = Bites.MissesForTest(ff) >= size && Schools.LeavingForTest > leavingBefore;
                    yield return null;
                }
                var misses = Bites.MissesForTest(ff);
                check.That(misses > 0, "fish bit and missed; " + misses.ToString(CultureInfo.InvariantCulture) + " misses of a school of " + size.ToString(CultureInfo.InvariantCulture));
                check.That(bitTwice.Count == 0, "a fish that missed bites the float no more; bit twice: " + string.Join(", ", bitTwice));
                check.That(Bites.BareForTest(ff) || swamOff, "the misses end with the bait taken or the whole school gone; misses "
                    + misses.ToString(CultureInfo.InvariantCulture) + ", bare " + Bites.BareForTest(ff));
                var bare = Bites.BareForTest(ff);
                check.Note(bare ? "the bait went at miss " + misses.ToString(CultureInfo.InvariantCulture) : "the school swam off after " + misses.ToString(CultureInfo.InvariantCulture) + " misses, the bait left");
                if (bare) check.That(Bites.BaitUsedForTest(ff), "a taken bait counts as used to the game");

                // Nothing bites again; reeled in, a bare float gives no bait back and one with bait left gives it back.
                var since = NibbleTime(ff);
                for (var waited = 0f; waited < 4f; waited += Time.deltaTime) yield return null;
                check.That(Mathf.Approximately(NibbleTime(ff), since), "nothing bites the float again");
                var carried = player.GetInventory().CountItems(baitName);
                SelfTestDraw.Reeling = true;
                for (var waited = 0f; waited < 60f && ff != null; waited += Time.deltaTime) yield return null;
                SelfTestDraw.Reeling = false;
                for (var waited = 0f; waited < 0.5f; waited += Time.deltaTime) yield return null;
                check.That(ff == null, "the float reels all the way in within 60 s");
                var now = player.GetInventory().CountItems(baitName);
                check.That(now == carried + (bare ? 0 : 1), (bare ? "the bare float gives no bait back" : "the float with its bait left gives it back") + "; "
                    + carried.ToString(CultureInfo.InvariantCulture) + " carried before, " + now.ToString(CultureInfo.InvariantCulture) + " after");
                if (!bare && now > carried) player.GetInventory().RemoveItem(baitName, now - carried);
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
