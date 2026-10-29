using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    // Castle hub: PSX / classic-platformer style keep, walls, towers, gardens and floating isles.
    public static partial class WizardsVRCSceneBuilder
    {
        private const string MeshesDir = "Assets/WizardsVRC/Meshes";
        private const string ShakePrefKey = "WizardsVRC.PsxShake";
        private const float PsxAffine = 0.2f;

        // Vertex-snap grid for the PSX shader: higher = finer, subtler wobble; 0 = off.
        private static float PsxShake { get { return EditorPrefs.GetFloat(ShakePrefKey, 900f); } }

        [MenuItem("WizardsVRC/PSX Shake/Off")] private static void ShakeOff() { SetShake(0f); }
        [MenuItem("WizardsVRC/PSX Shake/Subtle (default)")] private static void ShakeSubtle() { SetShake(900f); }
        [MenuItem("WizardsVRC/PSX Shake/Strong (retro)")] private static void ShakeStrong() { SetShake(320f); }

        private static void SetShake(float res)
        {
            EditorPrefs.SetFloat(ShakePrefKey, res);
            foreach (string guid in AssetDatabase.FindAssets("PSX_ t:Material", new[] { MaterialsDir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || !mat.HasProperty("_SnapRes")) continue;
                mat.SetFloat("_SnapRes", res);
                mat.SetFloat("_Affine", PsxAffine);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
        private static System.Random _rng;

        // ------------------------------------------------------------ atmosphere

        private static void SetupPsxAtmosphere(Light sun)
        {
            var skyShader = Shader.Find("WizardsVRC/PSXSky");
            if (skyShader != null)
            {
                Directory.CreateDirectory(MaterialsDir);
                string path = MaterialsDir + "/PSXSky.mat";
                var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (sky == null)
                {
                    sky = new Material(skyShader);
                    AssetDatabase.CreateAsset(sky, path);
                }
                sky.shader = skyShader;
                sky.SetColor("_TopColor", new Color(0.28f, 0.33f, 0.9f));
                sky.SetColor("_HorizonColor", new Color(1f, 0.68f, 0.52f));
                sky.SetColor("_BottomColor", new Color(0.5f, 0.32f, 0.65f));
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.9f, 0.66f, 0.68f);
            // Far enough to see the mountains over the castle's tunnels; the skylines are hazed to match.
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 260f;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.6f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.52f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.28f, 0.3f);
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------ castle

        // Layout follows the "Starfall Castle" hub map: island + sea, south gate & bridge, round
        // central courtyard with fountain, keep (N) on a raised terrace, library tower (NW),
        // marketplace (W), portal plaza (NE), training yard (E), two gardens (SW / SE).
        private static void BuildCastle(Transform hub)
        {
            _rng = new System.Random(7);
            var root = new GameObject("Castle").transform;
            root.SetParent(hub, false);

            var grass = PMat("Grass", new Color(0.36f, 0.78f, 0.26f), 24f);
            var flag = PMat("Flagstone", new Color(0.82f, 0.72f, 0.56f), 16f);
            var stone = PMat("Stone", new Color(0.66f, 0.62f, 0.7f), 4f);
            var stoneDark = PMat("StoneDark", new Color(0.5f, 0.45f, 0.58f), 4f);
            var cliff = PMat("Cliff", new Color(0.55f, 0.42f, 0.36f), 6f);
            var roofPurple = PMat("RoofPurple", new Color(0.45f, 0.25f, 0.82f), 2f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var bannerBlue = PMat("BannerBlue", new Color(0.15f, 0.25f, 0.75f), 1f);
            var door = PMat("Door", new Color(0.35f, 0.2f, 0.12f), 2f);
            var window = PMat("Window", new Color(1f, 0.85f, 0.4f), 1f, new Color(0.9f, 0.7f, 0.25f));
            var stained = PMat("StainedGlass", new Color(0.4f, 0.5f, 1f), 1f, new Color(0.35f, 0.3f, 0.8f));
            var dirt = PMat("Dirt", new Color(0.62f, 0.45f, 0.28f), 6f);
            var water = PMat("Water", new Color(0.15f, 0.5f, 0.95f), 30f, new Color(0.05f, 0.18f, 0.35f));
            var waterBright = PMat("WaterBright", new Color(0.4f, 0.8f, 1f), 4f, new Color(0.25f, 0.55f, 0.8f));
            var wood = PMat("Wood", new Color(0.55f, 0.34f, 0.18f), 2f);

            // ---- sea + island
            Block(root, "Sea", new Vector3(0, -6.5f, 10), new Vector3(420, 1f, 420), water);
            var poly = new[]
            {
                new Vector2(-14, -38), new Vector2(14, -38), new Vector2(38, -29), new Vector2(50, -8),
                new Vector2(52, 18), new Vector2(46, 44), new Vector2(26, 64), new Vector2(-26, 64),
                new Vector2(-46, 44), new Vector2(-52, 18), new Vector2(-50, -8), new Vector2(-38, -29)
            };
            Shape(root, "IslandCliff", PrismMesh("PSX_Island", poly, -7f, -0.2f), Vector3.zero, Vector3.one, cliff, true, Quaternion.identity);
            Shape(root, "IslandGrass", PrismMesh("PSX_IslandTop", poly, -0.2f, 0f), Vector3.zero, Vector3.one, grass, true, Quaternion.identity);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                for (float t = 0.15f; t < 1f; t += 0.35f)
                {
                    Vector2 p = Vector2.Lerp(a, b, t) * 1.04f;
                    Shape(root, "ShoreRock", BlobMesh(), new Vector3(p.x, -6f, p.y), new Vector3(R(5, 9), R(3, 6), R(5, 9)), cliff, false, Quaternion.Euler(0, R(0, 360), 0));
                }
            }

            // ---- island walls + towers (gate in the south edge)
            Vector2 centroid = new Vector2(0, 13);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                if (i != 0) WallSeg(root, a, b, centroid, 1.4f, 0.8f, stone);
                Tower(root, Inset(a, centroid, 1.2f), 2.4f, 8f, 5f, roofPurple, stone, gold);
                Pennant(root, Inset(a, centroid, 1.2f) + new Vector3(0, 13.5f, 0), gold, bannerBlue);
            }
            Block(root, "GateWallW", new Vector3(-9, 3.5f, -37), new Vector3(10, 7, 2), stone);
            Block(root, "GateWallE", new Vector3(9, 3.5f, -37), new Vector3(10, 7, 2), stone);
            Block(root, "GateLintel", new Vector3(0, 6, -37), new Vector3(8, 2, 2), stoneDark);
            for (float x = -3f; x <= 3f; x += 0.75f) Block(root, "Portcullis", new Vector3(x, 4.6f, -37), new Vector3(0.15f, 0.9f, 0.15f), door);
            Shape(root, "GateCrest", GemMesh(), new Vector3(0, 6.2f, -38.1f), new Vector3(1.6f, 1.6f, 0.4f), gold, false, Quaternion.identity);
            foreach (float x in new[] { -5.5f, 5.5f })
            {
                Tower(root, new Vector3(x, 0, -37.5f), 3f, 11f, 6f, roofPurple, stone, gold);
                Pennant(root, new Vector3(x, 17.5f, -37.5f), gold, bannerBlue);
                StarBanner(root, new Vector3(x, 6, -40.6f), 0, bannerBlue, gold);
                Lamp(root, new Vector3(x * 0.6f, 0, -40f), wood, window);
            }

            // ---- bridge to a small rock islet
            Block(root, "Bridge", new Vector3(0, -0.2f, -54), new Vector3(6, 0.4f, 32), flag);
            Block(root, "BridgeRailW", new Vector3(-3.1f, 0.5f, -54), new Vector3(0.3f, 1f, 32), stone);
            Block(root, "BridgeRailE", new Vector3(3.1f, 0.5f, -54), new Vector3(0.3f, 1f, 32), stone);
            foreach (float z in new[] { -46f, -54f, -62f })
                foreach (float x in new[] { -2.2f, 2.2f })
                    Shape(root, "BridgePier", CylMesh(), new Vector3(x, -3.4f, z), new Vector3(1.6f, 3f, 1.6f), stone, false, Quaternion.identity);
            Shape(root, "Islet", PrismMesh("PSX_Islet", Octagon(9f), -7f, 0f), new Vector3(0, 0, -76), Vector3.one, cliff, true, Quaternion.identity);
            Shape(root, "IsletTop", CylMesh(), new Vector3(0, 0.01f, -76), new Vector3(15, 0.02f, 15), grass, false, Quaternion.identity);
            Lamp(root, new Vector3(-2.6f, 0, -50f), wood, window);
            Lamp(root, new Vector3(2.6f, 0, -58f), wood, window);

            // ---- main path + central courtyard with fountain
            Block(root, "MainPath", new Vector3(0, 0.03f, -20), new Vector3(6, 0.06f, 36), flag);
            for (float z = -30f; z <= -6f; z += 8f) { Lamp(root, new Vector3(-4, 0, z), wood, window); Lamp(root, new Vector3(4, 0, z), wood, window); }
            Shape(root, "Courtyard", CylMesh(), new Vector3(0, 0.04f, 12), new Vector3(28, 0.04f, 28), flag, true, Quaternion.Euler(0, 22.5f, 0));
            Block(root, "PathWest", new Vector3(-18.5f, 0.03f, 6), new Vector3(9, 0.06f, 4), flag);
            Block(root, "PathEast", new Vector3(18.5f, 0.03f, 6), new Vector3(9, 0.06f, 4), flag);
            Vector3 fountain = new Vector3(0, 0, 13);
            Shape(root, "FountainBasin", CylMesh(), fountain + new Vector3(0, 0.4f, 0), new Vector3(9, 0.4f, 9), stone, true, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "FountainWater", CylMesh(), fountain + new Vector3(0, 0.82f, 0), new Vector3(8, 0.03f, 8), waterBright, false, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "FountainPillar", CylMesh(), fountain + new Vector3(0, 1.3f, 0), new Vector3(1.4f, 1.3f, 1.4f), stone, false, Quaternion.identity);
            Shape(root, "FountainStar", GemMesh(), fountain + new Vector3(0, 2.8f, 0), new Vector3(3f, 0.5f, 3f), gold, false, Quaternion.identity);
            Shape(root, "StarfallCrystal", GemMesh(), fountain + new Vector3(0, 5f, 0), new Vector3(1.8f, 3.4f, 1.8f), PMat("Crystal_Starfall", new Color(0.35f, 0.6f, 1f), 1f, new Color(0.25f, 0.45f, 0.9f)), false, Quaternion.Euler(0, 15, 0));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Shape(root, "FountainJet", CylMesh(), fountain + new Vector3(Mathf.Cos(a) * 2.6f, 1.6f, Mathf.Sin(a) * 2.6f), new Vector3(0.25f, 0.8f, 0.25f), waterBright, false, Quaternion.identity);
            }
            foreach (float deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad;
                FlowerBed(root, new Vector3(Mathf.Cos(a) * 11.5f, 0, 12 + Mathf.Sin(a) * 11.5f), new Vector2(3, 3), dirt);
                Tree(root, new Vector3(Mathf.Cos(a) * 15.5f, 0, 12 + Mathf.Sin(a) * 15.5f), 0.9f);
            }

            // ---- castle keep on a raised terrace (y = 6), grand stairs from the courtyard
            Block(root, "KeepTerrace", new Vector3(0, 3, 49), new Vector3(40, 6, 22), stoneDark);
            Stairs(root, "KeepStairs", new Vector3(0, 0, 26), new Vector3(0, 6, 38), 10f, stone);
            Block(root, "KeepStairRailW", new Vector3(-5.4f, 3.4f, 32), new Vector3(0.8f, 1f, 13.4f), stoneDark, Quaternion.Euler(-26.6f, 0, 0));
            Block(root, "KeepStairRailE", new Vector3(5.4f, 3.4f, 32), new Vector3(0.8f, 1f, 13.4f), stoneDark, Quaternion.Euler(-26.6f, 0, 0));
            for (float x = -19f; x <= 19f; x += 2.5f)
                if (Mathf.Abs(x) > 5.5f) Block(root, "TerraceMerlon", new Vector3(x, 6.5f, 38.4f), new Vector3(1, 1, 0.6f), stone);
            Block(root, "Keep", new Vector3(0, 13, 53), new Vector3(26, 14, 14), stone);
            Block(root, "KeepDoor", new Vector3(0, 8.5f, 45.95f), new Vector3(4, 5, 0.1f), door);
            Shape(root, "KeepRose", GemMesh(), new Vector3(0, 14, 45.9f), new Vector3(3.5f, 3.5f, 0.2f), stained, false, Quaternion.Euler(0, 0, 30));
            for (int i = 0; i < 4; i++)
                Block(root, "KeepWindow", new Vector3(-10 + i * (i < 2 ? 4 : 4) + (i >= 2 ? 8 : 0), 15, 45.95f), new Vector3(1.2f, 2.4f, 0.1f), window);
            StarBanner(root, new Vector3(-4.5f, 11.5f, 45.9f), 0, bannerBlue, gold);
            StarBanner(root, new Vector3(4.5f, 11.5f, 45.9f), 0, bannerBlue, gold);
            foreach (var c in new[] { new Vector3(-13, 6, 46), new Vector3(13, 6, 46), new Vector3(-13, 6, 60), new Vector3(13, 6, 60) })
            {
                Tower(root, c, 3.2f, 18f, 7f, roofPurple, stone, gold);
                Pennant(root, c + new Vector3(0, 25.5f, 0), gold, bannerBlue);
            }
            Tower(root, new Vector3(0, 20, 53), 4f, 12f, 10f, roofPurple, stone, gold);
            Pennant(root, new Vector3(0, 43, 53), gold, bannerBlue);
            Planter(root, new Vector3(-8, 6, 41), dirt);
            Planter(root, new Vector3(8, 6, 41), dirt);
            Tree(root, new Vector3(-17, 6, 42), 0.8f);
            Tree(root, new Vector3(17, 6, 42), 0.8f);

            // ---- library tower (NW): terrace y = 3, spiral path to a top balcony
            Block(root, "LibraryTerrace", new Vector3(-34, 1.5f, 31), new Vector3(20, 3, 20), stoneDark);
            Stairs(root, "LibraryStairs", new Vector3(-14, 0, 30), new Vector3(-24, 3, 30), 4f, stone);
            Vector3 lib = new Vector3(-36, 3, 32);
            Tower(root, lib, 4.5f, 14f, 7f, roofPurple, stone, gold);
            Pennant(root, lib + new Vector3(0, 21.5f, 0), gold, bannerBlue);
            Block(root, "LibraryDoor", lib + new Vector3(4.45f, 1.6f, 0), new Vector3(0.1f, 3.2f, 2f), door);
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + 90f * k) * Mathf.Deg2Rad;
                Block(root, "LibraryWindow", lib + new Vector3(Mathf.Cos(a) * 4.3f, 8 + k, Mathf.Sin(a) * 4.3f), new Vector3(0.9f, 1.6f, 0.9f), window);
            }
            const int libSteps = 24;
            for (int i = 0; i < libSteps; i++)
            {
                float a0 = i / (float)libSteps * Mathf.PI * 2f, a1 = (i + 1) / (float)libSteps * Mathf.PI * 2f;
                Vector3 p0 = lib + new Vector3(Mathf.Cos(a0) * 6.2f, i * 0.5f, Mathf.Sin(a0) * 6.2f);
                Vector3 p1 = lib + new Vector3(Mathf.Cos(a1) * 6.2f, (i + 1) * 0.5f, Mathf.Sin(a1) * 6.2f);
                Ramp(root, "LibrarySpiral" + i, p0, p1 + (p1 - p0).normalized * 0.15f, 1.7f, stone);
            }
            Block(root, "LibraryBalcony", lib + new Vector3(7.6f, 11.85f, 0), new Vector3(4, 0.3f, 4), stone);
            Block(root, "LibraryBalconyRail", lib + new Vector3(9.5f, 12.4f, 0), new Vector3(0.2f, 0.8f, 4), gold);
            Crystal(root, lib + new Vector3(8.2f, 13f, 1.3f), 0.7f, new Color(0.8f, 0.5f, 1f));
            Planter(root, new Vector3(-27, 3, 23), dirt);
            Planter(root, new Vector3(-42, 3, 23), dirt);

            // ---- marketplace (W)
            Vector3 market = new Vector3(-32, 0, 4);
            Shape(root, "MarketSquare", CylMesh(), market + new Vector3(0, 0.035f, 0), new Vector3(19, 0.035f, 19), flag, false, Quaternion.Euler(0, 22.5f, 0));
            var awnings = new[] { new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.85f), new Color(0.9f, 0.6f, 0.15f), new Color(0.2f, 0.65f, 0.3f), new Color(0.6f, 0.25f, 0.75f) };
            int s = 0;
            foreach (float deg in new[] { 60f, 120f, 180f, 240f, 300f })
            {
                float a = deg * Mathf.Deg2Rad;
                Stall(root, market + new Vector3(Mathf.Cos(a) * 6.5f, 0, Mathf.Sin(a) * 6.5f), -deg - 90f, wood, PMat("Awning" + s, awnings[s], 1f));
                s++;
            }
            Shape(root, "MarketBrazier", CylMesh(), market + new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.4f, 1.2f), stoneDark, true, Quaternion.identity);
            Shape(root, "MarketFire", GemMesh(), market + new Vector3(0, 1.2f, 0), new Vector3(0.8f, 1.2f, 0.8f), PMat("Fire", new Color(1f, 0.55f, 0.15f), 1f, new Color(1f, 0.45f, 0.1f)), false, Quaternion.identity);

            // ---- portal plaza (NE): terrace y = 4 with three portal arches
            Block(root, "PortalTerrace", new Vector3(33, 2, 30), new Vector3(20, 4, 18), stoneDark);
            Stairs(root, "PortalStairs", new Vector3(13, 0, 25), new Vector3(23, 4, 25), 4f, stone);
            var portalColors = new[] { new Color(0.25f, 0.55f, 1f), new Color(0.75f, 0.3f, 1f), new Color(0.25f, 1f, 0.55f) };
            for (int i = 0; i < 3; i++)
                PortalArch(root, new Vector3(26 + i * 7, 4, 36), stone, PMat("Portal" + i, portalColors[i], 1f, portalColors[i] * 0.8f));
            Shape(root, "RuneCircle", CylMesh(), new Vector3(33, 4.03f, 28), new Vector3(6, 0.03f, 6), PMat("Rune", new Color(0.3f, 0.5f, 1f), 1f, new Color(0.15f, 0.25f, 0.6f)), false, Quaternion.Euler(0, 22.5f, 0));
            StarBanner(root, new Vector3(23.9f, 1.8f, 33), 90, bannerBlue, gold);
            Planter(root, new Vector3(41, 4, 23), dirt);

            // ---- training yard (E): arena teleporter pad lives here (see BuildQueuePad)
            Block(root, "TrainingYard", new Vector3(33, 0.03f, 4), new Vector3(20, 0.06f, 16), dirt);
            for (float x = 23.5f; x <= 42.5f; x += 2.375f)
            {
                Block(root, "FencePost", new Vector3(x, 0.6f, -3.6f), new Vector3(0.2f, 1.2f, 0.2f), wood);
                Block(root, "FencePost", new Vector3(x, 0.6f, 11.6f), new Vector3(0.2f, 1.2f, 0.2f), wood);
            }
            Block(root, "FenceRailS", new Vector3(33, 0.9f, -3.6f), new Vector3(19, 0.15f, 0.1f), wood);
            Block(root, "FenceRailN", new Vector3(33, 0.9f, 11.6f), new Vector3(19, 0.15f, 0.1f), wood);
            Block(root, "FenceRailE", new Vector3(42.6f, 0.9f, 4), new Vector3(0.1f, 0.15f, 15.2f), wood);
            foreach (var p in new[] { new Vector3(38, 0, 9), new Vector3(40.5f, 0, 5.5f), new Vector3(40.5f, 0, 1.5f) }) Dummy(root, p, wood);
            Shape(root, "Tent", ConeMesh(), new Vector3(37, 2, -0.5f), new Vector3(5, 4, 5), PMat("Canvas", new Color(0.92f, 0.85f, 0.65f), 1f), true, Quaternion.Euler(0, 22.5f, 0));
            StarBanner(root, new Vector3(42.4f, 1.8f, 9), -90, bannerBlue, gold);

            // ---- garden terrace (SW) and sunrise gardens (SE)
            Gazebo(root, new Vector3(-33, 0, -17), wood, roofPurple, gold);
            Shape(root, "Pond", CylMesh(), new Vector3(-23, 0.05f, -20), new Vector3(8, 0.05f, 8), waterBright, false, Quaternion.identity);
            Shape(root, "PondRim", CylMesh(), new Vector3(-23, 0.03f, -20), new Vector3(9, 0.03f, 9), stone, false, Quaternion.identity);
            for (int i = 0; i < 4; i++)
                Shape(root, "LilyPad", CylMesh(), new Vector3(-23 + R(-2.5f, 2.5f), 0.12f, -20 + R(-2.5f, 2.5f)), new Vector3(0.8f, 0.02f, 0.8f), PMat("Leaf0", LeafColors[0], 2f), false, Quaternion.identity);
            Bench(root, new Vector3(-27, 0, -25), 20, wood);
            FlowerBed(root, new Vector3(-30, 0, -9), new Vector2(4, 3), dirt);
            Gazebo(root, new Vector3(32, 0, -16), wood, roofPurple, gold);
            foreach (var p in new[] { new Vector3(14, 0, -12), new Vector3(19, 0, -16), new Vector3(24, 0, -20) }) VineArch(root, p, 45f, wood);
            FlowerBed(root, new Vector3(24, 0, -10), new Vector2(4, 3), dirt);
            FlowerBed(root, new Vector3(36, 0, -24), new Vector2(3, 3), dirt);
            Bench(root, new Vector3(27, 0, -25), -20, wood);

            // ---- waterfalls off the cliffs
            foreach (var w in new[] { new Vector4(-44, -0.1f, -18, 60), new Vector4(44, -0.1f, -18, -60), new Vector4(-50, -0.1f, 30, 95), new Vector4(48, -0.1f, 32, -100) })
            {
                Block(root, "Waterfall", new Vector3(w.x, -3.2f, w.z), new Vector3(4, 6.4f, 0.6f), waterBright, Quaternion.Euler(0, w.w, 0));
                Shape(root, "Foam", BlobMesh(), new Vector3(w.x, -6f, w.z), new Vector3(6, 1.5f, 6), PMat("Foam", Color.white, 1f, new Color(0.4f, 0.45f, 0.5f)), false, Quaternion.identity);
            }

            // ---- trees, bushes, vines filling the gaps
            var keepOut = new[]
            {
                new Rect(-15, -38, 30, 25), new Rect(-16, -3, 32, 30), new Rect(-21, 25, 42, 40), new Rect(-45, 20, 22, 22),
                new Rect(-42, -6, 30, 20), new Rect(12, 20, 32, 20), new Rect(22, -5, 22, 18), new Rect(-38, -24, 20, 18),
                new Rect(12, -24, 26, 18), new Rect(-24, 26, 12, 8), new Rect(12, 22, 12, 6)
            };
            int placed = 0;
            for (int tries = 0; placed < 55 && tries < 900; tries++)
            {
                var p2 = new Vector2(R(-48f, 48f), R(-34f, 62f));
                if (!InsidePoly(poly, Vector2.Lerp(p2, centroid, -0.06f))) continue;
                bool blocked = false;
                foreach (var r in keepOut) if (r.Contains(p2)) { blocked = true; break; }
                if (blocked) continue;
                var p = new Vector3(p2.x, 0, p2.y);
                int kind = placed % 5;
                if (kind < 2) Conifer(root, p, R(0.8f, 1.4f));
                else if (kind == 2) Tree(root, p, R(0.8f, 1.2f));
                else if (kind == 3) Bush(root, p, R(0.9f, 1.4f));
                else if (placed % 10 == 9) Mushroom(root, p, R(0.6f, 1.2f));
                else FlowerBed(root, p, new Vector2(2, 2), dirt);
                placed++;
            }
            for (float x = -10f; x <= 10f; x += 2.6f) Vine(root, new Vector3(x, 6, 37.95f), R(2f, 4.5f), 0);
            for (float z = 22f; z <= 40f; z += 3f) Vine(root, new Vector3(-23.95f, 3, z), R(1.5f, 2.8f), 90);
            for (float z = 22f; z <= 38f; z += 3f) Vine(root, new Vector3(22.95f, 4, z), R(1.5f, 3.5f), 90);
            for (float x = -12f; x <= 12f; x += 3f) if (Mathf.Abs(x) > 4f) Vine(root, new Vector3(x, 7, -38.05f), R(2f, 5f), 0);
        }

        // ------------------------------------------------------------ hub-map pieces

        private static Vector3 Inset(Vector2 p, Vector2 toward, float d)
        {
            Vector2 q = p + (toward - p).normalized * d;
            return new Vector3(q.x, 0, q.y);
        }

        private static void WallSeg(Transform root, Vector2 a, Vector2 b, Vector2 centroid, float height, float thick, Material mat)
        {
            Vector3 pa = Inset(a, centroid, 1.2f), pb = Inset(b, centroid, 1.2f);
            Vector3 d = pb - pa;
            Block(root, "IslandWall", (pa + pb) / 2f + Vector3.up * height / 2f, new Vector3(thick, height, d.magnitude), mat, Quaternion.LookRotation(d.normalized));
        }

        private static bool InsidePoly(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        private static Vector2[] Octagon(float r)
        {
            var o = new Vector2[8];
            for (int i = 0; i < 8; i++) { var v = Ring(i, 8, r, 0); o[i] = new Vector2(v.x, v.z); }
            return o;
        }

        private static void Block(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            Block(parent, name, pos, scale, mat);
            parent.GetChild(parent.childCount - 1).localRotation = rot;
        }

        // Visual steps (no colliders) over an invisible ramp collider, so players glide up smoothly.
        private static void Stairs(Transform root, string name, Vector3 bottom, Vector3 top, float width, Material mat)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            Vector3 flat = new Vector3(top.x - bottom.x, 0, top.z - bottom.z);
            float rise = top.y - bottom.y, run = flat.magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(rise / 0.4f));
            var rot = Quaternion.LookRotation(flat.normalized);
            for (int i = 0; i < n; i++)
            {
                float h = (i + 1) * rise / n;
                Vector3 p = bottom + flat.normalized * (run * (i + 0.5f) / n);
                Block(t, "Step", new Vector3(p.x, bottom.y + h / 2f, p.z), new Vector3(width, h, run / n), mat, rot);
            }
            StripColliders(t, "Step");
            Ramp(t, "StairCollider", bottom, top, width, mat);
            var col = t.Find("StairCollider");
            Object.DestroyImmediate(col.GetComponent<MeshRenderer>());
        }

        private static void Pennant(Transform root, Vector3 top, Material pole, Material cloth)
        {
            Block(root, "FlagPole", top + new Vector3(0, -0.8f, 0), new Vector3(0.12f, 1.6f, 0.12f), pole);
            Block(root, "Flag", top + new Vector3(0.45f, -0.4f, 0), new Vector3(0.8f, 0.5f, 0.05f), cloth);
        }

        private static void StarBanner(Transform root, Vector3 center, float yaw, Material cloth, Material gold)
        {
            var t = new GameObject("StarBanner").transform;
            t.SetParent(root, false);
            t.localPosition = center;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            Block(t, "Cloth", Vector3.zero, new Vector3(1.6f, 3.4f, 0.08f), cloth);
            Block(t, "Rod", new Vector3(0, 1.75f, 0), new Vector3(2f, 0.12f, 0.12f), gold);
            Shape(t, "Star", GemMesh(), new Vector3(0, 0.4f, -0.08f), new Vector3(0.9f, 0.9f, 0.1f), gold, false, Quaternion.Euler(0, 0, 30));
            StripColliders(t, "Cloth");
        }

        private static void Lamp(Transform root, Vector3 p, Material post, Material glow)
        {
            Block(root, "LampPost", p + new Vector3(0, 1.3f, 0), new Vector3(0.18f, 2.6f, 0.18f), post);
            Block(root, "LampHead", p + new Vector3(0, 2.75f, 0), new Vector3(0.4f, 0.4f, 0.4f), glow);
        }

        private static void Conifer(Transform root, Vector3 p, float s)
        {
            var t = new GameObject("Conifer").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            Shape(t, "Trunk", CylMesh(), new Vector3(0, 0.6f * s, 0), new Vector3(0.5f * s, 0.6f * s, 0.5f * s), PMat("Bark", new Color(0.5f, 0.3f, 0.16f), 2f), true, Quaternion.identity);
            var needles = PMat("Pine", new Color(0.12f, 0.48f, 0.28f), 2f);
            for (int i = 0; i < 3; i++)
                Shape(t, "Needles", ConeMesh(), new Vector3(0, (2f + i * 1.5f) * s, 0), new Vector3((3.2f - i * 0.8f) * s, 2.6f * s, (3.2f - i * 0.8f) * s), needles, false, Quaternion.Euler(0, i * 20f, 0));
        }

        private static void Stall(Transform root, Vector3 p, float yaw, Material wood, Material awning)
        {
            var t = new GameObject("MarketStall").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            Block(t, "Counter", new Vector3(0, 0.5f, 0.6f), new Vector3(3, 1, 0.8f), wood);
            foreach (var c in new[] { new Vector3(-1.4f, 1.3f, 0.9f), new Vector3(1.4f, 1.3f, 0.9f), new Vector3(-1.4f, 1.3f, -0.9f), new Vector3(1.4f, 1.3f, -0.9f) })
                Block(t, "Post", c, new Vector3(0.15f, 2.6f, 0.15f), wood);
            var white = PMat("AwningWhite", new Color(0.95f, 0.92f, 0.85f), 1f);
            for (int i = 0; i < 6; i++)
                Block(t, "Awning", new Vector3(-1.5f + 0.25f + i * 0.5f, 2.75f, 0.1f), new Vector3(0.5f, 0.08f, 2.4f), i % 2 == 0 ? awning : white, Quaternion.Euler(-15, 0, 0));
            for (int i = 0; i < 3; i++)
                Shape(t, "Goods", BlobMesh(), new Vector3(-0.9f + i * 0.9f, 1.15f, 0.6f), Vector3.one * 0.35f, PMat("Petal" + i, PetalColors[i], 1f), false, Quaternion.identity);
        }

        private static void PortalArch(Transform root, Vector3 baseCenter, Material stone, Material glow)
        {
            Block(root, "PortalPillarL", baseCenter + new Vector3(-2.1f, 2.5f, 0), new Vector3(0.8f, 5, 0.8f), stone);
            Block(root, "PortalPillarR", baseCenter + new Vector3(2.1f, 2.5f, 0), new Vector3(0.8f, 5, 0.8f), stone);
            Block(root, "PortalTop", baseCenter + new Vector3(0, 5.3f, 0), new Vector3(5, 0.8f, 0.9f), stone);
            Shape(root, "PortalKeystone", GemMesh(), baseCenter + new Vector3(0, 6.1f, 0), new Vector3(0.9f, 1.2f, 0.4f), glow, false, Quaternion.identity);
            Shape(root, "PortalSwirl", CylMesh(), baseCenter + new Vector3(0, 2.5f, 0), new Vector3(3.4f, 0.03f, 4.8f), glow, false, Quaternion.Euler(90, 0, 0));
        }

        private static void Dummy(Transform root, Vector3 p, Material wood)
        {
            Block(root, "DummyPost", p + new Vector3(0, 1f, 0), new Vector3(0.2f, 2f, 0.2f), wood);
            Shape(root, "Target", CylMesh(), p + new Vector3(0, 1.6f, 0), new Vector3(1.2f, 0.05f, 1.2f), PMat("TargetRed", new Color(0.9f, 0.2f, 0.2f), 1f), false, Quaternion.Euler(0, 0, 90));
            Shape(root, "TargetEye", CylMesh(), p + new Vector3(-0.06f, 1.6f, 0), new Vector3(0.5f, 0.05f, 0.5f), PMat("AwningWhite", new Color(0.95f, 0.92f, 0.85f), 1f), false, Quaternion.Euler(0, 0, 90));
        }

        private static void Gazebo(Transform root, Vector3 p, Material wood, Material roof, Material gold)
        {
            Shape(root, "GazeboFloor", CylMesh(), p + new Vector3(0, 0.15f, 0), new Vector3(6, 0.15f, 6), PMat("Stone", new Color(0.66f, 0.62f, 0.7f), 4f), true, Quaternion.Euler(0, 22.5f, 0));
            for (int i = 0; i < 8; i++)
            {
                if (i == 6) continue;
                var c = Ring(i, 8, 2.7f, 0);
                Block(root, "GazeboPost", p + c + new Vector3(0, 1.65f, 0), new Vector3(0.2f, 3f, 0.2f), wood);
            }
            Shape(root, "GazeboRoof", ConeMesh(), p + new Vector3(0, 4.2f, 0), new Vector3(7, 2.4f, 7), roof, false, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "GazeboFinial", BlobMesh(), p + new Vector3(0, 5.5f, 0), Vector3.one * 0.4f, gold, false, Quaternion.identity);
            Bench(root, p + new Vector3(0, 0.3f, 1.6f), 180, wood);
        }

        private static void Bench(Transform root, Vector3 p, float yaw, Material wood)
        {
            Block(root, "Bench", p + new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.12f, 0.5f), wood, Quaternion.Euler(0, yaw, 0));
        }

        private static void VineArch(Transform root, Vector3 p, float yaw, Material wood)
        {
            var t = new GameObject("VineArch").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            Block(t, "ArchPostL", new Vector3(-1.5f, 1.4f, 0), new Vector3(0.2f, 2.8f, 0.2f), wood);
            Block(t, "ArchPostR", new Vector3(1.5f, 1.4f, 0), new Vector3(0.2f, 2.8f, 0.2f), wood);
            for (int i = 0; i <= 6; i++)
            {
                float a = i / 6f * Mathf.PI;
                int idx = i % 2 == 0 ? 0 : 3;
                Shape(t, "ArchLeaf", BlobMesh(), new Vector3(Mathf.Cos(a) * 1.5f, 2.8f + Mathf.Sin(a) * 1f, 0), Vector3.one * 0.8f, PMat("Leaf" + idx, LeafColors[idx], 2f), false, Quaternion.identity);
            }
        }

        // Convex polygon (x,z) extruded between yBottom and yTop.
        private static Mesh PrismMesh(string name, Vector2[] poly, float yBottom, float yTop)
        {
            var t = new List<Vector3>();
            Vector2 c = Vector2.zero;
            foreach (var v in poly) c += v;
            c /= poly.Length;
            Vector3 top = new Vector3(c.x, yTop, c.y), bot = new Vector3(c.x, yBottom, c.y);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector3 at = new Vector3(a.x, yTop, a.y), bt = new Vector3(b.x, yTop, b.y);
                Vector3 ab = new Vector3(a.x, yBottom, a.y), bb = new Vector3(b.x, yBottom, b.y);
                Quad(t, ab, bb, bt, at);
                t.Add(top); t.Add(at); t.Add(bt);
                t.Add(bot); t.Add(ab); t.Add(bb);
            }
            return SaveMesh(name, t, new Vector3(c.x, (yTop + yBottom) / 2f, c.y));
        }

        // ------------------------------------------------------------ pieces

        private static void Tower(Transform root, Vector3 c, float radius, float height, float roofH, Material roof, Material wall, Material gold)
        {
            Shape(root, "Tower", CylMesh(), c + new Vector3(0, height / 2f, 0), new Vector3(radius * 2, height / 2f, radius * 2), wall, true, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "TowerRoof", ConeMesh(), c + new Vector3(0, height + roofH / 2f, 0), new Vector3(radius * 2.4f, roofH, radius * 2.4f), roof, true, Quaternion.Euler(0, 22.5f, 0));
            Shape(root, "TowerFinial", BlobMesh(), c + new Vector3(0, height + roofH + 0.2f, 0), Vector3.one * 0.6f, gold, false, Quaternion.identity);
        }

        private static void Isle(Transform root, Vector3 top, float size, Material grass, Material dirt)
        {
            Shape(root, "IsleTop", CylMesh(), top + new Vector3(0, -0.2f, 0), new Vector3(size, 0.2f, size), grass, true, Quaternion.identity);
            Shape(root, "IsleRock", ConeMesh(), top + new Vector3(0, -0.4f - size * 0.5f, 0), new Vector3(size * 0.95f, size, size * 0.95f), dirt, false, Quaternion.Euler(180, 0, 0));
        }

        private static readonly Color[] LeafColors =
        {
            new Color(0.2f, 0.7f, 0.25f), new Color(0.35f, 0.82f, 0.2f), new Color(0.15f, 0.55f, 0.35f),
            new Color(1f, 0.55f, 0.75f), new Color(0.65f, 0.4f, 0.9f)
        };

        private static void Tree(Transform root, Vector3 p, float s)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(root, false);
            t.localPosition = p;
            Shape(t, "Trunk", CylMesh(), new Vector3(0, 1.6f * s, 0), new Vector3(0.7f * s, 1.6f * s, 0.7f * s), PMat("Bark", new Color(0.5f, 0.3f, 0.16f), 2f), true, Quaternion.identity);
            int leafIdx = _rng.Next(0, 10) < 7 ? _rng.Next(0, 3) : _rng.Next(3, 5);
            var leaf = PMat("Leaf" + leafIdx, LeafColors[leafIdx], 2f);
            for (int i = 0; i < 3; i++)
                Shape(t, "Leaves", BlobMesh(), new Vector3(R(-0.8f, 0.8f) * s, (3.4f + i * 0.9f) * s, R(-0.8f, 0.8f) * s),
                    Vector3.one * (3.4f - i * 0.7f) * s, leaf, false, Quaternion.Euler(0, R(0, 360), 0));
        }

        private static void Bush(Transform root, Vector3 p, float s)
        {
            int idx = _rng.Next(0, 3);
            Shape(root, "Bush", BlobMesh(), p + new Vector3(0, 0.45f * s, 0), new Vector3(1.6f * s, 1.1f * s, 1.6f * s),
                PMat("Leaf" + idx, LeafColors[idx], 2f), false, Quaternion.Euler(0, R(0, 360), 0));
        }

        private static readonly Color[] PetalColors =
        {
            new Color(1f, 0.3f, 0.4f), new Color(1f, 0.85f, 0.2f), new Color(0.6f, 0.4f, 1f), new Color(1f, 0.6f, 0.9f), new Color(0.3f, 0.8f, 1f)
        };

        private static void Flower(Transform root, Vector3 p)
        {
            float h = R(0.35f, 0.7f);
            Block(root, "Stem", p + new Vector3(0, h / 2f, 0), new Vector3(0.06f, h, 0.06f), PMat("Stem", new Color(0.25f, 0.6f, 0.2f), 1f));
            int idx = _rng.Next(0, PetalColors.Length);
            Shape(root, "Bloom", BlobMesh(), p + new Vector3(0, h, 0), Vector3.one * R(0.22f, 0.32f), PMat("Petal" + idx, PetalColors[idx], 1f), false, Quaternion.identity);
        }

        private static void FlowerBed(Transform root, Vector3 p, Vector2 size, Material dirt)
        {
            var bed = new GameObject("FlowerBed").transform;
            bed.SetParent(root, false);
            bed.localPosition = p;
            Block(bed, "Soil", new Vector3(0, 0.15f, 0), new Vector3(size.x, 0.3f, size.y), dirt);
            int n = Mathf.RoundToInt(size.x * size.y * 1.2f);
            for (int i = 0; i < n; i++)
                Flower(bed, new Vector3(R(-0.45f, 0.45f) * size.x, 0.3f, R(-0.45f, 0.45f) * size.y));
            StripColliders(bed, "Stem");
        }

        private static void Mushroom(Transform root, Vector3 p, float s)
        {
            var m = new GameObject("Mushroom").transform;
            m.SetParent(root, false);
            m.localPosition = p;
            Shape(m, "Stalk", CylMesh(), new Vector3(0, 1.2f * s, 0), new Vector3(0.9f * s, 1.2f * s, 0.9f * s), PMat("MushroomStalk", new Color(0.95f, 0.9f, 0.78f), 1f), true, Quaternion.identity);
            bool blue = _rng.Next(0, 3) == 0;
            Shape(m, "Cap", BlobMesh(), new Vector3(0, 2.5f * s, 0), new Vector3(3.2f * s, 1.3f * s, 3.2f * s),
                blue ? PMat("MushroomCapBlue", new Color(0.3f, 0.5f, 1f), 1f) : PMat("MushroomCapRed", new Color(0.95f, 0.2f, 0.25f), 1f), true, Quaternion.identity);
            var spot = PMat("MushroomSpot", new Color(1f, 1f, 0.95f), 1f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + R(0, 0.5f);
                Shape(m, "Spot", BlobMesh(), new Vector3(Mathf.Cos(a) * 1.05f * s, 2.85f * s, Mathf.Sin(a) * 1.05f * s), Vector3.one * 0.45f * s, spot, false, Quaternion.identity);
            }
        }

        private static void Vine(Transform root, Vector3 top, float length, float yaw)
        {
            var v = new GameObject("Vine").transform;
            v.SetParent(root, false);
            v.localPosition = top;
            v.localRotation = Quaternion.Euler(0, yaw, 0);
            var vineMat = PMat("Vine", new Color(0.15f, 0.5f, 0.2f), 1f);
            Block(v, "Strand", new Vector3(0, -length / 2f, 0), new Vector3(0.25f, length, 0.12f), vineMat);
            for (float y = 0.4f; y < length; y += 0.7f)
                Shape(v, "VineLeaf", BlobMesh(), new Vector3(R(-0.25f, 0.25f), -y, 0), new Vector3(0.5f, 0.35f, 0.25f), PMat("Leaf0", LeafColors[0], 2f), false, Quaternion.identity);
            StripColliders(v, "Strand");
        }

        private static void Planter(Transform root, Vector3 p, Material dirt)
        {
            var pl = new GameObject("Planter").transform;
            pl.SetParent(root, false);
            pl.localPosition = p;
            Block(pl, "Box", new Vector3(0, 0.3f, 0), new Vector3(1.4f, 0.6f, 1.4f), PMat("PlanterWood", new Color(0.55f, 0.34f, 0.18f), 1f));
            Bush(pl, new Vector3(0, 0.4f, 0), 0.7f);
            for (int i = 0; i < 4; i++) Flower(pl, new Vector3(R(-0.5f, 0.5f), 0.6f, R(-0.5f, 0.5f)));
            StripColliders(pl, "Stem");
        }

        private static void Crystal(Transform root, Vector3 p, float s, Color c)
        {
            string key = "Crystal_" + ColorUtility.ToHtmlStringRGB(c);
            Shape(root, "Crystal", GemMesh(), p, new Vector3(0.8f, 1.6f, 0.8f) * s, PMat(key, c, 1f, c * 0.6f), false, Quaternion.Euler(0, R(0, 90), 0));
        }

        // ------------------------------------------------------------ helpers

        private static float R(float min, float max) { return min + (float)_rng.NextDouble() * (max - min); }

        private static void StripColliders(Transform t, string childName)
        {
            foreach (var col in t.GetComponentsInChildren<Collider>())
                if (col.gameObject.name == childName) Object.DestroyImmediate(col);
        }

        private static void Ramp(Transform parent, string name, Vector3 bottom, Vector3 top, float width, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            Vector3 dir = top - bottom;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            go.transform.localRotation = rot;
            go.transform.localPosition = (bottom + top) / 2f - rot * Vector3.up * 0.15f;
            go.transform.localScale = new Vector3(width, 0.3f, dir.magnitude);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static GameObject Shape(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, bool collide, Quaternion rot)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            return go;
        }

        private static Material PMat(string name, Color color, float tiling, Color emission = default(Color))
        {
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/PSX_" + name + ".mat";
            var shader = Shader.Find("WizardsVRC/PSX") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = color;
            mat.mainTexture = NoiseTexture();
            mat.mainTextureScale = new Vector2(tiling, tiling);
            mat.SetColor("_EmissionColor", emission);
            mat.SetFloat("_SnapRes", PsxShake);
            mat.SetFloat("_Affine", PsxAffine);
            if (emission.maxColorComponent > 0f) mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 16x16 point-filtered speckle texture shared by every PSX material (chunky "pixel" grain).
        private static Texture2D NoiseTexture()
        {
            string path = MaterialsDir + "/PSX_Noise.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
            tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var rng = new System.Random(3);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float v = 0.8f + (float)rng.NextDouble() * 0.2f;
                    if ((x + y * 3) % 7 == 0) v -= 0.08f;
                    tex.SetPixel(x, y, new Color(v, v, v, 1));
                }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        // ------------------------------------------------------------ low-poly meshes (flat shaded)

        private static Mesh SaveMesh(string name, List<Vector3> tris, Vector3 center)
        {
            Directory.CreateDirectory(MeshesDir);
            string path = MeshesDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var idx = new List<int>();
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f) { var t = b; b = c; c = t; }
                n = n.normalized;
                foreach (var v in new[] { a, b, c })
                {
                    idx.Add(verts.Count);
                    verts.Add(v);
                    uvs.Add(Mathf.Abs(n.y) > 0.7f ? new Vector2(v.x, v.z) : new Vector2(v.x + v.z, v.y));
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void Quad(List<Vector3> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }

        private static Vector3 Ring(int i, int sides, float r, float y)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        // radius 0.5, y -0.5..0.5, 7 sides x 4 bands
        private static Mesh BlobMesh()
        {
            var t = new List<Vector3>();
            const int sides = 7, bands = 4;
            for (int b = 0; b < bands; b++)
            {
                float p0 = Mathf.PI * b / bands, p1 = Mathf.PI * (b + 1) / bands;
                for (int s = 0; s < sides; s++)
                {
                    Vector3 a = Ring(s, sides, Mathf.Sin(p0) * 0.5f, Mathf.Cos(p0) * 0.5f);
                    Vector3 c = Ring(s + 1, sides, Mathf.Sin(p0) * 0.5f, Mathf.Cos(p0) * 0.5f);
                    Vector3 d = Ring(s, sides, Mathf.Sin(p1) * 0.5f, Mathf.Cos(p1) * 0.5f);
                    Vector3 e = Ring(s + 1, sides, Mathf.Sin(p1) * 0.5f, Mathf.Cos(p1) * 0.5f);
                    if (b == 0) { t.Add(a); t.Add(d); t.Add(e); }
                    else if (b == bands - 1) { t.Add(a); t.Add(c); t.Add(d); }
                    else Quad(t, a, c, e, d);
                }
            }
            return SaveMesh("PSX_Blob", t, Vector3.zero);
        }

        // radius 0.5, y -1..1 (same proportions as Unity's cylinder), 8 sides
        private static Mesh CylMesh()
        {
            var t = new List<Vector3>();
            const int sides = 8;
            for (int s = 0; s < sides; s++)
            {
                Vector3 a = Ring(s, sides, 0.5f, -1), b = Ring(s + 1, sides, 0.5f, -1);
                Vector3 c = Ring(s + 1, sides, 0.5f, 1), d = Ring(s, sides, 0.5f, 1);
                Quad(t, a, b, c, d);
                t.Add(new Vector3(0, 1, 0)); t.Add(d); t.Add(c);
                t.Add(new Vector3(0, -1, 0)); t.Add(a); t.Add(b);
            }
            return SaveMesh("PSX_Cylinder", t, Vector3.zero);
        }

        // radius 0.5, base y -0.5, apex y 0.5, 8 sides
        private static Mesh ConeMesh()
        {
            var t = new List<Vector3>();
            const int sides = 8;
            for (int s = 0; s < sides; s++)
            {
                Vector3 a = Ring(s, sides, 0.5f, -0.5f), b = Ring(s + 1, sides, 0.5f, -0.5f);
                t.Add(new Vector3(0, 0.5f, 0)); t.Add(a); t.Add(b);
                t.Add(new Vector3(0, -0.5f, 0)); t.Add(a); t.Add(b);
            }
            return SaveMesh("PSX_Cone", t, new Vector3(0, -0.25f, 0));
        }

        // double pyramid, radius 0.5, y -0.5..0.5, 6 sides
        private static Mesh GemMesh()
        {
            var t = new List<Vector3>();
            const int sides = 6;
            for (int s = 0; s < sides; s++)
            {
                Vector3 a = Ring(s, sides, 0.5f, 0), b = Ring(s + 1, sides, 0.5f, 0);
                t.Add(new Vector3(0, 0.5f, 0)); t.Add(a); t.Add(b);
                t.Add(new Vector3(0, -0.5f, 0)); t.Add(a); t.Add(b);
            }
            return SaveMesh("PSX_Gem", t, Vector3.zero);
        }
    }
}
