using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The heave (<see cref="Heave"/>) in the game that cast the float, which runs the fight and owns
    /// the fish it called. A press of the rod's secondary attack, which the game leaves unused while a
    /// rod is drawn as a bow, lifts a resting hooked fish and its float toward the rod tip for the lift's
    /// length, or strains the line of a struggling one. The line keeps its length, so the game draws it
    /// sagging by its own rule; while it hangs slack the reel winds it in faster and the fish's weight
    /// costs no stamina, and a struggle starting with slack out jolts it.
    /// </summary>
    internal static class Heaves
    {
        private static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");
        private static readonly AccessTools.FieldRef<FishingFloat, Rigidbody> FloatBody = AccessTools.FieldRefAccess<FishingFloat, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<Fish, Rigidbody> FishBody = AccessTools.FieldRefAccess<Fish, Rigidbody>("m_body");
        private static readonly AccessTools.FieldRef<Fish, float> NextEscape = AccessTools.FieldRefAccess<Fish, float>("m_nextEscape");

        private struct Lift
        {
            public float Until;
            public Vector3 Way;

            // Stopped where the lift ends, so neither coasts on past the lift's metres.
            public bool FishStopped;
            public bool FloatStopped;
        }

        private static readonly Dictionary<Fish, Lift> Lifts = new Dictionary<Fish, Lift>();
        private static readonly HashSet<Fish> Slack = new HashSet<Fish>();

        /// <summary>Each heaving player's rod motion: when the heave began and whether the rod was held up then.</summary>
        private static readonly Dictionary<Humanoid, (float Since, bool Holding)> Poses = new Dictionary<Humanoid, (float, bool)>();

        private static readonly AccessTools.FieldRef<Character, ZSyncAnimation> Animation = AccessTools.FieldRefAccess<Character, ZSyncAnimation>("m_zanim");
        private static readonly AccessTools.FieldRef<Humanoid, bool> BlockingShown = AccessTools.FieldRefAccess<Humanoid, bool>("m_internalBlockingState");
        private static readonly int RaisedHash = ZSyncAnimation.GetHash("blocking");

        /// <summary>Whether a player's rod shows held up by the game now: the game's own blocking state it last drew.</summary>
        private static bool InternalBlocking(Humanoid humanoid) => humanoid != null && BlockingShown(humanoid);

        /// <summary>
        /// After the game drew a character's block pose, which with a rod is the raised reeling pose that
        /// every game sees: through a heave the rod snaps up for the lift, or, held up already, dips and
        /// comes back up; then it shows as the game has it again.
        /// </summary>
        public static void Pose(Humanoid humanoid)
        {
            if (!Poses.TryGetValue(humanoid, out var pose)) return;
            var zanim = Animation(humanoid);
            if (zanim == null)
            {
                Poses.Remove(humanoid);
                return;
            }
            var since = Time.time - pose.Since;
            if (Heave.PoseOver(pose.Holding, since))
            {
                Poses.Remove(humanoid);
                zanim.SetBool(RaisedHash, InternalBlocking(humanoid));
                return;
            }
            var raised = Heave.RodRaised(pose.Holding, since);
            zanim.SetBool(RaisedHash, raised);
            if (raised) PoseRaisedForTest++;
        }

        /// <summary>How many times a heave's rod motion showed the rod raised, for the self-test.</summary>
        [ForTest]
        public static int PoseRaisedForTest { get; private set; }

        /// <summary>Each frame, the local player's rod motion as well, so it shows whatever order the game updates in.</summary>
        public static void Tick()
        {
            var player = Player.m_localPlayer;
            if (player != null) Pose(player);
        }

        /// <summary>Whether a character's rod is in a heave's motion now, for the self-test.</summary>
        [ForTest]
        public static bool PosingForTest(Humanoid humanoid) => humanoid != null && Poses.ContainsKey(humanoid);

        /// <summary>How many heaves lifted a fish, strained a line, and jolted one at a struggle's start in this game, for the self-test.</summary>
        [ForTest]
        public static int LiftedForTest { get; private set; }

        [ForTest]
        public static int StrainedForTest { get; private set; }

        [ForTest]
        public static int JoltedForTest { get; private set; }

        /// <summary>The slack out when the last jolt came, and the strain it left, for the self-test.</summary>
        [ForTest]
        public static (float Slack, float Before, float After) LastJoltForTest { get; private set; }

        /// <summary>The local player pressed the secondary attack: a heave of the fish on their line, if any.</summary>
        public static void Pressed(Player player)
        {
            if (player == null || player != Player.m_localPlayer) return;
            var rules = PlayRules.Now;
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null || Lines.OwnerOf(ff) != player) continue;
                var view = ff.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || !view.IsOwner()) continue;
                var fish = ff.GetCatch();
                if (fish == null || !rules.Heave) return;
                var cost = CostFor(player);
                switch (Heave.OnPress(rules.Heave, true, fish.IsEscaping(), IsLifting(fish), rules.Tension, player.HaveStamina(cost), OnSlack(fish)))
                {
                    case Heave.Pressed.Lifts:
                        var tip = Utils.FindChild(player.transform, "_RodTop");
                        var way = (tip != null ? tip.position : player.transform.position) - fish.transform.position;
                        way.y = 0f;
                        if (way.sqrMagnitude < 0.01f) return;
                        player.UseStamina(cost);
                        Lifts[fish] = new Lift { Until = Time.time + Heave.LiftSeconds, Way = way.normalized };
                        Poses[player] = (Time.time, InternalBlocking(player));
                        // The fish answers the heave: its next struggle comes sooner, as the game owning it counts the rest.
                        var fishView = fish.GetComponent<ZNetView>();
                        if (fishView != null && fishView.IsValid() && fishView.IsOwner()) NextEscape(fish) = Heave.ShortenRest(NextEscape(fish), Time.time + Heave.LiftSeconds);
                        LiftedForTest++;
                        return;
                    case Heave.Pressed.Strains:
                        Lines.Strain(ff, Heave.StruggleHeave(Lines.StrainOf(ff)));
                        StrainedForTest++;
                        return;
                    default:
                        return;
                }
            }
        }

        /// <summary>
        /// What a heave costs this player: the secondary attack stamina of the rod in hand, raised by gear
        /// and effects and lowered by Fishing skill as the game charges any attack (<see cref="Heave.Cost"/>).
        /// </summary>
        public static float CostFor(Player player)
        {
            var rod = player != null ? player.GetCurrentWeapon() : null;
            if (rod == null) return 0f;
            var stamina = rod.m_shared.m_secondaryAttack.m_attackStamina;
            if (stamina <= 0f) return 0f;
            stamina *= 1f + player.GetEquipmentAttackStaminaModifier();
            player.GetSEMan().ModifyAttackStaminaUsage(stamina, ref stamina);
            return Heave.Cost(stamina, player.GetSkillFactor(Skills.SkillType.Fishing));
        }

        /// <summary>Whether a hooked fish is being lifted now.</summary>
        public static bool IsLifting(Fish fish) => fish != null && Lifts.TryGetValue(fish, out var lift) && Time.time < lift.Until;

        /// <summary>Whether a hooked fish's line hangs slack, so its weight is off the reel.</summary>
        public static bool OnSlack(Fish fish) => fish != null && Slack.Contains(fish);

        /// <summary>After the fish's step, in the game owning it: a lifted fish comes toward the rod at the lift's speed until the lift is over or it struggles.</summary>
        public static void MoveFish(Fish fish)
        {
            if (!Lifts.TryGetValue(fish, out var lift)) return;
            var body = FishBody(fish);
            if (Time.time >= lift.Until || fish.IsEscaping() || !fish.IsHooked())
            {
                if (!lift.FishStopped && body != null) body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
                lift.FishStopped = true;
                if (lift.FloatStopped || !fish.IsHooked()) Lifts.Remove(fish);
                else Lifts[fish] = lift;
                return;
            }
            if (body == null) return;
            var velocity = lift.Way * Heave.LiftSpeed;
            velocity.y = body.linearVelocity.y;
            body.linearVelocity = velocity;
        }

        /// <summary>
        /// In the float's step, in the game that cast it: the float rides a lift with its fish, the line's
        /// slack is noted, a reel winds slack in faster, and a struggle just started is jolted by the slack.
        /// Returns the strain after any jolt.
        /// </summary>
        public static float Step(FishingFloat ff, Fish fish, Character owner, float strain, bool struggleStarts, bool holding, FishingRules rules)
        {
            if (Lifts.TryGetValue(fish, out var lift))
            {
                var body = FloatBody(ff);
                if (Time.time < lift.Until && !fish.IsEscaping())
                {
                    if (body != null)
                    {
                        var velocity = lift.Way * Heave.LiftSpeed;
                        velocity.y = body.linearVelocity.y;
                        body.linearVelocity = velocity;
                    }
                }
                else if (!lift.FloatStopped)
                {
                    if (body != null) body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
                    lift.FloatStopped = true;
                    if (lift.FishStopped) Lifts.Remove(fish);
                    else Lifts[fish] = lift;
                }
            }
            var tip = owner != null ? Utils.FindChild(owner.transform, "_RodTop") : null;
            var slack = tip != null ? Heave.Slack(LineLength(ff), Vector3.Distance(tip.position, ff.transform.position)) : 0f;
            if (Heave.HasSlack(slack) && rules.Heave) Slack.Add(fish);
            else Slack.Remove(fish);

            if (struggleStarts && rules.Tension && rules.Heave && Heave.HasSlack(slack))
            {
                var jolted = Heave.Jolt(strain, slack);
                LastJoltForTest = (slack, strain, jolted);
                JoltedForTest++;
                strain = jolted;
            }
            if (holding && !fish.IsEscaping() && rules.Heave)
            {
                var skill = owner is Player player ? player.GetSkillFactor(Skills.SkillType.Fishing) : 0f;
                var reel = Mathf.Lerp(ff.m_pullLineSpeed, ff.m_pullLineSpeedMaxSkill, skill);
                LineLength(ff) -= Heave.WindExtra(slack, reel, Time.fixedDeltaTime);
            }
            return strain;
        }

        /// <summary>Lets go of a fish no longer on a line.</summary>
        public static void Forget(Fish fish)
        {
            if (fish == null) return;
            Lifts.Remove(fish);
            Slack.Remove(fish);
        }

        /// <summary>Lets go of every heave, on leaving a world.</summary>
        public static void Clear()
        {
            Lifts.Clear();
            Slack.Clear();
            Poses.Clear();
        }
    }
}
