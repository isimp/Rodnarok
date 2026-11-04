using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The rod's secondary attack heaves a hooked fish. A Perch is put beside a float in deep water and
    /// hooked; the heave is pressed through the game's own controls (<c>Player.SetControls</c>) as a
    /// player's key or gamepad press reaches it. In a rest with time to spare, a heave must bring the
    /// float about 3 m nearer the rod tip within the lift, for its stamina, leaving the line as long so
    /// it hangs slack; holding then winds the slack in at least twice as fast as the game's reel and
    /// costs no more than the hooked drain. A heave late in a rest, left unwound, must jolt the line by
    /// the slack as the struggle starts. A heave into a struggle must lift nothing and strain the line
    /// a quarter. Switched off, a heave does nothing. Picture: the slack line after a heave. The float
    /// carries no fish's bait; the fish is let go out in the water, and the fish, float and rod are
    /// taken away again.
    /// </summary>
    internal static class SelfTestHeave
    {
        private const string Perch = "Fish1";
        private const string NoBait = "Stone";

        /// <summary>The strain at which the tapping eases off, and at which it holds again, while waiting for the right moment.</summary>
        private const float EaseAt = 0.5f;
        private const float HoldAgainAt = 0.15f;

        /// <summary>How long a wait for the right moment may take, in seconds.</summary>
        private const float WaitSeconds = 40f;

        /// <summary>How high above the float the view is aimed, in metres, so the line and the float both show.</summary>
        private const float AimAbove = 1.2f;

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");
        private static readonly AccessTools.FieldRef<Character, ZSyncAnimation> SyncedAnimation = AccessTools.FieldRefAccess<Character, ZSyncAnimation>("m_zanim");
        private static readonly AccessTools.FieldRef<ZSyncAnimation, Animator> AnimatorOf = AccessTools.FieldRefAccess<ZSyncAnimation, Animator>("m_animator");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AHeaveLiftsARestingFishInLeavesSlackToWindAndJoltsALateStruggle", HeavesTheFish);
        }

        private static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);

        private static void Aim(Player player, FishingFloat ff)
        {
            if (ff == null) return;
            var dir = ff.transform.position + Vector3.up * AimAbove - player.m_eye.position;
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>A press of the secondary attack through the game's own controls, as a key or gamepad press arrives.</summary>
        private static void Press(Player player) =>
            player.SetControls(Vector3.zero, attack: false, attackHold: false, secondaryAttack: true, secondaryAttackHold: false, block: false, blockHold: false, jump: false, crouch: false, run: false, autoRun: false);

        private static float TipToFloat(Player player, FishingFloat ff)
        {
            var tip = Utils.FindChild(player.transform, "_RodTop");
            return tip != null && ff != null ? Vector3.Distance(tip.position, ff.transform.position) : 0f;
        }

        /// <summary>Seconds until the fish's next struggle by the game's own time; infinite while it struggles or none is set.</summary>
        private static float Until(Fish fish)
        {
            if (fish == null || fish.IsEscaping()) return float.PositiveInfinity;
            var until = StruggleTells.NextStruggleForTest(fish) - Time.time;
            return until > 0f ? until : float.PositiveInfinity;
        }

        /// <summary>The state a wait leaves: whether the moment came, the fish still on.</summary>
        private sealed class Moment
        {
            public bool Came;
        }

        /// <summary>
        /// Works the line, tapping through struggles and leaving rests unreeled, until the test says the
        /// moment has come or the wait runs out.
        /// </summary>
        private static IEnumerable WaitFor(Player player, FishingFloat ff, Fish fish, System.Func<bool> moment, Moment result)
        {
            var holding = false;
            for (var waited = 0f; waited < WaitSeconds; waited += Time.deltaTime)
            {
                if (ff == null || ff.GetCatch() != fish) yield break;
                if (moment())
                {
                    result.Came = true;
                    SelfTestHold.Holding = false;
                    yield break;
                }
                var strain = Lines.StrainOf(ff);
                holding = fish.IsEscaping() && (strain >= EaseAt ? false : strain <= HoldAgainAt || holding);
                SelfTestHold.Holding = holding;
                player.AddStamina(player.GetMaxStamina());
                Aim(player, ff);
                yield return null;
            }
            SelfTestHold.Holding = false;
        }

        private static IEnumerable HeavesTheFish(ScenarioCheck check)
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
                PlayRules.ForTest = SelfTestRules.Normal.With(tension: true, pullBySpeed: true, callWorldFish: false, wary: false, baitLost: false, heave: true);
                var daylight = EnvMan.IsDaylight();

                var bait = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = noBait;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                put = Object.Instantiate(perchPrefab, spot.Value + new Vector3(1f, -1f, 0f), Quaternion.identity);
                var fish = put.GetComponent<Fish>();
                yield return null;
                Nibbler(ff) = fish;
                NibbleTime(ff) = Time.time;
                TryToHook.Invoke(ff, null);
                check.That(ff.GetCatch() == fish, "the Perch put beside the float is hooked");
                if (ff.GetCatch() != fish) yield break;
                var skill = player.GetSkillFactor(Skills.SkillType.Fishing);
                var cost = Heaves.CostFor(player);
                var reel = Mathf.Lerp(ff.m_pullLineSpeed, ff.m_pullLineSpeedMaxSkill, skill);
                var drain = Mathf.Lerp(ff.m_hookedStaminaPerSec, ff.m_hookedStaminaPerSecMaxSkill, skill);

                // 1. In a rest with time to spare: the lift, its cost and the slack it leaves.
                var moment = new Moment();
                foreach (var frame in WaitFor(player, ff, fish, () => Until(fish) > 1.6f && Until(fish) < 6f, moment)) yield return frame;
                check.That(moment.Came, "the Perch rests with more than 1.6 s to its next struggle within " + F(WaitSeconds, "0") + " s");
                if (!moment.Came || ff == null) yield break;
                player.AddStamina(player.GetMaxStamina());
                var lifted = Heaves.LiftedForTest;
                var near = TipToFloat(player, ff);
                var line = LineLength(ff);
                var stamina = player.GetStamina();
                // The rod snaps up for the lift in the game's raised pose, which every game sees, and comes down after.
                // The animator the game's synced flags drive, not any animator under the character.
                var zanim = SyncedAnimation(player);
                var animator = zanim != null ? AnimatorOf(zanim) : null;
                var raisedBefore = Heaves.PoseRaisedForTest;
                var posing = false;
                var raisedFrames = 0;
                var liftFrames = 0;
                var pictured = false;
                Press(player);
                for (var waited = 0f; waited < Heave.LiftSeconds + 0.15f; waited += Time.deltaTime)
                {
                    Aim(player, ff);
                    yield return null;
                    posing |= Heaves.PosingForTest(player);
                    if (waited > 0.05f && waited < Heave.LiftSeconds - 0.05f)
                    {
                        liftFrames++;
                        if (animator != null && animator.GetBool("blocking")) raisedFrames++;
                        if (daylight && !pictured && waited > 0.1f)
                        {
                            pictured = true;
                            if (SelfTestPictures.Take("heave-rod-up") is string troubleUp) check.That(false, "a picture of the rod up in the heave is taken: " + troubleUp);
                        }
                    }
                }
                for (var waited = 0f; waited < 0.1f; waited += Time.deltaTime) yield return null;
                if (ff == null || ff.GetCatch() != fish) yield break;
                var downAfter = animator != null && !animator.GetBool("blocking") && !Heaves.PosingForTest(player);
                check.That(posing && liftFrames > 0 && raisedFrames >= liftFrames - 1 && downAfter, "the rod snaps up into the game's raised pose for the lift and comes down after: raised "
                    + raisedFrames.ToString(CultureInfo.InvariantCulture) + " of " + liftFrames.ToString(CultureInfo.InvariantCulture) + " lift frames, down after " + downAfter + "; posing seen " + posing + ", raised by the heave "
                    + (Heaves.PoseRaisedForTest - raisedBefore).ToString(CultureInfo.InvariantCulture) + " times, animator " + (animator != null ? animator.name : "none"));
                var spent = stamina - player.GetStamina();
                var came = near - TipToFloat(player, ff);
                var slack = LineLength(ff) - TipToFloat(player, ff);
                check.That(Heaves.LiftedForTest == lifted + 1, "a press in a rest heaves the fish once, " + (Heaves.LiftedForTest - lifted).ToString(CultureInfo.InvariantCulture) + " heaves");
                check.That(came > Heave.Metres * 0.6f && came < Heave.Metres + 1f, "the heave brings the float about " + F(Heave.Metres, "0") + " m nearer the rod tip within the lift, " + F(came) + " m");
                check.That(Mathf.Abs(LineLength(ff) - line) < 0.1f, "the line stays as long through the lift, " + F(line) + " m to " + F(LineLength(ff)) + " m");
                check.That(slack > 1.5f, "so it hangs slack, " + F(slack) + " m longer than the way to the float");
                check.That(spent > cost * 0.9f && spent < cost + drain * (Heave.LiftSeconds + 0.3f) + 1f, "the heave costs " + F(cost, "0.0") + " stamina, the rod's secondary attack as the game charges an attack; spent " + F(spent, "0.0"));
                if (daylight && SelfTestPictures.Take("heave-slack") is string trouble) check.That(false, "a picture of the slack line is taken: " + trouble);

                // 2. Holding winds the slack in fast, for no more than the hooked drain.
                SelfTestHold.Holding = true;
                player.AddStamina(player.GetMaxStamina());
                var windFrom = LineLength(ff);
                var windStamina = player.GetStamina();
                var wound = 0f;
                for (; wound < 0.35f && ff != null && ff.GetCatch() == fish && !fish.IsEscaping() && LineLength(ff) - TipToFloat(player, ff) > Heave.SlackFrom + 0.2f; wound += Time.deltaTime)
                {
                    Aim(player, ff);
                    yield return null;
                }
                SelfTestHold.Holding = false;
                if (ff == null || ff.GetCatch() != fish) yield break;
                var windRate = wound > 0f ? (windFrom - LineLength(ff)) / wound : 0f;
                var windCost = wound > 0f ? (windStamina - player.GetStamina()) / wound : 0f;
                check.That(wound > 0.1f && windRate > reel * 2f, "holding winds the slack in at least twice as fast as the game's " + F(reel, "0.0") + " m/s reel, " + F(windRate, "0.0") + " m/s over " + F(wound) + " s");
                check.That(wound > 0.1f && windCost < drain + fish.m_staminaUse * 0.5f, "winding slack costs only the hooked drain of " + F(drain, "0.0") + " a second, not the resting Perch's weight; " + F(windCost, "0.0") + " a second");

                // 3. A heave late in a rest, left unwound: the struggle starts with slack out and jolts the line.
                moment = new Moment();
                foreach (var frame in WaitFor(player, ff, fish, () => Until(fish) > 0.5f && Until(fish) < 1f, moment)) yield return frame;
                check.That(moment.Came, "the Perch rests with half a second to a second to its next struggle within " + F(WaitSeconds, "0") + " s");
                if (moment.Came && ff != null)
                {
                    var jolted = Heaves.JoltedForTest;
                    player.AddStamina(player.GetMaxStamina());
                    Press(player);
                    for (var waited = 0f; waited < 2f && Heaves.JoltedForTest == jolted && ff != null && ff.GetCatch() == fish; waited += Time.deltaTime)
                    {
                        player.AddStamina(player.GetMaxStamina());
                        Aim(player, ff);
                        yield return null;
                    }
                    var jolt = Heaves.LastJoltForTest;
                    check.That(Heaves.JoltedForTest == jolted + 1 && jolt.Slack > 1f && Mathf.Abs(jolt.After - Heave.Jolt(jolt.Before, jolt.Slack)) < 0.01f,
                        "the struggle starting with the slack still out jolts the line by it: " + F(jolt.Slack) + " m of slack took the strain from " + F(jolt.Before) + " to " + F(jolt.After));
                }
                if (ff == null || ff.GetCatch() != fish) yield break;

                // 4. A heave into a struggle lifts nothing and strains the line a quarter.
                moment = new Moment();
                foreach (var frame in WaitFor(player, ff, fish, () => fish.IsEscaping() && Lines.StrainOf(ff) < 0.4f, moment)) yield return frame;
                check.That(moment.Came, "the Perch struggles with the strain under 0.4 within " + F(WaitSeconds, "0") + " s");
                if (moment.Came && ff != null)
                {
                    var liftedBefore = Heaves.LiftedForTest;
                    var strained = Heaves.StrainedForTest;
                    var strainBefore = Lines.StrainOf(ff);
                    Press(player);
                    var strainAfter = Lines.StrainOf(ff);
                    check.That(Heaves.StrainedForTest == strained + 1 && Heaves.LiftedForTest == liftedBefore && Mathf.Abs(strainAfter - Heave.StruggleHeave(strainBefore)) < 0.01f,
                        "a heave into a struggle lifts nothing and strains the line a quarter: strain " + F(strainBefore) + " to " + F(strainAfter) + ", " + (Heaves.LiftedForTest - liftedBefore).ToString(CultureInfo.InvariantCulture) + " lifts");
                }
                if (ff == null || ff.GetCatch() != fish) yield break;

                // 5. Switched off, a press in a rest does nothing.
                PlayRules.ForTest = PlayRules.Now.With(heave: false);
                moment = new Moment();
                foreach (var frame in WaitFor(player, ff, fish, () => Until(fish) > 1f && Until(fish) < 6f, moment)) yield return frame;
                if (moment.Came && ff != null)
                {
                    // The float drifts on its own as the game's line pulls it, so what is held against the press is the
                    // heave's own doing: a lift counted, the fish being lifted, and the heave's stamina.
                    player.AddStamina(player.GetMaxStamina());
                    var liftedOff = Heaves.LiftedForTest;
                    var staminaOff = player.GetStamina();
                    Press(player);
                    var liftingOff = Heaves.IsLifting(fish);
                    var waitedOff = 0f;
                    for (; waitedOff < Heave.LiftSeconds + 0.1f; waitedOff += Time.deltaTime) yield return null;
                    var spentOff = staminaOff - player.GetStamina();
                    check.That(Heaves.LiftedForTest == liftedOff && !liftingOff && spentOff < cost * 0.5f + drain * waitedOff + 0.5f, "switched off, a press in a rest heaves nothing: "
                        + (Heaves.LiftedForTest - liftedOff).ToString(CultureInfo.InvariantCulture) + " heaves, lifting " + liftingOff + ", " + F(spentOff, "0.0") + " stamina spent against a heave's " + F(cost, "0.0"));
                }

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
    }
}
