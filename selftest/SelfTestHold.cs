using HarmonyLib;

namespace Rodnarok
{
    /// <summary>
    /// Stands in for the player holding the block key while a scenario holds the line: the game
    /// reels while the caster blocks, and a scenario has no hand on the keys.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.IsBlocking))]
    internal static class SelfTestHold
    {
        /// <summary>Whether the character is held to be blocking.</summary>
        internal static bool Holding;

        private static bool Prefix(Humanoid __instance, ref bool __result)
        {
            if (!Holding || __instance != Player.m_localPlayer) return true;
            __result = true;
            return false;
        }
    }
}
