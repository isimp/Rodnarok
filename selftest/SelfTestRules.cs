namespace Rodnarok
{
    /// <summary>
    /// The rules a scenario starts from: the settings at the Normal level, whatever level the world
    /// plays at, so each scenario's timings and checks hold the same on every level. A scenario then
    /// sets what it checks.
    /// </summary>
    internal static class SelfTestRules
    {
        public static FishingRules Normal => DifficultyLevels.Rules(Difficulty.Normal, Settings.Rules);
    }
}
