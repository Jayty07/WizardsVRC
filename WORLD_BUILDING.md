# Building the world yourself

Everything that makes up the explorable world is **data you edit in the Unity Inspector**. Nothing is hard-coded anymore:

| What | Asset | Holds |
| --- | --- | --- |
| Monsters | `Monsters.asset` (Monster Catalog) | id, name, school, level, **rank** and **creature type** (shown in the duel as "Rank 2 Life Plant"), health, **spell list**, XP, gold, drops, body size/colour |
| Zones | `Zone_*.asset` (Zone) | the layout as a text map, **battle ring spots**, **patrol routes**, NPCs, quest points, **landmarks** (school halls, gates, shops...), colours. One zone is the **castle hub**. |
| Quests | `Questline_*.asset` (Questline) | quests with steps, rewards, prerequisites, givers and dialogue; each questline is a **storyline** |
| Worlds | `World_*.asset` (World) | a world's **goal**, the quest that sets it out, its **10+ storylines** and its **finale** |

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

The first rebuild creates the folder with the defaults: the monsters, the **Starfall Castle hub**, **Sunpetal Gardens**
(the gentle first street), the maze-style **Lantern Row**, and their questlines. Commit it once so it's in the repo from
then on.

A folder made by an older version is **upgraded once** on the next rebuild: the hub, Sunpetal Gardens, the tutorial
questline and the new monsters are added, and Lantern Row moves later in the story (Lv 4-6). Only values that still hold
the old defaults are changed, so your edits are kept. `WorldDataVersion.txt` records that the upgrade ran; commit it too.

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
#  building (solid, random height, lit windows)   =  low wall (solid)   ^  tree (solid)   ~  water (solid)
.  road                                           ,  grass / courtyard  S  entrance (spawn + door back to the castle)
1-9  battle ring spot for encounter 1-9
a-i  patrol route for encounter 1-9 (a = 1, b = 2 ...)
any other symbol: an NPC, point or landmark from the zone's lists
```

Rules the checks enforce:

- Exactly one `S`. Every open cell should be reachable from it.
- **Ring spots need open space**: a duel ring is about 19 m across, so keep the 8 cells around a ring digit open
  (a 3x3 plaza). The check tells you which wall is in the way.
- A **patrol route** is a 1-cell-wide **line** (monsters walk to the end and back) or **loop** (they walk round and round)
  of its letter. Make it pass close to its ring.
- The outer edge should be `#`. It also gets an invisible wall.

Roads and plazas are cobbled with a kerb where they meet lawn, lawns are grassy, and the ground textures tile at a fixed
size whatever the cell size (`Textures/Terrain`, made by `Tools/make_psx_textures.py`; the zone colours tint them).

### Heights (terraces, sunken plazas, ponds)

Each zone has a **Height Map** under the map (*Edit map and height map as text*; *Start a flat height map* makes one): one
character per map cell, **0** = ground, **1-9** = raised that many steps, **a-e** = sunk 1-5 steps. **Height Step** is the
size of a step (1.5 m by default). The builder then:

- raises or lowers every cell, with **stone retaining walls** wherever the ground changes height;
- adds **stairs** (with stone cheeks) wherever two *paved* cells (road, plaza, an NPC's or point's cell...) meet at different
  heights, up to **2 steps** apart. Lawns, trees and water never get stairs, so a raised lawn is a wall you can't climb;
- caps every drop with a **coping** stone, or a waist-high **parapet** along paths more than 2.5 m above the ground below;
- fills **water** (`~`) a little below its lowest rim, with a low stone rim (and an invisible fence) round it. Paint the
  water's cells lower than the ground round it for a proper pond.

Keep buildings, fountains and battle-ring plazas on one height each (the checks warn otherwise). The map preview shades
cells by height, with white edges where stairs join and black edges where walls drop; red tint still means "can't be
reached from S".

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
| Even Fight | for new players' areas: the fight starts with **only the monster that was touched**. Each extra wizard who joins lets one more patrol monster join when it reaches the circle (or, with no patrol monster left, the next roster monster at once); any other patrol monster that reaches the circle **fades away** until it respawns. The rest keep patrolling during the fight. On in the castle practice ring and all of Sunpetal Gardens |

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

### Landmarks

Buildings and fixtures, mostly for the hub. Add one to the zone's **Landmarks** list with a map symbol, then paint it.
Buildings fill the block of cells you paint and put their door on the side facing the map's centre (at their own height),
or on the side you pick with **Door Side**; fixtures take one cell.

