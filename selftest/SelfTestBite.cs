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
    /// cost while holding it through a rest reels in. The fish is let go before it is landed, so
    /// nothing reaches the inventory; the rod, the float and the school are taken away again.
    /// </summary>
    internal static class SelfTestBite
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ABiteComesAtTheHookPullsTheFloatUnderAndAFightStallsWhileItStruggles", BiteAndFight);
        }

        private static IEnumerable BiteAndFight(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3.5f);
            check.That(spot.HasValue, "water at least 3.5 m deep within 6 to 14 m of the character (stand at a deep shore or on a dock)");
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
                var tipShown = false;
                for (var waited = 0f; waited < 3f && !tipShown; waited += Time.deltaTime)
                {
                    tipShown = Utils.FindChild(player.transform, "_RodTop") != null;
                    yield return null;
                }
                if (!tipShown) yield break;

                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, leaveSeconds: 2f);
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
                    yield return null;
                }
                check.That(biter != null, "a fish bit within 90 s");
                if (biter == null) yield break;
                check.That(Bites.LastBiteDistanceForTest >= 0f && Bites.LastBiteDistanceForTest <= Bites.Reach,
                    "the fish bit with its mouth within " + Bites.Reach.ToString("0.0", CultureInfo.InvariantCulture) + " m of the hook, at " + Bites.LastBiteDistanceForTest.ToString("0.00", CultureInfo.InvariantCulture) + " m");

                // The float goes under, and stays under for about a second.
                var deepest = 0f;
                var late = 0f;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime)
                {
                    var under = resting - ff.transform.position.y;
                    deepest = Mathf.Max(deepest, under);
                    if (waited >= 0.8f && late == 0f) late = under;
                    yield return null;
                }
                check.That(deepest >= 0.3f, "the float went under by at least 0.3 m, by " + deepest.ToString("0.00", CultureInfo.InvariantCulture) + " m");
                check.That(late >= 0.15f, "the float was still under 0.8 s after the bite, by " + late.ToString("0.00", CultureInfo.InvariantCulture) + " m");

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

                // Holding the line: a struggle stalls for the stall cost, a rest reels in.
                var skill = player.GetSkillFactor(Skills.SkillType.Fishing);
                var expected = rules.StallStaminaPerSecond * Mathf.Lerp(1f, rules.SkillCostShare, skill) + Mathf.Lerp(ff.m_hookedStaminaPerSec, ff.m_hookedStaminaPerSecMaxSkill, skill);
                float struggleTime = 0f, struggleStamina = 0f, struggleLine = 0f, restTime = 0f, restLine = 0f;
                SelfTestHold.Holding = true;
                for (var waited = 0f; waited < 15f; waited += Time.deltaTime)
                {
                    if (ff == null || ff.GetCatch() != fish || LineLength(ff) < 3f || player.GetStamina() < 15f) break;
                    var struggled = fish.IsEscaping();
                    var line = LineLength(ff);
                    var stamina = player.GetStamina();
                    yield return null;
                    if (ff == null || ff.GetCatch() != fish) break;
                    if (struggled && fish.IsEscaping())
                    {
                        struggleTime += Time.deltaTime;
                        struggleStamina += stamina - player.GetStamina();
                        struggleLine += line - LineLength(ff);
                    }
                    else if (!struggled && !fish.IsEscaping())
                    {
                        restTime += Time.deltaTime;
                        restLine += line - LineLength(ff);
                    }
                }
                SelfTestHold.Holding = false;
                check.That(struggleTime >= 0.5f, "the fish struggled at least half a second while the line was held, " + struggleTime.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                if (struggleTime > 0f)
                {
                    check.That(Mathf.Abs(struggleLine) < 0.01f, "holding through a struggle gained no line, " + struggleLine.ToString("0.000", CultureInfo.InvariantCulture) + " m in " + struggleTime.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                    var rate = struggleStamina / struggleTime;
                    check.That(rate > expected * 0.6f && rate < expected * 1.4f, "holding through a struggle cost " + rate.ToString("0.0", CultureInfo.InvariantCulture) + " stamina a second, the stall cost with the hooked drain is " + expected.ToString("0.0", CultureInfo.InvariantCulture));
                }
                check.That(restTime >= 0.5f && restLine / Mathf.Max(restTime, 0.01f) > 0.5f * Mathf.Lerp(rules.ReelSpeed, rules.ReelSpeedMaxSkill, skill),
                    "holding while it rests reels it in, " + restLine.ToString("0.0", CultureInfo.InvariantCulture) + " m in " + restTime.ToString("0.0", CultureInfo.InvariantCulture) + " s");

                // Let go before it is landed.
                if (ff != null && ff.GetCatch() != null) SetCatch.Invoke(ff, new object[] { null });
                fish.OnHooked(null);
            }
            finally
            {
                SelfTestHold.Holding = false;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }
    }
}
