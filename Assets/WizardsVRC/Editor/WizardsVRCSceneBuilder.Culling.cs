using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Client-side culling, so a player only pays for what's near them:
    ///  1. Area culling (AreaCuller, at runtime): the hub, the boss arena and every street zone are switched off locally
    ///     while the player isn't inside them (no rendering, physics or scripts). Teleports switch the destination on first.
    ///  2. Draw distance: the camera's far clip sits just past the fog, so nothing fully fogged is drawn.
    ///  3. Detail culling: small static props (lamps, windows, flowers, merlons, stalls...) are grouped into 32 m chunks with
    ///     an LODGroup that stops drawing the chunk beyond the zone's Detail Distance.
    /// Every zone built from world data gets all three automatically.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float CullChunk = 32f;
        /// <summary>Props up to this size (metres, largest side) are "detail" and get distance culling.</summary>
        private const float DetailMaxSize = 3.5f;
        private const float DefaultDetailDistance = 80f;

        /// <summary>Far clip just past the fog end: anything further is fully fogged anyway.</summary>
        private static float DrawDistance()
        {
            return RenderSettings.fog ? RenderSettings.fogEndDistance + 50f : 600f;
        }

        /// <summary>Groups an area's small static renderers into chunked LODGroups that cull beyond detailDistance.</summary>
        private static void AddDistanceCulling(Transform root, float detailDistance)
        {
            if (root == null) return;
            detailDistance = Mathf.Max(10f, detailDistance);
            var buckets = new Dictionary<Vector2Int, List<Renderer>>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || IsDynamic(r.transform, root)) continue;
                Bounds b = r.bounds;
                float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (size > DetailMaxSize || size <= 0f) continue;
                var key = new Vector2Int(Mathf.FloorToInt(b.center.x / CullChunk), Mathf.FloorToInt(b.center.z / CullChunk));
                List<Renderer> list;
                if (!buckets.TryGetValue(key, out list)) buckets[key] = list = new List<Renderer>();
                list.Add(r);
            }
            // Unity culls an LOD when (size / distance) / (2 tan(fov/2)) drops below the transition height. Assuming a
            // ~90 degree VR field of view (tan = 1) and LOD bias 1, this culls at about detailDistance.
            float h = Mathf.Clamp(CullChunk / (2f * detailDistance), 0.01f, 0.9f);
            int groups = 0, renderers = 0;
            foreach (var kv in buckets)
            {
                float y = 0f;
                foreach (var r in kv.Value) y += r.bounds.center.y;
                y /= kv.Value.Count;
                var go = new GameObject("LOD_" + kv.Key.x + "_" + kv.Key.y);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3((kv.Key.x + 0.5f) * CullChunk, y, (kv.Key.y + 0.5f) * CullChunk);
                var lod = go.AddComponent<LODGroup>();
                lod.fadeMode = LODFadeMode.None;
                lod.SetLODs(new[] { new LOD(h, kv.Value.ToArray()) });
                lod.localReferencePoint = Vector3.zero;
                lod.size = CullChunk;
                groups++;
                renderers += kv.Value.Count;
            }
            if (groups > 0) Debug.Log("[WizardsVRC] " + root.name + ": " + renderers + " small props in " + groups + " culling chunks (hidden beyond ~" + detailDistance + " m).");
        }

        /// <summary>Anything scripts move, show or hide (patrols, rings, quest objects, NPCs, gates) is left alone.</summary>
        private static bool IsDynamic(Transform t, Transform root)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
            {
                // Behaviours, moving things (pickups) and mirrors never go into the static distance-culling groups.
                if (p.GetComponent<UdonBehaviour>() != null || p.GetComponent<LODGroup>() != null || p.GetComponent<Rigidbody>() != null) return true;
                string n = p.name;
                if (n.StartsWith("Encounter") || n.StartsWith("Patrol_") || n.StartsWith("RingSpots_") || n.StartsWith("Point_") ||
                    n.StartsWith("NPC_") || n.StartsWith("Classroom_") || n.StartsWith("Gate_") || n.StartsWith("LOD_") ||
                    n.StartsWith("Mirror"))
                    return true;
            }
            return false;
        }

        /// <summary>Bounds (world space) of a zone root built at its height: the map plus a margin, 50 m below to 60 m above.</summary>
        private static Bounds ZoneCullBounds(Vector3 rootPos, float width, float depth)
        {
            const float below = 50f, above = 60f, margin = 20f;
            return new Bounds(rootPos + new Vector3(0, (above - below) / 2f, 0), new Vector3(width + margin * 2f, below + above, depth + margin * 2f));
        }

        /// <summary>The runtime AreaCuller, wired to every teleporter and battle client so destinations load before you land.</summary>
        private static AreaCuller BuildAreaCuller(Transform systems, List<GameObject> areas, List<Bounds> bounds, Scene scene, List<BattleClient> encounters)
        {
            var culler = UdonSharpUndo.AddComponent<AreaCuller>(Child(systems, "AreaCuller", Vector3.zero).gameObject);
            culler.areas = areas.ToArray();
            culler.boundsMin = new Vector3[bounds.Count];
            culler.boundsMax = new Vector3[bounds.Count];
            for (int i = 0; i < bounds.Count; i++)
            {
                culler.boundsMin[i] = bounds[i].min;
                culler.boundsMax[i] = bounds[i].max;
            }
            foreach (var go in scene.GetRootGameObjects())
                foreach (var tp in go.GetComponentsInChildren<TeleportHelper>(true))
                    tp.culler = culler;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var tp in go.GetComponentsInChildren<TunnelPassage>(true))
                    tp.culler = culler;
            foreach (var ec in encounters) ec.culler = culler;
            var names = new List<string>();
            foreach (var a in areas) names.Add(a.name);
            Debug.Log("[WizardsVRC] Area culling: " + string.Join(", ", names) + " switch off locally when you're not in them. Draw distance " + DrawDistance() + " m.");
            return culler;
        }
    }
}
