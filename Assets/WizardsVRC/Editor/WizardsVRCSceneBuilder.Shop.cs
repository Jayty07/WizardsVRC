using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Starshard Shop window (ShopPages): opened from the Starshard gem on the book, it covers the whole book in blue
    /// prismatic crystal (art from Tools/make_shard_shop.py in Textures/Shop). A header with the title, your Starshards,
    /// Get Starshards and the way back to the book; categories down the left; a 3 x 3 page of item tiles; the chosen
    /// item large on the right with its price and Buy. Light sweeps across the header and sparkles twinkle (an Animator).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string ShopTexDir = TexturesDir + "/Shop";

        private static readonly Dictionary<string, Vector4> ShopBorders = new Dictionary<string, Vector4>
        {
            { "GlassPanel", new Vector4(12, 12, 12, 12) }, { "Tile", new Vector4(12, 12, 12, 12) }, { "TileSel", new Vector4(12, 12, 12, 12) },
            { "CatButton", new Vector4(10, 10, 10, 10) }, { "CatButtonSel", new Vector4(10, 10, 10, 10) }, { "BuyButton", new Vector4(10, 10, 10, 10) },
            { "GetButton", new Vector4(10, 10, 10, 10) }, { "BackButton", new Vector4(10, 10, 10, 10) }, { "PriceChip", new Vector4(6, 6, 6, 6) },
        };

        private static readonly Color ShopInk = new Color(0.05f, 0.04f, 0.16f);
        private static readonly Color ShopText = new Color(0.9f, 0.97f, 1f);
        private static readonly Color ShopCyan = new Color(0.6f, 0.9f, 1f);

        /// <summary>A Starshard Shop sprite (point filtered, sliced when it has a border).</summary>
        private static Sprite ShopSprite(string name)
        {
            string path = ShopTexDir + "/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning("[WizardsVRC] Missing " + path + " (run Tools/make_shard_shop.py)"); return null; }
            Vector4 border;
            if (!ShopBorders.TryGetValue(name, out border)) border = Vector4.zero;
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image ShopImage(Transform parent, string name, Vector2 pos, Vector2 size, string sprite, float pixelScale, Color tint)
        {
            RectTransform rt = Panel(parent, name, pos, size, tint);
            var img = rt.GetComponent<Image>();
            SetUiSprite(img, ShopSprite(sprite), pixelScale);
            img.raycastTarget = false;
            return img;
        }

        private static Text ShopLabel(Transform parent, string name, Vector2 pos, Vector2 size, string text, int fontSize, TextAnchor anchor, Color color)
        {
            Text t = Label(parent, name, pos, size, text, fontSize, anchor);
            t.color = color;
            t.supportRichText = true;
            t.gameObject.AddComponent<Outline>().effectColor = ShopInk;
            return t;
        }

        /// <summary>A crystal button: its sprite set so the world UI styling leaves it as it is.</summary>
        private static GameObject ShopButton(Transform parent, string name, Vector2 pos, Vector2 size, string text, string sprite, UdonBehaviour target, string ev, int fontSize)
        {
            GameObject b = Button(parent, name, pos, size, text, Color.white, target, ev);
            SetUiSprite(b.GetComponent<Image>(), ShopSprite(sprite), 3f);
            var colors = b.GetComponent<Button>().colors;
            colors.highlightedColor = new Color(0.85f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.85f, 1f);
            b.GetComponent<Button>().colors = colors;
            Text t = b.GetComponentInChildren<Text>();
            t.fontSize = fontSize;
            t.color = ShopText;
            t.gameObject.AddComponent<Outline>().effectColor = ShopInk;
            return b;
        }

        /// <summary>A price: the Starshard and a number, on a little plate.</summary>
        private static Text ShopPrice(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize)
        {
            Image plate = ShopImage(parent, name, pos, size, "PriceChip", 3f, Color.white);
            float gem = size.y * 0.8f;
            ShopImage(plate.transform, "Shard", new Vector2(-size.x / 2f + gem * 0.7f, 0), new Vector2(gem * 0.8f, gem), "Shard", 1f, Color.white);
            return ShopLabel(plate.transform, "Amount", new Vector2(gem * 0.35f, 0), new Vector2(size.x - gem * 1.4f, size.y), "", fontSize, TextAnchor.MiddleCenter, ShopText);
        }

        private static GameObject BuildShopPages(RectTransform root, InventoryBook book)
        {
            // The window covers the whole book (tabs included): it's a place of its own, with a way back.
            RectTransform view = Rect(root, "ShopView", Vector2.zero, new Vector2(1960, 1100));
            view.gameObject.SetActive(false);
            var pages = UdonSharpUndo.AddComponent<ShopPages>(Child(book.transform.parent, "ShopPages", Vector3.zero).gameObject);
            BookShop = pages;
            ExtraBehaviours.Add(pages);
            pages.book = book;
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(pages);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);

            var back = ShopImage(view, "Prism", Vector2.zero, new Vector2(1960, 1100), "ShopBack", 5f, Color.white);
            back.raycastTarget = true;   // the window catches clicks, so nothing behind it is pressed

            // ---- magic: a sheen sweeping the header and sparkles twinkling across the window
            var sheen = ShopImage(view, "Sheen", new Vector2(-1100, 478), new Vector2(160, 150), "Sheen", 5f, new Color(1f, 1f, 1f, 0.6f));
            sheen.rectTransform.localRotation = Quaternion.Euler(0, 0, -18f);
            var rng = new System.Random(5);
            var sparkles = new List<RectTransform>();
            for (int k = 0; k < 16; k++)
            {
                var sp = ShopImage(view, "Twinkle" + k, new Vector2(rng.Next(-940, 940), rng.Next(-520, 520)), new Vector2(40, 40), "Sparkle", 2.5f,
                                   k % 3 == 0 ? new Color(0.8f, 0.7f, 1f) : new Color(0.7f, 0.95f, 1f));
                sparkles.Add(sp.rectTransform);
            }

            // ---- header
            ShopImage(view, "Header", new Vector2(0, 478), new Vector2(1900, 118), "GlassPanel", 3f, Color.white);
            ShopImage(view, "TitleGemL", new Vector2(-395, 478), new Vector2(64, 76), "Shard", 2f, Color.white);
            ShopImage(view, "TitleGemR", new Vector2(395, 478), new Vector2(64, 76), "Shard", 2f, Color.white);
            ShopLabel(view, "Title", new Vector2(0, 478), new Vector2(700, 100), "Starshard Shop", 64, TextAnchor.MiddleCenter, new Color(0.85f, 0.96f, 1f))
                .gameObject.AddComponent<Shadow>().effectColor = new Color(0.45f, 0.3f, 1f, 0.8f);
            ShopButton(view, "Back", new Vector2(-800, 478), new Vector2(250, 80), "< Book", "BackButton", target, nameof(ShopPages.BackToBook), 30);
            pages.balanceLabel = ShopPrice(view, "Balance", new Vector2(560, 478), new Vector2(260, 78), 40);
            pages.storeButton = ShopButton(view, "GetShards", new Vector2(830, 478), new Vector2(220, 80), "Get Shards", "GetButton", target, nameof(ShopPages.OpenVrcStore), 26);

            // ---- categories
            ShopImage(view, "Sidebar", new Vector2(-765, -62), new Vector2(390, 930), "GlassPanel", 3f, Color.white);
            string[] cats = { "Featured", "Surge Auras", "Auras", "Titles", "Spell Skins", "Elixirs" };
            string[] catIcons = { "IconFeatured", "IconAura", "IconAura", "IconTitle", "IconSkin", "IconElixir" };
            pages.catNames = cats;
            pages.catImages = new Image[cats.Length];
            for (int k = 0; k < cats.Length; k++)
            {
                GameObject b = ShopButton(view, "Cat" + k, new Vector2(-765, 330 - k * 112), new Vector2(345, 98), cats[k], "CatButton", target, "Cat" + k, 28);
                Text t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                ((RectTransform)t.transform).offsetMin = new Vector2(96, 0);
                ShopImage(b.transform, "Icon", new Vector2(-122, 0), new Vector2(64, 64), catIcons[k], 2f, k == 1 ? new Color(1f, 0.7f, 0.4f) : Color.white);
                pages.catImages[k] = b.GetComponent<Image>();
            }
            ShopLabel(view, "Earn", new Vector2(-765, -440), new Vector2(350, 140),
                "Starshards come from bosses, elites, tower tops and quests.", 20, TextAnchor.MiddleCenter, ShopCyan);
            pages.catSprite = ShopSprite("CatButton");
            pages.catSelectedSprite = ShopSprite("CatButtonSel");

            // ---- the grid
            ShopImage(view, "Grid", new Vector2(-95, -62), new Vector2(930, 930), "GlassPanel", 3f, Color.white);
            pages.catTitle = ShopLabel(view, "CatTitle", new Vector2(-95, 362), new Vector2(880, 60), "", 40, TextAnchor.MiddleCenter, ShopText);
            int n = ShopPages.TILES;
            pages.tileRoots = new GameObject[n];
            pages.tileFrames = new Image[n];
            pages.tileIcons = new Image[n];
            pages.tileOverlays = new Image[n];
            pages.tileNames = new Text[n];
            pages.tilePrices = new Text[n];
            pages.tileOwned = new GameObject[n];
            for (int t = 0; t < n; t++)
            {
                int col = t % 3, row = t / 3;
                GameObject b = ShopButton(view, "Tile" + t, new Vector2(-95 + (col - 1) * 292, 205 - row * 250), new Vector2(276, 236), "", "Tile", target, "Tile" + t, 20);
                Object.DestroyImmediate(b.GetComponentInChildren<Text>().gameObject);
                pages.tileFrames[t] = b.GetComponent<Image>();
                pages.tileIcons[t] = ShopImage(b.transform, "Icon", new Vector2(0, 38), new Vector2(116, 116), "AuraRing", 1f, Color.white);
                pages.tileOverlays[t] = ShopImage(b.transform, "Glass", new Vector2(0, 38), new Vector2(116, 116), "ElixirGlass", 1f, Color.white);
                pages.tileNames[t] = ShopLabel(b.transform, "Name", new Vector2(0, -50), new Vector2(256, 46), "", 22, TextAnchor.MiddleCenter, ShopText);
                pages.tileNames[t].resizeTextForBestFit = true;
                pages.tileNames[t].resizeTextMinSize = 14;
                pages.tileNames[t].resizeTextMaxSize = 22;
                pages.tilePrices[t] = ShopPrice(b.transform, "Price", new Vector2(0, -92), new Vector2(150, 40), 24);
                var owned = ShopImage(b.transform, "Owned", new Vector2(70, 92), new Vector2(120, 38), "BuyButton", 2f, Color.white);
                ShopLabel(owned.transform, "Text", Vector2.zero, new Vector2(120, 38), "Owned", 18, TextAnchor.MiddleCenter, ShopText);
                pages.tileOwned[t] = owned.gameObject;
                pages.tileRoots[t] = b;
            }
            ShopButton(view, "PrevPage", new Vector2(-300, -470), new Vector2(120, 64), "<", "CatButton", target, nameof(ShopPages.PrevPage), 34);
            pages.pageLabel = ShopLabel(view, "Page", new Vector2(-95, -470), new Vector2(260, 60), "", 28, TextAnchor.MiddleCenter, ShopText);
            ShopButton(view, "NextPage", new Vector2(110, -470), new Vector2(120, 64), ">", "CatButton", target, nameof(ShopPages.NextPage), 34);
            pages.tileSprite = ShopSprite("Tile");
            pages.tileSelectedSprite = ShopSprite("TileSel");

            // ---- the chosen item
            ShopImage(view, "Preview", new Vector2(690, -62), new Vector2(530, 930), "GlassPanel", 3f, Color.white);
            RectTransform pv = Rect(view, "PreviewContent", new Vector2(690, -62), new Vector2(530, 930));
            pages.previewRoot = pv.gameObject;
            pages.previewGlow = ShopImage(pv, "Glow", new Vector2(0, 245), new Vector2(380, 380), "Glow", 6f, Color.white);
            pages.previewIcon = ShopImage(pv, "Icon", new Vector2(0, 245), new Vector2(240, 240), "AuraRing", 1f, Color.white);
            pages.previewOverlay = ShopImage(pv, "Glass", new Vector2(0, 245), new Vector2(240, 240), "ElixirGlass", 1f, Color.white);
            pages.previewName = ShopLabel(pv, "Name", new Vector2(0, 70), new Vector2(490, 70), "", 42, TextAnchor.MiddleCenter, ShopText);
            pages.previewName.resizeTextForBestFit = true;
            pages.previewName.resizeTextMinSize = 24;
            pages.previewName.resizeTextMaxSize = 42;
            pages.previewKind = ShopLabel(pv, "Kind", new Vector2(0, 22), new Vector2(490, 40), "", 24, TextAnchor.MiddleCenter, ShopCyan);
            pages.previewBlurb = ShopLabel(pv, "Blurb", new Vector2(0, -110), new Vector2(470, 210), "", 24, TextAnchor.UpperCenter, ShopText);
            pages.previewBlurb.resizeTextForBestFit = true;
            pages.previewBlurb.resizeTextMinSize = 16;
            pages.previewBlurb.resizeTextMaxSize = 24;
            pages.previewBlurb.verticalOverflow = VerticalWrapMode.Truncate;
            pages.previewPrice = ShopPrice(pv, "Price", new Vector2(0, -258), new Vector2(240, 66), 36);
            pages.buyButton = ShopButton(pv, "Buy", new Vector2(0, -350), new Vector2(380, 96), "Buy", "BuyButton", target, nameof(ShopPages.BuySelected), 38);
            pages.buyLabel = pages.buyButton.GetComponentInChildren<Text>();
            pages.statusLabel = ShopLabel(view, "Status", new Vector2(690, -470), new Vector2(510, 60), "", 22, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.6f));
            pages.titleSprite = ShopSprite("TitleScroll");
            pages.auraSprite = ShopSprite("AuraRing");
            pages.skinSprite = ShopSprite("SkinGlyph");
            pages.elixirLiquidSprite = ShopSprite("ElixirLiquid");
            pages.elixirGlassSprite = ShopSprite("ElixirGlass");
            foreach (var img in pv.GetComponentsInChildren<Image>(true)) if (img.GetComponent<Button>() == null) img.raycastTarget = false;

            ShopMagic(view, sheen.rectTransform, sparkles, (RectTransform)pages.previewGlow.transform);
            FillShopEntries(pages);
            return view.gameObject;
        }

        /// <summary>The shop's icon on the book: the Starshard in its gold setting, glowing and gently pulsing, on its tab.</summary>
        private static void ShopTabGem(Transform tab)
        {
            var glow = ShopImage(tab, "GemGlow", new Vector2(-6, 0), new Vector2(150, 150), "Glow", 2.5f, new Color(0.55f, 0.8f, 1f, 0.7f));
            var gem = ShopImage(tab, "Gem", new Vector2(-6, 0), new Vector2(104, 104), "BookGem", 2.6f, Color.white);
            var twinkle = ShopImage(tab, "GemTwinkle", new Vector2(18, 30), new Vector2(34, 34), "Sparkle", 2f, Color.white);
            var clip = NewClip("ShopGemPulse", true);
            Wave(clip, gem.name, "scalex", 2.4f, 1, 1f, 0.06f);
            Wave(clip, gem.name, "scaley", 2.4f, 1, 1f, 0.06f);
            Wave(clip, glow.name, "scalex", 2.4f, 1, 1f, 0.14f, 0.1f);
            Wave(clip, glow.name, "scaley", 2.4f, 1, 1f, 0.14f, 0.1f);
            foreach (string p in new[] { "scalex", "scaley" }) Keys(clip, twinkle.name, p, 0f, 0.01f, 1.6f, 0.01f, 1.85f, 1f, 2.1f, 0.01f, 2.4f, 0.01f);
            System.IO.Directory.CreateDirectory(NpcDir);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(NpcDir + "/ShopGem.controller");
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            ctrl.layers[0].stateMachine.AddState("Pulse").motion = clip;
            var anim = tab.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>The window's shimmer: light sweeping the header, sparkles twinkling in turn, the preview glow breathing.</summary>
        private static void ShopMagic(RectTransform view, RectTransform sheen, List<RectTransform> sparkles, RectTransform glow)
        {
            var clip = NewClip("ShopMagic", true);
            const float len = 4f;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(sheen.name, typeof(RectTransform), "m_AnchoredPosition.x"),
                new AnimationCurve(new Keyframe(0f, -1100f), new Keyframe(1.6f, 1100f), new Keyframe(len, 1100f)));
            for (int k = 0; k < sparkles.Count; k++)
            {
                string path = sparkles[k].name;
                float t0 = (k * 0.53f) % len;
                // Each twinkles once a cycle: grows, turns a little, shrinks away.
                float a = t0, b = Mathf.Min(len, t0 + 0.35f), c = Mathf.Min(len, t0 + 0.7f);
                foreach (string p in new[] { "scalex", "scaley" })
                    Keys(clip, path, p, 0f, 0.01f, a, 0.01f, b, 1f, c, 0.01f, len, 0.01f);
                Keys(clip, path, "rotz", 0f, 0f, a, 0f, c, 45f, len, 45f);
            }
            string g = glow.parent.name + "/" + glow.name;
            Wave(clip, g, "scalex", len, 2, 1f, 0.08f);
            Wave(clip, g, "scaley", len, 2, 1f, 0.08f);
            Wave(clip, g, "rotz", len, 1, 0f, 8f);
            System.IO.Directory.CreateDirectory(NpcDir);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(NpcDir + "/ShopMagic.controller");
            AssetDatabase.AddObjectToAsset(clip, ctrl);
            ctrl.layers[0].stateMachine.AddState("Shimmer").motion = clip;
            var anim = view.gameObject.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>The catalogue, by category: Featured (the Surges and the rarest auras), Surge Auras, Auras, Titles, Spell Skins, Elixirs.</summary>
        private static void FillShopEntries(ShopPages pages)
        {
            var kinds = new List<int>();
            var idx = new List<int>();
            var blurbs = new List<string>();
            var cats = new List<int>[ShopPages.CATS];
            for (int c = 0; c < cats.Length; c++) cats[c] = new List<int>();
            System.Func<int, int, string, int> add = (kind, i, blurb) => { kinds.Add(kind); idx.Add(i); blurbs.Add(blurb); return kinds.Count - 1; };

            string surgeBlurb = "Smoulders round you, and in a duel it swells with every pip you hold. Cast and it blasts outward; " +
                                "at full pips it erupts in spires of energy and crackling arcs.";
            for (int i = SurgeFirst; i < AuraNames.Length; i++)
            {
                int e = add(1, i, surgeBlurb);
                cats[0].Add(e);
                cats[1].Add(e);
            }
            for (int i = 0; i < SurgeFirst; i++)
            {
                if (AuraPrices[i] <= 0) continue;
                int e = add(1, i, i < 7 ? "A school aura, whatever your school. Everyone sees it, in the world and in duels."
                                        : "A rare aura, seen by everyone in the world and in duels.");
                cats[2].Add(e);
                if (i >= 8) cats[0].Add(e);
            }
            for (int i = 0; i < TitleNames.Length; i++)
                if (TitlePrices[i] > 0) cats[3].Add(add(0, i, "Shown over your head for everyone to see."));
            for (int i = 0; i < SkinNames.Length; i++)
                cats[4].Add(add(2, i, "Your casting glyph and spell flashes take on this colour."));
            for (int i = 0; i < ElixirNames.Length; i++)
                cats[5].Add(add(3, i, ElixirBlurbs[i] + "\nCounts down only while you play; never works in duels against other wizards."));

            pages.entryKinds = kinds.ToArray();
            pages.entryIndexes = idx.ToArray();
            pages.entryBlurbs = blurbs.ToArray();
            var all = new List<int>();
            pages.catStart = new int[ShopPages.CATS];
            pages.catCount = new int[ShopPages.CATS];
            for (int c = 0; c < cats.Length; c++)
            {
                pages.catStart[c] = all.Count;
                pages.catCount[c] = cats[c].Count;
                all.AddRange(cats[c]);
            }
            pages.catEntries = all.ToArray();
            pages.surgeFirst = SurgeFirst;
            Color[] elixirs = { new Color(0.45f, 0.95f, 0.5f), new Color(0.4f, 0.75f, 1f), new Color(1f, 0.45f, 0.35f), new Color(1f, 0.8f, 0.3f), new Color(0.8f, 0.5f, 1f), new Color(0.35f, 1f, 0.9f), new Color(1f, 0.6f, 0.85f), new Color(0.95f, 0.95f, 0.95f) };
            pages.elixirColors = new Color[ElixirNames.Length];
            for (int i = 0; i < ElixirNames.Length; i++) pages.elixirColors[i] = elixirs[i % elixirs.Length];
        }
    }
}
