using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace Rodnarok
{
    /// <summary>
    /// A seated player with a rod shows the game's own fishing motions on the arms and head while
    /// the legs stay seated. The game plays the throw, the pull and the stance of holding the rod
    /// on the same animation layer as every sitting pose, and a layer plays one at a time, so a
    /// seated cast showed nothing. Each such player gets a small animation graph of their own on
    /// top of what the game's animator made that frame: the game's sitting clip for the body, and
    /// over the arms and head the wind-up played and held while drawing, the throw as the cast goes
    /// out, the pull while reeling and the stance while the float is out, crossing over from one to
    /// the next and fading in and out as a whole. Every game shows it for every seated player it
    /// sees, read from their animator's sitting state, the rod in their hand, their block and their
    /// float; the draw and the throw only the player's own game knows of.
    /// </summary>
    internal static class SeatedRodPose
    {
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        private static readonly AccessTools.FieldRef<VisEquipment, int> RightItem = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentRightItemHash");
        private static readonly System.Func<FishingFloat, Character> FloatOwner = AccessTools.MethodDelegate<System.Func<FishingFloat, Character>>(AccessTools.Method(typeof(FishingFloat), "GetOwner"));

        /// <summary>The game's sitting states on its base animation layer.</summary>
        private static readonly int[] SitStates = new[] { "SitChair", "Emote_sit", "SitShip", "SitThrone", "SitDivan" }.Select(Animator.StringToHash).ToArray();

        /// <summary>The game's clips for each motion.</summary>
        private const string ThrowClip = "Throw";
        private const string ReelClip = "ReelIn";
        private const string HoldClip = "Idle_Fishing";

        /// <summary>Where the overlay's graph stands among the animator's outputs: after the game's own.</summary>
        private const ushort Order = 1000;

        private sealed class Overlay
        {
            public PlayableGraph Graph;
            public AnimationLayerMixerPlayable Layers;
            public AnimationMixerPlayable Motions;
            public AnimationClipPlayable Sit;
            public AnimationClip SitClip;
            public AnimationClipPlayable Throw;
            public AnimationClipPlayable Reel;
            public AnimationClipPlayable Hold;
            public SeatedBlend Blend = new SeatedBlend();
            public float IdleSince;
        }

        private static readonly Dictionary<Player, Overlay> Overlays = new Dictionary<Player, Overlay>();
        private static readonly HashSet<Animator> Unfit = new HashSet<Animator>();

        /// <summary>The motion shown on each player now, for the self-test.</summary>
        [ForTest]
        public static SeatedPose ShowingForTest(Player player) => player != null && Overlays.TryGetValue(player, out var o) && o.Blend.Overlay > 0.5f ? o.Blend.Showing : SeatedPose.None;

        /// <summary>Called every frame in a world.</summary>
        public static void Tick()
        {
            if (!Settings.SeatedMotions)
            {
                if (Overlays.Count > 0) Forget();
                return;
            }
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                var animator = AnimatorOf(player);
                if (animator == null) continue;
                var local = player == Player.m_localPlayer;
                var seated = Seated(animator);
                var pose = SeatedPoses.For(
                    seated && HoldsRod(player, local),
                    local && player.IsDrawingBow(),
                    local && player.InAttack(),
                    player.IsBlocking(),
                    FloatOut(player));
                if (!Overlays.TryGetValue(player, out var overlay))
                {
                    if (pose == SeatedPose.None) continue;
                    overlay = Make(animator);
                    if (overlay == null) continue;
                    Overlays[player] = overlay;
                }
                Step(overlay, animator, seated, pose);
            }

            foreach (var pair in Overlays.ToList())
            {
                if (pair.Key != null && AnimatorOf(pair.Key) != null && !(pair.Value.Blend.Overlay <= 0f && Time.time - pair.Value.IdleSince > 2f)) continue;
                Drop(pair.Value);
                Overlays.Remove(pair.Key);
            }
        }

        /// <summary>Lets go of every overlay on leaving a world.</summary>
        public static void Forget()
        {
            foreach (var overlay in Overlays.Values) Drop(overlay);
            Overlays.Clear();
            Unfit.Clear();
        }

        private static bool Seated(Animator animator)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            return SitStates.Contains(state);
        }

        /// <summary>Whether the player holds a rod: from the hand for the player's own game, from what others see in it for the rest.</summary>
        private static bool HoldsRod(Player player, bool local)
        {
            if (local) return player.GetCurrentWeapon()?.m_shared.m_ammoType == SeatedFishing.RodAmmo;
            var vis = player.GetComponent<VisEquipment>();
            if (vis == null || ObjectDB.instance == null) return false;
            var item = ObjectDB.instance.GetItemPrefab(RightItem(vis))?.GetComponent<ItemDrop>();
            return item != null && item.m_itemData.m_shared.m_ammoType == SeatedFishing.RodAmmo;
        }

        private static bool FloatOut(Player player) =>
            FishingFloat.GetAllInstances().Any(f => f != null && FloatOwner(f) == player);

        /// <summary>
        /// Moves the overlay on toward the motion called for. While seated, the game's own sitting
        /// clip lies beneath the motions, so the hips and legs are the seat's; on standing up the
        /// overlay lets go at once, leaving the game's own way up.
        /// </summary>
        private static void Step(Overlay o, Animator animator, bool seated, SeatedPose pose)
        {
            if (!seated)
            {
                o.Blend = new SeatedBlend();
                if (o.IdleSince <= 0f) o.IdleSince = Time.time;
                o.Layers.SetInputWeight(0, 0f);
                o.Layers.SetInputWeight(1, 0f);
                return;
            }

            var throwLength = o.Throw.GetAnimationClip().length;
            o.Blend.Step(pose, Time.deltaTime, throwLength);
            var b = o.Blend;
            if (b.Overlay > 0f) o.IdleSince = 0f;
            else if (o.IdleSince <= 0f) o.IdleSince = Time.time;

            FollowSitting(o, animator);
            o.Throw.SetTime(b.ThrowShare * throwLength);
            o.Motions.SetInputWeight(0, b.Throw);
            o.Motions.SetInputWeight(1, b.Reel);
            o.Motions.SetInputWeight(2, b.Hold);
            o.Layers.SetInputWeight(0, b.Overlay > 0f && o.SitClip != null ? 1f : 0f);
            o.Layers.SetInputWeight(1, b.Overlay);
            if (!o.Graph.IsPlaying()) o.Graph.Play();
        }

        /// <summary>Plays the clip of the game's sitting state beneath the motions, at the time the game has it.</summary>
        private static void FollowSitting(Overlay o, Animator animator)
        {
            AnimationClip clip = null;
            var heaviest = 0f;
            foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
            {
                if (info.clip == null || info.weight <= heaviest) continue;
                clip = info.clip;
                heaviest = info.weight;
            }
            if (clip != o.SitClip)
            {
                o.Graph.Disconnect(o.Layers, 0);
                if (o.Sit.IsValid()) o.Sit.Destroy();
                o.SitClip = clip;
                if (clip != null)
                {
                    o.Sit = AnimationClipPlayable.Create(o.Graph, clip);
                    o.Sit.SetSpeed(0);
                    o.Graph.Connect(o.Sit, 0, o.Layers, 0);
                }
            }
            if (clip == null) return;
            var at = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            o.Sit.SetTime((clip.isLooping ? at - Mathf.Floor(at) : Mathf.Clamp01(at)) * clip.length);
        }

        /// <summary>A graph for one player's animator, or none where the animator is not humanoid or lacks the game's clips.</summary>
        private static Overlay Make(Animator animator)
        {
            if (Unfit.Contains(animator)) return null;
            var clips = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : null;
            var throwClip = clips?.FirstOrDefault(c => c != null && c.name == ThrowClip);
            var reelClip = clips?.FirstOrDefault(c => c != null && c.name == ReelClip);
            var holdClip = clips?.FirstOrDefault(c => c != null && c.name == HoldClip);
            if (!animator.isHuman || throwClip == null || reelClip == null || holdClip == null)
            {
                Unfit.Add(animator);
                Log.Warning("Seated fishing motions are not shown for " + animator.name + ": " + (!animator.isHuman ? "its rig is not humanoid" : "the game's fishing clips are not on its controller"));
                return null;
            }

            var o = new Overlay { Graph = PlayableGraph.Create("Rodnarok seated rod " + animator.name) };
            o.Graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            o.Throw = AnimationClipPlayable.Create(o.Graph, throwClip);
            o.Throw.SetSpeed(0);
            o.Reel = AnimationClipPlayable.Create(o.Graph, reelClip);
            o.Hold = AnimationClipPlayable.Create(o.Graph, holdClip);
            o.Motions = AnimationMixerPlayable.Create(o.Graph, 3);
            o.Graph.Connect(o.Throw, 0, o.Motions, 0);
            o.Graph.Connect(o.Reel, 0, o.Motions, 1);
            o.Graph.Connect(o.Hold, 0, o.Motions, 2);
            // Layer 0 takes the game's sitting clip for the whole body but its travel, so the hips
            // and legs come from the seat itself; layer 1 lays the motion over the arms and head.
            o.Layers = AnimationLayerMixerPlayable.Create(o.Graph, 2);
            o.Graph.Connect(o.Motions, 0, o.Layers, 1);
            o.Layers.SetLayerMaskFromAvatarMask(0, Mask(part => part != AvatarMaskBodyPart.Root));
            o.Layers.SetLayerMaskFromAvatarMask(1, Mask(part => ArmsAndHead.Contains(part)));
            o.Layers.SetInputWeight(0, 0f);
            o.Layers.SetInputWeight(1, 0f);
            var output = AnimationPlayableOutput.Create(o.Graph, "Rodnarok seated rod", animator);
            output.SetSourcePlayable(o.Layers);
            output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            output.SetSortingOrder(Order);
            o.Graph.Play();
            return o;
        }

        private static readonly AvatarMaskBodyPart[] ArmsAndHead =
        {
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.Head,
        };

        private static AvatarMask Mask(System.Func<AvatarMaskBodyPart, bool> takes)
        {
            var mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++) mask.SetHumanoidBodyPartActive(part, takes(part));
            return mask;
        }

        private static void Drop(Overlay o)
        {
            if (o.Graph.IsValid()) o.Graph.Destroy();
        }
    }
}
