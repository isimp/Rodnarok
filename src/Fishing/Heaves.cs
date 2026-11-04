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

        private struct Lift
        {
            public float Until;
            public Vector3 Way;
        }

        private static readonly Dictionary<Fish, Lift> Lifts = new Dictionary<Fish, Lift>();
        private static readonly HashSet<Fish> Slack = new HashSet<Fish>();

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
            if (!rules.Heave) return;
            foreach (var ff in FishingFloat.GetAllInstances())
            {
                if (ff == null || Lines.OwnerOf(ff) != player) continue;
                var view = ff.GetComponent<ZNetView>();
                var fish = ff.GetCatch();
                if (view == null || !view.IsValid() || !view.IsOwner() || fish == null) continue;
                var item = fish.GetComponent<ItemDrop>();
                var quality = item == null ? 1 : item.m_itemData.m_quality;
                var cost = Heave.Cost(fish.m_escapeStaminaUse, quality, player.GetSkillFactor(Skills.SkillType.Fishing), ff.m_pullStaminaUseMaxSkillMultiplier);
                switch (Heave.OnPress(rules.Heave, true, fish.IsEscaping(), Lifts.ContainsKey(fish), rules.Tension, player.HaveStamina(cost)))
                {
                    case Heave.Pressed.Lifts:
                        var tip = Utils.FindChild(player.transform, "_RodTop");
                        var way = (tip != null ? tip.position : player.transform.position) - fish.transform.position;
                        way.y = 0f;
                        if (way.sqrMagnitude < 0.01f) return;
                        player.UseStamina(cost);
                        Lifts[fish] = new Lift { Until = Time.time + Heave.LiftSeconds, Way = way.normalized };
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

        /// <summary>Whether a hooked fish is being lifted now.</summary>
        public static bool IsLifting(Fish fish) => fish != null && Lifts.ContainsKey(fish);

        /// <summary>Whether a hooked fish's line hangs slack, so its weight is off the reel.</summary>
        public static bool OnSlack(Fish fish) => fish != null && Slack.Contains(fish);

        /// <summary>After the fish's step, in the game owning it: a lifted fish comes toward the rod at the lift's speed until the lift is over or it struggles.</summary>
        public static void MoveFish(Fish fish)
        {
            if (!Lifts.TryGetValue(fish, out var lift)) return;
            if (Time.time >= lift.Until || fish.IsEscaping() || !fish.IsHooked())
            {
                Lifts.Remove(fish);
                return;
            }
            var body = FishBody(fish);
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
            if (Lifts.TryGetValue(fish, out var lift) && Time.time < lift.Until && !fish.IsEscaping())
            {
                var body = FloatBody(ff);
                if (body != null)
                {
                    var velocity = lift.Way * Heave.LiftSpeed;
                    velocity.y = body.linearVelocity.y;
                    body.linearVelocity = velocity;
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
        }
    }
}
