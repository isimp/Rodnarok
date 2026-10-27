namespace Rodnarok
{
    /// <summary>The game's fishing motion shown on a seated player's arms and chest.</summary>
    internal enum SeatedPose
    {
        /// <summary>None: the game's own animation is left alone.</summary>
        None,

        /// <summary>The throw's wind-up, held while the cast is drawn.</summary>
        Draw,

        /// <summary>The throw, played as the cast goes out.</summary>
        Throw,

        /// <summary>The pull, while the line is reeled in.</summary>
        Reel,

        /// <summary>The stance of holding the rod while the float is out.</summary>
        Hold,
    }

    /// <summary>Which fishing motion a seated player shows, from what they are doing.</summary>
    internal static class SeatedPoses
    {
        public static SeatedPose For(bool seatedWithRod, bool drawing, bool casting, bool reeling, bool floatOut)
        {
            if (!seatedWithRod) return SeatedPose.None;
            if (drawing) return SeatedPose.Draw;
            if (casting) return SeatedPose.Throw;
            if (reeling) return SeatedPose.Reel;
            return floatOut ? SeatedPose.Hold : SeatedPose.None;
        }
    }
}
