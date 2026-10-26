using System;

namespace Rodnarok
{
    /// <summary>What the words after <c>/rodnarok selftest</c> ask for.</summary>
    internal enum SelfTestAsk
    {
        /// <summary>Run the scenarios, those whose names hold the words if any are given.</summary>
        Run,

        /// <summary>Stop the run going on.</summary>
        Stop,

        /// <summary>Write every scenario's name.</summary>
        List,
    }

    /// <summary>A request to the self-test: what it asks, and the words picking scenarios.</summary>
    internal sealed class SelfTestRequest
    {
        public SelfTestRequest(SelfTestAsk ask, string words)
        {
            Ask = ask;
            Words = words;
        }

        public SelfTestAsk Ask { get; }
        public string Words { get; }
    }

    /// <summary>Reads the words typed after <c>/rodnarok</c>: the self-test's start with <c>selftest</c>, anything else is Rodnarok's own.</summary>
    internal static class SelfTestCommand
    {
        public const string Word = "selftest";

        /// <summary>The self-test's request, or null where the words are not the self-test's.</summary>
        public static SelfTestRequest Read(string typed)
        {
            var text = (typed ?? "").Trim();
            if (!text.StartsWith(Word, StringComparison.OrdinalIgnoreCase)) return null;
            var rest = text.Substring(Word.Length);
            if (rest.Length > 0 && !char.IsWhiteSpace(rest[0])) return null;
            rest = rest.Trim();
            if (rest.Equals("stop", StringComparison.OrdinalIgnoreCase)) return new SelfTestRequest(SelfTestAsk.Stop, "");
            if (rest.Equals("list", StringComparison.OrdinalIgnoreCase)) return new SelfTestRequest(SelfTestAsk.List, "");
            return new SelfTestRequest(SelfTestAsk.Run, rest);
        }
    }
}
