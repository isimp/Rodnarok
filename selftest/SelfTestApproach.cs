using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// While a school is at the float the crosshair ring tells how close the fish is, and when the school
    /// leaves the wisp comes home. The character wears a Wisplight and a float with real bait lands in
    /// deep water, the school not wary. Once a fish is picked and hovers by the float, the ring must keep
    /// the bite's size and grow stronger as the fish comes closer across the water, as the rule has it;
    /// the secondary attack before the hook must do nothing to the float or the fish; the wisp must guard
    /// the float meanwhile. Then the school is scared off, and the wisp must come back to the character
    /// and the ring go. Pictures: the ring faint with the fish out and strong with it close. The float is
    /// taken away again with its school.
    /// </summary>
    internal static class SelfTestApproach
    {
        private const string Bait = "FishingBait";
        private const string Wisplight = "Demister";

        /// <summary>How far the drawn ring's strength may stray from the rule for the fish's distance, the fish moving between frames.</summary>
        private const float StrengthWithin = 0.1f;

        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("TheRingGrowsStrongerAsTheFishComesCloserAndTheWispComesHomeWhenTheSchoolLeaves", Approach);
        }

        private static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static void Press(Player player) =>
            player.SetControls(Vector3.zero, attack: false, attackHold: false, secondaryAttack: true, secondaryAttackHold: false, block: false, blockHold: false, jump: false, crouch: false, run: false, autoRun: false);

        private static void Aim(Player player, FishingFloat ff)
        {
            if (ff == null) return;
            var dir = ff.transform.position + Vector3.up * 1.2f - player.m_eye.position;
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>The ring as drawn now: shown, its size and strength.</summary>
        private static (bool Shown, float Scale, float A) Drawn()
        {
            var ring = CrosshairRing.ImageForTest;
            if (ring == null || !ring.gameObject.activeInHierarchy) return (false, 0f, 0f);
            return (true, ring.transform.localScale.x, ring.color.a);
        }

        private static IEnumerable Approach(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            check.That(floatPrefab != null && baitPrefab != null, "the float and the " + Bait + " prefabs are there");
            if (floatPrefab == null || baitPrefab == null) yield break;
            check.That(Mathf.Approximately(ApproachRing.Reach, Bites.Reach), "the ring's reach is the bite's, " + F(ApproachRing.Reach) + " m against " + F(Bites.Reach) + " m");

            var gear = SelfTestGear.RodInHand(player);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                var worn = gear.Wear(Wisplight);
                check.That(worn != null, "a Wisplight is worn: " + gear.Trouble);
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                var daylight = EnvMan.IsDaylight();

                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, bumps: false);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);

                Fish picked = null;
                for (var waited = 0f; waited < 60f && picked == null; waited += Time.deltaTime)
                {
                    var (biter, free) = Schools.BiterForTest(ff);
                    if (biter != null && free > 2f) picked = biter;
                    Aim(player, ff);
                    yield return null;
                }
                check.That(picked != null, "a school comes to the baited float and picks a fish to bite, hovering more than 2 s before it may, within 60 s");
                if (picked == null || ff == null) yield break;

                // The ring keeps the bite's size and grows stronger as the fish comes closer.
                var biteSize = RingDraw.For(Ring.ForBite(0f, 1.5f)).Scale;
                int frames = 0, wrong = 0;
                var wrongSeen = "";
                float nearest = float.MaxValue, farthest = 0f;
                bool picturedFar = false, picturedNear = false;
                var guarded = false;
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime)
                {
                    Aim(player, ff);
                    yield return null;
                    if (ff == null) yield break;
                    var (fish, distance) = Schools.WaitingForTest(ff);
                    if (fish == null) break;
                    nearest = Mathf.Min(nearest, distance);
                    farthest = Mathf.Max(farthest, distance);
                    guarded |= FloatWisps.TaskForTest == WispTask.Search;
                    var drawn = Drawn();
                    frames++;
                    var strength = ApproachRing.Strength(distance);
                    if (!drawn.Shown || Mathf.Abs(drawn.Scale - biteSize) > 0.01f || Mathf.Abs(drawn.A - strength) > StrengthWithin)
                    {
                        wrong++;
                        wrongSeen = (drawn.Shown ? "size " + F(drawn.Scale) + " strength " + F(drawn.A) : "hidden") + " for size " + F(biteSize) + " strength " + F(strength) + " at " + F(distance) + " m";
                    }
                    if (daylight && !picturedFar && distance > ApproachRing.Hover - 0.2f)
                    {
                        picturedFar = true;
                        if (SelfTestPictures.Take("approach-ring-far") is string trouble) check.That(false, "a picture of the faint ring is taken: " + trouble);
                    }
                    if (daylight && !picturedNear && distance < ApproachRing.Reach + 0.3f)
                    {
                        picturedNear = true;
                        if (SelfTestPictures.Take("approach-ring-near") is string trouble) check.That(false, "a picture of the strong ring is taken: " + trouble);
                    }
                }
                check.That(frames > 0 && wrong <= frames / 10 + 1, "while the picked fish hovers the ring keeps the bite's size and grows stronger as it comes closer across the water: "
                    + N(wrong) + " of " + N(frames) + " frames off" + (wrong > 0 ? ", the last " + wrongSeen : "") + "; the fish between " + F(nearest) + " and " + F(farthest) + " m");
                check.That(worn == null || guarded, "with the school at the float the wisp guards it");
                if (ff == null) yield break;

                // The secondary attack before the hook does nothing to the float or the fish.
                var floatAt = ff.transform.position;
                var freeBefore = Schools.BiterForTest(ff).FreeIn;
                var pressedAt = Time.time;
                Press(player);
                for (var waited = 0f; waited < 0.3f; waited += Time.deltaTime) yield return null;
                if (ff == null) yield break;
                var (stillPicked, freeAfter) = Schools.BiterForTest(ff);
                var moved = new Vector2(ff.transform.position.x - floatAt.x, ff.transform.position.z - floatAt.z).magnitude;
                var expected = freeBefore - (Time.time - pressedAt);
                check.That(stillPicked == null || Mathf.Abs(freeAfter - expected) < 0.15f, "before the hook the secondary attack draws no fish sooner: " + F(freeBefore) + " s to wait before, " + F(freeAfter) + " s after " + F(Time.time - pressedAt) + " s");
                check.That(moved < 0.15f && ff.GetCatch() == null, "and moves the float no more than the water does, " + F(moved) + " m, hooking nothing");

                // The school leaves: the wisp comes back to the character, and the ring goes.
                Schools.Spook(picked, player.transform.position);
                var leftFloat = Schools.LeftFloat(ff);
                var cameHome = false;
                for (var waited = 0f; waited < 12f && !cameHome; waited += Time.deltaTime)
                {
                    yield return null;
                    cameHome = FloatWisps.TaskForTest == WispTask.Home;
                }
                var ringAfter = Drawn();
                check.That(leftFloat, "scared off, the school has left the float");
                check.That(worn == null || cameHome, "once the school has left, the wisp comes back to the character, " + FloatWisps.TaskForTest);
                check.That(!ringAfter.Shown, "and with no school at the float the ring is gone");
            }
            finally
            {
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
                SelfTestPictures.Forget();
                if (player != null)
                {
                    LookYaw(player) = yaw;
                    LookPitch(player) = pitch;
                }
            }
        }
    }
}
