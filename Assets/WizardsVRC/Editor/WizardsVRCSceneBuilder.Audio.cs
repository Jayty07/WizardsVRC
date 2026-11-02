using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The VRChat SDK warns about every 2D audio source (Spatial Blend 0: the interface sounds, music, notices...) that
    /// has no VRC Spatial Audio Source. Its Auto Fix adds one, switched off, which keeps the sound exactly as it is; this
    /// does the same for every such source after each rebuild (and from the menu, for the open scene).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static Type _spatialAudioType;

        /// <summary>VRChat's world VRCSpatialAudioSource component type (looked up by name, so a different SDK layout still works).</summary>
        private static Type SpatialAudioType()
        {
            if (_spatialAudioType != null) return _spatialAudioType;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
                foreach (var t in types)
                {
                    if (t == null || t.Name != "VRCSpatialAudioSource" || !typeof(Component).IsAssignableFrom(t)) continue;
                    // Prefer the worlds one over the avatars one if both are loaded.
                    if (_spatialAudioType == null || (t.Namespace != null && !t.Namespace.Contains("Avatars"))) _spatialAudioType = t;
                }
            }
            return _spatialAudioType;
        }

        /// <summary>Adds a disabled VRC Spatial Audio Source to every 2D audio source that has none. Returns how many.</summary>
        private static int FixFlatAudio(Scene scene)
        {
            Type t = SpatialAudioType();
            if (t == null)
            {
                Debug.LogWarning("[WizardsVRC] Couldn't find VRChat's VRCSpatialAudioSource component; 2D audio sources were left as they are.");
                return 0;
            }
            int fixedCount = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
                {
                    if (src.spatialBlend > 0f || src.GetComponent(t) != null) continue;
                    var c = src.gameObject.AddComponent(t) as Behaviour;
                    if (c != null) c.enabled = false;
                    EditorUtility.SetDirty(src.gameObject);
                    fixedCount++;
                }
            return fixedCount;
        }

        [MenuItem("WizardsVRC/Fix 2D Audio Sources")]
        private static void FixFlatAudioMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            int n = FixFlatAudio(scene);
            if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[WizardsVRC] Added a (disabled) VRC Spatial Audio Source to " + n + " 2D audio sources.");
        }
    }
}
