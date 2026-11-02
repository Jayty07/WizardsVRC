using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The school Surge auras (Starshard Shop): a power-up of school energy round the wizard that answers their duel.
    ///   - Out of a duel it smoulders: a low shell of flickering energy tongues licking up from the feet, a glow at the
    ///     chest, sparks rising.
    ///   - In a duel it grows with the pips they hold (PlayerCosmetics.SetPower scales it), so you can see who's charging.
    ///   - Casting a spell blasts it outward (the Flare system, fired by BattleClient).
    ///   - At full pips it erupts: tall spires of energy, crackling arcs round the body and a ring rolling out across the
    ///     ground (the Max system), until the pips are spent.
    /// Each school has its own colours and its own extra: Fire throws embers, Ice sheds frost crystals, Storm crackles
    /// with lightning, Life swirls with leaves, Death trails wisps, Myth sheds gold motes, Balance spins a ring of sand.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>The first Surge aura in the aura list (one per school after it, Fire to Balance).</summary>
        private const int SurgeFirst = 13;
        private const string AuraFlameTex = "Assets/WizardsVRC/Textures/Shop/AuraFlame.png";
        private const string AuraSparkTex = "Assets/WizardsVRC/Textures/Shop/AuraSpark.png";
        private const string AuraGlowTex = "Assets/WizardsVRC/Textures/Shop/Glow.png";

        // Per school: the energy's main colour, its hot core and its extra's colour.
        private static readonly Color[] SurgeMain =
        {
            new Color(1f, 0.42f, 0.08f), new Color(0.45f, 0.82f, 1f), new Color(0.62f, 0.35f, 1f), new Color(0.35f, 1f, 0.42f),
            new Color(0.5f, 0.2f, 0.75f), new Color(1f, 0.78f, 0.2f), new Color(1f, 0.62f, 0.25f),
        };
        private static readonly Color[] SurgeCore =
        {
            new Color(1f, 0.9f, 0.5f), new Color(0.92f, 1f, 1f), new Color(0.95f, 0.9f, 1f), new Color(0.88f, 1f, 0.7f),
            new Color(0.62f, 1f, 0.72f), new Color(1f, 1f, 0.8f), new Color(1f, 0.95f, 0.75f),
        };
        private static readonly Color[] SurgeExtra =
        {
            new Color(1f, 0.55f, 0.15f), new Color(0.85f, 0.97f, 1f), new Color(1f, 0.95f, 0.55f), new Color(0.6f, 1f, 0.35f),
            new Color(0.55f, 1f, 0.7f), new Color(1f, 0.9f, 0.45f), new Color(0.95f, 0.78f, 0.45f),
        };

        /// <summary>An additive particle material with one of the aura textures.</summary>
        private static Material AuraMat(string name, Color c, string texPath)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) return SparkleMat(name, c);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp != null && (imp.filterMode != FilterMode.Point || !imp.alphaIsTransparency || imp.mipmapEnabled))
            {
                imp.filterMode = FilterMode.Point;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.SetColor("_TintColor", c * 0.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Gradient Fade(Color a, Color b, float peak = 0.15f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, peak), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>A particle system with the settings every aura part shares (world space, scaled with its parents, culled off screen).</summary>
        private static ParticleSystem AuraPart(Transform parent, string name, Vector3 at, Material mat, float rate, Vector2 life, Vector2 size, int max, bool playing)
        {
            var go = Child(parent, name, at).gameObject;
            go.isStatic = false;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = playing;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = max;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var em = ps.emission;
            em.rateOverTime = rate;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Particles rising from a ring at the feet (the ring shape lies flat; local Z is up).</summary>
        private static void RingShape(ParticleSystem ps, float radius, float thickness)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius;
            sh.radiusThickness = thickness;
            sh.rotation = new Vector3(-90f, 0f, 0f);
        }

        private static void Rise(ParticleSystem ps, float up0, float up1, float inward)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(up0, up1);
            vel.radial = new ParticleSystem.MinMaxCurve(inward);
        }

        private static void Stretch(ParticleSystem ps, float length, float speedScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = length;
            r.velocityScale = speedScale;
        }

        private static void Flicker(ParticleSystem ps, float strength, float frequency)
        {
            var n = ps.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = frequency;
            n.scrollSpeed = 1.5f;
            n.quality = ParticleSystemNoiseQuality.Low;
        }

        private static void Shrink(ParticleSystem ps, float start, float peak, float end)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, start), new Keyframe(0.3f, peak), new Keyframe(1f, end)));
        }

        /// <summary>
        /// Builds school 'school's Surge aura at the player-object root: the aura itself (returned, played and stopped like
        /// every aura), and beside it the Flare burst and the Max eruption, which only PlayerCosmetics fires.
        /// </summary>
        private static ParticleSystem SurgeAura(Transform root, int auraIndex, int school, out ParticleSystem flare, out ParticleSystem maxFx)
        {
            Color main = SurgeMain[school], core = SurgeCore[school], extra = SurgeExtra[school];
            string key = "Surge" + school;
            var flameMat = AuraMat(key + "Flame", main, AuraFlameTex);
            var coreMat = AuraMat(key + "Core", core, AuraFlameTex);
            var sparkMat = AuraMat(key + "Spark", core, AuraSparkTex);
            var glowMat = AuraMat(key + "Glow", main, AuraGlowTex);
            var extraMat = AuraMat(key + "Extra", extra, school == 1 || school == 5 ? UiTexDir + "/Sparkle.png" : AuraSparkTex);

            // ---- the aura: a shell of flickering tongues of energy licking up round the body, narrowing above the head
            var shell = AuraPart(root, "Aura" + auraIndex, Vector3.zero, flameMat, 46f, new Vector2(0.45f, 0.7f), new Vector2(0.38f, 0.6f), 90, false);
            RingShape(shell, 0.42f, 0.25f);
            Rise(shell, 2.2f, 3.2f, -0.55f);
            Stretch(shell, 1.35f, 0.06f);
            Flicker(shell, 0.35f, 1.8f);
            Shrink(shell, 0.5f, 1f, 0.25f);
            var col = shell.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(core, main);
            // A hotter inner layer.
            var inner = AuraPart(shell.transform, "Inner", Vector3.zero, coreMat, 22f, new Vector2(0.3f, 0.45f), new Vector2(0.24f, 0.36f), 40, false);
            RingShape(inner, 0.26f, 0.4f);
            Rise(inner, 2.4f, 3f, -0.4f);
            Stretch(inner, 1.2f, 0.05f);
            Flicker(inner, 0.2f, 2.5f);
            var ic = inner.colorOverLifetime; ic.enabled = true; ic.color = Fade(Color.white, core);
            // The glow round the body.
            var glow = AuraPart(shell.transform, "Glow", new Vector3(0, 1f, 0), glowMat, 5f, new Vector2(0.5f, 0.7f), new Vector2(1.7f, 2.1f), 6, false);
            var gsh = glow.shape; gsh.enabled = false;
            var gc = glow.colorOverLifetime; gc.enabled = true; gc.color = Fade(main, main, 0.4f);
            // Sparks streaking up.
            var sparks = AuraPart(shell.transform, "Sparks", Vector3.zero, sparkMat, 14f, new Vector2(0.35f, 0.55f), new Vector2(0.05f, 0.09f), 30, false);
            RingShape(sparks, 0.5f, 0.3f);
            Rise(sparks, 3.5f, 5.2f, -0.2f);
            Stretch(sparks, 2.2f, 0.1f);
            // The school's own touch.
            BuildSurgeExtra(shell.transform, school, extraMat);

            // ---- the flare when a spell is cast: a blast of energy outward and up, and a ring across the ground
            var fgo = Child(root, "Aura" + auraIndex + "Flare", Vector3.zero);
            flare = AuraPart(fgo, "Blast", Vector3.zero, flameMat, 0f, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), 120, true);
            var fsh = flare.shape;
            fsh.shapeType = ParticleSystemShapeType.Hemisphere;
            fsh.radius = 0.5f;
            fsh.rotation = new Vector3(-90f, 0f, 0f);
            var fm = flare.main;
            fm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            Stretch(flare, 1.6f, 0.08f);
            var fc = flare.colorOverLifetime; fc.enabled = true; fc.color = Fade(Color.white, main, 0.05f);
            var fd = flare.limitVelocityOverLifetime; fd.enabled = true; fd.limit = 1.2f; fd.dampen = 0.15f;
            var ring = AuraPart(flare.transform, "Ring", new Vector3(0, 0.05f, 0), coreMat, 0f, new Vector2(0.4f, 0.55f), new Vector2(0.4f, 0.55f), 80, true);
            RingShape(ring, 0.3f, 0f);
            var rm = ring.main; rm.startSpeed = new ParticleSystem.MinMaxCurve(4f, 5f);
            var rv = ring.velocityOverLifetime; rv.enabled = true; rv.space = ParticleSystemSimulationSpace.Local;
            rv.x = new ParticleSystem.MinMaxCurve(0f); rv.y = new ParticleSystem.MinMaxCurve(0f); rv.z = new ParticleSystem.MinMaxCurve(0.4f);
            var rc = ring.colorOverLifetime; rc.enabled = true; rc.color = Fade(Color.white, main, 0.05f);
            fgo.gameObject.isStatic = false;

            // ---- full pips: spires of energy, crackling arcs, a ring rolling out across the ground
            var mgo = Child(root, "Aura" + auraIndex + "Max", Vector3.zero);
            maxFx = AuraPart(mgo, "Spires", Vector3.zero, flameMat, 26f, new Vector2(0.55f, 0.8f), new Vector2(0.3f, 0.5f), 60, false);
            RingShape(maxFx, 0.55f, 0.15f);
            Rise(maxFx, 4.5f, 6.5f, -0.35f);
            Stretch(maxFx, 2.4f, 0.12f);
            Flicker(maxFx, 0.25f, 2.2f);
            var mc = maxFx.colorOverLifetime; mc.enabled = true; mc.color = Fade(Color.white, main);
            // Arcs: short, jagged, snapping round the body (Storm's are the wildest).
            var arcs = AuraPart(maxFx.transform, "Arcs", new Vector3(0, 1f, 0), sparkMat, school == 2 ? 30f : 16f, new Vector2(0.08f, 0.16f), new Vector2(0.04f, 0.07f), 40, false);
            var ash = arcs.shape; ash.shapeType = ParticleSystemShapeType.Sphere; ash.radius = 0.7f; ash.radiusThickness = 0.3f;
            var am = arcs.main; am.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            Flicker(arcs, school == 2 ? 3.5f : 2f, 6f);
            var tr = arcs.trails;
            tr.enabled = true;
            tr.lifetime = 0.6f;
            tr.minVertexDistance = 0.04f;
            tr.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var ar = arcs.GetComponent<ParticleSystemRenderer>();
            ar.trailMaterial = sparkMat;
            var acol = arcs.colorOverLifetime; acol.enabled = true; acol.color = Fade(Color.white, core, 0.02f);
            // The ring rolling out across the ground, again and again.
            var roll = AuraPart(maxFx.transform, "Roll", new Vector3(0, 0.05f, 0), glowMat, 0f, new Vector2(0.6f, 0.7f), new Vector2(0.25f, 0.35f), 120, false);
            RingShape(roll, 0.4f, 0f);
            var rlm = roll.main; rlm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 3f);
            var rle = roll.emission;
            rle.rateOverTime = 0f;
            rle.SetBursts(new[] { new ParticleSystem.Burst(0f, 36, 36, 0, 0.8f) });
            var rlc = roll.colorOverLifetime; rlc.enabled = true; rlc.color = Fade(core, main, 0.05f);
            mgo.gameObject.isStatic = false;
            return shell;
        }

        /// <summary>Each school's own touch inside its aura.</summary>
        private static void BuildSurgeExtra(Transform shell, int school, Material mat)
        {
            switch (school)
            {
                case 0:   // Fire: embers tossed up and drifting
                {
                    var p = AuraPart(shell, "Embers", new Vector3(0, 0.4f, 0), mat, 14f, new Vector2(0.9f, 1.4f), new Vector2(0.04f, 0.07f), 30, false);
                    RingShape(p, 0.5f, 0.5f);
                    Rise(p, 1.2f, 2.2f, 0.25f);
                    Flicker(p, 0.6f, 1.2f);
                    break;
                }
                case 1:   // Ice: frost crystals shed and spinning slowly
                {
                    var p = AuraPart(shell, "FrostShards", new Vector3(0, 0.8f, 0), mat, 8f, new Vector2(1f, 1.5f), new Vector2(0.08f, 0.13f), 20, false);
                    var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f;
                    Rise(p, 0.3f, 0.6f, 0.15f);
                    var rot = p.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
                    break;
                }
                case 2:   // Storm: little bolts flickering in the shell all the time
                {
                    var p = AuraPart(shell, "Static", new Vector3(0, 1f, 0), mat, 12f, new Vector2(0.06f, 0.12f), new Vector2(0.04f, 0.06f), 20, false);
                    var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.55f;
                    var m = p.main; m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
                    Flicker(p, 3f, 8f);
                    Stretch(p, 3f, 0.15f);
                    break;
                }
                case 3:   // Life: leaves swirling up round the body
                {
                    var p = AuraPart(shell, "Leaves", new Vector3(0, 0.2f, 0), mat, 10f, new Vector2(1.2f, 1.7f), new Vector2(0.07f, 0.11f), 24, false);
                    RingShape(p, 0.6f, 0.2f);
                    Rise(p, 0.8f, 1.3f, 0f);
                    var v = p.velocityOverLifetime; v.orbitalZ = new ParticleSystem.MinMaxCurve(2.4f);
                    var rot = p.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                    break;
                }
                case 4:   // Death: pale wisps curling up out of the dark
                {
                    var p = AuraPart(shell, "Wisps", new Vector3(0, 0.3f, 0), mat, 8f, new Vector2(1.2f, 1.8f), new Vector2(0.12f, 0.18f), 20, false);
                    RingShape(p, 0.55f, 0.3f);
                    Rise(p, 0.7f, 1.1f, -0.1f);
                    Flicker(p, 0.5f, 0.8f);
                    Stretch(p, 2f, 0.2f);
                    break;
                }
                case 5:   // Myth: gold motes drifting out of the light
                {
                    var p = AuraPart(shell, "Motes", new Vector3(0, 1f, 0), mat, 14f, new Vector2(1f, 1.5f), new Vector2(0.06f, 0.1f), 26, false);
                    var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.7f;
                    Rise(p, 0.2f, 0.5f, 0.3f);
                    break;
                }
                default:   // Balance: a ring of sand spinning round the waist
                {
                    var p = AuraPart(shell, "Sand", new Vector3(0, 0.9f, 0), mat, 22f, new Vector2(0.9f, 1.2f), new Vector2(0.035f, 0.06f), 40, false);
                    RingShape(p, 0.75f, 0.05f);
                    Rise(p, -0.05f, 0.05f, 0f);
                    var v = p.velocityOverLifetime; v.orbitalZ = new ParticleSystem.MinMaxCurve(3.2f);
                    break;
                }
            }
        }
    }
}
