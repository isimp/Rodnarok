# Fishing seated

How a player fishes from a chair, a bench or the ground and stays seated, how a seated cast follows the aim, and how the seated body and the rod's motions are shown.

Code: `src/Fishing/SeatedFishing.cs`, `src/Fishing/SeatedBodies.cs`, `src/Fishing/SeatedRodPose.cs`, `src/Model/SeatedAim.cs`, `src/Model/SeatedBody.cs`, `src/Model/SeatedPose.cs`, `src/Model/SeatedBlend.cs`, `src/Model/ClipFrames.cs`, `src/Core/AfterAnimation.cs`.

See also: [the fight](fight.md) (a seated heave), [settings](settings.md); all notes in [the index](index.md).

## Staying seated

The game stands a seated player up on any attack or block press (`Player.SetControls`), cancels a drawn cast while the player sits on a seat (`Player.UpdateAttackBowDraw`), and lets nobody start an attack in the sitting pose (`Character.CanMove`). A player holding a rod (anything casting fishing bait) who sits on a chair, a bench or a boat's bench, or on the ground with the sit emote, and holds still, is seen as not seated by those three for the moment they ask (`SeatedFishing`), so the rod casts and reels and the player stays seated. Moving, jumping or crouching stands the player up as in the game. A ship's controls are not a seat for this.

## The seated aim

The game throws the rod's cast along the way the body faces, not the aim (the rod's attack has `m_useCharacterFacing` on, its upward aim off), and turns the body toward the aim while drawing; a seat holds the body still, so a seated cast went straight ahead. A seated player's cast and the point it leaves the rod are turned round the character toward the aim (`SeatedAim`, `SeatedThrow`, on `Attack.GetProjectileSpawnPoint`): on the ground all the way, on a chair, a bench or a boat's bench up to 60 degrees to either side of the seat's facing, an aim further round casting at that edge. Sitting on the ground, drawing also turns the body toward the aim at the game's own turning speed (`m_turnSpeed`, `SeatedTurn`), so the throw is seen going that way; a seat keeps its sitter's facing. The cast is made in the game of the one casting and its float is seen by every game.

## The seated body

The game plays its throw and its pull on the same base animation layer as the sitting pose, standing the whole body up for them, and its sitting down again after; the throw cannot be left out, since the cast leaves the rod at an event inside its animation. So every seated player's hips, spine and legs are kept as the seat has them (`SeatedBody`, `SeatedBodies`), whatever `SeatedMotions` says.

A player sits for every game alike: the game turns on an animator switch for the seat, one named `attach_…` for a chair, a bench or a boat's bench and `emote_sit` on the ground, and hands it to every other game. A player whose emote is the sit, which every game reads from the player's saved data, sits too, whatever the switch shows. While a player sits, those bones are kept whenever the base layer shows a sitting pose settled, 1 s or more into the sitting state with nothing crossing over, and put back right after the game's animation whenever it shows anything else, the crossing back to the seat included, or a sitting state younger than that, as when the game sits a player on the ground down again inside the same state. The switch blinking off for less than half a second keeps the pose. The arms are left to the game's throw, or to the fishing motions when they are on.

## The rod's motions

The game plays the cast (`Throw`), the pull (`ReelIn`) and the stance of holding the rod (`Idle_Fishing`) on the same base animation layer as every sitting pose (`SitChair`, `Emote_sit`, `SitShip`, read from the player's animator), and a layer plays one state at a time, so a seated cast or reel showed nothing. With `SeatedMotions` on, Rodnarök shows them on the bones instead (`SeatedRodPose`).

The three clips are read once per rig as still frames, at least 30 a second (`ClipFrames`): a copy of the animator's skeleton is made inside an inactive holder, so none of its scripts wake, stripped of everything but its bones and its animator, posed frame by frame by a Playables graph of its own and thrown away.

Then, right after the game's animators have posed the characters, before any script of the frame reads a bone (`AfterAnimation`, placed into Unity's own loop after the physics step's animation and after the frame's), only the shoulder, arm, hand, finger and head bones are turned toward those frames; the hips, the spine and the legs keep the seat's pose, kept as above. The shoulders (or the upper arms on a rig without them) and the head are held as the clip has them toward the whole character, and the bones below them as the clip has them toward the bone they hang from: the seated chest leans otherwise than the standing one the clips were made on, so held toward the whole character the arms carry the rod as they do standing.

Turning them there, rather than in a late script, keeps the line drawn from the rod tip and the cape from flickering: the player's animator may pose the character in the physics step, which some frames have and some do not, so scripts reading the bones before a late script would see the game's pose on some frames and the turned one on others. A bone the animator has not posed again since the last turn keeps it, so nothing is turned twice.

The throw's wind-up is played from the start of the throw and held while drawing, the throw goes on from there as the cast goes out, the pull plays while reeling and the stance while the float is out (`SeatedPoses`). One motion crosses over into the next in a quarter second, and the motions as a whole fade in and out as long, the last one playing on as they fade (`SeatedBlend`); standing up lets go at once, leaving the game's own way up.

Every game does this for every seated player it sees, reading the sitting from the game's seat switch, the rod from what is seen in their hand, the reel from their block and the float from its owner; the draw and the throw are known only to the player's own game, so others see the stance and the pull. `SeatedMotions` in section `Display`, each player's own and on by default, turns it off; the self-test switches it on for its sitting scenario and puts it back after.
