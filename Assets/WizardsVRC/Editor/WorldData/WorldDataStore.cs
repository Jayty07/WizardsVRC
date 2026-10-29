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

        /// <summary>Bumped when the defaults gain content that older folders should receive once (see Upgrade).</summary>
        public const int CurrentVersion = 3;
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
