using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The opening for a new character (AwakeningSequence + CharacterCreator): a dream space far from the world with
    /// five visions (the Starfall Crystal, the rift, the legion, the figure in grey iron, the title), the eyelid overlay
    /// that follows your head, floating subtitles, the wake spot in the Headmaster's study, the creator panel beside it,
    /// the dream's sounds (Tools/make_intro_audio.py) and the script itself, beat by beat.
    /// Voice lines can be dropped in as Audio/Voice/awaken_00.wav, awaken_01.wav... (one per beat).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 DreamOrigin = new Vector3(-2600f, 900f, -2600f);
        private const string IntroAudioDir = "Assets/WizardsVRC/Audio/Intro";

        /// <summary>Creator palette (index 0 = the wizard's own school colour, shown as white here).</summary>
        private static readonly Color[] LookPalette =
        {
            Color.white,
            new Color(0.86f, 0.16f, 0.2f), new Color(1f, 0.5f, 0.12f), new Color(1f, 0.82f, 0.25f), new Color(0.4f, 0.85f, 0.3f),
            new Color(0.15f, 0.72f, 0.52f), new Color(0.55f, 0.85f, 1f), new Color(0.25f, 0.42f, 0.95f), new Color(0.62f, 0.35f, 0.95f),
            new Color(1f, 0.45f, 0.7f), new Color(0.82f, 0.84f, 0.9f), new Color(0.36f, 0.24f, 0.48f),
        };
        private static readonly string[] LookPaletteNames =
            { "School color", "Crimson", "Ember", "Gold", "Meadow", "Jade", "Frost", "Royal", "Amethyst", "Rose", "Silver", "Shadow" };

        private static readonly string[] CreatorFirstNames =
        {
            "Aldric", "Ambrose", "Ash", "Bram", "Brynn", "Calla", "Cassia", "Corwin", "Dara", "Dorian", "Elara", "Elowen",
            "Ember", "Fenn", "Fiora", "Garrick", "Gideon", "Hazel", "Ilse", "Iris", "Jasper", "Joren", "Juniper", "Kael",
            "Kestrel", "Lark", "Lyra", "Mara", "Merrow", "Milo", "Nyx", "Odette", "Orrin", "Perrin", "Piper", "Quinn",
            "Rhiannon", "Rowan", "Sable", "Silas", "Tamsin", "Thane", "Una", "Vale", "Wynn", "Yara", "Zephyr", "Zinnia",
        };
        private static readonly string[] CreatorSurnameStarts =
        {
            "Ash", "Bright", "Cinder", "Dawn", "Dusk", "Ember", "Fable", "Frost", "Gale", "Gloom", "Hollow", "Iron",
            "Lumen", "Mist", "Moon", "Raven", "Rune", "Shadow", "Spell", "Star", "Storm", "Thorn", "Willow", "Wild",
        };
        private static readonly string[] CreatorSurnameEnds =
        {
            "bane", "bloom", "brook", "caller", "crest", "dale", "fall", "field", "flame", "forge", "glen", "grove",
            "heart", "mantle", "mere", "ridge", "shade", "song", "spark", "thistle", "vale", "weaver", "wick", "wind",
        };

        private class Beat
        {
            public string speaker, text;
            public float seconds, open;
            public int vision = -1, place, style, flags;
            public Beat(string speaker, string text, float seconds, float open, int vision, int place, int style, int flags)
            {
                this.speaker = speaker; this.text = text; this.seconds = seconds; this.open = open;
                this.vision = vision; this.place = place; this.style = style; this.flags = flags;
            }
        }

        private static List<Beat> AwakeningScript(bool outdoorSchools, out int askName, out int outro)
        {
            const int D = AwakeningSequence.PLACE_DREAM, O = AwakeningSequence.PLACE_STUDY;
            const int W = AwakeningSequence.STYLE_WHISPER, S = AwakeningSequence.STYLE_SPEECH, T = AwakeningSequence.STYLE_TITLE;
            const int FLASH = AwakeningSequence.FLAG_FLASH, FLUTTER = AwakeningSequence.FLAG_FLUTTER, HEART = AwakeningSequence.FLAG_HEARTBEAT;
            const string H = "Headmaster Greymantle";
            var b = new List<Beat>
            {
                new Beat("", "", 2.5f, 0f, -1, D, W, HEART),
                new Beat("", "...can you hear us?", 4f, 0f, -1, D, W, 0),
                new Beat("", "...wake...", 3.5f, 0f, -1, D, W, HEART),
                new Beat("", "...the light remembers you...", 5.5f, 0.55f, 0, D, W, FLASH),
                new Beat("", "...you fell through the dark once before...", 5.5f, 0.6f, 1, D, W, FLASH),
                new Beat("", "...they are coming. Iron, and fire, and drums...", 6f, 0.65f, 2, D, W, FLASH | AwakeningSequence.FLAG_DRUMS_ON),
                new Beat("", "...she has not forgotten you...", 5f, 0.6f, 3, D, W, FLASH | AwakeningSequence.FLAG_DRUMS_OFF),
                new Beat("", "", 1.2f, 0f, -1, D, W, 0),
                new Beat("", "", 5f, 0.75f, 4, D, T, FLASH),
                new Beat("A voice", "Steady... steady now. Can you hear me?", 4f, 0f, -1, D, S, AwakeningSequence.FLAG_DREAM_SOUND_OFF),
                new Beat(H, "There you are. Slowly, now. Open your eyes. You are safe.", 5f, 0.9f, -1, O, S, FLUTTER),
                new Beat(H, "You are in Starfall Castle, in my study. I am Headmaster Greymantle.", 5f, 1f, -1, O, S, 0),
                new Beat(H, "For seven nights I have dreamed the same dream: a darkness gathering beyond the edge of the Everweave.", 6.5f, 1f, -1, O, S, 0),
                new Beat(H, "Not Morvane, nor any shadow I have fought before. Something vast, and patient, that takes every light it touches.", 7f, 1f, -1, O, S, 0),
                new Beat(H, "In every dream it swallows our worlds, one after another. In every dream but one.", 5.5f, 1f, -1, O, S, 0),
                new Beat(H, "In that one, a wizard I had never met stood beside us, and the darkness broke against them.", 6f, 1f, -1, O, S, 0),
                new Beat(H, "Last night the Starfall Crystal flared like a falling star. We found you beneath it, cold as starlight, remembering nothing.", 7f, 1f, -1, O, S, 0),
                new Beat(H, "Let us start with what you can tell me. What is your name?", 4.5f, 1f, -1, O, S, 0),
                // ---- after the creator
                new Beat(H, "{name}. Yes... I believe I have heard that name before. In the dream.", 5f, 1f, -1, O, S, 0),
                new Beat(H, "And {school} magic answers you. Good. Your professor will teach you to wield it.", 5f, 1f, -1, O, S, 0),
                new Beat(H, "The crystal is fading, and old enemies stir in the north. I will not pretend we are ready for what I have seen.", 6f, 1f, -1, O, S, 0),
                new Beat(H, outdoorSchools
                    ? "Your professor waits at the School of {school}'s hall, out in the castle grounds. Go, and find them."
                    : "Your professor waits in the Hall of Schools, beyond that door. Go, and find them.", 5.5f, 1f, -1, O, S, 0),
                new Beat(H, "And {name}... what you saw while you slept, keep it to yourself. For now.", 5f, 1f, -1, O, S, 0),
            };
            askName = 17;
            outro = 18;
            return b;
        }

        /// <summary>Builds the opening and wires it to the intro flow and the character select. Returns the behaviours to sync.</summary>
        private static UdonSharpBehaviour[] BuildAwakening(Transform systems, Interior interior, CharacterSelectUI selectUI, TeleportHelper teleporter,
            IntroFlow flow, SaveManager saveManager, PlayerHUD hud, Canvas selectCanvas)
        {
            var seq = UdonSharpUndo.AddComponent<AwakeningSequence>(Child(systems, "AwakeningSequence", Vector3.zero).gameObject);
            var creator = UdonSharpUndo.AddComponent<CharacterCreator>(Child(systems, "CharacterCreator", Vector3.zero).gameObject);

            // ---------------- the dream
            var dream = new GameObject("DreamSet").transform;
            dream.position = DreamOrigin;
            var voidGo = new GameObject("Void");
            voidGo.transform.SetParent(dream, false);
            voidGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            voidGo.AddComponent<MeshFilter>().sharedMesh = InsideSphereMesh();
            var voidR = voidGo.AddComponent<MeshRenderer>();
            voidR.sharedMaterial = UnlitMat("DreamVoid", new Color(0.025f, 0.018f, 0.05f));
            voidR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            voidGo.transform.localScale = Vector3.one * 60f;
            var stars = new GameObject("DreamStars");
            stars.transform.SetParent(dream, false);
            stars.transform.localPosition = new Vector3(0, 1.6f, 0);
            stars.AddComponent<MeshFilter>().sharedMesh = StarShellMesh();
            var starR = stars.AddComponent<MeshRenderer>();
            starR.sharedMaterial = UnlitMat("DreamStar", new Color(0.75f, 0.72f, 1f));
            starR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var floor = new GameObject("DreamFloor");
            floor.transform.SetParent(dream, false);
            floor.transform.localPosition = new Vector3(0, -0.5f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(30f, 1f, 30f);
            var dreamSpot = Child(dream, "DreamSpot", new Vector3(0, 0.02f, 0));

            var visions = new GameObject[5];
            visions[0] = VisionCrystal(dream);
            visions[1] = VisionRift(dream);
            visions[2] = VisionLegion(dream);
            visions[3] = VisionHer(dream);
            visions[4] = VisionTitle(dream);
            foreach (var v in visions)
                foreach (Transform t in v.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;

            // ---------------- sounds
            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(IntroAudioDir + "/" + n + ".wav");
            AudioSource Source(Transform parent, string n, Vector3 at, AudioClip clip, bool loop, float spatial)
            {
                var a = Child(parent, n, at).gameObject.AddComponent<AudioSource>();
                a.clip = clip;
                a.loop = loop;
                a.playOnAwake = false;
                a.spatialBlend = spatial;
                a.minDistance = 1.5f;
                a.maxDistance = 20f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.dopplerLevel = 0f;
                a.volume = 0f;
                return a;
            }
            if (Clip("DreamDrone") == null) Debug.LogWarning("[WizardsVRC] Intro sounds missing: run python3 Tools/make_intro_audio.py");
            seq.droneSource = Source(dream, "Drone", new Vector3(0, 1.6f, 0), Clip("DreamDrone"), true, 0f);
            seq.whisperSources = new[]
            {
                Source(dream, "WhisperL", new Vector3(-2.5f, 1.7f, -1f), Clip("Whispers"), true, 1f),
                Source(dream, "WhisperR", new Vector3(2.5f, 1.4f, 0.5f), Clip("Whispers"), true, 1f),
                Source(dream, "WhisperBehind", new Vector3(0.3f, 2.2f, -2.5f), Clip("Whispers"), true, 1f),
            };
            seq.drumSource = Source(dream, "Drums", new Vector3(0, 2f, 7f), Clip("WarDrums"), true, 0.5f);
            seq.drumSource.volume = 1f;
            seq.sfxSource = Source(systems, "AwakeningSfx", Vector3.zero, null, false, 0f);
            seq.sfxSource.volume = 0.8f;
            seq.voiceSource = Source(systems, "AwakeningVoice", Vector3.zero, null, false, 0f);
            seq.voiceSource.volume = 1f;
            seq.flashClip = Clip("VisionFlash");
            seq.heartbeatClip = Clip("Heartbeat");

            // ---------------- eyelids: an inside-out sphere on your head
            var lids = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lids.name = "AwakeningLids";
            lids.transform.SetParent(systems, false);
            lids.transform.localScale = Vector3.one * 0.36f;
            Object.DestroyImmediate(lids.GetComponent<Collider>());
            var lidR = lids.GetComponent<Renderer>();
            lidR.sharedMaterial = EyelidMat();
            lidR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lidR.receiveShadows = false;
            lids.SetActive(false);
            seq.lids = lids.transform;
            seq.lidRenderer = lidR;

            // ---------------- words, floating ahead of you (drawn over the eyelids)
            var wordsGo = new GameObject("AwakeningWords", typeof(RectTransform));
            wordsGo.transform.SetParent(systems, false);
            MakeWorldCanvas(wordsGo, new Vector2(1500, 420), Vector3.zero, 0.0011f);
            var wr = (RectTransform)wordsGo.transform;
            seq.speakerLabel = Label(wr, "Speaker", new Vector2(0, 130), new Vector2(1400, 60), "", 38, TextAnchor.MiddleCenter);
            seq.speakerLabel.color = new Color(1f, 0.82f, 0.45f);
            seq.subtitleLabel = Label(wr, "Subtitle", new Vector2(0, 10), new Vector2(1400, 180), "", 44, TextAnchor.MiddleCenter);
            seq.titleLabel = Label(wr, "Title", new Vector2(0, 10), new Vector2(1400, 200), "", 90, TextAnchor.MiddleCenter);
            seq.titleLabel.color = new Color(1f, 0.85f, 0.45f, 0f);
            foreach (var l in new[] { seq.speakerLabel, seq.subtitleLabel, seq.titleLabel }) l.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            UdonBehaviour seqTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(seq);
            seq.skipButton = Button(wr, "SkipIntro", new Vector2(560, -170), new Vector2(240, 60), "Skip intro", new Color(0.25f, 0.22f, 0.32f, 0.85f), seqTarget, nameof(AwakeningSequence.Skip));
            seq.skipButton.GetComponentInChildren<Text>().fontSize = 26;
            var overlay = AboveEyelidsUiMat();
            foreach (var g in wordsGo.GetComponentsInChildren<Graphic>(true)) g.material = overlay;
            // Like the cutscene screen: on the Walkthrough layer, so it can be clicked but never bumps into you.
            int wordsLayer = LayerMask.NameToLayer("Walkthrough");
            if (wordsLayer < 0) wordsLayer = 17;
            foreach (Transform t in wordsGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = wordsLayer;
            wordsGo.SetActive(false);
            seq.subtitleRoot = wordsGo.transform;

            // ---------------- the study: where you wake, and the creator beside you
            Transform office = interior.office;
            var wake = Child(office, "WakeSpot", new Vector3(0, 0.05f, 0.4f));
            wake.localRotation = Quaternion.Euler(0, 180f, 0);
            seq.dreamSpot = dreamSpot;
            seq.wakeSpot = wake;
            BuildCreatorPanel(office, wake, creator);

            // ---------------- the script
            var beats = AwakeningScript(interior.outdoorSchools, out int askName, out int outro);
            int n = beats.Count;
            seq.beatSpeaker = new string[n];
            seq.beatText = new string[n];
            seq.beatSeconds = new float[n];
            seq.beatOpen = new float[n];
            seq.beatVision = new int[n];
            seq.beatPlace = new int[n];
            seq.beatStyle = new int[n];
            seq.beatFlags = new int[n];
            seq.beatVoice = new AudioClip[n];
            for (int i = 0; i < n; i++)
            {
                Beat bt = beats[i];
                seq.beatSpeaker[i] = bt.speaker;
                seq.beatText[i] = bt.text;
                seq.beatSeconds[i] = bt.seconds;
                seq.beatOpen[i] = bt.open;
                seq.beatVision[i] = bt.vision;
                seq.beatPlace[i] = bt.place;
                seq.beatStyle[i] = bt.style;
                seq.beatFlags[i] = bt.flags;
                seq.beatVoice[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + "/awaken_" + i.ToString("00") + ".wav");
            }
            seq.askNameBeat = askName;
            seq.outroStart = outro;
            seq.schoolNames = (string[])SchoolLabels.Clone();
            seq.visions = visions;

            // ---------------- wiring
            seq.teleporter = teleporter;
            seq.selectUI = selectUI;
            seq.creator = creator;
            seq.selectCanvas = selectCanvas;
            seq.hud = hud;
            creator.selectUI = selectUI;
            creator.sequence = seq;
            selectUI.awakening = seq;
            flow.awakening = seq;
            flow.saveManager = saveManager;
            flow.selectUI = selectUI;
            return new UdonSharpBehaviour[] { seq, creator };
        }

        // ================================================================== the creator panel

        private static void BuildCreatorPanel(Transform office, Transform wake, CharacterCreator creator)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(creator);
            // To your right and a little ahead, so the Headmaster stays in view.
            Vector3 at = wake.localPosition + new Vector3(-1.0f, 1.3f, -1.15f);
            Vector3 look = at - wake.localPosition;
            look.y = 0f;
            var go = new GameObject("CharacterCreatorCanvas", typeof(RectTransform));
            go.transform.SetParent(office, false);
            MakeWorldCanvas(go, new Vector2(1150, 800), at, 0.0011f);
            go.transform.localRotation = Quaternion.LookRotation(look);
            var root = (RectTransform)go.transform;
            Color bg = new Color(0.1f, 0.08f, 0.15f, 0.95f), ink = new Color(1f, 0.86f, 0.5f);
            Color btn = new Color(0.3f, 0.26f, 0.45f), go2 = new Color(0.22f, 0.5f, 0.3f), back = new Color(0.35f, 0.33f, 0.38f);
            Panel(root, "Background", Vector2.zero, new Vector2(1150, 800), bg);
            Panel(root, "Border", new Vector2(0, 385), new Vector2(1150, 6), ink);
            creator.stepLabel = Label(root, "Step", new Vector2(440, 355), new Vector2(240, 40), "", 24, TextAnchor.MiddleRight);
            creator.stepLabel.color = new Color(0.75f, 0.7f, 0.85f);
            creator.root = go;
            creator.steps = new GameObject[4];
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // ---- 1. name
            var s0 = Rect(root, "StepName", Vector2.zero, new Vector2(1150, 800));
            Label(s0, "Ask", new Vector2(0, 300), new Vector2(1000, 60), "What is your name?", 44, TextAnchor.MiddleCenter).color = ink;
            creator.namePreview = Label(s0, "Preview", new Vector2(0, 200), new Vector2(1050, 90), "", 64, TextAnchor.MiddleCenter);
            string[] rowNames = { "First name", "Surname", "" };
            string[] rowEvents = { "First", "Start", "End" };
            var values = new Text[3];
            for (int r = 0; r < 3; r++)
            {
                float y = 70 - r * 100;
                Label(s0, "RowLabel" + r, new Vector2(-420, y), new Vector2(220, 60), rowNames[r], 30, TextAnchor.MiddleRight).color = new Color(0.8f, 0.76f, 0.9f);
                Button(s0, rowEvents[r] + "Prev", new Vector2(-230, y), new Vector2(80, 80), "<", btn, target, rowEvents[r] + "Prev");
                Panel(s0, "Value" + r + "Back", new Vector2(0, y), new Vector2(340, 80), new Color(0.05f, 0.04f, 0.08f, 1f));
                values[r] = Label(s0, "Value" + r, new Vector2(0, y), new Vector2(330, 80), "", 40, TextAnchor.MiddleCenter);
                Button(s0, rowEvents[r] + "Next", new Vector2(230, y), new Vector2(80, 80), ">", btn, target, rowEvents[r] + "Next");
            }
            creator.firstLabel = values[0];
            creator.startLabel = values[1];
            creator.endLabel = values[2];
            Button(s0, "RandomName", new Vector2(-180, -290), new Vector2(300, 80), "Random", btn, target, nameof(CharacterCreator.RandomName));
            Button(s0, "NameContinue", new Vector2(180, -290), new Vector2(300, 80), "Continue", go2, target, nameof(CharacterCreator.NameNext));
            creator.steps[0] = s0.gameObject;
            creator.firstNames = CreatorFirstNames;
            creator.surnameStarts = CreatorSurnameStarts;
            creator.surnameEnds = CreatorSurnameEnds;

            // ---- 2. school
            var s1 = Rect(root, "StepSchool", Vector2.zero, new Vector2(1150, 800));
            creator.schoolTitle = Label(s1, "SchoolTitle", new Vector2(0, 300), new Vector2(1000, 70), "", 48, TextAnchor.MiddleCenter);
            creator.schoolMarks = new GameObject[SchoolLabels.Length];
            for (int i = 0; i < SchoolLabels.Length; i++)
            {
                float x = -465 + i * 155;
                creator.schoolMarks[i] = Panel(s1, "Mark" + i, new Vector2(x, 170), new Vector2(150, 108), ink).gameObject;
                var b = Button(s1, "School" + SchoolLabels[i], new Vector2(x, 170), new Vector2(138, 96), SchoolLabels[i], SchoolColors[i] * 0.75f, target, "School" + i);
                b.GetComponentInChildren<Text>().fontSize = 26;
            }
            creator.schoolBlurb = Label(s1, "Blurb", new Vector2(0, -20), new Vector2(980, 220), "", 34, TextAnchor.MiddleCenter);
            Button(s1, "SchoolBack", new Vector2(-180, -290), new Vector2(300, 80), "Back", back, target, nameof(CharacterCreator.SchoolBack));
            creator.schoolNext = Button(s1, "SchoolContinue", new Vector2(180, -290), new Vector2(300, 80), "Continue", go2, target, nameof(CharacterCreator.SchoolNext));
            creator.steps[1] = s1.gameObject;
            creator.schoolNames = (string[])SchoolLabels.Clone();
            creator.schoolBlurbs = (string[])ProfessorIntro.Clone();
            creator.schoolColors = (Color[])SchoolColors.Clone();

            // ---- 3. colours
            var s2 = Rect(root, "StepColours", Vector2.zero, new Vector2(1150, 800));
            int k = LookPalette.Length;
            creator.sigMarks = new GameObject[k];
            creator.auraMarks = new GameObject[k];
            creator.sigSwatches = new Image[k];
            creator.auraSwatches = new Image[k];
            Label(s2, "SigTitle", new Vector2(0, 310), new Vector2(1000, 50), "Signature color: your name, your duel seat and your casting ring", 28, TextAnchor.MiddleCenter).color = ink;
            Label(s2, "AuraTitle", new Vector2(0, 90), new Vector2(1000, 50), "Pip aura: the glow round you when you hold a lot of pips", 28, TextAnchor.MiddleCenter).color = ink;
            for (int i = 0; i < k; i++)
            {
                float x = -462 + i * 84;
                creator.sigMarks[i] = Panel(s2, "SigMark" + i, new Vector2(x, 235), new Vector2(82, 82), Color.white).gameObject;
                var sb = Button(s2, "Sig" + i, new Vector2(x, 235), new Vector2(68, 68), "", LookPalette[i], target, "Sig" + i);
                creator.sigSwatches[i] = sb.GetComponent<Image>();
                creator.auraMarks[i] = Panel(s2, "AuraMark" + i, new Vector2(x, 15), new Vector2(82, 82), Color.white).gameObject;
                var ab = Button(s2, "Aura" + i, new Vector2(x, 15), new Vector2(68, 68), "", LookPalette[i], target, "Aura" + i);
                creator.auraSwatches[i] = ab.GetComponent<Image>();
            }
            creator.sigLabel = Label(s2, "SigName", new Vector2(0, 165), new Vector2(1000, 40), "", 26, TextAnchor.MiddleCenter);
            creator.auraLabel = Label(s2, "AuraName", new Vector2(0, -55), new Vector2(1000, 40), "", 26, TextAnchor.MiddleCenter);
            var ring = Panel(s2, "AuraPreview", new Vector2(300, -170), new Vector2(150, 150), Color.white);
            ring.GetComponent<Image>().sprite = knob;
            creator.auraPreview = ring.GetComponent<Image>();
            creator.colorPreviewName = Label(s2, "NamePreview", new Vector2(-130, -170), new Vector2(700, 80), "", 46, TextAnchor.MiddleCenter);
            creator.colorPreviewName.gameObject.AddComponent<Outline>().effectColor = Color.black;
            Button(s2, "ColoursBack", new Vector2(-180, -305), new Vector2(300, 76), "Back", back, target, nameof(CharacterCreator.ColorsBack));
            Button(s2, "ColoursContinue", new Vector2(180, -305), new Vector2(300, 76), "Continue", go2, target, nameof(CharacterCreator.ColorsNext));
            creator.steps[2] = s2.gameObject;
            creator.palette = (Color[])LookPalette.Clone();
            creator.colorNames = (string[])LookPaletteNames.Clone();

            // ---- 4. confirm
            var s3 = Rect(root, "StepConfirm", Vector2.zero, new Vector2(1150, 800));
            Label(s3, "Ready", new Vector2(0, 300), new Vector2(1000, 60), "Is this you?", 44, TextAnchor.MiddleCenter).color = ink;
            var aura = Panel(s3, "SummaryAura", new Vector2(0, 150), new Vector2(560, 200), Color.white);
            aura.GetComponent<Image>().sprite = knob;
            creator.summaryAura = aura.GetComponent<Image>();
            creator.summaryName = Label(s3, "SummaryName", new Vector2(0, 150), new Vector2(1050, 100), "", 64, TextAnchor.MiddleCenter);
            creator.summaryName.gameObject.AddComponent<Outline>().effectColor = Color.black;
            creator.summaryDetails = Label(s3, "SummaryDetails", new Vector2(0, -60), new Vector2(1000, 180), "", 34, TextAnchor.MiddleCenter);
            Button(s3, "ConfirmBack", new Vector2(-180, -290), new Vector2(300, 80), "Back", back, target, nameof(CharacterCreator.ConfirmBack));
            Button(s3, "Begin", new Vector2(180, -290), new Vector2(300, 80), "Begin", go2, target, nameof(CharacterCreator.Begin));
            creator.steps[3] = s3.gameObject;

            for (int i = 1; i < 4; i++) creator.steps[i].SetActive(false);
            go.SetActive(false);
        }

        // ================================================================== the visions

        private static Transform VisionRoot(Transform dream, string name)
        {
            var t = Child(dream, name, new Vector3(0, 0, 8f));
            return t;
        }

        /// <summary>The Starfall Crystal blazing on its basin, castle towers behind, stars falling.</summary>
        private static GameObject VisionCrystal(Transform dream)
        {
            var v = VisionRoot(dream, "VisionCrystal");
            var stone = PMat("DreamStone", new Color(0.35f, 0.38f, 0.5f), 1f);
            var dark = PMat("DreamCastle", new Color(0.08f, 0.07f, 0.14f), 1f, new Color(0.05f, 0.04f, 0.1f));
            var crystal = PMat("DreamCrystal", new Color(0.7f, 0.92f, 1f), 1f, new Color(0.6f, 0.9f, 1f) * 1.6f);
            var star = PMat("DreamFallingStar", new Color(1f, 0.92f, 0.7f), 1f, new Color(1f, 0.9f, 0.6f) * 2f);
            Shape(v, "Basin", CylMesh(), new Vector3(0, 0.45f, 0), new Vector3(3f, 0.9f, 3f), stone, false, Quaternion.identity);
            Shape(v, "Crystal", GemMesh(), new Vector3(0, 2.6f, 0), new Vector3(1.2f, 2.8f, 1.2f), crystal, false, Quaternion.Euler(0, 15f, 0));
            PointLight(v, new Vector3(0, 2.6f, -1.6f), new Color(0.6f, 0.9f, 1f), 12f, 2.2f);
            float[] tx = { -6f, -3.2f, 3.2f, 6f };
            float[] th = { 7f, 9f, 8.5f, 6.5f };
            for (int i = 0; i < tx.Length; i++)
            {
                Shape(v, "Tower" + i, CylMesh(), new Vector3(tx[i], th[i] / 2f, 7f), new Vector3(1.6f, th[i], 1.6f), dark, false, Quaternion.identity);
                Shape(v, "Spire" + i, ConeMesh(), new Vector3(tx[i], th[i] + 0.9f, 7f), new Vector3(1.9f, 1.8f, 1.9f), dark, false, Quaternion.identity);
            }
            Deco(v, PrimitiveType.Cube, "Wall", new Vector3(0, 2.2f, 7.5f), new Vector3(13f, 4.4f, 0.8f), dark, Quaternion.identity, false);
            for (int i = 0; i < 7; i++)
            {
                float x = -6f + i * 2f + (i % 2) * 0.7f, y = 6f + (i * 37 % 5) * 0.7f;
                Deco(v, PrimitiveType.Cube, "FallingStar" + i, new Vector3(x, y, 2f + i % 3), new Vector3(0.06f, 0.06f, 1.8f), star, Quaternion.Euler(35f, -20f, 0), false);
            }
            v.gameObject.SetActive(false);
            return v.gameObject;
        }

        /// <summary>A rift torn in the sky with debris round it, and a small figure falling out of it.</summary>
        private static GameObject VisionRift(Transform dream)
        {
            var v = VisionRoot(dream, "VisionRift");
            var outer = PMat("DreamRiftOuter", new Color(0.35f, 0.1f, 0.6f), 1f, new Color(0.35f, 0.1f, 0.6f) * 1.4f);
            var inner = PMat("DreamRiftInner", new Color(0.95f, 0.88f, 1f), 1f, new Color(0.95f, 0.85f, 1f) * 2f);
            var debris = PMat("DreamDebris", new Color(0.12f, 0.1f, 0.16f), 1f);
            var figure = PMat("DreamFigure", new Color(0.05f, 0.05f, 0.08f), 1f, new Color(0.25f, 0.2f, 0.4f) * 0.6f);
            AddMesh(v, "RiftOuter", RiftMesh("PSX_DreamRiftOuter", 1.0f), outer);
            AddMesh(v, "RiftInner", RiftMesh("PSX_DreamRiftInner", 0.42f), inner, new Vector3(0, 0, -0.05f));
            PointLight(v, new Vector3(0, 3.2f, -2f), new Color(0.7f, 0.5f, 1f), 12f, 2f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 0.52f;
                Vector3 p = new Vector3(Mathf.Cos(a) * (2f + i % 3), 3.2f + Mathf.Sin(a * 1.7f) * 2.4f, -0.3f - (i % 4) * 0.3f);
                float s = 0.15f + (i % 4) * 0.1f;
                Deco(v, PrimitiveType.Cube, "Debris" + i, p, new Vector3(s, s * 0.8f, s), debris, Quaternion.Euler(i * 31f, i * 47f, i * 13f), false);
            }
            Deco(v, PrimitiveType.Capsule, "FallingFigure", new Vector3(1.3f, 1.8f, -0.8f), new Vector3(0.28f, 0.42f, 0.28f), figure, Quaternion.Euler(0, 0, 38f), false);
            v.gameObject.SetActive(false);
            return v.gameObject;
        }

        /// <summary>Ranks of iron soldiers with slit visors and red banners, lit red from behind.</summary>
        private static GameObject VisionLegion(Transform dream)
        {
            var v = VisionRoot(dream, "VisionLegion");
            var iron = PMat("DreamIron", new Color(0.3f, 0.31f, 0.34f), 1f);
            var visor = PMat("DreamVisor", new Color(1f, 0.15f, 0.1f), 1f, new Color(1f, 0.12f, 0.08f) * 2f);
            var shield = PMat("DreamShield", new Color(0.32f, 0.07f, 0.07f), 1f);
            var red = PMat("DreamBanner", new Color(0.6f, 0.06f, 0.08f), 1f, new Color(0.25f, 0.02f, 0.03f));
            var black = PMat("DreamEmblem", new Color(0.03f, 0.03f, 0.04f), 1f);
            var gold = PMat("DreamEmblemStar", new Color(1f, 0.8f, 0.3f), 1f, new Color(1f, 0.75f, 0.2f));
            for (int r = 0; r < 3; r++)
                for (int c = -3; c <= 3; c++)
                {
                    Vector3 p = new Vector3(c * 1.1f + (r % 2) * 0.55f, 0, r * 1.5f);
                    string id = r + "_" + (c + 3);
                    Deco(v, PrimitiveType.Cube, "Body" + id, p + new Vector3(0, 0.75f, 0), new Vector3(0.55f, 1.1f, 0.35f), iron, Quaternion.identity, false);
                    Deco(v, PrimitiveType.Cube, "Helm" + id, p + new Vector3(0, 1.5f, 0), new Vector3(0.32f, 0.36f, 0.32f), iron, Quaternion.identity, false);
                    Deco(v, PrimitiveType.Cube, "Visor" + id, p + new Vector3(0, 1.53f, -0.17f), new Vector3(0.24f, 0.05f, 0.02f), visor, Quaternion.identity, false);
                    Deco(v, PrimitiveType.Cube, "Shield" + id, p + new Vector3(-0.1f, 0.85f, -0.24f), new Vector3(0.5f, 0.8f, 0.08f), shield, Quaternion.identity, false);
                }
            foreach (float x in new[] { -4f, 0f, 4f })
            {
                Deco(v, PrimitiveType.Cylinder, "BannerPole", new Vector3(x, 2.4f, 4.4f), new Vector3(0.08f, 2.4f, 0.08f), iron, Quaternion.identity, false);
                Deco(v, PrimitiveType.Cube, "Banner", new Vector3(x, 3.4f, 4.35f), new Vector3(1.3f, 2.2f, 0.05f), red, Quaternion.identity, false);
                Deco(v, PrimitiveType.Cylinder, "Emblem", new Vector3(x, 3.6f, 4.3f), new Vector3(0.75f, 0.01f, 0.75f), black, Quaternion.Euler(90f, 0, 0), false);
                Shape(v, "EmblemStar", GemMesh(), new Vector3(x, 3.6f, 4.27f), new Vector3(0.3f, 0.3f, 0.05f), gold, false, Quaternion.Euler(0, 0, 0));
            }
            PointLight(v, new Vector3(0, 4f, 7f), new Color(1f, 0.25f, 0.15f), 16f, 2.4f);
            PointLight(v, new Vector3(0, 2f, -3f), new Color(0.6f, 0.2f, 0.2f), 8f, 0.8f);
            v.gameObject.SetActive(false);
            return v.gameObject;
        }

        /// <summary>A tall figure in grey iron and a red cloak, her back to you, over the shards of a broken crystal.</summary>
        private static GameObject VisionHer(Transform dream)
        {
            var v = VisionRoot(dream, "VisionHer");
            var iron = PMat("DreamIronDark", new Color(0.24f, 0.25f, 0.28f), 1f);
            var cloak = PMat("DreamCloak", new Color(0.5f, 0.05f, 0.08f), 1f, new Color(0.12f, 0.01f, 0.02f));
            var shard = PMat("DreamShard", new Color(0.5f, 0.7f, 0.85f), 1f, new Color(0.3f, 0.5f, 0.7f) * 0.8f);
            Deco(v, PrimitiveType.Capsule, "Body", new Vector3(0, 1.25f, 0), new Vector3(0.8f, 1.15f, 0.6f), iron, Quaternion.identity, false);
            Deco(v, PrimitiveType.Cube, "Shoulders", new Vector3(0, 2.0f, 0), new Vector3(1.35f, 0.3f, 0.6f), iron, Quaternion.identity, false);
            Deco(v, PrimitiveType.Cube, "Helm", new Vector3(0, 2.42f, 0.02f), new Vector3(0.44f, 0.5f, 0.46f), iron, Quaternion.identity, false);
            Deco(v, PrimitiveType.Cube, "Plume", new Vector3(0, 2.8f, 0.05f), new Vector3(0.08f, 0.3f, 0.5f), cloak, Quaternion.identity, false);
            Deco(v, PrimitiveType.Cube, "Cloak", new Vector3(0, 1.2f, -0.38f), new Vector3(1.15f, 1.8f, 0.06f), cloak, Quaternion.Euler(-4f, 0, 0), false);
            Deco(v, PrimitiveType.Cube, "ArmL", new Vector3(-0.6f, 1.3f, 0.05f), new Vector3(0.22f, 0.9f, 0.22f), iron, Quaternion.Euler(0, 0, -6f), false);
            Deco(v, PrimitiveType.Cube, "ArmR", new Vector3(0.6f, 1.3f, 0.05f), new Vector3(0.22f, 0.9f, 0.22f), iron, Quaternion.Euler(0, 0, 6f), false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f;
                Vector3 p = new Vector3(Mathf.Cos(a) * (0.9f + i % 2 * 0.5f), 0.25f, 0.9f + Mathf.Sin(a) * 0.8f);
                Shape(v, "Shard" + i, GemMesh(), p, new Vector3(0.35f, 0.7f + (i % 3) * 0.3f, 0.35f), shard, false, Quaternion.Euler(20f + i * 25f, i * 60f, 30f - i * 15f));
            }
            PointLight(v, new Vector3(0, 0.5f, 1f), new Color(0.5f, 0.75f, 1f), 6f, 1.6f);
            PointLight(v, new Vector3(0, 3f, 3f), new Color(1f, 0.25f, 0.2f), 9f, 1.8f);
            v.gameObject.SetActive(false);
            return v.gameObject;
        }

        /// <summary>The title, floating in the dark among a few stars.</summary>
        private static GameObject VisionTitle(Transform dream)
        {
            var v = VisionRoot(dream, "VisionTitle");
            Font pixel = PixelFont();
            var title = WorldText(v, "Title", new Vector3(0, 2.4f, 0), Quaternion.identity, new Vector2(1800, 320), 0.006f, "WIZARDS VRC", 210);
            title.color = new Color(1f, 0.85f, 0.45f);
            var sub = WorldText(v, "Subtitle", new Vector3(0, 1.1f, 0), Quaternion.identity, new Vector2(1800, 160), 0.006f, "Starfall Isle", 90);
            sub.color = new Color(0.8f, 0.78f, 1f);
            if (pixel != null) { title.font = pixel; sub.font = pixel; }
            var star = PMat("DreamTitleStar", new Color(1f, 0.95f, 0.8f), 1f, new Color(1f, 0.9f, 0.7f) * 2f);
            for (int i = 0; i < 9; i++)
                Shape(v, "Star" + i, GemMesh(), new Vector3(-5f + i * 1.25f, 3.6f + (i * 53 % 7) * 0.25f - (i % 2) * 2.6f, 0.5f), Vector3.one * 0.12f, star, false, Quaternion.identity);
            v.gameObject.SetActive(false);
            return v.gameObject;
        }

        // ================================================================== meshes and materials

        /// <summary>A zigzag tear, 6 m tall, facing -Z (towards you). width scales how wide it opens.</summary>
        private static Mesh RiftMesh(string name, float width)
        {
            var mb = new MeshBuilder();
            float[] ys = { 0.4f, 1.1f, 1.7f, 2.4f, 3.0f, 3.7f, 4.3f, 5.0f, 5.6f, 6.3f };
            float[] xs = { 0f, 0.35f, -0.25f, 0.4f, -0.3f, 0.3f, -0.4f, 0.2f, -0.15f, 0f };
            float[] half = { 0f, 0.25f, 0.4f, 0.55f, 0.7f, 0.65f, 0.5f, 0.35f, 0.2f, 0f };
            for (int i = 0; i + 1 < ys.Length; i++)
            {
                Vector3 l0 = new Vector3(xs[i] - half[i] * width, ys[i], 0), r0 = new Vector3(xs[i] + half[i] * width, ys[i], 0);
                Vector3 l1 = new Vector3(xs[i + 1] - half[i + 1] * width, ys[i + 1], 0), r1 = new Vector3(xs[i + 1] + half[i + 1] * width, ys[i + 1], 0);
                mb.Quad(l0, r0, r1, l1, Vector3.back);
            }
            return SaveBuiltMesh(name, mb);
        }

        /// <summary>A low-poly sphere (radius 0.5) whose faces point inwards: the inside of the dream.</summary>
        private static Mesh InsideSphereMesh()
        {
            var mb = new MeshBuilder();
            const int lon = 16, lat = 10;
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    Vector3 a = SpherePoint(i, j, lon, lat), b = SpherePoint(i + 1, j, lon, lat);
                    Vector3 c = SpherePoint(i + 1, j + 1, lon, lat), d = SpherePoint(i, j + 1, lon, lat);
                    Vector3 mid = (a + b + c + d) * 0.25f;
                    mb.Quad(a, b, c, d, -mid);
                }
            return SaveBuiltMesh("PSX_DreamInsideSphere", mb);
        }

        private static Vector3 SpherePoint(int i, int j, int lon, int lat)
        {
            float u = i / (float)lon * Mathf.PI * 2f, v = j / (float)lat * Mathf.PI;
            return new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u)) * 0.5f;
        }

        /// <summary>Small star quads scattered on a shell 26 m out, each facing the middle.</summary>
        private static Mesh StarShellMesh()
        {
            var mb = new MeshBuilder();
            var rng = new System.Random(11);
            for (int i = 0; i < 220; i++)
            {
                Vector3 dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 1.6f - 0.5f, (float)rng.NextDouble() * 2f - 1f);
                if (dir.sqrMagnitude < 0.05f) continue;
                dir.Normalize();
                Vector3 c = dir * 26f;
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(dir, right);
                float s = 0.05f + (float)rng.NextDouble() * 0.09f;
                mb.Quad(c - right * s - up * s, c + right * s - up * s, c + right * s + up * s, c - right * s + up * s, -dir);
            }
            return SaveBuiltMesh("PSX_DreamStars", mb);
        }

        /// <summary>Saves (or replaces) a built mesh as an asset under Meshes/.</summary>
        private static Mesh SaveBuiltMesh(string name, MeshBuilder mb)
        {
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + name + ".asset";
            Mesh fresh = mb.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            existing.Clear();
            existing.SetVertices(new List<Vector3>(fresh.vertices));
            existing.SetTriangles(fresh.triangles, 0);
            existing.RecalculateNormals();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(fresh);
            return existing;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 offset = default(Vector3))
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material UnlitMat(string name, Color c)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var shader = Shader.Find("Unlit/Color");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = c;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EyelidMat()
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/PSX_Eyelids.mat";
            var shader = Shader.Find("WizardsVRC/PSXEyelids");
            if (shader == null) Debug.LogError("[WizardsVRC] Shader WizardsVRC/PSXEyelids is missing; the intro's eyelids won't show.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader ?? Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (shader != null) mat.shader = shader;
            mat.SetFloat("_Open", 0f);
            mat.SetColor("_Tint", new Color(0.25f, 0.2f, 0.45f, 0f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>UI material that draws on top of everything (the eyelids included): the intro's subtitles.</summary>
        private static Material AboveEyelidsUiMat()
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/UI_AboveEyelids.mat";
            var shader = Shader.Find("UI/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 3200;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
