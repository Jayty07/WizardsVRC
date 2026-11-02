using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The Starshard Shop: a window of blue prismatic crystal opened from the Starshard gem on the book. Categories down
    /// the left (Featured, Surge Auras, Auras, Titles, Spell Skins, Elixirs), a page of item tiles in the middle, and the
    /// chosen item large on the right with its price and a Buy button; your Starshards at the top, and Get Starshards
    /// when the world sells bundles in the VRChat store (StarshardStore). Starshards come from bosses, elites, towers and
    /// quests. Each entry is (kind, index) into CosmeticsDatabase: kind 0 title, 1 aura, 2 spell skin, 3 elixir.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShopPages : UdonSharpBehaviour
    {
        public const int TILES = 9;
        public const int CATS = 6;

        public PlayerState playerState;
        public CosmeticsDatabase cosmetics;
        public StarshardStore store;
        public UiAudio uiAudio;
        public InventoryBook book;

        [Header("Entries (the builder fills these)")]
        public int[] entryKinds;
        public int[] entryIndexes;
        public string[] entryBlurbs;
        public string[] catNames;
        [Tooltip("Per category: its entries are catEntries[catStart .. catStart + catCount).")]
        public int[] catStart;
        public int[] catCount;
        public int[] catEntries;
        [Tooltip("Auras from this index on are school Surges (they power up with your pips).")]
        public int surgeFirst = 13;
        public Color[] elixirColors;

        [Header("Art")]
        public Sprite titleSprite;
        public Sprite auraSprite;
        public Sprite skinSprite;
        public Sprite elixirLiquidSprite;
        public Sprite elixirGlassSprite;
        public Sprite tileSprite;
        public Sprite tileSelectedSprite;
        public Sprite catSprite;
        public Sprite catSelectedSprite;

        [Header("Window")]
        public Text balanceLabel;
        public Text statusLabel;
        public Text pageLabel;
        public Text catTitle;
        public GameObject storeButton;
        public Image[] catImages;

        [Header("Tiles")]
        public GameObject[] tileRoots;
        public Image[] tileFrames;
        public Image[] tileIcons;
        public Image[] tileOverlays;
        public Text[] tileNames;
        public Text[] tilePrices;
        public GameObject[] tileOwned;

        [Header("Preview")]
        public GameObject previewRoot;
        public Image previewIcon;
        public Image previewOverlay;
        public Image previewGlow;
        public Text previewName;
        public Text previewKind;
        public Text previewBlurb;
        public Text previewPrice;
        public GameObject buyButton;
        public Text buyLabel;

        private int _cat;
        private int _page;
        private int _selected = -1;   // entry index

        public void Open()
        {
            _cat = 0;
            _page = 0;
            _selected = EntryAt(0);
            SetStatus("");
            Draw();
        }

        public void BackToBook() { if (book != null) book.ShowBackpack(); }
        public void OpenVrcStore() { if (store != null) store.OpenStore(); }

        public void Cat0() { Category(0); }
        public void Cat1() { Category(1); }
        public void Cat2() { Category(2); }
        public void Cat3() { Category(3); }
        public void Cat4() { Category(4); }
        public void Cat5() { Category(5); }

        public void Tile0() { Pick(0); }
        public void Tile1() { Pick(1); }
        public void Tile2() { Pick(2); }
        public void Tile3() { Pick(3); }
        public void Tile4() { Pick(4); }
        public void Tile5() { Pick(5); }
        public void Tile6() { Pick(6); }
        public void Tile7() { Pick(7); }
        public void Tile8() { Pick(8); }

        public void PrevPage() { if (_page > 0) { _page--; Click(); Draw(); } }
        public void NextPage() { if ((_page + 1) * TILES < CatCount(_cat)) { _page++; Click(); Draw(); } }

        public void BuySelected()
        {
            if (_selected < 0 || cosmetics == null) return;
            string msg = cosmetics.Buy(entryKinds[_selected], entryIndexes[_selected]);
            bool ok = msg.StartsWith("Bought");
            if (uiAudio != null) { if (ok) uiAudio.PlayOk(); else uiAudio.PlayCancel(); }
            SetStatus(msg);
            Draw();
        }

        // ------------------------------------------------------------------ choosing

        private void Category(int c)
        {
            if (c < 0 || c >= CATS || catCount == null || c >= catCount.Length) return;
            _cat = c;
            _page = 0;
            _selected = EntryAt(0);
            SetStatus("");
            Click();
            Draw();
        }

        private void Pick(int tile)
        {
            int e = EntryAt(tile);
            if (e < 0) return;
            _selected = e;
            SetStatus("");
            Click();
            Draw();
        }

        private void Click() { if (uiAudio != null) uiAudio.PlayOk(); }

        private int CatCount(int c) { return catCount != null && c >= 0 && c < catCount.Length ? catCount[c] : 0; }

        /// <summary>The entry shown on tile 'tile' of this page (-1 = none).</summary>
        private int EntryAt(int tile)
        {
            int k = _page * TILES + tile;
            if (k < 0 || k >= CatCount(_cat)) return -1;
            return catEntries[catStart[_cat] + k];
        }

        // ------------------------------------------------------------------ entries

        private int Price(int e)
        {
            int i = entryIndexes[e];
            switch (entryKinds[e])
            {
                case 0: return cosmetics.titlePrice[i];
                case 1: return cosmetics.auraPrice[i];
                case 2: return cosmetics.skinPrice[i];
                default: return cosmetics.elixirPrice[i];
            }
        }

        private string Name(int e)
        {
            int i = entryIndexes[e];
            switch (entryKinds[e])
            {
                case 0: return cosmetics.titleNames[i];
                case 1: return cosmetics.auraNames[i];
                case 2: return cosmetics.skinNames[i];
                default: return cosmetics.elixirNames[i];
            }
        }

        private string KindName(int e)
        {
            switch (entryKinds[e])
            {
                case 0: return "Title";
                case 1: return entryIndexes[e] >= surgeFirst ? "Surge Aura" : "Aura";
                case 2: return "Spell Skin";
                default: return "Elixir";
            }
        }

        private bool Owned(int e)
        {
            int i = entryIndexes[e];
            switch (entryKinds[e])
            {
                case 0: return cosmetics.HasTitle(i);
                case 1: return cosmetics.HasAura(i);
                case 2: return cosmetics.HasSkin(i);
                default: return false;   // elixirs stack
            }
        }

        /// <summary>The entry's picture on an icon image (and the elixir's glass on its overlay).</summary>
        private void Picture(int e, Image icon, Image overlay)
        {
            int i = entryIndexes[e];
            int kind = entryKinds[e];
            Sprite s = kind == 0 ? titleSprite : kind == 1 ? auraSprite : kind == 2 ? skinSprite : elixirLiquidSprite;
            Color c = Color.white;
            if (kind == 1 && cosmetics.auraColors != null && i < cosmetics.auraColors.Length) c = cosmetics.auraColors[i];
            if (kind == 2 && cosmetics.skinColors != null && i < cosmetics.skinColors.Length) c = cosmetics.skinColors[i];
            if (kind == 3) c = elixirColors != null && i < elixirColors.Length ? elixirColors[i] : new Color(0.5f, 0.9f, 0.6f);
            if (icon != null) { icon.sprite = s; icon.color = c; }
            if (overlay != null)
            {
                bool glass = kind == 3;
                if (overlay.gameObject.activeSelf != glass) overlay.gameObject.SetActive(glass);
                if (glass) overlay.sprite = elixirGlassSprite;
            }
        }

        // ------------------------------------------------------------------ drawing

        private void Draw()
        {
            if (cosmetics == null || playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            if (balanceLabel != null) balanceLabel.text = c.starshards.ToString();
            if (catTitle != null) catTitle.text = catNames != null && _cat < catNames.Length ? catNames[_cat] : "";
            if (catImages != null)
                for (int k = 0; k < catImages.Length; k++)
                    if (catImages[k] != null) catImages[k].sprite = k == _cat ? catSelectedSprite : catSprite;

            for (int t = 0; t < TILES; t++)
            {
                int e = EntryAt(t);
                bool has = e >= 0;
                if (tileRoots[t] != null && tileRoots[t].activeSelf != has) tileRoots[t].SetActive(has);
                if (!has) continue;
                bool owned = Owned(e);
                int price = Price(e);
                tileNames[t].text = Name(e);
                tilePrices[t].text = price <= 0 ? "-" : (c.starshards >= price || owned ? "" : "<color=#FF8A8A>") + price.ToString() + (c.starshards >= price || owned ? "" : "</color>");
                if (tileOwned[t] != null && tileOwned[t].activeSelf != owned) tileOwned[t].SetActive(owned);
                if (tileFrames[t] != null) tileFrames[t].sprite = e == _selected ? tileSelectedSprite : tileSprite;
                Picture(e, tileIcons[t], tileOverlays[t]);
            }
            int pages = Mathf.Max(1, (CatCount(_cat) + TILES - 1) / TILES);
            if (pageLabel != null) pageLabel.text = (_page + 1).ToString() + " / " + pages.ToString();
            if (storeButton != null) storeButton.SetActive(store != null && store.HasProducts());
            DrawPreview(c);
        }

        private void DrawPreview(CharacterData c)
        {
            bool has = _selected >= 0;
            if (previewRoot != null && previewRoot.activeSelf != has) previewRoot.SetActive(has);
            if (!has) return;
            int e = _selected;
            bool owned = Owned(e);
            int price = Price(e);
            Picture(e, previewIcon, previewOverlay);
            if (previewGlow != null) { Color g = previewIcon != null ? previewIcon.color : Color.white; g.a = 0.55f; previewGlow.color = g; }
            previewName.text = Name(e);
            previewKind.text = KindName(e);
            string blurb = entryBlurbs != null && e < entryBlurbs.Length ? entryBlurbs[e] : "";
            if (entryKinds[e] == 3)
            {
                int left = cosmetics.ElixirMinutesLeft(entryIndexes[e]);
                if (left > 0) blurb += "\n<color=#9FE8FF>Active: " + left.ToString() + " min left</color>";
            }
            if (entryKinds[e] == 0) blurb = "« " + Name(e) + " »\n" + blurb;
            previewBlurb.text = blurb;
            previewPrice.text = owned ? "Owned" : price.ToString();
            bool canBuy = !owned && price > 0;
            if (buyButton != null && buyButton.activeSelf != canBuy) buyButton.SetActive(canBuy);
            if (buyLabel != null) buyLabel.text = c.starshards >= price ? "Buy" : "Need " + (price - c.starshards).ToString() + " more";
        }

        private void SetStatus(string s) { if (statusLabel != null) statusLabel.text = s; }
    }
}
