# Rodnarök's notes

How Rodnarök works, which of the game's fishing rules it builds on, and what it changes. Rodnarök runs in every player's game (`BepInProcess("valheim.exe")`) and, with the same DLL, on a dedicated server, which sends its fishing rules to everyone on it ([settings](settings.md)). Every player needs it: a player without Rodnarök cannot join a server that has it.

Each note covers one area, starts with what it covers, names the source files it is about on its `Code:` line, and links its neighbours. The player-facing description is the [README](../README.md).

## Where to start

1. [How the game fishes](game-rules.md): the game's rules Rodnarök builds on. Read before changing anything.
2. The note of the area below.
3. [The self-test](selftest.md): what to run in game to see a change work, and how to plan a run of only what a change touched.

## Every note

| Note | Covers |
|---|---|
| [game-rules.md](game-rules.md) | the game's fish, float, bite and fight |
| [arrival.md](arrival.md) | the bait table, biome fish, the school's arrival and swim in, called fish, leaving, keeping off land |
| [wary.md](wary.md) | noise, flinching, shyness, a school leaving and why |
| [bite.md](bite.md) | the claimed float, the picked fish, bumps, the bite's reach, the hook window, bait loss, the ring before the bite |
| [fight.md](fight.md) | the ring in the fight, thrashing, stalling, line tension, pull by speed, the struggle warning, the heave |
| [signs.md](signs.md) | each fish's sign, the path, quiet and follow-in, light, waves, the Trollfish's hump, tremble, darts, stars |
| [calm-water.md](calm-water.md) | the Serpent trophy stand and the calmed waves |
| [sitting.md](sitting.md) | fishing seated, the seated aim, body and rod motions |
| [wisp.md](wisp.md) | a Wisplight's wisp guarding the float |
| [difficulty.md](difficulty.md) | the five levels and Custom |
| [settings.md](settings.md) | each player's settings, the server's, ServerSync, cast hints |
| [other-mods.md](other-mods.md) | rod-only fish, Spearfishing, other catchers |
| [limits.md](limits.md) | what Rodnarök knowingly leaves out |
| [selftest.md](selftest.md) | building, unit tests, the in-game self-test and its plans |
| [releasing.md](releasing.md) | releases and the stores |

## To do something

- Change what a bait brings or how a school comes: [arrival](arrival.md), then [wary](wary.md) if it changes what scares it.
- Change the bite or what the float shows before it: [bite](bite.md), [signs](signs.md).
- Change the fight: [fight](fight.md); the levels setting its numbers are in [difficulty](difficulty.md).
- Add a setting: [settings](settings.md); a number a level sets also goes into [difficulty](difficulty.md).
- A game update: [how the game fishes](game-rules.md), then the self-test's [scenarios](selftest.md).
- Release: [releasing](releasing.md).
