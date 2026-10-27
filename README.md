# Rodnarök

Rodnarök makes fishing in Valheim something you can read and enjoy. The bait on your hook decides which fish comes, in any water deep enough, so every fish of the game can be caught at your own dock: the bait recipes and their trophies are what opens the later fish, not a trip to their biome.

![The crosshair ring through a fight: white as it drains after the bite, red filling as a held struggle strains the line, red emptying as the line is eased off, yellow while the resting fish is reeled in](https://raw.githubusercontent.com/isimp/Rodnarok/main/docs/images/fight.webp)

## AI notice

Most of Rodnarök was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Using it

A fish of the bait's kind shows up a short while after the float lands and swims to it where you can watch it come. It bites only when it is really at the float, the float is pulled under with a splash, and the crosshair ring closes over the time you have to hook it by holding the line. In the fight the fish thrashes at the surface while it struggles and calms when it rests, and the ring turns red or yellow to show which. Hold the line while it rests to reel it in. Holding through a struggle gains nothing and strains the line, and the ring fills as it does: ease off before it closes or the line snaps, and the fish takes some line back while you do.

You can cast and reel while sitting on a chair, a bench or a boat's bench. The *Serpent post*, built with the hammer, calms the waves around it.

## Multiplayer

Install Rodnarök on every player and on a dedicated server. The server's settings apply to everyone on it, and only an admin changes them in game.

## Settings

All settings are in `BepInEx/config/isimp.Rodnarok.cfg`, and `/rodnarok` in the chat tells where.

## Other mods

The fish a bait brings can only be caught with the rod. This is a deliberate choice and changes how Spearfishing works with Rodnarök: a spear, harpoon, arrow or bolt that hits one of these fish catches nothing and scares the whole school away. Without it every cast would bring a free school of fish to shoot, for one bait you get back. Fish of the world's own are left to Spearfishing as before.

## Thanks

Several ideas come from ChillFishing by Lenson: above all calming the water where you fish, and also fishing while seated and fish that come to you rather than you travelling to them.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rodnarok
