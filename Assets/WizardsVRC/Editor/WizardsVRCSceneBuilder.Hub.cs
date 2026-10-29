using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The castle hub built from the hub zone's text map (a Zone with Is Hub ticked): castle walls round the island, a
    /// quarter for every school with its professor at the hall door, the Headmaster's tower (door to the office), the
    /// fountain, library, bazaar, training grounds and quest-locked gates to the streets. Without a hub zone the old
    /// hand-placed castle is built instead.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>A gate in the hub that leads to a street zone; the street pass fills in its destination.</summary>
        private class HubGate
        {
            public ZoneLandmark landmark;
            public GameObject clickable;
            public QuestGate gate;
            public TeleportHelper teleport;
            public Transform front;
        }

        /// <summary>Where the hub's fixed pieces go. The defaults are the old hand-placed castle's spots.</summary>
        private class HubLayout
        {
            public ZoneDefinition zone;
            public ZoneGrid grid;
            public Transform root;
            public Transform spawn;
            /// <summary>Every school has a hall in the hub, so professors stand outdoors and the keep is just the office.</summary>
            public bool outdoorSchools;
            public Vector3 spawnPos = new Vector3(0, 0.15f, 0);
            public float spawnYaw;
            public Vector3 canvasPos = new Vector3(-4f, 1.6f, 4f);
            public Quaternion canvasRot = Quaternion.Euler(0, 180f, 0);
            public Vector3 chestPos = new Vector3(-5f, 0f, 10f);
            public Vector3 pedestalPos = new Vector3(2.5f, 0f, 3f);
            public Vector3 padPos = new Vector3(30f, 0.06f, 4f);
            public int padRequiredQuest = -1;
            public string padLockedHint = "";
            public Vector3 keepDoorPos = new Vector3(0, 8.5f, 45.85f);
            /// <summary>Direction the keep door faces (out of the keep).</summary>
            public Vector3 keepOut = Vector3.back;
            public Vector3 keepFrontPos = new Vector3(0, 6.1f, 42.5f);
            public string keepLabel = "Hall of Schools (click door)";
            public readonly Dictionary<string, HubGate> gates = new Dictionary<string, HubGate>();
            /// <summary>World bounds of the hub for the AreaCuller (the classic castle's by default).</summary>
            public Bounds cullBounds = new Bounds(new Vector3(0, 45f, -5f), new Vector3(240f, 210f, 260f));
            public readonly List<UdonSharpBehaviour> behaviours = new List<UdonSharpBehaviour>();
        }

        /// <summary>Finds the hub zone (if any) and decides whether the schools move outdoors.</summary>
        private static HubLayout PlanHub(WorldDataStore.Data data)
        {
            var plan = new HubLayout();
            foreach (var z in data.zones)
            {
                if (z == null || !z.includeInBuild || !z.isHub) continue;
                var g = new ZoneGrid(z);
                if (g.height == 0 || g.Find('S').Count == 0)
                {
                    Debug.LogError("[WizardsVRC World] Hub zone '" + z.zoneKey + "' has no map or no S; the classic castle is built instead.");
                    continue;
                }
                plan.zone = z;
                plan.grid = g;
                break;
            }
            if (plan.zone == null) return plan;
            var halls = new HashSet<School>();
            if (plan.zone.landmarks != null)
                foreach (var l in plan.zone.landmarks)
                    if (l != null && l.kind == LandmarkKind.SchoolHall && plan.grid.Find(l.Symbol).Count > 0 &&
                        plan.grid.FrontSide(plan.grid.Footprint(l.Symbol)) != Vector2Int.zero)
                        halls.Add(l.school);
            plan.outdoorSchools = halls.Count == 7;
            if (!plan.outdoorSchools && halls.Count > 0)
                Debug.LogWarning("[WizardsVRC World] The hub has School Halls for " + halls.Count + " of the 7 schools, so the professors stay in the " +
                                 "keep's Hall of Schools. Add a hall for every school to move them outdoors.");
            return plan;
        }

        // ================================================================== geometry

        private static void BuildHubZone(Transform hubRoot, HubLayout hub, Interior interior, WorldDataStore.Data data)
        {
            _rng = new System.Random(11);
            var z = hub.zone;
            var g = hub.grid;
            string k = z.zoneKey;
            Transform root = Child(hubRoot, "Zone_" + k, Vector3.zero);
            hub.root = root;
            float cs = g.cell, W = g.width * cs, H = g.height * cs;

            Material ground = PMat(k + "_Ground", z.groundColor, 24f);
            Material road = PMat(k + "_Road", z.roadColor, 6f);
            Material grass = PMat(k + "_Grass", z.grassColor, 6f);
            Material stone = PMat(k + "_Building", z.buildingColor, 3f);
            Material roof = PMat(k + "_Roof", z.roofColor, 2f);
            Material low = PMat(k + "_LowWall", z.lowWallColor, 2f);
            Material leaves = PMat(k + "_Tree", z.treeColor, 1f);
            Material trunk = PMat(k + "_Trunk", new Color(0.3f, 0.2f, 0.13f), 1f);
            Material glow = PMat(k + "_Glow", z.accentColor, 1f, z.accentColor);
            Material window = PMat("Window", new Color(1f, 0.85f, 0.4f), 1f, new Color(0.9f, 0.7f, 0.25f));
            Material gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            Material banner = PMat("BannerBlue", new Color(0.15f, 0.25f, 0.75f), 1f);
            Material door = PMat("Door", new Color(0.35f, 0.2f, 0.12f), 2f);
            Material wood = PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f);
            Material dirt = PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f);
            Material cliff = PMat("Cliff", new Color(0.55f, 0.42f, 0.36f), 6f);

            // ---- ground: an island in the sea (castle style) or a plain slab
            if (z.castleStyle)
            {
                float m = cs * 2f, hw = W / 2f + m, hh = H / 2f + m, c = m * 1.5f;
                var poly = new[]
                {
                    new Vector2(-hw + c, -hh), new Vector2(hw - c, -hh), new Vector2(hw, -hh + c), new Vector2(hw, hh - c),
                    new Vector2(hw - c, hh), new Vector2(-hw + c, hh), new Vector2(-hw, hh - c), new Vector2(-hw, -hh + c),
                };
                string dims = Mathf.RoundToInt(W) + "x" + Mathf.RoundToInt(H);
                Shape(root, "IslandCliff", PrismMesh("PSX_HubIsland_" + dims, poly, -7f, -0.2f), Vector3.zero, Vector3.one, cliff, true, Quaternion.identity);
                Shape(root, "IslandTop", PrismMesh("PSX_HubIslandTop_" + dims, poly, -0.2f, 0f), Vector3.zero, Vector3.one, ground, true, Quaternion.identity);
                Block(root, "Sea", new Vector3(0, -6.5f, 0), new Vector3(W + 420f, 1f, H + 420f), PMat("Water", new Color(0.15f, 0.5f, 0.95f), 30f, new Color(0.05f, 0.18f, 0.35f)));
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                    for (float t = 0.1f; t < 1f; t += 0.2f)
                    {
                        Vector2 p = Vector2.Lerp(a, b, t) * 1.03f;
                        Shape(root, "ShoreRock", BlobMesh(), new Vector3(p.x, -6f, p.y), new Vector3(R(5, 9), R(3, 6), R(5, 9)), cliff, false, Quaternion.Euler(0, R(0, 360), 0));
                    }
                }
            }
            else Block(root, "Ground", new Vector3(0, -0.25f, 0), new Vector3(W, 0.5f, H), ground);

            Transform bounds = Child(root, "Bounds", Vector3.zero);
            foreach (var side in new[] { new Vector4(0, H / 2f, W, 1f), new Vector4(0, -H / 2f, W, 1f), new Vector4(W / 2f, 0, 1f, H), new Vector4(-W / 2f, 0, 1f, H) })
            {
                var wall = Child(bounds, "Edge", new Vector3(side.x, 15f, side.y)).gameObject.AddComponent<BoxCollider>();
                wall.size = new Vector3(side.z, 30f, side.w);
            }

            // ---- paving, lawns, walls, hedges, trees
            Transform layout = Child(root, "Layout", Vector3.zero);
            foreach (var r in Rects(g, ch => ch != ',' && !ZoneGrid.IsSolid(ch) && !SolidLandmark(z, ch), 6, 6)) Slab(layout, "Paving", g, r, 0.03f, road);
            foreach (var r in Rects(g, ch => ch == ',', 6, 6)) Slab(layout, "Lawn", g, r, 0.02f, grass);
            int houseIndex = 0;
            foreach (var r in Rects(g, ch => ch == '#', 3, 3))
            {
                bool edge = r.xMin == 0 || r.yMin == 0 || r.xMax == g.width || r.yMax == g.height;
                if (z.castleStyle && edge) Rampart(layout, g, r, stone, roof);
                else House(layout, g, z, r, houseIndex++, stone, roof, window);
            }
            if (z.castleStyle) WallTowers(layout, g, stone, roof, gold, banner);
            foreach (var r in Rects(g, ch => ch == '=', 6, 6))
            {
                Vector3 c = RectCenter(g, r);
                Block(layout, "Hedge", c + new Vector3(0, 0.8f, 0), new Vector3(r.width * cs, 1.6f, r.height * cs), low);
            }
            foreach (var t in g.Find('^'))
            {
                if (z.castleStyle)
                {
                    float roll = g.Hash(t.x, t.y, 5);
                    if (roll < 0.3f) Conifer(layout, g.Local(t), 0.9f + roll);
                    else Tree(layout, g.Local(t), 0.8f + roll * 0.5f);
                }
                else Tree(layout, g, t, trunk, leaves);
            }
            HubLamps(layout, g, z, wood, window);

            // ---- landmarks
            BuildLandmarks(root, g, z, hub, interior, data, stone, roof, gold, banner, door, window, wood);

            // ---- arrival point
            Vector2Int start = g.Find('S')[0];
            Vector3 sp = g.Local(start);
            Vector3 look = -sp;
            look.y = 0f;
            if (look.magnitude < cs * 2f) look = ZoneGrid.ToLocalDir(g.OpenDirection(start));
            hub.spawnPos = sp + new Vector3(0, 0.15f, 0);
            hub.spawnYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            Quaternion face = Quaternion.Euler(0, hub.spawnYaw, 0);
            bool hasBoard = false, hasChest = false, hasPedestal = false, hasPad = false;
            if (z.landmarks != null)
                foreach (var l in z.landmarks)
                {
                    if (l == null || g.Find(l.Symbol).Count == 0) continue;
                    hasBoard |= l.kind == LandmarkKind.NoticeBoard;
                    hasChest |= l.kind == LandmarkKind.BankChest;
                    hasPedestal |= l.kind == LandmarkKind.BackpackPedestal;
                    hasPad |= l.kind == LandmarkKind.ArenaPad;
                }
            // Anything the map doesn't place goes next to the arrival point.
            if (!hasBoard) { hub.canvasPos = sp + face * new Vector3(-4f, 1.6f, 3f); hub.canvasRot = face; }
            if (!hasChest) hub.chestPos = sp + face * new Vector3(-3f, 0, 1.5f);
            if (!hasPedestal) hub.pedestalPos = sp + face * new Vector3(3f, 0, 1.5f);
            if (!hasPad) hub.padPos = sp + face * new Vector3(8f, 0.06f, 0);
            bool hasKeep = false;
            if (z.landmarks != null)
                foreach (var l in z.landmarks) if (l != null && l.kind == LandmarkKind.Keep && g.Find(l.Symbol).Count > 0) hasKeep = true;
            if (!hasKeep)
            {
                // No keep on the map: a free-standing door beside the arrival point leads to the Headmaster's office.
                Vector3 back = -(face * Vector3.forward);
                Vector3 dp = sp + back * 5f;
                Block(root, "OfficeDoorFrame", dp + new Vector3(0, 3f, 0) - back * 0.3f, new Vector3(5.2f, 6f, 0.4f), stone, Quaternion.LookRotation(back));
                hub.keepOut = -back;
                hub.keepDoorPos = dp + new Vector3(0, 2.5f, 0) - back * 0.6f;
                hub.keepFrontPos = dp - back * 3f + new Vector3(0, 0.1f, 0);
                hub.keepLabel = "Headmaster's Office (click door)";
            }

            AddDistanceCulling(root, z.detailDistance);
            float span = 4f * cs + 80f;
            hub.cullBounds = new Bounds(new Vector3(0, 45f, 0), new Vector3(W + span, 210f, H + span));
        }

        /// <summary>
        /// Landmark buildings and fixtures from a zone's landmark list. Hub-only pieces (keep, gates, arena pad, chest,
        /// pedestal, notice board) are skipped when hub is null (a street zone).
        /// </summary>
        private static void BuildLandmarks(Transform root, ZoneGrid g, ZoneDefinition z, HubLayout hub, Interior interior, WorldDataStore.Data data,
            Material stone, Material roof, Material gold, Material banner, Material door, Material window, Material wood)
        {
            float cs = g.cell;
            int stall = 0;
            if (z.landmarks != null)
                foreach (var l in z.landmarks)
                {
                    if (l == null) continue;
                    var cells = g.Find(l.Symbol);
                    if (cells.Count == 0) continue;
                    Vector2Int cell = cells[0];
                    Vector3 p = g.Local(cell);
                    Vector3 open = OpenFront(g, cell);
                    switch (l.kind)
                    {
                        case LandmarkKind.SchoolHall: SchoolHall(root, g, z, l, hub, interior, gold, door); break;
                        case LandmarkKind.Keep: if (hub != null) Keep(root, g, l, hub, stone, roof, gold, banner, door, window); break;
                        case LandmarkKind.Library: Library(root, g, l, stone, roof, gold, door, window); break;
                        case LandmarkKind.Fountain: HubFountain(root, g, l, stone, gold); break;
                        case LandmarkKind.ShopStall:
                            var awnings = new[] { new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.85f), new Color(0.9f, 0.6f, 0.15f), new Color(0.2f, 0.65f, 0.3f), new Color(0.6f, 0.25f, 0.75f) };
                            foreach (var sc in cells)
                            {
                                Vector3 so = OpenFront(g, sc);
                                Stall(root, g.Local(sc), Mathf.Atan2(so.x, so.z) * Mathf.Rad2Deg, wood, PMat("Awning" + (stall % 5), awnings[stall % 5], 1f));
                                stall++;
                            }
                            if (!string.IsNullOrEmpty(l.label)) LandmarkSign(root, p + open * 2f + Vector3.up * 3.6f, open, l.label, new Color(1f, 0.9f, 0.6f));
                            break;
                        case LandmarkKind.Tower:
                        {
                            var fp = g.Footprint(l.Symbol);
                            Vector3 c = RectCenter(g, fp);
                            float rad = Mathf.Max(1.5f, Mathf.Min(fp.width, fp.height) * cs * 0.42f);
                            Tower(root, c, rad, 16f, 7f, roof, stone, gold);
                            Pennant(root, c + new Vector3(0, 23.5f, 0), gold, banner);
                            if (!string.IsNullOrEmpty(l.label)) LandmarkSign(root, c + open * (rad + 0.4f) + Vector3.up * 4f, open, l.label, new Color(1f, 0.85f, 0.4f));
                            break;
                        }
                        case LandmarkKind.Statue: Statue(root, p, open, stone, gold, l.label); break;
                        case LandmarkKind.Gazebo: Gazebo(root, p, wood, roof, gold); break;
                        case LandmarkKind.StreetGate: if (hub != null) HubGateLandmark(root, g, l, cell, hub, data, stone); break;
                        case LandmarkKind.ArenaPad:
                            if (hub == null) break;
                            hub.padPos = p + new Vector3(0, 0.06f, 0);
                            hub.padRequiredQuest = l.requiredQuest;
                            hub.padLockedHint = l.lockedHint ?? "";
                            break;
                        case LandmarkKind.BankChest: if (hub != null) hub.chestPos = p; break;
                        case LandmarkKind.BackpackPedestal: if (hub != null) hub.pedestalPos = p; break;
                        case LandmarkKind.NoticeBoard:
                        {
                            if (hub == null) break;
                            Quaternion rot = Quaternion.LookRotation(open);
                            Vector3 side = Vector3.Cross(Vector3.up, open);
                            foreach (float sx in new[] { -1.3f, 1.3f })
                                Block(root, "BoardPost", p + side * sx + new Vector3(0, 1.4f, 0), new Vector3(0.2f, 2.8f, 0.2f), wood);
                            Block(root, "NoticeBoard", p + new Vector3(0, 1.9f, 0) - open * 0.1f, new Vector3(2.8f, 1.7f, 0.15f), wood, rot);
                            Block(root, "BoardRoof", p + new Vector3(0, 2.95f, 0) - open * 0.1f, new Vector3(3.2f, 0.15f, 0.6f), roof, rot);
                            hub.canvasPos = p + new Vector3(0, 1.9f, 0) + open * 0.02f;
                            hub.canvasRot = Quaternion.LookRotation(-open);
                            break;
                        }
                    }
                }
        }

        private static bool SolidLandmark(ZoneDefinition z, char c)
        {
            var l = z.FindLandmark(c);
            return l != null && l.Solid;
        }

        /// <summary>Unit direction a one-cell fixture faces: its open side nearest the map centre.</summary>
        private static Vector3 OpenFront(ZoneGrid g, Vector2Int cell)
        {
            Vector2Int d = g.FrontSide(new RectInt(cell.x, cell.y, 1, 1));
            if (d == Vector2Int.zero) d = g.OpenDirection(cell);
            return ZoneGrid.ToLocalDir(d);
        }

        /// <summary>Castle rampart over a block of edge cells, crenellated along the top.</summary>
        private static void Rampart(Transform parent, ZoneGrid g, RectInt r, Material stone, Material roof)
        {
            float cs = g.cell, h = 8f;
            Vector3 c = RectCenter(g, r);
            Block(parent, "Rampart", c + new Vector3(0, h / 2f, 0), new Vector3(r.width * cs, h, r.height * cs), stone);
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        if (!g.Walkable(x + d.x, y + d.y)) continue;
                        Vector3 dir = ZoneGrid.ToLocalDir(d), side = Vector3.Cross(Vector3.up, dir);
                        Vector3 edge = g.Local(x, y) + dir * (cs * 0.5f - 0.35f);
                        foreach (float t in new[] { -0.3f, 0.3f })
                            Deco(parent, PrimitiveType.Cube, "Merlon", edge + side * (cs * t) + new Vector3(0, h + 0.6f, 0), new Vector3(cs * 0.3f, 1.2f, 0.7f), stone,
                                Quaternion.LookRotation(dir), false);
                    }
        }

        /// <summary>Round towers at the wall corners and every few cells along it.</summary>
        private static void WallTowers(Transform parent, ZoneGrid g, Material stone, Material roof, Material gold, Material banner)
        {
            var spots = new HashSet<Vector2Int>();
            for (int x = 0; x < g.width; x++)
            {
                if (x % 8 == 0 || x == g.width - 1) { spots.Add(new Vector2Int(x, 0)); spots.Add(new Vector2Int(x, g.height - 1)); }
            }
            for (int y = 0; y < g.height; y++)
            {
                if (y % 8 == 0 || y == g.height - 1) { spots.Add(new Vector2Int(0, y)); spots.Add(new Vector2Int(g.width - 1, y)); }
            }
            foreach (var s in spots)
            {
                if (g.At(s.x, s.y) != '#') continue;
                bool corner = (s.x == 0 || s.x == g.width - 1) && (s.y == 0 || s.y == g.height - 1);
                float rad = g.cell * (corner ? 0.75f : 0.6f), h = corner ? 15f : 12f;
                Vector3 p = g.Local(s);
                Tower(parent, p, rad, h, 6f, roof, stone, gold);
                Pennant(parent, p + new Vector3(0, h + 6.5f, 0), gold, banner);
            }
        }

        /// <summary>Lamp posts along the paths, at the edge of the paving next to a lawn or wall.</summary>
        private static void HubLamps(Transform parent, ZoneGrid g, ZoneDefinition z, Material post, Material glow)
        {
            if (z.lampDensity <= 0f) return;
            var rings = new List<Vector2Int>();
            foreach (char d in "123456789") rings.AddRange(g.Find(d));
            int count = 0;
            for (int y = 0; y < g.height && count < 120; y++)
                for (int x = 0; x < g.width && count < 120; x++)
                {
                    if (g.At(x, y) != '.' || g.Hash(x, y, 7) > z.lampDensity * 0.35f) continue;
                    bool nearRing = false;
                    foreach (var r in rings) if (Mathf.Abs(r.x - x) <= 2 && Mathf.Abs(r.y - y) <= 2) nearRing = true;
                    if (nearRing) continue;
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        char n = g.At(x + d.x, y + d.y);
                        if (n != ',' && n != '^' && n != '=') continue;
                        Lamp(parent, g.Local(x, y) + ZoneGrid.ToLocalDir(d) * (g.cell * 0.42f), post, glow);
                        count++;
                        break;
                    }
                }
        }

        /// <summary>Sign readable by someone standing on the 'toward' side, looking back at it.</summary>
        private static Text LandmarkSign(Transform parent, Vector3 pos, Vector3 toward, string text, Color color)
        {
            var t = WorldText(parent, "Sign", pos, Quaternion.LookRotation(-toward), new Vector2(1200, 220), 0.005f, text, 70);
            t.color = color;
            return t;
        }

        /// <summary>
        /// A building filling its map block, with its door on the side facing the map centre. Returns the door's centre
        /// on the front face (at ground level) and the outward direction.
        /// </summary>
        private static Vector3 HubBuilding(Transform parent, ZoneGrid g, ZoneLandmark l, float height, float roofH, Material wall, Material roofMat,
            Material gold, Material doorMat, out Vector3 front, out float across)
        {
            float cs = g.cell;
            var fp = g.Footprint(l.Symbol);
            Vector2Int d = g.FrontSide(fp);
            if (d == Vector2Int.zero) d = new Vector2Int(0, 1);
            front = ZoneGrid.ToLocalDir(d);
            Vector3 c = RectCenter(g, fp);
            float sx = fp.width * cs - 1f, sz = fp.height * cs - 1f;
            float depth = d.x != 0 ? sx : sz;
            across = d.x != 0 ? sz : sx;
            Block(parent, "Building_" + l.symbol, c + new Vector3(0, height / 2f, 0), new Vector3(sx, height, sz), wall);
            Block(parent, "Eaves_" + l.symbol, c + new Vector3(0, height + 0.2f, 0), new Vector3(sx + 1.2f, 0.4f, sz + 1.2f), roofMat);
            Shape(parent, "Roof_" + l.symbol, PyramidMesh(), c + new Vector3(0, height + 0.4f + roofH / 2f, 0), new Vector3(sx + 1f, roofH, sz + 1f), roofMat, false, Quaternion.identity);
            Vector3 face = c + front * (depth / 2f);
            Quaternion rot = Quaternion.LookRotation(front);
            Block(parent, "Door_" + l.symbol, face + front * 0.08f + new Vector3(0, 2.6f, 0), new Vector3(3.6f, 5.2f, 0.2f), doorMat, rot);
            Block(parent, "DoorArch_" + l.symbol, face + front * 0.12f + new Vector3(0, 5.5f, 0), new Vector3(4.4f, 0.6f, 0.3f), gold, rot);
            // Front corner turrets.
            Vector3 side = Vector3.Cross(Vector3.up, front);
            foreach (float s in new[] { -1f, 1f })
                Tower(parent, face + side * (s * across / 2f), 1.4f, height + 3f, 4f, roofMat, wall, gold);
            return face;
        }

        private static void FrontWindows(Transform parent, Vector3 face, Vector3 front, float across, float y, Material mat)
        {
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Quaternion rot = Quaternion.LookRotation(front);
            int n = Mathf.Clamp(Mathf.FloorToInt(across / 4f), 1, 6);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1) - 0.5f) * (across - 5f);
                if (Mathf.Abs(t) < 2.6f && n > 1) continue;
                Block(parent, "Window", face + front * 0.06f + side * t + new Vector3(0, y, 0), new Vector3(1.2f, 2f, 0.1f), mat, rot);
            }
        }

        private static void SchoolHall(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark l, HubLayout hub, Interior interior, Material gold, Material door)
        {
            int s = Mathf.Clamp((int)l.school, 0, 6);
            Color col = SchoolColors[s];
            string sn = SchoolLabels[s];
            var wall = PMat("HallWall" + sn, Color.Lerp(z.buildingColor, col, 0.3f), 3f);
            var roofMat = PMat("HallRoof" + sn, Color.Lerp(col, Color.black, 0.3f), 2f);
            var bright = PMat("School" + sn + "Glow", col, 1f, col * 0.6f);
            var cloth = PMat("School" + sn + "Cloth", col, 1f);
            Vector3 front;
            float across;
            Vector3 face = HubBuilding(root, g, l, 10f, 6f, wall, roofMat, gold, door, out front, out across);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float outYaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
            FrontWindows(root, face, front, across, 6.6f, bright);
            Shape(root, "HallCrest", GemMesh(), face + front * 0.3f + new Vector3(0, 7.6f, 0), new Vector3(1.4f, 1.8f, 0.4f), bright, false, Quaternion.LookRotation(front));
            foreach (float t in new[] { -2.9f, 2.9f })
                StarBanner(root, face + front * 0.15f + side * t + new Vector3(0, 4.2f, 0), outYaw, cloth, gold);
            LandmarkSign(root, face + front * 0.4f + new Vector3(0, 9.2f, 0), front, string.IsNullOrEmpty(l.label) ? "School of " + sn : l.label, col);
            SchoolPorch(root, s, face + front * 1.8f, front, col, bright);
            if (hub == null || !hub.outdoorSchools || interior == null) return;

            // The professor waits outside the door (Talk / Learn Spells); the "classroom" transform frames their cutscene.
            var room = Child(root, "Classroom_" + sn, face + front * 3.2f);
            room.localRotation = Quaternion.LookRotation(-front);
            interior.classrooms[s] = room;
            Color hat = Color.Lerp(col, Color.black, 0.35f);
            interior.professors[s] = Wizard(room, "Professor" + sn, Vector3.zero, 0f, col, hat, s == 1 || s == 6, ProfessorNames[s]);
        }

        /// <summary>Two school-themed props flanking a hall door.</summary>
        private static void SchoolPorch(Transform root, int s, Vector3 at, Vector3 front, Color col, Material bright)
        {
            Vector3 side = Vector3.Cross(Vector3.up, front);
            foreach (float t in new[] { -3.6f, 3.6f })
            {
                Vector3 p = at + side * t;
                switch (s)
                {
                    case 0:
                        Shape(root, "Brazier", CylMesh(), p + new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.6f, 0.9f), PMat("Iron", new Color(0.25f, 0.22f, 0.22f), 1f), true, Quaternion.identity);
                        Shape(root, "BrazierFlame", ConeMesh(), p + new Vector3(0, 1.7f, 0), new Vector3(0.8f, 1f, 0.8f), PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f)), false, Quaternion.identity);
                        break;
                    case 1:
                        Shape(root, "SnowDrift", BlobMesh(), p + new Vector3(0, 0.1f, 0), new Vector3(2f, 0.6f, 2f), PMat("Snow", new Color(0.95f, 0.97f, 1f), 4f), false, Quaternion.identity);
                        Crystal(root, p + new Vector3(0, 0.9f, 0), 1.3f, new Color(0.62f, 0.85f, 1f));
                        break;
                    case 2:
                        Shape(root, "Rod", CylMesh(), p + new Vector3(0, 1.4f, 0), new Vector3(0.3f, 1.4f, 0.3f), PMat("Copper", new Color(0.75f, 0.45f, 0.25f), 1f), true, Quaternion.identity);
                        Shape(root, "RodSpark", GemMesh(), p + new Vector3(0, 3.1f, 0), new Vector3(0.6f, 0.8f, 0.6f), PMat("Spark", new Color(1f, 0.95f, 0.4f), 1f, new Color(0.9f, 0.85f, 0.3f)), false, Quaternion.identity);
                        break;
                    case 3:
                        FlowerBed(root, p, new Vector2(1.8f, 1.8f), PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f));
                        Mushroom(root, p + side * (t > 0 ? 1.4f : -1.4f), 0.35f);
                        break;
                    case 4:
                        var grave = PMat("Gravestone", new Color(0.45f, 0.45f, 0.5f), 2f);
                        Block(root, "Gravestone", p + new Vector3(0, 0.7f, 0), new Vector3(0.9f, 1.4f, 0.25f), grave, Quaternion.LookRotation(front));
                        Shape(root, "Wisp", BlobMesh(), p + new Vector3(0, 2.2f, 0) + front * 0.4f, Vector3.one * 0.35f, PMat("GhostGlow", new Color(0.5f, 1f, 0.7f), 1f, new Color(0.3f, 0.8f, 0.5f)), false, Quaternion.identity);
                        break;
                    case 5:
                        var statue = PMat("StatueGold", new Color(0.9f, 0.75f, 0.3f), 1f);
                        Block(root, "Plinth", p + new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1f, 1.2f), PMat("Stone", new Color(0.66f, 0.62f, 0.7f), 4f));
                        Shape(root, "BeastBody", BlobMesh(), p + new Vector3(0, 1.5f, 0), new Vector3(0.9f, 0.9f, 1.3f), statue, false, Quaternion.LookRotation(front));
                        Shape(root, "BeastHead", BlobMesh(), p + new Vector3(0, 2.2f, 0) + front * 0.45f, Vector3.one * 0.55f, statue, false, Quaternion.identity);
                        break;
                    default:
                        var sand = PMat("Sand", new Color(0.9f, 0.78f, 0.52f), 8f);
                        Shape(root, "Pyramid", ConeMesh(), p + new Vector3(0, 0.8f, 0), new Vector3(1.8f, 1.6f, 1.8f), sand, true, Quaternion.Euler(0, 45, 0));
                        Shape(root, "Orb", GemMesh(), p + new Vector3(0, 2.1f, 0), new Vector3(0.5f, 0.7f, 0.5f), bright, false, Quaternion.identity);
                        break;
                }
            }
        }

        private static void Keep(Transform root, ZoneGrid g, ZoneLandmark l, HubLayout hub, Material stone, Material roof, Material gold, Material banner,
            Material door, Material window)
        {
            const float h = 16f;
            Vector3 front;
            float across;
            Vector3 face = HubBuilding(root, g, l, h, 8f, stone, roof, gold, door, out front, out across);
            var fp = g.Footprint(l.Symbol);
            Vector3 c = RectCenter(g, fp);
            float sx = fp.width * g.cell - 1f, sz = fp.height * g.cell - 1f;
            foreach (var corner in new[] { new Vector3(-sx / 2f, 0, -sz / 2f), new Vector3(sx / 2f, 0, -sz / 2f), new Vector3(-sx / 2f, 0, sz / 2f), new Vector3(sx / 2f, 0, sz / 2f) })
            {
                Tower(root, c + corner, 3.2f, h + 8f, 7f, roof, stone, gold);
                Pennant(root, c + corner + new Vector3(0, h + 15.5f, 0), gold, banner);
            }
            Tower(root, c + new Vector3(0, h, 0), 4f, 12f, 10f, roof, stone, gold);
            Pennant(root, c + new Vector3(0, h + 23f, 0), gold, banner);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Quaternion rot = Quaternion.LookRotation(front);
            Shape(root, "KeepRose", GemMesh(), face + front * 0.1f + new Vector3(0, 10f, 0), new Vector3(3.5f, 3.5f, 0.2f),
                PMat("StainedGlass", new Color(0.4f, 0.5f, 1f), 1f, new Color(0.35f, 0.3f, 0.8f)), false, rot * Quaternion.Euler(0, 0, 30));
            float outYaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
            foreach (float t in new[] { -4.5f, 4.5f }) StarBanner(root, face + front * 0.15f + side * t + new Vector3(0, 7f, 0), outYaw, banner, gold);
            FrontWindows(root, face, front, across, 12.5f, window);
            Planter(root, face + front * 2f + side * 4.2f, PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f));
            Planter(root, face + front * 2f - side * 4.2f, PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f));
            hub.keepOut = front;
            hub.keepDoorPos = face + front * 0.2f + new Vector3(0, 2.5f, 0);
            hub.keepFrontPos = face + front * 4f + new Vector3(0, 0.1f, 0);
            hub.keepLabel = string.IsNullOrEmpty(l.label) ? "Headmaster's Tower (click door)" : l.label;
        }

        private static void Library(Transform root, ZoneGrid g, ZoneLandmark l, Material stone, Material roof, Material gold, Material door, Material window)
        {
            Vector3 front;
            float across;
            Vector3 face = HubBuilding(root, g, l, 11f, 6f, stone, roof, gold, door, out front, out across);
            var fp = g.Footprint(l.Symbol);
            Vector3 c = RectCenter(g, fp);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float depth = (Mathf.Abs(front.x) > 0.5f ? fp.width : fp.height) * g.cell - 1f;
            Vector3 towerAt = c - front * (depth / 2f - 3f) + side * (across / 2f - 3f);
            Tower(root, towerAt, 3.6f, 18f, 7f, roof, stone, gold);
            Crystal(root, towerAt + new Vector3(0, 26.5f, 0), 0.8f, new Color(0.8f, 0.5f, 1f));
            FrontWindows(root, face, front, across, 6.4f, window);
            var books = new[] { PMat("BookRed", new Color(0.8f, 0.2f, 0.2f), 1f), PMat("BookBlue", new Color(0.2f, 0.35f, 0.85f), 1f), PMat("BookGreen", new Color(0.2f, 0.65f, 0.3f), 1f) };
            for (int i = 0; i < 5; i++)
                Block(root, "SignBook", face + front * 0.3f + side * (-1.2f + i * 0.6f) + new Vector3(0, 7.6f, 0), new Vector3(0.45f, 1.1f, 0.3f), books[i % 3], Quaternion.LookRotation(front));
            LandmarkSign(root, face + front * 0.4f + new Vector3(0, 9.4f, 0), front, string.IsNullOrEmpty(l.label) ? "Library" : l.label, new Color(1f, 0.85f, 0.4f));
        }

        private static void HubFountain(Transform root, ZoneGrid g, ZoneLandmark l, Material stone, Material gold)
        {
            var fp = g.Footprint(l.Symbol);
            Vector3 c = RectCenter(g, fp);
            float rad = Mathf.Max(2f, Mathf.Min(fp.width, fp.height) * g.cell * 0.5f - 0.4f);
            float f = Mathf.Clamp(rad / 4.5f, 1f, 2.2f);
            var waterBright = PMat("WaterBright", new Color(0.4f, 0.8f, 1f), 4f, new Color(0.25f, 0.55f, 0.8f));
            Shape(root, "FountainBasin", CylMesh(), c + new Vector3(0, 0.45f, 0), new Vector3(rad * 2f, 0.45f, rad * 2f), stone, true, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "FountainWater", CylMesh(), c + new Vector3(0, 0.92f, 0), new Vector3(rad * 2f - 1f, 0.03f, rad * 2f - 1f), waterBright, false, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "FountainPillar", CylMesh(), c + new Vector3(0, 1.3f * f, 0), new Vector3(1.4f * f, 1.3f * f, 1.4f * f), stone, true, Quaternion.identity);
            Shape(root, "FountainStar", GemMesh(), c + new Vector3(0, 2.8f * f, 0), new Vector3(3f, 0.5f, 3f) * f, gold, false, Quaternion.identity);
            Shape(root, "StarfallCrystal", GemMesh(), c + new Vector3(0, 5f * f, 0), new Vector3(1.8f, 3.4f, 1.8f) * f,
                PMat("Crystal_Starfall", new Color(0.35f, 0.6f, 1f), 1f, new Color(0.25f, 0.45f, 0.9f)), false, Quaternion.Euler(0, 15, 0));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Shape(root, "FountainJet", CylMesh(), c + new Vector3(Mathf.Cos(a) * rad * 0.55f, 1.6f, Mathf.Sin(a) * rad * 0.55f), new Vector3(0.25f, 0.8f, 0.25f), waterBright, false, Quaternion.identity);
            }
            var light = Child(root, "CrystalLight", c + new Vector3(0, 5f * f, 0)).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.7f, 1f);
            light.range = rad * 3f;
            light.intensity = 1.2f;
            if (!string.IsNullOrEmpty(l.label))
                WorldText(root, "FountainSign", c + new Vector3(0, 10f * f, 0), Quaternion.identity, new Vector2(1200, 220), 0.006f, l.label, 70).color = new Color(0.7f, 0.85f, 1f);
        }

        private static void Statue(Transform root, Vector3 p, Vector3 front, Material stone, Material gold, string label)
        {
            Quaternion rot = Quaternion.LookRotation(front);
            Block(root, "StatuePlinth", p + new Vector3(0, 0.8f, 0), new Vector3(2.4f, 1.6f, 2.4f), stone, rot);
            Shape(root, "StatueRobe", ConeMesh(), p + new Vector3(0, 2.9f, 0), new Vector3(1.6f, 2.6f, 1.6f), stone, false, Quaternion.identity);
            Shape(root, "StatueHead", BlobMesh(), p + new Vector3(0, 4.5f, 0), Vector3.one * 0.7f, stone, false, Quaternion.identity);
            Shape(root, "StatueHat", ConeMesh(), p + new Vector3(0, 5.3f, 0), new Vector3(0.8f, 1.3f, 0.8f), stone, false, Quaternion.Euler(-8, 0, 0));
            Shape(root, "StatueStar", GemMesh(), p + new Vector3(0, 5f, 0) + front * 0.9f, new Vector3(0.5f, 0.7f, 0.5f), gold, false, Quaternion.identity);
            if (!string.IsNullOrEmpty(label)) LandmarkSign(root, p + front * 1.4f + new Vector3(0, 1.1f, 0), front, label, new Color(1f, 0.85f, 0.4f));
        }

        /// <summary>A portal arch: click it to go to the gate's street (or be told why it's still locked).</summary>
        private static void HubGateLandmark(Transform root, ZoneGrid g, ZoneLandmark l, Vector2Int cell, HubLayout hub, WorldDataStore.Data data, Material stone)
        {
            string key = l.targetZone ?? "";
            if (key.Length == 0) return;
            if (hub.gates.ContainsKey(key))
            {
                Debug.LogWarning("[WizardsVRC World] Two hub gates lead to '" + key + "'; only the first is used.");
                return;
            }
            ZoneDefinition target = null;
            foreach (var t in data.zones) if (t != null && t.includeInBuild && !t.isHub && t.zoneKey == key) target = t;
            if (target == null) return;

            Vector3 front = OpenFront(g, cell);
            Vector3 p = g.Local(cell);
            Transform arch = Child(root, "Gate_" + key, p);
            arch.localRotation = Quaternion.LookRotation(front);
            Color c = target.accentColor;
            PortalArch(arch, Vector3.zero, stone, PMat("Portal_" + key, c, 1f, c * 0.8f));

            var click = Child(arch, "GateClick", new Vector3(0, 2.6f, 0)).gameObject;
            click.AddComponent<BoxCollider>().size = new Vector3(4f, 5.2f, 0.8f);
            var qg = UdonSharpUndo.AddComponent<QuestGate>(click);
            var tp = UdonSharpUndo.AddComponent<TeleportHelper>(Child(arch, "GateTeleport", Vector3.zero).gameObject);
            qg.teleporter = tp;
            qg.requiredQuest = l.requiredQuest;
            string name = string.IsNullOrEmpty(l.label) ? target.displayName : l.label;
            qg.openText = name + "  " + target.levelRange + "\n(click the gate)";
            qg.lockedText = name + "\n" + (string.IsNullOrEmpty(l.lockedHint) ? "Locked" : l.lockedHint);
            qg.label = WorldText(root, "GateLabel_" + key, p + new Vector3(0, 7.6f, 0), Quaternion.LookRotation(-front), new Vector2(1300, 260), 0.0045f,
                l.requiredQuest >= 0 ? qg.lockedText : qg.openText, 58);
            qg.label.color = new Color(1f, 0.85f, 0.4f);
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(qg);
            if (backing != null) backing.interactText = "Enter " + name;

            Transform ret = Child(root, key + "Return", p + front * (g.cell * 0.75f) + new Vector3(0, 0.1f, 0));
            ret.localRotation = Quaternion.LookRotation(front);
            hub.gates[key] = new HubGate { landmark = l, clickable = click, gate = qg, teleport = tp, front = ret };
            hub.behaviours.Add(qg);
            hub.behaviours.Add(tp);
        }

        // ================================================================== contents (after the quest system exists)

        /// <summary>The hub's NPCs, quest points and practice ring, plus the gates' quest locks.</summary>
        private static void BuildHubContents(HubLayout hub, Transform systems, QuestManager qm, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, List<BattleClient> encounters, WorldBuild wb)
        {
            foreach (var gate in hub.gates.Values)
            {
                gate.gate.questManager = qm;
                gate.gate.playerState = playerState;
            }
            wb.behaviours.AddRange(hub.behaviours);
            if (hub.zone == null) return;
            var z = hub.zone;
            var g = hub.grid;
            string k = z.zoneKey;
            Material glow = PMat(k + "_Glow", z.accentColor, 1f, z.accentColor);
            Material deadLamp = PMat(k + "_DeadLamp", new Color(0.12f, 0.12f, 0.14f), 1f);
            Material wood = PMat(k + "_Wood", new Color(0.32f, 0.21f, 0.14f), 2f);
            Vector2Int start = g.Find('S')[0];
            foreach (var npc in z.npcs) if (npc != null) BuildNpc(hub.root, g, z, npc, qm, wb);
            foreach (var p in z.points) if (p != null) BuildPoints(hub.root, g, p, qm, wb, glow, deadLamp, wood);
            foreach (var e in z.encounters)
                if (e != null && ZoneGrid.IsRingDigit(e.Symbol))
                    BuildZoneEncounter(hub.root, g, z, e, start, hub.spawn, systems, cardDb, itemDb, playerState, qm, encounters, wb);
        }

        // ================================================================== meshes

        /// <summary>Four-sided roof: base -0.5..0.5 at y -0.5, apex at y 0.5.</summary>
        private static Mesh PyramidMesh()
        {
            var t = new List<Vector3>();
            var apex = new Vector3(0, 0.5f, 0);
            var corners = new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f) };
            for (int i = 0; i < 4; i++) { t.Add(apex); t.Add(corners[i]); t.Add(corners[(i + 1) % 4]); }
            t.Add(corners[0]); t.Add(corners[1]); t.Add(corners[2]);
            t.Add(corners[0]); t.Add(corners[2]); t.Add(corners[3]);
            return SaveMesh("PSX_Pyramid", t, new Vector3(0, -0.25f, 0));
        }
    }
}
