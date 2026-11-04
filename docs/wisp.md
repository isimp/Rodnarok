# The wisp at the float

How the wisp of a worn *Wisplight* guards its wearer's float, flares at a bite, comes home, and keeps the mist cleared around the wearer.

Code: `src/Model/FloatWisp.cs`, `src/Fishing/FloatWisps.cs`, `src/Fishing/Schools.cs`.

See also: [settings](settings.md); all notes in [the index](index.md).

## Guarding the float

`FloatWisp` in section `Display`, on by default and each player's own, has the wisp of a *Wisplight* the player wears guard their float (`FloatWisp`, `FloatWisps`). The game moves the wisp (`SE_Demister`) only in the wearer's own game, every physics step, toward a place 3.5 m over the wearer, with the *Wisplight*'s acceleration 7, top speed 80 and friction 0.03 a step, and snaps it there from beyond 12 m.

While the wearer's float is out Rodnarök moves it right after the game does, the same way, toward a place by the float: searching, it goes round the float 1.1 to 1.5 m out at 0.7 radians a second, 1.05 to 1.75 m up, and every 4 s dips for 1.1 s to 0.4 m over the water. At a bite within the hook window it is drawn three times as hard to 0.45 m over the float, and its light, which the game flickers over its own intensity, flares to 2.2 times that intensity, fading to 1.3 times over the hook window; hooked, it is drawn twice as hard to 0.8 m over the float at 1.3 times its light.

## Coming home

Once the float is gone, or the school has left it with nothing hooked (scared away, too shy, driven off, done with a bare bait or shot at; `Schools.LeftFloat`), it is drawn back half again as hard toward its place over the wearer and handed to the game's own following, its speed with it, within 4.5 m, the *Wisplight*'s own 6 m leash less the 1.5 m its place drifts by, so it never snaps across or is dragged.

## Who sees it

The wisp is a networked object whose place every game is sent, so every player sees it move; the flare is the wearer's alone, since only the wearer's game learns of a bite.

## The mist

The wisp's mist clearing (`Demister`, a force field the game's mist leaves clear) moves with it, so while it guards a float its networked data names the wearer, and every game keeps a copy of the clearing 3.5 m over the wearer, as wide as the wisp's own, and narrows the wisp's own to 6 m, as wide as the game's clearing where wisps gather; once the mark is gone, or the wisp or the wearer, the copy goes and the clearing is the game's own again.
