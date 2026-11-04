# Settings and the server

Which settings are each player's own, which the server decides for everyone, how they reach every player, and the cast hint.

Code: `src/Core/Settings.cs`, `src/Core/PlayRules.cs`, `src/Model/FishingRules.cs`, `src/Model/CastHint.cs`, `src/ServerSync/ConfigSync.cs`, `src/Commands.cs`.

See also: [difficulty](difficulty.md), [wisp](wisp.md), [sitting](sitting.md), [limits](limits.md); all notes in [the index](index.md).

## Where they are

All settings are in `BepInEx/config/isimp.Rodnarok.cfg`; `/rodnarok` in the chat tells where.

## Each player's own

Three settings in section `Display` are each player's own and never synced:

- `CastHints`, off by default, makes each cast show a line in the game's top-left messages naming the bait's fish the water there fits, those that would need bigger or deeper water, or the fish of the world's own called (`CastHint`). The fish of a bait are rolled only among those the water fits, each as likely, so a bait two fish take never turns a cast away for the one that does not fit.
- `SeatedMotions`, on by default, shows the cast, the pull and the stance on a seated player's bones ([sitting](sitting.md)).
- `FloatWisp`, on by default, has a worn *Wisplight*'s wisp guard the float ([wisp](wisp.md)).

## The server's

The others are in section `Fishing` and are synced to every player by ServerSync (vendored, see `src/ServerSync/README.md`). The server's values win for everyone on it, and the entries are locked, so only an admin changes them in game. In a world without a server that has Rodnarök each player's own file plays.

`Difficulty` picks a level, which fills in the numbers it sets ([difficulty](difficulty.md)). The feature switches (`Schools`, `CallWorldFish`, `BiomeFishOnBait`, `Tension`, `PullBySpeed`, `StruggleWarning`, `Heave`), the waits (`WaitMin`, `WaitMax`), the calm water (`CalmReach`, `CalmKeep`) and `SignContrast` are their own settings at every level. Each entry's description in the config file says what it does.

A player without Rodnarök cannot join a server that has it.
