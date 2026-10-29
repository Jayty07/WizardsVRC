using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The PSX pixel font (the NPC name tags' glyphs) as a real font for UI text: Fonts/PsxPixel.ttf, made from
    /// Fonts/PsxNameFont.txt by Tools/make_pixel_font.py. Imported unsmoothed (hard pixel edges) and drawn at a high
    /// resolution on world-space canvases, so the pixels stay crisp at any size.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string PixelFontPath = "Assets/WizardsVRC/Fonts/PsxPixel.ttf";
        /// <summary>World-space canvases rasterise text at this many pixels per unit (higher = crisper pixel font).</summary>
        private const float PixelFontDensity = 4f;

        private static Font PixelFont()
        {
            var importer = AssetImporter.GetAtPath(PixelFontPath) as TrueTypeFontImporter;
            if (importer == null)
            {
                Debug.LogWarning("[WizardsVRC] " + PixelFontPath + " is missing (run Tools/make_pixel_font.py); combat text keeps the default font.");
                return null;
            }
            if (importer.fontRenderingMode != FontRenderingMode.HintedRaster || importer.fontTextureCase != FontTextureCase.Dynamic)
            {
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.includeFontData = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Font>(PixelFontPath);
        }

        /// <summary>Every Text under root in the pixel font, on canvases that draw it crisply.</summary>
        private static void ApplyCombatFont(Transform root)
        {
            Font font = PixelFont();
            if (font == null) return;
            foreach (var t in root.GetComponentsInChildren<Text>(true)) t.font = font;
            foreach (var c in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.WorldSpace) continue;
                var scaler = c.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = c.gameObject.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = Mathf.Max(scaler.dynamicPixelsPerUnit, PixelFontDensity);
            }
        }
    }
}
