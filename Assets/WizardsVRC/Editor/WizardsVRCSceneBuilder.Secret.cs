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
    /// Secret bosses (a street's Secret Boss settings): the Kraken beneath Tidewatch Docks. A diving bell hangs from a
    /// crane at the water's edge, chained shut until you've started the side quest that opens it (QuestGate). Click it to
    /// go down to a sunken platform beneath the waves: a ring of old stone on the sea floor among kelp, coral and a
    /// wrecked ship, the boss lurking at its edge (it speaks as you arrive). Step into the ring to fight. Beat it and it's
    /// marked on your wizard (the Krakenbane title) with Starshards; a bubble of air takes you back up. Lose and you wake
    /// on the dock.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 SecretOrigin = new Vector3(3200f, -600f, -3200f);
        private static int _secretCount;

        private static void BuildSecretBoss(Transform zoneRoot, ZoneGrid g, ZoneDefinition z, Vector2Int start, List<Vector2Int> ringCells,
            Transform systems, CardDatabase cardDb, ItemDatabase itemDb, PlayerState playerState, QuestManager qm, List<BattleClient> encounters, WorldBuild wb)
        {
            var sb = z.secretBoss;
            if (sb == null || !sb.enabled || sb.roster == null || sb.roster.Count == 0) return;
            Vector2Int toWater;
            Vector2Int cell;
            // A desert street has no sea: its way in stands on its own map symbol (the old station's bell).
            bool desert = IsDesert(z);
            var marked = string.IsNullOrEmpty(sb.entranceSymbol) ? new List<Vector2Int>() : g.Find(sb.entranceSymbol[0]);
            if (marked.Count > 0)
            {
                cell = marked[0];
                // It faces out: toward a wall beside it if there is one, otherwise toward the map's nearest edge.
                toWater = Vector2Int.zero;
                float far = float.MinValue;
                Vector2 mid = new Vector2(g.width * 0.5f, g.height * 0.5f);
                foreach (var d in ZoneGrid.Dirs)
                {
                    float score = (new Vector2(cell.x + d.x, cell.y + d.y) - mid).magnitude + (g.Walkable(cell.x + d.x, cell.y + d.y) ? 0f : 100f);
                    if (score > far) { far = score; toWater = d; }
                }
            }
            else cell = WaterEdgeCell(g, start, ringCells, out toWater);
            if (cell.x < 0)
            {
                Debug.LogWarning("[WizardsVRC World] " + z.displayName + ": no walkable spot beside the water for " + sb.displayName + "'s " + sb.entranceLabel + "; not built.");
                return;
            }
            int index = _secretCount++;
            int boss = sb.roster[0];

            // ---- on the dock: a crane with the diving bell hanging over the water
            Vector3 p = g.Local(cell);
            Vector3 water = ZoneGrid.ToLocalDir(toWater);
            Quaternion face = Quaternion.LookRotation(water);
            var dock = Child(zoneRoot, "SecretWay_" + sb.displayName.Replace(" ", ""), p);
            dock.localRotation = face;
            Transform bell;
            if (desert) bell = StationBell(dock);
            else
            {
            var metal = PMat("Brass", new Color(0.78f, 0.6f, 0.25f), 1f);
            var timber = PMat("TimberBeam", new Color(0.3f, 0.19f, 0.12f), 1f);
            var iron = PMat("Iron", new Color(0.25f, 0.22f, 0.22f), 1f);
            Block(dock, "CranePost", new Vector3(-1.6f, 3f, 0.2f), new Vector3(0.5f, 6f, 0.5f), timber);
            Block(dock, "CraneArm", new Vector3(-0.6f, 5.9f, 1.4f), new Vector3(0.35f, 0.35f, 3.6f), timber);
            Block(dock, "CraneBrace", new Vector3(-1.1f, 4.9f, 0.8f), new Vector3(0.2f, 0.2f, 1.9f), timber, Quaternion.Euler(-40f, 0, 0));
            Block(dock, "Chain", new Vector3(-0.6f, 4.5f, 2.9f), new Vector3(0.08f, 2.6f, 0.08f), iron);
            bell = Child(dock, "DivingBell", new Vector3(-0.6f, 2.2f, 2.9f));
            Shape(bell, "BellBody", CylMesh(), Vector3.zero, new Vector3(1.8f, 1f, 1.8f), metal, false, Quaternion.identity);
            Shape(bell, "BellDome", BlobMesh(), new Vector3(0, 1f, 0), new Vector3(1.8f, 1.2f, 1.8f), metal, false, Quaternion.identity);
            Shape(bell, "BellRim", CylMesh(), new Vector3(0, -0.95f, 0), new Vector3(2f, 0.1f, 2f), iron, false, Quaternion.identity);
            foreach (float a in new[] { 0f, 120f, 240f })
                Shape(bell, "Porthole", CylMesh(), Quaternion.Euler(0, a, 0) * new Vector3(0, 0.2f, -0.88f), new Vector3(0.55f, 0.04f, 0.55f),
                    PMat("Porthole", new Color(0.55f, 0.85f, 1f), 1f, new Color(0.3f, 0.6f, 0.8f)), false, Quaternion.Euler(0, a, 0) * Quaternion.Euler(90f, 0, 0));
            var hit = bell.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, 0.3f, 0);
            hit.size = new Vector3(2.2f, 3f, 2.2f);
            }
            Transform dockReturn = Child(zoneRoot, "SecretReturn_" + index, p - water * 0.4f + new Vector3(0, 0.1f, 0));
            dockReturn.localRotation = face;
            var sign = WorldText(dock, "BellSign", new Vector3(-1.6f, 6.9f, 0.6f), Quaternion.identity, new Vector2(1300, 260), 0.004f, sb.entranceLabel, 48);
            DressSign(sign, 1200f, 240f);
            // Quest arrows for the boss step point here.
            wb.monsterTargets[boss] = bell;

            // ---- the sunken platform, far away
            var root = new GameObject("Sunken_" + z.zoneKey).transform;
            AmbientBed(root, desert ? "Amb_GhostTrain" : "Amb_Underwater", 0.35f);
            root.position = SecretOrigin + new Vector3(index * 300f, 0f, 0f);
            Transform arrival = desert ? GhostTrainArena(root) : SunkenArena(root);

            // The way down (QuestGate on the bell) and back up (a bubble veil).
            var down = UdonSharpUndo.AddComponent<TeleportHelper>(Child(dock, "BellTeleport", Vector3.zero).gameObject);
            down.defaultTarget = arrival;
            wb.behaviours.Add(down);
            var gate = UdonSharpUndo.AddComponent<QuestGate>(bell.gameObject);
            gate.questManager = qm;
            gate.playerState = playerState;
            gate.requiredQuest = sb.questId;
            gate.openIfStarted = sb.questId >= 0 ? new[] { sb.questId } : new int[0];
            gate.teleporter = down;
            gate.label = sign;
            gate.openText = sb.entranceLabel + (desert ? "\n(click to ring it)" : "\n(click to go down)");
            gate.lockedText = sb.entranceLabel + "\n" + sb.lockedHint;
            sign.text = sb.questId >= 0 ? gate.lockedText : gate.openText;
            UdonBehaviour gateBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(gate);
            if (gateBacking != null) gateBacking.interactText = sb.entranceLabel;
            wb.behaviours.Add(gate);

            var bubble = Child(root, "AirBubble", new Vector3(-7f, 1.2f, -12.5f));
            bubble.localRotation = Quaternion.LookRotation(new Vector3(0.5f, 0, 1f));
            Shape(bubble, "Bubble", BlobMesh(), new Vector3(0, 1.6f, 0), desert ? new Vector3(2.2f, 3.2f, 0.4f) : new Vector3(2.6f, 3.2f, 2.6f),
                desert ? PMat("GhostDoor", new Color(0.6f, 1f, 0.9f), 1f, new Color(0.3f, 0.75f, 0.65f))
                    : PMat("AirBubble", new Color(0.75f, 0.92f, 1f), 1f, new Color(0.45f, 0.7f, 0.85f)), false, Quaternion.identity);
            var bcol = bubble.gameObject.AddComponent<BoxCollider>();
            bcol.center = new Vector3(0, 1.6f, 0);
            bcol.size = new Vector3(2.4f, 3.2f, 2.4f);
            var up = UdonSharpUndo.AddComponent<TeleportHelper>(bubble.gameObject);
            up.defaultTarget = dockReturn;
            UdonBehaviour upBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(up);
            if (upBacking != null) upBacking.interactText = desert ? "Step off the train" : "Back up to the surface";
            wb.behaviours.Add(up);
            var upSign = WorldText(bubble, "UpSign", new Vector3(0, 4f, -0.2f), Quaternion.identity, new Vector2(1000, 150), 0.0035f, desert ? "Step off the train" : "Back to the surface", 44);
            DressSign(upSign, 0f, 140f, false);

            // ---- the fight on the platform
            string name = z.zoneKey + "_Secret" + index;
            var client = MakeEncounter(systems, name, cardDb, itemDb, playerState, arrival, sb.roster.ToArray(), false, encounters);
            var m = client.manager;
            m.scaleRosterWithParty = true;
            m.queueSeconds = 6;
            Transform spot = Child(root, "SunkenRing", new Vector3(0, 1.2f, 0));
            m.ringSpots = new[] { spot };
            client.questManager = qm;
            BuildEncounter(root, spot.localPosition, name, client, 0);
            client.encounterRoot = root.Find("Encounter" + name);
            client.hubReturn = arrival;
            client.defeatReturn = dockReturn;
            client.padTitle = sb.displayName + "\nStep into the ring to face it";
            client.padLabel = WorldText(root, "RingSign", new Vector3(0, 6.4f, -2.5f), Quaternion.identity, new Vector2(1100, 220), 0.0045f, client.padTitle, 44);
            DressSign(client.padLabel, 1000f, 210f, false);
            var zone = Child(root, "SunkenZone", spot.localPosition + new Vector3(0, 1.5f, 0));
            var box = zone.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(DuelCircleRadius * 1.6f, 3f, DuelCircleRadius * 1.6f);
            var pad = UdonSharpUndo.AddComponent<BattleQueuePad>(zone.gameObject);
            pad.client = client;
            pad.keepQueued = true;
            pad.zoneRadius = DuelCircleRadius * 1.1f;
            wb.behaviours.Add(pad);

            // The boss lurking at the platform's far edge, and its greeting (TowerFloor: greeting, idle body, the clear).
            var floor = UdonSharpUndo.AddComponent<TowerFloor>(spot.gameObject);
            floor.client = client;
            floor.centre = spot;
            floor.radius = 26f;
            floor.speaker = MonsterLabel(boss);
            floor.line = sb.greeting;
            floor.floorTitle = "";
            floor.isTop = true;
            floor.towerName = sb.displayName;
            floor.clearBits = 1 << Mathf.Clamp(sb.clearBit, 1, 30);
            floor.clearShards = 8;
            var lurk = Child(root, "Lurking", new Vector3(0, desert ? 1.25f : 0.2f, DuelCircleRadius + 4.5f));
            lurk.localRotation = Quaternion.LookRotation(Vector3.back);
            floor.idleBody = MonsterBody(lurk, cardDb, boss, "LurkingBody");
            TowerDialog(spot, floor, cardDb.MonsterSchool(boss));
            client.towerFloor = floor;
            wb.behaviours.Add(floor);

            wb.cullRoots.Add(root.gameObject);
            var secretBox = new Bounds(root.position + new Vector3(0, 25f, 0), new Vector3(200f, 120f, 200f));
            wb.cullBounds.Add(secretBox);
            wb.MusicArea(z, secretBox, sb.displayName);
            root.gameObject.SetActive(false);
            Debug.Log("[WizardsVRC World] " + z.displayName + ": " + sb.displayName + (desert ? " on its ghost train (" + sb.entranceLabel : " beneath the waves (" + sb.entranceLabel + " by the water") +
                      ", quest " + sb.questId + ").");
        }

        /// <summary>
        /// A walkable cell next to the water, clear of NPCs, points, the start and (if it can be) the duel rings, as far
        /// along the street from the start as possible. toWater is the way to the water. (-1, -1) when there's none.
        /// </summary>
        private static Vector2Int WaterEdgeCell(ZoneGrid g, Vector2Int start, List<Vector2Int> ringCells, out Vector2Int toWater)
        {
            toWater = Vector2Int.zero;
            var dist = new Dictionary<Vector2Int, int> { { start, 0 } };
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in ZoneGrid.Dirs)
                {
                    var n = c + d;
                    if (dist.ContainsKey(n) || !g.CanStep(c, n)) continue;
                    dist[n] = dist[c] + 1;
                    q.Enqueue(n);
                }
            }
            Vector2Int best = new Vector2Int(-1, -1), fallback = best;
            Vector2Int bestDir = Vector2Int.zero, fallbackDir = Vector2Int.zero;
            int bestD = -1, fallbackD = -1;
            foreach (var kv in dist)
            {
                var c = kv.Key;
                Vector2Int wdir = Vector2Int.zero;
                foreach (var d in ZoneGrid.Dirs) if (ZoneGrid.IsWater(g.At(c.x + d.x, c.y + d.y))) { wdir = d; break; }
                if (wdir == Vector2Int.zero || g.IsCustom(g.At(c.x, c.y)) || g.At(c.x, c.y) == 'S') continue;
                if (_dungeonCells != null && _dungeonCells.Contains(c)) continue;
                bool free = true;
                Vector3 pc = g.Local(c);
                foreach (var r in ringCells)
                {
                    Vector3 d = g.Local(r) - pc;
                    d.y = 0f;
                    if (d.magnitude < DuelCircleRadius + 1f) { free = false; break; }
                }
                if (free && kv.Value > bestD) { bestD = kv.Value; best = c; bestDir = wdir; }
                if (kv.Value > fallbackD) { fallbackD = kv.Value; fallback = c; fallbackDir = wdir; }
            }
            if (best.x >= 0) { toWater = bestDir; return best; }
            toWater = fallbackDir;
            return fallback;
        }

        /// <summary>
        /// The sea floor round a sunken platform: sand, a wall of deep water all round and the bright surface far above,
        /// the platform's ring of old stone with broken pillars, kelp, coral, a wrecked ship, treasure and rising bubbles.
        /// Returns where you arrive (the platform's south edge, facing the ring).
        /// </summary>
        private static Transform SunkenArena(Transform root)
        {
            var sand = PsxTexMat("SeaFloorSand", new Color(0.5f, 0.58f, 0.6f), TileTexture(StreetTexDir + "/Sand.png"), 3f);
            var stone = PsxTexMat("SunkenStone", new Color(0.42f, 0.52f, 0.55f), TileTexture(TerrainTexDir + "/StoneWall.png"), 2.4f);
            var mossy = PsxTexMat("SunkenMoss", new Color(0.3f, 0.45f, 0.38f), TileTexture(TerrainTexDir + "/ForestFloor.png"), 2f);
            var deep = PMat("DeepWater", new Color(0.04f, 0.16f, 0.26f), 1f, new Color(0.03f, 0.11f, 0.19f));
            var surface = PMat("SurfaceFromBelow", new Color(0.4f, 0.65f, 0.75f), 1f, new Color(0.3f, 0.52f, 0.62f));
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var wreck = PsxTexMat("WreckWood", new Color(0.34f, 0.28f, 0.22f), TileTexture(StreetTexDir + "/Boards.png"), 2f);
            var kelp = PMat("Kelp", new Color(0.2f, 0.45f, 0.25f), 3f, new Color(0.02f, 0.08f, 0.03f));
            var coralCols = new[] { new Color(1f, 0.45f, 0.55f), new Color(1f, 0.6f, 0.25f), new Color(0.7f, 0.4f, 1f), new Color(0.35f, 0.9f, 0.85f) };
            var rng = new System.Random(7703);
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Block(root, "SeaFloor", new Vector3(0, -0.5f, 0), new Vector3(190f, 1f, 190f), sand);
            var mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, 88f, 90f, 0f, 360f, -1f, 64f, 40);
            TavernMesh(root, "DeepWater", mb, deep, false);
            mb = new MeshBuilder();
            float s = 92f, y = 60f;
            mb.Quad(new Vector3(-s, y, -s), new Vector3(s, y, -s), new Vector3(s, y, s), new Vector3(-s, y, s), Vector3.down);
            TavernMesh(root, "Surface", mb, surface, false);

            // The platform: a raised ring of old stone with a step round it.
            mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, 0f, 16f, 0f, 360f, 0f, 1.2f, 40);
            TavernMesh(root, "Platform", mb, stone, true);
            mb = new MeshBuilder();
            SectorPrism(mb, Vector3.zero, 16f, 17.6f, 0f, 360f, 0f, 0.6f, 40);
            TavernMesh(root, "PlatformStep", mb, mossy, true);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f + 18f;
                float h = i % 3 == 0 ? Rr(1.5f, 3f) : Rr(4f, 8f);
                if (AngleGap(a, 270f) < 30f) continue;   // keep the arrival side open
                Shape(root, "SunkenPillar", CylMesh(), TPolar(Vector3.zero, 15f, a, 1.2f + h / 2f), new Vector3(1.3f, h / 2f, 1.3f), stone, true, Quaternion.Euler(Rr(-6f, 6f), 0, Rr(-6f, 6f)));
            }
            for (int i = 0; i < 3; i++)
            {
                float a = Rr(0f, 360f);
                Shape(root, "FallenPillar", CylMesh(), TPolar(Vector3.zero, Rr(22f, 34f), a, 0.6f), new Vector3(1.2f, Rr(2.5f, 4f), 1.2f), stone, true,
                    Quaternion.Euler(0, Rr(0, 180f), 90f));
            }
            // Kelp in clumps and coral on the sea floor round the platform.
            for (int c = 0; c < 22; c++)
            {
                float a = Rr(0f, 360f), r = Rr(21f, 70f);
                if (AngleGap(a, 270f) < 20f && r < 30f) continue;
                Vector3 at = TPolar(Vector3.zero, r, a, 0f);
                int blades = rng.Next(3, 7);
                for (int k = 0; k < blades; k++)
                {
                    float h = Rr(4f, 13f);
                    Block(root, "Kelp", at + new Vector3(Rr(-1.2f, 1.2f), h / 2f, Rr(-1.2f, 1.2f)), new Vector3(0.35f, h, 0.06f), kelp,
                        Quaternion.Euler(Rr(-8f, 8f), Rr(0, 360f), Rr(-8f, 8f)));
                }
            }
            for (int c = 0; c < 30; c++)
            {
                float a = Rr(0f, 360f), r = Rr(18.5f, 60f);
                Vector3 at = TPolar(Vector3.zero, r, a, 0f);
                Color cc = coralCols[rng.Next(coralCols.Length)];
                var mat = PMat("Coral_" + ColorUtility.ToHtmlStringRGB(cc), cc, 1f, cc * 0.25f);
                if (c % 3 == 0) Shape(root, "CoralFan", BlobMesh(), at + new Vector3(0, 0.9f, 0), new Vector3(Rr(1.2f, 2.2f), Rr(1.4f, 2.4f), 0.2f), mat, false, Quaternion.Euler(0, Rr(0, 360f), 0));
                else
                    for (int k = 0; k < 4; k++)
                        Shape(root, "CoralSpire", ConeMesh(), at + new Vector3(Rr(-0.7f, 0.7f), 0.6f, Rr(-0.7f, 0.7f)), new Vector3(0.35f, Rr(0.9f, 2.2f), 0.35f), mat, false,
                            Quaternion.Euler(Rr(-12f, 12f), 0, Rr(-12f, 12f)));
            }
            // A wrecked ship on its side to the north-east: keel, ribs, a broken mast, and its treasure spilled on the sand.
            var ship = Child(root, "Wreck", new Vector3(30f, 0f, 30f));
            ship.localRotation = Quaternion.Euler(0, -35f, 18f);
            Block(ship, "Keel", new Vector3(0, 0.6f, 0), new Vector3(1.2f, 1.2f, 22f), wreck);
            for (int k = 0; k < 9; k++)
            {
                float z = -9f + k * 2.25f;
                foreach (float sx in new[] { -1f, 1f })
                    Block(ship, "Rib", new Vector3(sx * 2.2f, 2.6f, z), new Vector3(0.35f, 4.4f, 0.4f), wreck, Quaternion.Euler(0, 0, sx * -24f));
            }
            Block(ship, "HullSide", new Vector3(-3f, 2.4f, -2f), new Vector3(0.3f, 3.6f, 14f), wreck, Quaternion.Euler(0, 0, 22f));
            Shape(ship, "Mast", CylMesh(), new Vector3(6f, 1f, 4f), new Vector3(0.6f, 7f, 0.6f), wreck, false, Quaternion.Euler(0, 20f, 80f));
            for (int k = 0; k < 7; k++)
                Block(root, "SpilledGold", new Vector3(22f + Rr(-3f, 3f), 0.15f, 21f + Rr(-3f, 3f)), new Vector3(Rr(0.4f, 0.9f), 0.3f, Rr(0.3f, 0.6f)), gold, Quaternion.Euler(0, Rr(0, 360f), 0));

            // Bubbles rising everywhere, and the cold sea light.
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f;
                Motes(root, "Bubbles", TPolar(Vector3.zero, k == 0 ? 0f : 30f, a, 0.5f), new Vector3(k == 0 ? 30f : 40f, 0.3f, k == 0 ? 30f : 40f),
                    new Color(0.75f, 0.92f, 1f), 10f, 6f, 10f, new Vector2(0.08f, 0.22f), new Vector3(0, 1.4f, 0), new Vector3(0.15f, 0.4f, 0.15f), 0f);
            }
            PointLight(root, new Vector3(0, 12f, 0), new Color(0.45f, 0.75f, 0.85f), 60f, 1.6f);
            for (int k = 0; k < 4; k++)
                PointLight(root, TPolar(Vector3.zero, 14f, k * 90f + 45f, 5f), new Color(0.3f, 0.85f, 0.75f), 18f, 1.2f);

            Transform arrival = Child(root, "SunkenArrival", new Vector3(0, 1.3f, -13f));
            arrival.localRotation = Quaternion.identity;
            return arrival;
        }

        /// <summary>Kraken and tentacle bodies (creature types "Kraken" and "Tentacle"); null for anything else.</summary>
        private static GameObject CreatureBody(Transform parent, CardDatabase db, MonsterEntry entry, string name, float scale)
        {
            var dustwind = DustwindCreature(parent, db, entry, name, scale);
            if (dustwind != null) return dustwind;
            if (entry == null || (entry.creatureType != "Kraken" && entry.creatureType != "Tentacle")) return null;
            Material mat = MonsterMat(db, entry.id);
            if (mat == null) mat = PMat("Monster_" + entry.id, entry.bodyColor, 1f);
            var pale = PMat("Sucker", Color.Lerp(entry.bodyColor, Color.white, 0.6f), 1f);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            if (entry.creatureType == "Kraken")
            {
                var eye = PMat("KrakenEye", new Color(1f, 0.85f, 0.2f), 1f, new Color(1f, 0.7f, 0.1f));
                Shape(root, "Mantle", BlobMesh(), new Vector3(0, 2.1f, -0.35f), new Vector3(1.5f, 2.1f, 1.5f), mat, false, Quaternion.Euler(-15f, 0, 0));
                Shape(root, "Head", BlobMesh(), new Vector3(0, 0.95f, 0), new Vector3(1.7f, 1.15f, 1.6f), mat, false, Quaternion.identity);
                foreach (float sx in new[] { -1f, 1f })
                {
                    Shape(root, "Eye", BlobMesh(), new Vector3(sx * 0.48f, 1.15f, 0.68f), new Vector3(0.36f, 0.3f, 0.2f), eye, false, Quaternion.identity);
                    Shape(root, "Pupil", BlobMesh(), new Vector3(sx * 0.48f, 1.15f, 0.77f), new Vector3(0.08f, 0.24f, 0.06f), PMat("KrakenPupil", new Color(0.05f, 0.03f, 0.05f), 1f), false, Quaternion.identity);
                }
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f + 22.5f;
                    Quaternion yaw = Quaternion.Euler(0, a, 0);
                    for (int j = 0; j < 4; j++)
                    {
                        float r = 0.75f + j * 0.42f, h = 0.3f + Mathf.Sin(j * 0.95f) * 0.3f + (k % 2) * j * 0.12f;
                        float w = 0.34f * (1f - j * 0.2f);
                        Shape(root, "Tentacle", CylMesh(), yaw * new Vector3(0, h, r), new Vector3(w, 0.26f, w), mat, false, yaw * Quaternion.Euler(70f - j * 15f, 0, 0));
                        if (j > 0) Shape(root, "Sucker", BlobMesh(), yaw * new Vector3(0, h - w * 0.4f, r), Vector3.one * w * 0.45f, pale, false, Quaternion.identity);
                    }
                    Shape(root, "TentacleTip", ConeMesh(), yaw * new Vector3(0, 1.1f + (k % 2) * 0.4f, 2.5f), new Vector3(0.12f, 0.5f, 0.12f), mat, false, yaw * Quaternion.Euler(25f, 0, 0));
                }
            }
            else
            {
                // One arm rising from the water: tapering segments curling over at the top, suckers up its front.
                Vector3 at = Vector3.zero;
                for (int j = 0; j < 7; j++)
                {
                    float w = 0.55f * (1f - j * 0.11f);
                    float bend = j * 9f;
                    at += Quaternion.Euler(bend, 0, 0) * new Vector3(0, 0.42f, 0);
                    Shape(root, "Arm", CylMesh(), at, new Vector3(w, 0.24f, w), mat, false, Quaternion.Euler(bend, 0, 0));
                    Shape(root, "Sucker", BlobMesh(), at + Quaternion.Euler(bend, 0, 0) * new Vector3(0, 0, w * 0.48f), Vector3.one * w * 0.38f, pale, false, Quaternion.identity);
                }
                Shape(root, "ArmTip", ConeMesh(), at + new Vector3(0, -0.1f, 0.35f), new Vector3(0.18f, 0.55f, 0.18f), mat, false, Quaternion.Euler(120f, 0, 0));
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root.gameObject;
        }
    }
}
