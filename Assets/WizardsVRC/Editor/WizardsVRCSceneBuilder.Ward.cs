using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Starfall Ward: the great protective dome of light over World 1, raised by the Starfall Crystal. It is one dome
    /// for the whole world, centred on the crystal in the castle and big enough to cover every street round it: from the
    /// castle its crown stands straight overhead; from a street it stands off over the castle, its wall sweeping down out
    /// of the sky behind you, and the crystal's beam climbs to its crown on the horizon. A lattice of glowing hexagons
    /// shifting through blue, violet and gold, aurora curtains, twinkling starlight, pulses spreading out from the crown and
    /// a ring of runes turning where it meets the ground (WizardsVRC/WardDome shader, animated on the GPU: no scripts).
    ///
    /// Story (see README, "The Starfall Ward", and the doc "The Everweave: Main Story Plan"): World 1 introduces it as
    /// something everyone takes for granted. In World 3 the Dominion drains Bigdig's Mother Lode and the ward cracks as
    /// its storylines finish; it falls in World 3's finale, the siege of Starfall. The shader's _Crack (0 whole ..
    /// 1 failing) is there for those chapters: fractures spread, the light gutters and turns red.
    ///
    /// Each World 1 place draws the dome on its own (they're far apart and each is switched on alone): a hemisphere that
    /// follows the camera just inside the draw distance, whose pixels trace out to the one big dome where it really stands
    /// relative to that place in the world's layout (the castle in the middle, each street out along its tunnel, Horizon
    /// Distance away; see LayOutWorld). Each place has its own material for that (StarfallWard_*.mat).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>Metres of dome past the farthest World 1 street (so even it stands well inside).</summary>
        private const float WardMargin = 420f;

        /// <summary>The ward over every World 1 place (the hub and its streets; not the later worlds).</summary>
        private static void BuildWardDomes(List<SkyArea> areas)
        {
            if (areas == null) return;
            var shader = Shader.Find("WizardsVRC/WardDome");
            if (shader == null) { Debug.LogError("[WizardsVRC] Shader WizardsVRC/WardDome is missing; no Starfall Ward over World 1."); return; }
            Directory.CreateDirectory(MaterialsDir);
            Mesh dome = WardDomeMesh();
            // The crystal: the hub's fountain if it has one, else the castle's middle (world layout coordinates = hub local).
            Vector2 crystal = Vector2.zero;
            float far = 0f;
            foreach (var a in areas)
            {
                if (a == null || a.world != 1) continue;
                if (a.hub && a.zone != null && a.zone.landmarks != null)
                {
                    var g = new ZoneGrid(a.zone);
                    foreach (var l in a.zone.landmarks)
                    {
                        if (l == null || l.kind != LandmarkKind.Fountain) continue;
                        var cells = g.Find(l.Symbol);
                        if (cells.Count == 0) continue;
                        Vector3 sum = Vector3.zero;
                        foreach (var c in cells) sum += g.Local(c);
                        sum /= cells.Count;
                        crystal = new Vector2(sum.x, sum.z);
                        break;
                    }
                }
                if (!a.hub) far = Mathf.Max(far, a.pos.magnitude + a.radius);
            }
            float radius = Mathf.Max(900f, far + WardMargin);
            // The camera-following hemisphere is drawn just inside the draw distance (never clipped, never over anything near).
            float screen = Mathf.Max(120f, DrawDistance() - 30f);
            int n = 0;
            foreach (var a in areas)
            {
                if (a == null || a.root == null || a.world != 1) continue;
                // Where the crystal lies from here: its bearing in the world's layout, turned into this place's own frame.
                Vector2 to = crystal - a.pos;
                float worldBearing = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
                float local = (worldBearing - a.rot) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Sin(local), 0f, Mathf.Cos(local)) * to.magnitude;
                Vector3 centre = a.root.transform.TransformPoint(offset + new Vector3(0f, -8f, 0f));

                string matPath = MaterialsDir + "/StarfallWard_" + a.key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = shader;
                mat.SetColor("_Color", new Color(0.42f, 0.72f, 1f));
                mat.SetColor("_Glow", new Color(0.92f, 0.86f, 1f));
                mat.SetColor("_Aurora", new Color(0.62f, 0.38f, 1f));
                mat.SetColor("_Gold", new Color(1f, 0.84f, 0.48f));
                mat.SetFloat("_Strength", 0.95f);
                mat.SetFloat("_Cells", 30f);
                mat.SetFloat("_PulseEvery", 9f);
                mat.SetFloat("_Crack", 0f);   // whole, for now (World 3's last chapter will change that)
                mat.SetVector("_Centre", new Vector4(centre.x, centre.y, centre.z, 0f));
                mat.SetFloat("_Radius", radius);
                mat.SetFloat("_Screen", screen);
                mat.SetFloat("_BeamWidth", 7f);
                EditorUtility.SetDirty(mat);

                var go = new GameObject("StarfallWard");
                go.transform.SetParent(a.root.transform, false);
                go.transform.localPosition = new Vector3(0f, -8f, 0f);
                // Its bounds (all the shader cares about is that it's drawn while you're in the place) cover the place.
                go.transform.localScale = Vector3.one * Mathf.Max(screen, a.radius + 60f);
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
            Debug.Log("[WizardsVRC] The Starfall Ward over " + n + " World 1 places: one dome of radius " + radius.ToString("0") +
                      " m centred on the Starfall Crystal.");
        }

        /// <summary>A unit hemisphere (radius 1, faces inward) with a skirt below the horizon, finely divided round.</summary>
        private static Mesh WardDomeMesh()
        {
            var mb = new MeshBuilder();
            const int lon = 64, lat = 18;
            // Rows from below the horizon (-15 degrees) up to the crown.
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
            float el = Mathf.Lerp(-15f, 90f, j / (float)lat) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(el) * Mathf.Cos(u), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(u));
        }
    }
}
