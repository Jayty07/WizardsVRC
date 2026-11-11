using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The pieces the street dressing pass (WizardsVRCSceneBuilder.Dressing.cs) lays out, and each street style's kit:
    ///   - a handful of rounded low-poly meshes of its own (dome, barrel, urn, pot, two rings, a disc, grass tufts),
    ///     flat shaded like the rest, saved under Meshes/;
    ///   - its materials: a few shared ones (wood, iron, clay, rope, sacking...) and the street's own greens and glow;
    ///   - the recipes (Dr...): each builds one prop or plant out of those meshes at a point, facing out from a wall;
    ///   - DressKitFor: which recipes a style uses against its walls, in its corners, on its walls, on its lawns and on
    ///     its low walls, how thickly, and what it strings across its streets.
    /// Original designs throughout: nothing here copies another game's art or names.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string StreetFxDir = TexturesDir + "/StreetFx";

        // ------------------------------------------------------------------ meshes

        private static readonly Dictionary<string, Mesh> _dressMeshes = new Dictionary<string, Mesh>();

        /// <summary>Builds (once per editor session) and saves a dressing mesh, with the same UVs the other low-poly meshes get.</summary>
        private static Mesh DressMesh(string name, System.Func<MeshBuilder> build)
        {
            Mesh mesh;
            if (_dressMeshes.TryGetValue(name, out mesh) && mesh != null) return mesh;
            MeshBuilder mb = build();
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + name + ".asset";
            var uv = new List<Vector2>();
            for (int i = 0; i + 2 < mb.v.Count; i += 3)
            {
                Vector3 n = Vector3.Cross(mb.v[i + 1] - mb.v[i], mb.v[i + 2] - mb.v[i]).normalized;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 v = mb.v[i + k];
                    uv.Add(Mathf.Abs(n.y) > 0.7f ? new Vector2(v.x, v.z) : new Vector2(v.x + v.z, v.y));
                }
            }
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh { name = name };
            else mesh.Clear();
            mesh.SetVertices(mb.v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(mb.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            _dressMeshes[name] = mesh;
            return mesh;
        }

        /// <summary>
        /// A solid turned round the Y axis: 'profile' runs up its outside as (radius, height) points, starting and ending
        /// on the axis for closed ends. Faces look outwards.
        /// </summary>
        private static MeshBuilder DressLathe(Vector2[] profile, int sides)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i + 1 < profile.Length; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                for (int s = 0; s < sides; s++)
                {
                    float mid = (s + 0.5f) / sides * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid));
                    // Outwards: the profile's direction turned a quarter turn away from the axis.
                    Vector3 want = radial * (b.y - a.y) + Vector3.up * (a.x - b.x);
                    Vector3 a0 = Ring(s, sides, a.x, a.y), a1 = Ring(s + 1, sides, a.x, a.y);
                    Vector3 b0 = Ring(s, sides, b.x, b.y), b1 = Ring(s + 1, sides, b.x, b.y);
                    if (a.x > 0.0001f) mb.Tri(a0, a1, b1, want);
                    if (b.x > 0.0001f) mb.Tri(a0, b1, b0, want);
                }
            }
            return mb;
        }

        private static Vector3 DressRingPoint(int i, int segs, int j, int sides, float tube)
        {
            float a = i / (float)segs * Mathf.PI * 2f, b = j / (float)sides * Mathf.PI * 2f;
            float r = 0.5f + Mathf.Cos(b) * tube;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(b) * tube, Mathf.Sin(a) * r);
        }

        /// <summary>A ring lying flat (radius 0.5 to the middle of its tube).</summary>
        private static MeshBuilder DressRing(float tube, int segs, int sides)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < segs; i++)
            {
                float am = (i + 0.5f) / segs * Mathf.PI * 2f;
                Vector3 core = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * 0.5f;
                for (int j = 0; j < sides; j++)
                {
                    Vector3 a = DressRingPoint(i, segs, j, sides, tube), b = DressRingPoint(i + 1, segs, j, sides, tube);
                    Vector3 c = DressRingPoint(i + 1, segs, j + 1, sides, tube), e = DressRingPoint(i, segs, j + 1, sides, tube);
                    mb.Quad(a, b, c, e, (a + b + c + e) / 4f - core);
                }
            }
            return mb;
        }

        /// <summary>Blades of grass: thin three-sided spikes fanning out from the middle, up to 1 tall.</summary>
        private static MeshBuilder DressBlades(int blades, float lean, float width, int seed)
        {
            var mb = new MeshBuilder();
            var rng = new System.Random(seed);
            for (int k = 0; k < blades; k++)
            {
                float a = (k + (float)rng.NextDouble() * 0.7f) / blades * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 across = new Vector3(-dir.z, 0f, dir.x);
                float h = blades == 1 ? 1f : 0.55f + (float)rng.NextDouble() * 0.45f;
                Vector3 foot = blades == 1 ? Vector3.zero : dir * (0.05f + 0.12f * (float)rng.NextDouble());
                Vector3 tip = foot + dir * (lean * (0.5f + (float)rng.NextDouble())) + Vector3.up * h;
                Vector3 b0 = foot + across * width, b1 = foot - across * width, b2 = foot + dir * (width * 1.5f);
                Vector3 mid = (b0 + b1 + b2 + tip) / 4f;
                mb.Tri(b0, b1, tip, (b0 + b1 + tip) / 3f - mid);
                mb.Tri(b1, b2, tip, (b1 + b2 + tip) / 3f - mid);
                mb.Tri(b2, b0, tip, (b2 + b0 + tip) / 3f - mid);
            }
            return mb;
        }

        // radius 0.5, y 0..0.5: a mound with a flat base
        private static Mesh DressDome()
        {
            return DressMesh("PSX_DressDome", () => DressLathe(new[] { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.44f, 0.24f), new Vector2(0.25f, 0.43f), new Vector2(0f, 0.5f) }, 7));
        }

        // y 0..1, radius 0.4 at the ends and 0.5 at the belly
        private static Mesh DressBarrel()
        {
            return DressMesh("PSX_DressBarrel", () => DressLathe(new[] { new Vector2(0f, 0f), new Vector2(0.4f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.4f, 1f), new Vector2(0f, 1f) }, 8));
        }

        // y 0..1: a narrow foot, a belly of radius 0.5, a neck and a flared lip of radius 0.31
        private static Mesh DressUrn()
        {
            return DressMesh("PSX_DressUrn", () => DressLathe(new[] { new Vector2(0f, 0f), new Vector2(0.24f, 0f), new Vector2(0.5f, 0.42f), new Vector2(0.4f, 0.74f),
                new Vector2(0.24f, 0.86f), new Vector2(0.31f, 1f), new Vector2(0f, 1f) }, 7));
        }

        // y 0..1, radius 0.36 at the foot and 0.5 at the rim: a pot, bucket, tub or basket
        private static Mesh DressPot()
        {
            return DressMesh("PSX_DressPot", () => DressLathe(new[] { new Vector2(0f, 0f), new Vector2(0.36f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 1f) }, 8));
        }

        private static Mesh DressTorusThin() { return DressMesh("PSX_DressRingThin", () => DressRing(0.06f, 10, 4)); }
        private static Mesh DressTorusFat() { return DressMesh("PSX_DressRingFat", () => DressRing(0.14f, 8, 5)); }

        // radius 0.5 at y 0, facing up (one-sided): lily pads, puddles, lids
        private static Mesh DressDisc()
        {
            return DressMesh("PSX_DressDisc", () =>
            {
                var mb = new MeshBuilder();
                for (int s = 0; s < 8; s++) mb.Tri(Vector3.zero, Ring(s, 8, 0.5f, 0f), Ring(s + 1, 8, 0.5f, 0f), Vector3.up);
                return mb;
            });
        }

        private static Mesh DressTuft() { return DressMesh("PSX_DressTuft", () => DressBlades(6, 0.22f, 0.035f, 11)); }
        private static Mesh DressTuftWide() { return DressMesh("PSX_DressTuftWide", () => DressBlades(8, 0.5f, 0.05f, 23)); }
        private static Mesh DressTuftTall() { return DressMesh("PSX_DressTuftTall", () => DressBlades(4, 0.08f, 0.028f, 37)); }
        private static Mesh DressBlade() { return DressMesh("PSX_DressBlade", () => DressBlades(1, 0f, 0.03f, 5)); }

        // ------------------------------------------------------------------ materials

        private class DressMats
        {
            public Material[] leaf, petal, pots, paint, lanterns, glazes;
            public Material tuft, dry, bark, wood, iron, brass, clay, stone, rock, moss, sack, rope, hay, bone, coal, ember, pumpkin, pool, sludge, rust,
                cactus, sage, glow, crystal, capRed, capTan, glowCap, stalk, fish, roof, wax;
            public Material fxGlow, fxSpark, fxLeaf, fxPetal, fxPuff, fxFly, fxBubble, fxDrip, fxDot;
            /// <summary>The street has flowers; its rocks grow moss.</summary>
            public bool blooms, mossy;
        }

        private static DressMats MakeDressMats(ZoneDefinition z, string k, Material glow, Material roof)
        {
            ZoneTextureStyle st = z.textureStyle;
            bool dust = st == ZoneTextureStyle.Desert || st == ZoneTextureStyle.Boomtown;
            bool cave = st == ZoneTextureStyle.Mine || st == ZoneTextureStyle.Undercity;
            Texture2D leafTex = TileTexture(StreetTexDir + "/Leaves.png");
            Texture2D metal = TileTexture(StreetTexDir + "/Metal.png");
            Color tree = z.treeColor, grass = z.grassColor, accent = z.accentColor;
            Color[] leaves;
            Color tuft;
            if (st == ZoneTextureStyle.Keep)
            {
                leaves = new[] { Color.Lerp(tree, new Color(0.2f, 0.24f, 0.18f), 0.5f), Color.Lerp(tree, new Color(0.3f, 0.24f, 0.2f), 0.5f), Color.Lerp(tree, new Color(0.24f, 0.2f, 0.3f), 0.5f) };
                tuft = Color.Lerp(grass, new Color(0.4f, 0.4f, 0.36f), 0.5f);
            }
            else if (st == ZoneTextureStyle.Forge)
            {
                leaves = new[] { Color.Lerp(tree, new Color(0.25f, 0.3f, 0.16f), 0.5f), Color.Lerp(tree, new Color(0.4f, 0.32f, 0.16f), 0.5f), Color.Lerp(tree, new Color(0.3f, 0.36f, 0.2f), 0.5f) };
                tuft = Color.Lerp(grass, new Color(0.5f, 0.45f, 0.25f), 0.5f);
            }
            else if (st == ZoneTextureStyle.Undercity)
            {
                leaves = new[] { new Color(0.2f, 0.4f, 0.36f), new Color(0.3f, 0.42f, 0.28f), new Color(0.26f, 0.34f, 0.44f) };
                tuft = new Color(0.35f, 0.52f, 0.46f);
            }
            else if (dust)
            {
                leaves = new[] { new Color(0.42f, 0.5f, 0.3f), new Color(0.5f, 0.55f, 0.38f), new Color(0.36f, 0.46f, 0.34f) };
                tuft = new Color(0.74f, 0.62f, 0.36f);
            }
            else if (st == ZoneTextureStyle.Mine)
            {
                leaves = new[] { new Color(0.35f, 0.42f, 0.4f), new Color(0.4f, 0.38f, 0.5f), new Color(0.3f, 0.45f, 0.38f) };
                tuft = new Color(0.45f, 0.5f, 0.48f);
            }
            else
            {
                leaves = new[] { Color.Lerp(tree, new Color(0.1f, 0.3f, 0.12f), 0.3f), Color.Lerp(tree, new Color(0.3f, 0.6f, 0.18f), 0.45f), Color.Lerp(tree, new Color(0.18f, 0.5f, 0.3f), 0.4f) };
                tuft = Color.Lerp(grass, new Color(0.35f, 0.62f, 0.2f), 0.45f);
            }

            var m = new DressMats();
            m.blooms = !cave && st != ZoneTextureStyle.Keep;
            m.mossy = !cave && !dust && st != ZoneTextureStyle.Forge;
            m.leaf = new Material[3];
            for (int i = 0; i < 3; i++) m.leaf[i] = PsxTexMat(k + "_DressLeaf" + i, leaves[i], leafTex, 1.6f);
            m.tuft = PMat(k + "_DressTuft", tuft, 1f);
            m.crystal = PMat(k + "_DressCrystal", accent * 0.8f, 1f, accent * 0.9f);
            m.glow = glow;
            m.roof = roof;
            m.petal = new[]
            {
                PMat("DressPetalWhite", new Color(0.95f, 0.95f, 0.88f), 1f), PMat("DressPetalBlue", new Color(0.45f, 0.6f, 1f), 1f),
                PMat("DressPetalYellow", new Color(1f, 0.85f, 0.3f), 1f), PMat("DressPetalPink", new Color(1f, 0.55f, 0.75f), 1f),
                PMat("DressPetalRed", new Color(0.92f, 0.25f, 0.25f), 1f), PMat("DressPetalViolet", new Color(0.66f, 0.45f, 0.95f), 1f),
            };
            m.paint = new[]
            {
                PMat("DressPaintRed", new Color(0.75f, 0.18f, 0.15f), 1f), PMat("DressPaintWhite", new Color(0.9f, 0.9f, 0.86f), 1f),
                PMat("DressPaintTeal", new Color(0.15f, 0.5f, 0.5f), 1f), PMat("DressPaintYellow", new Color(0.9f, 0.72f, 0.2f), 1f),
                PMat("DressPaintBlue", new Color(0.2f, 0.32f, 0.62f), 1f),
            };
            m.glazes = new[]
            {
                PMat("DressGlazeBlue", new Color(0.25f, 0.42f, 0.62f), 1f), PMat("DressGlazeGreen", new Color(0.3f, 0.5f, 0.36f), 1f),
                PMat("DressGlazeCream", new Color(0.85f, 0.8f, 0.66f), 1f), PMat("DressGlazeOxblood", new Color(0.5f, 0.16f, 0.14f), 1f),
            };
            m.dry = PMat("DressDry", new Color(0.74f, 0.62f, 0.36f), 1f);
            m.bark = PsxTexMat("DressBark", new Color(0.45f, 0.3f, 0.18f), TileTexture(StreetTexDir + "/Bark.png"), 1.2f);
            m.wood = PsxTexMat("DressWood", new Color(0.52f, 0.36f, 0.22f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 1.3f);
            m.iron = PsxTexMat("DressIron", new Color(0.2f, 0.2f, 0.23f), metal, 1.2f);
            m.brass = UnderBrass();
            m.clay = PsxTexMat("DressClay", new Color(0.72f, 0.4f, 0.26f), TileTexture(StreetTexDir + "/Adobe.png"), 1.5f);
            m.stone = PsxTexMat("DressStone", new Color(0.56f, 0.56f, 0.58f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.6f);
            m.rock = cave ? PsxTexMat("DressRockCave", new Color(0.42f, 0.4f, 0.44f), TileTexture(TerrainTexDir + "/MineRock.png"), 2f)
                : dust ? PsxTexMat("DressRockRed", new Color(0.72f, 0.42f, 0.3f), TileTexture(TerrainTexDir + "/RedRock.png"), 2f)
                : st == ZoneTextureStyle.Keep || st == ZoneTextureStyle.Forge ? PsxTexMat("DressRockDark", new Color(0.3f, 0.3f, 0.34f), TileTexture(TerrainTexDir + "/MountainRock.png"), 2f)
                : PsxTexMat("DressRockGrey", new Color(0.5f, 0.5f, 0.52f), TileTexture(TerrainTexDir + "/MountainRock.png"), 2f);
            m.moss = PsxTexMat("DressMoss", new Color(0.3f, 0.45f, 0.2f), TileTexture(TerrainTexDir + "/ForestFloor.png"), 1.5f);
            m.sack = PsxTexMat("DressSack", new Color(0.72f, 0.62f, 0.44f), TileTexture(StreetTexDir + "/Cloth.png"), 1f);
            m.rope = PsxTexMat("DressRope", new Color(0.62f, 0.5f, 0.32f), TileTexture(StreetTexDir + "/Cloth.png"), 0.6f);
            m.hay = PsxTexMat("DressHay", new Color(0.85f, 0.7f, 0.32f), TileTexture(StreetTexDir + "/Sand.png"), 1.2f);
            m.rust = PsxTexMat("DressRust", new Color(0.55f, 0.3f, 0.18f), TileTexture(StreetTexDir + "/Corrugated.png"), 1.4f);
            m.cactus = PsxTexMat("DressCactus", new Color(0.36f, 0.55f, 0.3f), TileTexture(StreetTexDir + "/Cactus.png"), 1f);
            m.sage = PsxTexMat("DressSage", new Color(0.52f, 0.58f, 0.46f), leafTex, 1.4f);
            m.bone = PMat("DressBone", new Color(0.86f, 0.83f, 0.74f), 1f);
            m.coal = PMat("DressCoal", new Color(0.09f, 0.09f, 0.1f), 1f);
            m.ember = PMat("DressEmber", new Color(1f, 0.45f, 0.15f), 1f, new Color(1f, 0.4f, 0.1f));
            m.pumpkin = PMat("DressPumpkin", new Color(0.92f, 0.5f, 0.14f), 1f);
            m.pool = PMat("DressPool", new Color(0.1f, 0.2f, 0.3f), 1f);
            m.sludge = PMat("DressSludge", new Color(0.12f, 0.2f, 0.12f), 1f);
            m.capRed = PMat("DressCapRed", new Color(0.9f, 0.22f, 0.22f), 1f);
            m.capTan = PMat("DressCapTan", new Color(0.78f, 0.6f, 0.42f), 1f);
            m.stalk = PMat("DressStalk", new Color(0.93f, 0.9f, 0.8f), 1f);
            m.fish = PMat("DressFish", new Color(0.7f, 0.76f, 0.8f), 1f);
            m.wax = PMat("DressWax", new Color(0.9f, 0.86f, 0.72f), 1f);
            m.glowCap = st == ZoneTextureStyle.Undercity ? NeonMat(1) : m.crystal;
            m.lanterns = new[]
            {
                glow, PMat("DressLanternRed", new Color(0.9f, 0.3f, 0.2f), 1f, new Color(0.9f, 0.25f, 0.12f)),
                PMat("DressLanternGold", new Color(1f, 0.8f, 0.35f), 1f, new Color(0.9f, 0.65f, 0.2f)),
            };
            if (st == ZoneTextureStyle.Undercity) m.pots = new[] { m.brass, m.rust, m.iron };
            else if (st == ZoneTextureStyle.Mine) m.pots = new[] { m.iron, m.wood, m.rust };
            else if (dust) m.pots = new[] { m.clay, m.clay, m.wood };
            else if (st == ZoneTextureStyle.Keep) m.pots = new[] { m.stone, m.iron };
            else if (st == ZoneTextureStyle.Docks) m.pots = new[] { m.wood, m.clay, m.stone };
            else m.pots = new[] { m.clay, m.stone, m.glazes[0], m.glazes[1], m.glazes[2] };

            m.fxGlow = StreetFxMat("StreetFxGlow", AuraGlowTex, true);
            m.fxSpark = StreetFxMat("StreetFxSpark", AuraSparkTex, true);
            m.fxDot = StreetFxMat("StreetFxDot", AuraGlowTex, false);
            m.fxLeaf = StreetFxMat("StreetFxLeaf", StreetFxDir + "/Leaf.png", false);
            m.fxPetal = StreetFxMat("StreetFxPetal", StreetFxDir + "/Petal.png", false);
            m.fxPuff = StreetFxMat("StreetFxPuff", StreetFxDir + "/Puff.png", false);
            m.fxFly = StreetFxMat("StreetFxButterfly", StreetFxDir + "/Butterfly.png", false);
            m.fxBubble = StreetFxMat("StreetFxBubble", StreetFxDir + "/Bubble.png", false);
            m.fxDrip = StreetFxMat("StreetFxDrip", StreetFxDir + "/Drip.png", false);
            return m;
        }

        // ------------------------------------------------------------------ recipes: greenery

        private static void DrTuft(DressCtx d, Vector3 p, float s, Material mat)
        {
            d.Soft("Tuft", d.Pick(3) == 0 ? DressTuftWide() : DressTuft(), p, new Vector3(s, s * d.R(0.75f, 1.25f), s), mat, d.Yaw());
        }

        private static void DrWeeds(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            int n = 2 + d.Pick(3);
            for (int k = 0; k < n; k++) DrTuft(d, p + side * d.R(-0.35f, 0.35f) + o * d.R(-0.15f, 0.15f), d.R(0.35f, 0.7f), d.m.tuft);
            if (d.m.blooms && d.Chance(0.35f)) DrFlowers(d, p + side * d.R(-0.2f, 0.2f), 2 + d.Pick(2), 0.2f, 0.35f);
        }

        /// <summary>A clump of flowers: a tuft of leaves and n blooms on stems (buds or round daisies).</summary>
        private static void DrFlowers(DressCtx d, Vector3 p, int n, float spread, float tall)
        {
            DrTuft(d, p, 0.45f, d.m.tuft);
            if (!d.m.blooms) return;
            Material petal = d.Petal();
            for (int k = 0; k < n; k++)
            {
                Vector3 at = p + new Vector3(d.R(-spread, spread), 0f, d.R(-spread, spread));
                float h = tall * d.R(0.7f, 1.2f);
                d.Soft("Stem", DressBlade(), at, new Vector3(1f, h, 1f), d.m.tuft, Quaternion.identity);
                bool round = d.Chance(0.4f);
                d.Soft("Bloom", round ? BlobMesh() : GemMesh(), at + new Vector3(0f, h, 0f), round ? new Vector3(0.2f, 0.1f, 0.2f) : new Vector3(0.13f, 0.2f, 0.13f),
                    d.Chance(0.75f) ? petal : d.Petal(), d.Yaw());
            }
        }

        private static void DrBush(DressCtx d, Vector3 p, float s, bool blooms)
        {
            int n = 2 + d.Pick(2);
            for (int k = 0; k < n; k++)
            {
                float w = s * d.R(0.75f, 1.15f) * (k == 0 ? 1f : 0.75f);
                float h = w * d.R(0.62f, 0.85f);
                Vector3 at = k == 0 ? p : p + new Vector3(d.R(-0.45f, 0.45f) * s, 0f, d.R(-0.45f, 0.45f) * s);
                d.Add("Bush", BlobMesh(), at + new Vector3(0f, h * 0.42f, 0f), new Vector3(w, h, w), d.Leaf(), d.Yaw());
            }
            if (!blooms) return;
            Material petal = d.Petal();
            int b = 4 + d.Pick(4);
            for (int k = 0; k < b; k++)
            {
                float a = d.R(0f, Mathf.PI * 2f), r = s * d.R(0.12f, 0.36f);
                d.Soft("Bloom", GemMesh(), p + new Vector3(Mathf.Cos(a) * r, s * d.R(0.5f, 0.66f), Mathf.Sin(a) * r), Vector3.one * d.R(0.1f, 0.16f), petal, d.Yaw());
            }
        }

        /// <summary>A rounded mound of hedge: what turns a hedge's flat top into something clipped by hand.</summary>
        private static void DrHedgeMound(DressCtx d, Vector3 p)
        {
            float w = d.R(2.2f, 3.3f);
            d.Add("HedgeMound", DressDome(), p - new Vector3(0f, 0.1f, 0f), new Vector3(w, d.R(1.6f, 2.6f), w * d.R(0.8f, 1.1f)), d.Leaf(), d.Yaw());
            if (d.m.blooms && d.Chance(0.5f))
            {
                Material petal = d.Petal();
                for (int k = 0; k < 5; k++)
                {
                    float a = d.R(0f, Mathf.PI * 2f), r = w * d.R(0.1f, 0.3f);
                    d.Soft("Bloom", GemMesh(), p + new Vector3(Mathf.Cos(a) * r, d.R(0.55f, 0.8f), Mathf.Sin(a) * r), Vector3.one * 0.15f, petal, d.Yaw());
                }
            }
        }

        private static void DrBoulder(DressCtx d, Vector3 p, float s, bool mossy)
        {
            float w = s * d.R(0.9f, 1.3f), h = s * d.R(0.55f, 0.8f), l = s * d.R(0.8f, 1.2f);
            d.Add("Boulder", BlobMesh(), p + new Vector3(0f, h * 0.36f, 0f), new Vector3(w, h, l), d.m.rock, Quaternion.Euler(d.R(-10f, 10f), d.R(0f, 360f), d.R(-10f, 10f)));
            if (d.Chance(0.6f))
            {
                float s2 = s * d.R(0.35f, 0.55f);
                Vector3 off = d.Yaw() * new Vector3(Mathf.Max(w, l) * 0.5f + s2 * 0.2f, 0f, 0f);
                d.Add("Boulder", BlobMesh(), p + off + new Vector3(0f, s2 * 0.3f, 0f), new Vector3(s2 * 1.2f, s2 * 0.75f, s2), d.m.rock, d.Yaw());
            }
            if (mossy && d.Chance(0.6f))
            {
                float r = Mathf.Min(w, l) * 0.7f;
                d.Add("BoulderMoss", BlobMesh(), p + new Vector3(0f, h * 0.74f, 0f), new Vector3(r, h * 0.3f, r), d.m.moss, d.Yaw());
            }
        }

        private static void DrMushrooms(DressCtx d, Vector3 p, int n, float s, Material cap)
        {
            for (int k = 0; k < n; k++)
            {
                Vector3 at = k == 0 ? p : p + new Vector3(d.R(-0.35f, 0.35f), 0f, d.R(-0.35f, 0.35f));
                float h = s * d.R(0.6f, 1.2f);
                d.Soft("ShroomStalk", CylMesh(), at + new Vector3(0f, h * 0.5f, 0f), new Vector3(h * 0.28f, h * 0.5f, h * 0.28f), d.m.stalk, Quaternion.identity);
                d.Soft("ShroomCap", DressDome(), at + new Vector3(0f, h * 0.9f, 0f), new Vector3(h * 1.3f, h * 1.1f, h * 1.3f), cap, d.Yaw());
            }
        }

        /// <summary>A ring of little toadstools round a patch of longer grass.</summary>
        private static void DrFairyRing(DressCtx d, Vector3 p)
        {
            int n = 7 + d.Pick(3);
            float r = d.R(0.8f, 1.05f);
            Material cap = d.Chance(0.5f) ? d.m.capRed : d.m.capTan;
            for (int k = 0; k < n; k++)
            {
                float a = (k + d.R(-0.2f, 0.2f)) / n * Mathf.PI * 2f;
                DrMushrooms(d, p + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), 1, 0.22f, cap);
            }
            DrTuft(d, p, 0.8f, d.m.tuft);
        }

        private static void DrRoots(DressCtx d, Vector3 p, float s)
        {
            int n = 3 + d.Pick(2);
            float a0 = d.R(0f, 360f);
            for (int k = 0; k < n; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, a0 + k * 360f / n + d.R(-20f, 20f), 0f) * Vector3.forward;
                float len = d.R(1f, 1.6f) * s;
                d.Add("Root", ConeMesh(), p + dir * (0.25f + len * 0.5f) + new Vector3(0f, 0.06f, 0f), new Vector3(0.36f * s, len, 0.3f * s), d.m.bark,
                    Quaternion.FromToRotation(Vector3.up, dir));
            }
        }

        private static void DrReeds(DressCtx d, Vector3 p, bool dead)
        {
            Material mat = dead ? d.m.dry : d.m.tuft;
            int n = 3 + d.Pick(3);
            for (int k = 0; k < n; k++)
            {
                Vector3 at = p + new Vector3(d.R(-0.4f, 0.4f), -0.15f, d.R(-0.4f, 0.4f));
                float h = d.R(1.2f, 2.1f);
                d.Soft("Reed", DressTuftTall(), at, new Vector3(0.7f, h, 0.7f), mat, d.Yaw());
                if (d.Chance(0.5f))
                    d.Soft("ReedHead", CylMesh(), at + new Vector3(d.R(-0.08f, 0.08f), h * 0.92f, d.R(-0.08f, 0.08f)), new Vector3(0.09f, 0.16f, 0.09f), d.m.bark, Quaternion.identity);
            }
        }

        private static void DrLilyPads(DressCtx d, Vector3 p, int n)
        {
            for (int k = 0; k < n; k++)
            {
                Vector3 at = p + new Vector3(d.R(-1.6f, 1.6f), 0.015f, d.R(-1.6f, 1.6f));
                float s = d.R(0.45f, 0.9f);
                d.Soft("LilyPad", DressDisc(), at, new Vector3(s, 1f, s), d.Leaf(), d.Yaw());
                if (d.Chance(0.3f)) d.Soft("LilyBloom", GemMesh(), at + new Vector3(0f, 0.09f, 0f), Vector3.one * 0.2f, d.Petal(), d.Yaw());
            }
        }

        private static void DrPuddle(DressCtx d, Vector3 p, float s, Material mat, bool bubbles)
        {
            d.Soft("Puddle", DressDisc(), p + new Vector3(0f, 0.012f, 0f), new Vector3(s, 1f, s * d.R(0.6f, 1f)), mat, d.Yaw());
            if (!bubbles) return;
            for (int k = 0; k < 2; k++)
                d.Soft("PuddleBubble", DressDome(), p + new Vector3(d.R(-0.3f, 0.3f) * s, 0.01f, d.R(-0.2f, 0.2f) * s), Vector3.one * d.R(0.12f, 0.24f), d.m.glowCap, Quaternion.identity);
            if (d.PropFx()) SfxBubbles(d, p + new Vector3(0f, 0.1f, 0f), new Vector3(s * 0.6f, 0.05f, s * 0.5f), new Color(0.6f, 1f, 0.6f));
        }

        /// <summary>Tall sunflowers: a stem, a couple of leaves and a big round head.</summary>
        private static void DrSunflowers(DressCtx d, Vector3 p)
        {
            int n = 2 + d.Pick(3);
            for (int k = 0; k < n; k++)
            {
                Vector3 at = p + new Vector3(d.R(-0.5f, 0.5f), 0f, d.R(-0.5f, 0.5f));
                float h = d.R(1.3f, 2.2f);
                d.Soft("SunStem", CylMesh(), at + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.07f, h * 0.5f, 0.07f), d.m.tuft, Quaternion.identity);
                d.Soft("SunLeaf", BlobMesh(), at + new Vector3(0.14f, h * 0.45f, 0f), new Vector3(0.4f, 0.08f, 0.22f), d.Leaf(), d.Yaw());
                Quaternion face = Quaternion.Euler(d.R(55f, 80f), d.R(0f, 360f), 0f);
                d.Soft("SunHead", CylMesh(), at + new Vector3(0f, h, 0f), new Vector3(0.6f, 0.03f, 0.6f), d.m.petal[2], face);
                d.Soft("SunHeart", BlobMesh(), at + new Vector3(0f, h, 0f), new Vector3(0.3f, 0.12f, 0.3f), d.m.bark, face);
            }
        }

        private static void DrBarrelCactus(DressCtx d, Vector3 p, float s)
        {
            d.Add("BarrelCactus", BlobMesh(), p + new Vector3(0f, s * 0.42f, 0f), new Vector3(s, s * 0.95f, s), d.m.cactus, d.Yaw());
            for (int k = 0; k < 3; k++)
                d.Soft("CactusBloom", GemMesh(), p + Quaternion.Euler(0f, k * 120f, 0f) * new Vector3(s * 0.12f, s * 0.9f, 0f), Vector3.one * (0.13f * s), d.m.petal[k == 0 ? 3 : 2], d.Yaw());
        }

        /// <summary>Prickly pear: flat round pads growing out of each other, a red fruit or two on top.</summary>
        private static void DrPricklyPear(DressCtx d, Vector3 p, float s)
        {
            int n = 3 + d.Pick(3);
            Vector3 at = p + new Vector3(0f, 0.3f * s, 0f);
            for (int k = 0; k < n; k++)
            {
                Quaternion q = Quaternion.Euler(d.R(-18f, 18f), d.R(0f, 360f), d.R(-25f, 25f));
                d.Add("PearPad", BlobMesh(), at, new Vector3(0.6f, 0.75f, 0.16f) * s, d.m.cactus, q);
                if (d.Chance(0.4f)) d.Soft("PearFruit", GemMesh(), at + q * new Vector3(0f, 0.42f * s, 0f), Vector3.one * (0.12f * s), d.m.petal[4], d.Yaw());
                at = k % 2 == 0 ? at + q * new Vector3(d.R(-0.2f, 0.2f), 0.55f, 0f) * s : p + new Vector3(d.R(-0.4f, 0.4f) * s, 0.3f * s, d.R(-0.3f, 0.3f) * s);
            }
        }

        private static void DrAgave(DressCtx d, Vector3 p, float s)
        {
            d.Add("Agave", DressTuftWide(), p, new Vector3(1.5f, 1.2f, 1.5f) * s, d.m.sage, d.Yaw());
            d.Add("AgaveHeart", DressTuft(), p, new Vector3(0.9f, 1.4f, 0.9f) * s, d.m.sage, d.Yaw());
        }

        private static void DrSage(DressCtx d, Vector3 p, float s)
        {
            for (int k = 0; k < 2; k++)
                d.Add("Sagebrush", BlobMesh(), p + new Vector3(d.R(-0.3f, 0.3f) * s * k, 0.22f * s, d.R(-0.3f, 0.3f) * s * k), new Vector3(d.R(0.8f, 1.1f), d.R(0.45f, 0.6f), d.R(0.8f, 1.1f)) * s, d.m.sage, d.Yaw());
            DrTuft(d, p + new Vector3(0.3f * s, 0f, 0.2f * s), 0.5f, d.m.dry);
        }

        private static void DrTumbleweed(DressCtx d, Vector3 p, float s)
        {
            d.Add("Tumbleweed", BlobMesh(), p + new Vector3(0f, s * 0.44f, 0f), Vector3.one * s, d.m.dry, Quaternion.Euler(d.R(0f, 360f), d.R(0f, 360f), d.R(0f, 360f)));
            d.Soft("TumbleTwigs", DressTuftWide(), p + new Vector3(0f, s * 0.3f, 0f), Vector3.one * (s * 1.1f), d.m.bark, Quaternion.Euler(d.R(0f, 360f), d.R(0f, 360f), 180f));
        }

        // ------------------------------------------------------------------ recipes: pots, barrels and goods

        /// <summary>A potted plant: 0 bush, 1 flowers, 2 clipped ball on a stem, 3 spiky, 4 cactus, 5 glowing toadstools.</summary>
        private static void DrPlanter(DressCtx d, Vector3 p, float s, int kind)
        {
            bool urn = d.Chance(0.5f);
            float w = s * (urn ? 0.78f : 0.72f), h = s * (urn ? 0.85f : 0.6f);
            d.Add(urn ? "Urn" : "Pot", urn ? DressUrn() : DressPot(), p, new Vector3(w, h, w), d.Pot(), d.Yaw());
            Vector3 top = p + new Vector3(0f, h, 0f);
            float lip = w * (urn ? 0.6f : 0.95f);
            if (kind == 0)
            {
                d.Add("PotBush", BlobMesh(), top + new Vector3(0f, lip * 0.3f, 0f), new Vector3(lip * 1.5f, lip * 1.1f, lip * 1.5f), d.Leaf(), d.Yaw());
                if (d.m.blooms && d.Chance(0.5f))
                    for (int k = 0; k < 4; k++)
                        d.Soft("Bloom", GemMesh(), top + Quaternion.Euler(0f, k * 90f + d.R(0f, 40f), 0f) * new Vector3(lip * 0.4f, lip * 0.75f, 0f), Vector3.one * 0.13f, d.Petal(), d.Yaw());
            }
            else if (kind == 1)
            {
                Material petal = d.Petal();
                for (int k = 0; k < 5; k++)
                {
                    Vector3 at = top + new Vector3(d.R(-0.3f, 0.3f) * lip, 0f, d.R(-0.3f, 0.3f) * lip);
                    float fh = d.R(0.25f, 0.5f);
                    d.Soft("Stem", DressBlade(), at, new Vector3(1f, fh, 1f), d.m.tuft, Quaternion.identity);
                    d.Soft("Bloom", GemMesh(), at + new Vector3(0f, fh, 0f), new Vector3(0.14f, 0.2f, 0.14f), petal, d.Yaw());
                }
                d.Soft("PotLeaves", DressTuftWide(), top, Vector3.one * (lip * 1.1f), d.m.tuft, d.Yaw());
            }
            else if (kind == 2)
            {
                float stem = s * d.R(0.5f, 0.8f);
                d.Add("TopiaryStem", CylMesh(), top + new Vector3(0f, stem * 0.5f, 0f), new Vector3(0.08f, stem * 0.5f, 0.08f), d.m.bark);
                d.Add("TopiaryBall", BlobMesh(), top + new Vector3(0f, stem + s * 0.25f, 0f), Vector3.one * (s * 0.7f), d.Leaf(), d.Yaw());
                if (d.Chance(0.4f)) d.Add("TopiaryBall", BlobMesh(), top + new Vector3(0f, stem + s * 0.72f, 0f), Vector3.one * (s * 0.42f), d.Leaf(), d.Yaw());
            }
            else if (kind == 3) d.Add("PotSpikes", DressTuftWide(), top, new Vector3(1.2f, 1f, 1.2f) * s, d.m.sage, d.Yaw());
            else if (kind == 4)
            {
                d.Add("PotCactus", BlobMesh(), top + new Vector3(0f, lip * 0.45f, 0f), new Vector3(lip * 0.95f, lip * 1.3f, lip * 0.95f), d.m.cactus, d.Yaw());
                d.Soft("CactusBloom", GemMesh(), top + new Vector3(0f, lip * 1.15f, 0f), Vector3.one * 0.13f, d.m.petal[3], d.Yaw());
            }
            else DrMushrooms(d, top - new Vector3(0f, 0.02f, 0f), 2 + d.Pick(2), 0.24f, d.m.glowCap);
        }

        /// <summary>An upright barrel with two hoops. top: 0 a lid, 1 rain water, 2 something glowing.</summary>
        private static void DrBarrel(DressCtx d, Vector3 p, float s, Material body, Material band, int top)
        {
            float w = 0.8f * s, h = 1.05f * s;
            d.Add("Barrel", DressBarrel(), p, new Vector3(w, h, w), body, d.Yaw());
            foreach (float t in new[] { 0.24f, 0.76f })
                d.Add("BarrelHoop", CylMesh(), p + new Vector3(0f, h * t, 0f), new Vector3(w * 0.955f, 0.03f * s, w * 0.955f), band);
            if (top > 0) d.Soft("BarrelTop", DressDisc(), p + new Vector3(0f, h + 0.006f, 0f), new Vector3(w * 0.72f, 1f, w * 0.72f), top == 1 ? d.m.pool : d.m.glowCap, Quaternion.identity);
        }

        /// <summary>A barrel on its side, its middle at p (the ground under it), lying along 'axis'.</summary>
        private static void DrBarrelLying(DressCtx d, Vector3 p, Vector3 axis, float s, Material body, Material band)
        {
            float w = 0.8f * s, h = 1.05f * s;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);
            Vector3 foot = p - axis * (h * 0.5f) + new Vector3(0f, w * 0.5f, 0f);
            d.Add("Barrel", DressBarrel(), foot, new Vector3(w, h, w), body, rot);
            foreach (float t in new[] { 0.24f, 0.76f })
                d.Add("BarrelHoop", CylMesh(), foot + axis * (h * t), new Vector3(w * 0.955f, 0.03f * s, w * 0.955f), band, rot);
        }

        /// <summary>Two barrels side by side with a third on top, or lying in front.</summary>
        private static void DrBarrelStack(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            DrBarrel(d, p - side * 0.45f, 1f, d.m.wood, d.m.iron, 0);
            DrBarrel(d, p + side * 0.45f, d.R(0.85f, 1f), d.m.wood, d.m.iron, d.Chance(0.3f) ? 1 : 0);
            if (d.Chance(0.5f)) DrBarrel(d, p + new Vector3(0f, 1.05f, 0f), 0.85f, d.m.wood, d.m.iron, 0);
        }

        private static void DrSacks(DressCtx d, Vector3 p, Vector3 o, int n)
        {
            Vector3 side = d.Side(o);
            for (int k = 0; k < n; k++)
            {
                int row = k < 2 ? 0 : 1;
                Vector3 at = p + side * ((row == 0 ? k - 0.5f : 0f) * 0.62f) + new Vector3(0f, 0.2f + row * 0.36f, 0f) + o * d.R(-0.06f, 0.06f);
                d.Add("Sack", BlobMesh(), at, new Vector3(0.72f, 0.46f, 0.52f), d.m.sack, Quaternion.LookRotation(o) * Quaternion.Euler(d.R(-8f, 8f), d.R(-25f, 25f), d.R(-10f, 10f)));
            }
        }

        /// <summary>A row of jars and urns of different sizes.</summary>
        private static void DrJars(DressCtx d, Vector3 p, Vector3 o, int n, Material[] mats)
        {
            Vector3 side = d.Side(o);
            for (int k = 0; k < n; k++)
            {
                float s = d.R(0.45f, 1.05f);
                Vector3 at = p + side * ((k - (n - 1) * 0.5f) * 0.6f) + o * d.R(-0.1f, 0.12f);
                d.Add("Jar", d.Chance(0.7f) ? DressUrn() : DressPot(), at, new Vector3(s * 0.7f, s, s * 0.7f), mats[d.Pick(mats.Length)], d.Yaw());
            }
        }

        /// <summary>A round basket heaped with something: 0 fish, 1 apples, 2 coal, 3 ore or crystal, 4 peppers.</summary>
        private static void DrBasket(DressCtx d, Vector3 p, float s, int fill)
        {
            d.Add("Basket", DressPot(), p, new Vector3(0.8f * s, 0.42f * s, 0.8f * s), d.m.hay, d.Yaw());
            Material mat = fill == 0 ? d.m.fish : fill == 1 ? d.m.paint[0] : fill == 2 ? d.m.coal : fill == 3 ? d.m.crystal : d.m.petal[4];
            for (int k = 0; k < 5; k++)
                d.Soft("BasketFill", fill == 0 ? BlobMesh() : GemMesh(), p + new Vector3(d.R(-0.2f, 0.2f) * s, 0.42f * s + d.R(0f, 0.08f), d.R(-0.2f, 0.2f) * s),
                    fill == 0 ? new Vector3(0.34f, 0.1f, 0.12f) * s : Vector3.one * (0.17f * s), mat, d.Yaw());
        }

        private static void DrPumpkins(DressCtx d, Vector3 p, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float s = d.R(0.35f, 0.7f);
                Vector3 at = k == 0 ? p : p + new Vector3(d.R(-0.5f, 0.5f), 0f, d.R(-0.3f, 0.3f));
                d.Add("Pumpkin", BlobMesh(), at + new Vector3(0f, s * 0.36f, 0f), new Vector3(s, s * 0.78f, s), d.m.pumpkin, d.Yaw());
                d.Soft("PumpkinStem", CylMesh(), at + new Vector3(0f, s * 0.76f, 0f), new Vector3(0.07f, 0.06f, 0.07f), d.m.tuft, Quaternion.Euler(d.R(-20f, 20f), 0f, d.R(-20f, 20f)));
            }
        }

        /// <summary>A round bale on its side and a heap of loose hay.</summary>
        private static void DrHay(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            d.Add("HayBale", CylMesh(), p - side * 0.5f + new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 0.5f, 1.1f), d.m.hay, Quaternion.FromToRotation(Vector3.up, side));
            d.Add("HayHeap", DressDome(), p + side * 0.6f, new Vector3(1.1f, 1.2f, 0.9f), d.m.hay, d.Yaw());
        }

        private static void DrWoodpile(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            Quaternion lie = Quaternion.FromToRotation(Vector3.up, o);
            int[] rows = { 4, 3, 2 };
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < rows[r]; k++)
                    d.Add("Log", CylMesh(), p + side * ((k - (rows[r] - 1) * 0.5f) * 0.34f) + new Vector3(0f, 0.17f + r * 0.29f, 0f) + o * d.R(-0.05f, 0.05f),
                        new Vector3(0.32f, d.R(0.38f, 0.46f), 0.32f), d.m.bark, lie);
        }

        /// <summary>What a cart, a wall top or a platform carries, for this street. 'p' is the top of whatever it sits on.</summary>
        private static void DrLoad(DressCtx d, Vector3 p, Vector3 o)
        {
            switch (d.st)
            {
                case ZoneTextureStyle.Docks:
                    if (d.Chance(0.5f)) DrBarrel(d, p, 0.7f, d.m.wood, d.m.iron, 0); else DrBasket(d, p, 0.9f, 0);
                    break;
                case ZoneTextureStyle.Forge:
                    if (d.Chance(0.5f)) d.Add("CoalLoad", DressDome(), p, new Vector3(0.9f, 0.9f, 1.3f), d.m.coal, Quaternion.LookRotation(o)); else DrJars(d, p, o, 2, d.m.glazes);
                    break;
                case ZoneTextureStyle.Mine:
                case ZoneTextureStyle.Undercity:
                    DrOrePile(d, p, 0.7f);
                    break;
                case ZoneTextureStyle.Keep:
                    DrBoulder(d, p, 0.6f, false);
                    break;
                case ZoneTextureStyle.Desert:
                case ZoneTextureStyle.Boomtown:
                    if (d.Chance(0.5f)) DrSacks(d, p, o, 2); else d.Add("HayLoad", DressDome(), p, new Vector3(0.9f, 1.1f, 1.4f), d.m.hay, Quaternion.LookRotation(o));
                    break;
                default:
                    if (d.Chance(0.4f)) DrPumpkins(d, p, 3); else if (d.Chance(0.5f)) DrSacks(d, p, o, 2); else d.Add("HayLoad", DressDome(), p, new Vector3(0.9f, 1.1f, 1.4f), d.m.hay, Quaternion.LookRotation(o));
                    break;
            }
        }

        /// <summary>A handcart standing along the wall: a bed on two round wheels, shafts resting on the ground, a load.</summary>
        private static void DrCart(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            p -= side * 0.55f;      // the shafts stick out one end; this centres the whole thing on its spot
            Quaternion along = Quaternion.LookRotation(side);
            d.Box("CartBed", p + new Vector3(0f, 0.62f, 0f), new Vector3(0.95f, 0.1f, 1.7f), d.m.wood, along);
            d.Box("CartEnd", p - side * 0.85f + new Vector3(0f, 0.8f, 0f), new Vector3(0.95f, 0.3f, 0.06f), d.m.wood, along);
            Quaternion axle = Quaternion.FromToRotation(Vector3.up, o);
            foreach (float s in new[] { -1f, 1f })
            {
                d.Box("CartSide", p + o * (s * 0.45f) + new Vector3(0f, 0.8f, 0f), new Vector3(0.06f, 0.3f, 1.7f), d.m.wood, along);
                Vector3 hub = p + o * (s * 0.56f) + new Vector3(0f, 0.5f, 0f);
                d.Add("CartWheel", CylMesh(), hub, new Vector3(0.9f, 0.035f, 0.9f), d.m.wood, axle);
                d.Add("CartRim", DressTorusThin(), hub, new Vector3(0.96f, 0.9f, 0.96f), d.m.iron, axle);
                d.Add("CartHub", BlobMesh(), hub + o * (s * 0.05f), Vector3.one * 0.2f, d.m.iron);
                d.Box("CartShaft", p + o * (s * 0.38f) + side * 1.45f + new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.07f, 1.3f), d.m.wood, along * Quaternion.Euler(24f, 0f, 0f));
            }
            DrLoad(d, p + new Vector3(0f, 0.67f, 0f), side);
        }

        /// <summary>A round stone well with a rolled rim, a windlass under a little roof and a bucket.</summary>
        private static void DrWell(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            d.Add("WellWall", CylMesh(), p + new Vector3(0f, 0.42f, 0f), new Vector3(1.9f, 0.42f, 1.9f), d.m.stone);
            d.Add("WellRim", DressTorusFat(), p + new Vector3(0f, 0.84f, 0f), new Vector3(1.75f, 1.1f, 1.75f), d.m.stone);
            d.Add("WellWater", DressDisc(), p + new Vector3(0f, 0.87f, 0f), new Vector3(1.45f, 1f, 1.45f), d.m.pool);
            foreach (float s in new[] { -1f, 1f })
                d.Box("WellPost", p + side * (s * 0.98f) + new Vector3(0f, 1.35f, 0f), new Vector3(0.14f, 2.3f, 0.14f), d.m.wood, Quaternion.identity);
            d.Add("WellAxle", CylMesh(), p + new Vector3(0f, 2.05f, 0f), new Vector3(0.16f, 0.98f, 0.16f), d.m.wood, Quaternion.FromToRotation(Vector3.up, side));
            d.Add("WellRoof", ConeMesh(), p + new Vector3(0f, 2.95f, 0f), new Vector3(2.7f, 0.9f, 2.7f), d.m.roof, d.Yaw());
            d.Box("WellRope", p + side * 0.2f + new Vector3(0f, 1.6f, 0f), new Vector3(0.03f, 0.9f, 0.03f), d.m.rope, Quaternion.identity);
            d.Add("WellBucket", DressPot(), p + side * 0.2f + new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.3f, 0.34f), d.m.wood);
        }

        /// <summary>A short post with a round glowing globe under a little cap.</summary>
        private static void DrGlobeLamp(DressCtx d, Vector3 p, float h, Material post, Material globe)
        {
            d.Add("GlobePost", CylMesh(), p + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.12f, h * 0.5f, 0.12f), post);
            d.Add("GlobeFoot", DressDome(), p, new Vector3(0.36f, 0.3f, 0.36f), post);
            d.Add("Globe", BlobMesh(), p + new Vector3(0f, h + 0.14f, 0f), Vector3.one * 0.34f, globe);
            d.Add("GlobeCap", ConeMesh(), p + new Vector3(0f, h + 0.36f, 0f), new Vector3(0.3f, 0.16f, 0.3f), post);
        }

        /// <summary>An iron bowl of coals on three legs, with tongues of flame and a few rising sparks.</summary>
        private static void DrBrazier(DressCtx d, Vector3 p, Material flame, Color fire)
        {
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 120f, 0f);
                d.Box("BrazierLeg", p + q * new Vector3(0.2f, 0.42f, 0f), new Vector3(0.05f, 0.9f, 0.05f), d.m.iron, q * Quaternion.Euler(0f, 0f, 14f));
            }
            d.Add("BrazierBowl", DressPot(), p + new Vector3(0f, 0.78f, 0f), new Vector3(0.78f, 0.3f, 0.78f), d.m.iron);
            d.Add("BrazierCoals", DressDome(), p + new Vector3(0f, 1.04f, 0f), new Vector3(0.6f, 0.3f, 0.6f), d.m.coal);
            for (int k = 0; k < 3; k++)
                d.Soft("BrazierFlame", GemMesh(), p + Quaternion.Euler(0f, k * 120f + 40f, 0f) * new Vector3(0.12f, 1.24f + k * 0.06f, 0f), new Vector3(0.16f, 0.34f, 0.16f), flame, d.Yaw());
            if (d.PropFx()) SfxEmbers(d, p + new Vector3(0f, 1.35f, 0f), fire, 0.25f);
        }

        private static void DrCoalHeap(DressCtx d, Vector3 p, float s)
        {
            d.Add("CoalHeap", DressDome(), p, new Vector3(1.5f, 1.3f, 1.2f) * s, d.m.coal, d.Yaw());
            for (int k = 0; k < 3; k++)
                d.Soft("CoalEmber", GemMesh(), p + Quaternion.Euler(0f, k * 120f + d.R(0f, 60f), 0f) * new Vector3(0.35f * s, 0.32f * s, 0f), Vector3.one * (0.14f * s), d.m.ember, d.Yaw());
        }

        /// <summary>A beehive oven or kiln: a dome with a glowing mouth facing out and a stub of chimney, smoking.</summary>
        private static void DrOven(DressCtx d, Vector3 p, Vector3 o, Material body, float s)
        {
            d.Add("OvenDome", DressDome(), p, new Vector3(2.2f, 3.4f, 2.2f) * s, body, d.Yaw());
            d.Add("OvenMouth", CylMesh(), p + o * (0.98f * s) + new Vector3(0f, 0.45f * s, 0f), new Vector3(0.8f * s, 0.14f, 0.8f * s), d.m.coal, Quaternion.FromToRotation(Vector3.up, o));
            d.Soft("OvenGlow", CylMesh(), p + o * (1.06f * s) + new Vector3(0f, 0.42f * s, 0f), new Vector3(0.56f * s, 0.08f, 0.5f * s), d.m.ember, Quaternion.FromToRotation(Vector3.up, o));
            d.Add("OvenFlue", CylMesh(), p - o * (0.3f * s) + new Vector3(0f, 1.75f * s, 0f), new Vector3(0.4f * s, 0.35f * s, 0.4f * s), body);
            if (d.PropFx()) SfxSmoke(d, p - o * (0.3f * s) + new Vector3(0f, 2.2f * s, 0f), new Color(0.4f, 0.38f, 0.38f), 0.7f, 0.55f);
        }

        private static void DrRopeCoil(DressCtx d, Vector3 p, float s)
        {
            for (int k = 0; k < 3; k++)
                d.Add("RopeCoil", DressTorusFat(), p + new Vector3(0f, (0.1f + k * 0.15f) * s, 0f), new Vector3(0.95f - k * 0.08f, 1f, 0.95f - k * 0.08f) * s, d.m.rope, Quaternion.Euler(0f, k * 37f, 0f));
        }

        /// <summary>A heap of round net floats.</summary>
        private static void DrFloats(DressCtx d, Vector3 p, int n)
        {
            for (int k = 0; k < n; k++)
            {
                float s = d.R(0.32f, 0.5f);
                d.Add("Float", BlobMesh(), p + new Vector3(d.R(-0.4f, 0.4f), s * 0.42f + (k >= 3 ? 0.3f : 0f), d.R(-0.25f, 0.25f)), new Vector3(s, s * 1.1f, s), d.Paint(), d.Yaw());
            }
        }

        /// <summary>A marker buoy bobbing out on the water: a round float, a pole and a pennant.</summary>
        private static void DrBuoy(DressCtx d, Vector3 p)
        {
            Quaternion tilt = Quaternion.Euler(d.R(-8f, 8f), d.R(0f, 360f), d.R(-8f, 8f));
            d.Add("Buoy", BlobMesh(), p, new Vector3(0.9f, 0.8f, 0.9f), d.m.paint[d.Pick(2)], tilt);
            d.Add("BuoyPole", CylMesh(), p + tilt * new Vector3(0f, 0.8f, 0f), new Vector3(0.07f, 0.6f, 0.07f), d.m.iron, tilt);
            d.Box("BuoyFlag", p + tilt * new Vector3(0.18f, 1.25f, 0f), new Vector3(0.36f, 0.22f, 0.02f), d.Paint(), tilt);
        }

        /// <summary>A rowing boat turned keel up against the wall.</summary>
        private static void DrBoat(DressCtx d, Vector3 p, Vector3 o)
        {
            Quaternion along = Quaternion.LookRotation(d.Side(o));
            d.Add("BoatHull", DressDome(), p, new Vector3(1.3f, 1.5f, 3.2f), d.m.wood, along);
            d.Box("BoatKeel", p + new Vector3(0f, 0.74f, 0f), new Vector3(0.08f, 0.1f, 2.6f), d.m.bark, along);
            d.Add("BoatRim", DressTorusThin(), p + new Vector3(0f, 0.05f, 0f), new Vector3(1.32f, 1.2f, 3.2f), d.Paint(), along);
        }

        /// <summary>Fallen stone: boulders and a column drum or two lying where they rolled.</summary>
        private static void DrRubble(DressCtx d, Vector3 p, float s)
        {
            DrBoulder(d, p, 0.7f * s, false);
            int drums = 1 + d.Pick(2);
            for (int k = 0; k < drums; k++)
                d.Add("ColumnDrum", CylMesh(), p + d.Yaw() * new Vector3(0.7f * s, 0.34f * s, 0f), new Vector3(0.68f, 0.32f, 0.68f) * s, d.m.stone,
                    Quaternion.Euler(0f, d.R(0f, 180f), 90f + d.R(-8f, 8f)));
        }

        /// <summary>A huddle of candles of different heights, lit.</summary>
        private static void DrCandles(DressCtx d, Vector3 p, Material flame)
        {
            int n = 3 + d.Pick(3);
            for (int k = 0; k < n; k++)
            {
                Vector3 at = p + new Vector3(d.R(-0.22f, 0.22f), 0f, d.R(-0.18f, 0.18f));
                float h = d.R(0.15f, 0.5f);
                d.Soft("Candle", CylMesh(), at + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.09f, h * 0.5f, 0.09f), d.m.wax, Quaternion.identity);
                d.Soft("CandleFlame", GemMesh(), at + new Vector3(0f, h + 0.07f, 0f), new Vector3(0.05f, 0.12f, 0.05f), flame, Quaternion.identity);
            }
            d.Soft("CandleWax", DressDisc(), p + new Vector3(0f, 0.012f, 0f), new Vector3(0.7f, 1f, 0.6f), d.m.wax, d.Yaw());
        }

        /// <summary>A stone ball on a plinth: a gatepost's finial, a garden ornament.</summary>
        private static void DrOrb(DressCtx d, Vector3 p, Material ball)
        {
            d.Add("OrbPlinth", CylMesh(), p + new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.3f, 0.7f), d.m.stone);
            d.Add("OrbCollar", DressTorusFat(), p + new Vector3(0f, 0.62f, 0f), new Vector3(0.6f, 0.7f, 0.6f), d.m.stone);
            d.Add("Orb", BlobMesh(), p + new Vector3(0f, 1f, 0f), Vector3.one * 0.72f, ball, d.Yaw());
        }

        /// <summary>A bird bath: a pedestal, a wide shallow bowl and water.</summary>
        private static void DrBirdbath(DressCtx d, Vector3 p)
        {
            d.Add("BathFoot", DressDome(), p, new Vector3(0.6f, 0.4f, 0.6f), d.m.stone);
            d.Add("BathStem", CylMesh(), p + new Vector3(0f, 0.45f, 0f), new Vector3(0.18f, 0.4f, 0.18f), d.m.stone);
            d.Add("BathBowl", DressPot(), p + new Vector3(0f, 0.8f, 0f), new Vector3(1.1f, 0.22f, 1.1f), d.m.stone);
            d.Soft("BathWater", DressDisc(), p + new Vector3(0f, 1.03f, 0f), new Vector3(0.95f, 1f, 0.95f), d.m.pool, Quaternion.identity);
        }

        /// <summary>A straw beehive on a stool.</summary>
        private static void DrSkep(DressCtx d, Vector3 p)
        {
            d.Add("SkepStool", CylMesh(), p + new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.2f, 0.7f), d.m.wood);
            for (int k = 0; k < 3; k++)
                d.Add("Skep", DressTorusFat(), p + new Vector3(0f, 0.5f + k * 0.17f, 0f), new Vector3(0.6f - k * 0.13f, 0.9f, 0.6f - k * 0.13f), d.m.hay);
            d.Add("SkepTop", DressDome(), p + new Vector3(0f, 0.9f, 0f), new Vector3(0.36f, 0.3f, 0.36f), d.m.hay);
        }

        /// <summary>A weathered headstone with a rounded top.</summary>
        private static void DrHeadstone(DressCtx d, Vector3 p, Vector3 o)
        {
            Quaternion q = Quaternion.LookRotation(o) * Quaternion.Euler(d.R(-6f, 6f), d.R(-12f, 12f), d.R(-7f, 7f));
            d.Box("Headstone", p + new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.16f), d.m.stone, q);
            d.Add("HeadstoneTop", CylMesh(), p + q * new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 0.08f, 0.6f), d.m.stone, q * Quaternion.Euler(90f, 0f, 0f));
            if (d.Chance(0.5f)) DrTuft(d, p + o * 0.25f, 0.5f, d.m.tuft);
        }

        /// <summary>A wagon wheel leaning against the wall.</summary>
        private static void DrWagonWheel(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 axis = (o + Vector3.up * 0.24f).normalized;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);
            Vector3 hub = p - o * 0.08f + new Vector3(0f, 0.56f, 0f);
            d.Add("WheelRim", DressTorusThin(), hub, new Vector3(1.12f, 1f, 1.12f), d.m.wood, rot);
            for (int k = 0; k < 4; k++)
                d.Box("WheelSpoke", hub, new Vector3(1.04f, 0.05f, 0.05f), d.m.wood, rot * Quaternion.Euler(0f, k * 45f, 0f));
            d.Add("WheelHub", CylMesh(), hub, new Vector3(0.2f, 0.08f, 0.2f), d.m.iron, rot);
        }

        // ------------------------------------------------------------------ recipes: mine and undercity

        private static void DrOrePile(DressCtx d, Vector3 p, float s)
        {
            d.Add("OrePile", DressDome(), p, new Vector3(1.4f, 1.2f, 1.2f) * s, d.m.rock, d.Yaw());
            for (int k = 0; k < 4; k++)
                d.Soft("OreGlint", GemMesh(), p + Quaternion.Euler(0f, k * 90f + d.R(0f, 50f), 0f) * new Vector3(0.36f * s, 0.34f * s, 0f), Vector3.one * (d.R(0.14f, 0.22f) * s), d.m.crystal,
                    Quaternion.Euler(d.R(-30f, 30f), d.R(0f, 360f), d.R(-30f, 30f)));
        }

        /// <summary>A cluster of glowing crystals leaning out of the ground.</summary>
        private static void DrCrystals(DressCtx d, Vector3 p, float s)
        {
            int n = 3 + d.Pick(3);
            for (int k = 0; k < n; k++)
            {
                float h = s * d.R(0.5f, 1.3f) * (k == 0 ? 1.2f : 0.8f);
                Quaternion q = Quaternion.Euler(k == 0 ? 0f : d.R(15f, 40f), d.R(0f, 360f), 0f);
                d.Soft("Crystal", GemMesh(), p + q * new Vector3(0f, h * 0.4f, 0f), new Vector3(h * 0.3f, h, h * 0.3f), d.m.crystal, q);
            }
            d.Add("CrystalBed", DressDome(), p, new Vector3(0.9f, 0.4f, 0.9f) * s, d.m.rock, d.Yaw());
            if (d.PropFx()) SfxGlints(d, p + new Vector3(0f, 0.6f * s, 0f), Vector3.one * (0.8f * s), Color.Lerp(d.z.accentColor, Color.white, 0.4f));
        }

        private static void DrStalagmites(DressCtx d, Vector3 p, float s)
        {
            int n = 2 + d.Pick(2);
            for (int k = 0; k < n; k++)
            {
                float h = s * d.R(0.6f, 1.6f);
                Vector3 at = k == 0 ? p : p + new Vector3(d.R(-0.4f, 0.4f), 0f, d.R(-0.4f, 0.4f)) * s;
                d.Add("Stalagmite", ConeMesh(), at + new Vector3(0f, h * 0.5f, 0f), new Vector3(h * 0.45f, h, h * 0.45f), d.m.rock, d.Yaw());
            }
        }

        /// <summary>An ore tub on four little wheels, heaped up.</summary>
        private static void DrOreCart(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            Quaternion along = Quaternion.LookRotation(side);
            d.Add("OreTub", DressPot(), p + new Vector3(0f, 0.3f, 0f), new Vector3(1.05f, 0.75f, 1.5f), d.m.rust, along);
            d.Add("OreTubRim", DressTorusThin(), p + new Vector3(0f, 1.05f, 0f), new Vector3(1.05f, 1.2f, 1.5f), d.m.iron, along);
            Quaternion axle = Quaternion.FromToRotation(Vector3.up, o);
            foreach (float a in new[] { -0.45f, 0.45f })
                foreach (float b in new[] { -0.4f, 0.4f })
                    d.Add("OreWheel", CylMesh(), p + side * a + o * b + new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.04f, 0.36f), d.m.iron, axle);
            DrOrePile(d, p + new Vector3(0f, 0.95f, 0f), 0.6f);
        }

        /// <summary>An oil drum: ribbed, painted or rusty, sometimes with something glowing in it.</summary>
        private static void DrDrum(DressCtx d, Vector3 p, float s, Material body, bool glowing)
        {
            float h = 0.95f * s;
            d.Add("Drum", CylMesh(), p + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.62f * s, h * 0.5f, 0.62f * s), body, d.Yaw());
            foreach (float t in new[] { 0.33f, 0.67f })
                d.Add("DrumRib", CylMesh(), p + new Vector3(0f, h * t, 0f), new Vector3(0.66f * s, 0.025f, 0.66f * s), body);
            if (!glowing) return;
            d.Soft("DrumGlow", DressDisc(), p + new Vector3(0f, h + 0.01f, 0f), new Vector3(0.52f * s, 1f, 0.52f * s), d.m.glowCap, Quaternion.identity);
            if (d.PropFx()) SfxSpores(d, p + new Vector3(0f, h + 0.3f, 0f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.4f, 1f, 0.8f));
        }

        /// <summary>A pressure tank on its side along the wall: round ends, brass bands, a gauge and a red valve wheel.</summary>
        private static void DrTank(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            Quaternion lie = Quaternion.FromToRotation(Vector3.up, side);
            Vector3 c = p + new Vector3(0f, 0.72f, 0f);
            d.Add("Tank", CylMesh(), c, new Vector3(0.95f, 0.85f, 0.95f), d.m.rust, lie);
            foreach (float s in new[] { -1f, 1f })
            {
                d.Add("TankEnd", BlobMesh(), c + side * (s * 0.85f), new Vector3(0.95f, 0.95f, 0.55f), d.m.rust, Quaternion.LookRotation(side));
                d.Add("TankBand", CylMesh(), c + side * (s * 0.5f), new Vector3(1f, 0.04f, 1f), d.m.brass, lie);
                d.Box("TankFoot", p + side * (s * 0.55f) + new Vector3(0f, 0.14f, 0f), new Vector3(0.7f, 0.28f, 0.16f), d.m.iron, Quaternion.LookRotation(side));
            }
            Quaternion face = Quaternion.FromToRotation(Vector3.up, o);
            d.Add("TankGauge", CylMesh(), c + o * 0.5f + side * 0.2f, new Vector3(0.3f, 0.03f, 0.3f), d.m.brass, face);
            d.Soft("TankDial", DressDisc(), c + o * 0.535f + side * 0.2f, new Vector3(0.22f, 1f, 0.22f), d.m.wax, face);
            d.Add("TankValve", DressTorusThin(), c + o * 0.56f - side * 0.3f, new Vector3(0.34f, 1.4f, 0.34f), d.m.paint[0], face);
        }

        /// <summary>A pipe standing out of the ground with an elbow, puffing steam into the street.</summary>
        private static void DrVent(DressCtx d, Vector3 p, Vector3 o)
        {
            float h = d.R(0.9f, 1.5f);
            d.Add("VentPipe", CylMesh(), p + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.36f, h * 0.5f, 0.36f), d.m.brass);
            d.Add("VentElbow", BlobMesh(), p + new Vector3(0f, h, 0f), Vector3.one * 0.44f, d.m.brass);
            d.Add("VentMouth", CylMesh(), p + o * 0.22f + new Vector3(0f, h, 0f), new Vector3(0.36f, 0.22f, 0.36f), d.m.brass, Quaternion.FromToRotation(Vector3.up, o));
            d.Add("VentCollar", DressTorusThin(), p + new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 1.6f, 0.6f), d.m.iron);
            if (d.PropFx()) SfxSteam(d, p + o * 0.5f + new Vector3(0f, h, 0f), o);
        }

        /// <summary>Cogs leaning on each other: toothed wheels with round hubs.</summary>
        private static void DrGears(DressCtx d, Vector3 p, float s)
        {
            int n = 2 + d.Pick(2);
            for (int k = 0; k < n; k++)
            {
                float r = s * d.R(0.7f, 1.2f);
                Material mat = k % 2 == 0 ? d.m.brass : d.m.rust;
                Quaternion q = Quaternion.Euler(d.R(55f, 80f), d.R(0f, 360f), 0f);
                Vector3 at = p + new Vector3(d.R(-0.3f, 0.3f) * s, r * 0.42f + k * 0.05f, d.R(-0.15f, 0.15f) * s);
                if (k == n - 1 && d.Chance(0.5f)) { q = Quaternion.Euler(0f, d.R(0f, 360f), 0f); at.y = p.y + 0.07f; }
                d.Add("Cog", CylMesh(), at, new Vector3(r, 0.06f, r), mat, q);
                d.Add("CogHub", BlobMesh(), at, new Vector3(r * 0.3f, 0.2f, r * 0.3f), d.m.iron, q);
                for (int t = 0; t < 6; t++)
                    d.Box("CogTooth", at + q * (Quaternion.Euler(0f, t * 60f, 0f) * new Vector3(r * 0.53f, 0f, 0f)), new Vector3(r * 0.16f, 0.11f, r * 0.2f), mat, q * Quaternion.Euler(0f, t * 60f, 0f));
            }
        }

        /// <summary>A cable drum on its side: two round cheeks, the drum and turns of cable.</summary>
        private static void DrSpool(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            Quaternion lie = Quaternion.FromToRotation(Vector3.up, side);
            Vector3 c = p + new Vector3(0f, 0.55f, 0f);
            foreach (float s in new[] { -1f, 1f })
                d.Add("SpoolCheek", CylMesh(), c + side * (s * 0.4f), new Vector3(1.1f, 0.04f, 1.1f), d.m.wood, lie);
            d.Add("SpoolDrum", CylMesh(), c, new Vector3(0.5f, 0.4f, 0.5f), d.m.wood, lie);
            for (int k = 0; k < 3; k++)
                d.Add("SpoolCable", DressTorusFat(), c + side * ((k - 1) * 0.2f), new Vector3(0.62f, 0.8f, 0.62f), d.m.coal, lie);
        }

        private static void DrScrap(DressCtx d, Vector3 p, float s)
        {
            d.Add("ScrapHeap", DressDome(), p, new Vector3(1.5f, 1.1f, 1.2f) * s, d.m.rust, d.Yaw());
            for (int k = 0; k < 3; k++)
                d.Box("ScrapPlate", p + d.Yaw() * new Vector3(0.3f * s, 0.3f * s, 0f), new Vector3(0.6f, 0.04f, 0.4f) * s, k == 0 ? d.m.brass : d.m.iron,
                    Quaternion.Euler(d.R(-40f, 40f), d.R(0f, 360f), d.R(-40f, 40f)));
            d.Add("ScrapRing", DressTorusThin(), p + new Vector3(0.2f * s, 0.5f * s, 0f), Vector3.one * (0.6f * s), d.m.iron, Quaternion.Euler(d.R(20f, 70f), d.R(0f, 360f), 0f));
        }

        /// <summary>An upright boiler: a riveted drum with a domed top, bands, a gauge, a firebox glow and a flue puffing steam.</summary>
        private static void DrBoiler(DressCtx d, Vector3 p, Vector3 o)
        {
            d.Add("Boiler", CylMesh(), p + new Vector3(0f, 1.1f, 0f), new Vector3(1.7f, 1.1f, 1.7f), d.m.rust);
            d.Add("BoilerTop", DressDome(), p + new Vector3(0f, 2.2f, 0f), new Vector3(1.7f, 1.3f, 1.7f), d.m.rust);
            foreach (float y in new[] { 0.5f, 1.3f, 2.1f })
                d.Add("BoilerBand", CylMesh(), p + new Vector3(0f, y, 0f), new Vector3(1.76f, 0.05f, 1.76f), d.m.brass);
            Quaternion face = Quaternion.FromToRotation(Vector3.up, o);
            d.Soft("BoilerFire", CylMesh(), p + o * 0.84f + new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.04f, 0.5f), d.m.ember, face);
            d.Add("BoilerGauge", CylMesh(), p + o * 0.86f + new Vector3(0f, 1.6f, 0f), new Vector3(0.36f, 0.03f, 0.36f), d.m.brass, face);
            d.Soft("BoilerDial", DressDisc(), p + o * 0.895f + new Vector3(0f, 1.6f, 0f), new Vector3(0.27f, 1f, 0.27f), d.m.wax, face);
            d.Add("BoilerFlue", CylMesh(), p - o * 0.3f + new Vector3(0f, 3.3f, 0f), new Vector3(0.34f, 0.6f, 0.34f), d.m.brass);
            if (d.PropFx()) SfxSteam(d, p - o * 0.3f + new Vector3(0f, 4f, 0f), Vector3.up);
        }

        // ------------------------------------------------------------------ recipes: on the walls

        /// <summary>Ivy climbing the wall from its foot: overlapping flat clumps of leaves, wandering as they go up.</summary>
        private static void DrIvy(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            Quaternion flat = Quaternion.LookRotation(o);
            float top = Mathf.Min(d.wallH - 0.6f, d.R(2.6f, 5.4f));
            float x = 0f, y = 0.3f;
            Material leaf = d.Leaf();
            for (int guard = 0; guard < 10 && y < top; guard++)
            {
                float w = d.R(0.7f, 1.3f), h = d.R(0.6f, 1.1f);
                d.Soft("Ivy", BlobMesh(), p + side * x + new Vector3(0f, y, 0f) + o * 0.05f, new Vector3(w, h, 0.22f), d.Chance(0.7f) ? leaf : d.Leaf(), flat * Quaternion.Euler(0f, 0f, d.R(-25f, 25f)));
                if (d.m.blooms && d.Chance(0.25f))
                    d.Soft("IvyBloom", GemMesh(), p + side * (x + d.R(-0.3f, 0.3f)) + new Vector3(0f, y + d.R(-0.2f, 0.2f), 0f) + o * 0.16f, Vector3.one * 0.13f, d.Petal(), d.Yaw());
                y += h * d.R(0.45f, 0.75f);
                x = Mathf.Clamp(x + d.R(-0.45f, 0.45f), -0.7f, 0.7f);
            }
        }

        /// <summary>Greenery spilling over the top of a low wall and a tuft at its foot.</summary>
        private static void DrDrape(DressCtx d, Vector3 p, Vector3 o)
        {
            Quaternion flat = Quaternion.LookRotation(o);
            Vector3 side = d.Side(o);
            int n = 2 + d.Pick(2);
            for (int k = 0; k < n; k++)
                d.Soft("Drape", BlobMesh(), p + side * ((k - (n - 1) * 0.5f) * 0.55f) + new Vector3(0f, d.R(0.85f, 1.25f), 0f) + o * 0.05f, new Vector3(d.R(0.6f, 0.9f), d.R(0.6f, 1f), 0.2f),
                    d.Leaf(), flat * Quaternion.Euler(0f, 0f, d.R(-20f, 20f)));
            DrTuft(d, p + o * 0.25f, 0.55f, d.m.tuft);
        }

        /// <summary>A flower box on the sill at p (a point on the wall's face).</summary>
        private static void DrWindowBox(DressCtx d, Vector3 p, Vector3 o)
        {
            Quaternion flat = Quaternion.LookRotation(o);
            Vector3 side = Vector3.Cross(Vector3.up, o);
            d.Box("WindowBox", p + o * 0.16f, new Vector3(1.25f, 0.24f, 0.3f), d.m.wood, flat);
            Material petal = d.Petal();
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = p + o * 0.16f + side * ((k - 1.5f) * 0.3f) + new Vector3(0f, 0.16f, 0f);
                d.Soft("BoxLeaf", BlobMesh(), at, new Vector3(0.34f, 0.26f, 0.3f), d.Leaf(), d.Yaw());
                d.Soft("BoxBloom", GemMesh(), at + new Vector3(0f, 0.16f, 0f) + o * d.R(0f, 0.08f), Vector3.one * 0.13f, petal, d.Yaw());
            }
            if (d.Chance(0.4f))
                d.Soft("BoxTrail", BlobMesh(), p + o * 0.3f + side * d.R(-0.4f, 0.4f) - new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.5f, 0.16f), d.Leaf(), flat);
        }

        /// <summary>A basket of flowers hung from a bracket (the bracket 3.3 m up the wall).</summary>
        private static void DrHangingBasket(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 arm = p + new Vector3(0f, Mathf.Min(3.3f, d.wallH - 0.5f), 0f);
            d.Box("BasketArm", arm + o * 0.3f, new Vector3(0.05f, 0.05f, 0.6f), d.m.iron, Quaternion.LookRotation(o));
            Vector3 hang = arm + o * 0.55f;
            d.Box("BasketChain", hang - new Vector3(0f, 0.25f, 0f), new Vector3(0.025f, 0.5f, 0.025f), d.m.iron, Quaternion.identity);
            d.Add("BasketBowl", DressDome(), hang - new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), d.m.bark, Quaternion.Euler(180f, 0f, 0f));
            d.Soft("BasketLeaf", BlobMesh(), hang - new Vector3(0f, 0.42f, 0f), new Vector3(0.62f, 0.4f, 0.62f), d.Leaf(), d.Yaw());
            Material petal = d.Petal();
            for (int k = 0; k < 4; k++)
                d.Soft("BasketBloom", GemMesh(), hang + Quaternion.Euler(0f, k * 90f + d.R(0f, 40f), 0f) * new Vector3(0.22f, -0.3f, 0f), Vector3.one * 0.13f, petal, d.Yaw());
        }

        /// <summary>A round lantern on a bracket, 2.7 m up.</summary>
        private static void DrWallLantern(DressCtx d, Vector3 p, Vector3 o, Material globe)
        {
            Vector3 arm = p + new Vector3(0f, Mathf.Min(2.7f, d.wallH - 0.4f), 0f);
            d.Box("LanternArm", arm + o * 0.22f, new Vector3(0.05f, 0.05f, 0.44f), d.m.iron, Quaternion.LookRotation(o));
            d.Add("LanternGlobe", BlobMesh(), arm + o * 0.42f - new Vector3(0f, 0.2f, 0f), new Vector3(0.26f, 0.32f, 0.26f), globe);
            d.Add("LanternCap", ConeMesh(), arm + o * 0.42f, new Vector3(0.3f, 0.14f, 0.3f), d.m.iron);
        }

        /// <summary>A ring hung flat on the wall 1.7 m up: a life ring, a wreath, a spare wheel, a coil.</summary>
        private static void DrWallRing(DressCtx d, Vector3 p, Vector3 o, Material mat, float size, bool fat)
        {
            d.Add("WallRing", fat ? DressTorusFat() : DressTorusThin(), p + new Vector3(0f, 1.7f, 0f) + o * 0.12f, new Vector3(size, fat ? 0.8f : 1.2f, size), mat, Quaternion.FromToRotation(Vector3.up, o));
            d.Box("WallRingPeg", p + new Vector3(0f, 1.7f + size * 0.5f, 0f) + o * 0.08f, new Vector3(0.06f, 0.06f, 0.16f), d.m.iron, Quaternion.LookRotation(o));
        }

        /// <summary>A shelf of jars on the wall.</summary>
        private static void DrJarShelf(DressCtx d, Vector3 p, Vector3 o, Material[] mats)
        {
            Vector3 side = Vector3.Cross(Vector3.up, o);
            Vector3 shelf = p + new Vector3(0f, 1.5f, 0f) + o * 0.2f;
            d.Box("JarShelf", shelf, new Vector3(1.5f, 0.06f, 0.4f), d.m.wood, Quaternion.LookRotation(o));
            for (int k = 0; k < 4; k++)
            {
                float s = d.R(0.28f, 0.5f);
                d.Add("ShelfJar", DressUrn(), shelf + side * ((k - 1.5f) * 0.36f) + new Vector3(0f, 0.03f, 0f), new Vector3(s * 0.72f, s, s * 0.72f), mats[d.Pick(mats.Length)], d.Yaw());
            }
        }

        /// <summary>A string of round red peppers hung from a peg to dry.</summary>
        private static void DrRistra(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 peg = p + new Vector3(0f, Mathf.Min(2.5f, d.wallH - 0.4f), 0f) + o * 0.12f;
            int n = 6 + d.Pick(3);
            for (int k = 0; k < n; k++)
                d.Soft("Ristra", BlobMesh(), peg - new Vector3(0f, 0.1f + k * 0.11f, 0f) + Quaternion.Euler(0f, k * 137f, 0f) * new Vector3(0.05f, 0f, 0f), new Vector3(0.13f, 0.2f, 0.13f), d.m.petal[4],
                    Quaternion.Euler(d.R(-25f, 25f), d.R(0f, 360f), d.R(-25f, 25f)));
        }

        /// <summary>Crystals growing out of the rock face.</summary>
        private static void DrWallCrystals(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = Vector3.Cross(Vector3.up, o);
            Vector3 at = p + new Vector3(0f, d.R(0.8f, Mathf.Max(1f, Mathf.Min(3.4f, d.wallH - 0.6f))), 0f);
            int n = 3 + d.Pick(3);
            for (int k = 0; k < n; k++)
            {
                Vector3 dir = (o + side * d.R(-0.7f, 0.7f) + Vector3.up * d.R(-0.2f, 0.8f)).normalized;
                float h = d.R(0.35f, 0.9f);
                d.Soft("WallCrystal", GemMesh(), at + side * d.R(-0.3f, 0.3f) + new Vector3(0f, d.R(-0.3f, 0.3f), 0f) + dir * (h * 0.3f), new Vector3(h * 0.3f, h, h * 0.3f), d.m.crystal,
                    Quaternion.FromToRotation(Vector3.up, dir));
            }
            if (d.PropFx()) SfxGlints(d, at + o * 0.3f, new Vector3(0.7f, 0.7f, 0.7f), Color.Lerp(d.z.accentColor, Color.white, 0.4f));
        }

        /// <summary>A round brass pressure gauge on the wall, on a stub of pipe.</summary>
        private static void DrGauge(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 at = p + new Vector3(0f, d.R(1.3f, 1.9f), 0f);
            Quaternion face = Quaternion.FromToRotation(Vector3.up, o);
            d.Add("GaugePipe", CylMesh(), at + o * 0.08f - new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 0.5f, 0.14f), d.m.brass);
            d.Add("Gauge", CylMesh(), at + o * 0.1f, new Vector3(0.5f, 0.05f, 0.5f), d.m.brass, face);
            d.Soft("GaugeDial", DressDisc(), at + o * 0.155f, new Vector3(0.38f, 1f, 0.38f), d.m.wax, face);
            d.Box("GaugeNeedle", at + o * 0.165f, new Vector3(0.03f, 0.2f, 0.01f), d.m.paint[0], Quaternion.LookRotation(o) * Quaternion.Euler(0f, 0f, d.R(-120f, 120f)));
        }

        /// <summary>A run of pipe along the wall with round elbows down to the ground and a valve wheel.</summary>
        private static void DrPipeRun(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = d.Side(o);
            float y = d.R(1.9f, Mathf.Max(2f, Mathf.Min(2.6f, d.wallH - 0.5f)));
            Vector3 c = p + new Vector3(0f, y, 0f) + o * 0.16f;
            d.Add("PipeRun", CylMesh(), c, new Vector3(0.2f, 0.9f, 0.2f), d.m.brass, Quaternion.FromToRotation(Vector3.up, side));
            foreach (float s in new[] { -1f, 1f })
            {
                d.Add("PipeElbow", BlobMesh(), c + side * (s * 0.9f), Vector3.one * 0.27f, d.m.brass);
                if (s > 0f) d.Add("PipeDrop", CylMesh(), c + side * 0.9f - new Vector3(0f, y * 0.5f, 0f), new Vector3(0.2f, y * 0.5f, 0.2f), d.m.brass);
            }
            d.Add("PipeValve", DressTorusThin(), c + o * 0.16f - side * 0.3f, new Vector3(0.34f, 1.4f, 0.34f), d.m.paint[0], Quaternion.FromToRotation(Vector3.up, o));
        }

        /// <summary>Glowing bracket fungus: half-round shelves growing out of the wall.</summary>
        private static void DrBrackets(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = Vector3.Cross(Vector3.up, o);
            int n = 3 + d.Pick(3);
            float y = d.R(0.5f, 1.2f);
            for (int k = 0; k < n; k++)
            {
                float s = d.R(0.3f, 0.6f);
                d.Soft("Bracket", DressDome(), p + side * d.R(-0.35f, 0.35f) + new Vector3(0f, y, 0f) + o * 0.02f, new Vector3(s, s * 0.5f, s), d.m.glowCap, Quaternion.identity);
                y += d.R(0.25f, 0.5f);
            }
            if (d.PropFx()) SfxSpores(d, p + new Vector3(0f, y, 0f) + o * 0.3f, new Vector3(0.6f, 0.8f, 0.6f), new Color(0.4f, 1f, 0.85f));
        }

        // ------------------------------------------------------------------ recipes: strings across the street

        /// <summary>
        /// Beads (lanterns, blooms, bulbs, pennants) hung along a sagging cord from a to b, one about every 'every' metres.
        /// Flags hang flat in line with the cord; round beads hang 'drop' below it.
        /// </summary>
        private static void DrGarland(DressCtx d, Vector3 a, Vector3 b, float every, Mesh bead, Vector3 beadScale, Material[] mats, Material cord, float drop, bool flags)
        {
            float len = (b - a).magnitude;
            int beads = Mathf.Clamp(Mathf.RoundToInt(len / every), 2, 18);
            float sag = len * d.R(0.05f, 0.09f);
            Vector3 flat = b - a;
            flat.y = 0f;
            Quaternion hang = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(Vector3.Cross(Vector3.up, flat.normalized)) : Quaternion.identity;
            int shift = d.Pick(mats.Length);
            Vector3 prev = a;
            for (int k = 1; k <= beads + 1; k++)
            {
                float t = k / (float)(beads + 1);
                Vector3 at = Vector3.Lerp(a, b, t) - new Vector3(0f, Mathf.Sin(t * Mathf.PI) * sag, 0f);
                Vector3 seg = at - prev;
                if (cord != null && seg.sqrMagnitude > 0.0001f)
                    d.Box("Cord", (at + prev) * 0.5f, new Vector3(0.03f, 0.03f, seg.magnitude), cord, Quaternion.LookRotation(seg.normalized));
                if (k <= beads)
                {
                    Material mat = mats[(k + shift) % mats.Length];
                    if (flags) d.Box("Pennant", at - new Vector3(0f, beadScale.y * 0.5f + 0.02f, 0f), beadScale, mat, hang).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    else d.Soft("Bead", bead, at - new Vector3(0f, drop, 0f), beadScale, mat, Quaternion.identity);
                }
                prev = at;
            }
        }

        /// <summary>A net with round floats draped on the wall between two pegs.</summary>
        private static void DrNetFloats(DressCtx d, Vector3 p, Vector3 o)
        {
            Vector3 side = Vector3.Cross(Vector3.up, o);
            Vector3 c = p + new Vector3(0f, 2.2f, 0f) + o * 0.14f;
            DrGarland(d, c - side * 1.1f, c + side * 1.1f, 0.34f, BlobMesh(), new Vector3(0.2f, 0.2f, 0.2f), new[] { d.m.paint[0], d.m.hay, d.m.paint[1] }, d.m.rope, 0.1f, false);
        }

        // ------------------------------------------------------------------ the kits

        private static DressKit DressKitFor(DressCtx ctx)
        {
            ZoneTextureStyle st = ctx.st;
            var kit = new DressKit();
            // Pieces most streets share.
            DressProp weeds = Dp(3f, 0.4f, 0.25f, 0f, (d, p, o) => DrWeeds(d, p, o));
            DressProp barrel = Dp(2f, 0.45f, 0.45f, 1f, (d, p, o) => DrBarrel(d, p, d.R(0.85f, 1.1f), d.m.wood, d.m.iron, d.Pick(3) == 0 ? 1 : 0));
            DressProp barrels = Dp(1f, 0.95f, 0.5f, 1.2f, (d, p, o) => DrBarrelStack(d, p, o));
            DressProp sacks = Dp(1f, 0.7f, 0.42f, 0.6f, (d, p, o) => DrSacks(d, p, o, 2 + d.Pick(2)));
            DressProp bush = Dp(2f, 0.7f, 0.6f, 0f, (d, p, o) => DrBush(d, p, d.R(0.8f, 1.3f), d.m.blooms && d.Chance(0.4f)));
            DressProp rock = Dp(1.5f, 0.55f, 0.55f, 0.5f, (d, p, o) => DrBoulder(d, p, d.R(0.5f, 0.9f), d.m.mossy));
            DressProp woodpile = Dp(1f, 0.8f, 0.5f, 0.8f, (d, p, o) => DrWoodpile(d, p, o));
            DressProp lamp = Dp(0.6f, 0.22f, 0.22f, 0f, (d, p, o) => DrGlobeLamp(d, p, d.R(0.9f, 1.3f), d.m.iron, d.m.glow));
            DressProp cart = Dp(2f, 1.5f, 0.7f, 1f, (d, p, o) => DrCart(d, p, o));
            DressProp well = Dp(1.2f, 1.2f, 1.2f, 1f, (d, p, o) => DrWell(d, p, o));
            DressProp hay = Dp(1f, 1.2f, 0.65f, 0.9f, (d, p, o) => DrHay(d, p, o));
            DressProp ivy = Df(3f, 1f, 0f, 5.6f, (d, p, o) => DrIvy(d, p, o));
            DressProp wallLamp = Df(1.5f, 0.3f, 2.2f, 3f, (d, p, o) => DrWallLantern(d, p, o, d.m.glow));

            switch (st)
            {
                case ZoneTextureStyle.Garden:
                    kit.wall = new[]
                    {
                        weeds, bush, barrel, lamp,
                        Dp(4f, 0.45f, 0.45f, 0.8f, (d, p, o) => DrPlanter(d, p, d.R(0.8f, 1.2f), d.Pick(3))),
                        Dp(2.5f, 0.6f, 0.5f, 0f, (d, p, o) => DrBush(d, p, d.R(0.9f, 1.4f), true)),
                        Dp(1f, 0.9f, 0.4f, 0.7f, (d, p, o) => DrJars(d, p, o, 3, d.m.pots)),
                        Dp(0.8f, 0.42f, 0.42f, 0.9f, (d, p, o) => DrSkep(d, p)),
                        Dp(0.6f, 0.6f, 0.4f, 0f, (d, p, o) => DrPumpkins(d, p, 2 + d.Pick(2))),
                        Dp(1f, 0.7f, 0.5f, 0f, (d, p, o) => DrSunflowers(d, p)),
                    };
                    kit.nook = new[] { cart, well, hay, Dp(1.2f, 0.6f, 0.6f, 1f, (d, p, o) => DrBirdbath(d, p)), Dp(1f, 0.5f, 0.5f, 1.2f, (d, p, o) => DrOrb(d, p, d.m.stone)) };
                    kit.face = new[] { ivy, wallLamp, Df(3f, 0.45f, 2.3f, 3.6f, (d, p, o) => DrHangingBasket(d, p, o)) };
                    kit.lawn = new[]
                    {
                        Dp(4f, 0.7f, 0.7f, 0f, (d, p, o) => DrBush(d, p, d.R(0.9f, 1.5f), true)), rock,
                        Dp(1.2f, 0.7f, 0.7f, 0f, (d, p, o) => DrSunflowers(d, p)),
                        Dp(0.6f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.26f, d.m.capRed)),
                        Dp(0.5f, 1.1f, 1.1f, 0f, (d, p, o) => DrFairyRing(d, p)),
                        Dp(0.4f, 0.6f, 0.6f, 1f, (d, p, o) => DrBirdbath(d, p)),
                    };
                    kit.top = new[] { Dp(6f, 1.1f, 1.1f, 0f, (d, p, o) => DrHedgeMound(d, p)), Dp(1f, 0.5f, 0.5f, 0f, (d, p, o) => DrBush(d, p, d.R(0.9f, 1.3f), true)) };
                    kit.wallRate = 1.3f; kit.faceRate = 0.6f; kit.lawnRate = 0.8f; kit.topRate = 3.2f;
                    kit.tufts = 9f; kit.flowers = 2.5f; kit.windowBoxes = 0.8f; kit.borders = true; kit.drapes = true;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 0.7f, BlobMesh(), new Vector3(0.22f, 0.22f, 0.22f), d.m.petal, d.m.tuft, 0.08f, false);
                    break;

                case ZoneTextureStyle.Docks:
                    kit.wall = new[]
                    {
                        weeds, barrel, sacks, lamp,
                        Dp(3f, 0.95f, 0.5f, 1.2f, (d, p, o) => DrBarrelStack(d, p, o)),
                        Dp(2.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrRopeCoil(d, p, d.R(0.8f, 1.1f))),
                        Dp(2f, 0.6f, 0.4f, 0f, (d, p, o) => DrFloats(d, p, 3 + d.Pick(3))),
                        Dp(1.5f, 0.42f, 0.42f, 0.5f, (d, p, o) => DrBasket(d, p, d.R(0.9f, 1.2f), 0)),
                        Dp(1f, 0.9f, 0.4f, 0.7f, (d, p, o) => DrJars(d, p, o, 2 + d.Pick(2), d.m.pots)),
                    };
                    kit.nook = new[] { cart, barrels, Dp(1.5f, 1.7f, 0.75f, 0.8f, (d, p, o) => DrBoat(d, p, o)), Dp(1f, 0.8f, 0.8f, 0f, (d, p, o) => { DrRopeCoil(d, p, 1.4f); DrFloats(d, p + new Vector3(0f, 0.4f, 0f), 4); }) };
                    kit.face = new[]
                    {
                        wallLamp, Df(2f, 0.5f, 1.2f, 2.3f, (d, p, o) => DrWallRing(d, p, o, d.m.paint[d.Pick(2)], 0.8f, true)),
                        Df(2f, 1.2f, 1.7f, 2.4f, (d, p, o) => DrNetFloats(d, p, o)), Df(0.6f, 1f, 0f, 4f, (d, p, o) => DrIvy(d, p, o)),
                    };
                    kit.lawn = new[] { rock, bush, Dp(1f, 0.9f, 0.9f, 0f, (d, p, o) => d.Add("Driftwood", CylMesh(), p + new Vector3(0f, 0.14f, 0f), new Vector3(0.26f, d.R(0.7f, 1.2f), 0.26f), d.m.bone, Quaternion.Euler(0f, d.R(0f, 360f), 90f))) };
                    kit.top = new[] { barrel, Dp(1.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrRopeCoil(d, p, 1f)), Dp(1f, 0.6f, 0.6f, 0f, (d, p, o) => DrFloats(d, p, 4)), bush };
                    kit.tufts = 5f; kit.flowers = 0.4f; kit.windowBoxes = 0.15f; kit.drapes = true; kit.lawnRate = 0.4f;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 0.9f, null, new Vector3(0.32f, 0.36f, 0.02f), d.m.paint, d.m.rope, 0f, true);
                    kit.hangChance = 0.22f;
                    break;

                case ZoneTextureStyle.Forge:
                    kit.wall = new[]
                    {
                        barrel, sacks, woodpile,
                        Dp(1f, 0.4f, 0.25f, 0f, (d, p, o) => DrWeeds(d, p, o)),
                        Dp(4f, 0.9f, 0.4f, 0.7f, (d, p, o) => DrJars(d, p, o, 2 + d.Pick(3), d.m.glazes)),
                        Dp(2f, 0.75f, 0.6f, 0.5f, (d, p, o) => DrCoalHeap(d, p, d.R(0.8f, 1.1f))),
                        Dp(1.5f, 0.55f, 0.55f, 0.5f, (d, p, o) => DrBoulder(d, p, d.R(0.5f, 0.9f), false)),
                        Dp(0.9f, 0.42f, 0.42f, 1f, (d, p, o) => DrBrazier(d, p, d.m.ember, new Color(1f, 0.55f, 0.2f))),
                        Dp(1.2f, 0.42f, 0.42f, 0.5f, (d, p, o) => DrBasket(d, p, d.R(0.9f, 1.2f), 2)),
                    };
                    kit.nook = new[] { cart, woodpile, Dp(2.5f, 1.25f, 1.25f, 1.6f, (d, p, o) => DrOven(d, p, o, d.m.clay, 1f)) };
                    kit.face = new[] { Df(1.5f, 0.3f, 2.2f, 3f, (d, p, o) => DrWallLantern(d, p, o, d.m.ember)), Df(2f, 0.8f, 1.3f, 2.1f, (d, p, o) => DrJarShelf(d, p, o, d.m.glazes)), Df(0.5f, 1f, 0f, 4f, (d, p, o) => DrIvy(d, p, o)) };
                    kit.lawn = new[] { rock, Dp(2f, 0.8f, 0.8f, 0f, (d, p, o) => DrCoalHeap(d, p, d.R(0.8f, 1.3f))), Dp(1f, 0.6f, 0.6f, 0f, (d, p, o) => DrSage(d, p, 1f)) };
                    kit.top = new[] { Dp(2f, 0.6f, 0.6f, 0f, (d, p, o) => DrJars(d, p, o, 2, d.m.glazes)), Dp(1f, 0.8f, 0.8f, 0f, (d, p, o) => DrCoalHeap(d, p, 1f)), barrel };
                    kit.tufts = 4f; kit.flowers = 0.2f; kit.windowBoxes = 0.1f; kit.lawnRate = 0.5f;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 2.2f, BlobMesh(), new Vector3(0.28f, 0.34f, 0.28f), new[] { d.m.ember, d.m.lanterns[2] }, d.m.iron, 0.2f, false);
                    kit.hangChance = 0.2f;
                    break;

                case ZoneTextureStyle.Forest:
                    kit.wall = new[]
                    {
                        weeds, barrel, bush, rock, lamp,
                        Dp(3f, 0.8f, 0.5f, 0.8f, (d, p, o) => DrWoodpile(d, p, o)),
                        Dp(2f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.28f, d.Chance(0.5f) ? d.m.capRed : d.m.capTan)),
                        Dp(1f, 0.45f, 0.45f, 0.8f, (d, p, o) => DrPlanter(d, p, 1f, d.Pick(2))),
                    };
                    kit.nook = new[] { cart, well, woodpile };
                    kit.face = new[] { Df(5f, 1f, 0f, 5.6f, (d, p, o) => DrIvy(d, p, o)), wallLamp };
                    kit.lawn = new[] { rock, Dp(0.6f, 1.1f, 1.1f, 0f, (d, p, o) => DrFairyRing(d, p)) };
                    kit.top = new[] { Dp(5f, 1.1f, 1.1f, 0f, (d, p, o) => DrHedgeMound(d, p)), rock };
                    kit.faceRate = 0.7f; kit.lawnRate = 0.25f; kit.topRate = 2.6f; kit.tufts = 3f; kit.flowers = 0f; kit.windowBoxes = 0.3f; kit.drapes = true;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 2f, BlobMesh(), new Vector3(0.26f, 0.32f, 0.26f), new[] { d.m.glow }, d.m.rope, 0.2f, false);
                    break;

                case ZoneTextureStyle.Keep:
                    kit.wall = new[]
                    {
                        barrel, rock,
                        Dp(2f, 0.4f, 0.25f, 0f, (d, p, o) => DrWeeds(d, p, o)),
                        Dp(3f, 0.9f, 0.7f, 0.5f, (d, p, o) => DrRubble(d, p, 1f)),
                        Dp(2f, 0.9f, 0.4f, 0.7f, (d, p, o) => DrJars(d, p, o, 2 + d.Pick(2), new[] { d.m.stone, d.m.rock })),
                        Dp(1.2f, 0.42f, 0.42f, 1f, (d, p, o) => DrBrazier(d, p, d.m.crystal, d.z.accentColor)),
                        Dp(1.5f, 0.35f, 0.3f, 0f, (d, p, o) => DrCandles(d, p, d.m.crystal)),
                        Dp(1.2f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.28f, d.m.glowCap)),
                        Dp(1f, 0.42f, 0.42f, 1.2f, (d, p, o) => DrOrb(d, p, d.m.rock)),
                        Dp(1f, 0.22f, 0.22f, 0f, (d, p, o) => DrGlobeLamp(d, p, d.R(0.9f, 1.3f), d.m.iron, d.m.crystal)),
                    };
                    kit.nook = new[] { Dp(2f, 1.2f, 1f, 0.7f, (d, p, o) => { DrRubble(d, p, 1.5f); DrRubble(d, p + d.Side(o) * 0.6f, 1f); }), well, Dp(1f, 0.5f, 0.5f, 1.2f, (d, p, o) => DrOrb(d, p, d.m.crystal)) };
                    kit.face = new[] { Df(3f, 1f, 0f, 5f, (d, p, o) => DrIvy(d, p, o)), Df(2f, 0.3f, 2.2f, 3f, (d, p, o) => DrWallLantern(d, p, o, d.m.crystal)) };
                    kit.lawn = new[]
                    {
                        rock, Dp(2f, 0.9f, 0.9f, 0f, (d, p, o) => DrRubble(d, p, 1f)), Dp(1.5f, 0.4f, 0.4f, 0.8f, (d, p, o) => DrHeadstone(d, p, o)),
                        Dp(1f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.28f, d.m.glowCap)), Dp(1.5f, 0.6f, 0.6f, 0f, (d, p, o) => DrSage(d, p, 1f)),
                    };
                    kit.top = new[] { Dp(2f, 0.9f, 0.9f, 0f, (d, p, o) => DrRubble(d, p, 1f)), Dp(1f, 0.5f, 0.5f, 0f, (d, p, o) => DrOrb(d, p, d.m.rock)), rock };
                    kit.tufts = 4f; kit.flowers = 0f; kit.drapes = true; kit.lawnRate = 0.6f; kit.topRate = 1.6f;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 1.1f, null, new Vector3(0.34f, 0.5f, 0.02f), new[] { d.m.crystal, d.m.coal, d.m.rock }, d.m.iron, 0f, true);
                    kit.hangChance = 0.2f;
                    break;

                case ZoneTextureStyle.Desert:
                case ZoneTextureStyle.Boomtown:
                    bool town = st == ZoneTextureStyle.Boomtown;
                    kit.wall = new[]
                    {
                        barrel, sacks,
                        Dp(2f, 0.4f, 0.25f, 0f, (d, p, o) => DrWeeds(d, p, o)),
                        Dp(town ? 1.5f : 3f, 0.9f, 0.4f, 0.7f, (d, p, o) => DrJars(d, p, o, 2 + d.Pick(2), new[] { d.m.clay, d.m.glazes[2] })),
                        Dp(2f, 0.5f, 0.5f, 0.6f, (d, p, o) => DrBarrelCactus(d, p, d.R(0.6f, 0.95f))),
                        Dp(town ? 0.8f : 2f, 0.6f, 0.5f, 0.6f, (d, p, o) => DrPricklyPear(d, p, d.R(0.8f, 1.1f))),
                        Dp(1.5f, 0.7f, 0.7f, 0f, (d, p, o) => DrAgave(d, p, d.R(0.7f, 1f))),
                        Dp(1.5f, 0.6f, 0.5f, 0f, (d, p, o) => DrSage(d, p, d.R(0.8f, 1.2f))),
                        Dp(1.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrTumbleweed(d, p, d.R(0.6f, 1f))),
                        Dp(1.5f, 0.6f, 0.2f, 0f, (d, p, o) => DrWagonWheel(d, p, o)),
                        Dp(1.2f, 0.55f, 0.55f, 0.5f, (d, p, o) => DrBoulder(d, p, d.R(0.5f, 0.9f), false)),
                        Dp(1f, 0.42f, 0.42f, 0.5f, (d, p, o) => DrBasket(d, p, d.R(0.9f, 1.2f), 4)),
                        Dp(town ? 1.2f : 0.3f, 0.45f, 0.45f, 0.8f, (d, p, o) => DrPlanter(d, p, 1f, 3 + d.Pick(2))),
                    };
                    kit.nook = new[] { cart, hay, well, barrels, Dp(town ? 0.6f : 2.5f, 1.25f, 1.25f, 1.6f, (d, p, o) => DrOven(d, p, o, d.m.clay, 0.9f)) };
                    kit.face = new[]
                    {
                        wallLamp, Df(3f, 0.25f, 1.5f, 2.7f, (d, p, o) => DrRistra(d, p, o)),
                        Df(1.5f, 0.6f, 1.1f, 2.4f, (d, p, o) => DrWallRing(d, p, o, d.m.wood, 1f, false)),
                    };
                    kit.lawn = new[]
                    {
                        Dp(3f, 0.6f, 0.6f, 0f, (d, p, o) => DrSage(d, p, d.R(0.8f, 1.3f))), Dp(2f, 0.5f, 0.5f, 0.6f, (d, p, o) => DrBarrelCactus(d, p, d.R(0.6f, 1f))),
                        Dp(2f, 0.6f, 0.6f, 0.6f, (d, p, o) => DrPricklyPear(d, p, d.R(0.8f, 1.2f))), Dp(1.5f, 0.7f, 0.7f, 0f, (d, p, o) => DrAgave(d, p, d.R(0.8f, 1.2f))),
                        Dp(2f, 0.55f, 0.55f, 0.5f, (d, p, o) => DrBoulder(d, p, d.R(0.5f, 1.1f), false)), Dp(1.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrTumbleweed(d, p, d.R(0.6f, 1.1f))),
                    };
                    kit.top = new[] { Dp(2f, 0.55f, 0.55f, 0f, (d, p, o) => DrBoulder(d, p, d.R(0.5f, 1f), false)), Dp(2f, 0.5f, 0.5f, 0f, (d, p, o) => DrBarrelCactus(d, p, d.R(0.6f, 1f))), barrel, sacks };
                    kit.tufts = 5f; kit.flowers = 0.3f; kit.lawnRate = 0.7f; kit.faceRate = 0.4f; kit.topRate = 1.8f;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 0.8f, null, new Vector3(0.3f, 0.34f, 0.02f), d.m.paint, d.m.rope, 0f, true);
                    kit.hangChance = town ? 0.3f : 0.18f;
                    break;

                case ZoneTextureStyle.Mine:
                    kit.wall = new[]
                    {
                        rock, barrel, woodpile,
                        Dp(3f, 0.75f, 0.6f, 0.5f, (d, p, o) => DrOrePile(d, p, d.R(0.8f, 1.1f))),
                        Dp(2.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrCrystals(d, p, d.R(0.6f, 1f))),
                        Dp(2f, 0.5f, 0.5f, 0.6f, (d, p, o) => DrStalagmites(d, p, d.R(0.7f, 1.1f))),
                        Dp(1.5f, 0.45f, 0.45f, 1f, (d, p, o) => DrBarrel(d, p, 0.9f, d.m.wood, d.m.paint[0], 0)),
                        Dp(1.5f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.28f, d.m.glowCap)),
                        Dp(1f, 0.42f, 0.42f, 0.5f, (d, p, o) => DrBasket(d, p, d.R(0.9f, 1.2f), 3)),
                        Dp(1f, 0.22f, 0.22f, 0f, (d, p, o) => DrGlobeLamp(d, p, d.R(0.9f, 1.3f), d.m.wood, d.m.glow)),
                        Dp(0.8f, 0.5f, 0.5f, 0f, (d, p, o) => DrRopeCoil(d, p, 0.9f)),
                    };
                    kit.nook = new[] { Dp(2f, 0.85f, 0.65f, 1f, (d, p, o) => DrOreCart(d, p, o)), woodpile, Dp(1.5f, 0.9f, 0.9f, 0.6f, (d, p, o) => { DrCrystals(d, p, 1.6f); DrOrePile(d, p + d.Side(o) * 0.7f, 0.8f); }) };
                    kit.face = new[] { Df(4f, 0.6f, 0.4f, 3.8f, (d, p, o) => DrWallCrystals(d, p, o)), Df(2f, 0.3f, 2.2f, 3f, (d, p, o) => DrWallLantern(d, p, o, d.m.glow)) };
                    kit.lawn = new[]
                    {
                        rock, Dp(2f, 0.5f, 0.5f, 0.6f, (d, p, o) => DrStalagmites(d, p, d.R(0.7f, 1.3f))), Dp(1.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrCrystals(d, p, d.R(0.6f, 1.1f))),
                        Dp(1.5f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.3f, d.m.glowCap)), Dp(1f, 0.7f, 0.7f, 0f, (d, p, o) => DrPuddle(d, p, d.R(0.9f, 1.6f), d.m.pool, false)),
                    };
                    kit.top = new[] { rock, Dp(1.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrStalagmites(d, p, 1f)), Dp(1.5f, 0.5f, 0.5f, 0f, (d, p, o) => DrCrystals(d, p, 0.9f)), Dp(1f, 0.75f, 0.75f, 0f, (d, p, o) => DrOrePile(d, p, 1f)) };
                    kit.tufts = 2f; kit.flowers = 0f; kit.lawnRate = 0.7f; kit.faceRate = 0.55f; kit.topRate = 1.8f;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 2.4f, BlobMesh(), new Vector3(0.26f, 0.32f, 0.26f), new[] { d.m.glow }, d.m.rope, 0.2f, false);
                    kit.hangChance = 0.25f;
                    break;

                case ZoneTextureStyle.Undercity:
                    kit.wall = new[]
                    {
                        barrel, sacks,
                        Dp(0.6f, 0.4f, 0.25f, 0f, (d, p, o) => DrWeeds(d, p, o)),
                        Dp(3f, 0.4f, 0.4f, 1f, (d, p, o) => DrDrum(d, p, d.R(0.9f, 1.1f), d.Chance(0.4f) ? d.m.rust : d.Paint(), d.Chance(0.25f))),
                        Dp(1.2f, 1.25f, 0.55f, 1.2f, (d, p, o) => DrTank(d, p, o)),
                        Dp(1.5f, 0.35f, 0.35f, 1f, (d, p, o) => DrVent(d, p, o)),
                        Dp(1.5f, 0.75f, 0.45f, 0.8f, (d, p, o) => DrGears(d, p, d.R(0.8f, 1.1f))),
                        Dp(1.2f, 0.65f, 0.6f, 0.9f, (d, p, o) => DrSpool(d, p, o)),
                        Dp(2f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.3f, d.m.glowCap)),
                        Dp(1.5f, 0.8f, 0.65f, 0.5f, (d, p, o) => DrScrap(d, p, d.R(0.8f, 1.1f))),
                        Dp(1f, 0.22f, 0.22f, 0f, (d, p, o) => DrGlobeLamp(d, p, d.R(0.9f, 1.4f), d.m.brass, NeonMat(d.Pick(6)))),
                        Dp(1f, 0.45f, 0.4f, 0f, (d, p, o) => DrPuddle(d, p, d.R(0.7f, 0.9f), d.m.sludge, true)),
                    };
                    kit.nook = new[]
                    {
                        Dp(2f, 0.95f, 0.95f, 1.6f, (d, p, o) => DrBoiler(d, p, o)), Dp(1f, 0.9f, 0.7f, 0.8f, (d, p, o) => DrGears(d, p, 1.5f)),
                        Dp(1f, 0.85f, 0.65f, 1f, (d, p, o) => DrOreCart(d, p, o)), Dp(1f, 0.9f, 0.8f, 0.6f, (d, p, o) => DrScrap(d, p, 1.4f)),
                    };
                    kit.face = new[]
                    {
                        Df(2f, 0.3f, 0.7f, 2f, (d, p, o) => DrGauge(d, p, o)), Df(2f, 1.1f, 0f, 2.8f, (d, p, o) => DrPipeRun(d, p, o)),
                        Df(2f, 0.5f, 0.4f, 2.6f, (d, p, o) => DrBrackets(d, p, o)),
                    };
                    kit.lawn = new[]
                    {
                        Dp(3f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(4), 0.34f, d.m.glowCap)), Dp(2f, 0.8f, 0.8f, 0f, (d, p, o) => DrScrap(d, p, d.R(0.8f, 1.2f))),
                        Dp(1.5f, 0.4f, 0.4f, 0.9f, (d, p, o) => DrDrum(d, p, 1f, d.m.rust, d.Chance(0.4f))), Dp(2f, 0.8f, 0.8f, 0f, (d, p, o) => DrPuddle(d, p, d.R(1f, 1.8f), d.m.sludge, true)),
                        Dp(1f, 0.5f, 0.5f, 0.6f, (d, p, o) => DrStalagmites(d, p, d.R(0.6f, 1f))), Dp(1f, 0.75f, 0.75f, 0f, (d, p, o) => DrGears(d, p, 1f)),
                    };
                    kit.top = new[]
                    {
                        Dp(2f, 0.4f, 0.4f, 0f, (d, p, o) => DrDrum(d, p, 1f, d.Paint(), d.Chance(0.3f))), Dp(1f, 1.25f, 0.6f, 0f, (d, p, o) => DrTank(d, p, o)),
                        Dp(1f, 0.75f, 0.75f, 0f, (d, p, o) => DrGears(d, p, 1f)), Dp(1.5f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 4, 0.34f, d.m.glowCap)),
                        Dp(1f, 0.65f, 0.65f, 0f, (d, p, o) => DrSpool(d, p, o)),
                    };
                    kit.tufts = 2f; kit.flowers = 0f; kit.lawnRate = 0.8f; kit.faceRate = 0.6f; kit.topRate = 1.8f;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 0.9f, BlobMesh(), new Vector3(0.2f, 0.24f, 0.2f), new[] { NeonMat(0), NeonMat(1), NeonMat(2), NeonMat(3) }, d.m.coal, 0.14f, false);
                    kit.hangChance = 0.4f;
                    break;

                default:    // Town: Lantern Row and anything new
                    kit.wall = new[]
                    {
                        weeds, barrel, barrels, sacks, bush, woodpile, lamp,
                        Dp(3f, 0.45f, 0.45f, 0.8f, (d, p, o) => DrPlanter(d, p, d.R(0.8f, 1.15f), d.Pick(3))),
                        Dp(0.8f, 0.9f, 0.4f, 0.7f, (d, p, o) => DrJars(d, p, o, 2 + d.Pick(2), d.m.pots)),
                        Dp(0.7f, 0.6f, 0.4f, 0f, (d, p, o) => DrPumpkins(d, p, 2 + d.Pick(2))),
                        Dp(0.7f, 0.42f, 0.42f, 0.5f, (d, p, o) => DrBasket(d, p, d.R(0.9f, 1.2f), 1)),
                    };
                    kit.nook = new[] { cart, well, hay, barrels, woodpile };
                    kit.face = new[] { ivy, wallLamp, Df(2f, 0.45f, 2.3f, 3.6f, (d, p, o) => DrHangingBasket(d, p, o)) };
                    kit.lawn = new[]
                    {
                        Dp(3f, 0.7f, 0.7f, 0f, (d, p, o) => DrBush(d, p, d.R(0.8f, 1.4f), d.Chance(0.4f))), rock,
                        Dp(1f, 0.4f, 0.4f, 0f, (d, p, o) => DrMushrooms(d, p, 3 + d.Pick(3), 0.26f, d.Chance(0.5f) ? d.m.capRed : d.m.capTan)),
                        Dp(0.3f, 1.1f, 1.1f, 0f, (d, p, o) => DrFairyRing(d, p)),
                    };
                    kit.top = new[]
                    {
                        Dp(3f, 0.6f, 0.6f, 0f, (d, p, o) => DrBush(d, p, d.R(0.9f, 1.4f), true)), Dp(2f, 0.4f, 0.4f, 0f, (d, p, o) => DrFlowers(d, p, 5, 0.4f, 0.45f)),
                        Dp(1.5f, 0.45f, 0.45f, 0f, (d, p, o) => DrPlanter(d, p, 1f, d.Pick(3))), Dp(1f, 0.5f, 0.5f, 0f, (d, p, o) => DrOrb(d, p, d.m.stone)),
                    };
                    kit.windowBoxes = 0.5f; kit.drapes = true; kit.borders = true;
                    kit.hang = (d, a, b) => DrGarland(d, a, b, 1.15f, BlobMesh(), new Vector3(0.3f, 0.38f, 0.3f), d.m.lanterns, d.m.rope, 0.22f, false);
                    kit.hangChance = 0.45f;
                    break;
            }
            return kit;
        }
    }
}
