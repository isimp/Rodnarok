using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Keeps every seated player's hips, spine and legs as the seat has them (<see cref="SeatedBody"/>).
    /// A player sits for every game alike: the game turns on an animator switch for the seat, one
    /// named <c>attach_…</c> for a chair, a bench or a boat's bench and <c>emote_sit</c> on the ground,
    /// and hands the switch to every other game. While a switch is on, the bones are kept whenever
    /// the game's base layer shows a sitting pose plainly, and put back right after the game's
    /// animation whenever it shows anything else, its standing throw, its pull or its sitting
    /// down again. Every game does this for every player it sees, whatever the motions setting.
    /// </summary>
    internal static class SeatedBodies
    {
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");

        /// <summary>The game's sitting states on its base animation layer.</summary>
        private static readonly int[] SitStates = new[] { "SitChair", "Emote_sit", "SitShip", "SitThrone", "SitDivan" }.Select(Animator.StringToHash).ToArray();

        /// <summary>The bones kept: the hips, the spine up to the neck, and the legs.</summary>
        private static readonly HumanBodyBones[] Kept =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
        };

        private sealed class Body
        {
            public Transform[] Bones;
            public Quaternion[] Turns;
            public Vector3 HipsAt;
            public bool Kept;
        }

        private static readonly Dictionary<Player, Body> Bodies = new Dictionary<Player, Body>();
        private static readonly Dictionary<RuntimeAnimatorController, int[]> SeatSwitches = new Dictionary<RuntimeAnimatorController, int[]>();

        /// <summary>Whether a player sits and the game has shown their sitting pose since they sat down.</summary>
        public static bool Seated(Player player) => player != null && Bodies.TryGetValue(player, out var body) && body.Kept;

        /// <summary>Called right after the game's animation: keeps or puts back each seated player's sitting pose.</summary>
        public static void Hold()
        {
            if (ZNetScene.instance == null) return;
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                var animator = AnimatorOf(player);
                if (animator == null || !animator.isHuman || !SeatSwitchOn(animator))
                {
                    Bodies.Remove(player);
                    continue;
                }
                if (!Bodies.TryGetValue(player, out var body))
                {
                    body = new Body { Bones = Kept.Select(animator.GetBoneTransform).ToArray(), Turns = new Quaternion[Kept.Length] };
                    Bodies[player] = body;
                }
                var sittingPose = SitStates.Contains(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                var crossing = animator.IsInTransition(0);
                if (SeatedBody.Keeps(true, sittingPose, crossing)) Keep(body);
                else if (SeatedBody.PutsBack(true, sittingPose, crossing, body.Kept)) PutBack(body);
            }
            foreach (var gone in Bodies.Keys.Where(p => p == null).ToList()) Bodies.Remove(gone);
        }

        /// <summary>Lets go of every body on leaving a world.</summary>
        public static void Forget()
        {
            Bodies.Clear();
            SeatSwitches.Clear();
        }

        private static void Keep(Body body)
        {
            for (var i = 0; i < body.Bones.Length; i++)
                if (body.Bones[i] != null) body.Turns[i] = body.Bones[i].localRotation;
            if (body.Bones[0] != null) body.HipsAt = body.Bones[0].localPosition;
            body.Kept = true;
        }

        private static void PutBack(Body body)
        {
            for (var i = 0; i < body.Bones.Length; i++)
                if (body.Bones[i] != null) body.Bones[i].localRotation = body.Turns[i];
            if (body.Bones[0] != null) body.Bones[0].localPosition = body.HipsAt;
        }

        /// <summary>Whether one of the game's seat switches is on in this animator.</summary>
        private static bool SeatSwitchOn(Animator animator)
        {
            var controller = animator.runtimeAnimatorController;
            if (controller == null) return false;
            if (!SeatSwitches.TryGetValue(controller, out var switches))
            {
                switches = animator.parameters
                    .Where(p => p.type == AnimatorControllerParameterType.Bool && (p.name.StartsWith("attach_", System.StringComparison.Ordinal) || p.name == "emote_sit"))
                    .Select(p => p.nameHash)
                    .ToArray();
                SeatSwitches[controller] = switches;
            }
            foreach (var hash in switches)
                if (animator.GetBool(hash)) return true;
            return false;
        }
    }
}
