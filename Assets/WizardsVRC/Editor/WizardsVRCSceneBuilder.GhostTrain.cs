using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Midnight Express (Lonesome Bend's secret boss, see WizardsVRCSceneBuilder.Secret.cs): the station bell you ring
    /// to call it, and its arena, a duel ring on an open flatbed car between a ghostly engine and a lit passenger car, the
    /// desert streaming past below (scenery on looping native animation clips, so it costs nothing in Udon).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>A station bell on a timber frame (dock frame: z = out, away from the road). Returns the bell (it has the click box).</summary>
        private static Transform StationBell(Transform dock)
        {
            var timber = TimberMat();
            var brass = PMat("Brass", new Color(0.85f, 0.65f, 0.25f), 1f, new Color(0.2f, 0.14f, 0.02f));
            var iron = PMat("StoveIron", new Color(0.14f, 0.13f, 0.14f), 1f);
            foreach (float s in new[] { -1f, 1f })
                Block(dock, "BellPost", new Vector3(s * 1.4f, 2.6f, 0.8f), new Vector3(0.35f, 5.2f, 0.35f), timber, Quaternion.identity);
            Block(dock, "BellBeam", new Vector3(0, 5.3f, 0.8f), new Vector3(3.4f, 0.4f, 0.45f), timber, Quaternion.identity);
            Block(dock, "BellRoof", new Vector3(0, 5.8f, 0.8f), new Vector3(3.8f, 0.15f, 1.4f), timber, Quaternion.Euler(0, 0, 0));
            var bell = Child(dock, "StationBell", new Vector3(0, 4.3f, 0.8f));
            Shape(bell, "BellBody", ConeMesh(), new Vector3(0, -0.2f, 0), new Vector3(1.1f, 1.2f, 1.1f), brass, false, Quaternion.identity);
            Shape(bell, "BellCrown", BlobMesh(), new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.4f, 0.6f), brass, false, Quaternion.identity);
            Shape(bell, "BellClapper", BlobMesh(), new Vector3(0, -0.85f, 0), Vector3.one * 0.25f, iron, false, Quaternion.identity);
            // The rope down to hand height.
            Block(dock, "BellRope", new Vector3(0.3f, 2.4f, 0.8f), new Vector3(0.06f, 3f, 0.06f), PMat("Rope", new Color(0.7f, 0.6f, 0.42f), 1f), Quaternion.identity);
            var hit = bell.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, -1.5f, 0);
            hit.size = new Vector3(2.2f, 4f, 1.6f);
            return bell;
        }

        /// <summary>
        /// The ghost train arena: an open flatbed car (its deck at 1.2 m, the duel ring in the middle) with rails round it,
        /// the engine ahead and a passenger car behind, all pale and glowing, and the dusk desert rushing past below.
        /// Returns where you arrive (the back of the flatbed, facing the ring), like the sunken platform.
        /// </summary>
        private static Transform GhostTrainArena(Transform root)
        {
            var planks = PsxTexMat("GhostPlanks", new Color(0.42f, 0.5f, 0.52f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var ghostWood = PsxTexMat("GhostWood", new Color(0.35f, 0.5f, 0.5f), TileTexture(StreetTexDir + "/Timber.png"), 1.4f);
            var ghostIron = PMat("GhostIron", new Color(0.22f, 0.36f, 0.36f), 1f, new Color(0.05f, 0.14f, 0.13f));
            var glow = PMat("GhostLamp", new Color(0.7f, 1f, 0.95f), 1f, new Color(0.5f, 1f, 0.9f));
            var trim = PMat("GhostTrim", new Color(0.6f, 1f, 0.9f), 1f, new Color(0.3f, 0.7f, 0.6f));
            var sand = PsxTexMat("DuskSand", new Color(0.46f, 0.36f, 0.42f), TileTexture(TerrainTexDir + "/DesertSand.png"), 6f);
            var rock = PsxTexMat("DuskRock", new Color(0.5f, 0.32f, 0.36f), TileTexture(TerrainTexDir + "/RedRock.png"), 4f);
            var cactus = PsxTexMat("DuskCactus", new Color(0.22f, 0.32f, 0.3f), TileTexture(StreetTexDir + "/Cactus.png"), 1.4f);
            const float W = 26f, D = 38f, deck = 1.2f;

            // ---- the flatbed: deck, rails round it (solid, nobody falls off), bogies under it
            Block(root, "Flatbed", new Vector3(0, deck / 2f, 0), new Vector3(W, deck, D), planks, Quaternion.identity);
            foreach (float s in new[] { -1f, 1f })
            {
                Block(root, "FlatbedRail", new Vector3(s * (W / 2f - 0.15f), deck + 0.6f, 0), new Vector3(0.3f, 1.2f, D), ghostWood, Quaternion.identity);
                Block(root, "FlatbedEnd", new Vector3(0, deck + 0.6f, s * (D / 2f - 0.15f)), new Vector3(W, 1.2f, 0.3f), ghostWood, Quaternion.identity);
                for (float zz = -D / 2f + 2f; zz < D / 2f - 1f; zz += 4f)
                    Block(root, "RailPost", new Vector3(s * (W / 2f - 0.15f), deck + 0.9f, zz), new Vector3(0.45f, 1.8f, 0.45f), ghostWood, Quaternion.identity);
                foreach (float t in new[] { -1f, 1f })
                    for (int k = 0; k < 2; k++)
                        Shape(root, "Wheel", CylMesh(), new Vector3(s * (W / 2f - 3f), -1.4f, t * (D / 2f - 4f) + (k - 0.5f) * 2.6f), new Vector3(2.2f, 0.25f, 2.2f), ghostIron, false, Quaternion.Euler(0, 0, 90f));
            }
            // Lanterns on the rail posts at the corners.
            foreach (float s in new[] { -1f, 1f })
                foreach (float t in new[] { -1f, 1f })
                {
                    Shape(root, "CornerLamp", GemMesh(), new Vector3(s * (W / 2f - 0.15f), deck + 2.2f, t * (D / 2f - 2f)), new Vector3(0.45f, 0.65f, 0.45f), glow, false, Quaternion.identity);
                    PointLight(root, new Vector3(s * (W / 2f - 1.5f), deck + 3f, t * (D / 2f - 3f)), new Color(0.55f, 1f, 0.9f), 16f, 1.2f);
                }

            // ---- the passenger car behind and the engine ahead (out of reach past the rails)
            Vector3 car = new Vector3(0, 0, -D / 2f - 14f);
            Block(root, "PassengerCar", car + new Vector3(0, deck + 3f, 0), new Vector3(9f, 6f, 24f), ghostWood, Quaternion.identity);
            Block(root, "PassengerRoof", car + new Vector3(0, deck + 6.3f, 0), new Vector3(10f, 0.6f, 25f), ghostIron, Quaternion.identity);
            foreach (float s in new[] { -1f, 1f })
                for (int k = 0; k < 6; k++)
                    Deco(root, PrimitiveType.Cube, "CarWindow", car + new Vector3(s * 4.52f, deck + 3.6f, -10f + k * 4f), new Vector3(0.05f, 1.6f, 2.2f), glow, Quaternion.identity, false);
            Deco(root, PrimitiveType.Cube, "CarDoor", car + new Vector3(0, deck + 2.4f, 12.02f), new Vector3(1.8f, 3.6f, 0.05f), glow, Quaternion.identity, false);
            var engine = LocomotiveShape(root, Quaternion.identity, ghostIron, trim, glow, 7.5f);
            engine.transform.localPosition = new Vector3(0, -0.4f, D / 2f + 14f);
            engine.transform.localScale = Vector3.one * 2.4f;
            Motes(root, "EngineSteam", new Vector3(0, 10f, D / 2f + 19f), new Vector3(2f, 1f, 2f), new Color(0.75f, 1f, 0.95f), 14f, 2f, 4f,
                new Vector2(1.2f, 3f), new Vector3(0, 2.5f, -6f), new Vector3(0.6f, 0.6f, 0.6f), 0f);
            foreach (float zz in new[] { -D / 2f - 1.2f, D / 2f + 1.2f })
                Block(root, "Coupling", new Vector3(0, deck - 0.3f, zz), new Vector3(1.2f, 0.6f, 2.4f), ghostIron, Quaternion.identity);

            // ---- below: the track and the dusk desert, with scenery streaming past
            const float floorY = -3f;
            Block(root, "DesertBelow", new Vector3(0, floorY - 0.5f, 0), new Vector3(420f, 1f, 420f), sand, Quaternion.identity);
            Block(root, "TrackBed", new Vector3(0, floorY + 0.1f, 0), new Vector3(6f, 0.2f, 420f), PsxTexMat("RailTieBed", new Color(0.4f, 0.36f, 0.4f),
                TileTexture(TerrainTexDir + "/RailTies.png"), 1f), Quaternion.identity);
            const float span = 300f, seconds = 16f;
            for (int copy = 0; copy < 2; copy++)
            {
                var strip = Child(root, "Scenery" + copy, new Vector3(0, floorY, copy * span));
                var rng = new System.Random(4401);   // both copies the same, so the loop is seamless
                float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                for (int k = 0; k < 46; k++)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    Vector3 at = new Vector3(side * Rr(24f, 150f), 0, Rr(-span / 2f, span / 2f));
                    double roll = rng.NextDouble();
                    if (roll < 0.45) Saguaro(strip, at, Rr(1.2f, 2.2f), (float)rng.NextDouble(), cactus, false);
                    else if (roll < 0.75) Butte(strip, at, Rr(5f, 12f), Rr(4f, 12f), rock, sand, Rr(0f, 360f));
                    else Deco(strip, PrimitiveType.Sphere, "Boulder", at + new Vector3(0, 1f, 0), new Vector3(Rr(2f, 4f), Rr(1.5f, 3f), Rr(2f, 4f)), rock, Quaternion.Euler(0, Rr(0, 360f), 0), false);
                }
                // Telegraph poles beside the track, every 30 m.
                for (float zz = -span / 2f; zz < span / 2f; zz += 30f)
                {
                    Deco(strip, PrimitiveType.Cylinder, "TelegraphPole", new Vector3(18f, 5f, zz), new Vector3(0.35f, 5f, 0.35f), ghostWood, Quaternion.identity, false);
                    Deco(strip, PrimitiveType.Cube, "TelegraphArm", new Vector3(18f, 9.4f, zz), new Vector3(2.4f, 0.2f, 0.2f), ghostWood, Quaternion.identity, false);
                }
                foreach (var t in strip.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
                var anim = strip.gameObject.AddComponent<Animation>();
                var clip = ScrollClip("GhostTrainScenery" + copy, copy * span, span, seconds);
                anim.clip = clip;
                anim.AddClip(clip, clip.name);
                anim.playAutomatically = true;
                anim.wrapMode = WrapMode.Loop;
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
            }
            Motes(root, "BlowingDust", new Vector3(0, 2f, 20f), new Vector3(60f, 3f, 10f), new Color(0.8f, 0.7f, 0.75f), 18f, 2f, 3f,
                new Vector2(0.2f, 0.6f), new Vector3(0, 0.2f, -18f), new Vector3(1f, 0.4f, 1f), 0f);
            PointLight(root, new Vector3(0, 14f, 0), new Color(0.6f, 0.55f, 0.85f), 60f, 1.3f);

            Transform arrival = Child(root, "TrainArrival", new Vector3(0, deck + 0.1f, -13f));
            arrival.localRotation = Quaternion.identity;
            return arrival;
        }

        /// <summary>A looping clip sliding its object back along z by 'span' metres over 'seconds' (saved under the float clips).</summary>
        private static AnimationClip ScrollClip(string name, float z0, float span, float seconds)
        {
            Directory.CreateDirectory(FloatAnimDir);
            string path = FloatAnimDir + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool fresh = clip == null;
            if (fresh) clip = new AnimationClip();
            clip.ClearCurves();
            clip.name = name;
            clip.legacy = true;
            clip.wrapMode = WrapMode.Loop;
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, seconds, 0f));
            clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, seconds, -3f));
            clip.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0f, z0, seconds, z0 - span));
            if (fresh) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }
    }
}
