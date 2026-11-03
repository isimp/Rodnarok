using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The Difficulty setting, as a player sets it, changes what the fishing plays by at once. Each
    /// level is set in turn and the rules the game fishes by are read back: the hook window, the
    /// share of the game's reeling stamina, whether schools are wary and whether fish bump the
    /// float, and the fields it sets show its numbers; on Hard an edited HookWindow switches the
    /// level to Custom, which plays it and keeps Hard's other numbers. Then the game itself is
    /// fished at the levels: a Perch hooked beside a float and held through its rests costs what
    /// the game's own drain gives on Normal and on Very easy, both its parts halved on Very easy; a bite 0.8 s old can no longer be
    /// hooked on Very hard, where one 2.2 s old still can on Very easy. Needs a world of the
    /// player's own, since a server's level wins, and a level, not Custom, whose numbers would be
    /// written over; for the fishing, water at least 3 m deep beside the character. The setting,
    /// the rod and the fish are put back.
    /// </summary>
    internal static class SelfTestDifficulty
    {
        /// <summary>Each level with the hook window, the share of the game's reeling stamina, wary schools and bumps it sets.</summary>
        private static readonly (Difficulty Level, float HookWindow, float ReelShare, bool Wary, bool Bumps)[] Levels =
        {
            (Difficulty.VeryEasy, 2.5f, 0.5f, false, false),
            (Difficulty.Easy, 2f, 0.75f, true, false),
            (Difficulty.Normal, 1.5f, 1f, true, false),
            (Difficulty.Hard, 1f, 1f, true, true),
            (Difficulty.VeryHard, 0.5f, 1f, true, true),
        };

        /// <summary>An item no fish takes, put on the float so no school or fish of the world comes to it.</summary>
        private const string NoBait = "Stone";

        /// <summary>How long the line is held through the fish's rests at each level, in seconds.</summary>
        private const float RestHeld = 3f;

        /// <summary>How long a fish is worked at most for its rests, in seconds.</summary>
        private const float WorkedAtMost = 40f;

        /// <summary>The line at which the fish is let go, well out in the water.</summary>
        private const float LetGoAt = 8f;

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("EachDifficultyLevelSetsTheRulesAndShowsInItsFieldsAndAnEditMakesItCustom", EachLevelSetsTheRules);
            yield return new Scenario("VeryEasyReelsForHalfTheStaminaAndVeryHardGivesLessTimeToHook", LevelsInTheGame);
        }

        private static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>Sets a level as a player would; false where the setting does not take it, as on a server, whose level wins.</summary>
        private static bool Play(ScenarioCheck check, Difficulty level)
        {
            Settings.DifficultyForTest(level);
            var took = Settings.Difficulty == level;
            check.Needs(took, "a world of your own: the setting took " + level + " (a server's level wins)");
            return took;
        }

        /// <summary>Whether a scenario may change the level: not on Custom, whose numbers are the player's own and would be written over.</summary>
        private static bool MayChangeTheLevel(ScenarioCheck check)
        {
            var custom = Settings.Difficulty == Difficulty.Custom;
            check.Needs(!custom, "Difficulty at a level, not Custom: playing the levels writes over the numbers Custom keeps");
            return !custom;
        }

        private static IEnumerable EachLevelSetsTheRules(ScenarioCheck check)
        {
            if (!MayChangeTheLevel(check)) yield break;
            var was = Settings.Difficulty;
            try
            {
                PlayRules.ForTest = null;
                foreach (var (level, hookWindow, reelShare, wary, bumps) in Levels)
                {
                    if (!Play(check, level)) yield break;
                    yield return null;
                    var rules = PlayRules.Now;
                    check.That(Mathf.Approximately(rules.HookWindow, hookWindow), level + " gives " + F(hookWindow, "0.0") + " s to hook, the rules say " + F(rules.HookWindow, "0.0") + " s");
                    check.That(Mathf.Approximately(rules.ReelStaminaShare, reelShare), level + " reels for " + F(reelShare * 100f, "0") + "% of the game's stamina, the rules say " + F(rules.ReelStaminaShare * 100f, "0") + "%");
                    check.That(rules.Wary == wary, level + (wary ? " has wary schools" : " has no wary schools") + ", the rules say " + (rules.Wary ? "wary" : "not wary"));
                    check.That(rules.Bumps == bumps, level + (bumps ? " has fish bump the float" : " has no bumps") + ", the rules say " + (rules.Bumps ? "bumps" : "none"));
                    var fields = Settings.FieldsForTest;
                    check.That(DifficultyLevels.Matches(level, fields) && Mathf.Approximately(fields.HookWindow, hookWindow), level + " shows its numbers in the fields it sets, HookWindow showing "
                        + F(fields.HookWindow, "0.0") + " s, WaryHearing " + F(fields.Hearing) + ", BaitLossChances " + BaitLoss.ChancesText(fields.BaitTakeChances));
                }

                // A field the level sets edited by hand: the level turns Custom and plays the field as set, keeping the rest.
                if (!Play(check, Difficulty.Hard)) yield break;
                var hard = PlayRules.Now;
                Settings.HookWindowForTest(1.2f);
                yield return null;
                var edited = PlayRules.Now;
                check.That(Settings.Difficulty == Difficulty.Custom, "editing HookWindow on Hard switches Difficulty to Custom; it is " + Settings.Difficulty);
                check.That(Mathf.Approximately(edited.HookWindow, 1.2f), "on Custom the edited HookWindow is played, " + F(edited.HookWindow, "0.0") + " s");
                check.That(Mathf.Approximately(edited.Hearing, hard.Hearing) && edited.FrightToLeave == hard.FrightToLeave && edited.Bumps == hard.Bumps && Mathf.Approximately(edited.SnapSeconds, hard.SnapSeconds),
                    "the other numbers stay Hard's: hearing " + F(edited.Hearing) + ", frights " + edited.FrightToLeave.ToString(CultureInfo.InvariantCulture) + ", snap " + F(edited.SnapSeconds) + " s, bumps " + (edited.Bumps ? "on" : "off"));
            }
            finally
            {
                Settings.DifficultyForTest(was);
            }
        }

        private static IEnumerable LevelsInTheGame(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            var perch = ZNetScene.instance.GetPrefab("Fish1");
            check.That(floatPrefab != null && noBait != null && perch != null, "the float, the " + NoBait + " and the Perch prefabs are there");
            if (floatPrefab == null || noBait == null || perch == null) yield break;
            if (!MayChangeTheLevel(check)) yield break;

            var was = Settings.Difficulty;
            var gear = SelfTestGear.RodInHand(player);
            var hooked = new Hooked();
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = null;

                // Held through its rests, a Perch costs what the game's own drain gives for the level's
                // figures: the hooked drain every hooked fish costs and the reeling drain, the fish's
                // figure by its stars, both lowered by the level's share.
                var reelAt = new Dictionary<Difficulty, float>();
                var hookedAt = new Dictionary<Difficulty, float>();
                var spentAt = new Dictionary<Difficulty, float>();
                foreach (var level in new[] { Difficulty.Normal, Difficulty.VeryEasy })
                {
                    if (!Play(check, level)) yield break;
                    foreach (var frame in hooked.Put(floatPrefab, noBait, perch, spot.Value, player, 0f)) yield return frame;
                    check.That(hooked.Fish != null && hooked.Float != null && hooked.Float.GetCatch() == hooked.Fish, "on " + level + " the Perch put beside the float is hooked");
                    if (hooked.Fish == null || hooked.Float == null || hooked.Float.GetCatch() != hooked.Fish) continue;

                    float held = 0f, spent = 0f, expected = 0f, hookedPart = 0f, reelPart = 0f, figureOff = 0f;
                    var quality = 1;
                    var skill = 0f;
                    for (var worked = 0f; worked < WorkedAtMost && held < RestHeld; worked += Time.deltaTime)
                    {
                        var ff = hooked.Float;
                        var fish = hooked.Fish;
                        if (ff == null || fish == null || ff.GetCatch() != fish || LineLength(ff) <= LetGoAt) break;
                        var resting = !fish.IsEscaping();
                        // Eased off through every struggle, so only the rests are paid for.
                        SelfTestHold.Holding = resting;
                        player.AddStamina(player.GetMaxStamina());
                        var stamina = player.GetStamina();
                        yield return null;
                        if (hooked.Float == null || hooked.Fish == null || hooked.Float.GetCatch() != hooked.Fish) break;
                        if (!resting || hooked.Fish.IsEscaping()) continue;
                        ff = hooked.Float;
                        fish = hooked.Fish;
                        skill = player.GetSkillFactor(Skills.SkillType.Fishing);
                        var item = fish.GetComponent<ItemDrop>();
                        quality = item == null ? 1 : item.m_itemData.m_quality;
                        // The game's drain while the line is held, as FishingFloat charges it a step.
                        var hookedDrain = Mathf.Lerp(ff.m_hookedStaminaPerSec, ff.m_hookedStaminaPerSecMaxSkill, skill);
                        var pull = ff.m_pullStaminaUse + fish.GetStaminaUse() * quality;
                        var reelDrain = Mathf.Lerp(pull, pull * ff.m_pullStaminaUseMaxSkillMultiplier, skill);
                        held += Time.deltaTime;
                        spent += stamina - player.GetStamina();
                        expected += (hookedDrain + reelDrain) * Time.deltaTime;
                        hookedPart += hookedDrain * Time.deltaTime;
                        reelPart += reelDrain * Time.deltaTime;
                        figureOff = Mathf.Max(figureOff, Mathf.Abs(fish.GetStaminaUse() - fish.m_staminaUse * PlayRules.Now.ReelStaminaShare));
                    }
                    SelfTestHold.Holding = false;
                    check.That(held >= 1f, "on " + level + " the line was held through the Perch's rests at least 1 s, " + F(held, "0.0") + " s");
                    check.That(figureOff < 0.01f, "on " + level + " the game is told the Perch's own reeling stamina by the level's share, off by at most " + F(figureOff));
                    if (held >= 1f)
                    {
                        var rate = spent / held;
                        var due = expected / held;
                        check.That(due > 0f && rate > due * 0.8f && rate < due * 1.25f, "on " + level + " holding a resting Perch costs what the game's drain gives: " + F(rate) + " a second spent, "
                            + F(due) + " due (" + F(hookedPart / held) + " hooked + " + F(reelPart / held) + " reeling, " + quality.ToString(CultureInfo.InvariantCulture) + " star, Fishing "
                            + F(skill * 100f, "0") + ") over " + F(held, "0.0") + " s");
                        reelAt[level] = reelPart / held;
                        hookedAt[level] = hookedPart / held;
                        spentAt[level] = rate;
                    }
                    hooked.LetGo();
                    yield return null;
                }
                foreach (var (part, at) in new[] { ("reeling drain", reelAt), ("hooked drain", hookedAt), ("whole stamina spent", spentAt) })
                {
                    if (!at.TryGetValue(Difficulty.Normal, out var normal) || !at.TryGetValue(Difficulty.VeryEasy, out var easiest)) continue;
                    var share = normal > 0f ? easiest / normal : 0f;
                    // The parts come from the game's own figures, so they halve closely; the stamina spent is measured over a few seconds.
                    var (low, high) = part == "whole stamina spent" ? (0.35f, 0.65f) : (0.45f, 0.55f);
                    check.That(share > low && share < high, "the " + part + " of a resting Perch is half on Very easy what it is on Normal: "
                        + F(easiest) + " against " + F(normal) + " a second, " + F(share * 100f, "0") + "%");
                }

                // A bite past the level's hook window hooks nothing; one within it hooks.
                foreach (var (level, sinceBite, hooks) in new[] { (Difficulty.VeryHard, 0.8f, false), (Difficulty.VeryEasy, 2.2f, true) })
                {
                    if (!Play(check, level)) yield break;
                    foreach (var frame in hooked.Put(floatPrefab, noBait, perch, spot.Value, player, sinceBite)) yield return frame;
                    var caught = hooked.Float != null && hooked.Fish != null && hooked.Float.GetCatch() == hooked.Fish;
                    check.That(caught == hooks, "on " + level + " a bite " + F(sinceBite, "0.0") + " s old " + (hooks ? "is still hooked" : "is no longer hooked") + "; it " + (caught ? "was" : "was not"));
                    hooked.LetGo();
                    yield return null;
                }
            }
            finally
            {
                SelfTestHold.Holding = false;
                hooked.LetGo();
                Settings.DifficultyForTest(was);
                PlayRules.ForTest = null;
                gear.PutBack();
            }
        }

        /// <summary>A Perch put beside a float carrying no fish's bait and hooked through the game's own hook, its bite the age given.</summary>
        private sealed class Hooked
        {
            public FishingFloat Float { get; private set; }
            public Fish Fish { get; private set; }

            public IEnumerable Put(GameObject floatPrefab, GameObject noBait, GameObject fishPrefab, Vector3 at, Player player, float sinceBite)
            {
                var bait = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = noBait;
                Float = Object.Instantiate(floatPrefab, at, Quaternion.identity).GetComponent<FishingFloat>();
                Float.Setup(player, Vector3.zero, 0f, null, null, bait);
                Fish = Object.Instantiate(fishPrefab, at + new Vector3(1f, -1f, 0f), Quaternion.identity).GetComponent<Fish>();
                yield return null;
                if (Float == null || Fish == null) yield break;
                Nibbler(Float) = Fish;
                NibbleTime(Float) = Time.time - sinceBite;
                TryToHook.Invoke(Float, null);
            }

            /// <summary>Lets the fish go well out in the water and takes it and the float away.</summary>
            public void LetGo()
            {
                if (Float != null && Float.GetCatch() != null) SetCatch.Invoke(Float, new object[] { null });
                if (Fish != null) Fish.OnHooked(null);
                if (Float != null) ZNetScene.instance.Destroy(Float.gameObject);
                if (Fish != null) ZNetScene.instance.Destroy(Fish.gameObject);
                Float = null;
                Fish = null;
            }
        }
    }
}
