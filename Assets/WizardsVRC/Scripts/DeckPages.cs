using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Deck tab. Left page: one of your deck items (unique cards with copy counts), arrows to flip through
    /// the decks you own, and Equip. Right page: the spellbook (known spells, filter tabs, paged grid) and a details box
    /// with Add/Remove. Each deck item keeps its own cards; battles use the equipped one. Capacity and copies per spell
    /// come from the deck item (ItemDatabase.deckCapacity / deckMaxCopies).
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
        public ItemDatabase itemDatabase;

        [Header("Left page: deck")]
        public Text deckTitle;
        public Text deckNameLabel;
        public GameObject equipDeckButton;
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

        private int _deckItem;
        private int[] _owned = new int[CharacterData.INVENTORY_SLOTS + 1];
        private int _ownedCount;
        private int _filter;
        private int _page;
        private int _selected;

        private int[] _deckUnique = new int[DECK_CELLS];
        private int _deckUniqueCount;
        private int[] _known = new int[64];
        private int _knownCount;

        // ------------------------------------------------------------------ uGUI events

        public void PrevDeck() { StepDeck(-1); }
        public void NextDeck() { StepDeck(1); }

        /// <summary>Equip the deck shown on the left page (battles draw from it).</summary>
        public void EquipShownDeck()
        {
            if (!HasCharacter() || _deckItem <= 0) return;
            CharacterData c = playerState.current;
            if (c.equipped[CharacterData.EQUIP_DECK] == _deckItem) return;
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            string reason = itemDatabase.CheckCanEquip(c, _deckItem);
            if (reason != "") { SetStatus(reason); return; }
            if (c.DeckListFor(_deckItem, false) < 0 && !c.CanClaimDeckList()) { SetStatus("Your spellbook can only keep " + CharacterData.DECK_COUNT.ToString() + " decks."); return; }
            int inv = c.FindItem(_deckItem);
            if (inv < 0 || !c.RemoveItemAt(inv, 1)) return;
            int previous = c.equipped[CharacterData.EQUIP_DECK];
            c.equipped[CharacterData.EQUIP_DECK] = (ushort)_deckItem;
            if (previous != 0) c.AddItem(previous, 1);
            c.DeckListFor(_deckItem, true);
            playerState.MarkDirty();
            SetStatus("Equipped " + itemDatabase.GetName(_deckItem) + ".");
            Refresh();
        }

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
            if (_deckItem <= 0) { SetStatus("You don't own a deck. Find one, then equip it."); return; }
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            int list = c.DeckListFor(_deckItem, true);
            if (list < 0) { SetStatus("Your spellbook can only keep " + CharacterData.DECK_COUNT.ToString() + " decks."); return; }
            int cap = itemDatabase.GetDeckCapacity(_deckItem);
            int copies = itemDatabase.GetDeckMaxCopies(_deckItem);
            if (c.DeckCount(list) >= cap) { SetStatus("This deck is full (" + cap.ToString() + " spells)."); return; }
            if (c.CountInDeck(list, _selected) >= copies) { SetStatus("This deck holds at most " + copies.ToString() + " copies of one spell."); return; }
            if (!c.AddToDeck(list, _selected)) return;
            playerState.MarkDirty();
            SetStatus("Added " + cardDatabase.GetName(_selected) + ".");
            Refresh();
        }

        public void RemoveSelected()
        {
            if (!HasCharacter() || _selected <= 0) return;
            CharacterData c = playerState.current;
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            int list = c.DeckListFor(_deckItem, false);
            if (list < 0 || !c.RemoveFromDeck(list, _selected)) return;
            playerState.MarkDirty();
            SetStatus("Removed " + cardDatabase.GetName(_selected) + ".");
            Refresh();
        }

        /// <summary>Called by InventoryBook when the Deck tab is opened.</summary>
        public void Open()
        {
            if (HasCharacter()) _deckItem = playerState.current.equipped[CharacterData.EQUIP_DECK];
            _selected = 0;
            SetStatus("");
            Refresh();
        }

        // ------------------------------------------------------------------ state

        /// <summary>Flip to the previous/next deck item you own (equipped first, then the backpack).</summary>
        private void StepDeck(int dir)
        {
            if (!HasCharacter()) return;
            BuildOwned(playerState.current);
            if (_ownedCount == 0) return;
            int at = 0;
            for (int i = 0; i < _ownedCount; i++) if (_owned[i] == _deckItem) at = i;
            at = (at + dir + _ownedCount) % _ownedCount;
            _deckItem = _owned[at];
            _selected = 0;
            SetStatus("");
            Refresh();
        }

        private void BuildOwned(CharacterData c)
        {
            _ownedCount = 0;
            int eq = c.equipped[CharacterData.EQUIP_DECK];
            if (eq > 0) _owned[_ownedCount++] = eq;
            int used = c.UsedInventorySlots();
            for (int i = 0; i < used; i++)
            {
                int id = c.inventoryItemIds[i];
                if (id == 0 || c.inventoryCounts[i] == 0 || itemDatabase == null || !itemDatabase.IsDeck(id)) continue;
                bool seen = false;
                for (int k = 0; k < _ownedCount; k++) if (_owned[k] == id) seen = true;
                if (!seen) _owned[_ownedCount++] = id;
            }
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
            BuildOwned(c);
            bool stillOwned = false;
            for (int i = 0; i < _ownedCount; i++) if (_owned[i] == _deckItem) stillOwned = true;
            if (!stillOwned) _deckItem = _ownedCount > 0 ? _owned[0] : 0;
            int list = c.DeckListFor(_deckItem, false);
            int len = list >= 0 ? c.DeckCount(list) : 0;
            for (int k = 0; k < len; k++)
            {
                int id = c.deckCards[list * CharacterData.DECK_SIZE + k];
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

            int list = c.DeckListFor(_deckItem, false);
            int count = list >= 0 ? c.DeckCount(list) : 0;
            bool equipped = _deckItem > 0 && c.equipped[CharacterData.EQUIP_DECK] == _deckItem;
            int cap = _deckItem > 0 ? itemDatabase.GetDeckCapacity(_deckItem) : 0;
            int copies = _deckItem > 0 ? itemDatabase.GetDeckMaxCopies(_deckItem) : 0;
            if (deckNameLabel != null)
                deckNameLabel.text = _deckItem > 0 ? itemDatabase.GetName(_deckItem) + (equipped ? "  (equipped)" : "") : "No deck";
            if (deckTitle != null)
                deckTitle.text = _deckItem > 0
                    ? count.ToString() + " / " + cap.ToString() + " spells, up to " + copies.ToString() + " copies each" + ItemCardNote(c) + (_ownedCount > 1 ? "   (" + _ownedCount.ToString() + " decks owned)" : "")
                    : "Find a deck and equip it.";
            if (equipDeckButton != null) equipDeckButton.SetActive(_deckItem > 0 && !equipped);

            for (int cell = 0; cell < DECK_CELLS; cell++)
            {
                bool has = cell < _deckUniqueCount;
                if (deckCellRoots != null && cell < deckCellRoots.Length && deckCellRoots[cell] != null) deckCellRoots[cell].SetActive(has);
                if (!has) continue;
                int id = _deckUnique[cell];
                if (deckCellIcons[cell] != null) deckCellIcons[cell].color = cardDatabase.GetColor(id);
                if (deckCellNames[cell] != null) deckCellNames[cell].text = cardDatabase.GetName(id);
                if (deckCellCounts[cell] != null) deckCellCounts[cell].text = "x" + c.CountInDeck(list, id).ToString();
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
            int inDeck = list >= 0 ? c.CountInDeck(list, _selected) : 0;
            if (detailName != null) detailName.text = cardDatabase.GetName(_selected);
            if (detailBody != null)
                detailBody.text = cardDatabase.GetDetails(_selected) + "\nIn this deck: " + inDeck.ToString() + " / " + copies.ToString();
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

        /// <summary>"  + 7 item spells from your gear" (Wizard101 item cards), or empty.</summary>
        private string ItemCardNote(CharacterData c)
        {
            int n = itemDatabase != null ? itemDatabase.EquippedItemCardCount(c) : 0;
            return n > 0 ? "  + " + n.ToString() + " item spell" + (n > 1 ? "s" : "") + " from your gear" : "";
        }
    }
}
