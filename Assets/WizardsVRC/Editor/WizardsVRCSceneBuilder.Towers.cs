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
            Vector2Int best = new Vector2Int(-1, -1), fallback = best;
            int bestD = -1, fallbackD = -1;
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
                if (kv.Value > fallbackD) { fallbackD = kv.Value; fallback = c; }
            }
            return best.x >= 0 ? best : fallback;
        }

        private static void BuildZoneTower(Transform zoneRoot, ZoneGrid g, ZoneDefinition z, Vector2Int start, List<Vector2Int> ringCells,
            Transform systems, CardDatabase cardDb, ItemDatabase itemDb, PlayerState playerState, QuestManager qm, List<BattleClient> encounters, WorldBuild wb)
        {
            var regulars = new List<int>();
            var elites = new List<int>();
            int boss = TowerMonsters(z, regulars, elites);
            if (boss == 0) return;
            if (regulars.Count == 0) regulars.AddRange(elites);
            if (regulars.Count == 0) regulars.Add(boss);
            Vector2Int cell = TowerSigilCell(g, start, ringCells);
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
            var stone = PsxTexMat("TowerStone_" + z.zoneKey, Color.Lerp(new Color(0.62f, 0.6f, 0.66f), col, 0.12f), TileTexture(TerrainTexDir + "/StoneWall.png"), 2.4f);
            var floorMat = PsxTexMat("TowerFloor_" + z.zoneKey, new Color(0.6f, 0.56f, 0.52f), TileTexture(TerrainTexDir + "/Flagstone.png"), 2.5f);
            var ceiling = PsxTexMat("TowerCeiling", new Color(0.45f, 0.3f, 0.2f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var banner = PMat("TowerBanner_" + SchoolLabels[Mathf.Clamp(school, 0, 6)], Color.Lerp(col, Color.black, 0.25f), 1f);
            var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
            var veilMat = PMat("TowerVeil", new Color(0.65f, 0.4f, 1f), 1f, new Color(0.65f, 0.4f, 1f) * 1.2f);
            var slit = PMat("TowerWindow", new Color(0.55f, 0.75f, 1f), 1f, new Color(0.45f, 0.6f, 0.9f));

            int floors = TowerWaves + 1;
            var arrivals = new Transform[floors + 1];
            for (int k = 0; k <= floors; k++)
            {
                arrivals[k] = Child(troot, k < floors ? "Arrival" + k : "Summit", new Vector3(0, k * TowerFloorH + 0.1f, -(TowerRoomR - 3f)));
                arrivals[k].localRotation = Quaternion.identity;   // facing the ring (+Z)
            }

            // ---- the sigil in the street (the way in) and its referee
            Vector3 sc = g.Local(cell);
            Vector3 toStart = g.Local(start) - sc;
            toStart.y = 0f;
            Vector3 face = toStart.sqrMagnitude > 0.01f ? -toStart.normalized : Vector3.forward;   // a reader coming from the start looks this way
            DungeonSigil sigil = TowerSigil(zoneRoot, systems, z, sc, face, towerName, col, stone, gold, wb);
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
                var client = MakeEncounter(systems, name, cardDb, itemDb, playerState, arrivals[k], rosters[k], false, encounters);
                var m = client.manager;
                m.scaleRosterWithParty = true;
                m.queueSeconds = 6;
                Transform spot = Child(troot, "TowerRing" + k, c);
                m.ringSpots = new[] { spot };
                client.questManager = qm;
                BuildEncounter(troot, spot.localPosition, name, client, 0);
                Transform encRoot = troot.Find("Encounter" + name);
                client.encounterRoot = encRoot;
                client.hubReturn = arrivals[k];             // fled: still on this floor, try again
                client.defeatReturn = sigil.outside;        // beaten: back out by the sigil
                client.victoryReturn = arrivals[k + 1];     // won: up to the next floor (the roof after the boss)
                client.padTitle = (isTop ? "The Top: " + bossName : "Floor " + (k + 1) + " of " + floors) + "\nStep into the ring to fight";
                client.padLabel = WorldText(room, "RingSign", new Vector3(0, 5.2f, -2.5f), Quaternion.identity, new Vector2(1100, 220), 0.0045f, client.padTitle, 44);
                DressSign(client.padLabel, 1000f, 210f, false);

                // Step into the ring to start the countdown; everyone inside when it ends fights.
                var zone = Child(troot, "TowerZone" + k, c + new Vector3(0, 1.5f, 0));
                var box = zone.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(DuelCircleRadius * 1.6f, 3f, DuelCircleRadius * 1.6f);
                var pad = UdonSharpUndo.AddComponent<BattleQueuePad>(zone.gameObject);
                pad.client = client;
                pad.keepQueued = true;
                pad.zoneRadius = DuelCircleRadius * 1.1f;
                wb.behaviours.Add(pad);

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
                var stand = Child(room, "Leader", new Vector3(0, 0.05f, DuelCircleRadius + 2.2f));
                stand.localRotation = Quaternion.LookRotation(Vector3.back);
                floor.idleBody = MonsterBody(stand, cardDb, rosters[k][0], "LeaderBody");
                TowerDialog(room, floor, cardDb.MonsterSchool(rosters[k][0]));
                client.towerFloor = floor;
                wb.behaviours.Add(floor);

                // A veil by the arrival to give up and go home.
                TowerExit(room, new Vector3(-6.5f, 0, -(TowerRoomR - 2.2f)), Quaternion.LookRotation(new Vector3(0.6f, 0, 1f)), sigil, veilMat, stone, gold, towerName, wb);
            }

            // ---- the roof: battlements round an open top, a chest, and a veil home; a sea of cloud far below
            var roof = Child(troot, "Roof", new Vector3(0, floors * TowerFloorH, 0));
            var mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, 0f, TowerRoomR + 0.8f, 0f, 360f, -0.4f, 0f, 48);
            TavernMesh(roof, "RoofFloor", mb, floorMat, true);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                if (i % 2 == 0)
                    Block(roof, "Merlon", TPolar(Vector3.zero, TowerRoomR + 0.4f, a, 0.9f), new Vector3(2.2f, 1.8f, 0.8f), stone, TFacing(a));
                else
                    Block(roof, "Parapet", TPolar(Vector3.zero, TowerRoomR + 0.4f, a, 0.5f), new Vector3(2.2f, 1f, 0.8f), stone, TFacing(a));
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

        private static string MonsterLabel(int id)
        {
            MonsterEntry m = _monsterCatalog != null ? _monsterCatalog.Find(id) : null;
            return m != null ? m.name : "Monster";
        }

        /// <summary>A round stone chamber: floor, wall with window slits, pillars, banners, torches, a ceiling and an arch up.</summary>
        private static void TowerChamber(Transform room, float r, Material stone, Material floorMat, Material ceiling, Material gold, Material banner,
            Material flame, Material slit, bool isTop)
        {
            var mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, 0f, r + 0.8f, 0f, 360f, -0.4f, 0f, 48);
            TavernMesh(room, "ChamberFloor", mb, floorMat, true);
            mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, r, r + 0.8f, 0f, 360f, 0f, TowerWallH, 48);
            TavernMesh(room, "ChamberWall", mb, stone, true);
            mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, 0f, r + 0.8f, 0f, 360f, TowerWallH, TowerWallH + 0.4f, 48);
            TavernMesh(room, "ChamberCeiling", mb, ceiling, false);
            for (int i = 0; i < 8; i++)
            {
                float a = 22.5f + i * 45f;
                Shape(room, "Pillar", CylMesh(), TPolar(Vector3.zero, r - 0.7f, a, TowerWallH / 2f), new Vector3(1.1f, TowerWallH / 2f, 1.1f), stone, true, Quaternion.identity);
                Block(room, "PillarCap", TPolar(Vector3.zero, r - 0.7f, a, TowerWallH - 0.3f), new Vector3(1.5f, 0.5f, 1.5f), gold, TFacing(a));
                // Between the pillars: a window slit of sky high up, and a banner or a torch below it.
                float b = i * 45f;
                Block(room, "WindowSlit", TPolar(Vector3.zero, r - 0.02f, b, 7f), new Vector3(0.6f, 2.4f, 0.06f), slit, TFacing(b));
                if (i % 2 == 0)
                {
                    Block(room, "Banner", TPolar(Vector3.zero, r - 0.08f, b, 3.6f), new Vector3(1.5f, 3.4f, 0.06f), banner, TFacing(b));
                    Block(room, "BannerRod", TPolar(Vector3.zero, r - 0.15f, b, 5.4f), new Vector3(1.9f, 0.1f, 0.1f), gold, TFacing(b));
                }
                else
                {
                    Block(room, "Sconce", TPolar(Vector3.zero, r - 0.25f, b, 3.2f), new Vector3(0.3f, 0.5f, 0.4f), gold, TFacing(b));
                    Shape(room, "TorchFlame", ConeMesh(), TPolar(Vector3.zero, r - 0.4f, b, 3.7f), new Vector3(0.35f, 0.6f, 0.35f), flame, false, Quaternion.identity);
                    PointLight(room, TPolar(Vector3.zero, r - 1.2f, b, 3.8f), new Color(1f, 0.6f, 0.3f), 12f, 1.4f);
                }
            }
            // The arch up (north): where the party goes on to once the floor is won.
            Vector3 n = TPolar(Vector3.zero, r - 0.3f, 90f, 0f);
            foreach (float s in new[] { -1f, 1f })
                Block(room, "ArchPost", n + new Vector3(s * 1.9f, 2.3f, 0), new Vector3(0.6f, 4.6f, 0.8f), stone);
            Block(room, "ArchLintel", n + new Vector3(0, 4.9f, 0), new Vector3(4.6f, 0.7f, 0.9f), gold);
            Block(room, "ArchDark", n + new Vector3(0, 2.3f, 0.2f), new Vector3(3.2f, 4.6f, 0.1f), PMat("TunnelDark", new Color(0.05f, 0.04f, 0.08f), 1f));
            PointLight(room, new Vector3(0, TowerWallH - 1.5f, 0), new Color(0.85f, 0.8f, 1f), r * 1.6f, isTop ? 1.6f : 1.2f);
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
