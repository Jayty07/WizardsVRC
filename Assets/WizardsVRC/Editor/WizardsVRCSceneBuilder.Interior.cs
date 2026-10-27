using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    // Keep interior (a separate walkable set above the island): Headmaster's office (spawn), the Hall of Schools and one
    // themed classroom + professor per school. Doors on the keep and in the hall teleport between it and the castle.
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 InteriorOrigin = new Vector3(0f, 200f, 0f);

        private class Interior
        {
            public Transform root, office, officeSpawn, hallEntry, headmaster;
            public Transform[] classrooms = new Transform[7];
            public Transform[] professors = new Transform[7];
            public Transform exitDoor;
            public Canvas selectCanvas;
        }

        private static readonly string[] ProfessorNames =
        {
            "Professor Ignatia Embercrest", "Professor Halvard Rimeholt", "Professor Zephyra Voltwind", "Professor Briar Meadowsong",
            "Professor Mortimer Gloamsby", "Professor Orsolya Fablewright", "Professor Sahra Dunewise",
        };

        private static readonly string[] ProfessorIntro =
        {
            "Fire is the magic of passion. Our spells strike hard and fast, but a careless flame burns its own caster.",
            "Ice is the magic of endurance. We outlast every storm, and our wards still stand when others fall.",
            "Storm magic is raw power! Our spells hit harder than any other school's, if they hit at all.",
            "Life magic heals what is broken and helps green things grow. Every ally is stronger with us beside them.",
            "Death magic is nothing to fear. We take strength from our foes and turn it into our own.",
            "Myth magic gives shape to stories. Our spells call forth creatures from legends long told.",
            "Balance magic draws on every school at once. We are steady, patient, and ready for anything.",
        };

        private static string[] ProfessorLines(int s)
        {
            return new[]
            {
                "Welcome to the School of " + SchoolLabels[s] + "! I am " + ProfessorNames[s] + ".",
                ProfessorIntro[s],
                "Whatever school you follow, my classroom door is always open. Now run along: the Headmaster will want to know you found us.",
            };
        }

        private static readonly string[] ProphecyLines =
        {
            "Ah, you have arrived at last! Welcome to Starfall Castle, young wizard.",
            "I am Headmaster Greymantle. Forgive the sudden summons; it was I who called you across the stars to this place.",
            "Long ago, a prophecy was carved into the oldest stone of this castle: when the Starfall Crystal dims, a wizard from beyond the sky shall come to rekindle it.",
            "The crystal in our courtyard has begun to fade. Far to the north, Morvane the Hollow King stirs once more, draining the light from our world.",
            "The prophecy speaks of you. But no wizard can face such darkness untrained.",
            "So tell me, which school of magic calls to you? Choose, and your professor will be waiting in the Hall of Schools.",
        };

        private static readonly string[] ReportLines =
        {
            "You found your professor. Good! The castle already feels a little brighter with you in it.",
            "Study hard. Morvane's shadow grows, and the Starfall Crystal needs a champion.",
            "Take this for your journey. Explore the castle, and when you are ready, the Training Yard will test your spells.",
        };
        private static readonly string[] HintLines = { "Your professor awaits in the Hall of Schools, just beyond my door. Go on!" };
        private static readonly string[] IdleLines = { "The prophecy chose well. Keep training, young wizard; Morvane will not wait forever." };

        // ------------------------------------------------------------ geometry

        private static Interior BuildInterior()
        {
            if (_rng == null) _rng = new System.Random(7);
            var info = new Interior();
            var root = new GameObject("KeepInterior").transform;
            root.position = InteriorOrigin;
            info.root = root;

            var floorMat = PMat("HallFloor", new Color(0.62f, 0.55f, 0.5f), 12f);
            var wallMat = PMat("HallWall", new Color(0.72f, 0.66f, 0.74f), 6f);
            var trim = PMat("HallTrim", new Color(0.45f, 0.38f, 0.55f), 2f);
            var carpet = PMat("Carpet", new Color(0.55f, 0.12f, 0.2f), 4f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var wood = PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f);
            var window = PMat("Window", new Color(1f, 0.85f, 0.4f), 1f, new Color(0.9f, 0.7f, 0.25f));
            var doorMat = PMat("Door", new Color(0.35f, 0.2f, 0.12f), 2f);
            const float hallH = 9f, roomH = 7f, doorW = 4f, doorH = 5f;

            // ---- Hall of Schools: x[-6,6] z[0,50]
            Block(root, "HallFloor", new Vector3(0, -0.25f, 25), new Vector3(12, 0.5f, 50), floorMat);
            Block(root, "HallCeiling", new Vector3(0, hallH + 0.25f, 25), new Vector3(12, 0.5f, 50), trim);
            Block(root, "HallCarpet", new Vector3(0, 0.02f, 25), new Vector3(3.5f, 0.04f, 49), carpet);
            float[] sideGaps = { 12, 26, 40 };
            WallAlongZ(root, "HallWallW", -6, 0, 50, hallH, sideGaps, doorW, doorH, wallMat);
            WallAlongZ(root, "HallWallE", 6, 0, 50, hallH, sideGaps, doorW, doorH, wallMat);
            WallAlongX(root, "HallWallS", 0, -7, 7, hallH, new float[] { 0 }, doorW, doorH, wallMat);
            WallAlongX(root, "HallWallN", 50, -7, 7, hallH, new float[] { 0 }, doorW, doorH, wallMat);
            for (int i = 0; i < 6; i++)
            {
                float z = 5 + i * 8;
                foreach (float x in new[] { -5.3f, 5.3f })
                {
                    Shape(root, "HallPillar", CylMesh(), new Vector3(x, hallH / 2f, z), new Vector3(1f, hallH / 2f, 1f), trim, true, Quaternion.identity);
                    Lamp(root, new Vector3(x * 0.8f, 0, z + 4), gold, window);
                }
            }
            for (int i = 0; i < 3; i++) PointLight(root, new Vector3(0, hallH - 1.5f, 8 + i * 17), new Color(1f, 0.88f, 0.7f), 20f, 1.4f);
            Shape(root, "HallStar", GemMesh(), new Vector3(0, hallH - 1.2f, 25), new Vector3(1.4f, 2f, 1.4f), gold, false, Quaternion.identity);

            // ---- Headmaster's office: x[-7,7] z[-12,0] (spawn room)
            var office = Child(root, "HeadmasterOffice", new Vector3(0, 0, -6));
            info.office = office;
            BuildRoomShell(root, "Office", new Vector3(0, 0, -6), new Vector2(14, 12), roomH, floorMat, wallMat, trim);
            WallAlongX(root, "OfficeWallS", -12, -7, 7, roomH, new float[0], 0, 0, wallMat);
            WallAlongZ(root, "OfficeWallW", -7, -12, 0, roomH, new float[0], 0, 0, wallMat);
            WallAlongZ(root, "OfficeWallE", 7, -12, 0, roomH, new float[0], 0, 0, wallMat);
            Block(office, "Rug", new Vector3(0, 0.02f, -0.5f), new Vector3(7, 0.04f, 6), PMat("OfficeRug", new Color(0.25f, 0.2f, 0.6f), 2f));
            var shelf = PMat("Bookshelf", new Color(0.35f, 0.2f, 0.1f), 2f);
            var books = new[] { PMat("BookRed", new Color(0.8f, 0.2f, 0.2f), 1f), PMat("BookBlue", new Color(0.2f, 0.35f, 0.85f), 1f), PMat("BookGreen", new Color(0.2f, 0.65f, 0.3f), 1f) };
            for (int i = 0; i < 4; i++)
            {
                float x = -5.25f + i * 3.5f;
                if (i == 1 || i == 2) x = i == 1 ? -2.6f : 2.6f;
                Block(office, "Shelf", new Vector3(x, 1.75f, -5.65f), new Vector3(2.2f, 3.5f, 0.6f), shelf);
                for (int r = 0; r < 3; r++)
                    for (int b = 0; b < 5; b++)
                        Block(office, "Book", new Vector3(x - 0.8f + b * 0.4f, 0.75f + r * 1.05f, -5.4f), new Vector3(0.28f, 0.7f, 0.3f), books[(b + r + i) % 3]);
            }
            Block(office, "Desk", new Vector3(0, 0.5f, -2.2f), new Vector3(3.2f, 1f, 1.2f), wood);
            Shape(office, "DeskOrb", GemMesh(), new Vector3(-1f, 1.35f, -2.2f), new Vector3(0.35f, 0.5f, 0.35f), PMat("Crystal_9FD8FF", new Color(0.62f, 0.85f, 1f), 1f, new Color(0.37f, 0.5f, 0.6f)), false, Quaternion.identity);
            Block(office, "Window", new Vector3(0, 4f, -5.78f), new Vector3(3f, 2.6f, 0.1f), window);
            Block(office, "Fireplace", new Vector3(-6.6f, 1.2f, 2f), new Vector3(0.8f, 2.4f, 2.6f), trim);
            Shape(office, "Fire", ConeMesh(), new Vector3(-6.1f, 0.6f, 2f), new Vector3(0.9f, 1f, 0.9f), PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f)), false, Quaternion.identity);
            for (int i = 0; i < 2; i++) Planter(office, new Vector3(-5.5f + i * 11f, 0, 5.2f), PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f));
            PointLight(office, new Vector3(0, 5.5f, -1f), new Color(1f, 0.85f, 0.65f), 16f, 1.5f);
            PointLight(office, new Vector3(-5.5f, 1.5f, 2f), new Color(1f, 0.55f, 0.2f), 6f, 1.2f);
            info.headmaster = Wizard(office, "Headmaster", new Vector3(0, 0, -3.6f), 180f, new Color(0.2f, 0.25f, 0.6f), new Color(0.25f, 0.2f, 0.55f), true, "Headmaster Greymantle");
            info.officeSpawn = Child(office, "OfficeSpawn", new Vector3(0, 0.05f, 2.5f));
            info.officeSpawn.rotation = Quaternion.Euler(0, 180f, 0);
            var canvasGo = new GameObject("SelectCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(office, false);
            info.selectCanvas = MakeWorldCanvas(canvasGo, new Vector2(1600, 900), new Vector3(6.75f, 3.2f, 0), 0.0055f);
            canvasGo.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            HallSign(root, new Vector3(0, doorH + 1.1f, 0.3f), 180f, "Headmaster's Office", gold);

            // ---- classrooms: three each side of the hall, Balance at the north end
            int[] west = { 0, 2, 4 }, east = { 1, 3, 5 };
            for (int i = 0; i < 3; i++)
            {
                AddClassroom(info, west[i], new Vector3(-13, 0, sideGaps[i]), -90f, new Vector2(12, 14), roomH, floorMat, wallMat, trim, wood, gold);
                WallAlongZ(root, "ClassWallOuter", -20, sideGaps[i] - 6, sideGaps[i] + 6, roomH, new float[0], 0, 0, wallMat);
                WallAlongX(root, "ClassWallS", sideGaps[i] - 6, -20, -6, roomH, new float[0], 0, 0, wallMat);
                WallAlongX(root, "ClassWallN", sideGaps[i] + 6, -20, -6, roomH, new float[0], 0, 0, wallMat);
                HallSign(root, new Vector3(-5.7f, doorH + 1.1f, sideGaps[i]), -90f, "School of " + SchoolLabels[west[i]], gold);

                AddClassroom(info, east[i], new Vector3(13, 0, sideGaps[i]), 90f, new Vector2(12, 14), roomH, floorMat, wallMat, trim, wood, gold);
                WallAlongZ(root, "ClassWallOuter", 20, sideGaps[i] - 6, sideGaps[i] + 6, roomH, new float[0], 0, 0, wallMat);
                WallAlongX(root, "ClassWallS", sideGaps[i] - 6, 6, 20, roomH, new float[0], 0, 0, wallMat);
                WallAlongX(root, "ClassWallN", sideGaps[i] + 6, 6, 20, roomH, new float[0], 0, 0, wallMat);
                HallSign(root, new Vector3(5.7f, doorH + 1.1f, sideGaps[i]), 90f, "School of " + SchoolLabels[east[i]], gold);
            }
            AddClassroom(info, 6, new Vector3(0, 0, 56), 0f, new Vector2(14, 12), roomH, floorMat, wallMat, trim, wood, gold);
            WallAlongZ(root, "ClassWallW", -7, 50, 62, roomH, new float[0], 0, 0, wallMat);
            WallAlongZ(root, "ClassWallE", 7, 50, 62, roomH, new float[0], 0, 0, wallMat);
            WallAlongX(root, "ClassWallBack", 62, -7, 7, roomH, new float[0], 0, 0, wallMat);
            HallSign(root, new Vector3(0, doorH + 1.1f, 49.7f), 0f, "School of Balance", gold);

            // ---- way out to the courtyard (west wall, near the office)
            var exit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            exit.name = "ExitDoor";
            exit.transform.SetParent(root, false);
            exit.transform.localPosition = new Vector3(-5.75f, 1.75f, 3f);
            exit.transform.localScale = new Vector3(0.2f, 3.5f, 2.2f);
            exit.GetComponent<Renderer>().sharedMaterial = doorMat;
            info.exitDoor = exit.transform;
            HallSign(root, new Vector3(-5.6f, 4.1f, 3f), -90f, "To the Courtyard (click door)", gold);
            info.hallEntry = Child(root, "HallEntry", new Vector3(0, 0.05f, 3f));
            info.hallEntry.rotation = Quaternion.identity;
            return info;
        }

        private static void AddClassroom(Interior info, int s, Vector3 center, float yaw, Vector2 size, float h,
            Material floorMat, Material wallMat, Material trim, Material wood, Material gold)
        {
            BuildRoomShell(info.root, "Class" + SchoolLabels[s], center, yaw == 0f ? size : new Vector2(size.y, size.x), h, floorMat, wallMat, trim);
            var room = Child(info.root, "Classroom_" + SchoolLabels[s], center);
            room.localRotation = Quaternion.Euler(0, yaw, 0);
            info.classrooms[s] = room;
            float W = size.x, D = size.y, back = D / 2f;
            Color col = SchoolColors[s];
            var tint = PMat("School" + SchoolLabels[s] + "Tint", Color.Lerp(col, Color.black, 0.45f), 4f);
            var bright = PMat("School" + SchoolLabels[s] + "Glow", col, 1f, col * 0.6f);
            var cloth = PMat("School" + SchoolLabels[s] + "Cloth", col, 1f);

            Block(room, "Rug", new Vector3(0, 0.02f, 0), new Vector3(W - 3f, 0.04f, D - 4f), tint);
            Block(room, "Board", new Vector3(0, 3.4f, back - 0.25f), new Vector3(6f, 2.6f, 0.12f), PMat("Chalkboard", new Color(0.15f, 0.25f, 0.2f), 1f));
            WorldText(room, "BoardText", new Vector3(0, 3.6f, back - 0.33f), Quaternion.identity, new Vector2(1000, 300), 0.005f, "School of " + SchoolLabels[s], 110).color = col;
            Block(room, "Lectern", new Vector3(0, 0.6f, back - 3.2f), new Vector3(1.2f, 1.2f, 0.7f), wood);
            for (int r = 0; r < 2; r++)
                for (int c = -1; c <= 1; c++)
                {
                    Block(room, "Desk", new Vector3(c * 2.8f, 0.45f, -1.5f - r * 2.4f), new Vector3(1.8f, 0.9f, 0.8f), wood);
                    Block(room, "Stool", new Vector3(c * 2.8f, 0.25f, -2.4f - r * 2.4f), new Vector3(0.6f, 0.5f, 0.6f), trim);
                }
            foreach (float x in new[] { -W / 2f + 0.25f, W / 2f - 0.25f })
                for (int b = 0; b < 2; b++)
                {
                    Block(room, "Banner", new Vector3(x, 4.2f, -1.5f + b * 4f), new Vector3(0.08f, 2.6f, 1.3f), cloth);
                    Shape(room, "BannerGem", GemMesh(), new Vector3(x * 0.98f, 4.6f, -1.5f + b * 4f), new Vector3(0.4f, 0.6f, 0.4f), bright, false, Quaternion.identity);
                }
            PointLight(room, new Vector3(0, h - 1.2f, 0), Color.Lerp(col, Color.white, 0.4f), 14f, 1.6f);
            ThemeProps(room, s, W, D, h, col, bright, trim);

            Color hat = Color.Lerp(col, Color.black, 0.35f);
            info.professors[s] = Wizard(room, "Professor" + SchoolLabels[s], new Vector3(0, 0, back - 2f), 0f, col, hat, s == 1 || s == 6, ProfessorNames[s]);
        }

        private static void ThemeProps(Transform room, int s, float W, float D, float h, Color col, Material bright, Material trim)
        {
            float cx = W / 2f - 1.3f, back = D / 2f;
            Vector3[] corners = { new Vector3(-cx, 0, back - 1.3f), new Vector3(cx, 0, back - 1.3f), new Vector3(-cx, 0, -back + 2.5f), new Vector3(cx, 0, -back + 2.5f) };
            switch (s)
            {
                case 0: // Fire: braziers and a lava channel
                    var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
                    foreach (var p in corners)
                    {
                        Shape(room, "Brazier", CylMesh(), p + new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.6f, 0.9f), PMat("Iron", new Color(0.25f, 0.22f, 0.22f), 1f), true, Quaternion.identity);
                        Shape(room, "BrazierFlame", ConeMesh(), p + new Vector3(0, 1.7f, 0), new Vector3(0.8f, 1f, 0.8f), flame, false, Quaternion.identity);
                    }
                    Block(room, "Lava", new Vector3(0, 0.03f, back - 1.2f), new Vector3(W - 4f, 0.06f, 0.8f), PMat("Lava", new Color(1f, 0.35f, 0.05f), 2f, new Color(0.9f, 0.3f, 0f)));
                    break;
                case 1: // Ice: crystals, snow drifts, icicles
                    var snow = PMat("Snow", new Color(0.95f, 0.97f, 1f), 4f);
                    foreach (var p in corners)
                    {
                        Shape(room, "SnowDrift", BlobMesh(), p + new Vector3(0, 0.1f, 0), new Vector3(2f, 0.6f, 2f), snow, false, Quaternion.identity);
                        Crystal(room, p + new Vector3(0, 0.9f, 0), 1.3f, new Color(0.62f, 0.85f, 1f));
                    }
                    for (int i = 0; i < 10; i++)
                        Shape(room, "Icicle", ConeMesh(), new Vector3(R(-W / 2f + 1, W / 2f - 1), h - 0.5f, R(-D / 2f + 1, D / 2f - 1)), new Vector3(0.3f, 1f, 0.3f), bright, false, Quaternion.Euler(180, 0, 0));
                    break;
                case 2: // Storm: lightning rods and floating clouds
                    var cloud = PMat("StormCloud", new Color(0.42f, 0.4f, 0.55f), 2f);
                    foreach (var p in corners)
                    {
                        Shape(room, "Rod", CylMesh(), p + new Vector3(0, 1.4f, 0), new Vector3(0.3f, 1.4f, 0.3f), PMat("Copper", new Color(0.75f, 0.45f, 0.25f), 1f), true, Quaternion.identity);
                        Shape(room, "RodSpark", GemMesh(), p + new Vector3(0, 3.1f, 0), new Vector3(0.6f, 0.8f, 0.6f), PMat("Spark", new Color(1f, 0.95f, 0.4f), 1f, new Color(0.9f, 0.85f, 0.3f)), false, Quaternion.identity);
                    }
                    for (int i = 0; i < 5; i++)
                        Shape(room, "Cloud", BlobMesh(), new Vector3(R(-W / 2f + 2, W / 2f - 2), h - 1.4f, R(-D / 2f + 2, D / 2f - 2)), new Vector3(2.6f, 0.9f, 1.8f), cloud, false, Quaternion.Euler(0, R(0, 360), 0));
                    break;
                case 3: // Life: a little indoor garden
                    var soil = PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f);
                    Tree(room, corners[0], 0.6f);
                    Tree(room, corners[1], 0.6f);
                    FlowerBed(room, corners[2], new Vector2(2f, 2f), soil);
                    FlowerBed(room, corners[3], new Vector2(2f, 2f), soil);
                    Mushroom(room, new Vector3(-cx, 0, 0.5f), 0.4f);
                    Mushroom(room, new Vector3(cx, 0, 0.5f), 0.4f);
                    for (int i = 0; i < 6; i++) Vine(room, new Vector3(-W / 2f + 1f + i * (W - 2f) / 5f, h, back - 0.4f), R(1.5f, 3f), 0);
                    break;
                case 4: // Death: gravestones, candles, ghostly glow
                    var grave = PMat("Gravestone", new Color(0.45f, 0.45f, 0.5f), 2f);
                    var ghost = PMat("GhostGlow", new Color(0.5f, 1f, 0.7f), 1f, new Color(0.3f, 0.8f, 0.5f));
                    foreach (var p in corners)
                    {
                        Block(room, "Gravestone", p + new Vector3(0, 0.7f, 0), new Vector3(0.9f, 1.4f, 0.25f), grave);
                        Shape(room, "GraveTop", CylMesh(), p + new Vector3(0, 1.4f, 0), new Vector3(0.9f, 0.12f, 0.25f), grave, false, Quaternion.Euler(90, 0, 0));
                        Shape(room, "Wisp", BlobMesh(), p + new Vector3(0.6f, 2.2f, 0), Vector3.one * 0.35f, ghost, false, Quaternion.identity);
                    }
                    var wax = PMat("CandleWax", new Color(0.9f, 0.88f, 0.8f), 1f);
                    var candleFlame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
                    for (int i = 0; i < 8; i++)
                    {
                        var p = new Vector3(-W / 2f + 1.2f + i * (W - 2.4f) / 7f, 0, back - 0.8f);
                        Block(room, "Candle", p + new Vector3(0, 0.4f, 0), new Vector3(0.15f, 0.8f, 0.15f), wax);
                        Shape(room, "CandleFlame", BlobMesh(), p + new Vector3(0, 0.9f, 0), new Vector3(0.12f, 0.2f, 0.12f), candleFlame, false, Quaternion.identity);
                    }
                    break;
                case 5: // Myth: beast statues on plinths and floating books
                    var statue = PMat("StatueGold", new Color(0.9f, 0.75f, 0.3f), 1f);
                    foreach (var p in corners)
                    {
                        Block(room, "Plinth", p + new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1f, 1.2f), trim);
                        Shape(room, "BeastBody", BlobMesh(), p + new Vector3(0, 1.5f, 0), new Vector3(0.9f, 0.9f, 1.3f), statue, false, Quaternion.identity);
                        Shape(room, "BeastHead", BlobMesh(), p + new Vector3(0, 2.2f, 0.45f), Vector3.one * 0.55f, statue, false, Quaternion.identity);
                        Shape(room, "BeastHorn", ConeMesh(), p + new Vector3(0, 2.6f, 0.5f), new Vector3(0.2f, 0.5f, 0.2f), statue, false, Quaternion.identity);
                    }
                    var page = PMat("BookBlue", new Color(0.2f, 0.35f, 0.85f), 1f);
                    for (int i = 0; i < 6; i++)
                        Block(room, "FloatingBook", new Vector3(R(-W / 2f + 2, W / 2f - 2), R(3.5f, 5.5f), R(-D / 2f + 2, D / 2f - 2)), new Vector3(0.6f, 0.12f, 0.45f), page,
                            Quaternion.Euler(R(-20, 20), R(0, 360), R(-20, 20)));
                    break;
                default: // Balance: sand floor, pyramids, hourglass
                    var sand = PMat("Sand", new Color(0.9f, 0.78f, 0.52f), 8f);
                    Block(room, "Sand", new Vector3(0, 0.05f, 0.5f), new Vector3(W - 1.5f, 0.06f, D - 2.5f), sand);
                    foreach (var p in corners)
                        Shape(room, "Pyramid", ConeMesh(), p + new Vector3(0, 0.8f, 0), new Vector3(1.8f, 1.6f, 1.8f), sand, true, Quaternion.Euler(0, 45, 0));
                    var glass = PMat("HourglassSand", col, 1f, col * 0.4f);
                    Vector3 hg = new Vector3(0, 0, back - 0.9f);
                    Block(room, "HourglassBase", hg + new Vector3(0, 0.1f, 0), new Vector3(1f, 0.2f, 1f), trim);
                    Shape(room, "HourglassLow", ConeMesh(), hg + new Vector3(0, 0.6f, 0), new Vector3(0.8f, 0.8f, 0.8f), glass, false, Quaternion.Euler(180, 0, 0));
                    Shape(room, "HourglassHigh", ConeMesh(), hg + new Vector3(0, 1.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), glass, false, Quaternion.identity);
                    Block(room, "HourglassTop", hg + new Vector3(0, 1.9f, 0), new Vector3(1f, 0.2f, 1f), trim);
                    break;
            }
        }

        // ------------------------------------------------------------ pieces

        /// <summary>Placeholder robed wizard facing local -Z (towards whoever stands in front of it).</summary>
        private static Transform Wizard(Transform parent, string name, Vector3 pos, float yaw, Color robe, Color hat, bool beard, string title)
        {
            var w = Child(parent, name, pos);
            w.localRotation = Quaternion.Euler(0, yaw, 0);
            var robeMat = PMat("Robe_" + ColorUtility.ToHtmlStringRGB(robe), robe, 1f);
            var hatMat = PMat("Hat_" + ColorUtility.ToHtmlStringRGB(hat), hat, 1f);
            Shape(w, "Robe", ConeMesh(), new Vector3(0, 0.8f, 0), new Vector3(1.1f, 1.6f, 1.1f), robeMat, false, Quaternion.identity);
            Shape(w, "Body", CylMesh(), new Vector3(0, 1.45f, 0), new Vector3(0.55f, 0.35f, 0.55f), robeMat, false, Quaternion.identity);
            Shape(w, "Head", BlobMesh(), new Vector3(0, 2.05f, 0), Vector3.one * 0.45f, PMat("Skin", new Color(0.95f, 0.8f, 0.65f), 1f), false, Quaternion.identity);
            if (beard) Shape(w, "Beard", ConeMesh(), new Vector3(0, 1.72f, -0.16f), new Vector3(0.32f, 0.5f, 0.22f), PMat("Beard", new Color(0.92f, 0.92f, 0.95f), 1f), false, Quaternion.Euler(180, 0, 0));
            Shape(w, "HatBrim", CylMesh(), new Vector3(0, 2.28f, 0), new Vector3(0.8f, 0.03f, 0.8f), hatMat, false, Quaternion.identity);
            Shape(w, "HatCone", ConeMesh(), new Vector3(0, 2.75f, 0), new Vector3(0.5f, 0.9f, 0.5f), hatMat, false, Quaternion.Euler(-8, 0, 0));
            Shape(w, "Staff", CylMesh(), new Vector3(0.55f, 1.1f, -0.1f), new Vector3(0.08f, 1.1f, 0.08f), PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f), false, Quaternion.identity);
            Shape(w, "StaffGem", GemMesh(), new Vector3(0.55f, 2.35f, -0.1f), new Vector3(0.25f, 0.35f, 0.25f), PMat("Gem_" + ColorUtility.ToHtmlStringRGB(robe), robe, 1f, robe * 0.7f), false, Quaternion.identity);
            WorldText(w, "NameTag", new Vector3(0, 3.5f, 0), Quaternion.identity, new Vector2(900, 120), 0.004f, title, 60);
            WorldText(w, "TalkHint", new Vector3(0, 3.2f, 0), Quaternion.identity, new Vector2(900, 90), 0.003f, "(click to talk or learn spells)", 50).color = new Color(1f, 0.9f, 0.5f);
            var box = w.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1.3f, 0);
            box.size = new Vector3(1.2f, 2.6f, 1.2f);
            return w;
        }

        private static void HallSign(Transform root, Vector3 pos, float yaw, string text, Material gold)
        {
            // Readable from the hall: the canvas' +Z points the way the viewer looks.
            Quaternion rot = Quaternion.Euler(0, yaw, 0);
            Block(root, "SignBoard", pos + rot * Vector3.forward * 0.06f, new Vector3(4.2f, 0.9f, 0.1f), PMat("SignWood", new Color(0.4f, 0.25f, 0.13f), 1f), rot);
            var t = WorldText(root, "SignText", pos, rot, new Vector2(1000, 200), 0.004f, text, 70);
            t.color = new Color(1f, 0.85f, 0.4f);
        }

        private static void BuildRoomShell(Transform root, string name, Vector3 center, Vector2 sizeXZ, float h, Material floorMat, Material wallMat, Material ceiling)
        {
            Block(root, name + "Floor", center + new Vector3(0, -0.25f, 0), new Vector3(sizeXZ.x, 0.5f, sizeXZ.y), floorMat);
            Block(root, name + "Ceiling", center + new Vector3(0, h + 0.25f, 0), new Vector3(sizeXZ.x, 0.5f, sizeXZ.y), ceiling);
        }

        private const float WallThick = 0.4f;

        /// <summary>Wall at fixed x running from z0 to z1, with door gaps (centres along z) topped by lintels.</summary>
        private static void WallAlongZ(Transform root, string name, float x, float z0, float z1, float h, float[] gaps, float gapW, float doorH, Material m)
        {
            foreach (var seg in Segments(z0, z1, gaps, gapW))
                Block(root, name, new Vector3(x, h / 2f, (seg.x + seg.y) / 2f), new Vector3(WallThick, h, seg.y - seg.x), m);
            foreach (float g in gaps)
                Block(root, name + "Lintel", new Vector3(x, (doorH + h) / 2f, g), new Vector3(WallThick, h - doorH, gapW), m);
        }

        /// <summary>Wall at fixed z running from x0 to x1, with door gaps (centres along x) topped by lintels.</summary>
        private static void WallAlongX(Transform root, string name, float z, float x0, float x1, float h, float[] gaps, float gapW, float doorH, Material m)
        {
            foreach (var seg in Segments(x0, x1, gaps, gapW))
                Block(root, name, new Vector3((seg.x + seg.y) / 2f, h / 2f, z), new Vector3(seg.y - seg.x, h, WallThick), m);
            foreach (float g in gaps)
                Block(root, name + "Lintel", new Vector3(g, (doorH + h) / 2f, z), new Vector3(gapW, h - doorH, WallThick), m);
        }

        private static List<Vector2> Segments(float a, float b, float[] gaps, float gapW)
        {
            var list = new List<Vector2>();
            var sorted = new List<float>(gaps);
            sorted.Sort();
            float start = a;
            foreach (float g in sorted)
            {
                if (g - gapW / 2f > start) list.Add(new Vector2(start, g - gapW / 2f));
                start = g + gapW / 2f;
            }
            if (b > start) list.Add(new Vector2(start, b));
            return list;
        }

        private static void PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity)
        {
            var l = Child(parent, "Light", pos).gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
        }

        // ------------------------------------------------------------ quests

        /// <summary>Quest manager, NPC talk relays, doors and the quest cutscenes (filmed in the real rooms).</summary>
        private static List<UdonSharpBehaviour> BuildQuests(Transform systems, Interior info, CutscenePlayer player, PlayerState playerState,
            PlayerHUD hud, Transform hubRoot, SpellTrainer trainer, InteractionMenu menu)
        {
            var list = new List<UdonSharpBehaviour>();
            var qm = UdonSharpUndo.AddComponent<QuestManager>(Child(systems, "QuestManager", Vector3.zero).gameObject);
            qm.playerState = playerState;
            qm.cutscenePlayer = player;
            qm.hud = hud;
            qm.schoolNames = (string[])SchoolLabels.Clone();
            qm.professorNames = (string[])ProfessorNames.Clone();
            qm.questTitles = new[] { "The Prophecy", "Trial of the School" };
            qm.questDescriptions = new[]
            {
                "Headmaster Greymantle says an old prophecy foretold your arrival. The Starfall Crystal is fading, and Morvane the Hollow King "
                + "stirs in the north. Before you can help, you must meet the professor of your chosen school.",
                "You have grown strong enough for your school's trial. Prove yourself in battle and your professor will teach you "
                + "a spell that is never taught in the classroom.",
            };
            qm.questRewards = new[] { qm.prophecyXp + " XP, " + qm.prophecyGold + " gold", "Your school's trial spell (level " + QuestManager.TRIAL_LEVEL + ")" };
            qm.professorTargets = (Transform[])info.professors.Clone();
            qm.headmasterTarget = info.headmaster;
            list.Add(qm);

            var sets = Child(systems, "QuestCutscenes", Vector3.zero);
            qm.professorCutscenes = new Cutscene[7];
            for (int s = 0; s < 7; s++)
            {
                Transform room = info.classrooms[s];
                Vector3 face = info.professors[s].localPosition + new Vector3(0, 2f, 0);
                float back = face.z;
                var shots = new[]
                {
                    Shot(room, "ShotWide", new Vector3(-4f, 3f, back - 6.5f), face + new Vector3(0, -0.4f, 0)),
                    Shot(room, "ShotMedium", new Vector3(1.4f, 2.1f, back - 4.6f), face),
                    Shot(room, "ShotClose", new Vector3(0, 2.05f, back - 3.4f), face),
                };
                var cs = MakeCutscene(sets, "Professor" + SchoolLabels[s], ProfessorNames[s], ProfessorLines(s), "prof_" + SchoolLabels[s].ToLowerInvariant(), shots);
                cs.onFinished = qm;
                cs.onFinishedEvent = nameof(QuestManager.OnProfessorFinished);
                qm.professorCutscenes[s] = cs;
                list.Add(cs);
                list.Add(Interactions(info.professors[s].gameObject, ProfessorNames[s], menu,
                    new[] { "Talk", "Learn Spells" },
                    new UdonSharpBehaviour[] { qm, trainer },
                    new[] { "Talk" + SchoolLabels[s], "Open" + SchoolLabels[s] }));
            }

            Transform[] officeShots = OfficeShots(info);
            qm.headmasterReport = MakeCutscene(sets, "HeadmasterReport", "Headmaster Greymantle", ReportLines, "hm_report", officeShots);
            qm.headmasterReport.onFinished = qm;
            qm.headmasterReport.onFinishedEvent = nameof(QuestManager.OnReportFinished);
            qm.headmasterHint = MakeCutscene(sets, "HeadmasterHint", "Headmaster Greymantle", HintLines, "hm_hint", new[] { officeShots[1] });
            qm.headmasterIdle = MakeCutscene(sets, "HeadmasterIdle", "Headmaster Greymantle", IdleLines, "hm_idle", new[] { officeShots[2] });
            list.Add(qm.headmasterReport);
            list.Add(qm.headmasterHint);
            list.Add(qm.headmasterIdle);
            list.Add(Relay(info.headmaster.gameObject, qm, nameof(QuestManager.TalkHeadmaster)));

            // Doors: hall -> courtyard (in front of the keep), keep door -> hall.
            Transform keepFront = Child(hubRoot, "KeepFrontSpawn", new Vector3(0, 6.1f, 42.5f));
            keepFront.rotation = Quaternion.Euler(0, 180f, 0);
            var exit = UdonSharpUndo.AddComponent<TeleportHelper>(info.exitDoor.gameObject);
            exit.defaultTarget = keepFront;
            list.Add(exit);

            var keepDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keepDoor.name = "KeepEntrance";
            keepDoor.transform.SetParent(hubRoot, false);
            keepDoor.transform.localPosition = new Vector3(0, 8.5f, 45.85f);
            keepDoor.transform.localScale = new Vector3(4f, 5f, 0.2f);
            keepDoor.GetComponent<Renderer>().sharedMaterial = PMat("Door", new Color(0.35f, 0.2f, 0.12f), 2f);
            var enter = UdonSharpUndo.AddComponent<TeleportHelper>(keepDoor);
            enter.defaultTarget = info.hallEntry;
            list.Add(enter);
            WorldText(hubRoot, "KeepEntranceLabel", new Vector3(0, 11.6f, 45.7f), Quaternion.identity, new Vector2(1200, 160), 0.004f, "Hall of Schools (click door)", 70).color = new Color(1f, 0.85f, 0.4f);
            return list;
        }

        private static Transform[] OfficeShots(Interior info)
        {
            Transform office = info.office;
            Vector3 face = info.headmaster.localPosition + new Vector3(0, 2f, 0);
            return new[]
            {
                Shot(office, "ShotWide", new Vector3(4.5f, 3f, -0.5f), face + new Vector3(0, -0.4f, 0)),
                Shot(office, "ShotMedium", new Vector3(-1.1f, 2.0f, -0.4f), face),
                Shot(office, "ShotClose", new Vector3(0, 2.05f, -1.9f), face),
                Shot(office, "ShotCloseLow", new Vector3(0.6f, 1.6f, -2.0f), face),
            };
        }

        private static EventRelay Relay(GameObject go, UdonSharpBehaviour target, string eventName)
        {
            var relay = UdonSharpUndo.AddComponent<EventRelay>(go);
            relay.target = target;
            relay.eventName = eventName;
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(relay);
            if (backing != null) backing.interactText = "Talk";
            return relay;
        }
    }
}
