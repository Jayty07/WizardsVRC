using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Starfall Ward: the great protective dome of light over World 1 (the castle and every street round it). Fed by
    /// the Starfall Crystal, it is why nothing from beyond the Everweave has ever reached these isles; a lattice of glowing
    /// hexagons across the whole sky, shimmering, with pulses rolling down from its crown and a ring of runes turning where
    /// it meets the horizon (WizardsVRC/WardDome shader, animated on the GPU: no scripts).
    ///
    /// Story (see README, "The Starfall Ward"): World 1 introduces it as something everyone takes for granted. As the
    /// crystal fades it becomes the heart of World 3's story (the ward weakening, cracks appearing, the hunt for a way to
    /// mend it), and its failure opens World 4 with the invasion of the hub. The shader's _Crack (0 whole .. 1 failing)
    /// is there for those chapters: fractures spread, the light gutters and turns red.
    ///
    /// Each World 1 place gets its own copy of the dome (they're far apart and each is drawn on its own), a hemisphere
    /// just inside the draw distance centred on the place, so the ward always fills the sky overhead and comes down out
    /// of sight into the haze at the horizon.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>The ward over every World 1 place (the hub and its streets; not the later worlds).</summary>
        private static void BuildWardDomes(List<SkyArea> areas)
        {
            if (areas == null) return;
            var shader = Shader.Find("WizardsVRC/WardDome");
            if (shader == null) { Debug.LogError("[WizardsVRC] Shader WizardsVRC/WardDome is missing; no Starfall Ward over World 1."); return; }
            Directory.CreateDirectory(MaterialsDir);
            string matPath = MaterialsDir + "/StarfallWard.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.SetColor("_Color", new Color(0.42f, 0.72f, 1f));
            mat.SetColor("_Glow", new Color(0.92f, 0.86f, 1f));
            mat.SetFloat("_Strength", 0.9f);
            mat.SetFloat("_Cells", 22f);
            mat.SetFloat("_PulseEvery", 9f);
            mat.SetFloat("_Crack", 0f);   // whole, for now (World 3 and 4 will change that)
            EditorUtility.SetDirty(mat);
            Mesh dome = WardDomeMesh();
            // Just inside the draw distance, so it's never clipped; the shader ignores fog, so it shows through the haze.
            float radius = Mathf.Max(150f, DrawDistance() - 25f);
            int n = 0;
            foreach (var a in areas)
            {
                if (a == null || a.root == null || a.world != 1) continue;
                var go = new GameObject("StarfallWard");
                go.transform.SetParent(a.root.transform, false);
                go.transform.localPosition = new Vector3(0f, -8f, 0f);
                go.transform.localScale = Vector3.one * radius;
                go.AddComponent<MeshFilter>().sharedMesh = dome;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.allowOcclusionWhenDynamic = false;
                go.isStatic = false;   // not merged, not occlusion culled
                n++;
            }
            Debug.Log("[WizardsVRC] The Starfall Ward over " + n + " World 1 places (radius " + radius.ToString("0") + " m).");
        }

        /// <summary>A unit hemisphere (radius 1, faces inward) with a short skirt below the horizon, finely divided round.</summary>
        private static Mesh WardDomeMesh()
        {
            var mb = new MeshBuilder();
            const int lon = 64, lat = 18;
            // Rows from a little below the horizon (-6 degrees) up to the crown.
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    Vector3 a = WardPoint(i, j, lon, lat), b = WardPoint(i + 1, j, lon, lat);
                    Vector3 c = WardPoint(i + 1, j + 1, lon, lat), d = WardPoint(i, j + 1, lon, lat);
                    Vector3 mid = (a + b + c + d) * 0.25f;
                    mb.Quad(a, b, c, d, -mid);
                }
            return SaveBuiltMesh("PSX_StarfallWard", mb);
        }

        private static Vector3 WardPoint(int i, int j, int lon, int lat)
        {
            float u = i / (float)lon * Mathf.PI * 2f;
            float el = Mathf.Lerp(-6f, 90f, j / (float)lat) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(el) * Mathf.Cos(u), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(u));
        }
    }
}
