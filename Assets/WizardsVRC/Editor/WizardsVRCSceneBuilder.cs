using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Generates Assets/Scenes/MainWorld.unity (select room, hub stub, systems, UI wiring).
    /// Runs automatically the first time the project is opened if the scene does not exist yet,
    /// and can be re-run from the menu: WizardsVRC > Rebuild MainWorld Scene.
    /// </summary>
    [InitializeOnLoad]
    public static partial class WizardsVRCSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainWorld.unity";
        private const string ScriptsDir = "Assets/WizardsVRC/Scripts";
        private const string ProgramsDir = "Assets/WizardsVRC/UdonPrograms";
        private const string MaterialsDir = "Assets/WizardsVRC/Materials";
        private const string AutoBuildKey = "WizardsVRC.AutoBuildAttempted";

        private const float RespawnHeight = -600f;

        private static Font _font;

        static WizardsVRCSceneBuilder()
        {
            EditorApplication.delayCall += TryAutoBuild;
            EditorApplication.delayCall += CheckInputHandling;
        }

        private const string InputCheckKey = "WizardsVRC.InputHandlingChecked";

        /// <summary>
        /// VRChat (the SDK's PhysBones, ClientSim, Udon's Input) reads input through the old Input Manager. With Player
        /// Settings > Active Input Handling set to "Input System Package (New)" only, every frame throws
        /// InvalidOperationException. Offer to switch it to "Both" (needs an editor restart to take effect).
        /// </summary>
        private static void CheckInputHandling()
        {
            if (SessionState.GetBool(InputCheckKey, false)) return;
            SessionState.SetBool(InputCheckKey, true);
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0) return;
            var so = new SerializedObject(settings[0]);
            var prop = so.FindProperty("activeInputHandler");
            // 0 = Input Manager (Old), 1 = Input System Package (New), 2 = Both
            if (prop == null || prop.intValue != 1) return;
            if (!EditorUtility.DisplayDialog("WizardsVRC",
                    "Active Input Handling is set to \"Input System Package (New)\" only, but VRChat reads input the old way, so Play mode "
                    + "spams InvalidOperationException (PhysBoneGrabHelper).\n\nSwitch it to \"Both\"? Unity needs to restart for it to apply.",
                    "Switch and restart", "Not now"))
                return;
            prop.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }

        private static void TryAutoBuild()
        {
            if (File.Exists(ScenePath) || SessionState.GetBool(AutoBuildKey, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutoBuild;
                return;
            }
            SessionState.SetBool(AutoBuildKey, true);
            Debug.Log("[WizardsVRC] MainWorld scene not found, generating it.");
            BuildScene();
        }

        [MenuItem("WizardsVRC/Rebuild MainWorld Scene")]
        public static void RebuildFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild MainWorld",
                    "This replaces " + ScenePath + " with a freshly generated scene.\n\nAnything inside the \"" + MyChangesName
                    + "\" object is kept. Everything else you changed by hand is lost. Continue?", "Rebuild", "Cancel"))
                return;
            BuildScene();
        }

        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Authored world data (monsters, zones, questlines) lives in Assets/WizardsVRC/WorldData and is never rebuilt.
            // Checked here so you can cancel before the scene is replaced; loaded again after the new scene is created.
            var worldData = WorldDataStore.Load();
            var worldReport = WorldValidator.ValidateAll(worldData, null);
            worldReport.Log();
            if (!worldReport.Ok && !EditorUtility.DisplayDialog("Rebuild MainWorld", "The world data has problems:\n\n" + worldReport.Summary()
                    + "\n\nBuild anyway? Broken parts are skipped.", "Build anyway", "Cancel"))
                return;

            ConfigureProject();
            if (!EnsureProgramAssets()) return;
            ResetExtras();
            BackUpMyChanges();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ResetNameTags();
            // The VRChat SDK Control Panel re-validates on every scene change and throws on the brand-new empty scene
            // (it has no world descriptor yet), so close it while building and reopen it afterwards.
            System.Type sdkPanel = CloseSdkControlPanel();
            Scene scene;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (System.Exception e)
            {
                // Another editor window's scene-changed handler failed; the new scene still exists, so keep building.
                Debug.LogWarning("[WizardsVRC] Ignored an error from another editor window while creating the scene: " + e.Message);
                scene = SceneManager.GetActiveScene();
            }
            // Opening/creating a scene unloads assets nothing in it uses, which includes the world data loaded above
            // (Unity then reports those objects as null). Load it again now that the new scene exists.
            worldData = WorldDataStore.Load();

            // ---------------- lighting
            var sun = new GameObject("Directional Light");
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.1f;
            sunLight.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.15f);
            SetupPsxAtmosphere(sunLight);

            // ---------------- world descriptor
            var world = new GameObject("VRCWorld");
            var refCamGo = new GameObject("ReferenceCamera");
            refCamGo.transform.SetParent(world.transform, false);
            var refCam = refCamGo.AddComponent<Camera>();
            refCam.nearClipPlane = 0.05f;
            // Nothing past the fog is drawn (see WizardsVRCSceneBuilder.Culling.cs).
            refCam.farClipPlane = DrawDistance();
            refCam.enabled = false;

            // ---------------- areas
            // The hub zone (if the world data has one) decides whether the schools are indoors or out in the castle grounds.
            HubLayout hubPlan = PlanHub(worldData);
            Interior interior = BuildInterior(hubPlan.outdoorSchools);
            _interiorRoot = interior.root;
            _archFronts.Clear();
            Transform selectSpawn = interior.officeSpawn;
            Canvas selectCanvas = interior.selectCanvas;
            Transform hubSpawn = BuildHub(out Canvas hubCanvas, hubPlan, interior, worldData);
            AddFloaters(interior.root);   // the office's desk orb and the hall star hover too

            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { selectSpawn };
            descriptor.RespawnHeightY = RespawnHeight;
            descriptor.ReferenceCamera = refCamGo;
            AddPipelineManager(world);

            // ---------------- systems
            var systems = new GameObject("Systems");
            var activeGo = Child(systems.transform, "ActiveCharacter", Vector3.zero);
            var scratchGo = Child(systems.transform, "ScratchCharacter", Vector3.zero);
            var activeData = UdonSharpUndo.AddComponent<CharacterData>(activeGo.gameObject);
            var scratchData = UdonSharpUndo.AddComponent<CharacterData>(scratchGo.gameObject);

            var saveManager = UdonSharpUndo.AddComponent<SaveManager>(Child(systems.transform, "SaveManager", Vector3.zero).gameObject);
            var playerState = UdonSharpUndo.AddComponent<PlayerState>(Child(systems.transform, "PlayerState", Vector3.zero).gameObject);
            var teleporter = UdonSharpUndo.AddComponent<TeleportHelper>(Child(systems.transform, "TeleportHelper", Vector3.zero).gameObject);
            var selectUI = UdonSharpUndo.AddComponent<CharacterSelectUI>(selectCanvas.gameObject);
            var itemDb = UdonSharpUndo.AddComponent<ItemDatabase>(Child(systems.transform, "ItemDatabase", Vector3.zero).gameObject);
            var book = UdonSharpUndo.AddComponent<InventoryBook>(Child(systems.transform, "InventoryBook", Vector3.zero).gameObject);
            var hud = UdonSharpUndo.AddComponent<PlayerHUD>(Child(systems.transform, "PlayerHUD", Vector3.zero).gameObject);
            var cardDb = UdonSharpUndo.AddComponent<CardDatabase>(Child(systems.transform, "CardDatabase", Vector3.zero).gameObject);
            itemDb.cardDatabase = cardDb;
            var deckPages = UdonSharpUndo.AddComponent<DeckPages>(Child(systems.transform, "DeckPages", Vector3.zero).gameObject);
            var questPages = UdonSharpUndo.AddComponent<QuestPages>(Child(systems.transform, "QuestPages", Vector3.zero).gameObject);
            questPages.playerState = playerState;
            ApplyMonsterCatalog(cardDb, worldData.monsters);
            // Encounters: the Training Yard boss fight (queue pad) plus two wandering-monster lanes beside it. Each has its
            // own synced BattleManager, local BattleClient and duel circle (see BuildEncounter).
            var encounters = new System.Collections.Generic.List<BattleClient>();
            var battleClient = MakeEncounter(systems.transform, "Arena", cardDb, itemDb, playerState, hubSpawn, new[] { 1, 2, 2, 2 }, false, encounters);
            var laneA = MakeEncounter(systems.transform, "LaneRats", cardDb, itemDb, playerState, hubSpawn, new[] { 4, 4 }, true, encounters);
            var laneB = MakeEncounter(systems.transform, "LaneSprites", cardDb, itemDb, playerState, hubSpawn, new[] { 3, 3, 3 }, true, encounters);
            var battle = battleClient.manager;

            teleporter.defaultTarget = hubSpawn;
            saveManager.scratch = scratchData;
            saveManager.listener = selectUI;
            playerState.current = activeData;
            playerState.saveManager = saveManager;
            playerState.itemDatabase = itemDb;

            selectUI.saveManager = saveManager;
            selectUI.playerState = playerState;
            selectUI.teleporter = teleporter;
            selectUI.hubSpawn = hubSpawn;
            selectUI.selectSpawn = selectSpawn;
            selectUI.newCharacterSpawn = interior.officeSpawn;

            book.playerState = playerState;
            book.itemDatabase = itemDb;
            book.hud = hud;
            book.selectUI = selectUI;
            hud.playerState = playerState;
            hud.itemDatabase = itemDb;
            deckPages.playerState = playerState;
            deckPages.cardDatabase = cardDb;
            deckPages.itemDatabase = itemDb;
            book.deckPages = deckPages;
            book.questPages = questPages;

            BuildSelectCanvasContents(selectCanvas, selectUI);
            BuildHubCanvasContents(hubCanvas, selectUI, book);
            AssignCardArt(cardDb);
            BuildInventoryBook(book, hud, deckPages, questPages);
            BuildHud(hud);
            Transform hubRoot = hubSpawn.parent;
            var pedestal = BuildBackpackPedestal(hubRoot, hubPlan.pedestalPos, book);
            var queuePad = BuildQueuePad(hubRoot, hubPlan.padPos, battleClient);
            if (!HubTestProps)
            {
                // The old testing fixtures in the castle (the notice board's Change Character / Backpack panel, the
                // Backpack pedestal and the boss arena teleporter pad) stay out of sight: the book (B) does both jobs, and
                // the arena is only for the school trial, at level 35. Their parts are kept, switched off, so nothing that
                // refers to them breaks.
                hubCanvas.gameObject.SetActive(false);
                foreach (string n in new[] { "BackpackPedestal", "BackpackBook", "BackpackLabel", "ArenaTeleporterPad", "ArenaQueueTrigger", "ArenaPadLabel" })
                {
                    Transform tr = hubRoot.Find(n);
                    if (tr != null) tr.gameObject.SetActive(false);
                }
            }
            battleClient.queueRequiredQuest = hubPlan.padRequiredQuest;
            if (!string.IsNullOrEmpty(hubPlan.padLockedHint)) battleClient.queueLockedText = "Boss Arena Teleporter\n(locked: " + hubPlan.padLockedHint + ")";
            BuildArena(battleClient, new[] { laneA, laneB });
            var cutscenes = BuildCutscenes(systems.transform, selectUI, interior);
            var opening = BuildAwakening(systems.transform, interior, selectUI, teleporter, (IntroFlow)cutscenes[1], saveManager, hud, selectCanvas);
            var learning = BuildSpellLearning(systems.transform, playerState, cardDb, hud, out SpellTrainer trainer, out InteractionMenu interactionMenu);
            var quests = BuildQuests(systems.transform, interior, (CutscenePlayer)cutscenes[0], playerState, hud, hubRoot, trainer, interactionMenu, queuePad.transform, hubPlan);
            var questManager = (QuestManager)quests[0];
            questManager.cardDatabase = cardDb;
            questManager.popup = trainer.popup;
            foreach (var ec in encounters) ec.questManager = questManager;
            selectUI.questManager = questManager;
            hud.questManager = questManager;
            questPages.questManager = questManager;
            var wb = new WorldBuild { data = worldData, cutsceneParent = Child(systems.transform, "ZoneCutscenes", Vector3.zero) };
            BuildZones(systems.transform, hubRoot, questManager, cardDb, itemDb, playerState, encounters, wb, hubPlan);
            ApplyQuestlines(questManager, wb, itemDb);
            var streets = wb.behaviours;
            streets.AddRange(BuildPortalRoom(interior, questManager, playerState));
            var questGuide = BuildQuestGuide(systems.transform, playerState, questManager, (CutscenePlayer)cutscenes[0], encounters.ToArray(),
                interior.exitDoor, hubRoot.Find("KeepEntrance"));
            ApplyGuideAreas(questGuide, wb, interior.exitDoor, hubRoot.Find("KeepEntrance"), interior.outdoorSchools);
            book.questGuide = questGuide;
            var homeTeleport = UdonSharpUndo.AddComponent<TeleportHelper>(Child(systems.transform, "HomeTeleport", Vector3.zero).gameObject);
            homeTeleport.defaultTarget = hubSpawn;
            homeTeleport.hideZones = wb.zoneRoots.ToArray();
            book.homeTeleport = homeTeleport;
            streets.Add(homeTeleport);
            // Notices, tooltips, titles and auras, elixirs, the Starshard Shop and the gold shops (WizardsVRCSceneBuilder.Cosmetics.cs).
            WireExtras(systems.transform, playerState, itemDb, cardDb, book, hud, deckPages, questManager, null, encounters);
            // Every word in the book (every tab, the cards, the wardrobe and shop pages) in the pixel font, like the signs.
            if (book.bookRoot != null) ApplyCombatFont(book.bookRoot.transform);
            // Pop-up panels stay clickable when they open inside an NPC's or door's click box.
            var muter = UdonSharpUndo.AddComponent<InteractMuter>(Child(systems.transform, "InteractMuter", Vector3.zero).gameObject);
            muter.panels = new[]
            {
                ((CutscenePlayer)cutscenes[0]).screenRoot, interactionMenu.menuRoot, trainer.panelRoot,
                trainer.popup != null ? trainer.popup.popupRoot : null, book.bookRoot, GoldShopPanel, RailroadPanel, BountyPanel,
            };
            streets.Add(muter);
            // Pop-ups, menus, dialogue, the HUD, the quest arrow's words and the duel hand draw over everything, so they're
            // never lost inside a wall, an NPC or a monster.
            var overlay = UIOverlayMat();
            if (overlay != null)
            {
                foreach (var go in new GameObject[] { ((CutscenePlayer)cutscenes[0]).screenRoot, interactionMenu.menuRoot, trainer.panelRoot,
                             trainer.popup != null ? trainer.popup.popupRoot : null, book.bookRoot, hud.hudRoot,
                             questGuide.labelRoot != null ? questGuide.labelRoot.gameObject : null, GoldShopPanel, TooltipRoot, RailroadPanel, BountyPanel })
                    OverlayUI(go, overlay);
                foreach (var ec in encounters) { OverlayUI(ec.handRoot, overlay); OverlayUI(ec.fleeConfirmRoot, overlay); }
            }
            questPages.questGuide = questGuide;

            // Local area culling: the hub, the boss arena and each zone are off for a player who isn't in them.
            var cullAreas = new System.Collections.Generic.List<GameObject> { hubRoot.gameObject };
            var cullBounds = new System.Collections.Generic.List<Bounds> { hubPlan.cullBounds };
            var arenaGo = GameObject.Find("BossArena");
            if (arenaGo != null)
            {
                cullAreas.Add(arenaGo);
                cullBounds.Add(new Bounds(ArenaOrigin + new Vector3(0, 45f, 0), new Vector3(170f, 210f, 120f)));
            }
            cullAreas.AddRange(wb.cullRoots);
            cullBounds.AddRange(wb.cullBounds);
            var areaCuller = BuildAreaCuller(systems.transform, cullAreas, cullBounds, scene, encounters);
            // The quest guide points at the nearest unused one of a step's quest objects.
            var allTargets = new System.Collections.Generic.List<QuestTarget>();
            foreach (var go in scene.GetRootGameObjects()) allTargets.AddRange(go.GetComponentsInChildren<QuestTarget>(true));
            questManager.questTargets = allTargets.ToArray();
            streets.Add(areaCuller);
            BuildSkies(LayOutWorld(hubPlan, hubRoot, wb), areaCuller, RenderSettings.skybox);

            // Interface sounds: every button gets a click / hover sound; cards, the book and cutscenes call it directly.
            var uiAudio = BuildUiAudio(systems.transform);
            foreach (var ec in encounters)
            {
                ec.uiAudio = uiAudio;
                ec.lookPalette = (Color[])LookPalette.Clone();
                ec.schoolColors = (Color[])SchoolColors.Clone();
            }
            hud.lookPalette = (Color[])LookPalette.Clone();
            foreach (var orb in hubPlan.orbBehaviours) orb.uiAudio = uiAudio;
            foreach (var orb in wb.orbs) orb.uiAudio = uiAudio;
            book.uiAudio = uiAudio;
            foreach (var b in ExtraBehaviours)
            {
                var f = b.GetType().GetField("uiAudio");
                if (f != null && f.FieldType == typeof(UiAudio)) f.SetValue(b, uiAudio);
            }
            ((CutscenePlayer)cutscenes[0]).uiAudio = uiAudio;
            Debug.Log("[WizardsVRC] Interface sounds on " + WireUiSounds(scene, uiAudio) + " buttons.");
            streets.Add(uiAudio);

            // Background music: hub, combat and character creation themes (local to each player).
            streets.Add(BuildMusic(systems.transform, playerState, hubPlan.cullBounds, interior.root, book));

            var toSync = new System.Collections.Generic.List<UdonSharpBehaviour>(cutscenes);
            toSync.AddRange(opening);
            toSync.AddRange(quests);
            toSync.AddRange(learning);
            toSync.AddRange(streets);
            foreach (var b in new UdonSharpBehaviour[] { activeData, scratchData, saveManager, playerState, teleporter, selectUI, itemDb, book, hud, pedestal, cardDb, deckPages, queuePad, questPages, questGuide }) toSync.Add(b);
            foreach (var ec in encounters) { toSync.Add(ec.manager); toSync.Add(ec); foreach (var w in ec.GetComponentsInChildren<Wanderer>(true)) toSync.Add(w); }
            foreach (var b in ExtraBehaviours) if (b != null && !toSync.Contains(b)) toSync.Add(b);
            foreach (var b in toSync)
            {
                UdonSharpEditorUtility.CopyProxyToUdon(b);
                EditorUtility.SetDirty(b);
            }

            // Every plain menu panel and button in the PS1 look (WizardsVRCSceneBuilder.UiStyle.cs).
            Debug.Log("[WizardsVRC] Styled " + StyleWorldUi(scene.GetRootGameObjects()) + " menu panels and buttons.");
            RestoreMyChanges(scene);
            // 2D sounds get the (disabled) VRC Spatial Audio Source the SDK asks for (WizardsVRCSceneBuilder.Audio.cs).
            Debug.Log("[WizardsVRC] Spatial audio: fixed " + FixFlatAudio(scene) + " 2D audio sources.");

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Occlusion culling for the hub (its Occlusion Area is set up in BuildHubZone). Baked after the save so it's
            // baked for the saved scene; WizardsVRC > Bake Hub Occlusion re-bakes it on its own.
            if (BakeOcclusionOnRebuild && hubPlan.zone != null)
            {
                BakeHubOcclusion();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[WizardsVRC] Generated " + ScenePath);
            ReportWorld(worldData, wb, hubPlan);
            if (sdkPanel != null) EditorApplication.delayCall += () => EditorWindow.GetWindow(sdkPanel);
        }

        /// <summary>
        /// The VRChat SDK expects a PipelineManager next to the scene descriptor (it normally adds one itself when you add
        /// the descriptor by hand). Found by name so this compiles whichever SDK assembly it lives in.
        /// </summary>
        private static void AddPipelineManager(GameObject world)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = asm.GetType("VRC.Core.PipelineManager", false);
                if (t == null) continue;
                if (world.GetComponent(t) == null) world.AddComponent(t);
                return;
            }
        }

        /// <summary>Says what the world data produced, so a missing zone or questline is obvious.</summary>
        private static void ReportWorld(WorldDataStore.Data data, WorldBuild wb, HubLayout hub)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var root in wb.zoneRoots) names.Add(root.name);
            int quests = 0;
            foreach (var ql in data.questlines) if (ql != null && ql.includeInBuild) quests += ql.quests.Count;
            string hubText = hub.zone == null ? "classic castle (no hub zone)" : "'" + hub.zone.zoneKey + "' map" +
                             (hub.outdoorSchools ? ", schools outdoors" : ", schools in the keep") + ", " + hub.gates.Count + " gate(s)";
            string summary = "Hub: " + hubText + ". Zones built: " + (names.Count > 0 ? string.Join(", ", names) : "none") + ". Authored quests: " + quests +
                             ". World data folder: " + WorldDataStore.Folder + " (" + data.zones.Count + " zone asset(s), " + data.questlines.Count + " questline asset(s)).";
            if (names.Count > 0) { Debug.Log("[WizardsVRC] " + summary); return; }
            Debug.LogError("[WizardsVRC] " + summary + " No zones were built, so there is no Lantern Row. Check that " + WorldDataStore.Folder +
                           " contains Zone_LanternRow.asset with Include In Build ticked, then rebuild.");
            EditorUtility.DisplayDialog("Rebuild MainWorld", summary + "\n\nNo zones were built (so no Lantern Row). Check " + WorldDataStore.Folder +
                                                             " in the Project window: it should contain Zone_LanternRow.asset with Include In Build ticked.", "OK");
        }

        /// <summary>Closes the VRChat SDK Control Panel if it's open and returns its window type (null if it wasn't open).</summary>
        private static System.Type CloseSdkControlPanel()
        {
            System.Type found = null;
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (w == null || w.GetType().Name != "VRCSdkControlPanel") continue;
                found = w.GetType();
                w.Close();
            }
            return found;
        }

        // ================================================================== project config

        private static void ConfigureProject()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>
        /// When the editor loads: drops this project's UdonSharp program assets whose script has been deleted (e.g. the old
        /// LootChest testing chest). UdonSharp refuses to compile, and so to enter play mode, while one is left behind.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void DropOrphanPrograms()
        {
            EditorApplication.delayCall += () =>
            {
                foreach (string guid in AssetDatabase.FindAssets("t:UdonSharpProgramAsset", new[] { "Assets/WizardsVRC" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                    if (program == null || program.sourceCsScript != null) continue;
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log("[WizardsVRC] Removed " + path + ": its script no longer exists.");
                }
            };
        }

        /// <summary>Every runtime script; each needs an UdonSharp program asset in ProgramsDir.</summary>
        private static readonly string[] ProgramNames = { "CharacterData", "SaveManager", "PlayerState", "TeleportHelper", "CharacterSelectUI", "ItemDatabase", "InventoryBook", "PlayerHUD", "EventRelay", "CardDatabase", "DeckPages", "BattleManager", "BattleClient", "BattleQueuePad", "Wanderer", "QuestTarget", "Cutscene", "CutscenePlayer", "IntroFlow", "QuestManager", "SpellTrainer", "NewSpellPopup", "InteractionMenu", "NpcInteractions", "QuestPages", "QuestGuide", "QuestNpc", "QuestGate", "AreaCuller", "UiAudio", "Seat", "LocalToggle", "MugReset", "RestoreOrb", "TunnelGate", "TunnelPassage", "TavernJukebox", "AwakeningSequence", "CharacterCreator", "InteractMuter", "DungeonSigil", "MusicDirector", "FireFlicker", "NotificationCenter", "CosmeticsDatabase", "PlayerCosmetics", "StarshardStore", "GoldShop", "ItemTooltip", "WardrobePages", "ShopPages", "TowerFloor", "LevelRewards", "Railroad", "DepotStop", "BountyBoard", "HighNoon", "DustStorm" };

        /// <summary>Creates the program assets that are missing (and marks the rest current). True if any was made.</summary>
        private static bool CreateMissingPrograms()
        {
            bool created = false;
            foreach (string name in ProgramNames)
            {
                string assetPath = ProgramsDir + "/" + name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);
                if (existing != null)
                {
                    MarkScriptVersionCurrent(existing);
                    continue;
                }
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptsDir + "/" + name + ".cs");
                if (script == null)
                {
                    Debug.LogError("[WizardsVRC] Missing script " + name + ".cs");
                    continue;
                }
                var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = script;
                AssetDatabase.CreateAsset(program, assetPath);
                MarkScriptVersionCurrent(program);
                created = true;
            }
            return created;
        }

        /// <summary>
        /// When the editor loads: gives every new runtime script its program asset straight away. Without one, UdonSharp
        /// doesn't know the script is a behaviour and every other script that uses it fails ("type not found") until the
        /// scene is rebuilt.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AddMissingPrograms()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Directory.CreateDirectory(ProgramsDir);
                if (!CreateMissingPrograms()) return;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[WizardsVRC] Added UdonSharp program assets for new scripts; compiling.");
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            };
        }

        /// <summary>UdonSharp only compiles scripts that have a program asset next to them.</summary>
        private static bool EnsureProgramAssets()
        {
            Directory.CreateDirectory(ProgramsDir);
            // MonsterDatabase was merged into CardDatabase, and the hub's testing chest (LootChest) is gone; drop their old
            // program assets so UdonSharp doesn't look for the scripts.
            AssetDatabase.DeleteAsset(ProgramsDir + "/MonsterDatabase.asset");
            AssetDatabase.DeleteAsset(ProgramsDir + "/LootChest.asset");
            bool created = CreateMissingPrograms();
            AssetDatabase.SaveAssets();
            if (created) AssetDatabase.Refresh();
            // Compile synchronously: CopyProxyToUdon refuses to run until every program asset has been compiled
            // ("outdated behaviour version"), and CompileAllCsPrograms only starts an async compile.
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            string failed = UdonSharpProgramAsset.AnyUdonSharpScriptHasError() ? "some scripts" : null;
            foreach (var program in UdonSharpProgramAsset.GetAllUdonSharpPrograms())
                if (failed == null && program.CompiledVersion < UdonSharpProgramVersion.CurrentVersion) failed = "'" + program.name + "'";
            if (failed == null) return true;
            string msg = "UdonSharp could not compile " + failed + ". Fix the red [UdonSharp] errors in the Console, " +
                         "then run WizardsVRC > Rebuild MainWorld Scene again. The scene was not changed.";
            Debug.LogError("[WizardsVRC] " + msg);
            EditorUtility.DisplayDialog("Rebuild MainWorld", msg, "OK");
            return false;
        }

        /// <summary>
        /// New program assets start at ScriptVersion.Unknown and UdonSharp only bumps it in a deferred upgrade pass,
        /// so CopyProxyToUdon would throw "outdated script version" when the builder runs right after creating them.
        /// Our scripts already use current-format serialization, so no source upgrade is needed.
        /// </summary>
        private static void MarkScriptVersionCurrent(UdonSharpProgramAsset program)
        {
            if (program.ScriptVersion >= UdonSharpProgramVersion.CurrentVersion) return;
            program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            EditorUtility.SetDirty(program);
        }

        // ================================================================== select room

        private static void BuildSelectCanvasContents(Canvas canvas, CharacterSelectUI ui)
        {
            RectTransform root = (RectTransform)canvas.transform;
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(ui);

            Panel(root, "Background", Vector2.zero, new Vector2(1600, 900), new Color(0.08f, 0.07f, 0.12f, 0.92f));
            Label(root, "Title", new Vector2(0, 390), new Vector2(1500, 80), "Choose Your Wizard", 56, TextAnchor.MiddleCenter);
            ui.statusLabel = Label(root, "Status", new Vector2(0, -400), new Vector2(1500, 60), "Loading save data...", 32, TextAnchor.MiddleCenter);

            int n = SaveManager.SLOT_COUNT;
            ui.slotNameLabels = new Text[n];
            ui.slotSchoolLabels = new Text[n];
            ui.slotLevelLabels = new Text[n];
            ui.selectButtons = new GameObject[n];
            ui.newButtons = new GameObject[n];
            ui.deleteButtons = new GameObject[n];

            for (int i = 0; i < n; i++)
            {
                float x = -555f + i * 370f;
                RectTransform slot = Panel(root, "Slot" + i, new Vector2(x, -10), new Vector2(340, 640), new Color(0.2f, 0.18f, 0.3f, 1f));
                Panel(slot, "Portrait", new Vector2(0, 170), new Vector2(200, 200), new Color(0.35f, 0.32f, 0.5f, 1f));
                ui.slotNameLabels[i] = Label(slot, "Name", new Vector2(0, 30), new Vector2(320, 50), "Empty Slot", 30, TextAnchor.MiddleCenter);
                ui.slotSchoolLabels[i] = Label(slot, "School", new Vector2(0, -20), new Vector2(320, 40), "", 26, TextAnchor.MiddleCenter);
                ui.slotLevelLabels[i] = Label(slot, "Level", new Vector2(0, -60), new Vector2(320, 40), "", 26, TextAnchor.MiddleCenter);
                ui.selectButtons[i] = Button(slot, "SelectButton", new Vector2(0, -140), new Vector2(280, 70), "Select", new Color(0.2f, 0.5f, 0.3f), target, "SelectSlot" + i);
                ui.newButtons[i] = Button(slot, "NewButton", new Vector2(0, -140), new Vector2(280, 70), "New Character", new Color(0.25f, 0.35f, 0.6f), target, "NewSlot" + i);
                ui.deleteButtons[i] = Button(slot, "DeleteButton", new Vector2(0, -240), new Vector2(280, 60), "Delete", new Color(0.55f, 0.18f, 0.18f), target, "DeleteSlot" + i);
            }

            // School picker
            RectTransform picker = Panel(root, "SchoolPicker", Vector2.zero, new Vector2(1300, 620), new Color(0.05f, 0.05f, 0.08f, 0.97f));
            Label(picker, "Title", new Vector2(0, 240), new Vector2(1200, 70), "Choose a School", 48, TextAnchor.MiddleCenter);
            string[] schools = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
            Color[] colors =
            {
                new Color(0.75f, 0.25f, 0.1f), new Color(0.3f, 0.6f, 0.85f), new Color(0.5f, 0.3f, 0.75f),
                new Color(0.3f, 0.7f, 0.3f), new Color(0.3f, 0.3f, 0.3f), new Color(0.75f, 0.65f, 0.15f),
                new Color(0.7f, 0.5f, 0.3f),
            };
            for (int s = 0; s < schools.Length; s++)
            {
                int row = s < 4 ? 0 : 1;
                int col = row == 0 ? s : s - 4;
                int cols = row == 0 ? 4 : 3;
                float x = (col - (cols - 1) / 2f) * 300f;
                float y = row == 0 ? 90f : -60f;
                Button(picker, "Pick" + schools[s], new Vector2(x, y), new Vector2(270, 110), schools[s], colors[s], target, "Pick" + schools[s]);
            }
            Button(picker, "Cancel", new Vector2(0, -230), new Vector2(260, 70), "Cancel", new Color(0.35f, 0.35f, 0.35f), target, "CancelPicker");
            ui.schoolPickerPanel = picker.gameObject;

            // Delete confirmation
            RectTransform confirm = Panel(root, "ConfirmDelete", Vector2.zero, new Vector2(900, 400), new Color(0.05f, 0.05f, 0.08f, 0.97f));
            ui.confirmLabel = Label(confirm, "Message", new Vector2(0, 70), new Vector2(820, 160), "Delete this character?", 38, TextAnchor.MiddleCenter);
            Button(confirm, "Yes", new Vector2(-170, -110), new Vector2(260, 80), "Delete", new Color(0.6f, 0.15f, 0.15f), target, "ConfirmYes");
            Button(confirm, "No", new Vector2(170, -110), new Vector2(260, 80), "Keep", new Color(0.35f, 0.35f, 0.35f), target, "ConfirmNo");
            ui.confirmPanel = confirm.gameObject;

            picker.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);
        }

        // ================================================================== hub

        /// <summary>
        /// The castle hub: built from the hub zone's map when there is one (see WizardsVRCSceneBuilder.Hub.cs), otherwise
        /// the classic hand-placed castle. Fills in where the chest, pedestal, notice board, arena pad and keep door go.
        /// </summary>
        private static Transform BuildHub(out Canvas hubCanvas, HubLayout hub, Interior interior, WorldDataStore.Data data)
        {
            var root = new GameObject("Hub").transform;

            if (hub.zone != null) BuildHubZone(root, hub, interior, data);
            else
            {
                BuildCastle(root);
                AddDistanceCulling(root.Find("Castle"), DefaultDetailDistance);
            }

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "SpawnLandingPad";
            pad.transform.SetParent(root, false);
            pad.transform.localPosition = hub.spawnPos - new Vector3(0, 0.1f, 0);
            pad.transform.localScale = new Vector3(3f, 0.05f, 3f);
            pad.GetComponent<Renderer>().sharedMaterial = Mat("LandingPad", new Color(0.35f, 0.55f, 0.9f));

            Transform spawn = Child(root, "HubSpawn", hub.spawnPos);
            spawn.localRotation = Quaternion.Euler(0, hub.spawnYaw, 0);
            hub.spawn = spawn;

            var canvasGo = new GameObject("HubCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root, false);
            hubCanvas = MakeWorldCanvas(canvasGo, new Vector2(600, 360), hub.canvasPos, 0.004f);
            canvasGo.transform.localRotation = hub.canvasRot;
            return spawn;
        }

        private static void BuildHubCanvasContents(Canvas canvas, CharacterSelectUI ui, InventoryBook book)
        {
            RectTransform root = (RectTransform)canvas.transform;
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(ui);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            Panel(root, "Background", Vector2.zero, new Vector2(600, 360), new Color(0.08f, 0.07f, 0.12f, 0.9f));
            Label(root, "Title", new Vector2(0, 120), new Vector2(560, 60), "Starfall Castle", 40, TextAnchor.MiddleCenter);
            Button(root, "ChangeCharacter", new Vector2(0, 10), new Vector2(420, 90), "Change Character", new Color(0.25f, 0.35f, 0.6f), target, "ReturnToSelect");
            Button(root, "OpenBackpack", new Vector2(0, -105), new Vector2(420, 90), "Backpack (B)", new Color(0.55f, 0.38f, 0.2f), bookTarget, "ToggleBook");
        }

        // ================================================================== backpack book

        private static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f);
        private static readonly Color Ink = new Color(0.22f, 0.15f, 0.08f);

        private static void BuildInventoryBook(InventoryBook book, PlayerHUD hud, DeckPages deck, QuestPages quests)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            UdonBehaviour hudTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(hud);

            var canvasGo = new GameObject("BookCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(book.transform, false);
            // Wider than the cover so the bookmark tabs on the right edge are inside the VRCUiShape collider.
            MakeWorldCanvas(canvasGo, new Vector2(1960, 1100), Vector3.zero, 0.0006f);
            RectTransform root = (RectTransform)canvasGo.transform;
            book.bookRoot = canvasGo;

            Panel(root, "Cover", Vector2.zero, new Vector2(1800, 1100), new Color(0.36f, 0.2f, 0.1f));
            RectTransform backpackView = Rect(root, "BackpackView", Vector2.zero, new Vector2(1800, 1100));
            book.backpackView = backpackView.gameObject;
            RectTransform left = Panel(backpackView, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            RectTransform right = Panel(backpackView, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);
            BuildDeckPages(root, book, deck, hud);
            BuildQuestPages(root, book, quests);
            BuildSettingsPages(root, book);
            Panel(root, "Spine", Vector2.zero, new Vector2(30, 1060), new Color(0.22f, 0.12f, 0.06f));

            // Bookmark tabs down the right edge: Pack, Deck, Quests, Stats, Notices, Wardrobe, Shop, Settings.
            float TabY(int i) => 437f - i * 125f;
            GameObject Tab(int i, string name, string text, int size, string ev, UdonBehaviour to)
            {
                GameObject b = Button(root, name, new Vector2(935, TabY(i)), new Vector2(80, 115), text, new Color(0.45f, 0.3f, 0.18f), to, ev);
                b.GetComponentInChildren<Text>().fontSize = size;
                return b;
            }
            book.backpackTabImage = Tab(0, "BackpackTab", "Pack", 22, "ShowBackpack", target).GetComponent<Image>();
            book.deckTabImage = Tab(1, "DeckTab", "Deck", 22, "ShowDeck", target).GetComponent<Image>();
            book.questTabImage = Tab(2, "QuestTab", "Quests", 18, "ShowQuests", target).GetComponent<Image>();
            var extraTabs = new List<Image>();
            extraTabs.Add(Tab(3, "StatsTab", "Stats", 20, nameof(InventoryBook.ShowStats), target).GetComponent<Image>());
            GameObject noticeTab = Tab(4, "NoticesTab", "No-\ntices", 18, nameof(InventoryBook.ShowNotices), target);
            extraTabs.Add(noticeTab.GetComponent<Image>());
            extraTabs.Add(Tab(5, "WardrobeTab", "Ward-\nrobe", 18, nameof(InventoryBook.ShowWardrobe), target).GetComponent<Image>());
            extraTabs.Add(Tab(6, "ShopTab", "Shop", 20, nameof(InventoryBook.ShowShop), target).GetComponent<Image>());
            book.extraTabImages = extraTabs.ToArray();
            book.settingsTabImage = Tab(7, "SettingsTab", "Set-\ntings", 18, nameof(InventoryBook.ShowSettings), target).GetComponent<Image>();
            BookTabBadge = noticeTab.transform;
            book.extraViews = new[] { BuildStatsPages(root, book), BuildNoticesPages(root, book), BuildWardrobePages(root, book), BuildShopPages(root, book) };
            book.extraPages = new UdonSharpBehaviour[] { null, null, BookWardrobe, BookShop };
            canvasGo.transform.Find("Spine")?.SetAsLastSibling();

            // ---- left page: character, paper-doll slots, stats
            book.nameLabel = InkLabel(left, "Name", new Vector2(0, 450), new Vector2(800, 60), "Name", 44, TextAnchor.MiddleCenter);
            book.infoLabel = InkLabel(left, "Info", new Vector2(0, 395), new Vector2(800, 45), "Level 1", 30, TextAnchor.MiddleCenter);
            book.goldLabel = InkLabel(left, "Gold", new Vector2(0, 350), new Vector2(800, 45), "Gold: 0", 30, TextAnchor.MiddleCenter);
            Panel(left, "PortraitFrame", new Vector2(0, 60), new Vector2(214, 434), new Color(0.45f, 0.3f, 0.18f));
            RectTransform portrait = Rect(left, "Portrait", new Vector2(0, 60), new Vector2(200, 420));
            var portraitImage = portrait.gameObject.AddComponent<RawImage>();
            portraitImage.raycastTarget = false;
            portraitImage.texture = BuildPortraitCamera(book);

            Vector2[] slotPos =
            {
                new Vector2(-255, 250), new Vector2(-255, 120), new Vector2(-255, -10),
                new Vector2(255, 250), new Vector2(255, 120), new Vector2(255, -10), new Vector2(255, -140),
                new Vector2(-255, -140),
            };
            int slots = CharacterData.EQUIP_SLOTS;
            book.slotIcons = new Image[slots];
            book.slotLabels = new Text[slots];
            for (int s = 0; s < slots; s++)
            {
                GameObject b = Button(left, "Slot" + s, slotPos[s], new Vector2(230, 115), "", new Color(0.55f, 0.48f, 0.38f), target, "Slot" + s);
                book.slotIcons[s] = b.GetComponent<Image>();
                Text t = b.GetComponentInChildren<Text>();
                t.fontSize = 22;
                book.slotLabels[s] = t;
            }

            book.statsLabel = InkLabel(left, "Stats", new Vector2(0, -290), new Vector2(800, 135), "", 23, TextAnchor.MiddleCenter);
            book.statusLabel = InkLabel(left, "Status", new Vector2(0, -385), new Vector2(800, 50), "", 26, TextAnchor.MiddleCenter);
            Button(left, "Close", new Vector2(-250, -460), new Vector2(220, 65), "Close", new Color(0.45f, 0.25f, 0.15f), target, "CloseBook");
            Button(left, "ToggleHud", new Vector2(250, -460), new Vector2(220, 65), "HUD On/Off", new Color(0.45f, 0.25f, 0.15f), hudTarget, "ToggleHud");

            // ---- right page: tabs, grid, paging, details
            InkLabel(right, "Title", new Vector2(0, 385), new Vector2(800, 55), "Backpack", 40, TextAnchor.MiddleCenter);
            string[] tabs = { "All", "Hat", "Robe", "Boots", "Wand", "Athame", "Amulet", "Ring", "Deck", "Potion" };
            book.tabImages = new Image[tabs.Length];
            for (int t = 0; t < tabs.Length; t++)
            {
                GameObject b = Button(right, "Tab" + t, new Vector2(-378 + t * 84, 460), new Vector2(80, 60), tabs[t], new Color(0.45f, 0.3f, 0.18f), target, "Tab" + t);
                b.GetComponentInChildren<Text>().fontSize = 17;
                book.tabImages[t] = b.GetComponent<Image>();
            }

            int cells = InventoryBook.CELLS_PER_PAGE;
            book.cellRoots = new GameObject[cells];
            book.cellIcons = new Image[cells];
            book.cellNames = new Text[cells];
            book.cellCounts = new Text[cells];
            for (int i = 0; i < cells; i++)
            {
                int col = i % 4, row = i / 4;
                var pos = new Vector2(-300 + col * 200, 270 - row * 180);
                GameObject b = Button(right, "Cell" + i, pos, new Vector2(185, 165), "", Color.gray, target, "Cell" + i);
                Text nameText = b.GetComponentInChildren<Text>();
                nameText.fontSize = 20;
                nameText.alignment = TextAnchor.LowerCenter;
                Text countText = Label(b.transform, "Count", new Vector2(55, 60), new Vector2(70, 36), "", 24, TextAnchor.UpperRight);
                book.cellRoots[i] = b;
                book.cellIcons[i] = b.GetComponent<Image>();
                book.cellNames[i] = nameText;
                book.cellCounts[i] = countText;
            }

            Button(right, "PrevPage", new Vector2(-300, -205), new Vector2(150, 60), "<", new Color(0.45f, 0.3f, 0.18f), target, "PrevPage");
            book.pageLabel = InkLabel(right, "Page", new Vector2(0, -205), new Vector2(420, 50), "Page 1 / 1", 26, TextAnchor.MiddleCenter);
            Button(right, "NextPage", new Vector2(300, -205), new Vector2(150, 60), ">", new Color(0.45f, 0.3f, 0.18f), target, "NextPage");

            RectTransform details = Panel(right, "Details", new Vector2(0, -375), new Vector2(810, 250), new Color(0.82f, 0.74f, 0.58f));
            book.detailsPanel = details.gameObject;
            book.detailName = InkLabel(details, "ItemName", new Vector2(-110, 95), new Vector2(570, 50), "", 30, TextAnchor.MiddleLeft);
            book.detailBody = InkLabel(details, "ItemBody", new Vector2(-110, -25), new Vector2(570, 180), "", 22, TextAnchor.UpperLeft);
            book.actionButton = Button(details, "Action", new Vector2(300, -60), new Vector2(180, 80), "Equip", new Color(0.25f, 0.5f, 0.3f), target, "DoAction");
            book.actionLabel = book.actionButton.GetComponentInChildren<Text>();
            details.gameObject.SetActive(false);

            canvasGo.SetActive(false);
        }

        /// <summary>
        /// Camera that films the local avatar for the book's portrait. VRChat draws a full copy of the local avatar
        /// (head included) on the MirrorReflection layer; rendering only that layer shows just the player.
        /// </summary>
        private static RenderTexture BuildPortraitCamera(InventoryBook book)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/AvatarPortrait.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt == null)
            {
                rt = new RenderTexture(256, 512, 24, RenderTextureFormat.ARGB32);
                AssetDatabase.CreateAsset(rt, path);
            }

            int mirrorLayer = LayerMask.NameToLayer("MirrorReflection");
            if (mirrorLayer < 0) mirrorLayer = 18;

            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(book.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.cullingMask = 1 << mirrorLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.75f, 0.68f, 0.55f, 1f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 10f;
            cam.depth = -10f;
            camGo.SetActive(false);
            book.portraitCamera = cam;
            return rt;
        }

        /// <summary>The book's Settings bookmark: your character, HUD / quest arrow toggles, save, go home, change character.</summary>
        private static void BuildSettingsPages(RectTransform root, InventoryBook book)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            Color brown = new Color(0.45f, 0.3f, 0.18f);
            RectTransform view = Rect(root, "SettingsView", Vector2.zero, new Vector2(1800, 1100));
            book.settingsView = view.gameObject;
            RectTransform left = Panel(view, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            RectTransform right = Panel(view, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);

            InkLabel(left, "Title", new Vector2(0, 430), new Vector2(800, 60), "Settings", 44, TextAnchor.MiddleCenter);
            GameObject hudBtn = Button(left, "HudToggle", new Vector2(0, 260), new Vector2(560, 85), "On-screen HUD: On", brown, target, nameof(InventoryBook.SettingToggleHud));
            book.hudSettingLabel = hudBtn.GetComponentInChildren<Text>();
            GameObject guideBtn = Button(left, "GuideToggle", new Vector2(0, 150), new Vector2(560, 85), "Quest arrow: On", brown, target, nameof(InventoryBook.SettingToggleGuide));
            book.guideSettingLabel = guideBtn.GetComponentInChildren<Text>();
            // PS1 wobble: the vertex snap and texture swim (some players find it uncomfortable in VR).
            GameObject warpBtn = Button(left, "WarpToggle", new Vector2(0, 40), new Vector2(560, 85), "PS1 wobble: Off", brown, target, nameof(InventoryBook.SettingToggleWarp));
            book.warpSettingLabel = warpBtn.GetComponentInChildren<Text>();
            Button(left, "SaveNow", new Vector2(0, -70), new Vector2(560, 85), "Save Now", new Color(0.25f, 0.45f, 0.3f), target, nameof(InventoryBook.SettingSaveNow));
            Button(left, "GoHome", new Vector2(0, -180), new Vector2(560, 85), "Return to Starfall Castle", new Color(0.3f, 0.35f, 0.55f), target, nameof(InventoryBook.SettingGoHome));
            book.settingsStatus = InkLabel(left, "Status", new Vector2(0, -290), new Vector2(760, 60), "", 26, TextAnchor.MiddleCenter);
            Button(left, "Close", new Vector2(0, -440), new Vector2(260, 65), "Close", new Color(0.45f, 0.25f, 0.15f), target, nameof(InventoryBook.CloseBook));

            InkLabel(right, "Title", new Vector2(0, 430), new Vector2(800, 60), "Character", 44, TextAnchor.MiddleCenter);
            book.settingsCharacterLabel = InkLabel(right, "Character", new Vector2(0, 280), new Vector2(760, 120), "", 34, TextAnchor.MiddleCenter);
            InkLabel(right, "Hint", new Vector2(0, 60), new Vector2(720, 200),
                "Your wizard is saved automatically.\nChanging character saves first, then takes you back to the room where you pick or create a wizard.",
                24, TextAnchor.MiddleCenter);
            GameObject change = Button(right, "ChangeCharacter", new Vector2(0, -150), new Vector2(560, 95), "Change Character", new Color(0.55f, 0.25f, 0.2f), target, nameof(InventoryBook.SettingChangeCharacter));
            book.changeCharacterLabel = change.GetComponentInChildren<Text>();
            GameObject musicBtn = Button(right, "MusicVolume", new Vector2(0, -290), new Vector2(560, 85), "Music: Full", brown, target, nameof(InventoryBook.SettingCycleMusic));
            book.musicSettingLabel = musicBtn.GetComponentInChildren<Text>();
            view.gameObject.SetActive(false);
        }

        private static void BuildDeckPages(RectTransform root, InventoryBook book, DeckPages deck, PlayerHUD hud)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(deck);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            Color tabOff = new Color(0.45f, 0.3f, 0.18f);

            RectTransform view = Rect(root, "DeckView", Vector2.zero, new Vector2(1800, 1100));
            book.deckView = view.gameObject;
            RectTransform left = Panel(view, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            RectTransform right = Panel(view, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);

            // ---- left page: deck (flip through the deck items you own, equip one)
            InkLabel(left, "Title", new Vector2(0, 450), new Vector2(800, 55), "Decks", 40, TextAnchor.MiddleCenter);
            Button(left, "PrevDeck", new Vector2(-340, 385), new Vector2(90, 55), "<", tabOff, target, nameof(DeckPages.PrevDeck));
            deck.deckNameLabel = InkLabel(left, "DeckName", new Vector2(-40, 385), new Vector2(480, 55), "", 30, TextAnchor.MiddleCenter);
            Button(left, "NextDeck", new Vector2(260, 385), new Vector2(90, 55), ">", tabOff, target, nameof(DeckPages.NextDeck));
            deck.equipDeckButton = Button(left, "EquipDeck", new Vector2(355, 385), new Vector2(90, 55), "Equip", new Color(0.25f, 0.5f, 0.3f), target, nameof(DeckPages.EquipShownDeck));
            deck.equipDeckButton.GetComponentInChildren<Text>().fontSize = 22;
            deck.deckTitle = InkLabel(left, "DeckTitle", new Vector2(0, 320), new Vector2(800, 45), "", 24, TextAnchor.MiddleCenter);

            int deckCells = DeckPages.DECK_CELLS;
            // The deck's spells as small cards (the same art as in battle), each with how many copies it holds.
            deck.deckCellRoots = new GameObject[deckCells];
            deck.deckCellIcons = new Image[deckCells];
            deck.deckCellNames = new Text[deckCells];
            deck.deckCellCounts = new Text[deckCells];
            deck.deckCellPips = new Text[deckCells];
            deck.deckCellArts = new RawImage[deckCells];
            for (int i = 0; i < deckCells; i++)
            {
                int col = i % 5, row = i / 5;
                var card = CardFace(left, "DeckCell" + i, new Vector2(-264 + col * 132, 222 - row * 160), 0.55f, true, target, "DeckCell" + i);
                Text countText = CardText(card.root.transform, "Count", new Vector2(58, -112), new Vector2(80, 44), 34, TextAnchor.MiddleRight, Color.white);
                countText.fontStyle = FontStyle.Bold;
                countText.gameObject.AddComponent<Outline>().effectColor = new Color(0.12f, 0.09f, 0.14f);
                deck.deckCellRoots[i] = card.root;
                deck.deckCellIcons[i] = card.frame;
                deck.deckCellNames[i] = card.name;
                deck.deckCellPips[i] = card.pips;
                deck.deckCellArts[i] = card.art;
                deck.deckCellCounts[i] = countText;
            }
            deck.statusLabel = InkLabel(left, "Status", new Vector2(0, -385), new Vector2(800, 50), "", 26, TextAnchor.MiddleCenter);
            Button(left, "Close", new Vector2(-250, -460), new Vector2(220, 65), "Close", new Color(0.45f, 0.25f, 0.15f), bookTarget, "CloseBook");

            // ---- right page: spellbook
            InkLabel(right, "Title", new Vector2(0, 450), new Vector2(800, 55), "Spellbook", 40, TextAnchor.MiddleCenter);
            string[] filters = { "All", "School", "General" };
            deck.filterImages = new Image[filters.Length];
            for (int f = 0; f < filters.Length; f++)
            {
                GameObject b = Button(right, "Filter" + f, new Vector2(-200 + f * 200, 385), new Vector2(180, 55), filters[f], tabOff, target, "Filter" + f);
                Text t = b.GetComponentInChildren<Text>();
                t.fontSize = 24;
                if (f == DeckPages.FILTER_SCHOOL) deck.schoolFilterLabel = t;
                deck.filterImages[f] = b.GetComponent<Image>();
            }

            int bookCells = DeckPages.BOOK_CELLS;
            // Every spell you know, as a card.
            deck.bookCellRoots = new GameObject[bookCells];
            deck.bookCellIcons = new Image[bookCells];
            deck.bookCellNames = new Text[bookCells];
            deck.bookCellPips = new Text[bookCells];
            deck.bookCellArts = new RawImage[bookCells];
            for (int i = 0; i < bookCells; i++)
            {
                int col = i % 4, row = i / 4;
                var card = CardFace(right, "BookCell" + i, new Vector2(-270 + col * 180, 268 - row * 163), 0.56f, true, target, "BookCell" + i);
                deck.bookCellRoots[i] = card.root;
                deck.bookCellIcons[i] = card.frame;
                deck.bookCellNames[i] = card.name;
                deck.bookCellPips[i] = card.pips;
                deck.bookCellArts[i] = card.art;
            }
            Button(right, "PrevPage", new Vector2(-300, -180), new Vector2(150, 60), "<", tabOff, target, "PrevPage");
            deck.pageLabel = InkLabel(right, "Page", new Vector2(0, -180), new Vector2(420, 50), "Page 1 / 1", 26, TextAnchor.MiddleCenter);
            Button(right, "NextPage", new Vector2(300, -180), new Vector2(150, 60), ">", tabOff, target, "NextPage");

            RectTransform details = Panel(right, "Details", new Vector2(0, -360), new Vector2(810, 270), new Color(0.82f, 0.74f, 0.58f));
            deck.detailsPanel = details.gameObject;
            // The selected spell as a full card, its details beside it.
            var big = CardFace(details, "SpellCard", new Vector2(-305, 0), 0.88f, false, null, null);
            deck.detailFrame = big.frame;
            deck.detailCardName = big.name;
            deck.detailPips = big.pips;
            deck.detailArt = big.art;
            deck.detailAccuracy = big.accuracy;
            deck.detailInfo = big.info;
            deck.detailSchool = big.school;
            deck.detailName = InkLabel(details, "SpellName", new Vector2(0, 100), new Vector2(380, 50), "", 30, TextAnchor.MiddleLeft);
            deck.detailBody = InkLabel(details, "SpellBody", new Vector2(0, -25), new Vector2(380, 190), "", 20, TextAnchor.UpperLeft);
            deck.addButton = Button(details, "Add", new Vector2(300, 40), new Vector2(180, 70), "Add", new Color(0.25f, 0.5f, 0.3f), target, "AddSelected");
            deck.removeButton = Button(details, "Remove", new Vector2(300, -55), new Vector2(180, 70), "Remove", new Color(0.55f, 0.25f, 0.2f), target, "RemoveSelected");
            details.gameObject.SetActive(false);

            view.gameObject.SetActive(false);
        }

        private static Text InkLabel(Transform parent, string name, Vector2 pos, Vector2 size, string text, int fontSize, TextAnchor anchor)
        {
            Text t = Label(parent, name, pos, size, text, fontSize, anchor);
            t.color = Ink;
            return t;
        }

        /// <summary>Show the castle's testing fixtures (notice board panel, Backpack pedestal, arena teleporter pad).</summary>
        private const bool HubTestProps = false;

        private static EventRelay BuildBackpackPedestal(Transform hubRoot, Vector3 at, InventoryBook book)
        {
            Block(hubRoot, "BackpackPedestal", at + new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1f, 0.6f), Mat("Pedestal", new Color(0.5f, 0.5f, 0.55f)));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BackpackBook";
            go.transform.SetParent(hubRoot, false);
            go.transform.localPosition = at + new Vector3(0, 1.07f, 0);
            go.transform.localScale = new Vector3(0.45f, 0.12f, 0.35f);
            go.GetComponent<Renderer>().sharedMaterial = Mat("BookCover", new Color(0.36f, 0.2f, 0.1f));

            var label = new GameObject("BackpackLabel");
            label.transform.SetParent(hubRoot, false);
            label.transform.localPosition = at + new Vector3(0, 1.6f, 0);
            var tm = label.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.05f;
            tm.fontSize = 48;
            tm.color = Color.white;
            tm.text = "Backpack (click)";

            var relay = UdonSharpUndo.AddComponent<EventRelay>(go);
            relay.target = book;
            relay.eventName = "ToggleBook";
            return relay;
        }

        // ================================================================== combat

        private static readonly Vector3 ArenaOrigin = new Vector3(0f, 0f, 400f);

        private static BattleQueuePad BuildQueuePad(Transform hubRoot, Vector3 pos, BattleClient client)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "ArenaTeleporterPad";
            pad.transform.SetParent(hubRoot, false);
            pad.transform.localPosition = pos + new Vector3(0, 0.05f, 0);
            pad.transform.localScale = new Vector3(3f, 0.05f, 3f);
            pad.GetComponent<Renderer>().sharedMaterial = Mat("ArenaPad", new Color(0.55f, 0.3f, 0.85f));

            var trigger = new GameObject("ArenaQueueTrigger");
            trigger.transform.SetParent(hubRoot, false);
            trigger.transform.localPosition = pos + new Vector3(0, 1f, 0);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.6f, 2f, 2.6f);
            var queue = UdonSharpUndo.AddComponent<BattleQueuePad>(trigger);
            queue.client = client;

            client.padLabel = WorldText(hubRoot, "ArenaPadLabel", pos + new Vector3(0, 2.6f, 0), Quaternion.LookRotation(new Vector3(pos.x, 0, pos.z)),
                new Vector2(700, 200), 0.004f, "Boss Arena Teleporter", 48);
            return queue;
        }

        private const string SpellAnimationDir = "Assets/WizardsVRC/SpellAnimations";
        private const string CastSoundDir = "Assets/WizardsVRC/Audio/Cast";

        // Each spell can have a cast cinematic: a prefab in SpellAnimationDir named "<id>.prefab" or "<Spell Name>.prefab".
        // Instances start switched off; BattleClient moves one to the caster and switches it on for that spell's turn.
        private static void AttachSpellAnimations(Transform arenaRoot, CardDatabase db)
        {
            var holder = Child(arenaRoot, "SpellAnimations", Vector3.zero);
            var anims = new GameObject[db.ids.Length];
            int found = 0;
            for (int i = 0; i < db.ids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpellAnimationDir + "/" + db.ids[i] + ".prefab");
                if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpellAnimationDir + "/" + db.names[i] + ".prefab");
                if (prefab == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
                inst.name = db.ids[i] + " " + db.names[i];
                inst.SetActive(false);
                anims[i] = inst;
                found++;
            }
            db.spellAnimations = anims;
            if (found > 0) Debug.Log("[WizardsVRC] Attached " + found + " spell animation(s) from " + SpellAnimationDir);
        }

        private const string TexturesDir = "Assets/WizardsVRC/Textures";
        private const string CardsDir = TexturesDir + "/Cards";

        /// <summary>Imports a pixel-art PNG as a point-filtered UI sprite.</summary>
        private static Sprite SpriteTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[WizardsVRC] Missing " + path); return null; }
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private const float DuelCircleRadius = 9.3f;
        // Matches the seat sockets painted at 200/256 of the disc radius in DuelCircle.png.
        private const float DuelSeatRadius = DuelCircleRadius * 200f / 256f;
        private static readonly string[] FxKindNames = { "Shield", "Blade", "Trap" };
        // Shields at chest height, blades overhead, traps low (icons stand upright, so not on the ground).
        private static readonly float[] FxHeights = { 1.1f, 2.2f, 0.45f };
        private static readonly float[] FxIconSizes = { 0.5f, 0.42f, 0.42f };
        /// <summary>Scale of the 3D shield, blade and trap models (each about a metre across at scale 1).</summary>
        private static readonly float[] Fx3DSizes = { 0.62f, 0.75f, 0.6f };

        // ------------------------------------------------------------------ 3D shields, blades and traps

        /// <summary>Saves a mesh from triangles that are already wound outward (no re-winding), flat-shaded.</summary>
        private static Mesh SaveFxMesh(string name, List<Vector3> tris)
        {
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var verts = new List<Vector3>();
            var idx = new List<int>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
                for (int j = 0; j < 3; j++) { idx.Add(verts.Count); verts.Add(tris[i + j]); }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>A triangle wound to face away from 'inside'.</summary>
        private static void FxTri(List<Vector3> t, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - inside) < 0f) { var x = b; b = c; c = x; }
            t.Add(a); t.Add(b); t.Add(c);
        }

        /// <summary>A box (centre, half sizes), each face wound outward.</summary>
        private static void FxBox(List<Vector3> t, Vector3 c, Vector3 h)
        {
            Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
            int[,] faces = { { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 4, 5, 1 }, { 2, 3, 7, 6 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 } };
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = P((i & 4) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 1) != 0 ? 1 : -1);
            for (int f = 0; f < 6; f++)
            {
                FxTri(t, v[faces[f, 0]], v[faces[f, 1]], v[faces[f, 2]], c);
                FxTri(t, v[faces[f, 0]], v[faces[f, 2]], v[faces[f, 3]], c);
            }
        }

        /// <summary>A flat polygon (XY, convex about 'mid') extruded from z0 to z1, faces wound outward.</summary>
        private static void FxSlab(List<Vector3> t, Vector2[] poly, Vector2 mid, float z0, float z1)
        {
            Vector3 inside = new Vector3(mid.x, mid.y, (z0 + z1) / 2f);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                FxTri(t, new Vector3(mid.x, mid.y, z1), new Vector3(a.x, a.y, z1), new Vector3(b.x, b.y, z1), inside);
                FxTri(t, new Vector3(mid.x, mid.y, z0), new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), inside);
                Vector3 a0 = new Vector3(a.x, a.y, z0), a1 = new Vector3(a.x, a.y, z1), b0 = new Vector3(b.x, b.y, z0), b1 = new Vector3(b.x, b.y, z1);
                FxTri(t, a0, b0, b1, inside);
                FxTri(t, a0, b1, a1, inside);
            }
        }

        /// <summary>A heater shield facing +Z (pointed at the bottom, flat top) with a raised boss.</summary>
        private static Mesh FxShieldMesh()
        {
            var t = new List<Vector3>();
            var poly = new[] { new Vector2(-0.42f, 0.46f), new Vector2(0.42f, 0.46f), new Vector2(0.42f, 0.06f), new Vector2(0.26f, -0.3f),
                               new Vector2(0f, -0.52f), new Vector2(-0.26f, -0.3f), new Vector2(-0.42f, 0.06f) };
            FxSlab(t, poly, new Vector2(0f, 0.05f), -0.05f, 0.05f);
            // Boss: a low four-sided pyramid on the front.
            Vector3 apex = new Vector3(0f, 0.08f, 0.17f), centre = new Vector3(0f, 0.08f, 0.05f);
            var b = new[] { new Vector3(-0.13f, -0.05f, 0.05f), new Vector3(0.13f, -0.05f, 0.05f), new Vector3(0.13f, 0.21f, 0.05f), new Vector3(-0.13f, 0.21f, 0.05f) };
            for (int i = 0; i < 4; i++) FxTri(t, b[i], b[(i + 1) % 4], apex, centre);
            // Rim along the top edge.
            FxBox(t, new Vector3(0f, 0.46f, 0.02f), new Vector3(0.44f, 0.035f, 0.07f));
            return SaveFxMesh("PSX_FxShield", t);
        }

        /// <summary>A sword pointing up: a diamond-section blade with a point, crossguard, grip and pommel.</summary>
        private static Mesh FxBladeMesh()
        {
            var t = new List<Vector3>();
            // Blade: diamond cross-section from the guard up, tapering to a point.
            float y0 = -0.05f, y1 = 0.5f, tip = 0.66f;
            var d0 = new[] { new Vector3(-0.07f, y0, 0f), new Vector3(0f, y0, 0.018f), new Vector3(0.07f, y0, 0f), new Vector3(0f, y0, -0.018f) };
            var d1 = new[] { new Vector3(-0.065f, y1, 0f), new Vector3(0f, y1, 0.016f), new Vector3(0.065f, y1, 0f), new Vector3(0f, y1, -0.016f) };
            Vector3 inside = new Vector3(0f, (y0 + tip) / 2f, 0f);
            Vector3 point = new Vector3(0f, tip, 0f);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                FxTri(t, d0[i], d0[j], d1[j], inside);
                FxTri(t, d0[i], d1[j], d1[i], inside);
                FxTri(t, d1[i], d1[j], point, inside);
            }
            FxBox(t, new Vector3(0f, -0.08f, 0f), new Vector3(0.19f, 0.03f, 0.04f));     // crossguard
            FxBox(t, new Vector3(0f, -0.22f, 0f), new Vector3(0.03f, 0.11f, 0.03f));     // grip
            FxBox(t, new Vector3(0f, -0.36f, 0f), new Vector3(0.055f, 0.035f, 0.055f));  // pommel
            return SaveFxMesh("PSX_FxBlade", t);
        }

        /// <summary>A spiked trap lying flat: an octagonal iron band with eight teeth pointing up.</summary>
        private static Mesh FxTrapMesh()
        {
            var t = new List<Vector3>();
            const int n = 8;
            float ro = 0.44f, ri = 0.33f, h = 0.07f;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n, am = (a0 + a1) / 2f;
                Vector3 o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 mid = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * ((ro + ri) / 2f) + Vector3.up * (h / 2f);
                Vector3 up = Vector3.up * h;
                // Band segment (outer, inner, top and bottom faces).
                FxTri(t, o0 * ro, o1 * ro, o1 * ro + up, mid); FxTri(t, o0 * ro, o1 * ro + up, o0 * ro + up, mid);
                FxTri(t, o0 * ri, o1 * ri + up, o1 * ri, mid); FxTri(t, o0 * ri, o0 * ri + up, o1 * ri + up, mid);
                FxTri(t, o0 * ri + up, o1 * ro + up, o1 * ri + up, mid); FxTri(t, o0 * ri + up, o0 * ro + up, o1 * ro + up, mid);
                FxTri(t, o0 * ri, o1 * ri, o1 * ro, mid); FxTri(t, o0 * ri, o1 * ro, o0 * ro, mid);
                // A tooth: a three-sided spike standing on the band.
                Vector3 c = mid + Vector3.up * (h / 2f);
                Vector3 side = new Vector3(-Mathf.Sin(am), 0f, Mathf.Cos(am)) * 0.06f, radial = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * 0.04f;
                Vector3 p0 = c - side - radial, p1 = c + side - radial, p2 = c + radial, top = c + Vector3.up * 0.2f;
                Vector3 inside = (p0 + p1 + p2) / 3f + Vector3.up * 0.04f;
                FxTri(t, p0, p1, top, inside); FxTri(t, p1, p2, top, inside); FxTri(t, p2, p0, top, inside);
            }
            return SaveFxMesh("PSX_FxTrap", t);
        }
        private const string FxIconDir = TexturesDir + "/FxIcons";
        // Seats fill in index order (BattleManager takes the first free one), so index 0 is each side's right-most seat
        // as seen by whoever sits on that side (facing the centre), then leftwards. Wizards sit on the south half
        // facing north (their right = +X); monsters on the north half facing south (their right = -X).
        private static readonly float[] WizardSeatAngles = { 112.5f, 157.5f, 202.5f, 247.5f };
        private static readonly float[] MonsterSeatAngles = { 292.5f, 337.5f, 22.5f, 67.5f };
        // Segments in DuelSeatGauge.png (Tools/make_seat_gauge.py SEGMENTS).
        private const int SeatGaugeSegments = 24;

        private static Texture2D PixelTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.filterMode != FilterMode.Point || !importer.alphaIsTransparency))
            {
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CutoutMat(string name, Texture2D tex, Color color, Color emission)
        {
            Material mat = Mat(name, color);
            mat.mainTexture = tex;
            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.SetTexture("_EmissionMap", tex);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Per seat: a glow ring on the floor (BattleClient picks the ally / enemy material and spins it) and a click box
        /// round the combatant that sends Target&lt;n&gt;, both off until a spell that can go there is picked.
        /// </summary>
        private static void BuildSeatTargets(BattleClient client)
        {
            int slots = BattleManager.SLOTS;
            client.seatGlows = new Renderer[slots];
            client.seatPickers = new GameObject[slots];
            // Unlit and additive: they glow over the ring's art whatever the light.
            client.seatGlowAllyMat = SpriteQuadMat("SeatGlowAlly", "SeatGlowAlly", true);
            client.seatGlowEnemyMat = SpriteQuadMat("SeatGlowEnemy", "SeatGlowEnemy", true);
            UdonSharpBehaviour target = client;
            for (int s = 0; s < slots; s++)
            {
                Transform seat = client.seats[s];
                float sc = client.seatScales != null && s < client.seatScales.Length ? Mathf.Max(1f, client.seatScales[s]) : 1f;
                GameObject glow = FlatDecal(seat, "TargetGlow", new Vector3(0, 0.04f, 0), 2.6f * sc, client.seatGlowEnemyMat);
                glow.isStatic = false;
                glow.SetActive(false);
                client.seatGlows[s] = glow.GetComponent<Renderer>();

                var pick = new GameObject("TargetPick");
                pick.transform.SetParent(seat, false);
                var box = pick.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0, 1.2f * sc, 0);
                box.size = new Vector3(1.8f * sc, 2.6f * sc, 1.8f * sc);
                var relay = UdonSharpUndo.AddComponent<EventRelay>(pick);
                relay.target = target;
                relay.eventName = "Target" + s;
                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(relay);
                if (backing != null)
                {
                    backing.interactText = "Cast here";
                    backing.proximity = 40f;   // click any seat from your own, right across the ring
                }
                UdonSharpEditorUtility.CopyProxyToUdon(relay);
                pick.SetActive(false);
                client.seatPickers[s] = pick;
            }
        }

        /// <summary>The duel clock's dial material (each client makes its own copy at runtime to set the time left on).</summary>
        private static Material DuelClockMat()
        {
            string path = MaterialsDir + "/DuelClock.mat";
            Shader sh = Shader.Find("WizardsVRC/DuelClock");
            if (sh == null) sh = Shader.Find("Unlit/Transparent Cutout");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = sh;
            mat.mainTexture = PixelTexture(TexturesDir + "/DuelClock.png");
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>An unlit cut-out material for a texture (flat art on the ground that shouldn't take the light).</summary>
        private static Material UnlitCutoutMat(string name, Texture2D tex)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            Shader sh = Shader.Find("Unlit/Transparent Cutout");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = sh;
            mat.mainTexture = tex;
            mat.SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A flat textured quad lying on the ground (texture top = +Z).</summary>
        private static GameObject FlatDecal(Transform parent, string name, Vector3 localPos, float size, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            return quad;
        }

        /// <summary>One encounter = synced BattleManager + local BattleClient (+ wanderers), wired to the shared databases.</summary>
        private static BattleClient MakeEncounter(Transform systems, string name, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, Transform hubReturn, int[] roster, bool wander, System.Collections.Generic.List<BattleClient> all)
        {
            var holder = Child(systems, "Encounter_" + name, Vector3.zero);
            var manager = UdonSharpUndo.AddComponent<BattleManager>(Child(holder, "BattleManager", Vector3.zero).gameObject);
            var client = UdonSharpUndo.AddComponent<BattleClient>(Child(holder, "BattleClient", Vector3.zero).gameObject);
            manager.cardDatabase = cardDb;
            manager.client = client;
            // The roster is padded out to all four monster seats with more of its followers (every seat then has a body to
            // show), and rosterSize keeps the encounter's own size: the fight's limit is that plus one per extra wizard.
            manager.rosterSize = roster != null ? roster.Length : 0;
            manager.roster = PadRoster(roster, cardDb);
            manager.wanderMode = wander;
            manager.scaleRosterWithParty = !wander;
            client.manager = manager;
            client.playerState = playerState;
            client.itemDatabase = itemDb;
            client.cardDatabase = cardDb;
            client.hubReturn = hubReturn;
            all.Add(client);
            return client;
        }

        /// <summary>
        /// The roster filled out to four entries by repeating its followers (everything after a boss; everything for a
        /// plain group). A boss alone has nothing to repeat and stays as it is.
        /// </summary>
        private static int[] PadRoster(int[] roster, CardDatabase db)
        {
            if (roster == null || roster.Length == 0 || roster.Length >= BattleManager.SIDE) return roster;
            bool boss = db != null && db.IsBossMonster(roster[0]);
            int from = boss ? 1 : 0;
            if (roster.Length - from <= 0) return roster;
            var padded = new int[BattleManager.SIDE];
            for (int i = 0; i < padded.Length; i++)
                padded[i] = i < roster.Length ? roster[i] : roster[from + (i - roster.Length) % (roster.Length - from)];
            return padded;
        }

        private static void BuildArena(BattleClient client, BattleClient[] lanes)
        {
            var root = new GameObject("BossArena").transform;
            root.position = ArenaOrigin;
            Block(root, "Ground", new Vector3(0, -0.25f, 0), new Vector3(130, 0.5f, 80), Mat("ArenaGround", new Color(0.3f, 0.28f, 0.33f)));
            Material stone = Mat("ArenaStone", new Color(0.45f, 0.42f, 0.48f));
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -26f : 26f, z = i < 2 ? -26f : 26f;
                Block(root, "Pillar" + i, new Vector3(x, 4f, z), new Vector3(2f, 8f, 2f), stone);
            }
            client.stagingSpots = new Transform[BattleManager.SIDE];
            for (int k = 0; k < BattleManager.SIDE; k++)
                client.stagingSpots[k] = Child(root, "ArrivalSpot" + k, new Vector3(-22f, 0.05f, -9f + k * 1.5f));
            for (int k = 0; k < BattleManager.SIDE; k++)
                client.stagingSpots[k].localRotation = Quaternion.Euler(0, 90f, 0);
            var arrivalPad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrivalPad.name = "ArrivalPad";
            arrivalPad.transform.SetParent(root, false);
            arrivalPad.transform.localPosition = new Vector3(-22f, 0.05f, -6.75f);
            arrivalPad.transform.localScale = new Vector3(3.5f, 0.06f, 6f);
            arrivalPad.GetComponent<Renderer>().sharedMaterial = Mat("ArenaPad", new Color(0.55f, 0.3f, 0.85f));
            Object.DestroyImmediate(arrivalPad.GetComponent<Collider>());

            // Cast cinematic prefabs are shared by every encounter (BattleClient moves them to the caster).
            AttachSpellAnimations(root, client.cardDatabase);
            BuildEncounter(root, Vector3.zero, "Duel", client, 0);
            // Two wandering-monster lanes flank the boss circle: walk into a monster to start that lane's fight.
            for (int i = 0; i < lanes.Length; i++)
                BuildEncounter(root, new Vector3(i == 0 ? -44f : 44f, 0f, 0f), "Lane" + i, lanes[i], 3);
            WorldText(root, "LaneSign", new Vector3(0, 4f, -30f), Quaternion.identity, new Vector2(1600, 300), 0.005f,
                "Wandering monsters roam the circles to the east and west.\nWalk into one to start a fight!", 44);
        }

        /// <summary>
        /// A duel circle with everything a fight needs: seats, monster bodies, labels, orbiting shield/blade/trap icons,
        /// summoned-minion bodies, cast effects, the hand panel and (wanderCount > 0) wandering monsters circling it.
        /// Everything is positioned from the circle's own transforms so it works wherever it is placed.
        /// </summary>
        private static void BuildEncounter(Transform parent, Vector3 localPos, string name, BattleClient client, int wanderCount)
        {
            Transform root = Child(parent, "Encounter" + name, localPos);
            if (wanderCount > 0)
            {
                Transform back = Child(root, "ReturnSpot", new Vector3(0, 0.05f, -(DuelCircleRadius + 7f)));
                back.localRotation = Quaternion.identity;
                client.hubReturn = back;
            }

            var ring = new GameObject("DuelRing");
            ring.transform.SetParent(root, false);
            Material ringMat = Mat("DuelRing", new Color(0.95f, 0.8f, 0.35f));
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", new Color(0.9f, 0.7f, 0.25f) * 1.5f);
            // Duel circle: seat band (fixed), turning sigil and rune band, and the start flash (WizardsVRCSceneBuilder.CombatFx.cs).
            BuildCircleLayers(ring.transform, name, client);

            // Turn clock in the middle of the ring: the hand sweeps the planning timer, then points at each caster.
            var clock = Child(ring.transform, "TurnClock", Vector3.zero);
            client.arenaCenter = clock;
            clock.localScale = Vector3.one * 1.5f;
            client.engageRadius = DuelCircleRadius + 5f;
            // The clock is set into the floor art: a dial whose rim of rune segments goes dark as the planning time runs
            // out (gold, then red and pulsing near the end; WizardsVRC/DuelClock shader), and a gold hand that sweeps the
            // timer, then points at each caster in turn.
            var dialMat = DuelClockMat();
            GameObject dial = FlatDecal(clock, "ClockDial", new Vector3(0, 0.02f, 0), 2.6f, dialMat);
            dial.isStatic = false;
            client.clockFace = dial.GetComponent<Renderer>();
            var hand = Child(clock, "Hand", new Vector3(0, 0.035f, 0));
            FlatDecal(hand, "HandSprite", Vector3.zero, 2.6f, UnlitCutoutMat("DuelClockHand", PixelTexture(TexturesDir + "/DuelClockHand.png"))).isStatic = false;
            client.clockHand = hand;
            client.clockLabel = WorldText(ring.transform, "ClockLabel", new Vector3(0, 1.1f, 0), Quaternion.identity, new Vector2(700, 160), 0.004f, "", 44);

            Texture2D seatTex = PixelTexture(TexturesDir + "/DuelSeatRing.png");
            Material seatEmpty = CutoutMat("DuelSeatEmpty", seatTex, new Color(0.45f, 0.42f, 0.5f), new Color(0.08f, 0.08f, 0.1f));
            client.seatEmptyMat = seatEmpty;
            client.seatSchoolMats = new Material[SchoolLabels.Length];
            for (int k = 0; k < SchoolLabels.Length; k++)
                client.seatSchoolMats[k] = CutoutMat("DuelSeat" + SchoolLabels[k], seatTex, SchoolColors[k], SchoolColors[k] * 1.6f);
            client.seatRings = new Renderer[BattleManager.SLOTS];
            client.seatScales = new float[BattleManager.SLOTS];
            // The two halves read clearly: cool-tinted empty seats for wizards (south), warm for monsters (north), and a
            // glowing line across the circle between them.
            client.seatEmptyMat = CutoutMat("DuelSeatEmptyWizard", seatTex, new Color(0.45f, 0.55f, 0.8f), new Color(0.05f, 0.08f, 0.16f));
            client.seatEmptyMonsterMat = CutoutMat("DuelSeatEmptyMonster", seatTex, new Color(0.8f, 0.45f, 0.4f), new Color(0.16f, 0.05f, 0.04f));
            seatEmpty = client.seatEmptyMat;
            client.seatGaugeMat = SeatGaugeMat();
            client.gaugeSchoolColors = new[]
            {
                new Color(1f, 0.36f, 0.1f), new Color(0.22f, 0.7f, 1f), new Color(0.72f, 0.36f, 1f), new Color(0.2f, 0.9f, 0.3f),
                new Color(0.72f, 0.68f, 0.8f), new Color(1f, 0.8f, 0.08f), new Color(0.96f, 0.6f, 0.2f),
            };
            // Shields (chest height), blades (over the head) and traps (at the feet) orbit each seat while they're active.
            client.fxMats = new Material[SchoolLabels.Length + 1];
            for (int k = 0; k <= SchoolLabels.Length; k++)
            {
                Color c = k < SchoolLabels.Length ? SchoolColors[k] : new Color(0.85f, 0.85f, 0.9f);
                Material fm = Mat("DuelFx" + (k < SchoolLabels.Length ? SchoolLabels[k] : "General"), c);
                fm.EnableKeyword("_EMISSION");
                fm.SetColor("_EmissionColor", c * 1.3f);
                client.fxMats[k] = fm;
            }
            // Icon art per kind and school (Textures/FxIcons/<Shield|Blade|Trap><School|General>.png), plus a bright
            // version that blinks when one breaks. Index kind * 8 + school (7 = General).
            client.fxIconMats = new Material[3 * 8];
            client.fxIconFlashMats = new Material[3 * 8];
            client.fxIconTextures = new Texture[3 * 8];
            for (int kind = 0; kind < 3; kind++)
                for (int k = 0; k < 8; k++)
                {
                    string label = FxKindNames[kind] + (k < SchoolLabels.Length ? SchoolLabels[k] : "General");
                    Texture2D tex = PixelTexture(FxIconDir + "/" + label + ".png");
                    if (tex == null) { Debug.LogWarning("[WizardsVRC] Missing effect icon " + FxIconDir + "/" + label + ".png"); continue; }
                    client.fxIconTextures[kind * 8 + k] = tex;
                    client.fxIconMats[kind * 8 + k] = CutoutMat("FxIcon" + label, tex, Color.white, Color.white * 0.6f);
                    client.fxIconFlashMats[kind * 8 + k] = CutoutMat("FxIconFlash" + label, tex, Color.white, Color.white * 3f);
                }
            int fxMax = BattleManager.FX_MAX;
            client.fxOrbits = new Transform[BattleManager.SLOTS * 3];
            client.fxIcons = new Renderer[BattleManager.SLOTS * 3 * fxMax];

            int slots = BattleManager.SLOTS, side = BattleManager.SIDE;
            client.seats = new Transform[slots];
            client.overheadLabels = new Text[slots];
            client.monsterBodies = new GameObject[side];
            for (int s = 0; s < slots; s++)
            {
                bool monster = s >= side;
                int i = s % side;
                // 8 seats evenly round the circle: wizards on the south half, monsters facing them on the north half.
                // Each side fills from its own right-most seat (see WizardSeatAngles).
                float ang = monster ? MonsterSeatAngles[i] : WizardSeatAngles[i];
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                Vector3 pos = dir * DuelSeatRadius + new Vector3(0, 0.05f, 0);
                Transform seat = Child(root, (monster ? "MonsterSpot" : "WizardSeat") + i, pos);
                seat.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
                client.seats[s] = seat;

                GameObject marker = FlatDecal(seat, "SeatRing", new Vector3(0, -0.02f, 0), 1.75f, seatEmpty);
                marker.transform.SetParent(ring.transform, true);
                client.seatRings[s] = marker.GetComponent<Renderer>();

                float labelHeight = 2.5f;
                float seatScale = 1f;
                if (monster)
                {
                    // Seat i always holds roster[i] (BattleManager.StartBattle), so build that monster's own body here.
                    int[] rosterIds = client.manager != null ? client.manager.roster : null;
                    int seatMonster = rosterIds != null && i < rosterIds.Length ? rosterIds[i] : 0;
                    GameObject body = MonsterBody(seat, client.cardDatabase, seatMonster, i == 0 ? "Boss" : "Minion" + i);
                    body.SetActive(false);
                    client.monsterBodies[i] = body;
                    seatScale = seatMonster > 0 ? client.cardDatabase.MonsterBodyScale(seatMonster) : 1f;
                    labelHeight = seatScale * 2.2f + 0.5f;
                }
                client.seatScales[s] = seatScale;
                client.overheadLabels[s] = WorldText(root, "Label" + s, pos + new Vector3(0, labelHeight, 0), Quaternion.identity,
                    new Vector2(600, 160), 0.004f, "", 36);

                float fxScale = Mathf.Max(1f, seatScale);
                for (int kind = 0; kind < 3; kind++)
                {
                    Transform orbit = Child(ring.transform, "Fx" + FxKindNames[kind] + s, pos + new Vector3(0, FxHeights[kind] * fxScale, 0));
                    orbit.localScale = Vector3.one * fxScale;
                    client.fxOrbits[s * 3 + kind] = orbit;
                    for (int k = 0; k < fxMax; k++)
                    {
                        // A little 3D shield, sword or spiked trap in the school's glowing colour, circling the combatant
                        // (BattleClient turns and animates it).
                        var icon = new GameObject(FxKindNames[kind] + k);
                        icon.transform.SetParent(orbit, false);
                        icon.transform.localScale = Vector3.one * Fx3DSizes[kind];
                        icon.AddComponent<MeshFilter>().sharedMesh = kind == 0 ? FxShieldMesh() : kind == 1 ? FxBladeMesh() : FxTrapMesh();
                        var ir = icon.AddComponent<MeshRenderer>();
                        ir.sharedMaterial = client.fxMats[SchoolLabels.Length];
                        ir.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        icon.SetActive(false);
                        client.fxIcons[(s * 3 + kind) * fxMax + k] = ir;
                    }
                }
            }

            // Pips in front of every seat, with swirling motes.
            BuildSeatPips(ring.transform, client);
            // With a spell picked, the seats it can go to glow (green allies, red enemies) and can be clicked.
            BuildSeatTargets(client);

            // The round / cast / result banner: big, high over the middle of the ring, readable from every seat.
            client.bannerLabel = WorldText(root, "Banner", new Vector3(0, 6.4f, 0), Quaternion.identity, new Vector2(1800, 480), 0.008f, "", 64);

            var divider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            divider.name = "SideDivider";
            divider.transform.SetParent(ring.transform, false);
            divider.transform.localPosition = new Vector3(0, 0.035f, 0);
            divider.transform.localScale = new Vector3(DuelCircleRadius * 2f - 1.2f, 0.02f, 0.12f);
            divider.GetComponent<Renderer>().sharedMaterial = ringMat;
            divider.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(divider.GetComponent<Collider>());

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "SpellOrb";
            orb.transform.SetParent(root, false);
            orb.transform.localScale = Vector3.one * 0.35f;
            Material orbMat = Mat("SpellOrb", new Color(1f, 0.9f, 0.5f));
            orbMat.EnableKeyword("_EMISSION");
            orbMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.3f) * 2f);
            orb.GetComponent<Renderer>().sharedMaterial = orbMat;
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.SetActive(false);
            client.orb = orb.transform;
            client.orbRenderer = orb.GetComponent<Renderer>();

            // School cast effect (all parented to the ring): the caster's seat ring flares and rises, the school's glyph
            // pops up over their head, the orb flies in the school colour and bursts on the target.
            var castRing = FlatDecal(ring.transform, "CastRing", Vector3.zero, 1.9f, seatEmpty);
            castRing.SetActive(false);
            client.castRing = castRing.transform;
            client.castRingRenderer = castRing.GetComponent<Renderer>();
            var glyph = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glyph.name = "CastGlyph";
            glyph.transform.SetParent(ring.transform, false);
            Object.DestroyImmediate(glyph.GetComponent<Collider>());
            var glyphR = glyph.GetComponent<Renderer>();
            glyphR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glyph.SetActive(false);
            client.castGlyph = glyphR;
            // The caster draws their school's symbol in the air in front of them (WizardsVRC/GlyphDraw; the data maps and
            // pen paths come from Tools/make_glyph_draw.py). BattleClient copies the material per ring and drives it.
            Material drawMat = GlyphDrawMat();
            if (drawMat != null) glyphR.sharedMaterial = drawMat;
            client.castGlyphMats = new Material[0];
            int symbols = GlyphPenData.Labels.Length;
            client.glyphDrawTex = new Texture2D[symbols];
            for (int k = 0; k < symbols; k++)
            {
                client.glyphDrawTex[k] = DataTexture(TexturesDir + "/CastDraw/GlyphDraw" + GlyphPenData.Labels[k] + ".png");
                if (client.glyphDrawTex[k] == null) Debug.LogWarning("[WizardsVRC] Missing drawn symbol " + GlyphPenData.Labels[k] + " (run Tools/make_glyph_draw.py).");
            }
            client.glyphColors = (Color[])GlyphColors.Clone();
            client.glyphEdgeColors = (Color[])GlyphEdgeColors.Clone();
            client.glyphPenU = (float[])GlyphPenData.U.Clone();
            client.glyphPenV = (float[])GlyphPenData.V.Clone();
            client.glyphPenJump = (int[])GlyphPenData.Jump.Clone();
            client.glyphPenSamples = GlyphPenData.Samples;
            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "ImpactBurst";
            burst.transform.SetParent(ring.transform, false);
            Object.DestroyImmediate(burst.GetComponent<Collider>());
            burst.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            burst.SetActive(false);
            client.impactBurst = burst.transform;
            client.impactRenderer = burst.GetComponent<Renderer>();
            // Cast sound per school (Audio/Cast/FX_CastingSound_<School>.mp3), played from the caster's seat.
            var castAudio = Child(ring.transform, "CastAudio", Vector3.zero).gameObject.AddComponent<AudioSource>();
            castAudio.playOnAwake = false;
            castAudio.spatialBlend = 0.7f;
            castAudio.minDistance = 6f;
            castAudio.maxDistance = 40f;
            castAudio.rolloffMode = AudioRolloffMode.Linear;
            client.castAudio = castAudio;
            client.castSounds = new AudioClip[SchoolLabels.Length];
            for (int k = 0; k < SchoolLabels.Length; k++)
            {
                client.castSounds[k] = AssetDatabase.LoadAssetAtPath<AudioClip>(CastSoundDir + "/FX_CastingSound_" + SchoolLabels[k] + ".mp3");
                if (client.castSounds[k] == null) Debug.LogWarning("[WizardsVRC] Missing cast sound for " + SchoolLabels[k] + " in " + CastSoundDir);
                else if (client.manager != null && k < client.manager.castSeconds.Length) client.manager.castSeconds[k] = client.castSounds[k].length;
            }
            // General spells (Tools/make_cast_sound.py): same shape as the school ones, released at 2 s.
            client.generalCastSound = AssetDatabase.LoadAssetAtPath<AudioClip>(CastSoundDir + "/FX_CastingSound_General.mp3")
                                      ?? AssetDatabase.LoadAssetAtPath<AudioClip>(CastSoundDir + "/FX_CastingSound_General.wav");
            if (client.generalCastSound == null) Debug.LogWarning("[WizardsVRC] Missing cast sound for General spells in " + CastSoundDir);
            else if (client.manager != null)
            {
                client.manager.generalCastSeconds = client.generalCastSound.length;
                client.manager.generalCastPeakSeconds = 2f;
            }

            // School-coloured particle burst that explodes off the caster at the cast sound's peak.
            client.castBursts = new ParticleSystem[SchoolLabels.Length + 1];
            for (int k = 0; k <= SchoolLabels.Length; k++)
            {
                string label = k < SchoolLabels.Length ? SchoolLabels[k] : "General";
                Color c = k < SchoolLabels.Length ? SchoolColors[k] : new Color(0.9f, 0.9f, 0.95f);
                var go = Child(ring.transform, "CastBurst" + label, Vector3.zero).gameObject;
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.5f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
                main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.6f));
                main.gravityModifier = 0.35f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 200;
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90), new ParticleSystem.Burst(0.12f, 40) });
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.35f;
                var sizeLife = ps.sizeOverLifetime;
                sizeLife.enabled = true;
                sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
                var pr = go.GetComponent<ParticleSystemRenderer>();
                // Same emissive school material as the shield/blade icons: the built-in particle shaders can be stripped
                // from the VRChat build (they rendered as magenta), so stay on the Standard shader.
                Material pm = client.fxMats[k];
                pr.sharedMaterial = pm;
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                client.castBursts[k] = ps;
            }

            // The pen tip drawing the symbol, and each school's sparks trailing off it.
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "GlyphPenTip";
            tip.transform.SetParent(ring.transform, false);
            tip.transform.localScale = Vector3.one * 0.11f;
            Object.DestroyImmediate(tip.GetComponent<Collider>());
            tip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tip.SetActive(false);
            client.penTip = tip.transform;
            client.penTipRenderer = tip.GetComponent<Renderer>();
            client.glyphTrails = new ParticleSystem[SchoolLabels.Length + 1];
            for (int k = 0; k <= SchoolLabels.Length; k++)
                client.glyphTrails[k] = GlyphTrail(ring.transform, k, client.fxMats[k]);

            // A fizzle's puff of grey smoke, and its sound (Tools/make_fizzle_sound.py).
            client.fizzlePuff = FizzlePuff(ring.transform);
            client.fizzleSound = AssetDatabase.LoadAssetAtPath<AudioClip>(CastSoundDir + "/FX_Fizzle.wav");
            if (client.fizzleSound == null) Debug.LogWarning("[WizardsVRC] Missing fizzle sound " + CastSoundDir + "/FX_Fizzle.wav (run Tools/make_fizzle_sound.py).");

            // Breaking shields / blades / traps, floating combat text and hit / heal / defeat sounds.
            BuildCombatFeedback(ring.transform, client);

            // Summoned minions: one body per seat, parented to the ring. BattleClient grows them at the ring's centre and
            // walks them to their seat, so it works wherever the ring is placed.
            client.minionBodies = new Transform[slots];
            client.minionSkins = new Renderer[slots * 2];
            for (int s = 0; s < slots; s++)
            {
                var mb = Child(ring.transform, "SummonedMinion" + s, client.seats[s].localPosition);
                var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                torso.name = "Body";
                torso.transform.SetParent(mb, false);
                torso.transform.localPosition = new Vector3(0, 0.75f, 0);
                torso.transform.localScale = new Vector3(0.8f, 0.75f, 0.8f);
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(mb, false);
                head.transform.localPosition = new Vector3(0, 1.75f, 0.05f);
                head.transform.localScale = Vector3.one * 0.6f;
                foreach (var part in new[] { torso, head })
                {
                    Object.DestroyImmediate(part.GetComponent<Collider>());
                    part.GetComponent<Renderer>().sharedMaterial = client.fxMats[SchoolLabels.Length];
                }
                client.minionSkins[s * 2] = torso.GetComponent<Renderer>();
                client.minionSkins[s * 2 + 1] = head.GetComponent<Renderer>();
                mb.gameObject.SetActive(false);
                client.minionBodies[s] = mb;
            }
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "SummonFlash";
            flash.transform.SetParent(ring.transform, false);
            Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.GetComponent<Renderer>().sharedMaterial = orbMat;
            flash.SetActive(false);
            client.summonFlash = flash.transform;

            BuildHandPanel(root, client);
            // Lift everything in the ring clear of zone paving and lawns (their slabs stand ~7 cm proud of the ground).
            ring.transform.localPosition = new Vector3(0, RingLift, 0);
            ring.SetActive(false);
            client.ring = ring;

            // Wandering monsters circle the ring; their look comes from the roster's monster.
            int[] roster = client.manager != null ? client.manager.roster : null;
            for (int w = 0; w < wanderCount && roster != null && roster.Length > 0; w++)
            {
                int monsterId = roster[w % roster.Length];
                float ms = client.cardDatabase.MonsterBodyScale(monsterId);
                Transform wt = Child(root, "Wanderer" + w, Vector3.zero);
                var wanderer = UdonSharpUndo.AddComponent<Wanderer>(wt.gameObject);
                wanderer.body = MonsterBody(wt, client.cardDatabase, monsterId, "Body");
                wanderer.label = WanderLabel(wt, ms);
                wanderer.manager = client.manager;
                wanderer.client = client;
                wanderer.cardDatabase = client.cardDatabase;
                wanderer.monsterId = monsterId;
                wanderer.center = client.arenaCenter;
                wanderer.radius = DuelCircleRadius + 3f;
                wanderer.speed = 1.1f + 0.2f * w;
                wanderer.phaseOffset = w * Mathf.PI * 2f / Mathf.Max(1, wanderCount);
            }
            // Every word in the fight (labels, banner, clock, floating numbers, the hand and its cards) in the PSX pixel font.
            ApplyCombatFont(root);
        }

        private static void BuildHandPanel(Transform arenaRoot, BattleClient client)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(client);
            var canvasGo = new GameObject("HandCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(arenaRoot, false);
            MakeWorldCanvas(canvasGo, new Vector2(1600, 960), new Vector3(0, 1.2f, -2.4f), 0.0006f);
            RectTransform root = (RectTransform)canvasGo.transform;
            client.handRoot = canvasGo;

            Color gold = new Color(1f, 0.85f, 0.45f);
            // See-through backing so the duel stays visible behind the cards.
            Panel(root, "Background", Vector2.zero, new Vector2(1600, 960), new Color(0.06f, 0.05f, 0.1f, 0.38f));
            Panel(root, "TopBar", new Vector2(0, 385), new Vector2(1560, 160), new Color(0.12f, 0.1f, 0.2f, 0.55f));
            GoldRule(root, "TopRule", new Vector2(0, 300), 1480f);
            client.partyLabel = Label(root, "Party", new Vector2(-470, 385), new Vector2(600, 150), "", 18, TextAnchor.MiddleLeft);
            client.statusLabel = Label(root, "Status", new Vector2(320, 385), new Vector2(900, 150), "", 24, TextAnchor.MiddleLeft);

            // ---- targets, laid out like the duel circle: enemies on the far row, allies on the near row
            int slots = BattleManager.SLOTS;
            client.targetButtons = new GameObject[slots];
            client.targetLabels = new Text[slots];
            // Row titles on carved boards: red for the enemies' row, blue for your side.
            TitlePlate(root, "EnemiesTitle", new Vector2(-690, 240), new Vector2(170, 56), "Enemies", new Color(1f, 0.55f, 0.5f), 20);
            TitlePlate(root, "AlliesTitle", new Vector2(-690, 140), new Vector2(170, 56), "Allies", new Color(0.55f, 0.75f, 1f), 20);
            for (int t = 0; t < slots; t++)
            {
                bool enemy = t >= BattleManager.SIDE;
                int i = t % BattleManager.SIDE;
                Color c = enemy ? new Color(0.55f, 0.2f, 0.18f) : new Color(0.2f, 0.35f, 0.6f);
                // Same left-to-right order the wizard sees in the circle: monster seat 0 is on the far left, wizard
                // seat 0 on the near right.
                float bx = enemy ? -420 + i * 290 : 450 - i * 290;
                GameObject b = Button(root, "Target" + t, new Vector2(bx, enemy ? 240 : 140), new Vector2(270, 82), "", c, target, "Target" + t);
                Text label = b.GetComponentInChildren<Text>();
                label.fontSize = 20;
                client.targetLabels[t] = label;
                client.targetButtons[t] = b;
                b.SetActive(false);
            }

            // ---- the hand: seven cards fanned in an arc; hovering one makes it grow and rise (BattleClient.AnimateCards)
            int hand = BattleClient.HAND_SIZE;
            client.cardImages = new Image[hand];
            client.cardNames = new Text[hand];
            client.cardInfos = new Text[hand];
            client.cardFaces = new Image[hand];
            client.cardPips = new Text[hand];
            client.cardAccuracies = new Text[hand];
            client.cardSchools = new Text[hand];
            client.cardArts = new RawImage[hand];
            // Design 1 "Classic" frames per school (Textures/Cards/CardFrame<School>.png, made by Tools/make_card_frames.py):
            // the frame, parchment, name banner, art window, pip gem and accuracy seal; the text goes on top.
            client.cardFrames = new Sprite[8];
            for (int k = 0; k < 8; k++)
                client.cardFrames[k] = SpriteTexture(CardsDir + "/CardFrame" + (k < SchoolLabels.Length ? SchoolLabels[k] : "General") + ".png");
            Color ink = new Color(0.24f, 0.16f, 0.09f);
            Color nameGold = new Color(1f, 0.94f, 0.63f);
            Vector2 cardSize = new Vector2(196, 280);
            for (int i = 0; i < hand; i++)
            {
                float k = i - (hand - 1) / 2f;
                Vector2 pos = new Vector2(k * 178f, -170f - k * k * 7f);
                GameObject b = Button(root, "Card" + i, pos, cardSize, "", Color.white, target, "Card" + i);
                RectTransform brt = (RectTransform)b.transform;
                brt.localRotation = Quaternion.Euler(0, 0, -k * 4.5f);
                Object.DestroyImmediate(b.GetComponentInChildren<Text>().gameObject);
                var frameImg = b.GetComponent<Image>();
                frameImg.sprite = client.cardFrames[7];
                frameImg.type = Image.Type.Simple;
                // Positions follow the 80 x 112 frame art (x 2.45, y 2.5 on this 196 x 280 card).
                client.cardNames[i] = CardText(brt, "Name", new Vector2(0, 110), new Vector2(118, 26), 15, TextAnchor.MiddleCenter, nameGold);
                client.cardNames[i].fontStyle = FontStyle.Bold;
                client.cardNames[i].resizeTextForBestFit = true;
                client.cardNames[i].resizeTextMinSize = 9;
                client.cardNames[i].resizeTextMaxSize = 15;
                client.cardNames[i].gameObject.AddComponent<Outline>().effectColor = new Color(0.12f, 0.09f, 0.14f);
                client.cardPips[i] = CardText(brt, "Pips", new Vector2(-75, 114), new Vector2(34, 34), 22, TextAnchor.MiddleCenter, Color.white);
                client.cardPips[i].fontStyle = FontStyle.Bold;
                client.cardPips[i].gameObject.AddComponent<Outline>().effectColor = new Color(0.12f, 0.09f, 0.14f);
                // Art window: shields / blades / traps show their icon; other spells stay blank until they have art.
                var artGo = new GameObject("Art", typeof(RectTransform));
                artGo.transform.SetParent(brt, false);
                var art = artGo.AddComponent<RawImage>();
                art.raycastTarget = false;
                ((RectTransform)artGo.transform).anchoredPosition = new Vector2(0, 39);
                ((RectTransform)artGo.transform).sizeDelta = new Vector2(84, 84);
                artGo.SetActive(false);
                client.cardArts[i] = art;
                client.cardAccuracies[i] = CardText(brt, "Accuracy", new Vector2(-60, -21), new Vector2(54, 26), 13, TextAnchor.MiddleCenter, ink);
                client.cardAccuracies[i].fontStyle = FontStyle.Bold;
                // Short text with icons (CardDatabase.GetCardText). Truncate, not overflow, so best fit really shrinks it
                // to the parchment (with overflow on, Unity lets it run off the card instead).
                Text info = CardText(brt, "Info", new Vector2(0, -66), new Vector2(156, 56), 14, TextAnchor.MiddleCenter, ink);
                info.resizeTextForBestFit = true;
                info.resizeTextMinSize = 8;
                info.resizeTextMaxSize = 14;
                info.verticalOverflow = VerticalWrapMode.Truncate;
                info.lineSpacing = 0.9f;
                client.cardInfos[i] = info;
                client.cardSchools[i] = CardText(brt, "School", new Vector2(0, -106), new Vector2(140, 20), 12, TextAnchor.MiddleCenter, new Color(0.62f, 0.52f, 0.36f));
                AddHoverEvents(b, target, "HoverCard" + i, "UnhoverCard" + i);
                client.cardImages[i] = b.GetComponent<Image>();
                b.SetActive(false);
            }

            // ---- actions, on an ornate tray with a gold rule over it; each button has its icon (pixel font glyphs)
            BackPanel(root, "ActionTray", new Vector2(0, -415), new Vector2(1340, 104), "PanelOrnate", new Color(1f, 1f, 1f, 0.92f), 2f, 1);
            GoldRule(root, "ActionRule", new Vector2(0, -355), 1200f);
            Button(root, "Pass", new Vector2(-480, -415), new Vector2(280, 64), "\uE016 Pass", new Color(0.3f, 0.35f, 0.5f), target, "Pass");
            GameObject discard = Button(root, "Discard", new Vector2(-160, -415), new Vector2(280, 64), "\uE019 Discard", new Color(0.4f, 0.3f, 0.25f), target, "ToggleDiscard");
            client.discardImage = discard.GetComponent<Image>();
            GameObject potion = Button(root, "Potion", new Vector2(160, -415), new Vector2(280, 64), "Potion", new Color(0.55f, 0.2f, 0.3f), target, "Potion");
            client.potionLabel = potion.GetComponentInChildren<Text>();
            Button(root, "Flee", new Vector2(480, -415), new Vector2(280, 64), "< Flee", new Color(0.35f, 0.3f, 0.3f), target, "Flee");

            // ---- flee confirmation: a dimmer over the whole hand (blocks clicks on the cards) and a box with Flee / Stay
            RectTransform confirm = Panel(root, "FleeConfirm", Vector2.zero, new Vector2(1600, 960), new Color(0f, 0f, 0.02f, 0.6f));
            confirm.GetComponent<Image>().raycastTarget = true;
            Panel(confirm, "Border", new Vector2(0, -20), new Vector2(788, 408), new Color(0.78f, 0.6f, 0.28f, 1f));
            Panel(confirm, "Box", new Vector2(0, -20), new Vector2(776, 396), new Color(0.1f, 0.08f, 0.16f, 0.98f));
            Label(confirm, "Title", new Vector2(0, 110), new Vector2(740, 70), "Flee the battle?", 46, TextAnchor.MiddleCenter).color = gold;
            client.fleeConfirmLabel = Label(confirm, "Body", new Vector2(0, 10), new Vector2(720, 110), "", 26, TextAnchor.MiddleCenter);
            Button(confirm, "ConfirmFlee", new Vector2(-175, -130), new Vector2(300, 76), "Flee", new Color(0.62f, 0.2f, 0.17f), target, "ConfirmFlee");
            Button(confirm, "CancelFlee", new Vector2(175, -130), new Vector2(300, 76), "Stay", new Color(0.24f, 0.36f, 0.56f), target, "CancelFlee");
            confirm.gameObject.SetActive(false);
            client.fleeConfirmRoot = confirm.gameObject;
            canvasGo.SetActive(false);
        }

        /// <summary>Text on a card that doesn't block clicks on the card itself.</summary>
        private static Text CardText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
        {
            Text t = Label(parent, name, pos, size, "", fontSize, anchor);
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Pointer enter / exit on a UI element send Udon events (used for the card hover zoom).</summary>
        private static void AddHoverEvents(GameObject go, UdonBehaviour target, string enterEvent, string exitEvent)
        {
            var trigger = go.AddComponent<EventTrigger>();
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
            foreach (var pair in new[] { new { type = EventTriggerType.PointerEnter, ev = enterEvent }, new { type = EventTriggerType.PointerExit, ev = exitEvent } })
            {
                var entry = new EventTrigger.Entry { eventID = pair.type };
                UnityEventTools.AddStringPersistentListener(entry.callback, new UnityAction<string>(target.SendCustomEvent), pair.ev);
                trigger.triggers.Add(entry);
            }
        }

        /// <summary>Non-interactive world-space uGUI text (scripts can't change TextMesh text in Udon).</summary>
        private static Text WorldText(Transform parent, string name, Vector3 localPos, Quaternion rot, Vector2 size, float scale, string text, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localPosition = localPos;
            rt.localRotation = rot;
            rt.localScale = Vector3.one * scale;
            Text label = Label(go.transform, "Text", Vector2.zero, size, text, fontSize, TextAnchor.MiddleCenter);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            return label;
        }

        // ================================================================== cutscenes

        private static readonly Vector3 FilmSetOrigin = new Vector3(0f, -800f, 0f);
        private const string VoiceDir = "Assets/WizardsVRC/Audio/Voice";


        private static readonly string[] SchoolLabels = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
        private static readonly Color[] SchoolColors =
        {
            new Color(0.95f, 0.35f, 0.15f), new Color(0.55f, 0.85f, 1f), new Color(0.65f, 0.4f, 0.95f), new Color(0.4f, 0.9f, 0.4f),
            new Color(0.35f, 0.3f, 0.4f), new Color(0.95f, 0.8f, 0.25f), new Color(0.85f, 0.6f, 0.4f),
        };

        /// <summary>The film camera and the in-front-of-player video screen NPC cutscenes play on, and the intro flow.</summary>
        private static UdonSharpBehaviour[] BuildCutscenes(Transform systems, CharacterSelectUI selectUI, Interior interior)
        {
            var player = UdonSharpUndo.AddComponent<CutscenePlayer>(Child(systems, "CutscenePlayer", Vector3.zero).gameObject);
            var flow = UdonSharpUndo.AddComponent<IntroFlow>(Child(systems, "IntroFlow", Vector3.zero).gameObject);
            var sets = new GameObject("FilmSets").transform;
            sets.position = FilmSetOrigin;

            // ---- film camera + video screen in front of the player
            string rtPath = MaterialsDir + "/CutsceneFilm.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null)
            {
                rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
                AssetDatabase.CreateAsset(rt, rtPath);
            }
            var cam = Child(sets, "FilmCamera", Vector3.zero).gameObject.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;

            var screenGo = new GameObject("CutsceneScreen", typeof(RectTransform));
            MakeWorldCanvas(screenGo, new Vector2(1600, 1100), Vector3.zero, 0.001f);
            RectTransform root = (RectTransform)screenGo.transform;
            Panel(root, "Frame", Vector2.zero, new Vector2(1600, 1100), new Color(0.05f, 0.04f, 0.08f, 0.95f));
            var filmGo = new GameObject("Film", typeof(RectTransform), typeof(RawImage));
            filmGo.transform.SetParent(root, false);
            var filmRect = (RectTransform)filmGo.transform;
            filmRect.anchoredPosition = new Vector2(0, 90);
            filmRect.sizeDelta = new Vector2(1560, 878);
            var filmImage = filmGo.GetComponent<RawImage>();
            filmImage.texture = rt;
            filmImage.raycastTarget = false;
            Panel(root, "SubtitleBand", new Vector2(0, -250), new Vector2(1560, 200), new Color(0, 0, 0, 0.7f));
            // Who's speaking: a medallion with their initial, its rim blue (ally), yellow (neutral) or red (hostile),
            // and their name in the same colour (WizardsVRCSceneBuilder.UiStyle.cs has the art).
            UiImage(root, "PortraitBack", new Vector2(-660, -250), new Vector2(170, 170), "PortraitBack", Color.white, 2.6f).preserveAspect = true;
            player.portraitRing = UiImage(root, "PortraitRing", new Vector2(-660, -250), new Vector2(170, 170), "PortraitNeutral", Color.white, 2.6f);
            player.portraitRing.preserveAspect = true;
            player.portraitRings = new[] { UiSprite("PortraitAlly"), UiSprite("PortraitNeutral"), UiSprite("PortraitHostile") };
            player.portraitInitial = Label(root, "PortraitInitial", new Vector2(-660, -250), new Vector2(170, 170), "", 84, TextAnchor.MiddleCenter);
            player.portraitInitial.gameObject.AddComponent<Outline>().effectColor = Color.black;
            player.stanceColors = new[] { new Color(0.5f, 0.75f, 1f), new Color(1f, 0.85f, 0.35f), new Color(1f, 0.45f, 0.4f) };
            player.speakerLabel = Label(root, "Speaker", new Vector2(90, -180), new Vector2(1320, 50), "", 34, TextAnchor.MiddleLeft);
            player.speakerLabel.color = new Color(1f, 0.85f, 0.4f);
            player.subtitleLabel = Label(root, "Subtitle", new Vector2(90, -270), new Vector2(1320, 130), "", 32, TextAnchor.UpperLeft);
            ApplyCombatFont(player.portraitInitial.transform);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(player);
            Button(root, "Next", new Vector2(-170, -475), new Vector2(280, 80), "Next (Space)", new Color(0.3f, 0.35f, 0.55f), target, "Next");
            player.skipButton = Button(root, "Skip", new Vector2(170, -475), new Vector2(280, 80), "Skip", new Color(0.5f, 0.25f, 0.25f), target, "Skip");
            var voice = screenGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            player.voiceSource = voice;
            player.filmCamera = cam;
            player.screenRoot = screenGo;
            // The screen is only switched on for the local player, so nobody else ever sees it. It also goes on its own
            // layer (Walkthrough: still clickable, no collisions) that the film camera doesn't render, together with
            // the player avatars, so neither shows up in the shots.
            int screenLayer = LayerMask.NameToLayer("Walkthrough");
            if (screenLayer < 0) screenLayer = 17;
            foreach (Transform t in screenGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = screenLayer;
            int hidden = 1 << screenLayer;
            foreach (string layerName in new[] { "Player", "PlayerLocal", "MirrorReflection" })
            {
                int l = LayerMask.NameToLayer(layerName);
                if (l >= 0) hidden |= 1 << l;
            }
            cam.cullingMask = ~hidden;
            screenGo.SetActive(false);

            // The opening for new characters (the awakening, the Headmaster's visions, the creator) is built in
            // WizardsVRCSceneBuilder.Awakening.cs and wired to the flow there.
            flow.selectUI = selectUI;
            return new UdonSharpBehaviour[] { player, flow };
        }

        private static Cutscene MakeCutscene(Transform parent, string name, string speaker, string[] lines, string voicePrefix, Transform[] shots)
        {
            var cs = UdonSharpUndo.AddComponent<Cutscene>(Child(parent, name, Vector3.zero).gameObject);
            cs.cutsceneId = voicePrefix;
            cs.lines = lines;
            cs.speakers = new string[lines.Length];
            cs.voices = new AudioClip[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                cs.speakers[i] = speaker;
                cs.voices[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + "/" + voicePrefix + "_" + i.ToString("00") + ".wav");
            }
            cs.shots = shots;
            return cs;
        }

        private static Transform Shot(Transform parent, string name, Vector3 localPos, Vector3 localLookAt)
        {
            Transform t = Child(parent, name, localPos);
            t.localRotation = Quaternion.LookRotation(localLookAt - localPos, Vector3.up);
            return t;
        }

        private static void Prim(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        // ================================================================== builders

        private static Canvas MakeWorldCanvas(GameObject go, Vector2 size, Vector3 localPos, float scale)
        {
            go.layer = 0; // Default layer: the UI layer is not clickable in VRChat unless the menu is open.
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            go.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localPosition = localPos;
            rt.localScale = Vector3.one * scale;
            go.AddComponent<VRCUiShape>();
            return canvas;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform rt = Rect(parent, name, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static Text Label(Transform parent, string name, Vector2 pos, Vector2 size, string text, int fontSize, TextAnchor anchor)
        {
            RectTransform rt = Rect(parent, name, pos, size);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static GameObject Button(Transform parent, string name, Vector2 pos, Vector2 size, string text, Color color,
            UdonBehaviour target, string eventName)
        {
            RectTransform rt = Rect(parent, name, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            UnityEventTools.AddStringPersistentListener(button.onClick, new UnityAction<string>(target.SendCustomEvent), eventName);
            var label = Label(rt, "Label", Vector2.zero, size, text, 30, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            return rt.gameObject;
        }

        private static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static void Block(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Drawn symbols: energy colour and pen-tip colour per school (then General), as GlyphPenData.Labels.
        private static readonly Color[] GlyphColors =
        {
            new Color(1f, 0.42f, 0.1f), new Color(0.45f, 0.8f, 1f), new Color(0.66f, 0.42f, 1f), new Color(0.35f, 0.95f, 0.4f),
            new Color(0.5f, 0.32f, 0.68f), new Color(1f, 0.76f, 0.22f), new Color(0.92f, 0.66f, 0.4f), new Color(0.72f, 0.82f, 1f),
        };
        private static readonly Color[] GlyphEdgeColors =
        {
            new Color(1f, 0.92f, 0.55f), new Color(0.92f, 1f, 1f), new Color(0.95f, 0.92f, 1f), new Color(0.88f, 1f, 0.72f),
            new Color(0.72f, 1f, 0.82f), new Color(1f, 1f, 0.82f), new Color(1f, 0.96f, 0.82f), new Color(1f, 1f, 1f),
        };

        /// <summary>The drawn symbol's material (WizardsVRC/GlyphDraw); null if the shader is missing.</summary>
        private static Material GlyphDrawMat()
        {
            var shader = Shader.Find("WizardsVRC/GlyphDraw");
            if (shader == null)
            {
                Debug.LogError("[WizardsVRC] Shader WizardsVRC/GlyphDraw is missing; casters' symbols won't show.");
                return null;
            }
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/CastGlyphDraw.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Reveal", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A texture read as data, not colour: point-filtered, linear (no sRGB), uncompressed, no mipmaps.</summary>
        private static Texture2D DataTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.filterMode != FilterMode.Point || importer.sRGBTexture || importer.mipmapEnabled ||
                                     importer.textureCompression != TextureImporterCompression.Uncompressed || importer.alphaIsTransparency))
            {
                importer.filterMode = FilterMode.Point;
                importer.sRGBTexture = false;
                importer.alphaIsTransparency = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Sparks trailing off the pen tip while a symbol is drawn (BattleClient emits them as the tip moves), each school
        /// its own: fire embers rise, frost drifts down, storm sparks spit out, life motes float, death wisps creep up
        /// slow and big, myth glitter, balance dust settles; General is plain arcane sparkle.
        /// </summary>
        private static ParticleSystem GlyphTrail(Transform parent, int k, Material mat)
        {
            string label = k < SchoolLabels.Length ? SchoolLabels[k] : "General";
            // lifetime min/max, speed min/max, size min/max, gravity
            float[][] look =
            {
                new[] { 0.45f, 0.9f, 0.2f, 0.7f, 0.05f, 0.13f, -0.25f },   // Fire
                new[] { 0.8f, 1.4f, 0.05f, 0.2f, 0.03f, 0.08f, 0.06f },    // Ice
                new[] { 0.12f, 0.3f, 1.5f, 3.2f, 0.03f, 0.07f, 0f },       // Storm
                new[] { 1f, 1.6f, 0.08f, 0.3f, 0.05f, 0.1f, -0.03f },      // Life
                new[] { 0.9f, 1.5f, 0.05f, 0.2f, 0.09f, 0.18f, -0.08f },   // Death
                new[] { 0.6f, 1f, 0.2f, 0.5f, 0.03f, 0.07f, -0.05f },      // Myth
                new[] { 0.7f, 1.1f, 0.1f, 0.3f, 0.04f, 0.08f, 0.1f },      // Balance
                new[] { 0.5f, 0.9f, 0.2f, 0.4f, 0.04f, 0.08f, 0f },        // General
            };
            float[] v = look[Mathf.Clamp(k, 0, look.Length - 1)];
            var go = Child(parent, "GlyphTrail" + label, Vector3.zero).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(v[0], v[1]);
            main.startSpeed = new ParticleSystem.MinMaxCurve(v[2], v[3]);
            main.startSize = new ParticleSystem.MinMaxCurve(v[4], v[5]);
            main.gravityModifier = v[6];
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = mat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>The grey puff of smoke a fizzled spell leaves where its symbol was.</summary>
        private static ParticleSystem FizzlePuff(Transform parent)
        {
            var go = Child(parent, "FizzlePuff", Vector3.zero).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.gravityModifier = -0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30), new ParticleSystem.Burst(0.1f, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;
            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Mat("FizzleSmoke", new Color(0.42f, 0.42f, 0.45f));
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>The always-on-top UI material (WizardsVRC/UIOverlay); null if the shader is missing.</summary>
        private static Material UIOverlayMat()
        {
            var shader = Shader.Find("WizardsVRC/UIOverlay");
            if (shader == null)
            {
                Debug.LogError("[WizardsVRC] Shader WizardsVRC/UIOverlay is missing; menus can be hidden behind things.");
                return null;
            }
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/UIOverlay.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Every image and text under root draws on top of the world (it keeps its own texture or font).</summary>
        private static void OverlayUI(GameObject root, Material mat)
        {
            if (root == null || mat == null) return;
            foreach (var gfx in root.GetComponentsInChildren<Graphic>(true))
            {
                // Keep special materials (gauges, glyphs): only swap the default UI one.
                if (gfx.material != null && gfx.material != gfx.defaultMaterial && gfx.material.shader.name != "UI/Default") continue;
                gfx.material = mat;
            }
        }

        /// <summary>The seat health gauge (WizardsVRC/PSXGauge); BattleClient copies it per seat and sets fill and colours.</summary>
        private static Material SeatGaugeMat()
        {
            var shader = Shader.Find("WizardsVRC/PSXGauge");
            if (shader == null)
            {
                Debug.LogError("[WizardsVRC] Shader WizardsVRC/PSXGauge is missing; seats keep the plain ring.");
                return null;
            }
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/DuelSeatGauge.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = PixelTexture(TexturesDir + "/DuelSeatGauge.png");
            mat.SetFloat("_Segments", SeatGaugeSegments);
            mat.SetFloat("_Fill", 1f);
            mat.SetFloat("_Trail", 1f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
