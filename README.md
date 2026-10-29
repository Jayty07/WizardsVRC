# WizardsVRC

A VRChat **world** project (Unity 2022.3.22f1 + VRChat Worlds SDK 3.10.5, which includes UdonSharp) with a
character-select room, a hub stub, and a persistent save system built on VRChat PlayerData.
All art is generic placeholder geometry.

## Opening the project (Windows, first time)

1. Install **Unity Hub** and **Unity 2022.3.22f1** (Hub > Installs > Install Editor > Archive, or
   `unityhub://2022.3.22f1/887be4894c44`). Tick *Windows Build Support (Mono)*.
2. Install **VRChat Creator Companion** (https://vrchat.com/home/download) or **ALCOM**
   (https://vrc-get.anatawa12.com/alcom/).
3. Clone this repo:
   ```
   git clone https://github.com/Jayty07/WizardsVRC.git
   ```
4. In VCC/ALCOM: **Add Existing Project** → pick the `WizardsVRC` folder. It reads `Packages/vpm-manifest.json`
   and installs `com.vrchat.base` + `com.vrchat.worlds` 3.10.5 (click **Resolve** if asked).
5. Click **Open Project**. On first open the editor script `WizardsVRCSceneBuilder` automatically:
   - switches the build target to PC (Windows 64-bit) and the colour space to Linear,
   - creates UdonSharp program assets in `Assets/WizardsVRC/UdonPrograms/`,
   - generates `Assets/Scenes/MainWorld.unity` and adds it to Build Settings.

   If you ever need to regenerate it: menu **WizardsVRC > Rebuild MainWorld Scene**.
6. Commit the generated files (scene, materials, program assets) so they are in the repo too.

## Build & Test (local only)

1. Open `Assets/Scenes/MainWorld.unity`.
2. **VRChat SDK > Show Control Panel** → sign in → **Builder** tab. If it offers *Setup Layers for VRChat* /
   *Set Collision Matrix*, click those first.
3. Check the Unity Console has no red UdonSharp errors, then press **Build & Test New Build**.
   Do **not** press Upload.

Quick test without VRChat: press Play in Unity (ClientSim, bundled with the SDK). Persisted data can be
inspected via **VRChat SDK > ClientSim > Player Data** (exact menu may differ by SDK version).

## Layout

| Path | What |
| --- | --- |
| `Assets/WizardsVRC/Scripts/School.cs` | `School` enum (Fire/Ice/Storm/Life/Death/Myth/Balance) |
| `Assets/WizardsVRC/Scripts/CharacterData.cs` | Character fields + binary codec (version header, Adler-32 checksum, base64) |
| `Assets/WizardsVRC/Scripts/SaveManager.cs` | PlayerData persistence: `char_0`–`char_3`, `char_N_bak`, `meta` |
| `Assets/WizardsVRC/Scripts/PlayerState.cs` | Live character (source of truth) + autosave, current health/mana helpers, out-of-combat regeneration |
| `Assets/WizardsVRC/Scripts/TeleportHelper.cs` | `TeleportTo(Transform)` via `VRCPlayerApi.TeleportTo` |
| `Assets/WizardsVRC/Scripts/CharacterSelectUI.cs` | Slot panels, school picker, delete confirm, teleport to hub |
| `Assets/WizardsVRC/Scripts/ItemDatabase.cs` | Placeholder item definitions (category, level/school req, stat bonuses incl. critical/block/pierce/power pip, potion restore amounts) + stat totals |
| `Assets/WizardsVRC/Scripts/InventoryBook.cs` | Backpack book: live avatar portrait (MirrorReflection-layer camera → RenderTexture), equipment slots + stats (left page), tabbed paged item grid + Equip/Unequip (right page) |
| `Assets/WizardsVRC/Scripts/CardDatabase.cs` | Placeholder spell cards (4 per school + 4 general), level/school learning rules, deck limits |
| `Assets/WizardsVRC/Scripts/DeckPages.cs` | Book's Deck tab: flip through the deck items you own, edit and equip them (left), spellbook with Add/Remove (right) |
| `Assets/WizardsVRC/Scripts/BattleManager.cs` | Networked combat referee (object owner): arena queue countdown, boss + scaled minions, pips + power pips, accuracy, critical/block, school weakness/resist, wards/empower, monster AI, mid-fight joining, flee, round resolution |
| `Assets/WizardsVRC/Scripts/BattleClient.cs` | Local combat view: teleport/immobilize, 7-card hand from Deck 1, targeting, potions, flee, mana cost, round playback, rewards/defeat |
| `Assets/WizardsVRC/Scripts/BattleQueuePad.cs` | Hub teleporter pad trigger that joins/leaves the battle queue |
| `Assets/WizardsVRC/Scripts/PlayerHUD.cs` | Head-following HUD: name, level, health, mana, gold |
| `Assets/WizardsVRC/Scripts/LootChest.cs` | Test chest in the hub: random item + gold per click |
| `Assets/WizardsVRC/Scripts/EventRelay.cs` | Interact → `SendCustomEvent` on a target (backpack pedestal) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.cs` | Generates the scene and wires all references/buttons |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Zones.cs` | Builds zones, monster tables and authored quests from the world data |
| `Assets/WizardsVRC/Editor/WorldData/` | World authoring: Monster Catalog, Zone and Questline asset types, inspectors (map painter, spell picker, checks) |
| `Assets/WizardsVRC/WorldData/` | **Your authored world data** (created on first rebuild). Commit it. See [WORLD_BUILDING.md](WORLD_BUILDING.md) |
| `Assets/WizardsVRC/Scripts/QuestNpc.cs` | Zone NPC: click to talk, floating ! / ? quest markers |
| `Assets/WizardsVRC/Shaders/PSXBillboard.shader` | Pixel labels that face each player's camera (GPU only, VR-safe: faces the point between the eyes), stay upright, vertex-snap and dither out with distance |
| `Assets/WizardsVRC/Fonts/PsxNameFont.txt` | The PSX pixel font for NPC name tags, hint lines and quest markers, as editable `#`/`.` glyph grids (7 rows, or 9 with descenders). The builder bakes each label into `Materials/NameTags/` on rebuild (`WizardsVRCSceneBuilder.NameTags.cs`) |
| `Assets/WizardsVRC/Scripts/Wanderer.cs` | Patrolling monster: walks a route (loop or line) from the server clock, starts the fight on touch |

## Scene

- `VRCWorld` — `VRCSceneDescriptor`: spawn = `SelectRoom/SelectSpawn`, respawn height **-600** (must be below the
  select room at y = -500), reference camera = `VRCWorld/ReferenceCamera`.
  (There is no `VRC_SceneLightmapSettings` component in the current SDK; lighting uses normal Unity Lighting settings.)
- `SelectRoom` (y = -500) — closed room, light, spawn point, world-space canvas (`VRCUiShape`) with 4 slot panels,
  school picker, delete confirmation.
- `Hub` (origin) — **Starfall Castle**, built from the hub zone map (`WorldData/Zone_StarfallCastle.asset`): castle walls
  and towers on an island, a quarter for each of the seven schools with its professor at the hall door, the Headmaster's
  tower (door to the office), the Starfall fountain with the arrival point, the Bazaar, the Library, the Training
  Grounds (practice ring + boss arena pad), and quest-locked gates to Sunpetal Gardens and Lantern Row in the south wall.
  It also has the notice board ("Change Character" / Backpack), the **Practice Chest** and the **Backpack pedestal**.
  Without a hub zone the older hand-made castle is built.
- `KeepInterior` (y = 200) — the Headmaster's office (new characters start here). With outdoor schools that's all;
  otherwise also the Hall of Schools with a classroom per school.
- `Systems` — `SaveManager`, `PlayerState`, `TeleportHelper`, `ActiveCharacter` / `ScratchCharacter` (`CharacterData`),
  `ItemDatabase`, `InventoryBook` (book canvas opens in front of you: **B** on desktop, hub button, or pedestal),
  `PlayerHUD`.

## Save format

`CharacterData.Serialize()` writes:

```
'W' 'Z' | version u8 | payloadLength u16 | payload | Adler-32 u32
payload v1: name (u8 len + u16 chars), school u8, level u8, xp u32, gold u32, maxHealth u16, maxMana u16,
            equipped 7 x u16, inventory (u8 used + used x [itemId u16, count u8]),
            3 decks x (u8 count + count x cardId u16), quests (u8 used + used x u8)
```

v2-v5 append training points + learned spells, tracked quest and quest step counters (see `CharacterData.cs`).
v6 appends `currentHealth u16, currentMana u16` (65535 = full). Older saves load as full health/mana and get the new
mana curve (20 + 2 per level).
v7 stores 8 equipment slots (slot 7 = deck) and 6 deck card lists, and appends `deckOwners u16 x 6` (which deck item
owns each list). Older saves get an equipped Apprentice Deck holding their old Deck 1.

v8 has 512 quest slots (save ids up to 511) with u16 counts and tracked quest; older saves load as before.
Typical saves are 1-2 KB raw (worst case about 2.6 KB raw). `meta` = `1|school,level,name|...|...` (empty entry = free slot).

Saving rotates the previous valid blob into `char_N_bak`. Loading a blob that fails the checksum falls back to the
backup and rewrites `char_N` from it. PlayerData has no delete API, so Delete writes empty strings.
Saves happen on create, on select/swap (the previous character is saved first), on "Change Character", and every
120 s (`PlayerState.autosaveInterval`).

## The book

**B** (desktop), the hub button or the backpack pedestal opens it. Bookmarks: **Pack** (equipment and items),
**Deck** (decks and spells), **Quests**, and **Settings** (HUD and quest-arrow toggles, Save Now, Return to Starfall
Castle, and Change Character, which saves and takes you back to the select room).

## The start of the game

Like Wizard101, new wizards don't meet a real monster until they know the basics. After choosing a school in the
Headmaster's office you find your professor at their school hall, report back, then take the castle tour: Gatekeeper
Bramwell (exploring the grounds), Quartermaster Pell (backpack, gear, potions), Librarian Wren (cards, pips, accuracy,
decks) and Duel Master Holloway (a practice duel against training dummies that never attack). Only then does the
**Sunpetal Gardens** gate open (Lv 1-3 weeds, newts and moths, boss Old Briarwick). Fights there (and on the practice
ring) are **even fights**: one monster per wizard, starting with the one you touched; a second wizard brings in the next
patrol monster to reach the circle, and other patrol monsters that wander into the circle fade away. Finishing the gardens opens the
**Lantern Row** gate (now Lv 4-6) and the boss arena pad. The locks are the gates' *Required Quest* in the hub map.

## Performance

Each player's client only runs what's near them: areas you aren't in (hub, boss arena, every zone) are switched off
locally, nothing past the fog is drawn, and small props are chunked so they stop drawing beyond each zone's Detail
Distance. See *Performance (culling)* in [WORLD_BUILDING.md](WORLD_BUILDING.md).

## Building zones, monsters and quests

Worlds are built like Wizard101's: each has a goal set out in the area, 10+ storylines (questlines) that lead up to it,
and a finale that needs every storyline before players move on to the next world. The quest log shows the current
world's goal and how many storylines are finished.

Streets, battle ring spots, patrol routes, NPCs, monsters (with their spell lists) and questlines are all edited as
data in `Assets/WizardsVRC/WorldData` and versioned in git. See **[WORLD_BUILDING.md](WORLD_BUILDING.md)**.

## Combat rules (modelled on Wizard101)

- **Pips:** each round every combatant gains one pip (max 7). From level `powerPipLevel` (10) there is a
  `basePowerPipChance` (40%) that it is a **power pip**, plus any Power Pip % from gear at any level. A power pip is
  worth 2 pips for spells of your own school and 1 for anything else; on-school spells spend power pips first.
- **Fizzles** keep your pips, and the card isn't lost: it goes back into your deck at a random spot (Wizard101 rule), or to
  the end of your hand if `BattleClient.fizzleReturnsToHand` is on. Spells cost **1 mana per pip**, charged when cast, even on a fizzle;
  cards you don't have the mana for are greyed out.
- **Critical / Block:** gear Critical % doubles a hit or heal (`critMultiplier`); the target's Block % can stop it.
  Gear Pierce % ignores that much of shields and resist. Bosses have `bossCritPct`.
- **School matchups:** monsters and minions take `creatureSchoolWeakness` (30%) more damage from the opposite school
  (Fire/Ice, Storm/Myth, Life/Death) and `creatureSchoolResist` (30%) less from their own. Their overhead label says so,
  and the round log marks critical / weak / resisted / blocked hits.
- **Health and mana persist** between fights, and the HUD shows your live health during a fight. The school health
  trait (`PlayerState.schoolHealthPct`, e.g. Ice +25%, Storm -15%) is part of your max health everywhere. They regenerate slowly while you're out of combat
  (`PlayerState.healthRegenPct` / `manaRegenPct`), refill on level-up, and potions restore them: drink from the
  backpack (Drink button) or press **Potion** in battle, which uses your turn.
- **Defeat:** a wizard reduced to 0 health is sent home at once with `BattleClient.defeatHealthPct` (25%) health,
  and their seat frees up. The fight goes on for the rest of the party.
- **Flee** (planning only): leave the duel immediately; you keep your health but your mana drops to 0.
- **Joining a duel in progress:** in the wandering-monster lanes, walk into the circle while a seat is free and you
  join; you act from the next round.
- **Item cards:** gear can carry spells (`ItemDatabase.itemCardItems` / `itemCardIds`). While it's equipped they join
  your battle deck for each fight, on top of your deck (they don't use its space), and you can cast them whatever your
  school. The starting Training Staff holds one basic spell of every school, so new wizards have more to throw early on.
  The item's details in the backpack and the Deck page list them.
- **Decks are items** (Wizard101 style): the Apprentice Deck (20 spells, 4 copies each) to start, plus the Scholar's,
  Ironbound and Starfall decks (bigger, more copies, small stat bonuses) from loot and bosses. Equip one in the backpack
  or on the book's Deck tab; each deck keeps its own spells and battles draw from the equipped one.
- **Spell cards** use design 1 "Classic" (`Textures/Cards/CardFrame<School>.png`, made by `Tools/make_card_frames.py`):
  a vivid school-coloured frame, parchment face, name banner, pip gem, accuracy seal and the school badge. The art
  window shows the icon for shields, blades and traps; other spells keep a plain school glow until they have animations.
- **Battle hand:** seven cards fanned like Wizard101's; hovering a card makes it grow and rise, with its pip cost,
  school, accuracy and full effect. Pips show as dots (white, power, empty).
- **Two clear sides:** wizards always sit on the south half (blue seat sockets), monsters on the north half (red), with
  a glowing line between. Each monster seat shows the model of the monster actually in that seat.
- **Seat order:** each side fills from its own right-most seat (as seen by whoever sits there, facing the centre) and
  then leftwards; the target buttons are laid out in the same left-to-right order you see in the circle.
- **Health gauges:** an occupied seat's ring becomes a segmented PSX health gauge (24 segments, `PSXGauge` shader,
  `Tools/make_seat_gauge.py`) in the occupant's school colour. Damage drops it at once and leaves the lost part lit for
  a moment before it drains; heals fill back in; at 25% health or less it blinks red. Empty seats keep the plain ring.
- **Duel presentation:** when a fight forms, the circle appears in a white flash (column of light, sparks, the circle
  sound) and settles into its normal look. Its inner sigil and outer rune band then turn opposite ways at 15 frames a
  second (`BattleClient.circleFps`, PSX-style) while the sigil's glow pulses; the seat band stays put so the sockets line up with the seats.
- **Pips at the seats:** every combatant has their pips as gems in an arc in front of their seat (white = pip, gold = power
  pip), with white and gold motes swirling round them, more the more pips they hold. A caster's pips drop when their
  spell actually plays.
- **Shields, blades and traps** circle the combatant (shields at chest height, blades overhead, traps at the feet); each new
  one of a kind sits right next to the last. They appear when their spell plays and, when a hit uses one up, it blinks,
  bursts and its value floats off (-50% for a shield, +25% for a blade or trap, with the school's icon).
- **Effect icons** (`Textures/FxIcons`): shields are glossy heraldic shields with the school's symbol, blades are the
  school badge with a sword, traps are dark spiked shields with the symbol; damage numbers use the school badges.
  They face whoever is watching. `Tools/make_fx_icons.py` regenerates the shield / blade / trap set from the badges.
- **Floating combat text** over every combatant: damage numbers in the spell's school colour with its icon (CRITICAL! on
  crits), heals, Fizzled!, burns, Stunned!, and new shields / blades / traps. Several in one step stack up.
- **Combat UI:** the card panel hides while the round plays out, and its backing is see-through.
- **Defeat:** a monster (or minion) defeated this round stays in its seat until the hit that defeats it plays, holds
  for a moment (`BattleClient.defeatDelay`), then squashes into the floor in PSX steps (`defeatFadeSeconds`).
- **Sounds:** hit, heal and defeat sounds in the duel, the circle sound at the start, card hover / click / spell select,
  and click / hover sounds on every button (OK, Cancel, Delete, Next / Prev, tabs...). The book opening, potions and
  dialogue pop-ups have their own (`Audio/UI`).
- **Monster AI:** monsters heal an ally under 45% health, sometimes put a blade or shield on themselves first, and
  otherwise cast their biggest affordable attack (occasionally saving pips).

- **Spell design:** each school has a 12-spell tree on the Wizard101 milestones (L1 low-pip attack, L5 secondary
  attack/utility, L10 blade, L12 trap, L16 low-rank AoE or early minion, L22 shield/dispel, L26 mid attack, L28 minion,
  L33 buff utility / high-impact trap, L35 quest spell, L42 big hit, L48 7-pip AoE) and plays to its role: Storm glass
  cannon, Fire DoT + detonate, Ice tank, Life healer, Death drain, Myth minions + counter-play (shatter, dispel, stun,
  pierce), Balance universal support. See the remarks on `CardDatabase`.

All numbers are public fields on `BattleManager`, `BattleClient`, `PlayerState` and the databases, so they can be tuned
in the Inspector. **After pulling these changes run WizardsVRC > Rebuild MainWorld Scene** so the new Potion/Flee
buttons, synced arrays and database tables are in the scene.

## For future systems

Read and write `PlayerState.current` (a `CharacterData`), call `PlayerState.MarkDirty()` after changes, and
`PlayerState.SaveNow()` after important events (level up, quest turn-in).
