using System.Collections.Generic;

namespace Rodnarok
{
    /// <summary>
    /// The short line a cast shows with cast hints on: which of the bait's fish can come to the water
    /// here and which would need bigger or deeper water, or the fish of the world's own called to the
    /// float. A player's own choice, off unless switched on.
    /// </summary>
    internal static class CastHint
    {
        /// <summary>Whether cast hints are on before a player switches them.</summary>
        public const bool OnByDefault = false;

        /// <summary>The line for a cast, the fish named as the player reads them.</summary>
        public static string Text(IReadOnlyList<string> here, IReadOnlyList<string> needBetterWater, string called)
        {
            if (!string.IsNullOrEmpty(called)) return "A " + called + " nearby is coming to the float.";
            var line = "Here: " + (here.Count > 0 ? string.Join(", ", here) : "none") + ".";
            if (needBetterWater.Count > 0) line += " Needs bigger or deeper water: " + string.Join(", ", needBetterWater) + ".";
            return line;
        }
    }
}
