using System;

namespace Rodnarok
{
    /// <summary>
    /// The rod's secondary attack, which the game leaves unused for a rod, heaves the fish. In a rest a
    /// heave lifts the hooked fish and its float <see cref="Metres"/> toward the rod over
    /// <see cref="LiftSeconds"/>, for what the game charges for pulling that fish while it struggles
    /// (its <c>m_escapeStaminaUse</c> times its quality) for the lift's length. The line stays as long,
    /// so it hangs slack; slack winds in <see cref="WindFactor"/> times as fast as the game's reel, with
    /// no fish on it. A struggle starting with slack out jolts the line by the slack; heaving into a
    /// struggle lifts nothing and only strains the line.
    /// </summary>
    internal static class Heave
    {
        /// <summary>How far a heave lifts the fish toward the rod, in metres.</summary>
        public const float Metres = 3f;

        /// <summary>How long the lift takes, in seconds: short enough that a heave and its wind still beat the game's best reel.</summary>
        public const float LiftSeconds = 0.3f;

        /// <summary>How many times as fast as the game's reel slack line winds in.</summary>
        public const float WindFactor = 3f;

        /// <summary>How much longer than the way to the float the line must be, in metres, to count as slack.</summary>
        public const float SlackFrom = 0.3f;

        /// <summary>How much strain each metre of slack beyond <see cref="SlackFrom"/> puts on the line when a struggle starts.</summary>
        public const float JoltPerMetre = 0.2f;

        /// <summary>How much strain a heave into a struggle puts on the line.</summary>
        public const float StruggleStrain = 0.25f;

        /// <summary>How fast the lift brings the fish in, in metres a second.</summary>
        public static float LiftSpeed => Metres / LiftSeconds;

        /// <summary>What a press of the heave does.</summary>
        public enum Pressed
        {
            Nothing,
            Lifts,
            Strains,
        }

        /// <summary>What a press does: in a rest with the stamina for it a lift; into a struggle, with the line's strain on, only strain; else nothing.</summary>
        public static Pressed OnPress(bool on, bool hooked, bool struggling, bool lifting, bool tension, bool staminaEnough)
        {
            if (!on || !hooked || lifting) return Pressed.Nothing;
            if (struggling) return tension ? Pressed.Strains : Pressed.Nothing;
            return staminaEnough ? Pressed.Lifts : Pressed.Nothing;
        }

        /// <summary>The stamina a heave costs: the fish's struggling cost times its quality for the lift's length, lowered by Fishing skill as the game lowers reeling.</summary>
        public static float Cost(float escapeStaminaUse, int quality, float skill, float maxSkillShare)
        {
            var share = 1f + (maxSkillShare - 1f) * Clamp01(skill);
            return escapeStaminaUse * Math.Max(1, quality) * LiftSeconds * share;
        }

        /// <summary>How much longer the line is than the way from the rod tip to the float, in metres; 0 for none.</summary>
        public static float Slack(float line, float distance) => Math.Max(0f, line - distance);

        /// <summary>Whether the line hangs slack.</summary>
        public static bool HasSlack(float slack) => slack > SlackFrom;

        /// <summary>How much more line, in metres, a reel winds in over a step with slack out, on top of the game's own reel; never more than the slack.</summary>
        public static float WindExtra(float slack, float reelSpeed, float seconds) =>
            HasSlack(slack) ? Math.Min(slack, reelSpeed * (WindFactor - 1f) * seconds) : 0f;

        /// <summary>The strain once a struggle starts with this much slack out: the slack beyond <see cref="SlackFrom"/> jolts the line.</summary>
        public static float Jolt(float strain, float slack) => Clamp01(strain + Math.Max(0f, slack - SlackFrom) * JoltPerMetre);

        /// <summary>The strain after a heave into a struggle.</summary>
        public static float StruggleHeave(float strain) => Clamp01(strain + StruggleStrain);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
