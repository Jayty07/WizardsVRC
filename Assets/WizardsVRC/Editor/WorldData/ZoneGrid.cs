using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// Reads a zone's text map and height map: cell classes, cell levels and positions, which moves are possible (stairs
    /// join paved cells across level changes) and patrol routes. Shared by the builder, the validator and the inspector.
    /// </summary>
    public class ZoneGrid
    {
        /// <summary>Wizard seats sit 7.27 m from the ring centre; keep this much open space around a ring spot.</summary>
        public const float RingClearance = 8.5f;

        public readonly ZoneDefinition zone;
        public readonly string[] rows;
        public readonly int width;
        public readonly int height;
        public readonly float cell;
        /// <summary>Metres per height-map step.</summary>
        public readonly float step;
        /// <summary>Lowest and highest level on the map (0 when there's no height map).</summary>
        public readonly int minLevel, maxLevel;
        private readonly int[,] _levels;

        /// <summary>Most steps a staircase climbs (more is a wall you can't get up).</summary>
        public const int MaxStairSteps = 2;

        public ZoneGrid(ZoneDefinition zone)
        {
            this.zone = zone;
            rows = zone.Rows();
            height = rows.Length;
            width = height > 0 ? rows[0].Length : 0;
            cell = zone.cellSize;
            step = Mathf.Max(0.1f, zone.heightStep);
            _levels = new int[Mathf.Max(1, width), Mathf.Max(1, height)];
            string[] h = zone.HeightRows();
            for (int y = 0; y < height && y < h.Length; y++)
                for (int x = 0; x < width && x < h[y].Length; x++)
                {
                    int v = ZoneDefinition.LevelOf(h[y][x]);
                    _levels[x, y] = v == int.MinValue ? 0 : v;
                }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    minLevel = Mathf.Min(minLevel, _levels[x, y]);
                    maxLevel = Mathf.Max(maxLevel, _levels[x, y]);
                }
        }

        /// <summary>Height-map level of a cell (0 off the map).</summary>
        public int Level(int x, int y) { return x < 0 || y < 0 || x >= width || y >= height ? 0 : _levels[x, y]; }
        public int Level(Vector2Int c) { return Level(c.x, c.y); }

        /// <summary>Ground height of a cell in metres (zone-local).</summary>
        public float Elevation(int x, int y) { return Level(x, y) * step; }

        public bool HasHeights { get { return minLevel != 0 || maxLevel != 0; } }

        public char At(int x, int y) { return x < 0 || y < 0 || x >= width || y >= height ? '#' : rows[y][x]; }

        public static bool IsSolid(char c) { return c == '#' || c == '=' || c == '^' || c == '~'; }
        public static bool IsWater(char c) { return c == '~'; }
        public static bool IsRingDigit(char c) { return c >= '1' && c <= '9'; }
        public static bool IsPatrolLetter(char c) { return c >= 'a' && c <= 'i'; }
        public static bool IsBuiltIn(char c) { return IsSolid(c) || c == '.' || c == ',' || c == 'S' || IsRingDigit(c) || IsPatrolLetter(c); }

        /// <summary>True for symbols defined by the zone's NPC, point or landmark lists.</summary>
        public bool IsCustom(char c) { return zone.FindNpc(c) != null || zone.FindPoint(c) != null || zone.FindLandmark(c) != null; }

        /// <summary>Walls, trees and solid landmarks (buildings, fountains, stalls).</summary>
        public bool Solid(int x, int y)
        {
            char c = At(x, y);
            if (IsSolid(c)) return true;
            var lm = zone.FindLandmark(c);
            return lm != null && lm.Solid;
        }

        public bool Walkable(int x, int y)
        {
            char c = At(x, y);
            return !Solid(x, y) && (IsBuiltIn(c) || IsCustom(c));
        }

        /// <summary>Walkable and paved (anything but lawn): stairs can join paved cells at different heights.</summary>
        public bool Paved(int x, int y) { return Walkable(x, y) && At(x, y) != ','; }

        /// <summary>Stairs go between these two neighbouring cells (both paved, 1..MaxStairSteps apart).</summary>
        public bool HasStairs(Vector2Int a, Vector2Int b)
        {
            int d = Mathf.Abs(Level(a) - Level(b));
            return d > 0 && d <= MaxStairSteps && Paved(a.x, a.y) && Paved(b.x, b.y);
        }

        /// <summary>A player can walk from a to the neighbouring cell b: same height, or stairs.</summary>
        public bool CanStep(Vector2Int a, Vector2Int b)
        {
            if (!Walkable(b.x, b.y)) return false;
            return Level(a) == Level(b) || HasStairs(a, b);
        }

        /// <summary>
        /// Round castle hubs: cells whose centre lies outside the island's circle (the map's corners). They are treated
        /// as off the map: no ground is built there.
        /// </summary>
        public bool IsVoid(int x, int y)
        {
            if (!zone.roundCastle || !zone.castleStyle) return false;
            float dx = (x + 0.5f - width * 0.5f), dy = (y + 0.5f - height * 0.5f);
            float r = Mathf.Min(width, height) * 0.5f;
            return dx * dx + dy * dy > r * r;
        }

        /// <summary>Round castle hubs: radius (m) of the curtain wall's centre line, a cell and a half inside the map edge.</summary>
        public float RoundWallRadius() { return Mathf.Min(width, height) * cell * 0.5f - cell * 1.5f; }

        /// <summary>The side a building's door is on: its Door Side setting, or the open side facing the map centre.</summary>
        public Vector2Int DoorDirection(ZoneLandmark l)
        {
            switch (l.doorSide)
            {
                case global::WizardsVRC.EditorTools.WorldData.DoorSide.North: return new Vector2Int(0, -1);
                case global::WizardsVRC.EditorTools.WorldData.DoorSide.East: return new Vector2Int(1, 0);
                case global::WizardsVRC.EditorTools.WorldData.DoorSide.South: return new Vector2Int(0, 1);
                case global::WizardsVRC.EditorTools.WorldData.DoorSide.West: return new Vector2Int(-1, 0);
            }
            // The building stands at the height of its own cells (a corner of its block may be something else, e.g. a
            // pond, when it's turned to face the centre).
            var cells = Find(l.Symbol);
            return FrontSide(Footprint(l.Symbol), cells.Count > 0 ? Level(cells[0]) : int.MinValue);
        }

        /// <summary>Bounding block of a landmark's cells (empty when it isn't on the map).</summary>
        public RectInt Footprint(char c)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (rows[y][x] == c) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
            return x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        /// <summary>
        /// The side a building's door opens on: among the sides with open cells next to them, the one facing the map's
        /// centre most. Returns a map step (0,0 when no side is open).
        /// </summary>
        public Vector2Int FrontSide(RectInt r) { return FrontSide(r, int.MinValue); }

        /// <summary>As <see cref="FrontSide(RectInt)"/>, counting open cells at level 'lv' (int.MinValue = the block's corner).</summary>
        public Vector2Int FrontSide(RectInt r, int lv)
        {
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            Vector2 mid = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
            Vector2Int best = Vector2Int.zero;
            float bestScore = float.MinValue;
            // Only open cells at the building's own height count (a door into a retaining wall is no use).
            if (lv == int.MinValue) lv = Level(r.x, r.y);
            foreach (var d in Dirs)
            {
                int open = 0;
                if (d.y != 0) { int yy = d.y < 0 ? r.yMin - 1 : r.yMax; for (int x = r.xMin; x < r.xMax; x++) if (Walkable(x, yy) && Level(x, yy) == lv) open++; }
                else { int xx = d.x < 0 ? r.xMin - 1 : r.xMax; for (int y = r.yMin; y < r.yMax; y++) if (Walkable(xx, y) && Level(xx, y) == lv) open++; }
                if (open == 0) continue;
                Vector2 toCenter = (center - mid);
                float score = toCenter.sqrMagnitude > 0.01f ? Vector2.Dot(toCenter.normalized, new Vector2(d.x, d.y)) : 0f;
                score += open * 0.01f;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        /// <summary>
        /// Cell centre in zone-local space at the cell's ground height: row 0 is the north (+Z) edge, column 0 the west (-X) edge.
        /// </summary>
        public Vector3 Local(int x, int y)
        {
            return new Vector3((x + 0.5f - width * 0.5f) * cell, Elevation(x, y), (height * 0.5f - y - 0.5f) * cell);
        }

        public Vector3 Local(Vector2Int c) { return Local(c.x, c.y); }

        public List<Vector2Int> Find(char c)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (rows[y][x] == c) list.Add(new Vector2Int(x, y));
            return list;
        }

        public static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };

        /// <summary>Walkable cells reachable from the first S.</summary>
        public HashSet<Vector2Int> Reachable()
        {
            var seen = new HashSet<Vector2Int>();
            var starts = Find('S');
            if (starts.Count == 0) return seen;
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(starts[0]);
            seen.Add(starts[0]);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Dirs)
                {
                    var n = p + d;
                    if (!seen.Contains(n) && CanStep(p, n)) { seen.Add(n); queue.Enqueue(n); }
                }
            }
            return seen;
        }

        /// <summary>Direction (in map steps) from a cell toward its most open walkable neighbour (at its own height).</summary>
        public Vector2Int OpenDirection(Vector2Int c)
        {
            Vector2Int best = Dirs[0];
            int bestScore = -1;
            foreach (var d in Dirs)
            {
                int score = 0;
                for (int k = 1; k <= 3; k++)
                    if (Walkable(c.x + d.x * k, c.y + d.y * k) && Level(c.x + d.x * k, c.y + d.y * k) == Level(c)) score++;
                    else break;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        /// <summary>A map step as a zone-local direction (row index grows southward = -Z).</summary>
        public static Vector3 ToLocalDir(Vector2Int d) { return new Vector3(d.x, 0f, -d.y); }

        /// <summary>Cells that block a ring spot (inside the ring clearance but not walkable, or at another height).</summary>
        public List<Vector2Int> RingBlockers(Vector2Int ring)
        {
            var list = new List<Vector2Int>();
            int r = Mathf.CeilToInt(RingClearance / cell) + 1;
            for (int y = ring.y - r; y <= ring.y + r; y++)
                for (int x = ring.x - r; x <= ring.x + r; x++)
                {
                    float d = new Vector2(x - ring.x, y - ring.y).magnitude * cell;
                    if (d <= RingClearance && (!Walkable(x, y) || Level(x, y) != Level(ring))) list.Add(new Vector2Int(x, y));
                }
            return list;
        }

        /// <summary>
        /// Orders a patrol letter's cells into a route. Returns null when there are none. isLoop tells whether every cell
        /// has exactly two route neighbours; ok is false when the cells aren't a single line or loop (they're then
        /// chained nearest-first, which still works but may cut corners).
        /// </summary>
        public List<Vector2Int> PatrolRoute(char letter, out bool isLoop, out bool ok)
        {
            isLoop = false;
            ok = true;
            var cells = new HashSet<Vector2Int>(Find(letter));
            if (cells.Count == 0) return null;
            var deg = new Dictionary<Vector2Int, int>();
            foreach (var c in cells)
            {
                int n = 0;
                foreach (var d in Dirs) if (cells.Contains(c + d)) n++;
                deg[c] = n;
            }
            var ends = new List<Vector2Int>();
            bool bad = false;
            foreach (var kv in deg)
            {
                if (kv.Value == 1) ends.Add(kv.Key);
                else if (kv.Value != 2) bad = true;
            }
            isLoop = !bad && ends.Count == 0 && cells.Count >= 4;
            ok = isLoop || (!bad && ends.Count == 2) || cells.Count == 1;

            var route = new List<Vector2Int>();
            var left = new HashSet<Vector2Int>(cells);
            Vector2Int cur = ends.Count > 0 ? ends[0] : Min(cells);
            route.Add(cur);
            left.Remove(cur);
            while (left.Count > 0)
            {
                Vector2Int next = cur;
                bool found = false;
                foreach (var d in Dirs) if (left.Contains(cur + d)) { next = cur + d; found = true; break; }
                if (!found)
                {
                    float best = float.MaxValue;
                    foreach (var c in left) { float dd = (c - cur).sqrMagnitude; if (dd < best) { best = dd; next = c; } }
                    ok = false;
                }
                route.Add(next);
                left.Remove(next);
                cur = next;
            }
            return route;
        }

        /// <summary>Drops points in the middle of straight runs so only the corners stay.</summary>
        public static List<Vector2Int> Corners(List<Vector2Int> route, bool loop)
        {
            if (route == null || route.Count < 3) return route;
            var o = new List<Vector2Int>();
            int n = route.Count;
            for (int i = 0; i < n; i++)
            {
                bool endpoint = !loop && (i == 0 || i == n - 1);
                Vector2Int prev = route[(i - 1 + n) % n], next = route[(i + 1) % n];
                if (endpoint || (route[i] - prev) != (next - route[i])) o.Add(route[i]);
            }
            return o.Count >= 2 ? o : route;
        }

        private static Vector2Int Min(IEnumerable<Vector2Int> cells)
        {
            Vector2Int m = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (var c in cells) if (c.y < m.y || (c.y == m.y && c.x < m.x)) m = c;
            return m;
        }

        /// <summary>Stable 0..1 pseudo-random value per cell, so rebuilds look the same.</summary>
        public float Hash(int x, int y, int salt)
        {
            unchecked
            {
                // FNV-1a of the zone key (string.GetHashCode isn't guaranteed stable), mixed with the cell.
                int h = unchecked((int)2166136261u);
                foreach (char ch in zone.zoneKey ?? "") { h ^= ch; h *= 16777619; }
                h = h * 31 + x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }
    }
}
