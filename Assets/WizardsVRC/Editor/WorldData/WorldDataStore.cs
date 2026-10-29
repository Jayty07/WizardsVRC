using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// Finds the world data assets in <see cref="Folder"/>, creating the defaults the first time. Everything you author
    /// (monsters, questlines, zones) lives there as text assets, so commit that folder (with its .meta files) to keep
    /// your work between versions. Scene rebuilds never touch it.
    /// </summary>
    public static class WorldDataStore
    {
        public const string Folder = "Assets/WizardsVRC/WorldData";
        public const string MonstersPath = Folder + "/Monsters.asset";

        public class Data
        {
            public MonsterCatalog monsters;
            public List<Questline> questlines = new List<Questline>();
            public List<ZoneDefinition> zones = new List<ZoneDefinition>();
            public List<WorldArc> worlds = new List<WorldArc>();
        }

        /// <summary>Loads everything (creating the default assets if the folder has none) .</summary>
        public static Data Load()
        {
            EnsureDefaults();
            var data = new Data();
            data.monsters = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonstersPath);
            if (data.monsters == null) data.monsters = FindFirst<MonsterCatalog>();
            data.questlines = FindAll<Questline>();
            data.zones = FindAll<ZoneDefinition>();
            data.zones.Sort((a, b) => string.CompareOrdinal(a.zoneKey, b.zoneKey));
            data.questlines.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            data.worlds = FindAll<WorldArc>();
            data.worlds.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.name, b.name));
            return data;
        }

        [MenuItem("WizardsVRC/World Data/Open World Data Folder")]
        public static void OpenFolder()
        {
            EnsureDefaults();
            var folder = AssetDatabase.LoadAssetAtPath<Object>(Folder);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        [MenuItem("WizardsVRC/World Data/Validate World Data")]
        public static void ValidateMenu()
        {
            var data = Load();
            var report = WorldValidator.ValidateAll(data, WorldValidator.KnownCards());
            report.Log();
            EditorUtility.DisplayDialog("World Data", report.Summary(), "OK");
        }

        [MenuItem("WizardsVRC/World Data/New Zone")]
        public static void NewZone() { CreateNew<ZoneDefinition>("Zone_New", z => { z.zoneKey = "NewZone"; z.displayName = "New Zone"; z.height = NextFreeHeight(); z.map = WorldDefaults.StarterMap; }); }

        [MenuItem("WizardsVRC/World Data/New Questline")]
        public static void NewQuestline() { CreateNew<Questline>("Questline_New", q => { }); }

        [MenuItem("WizardsVRC/World Data/New World")]
        public static void NewWorld()
        {
            int order = 1;
            foreach (var w in FindAll<WorldArc>()) order = Mathf.Max(order, w.order + 1);
            CreateNew<WorldArc>("World_New", w => { w.order = order; w.displayName = "World " + order; });
        }

        /// <summary>Bumped when the defaults gain content that older folders should receive once (see Upgrade).</summary>
        public const int CurrentVersion = 11;
        public const string VersionPath = Folder + "/WorldDataVersion.txt";

        /// <summary>
        /// Creates the default assets (from the game's original content) the first time, and upgrades a folder made by an
        /// older version once (adding the castle hub, Sunpetal Gardens and the tutorial quests) without touching your edits.
        /// </summary>
        public static void EnsureDefaults()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                // CreateFolder registers the folder with the AssetDatabase straight away (a plain directory may not be).
                if (Directory.Exists(Folder)) AssetDatabase.Refresh();
                else AssetDatabase.CreateFolder("Assets/WizardsVRC", "WorldData");
            }
            bool hadData = FindAll<ZoneDefinition>().Count > 0 || FindAll<Questline>().Count > 0;
            bool created = false;
            if (AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonstersPath) == null && FindFirst<MonsterCatalog>() == null)
            {
                var m = ScriptableObject.CreateInstance<MonsterCatalog>();
                WorldDefaults.FillMonsters(m);
                AssetDatabase.CreateAsset(m, MonstersPath);
                created = true;
            }
            if (FindAll<ZoneDefinition>().Count == 0)
            {
                CreateZone("Zone_StarfallCastle", WorldDefaults.FillStarfallCastle);
                CreateZone("Zone_SunpetalGardens", WorldDefaults.FillSunpetalGardens);
                CreateZone("Zone_LanternRow", WorldDefaults.FillLanternRow);
                created = true;
            }
            if (FindAll<Questline>().Count == 0)
            {
                CreateQuestline("Questline_Tutorial", WorldDefaults.FillTutorialQuests);
                CreateQuestline("Questline_SunpetalGardens", WorldDefaults.FillGardenQuests);
                CreateQuestline("Questline_LanternRow", WorldDefaults.FillLanternRowQuests);
                CreateQuestline("Questline_World1Finale", WorldDefaults.FillWorld1Finale);
                created = true;
            }
            if (FindAll<WorldArc>().Count == 0 && !hadData)
            {
                CreateWorld1();
                created = true;
            }
            int version = ReadVersion();
            if (hadData && version < CurrentVersion)
            {
                Upgrade(version);
                created = true;
            }
            if (version < CurrentVersion) WriteVersion(CurrentVersion);
            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[WizardsVRC] Created/updated default world data in " + Folder + ". Commit this folder to keep your edits.");
            }
        }

        private static void CreateZone(string file, System.Action<ZoneDefinition> fill)
        {
            var z = ScriptableObject.CreateInstance<ZoneDefinition>();
            fill(z);
            AssetDatabase.CreateAsset(z, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + file + ".asset"));
        }

        private static void CreateQuestline(string file, System.Action<Questline> fill)
        {
            var q = ScriptableObject.CreateInstance<Questline>();
            fill(q);
            AssetDatabase.CreateAsset(q, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + file + ".asset"));
        }

        private static int ReadVersion()
        {
            try { return File.Exists(VersionPath) ? int.Parse(File.ReadAllText(VersionPath).Trim()) : 1; }
            catch (System.Exception) { return 1; }
        }

        private static void WriteVersion(int v)
        {
            File.WriteAllText(VersionPath, v + "\n");
            AssetDatabase.ImportAsset(VersionPath);
        }

        /// <summary>
        /// One-time upgrade of a folder made by an older version. Adds what's missing (by zone key, quest save id and
        /// monster id) and only changes values that still hold the old defaults, so nothing you edited is overwritten.
        /// </summary>
        private static void Upgrade(int from)
        {
            if (from < 2) UpgradeTo2();
            if (from < 3) UpgradeTo3();
            if (from < 4) UpgradeTo4();
            if (from < 5) UpgradeTo5();
            if (from < 6) UpgradeTo6();
            if (from < 7) UpgradeTo7();
            if (from < 8) UpgradeTo8();
            if (from < 9) UpgradeTo9();
            if (from < 10) UpgradeTo10();
            if (from < 11) UpgradeTo11();
        }

        /// <summary>
        /// v11: the round hub (rings round the tavern, halls facing the centre, round curtain wall), the duelling circle,
        /// health and mana orbs, and no testing chest. Only if the hub still has exactly the v10 layout; otherwise just
        /// the chest goes.
        /// </summary>
        private static void UpgradeTo11()
        {
            foreach (var z in FindAll<ZoneDefinition>())
            {
                if (z == null || !z.isHub) continue;
                var current = ScriptableObject.CreateInstance<ZoneDefinition>();
                WorldDefaults.FillStarfallCastle(current);
                if (Normalize(z.map) == Normalize(current.map)) continue; // already done by an earlier step
                if (Normalize(z.map) != Normalize(WorldDefaults.HubMapV10))
                {
                    if (z.landmarks != null && z.landmarks.RemoveAll(l => l != null && l.kind == LandmarkKind.BankChest) > 0) EditorUtility.SetDirty(z);
                    Debug.Log("[WizardsVRC] Your hub (" + z.displayName + ") has been edited, so the round hub wasn't applied (the practice chest " +
                              "was removed). New landmarks you can paint yourself: Duel Circle (PvP), Health Orb and Mana Orb; Door Side Centre turns " +
                              "a building to face the middle, and Round Castle gives a round island and wall.");
                    continue;
                }
                ApplyDefaultHubLayout(z);
                Debug.Log("[WizardsVRC] Upgraded world data: " + z.displayName + " is now round, with a duelling circle and health / mana orbs.");
            }
        }

        /// <summary>v10: the sunken plaza becomes a round tavern (the Crystal Tap) round a smaller Starfall fountain, with a barkeep.</summary>
        private static void UpgradeTo10()
        {
            foreach (var z in FindAll<ZoneDefinition>())
            {
                if (z == null || !z.isHub) continue;
                var current = ScriptableObject.CreateInstance<ZoneDefinition>();
                WorldDefaults.FillStarfallCastle(current);
                if (Normalize(z.map) == Normalize(current.map)) continue; // already done by an earlier step
                if (Normalize(z.map) != Normalize(WorldDefaults.HubMapV9))
                {
                    Debug.Log("[WizardsVRC] Your hub (" + z.displayName + ") has been edited, so the round tavern wasn't added. Paint a Tavern " +
                              "landmark over a block of about 8 x 8 cells to add one.");
                    continue;
                }
                ApplyDefaultHubLayout(z);
                Debug.Log("[WizardsVRC] Upgraded world data: " + z.displayName + " has a round tavern, the Crystal Tap, in the round hub.");
            }
        }

        /// <summary>
        /// v9: a taller hub (6 m terrace with landings, hills, a deeper plaza, mounds and a sunken garden), rounded corners
        /// and railings. Only if the hub still has exactly the v8 layout.
        /// </summary>
        private static void UpgradeTo9()
        {
            foreach (var z in FindAll<ZoneDefinition>())
            {
                if (z == null || !z.isHub) continue;
                var current = ScriptableObject.CreateInstance<ZoneDefinition>();
                WorldDefaults.FillStarfallCastle(current);
                if (Normalize(z.map) == Normalize(current.map) && Normalize(z.heightMap) == Normalize(current.heightMap)) continue; // already done by v8
                if (Normalize(z.map) != Normalize(WorldDefaults.HubMapV8) || Normalize(z.heightMap) != Normalize(WorldDefaults.HubHeightsV8))
                {
                    if (Normalize(z.map) != Normalize(WorldDefaults.HubMapV2))
                        Debug.Log("[WizardsVRC] Your hub (" + z.displayName + ") has been edited, so the taller layout wasn't applied. Corner Radius " +
                                  "and Railings are new settings on the zone; try a radius of 2.5 and Drops And Paths.");
                    continue;
                }
                ApplyDefaultHubLayout(z);
                Debug.Log("[WizardsVRC] Upgraded world data: " + z.displayName + " is taller, with rounded corners and railings.");
            }
        }

        private static void ApplyDefaultHubLayout(ZoneDefinition z)
        {
            var fresh = ScriptableObject.CreateInstance<ZoneDefinition>();
            WorldDefaults.FillStarfallCastle(fresh);
            z.map = fresh.map;
            z.heightMap = fresh.heightMap;
            z.heightStep = fresh.heightStep;
            z.cornerRadius = fresh.cornerRadius;
            z.railings = fresh.railings;
            z.cellSize = fresh.cellSize;
            z.castleStyle = fresh.castleStyle;
            z.roundCastle = fresh.roundCastle;
            z.lampDensity = fresh.lampDensity;
            WorldDefaults.SyncHubLandmarks(z);
            EditorUtility.SetDirty(z);
        }

        /// <summary>
        /// v8: the hub gets heights (north terrace, sunken fountain plaza, raised library / arena / garden), a waterfall and
        /// pond, defined door paths, an observatory, a gazebo and shops. Only if its map is still the old default.
        /// </summary>
        private static void UpgradeTo8()
        {
            foreach (var z in FindAll<ZoneDefinition>())
            {
                if (z == null || !z.isHub) continue;
                if (Normalize(z.map) != Normalize(WorldDefaults.HubMapV2) || !string.IsNullOrEmpty(z.heightMap))
                {
                    Debug.Log("[WizardsVRC] Your hub map (" + z.displayName + ") has been edited, so the new hub layout (terrace, waterfall, " +
                              "shops) wasn't applied. You can paint '~' water, Shop / Waterfall landmarks and a Height Map yourself.");
                    continue;
                }
                ApplyDefaultHubLayout(z);
                Debug.Log("[WizardsVRC] Upgraded world data: " + z.displayName + " now has a terrace, a waterfall, walk-in school halls and shops.");
            }
        }

        private static string Normalize(string map)
        {
            var rows = new List<string>();
            foreach (string r in (map ?? "").Replace("\r", "").Split('\n')) if (r.Trim().Length > 0) rows.Add(r.TrimEnd());
            return string.Join("\n", rows);
        }

        private const int MendId = 201;

        /// <summary>v7: a project upgraded by the earlier v5 (Briarwick with no heal at all) gets Mend back in.</summary>
        private static void UpgradeTo7()
        {
            var cat = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonstersPath) ?? FindFirst<MonsterCatalog>();
            var m = cat != null ? cat.Find(11) : null;
            if (m == null || m.spells == null || m.spells.Count != 2 || m.spells[0] != 136 || m.spells[1] != 138) return;
            m.spells.Insert(1, MendId);
            if (m.notes != null) m.notes = m.notes.Replace("No heal: first boss for new wizards.", "Heals with Mend only: first boss for new wizards.");
            EditorUtility.SetDirty(cat);
            Debug.Log("[WizardsVRC] Upgraded world data: " + m.name + " heals with Mend.");
        }

        /// <summary>v6: monsters get a rank and creature type (shown in the duel as "Rank 2 Life Plant").</summary>
        private static void UpgradeTo6()
        {
            var cat = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonstersPath) ?? FindFirst<MonsterCatalog>();
            if (cat == null) return;
            var defaults = ScriptableObject.CreateInstance<MonsterCatalog>();
            WorldDefaults.FillMonsters(defaults);
            int n = 0;
            foreach (var m in cat.monsters)
            {
                if (m == null || !string.IsNullOrEmpty(m.creatureType)) continue;
                // Only the default monsters still carrying their default name get the default rank and type.
                var d = defaults.Find(m.id);
                if (d == null || d.name != m.name) continue;
                if (WorldDefaults.ApplyDefaultRank(m)) n++;
            }
            if (n > 0)
            {
                EditorUtility.SetDirty(cat);
                Debug.Log("[WizardsVRC] Upgraded world data: gave " + n + " monsters a rank and creature type. Set them for your own " +
                          "monsters in Monsters.asset (Rank, Creature Type).");
            }
        }

        /// <summary>v5: Old Briarwick (first area boss) swaps Verdant Bloom for Mend (a smaller heal), if his spells are still the defaults.</summary>
        private static void UpgradeTo5()
        {
            var cat = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonstersPath) ?? FindFirst<MonsterCatalog>();
            var m = cat != null ? cat.Find(11) : null;
            if (m == null || m.spells == null) return;
            if (m.spells.Count == 3 && m.spells[0] == 136 && m.spells[1] == 137 && m.spells[2] == 138)
            {
                m.spells[1] = MendId;
                if (m.notes != null && !m.notes.Contains("Mend")) m.notes += " Heals with Mend only: first boss for new wizards.";
                EditorUtility.SetDirty(cat);
                Debug.Log("[WizardsVRC] Upgraded world data: " + m.name + " heals with Mend instead of Verdant Bloom.");
            }
            else Debug.Log("[WizardsVRC] Monster 11 (" + m.name + ") has edited spells, so its heal was left as you set it.");
        }

        /// <summary>World 1 (Starfall Isle) with its goal, storylines and finale.</summary>
        private static void CreateWorld1()
        {
            var storylines = new List<Questline>();
            foreach (string file in new[] { "Questline_Tutorial", "Questline_SunpetalGardens", "Questline_LanternRow" })
            {
                var ql = AssetDatabase.LoadAssetAtPath<Questline>(Folder + "/" + file + ".asset");
                if (ql != null) storylines.Add(ql);
            }
            var w = ScriptableObject.CreateInstance<WorldArc>();
            WorldDefaults.FillWorld1(w, storylines);
            AssetDatabase.CreateAsset(w, AssetDatabase.GenerateUniqueAssetPath(Folder + "/World_StarfallIsle.asset"));
        }

        /// <summary>v4: worlds. Adds World 1 (its goal, storylines and finale quest 16) unless you already made a world.</summary>
        private static void UpgradeTo4()
        {
            var questlines = FindAll<Questline>();
            var used = new HashSet<int>();
            foreach (var ql in questlines) foreach (var q in ql.quests) if (q != null) used.Add(q.saveId);
            // Name the default storylines and mark where each ends (side quests come after Lantern Row's quest 5).
            foreach (var ql in questlines)
            {
                bool has2 = false, has8 = false, has13 = false;
                foreach (var q in ql.quests) if (q != null) { has2 |= q.saveId == 2; has8 |= q.saveId == 8; has13 |= q.saveId == 13; }
                if (string.IsNullOrEmpty(ql.storylineName) && ql.storylineEnd < 0)
                {
                    if (has2) { ql.storylineName = "The First Lantern"; ql.storylineEnd = 5; }
                    else if (has8) { ql.storylineName = "A Castle Tour"; ql.storylineEnd = 12; }
                    else if (has13) { ql.storylineName = "The Briar Heart"; ql.storylineEnd = 15; }
                    EditorUtility.SetDirty(ql);
                }
            }
            if (!used.Contains(16)) CreateQuestline("Questline_World1Finale", WorldDefaults.FillWorld1Finale);
            if (FindAll<WorldArc>().Count == 0)
            {
                // In story order: the castle tour (quest 8), Sunpetal Gardens (13), Lantern Row (2).
                var storylines = new List<Questline>();
                foreach (int id in new[] { 8, 13, 2 })
                    foreach (var ql in questlines)
                        foreach (var q in ql.quests)
                            if (q != null && q.saveId == id && !storylines.Contains(ql)) storylines.Add(ql);
                var w = ScriptableObject.CreateInstance<WorldArc>();
                WorldDefaults.FillWorld1(w, storylines);
                AssetDatabase.CreateAsset(w, AssetDatabase.GenerateUniqueAssetPath(Folder + "/World_StarfallIsle.asset"));
                Debug.Log("[WizardsVRC] Upgraded world data: added World 1 (Starfall Isle) with " + storylines.Count + " storylines and its finale (quest 16).");
            }
        }

        /// <summary>v3: the first areas (castle practice ring, Sunpetal Gardens) use even fights.</summary>
        private static void UpgradeTo3()
        {
            foreach (var z in FindAll<ZoneDefinition>())
            {
                if (z.zoneKey != "SunpetalGardens" && z.zoneKey != "StarfallCastle") continue;
                foreach (var e in z.encounters) if (e != null) e.evenFight = true;
                EditorUtility.SetDirty(z);
                Debug.Log("[WizardsVRC] Upgraded world data: even fights in " + z.displayName + ".");
            }
        }

        private static void UpgradeTo2()
        {
            var zones = FindAll<ZoneDefinition>();
            var questlines = FindAll<Questline>();
            var report = new List<string>();

            // Monsters: add the new ones; move the Lantern Row monsters up to Lv 4-6 if they're still the old defaults.
            var cat = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonstersPath) ?? FindFirst<MonsterCatalog>();
            if (cat != null)
            {
                var defaults = ScriptableObject.CreateInstance<MonsterCatalog>();
                WorldDefaults.FillMonsters(defaults);
                int[] oldLevel = { 0, 0, 0, 0, 0, 1, 2, 4 }, oldHp = { 0, 0, 0, 0, 0, 180, 260, 900 };
                foreach (var d in defaults.monsters)
                {
                    var have = cat.Find(d.id);
                    if (have == null) { cat.monsters.Add(d); report.Add("monster " + d.id + " " + d.name); continue; }
                    if (d.id >= 5 && d.id <= 7 && have.level == oldLevel[d.id] && have.health == oldHp[d.id])
                    {
                        have.level = d.level;
                        have.health = d.health;
                        have.healthPerExtraWizard = d.healthPerExtraWizard;
                        have.xp = d.xp;
                        have.goldMin = d.goldMin;
                        have.goldMax = d.goldMax;
                        if (d.id == 7 && have.dropItem == 61) { have.dropItem = d.dropItem; have.notes = d.notes; }
                        report.Add(d.name + " to level " + d.level);
                    }
                }
                Object.DestroyImmediate(defaults);
                EditorUtility.SetDirty(cat);
            }

            // Zones: the castle hub (unless you already made one) and Sunpetal Gardens.
            bool hasHub = false, hasGardens = false;
            foreach (var z in zones) { hasHub |= z.isHub; hasGardens |= z.zoneKey == "SunpetalGardens"; }
            if (!hasHub) { CreateZone("Zone_StarfallCastle", WorldDefaults.FillStarfallCastle); report.Add("Starfall Castle hub map"); }
            if (!hasGardens) { CreateZone("Zone_SunpetalGardens", WorldDefaults.FillSunpetalGardens); report.Add("Sunpetal Gardens"); }
            foreach (var z in zones)
                if (z.zoneKey == "LanternRow" && z.levelRange == "Lv 1-3") { z.levelRange = "Lv 4-6"; EditorUtility.SetDirty(z); }

            // Questlines: the tutorial (8-12) and the gardens (13-15) unless those save ids are taken; Lantern Row now follows the gardens.
            var used = new HashSet<int>();
            foreach (var ql in questlines) foreach (var q in ql.quests) if (q != null) used.Add(q.saveId);
            if (!used.Contains(8) && !used.Contains(12)) { CreateQuestline("Questline_Tutorial", WorldDefaults.FillTutorialQuests); report.Add("tutorial quests 8-12"); }
            if (!used.Contains(13) && !used.Contains(15)) { CreateQuestline("Questline_SunpetalGardens", WorldDefaults.FillGardenQuests); report.Add("Sunpetal Gardens quests 13-15"); }
            int[] oldMin = { 0, 0, 1, 1, 2, 3, 1, 1 }, newMin = { 0, 0, 3, 4, 4, 5, 3, 3 };
            foreach (var ql in questlines)
            {
                bool dirty = false;
                foreach (var q in ql.quests)
                {
                    if (q == null || q.saveId < 2 || q.saveId > 7) continue;
                    if (q.minLevel == oldMin[q.saveId]) { q.minLevel = newMin[q.saveId]; dirty = true; }
                    if (q.saveId != 2) continue;
                    if (q.prerequisite == QuestManager.QUEST_PROPHECY) { q.prerequisite = 15; dirty = true; report.Add("Lantern Row now opens after Sunpetal Gardens"); }
                    if (q.description == WorldDefaults.OldLanternRowIntro) { q.description = WorldDefaults.LanternRowIntro; dirty = true; }
                    if (q.steps.Count > 0 && q.steps[0].objective == WorldDefaults.OldLanternRowIntroStep) { q.steps[0].objective = WorldDefaults.LanternRowIntroStep; dirty = true; }
                }
                if (dirty) EditorUtility.SetDirty(ql);
            }
            if (report.Count > 0) Debug.Log("[WizardsVRC] Upgraded world data: added/updated " + string.Join(", ", report) + ".");
        }

        private static float NextFreeHeight()
        {
            float h = 600f;
            foreach (var z in FindAll<ZoneDefinition>()) h = Mathf.Max(h, z.height + 100f);
            return h;
        }

        private static void CreateNew<T>(string baseName, System.Action<T> init) where T : ScriptableObject
        {
            EnsureDefaults();
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + baseName + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static List<T> FindAll<T>() where T : Object
        {
            var list = new List<T>();
            if (AssetDatabase.IsValidFolder(Folder))
                foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Folder }))
                {
                    var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                    if (a != null && !list.Contains(a)) list.Add(a);
                }
            // Also load the files directly, in case the search index hasn't caught up with assets created just now.
            if (Directory.Exists(Folder))
                foreach (string file in Directory.GetFiles(Folder, "*.asset", SearchOption.AllDirectories))
                {
                    var a = AssetDatabase.LoadAssetAtPath<T>(file.Replace('\\', '/'));
                    if (a != null && !list.Contains(a)) list.Add(a);
                }
            return list;
        }

        private static T FindFirst<T>() where T : Object
        {
            var all = FindAll<T>();
            return all.Count > 0 ? all[0] : null;
        }
    }
}
