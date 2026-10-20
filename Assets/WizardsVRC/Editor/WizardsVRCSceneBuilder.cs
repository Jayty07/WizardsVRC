using System.IO;
using UdonSharp;
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
    public static class WizardsVRCSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainWorld.unity";
        private const string ScriptsDir = "Assets/WizardsVRC/Scripts";
        private const string ProgramsDir = "Assets/WizardsVRC/UdonPrograms";
        private const string MaterialsDir = "Assets/WizardsVRC/Materials";
        private const string AutoBuildKey = "WizardsVRC.AutoBuildAttempted";

        private static readonly Vector3 SelectRoomOrigin = new Vector3(0f, -500f, 0f);
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
                    "This replaces " + ScenePath + " with a freshly generated scene. Continue?", "Rebuild", "Cancel"))
                return;
            BuildScene();
        }

        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureProject();
            EnsureProgramAssets();

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

            // ---------------- world descriptor
            var world = new GameObject("VRCWorld");
            var refCamGo = new GameObject("ReferenceCamera");
            refCamGo.transform.SetParent(world.transform, false);
            var refCam = refCamGo.AddComponent<Camera>();
            refCam.nearClipPlane = 0.05f;
            refCam.farClipPlane = 1500f;
            refCam.enabled = false;

            // ---------------- areas
            Transform selectSpawn = BuildSelectRoom(out Canvas selectCanvas);
            Transform hubSpawn = BuildHub(out Transform npcMarker, out Canvas hubCanvas);

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

            book.playerState = playerState;
            book.itemDatabase = itemDb;
            hud.playerState = playerState;
            hud.itemDatabase = itemDb;

            BuildSelectCanvasContents(selectCanvas, selectUI);
            BuildHubCanvasContents(hubCanvas, selectUI, book);
            BuildInventoryBook(book, hud);
            BuildHud(hud);
            Transform hubRoot = hubSpawn.parent;
            var chest = BuildLootChest(hubRoot, playerState, itemDb);
            var pedestal = BuildBackpackPedestal(hubRoot, book);

            foreach (var b in new UdonSharpBehaviour[] { activeData, scratchData, saveManager, playerState, teleporter, selectUI, itemDb, book, hud, chest, pedestal })
            {
                UdonSharpEditorUtility.CopyProxyToUdon(b);
                EditorUtility.SetDirty(b);
            }

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
        private static void EnsureProgramAssets()
        {
            Directory.CreateDirectory(ProgramsDir);
            bool created = false;
            foreach (string name in new[] { "CharacterData", "SaveManager", "PlayerState", "TeleportHelper", "CharacterSelectUI", "ItemDatabase", "InventoryBook", "PlayerHUD", "LootChest", "EventRelay" })
            {
                string assetPath = ProgramsDir + "/" + name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptsDir + "/" + name + ".cs");
                if (script == null)
                {
                    Debug.LogError("[WizardsVRC] Missing script " + name + ".cs");
                    continue;
                }
                var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                program.sourceCsScript = script;
                AssetDatabase.CreateAsset(program, assetPath);
                created = true;
            }
            if (created)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            UdonSharpProgramAsset.CompileAllCsPrograms(true);
        }

        // ================================================================== select room

        private static Transform BuildSelectRoom(out Canvas canvas)
        {
            var root = new GameObject("SelectRoom").transform;
            root.position = SelectRoomOrigin;

            Material floor = Mat("SelectFloor", new Color(0.18f, 0.16f, 0.24f));
            Material wall = Mat("SelectWall", new Color(0.28f, 0.25f, 0.38f));

            const float w = 14f, d = 12f, h = 5f, t = 0.3f;
            Block(root, "Floor", new Vector3(0, -t / 2, 0), new Vector3(w, t, d), floor);
            Block(root, "Ceiling", new Vector3(0, h + t / 2, 0), new Vector3(w, t, d), wall);
            Block(root, "Wall_North", new Vector3(0, h / 2, d / 2 + t / 2), new Vector3(w, h, t), wall);
            Block(root, "Wall_South", new Vector3(0, h / 2, -d / 2 - t / 2), new Vector3(w, h, t), wall);
            Block(root, "Wall_East", new Vector3(w / 2 + t / 2, h / 2, 0), new Vector3(t, h, d), wall);
            Block(root, "Wall_West", new Vector3(-w / 2 - t / 2, h / 2, 0), new Vector3(t, h, d), wall);

            var lamp = Child(root, "RoomLight", new Vector3(0, h - 0.5f, 0));
            var light = lamp.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 18f;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.9f, 0.8f);

            Transform spawn = Child(root, "SelectSpawn", new Vector3(0, 0.05f, -3.5f));
            spawn.rotation = Quaternion.identity;

            var canvasGo = new GameObject("SelectCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root, false);
            canvas = MakeWorldCanvas(canvasGo, new Vector2(1600, 900), new Vector3(0, 2.4f, d / 2 - 0.1f), 0.0055f);
            return spawn;
        }

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

        private static Transform BuildHub(out Transform npcMarker, out Canvas hubCanvas)
        {
            var root = new GameObject("Hub").transform;

            Block(root, "Ground", new Vector3(0, -0.25f, 0), new Vector3(60, 0.5f, 60), Mat("HubGround", new Color(0.32f, 0.45f, 0.28f)));
            Block(root, "Plaza", new Vector3(0, 0.02f, 8), new Vector3(16, 0.1f, 16), Mat("HubPlaza", new Color(0.55f, 0.52f, 0.47f)));

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "SpawnLandingPad";
            pad.transform.SetParent(root, false);
            pad.transform.localPosition = new Vector3(0, 0.05f, 0);
            pad.transform.localScale = new Vector3(3f, 0.05f, 3f);
            pad.GetComponent<Renderer>().sharedMaterial = Mat("LandingPad", new Color(0.35f, 0.55f, 0.9f));

            Transform spawn = Child(root, "HubSpawn", new Vector3(0, 0.15f, 0));

            npcMarker = Child(root, "NPC_Placeholder", new Vector3(4f, 0f, 8f));
            npcMarker.rotation = Quaternion.Euler(0, 200f, 0);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(npcMarker, false);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            body.GetComponent<Renderer>().sharedMaterial = Mat("NPCMarker", new Color(0.85f, 0.7f, 0.2f));
            var tag = new GameObject("NameTag");
            tag.transform.SetParent(npcMarker, false);
            tag.transform.localPosition = new Vector3(0, 2.4f, 0);
            tag.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var tm = tag.AddComponent<TextMesh>();
            tm.text = "Headmaster (NPC placeholder)";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.08f;
            tm.fontSize = 48;
            tm.color = Color.white;

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
            Label(root, "Title", new Vector2(0, 120), new Vector2(560, 60), "Hub (stub)", 40, TextAnchor.MiddleCenter);
            Button(root, "ChangeCharacter", new Vector2(0, 10), new Vector2(420, 90), "Change Character", new Color(0.25f, 0.35f, 0.6f), target, "ReturnToSelect");
            Button(root, "OpenBackpack", new Vector2(0, -105), new Vector2(420, 90), "Backpack (B)", new Color(0.55f, 0.38f, 0.2f), bookTarget, "ToggleBook");
        }

        // ================================================================== backpack book

        private static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f);
        private static readonly Color Ink = new Color(0.22f, 0.15f, 0.08f);

        private static void BuildInventoryBook(InventoryBook book, PlayerHUD hud)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            UdonBehaviour hudTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(hud);

            var canvasGo = new GameObject("BookCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(book.transform, false);
            MakeWorldCanvas(canvasGo, new Vector2(1800, 1100), Vector3.zero, 0.0006f);
            RectTransform root = (RectTransform)canvasGo.transform;
            book.bookRoot = canvasGo;

            Panel(root, "Cover", Vector2.zero, new Vector2(1800, 1100), new Color(0.36f, 0.2f, 0.1f));
            RectTransform left = Panel(root, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            RectTransform right = Panel(root, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);
            Panel(root, "Spine", Vector2.zero, new Vector2(30, 1060), new Color(0.22f, 0.12f, 0.06f));

            // ---- left page: character, paper-doll slots, stats
            book.nameLabel = InkLabel(left, "Name", new Vector2(0, 450), new Vector2(800, 60), "Name", 44, TextAnchor.MiddleCenter);
            book.infoLabel = InkLabel(left, "Info", new Vector2(0, 395), new Vector2(800, 45), "Level 1", 30, TextAnchor.MiddleCenter);
            book.goldLabel = InkLabel(left, "Gold", new Vector2(0, 350), new Vector2(800, 45), "Gold: 0", 30, TextAnchor.MiddleCenter);
            Panel(left, "Portrait", new Vector2(0, 60), new Vector2(200, 420), new Color(0.75f, 0.68f, 0.55f));
            InkLabel(left, "PortraitLabel", new Vector2(0, 60), new Vector2(190, 100), "(wizard)", 26, TextAnchor.MiddleCenter);

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
            rt.sizeDelta = new Vector2(420, 200);
            rt.localScale = Vector3.one * 0.0005f;
            hud.hudRoot = canvasGo;

            Panel(rt, "Background", Vector2.zero, new Vector2(420, 200), new Color(0.08f, 0.07f, 0.12f, 0.7f));
            hud.nameLabel = Label(rt, "Name", new Vector2(0, 65), new Vector2(400, 45), "", 28, TextAnchor.MiddleLeft);
            hud.healthLabel = Label(rt, "Health", new Vector2(0, 20), new Vector2(400, 40), "", 26, TextAnchor.MiddleLeft);
            hud.healthLabel.color = new Color(1f, 0.55f, 0.55f);
            hud.manaLabel = Label(rt, "Mana", new Vector2(0, -22), new Vector2(400, 40), "", 26, TextAnchor.MiddleLeft);
            hud.manaLabel.color = new Color(0.55f, 0.75f, 1f);
            hud.goldLabel = Label(rt, "Gold", new Vector2(0, -64), new Vector2(400, 40), "", 26, TextAnchor.MiddleLeft);
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

            var label = new GameObject("ChestLabel");
            label.transform.SetParent(hubRoot, false);
            label.transform.localPosition = new Vector3(-5f, 1.4f, 10f);
            var tm = label.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.06f;
            tm.fontSize = 48;
            tm.color = Color.white;
            tm.text = "Practice Chest";

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
