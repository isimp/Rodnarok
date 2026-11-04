using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float. Pressed through the game's own controls
    /// (<c>Player.SetControls</c>) as a key or gamepad press reaches it: with a school drawn by real bait
    /// and its picked fish hovering by the float, a jig must bring the moment that fish may bite 1.5 s
    /// sooner and twitch the float about a quarter metre toward the rod tip, hooking nothing. With a wary
    /// school near the float, one jig must not startle it, and a second within 0.6 s must make it flinch.
    /// Each float is taken away again with its school.
    /// </summary>
    internal static class SelfTestJig
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AJigDrawsAHoveringFishSoonerAndJiggingAgainTooSoonStartlesAWarySchool", Jigging);
        }

        private static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);

        private static void Press(Player player) =>
            player.SetControls(Vector3.zero, attack: false, attackHold: false, secondaryAttack: true, secondaryAttackHold: false, block: false, blockHold: false, jump: false, crouch: false, run: false, autoRun: false);

        private static float TipToFloat(Player player, FishingFloat ff)
        {
            var tip = Utils.FindChild(player.transform, "_RodTop");
            if (tip == null || ff == null) return 0f;
            var a = tip.position;
            var b = ff.transform.position;
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        private static float Nearest(List<Fish> school, FishingFloat ff)
        {
            if (school == null || ff == null) return float.MaxValue;
            var nearest = float.MaxValue;
            foreach (var fish in school)
            {
                if (fish == null) continue;
                var d = new Vector2(fish.transform.position.x - ff.transform.position.x, fish.transform.position.z - ff.transform.position.z).magnitude;
                nearest = Mathf.Min(nearest, d);
            }
            return nearest;
        }

        private static FishingFloat Cast(GameObject floatPrefab, GameObject baitPrefab, Player player, Vector3 at)
        {
            var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
            bait.m_dropPrefab = baitPrefab;
            var ff = Object.Instantiate(floatPrefab, at, Quaternion.identity).GetComponent<FishingFloat>();
            ff.Setup(player, Vector3.zero, 0f, null, null, bait);
            return ff;
        }

        private static IEnumerable Jigging(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            check.That(floatPrefab != null && baitPrefab != null, "the float and the " + Bait + " prefabs are there");
            if (floatPrefab == null || baitPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // 1. A picked fish hovering by the float comes sooner for a jig.
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, bumps: false, jig: true);
                ff = Cast(floatPrefab, baitPrefab, player, spot.Value);
                Fish picked = null;
                var freeIn = 0f;
                for (var waited = 0f; waited < 60f && picked == null; waited += Time.deltaTime)
                {
                    var (biter, free) = Schools.BiterForTest(ff);
                    if (biter != null && free > Jig.Cut + 0.3f)
                    {
                        picked = biter;
                        freeIn = free;
                        break;
                    }
                    yield return null;
                }
                check.That(picked != null, "a school comes to the baited float and picks a fish to bite, hovering more than " + F(Jig.Cut + 0.3f, "0.0") + " s before it may, within 60 s");
                if (picked == null || ff == null) yield break;
                var jigged = Heaves.JiggedForTest;
                var cuts = Schools.JigCutsForTest;
                var near = TipToFloat(player, ff);
                var pressedAt = Time.time;
                Press(player);
                var (after, freeAfter) = Schools.BiterForTest(ff);
                var expected = freeIn - Jig.Cut - (Time.time - pressedAt);
                check.That(Heaves.JiggedForTest == jigged + 1 && Schools.JigCutsForTest == cuts + 1 && after == picked && Mathf.Abs(freeAfter - expected) < 0.1f,
                    "a jig brings the hovering fish's bite " + F(Jig.Cut, "0.0") + " s sooner: " + F(freeIn) + " s to wait before, " + F(freeAfter) + " s after");
                for (var waited = 0f; waited < Jig.Seconds + 0.15f; waited += Time.deltaTime) yield return null;
                var twitched = ff != null ? near - TipToFloat(player, ff) : 0f;
                check.That(twitched > Jig.Metres * 0.4f && twitched < Jig.Metres * 2f, "the jig twitches the float about " + F(Jig.Metres) + " m toward the rod tip, " + F(twitched) + " m");
                check.That(ff != null && ff.GetCatch() == null, "a jig hooks nothing");
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // 2. A wary school near the float: one jig leaves it be, a second too soon makes it flinch.
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: true, bumps: false, jig: true);
                ff = Cast(floatPrefab, baitPrefab, player, spot.Value);
                var floatId = ff.GetComponent<ZNetView>().GetZDO().m_uid;
                var schoolNear = false;
                for (var waited = 0f; waited < 60f && !schoolNear; waited += Time.deltaTime)
                {
                    schoolNear = ff != null && Nearest(SelfTestArrival.SchoolOf(floatId), ff) < 5f && !Schools.AwayForTest(ff) && Schools.BiterForTest(ff).Fish == null;
                    yield return null;
                }
                check.That(schoolNear, "a wary school comes within 5 m of the float within 60 s");
                if (!schoolNear || ff == null) yield break;
                var startled = Schools.StartledForTest;
                var startles = Schools.JigStartlesForTest;
                Press(player);
                for (var waited = 0f; waited < Jig.TooSoonSeconds + 0.2f; waited += Time.deltaTime) yield return null;
                check.That(Schools.StartledForTest == startled && Schools.JigStartlesForTest == startles && ff != null && !Schools.AwayForTest(ff),
                    "one jig leaves a wary school near the float be: startled " + (Schools.StartledForTest - startled).ToString(CultureInfo.InvariantCulture) + " times");
                Press(player);
                for (var waited = 0f; waited < 0.2f; waited += Time.deltaTime) yield return null;
                Press(player);
                yield return null;
                check.That(Schools.JigStartlesForTest == startles + 1 && ff != null && Schools.AwayForTest(ff),
                    "jigging again within " + F(Jig.TooSoonSeconds, "0.0") + " s makes the wary school flinch off the float: " + (Schools.JigStartlesForTest - startles).ToString(CultureInfo.InvariantCulture) + " startles by jigging");
            }
            finally
            {
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
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
