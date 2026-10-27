using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// Fishing while sitting, as a player does it: on a chair, then sitting on the ground, the
    /// character holding a rod draws a cast, casts and reels in, and stays seated through all of
    /// it. Needs only a character beside water: the rod and a chair are brought for the run, and
    /// afterwards the character stands where it stood with its gear as before, the chair and the
    /// float taken away. Pictures of the seated draw and cast are taken for looking at.
    /// </summary>
    internal static class SelfTestSitting
    {
        private const string ChairPrefab = "piece_chair02";

        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly AccessTools.FieldRef<Character, Animator> AnimatorOf = AccessTools.FieldRefAccess<Character, Animator>("m_animator");
        private static readonly System.Reflection.MethodInfo StopEmote = AccessTools.Method(typeof(Player), "StopEmote");

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario("ARodCastsAndReelsWhileSittingAndTheCharacterStaysSeated", Sitting);
        }

        private static IEnumerable Sitting(ScenarioCheck check)
        {
            var player = Player.m_localPlayer;
            var water = SelfTestArrival.DeepWaterBeside(player.transform.position, 1f);
            check.That(water.HasValue, "water within 6 to 20 m of the character");
            if (!water.HasValue) yield break;
            var chairPrefab = ZNetScene.instance.GetPrefab(ChairPrefab);
            check.That(chairPrefab != null && chairPrefab.GetComponentInChildren<Chair>() != null, "the game's wooden chair is there to sit on");
            if (chairPrefab == null) yield break;

            var stoodAt = player.transform.position;
            // A spot whose throw toward the water is clear: a cast that meets a branch at once ends there.
            var dry = SelfTestPlaces.DryLandBeside(stoodAt, spot => SelfTestPlaces.ThrowBlockedBy(spot, water.Value) == null);
            check.That(dry.HasValue, "dry, level land within 1.5 to 4 m of the character for a chair, with a clear throw toward the water");
            if (!dry.HasValue) yield break;
            var toWater = water.Value - dry.Value;
            toWater.y = 0f;
            var facing = Quaternion.LookRotation(toWater.sqrMagnitude < 0.01f ? player.transform.forward : toWater.normalized);
            var gear = SelfTestGear.RodInHand(player);
            var standing = HeadHeight(player);
            GameObject chair = null;
            // The motions are checked whatever the player's own setting, which is put back after.
            var motionsWere = Settings.SeatedMotionsForTest(true);
            try
            {
                check.That(gear.Rod != null, "a fishing rod is in hand: " + gear.Trouble);
                if (gear.Rod == null) yield break;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;

                // On a chair on dry land, sat on as a player sits on one: walked up to, then used.
                chair = Object.Instantiate(chairPrefab, dry.Value, facing);
                yield return null;
                var seat = chair.GetComponentInChildren<Chair>();
                MoveTo(player, dry.Value + facing * Vector3.back * 0.6f);
                yield return null;
                var used = seat.Interact(player, false, false);
                for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime) yield return null;
                check.That(player.IsAttached() && Seated(player, standing), "the character sits on the chair, its head lowered as seated; used: " + used + ", attached: " + player.IsAttached() + ", head " + Heads(player, standing));
                Picture(check, "sitting-chair-seated");
                yield return null;
                if (player.IsAttached())
                {
                    foreach (var step in CastAndReel(check, player, water.Value, "chair", () => player.IsAttached(), () => Seated(player, standing), () => Heads(player, standing))) yield return step;
                }
                player.AttachStop();
                ZNetScene.instance.Destroy(chair);
                chair = null;
                for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;

                // On dry ground where the chair stood, with the sit emote.
                MoveTo(player, dry.Value);
                yield return null;
                var sat = player.StartEmote("sit", oneshot: false);
                for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime) yield return null;
                check.That(SeatedFishing.SitsOnTheGround(player) && Seated(player, standing), "the character sits on the ground, its head lowered as seated; emote started: " + sat + ", in the sit emote: " + SeatedFishing.SitsOnTheGround(player) + ", head " + Heads(player, standing));
                Picture(check, "sitting-ground-seated");
                yield return null;
                if (SeatedFishing.SitsOnTheGround(player))
                {
                    foreach (var step in CastAndReel(check, player, water.Value, "ground", () => SeatedFishing.SitsOnTheGround(player), () => Seated(player, standing), () => Heads(player, standing))) yield return step;
                }
            }
            finally
            {
                SelfTestDraw.Drawing = false;
                SelfTestDraw.Reeling = false;
                SelfTestHold.Holding = false;
                foreach (var ff in OwnFloats()) ZNetScene.instance.Destroy(ff.gameObject);
                if (player.IsAttached()) player.AttachStop();
                if (player.InEmote()) StopEmote?.Invoke(player, null);
                if (chair != null) ZNetScene.instance.Destroy(chair);
                gear.PutBack();
                Settings.SeatedMotionsForTest(motionsWere);
            }

            // Back where it stood.
            MoveTo(player, stoodAt);
            for (var waited = 0f; waited < 1f; waited += Time.deltaTime) yield return null;
            check.That(!player.IsAttached() && !player.InEmote(), "the character stands again after the run");
            check.That(gear.AsBefore(), "the character's gear and what it knows are as before the run: " + gear.Difference());
        }

        /// <summary>Draws a cast toward the water, casts, and reels in, checking the character stays seated throughout.</summary>
        private static IEnumerable<object> CastAndReel(ScenarioCheck check, Player player, Vector3 water, string seat, System.Func<bool> seated, System.Func<bool> posed, System.Func<string> heads)
        {
            foreach (var ff in OwnFloats()) ZNetScene.instance.Destroy(ff.gameObject);
            var look = water + Vector3.up * 3f - player.GetEyePoint();
            player.SetLookDir(look.normalized);
            yield return null;

            // A moment with the key up, then the key held past the rod's least draw.
            for (var waited = 0f; waited < 0.3f; waited += Time.deltaTime) yield return null;
            var legs = new LegWatch(player);
            SelfTestDraw.Drawing = true;
            var drew = false;
            var windUpShown = false;
            var pictured = false;
            var stayed = true;
            var lowered = true;
            for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime)
            {
                drew |= player.IsDrawingBow();
                windUpShown |= SeatedRodPose.ShowingForTest(player) == SeatedPose.Draw;
                stayed &= seated();
                if (waited >= 0.5f) lowered &= posed();
                if (waited >= 1f && !pictured) pictured = Picture(check, "sitting-" + seat + "-drawing");
                legs.Look();
                yield return null;
            }
            SelfTestDraw.Drawing = false;
            check.That(drew, "sitting on the " + seat + ", the rod draws");
            check.That(stayed, "the character stayed seated on the " + seat + " while drawing");
            check.That(lowered, "drawing on the " + seat + ", the character's head stays lowered as seated; at the end " + heads());
            check.That(windUpShown, "drawing on the " + seat + ", the arms show the throw's wind-up");

            // The float is watched for a while: when any float shows, whether this game owns it, and
            // what the character was doing should it go again, so a failed cast says what happened.
            FishingFloat cast = null;
            var shownAt = -1f;
            var ownedShown = false;
            var goneAt = -1f;
            var goneWhile = "";
            for (var waited = 0f; waited < CastWatch && cast == null; waited += Time.deltaTime)
            {
                var any = FishingFloat.GetAllInstances().Where(f => f != null).ToList();
                if (any.Count > 0 && shownAt < 0f)
                {
                    shownAt = waited;
                    var view = any[0].GetComponent<ZNetView>();
                    ownedShown = view != null && view.IsValid() && view.IsOwner();
                }
                if (shownAt >= 0f && any.Count == 0 && goneAt < 0f)
                {
                    goneAt = waited;
                    goneWhile = "in an attack " + player.InAttack() + ", drawing " + player.IsDrawingBow() + ", attached " + player.IsAttached() + ", rod tip " + (Utils.FindChild(player.transform, "_RodTop") != null);
                }
                cast = OwnFloats().FirstOrDefault();
                legs.Look();
                yield return null;
            }
            var told = shownAt < 0f
                ? "no float showed within " + CastWatch.ToString("0", CultureInfo.InvariantCulture) + " s"
                : "a float showed after " + shownAt.ToString("0.0", CultureInfo.InvariantCulture) + " s, owned here " + ownedShown
                    + (goneAt < 0f ? "" : ", gone after " + goneAt.ToString("0.0", CultureInfo.InvariantCulture) + " s while " + goneWhile);
            check.That(cast != null, "sitting on the " + seat + ", the rod casts; " + told + "; the throw meets " + (SelfTestPlaces.ThrowBlockedBy(player.transform.position, water) ?? "nothing"));
            check.That(seated(), "the character is still seated on the " + seat + " after the cast");
            for (var waited = 0f; waited < 1.5f; waited += Time.deltaTime)
            {
                legs.Look();
                yield return null;
            }
            check.That(posed(), "a moment after the cast the character's head is lowered as seated on the " + seat + ", " + heads());
            Picture(check, "sitting-" + seat + "-cast");
            yield return null;
            if (cast == null)
            {
                legs.Tell(check, seat);
                yield break;
            }

            // Reeling in.
            var line = LineLength(cast);
            stayed = true;
            // Held as the block key reaches the game, so the seated player's own way to reel is what is tried.
            SelfTestDraw.Reeling = true;
            var pullShown = false;
            var picturedReel = false;
            for (var waited = 0f; waited < 1.5f && cast != null; waited += Time.deltaTime)
            {
                stayed &= seated();
                pullShown |= SeatedRodPose.ShowingForTest(player) == SeatedPose.Reel;
                if (!picturedReel && waited >= 0.8f) picturedReel = Picture(check, "sitting-" + seat + "-reeling");
                legs.Look(hands: true);
                yield return null;
            }
            SelfTestDraw.Reeling = false;
            var reeledTo = cast != null ? LineLength(cast) : line;
            // The pull hands back to the rod held out.
            for (var waited = 0f; waited < 0.5f && cast != null; waited += Time.deltaTime)
            {
                legs.Look(hands: true);
                yield return null;
            }
            check.That(pullShown, "reeling on the " + seat + ", the arms show the pull");
            check.That(cast != null && reeledTo < line - 0.5f, "sitting on the " + seat + ", the line reels in, from " + line.ToString("0.0", CultureInfo.InvariantCulture) + " m");
            check.That(stayed, "the character stayed seated on the " + seat + " while reeling");
            legs.Tell(check, seat);
            if (cast != null) ZNetScene.instance.Destroy(cast.gameObject);
            yield return null;
        }

        /// <summary>
        /// Watches the legs and the right hand of a seated character against the character itself:
        /// how far the hips and legs move from where the seat had them before the draw, and the
        /// fastest the hand moves between two frames, which a cut from one motion to the next shows
        /// as a jump.
        /// </summary>
        private sealed class LegWatch
        {
            /// <summary>How far the hips and legs may move from the seat's pose, in metres.</summary>
            private const float LegsHold = 0.06f;

            /// <summary>The fastest the hand may move as the pull begins and ends, in metres a second.</summary>
            private const float HandSpeed = 4f;

            private static readonly HumanBodyBones[] LegBones =
            {
                HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            };

            private readonly Player _player;
            private readonly Animator _animator;
            private readonly Dictionary<HumanBodyBones, Vector3> _seated = new Dictionary<HumanBodyBones, Vector3>();
            private float _moved;
            private string _movedBone = "";
            private Vector3? _hand;
            private float _handFastest;

            public LegWatch(Player player)
            {
                _player = player;
                _animator = AnimatorOf(player);
                foreach (var bone in LegBones)
                {
                    var at = Where(bone);
                    if (at.HasValue) _seated[bone] = at.Value;
                }
            }

            /// <summary>Takes this frame's look; the hand is followed only where asked.</summary>
            public void Look(bool hands = false)
            {
                foreach (var pair in _seated)
                {
                    var at = Where(pair.Key);
                    if (!at.HasValue) continue;
                    var moved = Vector3.Distance(at.Value, pair.Value);
                    if (moved <= _moved) continue;
                    _moved = moved;
                    _movedBone = pair.Key.ToString();
                }
                var hand = hands ? Where(HumanBodyBones.RightHand) : null;
                if (hand.HasValue && _hand.HasValue && Time.deltaTime > 0f)
                    _handFastest = Mathf.Max(_handFastest, Vector3.Distance(hand.Value, _hand.Value) / Time.deltaTime);
                _hand = hand;
            }

            public void Tell(ScenarioCheck check, string seat)
            {
                check.That(_seated.Count == LegBones.Length, "the character's hips and legs are found on its rig, " + _seated.Count + " of " + LegBones.Length);
                check.That(_moved <= LegsHold, "the hips and legs stay as seated on the " + seat + " through the draw, the cast and the reel, at most "
                    + LegsHold.ToString("0.00", CultureInfo.InvariantCulture) + " m off, " + _moved.ToString("0.00", CultureInfo.InvariantCulture) + " m at " + _movedBone);
                check.That(SeatedRodPose.TroubleForTest == null, "the fishing motions are read from the game's clips for the character's rig: " + SeatedRodPose.TroubleForTest);
                check.That(_handFastest <= HandSpeed, "on the " + seat + " the hand flows into the pull and back without a jump, at most "
                        + HandSpeed.ToString("0", CultureInfo.InvariantCulture) + " m/s, " + _handFastest.ToString("0.0", CultureInfo.InvariantCulture) + " m/s");
            }

            private Vector3? Where(HumanBodyBones bone)
            {
                var t = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(bone) : null;
                return t != null ? _player.transform.InverseTransformPoint(t.position) : (Vector3?)null;
            }
        }

        /// <summary>How long a cast is watched for its float, in seconds.</summary>
        private const float CastWatch = 6f;

        /// <summary>How much lower than standing the head must be for the character to be seen seated, in metres.</summary>
        private const float SeatedDrop = 0.3f;

        /// <summary>
        /// How high the character's head is above the ground beneath it. The game's own pose labels
        /// say seated while a held rod stands the body up, so a seat is judged by what is seen: the
        /// head lowered.
        /// </summary>
        private static float HeadHeight(Player player)
        {
            var head = player.GetHeadPoint();
            return ZoneSystem.instance.GetSolidHeight(head, out var ground) ? head.y - ground : head.y - player.transform.position.y;
        }

        private static bool Seated(Player player, float standing) => HeadHeight(player) < standing - SeatedDrop;

        private static string Heads(Player player, float standing) =>
            HeadHeight(player).ToString("0.00", CultureInfo.InvariantCulture) + " m up, standing " + standing.ToString("0.00", CultureInfo.InvariantCulture) + " m";

        /// <summary>Stands the character at a place, as if it had walked there.</summary>
        private static void MoveTo(Player player, Vector3 place)
        {
            player.transform.position = place;
            var body = player.GetComponent<Rigidbody>();
            if (body == null) return;
            body.position = place;
            body.linearVelocity = Vector3.zero;
        }

        private static List<FishingFloat> OwnFloats() =>
            FishingFloat.GetAllInstances().Where(f => f != null && f.GetComponent<ZNetView>() is ZNetView v && v.IsValid() && v.IsOwner()).ToList();

        private static bool Picture(ScenarioCheck check, string what)
        {
            var trouble = SelfTestPictures.Take(what);
            check.That(trouble == null, "a picture of " + what + " is taken: " + trouble);
            return true;
        }
    }
}
