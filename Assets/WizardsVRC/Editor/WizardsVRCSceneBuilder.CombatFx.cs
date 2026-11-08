using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Duel presentation (Wizard101 style, PSX look): the layered circle with its start flash, pips in front of every
    /// seat with swirling motes, breaking shields / blades / traps, floating combat text and combat sounds. Also the
    /// interface sounds wired onto every button in the scene.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string UiSoundDir = "Assets/WizardsVRC/Audio/UI";
        /// <summary>The whole duel ring sits this far above the ground so it's never hidden under paths and lawns.</summary>
        private const float RingLift = 0.14f;

        private static AudioClip UiClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiSoundDir + "/" + name + ".wav");
            if (clip == null) Debug.LogWarning("[WizardsVRC] Missing sound " + UiSoundDir + "/" + name + ".wav");
            return clip;
        }

        private static Material Glow(string name, Color c, float strength)
        {
            Material m = Mat(name, c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================== circle

        /// <summary>
        /// The duel circle (art from Tools/make_duel_ring.py): the carved stone seat band (fixed, so its sockets stay under
        /// the seats), the star-chart sigil with the seven school emblems and the outer rune band (those two turn opposite
        /// ways), laid on a stone dais with a gold trim, two crystals hovering at the ends of the line between the sides,
        /// and motes rising off the rune band. Plus the start flash: a white copy of the circle that settles into it, a
        /// column of light, a light flash, sparks and the circle sound. All of it is looks: no colliders.
        /// </summary>
        private static void BuildCircleLayers(Transform ring, string name, BattleClient client)
        {
            // Layers 1.5 cm apart (base, then sigil and runes, then seat rings, then the flash) so they never fight; the
            // whole ring is lifted RingLift above the ground.
            float d = DuelCircleRadius * 2f;
            Color glow = new Color(0.35f, 0.35f, 0.42f);
            FlatDecal(ring, "CircleBase", new Vector3(0, 0f, 0), d,
                CutoutMat("DuelRingBase", PixelTexture(TexturesDir + "/DuelRingBase.png"), Color.white, glow));
            Transform sigil = Child(ring, "SigilSpin", Vector3.zero);
            Material sigilMat = CutoutMat("DuelRingSigil", PixelTexture(TexturesDir + "/DuelRingSigil.png"), Color.white, glow);
            FlatDecal(sigil, "CircleSigil", new Vector3(0, 0.015f, 0), d, sigilMat);
            Transform runes = Child(ring, "RuneSpin", Vector3.zero);
            FlatDecal(runes, "CircleRunes", new Vector3(0, 0.015f, 0), d,
                CutoutMat("DuelRingRunes", PixelTexture(TexturesDir + "/DuelRingRunes.png"), Color.white, glow * 1.3f));
            client.circleSigil = sigil;
            client.circleRunes = runes;
            client.circleSigilMat = sigilMat;
            client.circleSigilGlow = glow;
            BuildRingDais(ring);
            BuildRingPylons(ring);
            BuildRuneMotes(ring);

            // Start flash. Its own material per encounter (its glow is animated).
            Transform flash = Child(ring, "CircleFlash", new Vector3(0, 0.045f, 0));
            Material flashMat = CutoutMat("DuelFlash_" + name, PixelTexture(TexturesDir + "/DuelRingFlash.png"), Color.white, Color.white * 3f);
            FlatDecal(flash, "FlashDisc", Vector3.zero, d, flashMat);
            flash.gameObject.SetActive(false);
            client.circleFlash = flash;
            client.circleFlashMat = flashMat;

            Transform pillar = Child(ring, "FlashPillar", Vector3.zero);
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = "Column";
            col.transform.SetParent(pillar, false);
            col.transform.localPosition = new Vector3(0, 4f, 0);
            col.transform.localScale = new Vector3(d, 4f, d);
            col.GetComponent<Renderer>().sharedMaterial = Glow("DuelFlashColumn", Color.white, 2.5f);
            col.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(col.GetComponent<Collider>());
            pillar.gameObject.SetActive(false);
            client.flashPillar = pillar;

            var light = Child(ring, "FlashLight", new Vector3(0, 3f, 0)).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.renderMode = LightRenderMode.ForceVertex;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.range = 26f;
            light.intensity = 0f;
            light.enabled = false;
            client.flashLight = light;

            var sparksGo = Child(ring, "StartSparks", new Vector3(0, 0.2f, 0)).gameObject;
            var sparks = sparksGo.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            var em = sparks.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120), new ParticleSystem.Burst(0.2f, 60) });
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = DuelCircleRadius * 0.9f;
            shape.radiusThickness = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var sz = sparks.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var sr = sparksGo.GetComponent<ParticleSystemRenderer>();
            sr.sharedMaterial = Glow("DuelPip", Color.white, 1.6f);
            sr.renderMode = ParticleSystemRenderMode.Billboard;
            client.startSparks = sparks;

            var audio = Child(ring, "CircleAudio", new Vector3(0, 1f, 0)).gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.75f;
            audio.minDistance = 8f;
            audio.maxDistance = 45f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            client.circleAudio = audio;
            client.circleSound = UiClip("combat_circle_active");
        }

        /// <summary>
        /// The stone dais the circle is laid on: a lip just under the art's edge with a gold trim round the circle, sloping
        /// down to the ground and on into it, so uneven ground never shows a gap under the floating art. No collider.
        /// </summary>
        private static void BuildRingDais(Transform ring)
        {
            var go = new GameObject("Dais");
            go.transform.SetParent(ring, false);
            go.AddComponent<MeshFilter>().sharedMesh = DaisMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { RingStoneMat(), RingGoldMat() };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material RingStoneMat()
        {
            return PsxTexMat("DuelDais", new Color(0.56f, 0.57f, 0.72f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.4f);
        }

        private static Material RingGoldMat()
        {
            return PMat("DuelDaisGold", new Color(0.95f, 0.76f, 0.32f), 1f, new Color(0.45f, 0.32f, 0.08f));
        }

        private static Mesh _daisMesh;

        /// <summary>
        /// The dais as one mesh round the circle (shared by every ring in a build): submesh 0 the stone (lip, slope, the
        /// buried skirt), submesh 1 the gold trim on the lip. Profiles are (radius, height) in the ring's space; the ground
        /// is RingLift below it.
        /// </summary>
        private static Mesh DaisMesh()
        {
            if (_daisMesh != null) return _daisMesh;
            float R = DuelCircleRadius, ground = -RingLift;
            var stone = new[] { new Vector2(R - 0.05f, -0.008f), new Vector2(R + 0.16f, -0.008f), new Vector2(R + 0.62f, ground - 0.05f), new Vector2(R + 0.62f, ground - 0.6f) };
            var gold = new[] { new Vector2(R - 0.005f, 0.004f), new Vector2(R + 0.11f, 0.004f), new Vector2(R + 0.11f, -0.008f) };
            const int segs = 72;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var stoneTris = new List<int>();
            var goldTris = new List<int>();
            System.Action<Vector2[], List<int>> strips = (profile, tris) =>
            {
                for (int p = 0; p + 1 < profile.Length; p++)
                {
                    int start = verts.Count;
                    for (int i = 0; i <= segs; i++)
                    {
                        float a = i / (float)segs * Mathf.PI * 2f;
                        Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                        verts.Add(dir * profile[p].x + Vector3.up * profile[p].y);
                        verts.Add(dir * profile[p + 1].x + Vector3.up * profile[p + 1].y);
                        uvs.Add(new Vector2(i / (float)segs * 24f, 0f));
                        uvs.Add(new Vector2(i / (float)segs * 24f, 1f));
                    }
                    for (int i = 0; i < segs; i++)
                    {
                        int v = start + i * 2;
                        // Wound clockwise seen from above / outside, so the faces look up and outward.
                        tris.AddRange(new[] { v, v + 1, v + 2, v + 1, v + 3, v + 2 });
                    }
                }
            };
            strips(stone, stoneTris);
            strips(gold, goldTris);
            var mesh = new Mesh { name = "DuelRingDais" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(stoneTris, 0);
            mesh.SetTriangles(goldTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _daisMesh = mesh;
            return mesh;
        }

        /// <summary>
        /// A crystal hovering over a stone plinth at each end of the line between the two sides (east and west, clear of
        /// every seat), bobbing and turning out of step (the shared float clips, WizardsVRCSceneBuilder.Magic.cs).
        /// </summary>
        private static void BuildRingPylons(Transform ring)
        {
            var stone = RingStoneMat();
            var gold = RingGoldMat();
            var crystal = PMat("DuelPylonCrystal", new Color(0.72f, 0.86f, 1f), 1f, new Color(0.42f, 0.58f, 0.95f));
            float r = DuelCircleRadius + 0.42f, top = 0.5f, bottom = -RingLift - 0.3f;
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? 1f : -1f;
                var root = Child(ring, "Pylon" + k, new Vector3(side * r, 0f, 0f));
                root.gameObject.isStatic = false;
                Shape(root, "Plinth", CylMesh(), new Vector3(0, (top + bottom) / 2f, 0), new Vector3(0.5f, (top - bottom) / 2f, 0.5f), stone, false, Quaternion.identity);
                Shape(root, "PlinthBand", CylMesh(), new Vector3(0, 0.08f, 0), new Vector3(0.56f, 0.03f, 0.56f), gold, false, Quaternion.identity);
                Shape(root, "PlinthCap", CylMesh(), new Vector3(0, top + 0.04f, 0), new Vector3(0.62f, 0.04f, 0.62f), gold, false, Quaternion.identity);
                Shape(root, "PlinthGem", GemMesh(), new Vector3(0, top + 0.12f, 0), new Vector3(0.14f, 0.1f, 0.14f), crystal, false, Quaternion.identity);
                var pivot = Child(root, "Float_DuelPylon", new Vector3(0, 1.38f, 0));
                var body = Child(pivot, "Body", Vector3.zero);
                Shape(body, "Crystal", GemMesh(), Vector3.zero, new Vector3(0.34f, 0.62f, 0.34f), crystal, false, Quaternion.identity);
                // Two small shards orbiting with it, one above and one below.
                Shape(body, "ShardUp", GemMesh(), new Vector3(0.32f, 0.42f, 0f), new Vector3(0.08f, 0.14f, 0.08f), crystal, false, Quaternion.Euler(0, 0, -20f));
                Shape(body, "ShardDown", GemMesh(), new Vector3(-0.32f, -0.4f, 0f), new Vector3(0.08f, 0.14f, 0.08f), crystal, false, Quaternion.Euler(0, 0, 160f));
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
                foreach (var col in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                foreach (var rr in root.GetComponentsInChildren<Renderer>(true)) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                LoopAnimator(pivot.gameObject, FloatClip("DuelPylon", k, new FloatKind(0.09f, 8f, 2, 1)), AnimatorCullingMode.CullCompletely);
            }
        }

        /// <summary>Faint gold motes drifting up off the rune band while the circle is up (it's only on during a duel).</summary>
        private static void BuildRuneMotes(Transform ring)
        {
            var go = Child(ring, "RuneMotes", new Vector3(0, 0.05f, 0)).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.4f), new Color(0.75f, 0.9f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 70;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var em = ps.emission;
            em.rateOverTime = 16f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = DuelCircleRadius * 0.955f;
            sh.radiusThickness = 0.02f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = SparkleMat("DuelRingMote", new Color(1f, 0.85f, 0.5f));
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ================================================================== pips

        /// <summary>
        /// Seven pip gems in an arc in front of every seat (white = pip, gold = power pip), and two mote particle systems
        /// per seat that swirl round the combatant; the client emits more the more pips they hold.
        /// </summary>
        private static void BuildSeatPips(Transform ring, BattleClient client)
        {
            int slots = BattleManager.SLOTS, max = BattleManager.MAX_PIPS;
            Material white = Glow("DuelPip", Color.white, 1.6f);
            Material gold = Glow("DuelPowerPip", new Color(1f, 0.72f, 0.18f), 1.8f);
            client.pipMat = white;
            client.powerPipMat = gold;
            client.pipIcons = new Renderer[slots * max];
            client.pipSwirls = new ParticleSystem[slots];
            client.powerSwirls = new ParticleSystem[slots];
            client.auraSwirls = new ParticleSystem[slots];
            client.auraRenderers = new Renderer[slots];
            Material auraMat = Glow("DuelPipAura", Color.white, 2.2f);
            for (int s = 0; s < slots; s++)
            {
                Transform seat = client.seats[s];
                float scale = client.seatScales != null && s < client.seatScales.Length ? Mathf.Max(1f, client.seatScales[s]) : 1f;
                for (int k = 0; k < max; k++)
                {
                    float x = (k - (max - 1) * 0.5f) * 0.3f;
                    float z = 1.1f + (scale - 1f) * 0.6f - Mathf.Pow(k - (max - 1) * 0.5f, 2f) * 0.025f;
                    Vector3 p = seat.localPosition + seat.localRotation * new Vector3(x, 0.32f, z);
                    GameObject gem = Shape(ring, "Pip" + s + "_" + k, GemMesh(), p, new Vector3(0.15f, 0.22f, 0.15f), white, false, Quaternion.identity);
                    gem.isStatic = false;
                    var r = gem.GetComponent<Renderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    gem.SetActive(false);
                    client.pipIcons[s * max + k] = r;
                }
                client.pipSwirls[s] = MoteSwirl(ring, "PipSwirl" + s, seat.localPosition, scale, white);
                client.powerSwirls[s] = MoteSwirl(ring, "PowerSwirl" + s, seat.localPosition, scale, gold);
                // The pip aura: bigger, slower motes in a wider ring (tinted per wizard at runtime).
                var aura = MoteSwirl(ring, "PipAura" + s, seat.localPosition, scale, auraMat);
                var am = aura.main;
                am.startSize = new ParticleSystem.MinMaxCurve(0.12f * scale, 0.26f * scale);
                am.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
                am.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
                am.maxParticles = 140;
                var ash = aura.shape;
                ash.radius = 0.8f * scale;
                client.auraSwirls[s] = aura;
                client.auraRenderers[s] = aura.GetComponent<ParticleSystemRenderer>();
            }
        }

        /// <summary>Motes rising round a seat, orbiting it and waving a little. Emits only when the client calls Emit.</summary>
        private static ParticleSystem MoteSwirl(Transform ring, string name, Vector3 at, float scale, Material mat)
        {
            var go = Child(ring, name, at + new Vector3(0, 0.1f, 0)).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 0.6f * scale;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(2.4f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.25f);
            noise.frequency = 0.8f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = mat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // ================================================================== breaks, floating text, sounds

        private static void BuildCombatFeedback(Transform ring, BattleClient client)
        {
            // Shards that blink and burst where a shield / blade / trap breaks.
            const int pops = 8;
            client.breakPops = new Transform[pops];
            client.breakPopRenderers = new Renderer[pops];
            for (int i = 0; i < pops; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Quad);
                c.name = "BreakPop" + i;
                c.transform.SetParent(ring, false);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                var r = c.GetComponent<Renderer>();
                r.sharedMaterial = client.pipMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.SetActive(false);
                client.breakPops[i] = c.transform;
                client.breakPopRenderers[i] = r;
            }

            // Floating combat text: a school icon and a number / word, billboarded to each viewer.
            const int floaters = 14;
            client.floaters = new Transform[floaters];
            client.floaterTexts = new Text[floaters];
            client.floaterIcons = new RawImage[floaters];
            for (int i = 0; i < floaters; i++)
            {
                Transform holder = Child(ring, "Floater" + i, Vector3.zero);
                var go = new GameObject("Canvas", typeof(RectTransform));
                go.transform.SetParent(holder, false);
                go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(900, 260);
                rt.localScale = Vector3.one * 0.0055f;
                Text t = Label(go.transform, "Text", new Vector2(60, 0), new Vector2(760, 260), "", 76, TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(3f, -3f);
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.sizeDelta = new Vector2(120, 120);
                irt.anchoredPosition = new Vector2(-330, 0);
                var img = iconGo.AddComponent<RawImage>();
                img.raycastTarget = false;
                client.floaters[i] = holder;
                client.floaterTexts[i] = t;
                client.floaterIcons[i] = img;
                holder.gameObject.SetActive(false);
            }
            client.schoolIcons = new Texture[SchoolLabels.Length + 1];
            for (int k = 0; k <= SchoolLabels.Length; k++)
                client.schoolIcons[k] = PixelTexture(FxIconDir + "/School" + (k < SchoolLabels.Length ? SchoolLabels[k] : "General") + ".png");

            var hit = Child(ring, "HitAudio", Vector3.zero).gameObject.AddComponent<AudioSource>();
            hit.playOnAwake = false;
            hit.spatialBlend = 0.8f;
            hit.minDistance = 4f;
            hit.maxDistance = 35f;
            hit.rolloffMode = AudioRolloffMode.Linear;
            client.hitAudio = hit;
            client.wizardHitSounds = new[] { UiClip("combat_playerfemale_gethit_01"), UiClip("combat_playermale_gethit_01") };
            client.healSound = UiClip("ui_health_powerup_01");
            client.defeatSound = UiClip("ui_healthball_crack");
        }

        // ================================================================== interface sounds

        private static UiAudio BuildUiAudio(Transform systems)
        {
            var go = Child(systems, "UiAudio", Vector3.zero).gameObject;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            var ui = UdonSharpUndo.AddComponent<UiAudio>(go);
            ui.source = src;
            ui.click = UiClip("HudSelect");
            ui.hover = UiClip("ButtonMouseOver");
            ui.hudHover = UiClip("HudMouseOver");
            ui.ok = UiClip("OKButtonClick");
            ui.cancel = UiClip("HudCancel");
            ui.delete = UiClip("DeleteButtonClick");
            ui.nextClick = UiClip("ui_hudgenericnext_click");
            ui.nextHover = UiClip("ui_hudgenericnext_hover");
            ui.tab = UiClip("SweepChange");
            ui.bookOpen = UiClip("ui_hudspellbook_open");
            ui.cardHover = UiClip("ui_spellcard_hover");
            ui.cardClick = UiClip("ui_spellcard_click");
            ui.spellSelect = UiClip("SpellSelect");
            ui.nameSpin = UiClip("ui_charactername_spin");
            ui.popup = UiClip("ui_chatbubble_popup");
            ui.heal = UiClip("ui_health_powerup_01");
            // Tools/make_quest_sounds.py
            ui.questProgress = UiClip("QuestProgress");
            ui.questObjective = UiClip("QuestObjective");
            ui.questComplete = UiClip("QuestComplete");
            ui.discovery = UiClip("Discovery");
            return ui;
        }

        /// <summary>
        /// Adds a click and a hover sound to every uGUI button in the scene, chosen from the button's name (Delete, Cancel,
        /// Next/Prev, tabs, New, Select/Yes/Equip...). Battle cards are skipped: BattleClient plays their sounds.
        /// </summary>
        private static int WireUiSounds(Scene scene, UiAudio ui)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(ui);
            if (target == null) return 0;
            int wired = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                {
                    string n = b.gameObject.name;
                    string low = n.ToLowerInvariant();
                    if (n.StartsWith("Card") && b.GetComponentInParent<Canvas>(true) != null && b.GetComponentInParent<Canvas>(true).name == "HandCanvas") continue;
                    string parent = b.transform.parent != null ? b.transform.parent.name : "";
                    bool hud = b.GetComponentInParent<Canvas>(true) != null && b.GetComponentInParent<Canvas>(true).name == "HandCanvas";
                    string click = nameof(UiAudio.PlayClick), hover = hud ? nameof(UiAudio.PlayHudHover) : nameof(UiAudio.PlayHover);
                    if (low.Contains("delete") || (n == "Yes" && parent.Contains("Delete"))) click = nameof(UiAudio.PlayDelete);
                    else if (low.Contains("tab")) click = nameof(UiAudio.PlayTab);
                    else if (low.Contains("cancel") || n == "No" || low.Contains("close") || low == "back" || low == "leave" || low == "remove")
                        click = nameof(UiAudio.PlayCancel);
                    else if (low.Contains("next") || low.Contains("prev") || low.Contains("page"))
                    {
                        click = nameof(UiAudio.PlayNextClick);
                        hover = nameof(UiAudio.PlayNextHover);
                    }
                    else if (low.StartsWith("new")) click = nameof(UiAudio.PlayNameSpin);
                    else if (low.Contains("select") || n == "Yes" || low.StartsWith("ok") || low.Contains("confirm") || low.StartsWith("pick") || low == "add" ||
                             low.Contains("equip") || low.Contains("learn") || low.StartsWith("target") || low.Contains("save") || low == "action")
                        click = nameof(UiAudio.PlayOk);
                    UnityEventTools.AddStringPersistentListener(b.onClick, new UnityAction<string>(target.SendCustomEvent), click);
                    var trigger = b.GetComponent<EventTrigger>() ?? b.gameObject.AddComponent<EventTrigger>();
                    if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
                    var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    UnityEventTools.AddStringPersistentListener(entry.callback, new UnityAction<string>(target.SendCustomEvent), hover);
                    trigger.triggers.Add(entry);
                    wired++;
                }
            return wired;
        }
    }
}
