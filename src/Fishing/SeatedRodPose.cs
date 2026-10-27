using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rodnarok
{
    /// <summary>
    /// A seated player with a rod shows the game's own fishing motions on the arms and head while
    /// the hips and legs stay exactly as the game seats them. The game plays the throw, the pull
    /// and the stance of holding the rod on the same animation layer as every sitting pose, and a
    /// layer plays one at a time, so a seated cast showed nothing. The three clips are read once
    /// per rig as still frames on a hidden copy of the skeleton; then, each frame after the game
    /// has animated a seated player with a rod, only the shoulder, arm, hand, finger and head
    /// bones are turned toward those frames: the wind-up played and held while drawing, the throw
    /// as the cast goes out, the pull while reeling and the stance while the float is out,
    /// crossing over from one to the next and fading in and out as a whole. Every game shows it
    /// for every seated player it sees, read from the game's seat switch (<see cref="SeatedBodies"/>), the rod in their
    /// hand, their block and their float; the draw and the throw only the player's own game knows of.
    /// </summary>
    internal static class SeatedRodPose
    {
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        private static readonly AccessTools.FieldRef<VisEquipment, int> RightItem = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentRightItemHash");
        private static readonly System.Func<FishingFloat, Character> FloatOwner = AccessTools.MethodDelegate<System.Func<FishingFloat, Character>>(AccessTools.Method(typeof(FishingFloat), "GetOwner"));

        /// <summary>The game's clips for each motion.</summary>
        private const string ThrowClip = "Throw";
        private const string ReelClip = "ReelIn";
        private const string HoldClip = "Idle_Fishing";

        /// <summary>The bones the motions turn; the hips, the spine and the legs are never touched.</summary>
        private static readonly HumanBodyBones[] Turned = new[]
        {
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.Head,
        }.Concat(Enumerable.Range((int)HumanBodyBones.LeftThumbProximal, (int)HumanBodyBones.RightLittleDistal - (int)HumanBodyBones.LeftThumbProximal + 1).Select(b => (HumanBodyBones)b)).ToArray();

        /// <summary>One clip read as still frames: each frame the local turn of every turned bone.</summary>
        private sealed class Track
        {
            public float Length;
            public bool Loop;
            public Quaternion[][] Frames;

            public Quaternion At(float time, int bone)
            {
                ClipFrames.At(time, Length, Frames.Length, Loop, out var from, out var to, out var between);
                return Quaternion.Slerp(Frames[from][bone], Frames[to][bone], between);
            }
        }

        /// <summary>The three motions read for one rig, and which of the turned bones it has.</summary>
        private sealed class Poses
        {
            public HumanBodyBones[] Bones;
            public Track Throw;
            public Track Reel;
            public Track Hold;

            /// <summary>Which bones are held as the clip has them toward the whole character rather than toward the bone they hang from.</summary>
            public bool[] Anchored;
        }

        private sealed class Overlay
        {
            public Poses Poses;
            public Transform[] Bones;
            public Transform Root;
            public SeatedBlend Blend = new SeatedBlend();
            public float ReelTime;
            public float HoldTime;
            public float IdleSince;

            /// <summary>Each bone's turn as last set here, to tell whether the animator has posed it since.</summary>
            public Quaternion[] Written;
        }

        private static readonly Dictionary<Player, Overlay> Overlays = new Dictionary<Player, Overlay>();
        private static readonly Dictionary<Avatar, Poses> Read = new Dictionary<Avatar, Poses>();
        private static readonly HashSet<Avatar> Unfit = new HashSet<Avatar>();

        /// <summary>The motion shown on each player now, for the self-test.</summary>
        [ForTest]
        public static SeatedPose ShowingForTest(Player player) => player != null && Overlays.TryGetValue(player, out var o) && o.Blend.Overlay > 0.5f ? o.Blend.Showing : SeatedPose.None;

        /// <summary>Why the motions could not be read from the game's clips the last time it was tried; null when they could.</summary>
        [ForTest]
        public static string TroubleForTest => _trouble;

        private static string _trouble;

        /// <summary>Called every frame in a world, before the game animates: decides what each seated player shows.</summary>
        public static void Tick()
        {
            if (!Settings.SeatedMotions)
            {
                if (Overlays.Count > 0) Overlays.Clear();
                return;
            }
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                var animator = AnimatorOf(player);
                if (animator == null) continue;
                var local = player == Player.m_localPlayer;
                var seated = SeatedBodies.Seated(player);
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
                Step(overlay, seated, pose);
            }

            foreach (var pair in Overlays.ToList())
            {
                if (pair.Key != null && AnimatorOf(pair.Key) != null && !(pair.Value.Blend.Overlay <= 0f && Time.time - pair.Value.IdleSince > 2f)) continue;
                Overlays.Remove(pair.Key);
            }
        }

        /// <summary>
        /// Called right after the game's animators have posed the characters: turns the arm and head
        /// bones of each seated player toward the motion. A bone the animator has not posed again since
        /// the last turn keeps that turn, so a step without new animation turns nothing twice.
        /// </summary>
        public static void Turn()
        {
            if (ZNetScene.instance == null) return;
            foreach (var pair in Overlays)
            {
                var o = pair.Value;
                var weight = o.Blend.Overlay;
                if (pair.Key == null || weight <= 0f) continue;
                var b = o.Blend;
                for (var i = 0; i < o.Bones.Length; i++)
                {
                    var bone = o.Bones[i];
                    if (bone == null || o.Written[i] == bone.localRotation) continue;
                    var target = Quaternion.identity;
                    var taken = 0f;
                    Mix(ref target, ref taken, o.Poses.Throw.At(b.ThrowShare * o.Poses.Throw.Length, i), b.Throw);
                    Mix(ref target, ref taken, o.Poses.Reel.At(o.ReelTime, i), b.Reel);
                    Mix(ref target, ref taken, o.Poses.Hold.At(o.HoldTime, i), b.Hold);
                    if (taken <= 0f) continue;
                    if (o.Poses.Anchored[i]) bone.rotation = Quaternion.Slerp(bone.rotation, o.Root.rotation * target, weight);
                    else bone.localRotation = Quaternion.Slerp(bone.localRotation, target, weight);
                    o.Written[i] = bone.localRotation;
                }
            }
        }

        /// <summary>Lets go of every overlay on leaving a world.</summary>
        public static void Forget()
        {
            Overlays.Clear();
        }

        private static void Mix(ref Quaternion mixed, ref float taken, Quaternion pose, float share)
        {
            if (share <= 0f) return;
            taken += share;
            mixed = taken <= share ? pose : Quaternion.Slerp(mixed, pose, share / taken);
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

        /// <summary>Moves the blend on toward the motion called for; standing up lets go at once, leaving the game's own way up.</summary>
        private static void Step(Overlay o, bool seated, SeatedPose pose)
        {
            if (!seated)
            {
                o.Blend = new SeatedBlend();
                if (o.IdleSince <= 0f) o.IdleSince = Time.time;
                return;
            }
            o.Blend.Step(pose, Time.deltaTime, o.Poses.Throw.Length);
            o.ReelTime += Time.deltaTime;
            o.HoldTime += Time.deltaTime;
            if (o.Blend.Overlay > 0f) o.IdleSince = 0f;
            else if (o.IdleSince <= 0f) o.IdleSince = Time.time;
        }

        /// <summary>The overlay for one player's animator, or none where its rig could not be read.</summary>
        private static Overlay Make(Animator animator)
        {
            var avatar = animator.avatar;
            if (avatar == null || Unfit.Contains(avatar)) return null;
            if (!Read.TryGetValue(avatar, out var poses))
            {
                // Counted unfit until read, so a rig whose reading throws is tried once, not every frame.
                Unfit.Add(avatar);
                _trouble = "reading the clips stopped with an error, see the log";
                poses = ReadPoses(animator, out var trouble);
                _trouble = trouble;
                if (poses == null)
                {
                    Log.Warning("Seated fishing motions are not shown for " + animator.name + ": " + trouble);
                    return null;
                }
                Unfit.Remove(avatar);
                Read[avatar] = poses;
            }
            return new Overlay { Poses = poses, Bones = poses.Bones.Select(animator.GetBoneTransform).ToArray(), Root = animator.transform, Written = new Quaternion[poses.Bones.Length] };
        }

        /// <summary>
        /// Reads the three clips as still frames on a hidden copy of the animator's skeleton: the copy
        /// is made inside an inactive holder, so none of its scripts wake, stripped of everything but
        /// its bones and its animator, posed frame by frame by a graph of its own, then thrown away.
        /// </summary>
        private static Poses ReadPoses(Animator animator, out string trouble)
        {
            trouble = null;
            var clips = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.animationClips : null;
            var throwClip = clips?.FirstOrDefault(c => c != null && c.name == ThrowClip);
            var reelClip = clips?.FirstOrDefault(c => c != null && c.name == ReelClip);
            var holdClip = clips?.FirstOrDefault(c => c != null && c.name == HoldClip);
            if (!animator.isHuman)
            {
                trouble = "its rig is not humanoid";
                return null;
            }
            if (throwClip == null || reelClip == null || holdClip == null)
            {
                trouble = "the game's fishing clips are not on its controller";
                return null;
            }
            var bones = Turned.Where(b => animator.GetBoneTransform(b) != null).ToArray();

            var holder = new GameObject("Rodnarok seated rod poses");
            holder.SetActive(false);
            var graph = default(PlayableGraph);
            try
            {
                var copy = Object.Instantiate(animator.gameObject, holder.transform);
                Strip(copy);
                var copyAnimator = copy.GetComponent<Animator>();
                copyAnimator.runtimeAnimatorController = null;
                copyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                copyAnimator.applyRootMotion = false;
                holder.SetActive(true);
                copyAnimator.Rebind();
                var copyBones = bones.Select(copyAnimator.GetBoneTransform).ToArray();
                var anchored = Anchors(bones);

                graph = PlayableGraph.Create("Rodnarok seated rod poses");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "poses", copyAnimator);
                var poses = new Poses
                {
                    Bones = bones,
                    Anchored = anchored,
                    Throw = ReadTrack(graph, output, throwClip, false, copyBones, anchored, copy.transform),
                    Reel = ReadTrack(graph, output, reelClip, true, copyBones, anchored, copy.transform),
                    Hold = ReadTrack(graph, output, holdClip, true, copyBones, anchored, copy.transform),
                };
                if (Same(poses.Throw.Frames[0], poses.Reel.Frames[0]) && Same(poses.Reel.Frames[0], poses.Hold.Frames[0]))
                {
                    trouble = "the copy of its skeleton did not take the clips' poses";
                    return null;
                }
                return poses;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.Destroy(holder);
            }
        }

        private static Track ReadTrack(PlayableGraph graph, AnimationPlayableOutput output, AnimationClip clip, bool loop, Transform[] bones, bool[] anchored, Transform root)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable);
            var count = ClipFrames.Count(clip.length);
            var frames = new Quaternion[count][];
            for (var f = 0; f < count; f++)
            {
                playable.SetTime(clip.length * f / (count - 1));
                graph.Evaluate();
                frames[f] = bones.Select((b, i) => anchored[i] ? Quaternion.Inverse(root.rotation) * b.rotation : b.localRotation).ToArray();
            }
            playable.Destroy();
            return new Track { Length = clip.length, Loop = loop, Frames = frames };
        }

        /// <summary>
        /// The bones held toward the whole character: each side's shoulder, or its upper arm where the
        /// rig has no shoulder, and the head. The seated chest leans otherwise than the standing one
        /// the clips were made on, so turned only toward it the arms would carry the rod low; held
        /// toward the character, they carry it as standing, and the bones below follow the clip.
        /// </summary>
        private static bool[] Anchors(HumanBodyBones[] bones)
        {
            var anchors = new HashSet<HumanBodyBones> { HumanBodyBones.Head };
            anchors.Add(bones.Contains(HumanBodyBones.LeftShoulder) ? HumanBodyBones.LeftShoulder : HumanBodyBones.LeftUpperArm);
            anchors.Add(bones.Contains(HumanBodyBones.RightShoulder) ? HumanBodyBones.RightShoulder : HumanBodyBones.RightUpperArm);
            return bones.Select(anchors.Contains).ToArray();
        }

        private static bool Same(Quaternion[] a, Quaternion[] b)
        {
            for (var i = 0; i < a.Length; i++)
                if (Quaternion.Angle(a[i], b[i]) > 0.5f) return false;
            return true;
        }

        /// <summary>Takes from a copy everything but its bones and its animator, scripts first and what others depend on last.</summary>
        private static void Strip(GameObject copy)
        {
            var keep = copy.GetComponent<Animator>();
            var gone = copy.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && !(c is Transform) && c != keep)
                .OrderBy(StripOrder)
                .ToList();
            foreach (var c in gone)
                if (c != null) Object.DestroyImmediate(c);
        }

        /// <summary>Scripts go first, then capes' cloth before the meshes it hangs on, joints before bodies, and what is drawn last.</summary>
        private static int StripOrder(Component c)
        {
            if (c is MonoBehaviour) return 0;
            if (c.GetType().Name == "Cloth") return 1;
            if (c is Joint) return 2;
            if (c is Collider) return 3;
            if (c is Rigidbody) return 4;
            if (c is Renderer) return 5;
            return 6;
        }
    }
}
