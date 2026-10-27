using System.IO;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

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

            ConfigureProject();
            if (!EnsureProgramAssets()) return;
            BackUpMyChanges();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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
            refCam.farClipPlane = 1500f;
            refCam.enabled = false;

            // ---------------- areas
            Interior interior = BuildInterior();
            Transform selectSpawn = interior.officeSpawn;
            Canvas selectCanvas = interior.selectCanvas;
            Transform hubSpawn = BuildHub(out Canvas hubCanvas);

            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { selectSpawn };
            descriptor.RespawnHeightY = RespawnHeight;
            descriptor.ReferenceCamera = refCamGo;

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
            var deckPages = UdonSharpUndo.AddComponent<DeckPages>(Child(systems.transform, "DeckPages", Vector3.zero).gameObject);
            var questPages = UdonSharpUndo.AddComponent<QuestPages>(Child(systems.transform, "QuestPages", Vector3.zero).gameObject);
            questPages.playerState = playerState;
            var monsterDb = UdonSharpUndo.AddComponent<MonsterDatabase>(Child(systems.transform, "MonsterDatabase", Vector3.zero).gameObject);
            // Encounters: the Training Yard boss fight (queue pad) plus two wandering-monster lanes beside it. Each has its
            // own synced BattleManager, local BattleClient and duel circle (see BuildEncounter).
            var encounters = new System.Collections.Generic.List<BattleClient>();
            var battleClient = MakeEncounter(systems.transform, "Arena", cardDb, itemDb, monsterDb, playerState, hubSpawn, new[] { 1, 2, 2, 2 }, false, encounters);
            var laneA = MakeEncounter(systems.transform, "LaneRats", cardDb, itemDb, monsterDb, playerState, hubSpawn, new[] { 4, 4 }, true, encounters);
            var laneB = MakeEncounter(systems.transform, "LaneSprites", cardDb, itemDb, monsterDb, playerState, hubSpawn, new[] { 3, 3, 3 }, true, encounters);
            var battle = battleClient.manager;

            teleporter.defaultTarget = hubSpawn;
            saveManager.scratch = scratchData;
            saveManager.listener = selectUI;
            playerState.current = activeData;
            playerState.saveManager = saveManager;

            selectUI.saveManager = saveManager;
            selectUI.playerState = playerState;
            selectUI.teleporter = teleporter;
            selectUI.hubSpawn = hubSpawn;
            selectUI.selectSpawn = selectSpawn;
            selectUI.newCharacterSpawn = interior.officeSpawn;

            book.playerState = playerState;
            book.itemDatabase = itemDb;
            hud.playerState = playerState;
            hud.itemDatabase = itemDb;
            deckPages.playerState = playerState;
            deckPages.cardDatabase = cardDb;
            book.deckPages = deckPages;
            book.questPages = questPages;

            BuildSelectCanvasContents(selectCanvas, selectUI);
            BuildHubCanvasContents(hubCanvas, selectUI, book);
            BuildInventoryBook(book, hud, deckPages, questPages);
            BuildHud(hud);
            Transform hubRoot = hubSpawn.parent;
            var chest = BuildLootChest(hubRoot, playerState, itemDb);
            var pedestal = BuildBackpackPedestal(hubRoot, book);
            var queuePad = BuildQueuePad(hubRoot, battleClient);
            BuildArena(battleClient, new[] { laneA, laneB });
            var cutscenes = BuildCutscenes(systems.transform, selectUI, interior);
            var learning = BuildSpellLearning(systems.transform, playerState, cardDb, hud, out SpellTrainer trainer, out InteractionMenu interactionMenu);
            var quests = BuildQuests(systems.transform, interior, (CutscenePlayer)cutscenes[0], playerState, hud, hubRoot, trainer, interactionMenu, queuePad.transform);
            var questManager = (QuestManager)quests[0];
            questManager.cardDatabase = cardDb;
            questManager.popup = trainer.popup;
            foreach (var ec in encounters) ec.questManager = questManager;
            selectUI.questManager = questManager;
            hud.questManager = questManager;
            questPages.questManager = questManager;
            var questGuide = BuildQuestGuide(systems.transform, playerState, questManager, (CutscenePlayer)cutscenes[0], encounters.ToArray(),
                interior.exitDoor, hubRoot.Find("KeepEntrance"));
            questPages.questGuide = questGuide;

            var toSync = new System.Collections.Generic.List<UdonSharpBehaviour>(cutscenes);
            toSync.AddRange(quests);
            toSync.AddRange(learning);
            foreach (var b in new UdonSharpBehaviour[] { activeData, scratchData, saveManager, playerState, teleporter, selectUI, itemDb, book, hud, chest, pedestal, cardDb, deckPages, monsterDb, queuePad, questPages, questGuide }) toSync.Add(b);
            foreach (var ec in encounters) { toSync.Add(ec.manager); toSync.Add(ec); foreach (var w in ec.GetComponentsInChildren<Wanderer>(true)) toSync.Add(w); }
            foreach (var b in toSync)
            {
                UdonSharpEditorUtility.CopyProxyToUdon(b);
                EditorUtility.SetDirty(b);
            }

            RestoreMyChanges(scene);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[WizardsVRC] Generated " + ScenePath);
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

        /// <summary>UdonSharp only compiles scripts that have a program asset next to them.</summary>
        private static bool EnsureProgramAssets()
        {
            Directory.CreateDirectory(ProgramsDir);
            bool created = false;
            foreach (string name in new[] { "CharacterData", "SaveManager", "PlayerState", "TeleportHelper", "CharacterSelectUI", "ItemDatabase", "InventoryBook", "PlayerHUD", "LootChest", "EventRelay", "CardDatabase", "DeckPages", "BattleManager", "BattleClient", "BattleQueuePad", "MonsterDatabase", "Wanderer", "QuestTarget", "Cutscene", "CutscenePlayer", "IntroFlow", "QuestManager", "SpellTrainer", "NewSpellPopup", "InteractionMenu", "NpcInteractions", "QuestPages", "QuestGuide" })
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

        private static Transform BuildHub(out Canvas hubCanvas)
        {
            var root = new GameObject("Hub").transform;

            BuildCastle(root);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "SpawnLandingPad";
            pad.transform.SetParent(root, false);
            pad.transform.localPosition = new Vector3(0, 0.05f, 0);
            pad.transform.localScale = new Vector3(3f, 0.05f, 3f);
            pad.GetComponent<Renderer>().sharedMaterial = Mat("LandingPad", new Color(0.35f, 0.55f, 0.9f));

            Transform spawn = Child(root, "HubSpawn", new Vector3(0, 0.15f, 0));

            var canvasGo = new GameObject("HubCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root, false);
            hubCanvas = MakeWorldCanvas(canvasGo, new Vector2(600, 360), new Vector3(-4f, 1.6f, 4f), 0.004f);
            canvasGo.transform.localRotation = Quaternion.Euler(0, 180f, 0);
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
            Panel(root, "Spine", Vector2.zero, new Vector2(30, 1060), new Color(0.22f, 0.12f, 0.06f));

            GameObject packTab = Button(root, "BackpackTab", new Vector2(935, 380), new Vector2(80, 130), "Pack", new Color(0.85f, 0.65f, 0.3f), target, "ShowBackpack");
            packTab.GetComponentInChildren<Text>().fontSize = 22;
            book.backpackTabImage = packTab.GetComponent<Image>();
            GameObject deckTab = Button(root, "DeckTab", new Vector2(935, 235), new Vector2(80, 130), "Deck", new Color(0.45f, 0.3f, 0.18f), target, "ShowDeck");
            deckTab.GetComponentInChildren<Text>().fontSize = 22;
            book.deckTabImage = deckTab.GetComponent<Image>();
            GameObject questTab = Button(root, "QuestTab", new Vector2(935, 90), new Vector2(80, 130), "Quests", new Color(0.45f, 0.3f, 0.18f), target, "ShowQuests");
            questTab.GetComponentInChildren<Text>().fontSize = 18;
            book.questTabImage = questTab.GetComponent<Image>();

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

            book.statsLabel = InkLabel(left, "Stats", new Vector2(0, -290), new Vector2(800, 120), "", 28, TextAnchor.MiddleCenter);
            book.statusLabel = InkLabel(left, "Status", new Vector2(0, -385), new Vector2(800, 50), "", 26, TextAnchor.MiddleCenter);
            Button(left, "Close", new Vector2(-250, -460), new Vector2(220, 65), "Close", new Color(0.45f, 0.25f, 0.15f), target, "CloseBook");
            Button(left, "ToggleHud", new Vector2(250, -460), new Vector2(220, 65), "HUD On/Off", new Color(0.45f, 0.25f, 0.15f), hudTarget, "ToggleHud");

            // ---- right page: tabs, grid, paging, details
            InkLabel(right, "Title", new Vector2(0, 385), new Vector2(800, 55), "Backpack", 40, TextAnchor.MiddleCenter);
            string[] tabs = { "All", "Hat", "Robe", "Boots", "Wand", "Athame", "Amulet", "Ring", "Potion" };
            book.tabImages = new Image[tabs.Length];
            for (int t = 0; t < tabs.Length; t++)
            {
                GameObject b = Button(right, "Tab" + t, new Vector2(-376 + t * 94, 460), new Vector2(90, 60), tabs[t], new Color(0.45f, 0.3f, 0.18f), target, "Tab" + t);
                b.GetComponentInChildren<Text>().fontSize = 19;
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

        private static void BuildDeckPages(RectTransform root, InventoryBook book, DeckPages deck, PlayerHUD hud)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(deck);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            Color tabOff = new Color(0.45f, 0.3f, 0.18f);

            RectTransform view = Rect(root, "DeckView", Vector2.zero, new Vector2(1800, 1100));
            book.deckView = view.gameObject;
            RectTransform left = Panel(view, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            RectTransform right = Panel(view, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);

            // ---- left page: deck
            InkLabel(left, "Title", new Vector2(0, 450), new Vector2(800, 55), "Deck", 40, TextAnchor.MiddleCenter);
            deck.deckTabImages = new Image[CharacterData.DECK_COUNT];
            for (int d = 0; d < CharacterData.DECK_COUNT; d++)
            {
                GameObject b = Button(left, "Deck" + d, new Vector2(-200 + d * 200, 385), new Vector2(180, 55), "Deck " + (d + 1), tabOff, target, "Deck" + d);
                b.GetComponentInChildren<Text>().fontSize = 24;
                deck.deckTabImages[d] = b.GetComponent<Image>();
            }
            deck.deckTitle = InkLabel(left, "DeckTitle", new Vector2(0, 320), new Vector2(800, 45), "", 28, TextAnchor.MiddleCenter);

            int deckCells = DeckPages.DECK_CELLS;
            deck.deckCellRoots = new GameObject[deckCells];
            deck.deckCellIcons = new Image[deckCells];
            deck.deckCellNames = new Text[deckCells];
            deck.deckCellCounts = new Text[deckCells];
            for (int i = 0; i < deckCells; i++)
            {
                int col = i % 4, row = i / 4;
                GameObject b = Button(left, "DeckCell" + i, new Vector2(-300 + col * 200, 230 - row * 125), new Vector2(185, 110), "", Color.gray, target, "DeckCell" + i);
                Text nameText = b.GetComponentInChildren<Text>();
                nameText.fontSize = 20;
                Text countText = Label(b.transform, "Count", new Vector2(55, 35), new Vector2(70, 32), "", 22, TextAnchor.UpperRight);
                deck.deckCellRoots[i] = b;
                deck.deckCellIcons[i] = b.GetComponent<Image>();
                deck.deckCellNames[i] = nameText;
                deck.deckCellCounts[i] = countText;
            }
            deck.statusLabel = InkLabel(left, "Status", new Vector2(0, -360), new Vector2(800, 50), "", 26, TextAnchor.MiddleCenter);
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
            deck.bookCellRoots = new GameObject[bookCells];
            deck.bookCellIcons = new Image[bookCells];
            deck.bookCellNames = new Text[bookCells];
            for (int i = 0; i < bookCells; i++)
            {
                int col = i % 4, row = i / 4;
                GameObject b = Button(right, "BookCell" + i, new Vector2(-300 + col * 200, 270 - row * 165), new Vector2(185, 150), "", Color.gray, target, "BookCell" + i);
                Text nameText = b.GetComponentInChildren<Text>();
                nameText.fontSize = 22;
                deck.bookCellRoots[i] = b;
                deck.bookCellIcons[i] = b.GetComponent<Image>();
                deck.bookCellNames[i] = nameText;
            }
            Button(right, "PrevPage", new Vector2(-300, -180), new Vector2(150, 60), "<", tabOff, target, "PrevPage");
            deck.pageLabel = InkLabel(right, "Page", new Vector2(0, -180), new Vector2(420, 50), "Page 1 / 1", 26, TextAnchor.MiddleCenter);
            Button(right, "NextPage", new Vector2(300, -180), new Vector2(150, 60), ">", tabOff, target, "NextPage");

            RectTransform details = Panel(right, "Details", new Vector2(0, -360), new Vector2(810, 270), new Color(0.82f, 0.74f, 0.58f));
            deck.detailsPanel = details.gameObject;
            deck.detailName = InkLabel(details, "SpellName", new Vector2(-110, 100), new Vector2(570, 50), "", 30, TextAnchor.MiddleLeft);
            deck.detailBody = InkLabel(details, "SpellBody", new Vector2(-110, -25), new Vector2(570, 190), "", 22, TextAnchor.UpperLeft);
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

        private static void BuildHud(PlayerHUD hud)
        {
            var canvasGo = new GameObject("HudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(hud.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(420, 340);
            rt.localScale = Vector3.one * 0.0005f;
            hud.hudRoot = canvasGo;

            Panel(rt, "Background", new Vector2(0, 70), new Vector2(420, 200), new Color(0.08f, 0.07f, 0.12f, 0.7f));
            hud.questLabel = Label(rt, "Quest", new Vector2(0, -105), new Vector2(400, 120), "", 22, TextAnchor.UpperLeft);
            hud.questLabel.color = new Color(1f, 0.95f, 0.7f);
            hud.nameLabel = Label(rt, "Name", new Vector2(0, 135), new Vector2(400, 45), "", 28, TextAnchor.MiddleLeft);
            hud.healthLabel = Label(rt, "Health", new Vector2(0, 90), new Vector2(400, 40), "", 26, TextAnchor.MiddleLeft);
            hud.healthLabel.color = new Color(1f, 0.55f, 0.55f);
            hud.manaLabel = Label(rt, "Mana", new Vector2(0, 48), new Vector2(400, 40), "", 26, TextAnchor.MiddleLeft);
            hud.manaLabel.color = new Color(0.55f, 0.75f, 1f);
            hud.goldLabel = Label(rt, "Gold", new Vector2(0, 6), new Vector2(400, 40), "", 26, TextAnchor.MiddleLeft);
            hud.goldLabel.color = new Color(1f, 0.85f, 0.35f);
            canvasGo.SetActive(false);
        }

        private static LootChest BuildLootChest(Transform hubRoot, PlayerState playerState, ItemDatabase itemDb)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PracticeChest";
            go.transform.SetParent(hubRoot, false);
            go.transform.localPosition = new Vector3(-5f, 0.35f, 10f);
            go.transform.localScale = new Vector3(1.2f, 0.7f, 0.8f);
            go.GetComponent<Renderer>().sharedMaterial = Mat("Chest", new Color(0.5f, 0.32f, 0.15f));

            // uGUI Text, not TextMesh: TextMesh.text is not exposed to Udon. No raycaster, so it never blocks clicks.
            var labelGo = new GameObject("ChestLabel", typeof(RectTransform));
            labelGo.transform.SetParent(hubRoot, false);
            labelGo.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.sizeDelta = new Vector2(600, 200);
            labelRt.localPosition = new Vector3(-5f, 1.5f, 10f);
            labelRt.localScale = Vector3.one * 0.004f;
            var tm = Label(labelGo.transform, "Text", Vector2.zero, new Vector2(600, 200), "Practice Chest", 56, TextAnchor.MiddleCenter);

            var chest = UdonSharpUndo.AddComponent<LootChest>(go);
            chest.playerState = playerState;
            chest.itemDatabase = itemDb;
            chest.messageLabel = tm;
            return chest;
        }

        private static EventRelay BuildBackpackPedestal(Transform hubRoot, InventoryBook book)
        {
            Block(hubRoot, "BackpackPedestal", new Vector3(2.5f, 0.5f, 3f), new Vector3(0.6f, 1f, 0.6f), Mat("Pedestal", new Color(0.5f, 0.5f, 0.55f)));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BackpackBook";
            go.transform.SetParent(hubRoot, false);
            go.transform.localPosition = new Vector3(2.5f, 1.07f, 3f);
            go.transform.localScale = new Vector3(0.45f, 0.12f, 0.35f);
            go.GetComponent<Renderer>().sharedMaterial = Mat("BookCover", new Color(0.36f, 0.2f, 0.1f));

            var label = new GameObject("BackpackLabel");
            label.transform.SetParent(hubRoot, false);
            label.transform.localPosition = new Vector3(2.5f, 1.6f, 3f);
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

        private static BattleQueuePad BuildQueuePad(Transform hubRoot, BattleClient client)
        {
            Vector3 pos = new Vector3(30f, 0.06f, 4f);
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
        private const float DuelCircleRadius = 9.3f;
        // Matches the seat sockets painted at 200/256 of the disc radius in DuelCircle.png.
        private const float DuelSeatRadius = DuelCircleRadius * 200f / 256f;
        private static readonly string[] FxKindNames = { "Shield", "Blade", "Trap" };
        private static readonly float[] FxHeights = { 1.1f, 2.15f, 0.12f };
        private static readonly float[] MonsterSeatAngles = { 337.5f, 22.5f, 292.5f, 67.5f };

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
        private static BattleClient MakeEncounter(Transform systems, string name, CardDatabase cardDb, ItemDatabase itemDb, MonsterDatabase monsterDb,
            PlayerState playerState, Transform hubReturn, int[] roster, bool wander, System.Collections.Generic.List<BattleClient> all)
        {
            var holder = Child(systems, "Encounter_" + name, Vector3.zero);
            var manager = UdonSharpUndo.AddComponent<BattleManager>(Child(holder, "BattleManager", Vector3.zero).gameObject);
            var client = UdonSharpUndo.AddComponent<BattleClient>(Child(holder, "BattleClient", Vector3.zero).gameObject);
            manager.cardDatabase = cardDb;
            manager.monsterDatabase = monsterDb;
            manager.client = client;
            manager.roster = roster;
            manager.wanderMode = wander;
            manager.scaleRosterWithParty = !wander;
            client.manager = manager;
            client.playerState = playerState;
            client.itemDatabase = itemDb;
            client.cardDatabase = cardDb;
            client.monsterDatabase = monsterDb;
            client.hubReturn = hubReturn;
            all.Add(client);
            return client;
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
            arrivalPad.transform.localPosition = new Vector3(-22f, 0.02f, -6.75f);
            arrivalPad.transform.localScale = new Vector3(3.5f, 0.02f, 6f);
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
            // Duel circle: rune band, 8 seat sockets and a central sigil, painted on one flat disc.
            Texture2D circleTex = PixelTexture(TexturesDir + "/DuelCircle.png");
            FlatDecal(ring.transform, "CircleDisc", new Vector3(0, 0.02f, 0), DuelCircleRadius * 2f,
                CutoutMat("DuelCircle", circleTex, Color.white, new Color(0.35f, 0.35f, 0.4f)));

            // Turn clock in the middle of the ring: the hand sweeps the planning timer, then points at each caster.
            var clock = Child(ring.transform, "TurnClock", Vector3.zero);
            client.arenaCenter = clock;
            clock.localScale = Vector3.one * 1.5f;
            client.engageRadius = DuelCircleRadius + 5f;
            var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            face.name = "Face";
            face.transform.SetParent(clock, false);
            face.transform.localPosition = new Vector3(0, 0.03f, 0);
            face.transform.localScale = new Vector3(1.6f, 0.03f, 1.6f);
            face.GetComponent<Renderer>().sharedMaterial = Mat("ClockFace", new Color(0.15f, 0.13f, 0.22f));
            Object.DestroyImmediate(face.GetComponent<Collider>());
            for (int k = 0; k < 12; k++)
            {
                var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tick.name = "Tick" + k;
                tick.transform.SetParent(clock, false);
                tick.transform.localRotation = Quaternion.Euler(0, k * 30f, 0);
                tick.transform.localPosition = tick.transform.localRotation * new Vector3(0, 0.07f, 0.68f);
                tick.transform.localScale = new Vector3(0.04f, 0.03f, k % 3 == 0 ? 0.16f : 0.08f);
                tick.GetComponent<Renderer>().sharedMaterial = ringMat;
                Object.DestroyImmediate(tick.GetComponent<Collider>());
            }
            var hand = Child(clock, "Hand", new Vector3(0, 0.09f, 0));
            var needle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            needle.name = "Needle";
            needle.transform.SetParent(hand, false);
            needle.transform.localPosition = new Vector3(0, 0, 0.3f);
            needle.transform.localScale = new Vector3(0.07f, 0.04f, 0.6f);
            needle.GetComponent<Renderer>().sharedMaterial = ringMat;
            Object.DestroyImmediate(needle.GetComponent<Collider>());
            client.clockHand = hand;
            client.clockLabel = WorldText(ring.transform, "ClockLabel", new Vector3(0, 1.1f, 0), Quaternion.identity, new Vector2(700, 160), 0.004f, "", 44);

            Texture2D seatTex = PixelTexture(TexturesDir + "/DuelSeatRing.png");
            Material seatEmpty = CutoutMat("DuelSeatEmpty", seatTex, new Color(0.45f, 0.42f, 0.5f), new Color(0.08f, 0.08f, 0.1f));
            client.seatEmptyMat = seatEmpty;
            client.seatSchoolMats = new Material[SchoolLabels.Length];
            for (int k = 0; k < SchoolLabels.Length; k++)
                client.seatSchoolMats[k] = CutoutMat("DuelSeat" + SchoolLabels[k], seatTex, SchoolColors[k], SchoolColors[k] * 1.6f);
            client.seatRings = new Renderer[BattleManager.SLOTS];
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
                // 8 seats evenly round the circle: wizards on the south half (left to right), monsters facing them
                // on the north half, boss in the middle-left seat.
                float ang = monster ? MonsterSeatAngles[i] : 247.5f - 45f * i;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                Vector3 pos = dir * DuelSeatRadius + new Vector3(0, 0.05f, 0);
                Transform seat = Child(root, (monster ? "MonsterSpot" : "WizardSeat") + i, pos);
                seat.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
                client.seats[s] = seat;

                GameObject marker = FlatDecal(seat, "SeatRing", new Vector3(0, -0.02f, 0), 1.75f, seatEmpty);
                marker.transform.SetParent(ring.transform, true);
                client.seatRings[s] = marker.GetComponent<Renderer>();

                float labelHeight = 2.5f;
                if (monster)
                {
                    bool boss = i == 0;
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = boss ? "Boss" : "Minion" + i;
                    body.transform.SetParent(seat, false);
                    float scale = boss ? 1.7f : 1f;
                    body.transform.localPosition = new Vector3(0, scale, 0);
                    body.transform.localScale = Vector3.one * scale;
                    body.GetComponent<Renderer>().sharedMaterial = boss ? Mat("BossBody", new Color(0.25f, 0.2f, 0.3f)) : Mat("MinionBody", new Color(0.75f, 0.65f, 0.3f));
                    Object.DestroyImmediate(body.GetComponent<Collider>());
                    body.SetActive(false);
                    client.monsterBodies[i] = body;
                    labelHeight = boss ? 4f : 2.6f;
                }
                client.overheadLabels[s] = WorldText(root, "Label" + s, pos + new Vector3(0, labelHeight, 0), Quaternion.identity,
                    new Vector2(600, 160), 0.004f, "", 36);

                float fxScale = monster && i == 0 ? 1.7f : 1f;
                for (int kind = 0; kind < 3; kind++)
                {
                    Transform orbit = Child(ring.transform, "Fx" + FxKindNames[kind] + s, pos + new Vector3(0, FxHeights[kind] * fxScale, 0));
                    orbit.localScale = Vector3.one * fxScale;
                    client.fxOrbits[s * 3 + kind] = orbit;
                    for (int k = 0; k < fxMax; k++)
                    {
                        var icon = GameObject.CreatePrimitive(kind == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                        icon.name = FxKindNames[kind] + k;
                        icon.transform.SetParent(orbit, false);
                        icon.transform.localScale = kind == 0 ? new Vector3(0.3f, 0.015f, 0.3f)
                            : (kind == 1 ? new Vector3(0.06f, 0.42f, 0.025f) : new Vector3(0.24f, 0.02f, 0.24f));
                        var ir = icon.GetComponent<Renderer>();
                        ir.sharedMaterial = client.fxMats[SchoolLabels.Length];
                        ir.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Object.DestroyImmediate(icon.GetComponent<Collider>());
                        icon.SetActive(false);
                        client.fxIcons[(s * 3 + kind) * fxMax + k] = ir;
                    }
                }
            }

            client.bannerLabel = WorldText(root, "Banner", new Vector3(0, 5.2f, 7f), Quaternion.identity, new Vector2(1600, 420), 0.005f, "", 52);

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
            client.castGlyphMats = new Material[SchoolLabels.Length + 1];
            for (int k = 0; k <= SchoolLabels.Length; k++)
            {
                string label = k < SchoolLabels.Length ? SchoolLabels[k] : "General";
                Texture2D gt = PixelTexture(TexturesDir + "/CastGlyph" + label + ".png");
                // The glyph PNGs are painted in school colours, so the material stays untinted and just glows.
                client.castGlyphMats[k] = CutoutMat("CastGlyph" + label, gt, Color.white, Color.white * 0.9f);
            }
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

            // School-coloured particle burst that explodes off the caster at the cast sound's peak.
            Shader particleShader = Shader.Find("Particles/Standard Unlit");
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
                Material pm = particleShader != null ? new Material(particleShader) : Mat("CastBurst" + label, c);
                if (particleShader != null)
                {
                    pm.name = "CastBurst" + label;
                    pm.SetColor("_Color", Color.white);
                    string pmPath = MaterialsDir + "/CastBurst" + label + ".mat";
                    AssetDatabase.DeleteAsset(pmPath);
                    AssetDatabase.CreateAsset(pm, pmPath);
                }
                pr.sharedMaterial = pm;
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                client.castBursts[k] = ps;
            }

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
            ring.SetActive(false);
            client.ring = ring;

            // Wandering monsters circle the ring; their look comes from the roster's monster.
            int[] roster = client.manager != null ? client.manager.roster : null;
            for (int w = 0; w < wanderCount && roster != null && roster.Length > 0; w++)
            {
                int monsterId = roster[w % roster.Length];
                int sc = client.manager.monsterDatabase.GetSchool(monsterId);
                float ms = client.manager.monsterDatabase.GetBodyScale(monsterId);
                Transform wt = Child(root, "Wanderer" + w, Vector3.zero);
                var wanderer = UdonSharpUndo.AddComponent<Wanderer>(wt.gameObject);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(wt, false);
                body.transform.localPosition = new Vector3(0, ms, 0);
                body.transform.localScale = Vector3.one * ms;
                body.GetComponent<Renderer>().sharedMaterial = client.fxMats[sc >= 0 && sc < SchoolLabels.Length ? sc : SchoolLabels.Length];
                Object.DestroyImmediate(body.GetComponent<Collider>());
                wanderer.body = body;
                wanderer.label = WorldText(wt, "Label", new Vector3(0, ms * 2f + 0.6f, 0), Quaternion.identity, new Vector2(500, 140), 0.004f, "", 34);
                wanderer.manager = client.manager;
                wanderer.client = client;
                wanderer.monsterDatabase = client.manager.monsterDatabase;
                wanderer.monsterId = monsterId;
                wanderer.center = client.arenaCenter;
                wanderer.radius = DuelCircleRadius + 3f;
                wanderer.speed = 1.1f + 0.2f * w;
                wanderer.phaseOffset = w * Mathf.PI * 2f / Mathf.Max(1, wanderCount);
            }
        }

        private static void BuildHandPanel(Transform arenaRoot, BattleClient client)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(client);
            var canvasGo = new GameObject("HandCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(arenaRoot, false);
            MakeWorldCanvas(canvasGo, new Vector2(1400, 760), new Vector3(0, 1.2f, -2.4f), 0.0006f);
            RectTransform root = (RectTransform)canvasGo.transform;
            client.handRoot = canvasGo;

            Panel(root, "Background", Vector2.zero, new Vector2(1400, 760), new Color(0.08f, 0.07f, 0.12f, 0.85f));
            client.partyLabel = Label(root, "Party", new Vector2(-370, 235), new Vector2(620, 270), "", 21, TextAnchor.UpperLeft);
            client.statusLabel = Label(root, "Status", new Vector2(330, 235), new Vector2(700, 270), "", 26, TextAnchor.UpperLeft);

            int hand = BattleClient.HAND_SIZE;
            client.cardImages = new Image[hand];
            client.cardNames = new Text[hand];
            client.cardInfos = new Text[hand];
            for (int i = 0; i < hand; i++)
            {
                GameObject b = Button(root, "Card" + i, new Vector2(-570 + i * 190, -25), new Vector2(180, 250), "", Color.gray, target, "Card" + i);
                client.cardImages[i] = b.GetComponent<Image>();
                client.cardNames[i] = Label(b.transform, "Name", new Vector2(0, 85), new Vector2(170, 70), "", 23, TextAnchor.MiddleCenter);
                client.cardInfos[i] = Label(b.transform, "Info", new Vector2(0, -40), new Vector2(170, 160), "", 17, TextAnchor.UpperCenter);
                b.SetActive(false);
            }

            int slots = BattleManager.SLOTS;
            client.targetButtons = new GameObject[slots];
            client.targetLabels = new Text[slots];
            for (int t = 0; t < slots; t++)
            {
                Color c = t < BattleManager.SIDE ? new Color(0.25f, 0.45f, 0.3f) : new Color(0.55f, 0.22f, 0.2f);
                GameObject b = Button(root, "Target" + t, new Vector2(-612 + t * 175, -200), new Vector2(165, 70), "", c, target, "Target" + t);
                Text label = b.GetComponentInChildren<Text>();
                label.fontSize = 20;
                client.targetLabels[t] = label;
                client.targetButtons[t] = b;
                b.SetActive(false);
            }

            Button(root, "Pass", new Vector2(-160, -310), new Vector2(260, 70), "Pass", new Color(0.3f, 0.35f, 0.5f), target, "Pass");
            GameObject discard = Button(root, "Discard", new Vector2(160, -310), new Vector2(260, 70), "Discard", new Color(0.4f, 0.3f, 0.25f), target, "ToggleDiscard");
            client.discardImage = discard.GetComponent<Image>();
            canvasGo.SetActive(false);
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

        private static readonly string[] IntroLines =
        {
            "Long ago, the first wizards bound the magic of the world into seven schools.",
            "Fire, the school of passion, burns away all that stands before it.",
            "Ice, the school of endurance, holds firm like the oldest glacier.",
            "Storm, the school of wild power, strikes with thunder and fury.",
            "Life, the school of growth, mends all that has been broken.",
            "Death, the school of shadow, draws strength from what is lost.",
            "Myth, the school of legend, calls forth creatures of story.",
            "Balance, the school of harmony, weaves every magic together.",
            "And now, a new wizard arrives. Welcome to Wizards VRC.",
        };

        private static readonly string[] SchoolLabels = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
        private static readonly Color[] SchoolColors =
        {
            new Color(0.95f, 0.35f, 0.15f), new Color(0.55f, 0.85f, 1f), new Color(0.65f, 0.4f, 0.95f), new Color(0.4f, 0.9f, 0.4f),
            new Color(0.35f, 0.3f, 0.4f), new Color(0.95f, 0.8f, 0.25f), new Color(0.85f, 0.6f, 0.4f),
        };

        /// <summary>Film sets (far below the world), the in-front-of-player video screen, and the intro cutscenes.</summary>
        private static UdonSharpBehaviour[] BuildCutscenes(Transform systems, CharacterSelectUI selectUI, Interior interior)
        {
            var player = UdonSharpUndo.AddComponent<CutscenePlayer>(Child(systems, "CutscenePlayer", Vector3.zero).gameObject);
            var flow = UdonSharpUndo.AddComponent<IntroFlow>(Child(systems, "IntroFlow", Vector3.zero).gameObject);
            var sets = new GameObject("FilmSets").transform;
            sets.position = FilmSetOrigin;

            // ---- school showcase: seven monuments in a circle
            var showcase = Child(sets, "SchoolShowcase", Vector3.zero);
            Block(showcase, "Floor", new Vector3(0, -0.25f, 0), new Vector3(30, 0.5f, 30), Mat("FilmFloor", new Color(0.2f, 0.2f, 0.25f)));
            var introShots = new Transform[IntroLines.Length];
            introShots[0] = Shot(showcase, "ShotWide", new Vector3(0, 7f, -15f), new Vector3(0, 1.5f, 0));
            for (int i = 0; i < SchoolLabels.Length; i++)
            {
                float ang = i * 360f / SchoolLabels.Length;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                Vector3 pos = dir * 8f;
                Material mat = Mat("School" + SchoolLabels[i], SchoolColors[i]);
                Block(showcase, SchoolLabels[i] + "Pillar", pos + new Vector3(0, 1.5f, 0), new Vector3(1.2f, 3f, 1.2f), mat);
                var crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crystal.name = SchoolLabels[i] + "Crystal";
                crystal.transform.SetParent(showcase, false);
                crystal.transform.localPosition = pos + new Vector3(0, 3.7f, 0);
                crystal.transform.localScale = Vector3.one * 0.9f;
                Material glow = Mat("SchoolGlow" + SchoolLabels[i], SchoolColors[i]);
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", SchoolColors[i] * 2f);
                crystal.GetComponent<Renderer>().sharedMaterial = glow;
                Object.DestroyImmediate(crystal.GetComponent<Collider>());
                var light = Child(showcase, SchoolLabels[i] + "Light", pos + new Vector3(0, 4.5f, 0) - dir * 1.5f).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = SchoolColors[i];
                light.range = 8f;
                light.intensity = 2f;
                Quaternion faceIn = Quaternion.LookRotation(dir);
                WorldText(showcase, SchoolLabels[i] + "Label", pos + new Vector3(0, 5f, 0) - dir * 0.2f, faceIn, new Vector2(600, 150), 0.006f, SchoolLabels[i], 90);
                introShots[i + 1] = Shot(showcase, "Shot" + SchoolLabels[i], pos - dir * 5f + new Vector3(0, 2.2f, 0), pos + new Vector3(0, 3.2f, 0));
            }

            // ---- placeholder title screen
            var title = Child(sets, "TitleCard", new Vector3(0, 0, 60f));
            Block(title, "Backdrop", new Vector3(0, 3f, 1f), new Vector3(16, 9, 0.2f), Mat("TitleBackdrop", new Color(0.08f, 0.06f, 0.15f)));
            WorldText(title, "Title", new Vector3(0, 3.8f, 0.8f), Quaternion.identity, new Vector2(1600, 300), 0.006f, "WIZARDS VRC", 200);
            WorldText(title, "Subtitle", new Vector3(0, 2.2f, 0.8f), Quaternion.identity, new Vector2(1600, 200), 0.006f, "(placeholder title screen)", 70);
            introShots[IntroLines.Length - 1] = Shot(title, "ShotTitle", new Vector3(0, 3f, -7f), new Vector3(0, 3f, 0.8f));

            var headShots = OfficeShots(interior);

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
            player.speakerLabel = Label(root, "Speaker", new Vector2(0, -180), new Vector2(1500, 50), "", 34, TextAnchor.MiddleLeft);
            player.speakerLabel.color = new Color(1f, 0.85f, 0.4f);
            player.subtitleLabel = Label(root, "Subtitle", new Vector2(0, -270), new Vector2(1500, 130), "", 32, TextAnchor.UpperLeft);
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

            // ---- cutscene data + flow
            var intro = MakeCutscene(sets, "IntroCutscene", "Narrator", IntroLines, "intro", introShots);
            var headmaster = MakeCutscene(sets, "HeadmasterGreeting", "Headmaster Greymantle", ProphecyLines, "prophecy", headShots);
            intro.onFinished = flow;
            intro.onFinishedEvent = nameof(IntroFlow.OnIntroFinished);
            headmaster.onFinished = flow;
            headmaster.onFinishedEvent = nameof(IntroFlow.OnHeadmasterFinished);
            flow.player = player;
            flow.introCutscene = intro;
            flow.headmasterCutscene = headmaster;
            flow.selectUI = selectUI;
            return new UdonSharpBehaviour[] { player, flow, intro, headmaster };
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
