using System;

namespace Rodnarok
{
    /// <summary>
    /// A school is wary of the one fishing, without vanishing at every step. Under water it hears
    /// about a third of the game's own noise, which a player makes walking (15 m) or running and jumping
    /// (30 m) and which fades at 4 m a second, while sneaking, sitting or standing still make none:
    /// a player heard at the float or the school startles it, and it darts a few metres off and
    /// comes back once all has been quiet a moment. A float moved quickly, as a short reel does,
    /// only makes it flinch: a little sidestep away from the float, back as soon as the float is
    /// still. Frights add up, a startle counting twice a flinch, and only many of them make it
    /// leave; so does a float dragged through it for a while, as reeling in does. Close to the one
    /// fishing and in shallow water it hesitates long before biting and often gives up and swims
    /// off; far out over deep water it bites as before.
    /// </summary>
    internal static class Wary
    {
        /// <summary>The share of the game's noise range a fish hears under water.</summary>
        public const float Hearing = 0.35f;

        /// <summary>How far a startled school darts from the noise, and how far a flinching one sidesteps from the float, in metres.</summary>
        public const float DartDistance = 6f;
        public const float FlinchDistance = 2.5f;

        /// <summary>How long all must be quiet before a startled school comes back, and the float still before a flinching one does, in seconds.</summary>
        public const float QuietBeforeComingBack = 3f;
        public const float StillBeforeComingBack = 1.5f;

        /// <summary>How much a startle and a flinch frighten a school, and how much fright makes it leave.</summary>
        public const int StartleFright = 2;
        public const int FlinchFright = 1;
        public const int FrightToLeave = 8;

        /// <summary>How fast a float may move through the school, in metres a second, before it flinches.</summary>
        public const float FloatSpeed = 0.6f;

        /// <summary>How long a float dragged through the school drives it off, in seconds.</summary>
        public const float DraggedSeconds = 2f;

        /// <summary>Within how far of the one fishing a fish is shyest, and from how far out it is not shy, in metres.</summary>
        public const float CloseIn = 4f;
        public const float FarOut = 12f;

        /// <summary>Under how much water a fish is shyest, and from how deep it is not shy, in metres.</summary>
        public const float Shallow = 1f;
        public const float Deep = 3f;

        /// <summary>How long the shyest fish hesitates before biting, in seconds.</summary>
        public const float LongestHesitation = 12f;

        /// <summary>The share of schools of the shyest fish that give up and swim off without biting.</summary>
        public const double MostGivingUp = 0.3;

        /// <summary>Whether a player's noise reaches a fish: the game's own rule, its noise range against the distance, the range cut under water.</summary>
        public static bool Heard(float distance, float noise) => distance < noise * Hearing;

        /// <summary>Whether a school off the float comes back: a startled one all quiet for a while, a flinching one the float still a moment.</summary>
        public static bool ComesBack(float quietFor, bool flinched) => quietFor >= (flinched ? StillBeforeComingBack : QuietBeforeComingBack);

        /// <summary>How much a fright adds: a startle by noise, or a flinch at the float.</summary>
        public static int Fright(bool heard) => heard ? StartleFright : FlinchFright;

        /// <summary>Whether a school this frightened leaves.</summary>
        public static bool Leaves(int fright) => fright >= FrightToLeave;

        /// <summary>Whether a float moving this fast makes a school near it flinch.</summary>
        public static bool FloatStartles(float speed, bool schoolNear) => schoolNear && speed > FloatSpeed;

        /// <summary>
        /// Whether a float moving this fast is being dragged through the school: near it, or after
        /// the school flinched or darted aside from it, as reeling in goes on.
        /// </summary>
        public static bool Dragging(float speed, bool schoolNear, bool schoolAway) => speed > FloatSpeed && (schoolNear || schoolAway);

        /// <summary>Whether a float kept moving fast this long drives the school off.</summary>
        public static bool Dragged(float fastFor) => fastFor >= DraggedSeconds;

        /// <summary>How shy a fish is at the float, from 0 to 1: the shyer of how close the one fishing stands and how shallow the water is.</summary>
        public static float Shyness(float anglerDistance, float depth)
        {
            var close = 1f - Clamp01((anglerDistance - CloseIn) / (FarOut - CloseIn));
            var shallow = 1f - Clamp01((depth - Shallow) / (Deep - Shallow));
            return Math.Max(close, shallow);
        }

        /// <summary>How long a fish this shy hesitates before biting, in seconds.</summary>
        public static float Hesitation(float shyness) => Clamp01(shyness) * LongestHesitation;

        /// <summary>Whether a school this shy gives up and swims off without biting, for a roll from 0 to 1.</summary>
        public static bool GivesUp(float shyness, double roll) => roll < Clamp01(shyness) * MostGivingUp;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
