using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Bumps, as a player meets them with the setting on: a school comes to a float, the fish
    /// picked to take the bait bumps it, the float dips a little, reeling right then hooks nothing,
    /// and the real bite still follows. Needs a character beside water at least 3 m deep; the rod
    /// is brought for the run and put back after.
    /// </summary>
    internal static class SelfTestBumps
    {
        private const string Bait = "FishingBait";

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("WithBumpsOnTheFishBumpsTheFloatFirstAndABumpHooksNothing", Bumping);
        }

        private static IEnumerable Bumping(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var spot = SelfTestArrival.DeepWaterBeside(player.transform.position, 3f);
            check.That(spot.HasValue, "water at least 3 m deep within 6 to 20 m of the character (stand facing open water)");
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
                for (var waited = 0f; waited < 3f && Utils.FindChild(player.transform, "_RodTop") == null; waited += Time.deltaTime) yield return null;
                foreach (var frame in SelfTestArrival.UntilNoSchoolLeaves()) yield return frame;

                PlayRules.ForTest = Settings.Rules.With(waitMin: 1f, waitMax: 1f, schools: true, callWorldFish: false, wary: false, bumps: true);
                var bait = baitPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                bait.m_dropPrefab = baitPrefab;
                var go = Object.Instantiate(floatPrefab, spot.Value, Quaternion.identity);
                ff = go.GetComponent<FishingFloat>();
                ff.Setup(player, Vector3.zero, 0f, null, null, bait);

                // The first bump, with the float's height just before it.
                // Heights are taken over the water at the float, so the waves lifting it count for nothing.
                var bumpsBefore = Bites.BumpsForTest;
                var lastHeight = OverWater(ff);
                var heightBefore = lastHeight;
                var bumpedAt = -1f;
                for (var waited = 0f; waited < 60f && bumpedAt < 0f; waited += Time.deltaTime)
                {
                    if (Bites.BumpsForTest > bumpsBefore)
                    {
                        bumpedAt = Time.time;
                        heightBefore = lastHeight;
                        break;
                    }
                    lastHeight = OverWater(ff);
                    yield return null;
                }
                check.That(bumpedAt > 0f, "the fish picked to take the bait bumps the float within 60 s of the cast");
                if (bumpedAt < 0f) yield break;
                check.That(Nibbler(ff) == null || NibbleTime(ff) < bumpedAt - 0.5f, "a bump is no nibble the game knows of");

                // The float dips a little; reeling as it does, as a player taking it for the bite would, hooks nothing.
                var lowest = OverWater(ff);
                for (var waited = 0f; waited < 0.4f; waited += Time.deltaTime)
                {
                    lowest = Mathf.Min(lowest, OverWater(ff));
                    yield return null;
                }
                check.That(lowest < heightBefore - 0.05f, "the bump dips the float under the water's own rise and fall, by " + (heightBefore - lowest).ToString("0.00", CultureInfo.InvariantCulture) + " m");
                SelfTestDraw.Reeling = true;
                for (var waited = 0f; waited < 0.3f; waited += Time.deltaTime) yield return null;
                SelfTestDraw.Reeling = false;
                check.That(ff.GetCatch() == null, "reeling at the bump hooks nothing");

                // The real bite follows.
                var bit = false;
                for (var waited = 0f; waited < 10f && !bit; waited += Time.deltaTime)
                {
                    bit = Nibbler(ff) != null && NibbleTime(ff) > bumpedAt;
                    yield return null;
                }
                check.That(bit, "the real bite follows the bumps within 10 s");
            }
            finally
            {
                SelfTestDraw.Reeling = false;
                PlayRules.ForTest = null;
                if (ff != null) ZNetScene.instance.Destroy(ff.gameObject);
                gear.PutBack();
            }
        }

        /// <summary>How high the float rides over the water's surface at it, waves included.</summary>
        private static float OverWater(FishingFloat ff) => ff.transform.position.y - SelfTestCalm.SurfaceAt(ff.transform.position);
    }
}
