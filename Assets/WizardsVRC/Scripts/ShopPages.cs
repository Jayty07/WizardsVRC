using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Starshard Shop (Wizard101's Crown Shop / WoW's store): elixirs, titles, auras and spell skins for
    /// Starshards, which you earn from bosses, elites, towers and quests, or get in bundles from the world's VRChat
    /// store (StarshardStore). Pages of entries; each entry is (kind, index) into CosmeticsDatabase.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShopPages : UdonSharpBehaviour
    {
        public const int ROWS = 8;

        public PlayerState playerState;
        public CosmeticsDatabase cosmetics;
        public StarshardStore store;
        public UiAudio uiAudio;

        [Tooltip("Per entry: 0 title, 1 aura, 2 spell skin, 3 elixir.")]
        public int[] entryKinds;
        public int[] entryIndexes;
        public string[] entryBlurbs;
        public string[] tabNames;
        [Tooltip("Entries for each tab: first entry and count.")]
        public int[] tabStart;
        public int[] tabCount;

        public GameObject[] rows;
        public Text[] rowNames;
        public Text[] rowBlurbs;
        public Text[] rowPrices;
        public Image[] rowSwatches;
        public Image[] tabImages;
        public Text balanceLabel;
        public Text statusLabel;
        public Text pageLabel;
        public GameObject storeButton;

        private int _tab;
        private int _page;

        public void Open() { SetStatus(""); Draw(); }

        public void Tab0() { _tab = 0; _page = 0; Draw(); }
        public void Tab1() { _tab = 1; _page = 0; Draw(); }
        public void Tab2() { _tab = 2; _page = 0; Draw(); }
        public void Tab3() { _tab = 3; _page = 0; Draw(); }
        public void PrevPage() { if (_page > 0) { _page--; Draw(); } }
        public void NextPage() { if ((_page + 1) * ROWS < tabCount[_tab]) { _page++; Draw(); } }
        public void OpenVrcStore() { if (store != null) store.OpenStore(); }

        public void Buy0() { Buy(0); }
        public void Buy1() { Buy(1); }
        public void Buy2() { Buy(2); }
        public void Buy3() { Buy(3); }
        public void Buy4() { Buy(4); }
        public void Buy5() { Buy(5); }
        public void Buy6() { Buy(6); }
        public void Buy7() { Buy(7); }

        private int EntryAt(int row)
        {
            int k = _page * ROWS + row;
            return k < tabCount[_tab] ? tabStart[_tab] + k : -1;
        }

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
                case 0: return "Title: " + cosmetics.titleNames[i];
                case 1: return "Aura: " + cosmetics.auraNames[i];
                case 2: return "Spell skin: " + cosmetics.skinNames[i];
                default: return cosmetics.elixirNames[i];
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
                default: return false;
            }
        }

        private void Buy(int row)
        {
            int e = EntryAt(row);
            if (e < 0 || cosmetics == null) return;
            string msg = cosmetics.Buy(entryKinds[e], entryIndexes[e]);
            bool ok = msg.StartsWith("Bought");
            if (uiAudio != null) { if (ok) uiAudio.PlayOk(); else uiAudio.PlayCancel(); }
            SetStatus(msg);
            Draw();
        }

        private void Draw()
        {
            if (cosmetics == null || playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            if (balanceLabel != null) balanceLabel.text = " " + c.starshards.ToString() + " Starshards";
            if (tabImages != null)
                for (int t = 0; t < tabImages.Length; t++)
                    if (tabImages[t] != null) tabImages[t].color = t == _tab ? new Color(0.85f, 0.65f, 0.3f) : new Color(0.45f, 0.3f, 0.18f);
            for (int r = 0; r < ROWS; r++)
            {
                int e = EntryAt(r);
                bool has = e >= 0;
                if (rows[r] != null && rows[r].activeSelf != has) rows[r].SetActive(has);
                if (!has) continue;
                bool owned = Owned(e);
                int price = Price(e);
                rowNames[r].text = Name(e);
                string blurb = entryBlurbs != null && e < entryBlurbs.Length ? entryBlurbs[e] : "";
                if (entryKinds[e] == 3)
                {
                    int left = cosmetics.ElixirMinutesLeft(entryIndexes[e]);
                    if (left > 0) blurb += "  <b>(" + left.ToString() + " min left)</b>";
                }
                rowBlurbs[r].text = blurb;
                rowPrices[r].text = owned ? "Owned" : (c.starshards >= price ? "" : "<color=#B03020>") + price.ToString() + (c.starshards >= price ? "" : "</color>");
                if (rowSwatches != null && rowSwatches[r] != null)
                {
                    int i = entryIndexes[e];
                    rowSwatches[r].color = entryKinds[e] == 1 ? cosmetics.auraColors[i] : entryKinds[e] == 2 ? cosmetics.skinColors[i] : new Color(0.9f, 0.75f, 0.35f);
                }
            }
            int pages = Mathf.Max(1, (tabCount[_tab] + ROWS - 1) / ROWS);
            if (pageLabel != null) pageLabel.text = (_page + 1).ToString() + " / " + pages.ToString();
            if (storeButton != null) storeButton.SetActive(store != null && store.HasProducts());
        }

        private void SetStatus(string s) { if (statusLabel != null) statusLabel.text = s; }
    }
}
