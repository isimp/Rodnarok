namespace Rodnarok
{
    /// <summary>
    /// Missed bites wear the bait down. The game lets a fish bite the same bait again a second after
    /// a missed bite, for ever, and gives the bait back when the empty float is reeled in. With this
    /// rule a bite neither hooked nor any longer within the hook window is a miss; the first misses
    /// on a float only nibble at the bait, and the last takes it: the float is bare, nothing bites
    /// it again and reeling it in gives no bait back.
    /// </summary>
    internal static class BaitLoss
    {
        /// <summary>How many missed bites on a float take its bait.</summary>
        public const int MissesToTake = 3;

        /// <summary>Whether a bite is a miss now: neither hooked nor any longer within the hook window.</summary>
        public static bool Missed(bool bitten, bool hooked, float sinceBite, float window) =>
            bitten && !hooked && sinceBite > window;

        /// <summary>Whether this many misses on a float take its bait.</summary>
        public static bool Taken(bool on, int misses) => on && misses >= MissesToTake;
    }
}
