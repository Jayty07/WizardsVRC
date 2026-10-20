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
| `Assets/WizardsVRC/Scripts/PlayerState.cs` | Live character (source of truth) + autosave |
| `Assets/WizardsVRC/Scripts/TeleportHelper.cs` | `TeleportTo(Transform)` via `VRCPlayerApi.TeleportTo` |
| `Assets/WizardsVRC/Scripts/CharacterSelectUI.cs` | Slot panels, school picker, delete confirm, teleport to hub |
| `Assets/WizardsVRC/Scripts/ItemDatabase.cs` | Placeholder item definitions (category, level/school req, stat bonuses) + stat totals |
| `Assets/WizardsVRC/Scripts/InventoryBook.cs` | Backpack book: live avatar portrait (MirrorReflection-layer camera → RenderTexture), equipment slots + stats (left page), tabbed paged item grid + Equip/Unequip (right page) |
| `Assets/WizardsVRC/Scripts/CardDatabase.cs` | Placeholder spell cards (4 per school + 4 general), level/school learning rules, deck limits |
| `Assets/WizardsVRC/Scripts/DeckPages.cs` | Book's Deck tab: Deck 1/2/3 contents (left), spellbook with Add/Remove (right) |
| `Assets/WizardsVRC/Scripts/PlayerHUD.cs` | Head-following HUD: name, level, health, mana, gold |
| `Assets/WizardsVRC/Scripts/LootChest.cs` | Test chest in the hub: random item + gold per click |
| `Assets/WizardsVRC/Scripts/EventRelay.cs` | Interact → `SendCustomEvent` on a target (backpack pedestal) |
| `Assets/WizardsVRC/Editor/WizardsVRCSceneBuilder.cs` | Generates the scene and wires all references/buttons |

## Scene

- `VRCWorld` — `VRCSceneDescriptor`: spawn = `SelectRoom/SelectSpawn`, respawn height **-600** (must be below the
  select room at y = -500), reference camera = `VRCWorld/ReferenceCamera`.
  (There is no `VRC_SceneLightmapSettings` component in the current SDK; lighting uses normal Unity Lighting settings.)
- `SelectRoom` (y = -500) — closed room, light, spawn point, world-space canvas (`VRCUiShape`) with 4 slot panels,
  school picker, delete confirmation.
- `Hub` (origin) — ground, plaza, landing pad + `HubSpawn`, NPC placeholder, "Change Character" board.
- `Hub` also has a **Practice Chest** (random loot for testing) and a **Backpack pedestal**.
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

Worst case ≈ 0.9 KB raw / 1.2 KB base64. `meta` = `1|school,level,name|...|...` (empty entry = free slot).

Saving rotates the previous valid blob into `char_N_bak`. Loading a blob that fails the checksum falls back to the
backup and rewrites `char_N` from it. PlayerData has no delete API, so Delete writes empty strings.
Saves happen on create, on select/swap (the previous character is saved first), on "Change Character", and every
120 s (`PlayerState.autosaveInterval`).

## For future systems

Read and write `PlayerState.current` (a `CharacterData`), call `PlayerState.MarkDirty()` after changes, and
`PlayerState.SaveNow()` after important events (level up, quest turn-in).
