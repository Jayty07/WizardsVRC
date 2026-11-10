using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Bigdig's skies (every World 3 place): no sky at all, only the cavern. A tall panorama strip (from well below the
    /// horizon to high overhead) painted for each place:
    ///   - the cavern roof overhead, hung with stalactites, chains and lanterns, specked with glow-worms;
    ///   - the city sprawling away on every side: tier on tier of buildings climbing the far cavern walls, lit windows,
    ///     neon, smokestacks and catwalks of lights, and below the horizon the lower city's rooftops, lamplit streets and
    ///     glowing sludge canals going down into the dark;
    ///   - the other districts where they really lie (the Deep Bell over Bigdig's square, the Rustworks' smokestacks, the
    ///     Glimmer Mile's neon towers...), smaller the further off they are;
    ///   - and, in the direction it really stands, the city climbing to a summit with the Golden Seam Casino at the very
    ///     top: a gold tower, dome and spire, its name in neon, searchlights raking the cavern roof.
    /// The casino roof (where World 3's finale is fought) gets the view from the top: the whole city spread out below.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const int CaveW = 2048, CaveH = 320;
        /// <summary>Each cavern sky's strip range (degrees), for its material.</summary>
        private static readonly Dictionary<string, Vector2> _caveRange = new Dictionary<string, Vector2>();

        private class CaveStrip
        {
            public readonly Color[] px = new Color[CaveW * CaveH];
            public float low, high;
            public int X(float bearing) { return Strip.Mod(Mathf.RoundToInt(bearing / 360f * CaveW), CaveW); }
            public int Y(float elev) { return Mathf.RoundToInt((elev - low) / (high - low) * CaveH); }
            public float Elev(int y) { return low + (y + 0.5f) / CaveH * (high - low); }
            public float DegPerPx { get { return 360f / CaveW; } }
            public float DegPerRow { get { return (high - low) / CaveH; } }

            public void Set(int x, int y, Color c, float a = 1f)
            {
                if (y < 0 || y >= CaveH || a <= 0f) return;
                int i = y * CaveW + Strip.Mod(x, CaveW);
                Color o = px[i];
                a = Mathf.Min(1f, a);
                px[i] = new Color(Mathf.Lerp(o.r, c.r, a), Mathf.Lerp(o.g, c.g, a), Mathf.Lerp(o.b, c.b, a), 1f);
            }

            public Color Get(int x, int y) { return y < 0 || y >= CaveH ? Color.black : px[y * CaveW + Strip.Mod(x, CaveW)]; }

            /// <summary>Adds light (for glows, beams and halos).</summary>
            public void Add(int x, int y, Color c, float a)
            {
                if (y < 0 || y >= CaveH || a <= 0f) return;
                int i = y * CaveW + Strip.Mod(x, CaveW);
                Color o = px[i];
                px[i] = new Color(Mathf.Min(1f, o.r + c.r * a), Mathf.Min(1f, o.g + c.g * a), Mathf.Min(1f, o.b + c.b * a), 1f);
            }

            public void Fill(int x0, int x1, int y0, int y1, Color c, float a = 1f)
            {
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Set(x, y, c, a);
            }
        }

        /// <summary>Where the casino stands (world space): the lift up to its roof, if a World 3 street has a rooftop fight.</summary>
        private static bool CasinoWorldPos(List<SkyArea> all, out Vector3 pos)
        {
            pos = Vector3.zero;
            foreach (var a in all)
            {
                if (a == null || a.zone == null || a.root == null || a.overlook || a.zone.textureStyle != ZoneTextureStyle.Undercity) continue;
                foreach (var e in a.zone.encounters)
                {
                    if (e == null || e.arena != "Rooftop") continue;
                    var g = new ZoneGrid(a.zone);
                    var cells = g.Find(e.Symbol);
                    if (cells.Count == 0) continue;
                    pos = a.root.transform.position + g.Local(cells[0]);
                    return true;
                }
            }
            return false;
        }

        private static float BearingFrom(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float b = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return b < 0f ? b + 360f : b;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        private static Texture2D PaintUndergroundSky(SkyArea me, List<SkyArea> all)
        {
            bool top = me.overlook;
            var s = new CaveStrip { low = top ? -62f : -28f, high = top ? 48f : 72f };
            _caveRange[me.key] = new Vector2(s.low, s.high);
            var rng = new System.Random(StableSeed(me.key + "_cave"));
            ZoneDefinition z = me.zone;
            Vector3 eye = me.hasEye ? me.eye : me.root.transform.position;
            Color fog = z.fogColor;
            Color rock = Color.Lerp(fog, Rgb(0.13f, 0.11f, 0.1f), 0.5f);
            Color roofRock = CaveRoofColor(z);
            Color abyss = Rgb(0.03f, 0.025f, 0.03f);
            Color warm = Rgb(1f, 0.74f, 0.4f), gold = Rgb(1f, 0.8f, 0.3f), sludge = Rgb(0.35f, 0.95f, 0.3f);
            Color[] neon = UnderNeon;

            // ---- the base: rock above the horizon darkening up to the roof; the chasm below fading into the dark.
            float roofFrom = top ? 14f : 26f;
            for (int y = 0; y < CaveH; y++)
            {
                float e = s.Elev(y);
                for (int x = 0; x < CaveW; x++)
                {
                    Color c;
                    if (e >= 0f)
                    {
                        float strata = 0.92f + 0.08f * Mathf.Sin(e * 2.3f + Mathf.Sin(x * 0.011f) * 3f);
                        c = Color.Lerp(Color.Lerp(fog, rock, 0.6f), roofRock, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((e - 2f) / (roofFrom + 10f)))) * strata;
                    }
                    else c = Color.Lerp(Color.Lerp(fog, rock, 0.4f), abyss, Mathf.Clamp01(-e / (top ? 50f : 24f)));
                    s.px[y * CaveW + x] = new Color(c.r, c.g, c.b, 1f);
                }
            }

            // The city's light thrown up onto the rock above it, fading towards the roof.
            for (int y = s.Y(0f); y < CaveH; y++)
            {
                float e = s.Elev(y);
                float k = Mathf.Clamp01(1f - e / (top ? 30f : 38f));
                for (int x = 0; x < CaveW; x++) s.Add(x, y, Rgb(1f, 0.58f, 0.3f), 0.13f * k * k);
            }

            // ---- the roof: stalactites hanging down (their tips lit by the city), chains with lanterns, glow-worms.
            for (int i = 0; i < 190; i++)
            {
                float b = (float)rng.NextDouble() * 360f;
                float tip = Mathf.Lerp(roofFrom + 4f, s.high - 4f, (float)rng.NextDouble());
                float w = Mathf.Lerp(0.3f, 1.8f, (float)rng.NextDouble());
                Color dark = Color.Lerp(roofRock, Rgb(0.02f, 0.02f, 0.02f), 0.45f + 0.3f * (float)rng.NextDouble());
                int x0 = s.X(b - w), cols = Mathf.Max(2, Mathf.RoundToInt(w * 2f / s.DegPerPx));
                for (int k = 0; k <= cols; k++)
                {
                    float t = Mathf.Abs(k / (float)cols * 2f - 1f);
                    float e0 = Mathf.Lerp(tip, s.high, Mathf.Pow(t, 0.6f));
                    int yTip = s.Y(e0);
                    for (int y = yTip; y < CaveH; y++)
                    {
                        Color c = k == 0 ? Color.Lerp(dark, fog, 0.3f) : dark;
                        s.Set(x0 + k, y, Color.Lerp(c, Rgb(0.55f, 0.32f, 0.18f), Mathf.Clamp01(1f - (y - yTip) / 6f) * 0.5f));
                    }
                }
            }
            for (int i = 0; i < 70; i++)
            {
                int x = rng.Next(0, CaveW);
                float e = Mathf.Lerp(roofFrom + 2f, s.high - 6f, (float)rng.NextDouble());
                for (int y = s.Y(e); y < CaveH; y++) s.Set(x, y, Rgb(0.05f, 0.045f, 0.045f));
                s.Set(x, s.Y(e), warm); s.Set(x + 1, s.Y(e), warm); s.Set(x, s.Y(e) - 1, warm);
                for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++) s.Add(x + dx, s.Y(e) + dy, warm, 0.06f / (1 + dx * dx + dy * dy));
            }
            for (int i = 0; i < 110; i++)
            {
                // Glow-worms in colonies in the rock overhead, not stars: a cluster of faint blue-green specks.
                float cb = (float)rng.NextDouble() * 360f, ce = Mathf.Lerp(roofFrom, s.high - 2f, (float)rng.NextDouble());
                Color gw = rng.NextDouble() < 0.6 ? Rgb(0.4f, 0.85f, 1f) : Rgb(0.5f, 1f, 0.6f);
                for (int k = 0; k < 9; k++)
                {
                    float b = cb + ((float)rng.NextDouble() - 0.5f) * 3f, e = ce + ((float)rng.NextDouble() - 0.5f) * 2f;
                    s.Set(s.X(b), s.Y(e), gw, 0.3f + 0.4f * (float)rng.NextDouble());
                }
            }

            // ---- the city climbing the far walls: a skyline whose height swells into a summit where the casino stands.
            Vector3 casino;
            bool hasCasino = CasinoWorldPos(all, out casino) && !top;
            float casinoBearing = hasCasino ? BearingFrom(eye, casino) : -1000f;
            float casinoDist = hasCasino ? Mathf.Max(60f, FlatDistance(eye, casino)) : 1f;
            float baseTop = top ? 7f : 8f;
            float summitTop = Mathf.Clamp(16f + 3000f / (casinoDist + 200f), 18f, 30f);
            System.Func<float, float> skyline = b =>
            {
                float h = baseTop + 1.6f * Mathf.Sin(b * 0.07f + 1.3f) + 1.1f * Mathf.Sin(b * 0.19f) + 0.6f * Mathf.Sin(b * 0.53f + 2f);
                if (hasCasino)
                {
                    // A broad mound of city climbing to the summit: steep near the top, spreading out at its foot.
                    float d = Mathf.Abs(Mathf.DeltaAngle(b, casinoBearing));
                    h = Mathf.Max(h, summitTop - d * 0.36f);   // climbing evenly up to the summit
                }
                return h;
            };
            float wallBase = top ? -7f : -1.5f;
            float tierH = top ? 1.3f : 1.7f;
            int tiers = Mathf.CeilToInt((summitTop - wallBase) / tierH) + 1;
            // Highest (furthest up the walls) first, so the nearer, lower tiers stand in front.
            for (int t = tiers - 1; t >= 0; t--)
            {
                float e0 = wallBase + t * tierH;
                float haze = Mathf.Min(0.42f, 0.18f + t * 0.02f);
                Color bodyCol = Color.Lerp(Rgb(0.15f, 0.11f, 0.1f), fog, haze);
                int x = rng.Next(0, 8);
                while (x < CaveW + 8)
                {
                    int w = rng.Next(3, 13);
                    float b = (x + w * 0.5f) * s.DegPerPx;
                    float room = skyline(b) - e0;
                    if (room > 0.3f)
                    {
                        float h = Mathf.Min(room, tierH * Mathf.Lerp(0.9f, 2.4f, (float)rng.NextDouble()));
                        if (rng.NextDouble() < 0.08) { h = Mathf.Min(room + 2f, h * 2.2f); w = Mathf.Max(2, w / 2); }   // a spire
                        int y0 = s.Y(e0), y1 = s.Y(e0 + h);
                        Color body = Color.Lerp(bodyCol, Rgb(0.2f, 0.15f, 0.13f), (float)rng.NextDouble() * 0.3f);
                        s.Fill(x, x + w, y0, y1, body);
                        // a crooked roof line, then lit windows, now and then a neon sign
                        if (rng.NextDouble() < 0.4) s.Fill(x + 1, x + w - 1, y1, y1 + 1, body);
                        float lit = 0.14f + 0.16f * (float)rng.NextDouble();
                        // Brighter and busier climbing up to the casino.
                        if (hasCasino) lit += 0.25f * Mathf.Exp(-Mathf.Abs(Mathf.DeltaAngle(b, casinoBearing)) / 18f);
                        for (int yy = y0 + 1; yy < y1 - 1; yy += 2)
                            for (int xx = x + 1; xx < x + w - 1; xx += 2)
                                if (rng.NextDouble() < lit) s.Set(xx, yy, Color.Lerp(warm, fog, haze * 0.5f));
                        if (rng.NextDouble() < 0.16)
                        {
                            Color n = neon[rng.Next(0, neon.Length)];
                            int nx = x + rng.Next(0, w);
                            for (int yy = y0 + (y1 - y0) / 4; yy < y1 - 1; yy++) s.Set(nx, yy, Color.Lerp(n, fog, haze * 0.3f));
                        }
                        if (rng.NextDouble() < 0.06)
                        {
                            // a smokestack with a furnace glow at its mouth
                            int sx = x + w / 2, sy = s.Y(e0 + h + 1.5f);
                            for (int yy = y1; yy < sy; yy++) s.Set(sx, yy, body);
                            for (int dy = -2; dy <= 3; dy++) for (int dx = -2; dx <= 2; dx++) s.Add(sx + dx, sy + dy, Rgb(1f, 0.45f, 0.15f), 0.18f / (1 + dx * dx + dy * dy));
                        }
                        if (rng.NextDouble() < 0.1) s.Set(x + w / 2, y1, Rgb(1f, 0.2f, 0.15f));   // a beacon on the roof
                    }
                    x += w + (rng.NextDouble() < 0.15 ? rng.Next(1, 4) : 0);
                }
                // catwalks and bridges strung along the tier, dotted with lamps
                if (t % 2 == 1)
                {
                    int yl = s.Y(e0 + tierH * 0.6f);
                    float b0 = (float)rng.NextDouble() * 360f, span = 20f + (float)rng.NextDouble() * 70f;
                    for (int k = 0; k < span / s.DegPerPx; k++)
                    {
                        float b = b0 + k * s.DegPerPx;
                        if (skyline(b) < e0 + tierH) continue;
                        int xx = s.X(b);
                        if (k % 5 == 0) s.Set(xx, yl, Color.Lerp(warm, fog, haze * 0.4f)); else s.Set(xx, yl, bodyCol, 0.6f);
                    }
                }
            }

            // ---- below the horizon: the lower city, rooftops and lamplit streets going down into the dark, sludge canals.
            float cityBottom = top ? s.low + 4f : -24f;
            for (float e = top ? -2f : -0.6f; e > cityBottom; )
            {
                float depth = Mathf.Clamp01(-e / -cityBottom);
                float row = Mathf.Lerp(0.35f, top ? 4.5f : 2.6f, depth);   // nearer rows are taller (perspective)
                int y1 = s.Y(e), y0 = s.Y(e - row);
                Color roofCol = Color.Lerp(Color.Lerp(Rgb(0.13f, 0.1f, 0.09f), fog, 0.35f), abyss, depth * 0.7f);
                int x = rng.Next(0, 6);
                while (x < CaveW + 6)
                {
                    int w = Mathf.Max(2, Mathf.RoundToInt(rng.Next(3, 10) * (1f + depth * 2f)));
                    int hh = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) * Mathf.Lerp(0.5f, 1f, (float)rng.NextDouble())));
                    Color body = Color.Lerp(roofCol, Rgb(0.18f, 0.13f, 0.11f), (float)rng.NextDouble() * 0.25f);
                    s.Fill(x, x + w, y1 - hh, y1, body);
                    float lit = Mathf.Lerp(0.2f, 0.08f, depth);
                    for (int yy = y1 - hh; yy < y1; yy += 2)
                        for (int xx = x; xx < x + w; xx += 2)
                            if (rng.NextDouble() < lit) s.Set(xx, yy, Color.Lerp(warm, abyss, depth * 0.5f));
                    if (rng.NextDouble() < 0.07) { Color n = neon[rng.Next(0, neon.Length)]; s.Fill(x, x + Mathf.Min(w, 3), y1 - 1, y1, n); }
                    x += w + 1;
                }
                // the street in front of the row: a line of lamps
                for (int xx = rng.Next(0, 4); xx < CaveW; xx += rng.Next(3, 7)) s.Set(xx, y1 - (y1 - y0), Color.Lerp(warm, abyss, depth * 0.4f));
                e -= row;
            }
            for (int k = 0; k < 3; k++)
            {
                float ph = (float)rng.NextDouble() * 6.28f, amp = 2f + (float)rng.NextDouble() * 4f, mid = Mathf.Lerp(-4f, cityBottom * 0.7f, (float)rng.NextDouble());
                for (int x = 0; x < CaveW; x++)
                {
                    float e = mid + amp * Mathf.Sin(x * 0.006f + ph) + 0.8f * Mathf.Sin(x * 0.031f + ph * 2f);
                    int y = s.Y(e);
                    float depth = Mathf.Clamp01(-e / -cityBottom);
                    for (int dy = 0; dy < 1 + depth * 2f; dy++) s.Set(x, y - dy, Color.Lerp(sludge, abyss, 0.55f + depth * 0.25f), 0.6f);
                    if (rng.NextDouble() < 0.015) s.Add(x, y, sludge, 0.4f);
                }
            }
            if (top)
            {
                // From the top: the great avenues running out from under the casino, lamplit, to the far walls.
                for (int k = 0; k < 14; k++)
                {
                    float b = k * 360f / 14f + (float)rng.NextDouble() * 8f;
                    for (float e = -55f; e < -1.5f; e += 0.25f)
                    {
                        float wdeg = Mathf.Lerp(0.15f, 2.4f, Mathf.Clamp01(-e / 55f));
                        int x0 = s.X(b - wdeg / 2f), n = Mathf.Max(1, Mathf.RoundToInt(wdeg / s.DegPerPx));
                        for (int i = 0; i <= n; i++) s.Set(x0 + i, s.Y(e), Color.Lerp(Rgb(0.2f, 0.16f, 0.14f), abyss, Mathf.Clamp01(-e / 70f)));
                        if (Mathf.Repeat(e * 4f, 6f) < 1f) { s.Set(x0, s.Y(e), warm); s.Set(x0 + n, s.Y(e), warm); }
                    }
                }
            }

            // ---- the casino at the very top of the city (behind the districts nearer to you).
            if (hasCasino) CasinoSummit(s, casinoBearing, skyline(casinoBearing), Mathf.Clamp(900f / casinoDist, 1.2f, 3f));

            // ---- the other districts where they really lie.
            foreach (var a in all)
            {
                if (a == null || a == me || a.root == null || a.overlook || a.zone == null || a.zone.textureStyle != ZoneTextureStyle.Undercity) continue;
                if (hasCasino && a.zone.encounters.Exists(e => e != null && e.arena == "Rooftop")) continue;   // drawn as the summit
                Vector3 at = a.root.transform.position;
                float d = Mathf.Max(80f, FlatDistance(eye, at));
                float b = BearingFrom(eye, at);
                float size = Mathf.Clamp(380f / d, 0.45f, 1.8f);
                float e0 = top ? -Mathf.Rad2Deg * Mathf.Atan2(Mathf.Max(20f, eye.y - at.y + 20f), d) : Mathf.Lerp(0.5f, -2.5f, Mathf.Clamp01(at.y < eye.y - 15f ? 1f : 0f));
                DistrictMark(s, a.zone.zoneKey, a.zone.isTown, b, e0, size, rng);
            }


            var tex = new Texture2D(CaveW, CaveH, TextureFormat.RGBA32, false);
            var px = new Color32[s.px.Length];
            for (int i = 0; i < px.Length; i++) px[i] = s.px[i];
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>The cavern roof's colour (also the sky colour above the strip).</summary>
        private static Color CaveRoofColor(ZoneDefinition z)
        {
            return Color.Lerp(z.fogColor, Rgb(0.05f, 0.042f, 0.04f), 0.75f);
        }

        /// <summary>A district's landmark on the skyline: the Deep Bell over the town, smokestacks, neon towers, cranes...</summary>
        private static void DistrictMark(CaveStrip s, string key, bool town, float b, float e0, float size, System.Random rng)
        {
            int cx = s.X(b);
            int y0 = s.Y(e0);
            System.Func<float, int> W = deg => Mathf.Max(1, Mathf.RoundToInt(deg * size / s.DegPerPx));
            System.Func<float, int> Hh = deg => Mathf.Max(1, Mathf.RoundToInt(deg * size / s.DegPerRow));
            Color dark = Rgb(0.09f, 0.07f, 0.07f), warm = Rgb(1f, 0.74f, 0.4f), gold = Rgb(1f, 0.8f, 0.3f);
            // A glow over the district first, so it stands out from the city round it.
            Color tint = town ? gold : key.Contains("Glimmer") ? Rgb(0.3f, 1f, 0.9f) : key.Contains("Sprocket") ? Rgb(1f, 0.35f, 0.8f)
                : key.Contains("Rust") ? Rgb(1f, 0.45f, 0.15f) : key.Contains("Fusewick") ? Rgb(0.4f, 1f, 0.35f) : Rgb(1f, 0.25f, 0.2f);
            int gw = W(6f), gh = Hh(5f);
            for (int dy = -gh / 3; dy < gh; dy++)
                for (int dx = -gw; dx <= gw; dx++)
                {
                    float r = (dx / (float)gw) * (dx / (float)gw) + (dy / (float)gh) * (dy / (float)gh);
                    if (r < 1f) s.Add(cx + dx, y0 + dy, tint, 0.09f * (1f - r));
                }
            if (town)
            {
                // The Deep Bell's tower over Lodestone Square: a tall tower, a bell-shaped gold top, lit windows.
                int tw = W(0.9f), th = Hh(8f);
                s.Fill(cx - tw, cx + tw, y0, y0 + th, dark);
                for (int yy = y0 + 2; yy < y0 + th - 2; yy += 3) s.Set(cx, yy, warm);
                int bw = W(1.6f), bh = Hh(2.4f);
                for (int yy = 0; yy < bh; yy++)
                {
                    int half = Mathf.RoundToInt(bw * (1f - yy / (float)bh * 0.7f));
                    for (int xx = -half; xx <= half; xx++) s.Set(cx + xx, y0 + th + yy, gold);
                }
                for (int k = -4; k <= 4; k++) { int hx = W(1.4f) * k; s.Fill(cx + hx - W(0.4f), cx + hx + W(0.4f), y0, y0 + Hh(2.5f + (k * 7 % 3)), dark); }
            }
            else if (key.Contains("Rust"))
            {
                for (int k = -1; k <= 1; k++)
                {
                    int sx = cx + k * W(1.6f), sh = Hh(7f + k * k);
                    s.Fill(sx - W(0.3f), sx + W(0.3f) + 1, y0, y0 + sh, dark);
                    for (int dy = 0; dy < Hh(2f); dy++) for (int dx = -W(0.6f); dx <= W(0.6f); dx++) s.Add(sx + dx, y0 + sh + dy, Rgb(1f, 0.5f, 0.15f), 0.25f);
                }
                s.Fill(cx - W(3f), cx + W(3f), y0, y0 + Hh(2.5f), dark);
            }
            else if (key.Contains("Glimmer"))
            {
                for (int k = -2; k <= 2; k++)
                {
                    int tx = cx + k * W(1.1f), th = Hh(6f + (2 - Mathf.Abs(k)) * 2.5f);
                    s.Fill(tx - W(0.25f), tx + W(0.25f) + 1, y0, y0 + th, dark);
                    Color n = UnderNeon[(k + 5) % UnderNeon.Length];
                    for (int yy = y0 + 1; yy < y0 + th; yy++) s.Set(tx, yy, n);
                    s.Set(tx, y0 + th, Rgb(1f, 1f, 1f));
                }
            }
            else if (key.Contains("Fusewick"))
            {
                // Cranes over the wharves.
                for (int k = -1; k <= 1; k += 2)
                {
                    int tx = cx + k * W(1.8f), th = Hh(6f);
                    s.Fill(tx, tx + 1, y0, y0 + th, dark);
                    for (int xx = -W(1.5f); xx <= W(0.6f); xx++) s.Set(tx + xx * k, y0 + th, dark);
                    s.Set(tx - W(1.5f) * k, y0 + th - 1, Rgb(1f, 0.2f, 0.15f));
                }
                s.Fill(cx - W(3f), cx + W(3f), y0, y0 + Hh(1.5f), dark);
            }
            else if (key.Contains("Sprocket"))
            {
                s.Fill(cx - W(3f), cx + W(3f), y0, y0 + Hh(2.5f), dark);
                for (int i = 0; i < 40; i++)
                    s.Set(cx + rng.Next(-W(3f), W(3f)), y0 + rng.Next(0, Hh(3f)), UnderNeon[rng.Next(0, UnderNeon.Length)]);
            }
            else
            {
                // The Deep Seams: a drill derrick over the pit, its warning lamps.
                int th = Hh(7f);
                for (int yy = 0; yy < th; yy++)
                {
                    int half = Mathf.RoundToInt(W(1.4f) * (1f - yy / (float)th));
                    s.Set(cx - half, y0 + yy, dark); s.Set(cx + half, y0 + yy, dark);
                    if (yy % 3 == 0) for (int xx = -half; xx <= half; xx++) s.Set(cx + xx, y0 + yy, dark);
                }
                s.Set(cx, y0 + th, Rgb(1f, 0.15f, 0.1f));
                s.Set(cx, y0 + th / 2, Rgb(1f, 0.15f, 0.1f));
            }
        }

        /// <summary>The Golden Seam Casino crowning the city: tiers of gold, a tower with a dome and spire, neon, searchlights.</summary>
        private static void CasinoSummit(CaveStrip s, float b, float summit, float size)
        {
            int cx = s.X(b);
            System.Func<float, int> W = deg => Mathf.Max(1, Mathf.RoundToInt(deg * size / s.DegPerPx));
            System.Func<float, int> Hh = deg => Mathf.Max(1, Mathf.RoundToInt(deg * size / s.DegPerRow));
            Color gold = Rgb(1f, 0.8f, 0.3f), deep = Rgb(0.42f, 0.08f, 0.12f), pink = Rgb(1f, 0.3f, 0.78f), warm = Rgb(1f, 0.85f, 0.5f);
            int y = s.Y(summit - 0.8f * size);
            // A warm halo first.
            int gw = W(9f), gh = Hh(12f);
            for (int dy = -gh / 2; dy < gh; dy++)
                for (int dx = -gw; dx <= gw; dx++)
                {
                    float r = (dx / (float)gw) * (dx / (float)gw) + (dy / (float)gh) * (dy / (float)gh);
                    if (r < 1f) s.Add(cx + dx, y + dy, Rgb(1f, 0.7f, 0.35f), 0.16f * (1f - r));
                }
            // Searchlights fanning up into the roof.
            foreach (float lean in new[] { -0.55f, -0.2f, 0.25f, 0.6f })
            {
                float len = 45f;
                for (float t = 0f; t < len; t += s.DegPerRow * 0.5f)
                {
                    float e = summit + 4f * size + t;
                    float bx = b + lean * t;
                    float half = 0.2f + t * 0.06f;
                    int x0 = s.X(bx - half), n = Mathf.Max(1, Mathf.RoundToInt(half * 2f / s.DegPerPx));
                    for (int i = 0; i <= n; i++) s.Add(x0 + i, s.Y(e), Rgb(1f, 0.95f, 0.75f), 0.07f * (1f - t / len));
                }
            }
            // The crowded top of the city it stands on: a stack of buildings widening down the slope, densely lit.
            var stackRng = new System.Random(7);
            int foot = s.Y(summit - 9f * size);
            for (int yy = foot; yy < y + 1; yy++)
            {
                int half = W(4.6f) + Mathf.RoundToInt((y - yy) * 1.4f);
                for (int xx = -half; xx <= half; xx++)
                {
                    bool window = ((xx + yy) & 1) == 0 && stackRng.NextDouble() < 0.45;
                    Color c = window ? (stackRng.NextDouble() < 0.12 ? UnderNeon[stackRng.Next(0, UnderNeon.Length)] : warm) : Rgb(0.16f, 0.11f, 0.1f);
                    // Ragged edges, not a clean triangle.
                    if (Mathf.Abs(xx) > half - 3 && stackRng.NextDouble() < 0.5) continue;
                    s.Set(cx + xx, yy, c);
                }
            }
            // The casino's stepped tiers of gold and crimson, each ringed with lights.
            for (int k = 0; k < 3; k++)
            {
                int hw = W(4.2f - k * 1.2f), th = Hh(1.6f);
                int y0 = y + k * th;
                s.Fill(cx - hw, cx + hw, y0, y0 + th, deep);
                s.Fill(cx - hw, cx + hw, y0 + th - 1, y0 + th, gold);
                for (int xx = cx - hw; xx < cx + hw; xx += 2) s.Set(xx, y0 + th / 2, k == 1 ? pink : warm);
            }
            // The tower, its gold edges and neon bands, the dome, the spire and its gem.
            int towerY = y + 3 * Hh(1.6f);
            int tw = W(0.9f), tH = Hh(6f);
            s.Fill(cx - tw, cx + tw, towerY, towerY + tH, deep);
            s.Fill(cx - tw, cx - tw + 1, towerY, towerY + tH, gold);
            s.Fill(cx + tw - 1, cx + tw, towerY, towerY + tH, gold);
            for (int yy = towerY + 2; yy < towerY + tH; yy += 3) s.Fill(cx - tw, cx + tw, yy, yy + 1, pink);
            int dw = W(1.3f), dh = Hh(1.8f);
            for (int yy = 0; yy < dh; yy++)
            {
                float t = yy / (float)dh;
                int half = Mathf.RoundToInt(dw * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)));
                s.Fill(cx - half, cx + half + 1, towerY + tH + yy, towerY + tH + yy + 1, gold);
            }
            int spireY = towerY + tH + dh, spireH = Hh(3.5f);
            for (int yy = 0; yy < spireH; yy++) s.Set(cx, spireY + yy, gold);
            for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) s.Add(cx + dx, spireY + spireH + dy, pink, 0.35f / (1 + dx * dx + dy * dy));
            s.Set(cx, spireY + spireH, Rgb(1f, 0.85f, 1f));
            // The blade sign down the tower's face.
            for (int yy = towerY + 1; yy < towerY + tH - 1; yy++) s.Set(cx, yy, pink);
        }
    }
}
