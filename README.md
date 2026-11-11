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

   **Faster rebuilds:** the hub's occlusion bake (several minutes on its own) no longer runs on every rebuild. Run
   **WizardsVRC > Bake Hub Occlusion** once before you upload (it bakes and saves the open scene), or tick
   **WizardsVRC > Options > Bake Occlusion On Rebuild** to bake every time. Generated pictures (card art, the book's maps,
   the skylines) are only written and re-imported when they changed, so a rebuild with no art changes skips that work.
   Each rebuild logs how long it took and each stage's time, slowest first (`[WizardsVRC] Rebuild took ...`).

   **Debug buttons (testing):** tick **WizardsVRC > Options > Debug Buttons** and rebuild (or tick *Debug Enabled* on
   `Systems/DebugTools` in the scene, no rebuild needed). A red **Debug** button then appears at the top right of the
   book's Settings page; it opens a panel with: Level +1 / +5 / max, Training points +5, Gold +1,000, Starshards +100, Full
   health and mana, Treasure cards (3 of each), Learn my school's spells, Learn every spell, Next step of tracked quest and
   Finish tracked quest (rewards given as usual). They act on your own wizard and save at once (not during a duel). Fill
   *Allowed Names* on `Systems/DebugTools` to show them only to certain VRChat display names. The build log warns while
   they're on: turn them off before publishing.
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
| `Assets/WizardsVRC/Scripts/SaveManager.cs` | PlayerData persistence: `char_0`–`char_3`, `char_N_bak`, `meta`, and each character's last place and mark (`place_N`, `mark_N`) |
| `Assets/WizardsVRC/Scripts/PlayerState.cs` | Live character (source of truth) + autosave, current health/mana helpers, out-of-combat regeneration |
| `Assets/WizardsVRC/Scripts/TeleportHelper.cs` | `TeleportTo(Transform)` via `VRCPlayerApi.TeleportTo` |
| `Assets/WizardsVRC/Scripts/TunnelPassage.cs`, `TunnelGate.cs` | Walk-through tunnels between areas (no portals): the hand-over between two identical Z-shaped tunnels out of sight, and the castle end's quest-locked portcullis |
| `Assets/WizardsVRC/Scripts/CharacterSelectUI.cs` | The character select panel on the title stage: slot panels, delete confirm; Select hands over to the title screen (the scene fades out and back in where the character last logged off), New Character plays the opening, and `CreateFromCreator` makes the character the creator describes |
| `Assets/WizardsVRC/Scripts/LocationKeeper.cs` | Where each character last stood (so choosing them brings you back there), and **Mark Location** / **Teleport to Mark** |
| `Assets/WizardsVRC/Scripts/SceneFade.cs` | Fades the whole scene for the local player: the picture (a veil round your head, `Shaders/ScreenFade.shader`), the music and every ambient sound |
| `Assets/WizardsVRC/Scripts/IntroFlow.cs` | First join: once saves load, a player with no characters gets the opening; returning players get the character select on the title stage |
| `Assets/WizardsVRC/Scripts/AwakeningSequence.cs` | The opening, played round the player (not on a screen): whispers in the dark, eyes flickering open on five visions in a dream space (the Starfall Crystal, a rift in the sky, an iron legion, a figure in grey iron over a broken crystal, the title), then waking in the Headmaster's study to hear his visions, the creator, and his send-off. A beat table (subtitle, speaker, seconds, eyelid openness, vision, place, sounds) filled by `WizardsVRCSceneBuilder.Awakening.cs`; Skip jumps to the creator. Voice lines: `Audio/Voice/awaken_00.wav`... one per beat |
| `Assets/WizardsVRC/Scripts/CharacterCreator.cs` | The creator panel beside you in the study: a name from lists (first name + two-part surname, nothing typed), school (with each professor's pitch), signature colour and pip aura colour (12 each, the first = your school's colour), then a last look and Begin |
| `Assets/WizardsVRC/Scripts/ItemDatabase.cs`, `Tools/make_items.py` | World 1's 74 items (gear for every slot and school, potions, decks), generated from one level budget per stat; quest rewards and boss drops are unique. See *Items (World 1)* in [WORLD_BUILDING.md](WORLD_BUILDING.md) |
| `Assets/WizardsVRC/Scripts/InventoryBook.cs` | Backpack book: live avatar portrait (MirrorReflection-layer camera → RenderTexture), equipment slots + stats (left page), tabbed paged item grid + Equip/Unequip (right page) |
| `Assets/WizardsVRC/Scripts/CardDatabase.cs` | Spell cards, level/school learning rules, deck limits. Card faces show short Wizard101-style text with icons (`GetCardText`: numbers with school, damage, shield, blade, trap, heal, stun, over-time, accuracy, minion and break icons); the details box keeps the full sentence (`GetEffectText`) |
| `Assets/WizardsVRC/Scripts/MapPages.cs` | Book's **Map** tab: a storybook map of every zone and every world (drawn at build time by `Editor/WizardsVRCSceneBuilder.Maps.cs`); opens on the zone you're in with a red marker where you stand, turned to your heading; parchment tags name the exits and landmarks (an exit's tag opens that zone's map); **World Map** shows the world's zones as floating islands (click one to open it), **Next World** steps through the worlds, **Where am I?** comes back |
| `Assets/WizardsVRC/Scripts/DeckPages.cs` | Book's Deck tab, laid out like a spell deck screen: the "Spell Deck" plaque with a round icon per school (All, the seven schools, General) to filter by; the deck shown (flip through and equip the decks you own) with every card as its own square (click to take one out) and, under it, a row of the item spells your gear adds in battle; six big cards of the spells you know on the right (click to add a copy), each with how many the deck holds, page arrows down the sides; pointing at any card shows its details |
| `Assets/WizardsVRC/Scripts/BattleManager.cs` | Networked combat referee (object owner): arena queue countdown, boss + scaled minions, pips + power pips, accuracy, critical/block, school weakness/resist, wards/empower, monster AI, mid-fight joining, flee, round resolution |
| `Assets/WizardsVRC/Scripts/BattleClient.cs` | Local combat view: teleport/immobilize, 7-card hand from Deck 1, targeting, potions, flee, mana cost, round playback, rewards/defeat |
| `Assets/WizardsVRC/Scripts/CastPattern.cs`, `PatternWand.cs` | Drawing a spell's rune before it's sent (spells under 100% accuracy): the board of points along the school's symbol, the hip wand in VR (a local pickup; `PatternWand` relays its grab, drop and the sidearm's trigger), numbered points on desktop, the four casting styles (sabre, baton, lantern, sidearm), the quality that cuts the fizzle chance |
| `Assets/WizardsVRC/Scripts/SpellCinematic.cs` | Every attack spell, heal and dispel, staged by the formula in `SPELL_ANIMATIONS.md` on one shared rig, in six beats (cast, arrival, display, attack, result, exit; 14 s at most): the caster is taken to the middle of the ring and charges (four tiers by pip cost), then the spell plays its own recipe (flourish, arrival, display, delivery, impact, exit, film style, bolts), a creature riding the attack; filmed on the cutscene screen |
| `Assets/WizardsVRC/Scripts/BattleQueuePad.cs` | Trigger zone that joins/leaves a battle queue: the boss arena teleporter pad, and the duelling circle (keeps whoever stands inside queued) |
| `Assets/WizardsVRC/Scripts/RestoreOrb.cs` | Floating red / blue orb that drifts round the area on a wandering loop (server-clock timed, so everyone sees it in the same place): walk into it to restore health / mana (local; respawns after 30 s) |
| `Assets/WizardsVRC/Scripts/MusicDirector.cs` | Local background music: picks the combat, character creation or hub track from what you're doing and where you are, crossfades between them, scales by the Music setting, and credits what's playing ("WildStar OST - Character Creation") over the HUD, on the map and on the title stage |
| `Assets/WizardsVRC/Scripts/TavernJukebox.cs` | The tavern's synced video player: link, start time and pause are synced (anyone can change the song), everyone seeks to the same point, retries when VRChat rate-limits loads, and fades the speaker so it's only heard inside the tavern; mute and volume are per player. **Big screen** (synced) brings the screen down in front of the curtains at 2.2x the size for watching videos |
| `Assets/WizardsVRC/Scripts/FireFlicker.cs` | The tavern hearth's stylised fire: layered flame tongues stretch and sway, embers rise and the light flickers (local; only while you're near) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Magic.cs` | Hub atmosphere: crystals, gems, wisps and curios that hover and turn (native looping clips, a halo of shards round the Starfall Crystal), each classroom's motes and floating curios (embers, snow, sparks, pollen, wisps, glitter, sand), the tavern stage's finished front (nosing, velvet fascia, footlights, proscenium pillars, header and valance, backdrop), the hearth fire, and the hub's occlusion culling setup and bake |
| `Assets/WizardsVRC/Scripts/PlayerHUD.cs` | Head-following HUD: name, level, health, mana, gold, and a segmented XP bar (fills in steps; on level-up it fills, flashes and starts again). In VR it sits lower and nearer the middle of your view, 1.5x bigger and turned to face your eyes (`headOffsetVR`, `vrScale`), or floats over your left hand (Settings > **VR HUD**, remembered) |
| `Assets/WizardsVRC/Scripts/EventRelay.cs` | Interact → `SendCustomEvent` on a target (backpack pedestal) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.cs` | Generates the scene and wires all references/buttons |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Zones.cs` | Builds zones, monster tables and authored quests from the world data |
| `Assets/WizardsVRC/Editor/WorldData/` | World authoring: Monster Catalog, Zone and Questline asset types, inspectors (map painter, spell picker, checks) |
| `Assets/WizardsVRC/WorldData/` | **Your authored world data** (created on first rebuild). Commit it. See [WORLD_BUILDING.md](WORLD_BUILDING.md) |
| `Assets/WizardsVRC/Scripts/DungeonSigil.cs` | Wizard101-style dungeon entrance: four floor pads, a countdown, the party goes in together and has the dungeon to itself until everyone has left (Hollow Keep) |
| `Assets/WizardsVRC/Scripts/InteractMuter.cs` | Keeps pop-up panels (dialogue, NPC menu, spell trainer, new-spell pop-up, book) clickable: while one is open, clickable colliders between your eyes and it are switched off, so an NPC or door you're standing against can't catch the pointer |
| `Assets/WizardsVRC/Scripts/QuestNpc.cs` | Zone NPC: click to talk, floating ! / ? quest markers |
| `Assets/WizardsVRC/Shaders/PSX.shader` | The world's main shader, styled on PS1 platformers like Spyro: Gouraud lighting per vertex (sun, sky/ground ambient and the four nearest point lights, which the builder makes per-vertex), shadowed sides tinted a cool purple-blue, "hand-painted" vertex colour variation, a little extra saturation, vertex snapping (**WizardsVRC > PSX Shake**: Off / Subtle / Classic PS1 / Strong; players can turn the wobble off for themselves in the book's Settings), partly affine textures, per-vertex fog and 15-bit colour with ordered dither. Each material's *PlayStation 1* and *Spyro style colour* settings can be tuned in the inspector |
| `Tools/make_school_textures.py` | The classrooms' PS1 textures (`Textures/School`): a swallowtail banner and a medallion carpet with each school's symbol, an aisle runner per school, desk wood. The carpets and runners are 5 cm boxes on the floor, side by side, so nothing flickers |
| `Tools/make_street_fx.py` | The sprites for the streets' drifting particles (`Textures/StreetFx`): leaf, petal, smoke puff, butterfly (two frames), bubble and drip, all white so each street tints them |
| `Tools/make_street_textures.py` | The 64x64 PS1-style ground, wall, building, roof, hedge and tree textures (`Textures/Terrain`, `Textures/Street`), plus metal plates, sand, woven cloth, stage velvet and a chalkboard; each zone's *Texture Style* picks a set, its colours tint them. Plain PSX materials named for stone, wood, metal, cloth, sand, snow and so on wear these automatically (`AutoTexture` in `WizardsVRCSceneBuilder.Castle.cs`, laid out in world space); glowing ones keep the fine speckle |
| `Assets/WizardsVRC/Scripts/CosmeticsDatabase.cs`, `PlayerCosmetics.cs`, `WardrobePages.cs`, `ShopPages.cs`, `StarshardStore.cs` | Titles, auras, spell skins and elixirs; showing what each player wears to everyone; the book's Wardrobe and Starshard Shop tabs; the optional VRChat Creator Economy products. See *Shops, Starshards, titles and auras* |
| `Assets/WizardsVRC/Scripts/GoldShop.cs`, `ItemTooltip.cs` | The castle's gold shops (click a shop's counter) and the hover tooltips on items, gear slots, spell cards and shop rows (gear shows the worn piece beside it) |
| `Assets/WizardsVRC/Scripts/NotificationCenter.cs` | The book's Notices tab, the unread badges and the toast |
| `Assets/WizardsVRC/Scripts/TowerFloor.cs`, `Editor/WizardsVRCSceneBuilder.Towers.cs` | **Boss towers**: every street's boss waits behind a door at the far end of the street, in a tall tower you can see from the entrance. The four-pad sigil in front of it stays sealed until a quest sends you to fight the boss; inside is a single chamber with the boss, who speaks as you arrive; beating it marks your wizard and pays Starshards. See *Boss towers* in [WORLD_BUILDING.md](WORLD_BUILDING.md) |
| `Assets/WizardsVRC/Scripts/TreasureCache.cs`, `Editor/WizardsVRCSceneBuilder.Caches.cs` | **Treasure caches** (the Cache landmark): a chest at the end of a side branch; click it for gold. It's empty only for you and fills again after 20 minutes |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Secret.cs` | **Secret bosses**: the Kraken beneath Tidewatch Docks (a diving bell at the water's edge, opened by Fenwick's level-8 side quest, down to a sunken platform with kelp, coral and a wreck), plus the Kraken's and its tentacles' bodies. See *Secret bosses* in [WORLD_BUILDING.md](WORLD_BUILDING.md) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Backdrop.cs` | What you see past a street's edge, matching the skylines (every place an isle in a sea): the street's rocky island and cliffs down to a rolling sea, its neighbourhood round the map in its own style (rooftops, chimneys and lamplit windows; hills, trees and giant flowers; forest; crags and ruins; smokestacks and slag; warehouses), climbing away in three rows of rock terraces sized from the street's own roofline so it shows over the edge buildings, with a skyline row of tall shapes (towers, spires, smokestacks, giant trees, crags, sandstone spires) behind, boats, buoys and a lighthouse off any shore the street meets, and islets out at sea, also round the castle. Merged into one mesh per material, nothing solid |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Signs.cs` | Every name over a building, shop, tunnel or door is lettered in the pixel font on a carved board with a gold border (`Textures/UI/SignBoard.png`), backed by a wooden slab with gold finials; each street gets a welcome arch with its name and level range a few steps out of its tunnel |
| `Assets/WizardsVRC/Shaders/PSXCommon.cginc` | The PS1 tricks the PSX shaders share: vertex snap, 15-bit ordered dither in chunky virtual pixels (`PSX_DITHER_RES`, 480 across), steady per-vertex random |
| `Assets/WizardsVRC/Shaders/PSXWater.shader`, `PSXWaterfall.shader` | Spyro-style PSX water (vertex waves with a brightness shimmer and optional whitecaps, two scrolling pixel ripple layers, sparkles, affine warp, 15-bit dithered colour) and waterfall (racing streaks, foamy lip and foot, sway). Materials `Materials/PSX_Water`, `PSX_SeaSwell` (the open sea), `PSX_WaterFoam`, `PSX_Waterfall` are made once and then yours to tune; put them on any subdivided plane / sheet |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Round.cs` | The round hub's curtain wall and towers, the **duelling circle** (PvP encounter + countdown zone) and the health / mana orbs |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Tavern.cs` | The round **Tavern** landmark (the hub's Crystal Tap, 45 m across): a tall stone drum with pilasters, a string course and cornice, the grand arched entrance on the south with steps, lanterns and the sign, school banners round the outside, a low roof ring up to a clerestory drum and a cone roof with an oculus over the Starfall Crystal, sit-able seats (`Seat`: VRCStation), synced mug pickups and the bell that resets them (`MugReset`) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.SpellStyles.cs` | Each attack spell's recipe (how it travels, how it lands, its wind-up, film style, bolt count, shape, handedness and arc), checked so no two travel and land alike |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.SpellCast.cs` | Builds the rune board and casting wand, the choreography rig (rune, rings, column, bolts, beam, portals, bursts, spikes, stamp, light) re-coloured per school at runtime, the battle aura (two flame-shaped shells wearing `Shaders/AuraFlame.shader`, its flames and wind streaks), and the fade sphere; ring art by `Tools/make_spell_ring.py`, sounds by `Tools/make_spell_cast_sounds.py` |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.TavernHall.cs` | The Crystal Tap's hall (after the Aureion Collegium concept), a radial tower round the crystal: the **core** (a round bar ring round the fountain with stools, taps, bottles and kegs; a lounge ring of couches, armchairs and tables on a round velvet rug; the **theatre**, a four-sided screen hung under the crystal with the tavern's `TavernJukebox` and its controls on a lectern in the entrance hall), open up to the oculus; **seven school wedges** behind stone arches with lanterns (Ice's crystal study, Fire's hearth lounge, Balance's concord with the mirror toggle `LocalToggle`, Myth's archive, Death's reliquary, Life's garden, Storm's arcane lounge), each with couches by its arch, and the entrance hall in the eighth; a **gallery** over the wedges reached by a **double stair** to a landing at the Balance arch, with a railing and the school banners on the wall over the rooms; **ten rooms** off it, each with a bed, a desk and chair, a window with sunlight streaming in and its **own screen and video player** (loads only while you're near, heard only in the room, Stop button); a ring chandelier, and **sunbeams** through the roof and windows (`Tools/make_light_shaft.py`) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Terrain.cs` | Zone ground from the map + **height map**, as merged per-chunk meshes: every cell a column to its height with stone walls down to lower neighbours, cobbles / lawns / pond beds, optional **rounded corners** (outer corners cut, inner corners filled, path-lawn corners curved), kerbs, copings, iron **railings** (drops, ponds, stair handrails, optional path edges), stairs with stringers, water, waterfalls |
| `Assets/WizardsVRC/Shaders/PSXSkyline.shader` | Skybox: gradient sky plus a pixel-art skyline strip round the horizon (the other areas where they lie, far isles, sea, clouds), 15-bit dithered |
| `Assets/WizardsVRC/Shaders/PSXSurf.shader` | Surf on a ring round an island: churning foam at the rocks and breaking wave lines rolling in |
| `Assets/WizardsVRC/Shaders/PSXEyelids.shader` | The opening's eyelids, on an inside-out sphere that follows your head: lids closing from top and bottom, vignette, dream tint and white flash |
| `Tools/make_intro_audio.py` | Generates the opening's placeholder sounds in `Audio/Intro` (drone, wordless whispers, war drums, vision flash, heartbeat). Replace any with a real recording of the same name |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.Travel.cs`, `.Sky.cs` | Tunnels, mountains, the sea's swell and surf; the world's layout and each area's skyline (painted at build time) |
| `Assets/WizardsVRC/Shaders/PSXBillboard.shader` | Pixel labels that face each player's camera (GPU only, VR-safe: faces the point between the eyes), stay upright, vertex-snap and dither out with distance |
| `Assets/WizardsVRC/Fonts/PsxNameFont.txt` | The PSX pixel font for NPC name tags, hint lines and quest markers, as editable `#`/`.` glyph grids (7 rows, or 9 with descenders). The builder bakes each label into `Materials/NameTags/` on rebuild (`WizardsVRCSceneBuilder.NameTags.cs`) |
| `Assets/WizardsVRC/Fonts/PsxPixel.ttf` | The same pixel font as a real font, used by every word in a battle circle and on every spell card (card faces, which use its school and effect icons U+E000-U+E006 and U+E010-U+E01B, the HUD, overhead names, the banner, floating numbers). Generated from `PsxNameFont.txt` by `python3 Tools/make_pixel_font.py` (needs `pip install fonttools`); re-run it after adding or changing glyphs, then rebuild. The circle's overhead names, banner, clock words, floating numbers and effect icons billboard to face you, turning only about the vertical axis so they never tilt |
| `Assets/WizardsVRC/Scripts/Wanderer.cs` | Patrolling monster: walks a route (loop or line) from the server clock, starts the fight on touch; its name tag (name, level, rank, school) billboards upright towards each player |

