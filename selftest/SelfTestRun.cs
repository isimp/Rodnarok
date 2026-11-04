using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Rodnarok
{
    /// <summary>
    /// The in-game self-test: scenarios that check, in the running game, what the model and the
    /// game layer rely on, written to <c>BepInEx/Rodnarok-selftest.log</c>. It reads the game and
    /// changes nothing in the world, so it is safe on a real character.
    ///
    /// It starts with <c>/rodnarok selftest</c>, or with words after it for the scenarios whose
    /// names hold all of them; <c>/rodnarok selftest failed</c> runs again what failed or was
    /// skipped last (<c>BepInEx/Rodnarok-selftest.failed</c>), <c>/rodnarok selftest plan</c> the
    /// scenarios the plan a session wrote for a change asks for (<c>BepInEx/Rodnarok-selftest.plan</c>,
    /// <see cref="SelfTestPlan"/>), each coming off it once it passes; <c>/rodnarok selftest list</c>
    /// writes every scenario's name and <c>/rodnarok selftest stop</c> stops a run. It also starts by
    /// itself a few seconds after a character enters a world when the file
    /// <c>BepInEx/Rodnarok-selftest.run</c> exists, with the words it holds, or else when the plan
    /// asks to, once per game.
    /// </summary>
    internal sealed class SelfTestRun : SelfTestHost.IRunner
    {
        /// <summary>Seconds a character stands in the world before a run the marker file asks for starts.</summary>
        private const float SettleSeconds = 5f;

        private static string ResultFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest.log");
        private static string MarkerFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest.run");

        /// <summary>The plan a session writes for a change (<see cref="SelfTestPlan"/>) and the scenarios left from the last runs (<see cref="SelfTestLeft"/>).</summary>
        private static string PlanFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest.plan");
        private static string LeftFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest.failed");

        private ScenarioRunner _runner;
        private bool _markerRead;
        private float _inWorldSince = -1f;

        public void Tick()
        {
            if (_runner != null)
            {
                _runner.Step();
                if (!_runner.Running) Finish();
                return;
            }
            StartFromMarker();
        }

        public string Answer(string typed)
        {
            var request = SelfTestCommand.Read(typed);
            if (request == null) return null;
            switch (request.Ask)
            {
                case SelfTestAsk.Stop:
                    if (_runner == null) return "Rodnarök self-test: nothing is running.";
                    _runner.Stop();
                    Finish();
                    return "Rodnarök self-test: stopped.";
                case SelfTestAsk.List:
                    return string.Join(", ", SelfTestScenarios.All().Select(s => s.Name));
                case SelfTestAsk.Failed:
                    var left = new HashSet<string>(SelfTestLeft.Load(Read(LeftFile)), StringComparer.Ordinal);
                    if (left.Count == 0) return "Rodnarök self-test: nothing failed or was skipped in the last runs.";
                    return Start(SelfTestScenarios.All().Where(s => left.Contains(s.Name)).ToList(), "the scenarios that failed or were skipped last");
                case SelfTestAsk.Plan:
                    return StartPlan();
                default:
                    return Start(request.Words);
            }
        }

        private string Start(string words)
        {
            var picked = ScenarioRunner.Pick(SelfTestScenarios.All(), words).ToList();
            if (picked.Count == 0) return "Rodnarök self-test: no scenario's name holds all of \"" + words + "\".";
            return Start(picked, words.Length > 0 ? "words \"" + words + "\"" : null);
        }

        /// <summary>Starts the scenarios the plan asks for, saying why and what it asks for that no scenario matches.</summary>
        private string StartPlan()
        {
            var plan = SelfTestPlan.Load(Read(PlanFile));
            if (plan.Done) return "Rodnarök self-test: no plan is left; " + PlanFile + " is missing or holds nothing.";
            var names = SelfTestScenarios.All().Select(s => s.Name).ToList();
            var parts = new HashSet<string>(plan.Parts(names), StringComparer.Ordinal);
            var unmatched = plan.Unmatched(names);
            var answer = Start(SelfTestScenarios.All().Where(s => parts.Contains(s.Name)).ToList(), "the plan" + (plan.Why != null ? " for: " + plan.Why : ""));
            if (unmatched.Count > 0 && _runner != null) Write("The plan asks for what no scenario's name matches: " + string.Join("; ", unmatched));
            return answer;
        }

        private string Start(List<Scenario> picked, string what)
        {
            if (_runner != null) return "Rodnarök self-test: a run is going on; /rodnarok selftest stop ends it.";
            if (Player.m_localPlayer == null || ZNetScene.instance == null) return "Rodnarök self-test: enter a world first.";
            if (picked.Count == 0) return "Rodnarök self-test: no scenario to run for " + (what ?? "that") + ".";
            Write("");
            Write("Rodnarök self-test " + About.Version + " on Valheim " + global::Version.GetVersionString() + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + (what != null ? ", " + what : ""));
            _runner = new ScenarioRunner(picked, Write);
            return "Rodnarök self-test: running " + picked.Count.ToString(CultureInfo.InvariantCulture) + " scenario(s), results in " + ResultFile;
        }

        private void Finish()
        {
            var line = "Rodnarök self-test done: " + _runner.Passed.ToString(CultureInfo.InvariantCulture) + " passed, " + _runner.Failed.ToString(CultureInfo.InvariantCulture) + " failed"
                + (_runner.Skipped > 0 ? ", " + _runner.Skipped.ToString(CultureInfo.InvariantCulture) + " skipped for the place (each says what it needs)" : "") + ".";
            Write(line);
            KeepOutcomes(_runner.Outcomes);
            Log.Info(line);
            Chat.instance?.AddString(line);
            _runner = null;
        }

        /// <summary>Starts the run the marker file asks for, once per game, a few seconds after a character stands in a world.</summary>
        private void StartFromMarker()
        {
            if (_markerRead) return;
            if (Player.m_localPlayer == null || ZNetScene.instance == null)
            {
                _inWorldSince = -1f;
                return;
            }
            if (_inWorldSince < 0f) _inWorldSince = Time.time;
            if (Time.time - _inWorldSince < SettleSeconds) return;
            _markerRead = true;
            if (File.Exists(MarkerFile))
            {
                Log.Info(Start(File.ReadAllText(MarkerFile).Trim()));
                return;
            }
            // A plan asking to start by itself does, once the character has settled.
            var plan = SelfTestPlan.Load(Read(PlanFile));
            if (plan.OnLoad && !plan.Done) Log.Info(StartPlan());
        }

        private static void Write(string line) => File.AppendAllText(ResultFile, line + Environment.NewLine);

        private static string Read(string file) => File.Exists(file) ? File.ReadAllText(file) : null;

        /// <summary>After a run: what failed or was skipped kept for <c>failed</c>, and the scenarios that passed taken off the plan, which goes once nothing is left.</summary>
        private static void KeepOutcomes(IReadOnlyList<(string Name, ScenarioOutcome Outcome)> outcomes)
        {
            try
            {
                File.WriteAllText(LeftFile, SelfTestLeft.Save(SelfTestLeft.After(SelfTestLeft.Load(Read(LeftFile)), outcomes)));
                var planText = Read(PlanFile);
                if (planText == null) return;
                var left = SelfTestPlan.Load(planText).After(SelfTestScenarios.All().Select(s => s.Name).ToList(), outcomes);
                if (left.Done) File.Delete(PlanFile);
                else File.WriteAllText(PlanFile, left.Save());
            }
            catch (IOException ex)
            {
                Log.Warning("Rodnarök self-test could not keep what is left to run: " + ex.Message);
            }
        }
    }
}
