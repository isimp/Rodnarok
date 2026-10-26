namespace Rodnarok
{
    /// <summary>What a hooked fish costs to reel: its stamina a second resting and struggling, as its prefab sets them, and its quality.</summary>
    internal readonly struct FishPull
    {
        public FishPull(float restStamina, float struggleStamina, int quality)
        {
            RestStamina = restStamina;
            StruggleStamina = struggleStamina;
            Quality = quality;
        }

        public float RestStamina { get; }
        public float StruggleStamina { get; }
        public int Quality { get; }
    }

    /// <summary>What a step of holding the line gave and cost: metres of line reeled in, and stamina.</summary>
    internal readonly struct ReelStep
    {
        public ReelStep(float line, float stamina)
        {
            Line = line;
            Stamina = stamina;
        }

        public float Line { get; }
        public float Stamina { get; }
    }

    /// <summary>
    /// Holding the line through a fight. While the fish rests it reels in at the game's pace and
    /// cost: the float's reel speed rising with Fishing skill, and the fish's resting stamina by
    /// its quality, falling with skill. While it struggles the line stalls: nothing is gained, and
    /// it costs the rules' small stall cost, the same for every fish, falling with skill as reeling
    /// does, so holding on through a struggle never drains a player empty.
    /// </summary>
    internal static class Fight
    {
        /// <summary>One step of holding the line, <paramref name="skill"/> being Fishing skill from 0 to 1.</summary>
        public static ReelStep Hold(bool struggling, float seconds, float skill, FishPull fish, FishingRules rules)
        {
            var cheaper = Lerp(1f, rules.SkillCostShare, skill);
            if (struggling) return new ReelStep(0f, rules.StallStaminaPerSecond * cheaper * seconds);
            var speed = Lerp(rules.ReelSpeed, rules.ReelSpeedMaxSkill, skill);
            return new ReelStep(speed * seconds, fish.RestStamina * fish.Quality * cheaper * seconds);
        }

        private static float Lerp(float from, float to, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return from + (to - from) * t;
        }
    }
}
