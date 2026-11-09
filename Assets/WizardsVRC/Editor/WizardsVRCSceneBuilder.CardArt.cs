using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Card pictures: each spell's art fills the card's whole picture window (116 x 76 pixels) like a painted spell card
    /// illustration, in the game's own pixel style.
    ///   - A painted backdrop for the spell's school: a lava horizon for Fire, an aurora over snow for Ice, storm clouds
    ///     and lightning for Storm, a sunlit glade for Life, a moonlit graveyard for Death, a golden sunburst for Myth, a
    ///     sunset over dunes for Balance, a starry dusk for General. Each card's is a little different (seeded by its id).
    ///   - The subject, drawn bold with a dark outline, a rim of the school's light and a glow behind it: attack spells show
    ///     their creature mid-attack with speed streaks behind it (the same rig that's summoned in battle: diving, pouncing
    ///     or swinging, as it does in the duel), their bolt streaking in, or their rain or burst; heals their bloom; minions their portal. Shields, blades, traps and charms show their emblem (the
    ///     game's own icons), with two smaller ones behind for the three-way spells.
    ///   - School motes over it all (embers, snowflakes, sparks, petals, wisps, sand), a vignette, and the PS1's 15-bit
    ///     colour with its ordered dither.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const int ArtW = 116, ArtH = 76;
        private static readonly int[] ArtBayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        private static readonly Color ArtInk = new Color(0.07f, 0.05f, 0.1f);

        // Per school (0 Fire .. 6 Balance, 7 General): sky top, sky bottom, ground, light (glow and rim), motes.
        private static readonly Color[,] ArtPalette =
        {
            { new Color(0.30f, 0.04f, 0.05f), new Color(0.98f, 0.48f, 0.12f), new Color(0.16f, 0.05f, 0.05f), new Color(1f, 0.75f, 0.3f), new Color(1f, 0.6f, 0.2f) },
            { new Color(0.04f, 0.08f, 0.24f), new Color(0.45f, 0.75f, 0.98f), new Color(0.80f, 0.90f, 1f), new Color(0.75f, 0.95f, 1f), new Color(1f, 1f, 1f) },
            { new Color(0.10f, 0.04f, 0.22f), new Color(0.50f, 0.30f, 0.82f), new Color(0.14f, 0.08f, 0.24f), new Color(0.95f, 0.85f, 1f), new Color(1f, 0.95f, 0.5f) },
            { new Color(0.62f, 0.88f, 0.55f), new Color(0.16f, 0.48f, 0.22f), new Color(0.08f, 0.26f, 0.10f), new Color(0.9f, 1f, 0.7f), new Color(1f, 0.65f, 0.8f) },
            { new Color(0.05f, 0.04f, 0.09f), new Color(0.28f, 0.22f, 0.36f), new Color(0.07f, 0.06f, 0.10f), new Color(0.75f, 1f, 0.8f), new Color(0.7f, 1f, 0.75f) },
            { new Color(0.42f, 0.24f, 0.04f), new Color(1f, 0.85f, 0.40f), new Color(0.35f, 0.20f, 0.05f), new Color(1f, 0.95f, 0.6f), new Color(1f, 0.95f, 0.6f) },
            { new Color(0.70f, 0.30f, 0.18f), new Color(1f, 0.80f, 0.48f), new Color(0.55f, 0.32f, 0.14f), new Color(1f, 0.9f, 0.6f), new Color(1f, 0.85f, 0.55f) },
            { new Color(0.18f, 0.22f, 0.38f), new Color(0.70f, 0.74f, 0.86f), new Color(0.30f, 0.32f, 0.42f), new Color(1f, 1f, 1f), new Color(1f, 1f, 0.85f) },
        };

        private static readonly Dictionary<string, Color32[]> _artIcons = new Dictionary<string, Color32[]>();

        // ------------------------------------------------------------------ small helpers (pixels are top-down: y 0 = top)

        private static float Clamp01(float x) { return x < 0f ? 0f : x > 1f ? 1f : x; }

        private static void Put(Color[] px, int x, int y, Color c, float a = 1f)
        {
            if (x < 0 || y < 0 || x >= ArtW || y >= ArtH) return;
            int i = y * ArtW + x;
            px[i] = Color.Lerp(px[i], c, a);
        }

        /// <summary>Smooth value noise on a seeded 64 x 64 lattice (repeats every 64 units).</summary>
        private class ArtNoise
        {
            private readonly float[] _g = new float[64 * 64];

            public ArtNoise(System.Random rng)
            {
                for (int i = 0; i < _g.Length; i++) _g[i] = (float)rng.NextDouble();
            }

            public float V(float x, float y)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                int xa = ((x0 % 64) + 64) % 64, xb = (xa + 1) % 64, ya = ((y0 % 64) + 64) % 64, yb = (ya + 1) % 64;
                float a = _g[ya * 64 + xa], b = _g[ya * 64 + xb], c = _g[yb * 64 + xa], d = _g[yb * 64 + xb];
                float top = a + (b - a) * fx, bot = c + (d - c) * fx;
                return top + (bot - top) * fy;
            }

            public float Fbm(float x, float y)
            {
                return V(x, y) * 0.6f + V(x * 2.1f, y * 2.1f) * 0.3f + V(x * 4.3f, y * 4.3f) * 0.1f;
            }
        }

        private static int R(System.Random rng, int min, int maxExclusive) { return rng.Next(min, maxExclusive); }

        // ------------------------------------------------------------------ the backdrop

        private static Color[] PaintBackdrop(int school, System.Random rng)
        {
            Color top = ArtPalette[school, 0], bot = ArtPalette[school, 1], ground = ArtPalette[school, 2];
            var n = new ArtNoise(rng);
            var px = new Color[ArtW * ArtH];
            for (int y = 0; y < ArtH; y++)
            {
                Color c = Color.Lerp(top, bot, Mathf.Pow(y / (float)(ArtH - 1), 1.1f));
                for (int x = 0; x < ArtW; x++) px[y * ArtW + x] = c;
            }
            int horizon = (int)(ArtH * 0.72f) + R(rng, -3, 4);
            switch (school)
            {
                case 0:   // Fire: lava glow along the horizon, jagged rocks with glowing seams
                    for (int y = 0; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                            Put(px, x, y, new Color(1f, 0.8f, 0.35f), Clamp01(1f - Mathf.Abs(y - horizon) / 14f) * 0.5f);
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + (int)((n.Fbm(x / 9f, 3.3f) - 0.5f) * 18f);
                        for (int y = h; y < ArtH; y++)
                        {
                            bool seam = y - h < 10 && Mathf.Abs(n.V(x / 5f, y / 3f) - 0.5f) < 0.025f;
                            Put(px, x, y, seam ? new Color(1f, 0.55f, 0.15f) : ground);
                        }
                        Put(px, x, h, new Color(0.45f, 0.12f, 0.06f));
                    }
                    break;
                case 1:   // Ice: an aurora ribbon over snowy hills
                {
                    float phase = (float)rng.NextDouble() * 6f;
                    for (int x = 0; x < ArtW; x++)
                    {
                        float cy = 18f + Mathf.Sin(x / 13f + phase) * 6f + (n.V(x / 10f, 1f) - 0.5f) * 8f;
                        for (int y = 0; y < ArtH; y++)
                        {
                            float d = (y - cy) / 7f;
                            if (d > -1f && d < 1.6f) Put(px, x, y, d < 0.5f ? new Color(0.45f, 1f, 0.8f) : new Color(0.4f, 0.7f, 1f), (1f - Mathf.Abs(d) / 1.6f) * 0.45f);
                        }
                    }
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + (int)(Mathf.Sin(x / 17f + phase) * 4f + (n.V(x / 8f, 7f) - 0.5f) * 6f);
                        for (int y = h; y < ArtH; y++) Put(px, x, y, Color.Lerp(ground, new Color(0.55f, 0.7f, 0.95f), (y - h) / 22f));
                    }
                    break;
                }
                case 2:   // Storm: churning clouds and a distant fork of lightning
                {
                    Color cloud = Color.Lerp(top, new Color(0.35f, 0.25f, 0.55f), 0.6f);
                    for (int y = 0; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                        {
                            float c = n.Fbm(x / 14f, y / 7f);
                            if (c > 0.52f) Put(px, x, y, cloud, Mathf.Min(1f, (c - 0.52f) * 4f));
                        }
                    int bx = rng.NextDouble() < 0.5 ? R(rng, 12, 30) : R(rng, 86, 104);
                    for (int y = 0; y < horizon; y++)
                    {
                        Put(px, bx, y, new Color(1f, 1f, 0.75f));
                        Put(px, bx + 1, y, new Color(0.85f, 0.75f, 1f), 0.6f);
                        if (rng.NextDouble() < 0.35) bx += rng.NextDouble() < 0.5 ? -1 : 1;
                    }
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + 4 + (int)((n.V(x / 12f, 9f) - 0.5f) * 6f);
                        for (int y = h; y < ArtH; y++) Put(px, x, y, ground);
                    }
                    break;
                }
                case 3:   // Life: a sunlit glade, shafts of light, leafy edges
                    for (int y = 0; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                            if ((x + y * 0.6f) % 26f < 5f) Put(px, x, y, new Color(1f, 1f, 0.8f), 0.12f);
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + (int)((n.Fbm(x / 10f, 5f) - 0.5f) * 8f);
                        for (int y = h; y < ArtH; y++) Put(px, x, y, Color.Lerp(ground, new Color(0.2f, 0.45f, 0.15f), 0.3f * ((x + y) % 2)));
                    }
                    for (int side = 0; side < 2; side++)
                        for (int y = 0; y < ArtH; y++)
                        {
                            int edge = (int)(10f + (n.Fbm(y / 6f, side * 10f) - 0.5f) * 14f);
                            for (int x = 0; x < edge; x++)
                                Put(px, side == 0 ? x : ArtW - 1 - x, y, Color.Lerp(new Color(0.07f, 0.25f, 0.08f), new Color(0.15f, 0.4f, 0.12f), n.V(x / 3f, y / 3f)));
                        }
                    break;
                case 4:   // Death: a pale moon, a row of crooked headstones, mist
                {
                    int mx = R(rng, 70, 97), my = R(rng, 14, 23);
                    const float mr = 10f;
                    for (int y = 0; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                        {
                            float d = Mathf.Sqrt((x - mx) * (x - mx) + (y - my) * (y - my));
                            if (d < mr) Put(px, x, y, n.V(x / 3f, y / 3f) > 0.35f ? new Color(0.85f, 0.95f, 0.8f) : new Color(0.7f, 0.82f, 0.7f));
                            else if (d < mr + 8f) Put(px, x, y, new Color(0.6f, 0.85f, 0.65f), (1f - (d - mr) / 8f) * 0.25f);
                        }
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + (int)((n.V(x / 14f, 3f) - 0.5f) * 5f);
                        for (int y = h; y < ArtH; y++) Put(px, x, y, ground);
                    }
                    for (int k = 0; k < 5; k++)
                    {
                        int gx = R(rng, 4, ArtW - 8), gh = R(rng, 5, 10);
                        for (int y = horizon - gh; y < horizon + 2; y++)
                            for (int x = gx; x < gx + 5; x++)
                                if (y > horizon - gh + 1 || (x != gx && x != gx + 4)) Put(px, x, y, new Color(0.16f, 0.14f, 0.2f));
                    }
                    for (int y = horizon - 10; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                        {
                            float m = n.Fbm(x / 16f, y / 3f + 20f);
                            if (m > 0.5f) Put(px, x, y, new Color(0.55f, 0.6f, 0.7f), (m - 0.5f) * 0.9f);
                        }
                    break;
                }
                case 5:   // Myth: a golden sunburst
                {
                    float cx = ArtW / 2f, cy = ArtH * 0.55f;
                    for (int y = 0; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                        {
                            float a = Mathf.Atan2(y - cy, x - cx);
                            if (Mathf.Repeat(a * 12f / Mathf.PI, 2f) < 1f) Put(px, x, y, new Color(1f, 0.95f, 0.7f), 0.22f);
                        }
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + 6 + (int)((n.V(x / 10f, 2f) - 0.5f) * 4f);
                        for (int y = h; y < ArtH; y++) Put(px, x, y, Color.Lerp(ground, new Color(0.55f, 0.35f, 0.08f), ((x / 6 + y / 3) % 2) * 0.4f));
                    }
                    break;
                }
                case 6:   // Balance: a setting sun over dunes
                {
                    int sx = R(rng, 30, 87), sy = horizon - 6;
                    const float sr = 11f;
                    for (int y = 0; y < ArtH; y++)
                        for (int x = 0; x < ArtW; x++)
                        {
                            float d = Mathf.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
                            if (d < sr && (y % 3 != 0 || y < sy - 3)) Put(px, x, y, new Color(1f, 0.92f, 0.6f));
                            else if (d < sr + 10f) Put(px, x, y, new Color(1f, 0.85f, 0.55f), (1f - (d - sr) / 10f) * 0.35f);
                        }
                    float ph = (float)rng.NextDouble() * 6f;
                    for (int band = 0; band < 2; band++)
                    {
                        Color col = band == 0 ? new Color(0.78f, 0.48f, 0.22f) : new Color(0.62f, 0.36f, 0.16f);
                        for (int x = 0; x < ArtW; x++)
                        {
                            int h = horizon + band * 8 + (int)(Mathf.Sin(x / (16f - band * 4f) + band * 2f + ph) * 4f);
                            for (int y = h; y < ArtH; y++) Put(px, x, y, col);
                        }
                    }
                    break;
                }
                default:   // General: a starry dusk
                    for (int k = 0; k < 40; k++)
                        Put(px, R(rng, 0, ArtW), R(rng, 0, (int)(ArtH * 0.7f)), new Color(1f, 1f, 0.9f), (float)rng.NextDouble() * 0.8f + 0.2f);
                    for (int x = 0; x < ArtW; x++)
                    {
                        int h = horizon + 3 + (int)((n.V(x / 12f, 4f) - 0.5f) * 6f);
                        for (int y = h; y < ArtH; y++) Put(px, x, y, ground);
                    }
                    break;
            }
            return px;
        }

        // ------------------------------------------------------------------ light, emblems, motes, finish

        private static void ArtGlow(Color[] px, float cx, float cy, float r, Color c, float strength)
        {
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    float dx = (x - cx) / 1.25f, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d < 1f) Put(px, x, y, c, Mathf.Pow(1f - d, 1.6f) * strength);
                }
        }

        private static void ArtRays(Color[] px, float cx, float cy, Color c)
        {
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    float a = Mathf.Atan2(y - cy, x - cx);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d < 60f && Mathf.Repeat(a * 8f / Mathf.PI + 0.25f, 2f) < 0.6f) Put(px, x, y, c, 0.18f * (1f - d / 60f));
                }
        }

        /// <summary>One of the game's 64 x 64 effect icons (Textures/FxIcons/&lt;Kind&gt;&lt;School&gt;.png), top-down.</summary>
        private static Color32[] ArtIcon(string kind, int school)
        {
            string name = kind + (school >= 0 && school < SchoolLabels.Length ? SchoolLabels[school] : "General");
            Color32[] px;
            if (_artIcons.TryGetValue(name, out px)) return px;
            string path = FxIconDir + "/" + name + ".png";
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            if (tex.width != 64 || tex.height != 64) { Object.DestroyImmediate(tex); return null; }
            Color32[] raw = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++) px[y * 64 + x] = raw[(63 - y) * 64 + x];
            _artIcons[name] = px;
            return px;
        }

        /// <summary>Stamps an icon (size x size, top-down) with its top-left at (ox, oy): a soft drop shadow, then the icon. Step 2 = half size.</summary>
        private static void ArtStamp(Color[] px, Color32[] icon, int ox, int oy, int step, float shade)
        {
            int size = 64 / step;
            for (int pass = 0; pass < 2; pass++)
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Color32 c = icon[(y * step) * 64 + x * step];
                        if (c.a < 128) continue;
                        if (pass == 0) Put(px, ox + x + 1, oy + y + 1, ArtInk, 0.55f);
                        else Put(px, ox + x, oy + y, new Color(c.r / 255f * shade, c.g / 255f * shade, c.b / 255f * shade));
                    }
        }

        private static void ArtMotes(Color[] px, int school, System.Random rng)
        {
            Color part = ArtPalette[school, 4];
            bool plus = school == 1 || school == 5 || school == 7;
            for (int k = 0; k < 14; k++)
            {
                int x = R(rng, 0, ArtW), y = R(rng, 0, ArtH);
                if (plus)
                {
                    Put(px, x, y, part, 0.9f);
                    Put(px, x + 1, y, part, 0.5f); Put(px, x - 1, y, part, 0.5f);
                    Put(px, x, y + 1, part, 0.5f); Put(px, x, y - 1, part, 0.5f);
                }
                else
                {
                    Put(px, x, y, part, 0.9f);
                    if (school == 0 || school == 4) Put(px, x, y + 1, part, 0.45f);   // embers and wisps trail
                }
            }
        }

        /// <summary>Vignette, then 5 bits a channel with the PS1's 4 x 4 ordered dither; returned bottom-up for a Texture2D.</summary>
        private static Color[] ArtFinish(Color[] px)
        {
            var outPx = new Color[ArtW * ArtH];
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    Color c = px[y * ArtW + x];
                    float vx = (x - ArtW / 2f) / (ArtW / 2f), vy = (y - ArtH / 2f) / (ArtH / 2f);
                    float v = 1f - 0.32f * Clamp01(vx * vx + vy * vy - 0.35f);
                    float dither = (ArtBayer[(y % 4) * 4 + x % 4] / 16f - 0.5f) * 0.9f;
                    float r = Mathf.Round(Clamp01(c.r * v) * 31f + dither) / 31f;
                    float g = Mathf.Round(Clamp01(c.g * v) * 31f + dither) / 31f;
                    float b = Mathf.Round(Clamp01(c.b * v) * 31f + dither) / 31f;
                    outPx[(ArtH - 1 - y) * ArtW + x] = new Color(Clamp01(r), Clamp01(g), Clamp01(b), 1f);
                }
            return outPx;
        }

        /// <summary>
        /// Which emblem a non-attack spell's card shows: kind 0 shield, 1 blade, 2 trap, 3 the school's own symbol; its
        /// school (7 = General); and whether it's a three-way spell. False for spells shown by their animation.
        /// </summary>
        private static bool ArtEmblem(CardDatabase db, int i, out string kind, out int school, out bool tri)
        {
            int id = db.ids[i], e = db.effects[i], s = db.schools[i];
            kind = null;
            school = 7;
            tri = false;
            switch (e)
            {
                case CardDatabase.EFFECT_WARD: kind = "Shield"; school = db.ShieldSchool(id); break;
                case CardDatabase.EFFECT_MULTIWARD: kind = "Shield"; school = db.ShieldSchool(id); tri = true; break;
                case CardDatabase.EFFECT_TAUNT: kind = "Shield"; break;
                case CardDatabase.EFFECT_TRISHIELD: kind = "Shield"; tri = true; break;
                case CardDatabase.EFFECT_EMPOWER: kind = "Blade"; school = db.BladeSchool(id); break;
                case CardDatabase.EFFECT_TRIBLADE: kind = "Blade"; tri = true; break;
                case CardDatabase.EFFECT_HEALBLADE: kind = "Blade"; school = 3; break;
                case CardDatabase.EFFECT_TRAP: kind = "Trap"; school = db.TrapSchool(id); break;
                case CardDatabase.EFFECT_TRITRAP: kind = "Trap"; tri = true; break;
                case CardDatabase.EFFECT_CURSE: kind = "Trap"; break;
                case CardDatabase.EFFECT_FOCUS:
                case CardDatabase.EFFECT_DISPEL:
                case CardDatabase.EFFECT_STRIKEFX: kind = "School"; school = s; break;
            }
            if (school < 0 || school > 6) school = 7;
            return kind != null;
        }

        private static int ArtSchool(int s) { return s >= 0 && s <= 6 ? s : 7; }

        // ------------------------------------------------------------------ the subject: a render of the spell's animation

        /// <summary>
        /// Renders the spell's animation part (creature mid-cast, bolt, or area at its strike) on a transparent background,
        /// framed to fill the picture: top-down pixels, alpha = subject.
        /// </summary>
        private static Color[] RenderArtSubject(GameObject fx, SpellLook look)
        {
            const int layer = 31;
            var stage = new GameObject("CardArtStage").transform;
            stage.position = new Vector3(0f, -5000f, 0f);
            var copy = Object.Instantiate(fx, stage);
            copy.SetActive(true);
            copy.transform.localPosition = Vector3.zero;
            bool creature = look.mode == FxCreature, bolt = look.mode == FxBolt;
            // Creatures in three-quarter view turned towards the right of the picture; bolts streaking up to the right.
            // Attacking, as on the card it springs from: flyers pitched into their dive, beasts reared up mid-pounce, the rest
            // leaning into the blow.
            float pitch = 0f;
            if (creature && look.creature != null)
                pitch = look.creature.kind == RigKind.Floater ? 20f : look.creature.kind == RigKind.Quadruped ? -16f : 7f;
            copy.transform.localRotation = creature ? Quaternion.Euler(0f, 145f, 0f) * Quaternion.Euler(pitch, 0f, 0f)
                                         : bolt ? Quaternion.Euler(0f, 0f, 25f) * Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, 180f, 0f);
            Transform summon = copy.transform.Find("Summon"), proj = copy.transform.Find("Projectile"), area = copy.transform.Find("Area");
            if (summon != null) summon.gameObject.SetActive(creature);
            if (proj != null) proj.gameObject.SetActive(bolt);
            if (area != null) area.gameObject.SetActive(!creature && !bolt);
            // Pose it: the creature at the height of its cast, the area at its strike.
            foreach (var a in copy.GetComponentsInChildren<Animator>(true))
            {
                var ctrl = a.runtimeAnimatorController;
                if (ctrl == null || ctrl.animationClips.Length == 0) continue;
                AnimationClip c = ctrl.animationClips[0];
                foreach (var cc in ctrl.animationClips) if (cc.name == "Cast") c = cc;
                float t = a.transform == area ? Mathf.Min(look.strike + 0.15f, c.length) : c.name == "Cast" ? Mathf.Min(0.45f, c.length) : c.length * 0.3f;
                c.SampleAnimation(a.gameObject, t);
            }
            foreach (var ps in copy.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            var bounds = new Bounds(copy.transform.position + Vector3.up, Vector3.one * 0.5f);
            bool any = false;
            foreach (var r in copy.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }

            var camGo = new GameObject("CardArtCamera");
            camGo.transform.SetParent(stage, false);
            var cam = camGo.AddComponent<Camera>();
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.aspect = ArtW / (float)ArtH;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            var lightGo = new GameObject("CardArtLight");
            lightGo.transform.SetParent(stage, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, 30f, 0f);   // from the upper left, in front
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.cullingMask = 1 << layer;

            // Creatures from a little below (they loom), areas from in front and above, bolts side on.
            Vector3 dir = creature ? new Vector3(0f, -0.12f, 1f).normalized : bolt ? Vector3.forward : new Vector3(0f, -0.42f, 1f).normalized;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            float dist = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.4f;
            Vector3 aim = bounds.center;
            // How much of the picture it should fill, and where its middle goes (0-1 from the top left).
            float fill = creature ? 0.86f : bolt ? 0.42f : 0.8f;
            Vector2 at = creature ? new Vector2(0.5f, 0.54f) : bolt ? new Vector2(0.6f, 0.44f) : new Vector2(0.5f, 0.5f);

            var rt = RenderTexture.GetTemporary(ArtW, ArtH, 24, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            rt.antiAliasing = 1;
            Color[] result = null;
            for (int pass = 0; pass < 2; pass++)
            {
                cam.transform.position = aim - dir * dist;
                cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 3f);
                cam.farClipPlane = dist + radius * 3f;
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(ArtW, ArtH, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, ArtW, ArtH), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                Color[] raw = tex.GetPixels();
                Object.DestroyImmediate(tex);
                result = new Color[ArtW * ArtH];
                int x0 = ArtW, y0 = ArtH, x1 = -1, y1 = -1;
                for (int y = 0; y < ArtH; y++)
                    for (int x = 0; x < ArtW; x++)
                    {
                        Color c = raw[(ArtH - 1 - y) * ArtW + x];
                        result[y * ArtW + x] = c;
                        if (c.a < 0.5f) continue;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
                if (pass == 1 || x1 < 0) break;
                // Re-frame from the first render: centre it, zoom so it fills 'fill', then shift it to sit at 'at'.
                float w = (x1 - x0 + 1) / (float)ArtW, h = (y1 - y0 + 1) / (float)ArtH;
                float scale = fill / Mathf.Max(w, h);
                float mpp = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / ArtH;
                Vector3 right = cam.transform.right, up = cam.transform.up;
                Vector2 off = new Vector2((x0 + x1) / 2f - ArtW / 2f, (y0 + y1) / 2f - ArtH / 2f);
                aim += right * off.x * mpp - up * off.y * mpp;
                float mppNew = mpp / scale;
                Vector2 want = new Vector2((at.x - 0.5f) * ArtW, (at.y - 0.5f) * ArtH);
                aim += -right * want.x * mppNew + up * want.y * mppNew;
                dist /= scale;
            }
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(stage.gameObject);
            return result;
        }

        /// <summary>Lays a rendered subject over the backdrop: a glow behind it, a streak behind a bolt, a dark outline and a rim of light.</summary>
        private static bool ArtComposite(Color[] px, Color[] subject, int school, bool bolt, int streaks, Vector2 streakDir, System.Random rng)
        {
            if (subject == null) return false;
            var on = new bool[ArtW * ArtH];
            int x0 = ArtW, y0 = ArtH, x1 = -1, y1 = -1;
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                    if (subject[y * ArtW + x].a >= 0.5f)
                    {
                        on[y * ArtW + x] = true;
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
            if (x1 < 0) return false;
            Color light = ArtPalette[school, 3];
            float cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
            ArtGlow(px, cx, cy, Mathf.Max(x1 - x0, y1 - y0) * 0.75f + 8f, light, 0.6f);
            if (bolt)
            {
                // Its trail, back down to the lower left, thinning and fading.
                Vector2 back = new Vector2(-Mathf.Cos(25f * Mathf.Deg2Rad), Mathf.Sin(25f * Mathf.Deg2Rad));
                for (int k = 0; k < 46; k++)
                {
                    float f = k / 46f;
                    float half = Mathf.Lerp(Mathf.Max(x1 - x0, y1 - y0) * 0.3f, 0.5f, f);
                    Vector2 p = new Vector2(cx, cy) + back * k;
                    Vector2 nrm = new Vector2(-back.y, back.x);
                    for (float s = -half; s <= half; s += 0.5f)
                        Put(px, Mathf.RoundToInt(p.x + nrm.x * s), Mathf.RoundToInt(p.y + nrm.y * s), Color.Lerp(Color.white, light, Mathf.Abs(s) / Mathf.Max(half, 0.5f)), (1f - f) * 0.75f);
                }
            }
            // Speed streaks trailing off its back edge (creatures caught mid-attack).
            for (int k = 0; k < streaks; k++)
            {
                int sy = y0 + Mathf.RoundToInt((y1 - y0) * (k + 0.5f) / streaks) + rng.Next(-2, 3);
                if (sy < 0 || sy >= ArtH) continue;
                int sx = -1;
                for (int x = 0; x < ArtW; x++) if (on[sy * ArtW + x]) { sx = x; break; }
                if (sx < 0) continue;
                int length = rng.Next(10, 27);
                for (int j = 2; j < length; j++)
                {
                    float f = j / (float)length;
                    Put(px, Mathf.RoundToInt(sx + streakDir.x * j), Mathf.RoundToInt(sy + streakDir.y * j), Color.Lerp(Color.white, light, f), (1f - f) * 0.7f);
                }
            }
            for (int y = 0; y < ArtH; y++)
                for (int x = 0; x < ArtW; x++)
                {
                    int i = y * ArtW + x;
                    bool l = x > 0 && on[i - 1], r = x < ArtW - 1 && on[i + 1], u = y > 0 && on[i - ArtW], d = y < ArtH - 1 && on[i + ArtW];
                    if (!on[i])
                    {
                        if (l || r || u || d) px[i] = ArtInk;   // the outline
                        continue;
                    }
                    Color c = subject[i];
                    c.a = 1f;
                    // A rim of the school's light along its upper and right edges.
                    bool rim = (y > 0 && !u) || (x < ArtW - 1 && !r);
                    px[i] = rim ? Color.Lerp(c, light, 0.55f) : c;
                }
            return true;
        }

        // ------------------------------------------------------------------ one card

        /// <summary>Paints card i's picture (see the class summary) and saves it as Textures/CardArt/&lt;id&gt;.png.</summary>
        private static Texture PaintCardArt(GameObject fx, CardDatabase db, int i, SpellLook look)
        {
            int id = db.ids[i];
            int school = ArtSchool(db.schools[i]);
            var rng = new System.Random(id * 7919 + 17);
            Color[] px = PaintBackdrop(school, rng);
            Color light = ArtPalette[school, 3];
            string kind;
            int emblemSchool;
            bool tri;
            bool drawn = false;
            if (ArtEmblem(db, i, out kind, out emblemSchool, out tri))
            {
                Color32[] icon = ArtIcon(kind, emblemSchool);
                if (icon != null)
                {
                    int cx = ArtW / 2, cy = ArtH / 2;
                    ArtRays(px, cx, cy, light);
                    ArtGlow(px, cx, cy, 38f, light, 0.75f);
                    if (tri)
                    {
                        ArtStamp(px, icon, cx - 16 - 30, cy - 16 + 6, 2, 0.8f);
                        ArtStamp(px, icon, cx - 16 + 30, cy - 16 + 6, 2, 0.8f);
                    }
                    ArtStamp(px, icon, cx - 32, cy - 32, 1, 1f);
                    drawn = true;
                }
            }
            if (!drawn && look.mode != FxLegacy)
            {
                bool flyer = look.mode == FxCreature && look.creature != null && look.creature.kind == RigKind.Floater;
                int streaks = look.mode == FxCreature ? 6 : 0;
                Vector2 back = flyer ? new Vector2(-0.86f, -0.5f) : new Vector2(-1f, 0f);   // flyers dive in from the upper left
                drawn = ArtComposite(px, RenderArtSubject(fx, look), school, look.mode == FxBolt, streaks, back, rng);
            }
            if (!drawn)
            {
                // Nothing to show: the school's own symbol.
                Color32[] icon = ArtIcon("School", school);
                if (icon != null) { ArtGlow(px, ArtW / 2f, ArtH / 2f, 38f, light, 0.75f); ArtStamp(px, icon, ArtW / 2 - 32, ArtH / 2 - 32, 1, 1f); }
            }
            ArtMotes(px, school, rng);

            var tex = new Texture2D(ArtW, ArtH, TextureFormat.RGBA32, false);
            tex.SetPixels(ArtFinish(px));
            tex.Apply();
            Directory.CreateDirectory(CardArtDir);
            string path = CardArtDir + "/" + id + ".png";
            // Only written (and re-imported) when the picture changed.
            WritePngIfChanged(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                                     importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;   // 116 x 76 stays 116 x 76 (no stretching to a power of two)
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
