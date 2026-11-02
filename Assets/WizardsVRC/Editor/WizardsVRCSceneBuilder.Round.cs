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
            List<Vector2> gates)
        {
            var gateBearings = new List<float>();
            foreach (var gv in gates) gateBearings.Add(gv.x);
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
            foreach (var gv in gates)
            {
                float gb = gv.x;
                // A tunnel from a raised gate runs higher than the wall's ring: the arch (and the gatehouse round it) stands
                // clear of the tunnel's own roof, whatever height its floor is.
                float archBottom = Mathf.Max(ground, gv.y) + TunH + TunT;
                float gTop = Mathf.Max(top, archBottom + 1.6f);
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
                    TBox(gh, "GateWall", x0, x1, baseY - 0.5f, gTop, R - thick / 2f, R + thick / 2f, stone);
                    Vector3 tp = new Vector3(side * (open + 4.4f), baseY, R - thick / 2f - 1.2f);
                    Tower(gh, tp, 3.2f, gTop - baseY + 6f, 5f, roof, stone, gold);
                    Pennant(gh, tp + new Vector3(0, gTop - baseY + 11.5f, 0), gold, banner);
                }
                TBox(gh, "GateArch", -open - 0.1f, open + 0.1f, archBottom, gTop, R - thick / 2f, R + thick / 2f, stone);
                for (float x = -open + 0.4f; x < open; x += 1.2f)
                    Deco(gh, PrimitiveType.Cube, "Merlon", new Vector3(x, gTop + 0.6f, R - thick / 2f + 0.35f), new Vector3(0.7f, 1.2f, 0.7f), stone, Quaternion.identity, false);
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
            // (5 cm over the paving and 7 cm under the duel circle's painted layers, so none of them flicker; the gold rim
            // is a band of segments round the edge rather than a second disc under the floor.)
            Shape(t, "DuelFloor", CylMesh(), new Vector3(0, 0.035f, 0), new Vector3(rr * 2f - 0.6f, 0.035f, rr * 2f - 0.6f), floor, false, Quaternion.identity);
            const int rimSegs = 32;
            float rimChord = 2f * (rr - 0.05f) * Mathf.Sin(Mathf.PI / rimSegs) + 0.04f;
            for (int i = 0; i < rimSegs; i++)
            {
                Vector3 dir = Quaternion.Euler(0, (i + 0.5f) / rimSegs * 360f, 0) * Vector3.forward;
                Deco(t, PrimitiveType.Cube, "DuelRim", dir * (rr - 0.15f) + new Vector3(0, 0.045f, 0), new Vector3(rimChord, 0.09f, 0.22f), gold, Quaternion.LookRotation(dir), false);
            }
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
            // It turns to face whoever looks at it, upright (BattleClient.padPivot), so it's dressed on its own pivot.
            var signPivot = Child(t, "DuelSignPivot", new Vector3(0, 4.6f, rr + 0.8f));
            signPivot.localRotation = Quaternion.LookRotation(Vector3.back);
            var label = WorldText(signPivot, "DuelSign", Vector3.zero, Quaternion.identity, new Vector2(1500, 300), 0.005f,
                string.IsNullOrEmpty(l.label) ? "Duelling Circle" : l.label, 48);
            DressSign(label, 1420f, 260f);
            foreach (var tr in signPivot.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;

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
                client.padTitle = hub.duelLabels[i].text;
                client.padPivot = hub.duelLabels[i].transform.parent.parent;

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

        /// <summary>
        /// Floating red (health) and blue (mana) orbs that drift around the area: each follows its own wandering loop
        /// through the streets (to a few random places nearby and back, along walkable ground). Painted orb cells are where
        /// an orb's loop starts; 'perKind' more of each kind are added at random places. Walk into one to top up.
        /// </summary>
        private static void BuildWanderingOrbs(Transform root, ZoneGrid g, string key, List<HubOrb> seeds, int perKind, PlayerState playerState,
            List<RestoreOrb> output, WorldBuild wb)
        {
            var reach = new List<Vector2Int>(g.Reachable());
            if (reach.Count < 4 || (seeds.Count == 0 && perKind <= 0)) return;
            reach.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var rng = new System.Random(StableHash(key) ^ 0x51ed);
            var all = new List<HubOrb>(seeds);
            for (int k = 0; k < perKind * 2; k++)
            {
                var c = reach[rng.Next(reach.Count)];
                all.Add(new HubOrb { pos = g.Local(c), cell = c, mana = k % 2 == 1 });
            }

            Transform holder = Child(root, "Orbs", Vector3.zero);
            Transform routes = Child(root, "OrbRoutes", Vector3.zero);
            var redGlow = PMat("OrbHealth", new Color(1f, 0.2f, 0.22f), 1f, new Color(0.9f, 0.08f, 0.1f));
            var blueGlow = PMat("OrbMana", new Color(0.25f, 0.45f, 1f), 1f, new Color(0.08f, 0.2f, 0.95f));
            var redCore = PMat("OrbHealthCore", new Color(1f, 0.85f, 0.85f), 1f, new Color(1f, 0.55f, 0.55f));
            var blueCore = PMat("OrbManaCore", new Color(0.85f, 0.92f, 1f), 1f, new Color(0.55f, 0.7f, 1f));
            int n = 0;
            foreach (var o in all)
            {
                string name = (o.mana ? "ManaOrb" : "HealthOrb") + n;
                // The loop: from its start to a few random reachable places within ~10 cells, then home, by walkable paths.
                Vector2Int start = reach.Contains(o.cell) ? o.cell : NearestOf(reach, o.cell);
                var near = reach.FindAll(c => Mathf.Abs(c.x - start.x) + Mathf.Abs(c.y - start.y) <= 10);
                var loopCells = new List<Vector2Int> { start };
                Vector2Int at = start;
                for (int k = 0; k < 4 && near.Count > 1; k++)
                {
                    var to = near[rng.Next(near.Count)];
                    var leg = GridPath(g, at, to);
                    if (leg == null) continue;
                    for (int q = 1; q < leg.Count; q++) loopCells.Add(leg[q]);
                    at = to;
                }
                var back = GridPath(g, at, start);
                if (back != null) for (int q = 1; q < back.Count - 1; q++) loopCells.Add(back[q]);

                Transform t = Child(holder, name, o.pos);
                Transform[] path = null;
                float length = 0f;
                if (loopCells.Count >= 3)
                {
                    var corners = ZoneGrid.Corners(loopCells, true);
                    Transform r = Child(routes, name + "Route", Vector3.zero);
                    path = new Transform[corners.Count];
                    for (int q = 0; q < corners.Count; q++)
                    {
                        path[q] = Child(r, "P" + q, g.Local(corners[q]));
                        if (q > 0) length += Vector3.Distance(path[q - 1].localPosition, path[q].localPosition);
                    }
                    if (corners.Count > 1) length += Vector3.Distance(path[corners.Count - 1].localPosition, path[0].localPosition);
                }

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
                // It drifts, bobs and spins, so it can't be static (batched).
                foreach (var tr in t.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;
                var orb = UdonSharpUndo.AddComponent<RestoreOrb>(t.gameObject);
                orb.playerState = playerState;
                orb.mana = o.mana;
                orb.visual = vis;
                orb.percent = 25;
                orb.minimum = 10;
                orb.respawnSeconds = 30f;
                orb.path = path;
                orb.speed = 0.7f + (float)rng.NextDouble() * 0.4f;
                orb.phaseOffset = (float)rng.NextDouble() * Mathf.Max(1f, length);
                output.Add(orb);
                wb.behaviours.Add(orb);
                n++;
            }
            if (n > 0) Debug.Log("[WizardsVRC World] " + key + ": " + n + " wandering health / mana orbs.");
        }

        private static Vector2Int NearestOf(List<Vector2Int> cells, Vector2Int c)
        {
            Vector2Int best = cells[0];
            int bd = int.MaxValue;
            foreach (var o in cells)
            {
                int d = Mathf.Abs(o.x - c.x) + Mathf.Abs(o.y - c.y);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        /// <summary>Shortest walkable path between two cells (breadth-first over CanStep), both ends included; null if none.</summary>
        private static List<Vector2Int> GridPath(ZoneGrid g, Vector2Int from, Vector2Int to)
        {
            if (from == to) return new List<Vector2Int> { from };
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            prev[from] = from;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (p == to) break;
                foreach (var d in ZoneGrid.Dirs)
                {
                    var nb = p + d;
                    if (prev.ContainsKey(nb) || !g.CanStep(p, nb)) continue;
                    prev[nb] = p;
                    queue.Enqueue(nb);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<Vector2Int>();
            for (var c = to; c != from; c = prev[c]) path.Add(c);
            path.Add(from);
            path.Reverse();
            return path;
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                foreach (char ch in s ?? "") h = (h ^ ch) * 16777619;
                return h & 0x7fffffff;
            }
        }
    }
}
