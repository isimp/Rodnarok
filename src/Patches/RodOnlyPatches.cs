using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// A projectile's hit on a fish a bait drew finds nothing. This runs first among the hit's
    /// prefixes, before Spearfishing's by name: Harmony runs every prefix whatever an earlier one
    /// returns, so what those see has to change rather than whether they run.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "OnHit")]
    internal static class RodOnlyHit
    {
        [HarmonyPriority(Priority.First)]
        [HarmonyBefore("org.bepinex.plugins.spearfishing")]
        private static void Prefix(Projectile __instance, Collider collider) => RodOnly.Hit(__instance, collider);

        private static System.Exception Finalizer(System.Exception __exception)
        {
            RodOnly.Done();
            return __exception;
        }
    }

    /// <summary>While a projectile's hit is handled, the game's lookup of what it hit answers nothing for a fish a bait drew.</summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.FindHitObject))]
    internal static class RodOnlyHidden
    {
        private static void Postfix(ref GameObject __result) => __result = RodOnly.Seen(__result);
    }
}
