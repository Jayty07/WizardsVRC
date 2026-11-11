using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The street dressing's drifting particles (see WizardsVRCSceneBuilder.Dressing.cs): one small looping emitter per
    /// effect, built by StreetFx from a StreetFxSpec, with sprites from Textures/StreetFx (Tools/make_street_fx.py) and
    /// the aura glow and spark. Each street style gets its own mix (DressFx): chimney smoke, moths round the lamps,
    /// falling leaves and fireflies in town; butterflies, petals and pollen in the gardens; sea mist and glints at the
    /// docks; soot, ash and embers in the forge quarter; wisps and ground fog in the keep; blowing sand in Dustwind;
    /// dust, drips and glinting crystals down the mine; stack smoke, steam, sparks, spores and bubbles in Bigdig.
    /// Budgets are small (a few dozen particles each), emitters pause off screen and are culled by distance in 32 m
    /// chunks, and none of it is solid.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class StreetFxSpec
        {
            public string name = "StreetFx";
            public Material mat;
            /// <summary>The box particles appear in (metres), round the emitter.</summary>
            public Vector3 box = Vector3.one;
            public float rate = 2f, life0 = 3f, life1 = 6f;
            public Vector2 size = new Vector2(0.1f, 0.2f);
            /// <summary>Drift, and the random spread added to it per particle.</summary>
            public Vector3 vel, jitter;
            /// <summary>gravity pulls down (1 = falls freely); grow is the size at the end of life against the start;
            /// spin is degrees a second either way; noise makes them wander; alpha is how solid they get.</summary>
            public float gravity, grow = 1f, spin, noise, alpha = 1f;
            /// <summary>Each particle's colour is picked between these two.</summary>
            public Color c0 = Color.white, c1 = Color.white;
            /// <summary>Frames side by side in the sprite, and how many times they play through in a lifetime (wing beats).</summary>
            public int frames = 1, flaps = 1;
        }

        /// <summary>A particle material for a street sprite: additive (glows) or alpha blended (smoke, leaves), tinted by the particles' own colours.</summary>
        private static Material StreetFxMat(string name, string texPath, bool additive)
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/" + name + ".mat";
            Shader sh = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) return SparkleMat(name, Color.white);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp != null && (imp.filterMode != FilterMode.Point || !imp.alphaIsTransparency || imp.mipmapEnabled))
            {
                imp.filterMode = FilterMode.Point;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) Debug.LogWarning("[WizardsVRC] Missing texture " + texPath + " (run Tools/make_street_fx.py).");
            mat.mainTexture = tex;
            mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>One looping emitter at 'at' (local to 'parent'), already running when you arrive.</summary>
        private static ParticleSystem StreetFx(Transform parent, StreetFxSpec s, Vector3 at)
        {
            var go = Child(parent, s.name, at).gameObject;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.life0, s.life1);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(s.size.x, s.size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(s.c0, s.c1);
            if (s.spin > 0f) main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = s.gravity;
            main.maxParticles = Mathf.CeilToInt(s.rate * s.life1) + 4;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var em = ps.emission;
            em.rateOverTime = s.rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = s.box;
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(s.vel.x - s.jitter.x, s.vel.x + s.jitter.x);
            v.y = new ParticleSystem.MinMaxCurve(s.vel.y - s.jitter.y, s.vel.y + s.jitter.y);
            v.z = new ParticleSystem.MinMaxCurve(s.vel.z - s.jitter.z, s.vel.z + s.jitter.z);
            // Fade in and out, so nothing pops.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(s.alpha, 0.2f), new GradientAlphaKey(s.alpha, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            if (Mathf.Abs(s.grow - 1f) > 0.01f)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(s.grow, AnimationCurve.Linear(0f, 1f / s.grow, 1f, 1f));
            }
            if (s.spin > 0f)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-s.spin * Mathf.Deg2Rad, s.spin * Mathf.Deg2Rad);
            }
            if (s.noise > 0f)
            {
                var nz = ps.noise;
                nz.enabled = true;
                nz.strength = s.noise;
                nz.frequency = 0.35f;
                nz.scrollSpeed = 0.2f;
            }
            if (s.frames > 1)
            {
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = s.frames;
                sheet.numTilesY = 1;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                sheet.cycleCount = Mathf.Max(1, s.flaps);
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = s.mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        /// <summary>Groups the emitters into 32 m chunks that stop drawing beyond 'distance' (as Culling.cs does for small props).</summary>
        private static void DressFxCulling(Transform holder, float distance)
        {
            var buckets = new Dictionary<Vector2Int, List<Renderer>>();
            foreach (var r in holder.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Vector3 p = r.transform.position;
                var key = new Vector2Int(Mathf.FloorToInt(p.x / CullChunk), Mathf.FloorToInt(p.z / CullChunk));
                List<Renderer> list;
                if (!buckets.TryGetValue(key, out list)) buckets[key] = list = new List<Renderer>();
                list.Add(r);
            }
            float h = Mathf.Clamp(CullChunk / (2f * Mathf.Max(10f, distance)), 0.01f, 0.9f);
            foreach (var kv in buckets)
            {
                float y = 0f;
                foreach (var r in kv.Value) y += r.transform.position.y;
                y /= kv.Value.Count;
                var go = new GameObject("LOD_Fx_" + kv.Key.x + "_" + kv.Key.y);
                go.transform.SetParent(holder, false);
                go.transform.position = new Vector3((kv.Key.x + 0.5f) * CullChunk, y, (kv.Key.y + 0.5f) * CullChunk);
                var lod = go.AddComponent<LODGroup>();
                lod.fadeMode = LODFadeMode.None;
                lod.SetLODs(new[] { new LOD(h, kv.Value.ToArray()) });
                lod.localReferencePoint = Vector3.zero;
                lod.size = CullChunk;
            }
        }

        // ------------------------------------------------------------------ the effects

        private static void SfxEmit(DressCtx d, StreetFxSpec s, Vector3 at)
        {
            d.emitters++;
            StreetFx(d.fx, s, at);
        }

        /// <summary>Smoke rolling up off a chimney and leaning with the breeze.</summary>
        private static void SfxSmoke(DressCtx d, Vector3 at, Color c, float scale, float alpha)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxSmoke", mat = d.m.fxPuff, box = new Vector3(0.3f, 0.1f, 0.3f) * scale, rate = 1.5f, life0 = 4f, life1 = 7f,
                size = new Vector2(0.55f, 0.9f) * scale, vel = new Vector3(d.wind.x * 0.35f, 0.75f, d.wind.z * 0.35f), jitter = new Vector3(0.12f, 0.2f, 0.12f),
                grow = 2.8f, spin = 25f, alpha = alpha, c0 = c, c1 = Color.Lerp(c, Color.white, 0.2f),
            }, at);
        }

        /// <summary>A quick jet of steam along 'dir'.</summary>
        private static void SfxSteam(DressCtx d, Vector3 at, Vector3 dir)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxSteam", mat = d.m.fxPuff, box = Vector3.one * 0.12f, rate = 3f, life0 = 1.4f, life1 = 2.4f, size = new Vector2(0.35f, 0.55f),
                vel = dir * 1.3f + new Vector3(0f, 0.5f, 0f), jitter = new Vector3(0.15f, 0.15f, 0.15f), grow = 3.2f, spin = 40f, alpha = 0.5f,
                c0 = new Color(0.9f, 0.92f, 0.95f), c1 = Color.white,
            }, at);
        }

        /// <summary>Sparks rising off a fire.</summary>
        private static void SfxEmbers(DressCtx d, Vector3 at, Color c, float spread)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxEmbers", mat = d.m.fxGlow, box = new Vector3(spread, 0.1f, spread), rate = 5f, life0 = 1.2f, life1 = 2.6f, size = new Vector2(0.05f, 0.11f),
                vel = new Vector3(d.wind.x * 0.2f, 0.9f, d.wind.z * 0.2f), jitter = new Vector3(0.3f, 0.35f, 0.3f), noise = 0.3f,
                c0 = c, c1 = Color.Lerp(c, new Color(1f, 0.95f, 0.7f), 0.5f),
            }, at);
        }

        /// <summary>Slow glowing motes wandering in a box: fireflies, will-o'-the-wisps (bigger, fewer).</summary>
        private static void SfxFireflies(DressCtx d, Vector3 at, Vector3 box, Color c, float rate, float size)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxFireflies", mat = d.m.fxGlow, box = box, rate = rate, life0 = 3f, life1 = 6f, size = new Vector2(size, size * 2f),
                vel = new Vector3(0f, 0.08f, 0f), jitter = new Vector3(0.35f, 0.2f, 0.35f), noise = 0.5f, c0 = c, c1 = Color.Lerp(c, Color.white, 0.3f),
            }, at);
        }

        /// <summary>Leaves, petals or ash turning as they fall and drift with the breeze.</summary>
        private static void SfxFall(DressCtx d, Vector3 at, Vector3 box, Material mat, Color c0, Color c1, float rate, float size, float fall)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxFall", mat = mat, box = box, rate = rate, life0 = 5f, life1 = 8f, size = new Vector2(size, size * 1.7f),
                vel = new Vector3(d.wind.x * 0.4f, -fall, d.wind.z * 0.4f), jitter = new Vector3(0.35f, 0.15f, 0.35f), spin = 140f, noise = 0.25f, c0 = c0, c1 = c1,
            }, at);
        }

        /// <summary>Butterflies (or moths, small and pale, kept to a lamp's glow): two-frame sprites flapping as they wander.</summary>
        private static void SfxWings(DressCtx d, Vector3 at, Vector3 box, Color c0, Color c1, float size, float rate, float life)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxWings", mat = d.m.fxFly, box = box, rate = rate, life0 = life * 0.7f, life1 = life, size = new Vector2(size, size * 1.5f),
                jitter = new Vector3(0.5f, 0.2f, 0.5f), noise = 0.8f, c0 = c0, c1 = c1, frames = 2, flaps = Mathf.RoundToInt(life * 5f),
            }, at);
        }

        /// <summary>Big thin puffs creeping low: sea mist, ground fog.</summary>
        private static void SfxMist(DressCtx d, Vector3 at, Vector3 box, Color c, float alpha)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxMist", mat = d.m.fxPuff, box = box, rate = 0.45f, life0 = 9f, life1 = 14f, size = new Vector2(3f, 5f),
                vel = new Vector3(d.wind.x * 0.18f, 0.02f, d.wind.z * 0.18f), jitter = new Vector3(0.08f, 0.02f, 0.08f), grow = 1.5f, spin = 6f, alpha = alpha,
                c0 = c, c1 = Color.Lerp(c, Color.white, 0.15f),
            }, at);
        }

        /// <summary>Fine motes hanging in the air: pollen, dust in lamplight, grit sifting down ('fall' above zero).</summary>
        private static void SfxDust(DressCtx d, Vector3 at, Vector3 box, Color c, float fall)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxDust", mat = d.m.fxDot, box = box, rate = 5f, life0 = 5f, life1 = 9f, size = new Vector2(0.04f, 0.09f),
                vel = new Vector3(d.wind.x * 0.3f, 0.03f - fall, d.wind.z * 0.3f), jitter = new Vector3(0.25f, 0.08f, 0.25f), noise = 0.3f, alpha = 0.7f,
                c0 = c, c1 = Color.Lerp(c, Color.white, 0.3f),
            }, at);
        }

        /// <summary>Sand blowing low along the street.</summary>
        private static void SfxGust(DressCtx d, Vector3 at, Vector3 box, Color c)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxGust", mat = d.m.fxPuff, box = box, rate = 0.9f, life0 = 3.5f, life1 = 5.5f, size = new Vector2(1.2f, 2.2f),
                vel = new Vector3(d.wind.x * 2.4f, 0.15f, d.wind.z * 2.4f), jitter = new Vector3(0.4f, 0.1f, 0.4f), grow = 1.8f, spin = 30f, alpha = 0.28f,
                c0 = c, c1 = Color.Lerp(c, Color.white, 0.2f),
            }, at);
        }

        /// <summary>Drops falling from overhead.</summary>
        private static void SfxDrips(DressCtx d, Vector3 at, Vector3 box, Color c, float rate)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxDrips", mat = d.m.fxDrip, box = box, rate = rate, life0 = 0.9f, life1 = 1.2f, size = new Vector2(0.16f, 0.24f), gravity = 1f, alpha = 0.9f, c0 = c, c1 = c,
            }, at);
        }

        private static void SfxBubbles(DressCtx d, Vector3 at, Vector3 box, Color c)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxBubbles", mat = d.m.fxBubble, box = box, rate = 1.4f, life0 = 1.6f, life1 = 3f, size = new Vector2(0.1f, 0.24f),
                vel = new Vector3(0f, 0.22f, 0f), jitter = new Vector3(0.05f, 0.08f, 0.05f), alpha = 0.9f, c0 = c, c1 = Color.white,
            }, at);
        }

        /// <summary>Tiny glowing specks lifting off something that glows.</summary>
        private static void SfxSpores(DressCtx d, Vector3 at, Vector3 box, Color c)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxSpores", mat = d.m.fxGlow, box = box, rate = 2.2f, life0 = 3f, life1 = 6f, size = new Vector2(0.04f, 0.09f),
                vel = new Vector3(0f, 0.18f, 0f), jitter = new Vector3(0.12f, 0.1f, 0.12f), noise = 0.3f, c0 = c, c1 = Color.Lerp(c, Color.white, 0.4f),
            }, at);
        }

        /// <summary>Brief glints: light on water, on crystal.</summary>
        private static void SfxGlints(DressCtx d, Vector3 at, Vector3 box, Color c)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxGlints", mat = d.m.fxSpark, box = box, rate = 2f, life0 = 0.5f, life1 = 1.1f, size = new Vector2(0.1f, 0.2f),
                jitter = new Vector3(0.02f, 0.02f, 0.02f), c0 = c, c1 = Color.white,
            }, at);
        }

        /// <summary>Sparks spitting off a lamp or a wire and falling.</summary>
        private static void SfxSparks(DressCtx d, Vector3 at, Color c)
        {
            SfxEmit(d, new StreetFxSpec
            {
                name = "FxSparks", mat = d.m.fxSpark, box = Vector3.one * 0.15f, rate = 2.5f, life0 = 0.35f, life1 = 0.7f, size = new Vector2(0.06f, 0.11f),
                vel = new Vector3(0f, 0.4f, 0f), jitter = new Vector3(1.1f, 0.9f, 1.1f), gravity = 0.8f, c0 = c, c1 = Color.white,
            }, at);
        }

        // ------------------------------------------------------------------ where they go

        /// <summary>Up to n of the cells, shuffled, no two within 'gap' cells of each other.</summary>
        private static List<Vector2Int> DressSpread(DressCtx d, List<Vector2Int> cells, int n, int gap)
        {
            var picked = new List<Vector2Int>();
            if (n <= 0 || cells.Count == 0) return picked;
            var pool = new List<Vector2Int>(cells);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = d.rng.Next(i + 1);
                Vector2Int t = pool[i];
                pool[i] = pool[j];
                pool[j] = t;
            }
            foreach (var c in pool)
            {
                if (picked.Count >= n) break;
                bool far = true;
                foreach (var q in picked)
                    if (Mathf.Abs(q.x - c.x) < gap && Mathf.Abs(q.y - c.y) < gap) { far = false; break; }
                if (far) picked.Add(c);
            }
            return picked;
        }

        /// <summary>Up to n of the points, shuffled.</summary>
        private static List<Vector3> DressSome(DressCtx d, List<Vector3> points, int n)
        {
            var pool = new List<Vector3>(points);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = d.rng.Next(i + 1);
                Vector3 t = pool[i];
                pool[i] = pool[j];
                pool[j] = t;
            }
            if (pool.Count > Mathf.Max(0, n)) pool.RemoveRange(Mathf.Max(0, n), pool.Count - Mathf.Max(0, n));
            return pool;
        }

        private static void DressFx(DressCtx d, List<Vector2Int> lawns, List<Vector2Int> trees, List<Vector2Int> waters, List<Vector2Int> roads)
        {
            ZoneGrid g = d.g;
            float cs = d.cs;
            ZoneTextureStyle st = d.st;
            float amount = Mathf.Clamp(d.density, 0.3f, 1.5f);
            Color fog = d.z.fogColor, accent = d.z.accentColor;
            Color leafA = Color.Lerp(d.z.treeColor, new Color(0.45f, 0.7f, 0.25f), 0.5f), leafB = new Color(0.9f, 0.62f, 0.2f);
            Vector3 patch = new Vector3(cs * 2.6f, 2.2f, cs * 2.6f);

            // Open road and lawn the street-wide effects can hang over; the big soft ones keep clear of the duel rings.
            var open = new List<Vector2Int>();
            var calm = new List<Vector2Int>();
            foreach (var c in roads)
            {
                if (d.none.Contains(c)) continue;
                open.Add(c);
                if (!d.NearRing(g.Local(c), -1f)) calm.Add(c);
            }
            var green = new List<Vector2Int>();
            foreach (var c in lawns)
            {
                if (d.none.Contains(c)) continue;
                green.Add(c);
                if (!d.NearRing(g.Local(c), -1f)) calm.Add(c);
            }
            if (calm.Count == 0) calm = open;
            if (green.Count == 0) green = open;

            // Shared pieces, each asked for by the styles that want it.
            void Chimneys(float n, Color c, float alpha, bool embers)
            {
                int k = 0;
                foreach (var top in DressSome(d, _chimneyTops, Mathf.RoundToInt(n * amount)))
                {
                    SfxSmoke(d, top + new Vector3(0f, 0.2f, 0f), c, 1f, alpha);
                    if (embers && k++ % 3 == 0) SfxEmbers(d, top + new Vector3(0f, 0.3f, 0f), new Color(1f, 0.55f, 0.2f), 0.3f);
                }
            }
            void Moths(float n)
            {
                foreach (var lampAt in DressSome(d, _lampTops, Mathf.RoundToInt(n * amount)))
                    SfxWings(d, lampAt, new Vector3(1.3f, 1.1f, 1.3f), new Color(0.95f, 0.92f, 0.8f), new Color(0.8f, 0.76f, 0.66f), 0.1f, 0.7f, 6f);
            }
            void Leaves(float n, Material mat, Color c0, Color c1, float size)
            {
                foreach (var c in DressSpread(d, trees, Mathf.RoundToInt(n * amount), 2))
                {
                    float s = 0.8f + g.Hash(c.x, c.y, 4) * 0.5f;
                    SfxFall(d, g.Local(c) + new Vector3(0f, 4f * s, 0f), new Vector3(4f, 1.2f, 4f), mat, c0, c1, 1.6f, size, 0.55f);
                }
            }
            void Fireflies(float n, Color c)
            {
                foreach (var cell in DressSpread(d, green, Mathf.RoundToInt(n * amount), 3))
                    SfxFireflies(d, g.Local(cell) + new Vector3(0f, 1.5f, 0f), patch, c, 4f, 0.08f);
            }
            void Butterflies(float n)
            {
                foreach (var cell in DressSpread(d, green, Mathf.RoundToInt(n * amount), 3))
                    SfxWings(d, g.Local(cell) + new Vector3(0f, 1.3f, 0f), new Vector3(cs * 2f, 1.6f, cs * 2f), d.Chance(0.5f) ? new Color(1f, 0.75f, 0.25f) : new Color(0.55f, 0.75f, 1f),
                        d.Chance(0.5f) ? new Color(1f, 0.55f, 0.8f) : Color.white, 0.24f, 0.45f, 12f);
            }
            void WaterMist(float n, Color c, float alpha)
            {
                foreach (var cell in DressSpread(d, waters, Mathf.RoundToInt(n * amount), 2))
                {
                    float y;
                    if (!d.water.TryGetValue(cell, out y)) continue;
                    Vector3 at = g.Local(cell);
                    SfxMist(d, new Vector3(at.x, y + 0.7f, at.z), new Vector3(cs * 2f, 0.5f, cs * 2f), c, alpha);
                }
            }
            void WaterGlints(float n)
            {
                foreach (var cell in DressSpread(d, waters, Mathf.RoundToInt(n * amount), 2))
                {
                    float y;
                    if (!d.water.TryGetValue(cell, out y)) continue;
                    Vector3 at = g.Local(cell);
                    SfxGlints(d, new Vector3(at.x, y + 0.06f, at.z), new Vector3(cs * 1.6f, 0.02f, cs * 1.6f), new Color(0.85f, 0.95f, 1f));
                }
            }
            void Drips(float n, Color c)
            {
                foreach (var cell in DressSpread(d, open, Mathf.RoundToInt(n * amount), 3))
                    SfxDrips(d, g.Local(cell) + new Vector3(d.R(-0.3f, 0.3f) * cs, 6.2f, d.R(-0.3f, 0.3f) * cs), new Vector3(1.5f, 0.1f, 1.5f), c, 0.9f);
            }

            switch (st)
            {
                case ZoneTextureStyle.Garden:
                    Butterflies(9f);
                    Leaves(8f, d.m.fxPetal, new Color(1f, 0.7f, 0.82f), Color.white, 0.09f);
                    foreach (var cell in DressSpread(d, green, Mathf.RoundToInt(6f * amount), 3))
                        SfxDust(d, g.Local(cell) + new Vector3(0f, 1.6f, 0f), patch, new Color(1f, 0.92f, 0.5f), 0f);
                    Fireflies(3f, new Color(0.9f, 1f, 0.5f));
                    Chimneys(4f, Color.Lerp(new Color(0.7f, 0.7f, 0.72f), fog, 0.3f), 0.45f, false);
                    Moths(3f);
                    WaterGlints(3f);
                    break;

                case ZoneTextureStyle.Docks:
                    WaterMist(8f, Color.Lerp(fog, Color.white, 0.5f), 0.22f);
                    WaterGlints(5f);
                    Chimneys(6f, Color.Lerp(new Color(0.6f, 0.6f, 0.64f), fog, 0.3f), 0.5f, false);
                    Moths(4f);
                    Leaves(2f, d.m.fxLeaf, leafA, leafB, 0.18f);
                    break;

                case ZoneTextureStyle.Forge:
                    Chimneys(14f, new Color(0.26f, 0.24f, 0.25f), 0.65f, true);
                    foreach (var cell in DressSpread(d, open, Mathf.RoundToInt(7f * amount), 3))
                        SfxFall(d, g.Local(cell) + new Vector3(0f, 5f, 0f), new Vector3(cs * 2.4f, 1f, cs * 2.4f), d.m.fxPetal, new Color(0.16f, 0.15f, 0.15f), new Color(0.4f, 0.38f, 0.36f), 3f, 0.05f, 0.35f);
                    foreach (var cell in DressSpread(d, open, Mathf.RoundToInt(4f * amount), 4))
                        SfxEmbers(d, g.Local(cell) + new Vector3(0f, 1.2f, 0f), new Color(1f, 0.5f, 0.18f), cs * 1.5f);
                    break;

                case ZoneTextureStyle.Forest:
                    foreach (var cell in DressSpread(d, green, Mathf.RoundToInt(9f * amount), 3))
                        SfxFall(d, g.Local(cell) + new Vector3(0f, 5f, 0f), new Vector3(cs * 2f, 1.5f, cs * 2f), d.m.fxLeaf, leafA, leafB, 1.6f, 0.18f, 0.55f);
                    foreach (var cell in DressSpread(d, green, Mathf.RoundToInt(6f * amount), 3))
                        SfxSpores(d, g.Local(cell) + new Vector3(0f, 1.2f, 0f), patch, new Color(0.75f, 1f, 0.6f));
                    foreach (var cell in DressSpread(d, calm, Mathf.RoundToInt(4f * amount), 4))
                        SfxMist(d, g.Local(cell) + new Vector3(0f, 0.6f, 0f), new Vector3(cs * 2f, 0.4f, cs * 2f), Color.Lerp(fog, Color.white, 0.4f), 0.16f);
                    Chimneys(3f, new Color(0.7f, 0.7f, 0.72f), 0.45f, false);
                    WaterGlints(2f);
                    break;

                case ZoneTextureStyle.Keep:
                    foreach (var cell in DressSpread(d, calm, Mathf.RoundToInt(8f * amount), 3))
                        SfxFireflies(d, g.Local(cell) + new Vector3(0f, 1.6f, 0f), patch, Color.Lerp(accent, Color.white, 0.2f), 0.5f, 0.3f);
                    foreach (var cell in DressSpread(d, calm, Mathf.RoundToInt(8f * amount), 3))
                        SfxMist(d, g.Local(cell) + new Vector3(0f, 0.5f, 0f), new Vector3(cs * 2f, 0.3f, cs * 2f), Color.Lerp(fog, accent, 0.25f), 0.2f);
                    Leaves(4f, d.m.fxLeaf, new Color(0.35f, 0.28f, 0.2f), new Color(0.5f, 0.4f, 0.3f), 0.16f);
                    foreach (var lampAt in DressSome(d, _lampTops, Mathf.RoundToInt(5f * amount)))
                        SfxSpores(d, lampAt, new Vector3(0.6f, 0.6f, 0.6f), accent);
                    WaterMist(3f, Color.Lerp(fog, accent, 0.2f), 0.2f);
                    break;

                case ZoneTextureStyle.Desert:
                case ZoneTextureStyle.Boomtown:
                    foreach (var cell in DressSpread(d, calm, Mathf.RoundToInt(9f * amount), 3))
                        SfxGust(d, g.Local(cell) + new Vector3(0f, 0.6f, 0f), new Vector3(cs * 2f, 0.6f, cs * 2f), Color.Lerp(d.z.grassColor, new Color(0.9f, 0.8f, 0.62f), 0.5f));
                    foreach (var cell in DressSpread(d, open, Mathf.RoundToInt(6f * amount), 3))
                        SfxDust(d, g.Local(cell) + new Vector3(0f, 1.8f, 0f), patch, new Color(0.95f, 0.85f, 0.62f), 0f);
                    Moths(4f);
                    WaterGlints(2f);
                    break;

                case ZoneTextureStyle.Mine:
                    foreach (var cell in DressSpread(d, open, Mathf.RoundToInt(7f * amount), 3))
                        SfxDust(d, g.Local(cell) + new Vector3(0f, 3f, 0f), new Vector3(cs * 2f, 3f, cs * 2f), new Color(0.8f, 0.74f, 0.66f), 0.12f);
                    Drips(7f, new Color(0.7f, 0.85f, 1f));
                    foreach (var lampAt in DressSome(d, _lampTops, Mathf.RoundToInt(5f * amount)))
                        SfxDust(d, lampAt, new Vector3(1.6f, 1.4f, 1.6f), Color.Lerp(accent, Color.white, 0.4f), 0f);
                    WaterGlints(3f);
                    break;

                case ZoneTextureStyle.Undercity:
                    foreach (var top in DressSome(d, _stackTops, Mathf.RoundToInt(10f * amount)))
                    {
                        SfxSmoke(d, top, new Color(0.22f, 0.2f, 0.22f), 1.3f, 0.6f);
                        if (d.Chance(0.5f)) SfxEmbers(d, top, new Color(1f, 0.5f, 0.18f), 0.4f);
                    }
                    Drips(6f, new Color(0.6f, 0.95f, 0.8f));
                    foreach (var lampAt in DressSome(d, _lampTops, Mathf.RoundToInt(6f * amount)))
                        SfxSparks(d, lampAt, UnderNeon[d.Pick(UnderNeon.Length)]);
                    foreach (var cell in DressSpread(d, green, Mathf.RoundToInt(5f * amount), 3))
                        SfxSpores(d, g.Local(cell) + new Vector3(0f, 1f, 0f), patch, new Color(0.4f, 1f, 0.85f));
                    WaterMist(3f, new Color(0.8f, 0.9f, 0.85f), 0.2f);
                    foreach (var cell in DressSpread(d, waters, Mathf.RoundToInt(4f * amount), 2))
                    {
                        float y;
                        if (!d.water.TryGetValue(cell, out y)) continue;
                        Vector3 at = g.Local(cell);
                        SfxBubbles(d, new Vector3(at.x, y + 0.05f, at.z), new Vector3(cs * 1.4f, 0.05f, cs * 1.4f), new Color(0.6f, 1f, 0.7f));
                    }
                    break;

                default:    // Town
                    Chimneys(10f, Color.Lerp(new Color(0.62f, 0.62f, 0.66f), fog, 0.3f), 0.5f, false);
                    Moths(8f);
                    Leaves(8f, d.m.fxLeaf, leafA, leafB, 0.18f);
                    Fireflies(5f, Color.Lerp(accent, new Color(0.9f, 1f, 0.5f), 0.5f));
                    Butterflies(2f);
                    WaterMist(4f, Color.Lerp(fog, Color.white, 0.5f), 0.18f);
                    WaterGlints(2f);
                    break;
            }
        }
    }
}
