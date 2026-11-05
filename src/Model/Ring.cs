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

        /// <summary>A school is at the float: the ring grows stronger as the waiting or nearest fish comes closer across the water (<see cref="ApproachRing"/>).</summary>
        Waiting,
    }

    /// <summary>
    /// The crosshair ring, which tells the bite and the fight without words: open the moment a
    /// fish bites and closing over the hook window, then in the fight showing a struggle apart
    /// from a rest, and with tension how strained the line is.
    /// </summary>
    internal readonly struct Ring
    {
        private Ring(RingLook look, float open, float? strain, float wound = 1f)
        {
            Look = look;
            Open = open;
            Strain = strain;
            Wound = wound;
        }

        public RingLook Look { get; }

        /// <summary>How open the ring is while it closes, 1 at the bite and 0 as the window ends.</summary>
        public float Open { get; }

        /// <summary>How strained the line is in the fight, from 0 to 1 snapped; none without tension.</summary>
        public float? Strain { get; }

        /// <summary>In a rest, how far the last heave's slack is wound in, 1 with the next heave free (<see cref="Heave.Wound"/>).</summary>
        public float Wound { get; }

        public static Ring Hidden => new Ring(RingLook.Hidden, 0f, null);

        /// <summary>The ring this long after a bite.</summary>
        public static Ring ForBite(float sinceBite, float window)
        {
            if (!Bite.InHookWindow(sinceBite, window)) return Hidden;
            return new Ring(RingLook.Closing, window <= 0f ? 0f : 1f - sinceBite / window, null);
        }

        /// <summary>The ring while a fish is hooked, without tension.</summary>
        public static Ring ForFight(bool struggling, float wound = 1f) => new Ring(struggling ? RingLook.Struggle : RingLook.Rest, 1f, null, wound);

        /// <summary>The ring while a fish is hooked on a line this strained.</summary>
        public static Ring ForFight(bool struggling, float strain, float wound = 1f) => new Ring(struggling ? RingLook.Struggle : RingLook.Rest, 1f, strain, wound);

        /// <summary>The ring while a school is at the float with its fish this far from it across the water, in metres; <see cref="Open"/> carries the distance.</summary>
        public static Ring ForWaiting(float distance) => new Ring(RingLook.Waiting, distance, null);
    }
}
