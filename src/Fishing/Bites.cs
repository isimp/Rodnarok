using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The bite and the fight, on the game's own float. A fish bites only with its mouth at the
    /// hook, a metre under the float where the game sends it, measured through all three axes.
    /// The bite pulls the float under for about a second. Holding the line hooks the fish for as
    /// long as the hook window lasts, which the game sets at half a second. Holding it while the
    /// fish struggles stalls: no line comes in, and it costs the stall cost.
    /// </summary>
    internal static class Bites
    {
        /// <summary>How near the hook, in metres, a fish's mouth must be for its bite to count.</summary>
        public const float Reach = 0.6f;

        private static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");
        private static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");
        private static readonly AccessTools.FieldRef<FishingFloat, Rigidbody> Body = AccessTools.FieldRefAccess<FishingFloat, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");

        private static readonly Dictionary<FishingFloat, float> PulledUntil = new Dictionary<FishingFloat, float>();
        private static readonly Dictionary<FishingFloat, float> BumpedUntil = new Dictionary<FishingFloat, float>();

        /// <summary>How many bumps floats of this game took, for the self-test.</summary>
        [ForTest]
        public static int BumpsForTest { get; private set; }

        /// <summary>How far the fish's mouth was from the hook at the last bite that counted, for the self-test; below zero before any.</summary>
        [ForTest]
        public static float LastBiteDistanceForTest { get; private set; } = -1f;

        /// <summary>Where the hook hangs: a metre under the float, where the game sends a fish going for it.</summary>
        public static Vector3 Hook(FishingFloat ff) => ff.transform.position + Vector3.down;

        /// <summary>Whether a fish's bite at this float counts: only with the fish at the hook.</summary>
        public static bool Counts(FishingFloat ff, Fish fish)
        {
            var hook = Hook(ff);
            var mouth = fish.transform.position;
            var at = Bite.FishAtHook(new Spot(mouth.x, mouth.y, mouth.z), new Spot(hook.x, hook.y, hook.z), Reach);
            if (at) LastBiteDistanceForTest = Vector3.Distance(mouth, hook);
            return at;
        }

        /// <summary>A bite was taken by the float: it is pulled under for a while.</summary>
        public static void Bitten(FishingFloat ff)
        {
            if (Nibbler(ff) != null && Time.time - NibbleTime(ff) < 0.01f) PulledUntil[ff] = Time.time + Bite.PullSeconds;
        }

        /// <summary>A fish bumps the float (<see cref="Bumps"/>): a short, light pull, and no nibble the game knows of, so nothing to hook.</summary>
        public static void Bump(FishingFloat ff)
        {
            if (ff == null || ff.GetCatch() != null) return;
            BumpedUntil[ff] = Time.time + Bumps.PullSeconds;
            BumpsForTest++;
        }

        /// <summary>The float's step: a bitten float with nothing on the line is pulled under.</summary>
        public static void Pull(FishingFloat ff)
        {
            PullFor(ff, PulledUntil, Bite.Pull);
            PullFor(ff, BumpedUntil, Bumps.Pull);
        }

        /// <summary>Lets go of a float leaving the world.</summary>
        public static void Forget(FishingFloat ff)
        {
            PulledUntil.Remove(ff);
            BumpedUntil.Remove(ff);
        }

        private static void PullFor(FishingFloat ff, Dictionary<FishingFloat, float> pulls, float pull)
        {
            if (!pulls.TryGetValue(ff, out var until)) return;
            if (ff == null || Time.time >= until || ff.GetCatch() != null)
            {
                pulls.Remove(ff);
                return;
            }
            if (ff.IsInWater()) Body(ff).AddForce(Vector3.down * pull, ForceMode.Acceleration);
        }

        /// <summary>Reeling now hooks the fish that bit if the hook window still lasts; the game's own half second is then made to hold too.</summary>
        public static void StretchWindow(FishingFloat ff)
        {
            if (Nibbler(ff) == null || ff.GetCatch() != null) return;
            if (Bite.InHookWindow(Time.time - NibbleTime(ff), PlayRules.Now.HookWindow)) NibbleTime(ff) = Time.time;
        }

        /// <summary>The line before the float's step, so a struggle can give back what reeling took.</summary>
        public static float LineBefore(FishingFloat ff) => LineLength(ff);

        /// <summary>After the float's step: while the hooked fish struggles, reeling gains no line.</summary>
        public static void StallLine(FishingFloat ff, float before)
        {
            if (ff == null) return;
            var fish = ff.GetCatch();
            if (fish != null && fish.IsEscaping() && LineLength(ff) < before) LineLength(ff) = before;
        }

        /// <summary>The stamina figure a struggling fish gives the game, so holding through a struggle costs the stall cost; null while it rests.</summary>
        public static float? StruggleFigure(Fish fish)
        {
            if (!fish.IsEscaping()) return null;
            var item = fish.GetComponent<ItemDrop>();
            return Fight.StaminaFigureWhileStruggling(PlayRules.Now, item == null ? 1 : item.m_itemData.m_quality);
        }
    }
}
