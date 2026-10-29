using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// PSX name tags: text baked at build time into small point-filtered textures with the pixel font in
    /// Assets/WizardsVRC/Fonts/PsxNameFont.txt, shown on quads with the WizardsVRC/PSXBillboard shader so each player
    /// sees them turned toward their own camera. Only for text that never changes at runtime (Udon can't rebake).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string NameFontPath = "Assets/WizardsVRC/Fonts/PsxNameFont.txt";
        private const string NameTagsDir = MaterialsDir + "/NameTags";

        /// <summary>World size of one texture pixel for each kind of tag.</summary>
        private const float NameTexel = 0.03f;
        private const float HintTexel = 0.02f;
        private const float MarkerTexel = 0.09f;

        private static readonly Color32 NameTop = new Color32(255, 255, 248, 255), NameBottom = new Color32(176, 196, 255, 255);
        private static readonly Color32 GoldTop = new Color32(255, 236, 130, 255), GoldBottom = new Color32(232, 150, 40, 255);
        private static readonly Color32 TagOutline = new Color32(18, 14, 26, 255), TagShadow = new Color32(0, 0, 0, 255);

        private static Dictionary<char, string[]> _nameFont;
        private static Dictionary<string, Material> _nameTagMats;

        /// <summary>Called at the start of each build: forget cached tags (their folder is cleared on first use).</summary>
        private static void ResetNameTags()
        {
            _nameFont = null;
            _nameTagMats = null;
        }

        /// <summary>A white NPC name that faces each player.</summary>
        private static GameObject NameTag(Transform parent, string name, Vector3 localPos, string text)
        {
            return BillboardText(parent, name, localPos, text, NameTop, NameBottom, NameTexel, 22f, 30f);
        }

        /// <summary>A small gold hint line under a name.</summary>
        private static GameObject HintTag(Transform parent, string name, Vector3 localPos, string text)
        {
            return BillboardText(parent, name, localPos, text, GoldTop, GoldBottom, HintTexel, 9f, 13f);
        }

        /// <summary>A big gold quest marker ("!" or "?"), visible from further away.</summary>
        private static GameObject MarkerTag(Transform parent, string name, Vector3 localPos, string text)
        {
            return BillboardText(parent, name, localPos, text, GoldTop, GoldBottom, MarkerTexel, 55f, 65f);
        }

        private static GameObject BillboardText(Transform parent, string name, Vector3 localPos, string text, Color32 top, Color32 bottom,
            float texel, float fadeStart, float fadeEnd)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Material mat = NameTagMaterial(text, top, bottom, fadeStart, fadeEnd);
            if (mat == null) return null;
            Texture tex = mat.mainTexture;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            // The shader uses the world scale, so undo any parent scale.
            Vector3 ps = parent.lossyScale;
            go.transform.localScale = new Vector3(tex.width * texel / Mathf.Max(0.0001f, Mathf.Abs(ps.x)),
                tex.height * texel / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.isStatic = false;
            return go;
        }

        private static Material NameTagMaterial(string text, Color32 top, Color32 bottom, float fadeStart, float fadeEnd)
        {
            if (_nameTagMats == null)
            {
                _nameTagMats = new Dictionary<string, Material>();
                // Rebuilt every time, so names that changed or were removed don't leave old tags behind.
                if (AssetDatabase.IsValidFolder(NameTagsDir)) AssetDatabase.DeleteAsset(NameTagsDir);
            }
            string key = text + "|" + top + bottom + "|" + fadeStart + "|" + fadeEnd;
            Material mat;
            if (_nameTagMats.TryGetValue(key, out mat)) return mat;

            var shader = Shader.Find("WizardsVRC/PSXBillboard");
            if (shader == null)
            {
                Debug.LogError("[WizardsVRC] Shader WizardsVRC/PSXBillboard is missing, so name tags were skipped.");
                return null;
            }
            Texture2D tex = BakeText(text, top, bottom);
            if (tex == null) return null;

            if (!AssetDatabase.IsValidFolder(NameTagsDir))
            {
                if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets/WizardsVRC", "Materials");
                AssetDatabase.CreateFolder(MaterialsDir, "NameTags");
            }
            string safe = "";
            foreach (char c in text) if (char.IsLetterOrDigit(c) && safe.Length < 32) safe += c;
            string path = AssetDatabase.GenerateUniqueAssetPath(NameTagsDir + "/Tag_" + (safe.Length > 0 ? safe : "Text") + ".mat");
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mat, path);
            tex.name = mat.name + "_Tex";
            AssetDatabase.AddObjectToAsset(tex, mat);
            mat.mainTexture = tex;
            mat.SetFloat("_SnapRes", PsxShake);
            mat.SetFloat("_FadeStart", fadeStart);
            mat.SetFloat("_FadeEnd", fadeEnd);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            _nameTagMats[key] = mat;
            return mat;
        }

        private static Dictionary<char, string[]> NameFont()
        {
            if (_nameFont != null) return _nameFont;
            _nameFont = new Dictionary<char, string[]>();
            if (!File.Exists(NameFontPath))
            {
                Debug.LogError("[WizardsVRC] Name-tag font not found at " + NameFontPath);
                return _nameFont;
            }
            char cur = '\0';
            var rows = new List<string>();
            foreach (string raw in File.ReadAllLines(NameFontPath))
            {
                string line = raw.TrimEnd('\r', ' ', '\t');
                if (cur == '\0' && line.StartsWith("#")) continue; // header comments (glyph rows also start with '#')
                if (line.Length == 2 && line[0] == ':')
                {
                    if (cur != '\0') AddGlyph(cur, rows);
                    cur = line[1];
                    rows.Clear();
                    continue;
                }
                if (cur != '\0' && line.Length > 0) rows.Add(line);
            }
            if (cur != '\0') AddGlyph(cur, rows);
            return _nameFont;
        }

        private static void AddGlyph(char c, List<string> rows)
        {
            bool ok = rows.Count == 7 || rows.Count == 9;
            foreach (string r in rows) if (r.Length != rows[0].Length) ok = false;
            if (!ok)
            {
                Debug.LogError("[WizardsVRC] Name-tag font glyph '" + c + "' needs 7 or 9 rows of equal width; skipped.");
                return;
            }
            _nameFont[c] = rows.ToArray();
        }

        /// <summary>Text in the pixel font with a top-to-bottom gradient, a 1px dark outline and a drop shadow.</summary>
        private static Texture2D BakeText(string text, Color32 top, Color32 bottom)
        {
            var font = NameFont();
            string[] fallback;
            font.TryGetValue('?', out fallback);
            var glyphs = new List<string[]>();
            bool descenders = false;
            int width = 0;
            foreach (char c in text)
            {
                string[] g = null;
                if (c != ' ' && !font.TryGetValue(c, out g)) g = fallback;
                if (c != ' ' && g == null) continue;
                glyphs.Add(g);
                if (g != null && g.Length == 9) descenders = true;
                width += (g == null ? 3 : g[0].Length) + 1;
            }
            if (glyphs.Count == 0) return null;
            width -= 1;
            int height = descenders ? 9 : 7;

            var ink = new bool[width, height];
            int x0 = 0;
            foreach (string[] g in glyphs)
            {
                if (g == null) { x0 += 4; continue; }
                for (int y = 0; y < g.Length && y < height; y++)
                    for (int x = 0; x < g[y].Length; x++)
                        if (g[y][x] == '#') ink[x0 + x, y] = true;
                x0 += g[0].Length + 1;
            }

            const int pad = 2;
            int W = width + pad * 2, H = height + pad * 2 + 1;
            var outline = new bool[W, H];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (ink[x, y])
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                outline[x + pad + dx, y + pad + dy] = true;

            // Pixels are worked out top-down, then stored bottom-up as Unity textures expect.
            var px = new Color32[W * H];
            System.Action<int, int, Color32> set = (x, y, c) => px[(H - 1 - y) * W + x] = c;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (outline[x, y] && y + 1 < H && !outline[x, y + 1]) set(x, y + 1, TagShadow);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (outline[x, y]) set(x, y, TagOutline);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (ink[x, y]) set(x + pad, y + pad, Color32.Lerp(top, bottom, Mathf.Min(1f, y / 6f)));

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
