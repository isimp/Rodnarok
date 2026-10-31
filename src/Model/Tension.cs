using System;

namespace Rodnarok
{
    /// <summary>
    /// Line tension, a rule of Rodnarök's own on top of the game's fight, where holding never
    /// strains the line. Holding the line through a struggle strains it from none to snapped over
    /// the snap time. Easing off lets the strain go over the ease time, and while the fish rests it
    /// eases whether the line is held or not. A struggling fish takes line out while the line is
    /// eased off, slower the more strain is still on it, so easing off in short taps loses less line
    /// than letting go. Without tension, holding never strains and no line goes out.
    /// Each fish pulls by its speed in the game: the snap time and run speed are a Perch's, and a
    /// faster fish strains the line sooner and takes more line, by the fourth root of its speed
    /// against the Perch's, so the fish differ while the fastest stays a fight a beginner can win.
    /// </summary>
    internal static class Tension
    {
        /// <summary>The speed, in metres a second, of the Perch, whose pull the snap time and run speed set.</summary>
        public const float PerchSpeed = 3f;

        /// <summary>How much harder than a Perch a fish of this speed pulls; 1 for one of unknown speed or with the pull by speed off.</summary>
        public static float Pull(float fishSpeed, FishingRules rules) =>
            rules.PullBySpeed && fishSpeed > 0f && !float.IsInfinity(fishSpeed) ? (float)Math.Sqrt(Math.Sqrt(fishSpeed / PerchSpeed)) : 1f;

        /// <summary>Seconds of holding through this fish's struggle until the line snaps.</summary>
        public static float SnapSeconds(float fishSpeed, FishingRules rules) => rules.SnapSeconds / Pull(fishSpeed, rules);

        /// <summary>Metres of line a second this fish takes out while eased off with no strain left.</summary>
        public static float RunSpeed(float fishSpeed, FishingRules rules) => rules.RunSpeed * Pull(fishSpeed, rules);

        /// <summary>The strain after a step, from 0 for none to 1 for snapped.</summary>
        public static float Step(float strain, bool struggling, bool holding, float seconds, float fishSpeed, FishingRules rules)
        {
            if (!rules.Tension || float.IsNaN(strain)) return 0f;
            if (struggling && holding)
            {
                var snap = SnapSeconds(fishSpeed, rules);
                return snap <= 0f ? 1f : Math.Min(1f, strain + seconds / snap);
            }
            return rules.EaseSeconds <= 0f ? 0f : Math.Max(0f, strain - seconds / rules.EaseSeconds);
        }

        /// <summary>Whether a line this strained snaps.</summary>
        public static bool Snaps(float strain) => strain >= 1f;

        /// <summary>
        /// Metres a second a struggling fish runs while the line is eased off: its run speed with no
        /// strain left, none at snapping. The line goes out at this speed and the fish swims at it.
        /// </summary>
        public static float Running(float strain, float fishSpeed, FishingRules rules) =>
            rules.Tension && !float.IsNaN(strain) ? RunSpeed(fishSpeed, rules) * (1f - Math.Max(0f, Math.Min(1f, strain))) : 0f;

        /// <summary>Metres of line a struggling fish takes out in a step while the line is eased off.</summary>
        public static float LineOut(float strain, bool struggling, bool holding, float seconds, float fishSpeed, FishingRules rules) =>
            struggling && !holding ? Running(strain, fishSpeed, rules) * seconds : 0f;
    }
}
