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
    /// Far islets dot the sea round every street and the castle. The land climbs away from each street in rock terraces,
    /// in three rows, so the neighbourhood shows over the roofs of the street's edge buildings and a skyline of tall shapes
    /// (towers, spires, smokestacks, giant trees, crags, sandstone spires) stands behind it. None of it is solid; each
    /// street's props are merged into one mesh per material.
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
            // The map's own sea level, which OuterSea carries on past its wet edges: boats and buoys out there float on it.
            float wetY = edgeWater < float.MaxValue ? edgeWater : seaY;
            // Never above the island's top (or it floods the street and the land round it); the map's own sea carries on
            // to the horizon at its level past the wet edges (OuterSea).
            seaY = Mathf.Min(seaY, baseY - GroundDrop - 0.3f - 0.5f);

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
                // Not along a side that meets the sea: there the island's edge is the map's edge, under the harbour, and
                // the rocks would poke up through the water by the quays. Edges 0, 2, 4, 6 are south, east, north and west;
                // the odd ones are the corners between them.
                bool sWet = wet[2], eWet = wet[1], nWet = wet[0], wWet = wet[3];
                bool onWet = i == 0 ? sWet : i == 1 ? sWet || eWet : i == 2 ? eWet : i == 3 ? eWet || nWet : i == 4 ? nWet : i == 5 ? nWet || wWet : i == 6 ? wWet : wWet || sWet;
                if (onWet) continue;
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float len = (b - a).magnitude;
                for (float t = 0.05f; t < 1f; t += 18f / Mathf.Max(18f, len))
                {
                    Vector2 p = Vector2.Lerp(a, b, t) * 1.02f;
                    Shape(holder, "FootRock", BlobMesh(), new Vector3(p.x, seaY + 0.5f, p.y), new Vector3(Rr(5f, 10f), Rr(3f, 6f), Rr(5f, 10f)), rock, false, Quaternion.Euler(0, Rr(0, 360), 0));
                }
            }

            // ---- the neighbourhood on the strip round the map, in rows along each dry side. The land climbs away from
            // the street: every piece stands on a rock terrace high enough that it shows over the roofs of the street's
            // edge buildings (a player in the street sees the near row over the roofs, and the rows behind it higher still).
            float roofline = Mathf.Max(8f, EdgeRoofline(g, z) - top);
            var mouths = TunnelMouths(g, z, mouth);
            float[] rowOut = { 9f, 26f, 39f };
            float[] rowGapMin = { 9f, 9f, 20f }, rowGapMax = { 15f, 15f, 32f };
            // How high each row's tops reach above the island (times the roofline, plus metres): enough that, from about
            // 20 m into the street, each row shows over the edge roofs in front of it.
            float[] rowRise = { 1.5f, 2.2f, 2.8f }, rowPlus = { 4f, 8f, 10f };
            for (int side = 0; side < 4; side++)
            {
                if (wet[side]) continue;
                bool ns = side % 2 == 0;
                float len = (ns ? W : H) + BackdropMargin * 2f;
                Vector3 along = ns ? Vector3.right : Vector3.forward;
                Vector3 outward = side == 0 ? Vector3.forward : side == 1 ? Vector3.right : side == 2 ? Vector3.back : Vector3.left;
                float edge = ns ? hh : hw;
                float margin = side == 0 ? mN : side == 1 ? mE : side == 2 ? mS : mW;
                for (int row = 0; row < rowOut.Length; row++)
                {
                    for (float u = -len / 2f + 6f; u < len / 2f - 6f; u += Rr(rowGapMin[row], rowGapMax[row]))
                    {
                        Vector3 p = along * u + outward * (edge + rowOut[row] + Rr(-3f, 3f));
                        p.y = top;
                        if (NearTunnel(p, mouths)) continue;   // keep clear of the tunnels' mountains
                        if (p.x > hw + mE - 4f || p.x < -hw - mW + 4f || p.z > hh + mN - 4f || p.z < -hh - mS + 4f) continue;   // off the island
                        Quaternion face = Quaternion.LookRotation(-outward) * Quaternion.Euler(0, Rr(-12f, 12f), 0);
                        var piece = Child(holder, "Piece", Vector3.zero);
                        if (row == 2) SkylineProp(piece, st, garden, desert, p, rng, bld, roof, leaves, trunk, glow, rock, tm[6], z.accentColor);
                        else if (desert) DesertBackdropProp(piece, st, p, face, row > 0, rng, bld, roof, leaves, trunk, glow, rock, tm[6]);
                        else BackdropProp(piece, st, garden, p, face, row > 0, rng, bld, roof, leaves, trunk, glow, rock, z.accentColor);
                        float target = (roofline * rowRise[row] + rowPlus[row]) * Rr(0.9f, 1.12f);
                        RaiseOnTerrace(holder, piece, root, p, target, outward, edge, margin, along, ns ? hw + mE : hh + mN, ns ? -hw - mW : -hh - mS,
                            rock, isleTop, rng);
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
                    Boat(holder, new Vector3(p.x, wetY, p.z), Quaternion.Euler(0, Rr(0, 360), 0), rng, z.accentColor);
                }
                for (int k = 0; k < 6; k++)
                {
                    Vector3 p = along * Rr(-len * 0.5f, len * 0.5f) + outward * (edge + Rr(8f, 40f));
                    Buoy(holder, new Vector3(p.x, wetY, p.z), k % 2 == 0);
                }
                Vector3 lh = along * Rr(-len * 0.25f, len * 0.25f) + outward * (edge + 110f);
                Lighthouse(holder, new Vector3(lh.x, wetY, lh.z), rock);
            }
            // Far islets (buttes, out on the desert) all round, inside the draw distance.
            // (Tall enough that their tops clear the street's roofline too, from the desert floor far below.)
            if (desert) DesertButtes(holder, Mathf.Max(hw, hh) + BackdropMargin + 60f, Mathf.Max(hw, hh) + BackdropMargin + 170f, seaY, 9, rng, rock, tm[6],
                top - seaY + roofline * 2.1f + 10f);
            else Islets(holder, Vector3.zero, Mathf.Max(hw, hh) + BackdropMargin + 40f, Mathf.Max(hw, hh) + BackdropMargin + 150f, seaY, 7, rng, rock, leaves, trunk);

            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            MergeRenderers(holder, "BackdropMesh");
        }

        /// <summary>The height (zone-local) of the tallest roofs round the map's edge: what the backdrop has to show over.</summary>
        private static float EdgeRoofline(ZoneGrid g, ZoneDefinition z)
        {
            // As House() builds them: Dustwind's frontier buildings are 8-13 m and 6 m taller on the map's edge, the rest
            // the zone's buildingHeight and 4 m taller on the edge; then the roof ridge and chimneys.
            bool frontier = z.textureStyle == ZoneTextureStyle.Boomtown || z.textureStyle == ZoneTextureStyle.Desert || z.textureStyle == ZoneTextureStyle.Mine;
            float house = frontier ? 8f + 5f + 6f + 2f : z.buildingHeight.y + 4f + 2.2f;
            float ground = float.MinValue;
            for (int x = 0; x < g.width; x++) ground = Mathf.Max(ground, Mathf.Max(g.Elevation(x, 0), g.Elevation(x, g.height - 1)));
            for (int y = 0; y < g.height; y++) ground = Mathf.Max(ground, Mathf.Max(g.Elevation(0, y), g.Elevation(g.width - 1, y)));
            return ground + house;
        }

        /// <summary>Every tunnel mouth on this map's edge: the way home, and a town's Street Gates (their mountains stand there).</summary>
        private static List<Vector3> TunnelMouths(ZoneGrid g, ZoneDefinition z, Vector3 home)
        {
            var list = new List<Vector3> { home };
            if (z.landmarks == null) return list;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.StreetGate) continue;
                var cells = g.Find(l.Symbol);
                if (cells.Count == 0) continue;
                Vector3 m, o;
                TunnelMouth(g, z, cells[0], out m, out o);
                list.Add(m);
            }
            return list;
        }

        private static bool NearTunnel(Vector3 p, List<Vector3> mouths)
        {
            foreach (var m in mouths)
                if (new Vector2(p.x - m.x, p.z - m.z).magnitude < 80f) return true;
            return false;
        }

        /// <summary>
        /// Lifts a backdrop piece (built at ground level under 'piece') so its top reaches 'target' metres above the island,
        /// and builds the rock terrace it stands on: a block of rock with a cap of the island's ground, boulders round its
        /// foot. The terrace keeps inside the island's strip (never over the map, never past the cliffs).
        /// </summary>
        private static void RaiseOnTerrace(Transform holder, Transform piece, Transform root, Vector3 p, float target, Vector3 outward, float edge,
            float margin, Vector3 along, float alongMax, float alongMin, Material rock, Material cap, System.Random rng)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var rends = piece.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Vector3 lo = b.min - root.position, hi = b.max - root.position;
            float lift = Mathf.Clamp(target - (hi.y - p.y), 0f, 52f);
            if (lift < 1f) return;
            piece.localPosition = new Vector3(0f, lift, 0f);

            // The terrace's footprint: the piece's own, a little wider, clamped to the strip between the map and the cliffs.
            float o0 = Vector3.Dot(lo, outward), o1 = Vector3.Dot(hi, outward);
            float oMin = Mathf.Min(o0, o1) - 1.2f, oMax = Mathf.Max(o0, o1) + 1.2f;
            oMin = Mathf.Max(oMin, edge + 0.6f);
            oMax = Mathf.Min(oMax, edge + margin - 0.6f);
            float a0 = Vector3.Dot(lo, along), a1 = Vector3.Dot(hi, along);
            float aMin = Mathf.Max(Mathf.Min(a0, a1) - 1.2f, alongMin + 0.6f), aMax = Mathf.Min(Mathf.Max(a0, a1) + 1.2f, alongMax - 0.6f);
            if (oMax - oMin < 2f || aMax - aMin < 2f) return;
            Vector3 centre = outward * ((oMin + oMax) / 2f) + along * ((aMin + aMax) / 2f);
            float wOut = oMax - oMin, wAlong = aMax - aMin;
            Vector3 size = new Vector3(Mathf.Abs(along.x) * wAlong + Mathf.Abs(outward.x) * wOut, 0f, Mathf.Abs(along.z) * wAlong + Mathf.Abs(outward.z) * wOut);
            float bottom = p.y - 1.5f, plinthTop = p.y + lift - 0.3f;
            BackBlock(holder, "Terrace", new Vector3(centre.x, (bottom + plinthTop) / 2f, centre.z), new Vector3(size.x, plinthTop - bottom, size.z), rock, Quaternion.identity);
            BackBlock(holder, "TerraceTop", new Vector3(centre.x, p.y + lift - 0.18f, centre.z), new Vector3(size.x + 0.5f, 0.45f, size.z + 0.5f), cap, Quaternion.identity);
            // Boulders round its foot, on the far side and the ends (not toward the street, where they'd poke over the map).
            for (int k = 0; k < 3; k++)
            {
                float t = Rr(-0.5f, 0.5f);
                Vector3 foot = centre + outward * (wOut * 0.5f) + along * (t * wAlong);
                float bs = Mathf.Min(lift * 0.5f, 9f);
                Shape(holder, "TerraceRock", BlobMesh(), new Vector3(foot.x, p.y + bs * 0.25f, foot.z), new Vector3(Rr(0.7f, 1.2f) * bs * 1.4f, bs, Rr(0.7f, 1.2f) * bs), rock, false,
                    Quaternion.Euler(0, Rr(0f, 360f), 0));
            }
        }

        /// <summary>
        /// The far row: the tall shapes that make the street's skyline over its rooftops (lantern towers and spires round a
        /// town, smokestacks round the forges, giant trees round a garden, great pines round a grove, crags and broken
        /// towers round a keep, sandstone spires round Dustwind).
        /// </summary>
        private static void SkylineProp(Transform holder, ZoneTextureStyle st, bool garden, bool desert, Vector3 p, System.Random rng,
            Material bld, Material roof, Material leaves, Material trunk, Material glow, Material rock, Material sandTop, Color accent)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (desert)
            {
                Butte(holder, p, Rr(8f, 14f), Rr(14f, 24f), rock, sandTop, Rr(0f, 360f));
                return;
            }
            if (garden)
            {
                if (rng.NextDouble() < 0.65) RoundTree(holder, p, Rr(2.2f, 3f), leaves, trunk);
                else GiantFlower(holder, p, Rr(12f, 18f), accent, rng);
                return;
            }
            switch (st)
            {
                case ZoneTextureStyle.Forest:
                    Conifer(holder, p, Rr(2.4f, 3.2f), leaves, trunk);
                    Conifer(holder, p + new Vector3(Rr(-6f, 6f), 0, Rr(-6f, 6f)), Rr(1.6f, 2.2f), leaves, trunk);
                    return;
                case ZoneTextureStyle.Keep:
                    if (rng.NextDouble() < 0.55) Crag(holder, p, Rr(18f, 28f), rock, rng);
                    else RuinedTower(holder, p, Rr(14f, 22f), bld, rock);
                    return;
                case ZoneTextureStyle.Forge:
                {
                    var brick = PMat("Brick", new Color(0.45f, 0.25f, 0.2f), 2f);
                    BackBlock(holder, "Foundry", p + new Vector3(0, 4f, 0), new Vector3(Rr(10f, 14f), 8f, Rr(8f, 11f)), bld, Quaternion.Euler(0, Rr(0f, 90f), 0));
                    int stacks = rng.Next(2, 4);
                    for (int k = 0; k < stacks; k++)
                    {
                        float h = Rr(14f, 22f);
                        Shape(holder, "Smokestack", CylMesh(), p + new Vector3(-3f + k * 3f, h / 2f, Rr(-1.5f, 1.5f)), new Vector3(1.8f, h / 2f, 1.8f), brick, false, Quaternion.identity);
                        Shape(holder, "StackLip", CylMesh(), p + new Vector3(-3f + k * 3f, h + 0.3f, 0), new Vector3(2.3f, 0.3f, 2.3f), bld, false, Quaternion.identity);
                    }
                    return;
                }
                default:
                {
                    if (rng.NextDouble() < 0.5)
                    {
                        // A lantern tower.
                        float h = Rr(18f, 26f);
                        Shape(holder, "LanternTower", CylMesh(), p + new Vector3(0, h / 2f, 0), new Vector3(3.2f, h / 2f, 3.2f), bld, false, Quaternion.identity);
                        Shape(holder, "LanternTowerLight", GemMesh(), p + new Vector3(0, h + 1.6f, 0), new Vector3(2.2f, 2.6f, 2.2f), glow, false, Quaternion.identity);
                        Shape(holder, "LanternTowerTop", ConeMesh(), p + new Vector3(0, h + 4.6f, 0), new Vector3(4.4f, 4.4f, 4.4f), roof, false, Quaternion.identity);
                    }
                    else
                    {
                        // A tall townhouse with a steep spire.
                        float w = Rr(7f, 9f), h = Rr(16f, 24f);
                        BackBlock(holder, "Spire", p + new Vector3(0, h / 2f, 0), new Vector3(w, h, w), bld, Quaternion.Euler(0, Rr(0f, 90f), 0));
                        Shape(holder, "SpireRoof", ConeMesh(), p + new Vector3(0, h + w * 0.9f, 0), new Vector3(w * 1.2f, w * 0.95f, w * 1.2f), roof, false, Quaternion.Euler(0, 22.5f, 0));
                        for (int r = 0; r < 3; r++)
                            BackBlock(holder, "SpireWindow", p + new Vector3(0, h * (0.45f + r * 0.18f), -w / 2f - 0.04f), new Vector3(1f, 1.5f, 0.06f), glow, Quaternion.identity);
                    }
                    return;
                }
            }
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
