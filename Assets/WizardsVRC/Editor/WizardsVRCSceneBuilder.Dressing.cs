using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Street dressing: the last layer of every street and town built from a map, so it reads as a place people live
    /// and work in rather than a set of blocks. Run at the end of BuildZone (everything else is standing by then), it adds
    ///   - things against the house walls and low walls (barrels, jars, planters, sacks, coils of rope, ore, oil drums...),
    ///     each street from its own kit, with bigger pieces (a cart, a well, a kiln, a boiler) tucked into corners;
    ///   - things on the walls themselves (ivy, window boxes, hanging baskets, lanterns, life rings, crystals, gauges);
    ///   - greenery: grass tufts, flowers and bushes on the lawns, flower borders along the paths, lumpier crowns, roots
    ///     and underplanting round the trees, rounded mounds along the hedge tops, reeds and lily pads on the water;
    ///   - strings of lanterns, pennants or bulbs across the narrow streets;
    ///   - drifting particles (WizardsVRCSceneBuilder.DressingFx.cs): chimney smoke, falling leaves and petals,
    ///     butterflies, fireflies, moths round the lamps, sea mist, embers, blowing dust, drips, steam, spores.
    /// The kits and the pieces themselves are in WizardsVRCSceneBuilder.DressingProps.cs.
    ///
    /// Where it won't put things: on or beside the entrance, NPCs, quest points and landmarks; on stairs or in the tunnel's
    /// way; inside dungeons; round the tower sigil; and anywhere something already stands (it measures what's been built:
    /// lamp posts, porch posts, doors, low windows, pipes, sigils). Within a duel ring's reach of a ring spot it only puts
    /// low greenery and things on the walls: nothing you'd bump into, nothing in a seat's way. Everything is seeded by the
    /// zone's key, so rebuilds look the same. The amount is the zone's Street Dressing setting (0 = none).
    ///
    /// Cost: the pieces are static and share a small set of materials, so the last pass (Merge.cs) folds them into a few
    /// meshes per 32 m chunk, and all but the biggest drop out beyond the zone's Detail Distance (Culling.cs). The
    /// particle emitters have small budgets, pause off screen and are culled by distance in chunks of their own.
    /// Original designs throughout: nothing here copies another game's art or names.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        // ------------------------------------------------------------------ notes taken while the street is built

        private const int HousePlain = 0, HouseWindows = 1, HousePorch = 2, HouseShopfront = 3;

        private class HouseNote
        {
            public RectInt rect;
            /// <summary>The map step from the building to its front (0,0 when it has none).</summary>
            public Vector2Int front;
            /// <summary>Height of its flat wall above the street (for Bigdig, the shopfront storey under the overhang).</summary>
            public float height;
            /// <summary>HousePlain, HouseWindows (lit windows 3.2 m up on faces that look onto a road), HousePorch
            /// (a boardwalk and porch along the front), HouseShopfront (door, awning and signs on the front).</summary>
            public int kind;
        }

        private static readonly Dictionary<Vector2Int, HouseNote> _houseNotes = new Dictionary<Vector2Int, HouseNote>();
        /// <summary>Tops of house chimneys and of rooftop smokestacks, lamp glows (zone-local): smoke and moths go there.</summary>
        private static readonly List<Vector3> _chimneyTops = new List<Vector3>();
        private static readonly List<Vector3> _stackTops = new List<Vector3>();
        private static readonly List<Vector3> _lampTops = new List<Vector3>();

        private static void ResetDressingNotes()
        {
            _houseNotes.Clear();
            _chimneyTops.Clear();
            _stackTops.Clear();
            _lampTops.Clear();
        }

        /// <summary>Called by each house builder: what stands on these cells, how tall its wall is and which way it faces.</summary>
        private static void NoteHouse(RectInt r, Vector2Int front, float height, int kind)
        {
            var note = new HouseNote { rect = r, front = front, height = height, kind = kind };
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++) _houseNotes[new Vector2Int(x, y)] = note;
        }

        // ------------------------------------------------------------------ what's already standing

        /// <summary>
        /// Boxes round everything built so far (zone-local), filed under the map cells they reach, so a new piece can ask
        /// whether its spot is free. The ground, the sea and whole blocks of buildings are left out (too big to be "in the way").
        /// </summary>
        private class DressOcc
        {
            private readonly ZoneGrid g;
            private readonly List<Bounds>[,] cells;
            /// <summary>A box is filed under every cell within this far of it, so a query only looks in its own cell.</summary>
            private const float Reach = 2.2f;
            private const float Pad = 0.06f;

            public DressOcc(ZoneGrid grid)
            {
                g = grid;
                cells = new List<Bounds>[Mathf.Max(1, g.width), Mathf.Max(1, g.height)];
            }

            public Vector2Int CellOf(Vector3 p)
            {
                return new Vector2Int(Mathf.FloorToInt(p.x / g.cell + g.width * 0.5f), Mathf.FloorToInt(g.height * 0.5f - p.z / g.cell));
            }

            public void Add(Bounds b)
            {
                Vector2Int a = CellOf(new Vector3(b.min.x - Reach, 0f, b.max.z + Reach));
                Vector2Int c = CellOf(new Vector3(b.max.x + Reach, 0f, b.min.z - Reach));
                for (int y = Mathf.Max(0, a.y); y <= Mathf.Min(g.height - 1, c.y); y++)
                    for (int x = Mathf.Max(0, a.x); x <= Mathf.Min(g.width - 1, c.x); x++)
                    {
                        if (cells[x, y] == null) cells[x, y] = new List<Bounds>();
                        cells[x, y].Add(b);
                    }
            }

            public void Gather(Transform root)
            {
                Matrix4x4 toRoot = root.worldToLocalMatrix;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = mf.sharedMesh;
                    if (mesh == null) continue;
                    // The ground itself (paving, lawns, kerbs, pond beds, water) isn't in the way; its railings are.
                    string n = mf.name;
                    if ((n.StartsWith("TerrainMesh_") && !n.EndsWith("Railings")) || n == "Water" || n == "OuterSea" || n == "Ground") continue;
                    Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                    Bounds lb = mesh.bounds;
                    Vector3 c = m.MultiplyPoint3x4(lb.center);
                    Vector3 ex = m.MultiplyVector(new Vector3(lb.extents.x, 0f, 0f));
                    Vector3 ey = m.MultiplyVector(new Vector3(0f, lb.extents.y, 0f));
                    Vector3 ez = m.MultiplyVector(new Vector3(0f, 0f, lb.extents.z));
                    Vector3 e = new Vector3(Mathf.Abs(ex.x) + Mathf.Abs(ey.x) + Mathf.Abs(ez.x), Mathf.Abs(ex.y) + Mathf.Abs(ey.y) + Mathf.Abs(ez.y),
                        Mathf.Abs(ex.z) + Mathf.Abs(ey.z) + Mathf.Abs(ez.z));
                    if (e.x > 7f || e.z > 7f) continue;
                    var b = new Bounds(c, e * 2f);
                    if (n.Contains("Door")) b.Expand(new Vector3(3.2f, 0f, 3.2f));   // nothing set down on a doorstep
                    Add(b);
                }
            }

            /// <summary>
            /// Is the ground free for a piece standing at p (its feet), hx by hz half-sizes? Anything from the ground to
            /// 2.4 m up is in the way, except a low step wide enough to stand on (a boardwalk): the piece goes up on it
            /// ('lift') if it fits on it whole.
            /// </summary>
            public bool Free(Vector3 p, float hx, float hz, out float lift)
            {
                lift = 0f;
                Vector2Int c = CellOf(p);
                if (c.x < 0 || c.y < 0 || c.x >= g.width || c.y >= g.height) return false;
                var list = cells[c.x, c.y];
                if (list == null) return true;
                foreach (var b in list)
                {
                    if (b.min.x > p.x + hx + Pad || b.max.x < p.x - hx - Pad || b.min.z > p.z + hz + Pad || b.max.z < p.z - hz - Pad) continue;
                    float top = b.max.y - p.y;
                    if (top < -0.05f || b.min.y - p.y > 2.4f) continue;
                    bool step = top >= 0.12f && top <= 0.5f && Mathf.Min(b.size.x, b.size.z) >= 1.2f;
                    if (step && b.min.x <= p.x - hx && b.max.x >= p.x + hx && b.min.z <= p.z - hz && b.max.z >= p.z + hz)
                    {
                        lift = Mathf.Max(lift, top);
                        continue;
                    }
                    return false;
                }
                return true;
            }

            /// <summary>
            /// Is a stretch of wall free, from y0 to y1 above p (a point at the wall's foot)? The buildings themselves
            /// don't count; windows, doors, signs, pipes, posts and lamps do.
            /// </summary>
            public bool FaceFree(Vector3 p, float hx, float hz, float y0, float y1)
            {
                Vector2Int c = CellOf(p);
                if (c.x < 0 || c.y < 0 || c.x >= g.width || c.y >= g.height) return false;
                var list = cells[c.x, c.y];
                if (list == null) return true;
                foreach (var b in list)
                {
                    if (Mathf.Min(b.size.x, b.size.z) >= 3f) continue;
                    if (b.min.x > p.x + hx + Pad || b.max.x < p.x - hx - Pad || b.min.z > p.z + hz + Pad || b.max.z < p.z - hz - Pad) continue;
                    if (b.max.y < p.y + y0 || b.min.y > p.y + y1) continue;
                    return false;
                }
                return true;
            }

            public void Claim(Vector3 p, float hx, float hz, float y0, float y1)
            {
                Add(new Bounds(new Vector3(p.x, p.y + (y0 + y1) * 0.5f, p.z), new Vector3(hx * 2f, Mathf.Max(0.05f, y1 - y0), hz * 2f)));
            }
        }

        // ------------------------------------------------------------------ the pass's state

        private delegate void DressMake(DressCtx d, Vector3 p, Vector3 o);
        private delegate void DressString(DressCtx d, Vector3 a, Vector3 b);

        /// <summary>
        /// One kind of piece: how often it's picked, its half-length along the wall and half-depth out from it, how tall
        /// its collider is (0 = you walk through it: greenery, small things), and what makes it at p facing o (out from
        /// the wall). For a piece on a wall's face, y0..y1 is the stretch of wall it covers.
        /// </summary>
        private class DressProp
        {
            public float weight, half, depth, tall, y0, y1;
            public DressMake make;
            public bool Small { get { return tall <= 0f && half <= 0.45f; } }
        }

        private static DressProp Dp(float weight, float half, float depth, float tall, DressMake make)
        {
            return new DressProp { weight = weight, half = half, depth = depth, tall = tall, make = make };
        }

        /// <summary>A piece on a wall's face, covering y0..y1 of it.</summary>
        private static DressProp Df(float weight, float half, float y0, float y1, DressMake make)
        {
            return new DressProp { weight = weight, half = half, depth = 0.25f, y0 = y0, y1 = y1, make = make };
        }

        /// <summary>A street style's pieces and how thickly they're laid on (see DressKitFor, DressingProps.cs).</summary>
        private class DressKit
        {
            public DressProp[] wall, nook, face, lawn, top;
            /// <summary>Pieces per 8 m of wall (ground, face), per lawn cell (lawn) and per low-wall cell (top).</summary>
            public float wallRate = 1.1f, faceRate = 0.45f, lawnRate = 0.6f, topRate = 2.2f, nookChance = 0.5f;
            /// <summary>Grass tufts and flower clumps per lawn cell; share of windows that get a flower box.</summary>
            public float tufts = 7f, flowers = 1.2f, windowBoxes;
            /// <summary>Flower borders along the paths; greenery hanging down the low walls.</summary>
            public bool borders, drapes;
            public DressString hang;
            public float hangChance = 0.3f;
        }

        private class DressCtx
        {
            public ZoneGrid g;
            public ZoneDefinition z;
            public ZoneTextureStyle st;
            public Transform holder, fx;
            public System.Random rng;
            public DressMats m;
            public DressOcc occ;
            public float cs, density;
            /// <summary>Cells nothing goes on; cells only small soft things go on (beside NPCs, points, the way in).</summary>
            public readonly HashSet<Vector2Int> none = new HashSet<Vector2Int>();
            public readonly HashSet<Vector2Int> tight = new HashSet<Vector2Int>();
            /// <summary>Duel ring spots (zone-local).</summary>
            public readonly List<Vector3> rings = new List<Vector3>();
            /// <summary>Places to keep right off: x, y, z and a radius in w.</summary>
            public readonly List<Vector4> keepOut = new List<Vector4>();
            public Dictionary<Vector2Int, float> water;
            /// <summary>Height of the wall the piece being made stands at; which way "along the wall" runs for it (1 or -1).</summary>
            public float wallH = 4f, flip = 1f;
            /// <summary>The street's breeze (flat, unit length): smoke, leaves and dust all lean the same way.</summary>
            public Vector3 wind = Vector3.right;
            public int props, pieces, emitters, propFx, propFxMax = 14;

            public float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
            public bool Chance(float p) { return rng.NextDouble() < p; }
            public int Pick(int n) { return rng.Next(Mathf.Max(1, n)); }
            /// <summary>A whole number averaging 'want' (2.3 is two, and a third 30% of the time).</summary>
            public int Count(float want)
            {
                int n = Mathf.FloorToInt(Mathf.Max(0f, want));
                return Chance(want - n) ? n + 1 : n;
            }
            public Quaternion Yaw() { return Quaternion.Euler(0f, R(0f, 360f), 0f); }
            public Vector3 Side(Vector3 o) { return Vector3.Cross(Vector3.up, o) * flip; }
            public Material Leaf() { return m.leaf[rng.Next(m.leaf.Length)]; }
            public Material Petal() { return m.petal[rng.Next(m.petal.Length)]; }
            public Material Pot() { return m.pots[rng.Next(m.pots.Length)]; }
            public Material Paint() { return m.paint[rng.Next(m.paint.Length)]; }

            public GameObject Add(string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
            {
                pieces++;
                return Shape(holder, name, mesh, pos, scale, mat, false, rot);
            }

            public GameObject Add(string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat)
            {
                return Add(name, mesh, pos, scale, mat, Quaternion.identity);
            }

            public GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
            {
                pieces++;
                return Deco(holder, PrimitiveType.Cube, name, pos, scale, mat, rot, false);
            }

            /// <summary>Small greenery: as Add, but it casts no shadow.</summary>
            public GameObject Soft(string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
            {
                GameObject go = Add(name, mesh, pos, scale, mat, rot);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go;
            }

            public bool NearRing(Vector3 p, float extra)
            {
                foreach (var r in rings)
                {
                    float dx = r.x - p.x, dz = r.z - p.z;
                    float reach = DuelCircleRadius + extra;
                    if (dx * dx + dz * dz < reach * reach && Mathf.Abs(r.y - p.y) < 6f) return true;
                }
                return false;
            }

            public bool KeptOut(Vector3 p)
            {
                foreach (var k in keepOut)
                {
                    float dx = k.x - p.x, dz = k.z - p.z;
                    if (dx * dx + dz * dz < k.w * k.w) return true;
                }
                return false;
            }

            /// <summary>Only small soft things here: beside something special, or within a duel ring's reach of a ring spot.</summary>
            public bool Tight(Vector2Int cell, Vector3 p) { return tight.Contains(cell) || NearRing(p, 1.3f); }

            public DressProp Choose(DressProp[] kit, bool smallOnly)
            {
                if (kit == null) return null;
                float total = 0f;
                foreach (var k in kit) if (!smallOnly || k.Small) total += k.weight;
                if (total <= 0f) return null;
                float roll = R(0f, total);
                DressProp last = null;
                foreach (var k in kit)
                {
                    if (smallOnly && !k.Small) continue;
                    last = k;
                    roll -= k.weight;
                    if (roll <= 0f) return k;
                }
                return last;
            }

            /// <summary>A prop's own puff of particles (embers off a brazier, steam off a vent): a few per street.</summary>
            public bool PropFx()
            {
                if (propFx >= propFxMax) return false;
                propFx++;
                return true;
            }
        }

        // ------------------------------------------------------------------ the pass

        private static void DressStreet(Transform root, Transform layout, ZoneGrid g, ZoneDefinition z, Vector2Int start, HashSet<Vector2Int> stairCells,
            HashSet<Vector2Int> noHouse, List<Vector2Int> ringCells, Vector2Int towerSite, Material glow, Material roof)
        {
            float density = Mathf.Clamp(z.streetDressing, 0f, 2f);
            if (density <= 0.01f || g.width < 3 || g.height < 3) return;
            var d = new DressCtx
            {
                g = g, z = z, st = z.textureStyle, cs = g.cell, density = density,
                rng = new System.Random(StableSeed("dress:" + z.zoneKey)),
                holder = Child(layout, "Dressing", Vector3.zero),
                fx = Child(layout, "DressingFx", Vector3.zero),
            };
            d.m = MakeDressMats(z, z.zoneKey, glow, roof);
            d.water = WaterSurfaces(g);
            float windYaw = d.R(0f, Mathf.PI * 2f);
            d.wind = new Vector3(Mathf.Cos(windYaw), 0f, Mathf.Sin(windYaw));
            d.propFxMax = Mathf.RoundToInt(14f * Mathf.Clamp(density, 0.3f, 1.5f));
            // What's been built so far (measured before our own holder has anything in it).
            d.occ = new DressOcc(g);
            d.occ.Gather(root);

            // ---- where not to
            var lawns = new List<Vector2Int>();
            var trees = new List<Vector2Int>();
            var waters = new List<Vector2Int>();
            var lowWalls = new List<Vector2Int>();
            var roads = new List<Vector2Int>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    char c = g.At(x, y);
                    var cell = new Vector2Int(x, y);
                    if (c == 'S' || g.IsCustom(c))
                    {
                        d.none.Add(cell);
                        int reach = c == 'S' ? 2 : 1;
                        for (int yy = y - reach; yy <= y + reach; yy++)
                            for (int xx = x - reach; xx <= x + reach; xx++) d.tight.Add(new Vector2Int(xx, yy));
                        continue;
                    }
                    if (ZoneGrid.IsRingDigit(c)) d.rings.Add(g.Local(cell));
                    if (c == ',') lawns.Add(cell);
                    else if (c == '^') trees.Add(cell);
                    else if (c == '~') waters.Add(cell);
                    else if (c == '=') lowWalls.Add(cell);
                    else if (c == '.' || ZoneGrid.IsPatrolLetter(c) || ZoneGrid.IsRingDigit(c)) roads.Add(cell);
                }
            foreach (var c in stairCells)
            {
                d.none.Add(c);
                foreach (var dir in ZoneGrid.Dirs) d.tight.Add(c + dir);
            }
            foreach (var c in noHouse) d.none.Add(c);
            if (_dungeonCells != null) foreach (var c in _dungeonCells) d.none.Add(c);
            foreach (var c in ringCells) d.rings.Add(g.Local(c));
            if (towerSite.x >= 0)
            {
                Vector3 t = g.Local(towerSite);
                d.keepOut.Add(new Vector4(t.x, t.y, t.z, DuelCircleRadius + 5f));
            }
            Vector3 way = g.Local(start);
            d.keepOut.Add(new Vector4(way.x, way.y, way.z, 7f));

            // ---- the layers
            DressKit kit = DressKitFor(d);
            DressWalls(d, kit);
            DressLawns(d, kit, lawns);
            DressTrees(d, kit, trees);
            DressLowWalls(d, kit, lowWalls);
            DressWater(d, waters);
            DressStrings(d, kit);
            DressFx(d, lawns, trees, waters, roads);

            // Static, so the last pass merges them and the small ones cull with distance; the emitters stay as they are.
            foreach (var t in d.holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            DressFxCulling(d.fx, Mathf.Max(70f, z.detailDistance) * 1.25f);
            Debug.Log("[WizardsVRC World] " + z.displayName + ": dressed with " + d.props + " props and plants (" + d.pieces + " pieces) and " +
                      d.emitters + " particle effects.");
        }

        // ------------------------------------------------------------------ walls

        /// <summary>A plain open cell (road, lawn, a patrol's line, a ring's plaza): the only kind things are set down in.</summary>
        private static bool DressOpen(DressCtx d, Vector2Int cell)
        {
            if (d.none.Contains(cell) || !d.g.Walkable(cell.x, cell.y)) return false;
            char c = d.g.At(cell.x, cell.y);
            return c == '.' || c == ',' || ZoneGrid.IsPatrolLetter(c) || ZoneGrid.IsRingDigit(c);
        }

        private static void DressWalls(DressCtx d, DressKit kit)
        {
            ZoneGrid g = d.g;
            float cs = d.cs;
            // On a big map, thin the walls out so the whole street stays within a budget.
            int edges = 0;
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!DressOpen(d, cell)) continue;
                    foreach (var dir in ZoneGrid.Dirs)
                        if (_houseNotes.ContainsKey(cell + dir) || g.At(x + dir.x, y + dir.y) == '=') edges++;
                }
            float thin = Mathf.Min(1f, 460f / Mathf.Max(1f, edges * (cs / 8f)));
            float per = d.density * thin * (cs / 8f);

            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!DressOpen(d, cell)) continue;
                    Vector3 centre = g.Local(cell) + new Vector3(0f, 0.02f, 0f);
                    bool[] walled = new bool[4];
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2Int dir = ZoneGrid.Dirs[i];
                        Vector2Int n = cell + dir;
                        if (n.x < 0 || n.y < 0 || n.x >= g.width || n.y >= g.height || g.Level(n) != g.Level(cell)) continue;
                        HouseNote note;
                        bool house = _houseNotes.TryGetValue(n, out note);
                        bool low = !house && g.At(n.x, n.y) == '=';
                        if (!house && !low) continue;
                        walled[i] = true;
                        Vector3 toWall = ZoneGrid.ToLocalDir(dir);
                        Vector3 o = -toWall;
                        Vector3 foot = centre + toWall * (cs * 0.5f);
                        if (d.KeptOut(foot)) continue;
                        bool front = house && note.front == new Vector2Int(-dir.x, -dir.y);
                        bool busyFront = front && (note.kind == HousePorch || note.kind == HouseShopfront);
                        float gap = front && note.kind == HousePorch ? 0.5f : 0.18f;
                        d.wallH = house ? note.height : 1.4f;

                        // Things standing against it.
                        int count = d.Count(kit.wallRate * per * (low ? 0.55f : 1f));
                        for (int k = 0; k < count; k++) DressAgainstWall(d, kit.wall, cell, foot, o, gap);

                        if (house && !busyFront)
                        {
                            // Things on its face, and flower boxes under the windows that look out here.
                            int faces = d.Count(kit.faceRate * per);
                            for (int k = 0; k < faces; k++) DressOnFace(d, kit.face, foot, o);
                            if (note.kind == HouseWindows && kit.windowBoxes > 0f && g.Hash(n.x, n.y, 10 - dir.x * 3 - dir.y) <= 0.55f &&
                                d.Chance(kit.windowBoxes * Mathf.Min(1f, d.density)))
                            {
                                Vector3 sill = foot + new Vector3(0f, 2.36f, 0f);
                                if (d.occ.FaceFree(foot, Mathf.Abs(o.x) > 0.5f ? 0.3f : 0.7f, Mathf.Abs(o.x) > 0.5f ? 0.7f : 0.3f, 2.1f, 2.44f))
                                {
                                    DrWindowBox(d, sill, o);
                                    d.occ.Claim(foot + o * 0.2f, Mathf.Abs(o.x) > 0.5f ? 0.3f : 0.7f, Mathf.Abs(o.x) > 0.5f ? 0.7f : 0.3f, 1.9f, 2.9f);
                                    d.props++;
                                }
                            }
                        }
                        else if (low && kit.drapes && d.Chance(0.4f * Mathf.Min(1f, per)))
                        {
                            d.flip = d.Chance(0.5f) ? 1f : -1f;
                            DrDrape(d, foot + d.Side(o) * d.R(-0.35f, 0.35f) * cs, o);
                            d.props++;
                        }
                    }

                    // A corner between two walls: room for something bigger, out of everyone's way.
                    if (kit.nook == null || d.tight.Contains(cell)) continue;
                    for (int i = 0; i < 4; i++)
                    {
                        int j = (i + 1) % 4;
                        if (!walled[i] || !walled[j] || !d.Chance(kit.nookChance * Mathf.Min(1.2f, d.density * thin))) continue;
                        DressProp prop = d.Choose(kit.nook, false);
                        if (prop == null) continue;
                        Vector3 w0 = ZoneGrid.ToLocalDir(ZoneGrid.Dirs[i]), w1 = ZoneGrid.ToLocalDir(ZoneGrid.Dirs[j]);
                        Vector3 o = -w0;
                        Vector3 p = centre + w0 * (cs * 0.5f - prop.depth - 0.55f) + w1 * (cs * 0.5f - prop.half - 0.55f);
                        if (d.KeptOut(p) || d.NearRing(p, 2.2f)) continue;
                        float hx = Mathf.Abs(o.x) > 0.5f ? prop.depth : prop.half, hz = Mathf.Abs(o.x) > 0.5f ? prop.half : prop.depth;
                        float lift;
                        if (!d.occ.Free(p, hx, hz, out lift)) continue;
                        p.y += lift;
                        // "Along the wall" runs away from the other wall, so a cart's shafts point out into the street.
                        d.flip = Vector3.Dot(Vector3.Cross(Vector3.up, o), w1) > 0f ? -1f : 1f;
                        HouseNote corner;
                        d.wallH = _houseNotes.TryGetValue(cell + ZoneGrid.Dirs[i], out corner) ? corner.height : 1.4f;
                        prop.make(d, p, o);
                        d.occ.Claim(p, hx, hz, 0f, Mathf.Max(1f, prop.tall));
                        DressSolid(d, p, o, prop);
                        d.props++;
                        break;
                    }
                }
        }

        /// <summary>One piece from the kit against the wall whose foot (middle of the cell's edge) is 'foot', facing o.</summary>
        private static bool DressAgainstWall(DressCtx d, DressProp[] kit, Vector2Int cell, Vector3 foot, Vector3 o, float gap)
        {
            bool tightCell = d.tight.Contains(cell);
            Vector3 along = Vector3.Cross(Vector3.up, o);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                DressProp prop = d.Choose(kit, tightCell);
                if (prop == null) return false;
                float room = d.cs * 0.5f - prop.half - 0.15f;
                if (room < 0f) continue;
                float t = d.R(-room, room);
                Vector3 p = foot + along * t + o * (gap + prop.depth);
                if (!prop.Small && d.NearRing(p, 1.3f))
                {
                    prop = d.Choose(kit, true);
                    if (prop == null) return false;
                    p = foot + along * t + o * (gap + prop.depth);
                }
                float hx = Mathf.Abs(o.x) > 0.5f ? prop.depth : prop.half, hz = Mathf.Abs(o.x) > 0.5f ? prop.half : prop.depth;
                float lift;
                if (!d.occ.Free(p, hx, hz, out lift)) continue;
                p.y += lift;
                d.flip = d.Chance(0.5f) ? 1f : -1f;
                prop.make(d, p, o);
                d.occ.Claim(p, hx, hz, 0f, Mathf.Max(0.6f, prop.tall));
                DressSolid(d, p, o, prop);
                d.props++;
                return true;
            }
            return false;
        }

        /// <summary>One piece from the kit on the wall's face above 'foot'.</summary>
        private static bool DressOnFace(DressCtx d, DressProp[] kit, Vector3 foot, Vector3 o)
        {
            Vector3 along = Vector3.Cross(Vector3.up, o);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                DressProp prop = d.Choose(kit, false);
                if (prop == null) return false;
                float y1 = Mathf.Min(prop.y1, d.wallH - 0.3f);
                if (y1 < prop.y0 + 0.4f) continue;      // the wall's too low for it
                float room = d.cs * 0.5f - prop.half - 0.25f;
                if (room < 0f) continue;
                Vector3 p = foot + along * d.R(-room, room);
                Vector3 mid = p + o * 0.24f;
                float hx = Mathf.Abs(o.x) > 0.5f ? 0.26f : prop.half, hz = Mathf.Abs(o.x) > 0.5f ? prop.half : 0.26f;
                if (!d.occ.FaceFree(mid, hx, hz, prop.y0, y1)) continue;
                d.flip = d.Chance(0.5f) ? 1f : -1f;
                prop.make(d, p, o);
                d.occ.Claim(mid, hx, hz, prop.y0, y1);
                d.props++;
                return true;
            }
            return false;
        }

        /// <summary>A simple collider for a piece you shouldn't walk through (a capsule for round ones, else a box).</summary>
        private static void DressSolid(DressCtx d, Vector3 p, Vector3 o, DressProp prop)
        {
            if (prop.tall <= 0f) return;
            var go = Child(d.holder, "DressSolid", p + new Vector3(0f, prop.tall * 0.5f, 0f)).gameObject;
            if (Mathf.Abs(prop.half - prop.depth) < 0.06f)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.radius = prop.half;
                cap.height = Mathf.Max(prop.tall, prop.half * 2f);
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = Mathf.Abs(o.x) > 0.5f ? new Vector3(prop.depth * 2f, prop.tall, prop.half * 2f) : new Vector3(prop.half * 2f, prop.tall, prop.depth * 2f);
            }
        }

        // ------------------------------------------------------------------ lawns

        private static void DressLawns(DressCtx d, DressKit kit, List<Vector2Int> lawns)
        {
            ZoneGrid g = d.g;
            float cs = d.cs;
            float area = cs * cs / 64f;
            float thin = Mathf.Min(1f, 210f / Mathf.Max(1f, lawns.Count * area));
            float per = d.density * thin * area;
            float reach = cs * 0.5f - 0.4f;
            foreach (var cell in lawns)
            {
                if (d.none.Contains(cell)) continue;
                Vector3 centre = g.Local(cell) + new Vector3(0f, 0.02f, 0f);
                if (d.KeptOut(centre)) continue;
                bool tight = d.Tight(cell, centre);

                int tufts = d.Count(kit.tufts * per);
                for (int k = 0; k < tufts; k++)
                    DrTuft(d, centre + new Vector3(d.R(-reach, reach), 0f, d.R(-reach, reach)), d.R(0.4f, 0.75f), d.m.tuft);
                int flowers = d.Count(kit.flowers * per);
                for (int k = 0; k < flowers; k++)
                    DrFlowers(d, centre + new Vector3(d.R(-reach, reach), 0f, d.R(-reach, reach)), 3 + d.Pick(3), 0.4f, 0.4f);

                // Bigger things (bushes, rocks...), away from ring spots.
                if (!tight && kit.lawn != null)
                {
                    int count = d.Count(kit.lawnRate * per);
                    for (int k = 0; k < count; k++)
                    {
                        DressProp prop = d.Choose(kit.lawn, false);
                        if (prop == null) break;
                        float r = cs * 0.5f - prop.half - 0.4f;
                        if (r < 0f) continue;
                        Vector3 p = centre + new Vector3(d.R(-r, r), 0f, d.R(-r, r));
                        if (d.NearRing(p, 1.3f)) continue;
                        float lift;
                        if (!d.occ.Free(p, prop.half, prop.half, out lift)) continue;
                        d.flip = d.Chance(0.5f) ? 1f : -1f;
                        prop.make(d, p, d.Yaw() * Vector3.forward);
                        d.occ.Claim(p, prop.half, prop.half, 0f, Mathf.Max(0.6f, prop.tall));
                        DressSolid(d, p, Vector3.forward, prop);
                        d.props++;
                    }
                }

                // A flower border along a path.
                if (!kit.borders) continue;
                foreach (var dir in ZoneGrid.Dirs)
                {
                    int nx = cell.x + dir.x, ny = cell.y + dir.y;
                    if (!g.Paved(nx, ny) || g.Level(nx, ny) != g.Level(cell) || !d.Chance(0.55f * Mathf.Min(1f, per))) continue;
                    Vector3 toPath = ZoneGrid.ToLocalDir(dir), along = Vector3.Cross(Vector3.up, toPath);
                    int clumps = 3 + d.Pick(3);
                    float t0 = d.R(-0.3f, 0.1f) * cs;
                    for (int k = 0; k < clumps; k++)
                        DrFlowers(d, centre + toPath * (cs * 0.5f - d.R(0.6f, 0.95f)) + along * (t0 + k * 0.8f), 3 + d.Pick(2), 0.3f, 0.45f);
                    d.props++;
                }
            }
        }

        // ------------------------------------------------------------------ trees

        private static void DressTrees(DressCtx d, DressKit kit, List<Vector2Int> trees)
        {
            ZoneGrid g = d.g;
            float thin = Mathf.Min(1f, 150f / Mathf.Max(1f, trees.Count));
            ZoneTextureStyle st = d.st;
            bool cactus = IsDesert(d.z) || st == ZoneTextureStyle.Mine;
            foreach (var cell in trees)
            {
                Vector3 p = g.Local(cell) + new Vector3(0f, 0.02f, 0f);
                if (d.KeptOut(p)) continue;
                float reach = Mathf.Min(d.cs * 0.5f - 0.5f, 3.4f);
                if (st == ZoneTextureStyle.Forest)
                {
                    // The wood is already thick (Forest.cs): just toadstools and roots at a few feet.
                    if (d.Chance(0.3f * d.density * thin)) DrRoots(d, p, 1f);
                    if (d.Chance(0.25f * d.density * thin)) DrMushrooms(d, p + d.Yaw() * new Vector3(d.R(1f, reach), 0f, 0f), 2 + d.Pick(3), 0.3f, d.Chance(0.5f) ? d.m.capRed : d.m.capTan);
                    continue;
                }
                if (st == ZoneTextureStyle.Undercity)
                {
                    // A stalagmite or a pipe stack: glowing toadstools, rubble and a puddle at its foot.
                    int bits = d.Count(2.2f * d.density * thin);
                    for (int k = 0; k < bits; k++)
                    {
                        Vector3 at = p + d.Yaw() * new Vector3(d.R(1.4f, reach), 0f, 0f);
                        int pick = d.Pick(4);
                        if (pick == 0) DrMushrooms(d, at, 2 + d.Pick(3), 0.32f, d.m.glowCap);
                        else if (pick == 1) DrBoulder(d, at, d.R(0.4f, 0.8f), false);
                        else if (pick == 2) DrPuddle(d, at, d.R(0.8f, 1.5f), d.m.sludge, true);
                        else DrTuft(d, at, d.R(0.4f, 0.7f), d.m.tuft);
                    }
                    continue;
                }
                if (cactus)
                {
                    // A saguaro: rocks, scrub and smaller cactus at its foot, and a flower on its crown.
                    int bits = d.Count(2.6f * d.density * thin);
                    for (int k = 0; k < bits; k++)
                    {
                        Vector3 at = p + d.Yaw() * new Vector3(d.R(1.1f, reach), 0f, 0f);
                        int pick = d.Pick(6);
                        if (pick == 0) DrBoulder(d, at, d.R(0.4f, 0.9f), false);
                        else if (pick == 1) DrBarrelCactus(d, at, d.R(0.5f, 0.9f));
                        else if (pick == 2) DrPricklyPear(d, at, d.R(0.7f, 1.1f));
                        else if (pick == 3) DrSage(d, at, d.R(0.7f, 1.2f));
                        else DrTuft(d, at, d.R(0.45f, 0.8f), d.m.tuft);
                    }
                    continue;
                }

                // A leafy tree (Zones.cs builds a trunk and one round crown): more lumps to the crown, roots, underplanting.
                float s = 0.8f + g.Hash(cell.x, cell.y, 4) * 0.5f;
                int lumps = thin < 0.6f ? 2 : 2 + d.Pick(3);
                float a0 = d.R(0f, 360f);
                for (int k = 0; k < lumps; k++)
                {
                    Vector3 off = Quaternion.Euler(0f, a0 + k * 360f / lumps + d.R(-25f, 25f), 0f) * new Vector3(d.R(1.3f, 1.8f) * s, 0f, 0f);
                    d.Add("CrownLump", BlobMesh(), p + off + new Vector3(0f, (4.2f + d.R(-0.9f, 0.7f)) * s, 0f),
                        new Vector3(d.R(2f, 2.6f), d.R(1.6f, 2.2f), d.R(2f, 2.6f)) * s, d.Leaf(), d.Yaw());
                }
                if (d.Chance(0.6f))
                    d.Add("CrownLump", BlobMesh(), p + new Vector3(d.R(-0.5f, 0.5f) * s, 5.5f * s, d.R(-0.5f, 0.5f) * s), new Vector3(2.4f, 1.9f, 2.4f) * s, d.Leaf(), d.Yaw());
                if (d.Chance(0.7f)) DrRoots(d, p, s);
                int under = d.Count(3.2f * d.density * thin);
                for (int k = 0; k < under; k++)
                {
                    Vector3 at = p + d.Yaw() * new Vector3(d.R(1.1f, reach), 0f, 0f);
                    int pick = d.Pick(10);
                    if (pick < 4) DrTuft(d, at, d.R(0.45f, 0.8f), d.m.tuft);
                    else if (pick < 6) DrFlowers(d, at, 3 + d.Pick(3), 0.35f, 0.4f);
                    else if (pick < 8) DrBush(d, at, d.R(0.6f, 1f), st == ZoneTextureStyle.Garden || d.Chance(0.3f));
                    else if (pick == 8) DrMushrooms(d, at, 2 + d.Pick(3), 0.26f, st == ZoneTextureStyle.Keep ? d.m.glowCap : d.Chance(0.5f) ? d.m.capRed : d.m.capTan);
                    else DrBoulder(d, at, d.R(0.35f, 0.7f), true);
                }
                d.props++;
            }
        }

        // ------------------------------------------------------------------ low walls

        private static void DressLowWalls(DressCtx d, DressKit kit, List<Vector2Int> lowWalls)
        {
            if (kit.top == null) return;
            ZoneGrid g = d.g;
            float cs = d.cs;
            float area = cs * cs / 64f;
            float thin = Mathf.Min(1f, 160f / Mathf.Max(1f, lowWalls.Count * area));
            foreach (var cell in lowWalls)
            {
                // The wall's top: 1.4 m of wall and its cap.
                Vector3 top = g.Local(cell) + new Vector3(0f, 1.51f, 0f);
                if (d.KeptOut(top)) continue;
                int count = d.Count(kit.topRate * d.density * thin * area);
                for (int k = 0; k < count; k++)
                {
                    DressProp prop = d.Choose(kit.top, false);
                    if (prop == null) break;
                    float r = cs * 0.5f - prop.half - 0.15f;
                    if (r < 0f) continue;
                    Vector3 p = top + new Vector3(d.R(-r, r), 0f, d.R(-r, r));
                    float lift;
                    if (!d.occ.Free(p + new Vector3(0f, 0.1f, 0f), prop.half * 0.8f, prop.half * 0.8f, out lift)) continue;
                    d.flip = d.Chance(0.5f) ? 1f : -1f;
                    d.wallH = 4f;
                    prop.make(d, p, d.Yaw() * Vector3.forward);
                    d.occ.Claim(p + new Vector3(0f, 0.1f, 0f), prop.half * 0.8f, prop.half * 0.8f, 0f, 1f);
                    d.props++;
                }
            }
        }

        // ------------------------------------------------------------------ water

        private static void DressWater(DressCtx d, List<Vector2Int> waters)
        {
            ZoneGrid g = d.g;
            float cs = d.cs;
            ZoneTextureStyle st = d.st;
            float thin = Mathf.Min(1f, 140f / Mathf.Max(1f, waters.Count)) * d.density;
            bool docks = st == ZoneTextureStyle.Docks, under = st == ZoneTextureStyle.Undercity, mine = st == ZoneTextureStyle.Mine;
            bool grim = st == ZoneTextureStyle.Keep || st == ZoneTextureStyle.Forge;
            foreach (var cell in waters)
            {
                float surface;
                if (!d.water.TryGetValue(cell, out surface)) continue;
                Vector3 c = g.Local(cell);
                c.y = surface;
                // The banks.
                foreach (var dir in ZoneGrid.Dirs)
                {
                    int nx = cell.x + dir.x, ny = cell.y + dir.y;
                    if (nx < 0 || ny < 0 || nx >= g.width || ny >= g.height || ZoneGrid.IsWater(g.At(nx, ny))) continue;
                    if (d.none.Contains(new Vector2Int(nx, ny))) continue;
                    Vector3 toBank = ZoneGrid.ToLocalDir(dir), o = -toBank, along = Vector3.Cross(Vector3.up, o);
                    Vector3 edge = c + toBank * (cs * 0.5f);
                    float bank = g.Elevation(nx, ny) - surface;
                    if (docks)
                    {
                        // A quay: mooring posts standing in the water, fenders hung down the wall.
                        if (bank > 0.6f && d.Chance(0.5f * thin))
                        {
                            Vector3 at = edge + o * 0.55f + along * d.R(-0.35f, 0.35f) * cs;
                            float h = bank + 1.9f;
                            d.Add("MooringPost", CylMesh(), at + new Vector3(0f, h * 0.5f - 1.1f, 0f), new Vector3(0.42f, h * 0.5f, 0.42f), d.m.bark, Quaternion.Euler(d.R(-4f, 4f), 0f, d.R(-4f, 4f)));
                            d.Add("MooringCap", DressDome(), at + new Vector3(0f, h - 1.1f, 0f), new Vector3(0.5f, 0.3f, 0.5f), d.m.iron);
                            if (d.Chance(0.6f)) d.Add("MooringRope", DressTorusFat(), at + new Vector3(0f, h - 1.45f, 0f), new Vector3(0.62f, 0.8f, 0.62f), d.m.rope);
                            d.props++;
                        }
                        if (bank > 0.9f && d.Chance(0.45f * thin))
                        {
                            Vector3 at = edge + o * 0.2f + along * d.R(-0.4f, 0.4f) * cs;
                            d.Box("FenderRope", at + new Vector3(0f, bank * 0.6f, 0f), new Vector3(0.04f, bank * 0.8f, 0.04f), d.m.rope, Quaternion.identity);
                            d.Add("Fender", BlobMesh(), at + new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.75f, 0.5f), d.Paint(), d.Yaw());
                            d.props++;
                        }
                    }
                    else if (under)
                    {
                        // A canal wall: an outfall pipe now and then, dribbling.
                        if (bank > 0.5f && d.Chance(0.3f * thin))
                        {
                            Vector3 at = edge + along * d.R(-0.35f, 0.35f) * cs + new Vector3(0f, Mathf.Min(bank * 0.55f, 0.9f), 0f);
                            d.Add("Outfall", CylMesh(), at + o * 0.25f, new Vector3(0.6f, 0.4f, 0.6f), d.m.rust, Quaternion.FromToRotation(Vector3.up, o));
                            d.Add("OutfallRim", DressTorusThin(), at + o * 0.62f, new Vector3(0.7f, 1.2f, 0.7f), d.m.brass, Quaternion.FromToRotation(Vector3.up, o));
                            if (d.PropFx()) SfxDrips(d, at + o * 0.7f - new Vector3(0f, 0.2f, 0f), new Vector3(0.15f, 0.05f, 0.15f), new Color(0.5f, 0.95f, 0.6f), 0.5f);
                            d.props++;
                        }
                    }
                    else if (bank < 1.3f)
                    {
                        // A pond's bank: reeds in the shallows (crystals round a mine's pools), a rock at the waterline.
                        if (d.Chance(0.6f * thin))
                        {
                            Vector3 at = edge + o * d.R(0.35f, 0.8f) + along * d.R(-0.38f, 0.38f) * cs;
                            if (mine) DrCrystals(d, at, d.R(0.5f, 0.9f));
                            else DrReeds(d, at, grim);
                            d.props++;
                        }
                        if (d.Chance(0.3f * thin))
                            DrBoulder(d, edge + o * d.R(0.2f, 0.5f) + along * d.R(-0.4f, 0.4f) * cs - new Vector3(0f, 0.1f, 0f), d.R(0.35f, 0.7f), !mine && !grim);
                    }
                }

                // Out on the water.
                Vector3 spot = c + new Vector3(d.R(-0.3f, 0.3f) * cs, 0.02f, d.R(-0.3f, 0.3f) * cs);
                if (docks)
                {
                    if (d.Chance(0.14f * thin)) { DrBuoy(d, spot); d.props++; }
                    else if (d.Chance(0.08f * thin)) { DrBarrelLying(d, spot - new Vector3(0f, 0.22f, 0f), d.Yaw() * Vector3.forward, 0.9f, d.m.wood, d.m.iron); d.props++; }
                }
                else if (under)
                {
                    if (d.Chance(0.16f * thin)) { DrDrum(d, spot - new Vector3(0f, 0.5f, 0f), 1f, d.Chance(0.4f) ? d.m.rust : d.Paint(), false); d.props++; }
                    if (d.Chance(0.3f * thin))
                        for (int k = 0; k < 3; k++)
                            d.Soft("GlowScum", DressDisc(), spot + new Vector3(d.R(-1.4f, 1.4f), 0.01f, d.R(-1.4f, 1.4f)), new Vector3(d.R(0.5f, 1.3f), 1f, d.R(0.5f, 1.3f)), d.m.glowCap, d.Yaw());
                }
                else if (!mine && !grim)
                {
                    if (d.Chance(0.5f * thin)) { DrLilyPads(d, spot, 3 + d.Pick(4)); d.props++; }
                }
            }
        }

        // ------------------------------------------------------------------ strings across the street

        /// <summary>
        /// Where two buildings face each other across a street one or two cells wide: a sagging string of lanterns,
        /// pennants or bulbs between them, high enough to walk under and not over a duel ring.
        /// </summary>
        private static void DressStrings(DressCtx d, DressKit kit)
        {
            if (kit.hang == null) return;
            ZoneGrid g = d.g;
            float cs = d.cs;
            int made = 0, max = Mathf.RoundToInt(20f * d.density);
            var steps = new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) };
            for (int y = 0; y < g.height && made < max; y++)
                for (int x = 0; x < g.width && made < max; x++)
                {
                    var cell = new Vector2Int(x, y);
                    HouseNote a;
                    if (!_houseNotes.TryGetValue(cell, out a)) continue;
                    foreach (var dir in steps)
                    {
                        int wide = 0;
                        while (wide < 3)
                        {
                            Vector2Int c2 = cell + dir * (wide + 1);
                            if (!DressOpen(d, c2) || g.Level(c2) != g.Level(cell)) break;
                            wide++;
                        }
                        if (wide < 1 || wide > 2) continue;
                        Vector2Int far = cell + dir * (wide + 1);
                        HouseNote b;
                        if (!_houseNotes.TryGetValue(far, out b) || g.Level(far) != g.Level(cell)) continue;
                        float h = Mathf.Min(a.height, b.height) - 0.7f;
                        if (h < 4.2f || !d.Chance(kit.hangChance * Mathf.Min(1.3f, d.density))) continue;
                        h = Mathf.Min(h, d.R(4.6f, 6.2f));
                        Vector3 across = ZoneGrid.ToLocalDir(dir), along = Vector3.Cross(Vector3.up, across);
                        float shift = d.R(-0.3f, 0.3f) * cs;
                        Vector3 p0 = g.Local(cell) + across * (cs * 0.5f) + along * shift + new Vector3(0f, h, 0f);
                        Vector3 p1 = g.Local(far) - across * (cs * 0.5f) + along * (shift + d.R(-0.15f, 0.15f) * cs) + new Vector3(0f, h + d.R(-0.35f, 0.35f), 0f);
                        Vector3 mid = (p0 + p1) * 0.5f;
                        mid.y = g.Local(cell).y;
                        if (d.NearRing(mid, 3f) || d.KeptOut(mid)) continue;
                        kit.hang(d, p0, p1);
                        d.props++;
                        made++;
                    }
                }
        }
    }
}
