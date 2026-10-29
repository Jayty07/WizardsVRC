using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Ground for zones and the hub, from the map and its height map: raised and sunken blocks of ground with stone
    /// retaining walls, cobbled paving with kerbs, lawns, stairs wherever paths change height, copings and parapets along
    /// drops, and water ('~') with rims. Plus the PSX water / waterfall materials and meshes.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string TerrainTexDir = TexturesDir + "/Terrain";
        private const string WaterTexDir = TexturesDir + "/Water";

        private class TerrainMats
        {
            public Material paving, lawn, wall, kerb, bed, water, foam;
        }

        private static TerrainMats MakeTerrainMats(ZoneDefinition z, string k)
        {
            Texture2D cobble = TileTexture(TerrainTexDir + "/Cobble.png");
            Texture2D grass = TileTexture(TerrainTexDir + "/Grass.png");
            Texture2D stone = TileTexture(TerrainTexDir + "/StoneWall.png");
            return new TerrainMats
            {
                paving = PsxTexMat(k + "_Paving", z.roadColor, cobble, 2.4f),
                lawn = PsxTexMat(k + "_Lawn", z.grassColor, grass, 3f),
                wall = PsxTexMat(k + "_RetainingWall", Color.Lerp(z.buildingColor, new Color(0.62f, 0.55f, 0.5f), 0.55f), stone, 2.2f),
                kerb = PsxTexMat(k + "_Kerb", Color.Lerp(z.roadColor, new Color(0.85f, 0.83f, 0.8f), 0.55f), stone, 1.1f),
                bed = PsxTexMat(k + "_PondBed", new Color(0.34f, 0.36f, 0.3f), cobble, 3f),
                water = PsxWaterMat(),
                foam = PsxFoamMat(),
            };
        }

        /// <summary>
        /// Builds the zone's ground on top of a base at baseY (the lowest level): ground blocks for higher levels,
        /// paving and lawns, kerbs, stairs, retaining-wall copings / parapets and water. Cells marked by 'stairCells' get
        /// stairs in them (callers can keep lamps off them).
        /// </summary>
        private static void BuildTerrain(Transform parent, ZoneGrid g, TerrainMats m, float baseY, HashSet<Vector2Int> stairCells)
        {
            float cs = g.cell;
            Transform t = Child(parent, "Terrain", Vector3.zero);

            // ---- raised ground: every cell above the base level stands on a block of retaining-wall stone
            if (g.HasHeights)
                foreach (var r in Rects(g, ch => true, 10, 10))
                {
                    int lv = g.Level(r.xMin, r.yMin);
                    if (lv <= g.minLevel) continue;
                    Vector3 c = RectCenter(g, r);
                    float top = lv * g.step;
                    Block(t, "Ground", new Vector3(c.x, (baseY + top) / 2f, c.z), new Vector3(r.width * cs, top - baseY, r.height * cs), m.wall);
                }

            // ---- surfaces: lawns (grass, trees, hedges), water beds, cobbles everywhere else
            foreach (var r in Rects(g, ch => ch == ',' || ch == '^' || ch == '=', 8, 8)) Slab(t, "Lawn", g, r, 0.025f, m.lawn);
            foreach (var r in Rects(g, ch => ch != ',' && ch != '^' && ch != '=' && !ZoneGrid.IsWater(ch), 8, 8)) Slab(t, "Paving", g, r, 0.03f, m.paving);
            foreach (var r in Rects(g, ZoneGrid.IsWater, 8, 8)) Slab(t, "PondBed", g, r, 0.02f, m.bed);

            // ---- kerbs where paths meet lawn at the same height
            foreach (var run in EdgeRuns(g, (a, b) => g.Paved(a.x, a.y) && IsLawn(g.At(b.x, b.y)) && g.Level(a) == g.Level(b)))
            {
                Vector3 c = RunCenter(g, run, -0.15f);
                Deco(t, PrimitiveType.Cube, "Kerb", c + new Vector3(0, 0.08f, 0), RunScale(g, run, 0.3f, 0.16f), m.kerb, Quaternion.identity, false);
            }

            // ---- water: surface heights per pond, the water itself and a rim (with a taller invisible wall) round it
            var surface = WaterSurfaces(g);
            foreach (var r in Rects(g, ZoneGrid.IsWater, 12, 12))
            {
                Vector3 c = RectCenter(g, r);
                float y = surface[new Vector2Int(r.xMin, r.yMin)];
                float w = r.width * cs, h = r.height * cs;
                Mesh grid = GridMesh(Mathf.Clamp(Mathf.CeilToInt(w / 2f), 2, 32), Mathf.Clamp(Mathf.CeilToInt(h / 2f), 2, 32), false);
                Shape(t, "Water", grid, new Vector3(c.x, y, c.z), new Vector3(w, 1f, h), m.water, false, Quaternion.identity);
            }
            foreach (var run in EdgeRuns(g, (a, b) => !ZoneGrid.IsWater(g.At(a.x, a.y)) && ZoneGrid.IsWater(g.At(b.x, b.y)) && g.Walkable(a.x, a.y)))
            {
                Vector3 c = RunCenter(g, run, -0.25f);
                Block(t, "PondRim", c + new Vector3(0, 0.28f, 0), RunScale(g, run, 0.5f, 0.56f), m.kerb);
                var fence = Child(t, "PondFence", c + new Vector3(0, 0.75f, 0)).gameObject.AddComponent<BoxCollider>();
                fence.size = RunScale(g, run, 0.5f, 1.5f);
            }

            // ---- stairs where paved cells change height; copings or parapets along every other drop
            foreach (var run in StairRuns(g))
            {
                // 'a' is the upper cell; the stairs fill the edge of the lower cells (b side).
                Vector3 dirDown = ZoneGrid.ToLocalDir(run.dir);
                Vector3 edge = RunCenter(g, run, 0f);
                float upper = g.Elevation(run.start.x, run.start.y) + 0.06f;
                Vector2Int low = run.start + run.dir;
                float lower = g.Elevation(low.x, low.y) + 0.06f;
                float rise = upper - lower;
                float runLen = Mathf.Min(cs - 0.6f, rise * 1.7f);
                float width = run.length * cs - 0.2f;
                Vector3 top = new Vector3(edge.x, upper, edge.z);
                Vector3 bottom = top + dirDown * runLen;
                bottom.y = lower;
                Stairs(t, "Stairs", bottom, top, width, m.kerb);
                Vector3 side = Vector3.Cross(Vector3.up, dirDown);
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 p = (top + bottom) / 2f + side * sgn * (width / 2f + 0.2f);
                    Block(t, "StairCheek", new Vector3(p.x, lower + (rise + 0.45f) / 2f, p.z), new Vector3(0.4f, rise + 0.45f, runLen),
                        m.kerb, Quaternion.LookRotation(dirDown));
                }
                for (int i = 0; i < run.length; i++) stairCells.Add(low + run.along * i);
            }
            foreach (var run in EdgeRuns(g, (a, b) => g.Level(b) < g.Level(a) && !g.HasStairs(a, b) && !ZoneGrid.IsWater(g.At(b.x, b.y)) &&
                                                        !ZoneGrid.IsWater(g.At(a.x, a.y))))
            {
                Vector2Int low = run.start + run.dir;
                float drop = g.Elevation(run.start.x, run.start.y) - g.Elevation(low.x, low.y);
                bool parapet = drop >= 2.5f && g.Paved(run.start.x, run.start.y);
                Vector3 c = RunCenter(g, run, -0.25f);
                if (parapet) Block(t, "Parapet", c + new Vector3(0, 0.45f, 0), RunScale(g, run, 0.5f, 0.9f), m.kerb);
                else Deco(t, PrimitiveType.Cube, "Coping", c + new Vector3(0, 0.1f, 0), RunScale(g, run, 0.5f, 0.2f), m.kerb, Quaternion.identity, false);
            }
        }

        private static bool IsLawn(char c) { return c == ',' || c == '^' || c == '='; }

        /// <summary>
        /// Stair runs (upper cell side, pointing down). Where two flights at right angles would share a corner cell (a
        /// plaza corner with stairs on two sides), the longer flight keeps it and the other stops short.
        /// </summary>
        private static List<EdgeRun> StairRuns(ZoneGrid g)
        {
            var runs = EdgeRuns(g, (a, b) => g.Level(b) < g.Level(a) && g.HasStairs(a, b));
            var owner = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < runs.Count; i++)
                for (int k = 0; k < runs[i].length; k++)
                {
                    Vector2Int cell = runs[i].start + runs[i].dir + runs[i].along * k;
                    int other;
                    if (!owner.TryGetValue(cell, out other) || runs[i].length > runs[other].length) owner[cell] = i;
                }
            var result = new List<EdgeRun>();
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                while (r.length > 0 && owner[r.start + r.dir] != i) { r.start += r.along; r.length--; }
                while (r.length > 0 && owner[r.start + r.dir + r.along * (r.length - 1)] != i) r.length--;
                if (r.length > 0) result.Add(r);
            }
            return result;
        }

        // ------------------------------------------------------------------ edge runs

        /// <summary>
        /// A straight run of cell edges: 'start' is the first cell on the near side, 'dir' points across the edge to the
        /// far cell, 'along' runs along the edge, 'length' cells long.
        /// </summary>
        private struct EdgeRun
        {
            public Vector2Int start, dir, along;
            public int length;
        }

        /// <summary>All edges (a, a + dir) where test(a, b) holds, merged into straight runs.</summary>
        private static List<EdgeRun> EdgeRuns(ZoneGrid g, System.Func<Vector2Int, Vector2Int, bool> test)
        {
            var runs = new List<EdgeRun>();
            foreach (var d in ZoneGrid.Dirs)
            {
                var along = new Vector2Int(Mathf.Abs(d.y), Mathf.Abs(d.x));
                var done = new HashSet<Vector2Int>();
                for (int y = 0; y < g.height; y++)
                    for (int x = 0; x < g.width; x++)
                    {
                        var a = new Vector2Int(x, y);
                        if (done.Contains(a) || !test(a, a + d)) continue;
                        // Runs only join cells with the same heights on both sides, so each run is one flat piece.
                        int la = g.Level(a), lb = g.Level(a + d);
                        int n = 1;
                        while (true)
                        {
                            var nx = a + along * n;
                            if (nx.x >= g.width || nx.y >= g.height || done.Contains(nx) || !test(nx, nx + d) ||
                                g.Level(nx) != la || g.Level(nx + d) != lb) break;
                            n++;
                        }
                        for (int i = 0; i < n; i++) done.Add(a + along * i);
                        runs.Add(new EdgeRun { start = a, dir = d, along = along, length = n });
                    }
            }
            return runs;
        }

        /// <summary>Middle of a run's edge line on the near cell's ground, moved 'inset' metres across it (negative = into the near cell).</summary>
        private static Vector3 RunCenter(ZoneGrid g, EdgeRun run, float inset)
        {
            Vector3 first = g.Local(run.start), last = g.Local(run.start + run.along * (run.length - 1));
            Vector3 dir = ZoneGrid.ToLocalDir(run.dir);
            return (first + last) / 2f + dir * (g.cell / 2f + inset);
        }

        private static Vector3 RunScale(ZoneGrid g, EdgeRun run, float thickness, float height)
        {
            float len = run.length * g.cell;
            return run.dir.x != 0 ? new Vector3(thickness, height, len) : new Vector3(len, height, thickness);
        }

        /// <summary>Water surface height for every water cell: each pond sits a little below its lowest rim.</summary>
        private static Dictionary<Vector2Int, float> WaterSurfaces(ZoneGrid g)
        {
            var result = new Dictionary<Vector2Int, float>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    var s = new Vector2Int(x, y);
                    if (!ZoneGrid.IsWater(g.At(x, y)) || result.ContainsKey(s)) continue;
                    // Flood the pond, noting its floor and the lowest ground round it.
                    var body = new List<Vector2Int>();
                    var queue = new Queue<Vector2Int>();
                    var seen = new HashSet<Vector2Int> { s };
                    queue.Enqueue(s);
                    float floor = float.MinValue, rim = float.MaxValue;
                    while (queue.Count > 0)
                    {
                        var p = queue.Dequeue();
                        body.Add(p);
                        floor = Mathf.Max(floor, g.Elevation(p.x, p.y));
                        foreach (var d in ZoneGrid.Dirs)
                        {
                            var n = p + d;
                            if (seen.Contains(n)) continue;
                            if (ZoneGrid.IsWater(g.At(n.x, n.y))) { seen.Add(n); queue.Enqueue(n); }
                            else if (g.Walkable(n.x, n.y)) rim = Mathf.Min(rim, g.Elevation(n.x, n.y));
                        }
                    }
                    if (rim == float.MaxValue) rim = floor;
                    float surface = rim > floor + 0.6f ? Mathf.Max(floor + 0.35f, rim - 0.8f) : floor + 0.12f;
                    foreach (var p in body) result[p] = surface;
                }
            return result;
        }

        // ------------------------------------------------------------------ materials and textures

        /// <summary>Repeating point-filtered texture (mipmaps on, so distant ground doesn't sparkle in VR).</summary>
        private static Texture2D TileTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled))
            {
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogWarning("[WizardsVRC] Missing texture " + path + " (run Tools/make_psx_textures.py).");
            return tex;
        }

        /// <summary>PSX material with a texture that tiles in world space every 'tile' metres (whatever the object's size).</summary>
        private static Material PsxTexMat(string name, Color color, Texture2D tex, float tile)
        {
            Material mat = PMat(name, color, 1f);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = Vector2.one;
                mat.SetFloat("_WorldUV", tile);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Water materials are made once and then left alone, so you can tune them in the inspector.
        private static Material PsxWaterMat() { return WaterAsset("PSX_Water", false, m => { }); }

        private static Material PsxSeaMat()
        {
            return WaterAsset("PSX_WaterSea", false, m =>
            {
                m.SetColor("_DeepColor", new Color(0.03f, 0.16f, 0.5f));
                m.SetColor("_ShallowColor", new Color(0.15f, 0.5f, 0.92f));
                m.SetFloat("_TileSize", 11f);
                m.SetFloat("_WaveHeight", 0.35f);
                m.SetFloat("_WaveLength", 22f);
                m.SetFloat("_WaveSpeed", 0.8f);
                m.SetFloat("_Alpha", 0.9f);
            });
        }

        private static Material PsxFoamMat()
        {
            return WaterAsset("PSX_WaterFoam", false, m =>
            {
                m.SetColor("_DeepColor", new Color(0.62f, 0.85f, 1f));
                m.SetColor("_ShallowColor", new Color(0.92f, 0.98f, 1f));
                m.SetColor("_LineColor", Color.white);
                m.SetFloat("_Sparkle", 0.8f);
                m.SetFloat("_TileSize", 2f);
                m.SetVector("_Scroll", new Vector4(0.25f, 0.4f, -0.3f, 0.35f));
                m.SetFloat("_WaveHeight", 0.08f);
                m.SetFloat("_WaveLength", 1.6f);
                m.SetFloat("_WaveSpeed", 4f);
                m.SetFloat("_Alpha", 0.9f);
            });
        }

        private static Material PsxWaterfallMat() { return WaterAsset("PSX_Waterfall", true, m => { }); }

        private static Material WaterAsset(string name, bool waterfall, System.Action<Material> firstTime)
        {
            var shader = Shader.Find(waterfall ? "WizardsVRC/PSXWaterfall" : "WizardsVRC/PSXWater");
            if (shader == null)
            {
                Debug.LogError("[WizardsVRC] Water shaders are missing (Assets/WizardsVRC/Shaders); using a plain material.");
                return PMat(name + "_Fallback", new Color(0.2f, 0.5f, 0.95f), 4f, new Color(0.05f, 0.15f, 0.3f));
            }
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
                firstTime(mat);
            }
            mat.shader = shader;
            if (mat.mainTexture == null)
                mat.mainTexture = TileTexture(WaterTexDir + (waterfall ? "/WaterfallStreaks.png" : "/WaterRipple.png"));
            mat.SetFloat("_SnapRes", PsxShake);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>
        /// Subdivided unit plane for water. Flat: XZ from -0.5 to 0.5 at y 0 (normal up). Vertical: X from -0.5 to 0.5,
        /// Y from 0 to 1 (normal +Z, UV v 0 at the bottom) for waterfall sheets. Shared vertices, so waves stay joined.
        /// </summary>
        private static Mesh GridMesh(int sx, int sy, bool vertical)
        {
            string name = (vertical ? "PSX_WaterfallSheet_" : "PSX_WaterGrid_") + sx + "x" + sy;
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var idx = new List<int>();
            for (int j = 0; j <= sy; j++)
                for (int i = 0; i <= sx; i++)
                {
                    float u = i / (float)sx, v = j / (float)sy;
                    verts.Add(vertical ? new Vector3(u - 0.5f, v, 0f) : new Vector3(u - 0.5f, 0f, v - 0.5f));
                    uvs.Add(new Vector2(u, v));
                    normals.Add(vertical ? Vector3.forward : Vector3.up);
                }
            for (int j = 0; j < sy; j++)
                for (int i = 0; i < sx; i++)
                {
                    int a = j * (sx + 1) + i, b = a + 1, c = a + sx + 1, d = c + 1;
                    if (vertical) { idx.Add(a); idx.Add(b); idx.Add(c); idx.Add(b); idx.Add(d); idx.Add(c); }
                    else { idx.Add(a); idx.Add(c); idx.Add(b); idx.Add(b); idx.Add(c); idx.Add(d); }
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(normals);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            // Room for the waves, so the GPU-moved vertices are never culled early.
            Bounds b2 = mesh.bounds;
            b2.Expand(vertical ? new Vector3(0.2f, 0.2f, 0.6f) : new Vector3(0.05f, 1f, 0.05f));
            mesh.bounds = b2;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ waterfall landmark

        /// <summary>
        /// A waterfall pouring off the landmark's cells into the water next to them: a stone spring with a short channel
        /// on top, a falling sheet (PSXWaterfall), churning foam and spray where it lands.
        /// </summary>
        private static void WaterfallLandmark(Transform root, ZoneGrid g, ZoneLandmark l, Material rock)
        {
            var cells = g.Find(l.Symbol);
            var fp = g.Footprint(l.Symbol);
            // Pour toward the side with the most water next to the footprint.
            Vector2Int best = Vector2Int.zero;
            int bestCount = 0;
            foreach (var d in ZoneGrid.Dirs)
            {
                int n = 0;
                foreach (var c in cells) if (ZoneGrid.IsWater(g.At(c.x + d.x, c.y + d.y))) n++;
                if (n > bestCount) { bestCount = n; best = d; }
            }
            if (bestCount == 0)
            {
                Debug.LogWarning("[WizardsVRC World] Waterfall '" + l.symbol + "' has no water ('~') next to it; skipped.");
                return;
            }
            var surfaces = WaterSurfaces(g);
            Vector2Int waterCell = Vector2Int.zero;
            foreach (var c in cells) if (ZoneGrid.IsWater(g.At(c.x + best.x, c.y + best.y))) { waterCell = c + best; break; }
            float water = surfaces[waterCell];

            Vector3 dir = ZoneGrid.ToLocalDir(best), side = Vector3.Cross(Vector3.up, dir);
            Vector3 c0 = RectCenter(g, fp);
            float top = g.Elevation(fp.xMin, fp.yMin);
            float across = (best.x != 0 ? fp.height : fp.width) * g.cell, depth = (best.x != 0 ? fp.width : fp.height) * g.cell;
            Vector3 lip = new Vector3(c0.x, top, c0.z) + dir * (depth / 2f);
            Quaternion rot = Quaternion.LookRotation(dir);
            float sheetW = Mathf.Max(1.5f, across - 2.4f);
            Transform wf = Child(root, "Waterfall_" + l.symbol, lip);
            wf.localRotation = rot;

            // Spring: rock cheeks either side of a channel and a mossy rock at the back, the water running out over the lip.
            var moss = PMat("Moss", new Color(0.28f, 0.5f, 0.24f), 2f);
            foreach (float sgn in new[] { -1f, 1f })
            {
                Block(wf, "SpringCheek", new Vector3(sgn * (sheetW / 2f + 0.6f), 0.9f, -depth / 2f), new Vector3(1.2f, 1.8f, depth), rock);
                Shape(wf, "SpringRock", BlobMesh(), new Vector3(sgn * (sheetW / 2f + 0.8f), 1.9f, -depth * 0.35f), new Vector3(1.8f, 1.2f, 2.2f), rock, false,
                    Quaternion.Euler(0, sgn * 25f, 0));
                Shape(wf, "Moss", BlobMesh(), new Vector3(sgn * (sheetW / 2f + 0.5f), 2.45f, -depth * 0.4f), new Vector3(1.3f, 0.4f, 1.6f), moss, false, Quaternion.identity);
            }
            Block(wf, "SpringBack", new Vector3(0, 1.3f, -depth + 0.5f), new Vector3(sheetW + 2.4f, 2.6f, 1f), rock);
            Shape(wf, "SpringCap", BlobMesh(), new Vector3(0, 2.7f, -depth + 0.7f), new Vector3(sheetW + 1.6f, 1f, 1.6f), rock, false, Quaternion.identity);
            Block(wf, "ChannelBed", new Vector3(0, 0.15f, -depth / 2f + 0.5f), new Vector3(sheetW, 0.3f, depth - 1f), rock);
            Mesh channel = GridMesh(4, Mathf.Clamp(Mathf.CeilToInt(depth / 2f), 2, 8), false);
            Shape(wf, "ChannelWater", channel, new Vector3(0, 0.38f, -depth / 2f + 0.5f), new Vector3(sheetW, 1f, depth - 1f), PsxWaterMat(), false, Quaternion.identity);

            // The falling sheet, from just above the lip down into the pond.
            float h = top + 0.4f - (water - 0.15f);
            Mesh sheet = GridMesh(6, Mathf.Clamp(Mathf.CeilToInt(h / 0.6f), 4, 16), true);
            // (Not static: the shader reads the sheet's own width from its transform, which static batching would bake away.)
            Shape(wf, "WaterfallSheet", sheet, new Vector3(0, water - 0.15f - top, 0.25f), new Vector3(sheetW, h, 1f), PsxWaterfallMat(), false,
                Quaternion.identity).isStatic = false;
            // A second, narrower sheet a little in front gives the fall some thickness.
            Shape(wf, "WaterfallFront", sheet, new Vector3(0, water - 0.15f - top, 0.5f), new Vector3(sheetW * 0.8f, h * 0.97f, 1f), PsxWaterfallMat(), false,
                Quaternion.identity).isStatic = false;

            // Churning foam where it lands, and spray.
            float y = water - top + 0.04f;
            Mesh foamGrid = GridMesh(6, 3, false);
            Shape(wf, "Foam", foamGrid, new Vector3(0, y, 1.6f), new Vector3(sheetW + 1.2f, 1f, 2.6f), PsxFoamMat(), false, Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                float x = (i / 3f - 0.5f) * sheetW * 0.9f;
                Shape(wf, "FoamPuff", BlobMesh(), new Vector3(x, y + 0.1f, 0.9f + (i % 2) * 0.6f), new Vector3(1.3f, 0.35f, 1.1f), PsxFoamMat(), false,
                    Quaternion.Euler(0, i * 40f, 0));
            }
            WaterSpray(wf, new Vector3(0, y + 0.1f, 1f), sheetW);
        }

        private static void WaterSpray(Transform parent, Vector3 at, float width)
        {
            var go = Child(parent, "Spray", at).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.startColor = new Color(0.9f, 0.97f, 1f, 0.9f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 0.1f, 0.6f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            vel.y = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
            vel.z = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Glow("WaterSpray", new Color(0.9f, 0.97f, 1f), 0.5f);
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }
}
