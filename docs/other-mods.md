# Rod-only fish and other mods

Why the fish a bait draws can be caught only with the rod, how a projectile mod is kept from catching them, and what other ways of catching fish are and are not covered.

Code: `src/Fishing/RodOnly.cs`, `src/Patches/RodOnlyPatches.cs`, `src/Model/School.cs`.

See also: [arrival](arrival.md), [wary schools](wary.md) (a shot scares a school away), [limits](limits.md); all notes in [the index](index.md).

## Only with the rod

The fish a bait draws can be caught only with the rod (`RodOnly`, `School.Catchable`). The game spends bait only when a fish is hooked and gives it back when the line comes in empty, so without this a bait would bring a school of any fish at no cost for any other way of catching fish to take. Spearfishing (Korppis, `org.bepinex.plugins.spearfishing`) is the case in mind: its prefix on `Projectile.OnHit` turns a fish hit by a spear, harpoon, arrow or bolt into a floating item or puts it straight into the inventory, for any fish.

## How a hit is kept from the fish

Harmony runs every prefix of a method whatever an earlier one returns, so Rodnarök cannot keep another mod's prefix from running; it changes what that prefix sees instead. Its own prefix on `Projectile.OnHit` runs first (`Priority.First`, and before Spearfishing by name): if the hit is a fish a bait drew, read from the fish's saved float mark so every game agrees, then for the rest of that hit the game's `Projectile.FindHitObject` answers nothing for that fish. The game's own hit finds no fish either, which changes nothing in the game, as a projectile does nothing to a fish.

The school is spooked: it takes no bait from then on, and every fish of it not on a line leaves, diving out of sight as a school does once its float is gone. A fish of the world's own is left to the game and to those mods.

## What is covered

This covers every mod that finds what a projectile hit through the game's lookup. A mod that catches fish another way, by its own physics query or by taking fish near a piece, is not covered. OdinArchitect's *Fish Trap* never touches a fish in the water: it is a station turning Fishing Bait into raw fish, and nothing of Rodnarök changes it. The school is only steered away by the game that owns its fish, which is the game fishing; a projectile from another player still catches nothing, but the school swims on in that case.
