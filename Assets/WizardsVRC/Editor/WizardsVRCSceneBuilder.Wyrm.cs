using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Sunfire Wyrm's dragon: an eastern dragon in fire colours (long, wingless, antlered, whiskered, a mane and a crest
    /// of flame down its back, four short legs, a tuft of flame for a tail). Original art, made for this world.
    ///
    /// Its meshes and its skin come from Tools/make_sunfire_wyrm.py (WyrmMeshData.cs and Textures/Spell/WyrmSkin.png:
    /// one 64 x 64 sheet of pixel art, point-filtered). Here they become:
    ///   - the spell's rig (BuildSunfireWyrm): a head with a jaw that opens and 24 lengths of body, which SunfireWyrm
    ///     flies through the cast; the fire it sheds, its breath, and its voice;
    ///   - the dragon posed as on its card (BuildWyrmPose): what the card's picture is rendered from, and what a
    ///     minion's unstaged cast of the spell sends along its path.
    /// It is drawn after the dark that falls for a big spell's charge (WizardsVRC/DuskVeil), so it stays as bright as
    /// the rest of the magic.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>Sunfire Wyrm's card id.</summary>
        private const int SunfireWyrmCard = 111;
        private const string WyrmSkinTex = TexturesDir + "/Spell/WyrmSkin.png";

        /// <summary>One of the dragon's meshes as an asset: three vertices a triangle, wound to face outwards, UVs on its skin.</summary>
        private static Mesh WyrmMesh(string name, float[] v, float[] uv)
        {
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/PSX_Wyrm" + name + ".asset";
            int n = v.Length / 3;
            var verts = new List<Vector3>(n);
            var uvs = new List<Vector2>(n);
            var tris = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]));
                uvs.Add(new Vector2(uv[i * 2], uv[i * 2 + 1]));
                tris.Add(i);
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh { name = "PSX_Wyrm" + name };
            else mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// Its skin: the PSX shader with the sheet on the meshes' own UVs, a faint glow of its own, and drawn after the
        /// dark of a big spell's charge (the veil is at Transparent-10; this is solid, so it still hides and is hidden
        /// by the world as usual).
        /// </summary>
        private static Material WyrmSkinMat()
        {
            Texture2D skin = PixelTexture(WyrmSkinTex);
            if (skin == null) Debug.LogWarning("[WizardsVRC] Missing " + WyrmSkinTex + " (run Tools/make_sunfire_wyrm.py).");
            Material mat = PMatBase("WyrmSkin", Color.white, 1f, new Color(0.1f, 0.035f, 0.01f));
            if (skin != null) mat.mainTexture = skin;
            mat.mainTextureScale = Vector2.one;
            mat.SetFloat("_WorldUV", 0f);
            mat.renderQueue = 2995;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject WyrmPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.isStatic = false;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static Vector3 WyrmVec(float[] a, int at)
        {
            return new Vector3(a[at], a[at + 1], a[at + 2]);
        }

        private static Mesh[] _wyrmMeshes;

        /// <summary>The dragon's five meshes: head, jaw, a length of body, one with legs, and the tail (made once an editor session).</summary>
        private static Mesh[] WyrmMeshes()
        {
            if (_wyrmMeshes != null && _wyrmMeshes[0] != null && _wyrmMeshes[4] != null) return _wyrmMeshes;
            _wyrmMeshes = new[]
            {
                WyrmMesh("Head", WyrmMeshData.HeadV, WyrmMeshData.HeadUV),
                WyrmMesh("Jaw", WyrmMeshData.JawV, WyrmMeshData.JawUV),
                WyrmMesh("Segment", WyrmMeshData.SegmentV, WyrmMeshData.SegmentUV),
                WyrmMesh("SegmentLegs", WyrmMeshData.SegmentLegsV, WyrmMeshData.SegmentLegsUV),
                WyrmMesh("Tail", WyrmMeshData.TailV, WyrmMeshData.TailUV),
            };
            return _wyrmMeshes;
        }

        /// <summary>Which of WyrmMeshes body segment i wears.</summary>
        private static Mesh WyrmSegmentMesh(Mesh[] meshes, int i)
        {
            if (i == WyrmMeshData.Segments - 1) return meshes[4];
            return System.Array.IndexOf(WyrmMeshData.LegSegments, i) >= 0 ? meshes[3] : meshes[2];
        }

        /// <summary>A flame's colours over its life: white-hot, gold, orange, red, out; 'power' is how bright it is at its start.</summary>
        private static Gradient WyrmFireRamp(float power)
        {
            var g = new Gradient();
            g.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0.15f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.4f), new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 0.75f),
                    new GradientColorKey(new Color(0.4f, 0.05f, 0.05f), 1f),
                },
                new[] { new GradientAlphaKey(power, 0f), new GradientAlphaKey(0.8f * power, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>
        /// Makes one of the rig's emit-on-demand particle systems (CinePS) burn: its colours over its life, how its
        /// particles' size changes (from 'size0' to 'size1' of their own), how fast they rise whatever else they do, and
        /// how far they are drawn out along the way they move.
        /// </summary>
        private static void WyrmFire(ParticleSystem ps, Gradient ramp, float size0, float size1, float rise0, float rise1, float stretch)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = ramp;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            float most = Mathf.Max(size0, size1);
            sol.size = new ParticleSystem.MinMaxCurve(most, AnimationCurve.Linear(0f, size0 / most, 1f, size1 / most));
            if (rise1 > 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.y = new ParticleSystem.MinMaxCurve(rise0, rise1);
                vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = stretch;
            r.velocityScale = 0.03f;
        }

        /// <summary>
        /// The spell's rig, under SpellCinematic's: the dragon (switched off until it is out), its fire and its voice,
        /// wired to a SunfireWyrm.
        /// </summary>
        private static SunfireWyrm BuildSunfireWyrm(Transform rig)
        {
            var holder = Child(rig, "SunfireWyrm", Vector3.zero);
            var wyrm = UdonSharpUndo.AddComponent<SunfireWyrm>(holder.gameObject);
            Material skin = WyrmSkinMat();
            Mesh[] meshes = WyrmMeshes();

            // ---- the dragon: every part faces +Z; SunfireWyrm places them in the world each frame
            var dragon = Child(holder, "Dragon", Vector3.zero);
            Transform head = WyrmPart(dragon, "Head", meshes[0], skin).transform;
            Transform jaw = WyrmPart(head, "Jaw", meshes[1], skin).transform;
            jaw.localPosition = WyrmVec(WyrmMeshData.JawHinge, 0);
            int n = WyrmMeshData.Segments;
            var segments = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                Transform seg = WyrmPart(dragon, "Segment" + i.ToString("00"), WyrmSegmentMesh(meshes, i), skin).transform;
                seg.localScale = new Vector3(WyrmMeshData.SegmentAcross[i], WyrmMeshData.SegmentAcross[i], WyrmMeshData.SegmentAlong[i]);
                segments[i] = seg;
            }
            dragon.gameObject.SetActive(false);
            wyrm.dragon = dragon.gameObject;
            wyrm.head = head;
            wyrm.jaw = jaw;
            wyrm.segments = segments;
            wyrm.segmentAcross = (float[])WyrmMeshData.SegmentAcross.Clone();
            wyrm.segmentAlong = (float[])WyrmMeshData.SegmentAlong.Clone();
            wyrm.segmentRadius = (float[])WyrmMeshData.SegmentRadius.Clone();
            wyrm.mouthPoint = WyrmVec(WyrmMeshData.Mouth, 0);
            wyrm.manePoint = WyrmVec(WyrmMeshData.Mane, 0);

            // ---- its fire (each only emits when SunfireWyrm tells it to; world space, sized by its emitter's scale)
            // The flames it sheds all the time: teardrops that rise off its back and shrink away.
            Material flameMat = AuraMat("SpellWyrmFlame", Color.white, AuraFlameTex);
            var flames = CinePS(holder, "WyrmFlames", flameMat, null, 0.4f, 0.7f, 0.2f, 0.6f, 0.5f, 0.85f, 0f, ParticleSystemShapeType.Sphere, 0.12f, 1200);
            WyrmFire(flames, WyrmFireRamp(0.6f), 1f, 0.2f, 1.2f, 2.0f, 1.7f);
            wyrm.flames = flames;
            // Embers with them.
            var embers = CinePS(holder, "WyrmEmbers", AuraMat("SpellWyrmEmber", new Color(1f, 0.67f, 0.32f), AuraSparkTex), null,
                0.7f, 1.3f, 0.3f, 1.2f, 0.05f, 0.12f, -0.25f, ParticleSystemShapeType.Sphere, 0.15f, 500);
            var emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
            emberRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            emberRenderer.lengthScale = 2f;
            emberRenderer.velocityScale = 0.03f;
            wyrm.embers = embers;
            // Its breath: a cone of flame along the emitter's forward axis, swelling as it goes, and a brighter, faster
            // jet inside it. (Built to reach 5.8 m at scale 1; SunfireWyrm scales them to whoever it burns.)
            var breath = CinePS(holder, "WyrmBreath", flameMat, null, 0.42f, 0.56f, 12f, 16f, 0.35f, 0.6f, 0f, ParticleSystemShapeType.Cone, 0.1f, 900);
            var breathShape = breath.shape;
            breathShape.angle = 9f;
            WyrmFire(breath, WyrmFireRamp(0.5f), 0.7f, 2.8f, 0f, 0f, 1.6f);
            wyrm.breath = breath;
            var core = CinePS(holder, "WyrmBreathCore", flameMat, null, 0.25f, 0.36f, 18f, 22f, 0.2f, 0.34f, 0f, ParticleSystemShapeType.Cone, 0.05f, 400);
            var coreShape = core.shape;
            coreShape.angle = 4f;
            var coreRamp = new Gradient();
            coreRamp.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.97f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.67f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.56f, 0.6f), new GradientAlphaKey(0f, 1f) });
            WyrmFire(core, coreRamp, 0.8f, 1.8f, 0f, 0f, 2.2f);
            wyrm.breathCore = core;

            // ---- its voice: a source of its own that follows its head (not on the holder, which must stay put)
            var voice = Child(holder, "Voice", Vector3.zero).gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0.75f;
            voice.minDistance = 6f;
            voice.maxDistance = 50f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            wyrm.voice = voice;
            wyrm.roarSound = CastClip("Spell_WyrmRoar");
            wyrm.breathSound = CastClip("Spell_WyrmBreath");
            return wyrm;
        }

        /// <summary>
        /// The dragon as it is on its card (WyrmMeshData.Pose*, laid out by Tools/make_sunfire_wyrm.py): reared up out of
        /// a coil, its head lunging forward with its jaws open. Facing +Z, standing on 'parent'.
        /// </summary>
        private static void BuildWyrmPose(Transform parent)
        {
            Material skin = WyrmSkinMat();
            Mesh[] meshes = WyrmMeshes();
            float[] h = WyrmMeshData.PoseHead;
            Transform head = WyrmPart(parent, "Head", meshes[0], skin).transform;
            head.localPosition = WyrmVec(h, 0);
            head.localRotation = Quaternion.LookRotation(WyrmVec(h, 3), WyrmVec(h, 6));
            head.localScale = Vector3.one * h[9];
            Transform jaw = WyrmPart(head, "Jaw", meshes[1], skin).transform;
            jaw.localPosition = WyrmVec(WyrmMeshData.JawHinge, 0);
            jaw.localRotation = Quaternion.Euler(WyrmMeshData.PoseJawDegrees, 0f, 0f);
            float[] s = WyrmMeshData.PoseSegments;
            for (int i = 0; i < WyrmMeshData.Segments && i * 10 + 9 < s.Length; i++)
            {
                int at = i * 10;
                Transform seg = WyrmPart(parent, "Segment" + i.ToString("00"), WyrmSegmentMesh(meshes, i), skin).transform;
                seg.localPosition = WyrmVec(s, at);
                seg.localRotation = Quaternion.LookRotation(WyrmVec(s, at + 3), WyrmVec(s, at + 6));
                seg.localScale = new Vector3(WyrmMeshData.SegmentAcross[i], WyrmMeshData.SegmentAcross[i], WyrmMeshData.SegmentAlong[i]) * s[at + 9];
            }
        }
    }
}
