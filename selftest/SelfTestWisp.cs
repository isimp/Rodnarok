using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A Wisplight's wisp guards the float, as a player wearing one meets it: the character puts on
    /// a Wisplight and a float lands in deep water beside it, carrying no fish's bait so nothing
    /// comes to it. The wisp must leave the character for the float and search round it, on every
    /// side and dipping low now and then; the mist clearing must stay over the character while the
    /// wisp keeps a small pocket of its own; a bite must bring the wisp in close over the float and
    /// flare it; a hooked fish must have it follow the float close; with the float gone it must come
    /// back to the character without a jump and be the game's again, its clearing as before; and
    /// with the player's FloatWisp setting off it must stay with the character. When it is night in
    /// game, pictures of the searching wisp and the bite are taken. The setting, the character's
    /// view and gear, the float and the fish are put back after.
    /// </summary>
    internal static class SelfTestWisp
    {
        private const string Wisplight = "Demister";
        private const string NoBait = "Stone";
        private const string BiterPrefab = "Fish1";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");
        private static readonly AccessTools.FieldRef<SE_Demister, GameObject> BallOf = AccessTools.FieldRefAccess<SE_Demister, GameObject>("m_ballInstance");
        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AWisplightsWispSearchesRoundTheFloatDivesInAtABiteAndComesBack", Guards);
        }

        private static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>How far, in degrees, the character looks beside the float, so the character's own body leaves it in sight.</summary>
        private const float AimBeside = 10f;

        private static void Aim(Player player, Vector3 at)
        {
            var dir = Quaternion.Euler(0f, AimBeside, 0f) * (at - player.m_eye.position);
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private static float Across(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static IEnumerable Guards(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.Needs(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            var biterPrefab = ZNetScene.instance.GetPrefab(BiterPrefab);
            check.That(floatPrefab != null && noBait != null && biterPrefab != null, "the float, the " + NoBait + " and the Perch prefabs are there");
            if (floatPrefab == null || noBait == null || biterPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var settingWas = Settings.FloatWispForTest(true);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            GameObject biter = null;
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                var worn = gear.Wear(Wisplight);
                check.That(worn != null, "a Wisplight is worn: " + gear.Trouble);
                if (worn == null) yield break;
                SE_Demister wisplight = null;
                GameObject ball = null;
                for (var waited = 0f; waited < 3f && ball == null; waited += Time.deltaTime)
                {
                    wisplight = player.GetSEMan().GetStatusEffects().OfType<SE_Demister>().FirstOrDefault();
                    ball = wisplight != null ? BallOf(wisplight) : null;
                    yield return null;
                }
                check.That(ball != null, "the Wisplight's wisp shows over the character");
                if (ball == null) yield break;
                var field = ball.GetComponentInChildren<Demister>();
                check.That(field != null && field.m_forceField != null, "the wisp carries the game's mist clearing");
                if (field == null || field.m_forceField == null) yield break;
                // The clearing's own width is the game's prefab's: a float of an earlier scenario may still have the wisp out.
                var prefabField = wisplight.m_ballPrefab != null ? wisplight.m_ballPrefab.GetComponentInChildren<ParticleSystemForceField>(true) : null;
                var ownRange = prefabField != null ? prefabField.endRange : field.m_forceField.endRange;
                var flicker = ball.GetComponentInChildren<Light>();
                var homeIn = 0f;
                for (; homeIn < 10f && FloatWisps.TaskForTest != WispTask.Home; homeIn += Time.deltaTime) yield return null;
                check.That(FloatWisps.TaskForTest == WispTask.Home, "before the cast the wisp is with the character, " + FloatWisps.TaskForTest + " after " + F(homeIn) + " s");
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
                Vector3 Home() => player.transform.position + player.transform.TransformVector(wisplight.m_offset);
                var ownLight = flicker != null ? flicker.intensity : 0f;
                PlayRules.ForTest = Settings.Rules.With(callWorldFish: false, wary: false, baitLost: false);
                var window = PlayRules.Now.HookWindow;

                var bait = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = noBait;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);

                // Out to the float, then searching round it.
                var reached = false;
                var took = 0f;
                for (; took < 10f && !reached; took += Time.deltaTime)
                {
                    reached = Across(ball.transform.position, ff.transform.position) < FloatWisp.SearchRadius + 1f;
                    Aim(player, ff.transform.position);
                    yield return null;
                }
                check.That(reached, "with the float out the wisp goes to it, within " + F(FloatWisp.SearchRadius + 1f) + " m across, " + F(took) + " s; at the end "
                    + F(Across(ball.transform.position, ff.transform.position)) + " m off");
                var view = ball.GetComponent<ZNetView>();
                check.That(view != null && view.IsValid() && view.GetZDO().GetZDOID("RodnarokWispGuards") == player.GetZDOID(), "the wisp is marked for every game as guarding the character's float");

                var farthest = 0f;
                var lowest = float.MaxValue;
                var highest = float.MinValue;
                var sides = new bool[4];
                var pictured = false;
                for (var waited = 0f; waited < 10f; waited += Time.deltaTime)
                {
                    var off = ball.transform.position - ff.transform.position;
                    farthest = Mathf.Max(farthest, Across(ball.transform.position, ff.transform.position));
                    lowest = Mathf.Min(lowest, off.y);
                    highest = Mathf.Max(highest, off.y);
                    if (new Vector2(off.x, off.z).magnitude > 0.5f) sides[(int)((Mathf.Atan2(off.z, off.x) + Mathf.PI) / (2f * Mathf.PI) * 4f) % 4] = true;
                    Aim(player, ff.transform.position);
                    if (!pictured && waited > 3f && EnvMan.IsNight())
                    {
                        var trouble = SelfTestPictures.Take("wisp-search");
                        check.That(trouble == null, "a picture of the searching wisp is taken: " + trouble);
                        pictured = true;
                    }
                    yield return null;
                }
                check.That(farthest < 3.5f, "searching, the wisp keeps near the float, at most " + F(farthest) + " m across");
                check.That(sides.All(s => s), "searching, the wisp goes round every side of the float: " + string.Join("", sides.Select(s => s ? "x" : "-")));
                check.That(lowest < 0.9f && highest > 1.2f, "searching, the wisp drifts up and dips low over the water, from " + F(lowest) + " to " + F(highest) + " m over the float");

                // The clearing stays over the character; the wisp keeps a pocket of its own.
                var kept = FloatWisps.ClearingOverWearerForTest(field);
                check.That(kept != null && kept.m_forceField != null && Mathf.Abs(kept.m_forceField.endRange - ownRange) < 0.01f
                    && Vector3.Distance(kept.transform.position, player.transform.position) < 4f,
                    "the mist clearing stays over the character, " + F(ownRange) + " m wide: " + (kept == null ? "none" : F(kept.m_forceField.endRange) + " m wide, " + F(Vector3.Distance(kept.transform.position, player.transform.position)) + " m from the character"));
                check.That(Mathf.Abs(field.m_forceField.endRange - FloatWisp.PocketRange) < 0.01f, "the wisp clears a pocket of " + F(FloatWisp.PocketRange) + " m round the float: " + F(field.m_forceField.endRange) + " m");

                // A bite: in close over the float, flaring.
                biter = Object.Instantiate(biterPrefab, spot.Value + new Vector3(1f, -1f, 0f), Quaternion.identity);
                var fish = biter.GetComponent<Fish>();
                // Kept still, so it never goes for the float on its own.
                fish.enabled = false;
                Nibbler(ff) = fish;
                NibbleTime(ff) = Time.time;
                yield return null;
                yield return null;
                check.That(FloatWisps.TaskForTest == WispTask.Focus && FloatWisps.BrightForTest > 2f, "right after a bite the wisp flares: " + FloatWisps.TaskForTest + ", " + F(FloatWisps.BrightForTest) + " its own light");
                var close = float.MaxValue;
                var brightest = 0f;
                var picturedBite = false;
                for (var waited = 0f; waited < window - 0.1f; waited += Time.deltaTime)
                {
                    close = Vector3.Distance(ball.transform.position, ff.transform.position + Vector3.up * FloatWisp.FocusHeight);
                    if (flicker != null) brightest = Mathf.Max(brightest, flicker.intensity);
                    Aim(player, ff.transform.position);
                    if (!picturedBite && waited > 0.8f && EnvMan.IsNight())
                    {
                        var trouble = SelfTestPictures.Take("wisp-bite");
                        check.That(trouble == null, "a picture of the wisp at the bite is taken: " + trouble);
                        picturedBite = true;
                    }
                    yield return null;
                }
                check.That(close < 0.7f, "within the hook window the wisp is in close over the float, " + F(close) + " m from " + F(FloatWisp.FocusHeight) + " m over it");
                check.That(flicker != null && brightest > ownLight * 1.5f, "the wisp's light flares at the bite, " + F(brightest) + " against " + F(ownLight));

                // Hooked: it follows the float close.
                Nibbler(ff) = fish;
                NibbleTime(ff) = Time.time;
                yield return null;
                TryToHook.Invoke(ff, null);
                for (var waited = 0f; waited < 2f; waited += Time.deltaTime) yield return null;
                check.That(ff.GetCatch() == fish, "reeling within the hook window hooks the fish");
                var following = Vector3.Distance(ball.transform.position, ff.transform.position + Vector3.up * FloatWisp.FollowHeight);
                check.That(FloatWisps.TaskForTest == WispTask.Follow && following < 1.2f, "hooked, the wisp follows the float close: " + FloatWisps.TaskForTest + ", " + F(following) + " m from its place");
                if (ff.GetCatch() != null) SetCatch.Invoke(ff, new object[] { null });
                fish.OnHooked(null);

                // The float gone: back to the character without a jump, and the game's again.
                ZNetScene.instance.Destroy(ff.gameObject);
                ff = null;
                var last = ball.transform.position;
                var jump = 0f;
                var back = false;
                var backIn = 0f;
                for (; backIn < 10f && !back; backIn += Time.deltaTime)
                {
                    yield return null;
                    jump = Mathf.Max(jump, Vector3.Distance(ball.transform.position, last));
                    last = ball.transform.position;
                    back = FloatWisps.TaskForTest == WispTask.Home && view.GetZDO().GetZDOID("RodnarokWispGuards").IsNone();
                }
                check.That(back, "with the float gone the wisp is the game's again within 10 s: " + F(backIn) + " s, " + F(Vector3.Distance(ball.transform.position, Home())) + " m from its place over the character");
                check.That(jump < 1f, "coming back the wisp never jumps, at most " + F(jump) + " m in a frame");
                yield return null;
                yield return null;
                check.That(Mathf.Abs(field.m_forceField.endRange - ownRange) < 0.01f && FloatWisps.ClearingOverWearerForTest(field) == null,
                    "back with the character the wisp's clearing is its own again, " + F(field.m_forceField.endRange) + " m wide, no copy kept");

                // With the setting off the wisp stays with the character.
                Settings.FloatWispForTest(false);
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);
                for (var waited = 0f; waited < 3f; waited += Time.deltaTime) yield return null;
                check.That(FloatWisps.TaskForTest == WispTask.Home && Vector3.Distance(ball.transform.position, Home()) < FloatWisp.HandBack,
                    "with FloatWisp off the wisp stays with the character, " + F(Vector3.Distance(ball.transform.position, Home())) + " m from its place over it");
                if (!EnvMan.IsNight()) check.Note("no pictures of the wisp: it is day in game; run the self-test at night to see it");
            }
            finally
            {
                Settings.FloatWispForTest(settingWas);
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                if (biter != null) ZNetScene.instance.Destroy(biter);
                gear.PutBack();
                if (player != null)
                {
                    LookYaw(player) = yaw;
                    LookPitch(player) = pitch;
                }
            }
            check.That(gear.AsBefore(), "the character's gear and what it knows are as before the run: " + gear.Difference());
        }
    }
}
