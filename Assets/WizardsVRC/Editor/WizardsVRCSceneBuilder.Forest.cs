using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Forest streets (Thornhollow Grove): every tree cell grows a small stand of trees instead of one (broad oaks with
    /// layered crowns, tall pines, birch-pale saplings, in several greens), with ferns and bushes at their feet; the lawns
    /// fill with undergrowth (ferns, bushes, flowers, toadstools, mossy logs and stumps); and fireflies drift in the shade.
    /// All of it is merged into a few meshes per material, so it stays cheap to draw.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static void ForestGrowth(Transform layout, ZoneGrid g, ZoneDefinition z, Material trunk, HashSet<Vector2Int> stairCells)
        {
            var rng = new System.Random(z.zoneKey.GetHashCode() & 0x7fffffff);
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float cs = g.cell;
            var bark = PsxTexMat("Bark", new Color(0.5f, 0.3f, 0.16f), TileTexture(StreetTexDir + "/Bark.png"), 1.4f);
            var birch = PsxTexMat("BirchBark", new Color(0.86f, 0.84f, 0.78f), TileTexture(StreetTexDir + "/Bark.png"), 1.4f);
            Texture2D leafTex = TileTexture(StreetTexDir + "/Leaves.png");
            Color baseLeaf = z.treeColor;
            var leaves = new[]
            {
                PsxTexMat("ForestLeaf0", Color.Lerp(baseLeaf, new Color(0.1f, 0.3f, 0.12f), 0.3f), leafTex, 2.2f),
                PsxTexMat("ForestLeaf1", Color.Lerp(baseLeaf, new Color(0.3f, 0.6f, 0.18f), 0.45f), leafTex, 2.2f),
                PsxTexMat("ForestLeaf2", Color.Lerp(baseLeaf, new Color(0.18f, 0.5f, 0.3f), 0.4f), leafTex, 2.2f),
                PsxTexMat("ForestLeaf3", Color.Lerp(baseLeaf, new Color(0.45f, 0.62f, 0.2f), 0.35f), leafTex, 2.2f),
            };
            var pine = PsxTexMat("ForestPine", Color.Lerp(baseLeaf, new Color(0.06f, 0.26f, 0.18f), 0.5f), leafTex, 1.8f);
            var fern = PsxTexMat("ForestFern", Color.Lerp(z.grassColor, new Color(0.25f, 0.6f, 0.2f), 0.5f), leafTex, 1.2f);
            var moss = PsxTexMat("ForestMoss", new Color(0.3f, 0.45f, 0.2f), TileTexture(TerrainTexDir + "/ForestFloor.png"), 1.5f);
            var capRed = PMat("MushroomCapRed", new Color(0.95f, 0.2f, 0.25f), 1f);
            var capTan = PMat("ForestCapTan", new Color(0.78f, 0.6f, 0.42f), 1f);
            var stalk = PMat("MushroomStalk", new Color(0.95f, 0.9f, 0.78f), 1f);
            var petals = new[]
            {
                PMat("ForestPetalWhite", new Color(0.95f, 0.95f, 0.88f), 1f), PMat("ForestPetalBlue", new Color(0.45f, 0.6f, 1f), 1f),
                PMat("ForestPetalYellow", new Color(1f, 0.85f, 0.3f), 1f), PMat("ForestPetalPink", new Color(1f, 0.55f, 0.75f), 1f),
            };

            var holder = Child(layout, "Forest", Vector3.zero);
            // ---- tree cells: a stand of two or three trees each, the first with a trunk you bump into
            foreach (var cell in g.Find('^'))
            {
                Vector3 p = g.Local(cell);
                bool deep = true;   // tree cells with tree cells all round are deep wood: taller
                foreach (var d in ZoneGrid.Dirs) if (g.At(cell.x + d.x, cell.y + d.y) != '^') deep = false;
                int n = deep ? 2 : 3;
                for (int k = 0; k < n; k++)
                {
                    Vector3 at = p + new Vector3(Rr(-0.32f, 0.32f) * cs, 0, Rr(-0.32f, 0.32f) * cs);
                    float s = Rr(0.9f, 1.35f) * (deep ? 1.25f : 1f);
                    double roll = rng.NextDouble();
                    if (k == 0) Deco(holder, PrimitiveType.Cylinder, "TrunkCollider", at + new Vector3(0, 2f, 0), new Vector3(0.9f, 2f, 0.9f), bark, Quaternion.identity, true);
                    if (roll < 0.45) ForestOak(holder, at, s, bark, leaves[rng.Next(leaves.Length)], rng);
                    else if (roll < 0.8) Conifer(holder, at, s * 1.15f, pine, bark);
                    else ForestBirch(holder, at, s, birch, leaves[1 + rng.Next(3)], rng);
                }
                // Something low at the edge of the wood.
                if (!deep)
                    for (int k = 0; k < 2; k++)
                        Shape(holder, "WoodBush", BlobMesh(), p + new Vector3(Rr(-0.4f, 0.4f) * cs, 0.5f, Rr(-0.4f, 0.4f) * cs), new Vector3(Rr(1.6f, 2.6f), Rr(1f, 1.6f), Rr(1.6f, 2.6f)),
                            leaves[rng.Next(leaves.Length)], false, Quaternion.Euler(0, Rr(0, 360f), 0));
            }

            // ---- lawns: undergrowth (nothing you bump into, and not on or by the duel rings, the entrance or stairs)
            var keepClear = new HashSet<Vector2Int>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    char c = g.At(x, y);
                    if (!ZoneGrid.IsRingDigit(c) && c != 'S' && !g.IsCustom(c)) continue;
                    for (int yy = y - 1; yy <= y + 1; yy++)
                        for (int xx = x - 1; xx <= x + 1; xx++) keepClear.Add(new Vector2Int(xx, yy));
                }
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (g.At(x, y) != ',' || keepClear.Contains(cell) || stairCells.Contains(cell)) continue;
                    Vector3 p = g.Local(cell);
                    int n = rng.Next(2, 5);
                    for (int k = 0; k < n; k++)
                    {
                        Vector3 at = p + new Vector3(Rr(-0.45f, 0.45f) * cs, 0, Rr(-0.45f, 0.45f) * cs);
                        double roll = rng.NextDouble();
                        if (roll < 0.35) ForestFern(holder, at, Rr(0.7f, 1.3f), fern, rng);
                        else if (roll < 0.55) Shape(holder, "Bush", BlobMesh(), at + new Vector3(0, 0.4f, 0), new Vector3(Rr(1f, 1.8f), Rr(0.7f, 1.1f), Rr(1f, 1.8f)), leaves[rng.Next(leaves.Length)], false, Quaternion.Euler(0, Rr(0, 360f), 0));
                        else if (roll < 0.8)
                            for (int f = 0; f < 4; f++)
                            {
                                Vector3 fp = at + new Vector3(Rr(-0.6f, 0.6f), 0, Rr(-0.6f, 0.6f));
                                float h = Rr(0.25f, 0.5f);
                                Deco(holder, PrimitiveType.Cube, "FlowerStem", fp + new Vector3(0, h / 2f, 0), new Vector3(0.05f, h, 0.05f), fern, Quaternion.identity, false);
                                Shape(holder, "FlowerHead", BlobMesh(), fp + new Vector3(0, h, 0), Vector3.one * Rr(0.14f, 0.22f), petals[rng.Next(petals.Length)], false, Quaternion.identity);
                            }
                        else if (roll < 0.9)
                            for (int f = 0; f < 3; f++)
                            {
                                Vector3 fp = at + new Vector3(Rr(-0.4f, 0.4f), 0, Rr(-0.4f, 0.4f));
                                float s = Rr(0.12f, 0.3f);
                                Shape(holder, "ToadstoolStalk", CylMesh(), fp + new Vector3(0, s, 0), new Vector3(0.35f * s, s, 0.35f * s), stalk, false, Quaternion.identity);
                                Shape(holder, "ToadstoolCap", BlobMesh(), fp + new Vector3(0, s * 2f, 0), new Vector3(1.4f * s, 0.6f * s, 1.4f * s), rng.NextDouble() < 0.4 ? capRed : capTan, false, Quaternion.identity);
                            }
                        else if (roll < 0.96)
                        {
                            // A mossy fallen log.
                            float len = Rr(2f, 3.6f);
                            Quaternion q = Quaternion.Euler(0, Rr(0, 180f), 90f);
                            Shape(holder, "FallenLog", CylMesh(), at + new Vector3(0, 0.35f, 0), new Vector3(0.7f, len / 2f, 0.7f), bark, false, q);
                            Shape(holder, "LogMoss", BlobMesh(), at + new Vector3(0, 0.62f, 0), new Vector3(0.6f, 0.2f, len * 0.6f), moss, false, Quaternion.Euler(0, q.eulerAngles.y + 90f, 0));
                        }
                        else Shape(holder, "Stump", CylMesh(), at + new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.3f, 0.9f), bark, false, Quaternion.identity);
                    }
                }

            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            MergeRenderers(holder, "ForestMesh");

            // ---- fireflies drifting in the shade, scattered over the street
            int flies = Mathf.Clamp(g.width * g.height / 180, 3, 9);
            for (int k = 0; k < flies; k++)
            {
                var spot = new Vector2Int(rng.Next(1, g.width - 1), rng.Next(1, g.height - 1));
                Motes(layout, "Fireflies", g.Local(spot) + new Vector3(0, 1.8f, 0), new Vector3(cs * 4f, 2.5f, cs * 4f), new Color(0.85f, 1f, 0.45f), 5f, 3f, 6f,
                    new Vector2(0.06f, 0.12f), new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.3f, 0.4f), 0f);
            }
        }

        /// <summary>A broad oak: a thick trunk with a fork and a crown of three or four overlapping leaf blobs.</summary>
        private static void ForestOak(Transform holder, Vector3 p, float s, Material bark, Material leaf, System.Random rng)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Shape(holder, "OakTrunk", CylMesh(), p + new Vector3(0, 2f * s, 0), new Vector3(0.8f * s, 2f * s, 0.8f * s), bark, false, Quaternion.identity);
            Shape(holder, "OakBranch", CylMesh(), p + new Vector3(0.6f * s, 4.2f * s, 0), new Vector3(0.4f * s, 1.1f * s, 0.4f * s), bark, false, Quaternion.Euler(0, 0, -35f));
            int blobs = 3 + rng.Next(2);
            for (int k = 0; k < blobs; k++)
                Shape(holder, "OakCrown", BlobMesh(), p + new Vector3(Rr(-1.4f, 1.4f) * s, (4.6f + Rr(0f, 2f)) * s, Rr(-1.4f, 1.4f) * s),
                    new Vector3(Rr(3.4f, 4.8f), Rr(2.6f, 3.6f), Rr(3.4f, 4.8f)) * s, leaf, false, Quaternion.Euler(0, Rr(0, 360f), 0));
        }

        /// <summary>A slim pale birch with a narrow, lighter crown.</summary>
        private static void ForestBirch(Transform holder, Vector3 p, float s, Material birch, Material leaf, System.Random rng)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Shape(holder, "BirchTrunk", CylMesh(), p + new Vector3(0, 3f * s, 0), new Vector3(0.4f * s, 3f * s, 0.4f * s), birch, false, Quaternion.Euler(Rr(-4f, 4f), 0, Rr(-4f, 4f)));
            for (int k = 0; k < 2; k++)
                Shape(holder, "BirchCrown", BlobMesh(), p + new Vector3(Rr(-0.5f, 0.5f) * s, (5.6f + k * 1.5f) * s, Rr(-0.5f, 0.5f) * s),
                    new Vector3(2.4f - k * 0.6f, 2.6f, 2.4f - k * 0.6f) * s, leaf, false, Quaternion.Euler(0, Rr(0, 360f), 0));
        }

        /// <summary>A fern: a fan of flat fronds arching out from the ground.</summary>
        private static void ForestFern(Transform holder, Vector3 p, float s, Material fern, System.Random rng)
        {
            int fronds = 5 + rng.Next(3);
            float a0 = (float)rng.NextDouble() * 360f;
            for (int k = 0; k < fronds; k++)
            {
                float a = a0 + k * 360f / fronds;
                Quaternion q = Quaternion.Euler(0, a, 0) * Quaternion.Euler(-35f, 0, 0);
                Deco(holder, PrimitiveType.Cube, "Frond", p + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.3f * s, 0.45f * s), new Vector3(0.28f * s, 0.03f, 1f * s), fern, q, false);
            }
        }
    }
}
