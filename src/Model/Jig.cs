using System;

namespace Rodnarok
{
    /// <summary>
    /// Before the hook the rod's secondary attack jigs the float: it twitches <see cref="Metres"/> toward
    /// the rod over <see cref="Seconds"/>, and the rod flicks up for <see cref="FlickSeconds"/>. While a
    /// fish waits at the float, the one picked to bite as it hovers or the nearest of a shy school as it
    /// hesitates, the crosshair ring keeps the bite's size and fades in with the fish's distance across
    /// the water (<see cref="RingStrength"/>): whole within <see cref="Reach"/>, <see cref="Faint"/> at
    /// <see cref="Hover"/>. The waiting fish runs in steadily now and then and holds by the float
    /// (<see cref="NoseAt"/>), so the ring fades in on a rhythm. The first jig of a wait with the fish in
    /// reach draws it <see cref="Cut"/> sooner; any other is off cue, and a wary school near the float
    /// flinches.
    /// </summary>
    internal static class Jig
    {
        /// <summary>How far a jig twitches the float toward the rod, in metres.</summary>
        public const float Metres = 0.25f;

        /// <summary>How long the twitch takes, in seconds.</summary>
        public const float Seconds = 0.25f;

        /// <summary>How much sooner, in seconds, a waiting fish comes for a good jig.</summary>
        public const float Cut = 1.5f;

        /// <summary>How near the float, across the water, the waiting fish is in reach for a jig, in metres: the reach a fish bites from (as <c>Bites.Reach</c>).</summary>
        public const float Reach = 0.6f;

        /// <summary>How far the picked fish hovers from the float, in metres (as <see cref="Bumps.Hover"/>).</summary>
        public const float Hover = Bumps.Hover;

        /// <summary>How long every jig shows the rod flicked up, in seconds.</summary>
        public const float FlickSeconds = 0.15f;

        /// <summary>When a waiting fish first runs in, how often, how long the run and the hold by the float take, in seconds; and how long before it may bite it stops.</summary>
        public const float FirstNose = 0.4f;
        public const float NoseEvery = 2f;
        public const float RunSeconds = 0.8f;
        public const float HoldSeconds = 0.6f;
        public const float NoseLeaveBefore = 0.4f;

        /// <summary>How far from the float, across the water, a run in ends and the fish holds, in metres: within reach.</summary>
        public const float HoldAt = 0.3f;

        /// <summary>How strongly the ring shows with the waiting fish at the edge of its hover or beyond.</summary>
        public const float Faint = 0.15f;

        /// <summary>Where a waiting fish is in its runs in to the float.</summary>
        public enum Nose
        {
            Free,
            Run,
            Hold,
        }

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

        /// <summary>Whether a waiting fish this far from the float across the water is in reach.</summary>
        public static bool InReach(float distance) => distance <= Reach;

        /// <summary>How far out the waiting fish is between the reach and its hover, 0 in reach to 1 at the hover's edge or beyond.</summary>
        private static float Out(float distance) => Clamp01((distance - Reach) / (Hover - Reach));

        /// <summary>How strongly the ring shows while a fish waits this far from the float across the water: whole in reach, <see cref="Faint"/> at the hover's edge or beyond.</summary>
        public static float RingStrength(float distance) => 1f - (1f - Faint) * Out(distance);

        /// <summary>
        /// Where the waiting fish is in its runs in this long into its wait, with this long left before it
        /// may bite, and how far through that part it is: from <see cref="FirstNose"/> every
        /// <see cref="NoseEvery"/> a steady run of <see cref="RunSeconds"/>, a hold of <see cref="HoldSeconds"/>
        /// by the float, then free; free in the last <see cref="NoseLeaveBefore"/>.
        /// </summary>
        public static (Nose Phase, float Progress) NoseAt(float since, float left)
        {
            if (left < NoseLeaveBefore || since < FirstNose) return (Nose.Free, 0f);
            var into = (since - FirstNose) % NoseEvery;
            if (into < RunSeconds) return (Nose.Run, into / RunSeconds);
            if (into < RunSeconds + HoldSeconds) return (Nose.Hold, (into - RunSeconds) / HoldSeconds);
            return (Nose.Free, 0f);
        }

        /// <summary>The steady speed, in metres a second, of a run in from this far out across the water to <see cref="HoldAt"/>.</summary>
        public static float RunSpeed(float startDistance) => Math.Max(0f, startDistance - HoldAt) / RunSeconds;

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
