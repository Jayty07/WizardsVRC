using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The world and zone maps in the book (MapPages), drawn at build time from the zones' own grids so they always match
    /// the world data:
    ///   - Each zone: its streets as a little isometric model on old parchment, like a hand-made storybook map: lawns and
    ///     roads, houses with hip roofs and lit windows, hedges and low walls, round trees, water with waterfalls pouring
    ///     off the edge, terraces with earthy sides, the island's soil underneath; the landmarks as their own buildings
    ///     (school halls roofed in their school's colour, towers, the round tavern, the fountain); a badge for every NPC;
    ///     yellow arrows from parchment tags to the exits and landmarks (the tags are buttons in the book, so exits open
    ///     the next zone's map); and marker doodles that suit the zone in the margins (Tools/make_map_art.py).
    ///   - Each world: its zones as little floating islands in the sky round the hub (or town), placed the way their
    ///     tunnels lead, joined by dotted golden paths.
    /// The book shows where you are on both (a red marker turned to your heading).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string MapArtDir = TexturesDir + "/Maps";
        private const string MapOutDir = MapArtDir + "/Generated";
        private const int MapW = 1200, MapH = 800;
        private static readonly Color32 MapInk = new Color32(58, 38, 26, 255);

        /// <summary>The isometric view of one zone: grid (gx east, gy south, h metres) to map pixels (y down).</summary>
        private class MapProj
        {
            public const float A = 1f, B = 0.56f;
            public float ox, oy, sc, cell, step, stepMap, soil;
            public int minL, gw, gh;

            public Vector2 P(float gx, float gy, float h)
            {
                return new Vector2(ox + (gx - gy) * A * sc, oy + ((gx + gy) * B - h / cell * B * 2f) * sc);
            }

            /// <summary>Where a world position (in a zone whose root is at height rootY) lands on the map, as 0..1 (v up).</summary>
            public Vector2 Uv(Vector3 world, float rootY, int mapW, int mapH)
            {
                float gx = world.x / cell + gw * 0.5f;
                float gy = gh * 0.5f - world.z / cell;
                float hm = ((world.y - rootY) / step - minL) * stepMap;
                Vector2 p = P(gx, gy, hm);
                return new Vector2(p.x / mapW, 1f - p.y / mapH);
            }
        }

        private class MapLabelInfo
        {
            public int map;
            public string text;
            public Vector2 uv;      // the tag's middle, 0..1 (v up)
            public int target;      // the map it opens, or -1
        }

        private class MapZoneInfo
        {
            public ZoneDefinition zone;
            public ZoneGrid grid;
            public float rootY;
            public MapProj proj;
            public int worldMap = -1;
            public Vector2 onWorld;
        }

        // ================================================================== colours

        private static Color32 MapTone(Color c, float s, float v, float greyHue)
        {
            float h, cs, cv;
            Color.RGBToHSV(c, out h, out cs, out cv);
            if (cs < 0.08f && greyHue >= 0f) h = greyHue;
            return (Color32)Color.HSVToRGB(h, s, v);
        }

        private static Color32 Shade(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0f, 255f), (byte)Mathf.Clamp(c.g * k, 0f, 255f), (byte)Mathf.Clamp(c.b * k, 0f, 255f), c.a);
        }

        private static Color32 WithAlpha(Color32 c, int a) { return new Color32(c.r, c.g, c.b, (byte)Mathf.Clamp(a, 0, 255)); }

        private static float MapHash(int x, int y, int s)
        {
            unchecked
            {
                int v = x * 73856093 ^ y * 19349663 ^ s * 83492791;
                return (v & 0xffff) / 65535f;
            }
        }

        // ================================================================== the isometric zone

        private enum MapCell { Road, Grass, Water, Void }

        private class MapObject
        {
            public int kind;        // 0 house, 1 low wall, 2 tree, 3 landmark box, 4 round tavern, 5 fountain, 6 duel circle, 7 castle wall
            public RectInt r;
            public bool edge;
            public Color32 roof;
            public float height;    // metres (houses: picked when drawn)
            public bool flatRoof;
        }

        private static bool LandmarkBlock(LandmarkKind k)
        {
            return k == LandmarkKind.SchoolHall || k == LandmarkKind.Keep || k == LandmarkKind.Library || k == LandmarkKind.Shop || k == LandmarkKind.Tower ||
                   k == LandmarkKind.Tavern || k == LandmarkKind.Saloon || k == LandmarkKind.Fountain || k == LandmarkKind.DuelCircle;
        }

        /// <summary>The connected blocks of cells painted with c (a symbol can be painted in several places).</summary>
        private static List<RectInt> MapComponents(ZoneGrid g, char c)
        {
            var list = new List<RectInt>();
            var seen = new bool[g.width, g.height];
            var stack = new Stack<Vector2Int>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    if (seen[x, y] || g.At(x, y) != c) continue;
                    int x0 = x, x1 = x, y0 = y, y1 = y;
                    stack.Push(new Vector2Int(x, y));
                    seen[x, y] = true;
                    while (stack.Count > 0)
                    {
                        var p = stack.Pop();
                        x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
                        foreach (var d in ZoneGrid.Dirs)
                        {
                            int nx = p.x + d.x, ny = p.y + d.y;
                            if (nx < 0 || ny < 0 || nx >= g.width || ny >= g.height || seen[nx, ny] || g.At(nx, ny) != c) continue;
                            seen[nx, ny] = true;
                            stack.Push(new Vector2Int(nx, ny));
                        }
                    }
                    list.Add(new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
                }
            return list;
        }

        /// <summary>
        /// Draws a zone as a little isometric model on a transparent canvas W x H (fitting fillX x fillY of it, its middle at
        /// centreY of the height), supersampled twice. rock: a floating island's jagged rock underneath (world maps).
        /// </summary>
        private static MapCanvas RenderZoneIso(ZoneDefinition z, ZoneGrid g, int W, int H, float fillX, float fillY, float centreY, bool rock, out MapProj proj)
        {
            const int S = 2;
            int gw = g.width, gh = g.height;
            var pj = new MapProj { cell = g.cell, step = g.step, stepMap = g.step * 2f, minL = g.minLevel, gw = gw, gh = gh };
            pj.soil = 1.6f * g.cell;
            // The tallest thing on the map (towers, the tavern's cone, the houses' roofs), so the fit leaves room for it.
            bool tall = false;
            if (z.landmarks != null)
                foreach (var l in z.landmarks)
                    if (l != null && (l.kind == LandmarkKind.Keep || l.kind == LandmarkKind.Tower || l.kind == LandmarkKind.Tavern)) tall = true;
            float hmax = (g.maxLevel - g.minLevel) * pj.stepMap + Mathf.Max(g.cell * 2.6f, z.buildingHeight.y + g.cell * 1.2f, tall ? 34f : 0f);
            float under = rock ? g.cell * Mathf.Min(gw, gh) * 0.9f : 0f;
            // Fit: the four corners at the top and bottom of everything (and the rock's point).
            pj.ox = 0f; pj.oy = 0f; pj.sc = 1f;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var c in new[] { new Vector2(0, 0), new Vector2(gw, 0), new Vector2(0, gh), new Vector2(gw, gh) })
                foreach (float hh in new[] { hmax, -pj.soil })
                {
                    Vector2 p = pj.P(c.x, c.y, hh);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            if (rock)
            {
                Vector2 tipP = pj.P(gw * 0.55f, gh * 0.55f, -pj.soil - under);
                maxY = Mathf.Max(maxY, tipP.y);
            }
            pj.sc = Mathf.Min(W * fillX / Mathf.Max(1f, maxX - minX), H * fillY / Mathf.Max(1f, maxY - minY));
            pj.ox = W * 0.5f - (maxX + minX) * 0.5f * pj.sc;
            pj.oy = H * centreY - (maxY + minY) * 0.5f * pj.sc;
            proj = pj;

            var cv = new MapCanvas(W * S, H * S);
            var falls = new MapCanvas(W * S, H * S);
            System.Func<float, float, float, Vector2> Q = (gx, gy, hh) => pj.P(gx, gy, hh) * S;
            float lw = 1.6f;   // outline width at the supersampled size

            // ---- palette from the zone's colours, at storybook brightness
            Color32 grass = MapTone(z.grassColor, 0.56f, 0.72f, 0.3f);
            Color32 road = MapTone(z.roadColor, 0.15f, 0.8f, -1f);
            Color32 wall = MapTone(z.buildingColor, 0.08f, 0.86f, -1f);
            Color32 roof = MapTone(z.roofColor, 0.7f, 0.78f, 0.02f);
            Color32 low = MapTone(z.lowWallColor, 0.29f, 0.7f, 0.3f);
            Color32 tree = MapTone(z.treeColor, 0.62f, 0.52f, 0.33f);
            Color32 water = new Color32(104, 168, 232, 255);
            Color32 cliff = new Color32(198, 132, 74, 255);
            Color32 glass = new Color32(252, 214, 110, 255);
            bool desert = z.textureStyle == ZoneTextureStyle.Desert || z.textureStyle == ZoneTextureStyle.Boomtown || z.textureStyle == ZoneTextureStyle.Mine;
            if (desert) { grass = MapTone(new Color(0.85f, 0.7f, 0.45f), 0.38f, 0.86f, -1f); tree = new Color32(110, 170, 90, 255); }

            // ---- what each cell is, and the things standing on the map
            var kinds = new MapCell[gw, gh];
            var claimed = new bool[gw, gh];
            var objects = new Dictionary<int, List<MapObject>>();
            System.Action<MapObject> Add = o =>
            {
                int key = o.r.xMax - 1 + o.r.yMax - 1;
                List<MapObject> l;
                if (!objects.TryGetValue(key, out l)) { l = new List<MapObject>(); objects[key] = l; }
                l.Add(o);
            };
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    char ch = g.At(x, y);
                    if (g.IsVoid(x, y)) kinds[x, y] = MapCell.Void;
                    else if (ch == '~') kinds[x, y] = MapCell.Water;
                    else if (ch == ',' || ch == '^' || ch == '#' || ch == '=') kinds[x, y] = MapCell.Grass;
                    else kinds[x, y] = MapCell.Road;
                    var lm = z.FindLandmark(ch);
                    if (lm != null && lm.kind == LandmarkKind.Waterfall) kinds[x, y] = MapCell.Water;
                }
            if (z.landmarks != null)
                foreach (var l in z.landmarks)
                {
                    if (l == null || l.Symbol == '\0' || !LandmarkBlock(l.kind)) continue;
                    foreach (var r in MapComponents(g, l.Symbol))
                    {
                        var o = new MapObject { r = r, kind = 3, height = 10f, flatRoof = false };
                        switch (l.kind)
                        {
                            case LandmarkKind.SchoolHall: o.roof = (Color32)SchoolColors[Mathf.Clamp((int)l.school, 0, SchoolColors.Length - 1)]; o.height = 11f; break;
                            case LandmarkKind.Keep: o.roof = new Color32(80, 110, 210, 255); o.height = 24f; break;
                            case LandmarkKind.Tower: o.roof = new Color32(140, 90, 200, 255); o.height = 20f; break;
                            case LandmarkKind.Library: o.roof = new Color32(70, 120, 200, 255); o.height = 10f; break;
                            case LandmarkKind.Shop: o.roof = Shade(roof, 0.9f + 0.3f * MapHash(r.x, r.y, 9)); o.height = 8f; break;
                            case LandmarkKind.Saloon: o.roof = new Color32(150, 90, 50, 255); o.height = 10f; o.flatRoof = true; break;
                            case LandmarkKind.Tavern: o.kind = 4; o.roof = new Color32(130, 80, 190, 255); o.height = 12.6f; break;
                            case LandmarkKind.Fountain: o.kind = 5; break;
                            case LandmarkKind.DuelCircle: o.kind = 6; break;
                        }
                        Add(o);
                        for (int yy = r.yMin; yy < r.yMax; yy++)
                            for (int xx = r.xMin; xx < r.xMax; xx++)
                                if (xx >= 0 && yy >= 0 && xx < gw && yy < gh) claimed[xx, yy] = true;
                        if (o.kind == 3 || o.kind == 4)
                            for (int yy = r.yMin; yy < r.yMax; yy++)
                                for (int xx = r.xMin; xx < r.xMax; xx++)
                                    if (xx >= 0 && yy >= 0 && xx < gw && yy < gh && kinds[xx, yy] == MapCell.Road) kinds[xx, yy] = MapCell.Grass;
                    }
                }
            // Houses: blocks of '#' up to 3 x 3, each a house of its own.
            var used = new bool[gw, gh];
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    if (used[x, y] || g.At(x, y) != '#' || g.IsVoid(x, y)) continue;
                    int w = 1;
                    while (w < 3 && x + w < gw && g.At(x + w, y) == '#' && !used[x + w, y] && !g.IsVoid(x + w, y)) w++;
                    int hh = 1;
                    bool ok = true;
                    while (ok && hh < 3 && y + hh < gh)
                    {
                        for (int i = 0; i < w; i++) if (g.At(x + i, y + hh) != '#' || used[x + i, y + hh] || g.IsVoid(x + i, y + hh)) { ok = false; break; }
                        if (ok) hh++;
                    }
                    for (int j = 0; j < hh; j++) for (int i = 0; i < w; i++) used[x + i, y + j] = true;
                    bool edge = x == 0 || y == 0 || x + w == gw || y + hh == gh;
                    // A castle hub's '#' is its curtain wall: grey stone, not houses.
                    Add(new MapObject { kind = z.castleStyle ? 7 : 0, r = new RectInt(x, y, w, hh), edge = edge, roof = roof });
                }
            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    if (g.IsVoid(x, y) || claimed[x, y]) continue;
                    char ch = g.At(x, y);
                    if (ch == '=') Add(new MapObject { kind = 1, r = new RectInt(x, y, 1, 1) });
                    else if (ch == '^') Add(new MapObject { kind = 2, r = new RectInt(x, y, 1, 1) });
                }

            System.Func<int, int, float> Hc = (x, y) => (g.Level(x, y) - g.minLevel) * pj.stepMap;
            System.Func<int, int, bool> On = (x, y) => x >= 0 && y >= 0 && x < gw && y < gh && kinds[x, y] != MapCell.Void;

            // ---- the rock under a floating island (world maps): jagged facets from its bottom edges down to a point
            if (rock)
            {
                Vector2 tip = Q(gw * 0.55f, gh * 0.55f, -pj.soil - under);
                var edgePts = new List<Vector2> { Q(0, gh, -pj.soil) };
                for (int i = 1; i <= gw; i++) edgePts.Add(Q(i, gh, -pj.soil));
                for (int j = 1; j <= gh; j++) edgePts.Add(Q(gw, gh - j, -pj.soil));
                var cols = new[] { new Color32(150, 96, 56, 255), new Color32(124, 78, 46, 255), new Color32(170, 112, 66, 255), new Color32(110, 68, 40, 255) };
                for (int i = 0; i + 1 < edgePts.Count; i += 3)
                {
                    Vector2 p0 = edgePts[i], p1 = edgePts[Mathf.Min(edgePts.Count - 1, i + 3)];
                    Vector2 mid = (p0 + p1) * 0.5f;
                    float depth = 0.35f + 0.6f * MapHash(i, gw, 7);
                    Vector2 spike = new Vector2(mid.x + (tip.x - mid.x) * depth * 0.6f, mid.y + (tip.y - mid.y) * depth);
                    cv.FillPoly(new[] { p0, p1, tip }, cols[(i / 3) % 4]);
                    cv.Poly(new[] { p0, p1, spike }, cols[(i / 3 + 1) % 4], MapInk, lw * 0.6f);
                }
                cv.Polyline(edgePts.ToArray(), lw, MapInk, false);
            }

            // ---- ground and things, back to front by diagonal
            for (int s = 0; s <= gw + gh - 2; s++)
            {
                for (int y = 0; y < gh; y++)
                {
                    int x = s - y;
                    if (x < 0 || x >= gw || kinds[x, y] == MapCell.Void) continue;
                    MapCell k = kinds[x, y];
                    float h = Hc(x, y);
                    Color32 top = k == MapCell.Water ? water : k == MapCell.Grass ? grass : road;
                    // The visible sides: east (+x, right) and south (+y, left), down to the neighbour or the island's soil.
                    for (int f = 0; f < 2; f++)
                    {
                        int nx = f == 0 ? x + 1 : x, ny = f == 0 ? y : y + 1;
                        bool edge = !On(nx, ny);
                        float nh = edge ? -pj.soil : Hc(nx, ny);
                        if (nh >= h - 0.01f) continue;
                        Vector2[] face = f == 0
                            ? new[] { Q(x + 1, y, h), Q(x + 1, y + 1, h), Q(x + 1, y + 1, nh), Q(x + 1, y, nh) }
                            : new[] { Q(x, y + 1, h), Q(x + 1, y + 1, h), Q(x + 1, y + 1, nh), Q(x, y + 1, nh) };
                        cv.FillPoly(face, f == 0 ? Shade(cliff, 0.8f) : cliff);
                        if (edge)
                        {
                            foreach (float fr in new[] { 0.3f, 0.62f })
                            {
                                float hh = h - (h - nh) * fr;
                                if (f == 0) cv.Line(Q(x + 1, y, hh), Q(x + 1, y + 1, hh), S, Shade(cliff, 0.62f));
                                else cv.Line(Q(x, y + 1, hh), Q(x + 1, y + 1, hh), S, Shade(cliff, 0.62f));
                            }
                            if (k == MapCell.Water)
                            {
                                // A waterfall pouring off the edge, fading as it falls.
                                float far = -pj.soil - g.cell * 3.5f;
                                for (int kf = 0; kf < 6; kf++)
                                {
                                    float h0 = kf == 0 ? h : -pj.soil + (far + pj.soil) * kf / 6f, h1 = -pj.soil + (far + pj.soil) * (kf + 1) / 6f;
                                    int al = (int)(235 * (1f - kf / 6f));
                                    Vector2[] fp = f == 0
                                        ? new[] { Q(x + 1, y + 0.15f, h0), Q(x + 1, y + 0.85f, h0), Q(x + 1, y + 0.85f, h1), Q(x + 1, y + 0.15f, h1) }
                                        : new[] { Q(x + 0.15f, y + 1, h0), Q(x + 0.85f, y + 1, h0), Q(x + 0.85f, y + 1, h1), Q(x + 0.15f, y + 1, h1) };
                                    falls.FillPoly(fp, new Color32(120, 180, 240, (byte)al));
                                    falls.Line(fp[0], fp[3], S, new Color32(230, 245, 255, (byte)al));
                                    falls.Line(fp[1], fp[2], S, new Color32(230, 245, 255, (byte)al));
                                }
                            }
                        }
                        else
                        {
                            if (f == 0) cv.Line(Q(x + 1, y, h), Q(x + 1, y + 1, h), S, Shade(cliff, 0.5f));
                            else cv.Line(Q(x, y + 1, h), Q(x + 1, y + 1, h), S, Shade(cliff, 0.5f));
                        }
                    }
                    float th = k == MapCell.Water ? h - g.cell * 0.08f : h;
                    cv.FillPoly(new[] { Q(x, y, th), Q(x + 1, y, th), Q(x + 1, y + 1, th), Q(x, y + 1, th) }, top);
                    if (k == MapCell.Road)
                    {
                        // A darker edge where a road meets lawn or water.
                        if (On(x + 1, y) && kinds[x + 1, y] != MapCell.Road) cv.Line(Q(x + 1, y, th), Q(x + 1, y + 1, th), S, Shade(road, 0.72f));
                        if (On(x, y + 1) && kinds[x, y + 1] != MapCell.Road) cv.Line(Q(x, y + 1, th), Q(x + 1, y + 1, th), S, Shade(road, 0.72f));
                        if (On(x - 1, y) && kinds[x - 1, y] != MapCell.Road) cv.Line(Q(x, y, th), Q(x, y + 1, th), S, Shade(road, 0.72f));
                        if (On(x, y - 1) && kinds[x, y - 1] != MapCell.Road) cv.Line(Q(x, y, th), Q(x + 1, y, th), S, Shade(road, 0.72f));
                    }
                    if (k == MapCell.Water && MapHash(x, y, 1) < 0.5f)
                    {
                        Vector2 c = Q(x + 0.5f, y + 0.5f, th);
                        float u = pj.sc * S * 0.25f;
                        cv.Polyline(new[] { c + new Vector2(-u, 0), c + new Vector2(-u * 0.4f, -u * 0.35f), c + new Vector2(u * 0.2f, 0), c + new Vector2(u * 0.8f, -u * 0.35f) },
                            S, new Color32(210, 235, 255, 255), false);
                    }
                }
                List<MapObject> here;
                if (objects.TryGetValue(s, out here))
                    foreach (var o in here) DrawMapObject(cv, z, g, pj, S, Q, Hc, o, wall, low, tree, glass, water, lw, desert);
            }
            falls.Blit(cv, 0, 0);
            return falls.Downsample(S);
        }

        private static void DrawMapObject(MapCanvas cv, ZoneDefinition z, ZoneGrid g, MapProj pj, int S, System.Func<float, float, float, Vector2> Q, System.Func<int, int, float> Hc,
            MapObject o, Color32 wall, Color32 low, Color32 tree, Color32 glass, Color32 water, float lw, bool desert)
        {
            RectInt r = o.r;
            float ground = float.MinValue;
            for (int j = r.yMin; j < r.yMax; j++)
                for (int i = r.xMin; i < r.xMax; i++)
                    ground = Mathf.Max(ground, Hc(i, j));
            float cell = g.cell;
            if (o.kind == 7)
            {
                // Castle wall: grey stone blocks with a paler walk on top.
                float hb = 8f;
                float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;
                Color32 stone = Shade(wall, 0.95f);
                cv.Poly(new[] { Q(x1, y0, ground), Q(x1, y1, ground), Q(x1, y1, ground + hb), Q(x1, y0, ground + hb) }, Shade(stone, 0.78f), MapInk, lw * 0.5f);
                cv.Poly(new[] { Q(x0, y1, ground), Q(x1, y1, ground), Q(x1, y1, ground + hb), Q(x0, y1, ground + hb) }, stone, MapInk, lw * 0.5f);
                cv.Poly(new[] { Q(x0, y0, ground + hb), Q(x1, y0, ground + hb), Q(x1, y1, ground + hb), Q(x0, y1, ground + hb) }, Shade(stone, 1.12f), MapInk, lw * 0.5f);
                return;
            }
            if (o.kind == 0 || o.kind == 3)
            {
                // Houses: the zone's building heights (lower along the map's edge, taller when bigger); landmarks their own.
                Vector2 bh = z.buildingHeight;
                float hb = o.kind == 0
                    ? Mathf.Clamp(Mathf.Lerp(bh.x, bh.y, 0.8f * MapHash(r.x, r.y, 2)), 5f, 16f) * (o.edge ? 0.8f : 1f) * (1f + 0.12f * (Mathf.Min(r.width, r.height) - 1))
                    : o.height;
                float inset = o.kind == 3 ? 0.12f : 0.07f;
                float x0 = r.xMin + inset, x1 = r.xMax - inset, y0 = r.yMin + inset, y1 = r.yMax - inset;
                Color32 wallCol = o.kind == 3 ? Shade(wall, 1.02f) : wall;
                cv.Poly(new[] { Q(x1, y0, ground), Q(x1, y1, ground), Q(x1, y1, ground + hb), Q(x1, y0, ground + hb) }, Shade(wallCol, 0.78f), MapInk, lw * 0.5f);
                cv.Poly(new[] { Q(x0, y1, ground), Q(x1, y1, ground), Q(x1, y1, ground + hb), Q(x0, y1, ground + hb) }, wallCol, MapInk, lw * 0.5f);
                // Lit windows along the two front walls (rows of them on tall buildings).
                float u = pj.sc * S * 0.09f;
                int rows = Mathf.Clamp(Mathf.RoundToInt(hb / 8f), 1, 3);
                for (int row = 0; row < rows; row++)
                {
                    float wy = ground + hb * (rows == 1 ? 0.58f : (row + 0.55f) / rows);
                    for (int k = 0; k < r.width; k++)
                    {
                        Vector2 c = Q(x0 + k + 0.5f, y1, wy);
                        cv.Poly(new[] { c + new Vector2(-u, -u * 1.6f), c + new Vector2(u, -u * 1.1f), c + new Vector2(u, u), c + new Vector2(-u, u * 0.5f) }, glass, MapInk, 1f);
                    }
                    for (int k = 0; k < r.height; k++)
                    {
                        Vector2 c = Q(x1, y0 + k + 0.5f, wy);
                        cv.Poly(new[] { c + new Vector2(-u, -u * 1.1f), c + new Vector2(u, -u * 1.6f), c + new Vector2(u, u * 0.5f), c + new Vector2(-u, u) }, Shade(glass, 0.9f), MapInk, 1f);
                    }
                }
                float top = ground + hb, o_ = 0.06f;
                Vector2 c00 = Q(x0 - o_, y0 - o_, top), c10 = Q(x1 + o_, y0 - o_, top), c11 = Q(x1 + o_, y1 + o_, top), c01 = Q(x0 - o_, y1 + o_, top);
                Color32 rf = o.roof;
                if (o.flatRoof || desert && o.kind == 0)
                {
                    cv.Poly(new[] { c00, c10, c11, c01 }, Shade(rf, 1.05f), MapInk, lw * 0.5f);
                    return;
                }
                float wR = x1 - x0, hR = y1 - y0;
                float rise = cell * 0.45f * Mathf.Sqrt(Mathf.Min(wR, hR)) * (o.kind == 3 && o.height > 16f ? 1.8f : 1f);
                Vector2 r0, r1;
                if (wR > hR + 0.01f) { r0 = Q(x0 + hR / 2f, y0 + hR / 2f, top + rise); r1 = Q(x1 - hR / 2f, y0 + hR / 2f, top + rise); }
                else if (hR > wR + 0.01f) { r0 = Q(x0 + wR / 2f, y0 + wR / 2f, top + rise); r1 = Q(x0 + wR / 2f, y1 - wR / 2f, top + rise); }
                else { r0 = r1 = Q(x0 + wR / 2f, y0 + hR / 2f, top + rise); }
                if (hR > wR + 0.01f)
                {
                    cv.Poly(new[] { c00, c10, r0 }, Shade(rf, 1.12f), MapInk, lw * 0.5f);
                    cv.Poly(new[] { c00, c01, r1, r0 }, Shade(rf, 0.92f), MapInk, lw * 0.5f);
                    cv.Poly(new[] { c10, c11, r1, r0 }, Shade(rf, 0.74f), MapInk, lw * 0.5f);
                    cv.Poly(new[] { c01, c11, r1 }, rf, MapInk, lw * 0.5f);
                }
                else
                {
                    cv.Poly(new[] { c00, c10, r1, r0 }, Shade(rf, 1.12f), MapInk, lw * 0.5f);
                    cv.Poly(new[] { c00, c01, r0 }, Shade(rf, 0.92f), MapInk, lw * 0.5f);
                    cv.Poly(new[] { c10, c11, r1 }, Shade(rf, 0.74f), MapInk, lw * 0.5f);
                    cv.Poly(new[] { c01, c11, r1, r0 }, rf, MapInk, lw * 0.5f);
                }
                // A gold finial on the landmarks' ridges.
                if (o.kind == 3)
                {
                    Vector2 fin = (r0 + r1) * 0.5f;
                    cv.FillEllipse(fin + new Vector2(0, -u * 1.2f), u * 1.1f, u * 1.1f, new Color32(255, 214, 80, 255));
                    cv.StrokeEllipse(fin + new Vector2(0, -u * 1.2f), u * 1.1f, u * 1.1f, 1f, MapInk);
                }
            }
            else if (o.kind == 1)
            {
                // A low wall or hedge, joined to its neighbours.
                int x = r.x, y = r.y;
                float hb = cell * 0.2f;
                float x0 = x + 0.2f, x1 = x + 0.8f, y0 = y + 0.2f, y1 = y + 0.8f;
                if (g.At(x + 1, y) == '=') x1 = x + 1;
                if (g.At(x - 1, y) == '=') x0 = x;
                if (g.At(x, y + 1) == '=') y1 = y + 1;
                if (g.At(x, y - 1) == '=') y0 = y;
                cv.FillPoly(new[] { Q(x1, y0, ground), Q(x1, y1, ground), Q(x1, y1, ground + hb), Q(x1, y0, ground + hb) }, Shade(low, 0.78f));
                cv.FillPoly(new[] { Q(x0, y1, ground), Q(x1, y1, ground), Q(x1, y1, ground + hb), Q(x0, y1, ground + hb) }, low);
                cv.FillPoly(new[] { Q(x0, y0, ground + hb), Q(x1, y0, ground + hb), Q(x1, y1, ground + hb), Q(x0, y1, ground + hb) }, Shade(low, 1.18f));
            }
            else if (o.kind == 2)
            {
                Vector2 c = Q(r.x + 0.5f, r.y + 0.5f, ground);
                float rad = pj.sc * S * (0.62f + 0.18f * MapHash(r.x, r.y, 3));
                cv.FillEllipse(c, rad * 0.9f, rad * 0.32f, new Color32(40, 60, 30, 90));
                if (desert)
                {
                    // A cactus.
                    cv.Line(c, c + new Vector2(0, -rad * 1.9f), rad * 0.42f, tree);
                    cv.Line(c + new Vector2(0, -rad * 1.0f), c + new Vector2(-rad * 0.55f, -rad * 1.1f), rad * 0.26f, tree);
                    cv.Line(c + new Vector2(-rad * 0.55f, -rad * 1.1f), c + new Vector2(-rad * 0.55f, -rad * 1.55f), rad * 0.26f, tree);
                    cv.Line(c + new Vector2(0, -rad * 1.3f), c + new Vector2(rad * 0.5f, -rad * 1.35f), rad * 0.24f, tree);
                    cv.Line(c + new Vector2(rad * 0.5f, -rad * 1.35f), c + new Vector2(rad * 0.5f, -rad * 1.75f), rad * 0.24f, tree);
                    return;
                }
                cv.Line(c, c + new Vector2(0, -rad), rad * 0.28f, new Color32(112, 72, 40, 255));
                Vector2 crown = c + new Vector2(0, -rad * 1.375f);
                cv.FillEllipse(crown, rad, rad * 0.825f, tree);
                cv.StrokeEllipse(crown, rad, rad * 0.825f, S, MapInk);
                cv.FillEllipse(c + new Vector2(-rad * 0.22f, -rad * 1.72f), rad * 0.3f, rad * 0.25f, Shade(tree, 1.35f));
            }
            else if (o.kind == 4)
            {
                // The round tavern: a drum with lit windows and a cone roof.
                float rad = Mathf.Min(r.width, r.height) * 0.46f;
                Vector2 mid = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
                float hb = o.height;
                float rx = rad * 1.41421f * MapProj.A * pj.sc * S, ry = rad * 1.41421f * MapProj.B * pj.sc * S;
                Vector2 b = Q(mid.x, mid.y, ground), t = Q(mid.x, mid.y, ground + hb);
                var side = new List<Vector2> { new Vector2(t.x - rx, t.y), new Vector2(t.x + rx, t.y) };
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.PI * i / 16f;
                    side.Add(new Vector2(b.x + Mathf.Cos(a) * rx, b.y + Mathf.Sin(a) * ry));
                }
                cv.Poly(side.ToArray(), wall, MapInk, lw * 0.6f);
                for (int i = 1; i < 6; i++)
                {
                    float a = Mathf.PI * i / 6f;
                    Vector2 wpos = new Vector2(b.x + Mathf.Cos(a) * rx * 0.92f, (b.y + t.y) * 0.5f + Mathf.Sin(a) * ry * 0.9f);
                    float u = pj.sc * S * 0.1f;
                    cv.Poly(new[] { wpos + new Vector2(-u, -u * 1.4f), wpos + new Vector2(u, -u * 1.4f), wpos + new Vector2(u, u), wpos + new Vector2(-u, u) }, glass, MapInk, 1f);
                }
                Vector2 apex = Q(mid.x, mid.y, ground + hb + 15f);
                var cone = new List<Vector2> { new Vector2(t.x - rx * 1.08f, t.y), apex, new Vector2(t.x + rx * 1.08f, t.y) };
                for (int i = 1; i < 16; i++)
                {
                    float a = Mathf.PI * i / 16f;
                    cone.Add(new Vector2(t.x + Mathf.Cos(a) * rx * 1.08f, t.y + Mathf.Sin(a) * ry * 1.08f));
                }
                cv.Poly(cone.ToArray(), o.roof, MapInk, lw * 0.6f);
                cv.Line(apex, new Vector2(t.x - rx * 0.35f, t.y + ry * 0.9f), 1.5f, Shade(o.roof, 0.75f));
                cv.FillEllipse(apex, pj.sc * S * 0.12f, pj.sc * S * 0.12f, new Color32(255, 214, 80, 255));
            }
            else if (o.kind == 5)
            {
                // The fountain: a round stone basin, its water, and the crystal floating over it.
                float rad = Mathf.Min(r.width, r.height) * 0.45f;
                Vector2 mid = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
                float rx = rad * 1.41421f * MapProj.A * pj.sc * S, ry = rad * 1.41421f * MapProj.B * pj.sc * S;
                Vector2 c = Q(mid.x, mid.y, ground + cell * 0.12f);
                cv.FillEllipse(c + new Vector2(0, ry * 0.12f), rx, ry, new Color32(150, 140, 150, 255));
                cv.StrokeEllipse(c + new Vector2(0, ry * 0.12f), rx, ry, lw * 0.6f, MapInk);
                cv.FillEllipse(c, rx * 0.84f, ry * 0.84f, water);
                Vector2 cr = c + new Vector2(0, -ry * 2.2f);
                float u = rx * 0.22f;
                cv.Poly(new[] { cr + new Vector2(0, -u * 2.2f), cr + new Vector2(u, 0), cr + new Vector2(0, u * 1.6f), cr + new Vector2(-u, 0) },
                    new Color32(120, 180, 255, 255), MapInk, 1.2f);
                cv.Line(cr + new Vector2(0, -u * 2.2f), cr + new Vector2(0, u * 1.6f), 1f, new Color32(210, 235, 255, 255));
            }
            else if (o.kind == 6)
            {
                // A duelling circle: a stone disc with a rune ring.
                float rad = Mathf.Min(r.width, r.height) * 0.48f;
                Vector2 mid = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
                float rx = rad * 1.41421f * MapProj.A * pj.sc * S, ry = rad * 1.41421f * MapProj.B * pj.sc * S;
                Vector2 c = Q(mid.x, mid.y, ground);
                cv.FillEllipse(c, rx, ry, new Color32(196, 186, 200, 255));
                cv.StrokeEllipse(c, rx, ry, lw * 0.6f, MapInk);
                cv.StrokeEllipse(c, rx * 0.72f, ry * 0.72f, 2f * S, new Color32(150, 90, 220, 255));
                cv.Line(c + new Vector2(-rx * 0.7f, 0), c + new Vector2(rx * 0.7f, 0), 1.5f, new Color32(230, 190, 90, 255));
            }
        }
    }
}
