using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Spell cards outside battle (the book's Deck pages, the professors' spell lists, the new-spell popup) drawn with the
    /// same art as the cards in your hand: the school's frame, name banner, pip gem, art window, accuracy seal, effect
    /// text and school. CardDatabase.FillCard fills them in at runtime.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class CardFaceParts
        {
            public GameObject root;
            public Image frame;
            public Text name, pips, accuracy, info, school;
            public RawImage art;
        }

        /// <summary>Card frames (index School, 7 = General) and shield / blade / trap icons (kind * 8 + school) for CardDatabase.</summary>
        private static void AssignCardArt(CardDatabase db)
        {
            db.cardFrames = new Sprite[8];
            for (int k = 0; k < 8; k++)
                db.cardFrames[k] = SpriteTexture(CardsDir + "/CardFrame" + (k < SchoolLabels.Length ? SchoolLabels[k] : "General") + ".png");
            db.effectIcons = new Texture[3 * 8];
            for (int kind = 0; kind < 3; kind++)
                for (int k = 0; k < 8; k++)
                    db.effectIcons[kind * 8 + k] = PixelTexture(FxIconDir + "/" + FxKindNames[kind] + (k < SchoolLabels.Length ? SchoolLabels[k] : "General") + ".png");
        }

        /// <summary>
        /// A spell card (196 x 280 before scaling) laid out on the frame art like the battle hand's. With a target it is a
        /// button sending eventName. Mini cards (for grids) keep only the name, pip cost and art, with a bolder name.
        /// </summary>
        private static CardFaceParts CardFace(Transform parent, string objName, Vector2 pos, float scale, bool mini, UdonBehaviour target, string eventName)
        {
            var p = new CardFaceParts();
            Vector2 size = new Vector2(196, 280);
            GameObject go;
            if (target != null)
            {
                go = Button(parent, objName, pos, size, "", Color.white, target, eventName);
                Object.DestroyImmediate(go.GetComponentInChildren<Text>().gameObject);
            }
            else go = Panel(parent, objName, pos, size, Color.white).gameObject;
            var rt = (RectTransform)go.transform;
            rt.localScale = new Vector3(scale, scale, 1f);
            p.root = go;
            p.frame = go.GetComponent<Image>();
            p.frame.type = Image.Type.Simple;
            p.frame.preserveAspect = false;

            Color ink = new Color(0.24f, 0.16f, 0.09f);
            Color outline = new Color(0.12f, 0.09f, 0.14f);
            p.name = CardText(rt, "Name", new Vector2(0, mini ? 108 : 110), new Vector2(mini ? 132 : 118, mini ? 34 : 26), mini ? 22 : 15, TextAnchor.MiddleCenter,
                new Color(1f, 0.94f, 0.63f));
            p.name.fontStyle = FontStyle.Bold;
            p.name.resizeTextForBestFit = true;
            p.name.resizeTextMinSize = mini ? 12 : 9;
            p.name.resizeTextMaxSize = mini ? 24 : 15;
            p.name.gameObject.AddComponent<Outline>().effectColor = outline;
            p.pips = CardText(rt, "Pips", new Vector2(-75, 114), new Vector2(34, 34), mini ? 26 : 22, TextAnchor.MiddleCenter, Color.white);
            p.pips.fontStyle = FontStyle.Bold;
            p.pips.gameObject.AddComponent<Outline>().effectColor = outline;
            var artGo = new GameObject("Art", typeof(RectTransform));
            artGo.transform.SetParent(rt, false);
            p.art = artGo.AddComponent<RawImage>();
            p.art.raycastTarget = false;
            ((RectTransform)artGo.transform).anchoredPosition = new Vector2(0, 39);
            ((RectTransform)artGo.transform).sizeDelta = new Vector2(84, 84);
            artGo.SetActive(false);
            if (!mini)
            {
                p.accuracy = CardText(rt, "Accuracy", new Vector2(-60, -21), new Vector2(54, 26), 13, TextAnchor.MiddleCenter, ink);
                p.accuracy.fontStyle = FontStyle.Bold;
                p.info = CardText(rt, "Info", new Vector2(0, -66), new Vector2(150, 64), 13, TextAnchor.UpperCenter, ink);
                p.info.resizeTextForBestFit = true;
                p.info.resizeTextMinSize = 8;
                p.info.resizeTextMaxSize = 13;
                p.school = CardText(rt, "School", new Vector2(0, -106), new Vector2(140, 20), 12, TextAnchor.MiddleCenter, new Color(0.62f, 0.52f, 0.36f));
            }
            return p;
        }
    }
}
