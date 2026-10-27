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
    /// and hands the switch to every other game. A player whose emote is the sit, which every game
    /// reads from the player's saved data, sits too, whatever the switch shows. While a player
    /// sits, the bones are kept whenever
    /// the game's base layer shows a sitting pose plainly, and put back right after the game's
    /// animation whenever it shows anything else, its standing throw, its pull or its sitting
    /// down again. Every game does this for every player it sees, whatever the motions setting.
    /// The pose is kept only once the sitting state has lasted a while (<see cref="SeatedBody.SettleSeconds"/>),
    /// so the game sitting the player down again inside that state is put back too.
    /// </summary>
    internal static class SeatedBodies
    {
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        private static readonly AccessTools.FieldRef<Player, string> EmoteState = AccessTools.FieldRefAccess<Player, string>("m_emoteState");

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
            public bool SawSitting;
            public float SwitchOffSince = -1f;
        }

        private static readonly Dictionary<Player, Body> Bodies = new Dictionary<Player, Body>();
        private static readonly Dictionary<RuntimeAnimatorController, int[]> SeatSwitches = new Dictionary<RuntimeAnimatorController, int[]>();

        /// <summary>
        /// Whether a player sits, as the seat's switch or the sit emote says, a moment's blink of the
        /// switch kept over, and the game has shown a sitting state since: a ship's helm has a switch
        /// of its own but no sitting state.
        /// </summary>
        public static bool Seated(Player player) => player != null && Bodies.TryGetValue(player, out var body) && body.SawSitting;

        /// <summary>Called right after the game's animation: keeps or puts back each seated player's sitting pose.</summary>
        public static void Hold()
        {
            if (ZNetScene.instance == null) return;
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                var animator = AnimatorOf(player);
                if (animator == null || !animator.isHuman)
                {
                    Bodies.Remove(player);
                    continue;
                }
                Bodies.TryGetValue(player, out var body);
                if (!SeatSwitchOn(animator) && EmoteState(player) != "sit")
                {
                    // A switch blinking off for a moment keeps the pose; off for longer, the player stood up.
                    if (body == null) continue;
                    if (body.SwitchOffSince < 0f) body.SwitchOffSince = Time.time;
                    if (SeatedBody.Forgets(Time.time - body.SwitchOffSince)) Bodies.Remove(player);
                    continue;
                }
                if (body == null)
                {
                    body = new Body { Bones = Kept.Select(animator.GetBoneTransform).ToArray(), Turns = new Quaternion[Kept.Length] };
                    Bodies[player] = body;
                }
                body.SwitchOffSince = -1f;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                var sittingPose = SitStates.Contains(state.shortNameHash);
                var inPose = state.normalizedTime * state.length;
                var crossing = animator.IsInTransition(0);
                body.SawSitting |= sittingPose;
                if (SeatedBody.Keeps(true, sittingPose, crossing, inPose)) Keep(body);
                else if (SeatedBody.PutsBack(true, sittingPose, crossing, inPose, body.Kept)) PutBack(body);
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
