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

        /// <summary>Shortest wait, in seconds, before a fish comes to the float.</summary>
        public float WaitMin { get; private set; } = 10f;

        /// <summary>Longest wait, in seconds, before a fish comes to the float.</summary>
        public float WaitMax { get; private set; } = 30f;

        /// <summary>Stamina a second for holding the line while the fish struggles, before skill lowers it.</summary>
        public float StallStaminaPerSecond { get; private set; } = 2f;

        /// <summary>Metres of line a second reeled in at Fishing 0.</summary>
        public float ReelSpeed { get; private set; } = 2f;

        /// <summary>Metres of line a second reeled in at Fishing 100.</summary>
        public float ReelSpeedMaxSkill { get; private set; } = 6f;

        /// <summary>The share of the reeling cost left at Fishing 100.</summary>
        public float SkillCostShare { get; private set; } = 0.2f;

        /// <summary>How far across the ground the calm-water piece reaches a water corner, in metres.</summary>
        public float CalmReach { get; private set; } = 40f;

        /// <summary>The share of the waves a calmed corner keeps.</summary>
        public float CalmKeep { get; private set; } = 0.1f;

        /// <summary>Whether the bait draws a school; without, one fish comes.</summary>
        public bool Schools { get; private set; } = true;

        /// <summary>Whether the fish that takes the bait bumps the float lightly before it bites.</summary>
        public bool Bumps { get; private set; }

        /// <summary>Seconds after the float is gone before Rodnarök's fish that are left are taken away.</summary>
        public float LeaveSeconds { get; private set; } = 10f;

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
            float? leaveSeconds = null)
        {
            var copy = (FishingRules)MemberwiseClone();
            copy.LeaveSeconds = leaveSeconds ?? LeaveSeconds;
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
