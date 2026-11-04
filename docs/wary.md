# Wary schools

What scares a school, how it darts and flinches, when it is too shy to bite, and when it leaves for good and says why.

Code: `src/Model/Wary.cs`, `src/Model/SchoolGone.cs`, `src/Fishing/Schools.cs`.

See also: [arrival](arrival.md), [difficulty](difficulty.md), [limits](limits.md); all notes in [the index](index.md).

## Noise

Rodnarök's fish are not moved by the game's own flight from noise, which would scatter a school the moment a player walks up; the world's own fish still flee from it. Instead a school is wary in a way of its own (`Wary`, setting `Wary`, on by default).

Every frame, in the game that cast the float, the game's own noise rule is asked at the float and at the school's middle, with 35% of the range, as a fish hears under water: a player within that share of their noise (the game's walking 15 m, running and jumping 30 m, building 50 m, fading by 4 m a second, so about 5 m walking and 10.5 m running; sneaking, sitting and standing still make none) startles the school, which darts 6 m off the player at the game's fleeing speed and comes back once all has been quiet for 3 s. The cast's own start is too soft to matter: an attack's 10 m around the one fishing has faded before a school comes near.

## A quick reel

The float moving across the water faster than 0.6 m/s, measured over half seconds, while a fish of the school is within 6 m of it, as a short reel does, only makes the school flinch: a 2.5 m sidestep off the float, back once the float has lain still for 1.5 s. While off the float no fish of it goes for the bait. A fish sidesteps straight away where the water stays deep enough for it, else turned 45° or 90° aside, and waits where it is where none of these is deep enough.

## Leaving for good

Frights add up over a cast, a startle 2 and a flinch 1, and at 8 the school leaves for good, diving out of sight as a school whose float is gone; so does it when the float keeps moving that fast for 2 s with the school near it or already flinched aside, as reeling it in does.

## Shyness

Once a school is ready to bite it is shy as much as the shyer of two measures says: how close the one fishing stands across the ground (shyest within 4 m, not shy from 12 m) and how shallow the water is under the float (shyest under 1 m, not shy from 3 m). As it is first ready it gives up and swims off with a chance of 30% of its shyness, and otherwise hesitates up to 12 s, as long as its shyness, before a fish goes for the bait.

Only the game that cast the float runs this, as it steers the school; another player is heard only by their noise reaching the float. The hearing share, the frights that make a school leave and the chance of giving up are the Normal level's here ([difficulty](difficulty.md)).

## Saying why

A school scared away for good tells the one fishing why in a line at the top left (`SchoolGone`): their steps or another player's, named, when the last fright was heard; reeling through the school when it was a flinch or the float dragged through it; too shy to bite close in or in shallow water, by the shyer of the two measures; or a shot when something other than the rod hit one of its fish. A dart or a flinch it comes back from says nothing.