| Kind | Where | What it builds |
| --- | --- | --- |
| School Hall | hub | a walk-in hall in the school's colours with a full **classroom** inside (desks, chalkboard, banners, the school's props) and its **professor** (Talk / Learn Spells) at the front. One per school. |
| Keep | hub | the Headmaster's tower; its door leads to the Headmaster's office (where new characters start) |
| Fountain | any | basin with the Starfall Crystal (solid) |
| Library, Tower, Statue | any | scenery buildings (Library and halls are solid blocks with a door) |
| Shop Stall | any | a market stall per cell |
| Gazebo | any | a walk-in gazebo |
| Shop | any | a timber-framed shop or house filling its block: door, striped awning, shop windows, a hanging sign and its **Label** over the door (scenery) |
| Waterfall | any | paint it on the high side of a drop with water (`~`) below: a stone spring, a channel, the falling water (PSX waterfall shader), foam and spray |
| Street Gate | hub | portal arch to **Target Zone**; with **Required Quest** it stays locked (showing **Locked Hint**) until that quest is done |
| Arena Pad | hub | the boss arena teleporter (also lockable with Required Quest) |
| Bank Chest, Backpack Pedestal, Notice Board | hub | the practice chest, the book pedestal, and the Change Character / Backpack board |

### The castle hub

Tick **Is Hub** on one zone and it becomes Starfall Castle: it's built at the castle's place (Height is ignored), `S` is
where players arrive, and its **Street Gates** are the way into the other zones (so those zones need no door position).
**Castle Style** turns the map's edge into castle walls with towers on an island in the sea. When the hub has a School
Hall for all seven schools, every hall is a walk-in classroom with its professor inside and the keep interior is just the
Headmaster's office; otherwise the old indoor Hall of Schools is used. Without a hub zone the old hand-made castle is built.

The default hub is laid out like Wizard City's Ravenwood and Commons, on several levels:

```
 Observatory  Storm   Headmaster's Tower   Balance  Myth        NORTH TERRACE (+2 steps, parapet along its edge)
              ===== avenue ==== X waterfall =======              3 flights of stairs down
 Fire         ~~~ pond (-2) ~~~        Y gazebo         Life
 hats shop                                     wands shop
 Ice                 SUNKEN PLAZA (-1): Starfall fountain,      Death
 potion shop         C chest, P backpack, S arrival, A, N       Training Grounds (H, ring 1)
   Bazaar (stalls, Pell)       grand stairs                     ARENA DAIS (+1): Z boss arena pad
 LIBRARY RISE (+1)             RAISED GARDEN (+1)
   bakery  pets                                   inn   cards
        R Lantern Row gate                   G Sunpetal Gardens gate     (south wall)
```

The story order the defaults set up (nothing with real monsters until the castle tour is done):

| Save id | Quest | Teaches / unlocks |
| --- | --- | --- |
| 0 | The Prophecy (built in) | meet your professor at their school hall, report to the Headmaster |
| 8 | Finding Your Way | Gatekeeper Bramwell: explore the School Green, Bazaar and Training Grounds |
| 9 | Tools of the Trade | Quartermaster Pell: the backpack, equipment, potions (reward: healing potion) |
| 10 | The Spellbook | Librarian Wren: cards, pips, accuracy, the deck page (reward: mana draught) |
| 11 | First Duel | Duel Master Holloway: the practice ring against training dummies that never attack. **Opens the Sunpetal Gardens gate.** |
| 12 | Out Into the World | Bramwell sends you to Gardener Moss |
| 13-15 | Weeds in the Garden, Pest Control, The Briar Heart | Sunpetal Gardens (Lv 1-3), boss Old Briarwick (attack, blade, and Mend as his only heal). **15 opens the Lantern Row gate and the boss arena.** |
| 2-7 | Lantern Row | now Lv 4-6, after the gardens |

### Placement

Each street zone is built at its own **Height** (600, 700, 800...; at least 100 apart) so zones never overlap the castle
or each other. When the hub has a Street Gate for a zone, that gate is its way in. Otherwise the **door in the castle** is
placed with *Hub Door Position / Yaw* (Hub-local coordinates; select `Hub` in the built scene to find a spot), and *Hub
Return Position* is where you appear when you come back.

### Performance (culling)

Every zone is culled on each player's own client automatically, so you don't have to do anything when you add one:

