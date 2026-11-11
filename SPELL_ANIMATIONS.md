# Spell animations: the staging formula

How every spell in WizardsVRC is staged, and why. One formula turns a spell's pip cost into the whole show: the six
beats Wizard101 uses (cast, arrival, display, attack, result, exit), how long each lasts, and what the cast is made of.
The cast is a battle aura bursting alight round the caster; the display is the long beat, 5 seconds whatever the spell.
A spell lasts from 13 to 17 seconds (19 at the very most). The code is `Scripts/SpellCinematic.cs` (and `BattleClient` for blades, traps and
shields); the numbers are `CardDatabase.Plan*` and the fields under *Spell cinematics* on `BattleManager`.

## What Wizard101 does

An attack spell there runs through the same six beats, whatever it is:

1. **The cast.** The wizard draws the school's symbol in the air. It's the same length for every spell. A fizzle ends
   here.
2. **The arrival.** The view goes to the middle of the duel circle and the spell's subject (nearly always a creature)
   arrives: it rises out of the ground, drops in, climbs out of something. Bigger spells get longer arrivals and
   change the look of the circle while they play.
3. **The display.** The creature shows off before it does anything: a roar, a flourish, a pose.
4. **The attack.** It winds up, crosses to the target and strikes. Spells that hit everyone used to strike each enemy in
   turn, cutting from one to the next.
5. **The result.** The target flinches; the blades, traps and shields in play go off one by one; the number appears.
6. **The exit.** The creature leaves, often with a taunt, and the view goes back to the circle.

What players say about it, from the game's own forums (links under *Sources*):

| Finding | What we take from it |
| --- | --- |
| Length climbs with rank: "animation that are longer and longer as the difficulty to get them gets higher". | Length is a function of the spell's power, and only of that. |
| The show-off before the attack is valued: players defend the roar and the pose as part of what makes a spell feel big. | Keep a display beat before the damage. |
| Dead time is what's resented: an arrival whose extra seconds "really don't add anything", the walk from one target to the next, the taunt at the end. | Every act must add something new. The tail after the hit stays short. |
| Hitting enemies one at a time is the main complaint; spells changed to hit them all together are praised for it. | An area spell's hit lands on every target at once. |
| With the camera too close, the damage on the third and fourth enemy can't be seen. | The shot of the hit pulls back by the number of targets. |
| Repeats wear thin: the first cast "looks nice, but its getting very boring". | Waves differ from each other, and no two spells share a recipe. |
| Quick casts for shields and blades (which had taken 5 to 8 seconds each) were welcomed. | A spell that only places a charm, a shield or a trap has no animation: it just appears. |

None of those threads gives measured timings, so the lengths below are ours.

## What we change

Wizard101's subject is the summoned creature. **Ours is the caster.** The power comes out of the wizard (or monster)
standing in the middle of the ring. The six beats stay, each with our own content:

| Beat | In WizardsVRC |
| --- | --- |
| **Cast** | The caster vanishes from their seat, appears in the middle of the ring and charges: power streams in to them, a battle aura bursts alight round them and burns higher and wilder until the release, the school's rune draws itself under them, and the more the spell costs the bigger the aura and the more the charge is made of. |
| **Arrival** | The release. A shock goes off the caster and the spell's subject arrives: a core of power over the caster, or a creature spell's creature beside them. |
| **Display** | The subject shows off, for 5 seconds: it opens, builds through about ten quickening beats, and peaks. The long beat. |
| *(Expend)* | Every blade, trap and shield the cast uses goes, one at a time. Only when there are some. |
| **Attack** | The spell's own delivery flies to the target. A spell of 5 pips or more strikes twice: a lighter wave first, then the spell itself. |
| **Result** | The hit: the spell's own kind of impact, the numbers, the target reeling. It lands on every target at once for an area spell. |
| **Exit** | The power leaves and the caster goes back to their seat. |

