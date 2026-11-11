# Spell animations: the staging formula

How every attack spell in WizardsVRC is staged, and why. One formula turns a spell's pip cost into the whole show: how
long its caster charges, what the charge is made of, how long the spell takes to unleash, and what happens in each
second of it. The code is `Scripts/SpellCinematic.cs`; the numbers are `CardDatabase.Plan*` and the fields under
*Spell cinematics* on `BattleManager`.

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
| Quick casts for shields and blades (which had taken 5 to 8 seconds each) were welcomed. | Only attack spells get the long treatment. |

None of those threads gives measured timings, so the lengths below are ours, set to the 5 to 20 seconds we wanted.

## What we change

Wizard101's subject is the summoned creature. **Ours is the caster.** The power comes out of the wizard standing in the
middle of the ring, so its six beats become two of ours:

| Wizard101 | WizardsVRC |
| --- | --- |
| The cast (fixed length) | **The charge**: the caster gathers power in the middle of the ring. Longer and made of more things the more the spell costs. |
| Arrival, display, attack, result, exit | **The unleash**: 5 to 20 seconds, in five acts. |

## The formula

`P` is the spell's pip cost, from 1 to 7.

```
charge   C = 0.5 x (P - 1)  +  the school's cast sound (about 3 s)
unleash  U = 5 + 2.5 x (P - 1)

inside the unleash, with x = P - 1:
  surge      0.5 + 0.15x      the release
  form       0.6 + 0.3x       the power takes shape
  barrage    W waves          W = 0.8x rounded: 0, 1, 2, 2, 3, 4, 4
  finisher   one slot         1.5 times as long as a wave's
  afterglow  1.0 + 0.2x       after the hit

the waves and the finisher share what is left between the form and the hit;
the hit lands at U minus the afterglow
```

| Pips | Tier | Charge | Unleash | Surge | Form | Waves | Each wave | Finisher | Hit lands at | Afterglow | Whole turn |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 3.0 | 5.0 | 0.50 | 0.60 | 0 | - | 2.90 | 4.00 | 1.00 | 9.7 |
| 2 | 1 | 3.5 | 7.5 | 0.65 | 0.90 | 1 | 1.90 | 2.85 | 6.30 | 1.20 | 12.7 |
| 3 | 2 | 4.0 | 10.0 | 0.80 | 1.20 | 2 | 1.89 | 2.83 | 8.60 | 1.40 | 15.7 |
| 4 | 2 | 4.5 | 12.5 | 0.95 | 1.50 | 2 | 2.41 | 3.62 | 10.90 | 1.60 | 18.7 |
| 5 | 3 | 5.0 | 15.0 | 1.10 | 1.80 | 3 | 2.29 | 3.43 | 13.20 | 1.80 | 21.7 |
| 6 | 3 | 5.5 | 17.5 | 1.25 | 2.10 | 4 | 2.21 | 3.31 | 15.50 | 2.00 | 24.7 |
| 7 | 4 | 6.0 | 20.0 | 1.40 | 2.40 | 4 | 2.55 | 3.82 | 17.80 | 2.20 | 27.7 |

Seconds throughout. The charge column uses a 3-second cast sound (they run from 2.9 to 3.8). The whole turn adds 0.9 s
to reach the middle of the ring and 0.8 s to go back.

Everything inside the unleash is worked out from its length alone, so every client lays it out the same from one number.

## The charge

The caster vanishes from their seat and appears in the middle of the ring, facing their target. The school's rune draws
itself on the floor under them, turning faster as the power builds, with the spell's own flourish round them (energy
streaming in, two rings on tilted axes, a fountain of embers, a double spiral of sparks, or a column of light). The
symbol is drawn in the air and the cast sound plays over the last three seconds, ending on the release.

Each tier keeps everything below it and adds:

