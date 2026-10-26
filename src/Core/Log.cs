using BepInEx.Logging;

namespace Rodnarok
{
    /// <summary>Rodnarok's lines in the BepInEx log.</summary>
    internal static class Log
    {
        /// <summary>The plugin's log source, set as it loads.</summary>
        public static ManualLogSource Source { get; set; }

        public static void Info(string line) => Source?.LogInfo(line);
        public static void Warning(string line) => Source?.LogWarning(line);
    }
}
