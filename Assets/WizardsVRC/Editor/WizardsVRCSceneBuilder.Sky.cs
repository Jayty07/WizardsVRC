using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Skyboxes that line up with the world. The castle sits at the middle of a sea; every street lies out along the line
    /// of its tunnel, Horizon Distance away (Zone > Sky), and is turned so that its own tunnel points back at the castle.
    /// Each area gets a PSXSkyline skybox whose pixel-art skyline strip shows the others in the direction they really
    /// are: the castle (round wall, towers, the halls' roofs, the keep, the tavern's cone and the Starfall Crystal's
    /// beam) with the mountains over its tunnels, garden isles with giant flowers, lamplit towns, plus far islands, the
    /// sea and clouds. The AreaCuller swaps skyboxes and fog colours as you move between areas.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string SkyTexDir = TexturesDir + "/Sky";

        /// <summary>An area with a sky of its own and where it lies in the world.</summary>
        private class SkyArea
        {
            public string key;
            public ZoneDefinition zone;
            public GameObject root;
            public bool hub;
            /// <summary>Position in the world's layout (m): the castle at the origin, +Y = north.</summary>
            public Vector2 pos;
            /// <summary>World bearing = this area's own (local) bearing + rot.</summary>
            public float rot;
            public float radius;
            public bool garden;
            /// <summary>Streets: bearing (from the castle's middle) of the castle's tunnel to here, and of this street's own
            /// tunnel from its middle. The street lies out along the first; it's turned so the second points home.</summary>
            public float hubBearing, ownBearing;
            /// <summary>Hub only: the mountains over its tunnels, in world layout positions.</summary>
            public readonly List<Vector2> mountains = new List<Vector2>();
            public Material sky;
        }

        private static float BearingOf(Vector3 local) { return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; }

        /// <summary>Gardens if the map is mostly lawn, trees and hedges; otherwise a town.</summary>
        private static bool IsGardenZone(ZoneGrid g)
        {
            int green = 0, built = 0;
            for (int y = 1; y < g.height - 1; y++)
                for (int x = 1; x < g.width - 1; x++)
                {
                    char c = g.At(x, y);
                    if (c == ',' || c == '^' || c == '=') green++;
                    else if (c == '#') built++;
                }
            return green > built;
        }

        /// <summary>
        /// Where everything lies: the castle at the middle; each street out along the line of its tunnel from the castle's
        /// middle, Horizon Distance away, turned so its own tunnel points back at the castle.
        /// </summary>
        private static List<SkyArea> LayOutWorld(HubLayout hub, Transform hubRoot, WorldBuild wb)
        {
            var list = new List<SkyArea>();
            if (hub.zone == null) return list;
            var g = hub.grid;
            var h = new SkyArea
            {
                key = hub.zone.zoneKey, zone = hub.zone, root = hubRoot.gameObject, hub = true,
                pos = Vector2.zero, rot = 0f, radius = Mathf.Min(g.width, g.height) * g.cell / 2f,
            };
            foreach (var gate in hub.gates.Values)
            {
                Vector3 m = hubRoot.InverseTransformPoint(gate.tunnel.mountainCentre);
                h.mountains.Add(new Vector2(m.x, m.z));
            }
            list.Add(h);
            foreach (var a in wb.skyAreas)
            {
                float d = Mathf.Max(200f, a.zone.horizonDistance), t = a.hubBearing * Mathf.Deg2Rad;
                a.pos = new Vector2(Mathf.Sin(t) * d, Mathf.Cos(t) * d);
                a.rot = a.hubBearing + 180f - a.ownBearing;
                list.Add(a);
            }
            return list;
        }

        /// <summary>
        /// Makes every area's skybox and hands them (and the fog colours) to the AreaCuller, in the same order as its
        /// areas. Areas without a sky of their own (the boss arena) keep the default.
        /// </summary>
        private static void BuildSkies(List<SkyArea> skyAreas, AreaCuller culler, Material defaultSky)
        {
            if (skyAreas.Count == 0 || culler == null) return;
            var shader = Shader.Find("WizardsVRC/PSXSkyline");
            if (shader == null) { Debug.LogError("[WizardsVRC] Shader WizardsVRC/PSXSkyline is missing; areas keep the plain sky."); return; }
            Directory.CreateDirectory(SkyTexDir);
            Directory.CreateDirectory(MaterialsDir);
            foreach (var a in skyAreas)
            {
                var strip = PaintSkyline(a, skyAreas);
                string texPath = SkyTexDir + "/Skyline_" + a.key + ".png";
                File.WriteAllBytes(texPath, strip.EncodeToPNG());
                Object.DestroyImmediate(strip);
                AssetDatabase.ImportAsset(texPath);
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.wrapModeU = TextureWrapMode.Repeat;
                    importer.wrapModeV = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 4096;
                    importer.SaveAndReimport();
                }
                string matPath = MaterialsDir + "/Sky_" + a.key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = shader;
                mat.SetTexture("_Skyline", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                mat.SetColor("_TopColor", a.zone.skyTopColor);
                mat.SetColor("_HorizonColor", a.zone.skyHorizonColor);
                mat.SetColor("_BottomColor", new Color(0.06f, 0.2f, 0.52f));
                mat.SetFloat("_Low", SkyLow);
                mat.SetFloat("_High", SkyHigh);
                EditorUtility.SetDirty(mat);
                a.sky = mat;
            }
            var skies = new Material[culler.areas.Length];
            var fogs = new Color[culler.areas.Length];
            for (int i = 0; i < culler.areas.Length; i++)
                foreach (var a in skyAreas)
                    if (a.root == culler.areas[i]) { skies[i] = a.sky; fogs[i] = a.zone.fogColor; fogs[i].a = 1f; }
            culler.skies = skies;
            culler.fogColors = fogs;
            culler.defaultSky = defaultSky;
            culler.defaultFog = RenderSettings.fogColor;
            Debug.Log("[WizardsVRC] Skylines painted for " + skyAreas.Count + " area(s) in " + SkyTexDir + ".");
        }

        // ================================================================== the skyline painter

        private const int SkyW = 2048, SkyH = 128;
        private const float SkyLow = -6f, SkyHigh = 16f;

        /// <summary>A strip of pixels round the horizon: x = bearing (0 = north, clockwise), y = elevation.</summary>
        private class Strip
        {
            public readonly Color[] px = new Color[SkyW * SkyH];
            public Color horizon, sea;

            public static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
            public int X(float bearing) { return Mod(Mathf.RoundToInt(bearing / 360f * SkyW), SkyW); }
            public int Y(float elev) { return Mathf.RoundToInt((elev - SkyLow) / (SkyHigh - SkyLow) * SkyH); }
            public static float Elev(int y) { return SkyLow + (y + 0.5f) / SkyH * (SkyHigh - SkyLow); }

            public void Set(int x, int y, Color c, float a)
            {
                if (y < 0 || y >= SkyH || a <= 0f) return;
                int i = y * SkyW + Mod(x, SkyW);
                Color o = px[i];
                if (a >= 1f || o.a <= 0f) px[i] = new Color(c.r, c.g, c.b, Mathf.Min(1f, a));
                else px[i] = new Color(Mathf.Lerp(o.r, c.r, a), Mathf.Lerp(o.g, c.g, a), Mathf.Lerp(o.b, c.b, a), Mathf.Max(o.a, a));
            }

            public void Column(int x, float e0, float e1, Color c, float a = 1f)
            {
                for (int y = Y(e0); y < Y(e1); y++) Set(x, y, c, a);
            }

            /// <summary>Columns covering bearings b0..b1, with the bearing of each.</summary>
            public IEnumerable<KeyValuePair<int, float>> Cols(float b0, float b1)
            {
                int x0 = Mathf.FloorToInt(b0 / 360f * SkyW), x1 = Mathf.CeilToInt(b1 / 360f * SkyW);
                for (int x = x0; x <= x1; x++) yield return new KeyValuePair<int, float>(x, x * 360f / SkyW);
            }

            public Color Haze(Color c, float k) { return Color.Lerp(c, horizon, k); }
        }

        private static Color Rgb(float r, float g, float b) { return new Color(r, g, b, 1f); }

        /// <summary>0 at the ends of [-1, 1], 1 in the middle, with steep cliff-like ends.</summary>
        private static float Hump(float t, float shoulder)
        {
            float a = Mathf.Abs(t);
            if (a >= 1f) return 0f;
            return Mathf.Pow(Mathf.Min(1f, (1f - a) / shoulder), 0.7f);
        }

        private static int StableSeed(string s)
        {
            unchecked
            {
                int h = (int)2166136261u;
                foreach (char ch in s ?? "") { h ^= ch; h *= 16777619; }
                return h & 0x7fffffff;
            }
        }

        private static Texture2D PaintSkyline(SkyArea me, List<SkyArea> all)
        {
            var s = new Strip { horizon = me.zone.skyHorizonColor, sea = Rgb(0.12f, 0.36f, 0.78f) };
            var rng = new System.Random(StableSeed(me.key));
            PaintSea(s, rng);
            PaintClouds(s, rng, Color.Lerp(me.zone.skyHorizonColor, Color.white, 0.5f));

            // Everything else in the world, as seen from here, furthest first.
            var items = new List<KeyValuePair<float, System.Action>>();
            var taken = new List<Vector2>();
            foreach (var o in all)
            {
                if (o == me) continue;
                Vector2 d = o.pos - me.pos;
                float dist = Mathf.Max(50f, d.magnitude);
                float bearing = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg - me.rot;
                float hw = Mathf.Clamp(Mathf.Atan(o.radius / dist) * Mathf.Rad2Deg, 3f, 14f);
                float k = Mathf.Clamp(0.12f + dist / 2800f, 0.28f, 0.8f);
                float size = Mathf.Clamp(1100f / dist, 0.7f, 1.3f);
                taken.Add(new Vector2(bearing, hw));
                var other = o;
                if (o.hub)
                {
                    items.Add(new KeyValuePair<float, System.Action>(dist, () => PaintCastle(s, bearing, hw, k, size, other.zone)));
                    foreach (var m in o.mountains)
                    {
                        Vector2 dm = m - me.pos;
                        float mDist = Mathf.Max(50f, dm.magnitude);
                        float mb = Mathf.Atan2(dm.x, dm.y) * Mathf.Rad2Deg - me.rot;
                        float mhw = Mathf.Clamp(Mathf.Atan(55f / mDist) * Mathf.Rad2Deg, 1.5f, 10f);
                        float mk = Mathf.Clamp(0.12f + mDist / 2800f, 0.28f, 0.8f);
                        items.Add(new KeyValuePair<float, System.Action>(mDist, () => PaintPeaks(s, mb, mhw, mk, size)));
                    }
                }
                else if (o.garden)
                {
                    var z = o.zone;
                    int seed = StableSeed(o.key);
                    items.Add(new KeyValuePair<float, System.Action>(dist, () => PaintGarden(s, bearing, hw, k, size, z.grassColor, z.treeColor, z.accentColor, seed)));
                }
                else
                {
                    var z = o.zone;
                    int seed = StableSeed(o.key);
                    items.Add(new KeyValuePair<float, System.Action>(dist, () => PaintTown(s, bearing, hw, k, size, z.buildingColor, z.roofColor, z.accentColor, seed)));
                }
            }
            PaintIsles(s, rng, taken);
            items.Sort((a, b) => b.Key.CompareTo(a.Key));
            foreach (var it in items) it.Value();

            var tex = new Texture2D(SkyW, SkyH, TextureFormat.RGBA32, false);
            var cols = new Color32[s.px.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = s.px[i];
            tex.SetPixels32(cols);
            tex.Apply();
            return tex;
        }

        /// <summary>The sea below the horizon, paler at the horizon, with a scatter of glints.</summary>
        private static void PaintSea(Strip s, System.Random rng)
        {
            int top = s.Y(0f);
            for (int y = 0; y < top; y++)
            {
                float e = Strip.Elev(y);
                float t = Mathf.Min(1f, -e / 4f);
                Color c = Color.Lerp(Color.Lerp(s.sea, s.horizon, 0.55f), s.sea, t);
                for (int x = 0; x < SkyW; x++)
                    s.Set(x, y, rng.NextDouble() < 0.012 && e > -3f ? Color.Lerp(c, Color.white, 0.5f) : c, 1f);
            }
        }

        private static void PaintClouds(Strip s, System.Random rng, Color color)
        {
            for (int i = 0; i < 10; i++)
            {
                float b = (float)rng.NextDouble() * 360f, e = 7f + (float)rng.NextDouble() * 7f;
                float w = 6f + (float)rng.NextDouble() * 10f, h = 0.8f + (float)rng.NextDouble();
                foreach (var col in s.Cols(b - w, b + w))
                {
                    float t = (col.Value - b) / w;
                    float top = h * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * (0.75f + 0.25f * Mathf.Sin(col.Value * 1.7f + i));
                    if (top <= 0.05f) continue;
                    for (int y = s.Y(e - top * 0.35f); y < s.Y(e + top); y++)
                    {
                        float shade = Strip.Elev(y) < e ? 0.93f : 1f;
                        s.Set(col.Key, y, Color.Lerp(color, new Color(shade, shade, shade), 0.6f), 0.85f);
                    }
                }
            }
        }

        /// <summary>Low hazy islands round the horizon, away from the places that are really there.</summary>
        private static void PaintIsles(Strip s, System.Random rng, List<Vector2> taken)
        {
            Color land = Rgb(0.4f, 0.5f, 0.45f);
            for (int i = 0; i < 9; i++)
            {
                float b = (float)rng.NextDouble() * 360f;
                float w = 2f + (float)rng.NextDouble() * 4f, h = 0.4f + (float)rng.NextDouble();
                float k = 0.55f + (float)rng.NextDouble() * 0.25f;
                bool clear = true;
                foreach (var tk in taken) if (Mathf.Abs(Mathf.DeltaAngle(b, tk.x)) < tk.y + w + 6f) clear = false;
                if (!clear) continue;
                Color c = s.Haze(land, k);
                foreach (var col in s.Cols(b - w, b + w))
                {
                    float t = (col.Value - b) / w;
                    float top = h * Mathf.Pow(Mathf.Max(0f, 1f - t * t), 0.6f) * (0.85f + 0.15f * Mathf.Sin(col.Value * 3.1f + i));
                    if (top > 0f) s.Column(col.Key, -0.3f, top, c);
                }
            }
        }

        /// <summary>Snowy twin peaks with a pass between them (the mountains over a tunnel).</summary>
        private static void PaintPeaks(Strip s, float b, float hw, float k, float size)
        {
            Color rock = s.Haze(Rgb(0.5f, 0.45f, 0.48f), k), snow = s.Haze(Rgb(0.96f, 0.96f, 1f), k);
            float hMax = 7f * size;
            foreach (var col in s.Cols(b - hw, b + hw))
            {
                float t = (col.Value - b) / hw;
                float h = Mathf.Max(Hump((t + 0.45f) / 0.55f, 0.9f) * hMax, Hump((t - 0.5f) / 0.5f, 0.9f) * hMax * 0.85f);
                h = Mathf.Max(h, Hump(t, 0.3f) * hMax * 0.25f);
                if (h <= 0.05f) continue;
                s.Column(col.Key, -0.3f, h, rock);
                if (h > hMax * 0.62f) s.Column(col.Key, hMax * 0.62f + (col.Key % 3) * 0.12f, h, snow);
            }
        }

        /// <summary>The castle side-on: island, round wall with towers, hall roofs, the keep, the tavern and the crystal's beam.</summary>
        private static void PaintCastle(Strip s, float b, float hw, float k, float size, ZoneDefinition z)
        {
            Color st = s.Haze(z.buildingColor, k), rf = s.Haze(z.roofColor, k), cliff = s.Haze(Rgb(0.55f, 0.42f, 0.36f), k);
            Color grass = s.Haze(z.grassColor, k), gold = s.Haze(Rgb(1f, 0.78f, 0.2f), k);
            Color beam = Rgb(0.55f, 0.75f, 1f);
            System.Func<float, float> pos = f => b + f * hw;
            foreach (var col in s.Cols(b - hw * 1.12f, b + hw * 1.12f))
            {
                float top = 0.9f * size * Hump((col.Value - b) / (hw * 1.12f), 0.2f);
                if (top <= 0f) continue;
                s.Column(col.Key, -0.3f, top, cliff);
                s.Column(col.Key, top - 0.15f, top, grass);
            }
            float wallTop = 2.8f * size, towerTop = 4f * size;
            foreach (var col in s.Cols(b - hw * 0.96f, b + hw * 0.96f))
            {
                s.Column(col.Key, 0.7f * size, wallTop, st);
                if (Strip.Mod(col.Key, 3) == 0) s.Column(col.Key, wallTop, wallTop + 0.25f, st);
            }
            float[] halls = { -0.62f, -0.42f, -0.22f, 0.24f, 0.44f, 0.64f };
            for (int j = 0; j < halls.Length; j++)
            {
                Color c = s.Haze(SchoolColors[j % SchoolColors.Length], Mathf.Min(0.9f, k + 0.1f));
                float w = hw * 0.07f, hx = pos(halls[j]);
                foreach (var col in s.Cols(hx - w, hx + w))
                    s.Column(col.Key, wallTop, wallTop + 1.4f * size * (1f - Mathf.Abs(col.Value - hx) / w), c);
            }
            float kx = pos(-0.08f), kw = hw * 0.16f;
            foreach (var col in s.Cols(kx - kw, kx + kw)) s.Column(col.Key, wallTop, 5.6f * size, st);
            foreach (float f in new[] { -1f, 1f })
            {
                float tx = kx + f * kw;
                foreach (var col in s.Cols(tx - 0.35f, tx + 0.35f))
                {
                    s.Column(col.Key, wallTop, 6.6f * size, st);
                    s.Column(col.Key, 6.6f * size, (6.6f + 1.3f * (1f - Mathf.Abs(col.Value - tx) / 0.45f)) * size, rf);
                }
            }
            float cx = pos(0.12f), cw = hw * 0.14f;
            foreach (var col in s.Cols(cx - cw, cx + cw))
            {
                s.Column(col.Key, wallTop, 4.2f * size, s.Haze(Rgb(0.93f, 0.86f, 0.72f), k));
                s.Column(col.Key, 4.2f * size, (4.2f + 2.6f * (1f - Mathf.Abs(col.Value - cx) / cw)) * size, rf);
            }
            // The Starfall Crystal's beam out of the tavern's open roof.
            foreach (var col in s.Cols(cx - 0.18f, cx + 0.18f))
                for (int y = s.Y(6f * size); y < SkyH; y++)
                    s.Set(col.Key, y, beam, Mathf.Max(0f, 0.8f - (Strip.Elev(y) - 6f * size) / 12f));
            foreach (float f in new[] { -0.96f, -0.66f, -0.36f, 0.38f, 0.68f, 0.96f })
            {
                float tx = pos(f);
                foreach (var col in s.Cols(tx - 0.42f, tx + 0.42f))
                {
                    s.Column(col.Key, 0.7f * size, towerTop, st);
                    s.Column(col.Key, towerTop, towerTop + 1f * size * (1f - Mathf.Abs(col.Value - tx) / 0.5f), rf);
                }
                s.Column(s.X(tx), towerTop + 1f * size, towerTop + 1.5f * size, gold);
            }
        }

        /// <summary>A garden isle: rolling green hills, trees, giant flowers in the zone's accent colour and a gazebo.</summary>
        private static void PaintGarden(Strip s, float b, float hw, float k, float size, Color grass, Color tree, Color accent, int seed)
        {
            var r = new System.Random(seed);
            Color gr = s.Haze(grass, k), tr = s.Haze(tree, Mathf.Min(0.9f, k + 0.05f)), ac = s.Haze(accent, k * 0.8f), cliff = s.Haze(Rgb(0.5f, 0.4f, 0.32f), k);
            Color heart = s.Haze(Rgb(1f, 0.9f, 0.3f), k * 0.8f);
            var tops = new Dictionary<int, float>();
            foreach (var col in s.Cols(b - hw, b + hw))
            {
                float t = (col.Value - b) / hw;
                float top = Mathf.Max(0f, (3f + 1.4f * Mathf.Sin(col.Value * 0.9f + 1f) + 0.7f * Mathf.Sin(col.Value * 2.3f)) * size * Hump(t, 0.35f));
                tops[Strip.Mod(col.Key, SkyW)] = top;
                if (top <= 0f) continue;
                s.Column(col.Key, -0.3f, top * 0.35f, cliff);
                s.Column(col.Key, top * 0.35f, top, gr);
            }
            System.Func<float, float> baseAt = bx => { float v; return tops.TryGetValue(s.X(bx), out v) ? v : 0f; };
            int n = Mathf.RoundToInt(hw * 1.6f);
            for (int i = 0; i < n; i++)
            {
                float bx = b + ((float)r.NextDouble() * 1.7f - 0.85f) * hw;
                float bs = baseAt(bx);
                if (bs <= 0.3f) continue;
                if (r.NextDouble() < 0.35)
                {
                    float hgt = (2.4f + (float)r.NextDouble() * 2.2f) * size, rad = 0.5f + (float)r.NextDouble() * 0.4f;
                    s.Column(s.X(bx), bs, bs + hgt, tr);
                    foreach (var col in s.Cols(bx - rad, bx + rad))
                    {
                        float dx = (col.Value - bx) / rad;
                        float hh = rad * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx));
                        s.Column(col.Key, bs + hgt - hh, bs + hgt + hh, ac);
                    }
                    s.Set(s.X(bx), s.Y(bs + hgt), heart, 1f);
                }
                else
                {
                    float rad = 0.3f + (float)r.NextDouble() * 0.3f;
                    foreach (var col in s.Cols(bx - rad, bx + rad))
                    {
                        float dx = (col.Value - bx) / rad;
                        s.Column(col.Key, bs, bs + 0.3f + rad * 1.4f * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx)), tr);
                    }
                }
            }
            float gx = b + hw * 0.1f, gb = baseAt(gx);
            foreach (var col in s.Cols(gx - 0.5f, gx + 0.5f))
            {
                float dx = (col.Value - gx) / 0.5f;
                s.Column(col.Key, gb, gb + 0.5f + 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx)), s.Haze(Rgb(0.95f, 0.93f, 0.88f), k));
            }
        }

        /// <summary>A town on a rocky isle: rooftops, chimneys, lamplit windows and a tall lantern tower.</summary>
        private static void PaintTown(Strip s, float b, float hw, float k, float size, Color bld, Color roof, Color accent, int seed)
        {
            var r = new System.Random(seed);
            Color bc = s.Haze(bld, k), rc = s.Haze(roof, k), lit = s.Haze(accent, k * 0.5f), cliff = s.Haze(Rgb(0.42f, 0.38f, 0.4f), k);
            foreach (var col in s.Cols(b - hw * 1.05f, b + hw * 1.05f))
            {
                float top = 1f * size * Hump((col.Value - b) / (hw * 1.05f), 0.12f);
                if (top > 0f) s.Column(col.Key, -0.3f, top, cliff);
            }
            float bx = b - hw * 0.95f, baseE = 0.9f * size;
            while (bx < b + hw * 0.95f)
            {
                float w = 0.5f + (float)r.NextDouble() * 0.8f;
                float h = (2f + (float)r.NextDouble() * 2.6f) * size * (0.55f + 0.45f * Hump((bx - b) / hw, 0.6f));
                float mid = bx + w / 2f;
                foreach (var col in s.Cols(bx, bx + w))
                {
                    s.Column(col.Key, baseE, baseE + h, bc);
                    s.Column(col.Key, baseE + h, baseE + h + 0.7f * w * (1f - Mathf.Abs(col.Value - mid) / (w / 2f)), rc);
                }
                for (int wy = 0; wy < 2; wy++)
                    for (int wx = 0; wx < (int)(w / 0.35f); wx++)
                        if (r.NextDouble() < 0.45) s.Set(s.X(bx + 0.2f + wx * 0.35f), s.Y(baseE + 0.6f + wy * 0.9f), lit, 1f);
                if (r.NextDouble() < 0.4) s.Column(s.X(bx + w * (0.2f + (float)r.NextDouble() * 0.6f)), baseE + h, baseE + h + 0.9f, bc);
                bx += w + (float)r.NextDouble() * 0.25f;
            }
            float tx = b + hw * 0.3f;
            foreach (var col in s.Cols(tx - 0.3f, tx + 0.3f))
            {
                s.Column(col.Key, baseE, 8.5f * size, bc);
                s.Column(col.Key, 8.5f * size, 9.1f * size, lit);
                s.Column(col.Key, 9.1f * size, (9.1f + 1.2f * (1f - Mathf.Abs(col.Value - tx) / 0.35f)) * size, rc);
            }
        }
    }
}
