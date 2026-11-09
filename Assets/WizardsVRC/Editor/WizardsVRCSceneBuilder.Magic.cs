using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Hub atmosphere: crystals, gems, wisps and curios that hover and turn as if held up by magic, ambient motes in
    /// each school's classroom, and the finishing pieces for the tavern's stage and hearth.
    ///
    /// Floating runs on Unity's own Animator, not Udon: each floater is wrapped in a pivot that keeps its place, and a
    /// small looping clip bobs and turns the piece inside it. That costs nothing in Udon, and an Animator restarts its loop
    /// every time its area is switched back on (the legacy Animation component these used to have could stay stopped).
    /// Clips are shared: one per kind of floater and one of three phases, so neighbours don't move in step.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string FloatAnimDir = "Assets/WizardsVRC/Animations/Float";
        // Whether the occlusion bake runs on every rebuild is an editor option now (WizardsVRCSceneBuilder.Timing.cs).

        /// <summary>How each kind of piece floats: bob height (m), loop length (s), bobs per loop, turns per loop.</summary>
        private struct FloatKind
        {
            public float bob, length;
            public int bobs, turns;
            public FloatKind(float bob, float length, int bobs, int turns) { this.bob = bob; this.length = length; this.bobs = bobs; this.turns = turns; }
        }

        private static readonly Dictionary<string, FloatKind> FloatKinds = new Dictionary<string, FloatKind>
        {
            // ---- the hub's own pieces (found by name)
            { "StarfallCrystal", new FloatKind(0.35f, 12f, 2, 1) },
            { "CrystalHalo", new FloatKind(0.22f, 16f, 2, -1) },
            { "FountainStar", new FloatKind(0.06f, 20f, 4, -1) },
            { "Crystal", new FloatKind(0.14f, 10f, 3, 1) },
            { "RodSpark", new FloatKind(0.18f, 4f, 4, 1) },
            { "Orb", new FloatKind(0.2f, 8f, 3, 1) },
            { "Wisp", new FloatKind(0.3f, 6f, 3, 1) },
            { "SignGem", new FloatKind(0.06f, 6f, 4, 1) },
            { "StatueStar", new FloatKind(0.1f, 8f, 3, 1) },
            { "GableFinial", new FloatKind(0.08f, 10f, 4, 1) },
            { "HallStar", new FloatKind(0.15f, 12f, 3, 1) },
            { "DeskOrb", new FloatKind(0.05f, 8f, 4, 1) },
            { "FloatingBook", new FloatKind(0.25f, 14f, 3, 1) },
            { "Cloud", new FloatKind(0.15f, 30f, 3, 1) },
            // ---- classroom curios (ClassroomMagic)
            { "FlameGem", new FloatKind(0.25f, 6f, 3, 1) },
            { "IceShard", new FloatKind(0.2f, 10f, 3, -1) },
            { "SparkOrb", new FloatKind(0.3f, 4f, 3, 2) },
            { "Leaf", new FloatKind(0.3f, 8f, 2, 1) },
            { "Skull", new FloatKind(0.25f, 10f, 3, 1) },
            { "Tome", new FloatKind(0.22f, 12f, 3, -1) },
            { "SandPyramid", new FloatKind(0.2f, 12f, 3, 1) },
            { "BalanceRing", new FloatKind(0.15f, 10f, 2, 1) },
        };

        /// <summary>
        /// Wraps every piece under 'root' whose name is a FloatKind in a "Float_" pivot with a looping clip. Pieces under a
        /// behaviour, a rigidbody or another floater are left alone (they move by other means). Call before
        /// AddDistanceCulling: the pivots are not static, and the culling pass leaves anything under "Float_" alone.
        /// </summary>
        private static int AddFloaters(Transform root)
        {
            if (root == null) return 0;
            var found = new List<Transform>();
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr == root || !FloatKinds.ContainsKey(tr.name) || tr.GetComponent<Renderer>() == null && tr.childCount == 0) continue;
                bool skip = false;
                for (Transform p = tr.parent; p != null && p != root; p = p.parent)
                    if (p.name.StartsWith("Float_") || p.GetComponent<VRC.Udon.UdonBehaviour>() != null || p.GetComponent<Rigidbody>() != null ||
                        p.GetComponent<Animation>() != null || p.GetComponent<Animator>() != null || p.name.StartsWith("Encounter") || p.name.StartsWith("Patrol_"))
                    { skip = true; break; }
                if (!skip) found.Add(tr);
            }
            foreach (var tr in found) MakeFloater(tr);
            return found.Count;
        }

        private static void MakeFloater(Transform piece)
        {
            FloatKind k = FloatKinds[piece.name];
            Transform parent = piece.parent;
            var pivot = new GameObject("Float_" + piece.name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = piece.localPosition;
            pivot.localRotation = piece.localRotation;
            pivot.localScale = Vector3.one;
            pivot.SetSiblingIndex(piece.GetSiblingIndex());
            piece.SetParent(pivot, true);
            piece.localPosition = Vector3.zero;
            piece.localRotation = Quaternion.identity;
            piece.name = "Body";
            foreach (var t in pivot.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            // Floating things don't block anyone (the few that had a collider would push players about).
            foreach (var col in pivot.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (var r in pivot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 w = pivot.position;
            int phase = Mathf.Abs(Mathf.RoundToInt(w.x * 7.3f + w.y * 3.1f + w.z * 5.7f)) % 3;
            var clip = FloatClip(pivot.name.Substring(6), phase, k);
            LoopAnimator(pivot.gameObject, clip, AnimatorCullingMode.AlwaysAnimate);
        }

        /// <summary>
        /// Plays a clip on a loop with an Animator (one shared controller per clip, saved next to it). Animators start the
        /// loop again whenever their object is switched back on, so area culling never leaves one stopped.
        /// </summary>
        private static Animator LoopAnimator(GameObject go, AnimationClip clip, AnimatorCullingMode cull)
        {
            string ctrlPath = FloatAnimDir + "/" + clip.name + ".controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
            if (ctrl == null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            if (sm.states.Length == 0) sm.AddState("Loop");
            var state = sm.states[0].state;
            if (state.motion != clip) { state.motion = clip; EditorUtility.SetDirty(ctrl); }
            sm.defaultState = state;
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = cull;
            return anim;
        }

        /// <summary>Makes a clip a looping Animator clip (not legacy).</summary>
        private static void SetLooping(AnimationClip clip)
        {
            clip.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void SetTransformCurve(AnimationClip clip, string path, string prop, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), curve);
        }

        /// <summary>A looping clip that bobs "Body" up and down and turns it about its up axis (saved, shared by kind and phase).</summary>
        private static AnimationClip FloatClip(string kind, int phase, FloatKind k)
        {
            Directory.CreateDirectory(FloatAnimDir);
            string path = FloatAnimDir + "/Float_" + kind + "_" + phase + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool fresh = clip == null;
            if (fresh) clip = new AnimationClip();
            clip.ClearCurves();
            clip.name = "Float_" + kind + "_" + phase;
            SetLooping(clip);

            int steps = Mathf.Max(24, 24 * Mathf.Abs(k.turns));
            float p = phase * Mathf.PI * 2f / 3f, a0 = phase * 120f;
            var y = new Keyframe[steps + 1];
            var qx = new Keyframe[steps + 1];
            var qy = new Keyframe[steps + 1];
            var qz = new Keyframe[steps + 1];
            var qw = new Keyframe[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps, t = u * k.length;
                y[i] = new Keyframe(t, Mathf.Sin(u * k.bobs * Mathf.PI * 2f + p) * k.bob);
                // A slight rock as it bobs, so it reads as held in the air rather than on a turntable.
                float rock = Mathf.Sin(u * k.bobs * Mathf.PI * 2f + p + 1.3f) * 4f;
                Quaternion q = Quaternion.Euler(rock, a0 + 360f * k.turns * u, rock * 0.6f);
                // Keep neighbouring keys on the same side of the 4D sphere so the spin never takes the long way round.
                if (i > 0 && qx[i - 1].value * q.x + qy[i - 1].value * q.y + qz[i - 1].value * q.z + qw[i - 1].value * q.w < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                qx[i] = new Keyframe(t, q.x);
                qy[i] = new Keyframe(t, q.y);
                qz[i] = new Keyframe(t, q.z);
                qw[i] = new Keyframe(t, q.w);
            }
            SetTransformCurve(clip, "Body", "m_LocalPosition.x", AnimationCurve.Constant(0f, k.length, 0f));
            SetTransformCurve(clip, "Body", "m_LocalPosition.y", Smooth(y));
            SetTransformCurve(clip, "Body", "m_LocalPosition.z", AnimationCurve.Constant(0f, k.length, 0f));
            SetTransformCurve(clip, "Body", "m_LocalRotation.x", Smooth(qx));
            SetTransformCurve(clip, "Body", "m_LocalRotation.y", Smooth(qy));
            SetTransformCurve(clip, "Body", "m_LocalRotation.z", Smooth(qz));
            SetTransformCurve(clip, "Body", "m_LocalRotation.w", Smooth(qw));
            clip.EnsureQuaternionContinuity();
            if (fresh) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationCurve Smooth(Keyframe[] keys)
        {
            var c = new AnimationCurve(keys);
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        /// <summary>Six shards circling the Starfall Crystal (the floater pass turns the ring and bobs it).</summary>
        private static void CrystalHalo(Transform root, Vector3 at, float radius, float f, Material mat)
        {
            var halo = Child(root, "CrystalHalo", at);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, (i % 2 == 0 ? 0.4f : -0.4f) * f, Mathf.Sin(a) * radius);
                Shape(halo, "HaloShard", GemMesh(), p, new Vector3(0.45f, 0.9f, 0.45f) * f, mat, false, Quaternion.Euler(0, i * 60f, i % 2 == 0 ? 12f : -12f));
            }
        }

        // ================================================================== classrooms

        /// <summary>
        /// Each classroom's atmosphere: motes drifting through the air in the school's style (embers, snow, sparks, pollen,
        /// wisps, glitter, sand) and a few of its curios hovering over the desks (the floater pass animates them).
        /// </summary>
        private static void ClassroomMagic(Transform room, int s, float W, float D, float h, Color col)
        {
            var magic = Child(room, "ClassroomMagic", Vector3.zero);
            Vector3 box = new Vector3(W - 2f, h - 1f, D - 2f);
            Vector3 mid = new Vector3(0, h * 0.5f, 0);
            var glowCol = Color.Lerp(col, Color.white, 0.35f);
            var rng = new System.Random(4100 + s);
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // Spots for the curios: above the desks on both sides of the aisle, and over the front.
            var spots = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * Rr(W * 0.15f, W * 0.38f);
                float z = Mathf.Lerp(-D * 0.32f, D * 0.3f, i / 5f);
                spots.Add(new Vector3(x, Rr(3.4f, Mathf.Max(3.6f, h - 2.2f)), z));
            }
            switch (s)
            {
                case 0: // Fire: embers rising from the floor, flame gems hovering
                    Motes(magic, "Embers", new Vector3(0, 0.3f, 0), new Vector3(W - 2f, 0.2f, D - 2f), new Color(1f, 0.55f, 0.15f), 18f, 3.5f, 6f,
                        new Vector2(0.06f, 0.12f), new Vector3(0, 0.9f, 0), new Vector3(0.25f, 0.4f, 0.25f), -0.03f);
                    var fg = PMat("FlameGem", new Color(1f, 0.5f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
                    foreach (var p in spots) Shape(magic, "FlameGem", GemMesh(), p, new Vector3(0.4f, 0.7f, 0.4f), fg, false, Quaternion.identity);
                    break;
                case 1: // Ice: snow drifting down, ice shards
                    Motes(magic, "Snow", new Vector3(0, h - 0.6f, 0), new Vector3(W - 2f, 0.2f, D - 2f), new Color(0.92f, 0.97f, 1f), 22f, 7f, 9f,
                        new Vector2(0.05f, 0.1f), new Vector3(0, -0.55f, 0), new Vector3(0.25f, 0.1f, 0.25f), 0f);
                    var ice = PMat("IceShard", new Color(0.62f, 0.85f, 1f), 1f, new Color(0.35f, 0.55f, 0.75f));
                    foreach (var p in spots) Shape(magic, "IceShard", GemMesh(), p, new Vector3(0.3f, 0.9f, 0.3f), ice, false, Quaternion.Euler(0, 0, Rr(-15f, 15f)));
                    break;
                case 2: // Storm: crackling sparks, spark orbs
                    Motes(magic, "Sparks", mid, box, new Color(1f, 0.95f, 0.45f), 14f, 0.6f, 1.2f,
                        new Vector2(0.05f, 0.11f), Vector3.zero, new Vector3(1.6f, 1.6f, 1.6f), 0f);
                    Motes(magic, "StormMist", mid, box, new Color(0.62f, 0.55f, 0.95f), 6f, 4f, 6f,
                        new Vector2(0.18f, 0.3f), Vector3.zero, new Vector3(0.15f, 0.08f, 0.15f), 0f);
                    var spark = PMat("SparkOrb", new Color(0.8f, 0.85f, 1f), 1f, new Color(0.6f, 0.7f, 1f));
                    foreach (var p in spots) Shape(magic, "SparkOrb", GemMesh(), p, new Vector3(0.45f, 0.45f, 0.45f), spark, false, Quaternion.identity);
                    break;
                case 3: // Life: pollen motes, drifting leaves
                    Motes(magic, "Pollen", mid, box, new Color(1f, 0.95f, 0.5f), 12f, 6f, 9f,
                        new Vector2(0.05f, 0.09f), new Vector3(0, 0.08f, 0), new Vector3(0.2f, 0.12f, 0.2f), 0f);
                    Motes(magic, "Petals", new Vector3(0, h - 0.6f, 0), new Vector3(W - 2f, 0.2f, D - 2f), new Color(1f, 0.6f, 0.75f), 6f, 8f, 10f,
                        new Vector2(0.1f, 0.16f), new Vector3(0, -0.4f, 0), new Vector3(0.3f, 0.1f, 0.3f), 0f);
                    var leaf = PMat("FloatLeaf", new Color(0.4f, 0.85f, 0.35f), 1f, new Color(0.15f, 0.4f, 0.1f));
                    foreach (var p in spots) Shape(magic, "Leaf", BlobMesh(), p, new Vector3(0.7f, 0.08f, 0.4f), leaf, false, Quaternion.Euler(Rr(-20f, 20f), 0, Rr(-20f, 20f)));
                    break;
                case 4: // Death: green wisps rising slowly, floating skulls
                    Motes(magic, "Wisps", new Vector3(0, 0.4f, 0), new Vector3(W - 2f, 0.2f, D - 2f), new Color(0.5f, 1f, 0.7f), 8f, 5f, 8f,
                        new Vector2(0.12f, 0.22f), new Vector3(0, 0.45f, 0), new Vector3(0.2f, 0.1f, 0.2f), 0f);
                    var bone = PMat("FloatSkull", new Color(0.9f, 0.9f, 0.82f), 1f, new Color(0.2f, 0.35f, 0.25f));
                    var socket = PMat("SkullEye", new Color(0.4f, 1f, 0.6f), 1f, new Color(0.3f, 0.9f, 0.5f));
                    foreach (var p in spots)
                    {
                        // A little skull: a head, a jaw and two glowing eye sockets facing the door (local -Z).
                        var skull = Child(magic, "Skull", p);
                        Shape(skull, "Head", BlobMesh(), Vector3.zero, new Vector3(0.5f, 0.45f, 0.5f), bone, false, Quaternion.identity);
                        Shape(skull, "Jaw", BlobMesh(), new Vector3(0, -0.2f, -0.06f), new Vector3(0.32f, 0.18f, 0.32f), bone, false, Quaternion.identity);
                        foreach (float ex in new[] { -0.1f, 0.1f })
                            Shape(skull, "Eye", BlobMesh(), new Vector3(ex, 0.02f, -0.22f), new Vector3(0.1f, 0.1f, 0.06f), socket, false, Quaternion.identity);
                    }
                    break;
                case 5: // Myth: gold glitter, open tomes
                    Motes(magic, "Glitter", mid, box, new Color(1f, 0.85f, 0.35f), 14f, 3f, 5f,
                        new Vector2(0.04f, 0.08f), new Vector3(0, -0.1f, 0), new Vector3(0.15f, 0.15f, 0.15f), 0f);
                    var cover = PMat("TomeCover", new Color(0.55f, 0.2f, 0.65f), 1f);
                    var pages = PMat("TomePages", new Color(0.96f, 0.92f, 0.8f), 1f, new Color(0.25f, 0.2f, 0.1f));
                    foreach (var p in spots)
                    {
                        // An open book: two covers tilted up like a V, pages on top.
                        var tome = Child(magic, "Tome", p);
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            Quaternion tilt = Quaternion.Euler(0, 0, sx * -14f);
                            Shape(tome, "Cover", CylMesh(), tilt * new Vector3(sx * 0.2f, 0, 0), new Vector3(0.4f, 0.02f, 0.5f), cover, false, tilt);
                            Shape(tome, "Pages", CylMesh(), tilt * new Vector3(sx * 0.19f, 0.03f, 0), new Vector3(0.36f, 0.02f, 0.46f), pages, false, tilt);
                        }
                    }
                    break;
                default: // Balance: sand drifting sideways, sand pyramids and rings
                    Motes(magic, "Sand", mid, box, new Color(0.95f, 0.82f, 0.55f), 16f, 4f, 6f,
                        new Vector2(0.04f, 0.08f), new Vector3(0.35f, 0f, 0f), new Vector3(0.1f, 0.12f, 0.1f), 0f);
                    var sand = PMat("SandPyramid", new Color(0.9f, 0.78f, 0.52f), 1f, new Color(0.25f, 0.2f, 0.1f));
                    var ringMat = PMat("BalanceRing", col, 1f, col * 0.6f);
                    for (int i = 0; i < spots.Count; i++)
                    {
                        if (i % 2 == 0) Shape(magic, "SandPyramid", ConeMesh(), spots[i], new Vector3(0.6f, 0.55f, 0.6f), sand, false, Quaternion.Euler(180f, 45f, 0));
                        else
                        {
                            // A ring of four gems (one per element it balances) round a gold bead.
                            var ring = Child(magic, "BalanceRing", spots[i]);
                            for (int g = 0; g < 4; g++)
                            {
                                float a = g * 90f * Mathf.Deg2Rad;
                                Shape(ring, "Gem", GemMesh(), new Vector3(Mathf.Cos(a) * 0.4f, 0, Mathf.Sin(a) * 0.4f), new Vector3(0.18f, 0.28f, 0.18f), ringMat, false, Quaternion.identity);
                            }
                            Shape(ring, "Bead", GemMesh(), Vector3.zero, Vector3.one * 0.2f, PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f)), false, Quaternion.identity);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Ambient motes in a box (local to 'parent'): a looping particle system with a small budget that pauses when off
        /// screen. vel is the drift; jitter the random spread added to it per particle; gravity pulls (negative lifts).
        /// </summary>
        private static ParticleSystem Motes(Transform parent, string name, Vector3 at, Vector3 box, Color c, float rate, float life0, float life1,
            Vector2 size, Vector3 vel, Vector3 jitter, float gravity)
        {
            var go = Child(parent, name, at).gameObject;
            go.isStatic = false;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = c;
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.CeilToInt(rate * life1) + 4;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(vel.x - jitter.x, vel.x + jitter.x);
            v.y = new ParticleSystem.MinMaxCurve(vel.y - jitter.y, vel.y + jitter.y);
            v.z = new ParticleSystem.MinMaxCurve(vel.z - jitter.z, vel.z + jitter.z);
            // Fade in and out by size, so motes never pop.
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            sz.size = new ParticleSystem.MinMaxCurve(1f, curve);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Glow("Mote_" + ColorUtility.ToHtmlStringRGB(c), c, 1.2f);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // ================================================================== tavern: stage and hearth

        /// <summary>
        /// The stage's finished front: a gold-capped nosing and a painted fascia round its edge, wide steps up at both ends
        /// of the front, footlights along the lip, a proscenium of two carved pillars with a header and a scalloped
        /// valance over the stage, a backdrop behind the curtains, and velvet curtains.
        /// </summary>
        private static void TavernStageFinish(Transform t, Vector3 c, float R, float a0, float a1, Material planks, Material darkWood,
            Material gold, Material velvet, Material stone)
        {
            const float top = 0.45f, front = 12.6f, stepIn = 11.9f;
            var fascia = PsxTexMat("StageFascia", new Color(0.32f, 0.12f, 0.16f), TileTexture(StreetTexDir + "/Velvet.png"), 1.2f);
            var mb = new MeshBuilder();
            // Nosing: a thin overhanging lip round the stage's front edge; the fascia below it is a velvet skirt.
            SectorPrism(mb, c, front - 0.12f, front + 0.04f, a0 - 0.4f, a1 + 0.4f, top - 0.06f, top + 0.02f, 16);
            TavernMesh(t, "StageNosing", mb, gold, false);
            mb = new MeshBuilder();
            SectorPrism(mb, c, front - 0.04f, front - 0.02f, a0, a1, 0.22f, top - 0.06f, 16);
            TavernMesh(t, "StageFascia", mb, fascia, false);
            // The step's own edge.
            mb = new MeshBuilder();
            SectorPrism(mb, c, stepIn - 0.08f, stepIn + 0.04f, a0 + 2f, a1 - 2f, 0.18f, 0.24f, 16);
            TavernMesh(t, "StepNosing", mb, darkWood, false);

            // Footlights: little brass hoods with a warm glow along the lip, between the steps.
            var footGlow = PMat("Footlight", new Color(1f, 0.85f, 0.55f), 1f, new Color(1f, 0.75f, 0.4f));
            var brass = PMat("Brass", new Color(0.78f, 0.6f, 0.25f), 1f);
            for (int k = 0; k < 9; k++)
            {
                float a = Mathf.Lerp(a0 + 6f, a1 - 6f, k / 8f);
                Quaternion f = TFacing(a);
                Block(t, "FootlightHood", TPolar(c, front + 0.05f, a, top + 0.1f), new Vector3(0.36f, 0.16f, 0.18f), brass, f);
                Shape(t, "FootlightGlow", BlobMesh(), TPolar(c, front + 0.12f, a, top + 0.08f), new Vector3(0.24f, 0.1f, 0.1f), footGlow, false, f);
            }
            foreach (var col in t.GetComponentsInChildren<Collider>(true))
                if (col.name.StartsWith("Footlight")) Object.DestroyImmediate(col);

            // Proscenium: carved pillars at the stage's front corners, a header arc and a scalloped valance between them.
            foreach (float a in new[] { a0 + 1.4f, a1 - 1.4f })
            {
                Vector3 b = TPolar(c, front + 0.3f, a, 0f);
                Quaternion f = TFacing(a);
                Block(t, "ProsceniumBase", b + new Vector3(0, top + 0.3f, 0), new Vector3(1.1f, 0.6f, 1.1f), stone, f);
                Shape(t, "ProsceniumColumn", CylMesh(), b + new Vector3(0, top + 0.6f + 3.7f, 0), new Vector3(0.75f, 3.7f, 0.75f), darkWood, true, Quaternion.identity);
                for (int r = 0; r < 4; r++)   // gold bands up the column
                    Shape(t, "ProsceniumBand", CylMesh(), b + new Vector3(0, top + 1.5f + r * 2f, 0), new Vector3(0.82f, 0.06f, 0.82f), gold, false, Quaternion.identity);
                Block(t, "ProsceniumCapital", b + new Vector3(0, 8.15f, 0), new Vector3(1.1f, 0.3f, 1.1f), gold, f);
                Shape(t, "ProsceniumGem", GemMesh(), b + new Vector3(0, 8.5f, 0) - TDir(a) * 0.56f, new Vector3(0.35f, 0.5f, 0.35f),
                    PMat("StageLamp1", new Color(0.35f, 0.75f, 1f), 1f, new Color(0.35f, 0.75f, 1f) * 0.8f), false, Quaternion.identity);
            }
            mb = new MeshBuilder();
            SectorPrism(mb, c, front + 0.05f, front + 0.55f, a0 + 1.4f, a1 - 1.4f, 8.3f, 8.8f, 16);
            TavernMesh(t, "ProsceniumHeader", mb, darkWood, false);
            mb = new MeshBuilder();
            SectorPrism(mb, c, front - 0.02f, front + 0.06f, a0 + 1.4f, a1 - 1.4f, 8.26f, 8.32f, 16);
            TavernMesh(t, "ProsceniumHeaderTrim", mb, gold, false);
            // Valance: swags of velvet hanging under the header, a gold tassel between each.
            const int swags = 8;
            for (int k = 0; k < swags; k++)
            {
                float aa = Mathf.Lerp(a0 + 2.6f, a1 - 2.6f, (k + 0.5f) / swags);
                float chord = 2f * (front + 0.3f) * Mathf.Sin((a1 - a0 - 5.2f) / swags * 0.5f * Mathf.Deg2Rad);
                Shape(t, "Valance", BlobMesh(), TPolar(c, front + 0.3f, aa, 8.25f), new Vector3(chord * 1.08f, 0.55f, 0.22f), velvet, false, TFacing(aa));
                if (k > 0)
                {
                    float ta = Mathf.Lerp(a0 + 2.6f, a1 - 2.6f, k / (float)swags);
                    Shape(t, "Tassel", ConeMesh(), TPolar(c, front + 0.18f, ta, 7.85f), new Vector3(0.12f, 0.35f, 0.12f), gold, false, Quaternion.Euler(180f, 0, 0));
                }
            }

            // Backdrop: a dark cloth on the wall behind the curtains (it shows through their gaps), up to the curtain rod.
            mb = new MeshBuilder();
            SectorPrism(mb, c, R - 0.85f, R - 0.83f, a0 + 1f, a1 - 1f, top, 6.25f, 16);
            TavernMesh(t, "StageBackdrop", mb, PMat("StageBackdrop", new Color(0.08f, 0.07f, 0.18f), 1f), false);
        }

        /// <summary>
        /// A stylised fire in the hearth: three layered flame shapes (dark orange outside, yellow, a white-hot core) per
        /// tongue, a bed of glowing coals, and rising embers; FireFlicker sways the flames and flickers the light.
        /// </summary>
        private static FireFlicker HearthFire(Transform t, Vector3 at, Vector3 side, Quaternion facing, Light light)
        {
            var holder = Child(t, "HearthFire", at);
            holder.localRotation = facing;
            AmbientPoint(holder, new Vector3(0, 0.6f, 0), "Amb_Fire", 0.7f, 2f, 14f);   // its crackle, heard as you come near
            var outer = PMat("FlameOuter", new Color(0.95f, 0.32f, 0.06f), 1f, new Color(0.9f, 0.25f, 0.02f));
            var middle = PMat("FlameMiddle", new Color(1f, 0.7f, 0.15f), 1f, new Color(1f, 0.6f, 0.1f));
            var core = PMat("FlameCore", new Color(1f, 0.97f, 0.8f), 1f, new Color(1f, 0.92f, 0.65f));
            var coal = PMat("Coals", new Color(0.55f, 0.12f, 0.04f), 1f, new Color(0.6f, 0.12f, 0.02f));
            var flames = new List<Transform>();
            float[] xs = { -0.62f, 0f, 0.6f, -0.3f, 0.32f };
            float[] hs = { 0.85f, 1.35f, 0.9f, 1.05f, 1.0f };
            for (int i = 0; i < xs.Length; i++)
            {
                // Each tongue is its own pivot at the base, so it stretches upwards from the logs.
                var tongue = Child(holder, "Flame" + i, new Vector3(xs[i], 0.3f, i >= 3 ? 0.18f : -0.02f));
                Shape(tongue, "Outer", ConeMesh(), new Vector3(0, hs[i] * 0.5f, 0), new Vector3(0.62f, hs[i], 0.62f), outer, false, Quaternion.identity);
                Shape(tongue, "Middle", ConeMesh(), new Vector3(0, hs[i] * 0.4f, 0), new Vector3(0.42f, hs[i] * 0.78f, 0.42f), middle, false, Quaternion.Euler(0, 22f, 0));
                Shape(tongue, "Core", ConeMesh(), new Vector3(0, hs[i] * 0.28f, 0), new Vector3(0.22f, hs[i] * 0.5f, 0.22f), core, false, Quaternion.Euler(0, 45f, 0));
                flames.Add(tongue);
            }
            // The coal bed and the logs crossed on it.
            Shape(holder, "Coals", BlobMesh(), new Vector3(0, 0.12f, 0.05f), new Vector3(2f, 0.22f, 0.9f), coal, false, Quaternion.identity);
            var bark = PsxTexMat("HearthLog", new Color(0.4f, 0.26f, 0.16f), TileTexture(StreetTexDir + "/Bark.png"), 1f);
            Shape(holder, "Log", CylMesh(), new Vector3(0, 0.22f, -0.15f), new Vector3(0.3f, 0.95f, 0.3f), bark, false, Quaternion.Euler(0, 12f, 90f));
            Shape(holder, "Log", CylMesh(), new Vector3(0, 0.26f, 0.22f), new Vector3(0.28f, 0.9f, 0.28f), bark, false, Quaternion.Euler(0, -14f, 90f));

            // Embers rising out of the fire and up the chimney.
            var embers = Motes(holder, "Embers", new Vector3(0, 0.6f, 0.1f), new Vector3(1.6f, 0.2f, 0.6f), new Color(1f, 0.6f, 0.2f), 0f, 1.2f, 2.2f,
                new Vector2(0.04f, 0.08f), new Vector3(0, 1.3f, -0.15f), new Vector3(0.3f, 0.4f, 0.12f), -0.05f);
            var main = embers.main;
            main.prewarm = false;
            main.maxParticles = 40;

            foreach (var tr in holder.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fire = UdonSharpEditor.UdonSharpUndo.AddComponent<FireFlicker>(holder.gameObject);
            fire.flames = flames.ToArray();
            fire.fireLight = light;
            fire.embers = embers;
            fire.emberRate = 12f;
            fire.stretch = 0.28f;
            fire.speed = 5f;
            return fire;
        }

        // ================================================================== occlusion culling

        /// <summary>
        /// Readies the hub for Unity's occlusion culling: an Occlusion Area over the hub only (so the bake stays small and
        /// quick), and only big solid pieces as occluders (walls, roofs, rocks, terrain), since glass, leaves, cutouts and
        /// small props can't hide anything behind them and would only slow the bake. Everything static stays an occludee.
        /// </summary>
        private static void SetupHubOcclusion(Transform hubRoot, Bounds cull)
        {
            if (hubRoot == null) return;
            var areaGo = new GameObject("HubOcclusionArea");
            areaGo.transform.SetParent(hubRoot, false);
            areaGo.transform.position = cull.center;
            var area = areaGo.AddComponent<OcclusionArea>();
            area.center = Vector3.zero;
            area.size = cull.size;
            int occluders = 0, cleared = 0;
            foreach (var r in hubRoot.GetComponentsInChildren<Renderer>(true))
            {
                var go = r.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.OccluderStatic) == 0) continue;
                Bounds b = r.bounds;
                float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                bool solid = r is MeshRenderer && r.sharedMaterial != null && r.sharedMaterial.renderQueue < 2450 && big >= 2.5f &&
                             !go.name.Contains("Window") && !go.name.Contains("Water") && !go.name.Contains("Leaves") && !go.name.Contains("Hedge") &&
                             !go.name.Contains("Glass") && !go.name.Contains("Curtain") && !go.name.Contains("Mirror") && !go.name.Contains("Screen");
                if (solid) occluders++;
                else
                {
                    GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.OccluderStatic);
                    cleared++;
                }
            }
            Debug.Log("[WizardsVRC] Hub occlusion: " + occluders + " occluders, " + cleared + " small or see-through pieces set to occludee only.");
        }

        /// <summary>Bakes occlusion culling for the open scene with settings that suit the hub's big, blocky buildings.</summary>
        private static void BakeHubOcclusion()
        {
            StaticOcclusionCulling.smallestOccluder = 3f;
            StaticOcclusionCulling.smallestHole = 0.25f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            if (StaticOcclusionCulling.Compute()) Debug.Log("[WizardsVRC] Occlusion culling baked.");
            else Debug.LogWarning("[WizardsVRC] Occlusion bake failed or was cancelled; the hub still culls by distance and area.");
        }

        [MenuItem("WizardsVRC/Bake Hub Occlusion")]
        private static void BakeHubOcclusionMenu()
        {
            // Bakes the open scene and saves it (the bake data belongs to the saved scene).
            BakeHubOcclusion();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
