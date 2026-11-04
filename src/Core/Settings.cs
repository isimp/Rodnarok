using System.Collections.Generic;
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
        private static ConfigEntry<Difficulty> _difficulty;
        private static ConfigEntry<float> _stallStamina;
        private static ConfigEntry<float> _reelStamina;
        private static ConfigEntry<float> _calmReach;
        private static ConfigEntry<float> _calmKeep;
        private static ConfigEntry<float> _signContrast;
        private static ConfigEntry<bool> _struggleWarning;
        private static ConfigEntry<bool> _heave;
        private static ConfigEntry<bool> _schools;
        private static ConfigEntry<bool> _bumps;
        private static ConfigEntry<bool> _tension;
        private static ConfigEntry<float> _snapSeconds;
        private static ConfigEntry<float> _easeSeconds;
        private static ConfigEntry<float> _runSpeed;
        private static ConfigEntry<bool> _pullBySpeed;
        private static ConfigEntry<bool> _biomeFishOnBait;
        private static ConfigEntry<bool> _callWorldFish;
        private static ConfigEntry<bool> _wary;
        private static ConfigEntry<bool> _baitLost;
        private static ConfigEntry<float> _hearing;
        private static ConfigEntry<int> _frightsToLeave;
        private static ConfigEntry<float> _givingUp;
        private static ConfigEntry<string> _baitChances;
        private static ConfigEntry<bool> _castHints;

        /// <summary>While a level's numbers are written into the fields, so writing them is not taken for a player's edit.</summary>
        private static bool _filling;
        private static ConfigEntry<bool> _seatedMotions;
        private static ConfigEntry<bool> _floatWisp;

        /// <summary>The difficulty level as set: the server's while connected to one, else this player's.</summary>
        public static Difficulty Difficulty => _difficulty != null ? _difficulty.Value : DifficultyLevels.Default;

        /// <summary>Sets Difficulty as a player would, so the self-test can play each level and put the setting back; returns what it was.</summary>
        [ForTest]
        public static Difficulty DifficultyForTest(Difficulty level)
        {
            var was = Difficulty;
            if (_difficulty != null) _difficulty.Value = level;
            return was;
        }

        /// <summary>Whether this player's Wisplight wisp guards their float; a player's own choice.</summary>
        public static bool FloatWisp => _floatWisp != null ? _floatWisp.Value : Rodnarok.FloatWisp.OnByDefault;

        /// <summary>Sets FloatWisp as a player would, so the self-test can check the wisp and put the setting back; returns what it was.</summary>
        [ForTest]
        public static bool FloatWispForTest(bool on)
        {
            var was = FloatWisp;
            if (_floatWisp != null) _floatWisp.Value = on;
            return was;
        }

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

        /// <summary>The rules as the settings stand now, by the difficulty level; the defaults before they are bound.</summary>
        public static FishingRules Rules => _waitMin == null ? FishingRules.Default : DifficultyLevels.Rules(_difficulty.Value, Fields);

        /// <summary>The rules as the fields hold them, the level aside, for the self-test.</summary>
        [ForTest]
        public static FishingRules FieldsForTest => _waitMin == null ? FishingRules.Default : Fields;

        /// <summary>Sets HookWindow as a player would, so the self-test can see an edit switch the level to Custom; returns what it was.</summary>
        [ForTest]
        public static float HookWindowForTest(float seconds)
        {
            var was = _hookWindow != null ? _hookWindow.Value : FishingRules.Default.HookWindow;
            if (_hookWindow != null) _hookWindow.Value = seconds;
            return was;
        }

        private static FishingRules Fields =>
                FishingRules.Default.With(
                    hookWindow: _hookWindow.Value,
                    waitMin: _waitMin.Value,
                    waitMax: _waitMax.Value,
                    stallStaminaPerSecond: _stallStamina.Value,
                    reelStaminaShare: _reelStamina.Value,
                    calmReach: _calmReach.Value,
                    calmKeep: _calmKeep.Value,
                    signContrast: _signContrast.Value,
                    struggleWarning: _struggleWarning.Value,
                    heave: _heave.Value,
                    schools: _schools.Value,
                    bumps: _bumps.Value,
                    tension: _tension.Value,
                    snapSeconds: _snapSeconds.Value,
                    easeSeconds: _easeSeconds.Value,
                    runSpeed: _runSpeed.Value,
                    pullBySpeed: _pullBySpeed.Value,
                    biomeFishOnBait: _biomeFishOnBait.Value,
                    callWorldFish: _callWorldFish.Value,
                    wary: _wary.Value,
                    baitLost: _baitLost.Value,
                    hearing: _hearing.Value,
                    frightToLeave: _frightsToLeave.Value,
                    mostGivingUp: _givingUp.Value,
                    baitTakeChances: BaitLoss.ParseChances(_baitChances.Value) ?? FishingRules.Default.BaitTakeChances);

        /// <summary>
        /// A level picked: its numbers are written into the fields it sets, so the fields show what
        /// is played. Custom leaves them as they are; values arriving from the server are the
        /// server's, already filled there.
        /// </summary>
        private static void FillFields()
        {
            if (_filling || ServerSync.ConfigSync.ProcessingServerUpdate || _difficulty.Value == Difficulty.Custom) return;
            var level = DifficultyLevels.Rules(_difficulty.Value, Fields);
            var file = _difficulty.ConfigFile;
            var saveEach = file.SaveOnConfigSet;
            _filling = true;
            try
            {
                file.SaveOnConfigSet = false;
                _hookWindow.Value = level.HookWindow;
                _snapSeconds.Value = level.SnapSeconds;
                _easeSeconds.Value = level.EaseSeconds;
                _runSpeed.Value = level.RunSpeed;
                _stallStamina.Value = level.StallStaminaPerSecond;
                _reelStamina.Value = level.ReelStaminaShare;
                _wary.Value = level.Wary;
                _hearing.Value = level.Hearing;
                _frightsToLeave.Value = level.FrightToLeave;
                _givingUp.Value = (float)level.MostGivingUp;
                _baitLost.Value = level.BaitLost;
                _baitChances.Value = BaitLoss.ChancesText(level.BaitTakeChances);
                _bumps.Value = level.Bumps;
            }
            finally
            {
                file.SaveOnConfigSet = saveEach;
                _filling = false;
            }
            if (saveEach) file.Save();
        }

        /// <summary>A field a level sets edited by hand to other than the level's number: the level is Custom from then on.</summary>
        private static void FieldEdited()
        {
            if (_filling || ServerSync.ConfigSync.ProcessingServerUpdate) return;
            if (!DifficultyLevels.Matches(_difficulty.Value, Fields)) _difficulty.Value = Difficulty.Custom;
        }

        public static void Bind(ConfigFile config)
        {
            var rules = FishingRules.Default;
            _sync = new ServerSync.ConfigSync(About.Guid) { DisplayName = About.Name, CurrentVersion = About.Version, ModRequired = true, IsLocked = true };
            _difficulty = Sync(config.Bind(Section, "Difficulty", DifficultyLevels.Default,
                "How hard the fishing is, in the five steps of the game's own combat setting. A level sets the hook window, how soon a held struggle snaps the line and how slowly easing off unstrains it, how fast an eased fish takes line, what holding through a struggle, a hooked fish and reeling it in cost, how wary a school is, how fast missed bites wear the bait down, and whether fish bump the float first. VeryEasy: 2.5 s to hook, a Perch snaps a held line after 2 s, half the game's stamina for a hooked fish and for reeling it in, schools are not wary, missed bites cost no bait. Easy: 2 s to hook, 1.5 s to snap, three quarters of that stamina, schools hear a quarter of the game's noise and leave at 12 frights. Normal: 1.5 s to hook, 1 s to snap, the game's own stamina for a hooked fish, schools hear 35% of the noise and leave at 8 frights. Hard: 1 s to hook, 0.75 s to snap, schools hear half the noise and leave at 6 frights, missed bites take the bait more often, fish bump the float first. VeryHard: the game's own half second to hook, 0.5 s to snap, schools hear the game's whole noise as the world's fish do and leave at 4 frights, the third miss always takes the bait. Picking a level writes its numbers into HookWindow, SnapSeconds, EaseSeconds, RunSpeed, StallStamina, ReelStamina, Wary, WaryHearing, WaryFrightsToLeave, WaryGivingUp, BaitLost, BaitLossChances and Bumps, so they show what is played; changing any of them by hand switches to Custom, which plays them as set. The other settings apply at every level."));
            _waitMin = Sync(config.Bind(Section, "WaitMin", rules.WaitMin,
                new ConfigDescription("Shortest time, in seconds, a school takes to swim in from out of sight after the float lands. A long way in takes as long as its fish need to stay together.", new AcceptableValueRange<float>(0f, 300f))));
            _waitMax = Sync(config.Bind(Section, "WaitMax", rules.WaitMax,
                new ConfigDescription("Longest time, in seconds, a school takes to swim in from out of sight after the float lands, unless a long way in needs longer.", new AcceptableValueRange<float>(0f, 300f))));
            _hookWindow = Sync(config.Bind(Section, "HookWindow", rules.HookWindow,
                new ConfigDescription("Seconds after a bite during which holding the line hooks the fish. The game gives half a second. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0.5f, 5f))));
            _stallStamina = Sync(config.Bind(Section, "StallStamina", rules.StallStaminaPerSecond,
                new ConfigDescription("Stamina a second for holding the line while a hooked fish struggles, before Fishing skill lowers it. The line gains nothing meanwhile. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0f, 20f))));
            _reelStamina = Sync(config.Bind(Section, "ReelStamina", rules.ReelStaminaShare,
                new ConfigDescription("The share of the game's own stamina cost for a hooked fish, the drain it costs held or not and reeling it in while it rests: 1 is the game's, 0.5 half of it. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0.1f, 2f))));
            _calmReach = Sync(config.Bind(Section, "CalmReach", rules.CalmReach,
                new ConfigDescription("How far an item stand calming the waters with a Serpent trophy reaches, in metres. Waves are calmed by the corners of the game's 64 m water tiles, so the calm water fades out over a tile. Below 46 m a stand in the middle of a tile may reach no corner and calm nothing.", new AcceptableValueRange<float>(0f, 128f))));
            _calmKeep = Sync(config.Bind(Section, "CalmKeep", rules.CalmKeep,
                new ConfigDescription("The share of the waves left where a calming item stand reaches: 0 is flat water, 1 leaves the waves as they are.", new AcceptableValueRange<float>(0f, 1f))));
            _signContrast = Sync(config.Bind(Section, "SignContrast", rules.SignContrast,
                new ConfigDescription("How strongly the signs of fish coming to a float show on the water, for everyone: 1 as made, 0.5 half as strong, 2 twice as strong with lines half again as wide. Rough water still shows them fainter than calm water.", new AcceptableValueRange<float>(SignLight.MinContrast, SignLight.MaxContrast))));
            _struggleWarning = Sync(config.Bind(Section, "StruggleWarning", rules.StruggleWarning,
                "Whether a hooked fish shows its next struggle coming, for everyone: the float lies still while there is time, grows restless in the last 1.5 s before the struggle, and the line goes taut and quivers a moment before the fish bolts, sooner for a slow fish and later for a fast or starred one. Through the struggle the line stays taut and quivers harder the closer it is to snapping."));
            _heave = Sync(config.Bind(Section, "Heave", rules.Heave,
                "Whether the rod's secondary attack heaves a hooked fish, for everyone. While the fish rests, a heave lifts it and the float 3 m toward you in 0.3 s for the rod's own secondary attack stamina, charged as the game charges an attack (20 on the game's rod at Fishing 0); the line hangs slack, and holding to reel winds slack in three times as fast with none of the fish's weight. If the fish starts to struggle with slack still out, the line is jolted by the slack, a whole heave's slack nearly snapping it. A heave while the fish struggles lifts nothing and strains the line a quarter of the way to snapping. The rod snaps up for the lift, or dips and comes back up if held up already."));
            _schools = Sync(config.Bind(Section, "Schools", rules.Schools,
                "Whether the bait draws a school of its fish, as many as the world spawns together, of which one takes the bait. Off, a single fish comes."));
            _bumps = Sync(config.Bind(Section, "Bumps", rules.Bumps,
                "Whether the fish that takes the bait first bumps the float lightly once or twice before it goes for the hook. A bump is a short, light dip, far lighter than the bite, and can never be hooked; reeling at one drags the float and startles a wary school. The real bite pulls the float under. Set by Difficulty; changing it by hand switches Difficulty to Custom."));
            _tension = Sync(config.Bind(Section, "Tension", rules.Tension,
                "Whether holding the line through a struggle strains it until it snaps, shown by the crosshair ring filling. Easing off lets the strain go while the fish takes line out. Off, holding through a struggle only stalls."));
            _snapSeconds = Sync(config.Bind(Section, "SnapSeconds", rules.SnapSeconds,
                new ConfigDescription("Seconds of holding the line through a Perch's struggle until it snaps, with Tension on. With PullBySpeed on, a faster fish snaps it sooner and a slower one later. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0.5f, 10f))));
            _easeSeconds = Sync(config.Bind(Section, "EaseSeconds", rules.EaseSeconds,
                new ConfigDescription("Seconds of easing off, or of the fish resting, until a line strained to snapping is unstrained, with Tension on. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0.1f, 10f))));
            _runSpeed = Sync(config.Bind(Section, "RunSpeed", rules.RunSpeed,
                new ConfigDescription("Metres of line a second a struggling Perch takes out while the line is eased off with no strain left on it, with Tension on; with strain still on the line it runs slower, half as fast at half strain. With PullBySpeed on, a faster fish takes more line and a slower one less. The fish swims away as fast as the line goes out. The line breaks past the game's 30 m as ever. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0f, 10f))));
            _pullBySpeed = Sync(config.Bind(Section, "PullBySpeed", rules.PullBySpeed,
                "Whether each fish pulls the line by its own swimming speed in the game, with Tension on. SnapSeconds and RunSpeed are a Perch's (speed 3); a faster fish snaps a held line sooner and takes more line eased off, by the fourth root of its speed against the Perch's: a Pike (5) snaps it after 0.88 s and runs 2.3 m/s, a Tuna (10) after 0.74 s and 2.7 m/s, an Anglerfish (2) after 1.1 s and 1.8 m/s. Off, every fish pulls as a Perch."));
            _biomeFishOnBait = Sync(config.Bind(Section, "BiomeFishOnBait", rules.BiomeFishOnBait,
                "Whether the plain Fishing Bait also brings the fish that live where the float lands, as the world spawns them: in the Black Forest a Trollfish as well as a Perch or a Pike, in the Swamp a Giant herring, at sea a Tuna, a Coral cod or a Pufferfish, and so on; each fish that fits the water is as likely. A biome's own bait still brings only its fish, in any water. The Tetra lives only in the Mountain caves and keeps to its Cold Fishing Bait. Off, plain bait brings only the Perch and the Pike."));
            _callWorldFish = Sync(config.Bind(Section, "CallWorldFish", rules.CallWorldFish,
                "Whether a fish of the world's own that takes the bait, within the float's reach and able to swim to it, is called to the float instead of a school. Off, a school comes wherever the water allows one."));
            _baitLost = Sync(config.Bind(Section, "BaitLost", rules.BaitLost,
                "Whether missed bites wear the bait down. On, each bite not hooked in time may take the bait, more likely each time, by BaitLossChances (on Normal 10%, 40%, 90%, then 98%): taken, the float is bare, nothing bites it again, the school swims off, and reeling it in gives no bait back. A fish that missed bites that float no more, and once every fish of the school has missed, the school swims off and leaves the bait. Off, fish bite the same bait again and again, as in the game. Set by Difficulty; changing it by hand switches Difficulty to Custom."));
            _wary = Sync(config.Bind(Section, "Wary", rules.Wary,
                "Whether a school is wary of the one fishing. On, a player walking or running close to the school (WaryHearing of the game's own noise, on Normal 35%: walking about 5 m, running about 10.5 m; sneaking, sitting and standing still make none) makes it dart 6 m off and come back once all is quiet for 3 s; a short reel with the school near makes it flinch 2.5 m aside, back once the float is still for 1.5 s. A dart counts 2 frights, a flinch 1; at WaryFrightsToLeave (8 on Normal), or with the float reeled through it for 2 s, the school leaves. Close in (within 4 m, not shy from 12 m) and in shallow water (under 1 m, not shy from 3 m) a fish hesitates up to 12 s before biting and up to WaryGivingUp of such schools (30% on Normal) give up and swim off. Off, a school bites wherever it is. Set by Difficulty; changing it by hand switches Difficulty to Custom."));
            _hearing = Sync(config.Bind(Section, "WaryHearing", rules.Hearing,
                new ConfigDescription("The share of the game's own noise range a wary school hears under water: 1 is the game's whole range, as the world's fish hear it. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0.05f, 1f))));
            _frightsToLeave = Sync(config.Bind(Section, "WaryFrightsToLeave", rules.FrightToLeave,
                new ConfigDescription("How many frights make a wary school leave, a dart at noise counting 2 and a flinch at a short reel 1. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<int>(1, 30))));
            _givingUp = Sync(config.Bind(Section, "WaryGivingUp", (float)rules.MostGivingUp,
                new ConfigDescription("The share of the shyest schools, close in or over shallow water, that give up and swim off without biting; less shy ones give up less often. Set by Difficulty; changing it by hand switches Difficulty to Custom.", new AcceptableValueRange<float>(0f, 1f))));
            _baitChances = Sync(config.Bind(Section, "BaitLossChances", BaitLoss.ChancesText(rules.BaitTakeChances),
                "The chance, in percent, that the first, second, third and later missed bite on a float takes its bait, with BaitLost on, comma separated; the last stands for every later miss. Text that is no such list plays as Normal's. Set by Difficulty; changing it by hand switches Difficulty to Custom."));

            // Picking a level fills the fields it sets; editing one of them by hand makes it Custom.
            var levelFields = new HashSet<ConfigEntryBase> { _hookWindow, _snapSeconds, _easeSeconds, _runSpeed, _stallStamina, _reelStamina, _wary, _hearing, _frightsToLeave, _givingUp, _baitLost, _baitChances, _bumps };
            _difficulty.SettingChanged += (_, _) => FillFields();
            config.SettingChanged += (_, e) =>
            {
                if (levelFields.Contains(e.ChangedSetting)) FieldEdited();
            };
            FillFields();

            // Shown only to this player, so each player sets it and the server never does.
            _castHints = config.Bind("Display", "CastHints", CastHint.OnByDefault,
                "Whether each cast shows a short line at the top left saying which of the bait's fish can come to the water there, and which would need bigger or deeper water. Your own setting; the server's does not apply.");
            _seatedMotions = config.Bind("Display", "SeatedMotions", SeatedPoses.OnByDefault,
                "Whether a seated player with a rod shows the game's casting, reeling and rod-holding motions on the arms and head while the hips and legs stay as seated, as your game sees every player. Your own setting; the server's does not apply.");
            _floatWisp = config.Bind("Display", "FloatWisp", Rodnarok.FloatWisp.OnByDefault,
                "Whether the wisp of a Wisplight you wear guards your float. While the float waits it drifts round it, searching, and now and then dips low; at a bite it dives in close and flares; through the fight it follows the float, and afterwards comes back to you. Your mist clearing stays with you and the wisp clears a small pocket round the float. Other players see it move but not the flare, since only your game learns of a bite. Your own setting; the server's does not apply.");
        }

        private static ConfigEntry<T> Sync<T>(ConfigEntry<T> entry)
        {
            _sync.AddConfigEntry(entry).SynchronizedConfig = true;
            return entry;
        }
    }
}