## Scene

- `VRCWorld` — `VRCSceneDescriptor`: spawn = the Headmaster's study (`OfficeSpawn`; the title screen moves you to its
  stage a moment after you join), reference camera = `VRCWorld/ReferenceCamera`.
  (There is no `VRC_SceneLightmapSettings` component in the current SDK; lighting uses normal Unity Lighting settings.)
- `TitleStage` (far off at 2600, 1400, 2600) — the title screen's dais, sky, wordmark and menu, and `SelectCanvas`: the
  character select's world-space canvas (`VRCUiShape`) with 4 slot panels and the delete confirmation. Its script
  (`CharacterSelectUI`) sits on `Systems/CharacterSelect`, which is always on; the stage is switched off when you leave it.
- `Hub` (origin) — **Starfall Castle**, built from the hub zone map and height map (`WorldData/Zone_StarfallCastle.asset`),
  laid out in rings like Ravenwood on a round island inside a round curtain wall with towers, in a PSX sea. In the middle
  the Starfall fountain stands inside the Crystal Tap (a round walk-in tavern to hang out in; under its open roof the crystal floats high over the theatre, with a beam of light shooting from it into the sky) in a sunken round plaza
  with the arrival point; round it a ring of gardens and the inner ring road, then the seven school halls and the
  Library on a circle, each turned to face the centre (a walk-in classroom and professor in every hall), with four
  avenues between them; then the outer ring road and the outer quarter: the Headmaster's tower (door to the office) on
  a north terrace with a waterfall pouring into a pond, the observatory hill, the Training Grounds (practice ring) and
  the boss arena pad, the **duelling circle** (PvP), the quest-locked tunnels to Sunpetal Gardens (end of the south avenue)
  and Lantern Row (end of the west avenue) through gatehouses into snowy mountains, the inn,
  bakery and gazebo garden, and the Bazaar and shops (hats, wands, potions, pets, cards). **Red and blue orbs** drift
  round the ring roads (and every street) and restore health and mana when you walk into them. It also has the notice board ("Change Character" /
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

**Places** are kept beside the character rather than in it (so the format above is unchanged): `place_N` is where slot
N's character last stood and `mark_N` their marked location, each `x|y|z|yaw` in whole centimetres and degrees. The last
place is written every 5 s while you stand somewhere you could come back to (`LocationKeeper`); both are cleared when a
slot is deleted or a new character is made in it.

## Maps

The book's **Map** bookmark has a map of every zone and every world, drawn on every build from the zones' own text maps
(`Editor/WizardsVRCSceneBuilder.Maps.cs`, `MapCompose.cs`, `MapCanvas.cs`; saved to `Textures/Maps/Generated`, which git
ignores), so they always match the world data. The look is a hand-made storybook map:

- **Zone maps:** the zone as a little isometric model on old parchment (`Textures/Maps/MapParchment.png`) with a soft
  shadow and an ink outline: lawns and roads in the zone's colours (brightened), houses with hip roofs and lit windows,
  low walls and hedges, round trees (cacti in Dustwind), water with waterfalls pouring off the edge, terraces with earthy
  sides and the island's soil underneath. Landmarks get their own buildings: school halls roofed in their school's
  colour, the Headmaster's Tower and other towers, the library, shops, saloons, the round tavern with its cone roof, the
  fountain and its crystal, duelling circles; a castle hub's walls are grey stone. Every NPC has a badge (a gold ring round
  their robe colour, with their hat). Yellow arrows run from parchment tags to the way home, the gates to other zones and
  the main landmarks; the tags are buttons in the book, and an exit's ("Kiln Street >") opens that zone's map. Marker
  doodles that suit the zone fill the margins: little wizards, ghosts, rats, bats, a troll, a plant monster, a crab, a
  fire imp, an owl, a skeleton, a cactus, a tumbleweed, a lantern ghost, music notes, question marks.
