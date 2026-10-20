using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WizardsVRC.EditorTools
{
    // Keeps the user's hand-made scene content across rebuilds. Everything under the root object "My Changes" is
    // saved to a prefab before the scene is regenerated, and then placed back into the new scene.
    public static partial class WizardsVRCSceneBuilder
    {
        public const string MyChangesName = "My Changes";
        private const string MyChangesDir = "Assets/WizardsVRC/MyChanges";
        private const string MyChangesPrefab = MyChangesDir + "/MyChanges.prefab";

        /// <summary>Saves the "My Changes" object from the current MainWorld scene to <see cref="MyChangesPrefab"/>.</summary>
        private static void BackUpMyChanges()
        {
            if (!File.Exists(ScenePath)) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject root = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == MyChangesName) { root = go; break; }
            if (root == null) return;

            Directory.CreateDirectory(MyChangesDir);
            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, MyChangesPrefab, out ok);
            if (ok) Debug.Log("[WizardsVRC] Backed up '" + MyChangesName + "' (" + root.transform.childCount + " objects) to " + MyChangesPrefab);
            else Debug.LogError("[WizardsVRC] Could not back up '" + MyChangesName + "' to " + MyChangesPrefab);
        }

        /// <summary>Puts the saved "My Changes" back into the freshly built scene, or creates an empty one.</summary>
        private static void RestoreMyChanges(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MyChangesPrefab);
            if (prefab == null)
            {
                var empty = new GameObject(MyChangesName);
                SceneManager.MoveGameObjectToScene(empty, scene);
                return;
            }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            // Unpack only the outer layer so models you dragged in stay linked to their own prefabs.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = MyChangesName;
            root.transform.SetAsLastSibling();
        }
    }
}
