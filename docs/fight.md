# The fight

The crosshair ring through a fight, a struggling fish at the surface, the stalled line, line tension and each fish's pull, the warning of a struggle, the heave, and what is written to the log.

Code: `src/Model/Fight.cs`, `src/Model/Tension.cs`, `src/Model/Ring.cs`, `src/Model/RingDraw.cs`, `src/Model/StruggleWarning.cs`, `src/Model/Heave.cs`, `src/Fishing/Lines.cs`, `src/Fishing/Thrash.cs`, `src/Fishing/CrosshairRing.cs`, `src/Fishing/StruggleTells.cs`, `src/Fishing/Heaves.cs`, `src/Patches/HudPatches.cs`, `src/Patches/FishPatches.cs`.

See also: [how the game fishes](game-rules.md), [the bite](bite.md), [difficulty](difficulty.md), [limits](limits.md); all notes in [the index](index.md).

## The crosshair ring

The crosshair ring (`Ring`, `RingDraw`, `CrosshairRing`) is one ring of one size from the bite to the end of the fight, made from the game's bow ring as a ring of its own, so it never looks like drawing the rod. While a hooked fish struggles the ring is red; while it rests, yellow. With tension its arc is the line's strain, filling clockwise from the top over a faint full track of the same colour (a second ring made the same way); without, the whole ring shows. While a bow or the rod is drawn it is hidden and the game's bow ring shows as ever.

## Struggling and resting

Holding the line while the fish rests reels it in at the game's speed and cost. While it struggles the line stalls: no line is gained, and it costs the `StallStamina` setting a second, the same for every fish, lowered by Fishing skill as reeling is (`Fight`).

A struggling fish shows it at the surface (`Thrash`): the game wiggles it and now and then splashes with it, mostly unseen under the water; Rodnarök brings it up until its back breaks the surface and draws the game's splash for it about three times a second, on every player's screen, and stops when it rests; a hooked fish is held near the float in both. The game saves a struggle's length as a decimal number but its end as a whole 0, which it keeps apart, so every game, the game's own splashes included, went on reading a struggle after it ended; the game owning the fish now saves the end as a decimal too.

## Line tension

Line tension is a rule of Rodnarök's own, since the game's line never strains (`Tension`, `Lines`, setting `Tension`, on by default). Holding the line through a struggle strains it, from none to snapped over `SnapSeconds` (1 s); a line strained to snapping breaks as the game breaks one, with the game's message and effect. Easing off lets the strain go over `EaseSeconds` (1 s), and while the fish rests it eases whether the line is held or not, so a rest never strains it.

While the line is eased off in a struggle the fish takes line out at `RunSpeed` (2 m/s) less the strain still on the line (half as fast at half strain, none at snapping), so easing off in short taps that keep the arc partly filled loses less line than letting go. It heads away from the one fishing, swimming as fast as the line goes out, so it never drags the float beyond the line to where the game would break it; past the game's 30 m the line breaks as ever. Once the run ends, the game that owns the fish gives it back its own speed.

Only a short struggle can be held through; most, and every one of a fish with stars, need the line eased off once or more. The strain is kept by the game that cast the float, which runs the fight.

## Each fish's pull

`SnapSeconds` and `RunSpeed` are a Perch's (speed 3 in its prefab): with `PullBySpeed` (on by default) every other fish pulls by the fourth root of its prefab's speed against the Perch's, snapping a held line sooner and taking more line the faster it is. A Pike (5) snaps it after 0.88 s and runs 2.3 m/s, a Tuna (10) after 0.74 s and 2.7 m/s, an Anglerfish or Tetra (2) after 1.1 s and 1.8 m/s; the five fish of speed 3 pull as a Perch. A fish of a mod whose prefab gives no speed pulls as a Perch.

The fourth root keeps the game's order of fish with small steps between them, so a beginner can still land each: at the full speed a Tuna would take 10 m a second, more in one struggle than a Fishing 0 reel wins back in a rest, and even the square root left a one-star Tuna at Fishing 0 needing about half again the stamina of a fight without the pull. The game's own struggle and rest times and stamina costs stay as they are, so how tiring a fish is still follows the game.

## The struggle warning

