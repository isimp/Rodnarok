# How the game fishes

The game's fishing rules Rodnarök builds on, read from Valheim 1.0.20. Every other note names the rule it changes against these.

Code: the game's `Fish` and `FishingFloat`; Rodnarök's own rules in `src/Model/FishingRules.cs`.

See also: [arrival](arrival.md), [bite](bite.md), [fight](fight.md); all notes in [the index](index.md).

## A fish and a float

A fish picks a new place to swim every 5 to 15 seconds and, each time, takes a float within 50 m with a one in ten chance. It counts as arrived when it is level with the hook on one ground axis and in height, the other ground axis left out, so a float can dip with the fish still metres away. The bite is one downward push on the float, about as big as a wave, and holding the line hooks the fish only within half a second of it. A fish nibbling at the wrong bait never comes back to that float.

Fish flee from the noise a player makes: walking reaches 15 m, running and jumping 30 m, building 50 m, fading by 4 m a second.

Every fish takes exactly one bait. Fish spawn only in their own biome, never inside a player base and never near a player; the Tetra lives only in mountain caves.

## The fight

A hooked fish takes turns struggling and resting. Reeling costs the fish's stamina a second times its quality, far more while it struggles (a resting Perch 3, a struggling Northern Salmon 60), and Fishing skill lowers it to a fifth. While it struggles the line comes in at half speed, and at no stamina the fish is lost.

The line breaks when the fish drags the float 10 m past the reeled length or 30 m from the rod; holding never strains it, so in the game holding is always safe. A struggle lasts half a second up to its fish's longest, 1.5 s more per star, and a rest a second or two up to five.

## Fish in the save

Fish never leave the world by themselves: each is kept in the world's save until caught. The game picks up a fish lying near a player like any dropped item and lets one out of the water be taken by hand.
