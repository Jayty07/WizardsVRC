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
| `Assets/WizardsVRC/Scripts/TunnelPassage.cs`, `TunnelGate.cs` | Walk-through tunnels between areas (no portals): the hand-over between two identical Z-shaped tunnels out of sight, and the castle end's quest-locked portcullis |
| `Assets/WizardsVRC/Scripts/CharacterSelectUI.cs` | Slot panels, delete confirm, teleport to hub; New Character plays the opening, and `CreateFromCreator` makes the character the creator describes |
| `Assets/WizardsVRC/Scripts/IntroFlow.cs` | First join: once saves load, a player with no characters gets the opening; returning players stay at character select |
| `Assets/WizardsVRC/Scripts/AwakeningSequence.cs` | The opening, played round the player (not on a screen): whispers in the dark, eyes flickering open on five visions in a dream space (the Starfall Crystal, a rift in the sky, an iron legion, a figure in grey iron over a broken crystal, the title), then waking in the Headmaster's study to hear his visions, the creator, and his send-off. A beat table (subtitle, speaker, seconds, eyelid openness, vision, place, sounds) filled by `WizardsVRCSceneBuilder.Awakening.cs`; Skip jumps to the creator. Voice lines: `Audio/Voice/awaken_00.wav`... one per beat |
| `Assets/WizardsVRC/Scripts/CharacterCreator.cs` | The creator panel beside you in the study: a name from lists (first name + two-part surname, nothing typed), school (with each professor's pitch), signature colour and pip aura colour (12 each, the first = your school's colour), then a last look and Begin |
| `Assets/WizardsVRC/Scripts/ItemDatabase.cs` | Placeholder item definitions (category, level/school req, stat bonuses incl. critical/block/pierce/power pip, potion restore amounts) + stat totals |
| `Assets/WizardsVRC/Scripts/InventoryBook.cs` | Backpack book: live avatar portrait (MirrorReflection-layer camera → RenderTexture), equipment slots + stats (left page), tabbed paged item grid + Equip/Unequip (right page) |
| `Assets/WizardsVRC/Scripts/CardDatabase.cs` | Placeholder spell cards (4 per school + 4 general), level/school learning rules, deck limits |
| `Assets/WizardsVRC/Scripts/DeckPages.cs` | Book's Deck tab: flip through the deck items you own, edit and equip them (left), spellbook with Add/Remove (right) |
| `Assets/WizardsVRC/Scripts/BattleManager.cs` | Networked combat referee (object owner): arena queue countdown, boss + scaled minions, pips + power pips, accuracy, critical/block, school weakness/resist, wards/empower, monster AI, mid-fight joining, flee, round resolution |
| `Assets/WizardsVRC/Scripts/BattleClient.cs` | Local combat view: teleport/immobilize, 7-card hand from Deck 1, targeting, potions, flee, mana cost, round playback, rewards/defeat |
| `Assets/WizardsVRC/Scripts/BattleQueuePad.cs` | Trigger zone that joins/leaves a battle queue: the boss arena teleporter pad, and the duelling circle (keeps whoever stands inside queued) |
| `Assets/WizardsVRC/Scripts/RestoreOrb.cs` | Floating red / blue orb: walk through it to restore health / mana (local; respawns after 30 s) |
| `Assets/WizardsVRC/Scripts/TavernJukebox.cs` | The tavern's synced video player: link, start time and pause are synced (anyone can change the song), everyone seeks to the same point, retries when VRChat rate-limits loads, and fades the speaker so it's only heard inside the tavern; mute and volume are per player |
| `Assets/WizardsVRC/Scripts/PlayerHUD.cs` | Head-following HUD: name, level, health, mana, gold, and a segmented XP bar (fills in steps; on level-up it fills, flashes and starts again) |
| `Assets/WizardsVRC/Scripts/EventRelay.cs` | Interact → `SendCustomEvent` on a target (backpack pedestal) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.cs` | Generates the scene and wires all references/buttons |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Zones.cs` | Builds zones, monster tables and authored quests from the world data |
| `Assets/WizardsVRC/Editor/WorldData/` | World authoring: Monster Catalog, Zone and Questline asset types, inspectors (map painter, spell picker, checks) |
| `Assets/WizardsVRC/WorldData/` | **Your authored world data** (created on first rebuild). Commit it. See [WORLD_BUILDING.md](WORLD_BUILDING.md) |
| `Assets/WizardsVRC/Scripts/QuestNpc.cs` | Zone NPC: click to talk, floating ! / ? quest markers |
| `Assets/WizardsVRC/Shaders/PSX.shader` | The world's main shader, styled on PS1 platformers like Spyro: Gouraud lighting per vertex (sun, sky/ground ambient and the four nearest point lights, which the builder makes per-vertex), shadowed sides tinted a cool purple-blue, "hand-painted" vertex colour variation, a little extra saturation, vertex snapping (**WizardsVRC > PSX Shake**: Off / Subtle / Classic PS1 / Strong), partly affine textures, per-vertex fog and 15-bit colour with ordered dither. Each material's *PlayStation 1* and *Spyro style colour* settings can be tuned in the inspector |
| `Assets/WizardsVRC/Shaders/PSXCommon.cginc` | The PS1 tricks the PSX shaders share: vertex snap, 15-bit ordered dither in chunky virtual pixels (`PSX_DITHER_RES`, 480 across), steady per-vertex random |
| `Assets/WizardsVRC/Shaders/PSXWater.shader`, `PSXWaterfall.shader` | Spyro-style PSX water (vertex waves with a brightness shimmer and optional whitecaps, two scrolling pixel ripple layers, sparkles, affine warp, 15-bit dithered colour) and waterfall (racing streaks, foamy lip and foot, sway). Materials `Materials/PSX_Water`, `PSX_SeaSwell` (the open sea), `PSX_WaterFoam`, `PSX_Waterfall` are made once and then yours to tune; put them on any subdivided plane / sheet |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Round.cs` | The round hub's curtain wall and towers, the **duelling circle** (PvP encounter + countdown zone) and the health / mana orbs |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Tavern.cs` | The round **Tavern** landmark (the hub's Crystal Tap): curved bar, tables, stage with a jukebox (`TavernJukebox`, built by `WizardsVRCSceneBuilder.Jukebox.cs`: a synced VRChat video player with a screen over the stage, heard only inside the tavern, link field / default song / pause / per-player mute and volume on a lectern), hearth, mirror toggle, sit-able seats (`Seat`: VRCStation), synced mug pickups and the bell that resets them (`MugReset`), per-player toggles (`LocalToggle`) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Terrain.cs` | Zone ground from the map + **height map**, as merged per-chunk meshes: every cell a column to its height with stone walls down to lower neighbours, cobbles / lawns / pond beds, optional **rounded corners** (outer corners cut, inner corners filled, path-lawn corners curved), kerbs, copings, iron **railings** (drops, ponds, stair handrails, optional path edges), stairs with stringers, water, waterfalls |
| `Assets/WizardsVRC/Shaders/PSXSkyline.shader` | Skybox: gradient sky plus a pixel-art skyline strip round the horizon (the other areas where they lie, far isles, sea, clouds), 15-bit dithered |
| `Assets/WizardsVRC/Shaders/PSXSurf.shader` | Surf on a ring round an island: churning foam at the rocks and breaking wave lines rolling in |
| `Assets/WizardsVRC/Shaders/PSXEyelids.shader` | The opening's eyelids, on an inside-out sphere that follows your head: lids closing from top and bottom, vignette, dream tint and white flash |
| `Tools/make_intro_audio.py` | Generates the opening's placeholder sounds in `Audio/Intro` (drone, wordless whispers, war drums, vision flash, heartbeat). Replace any with a real recording of the same name |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Travel.cs`, `.Sky.cs` | Tunnels, mountains, the sea's swell and surf; the world's layout and each area's skyline (painted at build time) |
| `Assets/WizardsVRC/Shaders/PSXBillboard.shader` | Pixel labels that face each player's camera (GPU only, VR-safe: faces the point between the eyes), stay upright, vertex-snap and dither out with distance |
| `Assets/WizardsVRC/Fonts/PsxNameFont.txt` | The PSX pixel font for NPC name tags, hint lines and quest markers, as editable `#`/`.` glyph grids (7 rows, or 9 with descenders). The builder bakes each label into `Materials/NameTags/` on rebuild (`WizardsVRCSceneBuilder.NameTags.cs`) |
| `Assets/WizardsVRC/Fonts/PsxPixel.ttf` | The same pixel font as a real font, used by every word in a battle circle (card faces, the HUD, overhead names, the banner, floating numbers). Generated from `PsxNameFont.txt` by `python3 Tools/make_pixel_font.py` (needs `pip install fonttools`); re-run it after adding or changing glyphs, then rebuild. The circle's overhead names, banner, clock words, floating numbers and effect icons billboard to face you, turning only about the vertical axis so they never tilt |
| `Assets/WizardsVRC/Scripts/Wanderer.cs` | Patrolling monster: walks a route (loop or line) from the server clock, starts the fight on touch |

## Scene

- `VRCWorld` — `VRCSceneDescriptor`: spawn = `SelectRoom/SelectSpawn`, respawn height **-600** (must be below the
  select room at y = -500), reference camera = `VRCWorld/ReferenceCamera`.
  (There is no `VRC_SceneLightmapSettings` component in the current SDK; lighting uses normal Unity Lighting settings.)
- `SelectRoom` (y = -500) — closed room, light, spawn point, world-space canvas (`VRCUiShape`) with 4 slot panels,
  school picker, delete confirmation.
- `Hub` (origin) — **Starfall Castle**, built from the hub zone map and height map (`WorldData/Zone_StarfallCastle.asset`),
  laid out in rings like Ravenwood on a round island inside a round curtain wall with towers, in a PSX sea. In the middle
  the Starfall fountain stands inside the Crystal Tap (a round walk-in tavern to hang out in) in a sunken round plaza
  with the arrival point; round it a ring of gardens and the inner ring road, then the seven school halls and the
  Library on a circle, each turned to face the centre (a walk-in classroom and professor in every hall), with four
  avenues between them; then the outer ring road and the outer quarter: the Headmaster's tower (door to the office) on
  a north terrace with a waterfall pouring into a pond, the observatory hill, the Training Grounds (practice ring) and
  the boss arena pad, the **duelling circle** (PvP), the quest-locked tunnels to Sunpetal Gardens (end of the south avenue)
  and Lantern Row (end of the west avenue) through gatehouses into snowy mountains, the inn,
  bakery and gazebo garden, and the Bazaar and shops (hats, wands, potions, pets, cards). **Red and blue orbs** on the
  ring roads restore health and mana when you walk through them. It also has the notice board ("Change Character" /
  Backpack) and the **Backpack pedestal** by the tavern door. Without a hub zone the older hand-made castle is built.
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
v9 appends `signatureColor u8, auraColor u8` (the creator's colours; 0 = the school's colour). Older saves get 0 for both.
In battle they ride to the referee on top of the level in `NetJoin` (`level | signature << 8 | aura << 16`) and are
synced per seat in `BattleManager.look`: the wizard's name, seat frame and casting ring take the signature colour,
and a seat holding 5+ pips (`BattleClient.auraPipThreshold`) gets an aura of motes in the aura colour.
Typical saves are 1-2 KB raw (worst case about 2.6 KB raw). `meta` = `1|school,level,name|...|...` (empty entry = free slot).

Saving rotates the previous valid blob into `char_N_bak`. Loading a blob that fails the checksum falls back to the
backup and rewrites `char_N` from it. PlayerData has no delete API, so Delete writes empty strings.
Saves happen on create, on select/swap (the previous character is saved first), on "Change Character", and every
120 s (`PlayerState.autosaveInterval`).

## The book

**B** (desktop), the hub button or the backpack pedestal opens it. Bookmarks: **Pack** (equipment and items),
**Deck** (decks and spells, shown as the same spell cards as in battle), **Quests**, and **Settings** (HUD and quest-arrow toggles, Save Now, Return to Starfall
Castle, and Change Character, which saves and takes you back to the select room).

## The start of the game

Like Wizard101, new wizards don't meet a real monster until they know the basics. After choosing a school in the
Headmaster's office you find your professor at their school hall, report back, then take the castle tour: Gatekeeper
Bramwell (exploring the grounds), Quartermaster Pell (backpack, gear, potions), Librarian Wren (cards, pips, accuracy,
decks) and Duel Master Holloway (a practice duel against training dummies that never attack). Only then does the
**Sunpetal Gardens** tunnel open (Lv 1-3 weeds, newts and moths, boss Old Briarwick, who only heals with Mend so new wizards can beat him). Fights there (and on the practice
ring) are **even fights**: one monster per wizard, starting with the one you touched; a second wizard brings in the next
patrol monster to reach the circle, and other patrol monsters that wander into the circle fade away. Finishing the gardens opens the
**Lantern Row** tunnel (now Lv 4-6) and the boss arena pad. The locks are the Street Gates' *Required Quest* in the hub map.

After Lantern Row the rest of **World 1 (Starfall Isle)** opens street by street, each through its own tunnel:
**Tidewatch Docks** (Lv 7-9, smugglers and a spy in the castle), **Kiln Street** (Lv 8-10, the forge and the first
Dominion war-frame), **Thornhollow Grove** (Lv 10-11, survey stakes in the old wood) and **Hollow Keep** (Lv 11-13, the
Hollow King). Ten storylines lead to the finale, quest 16 *Rekindle the Starfall Crystal*, which ends around Lv 12-13
and opens the World 2 arch in the portal room.

**Levelling** gets slower as you go: XP to the next level is `100 x level + 10 x level^2` (Lv 1 → 2 takes 110, Lv 10 → 11
takes 2,000, Lv 30 → 31 takes 12,000). The level cap is **50** (`CharacterData.MAX_LEVEL`).

## Getting about

There are no portals between the castle and the streets: you walk through tunnels into the mountains round the castle and
come out of a mountainside in the street (they hand you over out of sight between two bends). Each area's sky shows the
others where they really lie, so from the castle you can see Sunpetal Gardens over the southern pass and Lantern Row over
the western one, and from the streets the castle with the Starfall Crystal's beam. The sea round the castle rolls with
swells and breaks in surf on the island. (The boss arena is still reached by its teleporter pad, and the keep's door
leads to the Headmaster's office.)

Travel **between worlds** goes through the **portal room**, through the door on the west wall of the Headmaster's study:
a round chamber with five arches. Starfall Isle's always glows; World 2's lights up once you finish quest 16
(it says "coming soon" until World 2 is built); the other three stay dark.

## Duels (PvP)

Step into the **duelling circle** (south-east of the hub) and a 15 second countdown starts; anyone else who steps in
before it ends joins. When it reaches zero everyone inside sits down in the circle, dealt alternately into the **Blue
team** (the near half) and the **Red team** (the far half), up to 4 a side. Alone in the circle, the countdown waits
for an opponent. Duels use the normal combat (cards, pips, shields, blades, minions), everyone starts at full health,
spells cost no mana, and nothing is won or lost: afterwards your health and mana are just as they were. Knocked-out
wizards stay in their seat to watch; the duel ends when a team has nobody standing (or leaves). Fleeing is free.

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
  (Fire/Ice, Storm/Myth, Life/Death) and `creatureSchoolResist` (30%) less from their own. Their overhead label doesn't
  spell this out: it reads like Wizard101, e.g. "Rank 2 <Life icon>", with "Elite" or "Boss" after it for those (minions: "<Fire icon> Minion"). The school icons are glyphs U+E000-U+E006 in the combat pixel font, coloured per school. The round log marks critical / weak / resisted / blocked hits.
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
- **Combat UI:** the card panel hides while the round plays out, and its backing is see-through. **Flee** asks first
  ("Flee the battle?" with Flee / Stay; it says whether your party fights on) and closes itself if the round starts.
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
