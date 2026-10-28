# Building the world yourself

Everything that makes up the explorable world is **data you edit in the Unity Inspector**. Nothing is hard-coded anymore:

| What | Asset | Holds |
| --- | --- | --- |
| Monsters | `Monsters.asset` (Monster Catalog) | id, name, school, level, health, **spell list**, XP, gold, drops, body size/colour |
| Zones | `Zone_*.asset` (Zone) | the street layout as a text map, **battle ring spots**, **patrol routes**, NPCs, quest points, doors, colours |
| Quests | `Questline_*.asset` (Questline) | quests with steps, rewards, prerequisites, givers and dialogue |

All of them live in **`Assets/WizardsVRC/WorldData/`**. They are plain text files, so git versions them like code.
Rebuilding the scene never touches them.

## Saving your work between versions

1. Edit the assets in the Inspector. Unity saves them when you press **Ctrl+S** (or File > Save Project).
2. Commit the folder, **including the `.meta` files**:
   ```
   git add Assets/WizardsVRC/WorldData Assets/WizardsVRC/WorldData.meta
   git commit -m "Add Tidewatch Docks"
   git push
   ```
3. On another computer (or after pulling), run **WizardsVRC > Rebuild MainWorld Scene** and your world is rebuilt from the data.

The first rebuild creates the folder with the defaults (the original monsters, the new maze-style Lantern Row and its
questline). Commit it once so it's in the repo from then on.

## The workflow

1. **WizardsVRC > World Data > Open World Data Folder** and click an asset to edit it.
2. Each inspector shows live checks (red = will break, yellow = probably a mistake).
3. **Rebuild MainWorld Scene** (button at the bottom of every inspector, or the WizardsVRC menu).
4. Press Play (ClientSim) or Build & Test to walk around.

Menu items: **WizardsVRC > World Data > New Zone / New Questline / Validate World Data**.

## Zones (streets, dungeons)

A zone is drawn as a **text map**. Each character is one square cell (`Cell Size` metres, 8 by default). North is the top.
In the inspector you can **paint** on the coloured map preview with a brush, or open *Edit map as text*.

```
#  building (solid, random height, lit windows)   =  low wall (solid)   ^  tree (solid)
.  road                                           ,  grass / courtyard  S  entrance (spawn + door back to the castle)
1-9  battle ring spot for encounter 1-9
a-i  patrol route for encounter 1-9 (a = 1, b = 2 ...)
any other symbol: an NPC or point from the zone's lists (it stands on road)
```

Rules the checks enforce:

- Exactly one `S`. Every open cell should be reachable from it.
- **Ring spots need open space**: a duel ring is about 19 m across, so keep the 8 cells around a ring digit open
  (a 3x3 plaza). The check tells you which wall is in the way.
- A **patrol route** is a 1-cell-wide **line** (monsters walk to the end and back) or **loop** (they walk round and round)
  of its letter. Make it pass close to its ring.
- The outer edge should be `#`. It also gets an invisible wall.

### Making it feel like Wizard101

- Roads 1-2 cells wide (8-16 m), with plazas where the rings are.
- Loop roads around blocks of houses so there's more than one way through, and **dead-end courtyards** (`,`) to hide
  quest items, NPCs and side quests in.
- Put a patrol loop around each ring plaza, or a patrol line along the road leading to it.
- Put the boss in its own walled courtyard (`=` walls, a narrow gate) with an Explore point at the gate.

### Battle rings (where fights happen)

Each **Encounter** in the zone's list links a ring digit to a fight:

| Field | Meaning |
| --- | --- |
| Ring Symbol | `1`-`9`. Put the digit on the map once, or several times for several possible spots. |
| Roster | monster ids in the fight; the first takes the boss seat (max 4) |
| Wanderers | how many monsters walk the patrol route (`a` for ring 1, `b` for ring 2...) |
| Patrol Speed | walking speed in m/s |
| Respawn Seconds | how long the patrol is gone after a fight |
| Scale With Party | spawn only the first monster plus one per wizard (bosses with adds) |

When a wizard touches a patrolling monster, the fight forms at the **nearest ring spot** of that encounter, like
Wizard101's duel circles. Other players can walk into the circle and fill free seats, even mid-fight. After a fight you
stay where you are; defeated wizards go back to the zone's entrance.

### NPCs

| Field | Meaning |
| --- | --- |
| Symbol | map character (one cell) |
| Key | name quests use (`Tilda`) |
| Display Name, colours, beard | how they look |
| Facing | degrees (0 = north), or -1 to face the road |
| Default Dialogue | what they say when no quest needs them (one line per row) |
| Voice Prefix | optional voice clips `Audio/Voice/<prefix>_00.wav`, `_01`... |

NPCs show a floating **!** when they have a quest for you and **?** when one of your quests wants you to talk to them.

### Points (quest objects)

| Kind | What it does |
| --- | --- |
| Interact | click it; advances *Interact* steps with this key (relight a lamp, pick up a cog) |
| Explore | walk into it; advances *Explore* steps (reach the tower, find the cat). *Trigger Size Cells* sets how big |
| Decoration | scenery, and a guide-arrow target |

Looks: StreetLamp (lights up when used), GlowingItem (disappears when picked up), Crate, Signpost, None.
Several cells with the same symbol make several of them (3 lamps = 3 `L`s); a step's count says how many are needed.

