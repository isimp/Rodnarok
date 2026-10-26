namespace Rodnarok
{
    /// <summary>
    /// Where the in-game self-test plugs in. The self-test is a plugin of its own
    /// (<c>Rodnarok.SelfTest.dll</c>), deployed with a development build and never packaged with
    /// a release, so everything of it lives there: its runner, its scenarios and the words after
    /// /rodnarok it answers. As it loads it registers here, and Rodnarok reaches it only through
    /// here: each frame, and the words typed after /rodnarok. Without it neither does anything.
    /// </summary>
    internal static class SelfTestHost
    {
        /// <summary>What the self-test offers Rodnarok.</summary>
        public interface IRunner
        {
            /// <summary>Called every frame.</summary>
            void Tick();

            /// <summary>The reply to the words typed after /rodnarok where they are the self-test's; null where they are Rodnarok's.</summary>
            string Answer(string typed);
        }

        private static IRunner _runner;

        /// <summary>The self-test registers itself as it loads.</summary>
        [ForTest]
        public static void Register(IRunner runner) => _runner = runner;

        public static void Tick() => _runner?.Tick();
        public static string Answer(string typed) => _runner?.Answer(typed);
    }
}
