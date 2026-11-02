using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// What you see past the edge of a street, matching how the skylines paint the world (WizardsVRCSceneBuilder.Sky.cs:
    /// every place is an isle in a sea). Each street now stands on a rocky island whose cliffs drop to a rolling sea, with
    /// the rest of its neighbourhood on the strip round the map: rooftops, chimneys and lamplit windows round a town, hills,
    /// trees and giant flowers round a garden, forest round a grove, crags and ruins round a keep, smokestacks and slag
    /// round the forges, warehouses round the docks, and moored boats, buoys and a lighthouse where it meets the water.
    /// Far islets dot the sea round every street and the castle. None of it is solid; each street's props are merged into
    /// one mesh per material.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float BackdropMargin = 46f;

        /// <summary>The island, sea and scenery round a street (zone-local; the map is W x H centred on the origin).</summary>
        private static void BuildStreetBackdrop(Transform root, ZoneGrid g, ZoneDefinition z, float W, float H, float baseY, Vector3 mouth,
            Material bld, Material roof, Material leaves, Material trunk, Material glow)
        {
            var tm = TunnelMats(z);
            Material rock = tm[5];
            var rng = new System.Random(StableSeed(z.zoneKey) ^ 0x6b1d);
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            ZoneTextureStyle st = z.textureStyle;
            bool garden = IsGardenZone(g);
            // Dustwind: a mesa edge over desert flats instead of an island in the sea.
            bool desert = IsDesert(z) || st == ZoneTextureStyle.Mine;
            if (desert) garden = false;

            // ---- which sides meet the water (a side with a lot of '~' on its edge has no land beyond it)
            bool[] wet = new bool[4];   // north, east, south, west (map up = north)
            float seaY = baseY - 22f;
            var surfaces = WaterSurfaces(g);
            float edgeWater = float.MaxValue;
            for (int side = 0; side < 4; side++)
            {
                int n = side % 2 == 0 ? g.width : g.height, water = 0;
                for (int i = 0; i < n; i++)
                {
                    for (int d = 0; d < 1; d++)   // the outermost row or column only: a canal inside the edge doesn't count
                    {
                        int x = side == 1 ? g.width - 1 - d : side == 3 ? d : i;
                        int y = side == 0 ? d : side == 2 ? g.height - 1 - d : i;
                        if (!ZoneGrid.IsWater(g.At(x, y))) continue;
                        water++;
                        float s;
                        if (surfaces.TryGetValue(new Vector2Int(x, y), out s)) edgeWater = Mathf.Min(edgeWater, s);
                        break;
                    }
                }
                wet[side] = !desert && water > n * 0.3f;
            }
            if (edgeWater < float.MaxValue) seaY = edgeWater - 0.08f;

            // ---- the island: cliffs from the sea up to just under the map, reaching out past every dry side
            float hw = W / 2f, hh = H / 2f, mN = wet[0] ? 0f : BackdropMargin, mE = wet[1] ? 0f : BackdropMargin,
                  mS = wet[2] ? 0f : BackdropMargin, mW = wet[3] ? 0f : BackdropMargin;
            float c = 14f;
            var poly = new[]
            {
                new Vector2(-hw - mW + c, -hh - mS), new Vector2(hw + mE - c, -hh - mS), new Vector2(hw + mE, -hh - mS + c), new Vector2(hw + mE, hh + mN - c),
                new Vector2(hw + mE - c, hh + mN), new Vector2(-hw - mW + c, hh + mN), new Vector2(-hw - mW, hh + mN - c), new Vector2(-hw - mW, -hh - mS + c),
            };
            float top = baseY - GroundDrop - 0.3f;
            Material isleTop = desert ? PsxTexMat(z.zoneKey + "_IsleSand", z.groundColor, TileTexture(TerrainTexDir + "/DesertSand.png"), 3f)
                : garden || st == ZoneTextureStyle.Forest
                ? PsxTexMat(z.zoneKey + "_IsleGrass", z.grassColor, TileTexture(TerrainTexDir + "/Grass.png"), 3f)
                : PsxTexMat(z.zoneKey + "_IsleGround", z.groundColor, TileTexture(TerrainTexDir + "/Dirt.png"), 3f);
            string key = z.zoneKey + "_" + Mathf.RoundToInt(W) + "x" + Mathf.RoundToInt(H) + "_" + Mathf.RoundToInt(seaY * 10f) + "_" + Mathf.RoundToInt(top * 10f) +
                         (wet[0] ? "n" : "") + (wet[1] ? "e" : "") + (wet[2] ? "s" : "") + (wet[3] ? "w" : "");
            Shape(root, "IsleCliff", PrismMesh("PSX_Isle_" + key, poly, seaY - 4f, top - 0.2f), Vector3.zero, Vector3.one, rock, false, Quaternion.identity);
            Shape(root, "IsleTop", PrismMesh("PSX_IsleTop_" + key, poly, top - 0.2f, top), Vector3.zero, Vector3.one, isleTop, false, Quaternion.identity);
            Material sandFloor = PsxTexMat("DesertFloor", new Color(0.85f, 0.62f, 0.42f), TileTexture(TerrainTexDir + "/DesertSand.png"), 6f);
            if (desert) Shape(root, "DesertFloor", GridMesh(8, 8, false), new Vector3(0, seaY, 0), new Vector3(W + 900f, 1f, H + 900f), sandFloor, false, Quaternion.identity);
            else
            {
                var sea = Shape(root, "Sea", GridMesh(64, 64, false), new Vector3(0, seaY, 0), new Vector3(W + 900f, 1f, H + 900f), PsxSwellMat(), false, Quaternion.identity);
                sea.isStatic = false;
            }

            var holder = Child(root, "Backdrop", Vector3.zero);
            // Rocks along the foot of the cliffs.
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = (b - a).magnitude;
                for (float t = 0.05f; t < 1f; t += 18f / Mathf.Max(18f, len))
                {
                    Vector2 p = Vector2.Lerp(a, b, t) * 1.02f;
                    Shape(holder, "FootRock", BlobMesh(), new Vector3(p.x, seaY + 0.5f, p.y), new Vector3(Rr(5f, 10f), Rr(3f, 6f), Rr(5f, 10f)), rock, false, Quaternion.Euler(0, Rr(0, 360), 0));
                }
            }

            // ---- the neighbourhood on the strip round the map, in rows along each dry side
            for (int side = 0; side < 4; side++)
            {
                if (wet[side]) continue;
                bool ns = side % 2 == 0;
                float len = (ns ? W : H) + BackdropMargin * 2f;
                Vector3 along = ns ? Vector3.right : Vector3.forward;
                Vector3 outward = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.left;
                float edge = ns ? hh : hw;
                foreach (float row in new[] { 9f, 26f })
                {
                    for (float u = -len / 2f + 6f; u < len / 2f - 6f; u += Rr(9f, 15f))
                    {
                        Vector3 p = along * u + outward * (edge + row + Rr(-3f, 3f));
                        p.y = top;
                        Vector3 flat = new Vector3(p.x, 0f, p.z);
                        if ((flat - new Vector3(mouth.x, 0, mouth.z)).magnitude < 75f) continue;   // keep clear of the tunnel's mountains
                        if (p.x > hw + mE - 4f || p.x < -hw - mW + 4f || p.z > hh + mN - 4f || p.z < -hh - mS + 4f) continue;   // off the island
                        Quaternion face = Quaternion.LookRotation(-outward) * Quaternion.Euler(0, Rr(-12f, 12f), 0);
                        if (desert) DesertBackdropProp(holder, st, p, face, row > 15f, rng, bld, roof, leaves, trunk, glow, rock, tm[6]);
                        else BackdropProp(holder, st, garden, p, face, row > 15f, rng, bld, roof, leaves, trunk, glow, rock, z.accentColor);
                    }
                }
            }
            // Sea-facing sides: moored boats and buoys along the shore, and a lighthouse on a rock offshore.
            for (int side = 0; side < 4; side++)
            {
                if (!wet[side]) continue;
                bool ns = side % 2 == 0;
                Vector3 along = ns ? Vector3.right : Vector3.forward;
                Vector3 outward = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.left;
                float edge = ns ? hh : hw;
                float len = ns ? W : H;
                for (int k = 0; k < 5; k++)
                {
                    Vector3 p = along * Rr(-len * 0.45f, len * 0.45f) + outward * (edge + Rr(18f, 70f));
                    Boat(holder, new Vector3(p.x, seaY, p.z), Quaternion.Euler(0, Rr(0, 360), 0), rng, z.accentColor);
                }
                for (int k = 0; k < 6; k++)
                {
                    Vector3 p = along * Rr(-len * 0.5f, len * 0.5f) + outward * (edge + Rr(8f, 40f));
                    Buoy(holder, new Vector3(p.x, seaY, p.z), k % 2 == 0);
                }
                Vector3 lh = along * Rr(-len * 0.25f, len * 0.25f) + outward * (edge + 110f);
                Lighthouse(holder, new Vector3(lh.x, seaY, lh.z), rock);
            }
            // Far islets (buttes, out on the desert) all round, inside the draw distance.
            if (desert) DesertButtes(holder, Mathf.Max(hw, hh) + BackdropMargin + 60f, Mathf.Max(hw, hh) + BackdropMargin + 170f, seaY, 9, rng, rock, tm[6]);
            else Islets(holder, Vector3.zero, Mathf.Max(hw, hh) + BackdropMargin + 40f, Mathf.Max(hw, hh) + BackdropMargin + 150f, seaY, 7, rng, rock, leaves, trunk);

            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            MergeRenderers(holder, "BackdropMesh");
        }

        /// <summary>A plain box with no collider (scenery nobody can reach).</summary>
        private static void BackBlock(Transform holder, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            Deco(holder, PrimitiveType.Cube, name, pos, scale, mat, rot, false);
        }

        /// <summary>One piece of the neighbourhood for the street's style (back row: a little bigger and plainer).</summary>
        private static void BackdropProp(Transform holder, ZoneTextureStyle st, bool garden, Vector3 p, Quaternion face, bool back, System.Random rng,
            Material bld, Material roof, Material leaves, Material trunk, Material glow, Material rock, Color accent)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (garden)
            {
                if (rng.NextDouble() < 0.45) Hill(holder, p, Rr(14f, 26f), Rr(5f, 11f), PMat("BackdropHill_" + ColorUtility.ToHtmlStringRGB(leaves.color), leaves.color * 0.9f, 4f));
                if (rng.NextDouble() < 0.6) RoundTree(holder, p + new Vector3(Rr(-4f, 4f), 0, Rr(-4f, 4f)), Rr(0.9f, 1.6f), leaves, trunk);
                else GiantFlower(holder, p, Rr(4f, 9f), accent, rng);
                return;
            }
            switch (st)
            {
                case ZoneTextureStyle.Forest:
                    if (rng.NextDouble() < 0.35) Hill(holder, p, Rr(16f, 28f), Rr(6f, 12f), PMat("BackdropHill_" + ColorUtility.ToHtmlStringRGB(leaves.color), leaves.color * 0.8f, 4f));
                    for (int k = 0; k < 3; k++) Conifer(holder, p + new Vector3(Rr(-5f, 5f), 0, Rr(-5f, 5f)), Rr(1f, 1.8f), leaves, trunk);
                    return;
                case ZoneTextureStyle.Keep:
                    if (rng.NextDouble() < 0.5) Crag(holder, p, Rr(8f, 22f), rock, rng);
                    else RuinedTower(holder, p, Rr(8f, 16f), bld, rock);
                    return;
                default:
                {
                    // Town, docks and forges: houses (warehouses at the docks), with chimneys or smokestacks.
                    float w = back ? Rr(9f, 14f) : Rr(7f, 11f), d = Rr(7f, 10f), h = back ? Rr(10f, 18f) : Rr(7f, 13f);
                    if (st == ZoneTextureStyle.Docks) { w *= 1.4f; h *= 0.8f; }
                    House(holder, p, face, w, d, h, bld, roof, glow, rng, st == ZoneTextureStyle.Forge);
                    if (st == ZoneTextureStyle.Forge && rng.NextDouble() < 0.3)
                        Crag(holder, p + face * new Vector3(Rr(-6f, 6f), 0, -8f), Rr(4f, 7f), PMat("Slag", new Color(0.2f, 0.17f, 0.16f), 3f), rng);
                    if (back && rng.NextDouble() < 0.08)
                    {
                        // A tall lantern tower, like the skylines show over every town.
                        Shape(holder, "LanternTower", CylMesh(), p + new Vector3(0, 14f, 0), new Vector3(3f, 14f, 3f), bld, false, Quaternion.identity);
                        Shape(holder, "LanternTowerTop", ConeMesh(), p + new Vector3(0, 30.5f, 0), new Vector3(4f, 4f, 4f), roof, false, Quaternion.identity);
                        Shape(holder, "LanternTowerLight", GemMesh(), p + new Vector3(0, 28f, 0), new Vector3(2f, 2.4f, 2f), glow, false, Quaternion.identity);
                    }
                    return;
                }
            }
        }

        private static void House(Transform holder, Vector3 p, Quaternion face, float w, float d, float h, Material bld, Material roof, Material glow,
            System.Random rng, bool forge)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            BackBlock(holder, "BackHouse", p + new Vector3(0, h / 2f, 0), new Vector3(w, h, d), bld, face);
            // A pyramid roof (an eight-sided cone squared off by turning it 22.5 degrees).
            Shape(holder, "BackRoof", ConeMesh(), p + new Vector3(0, h + w * 0.22f, 0), new Vector3(w * 1.3f, w * 0.45f, d * 1.3f), roof, false, face * Quaternion.Euler(0, 22.5f, 0));
            if (forge && rng.NextDouble() < 0.6)
                Shape(holder, "Smokestack", CylMesh(), p + face * new Vector3(w * 0.25f, h + 5f, 0), new Vector3(1.6f, 6f, 1.6f), PMat("Brick", new Color(0.45f, 0.25f, 0.2f), 2f), false, Quaternion.identity);
            else if (rng.NextDouble() < 0.6)
                BackBlock(holder, "Chimney", p + face * new Vector3(w * 0.3f, h + w * 0.25f, d * 0.1f), new Vector3(0.9f, 2.6f, 0.9f), bld, face);
            // Lamplit windows on the side facing the street.
            int rows = Mathf.Max(1, Mathf.FloorToInt((h - 2f) / 3.5f));
            int cols = Mathf.Max(1, Mathf.FloorToInt(w / 3f));
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    if (rng.NextDouble() < 0.45) continue;
                    float x = (k + 0.5f) / cols * w - w / 2f;
                    BackBlock(holder, "BackWindow", p + face * new Vector3(x, 2.4f + r * 3.5f, d / 2f + 0.03f), new Vector3(0.9f, 1.3f, 0.06f), glow, face);
                }
        }

        private static void Hill(Transform holder, Vector3 p, float w, float h, Material mat)
        {
            Shape(holder, "Hill", BlobMesh(), p + new Vector3(0, h * 0.25f, 0), new Vector3(w, h, w * 0.85f), mat, false, Quaternion.Euler(0, w * 7f, 0));
        }

        private static void RoundTree(Transform holder, Vector3 p, float s, Material leaves, Material trunk)
        {
            Shape(holder, "Trunk", CylMesh(), p + new Vector3(0, 2.2f * s, 0), new Vector3(0.7f * s, 2.2f * s, 0.7f * s), trunk, false, Quaternion.identity);
            Shape(holder, "Crown", BlobMesh(), p + new Vector3(0, 5.6f * s, 0), new Vector3(5f * s, 4.4f * s, 5f * s), leaves, false, Quaternion.Euler(0, s * 50f, 0));
            Shape(holder, "Crown", BlobMesh(), p + new Vector3(1.2f * s, 7.2f * s, 0.4f * s), new Vector3(3.2f * s, 3f * s, 3.2f * s), leaves, false, Quaternion.identity);
        }

        private static void Conifer(Transform holder, Vector3 p, float s, Material leaves, Material trunk)
        {
            Shape(holder, "Trunk", CylMesh(), p + new Vector3(0, 1.4f * s, 0), new Vector3(0.6f * s, 1.4f * s, 0.6f * s), trunk, false, Quaternion.identity);
            for (int k = 0; k < 3; k++)
                Shape(holder, "Needles", ConeMesh(), p + new Vector3(0, (3.4f + k * 2.6f) * s, 0), new Vector3((5f - k * 1.3f) * s, 3.6f * s, (5f - k * 1.3f) * s), leaves, false, Quaternion.Euler(0, k * 20f, 0));
        }

        private static void GiantFlower(Transform holder, Vector3 p, float h, Color accent, System.Random rng)
        {
            var stem = PMat("FlowerStem", new Color(0.3f, 0.55f, 0.25f), 2f);
            var petal = PMat("GiantPetal_" + ColorUtility.ToHtmlStringRGB(accent), accent, 1f, accent * 0.2f);
            var heart = PMat("FlowerHeart", new Color(1f, 0.85f, 0.3f), 1f, new Color(0.4f, 0.3f, 0.05f));
            Shape(holder, "Stem", CylMesh(), p + new Vector3(0, h / 2f, 0), new Vector3(0.5f, h / 2f, 0.5f), stem, false, Quaternion.identity);
            for (int k = 0; k < 6; k++)
                Shape(holder, "Petal", BlobMesh(), p + new Vector3(0, h, 0) + Quaternion.Euler(0, k * 60f, 0) * new Vector3(0, 0, 1.3f), new Vector3(1.4f, 0.4f, 2.2f), petal, false,
                    Quaternion.Euler(-15f, k * 60f, 0));
            Shape(holder, "Heart", BlobMesh(), p + new Vector3(0, h + 0.2f, 0), Vector3.one * 1.2f, heart, false, Quaternion.identity);
        }

        private static void Crag(Transform holder, Vector3 p, float h, Material rock, System.Random rng)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int k = 0; k < 3; k++)
                Shape(holder, "Crag", BlobMesh(), p + new Vector3(Rr(-3f, 3f), h * (0.3f + k * 0.15f), Rr(-3f, 3f)), new Vector3(Rr(5f, 9f), h * (0.9f - k * 0.2f), Rr(5f, 9f)), rock, false,
                    Quaternion.Euler(Rr(-10f, 10f), Rr(0, 360f), Rr(-10f, 10f)));
        }

        private static void RuinedTower(Transform holder, Vector3 p, float h, Material bld, Material rock)
        {
            Shape(holder, "Ruin", CylMesh(), p + new Vector3(0, h / 2f, 0), new Vector3(6f, h / 2f, 6f), bld, false, Quaternion.identity);
            for (int k = 0; k < 6; k++)
            {
                if (k % 3 == 1) continue;   // broken battlements
                Vector3 o = Quaternion.Euler(0, k * 60f, 0) * new Vector3(0, 0, 2.6f);
                BackBlock(holder, "RuinMerlon", p + o + new Vector3(0, h + 0.8f + (k % 2) * 0.6f, 0), new Vector3(1.4f, 1.6f + (k % 2) * 1.2f, 1f), bld, Quaternion.Euler(0, k * 60f, 0));
            }
            Shape(holder, "Rubble", BlobMesh(), p + new Vector3(4f, 0.6f, 2f), new Vector3(4f, 1.6f, 3f), rock, false, Quaternion.identity);
        }

        private static void Boat(Transform holder, Vector3 p, Quaternion rot, System.Random rng, Color accent)
        {
            var hull = PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f);
            var sail = PMat("Canvas", new Color(0.92f, 0.88f, 0.8f), 1f);
            Shape(holder, "Hull", BlobMesh(), p + new Vector3(0, 0.3f, 0), new Vector3(2.6f, 1.6f, 7f), hull, false, rot);
            BackBlock(holder, "Deck", p + new Vector3(0, 0.95f, 0), new Vector3(2f, 0.15f, 5.6f), hull, rot);
            Shape(holder, "Mast", CylMesh(), p + new Vector3(0, 4.2f, 0), new Vector3(0.22f, 3.4f, 0.22f), hull, false, rot);
            BackBlock(holder, "Sail", p + rot * new Vector3(0, 4.6f, 0.9f), new Vector3(0.08f, 4.2f, 2.8f), sail, rot);
            BackBlock(holder, "Pennant", p + new Vector3(0, 7.8f, 0) + rot * new Vector3(0, 0, -0.4f), new Vector3(0.05f, 0.4f, 0.9f), PMat("Pennant_" + ColorUtility.ToHtmlStringRGB(accent), accent, 1f), rot);
        }

        private static void Buoy(Transform holder, Vector3 p, bool red)
        {
            var mat = red ? PMat("BuoyRed", new Color(0.85f, 0.2f, 0.15f), 1f) : PMat("BuoyWhite", new Color(0.92f, 0.92f, 0.88f), 1f);
            Shape(holder, "Buoy", CylMesh(), p + new Vector3(0, 0.6f, 0), new Vector3(1.1f, 0.7f, 1.1f), mat, false, Quaternion.identity);
            Shape(holder, "BuoyTop", ConeMesh(), p + new Vector3(0, 1.6f, 0), new Vector3(0.7f, 0.9f, 0.7f), mat, false, Quaternion.identity);
            Shape(holder, "BuoyLamp", GemMesh(), p + new Vector3(0, 2.2f, 0), new Vector3(0.3f, 0.4f, 0.3f), PMat("BuoyLamp", new Color(1f, 0.9f, 0.5f), 1f, new Color(0.9f, 0.7f, 0.3f)), false, Quaternion.identity);
        }

        private static void Lighthouse(Transform holder, Vector3 p, Material rock)
        {
            var white = PMat("LighthouseWhite", new Color(0.92f, 0.9f, 0.86f), 1f);
            var red = PMat("LighthouseRed", new Color(0.75f, 0.18f, 0.15f), 1f);
            Shape(holder, "LighthouseRock", BlobMesh(), p + new Vector3(0, 1f, 0), new Vector3(16f, 8f, 14f), rock, false, Quaternion.identity);
            for (int k = 0; k < 5; k++)
                Shape(holder, "LighthouseBand", CylMesh(), p + new Vector3(0, 5f + k * 3f + 1.5f, 0), new Vector3(4.4f - k * 0.3f, 1.5f, 4.4f - k * 0.3f), k % 2 == 0 ? white : red, false, Quaternion.identity);
            Shape(holder, "LighthouseLamp", GemMesh(), p + new Vector3(0, 22.6f, 0), new Vector3(3f, 3.2f, 3f), PMat("LighthouseLamp", new Color(1f, 0.92f, 0.6f), 1f, new Color(1f, 0.85f, 0.45f)), false, Quaternion.identity);
            Shape(holder, "LighthouseCap", ConeMesh(), p + new Vector3(0, 25.6f, 0), new Vector3(3.8f, 2.6f, 3.8f), red, false, Quaternion.identity);
        }

        /// <summary>Low islets scattered between two radii round 'centre': a rock with a grassy top and a tree or two.</summary>
        private static void Islets(Transform holder, Vector3 centre, float r0, float r1, float seaY, int count, System.Random rng, Material rock, Material leaves, Material trunk)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var grass = PMat("IsletGrass", new Color(0.35f, 0.55f, 0.3f), 4f);
            for (int k = 0; k < count; k++)
            {
                float a = (k + Rr(0.1f, 0.9f)) / count * 360f;
                float r = Rr(r0, r1);
                Vector3 p = centre + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, r);
                p.y = seaY;
                float w = Rr(18f, 40f);
                Shape(holder, "Islet", BlobMesh(), p + new Vector3(0, 1f, 0), new Vector3(w, Rr(6f, 12f), w * Rr(0.6f, 1f)), rock, false, Quaternion.Euler(0, Rr(0, 360f), 0));
                Shape(holder, "IsletTop", BlobMesh(), p + new Vector3(0, 4.2f, 0), new Vector3(w * 0.8f, 2.4f, w * 0.6f), grass, false, Quaternion.Euler(0, Rr(0, 360f), 0));
                int trees = rng.Next(0, 3);
                for (int t = 0; t < trees; t++) RoundTree(holder, p + new Vector3(Rr(-w * 0.2f, w * 0.2f), 5f, Rr(-w * 0.15f, w * 0.15f)), Rr(0.8f, 1.3f), leaves, trunk);
            }
        }

        /// <summary>Round the castle's island: islets, a lighthouse on a rock, boats under sail and buoys off the shore.</summary>
        private static void HubBackdrop(Transform root, float islandR, float seaY, Material rock, Material leaves, Material trunk, Color accent)
        {
            var rng = new System.Random(0x4ab);
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var holder = Child(root, "HubBackdrop", Vector3.zero);
            Islets(holder, Vector3.zero, islandR + 60f, islandR + 150f, seaY, 9, rng, rock, leaves, trunk);
            Lighthouse(holder, Quaternion.Euler(0, 205f, 0) * new Vector3(0, 0, islandR + 95f) + new Vector3(0, seaY, 0), rock);
            for (int k = 0; k < 7; k++)
            {
                Vector3 p = Quaternion.Euler(0, Rr(0, 360f), 0) * new Vector3(0, 0, islandR + Rr(30f, 90f));
                Boat(holder, p + new Vector3(0, seaY, 0), Quaternion.Euler(0, Rr(0, 360f), 0), rng, accent);
            }
            for (int k = 0; k < 10; k++)
            {
                Vector3 p = Quaternion.Euler(0, k * 36f + Rr(0f, 20f), 0) * new Vector3(0, 0, islandR + Rr(22f, 40f));
                Buoy(holder, p + new Vector3(0, seaY, 0), k % 2 == 0);
            }
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            MergeRenderers(holder, "HubBackdropMesh");
        }
    }
}
