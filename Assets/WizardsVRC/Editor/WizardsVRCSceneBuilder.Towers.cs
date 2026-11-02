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
    /// Boss towers, like Wizard101's Golem Tower: every street with a boss gets a tower. Its way in is a four-pad sigil
    /// out in the street (DungeonSigil: the party goes in together and has the tower to itself). Inside, the party climbs
    /// floor by floor: each floor is a round stone chamber with a duel ring and a wave of the street's monsters, whose
    /// leader stands across the ring and taunts you as you arrive (TowerFloor). Step into the ring to fight; win and you
    /// go up to the next floor. The top floor is the street's boss; beat it and you come out on the roof, where a veil
    /// takes you home. Losing (or leaving by a veil) puts you back by the sigil.
    ///
    /// Towers are built far from everything (TowerOrigin), one beside the other, each its own culled area.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 TowerOrigin = new Vector3(-3200f, 700f, 3200f);
        private const int TowerWaves = 3;                     // floors of waves before the boss floor
        private const float TowerFloorH = 14f, TowerRoomR = 15f, TowerWallH = 10f;
        private static int _towerCount;
        private static readonly List<string> TowerNames = new List<string>();

        /// <summary>Clears what the last build left in the tower statics (called at the start of every build).</summary>
        private static void ResetTowers() { _towerCount = 0; _secretCount = 0; TowerNames.Clear(); }

        /// <summary>The street's boss (the first boss in its encounters' rosters) and its other monsters, weakest first.</summary>
        private static int TowerMonsters(ZoneDefinition z, List<int> regulars, List<int> elites)
        {
            int boss = 0;
            var seen = new HashSet<int>();
            foreach (var e in z.encounters)
            {
                if (e == null || e.roster == null) continue;
                foreach (int id in e.roster)
                {
                    if (id <= 0 || !seen.Add(id)) continue;
                    MonsterEntry m = _monsterCatalog != null ? _monsterCatalog.Find(id) : null;
                    if (m == null) continue;
                    if (m.isBoss) { if (boss == 0) boss = id; }
                    else if (m.isElite) elites.Add(id);
                    else regulars.Add(id);
                }
            }
            System.Comparison<int> byLevel = (a, b) => _monsterCatalog.Find(a).level.CompareTo(_monsterCatalog.Find(b).level);
            regulars.Sort(byLevel);
            elites.Sort(byLevel);
            return boss;
        }

        /// <summary>
        /// A spot for the tower's sigil: open, level ground (about 7 m across), clear of NPCs, points, the start and the duel
        /// rings, as far along the street from the start as possible (where the boss is). (-1, -1) when nothing fits.
        /// </summary>
        private static Vector2Int TowerSigilCell(ZoneGrid g, Vector2Int start, List<Vector2Int> ringCells)
        {
            var dist = new Dictionary<Vector2Int, int> { { start, 0 } };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var d in ZoneGrid.Dirs)
                {
                    var n = p + d;
                    if (dist.ContainsKey(n) || !g.CanStep(p, n)) continue;
                    dist[n] = dist[p] + 1;
                    q.Enqueue(n);
                }
            }
            Vector2Int best = new Vector2Int(-1, -1), fallback = best, roomy = best;
            int bestD = -1, fallbackD = -1, roomyD = -1;
            foreach (var kv in dist)
            {
                var c = kv.Key;
                if (kv.Value < 3 || !g.RingFits(c, 3.6f)) continue;
                if (_dungeonCells != null && _dungeonCells.Contains(c)) continue;
                bool clear = true;
                for (int yy = c.y - 1; yy <= c.y + 1 && clear; yy++)
                    for (int xx = c.x - 1; xx <= c.x + 1; xx++)
                    {
                        char ch = g.At(xx, yy);
                        if (ch == 'S' || g.IsCustom(ch)) { clear = false; break; }
                    }
                if (!clear) continue;
                bool ringFree = true;
                Vector3 pc = g.Local(c);
                foreach (var r in ringCells)
                {
                    Vector3 d = g.Local(r) - pc;
                    d.y = 0f;
                    if (d.magnitude < DuelCircleRadius + 1.5f) { ringFree = false; break; }
                }
                if (ringFree && kv.Value > bestD) { bestD = kv.Value; best = c; }
                if (ringFree && kv.Value > roomyD && g.RingFits(c, 8.5f)) { roomyD = kv.Value; roomy = c; }
                if (kv.Value > fallbackD) { fallbackD = kv.Value; fallback = c; }
            }
            // Somewhere with room for the tower beside the sigil if there is one.
            return roomy.x >= 0 ? roomy : best.x >= 0 ? best : fallback;
        }

        /// <summary>
        /// The street's boss leaves the street for its tower: the encounters whose roster has the tower's boss (outside a
        /// dungeon; a dungeon's boss stays in its dungeon) aren't built, and the first one's ring is where the tower stands.
        /// Returns that cell ((-1, -1) when the boss has no street ring) and fills 'skip'.
        /// </summary>
        private static Vector2Int TowerSite(ZoneGrid g, ZoneDefinition z, HashSet<ZoneEncounter> skip)
        {
            var regulars = new List<int>();
            var elites = new List<int>();
            int boss = TowerMonsters(z, regulars, elites);
            Vector2Int site = new Vector2Int(-1, -1);
            if (boss == 0) return site;
            foreach (var e in z.encounters)
            {
                if (e == null || e.roster == null || !e.roster.Contains(boss) || !ZoneGrid.IsRingDigit(e.Symbol)) continue;
                var home = g.Find(e.Symbol);
                if (home.Count == 0 || (_dungeonCells != null && _dungeonCells.Contains(home[0]))) continue;
                skip.Add(e);
                if (site.x < 0) site = home[0];
            }
            return site;
        }

        private static void BuildZoneTower(Transform zoneRoot, ZoneGrid g, ZoneDefinition z, Vector2Int start, List<Vector2Int> ringCells,
            Transform systems, CardDatabase cardDb, ItemDatabase itemDb, PlayerState playerState, QuestManager qm, List<BattleClient> encounters, WorldBuild wb,
            Vector2Int site)
        {
            var regulars = new List<int>();
            var elites = new List<int>();
            int boss = TowerMonsters(z, regulars, elites);
            if (boss == 0) return;
            if (regulars.Count == 0) regulars.AddRange(elites);
            if (regulars.Count == 0) regulars.Add(boss);
            Vector2Int cell = site.x >= 0 ? site : TowerSigilCell(g, start, ringCells);
            if (cell.x < 0)
            {
                Debug.LogWarning("[WizardsVRC World] " + z.displayName + ": no open level spot (about 7 m across) for the boss tower's sigil; no tower built.");
                return;
            }
            int index = _towerCount++;
            MonsterEntry bossEntry = _monsterCatalog.Find(boss);
            string bossName = bossEntry != null ? bossEntry.name : "the boss";
            string towerName = bossName.Split(' ')[0] + "'s Tower";
            TowerNames.Add(towerName);
            int school = cardDb.MonsterSchool(boss);
            Color col = school >= 0 && school < SchoolColors.Length ? SchoolColors[school] : new Color(0.6f, 0.5f, 0.8f);

            // ---- rosters: three waves, stronger each floor, then the boss with its strongest followers. The first monster
            // of each is the wave's leader (it speaks, and takes the big seat). Waves scale with the party: solo, two at a
            // time; each extra wizard, one more.
            int n = regulars.Count;
            int lo = regulars[0], mid = regulars[n / 2], hi = regulars[n - 1];
            int elite = elites.Count > 0 ? elites[elites.Count - 1] : hi;
            var rosters = new List<int[]>
            {
                new[] { mid, lo, lo, mid },
                new[] { hi, mid, lo, hi },
                new[] { elite, hi, mid, hi },
                new[] { boss, elite, hi, mid },
            };
            // No wave bigger than the street's biggest encounter (Lantern Row: two).
            int most = 1;
            foreach (var e in z.encounters) if (e != null && e.roster != null) most = Mathf.Max(most, e.roster.Count);
            most = Mathf.Clamp(most, 2, BattleManager.SIDE);
            for (int r = 0; r < rosters.Count; r++)
                if (rosters[r].Length > most) { var cut = new int[most]; System.Array.Copy(rosters[r], cut, most); rosters[r] = cut; }
            var lines = new[]
            {
                "Another little wizard comes knocking at " + towerName + "? Turn back while you still can!",
                "You got past the guards below? No matter. " + bossName + " will hear of this!",
                bossName + " waits at the top. You will never reach it!",
                "So, you climbed my tower. " + z.displayName + " is mine, wizard. Come, let us end this!",
            };

            // ---- the tower, far away, its own area
            var troot = new GameObject("Tower_" + z.zoneKey).transform;
            troot.position = TowerOrigin + new Vector3(index * 260f, index * 110f, 0f);
            // Dark stone: the rooms are lit by their torches and braziers, not the sky.
            var stone = PsxTexMat("TowerStoneDark_" + z.zoneKey, Color.Lerp(new Color(0.34f, 0.32f, 0.38f), col, 0.1f), TileTexture(TerrainTexDir + "/StoneWall.png"), 2.4f);
            var floorMat = PsxTexMat("TowerFloorDark_" + z.zoneKey, new Color(0.36f, 0.33f, 0.32f), TileTexture(TerrainTexDir + "/Flagstone.png"), 2.5f);
            var ceiling = PsxTexMat("TowerCeilingDark", new Color(0.24f, 0.16f, 0.11f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2f);
            // The outside (the street tower and the roof) keeps the lighter stone.
            var outerStone = PsxTexMat("TowerStone_" + z.zoneKey, Color.Lerp(new Color(0.62f, 0.6f, 0.66f), col, 0.12f), TileTexture(TerrainTexDir + "/StoneWall.png"), 2.4f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var banner = PMat("TowerBanner_" + SchoolLabels[Mathf.Clamp(school, 0, 6)], Color.Lerp(col, Color.black, 0.25f), 1f);
            var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
            var veilMat = PMat("TowerVeil", new Color(0.65f, 0.4f, 1f), 1f, new Color(0.65f, 0.4f, 1f) * 1.2f);
            var slit = PMat("TowerWindowNight", new Color(0.12f, 0.14f, 0.3f), 1f, new Color(0.1f, 0.12f, 0.32f));

            int floors = TowerWaves + 1;
            var arrivals = new Transform[floors + 1];
            for (int k = 0; k <= floors; k++)
            {
                arrivals[k] = Child(troot, k < floors ? "Arrival" + k : "Summit", new Vector3(0, k * TowerFloorH + 0.1f, -(TowerRoomR - 3f)));
                arrivals[k].localRotation = Quaternion.identity;   // facing the ring (+Z)
            }

            // ---- the tower itself, standing where the boss used to be, and the sigil in front of it (the way in)
            Vector3 sc = g.Local(cell);
            Vector3 toStart = g.Local(start) - sc;
            toStart.y = 0f;
            Vector3 face = toStart.sqrMagnitude > 0.01f ? -toStart.normalized : Vector3.forward;   // a reader coming from the start looks this way
            Vector3 towerPos;
            float towerR;
            if (site.x >= 0)
            {
                // The boss's old ring plaza (3 x 3 cells open): the tower at its far side, the sigil at its near side.
                towerPos = sc + face * (g.cell * 0.7f);
                towerR = Mathf.Min(3.6f, g.cell * 0.45f);
                sc -= face * (g.cell * 0.5f);
            }
            else
            {
                towerR = g.RingFits(cell, 8.5f) ? 2.8f : 0f;
                towerPos = sc + face * 7f;
            }
            if (towerR > 0f) StreetTower(zoneRoot, towerPos, face, towerR, col, outerStone, gold, banner, towerName, IsDesert(z));
            DungeonSigil sigil = TowerSigil(zoneRoot, systems, z, sc, face, towerName, col, outerStone, gold, wb);
            // Quest arrows for the boss (it no longer walks the street) lead to the tower's sigil.
            if (!wb.monsterTargets.ContainsKey(boss)) wb.monsterTargets[boss] = sigil.outside;
            sigil.start = arrivals[0];
            Vector3 top = troot.position + new Vector3(0, floors * TowerFloorH, 0);
            sigil.boundsMin = troot.position + new Vector3(-TowerRoomR - 12f, -10f, -TowerRoomR - 12f);
            sigil.boundsMax = top + new Vector3(TowerRoomR + 12f, 25f, TowerRoomR + 12f);

            // ---- floors
            for (int k = 0; k < floors; k++)
            {
                bool isTop = k == floors - 1;
                Vector3 c = new Vector3(0, k * TowerFloorH, 0);
                var room = Child(troot, "Floor" + k, c);
                TowerChamber(room, isTop ? TowerRoomR + 1.5f : TowerRoomR, stone, floorMat, ceiling, gold, banner, flame, slit, isTop);

                string name = z.zoneKey + "_Tower" + k;
                // Like any street encounter: the floor's monsters walk the far side of the chamber, and running into one
                // starts the fight in the ring (the others join as they come over, a round later, if there's a seat).
                var client = MakeEncounter(systems, name, cardDb, itemDb, playerState, arrivals[k], rosters[k], true, encounters);
                var m = client.manager;
                m.scaleRosterWithParty = true;
                Transform spot = Child(troot, "TowerRing" + k, c);
                m.ringSpots = new[] { spot };
                m.ringSpot = 0;
                m.groupLanes = new[] { m };
                m.peers = new[] { m };
                m.respawnSeconds = 20;
                int walkers = isTop ? 1 : rosters[k].Length;   // the boss walks alone (its followers come with it by party size)
                m.wandererMonsters = new int[walkers];
                for (int w = 0; w < walkers; w++) m.wandererMonsters[w] = rosters[k][w % rosters[k].Length];
                client.questManager = qm;
                BuildEncounter(troot, spot.localPosition, name, client, 0);
                Transform encRoot = troot.Find("Encounter" + name);
                client.encounterRoot = encRoot;
                client.hubReturn = arrivals[k];             // fled: still on this floor, try again
                client.defeatReturn = sigil.outside;        // beaten: back out by the sigil
                client.victoryReturn = arrivals[k + 1];     // won: up to the next floor (the roof after the boss)
                client.padTitle = (isTop ? "The Top: " + bossName : "Floor " + (k + 1) + " of " + floors) + "\nRun into a monster to fight";
                client.padLabel = WorldText(room, "RingSign", new Vector3(0, 5.2f, -2.5f), Quaternion.identity, new Vector2(1100, 220), 0.0045f, client.padTitle, 44);
                DressSign(client.padLabel, 1000f, 210f, false);

                // Their beat: back and forth along the far (north) side of the chamber, well away from where you arrive.
                var patrol = Child(troot, "TowerPatrol" + k, c);
                float pr = DuelCircleRadius + 1.4f;
                float[] beat = { 15f, 50f, 90f, 130f, 165f };
                var path = new Transform[beat.Length];
                float length = 0f;
                for (int i = 0; i < beat.Length; i++)
                {
                    path[i] = Child(patrol, "Waypoint" + i, TPolar(Vector3.zero, pr, beat[i], 0.05f));
                    if (i > 0) length += Vector3.Distance(path[i - 1].localPosition, path[i].localPosition);
                }
                for (int w = 0; w < walkers; w++)
                {
                    var wd = MakeWanderer(patrol, client, cardDb, m.wandererMonsters[w], "Wanderer" + w);
                    wd.wandererIndex = w;
                    wd.contactRadius = DuelCircleRadius + 8f;
                    wd.path = path;
                    wd.loop = false;
                    wd.speed = isTop ? 0.7f : 1f;
                    wd.phaseOffset = length * 2f * w / walkers;
                    wd.laneManagers = new[] { m };
                    wd.laneClients = new[] { client };
                    wb.behaviours.Add(wd);
                }
                if (!wb.monsterTargets.ContainsKey(rosters[k][0])) wb.monsterTargets[rosters[k][0]] = path[2];

                // The floor: its leader across the ring and the greeting.
                var floor = UdonSharpUndo.AddComponent<TowerFloor>(room.gameObject);
                floor.client = client;
                floor.centre = room;
                floor.radius = TowerRoomR + 1f;
                floor.speaker = MonsterLabel(rosters[k][0]);
                floor.line = lines[k];
                floor.floorTitle = isTop ? "Top floor" : "Floor " + (k + 1);
                floor.isTop = isTop;
                floor.towerName = towerName;
                floor.clearBits = 1 | (1 << (1 + Mathf.Min(index, 28)));
                floor.clearShards = 5;
                // The leader is the first of the walkers (no stand-in body: the monsters are right there).
                TowerDialog(room, floor, cardDb.MonsterSchool(rosters[k][0]));
                client.towerFloor = floor;
                wb.behaviours.Add(floor);

                // A veil by the arrival to give up and go home.
                TowerExit(room, new Vector3(-6.5f, 0, -(TowerRoomR - 2.2f)), Quaternion.LookRotation(new Vector3(0.6f, 0, 1f)), sigil, veilMat, stone, gold, towerName, wb);
            }

            // ---- the roof: battlements round an open top, a chest, and a veil home; a sea of cloud far below
            var roof = Child(troot, "Roof", new Vector3(0, floors * TowerFloorH, 0));
            var mb = new MeshBuilder();
            DiscSlab(mb, TowerRoomR + 2.3f, -0.4f, 0f, 48);   // over the top floor's wider wall too
            TavernMesh(roof, "RoofFloor", mb, PsxTexMat("TowerFloor_" + z.zoneKey, new Color(0.6f, 0.56f, 0.52f), TileTexture(TerrainTexDir + "/Flagstone.png"), 2.5f), true);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                if (i % 2 == 0)
                    Block(roof, "Merlon", TPolar(Vector3.zero, TowerRoomR + 0.4f, a, 0.9f), new Vector3(2.2f, 1.8f, 0.8f), outerStone, TFacing(a));
                else
                    Block(roof, "Parapet", TPolar(Vector3.zero, TowerRoomR + 0.4f, a, 0.5f), new Vector3(2.2f, 1f, 0.8f), outerStone, TFacing(a));
            }
            Block(roof, "Chest", new Vector3(0, 0.45f, 4f), new Vector3(1.6f, 0.9f, 1f), PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f));
            Block(roof, "ChestLid", new Vector3(0, 1.0f, 4f), new Vector3(1.7f, 0.25f, 1.1f), gold);
            Shape(roof, "ChestGlow", GemMesh(), new Vector3(0, 1.7f, 4f), new Vector3(0.6f, 0.9f, 0.6f), PMat("Crystal_Starfall", new Color(0.35f, 0.6f, 1f), 1f, new Color(0.25f, 0.45f, 0.9f)), false, Quaternion.identity);
            PointLight(roof, new Vector3(0, 3f, 0), new Color(1f, 0.9f, 0.7f), 26f, 1.2f);
            var win = WorldText(roof, "RoofSign", new Vector3(0, 3.2f, 7f), Quaternion.identity, new Vector2(1200, 220), 0.0045f, towerName + "\nYou reached the top!", 48);
            DressSign(win, 1100f, 210f);
            TowerExit(roof, new Vector3(0, 0, TowerRoomR - 3f), Quaternion.identity, sigil, veilMat, stone, gold, towerName, wb);
            var clouds = new MeshBuilder();
            SectorPrism(clouds, Vector3.zero, 0f, 220f, 0f, 360f, -0.1f, 0f, 32);
            TavernMesh(troot, "CloudSea", clouds, PMat("CloudSea", new Color(0.88f, 0.9f, 1f), 6f, new Color(0.25f, 0.27f, 0.35f)), false);
            troot.Find("CloudSea").localPosition = new Vector3(0, -60f, 0);

            // ---- its own area for the culler
            wb.cullRoots.Add(troot.gameObject);
            wb.cullBounds.Add(new Bounds(troot.position + new Vector3(0, floors * TowerFloorH * 0.5f, 0),
                new Vector3(TowerRoomR * 2f + 40f, floors * TowerFloorH + 140f, TowerRoomR * 2f + 40f)));
            troot.gameObject.SetActive(false);
            Debug.Log("[WizardsVRC World] " + z.displayName + ": " + towerName + " (" + TowerWaves + " wave floors, then " + bossName + ").");
        }

        /// <summary>
        /// The boss's tower out in the street (scenery; the sigil in front of it is the way in): a round stone tower with
        /// bands, glowing window slits, a dark door facing the sigil, a banner in the boss's school colour, battlements and
        /// a pointed roof with a crystal turning over it. Desert streets get sandstone and a flat watchtower top.
        /// </summary>
        private static void StreetTower(Transform root, Vector3 p, Vector3 face, float r, Color col, Material stone, Material gold, Material banner,
            string towerName, bool desert)
        {
            var t = Child(root, "BossTower", p);
            t.localRotation = Quaternion.LookRotation(-face);   // its door (local +Z) faces the sigil and the street
            if (desert) stone = PsxTexMat("TowerSandstone", new Color(0.78f, 0.5f, 0.36f), TileTexture(TerrainTexDir + "/RedRock.png"), 2.6f);
            var roofMat = PMat("TowerRoof_" + ColorUtility.ToHtmlStringRGB(col), Color.Lerp(col, new Color(0.2f, 0.18f, 0.25f), 0.45f), 1f);
            var glow = PMat("TowerSlitGlow", new Color(1f, 0.7f, 0.35f), 1f, new Color(1f, 0.55f, 0.2f));
            const float h = 22f;
            Shape(t, "TowerBase", CylMesh(), new Vector3(0, 1f, 0), new Vector3(r * 2f + 1.2f, 1f, r * 2f + 1.2f), stone, true, Quaternion.identity);
            Shape(t, "TowerBody", CylMesh(), new Vector3(0, h / 2f, 0), new Vector3(r * 2f, h / 2f, r * 2f), stone, true, Quaternion.identity);
            foreach (float y in new[] { 7.5f, 14.5f })
                Shape(t, "TowerBand", CylMesh(), new Vector3(0, y, 0), new Vector3(r * 2f + 0.4f, 0.25f, r * 2f + 0.4f), gold, false, Quaternion.identity);
            // Window slits round it, lit from inside.
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f + k * 30f + 30f;
                    if (AngleGap(a, 90f) < 25f && k == 0) continue;   // over the door
                    Block(t, "TowerSlit", TPolar(Vector3.zero, r * 0.99f, a, 4.5f + k * 7f), new Vector3(0.45f, 1.6f, 0.12f), glow, TFacing(a));
                }
            // The door, its arch and the banner over it.
            Vector3 d = TPolar(Vector3.zero, r * 0.97f, 90f, 0f);
            Block(t, "TowerDoor", d + new Vector3(0, 1.9f, 0.05f), new Vector3(2.2f, 3.8f, 0.2f), PMat("TunnelDark", new Color(0.05f, 0.04f, 0.08f), 1f), TFacing(90f));
            Block(t, "TowerDoorArch", d + new Vector3(0, 4f, 0.15f), new Vector3(3f, 0.5f, 0.4f), gold, TFacing(90f));
            Block(t, "TowerBanner", TPolar(Vector3.zero, r + 0.1f, 90f, 9.5f), new Vector3(1.8f, 4f, 0.08f), banner, TFacing(90f));
            // The top: battlements, then a pointed roof (or a flat lookout in the desert) and the school's crystal.
            Shape(t, "TowerCrown", CylMesh(), new Vector3(0, h + 0.3f, 0), new Vector3(r * 2f + 1.2f, 0.3f, r * 2f + 1.2f), stone, false, Quaternion.identity);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                if (i % 2 == 0) Block(t, "TowerMerlon", TPolar(Vector3.zero, r + 0.35f, a, h + 1.2f), new Vector3(1.3f, 1.4f, 0.6f), stone, TFacing(a));
            }
            if (desert) Shape(t, "TowerLookout", CylMesh(), new Vector3(0, h + 2.4f, 0), new Vector3(r * 1.4f, 1.8f, r * 1.4f), stone, false, Quaternion.identity);
            else Shape(t, "TowerRoof", ConeMesh(), new Vector3(0, h + 4.5f, 0), new Vector3(r * 2.3f, 8f, r * 2.3f), roofMat, false, Quaternion.identity);
            Shape(t, "TowerCrystal", GemMesh(), new Vector3(0, h + (desert ? 6.5f : 10.5f), 0), new Vector3(1.2f, 2f, 1.2f), PMat("Crystal_" + ColorUtility.ToHtmlStringRGB(col), col, 1f, col * 0.6f), false, Quaternion.identity);
            PointLight(t, TPolar(Vector3.zero, r + 2f, 90f, 4f), new Color(1f, 0.7f, 0.4f), 10f, 1.2f);
            var sign = WorldText(t, "TowerName", TPolar(Vector3.zero, r + 0.25f, 90f, 6.2f), Quaternion.LookRotation(-TDir(90f)), new Vector2(1200, 180), 0.005f, towerName, 52);
            DressSign(sign, 1100f, 170f);
        }

        private static string MonsterLabel(int id)
        {
            MonsterEntry m = _monsterCatalog != null ? _monsterCatalog.Find(id) : null;
            return m != null ? m.name : "Monster";
        }

        /// <summary>
        /// A solid round slab (floor or ceiling) of radius r between y0 and y1: its top, its underside and its edge, so it
        /// reads from above and below (the room under it sees its underside as the ceiling).
        /// </summary>
        private static void DiscSlab(MeshBuilder mb, float r, float y0, float y1, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                float aa = i * 360f / segs, ab = (i + 1) * 360f / segs;
                Vector3 o0 = TPolar(Vector3.zero, r, aa, 0f), o1 = TPolar(Vector3.zero, r, ab, 0f);
                Vector3 up = new Vector3(0, y1, 0), dn = new Vector3(0, y0, 0);
                mb.Quad(up, o0 + up, o1 + up, up, Vector3.up);
                mb.Quad(dn, o1 + dn, o0 + dn, dn, Vector3.down);
                mb.Quad(o0 + dn, o1 + dn, o1 + up, o0 + up, TDir((aa + ab) / 2f));
            }
        }

        /// <summary>
        /// A round stone chamber, closed all round: a solid floor, the wall up to the next floor's slab (its underside is
        /// this room's ceiling, crossed by dark beams), window slits of night, pillars, banners, and the warm light of
        /// torches on every pillar and four braziers round the ring; dust drifting in the torchlight. Dim and moody.
        /// </summary>
        private static void TowerChamber(Transform room, float r, Material stone, Material floorMat, Material ceiling, Material gold, Material banner,
            Material flame, Material slit, bool isTop)
        {
            float h = TowerFloorH - 0.4f;   // up to the underside of the next floor (or the roof)
            var mb = new MeshBuilder();
            DiscSlab(mb, r + 0.8f, -0.4f, 0f, 48);
            TavernMesh(room, "ChamberFloor", mb, floorMat, true);
            mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, r, r + 0.8f, 0f, 360f, 0f, h, 48);
            TavernMesh(room, "ChamberWall", mb, stone, true);
            // The ceiling: dark planks just under the next slab, and beams across it.
            mb = new MeshBuilder();
            DiscSlab(mb, r + 0.1f, h - 0.15f, h - 0.05f, 48);
            TavernMesh(room, "ChamberCeiling", mb, ceiling, false);
            for (int k = 0; k < 4; k++)
                Block(room, "CeilingBeam", new Vector3(0, h - 0.45f, 0), new Vector3(0.5f, 0.5f, r * 2f), ceiling, Quaternion.Euler(0, k * 45f, 0));
            var ironMat = PMat("TowerIron", new Color(0.12f, 0.11f, 0.12f), 1f);
            for (int i = 0; i < 8; i++)
            {
                float a = 22.5f + i * 45f;
                Shape(room, "Pillar", CylMesh(), TPolar(Vector3.zero, r - 0.7f, a, h / 2f), new Vector3(1.1f, h / 2f, 1.1f), stone, true, Quaternion.identity);
                Block(room, "PillarCap", TPolar(Vector3.zero, r - 0.7f, a, TowerWallH - 0.3f), new Vector3(1.5f, 0.5f, 1.5f), gold, TFacing(a));
                // A torch on every pillar (every other one throws light: plenty of pools of warm light in the dark).
                Block(room, "Sconce", TPolar(Vector3.zero, r - 1.35f, a, 3.4f), new Vector3(0.3f, 0.5f, 0.4f), gold, TFacing(a));
                Shape(room, "TorchFlame", ConeMesh(), TPolar(Vector3.zero, r - 1.5f, a, 3.95f), new Vector3(0.35f, 0.6f, 0.35f), flame, false, Quaternion.identity);
                if (i % 2 == 0) PointLight(room, TPolar(Vector3.zero, r - 2.4f, a, 4.2f), new Color(1f, 0.55f, 0.25f), 11f, 1.6f);
                // Between the pillars: a window slit onto the night, and a banner under every other one.
                float b = i * 45f;
                Block(room, "WindowSlit", TPolar(Vector3.zero, r - 0.02f, b, 8f), new Vector3(0.6f, 2.6f, 0.06f), slit, TFacing(b));
                if (i % 2 == 0)
                {
                    Block(room, "Banner", TPolar(Vector3.zero, r - 0.08f, b, 3.8f), new Vector3(1.5f, 3.4f, 0.06f), banner, TFacing(b));
                    Block(room, "BannerRod", TPolar(Vector3.zero, r - 0.15f, b, 5.6f), new Vector3(1.9f, 0.1f, 0.1f), gold, TFacing(b));
                }
            }
            // Four braziers round the ring, glowing up into the dark.
            for (int k = 0; k < 4; k++)
            {
                float a = 45f + k * 90f;
                Vector3 p = TPolar(Vector3.zero, DuelCircleRadius + 1.6f, a, 0f);
                Shape(room, "Brazier", CylMesh(), p + new Vector3(0, 0.5f, 0), new Vector3(1f, 0.5f, 1f), ironMat, true, Quaternion.identity);
                Shape(room, "BrazierCoals", CylMesh(), p + new Vector3(0, 1.02f, 0), new Vector3(0.85f, 0.04f, 0.85f), flame, false, Quaternion.identity);
                Shape(room, "BrazierFlame", ConeMesh(), p + new Vector3(0, 1.6f, 0), new Vector3(0.7f, 1.1f, 0.7f), flame, false, Quaternion.identity);
                if (k % 2 == 0) PointLight(room, p + new Vector3(0, 2.2f, 0), new Color(1f, 0.5f, 0.2f), 10f, 1.5f);
            }
            // The arch up (north): where the party goes on to once the floor is won.
            Vector3 n = TPolar(Vector3.zero, r - 0.3f, 90f, 0f);
            foreach (float s in new[] { -1f, 1f })
                Block(room, "ArchPost", n + new Vector3(s * 1.9f, 2.3f, 0), new Vector3(0.6f, 4.6f, 0.8f), stone);
            Block(room, "ArchLintel", n + new Vector3(0, 4.9f, 0), new Vector3(4.6f, 0.7f, 0.9f), gold);
            Block(room, "ArchDark", n + new Vector3(0, 2.3f, 0.2f), new Vector3(3.2f, 4.6f, 0.1f), PMat("TunnelDark", new Color(0.05f, 0.04f, 0.08f), 1f));
            // A dim cool fill from above, so the far side of the room isn't pitch black.
            PointLight(room, new Vector3(0, h - 2f, 0), new Color(0.55f, 0.5f, 0.75f), r * 1.5f, isTop ? 0.7f : 0.45f);
            Motes(room, "TowerDust", new Vector3(0, 3f, 0), new Vector3(r * 1.6f, 4f, r * 1.6f), new Color(1f, 0.75f, 0.45f), 6f, 6f, 10f,
                new Vector2(0.04f, 0.1f), new Vector3(0, 0.08f, 0), new Vector3(0.12f, 0.06f, 0.12f), 0f);
            if (isTop)
                Shape(room, "Throne", CylMesh(), TPolar(Vector3.zero, r - 2.6f, 90f, 1.2f), new Vector3(2.2f, 1.2f, 1.6f), gold, true, Quaternion.identity);
        }

        /// <summary>The leader's greeting: a carved plate with their portrait (school icon in a red, hostile rim), name and line.</summary>
        private static void TowerDialog(Transform room, TowerFloor floor, int school)
        {
            var go = new GameObject("Greeting", typeof(RectTransform));
            go.transform.SetParent(room, false);
            MakeWorldCanvas(go, new Vector2(1100, 300), new Vector3(0, 4.2f, DuelCircleRadius - 1f), 0.004f);
            var rt = (RectTransform)go.transform;
            UiImage(rt, "Plate", Vector2.zero, new Vector2(1100, 300), "PanelOrnate", Color.white, 3f).raycastTarget = false;
            UiImage(rt, "PortraitBack", new Vector2(-420, 0), new Vector2(200, 200), "PortraitBack", Color.white, 3f).raycastTarget = false;
            var icon = Label(rt, "PortraitIcon", new Vector2(-420, 0), new Vector2(200, 200), school >= 0 && school < 7 ? ((char)(0xE000 + school)).ToString() : "?", 110, TextAnchor.MiddleCenter);
            icon.color = school >= 0 && school < SchoolColors.Length ? SchoolColors[school] : Color.white;
            UiImage(rt, "PortraitRing", new Vector2(-420, 0), new Vector2(200, 200), "PortraitHostile", Color.white, 3f).raycastTarget = false;
            floor.dialogName = Label(rt, "Name", new Vector2(120, 95), new Vector2(780, 60), "", 40, TextAnchor.MiddleLeft);
            floor.dialogName.color = new Color(1f, 0.55f, 0.45f);
            floor.dialogLine = Label(rt, "Line", new Vector2(120, -25), new Vector2(780, 170), "", 34, TextAnchor.UpperLeft);
            floor.dialogLine.color = new Color(1f, 0.95f, 0.85f);
            ApplyCombatFont(rt);
            floor.dialogRoot = go;
            go.SetActive(false);
        }

        /// <summary>A glowing veil in a stone frame: click it to leave the tower (back out by the sigil).</summary>
        private static void TowerExit(Transform parent, Vector3 pos, Quaternion rot, DungeonSigil sigil, Material veilMat, Material stone, Material gold,
            string towerName, WorldBuild wb)
        {
            Transform door = Child(parent, "TowerExit", pos);
            door.localRotation = rot;
            Deco(door, PrimitiveType.Cube, "PostL", new Vector3(-1.3f, 1.6f, 0), new Vector3(0.4f, 3.2f, 0.4f), stone, Quaternion.identity, true);
            Deco(door, PrimitiveType.Cube, "PostR", new Vector3(1.3f, 1.6f, 0), new Vector3(0.4f, 3.2f, 0.4f), stone, Quaternion.identity, true);
            Deco(door, PrimitiveType.Cube, "Lintel", new Vector3(0, 3.4f, 0), new Vector3(3f, 0.4f, 0.5f), gold, Quaternion.identity, false);
            var veil = Deco(door, PrimitiveType.Cube, "ExitVeil", new Vector3(0, 1.6f, 0), new Vector3(2.2f, 3.2f, 0.1f), veilMat, Quaternion.identity, true);
            var relay = UdonSharpUndo.AddComponent<EventRelay>(veil);
            relay.target = sigil;
            relay.eventName = nameof(DungeonSigil.LeaveDungeon);
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(relay);
            if (backing != null) backing.interactText = "Leave " + towerName;
            wb.behaviours.Add(relay);
            var sign = WorldText(door, "ExitSign", new Vector3(0, 4.3f, -0.3f), Quaternion.identity, new Vector2(900, 150), 0.0035f, "Leave the tower", 44);
            DressSign(sign, 0f, 140f, false);
        }

        /// <summary>The four-pad sigil in the street with a rune obelisk in its middle and the tower's name over it.</summary>
        private static DungeonSigil TowerSigil(Transform root, Transform systems, ZoneDefinition z, Vector3 sc, Vector3 face, string towerName, Color col,
            Material stone, Material gold, WorldBuild wb)
        {
            Transform sigilRoot = Child(root, "TowerSigil", sc + new Vector3(0, RingLift, 0));
            Color tint = Color.Lerp(col, new Color(0.65f, 0.4f, 1f), 0.4f);
            FlatDecal(sigilRoot, "SigilRunes", Vector3.zero, 5.4f, CutoutMat("TowerSigilRunes_" + z.zoneKey, PixelTexture(TexturesDir + "/DuelCircleRunes.png"), tint, tint * 0.8f));
            FlatDecal(sigilRoot, "SigilCore", new Vector3(0, 0.015f, 0), 3.2f, CutoutMat("TowerSigilCore_" + z.zoneKey, PixelTexture(TexturesDir + "/DuelCircleSigil.png"), tint, tint * 0.6f));
            Texture2D padTex = PixelTexture(TexturesDir + "/DuelSeatRing.png");
            Material idle = CutoutMat("DungeonPadIdle", padTex, new Color(0.6f, 0.55f, 0.75f), new Color(0.2f, 0.15f, 0.3f));
            Material taken = CutoutMat("DungeonPadTaken", padTex, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.3f) * 1.4f);
            var pads = new Transform[DungeonSigil.PADS];
            var padRenderers = new Renderer[DungeonSigil.PADS];
            for (int i = 0; i < DungeonSigil.PADS; i++)
            {
                Vector3 dir = Quaternion.Euler(0, 45f + i * 90f, 0) * Vector3.forward;
                pads[i] = Child(sigilRoot, "Pad" + i, dir * 2.4f);
                padRenderers[i] = FlatDecal(pads[i], "PadRing", new Vector3(0, 0.03f, 0), 1.8f, idle).GetComponent<Renderer>();
            }
            // A little tower of rune stone in the middle, with a gem of the boss's school turning over it.
            Block(sigilRoot, "Obelisk", new Vector3(0, 1.4f, 0), new Vector3(0.9f, 2.8f, 0.9f), stone);
            Block(sigilRoot, "ObeliskCap", new Vector3(0, 2.9f, 0), new Vector3(1.2f, 0.3f, 1.2f), gold);
            Shape(sigilRoot, "Crystal", GemMesh(), new Vector3(0, 3.8f, 0), new Vector3(0.7f, 1.2f, 0.7f), PMat("Crystal_" + ColorUtility.ToHtmlStringRGB(col), col, 1f, col * 0.6f), false, Quaternion.identity);
            Text status = WorldText(root, "TowerStatus", sc + new Vector3(0, 5.6f, 0) - face * 0.2f, Quaternion.LookRotation(face), new Vector2(1500, 260), 0.0045f, towerName, 44);
            DressSign(status, 1350f, 240f);
            Transform outside = Child(root, "TowerOutside", sc - face * 4.5f + new Vector3(0, 0.1f, 0));
            outside.localRotation = Quaternion.LookRotation(face);

            var holder = Child(systems, "Tower_" + z.zoneKey, Vector3.zero);
            var sigil = UdonSharpUndo.AddComponent<DungeonSigil>(holder.gameObject);
            sigil.dungeonName = towerName;
            sigil.pads = pads;
            sigil.padRenderers = padRenderers;
            sigil.padIdle = idle;
            sigil.padTaken = taken;
            sigil.statusLabel = status;
            sigil.outside = outside;
            wb.behaviours.Add(sigil);
            return sigil;
        }
    }
}
