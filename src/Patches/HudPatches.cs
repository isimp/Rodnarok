using HarmonyLib;

namespace Rodnarok
{
    /// <summary>Once the game has set the crosshair for the frame, the bow ring tells the bite and the fight.</summary>
    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    internal static class CrosshairRingDrawn
    {
        private static void Postfix(Hud __instance, float bowDrawPercentage) => CrosshairRing.Draw(__instance, bowDrawPercentage);
    }
}
