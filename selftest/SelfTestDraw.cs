using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Stands in for the player holding the attack key while a scenario draws a cast: the game reads
    /// the player's controls each step and a scenario has no hand on the keys. Only the hold is
    /// added; every other control is the player's own.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class SelfTestDraw
    {
        /// <summary>Whether the character is held to be holding the attack key.</summary>
        internal static bool Drawing;

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Player __instance, ref bool attackHold)
        {
            if (Drawing && __instance == Player.m_localPlayer) attackHold = true;
        }
    }
}
