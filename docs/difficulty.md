# Difficulty

The five levels and `Custom`: what each sets, how a level fills in its entries, and how a hand edit switches to `Custom`.

Code: `src/Model/Difficulty.cs`, `src/Model/FishingRules.cs`, `src/Core/Settings.cs`.

See also: [the bite](bite.md), [the fight](fight.md), [wary schools](wary.md), [settings](settings.md); all notes in [the index](index.md).

## The levels

The server setting `Difficulty` picks one of five levels, in the steps of the game's own combat setting (`VeryEasy`, `Easy`, `Normal`, `Hard`, `VeryHard`), or `Custom` (`DifficultyLevels`). Normal, the default, is Rodnarök as it played before levels.

| Very easy to Very hard | Numbers |
|---|---|
| Hook window | 2.5, 2, 1.5, 1 and 0.5 s, the last the game's own |
| A Perch snapping a held line | after 2, 1.5, 1, 0.75 and 0.5 s |
| A strained line unstraining | over 0.5, 0.75, 1, 1.25 and 1.5 s |
| An eased Perch taking line | 1, 1.5, 2, 2.5 and 3 m a second |
| Holding through a struggle | 1, 1.5, 2, 3 and 4 stamina a second |
| Reeling in a resting fish | 50% and 75% of the game's own stamina on Very easy and Easy, the game's own from Normal on |
| A wary school hears | not wary on Very easy, then 25%, 35%, 50% and 100% of the game's noise range, the last as the world's own fish do |
| Frights that make it leave | 12, 8, 6 and 4 from Easy |
| The shyest schools giving up | up to 15%, 30%, 45% and 60% from Easy |
| The first four misses take the bait | none on Very easy, then 5/20/60/90%, 10/40/90/98%, 20/60/95/100% and 40/90/100% |
| Bumps before the bite | on Hard and Very hard |

Reeling stamina answers the game's `Fish.GetStaminaUse` with the fish's `m_staminaUse` by that share, and the game's own drain of 1 stamina a second for any hooked fish, 0.2 at Fishing 100, held or not, goes by the same share (the float's `m_hookedStaminaPerSec` and `m_hookedStaminaPerSecMaxSkill`, set from the float prefab's own figures before each step).

## A level fills its entries

Picking a level writes its numbers into the entries it sets, `HookWindow`, `SnapSeconds`, `EaseSeconds`, `RunSpeed`, `StallStamina`, `ReelStamina`, `Wary`, `WaryHearing`, `WaryFrightsToLeave`, `WaryGivingUp`, `BaitLost`, `BaitLossChances` and `Bumps`, so a config panel shows what is played, and the config file is saved once after. Changing any of them by hand to other than the level's number switches `Difficulty` to `Custom`, which plays the entries as set, the level's other numbers kept as they stood (`DifficultyLevels.Matches`).

On every start a level other than `Custom` writes its numbers again, so entries edited in the file while the level stays named are written over; a file meant to be played as edited names `Custom`. Values arriving from a server are taken as they come, since the server filled them by its own level; a player who is not an admin cannot change them there, as ServerSync locks them. The feature switches, the waits, the calm water and the sign contrast are their own settings at every level.

## The self-test

The self-test runs each scenario on Normal's numbers, whatever level the world plays at, except the two that play the levels: one sets each level and reads back the rules, the other fishes a Perch on Normal and Very easy for the reeling stamina and hooks bites past and within the window on Very hard and Very easy. Both need a world of the player's own, since a server's level wins.
