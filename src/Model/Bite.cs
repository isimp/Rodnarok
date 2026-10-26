namespace Rodnarok
{
    /// <summary>
    /// When a bite happens and when reeling hooks it. The fish bites only with its mouth at the
    /// hook, measured through all three axes, so the float never dips with the fish still metres
    /// away; reeling hooks it for as long as the hook window lasts after the bite.
    /// </summary>
    internal static class Bite
    {
        /// <summary>Whether the fish is at the hook: within reach of it, through all three axes.</summary>
        public static bool FishAtHook(Spot fish, Spot hook, float reach) => fish.To(hook) <= reach;

        /// <summary>Whether reeling this long after the bite still hooks the fish.</summary>
        public static bool InHookWindow(float sinceBite, float window) => sinceBite >= 0f && sinceBite <= window;
    }
}
