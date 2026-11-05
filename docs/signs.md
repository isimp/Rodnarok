# Signs on the water

How each fish coming to a float tells itself on the water: its sign, the path the signs walk, the quiet at the float and the follow-in, the light and water they are drawn in, the Trollfish's hump, and the moments around a bite.

Code: `src/Model/ArrivalSign.cs`, `src/Model/SignPath.cs`, `src/Model/SignDepth.cs`, `src/Model/SignLight.cs`, `src/Model/WaterMood.cs`, `src/Model/TrollHump.cs`, `src/Model/BiteBuildUp.cs`, `src/Fishing/ArrivalSigns.cs`, `src/Fishing/Schools.cs`.

See also: [arrival](arrival.md), [the bite](bite.md), [calm water](calm-water.md), [settings](settings.md); all notes in [the index](index.md).

## Each fish's sign

Every fish coming to a float tells itself by a sign of its own on the way in (`ArrivalSign`, `ArrivalSigns`): a Perch small rings where a fish rises, a Giant herring a flicker of tiny rings, a Pike nothing until its school is within about 9 m of the float and then one swirl beside it, a Trollfish a hump of water surging up with spray off its front and a V wake, a Grouper single big bubbles bursting into a ring, a Coral cod a fizz of tiny bubbles, a Pufferfish domes swelling on the surface, an Anglerfish a green lure glow under the water, a Magmafish steam puffs, a Tuna a boil of short splashes, a Northern salmon a fin cutting the surface, and a Tetra a fast zigzag of glints; a fish another mod adds shows the Perch's rings.

The game steering a school's fish, or a fish of the world's own called to a float, marks each on its networked data with the float it is coming to (`Rodnarok.coming`), from when it sets off until a fish bites, and the fish going for the bait with the same float (`Rodnarok.going`): a called fish all the way, a school's fish once it may bite. Every game reads the marks and shows the signs, so every player at the water sees them.

## One path

A float's coming fish show one sign together, on one path (`SignPath`): the game turns each fish to a new place every 1.5 to 3 s, so the sign does not follow any one fish but the school's centre, smoothed over 0.9 s, and every showing comes on that path. The hump, the glow and the fin ride it, facing the way it moves and keeping that heading while it is still, and their wake is drawn where the path was over the last 1.6 s, spreading behind and showing only while it moves.

How near a school is goes by its nearest fish, from 30 m off to 4 m from the float, and on the way in the signs come from every 1.9 s far off to every 0.6 s close and grow from 45% to full strength.

## Quiet at the float, then the follow-in

Once the path is within 4 m of the float with no fish going for the bait the school is milling there and the signs go quiet: the travelling sign fades out over about half a second and one showing comes by the float every 2 s, for the hump, glow and fin a slow ring. The picked fish hovers within 1.5 m of the float meanwhile ([the bite](bite.md)), so that showing is where it is. Once it may bite it is followed in, smoothed over 0.25 s, the travelling sign back and a showing every 0.9 s.

## Light

The signs are drawn on the surface in the light the game puts on the world each frame (`SignLight`). A crest of foam is lit by the game's directional light (the sun by day, the moon it turns that light into at night) by its colour, strength and height above the horizon, plus the ambient light, at 90%. It is never under 20% brightness, so a night sign still reads, nor under 1.15 times as bright as the sky the water mirrors (the game's fog colour), since under a low or weak sun the water looks as bright as lit foam; its hue is kept either way. Under it lies a trough in the ambient light's hue at 30%, drawn just inside a ring, beside a wake and as the Trollfish's hump.

A ring's crest is brightest where it faces the light across the water, from 55% facing away to full, and rings and wakes thin to 40% of their width as they spread. A sign takes the game's fog by its own mode, density and range from the camera, toward the fog's colour and fainter. The Northern salmon's fin is dark by day and edged in the crest's colour by night; the Anglerfish's glow is a real light 0.6 m under the water.

## Water

