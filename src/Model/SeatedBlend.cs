using System;

namespace Rodnarok
{
    /// <summary>
    /// How a seated player's fishing motions flow into one another. A change of motion crosses over
    /// from the last to the next rather than cutting; the wind-up is played from the start of the
    /// throw up to where it is held while drawing, and the throw goes on from there; the motions as
    /// a whole fade in as they begin and out as they end, the last one playing on as it fades.
    /// </summary>
    internal sealed class SeatedBlend
    {
        /// <summary>How long one motion takes to cross over into the next, in seconds.</summary>
        public const float CrossSeconds = 0.25f;

        /// <summary>How long the motions take to fade in or out as a whole, in seconds.</summary>
        public const float FadeSeconds = 0.25f;

        /// <summary>How far into the throw the wind-up is held while drawing, as a share of it.</summary>
        public const float WindUp = 0.3f;

        /// <summary>How much the motions show over the game's own animation, from 0 to 1.</summary>
        public float Overlay { get; private set; }

        /// <summary>The share of the motions the throw takes, the wind-up included; with the pull and the stance, adds up to 1.</summary>
        public float Throw { get; private set; }

        /// <summary>The share of the motions the pull takes.</summary>
        public float Reel { get; private set; }

        /// <summary>The share of the motions the stance of holding the rod takes.</summary>
        public float Hold { get; private set; }

        /// <summary>How far the throw has played, as a share of it.</summary>
        public float ThrowShare { get; private set; }

        /// <summary>The motion last called for; it plays on while the motions fade out.</summary>
        public SeatedPose Showing { get; private set; }

        /// <summary>Moves the blend on by a time toward the motion called for now.</summary>
        public void Step(SeatedPose pose, float seconds, float throwLength)
        {
            Overlay = Toward(Overlay, pose == SeatedPose.None ? 0f : 1f, seconds / FadeSeconds);
            if (pose == SeatedPose.None) return;

            if (pose == SeatedPose.Draw && Showing != SeatedPose.Draw) ThrowShare = 0f;
            if (pose == SeatedPose.Throw && Showing != SeatedPose.Draw && Showing != SeatedPose.Throw) ThrowShare = 0f;
            var played = throwLength > 0f ? seconds / throwLength : 1f;
            if (pose == SeatedPose.Draw) ThrowShare = Math.Max(ThrowShare, Math.Min(ThrowShare + played, WindUp));
            if (pose == SeatedPose.Throw) ThrowShare = Math.Min(ThrowShare + played, 1f);

            var throwing = pose == SeatedPose.Draw || pose == SeatedPose.Throw ? 1f : 0f;
            var reeling = pose == SeatedPose.Reel ? 1f : 0f;
            var holding = pose == SeatedPose.Hold ? 1f : 0f;
            if (Throw + Reel + Hold <= 0f)
            {
                // Nothing shown yet: the first motion shows alone while the whole fades in.
                Throw = throwing;
                Reel = reeling;
                Hold = holding;
            }
            else
            {
                var step = seconds / CrossSeconds;
                Throw = Toward(Throw, throwing, step);
                Reel = Toward(Reel, reeling, step);
                Hold = Toward(Hold, holding, step);
                var sum = Throw + Reel + Hold;
                Throw /= sum;
                Reel /= sum;
                Hold /= sum;
            }
            Showing = pose;
        }

        private static float Toward(float from, float to, float step) =>
            from < to ? Math.Min(from + step, to) : Math.Max(from - step, to);
    }
}
