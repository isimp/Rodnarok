using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Rodnarok
{
    /// <summary>What a scenario checks, what failed of it, what the place it runs in lacks for it, and what it notes beside its result.</summary>
    internal sealed class ScenarioCheck
    {
        private readonly List<string> _failures = new List<string>();
        private readonly List<string> _notes = new List<string>();
        private readonly List<string> _needs = new List<string>();

        public IReadOnlyList<string> Failures => _failures;
        public IReadOnlyList<string> Notes => _notes;
        public IReadOnlyList<string> Unmet => _needs;

        /// <summary>
        /// Whether the place the character stands in holds what the scenario needs, such as deep
        /// water in front; when it does not, the scenario is skipped saying so rather than failed,
        /// as nothing of the mod has been tried. The scenario stops where it is on false.
        /// </summary>
        public bool Needs(bool holds, string what)
        {
            if (!holds) _needs.Add(what);
            return holds;
        }

        /// <summary>Notes a failure, saying what was expected, unless it holds.</summary>
        public void That(bool holds, string what)
        {
            if (!holds) _failures.Add(what);
        }

        /// <summary>Notes something told beside the result without failing it, such as a part left out and why.</summary>
        public void Note(string what) => _notes.Add(what);
    }

    /// <summary>One part of the self-test: a name, and a body that yields once for each frame it waits.</summary>
    internal sealed class Scenario
    {
        public Scenario(string name, Func<ScenarioCheck, IEnumerable> body)
        {
            Name = name;
            Body = body;
        }

        public string Name { get; }
        public Func<ScenarioCheck, IEnumerable> Body { get; }
    }

    /// <summary>
    /// Runs scenarios one after another, a frame step at a time, and writes each one's result as a
    /// line starting PASS, FAIL or SKIP, the last for a scenario the place could not hold. A
    /// scenario that throws fails with what it threw, and the run goes on to the next.
    /// </summary>
    internal sealed class ScenarioRunner
    {
        private readonly Queue<Scenario> _waiting;
        private readonly Action<string> _write;
        private Scenario _current;
        private ScenarioCheck _check;
        private IEnumerator _steps;

        public ScenarioRunner(IEnumerable<Scenario> scenarios, Action<string> write)
        {
            _waiting = new Queue<Scenario>(scenarios);
            _write = write;
        }

        public bool Running => _current != null || _waiting.Count > 0;
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public int Skipped { get; private set; }

        /// <summary>The scenarios whose names hold the words, ignoring case; all of them for no words.</summary>
        public static IEnumerable<Scenario> Pick(IEnumerable<Scenario> all, string words) =>
            string.IsNullOrWhiteSpace(words) ? all : all.Where(s => s.Name.IndexOf(words.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>One frame's step of the scenario going on, starting the next when one ends.</summary>
        public void Step()
        {
            if (_current == null)
            {
                if (_waiting.Count == 0) return;
                _current = _waiting.Dequeue();
                _check = new ScenarioCheck();
                try
                {
                    _steps = _current.Body(_check).GetEnumerator();
                }
                catch (Exception ex) when (Survivable(ex))
                {
                    End(ex);
                    return;
                }
            }

            try
            {
                if (!_steps.MoveNext()) End(null);
            }
            catch (Exception ex) when (Survivable(ex))
            {
                End(ex);
            }
        }

        /// <summary>Whether a scenario's failure leaves the game able to go on: everything but running out of memory, which ends the run as it ends the game.</summary>
        private static bool Survivable(Exception ex) => !(ex is OutOfMemoryException);

        /// <summary>Ends the run where it is, the scenario going on counted as neither passed nor failed.</summary>
        public void Stop()
        {
            if (!Running) return;
            _write("STOPPED during " + (_current?.Name ?? "the start") + ", " + _waiting.Count + " not run");
            Close();
            _current = null;
            _waiting.Clear();
        }

        private void End(Exception broke)
        {
            var failures = _check.Failures.ToList();
            if (broke != null) failures.Add("broke: " + broke.GetType().Name + ": " + broke.Message);
            var notes = _check.Notes.Count == 0 ? "" : " (note: " + string.Join("; ", _check.Notes) + ")";
            var needs = string.Join("; ", _check.Unmet);
            if (failures.Count > 0)
            {
                Failed++;
                _write("FAIL " + _current.Name + ": " + string.Join("; ", failures) + (needs.Length > 0 ? " (needs " + needs + ")" : "") + notes);
            }
            else if (needs.Length > 0)
            {
                Skipped++;
                _write("SKIP " + _current.Name + ": needs " + needs + notes);
            }
            else
            {
                Passed++;
                _write("PASS " + _current.Name + notes);
            }
            Close();
            _current = null;
        }

        /// <summary>Ends the scenario's body where it is, so what it set out to put back afterwards is put back.</summary>
        private void Close()
        {
            var steps = _steps;
            _steps = null;
            try
            {
                (steps as IDisposable)?.Dispose();
            }
            catch (Exception ex) when (Survivable(ex))
            {
                _write("FAIL " + (_current?.Name ?? "a scenario") + " while putting things back: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
