using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The world creator's title: chunky 3D letters floating over their head, each one a solid extruded block in the
    /// game's own pixel font (Fonts/PsxNameFont.txt) with a glowing gold face and deep violet sides, bobbing and wobbling
    /// one after another in a wave. An Animator plays the wave (no script cost); PlayerCosmetics shows it only over the
    /// creator (by VRChat display name) and its title root turns it to face each viewer.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string CreatorTitleText = "World Creator";

        /// <summary>Builds the floating letters under 'parent' (centred on it, reading left to right to whoever it faces).</summary>
        private static Transform CreatorLetters(Transform parent, string text)
        {
            var font = NameFont();
            const float p = 0.026f, depth = 0.12f;   // deep enough that the violet sides show as they turn
            var root = Child(parent, "CreatorTitle", new Vector3(0f, 0.26f, 0f));
            root.gameObject.isStatic = false;
            var face = PMat("CreatorTitleFace", new Color(1f, 0.84f, 0.32f), 1f, new Color(1f, 0.72f, 0.22f) * 0.85f);
            var side = PMat("CreatorTitleSide", new Color(0.38f, 0.16f, 0.6f), 1f, new Color(0.26f, 0.1f, 0.42f) * 0.6f);

            // Lay the letters out (1 pixel between letters, 3 for a space), centred.
            float total = 0f;
            foreach (char c in text) total += c == ' ' ? 3f : GlyphFor(font, c)[0].Length + 1f;
            float x = -(total - 1f) * p / 2f;
            var letters = new List<Transform>();
            foreach (char c in text)
            {
                if (c == ' ') { x += 3f * p; continue; }
                string[] rows = GlyphFor(font, c);
                int w = rows[0].Length;
                var lt = Child(root, "L" + letters.Count, new Vector3(x + w * p / 2f, 0f, 0f));
                lt.gameObject.isStatic = false;
                lt.gameObject.AddComponent<MeshFilter>().sharedMesh = LetterMesh(rows, p, depth, "CreatorLetter_" + (int)c);
                var r = lt.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterials = new[] { face, side };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                letters.Add(lt);
                x += (w + 1f) * p;
            }

            // The wave: each letter bobs, rocks and turns a beat after the one before it.
            var clip = NewClip("CreatorWobble", true);
            const float len = 1.8f;
            for (int i = 0; i < letters.Count; i++)
            {
                float ph = -i * 0.075f;
                Wave(clip, "L" + i, "posy", len, 1, 0f, 0.045f, ph);
                Wave(clip, "L" + i, "rotz", len, 1, 0f, 8f, ph + 0.25f);
                Wave(clip, "L" + i, "roty", len, 1, 0f, 24f, ph + 0.1f);
                Wave(clip, "L" + i, "rotx", len, 1, 0f, 0f);
                Wave(clip, "L" + i, "scaley", len, 2, 1f, 0.06f, ph);
            }
            Directory.CreateDirectory(NpcDir);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(NpcDir + "/CreatorTitle.controller");
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            ctrl.layers[0].stateMachine.AddState("Wobble").motion = clip;
            var anim = root.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return root;
        }

        private static string[] GlyphFor(Dictionary<char, string[]> font, char c)
        {
            string[] rows;
            if (font.TryGetValue(c, out rows)) return rows;
            if (font.TryGetValue(char.ToUpperInvariant(c), out rows)) return rows;
            if (font.TryGetValue('?', out rows)) return rows;
            return new[] { "###", "#.#", "#.#", "#.#", "#.#", "#.#", "###" };
        }

        /// <summary>
        /// One letter as a solid: every ink pixel a block 'p' wide and 'depth' deep, centred on x, sitting on y = 0 (the two
        /// descender rows of a 9-row glyph go below it). Submesh 0 is the front (facing -Z, the viewer), 1 the sides and back.
        /// </summary>
        private static Mesh LetterMesh(string[] rows, float p, float depth, string name)
        {
            int w = rows[0].Length, h = rows.Length;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var front = new List<int>();
            var rest = new List<int>();
            float zf = -depth / 2f, zb = depth / 2f, ox = -w * p / 2f;
            System.Func<int, int, bool> ink = (cx, r) => cx >= 0 && cx < w && r >= 0 && r < h && rows[r][cx] == '#';
            for (int r = 0; r < h; r++)
                for (int cx = 0; cx < w; cx++)
                {
                    if (!ink(cx, r)) continue;
                    float x0 = ox + cx * p, x1 = x0 + p, y0 = (6 - r) * p, y1 = y0 + p;
                    LetterQuad(verts, norms, front, new Vector3(x0, y0, zf), new Vector3(x0, y1, zf), new Vector3(x1, y1, zf), new Vector3(x1, y0, zf), Vector3.back);
                    LetterQuad(verts, norms, rest, new Vector3(x0, y0, zb), new Vector3(x0, y1, zb), new Vector3(x1, y1, zb), new Vector3(x1, y0, zb), Vector3.forward);
                    if (!ink(cx - 1, r)) LetterQuad(verts, norms, rest, new Vector3(x0, y0, zf), new Vector3(x0, y1, zf), new Vector3(x0, y1, zb), new Vector3(x0, y0, zb), Vector3.left);
                    if (!ink(cx + 1, r)) LetterQuad(verts, norms, rest, new Vector3(x1, y0, zf), new Vector3(x1, y1, zf), new Vector3(x1, y1, zb), new Vector3(x1, y0, zb), Vector3.right);
                    if (!ink(cx, r - 1)) LetterQuad(verts, norms, rest, new Vector3(x0, y1, zf), new Vector3(x1, y1, zf), new Vector3(x1, y1, zb), new Vector3(x0, y1, zb), Vector3.up);
                    if (!ink(cx, r + 1)) LetterQuad(verts, norms, rest, new Vector3(x0, y0, zf), new Vector3(x1, y0, zf), new Vector3(x1, y0, zb), new Vector3(x0, y0, zb), Vector3.down);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[verts.Count]));
            mesh.subMeshCount = 2;
            mesh.SetTriangles(front, 0);
            mesh.SetTriangles(rest, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A flat quad (corners in order round it) wound so it faces 'n'.</summary>
        private static void LetterQuad(List<Vector3> verts, List<Vector3> norms, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) norms.Add(n);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) >= 0f)
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
    }
}
