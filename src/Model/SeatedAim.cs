using System;

namespace Rodnarok
{
    /// <summary>
    /// Where a seated player's cast goes. The game throws the rod's cast along the way the body
    /// faces, and turns a standing body toward the aim while drawing. Sitting on the ground the body
    /// turns too, so the cast goes where the player looks; a chair holds the body facing its own way,
    /// so the cast follows the aim across the chair's front, up to <see cref="ChairArc"/> to either side.
    /// </summary>
    internal static class SeatedAim
    {
        /// <summary>How far to either side of a chair's facing a cast can go, in degrees.</summary>
        public const float ChairArc = 60f;

        /// <summary>The way the cast goes, as a yaw in degrees from 0 to 360, from the body's yaw and the aim's.</summary>
        public static float ThrowYaw(float bodyYaw, float lookYaw, bool onChair)
        {
            if (!onChair) return Wrap(lookYaw);
            var turn = Wrap(lookYaw - bodyYaw);
            if (turn > 180f) turn -= 360f;
            return Wrap(bodyYaw + Math.Max(-ChairArc, Math.Min(ChairArc, turn)));
        }

        private static float Wrap(float degrees)
        {
            var wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }
    }
}
