using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Bigdig (World 3), the kobold undercity: a city of boroughs in one vast cavern under Dustwind, all rusty corrugated
    /// iron, riveted plate and brass, stacked higgledy-piggledy and lit by neon (ZoneTextureStyle.Undercity).
    ///   - Buildings: a shopfront storey, an overhanging upper storey on struts, sometimes a crooked top, with a flat
    ///     plated roof carrying a water tank, a smokestack or a bundle of pipes; porthole windows, a striped awning over the
    ///     door, brass pipes up a corner and a neon blade sign (UndercityHouse).
    ///   - Where a map has trees: stalagmites, some with glowing cave mushrooms, or brass pipe stacks (UndercityTree).
    ///   - Past the map: no sea. The street stands on a rock shelf over a chasm with glowing sludge pools far below; the
    ///     neighbourhood climbs away in shanty towers, smokestacks, cranes, gear towers and neon billboards; rock pillars
    ///     rise from the chasm to the roof with shanties clinging to them; and the cavern's walls and roof close over it
    ///     all, hung with stalactites, chains and lanterns, brass mains running round the walls (CavernShell).
    ///   - Points: neon signs, blasting barrels, heaps of gold, and the casino's slot machines, card tables and roulette
    ///     wheels (UndercityPointLook).
    ///   - Dungeons: the Golden Seam Casino gets red carpet, velvet walls with gold pilasters and chandeliers; the Siphon
    ///     Works gets pipe runs and red warning lamps along its walls (UndercityDungeon).
    /// Original designs throughout: nothing here copies another game's art or names.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static bool IsUndercity(ZoneDefinition z) { return z != null && z.textureStyle == ZoneTextureStyle.Undercity; }

        /// <summary>Bigdig's neon: pink, teal, gold, green, red, violet.</summary>
        private static readonly Color[] UnderNeon =
        {
            new Color(1f, 0.3f, 0.78f), new Color(0.25f, 1f, 0.88f), new Color(1f, 0.8f, 0.25f),
            new Color(0.5f, 1f, 0.32f), new Color(1f, 0.32f, 0.24f), new Color(0.65f, 0.42f, 1f),
        };

        private static Material NeonMat(int i)
        {
            int n = UnderNeon.Length;
            int k = ((i % n) + n) % n;
            Color c = UnderNeon[k];
            return PMat("Neon" + k, c * 0.75f, 1f, c * 1.25f);
        }

        private static Material UnderBrass() { return PMat("Brass", new Color(0.85f, 0.65f, 0.25f), 1f, new Color(0.2f, 0.14f, 0.02f)); }
        private static Material UnderIron() { return PMat("StoveIron", new Color(0.14f, 0.13f, 0.14f), 1f); }
        private static Material UnderGold() { return PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f)); }

        private static Material UnderAwning(int i)
        {
            Color[] cols = { new Color(0.75f, 0.18f, 0.2f), new Color(0.2f, 0.5f, 0.45f), new Color(0.8f, 0.6f, 0.2f), new Color(0.45f, 0.25f, 0.6f) };
            int k = ((i % cols.Length) + cols.Length) % cols.Length;
            return PMat("UnderAwning" + k, cols[k], 1f);
        }

        /// <summary>Axis-aligned size of a box 'along' metres wide across the front and 'deep' metres deep, for a front facing 'front'.</summary>
        private static Vector3 FrontBox(Vector3 front, float along, float height, float deep)
        {
            return Mathf.Abs(front.x) > 0.5f ? new Vector3(deep, height, along) : new Vector3(along, height, deep);
        }

        // ------------------------------------------------------------------ buildings

        /// <summary>A Bigdig building over a block of '#' cells.</summary>
        private static void UndercityHouse(Transform parent, ZoneGrid g, ZoneDefinition z, RectInt r, int index, Material wall, Material roof, Material window)
        {
            float cs = g.cell;
            Vector3 c = RectCenter(g, r);
            bool edge = r.xMin == 0 || r.yMin == 0 || r.xMax == g.width || r.yMax == g.height;
            float h1Hash = g.Hash(r.xMin, r.yMin, 1), h2Hash = g.Hash(r.xMin, r.yMin, 2), h3Hash = g.Hash(r.xMin, r.yMin, 3);
            float sx = r.width * cs, sz = r.height * cs;
            Vector2Int fd = g.FrontSide(r);
            if (fd == Vector2Int.zero)
            {
                // Hemmed in on every side (deep inside a block): only its roofline shows, so just the mass and a roof.
                float hm = Mathf.Lerp(z.buildingHeight.x, z.buildingHeight.y, h1Hash) * 0.55f + 4f + h2Hash * 3f + (edge ? 5f : 0f);
                NoteHouse(r, Vector2Int.zero, hm, HousePlain);
                Block(parent, "Back", c + Vector3.up * (hm / 2f), new Vector3(sx, hm, sz), wall);
                Deco(parent, PrimitiveType.Cube, "BackRoof", c + Vector3.up * (hm + 0.15f), new Vector3(sx + 0.3f, 0.3f, sz + 0.3f), roof, Quaternion.identity, false);
                return;
            }
            Vector3 front = ZoneGrid.ToLocalDir(fd);
            Vector3 across = Vector3.Cross(Vector3.up, front);
            float width = Mathf.Abs(front.x) > 0.5f ? sz : sx;
            float depth = Mathf.Abs(front.x) > 0.5f ? sx : sz;
            Quaternion rotF = Quaternion.LookRotation(front);
            Material brass = UnderBrass(), iron = UnderIron();
            Vector3 up = Vector3.up;

            // Shopfront storey.
            float h1 = Mathf.Lerp(z.buildingHeight.x, z.buildingHeight.y, h1Hash) * 0.55f + (edge ? 2f : 0f);
            NoteHouse(r, fd, h1, HouseShopfront);
            Block(parent, "Shop", c + up * (h1 / 2f), new Vector3(sx, h1, sz), wall);
            // Upper storey, overhanging the street on struts.
            float h2 = 3.5f + h2Hash * 2.5f + (edge ? 3f : 0f);
            const float over = 1.2f;
            Vector3 upperC = c + front * (over / 2f) + up * (h1 + h2 / 2f);
            Deco(parent, PrimitiveType.Cube, "Upper", upperC, FrontBox(front, width - 0.4f, h2, depth + over), wall, Quaternion.identity, false);
            float top = h1 + h2;
            foreach (float s in new[] { -0.38f, 0.38f })
                Deco(parent, PrimitiveType.Cube, "Strut", c + front * (depth / 2f + 0.45f) + across * (s * width) + up * (h1 - 0.5f), new Vector3(0.25f, 1.6f, 0.25f), iron,
                    rotF * Quaternion.Euler(40f, 0f, 0f), false);
            Deco(parent, PrimitiveType.Cube, "RoofPlate", c + front * (over / 2f) + up * (top + 0.15f), FrontBox(front, width + 0.2f, 0.3f, depth + over + 0.4f), roof, Quaternion.identity, false);
            // Sometimes a crooked top storey.
            if (h1Hash > 0.35f)
            {
                float h3 = 3f + h3Hash * 2f;
                Vector3 topC = c - front * (depth * 0.12f) + across * ((h2Hash - 0.5f) * width * 0.3f) + up * (top + h3 / 2f + 0.3f);
                Deco(parent, PrimitiveType.Cube, "Crooked", topC, new Vector3(width * 0.55f, h3, depth * 0.5f), wall, rotF * Quaternion.Euler(0f, 0f, (h2Hash - 0.5f) * 7f), false);
                Deco(parent, PrimitiveType.Cube, "CrookedRoof", topC + up * (h3 / 2f + 0.15f), new Vector3(width * 0.6f, 0.3f, depth * 0.56f), roof,
                    rotF * Quaternion.Euler(0f, 0f, (h2Hash - 0.5f) * 7f), false);
                // A porthole on it, facing the street.
                Deco(parent, PrimitiveType.Cylinder, "Porthole", topC + front * (depth * 0.25f + 0.04f), new Vector3(1f, 0.05f, 1f), window, rotF * Quaternion.Euler(90f, 0f, 0f), false);
            }
            // Rooftop: a water tank, a smokestack or a bundle of pipes.
            Vector3 roofSpot = c - front * (depth * 0.25f) - across * (width * 0.28f) + up * (top + 0.3f);
            float pick = g.Hash(r.xMin, r.yMin, 4);
            if (pick < 0.35f)
            {
                foreach (float lx in new[] { -0.6f, 0.6f })
                    foreach (float lz in new[] { -0.6f, 0.6f })
                        Deco(parent, PrimitiveType.Cube, "TankLeg", roofSpot + new Vector3(lx, 0.8f, lz), new Vector3(0.15f, 1.6f, 0.15f), iron, Quaternion.identity, false);
                Shape(parent, "WaterTank", CylMesh(), roofSpot + up * 2.8f, new Vector3(2.4f, 1.2f, 2.4f), roof, false, Quaternion.identity);
                Shape(parent, "TankCap", ConeMesh(), roofSpot + up * 4.3f, new Vector3(2.6f, 0.8f, 2.6f), brass, false, Quaternion.identity);
            }
            else if (pick < 0.7f)
            {
                float sh = 4f + h3Hash * 4f;
                Shape(parent, "Smokestack", CylMesh(), roofSpot + up * (sh / 2f), new Vector3(1.1f, sh / 2f, 1.1f), iron, false, Quaternion.identity);
                Shape(parent, "StackBand", CylMesh(), roofSpot + up * (sh * 0.7f), new Vector3(1.25f, 0.15f, 1.25f), brass, false, Quaternion.identity);
                Shape(parent, "StackEmber", CylMesh(), roofSpot + up * (sh + 0.05f), new Vector3(0.9f, 0.06f, 0.9f), PMat("FireboxGlow", new Color(1f, 0.5f, 0.2f), 1f, new Color(1f, 0.4f, 0.1f)),
                    false, Quaternion.identity);
                _stackTops.Add(roofSpot + up * (sh + 0.3f));
            }
            else
            {
                for (int k = 0; k < 3; k++)
                {
                    float ph = 1.5f + k * 0.9f;
                    Shape(parent, "RoofPipe", CylMesh(), roofSpot + across * (k * 0.7f) + up * (ph / 2f), new Vector3(0.45f, ph / 2f, 0.45f), brass, false, Quaternion.identity);
                }
                Shape(parent, "PipeElbow", CylMesh(), roofSpot + across * 0.7f + up * 3.4f + front * 0.6f, new Vector3(0.45f, 0.7f, 0.45f), brass, false, rotF * Quaternion.Euler(90f, 0f, 0f));
            }
            // Brass pipes up one front corner, with a valve wheel at head height.
            float side = h3Hash > 0.5f ? 1f : -1f;
            Vector3 pipeAt = c + front * (depth / 2f + 0.3f) + across * (side * (width / 2f - 0.4f));
            Shape(parent, "WallPipe", CylMesh(), pipeAt + up * (top / 2f), new Vector3(0.35f, top / 2f, 0.35f), brass, false, Quaternion.identity);
            Shape(parent, "Valve", CylMesh(), pipeAt + front * 0.25f + up * 1.6f, new Vector3(0.7f, 0.04f, 0.7f), PMat("ValveRed", new Color(0.7f, 0.15f, 0.12f), 1f), false,
                rotF * Quaternion.Euler(90f, 0f, 0f));

            // The front: a door, a striped awning, porthole windows and a neon blade sign.
            Vector3 faceC = c + front * (depth / 2f + 0.04f);
            Deco(parent, PrimitiveType.Cube, "Door", faceC + up * 1.4f, FrontBox(front, 1.6f, 2.8f, 0.1f), PMat("UnderDoor", new Color(0.25f, 0.16f, 0.12f), 1f), Quaternion.identity, false);
            Deco(parent, PrimitiveType.Cube, "DoorFrame", faceC + up * 2.95f, FrontBox(front, 2.1f, 0.3f, 0.16f), brass, Quaternion.identity, false);
            Material awning = UnderAwning(index);
            int stripes = 5;
            for (int k = 0; k < stripes; k++)
            {
                float t = (k + 0.5f) / stripes * 3.4f - 1.7f;
                Deco(parent, PrimitiveType.Cube, "Awning", faceC + front * 0.7f + across * t + up * 3.35f, new Vector3(3.4f / stripes, 0.08f, 1.6f),
                    k % 2 == 0 ? awning : PMat("AwningCream", new Color(0.85f, 0.8f, 0.68f), 1f), rotF * Quaternion.Euler(-20f, 0f, 0f), false);
            }
            int ports = Mathf.Max(1, Mathf.FloorToInt((width - 4f) / 3.2f));
            for (int k = 0; k < ports; k++)
            {
                float t = (k + 0.5f) / ports * (width - 4f) - (width - 4f) / 2f;
                if (Mathf.Abs(t) < 1.6f) t += Mathf.Sign(t == 0f ? 1f : t) * 1.8f;
                if (g.Hash(r.xMin + k, r.yMin, 12) < 0.25f) continue;
                Deco(parent, PrimitiveType.Cylinder, "Porthole", faceC + across * t + up * 2.2f, new Vector3(1.1f, 0.05f, 1.1f), window, rotF * Quaternion.Euler(90f, 0f, 0f), false);
                Deco(parent, PrimitiveType.Cylinder, "PortholeRim", faceC - front * 0.02f + across * t + up * 2.2f, new Vector3(1.35f, 0.04f, 1.35f), brass, rotF * Quaternion.Euler(90f, 0f, 0f), false);
                if (h2 > 4f)
                    Deco(parent, PrimitiveType.Cylinder, "Porthole", faceC + front * over + across * t + up * (h1 + h2 * 0.5f), new Vector3(1f, 0.05f, 1f), window,
                        rotF * Quaternion.Euler(90f, 0f, 0f), false);
            }
            // A neon blade sign standing out from the upper storey, lit on both sides.
            if (g.Hash(r.xMin, r.yMin, 5) > 0.3f)
            {
                Material neon = NeonMat(index + Mathf.FloorToInt(h1Hash * 7f));
                Vector3 signAt = c + front * (depth / 2f + over + 1.1f) + across * (-side * (width / 2f - 1f)) + up * (h1 + Mathf.Min(h2, 5f) * 0.5f + 0.4f);
                Quaternion blade = Quaternion.LookRotation(across);
                Deco(parent, PrimitiveType.Cube, "SignBoard", signAt, new Vector3(0.18f, 3.2f, 1.8f), iron, blade, false);
                Deco(parent, PrimitiveType.Cube, "SignNeon", signAt, new Vector3(0.26f, 2.6f, 1.3f), neon, blade, false);
                foreach (float s in new[] { -1f, 1f })
                    Deco(parent, PrimitiveType.Cube, "SignArm", signAt + front * (-0.6f) + up * (s * 1.2f), FrontBox(front, 0.12f, 0.12f, 1.4f), iron, Quaternion.identity, false);
            }
            else
            {
                // A neon strip along the shopfront instead.
                Deco(parent, PrimitiveType.Cube, "ShopNeon", faceC + front * 0.08f + up * (h1 - 0.5f), FrontBox(front, width * 0.7f, 0.22f, 0.1f), NeonMat(index), Quaternion.identity, false);
            }
        }

        /// <summary>Where a Bigdig map has a tree: a stalagmite (sometimes with glowing mushrooms) or a stack of brass pipes.</summary>
        private static void UndercityTree(Transform parent, ZoneGrid g, Vector2Int cell, Material rock, Material brass)
        {
            Vector3 p = g.Local(cell);
            float s = 0.8f + g.Hash(cell.x, cell.y, 4) * 0.6f;
            float pick = g.Hash(cell.x, cell.y, 5);
            if (pick < 0.62f)
            {
                float h = 5f * s + pick * 4f;
                Shape(parent, "Stalagmite", ConeMesh(), p + new Vector3(0, h / 2f, 0), new Vector3(2.6f * s, h, 2.6f * s), rock, false, Quaternion.Euler(0, pick * 300f, 0));
                Shape(parent, "Stalagmite", ConeMesh(), p + new Vector3(1.2f * s, h * 0.22f, 0.6f * s), new Vector3(1.3f * s, h * 0.45f, 1.3f * s), rock, false, Quaternion.identity);
                Deco(parent, PrimitiveType.Cylinder, "StalagmiteFoot", p + new Vector3(0, 1f, 0), new Vector3(1.6f * s, 1f, 1.6f * s), rock, Quaternion.identity, true);
                if (pick < 0.3f)
                {
                    Material shroom = NeonMat(1);
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 at = p + Quaternion.Euler(0, k * 125f + pick * 200f, 0) * new Vector3(1.5f * s, 0, 0);
                        float mh = 0.5f + k * 0.25f;
                        Deco(parent, PrimitiveType.Cylinder, "ShroomStem", at + new Vector3(0, mh / 2f, 0), new Vector3(0.14f, mh / 2f, 0.14f), PMat("ShroomStem", new Color(0.8f, 0.78f, 0.7f), 1f),
                            Quaternion.identity, false);
                        Deco(parent, PrimitiveType.Sphere, "ShroomCap", at + new Vector3(0, mh, 0), new Vector3(0.6f, 0.25f, 0.6f), shroom, Quaternion.identity, false);
                    }
                }
            }
            else
            {
                for (int k = 0; k < 3; k++)
                {
                    float ph = (2.5f + k * 1.4f) * s;
                    Vector3 at = p + Quaternion.Euler(0, k * 120f, 0) * new Vector3(0.55f, 0, 0);
                    Shape(parent, "PipeStack", CylMesh(), at + new Vector3(0, ph / 2f, 0), new Vector3(0.6f, ph / 2f, 0.6f), brass, false, Quaternion.identity);
                    Shape(parent, "PipeCap", CylMesh(), at + new Vector3(0, ph + 0.1f, 0), new Vector3(0.8f, 0.12f, 0.8f), UnderIron(), false, Quaternion.identity);
                }
                Shape(parent, "PipeValve", CylMesh(), p + new Vector3(0, 1.3f, -0.5f), new Vector3(0.8f, 0.05f, 0.8f), PMat("ValveRed", new Color(0.7f, 0.15f, 0.12f), 1f), false,
                    Quaternion.Euler(90f, 0f, 0f));
                var foot = Child(parent, "PipeStackFoot", p + new Vector3(0, 1f, 0)).gameObject.AddComponent<CapsuleCollider>();
                foot.radius = 0.9f;
                foot.height = 2f;
            }
        }

        // ------------------------------------------------------------------ past the map

        /// <summary>A shanty tower for the neighbourhood: 2-4 crooked boxes, lit windows, a neon sign, a tank or stack on top.</summary>
        private static void ShantyStack(Transform holder, Vector3 p, Quaternion face, float w, float d, float h, Material bld, Material roof, Material glow,
            System.Random rng, int neon)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int floors = Mathf.Clamp(Mathf.RoundToInt(h / 5f), 2, 4);
            float y = 0f, fw = w, fd = d;
            Vector3 drift = Vector3.zero;
            for (int f = 0; f < floors; f++)
            {
                float fh = h / floors * Rr(0.85f, 1.15f);
                Quaternion tilt = face * Quaternion.Euler(0f, Rr(-6f, 6f), Rr(-3f, 3f));
                BackBlock(holder, "Shanty", p + drift + new Vector3(0, y + fh / 2f, 0), new Vector3(fw, fh, fd), bld, tilt);
                BackBlock(holder, "ShantyLip", p + drift + new Vector3(0, y + fh, 0), new Vector3(fw + 0.8f, 0.4f, fd + 0.8f), roof, tilt);
                int cols = Mathf.Max(1, Mathf.FloorToInt(fw / 3f));
                for (int k = 0; k < cols; k++)
                {
                    if (rng.NextDouble() < 0.4) continue;
                    float x = (k + 0.5f) / cols * fw - fw / 2f;
                    BackBlock(holder, "ShantyWindow", p + drift + tilt * new Vector3(x, 0, fd / 2f + 0.04f) + new Vector3(0, y + fh * 0.55f, 0), new Vector3(1f, 1.1f, 0.06f), glow, tilt);
                }
                y += fh;
                fw *= Rr(0.7f, 0.95f);
                fd *= Rr(0.75f, 0.95f);
                drift += face * new Vector3(Rr(-1.5f, 1.5f), 0, Rr(-1f, 1f));
            }
            Vector3 topAt = p + drift + new Vector3(0, y, 0);
            if (rng.NextDouble() < 0.5)
            {
                float sh = Rr(4f, 9f);
                Shape(holder, "ShantyStack", CylMesh(), topAt + new Vector3(fw * 0.25f, sh / 2f, 0), new Vector3(1.4f, sh / 2f, 1.4f), UnderIron(), false, Quaternion.identity);
            }
            else Shape(holder, "ShantyTank", CylMesh(), topAt + new Vector3(0, 1.6f, 0), new Vector3(3f, 1.6f, 3f), roof, false, Quaternion.identity);
            if (rng.NextDouble() < 0.7)
            {
                // A neon sign on its face, big enough to read across the cavern.
                float sy = Rr(0.35f, 0.7f) * y;
                Vector3 at = p + face * new Vector3(Rr(-w * 0.2f, w * 0.2f), 0, d / 2f + 0.3f) + new Vector3(0, sy, 0);
                BackBlock(holder, "BillboardBack", at, new Vector3(Mathf.Min(w * 0.8f, 7f), 2.2f, 0.2f), UnderIron(), face);
                BackBlock(holder, "BillboardNeon", at + face * new Vector3(0, 0, 0.14f), new Vector3(Mathf.Min(w * 0.7f, 6.2f), 1.5f, 0.08f), NeonMat(neon), face);
            }
        }

        /// <summary>One piece of Bigdig's neighbourhood (rows 0 and 1 round the map).</summary>
        private static void UndercityBackdropProp(Transform holder, Vector3 p, Quaternion face, bool back, System.Random rng,
            Material bld, Material roof, Material glow, Material rock)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float w = back ? Rr(9f, 14f) : Rr(7f, 11f), d = Rr(7f, 10f), h = back ? Rr(14f, 24f) : Rr(9f, 16f);
            ShantyStack(holder, p, face, w, d, h, bld, roof, glow, rng, rng.Next(0, 6));
            if (rng.NextDouble() < 0.25)
                Shape(holder, "Stalagmite", ConeMesh(), p + face * new Vector3(Rr(-6f, 6f), 0, -7f) + new Vector3(0, 5f, 0), new Vector3(Rr(3f, 5f), Rr(8f, 14f), Rr(3f, 5f)), rock, false,
                    Quaternion.identity);
        }

        /// <summary>The far row: Bigdig's skyline of smokestacks, cranes, gear towers and neon billboards.</summary>
        private static void UndercitySkylineProp(Transform holder, Vector3 p, System.Random rng, Material bld, Material roof, Material glow)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material iron = UnderIron(), brass = UnderBrass();
            double roll = rng.NextDouble();
            if (roll < 0.3)
            {
                // A foundry with a cluster of smokestacks, glowing at the top.
                BackBlock(holder, "Foundry", p + new Vector3(0, 5f, 0), new Vector3(Rr(11f, 15f), 10f, Rr(9f, 12f)), bld, Quaternion.Euler(0, Rr(0f, 90f), 0));
                int stacks = rng.Next(2, 5);
                for (int k = 0; k < stacks; k++)
                {
                    float h = Rr(18f, 30f);
                    Vector3 at = p + new Vector3(-4f + k * 2.8f, 0, Rr(-2f, 2f));
                    Shape(holder, "Smokestack", CylMesh(), at + new Vector3(0, h / 2f, 0), new Vector3(2f, h / 2f, 2f), iron, false, Quaternion.identity);
                    Shape(holder, "StackBand", CylMesh(), at + new Vector3(0, h * 0.75f, 0), new Vector3(2.3f, 0.3f, 2.3f), brass, false, Quaternion.identity);
                    Shape(holder, "StackGlow", CylMesh(), at + new Vector3(0, h + 0.1f, 0), new Vector3(1.6f, 0.1f, 1.6f), PMat("FireboxGlow", new Color(1f, 0.5f, 0.2f), 1f, new Color(1f, 0.4f, 0.1f)),
                        false, Quaternion.identity);
                }
            }
            else if (roll < 0.5)
            {
                // A crane: a lattice mast, a long jib and a hook on a chain.
                float h = Rr(24f, 34f);
                float yaw = Rr(0f, 360f);
                Quaternion rot = Quaternion.Euler(0, yaw, 0);
                BackBlock(holder, "CraneMast", p + new Vector3(0, h / 2f, 0), new Vector3(1.6f, h, 1.6f), iron, rot);
                for (float y = 3f; y < h; y += 4f)
                    BackBlock(holder, "CraneBrace", p + new Vector3(0, y, 0), new Vector3(1.9f, 0.25f, 1.9f), brass, rot);
                float jib = Rr(14f, 22f);
                BackBlock(holder, "CraneJib", p + rot * new Vector3(0, 0, jib * 0.35f) + new Vector3(0, h + 0.6f, 0), new Vector3(1.1f, 1.1f, jib), iron, rot);
                BackBlock(holder, "CraneCab", p + new Vector3(0, h - 1.2f, 0), new Vector3(2.6f, 2.2f, 2.6f), roof, rot);
                float hang = Rr(6f, 12f);
                Vector3 tip = p + rot * new Vector3(0, 0, jib * 0.8f) + new Vector3(0, h, 0);
                BackBlock(holder, "CraneChain", tip - new Vector3(0, hang / 2f, 0), new Vector3(0.2f, hang, 0.2f), iron, rot);
                BackBlock(holder, "CraneLoad", tip - new Vector3(0, hang + 1.2f, 0), new Vector3(3f, 2.4f, 2.2f), brass, rot);
            }
            else if (roll < 0.7)
            {
                // A tower with a great gear wheel turned edge-on to the street.
                float h = Rr(16f, 24f), r = Rr(5f, 8f);
                Quaternion rot = Quaternion.Euler(0, Rr(0f, 360f), 0);
                BackBlock(holder, "GearTower", p + new Vector3(0, h / 2f, 0), new Vector3(5f, h, 5f), bld, rot);
                Vector3 hub = p + new Vector3(0, h + r * 0.6f, 0);
                Shape(holder, "Gear", CylMesh(), hub, new Vector3(r * 2f, 0.5f, r * 2f), brass, false, rot * Quaternion.Euler(90f, 0f, 0f));
                Shape(holder, "GearHub", CylMesh(), hub, new Vector3(r * 0.6f, 0.8f, r * 0.6f), iron, false, rot * Quaternion.Euler(90f, 0f, 0f));
                const int teeth = 12;
                for (int k = 0; k < teeth; k++)
                {
                    Quaternion spin = rot * Quaternion.Euler(0f, 0f, k * 360f / teeth);
                    BackBlock(holder, "GearTooth", hub + spin * new Vector3(0, r + 0.5f, 0), new Vector3(1.2f, 1.4f, 0.9f), brass, spin);
                }
            }
            else
            {
                // A tall shanty tower carrying a huge neon billboard on its roof.
                float h = Rr(18f, 28f), w = Rr(8f, 11f);
                Quaternion rot = Quaternion.Euler(0, Rr(0f, 360f), 0);
                ShantyStack(holder, p, rot, w, Rr(7f, 9f), h, bld, roof, glow, rng, rng.Next(0, 6));
                Material neon = NeonMat(rng.Next(0, 6));
                Vector3 board = p + new Vector3(0, h + 5f, 0);
                foreach (float s in new[] { -1f, 1f })
                    BackBlock(holder, "BillboardLeg", p + rot * new Vector3(s * w * 0.3f, 0, 0) + new Vector3(0, h + 1.5f, 0), new Vector3(0.4f, 3f, 0.4f), iron, rot);
                BackBlock(holder, "Billboard", board, new Vector3(w * 1.3f, 5f, 0.3f), iron, rot);
                BackBlock(holder, "BillboardNeon", board, new Vector3(w * 1.2f, 4.2f, 0.42f), neon, rot);
                BackBlock(holder, "BillboardFrame", board + new Vector3(0, 2.6f, 0), new Vector3(w * 1.35f, 0.3f, 0.5f), brass, rot);
            }
        }

        /// <summary>
        /// The cavern round a Bigdig street: rock pillars from the chasm to the roof with shanties on them, then the walls
        /// and roof (lumpy rock, inward-facing, no shadows) a long way out past the neighbourhood, hung with stalactites,
        /// chains and lanterns, with brass mains along the walls. 'poly' is the shelf the street stands on.
        /// </summary>
        private static void CavernShell(Transform root, Vector2[] poly, float floorY, float shelfTop, float rise, System.Random rng, Material rock, Material bld,
            Material roof, Material glow, string key)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var holder = Child(root, "Cavern", Vector3.zero);
            Material iron = UnderIron(), brass = UnderBrass();
            // The perimeter: the shelf's outline pushed far out, finely divided.
            var perim = new List<Vector2>();
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 16f));
                for (int s = 0; s < steps; s++) perim.Add(Vector2.Lerp(a, b, s / (float)steps));
            }
            const float push = 80f;
            int n = perim.Count;
            var outer = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = perim[i].sqrMagnitude > 0.01f ? perim[i].normalized : Vector2.up;
                outer[i] = perim[i] + dir * push;
            }
            float wallTop = shelfTop + rise, crown = shelfTop + rise * 1.3f;
            const int wallRows = 6, roofRows = 7;
            float noiseSeed = Rr(0f, 100f);
            Vector3 P(int i, int row)
            {
                int k = ((i % n) + n) % n;
                Vector2 o = outer[k];
                float bump = Mathf.PerlinNoise(noiseSeed + k * 0.37f, row * 0.53f) - 0.5f;
                if (row <= wallRows)
                {
                    float t = row / (float)wallRows;
                    Vector2 xz = o + o.normalized * (bump * 14f) - o.normalized * (Mathf.Sin(t * Mathf.PI) * 10f);
                    return new Vector3(xz.x, Mathf.Lerp(floorY - 2f, wallTop, t), xz.y);
                }
                float u = (row - wallRows) / (float)roofRows;   // 0 at the wall's top .. 1 at the crown
                float shrink = Mathf.Max(0.02f, 1f - u);
                Vector2 rxz = o * shrink;
                float y = Mathf.Lerp(wallTop, crown, Mathf.Sin(u * Mathf.PI * 0.5f)) + bump * 12f;
                return new Vector3(rxz.x, y, rxz.y);
            }
            var mb = new MeshBuilder();
            for (int row = 0; row < wallRows + roofRows; row++)
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = P(i, row), b = P(i + 1, row), c2 = P(i + 1, row + 1), d = P(i, row + 1);
                    Vector3 mid = (a + b + c2 + d) * 0.25f;
                    Vector3 inward = new Vector3(-mid.x, row < wallRows ? 0f : -Mathf.Abs(mid.y) * 0.02f - 1f, -mid.z);
                    mb.Quad(a, b, c2, d, inward);
                }
            var shell = Shape(holder, "CavernRock", SaveBuiltMesh("PSX_Cavern_" + key, mb), Vector3.zero, Vector3.one, rock, false, Quaternion.identity);
            NoShadows(shell);

            float RoofAt(float shrink) { return Mathf.Lerp(wallTop, crown, Mathf.Sin((1f - shrink) * Mathf.PI * 0.5f)); }
            // Stalactites, chains with lanterns, and a few hanging neon boards.
            for (int k = 0; k < 46; k++)
            {
                int i = rng.Next(0, n);
                float shrink = Rr(0.12f, 0.92f);
                Vector2 xz = outer[i] * shrink;
                float ry = RoofAt(shrink) - 4f;
                if (k < 30)
                {
                    float len = Rr(8f, 26f), wd = Rr(3f, 7f);
                    Shape(holder, "Stalactite", ConeMesh(), new Vector3(xz.x, ry - len / 2f, xz.y), new Vector3(wd, len, wd), rock, false, Quaternion.Euler(180f, Rr(0f, 360f), 0f));
                }
                else if (k < 42)
                {
                    float drop = Rr(25f, 55f);
                    Shape(holder, "Chain", CylMesh(), new Vector3(xz.x, ry - drop / 2f, xz.y), new Vector3(0.35f, drop / 2f, 0.35f), iron, false, Quaternion.identity);
                    Shape(holder, "Lantern", GemMesh(), new Vector3(xz.x, ry - drop - 1.4f, xz.y), new Vector3(2.4f, 3f, 2.4f), glow, false, Quaternion.identity);
                    Shape(holder, "LanternCap", ConeMesh(), new Vector3(xz.x, ry - drop + 0.4f, xz.y), new Vector3(3f, 1.4f, 3f), brass, false, Quaternion.identity);
                }
                else
                {
                    float drop = Rr(30f, 45f);
                    Quaternion yaw = Quaternion.Euler(0f, Rr(0f, 360f), 0f);
                    foreach (float s in new[] { -3f, 3f })
                    {
                        Vector3 top = new Vector3(xz.x, ry, xz.y) + yaw * new Vector3(s, 0, 0);
                        Shape(holder, "BoardChain", CylMesh(), top - new Vector3(0, drop / 2f, 0), new Vector3(0.25f, drop / 2f, 0.25f), iron, false, Quaternion.identity);
                    }
                    Vector3 boardAt = new Vector3(xz.x, ry - drop - 2.5f, xz.y);
                    BackBlock(holder, "HangingBoard", boardAt, new Vector3(9f, 5f, 0.3f), iron, yaw);
                    BackBlock(holder, "HangingNeon", boardAt, new Vector3(8f, 4f, 0.45f), NeonMat(k), yaw);
                }
            }
            // Brass mains running round the walls at two heights.
            foreach (float hFrac in new[] { 0.32f, 0.55f })
                for (int i = 0; i < n; i += 1)
                {
                    Vector3 a = P(i, 0), b = P(i + 1, 0);
                    a.y = b.y = Mathf.Lerp(floorY, wallTop, hFrac);
                    Vector3 inA = new Vector3(-a.x, 0, -a.z).normalized * 14f, inB = new Vector3(-b.x, 0, -b.z).normalized * 14f;
                    a += inA; b += inB;
                    Vector3 mid = (a + b) * 0.5f;
                    float len = (b - a).magnitude;
                    if (len < 0.5f) continue;
                    Shape(holder, "Main", CylMesh(), mid, new Vector3(2.4f, len / 2f + 0.6f, 2.4f), brass, false, Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
                    if (i % 3 == 0) Shape(holder, "MainFlange", CylMesh(), a, new Vector3(3.2f, 0.4f, 3.2f), iron, false, Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
                }
            // Rock pillars from the chasm floor to the roof, out past the neighbourhood, with shanties clinging to them.
            for (int k = 0; k < 7; k++)
            {
                int i = (int)(n * (k + Rr(0.2f, 0.8f)) / 7f) % n;
                Vector2 dir = perim[i].normalized;
                Vector2 xz = perim[i] + dir * Rr(18f, 38f);
                float shrink = xz.magnitude / Mathf.Max(1f, outer[i].magnitude);
                float roofY = RoofAt(Mathf.Clamp01(shrink));
                float r = Rr(7f, 11f);
                float h = roofY - floorY + 6f;
                Shape(holder, "Pillar", CylMesh(), new Vector3(xz.x, floorY + h / 2f - 3f, xz.y), new Vector3(r * 2f, h / 2f, r * 2f), rock, false, Quaternion.Euler(0, Rr(0f, 360f), 0));
                for (int b2 = 0; b2 < 3; b2++)
                    Shape(holder, "PillarBulge", BlobMesh(), new Vector3(xz.x, Mathf.Lerp(floorY, roofY, Rr(0.15f, 0.85f)), xz.y), new Vector3(r * 2.6f, Rr(8f, 16f), r * 2.6f), rock, false,
                        Quaternion.Euler(0, Rr(0f, 360f), 0));
                float ringY = Mathf.Lerp(shelfTop, roofY, Rr(0.15f, 0.35f));
                Shape(holder, "PillarWalk", CylMesh(), new Vector3(xz.x, ringY, xz.y), new Vector3(r * 2f + 6f, 0.4f, r * 2f + 6f), iron, false, Quaternion.identity);
                int huts = rng.Next(3, 6);
                for (int hk = 0; hk < huts; hk++)
                {
                    float ang = hk * 360f / huts + Rr(-15f, 15f);
                    Quaternion face = Quaternion.Euler(0f, ang, 0f);
                    Vector3 at = new Vector3(xz.x, ringY + 0.3f, xz.y) + face * new Vector3(0, 0, r + 1.8f);
                    ShantyStack(holder, at, face, Rr(4f, 6f), Rr(3.5f, 5f), Rr(7f, 12f), bld, roof, glow, rng, hk + k);
                }
            }
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            MergeRenderers(holder, "CavernMesh");
            NoShadows(holder.gameObject);
        }

        /// <summary>The chasm floor far below a Bigdig street: oily grit with pools of glowing sludge.</summary>
        private static void ChasmFloor(Transform root, float W, float H, float floorY, System.Random rng, string key)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material grit = PsxTexMat("ChasmFloor", new Color(0.32f, 0.3f, 0.28f), TileTexture(TerrainTexDir + "/Sludge.png"), 6f);
            Shape(root, "ChasmFloor", GridMesh(8, 8, false), new Vector3(0, floorY, 0), new Vector3(W + 900f, 1f, H + 900f), grit, false, Quaternion.identity);
            var holder = Child(root, "ChasmPools", Vector3.zero);
            Material slime = PMat("SludgeGlow", new Color(0.35f, 0.85f, 0.25f), 1f, new Color(0.3f, 0.9f, 0.2f));
            float reach = Mathf.Max(W, H) * 0.5f + 160f;
            for (int k = 0; k < 26; k++)
            {
                float ang = Rr(0f, Mathf.PI * 2f), dist = Rr(Mathf.Max(W, H) * 0.5f + 30f, reach);
                Vector3 at = new Vector3(Mathf.Cos(ang) * dist, floorY + 0.08f, Mathf.Sin(ang) * dist);
                Shape(holder, "SludgePool", CylMesh(), at, new Vector3(Rr(8f, 26f), 0.05f, Rr(8f, 26f)), slime, false, Quaternion.Euler(0, Rr(0f, 360f), 0));
            }
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            MergeRenderers(holder, "ChasmPoolMesh");
            NoShadows(holder.gameObject);
        }

        private static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------ points

        /// <summary>The way a point's prop faces: towards an open neighbouring cell (else south).</summary>
        private static Quaternion FacingOpen(ZoneGrid g, Vector2Int cell)
        {
            foreach (var d in ZoneGrid.Dirs)
            {
                char ch = g.At(cell.x + d.x, cell.y + d.y);
                if (g.Walkable(cell.x + d.x, cell.y + d.y) && ch != g.At(cell.x, cell.y)) return Quaternion.LookRotation(ZoneGrid.ToLocalDir(d));
            }
            return Quaternion.LookRotation(Vector3.back);
        }

        /// <summary>Bigdig's point props: neon signs, blasting barrels, gold, slot machines, card tables and roulette wheels.</summary>
        private static GameObject UndercityPointLook(Transform holder, ZoneGrid g, Vector2Int cell, ZonePoint p, Material wood, ref float labelY)
        {
            float hash = g.Hash(cell.x, cell.y, 71);
            Material iron = UnderIron(), brass = UnderBrass(), gold = UnderGold();
            Quaternion face = FacingOpen(g, cell);
            switch (p.look)
            {
                case ZonePointLook.NeonSign:
                {
                    // A sign on two iron posts: an iron board, a neon frame and two glowing bars.
                    Material neon = NeonMat(cell.x * 3 + cell.y);
                    Material neon2 = NeonMat(cell.x * 3 + cell.y + 2);
                    foreach (float s in new[] { -1.3f, 1.3f })
                        Deco(holder, PrimitiveType.Cylinder, "SignPost", face * new Vector3(s, 0, 0) + new Vector3(0, 1.8f, 0), new Vector3(0.18f, 1.8f, 0.18f), iron, Quaternion.identity, s < 0f);
                    var board = Deco(holder, PrimitiveType.Cube, "SignBoard", new Vector3(0, 3.6f, 0), new Vector3(3.2f, 1.6f, 0.2f), iron, face, true);
                    Deco(holder, PrimitiveType.Cube, "NeonTop", face * new Vector3(0, 0.75f, 0.13f) + new Vector3(0, 3.6f, 0), new Vector3(3.1f, 0.1f, 0.06f), neon, face, false);
                    Deco(holder, PrimitiveType.Cube, "NeonBottom", face * new Vector3(0, -0.75f, 0.13f) + new Vector3(0, 3.6f, 0), new Vector3(3.1f, 0.1f, 0.06f), neon, face, false);
                    foreach (float s in new[] { -1.55f, 1.55f })
                        Deco(holder, PrimitiveType.Cube, "NeonSide", face * new Vector3(s, 0, 0.13f) + new Vector3(0, 3.6f, 0), new Vector3(0.1f, 1.6f, 0.06f), neon, face, false);
                    Deco(holder, PrimitiveType.Cube, "NeonBar", face * new Vector3(-0.2f, 0.25f, 0.13f) + new Vector3(0, 3.6f, 0), new Vector3(2.2f, 0.32f, 0.06f), neon2, face, false);
                    Deco(holder, PrimitiveType.Cube, "NeonBar", face * new Vector3(0.35f, -0.25f, 0.13f) + new Vector3(0, 3.6f, 0), new Vector3(1.6f, 0.32f, 0.06f), neon, face, false);
                    labelY = 4.8f;
                    return board;
                }
                case ZonePointLook.BlastBarrel:
                {
                    // Red blasting barrels with a yellow band, a fuse on top, and a crate of blasting caps.
                    Material red = PMat("BlastRed", new Color(0.75f, 0.14f, 0.1f), 1f);
                    Material yellow = PMat("BlastYellow", new Color(0.95f, 0.78f, 0.15f), 1f);
                    int count = 2 + (hash > 0.5f ? 1 : 0);
                    GameObject first = null;
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 at = Quaternion.Euler(0, k * 120f + hash * 90f, 0) * new Vector3(0, 0, 0.9f);
                        var b = Deco(holder, PrimitiveType.Cylinder, "BlastBarrel", at + new Vector3(0, 0.6f, 0), new Vector3(0.85f, 0.6f, 0.85f), red, Quaternion.identity, true);
                        Deco(holder, PrimitiveType.Cylinder, "BlastBand", at + new Vector3(0, 0.65f, 0), new Vector3(0.88f, 0.12f, 0.88f), yellow, Quaternion.identity, false);
                        Deco(holder, PrimitiveType.Cylinder, "Fuse", at + new Vector3(0.15f, 1.35f, 0), new Vector3(0.05f, 0.15f, 0.05f), iron, Quaternion.Euler(0, 0, 20f), false);
                        if (first == null) first = b;
                    }
                    Deco(holder, PrimitiveType.Cube, "CapCrate", new Vector3(-1.1f, 0.35f, -0.6f), new Vector3(0.9f, 0.7f, 0.7f), wood, Quaternion.Euler(0, 25f, 0), true);
                    labelY = 2f;
                    return first;
                }
                case ZonePointLook.GoldPile:
                {
                    // A heap of singing gold: nuggets, a few gems, a pick stuck in the top.
                    var heap = Deco(holder, PrimitiveType.Sphere, "GoldHeap", new Vector3(0, 0.3f, 0), new Vector3(2.6f, 1.2f, 2.4f), gold, Quaternion.identity, true);
                    for (int k = 0; k < 7; k++)
                    {
                        Vector3 at = Quaternion.Euler(0, k * 51f + hash * 80f, 0) * new Vector3(0.7f + (k % 3) * 0.25f, 0, 0);
                        Deco(holder, PrimitiveType.Sphere, "Nugget", at + new Vector3(0, 0.55f + (k % 2) * 0.2f, 0), new Vector3(0.6f, 0.4f, 0.5f), gold, Quaternion.Euler(k * 30f, k * 47f, 0), false);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 at = Quaternion.Euler(0, k * 120f + 30f, 0) * new Vector3(0.9f, 0, 0);
                        Shape(holder, "Gem", GemMesh(), at + new Vector3(0, 0.75f, 0), new Vector3(0.35f, 0.5f, 0.35f), NeonMat(k * 2 + 1), false, Quaternion.Euler(20f, k * 40f, 0));
                    }
                    Deco(holder, PrimitiveType.Cube, "PickHaft", new Vector3(0.2f, 1.4f, 0), new Vector3(0.1f, 1.3f, 0.1f), wood, Quaternion.Euler(0, 0, -18f), false);
                    Deco(holder, PrimitiveType.Cube, "PickHead", new Vector3(0.42f, 2f, 0), new Vector3(1.1f, 0.14f, 0.14f), iron, Quaternion.Euler(0, 0, -18f), false);
                    labelY = 2.6f;
                    return heap;
                }
                case ZonePointLook.SlotMachine:
                {
                    // A cabinet with a glowing screen, three reels, a lever with a red knob and a light on top.
                    Material body = PMat("SlotBody", new Color(0.65f, 0.12f, 0.15f), 1f);
                    Material screen = NeonMat(1);
                    var cab = Deco(holder, PrimitiveType.Cube, "SlotCabinet", new Vector3(0, 1f, 0), new Vector3(1f, 2f, 0.8f), body, face, true);
                    Deco(holder, PrimitiveType.Cube, "SlotTrim", new Vector3(0, 2.05f, 0), new Vector3(1.1f, 0.12f, 0.9f), gold, face, false);
                    Deco(holder, PrimitiveType.Cube, "SlotScreen", face * new Vector3(0, 0, 0.41f) + new Vector3(0, 1.45f, 0), new Vector3(0.8f, 0.55f, 0.03f), screen, face, false);
                    for (int k = -1; k <= 1; k++)
                        Deco(holder, PrimitiveType.Cube, "SlotReel", face * new Vector3(k * 0.24f, 0, 0.43f) + new Vector3(0, 1.45f, 0), new Vector3(0.18f, 0.36f, 0.03f),
                            PMat("SlotReel", new Color(0.95f, 0.92f, 0.85f), 1f, new Color(0.3f, 0.28f, 0.24f)), face, false);
                    Deco(holder, PrimitiveType.Cube, "SlotTray", face * new Vector3(0, 0, 0.45f) + new Vector3(0, 0.75f, 0), new Vector3(0.7f, 0.12f, 0.2f), gold, face, false);
                    Deco(holder, PrimitiveType.Cylinder, "SlotLever", face * new Vector3(0.6f, 0, 0) + new Vector3(0, 1.5f, 0), new Vector3(0.07f, 0.4f, 0.07f), brass, face, false);
                    Deco(holder, PrimitiveType.Sphere, "SlotKnob", face * new Vector3(0.6f, 0, 0) + new Vector3(0, 1.95f, 0), Vector3.one * 0.2f, PMat("BlastRed", new Color(0.75f, 0.14f, 0.1f), 1f),
                        Quaternion.identity, false);
                    Deco(holder, PrimitiveType.Sphere, "SlotLight", new Vector3(0, 2.3f, 0), new Vector3(0.35f, 0.3f, 0.35f), NeonMat(2 + Mathf.FloorToInt(hash * 4f)), Quaternion.identity, false);
                    labelY = 2.9f;
                    return cab;
                }
                case ZonePointLook.CardTable:
                {
                    // A round green-baize table with a wooden rim, stools, cards and stacks of chips.
                    Material felt = PMat("Baize", new Color(0.1f, 0.42f, 0.2f), 1f);
                    var table = Deco(holder, PrimitiveType.Cylinder, "CardTable", new Vector3(0, 0.95f, 0), new Vector3(3f, 0.06f, 3f), felt, Quaternion.identity, true);
                    Deco(holder, PrimitiveType.Cylinder, "TableRim", new Vector3(0, 0.93f, 0), new Vector3(3.3f, 0.08f, 3.3f), wood, Quaternion.identity, false);
                    Deco(holder, PrimitiveType.Cylinder, "TableFoot", new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), wood, Quaternion.identity, false);
                    Material card = PMat("PlayingCard", new Color(0.95f, 0.94f, 0.9f), 1f);
                    for (int k = 0; k < 5; k++)
                        Deco(holder, PrimitiveType.Cube, "Card", new Vector3(-0.5f + k * 0.25f, 1.02f, 0.1f * (k % 2)), new Vector3(0.18f, 0.01f, 0.26f), card, Quaternion.Euler(0, k * 12f - 20f, 0), false);
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 at = Quaternion.Euler(0, k * 90f + 45f, 0) * new Vector3(0, 0, 1.05f);
                        Deco(holder, PrimitiveType.Cylinder, "Chips", at + new Vector3(0, 1.08f, 0), new Vector3(0.18f, 0.06f + k * 0.02f, 0.18f), NeonMat(k), Quaternion.identity, false);
                        Vector3 st = Quaternion.Euler(0, k * 90f, 0) * new Vector3(0, 0, 2.1f);
                        Deco(holder, PrimitiveType.Cylinder, "Stool", st + new Vector3(0, 0.38f, 0), new Vector3(0.5f, 0.38f, 0.5f), PMat("StoolVelvet", new Color(0.55f, 0.1f, 0.15f), 1f),
                            Quaternion.identity, false);
                    }
                    labelY = 2.2f;
                    return table;
                }
                case ZonePointLook.Roulette:
                {
                    // A roulette table: a gold-rimmed wheel of red and black pockets on a wooden bowl, and a betting cloth.
                    var bowl = Deco(holder, PrimitiveType.Cylinder, "RouletteBowl", new Vector3(0, 0.55f, 0), new Vector3(2.2f, 0.55f, 2.2f), wood, Quaternion.identity, true);
                    Deco(holder, PrimitiveType.Cylinder, "RouletteRim", new Vector3(0, 1.12f, 0), new Vector3(2.3f, 0.04f, 2.3f), gold, Quaternion.identity, false);
                    Material red = PMat("RouletteRed", new Color(0.7f, 0.1f, 0.12f), 1f), black = PMat("RouletteBlack", new Color(0.08f, 0.07f, 0.08f), 1f);
                    const int pockets = 16;
                    for (int k = 0; k < pockets; k++)
                    {
                        Quaternion q = Quaternion.Euler(0, k * 360f / pockets + hash * 30f, 0);
                        Deco(holder, PrimitiveType.Cube, "Pocket", q * new Vector3(0, 0, 0.72f) + new Vector3(0, 1.13f, 0), new Vector3(0.26f, 0.03f, 0.55f), k % 2 == 0 ? red : black, q, false);
                    }
                    Deco(holder, PrimitiveType.Cylinder, "RouletteHub", new Vector3(0, 1.2f, 0), new Vector3(0.5f, 0.08f, 0.5f), gold, Quaternion.identity, false);
                    Deco(holder, PrimitiveType.Cylinder, "RouletteSpindle", new Vector3(0, 1.35f, 0), new Vector3(0.08f, 0.15f, 0.08f), gold, Quaternion.identity, false);
                    Deco(holder, PrimitiveType.Sphere, "RouletteBall", new Vector3(0.6f, 1.2f, 0.3f), Vector3.one * 0.09f, PMat("PlayingCard", new Color(0.95f, 0.94f, 0.9f), 1f), Quaternion.identity, false);
                    Vector3 cloth = face * new Vector3(0, 0, -2.1f);
                    Deco(holder, PrimitiveType.Cube, "BettingTable", cloth + new Vector3(0, 0.5f, 0), new Vector3(2.6f, 1f, 1.4f), wood, face, true);
                    Deco(holder, PrimitiveType.Cube, "BettingCloth", cloth + new Vector3(0, 1.01f, 0), new Vector3(2.4f, 0.02f, 1.2f), PMat("Baize", new Color(0.1f, 0.42f, 0.2f), 1f), face, false);
                    for (int k = 0; k < 6; k++)
                        Deco(holder, PrimitiveType.Cube, "BetSquare", cloth + face * new Vector3(-1f + k * 0.4f, 0, 0.2f * (k % 2 == 0 ? 1 : -1)) + new Vector3(0, 1.03f, 0),
                            new Vector3(0.32f, 0.01f, 0.32f), k % 2 == 0 ? red : black, face, false);
                    labelY = 2.4f;
                    return bowl;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ dungeons

        /// <summary>Dresses a Bigdig dungeon's inside: the casino in red and gold, the siphon works in pipes and warning lamps.</summary>
        private static void UndercityDungeon(Transform parent, ZoneGrid g, ZoneDefinition z, HashSet<Vector2Int> inside, HashSet<Vector2Int> stairCells)
        {
            if (inside == null || inside.Count == 0 || z.landmarks == null) return;
            string label = "";
            foreach (var l in z.landmarks) if (l != null && l.kind == LandmarkKind.DungeonSigil) label = l.label ?? "";
            bool casino = label.IndexOf("Casino", System.StringComparison.OrdinalIgnoreCase) >= 0;
            float cs = g.cell;
            var holder = Child(parent, casino ? "CasinoDress" : "WorksDress", Vector3.zero);
            Material gold = UnderGold(), brass = UnderBrass(), iron = UnderIron();
            Material carpet = PsxTexMat("CasinoCarpet", new Color(0.55f, 0.08f, 0.12f), TileTexture(StreetTexDir + "/Velvet.png"), 1.6f);
            Material velvet = PsxTexMat("CasinoWall", new Color(0.4f, 0.06f, 0.1f), TileTexture(StreetTexDir + "/Velvet.png"), 2f);
            Material warn = PMat("WarnLamp", new Color(1f, 0.2f, 0.15f), 1f, new Color(1f, 0.15f, 0.1f));
            int n = 0;
            foreach (var cell in inside)
            {
                Vector3 c = g.Local(cell);
                float floor = g.Elevation(cell.x, cell.y) + Lift;
                c.y = floor;
                if (casino && !stairCells.Contains(cell))
                    Deco(holder, PrimitiveType.Cube, "Carpet", c + new Vector3(0, 0.025f, 0), new Vector3(cs, 0.05f, cs), carpet, Quaternion.identity, false);
                foreach (var d in ZoneGrid.Dirs)
                {
                    var nb = cell + d;
                    if (inside.Contains(nb) || g.Walkable(nb.x, nb.y)) continue;
                    // A wall of the dungeon (the next cell is a building or wall).
                    Vector3 dir = ZoneGrid.ToLocalDir(d);
                    Quaternion rot = Quaternion.LookRotation(-dir);
                    Vector3 wallAt = c + dir * (cs * 0.5f - 0.15f);
                    if (casino)
                    {
                        Deco(holder, PrimitiveType.Cube, "VelvetWall", wallAt + new Vector3(0, 4.5f, 0), new Vector3(cs, 9f, 0.25f), velvet, rot, false);
                        Deco(holder, PrimitiveType.Cube, "GoldSkirting", wallAt - dir * 0.08f + new Vector3(0, 0.3f, 0), new Vector3(cs, 0.6f, 0.15f), gold, rot, false);
                        Deco(holder, PrimitiveType.Cube, "GoldCornice", wallAt - dir * 0.1f + new Vector3(0, 8.9f, 0), new Vector3(cs, 0.4f, 0.3f), gold, rot, false);
                        Deco(holder, PrimitiveType.Cube, "CasinoNeon", wallAt - dir * 0.16f + new Vector3(0, 8.3f, 0), new Vector3(cs, 0.14f, 0.06f), NeonMat(0), rot, false);
                        if ((cell.x + cell.y) % 2 == 0)
                            Deco(holder, PrimitiveType.Cube, "GoldPilaster", wallAt - dir * 0.14f + new Vector3(0, 4.5f, 0) + Vector3.Cross(Vector3.up, dir) * (cs * 0.5f - 0.3f),
                                new Vector3(0.6f, 9f, 0.3f), gold, rot, false);
                    }
                    else
                    {
                        Vector3 along = Vector3.Cross(Vector3.up, dir);
                        Shape(holder, "WorksPipe", CylMesh(), wallAt - dir * 0.4f + new Vector3(0, 4.2f, 0), new Vector3(0.8f, cs * 0.5f + 0.1f, 0.8f), brass, false,
                            Quaternion.FromToRotation(Vector3.up, along));
                        Shape(holder, "WorksPipe", CylMesh(), wallAt - dir * 0.35f + new Vector3(0, 5.3f, 0), new Vector3(0.5f, cs * 0.5f + 0.1f, 0.5f), iron, false,
                            Quaternion.FromToRotation(Vector3.up, along));
                        if ((cell.x * 7 + cell.y * 3) % 5 == 0)
                        {
                            Deco(holder, PrimitiveType.Cube, "LampBracket", wallAt - dir * 0.3f + new Vector3(0, 3.2f, 0), new Vector3(0.2f, 0.2f, 0.6f), iron, rot, false);
                            Deco(holder, PrimitiveType.Sphere, "WarnLamp", wallAt - dir * 0.6f + new Vector3(0, 3.2f, 0), Vector3.one * 0.45f, warn, Quaternion.identity, false);
                        }
                    }
                }
                // Chandeliers over the casino floor, on a loose grid.
                if (casino && cell.x % 4 == 1 && cell.y % 4 == 1)
                {
                    Vector3 top = c + new Vector3(0, 11f, 0);
                    Deco(holder, PrimitiveType.Cylinder, "ChandelierChain", top + new Vector3(0, 6f, 0), new Vector3(0.08f, 6f, 0.08f), iron, Quaternion.identity, false);
                    Deco(holder, PrimitiveType.Cylinder, "ChandelierRing", top, new Vector3(2.6f, 0.08f, 2.6f), gold, Quaternion.identity, false);
                    Shape(holder, "ChandelierCrystal", GemMesh(), top - new Vector3(0, 0.6f, 0), new Vector3(0.9f, 1.3f, 0.9f), PMat("Crystal", new Color(1f, 0.95f, 0.8f), 1f, new Color(1f, 0.9f, 0.6f)),
                        false, Quaternion.identity);
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 at = top + Quaternion.Euler(0, k * 60f, 0) * new Vector3(1.3f, 0, 0);
                        Deco(holder, PrimitiveType.Sphere, "Candle", at + new Vector3(0, 0.2f, 0), Vector3.one * 0.3f, NeonMat(2), Quaternion.identity, false);
                    }
                }
                n++;
            }
            if (casino) CasinoCrown(holder, g, z, inside, label);
            Debug.Log("[WizardsVRC World] " + (casino ? "Casino" : "Works") + " dressing over " + n + " cells of '" + label + "'.");
        }

        /// <summary>
        /// The casino's face to the city: on the side of its block that looks toward the town (in an open world; else the
        /// map's nearest edge), a gold facade with its name in lights, gold pilasters and marquee lights, a tall tower with
        /// a gold dome and spire standing out over the cliff on a rock buttress, and two searchlights raking the cavern roof.
        /// </summary>
        private static void CasinoCrown(Transform holder, ZoneGrid g, ZoneDefinition z, HashSet<Vector2Int> inside, string label)
        {
            float cs = g.cell;
            Vector3 lo = new Vector3(float.MaxValue, 0, float.MaxValue), hi = new Vector3(float.MinValue, 0, float.MinValue);
            float floorY = 0f;
            foreach (var c in inside)
            {
                Vector3 p = g.Local(c);
                lo = Vector3.Min(lo, p);
                hi = Vector3.Max(hi, p);
                floorY = Mathf.Max(floorY, p.y);
            }
            // Which way the city lies: toward the town in an open world, else out of the map's nearest edge.
            Vector3 rootPos = ZoneRootPos(z);
            Vector3 toCity;
            OpenWorldPlan plan;
            if (_openPlan.TryGetValue(z.zoneKey, out plan) && plan.town != z) toCity = plan.townPos - rootPos;
            else
            {
                Vector3 mid = (lo + hi) * 0.5f;
                float W = g.width * cs, H = g.height * cs;
                float dn = H / 2f - mid.z, ds = mid.z + H / 2f, de = W / 2f - mid.x, dw = mid.x + W / 2f;
                float m = Mathf.Min(Mathf.Min(dn, ds), Mathf.Min(de, dw));
                toCity = m == ds ? Vector3.back : m == dn ? Vector3.forward : m == de ? Vector3.right : Vector3.left;
            }
            toCity.y = 0f;
            Vector3 f = Mathf.Abs(toCity.x) > Mathf.Abs(toCity.z) ? new Vector3(Mathf.Sign(toCity.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toCity.z));
            Vector3 across = Vector3.Cross(Vector3.up, f);
            Vector3 centre = (lo + hi) * 0.5f;
            float halfDepth = Mathf.Abs(Vector3.Dot(hi - lo, f)) * 0.5f;
            float width = Mathf.Abs(Vector3.Dot(hi - lo, across)) + cs * 2f;
            Vector3 face = new Vector3(centre.x, floorY, centre.z) + f * (halfDepth + cs);   // on the wall beyond the last row inside
            Quaternion outward = Quaternion.LookRotation(f);
            Material gold = UnderGold(), brass = UnderBrass(), iron = UnderIron();
            Material goldPlate = PsxTexMat("CasinoGoldPlate", new Color(0.95f, 0.75f, 0.32f), TileTexture(StreetTexDir + "/Metal.png"), 2.4f);
            Material velvet = PsxTexMat("CasinoWall", new Color(0.4f, 0.06f, 0.1f), TileTexture(StreetTexDir + "/Velvet.png"), 2f);
            Material rock = TunnelMats(z)[5];
            const float faceH = 30f;
            // The facade.
            Deco(holder, PrimitiveType.Cube, "CasinoFacade", face + Vector3.up * (faceH / 2f), new Vector3(width, faceH, 2f), velvet, outward, false);
            Deco(holder, PrimitiveType.Cube, "CasinoCornice", face + Vector3.up * (faceH + 0.6f) + f * 0.4f, new Vector3(width + 2f, 1.2f, 3f), gold, outward, false);
            Deco(holder, PrimitiveType.Cube, "CasinoPlinth", face + Vector3.up * 1.5f + f * 0.4f, new Vector3(width + 1f, 3f, 2.8f), goldPlate, outward, false);
            int piers = Mathf.Max(3, Mathf.RoundToInt(width / 7f));
            for (int i = 0; i <= piers; i++)
            {
                float t = i / (float)piers * width - width / 2f;
                Deco(holder, PrimitiveType.Cube, "CasinoPier", face + across * t + f * 1.1f + Vector3.up * (faceH / 2f), new Vector3(1.4f, faceH, 1.2f), goldPlate, outward, false);
            }
            foreach (float y in new[] { 4.5f, faceH - 1.5f })
                Deco(holder, PrimitiveType.Cube, "CasinoNeonBand", face + f * 1.8f + Vector3.up * y, new Vector3(width - 1f, 0.35f, 0.15f), NeonMat(2), outward, false);
            for (int i = 0; i < Mathf.RoundToInt(width / 2.2f); i++)
            {
                float t = (i + 0.5f) / Mathf.RoundToInt(width / 2.2f) * width - width / 2f;
                Shape(holder, "CasinoMarquee", GemMesh(), face + across * t + f * 1.9f + Vector3.up * (faceH + 1.6f), new Vector3(0.5f, 0.6f, 0.5f), i % 2 == 0 ? NeonMat(2) : NeonMat(0), false,
                    Quaternion.identity);
            }
            // Its name in lights, across the facade.
            var sign = WorldText(holder, "CasinoName", face + f * 1.9f + Vector3.up * (faceH * 0.62f), Quaternion.LookRotation(-f), new Vector2(5200, 600), 0.0085f,
                (string.IsNullOrEmpty(label) ? "THE CASINO" : label.ToUpperInvariant()), 300);
            sign.color = new Color(1f, 0.82f, 0.3f);
            var sub = WorldText(holder, "CasinoSub", face + f * 1.9f + Vector3.up * (faceH * 0.38f), Quaternion.LookRotation(-f), new Vector2(4000, 300), 0.0075f,
                "HIGH ROLLERS WELCOME  *  THE HOUSE ALWAYS WINS", 140);
            sub.color = new Color(1f, 0.4f, 0.8f);
            // The tower: out over the cliff on a buttress of rock from the chasm floor.
            Vector3 tower = face + f * 10f;
            float floorLocal = InOpenWorld(z) ? OpenFloorY(z) - rootPos.y : -45f;
            float buttressH = floorY - floorLocal + 2f;
            Shape(holder, "CasinoButtress", CylMesh(), new Vector3(tower.x, floorLocal + buttressH / 2f - 1f, tower.z), new Vector3(22f, buttressH / 2f, 22f), rock, false, Quaternion.identity);
            Shape(holder, "CasinoButtressCrag", BlobMesh(), new Vector3(tower.x, floorLocal + buttressH * 0.55f, tower.z), new Vector3(28f, buttressH * 0.5f, 28f), rock, false,
                Quaternion.Euler(0, 30f, 0));
            const float towerH = 58f;
            Deco(holder, PrimitiveType.Cube, "CasinoTower", tower + Vector3.up * (towerH / 2f), new Vector3(14f, towerH, 14f), velvet, outward, false);
            foreach (float s in new[] { -1f, 1f })
                foreach (float s2 in new[] { -1f, 1f })
                    Deco(holder, PrimitiveType.Cube, "CasinoTowerEdge", tower + across * (s * 7f) + f * (s2 * 7f) + Vector3.up * (towerH / 2f), new Vector3(1.4f, towerH, 1.4f), goldPlate,
                        Quaternion.identity, false);
            for (float y = 8f; y < towerH; y += 8f)
            {
                Deco(holder, PrimitiveType.Cube, "CasinoTowerBand", tower + Vector3.up * y, new Vector3(14.6f, 0.4f, 14.6f), NeonMat(y % 16f < 1f ? 0 : 2), Quaternion.identity, false);
                for (int k = -1; k <= 1; k++)
                    Deco(holder, PrimitiveType.Cube, "CasinoTowerWindow", tower + f * 7.05f + across * (k * 4f) + Vector3.up * (y + 4f), new Vector3(2f, 3f, 0.1f),
                        PMat("CasinoWindow", new Color(1f, 0.85f, 0.5f), 1f, new Color(1f, 0.75f, 0.35f)), outward, false);
            }
            Shape(holder, "CasinoDome", BlobMesh(), tower + Vector3.up * (towerH + 4f), new Vector3(15f, 12f, 15f), gold, false, Quaternion.identity);
            Shape(holder, "CasinoSpire", ConeMesh(), tower + Vector3.up * (towerH + 16f), new Vector3(3f, 16f, 3f), gold, false, Quaternion.identity);
            Shape(holder, "CasinoSpireGem", GemMesh(), tower + Vector3.up * (towerH + 25f), new Vector3(3f, 4f, 3f), NeonMat(0), false, Quaternion.identity);
            // A tall blade sign down the tower's face.
            var blade = WorldText(holder, "CasinoBlade", tower + f * 7.6f + Vector3.up * (towerH * 0.55f), Quaternion.LookRotation(-f), new Vector2(500, 3200), 0.0095f,
                "C\nA\nS\nI\nN\nO", 380);
            blade.color = new Color(1f, 0.3f, 0.75f);
            blade.lineSpacing = 0.85f;
            // Searchlights raking the cavern roof.
            Material beam = PMat("CasinoSearchlight", new Color(1f, 0.95f, 0.75f), 1f, new Color(0.9f, 0.85f, 0.6f));
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 baseAt = face + across * (s * (width / 2f - 2f)) + Vector3.up * (faceH + 2f) + f * 1f;
                Shape(holder, "Searchlight", CylMesh(), baseAt, new Vector3(1.8f, 0.8f, 1.8f), brass, false, Quaternion.identity);
                Vector3 dir = (Vector3.up * 2.2f + f * 0.9f + across * (s * 0.7f)).normalized;
                const float len = 90f;
                Shape(holder, "SearchlightBeam", ConeMesh(), baseAt + dir * (len / 2f), new Vector3(9f, len, 9f), beam, false, Quaternion.FromToRotation(Vector3.down, dir));
            }
        }
    }
}
