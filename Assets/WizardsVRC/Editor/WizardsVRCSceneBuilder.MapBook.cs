using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The book's Map bookmark (MapPages): the map across both pages in a carved wooden frame with a red lip, the title on
    /// a scroll over it, its level range in the corner, a pool of parchment tags (buttons, sized to their words), the
    /// you-are-here marker and the blue pin at your marked location, what's playing in the top corner, and World Map /
    /// Where am I? / Mark Location / Teleport to Mark / Close along the bottom. The maps themselves are drawn after the
    /// zones are built (BuildMaps).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static MapPages BookMap;

        /// <summary>A sprite from Textures/Maps: smooth, no mipmaps, with a 9-slice border when given.</summary>
        private static Sprite MapSprite(string file, Vector4 border)
        {
            string path = MapArtDir + "/" + file;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning("[WizardsVRC] Missing " + path + " (run Tools/make_map_art.py)."); return null; }
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != border || imp.mipmapEnabled || !imp.alphaIsTransparency)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spriteBorder = border;
                imp.filterMode = FilterMode.Bilinear;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject BuildMapPages(RectTransform root, InventoryBook book)
        {
            var pages = UdonSharpUndo.AddComponent<MapPages>(Child(book.transform.parent, "MapPages", Vector3.zero).gameObject);
            BookMap = pages;
            ExtraBehaviours.Add(pages);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(pages);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);

            RectTransform view = Rect(root, "MapView", Vector2.zero, new Vector2(1800, 1100));
            // The map, 1500 x 1000 (its art is 1200 x 800), with the tags and marker inside it.
            RectTransform map = Rect(view, "Map", new Vector2(0, -12), new Vector2(1500, 1000));
            var raw = map.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = Color.white;
            pages.mapImage = raw;
            pages.mapRect = map;

            Sprite tagSprite = MapSprite("MapTag.png", new Vector4(12, 12, 14, 12));
            pages.labelButtons = new GameObject[MapPages.LABELS];
            pages.labelTexts = new Text[MapPages.LABELS];
            for (int i = 0; i < MapPages.LABELS; i++)
            {
                GameObject b = Button(map, "MapTag" + i, Vector2.zero, new Vector2(220, 42), "", Color.white, target, "Label" + i);
                var img = b.GetComponent<Image>();
                img.sprite = tagSprite;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / 1.4f;
                Text t = b.GetComponentInChildren<Text>();
                t.fontSize = 24;
                t.color = Ink;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.alignment = TextAnchor.MiddleCenter;
                // The tag grows to its words.
                var lay = b.AddComponent<HorizontalLayoutGroup>();
                lay.padding = new RectOffset(16, 18, 6, 10);
                lay.childAlignment = TextAnchor.MiddleCenter;
                lay.childControlWidth = true;
                lay.childControlHeight = true;
                lay.childForceExpandWidth = false;
                lay.childForceExpandHeight = false;
                var fit = b.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                pages.labelButtons[i] = b;
                pages.labelTexts[i] = t;
                b.SetActive(false);
            }
            // Your marked location (LocationKeeper), under the you-are-here arrow.
            var mark = Panel(map, "MarkMarker", Vector2.zero, new Vector2(54, 54), Color.white);
            var markImg = mark.GetComponent<Image>();
            markImg.sprite = MapSprite("MapMark.png", Vector4.zero);
            markImg.preserveAspect = true;
            markImg.raycastTarget = false;
            mark.gameObject.SetActive(false);
            pages.markMarker = mark;
            pages.locations = BookLocations;
            var here = Panel(map, "HereMarker", Vector2.zero, new Vector2(58, 58), Color.white);
            var hereImg = here.GetComponent<Image>();
            hereImg.sprite = MapSprite("MapHere.png", Vector4.zero);
            hereImg.preserveAspect = true;
            hereImg.raycastTarget = false;
            here.gameObject.SetActive(false);
            pages.hereMarker = here;

            // The frame over the map's edges (its middle is open).
            var frame = Panel(view, "MapFrame", new Vector2(0, -12), new Vector2(1572, 1072), Color.white);
            var frameImg = frame.GetComponent<Image>();
            frameImg.sprite = MapSprite("MapFrame.png", new Vector4(34, 34, 34, 34));
            frameImg.type = Image.Type.Sliced;
            frameImg.pixelsPerUnitMultiplier = 1f / 1.25f;
            frameImg.raycastTarget = false;

            // The title on its scroll, and the level range in the top corner.
            var scroll = Panel(view, "MapTitleScroll", new Vector2(0, 486), new Vector2(620, 106), Color.white);
            var scrollImg = scroll.GetComponent<Image>();
            scrollImg.sprite = MapSprite("MapTitle.png", Vector4.zero);
            scrollImg.raycastTarget = false;
            pages.titleLabel = Label(scroll, "Title", new Vector2(0, 4), new Vector2(520, 70), "", 40, TextAnchor.MiddleCenter);
            pages.titleLabel.color = Ink;
            pages.titleLabel.resizeTextForBestFit = true;
            pages.titleLabel.resizeTextMinSize = 24;
            pages.titleLabel.resizeTextMaxSize = 40;
            pages.titleLabel.verticalOverflow = VerticalWrapMode.Truncate;
            pages.subtitleLabel = Label(view, "Subtitle", new Vector2(600, 442), new Vector2(240, 40), "", 26, TextAnchor.MiddleRight);
            pages.subtitleLabel.color = Ink;
            // What's playing, in the other top corner (two lines if it's a long one, clear of the title scroll).
            NowPlayingLabel(view, new Vector2(-528, 446), new Vector2(400, 52), 20, TextAnchor.MiddleLeft, Ink, false, true);

            // Along the bottom: World Map (Next World), Where am I?, Close.
            Color wood = new Color(0.45f, 0.3f, 0.18f);
            GameObject worldBtn = Button(view, "MapWorld", new Vector2(-618, -458), new Vector2(240, 62), "World Map", wood, target, nameof(MapPages.ShowWorld));
            pages.worldButtonLabel = worldBtn.GetComponentInChildren<Text>();
            pages.worldButtonLabel.fontSize = 26;
            Button(view, "MapHere", new Vector2(-366, -458), new Vector2(240, 62), "Where am I?", wood, target, nameof(MapPages.ShowHere))
                .GetComponentInChildren<Text>().fontSize = 26;
            // Mark Location (costs a little mana) and Teleport to Mark (free): LocationKeeper.
            Color blue = new Color(0.2f, 0.36f, 0.58f);
            Button(view, "MapMark", new Vector2(-109, -458), new Vector2(250, 62), "Mark Location", blue, target, nameof(MapPages.MarkHere))
                .GetComponentInChildren<Text>().fontSize = 26;
            Button(view, "MapTeleport", new Vector2(178, -458), new Vector2(300, 62), "Teleport to Mark", blue, target, nameof(MapPages.GoToMark))
                .GetComponentInChildren<Text>().fontSize = 26;
            Button(view, "MapClose", new Vector2(632, -458), new Vector2(200, 62), "Close", new Color(0.45f, 0.25f, 0.15f), bookTarget, nameof(InventoryBook.CloseBook))
                .GetComponentInChildren<Text>().fontSize = 26;

            view.gameObject.SetActive(false);
            return view.gameObject;
        }
    }
}
