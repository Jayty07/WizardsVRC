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

        /// <summary>What the zone pass hands to the quest pass and the guide.</summary>
        private class WorldBuild
        {
            public WorldDataStore.Data data;
            public Transform cutsceneParent;
            public readonly List<UdonSharpBehaviour> behaviours = new List<UdonSharpBehaviour>();
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
            db.monsterHps = new int[n];
            db.monsterSpells = new int[n * ms];
            db.monsterXps = new int[n];
            db.monsterGoldMin = new int[n];
            db.monsterGoldMax = new int[n];
            db.monsterDropItems = new int[n];
            db.monsterDropChances = new int[n];
            db.monsterBodyScales = new float[n];
            db.monsterIsBoss = new int[n];
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
                db.monsterHps[i] = Mathf.Max(1, m.health);
                for (int k = 0; k < ms; k++) db.monsterSpells[i * ms + k] = k < m.spells.Count ? m.spells[k] : 0;
                db.monsterXps[i] = m.xp;
                db.monsterGoldMin[i] = Mathf.Min(m.goldMin, m.goldMax);
                db.monsterGoldMax[i] = Mathf.Max(m.goldMin, m.goldMax);
                db.monsterDropItems[i] = m.dropItem;
                db.monsterDropChances[i] = Mathf.Clamp(m.dropChance, 0, 100);
                db.monsterBodyScales[i] = Mathf.Max(0.2f, m.bodyScale);
                db.monsterIsBoss[i] = m.isBoss ? 1 : 0;
                db.monsterHpPerExtraPlayer[i] = m.healthPerExtraWizard;
                db.monsterColors[i] = m.bodyColor;
                _monsterMats[i] = PMat("Monster_" + m.id, m.bodyColor, 1f);
            }
        }

        private static Material MonsterMat(CardDatabase db, int monsterId)
        {
            int i = db.MonsterIndexOf(monsterId);
            return _monsterMats != null && i >= 0 && i < _monsterMats.Length ? _monsterMats[i] : null;
        }

        // ================================================================== zones

        private static void BuildZones(Transform systems, Transform hubRoot, QuestManager qm, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, List<BattleClient> encounters, WorldBuild wb)
        {
            foreach (var z in wb.data.zones)
                if (z != null && z.includeInBuild) BuildZone(z, systems, hubRoot, qm, cardDb, itemDb, playerState, encounters, wb);
            qm.streetNpcCutscenes = wb.npcCutscenes.ToArray();
        }

        private static void BuildZone(ZoneDefinition z, Transform systems, Transform hubRoot, QuestManager qm, CardDatabase cardDb, ItemDatabase itemDb,
            PlayerState playerState, List<BattleClient> encounters, WorldBuild wb)
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
            float cs = g.cell;

            Material ground = PMat(k + "_Ground", z.groundColor, 8f);
            Material road = PMat(k + "_Road", z.roadColor, 3f);
            Material grass = PMat(k + "_Grass", z.grassColor, 3f);
            Material bld = PMat(k + "_Building", z.buildingColor, 2f);
            Material roof = PMat(k + "_Roof", z.roofColor, 2f);
            Material low = PMat(k + "_LowWall", z.lowWallColor, 2f);
            Material leaves = PMat(k + "_Tree", z.treeColor, 1f);
            Material trunk = PMat(k + "_Trunk", new Color(0.3f, 0.2f, 0.13f), 1f);
            Material glow = PMat(k + "_Glow", z.accentColor, 1f, z.accentColor);
            Material window = PMat(k + "_Window", z.accentColor * 0.8f, 1f, z.accentColor * 0.6f);
            Material deadLamp = PMat(k + "_DeadLamp", new Color(0.12f, 0.12f, 0.14f), 1f);
            Material wood = PMat(k + "_Wood", new Color(0.32f, 0.21f, 0.14f), 2f);
            Material door = PMat(k + "_Door", new Color(0.35f, 0.2f, 0.12f), 2f);

            // ---- ground, boundary and the map layers
            float W = g.width * cs, H = g.height * cs;
            Block(root, "Ground", new Vector3(0, -0.25f, 0), new Vector3(W, 0.5f, H), ground);
            Transform bounds = Child(root, "Bounds", Vector3.zero);
            foreach (var side in new[] { new Vector4(0, H / 2f, W, 1f), new Vector4(0, -H / 2f, W, 1f), new Vector4(W / 2f, 0, 1f, H), new Vector4(-W / 2f, 0, 1f, H) })
            {
                var wall = Child(bounds, "Edge", new Vector3(side.x, 15f, side.y)).gameObject.AddComponent<BoxCollider>();
                wall.size = new Vector3(side.z, 30f, side.w);
            }

            Transform layout = Child(root, "Layout", Vector3.zero);
            foreach (var r in Rects(g, ch => !ZoneGrid.IsSolid(ch) && ch != ',', 6, 6)) Slab(layout, "Road", g, r, 0.03f, road);
            foreach (var r in Rects(g, ch => ch == ',', 6, 6)) Slab(layout, "Grass", g, r, 0.04f, grass);
            int houseIndex = 0;
            foreach (var r in Rects(g, ch => ch == '#', 3, 3)) House(layout, g, z, r, houseIndex++, bld, roof, window);
            foreach (var r in Rects(g, ch => ch == '=', 6, 6))
            {
                Vector3 c = RectCenter(g, r);
                Block(layout, "LowWall", c + new Vector3(0, 0.7f, 0), new Vector3(r.width * cs, 1.4f, r.height * cs), low);
                Block(layout, "LowWallCap", c + new Vector3(0, 1.45f, 0), new Vector3(r.width * cs + 0.3f, 0.12f, r.height * cs + 0.3f), roof);
            }
            foreach (var t in g.Find('^')) Tree(layout, g, t, trunk, leaves);
            StreetLamps(layout, g, z, wood, glow);

            // ---- entrance: spawn, exit door, and the door in the castle
            Vector2Int start = starts[0];
            Vector2Int open = g.OpenDirection(start);
            Vector3 openDir = ZoneGrid.ToLocalDir(open);
            Transform spawn = Child(root, "Spawn", g.Local(start) + new Vector3(0, 0.05f, 0));
            spawn.localRotation = Quaternion.LookRotation(openDir);
            Transform hubReturn = Child(hubRoot, k + "Return", z.hubReturnPosition);
            hubReturn.localRotation = Quaternion.Euler(0, z.hubReturnYaw, 0);

            var outDoor = ZoneDoor(root, "ExitDoor", g.Local(start) - openDir * (cs * 0.45f) + new Vector3(0, 2.5f, 0), Quaternion.LookRotation(openDir),
                Quaternion.LookRotation(-openDir), door, "Starfall Castle\n(click door)");
            var toHub = UdonSharpUndo.AddComponent<TeleportHelper>(outDoor);
            toHub.defaultTarget = hubReturn;
            toHub.hideZones = new[] { root.gameObject };
            wb.behaviours.Add(toHub);

            Quaternion hubRot = Quaternion.Euler(0, z.hubDoorYaw, 0);
            var inDoor = ZoneDoor(hubRoot, k + "Door", z.hubDoorPosition, hubRot, hubRot, door, z.displayName + "\n(click door)  " + z.levelRange);
            var toZone = UdonSharpUndo.AddComponent<TeleportHelper>(inDoor);
            toZone.defaultTarget = spawn;
            toZone.showZones = new[] { root.gameObject };
            wb.behaviours.Add(toZone);

            wb.areaMin.Add(z.height - 50f);
            wb.areaMax.Add(z.height + 60f);
            wb.areaIn.Add(inDoor.transform);
            wb.areaOut.Add(outDoor.transform);
            wb.areaInNames.Add(z.displayName + " door (castle)");
            wb.areaOutNames.Add("Door back to Starfall Castle");

            // ---- NPCs, points, encounters
            foreach (var npc in z.npcs) if (npc != null) BuildNpc(root, g, z, npc, qm, wb);
            foreach (var p in z.points) if (p != null) BuildPoints(root, g, p, qm, wb, glow, deadLamp, wood);
            foreach (var e in z.encounters)
                if (e != null && ZoneGrid.IsRingDigit(e.Symbol))
                    BuildZoneEncounter(root, g, z, e, start, spawn, systems, cardDb, itemDb, playerState, qm, encounters, wb);

            root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ layout pieces

        /// <summary>Greedy rectangles of matching cells (fewer, bigger blocks), each at most maxW x maxH cells.</summary>
        private static List<RectInt> Rects(ZoneGrid g, System.Func<char, bool> match, int maxW, int maxH)
        {
            var used = new bool[g.width, g.height];
            var list = new List<RectInt>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    if (used[x, y] || !match(g.At(x, y))) continue;
                    int w = 1;
                    while (w < maxW && x + w < g.width && !used[x + w, y] && match(g.At(x + w, y))) w++;
                    int h = 1;
                    while (h < maxH && y + h < g.height)
                    {
                        bool rowOk = true;
                        for (int i = 0; i < w; i++) if (used[x + i, y + h] || !match(g.At(x + i, y + h))) { rowOk = false; break; }
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
        private static void StreetLamps(Transform parent, ZoneGrid g, ZoneDefinition z, Material post, Material glow)
        {
            if (z.lampDensity <= 0f) return;
            var rings = new List<Vector2Int>();
            foreach (char d in "123456789") rings.AddRange(g.Find(d));
            int count = 0;
            for (int y = 0; y < g.height && count < 150; y++)
                for (int x = 0; x < g.width && count < 150; x++)
                {
                    if (g.At(x, y) != '.' || g.Hash(x, y, 7) > z.lampDensity * 0.4f) continue;
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
            // The wizard model faces its local -Z, so turn it until -Z points along 'front'.
            float yaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
            int id = QuestManager.NPC_STREET_FIRST + wb.npcCutscenes.Count;

            Transform t = Wizard(root, "NPC_" + npc.key, pos, yaw, npc.robeColor, npc.hatColor, npc.beard, npc.displayName);
            Transform hint = t.Find("TalkHint");
            if (hint != null && hint.GetComponent<Text>() != null) hint.GetComponent<Text>().text = "(click to talk)";
            var qn = UdonSharpUndo.AddComponent<QuestNpc>(t.gameObject);
            qn.questManager = qm;
            qn.npcId = id;
            qn.marker = WorldText(t, "QuestMarker", new Vector3(0, 4.4f, 0), Quaternion.identity, new Vector2(300, 300), 0.008f, "", 220);
            qn.marker.color = new Color(1f, 0.85f, 0.2f);
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(qn);
            if (backing != null) backing.interactText = "Talk";

            Vector3 side = Vector3.Cross(Vector3.up, front);
            Vector3 face = pos + Vector3.up * 2f;
            var shots = new[]
            {
                Shot(root, "Shot_" + npc.key + "_Wide", pos + front * 7.5f + side * 0.5f + Vector3.up * 2.6f, face),
                Shot(root, "Shot_" + npc.key + "_Close", pos + front * 4.6f + side * 2.8f + Vector3.up * 2.05f, face),
            };
            var lines = npc.defaultDialogue != null && npc.defaultDialogue.Length > 0 ? npc.defaultDialogue : new[] { "..." };
            var cs = MakeCutscene(wb.cutsceneParent, z.zoneKey + "_" + npc.key, npc.displayName, lines, npc.voicePrefix ?? "", shots);
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
            int sc = db.MonsterSchool(monsterId);
            Transform wt = Child(parent, name, Vector3.zero);
            var wanderer = UdonSharpUndo.AddComponent<Wanderer>(wt.gameObject);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(wt, false);
            body.transform.localPosition = new Vector3(0, ms, 0);
            body.transform.localScale = Vector3.one * ms;
            body.GetComponent<Renderer>().sharedMaterial = MonsterMat(db, monsterId) ?? client.fxMats[sc >= 0 && sc < SchoolLabels.Length ? sc : SchoolLabels.Length];
            Object.DestroyImmediate(body.GetComponent<Collider>());
            wanderer.body = body;
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
                    offerCs[id] = QuestDialogue(qm, wb, "Quest" + id + "_Offer", q.giverNpc, q.offerDialogue, q.offerVoicePrefix);

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
                            if (HasLines(st.dialogue)) stepCs[id * MS + s] = QuestDialogue(qm, wb, "Quest" + id + "_Step" + s, st.npc, st.dialogue, st.voicePrefix);
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

        private static void ApplyGuideAreas(QuestGuide guide, WorldBuild wb, Transform interiorExit, Transform keepEntrance)
        {
            var min = new List<float> { InteriorOrigin.y * 0.5f };
            var max = new List<float> { InteriorOrigin.y + 100f };
            var into = new List<Transform> { keepEntrance };
            var outof = new List<Transform> { interiorExit };
            var inNames = new List<string> { "Keep door (Hall of Schools)" };
            var outNames = new List<string> { "Door to the Courtyard" };
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
