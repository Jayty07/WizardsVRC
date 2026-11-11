# Spell animations: the staging formula

How every spell in WizardsVRC is staged, and why. One formula turns a spell's pip cost into the whole show: the six
beats Wizard101 uses (cast, arrival, display, attack, result, exit), how long each lasts, and what the cast is made of.
No spell lasts more than 14 seconds. The code is `Scripts/SpellCinematic.cs` (and `BattleClient` for blades, traps and
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

None of those threads gives measured timings, so the lengths below are ours: no spell lasts more than 14 seconds.

## What we change

Wizard101's subject is the summoned creature. **Ours is the caster.** The power comes out of the wizard (or monster)
standing in the middle of the ring. The six beats stay, each with our own content:

| Beat | In WizardsVRC |
| --- | --- |
| **Cast** | The caster vanishes from their seat, appears in the middle of the ring and charges: the school's rune draws itself under them, the symbol is drawn in the air, and the more the spell costs the more the charge is made of. |
| **Arrival** | The release. A shock goes off the caster and the spell's subject arrives: a core of power over the caster, or a creature spell's creature beside them. |
| **Display** | The subject shows off. |
| *(Expend)* | Every blade, trap and shield the cast uses goes, one at a time. Only when there are some. |
| **Attack** | The spell's own delivery flies to the target. A spell of 5 pips or more strikes twice: a lighter wave first, then the spell itself. |
| **Result** | The hit: the spell's own kind of impact, the numbers, the target reeling. It lands on every target at once for an area spell. |
| **Exit** | The power leaves and the caster goes back to their seat. |

Every beat but the expending is the spell's own: each spell picks how it charges, arrives, shows off, travels, lands
and leaves (see *Each spell's own beats*).

## The formula

`P` is the spell's pip cost, from 1 to 7.

```
whole turn  T = 9 + 0.5 x (P - 1) seconds            9 s at 1 pip, 12 s at 7
expending   E = 0.4 s for each blade, trap and shield used, 2 s at most
            so no spell ever lasts more than 12 + 2 = 14 s

cast        = 0.9 s to reach the middle  +  0.1 s x (P - 1) gathering  +  the school's cast sound
what is left of T after the cast and the 0.8 s back to the seat is shared out:
  arrival 16%    display 16%    attack 40%    result 17%    exit 11%
```

| Pips | Tier | Cast | Arrival | Display | Attack | Strikes | Result | Exit | Whole turn | With 5 effects used |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 3.9 | 0.69 | 0.69 | 1.72 | 1 | 0.73 | 1.27 | 9.0 | 11.0 |
| 2 | 1 | 4.0 | 0.75 | 0.75 | 1.88 | 1 | 0.80 | 1.32 | 9.5 | 11.5 |
| 3 | 2 | 4.1 | 0.82 | 0.82 | 2.04 | 1 | 0.87 | 1.36 | 10.0 | 12.0 |
| 4 | 2 | 4.2 | 0.88 | 0.88 | 2.20 | 1 | 0.93 | 1.40 | 10.5 | 12.5 |
| 5 | 3 | 4.3 | 0.94 | 0.94 | 2.36 | 2 | 1.00 | 1.45 | 11.0 | 13.0 |
| 6 | 3 | 4.4 | 1.01 | 1.01 | 2.52 | 2 | 1.07 | 1.49 | 11.5 | 13.5 |
| 7 | 4 | 4.5 | 1.07 | 1.07 | 2.68 | 2 | 1.14 | 1.54 | 12.0 | 14.0 |

Seconds throughout, with a 3-second cast sound (they run from 2.9 to 3.8: a longer one leaves a little less for the
beats after it, so the whole turn is the same for every school). Cast includes reaching the middle; exit includes going
back. Everything after the cast is worked out from one number, so every client lays a spell out the same.

**Who gets it.** Every wizard, every monster and every boss: the same formula, the same lengths. Only a summoned minion
casts from its seat with no staging.

**Which spells.** Every attack spell, the heals and the dispel. A heal goes to whoever it heals (an ally, or the caster
standing in the middle) and lands gently: no flinch, no shake, a rush of air where an attack has a boom. Spells that
only place a charm, a shield or a trap have no animation at all (below). Minions and the duel-wide bubbles keep their
own short ones.

## Cast

The school's rune draws itself on the floor under the caster, turning faster as the power builds, with the spell's own
flourish round them (energy streaming in, two rings on tilted axes, a fountain of embers, a double spiral of sparks, or
a column of light). The cast sound ends on the release. Each tier keeps everything below it and adds:

| Tier | Pips | Adds |
| ---: | --- | --- |
| 1 | 1-2 | The rune, the flourish, the light building. |
| 2 | 3-4 | Grit and motes lift off the floor. Sparks race round the rune's rim. A heartbeat of rings spreads from the caster, from one a second to three. The hum of the charge. |
| 3 | 5-6 | Everything but the magic goes dark. Sparks spiral up round the caster. Power crackles out of them, more and more often, and the light flickers with it. They stand in a column of light for the back half. In the last half second it is all drawn in. |
| 4 | 7 | The school's rune opens in the sky over the ring, 23 m across, and light pours down from it on to the caster. Both sets of rings turn round them. |

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
| How long a spell lasts | `BattleManager.spellSecondsMin`, `spellSecondsMax` |
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
