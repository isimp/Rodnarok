using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A float glows at night and flashes at a bite, as a player meets it: a float lands in deep
    /// water beside the character, carrying no fish's bait so nothing comes to it. At full night
    /// it must carry a soft yellow light just above it; a bite must flash it white and brighter,
    /// fading back to the glow over the hook window; hooking the fish must end the flash at once;
    /// by day the light must be out, and with the player's FloatGlow setting off there must be no
    /// light at all. The night is set for the checks; when it is really night in game, pictures
    /// of the waiting glow and the flash are taken as well. The setting, the character's view, the
    /// float, the fish and the rod are put back after.
    /// </summary>
    internal static class SelfTestGlow
    {
        private const string NoBait = "Stone";
        private const string BiterPrefab = "Fish1";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly System.Reflection.MethodInfo TryToHook = AccessTools.Method(typeof(FishingFloat), "TryToHook");
        private static readonly System.Reflection.MethodInfo SetCatch = AccessTools.Method(typeof(FishingFloat), "SetCatch");
        private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
        private static readonly AccessTools.FieldRef<Player, float> LookPitch = AccessTools.FieldRefAccess<Player, float>("m_lookPitch");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("AFloatGlowsAtNightAndFlashesWhiteAtABite", GlowsAndFlashes);
        }

        private static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Seen(Light light) =>
            light == null ? "no light" : (light.enabled ? "lit" : "out") + ", colour " + F(light.color.r) + "/" + F(light.color.g) + "/" + F(light.color.b)
                + ", intensity " + F(light.intensity) + ", range " + F(light.range);

        private static bool Yellow(Light light) => light != null && Mathf.Abs(light.color.g - RingDraw.YellowG) < 0.03f && light.color.b < 0.1f;

        /// <summary>The character looks just above the float, as a player watching it would.</summary>
        private static void Aim(Player player, FishingFloat ff)
        {
            if (ff == null) return;
            var dir = ff.transform.position + Vector3.up * 1.2f - player.m_eye.position;
            if (dir.sqrMagnitude < 0.01f) return;
            player.SetLookDir(dir);
            LookPitch(player) = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        private static IEnumerable GlowsAndFlashes(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
            if (!spot.HasValue) yield break;
            var floatPrefab = ZNetScene.instance.GetPrefab("FishingRodFloat");
            var noBait = ObjectDB.instance.GetItemPrefab(NoBait);
            var biterPrefab = ZNetScene.instance.GetPrefab(BiterPrefab);
            check.That(floatPrefab != null && noBait != null && biterPrefab != null, "the float, the " + NoBait + " and the Perch prefabs are there");
            if (floatPrefab == null || noBait == null || biterPrefab == null) yield break;

            var gear = SelfTestGear.RodInHand(player);
            var settingWas = Settings.FloatGlowForTest(true);
            var yaw = LookYaw(player);
            var pitch = LookPitch(player);
            FishingFloat ff = null;
            GameObject biter = null;
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
                PlayRules.ForTest = Settings.Rules.With(callWorldFish: false, wary: false, baitLost: false);
                var window = PlayRules.Now.HookWindow;

                var bait = noBait.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = noBait;
                ff = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity).GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);

                // Full night: a soft yellow glow just above the float.
                FloatGlows.DarknessForTest = 1f;
                yield return null;
                yield return null;
                var light = FloatGlows.LightForTest(ff);
                check.That(light != null && light.enabled && light.type == LightType.Point, "at night the float carries a lit point light: " + Seen(light));
                if (light == null) yield break;
                check.That(light.transform.IsChildOf(ff.transform) && Vector3.Distance(light.transform.position, ff.transform.position) < 0.5f, "the light rides on the float, "
                    + F(Vector3.Distance(light.transform.position, ff.transform.position)) + " m from it");
                check.That(Yellow(light) && Mathf.Abs(light.intensity - FloatGlow.GlowIntensity) < 0.01f && Mathf.Abs(light.range - FloatGlow.GlowRange) < 0.01f,
                    "waiting, the float glows soft yellow at intensity " + F(FloatGlow.GlowIntensity) + " over " + F(FloatGlow.GlowRange) + " m: " + Seen(light));
                var night = EnvMan.IsNight();
                if (night)
                {
                    FloatGlows.DarknessForTest = null;
                    for (var waited = 0f; waited < 0.5f; waited += Time.deltaTime)
                    {
                        Aim(player, ff);
                        yield return null;
                    }
                    var trouble = SelfTestPictures.Take("glow-waiting");
                    check.That(trouble == null, "a picture of the waiting glow is taken: " + trouble);
                    FloatGlows.DarknessForTest = 1f;
                }

                // A bite: a white, brighter flash that fades back over the hook window.
                biter = Object.Instantiate(biterPrefab, spot.Value + new Vector3(1f, -1f, 0f), Quaternion.identity);
                var fish = biter.GetComponent<Fish>();
                // Kept still, so it never goes for the float on its own.
                fish.enabled = false;
                Nibbler(ff) = fish;
                NibbleTime(ff) = Time.time;
                yield return null;
                check.That(light.enabled && light.color.b > 0.8f && light.intensity > FloatGlow.GlowIntensity * 2f && light.range > FloatGlow.GlowRange,
                    "right after a bite the float flashes white and brighter: " + Seen(light));
                if (night)
                {
                    Aim(player, ff);
                    var trouble = SelfTestPictures.Take("glow-bite");
                    check.That(trouble == null, "a picture of the bite's flash is taken: " + trouble);
                }
                var flash = light.intensity;
                for (var waited = 0f; waited < window / 2f; waited += Time.deltaTime) yield return null;
                check.That(light.intensity < flash && light.intensity > FloatGlow.GlowIntensity, "half the hook window on, the flash has faded part way: " + Seen(light));
                for (var waited = 0f; waited < window / 2f + 0.2f; waited += Time.deltaTime) yield return null;
                check.That(Yellow(light) && Mathf.Abs(light.intensity - FloatGlow.GlowIntensity) < 0.01f, "once the hook window is out the float glows soft yellow again: " + Seen(light));

                // Hooking ends the flash at once, though the game's half second is stretched on the hook.
                Nibbler(ff) = fish;
                NibbleTime(ff) = Time.time;
                yield return null;
                TryToHook.Invoke(ff, null);
                yield return null;
                check.That(ff.GetCatch() == fish, "reeling within the hook window hooks the fish");
                check.That(Yellow(light) && Mathf.Abs(light.intensity - FloatGlow.GlowIntensity) < 0.01f, "hooked, the float glows soft yellow, no flash: " + Seen(light));
                if (ff.GetCatch() != null) SetCatch.Invoke(ff, new object[] { null });
                fish.OnHooked(null);

                // By day the light is out.
                FloatGlows.DarknessForTest = 0f;
                yield return null;
                check.That(!light.enabled, "by day the float's light is out: " + Seen(light));

                // With the player's setting off no float carries a light.
                FloatGlows.DarknessForTest = 1f;
                Settings.FloatGlowForTest(false);
                yield return null;
                check.That(FloatGlows.LightForTest(ff) == null && light == null, "with FloatGlow off the float carries no light");
                if (!night) check.Note("no pictures of the glow: it is day in game; run the self-test at night to see it");
            }
            finally
            {
                FloatGlows.DarknessForTest = null;
                Settings.FloatGlowForTest(settingWas);
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
        }
    }
}
