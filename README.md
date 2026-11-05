# Rodnarök

Rodnarök makes fishing in Valheim something you can read and enjoy. The bait on your hook decides which fish comes, in any water deep enough.

![The crosshair ring through a fight: white as it drains after the bite, red filling as a held struggle strains the line, red emptying as the line is eased off, yellow while the resting fish is reeled in](https://raw.githubusercontent.com/isimp/Rodnarok/main/docs/images/fight.webp)

## AI notice

Most of Rodnarök was written by Claude Code (Anthropic), which did the heavy lifting on implementation and design. Heads-up so you can judge for yourself.

## Fishing

Each bait brings its own fish anywhere, and plain *Fishing Bait* also brings the fish that live where the float lands. Cast out over deep water and keep still. A school swims in from out of sight, each kind showing its own sign on the water, brighter on calm water and fainter in a swell. The signs go quiet while the school waits at your float, then follow the one fish that goes for the bait, and the crosshair ring grows stronger as it comes closer.

Noise and quick reels scare the fish, and a line at the top left says why a school left.

## The fight

When a fish bites, the float goes under and the crosshair ring drains. Hold block before it empties to hook the fish. A missed bite may cost your bait.

A hooked fish takes turns struggling and resting. While it struggles the ring is red, and holding block strains the line until it snaps, so ease off and let the fish run. While it rests the ring is yellow, and holding block reels it in. A still float means the rest goes on a while yet, and a restless float and a line drawn taut mean the fish is about to bolt.

In a rest your secondary attack heaves the fish toward you and leaves slack on the line, which holding block winds in quickly. Slack still out when the fish bolts jolts the line, so wind it in before the next struggle. You can only heave again once the slack is in, and every heave makes the fish bolt sooner.

You can fish sitting on a chair, a bench or the ground. A *Serpent trophy* on an *Item stand* calms the waves around it once you tap the use key on that stand.

## Difficulty

`Difficulty` picks one of five levels, from `VeryEasy` to `VeryHard`, with `Normal` by default. Picking a level fills in the settings it sets, and changing one of them yourself makes it `Custom`.

## Multiplayer

Install Rodnarök on every player and on a dedicated server. The server's settings apply to everyone on it, and only an admin changes them in game.

## Settings

All settings are in `BepInEx/config/isimp.Rodnarok.cfg`, and `/rodnarok` in the chat tells where. `CastHints`, `SeatedMotions` and `FloatWisp` are your own, never the server's.

## Other mods

The fish a bait brings can only be caught with the rod, so a spear, harpoon, arrow or bolt from Spearfishing that hits one catches nothing and scares the school away. Fish of the world's own are left to Spearfishing as before.

## Thanks

Several ideas come from ChillFishing by Lenson, above all calming the water where you fish.

## More

Technical notes and build instructions are on GitHub at https://github.com/isimp/Rodnarok
