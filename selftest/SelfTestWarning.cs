using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A hooked fish shows its next struggle coming, and the line shows the struggle. A Perch is put
    /// beside a float in deep water and hooked, and the line is worked as a player would through
    /// several rests and struggles: tapped through each struggle, held through a rest only to win
    /// back line it took. Against the game's own time of the next struggle, the time shared on the
    /// fish's networked data must match; the float's look must lie still while more than 1.5 s are
    /// left and tremble toward the fish within them; the line must be drawn taut only in the Perch's
    /// last 0.6 s and through the struggle, quivering by the strain. Switched off, nothing shows and
    /// the float stands upright. Pictures: a still float, a restless one, the taut line, and a line
    /// strained in a struggle. The float carries no fish's bait, so nothing else comes; the fish is
    /// let go out in the water, never landed, and the fish, float and rod are taken away again.
    /// </summary>
    internal static class SelfTestWarning
    {
        private const string Perch = "Fish1";
        private const string NoBait = "Stone";

        /// <summary>The strain at which the tapping eases off, and at which it holds again.</summary>
        private const float EaseAt = 0.5f;
        private const float HoldAgainAt = 0.15f;

        /// <summary>How long the fight may go, in seconds, and how many struggles it waits for.</summary>
        private const float FightSeconds = 60f;
        private const int StrugglesWanted = 3;

        /// <summary>How far the shared time may stray from the game's own, in seconds: a frame or two and the clocks' rounding.</summary>
        private const float SharedWithin = 0.15f;

        /// <summary>How close to a border in time a frame may be and still be judged, in seconds, against the frame's own length.</summary>
        private const float Margin = 0.1f;

        /// <summary>How high above the float the view is aimed, in metres, so the line and the float both show.</summary>
        private const float AimAbove = 1.2f;

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly AccessTools.FieldRef<Player, float> LookYaw = AccessTools.FieldRefAccess<Player, float>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AHookedFishShowsItsNextStruggleComingAndTheLineQuiversThroughIt", ShowsItsStruggleComing);
        }

        private static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static void Aim(Player player, FishingFloat ff)
        {
            if (ff == null) return;
            var dir = ff.transform.position + Vector3.up * AimAbove - player.m_eye.position;
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private static IEnumerable ShowsItsStruggleComing(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            var perchPrefab = ZNetScene.instance.GetPrefab(Perch);
            check.That(floatPrefab != null && noBait != null && perchPrefab != null, "the float, the " + NoBait + " and the Perch prefabs are there");
            if (floatPrefab == null || noBait == null || perchPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            GameObject put = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = SelfTestRules.Normal.With(tension: true, pullBySpeed: true, callWorldFish: false, wary: false, baitLost: false, struggleWarning: true);
                var rules = PlayRules.Now;
                var daylight = EnvMan.IsDaylight();
                if (!daylight) check.Note("no pictures of the warning: it is night in game; run the self-test in daylight to see it");

                var bait = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = noBait;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                put = Object.Instantiate(perchPrefab, spot.Value + new Vector3(1f, -1f, 0f), Quaternion.identity);
                var fish = put.GetComponent<Fish>();
                yield return null;
                var lookBefore = new List<(Transform T, Quaternion Rot, Vector3 Pos)>();
                foreach (Transform child in ff.transform) lookBefore.Add((child, child.localRotation, child.localPosition));
                Nibbler(ff) = fish;
                NibbleTime(ff) = Time.time;
                TryToHook.Invoke(ff, null);
                check.That(ff.GetCatch() == fish, "the Perch put beside the float is hooked");
                if (ff.GetCatch() != fish) yield break;
                var keepLine = LineLength(ff);
                var tautSeconds = StruggleWarning.TautSeconds(FishBook.Current?.Speed(Perch) ?? 0f, 1, rules);

                int restFrames = 0, sharedOff = 0, stillFrames = 0, stillWrong = 0, restlessFrames = 0, restlessSeen = 0;
                int earlyTaut = 0, slackFrames = 0, tautFrames = 0, tautSeen = 0, tautBent = 0, struggleFrames = 0, struggleSlack = 0, struggleBent = 0, quiverOff = 0, missing = 0;
                int struggles = 0, warnedStruggles = 0, parts = 0, points = 0;
                float sharedWorst = 0f, mostTilt = 0f, restSag = 0f, tautBend = 0f, struggleBend = 0f, quiverLow = -1f, quiverHigh = -1f;
                bool wasStruggling = false, sawFarFromIt = false;
                string stillSeen = "", quiverSeen = "";
                var pictured = new HashSet<string>();
                var holding = false;
                for (var fought = 0f; fought < FightSeconds && struggles < StrugglesWanted + 1; fought += Time.deltaTime)
                {
                    if (ff == null || ff.GetCatch() != fish) break;
                    var struggled = fish.IsEscaping();
                    var strain = Lines.StrainOf(ff);
                    holding = struggled ? (strain >= EaseAt ? false : strain <= HoldAgainAt || holding) : LineLength(ff) > keepLine;
                    SelfTestHold.Holding = holding;
                    player.AddStamina(player.GetMaxStamina());
                    Aim(player, ff);
                    yield return null;
                    if (ff == null || ff.GetCatch() != fish) break;
                    var struggling = fish.IsEscaping();
                    if (struggling && !wasStruggling)
                    {
                        struggles++;
                        if (sawFarFromIt) warnedStruggles++;
                        sawFarFromIt = false;
                    }
                    wasStruggling = struggling;
                    var state = StruggleTells.StateForTest(ff);
                    if (state == null)
                    {
                        missing++;
                        continue;
                    }
                    var st = state.Value;
                    parts = Mathf.Max(parts, st.Parts);
                    var (count, bend) = StruggleTells.LineForTest(ff);
                    points = count;
                    if (struggled != struggling) continue;

                    if (!struggling)
                    {
                        var until = StruggleTells.NextStruggleForTest(fish) - Time.time;
                        if (until <= 0f) continue;
                        restFrames++;
                        var off = Mathf.Abs(st.Until - until);
                        if (off > SharedWithin) sharedOff++;
                        sharedWorst = Mathf.Max(sharedWorst, off);
                        if (until > StruggleWarning.RestlessFrom + Margin)
                        {
                            sawFarFromIt = true;
                            stillFrames++;
                            if (st.Tilt > 0.01f)
                            {
                                stillWrong++;
                                stillSeen = F(st.Tilt, "0.0") + " degrees with " + F(until) + " s to go";
                            }
                            if (!st.Taut) restSag = Mathf.Max(restSag, bend);
                            if (daylight && until > 2f && pictured.Add("warn-still")) SelfTestPictures.Take("warn-still");
                        }
                        else if (until < StruggleWarning.RestlessFrom - Margin)
                        {
                            restlessFrames++;
                            if (st.Restless > 0f && st.Tilt > 0f) restlessSeen++;
                            mostTilt = Mathf.Max(mostTilt, st.Tilt);
                            if (daylight && until > tautSeconds + Margin && until < 1f && st.Tilt > 3f && pictured.Add("warn-restless")) SelfTestPictures.Take("warn-restless");
                        }
                        if (until > tautSeconds + Margin)
                        {
                            slackFrames++;
                            if (st.Taut) earlyTaut++;
                        }
                        else if (until < tautSeconds - Margin)
                        {
                            tautFrames++;
                            if (st.Taut) tautSeen++;
                            tautBend = Mathf.Max(tautBend, bend);
                            if (bend > TautBendAllowed(StruggleWarning.WarnQuiver)) tautBent++;
                            if (daylight && pictured.Add("warn-taut")) SelfTestPictures.Take("warn-taut");
                        }
                        continue;
                    }

                    struggleFrames++;
                    if (!st.Taut) struggleSlack++;
                    struggleBend = Mathf.Max(struggleBend, bend);
                    if (bend > TautBendAllowed(st.Quiver)) struggleBent++;
                    var strainNow = Lines.StrainOf(ff);
                    var rule = StruggleWarning.Quiver(true, true, strainNow);
                    if (Mathf.Abs(st.Quiver - rule) > 0.1f)
                    {
                        quiverOff++;
                        quiverSeen = F(st.Quiver) + " at strain " + F(strainNow) + ", the rule " + F(rule);
                    }
                    if (strainNow < 0.15f) quiverLow = Mathf.Max(quiverLow, st.Quiver);
                    if (strainNow > 0.4f) quiverHigh = Mathf.Max(quiverHigh, st.Quiver);
                    if (daylight && strainNow > 0.4f && pictured.Add("struggle-strained")) SelfTestPictures.Take("struggle-strained");
                }
                SelfTestHold.Holding = false;

                check.That(missing == 0, "every frame of the fight the hooked float has its warning, " + N(missing) + " frames without");
                check.That(struggles >= 2 && warnedStruggles >= 1, "the fight went through a rest long enough to watch whole: " + N(struggles) + " struggles, " + N(warnedStruggles) + " after a rest seen from more than "
                    + F(StruggleWarning.RestlessFrom, "0.0") + " s before");
                check.That(restFrames > 0 && sharedOff <= 2, "the game owning the fish shares when its next struggle is due, the game's own time within " + F(SharedWithin) + " s: "
                    + N(sharedOff) + " of " + N(restFrames) + " rest frames off, at most " + F(sharedWorst) + " s");
                check.That(stillFrames > 0 && stillWrong == 0, "with more than " + F(StruggleWarning.RestlessFrom, "0.0") + " s to the struggle the float lies still: " + N(stillWrong) + " of " + N(stillFrames) + " frames tilted"
                    + (stillWrong > 0 ? ", the last " + stillSeen : ""));
                check.That(restlessFrames > 0 && restlessSeen >= restlessFrames * 0.9f && mostTilt > 3f && parts > 0, "in the last " + F(StruggleWarning.RestlessFrom, "0.0") + " s it grows restless, trembling toward the fish: "
                    + N(restlessSeen) + " of " + N(restlessFrames) + " frames, up to " + F(mostTilt, "0.0") + " degrees, " + N(parts) + " parts of its look");
                check.That(points >= 3, "the rod line is drawn with points between its ends to bend, " + N(points) + " points");
                check.That(slackFrames > 0 && earlyTaut == 0, "the line is not taut before the Perch's last " + F(tautSeconds) + " s: " + N(earlyTaut) + " of " + N(slackFrames) + " frames taut");
                check.That(tautFrames > 0 && tautSeen == tautFrames && tautBent == 0, "in the Perch's last " + F(tautSeconds) + " s the line goes taut, drawn straight but for its quiver: taut "
                    + N(tautSeen) + " of " + N(tautFrames) + " frames, bent at most " + F(tautBend, "0.000") + " m against " + F(restSag, "0.000") + " m sagging at rest");
                check.That(struggleFrames > 0 && struggleSlack == 0 && struggleBent == 0, "through the struggle the line stays taut, swinging no more than its quiver: slack " + N(struggleSlack) + " of "
                    + N(struggleFrames) + " frames, bent at most " + F(struggleBend, "0.000") + " m");
                check.That(quiverOff <= 3 && quiverLow >= 0f && quiverHigh > quiverLow, "the line quivers by the strain on it: " + N(quiverOff) + " frames off the rule" + (quiverOff > 0 ? ", the last " + quiverSeen : "")
                    + "; " + F(quiverLow) + " at little strain, " + F(quiverHigh) + " strained");

                // Switched off, nothing shows and the float stands upright.
                PlayRules.ForTest = PlayRules.Now.With(struggleWarning: false);
                for (var waited = 0f; waited < 0.5f; waited += Time.deltaTime) yield return null;
                var tilted = 0;
                foreach (var (t, rot, pos) in lookBefore)
                    if (t != null && (Quaternion.Angle(t.localRotation, rot) > 0.5f || Vector3.Distance(t.localPosition, pos) > 0.005f)) tilted++;
                check.That(ff != null && StruggleTells.StateForTest(ff) == null && tilted == 0, "switched off, a hooked float shows no warning and stands upright, " + N(tilted) + " parts tilted");
                if (daylight) check.Note("pictures: warn-still, warn-restless, warn-taut, struggle-strained (" + N(pictured.Count) + " of 4 taken)");

                if (ff != null && ff.GetCatch() != null) SetCatch.Invoke(ff, new object[] { null });
                if (fish != null) fish.OnHooked(null);
            }
            finally
            {
                SelfTestHold.Holding = false;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                if (put != null) ZNetScene.instance.Destroy(put);
                gear.PutBack();
                SelfTestPictures.Forget();
                if (player != null)
                {
                    LookYaw(player) = yaw;
                    LookPitch(player) = pitch;
                }
            }
        }

        /// <summary>How far from straight a taut line may be drawn at a quiver: its swing aside and half of it up and down, with a little room.</summary>
        private static float TautBendAllowed(float quiver) => StruggleWarning.MaxQuiverMetres * quiver * 1.15f + 0.02f;
    }
}
