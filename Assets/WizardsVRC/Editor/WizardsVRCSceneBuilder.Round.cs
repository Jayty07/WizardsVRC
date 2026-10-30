using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.UI;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Hub pieces for the round castle: the round curtain wall with its towers, the duelling circle (PvP ring) and the
    /// floating health and mana orbs.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        // ================================================================== round curtain wall

        /// <summary>
        /// Round castles: a crenellated curtain wall on a circle just inside the map edge (see ZoneGrid.RoundWallRadius),
        /// standing from the island's base, with round towers and pennants every 22.5 degrees, and a gatehouse wherever a
        /// tunnel goes out through it (gateBearings, degrees from north).
        /// </summary>
        private static void RoundCurtainWall(Transform parent, ZoneGrid g, float baseY, Material stone, Material roof, Material gold, Material banner,
            List<float> gateBearings)
        {
            Transform t = Child(parent, "CurtainWall", Vector3.zero);
            float R = g.RoundWallRadius(), thick = 4f;
            // Stands on the ring of '#' cells; take the ground height there from the cell straight north of the centre.
            int cx = g.width / 2, cy = Mathf.Clamp(Mathf.RoundToInt(g.height * 0.5f - R / g.cell), 0, g.height - 1);
            float ground = g.Elevation(cx, cy);
            float top = ground + 8f, h = top - baseY + 0.5f;
            const int segs = 72;
            float chord = 2f * R * Mathf.Sin(Mathf.PI / segs) + 0.4f;
            const float gapHalf = 5.6f;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) / segs * 360f;
                bool gap = false;
                foreach (float gb in gateBearings) if (Mathf.Abs(Mathf.DeltaAngle(a, gb)) < gapHalf) gap = true;
                if (gap) continue;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Quaternion rot = Quaternion.LookRotation(dir);
                Vector3 p = dir * R;
                Block(t, "Wall", new Vector3(p.x, baseY - 0.5f + h / 2f, p.z), new Vector3(chord, h, thick), stone, rot);
                // Merlons along both edges of the wall walk.
                foreach (float edge in new[] { -1f, 1f })
                    foreach (float along in new[] { -0.25f, 0.25f })
                    {
                        Vector3 m = p + dir * (edge * (thick / 2f - 0.35f)) + Vector3.Cross(Vector3.up, dir) * (along * chord);
                        Deco(t, PrimitiveType.Cube, "Merlon", new Vector3(m.x, top + 0.6f, m.z), new Vector3(chord * 0.28f, 1.2f, 0.7f), stone, rot, false);
                    }
            }
            float segHalf = 180f / segs;
            foreach (float gb in gateBearings)
            {
                // Gatehouse: the wall either side reaches to the nearest standing wall piece; an arch over the tunnel; two towers.
                Transform gh = Child(t, "Gatehouse", Vector3.zero);
                gh.localRotation = Quaternion.Euler(0, gb, 0);
                float open = TunW / 2f + TunT;
                foreach (float side in new[] { -1f, 1f })
                {
                    float nearest = 180f;
                    for (int i = 0; i < segs; i++)
                    {
                        float a = (i + 0.5f) / segs * 360f;
                        float d = Mathf.DeltaAngle(gb, a) * side;
                        if (d >= gapHalf && d < nearest) nearest = d;
                    }
                    float reach = R * Mathf.Sin((nearest - segHalf) * Mathf.Deg2Rad) + 0.4f;
                    float x0 = side < 0 ? -reach : open, x1 = side < 0 ? -open : reach;
                    TBox(gh, "GateWall", x0, x1, baseY - 0.5f, top, R - thick / 2f, R + thick / 2f, stone);
                    Vector3 tp = new Vector3(side * (open + 4.4f), baseY, R - thick / 2f - 1.2f);
                    Tower(gh, tp, 3.2f, top - baseY + 6f, 5f, roof, stone, gold);
                    Pennant(gh, tp + new Vector3(0, top - baseY + 11.5f, 0), gold, banner);
                }
                TBox(gh, "GateArch", -open - 0.1f, open + 0.1f, ground + TunH + TunT, top, R - thick / 2f, R + thick / 2f, stone);
                for (float x = -open + 0.4f; x < open; x += 1.2f)
                    Deco(gh, PrimitiveType.Cube, "Merlon", new Vector3(x, top + 0.6f, R - thick / 2f + 0.35f), new Vector3(0.7f, 1.2f, 0.7f), stone, Quaternion.identity, false);
            }
            for (int i = 0; i < 16; i++)
            {
                if (gateBearings.Exists(gb => Mathf.Abs(Mathf.DeltaAngle(i * 22.5f, gb)) < gapHalf + 3f)) continue;
                Vector3 dir = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                Vector3 p = dir * R;
                float th = top - baseY + 4f;
                Tower(t, new Vector3(p.x, baseY, p.z), 4.4f, th, 6f, roof, stone, gold);
                Pennant(t, new Vector3(p.x, baseY + th + 6.5f, p.z), gold, banner);
            }
        }

        // ================================================================== duelling circle (PvP)

        /// <summary>
        /// The duelling circle's stage: a round mosaic floor, a low kerb with gaps, Blue and Red team banners on the two
        /// halves, braziers and a sign. The fight itself (BattleManager in PvP mode, its duel circle and the zone that
        /// starts the countdown) is added by <see cref="BuildDuelCircles"/> once the battle system exists.
        /// </summary>
        private static void DuelCircleLandmark(Transform root, ZoneGrid g, ZoneLandmark l, HubLayout hub, Material stone, Material gold, Material wood)
        {
            var fp = g.Footprint(l.Symbol);
            var cells = g.Find(l.Symbol);
            Vector3 c = RectCenter(g, fp);
            c.y = g.Elevation(cells[0].x, cells[0].y);
            float rr = Mathf.Max(DuelCircleRadius + 1f, Mathf.Min(fp.width, fp.height) * g.cell * 0.5f);
            Vector3 toCentre = new Vector3(-c.x, 0f, -c.z);
            Vector3 fwd = toCentre.sqrMagnitude > 1f ? toCentre.normalized : Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(fwd);
            Transform t = Child(root, "DuelCircle_" + l.symbol, c);
            t.localRotation = rot;

            var floor = PsxTexMat("DuelFloor", new Color(0.62f, 0.58f, 0.66f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.4f);
            var blue = PMat("TeamBlue", new Color(0.2f, 0.4f, 0.95f), 1f, new Color(0.05f, 0.12f, 0.35f));
            var red = PMat("TeamRed", new Color(0.95f, 0.22f, 0.18f), 1f, new Color(0.35f, 0.05f, 0.04f));
            var flame = PMat("Flame", new Color(1f, 0.55f, 0.1f), 1f, new Color(1f, 0.4f, 0.05f));
            var iron = PMat("Iron", new Color(0.25f, 0.22f, 0.22f), 1f);

            // Floor: a stone disc with a gold rim, a tinted half for each team under the circle.
            Shape(t, "DuelFloor", CylMesh(), new Vector3(0, 0.03f, 0), new Vector3(rr * 2f - 0.6f, 0.03f, rr * 2f - 0.6f), floor, false, Quaternion.identity);
            Shape(t, "DuelRim", CylMesh(), new Vector3(0, 0.01f, 0), new Vector3(rr * 2f - 0.1f, 0.03f, rr * 2f - 0.1f), gold, false, Quaternion.identity);
            // Low kerb round the edge with four gaps (no colliders: step over it anywhere).
            const int segs = 32;
            float chord = 2f * (rr - 0.5f) * Mathf.Sin(Mathf.PI / segs) + 0.05f;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) / segs * 360f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 12f || Mathf.Abs(Mathf.DeltaAngle(a, 90f)) < 12f ||
                    Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 12f || Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 12f) continue;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Deco(t, PrimitiveType.Cube, "Kerb", dir * (rr - 0.5f) + new Vector3(0, 0.2f, 0), new Vector3(chord, 0.4f, 0.5f), stone, Quaternion.LookRotation(dir), false);
            }
            // Team banners: Blue behind the near half (seats 0-3), Red behind the far half (seats 4-7).
            foreach (float a in new[] { 125f, 155f, 205f, 235f, -55f, -25f, 25f, 55f })
            {
                bool redSide = Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 90f;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Vector3 p = dir * (rr + 0.6f);
                Block(t, "BannerPole", p + new Vector3(0, 2.4f, 0), new Vector3(0.18f, 4.8f, 0.18f), wood);
                Block(t, "Banner", p + new Vector3(0, 3.4f, 0) - dir * 0.15f, new Vector3(1.3f, 2.2f, 0.08f), redSide ? red : blue, Quaternion.LookRotation(dir));
                Shape(t, "BannerTop", GemMesh(), p + new Vector3(0, 5f, 0), new Vector3(0.35f, 0.5f, 0.35f), gold, false, Quaternion.identity);
            }
            // Braziers at the gaps.
            foreach (float a in new[] { 90f, 270f })
            {
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Vector3 p = dir * (rr + 0.9f);
                Shape(t, "Brazier", CylMesh(), p + new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.55f, 0.8f), iron, true, Quaternion.identity);
                Shape(t, "BrazierFlame", ConeMesh(), p + new Vector3(0, 1.55f, 0), new Vector3(0.7f, 0.9f, 0.7f), flame, false, Quaternion.identity);
            }
            // Sign over the gap facing the hub's centre (where the path comes in); BattleClient writes the countdown on it.
            var label = WorldText(t, "DuelSign", new Vector3(0, 4.6f, rr + 0.8f), Quaternion.LookRotation(Vector3.back), new Vector2(1500, 300), 0.005f,
                string.IsNullOrEmpty(l.label) ? "Duelling Circle" : l.label, 56);
            label.color = new Color(1f, 0.85f, 0.45f);

            Transform spot = Child(root, "DuelSpot_" + l.symbol, c);
            spot.localRotation = rot;
            // Duellists come back out on the side facing the hub's centre, clear of the zone.
            Transform back = Child(root, "DuelReturn_" + l.symbol, c + fwd * (rr + 2.5f) + new Vector3(0, 0.1f, 0));
            back.localRotation = Quaternion.LookRotation(-fwd);
            hub.duelSpots.Add(spot);
            hub.duelReturns.Add(back);
            hub.duelLabels.Add(label);
        }

        /// <summary>
        /// Each duelling circle gets its own PvP encounter (BattleManager with pvpMode, no monsters) and a zone round the
        /// circle that starts the 15 second countdown; everyone inside when it ends duels, split into two teams.
        /// </summary>
        private static void BuildDuelCircles(HubLayout hub, Transform systems, CardDatabase cardDb, ItemDatabase itemDb, PlayerState playerState,
            QuestManager qm, List<BattleClient> encounters, WorldBuild wb)
        {
            for (int i = 0; i < hub.duelSpots.Count; i++)
            {
                Transform spot = hub.duelSpots[i];
                string name = hub.zone.zoneKey + "_Duel" + i;
                var client = MakeEncounter(systems, name, cardDb, itemDb, playerState, hub.duelReturns[i], new int[0], false, encounters);
                var m = client.manager;
                m.pvpMode = true;
                m.scaleRosterWithParty = false;
                m.queueSeconds = 15;
                m.ringSpots = new[] { spot };
                client.questManager = qm;
                BuildEncounter(hub.root, spot.localPosition, name, client, 0);
                Transform encRoot = hub.root.Find("Encounter" + name);
                if (encRoot != null) encRoot.localRotation = spot.localRotation;
                client.encounterRoot = encRoot;
                client.hubReturn = hub.duelReturns[i];
                client.defeatReturn = null;
                client.padLabel = hub.duelLabels[i];

                var zone = Child(hub.root, "DuelZone" + i, spot.localPosition + new Vector3(0, 1.5f, 0));
                var box = zone.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(DuelCircleRadius * 2f, 3f, DuelCircleRadius * 2f);
                zone.localRotation = spot.localRotation;
                var pad = UdonSharpUndo.AddComponent<BattleQueuePad>(zone.gameObject);
                pad.client = client;
                pad.keepQueued = true;
                pad.zoneRadius = DuelCircleRadius * 1.45f;
                wb.behaviours.Add(pad);
            }
        }

        // ================================================================== health and mana orbs

        /// <summary>Floating red (health) and blue (mana) orbs on their cells: walk through one to top up.</summary>
        private static void BuildHubOrbs(HubLayout hub, PlayerState playerState, WorldBuild wb)
        {
            if (hub.orbs.Count == 0) return;
            Transform holder = Child(hub.root, "Orbs", Vector3.zero);
            var redGlow = PMat("OrbHealth", new Color(1f, 0.2f, 0.22f), 1f, new Color(0.9f, 0.08f, 0.1f));
            var blueGlow = PMat("OrbMana", new Color(0.25f, 0.45f, 1f), 1f, new Color(0.08f, 0.2f, 0.95f));
            var redCore = PMat("OrbHealthCore", new Color(1f, 0.85f, 0.85f), 1f, new Color(1f, 0.55f, 0.55f));
            var blueCore = PMat("OrbManaCore", new Color(0.85f, 0.92f, 1f), 1f, new Color(0.55f, 0.7f, 1f));
            var rune = PMat("OrbRune", new Color(0.85f, 0.8f, 0.7f), 1f, new Color(0.2f, 0.18f, 0.12f));
            int n = 0;
            foreach (var o in hub.orbs)
            {
                Transform t = Child(holder, (o.mana ? "ManaOrb" : "HealthOrb") + n++, o.pos);
                var col = t.gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 1.3f;
                col.center = new Vector3(0, 1.2f, 0);
                // A rune disc marks the spot (it stays while the orb is away).
                Shape(t, "OrbRune", CylMesh(), new Vector3(0, 0.04f, 0), new Vector3(1.6f, 0.02f, 1.6f), rune, false, Quaternion.identity);
                Shape(t, "OrbRuneGlow", CylMesh(), new Vector3(0, 0.05f, 0), new Vector3(1.1f, 0.02f, 1.1f), o.mana ? blueGlow : redGlow, false, Quaternion.identity);
                // The orb: a glowing low-poly ball with a bright gem inside and four motes circling it.
                Transform vis = Child(t, "Visual", new Vector3(0, 1.35f, 0));
                Deco(vis, PrimitiveType.Sphere, "Orb", Vector3.zero, Vector3.one * 0.62f, o.mana ? blueGlow : redGlow, Quaternion.identity, false);
                Shape(vis, "OrbGem", GemMesh(), Vector3.zero, new Vector3(0.3f, 0.5f, 0.3f), o.mana ? blueCore : redCore, false, Quaternion.identity);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 d = Quaternion.Euler(0, k * 90f, 0) * Vector3.forward;
                    Deco(vis, PrimitiveType.Cube, "Mote", d * 0.55f + new Vector3(0, (k % 2 == 0 ? 0.12f : -0.12f), 0), Vector3.one * 0.09f,
                        o.mana ? blueCore : redCore, Quaternion.Euler(45f, 45f, 0), false);
                }
                // It bobs and spins, so it can't be static (batched).
                foreach (var tr in vis.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
                var orb = UdonSharpUndo.AddComponent<RestoreOrb>(t.gameObject);
                orb.playerState = playerState;
                orb.mana = o.mana;
                orb.visual = vis;
                orb.percent = 25;
                orb.minimum = 10;
                orb.respawnSeconds = 30f;
                hub.orbBehaviours.Add(orb);
                wb.behaviours.Add(orb);
            }
        }
    }
}
