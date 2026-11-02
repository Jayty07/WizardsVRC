using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The VRChat SDK warns about every audio source without a VRC Spatial Audio Source. Its own Auto Fix adds one that
    /// stays switched ON as a component, with its "Enable Spatialization" box OFF for 2D sources (music, interface sounds,
    /// ambience) so they keep playing flat, everywhere. This does the same after each rebuild (and from the menu):
    ///   - 2D sources: component on, Enable Spatialization off, Spatial Blend stays 0.
    ///   - 3D sources: component on, spatialized, using the AudioSource's own falloff and distances.
    /// It also repairs components an older version of this fix added switched off with spatialization still on, which
    /// pulled the hub music and ambience into 3D at their source objects (so they went quiet away from them).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static Type _spatialAudioType, _spatialAudioBase;

        /// <summary>VRChat's world VRCSpatialAudioSource component type (looked up by name, so a different SDK layout still works).</summary>
        private static Type SpatialAudioType()
        {
            if (_spatialAudioType != null) return _spatialAudioType;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                foreach (var t in types)
                {
                    if (t == null || !typeof(Component).IsAssignableFrom(t)) continue;
                    if (t.Name == "VRC_SpatialAudioSource") _spatialAudioBase = t;
                    if (t.Name != "VRCSpatialAudioSource" || t.IsAbstract) continue;
                    // Prefer the worlds one over the avatars one if both are loaded.
                    bool avatars = t.Namespace != null && t.Namespace.Contains("Avatars");
                    if (_spatialAudioType == null || !avatars) _spatialAudioType = t;
                }
            }
            return _spatialAudioType;
        }

        /// <summary>The component the SDK checks for (the shared base class when it can be found).</summary>
        private static Type SpatialAudioCheckType()
        {
            SpatialAudioType();
            if (_spatialAudioBase != null && _spatialAudioType != null && _spatialAudioBase.IsAssignableFrom(_spatialAudioType)) return _spatialAudioBase;
            return _spatialAudioType;
        }

        private static void SetAudioMember(object target, string name, object value)
        {
            Type t = target.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type k = t; k != null; k = k.BaseType)
            {
                FieldInfo f = k.GetField(name, F | BindingFlags.DeclaredOnly);
                if (f != null) { f.SetValue(target, value); return; }
                PropertyInfo p = k.GetProperty(name, F | BindingFlags.DeclaredOnly);
                if (p != null && p.CanWrite) { p.SetValue(target, value, null); return; }
            }
        }

        private static object GetAudioMember(object target, string name)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type k = target.GetType(); k != null; k = k.BaseType)
            {
                FieldInfo f = k.GetField(name, F | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(target);
                PropertyInfo p = k.GetProperty(name, F | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead) return p.GetValue(target, null);
            }
            return null;
        }

        private static bool IsFlatAudio(AudioSource src)
        {
            AnimationCurve curve = src.GetCustomCurve(AudioSourceCurveType.SpatialBlend);
            return Mathf.Abs(src.spatialBlend) < float.Epsilon && (curve == null || curve.keys.Length <= 1);
        }

        /// <summary>Gives every audio source a correctly set VRC Spatial Audio Source. Returns how many were added or repaired.</summary>
        private static int FixFlatAudio(Scene scene)
        {
            Type add = SpatialAudioType();
            Type check = SpatialAudioCheckType();
            if (add == null)
            {
                Debug.LogWarning("[WizardsVRC] Couldn't find VRChat's VRCSpatialAudioSource component; audio sources were left as they are.");
                return 0;
            }
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
                {
                    var c = src.GetComponent(check) as Behaviour;
                    bool flat = IsFlatAudio(src);
                    if (c == null) c = src.gameObject.AddComponent(add) as Behaviour;
                    else if (c.enabled && (!flat || Equals(GetAudioMember(c, "EnableSpatialization"), false))) continue;   // already set up
                    if (c == null) continue;
                    c.enabled = true;
                    if (flat)
                    {
                        SetAudioMember(c, "EnableSpatialization", false);
                        src.spatialize = false;
                        src.spatialBlend = 0f;
                    }
                    else
                    {
                        SetAudioMember(c, "EnableSpatialization", true);
                        SetAudioMember(c, "UseAudioSourceVolumeCurve", true);
                        SetAudioMember(c, "Near", src.minDistance);
                        SetAudioMember(c, "Far", src.maxDistance);
                    }
                    EditorUtility.SetDirty(c);
                    EditorUtility.SetDirty(src);
                    count++;
                }
            return count;
        }

        [MenuItem("WizardsVRC/Fix Audio Sources (VRC Spatial Audio)")]
        private static void FixFlatAudioMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            int n = FixFlatAudio(scene);
            if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[WizardsVRC] Set up a VRC Spatial Audio Source on " + n + " audio sources (2D ones with spatialization off).");
        }
    }
}
