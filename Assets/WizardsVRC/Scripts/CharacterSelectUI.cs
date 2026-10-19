using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Drives the select-room canvas: 4 slot panels, school picker and delete confirmation.
    /// uGUI buttons call the public no-argument events below via UdonBehaviour.SendCustomEvent.
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
            if (playerState != null && playerState.HasCharacter() && teleporter != null)
                teleporter.TeleportTo(hubSpawn);
        }

        // ------------------------------------------------------------------ select

        public void SelectSlot0() { Select(0); }
        public void SelectSlot1() { Select(1); }
        public void SelectSlot2() { Select(2); }
        public void SelectSlot3() { Select(3); }

        private void Select(int slot)
        {
            if (!CanAct() || !saveManager.HasSlot(slot)) return;
            playerState.Deactivate();
            if (!saveManager.LoadSlot(slot, playerState.current))
            {
                SetStatus("That save could not be read.");
                return;
            }
            playerState.Activate(slot);
            EnterHub();
        }

        // ------------------------------------------------------------------ new character

        public void NewSlot0() { OpenPicker(0); }
        public void NewSlot1() { OpenPicker(1); }
        public void NewSlot2() { OpenPicker(2); }
        public void NewSlot3() { OpenPicker(3); }

        private void OpenPicker(int slot)
        {
            if (!CanAct() || saveManager.HasSlot(slot)) return;
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
            playerState.Activate(slot);
            RefreshSlots();
            EnterHub();
        }

        // ------------------------------------------------------------------ delete

        public void DeleteSlot0() { AskDelete(0); }
        public void DeleteSlot1() { AskDelete(1); }
        public void DeleteSlot2() { AskDelete(2); }
        public void DeleteSlot3() { AskDelete(3); }

        private void AskDelete(int slot)
        {
            if (!CanAct() || !saveManager.HasSlot(slot)) return;
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

        /// <summary>Hook for a hub-side "Change Character" button: saves, detaches and returns to the select room.</summary>
        public void ReturnToSelect()
        {
            if (playerState != null) playerState.Deactivate();
            RefreshSlots();
            SetStatus("Choose a character");
            if (teleporter != null) teleporter.TeleportTo(selectSpawn);
        }

        // ------------------------------------------------------------------ helpers

        private void EnterHub()
        {
            SetStatus("Welcome, " + playerState.current.characterName);
            if (teleporter != null) teleporter.TeleportTo(hubSpawn);
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
