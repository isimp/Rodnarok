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
        internal const string RodAmmo = "$item_fishingbait";

        private static readonly AccessTools.FieldRef<Player, string> EmoteState = AccessTools.FieldRefAccess<Player, string>("m_emoteState");

        /// <summary>The seated player the game is asking about now, seen as not seated for that question; null otherwise.</summary>
        internal static Player Passing;

        /// <summary>Whether a player sits on the ground with the sit emote.</summary>
        public static bool SitsOnTheGround(Player player) => player.InEmote() && EmoteState(player) == "sit";

        /// <summary>Whether a player holding a rod sits: on a seat (not at a ship's controls), or on the ground.</summary>
        public static bool SeatedWithRod(Player player)
        {
            if (player == null || player.GetCurrentWeapon()?.m_shared.m_ammoType != RodAmmo) return false;
            return (player.IsAttached() && player.GetDoodadController() == null) || SitsOnTheGround(player);
        }

        /// <summary>Whether this step's controls keep a seated player with a rod seated: no moving, jumping or crouching.</summary>
        public static bool StaysSeated(Player player, Vector3 movedir, bool jump, bool crouch) =>
            movedir == Vector3.zero && !jump && !crouch && SeatedWithRod(player);

        /// <summary>
        /// How far round from the way a seated player's body faces their cast goes, as <see cref="SeatedAim"/>
        /// has it; none for a player not seated with a rod. Asked as the player really sits, not as the
        /// game is shown them for a moment.
        /// </summary>
        public static Quaternion ThrowTurn(Player player)
        {
            var passing = Passing;
            Passing = null;
            try
            {
                if (!SeatedWithRod(player)) return Quaternion.identity;
                var body = player.transform.rotation.eulerAngles.y;
                var cast = SeatedAim.ThrowYaw(body, player.GetLookYaw().eulerAngles.y, onChair: player.IsAttached());
                return Quaternion.Euler(0f, cast - body, 0f);
            }
            finally
            {
                Passing = passing;
            }
        }
    }

    /// <summary>
    /// A seated player's cast goes where they aim, as <see cref="SeatedAim"/> has it. The game throws
    /// the rod's cast along the body's facing, which a seat holds still; the cast and where it
    /// leaves the rod are turned round the character to the aim.
    /// </summary>
    [HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
    internal static class SeatedThrow
    {
        private static readonly AccessTools.FieldRef<Attack, Humanoid> CharacterOf = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");

        private static void Postfix(Attack __instance, ref Vector3 spawnPoint, ref Vector3 aimDir)
        {
            if (!(CharacterOf(__instance) is Player player)) return;
            var turn = SeatedFishing.ThrowTurn(player);
            if (turn == Quaternion.identity) return;
            var pivot = player.transform.position;
            spawnPoint = pivot + turn * (spawnPoint - pivot);
            aimDir = turn * aimDir;
        }
    }

    /// <summary>
    /// Sitting on the ground, drawing a cast turns the body toward the aim at the game's own turning
    /// speed, as drawing does for a standing player; a chair holds its sitter's facing.
    /// </summary>
    [HarmonyPatch(typeof(Player), "FixedUpdate")]
    internal static class SeatedTurn
    {
        private static readonly AccessTools.FieldRef<Character, float> TurnSpeed = AccessTools.FieldRefAccess<Character, float>("m_turnSpeed");
        private static readonly AccessTools.FieldRef<Character, Rigidbody> Body = AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");

        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || !__instance.IsDrawingBow() || !SeatedFishing.SitsOnTheGround(__instance) || !SeatedFishing.SeatedWithRod(__instance)) return;
            var turned = Quaternion.RotateTowards(__instance.transform.rotation, __instance.GetLookYaw(), TurnSpeed(__instance) * Time.fixedDeltaTime);
            __instance.transform.rotation = turned;
            var body = Body(__instance);
            if (body != null) body.rotation = turned;
        }
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
