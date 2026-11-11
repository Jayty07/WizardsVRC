using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// World 1's sun and solar winds. Every World 1 area's skybox (Shaders/PSXSkyline.shader, PSX_SUN keyword) gets a
    /// great stylized pixel-art sun and winds of light streaming from it across the whole sky (what they are made of is
    /// set out at the top of the shader). Here the builder switches them on for World 1's open skies and gives each
    /// area's sun its own light:
    ///   - where it stands: the bearing the scene's light really comes from (so the lit sides of walls face it), 30
    ///     degrees up: lower than the light, where it can be seen. The same place in every World 1 sky.
    ///   - its colours: the sun's own (white-hot, gold, deep orange) and the winds' (gold, rose, aqua), each leaning
    ///     towards the colour of that area's horizon, and washed out under an overcast sky. So it is a peach sunset sun
    ///     over the castle, an ember over Kiln Street, pale silver through the grey at the docks, green-gold over the
    ///     grove and lilac over the Hollow Keep. A dim sky veils it a little.
    ///   - how much of it: the zone's Sun setting (Zone > Sky; 0 = no sun or winds there, 1 = all of it).
    /// Tools/preview_sun_sky.py draws the same thing in numpy (every area's colours included), for trying changes
    /// without Unity.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>Degrees above the horizon, and how big it is (1 = a disc 5 degrees across, its rays reaching 9 degrees out from its middle).</summary>
        private const float SunElevation = 30f, SunSize = 1.5f;
        /// <summary>Where it stands if the scene has no light to go by (degrees clockwise from north).</summary>
        private const float SunBearingDefault = 150f;
        /// <summary>The sky's animation runs in steps, like a sprite: this many a second (0 = smooth).</summary>
        private const float SunSkyFps = 12f;

        private static readonly Color SunCore = new Color(1f, 0.98f, 0.84f), SunGold = new Color(1f, 0.82f, 0.34f), SunDeep = new Color(1f, 0.52f, 0.24f);
        private static readonly Color SunWindA = new Color(1f, 0.88f, 0.52f), SunWindB = new Color(1f, 0.62f, 0.78f), SunWindC = new Color(0.62f, 0.93f, 1f);

        /// <summary>The bearing the scene's directional light comes from (degrees clockwise from north).</summary>
        private static float SunBearing()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                Vector3 to = -light.transform.forward;
                if (Mathf.Abs(to.x) + Mathf.Abs(to.z) < 0.01f) break;      // straight overhead: no bearing to take
                return Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            }
            return SunBearingDefault;
        }

        private static float SunSmooth(float a, float b, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        }

        /// <summary>A sun or wind colour leaning 'lean' of the way to the horizon's hue, and half washed out under a grey sky.</summary>
        private static Color SunLean(Color c, Color tint, float lean, float greySky)
        {
            c = Color.Lerp(c, c * tint, lean);
            float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            c = Color.Lerp(c, new Color(g, g, g), 0.5f * greySky);
            c.a = 1f;
            return c;
        }

        /// <summary>Switches an area's sun and winds on (World 1, under an open sky) or off, and gives them that area's light.</summary>
        private static void SetSkySun(Material mat, SkyArea a, bool cave)
        {
            float amount = a.zone != null ? Mathf.Clamp01(a.zone.sunStrength) : 0f;
            if (a.world != 1 || cave || amount <= 0.01f)
            {
                mat.DisableKeyword("PSX_SUN");
                return;
            }
            mat.EnableKeyword("PSX_SUN");
            float b = SunBearing() * Mathf.Deg2Rad, e = SunElevation * Mathf.Deg2Rad;
            mat.SetVector("_SunDir", new Vector4(Mathf.Sin(b) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(b) * Mathf.Cos(e), 0f));
            mat.SetFloat("_SunSize", SunSize);
            mat.SetFloat("_SkyPixels", SkyW / 360f);       // the strip's own pixels, so the whole sky is one size of pixel
            mat.SetFloat("_SkyFps", SunSkyFps);

            Color horizon = a.zone.skyHorizonColor;
            float lum = horizon.r * 0.3f + horizon.g * 0.59f + horizon.b * 0.11f;
            float mx = Mathf.Max(horizon.r, Mathf.Max(horizon.g, horizon.b)), mn = Mathf.Min(horizon.r, Mathf.Min(horizon.g, horizon.b));
            Color tint = horizon / Mathf.Max(mx, 0.01f);
            float greySky = 1f - SunSmooth(0.05f, 0.3f, (mx - mn) / Mathf.Max(mx, 0.01f));
            float bright = SunSmooth(0.35f, 0.8f, lum);
            mat.SetColor("_SunCore", SunLean(SunCore, tint, 0.3f, greySky));
            mat.SetColor("_SunGold", SunLean(SunGold, tint, 0.45f, greySky));
            mat.SetColor("_SunDeep", SunLean(SunDeep, tint, 0.45f, greySky));
            mat.SetColor("_WindA", SunLean(SunWindA, tint, 0.35f, greySky));
            mat.SetColor("_WindB", SunLean(SunWindB, tint, 0.35f, greySky));
            mat.SetColor("_WindC", SunLean(SunWindC, tint, 0.2f, greySky));
            mat.SetFloat("_SunPower", Mathf.Lerp(0.78f, 1f, bright) * Mathf.Lerp(0.5f, 1f, amount));
            mat.SetFloat("_WindPower", Mathf.Lerp(0.85f, 1f, bright) * amount);
        }
    }
}
