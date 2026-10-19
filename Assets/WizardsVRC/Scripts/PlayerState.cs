using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Source of truth for the local player's live character. Combat, quests, inventory etc.
    /// should read/write <see cref="current"/> and call <see cref="MarkDirty"/> after changes.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerState : UdonSharpBehaviour
    {
        public CharacterData current;
        public SaveManager saveManager;

        [Tooltip("Seconds between autosaves while a character is active.")]
        public float autosaveInterval = 120f;

        [HideInInspector] public int activeSlot = -1;

        private bool _dirty;
        private bool _autosaveScheduled;

        public bool HasCharacter()
        {
            return activeSlot >= 0;
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>Called after <see cref="current"/> has been filled from a slot (loaded or newly created).</summary>
        public void Activate(int slot)
        {
            activeSlot = slot;
            _dirty = false;
            if (!_autosaveScheduled)
            {
                _autosaveScheduled = true;
                SendCustomEventDelayedSeconds(nameof(_AutosaveTick), autosaveInterval);
            }
        }

        /// <summary>Saves (if needed) and detaches the active character, e.g. before swapping.</summary>
        public void Deactivate()
        {
            if (HasCharacter()) SaveNow();
            activeSlot = -1;
        }

        public bool SaveNow()
        {
            if (!HasCharacter() || saveManager == null) return false;
            bool ok = saveManager.SaveSlot(activeSlot, current);
            if (ok) _dirty = false;
            return ok;
        }

        public void _AutosaveTick()
        {
            if (!HasCharacter())
            {
                _autosaveScheduled = false;
                return;
            }
            // Always save on the tick: level/xp may change through fields written directly by other systems.
            SaveNow();
            SendCustomEventDelayedSeconds(nameof(_AutosaveTick), autosaveInterval);
        }

        public bool IsDirty()
        {
            return _dirty;
        }
    }
}
