# Calm water

How a Serpent trophy on the game's *Item stand* calms the waves around it, how the calm is switched and kept, and how the waves are lowered without a seam.

Code: `src/Model/CalmStand.cs`, `src/Model/CalmWater.cs`, `src/Calm/CalmStands.cs`, `src/Calm/CalmWaters.cs`, `src/Patches/CalmPatches.cs`.

See also: [signs](signs.md) (calm water reads clearest), [settings](settings.md); all notes in [the index](index.md).

## The stand

The water is calmed by the game's own *Item stand*, wall or horizontal, holding a Serpent trophy (`CalmStand`, `CalmStands`); Rodnarök adds no piece of its own. Put up, the trophy calms nothing. A tap of the use key on that stand switches its calm on or off: the game gives the plain tap on a stand holding an item no work, while holding the key still takes the trophy and the turn key still turns it.

The switch is kept in the stand's own saved data under a key of Rodnarök's, flipped by the stand's owner at a player's call, so every player sees the same and it lasts; the stand's ward is asked first, as the game asks it, and a boss's stone, whose trophy cannot be taken, is left alone. Taking the trophy off switches the calm off, so the next trophy put up starts off.

The stand's hover text says whether it calms the waters and offers the switch, the key named as the game names it; the words are plain text, since the game takes new words only through a private table it clears on a language change. Every game looks over its loaded stands twice a second, so a stand on a ship takes the calm along with that lag.

## The waves

A calming stand lowers the waves through the corner values the game keeps for each 64 m water tile. The game shapes the waves of what floats and draws the water from the same four values, so both stay calm together, and a corner is lowered by where it is alone, so two tiles meeting at it always agree and no seam shows (`CalmWater`). The calmed area follows the tiles' corners and fades across a tile.

A place lies at most 45 m from its tile's nearest corner, at the tile's middle, so the default reach of 48 m (`CalmReach`) calms at least one corner wherever a stand stands; near a tile's middle that corner's share of the waves there is small. `CalmKeep` (0.1 by default) is the share of the waves a calmed corner keeps.
