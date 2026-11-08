using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Casting a spell (shared by every duel circle, local to each player, on its own root so the area culler never
    /// switches it off):
    ///
    ///   CastPattern     the rune board that opens for a spell under 100% accuracy (points along the school's symbol, a
    ///                   faint and a bright copy of the symbol, numbered buttons for desktop, a timer, Cancel) and the
    ///                   casting wand that waits at your hip in VR (a local pickup; PatternWand passes on its events)
    ///   SpellCinematic  the rig every attack spell's choreography plays on: the rune on the floor, the wind-up's
    ///                   streams, rings, embers, spiral and column, the bolts in three shapes with their trails, the beam,
    ///                   two portals, bursts, shards, a flash, shock rings, a column on the target, spikes, the rune stamp,
    ///                   embers and a light, all re-coloured for the school at runtime; and the fade sphere for the caster
    ///
    /// The rune and stamp use the drawn-symbol shader and data maps (Tools/make_glyph_draw.py); rings use
    /// Textures/SpellRing.png (Tools/make_spell_ring.py); the sounds are Audio/Cast/Pattern_*.wav and Spell_*.wav
    /// (Tools/make_spell_cast_sounds.py).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string SpellRingTex = TexturesDir + "/SpellRing.png";

        private static void BuildSpellCasting(List<BattleClient> clients, CutscenePlayer cutscenes, InventoryBook book)
        {
            var root = new GameObject("SpellCasting").transform;
            int walk = LayerMask.NameToLayer("Walkthrough");
            if (walk < 0) walk = 17;
            int symbols = GlyphPenData.Labels.Length;
            var glyphTex = new Texture2D[symbols];
            for (int k = 0; k < symbols; k++) glyphTex[k] = DataTexture(TexturesDir + "/CastDraw/GlyphDraw" + GlyphPenData.Labels[k] + ".png");
            // One look per symbol: the seven schools, then General.
            var cores = new Material[symbols];
            for (int k = 0; k < symbols; k++) cores[k] = Glow("SpellCineCore" + GlyphPenData.Labels[k], GlyphColors[k], 1.7f);

            CastPattern pattern = BuildCastPattern(root, walk, glyphTex, cores);
            SpellCinematic cine = BuildSpellCinematicRig(root, walk, glyphTex, cores, cutscenes);
            foreach (var c in clients)
            {
                c.castPattern = pattern;
                c.spellCinematic = cine;
            }
            if (book != null) book.castPattern = pattern;
            ExtraBehaviours.Add(pattern);
            ExtraBehaviours.Add(cine);
        }

        // ================================================================== the rune board and the wand

        private static CastPattern BuildCastPattern(Transform root, int walk, Texture2D[] glyphTex, Material[] cores)
        {
            var holder = Child(root, "CastPattern", Vector3.zero);
            var pattern = UdonSharpUndo.AddComponent<CastPattern>(holder.gameObject);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(pattern);
            const float units = 230f;

            var boardGo = new GameObject("CastPatternBoard", typeof(RectTransform));
            boardGo.transform.SetParent(root, false);
            MakeWorldCanvas(boardGo, new Vector2(640, 780), Vector3.zero, 0.0011f);
            var board = (RectTransform)boardGo.transform;
            Panel(board, "Backing", Vector2.zero, new Vector2(560, 560), new Color(0.04f, 0.03f, 0.09f, 0.55f));
            GoldRule(board, "TopRule", new Vector2(0, 300), 520f);

            // The symbol: faint all over (the guide), and bright as far as it's been traced.
            Material ghostMat = GlyphQuadMat("CastPatternGhost");
            Material drawnMat = GlyphQuadMat("CastPatternDrawn");
            pattern.ghostGlyph = BoardQuad(board, "GhostSymbol", -2f, units * 2f, ghostMat);
            pattern.drawnGlyph = BoardQuad(board, "DrawnSymbol", -4f, units * 2f, drawnMat);

            // The points, and a numbered button on each for desktop.
            Material idle = Glow("CastPatternIdle", new Color(0.85f, 0.72f, 0.4f), 0.9f);
            Material next = Glow("CastPatternNext", new Color(1f, 0.98f, 0.9f), 2.2f);
            Material miss = Glow("CastPatternMiss", new Color(0.35f, 0.35f, 0.38f), 0.3f);
            pattern.nodeIdleMat = idle;
            pattern.nodeNextMat = next;
            pattern.nodeMissMat = miss;
            pattern.schoolMats = cores;
            int max = CastPattern.MAX_NODES;
            pattern.nodes = new Transform[max];
            pattern.nodeRenderers = new Renderer[max];
            pattern.dotButtons = new GameObject[max];
            pattern.dotLabels = new Text[max];
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            for (int k = 0; k < max; k++)
            {
                GameObject node = Shape(board, "Point" + k, BlobMesh(), new Vector3(0, 0, -12f), Vector3.one * 24f, idle, false, Quaternion.identity);
                node.isStatic = false;
                node.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pattern.nodes[k] = node.transform;
                pattern.nodeRenderers[k] = node.GetComponent<Renderer>();
                GameObject dot = Button(board, "Dot" + k, Vector2.zero, new Vector2(84, 84), (k + 1).ToString(), new Color(0.3f, 0.22f, 0.5f, 0.55f), target, "Dot" + k);
                var img = dot.GetComponent<Image>();
                if (knob != null) img.sprite = knob;
                var label = dot.GetComponentInChildren<Text>();
                label.fontSize = 34;
                label.gameObject.AddComponent<Outline>().effectColor = Color.black;
                pattern.dotButtons[k] = dot;
                pattern.dotLabels[k] = label;
                node.SetActive(false);
                dot.SetActive(false);
            }

            // A faint line through every point, a bright one through those done.
            pattern.guideLine = PatternLine(board, "GuideLine", Glow("CastPatternGuide", new Color(0.8f, 0.7f, 0.45f), 0.8f), 0.005f);
            pattern.traceLine = PatternLine(board, "TraceLine", Glow("CastPatternTrace", new Color(1f, 0.95f, 0.8f), 2.2f), 0.012f);

            pattern.titleLabel = Label(board, "Title", new Vector2(0, 345), new Vector2(620, 60), "", 40, TextAnchor.MiddleCenter);
            pattern.titleLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            pattern.promptLabel = Label(board, "Prompt", new Vector2(0, 262), new Vector2(600, 60), "", 24, TextAnchor.MiddleCenter);
            pattern.promptLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            Panel(board, "TimerBack", new Vector2(0, -292), new Vector2(460, 22), new Color(0f, 0f, 0f, 0.6f));
            RectTransform fillRt = Rect(board, "TimerFill", new Vector2(0, -292), new Vector2(452, 16));
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = UiSprite("BarFillMana");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.raycastTarget = false;
            pattern.timerFill = fill;
            Button(board, "Cancel", new Vector2(0, -345), new Vector2(240, 62), "Cancel", new Color(0.45f, 0.22f, 0.18f), target, nameof(CastPattern.CancelPattern));
            ApplyCombatFont(board);
            // Its own layer (Walkthrough: clickable, no collisions, not filmed), like the cutscene screen.
            foreach (Transform t in board.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = walk;
            pattern.board = board;
            pattern.boardUnits = units;

            pattern.glyphDrawTex = glyphTex;
            pattern.glyphColors = (Color[])GlyphColors.Clone();
            pattern.glyphEdgeColors = (Color[])GlyphEdgeColors.Clone();
            pattern.penU = (float[])GlyphPenData.U.Clone();
            pattern.penV = (float[])GlyphPenData.V.Clone();
            pattern.penJump = (int[])GlyphPenData.Jump.Clone();
            pattern.penSamples = GlyphPenData.Samples;

            // The wand: a dark wood shaft with a gold band and a gem tip in the school's colour, held anywhere.
            var wandGo = new GameObject("CastingWand");
            wandGo.transform.SetParent(root, false);
            int pickupLayer = LayerMask.NameToLayer("Pickup");
            Material wood = PMat("CastWandWood", new Color(0.28f, 0.18f, 0.12f), 1f);
            Material grip = PMat("CastWandGrip", new Color(0.14f, 0.09f, 0.07f), 1f);
            Material gold = PMat("CastWandGold", new Color(1f, 0.82f, 0.36f), 1f, new Color(0.5f, 0.38f, 0.1f));
            var shaft = Shape(wandGo.transform, "Shaft", CylMesh(), new Vector3(0, 0, 0.11f), new Vector3(0.016f, 0.13f, 0.016f), wood, false, Quaternion.Euler(90f, 0, 0));
            var handle = Shape(wandGo.transform, "Handle", CylMesh(), new Vector3(0, 0, -0.035f), new Vector3(0.026f, 0.055f, 0.026f), grip, false, Quaternion.Euler(90f, 0, 0));
            var band = Shape(wandGo.transform, "Band", CylMesh(), new Vector3(0, 0, 0.025f), new Vector3(0.03f, 0.008f, 0.03f), gold, false, Quaternion.Euler(90f, 0, 0));
            var pommel = Shape(wandGo.transform, "Pommel", GemMesh(), new Vector3(0, 0, -0.095f), new Vector3(0.03f, 0.03f, 0.03f), gold, false, Quaternion.Euler(90f, 0, 0));
            var gem = Shape(wandGo.transform, "Gem", GemMesh(), new Vector3(0, 0, 0.255f), new Vector3(0.028f, 0.05f, 0.028f), cores[0], false, Quaternion.Euler(90f, 0, 0));
            foreach (var go in new[] { shaft, handle, band, pommel, gem })
            {
                go.isStatic = false;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var tip = Child(wandGo.transform, "Tip", new Vector3(0, 0, 0.28f));
            var col = wandGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0, 0.09f);
            col.size = new Vector3(0.06f, 0.06f, 0.4f);
            var rb = wandGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var pickup = wandGo.AddComponent<VRCPickup>();
            pickup.InteractionText = "Casting wand";
            pickup.UseText = "";
            pickup.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.No;
            pickup.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
            pickup.allowManipulationWhenEquipped = true;
            pickup.DisallowTheft = true;
            pickup.proximity = 0.6f;
            var relay = UdonSharpUndo.AddComponent<PatternWand>(wandGo);
            relay.pattern = pattern;
            UdonSharpEditorUtility.CopyProxyToUdon(relay);
            if (pickupLayer >= 0) foreach (Transform t in wandGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = pickupLayer;
            pattern.wand = wandGo.transform;
            pattern.wandTip = tip;
            pattern.wandPickup = pickup;
            pattern.wandGem = gem.GetComponent<Renderer>();
            wandGo.SetActive(false);

            // Sparks off the tip while it's held.
            var sparks = CinePS(root, "WandTipSparks", SparkleMat("CastWandSparks", new Color(1f, 0.9f, 0.6f)), null, 0.25f, 0.5f, 0.05f, 0.3f, 0.02f, 0.05f, 0f, ParticleSystemShapeType.Sphere, 0.01f, 300);
            pattern.tipSparks = sparks;

            var sfx = holder.gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            pattern.sfx = sfx;
            pattern.grabSound = CastClip("Pattern_Grab");
            pattern.tickSound = CastClip("Pattern_Tick");
            pattern.missSound = CastClip("Pattern_Miss");
            pattern.doneSound = CastClip("Pattern_Done");
            pattern.failSound = CastClip("Pattern_Fail");
            boardGo.SetActive(false);
            return pattern;
        }

        private static AudioClip CastClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CastSoundDir + "/" + name + ".wav");
            if (clip == null) Debug.LogWarning("[WizardsVRC] Missing " + CastSoundDir + "/" + name + ".wav (run Tools/make_spell_cast_sounds.py).");
            return clip;
        }

        /// <summary>A material on the drawn-symbol shader for a quad of its own (copied again at runtime).</summary>
        private static Material GlyphQuadMat(string name)
        {
            var shader = Shader.Find("WizardsVRC/GlyphDraw");
            if (shader == null) return null;
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Reveal", 1.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A quad standing on the board (canvas units), facing you.</summary>
        private static Renderer BoardQuad(Transform board, string name, float z, float size, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(board, false);
            q.transform.localPosition = new Vector3(0, 0, z);
            q.transform.localScale = new Vector3(size, size, 1f);
            var r = q.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private static LineRenderer PatternLine(Transform parent, string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = mat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // ================================================================== the choreography rig

        private static SpellCinematic BuildSpellCinematicRig(Transform root, int walk, Texture2D[] glyphTex, Material[] cores, CutscenePlayer cutscenes)
        {
            var rig = Child(root, "SpellCinematic", Vector3.zero);
            var cine = UdonSharpUndo.AddComponent<SpellCinematic>(rig.gameObject);
            cine.cutscenePlayer = cutscenes;
            int looks = GlyphPenData.Labels.Length;
            cine.sparkMats = new Material[looks];
            cine.glowMats = new Material[looks];
            cine.ringMats = new Material[looks];
            cine.pillarMats = new Material[looks];
            cine.coreMats = cores;
            PrepareRingTexture();
            for (int k = 0; k < looks; k++)
            {
                string label = GlyphPenData.Labels[k];
                Color c = GlyphColors[k];
                cine.sparkMats[k] = AuraMat("SpellCineSpark" + label, Color.Lerp(c, Color.white, 0.25f), AuraSparkTex);
                cine.glowMats[k] = AuraMat("SpellCineGlow" + label, c, AuraGlowTex);
                cine.ringMats[k] = AuraMat("SpellCineRing" + label, Color.Lerp(c, Color.white, 0.2f), SpellRingTex);
                cine.pillarMats[k] = ShaftMat("SpellCinePillar" + label, c * 0.8f);
            }
            var sparks = new List<Renderer>();
            var glows = new List<Renderer>();
            var rings = new List<Renderer>();
            var pillars = new List<Renderer>();
            var coreR = new List<Renderer>();
            Material spark0 = cine.sparkMats[0], glow0 = cine.glowMats[0], ring0 = cine.ringMats[0], pillar0 = cine.pillarMats[0];

            // ---- wind-up
            var rune = Child(rig, "RuneCircle", Vector3.zero);
            Material runeMat = GlyphQuadMat("SpellCineRune");
            cine.runeRenderer = FlatQuad(rune, "Rune", 1f, runeMat, null);
            var runeRing = Child(rune, "RuneRing", new Vector3(0, 0.01f, 0));
            FlatQuad(runeRing, "Ring", 1.3f, ring0, rings);
            cine.runeCircle = rune;
            cine.runeRing = runeRing;
            rune.gameObject.SetActive(false);
            var aura = CinePS(rig, "ChargeAura", spark0, sparks, 0.45f, 0.55f, -4.6f, -3.8f, 0.08f, 0.2f, 0f, ParticleSystemShapeType.Sphere, 2.2f, 600);
            cine.chargeAura = aura;
            cine.castPillar = Pillar(rig, "CastPillar", pillar0, pillars, null, null);
            cine.gyroRings = new[] { Ring(rig, "GyroRingA", ring0, rings), Ring(rig, "GyroRingB", ring0, rings) };
            cine.portalBurst = CinePS(rig, "PortalBurst", spark0, sparks, 0.6f, 1.0f, 1.5f, 4.5f, 0.08f, 0.22f, -0.2f, ParticleSystemShapeType.Sphere, 0.5f, 500);

            // ---- release
            var release = CinePS(rig, "ReleaseBurst", glow0, glows, 0.3f, 0.5f, 3.5f, 7f, 0.12f, 0.35f, 0f, ParticleSystemShapeType.Cone, 0.08f, 300);
            var rshape = release.shape;
            rshape.angle = 16f;
            cine.releaseBurst = release;
            int heads = SpellCinematic.MAX_HEADS;
            cine.heads = new Transform[heads];
            cine.headShapes = new Renderer[heads * SpellCinematic.SHAPES];
            for (int i = 0; i < heads; i++)
            {
                var h = Child(rig, "Bolt" + i, Vector3.zero);
                var orb = Shape(h, "Orb", BlobMesh(), Vector3.zero, Vector3.one * 0.34f, cores[0], false, Quaternion.identity);
                var shard = Shape(h, "Shard", GemMesh(), Vector3.zero, new Vector3(0.17f, 0.55f, 0.17f), cores[0], false, Quaternion.Euler(90f, 0, 0));
                var disc = Shape(h, "Disc", CylMesh(), Vector3.zero, new Vector3(0.44f, 0.025f, 0.44f), cores[0], false, Quaternion.Euler(90f, 0, 0));
                GameObject[] shapes = { orb, shard, disc };
                for (int k = 0; k < shapes.Length; k++)
                {
                    shapes[k].isStatic = false;
                    var r = shapes[k].GetComponent<Renderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.enabled = k == 0;
                    coreR.Add(r);
                    cine.headShapes[i * SpellCinematic.SHAPES + k] = r;
                }
                // Trails: sparks, and a soft glow round the head.
                var trail = CinePS(h, "Trail", spark0, sparks, 0.3f, 0.5f, 0f, 0.4f, 0.06f, 0.16f, 0f, ParticleSystemShapeType.Sphere, 0.08f, 300);
                var tem = trail.emission;
                tem.rateOverDistance = 28f;
                var halo = CinePS(h, "Halo", glow0, glows, 0.12f, 0.2f, 0f, 0f, 0.5f, 0.7f, 0f, ParticleSystemShapeType.Sphere, 0.01f, 60);
                var hem = halo.emission;
                hem.rateOverTime = 40f;
                h.gameObject.SetActive(false);
                cine.heads[i] = h;
            }
            cine.beam = Pillar(rig, "Beam", pillar0, pillars, cores[0], coreR);
            cine.portals = new[] { Portal(rig, "PortalIn", ring0, glow0, rings, glows), Portal(rig, "PortalOut", ring0, glow0, rings, glows) };

            // ---- impact
            cine.impactBurst = CinePS(rig, "ImpactBurst", spark0, sparks, 0.4f, 0.8f, 2f, 6f, 0.1f, 0.3f, 0.2f, ParticleSystemShapeType.Sphere, 0.4f, 800);
            var shards = CinePS(rig, "ShardBurst", spark0, sparks, 0.8f, 1.4f, 4f, 8f, 0.06f, 0.14f, 1.2f, ParticleSystemShapeType.Sphere, 0.3f, 500);
            var sr = shards.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.lengthScale = 2.5f;
            sr.velocityScale = 0.04f;
            cine.shardBurst = shards;
            var flash = Shape(rig, "ImpactFlash", BlobMesh(), Vector3.zero, Vector3.one, cores[0], false, Quaternion.identity);
            flash.isStatic = false;
            flash.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreR.Add(flash.GetComponent<Renderer>());
            flash.SetActive(false);
            cine.impactFlash = flash.transform;
            cine.shockRings = new[] { Ring(rig, "ShockRingA", ring0, rings), Ring(rig, "ShockRingB", ring0, rings), Ring(rig, "ShockRingC", ring0, rings) };
            cine.impactPillar = Pillar(rig, "ImpactPillar", pillar0, pillars, null, null);
            cine.spikes = new Transform[8];
            for (int i = 0; i < 8; i++)
            {
                var s = Child(rig, "Spike" + i, Vector3.zero);
                var g = Shape(s, "Shape", GemMesh(), new Vector3(0, 0.5f, 0), Vector3.one, cores[0], false, Quaternion.identity);
                g.isStatic = false;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                coreR.Add(g.GetComponent<Renderer>());
                s.gameObject.SetActive(false);
                cine.spikes[i] = s;
            }
            var stamp = Child(rig, "SigilStamp", Vector3.zero);
            cine.sigilRenderer = FlatQuad(stamp, "Rune", 1f, GlyphQuadMat("SpellCineSigil"), null);
            cine.sigilStamp = stamp;
            stamp.gameObject.SetActive(false);
            cine.embers = CinePS(rig, "Embers", spark0, sparks, 1.0f, 1.8f, 0.4f, 1.4f, 0.05f, 0.13f, -0.3f, ParticleSystemShapeType.Sphere, 0.8f, 600);
            var lightGo = Child(rig, "SpellLight", Vector3.zero).gameObject;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.range = 6f;
            light.intensity = 0f;
            lightGo.SetActive(false);
            cine.fxLight = light;

            cine.sparkRenderers = sparks.ToArray();
            cine.glowRenderers = glows.ToArray();
            cine.ringRenderers = rings.ToArray();
            cine.pillarRenderers = pillars.ToArray();
            cine.coreRenderers = coreR.ToArray();
            cine.glyphDrawTex = glyphTex;
            cine.glyphColors = (Color[])GlyphColors.Clone();
            cine.glyphEdgeColors = (Color[])GlyphEdgeColors.Clone();

            // ---- the caster's fade: an inside-out sphere on their head
            var fade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fade.name = "SpellFade";
            fade.transform.SetParent(root, false);
            fade.transform.localScale = Vector3.one * 0.34f;
            Object.DestroyImmediate(fade.GetComponent<Collider>());
            var fr = fade.GetComponent<Renderer>();
            fr.sharedMaterial = SpellFadeMat();
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = false;
            fade.layer = walk;
            fade.SetActive(false);
            cine.fadeSphere = fade.transform;
            cine.fadeRenderer = fr;

            var sfx = rig.gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0.75f;
            sfx.minDistance = 6f;
            sfx.maxDistance = 50f;
            sfx.rolloffMode = AudioRolloffMode.Linear;
            cine.sfx = sfx;
            cine.whooshSound = CastClip("Spell_Whoosh");
            cine.releaseSound = CastClip("Spell_Release");
            cine.impactSound = CastClip("Spell_Impact");
            cine.impactBigSound = CastClip("Spell_ImpactBig");

            // Nothing in the rig is static or casts shadows.
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            return cine;
        }

        private static Material SpellFadeMat()
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/SpellFade.mat";
            var shader = Shader.Find("WizardsVRC/SpellFade");
            if (shader == null) Debug.LogError("[WizardsVRC] Shader WizardsVRC/SpellFade is missing; spell cinematics won't fade.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader ?? Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (shader != null) mat.shader = shader;
            mat.SetFloat("_Fade", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The ring texture's import settings (soft, clamped).</summary>
        private static void PrepareRingTexture()
        {
            var imp = AssetImporter.GetAtPath(SpellRingTex) as TextureImporter;
            if (imp == null) { Debug.LogWarning("[WizardsVRC] Missing " + SpellRingTex + " (run Tools/make_spell_ring.py)."); return; }
            if (imp.wrapMode != TextureWrapMode.Clamp || !imp.alphaIsTransparency)
            {
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
        }

        /// <summary>A flat quad (lying in its parent's XZ plane, facing up), 'size' across.</summary>
        private static Renderer FlatQuad(Transform parent, string name, float size, Material mat, List<Renderer> list)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(size, size, 1f);
            var r = q.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (list != null) list.Add(r);
            return r;
        }

        /// <summary>A ring 1 m across (its pivot scaled at runtime), flat in the pivot's XZ plane.</summary>
        private static Transform Ring(Transform parent, string name, Material mat, List<Renderer> rings)
        {
            var pivot = Child(parent, name, Vector3.zero);
            FlatQuad(pivot, "Ring", 1f, mat, rings);
            pivot.gameObject.SetActive(false);
            return pivot;
        }

        /// <summary>A ring with a glowing middle: something comes through it along the pivot's +Y.</summary>
        private static Transform Portal(Transform parent, string name, Material ring, Material glow, List<Renderer> rings, List<Renderer> glows)
        {
            var pivot = Child(parent, name, Vector3.zero);
            FlatQuad(pivot, "Ring", 1f, ring, rings);
            FlatQuad(pivot, "Middle", 0.8f, glow, glows);
            pivot.gameObject.SetActive(false);
            return pivot;
        }

        /// <summary>A column of light 1 m wide and 1 m up its pivot's +Y (soft crossed planes), with a solid core if given.</summary>
        private static Transform Pillar(Transform parent, string name, Material shaft, List<Renderer> pillars, Material core, List<Renderer> cores)
        {
            var pivot = Child(parent, name, Vector3.zero);
            var go = LightShaft(pivot, "Shaft", Vector3.zero, Vector3.up, 1f, shaft);
            go.isStatic = false;
            pillars.Add(go.GetComponent<Renderer>());
            if (core != null)
            {
                var c = Shape(pivot, "Core", CylMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.22f, 0.5f, 0.22f), core, false, Quaternion.identity);
                c.isStatic = false;
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (cores != null) cores.Add(c.GetComponent<Renderer>());
            }
            pivot.gameObject.SetActive(false);
            return pivot;
        }

        /// <summary>
        /// A particle system that only ever emits when told (Emit; it keeps running with no rate of its own), in world space
        /// and scaled with its transform: lifetime, speed and size ranges, gravity, its emission shape.
        /// </summary>
        private static ParticleSystem CinePS(Transform parent, string name, Material mat, List<Renderer> list, float life0, float life1,
            float speed0, float speed1, float size0, float size1, float gravity, ParticleSystemShapeType shapeType, float radius, int max)
        {
            var go = Child(parent, name, Vector3.zero).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed0, speed1);
            main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = max;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = shapeType;
            shape.radius = radius;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (list != null) list.Add(r);
            return ps;
        }
    }
}
