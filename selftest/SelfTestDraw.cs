using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Stands in for the player holding the attack key while a scenario draws a cast, or the block
    /// key while it reels, through the controls the game reads each step: a scenario has no hand on
    /// the keys, and the keys reach the game this way. Only the hold is added; every other control
    /// is the player's own.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class SelfTestDraw
    {
        /// <summary>Whether the character is held to be holding the attack key.</summary>
        internal static bool Drawing;

        /// <summary>Whether the character is held to be holding the block key, as reeling does.</summary>
        internal static bool Reeling;

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, ref bool attackHold, ref bool blockHold)
        {
            if (__instance != Player.m_localPlayer) return;
            if (Drawing) attackHold = true;
            if (Reeling) blockHold = true;
        }
    }
}