- **World maps:** one per world, on a starry sky (`MapSky1.png`, `MapSky2.png`): the hub (or the world's town) in the
  middle and each of the world's zones as its own floating island with jagged rock underneath, placed the way its
  tunnel leads, joined by dotted golden paths, with a tag per island (zone name and level range) that opens its map.
- **You are here:** a red marker on your zone's map where you stand, turned to your heading (it follows you while the
  map is open), and on your island on the world map.
- The art (parchment, skies, the wooden frame, tags, title scroll, marker and doodles) is all original, made by
  `python3 Tools/make_map_art.py Assets/WizardsVRC/Textures/Maps`.

## The book

**B** (desktop), the hub button or the backpack pedestal opens it. Bookmarks: **Pack** (equipment and items),
**Deck** (decks and spells, shown as the same spell cards as in battle), **Quests**, **Map** (see *Maps* below), and **Settings** (HUD and quest-arrow toggles, **VR HUD** (in view or over your left hand), Save Now, Return to Starfall
Castle, music volume, **Wand hand** (which hip the casting wand waits at in VR, remembered), and Change Character, which saves, remembers where you stand and takes you back to the character select on the title stage). All of the book's text uses the pixel
font, like the signs and the duel ring. The book opens 1.2x bigger in VR (`InventoryBook.vrScale`; 1.08x on a screen,
`desktopScale`), and the pop-up details grow with it.

On the **Quests** page, **Abandon** (click it twice to confirm) wipes an active quest's progress from your save, so it
counts as never started: its quest giver offers it again (givers check your saved quests), quest objects it used come
back, and a story quest that starts by itself starts over from its first step. Quests only reward you when finished, so
nothing is taken back.

## Shops, Starshards, titles and auras

- **Gold shops:** every shop in the castle has a counter out front. Click it and its wares open in front of you (gear,
  potions, decks). Hovering a row shows the item beside the piece you're wearing in that slot, with the stat changes
  in green and red (`GoldShop`, `ItemTooltip`).
- **Starshards** are the rare currency: 3 per boss, 1 per elite, 5 per boss tower (double the first time) and 8 for the
  Kraken. Spend them in the book's **Starshard Shop** tab (`ShopPages`) on elixirs (timed stat boosts that tick down
  while you play and never apply in PvP duels), titles, auras and spell skins (your casting glyph and spell flashes in
  that colour). Before you buy, the shop **tries the item on you**: its preview frame films your own avatar (VRChat's
  mirror copy, as the book's portrait does) wearing the real aura, the title over your head, or your casting symbol
  drawn in the skin's colour, with the camera swinging gently round you. A Surge aura plays a duel on loop (pips
  building, a cast, the full-pips eruption). It's local to you, on the MirrorReflection layer, and only runs while the
  shop is open on something you can try on (`ShopPreview`).
- **Titles** show over your head and **auras** swirl round you, in the open world and in duels, seen by everyone
  (`PlayerCosmetics`, synced through a VRChat player object). Earn them by level, monsters, bosses, boss towers and the
  Kraken, or buy them. Pick what you wear in the book's **Wardrobe** tab (`WardrobePages`).
