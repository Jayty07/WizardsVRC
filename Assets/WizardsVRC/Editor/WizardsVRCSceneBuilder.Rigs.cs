using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Animated monster bodies. Every monster is built from primitives as a jointed rig of one of five kinds (biped,
    /// four-legged beast, floater, plant, or a special body such as the Kraken or the Juggernaut moved as a whole), with
    /// details from its type and name (hats, helmets, horns, crowns, bones, tusks, pincers, wings, a glowing core...).
    /// Each kind has keyframed Idle, Walk, Hit, Die and Cast clips (Animator controllers in Assets/WizardsVRC/Generated/
    /// Rigs), and a voice (Audio/Creatures, by Tools/make_creature_sounds.py) for casting, getting hit and dying.
    /// Each monster's rig is saved as a prefab once per build and placed as prefab instances, so a street full of them
    /// stays light. Battle bodies stand idle and act out the round (MonsterRig); patrol bodies walk.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string RigDir = "Assets/WizardsVRC/Generated/Rigs";
        private const string CreatureSoundDir = "Assets/WizardsVRC/Audio/Creatures";
        private enum RigKind { Biped, Quadruped, Floater, Plant, Simple }

        private class RigLook
        {
            public RigKind kind;
            public string voice = "Biped";
            public bool hat, helmet, crown, horns, ears, bones, core, heavy, wings, tail, tusks, pincers, stinger, beak, robe, small;
            public Color accent = Color.white;
        }

        private static readonly Dictionary<string, GameObject> _rigPrefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, RuntimeAnimatorController> _rigControllers = new Dictionary<string, RuntimeAnimatorController>();

        private static void ResetRigs()
        {
            _rigPrefabs.Clear();
            _rigControllers.Clear();
            _tentacleControllers.Clear();
            if (AssetDatabase.IsValidFolder(RigDir)) AssetDatabase.DeleteAsset(RigDir);
        }

        // ================================================================== looks

        private static bool Has(string s, params string[] words)
        {
            foreach (var w in words) if (s.Contains(w)) return true;
            return false;
        }

        /// <summary>What kind of body a monster gets, and its details, from its creature type and name.</summary>
        private static RigLook LookFor(MonsterEntry e, Color schoolCol)
        {
            string n = (e != null ? e.name : "").ToLowerInvariant();
            string t = (e != null ? e.creatureType : "").ToLowerInvariant();
            var L = new RigLook { accent = schoolCol };
            if (t == "kraken" || t == "tentacle") { L.kind = RigKind.Simple; L.voice = "Deep"; return L; }
            if (t == "locomotive" || t == "ghosttrain") { L.kind = RigKind.Simple; L.voice = "Machine"; return L; }
            if (t == "tumbleweed" || t == "cactus") { L.kind = RigKind.Simple; L.voice = "Plant"; return L; }
            if (t == "plant" || Has(n, "sprout", "briar", "thistle")) { L.kind = RigKind.Plant; L.voice = "Plant"; return L; }
            if (Has(n, "hawk", "moth", "wisp", "sprite", "devil", "ghost", "phantom") || t == "insect")
            {
                L.kind = RigKind.Floater;
                L.voice = Has(n, "hawk") ? "Beast" : "Spirit";
                L.wings = Has(n, "hawk", "moth", "sprite");
                L.beak = Has(n, "hawk");
                L.tail = Has(n, "wisp", "devil", "ghost", "phantom");
                L.hat = Has(n, "outlaw", "phantom");
                return L;
            }
            if (t == "beast" || Has(n, "rat", "newt", "crab", "boar", "coyote", "scorpion"))
            {
                L.kind = RigKind.Quadruped;
                L.voice = "Beast";
                L.tail = !Has(n, "crab");
                L.ears = Has(n, "rat", "coyote", "boar");
                L.tusks = Has(n, "boar");
                L.pincers = Has(n, "crab", "scorpion");
                L.stinger = Has(n, "scorpion");
                return L;
            }
            L.kind = RigKind.Biped;
            L.heavy = t == "construct" || Has(n, "golem", "warden", "dummy");
            L.voice = L.heavy ? "Construct" : "Biped";
            L.small = Has(n, "imp", "goblin", "snatcher", "tommyknocker", "sprite");
            L.horns = Has(n, "imp", "snuffer", "gloomfang");
            L.ears = Has(n, "snatcher", "goblin", "tommyknocker");
            L.hat = t == "outlaw" || t == "rogue" || Has(n, "rustler", "prospector", "bandit", "penny", "smuggler", "grell", "foreman");
            L.helmet = t == "dominion" && !Has(n, "vesper", "calloway", "selwyn", "merchant");
            L.crown = t == "undead king" || Has(n, "king", "railmaster");
            L.bones = t == "undead" || Has(n, "thrall", "bone");
            L.core = t == "hollowed";
            L.robe = Has(n, "snuffer", "selwyn", "merchant", "vesper", "captain", "gloomfang");
            return L;
        }

        // ================================================================== bodies

        /// <summary>
        /// The monster's animated body (a prefab instance, built once per monster and kind of use). Null if it can't be
        /// built (MonsterBody then falls back to the old shapes).
        /// </summary>
        private static GameObject RiggedMonster(Transform parent, CardDatabase db, MonsterEntry entry, int monsterId, string name, float scale, bool walking)
        {
            if (entry == null) return null;
            int school = db.MonsterSchool(monsterId);
            Color col = school >= 0 && school < SchoolColors.Length ? SchoolColors[school] : new Color(0.7f, 0.6f, 0.9f);
            RigLook look = LookFor(entry, col);
            string key = "Monster_" + monsterId + (walking ? "_Walk" : "");
            GameObject prefab;
            if (!_rigPrefabs.TryGetValue(key, out prefab))
            {
                Material mat = MonsterMat(db, monsterId);
                if (mat == null) mat = PMat("Monster_" + entry.id, entry.bodyColor, 1f);
                var temp = new GameObject(key);
                Transform bob = Child(temp.transform, "Bob", Vector3.zero);
                if (look.kind == RigKind.Simple)
                {
                    var special = CreatureBody(bob, db, entry, "Shape", 1f);
                    if (special == null) { Object.DestroyImmediate(temp); return null; }
                }
                else BuildRigShapes(bob, look, mat, entry.bodyColor);
                foreach (var tr in temp.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
                foreach (var r in temp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var c in temp.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var anim = temp.AddComponent<Animator>();
                anim.runtimeAnimatorController = RigController(look.kind, walking, look.heavy);
                anim.applyRootMotion = false;
                // Patrolling bodies only loop idle/walk, so off screen they stop entirely; fight bodies keep their clock
                // (a hit or a fall carries on while you look away) and only skip moving their parts.
                anim.cullingMode = walking ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.CullUpdateTransforms;
                if (!walking)
                {
                    var voice = temp.AddComponent<AudioSource>();
                    voice.playOnAwake = false;
                    voice.spatialBlend = 1f;
                    voice.rolloffMode = AudioRolloffMode.Linear;
                    voice.minDistance = 3f;
                    voice.maxDistance = 30f;
                    voice.dopplerLevel = 0f;
                }
                Directory.CreateDirectory(RigDir);
                prefab = PrefabUtility.SaveAsPrefabAsset(temp, RigDir + "/" + key + ".prefab");
                Object.DestroyImmediate(temp);
                _rigPrefabs[key] = prefab;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * scale;
            if (!walking)
            {
                var rig = UdonSharpUndo.AddComponent<MonsterRig>(inst);
                rig.animator = inst.GetComponent<Animator>();
                rig.voice = inst.GetComponent<AudioSource>();
                rig.castClip = CreatureClip(look.voice + "_Cast");
                rig.hitClip = CreatureClip(look.voice + "_Hit");
                rig.dieClip = CreatureClip(look.voice + "_Die");
                // Big monsters sound deeper, small ones higher.
                rig.pitch = Mathf.Clamp(1.25f - (scale - 0.6f) * 0.35f, 0.7f, 1.35f);
                _monsterRigs.Add(rig);
            }
            return inst;
        }

        /// <summary>Every MonsterRig made this build (they're added to the behaviours synced to Udon).</summary>
        private static readonly List<MonsterRig> _monsterRigs = new List<MonsterRig>();

        private static AudioClip CreatureClip(string name)
        {
            string path = CreatureSoundDir + "/" + name + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return null;
            if (!importer.forceToMono || importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                var s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
                importer.defaultSampleSettings = s;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>A jointed part: an empty pivot (at a joint, rest rotation identity) that the clips turn.</summary>
        private static Transform Joint(Transform parent, string name, Vector3 at)
        {
            var j = Child(parent, name, at);
            j.localRotation = Quaternion.identity;
            return j;
        }

        private static void Part(Transform j, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            Shape(j, "Mesh", mesh, pos, scale, mat, false, rot);
        }

        private static void Part(Transform j, Mesh mesh, Vector3 pos, Vector3 scale, Material mat)
        {
            Shape(j, "Mesh", mesh, pos, scale, mat, false, Quaternion.identity);
        }

        /// <summary>The shapes of a rig, about 2 m tall at scale 1 (feet on the ground at the origin, facing +Z).</summary>
        private static void BuildRigShapes(Transform bob, RigLook L, Material body, Color bodyColor)
        {
            var dark = PMat("RigDark_" + ColorUtility.ToHtmlStringRGB(bodyColor), Color.Lerp(bodyColor, Color.black, 0.45f), 1f);
            var light = PMat("RigLight_" + ColorUtility.ToHtmlStringRGB(bodyColor), Color.Lerp(bodyColor, Color.white, 0.35f), 1f);
            var accent = PMat("RigAccent_" + ColorUtility.ToHtmlStringRGB(L.accent), L.accent, 1f, L.accent * 0.35f);
            var eye = PMat("RigEye", new Color(1f, 0.95f, 0.6f), 1f, new Color(1f, 0.85f, 0.4f));
            var bone = PMat("RigBone", new Color(0.92f, 0.9f, 0.82f), 1f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var iron = PMat("RigIron", new Color(0.42f, 0.44f, 0.5f), 1f);
            Mesh blob = BlobMesh(), cyl = CylMesh(), cone = ConeMesh(), gem = GemMesh();
            Mesh box = BoxMesh();
            switch (L.kind)
            {
                case RigKind.Biped:
                {
                    float legH = L.small ? 0.7f : 0.85f;
                    Material skin = L.bones ? bone : body;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var leg = Joint(bob, s < 0 ? "LegL" : "LegR", new Vector3(s * 0.2f, legH, 0));
                        Part(leg, L.heavy ? box : cyl, new Vector3(0, -legH / 2f, 0), L.heavy ? new Vector3(0.34f, legH, 0.34f) : new Vector3(0.25f, legH / 2f, 0.25f), L.robe ? dark : skin);
                        Part(leg, blob, new Vector3(0, -legH + 0.06f, 0.08f), new Vector3(0.28f, 0.16f, 0.38f), dark);
                    }
                    var hips = Joint(bob, "Hips", new Vector3(0, legH, 0));
                    if (L.heavy) Part(hips, box, new Vector3(0, 0.45f, 0), new Vector3(0.85f, 0.9f, 0.55f), body);
                    else Part(hips, blob, new Vector3(0, 0.42f, 0), new Vector3(0.7f, 0.95f, 0.46f), L.bones ? dark : body);
                    if (L.robe) Part(hips, cone, new Vector3(0, 0.05f, 0), new Vector3(0.95f, 1.1f, 0.8f), body);
                    if (L.bones)
                        for (int k = 0; k < 3; k++)
                            Part(hips, cyl, new Vector3(0, 0.3f + k * 0.17f, 0.2f), new Vector3(0.55f, 0.03f, 0.06f), bone, Quaternion.Euler(0, 0, 90f));
                    if (L.core) Part(hips, gem, new Vector3(0, 0.5f, 0.24f), new Vector3(0.22f, 0.3f, 0.12f), accent);
                    if (L.helmet || L.heavy) Part(hips, box, new Vector3(0, 0.62f, 0.05f), new Vector3(0.8f, 0.35f, 0.42f), iron);
                    var head = Joint(hips, "Head", new Vector3(0, 0.9f, 0));
                    if (L.heavy) Part(head, box, new Vector3(0, 0.22f, 0), new Vector3(0.45f, 0.42f, 0.42f), body);
                    else Part(head, blob, new Vector3(0, 0.22f, 0), Vector3.one * (L.small ? 0.56f : 0.48f), L.core ? iron : skin);
                    foreach (float s in new[] { -1f, 1f })
                        Part(head, blob, new Vector3(s * 0.1f, 0.25f, 0.21f), new Vector3(0.09f, 0.09f, 0.05f), L.core || L.bones ? accent : eye);
                    if (L.hat)
                    {
                        Part(head, cyl, new Vector3(0, 0.44f, 0), new Vector3(0.75f, 0.025f, 0.75f), dark);
                        Part(head, cyl, new Vector3(0, 0.56f, 0), new Vector3(0.38f, 0.12f, 0.38f), dark);
                    }
                    if (L.helmet)
                    {
                        Part(head, blob, new Vector3(0, 0.34f, 0), new Vector3(0.54f, 0.36f, 0.54f), iron);
                        Part(head, box, new Vector3(0, 0.55f, -0.02f), new Vector3(0.06f, 0.18f, 0.4f), accent);
                    }
                    if (L.crown)
                        for (int k = 0; k < 6; k++)
                            Part(head, cone, Quaternion.Euler(0, k * 60f, 0) * new Vector3(0, 0.5f, 0.18f), new Vector3(0.1f, 0.22f, 0.1f), gold);
                    if (L.horns)
                        foreach (float s in new[] { -1f, 1f })
                            Part(head, cone, new Vector3(s * 0.17f, 0.48f, 0), new Vector3(0.1f, 0.28f, 0.1f), dark, Quaternion.Euler(0, 0, -s * 25f));
                    if (L.ears)
                        foreach (float s in new[] { -1f, 1f })
                            Part(head, cone, new Vector3(s * 0.32f, 0.3f, 0), new Vector3(0.12f, 0.3f, 0.06f), body, Quaternion.Euler(0, 0, -s * 70f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var arm = Joint(hips, s < 0 ? "ArmL" : "ArmR", new Vector3(s * (L.heavy ? 0.52f : 0.42f), 0.78f, 0));
                        Part(arm, L.heavy ? box : cyl, new Vector3(0, -0.33f, 0), L.heavy ? new Vector3(0.3f, 0.7f, 0.3f) : new Vector3(0.2f, 0.33f, 0.2f), L.robe ? body : skin);
                        Part(arm, blob, new Vector3(0, -0.68f, 0), Vector3.one * (L.heavy ? 0.34f : 0.2f), L.bones ? bone : dark);
                    }
                    // A glow in the casting hand.
                    Part(bob.Find("Hips/ArmR"), gem, new Vector3(0, -0.85f, 0.05f), new Vector3(0.14f, 0.2f, 0.14f), accent);
                    break;
                }
                case RigKind.Quadruped:
                {
                    bool low = L.pincers || L.stinger;
                    float h = low ? 0.4f : 0.62f;
                    var hips = Joint(bob, "Hips", new Vector3(0, h, 0));
                    Part(hips, blob, Vector3.zero, low ? new Vector3(1.0f, 0.4f, 1.1f) : new Vector3(0.72f, 0.58f, 1.35f), body);
                    var head = Joint(hips, "Head", new Vector3(0, 0.12f, low ? 0.5f : 0.62f));
                    Part(head, blob, new Vector3(0, 0.05f, 0.22f), low ? new Vector3(0.5f, 0.3f, 0.4f) : new Vector3(0.46f, 0.42f, 0.5f), body);
                    if (!low) Part(head, blob, new Vector3(0, -0.04f, 0.48f), new Vector3(0.24f, 0.2f, 0.3f), light);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Part(head, blob, new Vector3(s * 0.13f, 0.14f, 0.42f), new Vector3(0.08f, 0.08f, 0.05f), eye);
                        if (L.ears) Part(head, cone, new Vector3(s * 0.14f, 0.33f, 0.16f), new Vector3(0.14f, 0.26f, 0.08f), body, Quaternion.Euler(-10f, 0, -s * 15f));
                        if (L.tusks) Part(head, cone, new Vector3(s * 0.12f, -0.06f, 0.62f), new Vector3(0.06f, 0.22f, 0.06f), bone, Quaternion.Euler(-60f, 0, 0));
                        if (L.pincers)
                        {
                            var claw = Joint(hips, s < 0 ? "ClawL" : "ClawR", new Vector3(s * 0.4f, 0.05f, 0.55f));
                            Part(claw, cyl, new Vector3(0, 0, 0.25f), new Vector3(0.12f, 0.25f, 0.12f), dark, Quaternion.Euler(90f, 0, 0));
                            Part(claw, blob, new Vector3(0, 0, 0.55f), new Vector3(0.3f, 0.18f, 0.36f), body);
                        }
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        float sx = k % 2 == 0 ? -1f : 1f, sz = k < 2 ? 1f : -1f;
                        var leg = Joint(bob, (sz > 0 ? "LegF" : "LegB") + (sx < 0 ? "L" : "R"), new Vector3(sx * (low ? 0.45f : 0.26f), h, sz * (low ? 0.3f : 0.45f)));
                        Part(leg, cyl, new Vector3(sx * (low ? 0.15f : 0f), -h / 2f, 0), new Vector3(0.15f, h / 2f, 0.15f), dark, Quaternion.Euler(0, 0, low ? sx * 35f : 0f));
                    }
                    if (L.tail)
                    {
                        var tail = Joint(hips, "Tail", new Vector3(0, 0.1f, low ? -0.5f : -0.66f));
                        if (L.stinger)
                        {
                            for (int k = 0; k < 4; k++)
                                Part(tail, blob, new Vector3(0, 0.15f + k * 0.22f, -0.12f - k * 0.06f + (k > 2 ? 0.2f : 0f)), Vector3.one * (0.24f - k * 0.03f), body);
                            Part(tail, cone, new Vector3(0, 0.95f, 0.15f), new Vector3(0.12f, 0.28f, 0.12f), accent, Quaternion.Euler(120f, 0, 0));
                        }
                        else Part(tail, cyl, new Vector3(0, 0.05f, -0.32f), new Vector3(0.12f, 0.34f, 0.12f), L.ears ? light : body, Quaternion.Euler(-60f, 0, 0));
                    }
                    break;
                }
                case RigKind.Floater:
                {
                    float y = 1.15f;
                    var core = Joint(bob, "Hips", new Vector3(0, y, 0));
                    if (L.beak)
                    {
                        Part(core, blob, Vector3.zero, new Vector3(0.5f, 0.45f, 0.95f), body);
                        Part(core, blob, new Vector3(0, 0.18f, 0.48f), Vector3.one * 0.36f, light);
                        Part(core, cone, new Vector3(0, 0.15f, 0.72f), new Vector3(0.12f, 0.2f, 0.12f), gold, Quaternion.Euler(90f, 0, 0));
                        Part(core, cone, new Vector3(0, 0, -0.6f), new Vector3(0.45f, 0.4f, 0.1f), dark, Quaternion.Euler(-90f, 0, 0));
                    }
                    else
                    {
                        Part(core, blob, Vector3.zero, Vector3.one * 0.75f, body);
                        Part(core, blob, Vector3.zero, Vector3.one * 0.95f, PMat("RigGlow_" + ColorUtility.ToHtmlStringRGB(bodyColor), bodyColor * 0.6f, 1f, bodyColor * 0.5f));
                    }
                    var head = Joint(core, "Head", new Vector3(0, 0.1f, L.beak ? 0.45f : 0.3f));
                    foreach (float s in new[] { -1f, 1f })
                        Part(head, blob, new Vector3(s * 0.12f, 0.12f, 0.08f), new Vector3(0.1f, 0.12f, 0.05f), eye);
                    if (L.hat)
                    {
                        Part(head, cyl, new Vector3(0, 0.42f, -0.2f), new Vector3(0.7f, 0.025f, 0.7f), dark);
                        Part(head, cyl, new Vector3(0, 0.54f, -0.2f), new Vector3(0.36f, 0.12f, 0.36f), dark);
                    }
                    if (L.wings)
                        foreach (float s in new[] { -1f, 1f })
                        {
                            var wing = Joint(core, s < 0 ? "WingL" : "WingR", new Vector3(s * 0.25f, 0.1f, 0));
                            Part(wing, blob, new Vector3(s * 0.55f, 0, 0), new Vector3(1.1f, 0.06f, L.beak ? 0.5f : 0.75f), L.beak ? dark : light);
                        }
                    if (L.tail)
                    {
                        var tail = Joint(core, "Tail", new Vector3(0, -0.3f, -0.1f));
                        Part(tail, cone, new Vector3(0, -0.35f, -0.1f), new Vector3(0.45f, -0.8f, 0.45f), body);
                    }
                    break;
                }
                default:   // Plant
                {
                    var stem = Joint(bob, "Hips", Vector3.zero);
                    Part(stem, cyl, new Vector3(0, 0.45f, 0), new Vector3(0.3f, 0.45f, 0.3f), dark);
                    Part(stem, blob, new Vector3(0, 1.05f, 0), new Vector3(0.85f, 0.75f, 0.85f), body);
                    for (int k = 0; k < 5; k++)
                        Part(stem, cone, Quaternion.Euler(0, k * 72f, 0) * new Vector3(0, 1.35f, 0.3f), new Vector3(0.1f, 0.3f, 0.1f), light, Quaternion.Euler(0, k * 72f, 0) * Quaternion.Euler(30f, 0, 0));
                    var head = Joint(stem, "Head", new Vector3(0, 1.05f, 0.35f));
                    foreach (float s in new[] { -1f, 1f })
                        Part(head, blob, new Vector3(s * 0.15f, 0.08f, 0.05f), new Vector3(0.11f, 0.13f, 0.05f), eye);
                    Part(head, blob, new Vector3(0, -0.12f, 0.06f), new Vector3(0.22f, 0.07f, 0.04f), PMat("KrakenPupil", new Color(0.05f, 0.03f, 0.05f), 1f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var leaf = Joint(stem, s < 0 ? "ArmL" : "ArmR", new Vector3(s * 0.3f, 0.8f, 0));
                        Part(leaf, blob, new Vector3(s * 0.4f, 0.1f, 0), new Vector3(0.75f, 0.12f, 0.38f), light, Quaternion.Euler(0, 0, s * 20f));
                    }
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var root = Joint(bob, s < 0 ? "LegL" : "LegR", new Vector3(s * 0.15f, 0.12f, 0));
                        Part(root, cone, new Vector3(s * 0.12f, -0.06f, 0.1f), new Vector3(0.18f, 0.25f, 0.18f), dark, Quaternion.Euler(0, 0, s * 60f));
                    }
                    break;
                }
            }
        }

        /// <summary>A unit cube mesh (the PSX primitives' size: -0.5..0.5).</summary>
        private static Mesh BoxMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return m;
        }

        // ================================================================== clips and controllers

        /// <summary>Keys on one property: pairs of (time, value). Rotations are local Euler degrees, positions metres.</summary>
        private static void Keys(AnimationClip clip, string path, string prop, params float[] tv)
        {
            // Times must rise: a key at (or before) the previous key's time replaces that key's value (AnimationCurve would
            // otherwise drop it and the tangent loop below would run past the end).
            var list = new System.Collections.Generic.List<Keyframe>();
            for (int i = 0; i + 1 < tv.Length; i += 2)
            {
                if (list.Count > 0 && tv[i] <= list[list.Count - 1].time + 0.0001f)
                {
                    var last = list[list.Count - 1];
                    list[list.Count - 1] = new Keyframe(last.time, tv[i + 1]);
                    continue;
                }
                list.Add(new Keyframe(tv[i], tv[i + 1]));
            }
            var curve = new AnimationCurve(list.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            string p = prop.StartsWith("rot") ? "localEulerAnglesRaw." + prop.Substring(3) : prop.StartsWith("pos") ? "m_LocalPosition." + prop.Substring(3) : "m_LocalScale." + prop.Substring(5);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), p), curve);
        }

        /// <summary>A whole-number cycle of a sine on one property (for flaps and sways): 'cycles' over 'len' seconds.</summary>
        private static void Wave(AnimationClip clip, string path, string prop, float len, int cycles, float centre, float amp, float phase = 0f)
        {
            int steps = Mathf.Max(8, cycles * 8);
            var tv = new float[(steps + 1) * 2];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps * len;
                tv[i * 2] = t;
                tv[i * 2 + 1] = centre + amp * Mathf.Sin((t / len * cycles + phase) * Mathf.PI * 2f);
            }
            Keys(clip, path, prop, tv);
        }

        private static AnimationClip NewClip(string name, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        /// <summary>The five clips for a kind of rig.</summary>
        private static AnimationClip[] RigClips(RigKind kind)
        {
            var idle = NewClip("Idle", true);
            var walk = NewClip("Walk", true);
            var hit = NewClip("Hit", false);
            var cast = NewClip("Cast", false);
            var die = NewClip("Die", false);
            switch (kind)
            {
                case RigKind.Biped:
                    Wave(idle, "Bob", "posy", 2f, 1, 0.015f, 0.015f);
                    Wave(idle, "Bob/Hips/ArmL", "rotz", 2f, 1, -8f, 3f);
                    Wave(idle, "Bob/Hips/ArmR", "rotz", 2f, 1, 8f, 3f, 0.5f);
                    Keys(idle, "Bob/Hips/Head", "roty", 0, -6, 1, 6, 2, -6);
                    Wave(walk, "Bob/LegL", "rotx", 0.8f, 1, 0, 30f);
                    Wave(walk, "Bob/LegR", "rotx", 0.8f, 1, 0, 30f, 0.5f);
                    Wave(walk, "Bob/Hips/ArmL", "rotx", 0.8f, 1, 0, 25f, 0.5f);
                    Wave(walk, "Bob/Hips/ArmR", "rotx", 0.8f, 1, 0, 25f);
                    Wave(walk, "Bob", "posy", 0.8f, 2, 0.03f, 0.03f, 0.25f);
                    Keys(hit, "Bob", "rotx", 0, 0, 0.08f, -18, 0.25f, 4, 0.45f, 0);
                    Keys(hit, "Bob", "posz", 0, 0, 0.1f, -0.25f, 0.45f, 0);
                    Keys(hit, "Bob/Hips/Head", "rotx", 0, 0, 0.1f, -22, 0.45f, 0);
                    Keys(cast, "Bob/Hips/ArmR", "rotx", 0, 0, 0.3f, -160, 0.55f, -160, 0.7f, -60, 1f, 0);
                    Keys(cast, "Bob/Hips/ArmL", "rotx", 0, 0, 0.3f, -110, 0.55f, -120, 0.7f, -40, 1f, 0);
                    Keys(cast, "Bob", "rotx", 0, 0, 0.3f, -8, 0.55f, -8, 0.7f, 12, 1f, 0);
                    Keys(cast, "Bob/Hips/Head", "rotx", 0, 0, 0.3f, -15, 0.7f, 10, 1f, 0);
                    Keys(die, "Bob", "rotx", 0, 0, 0.15f, -10, 0.6f, -95, 0.75f, -86, 0.9f, -90, 1.2f, -90);
                    Keys(die, "Bob", "posy", 0, 0, 0.15f, 0.1f, 0.6f, 0.05f, 1.2f, 0.05f);
                    Keys(die, "Bob/Hips/ArmL", "rotz", 0, 0, 0.6f, -60, 1.2f, -60);
                    Keys(die, "Bob/Hips/ArmR", "rotz", 0, 0, 0.6f, 60, 1.2f, 60);
                    break;
                case RigKind.Quadruped:
                    Wave(idle, "Bob", "posy", 2f, 1, 0.01f, 0.012f);
                    Keys(idle, "Bob/Hips/Head", "rotx", 0, 0, 1, 6, 2, 0);
                    Wave(idle, "Bob/Hips/Tail", "roty", 2f, 2, 0, 15f);
                    Wave(idle, "Bob/Hips/ClawL", "roty", 2f, 2, 0, 10f);
                    Wave(idle, "Bob/Hips/ClawR", "roty", 2f, 2, 0, 10f, 0.5f);
                    Wave(walk, "Bob/LegFL", "rotx", 0.6f, 1, 0, 28f);
                    Wave(walk, "Bob/LegBR", "rotx", 0.6f, 1, 0, 28f);
                    Wave(walk, "Bob/LegFR", "rotx", 0.6f, 1, 0, 28f, 0.5f);
                    Wave(walk, "Bob/LegBL", "rotx", 0.6f, 1, 0, 28f, 0.5f);
                    Wave(walk, "Bob", "posy", 0.6f, 2, 0.03f, 0.03f);
                    Wave(walk, "Bob/Hips/Tail", "roty", 0.6f, 1, 0, 20f);
                    Keys(hit, "Bob", "rotz", 0, 0, 0.08f, 15, 0.25f, -5, 0.45f, 0);
                    Keys(hit, "Bob", "posz", 0, 0, 0.1f, -0.3f, 0.45f, 0);
                    Keys(cast, "Bob", "rotx", 0, 0, 0.35f, -25, 0.55f, -25, 0.7f, 10, 1f, 0);
                    Keys(cast, "Bob/Hips/Head", "rotx", 0, 0, 0.35f, -20, 0.55f, -20, 0.7f, 15, 1f, 0);
                    Keys(cast, "Bob", "posz", 0, 0, 0.35f, -0.2f, 0.55f, -0.2f, 0.7f, 0.8f, 1f, 0);
                    Keys(cast, "Bob/Hips/ClawL", "roty", 0, 0, 0.5f, -35, 0.7f, 20, 1f, 0);
                    Keys(cast, "Bob/Hips/ClawR", "roty", 0, 0, 0.5f, 35, 0.7f, -20, 1f, 0);
                    Keys(die, "Bob", "rotz", 0, 0, 0.15f, -10, 0.6f, 90, 0.75f, 82, 0.9f, 88, 1.2f, 88);
                    Keys(die, "Bob/Hips/Head", "rotx", 0, 0, 0.6f, 25, 1.2f, 25);
                    break;
                case RigKind.Floater:
                    Wave(idle, "Bob", "posy", 2f, 1, 0.1f, 0.12f);
                    Wave(idle, "Bob/Hips/WingL", "rotz", 2f, 8, 0, 35f);
                    Wave(idle, "Bob/Hips/WingR", "rotz", 2f, 8, 0, -35f);
                    Wave(idle, "Bob/Hips/Tail", "rotz", 2f, 2, 0, 18f);
                    Wave(walk, "Bob", "posy", 1f, 1, 0.12f, 0.1f);
                    Keys(walk, "Bob", "rotx", 0, 10, 1, 10);
                    Wave(walk, "Bob/Hips/WingL", "rotz", 1f, 6, 0, 40f);
                    Wave(walk, "Bob/Hips/WingR", "rotz", 1f, 6, 0, -40f);
                    Wave(walk, "Bob/Hips/Tail", "rotz", 1f, 2, 0, 22f);
                    Keys(hit, "Bob", "posz", 0, 0, 0.1f, -0.4f, 0.45f, 0);
                    Keys(hit, "Bob", "rotz", 0, 0, 0.1f, 20, 0.25f, -12, 0.45f, 0);
                    Keys(cast, "Bob", "scalex", 0, 1, 0.45f, 1.25f, 0.7f, 0.9f, 1f, 1);
                    Keys(cast, "Bob", "scaley", 0, 1, 0.45f, 1.25f, 0.7f, 0.9f, 1f, 1);
                    Keys(cast, "Bob", "scalez", 0, 1, 0.45f, 1.25f, 0.7f, 0.9f, 1f, 1);
                    Keys(cast, "Bob", "roty", 0, 0, 0.6f, 360, 1f, 360);
                    Keys(cast, "Bob", "posz", 0, 0, 0.6f, 0, 0.75f, 0.7f, 1f, 0);
                    Keys(die, "Bob", "posy", 0, 0.1f, 1.2f, -0.9f);
                    Keys(die, "Bob", "roty", 0, 0, 1.2f, 540);
                    Keys(die, "Bob", "scalex", 0, 1, 1.2f, 0.15f);
                    Keys(die, "Bob", "scaley", 0, 1, 1.2f, 0.15f);
                    Keys(die, "Bob", "scalez", 0, 1, 1.2f, 0.15f);
                    break;
                case RigKind.Plant:
                    Keys(idle, "Bob", "scaley", 0, 1, 1, 1.05f, 2, 1);
                    Wave(idle, "Bob/Hips/ArmL", "rotz", 2f, 1, 0, 10f);
                    Wave(idle, "Bob/Hips/ArmR", "rotz", 2f, 1, 0, -10f);
                    Keys(walk, "Bob", "posy", 0, 0, 0.3f, 0.3f, 0.6f, 0);
                    Keys(walk, "Bob", "scaley", 0, 0.88f, 0.3f, 1.1f, 0.6f, 0.88f);
                    Wave(walk, "Bob/Hips/ArmL", "rotz", 0.6f, 1, 0, 20f);
                    Wave(walk, "Bob/Hips/ArmR", "rotz", 0.6f, 1, 0, -20f);
                    Keys(hit, "Bob", "scaley", 0, 1, 0.1f, 0.8f, 0.3f, 1.05f, 0.45f, 1);
                    Keys(hit, "Bob", "rotx", 0, 0, 0.1f, -15, 0.45f, 0);
                    Keys(cast, "Bob", "scaley", 0, 1, 0.4f, 1.25f, 0.6f, 1.25f, 0.75f, 0.9f, 1f, 1);
                    Keys(cast, "Bob/Hips/ArmL", "rotz", 0, 0, 0.4f, 70, 0.75f, -10, 1f, 0);
                    Keys(cast, "Bob/Hips/ArmR", "rotz", 0, 0, 0.4f, -70, 0.75f, 10, 1f, 0);
                    Keys(die, "Bob", "rotx", 0, 0, 0.8f, 80, 1.2f, 80);
                    Keys(die, "Bob", "scaley", 0, 1, 1.2f, 0.5f);
                    break;
                default:   // Simple: the whole special body moves
                    Wave(idle, "Bob", "posy", 2f, 1, 0.04f, 0.04f);
                    Wave(walk, "Bob", "posy", 1f, 2, 0.06f, 0.06f);
                    Wave(walk, "Bob", "rotz", 1f, 1, 0, 4f);
                    Keys(hit, "Bob", "posz", 0, 0, 0.1f, -0.4f, 0.45f, 0);
                    Keys(hit, "Bob", "rotx", 0, 0, 0.1f, -8, 0.45f, 0);
                    Keys(cast, "Bob", "scalex", 0, 1, 0.4f, 1.15f, 0.7f, 1, 1f, 1);
                    Keys(cast, "Bob", "scaley", 0, 1, 0.4f, 1.15f, 0.7f, 1, 1f, 1);
                    Keys(cast, "Bob", "scalez", 0, 1, 0.4f, 1.15f, 0.7f, 1, 1f, 1);
                    Keys(cast, "Bob", "posz", 0, 0, 0.4f, -0.2f, 0.65f, 0.7f, 1f, 0);
                    Keys(die, "Bob", "posy", 0, 0, 1.2f, -1.5f);
                    Keys(die, "Bob", "rotz", 0, 0, 1.2f, 25);
                    Keys(die, "Bob", "scalex", 0, 1, 1.2f, 0.3f);
                    Keys(die, "Bob", "scaley", 0, 1, 1.2f, 0.3f);
                    Keys(die, "Bob", "scalez", 0, 1, 1.2f, 0.3f);
                    break;
            }
            var all = new[] { idle, walk, hit, cast, die };
            // Hit, Cast and Die start from the rest pose, so their first keys are the rest values.
            CompleteClips(all, new[] { 2, 3, 4, 0, 1 });
            return all;
        }

        /// <summary>
        /// Makes every clip set every property any of them animates (the ones it doesn't move held at rest). An Animator
        /// switched off and on again (a body hidden between fights) takes whatever pose it was left in as its resting pose,
        /// so without this a monster that died stayed knocked over: idle never touched the parts the fall had turned.
        /// 'order' says which clips' first keys count as the rest values, most trusted first.
        /// </summary>
        private static void CompleteClips(AnimationClip[] clips, int[] order)
        {
            var rest = new Dictionary<string, float>();
            var bindings = new Dictionary<string, EditorCurveBinding>();
            foreach (int i in order)
                foreach (var b in AnimationUtility.GetCurveBindings(clips[i]))
                {
                    string key = b.path + "|" + b.propertyName;
                    if (rest.ContainsKey(key)) continue;
                    AnimationCurve c = AnimationUtility.GetEditorCurve(clips[i], b);
                    if (c == null || c.length == 0) continue;
                    rest[key] = c.keys[0].value;
                    bindings[key] = b;
                }
            foreach (var clip in clips)
            {
                var has = new HashSet<string>();
                foreach (var b in AnimationUtility.GetCurveBindings(clip)) has.Add(b.path + "|" + b.propertyName);
                float len = Mathf.Max(clip.length, 0.1f);
                foreach (var kv in rest)
                    if (!has.Contains(kv.Key)) AnimationUtility.SetEditorCurve(clip, bindings[kv.Key], AnimationCurve.Constant(0f, len, kv.Value));
            }
        }

        /// <summary>
        /// The Animator controller for a summoned spell creature: it starts in Idle and BattleClient cues each beat of the
        /// attack with a trigger from any state: Walk (the run in), Cast (rearing up, the blow), Hit (thrown back off the
        /// target), Die, and Idle to settle. Cast and Hit go back to Idle on their own.
        /// </summary>
        private static RuntimeAnimatorController SpellRigController(RigKind kind, bool heavy)
        {
            string key = "Spell_" + kind + (heavy ? "_Heavy" : "");
            RuntimeAnimatorController rc;
            if (_rigControllers.TryGetValue(key, out rc)) return rc;
            Directory.CreateDirectory(RigDir);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(RigDir + "/Rig_" + key + ".controller");
            var clips = RigClips(kind);
            foreach (var c in clips) AssetDatabase.AddObjectToAsset(c, ctrl);
            var sm = ctrl.layers[0].stateMachine;
            float speed = heavy ? 0.8f : 1f;
            string[] names = { "Idle", "Walk", "Hit", "Cast", "Die" };
            var states = new AnimatorState[5];
            for (int i = 0; i < 5; i++)
            {
                states[i] = sm.AddState(names[i]);
                states[i].motion = clips[i];
                states[i].speed = i == 1 ? speed * 1.6f : speed;   // a run, not a stroll
                ctrl.AddParameter(names[i], AnimatorControllerParameterType.Trigger);
                var t = sm.AddAnyStateTransition(states[i]);
                t.AddCondition(AnimatorConditionMode.If, 0, names[i]);
                t.duration = 0.06f;
                t.hasExitTime = false;
                t.canTransitionToSelf = i == 2 || i == 3;
            }
            sm.defaultState = states[0];
            foreach (int i in new[] { 2, 3 })
            {
                var back = states[i].AddTransition(states[0]);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.1f;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            _rigControllers[key] = ctrl;
            return ctrl;
        }

        /// <summary>
        /// The Animator controller for a kind of rig: battle bodies start in Idle and take the Cast, Hit and Die triggers
        /// (Hit and Cast go back to Idle; Die holds); patrol bodies only walk. Heavy constructs move a little slower.
        /// </summary>
        private static RuntimeAnimatorController RigController(RigKind kind, bool walking, bool heavy)
        {
            string key = kind + (walking ? "_Walk" : "") + (heavy ? "_Heavy" : "");
            RuntimeAnimatorController rc;
            if (_rigControllers.TryGetValue(key, out rc)) return rc;
            Directory.CreateDirectory(RigDir);
            string path = RigDir + "/Rig_" + key + ".controller";
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips = RigClips(kind);
            foreach (var c in clips) AssetDatabase.AddObjectToAsset(c, ctrl);
            var sm = ctrl.layers[0].stateMachine;
            float speed = heavy ? 0.75f : 1f;
            if (walking)
            {
                var w = sm.AddState("Walk");
                w.motion = clips[1];
                w.speed = speed;
                sm.defaultState = w;
            }
            else
            {
                foreach (var p in new[] { "Cast", "Hit", "Die" }) ctrl.AddParameter(p, AnimatorControllerParameterType.Trigger);
                var idle = sm.AddState("Idle");
                idle.motion = clips[0];
                idle.speed = speed;
                var hit = sm.AddState("Hit");
                hit.motion = clips[2];
                var cast = sm.AddState("Cast");
                cast.motion = clips[3];
                cast.speed = speed;
                var die = sm.AddState("Die");
                die.motion = clips[4];
                sm.defaultState = idle;
                foreach (var from in new[] { idle, hit, cast })
                {
                    var td = from.AddTransition(die);
                    td.AddCondition(AnimatorConditionMode.If, 0, "Die");
                    td.duration = 0.05f;
                    td.hasExitTime = false;
                    var th = from.AddTransition(hit);
                    th.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                    th.duration = 0.05f;
                    th.hasExitTime = false;
                    if (from != cast)
                    {
                        var tc = from.AddTransition(cast);
                        tc.AddCondition(AnimatorConditionMode.If, 0, "Cast");
                        tc.duration = 0.08f;
                        tc.hasExitTime = false;
                    }
                }
                foreach (var back in new[] { hit, cast })
                {
                    var tb = back.AddTransition(idle);
                    tb.hasExitTime = true;
                    tb.exitTime = 0.95f;
                    tb.duration = 0.1f;
                }
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            _rigControllers[key] = ctrl;
            return ctrl;
        }
    }
}
