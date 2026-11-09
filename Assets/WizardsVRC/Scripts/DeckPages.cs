using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Deck tab, laid out like a card game's spell deck screen.
    ///   - Across the top: the "Spell Deck" plaque with a round icon for each school (and All / General), which filters
    ///     the spells on the right page.
    ///   - Left page: the deck item shown (flip through the decks you own with the arrows, Equip one), and every card in
    ///     it as its own square, copies side by side, then empty squares up to the deck's size. Click a square to take
    ///     that copy out; hover for the spell's details.
    ///   - Under the deck: the item spells your equipped gear adds to it in battle (a wand's basic attacks and the like),
    ///     each copy its own square; they don't use the deck's space and can't be taken out here (change the gear).
    ///   - Right page: the spells you know in the chosen school, six full cards to a page, each with how many copies the
    ///     deck holds. Click a card to put a copy in; hover for its details. Page arrows down the sides.
    /// Each deck item keeps its own cards; battles use the equipped one. Capacity and copies per spell come from the deck
    /// item (ItemDatabase.deckCapacity / deckMaxCopies).
    ///   - Treasure cards (Wizard101): the Treasure icon at the end of the header lists the treasure cards you own (from the
    ///     Library or Starshard packs); click one to move it into the shown deck's sideboard. The row under the deck switches
    ///     (Item Spells / Treasure Cards tabs) to the sideboard, each card its own square; click one to take it back out.
    ///     In battle the Treasure button draws from the equipped deck's sideboard.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DeckPages : UdonSharpBehaviour
    {
        /// <summary>Squares on the left page: every copy of every card (the biggest deck holds 52).</summary>
        public const int DECK_SLOTS = 54;
        /// <summary>Squares in the item spell row under the deck (two rows of nine).</summary>
        public const int ITEM_SLOTS = 18;
        public const int ITEM_ROW = 9;
        public const int BOOK_CELLS = 6;
        /// <summary>Filters, in the order of the icons across the top: All, then the schools, then General, then Tech (hidden).</summary>
        public const int FILTERS = 11;
        /// <summary>The Tech School's filter: its icon only shows once you know a Tech spell.</summary>
        public const int FILTER_TECH = 9;
        /// <summary>The treasure cards you own (the last icon): click one to put it in the deck's sideboard.</summary>
        public const int FILTER_TREASURE = 10;
        public const int FILTER_ALL = 0;

        public PlayerState playerState;
        public CardDatabase cardDatabase;
        public ItemDatabase itemDatabase;
        public ItemTooltip tooltip;

        [Header("Left page: the deck")]
        public Text deckNameLabel;
        public Text deckTitle;
        public GameObject equipDeckButton;
        [Tooltip("One square per card in the deck (copies side by side): its frame (school colour), art, pip cost and, without art, its name.")]
        public GameObject[] deckSlotRoots;
        public Image[] deckSlotFrames;
        public RawImage[] deckSlotArts;
        public Text[] deckSlotPips;
        public Text[] deckSlotNames;
        [Tooltip("The item spell row under the deck: the same squares, for the spells your gear adds.")]
        public Text itemLabel;
        public GameObject[] itemSlotRoots;
        public Image[] itemSlotFrames;
        public RawImage[] itemSlotArts;
        public Text[] itemSlotPips;
        public Text[] itemSlotNames;
        public Text statusLabel;
        [Tooltip("The two tabs over the row under the deck: Item Spells / Treasure Cards (their images light up).")]
        public Image itemTabImage;
        public Image treasureTabImage;

        [Header("Top: school filters")]
        [Tooltip("The round school icons across the top (FILTERS of them): their backing discs light up for the chosen one.")]
        public Image[] filterImages;
        [Tooltip("The Tech School's icon (shown once you know one of its spells) and the gem that stands in its place until then.")]
        public GameObject techFilterButton;
        public GameObject techPlaceholder;

        [Header("Right page: the spells you know")]
        public Text filterTitle;
        public GameObject[] bookCellRoots;
        public Image[] bookCellIcons;
        public Text[] bookCellNames;
        public Text[] bookCellPips;
        public RawImage[] bookCellArts;
        public Text[] bookCellAccuracies;
        public Text[] bookCellInfos;
        public Text[] bookCellSchools;
        [Tooltip("Under each card: how many copies the deck holds of it (its plaque goes green while you can add more).")]
        public Text[] bookCellCounts;
        public Image[] bookCellBadges;
        public Text pageLabel;

        private Color _tabOn = new Color(1f, 0.85f, 0.45f);
        private Color _tabOff = new Color(0.42f, 0.32f, 0.26f);
        private Color _slotEmpty = new Color(0.62f, 0.54f, 0.42f, 0.55f);
        private Color _badgeRoom = new Color(0.3f, 0.5f, 0.3f);
        private Color _badgeFull = new Color(0.55f, 0.3f, 0.22f);
        private Color _badgeNone = new Color(0.45f, 0.36f, 0.28f);

        // School shown by each filter icon (-2 = all, -1 = General): All, Fire, Ice, Storm, Myth, Life, Death, Balance, General.
        private int[] _filterSchool = { -2, 0, 1, 2, 5, 3, 4, 6, -1, CardDatabase.TECH, -3 };
        private string[] _filterNames = { "All", "Fire", "Ice", "Storm", "Myth", "Life", "Death", "Balance", "General", "Tech", "Treasure" };
        /// <summary>The row under the deck shows the sideboard (treasure cards) instead of the item spells.</summary>
        private bool _lowerTreasure;
        private int[] _side = new int[ITEM_SLOTS];
        private int _sideCount;

        private int _deckItem;
        private int[] _owned = new int[CharacterData.INVENTORY_SLOTS + 1];
        private int _ownedCount;
        private int _filter;
        private int _page;

        private int[] _slots = new int[DECK_SLOTS];   // the card in each square, copies together
        private int _slotCount;
        private int[] _unique = new int[DECK_SLOTS];
        private int _uniqueCount;
        private int[] _items = new int[ITEM_SLOTS];
        private int _itemCount;
        private int[] _known = new int[96];
        private int _knownCount;

        // ------------------------------------------------------------------ uGUI events

        public void PrevDeck() { StepDeck(-1); }
        public void NextDeck() { StepDeck(1); }

        public void Filter0() { SetFilter(0); }
        public void Filter1() { SetFilter(1); }
        public void Filter2() { SetFilter(2); }
        public void Filter3() { SetFilter(3); }
        public void Filter4() { SetFilter(4); }
        public void Filter5() { SetFilter(5); }
        public void Filter6() { SetFilter(6); }
        public void Filter7() { SetFilter(7); }
        public void Filter8() { SetFilter(8); }
        public void Filter9() { SetFilter(9); }
        public void Filter10() { SetFilter(10); }

        public void ShowItemRow() { _lowerTreasure = false; if (tooltip != null) tooltip.Hide(); Refresh(); }
        public void ShowTreasureRow() { _lowerTreasure = true; if (tooltip != null) tooltip.Hide(); Refresh(); }

        public void ItemSlot0() { ClickLower(0); }
        public void ItemSlot1() { ClickLower(1); }
        public void ItemSlot2() { ClickLower(2); }
        public void ItemSlot3() { ClickLower(3); }
        public void ItemSlot4() { ClickLower(4); }
        public void ItemSlot5() { ClickLower(5); }
        public void ItemSlot6() { ClickLower(6); }
        public void ItemSlot7() { ClickLower(7); }
        public void ItemSlot8() { ClickLower(8); }
        public void ItemSlot9() { ClickLower(9); }
        public void ItemSlot10() { ClickLower(10); }
        public void ItemSlot11() { ClickLower(11); }
        public void ItemSlot12() { ClickLower(12); }
        public void ItemSlot13() { ClickLower(13); }
        public void ItemSlot14() { ClickLower(14); }
        public void ItemSlot15() { ClickLower(15); }
        public void ItemSlot16() { ClickLower(16); }
        public void ItemSlot17() { ClickLower(17); }

        public void DeckSlot0() { RemoveSlot(0); }
        public void DeckSlot1() { RemoveSlot(1); }
        public void DeckSlot2() { RemoveSlot(2); }
        public void DeckSlot3() { RemoveSlot(3); }
        public void DeckSlot4() { RemoveSlot(4); }
        public void DeckSlot5() { RemoveSlot(5); }
        public void DeckSlot6() { RemoveSlot(6); }
        public void DeckSlot7() { RemoveSlot(7); }
        public void DeckSlot8() { RemoveSlot(8); }
        public void DeckSlot9() { RemoveSlot(9); }
        public void DeckSlot10() { RemoveSlot(10); }
        public void DeckSlot11() { RemoveSlot(11); }
        public void DeckSlot12() { RemoveSlot(12); }
        public void DeckSlot13() { RemoveSlot(13); }
        public void DeckSlot14() { RemoveSlot(14); }
        public void DeckSlot15() { RemoveSlot(15); }
        public void DeckSlot16() { RemoveSlot(16); }
        public void DeckSlot17() { RemoveSlot(17); }
        public void DeckSlot18() { RemoveSlot(18); }
        public void DeckSlot19() { RemoveSlot(19); }
        public void DeckSlot20() { RemoveSlot(20); }
        public void DeckSlot21() { RemoveSlot(21); }
        public void DeckSlot22() { RemoveSlot(22); }
        public void DeckSlot23() { RemoveSlot(23); }
        public void DeckSlot24() { RemoveSlot(24); }
        public void DeckSlot25() { RemoveSlot(25); }
        public void DeckSlot26() { RemoveSlot(26); }
        public void DeckSlot27() { RemoveSlot(27); }
        public void DeckSlot28() { RemoveSlot(28); }
        public void DeckSlot29() { RemoveSlot(29); }
        public void DeckSlot30() { RemoveSlot(30); }
        public void DeckSlot31() { RemoveSlot(31); }
        public void DeckSlot32() { RemoveSlot(32); }
        public void DeckSlot33() { RemoveSlot(33); }
        public void DeckSlot34() { RemoveSlot(34); }
        public void DeckSlot35() { RemoveSlot(35); }
        public void DeckSlot36() { RemoveSlot(36); }
        public void DeckSlot37() { RemoveSlot(37); }
        public void DeckSlot38() { RemoveSlot(38); }
        public void DeckSlot39() { RemoveSlot(39); }
        public void DeckSlot40() { RemoveSlot(40); }
        public void DeckSlot41() { RemoveSlot(41); }
        public void DeckSlot42() { RemoveSlot(42); }
        public void DeckSlot43() { RemoveSlot(43); }
        public void DeckSlot44() { RemoveSlot(44); }
        public void DeckSlot45() { RemoveSlot(45); }
        public void DeckSlot46() { RemoveSlot(46); }
        public void DeckSlot47() { RemoveSlot(47); }
        public void DeckSlot48() { RemoveSlot(48); }
        public void DeckSlot49() { RemoveSlot(49); }
        public void DeckSlot50() { RemoveSlot(50); }
        public void DeckSlot51() { RemoveSlot(51); }
        public void DeckSlot52() { RemoveSlot(52); }
        public void DeckSlot53() { RemoveSlot(53); }

        public void HoverSlot0() { HoverSlot(0); }
        public void HoverSlot1() { HoverSlot(1); }
        public void HoverSlot2() { HoverSlot(2); }
        public void HoverSlot3() { HoverSlot(3); }
        public void HoverSlot4() { HoverSlot(4); }
        public void HoverSlot5() { HoverSlot(5); }
        public void HoverSlot6() { HoverSlot(6); }
        public void HoverSlot7() { HoverSlot(7); }
        public void HoverSlot8() { HoverSlot(8); }
        public void HoverSlot9() { HoverSlot(9); }
        public void HoverSlot10() { HoverSlot(10); }
        public void HoverSlot11() { HoverSlot(11); }
        public void HoverSlot12() { HoverSlot(12); }
        public void HoverSlot13() { HoverSlot(13); }
        public void HoverSlot14() { HoverSlot(14); }
        public void HoverSlot15() { HoverSlot(15); }
        public void HoverSlot16() { HoverSlot(16); }
        public void HoverSlot17() { HoverSlot(17); }
        public void HoverSlot18() { HoverSlot(18); }
        public void HoverSlot19() { HoverSlot(19); }
        public void HoverSlot20() { HoverSlot(20); }
        public void HoverSlot21() { HoverSlot(21); }
        public void HoverSlot22() { HoverSlot(22); }
        public void HoverSlot23() { HoverSlot(23); }
        public void HoverSlot24() { HoverSlot(24); }
        public void HoverSlot25() { HoverSlot(25); }
        public void HoverSlot26() { HoverSlot(26); }
        public void HoverSlot27() { HoverSlot(27); }
        public void HoverSlot28() { HoverSlot(28); }
        public void HoverSlot29() { HoverSlot(29); }
        public void HoverSlot30() { HoverSlot(30); }
        public void HoverSlot31() { HoverSlot(31); }
        public void HoverSlot32() { HoverSlot(32); }
        public void HoverSlot33() { HoverSlot(33); }
        public void HoverSlot34() { HoverSlot(34); }
        public void HoverSlot35() { HoverSlot(35); }
        public void HoverSlot36() { HoverSlot(36); }
        public void HoverSlot37() { HoverSlot(37); }
        public void HoverSlot38() { HoverSlot(38); }
        public void HoverSlot39() { HoverSlot(39); }
        public void HoverSlot40() { HoverSlot(40); }
        public void HoverSlot41() { HoverSlot(41); }
        public void HoverSlot42() { HoverSlot(42); }
        public void HoverSlot43() { HoverSlot(43); }
        public void HoverSlot44() { HoverSlot(44); }
        public void HoverSlot45() { HoverSlot(45); }
        public void HoverSlot46() { HoverSlot(46); }
        public void HoverSlot47() { HoverSlot(47); }
        public void HoverSlot48() { HoverSlot(48); }
        public void HoverSlot49() { HoverSlot(49); }
        public void HoverSlot50() { HoverSlot(50); }
        public void HoverSlot51() { HoverSlot(51); }
        public void HoverSlot52() { HoverSlot(52); }
        public void HoverSlot53() { HoverSlot(53); }

        public void BookCell0() { AddCell(0); }
        public void BookCell1() { AddCell(1); }
        public void BookCell2() { AddCell(2); }
        public void BookCell3() { AddCell(3); }
        public void BookCell4() { AddCell(4); }
        public void BookCell5() { AddCell(5); }

        public void HoverBook0() { HoverBook(0); }
        public void HoverBook1() { HoverBook(1); }
        public void HoverBook2() { HoverBook(2); }
        public void HoverBook3() { HoverBook(3); }
        public void HoverBook4() { HoverBook(4); }
        public void HoverBook5() { HoverBook(5); }

        public void HoverItem0() { HoverItem(0); }
        public void HoverItem1() { HoverItem(1); }
        public void HoverItem2() { HoverItem(2); }
        public void HoverItem3() { HoverItem(3); }
        public void HoverItem4() { HoverItem(4); }
        public void HoverItem5() { HoverItem(5); }
        public void HoverItem6() { HoverItem(6); }
        public void HoverItem7() { HoverItem(7); }
        public void HoverItem8() { HoverItem(8); }
        public void HoverItem9() { HoverItem(9); }
        public void HoverItem10() { HoverItem(10); }
        public void HoverItem11() { HoverItem(11); }
        public void HoverItem12() { HoverItem(12); }
        public void HoverItem13() { HoverItem(13); }
        public void HoverItem14() { HoverItem(14); }
        public void HoverItem15() { HoverItem(15); }
        public void HoverItem16() { HoverItem(16); }
        public void HoverItem17() { HoverItem(17); }

        public void Unhover() { if (tooltip != null) tooltip.Hide(); }

        public void PrevPage()
        {
            _page = _page > 0 ? _page - 1 : PageCount() - 1;
            Refresh();
        }

        public void NextPage()
        {
            _page = _page < PageCount() - 1 ? _page + 1 : 0;
            Refresh();
        }

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

        /// <summary>Called by InventoryBook when the Deck tab is opened.</summary>
        public void Open()
        {
            if (HasCharacter()) _deckItem = playerState.current.equipped[CharacterData.EQUIP_DECK];
            SetStatus("");
            Refresh();
        }

        // ------------------------------------------------------------------ adding and taking out

        /// <summary>A card on the right page clicked: one more copy of it into the deck shown.</summary>
        private void AddCell(int cell)
        {
            int i = _page * BOOK_CELLS + cell;
            if (!HasCharacter() || i >= _knownCount) return;
            int card = _known[i];
            CharacterData c = playerState.current;
            if (_filter == FILTER_TREASURE) { AddTreasure(c, card); return; }
            if (!cardDatabase.Knows(c, card)) { SetStatus("You haven't learned that spell yet."); return; }
            if (_deckItem <= 0) { SetStatus("You don't own a deck. Find one, then equip it."); return; }
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            int list = c.DeckListFor(_deckItem, true);
            if (list < 0) { SetStatus("Your spellbook can only keep " + CharacterData.DECK_COUNT.ToString() + " decks."); return; }
            int cap = itemDatabase.GetDeckCapacity(_deckItem);
            int copies = itemDatabase.GetDeckMaxCopies(_deckItem);
            if (c.DeckCount(list) >= cap) { SetStatus("This deck is full (" + cap.ToString() + " spells)."); return; }
            if (c.CountInDeck(list, card) >= copies) { SetStatus("This deck holds at most " + copies.ToString() + " copies of one spell."); return; }
            if (!c.AddToDeck(list, card)) return;
            playerState.MarkDirty();
            SetStatus("Added " + cardDatabase.GetName(card) + " (" + c.CountInDeck(list, card).ToString() + " / " + copies.ToString() + ").");
            Refresh();
        }

        /// <summary>A square on the left page clicked: that copy comes out of the deck.</summary>
        private void RemoveSlot(int slot)
        {
            if (!HasCharacter() || slot >= _slotCount) return;
            int card = _slots[slot];
            CharacterData c = playerState.current;
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            int list = c.DeckListFor(_deckItem, false);
            if (list < 0 || !c.RemoveFromDeck(list, card)) return;
            playerState.MarkDirty();
            if (tooltip != null) tooltip.Hide();
            SetStatus("Took out " + cardDatabase.GetName(card) + ".");
            Refresh();
        }

        /// <summary>A treasure card on the right page clicked: one copy moves from your collection into the deck's sideboard.</summary>
        private void AddTreasure(CharacterData c, int card)
        {
            if (_deckItem <= 0) { SetStatus("You don't own a deck. Find one, then equip it."); return; }
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            if (c.TreasureCount(card) <= 0) { SetStatus("All your " + cardDatabase.GetName(card) + " treasure cards are already in sideboards."); return; }
            int list = c.DeckListFor(_deckItem, true);
            if (list < 0) { SetStatus("Your spellbook can only keep " + CharacterData.DECK_COUNT.ToString() + " decks."); return; }
            int cap = itemDatabase.GetDeckTreasureCapacity(_deckItem);
            if (c.SideCount(list) >= cap) { SetStatus("This deck's sideboard is full (" + cap.ToString() + " treasure cards)."); return; }
            if (!c.MoveToSide(list, card)) return;
            _lowerTreasure = true;
            playerState.MarkDirty();
            SetStatus("Put a " + cardDatabase.GetName(card) + " treasure card in the sideboard (" + c.SideCount(list).ToString() + " / " + cap.ToString() + ").");
            Refresh();
        }

        /// <summary>A square in the row under the deck clicked: a sideboard card goes back to your collection; an item spell shows its details.</summary>
        private void ClickLower(int slot)
        {
            if (!HasCharacter()) return;
            if (!_lowerTreasure) { HoverItem(slot); return; }
            if (slot >= _sideCount) return;
            if (playerState.inCombat) { SetStatus("You can't change decks in the middle of a duel."); return; }
            CharacterData c = playerState.current;
            int list = c.DeckListFor(_deckItem, false);
            int card = _side[slot];
            if (list < 0 || !c.TakeFromSide(list, card, true)) return;
            playerState.MarkDirty();
            if (tooltip != null) tooltip.Hide();
            SetStatus("Took the " + cardDatabase.GetName(card) + " treasure card out of the sideboard.");
            Refresh();
        }

        private void HoverSlot(int slot)
        {
            if (tooltip == null || slot >= _slotCount || deckSlotRoots == null || slot >= deckSlotRoots.Length) return;
            tooltip.ShowSpell(_slots[slot], deckSlotRoots[slot].transform);
        }

        private void HoverItem(int slot)
        {
            if (tooltip == null || itemSlotRoots == null || slot >= itemSlotRoots.Length) return;
            if (_lowerTreasure) { if (slot < _sideCount) tooltip.ShowSpell(_side[slot], itemSlotRoots[slot].transform); return; }
            if (slot >= _itemCount) return;
            tooltip.ShowSpell(_items[slot], itemSlotRoots[slot].transform);
        }

        private void HoverBook(int cell)
        {
            int i = _page * BOOK_CELLS + cell;
            if (tooltip == null || i >= _knownCount || bookCellRoots == null || cell >= bookCellRoots.Length) return;
            tooltip.ShowSpell(_known[i], bookCellRoots[cell].transform);
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
                if (!seen && _ownedCount < _owned.Length) _owned[_ownedCount++] = id;
            }
        }

        private void SetFilter(int filter)
        {
            _filter = Mathf.Clamp(filter, 0, FILTERS - 1);
            _page = 0;
            if (_filter == FILTER_TREASURE) _lowerTreasure = true;
            if (tooltip != null) tooltip.Hide();
            Refresh();
        }

        private int PageCount()
        {
            return Mathf.Max(1, (_knownCount + BOOK_CELLS - 1) / BOOK_CELLS);
        }

        private void BuildLists(CharacterData c)
        {
            // The hidden Tech School: no tab (nor its filter) until you know one of its spells.
            bool tech = cardDatabase.KnowsAnyOf(c, CardDatabase.TECH);
            if (techFilterButton != null && techFilterButton.activeSelf != tech) techFilterButton.SetActive(tech);
            if (techPlaceholder != null && techPlaceholder.activeSelf == tech) techPlaceholder.SetActive(!tech);
            if (!tech && _filter == FILTER_TECH) _filter = FILTER_ALL;
            BuildOwned(c);
            bool stillOwned = false;
            for (int i = 0; i < _ownedCount; i++) if (_owned[i] == _deckItem) stillOwned = true;
            if (!stillOwned) _deckItem = _ownedCount > 0 ? _owned[0] : 0;

            // The deck's cards, each spell's copies together, in the order the spells were first put in.
            _uniqueCount = 0;
            _slotCount = 0;
            int list = c.DeckListFor(_deckItem, false);
            int len = list >= 0 ? c.DeckCount(list) : 0;
            for (int k = 0; k < len; k++)
            {
                int id = c.deckCards[list * CharacterData.DECK_SIZE + k];
                bool seen = false;
                for (int u = 0; u < _uniqueCount; u++)
                    if (_unique[u] == id) { seen = true; break; }
                if (!seen && _uniqueCount < _unique.Length) _unique[_uniqueCount++] = id;
            }
            for (int u = 0; u < _uniqueCount; u++)
            {
                int n = c.CountInDeck(list, _unique[u]);
                for (int k = 0; k < n && _slotCount < DECK_SLOTS; k++) _slots[_slotCount++] = _unique[u];
            }

            // The item spells your gear adds in battle.
            _itemCount = itemDatabase != null ? itemDatabase.EquippedItemCards(c, _items) : 0;
            // The shown deck's sideboard (treasure cards).
            _sideCount = 0;
            int side = list >= 0 ? c.SideCount(list) : 0;
            for (int k = 0; k < side && _sideCount < _side.Length; k++) _side[_sideCount++] = c.SideCard(list, k);

            // The spells you know in the chosen school (school spells first when showing all, cheapest first as listed).
            _knownCount = 0;
            int want = _filterSchool[_filter];
            int total = cardDatabase.Count();
            if (want == -3)
            {
                // Treasure cards: the ones in your collection, and any in this deck's sideboard.
                for (int i = 0; i < total && _knownCount < _known.Length; i++)
                {
                    int id = cardDatabase.IdAt(i);
                    if (c.TreasureCount(id) > 0 || (list >= 0 && c.CountInSide(list, id) > 0)) _known[_knownCount++] = id;
                }
                return;
            }
            for (int i = 0; i < total && _knownCount < _known.Length; i++)
            {
                int id = cardDatabase.IdAt(i);
                if (!cardDatabase.Knows(c, id)) continue;
                int school = cardDatabase.GetSchool(id);
                if (want == -2 || school == want) _known[_knownCount++] = id;
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
            bool equipped = _deckItem > 0 && c.equipped[CharacterData.EQUIP_DECK] == _deckItem;
            int cap = _deckItem > 0 ? itemDatabase.GetDeckCapacity(_deckItem) : 0;
            int copies = _deckItem > 0 ? itemDatabase.GetDeckMaxCopies(_deckItem) : 0;
            if (deckNameLabel != null)
                deckNameLabel.text = _deckItem > 0 ? itemDatabase.GetName(_deckItem) + (equipped ? "  (equipped)" : "") : "No deck";
            if (deckTitle != null)
                deckTitle.text = _deckItem > 0
                    ? _slotCount.ToString() + " / " + cap.ToString() + " spells   -   up to " + copies.ToString() + " of each" + ItemCardNote(c) + (_ownedCount > 1 ? "   -   " + _ownedCount.ToString() + " decks" : "")
                    : "Find a deck and equip it.";
            if (equipDeckButton != null) equipDeckButton.SetActive(_deckItem > 0 && !equipped);

            // ---- the deck's squares: cards, then empty slots up to its size
            int shown = Mathf.Min(DECK_SLOTS, Mathf.Max(cap, _slotCount));
            for (int slot = 0; slot < DECK_SLOTS; slot++)
            {
                GameObject root = deckSlotRoots != null && slot < deckSlotRoots.Length ? deckSlotRoots[slot] : null;
                if (root == null) continue;
                bool on = slot < shown;
                if (root.activeSelf != on) root.SetActive(on);
                if (on) DrawSquare(ImageAt(deckSlotFrames, slot), ArtAt(deckSlotArts, slot), TextAt(deckSlotPips, slot), TextAt(deckSlotNames, slot), slot < _slotCount ? _slots[slot] : 0);
            }

            // ---- the item spells from your gear: a row of nine squares (two when there are more)
            int itemTotal = itemDatabase != null ? itemDatabase.EquippedItemCardCount(c) : 0;
            int sideCap = _deckItem > 0 ? itemDatabase.GetDeckTreasureCapacity(_deckItem) : 0;
            if (itemLabel != null)
            {
                if (_lowerTreasure)
                    itemLabel.text = _deckItem > 0 ? "Sideboard: " + _sideCount.ToString() + " / " + sideCap.ToString() + " treasure cards  (click one to take it out)" : "Sideboard: no deck";
                else
                    itemLabel.text = itemTotal > 0 ? "Item spells from your gear: " + itemTotal.ToString() + "  (added in battle)" : "Item spells from your gear: none";
            }
            if (itemTabImage != null) itemTabImage.color = _lowerTreasure ? _tabOff : _tabOn;
            if (treasureTabImage != null) treasureTabImage.color = _lowerTreasure ? _tabOn : _tabOff;
            int lowerCount = _lowerTreasure ? _sideCount : _itemCount;
            int itemShown = _lowerTreasure ? Mathf.Max(sideCap, _sideCount) : (_itemCount > ITEM_ROW ? ITEM_SLOTS : ITEM_ROW);
            itemShown = Mathf.Clamp(itemShown, ITEM_ROW, ITEM_SLOTS);
            if (_lowerTreasure && itemShown > ITEM_ROW) itemShown = ITEM_SLOTS;
            for (int slot = 0; slot < ITEM_SLOTS; slot++)
            {
                GameObject root = itemSlotRoots != null && slot < itemSlotRoots.Length ? itemSlotRoots[slot] : null;
                if (root == null) continue;
                bool on = slot < itemShown;
                if (root.activeSelf != on) root.SetActive(on);
                int id = slot < lowerCount ? (_lowerTreasure ? _side[slot] : _items[slot]) : 0;
                // Squares past the sideboard's size are dimmed further (no room there).
                if (on) DrawSquare(ImageAt(itemSlotFrames, slot), ArtAt(itemSlotArts, slot), TextAt(itemSlotPips, slot), TextAt(itemSlotNames, slot), id);
                if (on && _lowerTreasure && id == 0 && slot >= sideCap && itemSlotFrames != null && slot < itemSlotFrames.Length && itemSlotFrames[slot] != null)
                    itemSlotFrames[slot].color = new Color(0.4f, 0.34f, 0.28f, 0.25f);
            }

            // ---- the school icons across the top
            if (filterImages != null)
                for (int f = 0; f < filterImages.Length; f++)
                    if (filterImages[f] != null) filterImages[f].color = f == _filter ? _tabOn : _tabOff;
            if (filterTitle != null)
                filterTitle.text = (_filter == FILTER_ALL ? "All your spells" : _filter == FILTER_TREASURE ? "Your treasure cards" : _filterNames[_filter] + " spells") +
                                   "   (" + _knownCount.ToString() + ")";

            // ---- the spells you know, six to a page
            _page = Mathf.Clamp(_page, 0, PageCount() - 1);
            for (int cell = 0; cell < BOOK_CELLS; cell++)
            {
                int i = _page * BOOK_CELLS + cell;
                bool has = i < _knownCount;
                GameObject root = bookCellRoots != null && cell < bookCellRoots.Length ? bookCellRoots[cell] : null;
                if (root != null && root.activeSelf != has) root.SetActive(has);
                Text count = TextAt(bookCellCounts, cell);
                Image badge = ImageAt(bookCellBadges, cell);
                if (badge != null && badge.gameObject.activeSelf != has) badge.gameObject.SetActive(has);
                if (!has) continue;
                int id = _known[i];
                cardDatabase.FillCard(id, ImageAt(bookCellIcons, cell), TextAt(bookCellNames, cell), TextAt(bookCellPips, cell), ArtAt(bookCellArts, cell),
                    TextAt(bookCellAccuracies, cell), TextAt(bookCellInfos, cell), TextAt(bookCellSchools, cell), false);
                if (_filter == FILTER_TREASURE)
                {
                    int have = c.TreasureCount(id);
                    int inSide = list >= 0 ? c.CountInSide(list, id) : 0;
                    if (count != null) count.text = "Have " + have.ToString() + "   Sideboard " + inSide.ToString();
                    if (badge != null) badge.color = have > 0 && _deckItem > 0 && _sideCount < sideCap ? _badgeRoom : inSide > 0 ? _badgeFull : _badgeNone;
                    continue;
                }
                int inDeck = list >= 0 ? c.CountInDeck(list, id) : 0;
                if (count != null) count.text = _deckItem > 0 ? "In deck: " + inDeck.ToString() + " / " + copies.ToString() : "No deck";
                if (badge != null)
                    badge.color = _deckItem <= 0 ? _badgeNone : (inDeck >= copies || _slotCount >= cap) ? _badgeFull : inDeck > 0 ? _badgeRoom : _badgeNone;
            }
            if (pageLabel != null) pageLabel.text = "Page " + (_page + 1).ToString() + " / " + PageCount().ToString();
        }

        /// <summary>One square: the card's school colour, its art (the middle of a wide picture), pip cost, and its name when it has no art; 0 = an empty slot.</summary>
        private void DrawSquare(Image frame, RawImage art, Text pips, Text nm, int id)
        {
            if (id == 0)
            {
                if (frame != null) frame.color = _slotEmpty;
                if (art != null && art.gameObject.activeSelf) art.gameObject.SetActive(false);
                if (pips != null) pips.text = "";
                if (nm != null) nm.text = "";
                return;
            }
            if (frame != null) frame.color = cardDatabase.GetColor(id);
            Texture t = cardDatabase.ArtFor(id);
            if (art != null)
            {
                art.texture = t;
                if (t != null) art.uvRect = t.width > t.height ? new Rect(0.5f - 0.5f * t.height / t.width, 0f, (float)t.height / t.width, 1f) : new Rect(0f, 0f, 1f, 1f);
                if (art.gameObject.activeSelf != (t != null)) art.gameObject.SetActive(t != null);
            }
            if (pips != null) pips.text = cardDatabase.GetPipCost(id).ToString();
            if (nm != null) nm.text = t == null ? cardDatabase.GetName(id) : "";
        }

        private Image ImageAt(Image[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : null; }
        private Text TextAt(Text[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : null; }
        private RawImage ArtAt(RawImage[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : null; }

        private bool HasCharacter()
        {
            return playerState != null && playerState.HasCharacter() && playerState.current != null;
        }

        /// <summary>A message under the deck; with none, how the page works.</summary>
        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message.Length > 0 ? message : "<color=#8A7458>Click a card in the deck to take it out. Point at any card for its details.</color>";
        }

        /// <summary>"   -   + 7 item spells from your gear" (item cards), or empty.</summary>
        private string ItemCardNote(CharacterData c)
        {
            int n = itemDatabase != null ? itemDatabase.EquippedItemCardCount(c) : 0;
            return n > 0 ? "   -   + " + n.ToString() + " item spell" + (n > 1 ? "s" : "") + " from gear" : "";
        }
    }
}
