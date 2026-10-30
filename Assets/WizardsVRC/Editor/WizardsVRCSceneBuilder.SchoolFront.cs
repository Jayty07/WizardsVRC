using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The front of a school's hall: a stepped porch, two columns carrying a school-coloured entablature and a stone gable
    /// with the school's symbol, framed and silled windows, wall lanterns, carved bands along the base and under the
    /// eaves, and the school's banners on the front and both side walls. No lettering; the symbol and colours say which
    /// school it is. Built round HubBuilding's facade ('face' is the middle of the front wall at the ground).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static void SchoolFront(Transform root, ZoneGrid g, ZoneLandmark l, int s, Vector3 face, Vector3 front, float across, float hallH,
            Material wall, Material gold, Material bright)
        {
            string sn = SchoolLabels[s];
            Color col = SchoolColors[s];
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Quaternion rot = Quaternion.LookRotation(front);
            Vector3 up = Vector3.up;
            const float doorH = 5.2f;
            float gapW = doorH * 0.7f + 0.6f;
            Material trim = PsxTexMat("SchoolTrim" + sn, Color.Lerp(new Color(0.86f, 0.82f, 0.78f), col, 0.3f), TileTexture(TerrainTexDir + "/StoneWall.png"), 1.6f);
            Material band = PMat("SchoolBand" + sn, Color.Lerp(col, Color.black, 0.25f), 1f);
            Material banner = ClassMat("ClassBanner" + sn, "Banner" + sn + ".png", Vector2.one, 0.5f);
            Material symbol = PMat("SchoolSymbol" + sn, Color.white, 1f, new Color(0.25f, 0.25f, 0.25f));
            Texture2D glyph = TileTexture(TexturesDir + "/CastGlyph" + sn + ".png");
            if (glyph != null) symbol.mainTexture = glyph;
            symbol.mainTextureScale = Vector2.one;
            symbol.SetFloat("_WorldUV", 0f);
            symbol.SetFloat("_Affine", 0.05f);
            symbol.SetFloat("_Cutoff", 0.5f);
            symbol.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            // ---- porch: two shallow steps up to the door
            Block(root, "PorchStepLow", face + front * 1.4f + up * 0.03f, new Vector3(gapW + 4.2f, 0.06f, 2.8f), trim, rot);
            Block(root, "PorchStep", face + front * 1.1f + up * 0.06f, new Vector3(gapW + 3.2f, 0.12f, 2.2f), trim, rot);

            // ---- columns, entablature and the gable with the school's symbol
            foreach (float sg in new[] { -1f, 1f })
            {
                Vector3 p = face + front * 0.9f + side * (sg * (gapW / 2f + 0.75f));
                Block(root, "ColumnBase", p + up * 0.27f, new Vector3(0.9f, 0.3f, 0.9f), trim, rot);
                Shape(root, "Column", CylMesh(), p + up * 3.21f, new Vector3(0.62f, 2.79f, 0.62f), trim, true, Quaternion.identity);
                Block(root, "ColumnCapital", p + up * 6.2f, new Vector3(0.95f, 0.4f, 0.95f), gold, rot);
            }
            float span = gapW + 3.2f;
            Block(root, "Entablature", face + front * 0.7f + up * 6.7f, new Vector3(span, 0.6f, 1.4f), band, rot);
            Block(root, "EntablatureGold", face + front * 0.72f + up * 7.06f, new Vector3(span + 0.1f, 0.12f, 1.46f), gold, rot);
            float gableH = 2.3f;
            var poly = new[] { new Vector2(-span / 2f, 0f), new Vector2(span / 2f, 0f), new Vector2(0f, gableH) };
            Mesh gable = PrismMesh("PSX_SchoolGable_" + Mathf.RoundToInt(span * 10f), poly, 0f, 1.25f);
            // The prism stands up (its polygon in the facade's plane) and runs back from the porch's front to the wall.
            Shape(root, "Gable", gable, face + front * 1.3f + up * 7.12f, Vector3.one, trim, false, rot * Quaternion.Euler(-90f, 0f, 0f));
            var medallion = GameObject.CreatePrimitive(PrimitiveType.Quad);
            medallion.name = "SchoolSymbol";
            medallion.transform.SetParent(root, false);
            medallion.transform.localPosition = face + front * 1.42f + up * (7.12f + 0.85f);
            medallion.transform.localRotation = Quaternion.LookRotation(-front);   // a quad shows its face to -Z
            medallion.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            medallion.GetComponent<Renderer>().sharedMaterial = symbol;
            Object.DestroyImmediate(medallion.GetComponent<Collider>());
            Shape(root, "GableFinial", GemMesh(), face + front * 0.7f + up * (7.12f + gableH + 0.35f), new Vector3(0.45f, 0.7f, 0.45f), bright, false, Quaternion.identity);

            // ---- carved bands: a plinth along the base (either side of the door) and a coloured band under the eaves
            float edge = across / 2f - 1.4f;
            foreach (float sg in new[] { -1f, 1f })
            {
                float x0 = gapW / 2f, x1 = edge;
                if (x1 - x0 > 0.2f)
                    Block(root, "Plinth", face + front * 0.1f + side * (sg * (x0 + x1) / 2f) + up * 0.4f, new Vector3(x1 - x0, 0.8f, 0.2f), trim, rot);
            }
            Block(root, "EavesBand", face + front * 0.12f + up * (hallH - 1.1f), new Vector3(across - 2.6f, 0.4f, 0.24f), band, rot);
            Block(root, "EavesGold", face + front * 0.14f + up * (hallH - 1.35f), new Vector3(across - 2.6f, 0.1f, 0.28f), gold, rot);

            // ---- windows up top with stone frames and sills (clear of the gable and the banners)
            float bx = Mathf.Min(gapW / 2f + 3.3f, across / 2f - 2.3f);
            int n = Mathf.Clamp(Mathf.FloorToInt(across / 4f), 1, 6);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1) - 0.5f) * (across - 5f);
                if (Mathf.Abs(t) < span / 2f + 0.9f || Mathf.Abs(Mathf.Abs(t) - bx) < 1.7f) continue;
                Vector3 w = face + side * t + up * 6.6f;
                Block(root, "Window", w + front * 0.06f, new Vector3(1.2f, 2f, 0.1f), bright, rot);
                Block(root, "WindowHead", w + front * 0.1f + up * 1.12f, new Vector3(1.6f, 0.25f, 0.2f), trim, rot);
                Block(root, "WindowSill", w + front * 0.17f - up * 1.08f, new Vector3(1.7f, 0.15f, 0.34f), trim, rot);
                foreach (float sg in new[] { -1f, 1f })
                    Block(root, "WindowJamb", w + front * 0.1f + side * (sg * 0.69f), new Vector3(0.18f, 2.2f, 0.2f), trim, rot);
            }

            // ---- wall lanterns either side of the door
            foreach (float sg in new[] { -1f, 1f })
            {
                Vector3 p = face + side * (sg * (gapW / 2f + 1.75f)) + up * 3.5f;
                Block(root, "LanternBracket", p + front * 0.3f, new Vector3(0.12f, 0.12f, 0.6f), gold, rot);
                Block(root, "Lantern", p + front * 0.62f - up * 0.35f, new Vector3(0.4f, 0.55f, 0.4f), bright, rot);
                Block(root, "LanternCap", p + front * 0.62f - up * 0.02f, new Vector3(0.52f, 0.1f, 0.52f), gold, rot);
            }

            // ---- banners: either side of the porch on the front, and one on each side wall
            foreach (float sg in new[] { -1f, 1f })
                OutsideBanner(root, face + front * 0.1f + side * (sg * bx) + up * 4.6f, front, banner, gold);
            Vector3 c, f2;
            float a2, depth;
            BuildingFrame(g, l, HallAspect, out c, out f2, out a2, out depth);
            foreach (float sg in new[] { -1f, 1f })
            {
                Vector3 outward = side * sg;
                OutsideBanner(root, c + outward * (across / 2f + 0.1f) + up * 5.2f, outward, banner, gold);
            }
        }

        /// <summary>A school banner hanging flat on a wall that faces 'outward', on a gold rod with knobs.</summary>
        private static void OutsideBanner(Transform root, Vector3 center, Vector3 outward, Material banner, Material gold)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "SchoolBanner";
            q.transform.SetParent(root, false);
            q.transform.localPosition = center;
            q.transform.localRotation = Quaternion.LookRotation(-outward);          // a quad shows its face to -Z
            q.transform.localScale = new Vector3(1.6f, 3.2f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = banner;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 rodAt = center + Vector3.up * 1.68f + outward * 0.05f;
            Shape(root, "BannerRod", CylMesh(), rodAt, new Vector3(0.08f, 0.95f, 0.08f), gold, false, Quaternion.LookRotation(outward) * Quaternion.Euler(0, 0, 90f));
            foreach (float e in new[] { -1f, 1f })
                Shape(root, "BannerKnob", GemMesh(), rodAt + along * (e * 1f), new Vector3(0.2f, 0.25f, 0.2f), gold, false, Quaternion.identity);
        }
    }
}
