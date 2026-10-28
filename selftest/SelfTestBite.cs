using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The bite and the fight as a player meets them: a float with Fishing Bait lands in deep water
    /// beside the character and the school comes. The fish must bite only with its mouth at the
    /// hook, the float must go under for about a second, reeling must hook within the hook window
    /// and not after it, and holding the line through a struggle must gain no line for the stall
    /// cost and strain the line at the tension's pace, while easing off lets the strain go as the
    /// fish takes line out and holding through a rest reels in. The ring's arc must follow the
    /// strain, and held too long the line must snap. Tension is on for it, at the server's numbers.
    /// The character watches the float throughout, and the bite, a held struggle, an eased one and
    /// a rest are grabbed with captions into <c>wiki-fight.png</c>, the picture for the mod's pages;
    /// the character's view is put back after.
    /// The fish is let go or freed before it is landed, so nothing reaches the inventory; the rod,
    /// the float and the school are taken away again.
    /// </summary>
    internal static class SelfTestBite
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly AccessTools.FieldRef<Fish, float> WaterLevel = AccessTools.FieldRefAccess<Fish, float>("m_inWater");

        /// <summary>The line left when the scenario lets the fish go: well out in the water, never at the bank.</summary>
        private const float LetGoAt = 8f;

        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        private static UnityEngine.UI.Image Ring => CrosshairRing.ImageForTest;

        private static bool RingShown() => Ring != null && Ring.gameObject.activeSelf;

        /// <summary>The strain at which the scenario eases off in a struggle, and at which it holds again.</summary>
        private const float EaseAt = 0.55f;

        /// <summary>How long a struggling fish is given to rise to the surface before its depth counts, in seconds.</summary>
        private const float RiseSeconds = 0.5f;
        private const float HoldAgainAt = 0.05f;

        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        /// <summary>How far above the float the character looks, so the float shows under the crosshair ring.</summary>
        private const float AimAbove = 1.2f;

        /// <summary>The panels of the fight picture for the mod's pages, in reading order, with their captions.</summary>
        private static readonly string[] WikiPanels = { "bite", "struggle", "ease", "rest" };
        private const string WikiPicture = "wiki-fight";

        /// <summary>The character looks just above the float, as a player watching it would.</summary>
        private static void Aim(Player player, FishingFloat ff)
        {
            if (ff == null) return;
            var dir = ff.transform.position + Vector3.up * AimAbove - player.m_eye.position;
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Whether this run takes the fight picture: only in daylight in game, since the water and the float are unseen at night.</summary>
        private static bool _wikiLight;

        /// <summary>The frame the last whole-screen picture was taken in.</summary>
        private static int _pictureFrame = -1;

        /// <summary>Grabs a panel of the fight picture once, noting why when it cannot.</summary>
        private static void WikiPanel(ScenarioCheck check, string name, string caption)
        {
            // Never in a frame a whole-screen picture is taken, which would catch the caption.
            if (!_wikiLight || SelfTestPictures.Has(name) || Time.frameCount == _pictureFrame) return;
            var trouble = SelfTestPictures.Grab(name, caption);
            check.That(trouble == null, "the fight picture's panel " + name + " is grabbed: " + trouble);
        }

        /// <summary>
        /// Whether the ring shows a struggle (red) or a rest (yellow) at its one size, its arc the
        /// line's strain over a faint track of the same colour.
        /// </summary>
        private static bool RingIs(bool red, float strain)
        {
            if (!RingShown()) return false;
            var ring = Ring;
            var track = CrosshairRing.TrackForTest;
            var sized = Mathf.Abs(ring.transform.localScale.x - RingSize) < 0.01f;
            var arc = Mathf.Abs(ring.fillAmount - strain) < 0.06f;
            var faint = track != null && track.gameObject.activeSelf && track.color.a > 0.1f && track.color.a < 0.5f
                && Mathf.Abs(track.color.g - ring.color.g) < 0.01f && Mathf.Abs(track.transform.localScale.x - RingSize) < 0.01f;
            return sized && arc && faint && (red
                ? ring.color.r > 0.8f && ring.color.g < 0.4f
                : ring.color.r > 0.9f && ring.color.g > 0.8f && ring.color.b < 0.1f);
        }

        /// <summary>The ring as drawn, for a failure's message.</summary>
        private static string RingSeen()
        {
            if (!RingShown()) return "hidden";
            var track = CrosshairRing.TrackForTest;
            return "arc " + Ring.fillAmount.ToString("0.00", CultureInfo.InvariantCulture) + ", colour " + Ring.color + ", size " + Ring.transform.localScale.x.ToString("0.00", CultureInfo.InvariantCulture)
                + ", track " + (track != null && track.gameObject.activeSelf ? track.color.ToString() : "hidden");
        }

        /// <summary>Whether a rate measured over frames is within a third of the rule's.</summary>
        private static bool Near(float measured, float rule) => rule > 0f && measured > rule * 0.67f && measured < rule * 1.33f;

        /// <summary>The ring's one size, as the ring is drawn the moment after a bite.</summary>
        private static float RingSize = -1f;

        /// <summary>Takes a picture of the screen for looking at; notes why when none can be taken.</summary>
        private static bool Picture(ScenarioCheck check, string what)
        {
            _pictureFrame = Time.frameCount;
            var trouble = SelfTestPictures.Take(what);
            check.That(trouble == null, "a picture of " + what + " is taken: " + trouble);
            return true;
        }

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ABiteComesAtTheHookPullsTheFloatUnderAndAHeldStruggleStrainsTheLine", BiteAndFight);
        }

        private static IEnumerable BiteAndFight(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var baitPrefab = ObjectDB.instance.GetItemPrefab(Bait);
            if (floatPrefab == null || baitPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                var tipShown = false;
                for (var waited = 0f; waited < 3f && !tipShown; waited += Time.deltaTime)
                {
                    tipShown = Utils.FindChild(player.transform, "_RodTop") != null;
                    yield return null;
                }
                if (!tipShown) yield break;

                SelfTestPictures.Forget();
                _wikiLight = EnvMan.IsDaylight();
                // The school of a scenario before may still be leaving, and a new one waits for it.
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, tension: true, callWorldFish: false, wary: false, baitLost: false);
                var rules = PlayRules.Now;
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);

                // A bite, with the fish's mouth at the hook.
                var since = Time.time;
                var resting = ff.transform.position.y;
                Fish biter = null;
                for (var waited = 0f; waited < 90f && biter == null; waited += Time.deltaTime)
                {
                    if (NibbleTime(ff) > since && Nibbler(ff) != null) biter = Nibbler(ff);
                    else resting = Mathf.Lerp(resting, ff.transform.position.y, 0.1f);
                    Aim(player, ff);
                    yield return null;
                }
                check.That(biter != null, "a fish bit within 90 s");
                if (biter == null) yield break;
                check.That(Bites.LastBiteDistanceForTest >= 0f && Bites.LastBiteDistanceForTest <= Bites.Reach,
                    "the fish bit with its mouth within " + Bites.Reach.ToString("0.0", CultureInfo.InvariantCulture) + " m of the hook, at " + Bites.LastBiteDistanceForTest.ToString("0.00", CultureInfo.InvariantCulture) + " m");

                // The float goes under, and stays under for about a second.
                // Meanwhile the crosshair ring opens wide and white and closes toward yellow as the window runs out.
                var deepest = 0f;
                var late = 0f;
                Color? ringEarly = null;
                var scaleEarly = 0f;
                var fillEarly = 0f;
                var pictured = false;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime)
                {
                    var under = resting - ff.transform.position.y;
                    deepest = Mathf.Max(deepest, under);
                    if (waited >= 0.8f && late == 0f) late = under;
                    if (waited >= 0.1f && ringEarly == null && RingShown())
                    {
                        ringEarly = Ring.color;
                        scaleEarly = Ring.transform.localScale.x;
                        fillEarly = Ring.fillAmount;
                        RingSize = scaleEarly;
                    }
                    if (waited >= 0.3f && !pictured)
                    {
                        pictured = true;
                        Picture(check, "ring-bite");
                    }
                    // The panel a frame later than the picture, so the caption stays out of the picture.
                    if (pictured && waited >= 0.33f) WikiPanel(check, "bite", "Bite: hold to hook");
                    Aim(player, ff);
                    yield return null;
                }
                check.That(deepest >= 0.3f, "the float went under by at least 0.3 m, by " + deepest.ToString("0.00", CultureInfo.InvariantCulture) + " m");
                check.That(late >= 0.15f, "the float was still under 0.8 s after the bite, by " + late.ToString("0.00", CultureInfo.InvariantCulture) + " m");
                check.That(ringEarly != null, "the crosshair ring showed right after the bite");
                if (ringEarly != null)
                {
                    var now = Ring;
                    check.That(ringEarly.Value.b > 0.6f && fillEarly > 0.8f, "right after the bite the ring's arc is near full and white, arc " + fillEarly.ToString("0.00", CultureInfo.InvariantCulture) + ", blue " + ringEarly.Value.b.ToString("0.00", CultureInfo.InvariantCulture));
                    check.That(RingShown() && now.fillAmount < fillEarly - 0.4f && now.color.b < ringEarly.Value.b - 0.2f, "a second after the bite the arc has drained and turned yellower, arc " + now.fillAmount.ToString("0.00", CultureInfo.InvariantCulture) + ", blue " + now.color.b.ToString("0.00", CultureInfo.InvariantCulture));
                    check.That(Mathf.Abs(now.transform.localScale.x - scaleEarly) < 0.01f, "the ring keeps its size while the arc drains, " + scaleEarly.ToString("0.00", CultureInfo.InvariantCulture) + " then " + now.transform.localScale.x.ToString("0.00", CultureInfo.InvariantCulture));
                    check.That(!Hud.instance.m_crosshairBow.gameObject.activeSelf || Hud.instance.m_crosshairBow.color.a < 0.05f, "the game's bow ring is not shown beside it");
                }

                // Reeling after the hook window hooks nothing; within it, past the game's half second, it hooks.
                Nibbler(ff) = biter;
                NibbleTime(ff) = Time.time - (rules.HookWindow + 0.1f);
                TryToHook.Invoke(ff, null);
                check.That(ff.GetCatch() == null, "reeling " + (rules.HookWindow + 0.1f).ToString("0.0", CultureInfo.InvariantCulture) + " s after a bite hooks nothing");
                Nibbler(ff) = biter;
                NibbleTime(ff) = Time.time - (rules.HookWindow - 0.3f);
                TryToHook.Invoke(ff, null);
                var fish = ff.GetCatch();
                check.That(fish == biter, "reeling " + (rules.HookWindow - 0.3f).ToString("0.0", CultureInfo.InvariantCulture) + " s after a bite hooks the fish");
                if (fish == null) yield break;
                yield return null;

                // Holding the line in rhythm, as a player would: through a rest it reels in, through a
                // struggle it stalls for the stall cost and strains the line, and eased off in time the
                // strain goes while the fish takes line out. Near the bank the rests are left unreeled.
                // A character without food has 50 stamina and a hooked fish lets none come back, so the
                // character is kept on full stamina, topped up before each frame's reading so the cost
                // the game takes within the frame is still measured as it is.
                var skill = player.GetSkillFactor(Skills.SkillType.Fishing);
                var expected = rules.StallStaminaPerSecond * Mathf.Lerp(1f, rules.SkillCostShare, skill) + Mathf.Lerp(ff.m_hookedStaminaPerSec, ff.m_hookedStaminaPerSecMaxSkill, skill);
                float struggleTime = 0f, heldTime = 0f, heldStamina = 0f, heldLine = 0f, strained = 0f;
                float easedTime = 0f, easedLineOut = 0f, easedRule = 0f, eased = 0f, restTime = 0f, restHeld = 0f, restLine = 0f;
                var ringWrong = 0;
                var ringSeen = "";
                float struggleDepth = 0f, restDepth = 0f;
                // A struggle's depth is measured once the fish has had time to rise to the surface.
                float settledDepth = 0f, settledTime = 0f, struggleFrom = -1f;
                var splashesThen = Thrash.SplashesForTest;
                var splashesBefore = Thrash.SplashesForTest;
                var restSplashes = 0;
                var picturedStruggle = false;
                var picturedRest = false;
                var holding = true;
                for (var waited = 0f; waited < 30f; waited += Time.deltaTime)
                {
                    if (ff == null || ff.GetCatch() != fish) break;
                    if (heldTime >= 0.5f && easedTime >= 0.5f && restHeld >= 0.5f) break;
                    var struggled = fish.IsEscaping();
                    var line = LineLength(ff);
                    player.AddStamina(player.GetMaxStamina());
                    var stamina = player.GetStamina();
                    var strain = Lines.StrainOf(ff);
                    if (struggled) holding = strain >= EaseAt ? false : strain <= HoldAgainAt || holding;
                    else holding = line > LetGoAt + 3f;
                    SelfTestHold.Holding = holding;
                    Aim(player, ff);
                    yield return null;
                    if (ff == null || ff.GetCatch() != fish) break;
                    var strainNow = Lines.StrainOf(ff);
                    var depth = WaterLevel(fish) - fish.transform.position.y;
                    var splashesAtStep = splashesThen;
                    splashesThen = Thrash.SplashesForTest;
                    if (struggled && fish.IsEscaping())
                    {
                        struggleDepth += depth * Time.deltaTime;
                        struggleTime += Time.deltaTime;
                        if (struggleFrom < 0f) struggleFrom = Time.time;
                        if (Time.time - struggleFrom >= RiseSeconds)
                        {
                            settledDepth += depth * Time.deltaTime;
                            settledTime += Time.deltaTime;
                        }
                        if (holding)
                        {
                            heldTime += Time.deltaTime;
                            heldStamina += stamina - player.GetStamina();
                            heldLine += line - LineLength(ff);
                            strained += strainNow - strain;
                        }
                        else
                        {
                            easedTime += Time.deltaTime;
                            easedLineOut += LineLength(ff) - line;
                            easedRule += rules.RunSpeed * (1f - (strain + strainNow) / 2f) * Time.deltaTime;
                            eased += strain - strainNow;
                        }
                        if (!RingIs(red: true, strainNow)) { ringWrong++; ringSeen = RingSeen(); }
                        if (!picturedStruggle && holding && strainNow > 0.25f) picturedStruggle = Picture(check, "ring-struggle");
                        if (holding && strainNow >= 0.45f) WikiPanel(check, "struggle", "Struggle: holding strains");
                        if (!holding && easedTime > 0.2f && strainNow > 0.15f && strainNow < 0.35f) WikiPanel(check, "ease", "Ease off: it takes line");
                    }
                    else if (!struggled && !fish.IsEscaping())
                    {
                        struggleFrom = -1f;
                        restDepth += depth * Time.deltaTime;
                        restTime += Time.deltaTime;
                        restSplashes += Thrash.SplashesForTest - splashesAtStep;
                        if (holding)
                        {
                            restHeld += Time.deltaTime;
                            restLine += line - LineLength(ff);
                        }
                        if (!RingIs(red: false, strainNow)) { ringWrong++; ringSeen = RingSeen(); }
                        if (!picturedRest && restTime > 0.3f) picturedRest = Picture(check, "ring-rest");
                        if (holding && restLine > 0.5f) WikiPanel(check, "rest", "Rest: reel it in");
                    }
                }
                SelfTestHold.Holding = false;
                var splashes = Thrash.SplashesForTest - splashesBefore;
                if (struggleTime > 0.5f)
                {
                    var high = settledTime > 0.2f ? settledDepth / settledTime : struggleDepth / struggleTime;
                    check.That(Mathf.Abs(high) < 0.3f, "a struggling fish thrashes at the surface once risen, " + high.ToString("0.00", CultureInfo.InvariantCulture) + " m under it on average past the first "
                        + RiseSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s of each struggle, " + (struggleDepth / struggleTime).ToString("0.00", CultureInfo.InvariantCulture) + " m over the whole");
                    check.That(splashes / struggleTime >= 2f, "a struggling fish splashes at least twice a second, " + (splashes / struggleTime).ToString("0.0", CultureInfo.InvariantCulture) + " a second");
                }
                if (restTime > 0.5f) check.That(restSplashes == 0, "a resting fish splashes no more than the game has it, " + restSplashes.ToString(CultureInfo.InvariantCulture) + " extra splashes at rest");
                check.That(ringWrong <= 3, ringWrong.ToString(CultureInfo.InvariantCulture) + " frames of the fight showed the ring wrong: red in a struggle, yellow at rest, its arc the line's strain over a faint track, at the bite's size; the last " + ringSeen);
                check.That(heldTime >= 0.5f, "the line was held through a struggle at least half a second, " + heldTime.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                if (heldTime > 0.2f)
                {
                    check.That(Mathf.Abs(heldLine) < 0.01f, "holding through a struggle gained no line, " + heldLine.ToString("0.000", CultureInfo.InvariantCulture) + " m in " + heldTime.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                    var rate = heldStamina / heldTime;
                    check.That(rate > expected * 0.6f && rate < expected * 1.4f, "holding through a struggle cost " + rate.ToString("0.0", CultureInfo.InvariantCulture) + " stamina a second, the stall cost with the hooked drain is " + expected.ToString("0.0", CultureInfo.InvariantCulture));
                    var strainRate = strained / heldTime;
                    check.That(Near(strainRate, 1f / rules.SnapSeconds), "holding through a struggle strains the line to snapping in " + rules.SnapSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s, " + (strainRate * 100f).ToString("0", CultureInfo.InvariantCulture) + "% a second");
                }
                check.That(easedTime >= 0.5f, "the line was eased off in a struggle at least half a second, " + easedTime.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                if (easedTime > 0.2f)
                {
                    check.That(Near(eased / easedTime, 1f / rules.EaseSeconds), "easing off lets the strain go over " + rules.EaseSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s, " + (eased / easedTime * 100f).ToString("0", CultureInfo.InvariantCulture) + "% a second");
                    check.That(Near(easedLineOut / easedTime, easedRule / easedTime), "eased off, the struggling fish takes line out at " + rules.RunSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " m/s less the strain still on the line, "
                        + (easedRule / easedTime).ToString("0.0", CultureInfo.InvariantCulture) + " m/s by the strain seen, " + (easedLineOut / easedTime).ToString("0.0", CultureInfo.InvariantCulture) + " m/s");
                }
                // Rests near the bank are left unreeled, so the reel is measured over the time the line was held.
                check.That(restHeld >= 0.5f && restLine / Mathf.Max(restHeld, 0.01f) > 0.5f * Mathf.Lerp(rules.ReelSpeed, rules.ReelSpeedMaxSkill, skill),
                    "holding while it rests reels it in, " + restLine.ToString("0.0", CultureInfo.InvariantCulture) + " m in " + restHeld.ToString("0.0", CultureInfo.InvariantCulture) + " s held");

                // The four moments together, as the picture for the mod's pages.
                yield return null;
                if (_wikiLight)
                {
                    var sheet = SelfTestPictures.Compose(WikiPicture, WikiPanels);
                    check.That(sheet == null, "the fight picture for the mod's pages is put together from bite, struggle, ease and rest: " + sheet);
                }
                else
                {
                    check.Note("the picture for the mod's pages was left out: it is night in game, run again in daylight for it");
                }

                // Held through a struggle too long, the line snaps as the game breaks one.
                if (ff != null && ff.GetCatch() == fish)
                {
                    PlayRules.ForTest = rules.With(snapSeconds: 0.3f);
                    var snappedBefore = Lines.SnappedForTest;
                    // Kept on full stamina as above. The line is held only while the fish struggles, which
                    // is all a snap needs; held through a rest it would be reeled in and landed instead.
                    var struggledSince = false;
                    for (var waited = 0f; waited < 20f && ff != null && ff.GetCatch() == fish; waited += Time.deltaTime)
                    {
                        player.AddStamina(player.GetMaxStamina());
                        var struggling = fish.IsEscaping();
                        struggledSince |= struggling;
                        SelfTestHold.Holding = struggling;
                        yield return null;
                    }
                    SelfTestHold.Holding = false;
                    check.That(Lines.SnappedForTest == snappedBefore + 1, "held through a struggle past the snap time, the line snapped; a struggle came within 20 s " + struggledSince);
                    check.That(ff == null || ff.GetCatch() == null, "after the snap nothing is on the line");
                    check.That(fish == null || !fish.IsHooked(), "after the snap the fish swims free");
                    PlayRules.ForTest = rules;
                }

                // Let go before it is landed.
                if (ff != null && ff.GetCatch() != null) SetCatch.Invoke(ff, new object[] { null });
                if (fish != null) fish.OnHooked(null);
            }
            finally
            {
                SelfTestHold.Holding = false;
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
