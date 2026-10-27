using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>
    /// A Serpent trophy on one of the game's item stands calms the waters around it once a player
    /// taps the use key on that stand, until tapped again or the trophy is taken. The game's own
    /// keys keep their work: holding the use key takes the trophy, the turn key turns it. A boss's
    /// stone, whose trophy cannot be taken, and a stand holding anything else are left alone.
    /// </summary>
    internal static class CalmStand
    {
        /// <summary>The trophy that calms the waters on a stand.</summary>
        public const string Trophy = "TrophySerpent";

        /// <summary>Whether a stand holding this item calms the waters, switched on or not.</summary>
        public static bool Calms(string holds, bool switchedOn) => switchedOn && holds == Trophy;

        /// <summary>Whether a press of the use key on a stand switches its calm rather than doing the game's own work.</summary>
        public static bool TapSwitches(bool hold, bool alt, string holds, bool takesItemsOff) =>
            !hold && !alt && takesItemsOff && holds == Trophy;

        /// <summary>The lines a stand holding the Serpent trophy adds to its hover text, the key named as the game names it.</summary>
        public static IReadOnlyList<string> HoverLines(bool switchedOn, bool gamepad)
        {
            var key = gamepad ? "<b>$KEY_Use</b> " : "[<color=yellow><b>$KEY_Use</b></color>] ";
            return switchedOn
                ? new[] { "Calming the waters", key + "Let the waters be" }
                : new[] { key + "Calm the waters" };
        }
    }
}
