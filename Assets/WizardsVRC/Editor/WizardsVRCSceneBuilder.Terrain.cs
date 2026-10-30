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
        private const string StreetTexDir = TexturesDir + "/Street";
        private const string WaterTexDir = TexturesDir + "/Water";

        private class TerrainMats
        {
            public Material paving, lawn, wall, kerb, bed, water, foam, iron;
        }

        private static TerrainMats MakeTerrainMats(ZoneDefinition z, string k)
        {
            Texture2D cobble = TileTexture(TerrainTexDir + "/Cobble.png");
            Texture2D stone = TileTexture(TerrainTexDir + "/StoneWall.png");
            ZoneTextureStyle st = z.textureStyle;
            // The street's own paving and lawn (see ZoneTextureStyle); walls and kerbs are stone everywhere.
            string pave = st == ZoneTextureStyle.Garden || st == ZoneTextureStyle.Keep ? "Flagstone" : st == ZoneTextureStyle.Forest ? "Dirt" : "Cobble";
            string lawnTex = st == ZoneTextureStyle.Forest ? "ForestFloor" : "Grass";
            float paveTile = pave == "Flagstone" ? 3.4f : pave == "Dirt" ? 3f : 2.4f;
            return new TerrainMats
            {
                paving = PsxTexMat(k + "_Paving", z.roadColor, TileTexture(TerrainTexDir + "/" + pave + ".png"), paveTile),
                lawn = PsxTexMat(k + "_Lawn", z.grassColor, TileTexture(TerrainTexDir + "/" + lawnTex + ".png"), 3f),
                wall = PsxTexMat(k + "_RetainingWall", Color.Lerp(z.buildingColor, new Color(0.62f, 0.55f, 0.5f), 0.55f),
                    st == ZoneTextureStyle.Forge ? TileTexture(TerrainTexDir + "/Brick.png") : stone, st == ZoneTextureStyle.Forge ? 2f : 3f),
                kerb = PsxTexMat(k + "_Kerb", Color.Lerp(z.roadColor, new Color(0.85f, 0.83f, 0.8f), 0.55f), stone, 1.1f),
                bed = PsxTexMat(k + "_PondBed", new Color(0.34f, 0.36f, 0.3f), cobble, 3f),
                water = PsxWaterMat(),
                foam = PsxFoamMat(),
                iron = PMat("RailingIron", new Color(0.17f, 0.16f, 0.21f), 1f),
            };
        }

        /// <summary>
        /// Builds the zone's ground above a base at baseY (the lowest level): every cell is a column up to its height,
        /// its top cobbled, grassy or a pond bed, with stone walls where it drops to a lower neighbour. With a corner
        /// radius, outer corners of raised ground are rounded off and inner corners filled in (likewise where paths meet
        /// lawns). Then kerbs, copings, railings, stairs (with stringers and handrails) and water. Stair cells are added
        /// to 'stairCells' so callers can keep lamps off them. Geometry is merged into a few meshes per 8x8-cell chunk.
        /// </summary>
        /// <summary>
        /// Cells the next BuildTerrain leaves out (under a tunnel, which has its own floor), and cells it fills with a plain
        /// paved block up to a given level instead (between a gate and its tunnel, so the way in is level). Edges next to
        /// left-out cells get no kerb, coping or railing. Set just before BuildTerrain; it clears them.
        /// </summary>
        private static HashSet<Vector2Int> _terrainSkip;
        private static Dictionary<Vector2Int, int> _terrainFill;

        private static void BuildTerrain(Transform parent, ZoneGrid g, TerrainMats m, float baseY, HashSet<Vector2Int> stairCells)
        {
            var skip = _terrainSkip ?? new HashSet<Vector2Int>();
            var fill = _terrainFill ?? new Dictionary<Vector2Int, int>();
            _terrainSkip = null;
            _terrainFill = null;
            _skipNow = skip;
            float cs = g.cell;
            Transform t = Child(parent, "Terrain", Vector3.zero);
            var stairs = StairRuns(g);
            var tc = new TerrainContext(g, stairs);
            var piles = new Dictionary<long, MeshBuilder>();
            Transform colliders = Child(t, "RailingColliders", Vector3.zero);
            RailingMode mode = g.zone.railings;

            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                    if (!g.IsVoid(x, y) && !skip.Contains(new Vector2Int(x, y))) BuildCell(tc, x, y, piles, colliders, mode);
            _skipNow = null;
            // Filled cells: a stone block up to the fill level, paved on top (flush with paving at that level).
            foreach (var kv in fill)
            {
                Vector3 c = g.Local(kv.Key);
                float top = kv.Value * g.step + Lift;
                float bottom = Mathf.Min(g.Elevation(kv.Key.x, kv.Key.y), top) - 0.4f;
                Block(t, "GateFill", new Vector3(c.x, (bottom + top - 0.12f) / 2f, c.z), new Vector3(cs, top - 0.12f - bottom, cs), m.wall);
                Block(t, "GateFillPaving", new Vector3(c.x, top - 0.06f, c.z), new Vector3(cs, 0.12f, cs), m.paving);
            }

            // ---- stairs where paved cells change height: steps, sloped stone stringers and handrails
            foreach (var run in stairs)
            {
                // 'start' is the upper cell; the stairs fill the edge of the lower cells.
                Vector3 dirDown = ZoneGrid.ToLocalDir(run.dir);
                Vector3 edge = RunCenter(g, run, 0f);
                float upper = g.Elevation(run.start.x, run.start.y) + Lift;
                Vector2Int low = run.start + run.dir;
                float lower = g.Elevation(low.x, low.y) + Lift;
                float rise = upper - lower;
                float runLen = Mathf.Min(cs - 0.6f, rise * 1.7f);
                float width = run.length * cs - 0.2f;
                Vector3 top = new Vector3(edge.x, upper, edge.z);
                Vector3 bottom = top + dirDown * runLen;
                bottom.y = lower;
                Stairs(t, "Stairs", bottom, top, width, m.kerb);
                Vector3 side = Vector3.Cross(Vector3.up, dirDown);
                Vector2 d2 = new Vector2(dirDown.x, dirDown.z);
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Vector3 o = side * sgn * (width / 2f + 0.2f);
                    Vector2 a = new Vector2(top.x + o.x, top.z + o.z), b = a + d2 * runLen;
                    var cell = new Vector2Int(low.x, low.y);
                    Prism(Pile(piles, cell, MatTrim), a, b, 0.4f, lower - 0.05f, upper + 0.35f, lower - 0.05f, lower + 0.35f);
                    if (mode != RailingMode.None)
                    {
                        // Handrail: posts at both ends and the middle, the rail following the slope.
                        var iron = Pile(piles, cell, MatIron);
                        for (int k = 0; k <= 2; k++)
                        {
                            Vector2 pp = Vector2.Lerp(a, b, k / 2f);
                            float yb = Mathf.Lerp(upper, lower, k / 2f) + 0.35f;
                            Prism(iron, pp - d2 * 0.05f, pp + d2 * 0.05f, 0.1f, yb, yb + 0.95f, yb, yb + 0.95f);
                        }
                        Prism(iron, a, b, 0.09f, upper + 1.22f, upper + 1.32f, lower + 1.22f, lower + 1.32f);
                    }
                }
                for (int i = 0; i < run.length; i++) stairCells.Add(low + run.along * i);
            }

            // ---- water surfaces, a little below each pond's lowest rim
            var surface = WaterSurfaces(g);
            foreach (var r in Rects(g, ZoneGrid.IsWater, 12, 12))
            {
                Vector3 c = RectCenter(g, r);
                float y = surface[new Vector2Int(r.xMin, r.yMin)];
                float w = r.width * cs, h = r.height * cs;
                Mesh grid = GridMesh(Mathf.Clamp(Mathf.CeilToInt(w / 2f), 2, 32), Mathf.Clamp(Mathf.CeilToInt(h / 2f), 2, 32), false);
                Shape(t, "Water", grid, new Vector3(c.x, y, c.z), new Vector3(w, 1f, h), m.water, false, Quaternion.identity);
            }

            // ---- one mesh per chunk and material (solid ones get colliders)
            var mats = new[] { m.wall, m.paving, m.lawn, m.bed, m.kerb, m.iron };
            foreach (var kv in piles)
            {
                int mat = (int)(kv.Key & 0xff);
                if (kv.Value.v.Count == 0) continue;
                var go = new GameObject("TerrainMesh_" + (kv.Key >> 8) + "_" + MatNames[mat]);
                go.isStatic = true;
                go.transform.SetParent(t, false);
                var mesh = kv.Value.ToMesh(go.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mats[mat];
                if (mat <= MatBed) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        // ------------------------------------------------------------------ terrain cells

        private const float Lift = 0.02f;
        private static HashSet<Vector2Int> _skipNow;
        /// <summary>How far the plain ground slab (or island top) under a map sits below its lowest terrain.</summary>
        private const float GroundDrop = 0.5f;
        private const int ArcSegs = 4;
        private const int MatWall = 0, MatPaving = 1, MatLawn = 2, MatBed = 3, MatTrim = 4, MatIron = 5;
        private static readonly string[] MatNames = { "Walls", "Paving", "Lawn", "PondBed", "Trim", "Railings" };

        // Corners of a cell, anticlockwise seen from above (SW, SE, NE, NW), as map signs (x east, y south); side k runs
        // from corner k to corner k + 1 and faces the neighbour in SideDirs[k].
        private static readonly Vector2Int[] CornerSigns = { new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) };
        private static readonly Vector2Int[] SideDirs = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        private enum CornerKind { Square, TerrainCut, LawnCut, PaveCut, Fillet }
        private enum EdgeKind { None, Drop, StairWall, Kerb, PondFence }

        /// <summary>What every cell corner does, worked out once per zone.</summary>
        private class TerrainContext
        {
            public readonly ZoneGrid g;
            public readonly float r;
            public readonly CornerKind[,,] kinds;
            public readonly HashSet<Vector2Int> stairTouch = new HashSet<Vector2Int>();
            public readonly HashSet<long> stairEdges = new HashSet<long>();

            public TerrainContext(ZoneGrid g, List<EdgeRun> stairs)
            {
                this.g = g;
                r = Mathf.Clamp(g.zone.cornerRadius, 0f, g.cell * 0.45f);
                foreach (var run in stairs)
                    for (int i = 0; i < run.length; i++)
                    {
                        var up = run.start + run.along * i;
                        stairTouch.Add(up);
                        stairTouch.Add(up + run.dir);
                        stairEdges.Add(EdgeKey(up, run.dir));
                        stairEdges.Add(EdgeKey(up + run.dir, -run.dir));
                    }
                kinds = new CornerKind[g.width, g.height, 4];
                for (int y = 0; y < g.height; y++)
                    for (int x = 0; x < g.width; x++)
                        for (int k = 0; k < 4; k++)
                            kinds[x, y, k] = Kind(x, y, k);
            }

            /// <summary>On the map (round castles: and inside the island's circle).</summary>
            public bool In(int x, int y) { return x >= 0 && y >= 0 && x < g.width && y < g.height && !g.IsVoid(x, y); }
            /// <summary>Level, with cells off the map at the base (so the zone's outer edge gets a wall down to it).</summary>
            public int Lv(int x, int y) { return In(x, y) ? g.Level(x, y) : g.minLevel; }
            public bool Lawn(int x, int y) { return In(x, y) && IsLawn(g.At(x, y)); }
            public bool Water(int x, int y) { return In(x, y) && ZoneGrid.IsWater(g.At(x, y)); }
            public int Surface(int x, int y) { return Lawn(x, y) ? MatLawn : Water(x, y) ? MatBed : MatPaving; }

            public static long EdgeKey(Vector2Int c, Vector2Int d) { return ((long)c.x << 40) ^ ((long)c.y << 16) ^ ((long)(d.x + 2) << 4) ^ (long)(d.y + 2); }

            private CornerKind Kind(int x, int y, int k)
            {
                if (r <= 0.01f) return CornerKind.Square;
                Vector2Int s = CornerSigns[k];
                int x1 = x + s.x, y2 = y + s.y;
                if (!In(x1, y) || !In(x, y2) || !In(x1, y2)) return CornerKind.Square;
                if (stairTouch.Contains(new Vector2Int(x, y)) || stairTouch.Contains(new Vector2Int(x1, y)) ||
                    stairTouch.Contains(new Vector2Int(x, y2)) || stairTouch.Contains(new Vector2Int(x1, y2))) return CornerKind.Square;
                int lc = Lv(x, y), l1 = Lv(x1, y), l2 = Lv(x, y2), ld = Lv(x1, y2);
                if (l1 < lc && l2 == l1 && ld == l1) return CornerKind.TerrainCut;
                if (l1 > lc && l2 == l1 && ld >= l1) return CornerKind.Fillet;
                if (l1 == lc && l2 == lc && ld == lc)
                {
                    bool lawn = Lawn(x, y), a = Lawn(x1, y), b = Lawn(x, y2);
                    if (lawn && !a && !b && g.Walkable(x1, y) && g.Walkable(x, y2)) return CornerKind.LawnCut;
                    if (!lawn && a && b && g.Walkable(x, y)) return CornerKind.PaveCut;
                }
                return CornerKind.Square;
            }

            /// <summary>Corner point in zone-local XZ.</summary>
            public Vector2 Corner(int x, int y, int k)
            {
                Vector3 c = g.Local(x, y);
                Vector2Int s = CornerSigns[k];
                return new Vector2(c.x + s.x * g.cell / 2f, c.z - s.y * g.cell / 2f);
            }

            /// <summary>The corner of the neighbour across side 'side' that is the same point as corner k of this cell.</summary>
            public CornerKind NeighbourCorner(int x, int y, int side, int k)
            {
                Vector2Int d = SideDirs[side], s = CornerSigns[k];
                int nx = x + d.x, ny = y + d.y;
                if (!In(nx, ny)) return CornerKind.Square;
                var ns = new Vector2Int(d.x != 0 ? -s.x : s.x, d.y != 0 ? -s.y : s.y);
                for (int i = 0; i < 4; i++) if (CornerSigns[i] == ns) return kinds[nx, ny, i];
                return CornerKind.Square;
            }

            /// <summary>Points of the rounded corner piece at corner k: the corner, then along the arc between its two sides.</summary>
            public List<Vector2> Arc(int x, int y, int k)
            {
                Vector2 p = Corner(x, y, k), prev = Corner(x, y, (k + 3) % 4), next = Corner(x, y, (k + 1) % 4);
                Vector2 dp = (prev - p).normalized, dn = (next - p).normalized;
                Vector2 c = p + (dp + dn) * r;
                var pts = new List<Vector2>();
                for (int i = 0; i <= ArcSegs; i++)
                {
                    float a = i / (float)ArcSegs * Mathf.PI * 0.5f;
                    pts.Add(c - r * (Mathf.Cos(a) * dn + Mathf.Sin(a) * dp));
                }
                return pts;
            }
        }

        private static MeshBuilder Pile(Dictionary<long, MeshBuilder> piles, Vector2Int cell, int mat)
        {
            long key = ((long)(cell.x / 8) << 20 | (long)(cell.y / 8)) << 8 | (long)mat;
            MeshBuilder mb;
            if (!piles.TryGetValue(key, out mb)) piles[key] = mb = new MeshBuilder();
            return mb;
        }

        /// <summary>One cell: its top (with rounded corner cuts), corner pieces, walls down to lower neighbours and edge trim.</summary>
        private static void BuildCell(TerrainContext tc, int x, int y, Dictionary<long, MeshBuilder> piles, Transform colliders, RailingMode mode)
        {
            ZoneGrid g = tc.g;
            var cell = new Vector2Int(x, y);
            int lc = g.Level(x, y);
            float yc = lc * g.step + Lift;
            var pts = new List<Vector2>();
            var kinds = new List<EdgeKind>();
            var lows = new List<int>();
            var sides = new List<int>();
            for (int k = 0; k < 4; k++)
            {
                CornerKind ck = tc.kinds[x, y, k];
                // The side that starts at this corner.
                Vector2Int d = SideDirs[k];
                int nx = x + d.x, ny = y + d.y, ln = tc.Lv(nx, ny);
                EdgeKind side = EdgeKind.None;
                bool openSide = _skipNow != null && _skipNow.Contains(new Vector2Int(nx, ny));
                if (openSide) side = ln < lc ? EdgeKind.StairWall : EdgeKind.None;   // a plain wall, no coping or railing
                else if (ln < lc) side = tc.stairEdges.Contains(TerrainContext.EdgeKey(cell, d)) ? EdgeKind.StairWall : EdgeKind.Drop;
                else if (ln == lc && tc.Lawn(x, y) && !tc.Lawn(nx, ny) && tc.In(nx, ny) && g.Walkable(nx, ny)) side = EdgeKind.Kerb;
                else if (ln == lc && tc.Water(nx, ny) && !tc.Water(x, y) && g.Walkable(x, y)) side = EdgeKind.PondFence;

                if (ck == CornerKind.TerrainCut || ck == CornerKind.LawnCut || ck == CornerKind.PaveCut)
                {
                    var arc = tc.Arc(x, y, k);
                    EdgeKind arcKind = ck == CornerKind.TerrainCut ? EdgeKind.Drop : ck == CornerKind.LawnCut ? EdgeKind.Kerb : EdgeKind.None;
                    Vector2Int s = CornerSigns[k];
                    int arcLow = tc.Lv(x + s.x, y);
                    for (int i = 0; i < arc.Count - 1; i++) { pts.Add(arc[i]); kinds.Add(arcKind); lows.Add(arcLow); sides.Add(-1); }
                    pts.Add(arc[arc.Count - 1]);
                    // The corner piece cut away: lower ground (terrain), cobbles or lawn round the corner.
                    var piece = new List<Vector2> { tc.Corner(x, y, k) };
                    piece.AddRange(arc);
                    if (ck == CornerKind.TerrainCut) TopPoly(Pile(piles, cell, tc.Surface(x + s.x, y)), piece, arcLow * g.step + Lift);
                    else if (ck == CornerKind.LawnCut) TopPoly(Pile(piles, cell, MatPaving), piece, yc);
                    else
                    {
                        TopPoly(Pile(piles, cell, MatLawn), piece, yc);
                        PieceTrim(piles, cell, piece, yc, mode == RailingMode.DropsAndPaths);
                    }
                }
                else pts.Add(tc.Corner(x, y, k));
                kinds.Add(side);
                lows.Add(ln);
                sides.Add(k);

                if (ck == CornerKind.Fillet)
                {
                    // Inner corner: the higher ground round this corner sweeps into this cell in a curve.
                    Vector2Int s = CornerSigns[k];
                    int lu = tc.Lv(x + s.x, y);
                    var piece = new List<Vector2> { tc.Corner(x, y, k) };
                    piece.AddRange(tc.Arc(x, y, k));
                    float yu = lu * g.step + Lift;
                    TopPoly(Pile(piles, cell, tc.Surface(x + s.x, y)), piece, yu);
                    var arc = tc.Arc(x, y, k);
                    bool railed = mode != RailingMode.None && (g.Paved(x + s.x, y) || g.Paved(x, y + s.y) || tc.Water(x, y));
                    for (int i = 0; i < arc.Count - 1; i++)
                    {
                        // Walls face out of the piece (towards this cell's middle).
                        Vector2 a = arc[i], b = arc[i + 1];
                        Vector2 mid = (a + b) / 2f, toCorner = tc.Corner(x, y, k) - mid;
                        if (Vector2.Dot(Right(b - a), toCorner) > 0f) { var tmp = a; a = b; b = tmp; }
                        Wall(Pile(piles, cell, MatWall), a, b, yc, yu);
                        DropTrim(piles, cell, a, b, yu, railed, colliders);
                    }
                }
            }
            // Order the corner points anticlockwise (they already are) and lay the top.
            TopPoly(Pile(piles, cell, tc.Surface(x, y)), pts, yc);

            // Walls and trim along the outline.
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                EdgeKind ek = kinds[i];
                if (ek == EdgeKind.None || (a - b).sqrMagnitude < 0.0001f) continue;
                float yl = lows[i] * g.step + Lift;
                if (ek == EdgeKind.Drop || ek == EdgeKind.StairWall) Wall(Pile(piles, cell, MatWall), a, b, yl, yc);
                if (ek == EdgeKind.StairWall) continue;
                // Straight sides stop short where a neighbour's rounded piece takes over the corner.
                Vector2 sa = a, sb = b;
                int side = sides[i];
                if (side >= 0)
                {
                    int k0 = side, k1 = (side + 1) % 4;
                    var c0 = tc.NeighbourCorner(x, y, side, k0);
                    var c1 = tc.NeighbourCorner(x, y, side, k1);
                    Vector2 dir = (b - a).normalized;
                    if ((c0 == CornerKind.Fillet || c0 == CornerKind.PaveCut) && (a - tc.Corner(x, y, k0)).sqrMagnitude < 0.0001f) sa = a + dir * tc.r;
                    if ((c1 == CornerKind.Fillet || c1 == CornerKind.PaveCut) && (b - tc.Corner(x, y, k1)).sqrMagnitude < 0.0001f) sb = b - dir * tc.r;
                    if (Vector2.Dot(sb - sa, dir) <= 0.01f) continue;
                }
                if (ek == EdgeKind.Drop)
                {
                    Vector2Int d = side >= 0 ? SideDirs[side] : Vector2Int.zero;
                    bool lowWater = side >= 0 ? tc.Water(x + d.x, y + d.y) : false;
                    bool railed = mode != RailingMode.None && (g.Paved(x, y) || lowWater);
                    DropTrim(piles, cell, sa, sb, yc, railed, colliders);
                }
                else if (ek == EdgeKind.Kerb)
                {
                    Prism(Pile(piles, cell, MatTrim), sa, sb, 0.3f, yc - 0.05f, yc + 0.16f, yc - 0.05f, yc + 0.16f);
                    if (mode == RailingMode.DropsAndPaths) Railing(piles, cell, Shift(sa, sa, sb, 0.45f), Shift(sb, sa, sb, 0.45f), yc, 0.55f, false, null);
                }
                else if (ek == EdgeKind.PondFence)
                {
                    Vector2 fa = Shift(sa, sa, sb, 0.25f), fb = Shift(sb, sa, sb, 0.25f);
                    Prism(Pile(piles, cell, MatTrim), fa, fb, 0.5f, yc - 0.05f, yc + 0.5f, yc - 0.05f, yc + 0.5f);
                    if (mode != RailingMode.None) Railing(piles, cell, fa, fb, yc + 0.5f, 0.6f, true, colliders);
                    else PondFenceCollider(colliders, sa, sb, yc);
                }
            }
        }

        /// <summary>Kerb (and low railing) round a lawn piece filling a path corner.</summary>
        private static void PieceTrim(Dictionary<long, MeshBuilder> piles, Vector2Int cell, List<Vector2> piece, float y, bool railing)
        {
            var poly = Ccw(piece);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                // Only the curved edge (the two straight edges lie against the lawn cells either side).
                bool straightFromCorner = (a - piece[0]).sqrMagnitude < 0.0001f || (b - piece[0]).sqrMagnitude < 0.0001f;
                if (straightFromCorner) continue;
                Prism(Pile(piles, cell, MatTrim), a, b, 0.3f, y - 0.05f, y + 0.16f, y - 0.05f, y + 0.16f);
                if (railing) Railing(piles, cell, Shift(a, a, b, 0.45f), Shift(b, a, b, 0.45f), y, 0.55f, false, null);
            }
        }

        /// <summary>Coping along the top of a drop (a -> b anticlockwise, high ground on the left), plus a railing on paths.</summary>
        private static void DropTrim(Dictionary<long, MeshBuilder> piles, Vector2Int cell, Vector2 a, Vector2 b, float y, bool railing, Transform colliders)
        {
            Vector2 ca = Shift(a, a, b, 0.22f), cb = Shift(b, a, b, 0.22f);
            Prism(Pile(piles, cell, MatTrim), ca, cb, 0.45f, y - 0.12f, y + 0.18f, y - 0.12f, y + 0.18f);
            if (railing) Railing(piles, cell, ca, cb, y + 0.18f, 0.95f, true, colliders);
        }

        /// <summary>Iron railing from a to b standing at height y: posts about every 2 m, a top rail (and a middle one when tall).</summary>
        private static void Railing(Dictionary<long, MeshBuilder> piles, Vector2Int cell, Vector2 a, Vector2 b, float y, float height, bool solid, Transform colliders)
        {
            float len = (b - a).magnitude;
            if (len < 0.05f) return;
            var iron = Pile(piles, cell, MatIron);
            Vector2 d = (b - a) / len;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)n);
                Prism(iron, p - d * 0.05f, p + d * 0.05f, 0.1f, y, y + height + 0.08f, y, y + height + 0.08f);
            }
            Prism(iron, a, b, 0.08f, y + height - 0.06f, y + height + 0.02f, y + height - 0.06f, y + height + 0.02f);
            if (height > 0.8f) Prism(iron, a, b, 0.06f, y + height * 0.48f - 0.03f, y + height * 0.48f + 0.03f, y + height * 0.48f - 0.03f, y + height * 0.48f + 0.03f);
            if (solid && colliders != null)
            {
                Vector2 mid = (a + b) / 2f;
                var go = Child(colliders, "Railing", new Vector3(mid.x, y + 0.6f, mid.y));
                go.localRotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
                go.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.25f, 1.2f, len + 0.1f);
            }
        }

        private static void PondFenceCollider(Transform colliders, Vector2 a, Vector2 b, float y)
        {
            Vector2 mid = (a + b) / 2f, d = (b - a).normalized;
            var go = Child(colliders, "PondFence", new Vector3(mid.x, y + 0.75f, mid.y));
            go.localRotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y));
            go.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.5f, 1.5f, (b - a).magnitude);
        }

        /// <summary>Point p moved 'inset' metres to the left of the line a -> b (into the high ground / the lawn).</summary>
        private static Vector2 Shift(Vector2 p, Vector2 a, Vector2 b, float inset)
        {
            Vector2 d = (b - a).normalized;
            return p + new Vector2(-d.y, d.x) * inset;
        }

        private static Vector2 Right(Vector2 d) { return new Vector2(d.y, -d.x); }

        private static List<Vector2> Ccw(List<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++) { Vector2 a = poly[i], b = poly[(i + 1) % poly.Count]; area += a.x * b.y - b.x * a.y; }
            if (area >= 0f) return poly;
            var r = new List<Vector2>(poly);
            r.Reverse();
            return r;
        }

        // ------------------------------------------------------------------ mesh building

        private class MeshBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            /// <summary>A triangle facing 'want' (Unity draws clockwise faces; this swaps the winding if needed).</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var tmp = b; b = c; c = tmp; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want) { Tri(a, b, c, want); Tri(a, c, d, want); }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>Flat top polygon at height y (fan from its first point, which suits convex cells and corner pieces).</summary>
        private static void TopPoly(MeshBuilder mb, List<Vector2> poly, float y)
        {
            for (int i = 1; i + 1 < poly.Count; i++)
                mb.Tri(new Vector3(poly[0].x, y, poly[0].y), new Vector3(poly[i].x, y, poly[i].y), new Vector3(poly[i + 1].x, y, poly[i + 1].y), Vector3.up);
        }

        /// <summary>Vertical wall along a -> b (high ground on the left), from y0 up to y1, facing right (outwards).</summary>
        private static void Wall(MeshBuilder mb, Vector2 a, Vector2 b, float y0, float y1)
        {
            if (y1 - y0 < 0.001f) return;
            Vector2 n = Right(b - a).normalized;
            mb.Quad(new Vector3(a.x, y0, a.y), new Vector3(b.x, y0, b.y), new Vector3(b.x, y1, b.y), new Vector3(a.x, y1, a.y), new Vector3(n.x, 0, n.y));
        }

        /// <summary>
        /// A beam from a to b, w wide, its bottom and top heights given at each end (sloped stringers and rails). All faces
        /// but the bottom.
        /// </summary>
        private static void Prism(MeshBuilder mb, Vector2 a, Vector2 b, float w, float y0a, float y1a, float y0b, float y1b)
        {
            Vector2 d = (b - a);
            if (d.sqrMagnitude < 0.000001f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (w / 2f);
            Vector3 A0 = new Vector3(a.x + n.x, y0a, a.y + n.y), A1 = new Vector3(a.x - n.x, y0a, a.y - n.y);
            Vector3 A2 = new Vector3(a.x - n.x, y1a, a.y - n.y), A3 = new Vector3(a.x + n.x, y1a, a.y + n.y);
            Vector3 B0 = new Vector3(b.x + n.x, y0b, b.y + n.y), B1 = new Vector3(b.x - n.x, y0b, b.y - n.y);
            Vector3 B2 = new Vector3(b.x - n.x, y1b, b.y - n.y), B3 = new Vector3(b.x + n.x, y1b, b.y + n.y);
            Vector3 n3 = new Vector3(n.x, 0, n.y), d3 = new Vector3(d.x, 0, d.y);
            mb.Quad(A3, A2, B2, B3, Vector3.up);
            mb.Quad(A0, A3, B3, B0, n3);
            mb.Quad(A1, A2, B2, B1, -n3);
            mb.Quad(A0, A1, A2, A3, -d3);
            mb.Quad(B0, B1, B2, B3, d3);
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
            // The PS1 textures are darker on average than the plain speckle; lift the tint so a colour looks the same.
            float gain = tex == null ? 1f : TexGain(tex.name);
            Material mat = PMat(name, new Color(color.r * gain, color.g * gain, color.b * gain, color.a), 1f);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = Vector2.one;
                mat.SetFloat("_WorldUV", tile);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>How much to lift a tint on each PS1 texture (about 0.8 / its average brightness).</summary>
        private static float TexGain(string texName)
        {
            switch (texName)
            {
                case "Cobble": return 1.3f;
                case "Flagstone": return 1.2f;
                case "Grass": return 1.25f;
                case "ForestFloor": return 1.5f;
                case "Dirt": return 1.4f;
                case "StoneWall": return 1.3f;
                case "Brick": return 1.4f;
                case "WoodPlanks": return 1.5f;
                case "Plaster": return 1.15f;
                case "Boards": return 1.45f;
                case "RoofTiles": return 1.6f;
                case "Leaves": return 1.7f;
                case "Hedge": return 1.6f;
                case "Bark": return 1.6f;
                default: return 1f;
            }
        }

        /// <summary>
        /// A street's building, roof, low-wall, tree and trunk materials, textured for its style (ZoneTextureStyle) and tinted
        /// with its colours.
        /// </summary>
        private static void StreetMats(ZoneDefinition z, string k, out Material building, out Material roof, out Material low, out Material leaves, out Material trunk)
        {
            ZoneTextureStyle st = z.textureStyle;
            string wallTex = st == ZoneTextureStyle.Docks ? StreetTexDir + "/Boards.png" : st == ZoneTextureStyle.Forge ? TerrainTexDir + "/Brick.png"
                : st == ZoneTextureStyle.Keep || st == ZoneTextureStyle.Forest ? TerrainTexDir + "/StoneWall.png" : StreetTexDir + "/Plaster.png";
            float wallTile = st == ZoneTextureStyle.Forge ? 2.2f : st == ZoneTextureStyle.Docks ? 3f : st == ZoneTextureStyle.Keep ? 3.6f : 4f;
            building = PsxTexMat(k + "_Building", z.buildingColor, TileTexture(wallTex), wallTile);
            roof = PsxTexMat(k + "_Roof", z.roofColor, TileTexture(StreetTexDir + "/RoofTiles.png"), 2.6f);
            bool hedge = st == ZoneTextureStyle.Garden || st == ZoneTextureStyle.Forest;
            low = PsxTexMat(k + "_LowWall", z.lowWallColor, TileTexture(hedge ? StreetTexDir + "/Hedge.png" : st == ZoneTextureStyle.Forge ? TerrainTexDir + "/Brick.png" : TerrainTexDir + "/StoneWall.png"),
                hedge ? 1.6f : 2.2f);
            leaves = PsxTexMat(k + "_Tree", z.treeColor, TileTexture(StreetTexDir + "/Leaves.png"), 2.2f);
            trunk = PsxTexMat(k + "_Trunk", new Color(0.3f, 0.2f, 0.13f), TileTexture(StreetTexDir + "/Bark.png"), 1.4f);
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
