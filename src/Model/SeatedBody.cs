namespace Rodnarok
{
    /// <summary>
    /// A seated player's hips, spine and legs stay as the seat has them. The game plays its throw
    /// and its pull on the same animation layer as the sitting pose, standing the whole body up for
    /// them, and may sit the player down again after, in the sitting state itself. The sitting pose
    /// is kept only once the game has shown it settled, a while into the sitting state with nothing
    /// crossing over, and put back whenever the game shows anything else or a sitting state not yet
    /// settled, while the player still sits. The seat's switch blinking off for a moment keeps the
    /// pose; off for longer, the player stood up and the game's animation is let be.
    /// </summary>
    internal static class SeatedBody
    {
        /// <summary>How long into a sitting state its pose counts as settled, past any sitting down, in seconds.</summary>
        public const float SettleSeconds = 1.5f;

        /// <summary>How long the seat's switch may be off before the kept pose is let go, in seconds.</summary>
        public const float SwitchGrace = 0.5f;

        /// <summary>Whether the pose shown now is kept as the seat's: the game shows a seated player the sitting pose, settled.</summary>
        public static bool Keeps(bool seated, bool sittingPose, bool crossing, float inPose) =>
            seated && sittingPose && !crossing && inPose >= SettleSeconds;

        /// <summary>Whether the kept sitting pose is put back now: the player sits, but the game shows something else or a sitting pose not yet settled.</summary>
        public static bool PutsBack(bool seated, bool sittingPose, bool crossing, float inPose, bool kept) =>
            seated && kept && (!sittingPose || crossing || inPose < SettleSeconds);

        /// <summary>Whether the kept pose is let go, the seat's switch off this long.</summary>
        public static bool Forgets(float switchOffFor) => switchOffFor >= SwitchGrace;
    }
}
