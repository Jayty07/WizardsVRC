using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>Reads a zone's text map: cell classes, cell positions and patrol routes. Shared by the builder and the inspector.</summary>
    public class ZoneGrid
    {
        /// <summary>Wizard seats sit 7.27 m from the ring centre; keep this much open space around a ring spot.</summary>
        public const float RingClearance = 8.5f;

        public readonly ZoneDefinition zone;
        public readonly string[] rows;
        public readonly int width;
        public readonly int height;
        public readonly float cell;

        public ZoneGrid(ZoneDefinition zone)
        {
            this.zone = zone;
            rows = zone.Rows();
            height = rows.Length;
            width = height > 0 ? rows[0].Length : 0;
            cell = zone.cellSize;
        }

        public char At(int x, int y) { return x < 0 || y < 0 || x >= width || y >= height ? '#' : rows[y][x]; }

        public static bool IsSolid(char c) { return c == '#' || c == '=' || c == '^'; }
        public static bool IsRingDigit(char c) { return c >= '1' && c <= '9'; }
        public static bool IsPatrolLetter(char c) { return c >= 'a' && c <= 'i'; }
        public static bool IsBuiltIn(char c) { return IsSolid(c) || c == '.' || c == ',' || c == 'S' || IsRingDigit(c) || IsPatrolLetter(c); }

        /// <summary>True for symbols defined by the zone's NPC or point lists.</summary>
        public bool IsCustom(char c) { return zone.FindNpc(c) != null || zone.FindPoint(c) != null; }

        public bool Walkable(int x, int y)
        {
            char c = At(x, y);
            return !IsSolid(c) && (IsBuiltIn(c) || IsCustom(c));
        }

        /// <summary>Cell centre in zone-local space: row 0 is the north (+Z) edge, column 0 the west (-X) edge.</summary>
        public Vector3 Local(int x, int y)
        {
            return new Vector3((x + 0.5f - width * 0.5f) * cell, 0f, (height * 0.5f - y - 0.5f) * cell);
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
                    if (!seen.Contains(n) && Walkable(n.x, n.y)) { seen.Add(n); queue.Enqueue(n); }
                }
            }
            return seen;
        }

        /// <summary>Direction (in map steps) from a cell toward its most open walkable neighbour.</summary>
        public Vector2Int OpenDirection(Vector2Int c)
        {
            Vector2Int best = Dirs[0];
            int bestScore = -1;
            foreach (var d in Dirs)
            {
                int score = 0;
                for (int k = 1; k <= 3; k++) if (Walkable(c.x + d.x * k, c.y + d.y * k)) score++; else break;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        /// <summary>A map step as a zone-local direction (row index grows southward = -Z).</summary>
        public static Vector3 ToLocalDir(Vector2Int d) { return new Vector3(d.x, 0f, -d.y); }

        /// <summary>Cells that block a ring spot (inside the ring clearance but not walkable).</summary>
        public List<Vector2Int> RingBlockers(Vector2Int ring)
        {
            var list = new List<Vector2Int>();
            int r = Mathf.CeilToInt(RingClearance / cell) + 1;
            for (int y = ring.y - r; y <= ring.y + r; y++)
                for (int x = ring.x - r; x <= ring.x + r; x++)
                {
                    float d = new Vector2(x - ring.x, y - ring.y).magnitude * cell;
                    if (d <= RingClearance && !Walkable(x, y)) list.Add(new Vector2Int(x, y));
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
