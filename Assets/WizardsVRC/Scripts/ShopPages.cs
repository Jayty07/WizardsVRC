using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The Starshard Shop: a window of blue prismatic crystal opened from the Starshard gem on the book. Categories down
    /// the left (Featured, Surge Auras, Auras, Titles, Spell Skins, Elixirs), a page of item tiles in the middle, and the
    /// chosen item on the right with its price and a Buy button: auras, titles and spell skins are tried on your own
    /// avatar there (ShopPreview films you wearing them), elixirs show their picture. Your Starshards at the top, and Get Starshards
    /// when the world sells bundles in the VRChat store (StarshardStore). Starshards come from bosses, elites, towers and
    /// quests. Each entry is (kind, index) into CosmeticsDatabase: kind 0 title, 1 aura, 2 spell skin, 3 elixir; kind 4 is a
    /// card pack (index into CardPacks): buying one opens it at once, its cards turning over one by one with their rarity
    /// glowing behind them, then Open Another or Done.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShopPages : UdonSharpBehaviour
    {
        public const int TILES = 9;
        public const int CATS = 7;
        public const int KIND_PACK = 4;

        public PlayerState playerState;
        public CosmeticsDatabase cosmetics;
        public StarshardStore store;
        public UiAudio uiAudio;
        public InventoryBook book;
        public CardPacks packs;
        public CardDatabase cardDatabase;
        public ItemDatabase itemDatabase;

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
        [Tooltip("A card pack: its foil bag (tinted the pack's colour) and its crimped gold ends and seal on top.")]
        public Sprite packSprite;
        public Sprite packTrimSprite;

        [Header("Opening a pack")]
        public GameObject revealRoot;
        public Text revealTitle;
        public Text revealNote;
        public GameObject revealAgainButton;
        public Text revealAgainLabel;
        [Tooltip("Per card (CardPacks.MAX_CARDS): the card itself (it turns over), its back, its face, and on the face its frame, art, icon, name, kind and rarity, with a rarity glow behind.")]
        public RectTransform[] revealCards;
        public GameObject[] revealBacks;
        public GameObject[] revealFronts;
        public Image[] revealFrames;
        public RawImage[] revealArts;
        public Image[] revealIcons;
        public Text[] revealNames;
        public Text[] revealKinds;
        public Text[] revealRarities;
        public Image[] revealGlows;
        public Image[] revealBackTints;

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

        [Header("Try-on")]
        [Tooltip("Films your own avatar wearing the picked aura, title or spell skin (elixirs show their picture instead).")]
        public ShopPreview preview;
        [Tooltip("The frame holding the try-on film (shown instead of the icon).")]
        public GameObject stageFrame;

        private int _cat;
        private int _page;
        private int _selected = -1;   // entry index
        private int _revealPack = -1;
        private float _revealStart;
        private int _revealShown;

        public void Open()
        {
            _revealPack = -1;
            if (revealRoot != null) revealRoot.SetActive(false);
            _cat = 0;
            _page = 0;
            _selected = EntryAt(0);
            SetStatus("");
            Draw();
        }

        public void BackToBook()
        {
            if (preview != null) preview.Hide();
            _revealPack = -1;
            if (revealRoot != null) revealRoot.SetActive(false);
            if (book != null) book.ShowBackpack();
        }
        public void OpenVrcStore() { if (store != null) store.OpenStore(); }

        public void Cat0() { Category(0); }
        public void Cat1() { Category(1); }
        public void Cat2() { Category(2); }
        public void Cat3() { Category(3); }
        public void Cat4() { Category(4); }
        public void Cat5() { Category(5); }
        public void Cat6() { Category(6); }

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
            if (_selected >= 0 && entryKinds[_selected] == KIND_PACK) { OpenPack(entryIndexes[_selected]); return; }
            if (_selected < 0 || cosmetics == null) return;
            string msg = cosmetics.Buy(entryKinds[_selected], entryIndexes[_selected]);
            bool ok = msg.StartsWith("Bought");
            if (uiAudio != null) { if (ok) uiAudio.PlayOk(); else uiAudio.PlayCancel(); }
            SetStatus(msg);
            Draw();
        }

        // ------------------------------------------------------------------ card packs

        /// <summary>Buys pack p and turns its cards over (CardPacks rolls and gives them).</summary>
        private void OpenPack(int p)
        {
            if (packs == null) return;
            string msg = packs.Open(p);
            if (msg.Length > 0)
            {
                if (uiAudio != null) uiAudio.PlayCancel();
                SetStatus(msg);
                Draw();
                return;
            }
            if (uiAudio != null) uiAudio.PlayBookOpen();
            if (preview != null) preview.Hide();
            _revealPack = p;
            _revealStart = Time.time;
            _revealShown = 0;
            SetStatus("Opened a " + packs.PackName(p) + "!");
            DrawReveal();
            Draw();
        }

        /// <summary>Open Another: the same pack again.</summary>
        public void RevealAgain()
        {
            if (_revealPack < 0) return;
            OpenPack(_revealPack);
        }

        public void RevealDone()
        {
            _revealPack = -1;
            if (revealRoot != null) revealRoot.SetActive(false);
            if (uiAudio != null) uiAudio.PlayOk();
            Draw();
        }

        /// <summary>Lays the pulled cards out face down; Update turns them over one by one.</summary>
        private void DrawReveal()
        {
            if (revealRoot == null || packs == null) return;
            revealRoot.SetActive(true);
            int n = packs.resultCount;
            if (revealTitle != null) revealTitle.text = packs.PackName(_revealPack);
            if (revealNote != null) revealNote.text = "";
            Color packCol = packs.PackColor(_revealPack);
            for (int k = 0; k < CardPacks.MAX_CARDS; k++)
            {
                RectTransform card = revealCards != null && k < revealCards.Length ? revealCards[k] : null;
                if (card == null) continue;
                bool has = k < n;
                if (card.gameObject.activeSelf != has) card.gameObject.SetActive(has);
                if (!has) continue;
                // The row is centred however many cards the pack holds.
                card.anchoredPosition = new Vector2((k - (n - 1) / 2f) * 232f, -10f);
                card.localScale = Vector3.one;
                SetActive(revealBacks, k, true);
                SetActive(revealFronts, k, false);
                if (revealBackTints != null && k < revealBackTints.Length && revealBackTints[k] != null) revealBackTints[k].color = packCol;
                if (revealGlows != null && k < revealGlows.Length && revealGlows[k] != null) revealGlows[k].gameObject.SetActive(false);
                FillRevealFace(k);
            }
            int price = packs.PackPrice(_revealPack);
            if (revealAgainLabel != null) revealAgainLabel.text = "Open Another (" + price.ToString() + ")";
            if (revealAgainButton != null) revealAgainButton.SetActive(false);
        }

        private void FillRevealFace(int k)
        {
            int kind = packs.resultKinds[k];
            int id = packs.resultIds[k];
            int rarity = packs.resultRarity[k];
            Image frame = revealFrames != null && k < revealFrames.Length ? revealFrames[k] : null;
            RawImage art = revealArts != null && k < revealArts.Length ? revealArts[k] : null;
            Image icon = revealIcons != null && k < revealIcons.Length ? revealIcons[k] : null;
            string nm = "";
            string what = "";
            if ((kind == CardPacks.SLOT_TREASURE || kind == CardPacks.SLOT_SPELL) && cardDatabase != null)
            {
                nm = cardDatabase.GetName(id);
                what = kind == CardPacks.SLOT_SPELL
                    ? "<color=#FF9CFF>STARSHARD SPELL - learned for good!</color>\n" + cardDatabase.SchoolName(cardDatabase.GetSchool(id)) + ", " + cardDatabase.GetPipCost(id).ToString() + " pips"
                    : "Treasure Card  -  " + cardDatabase.SchoolName(cardDatabase.GetSchool(id)) + ", " + cardDatabase.GetPipCost(id).ToString() + " pips";
                if (frame != null) { Sprite fs = cardDatabase.FrameFor(id); frame.sprite = fs; frame.color = fs != null ? Color.white : cardDatabase.GetColor(id); }
                Texture t = cardDatabase.ArtFor(id);
                if (art != null)
                {
                    art.texture = t;
                    if (t != null) art.uvRect = t.width == t.height ? new Rect(-0.25f, 0f, 1.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
                    if (art.gameObject.activeSelf != (t != null)) art.gameObject.SetActive(t != null);
                }
                if (icon != null && icon.gameObject.activeSelf) icon.gameObject.SetActive(false);
            }
            else
            {
                if (frame != null) { frame.sprite = null; frame.color = new Color(0.93f, 0.86f, 0.68f); }
                if (art != null && art.gameObject.activeSelf) art.gameObject.SetActive(false);
                if (icon != null)
                {
                    if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
                    icon.color = kind == CardPacks.SLOT_GEAR && itemDatabase != null ? itemDatabase.GetIconColor(id) : new Color(1f, 0.8f, 0.2f);
                }
                if (kind == CardPacks.SLOT_GEAR && itemDatabase != null)
                {
                    nm = itemDatabase.GetName(id);
                    what = itemDatabase.GetCategoryName(itemDatabase.GetCategory(id)) + "  -  level " + itemDatabase.GetLevelReq(id).ToString();
                }
                else
                {
                    nm = id.ToString() + " gold";
                    what = "Gold";
                }
            }
            if (packs.resultGold[k] > 0) what += "\n<color=#FFD27A>(no room: +" + packs.resultGold[k].ToString() + " gold)</color>";
            SetText(revealNames, k, nm);
            SetText(revealKinds, k, what);
            SetText(revealRarities, k, cardDatabase != null ? cardDatabase.RarityName(rarity) : "");
            if (revealRarities != null && k < revealRarities.Length && revealRarities[k] != null && cardDatabase != null) revealRarities[k].color = cardDatabase.RarityColor(rarity);
            if (revealGlows != null && k < revealGlows.Length && revealGlows[k] != null && cardDatabase != null)
            {
                Color g = cardDatabase.RarityColor(rarity);
                g.a = rarity >= 2 ? 0.85f : 0.45f;
                revealGlows[k].color = g;
            }
        }

        private void Update()
        {
            if (_revealPack < 0 || revealRoot == null || !revealRoot.activeInHierarchy || packs == null) return;
            // Each card turns over in turn: its back narrows to nothing, then its face widens out.
            float t = Time.time - _revealStart;
            int n = packs.resultCount;
            bool allDone = true;
            for (int k = 0; k < n && revealCards != null && k < revealCards.Length; k++)
            {
                RectTransform card = revealCards[k];
                if (card == null) continue;
                float f = t - (0.6f + k * 0.55f);
                if (f < 0f) { allDone = false; continue; }
                if (f < 0.14f)
                {
                    card.localScale = new Vector3(1f - f / 0.14f, 1f, 1f);
                    allDone = false;
                    continue;
                }
                if (k >= _revealShown)
                {
                    _revealShown = k + 1;
                    SetActive(revealBacks, k, false);
                    SetActive(revealFronts, k, true);
                    if (revealGlows != null && k < revealGlows.Length && revealGlows[k] != null) revealGlows[k].gameObject.SetActive(true);
                    int r = packs.resultRarity[k];
                    if (uiAudio != null) { if (r >= 3) uiAudio.PlayQuestComplete(); else if (r == 2) uiAudio.PlayPopup(); else uiAudio.PlayCardClick(); }
                }
                float w = Mathf.Clamp01((f - 0.14f) / 0.14f);
                // A rare card swells a little as it lands.
                float pop = packs.resultRarity[k] >= 2 ? 1f + 0.08f * Mathf.Sin(Mathf.Clamp01((f - 0.14f) / 0.4f) * Mathf.PI) : 1f;
                card.localScale = new Vector3(w * pop, pop, 1f);
                if (f < 0.6f) allDone = false;
            }
            if (allDone && revealAgainButton != null && !revealAgainButton.activeSelf)
            {
                revealAgainButton.SetActive(true);
                bool learned = false;
                for (int k = 0; k < n; k++) if (packs.resultKinds[k] == CardPacks.SLOT_SPELL) learned = true;
                if (revealNote != null)
                    revealNote.text = learned ? "<color=#FF9CFF>A Starshard spell! It's yours for good: add it to a deck on your book's Deck page.</color>"
                                              : "Treasure cards wait in your book's Deck page (Treasure icon); gear and wands are in your backpack.";
            }
        }

        private void SetActive(GameObject[] a, int i, bool on)
        {
            if (a != null && i >= 0 && i < a.Length && a[i] != null && a[i].activeSelf != on) a[i].SetActive(on);
        }

        private void SetText(Text[] a, int i, string v)
        {
            if (a != null && i >= 0 && i < a.Length && a[i] != null) a[i].text = v;
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
                case KIND_PACK: return packs != null ? packs.PackPrice(i) : 0;
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
                case KIND_PACK: return packs != null ? packs.PackName(i) : "";
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
                case KIND_PACK: return "Card Pack";
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
            Sprite s = kind == 0 ? titleSprite : kind == 1 ? auraSprite : kind == 2 ? skinSprite : kind == KIND_PACK ? packSprite : elixirLiquidSprite;
            Color c = Color.white;
            if (kind == KIND_PACK && packs != null) c = packs.PackColor(i);
            if (kind == 1 && cosmetics.auraColors != null && i < cosmetics.auraColors.Length) c = cosmetics.auraColors[i];
            if (kind == 2 && cosmetics.skinColors != null && i < cosmetics.skinColors.Length) c = cosmetics.skinColors[i];
            if (kind == 3) c = elixirColors != null && i < elixirColors.Length ? elixirColors[i] : new Color(0.5f, 0.9f, 0.6f);
            if (icon != null) { icon.sprite = s; icon.color = c; }
            if (overlay != null)
            {
                bool glass = kind == 3 || kind == KIND_PACK;
                if (overlay.gameObject.activeSelf != glass) overlay.gameObject.SetActive(glass);
                if (glass) { overlay.sprite = kind == KIND_PACK ? packTrimSprite : elixirGlassSprite; overlay.color = Color.white; }
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
            if (!has)
            {
                if (preview != null) preview.Hide();
                return;
            }
            int e = _selected;
            bool owned = Owned(e);
            int price = Price(e);
            Picture(e, previewIcon, previewOverlay);
            // Auras, titles and spell skins are tried on your own avatar; elixirs keep their picture.
            int kind = entryKinds[e];
            Color skinColor = kind == 2 ? cosmetics.SkinColor(entryIndexes[e] + 1) : Color.white;
            bool onYou = kind != 3 && kind != KIND_PACK && preview != null && preview.Show(kind, entryIndexes[e], (int)c.school, skinColor, Name(e));
            if (!onYou && preview != null) preview.Hide();
            if (stageFrame != null && stageFrame.activeSelf != onYou) stageFrame.SetActive(onYou);
            if (previewIcon != null && previewIcon.gameObject.activeSelf == onYou) previewIcon.gameObject.SetActive(!onYou);
            if (previewGlow != null && previewGlow.gameObject.activeSelf == onYou) previewGlow.gameObject.SetActive(!onYou);
            if (onYou && previewOverlay != null && previewOverlay.gameObject.activeSelf) previewOverlay.gameObject.SetActive(false);
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
            if (kind == KIND_PACK && packs != null) blurb = packs.PackBlurb(entryIndexes[e]);
            previewBlurb.text = blurb;
            previewPrice.text = owned ? "Owned" : price.ToString();
            bool canBuy = !owned && price > 0;
            if (buyButton != null && buyButton.activeSelf != canBuy) buyButton.SetActive(canBuy);
            if (buyLabel != null) buyLabel.text = c.starshards >= price ? (kind == KIND_PACK ? "Buy & Open" : "Buy") : "Need " + (price - c.starshards).ToString() + " more";
        }

        private void SetStatus(string s) { if (statusLabel != null) statusLabel.text = s; }
    }
}
