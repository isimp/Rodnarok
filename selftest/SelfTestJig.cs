using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float, timed by the crosshair ring. Pressed
    /// through the game's own controls (<c>Player.SetControls</c>) as a key or gamepad press reaches it.
    /// With a school drawn by real bait and its picked fish hovering by the float: the ring must keep the
    /// bite's size and fade in with the fish's distance across the water as the rule has it, the fish must
    /// make a steady run in and hold within reach during its wait, a jig out of reach must draw nothing, the first jig in reach must bring the bite 1.5 s sooner
    /// and flick the rod, and a second one that wait must draw nothing more. With a wary school, a jig off
    /// cue must make it flinch. Pictures: the ring faint with the fish far and whole in reach. Each float is taken away
    /// again with its school.
    /// </summary>
    internal static class SelfTestJig
    {
        private const string Bait = "FishingBait";

        /// <summary>How far the drawn ring's strength may stray from the rule for the fish's distance, the fish moving between frames.</summary>
        private const float StrengthWithin = 0.1f;

        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("TheRingBreathesWithAWaitingFishAndAJigInReachDrawsItSoonerOncePerWait", Jigging);
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

        private static FishingFloat Cast(GameObject floatPrefab, GameObject baitPrefab, Player player, Vector3 at)
        {
            var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
            bait.m_dropPrefab = baitPrefab;
            var ff = Object.Instantiate(floatPrefab, at, Quaternion.identity).GetComponent<FishingFloat>();
            ff.Setup(player, Vector3.zero, 0f, null, null, bait);
            return ff;
        }

        /// <summary>The ring as drawn now: shown, its size and strength.</summary>
        private static (bool Shown, float Scale, float A, float Fill, float R, float G, float B) Drawn()
        {
            var ring = CrosshairRing.ImageForTest;
            if (ring == null || !ring.gameObject.activeInHierarchy) return (false, 0f, 0f, 0f, 0f, 0f, 0f);
            var c = ring.color;
            return (true, ring.transform.localScale.x, c.a, ring.fillAmount, c.r, c.g, c.b);
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
            check.That(Mathf.Approximately(Jig.Reach, Bites.Reach), "the ring's reach is the bite's, " + F(Jig.Reach) + " m against " + F(Bites.Reach) + " m");

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
                var daylight = EnvMan.IsDaylight();

                // 1. A picked fish hovering by the float, the school not wary.
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, bumps: false, jig: true);
                ff = Cast(floatPrefab, baitPrefab, player, spot.Value);
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

                // The ring fades in with the fish; a jig off cue draws nothing and blinks red; the fish runs in and holds in reach.
                int frames = 0, wrong = 0;
                var wrongSeen = "";
                var sawRun = false;
                var holdFrames = 0;
                var holdOut = 0f;
                var nearest = float.MaxValue;
                bool offCueTried = false, offCueDrew = false, offCueRed = false;
                var picturedFar = false;
                var window = false;
                var lastPress = -10f;
                var steeredBefore = Schools.SteeredForTest;
                int runFrames = 0, freeFrames = 0;
                var runSpeed = 0f;
                for (var waited = 0f; waited < 4f && !window; waited += Time.deltaTime)
                {
                    Aim(player, ff);
                    yield return null;
                    if (ff == null) yield break;
                    var (fish, distance, used) = Schools.WaitingForTest(ff);
                    if (fish == null) break;
                    nearest = Mathf.Min(nearest, distance);
                    var drawn = Drawn();
                    var phase = Schools.NosePhaseForTest(ff);
                    if (phase == Jig.Nose.Run)
                    {
                        sawRun = true;
                        runFrames++;
                        runSpeed = Schools.RunSpeedForTest(ff);
                    }
                    else if (phase == Jig.Nose.Free) freeFrames++;
                    if (phase == Jig.Nose.Hold && sawRun)
                    {
                        holdFrames++;
                        if (!Jig.InReach(distance)) holdOut = Mathf.Max(holdOut, distance);
                    }
                    var recent = Time.time - lastPress < Jig.TookSeconds;
                    if (!recent && phase != Jig.Nose.Hold)
                    {
                        frames++;
                        var rule = RingDraw.For(Ring.ForWaiting(distance));
                        if (!drawn.Shown || Mathf.Abs(drawn.Scale - rule.Scale) > 0.01f || Mathf.Abs(drawn.A - rule.A) > StrengthWithin)
                        {
                            wrong++;
                            wrongSeen = (drawn.Shown ? "size " + F(drawn.Scale) + " strength " + F(drawn.A) : "hidden") + " for the rule's size " + F(rule.Scale) + " strength " + F(rule.A) + " at " + F(distance) + " m";
                        }
                        if (daylight && !picturedFar && distance > Jig.Hover - 0.3f)
                        {
                            picturedFar = true;
                            if (SelfTestPictures.Take("jig-ring-far") is string trouble) check.That(false, "a picture of the faint ring is taken: " + trouble);
                        }
                    }
                    if (!offCueTried && phase != Jig.Nose.Hold && distance > Jig.Reach + 0.3f)
                    {
                        offCueTried = true;
                        var before = Schools.JigCutsForTest;
                        var freeBefore = Schools.BiterForTest(ff).FreeIn;
                        Press(player);
                        lastPress = Time.time;
                        offCueDrew = Schools.JigCutsForTest != before || Schools.BiterForTest(ff).FreeIn < freeBefore - 0.2f;
                        yield return null;
                        var blink = Drawn();
                        offCueRed = blink.Shown && blink.R > 0.9f && blink.G < 0.4f;
                        continue;
                    }
                    window = !used && !recent && Jig.InWindow(phase, distance);
                }
                check.That(frames > 0 && wrong <= frames / 10 + 1, "while the picked fish hovers the ring keeps the bite's size and fades in with its distance across the water, whole in reach, faint at the hover's edge: "
                    + N(wrong) + " of " + N(frames) + " frames off" + (wrong > 0 ? ", the last " + wrongSeen : ""));
                check.That(holdOut == 0f, "after a steady run in the fish holds within reach of the float" + (holdOut > 0f ? ", but held as far as " + F(holdOut) + " m" : ""));
                check.That(offCueTried && !offCueDrew && offCueRed, "a jig off cue draws the fish no sooner and blinks the ring red" + (offCueTried ? (offCueRed ? "" : ", but the ring was not red") : " (the fish was never out of reach to try)"));
                check.That(window, "the hovering fish runs into reach of the float and holds there within 4 s, nearest " + F(nearest) + " m; " + N(runFrames) + " run frames, " + N(holdFrames) + " hold frames, "
                    + N(freeFrames) + " free, run speed " + F(runSpeed) + " m/s, steered in " + N(Schools.SteeredForTest - steeredBefore) + " fish steps");
                if (!window || ff == null) yield break;

                // The window: a whole-strength white arc draining over the hold.
                var arc1 = Drawn();
                if (daylight && SelfTestPictures.Take("jig-ring-window") is string troubleWindow) check.That(false, "a picture of the window is taken: " + troubleWindow);
                yield return null;
                var arc2 = Drawn();
                check.That(arc1.Shown && arc1.A > 0.9f && arc1.Fill < 1f && arc2.Fill < arc1.Fill, "while the fish holds the ring is a white arc draining like the bite's, " + F(arc1.Fill) + " then " + F(arc2.Fill) + " of it, strength " + F(arc1.A));
                if (ff == null || !Jig.InWindow(Schools.NosePhaseForTest(ff), Schools.WaitingForTest(ff).Distance)) yield break;

                // A jig in the window takes: 1.5 s sooner, the rod flicks, the ring flashes white then turns yellow.
                var cuts = Schools.JigCutsForTest;
                var freeIn = Schools.BiterForTest(ff).FreeIn;
                var pressedAt = Time.time;
                Press(player);
                var freeAfter = Schools.BiterForTest(ff).FreeIn;
                var flicking = Heaves.PosingForTest(player);
                var expected = freeIn - Jig.Cut - (Time.time - pressedAt);
                check.That(Schools.JigCutsForTest == cuts + 1 && Mathf.Abs(freeAfter - Mathf.Max(0f, expected)) < 0.15f,
                    "a jig in the window brings the bite " + F(Jig.Cut, "0.0") + " s sooner: " + F(freeIn) + " s to wait before, " + F(freeAfter) + " s after");
                check.That(flicking, "the jig flicks the rod up");
                yield return null;
                var flash = Drawn();
                for (var waited = 0f; waited < Jig.TookFlashSeconds + 0.05f; waited += Time.deltaTime) yield return null;
                var took = Drawn();
                check.That(flash.Shown && flash.R > 0.95f && flash.B > 0.9f && took.Shown && took.R > 0.95f && took.G > 0.8f && took.B < 0.2f,
                    "the ring flashes white and turns yellow as the jig takes: " + F(flash.R) + "," + F(flash.G) + "," + F(flash.B) + " then " + F(took.R) + "," + F(took.G) + "," + F(took.B));
                check.That(!Heaves.PosingForTest(player), "the rod's flick is over within " + F(Jig.FlickSeconds) + " s");

                // A second jig in the same hold draws nothing.
                var cutsAfter = Schools.JigCutsForTest;
                if (ff != null && Schools.NosePhaseForTest(ff) == Jig.Nose.Hold && Schools.WaitingForTest(ff).Used)
                {
                    Press(player);
                    check.That(Schools.JigCutsForTest == cutsAfter, "a second jig in the same hold draws the fish no sooner");
                }

                // The next hold is a window of its own, if the wait lasts that long.
                var nextWindow = false;
                for (var waited = 0f; waited < 2.5f && !nextWindow && ff != null; waited += Time.deltaTime)
                {
                    Aim(player, ff);
                    yield return null;
                    if (ff == null) break;
                    var (fish, distance, used) = Schools.WaitingForTest(ff);
                    if (fish == null) break;
                    nextWindow = !used && Jig.InWindow(Schools.NosePhaseForTest(ff), distance);
                }
                if (nextWindow)
                {
                    var cutsNext = Schools.JigCutsForTest;
                    Press(player);
                    check.That(Schools.JigCutsForTest == cutsNext + 1, "the next hold is a window of its own: a jig there draws the fish sooner again");
                }
                else check.Note("the wait ended before a second hold, so a second window was not tried");
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                // 2. A wary school: a jig off cue makes it flinch, and the ring jolts.
                PlayRules.ForTest = SelfTestRules.Normal.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: true, bumps: false, jig: true);
                ff = Cast(floatPrefab, baitPrefab, player, spot.Value);
                var ready = false;
                for (var waited = 0f; waited < 60f && !ready; waited += Time.deltaTime)
                {
                    var (fish, distance, _) = Schools.WaitingForTest(ff);
                    ready = fish != null && distance > Jig.Reach + 0.3f && !Schools.AwayForTest(ff);
                    Aim(player, ff);
                    yield return null;
                }
                check.That(ready, "a wary school waits at the float with its fish out of reach within 60 s");
                if (!ready || ff == null) yield break;
                var startles = Schools.JigStartlesForTest;
                Press(player);
                yield return null;
                var jolt = Drawn();
                check.That(Schools.JigStartlesForTest == startles + 1 && Schools.AwayForTest(ff), "a jig off cue makes the wary school flinch off the float, "
                    + N(Schools.JigStartlesForTest - startles) + " startles by jigging");
                var biteSize = RingDraw.For(Ring.ForBite(0f, 1.5f)).Scale;
                check.That(jolt.Shown && jolt.Scale <= biteSize + 0.01f && jolt.R > 0.9f && jolt.G < 0.4f, "the ring blinks red at its size as the school flinches, size " + F(jolt.Scale) + ", colour " + F(jolt.R) + "," + F(jolt.G) + "," + F(jolt.B));
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
