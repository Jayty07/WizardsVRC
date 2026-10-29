using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Getting about without portals, and seeing where you're going:
    /// - Tunnels: each street is reached through a Z-shaped tunnel bored into a mountain, from the castle's wall to the
    ///   street's entrance. The two ends are identical; between the bends you are moved from one to the other out of
    ///   sight (TunnelPassage), so you just walk through.
    /// - Mountains: twin peaks with a pass between them over every tunnel, at both ends.
    /// - Skylines: every area's skybox shows the castle and the streets where they really lie (the line of each
    ///   tunnel, Horizon Distance away), over the sea, with clouds and other far islands.
    /// - The sea's swell and the surf round the island.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        // ================================================================== tunnels

        private const float TunW = 4.4f, TunH = 4.4f, TunT = 0.6f, TunL1 = 9f, TunL2 = 10f, TunL3 = 6f;

        /// <summary>One end of a tunnel: its root (frame: mouth at the origin, +Z into the tunnel), middle frame and trigger.</summary>
        private class TunnelEnd
        {
            public Transform root;
            public Transform mid;
            /// <summary>A spot just outside the mouth, facing away from it (guide arrows, returns).</summary>
            public Transform front;
            public TunnelPassage passage;
            public TunnelGate gate;
            /// <summary>Where the tunnel's mountain stands (local to the area root), for the skylines.</summary>
            public Vector3 mountainCentre;
        }

        /// <summary>Box from min to max corners in the parent's frame.</summary>
        private static void TBox(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, Material mat)
        {
            Block(parent, name, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, y1 - y0, z1 - z0), mat);
        }

        /// <summary>
        /// A Z-shaped stone tunnel: from the mouth (local origin, floor level) 9 m straight in (+Z), 10 m to the right
        /// (+X), then on into darkness. The swap trigger fills the far half of the middle stretch; the middle frame's +Z
        /// points along it, away from the mouth. Lanterns light the bends.
        /// </summary>
        private static TunnelEnd BuildTunnel(Transform parent, string name, Vector3 mouth, Vector3 outward, Material stone, Material floor,
            Material dark, Material glow, List<UdonSharpBehaviour> behaviours)
        {
            var e = new TunnelEnd();
            Transform t = Child(parent, "Gate_Tunnel_" + name, mouth);
            t.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z).normalized);
            e.root = t;
            float hw = TunW / 2f, h = TunH, th = TunT, l1 = TunL1, l2 = TunL2, end = TunL1 + TunL3;
            // floors and ceilings of the three stretches
            TBox(t, "TunnelFloor", -hw, hw, -0.3f, 0.03f, 0f, l1 + hw, floor);
            TBox(t, "TunnelFloor", -hw, l2 + hw, -0.3f, 0.03f, l1 - hw, l1 + hw, floor);
            TBox(t, "TunnelFloor", l2 - hw, l2 + hw, -0.3f, 0.03f, l1 - hw, end, floor);
            TBox(t, "TunnelRoof", -hw - th, hw + th, h, h + th, 0f, l1 + hw + th, stone);
            TBox(t, "TunnelRoof", -hw - th, l2 + hw + th, h, h + th, l1 - hw - th, l1 + hw + th, stone);
            TBox(t, "TunnelRoof", l2 - hw - th, l2 + hw + th, h, h + th, l1 - hw - th, end + th, stone);
            // walls
            TBox(t, "TunnelWall", -hw - th, -hw, -0.3f, h, 0f, l1 + hw + th, stone);
            TBox(t, "TunnelWall", hw, hw + th, -0.3f, h, 0f, l1 - hw, stone);
            TBox(t, "TunnelWall", hw, l2 + hw + th, -0.3f, h, l1 - hw - th, l1 - hw, stone);
            TBox(t, "TunnelWall", -hw - th, l2 - hw, -0.3f, h, l1 + hw, l1 + hw + th, stone);
            TBox(t, "TunnelWall", l2 - hw - th, l2 - hw, -0.3f, h, l1 + hw, end, stone);
            TBox(t, "TunnelWall", l2 + hw, l2 + hw + th, -0.3f, h, l1 - hw - th, end, stone);
            TBox(t, "TunnelEnd", l2 - hw - th, l2 + hw + th, -0.3f, h + th, end, end + th, dark);
            // Closes the outside corner of the Z (between the mouth and the far stretch) so nobody walks round the tunnel.
            TBox(t, "TunnelFacade", hw, l2 + hw + th, -0.3f, h + th, 0f, th, stone);
            // Lanterns at the bends and just inside the mouth; a dark band along the walls' foot.
            foreach (var p in new[] { new Vector3(-hw + 0.12f, 3.1f, l1), new Vector3(l2 + hw - 0.12f, 3.1f, l1), new Vector3(-hw + 0.12f, 3.1f, 2.5f) })
            {
                Block(t, "TunnelLantern", p, new Vector3(0.22f, 0.45f, 0.3f), glow);
                Block(t, "TunnelLanternBracket", p + new Vector3(0, -0.35f, 0), new Vector3(0.3f, 0.08f, 0.3f), dark);
            }

            e.mid = Child(t, "TunnelMiddle", new Vector3(l2 / 2f, 0f, l1));
            e.mid.localRotation = Quaternion.Euler(0f, 90f, 0f);
            // The trigger starts 0.8 m past the middle, so where you land at the other end is clear of its trigger.
            var trig = Child(e.mid, "Passage", new Vector3(0f, 1.6f, 1.9f));
            var box = trig.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(TunW, 3.2f, 2.2f);
            e.passage = UdonSharpUndo.AddComponent<TunnelPassage>(trig.gameObject);
            e.passage.here = e.mid;
            behaviours.Add(e.passage);

            e.front = Child(t, "TunnelFront", new Vector3(0f, 0.1f, -3f));
            e.front.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return e;
        }

        /// <summary>Stone pillars and a lintel round a tunnel mouth, with a sign over it readable from outside.</summary>
        private static Text TunnelFacade(TunnelEnd e, Material stone, Material gold, string text, Color color)
        {
            Transform t = e.root;
            float hw = TunW / 2f + TunT;
            foreach (float s in new[] { -1f, 1f })
                TBox(t, "MouthPillar", s < 0 ? -hw - 0.9f : hw, s < 0 ? -hw : hw + 0.9f, -0.3f, TunH + 1.4f, -0.7f, 0.2f, stone);
            TBox(t, "MouthLintel", -hw - 1.1f, hw + 1.1f, TunH, TunH + 1.9f, -0.8f, 0.2f, stone);
            TBox(t, "MouthKeystone", -0.45f, 0.45f, TunH + 0.4f, TunH + 1.6f, -0.95f, -0.75f, gold);
            // Read from outside, looking in (+Z): the canvas faces the way the reader looks.
            var label = WorldText(t, "TunnelSign", new Vector3(0f, TunH + 3f, -1.2f), Quaternion.identity, new Vector2(1500, 300), 0.005f, text, 56);
            label.color = color;
            return label;
        }

        /// <summary>An iron portcullis across the mouth (with its collider): the gate's barrier, down while it's locked.</summary>
        private static GameObject Portcullis(TunnelEnd e, Material iron)
        {
            var go = Child(e.root, "Portcullis", new Vector3(0f, TunH / 2f, 0.4f)).gameObject;
            go.AddComponent<BoxCollider>().size = new Vector3(TunW, TunH, 0.3f);
            for (float x = -TunW / 2f + 0.3f; x < TunW / 2f; x += 0.55f)
                Deco(go.transform, PrimitiveType.Cube, "Bar", new Vector3(x, 0f, 0f), new Vector3(0.1f, TunH, 0.1f), iron, Quaternion.identity, false);
            for (float y = -TunH / 2f + 0.5f; y < TunH / 2f; y += 0.9f)
                Deco(go.transform, PrimitiveType.Cube, "Rail", new Vector3(0f, y, 0f), new Vector3(TunW, 0.1f, 0.12f), iron, Quaternion.identity, false);
            return go;
        }

        /// <summary>The tunnel's footprint (zone-local XZ bounds of its walls), for keeping houses and map edges out of it.</summary>
        private static Rect TunnelFootprint(TunnelEnd e, Transform zoneRoot)
        {
            float hw = TunW / 2f + TunT;
            var corners = new[] { new Vector3(-hw - 1f, 0, -1f), new Vector3(TunL2 + hw + 1f, 0, -1f), new Vector3(-hw - 1f, 0, TunL1 + TunL3 + 1f),
                                  new Vector3(TunL2 + hw + 1f, 0, TunL1 + TunL3 + 1f) };
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var c in corners)
            {
                Vector3 p = zoneRoot.InverseTransformPoint(e.root.TransformPoint(c));
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        /// <summary>The four invisible walls round a map's edge, with a gap wherever a tunnel goes through.</summary>
        private static void EdgeWalls(Transform root, float W, float H, List<Rect> gaps)
        {
            Transform bounds = Child(root, "Bounds", Vector3.zero);
            // Each edge as a line: (along X or Z, fixed coordinate, half length).
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float fixedC = side == 0 ? H / 2f : side == 1 ? -H / 2f : side == 2 ? W / 2f : -W / 2f;
                float half = alongX ? W / 2f : H / 2f;
                var cuts = new List<Vector2>();
                foreach (var g in gaps)
                {
                    bool crosses = alongX ? (fixedC >= g.yMin - 1f && fixedC <= g.yMax + 1f) : (fixedC >= g.xMin - 1f && fixedC <= g.xMax + 1f);
                    if (crosses) cuts.Add(alongX ? new Vector2(g.xMin, g.xMax) : new Vector2(g.yMin, g.yMax));
                }
                cuts.Sort((a, b) => a.x.CompareTo(b.x));
                float from = -half;
                var pieces = new List<Vector2>();
                foreach (var c in cuts) { if (c.x > from) pieces.Add(new Vector2(from, c.x)); from = Mathf.Max(from, c.y); }
                if (from < half) pieces.Add(new Vector2(from, half));
                foreach (var p in pieces)
                {
                    float mid = (p.x + p.y) / 2f, len = p.y - p.x;
                    var wall = Child(bounds, "Edge", alongX ? new Vector3(mid, 15f, fixedC) : new Vector3(fixedC, 15f, mid)).gameObject.AddComponent<BoxCollider>();
                    wall.size = alongX ? new Vector3(len, 30f, 1f) : new Vector3(1f, 30f, len);
                }
            }
        }

        // ================================================================== mountains

        /// <summary>
        /// Twin rocky peaks with a pass between them, standing over a tunnel (in the tunnel's frame: the mountain begins
        /// frontZ in front of the mouth and runs back from there). The rock front facing the area is a cliff, open where
        /// the tunnel goes in; the ground over the whole tunnel is well above its roof; the far sides plunge 'drop' metres
        /// (into the sea round the castle). Snow on the tops. Scenery only (no colliders).
        /// </summary>
        private static Vector3 Mountains(Transform parent, string key, TunnelEnd e, float frontZ, float drop, float scale, Material rock, Material snow)
        {
            float Wm = 48f * scale, Dm = 62f * scale;
            const int nx = 26, nz = 22;
            var heights = new float[nx + 1, nz + 1];
            float peak = 0f;
            float hwT = TunW / 2f + TunT + 1.5f;
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = -Wm + 2f * Wm * i / nx, z = frontZ + Dm * j / nz;
                    float h = MountainHeight(x, z, frontZ, Wm, Dm, scale);
                    // Over the tunnel the rock stays well above its roof.
                    if (x > -hwT - 2f && x < TunL2 + hwT + 2f && z < TunL1 + TunL3 + 3f) h = Mathf.Max(h, TunH + TunT + 2.5f * scale + 1.5f);
                    // The far sides fall away below the ground (into the sea).
                    float edge = Mathf.Min((Wm - Mathf.Abs(x)) / Wm, (frontZ + Dm - z) / Dm);
                    h = Mathf.Lerp(-drop, h, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.18f)));
                    heights[i, j] = h;
                    peak = Mathf.Max(peak, h);
                }

            var verts = new List<Vector3>();
            var rockTris = new List<int>();
            var snowTris = new List<int>();
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                    verts.Add(new Vector3(-Wm + 2f * Wm * i / nx, heights[i, j], frontZ + Dm * j / nz));
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    // PSX-ish: alternate the diagonal so the facets look chiselled.
                    int[] tri = (i + j) % 2 == 0 ? new[] { a, c, b, b, c, d } : new[] { a, c, d, a, d, b };
                    for (int k = 0; k < 6; k += 3)
                    {
                        float avg = (verts[tri[k]].y + verts[tri[k + 1]].y + verts[tri[k + 2]].y) / 3f;
                        var list = avg > peak * 0.7f ? snowTris : rockTris;
                        list.Add(tri[k]); list.Add(tri[k + 1]); list.Add(tri[k + 2]);
                    }
                }
            // Flat-shaded: every triangle its own vertices.
            var fv = new List<Vector3>();
            var fr = new List<int>();
            var fs = new List<int>();
            foreach (int idx in rockTris) { fr.Add(fv.Count); fv.Add(verts[idx]); }
            foreach (int idx in snowTris) { fs.Add(fv.Count); fv.Add(verts[idx]); }
            var mesh = new Mesh { name = "PSX_Mountain_" + key };
            mesh.SetVertices(fv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(fr, 0);
            mesh.SetTriangles(fs, 1);
            var uv = new List<Vector2>();
            foreach (var v in fv) uv.Add(new Vector2(v.x + v.z, v.y) * 0.125f);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + mesh.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("Mountain_" + key);
            go.isStatic = true;
            go.transform.SetParent(e.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { rock, snow };

            // The cliff across the front, in columns as tall as the rock behind them, open over the tunnel's mouth.
            float hwM = TunW / 2f + TunT;
            for (int i = 0; i < nx; i++)
            {
                float x0 = -Wm + 2f * Wm * i / nx, x1 = -Wm + 2f * Wm * (i + 1) / nx;
                float top = Mathf.Min(heights[i, 0], heights[i + 1, 0]);
                if (top <= 0.2f) continue;
                if (x1 <= -hwM || x0 >= hwM) TBox(e.root, "Cliff", x0, x1, -drop, top, frontZ - 0.6f, frontZ + 0.6f, rock);
                else
                {
                    if (x0 < -hwM) TBox(e.root, "Cliff", x0, -hwM, -drop, top, frontZ - 0.6f, frontZ + 0.6f, rock);
                    if (x1 > hwM) TBox(e.root, "Cliff", hwM, x1, -drop, top, frontZ - 0.6f, frontZ + 0.6f, rock);
                    TBox(e.root, "Cliff", Mathf.Max(x0, -hwM), Mathf.Min(x1, hwM), TunH + TunT, Mathf.Max(top, TunH + TunT + 0.5f), frontZ - 0.6f, frontZ + 0.6f, rock);
                }
            }
            return e.root.TransformPoint(new Vector3(TunL2 / 2f, 0f, frontZ + Dm * 0.45f));
        }

        /// <summary>Two peaks either side of the tunnel's line with a pass over it, plus some rolling rock.</summary>
        private static float MountainHeight(float x, float z, float frontZ, float Wm, float Dm, float scale)
        {
            float h = 0f;
            var peaks = new[]
            {
                new Vector4(-Wm * 0.5f, frontZ + Dm * 0.5f, 56f * scale, Wm * 0.62f),
                new Vector4(Wm * 0.56f, frontZ + Dm * 0.46f, 47f * scale, Wm * 0.56f),
                new Vector4(-Wm * 0.1f, frontZ + Dm * 0.92f, 34f * scale, Wm * 0.45f),
            };
            foreach (var p in peaks)
            {
                float d = new Vector2(x - p.x, z - p.y).magnitude / p.w;
                if (d < 1f) h = Mathf.Max(h, p.z * Mathf.Pow(1f - d, 1.25f));
            }
            // The pass: a low saddle between the peaks, and rough rock everywhere.
            h = Mathf.Max(h, 9f * scale * Mathf.Clamp01(1f - Mathf.Abs(z - frontZ - Dm * 0.45f) / (Dm * 0.6f)));
            h += (Mathf.Sin(x * 0.37f + z * 0.21f) + Mathf.Sin(x * 0.11f - z * 0.43f)) * 1.4f * scale;
            return h;
        }

        // ================================================================== sea and surf

        /// <summary>Material for the open sea: long rolling swells with whitecaps on their crests.</summary>
        private static Material PsxSwellMat()
        {
            return WaterAsset("PSX_SeaSwell", false, m =>
            {
                m.SetColor("_DeepColor", new Color(0.03f, 0.16f, 0.5f));
                m.SetColor("_ShallowColor", new Color(0.15f, 0.5f, 0.92f));
                m.SetFloat("_TileSize", 11f);
                m.SetFloat("_WaveHeight", 0.9f);
                m.SetFloat("_WaveLength", 34f);
                m.SetFloat("_WaveSpeed", 0.9f);
                m.SetFloat("_Shimmer", 0.3f);
                m.SetFloat("_CrestFoam", 0.22f);
                m.SetFloat("_Alpha", 0.92f);
            });
        }

        private static Material PsxSurfMat()
        {
            var shader = Shader.Find("WizardsVRC/PSXSurf");
            if (shader == null) { Debug.LogError("[WizardsVRC] Shader WizardsVRC/PSXSurf is missing; no surf round the island."); return null; }
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/PSX_Surf.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.mainTexture = TileTexture(WaterTexDir + "/WaterRipple.png");
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        /// <summary>A flat ring from radius r0 (UV v = 0, the shore) out to r1 (v = 1), u running round it.</summary>
        private static Mesh SurfRingMesh(float r0, float r1, int segs)
        {
            string name = "PSX_SurfRing_" + Mathf.RoundToInt(r0) + "_" + Mathf.RoundToInt(r1);
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var idx = new List<int>();
            const int rings = 4;
            for (int i = 0; i <= segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                for (int k = 0; k <= rings; k++)
                {
                    float r = Mathf.Lerp(r0, r1, k / (float)rings);
                    verts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    uvs.Add(new Vector2(i / (float)segs, k / (float)rings));
                }
            }
            for (int i = 0; i < segs; i++)
                for (int k = 0; k < rings; k++)
                {
                    int a = i * (rings + 1) + k, b = a + 1, c = a + rings + 1, d = c + 1;
                    idx.Add(a); idx.Add(b); idx.Add(c); idx.Add(b); idx.Add(d); idx.Add(c);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
