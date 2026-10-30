using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The school classrooms' furnishings: the school's carpet and aisle runner, desks and chairs, and banners with the
    /// school's symbol (textures from Tools/make_school_textures.py, in Textures/School). Room-local, +Z = the board,
    /// the door at -Z, the floor's top at y 0.
    /// The carpet and runner are 5 cm thick boxes standing on the floor, side by side (never one over the other), so
    /// nothing flat lies a hair above anything else; in the Balance room the sand fills round them at 3 cm.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string SchoolTexDir = TexturesDir + "/School";
        private const float CarpetH = 0.05f;
        private const float AisleW = 2.2f;

        /// <summary>The carpet: rw wide, rd deep, centred at z = cz (it reaches up to 3 m from the board).</summary>
        private static void ClassCarpetRect(float W, float D, out float rw, out float rd, out float cz)
        {
            rw = Mathf.Max(4f, W - 3f);
            rd = Mathf.Max(4f, D - 7f);
            cz = D / 2f - 3f - rd / 2f;
        }

        /// <summary>A PSX material showing one texture once over a mesh's UVs (carpets, banners), with little texture swim.</summary>
        private static Material ClassMat(string name, string texFile, Vector2 tiling, float cutoff)
        {
            Material m = PMat(name, Color.white, 1f);
            Texture2D tex = TileTexture(SchoolTexDir + "/" + texFile);
            if (tex != null) m.mainTexture = tex;
            m.mainTextureScale = tiling;
            m.mainTextureOffset = Vector2.zero;
            m.SetFloat("_WorldUV", 0f);
            m.SetFloat("_Affine", 0.08f);
            m.SetFloat("_Cutoff", cutoff);
            m.renderQueue = cutoff > 0f ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void ClassroomCarpet(Transform room, int s, float W, float D)
        {
            string sn = SchoolLabels[s];
            float rw, rd, cz;
            ClassCarpetRect(W, D, out rw, out rd, out cz);
            Mesh box = CornerBoxMesh();
            // Carpet: the box's corner sits at its door-side, left corner.
            Shape(room, "Carpet", box, new Vector3(-rw / 2f, 0f, cz - rd / 2f), new Vector3(rw, CarpetH, rd),
                ClassMat("ClassCarpet" + sn, "Carpet" + sn + ".png", Vector2.one, 0f), false, Quaternion.identity);
            // Runner from just inside the door up to the carpet's edge (they meet, they don't overlap).
            float z0 = -D / 2f + 0.3f, z1 = cz - rd / 2f;
            if (z1 - z0 > 0.5f)
            {
                float len = z1 - z0;
                Shape(room, "Runner", box, new Vector3(-AisleW / 2f, 0f, z0), new Vector3(AisleW, CarpetH, len),
                    ClassMat("ClassRunner" + sn + "_" + Mathf.RoundToInt(len * 10f), "Runner" + sn + ".png", new Vector2(1f, len / (AisleW * 2f)), 0f),
                    false, Quaternion.identity);
            }
        }

        /// <summary>Balance's sand floor: strips round the carpet and either side of the runner, a little lower than both.</summary>
        private static void ClassroomSand(Transform room, float W, float D, Material sand)
        {
            float rw, rd, cz;
            ClassCarpetRect(W, D, out rw, out rd, out cz);
            float sx = (W - 1.5f) / 2f, s0 = -D / 2f + 0.3f, s1 = D / 2f - 1f;
            float c0 = cz - rd / 2f, c1 = cz + rd / 2f, hx = rw / 2f;
            const float h = 0.03f;
            var strips = new List<UnityEngine.Rect>
            {
                UnityEngine.Rect.MinMaxRect(-sx, s0, -hx, s1),                      // left of the carpet, full depth
                UnityEngine.Rect.MinMaxRect(hx, s0, sx, s1),                        // right
                UnityEngine.Rect.MinMaxRect(-hx, c1, hx, s1),                       // behind it
                UnityEngine.Rect.MinMaxRect(-hx, s0, -AisleW / 2f, c0),             // in front, left of the runner
                UnityEngine.Rect.MinMaxRect(AisleW / 2f, s0, hx, c0),               // in front, right of the runner
            };
            foreach (var r in strips)
                if (r.width > 0.05f && r.height > 0.05f)
                    Deco(room, PrimitiveType.Cube, "Sand", new Vector3(r.center.x, h / 2f, r.center.y), new Vector3(r.width, h, r.height), sand, Quaternion.identity, false);
        }

        private static void ClassroomDesks(Transform room, int s, float W, float D)
        {
            float back = D / 2f;
            Material desk = PsxTexMat("ClassDesk", Color.white, TileTexture(SchoolTexDir + "/Desk.png"), 1.4f);
            Material dark = PsxTexMat("ClassDeskDark", new Color(0.55f, 0.5f, 0.5f), TileTexture(SchoolTexDir + "/Desk.png"), 1.4f);
            Color col = SchoolColors[s];
            Material book = PMat("ClassBook" + SchoolLabels[s], Color.Lerp(col, Color.black, 0.25f), 1f);
            Material ink = PMat("Inkwell", new Color(0.12f, 0.12f, 0.18f), 1f);
            Material paper = PMat("Parchment", new Color(0.93f, 0.88f, 0.74f), 1f);
            var holder = Child(room, "Desks", Vector3.zero);
            int cols = Mathf.Clamp(Mathf.FloorToInt((W - 4f) / 2.8f), 2, 6);
            if (cols % 2 == 1) cols--;
            int rows = Mathf.Clamp(Mathf.FloorToInt((D - 9f) / 2.4f), 2, 5);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int half = cols / 2;
                    float x = (c < half ? c - half : c - half + 1) * 2.8f - (c < half ? -0.6f : 0.6f);
                    float z = back - 6.6f - r * 2.4f;
                    var d = Child(holder, "Desk", new Vector3(x, 0f, z));
                    // Desk: a top on two side panels with a modesty panel on the board side; a book, parchment and ink.
                    Block(d, "DeskTop", new Vector3(0, 0.8f, 0), new Vector3(1.9f, 0.08f, 0.85f), desk);
                    foreach (float sx in new[] { -0.86f, 0.86f })
                        Block(d, "DeskSide", new Vector3(sx, 0.38f, 0), new Vector3(0.08f, 0.76f, 0.8f), dark);
                    Deco(d, PrimitiveType.Cube, "DeskFront", new Vector3(0, 0.5f, 0.36f), new Vector3(1.64f, 0.5f, 0.05f), dark, Quaternion.identity, false);
                    float h = Hash01(r * 7 + c);
                    Deco(d, PrimitiveType.Cube, "Book", new Vector3(-0.45f + h * 0.2f, 0.88f, 0.05f), new Vector3(0.42f, 0.08f, 0.3f), book, Quaternion.Euler(0, -12f + h * 24f, 0), false);
                    Deco(d, PrimitiveType.Cube, "Parchment", new Vector3(0.2f, 0.845f, -0.1f), new Vector3(0.36f, 0.01f, 0.26f), paper, Quaternion.Euler(0, 8f - h * 16f, 0), false);
                    Shape(d, "Inkwell", CylMesh(), new Vector3(0.62f, 0.9f, 0.18f), new Vector3(0.1f, 0.06f, 0.1f), ink, false, Quaternion.identity);
                    // Chair behind it: seat, back and two leg panels.
                    Block(d, "ChairSeat", new Vector3(0, 0.46f, -0.85f), new Vector3(0.6f, 0.07f, 0.5f), desk);
                    Deco(d, PrimitiveType.Cube, "ChairBack", new Vector3(0, 0.82f, -1.08f), new Vector3(0.6f, 0.55f, 0.06f), desk, Quaternion.identity, false);
                    foreach (float sx in new[] { -0.26f, 0.26f })
                        Deco(d, PrimitiveType.Cube, "ChairLegs", new Vector3(sx, 0.22f, -0.85f), new Vector3(0.06f, 0.44f, 0.46f), dark, Quaternion.identity, false);
                }
            // One mesh per material for the whole room's desks (the colliders stay on the pieces).
            MergeRenderers(holder, "DesksMesh");
        }

        private static void ClassroomBanners(Transform room, int s, float W, float D)
        {
            string sn = SchoolLabels[s];
            Material cloth = ClassMat("ClassBanner" + sn, "Banner" + sn + ".png", Vector2.one, 0.5f);
            Material gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            // Between the windows (one every 5 m from 3 m in), up to four a side.
            var zs = new List<float>();
            for (float z = -D / 2f + 5.5f; z < D / 2f - 3f && zs.Count < 4; z += 5f) zs.Add(z);
            if (zs.Count == 0) zs.Add(0f);
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in zs)
                {
                    float x = side * (W / 2f - 0.14f);
                    // A quad shows its face to -Z: turn it so that faces into the room.
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Banner";
                    q.transform.SetParent(room, false);
                    q.transform.localPosition = new Vector3(x, 4.3f, z);
                    q.transform.localRotation = Quaternion.LookRotation(new Vector3(side, 0f, 0f));
                    q.transform.localScale = new Vector3(1.4f, 2.8f, 1f);
                    q.GetComponent<Renderer>().sharedMaterial = cloth;
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    // Gold rod across the top with a knob at each end.
                    Shape(room, "BannerRod", CylMesh(), new Vector3(x, 5.78f, z), new Vector3(0.07f, 0.85f, 0.07f), gold, false, Quaternion.Euler(90f, 0, 0));
                    foreach (float e in new[] { -0.9f, 0.9f })
                        Shape(room, "BannerKnob", GemMesh(), new Vector3(x, 5.78f, z + e), new Vector3(0.18f, 0.22f, 0.18f), gold, false, Quaternion.identity);
                }
        }

        private static float Hash01(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 13;
            return (h % 1000) / 1000f;
        }

        /// <summary>
        /// A box from 0 to 1 on every axis (its corner is the pivot) with the top's UVs running 0..1 along X and Z (the
        /// texture's top towards +Z), the top split 6 x 6 so a texture on it barely swims. Sides get a thin strip of the
        /// texture's edge.
        /// </summary>
        private static Mesh CornerBoxMesh()
        {
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/PSX_CornerBox6.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int n = 6;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    v.Add(new Vector3(i / (float)n, 1f, j / (float)n));
                    uv.Add(new Vector2(i / (float)n, j / (float)n));
                }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int a = i * (n + 1) + j, b = a + 1, c = a + n + 2, d = a + n + 1;
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(a); t.Add(c); t.Add(d);
                }
            // Sides: each a quad facing out (wound clockwise seen from outside).
            Vector3[][] sides =
            {
                new[] { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0) },   // -Z
                new[] { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1) },   // +Z
                new[] { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0) },   // -X
                new[] { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1) },   // +X
            };
            foreach (var q in sides)
            {
                int i0 = v.Count;
                foreach (var p in q)
                {
                    v.Add(p);
                    uv.Add(new Vector2((p.x + p.z) * 0.5f, 0.01f + p.y * 0.02f));
                }
                t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
                t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
            }
            var mesh = new Mesh { name = "PSX_CornerBox6" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Combines every mesh renderer under 'holder' into one mesh per material (fewer draw calls for rooms full of
        /// desks); the original pieces keep their colliders but lose their renderers.
        /// </summary>
        private static void MergeRenderers(Transform holder, string name)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var done = new List<MeshRenderer>();
            Matrix4x4 toLocal = holder.worldToLocalMatrix;
            foreach (var mr in holder.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                List<CombineInstance> list;
                if (!groups.TryGetValue(mr.sharedMaterial, out list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mr.transform.localToWorldMatrix });
                done.Add(mr);
            }
            int k = 0;
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = name + k };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject(name + k++);
                go.transform.SetParent(holder, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            foreach (var mr in done)
            {
                var mf = mr.GetComponent<MeshFilter>();
                Object.DestroyImmediate(mr);
                if (mf != null) Object.DestroyImmediate(mf);
            }
        }
    }
}
