# Building and the self-test

How to build, what the unit tests cover, and how the in-game self-test runs: by words, what failed last, or a plan written for a change.

Code: `tests/Rodnarok.Tests`, `selftest/` (`SelfTestRun.cs`, `SelfTestScenarios.cs`, `Model/ScenarioRunner.cs`, `Model/SelfTestCommand.cs`, `Model/SelfTestPlan.cs`).

See also: [releasing](releasing.md); all notes in [the index](index.md).

## Building

`dotnet build` builds against the installed game and deploys into the Gale profile; `dotnet build -c Release -p:LibsDir=lib` builds against the metadata-only copies in `lib/`, as CI does. `dotnet test tests/Rodnarok.Tests` runs the tests of everything that needs no game, and the standards tests that read the source.

## The self-test

The in-game self-test is a plugin of its own (`selftest/`, `Rodnarok.SelfTest.dll`), built and deployed with `dotnet build selftest -c Debug` and never packaged. In a world, `/rodnarok selftest` runs it and writes `BepInEx/Rodnarok-selftest.log`; pictures go to `BepInEx/Rodnarok-selftest-pictures`.

| Command | Runs |
|---|---|
| `/rodnarok selftest` | every scenario |
| `/rodnarok selftest` and words | the scenarios whose names hold all the words, in any order |
| `/rodnarok selftest failed` | what failed or was skipped in the last runs, kept in `BepInEx/Rodnarok-selftest.failed` until a run passes them |
| `/rodnarok selftest plan` | what the plan file asks for |
| `/rodnarok selftest list` | names every scenario |
| `/rodnarok selftest stop` | ends a run |

Each scenario ends PASS, FAIL, or SKIP when the place the character stands in lacks what it needs, such as deep water in front or an out-of-sight spot beyond the float; a skipped scenario says what it needs, tried nothing of the mod, and counts apart from the failures.

## Plans

A change is checked by a plan rather than a whole run: whoever changed something writes `BepInEx/Rodnarok-selftest.plan`, one line each, a word and a tab before it: `why` the change, `part` a scenario by name, `words` every scenario whose name holds the words. After every run the scenarios that passed come off the plan, skipped ones stay, and the file goes once nothing is left. The line `run on-load` starts it by itself a few seconds after a character enters a world; a plan without it waits for `/rodnarok selftest plan`, so the character can be lined up at the water first.

## By itself

With the file `BepInEx/Rodnarok-selftest.run` present the self-test runs by itself a few seconds after a character enters a world, with the words the file holds.
