using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rodnarok
{
    /// <summary>
    /// The scenarios a session plans for a change (<c>BepInEx/Rodnarok-selftest.plan</c>), one line
    /// each, a word and a tab before it: <c>why</c> the change the plan is for, <c>part</c> a scenario
    /// by its whole name, <c>words</c> every scenario whose name holds all the words, and
    /// <c>run on-load</c> to start the plan by itself once a character stands in a world. A scenario
    /// comes off once it passes; a skipped one stays, as it was not checked; words become the
    /// scenarios they matched as the plan is written again; the plan is done once nothing is left.
    /// </summary>
    internal sealed class SelfTestPlan
    {
        private readonly List<(string Kind, string Text)> _asked = new List<(string, string)>();

        /// <summary>The change the plan is for, as the session wrote it; null where it said nothing.</summary>
        public string Why { get; private set; }

        /// <summary>Whether the plan starts by itself once a character stands in a world.</summary>
        public bool OnLoad { get; private set; }

        /// <summary>Whether nothing is left to run.</summary>
        public bool Done => _asked.Count == 0;

        public static SelfTestPlan Load(string text)
        {
            var plan = new SelfTestPlan();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                var kind = line.Substring(0, tab).Trim().ToLowerInvariant();
                var value = line.Substring(tab + 1).Trim();
                if (value.Length == 0) continue;
                switch (kind)
                {
                    case "why": plan.Why = value; break;
                    case "run": plan.OnLoad = value.Equals("on-load", StringComparison.OrdinalIgnoreCase); break;
                    case "part":
                    case "words":
                        plan._asked.Add((kind, value));
                        break;
                }
            }
            return plan;
        }

        /// <summary>The scenarios it asks for, in the order the run takes them, each once.</summary>
        public List<string> Parts(IReadOnlyList<string> all)
        {
            var wanted = new HashSet<string>(_asked.SelectMany(a => Matching(a, all)), StringComparer.Ordinal);
            return all.Where(wanted.Contains).ToList();
        }

        /// <summary>What it asks for that matches no scenario, as written.</summary>
        public List<string> Unmatched(IReadOnlyList<string> all) => _asked.Where(a => !Matching(a, all).Any()).Select(a => a.Text).ToList();

        /// <summary>The plan after a run: what it asked for less every scenario that passed, words turned into the scenarios they matched.</summary>
        public SelfTestPlan After(IReadOnlyList<string> all, IEnumerable<(string Name, ScenarioOutcome Outcome)> ran)
        {
            var passed = new HashSet<string>(ran.Where(r => r.Outcome == ScenarioOutcome.Passed).Select(r => r.Name), StringComparer.Ordinal);
            var left = new SelfTestPlan { Why = Why, OnLoad = OnLoad };
            foreach (var part in Parts(all))
                if (!passed.Contains(part)) left._asked.Add(("part", part));
            return left;
        }

        public string Save()
        {
            var text = new StringBuilder();
            if (Why != null) text.Append("why\t").Append(Why).Append('\n');
            foreach (var (kind, value) in _asked) text.Append(kind).Append('\t').Append(value).Append('\n');
            if (OnLoad) text.Append("run\ton-load\n");
            return text.ToString();
        }

        private static IEnumerable<string> Matching((string Kind, string Text) asked, IReadOnlyList<string> all) =>
            asked.Kind == "part"
                ? all.Where(n => n.Equals(asked.Text, StringComparison.OrdinalIgnoreCase))
                : all.Where(n => ScenarioRunner.HoldsAll(n, asked.Text));
    }

    /// <summary>
    /// The scenarios left from the last runs (<c>BepInEx/Rodnarok-selftest.failed</c>), one name a
    /// line, for <c>/rodnarok selftest failed</c>: those that failed or were skipped, kept until a
    /// run passes them, and those not run again kept as they were.
    /// </summary>
    internal static class SelfTestLeft
    {
        public static List<string> Load(string text) =>
            (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        public static string Save(IEnumerable<string> names) => string.Concat(names.Select(n => n + "\n"));

        /// <summary>What is left after a run: what was left before and not run again, and what failed or was skipped now.</summary>
        public static List<string> After(IReadOnlyList<string> before, IEnumerable<(string Name, ScenarioOutcome Outcome)> ran)
        {
            var runNow = ran.ToList();
            var ranNames = new HashSet<string>(runNow.Select(r => r.Name), StringComparer.Ordinal);
            var left = before.Where(n => !ranNames.Contains(n)).ToList();
            left.AddRange(runNow.Where(r => r.Outcome != ScenarioOutcome.Passed).Select(r => r.Name));
            return left.Distinct(StringComparer.Ordinal).ToList();
        }
    }
}
