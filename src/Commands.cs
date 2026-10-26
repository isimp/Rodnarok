namespace Rodnarok
{
    /// <summary>
    /// <c>/rodnarok</c> in the chat or <c>rodnarok</c> in the console. Registered as a normal
    /// command, so the chat accepts it without cheats.
    /// </summary>
    internal static class Commands
    {
        private const string Help = "Rodnarök: the bait decides the fish, in any water. Settings are in BepInEx/config/isimp.Rodnarok.cfg.";

        public static void Register()
        {
            // The game keeps a command as it is made.
            _ = new Terminal.ConsoleCommand("rodnarok", "- tells what Rodnarök does and where its settings are.", args =>
            {
                var rest = args.Length > 1 ? (args.ArgsAll ?? "").Trim() : "";
                args.Context?.AddString(SelfTestHost.Answer(rest) ?? Help);
            });
        }
    }
}
