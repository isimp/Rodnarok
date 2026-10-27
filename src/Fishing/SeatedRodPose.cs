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
    /// top of what the game's animator made that frame: the throw's wind-up held while drawing, the
    /// throw as the cast goes out, the pull while reeling, the stance while the float is out, each
    /// limited to the arms and head and faded in and out. Every game shows it for every seated
    /// player it sees, read from their animator's sitting state, the rod in their hand, their block
    /// and their float; the draw and the throw only the player's own game knows of.
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

        /// <summary>How far into the throw the wind-up is held while drawing, as a share of it.</summary>
        private const float WindUp = 0.3f;

        /// <summary>How fast the overlay fades in and out, its weight a second.</summary>
        private const float Fade = 6f;

        /// <summary>Where the overlay's graph stands among the animator's outputs: after the game's own.</summary>
        private const ushort Order = 1000;

        private sealed class Overlay
        {
            public PlayableGraph Graph;
            public AnimationLayerMixerPlayable Layers;
            public AnimationMixerPlayable Motions;
            public AnimationClipPlayable Throw;
            public AnimationClipPlayable Reel;
            public AnimationClipPlayable Hold;
            public SeatedPose Showing;
            public float Weight;
            public float IdleSince;
        }

        private static readonly Dictionary<Player, Overlay> Overlays = new Dictionary<Player, Overlay>();
        private static readonly HashSet<Animator> Unfit = new HashSet<Animator>();

        /// <summary>The motion shown on each player now, for the self-test.</summary>
        [ForTest]
        public static SeatedPose ShowingForTest(Player player) => player != null && Overlays.TryGetValue(player, out var o) && o.Weight > 0.5f ? o.Showing : SeatedPose.None;

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
                var pose = SeatedPoses.For(
                    Seated(animator) && HoldsRod(player, local),
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
                Step(overlay, pose);
            }

            foreach (var pair in Overlays.ToList())
            {
                if (pair.Key != null && AnimatorOf(pair.Key) != null && !(pair.Value.Weight <= 0f && Time.time - pair.Value.IdleSince > 2f)) continue;
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

        /// <summary>Fades the overlay toward the motion called for and plays that motion.</summary>
        private static void Step(Overlay o, SeatedPose pose)
        {
            var target = pose == SeatedPose.None ? 0f : 1f;
            o.Weight = Mathf.MoveTowards(o.Weight, target, Fade * Time.deltaTime);
            if (pose == SeatedPose.None)
            {
                if (o.Weight <= 0f && o.IdleSince <= 0f) o.IdleSince = Time.time;
            }
            else
            {
                o.IdleSince = 0f;
                if (pose != o.Showing) Begin(o, pose);
                o.Showing = pose;
            }
            if (o.Showing == SeatedPose.Throw && o.Throw.GetTime() >= o.Throw.GetAnimationClip().length) o.Throw.SetSpeed(0);
            o.Layers.SetInputWeight(1, o.Weight);
            if (!o.Graph.IsPlaying()) o.Graph.Play();
        }

        /// <summary>Starts a motion: the wind-up held, the throw from where the wind-up left it, the pull and the stance looping.</summary>
        private static void Begin(Overlay o, SeatedPose pose)
        {
            o.Motions.SetInputWeight(0, pose == SeatedPose.Draw || pose == SeatedPose.Throw ? 1f : 0f);
            o.Motions.SetInputWeight(1, pose == SeatedPose.Reel ? 1f : 0f);
            o.Motions.SetInputWeight(2, pose == SeatedPose.Hold ? 1f : 0f);
            var length = o.Throw.GetAnimationClip().length;
            switch (pose)
            {
                case SeatedPose.Draw:
                    o.Throw.SetTime(length * WindUp);
                    o.Throw.SetSpeed(0);
                    break;
                case SeatedPose.Throw:
                    if (o.Showing != SeatedPose.Draw) o.Throw.SetTime(0);
                    o.Throw.SetSpeed(1);
                    break;
            }
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
            o.Reel = AnimationClipPlayable.Create(o.Graph, reelClip);
            o.Hold = AnimationClipPlayable.Create(o.Graph, holdClip);
            o.Motions = AnimationMixerPlayable.Create(o.Graph, 3);
            o.Graph.Connect(o.Throw, 0, o.Motions, 0);
            o.Graph.Connect(o.Reel, 0, o.Motions, 1);
            o.Graph.Connect(o.Hold, 0, o.Motions, 2);
            // Layer 0 stays empty, so what the game's animator made passes through; layer 1 lays the
            // motion over the arms and head only.
            o.Layers = AnimationLayerMixerPlayable.Create(o.Graph, 2);
            o.Graph.Connect(o.Motions, 0, o.Layers, 1);
            o.Layers.SetLayerMaskFromAvatarMask(1, ArmsAndHead());
            o.Layers.SetInputWeight(1, 0f);
            var output = AnimationPlayableOutput.Create(o.Graph, "Rodnarok seated rod", animator);
            output.SetSourcePlayable(o.Layers);
            output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
            output.SetSortingOrder(Order);
            o.Graph.Play();
            return o;
        }

        private static AvatarMask ArmsAndHead()
        {
            var mask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++) mask.SetHumanoidBodyPartActive(part, false);
            foreach (var part in new[] { AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.Head })
                mask.SetHumanoidBodyPartActive(part, true);
            return mask;
        }

        private static void Drop(Overlay o)
        {
            if (o.Graph.IsValid()) o.Graph.Destroy();
        }
    }
}
