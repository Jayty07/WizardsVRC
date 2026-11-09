using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Open worlds (a town with Open World set: World 3's Bigdig). Instead of each street standing on its own island at
    /// its own Height, the town and every street its gates lead to are built side by side in one space:
    ///   - Placement (PlanOpenWorlds, before anything is built): each street stands just past its town gate's tunnel, its
    ///     own entrance tunnel facing the gate across Open World Gap metres of rock, raised or sunk by its Open World Rise.
    ///     The tunnels still carry you between them (TunnelPassage), so each district stays its own area.
    ///   - Each district keeps only its rock shelf (no sea, no neighbourhood of its own, smaller rock heads over its
    ///     tunnels); the culler keeps the districts round you switched on (AreaCuller.viewMargin), so you see the others.
    ///   - Shared by the whole open world (BuildOpenWorlds, after the zones): one chasm floor with glowing sludge, one cavern
    ///     round everything (walls, roof, stalactites, lanterns, rock pillars with shanties), a rock bridge over each pair of
    ///     linked tunnels, and the lower city: shanty towers on stilts rising out of the chasm between the districts.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class OpenWorldPlan
        {
            public ZoneDefinition town;
            public Vector3 townPos;
            public readonly Dictionary<string, Vector3> rootPos = new Dictionary<string, Vector3>();
            public readonly List<ZoneDefinition> members = new List<ZoneDefinition>();
            public readonly List<Rect> rects = new List<Rect>();
            /// <summary>Linked tunnel mouths (world): the gate's mouth and outward, the street's mouth and outward.</summary>
            public readonly List<Vector3[]> links = new List<Vector3[]>();
            public Rect region;
            public float floorY;
            public float shelfY;
        }

        private static readonly Dictionary<string, OpenWorldPlan> _openPlan = new Dictionary<string, OpenWorldPlan>();
        private static readonly List<OpenWorldPlan> _openPlans = new List<OpenWorldPlan>();
        private const float OpenWorldViewMargin = 260f;
        private const float OpenWorldShelfMargin = 5f;

        /// <summary>Works out where every open world's town and streets stand (call before building any zone).</summary>
        private static void PlanOpenWorlds(WorldDataStore.Data data)
        {
            _openPlan.Clear();
            _openPlans.Clear();
            if (data == null || data.zones == null) return;
            foreach (var town in data.zones)
            {
                if (town == null || !town.includeInBuild || town.isHub || !town.isTown || !town.openWorld || town.landmarks == null) continue;
                var tg = new ZoneGrid(town);
                if (tg.height == 0) continue;
                var plan = new OpenWorldPlan { town = town, townPos = new Vector3(0f, town.height, 0f) };
                plan.rootPos[town.zoneKey] = plan.townPos;
                plan.members.Add(town);
                Rect region = ZoneRect(plan.townPos, tg);
                plan.rects.Add(region);
                float floor = plan.townPos.y + tg.minLevel * tg.step;
                plan.shelfY = floor;
                foreach (var l in town.landmarks)
                {
                    if (l == null || l.kind != LandmarkKind.StreetGate || string.IsNullOrEmpty(l.targetZone)) continue;
                    ZoneDefinition sz = null;
                    foreach (var z in data.zones) if (z != null && z.includeInBuild && !z.isTown && !z.isHub && z.zoneKey == l.targetZone) sz = z;
                    if (sz == null || _openPlan.ContainsKey(sz.zoneKey) || plan.rootPos.ContainsKey(sz.zoneKey)) continue;
                    var cells = tg.Find(l.Symbol);
                    if (cells.Count == 0) continue;
                    Vector3 mouth, outward;
                    TunnelMouth(tg, town, cells[0], out mouth, out outward);
                    var sg = new ZoneGrid(sz);
                    var ss = sg.Find('S');
                    if (sg.height == 0 || ss.Count == 0) continue;
                    Vector3 od = ZoneGrid.ToLocalDir(sg.OpenDirection(ss[0]));
                    Vector3 home = sg.Local(ss[0]) - od * (sg.cell * 0.5f);
                    Vector3 gate = plan.townPos + mouth;
                    Vector3 target = gate + outward * Mathf.Max(20f, town.openWorldGap);
                    var pos = new Vector3(target.x - home.x, plan.townPos.y + sz.openWorldRise, target.z - home.z);
                    if (Vector3.Dot(outward, -od) < 0.7f)
                        Debug.LogWarning("[WizardsVRC World] " + sz.displayName + "'s entrance (S) isn't on the side facing " + town.displayName +
                                         "'s gate to it, so its tunnel won't face the gate across the rock. Paint its S on that side.");
                    plan.rootPos[sz.zoneKey] = pos;
                    plan.members.Add(sz);
                    var r = ZoneRect(pos, sg);
                    plan.rects.Add(r);
                    region = Rect.MinMaxRect(Mathf.Min(region.xMin, r.xMin), Mathf.Min(region.yMin, r.yMin), Mathf.Max(region.xMax, r.xMax), Mathf.Max(region.yMax, r.yMax));
                    plan.links.Add(new[] { gate, outward, pos + home, -od });
                    floor = Mathf.Min(floor, pos.y + sg.minLevel * sg.step);
                }
                // Districts too close together get a warning (their shelves would run into each other).
                for (int i = 0; i < plan.rects.Count; i++)
                    for (int j = i + 1; j < plan.rects.Count; j++)
                    {
                        Rect a = plan.rects[i], b = plan.rects[j];
                        float gx = Mathf.Max(b.xMin - a.xMax, a.xMin - b.xMax), gz = Mathf.Max(b.yMin - a.yMax, a.yMin - b.yMax);
                        if (Mathf.Max(gx, gz) < 2f * OpenWorldShelfMargin + 4f)
                            Debug.LogWarning("[WizardsVRC World] " + plan.members[i].displayName + " and " + plan.members[j].displayName +
                                             " overlap in " + town.displayName + "'s open world; move a gate or an entrance (S).");
                    }
                plan.region = region;
                plan.floorY = floor - 45f;
                foreach (var m in plan.members) _openPlan[m.zoneKey] = plan;
                _openPlans.Add(plan);
                Debug.Log("[WizardsVRC World] " + town.displayName + " is an open world: " + plan.members.Count + " districts in " +
                          Mathf.RoundToInt(region.width) + " x " + Mathf.RoundToInt(region.height) + " m.");
            }
        }

        private static Rect ZoneRect(Vector3 pos, ZoneGrid g)
        {
            float w = g.width * g.cell, h = g.height * g.cell;
            return new Rect(pos.x - w / 2f, pos.z - h / 2f, w, h);
        }

        /// <summary>Where a zone's root stands: its place in its open world, else straight up from the castle at its Height.</summary>
        private static Vector3 ZoneRootPos(ZoneDefinition z)
        {
            OpenWorldPlan plan;
            Vector3 p;
            if (z != null && _openPlan.TryGetValue(z.zoneKey, out plan) && plan.rootPos.TryGetValue(z.zoneKey, out p)) return p;
            return new Vector3(0f, z != null ? z.height : 0f, 0f);
        }

        private static bool InOpenWorld(ZoneDefinition z) { return z != null && _openPlan.ContainsKey(z.zoneKey); }

        /// <summary>The open world's chasm floor (world height), or float.NaN when the zone isn't in one.</summary>
        private static float OpenFloorY(ZoneDefinition z)
        {
            OpenWorldPlan plan;
            return z != null && _openPlan.TryGetValue(z.zoneKey, out plan) ? plan.floorY : float.NaN;
        }

        /// <summary>A zone's ground rectangle (x, z) for the quest guide and the map pages: its open-world footprint, or 'any' (min > max).</summary>
        private static void ZoneGroundRect(ZoneDefinition z, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(1f, 1f);
            max = new Vector2(-1f, -1f);
            OpenWorldPlan plan;
            if (z == null || !_openPlan.TryGetValue(z.zoneKey, out plan)) return;
            int i = plan.members.IndexOf(z);
            if (i < 0 || i >= plan.rects.Count) return;
            Rect r = plan.rects[i];
            // Out to halfway across the rock to the next district, so nowhere in the open world is in no district.
            float m = 0.5f * Mathf.Max(20f, plan.town.openWorldGap) + 10f;
            min = new Vector2(r.xMin - m, r.yMin - m);
            max = new Vector2(r.xMax + m, r.yMax + m);
        }

        // ================================================================== the shared world

        /// <summary>Builds each open world's shared pieces (after its zones): chasm floor, cavern, rock bridges, lower city.</summary>
        private static void BuildOpenWorlds(WorldBuild wb)
        {
            foreach (var plan in _openPlans)
            {
                var z = plan.town;
                Rect reg = plan.region;
                var root = new GameObject("OpenWorld_" + z.zoneKey).transform;
                root.position = new Vector3(reg.center.x, plan.floorY, reg.center.y);
                var rng = new System.Random(StableSeed(z.zoneKey) ^ 0x0be7);
                float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                var tm = TunnelMats(z);
                Material rock = tm[5];
                Material bld, roof, low, leaves, trunk;
                StreetMats(z, z.zoneKey + "_Open", out bld, out roof, out low, out leaves, out trunk);
                Material glow = PMat(z.zoneKey + "_Glow", z.accentColor, 1f, z.accentColor);
                float shelf = plan.shelfY - plan.floorY;   // the town's ground, above the chasm floor

                // ---- the chasm floor, and the cavern round the whole open world
                ChasmFloor(root, reg.width, reg.height, 0f, rng, z.zoneKey + "_Open");
                float hw = reg.width / 2f + 40f, hh = reg.height / 2f + 40f, c = 60f;
                var poly = new[]
                {
                    new Vector2(-hw + c, -hh), new Vector2(hw - c, -hh), new Vector2(hw, -hh + c), new Vector2(hw, hh - c),
                    new Vector2(hw - c, hh), new Vector2(-hw + c, hh), new Vector2(-hw, hh - c), new Vector2(-hw, -hh + c),
                };
                CavernShell(root, poly, 0f, shelf, 190f, rng, rock, bld, roof, glow, z.zoneKey + "_Open");

                // ---- a rock bridge over each pair of linked tunnels, from one district's rock head to the other's
                var spines = new List<Vector2[]>();
                var holder = Child(root, "Bridges", Vector3.zero);
                foreach (var link in plan.links)
                {
                    Vector3 a = link[0] - root.position, b = link[2] - root.position;
                    OpenSpine(holder, a, link[1], b, link[3], rock, rng);
                    spines.Add(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z) });
                }
                foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                MergeRenderers(holder, "BridgeMesh");
                NoShadows(holder.gameObject);

                // ---- the lower city: shanty towers on stilts rising out of the chasm, between and round the districts
                var city = Child(root, "LowerCity", Vector3.zero);
                const float step = 30f;
                for (float x = -hw + 20f; x < hw - 20f; x += step)
                    for (float zz = -hh + 20f; zz < hh - 20f; zz += step)
                    {
                        Vector2 p = new Vector2(x + Rr(-8f, 8f), zz + Rr(-8f, 8f));
                        Vector2 world = p + reg.center;
                        bool clear = true;
                        foreach (var r in plan.rects)
                        {
                            var big = new Rect(r.xMin - 16f, r.yMin - 16f, r.width + 32f, r.height + 32f);
                            if (big.Contains(world)) { clear = false; break; }
                        }
                        if (!clear) continue;
                        foreach (var s in spines) if (DistToSegment(p, s[0], s[1]) < 22f) { clear = false; break; }
                        if (!clear || rng.NextDouble() < 0.25) continue;
                        float top = shelf + Rr(-14f, 10f);
                        float hut = Rr(9f, 18f);
                        float stilt = Mathf.Max(4f, top - hut);
                        Vector3 at = new Vector3(p.x, 0f, p.y);
                        Shape(city, "Stilt", CylMesh(), at + new Vector3(0, stilt / 2f, 0), new Vector3(Rr(3f, 5f), stilt / 2f, Rr(3f, 5f)), rock, false, Quaternion.identity);
                        if (rng.NextDouble() < 0.5)
                            foreach (float s in new[] { -1f, 1f })
                                Shape(city, "Strut", CylMesh(), at + new Vector3(s * 3f, stilt * 0.4f, 0), new Vector3(0.5f, stilt * 0.42f, 0.5f), UnderIron(), false,
                                    Quaternion.Euler(0, 0, s * 12f));
                        Quaternion face = Quaternion.Euler(0, Rr(0f, 360f), 0);
                        BackBlock(city, "Deck", at + new Vector3(0, stilt, 0), new Vector3(Rr(9f, 13f), 0.6f, Rr(9f, 13f)), roof, face);
                        ShantyStack(city, at + new Vector3(0, stilt + 0.3f, 0), face, Rr(6f, 10f), Rr(5f, 8f), hut, bld, roof, glow, rng, rng.Next(0, 6));
                    }
                foreach (var t in city.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                MergeRenderers(city, "LowerCityMesh");
                NoShadows(city.gameObject);

                // ---- its own area: on wherever you are in the open world
                wb.cullRoots.Add(root.gameObject);
                var area = new Bounds(new Vector3(root.position.x, plan.floorY + 150f, root.position.z), new Vector3(reg.width + 360f, 340f, reg.height + 360f));
                wb.cullBounds.Add(area);
                root.gameObject.SetActive(false);
            }
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>
        /// A ridge of rock from one tunnel mouth to the other (local to the open world's root): the tunnels run inside it.
        /// Its top slopes from one mouth's height to the other's; below it falls sheer to the chasm floor, craggy all over.
        /// </summary>
        private static void OpenSpine(Transform holder, Vector3 a, Vector3 outA, Vector3 b, Vector3 outB, Material rock, System.Random rng)
        {
            float Rr(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float len = flat.magnitude;
            if (len < 1f) return;
            Quaternion rot = Quaternion.LookRotation(flat / len);
            const int segs = 8;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs, tm = (t0 + t1) / 2f;
                Vector3 mid = Vector3.Lerp(a, b, tm);
                float top = mid.y + 11f + Mathf.Sin(tm * Mathf.PI) * 6f;
                float w = 20f + Mathf.Sin(tm * Mathf.PI) * 6f;
                BackBlock(holder, "Ridge", new Vector3(mid.x, top / 2f, mid.z), new Vector3(w, top, len / segs + 2f), rock, rot);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 crag = mid + rot * new Vector3(Rr(-w * 0.4f, w * 0.4f), 0, Rr(-len / segs * 0.5f, len / segs * 0.5f));
                    Shape(holder, "Crag", BlobMesh(), new Vector3(crag.x, top + Rr(-1f, 2f), crag.z), new Vector3(Rr(7f, 13f), Rr(5f, 10f), Rr(7f, 13f)), rock, false,
                        Quaternion.Euler(0, Rr(0f, 360f), 0));
                }
                if (rng.NextDouble() < 0.5)
                    Shape(holder, "Spur", ConeMesh(), new Vector3(mid.x, top + 5f, mid.z) + rot * new Vector3(Rr(-6f, 6f), 0, 0), new Vector3(Rr(4f, 7f), Rr(9f, 16f), Rr(4f, 7f)), rock, false,
                        Quaternion.identity);
            }
        }
    }
}
