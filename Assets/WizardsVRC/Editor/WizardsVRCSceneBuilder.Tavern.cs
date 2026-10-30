using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The round tavern (Tavern landmark): a cylindrical hall filling the circle inside its block, with a conical roof
    /// open over the middle (the Starfall fountain stands there in the hub). Inside, a hang-out space: a curved bar with
    /// stools, shelves, kegs and a bell that brings the mugs back, round tables with stools, a stage with curtains, lights
    /// and a jukebox (video player heard only inside, WizardsVRCSceneBuilder.Jukebox.cs), a hearth with log seats, a mirror you switch on yourself, hanging lanterns. Every seat is a
    /// VRCStation you can sit in; the mugs are pickups synced for everyone.
    /// Angles below: 0 = east, 90 = north (doors at 90, 180 and 270; the bar faces them from the east).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float TavernWallH = 9f, TavernRoofRise = 9f, TavernDoorH = 5.2f;
        // The plank floor sits 12 cm over the paving under it (well clear, so the two never flicker) and is walked on.
        private const float TavernFloorY = 0.14f;
        private static readonly float[] TavernDoors = { 90f, 180f, 270f };

        private static Vector3 TDir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
        private static Vector3 TPolar(Vector3 c, float r, float deg, float y) { Vector3 d = TDir(deg); return new Vector3(c.x + d.x * r, c.y + y, c.z + d.z * r); }
        /// <summary>Rotation whose +Z points outwards at that angle (a wall piece's thickness runs along the radius).</summary>
        private static Quaternion TFacing(float deg) { return Quaternion.LookRotation(TDir(deg)); }
        private static float AngleGap(float a, float b) { return Mathf.Abs(Mathf.DeltaAngle(a, b)); }

        /// <summary>
        /// If 'cell' lies inside a tavern's round hall, the spot behind the bar for that NPC (on the tavern's floor) and the
        /// way they face (into the room). The bar is on the east side, 13.5 m out (see TavernLandmark). Only cells within
        /// the hall's wall count: the corners of the tavern's square block are outside, so NPCs painted there stay put.
        /// When several NPCs are painted inside, the first stands at the bar's middle and the rest spread along it
        /// (12 degrees apart, alternating sides) so nobody stands inside anyone else.
        /// </summary>
        private static bool TavernBarkeepSpot(ZoneGrid g, ZoneDefinition z, Vector2Int cell, out Vector3 pos, out Vector3 front)
        {
            pos = front = Vector3.zero;
            if (z.landmarks == null) return false;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.Tavern) continue;
                var fp = g.Footprint(l.Symbol);
                if (fp.width == 0 || !fp.Contains(cell) || !InTavernHall(g, fp, cell)) continue;
                Vector3 c = RectCenter(g, fp);
                var cells = g.Find(l.Symbol);
                if (cells.Count > 0) c.y = g.Elevation(cells[0].x, cells[0].y);
                // This NPC's place in line among the zone's NPCs inside this hall.
                int slot = 0;
                if (z.npcs != null)
                    foreach (var other in z.npcs)
                    {
                        if (other == null) continue;
                        var oc = g.Find(other.Symbol);
                        if (oc.Count == 0) continue;
                        if (oc[0] == cell) break;
                        if (fp.Contains(oc[0]) && InTavernHall(g, fp, oc[0])) slot++;
                    }
                float a = slot == 0 ? 0f : ((slot + 1) / 2) * 12f * (slot % 2 == 1 ? 1f : -1f);
                a = Mathf.Clamp(a, -24f, 24f);
                pos = TPolar(c, 15.2f, a, 0f);
                front = -TDir(a);
                return true;
            }
            return false;
        }

        /// <summary>The tavern hall's wall radius for its block (the same as TavernLandmark builds).</summary>
        private static float TavernRadius(ZoneGrid g, RectInt fp)
        {
            return Mathf.Max(8f, Mathf.Min(fp.width, fp.height) * g.cell * 0.5f - 4f);
        }

        /// <summary>True when the cell's centre is inside the tavern's round wall (not just its square block).</summary>
        private static bool InTavernHall(ZoneGrid g, RectInt fp, Vector2Int cell)
        {
            Vector3 c = RectCenter(g, fp), p = g.Local(cell);
            float dx = p.x - c.x, dz = p.z - c.z;
            float r = TavernRadius(g, fp);
            return dx * dx + dz * dz < r * r;
        }

        private static void TavernLandmark(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark l, List<UdonSharpBehaviour> behaviours,
            Material stone, Material roofMat, Material gold, Material window, Material wood)
        {
            var fp = g.Footprint(l.Symbol);
            Vector3 c = RectCenter(g, fp);
            var tCells = g.Find(l.Symbol);
            if (tCells.Count > 0) c.y = g.Elevation(tCells[0].x, tCells[0].y);
            float R = TavernRadius(g, fp);
            string name = string.IsNullOrEmpty(l.label) ? "The Crystal Tap" : l.label;
            Transform t = Child(root, "Tavern_" + l.symbol, Vector3.zero);

            var plaster = PsxTexMat("TavernPlaster", new Color(0.93f, 0.86f, 0.72f), TileTexture(StreetTexDir + "/Plaster.png"), 4f);
            var beam = PsxTexMat("TimberBeam", new Color(0.3f, 0.19f, 0.12f), TileTexture(StreetTexDir + "/Bark.png"), 1.2f);
            var darkWood = PsxTexMat("TavernDarkWood", new Color(0.36f, 0.22f, 0.13f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 1.6f);
            var planks = PsxTexMat("TavernFloor", new Color(0.72f, 0.5f, 0.32f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var ceiling = PsxTexMat("TavernCeiling", new Color(0.55f, 0.37f, 0.22f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var roofTex = PsxTexMat("TavernRoof", Color.Lerp(roofMat.color, new Color(0.7f, 0.25f, 0.2f), 0.5f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.6f);
            var cloth = PMat("TavernCurtain", new Color(0.62f, 0.1f, 0.14f), 1f);
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));

            // ---- walls: 40 pieces round the circle, gaps for the doors with lintels over them, timber posts between
            const int segs = 40;
            float dA = 360f / segs;
            for (int i = 0; i < segs; i++)
            {
                float a = dA * (i + 0.5f);
                float chord = 2f * R * Mathf.Sin(dA * 0.5f * Mathf.Deg2Rad) + 0.12f;
                bool door = false;
                foreach (float d in TavernDoors) if (AngleGap(a, d) < dA) door = true;
                if (door)
                    Block(t, "Lintel", TPolar(c, R - 0.4f, a, (TavernDoorH + TavernWallH) / 2f), new Vector3(chord, TavernWallH - TavernDoorH, 0.8f), plaster, TFacing(a));
                else
                {
                    Block(t, "Wall", TPolar(c, R - 0.4f, a, TavernWallH / 2f), new Vector3(chord, TavernWallH, 0.8f), plaster, TFacing(a));
                    Block(t, "Plinth", TPolar(c, R + 0.1f, a, 0.4f), new Vector3(chord, 0.8f, 0.4f), stone, TFacing(a));
                    // Windows all round, except behind the bar's shelves, the hearth and the mirror.
                    if (i % 4 == 2 && AngleGap(a, 0f) > 40f && AngleGap(a, 225f) > 12f && AngleGap(a, 315f) > 12f)
                    {
                        Deco(t, PrimitiveType.Cube, "Window", TPolar(c, R + 0.03f, a, 5.4f), new Vector3(1.3f, 2.2f, 0.08f), window, TFacing(a), false);
                        Deco(t, PrimitiveType.Cube, "Window", TPolar(c, R - 0.83f, a, 5.4f), new Vector3(1.3f, 2.2f, 0.08f), window, TFacing(a), false);
                    }
                }
                Deco(t, PrimitiveType.Cube, "Cornice", TPolar(c, R - 0.2f, a, TavernWallH + 0.2f), new Vector3(chord + 0.1f, 0.45f, 1.6f), darkWood, TFacing(a), false);
                if (i % 2 == 0)
                    Deco(t, PrimitiveType.Cube, "Post", TPolar(c, R + 0.05f, a - dA * 0.5f, TavernWallH / 2f), new Vector3(0.35f, TavernWallH, 0.3f), beam, TFacing(a - dA * 0.5f), false);
            }
            foreach (float d in TavernDoors)
            {
                // Door frame, an arch beam, lanterns either side and the tavern's name outside.
                float half = dA;
                foreach (float s in new[] { -1f, 1f })
                {
                    Block(t, "DoorPost", TPolar(c, R - 0.4f, d + s * half, TavernDoorH / 2f), new Vector3(0.6f, TavernDoorH, 1f), darkWood, TFacing(d + s * half));
                    Shape(t, "DoorLantern", GemMesh(), TPolar(c, R + 0.5f, d + s * (half + 3f), 3.6f), new Vector3(0.45f, 0.7f, 0.45f), glow, false, Quaternion.identity);
                }
                Block(t, "DoorBeam", TPolar(c, R - 0.4f, d, TavernDoorH + 0.25f), new Vector3(2f * R * Mathf.Sin(half * Mathf.Deg2Rad) + 0.8f, 0.5f, 1.1f), gold, TFacing(d));
                LandmarkSign(t, TPolar(c, R + 0.5f, d, TavernDoorH + 1.4f), TDir(d), name, new Color(1f, 0.85f, 0.45f));
            }

            // ---- floor and the conical roof (open over the middle), outside tiles and a wooden ceiling inside
            var floor = new MeshBuilder();
            var roofOut = new MeshBuilder();
            var roofIn = new MeshBuilder();
            float r0 = R + 1.4f, r1 = 4.6f, yr0 = TavernWallH + 0.35f, yr1 = TavernWallH + TavernRoofRise;
            for (int i = 0; i < segs; i++)
            {
                float a0 = dA * i, a1 = dA * (i + 1);
                Vector3 f0 = TPolar(c, R - 0.8f, a0, TavernFloorY), f1 = TPolar(c, R - 0.8f, a1, TavernFloorY);
                floor.Tri(c + new Vector3(0, TavernFloorY, 0), f0, f1, Vector3.up);
                Vector3 p00 = TPolar(c, r0, a0, yr0), p01 = TPolar(c, r0, a1, yr0), p10 = TPolar(c, r1, a0, yr1), p11 = TPolar(c, r1, a1, yr1);
                Vector3 outward = (TDir((a0 + a1) / 2f) * TavernRoofRise + Vector3.up * (r0 - r1)).normalized;
                roofOut.Quad(p00, p01, p11, p10, outward);
                Vector3 dn = new Vector3(0, -0.3f, 0);
                roofIn.Quad(p00 + dn, p01 + dn, p11 + dn, p10 + dn, -outward);
            }
            TavernMesh(t, "TavernFloor", floor, planks, true);
            TavernMesh(t, "TavernRoof", roofOut, roofTex, true);
            TavernMesh(t, "TavernCeiling", roofIn, ceiling, false);
            for (int i = 0; i < 16; i++)
            {
                float a = 360f / 16 * (i + 0.5f);
                Deco(t, PrimitiveType.Cube, "OculusRim", TPolar(c, r1, a, yr1 + 0.2f), new Vector3(2f * r1 * Mathf.Sin(11.25f * Mathf.Deg2Rad) + 0.1f, 0.5f, 0.6f), gold, TFacing(a), false);
            }
            // Rafters under the roof.
            for (int i = 0; i < 8; i++)
            {
                float a = 45f * i + 22.5f;
                Vector3 lo = TPolar(c, R - 0.8f, a, yr0 - 0.6f), hi = TPolar(c, r1 + 0.3f, a, yr1 - 0.6f);
                var rafter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rafter.name = "Rafter";
                rafter.isStatic = true;
                Object.DestroyImmediate(rafter.GetComponent<Collider>());
                rafter.transform.SetParent(t, false);
                rafter.transform.localPosition = (lo + hi) / 2f;
                rafter.transform.localRotation = Quaternion.LookRotation(hi - lo);
                rafter.transform.localScale = new Vector3(0.35f, 0.45f, (hi - lo).magnitude);
                rafter.GetComponent<Renderer>().sharedMaterial = beam;
            }

            var mugs = new List<GameObject>();
            var pewter = PMat("Pewter", new Color(0.62f, 0.63f, 0.68f), 1f);
            var foam = PMat("MugFoam", new Color(0.98f, 0.95f, 0.85f), 1f);

            // ---- the bar (east): a curved counter with stools, taps, shelves of bottles, kegs and a bell
            const float barR = 13.5f;
            for (int k = 0; k < 8; k++)
            {
                float a = -28f + 8f * k;
                float chord = 2f * barR * Mathf.Sin(4f * Mathf.Deg2Rad) + 0.08f;
                Block(t, "BarCounter", TPolar(c, barR, a, 0.55f), new Vector3(chord, 1.1f, 0.8f), darkWood, TFacing(a));
                Block(t, "BarTop", TPolar(c, barR - 0.05f, a, 1.15f), new Vector3(chord + 0.05f, 0.1f, 1.05f), wood, TFacing(a));
                Deco(t, PrimitiveType.Cube, "BarRail", TPolar(c, barR - 0.5f, a, 0.25f), new Vector3(chord, 0.06f, 0.06f), gold, TFacing(a), false);
            }
            foreach (float s in new[] { -1f, 1f })
                Block(t, "BarEnd", TPolar(c, barR + 0.9f, 32f * s, 0.55f), new Vector3(0.8f, 1.1f, 2.6f), darkWood, TFacing(32f * s));
            for (int k = 0; k < 7; k++)
            {
                float a = -27f + 9f * k;
                MakeSeat(t, "BarStool", TPolar(c, barR - 1.3f, a, 0f), TDir(a), 0.78f, darkWood, gold, behaviours);
                mugs.Add(MakeMug(t, TPolar(c, barR - 0.1f, a + 4f, 1.21f), pewter, foam));
            }
            foreach (float a in new[] { -6f, 6f })
            {
                Shape(t, "Tap", CylMesh(), TPolar(c, barR + 0.15f, a, 1.45f), new Vector3(0.12f, 0.25f, 0.12f), gold, false, Quaternion.identity);
                Shape(t, "TapHandle", CylMesh(), TPolar(c, barR + 0.15f, a, 1.85f), new Vector3(0.08f, 0.18f, 0.08f), darkWood, false, Quaternion.identity);
            }
            var bottleMats = new[]
            {
                PMat("BottleGreen", new Color(0.2f, 0.6f, 0.3f), 1f, new Color(0.05f, 0.2f, 0.08f)), PMat("BottleAmber", new Color(0.85f, 0.5f, 0.15f), 1f, new Color(0.25f, 0.12f, 0.02f)),
                PMat("BottleBlue", new Color(0.3f, 0.45f, 0.95f), 1f, new Color(0.08f, 0.12f, 0.3f)), PMat("BottleRed", new Color(0.75f, 0.15f, 0.25f), 1f, new Color(0.2f, 0.03f, 0.06f)),
            };
            for (int u = 0; u < 5; u++)
            {
                float a = -24f + 12f * u;
                Block(t, "Shelves", TPolar(c, R - 1.15f, a, 1.7f), new Vector3(2.3f, 3.4f, 0.6f), darkWood, TFacing(a));
                for (int row = 0; row < 3; row++)
                    for (int b = 0; b < 5; b++)
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, TDir(a));
                        Vector3 p = TPolar(c, R - 1.55f, a, 0.75f + row * 1.05f) + side * (-0.8f + b * 0.4f);
                        Shape(t, "Bottle", CylMesh(), p + new Vector3(0, 0.22f, 0), new Vector3(0.16f, 0.22f, 0.16f), bottleMats[(u + row + b) % 4], false, Quaternion.identity);
                    }
            }
            foreach (float a in new[] { -31f, 31f })
                for (int k = 0; k < 3; k++)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, TDir(a));
                    Vector3 p = TPolar(c, 16.6f, a, k < 2 ? 0.55f : 1.6f) + side * (k == 0 ? -0.6f : k == 1 ? 0.6f : 0f);
                    Shape(t, "Keg", CylMesh(), p, new Vector3(1.05f, 0.65f, 1.05f), darkWood, k < 2, Quaternion.LookRotation(TDir(a)) * Quaternion.Euler(90f, 0, 0));
                }
            var signBoard = TPolar(c, barR + 2.4f, 0f, 4.4f);
            Block(t, "BarSignBoard", signBoard + TDir(0f) * 0.08f, new Vector3(5.2f, 1.1f, 0.12f), darkWood, TFacing(0f));
            LandmarkSign(t, signBoard, TDir(180f), name, new Color(1f, 0.85f, 0.45f));

            // ---- round tables with stools and candles, two mugs on each: an inner ring round the fountain and two further
            // out, clear of the door aisles, the bar stools, the hearth seats and the space in front of the mirror
            var tableSpots = new[] { new Vector2(10.5f, 48f), new Vector2(10.5f, 132f), new Vector2(10.5f, 222f), new Vector2(10.5f, 312f),
                                     new Vector2(15.8f, 58f), new Vector2(15.8f, 298f) };
            foreach (var spot in tableSpots)
            {
                float a = spot.y;
                Vector3 tp = TPolar(c, spot.x, a, 0f);
                Shape(t, "TableTop", CylMesh(), tp + new Vector3(0, 1f, 0), new Vector3(1.8f, 0.05f, 1.8f), wood, true, Quaternion.identity);
                Shape(t, "TableLeg", CylMesh(), tp + new Vector3(0, 0.5f, 0), new Vector3(0.25f, 0.5f, 0.25f), darkWood, true, Quaternion.identity);
                Shape(t, "TableFoot", CylMesh(), tp + new Vector3(0, 0.04f, 0), new Vector3(0.9f, 0.04f, 0.9f), darkWood, false, Quaternion.identity);
                Shape(t, "Candle", CylMesh(), tp + new Vector3(0, 1.17f, 0), new Vector3(0.1f, 0.12f, 0.1f), PMat("CandleWax", new Color(0.9f, 0.88f, 0.8f), 1f), false, Quaternion.identity);
                Shape(t, "CandleFlame", BlobMesh(), tp + new Vector3(0, 1.35f, 0), new Vector3(0.08f, 0.14f, 0.08f), flame, false, Quaternion.identity);
                for (int k = 0; k < 4; k++)
                {
                    float sa = a + 45f + 90f * k;
                    Vector3 sp = tp + TDir(sa) * 1.35f;
                    MakeSeat(t, "TableStool", sp, -TDir(sa), 0.62f, darkWood, gold, behaviours);
                }
                mugs.Add(MakeMug(t, tp + TDir(a + 45f) * 0.45f + new Vector3(0, 1.06f, 0), pewter, foam));
                mugs.Add(MakeMug(t, tp + TDir(a + 225f) * 0.45f + new Vector3(0, 1.06f, 0), pewter, foam));
            }

            // ---- the bell that brings every mug back
            var bellGo = Shape(t, "Bell", BlobMesh(), TPolar(c, barR - 0.1f, -31f, 1.42f), new Vector3(0.38f, 0.42f, 0.38f), gold, false, Quaternion.identity);
            bellGo.isStatic = false;
            bellGo.AddComponent<SphereCollider>().radius = 0.6f;
            var bellAudio = bellGo.AddComponent<AudioSource>();
            bellAudio.playOnAwake = false;
            bellAudio.spatialBlend = 1f;
            bellAudio.maxDistance = 25f;
            bellAudio.clip = UiClip("ui_health_powerup_01");
            var reset = UdonSharpUndo.AddComponent<MugReset>(bellGo);
            reset.mugs = mugs.ToArray();
            reset.bell = bellAudio;
            SetInteractText(reset, "Ring for fresh mugs");
            behaviours.Add(reset);

            // ---- stage (north-west): raised planks, a step, curtains, coloured lights, instruments and the jukebox
            var stage = new MeshBuilder();
            SectorPrism(stage, c, 12.6f, R - 0.85f, 104f, 166f, 0f, 0.45f, 12);
            SectorPrism(stage, c, 11.9f, 12.6f, 106f, 164f, 0f, 0.22f, 12);
            TavernMesh(t, "Stage", stage, planks, true);
            for (int k = 0; k < 7; k++)
            {
                float a = 108f + k * 8.5f;
                Deco(t, PrimitiveType.Cube, "Curtain", TPolar(c, R - 1.0f, a, 3.4f), new Vector3(2.2f * R / 20f, 5.8f, 0.18f), cloth, TFacing(a) * Quaternion.Euler(0, (k % 2 == 0 ? 6f : -6f), 0), false);
            }
            Deco(t, PrimitiveType.Cube, "CurtainRod", TPolar(c, R - 1.05f, 135f, 6.4f), new Vector3(2f * (R - 1f) * Mathf.Sin(30f * Mathf.Deg2Rad), 0.12f, 0.12f), gold, TFacing(135f), false);
            Shape(t, "Drum", CylMesh(), TPolar(c, 15.8f, 125f, 0.85f), new Vector3(0.9f, 0.4f, 0.9f), cloth, true, Quaternion.identity);
            Shape(t, "DrumHead", CylMesh(), TPolar(c, 15.8f, 125f, 1.26f), new Vector3(0.92f, 0.02f, 0.92f), foam, false, Quaternion.identity);
            Shape(t, "LuteBody", BlobMesh(), TPolar(c, 16.6f, 146f, 1.25f), new Vector3(0.6f, 0.75f, 0.25f), wood, false, TFacing(146f) * Quaternion.Euler(0, 0, 20f));
            Block(t, "LuteNeck", TPolar(c, 16.6f, 146f, 1.95f), new Vector3(0.08f, 0.9f, 0.08f), darkWood, TFacing(146f) * Quaternion.Euler(0, 0, 20f));
            var stageColours = new[] { new Color(1f, 0.35f, 0.75f), new Color(0.35f, 0.75f, 1f), new Color(1f, 0.8f, 0.35f) };
            for (int k = 0; k < 3; k++)
            {
                float a = 115f + k * 20f;
                PointLight(t, TPolar(c, 14.5f, a, 6f), stageColours[k], 12f, 1.6f);
                Shape(t, "StageLamp", GemMesh(), TPolar(c, 14.5f, a, 6.3f), new Vector3(0.45f, 0.6f, 0.45f),
                    PMat("StageLamp" + k, stageColours[k], 1f, stageColours[k] * 0.8f), false, Quaternion.identity);
            }
            // Music: the jukebox (a synced video player, heard only inside the tavern) with its screen over the
            // curtains and its controls on a lectern at the stage's west end.
            BuildJukebox(t, c, R, TavernWallH, TavernRoofRise, 135f, 168f, darkWood, darkWood, behaviours);

            // ---- hearth (south-west): a big fireplace with a chimney through the roof, a rug and log seats round it
            const float hearthA = 225f;
            Vector3 hearthOut = TDir(hearthA), hearthSide = Vector3.Cross(Vector3.up, hearthOut);
            Vector3 hb = TPolar(c, R - 1.6f, hearthA, 0f);
            foreach (float s in new[] { -1f, 1f })
                Block(t, "HearthPillar", hb + hearthSide * s * 1.75f + new Vector3(0, 1.8f, 0), new Vector3(0.9f, 3.6f, 1.6f), stone, TFacing(hearthA));
            Block(t, "HearthLintel", hb + new Vector3(0, 3.2f, 0), new Vector3(4.4f, 0.8f, 1.6f), stone, TFacing(hearthA));
            Block(t, "HearthBack", hb + hearthOut * 0.6f + new Vector3(0, 1.4f, 0), new Vector3(2.6f, 2.8f, 0.3f), PMat("Soot", new Color(0.12f, 0.1f, 0.1f), 1f), TFacing(hearthA));
            Deco(t, PrimitiveType.Cube, "Mantel", hb - hearthOut * 0.15f + new Vector3(0, 3.75f, 0), new Vector3(5f, 0.3f, 1.9f), darkWood, TFacing(hearthA), false);
            Block(t, "Chimney", TPolar(c, R - 1.4f, hearthA, (3.9f + yr1 - 2f) / 2f), new Vector3(2.2f, yr1 - 2f - 3.9f, 1.4f), stone, TFacing(hearthA));
            for (int k = 0; k < 3; k++)
            {
                Vector3 fp2 = hb + hearthSide * (k - 1) * 0.55f + new Vector3(0, 0.55f, 0);
                Shape(t, "Fire", ConeMesh(), fp2, new Vector3(0.7f, 1.1f - (k == 1 ? 0f : 0.3f), 0.7f), flame, false, Quaternion.identity);
            }
            Shape(t, "Log", CylMesh(), hb + new Vector3(0, 0.15f, 0), new Vector3(0.3f, 1f, 0.3f), darkWood, false, TFacing(hearthA) * Quaternion.Euler(0, 0, 90f));
            PointLight(t, hb - hearthOut * 1.5f + new Vector3(0, 1.4f, 0), new Color(1f, 0.55f, 0.2f), 14f, 2f);
            Shape(t, "HearthRug", CylMesh(), TPolar(c, R - 5.2f, hearthA, 0.12f), new Vector3(5.5f, 0.02f, 5.5f), cloth, false, Quaternion.identity);
            foreach (float a in new[] { 210f, 217.5f, 225f, 232.5f, 240f })
            {
                Vector3 sp = TPolar(c, R - 5.5f, a, 0f);
                Vector3 toFire = hb - sp;
                toFire.y = 0f;
                MakeSeat(t, "LogSeat", sp, toFire.normalized, 0.5f, darkWood, gold, behaviours, true);
            }

            // ---- mirror (south-east), off until you switch it on (mirrors are heavy; each player chooses)
            const float mirrorA = 315f;
            Block(t, "MirrorFrame", TPolar(c, R - 0.86f, mirrorA, 2.45f), new Vector3(4.6f, 3.6f, 0.12f), gold, TFacing(mirrorA));
            var mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = "Mirror";
            Object.DestroyImmediate(mirror.GetComponent<Collider>());
            mirror.transform.SetParent(t, false);
            mirror.transform.localPosition = TPolar(c, R - 0.94f, mirrorA, 2.45f);
            mirror.transform.localRotation = TFacing(mirrorA);
            mirror.transform.localScale = new Vector3(4.2f, 3.2f, 1f);
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
            else Debug.LogWarning("[WizardsVRC] Shader FX/MirrorReflection not found (VRChat SDK); the tavern mirror has no material.");
            mirror.AddComponent<VRCMirrorReflection>();
            mirror.SetActive(false);
            var mirrorButton = Shape(t, "MirrorToggle", GemMesh(), TPolar(c, R - 1f, mirrorA - 8.5f, 1.5f), new Vector3(0.35f, 0.5f, 0.35f),
                PMat("MirrorGem", new Color(0.6f, 0.9f, 1f), 1f, new Color(0.3f, 0.55f, 0.7f)), false, Quaternion.identity);
            mirrorButton.isStatic = false;
            mirrorButton.AddComponent<SphereCollider>().radius = 0.6f;
            WorldText(t, "MirrorLabel", TPolar(c, R - 1.05f, mirrorA - 8.5f, 2.1f), Quaternion.LookRotation(TDir(mirrorA - 8.5f)), new Vector2(500, 100), 0.004f, "Mirror", 60)
                .color = new Color(0.8f, 0.95f, 1f);
            var mirrorToggle = UdonSharpUndo.AddComponent<LocalToggle>(mirrorButton);
            mirrorToggle.targets = new[] { mirror };
            SetInteractText(mirrorToggle, "Mirror on/off (just for you)");
            behaviours.Add(mirrorToggle);

            // ---- hanging lanterns on chains from the roof, and warm light
            for (int k = 0; k < 12; k++)
            {
                float a = 15f + 30f * k;
                const float lr = 11.5f;
                float roofY = yr0 + (r0 - lr) / (r0 - r1) * TavernRoofRise - 0.35f;
                Deco(t, PrimitiveType.Cube, "Chain", TPolar(c, lr, a, (7.8f + roofY) / 2f), new Vector3(0.06f, roofY - 7.8f, 0.06f), beam, Quaternion.identity, false);
                Shape(t, "Lantern", GemMesh(), TPolar(c, lr, a, 7.3f), new Vector3(0.6f, 0.9f, 0.6f), glow, false, Quaternion.identity);
                Deco(t, PrimitiveType.Cube, "LanternCap", TPolar(c, lr, a, 7.85f), new Vector3(0.5f, 0.12f, 0.5f), beam, Quaternion.identity, false);
            }
            for (int k = 0; k < 4; k++) PointLight(t, TPolar(c, 11f, 45f + 90f * k, 6.5f), new Color(1f, 0.82f, 0.6f), 18f, 1.3f);
        }

        /// <summary>A seat you can sit in: a stool (or a log), a VRCStation facing 'facing', and a Seat behaviour (click to sit).</summary>
        private static void MakeSeat(Transform parent, string name, Vector3 floorPos, Vector3 facing, float height, Material wood, Material gold,
            List<UdonSharpBehaviour> behaviours, bool log = false)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = Child(parent, name, floorPos);
            go.localRotation = Quaternion.LookRotation(facing.normalized);
            if (log)
                Shape(go, "Log", CylMesh(), new Vector3(0, height / 2f, 0), new Vector3(height, 0.6f, height), wood, false, Quaternion.Euler(0, 0, 90f));
            else
            {
                Shape(go, "SeatTop", CylMesh(), new Vector3(0, height, 0), new Vector3(0.55f, 0.04f, 0.55f), wood, false, Quaternion.identity);
                Shape(go, "SeatLeg", CylMesh(), new Vector3(0, height / 2f, 0), new Vector3(0.12f, height / 2f, 0.12f), wood, false, Quaternion.identity);
                Shape(go, "SeatRing", CylMesh(), new Vector3(0, height * 0.35f, 0), new Vector3(0.4f, 0.02f, 0.4f), gold, false, Quaternion.identity);
            }
            var box = go.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, height / 2f, 0);
            box.size = new Vector3(log ? 1.3f : 0.6f, height, 0.6f);
            var enter = Child(go, "SitHere", new Vector3(0, height, 0));
            var exit = Child(go, "StandHere", new Vector3(0, 0.05f, -0.9f));
            var station = go.gameObject.AddComponent<VRCStation>();
            station.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
            station.seated = true;
            station.disableStationExit = false;
            station.canUseStationFromStation = true;
            station.stationEnterPlayerLocation = enter;
            station.stationExitPlayerLocation = exit;
            var seat = UdonSharpUndo.AddComponent<Seat>(go.gameObject);
            SetInteractText(seat, "Sit");
            behaviours.Add(seat);
        }

        /// <summary>A pewter mug of something foamy: a pickup everyone sees moved (VRCObjectSync), put back by the bar bell.</summary>
        private static GameObject MakeMug(Transform parent, Vector3 pos, Material pewter, Material foam)
        {
            var go = new GameObject("Pickup_Mug");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var body = Shape(go.transform, "MugBody", CylMesh(), new Vector3(0, 0.1f, 0), new Vector3(0.17f, 0.1f, 0.17f), pewter, false, Quaternion.identity);
            var top = Shape(go.transform, "MugFoam", CylMesh(), new Vector3(0, 0.205f, 0), new Vector3(0.15f, 0.012f, 0.15f), foam, false, Quaternion.identity);
            var handle = Deco(go.transform, PrimitiveType.Cube, "MugHandle", new Vector3(0.11f, 0.1f, 0), new Vector3(0.04f, 0.12f, 0.05f), pewter, Quaternion.identity, false);
            body.isStatic = top.isStatic = handle.isStatic = false;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.1f, 0);
            col.size = new Vector3(0.2f, 0.21f, 0.2f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var pickup = go.AddComponent<VRCPickup>();
            pickup.InteractionText = "Mug";
            pickup.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes;
            pickup.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
            go.AddComponent<VRCObjectSync>();
            return go;
        }

        private static void SetInteractText(UdonSharpBehaviour b, string text)
        {
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
            if (backing != null) backing.interactText = text;
        }

        private static void TavernMesh(Transform parent, string name, MeshBuilder mb, Material mat, bool collide)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            var mesh = mb.ToMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>A ring-sector block (stages, steps): radius r0..r1, angles a0..a1 (degrees), heights y0..y1 above c.</summary>
        private static void SectorPrism(MeshBuilder mb, Vector3 c, float r0, float r1, float a0, float a1, float y0, float y1, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                float aa = Mathf.Lerp(a0, a1, i / (float)segs), ab = Mathf.Lerp(a0, a1, (i + 1) / (float)segs);
                Vector3 i0 = TPolar(c, r0, aa, y1), i1 = TPolar(c, r0, ab, y1), o0 = TPolar(c, r1, aa, y1), o1 = TPolar(c, r1, ab, y1);
                mb.Quad(i0, o0, o1, i1, Vector3.up);
                Vector3 d = new Vector3(0, y1 - y0, 0);
                mb.Quad(i0 - d, i1 - d, i1, i0, -TDir((aa + ab) / 2f));
                mb.Quad(o0 - d, o1 - d, o1, o0, TDir((aa + ab) / 2f));
            }
            foreach (float a in new[] { a0, a1 })
            {
                Vector3 p0 = TPolar(c, r0, a, y0), p1 = TPolar(c, r1, a, y0);
                Vector3 d = new Vector3(0, y1 - y0, 0);
                Vector3 side = Vector3.Cross(Vector3.up, TDir(a)) * (a == a0 ? 1f : -1f);
                mb.Quad(p0, p1, p1 + d, p0 + d, side);
            }
        }
    }
}
