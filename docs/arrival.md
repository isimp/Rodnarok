# What a bait brings and how it comes

Which fish a bait brings, how a school of them appears and swims in, how a fish of the world's own is called instead, and how a school leaves.

Code: `src/Model/BaitTable.cs`, `src/Fishing/FishBook.cs`, `src/Model/School.cs`, `src/Model/Arrival.cs`, `src/Model/Drift.cs`, `src/Model/Stars.cs`, `src/Model/CastHint.cs`, `src/Fishing/Schools.cs`, `src/Patches/FishPatches.cs`.

See also: [how the game fishes](game-rules.md), [wary schools](wary.md), [the bite](bite.md), [signs](signs.md), [limits](limits.md); all notes in [the index](index.md).

## The bait table

The bait table is built at runtime from the baits the game's own fish take (`BaitTable`), so a fish another mod adds with a bait of its own comes too. A bait two fish take brings each in equal share.

With the server setting `BiomeFishOnBait` on, as by default, the game's plain `FishingBait`, the bait every biome's bait is made from, also brings every fish the world's spawn lists spawn in the biome the float lands in. `FishBook` reads each fish's biomes from the enabled entries of every spawn list, judged as a spawn judges its point, by the biome of the ground under it (`ZoneSystem.GetGroundData`): the Trollfish in the Black Forest, the Giant herring in the Swamp, the Grouper in the Plains, the Tuna, Coral cod and Pufferfish at sea, the Anglerfish and Pufferfish in the Mistlands, the Northern salmon in the Deep North and the Magmafish in the Ashlands, beside the Perch and the Pike that take plain bait anywhere. Each fish that fits takes an equal share. A biome's own bait still brings only its fish, in any water. The Tetra spawns only from the Mountain caves' own spawners, in no biome of the world, and keeps to its Cold Fishing Bait.

The game lets a fish bite only a bait its prefab lists (`Fish.TestBate`), so with the setting on a fish takes plain bait where it lives at the chance its own bait has (`BiomeFishTakePlainBait`); the same rule picks the fish of the world's own a float calls, and the cast hint names these fish too ([settings](settings.md)).

## The school

A while after the float lands, the bait draws a school of its fish (`School`): as many as the world spawns of that fish together (a Perch comes in twos to sixes, a Northern Salmon in threes to tens), each size as likely, and two to four of a fish the world never spawns in groups, such as the cave fish. Each fish rolls its stars as the world's spawns do, with the game's own level-up roll beside the float, and the game draws a fish 40% larger for each star; a cave fish rolls them by the world's general rule. One of them, each as likely, takes the bait, so the catch keeps the game's odds of stars. When it is hooked the others scatter. With `Schools` off a single fish comes.

## What comes, and where from

When a float the player's own game cast lies in the water, the wait is rolled (`Schools`); when it is over, what comes is decided (`Arrival.Choose`).

A fish of the world's own that takes the bait is called first, if this game steers it, it is within the float's reach (the game's 50 m) and the water is deep enough for it every 2 m of the way to the float: it goes for the float at its next choice, hears no noise while called, and nothing is spawned; once hooked or let go it is the game's again (setting `CallWorldFish`, on by default).

Without one, a school comes only where the water allows its whole visit: a place 6 to 14 m out to come from, and a way out from the float through water deep enough for it all the way, 40 m long or to water 4.5 m deep to dive in. Where it does not, no school comes and a line on the screen says once that there is too little water here for a school and that fish of the world's own still bite. A school also comes only once the caster's last school has left, so casting again never stacks schools.

The school appears out of sight beyond the float as seen from the one fishing, by the same measure a leaving fish is taken away by (`Arrival.StartOutOfSight`): at the nearest place, up to 60 m out and through water deep enough for its fish all the way in, where the whole school is 40 m from every player; else 6 to 14 m out where the water lets it swim 4 m under the surface. Its fish spread up to 4 m apart, facing every way, each placed only where the water is deep enough for it.

## The swim in

The rolled wait is its swim in (`Drift`), counted from the float landing. The game picks each fish's next place to swim around its home, and the school's home creeps from where it appeared to the float over that time, or longer on a long way in, so that the fish, cruising at their own speed up to 3 m/s, always swim half again as fast as their home: from 40 m that is 20 s for a Perch and 30 s for the slowest fish. Its fish wander up to 6 m around the home, and a school that started deep rises to its own depth as it comes.

At the float each fish circles it within 4 m at its own speed, picking a new place to swim every 1.5 to 3 seconds, near the top of the depth it swims at. The saved home of each fish is the float from the start, so another game taking a fish over sends it straight there.

Once the swim is over and half the school is near the float, or 8 seconds after that whatever the school is doing, one fish is picked and let go for the bait a couple of seconds later ([the bite](bite.md)); no other fish of the school goes for any float.

Only a Tuna or a Northern salmon of a quality the game lets leap (`m_jumpMaxLevel`) leaps on its way in, at most every 3 to 6 s, with the chance the game gives its own fish by the hour (full at a quarter and three quarters of the day, none at noon and midnight), and only where the water stays deep for 10 m ahead. The game's own leap values are the same for every fish, so which fish leap is Rodnarök's choice, the two that leap in life. A school rising from deep leaps only near the end of the way in.

## Leaving

When the picked fish is hooked, the rest make a place 30 m out in open water their home, in the direction most away from the float whose water is deep enough for them every 2 m of the way, so never across a spit of land or toward the shore, a shorter way of 20 or 10 m where no 30 m one is, and swim there at the game's flee speed. When the float is gone, all of them do, diving where the water is 4 m deep or more, and each is taken away only out of sight: 40 m from every player or 4 m under the surface; one stuck in sight is taken away 30 seconds on. As a school only comes where it has a way out, that is a safety net for a fish caught on something, not how fish leave.

Rodnarök marks its own fish, and any player's game takes away a marked fish it finds with no float, so none gather in the save. A fish of the world's own is never taken away.

## Kept off land

A school's fish never ends up as a free fish on land: the schools' fish are never picked up in passing, and any of them found out of the water over dry ground and not on a line, flopping or lying still, is taken away at once. The rod's catch hands a hooked fish over as in the game.

The game writes a fish's next place to swim before it finds the water there too shallow and turns it down, and the fish swims for it until a later choice holds; and a place it takes it checks only there and halfway, so the way can cross a bank. A school's fish and a called one swim only for a place reached through water deep enough every metre of the way; otherwise the place it swam for before stands, so none is steered at a bank either way. One already in shallower water than it swims in may take any way that never gets shallower than where it is, so it swims back out rather than holding on to a place beyond the bank.

## Which game steers a school

A school is steered by the game that spawned it, the game fishing. Should another player's game take a fish over, as happens when the one fishing walks far off, it swims as the game would swim it around the float; any game owning a marked fish whose float is gone takes it away.
