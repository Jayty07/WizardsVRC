using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The title screen (TitleScreen), a stage of its own far from everything else: a dais of starlit stone floating over a
    /// sea of moonlit clouds under the aurora (a PSXSkyline dome painted by Tools/make_title_art.py), the WIZARDS VRC
    /// wordmark hanging ahead and gently bobbing with a violet glow and twinkling sparkles, the seven schools' symbols in an
    /// arc drawing themselves stroke by stroke (each in its own style, the GlyphDraw shader) then fading and drawing again,
    /// Starfall Isle's castle on its floating island beyond with its crystal turning, two smaller isles, a rune circle
    /// turning under your feet, shooting stars and golden motes, and the menu: Begin, and the spellbook loading line.
    /// Everything moves by itself (Animators and particles), so it costs no script time.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 TitleOrigin = new Vector3(2600f, 1400f, 2600f);
        private const string TitleTexDir = TexturesDir + "/Title";
        private const string TitleAnimDir = "Assets/WizardsVRC/Animations/Title";

        private static TitleScreen BuildTitleScreen(Transform systems, IntroFlow flow, SaveManager saveManager, TeleportHelper teleporter, Transform studySpot)
        {
            var title = UdonSharpUndo.AddComponent<TitleScreen>(Child(systems, "TitleScreen", Vector3.zero).gameObject);
            title.introFlow = flow;
            title.awakening = flow != null ? flow.awakening : null;
            title.saveManager = saveManager;
            title.teleporter = teleporter;
            title.studySpot = studySpot;
            if (flow != null) flow.titleScreen = title;
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(title);
            Directory.CreateDirectory(TitleAnimDir);

            var stage = new GameObject("TitleStage").transform;
            stage.position = TitleOrigin;
            title.stageRoot = stage.gameObject;
            var spot = Child(stage, "TitleSpot", new Vector3(0, 0.05f, 0));
            title.titleSpot = spot;

            TitleSky(stage);
            TitleDais(stage);
            TitleGlyphs(stage);
            TitleIsland(stage);
            TitleLogo(stage);
            TitleParticles(stage);
            TitleMenu(stage, title, target);
            // A night breeze, quiet, and two lights for the stage (the PSX shaders light by the nearest few).
            AmbientBed(stage, "Amb_NightDusk", 0.25f);
            PointLight(stage, new Vector3(0, 4.5f, 6f), new Color(0.65f, 0.5f, 1f), 26f, 1.6f);
            PointLight(stage, new Vector3(0, 1.5f, 0.5f), new Color(1f, 0.82f, 0.5f), 9f, 1.2f);

            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            ApplyCombatFont(stage);
            ExtraBehaviours.Add(title);
            return title;
        }

        // ------------------------------------------------------------------ the sky

        private static void TitleSky(Transform stage)
        {
            var dome = new GameObject("TitleSky");
            dome.transform.SetParent(stage, false);
            dome.transform.localPosition = new Vector3(0, 1.6f, 0);
            dome.transform.localScale = Vector3.one * 200f;
            dome.AddComponent<MeshFilter>().sharedMesh = InsideSphereMesh();
            var r = dome.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var shader = Shader.Find("WizardsVRC/PSXSkyline");
            string texPath = TitleTexDir + "/TitleSkyline.png";
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp == null) Debug.LogWarning("[WizardsVRC] Missing " + texPath + " (run Tools/make_title_art.py).");
            else if (imp.filterMode != FilterMode.Point || imp.mipmapEnabled || imp.wrapModeU != TextureWrapMode.Repeat || imp.wrapModeV != TextureWrapMode.Clamp ||
                     !imp.alphaIsTransparency || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None ||
                     imp.maxTextureSize < 2048)
            {
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.wrapModeU = TextureWrapMode.Repeat;
                imp.wrapModeV = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            if (shader != null)
            {
                Directory.CreateDirectory(MaterialsDir);
                string path = MaterialsDir + "/TitleSky.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetColor("_TopColor", new Color(0.04f, 0.03f, 0.13f));
                mat.SetColor("_HorizonColor", new Color(0.46f, 0.3f, 0.66f));
                mat.SetColor("_BottomColor", new Color(0.22f, 0.15f, 0.4f));
                mat.SetFloat("_Sharpness", 1.5f);
                mat.SetTexture("_Skyline", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.SetFloat("_Low", -12f);
                mat.SetFloat("_High", 40f);
                mat.SetFloat("_Rotation", 0f);
                mat.SetFloat("_ColorDepth", 32f);
                EditorUtility.SetDirty(mat);
                r.sharedMaterial = mat;
            }
            else r.sharedMaterial = UnlitMat("TitleSkyPlain", new Color(0.06f, 0.04f, 0.16f));

            // Stars scattered high on a shell inside the dome (none below the cloud sea).
            var stars = new GameObject("TitleStars");
            stars.transform.SetParent(stage, false);
            stars.transform.localPosition = new Vector3(0, 1.6f, 0);
            stars.transform.localScale = Vector3.one * 3f;
            stars.AddComponent<MeshFilter>().sharedMesh = StarShellMesh();
            var sr = stars.AddComponent<MeshRenderer>();
            sr.sharedMaterial = UnlitMat("TitleStar", new Color(0.9f, 0.88f, 1f));
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The stars turn very slowly.
            var clip = NewClip("TitleStarsTurn", true);
            Keys(clip, "", "roty", 0f, 0f, 240f, 360f);
            Animate(stars, "TitleStars", clip);
        }

        // ------------------------------------------------------------------ the dais you stand on

        private static void TitleDais(Transform stage)
        {
            Material stone = PMat("TitleDaisStone", new Color(0.36f, 0.32f, 0.46f), 1f);
            Material rock = PMat("TitleDaisRock", new Color(0.22f, 0.18f, 0.3f), 1f);
            Material gold = PMat("TitleDaisGold", new Color(0.95f, 0.75f, 0.3f), 1f, new Color(0.35f, 0.25f, 0.05f));
            var dais = Child(stage, "Dais", Vector3.zero);
            Shape(dais, "Top", CylMesh(), new Vector3(0, -0.2f, 0), new Vector3(6.4f, 0.2f, 6.4f), stone, false, Quaternion.identity);
            Shape(dais, "Rim", CylMesh(), new Vector3(0, -0.12f, 0), new Vector3(6.6f, 0.06f, 6.6f), gold, false, Quaternion.identity);
            Shape(dais, "Underside", ConeMesh(), new Vector3(0, -2.6f, 0), new Vector3(6.2f, 4.4f, 6.2f), rock, false, Quaternion.Euler(180f, 0, 0));
            var floor = Child(dais, "Floor", new Vector3(0, -0.2f, 0)).gameObject.AddComponent<BoxCollider>();
            floor.size = new Vector3(12f, 0.4f, 12f);
            // A low invisible rail all round (you can't step off the edge).
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                var wall = Child(dais, "Rail" + k, new Vector3(Mathf.Sin(a) * 3.5f, 1f, Mathf.Cos(a) * 3.5f));
                wall.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
                wall.gameObject.AddComponent<BoxCollider>().size = new Vector3(3f, 2f, 0.2f);
            }
            // The rune circle, turning slowly under your feet.
            PrepareRingTexture();
            var ringPivot = Child(dais, "RunePivot", new Vector3(0, 0.02f, 0));
            FlatQuad(ringPivot, "Runes", 5.6f, AuraMat("TitleRuneRing", new Color(0.7f, 0.55f, 1f), SpellRingTex), null);
            var inner = Child(ringPivot, "InnerPivot", Vector3.zero);
            FlatQuad(inner, "RunesInner", 3.2f, AuraMat("TitleRuneRingInner", new Color(1f, 0.82f, 0.45f), SpellRingTex), null);
            var clip = NewClip("TitleRunes", true);
            Keys(clip, "", "roty", 0f, 0f, 40f, 360f);
            Keys(clip, "InnerPivot", "roty", 0f, 0f, 40f, -720f);
            Animate(ringPivot.gameObject, "TitleRunes", clip);
        }

        // ------------------------------------------------------------------ the seven schools' symbols

        private static void TitleGlyphs(Transform stage)
        {
            var arc = Child(stage, "SchoolGlyphs", Vector3.zero);
            Material baseMat = GlyphDrawMat();
            if (baseMat == null) return;
            const float len = 14f;
            var clip = NewClip("TitleGlyphs", true);
            int n = SchoolLabels.Length;
            for (int k = 0; k < n; k++)
            {
                // An arc ahead of you from left to right, the middle ones higher, round the logo.
                float a = Mathf.Lerp(-68f, 68f, k / (float)(n - 1)) * Mathf.Deg2Rad;
                float lift = Mathf.Cos(a * 1.4f) * 0.9f;
                var pos = new Vector3(Mathf.Sin(a) * 7.2f, 1.5f + lift, Mathf.Cos(a) * 7.2f + 1.2f);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Glyph" + k;
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(arc, false);
                q.transform.localPosition = pos;
                q.transform.localRotation = Quaternion.LookRotation(new Vector3(pos.x, 0f, pos.z - 0.5f));
                q.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
                var r = q.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                string path = MaterialsDir + "/TitleGlyph" + SchoolLabels[k] + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = baseMat.shader;
                mat.SetTexture("_MainTex", DataTexture(TexturesDir + "/CastDraw/GlyphDraw" + GlyphPenData.Labels[k] + ".png"));
                mat.SetColor("_Color", GlyphColors[k]);
                mat.SetColor("_EdgeColor", GlyphEdgeColors[k]);
                mat.SetFloat("_Style", k + 1f);
                mat.SetFloat("_Reveal", 1.05f);
                mat.SetFloat("_Fade", 1f);
                mat.SetFloat("_Flare", 0f);
                mat.SetFloat("_Intensity", 1.5f);
                EditorUtility.SetDirty(mat);
                r.sharedMaterial = mat;

                // Each draws itself in turn, flares as it finishes, holds, and fades before they all draw again.
                float t0 = 0.4f + k * 0.45f;
                string p = q.name;
                MatKeys(clip, p, "_Reveal", 0f, 0f, t0, 0f, t0 + 1.6f, 1.05f, len, 1.05f);
                MatKeys(clip, p, "_Flare", 0f, 0f, t0 + 1.4f, 0f, t0 + 1.7f, 1.2f, t0 + 2.4f, 0f, len, 0f);
                MatKeys(clip, p, "_Fade", 0f, 1f, len - 1.4f, 1f, len - 0.3f, 0f, len, 0f);
                // and bobs gently, out of step with its neighbours
                Wave(clip, p, "posy", len, 2, pos.y, 0.12f, k * 0.17f);
            }
            Animate(arc.gameObject, "TitleGlyphs", clip);
        }

        /// <summary>Keys on one of a renderer's material properties (path relative to the Animator).</summary>
        private static void MatKeys(AnimationClip clip, string path, string prop, params float[] tv)
        {
            var keys = new Keyframe[tv.Length / 2];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(tv[i * 2], tv[i * 2 + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(MeshRenderer), "material." + prop), curve);
        }

        // ------------------------------------------------------------------ Starfall Isle, floating beyond

        private static void TitleIsland(Transform stage)
        {
            Material rock = PMat("TitleIsleRock", new Color(0.3f, 0.24f, 0.36f), 1f);
            Material grass = PMat("TitleIsleGrass", new Color(0.28f, 0.55f, 0.36f), 1f);
            Material wall = PMat("TitleIsleWall", new Color(0.7f, 0.66f, 0.78f), 1f);
            Material roof = PMat("TitleIsleRoof", new Color(0.45f, 0.25f, 0.82f), 1f);
            Material window = PMat("TitleIsleWindow", new Color(1f, 0.85f, 0.5f), 1f, new Color(1f, 0.75f, 0.35f) * 1.5f);
            Material crystal = PMat("TitleIsleCrystal", new Color(0.55f, 0.85f, 1f), 1f, new Color(0.4f, 0.75f, 1f) * 2f);
            Material falls = PMat("TitleIsleFalls", new Color(0.7f, 0.85f, 1f), 1f, new Color(0.4f, 0.6f, 0.9f));

            var isle = Child(stage, "StarfallIsle", new Vector3(0f, -5f, 44f));
            Shape(isle, "Rock", ConeMesh(), new Vector3(0, -6f, 0), new Vector3(26f, 16f, 22f), rock, false, Quaternion.Euler(180f, 0, 0));
            Shape(isle, "Grass", CylMesh(), new Vector3(0, 0.4f, 0), new Vector3(26.5f, 0.6f, 22.5f), grass, false, Quaternion.identity);
            // The keep: a great tower, four round towers, curtain walls, lit windows, purple roofs, the crystal on top.
            Shape(isle, "Keep", CylMesh(), new Vector3(0, 7f, 2f), new Vector3(5f, 7f, 5f), wall, false, Quaternion.identity);
            Shape(isle, "KeepRoof", ConeMesh(), new Vector3(0, 16.5f, 2f), new Vector3(6.4f, 5f, 6.4f), roof, false, Quaternion.identity);
            float[] tx = { -7f, 7f, -6f, 6f };
            float[] tz = { -3f, -3f, 7f, 7f };
            for (int k = 0; k < 4; k++)
            {
                float h = k < 2 ? 4.5f : 5.5f;
                Shape(isle, "Tower" + k, CylMesh(), new Vector3(tx[k], h, tz[k]), new Vector3(2.6f, h, 2.6f), wall, false, Quaternion.identity);
                Shape(isle, "TowerRoof" + k, ConeMesh(), new Vector3(tx[k], h * 2f + 1.8f, tz[k]), new Vector3(3.4f, 3.6f, 3.4f), roof, false, Quaternion.identity);
                Block(isle, "Window" + k, new Vector3(tx[k], h * 1.4f, tz[k] - 1.31f), new Vector3(0.5f, 0.9f, 0.1f), window);
            }
            Block(isle, "WallFront", new Vector3(0, 2.2f, -3f), new Vector3(14f, 4.4f, 1f), wall);
            Block(isle, "WallLeft", new Vector3(-6.5f, 2.2f, 2f), new Vector3(1f, 4.4f, 10f), wall);
            Block(isle, "WallRight", new Vector3(6.5f, 2.2f, 2f), new Vector3(1f, 4.4f, 10f), wall);
            Block(isle, "Gate", new Vector3(0, 1.5f, -3.55f), new Vector3(2.4f, 3f, 0.2f), PMat("TitleIsleGate", new Color(0.25f, 0.15f, 0.1f), 1f));
            for (int k = 0; k < 3; k++) Block(isle, "KeepWindow" + k, new Vector3(-1.4f + k * 1.4f, 9f + (k % 2) * 1.5f, -0.52f), new Vector3(0.5f, 1f, 0.1f), window);
            Block(isle, "Falls", new Vector3(-9f, -5f, -6f), new Vector3(1.4f, 10f, 0.4f), falls);
            var crystalPivot = Child(isle, "CrystalPivot", new Vector3(0, 23.5f, 2f));
            Shape(crystalPivot, "Crystal", GemMesh(), Vector3.zero, new Vector3(2f, 3.6f, 2f), crystal, false, Quaternion.identity);
            Shape(crystalPivot, "Shard0", GemMesh(), new Vector3(2.2f, 0.4f, 0), new Vector3(0.5f, 0.9f, 0.5f), crystal, false, Quaternion.identity);
            Shape(crystalPivot, "Shard1", GemMesh(), new Vector3(-2.2f, -0.3f, 0), new Vector3(0.45f, 0.8f, 0.45f), crystal, false, Quaternion.identity);
            PointLight(crystalPivot, Vector3.zero, new Color(0.5f, 0.8f, 1f), 30f, 2f);

            var clip = NewClip("TitleIsle", true);
            Wave(clip, "", "posy", 10f, 1, -5f, 0.45f);
            Keys(clip, "CrystalPivot", "roty", 0f, 0f, 10f, 360f);
            Wave(clip, "CrystalPivot", "posy", 10f, 2, 23.5f, 0.3f);
            Animate(isle.gameObject, "TitleIsle", clip);

            // Two little isles off to the sides: a ruined arch on one, a lone tree on the other.
            Material bark = PMat("TitleIsleBark", new Color(0.35f, 0.22f, 0.15f), 1f);
            Material leaf = PMat("TitleIsleLeaf", new Color(0.3f, 0.62f, 0.4f), 1f);
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                var little = Child(stage, s == 0 ? "IsleLeft" : "IsleRight", new Vector3(side * 26f, s == 0 ? 2f : -1f, 30f + s * 6f));
                Shape(little, "Rock", ConeMesh(), new Vector3(0, -3f, 0), new Vector3(8f, 7f, 7f), rock, false, Quaternion.Euler(180f, 0, 0));
                Shape(little, "Grass", CylMesh(), new Vector3(0, 0.25f, 0), new Vector3(8.2f, 0.4f, 7.2f), grass, false, Quaternion.identity);
                if (s == 0)
                {
                    Block(little, "PillarL", new Vector3(-1.4f, 2f, 0), new Vector3(0.8f, 4f, 0.8f), wall);
                    Block(little, "PillarR", new Vector3(1.4f, 1.4f, 0), new Vector3(0.8f, 2.8f, 0.8f), wall);
                    Block(little, "Lintel", new Vector3(-0.6f, 4.2f, 0), new Vector3(2.6f, 0.6f, 0.9f), wall, Quaternion.Euler(0, 0, -8f));
                }
                else
                {
                    Shape(little, "Trunk", CylMesh(), new Vector3(0, 2f, 0), new Vector3(0.6f, 2f, 0.6f), bark, false, Quaternion.identity);
                    Shape(little, "Crown", BlobMesh(), new Vector3(0, 5f, 0), new Vector3(4f, 3f, 4f), leaf, false, Quaternion.identity);
                }
                var lc = NewClip(little.name, true);
                Wave(lc, "", "posy", 7f + s * 2f, 1, little.localPosition.y, 0.6f, s * 0.4f);
                Animate(little.gameObject, "Title" + little.name, lc);
            }
        }

        // ------------------------------------------------------------------ the wordmark

        private static void TitleLogo(Transform stage)
        {
            var bob = Child(stage, "LogoBob", new Vector3(0, 3.5f, 8.5f));
            var go = new GameObject("TitleLogo", typeof(RectTransform));
            go.transform.SetParent(bob, false);
            MakeWorldCanvas(go, new Vector2(2000, 1100), Vector3.zero, 0.0042f);
            var rt = (RectTransform)go.transform;
            // A violet glow behind it, breathing.
            var glow = Panel(rt, "Glow", new Vector2(0, 40), new Vector2(2100, 1200), new Color(0.55f, 0.35f, 1f, 0.45f));
            var glowImg = glow.GetComponent<Image>();
            glowImg.sprite = ShopSprite("Glow");
            glowImg.raycastTarget = false;
            var logoGo = new GameObject("Wordmark", typeof(RectTransform));
            logoGo.transform.SetParent(rt, false);
            var lrt = (RectTransform)logoGo.transform;
            lrt.sizeDelta = new Vector2(1536, 768);
            lrt.anchoredPosition = new Vector2(0, 60);
            var raw = logoGo.AddComponent<RawImage>();
            raw.raycastTarget = false;
            string logoPath = TitleTexDir + "/TitleLogo.png";
            var imp = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            if (imp == null) Debug.LogWarning("[WizardsVRC] Missing " + logoPath + " (run Tools/make_title_art.py).");
            else if (imp.filterMode != FilterMode.Point || imp.mipmapEnabled || !imp.alphaIsTransparency || imp.textureCompression != TextureImporterCompression.Uncompressed)
            {
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(logoPath);
            Text tag = Label(rt, "Tagline", new Vector2(0, -400), new Vector2(1900, 110), "Seven schools. One spellbook. Your story.", 64, TextAnchor.MiddleCenter);
            tag.color = new Color(0.85f, 0.82f, 1f);
            tag.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.05f, 0.2f, 0.9f);

            // Sparkles winking in turn round the wordmark.
            var rng = new System.Random(3);
            var sparkles = new RectTransform[12];
            Sprite sp = ShopSprite("Sparkle");
            for (int k = 0; k < sparkles.Length; k++)
            {
                var s = Panel(rt, "Sparkle" + k, new Vector2(rng.Next(-900, 900), rng.Next(-260, 460)), new Vector2(70, 70), k % 3 == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(0.85f, 0.9f, 1f));
                var si = s.GetComponent<Image>();
                si.sprite = sp;
                si.raycastTarget = false;
                sparkles[k] = s;
            }

            const float len = 6f;
            var clip = NewClip("TitleLogo", true);
            Wave(clip, "", "posy", len, 1, 3.5f, 0.14f);
            Wave(clip, "TitleLogo/Wordmark", "scalex", len, 2, 1f, 0.018f);
            Wave(clip, "TitleLogo/Wordmark", "scaley", len, 2, 1f, 0.018f);
            Wave(clip, "TitleLogo/Glow", "scalex", len, 1, 1f, 0.06f, 0.25f);
            Wave(clip, "TitleLogo/Glow", "scaley", len, 1, 1f, 0.06f, 0.25f);
            for (int k = 0; k < sparkles.Length; k++)
            {
                string p = "TitleLogo/" + sparkles[k].name;
                float t0 = (k * 0.53f) % len;
                float a = t0, b = Mathf.Min(len, t0 + 0.35f), c = Mathf.Min(len, t0 + 0.7f);
                foreach (string prop in new[] { "scalex", "scaley" }) Keys(clip, p, prop, 0f, 0.01f, a, 0.01f, b, 1f, c, 0.01f, len, 0.01f);
                Keys(clip, p, "rotz", 0f, 0f, a, 0f, c, 45f, len, 45f);
            }
            Animate(bob.gameObject, "TitleLogo", clip);
        }

        // ------------------------------------------------------------------ shooting stars and motes

        private static void TitleParticles(Transform stage)
        {
            Material streak = Mat("TitleShootingStar", new Color(0.85f, 0.9f, 1f));
            streak.EnableKeyword("_EMISSION");
            streak.SetColor("_EmissionColor", new Color(0.85f, 0.9f, 1f) * 2f);
            var stars = Child(stage, "ShootingStars", new Vector3(10f, 34f, 40f)).gameObject;
            stars.transform.localRotation = Quaternion.LookRotation(new Vector3(-1f, -0.32f, 0.15f));
            var ps = stars.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(28f, 44f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0.7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60f, 14f, 1f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var pr = stars.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = streak;
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.05f;
            pr.lengthScale = 4f;

            Material mote = Mat("TitleMote", new Color(1f, 0.85f, 0.5f));
            mote.EnableKeyword("_EMISSION");
            mote.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.45f) * 1.5f);
            var motes = Child(stage, "Motes", new Vector3(0, 2f, 3f)).gameObject;
            var mps = motes.AddComponent<ParticleSystem>();
            var mm = mps.main;
            mm.loop = true;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            mm.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            mm.gravityModifier = -0.004f;
            mm.maxParticles = 140;
            mm.prewarm = true;
            var mem = mps.emission;
            mem.rateOverTime = 14f;
            var msh = mps.shape;
            msh.shapeType = ParticleSystemShapeType.Sphere;
            msh.radius = 8f;
            var mcol = mps.colorOverLifetime;
            mcol.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            mcol.color = g;
            var mr = motes.GetComponent<ParticleSystemRenderer>();
            mr.sharedMaterial = mote;
            mr.renderMode = ParticleSystemRenderMode.Billboard;
        }

        // ------------------------------------------------------------------ the menu

        private static void TitleMenu(Transform stage, TitleScreen title, UdonBehaviour target)
        {
            var go = new GameObject("TitleMenu", typeof(RectTransform));
            go.transform.SetParent(stage, false);
            MakeWorldCanvas(go, new Vector2(1200, 560), new Vector3(0, 1.15f, 2.3f), 0.0011f);
            go.transform.localRotation = Quaternion.Euler(18f, 0, 0);
            var rt = (RectTransform)go.transform;
            // Begin: its glow, a dark border and the button itself, shown together once your saves are in.
            var group = Rect(rt, "BeginGroup", new Vector2(0, 90), new Vector2(760, 300));
            var glow = Panel(group, "BeginGlow", Vector2.zero, new Vector2(760, 300), new Color(1f, 0.8f, 0.4f, 0.4f));
            var glowImg = glow.GetComponent<Image>();
            glowImg.sprite = ShopSprite("Glow");
            glowImg.raycastTarget = false;
            title.beginGlow = glowImg;
            Panel(group, "BeginBorder", Vector2.zero, new Vector2(536, 146), new Color(0.35f, 0.2f, 0.05f, 1f)).GetComponent<Image>().raycastTarget = false;
            GameObject begin = Button(group, "Begin", Vector2.zero, new Vector2(520, 130), "Begin", new Color(0.82f, 0.6f, 0.18f), target, nameof(TitleScreen.Begin));
            Text bl = begin.GetComponentInChildren<Text>();
            bl.fontSize = 72;
            bl.color = new Color(1f, 0.97f, 0.85f);
            bl.gameObject.AddComponent<Outline>().effectColor = new Color(0.3f, 0.15f, 0.02f);
            title.beginButton = group.gameObject;
            title.beginLabel = bl;
            title.statusLabel = Label(rt, "Status", new Vector2(0, 90), new Vector2(1100, 80), "Opening your spellbook", 44, TextAnchor.MiddleCenter);
            title.statusLabel.color = new Color(0.85f, 0.82f, 1f);
            Text hint = Label(rt, "Hint", new Vector2(0, -60), new Vector2(1150, 60), "Point at Begin and pull the trigger (or click it).", 30, TextAnchor.MiddleCenter);
            hint.color = new Color(0.75f, 0.72f, 0.9f);
            Text credit = Label(rt, "Credit", new Vector2(0, -200), new Vector2(1150, 50), "WizardsVRC  -  a world of spells for VRChat", 26, TextAnchor.MiddleCenter);
            credit.color = new Color(0.6f, 0.56f, 0.75f);
            foreach (var t in new[] { title.statusLabel, hint, credit }) t.gameObject.AddComponent<Outline>().effectColor = new Color(0.08f, 0.04f, 0.15f, 0.9f);
            group.gameObject.SetActive(false);
            // Clickable, but never in your way.
            int layer = LayerMask.NameToLayer("Walkthrough");
            if (layer < 0) layer = 17;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Plays a looping clip on an object (a one-state controller in Animations/Title).</summary>
        private static void Animate(GameObject go, string name, AnimationClip clip)
        {
            string path = TitleAnimDir + "/" + name + ".controller";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            ctrl.layers[0].stateMachine.AddState("Play").motion = clip;
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }
}
