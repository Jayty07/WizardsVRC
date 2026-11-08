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
    /// The Crystal Tap (Tavern landmark), after the Aureion Collegium concept sheet: a round stone tower filling nearly all of
    /// its block, built round the Starfall fountain (its crystal floats high in the open middle).
    ///   Outside: a 12.6 m drum of grey-violet stone with pilasters, a band at the gallery floor and a cornice; seven school
    ///   banners and lanterns round the ground floor; the room windows above; the grand arched entrance on the south with
    ///   steps and lantern posts; a low purple roof ring up to an upper drum of clerestory windows, then a steep purple cone
    ///   with gold spires round the open oculus and flags round its eaves.
    /// Here: walls, floor, roofs, the entrance and the bell that brings the mugs back. Everything inside (the bar ring round the
    /// fountain, the lounge and the theatre's screens, the seven school wedges under the gallery, the gallery and its ten
    /// rooms, the stairs, the sunbeams) is TavernHall (WizardsVRCSceneBuilder.TavernHall.cs). Every seat is a VRCStation you
    /// can sit in; the mugs are pickups synced for everyone.
    /// Angles below: 0 = east, 90 = north (the entrance at 270, south).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float TavernWallH = 12.6f, TavernDoorH = 7.0f;
        /// <summary>From the top of the walls to the oculus (the jukebox's quiet zone reaches that high).</summary>
        private const float TavernRoofRise = 16f;
        // The plank floor sits 12 cm over the paving under it (well clear, so the two never flicker) and is walked on.
        private const float TavernFloorY = 0.14f;
        private static readonly float[] TavernDoors = { 270f };
        // Above the walls: the low roof ring (from its eaves up to the upper drum), the drum of clerestory windows, the cone.
        private const float LowRoofOver = 1.2f, LowRoofOutY = 12.95f, LowRoofInR = 13.4f, LowRoofInY = 16.6f;
        private const float DrumIn = 13.4f, DrumOut = 14.0f, DrumY0 = 16.3f, DrumY1 = 22.3f;
        private const float ConeR0 = 14.7f, ConeY0 = 22.6f, ConeR1 = 4.6f, ConeY1 = 28.6f;
        /// <summary>Where the barkeep stands, inside the bar ring round the fountain (TavernHall's TavernCore).</summary>
        private const float BarKeepR = 4.3f;

        private static Vector3 TDir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
        private static Vector3 TPolar(Vector3 c, float r, float deg, float y) { Vector3 d = TDir(deg); return new Vector3(c.x + d.x * r, c.y + y, c.z + d.z * r); }
        /// <summary>Rotation whose +Z points outwards at that angle (a wall piece's thickness runs along the radius).</summary>
        private static Quaternion TFacing(float deg) { return Quaternion.LookRotation(TDir(deg)); }
        private static float AngleGap(float a, float b) { return Mathf.Abs(Mathf.DeltaAngle(a, b)); }

        /// <summary>
        /// If 'cell' lies inside a tavern's round hall, the spot behind the bar for that NPC (inside the bar ring round the
        /// fountain, on the east side) and the way they face (out over the bar). Only cells within the hall's wall count:
        /// the corners of the tavern's square block are outside, so NPCs painted there stay put. When several NPCs are
        /// painted inside, the first stands on the east side and the rest spread round the ring (25 degrees apart,
        /// alternating sides) so nobody stands inside anyone else.
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
                float a = slot == 0 ? 0f : ((slot + 1) / 2) * 25f * (slot % 2 == 1 ? 1f : -1f);
                a = Mathf.Clamp(a, -100f, 100f);
                pos = TPolar(c, BarKeepR, a, TavernFloorY);
                front = TDir(a);
                return true;
            }
            return false;
        }

        /// <summary>The tavern hall's wall radius for its block (the same as TavernLandmark builds): nearly all of it.</summary>
        private static float TavernRadius(ZoneGrid g, RectInt fp)
        {
            return Mathf.Max(8f, Mathf.Min(fp.width, fp.height) * g.cell * 0.5f - 1.5f);
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

            var wallStone = PsxTexMat("TavernWallStone", new Color(0.66f, 0.6f, 0.72f), TileTexture(TerrainTexDir + "/StoneWall.png"), 2.2f);
            var trimStone = PsxTexMat("TavernTrimStone", new Color(0.8f, 0.76f, 0.84f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.2f);
            var plaster = PsxTexMat("TavernPlaster", new Color(0.93f, 0.86f, 0.72f), TileTexture(StreetTexDir + "/Plaster.png"), 4f);
            var beam = PsxTexMat("TimberBeam", new Color(0.3f, 0.19f, 0.12f), TileTexture(StreetTexDir + "/Bark.png"), 1.2f);
            var darkWood = PsxTexMat("TavernDarkWood", new Color(0.36f, 0.22f, 0.13f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 1.6f);
            var planks = PsxTexMat("TavernFloor", new Color(0.72f, 0.5f, 0.32f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var ceiling = PsxTexMat("TavernCeiling", new Color(0.55f, 0.37f, 0.22f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var roofTex = PsxTexMat("TavernRoofPurple", Color.Lerp(roofMat.color, new Color(0.42f, 0.22f, 0.62f), 0.5f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.6f);
            var flagCloth = PMat("TavernFlag", new Color(0.46f, 0.2f, 0.7f), 1f, new Color(0.08f, 0.03f, 0.12f));
            var velvet = PsxTexMat("TavernVelvet", new Color(0.78f, 0.14f, 0.2f), TileTexture(StreetTexDir + "/Velvet.png"), 1f);
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));

            // ---- the outer wall: 48 stone pieces round the circle, the entrance gap on the south with a lintel over it
            const int segs = 48;
            float dA = 360f / segs;
            for (int i = 0; i < segs; i++)
            {
                float a = dA * (i + 0.5f);
                float chord = 2f * R * Mathf.Sin(dA * 0.5f * Mathf.Deg2Rad) + 0.12f;
                bool door = false;
                foreach (float d in TavernDoors) if (AngleGap(a, d) < dA) door = true;
                if (door)
                    Block(t, "Lintel", TPolar(c, R - 0.4f, a, (TavernDoorH + TavernWallH) / 2f), new Vector3(chord, TavernWallH - TavernDoorH, 0.8f), wallStone, TFacing(a));
                else
                {
                    Block(t, "Wall", TPolar(c, R - 0.4f, a, TavernWallH / 2f), new Vector3(chord, TavernWallH, 0.8f), wallStone, TFacing(a));
                    Block(t, "Plinth", TPolar(c, R + 0.1f, a, 0.45f), new Vector3(chord, 0.9f, 0.45f), trimStone, TFacing(a));
                }
                // A band at the gallery floor and a cornice at the top, round the whole drum.
                Deco(t, PrimitiveType.Cube, "StringCourse", TPolar(c, R + 0.05f, a, MezzY), new Vector3(chord + 0.06f, 0.4f, 0.4f), trimStone, TFacing(a), false);
                Deco(t, PrimitiveType.Cube, "Cornice", TPolar(c, R - 0.15f, a, TavernWallH + 0.15f), new Vector3(chord + 0.1f, 0.5f, 1.5f), trimStone, TFacing(a), false);
                Deco(t, PrimitiveType.Cube, "CorniceGold", TPolar(c, R + 0.62f, a, TavernWallH - 0.16f), new Vector3(chord + 0.1f, 0.12f, 0.1f), gold, TFacing(a), false);
                // Pilasters every other piece (none beside the entrance or over a room's window).
                float pa = a - dA * 0.5f;
                if (i % 2 == 0 && AngleGap(pa, 270f) > dA * 1.6f && !NearRoomWindow(pa, 2.5f))
                {
                    Deco(t, PrimitiveType.Cube, "Pilaster", TPolar(c, R + 0.12f, pa, TavernWallH / 2f), new Vector3(0.62f, TavernWallH, 0.42f), trimStone, TFacing(pa), false);
                    Deco(t, PrimitiveType.Cube, "PilasterCap", TPolar(c, R + 0.2f, pa, TavernWallH - 0.5f), new Vector3(0.8f, 0.24f, 0.5f), gold, TFacing(pa), false);
                }
            }

            // ---- the grand entrance (south): door posts, an arch in the opening, a gold beam, lantern posts, steps, the name
            foreach (float d in TavernDoors)
            {
                float half = dA;
                Vector3 outDir = TDir(d), side = Vector3.Cross(Vector3.up, outDir);
                foreach (float s in new[] { -1f, 1f })
                {
                    Block(t, "DoorPost", TPolar(c, R - 0.4f, d + s * half, TavernDoorH / 2f), new Vector3(0.9f, TavernDoorH, 1.2f), trimStone, TFacing(d + s * half));
                    // Lantern posts either side of the steps.
                    Vector3 lp = TPolar(c, R + 2.1f, d + s * (half + 2.5f), 0f);
                    Block(t, "LanternPost", lp + new Vector3(0, 1.4f, 0), new Vector3(0.22f, 2.8f, 0.22f), darkWood);
                    Shape(t, "LanternPostCap", GemMesh(), lp + new Vector3(0, 3.1f, 0), new Vector3(0.55f, 0.8f, 0.55f), glow, false, Quaternion.identity);
                    Shape(t, "DoorLantern", GemMesh(), TPolar(c, R + 0.5f, d + s * (half + 2.2f), 4.2f), new Vector3(0.45f, 0.7f, 0.45f), glow, false, Quaternion.identity);
                }
                float span = (R - 0.4f) * Mathf.Sin(half * Mathf.Deg2Rad) - 0.45f;
                Vector3 mid = TPolar(c, (R - 0.4f) * Mathf.Cos(half * Mathf.Deg2Rad), d, 0f);
                ArchBand(t, mid, side, outDir, span, TavernDoorH - span * 0.95f, span * 0.95f, TavernDoorH, 1.0f, trimStone, wallStone, gold);
                Block(t, "DoorBeam", TPolar(c, R + 0.15f, d, TavernDoorH + 0.3f), new Vector3(2f * R * Mathf.Sin(half * Mathf.Deg2Rad) + 1.2f, 0.45f, 0.35f), gold, TFacing(d));
                LandmarkSign(t, TPolar(c, R + 0.55f, d, TavernDoorH + 1.35f), outDir, name, new Color(1f, 0.85f, 0.45f));
                // Two shallow semicircular steps up to the door.
                var steps = new MeshBuilder();
                SectorPrism(steps, c, R - 0.3f, R + 2.4f, d - 13f, d + 13f, 0f, 0.07f, 8);
                SectorPrism(steps, c, R - 0.3f, R + 1.3f, d - 10f, d + 10f, 0.07f, TavernFloorY, 6);
                TavernMesh(t, "EntranceSteps", steps, trimStone, true);
            }

            // ---- round the ground floor: a school banner on each wedge's outer wall and lanterns between them
            foreach (var n in TavernNooks)
            {
                int sc = (int)n.x;
                float a = (n.y + n.z) / 2f;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "OuterBanner";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(t, false);
                q.transform.localPosition = TPolar(c, R + 0.07f, a, 3.9f);
                q.transform.localRotation = TFacing(a + 180f);   // its face (-Z) turned outwards
                q.transform.localScale = new Vector3(1.7f, 3.3f, 1f);
                q.GetComponent<Renderer>().sharedMaterial = ClassMat("ClassBanner" + SchoolLabels[sc], "Banner" + SchoolLabels[sc] + ".png", Vector2.one, 0.5f);
                Deco(t, PrimitiveType.Cube, "OuterBannerRod", TPolar(c, R + 0.15f, a, 5.65f), new Vector3(2.0f, 0.09f, 0.09f), gold, TFacing(a), false);
            }
            for (int k = 0; k < 8; k++)
            {
                float a = 22.5f + 45f * k;
                if (AngleGap(a, 270f) < 30f) continue;
                Deco(t, PrimitiveType.Cube, "SconceBracket", TPolar(c, R + 0.3f, a, 3.3f), new Vector3(0.12f, 0.12f, 0.6f), darkWood, TFacing(a), false);
                Shape(t, "Sconce", GemMesh(), TPolar(c, R + 0.6f, a, 3.45f), new Vector3(0.4f, 0.6f, 0.4f), glow, false, Quaternion.identity);
            }

            // ---- floor, the low roof ring up to the drum (tiles outside, wood ceiling under the gallery), the drum, the cone
            var floor = new MeshBuilder();
            var roofOut = new MeshBuilder();
            var roofIn = new MeshBuilder();
            var coneOut = new MeshBuilder();
            var coneIn = new MeshBuilder();
            for (int i = 0; i < segs; i++)
            {
                float a0 = dA * i, a1 = dA * (i + 1);
                Vector3 f0 = TPolar(c, R - 0.8f, a0, TavernFloorY), f1 = TPolar(c, R - 0.8f, a1, TavernFloorY);
                floor.Tri(c + new Vector3(0, TavernFloorY, 0), f0, f1, Vector3.up);
                float r0 = R + LowRoofOver;
                Vector3 p00 = TPolar(c, r0, a0, LowRoofOutY), p01 = TPolar(c, r0, a1, LowRoofOutY), p10 = TPolar(c, LowRoofInR, a0, LowRoofInY), p11 = TPolar(c, LowRoofInR, a1, LowRoofInY);
                Vector3 outward = (TDir((a0 + a1) / 2f) * (LowRoofInY - LowRoofOutY) + Vector3.up * (r0 - LowRoofInR)).normalized;
                roofOut.Quad(p00, p01, p11, p10, outward);
                Vector3 dn = new Vector3(0, -0.3f, 0);
                roofIn.Quad(p00 + dn, p01 + dn, p11 + dn, p10 + dn, -outward);
                Vector3 q00 = TPolar(c, ConeR0, a0, ConeY0), q01 = TPolar(c, ConeR0, a1, ConeY0), q10 = TPolar(c, ConeR1, a0, ConeY1), q11 = TPolar(c, ConeR1, a1, ConeY1);
                Vector3 coneN = (TDir((a0 + a1) / 2f) * (ConeY1 - ConeY0) + Vector3.up * (ConeR0 - ConeR1)).normalized;
                coneOut.Quad(q00, q01, q11, q10, coneN);
                coneIn.Quad(q00 + dn, q01 + dn, q11 + dn, q10 + dn, -coneN);
                // The upper drum: stone, clerestory windows on every fourth piece (inside and out).
                float am = (a0 + a1) / 2f;
                float dchord = 2f * DrumOut * Mathf.Sin(dA * 0.5f * Mathf.Deg2Rad) + 0.1f;
                Block(t, "Drum", TPolar(c, (DrumIn + DrumOut) / 2f, am, (DrumY0 + DrumY1) / 2f), new Vector3(dchord, DrumY1 - DrumY0, DrumOut - DrumIn), wallStone, TFacing(am));
                Deco(t, PrimitiveType.Cube, "DrumCornice", TPolar(c, DrumOut - 0.05f, am, DrumY1 + 0.1f), new Vector3(dchord + 0.1f, 0.4f, 1.0f), trimStone, TFacing(am), false);
                Deco(t, PrimitiveType.Cube, "EaveTrim", TPolar(c, r0 + 0.05f, am, LowRoofOutY - 0.1f), new Vector3(2f * r0 * Mathf.Sin(dA * 0.5f * Mathf.Deg2Rad) + 0.1f, 0.18f, 0.18f), gold, TFacing(am), false);
                if (i % 4 == 1)
                {
                    float wy = (DrumY0 + DrumY1) / 2f + 0.2f;
                    Deco(t, PrimitiveType.Cube, "DrumWindow", TPolar(c, DrumOut + 0.03f, am, wy), new Vector3(1.4f, 2.8f, 0.06f), window, TFacing(am), false);
                    Deco(t, PrimitiveType.Cube, "DrumWindowIn", TPolar(c, DrumIn - 0.03f, am, wy), new Vector3(1.4f, 2.8f, 0.06f), window, TFacing(am), false);
                    Deco(t, PrimitiveType.Cube, "DrumWindowArch", TPolar(c, DrumOut + 0.06f, am, wy + 1.55f), new Vector3(1.7f, 0.3f, 0.1f), gold, TFacing(am), false);
                    Deco(t, PrimitiveType.Cube, "DrumWindowSill", TPolar(c, DrumOut + 0.1f, am, wy - 1.5f), new Vector3(1.7f, 0.14f, 0.3f), trimStone, TFacing(am), false);
                }
            }
            TavernMesh(t, "TavernFloor", floor, planks, true);
            TavernMesh(t, "TavernRoof", roofOut, roofTex, true);
            TavernMesh(t, "TavernCeiling", roofIn, ceiling, false);
            TavernMesh(t, "TavernCone", coneOut, roofTex, true);
            TavernMesh(t, "TavernConeCeiling", coneIn, ceiling, false);
            // The oculus: a gold rim with eight little spires round it.
            for (int i = 0; i < 16; i++)
            {
                float a = 360f / 16 * (i + 0.5f);
                Deco(t, PrimitiveType.Cube, "OculusRim", TPolar(c, ConeR1, a, ConeY1 + 0.2f), new Vector3(2f * ConeR1 * Mathf.Sin(11.25f * Mathf.Deg2Rad) + 0.1f, 0.5f, 0.6f), gold, TFacing(a), false);
                if (i % 2 == 0) Shape(t, "OculusSpire", GemMesh(), TPolar(c, ConeR1 + 0.1f, a, ConeY1 + 1.0f), new Vector3(0.35f, 1.3f, 0.35f), gold, false, Quaternion.identity);
            }
            // Rafters under the cone, from the drum's top to the oculus.
            for (int i = 0; i < 12; i++)
            {
                float a = 30f * i + 15f;
                Vector3 lo = TPolar(c, DrumIn - 0.25f, a, DrumY1 - 0.2f), hi = TPolar(c, ConeR1 + 0.35f, a, ConeY1 - 0.55f);
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
            // Flags round the cone's eaves: a pole, a violet flag and a gold star.
            for (int k = 0; k < 8; k++)
            {
                float a = 45f * k + 22.5f;
                Vector3 b = TPolar(c, ConeR0 - 0.3f, a, ConeY0 + 0.1f);
                Block(t, "FlagPole", b + new Vector3(0, 1.6f, 0), new Vector3(0.14f, 3.2f, 0.14f), darkWood);
                Vector3 side = Vector3.Cross(Vector3.up, TDir(a));
                Block(t, "Flag", b + new Vector3(0, 2.75f, 0) + side * 0.75f, new Vector3(1.4f, 0.85f, 0.06f), flagCloth, Quaternion.LookRotation(TDir(a)));
                Shape(t, "FlagStar", GemMesh(), b + new Vector3(0, 3.35f, 0), new Vector3(0.32f, 0.42f, 0.32f), gold, false, Quaternion.identity);
            }

            var mugs = new List<GameObject>();
            var pewter = PMat("Pewter", new Color(0.62f, 0.63f, 0.68f), 1f);
            var foam = PMat("MugFoam", new Color(0.98f, 0.95f, 0.85f), 1f);

            // ---- the hall (TavernHall): the bar ring and lounge round the fountain, the theatre's screens, the seven school
            // wedges, the gallery and its rooms, the stairs, sunbeams. Quest objects and the like painted inside the tavern
            // keep the seats off them.
            var avoid = new List<Vector3>();
            if (z != null && z.points != null)
                foreach (var zp in z.points)
                {
                    if (zp == null || zp.Symbol == '\0') continue;
                    foreach (var cell in g.Find(zp.Symbol))
                    {
                        Vector3 p = g.Local(cell);
                        float dx = p.x - c.x, dz = p.z - c.z;
                        if (dx * dx + dz * dz < R * R) avoid.Add(new Vector3(p.x, c.y, p.z));
                    }
                }
            TavernHall(t, c, R, behaviours, mugs, avoid, stone, gold, wood, darkWood, planks, ceiling, beam, window, plaster, velvet, glow, flame, pewter, foam,
                wallStone, trimStone);

            // ---- the bell on the bar that brings every mug back
            var bellGo = Shape(t, "Bell", BlobMesh(), TPolar(c, 5.4f, 335f, 1.42f), new Vector3(0.38f, 0.42f, 0.38f), gold, false, Quaternion.identity);
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
        }

        /// <summary>
        /// A stone arch in the plane through 'mid' (its +X 'side', facing 'fwd'): voussoirs along a half-ellipse 'hs' to each
        /// side, springing at 'spring' and rising 'rise', a gold keystone, and the spandrels filled up to 'top'.
        /// </summary>
        private static void ArchBand(Transform t, Vector3 mid, Vector3 side, Vector3 fwd, float hs, float spring, float rise, float top, float depth,
            Material archMat, Material fillMat, Material keyMat)
        {
            Quaternion face = Quaternion.LookRotation(fwd, Vector3.up);
            const int N = 12;
            for (int k = 0; k < N; k++)
            {
                float t0 = Mathf.PI * k / N, t1 = Mathf.PI * (k + 1) / N;
                Vector2 p0 = new Vector2(hs * Mathf.Cos(t0), spring + rise * Mathf.Sin(t0));
                Vector2 p1 = new Vector2(hs * Mathf.Cos(t1), spring + rise * Mathf.Sin(t1));
                Vector2 m = (p0 + p1) / 2f, d = p1 - p0;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                Block(t, "ArchStone", mid + side * m.x + Vector3.up * m.y, new Vector3(d.magnitude + 0.05f, 0.42f, depth), archMat, face * Quaternion.Euler(0f, 0f, ang));
            }
            Shape(t, "ArchKeystone", GemMesh(), mid + Vector3.up * (spring + rise + 0.05f) - fwd * (depth * 0.5f + 0.02f), new Vector3(0.42f, 0.62f, 0.2f), keyMat, false, face);
            const int S = 12;
            for (int j = 0; j < S; j++)
            {
                float x0 = -hs + 2f * hs * j / S, x1 = -hs + 2f * hs * (j + 1) / S, xm = (x0 + x1) / 2f;
                float yc = spring + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - (xm / hs) * (xm / hs))) + 0.18f;
                float h = top - yc;
                if (h < 0.05f) continue;
                Block(t, "ArchFill", mid + side * xm + Vector3.up * (yc + h / 2f), new Vector3(x1 - x0 + 0.03f, h, depth - 0.12f), fillMat, face);
            }
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