- **Areas you're not in are switched off.** The hub, the boss arena and every street zone are deactivated locally
  unless you're inside them: no rendering, no physics, no scripts. Teleports switch the destination on just before you
  arrive (`AreaCuller` in `Systems`). The Headmaster's office and the cutscene sets always stay on.
- **Nothing past the fog is drawn.** The camera's far clip is the fog end + 50 m (220 m by default).
- **Small props drop out with distance.** Lamps, windows, flowers, stalls, merlons and other props up to 3.5 m are grouped
  into 32 m chunks that stop drawing beyond the zone's **Detail Distance** (80 m by default, in the zone's *Performance*
  section). Big buildings, walls and trees stay until the fog. Anything scripts use (patrols, rings, NPCs, quest points,
  gates) is never grouped.

Tips: keep zones at least 100 m apart in height (the checks enforce it), lower Detail Distance for dense mazes, and
lower `Lamp Density` if a zone has lots of lamps. The rebuild log says how many props were chunked per zone.

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

## Worlds

A world works like one of Wizard101's: its **goal** is set out in the area (for World 1, the Headmaster in The Prophecy:
the Starfall Crystal is fading), **at least ten storylines** build toward it, and a **finale** wraps it up. Players move
on to the next world only after the finale.

| Field | Meaning |
| --- | --- |
| Order | 1 = first world, 2 = next... |
| Goal | one or two sentences; the quest log shows it with "Storylines finished 3 / 10" |
| Goal Quest | save id of the quest where the goal is set out in the area. Every storyline should come after it |
| Finale Quest | save id of the world's last quest. **It automatically needs the end of every storyline**, so it can't start until they're all done |
| Storylines | the questlines leading up to the goal (10 or more). Each one counts as finished at its **Storyline End** quest (set on the questline; -1 = its last quest). Side quests can live in the same questline after or beside the end |
| Zones | the world's areas, for the checks |

The checks (in the World inspector and Validate World Data) warn when a world has fewer than 10 storylines, when a
storyline doesn't follow from the goal quest, and stop you when the next world's storylines or gates could be played
before the previous world's finale.

Making World 2: **WizardsVRC > World Data > New World**, set Order 2, its goal and goal quest; make the goal quest require
World 1's finale (16); give the hub gates to its zones Required Quest 16 (or its goal quest); add its questlines.

Save ids go up to **511** (save format v8), so there's room for 10+ storylines of several quests in every world.

The default World 1 (Starfall Isle) has three storylines so far (A Castle Tour, The Briar Heart, The First Lantern) and
its finale, quest 16 *Rekindle the Starfall Crystal*. As Tidewatch Docks, Kiln Street, Thornhollow Grove and the rest are
built, add their questlines to `World_StarfallIsle` and the finale waits for them too.

## Questlines

A Questline is a list of quests. Make one per street plus side quests, or put everything in one; all are loaded.

| Field | Meaning |
| --- | --- |
| **Save Id** | 2-511, unique across all questlines. Player progress is stored under this number, so **never change or reuse it** once people have played the quest. 0 and 1 are the built-in tutorial and trial. |
| Starts Automatically | on = starts once the prerequisite and level are met; off = talk to the **Giver Npc** |
| Offer Dialogue | what the giver says when offering it |
| Prerequisite | save id of the quest that must be finished first (-1 none, 0 = The Prophecy) |
| Also Requires | more quests that must all be finished first |
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

## The default Sunpetal Gardens

```
#############################
#^,,,,,,,,,,,^,,,,,,,,,,,,,^#
#,==========,,,,==========,,#
#,=,,,,,,,,=.....=,,,,,,,=,,#
#,=,ddd,,,,=..Y..=,,ccccc=,,#     Y = overgrown gazebo, ring 3: Mischief Moths (loop c)
#,=,d4d,,,,=..X..=,,c.3.c=,,#     ring 4: Old Briarwick in the briar patch, X = Explore point
#,=,ddd,,,,.......,,c...c=,,#
...
#,,.......bbbbbbbbbbbb....,,#     patrol line b: Puddle Newts by the ponds (ring 2)
...
#,,,,,,=,,,aaaaaaaa=,,,=,,,,#     ring 1: Thistle Sprouts, patrol loop a
...
#,,^,,,,,,,,M..,,,,,,,^,,,,,#     M = Gardener Moss
#,,,,,,,,,,,,.S.,,,,,,,,,,,,#     S = gate from the castle
#############################
```

## The default Lantern Row (Lv 4-6)

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
