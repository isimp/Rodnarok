namespace Rodnarok
{
    /// <summary>What the crosshair ring shows.</summary>
    internal enum RingLook
    {
        /// <summary>Nothing to tell.</summary>
        Hidden,

        /// <summary>A fish has bitten: the ring closes as the hook window runs out.</summary>
        Closing,

        /// <summary>The hooked fish struggles: holding the line only stalls, and with tension strains it.</summary>
        Struggle,

        /// <summary>The hooked fish rests: holding the line reels it in.</summary>
        Rest,
    }

    /// <summary>
    /// The crosshair ring, which tells the bite and the fight without words: open the moment a
    /// fish bites and closing over the hook window, then in the fight showing a struggle apart
    /// from a rest, and with tension how strained the line is.
    /// </summary>
    internal readonly struct Ring
    {
        private Ring(RingLook look, float open, float? strain)
        {
            Look = look;
            Open = open;
            Strain = strain;
        }

        public RingLook Look { get; }

        /// <summary>How open the ring is while it closes, 1 at the bite and 0 as the window ends.</summary>
        public float Open { get; }

        /// <summary>How strained the line is in the fight, from 0 to 1 snapped; none without tension.</summary>
        public float? Strain { get; }

        public static Ring Hidden => new Ring(RingLook.Hidden, 0f, null);

        /// <summary>The ring this long after a bite.</summary>
        public static Ring ForBite(float sinceBite, float window)
        {
            if (!Bite.InHookWindow(sinceBite, window)) return Hidden;
            return new Ring(RingLook.Closing, window <= 0f ? 0f : 1f - sinceBite / window, null);
        }

        /// <summary>The ring while a fish is hooked, without tension.</summary>
        public static Ring ForFight(bool struggling) => new Ring(struggling ? RingLook.Struggle : RingLook.Rest, 1f, null);

        /// <summary>The ring while a fish is hooked on a line this strained.</summary>
        public static Ring ForFight(bool struggling, float strain) => new Ring(struggling ? RingLook.Struggle : RingLook.Rest, 1f, strain);
    }
}
