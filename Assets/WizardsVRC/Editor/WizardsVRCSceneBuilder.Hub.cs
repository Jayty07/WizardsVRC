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
        /// <summary>A tunnel in the hub that leads to a street zone; the street pass builds the other end and links them.</summary>
        private class HubGate
        {
            public ZoneLandmark landmark;
            public TunnelEnd tunnel;
            public TunnelGate gate;
            /// <summary>Just outside the tunnel's mouth, facing the castle (guide arrows).</summary>
            public Transform front;
        }

        /// <summary>A health or mana orb to build once the player state exists.</summary>
        private class HubOrb
        {
            public Vector3 pos;
            public bool mana;
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
            public readonly List<HubOrb> orbs = new List<HubOrb>();
            /// <summary>Duelling circles (PvP rings): their centre and the way back out (where duellists go afterwards).</summary>
            public readonly List<Transform> duelSpots = new List<Transform>();
            public readonly List<Transform> duelReturns = new List<Transform>();
            public readonly List<Text> duelLabels = new List<Text>();
            /// <summary>Orb behaviours, given the interface sounds once those exist.</summary>
            public readonly List<RestoreOrb> orbBehaviours = new List<RestoreOrb>();
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
                        plan.grid.DoorDirection(l) != Vector2Int.zero)
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

            // ---- ground: an island in the sea (castle style) or a plain slab, at the lowest level of the height map;
            // higher ground is built on top of it by BuildTerrain.
            float baseY = g.minLevel * g.step;
            bool round = z.castleStyle && z.roundCastle;
            if (z.castleStyle)
            {
                float m = cs * 2f, hw = W / 2f + m, hh = H / 2f + m, c = m * 1.5f;
                var poly = new[]
                {
                    new Vector2(-hw + c, -hh), new Vector2(hw - c, -hh), new Vector2(hw, -hh + c), new Vector2(hw, hh - c),
                    new Vector2(hw - c, hh), new Vector2(-hw + c, hh), new Vector2(-hw, hh - c), new Vector2(-hw, -hh + c),
                };
                string dims = Mathf.RoundToInt(W) + "x" + Mathf.RoundToInt(H) + (g.minLevel != 0 ? "_b" + Mathf.RoundToInt(-baseY * 10f) : "");
                if (round)
                {
                    // A round island just bigger than the map's circle (the corners of the map are left out).
                    float ri = Mathf.Min(W, H) / 2f + m;
                    poly = new Vector2[48];
                    for (int i = 0; i < poly.Length; i++)
                    {
                        float a = (i + 0.5f) / poly.Length * Mathf.PI * 2f;
                        poly[i] = new Vector2(Mathf.Cos(a) * ri, Mathf.Sin(a) * ri);
                    }
                    dims = "Round" + Mathf.RoundToInt(ri) + (g.minLevel != 0 ? "_b" + Mathf.RoundToInt(-baseY * 10f) : "");
                }
                // The island's top sits GroundDrop below the map's terrain (which covers every map cell), so the two
                // never fight; only the strip outside the map shows it.
                float islandTop = baseY - GroundDrop;
                Shape(root, "IslandCliff", PrismMesh("PSX_HubIsland2_" + dims, poly, -7f, islandTop - 0.2f), Vector3.zero, Vector3.one, cliff, true, Quaternion.identity);
                Shape(root, "IslandTop", PrismMesh("PSX_HubIslandTop2_" + dims, poly, islandTop - 0.2f, islandTop), Vector3.zero, Vector3.one, ground, true, Quaternion.identity);
                // The sea: a big subdivided PSX water plane rolling with long swells and whitecaps (the island's cliffs
                // stand in it), and surf breaking round a round island's shore.
                var sea = Shape(root, "Sea", GridMesh(96, 96, false), new Vector3(0, -6.2f, 0), new Vector3(W + 420f, 1f, H + 420f), PsxSwellMat(), false,
                    Quaternion.identity);
                sea.isStatic = false;
                if (round)
                {
                    float ri = Mathf.Min(W, H) / 2f + m;
                    var surfMat = PsxSurfMat();
                    if (surfMat != null)
                    {
                        var surf = Shape(root, "Surf", SurfRingMesh(ri - 1.5f, ri + 18f, 96), new Vector3(0, -5.6f, 0), Vector3.one, surfMat, false, Quaternion.identity);
                        surf.isStatic = false;
                    }
                }
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                    for (float t = 0.1f; t < 1f; t += round ? 0.45f : 0.2f)
                    {
                        Vector2 p = Vector2.Lerp(a, b, t) * 1.03f;
                        Shape(root, "ShoreRock", BlobMesh(), new Vector3(p.x, -6f, p.y), new Vector3(R(5, 9), R(3, 6), R(5, 9)), cliff, false, Quaternion.Euler(0, R(0, 360), 0));
                    }
                }
            }
            else Block(root, "Ground", new Vector3(0, baseY - GroundDrop - 0.25f, 0), new Vector3(W, 0.5f, H), ground);

            Transform bounds = Child(root, "Bounds", Vector3.zero);
            foreach (var side in new[] { new Vector4(0, H / 2f, W, 1f), new Vector4(0, -H / 2f, W, 1f), new Vector4(W / 2f, 0, 1f, H), new Vector4(-W / 2f, 0, 1f, H) })
            {
                var wall = Child(bounds, "Edge", new Vector3(side.x, 15f, side.y)).gameObject.AddComponent<BoxCollider>();
                wall.size = new Vector3(side.z, 30f, side.w);
            }

            // ---- terrain (heights, paving, lawns, kerbs, stairs, water), walls, hedges, trees
            Transform layout = Child(root, "Layout", Vector3.zero);
            var stairCells = new HashSet<Vector2Int>();
            BuildTerrain(layout, g, MakeTerrainMats(z, k), baseY, stairCells);
            int houseIndex = 0;
            if (round) RoundCurtainWall(layout, g, baseY, stone, roof, gold, banner, TunnelBearings(g, z, data));
            else
            {
                foreach (var r in Rects(g, ch => ch == '#', 3, 3))
                {
                    bool edge = r.xMin == 0 || r.yMin == 0 || r.xMax == g.width || r.yMax == g.height;
                    if (z.castleStyle && edge) Rampart(layout, g, r, stone, roof);
                    else House(layout, g, z, r, houseIndex++, stone, roof, window);
                }
                if (z.castleStyle) WallTowers(layout, g, stone, roof, gold, banner);
            }
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
            HubLamps(layout, g, z, wood, window, stairCells);

            // ---- landmarks
            BuildLandmarks(root, g, z, hub, interior, data, stone, roof, gold, banner, door, window, wood, hub.behaviours);

            // ---- arrival point
            Vector2Int start = g.Find('S')[0];
            Vector3 sp = g.Local(start);
            Vector3 look = -sp;
            look.y = 0f;
            if (look.magnitude < cs * 2f) look = ZoneGrid.ToLocalDir(g.OpenDirection(start));
            hub.spawnPos = sp + new Vector3(0, 0.15f, 0);
            hub.spawnYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            Quaternion face = Quaternion.Euler(0, hub.spawnYaw, 0);
            bool hasBoard = false, hasPedestal = false, hasPad = false;
            if (z.landmarks != null)
                foreach (var l in z.landmarks)
                {
                    if (l == null || g.Find(l.Symbol).Count == 0) continue;
                    hasBoard |= l.kind == LandmarkKind.NoticeBoard;
                    hasPedestal |= l.kind == LandmarkKind.BackpackPedestal;
                    hasPad |= l.kind == LandmarkKind.ArenaPad;
                }
            // Anything the map doesn't place goes next to the arrival point.
            if (!hasBoard) { hub.canvasPos = sp + face * new Vector3(-4f, 1.6f, 3f); hub.canvasRot = face; }
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
            Material stone, Material roof, Material gold, Material banner, Material door, Material window, Material wood, List<UdonSharpBehaviour> behaviours)
        {
            float cs = g.cell;
            int stall = 0, shop = 0;
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
                        case LandmarkKind.Shop: Shop(root, g, l, shop++, gold, door, window, wood); break;
                        case LandmarkKind.Waterfall: WaterfallLandmark(root, g, l, PMat("WaterfallRock", new Color(0.5f, 0.47f, 0.45f), 3f)); break;
                        case LandmarkKind.Tavern: TavernLandmark(root, g, z, l, behaviours, stone, roof, gold, window, wood); break;
                        case LandmarkKind.StreetGate: if (hub != null) HubTunnelLandmark(root, g, z, l, cell, hub, data, stone, gold); break;
                        case LandmarkKind.ArenaPad:
                            if (hub == null) break;
                            hub.padPos = p + new Vector3(0, 0.06f, 0);
                            hub.padRequiredQuest = l.requiredQuest;
                            hub.padLockedHint = l.lockedHint ?? "";
                            break;
                        case LandmarkKind.HealthOrb:
                        case LandmarkKind.ManaOrb:
                            if (hub != null) foreach (var oc in cells) hub.orbs.Add(new HubOrb { pos = g.Local(oc), mana = l.kind == LandmarkKind.ManaOrb });
                            break;
                        case LandmarkKind.DuelCircle: if (hub != null) DuelCircleLandmark(root, g, l, hub, stone, gold, wood); break;
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
        private static void HubLamps(Transform parent, ZoneGrid g, ZoneDefinition z, Material post, Material glow, HashSet<Vector2Int> stairCells)
        {
            if (z.lampDensity <= 0f) return;
            var rings = new List<Vector2Int>();
            foreach (char d in "123456789") rings.AddRange(g.Find(d));
            int count = 0;
            // The chance is per cell, so scale it by cell area: smaller cells mean more of them (6 m cells = as set).
            float chance = z.lampDensity * 0.35f * Mathf.Clamp(g.cell * g.cell / 36f, 0.2f, 2f);
            for (int y = 0; y < g.height && count < 160; y++)
                for (int x = 0; x < g.width && count < 160; x++)
                {
                    if (g.At(x, y) != '.' || g.Hash(x, y, 7) > chance || stairCells.Contains(new Vector2Int(x, y))) continue;
                    bool nearRing = false;
                    foreach (var r in rings) if (Mathf.Abs(r.x - x) <= 2 && Mathf.Abs(r.y - y) <= 2) nearRing = true;
                    if (nearRing) continue;
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        char n = g.At(x + d.x, y + d.y);
                        if ((n != ',' && n != '^' && n != '=') || g.Level(x + d.x, y + d.y) != g.Level(x, y)) continue;
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

        private const float HallWallThick = 0.6f;
        /// <summary>Width : depth of buildings that turn to face the centre (Door Side Centre).</summary>
        private const float HallAspect = 1.12f, ShopAspect = 1.36f, KeepAspect = 1.5f;

        /// <summary>
        /// Where a building stands in its block: centre (at the height of its cells), the way out of its door, and its
        /// size across and deep. Door Side Centre turns it to face the map's centre and makes it as big as fits in its
        /// block at that angle, 'aspect' times as wide as it is deep; otherwise it fills the block, door on its door side.
        /// </summary>
        private static void BuildingFrame(ZoneGrid g, ZoneLandmark l, float aspect, out Vector3 c, out Vector3 front, out float across, out float depth)
        {
            float cs = g.cell;
            var fp = g.Footprint(l.Symbol);
            c = RectCenter(g, fp);
            var cells = g.Find(l.Symbol);
            if (cells.Count > 0) c.y = g.Elevation(cells[0].x, cells[0].y);
            float sx = fp.width * cs - 1f, sz = fp.height * cs - 1f;
            var toCentre = new Vector3(-c.x, 0f, -c.z);
            if (l.doorSide == global::WizardsVRC.EditorTools.WorldData.DoorSide.Centre && toCentre.sqrMagnitude > 1f)
            {
                front = toCentre.normalized;
                float fx = Mathf.Abs(front.x), fz = Mathf.Abs(front.z), rho = Mathf.Max(0.3f, aspect);
                float k = Mathf.Min(sx / (fz * rho + fx), sz / (fx * rho + fz));
                across = rho * k;
                depth = k;
                return;
            }
            Vector2Int d = g.DoorDirection(l);
            if (d == Vector2Int.zero) d = new Vector2Int(0, 1);
            front = ZoneGrid.ToLocalDir(d);
            depth = d.x != 0 ? sx : sz;
            across = d.x != 0 ? sz : sx;
        }

        /// <summary>
        /// A building in its map block (see <see cref="BuildingFrame"/>), with its door on its front. Returns the door's
        /// centre on the front face (at ground level) and the outward direction. Hollow: real walls, ceiling and floor
        /// with an open doorway you can walk through (the inside is the building minus the walls).
        /// </summary>
        private static Vector3 HubBuilding(Transform parent, ZoneGrid g, ZoneLandmark l, float height, float roofH, Material wall, Material roofMat,
            Material gold, Material doorMat, out Vector3 front, out float across, bool hollow = false, bool turrets = true, float doorH = 5.2f,
            Material floorMat = null, float aspect = HallAspect)
        {
            Vector3 c;
            float depth;
            BuildingFrame(g, l, aspect, out c, out front, out across, out depth);
            Quaternion rot = Quaternion.LookRotation(front);
            float doorW = doorH * 0.7f;
            if (hollow)
            {
                // Walls in the building's own frame (+Z = out of the door).
                Transform b = Child(parent, "Building_" + l.symbol, c);
                b.localRotation = rot;
                float t = HallWallThick, gapW = doorW + 0.6f, gapH = doorH + 0.4f;
                float seg = (across - gapW) / 2f;
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Block(b, "FrontWall", new Vector3(sgn * (gapW / 2f + seg / 2f), height / 2f, depth / 2f - t / 2f), new Vector3(seg, height, t), wall);
                    Block(b, "SideWall", new Vector3(sgn * (across / 2f - t / 2f), height / 2f, 0), new Vector3(t, height, depth - 2f * t), wall);
                }
                Block(b, "Lintel", new Vector3(0, (gapH + height) / 2f, depth / 2f - t / 2f), new Vector3(gapW, height - gapH, t), wall);
                Block(b, "BackWall", new Vector3(0, height / 2f, -depth / 2f + t / 2f), new Vector3(across, height, t), wall);
                Block(b, "Ceiling", new Vector3(0, height - 0.25f, 0), new Vector3(across - 0.1f, 0.5f, depth - 0.1f), wall);
                if (floorMat != null)
                    Deco(b, PrimitiveType.Cube, "Floor", new Vector3(0, 0.07f, 0), new Vector3(across - 2f * t, 0.06f, depth - 2f * t), floorMat, Quaternion.identity, false);
                // The doors stand open, swung inwards against the wall.
                foreach (float sgn in new[] { -1f, 1f })
                {
                    Transform hinge = Child(b, "DoorHinge", new Vector3(sgn * gapW / 2f, 0, depth / 2f - t));
                    hinge.localRotation = Quaternion.Euler(0, sgn * -100f, 0);
                    Block(hinge, "DoorLeaf", new Vector3(-sgn * gapW / 4f, doorH / 2f, -0.08f), new Vector3(gapW / 2f - 0.05f, doorH, 0.15f), doorMat);
                }
            }
            else Block(parent, "Building_" + l.symbol, c + new Vector3(0, height / 2f, 0), new Vector3(across, height, depth), wall, rot);
            Block(parent, "Eaves_" + l.symbol, c + new Vector3(0, height + 0.2f, 0), new Vector3(across + 1.2f, 0.4f, depth + 1.2f), roofMat, rot);
            Shape(parent, "Roof_" + l.symbol, PyramidMesh(), c + new Vector3(0, height + 0.4f + roofH / 2f, 0), new Vector3(across + 1f, roofH, depth + 1f), roofMat, false, rot);
            Vector3 face = c + front * (depth / 2f);
            if (!hollow) Block(parent, "Door_" + l.symbol, face + front * 0.08f + new Vector3(0, doorH / 2f, 0), new Vector3(doorW, doorH, 0.2f), doorMat, rot);
            Block(parent, "DoorArch_" + l.symbol, face + front * 0.12f + new Vector3(0, doorH + 0.3f, 0), new Vector3(doorW + 0.8f, 0.6f, 0.3f), gold, rot);
            if (turrets)
            {
                Vector3 side = Vector3.Cross(Vector3.up, front);
                foreach (float s2 in new[] { -1f, 1f })
                    Tower(parent, face + side * (s2 * across / 2f), 1.4f, height + 3f, 4f, roofMat, wall, gold);
            }
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
            // With every school in the hub, each hall is a walk-in classroom with its professor inside.
            bool walkIn = hub != null && hub.outdoorSchools && interior != null;
            const float hallH = 10f;
            var floorMat = PsxTexMat("ClassFloor" + sn, Color.Lerp(new Color(0.62f, 0.5f, 0.38f), col, 0.12f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.6f);
            Vector3 front;
            float across;
            Vector3 face = HubBuilding(root, g, l, hallH, 6f, wall, roofMat, gold, door, out front, out across, walkIn, true, 5.2f, floorMat);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            float outYaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
            FrontWindows(root, face, front, across, 6.6f, bright);
            Shape(root, "HallCrest", GemMesh(), face + front * 0.3f + new Vector3(0, 7.6f, 0), new Vector3(1.4f, 1.8f, 0.4f), bright, false, Quaternion.LookRotation(front));
            foreach (float t in new[] { -2.9f, 2.9f })
                StarBanner(root, face + front * 0.15f + side * t + new Vector3(0, 4.2f, 0), outYaw, cloth, gold);
            LandmarkSign(root, face + front * 0.4f + new Vector3(0, 9.2f, 0), front, string.IsNullOrEmpty(l.label) ? "School of " + sn : l.label, col);
            SchoolPorch(root, s, face + front * 1.8f, front, col, bright);
            if (!walkIn) return;

            // ---- the classroom inside: local +Z is the back wall, the door is behind you as you face the professor
            Vector3 c, f2;
            float a2, depth;
            BuildingFrame(g, l, HallAspect, out c, out f2, out a2, out depth);
            float inW = across - 2f * HallWallThick, inD = depth - 2f * HallWallThick;
            var room = Child(root, "Classroom_" + sn, c);
            room.localRotation = Quaternion.LookRotation(-front);
            interior.classrooms[s] = room;
            var trim = PMat("HallTrim", new Color(0.45f, 0.38f, 0.55f), 2f);
            var wood = PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f);
            interior.professors[s] = FurnishClassroom(room, s, inW, inD, hallH - 0.5f, trim, wood);
            // Glowing windows down both side walls, and a school-coloured runner from the door to the lectern.
            for (float zz = -inD / 2f + 3f; zz < inD / 2f - 2f; zz += 5f)
                foreach (float x in new[] { -inW / 2f + 0.04f, inW / 2f - 0.04f })
                    Block(room, "InnerWindow", new Vector3(x, 4.6f, zz), new Vector3(0.08f, 2.4f, 1.3f), bright);
            Deco(room, PrimitiveType.Cube, "Runner", new Vector3(0, 0.12f, -inD / 4f), new Vector3(2.2f, 0.04f, inD / 2f - 1f), cloth, Quaternion.identity, false);
        }

        private static readonly Color[] ShopWalls =
        {
            new Color(0.93f, 0.88f, 0.76f), new Color(0.86f, 0.8f, 0.9f), new Color(0.82f, 0.9f, 0.86f), new Color(0.95f, 0.84f, 0.72f),
        };
        private static readonly Color[] ShopRoofs =
        {
            new Color(0.75f, 0.22f, 0.18f), new Color(0.2f, 0.36f, 0.72f), new Color(0.22f, 0.55f, 0.35f), new Color(0.55f, 0.28f, 0.65f),
            new Color(0.8f, 0.55f, 0.15f),
        };

        /// <summary>
        /// A little timber-framed shop or house filling its block: plaster walls, dark beams, a coloured roof and striped
        /// awning over the door, shop windows and a hanging sign with the landmark's label. Scenery (the door stays shut).
        /// </summary>
        private static void Shop(Transform root, ZoneGrid g, ZoneLandmark l, int index, Material gold, Material door, Material window, Material wood)
        {
            var fp = g.Footprint(l.Symbol);
            float hash = g.Hash(fp.xMin, fp.yMin, 31);
            Color wallCol = ShopWalls[index % ShopWalls.Length], roofCol = ShopRoofs[(index + Mathf.FloorToInt(hash * 3f)) % ShopRoofs.Length];
            var wall = PMat("ShopWall" + index % ShopWalls.Length, wallCol, 2f);
            var roofMat = PMat("ShopRoof" + ColorUtility.ToHtmlStringRGB(roofCol), roofCol, 2f);
            var beam = PMat("TimberBeam", new Color(0.3f, 0.19f, 0.12f), 2f);
            float h = 5.6f + hash * 1.8f;
            Vector3 front;
            float across;
            Vector3 face = HubBuilding(root, g, l, h, 4.5f, wall, roofMat, gold, door, out front, out across, false, false, 3f, null, ShopAspect);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Quaternion rot = Quaternion.LookRotation(front);
            Vector3 c, f2;
            float a2, depth;
            BuildingFrame(g, l, ShopAspect, out c, out f2, out a2, out depth);

            // Timber frame on every corner and a beam round the building at first-floor height.
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Block(root, "Beam", c + side * (sx * (across / 2f + 0.05f)) + front * (sz * (depth / 2f + 0.05f)) + new Vector3(0, h / 2f, 0), new Vector3(0.4f, h, 0.4f), beam, rot);
            Block(root, "BeamBand", c + new Vector3(0, 3.4f, 0), new Vector3(across + 0.3f, 0.3f, depth + 0.3f), beam, rot);
            foreach (float t in new[] { -1.4f, 1.4f })
                Block(root, "DoorPost", face + front * 0.12f + side * t + new Vector3(0, 1.6f, 0), new Vector3(0.3f, 3.2f, 0.2f), beam, rot);
            FrontWindows(root, face, front, across, 1.9f, window);
            FrontWindows(root, face, front, across, 4.5f, window);

            // Striped awning over the door and windows, tilted down and out.
            var stripe = PMat("AwningStripe", new Color(0.97f, 0.95f, 0.9f), 1f);
            float aw = Mathf.Min(across - 1f, 7f);
            int strips = Mathf.Max(3, Mathf.RoundToInt(aw / 0.9f));
            for (int i = 0; i < strips; i++)
            {
                float x = (i + 0.5f) / strips * aw - aw / 2f;
                Block(root, "Awning", face + front * 0.9f + side * x + new Vector3(0, 3.75f, 0), new Vector3(aw / strips + 0.01f, 0.08f, 1.8f),
                    i % 2 == 0 ? roofMat : stripe, rot * Quaternion.Euler(18f, 0, 0));
            }

            // Hanging sign on a bracket beside the door, plus the shop's name over the awning.
            Vector3 bracket = face + side * (across / 2f - 1.2f) + new Vector3(0, 4.6f, 0);
            Block(root, "SignBracket", bracket + front * 0.6f, new Vector3(0.12f, 0.12f, 1.2f), beam, rot);
            Block(root, "SignBoard", bracket + front * 1.05f + new Vector3(0, -0.5f, 0), new Vector3(0.1f, 0.8f, 0.9f), wood, rot);
            Shape(root, "SignGem", GemMesh(), bracket + front * 1.05f + new Vector3(0, -0.5f, 0), new Vector3(0.35f, 0.5f, 0.35f), gold, false, Quaternion.identity);
            if (!string.IsNullOrEmpty(l.label)) LandmarkSign(root, face + front * 0.5f + new Vector3(0, h - 0.6f, 0), front, l.label, new Color(1f, 0.92f, 0.65f));

            // Crates and a barrel by the door, and sometimes a chimney.
            Block(root, "Crate", face + front * 1.2f - side * (across / 2f - 1f) + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), wood, rot * Quaternion.Euler(0, 15f, 0));
            Shape(root, "Barrel", CylMesh(), face + front * 1.1f + side * 2.2f + new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.5f, 0.7f), beam, true, Quaternion.identity);
            if (hash > 0.4f)
                Block(root, "Chimney", c - front * (depth * 0.2f) + side * (across * 0.25f) + new Vector3(0, h + 2.4f, 0), new Vector3(0.9f, 2.6f, 0.9f), wall);
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
            Vector3 face = HubBuilding(root, g, l, h, 8f, stone, roof, gold, door, out front, out across, false, true, 5.2f, null, KeepAspect);
            Vector3 c, f2;
            float a2, depth;
            BuildingFrame(g, l, KeepAspect, out c, out f2, out a2, out depth);
            Vector3 kSide = Vector3.Cross(Vector3.up, front);
            foreach (var corner in new[] { kSide * (-across / 2f) - front * (depth / 2f), kSide * (across / 2f) - front * (depth / 2f),
                                           kSide * (-across / 2f) + front * (depth / 2f), kSide * (across / 2f) + front * (depth / 2f) })
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
            Vector3 c, f2;
            float a2, depth;
            BuildingFrame(g, l, HallAspect, out c, out f2, out a2, out depth);
            Vector3 side = Vector3.Cross(Vector3.up, front);
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
            // PSX water in the basin (a subdivided disc-sized plane inside the rim) and falling-water jets.
            var waterBright = PsxWaterfallMat();
            Shape(root, "FountainBasin", CylMesh(), c + new Vector3(0, 0.45f, 0), new Vector3(rad * 2f, 0.45f, rad * 2f), stone, true, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "FountainWater", GridMesh(8, 8, false), c + new Vector3(0, 0.88f, 0), new Vector3(rad * 1.3f, 1f, rad * 1.3f), PsxWaterMat(), false,
                Quaternion.Euler(0, 22.5f, 0));
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
            light.renderMode = LightRenderMode.ForceVertex;
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

        /// <summary>The street a Street Gate leads to, if it's built (null otherwise).</summary>
        private static ZoneDefinition GateTarget(ZoneLandmark l, WorldDataStore.Data data)
        {
            string key = l.targetZone ?? "";
            if (key.Length == 0) return null;
            foreach (var t in data.zones) if (t != null && t.includeInBuild && !t.isHub && t.zoneKey == key) return t;
            return null;
        }

        /// <summary>Bearings (degrees, 0 = north) of the hub's tunnels, so the round wall leaves a gateway for each.</summary>
        private static List<float> TunnelBearings(ZoneGrid g, ZoneDefinition z, WorldDataStore.Data data)
        {
            var list = new List<float>();
            var keys = new HashSet<string>();
            if (z.landmarks == null) return list;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.StreetGate || GateTarget(l, data) == null || !keys.Add(l.targetZone)) continue;
                var cells = g.Find(l.Symbol);
                if (cells.Count > 0) list.Add(BearingOf(g.Local(cells[0])));
            }
            return list;
        }

        /// <summary>Where a hub tunnel's mouth is and which way it runs: round castles bore through the curtain wall at the
        /// gate's bearing; otherwise the tunnel starts at the back of the gate's cell (away from its open side).</summary>
        private static void TunnelMouth(ZoneGrid g, ZoneDefinition z, Vector2Int cell, out Vector3 mouth, out Vector3 outward)
        {
            Vector3 p = g.Local(cell);
            if (z.castleStyle && z.roundCastle)
            {
                outward = new Vector3(p.x, 0f, p.z).normalized;
                mouth = outward * (g.RoundWallRadius() - 2f);
                mouth.y = p.y;
                return;
            }
            outward = -OpenFront(g, cell);
            mouth = p + outward * (g.cell * 0.5f);
        }

        /// <summary>
        /// A Street Gate becomes a tunnel into the mountains: a stone mouth with a portcullis that stays down until the
        /// gate's quest is done, a sign with the street's name, the Z-shaped tunnel itself and the twin peaks over it.
        /// The street pass builds the far end and links the two.
        /// </summary>
        private static void HubTunnelLandmark(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark l, Vector2Int cell, HubLayout hub,
            WorldDataStore.Data data, Material stone, Material gold)
        {
            var target = GateTarget(l, data);
            if (target == null) return;
            string key = target.zoneKey;
            if (hub.gates.ContainsKey(key))
            {
                Debug.LogWarning("[WizardsVRC World] Two hub gates lead to '" + key + "'; only the first is used.");
                return;
            }
            Vector3 mouth, outward;
            TunnelMouth(g, z, cell, out mouth, out outward);
            var tm = TunnelMats(z);
            var e = BuildTunnel(root, key, mouth, outward, tm[0], tm[1], tm[2], tm[3], hub.behaviours);

            string name = string.IsNullOrEmpty(l.label) ? target.displayName : l.label;
            var label = TunnelFacade(e, tm[0], gold, name, new Color(1f, 0.85f, 0.4f));
            var gate = UdonSharpUndo.AddComponent<TunnelGate>(e.root.gameObject);
            gate.barrier = Portcullis(e, tm[4]);
            gate.label = label;
            gate.requiredQuest = l.requiredQuest;
            gate.openText = name + "  " + target.levelRange + "\n(walk through the tunnel)";
            gate.lockedText = name + "\n" + (string.IsNullOrEmpty(l.lockedHint) ? "Locked" : l.lockedHint);
            label.text = l.requiredQuest >= 0 ? gate.lockedText : gate.openText;
            e.passage.gate = gate;
            hub.behaviours.Add(gate);

            bool round = z.castleStyle && z.roundCastle;
            e.mountainCentre = Mountains(root, z.zoneKey + "_" + key, e, round ? 4.4f : 0f, mouth.y + 7.5f, 1f, tm[5], tm[6]);
            hub.gates[key] = new HubGate { landmark = l, tunnel = e, gate = gate, front = e.front };
        }

        /// <summary>Tunnel and mountain materials: stone, floor, dark, lantern glow, iron, rock, snow.</summary>
        private static Material[] TunnelMats(ZoneDefinition z)
        {
            Texture2D wallTex = TileTexture(TerrainTexDir + "/StoneWall.png");
            return new[]
            {
                PsxTexMat("TunnelStone", Color.Lerp(z.buildingColor, new Color(0.45f, 0.42f, 0.44f), 0.6f), wallTex, 2.2f),
                PsxTexMat("TunnelFloor", new Color(0.5f, 0.45f, 0.4f), TileTexture(TerrainTexDir + "/Cobble.png"), 2.4f),
                PMat("TunnelDark", new Color(0.04f, 0.04f, 0.06f), 1f),
                PMat("TunnelLantern", new Color(1f, 0.75f, 0.4f), 1f, new Color(1f, 0.6f, 0.2f)),
                PMat("RailingIron", new Color(0.17f, 0.16f, 0.21f), 1f),
                PsxTexMat("MountainRock", new Color(0.56f, 0.5f, 0.52f), wallTex, 6f),
                PMat("MountainSnow", new Color(0.95f, 0.96f, 1f), 1f, new Color(0.12f, 0.12f, 0.16f)),
            };
        }

        // ================================================================== contents (after the quest system exists)

        /// <summary>The hub's NPCs, quest points, practice ring, duelling circles and orbs, plus the gates' quest locks.</summary>
        private static void BuildHubContents(HubLayout hub, Transform systems, QuestManager qm, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, List<BattleClient> encounters, WorldBuild wb)
        {
            foreach (var gate in hub.gates.Values) gate.gate.questManager = qm;
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
            BuildDuelCircles(hub, systems, cardDb, itemDb, playerState, qm, encounters, wb);
            BuildHubOrbs(hub, playerState, wb);
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
