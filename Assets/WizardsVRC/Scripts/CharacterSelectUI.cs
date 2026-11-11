using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Drives the character select panel (on the title stage): 4 slot panels, school picker and delete confirmation.
    /// uGUI buttons call the public no-argument events below via UdonBehaviour.SendCustomEvent. With a title screen,
    /// Select hands over to it (the scene fades out and back in where the character last logged off) and New Character
    /// leaves through it to the opening; Change Character comes back to it. This behaviour lives on an always-active
    /// object (the panel itself is switched off with the stage).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CharacterSelectUI : UdonSharpBehaviour
    {
        [Header("Systems")]
        public SaveManager saveManager;
        public PlayerState playerState;
        public TeleportHelper teleporter;
        public Transform hubSpawn;
        public Transform selectSpawn;
        [Tooltip("Optional: new characters are sent here instead of the hub (e.g. the Headmaster's office).")]
        public Transform newCharacterSpawn;
        [Tooltip("Receives OnCharacterActivated after a character is created or loaded.")]
        public UdonSharpBehaviour questManager;
        [Tooltip("The opening for new characters (awakening, the Headmaster, the creator). Without it New Character opens the old school picker.")]
        public AwakeningSequence awakening;
        [Tooltip("The title stage, where the select panel stands: leaving it and coming back to it go through this.")]
        public TitleScreen titleScreen;
        [Tooltip("Remembers where the character stands when you change character.")]
        public LocationKeeper locations;

        [Header("Slot panels (index = slot)")]
        public Text[] slotNameLabels;
        public Text[] slotSchoolLabels;
        public Text[] slotLevelLabels;
        public GameObject[] selectButtons;
        public GameObject[] newButtons;
        public GameObject[] deleteButtons;

        [Header("Popups")]
        public GameObject schoolPickerPanel;
        public GameObject confirmPanel;
        public Text confirmLabel;
        public Text statusLabel;

        private string[] _schoolNames = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
        private string[] _firstNames = { "Aldric", "Brynn", "Corwin", "Dara", "Elowen", "Fenn", "Garrick", "Isolde", "Joren", "Kestrel", "Lyra", "Merrow", "Nyx", "Orrin", "Perrin", "Rowan", "Sable", "Tamsin", "Vale", "Wren" };
        private string[] _lastNames = { "Ashgrove", "Brightwater", "Cinderfall", "Duskmantle", "Emberly", "Frostvale", "Gloomwick", "Hollowmere", "Ironbloom", "Moonbrook", "Ravenshade", "Starfield", "Stormcaller", "Thornwood", "Willowind" };

        private int _pendingSlot = -1;
        private bool _promptWhenReady;

        private void Start()
        {
            SetPopup(schoolPickerPanel, false);
            SetPopup(confirmPanel, false);
            SetStatus("Loading save data...");
            SetAllButtons(false);
            if (saveManager != null && saveManager.isReady) OnSaveDataReady();
        }

        // ------------------------------------------------------------------ SaveManager listener

        public void OnSaveDataReady()
        {
            SetStatus("Choose a character");
            RefreshSlots();
            if (_promptWhenReady) PromptSchool();
        }

        /// <summary>
        /// Called after the Headmaster's greeting. New players (no saved characters) go straight to the school picker;
        /// returning players are asked to choose a character.
        /// </summary>
        public void PromptSchool()
        {
            if (!CanAct())
            {
                _promptWhenReady = true;
                return;
            }
            _promptWhenReady = false;
            if (playerState.HasCharacter()) return;
            int empty = -1;
            int used = 0;
            for (int i = 0; i < SaveManager.SLOT_COUNT; i++)
            {
                if (saveManager.HasSlot(i)) used++;
                else if (empty < 0) empty = i;
            }
            if (used == 0 && empty >= 0)
            {
                OpenPicker(empty);
                SetStatus("The Headmaster asks: which school calls to you?");
            }
            else SetStatus("Welcome back! Choose a character");
        }

        public void OnSaveMetaChanged()
        {
            RefreshSlots();
        }

        public void RefreshSlots()
        {
            if (saveManager == null || !saveManager.isReady) return;
            for (int i = 0; i < SaveManager.SLOT_COUNT; i++)
            {
                bool used = saveManager.HasSlot(i);
                SetLabel(slotNameLabels, i, used ? saveManager.GetSlotName(i) : "Empty Slot");
                SetLabel(slotSchoolLabels, i, used ? SchoolName(saveManager.GetSlotSchool(i)) : "");
                SetLabel(slotLevelLabels, i, used ? "Level " + saveManager.GetSlotLevel(i).ToString() : "");
                SetActive(selectButtons, i, used);
                SetActive(deleteButtons, i, used);
                SetActive(newButtons, i, !used);
            }
        }

        // ------------------------------------------------------------------ player events

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            if (playerState != null && playerState.HasCharacter())
            {
                if (teleporter != null) teleporter.TeleportTo(hubSpawn);
                return;
            }
            // No character (and not in the opening): back to the title stage's character select.
            if (titleScreen != null && !titleScreen.IsShowing() && (awakening == null || !awakening.IsRunning()) && saveManager != null && saveManager.isReady)
                titleScreen.ShowSelect();
        }

        /// <summary>On the title stage and already on the way out: the buttons do nothing.</summary>
        private bool Leaving()
        {
            return titleScreen != null && titleScreen.IsShowing() && titleScreen.IsLeaving();
        }

        private bool OnTitleStage()
        {
            return titleScreen != null && titleScreen.IsShowing();
        }

        // ------------------------------------------------------------------ select

        public void SelectSlot0() { Select(0); }
        public void SelectSlot1() { Select(1); }
        public void SelectSlot2() { Select(2); }
        public void SelectSlot3() { Select(3); }

        private void Select(int slot)
        {
            if (!CanAct() || Leaving() || !saveManager.HasSlot(slot)) return;
            playerState.Deactivate();
            if (!saveManager.LoadSlot(slot, playerState.current))
            {
                SetStatus("That save could not be read.");
                return;
            }
            playerState.Activate(slot);
            NotifyActivated();
            if (OnTitleStage())
            {
                // The scene fades out and back in where this character last logged off.
                SetStatus("Welcome back, " + playerState.current.characterName);
                titleScreen.LeaveWithCharacter(hubSpawn);
                return;
            }
            EnterHub();
        }

        // ------------------------------------------------------------------ new character

        public void NewSlot0() { OpenPicker(0); }
        public void NewSlot1() { OpenPicker(1); }
        public void NewSlot2() { OpenPicker(2); }
        public void NewSlot3() { OpenPicker(3); }

        private void OpenPicker(int slot)
        {
            if (!CanAct() || Leaving() || saveManager.HasSlot(slot)) return;
            if (awakening != null)
            {
                if (awakening.IsRunning()) return;
                SetPopup(confirmPanel, false);
                SetStatus("");
                if (OnTitleStage()) titleScreen.LeaveForOpening(slot);
                else awakening.Begin(slot);
                return;
            }
            _pendingSlot = slot;
            SetPopup(confirmPanel, false);
            SetPopup(schoolPickerPanel, true);
            SetStatus("Choose a school");
        }

        public void PickFire() { CreateCharacter(School.Fire); }
        public void PickIce() { CreateCharacter(School.Ice); }
        public void PickStorm() { CreateCharacter(School.Storm); }
        public void PickLife() { CreateCharacter(School.Life); }
        public void PickDeath() { CreateCharacter(School.Death); }
        public void PickMyth() { CreateCharacter(School.Myth); }
        public void PickBalance() { CreateCharacter(School.Balance); }

        public void CancelPicker()
        {
            _pendingSlot = -1;
            SetPopup(schoolPickerPanel, false);
            SetStatus("Choose a character");
        }

        private void CreateCharacter(School school)
        {
            int slot = _pendingSlot;
            SetPopup(schoolPickerPanel, false);
            _pendingSlot = -1;
            if (!CanAct() || slot < 0 || saveManager.HasSlot(slot)) return;

            playerState.Deactivate();
            playerState.current.ResetToDefaults(school, RandomName());
            if (!saveManager.SaveSlot(slot, playerState.current))
            {
                SetStatus("Could not save the new character.");
                return;
            }
            saveManager.ClearPlace(slot, SaveManager.PLACE_LAST);
            saveManager.ClearPlace(slot, SaveManager.PLACE_MARK);
            playerState.Activate(slot);
            RefreshSlots();
            NotifyActivated();
            if (OnTitleStage())
            {
                SetStatus("Welcome, " + playerState.current.characterName);
                titleScreen.LeaveWithCharacter(newCharacterSpawn != null ? newCharacterSpawn : hubSpawn);
            }
            else if (newCharacterSpawn != null)
            {
                SetStatus("Welcome, " + playerState.current.characterName);
                if (teleporter != null) teleporter.TeleportTo(newCharacterSpawn);
            }
            else EnterHub();
        }

        /// <summary>
        /// The character creator's Begin: a new character with the chosen name, school and colours in this slot. The
        /// player is already in the Headmaster's study, so nobody is moved. False if it couldn't be made or saved.
        /// </summary>
        public bool CreateFromCreator(int slot, int school, string characterName, int signatureColor, int auraColor)
        {
            if (!CanAct() || slot < 0 || slot >= SaveManager.SLOT_COUNT || saveManager.HasSlot(slot)) return false;
            if (school < 0 || school > (int)School.Balance) return false;
            playerState.Deactivate();
            playerState.current.ResetToDefaults((School)school, characterName);
            playerState.current.signatureColor = Mathf.Clamp(signatureColor, 0, CharacterData.COLOR_CHOICES - 1);
            playerState.current.auraColor = Mathf.Clamp(auraColor, 0, CharacterData.COLOR_CHOICES - 1);
            if (!saveManager.SaveSlot(slot, playerState.current))
            {
                SetStatus("Could not save the new character.");
                return false;
            }
            saveManager.ClearPlace(slot, SaveManager.PLACE_LAST);
            saveManager.ClearPlace(slot, SaveManager.PLACE_MARK);
            playerState.Activate(slot);
            RefreshSlots();
            NotifyActivated();
            SetStatus("Welcome, " + playerState.current.characterName);
            return true;
        }

        /// <summary>First empty save slot, or -1 when all are used.</summary>
        public int FirstEmptySlot()
        {
            if (saveManager == null || !saveManager.isReady) return -1;
            for (int i = 0; i < SaveManager.SLOT_COUNT; i++)
                if (!saveManager.HasSlot(i)) return i;
            return -1;
        }

        /// <summary>True when this player has no saved characters at all (a first visit).</summary>
        public bool IsNewPlayer()
        {
            if (saveManager == null || !saveManager.isReady) return false;
            for (int i = 0; i < SaveManager.SLOT_COUNT; i++)
                if (saveManager.HasSlot(i)) return false;
            return true;
        }

        // ------------------------------------------------------------------ delete

        public void DeleteSlot0() { AskDelete(0); }
        public void DeleteSlot1() { AskDelete(1); }
        public void DeleteSlot2() { AskDelete(2); }
        public void DeleteSlot3() { AskDelete(3); }

        private void AskDelete(int slot)
        {
            if (!CanAct() || Leaving() || !saveManager.HasSlot(slot)) return;
            _pendingSlot = slot;
            SetPopup(schoolPickerPanel, false);
            if (confirmLabel != null)
                confirmLabel.text = "Delete " + saveManager.GetSlotName(slot) + "?\nThis cannot be undone.";
            SetPopup(confirmPanel, true);
        }

        public void ConfirmYes()
        {
            int slot = _pendingSlot;
            _pendingSlot = -1;
            SetPopup(confirmPanel, false);
            if (!CanAct() || slot < 0) return;
            if (playerState.activeSlot == slot) playerState.activeSlot = -1;
            saveManager.DeleteSlot(slot);
            RefreshSlots();
            SetStatus("Character deleted");
        }

        public void ConfirmNo()
        {
            _pendingSlot = -1;
            SetPopup(confirmPanel, false);
        }

        // ------------------------------------------------------------------ return to select room

        /// <summary>
        /// Change Character (the book's settings): remembers where the character stands, saves and detaches it, and goes
        /// back to the character select on the title stage (or, without one, to the select room).
        /// </summary>
        public void ReturnToSelect()
        {
            if (locations != null) locations.Remember();
            if (playerState != null) playerState.Deactivate();
            RefreshSlots();
            SetStatus("Choose a character");
            SetPopup(confirmPanel, false);
            SetPopup(schoolPickerPanel, false);
            if (titleScreen != null) titleScreen.ShowSelect();
            else if (teleporter != null) teleporter.TeleportTo(selectSpawn);
        }

        // ------------------------------------------------------------------ helpers

        private void EnterHub()
        {
            SetStatus("Welcome, " + playerState.current.characterName);
            if (teleporter != null) teleporter.TeleportTo(hubSpawn);
        }

        private void NotifyActivated()
        {
            if (questManager != null) questManager.SendCustomEvent("OnCharacterActivated");
        }

        private bool CanAct()
        {
            return saveManager != null && saveManager.isReady && playerState != null && playerState.current != null;
        }

        private string RandomName()
        {
            return _firstNames[Random.Range(0, _firstNames.Length)] + " " + _lastNames[Random.Range(0, _lastNames.Length)];
        }

        public string SchoolName(int school)
        {
            return school >= 0 && school < _schoolNames.Length ? _schoolNames[school] : "?";
        }

        private void SetAllButtons(bool on)
        {
            for (int i = 0; i < SaveManager.SLOT_COUNT; i++)
            {
                SetActive(selectButtons, i, on);
                SetActive(newButtons, i, on);
                SetActive(deleteButtons, i, on);
            }
        }

        private void SetLabel(Text[] labels, int i, string value)
        {
            if (labels != null && i < labels.Length && labels[i] != null) labels[i].text = value;
        }

        private void SetActive(GameObject[] objects, int i, bool on)
        {
            if (objects != null && i < objects.Length && objects[i] != null) objects[i].SetActive(on);
        }

        private void SetPopup(GameObject panel, bool on)
        {
            if (panel != null) panel.SetActive(on);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }
    }
}
