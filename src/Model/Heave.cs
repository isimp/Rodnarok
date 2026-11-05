using System;

namespace Rodnarok
{
    /// <summary>
    /// The rod's secondary attack, which the game leaves unused for a rod, heaves the fish. In a rest a
    /// heave lifts the hooked fish and its float <see cref="Metres"/> toward the rod over
    /// <see cref="LiftSeconds"/>, for the rod's own secondary attack stamina as the game charges an
    /// attack (<see cref="Cost"/>). The line stays as long,
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
        public const float JoltPerMetre = 0.35f;

        /// <summary>How much strain a heave into a struggle puts on the line.</summary>
        public const float StruggleStrain = 0.25f;

        /// <summary>How long a rod already held up dips before it comes back up, in seconds.</summary>
        public const float DipSeconds = 0.15f;

        /// <summary>Whether the rod shows raised this long into a heave: snapped up for the lift, or, held up already, dipped first.</summary>
        public static bool RodRaised(bool holding, float since) => holding ? since >= DipSeconds : since < LiftSeconds;

        /// <summary>Whether the heave's rod motion is over and the rod shows as the game has it again.</summary>
        public static bool PoseOver(bool holding, float since) => since >= (holding ? DipSeconds : LiftSeconds);

        /// <summary>How fast the lift brings the fish in, in metres a second.</summary>
        public static float LiftSpeed => Metres / LiftSeconds;

        /// <summary>What a press of the heave does.</summary>
        public enum Pressed
        {
            Nothing,
            Lifts,
            Strains,

            /// <summary>In a rest with the last heave's slack still out: nothing lifts, but the rod twitches and the ring shakes, so the press shows it was taken.</summary>
            Refused,

            /// <summary>In a rest without the stamina for a lift: nothing lifts, and the stamina bar flashes as for any attack the game has not the stamina for.</summary>
            NoStamina,
        }

        /// <summary>
        /// What a press does: in a rest, with the slack of the last heave wound in (<see cref="Unwound"/>)
        /// and the stamina for it, a lift; with that slack still out, or without the stamina, a refusal that shows; into a
        /// struggle, with the line's strain on, only strain; else nothing.
        /// </summary>
        public static Pressed OnPress(bool on, bool hooked, bool struggling, bool lifting, bool tension, bool staminaEnough, bool slackOut = false)
        {
            if (!on || !hooked || lifting) return Pressed.Nothing;
            if (struggling) return tension ? Pressed.Strains : Pressed.Nothing;
            if (slackOut) return Pressed.Refused;
            return staminaEnough ? Pressed.Lifts : Pressed.NoStamina;
        }

        /// <summary>
        /// Whether this much stamina is enough for a heave of this cost: as the game asks of any attack
        /// (<c>Attack.Start</c>), a tenth more than it costs, and none for one that costs nothing.
        /// </summary>
        public static bool StaminaEnough(float stamina, float cost) => cost <= 0f || stamina > cost + 0.1f;

        /// <summary>
        /// How far the last heave's slack is wound in, from 0 with a whole heave's slack out to 1 once
        /// no more than <see cref="SlackFrom"/> is left and the next heave is free; the rest's ring track
        /// refills by it.
        /// </summary>
        public static float Wound(float unwound) => 1f - Clamp01((unwound - SlackFrom) / (Metres - SlackFrom));

        /// <summary>How long a refused press twitches the rod, in seconds: shorter than a heave's dip, so it reads as a limp try.</summary>
        public const float TwitchSeconds = 0.1f;

        /// <summary>Whether the rod shows raised this long into a refused press's twitch: down, it flicks up; held up, it dips.</summary>
        public static bool TwitchRaised(bool holding, float since) => holding ? since >= TwitchSeconds : since < TwitchSeconds;

        /// <summary>Whether a refused press's twitch is over.</summary>
        public static bool TwitchOver(float since) => since >= TwitchSeconds;

        /// <summary>How long and how far, in the hud's units, the ring shakes sideways at a refused press.</summary>
        public const float ShakeSeconds = 0.25f;
        public const float ShakeAmplitude = 4f;

        /// <summary>How far aside the ring shows this long after a refused press: a quick shake dying away, none outside it.</summary>
        public static float Shake(float since) =>
            since <= 0f || since >= ShakeSeconds ? 0f : (float)Math.Sin(since * 60f) * ShakeAmplitude * (1f - since / ShakeSeconds);

        /// <summary>How much sooner, in seconds, each heave brings the fish's next struggle.</summary>
        public const float RestCut = 2f;

        /// <summary>
        /// When the fish's next struggle comes after a heave whose lift ends at <paramref name="liftEnds"/>:
        /// <see cref="RestCut"/> sooner, but not before the lift is done, so a heave too late in a rest has
        /// the fish bolt as it ends with the whole slack out; never later than it would have come.
        /// </summary>
        public static float ShortenRest(float nextStruggle, float liftEnds) => Math.Min(nextStruggle, Math.Max(liftEnds, nextStruggle - RestCut));

        /// <summary>
        /// The stamina a heave costs: the rod's own secondary attack stamina (20 on the game's rod, as an
        /// iron sword's secondary), already raised by the player's gear and effects as the game does,
        /// lowered by a third at full Fishing skill as the game lowers any attack (<c>Attack.GetAttackStamina</c>).
        /// </summary>
        public static float Cost(float attackStamina, float skill) => Math.Max(0f, attackStamina) * (1f - 0.33f * Clamp01(skill));

        /// <summary>How much longer the line is than the way from the rod tip to the float, in metres; 0 for none.</summary>
        public static float Slack(float line, float distance) => Math.Max(0f, line - distance);

        /// <summary>
        /// The slack the last heave put out that is still out, in metres, after a step with this much
        /// slack on the line: through the lift it is the line's slack; after, it only shrinks, as the line
        /// is wound in or the fish swims away, and slack from the float drifting closer adds none, so only
        /// a heave's own slack holds the next heave back.
        /// </summary>
        public static float Unwound(float unwound, float slack, bool lifting) => lifting ? slack : Math.Min(unwound, slack);

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