Every beat but the expending is the spell's own: each spell picks how it charges, arrives, shows off, travels, lands
and leaves (see *Each spell's own beats*).

## The formula

`P` is the spell's pip cost, from 1 to 7.

```
cast        = 0.9 s to reach the middle  +  0.1 s x (P - 1) gathering  +  the school's cast sound (2.9 to 3.8 s)
display     = 5 s, whatever the spell
the rest    R = 3.6 + 0.35 x (P - 1) seconds          3.6 s at 1 pip, 5.7 s at 7, shared out:
              arrival 19%    attack 48%    result 20%    exit 13%
expending   E = 0.4 s for each blade, trap and shield used, 2 s at most (between the display and the attack)
back        = 0.8 s to the seat

whole turn  = cast + arrival + display + E + attack + result + exit + back
```

| Pips | Tier | Cast | Arrival | Display | Attack | Strikes | Result | Exit | Whole turn | With 5 effects used |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 3.9 | 0.68 | 5.00 | 1.73 | 1 | 0.72 | 1.27 | 13.3 | 15.3 |
| 2 | 1 | 4.0 | 0.75 | 5.00 | 1.90 | 1 | 0.79 | 1.31 | 13.8 | 15.8 |
| 3 | 2 | 4.1 | 0.82 | 5.00 | 2.06 | 1 | 0.86 | 1.36 | 14.2 | 16.2 |
| 4 | 2 | 4.2 | 0.88 | 5.00 | 2.23 | 1 | 0.93 | 1.40 | 14.7 | 16.7 |
| 5 | 3 | 4.3 | 0.95 | 5.00 | 2.40 | 2 | 1.00 | 1.45 | 15.1 | 17.1 |
| 6 | 3 | 4.4 | 1.02 | 5.00 | 2.57 | 2 | 1.07 | 1.50 | 15.6 | 17.6 |
| 7 | 4 | 4.5 | 1.08 | 5.00 | 2.74 | 2 | 1.14 | 1.54 | 16.0 | 18.0 |

Seconds throughout, with a 3-second cast sound (they run from 2.9 to 3.8: a longer one makes the cast, and so the whole
turn, that much longer). Cast includes reaching the middle; exit includes going back. The shortest a spell can be is
13.2 s (1 pip, Fire, nothing used up); the longest is 18.8 s (7 pips, Ice, five blades, traps and shields used).
Everything after the cast is worked out from one number, so every client lays a spell out the same.

**The 14-second ceiling is gone.** It could not stand with a 5-second display: the cast, the display and five effects
going already come to 12 s before anything is thrown. To bring spells back under it, shorten the display
(`CardDatabase.cineDisplaySeconds`) or the rest (`BattleManager.spellRestSecondsMin` / `Max`); `Tools/spell_staging_table.py`
prints what any numbers give.

**Who gets it.** Every wizard, every monster and every boss: the same formula, the same lengths. Only a summoned minion
casts from its seat with no staging.

**Which spells.** Every attack spell, the heals and the dispel. A heal goes to whoever it heals (an ally, or the caster
standing in the middle) and lands gently: no flinch, no shake, a rush of air where an attack has a boom. Spells that
only place a charm, a shield or a trap have no animation at all (below). Minions and the duel-wide bubbles keep their
own short ones.

## Cast

**A battle aura.** Every staged cast is the caster powering up in the middle of the ring:

1. **Gathering** (the first half second). Sparks stream in to the caster from all round; the school's rune opens on the
   floor under them.
2. **Ignition.** The aura catches with a thump: a flame-shaped shell of light in the school's colour bursts up round
   the caster, white-hot at its root, its outline licking and flickering; a ring and a spray of sparks blast out along
   the floor and the light jumps.
3. **Burning.** Tongues of flame race up inside the shell. It grows taller and wilder as the charge builds, and swells
   with every beat of the charge (the beats come quicker and quicker). Grit lifts off the floor. The film sinks to look
   up at the caster through the flames, and shakes harder as it goes on.
4. **The flare.** In the last fifth of the charge the aura flares to half as big again, brighter and faster.
5. **The release.** It bursts outward as the spell goes. A thin glow of it stays on the caster until their spell lands,
   and jumps once more at the display's peak.

The shell is a mesh that the `WizardsVRC/AuraFlame` shader sets burning (`SpellCinematic.AnimateAura`); the spell's own
flourish still plays round it (energy streaming in, two rings on tilted axes, a fountain of embers, a double spiral of
sparks, or a column of light). The cast sound ends on the release. The aura is sized by the pip cost, about 2.6 m tall
round a wizard at 1 pip and nearly 6 m at 7, and each tier keeps everything below it and adds:

| Tier | Pips | Adds |
| ---: | --- | --- |
| 1 | 1-2 | The aura, its flames, a soft beat of rings, a little grit lifting, the hum of the charge, the rune and the flourish. |
| 2 | 3-4 | Streaks of wind rush up round the aura. More grit and motes lift off the floor. Sparks race round the rune's rim. The beat of rings spreads from the caster from one a second to three, each swelling the aura. |
| 3 | 5-6 | A second, paler shell burns outside the first. Everything but the magic goes dark. Sparks spiral up round the caster. Power crackles out of them, more and more often, and the light flickers with it. They stand in a column of light for the back half. In the last half second it is all drawn in. |
| 4 | 7 | The school's rune opens in the sky over the ring, 23 m across, and light pours down from it into the aura. Both sets of rings turn round them. |

## Display

The long beat: 5 seconds for every spell, in three parts.

1. **It opens.** The subject has just arrived (a creature rears), and the hum starts to climb again.
2. **It builds.** Beats come slowly at first and then quicker and quicker, about ten in all. Each one swells the
   spell's subject, jolts the film, and does the spell's own thing (below).
3. **It peaks** (the last seventh): a ring of light and a spray of sparks from the subject, a boom, a flash, and the
   caster's aura jumps. Then the blades, traps and shields go, and the attack.

| The spell's display | Every beat | Throughout |
| --- | --- | --- |
| Flare | A ring of sparks and a ring of light from the subject, bigger each time. | |
| Halo | A ring of light spreads from it. | Rings spin round it and sparks circle it, faster and in more strands as it builds, their circle breathing in and out. |
| Crown | The next bolt along the crown flares; at the peak they all do. | A crown of bolts fans out behind the caster over the first third and hangs there, swaying. |
| Storm | A bolt strikes round it. | It crackles, thinly at first and then without a pause, sparks jumping from the floor further and further out. |
| Ascend | A ring of light climbs the column. | A column of light through it, and sparks spiralling up ever thicker. |

The film gives it three shots: round the caster and up to what has arrived; close under the subject, turning slowly
round it; then back and high behind the caster, the subject over them and the target beyond, closing in as it peaks.

## How big

Everything a spell shows is sized from its pip cost, and the size climbs faster than the cost: the effects treat 1 to 7
pips as 1, 2, 4, 6, 8, 10 and 13 (`SpellCinematic._vis`). That one number drives every size, count and rate.

| | 1 pip | 4 pips | 7 pips |
| --- | ---: | ---: | ---: |
| Rune on the floor | 2.7 m | 4.9 m | 8.0 m |
| Power core | 0.6 m | 1.3 m | 2.2 m |
| Sparks in the hit | about 60 | about 170 | about 320 |
| Column of light (a pillar impact) | 7 m | 13 m | 22 m |
| Light it throws | 6 m | 14 m | 32 m |

On top of the sizes, cost unlocks more:

| From | Adds |
| --- | --- |
| 3 pips | Aftershocks: the hit goes on going off round the target, one burst and ring of sparks after another (one at 3 pips, five at 7), each jolting the film. |
| 4 pips | Every beat of the charge's heartbeat throws a ring of sparks along the floor. The big impact sound. |
| 5 pips | A second strike (the wave before the spell itself), and everything in tier 3. |
| 6 pips | A second, wider ring of sparks on the release. |
| 7 pips | Tier 4: the rune in the sky, 23 m across, and the light pouring from it. |

The film stands further back for a bigger spell so it stays in the shot.

## Each spell's own beats

A spell's recipe (`Editor/WizardsVRCSceneBuilder.SpellStyles.cs`) picks one of each:

| Beat | The choices |
| --- | --- |
| Cast | The flourish: stream, rings, fountain, spiral, column. |
| Arrival | **Erupt**: up out of the rune in a geyser of sparks. **Descend**: down out of the sky behind streaks of light. **Converge**: sparks streak in from all round and knot together. **Rift**: through a gate that opens behind it. **Kindle**: wound up out of a tightening spiral of sparks. |
| Display | **Flare**: three pulses, each throwing a ring of sparks. **Halo**: rings spin up round it with sparks circling. **Crown**: a crown of bolts fans out behind the caster and hangs there. **Storm**: it crackles and sparks jump off the floor all round. **Ascend**: a column of light through it, sparks spiralling up. |
| Attack | The delivery: lance, helix, volley, meteor, rain, runner, beam, orbit, zigzag, portals, wave, boomerang; and the bolts' number, shape, lean and arc. |
| Result | The impact: burst, implosion, column, rings, spikes, vortex, nova, shatter, rune stamp. |
| Exit | **Fade**: embers drift up as the rune closes. **Scatter**: what is left is blown apart. **Ascend**: a column of sparks winds up into the sky from the hit. **Sink**: sparks all round the rune's rim as it shuts. **Echo**: a ring of sparks from the hit, then one from the caster. |

No two spells travel and land the same way (the build checks). A creature spell's creature is the subject in place of
the core: it arrives and shows off the same ways, rears for its display, and rides the attack.

## Spells with staging of their own

A spell can keep the formula's six beats and their times, its charge, how its hit lands and how it leaves, and play
its arrival, display and attack on a rig of its own, filmed with its own shots (`CardDatabase.cineSpecials`, set in
`SpellSpecialTable` in `WizardsVRCSceneBuilder.SpellStyles.cs`; `SpellCinematic.SetSpecial`). Only a cinematic cast
plays it; a minion's unstaged cast falls back on the spell's recipe.

### Sunfire Wyrm

The caster summons an eastern dragon out of themselves (`Scripts/SunfireWyrm.cs`; the stage's part is
`SpellCinematic.AnimateWyrm` and `WyrmShot`). It is about ten metres long, in fire colours, and from the moment it is
out until it is gone it sheds fire: flames off its back the whole way along, embers with them, more from its mane.

| Beat | What it does | The film |
| --- | --- | --- |
| Arrival | It bursts out of the caster's chest, jaws wide, in a gout of flame, and swings round into its first coil. | Before the caster, off to the side it swings away from, so it comes out at the lens. |
| Display | It twirls round the caster three times, climbing; a ring of light spreads under them each time it comes round. It sweeps out and down to the floor behind them, rears up over their shoulder, and comes to rest there four fifths of the way through the display. Then it roars: the display's peak. | Wide, drifting round against the twirls; then low and to one side as it comes down behind and rears; then low before the caster, looking up at them both, pushing in on the roar. |
| (Expend) | It waits behind them, swaying, while the blades, traps and shields go. | The usual side shot. |
| Attack | It draws its head back and goes: at its target and through it, jaws open (through every target in turn round the rim of the ring for a cast that hits them all; each bursts as its head goes through, and the first makes them flinch). It loops round (one target: up over them and back; several: spiralling in over the caster) and pulls up before them. For the last quarter of the attack it breathes fire on them, swept along them and back. | Wide (one target: side on, caster at one end of the picture and target at the other; several: from high behind the caster, the whole far side in view), following its head a little; then, for the fire, from beyond whoever it burns, looking back past them at it and the caster under it. |
| Result | The spell's hit lands in the middle of the fire (a nova on every target, as its recipe says) and the fire goes on pouring over them. | Wide again, shaken. |
| Exit | It burns away from the tail up, its head lifting to the sky. | Wide, turning slowly round the ring. |

How it flies: every place it goes is a waypoint; the waypoints are joined by a smooth curve and walked into points
15 cm apart, each with an "up" carried along the curve so the body never flips (it rolls upright at a steady rate and
leans into its turns); the head's distance along the curve is a function of time, and each of its 24 body segments
sits a fixed distance behind the head on the same curve, swaying, facing the one ahead. The curve is laid out for
whoever is cast at (their number, where they stand, how big the caster is: a boss's dragon goes wider round it) and
worked out a little each frame through the charge.

`Tools/sunfire_wyrm_flight.py` is the same flight in Python: it prints each case's path lengths, speeds and how level
the dragon flies. `Tools/preview_sunfire_wyrm.py` draws the whole cast from the film camera without Unity (a GIF, a
contact sheet or one frame) from that flight, the game's own meshes and skin, and SpellCinematic's numbers; the
wizard, the monsters and the scenery in it are stand-ins. It also paints the spell's card the way the builder does.
`Tools/make_sunfire_wyrm.py` makes the dragon itself: its skin (`Textures/Spell/WyrmSkin.png`, one 64 x 64 sheet of
pixel art), its five meshes and its pose on its card (`Editor/WyrmMeshData.cs`), which
`WizardsVRCSceneBuilder.Wyrm.cs` turns into the rig. Its card's picture is rendered from that posed dragon, like every
creature spell's.

## Blades, traps and shields

**Used.** After the display and right before the attack, every blade, trap and shield the cast uses goes, one at a time:
the caster's blades first, then the target's traps, then its shields. Each lifts out of its orbit flickering white,
holds white, then shatters into shards with its sound (pitched high for a blade, lower for a trap, lowest for a shield)
and its bonus floating up. Each takes 0.4 s; if there are more than five they go quicker, so the lot never takes over
2 s. The film pulls back to a side shot that holds the caster's seat and the target while they go. (A dispel is the
exception: what it strips goes as it lands.)

**Placed.** A spell that only places a charm, a shield or a trap has no cast and no animation. The effect appears round
its target with a chime (pitched the same way), its words float up, and the next caster goes about a second later.

## The film

Everyone else in the fight watches on the film screen from the start of the cast. The caster sees the cast with their
own eyes and the film from the release.

| Beat | Shot |
| --- | --- |
| Cast | Circling the caster, drawing in. Tier 3 and up open wide and high first; tier 4 then looks up from the floor at the rune in the sky. The shot shakes as a big one nears its release. |
| Arrival | Close on the caster, thrown back as it goes off and turning up to what arrives. |
| Display | Round the caster and up to the core or the creature. |
| Expend | From the side, wide enough for the caster's seat and the target. |
| Attack | A wave: over the caster's shoulder as it gathers, a travelling shot as it flies. The finisher: the spell's own hero shot of the caster pushing in, then its own way of following the flight. |
| Result | The target, shaken, held until the exit. For an area spell the shot pulls back for each extra target. |
| Exit | Wide, turning slowly round the ring. |

## Changing it

| To change | Where |
| --- | --- |
| How long the display lasts | `CardDatabase.cineDisplaySeconds` (5 s) |
| How long the arrival, attack, result and exit get | `BattleManager.spellRestSecondsMin`, `spellRestSecondsMax` |
| The aura's size, flames and brightness | `SpellCinematic.AnimateAura`; its flicker in `Shaders/AuraFlame.shader` |
| How long each used effect takes, and the most they take together | `BattleManager.expendSeconds`, `expendMaxSeconds` |
| The gather | `BattleManager.gatherPerPip` |
| How long a charm, shield or trap spell holds before the next caster | `BattleManager.charmSeconds` |
| Each beat's share, and when a spell strikes twice | `CardDatabase.PlanArrival`, `PlanDisplay`, `PlanAttack`, `PlanResult`, `PlanWaves` |
| Which pips are which tier | `SpellCinematic.Begin` (`_tier`) |
| What each delivery's wave is | `SpellCinematic._preludes` |
| A spell's own beats | `Editor/WizardsVRCSceneBuilder.SpellStyles.cs` |
| The table above | `Tools/spell_staging_table.py` prints it from the same formula |

Adding an attack spell needs nothing here: give it a pip cost and (if you want to choose them) a recipe, and the formula
stages it.

## Sources

- [Design: high level AoE spells duration](https://www.wizard101.com/forum/the-dorms/design-high-level-aoe-spells-duration-8ad6a414512444ef015155df83391303)
- [Spell animations that take too long](https://www.wizard101.com/forum/the-dorms/spell-animations-that-take-too-long-8ad6a41c3a920d1e013aafa2ccea3e3b)
- [Long boring spell animations: the fix](https://akamai.wizard101.com/forum/the-dorms/long-boring-spell-animations-the-fix-8ad6a4185124450a015136f53ec15d00)
- [Spell animation overhauls](https://www.wizard101.com/forum/the-dorms/spell-animation-overhauls-8ad6a426607332780160814a63e55aa2)
- [AoE animation change concern](https://www.wizard101.com/forum/halstons-laboratory/aoe-animation-change-concern-8ad6a42b78f3767b0178f82e58c66c11)
- [Spell animations too long?](https://www.wizard101.com/forum/the-dorms/spell-animations-too-long-8ad6a424586c66e8015881fb770946f6)
- [Very tired of spells with real long animation](https://www.wizard101.com/forum/ravenwood-commons/very-tired-of-spells-with-real-long-animation-43523)
- [About the new spell animations](https://www.wizard101.com/forum/ravenwood-commons/about-the-new-spell-animations-7923)
- [Personalize casting motions](https://www.wizard101.com/forum/the-dorms/personalize-casting-motions-8ad6a4255f529a5b015f59f4e134544a)
