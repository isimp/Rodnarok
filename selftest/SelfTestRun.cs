using System;
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
    /// names hold them; <c>/rodnarok selftest list</c> writes every scenario's name and
    /// <c>/rodnarok selftest stop</c> stops a run. It also starts by itself a few seconds after a
    /// character enters a world when the file <c>BepInEx/Rodnarok-selftest.run</c> exists, with the
    /// words it holds, once per game.
    /// </summary>
    internal sealed class SelfTestRun : SelfTestHost.IRunner
    {
        /// <summary>Seconds a character stands in the world before a run the marker file asks for starts.</summary>
        private const float SettleSeconds = 5f;

        private static string ResultFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest.log");
        private static string MarkerFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Rodnarok-selftest.run");

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
                default:
                    return Start(request.Words);
            }
        }

        private string Start(string words)
        {
            if (_runner != null) return "Rodnarök self-test: a run is going on; /rodnarok selftest stop ends it.";
            if (Player.m_localPlayer == null || ZNetScene.instance == null) return "Rodnarök self-test: enter a world first.";
            var picked = ScenarioRunner.Pick(SelfTestScenarios.All(), words).ToList();
            if (picked.Count == 0) return "Rodnarök self-test: no scenario's name holds \"" + words + "\".";
            Write("");
            Write("Rodnarök self-test " + About.Version + " on Valheim " + global::Version.GetVersionString() + ", " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + (words.Length > 0 ? ", words \"" + words + "\"" : ""));
            _runner = new ScenarioRunner(picked, Write);
            return "Rodnarök self-test: running " + picked.Count.ToString(CultureInfo.InvariantCulture) + " scenario(s), results in " + ResultFile;
        }

        private void Finish()
        {
            var line = "Rodnarök self-test done: " + _runner.Passed.ToString(CultureInfo.InvariantCulture) + " passed, " + _runner.Failed.ToString(CultureInfo.InvariantCulture) + " failed.";
            Write(line);
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
            if (!File.Exists(MarkerFile)) return;
            Log.Info(Start(File.ReadAllText(MarkerFile).Trim()));
        }

        private static void Write(string line) => File.AppendAllText(ResultFile, line + Environment.NewLine);
    }
}
