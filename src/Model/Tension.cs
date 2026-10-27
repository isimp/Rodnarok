using System;

namespace Rodnarok
{
    /// <summary>
    /// Line tension, a rule of Rodnarök's own on top of the game's fight, where holding never
    /// strains the line. Holding the line through a struggle strains it from none to snapped over
    /// the snap time. Easing off lets the strain go over the ease time, and while the fish rests it
    /// eases whether the line is held or not. A struggling fish takes line out while the line is
    /// eased off. Without tension, holding never strains and no line goes out.
    /// </summary>
    internal static class Tension
    {
        /// <summary>The strain after a step, from 0 for none to 1 for snapped.</summary>
        public static float Step(float strain, bool struggling, bool holding, float seconds, FishingRules rules)
        {
            if (!rules.Tension || float.IsNaN(strain)) return 0f;
            if (struggling && holding)
                return rules.SnapSeconds <= 0f ? 1f : Math.Min(1f, strain + seconds / rules.SnapSeconds);
            return rules.EaseSeconds <= 0f ? 0f : Math.Max(0f, strain - seconds / rules.EaseSeconds);
        }

        /// <summary>Whether a line this strained snaps.</summary>
        public static bool Snaps(float strain) => strain >= 1f;

        /// <summary>Metres of line a struggling fish takes out in a step while the line is eased off.</summary>
        public static float LineOut(bool struggling, bool holding, float seconds, FishingRules rules) =>
            rules.Tension && struggling && !holding ? rules.RunSpeed * seconds : 0f;
    }
}
