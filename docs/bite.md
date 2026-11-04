# The bite

Which fish may take the bait, how the picked fish waits and bumps, when a bite counts, the hook window, what a missed bite costs, and the crosshair ring before the bite.

Code: `src/Model/Arrival.cs`, `src/Model/Bite.cs`, `src/Model/BiteApproach.cs`, `src/Model/Bumps.cs`, `src/Model/BaitLoss.cs`, `src/Model/ApproachRing.cs`, `src/Fishing/Bites.cs`, `src/Fishing/Schools.cs`, `src/Patches/BitePatches.cs`, `src/Patches/FishPatches.cs`.

See also: [how the game fishes](game-rules.md), [arrival](arrival.md), [the fight](fight.md), [signs](signs.md); all notes in [the index](index.md).

## Only the fish the sign follows

The fish a sign follows is the fish that touches the bait. From the cast until a fish is hooked the caster's game marks its float as claimed (`Rodnarok.claimed` on its networked data), and the world's other fish, in every game, leave a claimed float's bait alone (`Fish.FindFloat` gives them none), so no unannounced fish bites while a sign shows another coming.

## The picked fish

Until the fish picked to take the bait may go for the hook it hovers within 1.5 m of the float, where the quiet sign's slow showing comes ([signs](signs.md)), slowing near it as it will for the bite. It is marked as going for the bait (`Rodnarok.going`), and the sign follows it in, only once it may bite.

With `Bumps` on (off by default, on at Hard and Very hard), the picked fish bumps the float once or twice before it goes for the hook (`Bumps`): a pull of 12 m/s² for 0.25 s against the bite's 14 m/s² for a second, none sooner than 0.4 s after it is picked or later than 0.6 s before it goes, two at least 0.5 s apart. A bump comes only while the picked fish is within 2 m of the float, and one still due in the last 0.6 s before it may bite is dropped. A bump is no nibble the game knows of, so reeling at it hooks nothing, and it drags the float, which startles a wary school.

## Within reach

A bite needs the fish within reach of the hook through all three axes (`Bite`): its mouth within 0.6 m of the point a metre under the float where the game sends a fish going for it. A bite from further off is held back, and the fish tries again as it swims closer.

The game counts a fish at that point by two of its three axes and steers it at its own speed and turning rate, so a fast fish circles the point: a Pike, 5 m/s turning 100 degrees a second, 2.9 m wide, and a Tuna, 10 m/s turning 80, 7 m. So within 3 m of the point a fish going for the bait, of a school or of the world's own called, swims no faster than lets it turn inside half its distance to the point, and at the point inside half the reach: its turning rate in radians a second times half that distance, for most fish easing from about 2.6 m/s at 3 m to 0.5 m/s at the point, the last 3 m in about 2 s (`BiteApproach`); at its own speed again once it bites, is hooked, misses or leaves.

A fish that missed is given the game's own mark of a bait it failed at (`m_failedBait`), so lingering slowly by the hook it does not nibble that float again.

## The hook window

A bite pulls the float under for about a second, beyond the one short push the game gives it. The hook window is a setting, 1.5 s by default. The game's float takes a new nibble a second after the last (`FishingFloat.RPC_Nibble`), inside that longer window, which would dip the float a second time and start the window over; so while the last bite can still be hooked the float takes no other, from that fish or any. These apply to every fish at a float, a school's or the world's own.

After a bite the crosshair ring's arc is full and white and drains clockwise toward the top, turning yellow, as the hook window runs out ([the fight](fight.md) has the ring as a whole).

## Missed bites

Missed bites wear the bait down (`BaitLoss`, setting `BaitLost`, on by default). The game lets a fish bite the same bait again a second after a missed bite, for ever, and gives the bait back when the empty float is reeled in. In the game that owns the float, a bite neither hooked nor any longer within the hook window is a miss, and the fish that bit can no longer be hooked.

Each miss on a float may take the bait, more likely each time: on Normal 10% for the first, 40% for the second, 90% for the third and 98% for every later one. Taken, the float is bare, a bite on it is refused, the game counts its bait as used so reeling it in gives none back, and its school, or the fish of the world's own called to it, moves on.

A fish that missed has learned: its bites on that float are refused and the school sends another of its fish; once every fish of the school has missed, the school swims off and the bait is left, so reeling in gives it back; a fish of the world's own called to the float is the game's again after its one miss. A line at the top left says each time whether the fish only nibbled at the bait, nibbled while the school swam off, or took it. A bump is no bite and costs nothing.

## The ring before the bite

While a school is at the float and no fish bites yet, the crosshair ring tells how close the fish is (`ApproachRing`), from the moment the school comes until the bite or until it leaves: at the bite's size, white, over a faint track, its strength by that fish's distance from the float across the water, as the player sees it rather than by its depth: whole within 0.6 m, the reach a fish bites from, 15% at 1.5 m, the picked fish's hover range, and beyond, eased between by distance. The fish it follows is the one picked to bite while it hovers, the nearest of a wary school while it hesitates, or else the nearest of the school. The secondary attack does nothing before the hook.
