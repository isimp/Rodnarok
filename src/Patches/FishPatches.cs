using System;
using HarmonyLib;

namespace Rodnarok
{
    /// <summary>A fish of a school goes for no float but its own, and only once it is the one let to; a called fish goes for the float it is called to; the world's own fish otherwise choose as the game has them.</summary>
    [HarmonyPatch(typeof(Fish), "FindFloat")]
    internal static class SchoolFloatChoice
    {
        private static bool Prefix(Fish __instance, ref FishingFloat __result)
        {
            if (!Schools.IsOurs(__instance) && !Schools.IsCalled(__instance)) return true;
            __result = Schools.FloatFor(__instance);
            return false;
        }
    }

    /// <summary>
    /// The fish of a school, and a fish of the world's own while it is called to a float, pay no
    /// attention to a player's noise. The game asks who is heard at a fish's place while it moves
    /// the fish, so for the time such a fish is moved that answer is nobody; the world's own fish
    /// otherwise still hear as the game has them.
    /// </summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.CustomFixedUpdate))]
    internal static class SchoolHearsNoNoise
    {
        /// <summary>The school's fish being moved now; null while another fish, or none, is.</summary>
        internal static Fish Moving;

        private static void Prefix(Fish __instance) => Moving = Schools.IsOurs(__instance) || Schools.IsCalled(__instance) ? __instance : null;

        private static Exception Finalizer(Exception __exception)
        {
            Moving = null;
            return __exception;
        }
    }

    /// <summary>The noise answer for a school's fish: nobody is heard.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetPlayerNoiseRange))]
    internal static class NoiseUnheardBySchools
    {
        private static bool Prefix(ref Player __result)
        {
            if (SchoolHearsNoNoise.Moving == null) return true;
            __result = null;
            return false;
        }
    }

    /// <summary>A hooked fish shows its struggle at the surface.</summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.CustomFixedUpdate))]
    internal static class StruggleShows
    {
        private static void Postfix(Fish __instance) => Thrash.Step(__instance);
    }

    /// <summary>A fish leaving the world is let go of by the thrashing.</summary>
    [HarmonyPatch(typeof(Fish), "OnDisable")]
    internal static class StruggleForgets
    {
        private static void Postfix(Fish __instance) => Thrash.Forget(__instance);
    }

    /// <summary>When a school's fish is hooked, the rest of it scatters.</summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.OnHooked))]
    internal static class SchoolScattersOnHook
    {
        private static void Postfix(Fish __instance, FishingFloat ff) => Schools.Hooked(__instance, ff);
    }
}
