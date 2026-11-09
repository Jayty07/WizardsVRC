using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Putting the maps together (see WizardsVRCSceneBuilder.Maps.cs): each zone's model on parchment with its shadow and
    /// ink outline, arrows from the tags to the exits and landmarks, NPC badges and the margin doodles; each world's
    /// islands in the sky with dotted paths; then everything handed to the book's MapPages.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>Where the book's buttons sit over a map (map pixels): kept clear of tags and doodles.</summary>
        private static readonly Rect[] MapReserved =
        {
            new Rect(MapW * 0.5f - 300f, 0f, 600f, 112f),     // the title scroll
            new Rect(0f, MapH - 86f, 420f, 86f),             // World Map / Where am I?
            new Rect(MapW - 230f, MapH - 86f, 230f, 86f),    // Close
        };

        /// <summary>Roughly how big a tag is on the map (its text is drawn by the book over the map, 1.25x).</summary>
        private static Vector2 MapTagSize(string text)
        {
            return new Vector2(26f + text.Length * 12.6f, 32f);
        }

        private static bool RectHits(Rect a, List<Rect> list, float margin)
        {
            Rect g = new Rect(a.x - margin, a.y - margin, a.width + margin * 2f, a.height + margin * 2f);
            foreach (var r in list) if (g.Overlaps(r)) return true;
            return false;
        }

        /// <summary>A yellow arrow with an ink outline and a soft shadow, from the tag's middle 't' to just short of 'p'.</summary>
        private static void MapArrow(MapCanvas cv, Vector2 t, Vector2 p)
        {
            Vector2 d = p - t;
            float len = d.magnitude;
            if (len < 34f) return;
            Vector2 u = d / len, n = new Vector2(-u.y, u.x);
            Vector2 e = p - u * 10f;
            const float w = 6f, hw = 13f, hl = 20f;
            Vector2 b = e - u * hl;
            Vector2[] shaft = { t + n * w, b + n * w, b + n * hw, e, b - n * hw, b - n * w, t - n * w };
            var shadow = new Vector2[shaft.Length];
            for (int i = 0; i < shaft.Length; i++) shadow[i] = shaft[i] + new Vector2(3f, 4f);
            cv.FillPoly(shadow, new Color32(40, 25, 10, 90));
            cv.Poly(shaft, new Color32(250, 206, 60, 255), MapInk, 3f);
        }

        /// <summary>An NPC's badge: a gold ring round their robe colour, a little hat and face.</summary>
        private static void MapBadge(MapCanvas cv, Vector2 p, Color robe, Color hat)
        {
            const float r = 13f;
            cv.FillEllipse(p + new Vector2(2f, 3f), r, r, new Color32(40, 25, 10, 110));
            cv.FillEllipse(p, r, r, new Color32(250, 214, 90, 255));
            cv.StrokeEllipse(p, r, r, 2f, MapInk);
            cv.FillEllipse(p, r - 4f, r - 4f, (Color32)robe);
            cv.FillEllipse(p + new Vector2(0f, 4.5f), 4f, 3.6f, new Color32(250, 214, 170, 255));
            cv.Poly(new[] { p + new Vector2(-7f, 1f), p + new Vector2(7f, 1f), p + new Vector2(1f, -10f) }, (Color32)hat, MapInk, 1f);
        }

        /// <summary>The doodles that suit a zone (by its look), for its margins.</summary>
        private static string[] MapDoodlesFor(ZoneDefinition z)
        {
            if (z.isHub) return new[] { "WizardFire", "WizardIce", "WizardStorm", "WizardLife", "WizardMyth", "Sparkle", "Notes" };
            switch (z.textureStyle)
            {
                case ZoneTextureStyle.Garden: return new[] { "WizardLife", "Plant", "Rat", "Sparkle", "WizardFire", "Question" };
                case ZoneTextureStyle.Docks: return new[] { "Crab", "WizardIce", "GhostBlue", "Bubble", "Rat", "Question" };
                case ZoneTextureStyle.Forge: return new[] { "Imp", "Troll", "WizardFire", "Notes", "Bat", "Question" };
                case ZoneTextureStyle.Forest: return new[] { "Owl", "Plant", "Bat", "WizardLife", "Question", "Sparkle" };
                case ZoneTextureStyle.Keep: return new[] { "Skeleton", "GhostBlue", "Bat", "WizardDeath", "Question", "Sparkle" };
                case ZoneTextureStyle.Desert:
                case ZoneTextureStyle.Boomtown: return new[] { "Cactus", "Tumbleweed", "Cowboy", "Rat", "Sparkle", "Question" };
                case ZoneTextureStyle.Undercity: return new[] { "Imp", "Rat", "Notes", "LanternGhost", "Sparkle", "Question" };
                case ZoneTextureStyle.Mine: return new[] { "Troll", "Bat", "LanternGhost", "Cowboy", "Question" };
                default: return new[] { "WizardStorm", "GhostPink", "Rat", "LanternGhost", "Question", "Notes" };
            }
        }

        /// <summary>
        /// Scatters doodles in the parchment round the island ('mask', this canvas's size), clear of 'avoid', spread apart
        /// (a coarse grid of 20 px cells; seeded per zone so a rebuild draws them in the same places).
        /// </summary>
        private static void PlaceMapDoodles(MapCanvas cv, float[] mask, string[] names, List<Rect> avoid, int seed)
        {
            const int cs = 20;
            int gw = cv.w / cs, gh = cv.h / cs;
            var occ = new bool[gw, gh];
            for (int j = 0; j < gh; j++)
                for (int i = 0; i < gw; i++)
                {
                    bool hit = false;
                    for (int y = j * cs; y < (j + 1) * cs && !hit; y += 2)
                        for (int x = i * cs; x < (i + 1) * cs; x += 2)
                            if (mask[y * cv.w + x] > 0.1f) { hit = true; break; }
                    if (!hit)
                    {
                        Rect cr = new Rect(i * cs, j * cs, cs, cs);
                        foreach (var r in avoid) if (cr.Overlaps(new Rect(r.x - 10f, r.y - 10f, r.width + 20f, r.height + 20f))) { hit = true; break; }
                    }
                    occ[i, j] = hit;
                }
            const int margin = 2;
            var placed = new List<Vector2>();
            var rnd = new System.Random(seed);
            foreach (string n in names)
            {
                MapCanvas im = LoadMapPng(MapArtDir + "/Doodles/Doodle" + n + ".png");
                if (im == null) continue;
                int cw = Mathf.CeilToInt(im.w / (float)cs), ch = Mathf.CeilToInt(im.h / (float)cs);
                int bi = -1, bj = -1;
                float best = -1f;
                for (int j = margin; j < gh - margin - ch; j++)
                    for (int i = margin; i < gw - margin - cw; i++)
                    {
                        bool free = true;
                        for (int jj = j; jj < j + ch && free; jj++)
                            for (int ii = i; ii < i + cw; ii++)
                                if (occ[ii, jj]) { free = false; break; }
                        if (!free) continue;
                        Vector2 c = new Vector2((i + cw * 0.5f) * cs, (j + ch * 0.5f) * cs);
                        float dist = 9999f;
                        foreach (var p in placed) dist = Mathf.Min(dist, Vector2.Distance(c, p));
                        float score = dist + (float)rnd.NextDouble() * 40f;
                        if (score > best) { best = score; bi = i; bj = j; }
                    }
                if (bi < 0) continue;
                for (int jj = bj; jj < bj + ch; jj++)
                    for (int ii = bi; ii < bi + cw; ii++) occ[ii, jj] = true;
                cv.Blit(im, bi * cs, bj * cs);
                placed.Add(new Vector2((bi + cw * 0.5f) * cs, (bj + ch * 0.5f) * cs));
            }
        }

        /// <summary>A landmark's name for its tag ("" for ones that don't get one), and how much it matters (lower first).</summary>
        private static string MapLandmarkName(ZoneLandmark l, out int priority)
        {
            priority = 2;
            string label = l.label != null ? l.label.Trim() : "";
            // "Headmaster's Tower (click door)": the map keeps the name.
            int cut = label.IndexOf(" (");
            if (cut > 0) label = label.Substring(0, cut);
            switch (l.kind)
            {
                case LandmarkKind.SchoolHall: priority = 1; return label.Length > 0 ? label : "School of " + l.school;
                case LandmarkKind.Keep: priority = 1; return label.Length > 0 ? label : "Headmaster's Tower";
                case LandmarkKind.Tavern: priority = 1; return label.Length > 0 ? label : "Tavern";
                case LandmarkKind.Saloon: priority = 1; return label.Length > 0 ? label : "Saloon";
                case LandmarkKind.Hotel: priority = 1; return label.Length > 0 ? label : "Hotel";
                case LandmarkKind.DungeonSigil: priority = 1; return label.Length > 0 ? label : "Dungeon";
                case LandmarkKind.TrainDepot: priority = 1; return label.Length > 0 ? label : "Depot";
                case LandmarkKind.DuelCircle: priority = 1; return "Duelling Circle";
                case LandmarkKind.Library: return label.Length > 0 ? label : "Library";
                case LandmarkKind.BountyBoard: return "Bounty Board";
                case LandmarkKind.Shop: priority = 3; return label;
                case LandmarkKind.Tower:
                case LandmarkKind.Statue:
                case LandmarkKind.Gazebo:
                case LandmarkKind.Fountain: priority = 3; return label;
                default: return "";
            }
        }

        private struct MapPin
        {
            public Vector2 at;      // map pixels
            public string text;
            public int target;
            public int priority;
        }

        /// <summary>Places each pin's tag near it (outward from the island's middle, clear of the others and the book's buttons), with an arrow.</summary>
        private static void PlaceMapTags(MapCanvas cv, List<MapPin> pins, Vector2 centre, int map, List<MapLabelInfo> labels, List<Rect> taken, int max)
        {
            pins.Sort((a, b) => a.priority.CompareTo(b.priority));
            int n = 0;
            float[] turns = { 0f, -30f, 30f, -60f, 60f, -95f, 95f, 150f, -150f, 180f };
            float[] dists = { 80f, 115f, 150f, 60f };
            foreach (var pin in pins)
            {
                if (n >= max) break;
                Vector2 size = MapTagSize(pin.text);
                Vector2 outward = pin.at - centre;
                if (outward.sqrMagnitude < 1f) outward = Vector2.down;
                outward.Normalize();
                bool done = false;
                foreach (float dist in dists)
                {
                    foreach (float turn in turns)
                    {
                        Vector2 dir = Quaternion.Euler(0f, 0f, turn) * outward;
                        Vector2 t = pin.at + dir * dist + new Vector2(0f, -10f);
                        Rect r = new Rect(t.x - size.x * 0.5f, t.y - size.y * 0.5f, size.x, size.y);
                        if (r.xMin < 40f || r.xMax > cv.w - 40f || r.yMin < 40f || r.yMax > cv.h - 40f) continue;
                        if (RectHits(r, taken, 8f)) continue;
                        bool reserved = false;
                        foreach (var rr in MapReserved) if (r.Overlaps(rr)) { reserved = true; break; }
                        if (reserved) continue;
                        MapArrow(cv, t, pin.at);
                        taken.Add(r);
                        labels.Add(new MapLabelInfo { map = map, text = pin.text, uv = new Vector2(t.x / cv.w, 1f - t.y / cv.h), target = pin.target });
                        n++;
                        done = true;
                        break;
                    }
                    if (done) break;
                }
            }
        }

        /// <summary>Lays one rendered island (or zone) on the parchment: soft shadow, ink outline, then the drawing.</summary>
        private static void LayIsland(MapCanvas onto, MapCanvas island, int dx, int dy, bool shadow)
        {
            float[] mask = island.Mask(30);
            float[] grown = Dilate(mask, island.w, island.h, 2);
            if (shadow)
            {
                float[] soft = Blur(grown, island.w, island.h, 4);
                var sh = new MapCanvas(island.w, island.h);
                sh.Wash(soft, 0, 0, new Color32(60, 40, 20, 102));
                onto.Blit(sh, dx + 12, dy + 16);
            }
            var ol = new MapCanvas(island.w, island.h);
            ol.Wash(grown, 0, 0, MapInk);
            onto.Blit(ol, dx, dy);
            onto.Blit(island, dx, dy);
        }

        // ================================================================== everything, for the book

        /// <summary>
        /// Draws every zone's map and every world's map and fills in the book's MapPages: the textures, the titles, where
        /// each zone sits (height bands, for "you are here"), how world positions land on each map, and the tags.
        /// </summary>
        private static void BuildMaps(MapPages mp, WorldBuild wb, HubLayout hub)
        {
            if (mp == null) return;
            var infos = new List<MapZoneInfo>();
            ZoneDefinition hubZone = hub != null ? hub.zone : null;
            if (hubZone != null && hub.grid != null) infos.Add(new MapZoneInfo { zone = hubZone, grid = hub.grid, rootY = 0f, rootPos = Vector3.zero });
            foreach (var z in wb.data.zones)
            {
                if (z == null || !z.includeInBuild || z.isHub) continue;
                var g = new ZoneGrid(z);
                if (g.height == 0 || g.Find('S').Count == 0) continue;
                infos.Add(new MapZoneInfo { zone = z, grid = g, rootY = ZoneRootPos(z).y, rootPos = ZoneRootPos(z) });
            }
            if (infos.Count == 0) return;
            var indexOf = new Dictionary<string, int>();
            for (int i = 0; i < infos.Count; i++) indexOf[infos[i].zone.zoneKey] = i;
            int hubIndex = hubZone != null && indexOf.ContainsKey(hubZone.zoneKey) ? indexOf[hubZone.zoneKey] : 0;

            // The worlds (each gets a map after the zones'), and the zone in the middle of each.
            var worlds = new List<int>();
            foreach (var info in infos) if (!worlds.Contains(info.zone.world)) worlds.Add(info.zone.world);
            worlds.Sort();
            int zoneCount = infos.Count, mapCount = zoneCount + worlds.Count;
            var textures = new Texture[mapCount];
            var titles = new string[mapCount];
            var subtitles = new string[mapCount];
            var minY = new float[mapCount];
            var maxY = new float[mapCount];
            var minXZ = new Vector2[mapCount];
            var maxXZ = new Vector2[mapCount];
            for (int mi = 0; mi < mapCount; mi++) { minXZ[mi] = new Vector2(1f, 1f); maxXZ[mi] = new Vector2(-1f, -1f); }
            var projU = new Vector4[mapCount];
            var projV = new Vector4[mapCount];
            var worldOf = new int[mapCount];
            var onWorld = new Vector2[mapCount];
            var labels = new List<MapLabelInfo>();
            MapCanvas parchment = LoadMapPng(MapArtDir + "/MapParchment.png");

            // ---- the zones
            for (int i = 0; i < zoneCount; i++)
            {
                var info = infos[i];
                ZoneDefinition z = info.zone;
                ZoneGrid g = info.grid;
                MapProj pj;
                MapCanvas island = RenderZoneIso(z, g, MapW, MapH, 0.82f, 0.74f, 0.54f, false, out pj);
                info.proj = pj;
                var cv = new MapCanvas(MapW, MapH);
                if (parchment != null && parchment.w == MapW && parchment.h == MapH) System.Array.Copy(parchment.px, cv.px, cv.px.Length);
                else for (int k = 0; k < cv.px.Length; k++) cv.px[k] = new Color32(222, 200, 152, 255);
                LayIsland(cv, island, 0, 0, true);
                float[] mask = Dilate(island.Mask(30), MapW, MapH, 2);

                // NPC badges.
                if (z.npcs != null)
                    foreach (var npc in z.npcs)
                    {
                        if (npc == null || npc.Symbol == '\0') continue;
                        var cells = g.Find(npc.Symbol);
                        if (cells.Count == 0) continue;
                        Vector2 p = pj.P(cells[0].x + 0.5f, cells[0].y + 0.5f, (g.Level(cells[0]) - g.minLevel) * pj.stepMap + g.cell * 0.5f);
                        MapBadge(cv, p, npc.robeColor, npc.hatColor);
                    }

                // Pins: the way home (S), the gates to other zones, and the landmarks.
                var pins = new List<MapPin>();
                var start = g.Find('S');
                if (start.Count > 0 && !z.isHub)
                {
                    int home = hubIndex;
                    if (_townOf.ContainsKey(z.zoneKey) && indexOf.ContainsKey(_townOf[z.zoneKey].zoneKey)) home = indexOf[_townOf[z.zoneKey].zoneKey];
                    string homeName = home >= 0 && home < zoneCount ? infos[home].zone.displayName : "Starfall Castle";
                    if (z.isTown) homeName = "Arch to " + (hubZone != null ? hubZone.displayName : "Starfall Castle");
                    pins.Add(new MapPin { at = pj.P(start[0].x + 0.5f, start[0].y + 0.5f, (g.Level(start[0]) - g.minLevel) * pj.stepMap), text = homeName + " >", target = home, priority = 0 });
                }
                if (z.landmarks != null)
                    foreach (var l in z.landmarks)
                    {
                        if (l == null || l.Symbol == '\0') continue;
                        var comps = MapComponents(g, l.Symbol);
                        if (comps.Count == 0) continue;
                        RectInt r = comps[0];
                        foreach (var c in comps) if (c.width * c.height > r.width * r.height) r = c;
                        float ground = (g.Level(new Vector2Int(r.x, r.y)) - g.minLevel) * pj.stepMap;
                        Vector2 at = pj.P(r.x + r.width * 0.5f, r.y + r.height * 0.5f, ground + (LandmarkBlock(l.kind) ? g.cell * 1.4f : 0f));
                        if (l.kind == LandmarkKind.StreetGate)
                        {
                            if (string.IsNullOrEmpty(l.targetZone) || !indexOf.ContainsKey(l.targetZone)) continue;
                            int t = indexOf[l.targetZone];
                            pins.Add(new MapPin { at = at, text = infos[t].zone.displayName + " >", target = t, priority = 0 });
                            continue;
                        }
                        int pr;
                        string name = MapLandmarkName(l, out pr);
                        if (name.Length == 0) continue;
                        pins.Add(new MapPin { at = at, text = name, target = -1, priority = pr });
                    }
                var taken = new List<Rect>();
                Vector2 mid = pj.P(g.width * 0.5f, g.height * 0.5f, 0f);
                PlaceMapTags(cv, pins, mid, i, labels, taken, z.isHub || z.isTown ? 20 : 14);
                var avoid = new List<Rect>(taken);
                avoid.AddRange(MapReserved);
                PlaceMapDoodles(cv, mask, MapDoodlesFor(z), avoid, z.zoneKey.GetHashCode());
                textures[i] = SaveMapPng(cv, MapOutDir + "/Map_" + z.zoneKey + ".png");
                titles[i] = z.displayName;
                subtitles[i] = z.isHub ? "" : z.levelRange;
                minY[i] = z.isHub ? -50f : info.rootY - 50f;
                maxY[i] = z.isHub ? 60f : info.rootY + 60f;
                ZoneGroundRect(z, out minXZ[i], out maxXZ[i]);
                // How world positions land on this map: linear, so its value at the origin and one step along each axis.
                Vector2 o0 = pj.Uv(Vector3.zero, info.rootPos, MapW, MapH);
                Vector2 ox = pj.Uv(Vector3.right, info.rootPos, MapW, MapH) - o0;
                Vector2 oyv = pj.Uv(Vector3.up, info.rootPos, MapW, MapH) - o0;
                Vector2 oz = pj.Uv(Vector3.forward, info.rootPos, MapW, MapH) - o0;
                projU[i] = new Vector4(ox.x, oyv.x, oz.x, o0.x);
                projV[i] = new Vector4(ox.y, oyv.y, oz.y, o0.y);
            }

            // ---- the worlds: the zones as floating islands round the one in the middle
            for (int wi = 0; wi < worlds.Count; wi++)
            {
                int w = worlds[wi];
                int mapIndex = zoneCount + wi;
                var members = new List<int>();
                for (int i = 0; i < zoneCount; i++) if (infos[i].zone.world == w) members.Add(i);
                int centre = members[0];
                foreach (int i in members) if (infos[i].zone.isHub) centre = i;
                if (!infos[centre].zone.isHub) foreach (int i in members) if (infos[i].zone.isTown) { centre = i; break; }
                MapCanvas sky = LoadMapPng(MapArtDir + "/MapSky" + w + ".png") ?? LoadMapPng(MapArtDir + "/MapSky1.png");
                var cv = new MapCanvas(MapW, MapH);
                if (sky != null && sky.w == MapW && sky.h == MapH) System.Array.Copy(sky.px, cv.px, cv.px.Length);
                else for (int k = 0; k < cv.px.Length; k++) cv.px[k] = new Color32(70, 60, 140, 255);

                // Where each zone goes: the way its tunnel leads from the middle zone, turned to the map's view.
                Vector2 C = new Vector2(MapW * 0.5f, MapH * 0.54f);
                var angle = new Dictionary<int, float>();
                var others = new List<int>();
                foreach (int i in members) if (i != centre) others.Add(i);
                for (int k = 0; k < others.Count; k++)
                {
                    int i = others[k];
                    string key = infos[i].zone.zoneKey;
                    Vector3 dirW = Vector3.zero;
                    HubGate gate = null;
                    if (hub != null && hub.gates.TryGetValue(key, out gate) && gate != null && gate.tunnel != null && gate.tunnel.root != null) dirW = gate.tunnel.root.position;
                    else if (_townGates.TryGetValue(key, out gate) && gate != null && gate.tunnel != null && gate.tunnel.root != null) dirW = gate.tunnel.root.position;
                    dirW.y = 0f;
                    float a;
                    if (dirW.sqrMagnitude > 1f)
                    {
                        // World x / z to the isometric screen: east goes right and down, north right and up.
                        float sx = dirW.x + dirW.z, sy = MapProj.B * (dirW.x - dirW.z);
                        a = Mathf.Atan2(sy, sx) * Mathf.Rad2Deg;
                    }
                    else a = k * 360f / Mathf.Max(1, others.Count) - 90f;
                    angle[i] = a;
                }
                // Keep them apart: sorted round the circle, nudged until neighbours are far enough apart.
                others.Sort((p, q) => angle[p].CompareTo(angle[q]));
                float gap = Mathf.Min(52f, 360f / Mathf.Max(1, others.Count) * 0.85f);
                for (int pass = 0; pass < 12 && others.Count > 1; pass++)
                    for (int k = 0; k < others.Count; k++)
                    {
                        int p = others[k], q = others[(k + 1) % others.Count];
                        float d = Mathf.DeltaAngle(angle[p], angle[q]);
                        if (d < gap)
                        {
                            float push = (gap - d) * 0.5f;
                            angle[p] -= push;
                            angle[q] += push;
                        }
                    }
                var pos = new Dictionary<int, Vector2>();
                foreach (int i in others)
                {
                    float a = angle[i] * Mathf.Deg2Rad;
                    Vector2 p = C + new Vector2(Mathf.Cos(a) * MapW * 0.36f, Mathf.Sin(a) * MapH * 0.31f);
                    p.y = Mathf.Clamp(p.y, 190f, MapH - 150f);
                    pos[i] = p;
                }
                pos[centre] = C;
                // Dotted golden paths from the middle out to each.
                foreach (int i in others)
                {
                    Vector2 d = pos[i] - C;
                    for (float t = 0.28f; t < 0.78f; t += 0.045f)
                    {
                        Vector2 p = C + d * t;
                        cv.FillEllipse(p, 5f, 5f, new Color32(255, 214, 90, 255));
                        cv.StrokeEllipse(p, 5f, 5f, 2f, MapInk);
                    }
                }
                // The islands, back to front.
                var order = new List<int>(pos.Keys);
                order.Sort((p, q) => pos[p].y.CompareTo(pos[q].y));
                foreach (int i in order)
                {
                    bool big = i == centre;
                    int iw = big ? 380 : 236, ih = big ? 330 : 206;
                    MapProj ipj;
                    MapCanvas isl = RenderZoneIso(infos[i].zone, infos[i].grid, iw, ih, 0.96f, 0.96f, 0.5f, true, out ipj);
                    Vector2 p = pos[i];
                    int dx = Mathf.RoundToInt(p.x - iw * 0.5f), dy = Mathf.RoundToInt(p.y - ih * 0.42f);
                    LayIsland(cv, isl, dx, dy, false);
                    infos[i].worldMap = mapIndex;
                    infos[i].onWorld = new Vector2(p.x / MapW, 1f - (p.y - ih * 0.12f) / MapH);
                    Vector2 tag = p + new Vector2(0f, big ? 118f : 56f);
                    labels.Add(new MapLabelInfo
                    {
                        map = mapIndex,
                        text = infos[i].zone.displayName + (infos[i].zone.isHub ? "" : "  (" + infos[i].zone.levelRange + ")"),
                        uv = new Vector2(tag.x / MapW, 1f - tag.y / MapH),
                        target = i,
                    });
                }
                textures[mapIndex] = SaveMapPng(cv, MapOutDir + "/Map_World" + w + ".png");
                string title = "World " + w;
                if (wb.data.worlds != null)
                    foreach (var arc in wb.data.worlds) if (arc != null && arc.order == w && !string.IsNullOrEmpty(arc.displayName)) title = arc.displayName;
                titles[mapIndex] = title;
                subtitles[mapIndex] = "";
                minY[mapIndex] = 1f;   // a world map is never "where you are"
                maxY[mapIndex] = 0f;
                worldOf[mapIndex] = -1;
            }
            for (int i = 0; i < zoneCount; i++)
            {
                worldOf[i] = infos[i].worldMap;
                onWorld[i] = infos[i].onWorld;
            }

            mp.maps = textures;
            mp.mapTitles = titles;
            mp.mapSubtitles = subtitles;
            mp.mapMinY = minY;
            mp.mapMaxY = maxY;
            mp.mapMinXZ = minXZ;
            mp.mapMaxXZ = maxXZ;
            mp.mapProjU = projU;
            mp.mapProjV = projV;
            mp.mapWorld = worldOf;
            mp.mapOnWorld = onWorld;
            mp.homeMap = hubIndex;
            mp.labelMap = new int[labels.Count];
            mp.labelText = new string[labels.Count];
            mp.labelPos = new Vector2[labels.Count];
            mp.labelTarget = new int[labels.Count];
            for (int k = 0; k < labels.Count; k++)
            {
                mp.labelMap[k] = labels[k].map;
                mp.labelText[k] = labels[k].text;
                mp.labelPos[k] = labels[k].uv;
                mp.labelTarget[k] = labels[k].target;
            }
            Debug.Log("[WizardsVRC] Maps: " + zoneCount + " zones, " + worlds.Count + " worlds, " + labels.Count + " tags (" + MapOutDir + ").");
        }
    }
}
