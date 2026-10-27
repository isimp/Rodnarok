namespace Rodnarok
{
    /// <summary>
    /// A seated player's hips, spine and legs stay as the seat has them. The game plays its throw
    /// and its pull on the same animation layer as the sitting pose, standing the whole body up for
    /// them, and plays its sitting down again after; the sitting pose is kept whenever the game
    /// shows it plainly, and put back whenever the game shows anything else, the crossing back to
    /// the seat included, while the player still sits. Standing up lets the game's animation be.
    /// </summary>
    internal static class SeatedBody
    {
        /// <summary>Whether the pose shown now is kept as the seat's: the game shows the plain sitting pose to a seated player.</summary>
        public static bool Keeps(bool seated, bool sittingPose, bool crossing) => seated && sittingPose && !crossing;

        /// <summary>Whether the kept sitting pose is put back now: the player sits, but the game shows something else.</summary>
        public static bool PutsBack(bool seated, bool sittingPose, bool crossing, bool kept) =>
            seated && kept && (!sittingPose || crossing);
    }
}
