# Rodnarök

Rodnarök makes fishing in Valheim something you can read and enjoy. The bait on your hook decides which fish comes, in any water deep enough, so every fish of the game can be caught at your own dock: the bait recipes and their trophies are what opens the later fish, not a trip to their biome. Plain *Fishing Bait* also brings the fish that live where the float lands, so in the Black Forest it brings a *Trollfish* as often as a *Perch* or a *Pike*, and at sea a *Tuna*, a *Coral cod* or a *Pufferfish*; the server setting `BiomeFishOnBait` turns that off.

![The crosshair ring through a fight: white as it drains after the bite, red filling as a held struggle strains the line, red emptying as the line is eased off, yellow while the resting fish is reeled in](https://raw.githubusercontent.com/isimp/Rodnarok/main/docs/images/fight.webp)

## AI notice

Most of Rodnarök was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

A school of the bait's fish sets off from out of sight beyond the float as it lands and swims in, so you watch it come rather than see it appear, and each fish tells itself on the way by a sign of its own on the water: a *Perch* small rings, a *Pike* one swirl by the float, a *Coral cod* a fizz of bubbles, an *Anglerfish* a lure glowing under the water, a *Tuna* a boil of splashes, and so on, more often as it nears. Only a *Tuna* or a *Northern salmon* leaps on its way, and mostly at dawn and dusk. If a fish of the world's own that takes your bait swims nearby, that one is called to the float instead, and nothing new is spawned. A school only comes to water big enough for it to swim in and out again; in a small pond a line on the screen says so, and the world's own fish still bite as in the game. The fish are wary: walking or running close to them makes them dart a few metres off, and a short reel makes them flinch aside; either way they come back once all is quiet. Only many frights, or reeling the float through them, drives them off. Sneak, sit or stand still while they come. A float dropped close to your feet or in shallow water makes them shy, slow to bite and often gone without biting, so cast out over deep water. When a school is scared away for good, a line at the top left tells you why.

A fish bites only when it is really at the float, the float is pulled under with a splash, and the crosshair ring closes over the time you have to hook it by holding the line. In the fight the fish thrashes at the surface while it struggles and calms when it rests, and the ring turns red or yellow to show which. Hold the line while it rests to reel it in. Miss a bite and the fish nibbles at your bait, and each miss is likelier to take it; a fish that missed won't bite again, and once the whole school has, it swims off. Then reel in and cast again. Holding through a struggle gains nothing and strains the line, and the ring fills as it does: ease off before it closes or the line snaps, and the fish takes some line back while you do, less the more strain the line still holds, so short taps keep it closer than letting go. Each fish pulls by how fast it swims in the game: a Pike or a Tuna strains the line sooner and takes more line, an Anglerfish less.

You can cast and reel while sitting on a chair, a bench or a boat's bench, or on the ground, and the cast goes where you aim: on the ground you turn toward it, on a seat it reaches 60 degrees to either side. A *Serpent trophy* put up on an *Item stand* calms the waves around it once you tap the use key on that stand; tap it again, or take the trophy off, to let the waters be.

## Multiplayer

Install Rodnarök on every player and on a dedicated server. The server's settings apply to everyone on it, and only an admin changes them in game.

## Settings

All settings are in `BepInEx/config/isimp.Rodnarok.cfg`, and `/rodnarok` in the chat tells where. `Difficulty` picks how hard the fishing is, in the five steps of the game's own combat setting from `VeryEasy` to `VeryHard`, `Normal` by default: a level sets how long you have to hook, how soon a held line snaps, how much stamina reeling costs, how wary the fish are and how fast missed bites take the bait, and picking one fills in those settings so you can see them. Change any of them yourself and `Difficulty` becomes `Custom`, which plays them as you set them. On a server its setting applies to everyone. `CastHints`, your own setting and off at first, makes each cast say at the top left which of the bait's fish can come to the water there and which would need bigger or deeper water. Seated players with a rod show the cast, the pull and the rod held out on their arms while they stay seated; `SeatedMotions`, also your own, turns that off. Wearing a *Wisplight*, its wisp guards your float: it drifts round the float searching while you wait, dives in close and flares when a fish bites, follows the fight and comes back to you after, while your mist clearing stays with you and the wisp clears a small pocket round the float; `FloatWisp`, your own as well, turns that off.

## Other mods

The fish a bait brings can only be caught with the rod. This is a deliberate choice and changes how Spearfishing works with Rodnarök: a spear, harpoon, arrow or bolt that hits one of these fish catches nothing and scares the whole school away. Without it every cast would bring a free school of fish to shoot, for one bait you get back. Fish of the world's own are left to Spearfishing as before.

## Thanks

Several ideas come from ChillFishing by Lenson: above all calming the water where you fish, and also fishing while seated and fish that come to you rather than you travelling to them.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rodnarok
