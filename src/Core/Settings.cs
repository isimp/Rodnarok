using BepInEx.Configuration;

namespace Rodnarok
{
    /// <summary>
    /// Rodnarok's settings, every one synced to each player by ServerSync: set on the world's
    /// server, its values win while a player is connected to it, the player's own kept beneath,
    /// and only an admin changes them in game. Each turns into the <see cref="FishingRules"/> the
    /// fishing follows.
    /// </summary>
    internal static class Settings
    {
        private const string Section = "Fishing";

        private static ServerSync.ConfigSync _sync;
        private static ConfigEntry<float> _waitMin;
        private static ConfigEntry<float> _waitMax;
        private static ConfigEntry<float> _hookWindow;
        private static ConfigEntry<float> _stallStamina;
        private static ConfigEntry<float> _calmReach;
        private static ConfigEntry<float> _calmKeep;

        /// <summary>The rules as the settings stand now; the defaults before they are bound.</summary>
        public static FishingRules Rules =>
            _waitMin == null
                ? FishingRules.Default
                : FishingRules.Default.With(
                    hookWindow: _hookWindow.Value,
                    waitMin: _waitMin.Value,
                    waitMax: _waitMax.Value,
                    stallStaminaPerSecond: _stallStamina.Value,
                    calmReach: _calmReach.Value,
                    calmKeep: _calmKeep.Value);

        public static void Bind(ConfigFile config)
        {
            var rules = FishingRules.Default;
            _sync = new ServerSync.ConfigSync(About.Guid) { DisplayName = About.Name, CurrentVersion = About.Version, ModRequired = true, IsLocked = true };
            _waitMin = Sync(config.Bind(Section, "WaitMin", rules.WaitMin,
                new ConfigDescription("Shortest wait, in seconds, from the float landing until a fish of the bait's kind comes to it.", new AcceptableValueRange<float>(0f, 300f))));
            _waitMax = Sync(config.Bind(Section, "WaitMax", rules.WaitMax,
                new ConfigDescription("Longest wait, in seconds, from the float landing until a fish of the bait's kind comes to it.", new AcceptableValueRange<float>(0f, 300f))));
            _hookWindow = Sync(config.Bind(Section, "HookWindow", rules.HookWindow,
                new ConfigDescription("Seconds after a bite during which holding the line hooks the fish. The game gives half a second.", new AcceptableValueRange<float>(0.5f, 5f))));
            _stallStamina = Sync(config.Bind(Section, "StallStamina", rules.StallStaminaPerSecond,
                new ConfigDescription("Stamina a second for holding the line while a hooked fish struggles, before Fishing skill lowers it. The line gains nothing meanwhile.", new AcceptableValueRange<float>(0f, 20f))));
            _calmReach = Sync(config.Bind(Section, "CalmReach", rules.CalmReach,
                new ConfigDescription("How far the calm-water piece reaches, in metres. Waves are calmed by the corners of the game's 64 m water tiles, so the calm water fades out over a tile.", new AcceptableValueRange<float>(0f, 128f))));
            _calmKeep = Sync(config.Bind(Section, "CalmKeep", rules.CalmKeep,
                new ConfigDescription("The share of the waves left where the calm-water piece reaches: 0 is flat water, 1 leaves the waves as they are.", new AcceptableValueRange<float>(0f, 1f))));
        }

        private static ConfigEntry<T> Sync<T>(ConfigEntry<T> entry)
        {
            _sync.AddConfigEntry(entry).SynchronizedConfig = true;
            return entry;
        }
    }
}
