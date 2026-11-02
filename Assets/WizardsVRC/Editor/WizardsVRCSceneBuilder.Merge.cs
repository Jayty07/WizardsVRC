using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The last pass of a rebuild: the world is built from tens of thousands of small blocks and shapes, each its own
    /// renderer. Every frame Unity culls, sorts and submits each one, which costs far more CPU than drawing them. This
    /// combines static scenery that shares a material into one mesh per 32 m chunk, so a street is a few hundred renderers
    /// instead of many thousands. It looks exactly the same (the same vertices, material and PSX shader).
    ///
    /// What is never merged away:
    ///   - anything a script refers to (a gate's closed visual, a seat's glow, a quest object...), anything with a
    ///     behaviour, a rigidbody or that's switched off, and anything not marked static: these are "anchors". Static
    ///     pieces under an anchor are still merged, but into a mesh that's a child of that anchor, so when a script moves
    ///     or hides the anchor the merged mesh moves and hides with it, exactly as the pieces did.
    ///   - whole subtrees under an Animator, a legacy Animation, a particle system or a canvas (they animate children by
    ///     name), and the spell animations, the systems and your own MyChanges objects.
    ///   - see-through materials (their draw order matters) and anything not using the PSX shader.
    /// Small props in a distance-culling chunk (LODGroup, WizardsVRCSceneBuilder.Culling.cs) are merged per chunk and the
    /// chunk is pointed at the merged meshes, so they still disappear beyond the zone's detail distance.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float MergeCell = 32f;

        private struct MergeKey
        {
            public GameObject anchor;
            public LODGroup lod;
            public Material mat;
            public Vector2Int cell;
            public int shadow, receive, flags, layer;

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = anchor != null ? anchor.GetInstanceID() : 0;
                    h = h * 31 + (lod != null ? lod.GetInstanceID() : 0);
                    h = h * 31 + (mat != null ? mat.GetInstanceID() : 0);
                    h = h * 31 + cell.GetHashCode();
                    h = h * 31 + shadow;
                    h = h * 31 + receive;
                    h = h * 31 + flags;
                    return h * 31 + layer;
                }
            }

            public override bool Equals(object o)
            {
                if (!(o is MergeKey)) return false;
                var k = (MergeKey)o;
                return k.anchor == anchor && k.lod == lod && k.mat == mat && k.cell == cell && k.shadow == shadow && k.receive == receive && k.flags == flags && k.layer == layer;
            }
        }

        /// <summary>Every scene object a behaviour's fields point at (scripts may move, show or hide them).</summary>
        private static HashSet<GameObject> ReferencedObjects(Scene scene)
        {
            var found = new HashSet<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string ns = mb.GetType().Namespace ?? "";
                    // Text, images and layout components only point at fonts, sprites and materials.
                    if (ns.StartsWith("UnityEngine.UI") || ns.StartsWith("TMPro")) continue;
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    bool enter = true;
                    while (it.Next(enter))
                    {
                        enter = it.propertyType != SerializedPropertyType.String;
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object o = it.objectReferenceValue;
                        GameObject g = o as GameObject;
                        if (g == null && o is Component) g = ((Component)o).gameObject;
                        if (g != null && g.scene == scene) found.Add(g);
                    }
                }
            return found;
        }

        private static bool BlocksMerging(GameObject g)
        {
            return g.GetComponent<Animator>() != null || g.GetComponent<Animation>() != null || g.GetComponent<ParticleSystem>() != null ||
                   g.GetComponent<Canvas>() != null || g.GetComponent<SkinnedMeshRenderer>() != null || g.GetComponent<Cloth>() != null ||
                   g.name == "SpellAnimations" || g.name.StartsWith("Mirror") || g.name.StartsWith("JukeboxScreenRig");
        }

        private static bool IsMergeAnchor(GameObject g, HashSet<GameObject> referenced)
        {
            if (!g.activeSelf || !g.isStatic || referenced.Contains(g)) return true;
            if (g.GetComponent<UdonBehaviour>() != null || g.GetComponent<Rigidbody>() != null || g.GetComponent<LODGroup>() != null) return true;
            string n = g.name;
            return n.StartsWith("Encounter") || n.StartsWith("Patrol_") || n.StartsWith("NPC_") || n.StartsWith("Gate_") || n.StartsWith("Float_") ||
                   n.StartsWith("HearthFire") || n.StartsWith("ClassroomMagic") || n.StartsWith("Classroom_") || n.StartsWith("RingSpots_") || n.StartsWith("Point_");
        }

        /// <summary>Combines static scenery renderers (see the class summary). Returns how many renderers it removed.</summary>
        private static int MergeStaticScenery(Scene scene)
        {
            var referenced = ReferencedObjects(scene);
            var skipRoots = new HashSet<string> { "Systems", "VRCWorld", "SpellAnimations", MyChangesName, "Directional Light" };

            // Which distance-culling chunk each small prop belongs to.
            var lodOf = new Dictionary<Renderer, LODGroup>();
            var lodHeight = new Dictionary<LODGroup, float>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
                {
                    LOD[] lods = lg.GetLODs();
                    if (lods.Length != 1) continue;   // only the single-level chunks the builder makes
                    lodHeight[lg] = lods[0].screenRelativeTransitionHeight;
                    foreach (var r in lods[0].renderers) if (r != null) lodOf[r] = lg;
                }

            var groups = new Dictionary<MergeKey, List<KeyValuePair<MeshRenderer, int>>>();
            var psx = Shader.Find("WizardsVRC/PSX");
            int before = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (skipRoots.Contains(root.name)) continue;
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    before++;
                    GameObject go = mr.gameObject;
                    if (!mr.enabled || !go.activeSelf || !go.isStatic || IsMergeAnchor(go, referenced) || BlocksMerging(go)) continue;
                    var mf = go.GetComponent<MeshFilter>();
                    Mesh mesh = mf != null ? mf.sharedMesh : null;
                    // Unity's own primitives (cubes, cylinders...) can always be combined in the editor.
                    if (mesh == null || go.GetComponent<TextMesh>() != null) continue;
                    if (!mesh.isReadable && !AssetDatabase.GetAssetPath(mesh).StartsWith("Library/")) continue;
                    Vector3 ls = go.transform.lossyScale;
                    if (ls.x * ls.y * ls.z <= 0f) continue;   // mirrored: combining wouldn't flip its faces back
                    Material[] mats = mr.sharedMaterials;
                    if (mats.Length == 0 || mats.Length > mesh.subMeshCount) continue;
                    bool ok = true;
                    foreach (var m in mats) if (m == null || m.shader != psx || m.renderQueue >= 2500) { ok = false; break; }
                    if (!ok) continue;

                    // The nearest anchor above it; nothing merges out from under an Animator, particle system or canvas.
                    GameObject anchor = root;
                    bool blocked = false;
                    for (Transform p = go.transform.parent; p != null; p = p.parent)
                    {
                        if (BlocksMerging(p.gameObject)) { blocked = true; break; }
                        if (anchor == root && IsMergeAnchor(p.gameObject, referenced)) anchor = p.gameObject;
                    }
                    if (blocked) continue;

                    LODGroup lod;
                    if (lodOf.TryGetValue(mr, out lod))
                    {
                        // A chunk member merges into its chunk, if hiding or moving anything above it wouldn't be lost.
                        GameObject lodAnchor = lod.transform.root.gameObject;
                        for (Transform p = lod.transform.parent; p != null; p = p.parent)
                            if (IsMergeAnchor(p.gameObject, referenced)) { lodAnchor = p.gameObject; break; }
                        if (lodAnchor != anchor) continue;
                    }
                    else lod = null;

                    Vector3 c = mr.bounds.center;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var key = new MergeKey
                        {
                            anchor = lod != null ? lod.gameObject : anchor,
                            lod = lod,
                            mat = mats[i],
                            cell = lod != null ? Vector2Int.zero : new Vector2Int(Mathf.FloorToInt(c.x / MergeCell), Mathf.FloorToInt(c.z / MergeCell)),
                            shadow = (int)mr.shadowCastingMode,
                            receive = mr.receiveShadows ? 1 : 0,
                            flags = (int)GameObjectUtility.GetStaticEditorFlags(go),
                            layer = go.layer,
                        };
                        List<KeyValuePair<MeshRenderer, int>> list;
                        if (!groups.TryGetValue(key, out list)) groups[key] = list = new List<KeyValuePair<MeshRenderer, int>>();
                        list.Add(new KeyValuePair<MeshRenderer, int>(mr, i));
                    }
                }
            }

            // A renderer is only taken apart if every one of its materials found a group with company.
            var merged = new HashSet<MeshRenderer>();
            var lonely = new HashSet<MeshRenderer>();
            foreach (var kv in groups)
                if (kv.Value.Count < 2)
                    foreach (var e in kv.Value) lonely.Add(e.Key);

            var newByLod = new Dictionary<LODGroup, List<Renderer>>();
            int made = 0;
            foreach (var kv in groups)
            {
                var parts = new List<CombineInstance>();
                foreach (var e in kv.Value)
                {
                    if (lonely.Contains(e.Key)) continue;
                    Transform anchorT = kv.Key.anchor.transform;
                    parts.Add(new CombineInstance
                    {
                        mesh = e.Key.GetComponent<MeshFilter>().sharedMesh,
                        subMeshIndex = e.Value,
                        transform = anchorT.worldToLocalMatrix * e.Key.transform.localToWorldMatrix,
                    });
                    merged.Add(e.Key);
                }
                if (parts.Count == 0) continue;
                var mesh = new Mesh { name = "Merged_" + kv.Key.mat.name + "_" + made };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(parts.ToArray(), true, true);
                var go = new GameObject("Merged_" + kv.Key.mat.name);
                go.transform.SetParent(kv.Key.anchor.transform, false);
                go.layer = kv.Key.layer;
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)kv.Key.flags);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = kv.Key.mat;
                r.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode)kv.Key.shadow;
                r.receiveShadows = kv.Key.receive != 0;
                if (kv.Key.lod != null)
                {
                    List<Renderer> list;
                    if (!newByLod.TryGetValue(kv.Key.lod, out list)) newByLod[kv.Key.lod] = list = new List<Renderer>();
                    list.Add(r);
                }
                made++;
            }

            // Point each chunk at its merged meshes (and whatever of it wasn't merged).
            foreach (var kv in newByLod)
            {
                var keep = new List<Renderer>(kv.Value);
                foreach (var r in kv.Key.GetLODs()[0].renderers)
                    if (r != null && !(r is MeshRenderer && merged.Contains((MeshRenderer)r))) keep.Add(r);
                kv.Key.SetLODs(new[] { new LOD(lodHeight[kv.Key], keep.ToArray()) });
            }

            // Strip the merged pieces; empty leftovers nothing points at go entirely.
            int removed = 0;
            foreach (var mr in merged)
            {
                GameObject go = mr.gameObject;
                var mf = go.GetComponent<MeshFilter>();
                Object.DestroyImmediate(mr);
                if (mf != null) Object.DestroyImmediate(mf);
                removed++;
                if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1 && !referenced.Contains(go)) Object.DestroyImmediate(go);
            }
            Debug.Log("[WizardsVRC] Merged " + removed + " static scenery renderers into " + made + " meshes (" + before + " mesh renderers before, " +
                      (before - removed + made) + " after).");
            return removed - made;
        }
    }
}
