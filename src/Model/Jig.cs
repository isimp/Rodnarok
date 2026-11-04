using System;

namespace Rodnarok
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float: it twitches <see cref="Metres"/> toward
    /// the rod over <see cref="Seconds"/>, and the rod flicks up for <see cref="FlickSeconds"/>. While a
    /// fish waits at the float, the one picked to bite as it hovers or the nearest of a shy school as it
    /// hesitates, the crosshair ring breathes with it (<see cref="RingScale"/>): at the bite's size and
    /// whole with the fish within <see cref="Reach"/> of the hook, twice the size and faint at
    /// <see cref="Hover"/>. The waiting fish noses in now and then (<see cref="Nosing"/>), so every wait
    /// has its moments. The first jig of a wait with the fish in reach draws it <see cref="Cut"/> sooner;
    /// any other is off cue, and a wary school near the float flinches.
    /// </summary>
    internal static class Jig
    {
        /// <summary>How far a jig twitches the float toward the rod, in metres.</summary>
        public const float Metres = 0.25f;

        /// <summary>How long the twitch takes, in seconds.</summary>
        public const float Seconds = 0.25f;

        /// <summary>How much sooner, in seconds, a waiting fish comes for a good jig.</summary>
        public const float Cut = 1.5f;

        /// <summary>The reach a fish bites from, in metres, to the hook a metre under the float (as <c>Bites.Reach</c>).</summary>
        public const float Reach = 0.6f;

        /// <summary>How far the picked fish hovers from the float, in metres (as <see cref="Bumps.Hover"/>).</summary>
        public const float Hover = Bumps.Hover;

        /// <summary>How long every jig shows the rod flicked up, in seconds.</summary>
        public const float FlickSeconds = 0.15f;

        /// <summary>When a waiting fish first noses in, how often, and for how long, in seconds; and how long before it may bite it stops.</summary>
        public const float FirstNose = 0.4f;
        public const float NoseEvery = 2f;
        public const float NoseSeconds = 1f;
        public const float NoseLeaveBefore = 0.4f;

        /// <summary>How long a good jig's flash and an off-cue jig's jolt show on the ring, in seconds.</summary>
        public const float FlashSeconds = 0.3f;
        public const float JoltSeconds = 0.6f;

        /// <summary>How fast the float twitches, in metres a second.</summary>
        public static float Speed => Metres / Seconds;

        /// <summary>How a jig is judged.</summary>
        public enum Judged
        {
            Good,
            OffCue,
        }

        /// <summary>Whether a press jigs: a float on the water with nothing hooked, with jigging on.</summary>
        public static bool OnPress(bool on, bool onWater, bool hooked) => on && onWater && !hooked;

        /// <summary>A jig is good only as the first of its wait with the fish in reach.</summary>
        public static Judged Judge(bool usedThisWait, bool inReach) => !usedThisWait && inReach ? Judged.Good : Judged.OffCue;

        /// <summary>Whether a waiting fish this far from the hook is in reach of it.</summary>
        public static bool InReach(float distance) => distance <= Reach;

        /// <summary>How far out the waiting fish is between the reach and its hover, 0 in reach to 1 at the hover's edge or beyond.</summary>
        private static float Out(float distance) => Clamp01((distance - Reach) / (Hover - Reach));

        /// <summary>The ring's size against the bite's while a fish waits this far from the hook: 1 in reach, 2 at the hover's edge.</summary>
        public static float RingScale(float distance) => 1f + Out(distance);

        /// <summary>How strongly the ring shows while a fish waits this far from the hook: whole in reach, a quarter at the hover's edge.</summary>
        public static float RingStrength(float distance) => 1f - 0.75f * Out(distance);

        /// <summary>Whether the waiting fish noses in toward the hook this long into its wait with this long left before it may bite.</summary>
        public static bool Nosing(float since, float left)
        {
            if (left < NoseLeaveBefore || since < FirstNose) return false;
            var phase = (since - FirstNose) % NoseEvery;
            return phase < NoseSeconds;
        }

        /// <summary>When the hovering picked fish may bite after a good jig: <see cref="Cut"/> sooner, never before now; unchanged with no fish waiting.</summary>
        public static float SoonerFree(float freeAt, float now)
        {
            if (freeAt >= float.MaxValue || freeAt <= now) return freeAt;
            return Math.Max(now, freeAt - Cut);
        }

        /// <summary>When a hesitating school counts its hesitation from after a good jig: <see cref="Cut"/> earlier; unchanged for one not yet ready (negative).</summary>
        public static float SoonerReady(float readyAt) => readyAt < 0f ? readyAt : readyAt - Cut;

        /// <summary>Whether the rod shows raised this long into a jig: flicked up, or, held up already, dipped first as in a heave.</summary>
        public static bool RodRaised(bool holding, float since) => holding ? since >= Heave.DipSeconds : since < FlickSeconds;

        /// <summary>Whether the jig's rod motion is over.</summary>
        public static bool PoseOver(bool holding, float since) => since >= (holding ? Heave.DipSeconds : FlickSeconds);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
