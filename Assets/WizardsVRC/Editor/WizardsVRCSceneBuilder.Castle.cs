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
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 170f;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.6f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.65f, 0.52f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.28f, 0.3f);
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------ castle

        private static void BuildCastle(Transform hub)
        {
            _rng = new System.Random(7);
            var root = new GameObject("Castle").transform;
            root.SetParent(hub, false);

            var grass = PMat("Grass", new Color(0.36f, 0.78f, 0.26f), 24f);
            var grassDark = PMat("GrassDark", new Color(0.22f, 0.58f, 0.24f), 8f);
            var flag = PMat("Flagstone", new Color(0.78f, 0.7f, 0.56f), 16f);
            var stone = PMat("Stone", new Color(0.64f, 0.62f, 0.74f), 4f);
            var stoneDark = PMat("StoneDark", new Color(0.48f, 0.44f, 0.6f), 4f);
            var roofRed = PMat("RoofRed", new Color(0.85f, 0.22f, 0.28f), 2f);
            var roofPurple = PMat("RoofPurple", new Color(0.52f, 0.26f, 0.8f), 2f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var banner = PMat("Banner", new Color(0.75f, 0.12f, 0.35f), 1f);
            var door = PMat("Door", new Color(0.35f, 0.2f, 0.12f), 2f);
            var window = PMat("Window", new Color(1f, 0.85f, 0.4f), 1f, new Color(0.9f, 0.7f, 0.25f));
            var dirt = PMat("Dirt", new Color(0.55f, 0.36f, 0.22f), 3f);

            // ground, hills, courtyard
            Block(root, "Ground", new Vector3(0, -0.5f, 4), new Vector3(170, 1f, 170), grass);
            Block(root, "Courtyard", new Vector3(0, 0.02f, 4), new Vector3(44, 0.1f, 44), flag);
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f + R(-0.15f, 0.15f);
                float d = R(48f, 75f);
                float rx = R(18f, 32f), ry = R(6f, 15f), rz = R(18f, 32f);
                Shape(root, "Hill" + i, BlobMesh(), new Vector3(Mathf.Cos(a) * d, -ry * 0.25f, 4 + Mathf.Sin(a) * d),
                    new Vector3(rx, ry, rz), i % 2 == 0 ? grass : grassDark, true, Quaternion.Euler(0, R(0, 360), 0));
            }

            // curtain walls (inner courtyard x -22..22, z -18..26), gate gap in the south wall
            Block(root, "WallWest", new Vector3(-23, 3, 4.5f), new Vector3(2, 6, 47), stone);
            Block(root, "WallEast", new Vector3(23, 3, 4.5f), new Vector3(2, 6, 47), stone);
            Block(root, "WallNorth", new Vector3(0, 3, 27), new Vector3(48, 6, 2), stone);
            Block(root, "WallSouthW", new Vector3(-13.5f, 3, -19), new Vector3(21, 6, 2), stone);
            Block(root, "WallSouthE", new Vector3(13.5f, 3, -19), new Vector3(21, 6, 2), stone);
            Block(root, "GateLintel", new Vector3(0, 5, -19), new Vector3(6, 2, 2), stoneDark);
            for (float z = -17f; z <= 26f; z += 2.5f)
            {
                Block(root, "Merlon", new Vector3(-23.7f, 6.5f, z), new Vector3(0.6f, 1, 1), stoneDark);
                Block(root, "Merlon", new Vector3(23.7f, 6.5f, z), new Vector3(0.6f, 1, 1), stoneDark);
            }
            for (float x = -21.5f; x <= 21.5f; x += 2.5f)
            {
                Block(root, "Merlon", new Vector3(x, 6.5f, 27.7f), new Vector3(1, 1, 0.6f), stoneDark);
                if (Mathf.Abs(x) > 3f) Block(root, "Merlon", new Vector3(x, 6.5f, -19.7f), new Vector3(1, 1, 0.6f), stoneDark);
            }

            // corner towers + gatehouse towers
            foreach (var c in new[] { new Vector3(-23, 0, -19), new Vector3(23, 0, -19), new Vector3(-23, 0, 27), new Vector3(23, 0, 27) })
                Tower(root, c, 3.5f, 12f, 8f, roofRed, stone, gold);
            Tower(root, new Vector3(-4.5f, 0, -19.5f), 2f, 10f, 6f, roofPurple, stone, gold);
            Tower(root, new Vector3(4.5f, 0, -19.5f), 2f, 10f, 6f, roofPurple, stone, gold);
            Block(root, "BannerGateW", new Vector3(-8, 3.5f, -20.1f), new Vector3(1.4f, 3.5f, 0.1f), banner);
            Block(root, "BannerGateE", new Vector3(8, 3.5f, -20.1f), new Vector3(1.4f, 3.5f, 0.1f), banner);

            // ramps up to the ramparts (y = 6)
            Ramp(root, "RampWest", new Vector3(-20.5f, 0, -12), new Vector3(-20.5f, 6, 8), 3f, stoneDark);
            Ramp(root, "RampEast", new Vector3(20.5f, 0, -12), new Vector3(20.5f, 6, 8), 3f, stoneDark);
            Block(root, "RampWestLanding", new Vector3(-20.5f, 5.85f, 9.5f), new Vector3(3, 0.3f, 3), stoneDark);
            Block(root, "RampEastLanding", new Vector3(20.5f, 5.85f, 9.5f), new Vector3(3, 0.3f, 3), stoneDark);

            // keep (x -8..8, z 15..26, roof y = 14)
            Block(root, "Keep", new Vector3(0, 7, 20.5f), new Vector3(16, 14, 11), stoneDark);
            Block(root, "KeepDoor", new Vector3(0, 2, 14.95f), new Vector3(3, 4, 0.1f), door);
            for (int i = 0; i < 4; i++)
                Block(root, "KeepWindow", new Vector3(-6 + i * 4, 9.5f, 14.95f), new Vector3(1, 1.8f, 0.1f), window);
            Block(root, "KeepBannerW", new Vector3(-5, 4, 14.9f), new Vector3(1.6f, 4f, 0.1f), banner);
            Block(root, "KeepBannerE", new Vector3(5, 4, 14.9f), new Vector3(1.6f, 4f, 0.1f), banner);

            // balcony at rampart height + bridges to both walls, railing on the courtyard side
            Block(root, "KeepBalcony", new Vector3(0, 5.85f, 12.5f), new Vector3(16, 0.3f, 5), stone);
            Block(root, "BridgeWest", new Vector3(-15, 5.85f, 11.25f), new Vector3(14, 0.3f, 2.5f), stone);
            Block(root, "BridgeEast", new Vector3(15, 5.85f, 11.25f), new Vector3(14, 0.3f, 2.5f), stone);
            Block(root, "BalconyRail", new Vector3(0, 6.3f, 10.1f), new Vector3(44, 0.6f, 0.2f), stoneDark);
            for (float x = -20f; x <= 20f; x += 4f)
                Block(root, "BalconyPillar", new Vector3(x, 2.85f, 10.4f), new Vector3(0.6f, 5.7f, 0.6f), stone);

            // ramp up the keep face to the roof terrace
            Ramp(root, "RampKeep", new Vector3(-6, 6, 13.5f), new Vector3(7, 14, 13.5f), 2.6f, stone);
            Block(root, "RampKeepLanding", new Vector3(7, 13.85f, 14), new Vector3(3, 0.3f, 4), stone);
            for (float x = -7.5f; x <= 7.5f; x += 2.5f)
                Block(root, "KeepMerlon", new Vector3(x, 14.5f, 25.7f), new Vector3(1, 1, 0.6f), stone);
            for (float z = 16.5f; z <= 24.5f; z += 2.5f)
                Block(root, "KeepMerlon", new Vector3(-7.7f, 14.5f, z), new Vector3(0.6f, 1, 1), stone);

            // tall central spire with a spiral ramp to a high balcony (y = 22)
            Vector3 spire = new Vector3(0, 0, 21);
            Shape(root, "Spire", CylMesh(), spire + new Vector3(0, 22, 0), new Vector3(5, 8, 5), stone, true, Quaternion.identity);
            Shape(root, "SpireRoof", ConeMesh(), spire + new Vector3(0, 34.5f, 0), new Vector3(6.5f, 9, 6.5f), roofPurple, true, Quaternion.identity);
            Shape(root, "SpireFinial", GemMesh(), spire + new Vector3(0, 40, 0), Vector3.one * 1.2f, gold, false, Quaternion.identity);
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * Mathf.PI * 2f, a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
                Vector3 p0 = spire + new Vector3(Mathf.Cos(a0) * 4.1f, 14 + i * 0.5f, Mathf.Sin(a0) * 4.1f);
                Vector3 p1 = spire + new Vector3(Mathf.Cos(a1) * 4.1f, 14 + (i + 1) * 0.5f, Mathf.Sin(a1) * 4.1f);
                Ramp(root, "SpiralStep" + i, p0, p1 + (p1 - p0).normalized * 0.15f, 1.7f, stoneDark);
            }
            Block(root, "HighBalcony", spire + new Vector3(6, 21.85f, 0), new Vector3(4, 0.3f, 5), stone);
            Block(root, "HighBalconyRail", spire + new Vector3(7.9f, 22.4f, 0), new Vector3(0.2f, 0.8f, 5), gold);
            Crystal(root, spire + new Vector3(6.8f, 23f, 1.6f), 0.8f, new Color(0.3f, 0.9f, 1f));
            Planter(root, spire + new Vector3(6.8f, 22, -1.6f), dirt);

            // floating isles off the keep roof (jump across)
            Isle(root, new Vector3(10.5f, 14, 22), 3f, grass, dirt);
            Isle(root, new Vector3(14f, 14, 19.5f), 2.6f, grass, dirt);
            Isle(root, new Vector3(17.2f, 14, 16.5f), 2.6f, grass, dirt);
            Isle(root, new Vector3(19.5f, 14, 12f), 5f, grass, dirt);
            Crystal(root, new Vector3(20.5f, 15f, 11f), 1f, new Color(1f, 0.35f, 0.8f));
            Tree(root, new Vector3(18.5f, 14, 13f), 0.7f);

            // courtyard gardens (centre x -12..12, z -8..13 is kept clear for spawn / pads / NPC)
            foreach (var p in new[] { new Vector3(-15, 0, -14), new Vector3(15, 0, -14), new Vector3(-14, 0, 20), new Vector3(14, 0, 20), new Vector3(-12, 0, 24), new Vector3(12, 0, 24) })
                Tree(root, p, R(0.9f, 1.2f));
            foreach (var p in new[] { new Vector3(-13, 0, -3), new Vector3(13, 0, -3), new Vector3(-13, 0, 6), new Vector3(13, 0, 6) })
                FlowerBed(root, p, new Vector2(3, 5), dirt);
            Mushroom(root, new Vector3(-16.5f, 0, 1), 1.5f);
            Mushroom(root, new Vector3(16.5f, 0, 1), 1.8f);
            Mushroom(root, new Vector3(-9.5f, 0, -15.5f), 0.6f);
            Mushroom(root, new Vector3(-8.3f, 0, -16.3f), 0.4f);
            Mushroom(root, new Vector3(9f, 0, -15.8f), 0.5f);
            for (float x = -18f; x <= 18f; x += 2.2f)
                if (Mathf.Abs(x) > 5f) Bush(root, new Vector3(x + R(-0.4f, 0.4f), 0, -16.8f), R(0.8f, 1.2f));
            for (float x = -20f; x <= 20f; x += 2.5f)
                if (Mathf.Abs(x) > 9f) Bush(root, new Vector3(x, 0, 25), R(0.9f, 1.3f));
            Crystal(root, new Vector3(-3.5f, 0.6f, -16), 0.6f, new Color(0.4f, 1f, 0.5f));
            Crystal(root, new Vector3(3.5f, 0.6f, -16), 0.6f, new Color(1f, 0.85f, 0.3f));

            // vines hanging from walls and the keep
            for (float x = -20f; x <= 20f; x += 3.1f)
                if (Mathf.Abs(x) > 4f) Vine(root, new Vector3(x, 6, -17.95f), R(2.5f, 5f), 0);
            for (float z = 12f; z <= 25f; z += 3.3f) { Vine(root, new Vector3(-21.95f, 6, z), R(2.5f, 5f), 90); Vine(root, new Vector3(21.95f, 6, z), R(2.5f, 5f), 90); }
            for (float x = -7f; x <= 7f; x += 2.3f) Vine(root, new Vector3(x, 14, 14.93f), R(3f, 6f), 0);
            for (float x = -22f; x <= 22f; x += 3.7f) Vine(root, new Vector3(x, 6, -20.05f), R(2f, 4.5f), 0);

            // planters on the upper levels
            foreach (var p in new[] { new Vector3(-6.5f, 14, 17), new Vector3(-6.5f, 14, 24.5f), new Vector3(6.5f, 14, 24.5f), new Vector3(-7, 6, 14.2f), new Vector3(-21f, 6, 11.25f), new Vector3(21f, 6, 11.25f) })
                Planter(root, p, dirt);

            // trees and mushrooms on the hills outside the walls
            int placed = 0;
            for (int tries = 0; placed < 34 && tries < 400; tries++)
            {
                var p = new Vector3(R(-80f, 80f), 0, 4 + R(-80f, 80f));
                if (Mathf.Abs(p.x) < 30f && p.z > -27f && p.z < 35f) continue;
                if (placed % 6 == 5) Mushroom(root, p, R(1f, 2.2f)); else Tree(root, p, R(1f, 1.7f));
                placed++;
            }
            for (int i = 0; i < 6; i++) FlowerBed(root, new Vector3(R(-12f, 12f), 0, R(-34f, -24f)), new Vector2(2.5f, 2.5f), dirt);
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
