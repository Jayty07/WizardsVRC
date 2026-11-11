using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Dustwind's (World 2) street pieces: boomtown buildings with tall false fronts, covered boardwalk porches and
    /// shingle roofs; flat-roofed adobe houses with poking roof beams; timber-propped rock in the mines; saguaro cactus
    /// for trees; and the desert past the map's edge (sand flats, buttes, windmills, water towers) instead of the sea.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static Material TimberMat()
        {
            return PsxTexMat("FrontierTimber", new Color(0.5f, 0.36f, 0.24f), TileTexture(StreetTexDir + "/Timber.png"), 1.4f);
        }

        /// <summary>A building over a block of '#' cells, in the street's Dustwind style.</summary>
        private static void FrontierHouse(Transform parent, ZoneGrid g, ZoneDefinition z, RectInt r, Material wall, Material roof, Material window)
        {
            float cs = g.cell;
            Vector3 c = RectCenter(g, r);
            float sx = r.width * cs, sz = r.height * cs;
            bool edge = r.xMin == 0 || r.yMin == 0 || r.xMax == g.width || r.yMax == g.height;
            float hash = g.Hash(r.xMin, r.yMin, 1);
            Material timber = TimberMat();
            if (z.textureStyle == ZoneTextureStyle.Mine)
            {
                // Bare rock, lumpy on top, with timber props where it meets a passage.
                float h = 8f + hash * 5f + (edge ? 6f : 0f);
                NoteHouse(r, Vector2Int.zero, h, HousePlain);
                Block(parent, "RockMass", c + new Vector3(0, h / 2f, 0), new Vector3(sx, h, sz), wall);
                Deco(parent, PrimitiveType.Sphere, "RockTop", c + new Vector3(0, h, 0), new Vector3(sx * 0.9f, 3f + hash * 3f, sz * 0.9f), wall,
                    Quaternion.Euler(0, hash * 90f, 0), false);
                for (int y = r.yMin; y < r.yMax; y++)
                    for (int x = r.xMin; x < r.xMax; x++)
                        foreach (var d in ZoneGrid.Dirs)
                        {
                            if (!g.Walkable(x + d.x, y + d.y) || g.Hash(x, y, 30 + d.x * 3 + d.y) > 0.5f) continue;
                            Vector3 dir = ZoneGrid.ToLocalDir(d), side = Vector3.Cross(Vector3.up, dir);
                            Vector3 face = g.Local(x, y) + dir * (cs * 0.5f + 0.2f);
                            Quaternion rot = Quaternion.LookRotation(dir);
                            foreach (float s in new[] { -1f, 1f })
                                Deco(parent, PrimitiveType.Cube, "MineProp", face + side * (s * cs * 0.4f) + new Vector3(0, 2.4f, 0), new Vector3(0.45f, 4.8f, 0.45f), timber, rot, false);
                            Deco(parent, PrimitiveType.Cube, "MineLintel", face + new Vector3(0, 4.9f, 0), new Vector3(cs * 0.95f, 0.45f, 0.5f), timber, rot, false);
                            if (g.Hash(x, y, 40 + d.x + d.y * 5) > 0.6f)
                                Deco(parent, PrimitiveType.Cube, "MineLamp", face + dir * 0.4f + new Vector3(0, 4.3f, 0), new Vector3(0.35f, 0.45f, 0.35f), window, rot, false);
                        }
                return;
            }

            Vector2Int fd = g.FrontSide(r);
            Vector3 front = fd == Vector2Int.zero ? Vector3.back : ZoneGrid.ToLocalDir(fd);
            Vector3 across = Vector3.Cross(Vector3.up, front);
            float width = Mathf.Abs(front.x) > 0.5f ? sz : sx;     // the front's length
            float depth = Mathf.Abs(front.x) > 0.5f ? sx : sz;
            Quaternion rotF = Quaternion.LookRotation(front);
            Vector3 faceC = c + front * (depth / 2f);

            if (z.textureStyle == ZoneTextureStyle.Desert)
            {
                // Adobe: thick walls, a flat roof behind a low parapet, roof beams poking out, small deep windows.
                float h = 4.5f + hash * 2.5f + (edge ? 3f : 0f);
                NoteHouse(r, fd, h, HousePlain);
                Block(parent, "Adobe", c + new Vector3(0, h / 2f, 0), new Vector3(sx, h, sz), wall);
                Deco(parent, PrimitiveType.Cube, "Parapet", c + new Vector3(0, h + 0.35f, 0), new Vector3(sx + 0.3f, 0.7f, sz + 0.3f), wall, Quaternion.identity, false);
                Deco(parent, PrimitiveType.Cube, "RoofTop", c + new Vector3(0, h + 0.5f, 0), new Vector3(sx - 0.6f, 0.5f, sz - 0.6f), roof, Quaternion.identity, false);
                int beams = Mathf.Max(2, Mathf.FloorToInt(width / 1.6f));
                for (int i = 0; i < beams; i++)
                {
                    float t = (i + 0.5f) / beams * width - width / 2f;
                    Deco(parent, PrimitiveType.Cylinder, "Viga", faceC + across * t + front * 0.4f + new Vector3(0, h - 0.6f, 0), new Vector3(0.28f, 0.45f, 0.28f), timber,
                        rotF * Quaternion.Euler(90f, 0, 0), false);
                }
                int wins = Mathf.Max(1, Mathf.FloorToInt(width / 4f));
                for (int i = 0; i < wins; i++)
                {
                    if (g.Hash(r.xMin + i, r.yMin, 12) > 0.7f) continue;
                    float t = (i + 0.5f) / wins * width - width / 2f;
                    Deco(parent, PrimitiveType.Cube, "Window", faceC + across * t + front * 0.03f + new Vector3(0, 2.2f, 0), new Vector3(0.9f, 1.1f, 0.08f), window, rotF, false);
                }
                return;
            }

            // Boomtown: a two-storey board building with a gable roof hidden behind a tall square false front, and a
            // covered boardwalk porch along the front.
            float bh = 5.5f + hash * 3f + (edge ? 3f : 0f);
            NoteHouse(r, fd, bh, HousePorch);
            Block(parent, "Building", c + new Vector3(0, bh / 2f, 0), new Vector3(sx, bh, sz), wall);
            bool ridgeAlongDepth = true;
            Vector3 ridgeSize = ridgeAlongDepth ? new Vector3(width * 0.55f, 1.6f, depth) : new Vector3(width, 1.6f, depth * 0.55f);
            Deco(parent, PrimitiveType.Cube, "Roof", c + new Vector3(0, bh + 0.25f, 0), new Vector3(sx + 0.6f, 0.5f, sz + 0.6f), roof, Quaternion.identity, false);
            Deco(parent, PrimitiveType.Cube, "RoofRidge", c + new Vector3(0, bh + 1f, 0), ridgeSize, roof, rotF, false);
            float fh = bh + 2.4f + g.Hash(r.xMin, r.yMin, 7) * 1.6f;
            Block(parent, "FalseFront", faceC + front * 0.15f + new Vector3(0, fh / 2f, 0), new Vector3(width, fh, 0.35f), wall, rotF);
            Deco(parent, PrimitiveType.Cube, "Cornice", faceC + front * 0.35f + new Vector3(0, fh + 0.15f, 0), new Vector3(width + 0.4f, 0.35f, 0.6f), timber, rotF, false);
            if (g.Hash(r.xMin, r.yMin, 8) > 0.5f)
                Deco(parent, PrimitiveType.Cube, "Pediment", faceC + front * 0.2f + new Vector3(0, fh + 0.7f, 0), new Vector3(width * 0.35f, 0.8f, 0.3f), wall, rotF, false);
            // Porch roof on posts out over the boardwalk.
            float porch = Mathf.Min(2.6f, cs * 0.35f);
            Deco(parent, PrimitiveType.Cube, "PorchRoof", faceC + front * (porch / 2f + 0.3f) + new Vector3(0, 3.4f, 0), new Vector3(width, 0.15f, porch + 0.4f), roof,
                rotF * Quaternion.Euler(-8f, 0, 0), false);
            int posts = Mathf.Max(2, Mathf.FloorToInt(width / 3.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float t = posts == 1 ? 0f : i / (float)(posts - 1) * (width - 0.4f) - (width - 0.4f) / 2f;
                Deco(parent, PrimitiveType.Cube, "PorchPost", faceC + across * t + front * (porch + 0.2f) + new Vector3(0, 1.7f, 0), new Vector3(0.22f, 3.4f, 0.22f), timber, rotF, false);
            }
            Deco(parent, PrimitiveType.Cube, "Boardwalk", faceC + front * (porch / 2f + 0.3f) + new Vector3(0, 0.12f, 0), new Vector3(width, 0.24f, porch + 0.4f), timber, rotF, false);
            // Windows on both storeys and a door.
            int cols = Mathf.Max(1, Mathf.FloorToInt(width / 3.2f));
            for (int i = 0; i < cols; i++)
            {
                float t = (i + 0.5f) / cols * width - width / 2f;
                bool door = i == cols / 2;
                if (door) Deco(parent, PrimitiveType.Cube, "Door", faceC + across * t + front * 0.36f + new Vector3(0, 1.3f, 0), new Vector3(1.3f, 2.6f, 0.08f), timber, rotF, false);
                else Deco(parent, PrimitiveType.Cube, "Window", faceC + across * t + front * 0.36f + new Vector3(0, 1.9f, 0), new Vector3(1.3f, 1.5f, 0.08f), window, rotF, false);
                if (bh > 6.5f && g.Hash(r.xMin + i, r.yMin, 13) > 0.3f)
                    Deco(parent, PrimitiveType.Cube, "Window", faceC + across * t + front * 0.36f + new Vector3(0, 5.2f, 0), new Vector3(1.1f, 1.3f, 0.08f), window, rotF, false);
            }
        }

        /// <summary>A saguaro: a ribbed trunk with one or two arms curling up. 'pick' (0..1) picks the arms.</summary>
        private static void Saguaro(Transform parent, Vector3 p, float s, float pick, Material skin, bool collider)
        {
            float h = 5.2f * s;
            Deco(parent, PrimitiveType.Cylinder, "Saguaro", p + new Vector3(0, h / 2f, 0), new Vector3(0.9f * s, h / 2f, 0.9f * s), skin, Quaternion.identity, collider);
            Deco(parent, PrimitiveType.Sphere, "SaguaroTop", p + new Vector3(0, h, 0), new Vector3(0.9f, 0.7f, 0.9f) * s, skin, Quaternion.identity, false);
            int arms = pick < 0.3f ? 1 : pick < 0.85f ? 2 : 0;
            for (int a = 0; a < arms; a++)
            {
                float side = a == 0 ? 1f : -1f;
                Quaternion turn = Quaternion.Euler(0, pick * 360f + a * 170f, 0);
                Vector3 outDir = turn * Vector3.right * side;
                float ay = h * (0.38f + 0.12f * a + pick * 0.1f);
                Deco(parent, PrimitiveType.Cylinder, "SaguaroElbow", p + outDir * 0.75f * s + new Vector3(0, ay, 0), new Vector3(0.55f, 0.6f, 0.55f) * s, skin,
                    Quaternion.LookRotation(outDir) * Quaternion.Euler(90f, 0, 0), false);
                Deco(parent, PrimitiveType.Cylinder, "SaguaroArm", p + outDir * 1.3f * s + new Vector3(0, ay + 1.1f * s, 0), new Vector3(0.6f, 1.1f, 0.6f) * s, skin,
                    Quaternion.identity, false);
                Deco(parent, PrimitiveType.Sphere, "SaguaroArmTop", p + outDir * 1.3f * s + new Vector3(0, ay + 2.2f * s, 0), new Vector3(0.6f, 0.5f, 0.6f) * s, skin,
                    Quaternion.identity, false);
            }
        }

        /// <summary>A desert butte or mesa far off: banded red rock with a flat top.</summary>
        private static void Butte(Transform holder, Vector3 p, float w, float h, Material rock, Material top, float yaw)
        {
            Shape(holder, "Butte", CylMesh(), p + new Vector3(0, h / 2f, 0), new Vector3(w, h / 2f, w * 0.8f), rock, false, Quaternion.Euler(0, yaw, 0));
            Shape(holder, "ButteTalus", ConeMesh(), p + new Vector3(0, h * 0.12f, 0), new Vector3(w * 1.5f, h * 0.35f, w * 1.25f), rock, false, Quaternion.Euler(0, yaw + 22f, 0));
            Shape(holder, "ButteTop", CylMesh(), p + new Vector3(0, h + 0.2f, 0), new Vector3(w * 1.02f, 0.25f, w * 0.82f), top, false, Quaternion.Euler(0, yaw, 0));
        }

        /// <summary>A farm windmill: a lattice tower with a bladed wheel and a tail vane.</summary>
        private static void Windmill(Transform holder, Vector3 p, Quaternion face, Material wood, Material metal)
        {
            const float h = 9f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    BackBlock(holder, "WindmillLeg", p + face * new Vector3(sx * 0.9f, h / 2f, sz * 0.9f), new Vector3(0.2f, h, 0.2f), wood,
                        face * Quaternion.Euler(-sz * 5f, 0, sx * 5f));
            BackBlock(holder, "WindmillDeck", p + new Vector3(0, h, 0), new Vector3(1.2f, 0.2f, 1.2f), wood, face);
            Shape(holder, "WindmillHub", CylMesh(), p + face * new Vector3(0, h + 0.8f, 0.6f), new Vector3(0.5f, 0.2f, 0.5f), metal, false, face * Quaternion.Euler(90f, 0, 0));
            for (int b = 0; b < 12; b++)
                BackBlock(holder, "WindmillBlade", p + face * (new Vector3(0, h + 0.8f, 0.65f) + Quaternion.Euler(0, 0, b * 30f) * new Vector3(0, 1.3f, 0)),
                    new Vector3(0.35f, 2.2f, 0.04f), metal, face * Quaternion.Euler(0, 0, b * 30f) * Quaternion.Euler(0, 20f, 0));
            BackBlock(holder, "WindmillTail", p + face * new Vector3(0, h + 0.8f, -1.6f), new Vector3(0.05f, 1f, 2.2f), metal, face);
        }

        /// <summary>A wooden water tower on stilts.</summary>
        private static void WaterTower(Transform holder, Vector3 p, Material wood, Material roof, bool collider)
        {
            const float legs = 6f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Deco(holder, PrimitiveType.Cube, "TowerLeg", p + new Vector3(sx * 1.6f, legs / 2f, sz * 1.6f), new Vector3(0.3f, legs, 0.3f), wood, Quaternion.identity, collider);
            Deco(holder, PrimitiveType.Cylinder, "TowerTank", p + new Vector3(0, legs + 2f, 0), new Vector3(4.4f, 2f, 4.4f), wood, Quaternion.identity, false);
            Shape(holder, "TowerRoof", ConeMesh(), p + new Vector3(0, legs + 4.6f, 0), new Vector3(5f, 1.6f, 5f), roof, false, Quaternion.identity);
            Deco(holder, PrimitiveType.Cylinder, "TowerSpout", p + new Vector3(2.4f, legs + 0.4f, 0), new Vector3(0.25f, 1.2f, 0.25f), wood, Quaternion.Euler(0, 0, 30f), false);
        }

        /// <summary>One piece of the desert round a Dustwind street (backdrop: no colliders).</summary>
        private static void DesertBackdropProp(Transform holder, ZoneTextureStyle st, Vector3 p, Quaternion face, bool back, System.Random rng,
            Material bld, Material roof, Material leaves, Material trunk, Material glow, Material rock, Material sandTop)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            double roll = rng.NextDouble();
            if (st == ZoneTextureStyle.Mine || roll < 0.25)
            {
                // Rock outcrops and boulders.
                Butte(holder, p, Rr(6f, 14f), Rr(5f, 14f), rock, sandTop, Rr(0f, 360f));
                return;
            }
            if (roll < 0.55)
            {
                for (int k = 0; k < 3; k++) Saguaro(holder, p + new Vector3(Rr(-6f, 6f), 0, Rr(-6f, 6f)), Rr(0.8f, 1.4f), (float)rng.NextDouble(), leaves, false);
                return;
            }
            if (roll < 0.68 && back)
            {
                Windmill(holder, p, face, TimberMat(), PMat("WindmillMetal", new Color(0.55f, 0.55f, 0.58f), 2f));
                return;
            }
            if (roll < 0.76)
            {
                WaterTower(holder, p, TimberMat(), roof, false);
                return;
            }
            // A ranch house or a shack with a lit window.
            float w = Rr(7f, 11f), d = Rr(6f, 9f), h = Rr(4f, 7f);
            BackBlock(holder, "Ranch", p + new Vector3(0, h / 2f, 0), new Vector3(w, h, d), bld, face);
            BackBlock(holder, "RanchRoof", p + new Vector3(0, h + 0.3f, 0), new Vector3(w + 0.8f, 0.6f, d + 0.8f), roof, face);
            BackBlock(holder, "RanchFront", p + face * new Vector3(0, h / 2f + 1.2f, d / 2f), new Vector3(w, h + 2.4f, 0.3f), bld, face);
            if (rng.NextDouble() < 0.6)
                BackBlock(holder, "RanchWindow", p + face * new Vector3(Rr(-w * 0.3f, w * 0.3f), 2f, d / 2f + 0.2f), new Vector3(1f, 1.2f, 0.06f), glow, face);
        }

        /// <summary>
        /// Distant buttes and mesas all round a Dustwind street, on the sand flats below it, at least about 'minHeight' tall
        /// (enough to rise over the street's own rooftops) when it's given.
        /// </summary>
        private static void DesertButtes(Transform holder, float r0, float r1, float floorY, int count, System.Random rng, Material rock, Material top, float minHeight = 0f)
        {
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int k = 0; k < count; k++)
            {
                float a = (k + Rr(0.1f, 0.9f)) / count * 360f;
                Vector3 p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, Rr(r0, r1));
                p.y = floorY;
                bool mesa = rng.NextDouble() < 0.5;
                float h = mesa ? Rr(18f, 30f) : Rr(28f, 45f);
                if (minHeight > 0f) h = Mathf.Max(h, minHeight * (mesa ? Rr(0.8f, 0.95f) : Rr(0.95f, 1.15f)));
                Butte(holder, p, mesa ? Rr(30f, 55f) : Rr(10f, 18f), h, rock, top, Rr(0f, 360f));
            }
        }
    }
}
