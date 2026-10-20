using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The backpack "book". Left page: character info, 7 equipment slots and stat totals.
    /// Right page: category tabs, a paged item grid and an item details box with Equip/Unequip.
    /// Lives on an always-active object; <see cref="bookRoot"/> is the canvas that opens in front of the player.
    /// Open with B on desktop, the hub button, or the backpack pedestal.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InventoryBook : UdonSharpBehaviour
    {
        public const int TAB_ALL = 0;
        public const int TAB_COUNT = 1 + ItemDatabase.CATEGORY_COUNT;
        public const int CELLS_PER_PAGE = 12;

        [Header("Systems")]
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public GameObject bookRoot;
        public float openDistance = 0.85f;
        public KeyCode toggleKey = KeyCode.B;

        [Header("Left page")]
        public Text nameLabel;
        public Text infoLabel;
        public Text goldLabel;
        public Text statsLabel;
        public Text statusLabel;
        public Image[] slotIcons;
        public Text[] slotLabels;

        [Header("Right page")]
        public Image[] tabImages;
        public GameObject[] cellRoots;
        public Image[] cellIcons;
        public Text[] cellNames;
        public Text[] cellCounts;
        public Text pageLabel;

        [Header("Details")]
        public GameObject detailsPanel;
        public Text detailName;
        public Text detailBody;
        public GameObject actionButton;
        public Text actionLabel;

        private Color _tabOn = new Color(0.85f, 0.65f, 0.3f);
        private Color _tabOff = new Color(0.45f, 0.3f, 0.18f);
        private Color _emptySlot = new Color(0.55f, 0.48f, 0.38f);

        private int _tab;
        private int _page;
        private int[] _visible = new int[CharacterData.INVENTORY_SLOTS];
        private int _visibleCount;

        // Selection: an inventory index (_selInv) or an equipment slot (_selSlot); -1 = none.
        private int _selInv = -1;
        private int _selSlot = -1;

        private bool _open;

        private void Start()
        {
            if (bookRoot != null) bookRoot.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) ToggleBook();
            if (_open && (playerState == null || !playerState.HasCharacter())) CloseBook();
        }

        // ------------------------------------------------------------------ open / close

        public void ToggleBook()
        {
            if (_open) CloseBook();
            else OpenBook();
        }

        public void OpenBook()
        {
            if (playerState == null || !playerState.HasCharacter() || bookRoot == null) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                bookRoot.transform.position = head.position + forward * openDistance + Vector3.down * 0.12f;
                bookRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            _open = true;
            bookRoot.SetActive(true);
            ClearSelection();
            SetStatus("");
            Refresh();
        }

        public void CloseBook()
        {
            _open = false;
            if (bookRoot != null) bookRoot.SetActive(false);
            if (playerState != null && playerState.IsDirty()) playerState.SaveNow();
        }

        // ------------------------------------------------------------------ uGUI events

        public void Tab0() { SetTab(0); }
        public void Tab1() { SetTab(1); }
        public void Tab2() { SetTab(2); }
        public void Tab3() { SetTab(3); }
        public void Tab4() { SetTab(4); }
        public void Tab5() { SetTab(5); }
        public void Tab6() { SetTab(6); }
        public void Tab7() { SetTab(7); }
        public void Tab8() { SetTab(8); }

        public void Cell0() { SelectCell(0); }
        public void Cell1() { SelectCell(1); }
        public void Cell2() { SelectCell(2); }
        public void Cell3() { SelectCell(3); }
        public void Cell4() { SelectCell(4); }
        public void Cell5() { SelectCell(5); }
        public void Cell6() { SelectCell(6); }
        public void Cell7() { SelectCell(7); }
        public void Cell8() { SelectCell(8); }
        public void Cell9() { SelectCell(9); }
        public void Cell10() { SelectCell(10); }
        public void Cell11() { SelectCell(11); }

        public void Slot0() { SelectSlot(0); }
        public void Slot1() { SelectSlot(1); }
        public void Slot2() { SelectSlot(2); }
        public void Slot3() { SelectSlot(3); }
        public void Slot4() { SelectSlot(4); }
        public void Slot5() { SelectSlot(5); }
        public void Slot6() { SelectSlot(6); }

        public void PrevPage()
        {
            if (_page > 0) _page--;
            ClearSelection();
            Refresh();
        }

        public void NextPage()
        {
            if (_page < PageCount() - 1) _page++;
            ClearSelection();
            Refresh();
        }

        /// <summary>The details box button: Equip for a backpack item, Unequip for a worn item.</summary>
        public void DoAction()
        {
            if (!HasCharacter()) return;
            if (_selSlot >= 0) Unequip(_selSlot);
            else if (_selInv >= 0) Equip(_selInv);
            Refresh();
        }

        // ------------------------------------------------------------------ equipment

        private void Equip(int invIndex)
        {
            CharacterData c = playerState.current;
            int itemId = c.inventoryItemIds[invIndex];
            string reason = itemDatabase.CheckCanEquip(c, itemId);
            if (reason != "")
            {
                SetStatus(reason);
                return;
            }
            int slot = itemDatabase.GetCategory(itemId);
            int previous = c.equipped[slot];
            if (!c.RemoveItemAt(invIndex, 1)) return;
            c.equipped[slot] = (ushort)itemId;
            if (previous != 0 && !c.AddItem(previous, 1))
            {
                // No room for the old item: undo.
                c.equipped[slot] = (ushort)previous;
                c.AddItem(itemId, 1);
                SetStatus("Your backpack is full.");
                return;
            }
            playerState.MarkDirty();
            SetStatus("Equipped " + itemDatabase.GetName(itemId) + ".");
            ClearSelection();
            _selSlot = slot;
        }

        private void Unequip(int slot)
        {
            CharacterData c = playerState.current;
            int itemId = c.equipped[slot];
            if (itemId == 0) return;
            if (!c.AddItem(itemId, 1))
            {
                SetStatus("Your backpack is full.");
                return;
            }
            c.equipped[slot] = 0;
            playerState.MarkDirty();
            SetStatus("Unequipped " + itemDatabase.GetName(itemId) + ".");
            ClearSelection();
        }

        // ------------------------------------------------------------------ selection / filtering

        private void SetTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TAB_COUNT - 1);
            _page = 0;
            ClearSelection();
            Refresh();
        }

        private void SelectCell(int cell)
        {
            int i = _page * CELLS_PER_PAGE + cell;
            if (i >= _visibleCount) return;
            _selSlot = -1;
            _selInv = _visible[i];
            SetStatus("");
            Refresh();
        }

        private void SelectSlot(int slot)
        {
            if (!HasCharacter()) return;
            _selInv = -1;
            _selSlot = playerState.current.equipped[slot] != 0 ? slot : -1;
            SetStatus(_selSlot < 0 ? "No " + itemDatabase.GetCategoryName(slot) + " equipped." : "");
            Refresh();
        }

        private void ClearSelection()
        {
            _selInv = -1;
            _selSlot = -1;
        }

        private void BuildVisibleList()
        {
            CharacterData c = playerState.current;
            int used = c.UsedInventorySlots();
            _visibleCount = 0;
            for (int i = 0; i < used; i++)
            {
                int id = c.inventoryItemIds[i];
                if (id == 0 || c.inventoryCounts[i] == 0) continue;
                if (_tab != TAB_ALL && itemDatabase.GetCategory(id) != _tab - 1) continue;
                _visible[_visibleCount++] = i;
            }
        }

        private int PageCount()
        {
            return Mathf.Max(1, (_visibleCount + CELLS_PER_PAGE - 1) / CELLS_PER_PAGE);
        }

        // ------------------------------------------------------------------ drawing

        public void Refresh()
        {
            if (!HasCharacter() || itemDatabase == null) return;
            CharacterData c = playerState.current;
            c.EnsureArrays();

            if (nameLabel != null) nameLabel.text = c.characterName;
            if (infoLabel != null) infoLabel.text = "Level " + c.level.ToString() + " " + itemDatabase.SchoolName((int)c.school) + " Wizard";
            if (goldLabel != null) goldLabel.text = "Gold: " + c.gold.ToString();

            for (int s = 0; s < CharacterData.EQUIP_SLOTS; s++)
            {
                int id = c.equipped[s];
                if (slotIcons != null && s < slotIcons.Length && slotIcons[s] != null)
                    slotIcons[s].color = id != 0 ? itemDatabase.GetIconColor(id) : _emptySlot;
                if (slotLabels != null && s < slotLabels.Length && slotLabels[s] != null)
                    slotLabels[s].text = itemDatabase.GetCategoryName(s) + "\n" + (id != 0 ? itemDatabase.GetName(id) : "(empty)");
            }

            if (statsLabel != null)
            {
                statsLabel.text =
                    "Health  " + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_HEALTH).ToString() +
                    "      Mana  " + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_MANA).ToString() + "\n" +
                    "Damage  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_DAMAGE).ToString() + "%" +
                    "    Resist  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_RESIST).ToString() + "%" +
                    "    Accuracy  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_ACCURACY).ToString() + "%";
            }

            if (tabImages != null)
                for (int t = 0; t < tabImages.Length; t++)
                    if (tabImages[t] != null) tabImages[t].color = t == _tab ? _tabOn : _tabOff;

            BuildVisibleList();
            _page = Mathf.Clamp(_page, 0, PageCount() - 1);
            for (int cell = 0; cell < CELLS_PER_PAGE; cell++)
            {
                int i = _page * CELLS_PER_PAGE + cell;
                bool has = i < _visibleCount;
                if (cellRoots != null && cell < cellRoots.Length && cellRoots[cell] != null) cellRoots[cell].SetActive(has);
                if (!has) continue;
                int inv = _visible[i];
                int id = c.inventoryItemIds[inv];
                int count = c.inventoryCounts[inv];
                if (cellIcons[cell] != null) cellIcons[cell].color = itemDatabase.GetIconColor(id);
                if (cellNames[cell] != null) cellNames[cell].text = itemDatabase.GetName(id);
                if (cellCounts[cell] != null) cellCounts[cell].text = count > 1 ? "x" + count.ToString() : "";
            }
            if (pageLabel != null)
                pageLabel.text = "Page " + (_page + 1).ToString() + " / " + PageCount().ToString() +
                                 "   (" + c.UsedInventorySlots().ToString() + "/" + CharacterData.INVENTORY_SLOTS.ToString() + ")";

            DrawDetails(c);
        }

        private void DrawDetails(CharacterData c)
        {
            int itemId = 0;
            bool worn = false;
            if (_selSlot >= 0) { itemId = c.equipped[_selSlot]; worn = true; }
            else if (_selInv >= 0 && _selInv < CharacterData.INVENTORY_SLOTS) itemId = c.inventoryItemIds[_selInv];

            bool show = itemId != 0;
            if (detailsPanel != null) detailsPanel.SetActive(show);
            if (!show) return;

            if (detailName != null)
                detailName.text = itemDatabase.GetName(itemId) + "  (" + itemDatabase.GetCategoryName(itemDatabase.GetCategory(itemId)) + ")";
            if (detailBody != null)
                detailBody.text = itemDatabase.GetDescription(itemId) + "\n" + itemDatabase.GetStatSummary(itemId);

            bool canAct = worn || itemDatabase.IsEquippable(itemId);
            if (actionButton != null) actionButton.SetActive(canAct);
            if (actionLabel != null) actionLabel.text = worn ? "Unequip" : "Equip";
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
