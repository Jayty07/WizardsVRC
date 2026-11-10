using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Rooftop fights (a zone encounter with Arena = Rooftop): World 3's finale, Marshal Brannoch on the roof of the Golden
    /// Seam Casino, the very top of Bigdig, with the whole city spread out below (its own sky: CaveSky's view from the top).
    /// The encounter's ring, inside the casino dungeon's high-roller vault, becomes a brass cage lift up. On the roof: a
    /// gold-trimmed deck ringed by a parapet of marquee lights, the casino's tower with its dome and spire rising behind the
    /// duel ring, searchlights raking the cavern roof, the building dropping away in tiers below the edge. The boss waits
    /// across the ring and has its say as you arrive (its Intro Lines); step into the ring to fight. Win and the lift takes
    /// you back down to the vault (the lever and the Mother Lode are there); lose and you wake at the dungeon's start.
    /// The roof is its own area far from everything, and counts as inside the dungeon for the party.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 RoofOrigin = new Vector3(0f, 2600f, -3600f);
        private static int _roofCount;
        private const float RoofHalf = 26f;

        private static void BuildRooftopFight(Transform zoneRoot, ZoneGrid g, ZoneDefinition z, ZoneEncounter e, Transform systems, CardDatabase cardDb,
            ItemDatabase itemDb, PlayerState playerState, QuestManager qm, List<BattleClient> encounters, WorldBuild wb)
        {
            var cells = g.Find(e.Symbol);
            if (cells.Count == 0 || e.roster == null || e.roster.Count == 0) return;
            int index = _roofCount++;
            int boss = e.roster[0];
            string bossName = MonsterLabel(boss);
            Vector3 liftAt = g.Local(cells[0]);

            // ---- the lift in the vault
            Vector3 toMiddle = new Vector3(-liftAt.x, 0f, -liftAt.z);
            Vector3 face = toMiddle.sqrMagnitude > 0.01f ? toMiddle.normalized : Vector3.forward;
            var brass = UnderBrass();
            var iron = UnderIron();
            var gold = UnderGold();
            var lift = Child(zoneRoot, "RoofLift", liftAt);
            lift.localRotation = Quaternion.LookRotation(face);
            foreach (float sx in new[] { -1.4f, 1.4f })
                foreach (float sz in new[] { -1.4f, 1.4f })
                    Deco(lift, PrimitiveType.Cylinder, "LiftPost", new Vector3(sx, 2f, sz), new Vector3(0.18f, 2f, 0.18f), brass, Quaternion.identity, true);
            Deco(lift, PrimitiveType.Cube, "LiftFloor", new Vector3(0, 0.08f, 0), new Vector3(3f, 0.16f, 3f), gold, Quaternion.identity, false);
            Deco(lift, PrimitiveType.Cube, "LiftTop", new Vector3(0, 4.05f, 0), new Vector3(3.1f, 0.2f, 3.1f), brass, Quaternion.identity, false);
            for (int k = 0; k < 5; k++)
                Deco(lift, PrimitiveType.Cube, "LiftBars", new Vector3(-1.2f + k * 0.6f, 2f, -1.4f), new Vector3(0.06f, 3.9f, 0.06f), iron, Quaternion.identity, false);
            Deco(lift, PrimitiveType.Cylinder, "LiftCable", new Vector3(0, 9f, 0), new Vector3(0.1f, 5f, 0.1f), iron, Quaternion.identity, false);
            Shape(lift, "LiftLamp", GemMesh(), new Vector3(0, 4.6f, 0), new Vector3(0.5f, 0.7f, 0.5f), NeonMat(0), false, Quaternion.identity);
            var hit = lift.gameObject.AddComponent<BoxCollider>();
            hit.isTrigger = false;
            hit.center = new Vector3(0, 1.5f, 0);
            hit.size = new Vector3(2.6f, 3f, 2.6f);
            var liftSign = WorldText(lift, "LiftSign", new Vector3(0, 5.4f, 1.6f), Quaternion.identity, new Vector2(1200, 220), 0.0042f, "Roof Lift\n(click to go up to " + bossName + ")", 44);
            liftSign.color = new Color(1f, 0.85f, 0.4f);
            DressSign(liftSign, 1100f, 200f);
            Transform vaultReturn = Child(zoneRoot, "RoofLiftReturn", liftAt + face * 3.2f + new Vector3(0, 0.1f, 0));
            vaultReturn.localRotation = Quaternion.LookRotation(face);
            wb.monsterTargets[boss] = lift;

            // ---- the roof, far away
            var root = new GameObject("CasinoRoof_" + z.zoneKey).transform;
            root.position = RoofOrigin + new Vector3(index * 400f, 0f, 0f);
            AmbientBed(root, AmbientFor(z), 0.4f);
            Transform arrival = Child(root, "RoofArrival", new Vector3(0, 0.1f, -RoofHalf + 5f));
            arrival.localRotation = Quaternion.identity;   // facing the ring and the tower (+Z)
            RoofDeck(root, z);

            var up = UdonSharpUndo.AddComponent<TeleportHelper>(lift.gameObject);
            up.defaultTarget = arrival;
            UdonBehaviour upBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(up);
            if (upBacking != null) upBacking.interactText = "Up to the roof";
            wb.behaviours.Add(up);

            // The lift back down, at the deck's south-west corner.
            var down = Child(root, "RoofLiftDown", new Vector3(-RoofHalf + 4f, 0f, -RoofHalf + 4f));
            down.localRotation = Quaternion.LookRotation(new Vector3(1f, 0f, 1f));
            foreach (float sx in new[] { -1.4f, 1.4f })
                foreach (float sz in new[] { -1.4f, 1.4f })
                    Deco(down, PrimitiveType.Cylinder, "LiftPost", new Vector3(sx, 2f, sz), new Vector3(0.18f, 2f, 0.18f), brass, Quaternion.identity, true);
            Deco(down, PrimitiveType.Cube, "LiftTop", new Vector3(0, 4.05f, 0), new Vector3(3.1f, 0.2f, 3.1f), brass, Quaternion.identity, false);
            var downHit = down.gameObject.AddComponent<BoxCollider>();
            downHit.center = new Vector3(0, 1.5f, 0);
            downHit.size = new Vector3(2.6f, 3f, 2.6f);
            var back = UdonSharpUndo.AddComponent<TeleportHelper>(down.gameObject);
            back.defaultTarget = vaultReturn;
            UdonBehaviour backBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(back);
            if (backBacking != null) backBacking.interactText = "Back down to the vault";
            wb.behaviours.Add(back);
            var downSign = WorldText(down, "LiftSign", new Vector3(0, 5f, 0), Quaternion.identity, new Vector2(1000, 160), 0.004f, "Lift down to the vault", 44);
            downSign.color = new Color(1f, 0.85f, 0.4f);
            DressSign(downSign, 900f, 150f);

            // ---- the fight in the ring at the middle of the deck
            string name = z.zoneKey + "_Roof" + index;
            var client = MakeEncounter(systems, name, cardDb, itemDb, playerState, arrival, e.roster.ToArray(), false, encounters);
            var m = client.manager;
            m.scaleRosterWithParty = true;
            m.queueSeconds = 6;
            Transform spot = Child(root, "RoofRing", new Vector3(0, 0.05f, 2f));
            m.ringSpots = new[] { spot };
            client.questManager = qm;
            BuildEncounter(root, spot.localPosition, name, client, 0);
            client.encounterRoot = root.Find("Encounter" + name);
            client.hubReturn = arrival;
            client.victoryReturn = vaultReturn;
            client.defeatReturn = _dungeonStart != null ? _dungeonStart : vaultReturn;
            client.padTitle = bossName + "\nStep into the ring to fight";
            var zone = Child(root, "RoofZone", spot.localPosition + new Vector3(0, 1.5f, 0));
            var box = zone.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(DuelCircleRadius * 1.6f, 3f, DuelCircleRadius * 1.6f);
            var pad = UdonSharpUndo.AddComponent<BattleQueuePad>(zone.gameObject);
            pad.client = client;
            pad.keepQueued = true;
            pad.zoneRadius = DuelCircleRadius * 1.1f;
            wb.behaviours.Add(pad);

            // The boss waiting across the ring, with the city behind it, and what it says as you arrive.
            var floor = UdonSharpUndo.AddComponent<TowerFloor>(spot.gameObject);
            floor.client = client;
            floor.centre = spot;
            floor.radius = RoofHalf + 2f;
            floor.speaker = bossName;
            var lines = new List<string>();
            if (e.introLines != null) foreach (var l in e.introLines) if (!string.IsNullOrEmpty(l)) lines.Add(l);
            if (lines.Count == 0) lines.Add("So you made it to the top. Look around you, wizard: there's nowhere higher to fall from.");
            floor.line = StripSpeaker(lines[0]);
            floor.floorTitle = "";
            floor.isTop = true;
            floor.towerName = "The Golden Seam's roof";
            floor.clearBits = 0;
            floor.clearShards = 5;
            var lurk = Child(root, "Lurking", new Vector3(0, 0.1f, 2f + DuelCircleRadius + 4f));
            lurk.localRotation = Quaternion.LookRotation(Vector3.back);
            floor.idleBody = MonsterBody(lurk, cardDb, boss, "LurkingBody");
            TowerDialog(spot, floor, cardDb.MonsterSchool(boss));
            var shots = new[]
            {
                Shot(root, "Shot_Wide", new Vector3(-6f, 4.5f, -RoofHalf + 3f), new Vector3(0, 6f, RoofHalf)),
                Shot(root, "Shot_Boss", new Vector3(3f, 2.4f, 2f + DuelCircleRadius - 2f), lurk.localPosition + new Vector3(0, 1.8f, 0)),
            };
            var cs = MakeCutscene(wb.cutsceneParent, name + "_Greeting", bossName, lines.ToArray(), "roof_" + e.key.ToLowerInvariant(), shots);
            cs.cutsceneId = name + "_Greeting";
            SpeakerLines(cs, bossName, shots, wb);
            if (cs.stances == null || cs.stances.Length != cs.lines.Length)
            {
                cs.stances = new int[cs.lines.Length];
                for (int i = 0; i < cs.stances.Length; i++) cs.stances[i] = Cutscene.STANCE_HOSTILE;
            }
            floor.cutscene = cs;
            floor.cutscenePlayer = qm != null ? qm.cutscenePlayer : null;
            wb.behaviours.Add(cs);
            client.towerFloor = floor;
            wb.behaviours.Add(floor);

            // ---- its own area (and its own sky: the view from the top of the city), inside the dungeon for the party
            wb.cullRoots.Add(root.gameObject);
            var roofBox = new Bounds(root.position + new Vector3(0, 30f, 0), new Vector3(RoofHalf * 2f + 120f, 160f, RoofHalf * 2f + 120f));
            wb.cullBounds.Add(roofBox);
            wb.MusicArea(z, roofBox, "The Golden Seam's roof", true);
            if (_dungeonSigil != null)
            {
                _dungeonSigil.extraMin = roofBox.min;
                _dungeonSigil.extraMax = roofBox.max;
            }
            wb.skyAreas.Add(new SkyArea
            {
                key = z.zoneKey + "_Roof", zone = z, root = root.gameObject, world = z.world, radius = RoofHalf,
                overlook = true, hasEye = true, eye = zoneRoot.TransformPoint(liftAt) + new Vector3(0, 70f, 0),
            });
            root.gameObject.SetActive(false);
            Debug.Log("[WizardsVRC World] " + z.displayName + ": " + bossName + " fought on the casino roof (lift at column " + cells[0].x + ", row " + cells[0].y + ").");
        }

        private static Vector3 AbsV(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        /// <summary>
        /// The casino's roof: a gold-trimmed deck with a roulette inlay round the duel ring, a parapet of marquee lights, the
        /// tower with its dome and spire behind the ring, searchlights, vents and a water tank, and the building falling
        /// away in neon-ringed tiers below the edge (nothing else near: past it, the sky's city).
        /// </summary>
        private static void RoofDeck(Transform root, ZoneDefinition z)
        {
            Material gold = UnderGold(), brass = UnderBrass(), iron = UnderIron();
            Material deck = PsxTexMat("CasinoRoofDeck", new Color(0.26f, 0.18f, 0.2f), TileTexture(TerrainTexDir + "/DeckPlate.png"), 3f);
            Material velvet = PsxTexMat("CasinoWall", new Color(0.4f, 0.06f, 0.1f), TileTexture(StreetTexDir + "/Velvet.png"), 2f);
            Material goldPlate = PsxTexMat("CasinoGoldPlate", new Color(0.95f, 0.75f, 0.32f), TileTexture(StreetTexDir + "/Metal.png"), 2.4f);
            float h = RoofHalf;
            Block(root, "RoofDeck", new Vector3(0, -0.5f, 0), new Vector3(h * 2f, 1f, h * 2f), deck);
            // The roulette inlay round the ring: alternating crimson and black wedges, a gold rim.
            var mb = new MeshBuilder();
            for (int i = 0; i < 36; i++)
                SectorPrism(mb, new Vector3(0, 0, 2f), DuelCircleRadius + 0.4f, DuelCircleRadius + 2.4f, i * 10f, i * 10f + 10f, 0f, 0.03f, 1);
            TavernMesh(root, "RouletteRing", mb, PMat("RouletteRed", new Color(0.55f, 0.06f, 0.08f), 1f), false);
            var mb2 = new MeshBuilder();
            for (int i = 0; i < 36; i += 2)
                SectorPrism(mb2, new Vector3(0, 0, 2f), DuelCircleRadius + 0.4f, DuelCircleRadius + 2.4f, i * 10f, i * 10f + 10f, 0.03f, 0.04f, 1);
            TavernMesh(root, "RouletteBlack", mb2, PMat("RouletteBlack", new Color(0.05f, 0.04f, 0.05f), 1f), false);
            var mb3 = new MeshBuilder();
            SectorPrism(mb3, new Vector3(0, 0, 2f), DuelCircleRadius + 2.4f, DuelCircleRadius + 2.8f, 0f, 360f, 0f, 0.06f, 48);
            TavernMesh(root, "RouletteRim", mb3, gold, false);

            // The parapet, its marquee bulbs, and an invisible wall over it (nobody falls off the top of Bigdig).
            for (int side = 0; side < 4; side++)
            {
                Quaternion rot = Quaternion.Euler(0, side * 90f, 0);
                Vector3 edge = rot * new Vector3(0, 0, h);
                Deco(root, PrimitiveType.Cube, "Parapet", edge + new Vector3(0, 0.6f, 0), AbsV(rot * new Vector3(h * 2f + 1f, 1.2f, 0.8f)), velvet, Quaternion.identity, true);
                Deco(root, PrimitiveType.Cube, "ParapetCap", edge + new Vector3(0, 1.25f, 0), AbsV(rot * new Vector3(h * 2f + 1.4f, 0.15f, 1.1f)), gold, Quaternion.identity, false);
                for (int k = 0; k < 26; k++)
                {
                    Vector3 at = rot * new Vector3(-h + 1f + k * (h * 2f - 2f) / 25f, 1.45f, h);
                    Shape(root, "MarqueeBulb", GemMesh(), at, new Vector3(0.25f, 0.3f, 0.25f), k % 2 == 0 ? NeonMat(2) : NeonMat(0), false, Quaternion.identity);
                }
                var wall = Child(root, "EdgeWall", edge + new Vector3(0, 3f, 0));
                var wc = wall.gameObject.AddComponent<BoxCollider>();
                wc.size = AbsV(rot * new Vector3(h * 2f + 2f, 6f, 1f));
                // The building falling away below the edge in tiers, each ringed with neon.
                for (int t = 1; t <= 3; t++)
                {
                    float drop = t * 14f, out_ = t * 6f;
                    Vector3 c = rot * new Vector3(0, -drop, h + out_ * 0.5f);
                    Deco(root, PrimitiveType.Cube, "Tier", c, AbsV(rot * new Vector3(h * 2f + out_ * 2f, 14f, out_ + 2f)), t % 2 == 0 ? goldPlate : velvet, Quaternion.identity, false);
                    Deco(root, PrimitiveType.Cube, "TierNeon", c + rot * new Vector3(0, 6.9f, out_ * 0.5f + 1.05f), AbsV(rot * new Vector3(h * 2f + out_ * 2f, 0.3f, 0.1f)),
                        NeonMat(t), Quaternion.identity, false);
                }
            }

            // The tower behind the ring: crimson with gold edges and neon bands, the dome, the spire and its gem, the blade sign.
            Vector3 tower = new Vector3(0, 0, h - 8f);
            const float towerH = 30f;
            Deco(root, PrimitiveType.Cube, "CasinoTower", tower + Vector3.up * (towerH / 2f), new Vector3(12f, towerH, 10f), velvet, Quaternion.identity, true);
            foreach (float sx in new[] { -6f, 6f })
                Deco(root, PrimitiveType.Cube, "TowerEdge", tower + new Vector3(sx, towerH / 2f, -5f), new Vector3(1.2f, towerH, 1.2f), goldPlate, Quaternion.identity, false);
            for (float y = 6f; y < towerH; y += 6f)
                Deco(root, PrimitiveType.Cube, "TowerBand", tower + new Vector3(0, y, -5.05f), new Vector3(12.2f, 0.35f, 0.1f), NeonMat(y % 12f < 1f ? 0 : 2), Quaternion.identity, false);
            Shape(root, "CasinoDome", BlobMesh(), tower + Vector3.up * (towerH + 3.5f), new Vector3(13f, 10f, 11f), gold, false, Quaternion.identity);
            Shape(root, "CasinoSpire", ConeMesh(), tower + Vector3.up * (towerH + 14f), new Vector3(2.6f, 14f, 2.6f), gold, false, Quaternion.identity);
            Shape(root, "SpireGem", GemMesh(), tower + Vector3.up * (towerH + 22f), new Vector3(2.6f, 3.4f, 2.6f), NeonMat(0), false, Quaternion.identity);
            var blade = WorldText(root, "CasinoBlade", tower + new Vector3(0, towerH * 0.55f, -5.2f), Quaternion.LookRotation(Vector3.forward), new Vector2(3600, 500), 0.0065f,
                "THE GOLDEN SEAM", 300);
            blade.color = new Color(1f, 0.82f, 0.3f);
            PointLight(root, tower + new Vector3(0, 8f, -8f), new Color(1f, 0.75f, 0.4f), 34f, 1.6f);
            PointLight(root, new Vector3(0, 6f, 2f), new Color(1f, 0.4f, 0.8f), 30f, 1.1f);
            PointLight(root, new Vector3(0, 5f, -h + 6f), new Color(1f, 0.85f, 0.6f), 24f, 1f);

            // Searchlights on the four corners, raking the cavern roof.
            Material beam = PMat("CasinoSearchlight", new Color(1f, 0.95f, 0.75f), 1f, new Color(0.9f, 0.85f, 0.6f));
            for (int k = 0; k < 4; k++)
            {
                float sx = k % 2 == 0 ? -1f : 1f, sz = k < 2 ? -1f : 1f;
                Vector3 baseAt = new Vector3(sx * (h - 2.5f), 0.9f, sz * (h - 2.5f));
                Shape(root, "Searchlight", CylMesh(), baseAt, new Vector3(1.8f, 0.9f, 1.8f), brass, true, Quaternion.identity);
                Vector3 dir = (Vector3.up * 2.4f + new Vector3(sx, 0, sz) * 0.8f).normalized;
                const float len = 80f;
                Shape(root, "SearchlightBeam", ConeMesh(), baseAt + Vector3.up * 0.6f + dir * (len / 2f), new Vector3(7f, len, 7f), beam, false,
                    Quaternion.FromToRotation(Vector3.down, dir));
            }
            // Rooftop clutter: vents, a water tank on legs, a gold slot machine on a plinth.
            Shape(root, "WaterTank", CylMesh(), new Vector3(h - 6f, 6f, -h + 7f), new Vector3(4f, 2.2f, 4f), iron, true, Quaternion.identity);
            foreach (float sx in new[] { -1.4f, 1.4f })
                foreach (float sz in new[] { -1.4f, 1.4f })
                    Deco(root, PrimitiveType.Cylinder, "TankLeg", new Vector3(h - 6f + sx, 1.9f, -h + 7f + sz), new Vector3(0.25f, 1.9f, 0.25f), iron, Quaternion.identity, true);
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = new Vector3((k % 2 == 0 ? -1f : 1f) * (h - 5f), 0.8f, -4f + k * 5f);
                Deco(root, PrimitiveType.Cube, "Vent", at, new Vector3(2.2f, 1.6f, 2.2f), iron, Quaternion.identity, true);
                Deco(root, PrimitiveType.Cylinder, "VentCap", at + new Vector3(0, 1.1f, 0), new Vector3(1.2f, 0.3f, 1.2f), brass, Quaternion.identity, false);
            }
            Deco(root, PrimitiveType.Cube, "SlotPlinth", new Vector3(-h + 7f, 0.6f, h - 9f), new Vector3(2.4f, 1.2f, 2.4f), velvet, Quaternion.identity, true);
            Shape(root, "GoldSlot", BlobMesh(), new Vector3(-h + 7f, 2.6f, h - 9f), new Vector3(1.6f, 2.6f, 1.2f), gold, false, Quaternion.identity);
        }
    }
}
