namespace Rodnarok
{
    /// <summary>
    /// The numbers Rodnarok fishes by. The defaults are the ones played without settings; the
    /// reel speeds and the skill share are those of the game's own fishing float. A server's
    /// settings set the rest for everyone on it.
    /// </summary>
    internal sealed class FishingRules
    {
        public static readonly FishingRules Default = new FishingRules();

        /// <summary>Seconds after a bite that reeling still hooks the fish; the game gives half a second.</summary>
        public float HookWindow { get; private set; } = 1.5f;

        /// <summary>Shortest time, in seconds, a school swims in after the float lands; a long way in takes as long as its fish need.</summary>
        public float WaitMin { get; private set; } = 10f;

        /// <summary>Longest time, in seconds, a school swims in after the float lands, unless a long way in needs longer.</summary>
        public float WaitMax { get; private set; } = 30f;

        /// <summary>Stamina a second for holding the line while the fish struggles, before skill lowers it.</summary>
        public float StallStaminaPerSecond { get; private set; } = 2f;

        /// <summary>Whether holding through a struggle strains the line; without, holding only stalls.</summary>
        public bool Tension { get; private set; } = true;

        /// <summary>Seconds of holding through a struggle until the line snaps.</summary>
        public float SnapSeconds { get; private set; } = 2f;

        /// <summary>Seconds of easing off until a line strained to snapping is unstrained.</summary>
        public float EaseSeconds { get; private set; } = 1f;

        /// <summary>Metres of line a second a struggling fish takes out while the line is eased off.</summary>
        public float RunSpeed { get; private set; } = 2f;

        /// <summary>Metres of line a second reeled in at Fishing 0.</summary>
        public float ReelSpeed { get; private set; } = 2f;

        /// <summary>Metres of line a second reeled in at Fishing 100.</summary>
        public float ReelSpeedMaxSkill { get; private set; } = 6f;

        /// <summary>The share of the reeling cost left at Fishing 100.</summary>
        public float SkillCostShare { get; private set; } = 0.2f;

        /// <summary>How far across the ground a calming item stand reaches a water corner, in metres.</summary>
        public float CalmReach { get; private set; } = 48f;

        /// <summary>The share of the waves a calmed corner keeps.</summary>
        public float CalmKeep { get; private set; } = 0.1f;

        /// <summary>Whether the bait draws a school; without, one fish comes.</summary>
        public bool Schools { get; private set; } = true;

        /// <summary>Whether a fish of the world's own that takes the bait within the float's reach is called to it instead of a school.</summary>
        public bool CallWorldFish { get; private set; } = true;

        /// <summary>Whether a school is wary of the one fishing: startled by noise and a quickly dragged float, shy close in and in shallow water.</summary>
        public bool Wary { get; private set; } = true;

        /// <summary>Whether a bite not hooked in time takes the bait, so the float is bare and reeling it in gives none back.</summary>
        public bool BaitLost { get; private set; } = true;

        /// <summary>Whether the fish that takes the bait bumps the float lightly before it bites.</summary>
        public bool Bumps { get; private set; }

        /// <summary>How far, in metres, a leaving fish must be from every player to be taken away out of sight.</summary>
        public float OutOfSight { get; private set; } = 40f;

        /// <summary>How deep under the surface, in metres, a leaving fish dives to be taken away out of sight.</summary>
        public float DiveDepth { get; private set; } = 4f;

        /// <summary>Seconds after its float is gone that a leaving fish still in sight is taken away anyway, as one stuck.</summary>
        public float LeaveBackstop { get; private set; } = 30f;

        /// <summary>These rules with the values given changed.</summary>
        public FishingRules With(
            float? hookWindow = null,
            float? waitMin = null,
            float? waitMax = null,
            float? stallStaminaPerSecond = null,
            float? calmReach = null,
            float? calmKeep = null,
            bool? schools = null,
            bool? bumps = null,
            float? leaveBackstop = null,
            bool? tension = null,
            float? snapSeconds = null,
            float? easeSeconds = null,
            float? runSpeed = null,
            bool? callWorldFish = null,
            bool? wary = null,
            bool? baitLost = null)
        {
            var copy = (FishingRules)MemberwiseClone();
            copy.CallWorldFish = callWorldFish ?? CallWorldFish;
            copy.Wary = wary ?? Wary;
            copy.BaitLost = baitLost ?? BaitLost;
            copy.Tension = tension ?? Tension;
            copy.SnapSeconds = snapSeconds ?? SnapSeconds;
            copy.EaseSeconds = easeSeconds ?? EaseSeconds;
            copy.RunSpeed = runSpeed ?? RunSpeed;
            copy.LeaveBackstop = leaveBackstop ?? LeaveBackstop;
            copy.Schools = schools ?? Schools;
            copy.Bumps = bumps ?? Bumps;
            copy.HookWindow = hookWindow ?? HookWindow;
            copy.WaitMin = waitMin ?? WaitMin;
            copy.WaitMax = waitMax ?? WaitMax;
            copy.StallStaminaPerSecond = stallStaminaPerSecond ?? StallStaminaPerSecond;
            copy.CalmReach = calmReach ?? CalmReach;
            copy.CalmKeep = calmKeep ?? CalmKeep;
            return copy;
        }
    }
}