A hooked fish shows its next struggle coming (`StruggleWarning`, `StruggleTells`, setting `StruggleWarning`, on by default). The game decides when the next struggle starts as one ends (`Fish.m_nextEscape`, a rest of the fish's `m_escapeWaitMin` to `m_escapeWaitMax`, brought sooner by each heave), on the game owning the fish; that game writes the time, again whenever it changes, onto the fish's networked data as server time (`Rodnarok.struggleAt`), and the game owning the float writes the line's strain in steps of 0.05 (`Rodnarok.strain`), so every game shows the same.

With more than 1.5 s to go the float lies still, so a still float always leaves that long. Within the last 1.5 s the float's look trembles toward the fish as at a bite (`FloatTremble`, its closeness standing for how restless the fish is, eased in from the first moment), never the float with its physics body.

In the last 0.6 s before a Perch's struggle the rod line is drawn taut, straight from the rod to the float over the game's sag (`LineConnect` draws the sag the float sets each step), and quivers a little; a faster fish gives less of that moment by its pull (`Tension.Pull`, a Tuna 0.45 s, an Anglerfish 0.65 s) and each star takes a tenth off, never under 0.25 s. The line never changes at once: it draws from the game's sag to taut over 0.3 s, eased at both ends, sags back to the game's own over 0.6 s once the struggle is over, and its quiver swells and settles over 0.3 s.

Through the struggle the line stays taut and quivers by the strain, from a fifth of its swing with none to its whole swing at snapping: the middle of the line swings up to 0.25 m aside and half that up and down, nine times a second, both ends held. A struggle starting at the hook comes at once, so there is nothing to warn of. Switched off, nothing shows and the float stands upright.

## The heave

The rod's secondary attack heaves a hooked fish (`Heave`, `Heaves`, setting `Heave`, on by default). A rod is drawn as a bow, so the game reads the secondary attack (`SecondaryAttack` or `JoySecondaryAttack`) and passes the press to `Player.SetControls` once per press but does nothing else with it; Rodnarök takes the press there.

In a rest a heave lifts the fish and its float 3 m toward the rod tip over 0.3 s (10 m/s, beyond the game's best reel of 6 m/s, so a heave and its wind gain line faster than reeling at any Fishing skill), for the rod's own secondary attack stamina, which the game's rod carries though it has no secondary attack (`m_secondaryAttack.m_attackStamina`, 20, as an iron sword's or a mace's secondary), charged as the game charges any attack (`Attack.GetAttackStamina`): raised by the gear's attack stamina modifier and the status effects, then lowered by a third at full Fishing skill, 20 at Fishing 0 and 13.4 at 100. A modded rod's own figure counts.

The game that cast the float runs the lift, moving the fish and the float together and stopping both where the lift ends so neither coasts on, and keeps the line's length, so the line is longer than the way to the float and the game draws it sagging by its own rule. While the line is more than 0.3 m longer than that way, holding the line winds it in three times as fast as the game's reel, never past the float, and the resting fish's weight costs no stamina, only the game's hooked drain.

A heave is pumping, not a button to hold down. While the line hangs slack from the last heave a press lifts nothing and costs nothing, so each heave must be wound in before the next. And the fish answers each heave: the game owning it moves its next struggle (`Fish.m_nextEscape`, the end of a rest of the fish's `m_escapeWaitMin` to `m_escapeWaitMax`) 2 s sooner, but never before the lift ends nor later than it would have come. The struggle warning reads that time, so the float grows restless and the line goes taut sooner after each heave. A rest of the game's longest, 5 s, takes two heaves before the next would bring the fish bolting as its lift ends; a rest of the shortest, 1.25 s, none.

A struggle starting with slack out jolts the line: 0.35 strain for every metre of slack beyond 0.3 m, so a whole heave's 3 m left unwound nearly snaps an unstrained line by itself and snaps any line already strained. A heave too late in a rest has the fish bolt as its lift ends, with the whole slack out. A heave into a struggle lifts nothing and adds a quarter of the way to snapping. Without tension a heave into a struggle does nothing. The heave relies on the game that cast the float owning the fish, as the line's strain and a fish's run do.

The rod shows the heave: with a rod the game's block pose is the raised reeling pose, drawn from an animation flag (`blocking`) that `Humanoid.UpdateBlock` sets only when the blocking state changes and every game sees. Through a heave Rodnarök sets that flag after the game's update: the rod snaps up for the 0.3 s lift and comes down after, or, held up already, dips for 0.15 s and comes back up; then the flag is the game's own again. A seated player's rod moves by Rodnarök's seated pose instead and shows no heave.

## The log

Every line that breaks in the game that cast it is written to the log, saying whether the strain snapped it or the game's own rule broke it, with the line's length, the float's distance from the rod tip, the tip's height above the sea, the fish's distance from the float and whether it struggled, the strain, the hold, the stamina and whether the one fishing sat.

Each struggle writes one log line as it ends (the fish rests, the line snaps or the line is gone): how much line the fish took in how many runs, how long the line was eased off and held, and the line's length; a run with the block button down that the game did not take as blocking (with what it was doing: attacking, dodging, building, overweight, a minor action or staggered) or with stamina out is counted apart.