The signs belong to the water they are on (`WaterMood`). The game makes its waves as strong as the water's depth setting at a place times the wind (`WaterVolume.CalcWave` scales its wave sum by `Lerp(0, wind, depth)`, and a Serpent trophy's calm lowers that depth setting), and that product, 0 to 1, is the roughness a sign reads. On calm water a sign shows whole; rougher water shows it fainter, down to 45% of its strength, and from a roughness of 0.2 breaks each ring in two places, up to 85% gone there, each ring's breaks turned at random.

Signs drift downwind with the game's wind, 0.35 m/s in the strongest, and steam leans with it as it rises. Within 40 m of the camera they rise and fall with the waves: each sign finds its body of water once, then reads the game's own wave sum directly (no physics search per point), a ring at 8 places round it and followed between, a wake at every fourth point along it, a dart at its three points; the hump, glow and fin sit on the water at the path. Farther signs lie flat.

## Contrast

The server setting `SignContrast` (0.5 to 2, 1 as made) sets how strongly the signs show for everyone: every part's strength is multiplied by it, never past whole, and above 1 the lines of rings, wakes, splashes and darts widen, up to half again at 2; a weaker contrast keeps them as wide. The Anglerfish's light shines by it too. Rough water's fading comes on top, so calm water reads clearest at any contrast, and a difficulty level leaves it as set.

## The Trollfish's hump

The Trollfish's hump (`TrollHump`) stands up from the water rather than lying on it, since from the shore a mark flat on the surface is seen edge-on and does not read. It faces the camera across the view, so each player sees it side-on: 0.7 m long seen end-on to 1.7 m swimming across, higher toward its front, its ends meeting the water. Every 0.9 s it surges from 0.13 m to 0.3 m, rising in the first quarter and settling, and through the first 35% of a surge spray ticks fly off its front; a fish barely moving raises it to a third. Its crest is a line lit by the game's light over a band in the trough's colour, fading toward the water, both riding the waves near the camera.

## Around the bite

As the fish marked as going for the bait closes its last 1.5 m, every game tilts the float's look (`FloatTremble`; the float's child objects drawn by a mesh, never the float with its physics body, so the hook and line are untouched) in twitches toward the fish: each noses it over in the first fifth and eases it back, up to 12 degrees at the 0.6 m reach a fish bites from, a twitch every 0.6 s far to 0.3 s close; the look stands upright again when the marks are taken at the nibble.

A fish that flees, the rest of a hooked school, a school scared away or darting from a fright, has the place it fled from and the server time marked on its networked data (`Rodnarok.fledFrom`, `Rodnarok.fledAt`). Every game watches the fish that came to a float within the last 10 s and, for a flee marked within 1.5 s, shows it darting off once (`ScatterBurst`): a ring where it was and a short V of a wake running 2.5 m away from the fright in 0.8 s, fast at first. The game takes the coming marks at the nibble, before the hook scatters the school, hence the watch.

A starred fish (the game's item quality on the fish's networked data, 1 plain) tells itself (`SignStars`): a school holding one shows 10% bigger and 15% bolder on the way in, the same for any number of stars, and a fish going for the bait shows by its own stars, 15% bigger a star up to 45% and 20% bolder, plain if it has none.

How deep the fish swims shows in its sign (`SignDepth`), by how far under the surface the path the signs ride lies: from the bait's depth, 1 m under the float where the hook hangs, up to the surface a sign shows as made; deeper it shows broader and fainter, as a swirl spreads before it reaches the surface, up to 60% bigger and 45% fainter from the 4 m a school out of sight dives at, about as deep as a Tuna swims (its prefab's `m_minDepth`). The game's other fish swim 1.5 to 3 m deep by their prefabs (a Pike 2 to 4 m, a Tetra 0.5 to 1 m), and a school's fish keep within half a metre of their least depth all the way in, so most signs show about 15% bigger and 11% fainter and tighten as the fish going for the bait rises to the hook; a school that dived out of sight sharpens as it rises on the way in. It comes on top of the stars.

The signs' rings, lines and spots are kept and used again rather than made with each showing (`PartPool`, 64 of each kind spare).
