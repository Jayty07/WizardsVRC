using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Deck tab. Left page: the selected deck (unique cards with copy counts) and a Deck 1/2/3 switch.
    /// Right page: the spellbook (known spells, filter tabs, paged grid) and a details box with Add/Remove.
    /// Shown/hidden by <see cref="InventoryBook"/>; writes to PlayerState.current deck arrays.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DeckPages : UdonSharpBehaviour
    {
        public const int DECK_CELLS = CardDatabase.DECK_MAX;
        public const int BOOK_CELLS = 12;
        public const int FILTER_ALL = 0;
        public const int FILTER_SCHOOL = 1;
        public const int FILTER_GENERAL = 2;

        public PlayerState playerState;
        public CardDatabase cardDatabase;

        [Header("Left page: deck")]
        public Text deckTitle;
        public Image[] deckTabImages;
        public GameObject[] deckCellRoots;
        public Image[] deckCellIcons;
        public Text[] deckCellNames;
        public Text[] deckCellCounts;
        public Text statusLabel;

        [Header("Right page: spellbook")]
        public Image[] filterImages;
        public Text schoolFilterLabel;
        public GameObject[] bookCellRoots;
        public Image[] bookCellIcons;
        public Text[] bookCellNames;
        public Text pageLabel;
        public GameObject detailsPanel;
        public Text detailName;
        public Text detailBody;
        public GameObject addButton;
        public GameObject removeButton;

        private Color _tabOn = new Color(0.85f, 0.65f, 0.3f);
        private Color _tabOff = new Color(0.45f, 0.3f, 0.18f);

        private int _deck;
        private int _filter;
        private int _page;
        private int _selected;

        private int[] _deckUnique = new int[DECK_CELLS];
        private int _deckUniqueCount;
        private int[] _known = new int[64];
        private int _knownCount;

        // ------------------------------------------------------------------ uGUI events

        public void Deck0() { SetDeck(0); }
        public void Deck1() { SetDeck(1); }
        public void Deck2() { SetDeck(2); }

        public void Filter0() { SetFilter(0); }
        public void Filter1() { SetFilter(1); }
        public void Filter2() { SetFilter(2); }

        public void DeckCell0() { SelectDeckCell(0); }
        public void DeckCell1() { SelectDeckCell(1); }
        public void DeckCell2() { SelectDeckCell(2); }
        public void DeckCell3() { SelectDeckCell(3); }
        public void DeckCell4() { SelectDeckCell(4); }
        public void DeckCell5() { SelectDeckCell(5); }
        public void DeckCell6() { SelectDeckCell(6); }
        public void DeckCell7() { SelectDeckCell(7); }
        public void DeckCell8() { SelectDeckCell(8); }
        public void DeckCell9() { SelectDeckCell(9); }
        public void DeckCell10() { SelectDeckCell(10); }
        public void DeckCell11() { SelectDeckCell(11); }
        public void DeckCell12() { SelectDeckCell(12); }
        public void DeckCell13() { SelectDeckCell(13); }
        public void DeckCell14() { SelectDeckCell(14); }
        public void DeckCell15() { SelectDeckCell(15); }
        public void DeckCell16() { SelectDeckCell(16); }
        public void DeckCell17() { SelectDeckCell(17); }
        public void DeckCell18() { SelectDeckCell(18); }
        public void DeckCell19() { SelectDeckCell(19); }

        public void BookCell0() { SelectBookCell(0); }
        public void BookCell1() { SelectBookCell(1); }
        public void BookCell2() { SelectBookCell(2); }
        public void BookCell3() { SelectBookCell(3); }
        public void BookCell4() { SelectBookCell(4); }
        public void BookCell5() { SelectBookCell(5); }
        public void BookCell6() { SelectBookCell(6); }
        public void BookCell7() { SelectBookCell(7); }
        public void BookCell8() { SelectBookCell(8); }
        public void BookCell9() { SelectBookCell(9); }
        public void BookCell10() { SelectBookCell(10); }
        public void BookCell11() { SelectBookCell(11); }

        public void PrevPage()
        {
            if (_page > 0) _page--;
            Refresh();
        }

        public void NextPage()
        {
            if (_page < PageCount() - 1) _page++;
            Refresh();
        }

        public void AddSelected()
        {
            if (!HasCharacter() || _selected <= 0) return;
            CharacterData c = playerState.current;
            if (!cardDatabase.Knows(c, _selected)) { SetStatus("You haven't learned that spell yet."); return; }
            if (c.DeckCount(_deck) >= CardDatabase.DECK_MAX) { SetStatus("This deck is full (" + CardDatabase.DECK_MAX.ToString() + " cards)."); return; }
            if (c.CountInDeck(_deck, _selected) >= CardDatabase.MAX_COPIES) { SetStatus("Max " + CardDatabase.MAX_COPIES.ToString() + " copies of one spell."); return; }
            if (!c.AddToDeck(_deck, _selected)) return;
            playerState.MarkDirty();
            SetStatus("Added " + cardDatabase.GetName(_selected) + ".");
            Refresh();
        }

        public void RemoveSelected()
        {
            if (!HasCharacter() || _selected <= 0) return;
            CharacterData c = playerState.current;
            if (!c.RemoveFromDeck(_deck, _selected)) return;
            playerState.MarkDirty();
            SetStatus("Removed " + cardDatabase.GetName(_selected) + ".");
            Refresh();
        }

        /// <summary>Called by InventoryBook when the Deck tab is opened.</summary>
        public void Open()
        {
            _selected = 0;
            SetStatus("");
            Refresh();
        }

        // ------------------------------------------------------------------ state

        private void SetDeck(int deck)
        {
            _deck = Mathf.Clamp(deck, 0, CharacterData.DECK_COUNT - 1);
            _selected = 0;
            SetStatus("");
            Refresh();
        }

        private void SetFilter(int filter)
        {
            _filter = filter;
            _page = 0;
            Refresh();
        }

        private void SelectDeckCell(int cell)
        {
            if (cell >= _deckUniqueCount) return;
            _selected = _deckUnique[cell];
            SetStatus("");
            Refresh();
        }

        private void SelectBookCell(int cell)
        {
            int i = _page * BOOK_CELLS + cell;
            if (i >= _knownCount) return;
            _selected = _known[i];
            SetStatus("");
            Refresh();
        }

        private int PageCount()
        {
            return Mathf.Max(1, (_knownCount + BOOK_CELLS - 1) / BOOK_CELLS);
        }

        private void BuildLists(CharacterData c)
        {
            _deckUniqueCount = 0;
            int len = c.DeckCount(_deck);
            for (int k = 0; k < len; k++)
            {
                int id = c.deckCards[_deck * CharacterData.DECK_SIZE + k];
                bool seen = false;
                for (int u = 0; u < _deckUniqueCount; u++)
                    if (_deckUnique[u] == id) { seen = true; break; }
                if (!seen && _deckUniqueCount < DECK_CELLS) _deckUnique[_deckUniqueCount++] = id;
            }

            _knownCount = 0;
            int n = cardDatabase.Count();
            for (int i = 0; i < n && _knownCount < _known.Length; i++)
            {
                int id = cardDatabase.IdAt(i);
                if (!cardDatabase.Knows(c, id)) continue;
                int school = cardDatabase.GetSchool(id);
                if (_filter == FILTER_SCHOOL && school == CardDatabase.GENERAL) continue;
                if (_filter == FILTER_GENERAL && school != CardDatabase.GENERAL) continue;
                _known[_knownCount++] = id;
            }
        }

        // ------------------------------------------------------------------ drawing

        public void Refresh()
        {
            if (!HasCharacter() || cardDatabase == null) return;
            CharacterData c = playerState.current;
            c.EnsureArrays();
            BuildLists(c);

            if (deckTitle != null)
                deckTitle.text = "Deck " + (_deck + 1).ToString() + "   " + c.DeckCount(_deck).ToString() + " / " + CardDatabase.DECK_MAX.ToString() + " cards";
            if (deckTabImages != null)
                for (int d = 0; d < deckTabImages.Length; d++)
                    if (deckTabImages[d] != null) deckTabImages[d].color = d == _deck ? _tabOn : _tabOff;

            for (int cell = 0; cell < DECK_CELLS; cell++)
            {
                bool has = cell < _deckUniqueCount;
                if (deckCellRoots != null && cell < deckCellRoots.Length && deckCellRoots[cell] != null) deckCellRoots[cell].SetActive(has);
                if (!has) continue;
                int id = _deckUnique[cell];
                if (deckCellIcons[cell] != null) deckCellIcons[cell].color = cardDatabase.GetColor(id);
                if (deckCellNames[cell] != null) deckCellNames[cell].text = cardDatabase.GetName(id);
                if (deckCellCounts[cell] != null) deckCellCounts[cell].text = "x" + c.CountInDeck(_deck, id).ToString();
            }

            if (schoolFilterLabel != null) schoolFilterLabel.text = cardDatabase.SchoolName((int)c.school);
            if (filterImages != null)
                for (int f = 0; f < filterImages.Length; f++)
                    if (filterImages[f] != null) filterImages[f].color = f == _filter ? _tabOn : _tabOff;

            _page = Mathf.Clamp(_page, 0, PageCount() - 1);
            for (int cell = 0; cell < BOOK_CELLS; cell++)
            {
                int i = _page * BOOK_CELLS + cell;
                bool has = i < _knownCount;
                if (bookCellRoots != null && cell < bookCellRoots.Length && bookCellRoots[cell] != null) bookCellRoots[cell].SetActive(has);
                if (!has) continue;
                int id = _known[i];
                if (bookCellIcons[cell] != null) bookCellIcons[cell].color = cardDatabase.GetColor(id);
                if (bookCellNames[cell] != null) bookCellNames[cell].text = cardDatabase.GetName(id);
            }
            if (pageLabel != null) pageLabel.text = "Page " + (_page + 1).ToString() + " / " + PageCount().ToString();

            bool show = _selected > 0;
            if (detailsPanel != null) detailsPanel.SetActive(show);
            if (!show) return;
            int inDeck = c.CountInDeck(_deck, _selected);
            if (detailName != null) detailName.text = cardDatabase.GetName(_selected);
            if (detailBody != null)
                detailBody.text = cardDatabase.GetDetails(_selected) + "\nIn this deck: " + inDeck.ToString() + " / " + CardDatabase.MAX_COPIES.ToString();
            if (addButton != null) addButton.SetActive(cardDatabase.Knows(c, _selected));
            if (removeButton != null) removeButton.SetActive(inDeck > 0);
        }

        private bool HasCharacter()
        {
            return playerState != null && playerState.HasCharacter() && playerState.current != null;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }
    }
}