- **Selling Starshards for real money** is optional and goes through VRChat's Creator Economy (`StarshardStore`). You
  need Creator Economy access and Tilia authorization (see below). Make each product on the VRChat creator site, put
  its `UdonProduct` asset in **`Assets/WizardsVRC/Store`**, and rebuild: the builder fills the `StarshardStore` from that
  folder every time, so nothing is lost.
  - an asset whose name has a number in it (`Shards_500`) is a **Starshard bundle** of that many: every purchase adds
    them (times the quantity bought) to the wizard you're playing. Sell bundles as **Instant** listings with quantity
    purchases on, so they can be bought again and again. Shards bought with no wizard loaded wait until you pick one;
  - any other asset (`Patron`) is a **supporter perk**: owning it unlocks the cosmetics that list it
    (`CosmeticsDatabase.UNLOCK_PRODUCT`, e.g. the *Patron of Starfall* title and *Patron's Radiance* aura, which go
    with the first perk by name) on every wizard. Sell perks as **Permanent** listings.

  The shop's **Get Shards** button (it appears once there's a product) opens the world's store page in VRChat.
  Setting it up, step by step:
  1. Apply for Creator Economy access, then get authorized by Tilia (vrchat.com > Marketplace > Storefront > Get
     Started with Tilia; usually about 5 business days).
  2. Storefront > My Products > **Create Udon Product** for each bundle ("500 Starshards") and perk.
  3. In Unity: VRChat SDK > **UdonProducts Manager** (or Assets > Create > VRChat > UdonProduct and paste the product's
     ID) and save each asset in `Assets/WizardsVRC/Store`, named like `Shards_500` (bundles) or `Patron` (perks).
  4. On the site, make a **listing** per bundle: type **Instant**, **Enable Quantity Purchases**, add the bundle's
     Udon product, price it in Credits (100 to 10,000 for instant listings), and in its visibility step add it to
     this world's store; publish it. Perks: a **Permanent** listing.
  5. Rebuild, then test: the UdonProducts Manager can buy and expire products in ClientSim, and in Build & Test (or as
     the world's owner) your own listings cost nothing and local test purchases expire after 60 seconds.
  With no products set up, the shop simply runs on earned Starshards.
- **Quest and exploring sounds** (`Tools/make_quest_sounds.py`, original synthesis): a soft ding as each objective
  counts, a bright rising chime when a quest step is done and a short horn fanfare when the quest is complete
  (`QuestManager.uiAudio`). Walking into a street, town, boss tower or secret place for the first time
  (`AreaDiscovery`) plays a harp-like chime, shows "Discovered: (place)" and gives a little XP (20 + 10 per level);
  each wizard's discoveries are saved (save format v12), so each place counts once.
- **Notices:** the book's **Notices** tab (`NotificationCenter`) lists new loot and gear, professors' messages when a
  new spell unlocks at your level, newly available quests and tower clears, with an unread badge on the HUD and the
  tab and a one-line toast.

## The start of the game

**The title screen** (`TitleScreen`, `WizardsVRCSceneBuilder.Title.cs`): every player arrives on a dais of starlit stone
floating over a sea of moonlit clouds under the aurora. Ahead hangs the WIZARDS VRC wordmark (gold pixel letters with a
violet extrude, a wizard's hat tipped on the I, VRC on a ribbon), bobbing gently in a breathing violet glow with sparkles
winking round it, over the line "Seven schools. One spellbook. Your story." The seven schools' symbols float in an arc
around it and draw themselves stroke by stroke, each in its own style, flare, hold and fade before drawing again. Beyond,
Starfall Isle's castle rides its floating island with the crystal turning on top, between two smaller isles; a rune
circle turns under your feet, shooting stars streak across the sky and golden motes drift past. The menu below reads
"Opening your spellbook..." until your saves have loaded, then **Begin** lights up. For a new player, your eyes close,
you're taken to the Headmaster's study and the opening plays. For a returning player the **character select** opens right
there on the stage in the menu's place (the panel that used to hang in the study): **Select** fades the whole scene out
slowly, picture, music and ambience together (`SceneFade`), and fades it back in **where that character last logged
off**; **New Character** closes your eyes and plays the opening for that slot; Delete works in place. Change Character (the
book's settings) brings you back to the same panel. The title stage switches itself off whenever you're not on it. It
all animates itself (Animators, the GlyphDraw shader, particles). Art: `Tools/make_title_art.py` (Textures/Title: the
wordmark and the sky panorama); music: the character creation theme, credited under the menu.

**Where you last logged off** (`LocationKeeper`): every 5 seconds, while you stand on the ground somewhere you could
come back to (the castle grounds, the keep, any street or town; not in a duel, the opening, a dungeon, a boss's tower, a
secret boss's place or the arena), your spot is remembered for that character. Choosing the character puts you back on
it, facing the same way. Before you land, the spot is checked (there's ground under it and nothing solid where you'd
stand), so if a street has been rebuilt since and the spot is now inside a wall, you arrive in the castle instead; a new
character, or one saved before this existed, arrives in the castle too.

Like Wizard101, new wizards don't meet a real monster until they know the basics. After choosing a school in the
Headmaster's office you find your professor at their school hall, report back, then take the castle tour: Gatekeeper
Bramwell (exploring the grounds), Quartermaster Pell (backpack, gear, potions), Librarian Wren (cards, pips, accuracy,
decks) and Duel Master Holloway (a practice duel against training dummies that never attack). Only then does the
**Sunpetal Gardens** tunnel open (Lv 1-3 weeds, newts and moths, boss Old Briarwick, who only heals with Mend so new wizards can beat him). Fights there (and on the practice
ring) are **even fights**: one monster per wizard, starting with the one you touched; a second wizard brings in the next
patrol monster to reach the circle, and other patrol monsters that wander into the circle fade away. Everywhere else a
street fight also starts with only the monster(s) you ran into, and patrol monsters that wander into the circle take a
free seat (up to the encounter's roster size plus one per extra wizard, four at most) and join from the next round,
but only while none of the fight's monsters has been defeated: a defeated monster is never replaced. Tower floors cap a
solo wizard at two monsters, plus one per extra wizard.
Bosses fight a solo wizard alone and bring one follower per extra wizard. Finishing the gardens opens the
**Lantern Row** tunnel (now Lv 4-6: rank 1 wisps and snatchers, never more than two to a fight) and the boss arena pad. The locks are the Street Gates' *Required Quest* in the hub map.

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
a round chamber with five arches. Starfall Isle's always glows; World 2's lights up once you finish quest 16 and takes
you through to **Archrock**, Dustwind's town (the sandstone arch at its south end brings you back); World 3's lights up
after quest 77 and takes you down to **Bigdig**; the other two stay dark.

**Mark Location and Teleport to Mark** (`LocationKeeper`; the two blue buttons along the bottom of the book's **Map**
page, or **Page Down** and **End** on a keyboard): marking remembers the spot you stand on, for **10% of your maximum
mana** (`markCostPercent`; you're told if you haven't enough), anywhere your place can be remembered. A ring of blue runes
with a small crystal turning over it glows there (only you see yours) and a blue pin shows it on that zone's map, or on
the zone's island on the world map. Teleporting to it is free and works from anywhere outside a duel: a quick fade out
and in. You have one mark per character; it stays where it is until you mark somewhere else, and it's saved with the
character. A mark whose spot no longer exists (the street was rebuilt) is dropped when you try to go to it, and you stay
where you are.

## The Starfall Ward

A great dome of light covers **World 1**, raised by the Starfall Crystal: one dome for the whole world, centred on the
crystal in the castle and big enough to take in every street (its radius is the farthest street's distance plus 420 m).
In the castle its crown is straight overhead; out on a street it stands off over the castle, its wall sweeping down out
of the sky behind you, and the crystal's beam of light climbs from the castle to the crown, so you can always see what is
keeping the isles safe. The `WizardsVRC/WardDome` shader draws it on a hemisphere that follows the camera just inside the
draw distance; each pixel traces its view ray out to the real dome where it stands relative to that place in the world's
layout (the same layout the skylines use), so it can be any size and is never clipped or fogged. It's animated on the GPU:
a lattice of glowing hexagons whose colour drifts through blue, violet and gold, aurora curtains rippling round its lower
reaches, starlight twinkling in the cells, cells that flare now and then, a pulse spreading out from the crown every 9
seconds (from a street, rolling towards you from the castle), motes of light streaming up the crystal's beam, and a ring
of turning runes and a glowing seam where it meets the ground. Each World 1 place has its own material with the dome's
centre and radius (`Materials/StarfallWard_<zone>.mat`; builder: `WizardsVRCSceneBuilder.Ward.cs`).

**Story.** The Starfall Crystal raised the ward over the isles long ago, and nothing from beyond the Everweave has ever
reached them since; the Headmaster tells you so on waking, and that the ward drinks from the crystal ("if one fails, so
will the other"). The arc it carries:
- **World 1**: there, taken for granted by everyone, overhead wherever you go.
- **World 3**: Bigdig, the kobolds' homeland, where the Dominion is quietly draining the Mother Lode while the kobold
  gangs feud. Bigdig's own ward died the same way; Starfall's cracks as World 3's storylines finish.
- **World 3's finale**: the siege of Starfall. The Threadfleet anchors over the isle, the ward falls and the Grand
  General fights for the portal room. (The full plan is in the doc *The Everweave: Main Story Plan*.)
The shader's **Crack** (`_Crack`, 0 whole to 1 failing) is there for those chapters: cells break along their edges, red
fractures spread, the light gutters and turns red. Set it on the material (or from a script for a story beat).

## World 2: Dustwind (Lv 13-25)

A Wild West world (see the World 2 plan): the Dominion's Railmaster, **Ironhand Calloway**, is laying the Iron Line across
Dustwind to drive its last spike into the **Heartstone** under Thunder Mesa. It's all in the same VRChat world as
Starfall Isle (saves can't cross worlds); every Dustwind area is switched off for anyone not in it.

**Archrock** is the town (it plays the castle's part): Marshal Ada Quickwater's office and the bounty board on High
Street, the **Dusty Lantern Saloon** (a two-storey timber saloon with everything the Crystal Tap has: the jukebox, here a
player piano whose keys move while it plays, the mug bell, seats, the mirror and a pot-belly stove), four shops selling
Dustwind's gear (Lv 13+), the **High Noon Circle**, the corral's straw dummies, the depot and six trails out of town:

| Street | Lv | Boss | Storyline (save ids) |
| --- | --- | --- | --- |
| Tumbleweed Row | 13-14 | Bad Penny | The Stagecoach Robberies (44-47) |
| Coyote Gulch | 14-16 | Old Mesquite | Bone Dry (48-51) |
| Copperbottom Mine | 16-18 | Foreman Grist (the mine is a dungeon) | Light in the Dark (52-55) |
| Iron Line Railyard | 18-20 | Captain Vesper and the Juggernaut | Full Steam (56-59) |
| Thunder Mesa | 20-22 | Stone Warden Prime | The Wardens' Trust (60-63) |
| Lonesome Bend | 22-25 | Railmaster Calloway, in the drill fortress (a dungeon) | Ghosts of Lonesome Bend (64-67) |

Ten storylines (with Deputized 41-43 in town, Water for Archrock 68-70, Wanted 71-73 and The Missing Songbook 74-75) lead
to the finale, *Last Spike* (76-77). Seven school side quests (78-84) each teach a utility spell early, and **the Midnight
Express** (quest 85, Old Silas at Lonesome Bend from level 22) is the secret boss: ring the old station's bell and duel it
on a flatbed car with the desert rushing past (the *Ghost Rider* title). Monsters are ids 28-53.

- **Railroad:** walk past a street's whistle stop (by its way in) once, then ride the train there from Archrock's depot
  (and back from any stop): a few seconds in a passenger car with the desert going by, then you step off.
- **Bounty board** (from level 17): three wanted posters at a time; battles count toward them anywhere, turn them in at
  the board for gold and Starshards (the Bounty Hunter, Lawman and Legend of the West titles at 3, 10 and 25).
- **High Noon:** Archrock's day runs on the server clock (20 minutes); for 3 minutes round each noon, duels won in the
  High Noon Circle pay +5 Starshards (the High Noon Duelist title at 5 wins).
- **Dust storms:** every 9 minutes a storm blows through each street in turn for about a minute (orange fog, blowing
  sand). Everyone on the street sees the same storm. It's only a look.

Spell levels were moved so Dustwind has new spells to learn: each school's rank 4 attack at **18**, its shield at
**20**, its second rank 4 spell at **22** and its rank 5 attack at **25** (they were 26, 22, 28 and 42).


**Dustwind's people** are mostly reptiles: about six in seven residents are lizardfolk (iguana, gecko, frilled, horned
or crocodile), old tortoises or the odd kobold, in desert colours with bright crests, keeping the clothes, hats and
tools their looks give them (`WorldTwoReptile` in `WizardsVRCSceneBuilder.NpcLooks.cs`; the new species are in
`WizardsVRCSceneBuilder.Npcs.cs`).

**Kobold dig crews** from Bigdig (World 3, the kobolds' homeland underground) are prospecting in four World 1 streets
(Lantern Row, Tidewatch Docks, Kiln Street, Thornhollow Grove) and five in Dustwind (Tumbleweed Row, Coyote Gulch,
Copperbottom Mine, Thunder Mesa, Lonesome Bend): a claim against a wall with a dig hole, spoil heap, ore cart of gold,
gold pan, lantern and company sign, a foreman and a digger or two, all fast-talking, money-mad hustlers you can talk to. What the foremen say hints at
World 3: Bigdig's singing gold has gone quiet, the elders say it and the Starfall Crystal were struck from the same
star, someone is buying it all up, and there's an old kobold door below Copperbottom Mine locked from the other side.
The crews are added by the builder (`WizardsVRCSceneBuilder.Kobolds.cs`), each on an open spot clear of the duel rings.

## World 3: Bigdig (Lv 25-35)

The kobolds' homeland: one vast cavern under Dustwind, its boroughs stacked up the rock in rusty corrugated iron,
riveted plate and brass, lit by furnaces and neon. The Dominion is quietly siphoning Bigdig's **Mother Lode** (the
singing gold struck from the same star as the Starfall Crystal), while the kobold gangs are too busy feuding to notice.
The portal room's third arch opens once you finish *Last Spike* (quest 77) and brings you to **Bigdig**, the town.

**Bigdig** is laid out like a Wizard101 hub: a star of narrow, winding lanes lined with crooked buildings, all meeting
at **Lodestone Square**, the market in the middle. The square has the Deep Bell, Deepwarden Bruma Stonehum, the
assayer's stall and four shops selling Bigdig's gear (Lv 25+). The west lane runs to the portal arch. The **Gilded
Rivet Hotel** stands on the west lane: a walk-in, three-storey lobby atrium with a marquee canopy and neon signs. Inside
are the front desk (Mags, the manager) with its bell, a lounge bar with the mirror, a fireplace with armchairs, the
jukebox, galleries of numbered room doors and a cage lift that's out of order (`LandmarkKind.Hotel`,
`WizardsVRCSceneBuilder.Hotel.cs`). A short lane south-east leads to the **Brawl Pit** duelling circle and the training
yard. Six lanes wind out to the boroughs' tunnels.

**One open world.** Bigdig and its six boroughs stand together in one vast cavern (the town's **Open World** setting,
`WizardsVRCSceneBuilder.OpenWorld.cs`). Each borough is placed just past its tunnel, its own entrance facing the town
across 50 m of rock. Highroller Heights stands 45 m up on a plateau to the north, and the Deep Seams lie 30 m down to the
south-west. Between the districts there's no sea: there's a chasm with glowing sludge, a rock bridge over each pair of
linked tunnels, and the lower city of shanty towers on stilts. One cavern roof covers everything, with stalactites,
lanterns and rock pillars. The tunnels are still the load zones: walk through one and you're carried to the far end.
The districts round you stay switched on, so you see the rest of the city (`AreaCuller.viewMargin`; rings in the other
districts sleep until you're in them). The quest arrow and the book's maps tell districts apart by where they lie
(`QuestGuide.areaMinXZ`, `MapPages.mapMinXZ`). The Golden Seam Casino looks out over the whole city from Highroller's
cliff: a gold facade with its name in lights, a tower on a rock buttress, and searchlights raking the roof.

Every borough has its own layout:

| Street (where) | Lv | Layout | Gang you help; boss | Storyline (save ids) |
| --- | --- | --- | --- | --- |
| Sprocket Row (E) | 25-27 | the bazaar road winding east, south, east and south to Counting House Court; forks to the counterfeiters' yard and the back alley | the Company; Copperjaw Grubb | Company Business (133-136) |
| The Rustworks (S) | 27-28 | Gate Road, Crucible Lane and the conveyor line down to the Drill Pit; forks to the union hall and the smokestack yard | the Union; Drillmaster Vane | Union Strong (137-140) |
| The Fusewick Canals (NW, lower) | 28-30 | Low Wharf and Tanner's Quay, over the Grand Canal and the Sludge Cut to the canal mouth; forks to the runners' den and the locks | the Syndicate; Lieutenant Sorrel | Smuggler's Run (141-144) |
| The Glimmer Mile (NE) | 30-31 | the Low Mile, the Arcade Stair, the Middle Mile and the Marquee Climb up three terraces to the Top Terrace; forks to the arcade alley and the rooftops | the Glimmerfangs; Agent Nightglass | Neon Nights (145-148) |
| The Deep Seams (SW, 30 m down) | 31-33 | galleries stepping down past the miners' camp and the old seam to the Siphon Works (a dungeon) | Chief Engineer Strake | The Deep Seams (149-152) |
| Highroller Heights (N, 45 m up) | 33-35 | the cab rank, the Boulevard and the High Strip to the casino's forecourt (the casino is a dungeon); a fork down the West Strip to its back door | the Assayer and Marshal Brannoch | Highroller Heights (157-159) |

Ten storylines (with Under Everything 130-132 in town, **The Parley** 153-156, Odd Jobs 162-164 and Lucky Streak
165-166) lead to the finale, *Ante Up* and *All In* (160-161): every gang storming the casino together. **The Parley** is
where the kobolds wise up: the gang bosses, called to a moot, argue it out in scenes with several voices (lines written
`Name|words`, `!Name|words` for an enemy) until they shake on it. Monsters are ids 60-88: kobold gang toughs and the
Dominion's Tech-school troops; items are 200-263 (`Tools/make_items.py`).

- **Bosses who speak first:** a zone encounter with **Intro Lines** plays a cutscene as you walk up to its ring, or for a
  street boss as you arrive in its tower (a wide
  shot, then close on the boss; `EncounterIntro`, builder `WizardsVRCSceneBuilder.Intros.cs`). Strake, the Assayer and
  Marshal Brannoch all have one.
- **The look** (`WizardsVRCSceneBuilder.Undercity.cs`, texture style **Undercity**): riveted deck-plate streets, oily grit
  lawns, buildings with a shopfront, an overhanging upper storey and a crooked top, water tanks and smokestacks on the
  roofs, porthole windows, striped awnings, brass pipes and neon blade signs; stalagmites and pipe stacks where a map
  has trees. Past the map there's no sea: each street stands on a rock shelf over a chasm of glowing sludge, its
  neighbourhood climbing away in shanty towers, cranes, gear towers and neon billboards, rock pillars with shanties
  clinging to them, and the cavern's walls and roof over it all, hung with stalactites, chains and lanterns. Points can
  be neon signs, blasting barrels, heaps of gold, slot machines, card tables and roulette wheels; the casino gets red
  carpet, velvet walls with gold pilasters and chandeliers, the Siphon Works pipe runs and warning lamps.
- **The sky underground** (`WizardsVRCSceneBuilder.CaveSky.cs`): every World 3 place has no sky at all, only the
  cavern. Its panorama runs from far below the horizon to high overhead: the cavern roof hung with stalactites, chains,
  lanterns and glow-worm colonies, lit warm from below by the city; the city sprawling away on every side, tier on tier
  of lit buildings, neon, smokestacks and catwalks climbing the far walls, and below the horizon the lower city's
  rooftops, lamplit streets and sludge canals going down into the dark; the other districts where they really lie (the
  Deep Bell over Bigdig's square, the Rustworks' smokestacks, the Glimmer Mile's neon towers, the Fusewick cranes, the
  Deep Seams' drill derrick); and, in the direction it really stands, the city climbing to a summit crowned by the
  Golden Seam Casino, its tower, dome and spire, its neon and its searchlights raking the cavern roof.
- **The finale on the casino roof:** Marshal Brannoch, the last fight of *All In*, waits on the roof of the Golden Seam,
  the very top of Bigdig, with the whole city spread out below (an encounter with **Arena** `Rooftop`,
  `WizardsVRCSceneBuilder.CasinoRoof.cs`). A brass cage lift in the high-roller vault takes the party up: a gold-trimmed
  deck with a roulette inlay round the duel ring, a parapet of marquee lights, the casino's tower, dome and spire behind
  him, searchlights, and the building falling away in neon-ringed tiers. He has his say as you arrive; win and the lift
  brings you back down to the vault, the lever and the Mother Lode. The roof counts as inside the dungeon for the party.
- **Bigdig's people** are kobolds (named looks for the gang bosses; `WorldThreeKobold` in `WizardsVRCSceneBuilder.NpcLooks.cs`).

The siege of Starfall, when the ward falls, is still to come as World 3's last chapter (see *The Everweave: Main Story
Plan*).

## Duels (PvP)

The circle's sign (on a carved board) turns to face you, staying upright, and shows the circle's name with its countdown.

Step into the **duelling circle** (south-east of the hub) and a 15 second countdown starts; anyone else who steps in
before it ends joins. When it reaches zero everyone inside sits down in the circle, dealt alternately into the **Blue
team** (the near half) and the **Red team** (the far half), up to 4 a side. Alone in the circle, the countdown waits
for an opponent. Duels use the normal combat (cards, pips, shields, blades, minions), everyone starts at full health,
spells cost no mana, and nothing is won or lost: afterwards your health and mana are just as they were. Knocked-out
wizards stay in their seat to watch; the duel ends when a team has nobody standing (or leaves). Fleeing is free.

## Monsters, spell animations and card art

**Monsters** are animated rigs built from primitives: bipeds (with hats, helmets, crowns, horns, ears, bones or a glowing
core by type), four-legged beasts (tusks, pincers, a scorpion's stinger), floaters (wisps, sprites, moths, hawks with
wings and tails), walking plants, and the special bodies (the Kraken, the Juggernaut, the ghost train, tumbleweeds and
cacti). Each kind has keyframed **Idle, Walk, Hit, Die and Cast** clips: patrols walk, and in the ring a monster rears up
or raises its arms as it casts, flinches when hit and falls when beaten, with a voice for each (`Tools/make_creature_sounds.py`:
biped, beast, spirit, plant, construct, machine and deep voices, pitched by size). The rigs, clips and controllers are
generated into `Assets/WizardsVRC/Generated/Rigs` on every rebuild (one prefab per monster).

**Spell animations.** Attack spells, heals and the dispel are staged in six beats (below); a charm, shield or trap
simply appears round its target with a sound. A prefab in
`SpellAnimations/<id>.prefab` or `<Spell Name>.prefab` still replaces a spell's animation.

**Spells are staged by one formula** (`SPELL_ANIMATIONS.md`, which also sets out the Wizard101 research behind it).
Every attack spell, heal and dispel goes through the same six beats, whoever casts it (a wizard, a monster or a boss),
and the **display is the long beat: 5 seconds for every spell**. A whole turn runs from about 13 s at 1 pip to 17 s at
7, plus up to 2 s while the effects it uses go (the old 14-second ceiling could not stand with a 5-second display; the
numbers to bring it back down are in `SPELL_ANIMATIONS.md`).

- **Cast.** The caster appears in the middle of the ring and powers up in a **battle aura**. Sparks stream in to
  them, then the aura catches with a thump: a flame-shaped shell of light in the school's colour (white-hot at its
  root, its outline licking and flickering: `Shaders/AuraFlame.shader`), tongues of flame racing up inside it, a ring
  and a spray of sparks blasting out along the floor. It burns taller and wilder as the charge builds, swells with
  every beat, flares to half as big again in the last fifth, and bursts outward on the release; a thin glow of it
  stays on the caster until their spell lands. It is sized by the pip cost (about 2.6 m round a wizard at 1 pip,
  nearly 6 m at 7) and the tiers add to it. Tier 1 (1-2 pips): the aura, a soft beat of rings, a little grit lifting,
  the rune on the floor, the spell's flourish, a rising hum. Tier 2 (3-4): streaks of wind rush up round it, more
  grit, sparks race round the rune's rim, a quickening heartbeat of rings. Tier 3 (5-6): a second, paler shell
  outside the first, everything but the magic goes dark (`Shaders/DuskVeil.shader`), sparks spiral up round the
  caster, power crackles out of them, a column of light, and it's all drawn in just before it goes. Tier 4 (7): the
  school's rune opens in the sky and pours light down into it.
- **Arrival.** The release: a shock off the caster, and the spell's subject arrives (a core of power over the caster, or
  a creature spell's creature beside them) the spell's own way: up out of the rune, down out of the sky, drawn in from
  all round, through a gate, or wound up out of a spiral of sparks.
- **Display.** The long beat: 5 seconds for every spell (`CardDatabase.cineDisplaySeconds`), in three parts. It opens
  (a creature rears, the hum starts to climb again); it builds through about ten beats that come quicker and quicker,
  each one swelling the spell's subject and jolting the film; and it peaks with a ring of light, a spray of sparks, a
  boom and a flash as the caster's aura jumps. Each beat does the spell's own thing: a ring of sparks and light bigger
  each time, a ring spreading while sparks circle it faster in more strands, the next bolt of a crown flaring (a crown
  of bolts fans out behind the caster and hangs there swaying), a bolt striking round it as it crackles ever more
  thickly, or a ring of light climbing a column of light with sparks spiralling up it. The film gives it three shots:
  round the caster, close under the subject, then back and high behind the caster with the target beyond.
- **Expend.** Every blade, trap and shield the cast uses goes now, one at a time (blades, then traps, then shields): it
  lifts out of its orbit flickering white, holds white, then shatters into shards with its sound and its bonus floating
  up. 0.4 s each, 2 s at most.
- **Attack.** The spell's own delivery. A spell of 5 pips or more strikes twice: a lighter wave shed from the core
  (the target flinches), then the spell itself.
- **Result.** The hit: the spell's own impact and its numbers, on every target at once for an area spell.
- **Exit.** The power leaves the spell's own way (embers, blown apart, a column of sparks into the sky, sinking into
  the floor, a last pair of rings) and the caster goes back to their seat.

**Cost is size.** Everything a spell shows is sized from its pip cost on a curve that climbs faster than the cost (the
effects treat 1 to 7 pips as 1, 2, 4, 6, 8, 10, 13): at 7 pips the rune on the floor is 8 m across, the core over the
caster 2.2 m, the rune in the sky 23 m, and a column impact 22 m tall. From 3 pips the hit throws aftershocks round the
target (one, up to five at 7), from 4 each beat of the charge throws a ring of sparks, from 6 the release throws two.

**Charm, shield and trap spells have no animation.** The effect just appears round its target with a chime (pitched
high for a blade, lower for a trap, lowest for a shield) and the next caster goes about a second later
(`BattleManager.charmSeconds`). A heal is staged like an attack but lands gently on whoever it heals. A summoned minion
casts from its seat with no staging; minions being summoned and the duel-wide bubbles keep their own short animations.

`Tools/spell_staging_table.py` prints what each pip cost gets. Sounds: `Spell_Charge`, `Spell_Surge`, `Charm_Appear`
and `Charm_Shatter` (`Tools/make_spell_cast_sounds.py`).

**Every attack spell moves its own way** (`SpellCinematic`, recipes in `Editor/WizardsVRCSceneBuilder.SpellStyles.cs`,
packed into `CardDatabase.cineStyles`). Each recipe picks:

- how it **travels**: a lance, a corkscrew of 2-3 bolts, a fanned volley, a meteor that climbs out of sight and falls,
  a rain out of a ring in the sky, a runner racing under the floor, a beam, orbs circling the caster before they fly
  one by one, a jagged lightning path, a pair of portals (in at the hand, out high behind the target), a wave abreast,
  or a boomerang round the flank;
- how it **lands**: a burst, an implosion (energy drawn in, then out), a column of light, a cascade of rings, spikes
  out of the ground, a climbing vortex, a nova, a shatter of falling shards, or the school's rune stamped down;
- the **wind-up** flourish round the caster (energy streaming in, two rings turning on tilted axes, a fountain of embers,
  a double spiral of sparks, or a column of light), the **film** style (standard, low, crane, following the bolt, or
  from behind the target), and the bolts' **count, shape** (orb, shard, disc), **handedness** and **arc height**.

No two attack spells share both a delivery and an impact: the build logs an error if two do, and an attack spell missing
from the table gets the first free pair. A creature spell sends its creature along the finisher's path (it arrives
beside the caster, roars with each wave, flies or runs the route, strikes, is thrown back and breaks up); walkers and beasts keep to the ground on
lances, zigzags, runners, waves, boomerangs and beams, and leap meteors or go through portals. Everything scales with the
spell's pips: the rune on the floor, the bursts, the light, the rings, the shake. A monster casting an attack spell plays
the same choreography from its seat. Each school's starting spell is now cast from the wand rather than summoned:
**Ember Lance, Frost Shard, Spark Lash, Thorn Volley, Shade Siphon, Mirage Dart, Prism Ray** (same ids, so saved decks keep them).

**Card art** is rendered from each spell's animation on every rebuild (the creature mid-attack, the bolt, the rain,
the bloom) as a 64-pixel picture over the school's colour, into `Textures/CardArt`, and shows on the cards everywhere:
the battle hand, the deck, the spell trainers and new-spell pop-ups.

## Ambient sound

Under the music, every area has its own looping ambience: breeze and birdsong in the castle grounds, Sunpetal Gardens and
Lantern Row, waves and gulls at Tidewatch Docks, the forge's roar and anvils on Kiln Street, birds, leaves and a
woodpecker in Thornhollow Grove, cold wind at the Hollow Keep; in Dustwind, wind and creaking windmills in Archrock and
on Tumbleweed Row, dry desert wind and a hawk in Coyote Gulch, drips and rumbling in Copperbottom Mine, steam and clanks
in the railyard, thunder on Thunder Mesa and crickets and owls at Lonesome Bend. Tower chambers crackle with torches, the
Kraken's platform sounds like the deep, and the ghost train clatters. Hearths, the saloon's stove and waterfalls make
their own sound as you come near. The loops are generated by `Tools/make_ambient_sounds.py` (seamless, 20 s each) into
`Audio/Ambient`; each area's bed is on its root, so it only plays while you're there.

## Performance

Each player's client only runs what's near them: areas you aren't in (hub, boss arena, every zone) are switched off
locally, nothing past the fog is drawn, and small props are chunked so they stop drawing beyond each zone's Detail
Distance. See *Performance (culling)* in [WORLD_BUILDING.md](WORLD_BUILDING.md).

## How the streets are laid out

Every street in Worlds 1-3 follows the checklist in the *WizardsVRC Level Design Guide* (world data v32):

- One long road from the tunnel to the boss, bending four or five times and forking once or twice, that never loops back
  on itself or returns to the entrance: about 300-450 m (two to four minutes' walk).
- Fights on the road: each group's duel ring and patrol sit on the road's centre line, with a clear verge along the walls,
  and nothing within three cells of the entrance. Every quest NPC and object can be reached along the verges without
  crossing a patrol.
- About six to eight rings a street, one family of monsters, and each street's fog and sky a little darker than the one
  before it in its world.
- The boss behind a door at the far end: its tower (sealed until the story reaches it) is the street's landmark, seen
  from the entrance. The dungeon streets (the Hollow Keep, Copperbottom Mine, Lonesome Bend, the Deep Seams, Highroller
  Heights) end at their dungeon's sigil instead.
- Every side branch ends in something: a quest NPC or object, a minor boss or a treasure cache.

## Street dressing

Every street and town built from a map gets a last layer that makes it look lived in, laid by
`WizardsVRCSceneBuilder.Dressing.cs` at the end of `BuildZone` (pieces and kits in `...DressingProps.cs`, particles in
`...DressingFx.cs`). It is seeded by the zone's key, so a rebuild looks the same each time.

- **Against the walls:** each street's own kit of rounded props. Barrels, planters, sacks and woodpiles in Lantern Row;
  topiary, beehives and sunflowers in Sunpetal Gardens; rope coils, net floats and fish baskets at the docks; glazed
  jars, coal heaps and braziers in Kiln Street; rubble, candles and stone orbs in the keep; clay jars, cactus,
  tumbleweeds and wagon wheels in Dustwind; ore piles, crystals and stalagmites down the mine; oil drums, pressure
  tanks, steam vents, cogs and glowing toadstools in Bigdig. Bigger pieces (a handcart, a well, a kiln, an upturned
  boat, an ore cart, a boiler) go in the corners between two walls.
- **On the walls:** ivy, flower boxes under the windows, hanging baskets, round lanterns, life rings and nets, jar
  shelves, strings of peppers, crystals in the rock, brass gauges, pipe runs and bracket fungus.
- **Greenery:** grass tufts, flower clumps, bushes, toadstool rings and rocks on the lawns; flower borders along the
  paths; extra lumps on every tree's crown with roots and underplanting at its foot; rounded mounds along the hedge
  tops; reeds, lily pads, mooring posts, fenders and buoys on the water.
- **Across the street:** strings of paper lanterns, flower garlands, pennants or neon bulbs between buildings that face
  each other over a narrow street.
- **In the air:** chimney smoke, moths round the lamps, falling leaves and petals, butterflies, fireflies, pollen, sea
  mist, glints on water, soot and embers, wisps and ground fog, blowing sand, sifting dust and drips, stack smoke,
  steam, sparks, spores and bubbles. Sprites from `Tools/make_street_fx.py` (`Textures/StreetFx`).

It keeps out of the way: nothing on or beside the entrance, NPCs, quest points, landmarks, stairs, doorsteps, tunnels
or the tower sigil, nothing inside dungeons, and nothing where something already stands (it measures what has been
built). Within a duel ring's reach of a ring spot it only places low greenery and things on the walls. Larger props are
solid (a simple box or capsule); greenery and small things are not.

It is cheap to draw: the pieces are static and share a small set of materials, so the last pass merges them into a few
meshes per 32 m chunk, and all but the biggest stop drawing beyond the zone's Detail Distance. Each emitter holds a few
dozen particles at most, pauses off screen and is culled by distance in chunks. Each zone's **Street Dressing** setting
(under *Look*) scales the lot: 0 is none, 1 the usual amount, 2 twice as thick.

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
- **Casting:** the caster traces their school's symbol in the air in front of them, stroke by stroke, with a glowing pen
  tip trailing that school's sparks, then a seal ring round it (a flame, a snowflake, a lightning bolt, a flower, a skull,
  an open book, scales; a star for General spells). Each school's energy looks its own: fire flickers, ice glints, storm
  crackles and jitters, life breathes, death crawls with wisps, myth shimmers gold, balance pulses evenly. It flares when
  the spell is released. The symbols are drawn by `Tools/make_glyph_draw.py` (data maps in `Textures/CastDraw` and the
  pen paths in `Editor/GlyphPenData.cs`) and shown by the `WizardsVRC/GlyphDraw` shader.
- **The duel circle** is laid on a stone dais with a gold trim: a band of carved slate flagstones holds the eight
  seat sockets, linked by a glowing channel; inside it a star chart turns, a seven-point star joining the seven school
  emblems; outside it a band of glowing runes turns the other way. A gold inlay splits the two sides, with a crystal
  hovering over a plinth at each end, and motes drift up off the runes. Art by `Tools/make_duel_ring.py`
  (`Textures/DuelRing*.png`); the older `DuelCircle*.png` art is still used by the tower and dungeon sigils.
- **The turn clock** is set into the middle of the duel circle: a dial whose rim of 40 rune segments goes dark
  clockwise as the time to choose runs out (gold, then red and pulsing near the end), and a gold hand that sweeps the
  timer, then points at each caster in turn (`Shaders/DuelClock.shader`, dial and hand by `Tools/make_duel_ring.py`).
  Over it, from the bottom up: whose turn it is, the round / cast banner, and the **round timer**: the seconds left to
  choose, gold, then red for the last ten, when every second ticks (`Audio/UI/Clock_Tick.wav`, a brighter
  `Clock_TickLast.wav` for the last three; `Tools/make_clock_tick.py`) and the number pops. They're hidden for you while
  you stand in the middle yourself (casting a cinematic).
- **Picking a target:** with a spell picked, the seats it can go to glow on the floor (green for allies, red for
  enemies); click the combatant (or their button on the hand) to cast there.
- **Drawing the rune (`CastPattern`):** a spell under 100% accuracy (with your bonuses and Focus) isn't sent the moment
  you pick it. A board opens in front of you with glowing points along your school's symbol (the one you draw in the
  air, without its seal ring), a faint copy of the symbol behind them, a timer and Cancel (the card goes back to your
  hand). **VR:** a wand waits at your right hip (left with the book's **Wand hand** setting); take it and touch each
  point with its tip in order, and the symbol lights up as you trace it. **Desktop:** click the numbered points in
  order. Points: 2 + the spell's pips + one per 15% of accuracy under 100 (3 to 10); bigger spells also get smaller
  targets in VR and less time per point on desktop. Touching the point after the one you're on (or clicking it) counts
  the skipped one as missed and goes on. When it's done, the time is up or the round is about to start, the share of
  points hit takes that share of `BattleManager.patternFizzleCut` (90%) off the spell's chance to fizzle: a perfect rune
  on a 75% spell fizzles 2.5% of the time. Monsters never draw one. Sounds: `Audio/Cast/Pattern_*.wav`
  (`Tools/make_spell_cast_sounds.py`).
- **Casting styles:** four wands change how the rune is drawn (`ItemDatabase.castStyleItems`; the item's details say
  which). In VR the wand pickup shows that wand's model, and the sidearm is a pickup of its own.

  | Wand (item, Lv, sold) | VR | Desktop |
  | --- | --- | --- |
  | Runecutter Saber (264, Lv 6) | draw the sabre and slash through the board along each glowing stroke (point to point), fast, in order. A cut counts where the blade crosses the board, and one long sweep can cut several strokes | swing your view fast across each stroke |
  | Conductor's Baton (265, Lv 9) | after a two-beat count-in on the metronome, each point comes due on its own beat (it swells as it nears). Touch it with the baton on the beat (±0.24 s) | click it on the beat. Early or late loses it |
  | Wisp Lantern (266, Lv 14) | a wisp runs the rune's path (jumping where the pen lifts). Keep the lantern in its glow. The quality is the share of the run spent in the glow | keep your view (the crosshair) on it |
  | Dominion Sidearm (267, Lv 26) | the points pop up one at a time as targets that shrink as they fade. Draw the pistol (aim laser) and pull the trigger on each. A wild shot eats 30% of the target's time | click each target before it fades |

  Any other wand draws the usual way. Bigger spells make every style harder: more points, smaller targets, faster beats,
  a quicker wisp and quicker targets. Each style fits itself into the time left in the round. Sounds:
  `Pattern_Slash`, `Pattern_Shot`, `Pattern_Beat` and `Pattern_Wisp`.
- **A wizard's attack spell is a cinematic** (`BattleManager.FLAG_CINE` on its first act, so every client times it the
  same): your view irises shut in your school's colour (`Shaders/SpellFade.shader`), you vanish from your seat in a burst
  and appear in the middle of the ring facing your target, and it opens again. You charge in first person: the symbol is
  drawn in the air before you while your school's rune draws itself on the floor, the flourish plays and whatever your
  spell's power adds builds round you. As the spell is let go the cutscene screen (the one NPC cutscenes play on) cuts
  to film (the film camera sees your own avatar through its mirror copy): the surge, the power taking shape over you,
  each wave from over your shoulder to its landing, the finisher shot the spell's own way with a close, shaken shot of
  the hit, and a wide shot turning round the ring while it settles (Skip closes the screen). Then the view shuts,
  you're back in your seat, and it opens. Everyone else in the fight sees you go to the middle and cast from there, and
  their cutscene screen films it too (from the start of the charge). **Monsters' attack spells are cinematics as
  well:** the monster walks to the middle of the ring, casts from there and goes back to its seat, filmed for everyone
  in the fight. Every spell flies **from the middle of the ring to its target's seat** and lands on the target's front
  (a creature pulls up just short of it), its path and impact scaled to that distance, so nothing overshoots. Timings
  follow the staging formula (`SPELL_ANIMATIONS.md`): `cineInSeconds`, `gatherPerPip`, the cast sound, the display
  (`CardDatabase.cineDisplaySeconds`), `spellRestSecondsMin` to `spellRestSecondsMax` by pip cost for the arrival,
  attack, result and exit, `cineOutSeconds`, plus `expendSeconds` for each blade, trap and shield used
  (`expendMaxSeconds` at most); the beats' shares are `CardDatabase.Plan*`. Sounds:
  `Audio/Cast/Spell_*.wav`.
- **A fizzle** plays the cast as normal for its first 2 seconds (`BattleManager.fizzleCutSeconds`), then the cast sound
  cuts off, the fizzle sound plays (`Audio/Cast/FX_Fizzle.wav`, `Tools/make_fizzle_sound.py`), and the symbol sputters,
  greys and sags away in a puff of smoke.
- **Fizzles** keep your pips, and the card isn't lost: it goes back into your deck at a random spot (Wizard101 rule), or to
  the end of your hand if `BattleClient.fizzleReturnsToHand` is on. Spells cost **1 mana per pip**, charged when cast, even on a fizzle;
  cards you don't have the mana for are greyed out.
- **Burns (damage over time):** every burn spell hits when it lands (its own hit, or `CardDatabase.dotInitialPct`
  (25%) of its burn when it has none), and its burn is `dotBoostPct` (150%) of the amount it lists. A blade boosts both
  the hit and the burn (used once per cast), and so does the caster's damage bonus; the target's resist lowers the burn.
  The card shows both numbers.
- **Critical / Block:** gear Critical % doubles a hit or heal (`critMultiplier`); the target's Block % can stop it.
  Gear Pierce % ignores that much of shields and resist. Bosses have `bossCritPct`.
- **School matchups:** monsters and minions take `creatureSchoolWeakness` (30%) more damage from the opposite school
  (Fire/Ice, Storm/Myth, Life/Death) and `creatureSchoolResist` (30%) less from their own. Their overhead label doesn't
  spell this out: it reads like Wizard101, e.g. "Rank 2 <Life icon>", with "Elite" or "Boss" after it for those (minions: "<Fire icon> Minion"). The school icons are glyphs U+E000-U+E006 in the combat pixel font, coloured per school. The round log marks critical / weak / resisted / blocked hits.
- **Health and mana persist** between fights, and the HUD shows your live health during a fight. The school health
  trait (`PlayerState.schoolHealthPct`, e.g. Ice +25%, Storm -15%) is part of your max health everywhere. They regenerate slowly while you're out of combat
  (`PlayerState.healthRegenPct` / `manaRegenPct`), refill on level-up, and potions restore them: drink from the
  backpack (Drink button) or press **Potion** in battle, which uses your turn: it opens a menu of the potions you carry
  (each kind with how many you have and what it restores, your health and mana at the top); pick one to drink it, or
  Cancel. A potion that couldn't do anything (full health for a health potion) isn't drunk.
- **Defeat:** a wizard reduced to 0 health is sent home at once with `BattleClient.defeatHealthPct` (25%) health,
  and their seat frees up. The fight goes on for the rest of the party.
- **Flee** (planning only): leave the duel immediately; you keep your health but your mana drops to 0.
- **Waiting for the others:** once your spell is chosen while another wizard is still choosing, a box covers your cards:
  your spell and its target, who you're waiting on and the time left. **Back to my spells** takes your spell back
  (`BattleManager.NetUnpick`), returns the card to your hand and lets you choose again (not for a potion, which is
  already drunk). The action tray stays usable, so you can still flee. If the round starts just as you press Back, the
  spell is cast and leaves your hand again. Fighting alone you never see it: the round starts at once.
- **Joining a duel in progress:** in the wandering-monster lanes, walk into the circle while a seat is free and you
  join; you act from the next round.
- **Item cards:** gear can carry spells (`ItemDatabase.itemCardItems` / `itemCardIds`). While it's equipped they join
  your battle deck for each fight, on top of your deck (they don't use its space), and you can cast them whatever your
  school. The starting Training Staff holds one basic spell of every school, so new wizards have more to throw early on.
  Every other wand (one with no item cards of its own) adds 7 copies of a basic attack (`ItemDatabase.wandBasicCopies`):
  its school's basic spell, or your own school's for a wand any school can use. The item's details in the backpack and
  the Deck page list them.
- **No wand drops before level 8** (`BattleClient.wandDropLevel`): below it, no wand comes from a monster's own drop,
  the random loot or a ring's loot table, so new wizards keep the Training Staff. Shops and quests are unaffected.
- **Decks are items** (Wizard101 style): the Apprentice Deck (20 spells, 4 copies each) to start, plus the Scholar's,
  Ironbound and Starfall decks (bigger, more copies, small stat bonuses) from loot and bosses. Equip one in the backpack
  or on the book's Deck tab; each deck keeps its own spells and battles draw from the equipped one.
- **Spell cards** use design 1 "Classic" (`Textures/Cards/CardFrame<School>.png`, made by `Tools/make_card_frames.py`):
  a vivid school-coloured frame, parchment face, name banner, pip gem, accuracy seal and the school badge. The art
  window shows the icon for shields, blades and traps; other spells keep a plain school glow until they have animations.
- **Battle HUD placement:** the hand of cards floats in front of you (low like a lectern in VR, so pointing across the
  ring passes over it) and swings round to follow you when you turn. Grab the gold grip under it (`HudGrip`, a local
  pickup) to put it anywhere; it stays there, relative to you, for every fight after (`PlayerState.battleHudOffset`).
- **In your seat** you're no longer frozen: you can turn and shuffle about slowly within `BattleClient.seatRoam` (0.9 m)
  of the seat, and you're eased back if you go further. No jumping in a seat.
- **Targets in VR:** each seat's click target also covers the glowing ring on the floor, so you can pick an ally (whose
  avatar is standing in the seat) by pointing at the ring round their feet; the hand's target buttons work too.
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
- **Music** (`MusicDirector`, built by `WizardsVRCSceneBuilder.Music.cs`, local to each player): the combat theme in
  every duel (from the top each fight), the character creation theme on the title stage and in the creator, and the
  hub theme in the castle grounds and the keep (not in the tavern, where the jukebox plays; nothing during the opening
  dream). Tracks crossfade; the hub and creation tracks carry on where they left off. The book's Settings page has
  **Music: Full / Medium / Quiet / Off** (remembered). Tracks are `Audio/Music/Music_Hub_Temp.mp3` (a placeholder),
  `Music_Combat_World1.mp3` and `Music_CharacterCreation.mp3`; replace a file with one of the same name to change it.
  Any zone can have its own track too: drop the file into `Audio/Music` and type its name (without `.mp3`) into the
  zone's **Music Track** (Zone > Music in the World Creator, or a World's **Music Track** for all of its zones). It
  crossfades in as you walk out of the tunnel into the zone and out as you leave, follows you into the zone's secret
  boss place, and gives way to the combat theme in duels. Boss towers play the zone's **Tower Music Track** (or the
  world's) instead, falling back to the zone's music. Zones that share a file share the music. A world's **Dialogue
  Music Track** plays while anyone on its **Dialogue Music Speakers** list talks (a cutscene line, a tower leader's
  greeting); the Dominion's theme is wired this way. Current files: `Music_Zone_SunpetalGardens`, `Music_Zone_LanternRow`,
  `Music_Zone_TidewatchDocks`, `Music_Zone_KilnStreet`, `Music_Zone_ThornhollowGrove`, `Music_Zone_CopperbottomMine`,
  `Music_Tower_World1` (every World 1 boss tower) and `Music_Dialogue_Dominion`. World data v26 fills in the zone
  music for zones that were left blank (Sunpetal Gardens, Lantern Row and the rest of World 1 and 2) without touching
  tracks you've set; the build log lists any music area still without a track. A track that stops (or never started)
  is started again within a second while you're in its area.
  **Now playing:** while a track plays, the game it's from and its name are shown in a small line over the HUD, in the
  top-left corner of the book's map and under the title stage's menu and character select ("♪ WildStar OST - Character
  Creation"). The words come from `Audio/Music/MusicCredits.txt`, one `Music_FileName = Game OST - Track Name` line per
  file; a track with no line plays uncredited and the build log names it (`Music_Hub_Temp` and `Music_Combat_World1` have
  none yet). The line clears when nothing is playing or the music is switched off.
- **Monster AI:** monsters heal an ally under 45% health (a monster that heals itself waits 2 rounds before healing
  again). **Bosses are never sure to heal:** below `BattleManager.bossHealBelowPct` (60%) a boss only heals on a chance,
  `bossHealChance` (25%) at the threshold rising to `bossHealChanceLow` (55%) near 0, and after any heal it waits
  `bossHealGraceRounds` (4) rounds before it can heal again. Monsters sometimes put a blade or shield on themselves first, and otherwise cast their biggest affordable attack
  (occasionally saving pips).

- **The Tech School (hidden, The Dominion's):** `School.Tech` / `CardDatabase.TECH` (code 8; 7 is the healing-charm
  code), electric teal, badge `Textures/FxIcons/SchoolTech.png` (`Tools/make_tech_icon.py`). Machine guns, hacking, cyber
  attacks and armour-piercing shots. Its gimmick: **barrages** (`EFFECT_BARRAGE`: many small hits, each its own hit, so
  each strips a shield, and one blade boosts every shot), **hacks** (`EFFECT_HACK`: steal the target's blades, and
  shields too), drones, turrets and mechs that fire three-shot bursts, and the **Neon Grid** (`EFFECT_FIELD`: a bubble
  over the whole duel, +25% to every Tech spell for 4 rounds; one at a time). Nobody teaches it yet (players get it
  much later); the book's Deck page shows its tab only once you know one of its spells. Monsters can use it now (set a
  monster's school to Tech and give it Tech spells). Its look: a microchip casting glyph (two pins a side, a charge
  zig-zagging across it; `Tools/make_glyph_draw.py`) drawn with a digital style (scanlines, a rolling scan bar, blocky
  glitches; GlyphDraw shader style 9), teal effect colours, a box-shaped burst of streaking data shards when it's cast and
  falling data streaks off the pen, a teal card frame with gold circuit traces (`Tools/make_card_frames.py`), Tech
  shield / blade / trap icons (`Tools/make_fx_icons.py`), a cog icon in card text (U+E007 in the pixel font) and a night
  skyline over a glowing grid on its card art. Looks are indexed by `CardDatabase.LookIndex` (0-6 the schools, 7 General,
  8 Tech); Tech casts with its own sound, machinery rather than magic (`Audio/Cast/FX_CastingSound_Tech.mp3`, made by
  `Tools/make_tech_cast_sound.py`). Spells (id, level, pips):
  - Professor: 300 **Sidearm Burst** (L1, 1: 3 x 22-34), 301 **Taser Dart** (L5, 2: 80-120 and stun), 302 **Kevlar Plating**
    (L10, 0: 45% shield against any school), 303 **Proximity Mine** (L16, 0: +35% Tech trap), 304 **Firewall Breach**
    (L22, 2: 60-90, steals blades and shields), 305 **Overclock** (L26, 0: +40% Tech blade), 306 **Railgun** (L33, 4:
    360-520, ignores 30% of shields and resist), 307 **Gatling Storm** (L42, 5: 8 x 70-95).
  - School quests: 308 **Scout Drone** (L7, 2: 220-health drone), 309 **Neon Grid** (L12, 1: the bubble), 310 **Sentry
    Turret** (L18, 3: 380 health), 311 **Assault Mech** (L28, 4: 650 health), 312 **Zero-Day Exploit** (L38, 3: 120-170
    to all enemies, steals all their blades and shields), 313 **Orbital Strike** (L48, 7: 5 x 110-150 on every enemy).
- **Spell design:** every school (Fire, Ice, Storm, Life, Death, Myth, Balance, and the hidden Tech School) has 14 spells
  on the same level format, and plays to its role: Storm glass cannon, Fire DoT + detonate, Ice tank, Life healer, Death
  drain, Myth minions + counter-play (shatter, dispel, stun, pierce), Balance universal support. See the remarks on
  `CardDatabase`.
  - **Professor training** (no quest: visit your professor once you reach the level; free for your own school, a training
    point for another): L1 starter, L5 minor attack or utility, L10 shield, L16 3-pip attack, L22 mid-tier attack or
    manipulation, L26 blade, L33 advanced single-target attack, L42 big hit or unique utility. Each needs the professor
    spell before it. Your professor writes to you (Notices) when one unlocks.
  - **School quests** (your professor sends you to prove yourself; the professor's spell list never shows them): L7 trap,
    L12 school bubble, L18 low-rank minion, L28 mid-tier minion, L38 the trial spell (the built-in Trial of the School,
    quest 1, now at level 38), L48 the signature 7-pip spell.
  - By school (professor L1 / 5 / 10 / 16 / 22 / 26 / 33 / 42; quests L7 / 12 / 18 / 28 / 38 / 48):
    - Fire: Ember Lance, Smoldering Brand, Ember Aegis, Cinder Rain, Pyre Hawk, Ember Edge, Molten Behemoth, Stoke the
      Embers; Cinder Snare, **Kindled Sky**, **Cinder Whelp**, Magma Hound, Wildfire Oath, Sunfire Wyrm.
    - Ice: Frost Shard, Rime Challenge, Frostward, Hoarfrost Gale, Frost Wyvern, Rime Edge, Frozen Colossus, Permafrost
      Bulwark; Frost Snare, **Glacial Hush**, **Rime Pup**, Rime Sentinel, Winter's Bastion, Winter's Wrath.
    - Storm: Spark Lash, Thunder Hare, Storm Aegis, Thunderclap, Skyfall Serpent, Static Charge, Skyfall Leviathan, Gale
      Mark; Thunder Snare, **Thunderhead**, **Spark Sprite**, Thunder Golem, Overcharge, Maelstrom Eye.
    - Life: Thorn Volley, Verdant Bloom, Verdant Aegis, Rejuvenate, Grove Stag, Thorn Edge, Wildwood Stampede,
      Wellspring; Bramble Snare, **Verdant Canopy**, **Sproutling**, Treant Warden, Bloom of Renewal, Verdant Titan.
    - Death: Shade Siphon, Grave Ghast, Veil of Dread, Soul Swarm, Night Leech, Grave Edge, Bone Dragon, Dread Mark; Hex
      Snare, **Gloaming**, **Bone Scuttler**, Bone Thrall, Dark Tithe, Requiem of Ash.
    - Myth: Mirage Dart, Mirage Lancer, **Fable Aegis**, Chimera's Roar, Stone Basilisk, Fable Edge, Fabled Hydra,
      Sundering Gaze; Mirage Snare, **Twice-Told Tale**, Clay Golemling, Stone Colossus, Riddle of Iron, Titan of Legends.
    - Balance: Prism Ray, Sand Djinn, Balance Aegis, Sandstorm, Sphinx of Scales, Accord Edge, Sandstorm Sphinx,
      Elemental Tri-Blade; Sand Snare, **Equinox**, **Dune Scarab**, Sand Golem, Spirit Tri-Blade, Grand Convergence. Balance's
      Tri-Shields and Tri-Traps stay professor extras at their old levels.
  - New (bold above), ids 210-223: each school's **bubble** (`EFFECT_FIELD`, 1 pip: +25% to every spell of that school,
    anyone's, damage and heals, for 4 rounds; one bubble or grid at a time), the low-rank minions (3 pips) and Myth's
    shield. Spell ids never changed, so saves keep every spell they've learned; only levels, prerequisites and how each
    spell is earned moved. Dustwind's school side quests still teach each school's L42 spell early.
- **Treasure cards** (Wizard101's): single-use copies of spells. Anyone can cast one, whatever their school or level,
  without knowing the spell. Each deck item has a **sideboard** (about a third of its size, 6-18 cards,
  `ItemDatabase.GetDeckTreasureCapacity`); on the book's Deck page the Treasure icon (the last one in the header) lists the
  treasure cards you own, click one to move it into the shown deck's sideboard, and the row under the deck switches
  (Item Spells / Treasure Cards tabs) to the sideboard, where clicking a card takes it back out. In a duel the action
  tray's **Treasure** button draws one card from the equipped deck's sideboard into your hand, but only after you've
  **discarded a card**: each card discarded this round lets you draw one treasure card (drawing doesn't use your turn,
  and unused discards don't carry over to the next round). It wears a gold TREASURE ribbon. A treasure card is used up when it's cast, fizzles or is discarded;
  ones drawn but not cast stay in the sideboard. Saved in save format v13 (`CharacterData.treasureCounts`, `sideCards`).
  - **The Library sells them:** Librarian Wren's counter in the castle grounds (`GoldShop.OpenTreasure`) has a treasure
    card of every school spell up to level 33, by school (school buttons jump to each), priced by pip cost:
    25 + 15 x pips + 15 x pips² gold (25 for 0 pips up to 475 for 5).
- **Starshard card packs** (the Starshard Shop's **Card Packs** category, `CardPacks`): buy one and it opens on the spot,
  its cards lying face down and turning over one by one, each glowing its rarity (Common, Uncommon, Rare, Ultra-Rare;
  treasure-card rarity comes from pip cost). Packs: Spellbinder's (12 shards, five treasure cards), Wanderer's (20: three
  treasure cards, gear and a wand for your level), Lorekeeper's (28: four treasure cards, one 3+ pips, and gear), Arcane
  Hoard (45: six cards, a 5+ pip treasure card, a wand and gear at or above your level, gold) and a 15-shard pack per
  school (five of its treasure cards, one 4+ pips). Gear and wands are for your school or any school and go to your
  backpack (a full backpack pays their worth in gold). **Ludicrously rarely** (3 in 10,000 per card, 12 on a pack's best
  card) a card is instead a **Starshard spell** you learn for good, one per school: Phoenix Ascendant, Aurora Glacier,
  Skybreaker Roc, Celestial Bloom, Eclipse Wraith, Starforged Golem and Astral Scales (ids 230-236; never taught or sold,
  usable by any wizard who finds one). Pack art: `Tools/make_pack_art.py`.
- **School quests** (world data v27, the **School Quests** questline, save ids 90-124: school s, slot k at 90 + s * 5 + k):
  five per school, offered by the professor in the school hall to their own wizards from the slot's level once the street
  is open (its prerequisite is the street's gate quest). The professor speaks the offer and hand-in lines in their hall
  (`QuestManager.TalkProfessor` plays a quest's dialogue before their usual lines). Each sends you to fight and gather in
  one street, then back for the spell: L7 Tidewatch Docks, L12 the Hollow Keep, L18 the Iron Line Railyard, L28
  Lonesome Bend, L48 Lonesome Bend's fortress after Dustwind's finale. The L28 and L48 quests use the toughest street so
  far; point them at later worlds' streets as those are built (the questline is editable like any other).
- **Shields, blades and traps** circle each combatant as small 3D models in their school's colour (shields at the chest
  facing out, swords twirling over the head, spiked traps turning at the feet). When a cast uses some they go one at a
  time, blades first, then traps, then shields: each lifts out of its orbit flickering white, holds white, then
  shatters into shards with its sound and its bonus floating up. For a staged spell that happens after the display,
  right before the attack (0.4 s each, 2 s at most); for a cast with no staging (a minion's) they go in the half second
  before the hit (`BattleClient.fxUseSeconds`). One being placed just appears with a chime. School shields block 80%;
  General Empower is +15% to any attack.
- **Stuns** (Myth's Stone Basilisk, Riddle of Iron and Fabled Hydra): a stunned combatant loses its next turn and gains
  a **stun shield** that blocks the next stun, so stuns can't chain.
- **Special spells:** Ice's Permafrost Bulwark (two any-school shields on one wizard) and Winter's Bastion (Ice damage
  plus a shield on yourself); Storm's Gale Mark (damage plus a Storm trap on the target); Fire's Wildfire Oath (damage
  plus a Fire blade on yourself); Death's Dread Mark (+70% trap on an enemy, but a +30% trap on yourself); Myth's
  Chimera's Roar (damage to all enemies); Balance's Elemental / Spirit Tri-Shield and Tri-Trap; Life's Wellspring Charm
  (your next heal heals 35% more), taught by Gardener Moss in a Life-only side quest (Moss's Tonic, quest 39); and
  Second Wind, a General spell every wizard has from level 5 that heals you for 420.
- **School-only spells:** a school's blades and traps (and Dread Mark and Wellspring Charm) can only be learned by
  wizards of that school; other schools training there skip them in the tree.
- **Training points:** one every 4 levels up to level 19 (4, 8, 12, 16), then one every 5 levels from 20.

All numbers are public fields on `BattleManager`, `BattleClient`, `PlayerState` and the databases, so they can be tuned
in the Inspector. **After pulling these changes run WizardsVRC > Rebuild MainWorld Scene** so the new Potion/Flee
buttons, synced arrays and database tables are in the scene.

## For future systems

Read and write `PlayerState.current` (a `CharacterData`), call `PlayerState.MarkDirty()` after changes, and
`PlayerState.SaveNow()` after important events (level up, quest turn-in).
