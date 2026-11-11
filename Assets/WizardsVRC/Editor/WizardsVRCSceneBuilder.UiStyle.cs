using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The interface's look: ornate 9-slice panels, bevelled buttons, gold-framed bars (Textures/UI, made by
    /// Tools/make_ui_textures.py), the player HUD with health and mana bars that show their numbers, the level-up
    /// effect, and a pass at the end of the build that dresses every plain panel and button in the world's menus.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string UiTexDir = TexturesDir + "/UI";

        /// <summary>9-slice borders (left, bottom, right, top, in pixels) of the UI textures that stretch.</summary>
        private static readonly Dictionary<string, Vector4> UiBorders = new Dictionary<string, Vector4>
        {
            { "PanelOrnate", new Vector4(16, 16, 16, 16) },
            { "PanelParchment", new Vector4(16, 16, 16, 16) },
            { "Tooltip", new Vector4(16, 16, 16, 16) },
            { "Button", new Vector4(6, 6, 6, 6) },
            { "BarFrame", new Vector4(10, 8, 10, 8) },
            { "SignBoard", new Vector4(16, 14, 16, 14) },
        };

        /// <summary>A UI sprite from Textures/UI, point filtered, with its 9-slice border when it has one.</summary>
        private static Sprite UiSprite(string name)
        {
            string path = UiTexDir + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[WizardsVRC] Missing " + path + " (run Tools/make_ui_textures.py)"); return null; }
            Vector4 border;
            if (!UiBorders.TryGetValue(name, out border)) border = Vector4.zero;
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Puts a sprite on an image: sliced when it has a border, its pixels drawn 'pixelScale' canvas units big.</summary>
        private static void SetUiSprite(Image img, Sprite s, float pixelScale)
        {
            if (img == null || s == null) return;
            img.sprite = s;
            img.type = s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, pixelScale);
        }

        private static Image UiImage(Transform parent, string name, Vector2 pos, Vector2 size, string sprite, Color tint, float pixelScale)
        {
            RectTransform rt = Panel(parent, name, pos, size, tint);
            var img = rt.GetComponent<Image>();
            SetUiSprite(img, UiSprite(sprite), pixelScale);
            return img;
        }

        /// <summary>
        /// A gold-framed bar: a dark well, a left-pivoted fill (scale its X), an optional trail behind it, and centred
        /// text in the pixel font. Returns the fill; trail and label come back through out.
        /// </summary>
        private static RectTransform UiBar(Transform parent, string name, Vector2 pos, Vector2 size, string fillSprite, bool trail,
            out RectTransform trailRt, out Text label, int fontSize)
        {
            Image frame = UiImage(parent, name, pos, size, "BarFrame", Color.white, 2f);
            var frt = (RectTransform)frame.transform;
            float inset = 8f;
            Vector2 inner = new Vector2(size.x - inset * 2f, size.y - inset * 2f);
            trailRt = null;
            if (trail)
            {
                trailRt = Panel(frt, "Trail", Vector2.zero, inner, new Color(1f, 0.92f, 0.75f, 0.85f));
                trailRt.anchorMin = trailRt.anchorMax = new Vector2(0f, 0.5f);
                trailRt.pivot = new Vector2(0f, 0.5f);
                trailRt.anchoredPosition = new Vector2(inset, 0f);
            }
            Image fill = UiImage(frt, "Fill", Vector2.zero, inner, fillSprite, Color.white, 2f);
            fill.type = Image.Type.Tiled;
            var fill_rt = (RectTransform)fill.transform;
            fill_rt.anchorMin = fill_rt.anchorMax = new Vector2(0f, 0.5f);
            fill_rt.pivot = new Vector2(0f, 0.5f);
            fill_rt.anchoredPosition = new Vector2(inset, 0f);
            label = Label(frt, "Value", new Vector2(0, 1), size, "", fontSize, TextAnchor.MiddleCenter);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return fill_rt;
        }

        // ================================================================== flourishes

        /// <summary>A title on a carved gold-rimmed board (SignBoard, 9-sliced), tinted; the text in gold with an outline.</summary>
        private static Text TitlePlate(Transform parent, string name, Vector2 pos, Vector2 size, string text, Color tint, int fontSize)
        {
            Image board = UiImage(parent, name, pos, size, "SignBoard", tint, 2f);
            board.raycastTarget = false;
            Text t = Label(board.transform, "Text", new Vector2(0, 2), size, text, fontSize, TextAnchor.MiddleCenter);
            t.color = new Color(1f, 0.9f, 0.55f);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.06f, 0.04f, 0.95f);
            return t;
        }

        /// <summary>A thin gold rule with a gem in the middle and one at each end, to split a panel into parts.</summary>
        private static void GoldRule(Transform parent, string name, Vector2 pos, float width)
        {
            Color gold = new Color(0.95f, 0.75f, 0.3f);
            Panel(parent, name, pos, new Vector2(width, 4f), gold).GetComponent<Image>().raycastTarget = false;
            Panel(parent, name + "Shade", pos + new Vector2(0, -3f), new Vector2(width, 2f), new Color(0.25f, 0.15f, 0.08f, 0.9f)).GetComponent<Image>().raycastTarget = false;
            foreach (float x in new[] { -width / 2f, 0f, width / 2f })
            {
                RectTransform gem = Panel(parent, name + "Gem", pos + new Vector2(x, 0), new Vector2(x == 0f ? 16f : 10f, x == 0f ? 16f : 10f), gold);
                gem.localRotation = Quaternion.Euler(0, 0, 45f);
                gem.GetComponent<Image>().raycastTarget = false;
            }
        }

        /// <summary>
        /// A panel's backing as an image right behind the existing children (sibling index 'index'), so it can be added
        /// after the parts it sits behind.
        /// </summary>
        private static Image BackPanel(Transform parent, string name, Vector2 pos, Vector2 size, string sprite, Color tint, float pixelScale, int index)
        {
            Image img = UiImage(parent, name, pos, size, sprite, tint, pixelScale);
            img.raycastTarget = false;
            img.transform.SetSiblingIndex(index);
            return img;
        }

        // ================================================================== player HUD

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

            // The plate: an ornate violet panel with gold trim.
            UiImage(rt, "Background", new Vector2(0, 62), new Vector2(430, 232), "PanelOrnate", new Color(1f, 1f, 1f, 0.92f), 2f);
            // Level medallion on the left: a dark disc in a gold ring, the level in it.
            Image back = UiImage(rt, "LevelBack", new Vector2(-160, 120), new Vector2(84, 84), "PortraitBack", Color.white, 1.3f);
            back.preserveAspect = true;
            Image ring = UiImage(rt, "LevelRing", new Vector2(-160, 120), new Vector2(84, 84), "PortraitNeutral", Color.white, 1.3f);
            ring.preserveAspect = true;
            hud.levelLabel = Label(rt, "Level", new Vector2(-160, 116), new Vector2(84, 50), "", 34, TextAnchor.MiddleCenter);
            hud.levelLabel.color = new Color(1f, 0.9f, 0.55f);
            hud.levelLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            Label(rt, "LevelCaption", new Vector2(-160, 82), new Vector2(84, 22), "LEVEL", 14, TextAnchor.MiddleCenter).color = new Color(1f, 0.85f, 0.5f);

            hud.nameLabel = Label(rt, "Name", new Vector2(42, 152), new Vector2(300, 36), "", 26, TextAnchor.MiddleLeft);
            hud.nameLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            // Health and mana bars with their numbers inside, and an icon at the left of each.
            RectTransform trail;
            Text value;
            hud.healthFill = UiBar(rt, "HealthBar", new Vector2(52, 114), new Vector2(286, 34), "BarFillHealth", true, out trail, out value, 20);
            hud.healthTrail = trail;
            hud.healthLabel = value;
            Label(rt, "HealthIcon", new Vector2(-104, 114), new Vector2(30, 30), "", 24, TextAnchor.MiddleCenter).color = new Color(1f, 0.35f, 0.35f);
            hud.manaFill = UiBar(rt, "ManaBar", new Vector2(52, 76), new Vector2(286, 34), "BarFillMana", false, out trail, out value, 20);
            hud.manaLabel = value;
            Label(rt, "ManaIcon", new Vector2(-104, 76), new Vector2(30, 30), "", 24, TextAnchor.MiddleCenter).color = new Color(0.45f, 0.75f, 1f);
            // Gold and training points.
            Label(rt, "GoldIcon", new Vector2(-104, 36), new Vector2(30, 30), "●", 22, TextAnchor.MiddleCenter).color = new Color(1f, 0.8f, 0.25f);
            hud.goldLabel = Label(rt, "Gold", new Vector2(-12, 36), new Vector2(150, 30), "", 22, TextAnchor.MiddleLeft);
            hud.goldLabel.color = new Color(1f, 0.85f, 0.35f);
            hud.pointsLabel = Label(rt, "Points", new Vector2(130, 36), new Vector2(140, 30), "", 22, TextAnchor.MiddleRight);
            hud.pointsLabel.color = new Color(0.75f, 0.95f, 0.75f);

            // XP bar: the purple fill moves in segments (PlayerHUD), ticks over it, the numbers in the middle.
            const int xpSegments = 20;
            RectTransform dummy;
            Text xpText;
            RectTransform xpFill = UiBar(rt, "XpBar", new Vector2(0, -26), new Vector2(404, 28), "BarFillXp", false, out dummy, out xpText, 16);
            xpFill.localScale = new Vector3(0f, 1f, 1f);
            hud.xpFill = xpFill;
            hud.xpFillImage = xpFill.GetComponent<Image>();
            hud.xpColor = Color.white;
            hud.xpFlashColor = new Color(1f, 1f, 0.6f);
            hud.xpSegments = xpSegments;
            Transform xpFrame = xpFill.parent;
            float innerW = 404f - 16f;
            for (int k = 1; k < xpSegments; k++)
                Panel(xpFrame, "Tick" + k, new Vector2(-innerW / 2f + innerW * k / xpSegments, 0f), new Vector2(2f, 12f), new Color(0.05f, 0.02f, 0.08f, 0.8f));
            xpText.transform.SetAsLastSibling();
            hud.xpLabel = xpText;

            hud.questLabel = Label(rt, "Quest", new Vector2(0, -122), new Vector2(400, 120), "", 20, TextAnchor.UpperLeft);
            hud.questLabel.color = new Color(1f, 0.95f, 0.7f);
            hud.questLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            // What's playing, just over the plate (MusicDirector fills it: the game and the track).
            NowPlayingLabel(rt, new Vector2(0, 192), new Vector2(420, 22), 15, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.62f));

            // LEVEL UP banner above that.
            Image banner = UiImage(rt, "LevelUpBanner", new Vector2(0, 254), new Vector2(360, 96), "Tooltip", Color.white, 2f);
            hud.levelUpText = Label(banner.transform, "Text", Vector2.zero, new Vector2(340, 90), "", 30, TextAnchor.MiddleCenter);
            hud.levelUpText.color = new Color(1f, 0.88f, 0.4f);
            hud.levelUpText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            hud.levelUpBanner = banner.gameObject;
            banner.gameObject.SetActive(false);

            ApplyCombatFont(rt);
            canvasGo.SetActive(false);
            BuildLevelUpFx(hud);
        }

        /// <summary>A sparkle material (additive, the star texture) for particle effects.</summary>
        private static Material SparkleMat(string name, Color c)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) return Glow(name, c, 1.5f);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiTexDir + "/Sparkle.png");
            if (tex != null)
            {
                var imp = AssetImporter.GetAtPath(UiTexDir + "/Sparkle.png") as TextureImporter;
                if (imp != null && (imp.filterMode != FilterMode.Point || !imp.alphaIsTransparency))
                {
                    imp.filterMode = FilterMode.Point;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                }
                mat.mainTexture = tex;
            }
            mat.SetColor("_TintColor", c * 0.6f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// The level-up effect, local to you: a burst of gold and white sparkles round you, a ring of light rising from
        /// your feet with motes spiralling up it, and the fanfare (PlayerHUD.PlayLevelUp moves it to you and fires it).
        /// </summary>
        private static void BuildLevelUpFx(PlayerHUD hud)
        {
            var root = Child(hud.transform, "LevelUpFx", Vector3.zero);
            // Burst: emitted all at once by script.
            var burstGo = Child(root, "Burst", Vector3.zero).gameObject;
            var ps = burstGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 1f, 0.9f));
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var pr = burstGo.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = SparkleMat("LevelUpSparkle", new Color(1f, 0.85f, 0.4f));
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            hud.levelUpBurst = ps;

            // Rise: motes spiralling up a ring round you for about two seconds.
            var riseGo = Child(root, "Rise", Vector3.zero).gameObject;
            var rs = riseGo.AddComponent<ParticleSystem>();
            var rm = rs.main;
            rm.playOnAwake = false;
            rm.loop = false;
            rm.duration = 1.8f;
            rm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            rm.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            rm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            rm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.85f, 1f), new Color(1f, 0.9f, 0.5f));
            rm.simulationSpace = ParticleSystemSimulationSpace.World;
            rm.maxParticles = 160;
            var re = rs.emission;
            re.rateOverTime = 70f;
            var rsh = rs.shape;
            rsh.shapeType = ParticleSystemShapeType.Circle;
            rsh.radius = 0.75f;
            rsh.radiusThickness = 0f;
            rsh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = rs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);   // the circle is turned to lie flat: local z is up
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(3f);
            var rcol = rs.colorOverLifetime;
            rcol.enabled = true;
            rcol.color = g;
            var rr = riseGo.GetComponent<ParticleSystemRenderer>();
            rr.sharedMaterial = SparkleMat("LevelUpMote", new Color(0.7f, 0.9f, 1f));
            rr.renderMode = ParticleSystemRenderMode.Billboard;
            hud.levelUpRise = rs;

            var audioGo = Child(root, "Fanfare", Vector3.zero).gameObject;
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0.8f;
            src.clip = UiClip("LevelUp");
            hud.levelUpAudio = src;
        }

        /// <summary>An unlit cut-out material showing one of the UI textures on a quad in the world (quest arrow, seat glows).</summary>
        private static Material SpriteQuadMat(string matName, string uiTexture, bool additive = false)
        {
            Sprite sp = UiSprite(uiTexture);
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + matName + ".mat";
            Shader sh = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Unlit/Transparent Cutout");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            if (sp != null) mat.mainTexture = sp.texture;
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.4f);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================== dressing the world's menus

        /// <summary>
        /// Gives every plain menu in the scene the PS1 look: big untextured panels become ornate gold-trimmed plates
        /// (parchment when they were light), and plain buttons get a bevelled gold-rimmed face tinted with their colour.
        /// Small pieces (bar fills, ticks, swatches) and full-screen dimmers are left alone, and anything that already
        /// has a sprite (card frames, icons) is untouched.
        /// </summary>
        private static int StyleWorldUi(GameObject[] roots)
        {
            Sprite ornate = UiSprite("PanelOrnate"), parchment = UiSprite("PanelParchment"), button = UiSprite("Button");
            if (ornate == null || button == null) return 0;
            int n = 0;
            foreach (var go in roots)
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite != null || img.GetComponentInParent<Canvas>(true) == null) continue;
                    var r = (RectTransform)img.transform;
                    Vector2 size = r.sizeDelta;
                    if (img.GetComponent<Button>() != null)
                    {
                        if (Mathf.Min(size.x, size.y) < 24f) continue;
                        Color c = img.color;
                        // Lift dark colours a little so the bevel still shows on them.
                        img.color = new Color(Mathf.Lerp(c.r, 1f, 0.15f), Mathf.Lerp(c.g, 1f, 0.15f), Mathf.Lerp(c.b, 1f, 0.15f), c.a);
                        SetUiSprite(img, button, Mathf.Min(size.x, size.y) >= 60f ? 2f : 1f);
                        n++;
                        continue;
                    }
                    if (Mathf.Min(size.x, size.y) < 90f || (size.x >= 1200f && size.y >= 800f) || img.color.a < 0.45f) continue;
                    if (img.name.Contains("Fill") || img.name.Contains("Track") || img.name.Contains("Bar") || img.name.Contains("Swatch")) continue;
                    Color pc = img.color;
                    bool light = pc.r * 0.3f + pc.g * 0.59f + pc.b * 0.11f > 0.5f;
                    SetUiSprite(img, light && parchment != null ? parchment : ornate, Mathf.Max(size.x, size.y) >= 500f ? 3f : 2f);
                    img.color = new Color(1f, 1f, 1f, Mathf.Max(0.88f, pc.a));
                    n++;
                }
            return n;
        }
    }
}
