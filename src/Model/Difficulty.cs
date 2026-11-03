namespace Rodnarok
{
    /// <summary>How hard the fishing is, in the five steps of the game's own combat setting; Custom takes the separate settings.</summary>
    internal enum Difficulty
    {
        VeryEasy,
        Easy,
        Normal,
        Hard,
        VeryHard,
        Custom,
    }

    /// <summary>
    /// What each level sets: the hook window, how soon a held struggle snaps the line and how slowly
    /// easing off unstrains it, how fast an eased fish takes line, what holding through a struggle
    /// and reeling in a resting fish cost, how wary a school is, how fast missed bites wear the bait
    /// down, and whether fish bump the float first. Normal is Rodnarok as it played before levels;
    /// Very hard gives the game's own hook window, and its schools hear the game's whole noise as
    /// the world's fish do. The feature switches, the waits and the calm water stay with their own
    /// settings at every level.
    /// </summary>
    internal static class DifficultyLevels
    {
        public const Difficulty Default = Difficulty.Normal;

        /// <summary>The chance that the first, second, third and later miss on a float takes its bait, on Easy, Hard and Very hard.</summary>
        private static readonly double[] EasyChances = { 0.05, 0.2, 0.6, 0.9 };
        private static readonly double[] HardChances = { 0.2, 0.6, 0.95, 1.0 };
        private static readonly double[] VeryHardChances = { 0.4, 0.9, 1.0 };

        /// <summary>The rules for a level, the rest taken from the settings; Custom is the settings as they stand.</summary>
        public static FishingRules Rules(Difficulty level, FishingRules settings)
        {
            switch (level)
            {
                case Difficulty.VeryEasy:
                    return settings.With(hookWindow: 2.5f, snapSeconds: 2f, easeSeconds: 0.5f, runSpeed: 1f, stallStaminaPerSecond: 1f, reelStaminaShare: 0.5f,
                        wary: false, hearing: Normal.Hearing, frightToLeave: Normal.FrightToLeave, mostGivingUp: Normal.MostGivingUp,
                        baitLost: false, baitTakeChances: Normal.BaitTakeChances, bumps: false);
                case Difficulty.Easy:
                    return settings.With(hookWindow: 2f, snapSeconds: 1.5f, easeSeconds: 0.75f, runSpeed: 1.5f, stallStaminaPerSecond: 1.5f, reelStaminaShare: 0.75f,
                        wary: true, hearing: 0.25f, frightToLeave: 12, mostGivingUp: 0.15,
                        baitLost: true, baitTakeChances: EasyChances, bumps: false);
                case Difficulty.Normal:
                    return settings.With(hookWindow: Normal.HookWindow, snapSeconds: Normal.SnapSeconds, easeSeconds: Normal.EaseSeconds, runSpeed: Normal.RunSpeed,
                        stallStaminaPerSecond: Normal.StallStaminaPerSecond, reelStaminaShare: Normal.ReelStaminaShare,
                        wary: Normal.Wary, hearing: Normal.Hearing, frightToLeave: Normal.FrightToLeave, mostGivingUp: Normal.MostGivingUp,
                        baitLost: Normal.BaitLost, baitTakeChances: Normal.BaitTakeChances, bumps: Normal.Bumps);
                case Difficulty.Hard:
                    return settings.With(hookWindow: 1f, snapSeconds: 0.75f, easeSeconds: 1.25f, runSpeed: 2.5f, stallStaminaPerSecond: 3f, reelStaminaShare: 1f,
                        wary: true, hearing: 0.5f, frightToLeave: 6, mostGivingUp: 0.45,
                        baitLost: true, baitTakeChances: HardChances, bumps: true);
                case Difficulty.VeryHard:
                    return settings.With(hookWindow: 0.5f, snapSeconds: 0.5f, easeSeconds: 1.5f, runSpeed: 3f, stallStaminaPerSecond: 4f, reelStaminaShare: 1f,
                        wary: true, hearing: 1f, frightToLeave: 4, mostGivingUp: 0.6,
                        baitLost: true, baitTakeChances: VeryHardChances, bumps: true);
                default:
                    return settings;
            }
        }

        /// <summary>Normal's numbers: the rules as Rodnarok fished before levels.</summary>
        private static FishingRules Normal => FishingRules.Default;
    }
}
