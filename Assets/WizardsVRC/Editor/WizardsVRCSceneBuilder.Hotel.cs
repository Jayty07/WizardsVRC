using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Hotel landmark (Bigdig's Gilded Rivet Hotel): a walk-in, three-storey kobold hotel round a tall lobby atrium,
    /// in corrugated iron, riveted plate and brass, with neon on the outside. It has every working part of the Crystal Tap
    /// (WizardsVRCSceneBuilder.Tavern.cs): the jukebox (its screen over the front desk), the bell (here the front desk's
    /// bell: it brings fresh mugs), seats you can sit in, a mirror you switch on yourself and a flickering fire.
    ///   - Outside: a marquee canopy over the door ringed with chase lights, a red carpet, the name in lights across the
    ///     front, a tall neon HOTEL blade sign up one corner, rows of lit windows, and a crooked penthouse, water tank and
    ///     smokestack on the roof.
    ///   - Inside: the front desk facing the door with a wall of key cubbies behind it (an NPC painted inside the block
    ///     works the desk), the lounge bar down the right side with stools, bottles and the mirror, armchairs round a
    ///     fireplace on the left, luggage carts, a brass cage lift in the back corner, chandeliers in the atrium, and
    ///     galleries up both sides (stairs at the front) lined with numbered room doors, another floor of balconies above.
    /// Built in its own frame: x across the front, z from the back wall (-) to the front doors (+), y up.
    /// Original design: nothing here copies another game's hotel or names.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float HotelWallH = 14f, HotelGalleryY = 5f, HotelBalconyY = 9.5f, HotelGalleryW = 3.2f, HotelFloorY = 0.14f;

        /// <summary>The hotel's frame: its middle (on the floor), the way its doors face, its inside width and depth.</summary>
        private static bool HotelFrame(ZoneGrid g, ZoneLandmark l, out Vector3 c, out Vector3 front, out float W, out float D)
        {
            var fp = g.Footprint(l.Symbol);
            var cells = g.Find(l.Symbol);
            c = RectCenter(g, fp);
            front = Vector3.forward;
            W = D = 0f;
            if (fp.width == 0 || cells.Count == 0) return false;
            c.y = g.Elevation(cells[0].x, cells[0].y);
            Vector2Int fd = g.FrontSide(fp);
            front = fd == Vector2Int.zero ? Vector3.back : ZoneGrid.ToLocalDir(fd);
            bool alongX = Mathf.Abs(front.x) > 0.5f;
            float across = (alongX ? fp.height : fp.width) * g.cell, deep = (alongX ? fp.width : fp.height) * g.cell;
            // A walkway round the sides, and room out front for the canopy.
            W = Mathf.Max(20f, across - 3f);
            D = Mathf.Max(22f, deep - 6f);
            // Pulled back a little so the canopy stays inside the block.
            c -= front * 1.5f;
            return true;
        }

        /// <summary>An NPC painted inside a hotel's block works the front desk (the first in the middle, the rest along it).</summary>
        private static bool HotelDeskSpot(ZoneGrid g, ZoneDefinition z, Vector2Int cell, out Vector3 pos, out Vector3 front)
        {
            pos = front = Vector3.zero;
            if (z.landmarks == null) return false;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.Hotel) continue;
                var fp = g.Footprint(l.Symbol);
                if (fp.width == 0 || !fp.Contains(cell)) continue;
                Vector3 c, f;
                float W, D;
                if (!HotelFrame(g, l, out c, out f, out W, out D)) continue;
                int slot = 0;
                if (z.npcs != null)
                    foreach (var other in z.npcs)
                    {
                        if (other == null) continue;
                        var oc = g.Find(other.Symbol);
                        if (oc.Count == 0) continue;
                        if (oc[0] == cell) break;
                        if (fp.Contains(oc[0])) slot++;
                    }
                Quaternion rot = Quaternion.LookRotation(f);
                float along = slot == 0 ? 0f : ((slot + 1) / 2) * 2.5f * (slot % 2 == 1 ? 1f : -1f);
                Vector3 local = new Vector3(HotelDeskX() + along, HotelFloorY, HotelDeskZ(D) - 1.4f);
                pos = c + rot * local;
                front = f;
                return true;
            }
            return false;
        }

        private static float HotelDeskX() { return -2f; }
        private static float HotelDeskZ(float D) { return -D / 2f + 4.2f; }

        private static void HotelLandmark(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark l, List<UdonSharpBehaviour> behaviours, Material window)
        {
            Vector3 c, front;
            float W, D;
            if (!HotelFrame(g, l, out c, out front, out W, out D)) return;
            string name = string.IsNullOrEmpty(l.label) ? "The Grand Hotel" : l.label;
            Transform t = Child(root, "Hotel_" + l.symbol, c);
            t.localRotation = Quaternion.LookRotation(front);
            float hw = W / 2f, hd = D / 2f;

            var outside = PsxTexMat("HotelOutside", new Color(0.55f, 0.42f, 0.36f), TileTexture(StreetTexDir + "/Corrugated.png"), 2.6f);
            var plate = PsxTexMat("HotelPlate", new Color(0.42f, 0.38f, 0.38f), TileTexture(StreetTexDir + "/Metal.png"), 2.2f);
            var wallpaper = PsxTexMat("HotelWallpaper", new Color(0.16f, 0.42f, 0.4f), TileTexture(StreetTexDir + "/Velvet.png"), 2f);
            var carpet = PsxTexMat("HotelCarpet", new Color(0.55f, 0.1f, 0.14f), TileTexture(StreetTexDir + "/Velvet.png"), 1.6f);
            var floorTex = PsxTexMat("HotelFloor", new Color(0.5f, 0.44f, 0.4f), TileTexture(TerrainTexDir + "/DeckPlate.png"), 2.4f);
            var darkWood = PsxTexMat("SaloonDarkWood", new Color(0.34f, 0.2f, 0.12f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 1.6f);
            var planks = PsxTexMat("HotelPlanks", new Color(0.6f, 0.42f, 0.3f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var brass = UnderBrass();
            var gold = UnderGold();
            var iron = UnderIron();
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            var leather = PMat("HotelLeather", new Color(0.45f, 0.16f, 0.12f), 1f);
            var pewter = PMat("Pewter", new Color(0.6f, 0.62f, 0.66f), 1f);
            var foam = PMat("MugFoam", new Color(0.98f, 0.95f, 0.85f), 1f);
            var bottleG = PMat("BottleGreen", new Color(0.2f, 0.5f, 0.3f), 1f, new Color(0.02f, 0.1f, 0.04f));
            var bottleB = PMat("BottleAmber", new Color(0.7f, 0.4f, 0.12f), 1f, new Color(0.15f, 0.06f, 0f));
            var crystal = PMat("Crystal", new Color(1f, 0.95f, 0.8f), 1f, new Color(1f, 0.9f, 0.6f));

            // ---- floor, walls (a door gap in the front), ceiling, roof
            Block(t, "HotelFloor", new Vector3(0, HotelFloorY - 0.1f, 0), new Vector3(W, 0.2f, D), floorTex);
            Deco(t, PrimitiveType.Cube, "LobbyRug", new Vector3(0, HotelFloorY + 0.01f, 0.5f), new Vector3(W - 2f * HotelGalleryW - 3f, 0.02f, D - 6f), carpet, Quaternion.identity, false);
            const float th = 0.5f, doorW = 4f, doorH = 4.6f;
            Block(t, "BackWall", new Vector3(0, HotelWallH / 2f, -hd - th / 2f), new Vector3(W + th * 2f, HotelWallH, th), outside);
            foreach (float s in new[] { -1f, 1f })
                Block(t, "SideWall", new Vector3(s * (hw + th / 2f), HotelWallH / 2f, 0), new Vector3(th, HotelWallH, D + th * 2f), outside);
            float sideFront = (W - doorW) / 2f;
            foreach (float s in new[] { -1f, 1f })
                Block(t, "FrontWall", new Vector3(s * (doorW / 2f + sideFront / 2f), HotelWallH / 2f, hd + th / 2f), new Vector3(sideFront + th, HotelWallH, th), outside);
            Block(t, "DoorLintel", new Vector3(0, (doorH + HotelWallH) / 2f, hd + th / 2f), new Vector3(doorW, HotelWallH - doorH, th), outside);
            // Wallpaper inside, with a brass picture rail.
            Deco(t, PrimitiveType.Cube, "Wallpaper", new Vector3(0, HotelWallH / 2f, -hd + 0.03f), new Vector3(W, HotelWallH, 0.04f), wallpaper, Quaternion.identity, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(t, PrimitiveType.Cube, "Wallpaper", new Vector3(s * (hw - 0.03f), HotelWallH / 2f, 0), new Vector3(0.04f, HotelWallH, D), wallpaper, Quaternion.identity, false);
                Deco(t, PrimitiveType.Cube, "Wallpaper", new Vector3(s * (doorW / 2f + sideFront / 2f), HotelWallH / 2f, hd - 0.03f), new Vector3(sideFront, HotelWallH, 0.04f), wallpaper,
                    Quaternion.identity, false);
            }
            foreach (float y in new[] { 1.1f, 4.4f })
            {
                Deco(t, PrimitiveType.Cube, "BrassRail", new Vector3(0, y, -hd + 0.07f), new Vector3(W, 0.08f, 0.05f), brass, Quaternion.identity, false);
                foreach (float s in new[] { -1f, 1f })
                    Deco(t, PrimitiveType.Cube, "BrassRail", new Vector3(s * (hw - 0.07f), y, 0), new Vector3(0.05f, 0.08f, D), brass, Quaternion.identity, false);
            }
            Block(t, "Ceiling", new Vector3(0, HotelWallH + 0.15f, 0), new Vector3(W + th * 2f, 0.3f, D + th * 2f), plate);
            // The roof: a parapet, a crooked penthouse, a water tank and a smokestack.
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(t, PrimitiveType.Cube, "Parapet", new Vector3(s * (hw + th / 2f), HotelWallH + 0.9f, 0), new Vector3(th, 1.2f, D + th * 2f), plate, Quaternion.identity, false);
                Deco(t, PrimitiveType.Cube, "Parapet", new Vector3(0, HotelWallH + 0.9f, s * (hd + th / 2f)), new Vector3(W + th * 2f, 1.2f, th), plate, Quaternion.identity, false);
            }
            Deco(t, PrimitiveType.Cube, "Penthouse", new Vector3(-hw * 0.35f, HotelWallH + 3.2f, -hd * 0.3f), new Vector3(W * 0.4f, 5.6f, D * 0.42f), outside,
                Quaternion.Euler(0, 6f, 2.5f), false);
            Deco(t, PrimitiveType.Cube, "PenthouseRoof", new Vector3(-hw * 0.35f, HotelWallH + 6.2f, -hd * 0.3f), new Vector3(W * 0.44f, 0.35f, D * 0.46f), plate,
                Quaternion.Euler(0, 6f, 2.5f), false);
            for (int k = 0; k < 4; k++)
                Deco(t, PrimitiveType.Cube, "PenthouseWindow", new Vector3(-hw * 0.35f - W * 0.15f + k * W * 0.1f, HotelWallH + 3.5f, -hd * 0.3f + D * 0.21f + 0.05f),
                    new Vector3(1.4f, 1.6f, 0.08f), window, Quaternion.Euler(0, 6f, 2.5f), false);
            Vector3 tank = new Vector3(hw * 0.55f, HotelWallH, -hd * 0.45f);
            foreach (float lx in new[] { -1f, 1f })
                foreach (float lz in new[] { -1f, 1f })
                    Deco(t, PrimitiveType.Cube, "TankLeg", tank + new Vector3(lx, 1.2f, lz), new Vector3(0.2f, 2.4f, 0.2f), iron, Quaternion.identity, false);
            Shape(t, "WaterTank", CylMesh(), tank + new Vector3(0, 4.2f, 0), new Vector3(3.6f, 1.8f, 3.6f), plate, false, Quaternion.identity);
            Shape(t, "TankCap", ConeMesh(), tank + new Vector3(0, 6.5f, 0), new Vector3(3.9f, 1.2f, 3.9f), brass, false, Quaternion.identity);
            Shape(t, "Smokestack", CylMesh(), new Vector3(hw * 0.2f, HotelWallH + 4f, -hd + 1.5f), new Vector3(1.3f, 4f, 1.3f), iron, false, Quaternion.identity);

            // ---- outside: lit windows, the name in lights, the HOTEL blade sign, the marquee canopy and the red carpet
            for (int floor = 0; floor < 3; floor++)
            {
                float y = floor == 0 ? 2.6f : floor == 1 ? 7f : 11.4f;
                int cols = Mathf.Max(2, Mathf.FloorToInt(sideFront / 3.2f));
                foreach (float s in new[] { -1f, 1f })
                    for (int k = 0; k < cols; k++)
                    {
                        float x = s * (doorW / 2f + (k + 0.5f) / cols * sideFront);
                        if (floor == 0 && Mathf.Abs(x) < doorW / 2f + 1.2f) continue;
                        Deco(t, PrimitiveType.Cube, "Window", new Vector3(x, y, hd + th + 0.03f), new Vector3(1.6f, 2f, 0.08f), window, Quaternion.identity, false);
                        Deco(t, PrimitiveType.Cube, "WindowSill", new Vector3(x, y - 1.1f, hd + th + 0.12f), new Vector3(2f, 0.15f, 0.25f), brass, Quaternion.identity, false);
                    }
                // Along the sides too.
                foreach (float s in new[] { -1f, 1f })
                    for (float zz = -hd + 3f; zz < hd - 2f; zz += 4.5f)
                        Deco(t, PrimitiveType.Cube, "Window", new Vector3(s * (hw + th + 0.03f), y, zz), new Vector3(0.08f, 2f, 1.6f), window, Quaternion.identity, false);
            }
            // The name across the front, in lights on an iron board above the canopy.
            Block(t, "NameBoard", new Vector3(0, 9.2f, hd + th + 0.3f), new Vector3(Mathf.Min(W - 2f, 26f), 2.6f, 0.3f), iron);
            Deco(t, PrimitiveType.Cube, "NameBoardNeon", new Vector3(0, 10.55f, hd + th + 0.48f), new Vector3(Mathf.Min(W - 2f, 26f), 0.14f, 0.06f), NeonMat(2), Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "NameBoardNeon", new Vector3(0, 7.85f, hd + th + 0.48f), new Vector3(Mathf.Min(W - 2f, 26f), 0.14f, 0.06f), NeonMat(2), Quaternion.identity, false);
            var sign = WorldText(t, "HotelName", new Vector3(0, 9.2f, hd + th + 0.5f), Quaternion.Euler(0, 180f, 0), new Vector2(3200, 300), 0.0075f, name.ToUpperInvariant(), 96);
            sign.color = new Color(1f, 0.82f, 0.35f);
            // The tall HOTEL blade sign up the front corner, standing out from the wall.
            float bladeX = hw - 1.2f;
            Block(t, "BladeSign", new Vector3(bladeX, 9.5f, hd + th + 1.6f), new Vector3(0.4f, 9f, 2.2f), iron);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(t, PrimitiveType.Cube, "BladeNeonEdge", new Vector3(bladeX + s * 0.22f, 9.5f, hd + th + 2.65f), new Vector3(0.06f, 9f, 0.12f), NeonMat(0), Quaternion.identity, false);
                Deco(t, PrimitiveType.Cube, "BladeNeonEdge", new Vector3(bladeX + s * 0.22f, 9.5f, hd + th + 0.55f), new Vector3(0.06f, 9f, 0.12f), NeonMat(0), Quaternion.identity, false);
                var blade = WorldText(t, "BladeLetters", new Vector3(bladeX + s * 0.23f, 9.5f, hd + th + 1.6f), Quaternion.Euler(0, s > 0 ? -90f : 90f, 0), new Vector2(260, 1200),
                    0.0072f, "H\nO\nT\nE\nL", 200);
                blade.color = new Color(1f, 0.35f, 0.8f);
                blade.lineSpacing = 0.85f;
            }
            Block(t, "BladeBracket", new Vector3(bladeX, 13.6f, hd + th + 0.8f), new Vector3(0.2f, 0.2f, 1.6f), brass);
            Block(t, "BladeBracket", new Vector3(bladeX, 5.4f, hd + th + 0.8f), new Vector3(0.2f, 0.2f, 1.6f), brass);
            // The marquee canopy over the door: a deep box on brass posts, ringed with chase lights.
            const float canopyD = 3.4f, canopyW = 8f;
            Vector3 cc = new Vector3(0, 5.2f, hd + th + canopyD / 2f);
            Deco(t, PrimitiveType.Cube, "Canopy", cc, new Vector3(canopyW, 0.8f, canopyD), plate, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "CanopyTrim", cc + new Vector3(0, 0.45f, 0), new Vector3(canopyW + 0.2f, 0.12f, canopyD + 0.2f), gold, Quaternion.identity, false);
            foreach (float s in new[] { -1f, 1f })
                Block(t, "CanopyPost", new Vector3(s * (canopyW / 2f - 0.3f), 2.5f, hd + th + canopyD - 0.3f), new Vector3(0.22f, 5f, 0.22f), brass);
            for (int k = 0; k < 14; k++)
            {
                float x = (k + 0.5f) / 14f * canopyW - canopyW / 2f;
                Shape(t, "ChaseLight", GemMesh(), new Vector3(x, 4.75f, hd + th + canopyD + 0.02f), new Vector3(0.22f, 0.26f, 0.22f), k % 2 == 0 ? NeonMat(2) : glow, false, Quaternion.identity);
            }
            for (int k = 0; k < 6; k++)
            {
                float zz = hd + th + (k + 0.5f) / 6f * canopyD;
                foreach (float s in new[] { -1f, 1f })
                    Shape(t, "ChaseLight", GemMesh(), new Vector3(s * (canopyW / 2f + 0.02f), 4.75f, zz), new Vector3(0.22f, 0.26f, 0.22f), k % 2 == 0 ? glow : NeonMat(2), false, Quaternion.identity);
            }
            Deco(t, PrimitiveType.Cube, "RedCarpet", new Vector3(0, 0.03f, hd + th + canopyD / 2f + 0.2f), new Vector3(2.6f, 0.05f, canopyD + 1.6f), carpet, Quaternion.identity, false);
            // A brass door frame, revolving doors parked open either side.
            Deco(t, PrimitiveType.Cube, "DoorFrame", new Vector3(0, doorH + 0.15f, hd + th + 0.05f), new Vector3(doorW + 0.5f, 0.3f, 0.4f), brass, Quaternion.identity, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Block(t, "DoorJamb", new Vector3(s * (doorW / 2f + 0.12f), doorH / 2f, hd + th + 0.05f), new Vector3(0.25f, doorH, 0.4f), brass);
                Deco(t, PrimitiveType.Cube, "DoorLeaf", new Vector3(s * (doorW / 2f - 0.15f), 2.2f, hd + 0.3f), new Vector3(0.08f, 4f, 1.6f), PMat("HotelGlass", new Color(0.6f, 0.8f, 0.8f), 1f,
                    new Color(0.1f, 0.18f, 0.18f)), Quaternion.Euler(0, s * 20f, 0), false);
            }
            // Glowing cave mushrooms in brass planters by the door.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pot = new Vector3(s * (canopyW / 2f + 1.2f), 0f, hd + th + 1.2f);
                Shape(t, "Planter", CylMesh(), pot + new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.45f, 1.1f), brass, true, Quaternion.identity);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 sp = pot + Quaternion.Euler(0, k * 120f, 0) * new Vector3(0.25f, 0, 0);
                    Deco(t, PrimitiveType.Cylinder, "ShroomStem", sp + new Vector3(0, 1.2f + k * 0.15f, 0), new Vector3(0.1f, 0.35f + k * 0.08f, 0.1f),
                        PMat("ShroomStem", new Color(0.8f, 0.78f, 0.7f), 1f), Quaternion.identity, false);
                    Deco(t, PrimitiveType.Sphere, "ShroomCap", sp + new Vector3(0, 1.6f + k * 0.3f, 0), new Vector3(0.55f, 0.22f, 0.55f), NeonMat(1), Quaternion.identity, false);
                }
            }

            // ---- the front desk facing the door, the key cubbies behind it and the desk bell (it brings fresh mugs)
            float deskZ = HotelDeskZ(D), deskX = HotelDeskX(), deskLen = 8f;
            Block(t, "FrontDesk", new Vector3(deskX, 0.6f + HotelFloorY, deskZ), new Vector3(deskLen, 1.2f, 1f), darkWood);
            Deco(t, PrimitiveType.Cube, "DeskTop", new Vector3(deskX, 1.24f + HotelFloorY, deskZ), new Vector3(deskLen + 0.3f, 0.08f, 1.3f), plate, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "DeskTrim", new Vector3(deskX, 0.9f + HotelFloorY, deskZ + 0.52f), new Vector3(deskLen, 0.12f, 0.04f), gold, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "DeskNeon", new Vector3(deskX, 0.35f + HotelFloorY, deskZ + 0.52f), new Vector3(deskLen - 0.4f, 0.08f, 0.04f), NeonMat(1), Quaternion.identity, false);
            var deskSign = WorldText(t, "DeskSign", new Vector3(deskX, 1.65f + HotelFloorY, deskZ + 0.1f), Quaternion.Euler(0, 180f, 0), new Vector2(700, 110), 0.0035f, "FRONT DESK", 70);
            deskSign.color = new Color(1f, 0.85f, 0.45f);
            // Key cubbies on the back wall, a brass key in most of them.
            const int cubCols = 8, cubRows = 4;
            for (int r = 0; r < cubRows; r++)
                for (int k = 0; k < cubCols; k++)
                {
                    Vector3 at = new Vector3(deskX - 3.2f + k * 0.92f, 1.6f + r * 0.7f, -hd + 0.3f);
                    Deco(t, PrimitiveType.Cube, "Cubby", at, new Vector3(0.84f, 0.62f, 0.5f), darkWood, Quaternion.identity, false);
                    if ((r * 5 + k * 3) % 4 != 0)
                        Deco(t, PrimitiveType.Cube, "RoomKey", at + new Vector3(0, -0.1f, 0.27f), new Vector3(0.12f, 0.3f, 0.03f), brass, Quaternion.identity, false);
                }
            var mugs = new List<GameObject>();
            var bellGo = Shape(t, "DeskBell", BlobMesh(), new Vector3(deskX + deskLen / 2f - 0.6f, 1.42f + HotelFloorY, deskZ + 0.2f), new Vector3(0.36f, 0.3f, 0.36f), brass, false,
                Quaternion.identity);
            bellGo.isStatic = false;
            bellGo.AddComponent<SphereCollider>().radius = 0.6f;
            var bellAudio = bellGo.AddComponent<AudioSource>();
            bellAudio.playOnAwake = false;
            bellAudio.spatialBlend = 1f;
            bellAudio.maxDistance = 25f;
            bellAudio.clip = UiClip("ui_health_powerup_01");
            var reset = UdonSharpUndo.AddComponent<MugReset>(bellGo);
            reset.bell = bellAudio;
            SetInteractText(reset, "Ring for room service (fresh fizz)");
            behaviours.Add(reset);
            // Luggage carts by the desk.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 cart = new Vector3(deskX + s * (deskLen / 2f + 2f), HotelFloorY, deskZ + 1.6f);
                Deco(t, PrimitiveType.Cube, "CartBase", cart + new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.08f, 0.8f), plate, Quaternion.identity, false);
                foreach (float px in new[] { -0.55f, 0.55f })
                    Deco(t, PrimitiveType.Cylinder, "CartPost", cart + new Vector3(px, 1.2f, 0), new Vector3(0.05f, 0.9f, 0.05f), brass, Quaternion.identity, false);
                Deco(t, PrimitiveType.Cylinder, "CartBar", cart + new Vector3(0, 2.1f, 0), new Vector3(0.05f, 0.6f, 0.05f), brass, Quaternion.Euler(0, 0, 90f), false);
                Block(t, "Suitcase", cart + new Vector3(0, 0.62f, 0), new Vector3(0.9f, 0.55f, 0.6f), leather);
                Deco(t, PrimitiveType.Cube, "Suitcase", cart + new Vector3(0.1f, 1.1f, 0), new Vector3(0.7f, 0.42f, 0.5f), PMat("HotelCase", new Color(0.3f, 0.35f, 0.3f), 1f),
                    Quaternion.Euler(0, 12f, 0), false);
            }

            // ---- the jukebox: its screen high on the back wall over the desk, its lectern by the lounge
            var centre = Child(t, "JukeboxCentre", new Vector3(0, HotelFloorY, 0));
            BuildJukeboxAt(t, centre, new Vector3(deskX, 7.4f, -hd + 0.15f), Quaternion.Euler(0, 180f, 0), new Vector3(0, 3.5f, -hd + 1.5f),
                new Vector3(0, 5.2f, -hd + 0.9f), new Vector3(hw * 0.3f, HotelFloorY, -hd + 7f), Quaternion.Euler(0, 180f, 0), 0f,
                new Vector2(hw + 0.4f, hd + 0.4f), HotelWallH, "Hotel Jukebox", darkWood, darkWood, behaviours);

            // ---- the lounge bar down the right side, with stools, bottles, mugs and the mirror
            float barX = hw - HotelGalleryW - 1.4f, barLen = Mathf.Min(D * 0.45f, 11f), barZ = -hd + 7f + barLen / 2f;
            Block(t, "BarCounter", new Vector3(barX, 0.6f + HotelFloorY, barZ), new Vector3(0.9f, 1.2f, barLen), darkWood);
            Deco(t, PrimitiveType.Cube, "BarTop", new Vector3(barX, 1.24f + HotelFloorY, barZ), new Vector3(1.2f, 0.08f, barLen + 0.2f), plate, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cylinder, "FootRail", new Vector3(barX - 0.65f, 0.25f + HotelFloorY, barZ), new Vector3(0.07f, barLen / 2f, 0.07f), brass, Quaternion.Euler(90f, 0, 0), false);
            int stools = Mathf.Max(4, Mathf.FloorToInt(barLen / 1.6f));
            for (int i = 0; i < stools; i++)
            {
                float zz = barZ - barLen / 2f + (i + 0.5f) / stools * barLen;
                MakeSeat(t, "BarStool", new Vector3(barX - 1.5f, HotelFloorY, zz), Vector3.right, 0.78f, darkWood, brass, behaviours);
                if (i % 2 == 0) mugs.Add(MakeMug(t, new Vector3(barX - 0.2f, 1.29f + HotelFloorY, zz + 0.3f), pewter, foam));
            }
            float shelfX = hw - 0.35f;
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = 1.6f + shelf * 0.75f;
                Deco(t, PrimitiveType.Cube, "BottleShelf", new Vector3(shelfX, y, barZ), new Vector3(0.6f, 0.08f, barLen), darkWood, Quaternion.identity, false);
                for (int b = 0; b < Mathf.FloorToInt(barLen / 0.7f); b++)
                {
                    if ((b + shelf) % 4 == 3) continue;
                    float zz = barZ - barLen / 2f + 0.4f + b * 0.7f;
                    Shape(t, "Bottle", CylMesh(), new Vector3(shelfX, y + 0.3f, zz), new Vector3(0.18f, 0.25f, 0.18f), (b + shelf) % 2 == 0 ? bottleG : bottleB, false, Quaternion.identity);
                }
            }
            var mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = "Mirror";
            Object.DestroyImmediate(mirror.GetComponent<Collider>());
            mirror.transform.SetParent(t, false);
            mirror.transform.localPosition = new Vector3(hw - 0.08f, 3.5f, barZ);
            mirror.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            mirror.transform.localScale = new Vector3(Mathf.Min(barLen, 9f), 1.4f, 1f);
            Block(t, "MirrorFrame", new Vector3(hw - 0.05f, 3.5f, barZ), new Vector3(0.06f, 1.7f, Mathf.Min(barLen, 9f) + 0.3f), brass);
            var mirrorShader = Shader.Find("FX/MirrorReflection");
            if (mirrorShader != null)
            {
                var mirrorMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/TavernMirror.mat");
                if (mirrorMat == null)
                {
                    mirrorMat = new Material(mirrorShader);
                    AssetDatabase.CreateAsset(mirrorMat, MaterialsDir + "/TavernMirror.mat");
                }
                mirror.GetComponent<Renderer>().sharedMaterial = mirrorMat;
            }
            mirror.AddComponent<VRCMirrorReflection>();
            mirror.SetActive(false);
            var mirrorButton = Shape(t, "MirrorToggle", GemMesh(), new Vector3(barX - 0.1f, 1.55f + HotelFloorY, barZ - barLen / 2f + 0.4f), new Vector3(0.3f, 0.45f, 0.3f),
                PMat("MirrorGem", new Color(0.6f, 0.9f, 1f), 1f, new Color(0.3f, 0.55f, 0.7f)), false, Quaternion.identity);
            mirrorButton.isStatic = false;
            mirrorButton.AddComponent<SphereCollider>().radius = 0.5f;
            var mirrorToggle = UdonSharpUndo.AddComponent<LocalToggle>(mirrorButton);
            mirrorToggle.targets = new[] { mirror };
            SetInteractText(mirrorToggle, "Mirror on/off (just for you)");
            behaviours.Add(mirrorToggle);

            // ---- the fireplace on the left wall, armchairs round it, and more armchairs round low tables
            Vector3 fire = new Vector3(-hw + HotelGalleryW + 0.6f, HotelFloorY, 1f);
            Block(t, "Fireplace", fire + new Vector3(-0.5f, 1.4f, 0), new Vector3(1.2f, 2.8f, 3.6f), plate);
            Deco(t, PrimitiveType.Cube, "FireplaceMouth", fire + new Vector3(0.11f, 0.8f, 0), new Vector3(0.05f, 1.3f, 2f), PMat("TunnelDark", new Color(0.04f, 0.04f, 0.06f), 1f),
                Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "Mantel", fire + new Vector3(0.05f, 2.9f, 0), new Vector3(1.5f, 0.2f, 4f), brass, Quaternion.identity, false);
            PointLight(t, fire + new Vector3(1f, 1.2f, 0), new Color(1f, 0.55f, 0.2f), 10f, 1.6f);
            Light fireLight = t.GetChild(t.childCount - 1).GetComponent<Light>();
            fireLight.gameObject.name = "HearthLight";
            var flames = HearthFire(t, fire + new Vector3(0.2f, 0.25f, 0), Vector3.forward, Quaternion.LookRotation(Vector3.right), fireLight);
            flames.transform.localScale = Vector3.one * 0.55f;
            behaviours.Add(flames);
            foreach (float a in new[] { 45f, 90f, 135f })
            {
                Vector3 d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                MakeSeat(t, "Armchair", fire + d * 3f, -d, 0.55f, leather, brass, behaviours);
            }
            for (int k = 0; k < 2; k++)
            {
                Vector3 tp = new Vector3(k == 0 ? -hw * 0.42f : hw * 0.3f, HotelFloorY, hd - 5f);
                Block(t, "LowTable", tp + new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.1f, 1.4f), darkWood);
                Deco(t, PrimitiveType.Cylinder, "LowTableLeg", tp + new Vector3(0, 0.22f, 0), new Vector3(0.2f, 0.22f, 0.2f), brass, Quaternion.identity, false);
                mugs.Add(MakeMug(t, tp + new Vector3(0.3f, 0.55f, 0.3f), pewter, foam));
                foreach (var d in new[] { Vector3.left, Vector3.right })
                    MakeSeat(t, "Armchair", tp + d * 1.5f, -d, 0.55f, leather, brass, behaviours);
            }
            reset.mugs = mugs.ToArray();

            // ---- the galleries up both sides (stairs from the front), room doors along them, balconies a floor above
            float gw = HotelGalleryW, gLen = D - 7f, gMid = -hd + gLen / 2f;
            foreach (float s in new[] { -1f, 1f })
            {
                Block(t, "Gallery", new Vector3(s * (hw - gw / 2f), HotelGalleryY, gMid), new Vector3(gw, 0.3f, gLen), planks);
                Block(t, "GalleryRail", new Vector3(s * (hw - gw), HotelGalleryY + 0.6f, gMid), new Vector3(0.12f, 1.1f, gLen), brass);
                Block(t, "GalleryRailBack", new Vector3(s * (hw - gw / 2f), HotelGalleryY + 0.6f, -hd + 0.1f), new Vector3(gw, 1.1f, 0.12f), brass);
                for (float zz = -hd + gw; zz <= gMid + gLen / 2f + 0.01f; zz += 4f)
                    Block(t, "GalleryPost", new Vector3(s * (hw - gw), HotelGalleryY / 2f, zz), new Vector3(0.25f, HotelGalleryY, 0.25f), brass);
                // The stairs: a ramp from the floor by the front up to the gallery's open end.
                float stairZ0 = gMid + gLen / 2f, stairLen = Mathf.Min(6.5f, hd - 0.6f - stairZ0);
                float stairAngle = Mathf.Atan2(HotelGalleryY, stairLen) * Mathf.Rad2Deg;
                float rampLen = Mathf.Sqrt(stairLen * stairLen + HotelGalleryY * HotelGalleryY);
                Block(t, "StairRamp", new Vector3(s * (hw - gw / 2f), HotelGalleryY / 2f, stairZ0 + stairLen / 2f), new Vector3(gw - 0.4f, 0.2f, rampLen), carpet, Quaternion.Euler(stairAngle, 0, 0));
                for (int k = 0; k < 14; k++)
                {
                    float f = (k + 0.5f) / 14f;
                    Deco(t, PrimitiveType.Cube, "Step", new Vector3(s * (hw - gw / 2f), f * HotelGalleryY + 0.1f, stairZ0 + stairLen * (1f - f)), new Vector3(gw - 0.4f, 0.06f, stairLen / 14f * 0.6f),
                        brass, Quaternion.identity, false);
                }
                Block(t, "StairRail", new Vector3(s * (hw - gw + 0.1f), HotelGalleryY / 2f + 1f, stairZ0 + stairLen / 2f), new Vector3(0.1f, 0.9f, rampLen), brass, Quaternion.Euler(stairAngle, 0, 0));
                // The balconies a floor up (a ledge and a rail: rooms you can see but not reach).
                Deco(t, PrimitiveType.Cube, "Balcony", new Vector3(s * (hw - 0.9f), HotelBalconyY, gMid), new Vector3(1.8f, 0.25f, gLen), planks, Quaternion.identity, false);
                Deco(t, PrimitiveType.Cube, "BalconyRail", new Vector3(s * (hw - 1.8f), HotelBalconyY + 0.6f, gMid), new Vector3(0.1f, 1f, gLen), brass, Quaternion.identity, false);
                // Room doors along each floor, numbered.
                int doors = Mathf.Max(2, Mathf.FloorToInt(gLen / 4.5f));
                for (int floor = 0; floor < 2; floor++)
                {
                    float fy = floor == 0 ? HotelGalleryY : HotelBalconyY;
                    for (int k = 0; k < doors; k++)
                    {
                        float zz = -hd + 2.5f + (k + 0.5f) / doors * (gLen - 3f);
                        Vector3 door = new Vector3(s * (hw - 0.06f), fy + 1.3f, zz);
                        Deco(t, PrimitiveType.Cube, "RoomDoor", door, new Vector3(0.08f, 2.4f, 1.2f), darkWood, Quaternion.identity, false);
                        Deco(t, PrimitiveType.Cube, "RoomDoorFrame", door + new Vector3(0, 1.3f, 0), new Vector3(0.1f, 0.12f, 1.4f), brass, Quaternion.identity, false);
                        Deco(t, PrimitiveType.Sphere, "RoomDoorKnob", door + new Vector3(-s * 0.08f, -0.1f, 0.4f), Vector3.one * 0.1f, brass, Quaternion.identity, false);
                        Shape(t, "RoomLamp", GemMesh(), door + new Vector3(-s * 0.15f, 1.6f, 0.9f), new Vector3(0.18f, 0.26f, 0.18f), glow, false, Quaternion.identity);
                        int number = (floor + 1) * 100 + (s < 0f ? 0 : doors) + k + 1;
                        var plateText = WorldText(t, "RoomNumber", door + new Vector3(-s * 0.06f, 0.75f, 0), Quaternion.Euler(0, s > 0 ? 90f : -90f, 0), new Vector2(200, 80), 0.0028f,
                            number.ToString(), 60);
                        plateText.color = new Color(1f, 0.85f, 0.45f);
                    }
                }
            }

            // ---- a brass cage lift in the back left corner (it doesn't go anywhere: the lift kobold is on strike)
            Vector3 lift = new Vector3(-hw + gw + 1.8f, HotelFloorY, -hd + 1.8f);
            foreach (float lx in new[] { -1.2f, 1.2f })
                foreach (float lz in new[] { -1.2f, 1.2f })
                    Block(t, "LiftPost", lift + new Vector3(lx, HotelWallH / 2f, lz), new Vector3(0.12f, HotelWallH, 0.12f), brass);
            for (float y = 0.5f; y < HotelWallH; y += 1.2f)
                foreach (float s in new[] { -1f, 1f })
                {
                    Deco(t, PrimitiveType.Cube, "LiftBar", lift + new Vector3(s * 1.2f, y, 0), new Vector3(0.06f, 0.06f, 2.4f), brass, Quaternion.identity, false);
                    Deco(t, PrimitiveType.Cube, "LiftBar", lift + new Vector3(0, y, s * 1.2f), new Vector3(2.4f, 0.06f, 0.06f), brass, Quaternion.identity, false);
                }
            Block(t, "LiftCar", lift + new Vector3(0, 7.5f, 0), new Vector3(2f, 2.6f, 2f), darkWood);
            Shape(t, "LiftLamp", GemMesh(), lift + new Vector3(0, 9f, 1.05f), new Vector3(0.25f, 0.3f, 0.25f), NeonMat(4), false, Quaternion.identity);
            Deco(t, PrimitiveType.Cylinder, "LiftCable", lift + new Vector3(0, 11.4f, 0), new Vector3(0.06f, 2.6f, 0.06f), iron, Quaternion.identity, false);
            var liftSign = WorldText(t, "LiftSign", lift + new Vector3(0, 2.6f, 1.3f), Quaternion.Euler(0, 180f, 0), new Vector2(800, 120), 0.0028f, "LIFT OUT OF ORDER\n(operator on strike)", 46);
            liftSign.color = new Color(1f, 0.6f, 0.4f);

            // ---- chandeliers over the atrium, and lights
            for (int k = 0; k < 2; k++)
            {
                Vector3 ch = new Vector3(0, 10.4f, -hd * 0.35f + k * hd * 0.8f);
                Deco(t, PrimitiveType.Cylinder, "ChandelierChain", ch + new Vector3(0, (HotelWallH - 10.4f) / 2f, 0), new Vector3(0.06f, (HotelWallH - 10.4f) / 2f, 0.06f), iron,
                    Quaternion.identity, false);
                Deco(t, PrimitiveType.Cylinder, "ChandelierRing", ch, new Vector3(3.2f, 0.08f, 3.2f), gold, Quaternion.identity, false);
                Deco(t, PrimitiveType.Cylinder, "ChandelierRing", ch - new Vector3(0, 0.8f, 0), new Vector3(2f, 0.06f, 2f), gold, Quaternion.identity, false);
                Shape(t, "ChandelierCrystal", GemMesh(), ch - new Vector3(0, 1.5f, 0), new Vector3(0.8f, 1.4f, 0.8f), crystal, false, Quaternion.identity);
                for (int sp = 0; sp < 8; sp++)
                    Shape(t, "ChandelierCandle", GemMesh(), ch + Quaternion.Euler(0, sp * 45f, 0) * new Vector3(1.55f, 0.25f, 0), new Vector3(0.22f, 0.32f, 0.22f), glow, false, Quaternion.identity);
                PointLight(t, ch - new Vector3(0, 1.2f, 0), new Color(1f, 0.85f, 0.65f), 16f, 1.3f);
            }
            PointLight(t, new Vector3(barX - 1.5f, 3.5f, barZ), new Color(1f, 0.7f, 0.45f), 10f, 1f);
            PointLight(t, new Vector3(deskX, 3.4f, deskZ + 2f), new Color(1f, 0.8f, 0.55f), 10f, 1f);
            for (float zz = -hd + 3f; zz < hd - 2f; zz += 6f)
                foreach (float s in new[] { -1f, 1f })
                    Shape(t, "WallLamp", GemMesh(), new Vector3(s * (hw - 0.3f), 3.2f, zz), new Vector3(0.28f, 0.4f, 0.28f), glow, false, Quaternion.identity);
        }
    }
}
