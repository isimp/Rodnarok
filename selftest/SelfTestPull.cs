using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Each fish pulls the line by its own speed in the game, and a player tapping the line through
    /// its struggles wins it back. A Perch, a Pike, a Tuna and an Anglerfish are each put beside a
    /// float in deep water in turn and hooked, and the line is worked as a player would: tapped
    /// through every struggle to keep the strain low, held through every rest to reel in. Each must
    /// strain a held line at its own snap time, take line eased off at its own run speed, swim as
    /// fast as the line goes out, and be reeled in from the cast to 8 m within two minutes. The
    /// Tuna must strain the line faster than the Anglerfish. How long each fight took and the
    /// stamina it cost at the character's Fishing skill are noted, the character kept on full
    /// stamina meanwhile. The float carries no fish's bait, so nothing else comes to it; each fish
    /// is let go well out in the water, never landed, so nothing reaches the inventory, and the
    /// fish, the float and the rod are taken away again.
    /// </summary>
    internal static class SelfTestPull
    {
        /// <summary>The fish fought, from the slowest to the fastest of the game, with the Perch whose pull the settings set.</summary>
        private static readonly (string Prefab, string Name)[] Kinds =
        {
            ("Fish9", "Anglerfish"),
            ("Fish1", "Perch"),
            ("Fish2", "Pike"),
            ("Fish3", "Tuna"),
        };

        /// <summary>An item no fish takes, put on the float so no school or fish of the world comes to it.</summary>
        private const string NoBait = "Stone";

        /// <summary>The line at which a fish counts as reeled in and is let go, well out in the water.</summary>
        private const float ReeledInAt = 8f;

        /// <summary>The strain at which the tapping eases off, and at which it holds again.</summary>
        private const float EaseAt = 0.4f;
        private const float HoldAgainAt = 0.2f;

        /// <summary>How long a fight may take, in seconds.</summary>
        private const float FightSeconds = 120f;

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("EachFishPullsTheLineByItsOwnSpeedAndATappedFightWinsItBack", EachFishPulls);
        }

        /// <summary>Whether a rate measured over frames is within a third of the rule's.</summary>
        private static bool Near(float measured, float rule) => rule > 0f && measured > rule * 0.67f && measured < rule * 1.33f;

        private static string F(float value, string format = "0.00") => value.ToString(format, CultureInfo.InvariantCulture);

        private static IEnumerable EachFishPulls(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            check.That(floatPrefab != null && noBait != null, "the float and the " + NoBait + " prefabs are there");
            if (floatPrefab == null || noBait == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            FishingFloat ff = null;
            GameObject put = null;
            var strainRates = new Dictionary<string, float>();
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
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;
                PlayRules.ForTest = Settings.Rules.With(tension: true, pullBySpeed: true, callWorldFish: false, wary: false, baitLost: false);
                var rules = PlayRules.Now;
                var skill = player.GetSkillFactor(Skills.SkillType.Fishing);

                foreach (var (prefabName, name) in Kinds)
                {
                    var prefab = ZNetScene.instance.GetPrefab(prefabName);
                    check.That(prefab != null, "the " + name + " prefab " + prefabName + " is there");
                    if (prefab == null) continue;
                    var speed = FishBook.Current?.Speed(prefabName) ?? 0f;
                    var snapSeconds = Tension.SnapSeconds(speed, rules);
                    var runSpeed = Tension.RunSpeed(speed, rules);

                    // A float with no fish's bait at the deep spot, and the fish beside it, hooked.
                    var bait = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                    bait.m_dropPrefab = noBait;
                    ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                    ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                    put = Object.Instantiate(prefab, spot.Value + new Vector3(1f, -1f, 0f), Quaternion.identity);
                    var fish = put.GetComponent<Fish>();
                    yield return null;
                    if (ff == null || fish == null) continue;
                    Nibbler(ff) = fish;
                    NibbleTime(ff) = Time.time;
                    TryToHook.Invoke(ff, null);
                    check.That(ff.GetCatch() == fish, "the " + name + " put beside the float is hooked");
                    if (ff.GetCatch() != fish) continue;

                    // Worked as a player would: tapped through each struggle, held through each rest.
                    var lineFrom = LineLength(ff);
                    float heldTime = 0f, strained = 0f, easedTime = 0f, easedLineOut = 0f, easedRule = 0f, spent = 0f, fought = 0f;
                    int struggles = 0, runFrames = 0, pacedWrong = 0;
                    var pacedSeen = "";
                    var holding = true;
                    var wasStruggling = false;
                    var reeledIn = false;
                    for (; fought < FightSeconds; fought += Time.deltaTime)
                    {
                        if (ff == null || ff.GetCatch() != fish) break;
                        if (LineLength(ff) <= ReeledInAt)
                        {
                            reeledIn = true;
                            break;
                        }
                        var struggled = fish.IsEscaping();
                        if (struggled && !wasStruggling) struggles++;
                        wasStruggling = struggled;
                        var line = LineLength(ff);
                        player.AddStamina(player.GetMaxStamina());
                        var stamina = player.GetStamina();
                        var strain = Lines.StrainOf(ff);
                        holding = struggled ? (strain >= EaseAt ? false : strain <= HoldAgainAt || holding) : true;
                        SelfTestHold.Holding = holding;
                        yield return null;
                        if (ff == null || ff.GetCatch() != fish) break;
                        spent += stamina - player.GetStamina();
                        if (!struggled || !fish.IsEscaping()) continue;
                        var strainNow = Lines.StrainOf(ff);
                        if (holding)
                        {
                            heldTime += Time.deltaTime;
                            strained += strainNow - strain;
                            continue;
                        }
                        easedTime += Time.deltaTime;
                        easedLineOut += LineLength(ff) - line;
                        easedRule += runSpeed * (1f - (strain + strainNow) / 2f) * Time.deltaTime;
                        if (!Lines.IsRunning(fish)) continue;
                        runFrames++;
                        var given = Thrash.RunSpeedForTest(fish);
                        var paying = Lines.RunSpeedOf(fish);
                        if (Mathf.Abs(given - paying) > Mathf.Max(0.05f, paying * 0.25f))
                        {
                            pacedWrong++;
                            pacedSeen = F(given) + " m/s given, the line " + F(paying) + " m/s";
                        }
                    }
                    SelfTestHold.Holding = false;

                    var lead = "the " + name + " (speed " + F(speed, "0.0") + ")";
                    check.That(struggles > 0, lead + " struggled at least once in the fight");
                    if (heldTime > 0.2f)
                    {
                        var rate = strained / heldTime;
                        strainRates[prefabName] = rate;
                        check.That(Near(rate, 1f / snapSeconds), lead + " strains a held line to snapping in " + F(snapSeconds) + " s, measured " + F(rate > 0f ? 1f / rate : 0f) + " s");
                    }
                    if (easedTime > 0.2f)
                        check.That(Near(easedLineOut / easedTime, easedRule / easedTime), lead + " takes line eased off at " + F(runSpeed, "0.0") + " m/s less the strain, "
                            + F(easedRule / easedTime, "0.0") + " m/s by the strain seen, " + F(easedLineOut / easedTime, "0.0") + " m/s");
                    if (runFrames > 0)
                        check.That(pacedWrong <= 3, lead + " swims as fast as the line goes out: " + pacedWrong.ToString(CultureInfo.InvariantCulture) + " of " + runFrames.ToString(CultureInfo.InvariantCulture)
                            + " frames off" + (pacedWrong > 0 ? ", the last " + pacedSeen : ""));
                    check.That(reeledIn, lead + " is reeled in from " + F(lineFrom, "0") + " m to " + F(ReeledInAt, "0") + " m within " + F(FightSeconds, "0") + " s by tapping through its struggles, line now "
                        + (ff != null ? F(LineLength(ff), "0.0") : "gone") + " m after " + F(fought, "0") + " s");
                    check.Note(name + ": " + F(lineFrom, "0") + " m to " + F(ReeledInAt, "0") + " m in " + F(fought, "0") + " s, " + struggles.ToString(CultureInfo.InvariantCulture) + " struggles, "
                        + F(spent, "0") + " stamina at Fishing " + F(skill * 100f, "0"));

                    // Let go well out in the water, the fish and the float taken away.
                    if (ff != null && ff.GetCatch() != null) SetCatch.Invoke(ff, new object[] { null });
                    if (fish != null) fish.OnHooked(null);
                    if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                    ff = null;
                    if (put != null) ZNetScene.instance.Destroy(put);
                    put = null;
                    yield return null;
                }

                if (strainRates.TryGetValue("Fish3", out var tuna) && strainRates.TryGetValue("Fish9", out var angler))
                    check.That(tuna > angler * 1.2f, "a held line strains faster on the Tuna than on the Anglerfish, " + F(tuna) + " against " + F(angler) + " a second");
            }
            finally
            {
                SelfTestHold.Holding = false;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                if (put != null) ZNetScene.instance.Destroy(put);
                gear.PutBack();
            }
        }
    }
}
