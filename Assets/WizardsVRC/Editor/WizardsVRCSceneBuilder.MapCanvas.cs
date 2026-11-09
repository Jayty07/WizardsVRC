using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// A small software canvas for drawing the maps at build time (WizardsVRCSceneBuilder.Maps.cs): filled polygons,
    /// thick lines, ellipses, alpha blending, blits, a box blur, supersampled drawing shrunk down, and PNG in and out.
    /// Pixel (0,0) is the top-left corner and y grows downwards, like the map art; Unity's bottom-up rows are flipped on
    /// the way in and out.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class MapCanvas
        {
            public readonly int w, h;
            public readonly Color32[] px;
            private readonly List<float> _xs = new List<float>(16);

            public MapCanvas(int w, int h)
            {
                this.w = w;
                this.h = h;
                px = new Color32[w * h];
            }

            /// <summary>Paints c over the pixel (source-over alpha).</summary>
            public void Blend(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || c.a == 0) return;
                int i = y * w + x;
                if (c.a == 255) { px[i] = c; return; }
                Color32 d = px[i];
                float sa = c.a / 255f, da = d.a / 255f;
                float oa = sa + da * (1f - sa);
                if (oa <= 0.0001f) return;
                float k = da * (1f - sa);
                px[i] = new Color32(
                    (byte)Mathf.Clamp((c.r * sa + d.r * k) / oa, 0f, 255f),
                    (byte)Mathf.Clamp((c.g * sa + d.g * k) / oa, 0f, 255f),
                    (byte)Mathf.Clamp((c.b * sa + d.b * k) / oa, 0f, 255f),
                    (byte)Mathf.Clamp(oa * 255f, 0f, 255f));
            }

            /// <summary>A filled polygon (any simple outline, even-odd), sampled at pixel centres.</summary>
            public void FillPoly(Vector2[] p, Color32 c)
            {
                int n = p.Length;
                if (n < 3) return;
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < n; i++) { minY = Mathf.Min(minY, p[i].y); maxY = Mathf.Max(maxY, p[i].y); }
                int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY));
                for (int y = y0; y <= y1; y++)
                {
                    float sy = y + 0.5f;
                    _xs.Clear();
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 a = p[i], b = p[(i + 1) % n];
                        if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                            _xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                    if (_xs.Count < 2) continue;
                    _xs.Sort();
                    for (int k = 0; k + 1 < _xs.Count; k += 2)
                    {
                        int xa = Mathf.Max(0, Mathf.CeilToInt(_xs[k] - 0.5f));
                        int xb = Mathf.Min(w - 1, Mathf.FloorToInt(_xs[k + 1] - 0.5f));
                        for (int x = xa; x <= xb; x++) Blend(x, y, c);
                    }
                }
            }

            /// <summary>A line 'width' thick (round ends when it's thick enough to show them).</summary>
            public void Line(Vector2 a, Vector2 b, float width, Color32 c)
            {
                Vector2 d = b - a;
                float len = d.magnitude, r = width * 0.5f;
                if (len < 0.001f) { FillEllipse(a, r, r, c); return; }
                Vector2 nrm = new Vector2(-d.y, d.x) / len * r;
                FillPoly(new[] { a + nrm, b + nrm, b - nrm, a - nrm }, c);
                if (width >= 2.5f) { FillEllipse(a, r, r, c); FillEllipse(b, r, r, c); }
            }

            public void Polyline(Vector2[] pts, float width, Color32 c, bool closed)
            {
                for (int i = 0; i + 1 < pts.Length; i++) Line(pts[i], pts[i + 1], width, c);
                if (closed && pts.Length > 2) Line(pts[pts.Length - 1], pts[0], width, c);
            }

            /// <summary>A filled polygon with an outline round it (no outline when width is 0).</summary>
            public void Poly(Vector2[] p, Color32 fill, Color32 outline, float width)
            {
                FillPoly(p, fill);
                if (width > 0f) Polyline(p, width, outline, true);
            }

            public void FillEllipse(Vector2 c, float rx, float ry, Color32 col)
            {
                if (rx <= 0f || ry <= 0f) return;
                int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - ry)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(c.y + ry));
                for (int y = y0; y <= y1; y++)
                {
                    float dy = (y + 0.5f - c.y) / ry;
                    if (dy * dy > 1f) continue;
                    float hw = rx * Mathf.Sqrt(1f - dy * dy);
                    int xa = Mathf.Max(0, Mathf.CeilToInt(c.x - hw - 0.5f)), xb = Mathf.Min(w - 1, Mathf.FloorToInt(c.x + hw - 0.5f));
                    for (int x = xa; x <= xb; x++) Blend(x, y, col);
                }
            }

            public void StrokeEllipse(Vector2 c, float rx, float ry, float width, Color32 col)
            {
                const int n = 36;
                var pts = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                }
                Polyline(pts, width, col, true);
            }

            /// <summary>Paints another canvas over this one with its top-left corner at (dx, dy), its alpha times 'alpha'.</summary>
            public void Blit(MapCanvas src, int dx, int dy, float alpha = 1f)
            {
                for (int y = 0; y < src.h; y++)
                {
                    int ty = y + dy;
                    if (ty < 0 || ty >= h) continue;
                    for (int x = 0; x < src.w; x++)
                    {
                        int tx = x + dx;
                        if (tx < 0 || tx >= w) continue;
                        Color32 c = src.px[y * src.w + x];
                        if (c.a == 0) continue;
                        if (alpha < 1f) c.a = (byte)(c.a * alpha);
                        Blend(tx, ty, c);
                    }
                }
            }

            /// <summary>One colour wherever 'mask' (0..1 per pixel, this canvas's size, shifted by dx, dy) is set.</summary>
            public void Wash(float[] mask, int dx, int dy, Color32 col)
            {
                for (int y = 0; y < h; y++)
                {
                    int sy = y - dy;
                    if (sy < 0 || sy >= h) continue;
                    for (int x = 0; x < w; x++)
                    {
                        int sx = x - dx;
                        if (sx < 0 || sx >= w) continue;
                        float m = mask[sy * w + sx];
                        if (m <= 0.002f) continue;
                        Blend(x, y, new Color32(col.r, col.g, col.b, (byte)Mathf.Clamp(col.a * m, 0f, 255f)));
                    }
                }
            }

            /// <summary>Where this canvas is drawn on (alpha over 'threshold'), as 0 or 1 per pixel.</summary>
            public float[] Mask(byte threshold)
            {
                var m = new float[w * h];
                for (int i = 0; i < m.Length; i++) m[i] = px[i].a > threshold ? 1f : 0f;
                return m;
            }

            /// <summary>Shrinks by 'f' (averaging each f x f block, colours weighted by alpha): smooth edges from a big drawing.</summary>
            public MapCanvas Downsample(int f)
            {
                var o = new MapCanvas(w / f, h / f);
                float inv = 1f / (f * f);
                for (int y = 0; y < o.h; y++)
                    for (int x = 0; x < o.w; x++)
                    {
                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        for (int j = 0; j < f; j++)
                        {
                            int row = (y * f + j) * w + x * f;
                            for (int i = 0; i < f; i++)
                            {
                                Color32 c = px[row + i];
                                float ca = c.a / 255f;
                                r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
                            }
                        }
                        if (a <= 0.0001f) continue;
                        o.px[y * o.w + x] = new Color32((byte)Mathf.Clamp(r / a, 0f, 255f), (byte)Mathf.Clamp(g / a, 0f, 255f),
                            (byte)Mathf.Clamp(b / a, 0f, 255f), (byte)Mathf.Clamp(a * inv * 255f, 0f, 255f));
                    }
                return o;
            }
        }

        // ------------------------------------------------------------------ masks: grow and blur

        /// <summary>Grows a 0..1 mask by r pixels (a square max filter, done along rows then columns).</summary>
        private static float[] Dilate(float[] m, int w, int h, int r)
        {
            var t = new float[m.Length];
            var o = new float[m.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    for (int k = -r; k <= r && v < 1f; k++)
                    {
                        int xx = x + k;
                        if (xx >= 0 && xx < w) v = Mathf.Max(v, m[y * w + xx]);
                    }
                    t[y * w + x] = v;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 0f;
                    for (int k = -r; k <= r && v < 1f; k++)
                    {
                        int yy = y + k;
                        if (yy >= 0 && yy < h) v = Mathf.Max(v, t[yy * w + x]);
                    }
                    o[y * w + x] = v;
                }
            return o;
        }

        /// <summary>A soft blur of a mask: three box blurs of radius r along rows and columns.</summary>
        private static float[] Blur(float[] m, int w, int h, int r)
        {
            var a = (float[])m.Clone();
            var b = new float[m.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    float sum = 0f;
                    int row = y * w;
                    for (int x = -r; x <= r; x++) sum += a[row + Mathf.Clamp(x, 0, w - 1)];
                    for (int x = 0; x < w; x++)
                    {
                        b[row + x] = sum / (2 * r + 1);
                        sum += a[row + Mathf.Min(w - 1, x + r + 1)] - a[row + Mathf.Max(0, x - r)];
                    }
                }
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int y = -r; y <= r; y++) sum += b[Mathf.Clamp(y, 0, h - 1) * w + x];
                    for (int y = 0; y < h; y++)
                    {
                        a[y * w + x] = sum / (2 * r + 1);
                        sum += b[Mathf.Min(h - 1, y + r + 1) * w + x] - b[Mathf.Max(0, y - r) * w + x];
                    }
                }
            }
            return a;
        }

        // ------------------------------------------------------------------ PNGs in and out

        /// <summary>A PNG from disk as a top-down canvas (null when it's missing).</summary>
        private static MapCanvas LoadMapPng(string assetPath)
        {
            if (!File.Exists(assetPath)) { Debug.LogWarning("[WizardsVRC] Missing " + assetPath + " (run Tools/make_map_art.py)."); return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(assetPath))) { Object.DestroyImmediate(tex); return null; }
            var c = new MapCanvas(tex.width, tex.height);
            Color32[] src = tex.GetPixels32();
            for (int y = 0; y < tex.height; y++)
                System.Array.Copy(src, (tex.height - 1 - y) * tex.width, c.px, y * tex.width, tex.width);
            Object.DestroyImmediate(tex);
            return c;
        }

        /// <summary>Writes a canvas as a PNG asset and imports it for the book (mipmapped, compressed, no wrapping).</summary>
        private static Texture2D SaveMapPng(MapCanvas c, string assetPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var tex = new Texture2D(c.w, c.h, TextureFormat.RGBA32, false);
            var flipped = new Color32[c.px.Length];
            for (int y = 0; y < c.h; y++)
                System.Array.Copy(c.px, y * c.w, flipped, (c.h - 1 - y) * c.w, c.w);
            tex.SetPixels32(flipped);
            tex.Apply();
            // Only written and re-imported when the map changed (compressing a big map is slow).
            WritePngIfChanged(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Default || !imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp ||
                                imp.filterMode != FilterMode.Trilinear || !imp.mipmapEnabled || imp.maxTextureSize != 2048 ||
                                imp.textureCompression != TextureImporterCompression.CompressedHQ))
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Trilinear;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