### Placement

Each zone is built at its own **Height** (600, 700, 800...; at least 100 apart) so zones never overlap the castle or each
other. The **door in the castle** is placed with *Hub Door Position / Yaw* (Hub-local coordinates; select `Hub` in the
built scene to find a spot), and *Hub Return Position* is where you appear when you come back.

## Monsters

Open `Monsters.asset`. Each monster has:

- **id** (1+): what rosters and quests use. Don't change it once quests refer to it.
- school, level, **health**, boss flag (+ extra health per extra wizard)
- **Spells**: up to 6, picked by name from the drop-down (open `MainWorld.unity` once so the list loads). The AI heals a
  hurt ally, sometimes blades/shields itself, and otherwise casts its biggest affordable attack. Monsters resist their
  own school and are weak to the opposite one.
- XP, gold range, a drop item + chance (deck items 60-63 make good boss drops), body size and colour.
- **Model Prefab** (optional): drag in a prefab and it's used for that monster on patrol and in its ring seat (feet at the
  pivot, facing +Z; Body Scale scales it; colliders are removed). Without one you get a coloured placeholder. Each ring
  seat always holds the same roster entry, so the model in a seat is always the monster really sitting there.

## Questlines

A Questline is a list of quests. Make one per street plus side quests, or put everything in one; all are loaded.

| Field | Meaning |
| --- | --- |
| **Save Id** | 2-127, unique across all questlines. Player progress is stored under this number, so **never change or reuse it** once people have played the quest. 0 and 1 are the built-in tutorial and trial. |
| Starts Automatically | on = starts once the prerequisite and level are met; off = talk to the **Giver Npc** |
| Offer Dialogue | what the giver says when offering it |
| Prerequisite | save id of the quest that must be finished first (-1 none, 0 = The Prophecy) |
| Min Level, XP, Gold, Reward Item, Reward Spell | rewards (`-1` spell = the player's own school quest spell) |
| Steps | up to 8, in order |

Step kinds:

| Kind | Uses | Example |
| --- | --- | --- |
| Talk | `npc` key (zone NPC, `Headmaster`, `OwnProfessor`, or a school name for that professor) + optional `dialogue` | "Return to Watchwoman Tilda" |
| Defeat | `monsterId`, `count` | "Defeat Gloom Wisps" x3 |
| Collect | `monsterId`, `count` (drops 60% per kill, guaranteed on the last one) | "Recover lamp-flames" x4 |
| Interact | `point` key, `count` | "Relight the lamps" x3 |
| Explore | `point` key | "Reach the tower" |
| Boss | `monsterId` | "Defeat Ashwick the Snuffer" |

The Questline inspector lists every NPC key, point key, monster id and the next free save id, so you don't have to look
them up. The guide arrow and HUD tracker work automatically from the step's target.

## The default Lantern Row

```
#################################
###########^,,,,,,,,,^###########
###########,.........,###########
###########,.........,###########     Snuffer's Tower courtyard: boss ring 4, Ashwick circling it
###########,...ddd...,###########
###########,...d4d...,###########
###########,...ddd...,###########
###########,.........,###########
###########==,.....,==###########
#############=..X..=#############     X = tower gate (Explore)
######.........................##
######.###.###.###.###.#######.##
######.###.###.###.###.#######.##
##.......#.....ccccccc.....###.##
##.#####.#.###.c.3.#.c.###.###.##     ring 3: Snatcher gang, patrol loop c
##.#^,G#.#.###.c...#.c.###.....##     G = brass cog in a dead-end courtyard
##.#,,,#.#.###.ccccccc.###.###.##
##.##.##...###.........###.###.##
##....########.#####.#####.###.##
#######.......L#####...........##     L = dead street lamps (3 in all)
#######.#####.######.#########.##
#.......#####.######.#########.##
#.#####.#####........#####,,,#.##
#.#,,C#.#####.bbbbbbb.####,K,#.##     C = Whiskers the cat, K = Marla the Candlemaker
#.#,,,#.......b.....b.####.,,#.##
#.##.##.#####.b..2..b.####.###.##     ring 2: Lantern Snatchers, patrol loop b
#....##.#####.b.....b.#....###.##
######..#####.bbbbbbb.#.#######.#
######.######.........#.......L.#
######.##############.#######.#.#
##.......#####...............##.#
##.#####.#####.###.#####.###.##.#
##.#^G,#.#####.###.#####.###....#
##.#,,J#.......###.#####.###.####     J = Pip (broken toy side quest)
##.#,,,#.#####.###.......###.####
##.##.##.#####.###.#####.###.####
##.......#####.eee.#####.....####
##########.....e5e.##############     ring 5: a pack of three Gloom Wisps
##########.....eee.##############
##########...aaaaa.##############
######.......a...a.##############
######..L....a.1.a.##############     ring 1: Gloom Wisps, patrol loop a
######..T....a...a.##############     T = Watchwoman Tilda
######.......aaaaa.##############
##########...S...################     S = gate from the castle
#################################
```

264 m x 368 m of crooked lanes, crossroads, loops round the house blocks and dead-end courtyards, with the four story
quests (ids 2-5, unchanged so existing saves keep their progress) and two new side quests (6: Whiskers Is Missing,
7: Pip's Broken Toy).
