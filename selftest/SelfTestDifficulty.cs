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
    /// float. Then the game itself is fished at the levels: a Perch hooked beside a float and held
    /// through its rests costs half the stamina a second on Very easy that it costs on Normal; a
    /// bite 0.8 s old can no longer be hooked on Very hard, where one 2.2 s old still can on Very
    /// easy. Needs a world of the player's own, since a server's level wins, and for the fishing
    /// water at least 3 m deep beside the character. The setting, the rod and the fish are put back.
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
        private const float RestHeld = 2.5f;

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
            yield return new Scenario("EachDifficultyLevelSetsTheRulesTheGameFishesBy", EachLevelSetsTheRules);
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

        private static IEnumerable EachLevelSetsTheRules(ScenarioCheck check)
        {
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
                }
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

                // Held through its rests, a Perch costs the level's share of the game's stamina.
                var spentAt = new Dictionary<Difficulty, float>();
                foreach (var level in new[] { Difficulty.Normal, Difficulty.VeryEasy })
                {
                    if (!Play(check, level)) yield break;
                    foreach (var frame in hooked.Put(floatPrefab, noBait, perch, spot.Value, player, 0f)) yield return frame;
                    check.That(hooked.Fish != null && hooked.Float != null && hooked.Float.GetCatch() == hooked.Fish, "on " + level + " the Perch put beside the float is hooked");
                    if (hooked.Fish == null || hooked.Float == null || hooked.Float.GetCatch() != hooked.Fish) continue;

                    float held = 0f, spent = 0f, figureOff = 0f;
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
                        held += Time.deltaTime;
                        spent += stamina - player.GetStamina();
                        figureOff = Mathf.Max(figureOff, Mathf.Abs(hooked.Fish.GetStaminaUse() - hooked.Fish.m_staminaUse * PlayRules.Now.ReelStaminaShare));
                    }
                    SelfTestHold.Holding = false;
                    check.That(held >= 1f, "on " + level + " the line was held through the Perch's rests at least 1 s, " + F(held, "0.0") + " s");
                    check.That(figureOff < 0.01f, "on " + level + " the game is told the Perch's own reeling stamina by the level's share, off by at most " + F(figureOff));
                    if (held >= 1f) spentAt[level] = spent / held;
                    hooked.LetGo();
                    yield return null;
                }
                if (spentAt.TryGetValue(Difficulty.Normal, out var normal) && spentAt.TryGetValue(Difficulty.VeryEasy, out var easiest))
                {
                    var share = normal > 0f ? easiest / normal : 0f;
                    check.That(share > 0.35f && share < 0.65f, "reeling in a resting Perch costs half the stamina on Very easy that it costs on Normal: "
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
