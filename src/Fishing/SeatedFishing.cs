using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Fishing while sitting: on a chair, a bench, a boat's bench or the ground, a player holding a
    /// rod casts and reels without standing up. The game stands a seated player up on any attack or
    /// block, cancels a drawn cast while attached to a seat, and lets nobody attack in the sitting
    /// pose. While such a player holds still, those three see them as not seated for the moment
    /// they ask; moving, jumping or crouching still stands them up as in the game.
    /// </summary>
    internal static class SeatedFishing
    {
        /// <summary>The ammunition a fishing rod casts; a rod of another mod casting bait counts too.</summary>
        private const string Bait = "$item_fishingbait";

        private static readonly AccessTools.FieldRef<Player, string> EmoteState = AccessTools.FieldRefAccess<Player, string>("m_emoteState");

        /// <summary>The seated player the game is asking about now, seen as not seated for that question; null otherwise.</summary>
        internal static Player Passing;

        /// <summary>Whether a player sits on the ground with the sit emote.</summary>
        public static bool SitsOnTheGround(Player player) => player.InEmote() && EmoteState(player) == "sit";

        /// <summary>Whether a player holding a rod sits: on a seat (not at a ship's controls), or on the ground.</summary>
        public static bool SeatedWithRod(Player player)
        {
            if (player == null || player.GetCurrentWeapon()?.m_shared.m_ammoType != Bait) return false;
            return (player.IsAttached() && player.GetDoodadController() == null) || SitsOnTheGround(player);
        }

        /// <summary>Whether this step's controls keep a seated player with a rod seated: no moving, jumping or crouching.</summary>
        public static bool StaysSeated(Player player, Vector3 movedir, bool jump, bool crouch) =>
            movedir == Vector3.zero && !jump && !crouch && SeatedWithRod(player);
    }

    /// <summary>A seated player with a rod who holds still is not stood up by casting or reeling.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class SeatedControls
    {
        private static void Prefix(Player __instance, Vector3 movedir, bool jump, bool crouch)
        {
            if (SeatedFishing.StaysSeated(__instance, movedir, jump, crouch)) SeatedFishing.Passing = __instance;
        }

        private static System.Exception Finalizer(System.Exception __exception)
        {
            SeatedFishing.Passing = null;
            return __exception;
        }
    }

    /// <summary>A seated player with a rod draws a cast as a standing one does.</summary>
    [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
    internal static class SeatedDraw
    {
        private static void Prefix(Player __instance)
        {
            if (SeatedFishing.SeatedWithRod(__instance)) SeatedFishing.Passing = __instance;
        }

        private static System.Exception Finalizer(System.Exception __exception)
        {
            SeatedFishing.Passing = null;
            return __exception;
        }
    }

    /// <summary>Seen as not attached while the game asks for a seated player with a rod.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.IsAttached))]
    internal static class SeatedNotAttached
    {
        private static bool Prefix(Player __instance, ref bool __result)
        {
            if (SeatedFishing.Passing != __instance) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>Seen as not in an emote while the game asks for a seated player with a rod.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.InEmote))]
    internal static class SeatedNotInEmote
    {
        private static bool Prefix(Player __instance, ref bool __result)
        {
            if (SeatedFishing.Passing != __instance) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>The sitting pose lets a seated player with a rod start the cast.</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.CanMove))]
    internal static class SeatedCanCast
    {
        private static bool Prefix(Character __instance, ref bool __result)
        {
            if (SeatedFishing.Passing == null || !ReferenceEquals(SeatedFishing.Passing, __instance)) return true;
            __result = !__instance.IsStaggering();
            return false;
        }
    }
}
