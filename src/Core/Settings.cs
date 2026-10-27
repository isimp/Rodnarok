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
        private static ConfigEntry<bool> _schools;
        private static ConfigEntry<bool> _bumps;
        private static ConfigEntry<bool> _tension;
        private static ConfigEntry<float> _snapSeconds;
        private static ConfigEntry<float> _easeSeconds;
        private static ConfigEntry<float> _runSpeed;
        private static ConfigEntry<bool> _callWorldFish;
        private static ConfigEntry<bool> _wary;
        private static ConfigEntry<bool> _baitLost;
        private static ConfigEntry<bool> _castHints;
        private static ConfigEntry<bool> _seatedMotions;

        /// <summary>Whether seated players with a rod show the game's fishing motions on their arms; a player's own choice.</summary>
        public static bool SeatedMotions => _seatedMotions != null ? _seatedMotions.Value : SeatedPoses.OnByDefault;

        /// <summary>Sets SeatedMotions as a player would, so the self-test can check the motions and put the setting back; returns what it was.</summary>
        [ForTest]
        public static bool SeatedMotionsForTest(bool on)
        {
            var was = SeatedMotions;
            if (_seatedMotions != null) _seatedMotions.Value = on;
            return was;
        }

        /// <summary>Whether this player's casts show which fish can come; a player's own choice, never the server's.</summary>
        public static bool CastHints => _castHints != null && _castHints.Value;

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
                    calmKeep: _calmKeep.Value,
                    schools: _schools.Value,
                    bumps: _bumps.Value,
                    tension: _tension.Value,
                    snapSeconds: _snapSeconds.Value,
                    easeSeconds: _easeSeconds.Value,
                    runSpeed: _runSpeed.Value,
                    callWorldFish: _callWorldFish.Value,
                    wary: _wary.Value,
                    baitLost: _baitLost.Value);

        public static void Bind(ConfigFile config)
        {
            var rules = FishingRules.Default;
            _sync = new ServerSync.ConfigSync(About.Guid) { DisplayName = About.Name, CurrentVersion = About.Version, ModRequired = true, IsLocked = true };
            _waitMin = Sync(config.Bind(Section, "WaitMin", rules.WaitMin,
                new ConfigDescription("Shortest time, in seconds, a school takes to swim in from out of sight after the float lands. A long way in takes as long as its fish need to stay together.", new AcceptableValueRange<float>(0f, 300f))));
            _waitMax = Sync(config.Bind(Section, "WaitMax", rules.WaitMax,
                new ConfigDescription("Longest time, in seconds, a school takes to swim in from out of sight after the float lands, unless a long way in needs longer.", new AcceptableValueRange<float>(0f, 300f))));
            _hookWindow = Sync(config.Bind(Section, "HookWindow", rules.HookWindow,
                new ConfigDescription("Seconds after a bite during which holding the line hooks the fish. The game gives half a second.", new AcceptableValueRange<float>(0.5f, 5f))));
            _stallStamina = Sync(config.Bind(Section, "StallStamina", rules.StallStaminaPerSecond,
                new ConfigDescription("Stamina a second for holding the line while a hooked fish struggles, before Fishing skill lowers it. The line gains nothing meanwhile.", new AcceptableValueRange<float>(0f, 20f))));
            _calmReach = Sync(config.Bind(Section, "CalmReach", rules.CalmReach,
                new ConfigDescription("How far an item stand calming the waters with a Serpent trophy reaches, in metres. Waves are calmed by the corners of the game's 64 m water tiles, so the calm water fades out over a tile. Below 46 m a stand in the middle of a tile may reach no corner and calm nothing.", new AcceptableValueRange<float>(0f, 128f))));
            _calmKeep = Sync(config.Bind(Section, "CalmKeep", rules.CalmKeep,
                new ConfigDescription("The share of the waves left where a calming item stand reaches: 0 is flat water, 1 leaves the waves as they are.", new AcceptableValueRange<float>(0f, 1f))));
            _schools = Sync(config.Bind(Section, "Schools", rules.Schools,
                "Whether the bait draws a school of its fish, as many as the world spawns together, of which one takes the bait. Off, a single fish comes."));
            _bumps = Sync(config.Bind(Section, "Bumps", rules.Bumps,
                "Whether the fish that takes the bait first bumps the float lightly once or twice before it goes for the hook. A bump is a short, light dip, far lighter than the bite, and can never be hooked; reeling at one drags the float and startles a wary school. The real bite pulls the float under."));
            _tension = Sync(config.Bind(Section, "Tension", rules.Tension,
                "Whether holding the line through a struggle strains it until it snaps, shown by the crosshair ring filling. Easing off lets the strain go while the fish takes line out. Off, holding through a struggle only stalls."));
            _snapSeconds = Sync(config.Bind(Section, "SnapSeconds", rules.SnapSeconds,
                new ConfigDescription("Seconds of holding the line through a struggle until it snaps, with Tension on.", new AcceptableValueRange<float>(0.5f, 10f))));
            _easeSeconds = Sync(config.Bind(Section, "EaseSeconds", rules.EaseSeconds,
                new ConfigDescription("Seconds of easing off, or of the fish resting, until a line strained to snapping is unstrained, with Tension on.", new AcceptableValueRange<float>(0.1f, 10f))));
            _runSpeed = Sync(config.Bind(Section, "RunSpeed", rules.RunSpeed,
                new ConfigDescription("Metres of line a second a struggling fish takes out while the line is eased off, with Tension on. The line breaks past the game's 30 m as ever.", new AcceptableValueRange<float>(0f, 10f))));
            _callWorldFish = Sync(config.Bind(Section, "CallWorldFish", rules.CallWorldFish,
                "Whether a fish of the world's own that takes the bait, within the float's reach and able to swim to it, is called to the float instead of a school. Off, a school comes wherever the water allows one."));
            _baitLost = Sync(config.Bind(Section, "BaitLost", rules.BaitLost,
                "Whether missed bites wear the bait down. On, each bite not hooked in time may take the bait, more likely each time (10%, 40%, 90%, then 98%): taken, the float is bare, nothing bites it again, the school swims off, and reeling it in gives no bait back. A fish that missed bites that float no more, and once every fish of the school has missed, the school swims off and leaves the bait. Off, fish bite the same bait again and again, as in the game."));
            _wary = Sync(config.Bind(Section, "Wary", rules.Wary,
                "Whether a school is wary of the one fishing. On, a player walking or running close to the school (35% of the game's own noise: walking about 5 m, running about 10.5 m; sneaking, sitting and standing still make none) makes it dart 6 m off and come back once all is quiet for 3 s; a short reel with the school near makes it flinch 2.5 m aside, back once the float is still for 1.5 s. A dart counts 2 frights, a flinch 1; at 8, or with the float reeled through it for 2 s, the school leaves. Close in (within 4 m, not shy from 12 m) and in shallow water (under 1 m, not shy from 3 m) a fish hesitates up to 12 s before biting and up to 30% of such schools give up and swim off. Off, a school bites wherever it is."));
            // Shown only to this player, so each player sets it and the server never does.
            _castHints = config.Bind("Display", "CastHints", CastHint.OnByDefault,
                "Whether each cast shows a short line at the top left saying which of the bait's fish can come to the water there, and which would need bigger or deeper water. Your own setting; the server's does not apply.");
            _seatedMotions = config.Bind("Display", "SeatedMotions", SeatedPoses.OnByDefault,
                "Whether a seated player with a rod shows the game's casting, reeling and rod-holding motions on the arms and head while the hips and legs stay as seated, as your game sees every player. Your own setting; the server's does not apply.");
        }

        private static ConfigEntry<T> Sync<T>(ConfigEntry<T> entry)
        {
            _sync.AddConfigEntry(entry).SynchronizedConfig = true;
            return entry;
        }
    }
}
