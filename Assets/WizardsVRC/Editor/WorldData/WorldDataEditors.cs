using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    internal static class WorldEditorUI
    {
        private static GUIStyle _mono;
        private static GUIStyle _cellLabel;

        public static GUIStyle Mono
        {
            get
            {
                if (_mono == null)
                {
                    _mono = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
                    Font f = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New", "Monaco", "DejaVu Sans Mono" }, 13);
                    if (f != null) _mono.font = f;
                }
                return _mono;
            }
        }

        public static GUIStyle CellLabel
        {
            get
            {
                if (_cellLabel == null)
                {
                    _cellLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                    _cellLabel.normal.textColor = Color.black;
                }
                return _cellLabel;
            }
        }

        public static void Report(WorldValidator.Report r)
        {
            if (r == null) return;
            if (r.errors.Count == 0 && r.warnings.Count == 0) { EditorGUILayout.HelpBox("No problems found.", MessageType.Info); return; }
            foreach (var e in r.errors.Take(12)) EditorGUILayout.HelpBox(e, MessageType.Error);
            foreach (var w in r.warnings.Take(8)) EditorGUILayout.HelpBox(w, MessageType.Warning);
            if (r.errors.Count > 12 || r.warnings.Count > 8) EditorGUILayout.LabelField("(more in the Console after Validate World Data)", EditorStyles.miniLabel);
        }

        private static WorldDataStore.Data _data;
        private static double _dataTime;

        /// <summary>All world data, reloaded at most every couple of seconds (inspectors repaint often).</summary>
        public static WorldDataStore.Data Data()
        {
            if (_data == null || EditorApplication.timeSinceStartup - _dataTime > 2.0)
            {
                _data = WorldDataStore.Load();
                _dataTime = EditorApplication.timeSinceStartup;
            }
            return _data;
        }

        public static void Buttons()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate World Data")) WorldDataStore.ValidateMenu();
                if (GUILayout.Button("Rebuild MainWorld Scene")) EditorApplication.delayCall += WizardsVRCSceneBuilder.RebuildFromMenu;
            }
            EditorGUILayout.HelpBox("Edits are saved in " + WorldDataStore.Folder + ". Commit that folder (with the .meta files) to keep them. "
                                    + "Rebuild the scene to see changes in the world.", MessageType.None);
        }
    }

    // ====================================================================== zones

    [CustomEditor(typeof(ZoneDefinition))]
    public class ZoneDefinitionEditor : Editor
    {
        private static readonly string[] BaseBrushes = { "#", "=", "^", "~", ".", ",", "S", "1", "2", "3", "4", "5", "a", "b", "c", "d", "e" };
        private string _brush = ".";
        private WorldValidator.Report _report;
        private string _reportFor;
        private bool _showText;
        private bool _showHeights = true;

        public override void OnInspectorGUI()
        {
            var z = (ZoneDefinition)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "map", "heightMap", "npcs", "points", "encounters", "landmarks");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Map", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(ZoneDefinition.Legend, MessageType.None);
            DrawBrushes(z);
            DrawPreview(z);
            _showHeights = EditorGUILayout.ToggleLeft("Shade by height (lighter = higher; white edges = stairs, black = walls)", _showHeights);
            _showText = EditorGUILayout.Foldout(_showText, "Edit map and height map as text", true);
            if (_showText)
            {
                var mapProp = serializedObject.FindProperty("map");
                mapProp.stringValue = EditorGUILayout.TextArea(mapProp.stringValue, WorldEditorUI.Mono, GUILayout.MinHeight(200));
                EditorGUILayout.HelpBox(ZoneDefinition.HeightLegend, MessageType.None);
                var hProp = serializedObject.FindProperty("heightMap");
                hProp.stringValue = EditorGUILayout.TextArea(hProp.stringValue, WorldEditorUI.Mono, GUILayout.MinHeight(120));
                if (string.IsNullOrEmpty(hProp.stringValue) && GUILayout.Button("Start a flat height map (all 0)"))
                {
                    var rows = new List<string>();
                    foreach (string r in new ZoneGrid(z).rows) rows.Add(new string('0', r.Length));
                    hProp.stringValue = string.Join("\n", rows);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("npcs"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("points"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("encounters"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("landmarks"), new GUIContent("Landmarks (school halls, keep, gates, shops...)"), true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
            string key = JsonUtility.ToJson(z);
            if (_report == null || _reportFor != key)
            {
                _reportFor = key;
                _report = new WorldValidator.Report();
                WorldValidator.ValidateZone(z, AssetDatabase.LoadAssetAtPath<MonsterCatalog>(WorldDataStore.MonstersPath), _report);
            }
            WorldEditorUI.Report(_report);
            WorldEditorUI.Buttons();
        }

        private void DrawBrushes(ZoneDefinition z)
        {
            var brushes = new List<string>(BaseBrushes);
            foreach (var n in z.npcs) if (n != null && !string.IsNullOrEmpty(n.symbol) && !brushes.Contains(n.symbol)) brushes.Add(n.symbol);
            foreach (var p in z.points) if (p != null && !string.IsNullOrEmpty(p.symbol) && !brushes.Contains(p.symbol)) brushes.Add(p.symbol);
            if (z.landmarks != null)
                foreach (var l in z.landmarks) if (l != null && !string.IsNullOrEmpty(l.symbol) && !brushes.Contains(l.symbol)) brushes.Add(l.symbol);
            int sel = Mathf.Max(0, brushes.IndexOf(_brush));
            EditorGUILayout.LabelField("Paint brush (click or drag on the map)", EditorStyles.miniBoldLabel);
            sel = GUILayout.SelectionGrid(sel, brushes.ToArray(), 16, EditorStyles.miniButton);
            _brush = brushes[Mathf.Clamp(sel, 0, brushes.Count - 1)];
            using (new EditorGUILayout.HorizontalScope())
            {
                _brush = EditorGUILayout.TextField("Brush", _brush, GUILayout.Width(200));
                if (_brush.Length > 1) _brush = _brush.Substring(0, 1);
                if (GUILayout.Button("Add column", EditorStyles.miniButtonLeft)) Resize(z, 1, 0);
                if (GUILayout.Button("Remove column", EditorStyles.miniButtonMid)) Resize(z, -1, 0);
                if (GUILayout.Button("Add row", EditorStyles.miniButtonMid)) Resize(z, 0, 1);
                if (GUILayout.Button("Remove row", EditorStyles.miniButtonRight)) Resize(z, 0, -1);
            }
        }

        private void DrawPreview(ZoneDefinition z)
        {
            var g = new ZoneGrid(z);
            if (g.width == 0) return;
            float avail = EditorGUIUtility.currentViewWidth - 40f;
            float px = Mathf.Clamp(Mathf.Floor(avail / g.width), 6f, 18f);
            Rect area = GUILayoutUtility.GetRect(g.width * px, g.height * px, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, Color.black);
            var reach = g.Reachable();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    char c = g.At(x, y);
                    var r = new Rect(area.x + x * px, area.y + y * px, px - 1, px - 1);
                    Color col = g.IsVoid(x, y) ? new Color(0.1f, 0.18f, 0.35f) : CellColor(z, g, c);
                    if (_showHeights && g.HasHeights)
                    {
                        int lv = g.Level(x, y);
                        col = lv >= 0 ? Color.Lerp(col, Color.white, Mathf.Min(0.6f, lv * 0.14f)) : Color.Lerp(col, Color.black, Mathf.Min(0.6f, -lv * 0.18f));
                    }
                    if (g.Paved(x, y) && !reach.Contains(new Vector2Int(x, y)) && reach.Count > 0) col = Color.Lerp(col, Color.red, 0.5f);
                    EditorGUI.DrawRect(r, col);
                    if (_showHeights && g.HasHeights)
                    {
                        // Edges to the east and south: white where stairs join, black where a wall drops.
                        var here = new Vector2Int(x, y);
                        if (x + 1 < g.width && g.Level(x + 1, y) != g.Level(x, y))
                            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 2, px), g.HasStairs(here, new Vector2Int(x + 1, y)) ? Color.white : Color.black);
                        if (y + 1 < g.height && g.Level(x, y + 1) != g.Level(x, y))
                            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, px, 2), g.HasStairs(here, new Vector2Int(x, y + 1)) ? Color.white : Color.black);
                    }
                    bool marked = c == 'S' || ZoneGrid.IsRingDigit(c) || g.IsCustom(c) || (!ZoneGrid.IsBuiltIn(c) && !g.IsCustom(c));
                    if (marked && px >= 11f) GUI.Label(r, c.ToString(), WorldEditorUI.CellLabel);
                }
            EditorGUILayout.LabelField(g.width + " x " + g.height + " cells = " + (g.width * z.cellSize) + " m x " + (g.height * z.cellSize)
                                       + " m. North is up. Red tint = a path that can't be reached from S (lawns may be scenery).", EditorStyles.miniLabel);

            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && area.Contains(e.mousePosition))
            {
                int cx = (int)((e.mousePosition.x - area.x) / px), cy = (int)((e.mousePosition.y - area.y) / px);
                if (cx >= 0 && cy >= 0 && cx < g.width && cy < g.height && g.At(cx, cy) != _brush[0])
                {
                    Undo.RecordObject(z, "Paint zone map");
                    var rows = g.rows.ToArray();
                    char[] chars = rows[cy].ToCharArray();
                    chars[cx] = _brush.Length > 0 ? _brush[0] : '.';
                    rows[cy] = new string(chars);
                    z.map = string.Join("\n", rows);
                    EditorUtility.SetDirty(z);
                    Repaint();
                }
                e.Use();
            }
        }

        private static Color CellColor(ZoneDefinition z, ZoneGrid g, char c)
        {
            switch (c)
            {
                case '#': return Color.Lerp(z.buildingColor, Color.white, 0.15f);
                case '=': return Color.Lerp(z.lowWallColor, Color.white, 0.35f);
                case '^': return Color.Lerp(z.treeColor, Color.green, 0.35f);
                case '.': return Color.Lerp(z.roadColor, Color.white, 0.45f);
                case ',': return Color.Lerp(z.grassColor, Color.green, 0.3f);
                case 'S': return new Color(0.3f, 1f, 0.4f);
                case '~': return new Color(0.25f, 0.55f, 0.95f);
            }
            if (ZoneGrid.IsRingDigit(c)) return new Color(1f, 0.6f, 0.15f);
            if (ZoneGrid.IsPatrolLetter(c)) return new Color(0.95f, 0.85f, 0.35f);
            if (z.FindNpc(c) != null) return new Color(0.35f, 0.85f, 1f);
            if (z.FindPoint(c) != null) return new Color(0.95f, 0.45f, 0.95f);
            var l = z.FindLandmark(c);
            if (l != null)
            {
                if (l.kind == LandmarkKind.SchoolHall) return Color.Lerp(SchoolColor(l.school), Color.white, 0.2f);
                if (l.kind == LandmarkKind.Keep) return new Color(0.55f, 0.45f, 0.85f);
                if (l.kind == LandmarkKind.StreetGate) return new Color(0.3f, 0.95f, 0.85f);
                if (l.kind == LandmarkKind.HealthOrb) return new Color(1f, 0.25f, 0.25f);
                if (l.kind == LandmarkKind.ManaOrb) return new Color(0.3f, 0.45f, 1f);
                if (l.kind == LandmarkKind.DuelCircle) return new Color(0.95f, 0.5f, 0.55f);
                if (l.Solid) return new Color(0.75f, 0.65f, 0.5f);
                return new Color(1f, 0.95f, 0.6f);
            }
            return Color.red;
        }

        private static Color SchoolColor(School s)
        {
            switch (s)
            {
                case School.Fire: return new Color(0.85f, 0.3f, 0.12f);
                case School.Ice: return new Color(0.4f, 0.7f, 0.95f);
                case School.Storm: return new Color(0.6f, 0.35f, 0.85f);
                case School.Life: return new Color(0.35f, 0.8f, 0.35f);
                case School.Death: return new Color(0.35f, 0.35f, 0.38f);
                case School.Myth: return new Color(0.85f, 0.75f, 0.2f);
                default: return new Color(0.8f, 0.6f, 0.35f);
            }
        }

        private static void Resize(ZoneDefinition z, int dw, int dh)
        {
            var g = new ZoneGrid(z);
            var rows = new List<string>(g.rows);
            Undo.RecordObject(z, "Resize zone map");
            if (dw > 0) for (int i = 0; i < rows.Count; i++) rows[i] = rows[i] + "#";
            if (dw < 0 && g.width > 3) for (int i = 0; i < rows.Count; i++) rows[i] = rows[i].Substring(0, rows[i].Length - 1);
            if (dh > 0) rows.Add(new string('#', Mathf.Max(1, g.width)));
            if (dh < 0 && rows.Count > 3) rows.RemoveAt(rows.Count - 1);
            z.map = string.Join("\n", rows);
            // Keep the height map the same size (new cells at ground level).
            var hRows = new List<string>(z.HeightRows());
            if (hRows.Count > 0)
            {
                for (int i = 0; i < hRows.Count; i++)
                {
                    if (dw > 0) hRows[i] = hRows[i] + "0";
                    if (dw < 0 && hRows[i].Length > 3) hRows[i] = hRows[i].Substring(0, hRows[i].Length - 1);
                }
                if (dh > 0) hRows.Add(new string('0', Mathf.Max(1, g.width + dw)));
                if (dh < 0 && hRows.Count > 3) hRows.RemoveAt(hRows.Count - 1);
                z.heightMap = string.Join("\n", hRows);
            }
            EditorUtility.SetDirty(z);
        }
    }

    // ====================================================================== monsters

    [CustomEditor(typeof(MonsterCatalog))]
    public class MonsterCatalogEditor : Editor
    {
        private readonly Dictionary<int, bool> _open = new Dictionary<int, bool>();
        private Dictionary<int, string> _cards;
        private int[] _cardIds;
        private string[] _cardLabels;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            LoadCards();
            var list = serializedObject.FindProperty("monsters");
            if (_cards == null)
                EditorGUILayout.HelpBox("Open Assets/Scenes/MainWorld.unity to pick spells by name (the spell list comes from its CardDatabase).", MessageType.Info);

            for (int i = 0; i < list.arraySize; i++)
            {
                var m = list.GetArrayElementAtIndex(i);
                int id = m.FindPropertyRelative("id").intValue;
                string title = id + "  " + m.FindPropertyRelative("name").stringValue + "  (" +
                               ((School)m.FindPropertyRelative("school").enumValueIndex) + ", Lv " + m.FindPropertyRelative("level").intValue +
                               (m.FindPropertyRelative("isBoss").boolValue ? ", boss" : m.FindPropertyRelative("isElite").boolValue ? ", elite" : "") + ")";
                bool open;
                _open.TryGetValue(i, out open);
                using (new EditorGUILayout.HorizontalScope())
                {
                    open = EditorGUILayout.Foldout(open, title, true);
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22)) &&
                        EditorUtility.DisplayDialog("Delete monster", "Delete " + title + "?", "Delete", "Cancel"))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
                _open[i] = open;
                if (!open) continue;
                EditorGUI.indentLevel++;
                var it = m.Copy();
                var end = m.GetEndProperty();
                it.NextVisible(true);
                while (!SerializedProperty.EqualContents(it, end))
                {
                    if (it.name == "spells") DrawSpells(it);
                    else EditorGUILayout.PropertyField(it, true);
                    if (!it.NextVisible(false)) break;
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }
            if (GUILayout.Button("Add monster"))
            {
                int next = 1;
                for (int i = 0; i < list.arraySize; i++) next = Mathf.Max(next, list.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue + 1);
                list.arraySize++;
                var m = list.GetArrayElementAtIndex(list.arraySize - 1);
                m.FindPropertyRelative("id").intValue = next;
                m.FindPropertyRelative("name").stringValue = "New Monster";
                m.FindPropertyRelative("spells").arraySize = 0;
                _open[list.arraySize - 1] = true;
            }
            serializedObject.ApplyModifiedProperties();

            var report = new WorldValidator.Report();
            WorldValidator.ValidateMonsters((MonsterCatalog)target, _cards, report);
            EditorGUILayout.Space();
            WorldEditorUI.Report(report);
            WorldEditorUI.Buttons();
        }

        private void LoadCards()
        {
            if (_cards != null) return;
            _cards = WorldValidator.KnownCards();
            if (_cards == null) return;
            var ids = _cards.Keys.OrderBy(k => k).ToArray();
            _cardIds = ids;
            _cardLabels = ids.Select(k => k + "  " + _cards[k]).ToArray();
        }

        private void DrawSpells(SerializedProperty spells)
        {
            EditorGUILayout.LabelField("Spells (" + spells.arraySize + " / " + CardDatabase.MONSTER_SPELLS + ")", EditorStyles.boldLabel);
            for (int k = 0; k < spells.arraySize; k++)
            {
                var sp = spells.GetArrayElementAtIndex(k);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_cardIds != null)
                    {
                        int cur = System.Array.IndexOf(_cardIds, sp.intValue);
                        int pick = EditorGUILayout.Popup(cur, _cardLabels);
                        if (pick >= 0 && pick != cur) sp.intValue = _cardIds[pick];
                    }
                    else sp.intValue = EditorGUILayout.IntField("Spell id", sp.intValue);
                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(22))) { spells.DeleteArrayElementAtIndex(k); break; }
                }
            }
            if (spells.arraySize < CardDatabase.MONSTER_SPELLS && GUILayout.Button("Add spell", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                spells.arraySize++;
                spells.GetArrayElementAtIndex(spells.arraySize - 1).intValue = _cardIds != null && _cardIds.Length > 0 ? _cardIds[0] : 100;
            }
        }
    }

    // ====================================================================== questlines

    [CustomEditor(typeof(WorldArc))]
    public class WorldArcEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var w = (WorldArc)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            int count = 0;
            var lines = new List<string>();
            foreach (var sl in w.storylines)
            {
                if (sl == null || !sl.includeInBuild) continue;
                count++;
                lines.Add(count + ". " + sl.StorylineName() + "  (ends at quest " + sl.StorylineEnd() + ")");
            }
            EditorGUILayout.HelpBox(count + " / " + WorldArc.MinStorylines + " storylines lead up to the goal." +
                                    (count < WorldArc.MinStorylines ? " Add more before players move on to the next world." : "") +
                                    "\n" + string.Join("\n", lines) +
                                    (w.finaleQuest >= 0 ? "\nFinale: quest " + w.finaleQuest + " (needs every storyline above)." : ""),
                count >= WorldArc.MinStorylines ? MessageType.Info : MessageType.Warning);

            var report = new WorldValidator.Report();
            WorldValidator.ValidateWorlds(WorldEditorUI.Data(), report);
            EditorGUILayout.LabelField("Checks (all worlds)", EditorStyles.boldLabel);
            WorldEditorUI.Report(report);
            WorldEditorUI.Buttons();
        }
    }

    [CustomEditor(typeof(Questline))]
    public class QuestlineEditor : Editor
    {
        private bool _showRefs = true;

        public override void OnInspectorGUI()
        {
            var data = WorldEditorUI.Data();
            _showRefs = EditorGUILayout.Foldout(_showRefs, "Reference: NPC keys, points, monsters, save ids", true);
            if (_showRefs) DrawReference(data);
            EditorGUILayout.Space();

            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var report = new WorldValidator.Report();
            WorldValidator.ValidateQuests(data, report);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Checks (all questlines)", EditorStyles.boldLabel);
            WorldEditorUI.Report(report);
            WorldEditorUI.Buttons();
        }

        private static void DrawReference(WorldDataStore.Data data)
        {
            var npcs = new List<string> { "Headmaster", "OwnProfessor", "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
            var points = new List<string>();
            foreach (var z in data.zones)
            {
                if (!z.includeInBuild) continue;
                foreach (var n in z.npcs) if (n != null) npcs.Add(n.key + " (" + z.zoneKey + ")");
                foreach (var p in z.points) if (p != null) points.Add(p.key + " [" + p.kind + "] (" + z.zoneKey + ")");
            }
            var monsters = new List<string>();
            if (data.monsters != null) foreach (var m in data.monsters.monsters) if (m != null) monsters.Add(m.id + " " + m.name);
            var used = new List<int>();
            foreach (var ql in data.questlines) foreach (var q in ql.quests) if (q != null) used.Add(q.saveId);
            used.Sort();
            int free = 2;
            while (used.Contains(free)) free++;
            EditorGUILayout.HelpBox(
                "NPC keys: " + string.Join(", ", npcs) + "\n\n" +
                "Points: " + (points.Count > 0 ? string.Join(", ", points) : "(none)") + "\n\n" +
                "Monsters: " + (monsters.Count > 0 ? string.Join(", ", monsters) : "(none)") + "\n\n" +
                "Save ids in use: 0, 1 (built in), " + string.Join(", ", used) + ". Next free: " + free + ".\n" +
                "Prerequisite 0 = The Prophecy (the tutorial). Reward spell -1 = the player's own school quest spell.",
                MessageType.None);
        }
    }
}
