namespace Rodnarok
{
    /// <summary>The rules fishing follows right now: the settings, or what the self-test sets for its run.</summary>
    internal static class PlayRules
    {
        /// <summary>Rules the self-test sets for its run in place of the settings.</summary>
        [ForTest]
        public static FishingRules ForTest { get; set; }

        public static FishingRules Now => ForTest ?? Settings.Rules;
    }
}