| Tier | Pips | Adds |
| ---: | --- | --- |
| 1 | 1-2 | The rune, the flourish, the light building. |
| 2 | 3-4 | Grit and motes lift off the floor. Sparks race round the rune's rim. A heartbeat of rings spreads from the caster, from one a second to three. The hum of the charge. |
| 3 | 5-6 | Everything but the magic goes dark. Sparks spiral up round the caster. Power crackles out of them, more and more often, and the light flickers with it. They stand in a column of light for the back half. In the last half second it is all drawn in. |
| 4 | 7 | The school's rune opens in the sky over the ring, 15 m across, drawing itself as the one on the floor does, and light pours down from it on to the caster. Both sets of rings turn round them. |

## The unleash

1. **Surge.** The release. A ring of sparks races out along the floor from the caster with a ring of light behind it,
   and a burst goes off them. Bigger spells throw up embers and crackle, and the dark flashes the school's colour.
2. **Form.** The power takes shape: a glowing **core** forms over the caster with sparks drawn into it. A creature
   spell's creature arrives beside the caster instead, out of a burst, and rears. This is the display beat.
3. **Barrage.** Waves are shed from the core, each knocking it back before it swells again. A wave is a lighter cousin
   of the spell's own delivery (a lance spell's waves are a volley, a zigzag, a helix, a wave abreast; which comes first
   is the spell's own), with one more bolt and more weight each time, leaning left then right. Each lands on the target:
   sparks, a ring under it, and the target flinches. No damage yet. A creature roars as each wave goes.
4. **Finisher.** The core swells and shudders, then goes into the spell's own delivery at full size: thrown, driven into
   the floor, flung into the sky, or held as the mouth of a beam. A creature rides it. Its hit is the spell's: the
   blades, traps and shields go off and the damage lands, on every target at once for an area spell, with the spell's
   own kind of impact. Bigger spells add a ring of sparks along the floor and crackle off the hit, and at seven pips
   the rune in the sky comes apart in a rain of light.
5. **Afterglow.** Embers rise, the rune closes, the dark lifts. It's never more than 2.2 seconds.

## The film

Everyone else in the fight watches on the film screen from the start of the charge. The caster sees the charge with
their own eyes and the film from the release.

| Act | Shot |
| --- | --- |
| Charge | Circling the caster, drawing in. Tier 3 and up open wide and high first; tier 4 then looks up from the floor at the rune in the sky. The shot shakes as a big one nears its release. |
| Surge | Close on the caster, thrown back as it goes off. |
| Form | Round the caster and up to the core (or the creature). |
| A wave | Over the caster's shoulder as it gathers (one shoulder, then the other), a travelling shot as it flies (a different kind for each wave), then the target as it lands. |
| Finisher | The spell's own shots: its hero shot of the caster pushing in, its way of following the flight, then the target, shaken. For an area spell the shot pulls back for each extra target. |
| Afterglow | Wide, turning slowly round the ring. |

## Who gets what

| Caster | Unleash | Gather |
| --- | --- | --- |
| A wizard, or a boss | 5 to 20 s | 0.5 s per pip over the first |
| Any other monster | 3.5 to 10 s | none |
| A summoned minion | no staging: one volley from its seat | none |

Lesser monsters get the short version so that their turns don't drag: three of them casting two-pip spells take about
28 seconds this way, against 38 at full length. The same formula lays their unleash out; at those lengths it's mostly
surge, form and finisher, with a wave or two for their biggest spells.

## Changing it

| To change | Where |
| --- | --- |
| How long wizards and bosses unleash for | `BattleManager.unleashMinSeconds`, `unleashMaxSeconds` |
| How long other monsters unleash for | `BattleManager.monsterUnleashMinSeconds`, `monsterUnleashMaxSeconds` |
| The gather | `BattleManager.gatherPerPip` |
| The acts' shares, the number of waves | `CardDatabase.PlanSurge`, `PlanForm`, `PlanAfterglow`, `PlanWaves` |
| Which pips are which tier | `SpellCinematic.Begin` (`_tier`) |
| What each delivery's waves are | `SpellCinematic._preludes` |
| A spell's own recipe (delivery, impact, flourish, film, bolts) | `Editor/WizardsVRCSceneBuilder.SpellStyles.cs` |
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
