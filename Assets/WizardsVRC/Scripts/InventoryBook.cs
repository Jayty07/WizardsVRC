using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Persistence;
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
        public const int VIEW_PACK = 0;
        public const int VIEW_DECK = 1;
        public const int VIEW_QUESTS = 2;
        public const int VIEW_SETTINGS = 3;

        [Header("Systems")]
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public GameObject bookRoot;
        public float openDistance = 0.85f;
        public KeyCode toggleKey = KeyCode.B;

        [Header("Bookmark tabs")]
        public GameObject backpackView;
        public GameObject deckView;
        public DeckPages deckPages;
        public Image backpackTabImage;
        public Image deckTabImage;
        public GameObject questView;
        public QuestPages questPages;
        public Image questTabImage;
        public GameObject settingsView;
        public Image settingsTabImage;

        [Header("Settings page")]
        public PlayerHUD hud;
        [Tooltip("Interface sounds (book opening, potions).")]
        public UiAudio uiAudio;
        public QuestGuide questGuide;
        public CharacterSelectUI selectUI;
        [Tooltip("Teleports to the castle and hides every zone (used by Go Home and Change Character).")]
        public TeleportHelper homeTeleport;
        public Text settingsCharacterLabel;
        public Text hudSettingLabel;
        public Text guideSettingLabel;
        public Text warpSettingLabel;
        public Text musicSettingLabel;
        [Tooltip("Background music (the Music setting sets its volume).")]
        public MusicDirector music;
        public Text changeCharacterLabel;
        public Text settingsStatus;

        [Header("Avatar portrait")]
        [Tooltip("Renders the local avatar (MirrorReflection layer) into the left page's portrait. Only active while the book is open.")]
        public Camera portraitCamera;
        [Tooltip("Camera distance in front of the avatar, as a multiple of the avatar's eye height.")]
        public float portraitDistance = 1.8f;

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
        private int _view;

        private void Start()
        {
            if (bookRoot != null) bookRoot.SetActive(false);
            SetPortraitActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) ToggleBook();
            if (_open && (playerState == null || !playerState.HasCharacter())) CloseBook();
            if (_open && _view == VIEW_PACK) UpdatePortraitCamera();
        }

        // ------------------------------------------------------------------ bookmark tabs

        public void ShowBackpack()
        {
            _view = VIEW_PACK;
            ApplyView();
            Refresh();
        }

        public void ShowDeck()
        {
            _view = VIEW_DECK;
            ApplyView();
            if (deckPages != null) deckPages.Open();
        }

        public void ShowQuests()
        {
            _view = VIEW_QUESTS;
            ApplyView();
            if (questPages != null) questPages.Open();
        }

        public void ShowSettings()
        {
            _view = VIEW_SETTINGS;
            _confirmUntil = 0f;
            ApplyView();
            RefreshSettings("");
        }

        // ------------------------------------------------------------------ settings

        private float _confirmUntil;

        public void SettingToggleHud()
        {
            if (hud != null) hud.ToggleHud();
            RefreshSettings("");
        }

        public void SettingToggleGuide()
        {
            if (questGuide != null) questGuide.ToggleGuide();
            RefreshSettings("");
        }

        // PS1 wobble: the vertex snap and texture warp of every PSX shader, through a global shader value (local only).
        private const string WarpKey = "wz_psx_warp_off";
        private bool _warpOff;

        public void SettingToggleWarp()
        {
            _warpOff = !_warpOff;
            ApplyWarp();
            PlayerData.SetBool(WarpKey, _warpOff);
            RefreshSettings("");
        }

        private void ApplyWarp()
        {
            VRCShader.SetGlobalFloat(VRCShader.PropertyToID("_Udon_PsxWarpOff"), _warpOff ? 1f : 0f);
        }

        // Music volume: full, two thirds, one third, off (local only, remembered).
        private const string MusicKey = "wz_music_step";
        private int _musicStep;

        public void SettingCycleMusic()
        {
            _musicStep = (_musicStep + 1) % 4;
            ApplyMusic();
            PlayerData.SetInt(MusicKey, _musicStep);
            RefreshSettings("");
        }

        private void ApplyMusic()
        {
            if (music != null) music.SetMasterVolume(1f - _musicStep / 3f);
        }

        private string MusicText()
        {
            if (_musicStep == 0) return "Music: Full";
            if (_musicStep == 1) return "Music: Medium";
            if (_musicStep == 2) return "Music: Quiet";
            return "Music: Off";
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            bool off;
            if (PlayerData.TryGetBool(player, WarpKey, out off)) _warpOff = off;
            ApplyWarp();
            int step;
            if (PlayerData.TryGetInt(player, MusicKey, out step)) _musicStep = Mathf.Clamp(step, 0, 3);
            ApplyMusic();
            RefreshSettings("");
        }

        public void SettingSaveNow()
        {
            bool ok = playerState != null && playerState.SaveNow();
            RefreshSettings(ok ? "Saved." : "Couldn't save right now.");
        }

        /// <summary>Back to the castle courtyard from wherever you are (not during a duel).</summary>
        public void SettingGoHome()
        {
            if (playerState != null && playerState.inCombat) { RefreshSettings("You can't leave in the middle of a duel."); return; }
            if (homeTeleport != null) homeTeleport.TeleportToDefault();
            CloseBook();
        }

        /// <summary>Saves and goes back to the character select room (press twice to confirm).</summary>
        public void SettingChangeCharacter()
        {
            if (playerState != null && playerState.inCombat) { RefreshSettings("You can't change character in the middle of a duel."); return; }
            if (Time.time > _confirmUntil)
            {
                _confirmUntil = Time.time + 4f;
                RefreshSettings("Press again to save and change character.");
                return;
            }
            _confirmUntil = 0f;
            if (playerState != null) playerState.SaveNow();
            CloseBook();
            if (homeTeleport != null) homeTeleport.SwitchZones();
            if (selectUI != null) selectUI.ReturnToSelect();
        }

        private void RefreshSettings(string status)
        {
            if (settingsCharacterLabel != null && HasCharacter())
            {
                CharacterData c = playerState.current;
                settingsCharacterLabel.text = c.characterName + "\nLevel " + c.level.ToString() + " " + itemDatabase.SchoolName((int)c.school) + " Wizard";
            }
            if (hudSettingLabel != null) hudSettingLabel.text = "On-screen HUD: " + (hud != null && hud.hudEnabled ? "On" : "Off");
            if (guideSettingLabel != null) guideSettingLabel.text = "Quest arrow: " + (questGuide != null && questGuide.IsGuideEnabled() ? "On" : "Off");
            if (warpSettingLabel != null) warpSettingLabel.text = "PS1 wobble: " + (_warpOff ? "Off" : "On");
            if (musicSettingLabel != null) musicSettingLabel.text = MusicText();
            if (changeCharacterLabel != null) changeCharacterLabel.text = Time.time <= _confirmUntil ? "Press again to confirm" : "Change Character";
            if (settingsStatus != null) settingsStatus.text = status;
        }

        private void ApplyView()
        {
            bool pack = _view == VIEW_PACK;
            if (backpackView != null) backpackView.SetActive(pack);
            if (deckView != null) deckView.SetActive(_view == VIEW_DECK);
            if (questView != null) questView.SetActive(_view == VIEW_QUESTS);
            if (settingsView != null) settingsView.SetActive(_view == VIEW_SETTINGS);
            if (settingsTabImage != null) settingsTabImage.color = _view == VIEW_SETTINGS ? _tabOn : _tabOff;
            if (backpackTabImage != null) backpackTabImage.color = pack ? _tabOn : _tabOff;
            if (deckTabImage != null) deckTabImage.color = _view == VIEW_DECK ? _tabOn : _tabOff;
            if (questTabImage != null) questTabImage.color = _view == VIEW_QUESTS ? _tabOn : _tabOff;
            if (_open)
            {
                if (pack) UpdatePortraitCamera();
                SetPortraitActive(pack);
            }
        }

        // ------------------------------------------------------------------ avatar portrait

        private void SetPortraitActive(bool active)
        {
            if (portraitCamera != null) portraitCamera.gameObject.SetActive(active);
        }

        /// <summary>Keeps the portrait camera in front of the avatar, facing it, framed to the avatar's height.</summary>
        private void UpdatePortraitCamera()
        {
            if (portraitCamera == null) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            float eye = local.GetAvatarEyeHeightAsMeters();
            if (eye < 0.2f) eye = 1.6f;
            Vector3 feet = local.GetPosition();
            Vector3 forward = local.GetRotation() * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 target = feet + Vector3.up * (eye * 0.55f);
            Transform cam = portraitCamera.transform;
            cam.position = target + forward * (eye * portraitDistance);
            cam.rotation = Quaternion.LookRotation(-forward, Vector3.up);
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
            if (uiAudio != null) uiAudio.PlayBookOpen();
            ClearSelection();
            SetStatus("");
            ApplyView();
            if (_view == VIEW_DECK && deckPages != null) deckPages.Open();
            else if (_view == VIEW_QUESTS && questPages != null) questPages.Open();
            else if (_view == VIEW_SETTINGS) RefreshSettings("");
            else Refresh();
        }

        public void CloseBook()
        {
            _open = false;
            if (bookRoot != null) bookRoot.SetActive(false);
            SetPortraitActive(false);
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
        public void Tab9() { SetTab(9); }

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
        public void Slot7() { SelectSlot(7); }

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
            else if (_selInv >= 0 && itemDatabase.IsPotion(playerState.current.inventoryItemIds[_selInv])) Drink(_selInv);
            else if (_selInv >= 0) Equip(_selInv);
            Refresh();
        }

        // ------------------------------------------------------------------ potions

        private void Drink(int invIndex)
        {
            CharacterData c = playerState.current;
            int itemId = c.inventoryItemIds[invIndex];
            if (playerState.inCombat)
            {
                SetStatus("In battle, drink potions with the Potion button (it uses your turn).");
                return;
            }
            int heal = itemDatabase.GetPotionHealth(itemId);
            int mana = itemDatabase.GetPotionMana(itemId);
            bool helps = (heal > 0 && playerState.GetHealth() < playerState.MaxHealth()) || (mana > 0 && playerState.GetMana() < playerState.MaxMana());
            if (!helps)
            {
                SetStatus("You're already topped up.");
                return;
            }
            if (!c.RemoveItemAt(invIndex, 1)) return;
            if (uiAudio != null) uiAudio.PlayHeal();
            if (heal > 0) playerState.Heal(heal);
            if (mana > 0) playerState.RestoreMana(mana);
            playerState.MarkDirty();
            SetStatus("You drink the " + itemDatabase.GetName(itemId) + ".");
            if (c.FindItem(itemId) < 0) ClearSelection();
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
            bool deck = itemDatabase.IsDeck(itemId);
            if (deck && c.DeckListFor(itemId, false) < 0 && !c.CanClaimDeckList())
            {
                SetStatus("Your spellbook can keep " + CharacterData.DECK_COUNT.ToString() + " decks. Sell or drop one first.");
                return;
            }
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
            if (deck) c.DeckListFor(itemId, true);
            playerState.MarkDirty();
            SetStatus("Equipped " + itemDatabase.GetName(itemId) + (deck ? ". Fill it with spells in the Deck tab." : "."));
            ClearSelection();
            _selSlot = slot;
        }

        private void Unequip(int slot)
        {
            CharacterData c = playerState.current;
            int itemId = c.equipped[slot];
            if (itemId == 0) return;
            if (slot == CharacterData.EQUIP_DECK)
            {
                SetStatus("You always need a deck. Equip another deck to swap.");
                return;
            }
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
                    "Health  " + playerState.GetHealth().ToString() + " / " + playerState.MaxHealth().ToString() +
                    "      Mana  " + playerState.GetMana().ToString() + " / " + playerState.MaxMana().ToString() + "\n" +
                    "Damage  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_DAMAGE).ToString() + "%" +
                    "    Resist  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_RESIST).ToString() + "%" +
                    "    Accuracy  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_ACCURACY).ToString() + "%\n" +
                    "Critical  " + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_CRITICAL).ToString() + "%" +
                    "    Block  " + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_BLOCK).ToString() + "%" +
                    "    Pierce  " + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_PIERCE).ToString() + "%" +
                    "    Power Pip  +" + itemDatabase.GetTotalStat(c, ItemDatabase.STAT_POWERPIP).ToString() + "%";
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
            {
                string spells = itemDatabase.ItemCardText(itemId);
                detailBody.text = itemDatabase.GetDescription(itemId) + "\n" + itemDatabase.GetStatSummary(itemId) + (spells.Length > 0 ? "\n" + spells : "");
            }

            bool potion = !worn && itemDatabase.IsPotion(itemId);
            bool canAct = worn || potion || itemDatabase.IsEquippable(itemId);
            if (actionButton != null) actionButton.SetActive(canAct);
            if (actionLabel != null) actionLabel.text = worn ? "Unequip" : (potion ? "Drink" : "Equip");
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
