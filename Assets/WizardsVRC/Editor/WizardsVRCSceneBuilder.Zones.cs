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
    /// Builds the world from the authored data in Assets/WizardsVRC/WorldData: the Monster Catalog goes into
    /// CardDatabase, every Zone becomes a street set (maze from its text map, duel ring spots, patrols, NPCs, quest
    /// points, doors), and every Questline is appended to the QuestManager tables at its save ids.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>Body material per monster (same order as CardDatabase.monsterIds), shared by every encounter.</summary>
        private static Material[] _monsterMats;
        /// <summary>The catalog used for this build (for model prefabs).</summary>
        private static MonsterCatalog _monsterCatalog;

        /// <summary>What the zone pass hands to the quest pass and the guide.</summary>
        private class WorldBuild
        {
            public WorldDataStore.Data data;
            public Transform cutsceneParent;
            public readonly List<UdonSharpBehaviour> behaviours = new List<UdonSharpBehaviour>();
            public readonly List<GameObject> zoneRoots = new List<GameObject>();
            public readonly Dictionary<string, int> npcIds = new Dictionary<string, int>();
            public readonly Dictionary<string, Transform> npcTargets = new Dictionary<string, Transform>();
            public readonly Dictionary<string, Transform[]> npcShots = new Dictionary<string, Transform[]>();
            public readonly Dictionary<string, string> npcNames = new Dictionary<string, string>();
            public readonly List<Cutscene> npcCutscenes = new List<Cutscene>();
            public readonly Dictionary<string, int> pointIds = new Dictionary<string, int>();
            public readonly Dictionary<string, Transform> pointTargets = new Dictionary<string, Transform>();
            public readonly Dictionary<string, string> pointLabels = new Dictionary<string, string>();
            public readonly Dictionary<int, Transform> monsterTargets = new Dictionary<int, Transform>();
            public readonly List<float> areaMin = new List<float>(), areaMax = new List<float>();
            public readonly List<Transform> areaIn = new List<Transform>(), areaOut = new List<Transform>();
            public readonly List<string> areaInNames = new List<string>(), areaOutNames = new List<string>();
            /// <summary>Zone roots and their bounds for the AreaCuller.</summary>
            public readonly List<GameObject> cullRoots = new List<GameObject>();
            public readonly List<Bounds> cullBounds = new List<Bounds>();
            /// <summary>Street zones for the skylines (where each lies is worked out once the hub is known).</summary>
            public readonly List<SkyArea> skyAreas = new List<SkyArea>();
        }

        // ================================================================== monsters

        private static Dictionary<int, string> CardsOf(CardDatabase db)
        {
            var d = new Dictionary<int, string>();
            for (int i = 0; i < db.ids.Length; i++) d[db.ids[i]] = db.names[i];
            return d;
        }

        /// <summary>Copies the Monster Catalog into CardDatabase's monster tables and makes one body material per monster.</summary>
        private static void ApplyMonsterCatalog(CardDatabase db, MonsterCatalog cat)
        {
            _monsterMats = null;
            _monsterCatalog = cat;
            if (cat == null) return;
            var spellReport = new WorldValidator.Report();
            WorldValidator.ValidateMonsters(cat, CardsOf(db), spellReport);
            foreach (var e in spellReport.errors) Debug.LogError("[WizardsVRC World] " + e);
            var list = new List<MonsterEntry>();
            var seen = new HashSet<int>();
            foreach (var m in cat.monsters) if (m != null && m.id > 0 && seen.Add(m.id)) list.Add(m);
            int n = list.Count, ms = CardDatabase.MONSTER_SPELLS;
            db.monsterIds = new int[n];
            db.monsterNames = new string[n];
            db.monsterSchools = new int[n];
            db.monsterLevels = new int[n];
            db.monsterRanks = new int[n];
            db.monsterTypes = new string[n];
            db.monsterHps = new int[n];
            db.monsterSpells = new int[n * ms];
            db.monsterXps = new int[n];
            db.monsterGoldMin = new int[n];
            db.monsterGoldMax = new int[n];
            db.monsterDropItems = new int[n];
            db.monsterDropChances = new int[n];
            db.monsterBodyScales = new float[n];
            db.monsterIsBoss = new int[n];
            db.monsterIsElite = new int[n];
            db.monsterHpPerExtraPlayer = new int[n];
            db.monsterColors = new Color[n];
            _monsterMats = new Material[n];
            for (int i = 0; i < n; i++)
            {
                var m = list[i];
                db.monsterIds[i] = m.id;
                db.monsterNames[i] = m.name;
                db.monsterSchools[i] = (int)m.school;
                db.monsterLevels[i] = Mathf.Max(1, m.level);
                db.monsterRanks[i] = Mathf.Max(1, m.rank);
                db.monsterTypes[i] = m.creatureType != null ? m.creatureType.Trim() : "";
                db.monsterHps[i] = Mathf.Max(1, m.health);
                for (int k = 0; k < ms; k++) db.monsterSpells[i * ms + k] = k < m.spells.Count ? m.spells[k] : 0;
                db.monsterXps[i] = m.xp;
                db.monsterGoldMin[i] = Mathf.Min(m.goldMin, m.goldMax);
                db.monsterGoldMax[i] = Mathf.Max(m.goldMin, m.goldMax);
                db.monsterDropItems[i] = m.dropItem;
                db.monsterDropChances[i] = Mathf.Clamp(m.dropChance, 0, 100);
                db.monsterBodyScales[i] = Mathf.Max(0.2f, m.bodyScale);
                db.monsterIsBoss[i] = m.isBoss ? 1 : 0;
                db.monsterIsElite[i] = m.isElite && !m.isBoss ? 1 : 0;
                db.monsterHpPerExtraPlayer[i] = m.healthPerExtraWizard;
                db.monsterColors[i] = m.bodyColor;
                _monsterMats[i] = PMat("Monster_" + m.id, m.bodyColor, 1f);
            }
        }

        /// <summary>
        /// The body of a monster (patrol or ring seat): its catalog model prefab if it has one (unpacked, colliders
        /// removed, scaled by bodyScale), otherwise a capsule in its colour. Feet at the parent's origin.
        /// </summary>
        private static GameObject MonsterBody(Transform parent, CardDatabase db, int monsterId, string name)
        {
            float scale = monsterId > 0 ? db.MonsterBodyScale(monsterId) : 1f;
            MonsterEntry entry = _monsterCatalog != null && monsterId > 0 ? _monsterCatalog.Find(monsterId) : null;
            if (entry != null && entry.modelPrefab != null)
            {
                var model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(entry.modelPrefab);
                UnityEditor.PrefabUtility.UnpackPrefabInstance(model, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
                model.name = name;
                model.transform.SetParent(parent, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = model.transform.localScale * scale;
                foreach (var col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                return model;
            }
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = name;
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0, scale, 0);
            body.transform.localScale = Vector3.one * scale;
            Material mat = MonsterMat(db, monsterId);
            if (mat == null) mat = Mat("MinionBody", new Color(0.75f, 0.65f, 0.3f));
            body.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            return body;
        }

        private static Material MonsterMat(CardDatabase db, int monsterId)
        {
            int i = db.MonsterIndexOf(monsterId);
            return _monsterMats != null && i >= 0 && i < _monsterMats.Length ? _monsterMats[i] : null;
        }

        // ================================================================== zones

        private static void BuildZones(Transform systems, Transform hubRoot, QuestManager qm, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, List<BattleClient> encounters, WorldBuild wb, HubLayout hub)
        {
            // The hub's geometry was built with the castle; its NPCs, points and practice ring go in now.
            BuildHubContents(hub, systems, qm, cardDb, itemDb, playerState, encounters, wb);
            foreach (var z in wb.data.zones)
                if (z != null && z.includeInBuild && !z.isHub) BuildZone(z, systems, hubRoot, qm, cardDb, itemDb, playerState, encounters, wb, hub);
            qm.streetNpcCutscenes = wb.npcCutscenes.ToArray();
        }

        private static void BuildZone(ZoneDefinition z, Transform systems, Transform hubRoot, QuestManager qm, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, List<BattleClient> encounters, WorldBuild wb, HubLayout hub)
        {
            var g = new ZoneGrid(z);
            var starts = g.Find('S');
            if (g.height == 0 || starts.Count == 0)
            {
                Debug.LogError("[WizardsVRC World] Zone '" + z.zoneKey + "' has no map or no S entrance; skipped.");
                return;
            }
            string k = z.zoneKey;
            var root = new GameObject("Zone_" + k).transform;
            root.position = new Vector3(0f, z.height, 0f);
            wb.zoneRoots.Add(root.gameObject);
            float cs = g.cell;

            Material ground = PMat(k + "_Ground", z.groundColor, 8f);
            // PS1 textures for the street's style (Tools/make_street_textures.py), tinted with its colours.
            Material bld, roof, low, leaves, trunk;
            StreetMats(z, k, out bld, out roof, out low, out leaves, out trunk);
            Material glow = PMat(k + "_Glow", z.accentColor, 1f, z.accentColor);
            Material window = PMat(k + "_Window", z.accentColor * 0.8f, 1f, z.accentColor * 0.6f);
            Material deadLamp = PMat(k + "_DeadLamp", new Color(0.12f, 0.12f, 0.14f), 1f);
            Material wood = PMat(k + "_Wood", new Color(0.32f, 0.21f, 0.14f), 2f);
            Material door = PMat(k + "_Door", new Color(0.35f, 0.2f, 0.12f), 2f);

            // ---- ground, boundary and the map layers
            float W = g.width * cs, H = g.height * cs;
            float baseY = g.minLevel * g.step;
            // The terrain tops every cell 2 cm above baseY, so the slab under it sits well below (no z-fighting far off).
            Block(root, "Ground", new Vector3(0, baseY - GroundDrop - 0.25f, 0), new Vector3(W, 0.5f, H), ground);

            // The way home: a tunnel behind the entrance (S) into the mountains, if the castle has a gate (tunnel) for this
            // street. Houses keep clear of it and the map's edge has a gap for it.
            Vector2Int start = starts[0];
            Vector2Int open = g.OpenDirection(start);
            Vector3 openDir = ZoneGrid.ToLocalDir(open);
            HubGate gate = null;
            if (hub != null) hub.gates.TryGetValue(k, out gate);
            Vector3 mouth = g.Local(start) - openDir * (cs * 0.5f);
            var gaps = new List<UnityEngine.Rect>();
            UnityEngine.Rect tunnelRect = new UnityEngine.Rect();
            if (gate != null)
            {
                Vector3 right = Vector3.Cross(Vector3.up, -openDir);
                float hwT = TunW / 2f + TunT + 1f;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (float a in new[] { -hwT, TunL2 + hwT })
                    foreach (float b in new[] { -1f, TunL1 + TunL3 + 1f })
                    {
                        Vector3 c = mouth + right * a - openDir * b;
                        x0 = Mathf.Min(x0, c.x); x1 = Mathf.Max(x1, c.x); z0 = Mathf.Min(z0, c.z); z1 = Mathf.Max(z1, c.z);
                    }
                tunnelRect = UnityEngine.Rect.MinMaxRect(x0, z0, x1, z1);
                gaps.Add(tunnelRect);
            }
            EdgeWalls(root, W, H, gaps);

            Transform layout = Child(root, "Layout", Vector3.zero);
            var stairCells = new HashSet<Vector2Int>();
            // No ground (or kerbs) under the tunnel home: it has its own floor, and a kerb across its mouth would trip you.
            if (gate != null) _terrainSkip = CellsBehind(g, mouth, -openDir, TunW / 2f + TunT + 1f, TunL1 + TunL3 + 2f);
            BuildTerrain(layout, g, MakeTerrainMats(z, k), baseY, stairCells);
            OuterSea(layout, g);
            int houseIndex = 0;
            var noHouse = new HashSet<Vector2Int>();
            if (gate != null)
                for (int y = 0; y < g.height; y++)
                    for (int x = 0; x < g.width; x++)
                    {
                        Vector3 c = g.Local(x, y);
                        if (tunnelRect.Overlaps(new UnityEngine.Rect(c.x - cs / 2f, c.z - cs / 2f, cs, cs))) noHouse.Add(new Vector2Int(x, y));
                    }
            foreach (var r in RectsAt(g, (x, y) => g.At(x, y) == '#' && !noHouse.Contains(new Vector2Int(x, y)), 3, 3))
                House(layout, g, z, r, houseIndex++, bld, roof, window);
            foreach (var r in Rects(g, ch => ch == '=', 6, 6))
            {
                Vector3 c = RectCenter(g, r);
                Block(layout, "LowWall", c + new Vector3(0, 0.7f, 0), new Vector3(r.width * cs, 1.4f, r.height * cs), low);
                Block(layout, "LowWallCap", c + new Vector3(0, 1.45f, 0), new Vector3(r.width * cs + 0.3f, 0.12f, r.height * cs + 0.3f), roof);
            }
            foreach (var t in g.Find('^')) Tree(layout, g, t, trunk, leaves);
            StreetLamps(layout, g, z, wood, glow, stairCells);
            _dungeonSystems = systems;
            _dungeonStart = null;
            _dungeonCells = null;
            if (z.landmarks != null && z.landmarks.Count > 0)
                BuildLandmarks(root, g, z, null, null, wb.data, bld, roof, PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f)),
                    PMat("BannerBlue", new Color(0.15f, 0.25f, 0.75f), 1f), door, window, wood, wb.behaviours);

            // ---- entrance: spawn, the tunnel home (or, without a castle gate, a door), and the way in from the castle
            Transform spawn = Child(root, "Spawn", g.Local(start) + new Vector3(0, 0.05f, 0));
            spawn.localRotation = Quaternion.LookRotation(openDir);
            var sky = new SkyArea { key = k, zone = z, root = root.gameObject, radius = Mathf.Max(W, H) / 2f, garden = IsGardenZone(g) };
            GameObject inDoor, outDoor;
            if (gate != null)
            {
                // Walk out of the castle's tunnel and you walk out of this one (TunnelPassage links the two middles).
                var tm = TunnelMats(z);
                var e = BuildTunnel(root, "Home", mouth, -openDir, tm[0], tm[1], tm[2], tm[3], wb.behaviours);
                TunnelFacade(e, tm[0], PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f)), "Starfall Castle", new Color(1f, 0.85f, 0.4f));
                e.mountainCentre = Mountains(root, k + "_Home", e, 0f, 12f, 0.8f, tm[5], tm[6]);
                e.passage.there = gate.tunnel.mid;
                gate.tunnel.passage.there = e.mid;
                gate.gate.openIfStarted = QuestsInZone(z, wb.data);
                inDoor = gate.front.gameObject;
                outDoor = e.front.gameObject;
                sky.hubBearing = BearingOf(gate.tunnel.root.position);
                sky.ownBearing = BearingOf(e.root.localPosition);
            }
            else
            {
                Debug.LogWarning("[WizardsVRC World] Zone '" + k + "' has no Street Gate in the hub, so it gets a door instead of a tunnel. " +
                                 "Add a Street Gate landmark (Target Zone " + k + ") to the hub map for a tunnel.");
                Transform hubReturn = Child(hubRoot, k + "Return", z.hubReturnPosition);
                hubReturn.localRotation = Quaternion.Euler(0, z.hubReturnYaw, 0);
                outDoor = ZoneDoor(root, "ExitDoor", g.Local(start) - openDir * (cs * 0.45f) + new Vector3(0, 2.5f, 0), Quaternion.LookRotation(openDir),
                    Quaternion.LookRotation(-openDir), door, "Starfall Castle\n(click door)");
                var toHub = UdonSharpUndo.AddComponent<TeleportHelper>(outDoor);
                toHub.defaultTarget = hubReturn;
                toHub.hideZones = new[] { root.gameObject };
                wb.behaviours.Add(toHub);
                sky.hubBearing = BearingOf(z.hubDoorPosition);
                sky.ownBearing = BearingOf(g.Local(start));
                Quaternion hubRot = Quaternion.Euler(0, z.hubDoorYaw, 0);
                inDoor = ZoneDoor(hubRoot, k + "Door", z.hubDoorPosition, hubRot, hubRot, door, z.displayName + "\n(click door)  " + z.levelRange);
                var toZone = UdonSharpUndo.AddComponent<TeleportHelper>(inDoor);
                toZone.defaultTarget = spawn;
                toZone.showZones = new[] { root.gameObject };
                wb.behaviours.Add(toZone);
            }
            wb.skyAreas.Add(sky);

            wb.areaMin.Add(z.height - 50f);
            wb.areaMax.Add(z.height + 60f);
            wb.areaIn.Add(inDoor.transform);
            wb.areaOut.Add(outDoor.transform);
            wb.areaInNames.Add(z.displayName + (gate != null ? " tunnel (castle)" : " door (castle)"));
            wb.areaOutNames.Add(gate != null ? "Tunnel back to Starfall Castle" : "Door back to Starfall Castle");

            // ---- NPCs, points, encounters
            foreach (var npc in z.npcs) if (npc != null) BuildNpc(root, g, z, npc, qm, wb);
            foreach (var p in z.points) if (p != null) BuildPoints(root, g, p, qm, wb, glow, deadLamp, wood);
            foreach (var e in z.encounters)
                if (e != null && ZoneGrid.IsRingDigit(e.Symbol))
                    BuildZoneEncounter(root, g, z, e, start, spawn, systems, cardDb, itemDb, playerState, qm, encounters, wb);

            // Culling: small props drop out with distance; the whole zone is off for anyone not in it.
            AddDistanceCulling(root, z.detailDistance);
            wb.cullRoots.Add(root.gameObject);
            wb.cullBounds.Add(ZoneCullBounds(root.position, W, H));
            root.gameObject.SetActive(false);
        }

        /// <summary>Save ids of quests with a step (or giver) among this zone's NPCs or points.</summary>
        private static int[] QuestsInZone(ZoneDefinition z, WorldDataStore.Data data)
        {
            var keys = new HashSet<string>();
            foreach (var n in z.npcs) if (n != null && !string.IsNullOrEmpty(n.key)) keys.Add(n.key);
            foreach (var p in z.points) if (p != null && !string.IsNullOrEmpty(p.key)) keys.Add(p.key);
            var ids = new List<int>();
            foreach (var ql in data.questlines)
            {
                if (ql == null || !ql.includeInBuild) continue;
                foreach (var q in ql.quests)
                {
                    if (q == null) continue;
                    bool here = !q.startsAutomatically && keys.Contains(q.giverNpc ?? "");
                    foreach (var st in q.steps)
                        if (st != null && (keys.Contains(st.npc ?? "") && st.kind == QuestStepKind.Talk ||
                                           keys.Contains(st.point ?? "") && (st.kind == QuestStepKind.Interact || st.kind == QuestStepKind.Explore)))
                            here = true;
                    if (here && !ids.Contains(q.saveId)) ids.Add(q.saveId);
                }
            }
            return ids.ToArray();
        }

        // ------------------------------------------------------------------ layout pieces

        /// <summary>
        /// Greedy rectangles of matching cells (fewer, bigger blocks), each at most maxW x maxH cells. A rectangle never
        /// spans two heights, so things built over it sit flat on the ground.
        /// </summary>
        private static List<RectInt> Rects(ZoneGrid g, System.Func<char, bool> match, int maxW, int maxH)
        {
            return RectsAt(g, (x, y) => match(g.At(x, y)), maxW, maxH);
        }

        /// <summary>As <see cref="Rects"/>, matching cells by position.</summary>
        private static List<RectInt> RectsAt(ZoneGrid g, System.Func<int, int, bool> match, int maxW, int maxH)
        {
            var used = new bool[g.width, g.height];
            var list = new List<RectInt>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    if (used[x, y] || !match(x, y)) continue;
                    int lv = g.Level(x, y);
                    int w = 1;
                    while (w < maxW && x + w < g.width && !used[x + w, y] && match(x + w, y) && g.Level(x + w, y) == lv) w++;
                    int h = 1;
                    while (h < maxH && y + h < g.height)
                    {
                        bool rowOk = true;
                        for (int i = 0; i < w; i++)
                            if (used[x + i, y + h] || !match(x + i, y + h) || g.Level(x + i, y + h) != lv) { rowOk = false; break; }
                        if (!rowOk) break;
                        h++;
                    }
                    for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) used[xx, yy] = true;
                    list.Add(new RectInt(x, y, w, h));
                }
            return list;
        }

        private static Vector3 RectCenter(ZoneGrid g, RectInt r)
        {
            return (g.Local(r.xMin, r.yMin) + g.Local(r.xMax - 1, r.yMax - 1)) * 0.5f;
        }

        /// <summary>A thin floor tile without a collider (the ground under it is solid).</summary>
        private static void Slab(Transform parent, string name, ZoneGrid g, RectInt r, float y, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = RectCenter(g, r) + new Vector3(0, y, 0);
            go.transform.localScale = new Vector3(r.width * g.cell, 0.06f, r.height * g.cell);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static GameObject Deco(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, Quaternion rot, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A house filling the rectangle: walls, an overhanging roof, sometimes a ridge, lit windows facing the roads.</summary>
        /// <summary>Cells lying wholly past 'mouth' in direction 'dir', within 'halfWidth' of its line and 'length' of it.</summary>
        private static HashSet<Vector2Int> CellsBehind(ZoneGrid g, Vector3 mouth, Vector3 dir, float halfWidth, float length)
        {
            var set = new HashSet<Vector2Int>();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    float sMin, sMax, lMin, lMax;
                    CellSpan(g, x, y, mouth, dir, right, out sMin, out sMax, out lMin, out lMax);
                    if (sMin >= -0.01f && sMin < length && lMax > -halfWidth && lMin < halfWidth) set.Add(new Vector2Int(x, y));
                }
            return set;
        }

        /// <summary>A cell's extent along 'dir' (s) and across it (l), measured from 'origin'.</summary>
        private static void CellSpan(ZoneGrid g, int x, int y, Vector3 origin, Vector3 dir, Vector3 right, out float sMin, out float sMax, out float lMin, out float lMax)
        {
            Vector3 c = g.Local(x, y) - origin;
            float h = g.cell / 2f;
            sMin = lMin = float.MaxValue;
            sMax = lMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 q = c + new Vector3(i % 2 == 0 ? -h : h, 0f, i < 2 ? -h : h);
                float s = q.x * dir.x + q.z * dir.z, l = q.x * right.x + q.z * right.z;
                sMin = Mathf.Min(sMin, s); sMax = Mathf.Max(sMax, s);
                lMin = Mathf.Min(lMin, l); lMax = Mathf.Max(lMax, l);
            }
        }

        /// <summary>
        /// Water that reaches the map's edge (a harbour, a lake shore) carries on out to the horizon: a water plane past
        /// each edge where the map's water meets it, over a dark sea bed so nothing shows through. East and west planes run
        /// on past the corners they reach; north and south ones stop at the map's corners, so the planes never overlap.
        /// </summary>
        private static void OuterSea(Transform parent, ZoneGrid g)
        {
            const float Out = 600f;
            float cs = g.cell, W = g.width * cs, H = g.height * cs;
            var surfaces = WaterSurfaces(g);
            Material water = PsxWaterMat();
            Material bed = PMat("SeaBed", new Color(0.05f, 0.09f, 0.16f), 4f);
            for (int side = 0; side < 4; side++)
            {
                // side 0 north (row 0), 1 south (last row), 2 east (last column), 3 west (column 0)
                bool alongX = side < 2;
                int n = alongX ? g.width : g.height;
                int first = -1, last = -1;
                float y = 0f;
                for (int i = 0; i < n; i++)
                {
                    int x = alongX ? i : side == 2 ? g.width - 1 : 0;
                    int row = alongX ? (side == 0 ? 0 : g.height - 1) : i;
                    if (!ZoneGrid.IsWater(g.At(x, row))) continue;
                    if (first < 0) { first = i; y = surfaces[new Vector2Int(x, row)]; }
                    last = i;
                }
                if (first < 0) continue;
                float a0 = first * cs, a1 = (last + 1) * cs;
                if (!alongX)
                {
                    if (first == 0) a0 -= Out;
                    if (last == n - 1) a1 += Out;
                }
                float mid = (a0 + a1) / 2f, len = a1 - a0;
                Vector3 c;
                Vector3 size;
                if (alongX)
                {
                    float x = mid - W / 2f;
                    float zc = side == 0 ? H / 2f + Out / 2f : -H / 2f - Out / 2f;
                    c = new Vector3(x, 0f, zc);
                    size = new Vector3(len, 1f, Out);
                }
                else
                {
                    float zc = H / 2f - mid;
                    float x = side == 2 ? W / 2f + Out / 2f : -W / 2f - Out / 2f;
                    c = new Vector3(x, 0f, zc);
                    size = new Vector3(Out, 1f, len);
                }
                Shape(parent, "OuterSea", GridMesh(24, 24, false), new Vector3(c.x, y, c.z), size, water, false, Quaternion.identity);
                Block(parent, "OuterSeaBed", new Vector3(c.x, y - 3f, c.z), new Vector3(size.x, 0.5f, size.z), bed);
            }
        }

        private static void House(Transform parent, ZoneGrid g, ZoneDefinition z, RectInt r, int index, Material wall, Material roof, Material window)
        {
            float cs = g.cell;
            Vector3 c = RectCenter(g, r);
            bool edge = r.xMin == 0 || r.yMin == 0 || r.xMax == g.width || r.yMax == g.height;
            float h = Mathf.Lerp(z.buildingHeight.x, z.buildingHeight.y, g.Hash(r.xMin, r.yMin, 1)) + (edge ? 4f : 0f);
            float sx = r.width * cs, sz = r.height * cs;
            Block(parent, "House", c + new Vector3(0, h / 2f, 0), new Vector3(sx, h, sz), wall);
            Deco(parent, PrimitiveType.Cube, "Roof", c + new Vector3(0, h + 0.3f, 0), new Vector3(sx + 1f, 0.6f, sz + 1f), roof, Quaternion.identity, false);
            if (g.Hash(r.xMin, r.yMin, 2) > 0.45f)
            {
                bool alongX = sx >= sz;
                Deco(parent, PrimitiveType.Cube, "Ridge", c + new Vector3(0, h + 1.1f, 0), alongX ? new Vector3(sx * 0.9f, 1.2f, sz * 0.45f) : new Vector3(sx * 0.45f, 1.2f, sz * 0.9f),
                    roof, Quaternion.identity, false);
            }
            if (g.Hash(r.xMin, r.yMin, 3) > 0.6f)
                Deco(parent, PrimitiveType.Cube, "Chimney", c + new Vector3(sx * 0.25f, h + 1.6f, -sz * 0.2f), new Vector3(0.9f, 2.4f, 0.9f), wall, Quaternion.identity, false);

            // Glowing windows on faces that look onto a road.
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        if (!g.Walkable(x + d.x, y + d.y) || g.Hash(x, y, 10 + d.x * 3 + d.y) > 0.55f) continue;
                        Vector3 dir = ZoneGrid.ToLocalDir(d);
                        Vector3 face = g.Local(x, y) + dir * (cs * 0.5f + 0.03f);
                        Quaternion rot = Quaternion.LookRotation(dir);
                        Deco(parent, PrimitiveType.Cube, "Window", face + new Vector3(0, 3.2f, 0), new Vector3(1.1f, 1.4f, 0.08f), window, rot, false);
                        if (h > 9f && g.Hash(x, y, 20 + d.x + d.y * 5) > 0.4f)
                            Deco(parent, PrimitiveType.Cube, "Window", face + new Vector3(0, 6.6f, 0), new Vector3(1.1f, 1.4f, 0.08f), window, rot, false);
                    }
        }

        private static void Tree(Transform parent, ZoneGrid g, Vector2Int cell, Material trunk, Material leaves)
        {
            Vector3 p = g.Local(cell);
            float s = 0.8f + g.Hash(cell.x, cell.y, 4) * 0.5f;
            Deco(parent, PrimitiveType.Cylinder, "Trunk", p + new Vector3(0, 1.6f * s, 0), new Vector3(0.7f, 1.6f * s, 0.7f), trunk, Quaternion.identity, true);
            Deco(parent, PrimitiveType.Sphere, "Canopy", p + new Vector3(0, 4.2f * s, 0), new Vector3(4.5f, 3.6f, 4.5f) * s, leaves, Quaternion.identity, false);
        }

        /// <summary>Decorative lamp posts along road cells that border buildings, away from ring plazas.</summary>
        private static void StreetLamps(Transform parent, ZoneGrid g, ZoneDefinition z, Material post, Material glow, HashSet<Vector2Int> stairCells)
        {
            if (z.lampDensity <= 0f) return;
            var rings = new List<Vector2Int>();
            foreach (char d in "123456789") rings.AddRange(g.Find(d));
            int count = 0;
            for (int y = 0; y < g.height && count < 150; y++)
                for (int x = 0; x < g.width && count < 150; x++)
                {
                    if (g.At(x, y) != '.' || g.Hash(x, y, 7) > z.lampDensity * 0.4f || stairCells.Contains(new Vector2Int(x, y))) continue;
                    bool nearRing = false;
                    foreach (var r in rings) if (Mathf.Abs(r.x - x) <= 2 && Mathf.Abs(r.y - y) <= 2) nearRing = true;
                    if (nearRing) continue;
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        if (g.At(x + d.x, y + d.y) != '#') continue;
                        Vector3 p = g.Local(x, y) + ZoneGrid.ToLocalDir(d) * (g.cell * 0.4f);
                        Deco(parent, PrimitiveType.Cylinder, "LampPost", p + new Vector3(0, 1.7f, 0), new Vector3(0.18f, 1.7f, 0.18f), post, Quaternion.identity, false);
                        Deco(parent, PrimitiveType.Cube, "LampGlow", p + new Vector3(0, 3.6f, 0), new Vector3(0.55f, 0.6f, 0.55f), glow, Quaternion.identity, false);
                        count++;
                        break;
                    }
                }
        }

        /// <summary>A clickable door with a label readable from labelRot's side (canvas +Z = the way the viewer looks).</summary>
        private static GameObject ZoneDoor(Transform parent, string name, Vector3 localPos, Quaternion doorRot, Quaternion labelRot, Material mat, string label)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = name;
            d.transform.SetParent(parent, false);
            d.transform.localPosition = localPos;
            d.transform.localRotation = doorRot;
            d.transform.localScale = new Vector3(3f, 5f, 0.3f);
            d.GetComponent<Renderer>().sharedMaterial = mat;
            WorldText(parent, name + "Label", localPos + new Vector3(0, 3.3f, 0), labelRot, new Vector2(1000, 220), 0.004f, label, 64).color = new Color(1f, 0.85f, 0.4f);
            return d;
        }

        // ------------------------------------------------------------------ NPCs and points

        private static void BuildNpc(Transform root, ZoneGrid g, ZoneDefinition z, ZoneNpc npc, QuestManager qm, WorldBuild wb)
        {
            var cells = g.Find(npc.Symbol);
            if (cells.Count == 0) return;
            if (wb.npcIds.ContainsKey(npc.key))
            {
                Debug.LogError("[WizardsVRC World] NPC key '" + npc.key + "' is used in two zones; the second is skipped.");
                return;
            }
            Vector3 pos = g.Local(cells[0]);
            Vector3 front;
            if (npc.facing < 0f) front = ZoneGrid.ToLocalDir(g.OpenDirection(cells[0]));
            else front = Quaternion.Euler(0, npc.facing, 0) * Vector3.forward;
            // An NPC painted inside a tavern's block keeps the bar: behind the counter, facing the room.
            Vector3 barSpot, barFront;
            if (TavernBarkeepSpot(g, z, cells[0], out barSpot, out barFront)) { pos = barSpot; front = barFront; }
            // The wizard model faces its local -Z, so turn it until -Z points along 'front'.
            float yaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
            int id = QuestManager.NPC_STREET_FIRST + wb.npcCutscenes.Count;

            Transform t = Wizard(root, "NPC_" + npc.key, pos, yaw, npc.robeColor, npc.hatColor, npc.beard, npc.displayName, "(click to talk)");
            var qn = UdonSharpUndo.AddComponent<QuestNpc>(t.gameObject);
            qn.questManager = qm;
            qn.npcId = id;
            qn.markerNew = MarkerTag(t, "QuestMarkerNew", new Vector3(0, 4.5f, 0), "!");
            qn.markerTalk = MarkerTag(t, "QuestMarkerTalk", new Vector3(0, 4.5f, 0), "?");
            if (qn.markerNew != null) qn.markerNew.SetActive(false);
            if (qn.markerTalk != null) qn.markerTalk.SetActive(false);
            qn.showFromQuest = npc.showFromQuest;
            qn.hideAfterQuest = npc.hideAfterQuest;
            if (npc.showFromQuest >= 0 || npc.hideAfterQuest >= 0)
            {
                var parts = new List<GameObject>();
                foreach (Transform child in t)
                    if (child.gameObject != qn.markerNew && child.gameObject != qn.markerTalk) parts.Add(child.gameObject);
                qn.parts = parts.ToArray();
                qn.clickCollider = t.GetComponent<Collider>();
            }
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(qn);
            if (backing != null)
            {
                backing.interactText = "Talk";
                // A little further than the default 2 m, so you can talk across a counter or a shop stall.
                backing.proximity = 3.5f;
            }

            Vector3 side = Vector3.Cross(Vector3.up, front);
            Vector3 face = pos + Vector3.up * 2f;
            var shots = new[]
            {
                Shot(root, "Shot_" + npc.key + "_Wide", pos + front * 7.5f + side * 0.5f + Vector3.up * 2.6f, face),
                Shot(root, "Shot_" + npc.key + "_Close", pos + front * 4.6f + side * 2.8f + Vector3.up * 2.05f, face),
            };
            var lines = npc.defaultDialogue != null && npc.defaultDialogue.Length > 0 ? npc.defaultDialogue : new[] { "..." };
            // Voice clips: the NPC's Voice Prefix, or by default Audio/Voice/npc_<key>_00.wav, _01...
            string npcVoice = string.IsNullOrEmpty(npc.voicePrefix) ? "npc_" + npc.key.ToLowerInvariant() : npc.voicePrefix;
            var cs = MakeCutscene(wb.cutsceneParent, z.zoneKey + "_" + npc.key, npc.displayName, lines, npcVoice, shots);
            cs.cutsceneId = z.zoneKey + "_" + npc.key;
            cs.onFinished = qm;
            cs.onFinishedEvent = nameof(QuestManager.OnStreetNpcFinished);

            wb.npcIds[npc.key] = id;
            wb.npcTargets[npc.key] = t;
            wb.npcShots[npc.key] = shots;
            wb.npcNames[npc.key] = npc.displayName;
            wb.npcCutscenes.Add(cs);
            wb.behaviours.Add(qn);
            wb.behaviours.Add(cs);
        }

        private static void BuildPoints(Transform root, ZoneGrid g, ZonePoint p, QuestManager qm, WorldBuild wb, Material glow, Material deadLamp, Material wood)
        {
            var cells = g.Find(p.Symbol);
            if (cells.Count == 0) return;
            int id;
            if (!wb.pointIds.TryGetValue(p.key, out id))
            {
                id = 101 + wb.pointIds.Count;
                wb.pointIds[p.key] = id;
                wb.pointLabels[p.key] = string.IsNullOrEmpty(p.label) ? p.key : p.label;
            }
            for (int n = 0; n < cells.Count; n++)
            {
                Transform holder = Child(root, "Point_" + p.key + n, g.Local(cells[n]));
                if (!wb.pointTargets.ContainsKey(p.key)) wb.pointTargets[p.key] = holder;
                GameObject click = null, used = null, hide = null;
                float labelY = 2.4f;
                switch (p.look)
                {
                    case ZonePointLook.StreetLamp:
                        Deco(holder, PrimitiveType.Cylinder, "Post", new Vector3(0, 2f, 0), new Vector3(0.25f, 2f, 0.25f), wood, Quaternion.identity, true);
                        click = Deco(holder, PrimitiveType.Cube, "LampHead", new Vector3(0, 4.3f, 0), new Vector3(0.8f, 0.9f, 0.8f), deadLamp, Quaternion.identity, true);
                        used = Deco(click.transform, PrimitiveType.Sphere, "Flame", Vector3.zero, Vector3.one * 0.9f, glow, Quaternion.identity, false);
                        used.SetActive(false);
                        labelY = 5.4f;
                        break;
                    case ZonePointLook.GlowingItem:
                        Deco(holder, PrimitiveType.Cylinder, "Pedestal", new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.3f, 0.7f), wood, Quaternion.identity, true);
                        click = Deco(holder, PrimitiveType.Cube, "Item", new Vector3(0, 1.05f, 0), Vector3.one * 0.5f, glow, Quaternion.Euler(45f, 0f, 45f), true);
                        hide = click;
                        break;
                    case ZonePointLook.Crate:
                        click = Deco(holder, PrimitiveType.Cube, "Crate", new Vector3(0, 0.6f, 0), Vector3.one * 1.2f, wood, Quaternion.Euler(0, 20f, 0), true);
                        break;
                    case ZonePointLook.Signpost:
                        Deco(holder, PrimitiveType.Cylinder, "Post", new Vector3(0, 1.1f, 0), new Vector3(0.18f, 1.1f, 0.18f), wood, Quaternion.identity, true);
                        click = Deco(holder, PrimitiveType.Cube, "Board", new Vector3(0, 2f, 0), new Vector3(1.6f, 0.6f, 0.1f), wood, Quaternion.identity, true);
                        labelY = 2.9f;
                        break;
                }
                if (!string.IsNullOrEmpty(p.label))
                    WorldText(holder, "Label", new Vector3(0, labelY, 0), Quaternion.identity, new Vector2(800, 140), 0.004f, p.label, 48).color = new Color(1f, 0.9f, 0.6f);
                if (p.kind == ZonePointKind.Decoration) continue;

                QuestTarget qt;
                if (p.kind == ZonePointKind.Explore)
                {
                    var trig = Child(holder, "Trigger", new Vector3(0, 1.5f, 0));
                    // Ignore Raycast: still a trigger you walk into, but the pointer passes through it (no hover
                    // highlight, and it can't block a panel's buttons).
                    trig.gameObject.layer = 2;
                    var box = trig.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    float size = g.cell * Mathf.Max(0.25f, p.triggerSizeCells);
                    box.size = new Vector3(size, 3f, size);
                    qt = UdonSharpUndo.AddComponent<QuestTarget>(trig.gameObject);
                    qt.isTrigger = true;
                }
                else
                {
                    if (click == null)
                    {
                        click = Child(holder, "ClickArea", new Vector3(0, 1f, 0)).gameObject;
                        click.AddComponent<BoxCollider>().size = new Vector3(1.5f, 2f, 1.5f);
                    }
                    qt = UdonSharpUndo.AddComponent<QuestTarget>(click);
                    qt.usedVisual = used;
                    qt.hideWhenUsed = hide;
                    UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(qt);
                    if (backing != null) backing.interactText = string.IsNullOrEmpty(p.label) ? "Use" : p.label;
                }
                qt.questManager = qm;
                qt.targetId = id;
                wb.behaviours.Add(qt);
            }
        }

        // ------------------------------------------------------------------ encounters

        private static void BuildZoneEncounter(Transform root, ZoneGrid g, ZoneDefinition z, ZoneEncounter e, Vector2Int start, Transform spawn,
            Transform systems, CardDatabase cardDb, ItemDatabase itemDb, PlayerState playerState, QuestManager qm, List<BattleClient> encounters, WorldBuild wb)
        {
            var spots = g.Find(e.Symbol);
            if (spots.Count == 0 || e.roster == null || e.roster.Count == 0) return;
            var roster = new List<int>();
            foreach (int id in e.roster) if (id > 0 && roster.Count < BattleManager.SIDE) roster.Add(id);
            if (roster.Count == 0) return;
            string name = z.zoneKey + "_" + e.key;

            var client = MakeEncounter(systems, name, cardDb, itemDb, playerState, spawn, roster.ToArray(), true, encounters);
            client.questManager = qm;
            client.manager.scaleRosterWithParty = e.scaleWithParty;
            // Even fights: one monster per wizard, patrol monster w is roster[w % roster size].
            client.manager.evenFight = e.evenFight;
            client.manager.wandererMonsters = new int[Mathf.Max(0, e.wanderers)];
            for (int w = 0; w < e.wanderers; w++) client.manager.wandererMonsters[w] = roster[w % roster.Count];
            client.manager.respawnSeconds = Mathf.Max(5, e.respawnSeconds);

            // Ring spots: the fight forms at the one nearest the wizard who touched a monster. Monsters take the side away
            // from the street entrance, so wizards face deeper into the street.
            Transform spotsRoot = Child(root, "RingSpots_" + e.key, Vector3.zero);
            var ringSpots = new Transform[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                Vector3 p = g.Local(spots[i]);
                Vector3 away = p - g.Local(start);
                away.y = 0f;
                Transform t = Child(spotsRoot, "Spot" + i, p);
                t.localRotation = away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away) : Quaternion.identity;
                ringSpots[i] = t;
            }
            client.manager.ringSpots = ringSpots;

            BuildEncounter(root, ringSpots[0].localPosition, name, client, 0);
            Transform encRoot = root.Find("Encounter" + name);
            if (encRoot != null) encRoot.localRotation = ringSpots[0].localRotation;
            client.encounterRoot = encRoot;
            // Like Wizard101, you stay where you are after a fight; defeat sends you back to the entrance.
            client.hubReturn = null;
            client.defeatReturn = spawn;
            // In a dungeon, defeat sends you back to the dungeon's start, not out of it.
            if (_dungeonStart != null && _dungeonCells != null && _dungeonCells.Contains(spots[0])) client.defeatReturn = _dungeonStart;

            // Patrol: walkers follow the route of the matching letter, spread evenly along it.
            Transform guideTarget = ringSpots[0];
            if (e.wanderers > 0)
            {
                bool loop, ok;
                var route = g.PatrolRoute(e.PatrolSymbol, out loop, out ok);
                Transform patrol = Child(root, "Patrol_" + e.key, Vector3.zero);
                if (route != null && route.Count >= 2)
                {
                    var corners = ZoneGrid.Corners(route, loop);
                    var path = new Transform[corners.Count];
                    float length = 0f;
                    for (int i = 0; i < corners.Count; i++)
                    {
                        path[i] = Child(patrol, "Waypoint" + i, g.Local(corners[i]));
                        if (i > 0) length += Vector3.Distance(path[i - 1].localPosition, path[i].localPosition);
                    }
                    if (loop) length += Vector3.Distance(path[corners.Count - 1].localPosition, path[0].localPosition);
                    float span = loop ? length : length * 2f;
                    for (int w = 0; w < e.wanderers; w++)
                    {
                        var wd = MakeWanderer(patrol, client, cardDb, roster[w % roster.Count], "Wanderer" + w);
                        wd.wandererIndex = w;
                        wd.contactRadius = DuelCircleRadius + 8f;
                        wd.path = path;
                        wd.loop = loop;
                        wd.speed = Mathf.Max(0.2f, e.patrolSpeed);
                        wd.phaseOffset = span * w / e.wanderers;
                        wb.behaviours.Add(wd);
                    }
                    guideTarget = path[0];
                }
                else
                {
                    for (int w = 0; w < e.wanderers; w++)
                    {
                        var wd = MakeWanderer(patrol, client, cardDb, roster[w % roster.Count], "Wanderer" + w);
                        wd.wandererIndex = w;
                        wd.contactRadius = DuelCircleRadius + 8f;
                        wd.center = client.arenaCenter;
                        wd.radius = DuelCircleRadius + 3f;
                        wd.speed = Mathf.Max(0.2f, e.patrolSpeed);
                        wd.phaseOffset = w * Mathf.PI * 2f / e.wanderers;
                        wb.behaviours.Add(wd);
                    }
                }
            }
            foreach (int id in roster) if (!wb.monsterTargets.ContainsKey(id)) wb.monsterTargets[id] = guideTarget;
        }

        private static Wanderer MakeWanderer(Transform parent, BattleClient client, CardDatabase db, int monsterId, string name)
        {
            float ms = db.MonsterBodyScale(monsterId);
            Transform wt = Child(parent, name, Vector3.zero);
            var wanderer = UdonSharpUndo.AddComponent<Wanderer>(wt.gameObject);
            wanderer.body = MonsterBody(wt, db, monsterId, "Body");
            wanderer.label = WorldText(wt, "Label", new Vector3(0, ms * 2f + 0.6f, 0), Quaternion.identity, new Vector2(500, 140), 0.004f, "", 34);
            wanderer.manager = client.manager;
            wanderer.client = client;
            wanderer.cardDatabase = db;
            wanderer.monsterId = monsterId;
            wanderer.aggroRadius = 3f;
            return wanderer;
        }

        // ================================================================== quests

        /// <summary>
        /// Appends every Questline's quests to the QuestManager tables at their save ids (gaps become empty slots that never
        /// start), resolving NPC / monster / point references, guide targets and dialogue cutscenes.
        /// </summary>
        private static void ApplyQuestlines(QuestManager qm, WorldBuild wb, ItemDatabase itemDb)
        {
            var quests = new List<QuestEntry>();
            var seen = new HashSet<int>();
            foreach (var ql in wb.data.questlines)
            {
                if (ql == null || !ql.includeInBuild) continue;
                foreach (var q in ql.quests)
                {
                    if (q == null || q.saveId < 2 || q.saveId >= CharacterData.QUEST_SLOTS) continue;
                    if (!seen.Add(q.saveId)) { Debug.LogError("[WizardsVRC World] Quest save id " + q.saveId + " is used twice; '" + q.title + "' skipped."); continue; }
                    quests.Add(q);
                }
            }
            int max = qm.questTitles.Length - 1;
            foreach (var q in quests) max = Mathf.Max(max, q.saveId);
            while (qm.questTitles.Length <= max) AppendQuestSlot(qm);
            int MS = QuestManager.MAX_STEPS;
            var stepCs = new Cutscene[qm.questTitles.Length * MS];
            var offerCs = new Cutscene[qm.questTitles.Length];

            foreach (var q in quests)
            {
                int id = q.saveId;
                qm.questTitles[id] = q.title;
                qm.questDescriptions[id] = q.description;
                string reward = q.xp + " XP, " + q.gold + " gold";
                if (q.rewardItem > 0 && itemDb != null) reward += ", " + itemDb.GetName(q.rewardItem);
                if (q.rewardSpell != 0) reward += ", a new spell";
                qm.questRewards[id] = reward;
                qm.questXp[id] = q.xp;
                qm.questGold[id] = q.gold;
                qm.questItems[id] = q.rewardItem;
                qm.questSpells[id] = q.rewardSpell;
                qm.questPrereqs[id] = q.prerequisite;
                qm.questMinLevels[id] = Mathf.Max(1, q.minLevel);
                qm.questAutoStart[id] = q.startsAutomatically ? 1 : 0;
                qm.questGivers[id] = q.startsAutomatically ? -1 : NpcId(q.giverNpc, wb);
                qm.questStepCounts[id] = 0;

                if (!q.startsAutomatically && HasLines(q.offerDialogue))
                    offerCs[id] = QuestDialogue(qm, wb, "Quest" + id + "_Offer", q.giverNpc, q.offerDialogue,
                        string.IsNullOrEmpty(q.offerVoicePrefix) ? "q" + id + "_offer" : q.offerVoicePrefix);

                for (int s = 0; s < q.steps.Count && s < MS; s++)
                {
                    var st = q.steps[s];
                    int type, param = 0;
                    Transform target = null;
                    string label = st.guideLabel;
                    switch (st.kind)
                    {
                        case QuestStepKind.Talk:
                            type = QuestManager.STEP_TALK;
                            param = NpcId(st.npc, wb);
                            wb.npcTargets.TryGetValue(st.npc ?? "", out target);
                            if (string.IsNullOrEmpty(label)) { string nn; label = wb.npcNames.TryGetValue(st.npc ?? "", out nn) ? nn : st.npc; }
                            // Voice clips: the step's Voice Prefix, or by default Audio/Voice/q<quest>_s<step>_00.wav, _01...
                            if (HasLines(st.dialogue)) stepCs[id * MS + s] = QuestDialogue(qm, wb, "Quest" + id + "_Step" + s, st.npc, st.dialogue,
                                string.IsNullOrEmpty(st.voicePrefix) ? "q" + id + "_s" + s : st.voicePrefix);
                            break;
                        case QuestStepKind.Interact:
                        case QuestStepKind.Explore:
                            type = st.kind == QuestStepKind.Interact ? QuestManager.STEP_INTERACT : QuestManager.STEP_EXPLORE;
                            if (!wb.pointIds.TryGetValue(st.point ?? "", out param)) Debug.LogError("[WizardsVRC World] Quest " + id + ": no zone point '" + st.point + "'.");
                            wb.pointTargets.TryGetValue(st.point ?? "", out target);
                            if (string.IsNullOrEmpty(label)) { string pl; label = wb.pointLabels.TryGetValue(st.point ?? "", out pl) ? pl : st.point; }
                            break;
                        default:
                            type = st.kind == QuestStepKind.Defeat ? QuestManager.STEP_DEFEAT : (st.kind == QuestStepKind.Collect ? QuestManager.STEP_COLLECT : QuestManager.STEP_BOSS);
                            param = st.monsterId;
                            wb.monsterTargets.TryGetValue(st.monsterId, out target);
                            if (string.IsNullOrEmpty(label) && wb.data.monsters != null)
                            {
                                var m = wb.data.monsters.Find(st.monsterId);
                                label = m != null ? m.name : "Monster " + st.monsterId;
                            }
                            break;
                    }
                    SetStep(qm, id, s, type, param, Mathf.Clamp(st.count, 1, 255), st.objective ?? "", label ?? "", target);
                }
            }
            qm.stepCutscenes = stepCs;
            qm.questOfferCutscenes = offerCs;
            ApplyWorlds(qm, wb, quests);
        }

        /// <summary>
        /// Extra requirements (QuestEntry.alsoRequires) and the worlds: each world's finale needs the end of every one of
        /// its storylines, and the quest log shows the current world's goal and how many storylines are finished.
        /// </summary>
        private static void ApplyWorlds(QuestManager qm, WorldBuild wb, List<QuestEntry> quests)
        {
            int n = qm.questTitles.Length;
            var reqs = new List<int>[n];
            System.Action<int, int> require = (q, r) =>
            {
                if (q < 0 || q >= n || r < 0 || r == q) return;
                if (reqs[q] == null) reqs[q] = new List<int>();
                if (!reqs[q].Contains(r)) reqs[q].Add(r);
            };
            foreach (var q in quests)
                if (q.alsoRequires != null) foreach (int r in q.alsoRequires) require(q.saveId, r);

            var names = new List<string>();
            var goals = new List<string>();
            var levels = new List<string>();
            var goalQuests = new List<int>();
            var finales = new List<int>();
            var slStart = new List<int>();
            var slCount = new List<int>();
            var ends = new List<int>();
            var endNames = new List<string>();
            foreach (var w in wb.data.worlds)
            {
                if (w == null || !w.includeInBuild) continue;
                names.Add(w.displayName);
                goals.Add(w.goal ?? "");
                levels.Add(w.levelRange ?? "");
                goalQuests.Add(w.goalQuest);
                finales.Add(w.finaleQuest);
                slStart.Add(ends.Count);
                int count = 0;
                foreach (var sl in w.storylines)
                {
                    if (sl == null || !sl.includeInBuild) continue;
                    int end = sl.StorylineEnd();
                    if (end < 0 || end == w.finaleQuest) continue;
                    ends.Add(end);
                    endNames.Add(sl.StorylineName());
                    count++;
                    require(w.finaleQuest, end);
                }
                slCount.Add(count);
                if (count < WorldArc.MinStorylines)
                    Debug.Log("[WizardsVRC World] " + w.displayName + " has " + count + " storylines (worlds are meant to have at least " + WorldArc.MinStorylines + " before players move on).");
            }
            qm.worldNames = names.ToArray();
            qm.worldGoals = goals.ToArray();
            qm.worldLevelRanges = levels.ToArray();
            qm.worldGoalQuests = goalQuests.ToArray();
            qm.worldFinales = finales.ToArray();
            qm.worldStorylineStart = slStart.ToArray();
            qm.worldStorylineCount = slCount.ToArray();
            qm.storylineEnds = ends.ToArray();
            qm.storylineNames = endNames.ToArray();

            var start = new int[n];
            var cnt = new int[n];
            var flat = new List<int>();
            for (int q = 0; q < n; q++)
            {
                start[q] = flat.Count;
                if (reqs[q] != null) flat.AddRange(reqs[q]);
                cnt[q] = flat.Count - start[q];
            }
            qm.questRequireStart = start;
            qm.questRequireCount = cnt;
            qm.requireIds = flat.ToArray();
        }

        private static bool HasLines(string[] lines)
        {
            if (lines == null) return false;
            foreach (var l in lines) if (!string.IsNullOrEmpty(l)) return true;
            return false;
        }

        private static Cutscene QuestDialogue(QuestManager qm, WorldBuild wb, string name, string npcKey, string[] lines, string voicePrefix)
        {
            var clean = new List<string>();
            foreach (var l in lines) if (!string.IsNullOrEmpty(l)) clean.Add(l);
            string speaker;
            if (!wb.npcNames.TryGetValue(npcKey ?? "", out speaker)) speaker = npcKey;
            Transform[] shots;
            wb.npcShots.TryGetValue(npcKey ?? "", out shots);
            if (shots == null) Debug.LogWarning("[WizardsVRC World] Dialogue '" + name + "' is for '" + npcKey + "', which isn't a zone NPC; it only plays for zone NPCs.");
            var cs = MakeCutscene(wb.cutsceneParent, name, speaker, clean.ToArray(), voicePrefix ?? "", shots ?? new Transform[0]);
            cs.cutsceneId = name;
            cs.onFinished = qm;
            cs.onFinishedEvent = nameof(QuestManager.OnStreetNpcFinished);
            wb.behaviours.Add(cs);
            return cs;
        }

        /// <summary>Zone NPC key, Headmaster, OwnProfessor or a school name to an NPC id (-1 if unknown).</summary>
        private static int NpcId(string key, WorldBuild wb)
        {
            int builtIn = WorldValidator.BuiltInNpc(key);
            if (builtIn != int.MinValue) return builtIn;
            int id;
            if (key != null && wb.npcIds.TryGetValue(key, out id)) return id;
            if (!string.IsNullOrEmpty(key)) Debug.LogError("[WizardsVRC World] Unknown NPC '" + key + "'.");
            return -1;
        }

        /// <summary>An empty quest slot (never starts) so authored quests can sit at their save ids.</summary>
        private static void AppendQuestSlot(QuestManager qm)
        {
            int ms = QuestManager.MAX_STEPS;
            qm.questTitles = Grow(qm.questTitles, "");
            qm.questDescriptions = Grow(qm.questDescriptions, "");
            qm.questRewards = Grow(qm.questRewards, "");
            qm.questXp = Grow(qm.questXp, 0);
            qm.questGold = Grow(qm.questGold, 0);
            qm.questItems = Grow(qm.questItems, 0);
            qm.questSpells = Grow(qm.questSpells, 0);
            qm.questPrereqs = Grow(qm.questPrereqs, -1);
            qm.questMinLevels = Grow(qm.questMinLevels, CharacterData.MAX_LEVEL + 1);
            qm.questAutoStart = Grow(qm.questAutoStart, 0);
            qm.questGivers = Grow(qm.questGivers, -1);
            qm.questStepCounts = Grow(qm.questStepCounts, 0);
            for (int k = 0; k < ms; k++)
            {
                qm.stepTypes = Grow(qm.stepTypes, 0);
                qm.stepParams = Grow(qm.stepParams, 0);
                qm.stepCounts = Grow(qm.stepCounts, 1);
                qm.stepTexts = Grow(qm.stepTexts, "");
                qm.stepTargetNames = Grow(qm.stepTargetNames, "");
                qm.stepTargets = Grow(qm.stepTargets, (Transform)null);
            }
        }

        private static T[] Grow<T>(T[] a, T v)
        {
            var r = new T[(a == null ? 0 : a.Length) + 1];
            if (a != null) System.Array.Copy(a, r, a.Length);
            r[r.Length - 1] = v;
            return r;
        }

        // ================================================================== guide areas

        private static void ApplyGuideAreas(QuestGuide guide, WorldBuild wb, Transform interiorExit, Transform keepEntrance, bool outdoorSchools)
        {
            var min = new List<float> { InteriorOrigin.y * 0.5f };
            var max = new List<float> { InteriorOrigin.y + 100f };
            var into = new List<Transform> { keepEntrance };
            var outof = new List<Transform> { interiorExit };
            var inNames = new List<string> { outdoorSchools ? "Headmaster's Tower door" : "Keep door (Hall of Schools)" };
            var outNames = new List<string> { outdoorSchools ? "Door to the castle grounds" : "Door to the Courtyard" };
            min.AddRange(wb.areaMin);
            max.AddRange(wb.areaMax);
            into.AddRange(wb.areaIn);
            outof.AddRange(wb.areaOut);
            inNames.AddRange(wb.areaInNames);
            outNames.AddRange(wb.areaOutNames);
            guide.areaMinY = min.ToArray();
            guide.areaMaxY = max.ToArray();
            guide.areaEntrances = into.ToArray();
            guide.areaExits = outof.ToArray();
            guide.areaEntranceNames = inNames.ToArray();
            guide.areaExitNames = outNames.ToArray();
        }
    }
}
