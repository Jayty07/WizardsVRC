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

        /// <summary>Creates the default assets (from the game's original content) for anything that is missing.</summary>
        public static void EnsureDefaults()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                // CreateFolder registers the folder with the AssetDatabase straight away (a plain directory may not be).
                if (Directory.Exists(Folder)) AssetDatabase.Refresh();
                else AssetDatabase.CreateFolder("Assets/WizardsVRC", "WorldData");
            }
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
                var z = ScriptableObject.CreateInstance<ZoneDefinition>();
                WorldDefaults.FillLanternRow(z);
                AssetDatabase.CreateAsset(z, Folder + "/Zone_LanternRow.asset");
                created = true;
            }
            if (FindAll<Questline>().Count == 0)
            {
                var q = ScriptableObject.CreateInstance<Questline>();
                WorldDefaults.FillLanternRowQuests(q);
                AssetDatabase.CreateAsset(q, Folder + "/Questline_LanternRow.asset");
                created = true;
            }
            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[WizardsVRC] Created default world data in " + Folder + ". Commit this folder to keep your edits.");
            }
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
