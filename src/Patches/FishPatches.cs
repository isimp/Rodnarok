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

    /// <summary>
    /// A school's fish, or a called one, swims only for a place it reaches through deep water. The
    /// game writes the new place before it finds the water there too shallow, and the fish swims for
    /// it until a later choice holds; and a place it takes it checks only there and halfway, so the
    /// way can cross a bank. Near a bank, and fleeing at speed, either can run the fish aground. For
    /// these fish a place turned down, or one whose way is not deep enough every metre, leaves the
    /// place they swam for before standing, or none where they had none. The float a fish goes for
    /// is the game's choice and is left to it.
    /// </summary>
    [HarmonyPatch(typeof(Fish), "RandomizeWaypoint")]
    internal static class TurnedDownPlaceHoldsNoFish
    {
        private static readonly AccessTools.FieldRef<Fish, UnityEngine.Vector3> Waypoint = AccessTools.FieldRefAccess<Fish, UnityEngine.Vector3>("m_waypoint");
        private static readonly AccessTools.FieldRef<Fish, bool> HaveWaypoint = AccessTools.FieldRefAccess<Fish, bool>("m_haveWaypoint");
        private static readonly AccessTools.FieldRef<Fish, FishingFloat> WaypointFloat = AccessTools.FieldRefAccess<Fish, FishingFloat>("m_waypointFF");

        internal struct Before
        {
            public UnityEngine.Vector3 Place;
            public bool Had;
        }

        private static void Prefix(Fish __instance, out Before __state) =>
            __state = new Before { Place = Waypoint(__instance), Had = HaveWaypoint(__instance) };

        private static void Postfix(Fish __instance, ref bool __result, Before __state)
        {
            if (!Schools.IsOurs(__instance) && !Schools.IsCalled(__instance)) return;
            if (__result && (WaypointFloat(__instance) != null || Schools.DeepWayTo(__instance, Waypoint(__instance)))) return;
            Waypoint(__instance) = __state.Place;
            HaveWaypoint(__instance) = __state.Had;
            __result = false;
        }
    }
}
