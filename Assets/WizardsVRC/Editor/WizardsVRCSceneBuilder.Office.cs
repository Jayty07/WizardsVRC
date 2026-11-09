using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Headmaster's office, furnished to look lived in (office-local: floor at y 0, x -7..7, z -6 (the back wall,
    /// behind the Headmaster) .. 6 (the door); the select canvas hangs in the middle of the east wall, so only its corners
    /// are furnished). What's in it:
    ///   - Back wall: two floor-to-ceiling bookcases crammed with books (some leaning, some stacked flat) and curios,
    ///     a library ladder, the seven schools' crests over them, and between them a tall arched window with a cushioned
    ///     window seat and the daylight falling in across the floor.
    ///   - The desk: drawers and a gold star on its front, and the clutter of a busy Headmaster: papers, an open book,
    ///     an inkwell and quill, sealed letters, a teacup, a candle, an hourglass, books piled up, the scrying orb on its
    ///     stand. His tall velvet chair behind, his owl asleep-eyed on a perch beside him.
    ///   - West wall: a stone fireplace with a real crackling fire, a mantel of clock, candlesticks and keepsakes, a portrait
    ///     of the first Headmaster over it, firewood, fire irons, a hearth rug with the office cat curled up on it, and two
    ///     armchairs you can sit in round a little tea table.
    ///   - North wall by the door: a potion cabinet of glowing bottles; a brass orrery turning on its table under a star
    ///     chart. East corners: a ticking grandfather clock, a coat stand with his travelling cloak and hat.
    ///   - Overhead: beams, a candle chandelier and candles floating over the bookcases. A trunk, a basket of scrolls and a
    ///     pile of books on the floor.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static void FurnishOffice(Transform office, Material trim, Material gold, Material wood, Material window, float roomH)
        {
            var darkWood = PMat("OfficeDarkWood", new Color(0.27f, 0.15f, 0.08f), 1f);
            var velvet = PMat("OfficeVelvet", new Color(0.34f, 0.14f, 0.48f), 1f);
            var leather = PMat("OfficeLeather", new Color(0.5f, 0.16f, 0.1f), 1f);
            var paper = PMat("OfficePaper", new Color(0.94f, 0.9f, 0.77f), 1f);
            var brass = PMat("OfficeBrass", new Color(0.82f, 0.62f, 0.26f), 1f, new Color(0.1f, 0.07f, 0.02f));
            var iron = PMat("OfficeIron", new Color(0.2f, 0.19f, 0.2f), 1f);
            var stone = PMat("OfficeStone", new Color(0.56f, 0.53f, 0.5f), 2f);
            var soot = PMat("OfficeSoot", new Color(0.08f, 0.07f, 0.07f), 1f);
            var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
            var wax = PMat("CandleWax", new Color(0.9f, 0.88f, 0.8f), 1f);
            var porcelain = PMat("OfficePorcelain", new Color(0.95f, 0.95f, 0.98f), 1f);
            var crystal = PMat("Crystal_9FD8FF", new Color(0.62f, 0.85f, 1f), 1f, new Color(0.37f, 0.5f, 0.6f));
            Color[] bookCols =
            {
                new Color(0.62f, 0.14f, 0.12f), new Color(0.16f, 0.26f, 0.58f), new Color(0.18f, 0.44f, 0.22f), new Color(0.48f, 0.3f, 0.14f),
                new Color(0.42f, 0.16f, 0.46f), new Color(0.76f, 0.6f, 0.26f), new Color(0.12f, 0.38f, 0.42f), new Color(0.3f, 0.12f, 0.1f),
                new Color(0.66f, 0.42f, 0.2f), new Color(0.2f, 0.2f, 0.24f),
            };
            var books = new Material[bookCols.Length];
            for (int i = 0; i < books.Length; i++) books[i] = PMat("OfficeBook" + i, bookCols[i], 1f);
            var animated = new List<Transform>();

            // ---------------------------------------------------------------- floor and ceiling
            Block(office, "RugBorder", new Vector3(0, 0.015f, -0.6f), new Vector3(7.6f, 0.03f, 6.4f), PMat("OfficeRugGold", new Color(0.72f, 0.55f, 0.2f), 2f));
            Block(office, "Rug", new Vector3(0, 0.025f, -0.6f), new Vector3(7.1f, 0.03f, 5.9f), PMat("OfficeRug", new Color(0.2f, 0.17f, 0.5f), 2f));
            Deco(office, PrimitiveType.Cube, "RugStar", new Vector3(0, 0.045f, -0.6f), new Vector3(1.4f, 0.01f, 1.4f), PMat("OfficeRugStar", new Color(0.5f, 0.42f, 0.8f), 1f), Quaternion.Euler(0, 45f, 0), false);
            foreach (float z in new[] { -4f, 0f, 4f })
                Deco(office, PrimitiveType.Cube, "CeilingBeam", new Vector3(0, roomH - 0.18f, z), new Vector3(14f, 0.32f, 0.32f), darkWood, Quaternion.identity, false);

            // ---------------------------------------------------------------- back wall: bookcases, window, crests
            foreach (float side in new[] { -1f, 1f })
                Bookcase(office, new Vector3(side * 4.35f, 0, -5.68f), 4.7f, 5.6f, darkWood, gold, books, paper, brass, crystal, wax, flame);
            // The library ladder leaning on the right-hand case.
            {
                var ladder = Child(office, "LibraryLadder", new Vector3(5.1f, 0, -4.45f));
                ladder.localRotation = Quaternion.Euler(-10.5f, 0, 0);
                foreach (float x in new[] { -0.28f, 0.28f }) Deco(ladder, PrimitiveType.Cube, "LadderRail", new Vector3(x, 2.5f, 0), new Vector3(0.07f, 5f, 0.07f), wood, Quaternion.identity, false);
                for (int r = 0; r < 12; r++) Deco(ladder, PrimitiveType.Cube, "LadderRung", new Vector3(0, 0.3f + r * 0.4f, 0), new Vector3(0.56f, 0.05f, 0.06f), wood, Quaternion.identity, false);
            }
            // The tall window between the cases, arched at the top, with mullions, a cushioned seat and daylight falling in.
            Block(office, "Window", new Vector3(0, 3.5f, -5.93f), new Vector3(3.1f, 3.8f, 0.06f), window);
            Deco(office, PrimitiveType.Cube, "WindowMullion", new Vector3(0, 3.5f, -5.88f), new Vector3(0.08f, 3.8f, 0.06f), darkWood, Quaternion.identity, false);
            foreach (float y in new[] { 2.5f, 3.9f })
                Deco(office, PrimitiveType.Cube, "WindowTransom", new Vector3(0, y, -5.88f), new Vector3(3.1f, 0.08f, 0.06f), darkWood, Quaternion.identity, false);
            foreach (float x in new[] { -1.62f, 1.62f })
                Deco(office, PrimitiveType.Cube, "WindowJamb", new Vector3(x, 3.5f, -5.86f), new Vector3(0.16f, 3.9f, 0.14f), gold, Quaternion.identity, false);
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                Deco(office, PrimitiveType.Cube, "WindowArch", new Vector3(Mathf.Cos(a) * 1.6f, 5.4f + Mathf.Sin(a) * 0.75f, -5.86f), new Vector3(0.62f, 0.14f, 0.14f), gold,
                    Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f), false);
            }
            Block(office, "WindowArchGlass", new Vector3(0, 5.65f, -5.94f), new Vector3(2.6f, 0.6f, 0.05f), window);
            Block(office, "WindowSeat", new Vector3(0, 0.3f, -5.55f), new Vector3(3.2f, 0.6f, 0.85f), darkWood);
            Deco(office, PrimitiveType.Cube, "WindowSeatCushion", new Vector3(0, 0.66f, -5.55f), new Vector3(3.1f, 0.12f, 0.8f), velvet, Quaternion.identity, false);
            Shape(office, "SeatPillow", BlobMesh(), new Vector3(-1.1f, 0.88f, -5.75f), new Vector3(0.55f, 0.4f, 0.22f), PMat("OfficePillowGold", new Color(0.85f, 0.68f, 0.3f), 1f), false, Quaternion.Euler(-12f, 15f, 0));
            Shape(office, "SeatPillow", BlobMesh(), new Vector3(1.15f, 0.86f, -5.75f), new Vector3(0.5f, 0.38f, 0.2f), PMat("OfficePillowTeal", new Color(0.2f, 0.5f, 0.52f), 1f), false, Quaternion.Euler(-12f, -20f, 0));
            OpenBook(office, new Vector3(0.3f, 0.73f, -5.45f), 20f, leather, paper);
            LightShaft(office, "WindowDaylight", new Vector3(0, 4.6f, -5.85f), new Vector3(0.5f, 0.02f, -2.9f), 2.6f, ShaftMat("OfficeDaylight", new Color(1f, 0.92f, 0.72f, 0.2f)));
            // The seven schools' crests in a row above the cases and the window.
            for (int s = 0; s < 7; s++)
            {
                Vector3 p = new Vector3(-5.4f + s * 1.8f, 6.25f, -5.9f);
                Color c = SchoolColors[s];
                Shape(office, "SchoolCrest", GemMesh(), p, new Vector3(0.7f, 0.85f, 0.12f), PMat("OfficeCrest" + SchoolLabels[s], c, 1f, c * 0.25f), false, Quaternion.identity);
                Shape(office, "SchoolCrestRim", GemMesh(), p + new Vector3(0, 0, 0.02f), new Vector3(0.82f, 0.98f, 0.08f), gold, false, Quaternion.identity);
            }

            // ---------------------------------------------------------------- the desk and the Headmaster's corner
            Block(office, "DeskTop", new Vector3(0, 0.98f, -2.2f), new Vector3(3.4f, 0.08f, 1.4f), darkWood);
            Deco(office, PrimitiveType.Cube, "DeskEdge", new Vector3(0, 0.93f, -2.2f), new Vector3(3.46f, 0.04f, 1.46f), gold, Quaternion.identity, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Block(office, "DeskPedestal", new Vector3(side * 1.22f, 0.46f, -2.2f), new Vector3(0.84f, 0.92f, 1.26f), wood);
                for (int d = 0; d < 3; d++)
                {
                    // Drawers face the Headmaster's side (-z).
                    Deco(office, PrimitiveType.Cube, "Drawer", new Vector3(side * 1.22f, 0.2f + d * 0.28f, -2.84f), new Vector3(0.7f, 0.22f, 0.03f), darkWood, Quaternion.identity, false);
                    Shape(office, "DrawerKnob", BlobMesh(), new Vector3(side * 1.22f, 0.2f + d * 0.28f, -2.87f), Vector3.one * 0.06f, gold, false, Quaternion.identity);
                }
            }
            Block(office, "DeskFront", new Vector3(0, 0.55f, -1.56f), new Vector3(1.6f, 0.75f, 0.06f), wood);
            Shape(office, "DeskStar", GemMesh(), new Vector3(0, 0.58f, -1.52f), new Vector3(0.36f, 0.42f, 0.05f), gold, false, Quaternion.identity);
            float top = 1.02f;
            // The scrying orb on its brass stand.
            Shape(office, "OrbStand", CylMesh(), new Vector3(-1.15f, top + 0.08f, -2.35f), new Vector3(0.26f, 0.08f, 0.26f), brass, false, Quaternion.identity);
            foreach (float a in new[] { 0f, 120f, 240f })
                Deco(office, PrimitiveType.Cube, "OrbClaw", new Vector3(-1.15f, top + 0.2f, -2.35f) + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.13f), new Vector3(0.03f, 0.16f, 0.03f), brass, Quaternion.Euler(0, a, 0) * Quaternion.Euler(20f, 0, 0), false);
            Shape(office, "DeskOrb", BlobMesh(), new Vector3(-1.15f, top + 0.4f, -2.35f), Vector3.one * 0.36f, crystal, false, Quaternion.identity);
            // Papers, a letter pile with a red seal, an open book, ink and quill.
            for (int i = 0; i < 4; i++)
                Deco(office, PrimitiveType.Cube, "Paper", new Vector3(0.3f + i * 0.02f, top + 0.005f + i * 0.008f, -2.05f), new Vector3(0.42f, 0.006f, 0.56f), paper, Quaternion.Euler(0, -8f + i * 7f, 0), false);
            for (int i = 0; i < 3; i++)
                Deco(office, PrimitiveType.Cube, "Letter", new Vector3(-0.7f, top + 0.005f + i * 0.012f, -1.8f), new Vector3(0.3f, 0.01f, 0.2f), paper, Quaternion.Euler(0, 10f - i * 9f, 0), false);
            Shape(office, "WaxSeal", CylMesh(), new Vector3(-0.7f, top + 0.045f, -1.8f), new Vector3(0.07f, 0.008f, 0.07f), PMat("OfficeSealWax", new Color(0.7f, 0.08f, 0.08f), 1f), false, Quaternion.identity);
            OpenBook(office, new Vector3(-0.28f, top + 0.02f, -2.15f), -6f, books[1], paper);
            Shape(office, "Inkwell", CylMesh(), new Vector3(0.82f, top + 0.06f, -2.0f), new Vector3(0.12f, 0.06f, 0.12f), PMat("OfficeInk", new Color(0.05f, 0.06f, 0.12f), 1f), false, Quaternion.identity);
            Shape(office, "Quill", ConeMesh(), new Vector3(0.86f, top + 0.3f, -2.02f), new Vector3(0.06f, 0.45f, 0.02f), porcelain, false, Quaternion.Euler(0, 30f, -16f));
            // A teacup on its saucer, a candle, an hourglass, a pile of books.
            Shape(office, "Saucer", CylMesh(), new Vector3(1.1f, top + 0.01f, -1.8f), new Vector3(0.2f, 0.01f, 0.2f), porcelain, false, Quaternion.identity);
            Shape(office, "Teacup", CylMesh(), new Vector3(1.1f, top + 0.06f, -1.8f), new Vector3(0.11f, 0.05f, 0.11f), porcelain, false, Quaternion.identity);
            Shape(office, "Tea", CylMesh(), new Vector3(1.1f, top + 0.105f, -1.8f), new Vector3(0.09f, 0.004f, 0.09f), PMat("OfficeTea", new Color(0.45f, 0.24f, 0.1f), 1f), false, Quaternion.identity);
            Candle(office, new Vector3(1.4f, top, -2.55f), 0.22f, brass, wax, flame);
            Hourglass(office, new Vector3(-1.5f, top, -1.85f), brass);
            for (int i = 0; i < 4; i++)
                Deco(office, PrimitiveType.Cube, "DeskBook", new Vector3(1.35f, top + 0.04f + i * 0.08f, -2.1f), new Vector3(0.36f, 0.08f, 0.5f), books[(i * 3 + 2) % books.Length], Quaternion.Euler(0, i * 11f - 10f, 0), false);
            PointLight(office, new Vector3(1.4f, 1.6f, -2.5f), new Color(1f, 0.75f, 0.45f), 3.5f, 0.8f);

            // His high-backed chair behind him.
            {
                var chair = Child(office, "HeadmasterChair", new Vector3(0, 0, -4.45f));
                Block(chair, "Seat", new Vector3(0, 0.5f, 0), new Vector3(1.0f, 0.14f, 0.85f), darkWood);
                Deco(chair, PrimitiveType.Cube, "SeatCushion", new Vector3(0, 0.62f, 0), new Vector3(0.92f, 0.1f, 0.78f), velvet, Quaternion.identity, false);
                Block(chair, "Back", new Vector3(0, 1.55f, -0.38f), new Vector3(1.0f, 2.2f, 0.12f), darkWood);
                Deco(chair, PrimitiveType.Cube, "BackCushion", new Vector3(0, 1.45f, -0.31f), new Vector3(0.78f, 1.6f, 0.06f), velvet, Quaternion.identity, false);
                foreach (float x in new[] { -0.46f, 0.46f })
                {
                    Shape(chair, "Finial", GemMesh(), new Vector3(x, 2.78f, -0.38f), new Vector3(0.16f, 0.28f, 0.16f), gold, false, Quaternion.identity);
                    Deco(chair, PrimitiveType.Cube, "Arm", new Vector3(x, 0.85f, 0), new Vector3(0.1f, 0.08f, 0.8f), darkWood, Quaternion.identity, false);
                    foreach (float z in new[] { -0.36f, 0.36f })
                        Deco(chair, PrimitiveType.Cube, "Leg", new Vector3(x, 0.22f, z), new Vector3(0.1f, 0.44f, 0.1f), darkWood, Quaternion.identity, false);
                }
                Shape(chair, "Crest", GemMesh(), new Vector3(0, 2.72f, -0.38f), new Vector3(0.34f, 0.4f, 0.08f), crystal, false, Quaternion.identity);
            }
            // His owl on its perch, watching the door.
            Owl(office, new Vector3(2.75f, 0, -4.1f), wood, brass, animated);

            // ---------------------------------------------------------------- west wall: the fireplace and its armchairs
            {
                Vector3 hc = new Vector3(-6.62f, 0, 2.0f);   // the hearth's centre against the wall
                Block(office, "ChimneyBreast", new Vector3(-6.72f, (2.7f + roomH) / 2f, 2.0f), new Vector3(0.56f, roomH - 2.7f, 3.0f), stone);
                foreach (float s in new[] { -1f, 1f })
                    Block(office, "HearthPillar", new Vector3(-6.62f, 1.25f, 2.0f + s * 1.3f), new Vector3(0.75f, 2.5f, 0.5f), stone);
                Block(office, "HearthLintel", new Vector3(-6.62f, 2.3f, 2.0f), new Vector3(0.75f, 0.4f, 2.1f), stone);
                Block(office, "Mantel", new Vector3(-6.5f, 2.56f, 2.0f), new Vector3(1.0f, 0.12f, 3.5f), darkWood);
                Deco(office, PrimitiveType.Cube, "Firebox", new Vector3(-6.96f, 1.05f, 2.0f), new Vector3(0.08f, 2.1f, 2.1f), soot, Quaternion.identity, false);
                Block(office, "Hearthstone", new Vector3(-6.2f, 0.04f, 2.0f), new Vector3(1.4f, 0.08f, 3.4f), stone);
                foreach (float z in new[] { -0.55f, -0.2f, 0.15f, 0.5f })
                    Deco(office, PrimitiveType.Cube, "Grate", new Vector3(-6.4f, 0.3f, 2.0f + z), new Vector3(0.6f, 0.06f, 0.05f), iron, Quaternion.identity, false);
                PointLight(office, new Vector3(-5.6f, 1.2f, 2f), new Color(1f, 0.55f, 0.2f), 7f, 1.4f);
                Light fireLight = office.GetChild(office.childCount - 1).GetComponent<Light>();
                fireLight.gameObject.name = "HearthLight";
                var fire = HearthFire(office, hc + new Vector3(0.12f, 0.12f, 0), Vector3.forward, Quaternion.LookRotation(Vector3.right), fireLight);
                fire.transform.localScale = Vector3.one * 0.55f;
                ExtraBehaviours.Add(fire);

                // The mantel: a clock in the middle, candlesticks, keepsakes.
                float mt = 2.62f;
                Block(office, "MantelClock", new Vector3(-6.55f, mt + 0.28f, 2.0f), new Vector3(0.3f, 0.56f, 0.5f), darkWood);
                Shape(office, "MantelClockFace", CylMesh(), new Vector3(-6.39f, mt + 0.36f, 2.0f), new Vector3(0.32f, 0.01f, 0.32f), porcelain, false, Quaternion.Euler(0, 0, 90f));
                Shape(office, "MantelClockRim", CylMesh(), new Vector3(-6.395f, mt + 0.36f, 2.0f), new Vector3(0.38f, 0.008f, 0.38f), gold, false, Quaternion.Euler(0, 0, 90f));
                Deco(office, PrimitiveType.Cube, "ClockHand", new Vector3(-6.38f, mt + 0.4f, 2.0f), new Vector3(0.01f, 0.1f, 0.015f), soot, Quaternion.identity, false);
                Deco(office, PrimitiveType.Cube, "ClockHand", new Vector3(-6.38f, mt + 0.36f, 2.04f), new Vector3(0.01f, 0.015f, 0.09f), soot, Quaternion.identity, false);
                foreach (float z in new[] { 0.85f, 3.15f }) Candle(office, new Vector3(-6.5f, mt, z), 0.35f, brass, wax, flame);
                PictureFrame(office, new Vector3(-6.62f, mt + 0.2f, 1.25f), 0.32f, 0.26f, gold, PMat("OfficeFramePic1", new Color(0.3f, 0.45f, 0.6f), 1f), -1f);
                PictureFrame(office, new Vector3(-6.62f, mt + 0.17f, 2.75f), 0.24f, 0.3f, gold, PMat("OfficeFramePic2", new Color(0.55f, 0.42f, 0.3f), 1f), -1f);
                Shape(office, "MantelVase", BlobMesh(), new Vector3(-6.5f, mt + 0.16f, 3.45f), new Vector3(0.18f, 0.3f, 0.18f), PMat("OfficeVase", new Color(0.22f, 0.4f, 0.6f), 1f), false, Quaternion.identity);
                Crystal(office, new Vector3(-6.5f, mt + 0.12f, 0.6f), 0.14f, new Color(0.75f, 0.55f, 1f));

                // The first Headmaster's portrait over the mantel, and the plate beneath it.
                Portrait(office, new Vector3(-6.4f, 4.45f, 2.0f), gold);
                WorldText(office, "PortraitPlate", new Vector3(-6.38f, 3.3f, 2.0f), Quaternion.Euler(0, 90f, 0), new Vector2(600, 70), 0.0025f, "Aldous Wyndhallow, first Headmaster", 40)
                    .color = new Color(1f, 0.86f, 0.5f);

                // Firewood stacked on the left, fire irons on the right.
                var bark = PsxTexMat("HearthLog", new Color(0.4f, 0.26f, 0.16f), TileTexture(StreetTexDir + "/Bark.png"), 1f);
                int[] row = { 3, 2, 1 };
                for (int r = 0; r < row.Length; r++)
                    for (int i = 0; i < row[r]; i++)
                        Shape(office, "Firewood", CylMesh(), new Vector3(-6.35f, 0.12f + r * 0.21f, -0.15f + (i - (row[r] - 1) / 2f) * 0.23f), new Vector3(0.22f, 0.4f, 0.22f), bark, false, Quaternion.Euler(0, 0, 90f));
                Shape(office, "FireIronStand", CylMesh(), new Vector3(-6.3f, 0.08f, 3.95f), new Vector3(0.24f, 0.04f, 0.24f), iron, false, Quaternion.identity);
                Deco(office, PrimitiveType.Cube, "FireIronPost", new Vector3(-6.3f, 0.5f, 3.95f), new Vector3(0.04f, 0.9f, 0.04f), iron, Quaternion.identity, false);
                foreach (float a in new[] { -12f, 10f, 24f })
                    Deco(office, PrimitiveType.Cube, "FireIron", new Vector3(-6.3f, 0.5f, 3.95f) + Quaternion.Euler(a, 0, 0) * new Vector3(0, 0, 0.05f), new Vector3(0.03f, 0.95f, 0.03f), brass, Quaternion.Euler(a, 0, 0), false);

                // The hearth rug, the cat asleep on it, and two armchairs round a tea table.
                Block(office, "HearthRug", new Vector3(-5.0f, 0.02f, 2.0f), new Vector3(1.8f, 0.03f, 2.8f), PMat("OfficeHearthRug", new Color(0.55f, 0.16f, 0.14f), 2f));
                Deco(office, PrimitiveType.Cube, "HearthRugStripe", new Vector3(-5.0f, 0.04f, 2.0f), new Vector3(1.5f, 0.01f, 0.2f), PMat("OfficeRugGold", new Color(0.72f, 0.55f, 0.2f), 2f), Quaternion.identity, false);
                Cat(office, new Vector3(-5.3f, 0.04f, 2.55f), animated);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 at = new Vector3(-3.95f, 0, 2.0f + s * 1.35f);
                    Vector3 face = (new Vector3(-6.2f, 0, 2.0f) - at).normalized;
                    MakeChair(office, "Armchair", at, face, velvet, darkWood, gold, ExtraBehaviours, 0.85f, true);
                }
                Shape(office, "TeaTable", CylMesh(), new Vector3(-3.7f, 0.62f, 2.0f), new Vector3(0.75f, 0.03f, 0.75f), darkWood, false, Quaternion.identity);
                Shape(office, "TeaTableLeg", CylMesh(), new Vector3(-3.7f, 0.31f, 2.0f), new Vector3(0.1f, 0.31f, 0.1f), darkWood, false, Quaternion.identity);
                Shape(office, "TeaTableFoot", CylMesh(), new Vector3(-3.7f, 0.03f, 2.0f), new Vector3(0.45f, 0.03f, 0.45f), darkWood, false, Quaternion.identity);
                Shape(office, "Teapot", BlobMesh(), new Vector3(-3.7f, 0.76f, 2.0f), new Vector3(0.24f, 0.2f, 0.24f), porcelain, false, Quaternion.identity);
                Shape(office, "TeapotSpout", ConeMesh(), new Vector3(-3.7f, 0.8f, 2.17f), new Vector3(0.05f, 0.16f, 0.05f), porcelain, false, Quaternion.Euler(50f, 0, 0));
                Shape(office, "TeapotLid", BlobMesh(), new Vector3(-3.7f, 0.87f, 2.0f), new Vector3(0.1f, 0.06f, 0.1f), gold, false, Quaternion.identity);
                foreach (float z in new[] { 1.72f, 2.3f })
                    Shape(office, "Cup", CylMesh(), new Vector3(-3.55f, 0.68f, z), new Vector3(0.08f, 0.04f, 0.08f), porcelain, false, Quaternion.identity);
            }

            // ---------------------------------------------------------------- north wall, either side of the door
            PotionCabinet(office, new Vector3(-3.1f, 0, 5.62f), darkWood, gold);
            {
                // The orrery on its table, under a star chart.
                Vector3 ot = new Vector3(3.2f, 0, 4.9f);
                Shape(office, "OrreryTable", CylMesh(), ot + new Vector3(0, 0.82f, 0), new Vector3(1.1f, 0.04f, 1.1f), darkWood, true, Quaternion.identity);
                Shape(office, "OrreryTableLeg", CylMesh(), ot + new Vector3(0, 0.4f, 0), new Vector3(0.14f, 0.4f, 0.14f), darkWood, false, Quaternion.identity);
                foreach (float a in new[] { 0f, 120f, 240f })
                    Deco(office, PrimitiveType.Cube, "OrreryTableFoot", ot + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.05f, 0.28f), new Vector3(0.08f, 0.1f, 0.5f), darkWood, Quaternion.Euler(0, a, 0), false);
                Orrery(office, ot + new Vector3(0, 0.86f, 0), brass, animated);
                StarChart(office, new Vector3(3.2f, 3.3f, 5.93f), gold);
            }
            Planter(office, new Vector3(-5.6f, 0, 5.2f), PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f));

            // ---------------------------------------------------------------- east corners (the select canvas is between)
            GrandfatherClock(office, new Vector3(6.5f, 0, -5.3f), darkWood, gold, brass, porcelain, animated);
            CoatStand(office, new Vector3(6.2f, 0, 5.2f), darkWood, brass, velvet);

            // ---------------------------------------------------------------- overhead: chandelier and floating candles
            {
                var ch = Child(office, "Chandelier", new Vector3(0, 0, -0.5f));
                Deco(ch, PrimitiveType.Cube, "ChandelierChain", new Vector3(0, (5.5f + roomH) / 2f, 0), new Vector3(0.05f, roomH - 5.5f, 0.05f), iron, Quaternion.identity, false);
                Shape(ch, "ChandelierHub", BlobMesh(), new Vector3(0, 5.35f, 0), new Vector3(0.3f, 0.36f, 0.3f), brass, false, Quaternion.identity);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f;
                    Vector3 p = Quaternion.Euler(0, a, 0) * new Vector3(0, 5.2f, 0.95f);
                    Deco(ch, PrimitiveType.Cube, "ChandelierArm", Quaternion.Euler(0, a, 0) * new Vector3(0, 5.25f, 0.5f), new Vector3(0.04f, 0.04f, 0.95f), brass, Quaternion.Euler(0, a, 0), false);
                    Candle(ch, p, 0.2f, brass, wax, flame);
                }
            }
            for (int k = 0; k < 7; k++)
            {
                // Over the bookcases and the desk, each bobbing on its own.
                float x = -5.4f + k * 1.8f + R(-0.3f, 0.3f);
                var fc = Child(office, "FloatingCandle" + k, new Vector3(x, 4.1f + R(-0.25f, 0.35f), -4.3f + R(-0.5f, 0.5f)));
                Shape(fc, "Wax", CylMesh(), new Vector3(0, 0.12f, 0), new Vector3(0.07f, 0.12f, 0.07f), wax, false, Quaternion.identity);
                Shape(fc, "Flame", BlobMesh(), new Vector3(0, 0.3f, 0), new Vector3(0.06f, 0.12f, 0.06f), flame, false, Quaternion.identity);
                var clip = NewClip("FloatingCandle" + k, true);
                Wave(clip, "", "posy", 4f + (k % 3), 1, fc.localPosition.y, 0.12f, k * 0.13f);
                Keys(clip, "", "posx", 0, fc.localPosition.x, 1f, fc.localPosition.x);
                Keys(clip, "", "posz", 0, fc.localPosition.z, 1f, fc.localPosition.z);
                OfficeLoop(fc.gameObject, "FloatingCandle" + k, clip);
                animated.Add(fc);
            }

            // ---------------------------------------------------------------- odds and ends on the floor
            {
                var trunk = Child(office, "Trunk", new Vector3(-6.15f, 0, -3.7f));
                trunk.localRotation = Quaternion.Euler(0, 90f, 0);
                Block(trunk, "TrunkBox", new Vector3(0, 0.32f, 0), new Vector3(1.2f, 0.64f, 0.7f), leather);
                Shape(trunk, "TrunkLid", CylMesh(), new Vector3(0, 0.64f, 0), new Vector3(0.7f, 0.6f, 0.36f), leather, false, Quaternion.Euler(0, 0, 90f));
                foreach (float x in new[] { -0.4f, 0.4f })
                    Deco(trunk, PrimitiveType.Cube, "TrunkBand", new Vector3(x, 0.5f, 0), new Vector3(0.06f, 0.72f, 0.74f), brass, Quaternion.identity, false);
                Deco(trunk, PrimitiveType.Cube, "TrunkLock", new Vector3(0, 0.55f, 0.37f), new Vector3(0.12f, 0.14f, 0.03f), gold, Quaternion.identity, false);
            }
            {
                var basket = Child(office, "ScrollBasket", new Vector3(1.95f, 0, -5.1f));
                Shape(basket, "Basket", CylMesh(), new Vector3(0, 0.25f, 0), new Vector3(0.55f, 0.25f, 0.55f), PMat("OfficeWicker", new Color(0.7f, 0.55f, 0.3f), 2f), true, Quaternion.identity);
                for (int i = 0; i < 6; i++)
                    Shape(basket, "Scroll", CylMesh(), new Vector3(R(-0.15f, 0.15f), 0.55f, R(-0.15f, 0.15f)), new Vector3(0.09f, 0.35f, 0.09f), paper, false, Quaternion.Euler(R(-18f, 18f), 0, R(-18f, 18f)));
            }
            for (int i = 0; i < 5; i++)
                Deco(office, PrimitiveType.Cube, "FloorBook", new Vector3(2.35f, 0.05f + i * 0.09f, -1.5f), new Vector3(0.4f, 0.09f, 0.55f), books[(i * 7 + 1) % books.Length], Quaternion.Euler(0, R(-25f, 25f), 0), false);
            OpenBook(office, new Vector3(2.4f, 0.52f, -1.5f), 35f, books[4], paper);

            foreach (var t in animated)
                foreach (var tr in t.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
        }

        // ================================================================== the office's pieces

        /// <summary>A bookcase against the back wall centred on 'at' (its back on the wall, opening towards +z): five shelves
        /// crammed with books of every height, some leaning, a few laid flat in stacks, and here and there a curio.</summary>
        private static void Bookcase(Transform office, Vector3 at, float width, float height, Material frame, Material gold, Material[] books, Material paper,
            Material brass, Material crystal, Material wax, Material flame)
        {
            var bc = Child(office, "Bookcase", at);
            float hw = width / 2f;
            Block(bc, "CaseBack", new Vector3(0, height / 2f, -0.26f), new Vector3(width, height, 0.06f), frame);
            foreach (float s in new[] { -1f, 1f }) Block(bc, "CaseSide", new Vector3(s * hw, height / 2f, 0), new Vector3(0.1f, height, 0.6f), frame);
            Block(bc, "CaseCrown", new Vector3(0, height + 0.08f, 0.02f), new Vector3(width + 0.25f, 0.16f, 0.68f), frame);
            Deco(bc, PrimitiveType.Cube, "CaseCrownTrim", new Vector3(0, height - 0.02f, 0.33f), new Vector3(width + 0.2f, 0.06f, 0.04f), gold, Quaternion.identity, false);
            Block(bc, "CasePlinth", new Vector3(0, 0.08f, 0.02f), new Vector3(width + 0.1f, 0.16f, 0.64f), frame);
            float[] shelves = { 0.16f, 1.22f, 2.28f, 3.34f, 4.4f };
            foreach (float y in shelves)
                Deco(bc, PrimitiveType.Cube, "Shelf", new Vector3(0, y + 0.03f, 0), new Vector3(width - 0.1f, 0.06f, 0.56f), frame, Quaternion.identity, false);
            for (int r = 0; r < shelves.Length; r++)
            {
                float y = shelves[r] + 0.06f;
                float x = -hw + 0.12f;
                while (x < hw - 0.2f)
                {
                    float roll = R(0f, 1f);
                    if (roll < 0.06f && x < hw - 0.6f)
                    {
                        // A curio in a gap: a glowing jar, a crystal, a candle or a little globe.
                        float cx = x + 0.2f;
                        int kind = (int)R(0f, 4f);
                        if (kind == 0)
                        {
                            Color c = Color.HSVToRGB(R(0f, 1f), 0.6f, 0.95f);
                            Shape(bc, "Jar", CylMesh(), new Vector3(cx, y + 0.18f, 0.02f), new Vector3(0.2f, 0.18f, 0.2f), PMat("OfficeJar" + ColorUtility.ToHtmlStringRGB(c), c, 1f, c * 0.4f), false, Quaternion.identity);
                            Shape(bc, "JarLid", CylMesh(), new Vector3(cx, y + 0.38f, 0.02f), new Vector3(0.22f, 0.02f, 0.22f), brass, false, Quaternion.identity);
                        }
                        else if (kind == 1) Crystal(bc, new Vector3(cx, y + 0.2f, 0.02f), 0.22f, new Color(0.55f, 0.8f, 1f));
                        else if (kind == 2) Candle(bc, new Vector3(cx, y, 0.06f), 0.2f, brass, wax, flame);
                        else
                        {
                            Shape(bc, "Globe", BlobMesh(), new Vector3(cx, y + 0.32f, 0.02f), Vector3.one * 0.3f, PMat("OfficeGlobe", new Color(0.3f, 0.5f, 0.62f), 1f), false, Quaternion.identity);
                            Shape(bc, "GlobeStand", CylMesh(), new Vector3(cx, y + 0.08f, 0.02f), new Vector3(0.16f, 0.08f, 0.16f), brass, false, Quaternion.identity);
                        }
                        x += 0.42f;
                        continue;
                    }
                    if (roll < 0.14f && x < hw - 0.6f)
                    {
                        // A few books laid flat in a stack.
                        int n = 2 + (int)R(0f, 3f);
                        for (int i = 0; i < n; i++)
                            Deco(bc, PrimitiveType.Cube, "FlatBook", new Vector3(x + 0.22f, y + 0.04f + i * 0.08f, 0.02f), new Vector3(0.4f, 0.08f, 0.32f), books[(int)R(0f, books.Length - 0.01f)],
                                Quaternion.Euler(0, R(-8f, 8f), 0), false);
                        x += 0.48f;
                        continue;
                    }
                    float w = R(0.07f, 0.15f), h = R(0.6f, 0.92f), dep = R(0.28f, 0.4f);
                    bool lean = roll > 0.93f;
                    var mat = books[(int)R(0f, books.Length - 0.01f)];
                    if (lean)
                    {
                        // Leaning on the book before it.
                        Deco(bc, PrimitiveType.Cube, "Book", new Vector3(x + h * 0.18f, y + h * 0.48f, 0.02f), new Vector3(w, h, dep), mat, Quaternion.Euler(0, 0, -16f), false);
                        x += w + h * 0.3f;
                    }
                    else
                    {
                        Deco(bc, PrimitiveType.Cube, "Book", new Vector3(x + w / 2f, y + h / 2f, 0.02f + (0.4f - dep) * 0.3f), new Vector3(w, h, dep), mat, Quaternion.identity, false);
                        if (R(0f, 1f) < 0.3f)   // a gilt band on the spine
                            Deco(bc, PrimitiveType.Cube, "BookBand", new Vector3(x + w / 2f, y + h * 0.78f, 0.03f + dep / 2f), new Vector3(w + 0.004f, 0.03f, 0.01f), gold, Quaternion.identity, false);
                        x += w + 0.008f;
                    }
                }
            }
        }

        /// <summary>An open book lying at 'at' (its spine along the z axis turned by 'yaw'): two cover halves and the pages.</summary>
        private static void OpenBook(Transform parent, Vector3 at, float yaw, Material cover, Material paper)
        {
            var b = Child(parent, "OpenBook", at);
            b.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(b, PrimitiveType.Cube, "Cover", new Vector3(s * 0.15f, 0.0f, 0), new Vector3(0.3f, 0.015f, 0.4f), cover, Quaternion.Euler(0, 0, -s * 6f), false);
                Deco(b, PrimitiveType.Cube, "Pages", new Vector3(s * 0.14f, 0.025f, 0), new Vector3(0.27f, 0.03f, 0.37f), paper, Quaternion.Euler(0, 0, -s * 7f), false);
            }
        }

        /// <summary>A candle in a brass holder standing at 'at' (the holder's foot), 'h' tall.</summary>
        private static void Candle(Transform parent, Vector3 at, float h, Material brass, Material wax, Material flame)
        {
            Shape(parent, "CandleHolder", CylMesh(), at + new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.02f, 0.12f), brass, false, Quaternion.identity);
            Shape(parent, "CandleWax", CylMesh(), at + new Vector3(0, 0.04f + h / 2f, 0), new Vector3(0.05f, h / 2f, 0.05f), wax, false, Quaternion.identity);
            Shape(parent, "CandleFlame", BlobMesh(), at + new Vector3(0, 0.1f + h, 0), new Vector3(0.045f, 0.1f, 0.045f), flame, false, Quaternion.identity);
        }

        private static void Hourglass(Transform parent, Vector3 at, Material brass)
        {
            var sand = PMat("OfficeSand", new Color(0.92f, 0.78f, 0.45f), 1f);
            var glass = PMat("OfficeGlass", new Color(0.75f, 0.88f, 0.95f), 1f);
            foreach (float y in new[] { 0.01f, 0.31f })
                Shape(parent, "HourglassCap", CylMesh(), at + new Vector3(0, y, 0), new Vector3(0.16f, 0.01f, 0.16f), brass, false, Quaternion.identity);
            Shape(parent, "HourglassTop", ConeMesh(), at + new Vector3(0, 0.235f, 0), new Vector3(0.13f, 0.15f, 0.13f), glass, false, Quaternion.Euler(180f, 0, 0));
            Shape(parent, "HourglassSand", ConeMesh(), at + new Vector3(0, 0.07f, 0), new Vector3(0.12f, 0.1f, 0.12f), sand, false, Quaternion.identity);
            Shape(parent, "HourglassBottom", ConeMesh(), at + new Vector3(0, 0.085f, 0), new Vector3(0.13f, 0.15f, 0.13f), glass, false, Quaternion.identity);
            foreach (float a in new[] { 45f, 225f })
                Deco(parent, PrimitiveType.Cube, "HourglassPost", at + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.16f, 0.07f), new Vector3(0.015f, 0.3f, 0.015f), brass, Quaternion.identity, false);
        }

        /// <summary>A little framed picture standing on a shelf, facing +x when 'facing' is 1 (or -x... into the room from the west wall: pass -1).</summary>
        private static void PictureFrame(Transform parent, Vector3 at, float w, float h, Material gold, Material picture, float facing)
        {
            Quaternion rot = Quaternion.Euler(0, facing < 0f ? 90f : -90f, -8f);
            Deco(parent, PrimitiveType.Cube, "Frame", at, new Vector3(w, h, 0.03f), gold, rot, false);
            Deco(parent, PrimitiveType.Cube, "FramePicture", at + rot * new Vector3(0, 0, 0.018f), new Vector3(w * 0.78f, h * 0.78f, 0.01f), picture, rot, false);
        }

        /// <summary>The first Headmaster's portrait on the west wall (facing +x): a gilt frame and a painted figure in a starry robe.</summary>
        private static void Portrait(Transform parent, Vector3 at, Material gold)
        {
            var p = Child(parent, "Portrait", at);
            p.localRotation = Quaternion.Euler(0, 90f, 0);   // its +z faces into the room (+x)
            Deco(p, PrimitiveType.Cube, "PortraitFrame", Vector3.zero, new Vector3(1.5f, 1.9f, 0.08f), gold, Quaternion.identity, false);
            Deco(p, PrimitiveType.Cube, "PortraitCanvas", new Vector3(0, 0, 0.045f), new Vector3(1.26f, 1.66f, 0.01f), PMat("OfficePortraitBg", new Color(0.12f, 0.22f, 0.2f), 1f), Quaternion.identity, false);
            var robe = PMat("OfficePortraitRobe", new Color(0.28f, 0.2f, 0.5f), 1f);
            var skin = PMat("OfficePortraitSkin", new Color(0.86f, 0.7f, 0.56f), 1f);
            var beard = PMat("OfficePortraitBeard", new Color(0.86f, 0.86f, 0.9f), 1f);
            Shape(p, "PortraitShoulders", BlobMesh(), new Vector3(0, -0.55f, 0.05f), new Vector3(1.0f, 0.75f, 0.02f), robe, false, Quaternion.identity);
            Shape(p, "PortraitHead", BlobMesh(), new Vector3(0, 0.12f, 0.055f), new Vector3(0.36f, 0.46f, 0.02f), skin, false, Quaternion.identity);
            Shape(p, "PortraitBeard", ConeMesh(), new Vector3(0, -0.2f, 0.06f), new Vector3(0.34f, 0.5f, 0.02f), beard, false, Quaternion.Euler(0, 0, 180f));
            Shape(p, "PortraitHat", ConeMesh(), new Vector3(0, 0.55f, 0.055f), new Vector3(0.5f, 0.55f, 0.02f), robe, false, Quaternion.identity);
            for (int k = 0; k < 6; k++)
                Shape(p, "PortraitStar", GemMesh(), new Vector3(R(-0.4f, 0.4f), R(-0.75f, -0.35f), 0.065f), Vector3.one * 0.05f, gold, false, Quaternion.identity);
            foreach (float x in new[] { -0.07f, 0.07f })
                Shape(p, "PortraitEye", BlobMesh(), new Vector3(x, 0.16f, 0.068f), new Vector3(0.04f, 0.03f, 0.01f), PMat("OfficeSoot", new Color(0.08f, 0.07f, 0.07f), 1f), false, Quaternion.identity);
        }

        /// <summary>The Headmaster's owl on a T-perch: round body, big amber eyes, ear tufts; it turns its head now and then.</summary>
        private static void Owl(Transform parent, Vector3 at, Material wood, Material brass, List<Transform> animated)
        {
            var perch = Child(parent, "OwlPerch", at);
            perch.localRotation = Quaternion.Euler(0, 200f, 0);   // facing out into the room, towards the door
            Shape(perch, "PerchFoot", CylMesh(), new Vector3(0, 0.04f, 0), new Vector3(0.5f, 0.04f, 0.5f), wood, true, Quaternion.identity);
            Shape(perch, "PerchPole", CylMesh(), new Vector3(0, 0.8f, 0), new Vector3(0.07f, 0.8f, 0.07f), wood, false, Quaternion.identity);
            Shape(perch, "PerchBar", CylMesh(), new Vector3(0, 1.6f, 0), new Vector3(0.06f, 0.35f, 0.06f), brass, false, Quaternion.Euler(0, 0, 90f));
            var feathers = PMat("OwlFeathers", new Color(0.55f, 0.42f, 0.3f), 1f);
            var chest = PMat("OwlChest", new Color(0.85f, 0.76f, 0.6f), 1f);
            var eyes = PMat("OwlEyes", new Color(1f, 0.7f, 0.15f), 1f, new Color(0.3f, 0.2f, 0.02f));
            var dark = PMat("OfficeSoot", new Color(0.08f, 0.07f, 0.07f), 1f);
            var owl = Child(perch, "Owl", new Vector3(0, 1.64f, 0));
            Shape(owl, "OwlBody", BlobMesh(), new Vector3(0, 0.2f, 0), new Vector3(0.3f, 0.4f, 0.28f), feathers, false, Quaternion.identity);
            Shape(owl, "OwlChest", BlobMesh(), new Vector3(0, 0.18f, -0.08f), new Vector3(0.22f, 0.3f, 0.16f), chest, false, Quaternion.identity);
            foreach (float x in new[] { -1f, 1f })
            {
                Shape(owl, "OwlWing", BlobMesh(), new Vector3(x * 0.13f, 0.18f, 0.02f), new Vector3(0.1f, 0.32f, 0.24f), feathers, false, Quaternion.Euler(0, 0, x * 8f));
                Shape(owl, "OwlFoot", BlobMesh(), new Vector3(x * 0.06f, 0.0f, -0.04f), new Vector3(0.06f, 0.04f, 0.08f), PMat("OwlFeet", new Color(0.85f, 0.65f, 0.3f), 1f), false, Quaternion.identity);
            }
            Shape(owl, "OwlTail", BlobMesh(), new Vector3(0, 0.02f, 0.14f), new Vector3(0.14f, 0.18f, 0.06f), feathers, false, Quaternion.Euler(-30f, 0, 0));
            var head = Child(owl, "Head", new Vector3(0, 0.44f, 0));
            Shape(head, "OwlHead", BlobMesh(), new Vector3(0, 0.08f, 0), new Vector3(0.28f, 0.24f, 0.26f), feathers, false, Quaternion.identity);
            Shape(head, "OwlFace", BlobMesh(), new Vector3(0, 0.07f, -0.1f), new Vector3(0.24f, 0.18f, 0.08f), chest, false, Quaternion.identity);
            foreach (float x in new[] { -1f, 1f })
            {
                Shape(head, "OwlEye", BlobMesh(), new Vector3(x * 0.055f, 0.09f, -0.14f), new Vector3(0.08f, 0.08f, 0.03f), eyes, false, Quaternion.identity);
                Shape(head, "OwlPupil", BlobMesh(), new Vector3(x * 0.055f, 0.09f, -0.155f), new Vector3(0.035f, 0.035f, 0.01f), dark, false, Quaternion.identity);
                Shape(head, "OwlTuft", ConeMesh(), new Vector3(x * 0.09f, 0.21f, -0.02f), new Vector3(0.05f, 0.12f, 0.04f), feathers, false, Quaternion.Euler(0, 0, -x * 20f));
            }
            Shape(head, "OwlBeak", ConeMesh(), new Vector3(0, 0.04f, -0.15f), new Vector3(0.035f, 0.06f, 0.035f), PMat("OwlFeet", new Color(0.85f, 0.65f, 0.3f), 1f), false, Quaternion.Euler(160f, 0, 0));
            var clip = NewClip("OwlLook", true);
            Keys(clip, "Owl/Head", "roty", 0, 0, 2.5f, 0, 2.8f, 70f, 5f, 70f, 5.3f, -40f, 7f, -40f, 7.4f, 0, 10f, 0);
            Keys(clip, "Owl/Head", "rotz", 0, 0, 7.4f, 0, 7.8f, 14f, 8.6f, 14f, 9f, 0, 10f, 0);
            Wave(clip, "Owl", "scaley", 10f, 4, 1f, 0.025f);
            OfficeLoop(perch.gameObject, "OwlLook", clip);
            animated.Add(perch);
        }

        /// <summary>The office cat, curled up asleep on the hearth rug, its side rising and falling and its tail tip twitching.</summary>
        private static void Cat(Transform parent, Vector3 at, List<Transform> animated)
        {
            var root = Child(parent, "OfficeCat", at);
            root.localRotation = Quaternion.Euler(0, 30f, 0);
            var fur = PMat("CatFur", new Color(0.88f, 0.55f, 0.22f), 1f);
            var pale = PMat("CatPale", new Color(0.97f, 0.9f, 0.8f), 1f);
            var dark = PMat("OfficeSoot", new Color(0.08f, 0.07f, 0.07f), 1f);
            var body = Child(root, "Body", Vector3.zero);
            Shape(body, "CatBody", BlobMesh(), new Vector3(0, 0.12f, 0), new Vector3(0.5f, 0.24f, 0.36f), fur, false, Quaternion.Euler(0, 15f, 0));
            Shape(body, "CatChest", BlobMesh(), new Vector3(-0.16f, 0.1f, -0.1f), new Vector3(0.22f, 0.18f, 0.2f), pale, false, Quaternion.identity);
            Shape(root, "CatHead", BlobMesh(), new Vector3(-0.26f, 0.12f, -0.1f), new Vector3(0.2f, 0.17f, 0.19f), fur, false, Quaternion.Euler(0, 0, -15f));
            Shape(root, "CatMuzzle", BlobMesh(), new Vector3(-0.34f, 0.09f, -0.11f), new Vector3(0.08f, 0.07f, 0.09f), pale, false, Quaternion.identity);
            foreach (float z in new[] { -0.17f, -0.03f })
            {
                Shape(root, "CatEar", ConeMesh(), new Vector3(-0.24f, 0.22f, z), new Vector3(0.07f, 0.09f, 0.04f), fur, false, Quaternion.Euler(0, 0, -25f));
                Deco(root, PrimitiveType.Cube, "CatClosedEye", new Vector3(-0.35f, 0.15f, z * 0.55f - 0.05f), new Vector3(0.012f, 0.008f, 0.04f), dark, Quaternion.identity, false);
            }
            // The tail curled round the front, its tip on a pivot so it can twitch.
            for (int k = 0; k < 4; k++)
            {
                float a = 40f + k * 32f;
                Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.3f, 0.05f, Mathf.Sin(-a * Mathf.Deg2Rad) * 0.25f - 0.02f);
                Shape(root, "CatTail", BlobMesh(), p, new Vector3(0.09f, 0.08f, 0.16f), k % 2 == 0 ? fur : PMat("CatStripe", new Color(0.7f, 0.4f, 0.14f), 1f), false, Quaternion.Euler(0, a + 90f, 0));
            }
            var tip = Child(root, "TailTip", new Vector3(-0.22f, 0.05f, -0.3f));
            Shape(tip, "CatTailTip", BlobMesh(), new Vector3(-0.06f, 0, 0), new Vector3(0.13f, 0.07f, 0.07f), pale, false, Quaternion.identity);
            var clip = NewClip("CatSleep", true);
            Wave(clip, "Body", "scaley", 4f, 2, 1f, 0.06f);
            Wave(clip, "Body", "scalez", 4f, 2, 1f, 0.03f);
            Keys(clip, "TailTip", "roty", 0, 0, 2.6f, 0, 2.8f, 25f, 3.0f, -10f, 3.2f, 20f, 3.5f, 0, 8f, 0);
            OfficeLoop(root.gameObject, "CatSleep", clip);
            animated.Add(root);
        }

        /// <summary>An open-fronted cabinet of potions against the north wall (facing -z, into the room).</summary>
        private static void PotionCabinet(Transform parent, Vector3 at, Material frame, Material gold)
        {
            var cab = Child(parent, "PotionCabinet", at);
            cab.localRotation = Quaternion.Euler(0, 180f, 0);   // its +z faces into the room
            Block(cab, "CabinetBack", new Vector3(0, 1.2f, -0.24f), new Vector3(1.7f, 2.4f, 0.05f), frame);
            foreach (float s in new[] { -1f, 1f }) Block(cab, "CabinetSide", new Vector3(s * 0.85f, 1.2f, 0), new Vector3(0.08f, 2.4f, 0.5f), frame);
            Block(cab, "CabinetTop", new Vector3(0, 2.44f, 0.02f), new Vector3(1.86f, 0.1f, 0.58f), frame);
            Block(cab, "CabinetBase", new Vector3(0, 0.3f, 0), new Vector3(1.7f, 0.6f, 0.5f), frame);
            foreach (float s in new[] { -0.42f, 0.42f }) Shape(cab, "CabinetKnob", BlobMesh(), new Vector3(s * 0.2f, 0.4f, 0.26f), Vector3.one * 0.05f, gold, false, Quaternion.identity);
            float[] shelves = { 0.62f, 1.22f, 1.82f };
            foreach (float y in shelves)
            {
                Deco(cab, PrimitiveType.Cube, "CabinetShelf", new Vector3(0, y, 0), new Vector3(1.62f, 0.04f, 0.44f), frame, Quaternion.identity, false);
                float x = -0.68f;
                while (x < 0.66f)
                {
                    Color c = Color.HSVToRGB(R(0f, 1f), R(0.5f, 0.9f), 0.95f);
                    var m = PMat("OfficePotion" + ColorUtility.ToHtmlStringRGB(c), c, 1f, c * 0.45f);
                    float h = R(0.18f, 0.36f), w = R(0.1f, 0.16f);
                    bool round = R(0f, 1f) < 0.5f;
                    if (round) Shape(cab, "Potion", BlobMesh(), new Vector3(x + w / 2f, y + 0.02f + w * 0.5f, R(-0.08f, 0.08f)), Vector3.one * w * 1.1f, m, false, Quaternion.identity);
                    else Shape(cab, "Potion", CylMesh(), new Vector3(x + w / 2f, y + 0.02f + h / 2f, R(-0.08f, 0.08f)), new Vector3(w, h / 2f, w), m, false, Quaternion.identity);
                    float neckY = round ? y + 0.02f + w * 1.1f : y + 0.02f + h;
                    Shape(cab, "PotionNeck", CylMesh(), new Vector3(x + w / 2f, neckY + 0.04f, 0), new Vector3(0.04f, 0.04f, 0.04f), m, false, Quaternion.identity);
                    Shape(cab, "PotionCork", CylMesh(), new Vector3(x + w / 2f, neckY + 0.09f, 0), new Vector3(0.045f, 0.02f, 0.045f), PMat("OfficeCork", new Color(0.62f, 0.45f, 0.28f), 1f), false, Quaternion.identity);
                    x += w + R(0.03f, 0.08f);
                }
            }
            // Gilt edging along the front of the open shelves.
            foreach (float y in new[] { 0.64f, 2.38f })
                Deco(cab, PrimitiveType.Cube, "CabinetTrim", new Vector3(0, y, 0.25f), new Vector3(1.66f, 0.05f, 0.02f), gold, Quaternion.identity, false);
        }

        /// <summary>A brass orrery: a glowing sun on a stand and three worlds on arms turning round it at their own speeds.</summary>
        private static void Orrery(Transform parent, Vector3 at, Material brass, List<Transform> animated)
        {
            var o = Child(parent, "Orrery", at);
            Shape(o, "OrreryBase", CylMesh(), new Vector3(0, 0.03f, 0), new Vector3(0.36f, 0.03f, 0.36f), brass, false, Quaternion.identity);
            Shape(o, "OrreryPost", CylMesh(), new Vector3(0, 0.3f, 0), new Vector3(0.04f, 0.27f, 0.04f), brass, false, Quaternion.identity);
            Shape(o, "OrrerySun", BlobMesh(), new Vector3(0, 0.6f, 0), Vector3.one * 0.18f, PMat("OrrerySun", new Color(1f, 0.8f, 0.3f), 1f, new Color(1f, 0.65f, 0.15f)), false, Quaternion.identity);
            Shape(o, "OrreryRing", CylMesh(), new Vector3(0, 0.45f, 0), new Vector3(0.86f, 0.006f, 0.86f), brass, false, Quaternion.identity);
            Color[] cols = { new Color(0.4f, 0.6f, 0.9f), new Color(0.85f, 0.45f, 0.3f), new Color(0.55f, 0.8f, 0.45f) };
            float[] radii = { 0.2f, 0.3f, 0.4f };
            float[] secs = { 6f, 10f, 15f };
            var clip = NewClip("Orrery", true);
            for (int k = 0; k < 3; k++)
            {
                var arm = Child(o, "Arm" + k, new Vector3(0, 0.5f + k * 0.03f, 0));
                arm.localRotation = Quaternion.Euler(0, k * 110f, 0);
                Deco(arm, PrimitiveType.Cube, "OrreryArm", new Vector3(0, 0, radii[k] / 2f), new Vector3(0.015f, 0.015f, radii[k]), brass, Quaternion.identity, false);
                Deco(arm, PrimitiveType.Cube, "OrreryDrop", new Vector3(0, 0.04f, radii[k]), new Vector3(0.012f, 0.08f, 0.012f), brass, Quaternion.identity, false);
                Shape(arm, "OrreryWorld", BlobMesh(), new Vector3(0, 0.1f, radii[k]), Vector3.one * (0.06f + k * 0.015f), PMat("OrreryWorld" + k, cols[k], 1f, cols[k] * 0.2f), false, Quaternion.identity);
                // Whole turns over the clip (the clip's length is the slowest world's period times the others' ratios kept whole).
                float len = 30f;
                int turns = Mathf.RoundToInt(len / secs[k]);
                Keys(clip, "Arm" + k, "roty", 0, k * 110f, len, k * 110f + 360f * turns);
            }
            // Linear: the worlds keep a steady pace.
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            OfficeLoop(o.gameObject, "Orrery", clip);
            animated.Add(o);
        }

        /// <summary>A star chart pinned to the north wall (facing -z): a deep blue sheet dotted with gold stars and constellation lines.</summary>
        private static void StarChart(Transform parent, Vector3 at, Material gold)
        {
            var c = Child(parent, "StarChart", at);
            c.localRotation = Quaternion.Euler(0, 180f, 0);
            Deco(c, PrimitiveType.Cube, "ChartSheet", Vector3.zero, new Vector3(2.2f, 1.5f, 0.02f), PMat("OfficeChart", new Color(0.1f, 0.13f, 0.3f), 1f), Quaternion.identity, false);
            Deco(c, PrimitiveType.Cube, "ChartRodTop", new Vector3(0, 0.78f, 0.01f), new Vector3(2.35f, 0.05f, 0.05f), gold, Quaternion.identity, false);
            Deco(c, PrimitiveType.Cube, "ChartRodBottom", new Vector3(0, -0.78f, 0.01f), new Vector3(2.35f, 0.05f, 0.05f), gold, Quaternion.identity, false);
            var line = PMat("OfficeChartLine", new Color(0.6f, 0.65f, 0.85f), 1f);
            var pts = new List<Vector2>();
            for (int k = 0; k < 16; k++) pts.Add(new Vector2(R(-0.95f, 0.95f), R(-0.6f, 0.6f)));
            foreach (var p in pts)
                Shape(c, "ChartStar", GemMesh(), new Vector3(p.x, p.y, 0.015f), Vector3.one * R(0.035f, 0.07f), gold, false, Quaternion.identity);
            for (int k = 0; k + 1 < 7; k++)
            {
                Vector2 a = pts[k], b = pts[k + 1];
                Vector2 m = (a + b) / 2f, d = b - a;
                Deco(c, PrimitiveType.Cube, "ChartLine", new Vector3(m.x, m.y, 0.012f), new Vector3(d.magnitude, 0.008f, 0.004f), line, Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), false);
            }
        }

        /// <summary>A grandfather clock in the south-east corner (facing -x, into the room), its pendulum swinging.</summary>
        private static void GrandfatherClock(Transform parent, Vector3 at, Material wood, Material gold, Material brass, Material face, List<Transform> animated)
        {
            var c = Child(parent, "GrandfatherClock", at);
            c.localRotation = Quaternion.Euler(0, -90f, 0);   // its +z faces into the room (-x)
            Block(c, "ClockBase", new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.5f), wood);
            Block(c, "ClockTrunk", new Vector3(0, 1.35f, -0.02f), new Vector3(0.62f, 1.5f, 0.4f), wood);
            Block(c, "ClockHood", new Vector3(0, 2.45f, 0), new Vector3(0.78f, 0.75f, 0.5f), wood);
            Shape(c, "ClockTop", ConeMesh(), new Vector3(0, 3.0f, 0), new Vector3(0.6f, 0.35f, 0.4f), wood, false, Quaternion.identity);
            Shape(c, "ClockFinial", GemMesh(), new Vector3(0, 3.28f, 0), new Vector3(0.12f, 0.2f, 0.12f), gold, false, Quaternion.identity);
            Shape(c, "ClockFace", CylMesh(), new Vector3(0, 2.45f, 0.26f), new Vector3(0.52f, 0.01f, 0.52f), face, false, Quaternion.Euler(90f, 0, 0));
            Shape(c, "ClockRim", CylMesh(), new Vector3(0, 2.45f, 0.255f), new Vector3(0.6f, 0.01f, 0.6f), gold, false, Quaternion.Euler(90f, 0, 0));
            var ink = PMat("OfficeSoot", new Color(0.08f, 0.07f, 0.07f), 1f);
            for (int k = 0; k < 12; k++)
                Deco(c, PrimitiveType.Cube, "ClockTick", new Vector3(0, 2.45f, 0.27f) + Quaternion.Euler(0, 0, k * 30f) * new Vector3(0, 0.21f, 0), new Vector3(0.015f, 0.04f, 0.005f), ink, Quaternion.Euler(0, 0, k * 30f), false);
            Deco(c, PrimitiveType.Cube, "ClockHourHand", new Vector3(0.05f, 2.49f, 0.275f), new Vector3(0.02f, 0.13f, 0.005f), ink, Quaternion.Euler(0, 0, -50f), false);
            Deco(c, PrimitiveType.Cube, "ClockMinuteHand", new Vector3(-0.03f, 2.53f, 0.278f), new Vector3(0.014f, 0.19f, 0.005f), ink, Quaternion.Euler(0, 0, 18f), false);
            // The pendulum, seen through the trunk's window.
            Deco(c, PrimitiveType.Cube, "ClockWindow", new Vector3(0, 1.4f, 0.185f), new Vector3(0.4f, 1.1f, 0.01f), PMat("OfficeSoot", new Color(0.08f, 0.07f, 0.07f), 1f), Quaternion.identity, false);
            var pend = Child(c, "Pendulum", new Vector3(0, 1.9f, 0.2f));
            Deco(pend, PrimitiveType.Cube, "PendulumRod", new Vector3(0, -0.4f, 0), new Vector3(0.025f, 0.8f, 0.01f), brass, Quaternion.identity, false);
            Shape(pend, "PendulumBob", CylMesh(), new Vector3(0, -0.85f, 0), new Vector3(0.18f, 0.01f, 0.18f), brass, false, Quaternion.Euler(90f, 0, 0));
            var clip = NewClip("ClockPendulum", true);
            Wave(clip, "Pendulum", "rotz", 2f, 1, 0f, 11f);
            OfficeLoop(c.gameObject, "ClockPendulum", clip);
            animated.Add(pend);
        }

        /// <summary>A coat stand with the Headmaster's travelling cloak and a spare pointed hat.</summary>
        private static void CoatStand(Transform parent, Vector3 at, Material wood, Material brass, Material cloak)
        {
            var s = Child(parent, "CoatStand", at);
            Shape(s, "StandPole", CylMesh(), new Vector3(0, 1.0f, 0), new Vector3(0.08f, 1.0f, 0.08f), wood, true, Quaternion.identity);
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
            {
                Deco(s, PrimitiveType.Cube, "StandFoot", Quaternion.Euler(0, a, 0) * new Vector3(0, 0.05f, 0.22f), new Vector3(0.06f, 0.06f, 0.44f), wood, Quaternion.Euler(0, a, 0), false);
                Deco(s, PrimitiveType.Cube, "StandHook", Quaternion.Euler(0, a + 45f, 0) * new Vector3(0, 1.85f, 0.1f), new Vector3(0.03f, 0.03f, 0.2f), brass, Quaternion.Euler(0, a + 45f, 0) * Quaternion.Euler(-30f, 0, 0), false);
            }
            Shape(s, "StandTop", BlobMesh(), new Vector3(0, 2.03f, 0), Vector3.one * 0.1f, brass, false, Quaternion.identity);
            // The cloak hanging from a hook, falling in folds.
            Shape(s, "Cloak", ConeMesh(), new Vector3(-0.12f, 1.25f, -0.12f), new Vector3(0.55f, 1.2f, 0.32f), cloak, false, Quaternion.Euler(0, 45f, 0));
            Shape(s, "CloakCollar", BlobMesh(), new Vector3(-0.1f, 1.82f, -0.1f), new Vector3(0.24f, 0.12f, 0.2f), cloak, false, Quaternion.Euler(0, 45f, 0));
            // The hat on top, its brim tipped.
            var hatMat = PMat("OfficeHat", new Color(0.24f, 0.2f, 0.45f), 1f);
            Shape(s, "HatBrim", CylMesh(), new Vector3(0, 2.1f, 0), new Vector3(0.62f, 0.015f, 0.62f), hatMat, false, Quaternion.Euler(8f, 0, 4f));
            Shape(s, "HatCrown", ConeMesh(), new Vector3(0.03f, 2.42f, 0.02f), new Vector3(0.36f, 0.62f, 0.36f), hatMat, false, Quaternion.Euler(10f, 0, -12f));
            Shape(s, "HatBand", CylMesh(), new Vector3(0.005f, 2.16f, 0.01f), new Vector3(0.37f, 0.03f, 0.37f), PMat("OfficeHatBand", new Color(0.85f, 0.68f, 0.3f), 1f), false, Quaternion.Euler(8f, 0, 4f));
        }

        /// <summary>Plays a looping clip on an office piece (a one-state controller, made fresh each rebuild with the NPCs').</summary>
        private static void OfficeLoop(GameObject go, string name, AnimationClip clip)
        {
            Directory.CreateDirectory(NpcDir);
            string path = NpcDir + "/Office_" + name + ".controller";
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            ctrl.layers[0].stateMachine.AddState("Play").motion = clip;
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.cullingMode = AnimatorCullingMode.CullCompletely;
        }
    }
}
