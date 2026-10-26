# Rodnarök technical notes

## How the game fishes

These are the game's rules Rodnarök builds on, read from Valheim 1.0.20.

A fish picks a new place to swim every 5 to 15 seconds and, each time, takes a float within 50 m with a one in ten chance. It counts as arrived when it is level with the hook on one ground axis and in height, the other ground axis left out, so a float can dip with the fish still metres away. The bite is one downward push on the float, about as big as a wave, and holding the line hooks the fish only within half a second of it. A fish nibbling at the wrong bait never comes back to that float. Fish flee from the noise a player makes: walking reaches 15 m, running and jumping 30 m, building 50 m, fading by 4 m a second.

A hooked fish takes turns struggling and resting. Reeling costs the fish's stamina a second times its quality, far more while it struggles (a resting Perch 3, a struggling Northern Salmon 60), and Fishing skill lowers it to a fifth. While it struggles the line comes in at half speed, and at no stamina the fish is lost. The line breaks when the fish drags the float 10 m past the reeled length.

Every fish takes exactly one bait. Fish spawn only in their own biome, never inside a player base and never near a player; the Tetra lives only in mountain caves.

## What Rodnarök changes

The bait table is built at runtime from the baits the game's own fish take (`BaitTable`), so a fish another mod adds with a bait of its own comes too. A bait two fish take brings each in equal share.

A while after the float lands, the bait draws a school of its fish (`School`): as many as the world spawns of that fish together (a Perch comes in twos to sixes, a Northern Salmon in threes to tens), each size as likely, and two to four of a fish the world never spawns in groups, such as the cave fish. Each fish rolls its stars as the world's spawns do, and the game draws a fish 40% larger for each star. They leap on their way in, circle the float near the top of the depth they swim at, and one of them, each as likely, takes the bait, so the catch keeps the game's odds of stars. When it is hooked the others scatter. With `Schools` off a single fish comes; with `Bumps` on the fish that takes the bait bumps the float lightly first, which can never be hooked.

Fish never leave the world by themselves in the game: each is kept in the world's save until caught. Rodnarök marks its own fish, takes them away 10 seconds after their float is gone, and any player's game takes away a marked fish it finds with no float, so none gather in the save. A fish of the world's own is never taken away. Rodnarök's fish pay no attention to a player's noise; the world's own fish still flee from it.

A bite needs the fish within reach of the hook through all three axes (`Bite`). The hook window is a setting, 1.5 s by default. The crosshair ring (`Ring`) closes over the window and shows a struggle apart from a rest in the fight.

Holding the line while the fish rests reels it in at the game's speed and cost. While it struggles the line stalls: no line is gained, and it costs the `StallStamina` setting a second, the same for every fish, lowered by Fishing skill as reeling is (`Fight`).

The calm-water piece lowers the waves through the corner values the game keeps for each 64 m water tile. The game shapes the waves of what floats and draws the water from the same four values, so both stay calm together, and a corner is lowered by where it is alone, so two tiles meeting at it always agree and no seam shows (`CalmWater`). The calmed area follows the tiles' corners and fades across a tile.

## Limits

A fish needs water as deep as its own swimming depth: the Tuna swims no shallower than 4 m. The Frosty bait is made from a Drake trophy, so the Northern Salmon is open from the Mountain on. Fish that come to a float belong to the game of the player fishing, and others see them as ordinary fish.

## Settings

All settings are in `BepInEx/config/isimp.Rodnarok.cfg`, section `Fishing`, and are synced to every player by ServerSync (vendored, see `src/ServerSync/README.md`). The server's values win for everyone on it, and the entries are locked, so only an admin changes them in game. A player without Rodnarök cannot join a server that has it.

## Building and testing

`dotnet build` builds against the installed game and deploys into the Gale profile; `dotnet build -c Release -p:LibsDir=lib` builds against the metadata-only copies in `lib/`, as CI does. `dotnet test tests/Rodnarok.Tests` runs the tests of everything that needs no game, and the standards tests that read the source.

The in-game self-test is a plugin of its own (`selftest/`, `Rodnarok.SelfTest.dll`), built and deployed with `dotnet build selftest -c Debug` and never packaged. In a world, `/rodnarok selftest` runs it and writes `BepInEx/Rodnarok-selftest.log`; words after it pick the scenarios whose names hold them, `list` names them all and `stop` ends a run. With the file `BepInEx/Rodnarok-selftest.run` present it runs by itself a few seconds after a character enters a world, with the words the file holds.
