using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Signs: every name over a building, a shop, a tunnel mouth or a door is lettered in the pixel font on a carved
    /// wooden board with a gold border and corner studs (Textures/UI/SignBoard.png, 9-sliced), backed by a real board
    /// with gold finials so it reads as a sign from any side. Each street also gets a welcome arch over its way in.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>
        /// Puts a world-text label on a sign board. The board is sized to the text (pixels on the label's canvas, wPx 0 =
        /// guess from the text), drawn on the canvas behind the letters, with a wooden slab and gold finials behind it.
        /// </summary>
        private static void DressSign(Text label, float wPx, float hPx, bool finials = true)
        {
            if (label == null) return;
            var canvas = (RectTransform)label.transform.parent;
            if (canvas == null) return;
            Font pixel = PixelFont();
            if (pixel != null) label.font = pixel;
            if (wPx <= 0f)
            {
                // The pixel font's letters are about 0.62 em wide; take the longest line.
                int longest = 0;
                foreach (string line in (label.text ?? "").Split('\n')) longest = Mathf.Max(longest, line.Length);
                wPx = Mathf.Max(longest * label.fontSize * 0.62f + label.fontSize * 1.6f, label.fontSize * 4f);
            }
            wPx = Mathf.Min(wPx, canvas.sizeDelta.x);
            hPx = Mathf.Min(hPx, canvas.sizeDelta.y);
            float scale = canvas.localScale.x;
            // On the canvas, behind the letters: the carved board, its pixels about 1.5 cm big in the world.
            Image board = UiImage(canvas, "SignBoard", Vector2.zero, new Vector2(wPx, hPx), "SignBoard", Color.white, Mathf.Max(1f, 0.015f / scale));
            board.raycastTarget = false;
            board.transform.SetSiblingIndex(0);
            label.color = new Color(1f, 0.88f, 0.5f);
            var outline = label.GetComponent<Outline>();
            if (outline == null) outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.03f, 1f);
            var cs = canvas.GetComponent<CanvasScaler>();
            if (cs == null) cs = canvas.gameObject.AddComponent<CanvasScaler>();
            cs.dynamicPixelsPerUnit = Mathf.Max(cs.dynamicPixelsPerUnit, PixelFontDensity);

            // Behind it (canvas +Z is the way the reader looks): a wooden slab a little bigger than the board, so the
            // sign has a back and an edge, and gold finials at its ends and a gem on top.
            Transform parent = canvas.parent;
            if (parent == null) return;
            float w = wPx * scale, h = hPx * scale;
            Quaternion rot = canvas.localRotation;
            Vector3 c = canvas.localPosition;
            var slabMat = PsxTexMat("SignSlab", new Color(0.42f, 0.27f, 0.16f), TileTexture(StreetTexDir + "/Boards.png"), 1f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            Deco(parent, PrimitiveType.Cube, "SignSlab", c + rot * new Vector3(0, 0, 0.07f), new Vector3(w + 0.12f, h + 0.12f, 0.1f), slabMat, rot, false).isStatic = true;
            if (!finials) return;
            foreach (float sx in new[] { -1f, 1f })
                Shape(parent, "SignFinial", GemMesh(), c + rot * new Vector3(sx * (w / 2f + 0.16f), 0, 0.07f), new Vector3(0.22f, 0.34f, 0.22f), gold, false, rot);
            Shape(parent, "SignCrest", GemMesh(), c + rot * new Vector3(0, h / 2f + 0.16f, 0.07f), new Vector3(0.26f, 0.3f, 0.12f), gold, false, rot);
        }

        /// <summary>
        /// A welcome arch over a street's way in: two carved posts with lanterns, a beam across, and the street's name
        /// and level range on a board hanging from it, readable as you walk in (facing 'inward').
        /// </summary>
        private static void StreetArch(Transform root, Vector3 at, Vector3 inward, string name, string levels)
        {
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.01f) return;
            inward.Normalize();
            Quaternion face = Quaternion.LookRotation(inward);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            var post = PsxTexMat("ArchPost", new Color(0.4f, 0.26f, 0.15f), TileTexture(StreetTexDir + "/Bark.png"), 1f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            const float half = 3.3f, top = 6.2f;
            var arch = Child(root, "StreetArch", at);
            // Nothing here is solid: the arch never blocks the way, even where the street is narrow.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 p = side * (s * half);
                Deco(arch, PrimitiveType.Cube, "ArchPost", p + new Vector3(0, top / 2f, 0), new Vector3(0.45f, top, 0.45f), post, face, false);
                Deco(arch, PrimitiveType.Cube, "ArchPostCap", p + new Vector3(0, top + 0.12f, 0), new Vector3(0.62f, 0.24f, 0.62f), gold, face, false);
                Shape(arch, "ArchFinial", GemMesh(), p + new Vector3(0, top + 0.6f, 0), new Vector3(0.35f, 0.6f, 0.35f), gold, false, Quaternion.identity);
                Shape(arch, "ArchLantern", GemMesh(), p + new Vector3(0, top - 1.2f, 0) - inward * 0.45f, new Vector3(0.4f, 0.6f, 0.4f), glow, false, Quaternion.identity);
                Deco(arch, PrimitiveType.Cube, "ArchBrace", p - side * (s * 0.55f) + new Vector3(0, top - 0.75f, 0), new Vector3(1.4f, 0.18f, 0.2f), post,
                    face * Quaternion.Euler(0, 0, s * 40f), false);
            }
            Deco(arch, PrimitiveType.Cube, "ArchBeam", new Vector3(0, top - 0.15f, 0), new Vector3(half * 2f + 1.2f, 0.4f, 0.4f), post, face, false);
            foreach (float s in new[] { -1f, 1f })
                Deco(arch, PrimitiveType.Cube, "ArchChain", side * (s * 1.6f) + new Vector3(0, top - 0.75f, 0), new Vector3(0.06f, 0.8f, 0.06f), gold, face, false);
            string text = string.IsNullOrEmpty(levels) ? name : name + "\n" + levels;
            var label = WorldText(arch, "ArchSign", new Vector3(0, top - 1.75f, 0), face, new Vector2(1000, 260), 0.004f, text, 70);
            DressSign(label, 0f, 250f, false);
            label.alignment = TextAnchor.MiddleCenter;
            foreach (var tr in arch.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = true;
        }
    }
}
