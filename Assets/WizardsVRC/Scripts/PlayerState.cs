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
        [Tooltip("Used for gear-adjusted max health / mana.")]
        public ItemDatabase itemDatabase;

        [Header("Out-of-combat recovery (Wizard101 regenerates while you walk around)")]
        public float regenInterval = 2f;
        [Tooltip("% of max health restored per regen tick (at least 1).")]
        public int healthRegenPct = 1;
        [Tooltip("% of max mana restored per regen tick (at least 1).")]
        public int manaRegenPct = 1;

        /// <summary>Set by BattleClient while the local player is in a fight (no regeneration).</summary>
        [HideInInspector] public bool inCombat;
        private float _nextRegen;

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

        // ------------------------------------------------------------------ health / mana

        public int MaxHealth()
        {
            if (!HasCharacter()) return 0;
            return itemDatabase != null ? itemDatabase.GetTotalStat(current, ItemDatabase.STAT_HEALTH) : current.maxHealth;
        }

        public int MaxMana()
        {
            if (!HasCharacter()) return 0;
            return itemDatabase != null ? itemDatabase.GetTotalStat(current, ItemDatabase.STAT_MANA) : current.maxMana;
        }

        public int GetHealth() { return HasCharacter() ? Mathf.Clamp(current.currentHealth, 0, MaxHealth()) : 0; }
        public int GetMana() { return HasCharacter() ? Mathf.Clamp(current.currentMana, 0, MaxMana()) : 0; }

        /// <summary>Stores FULL when topped up, so equipping more health/mana gear keeps you full.</summary>
        public void SetHealth(int value)
        {
            if (!HasCharacter()) return;
            int max = MaxHealth();
            current.currentHealth = value >= max ? CharacterData.FULL : Mathf.Max(0, value);
            MarkDirty();
        }

        public void SetMana(int value)
        {
            if (!HasCharacter()) return;
            int max = MaxMana();
            current.currentMana = value >= max ? CharacterData.FULL : Mathf.Max(0, value);
            MarkDirty();
        }

        public void Heal(int amount) { SetHealth(GetHealth() + amount); }
        public void RestoreMana(int amount) { SetMana(GetMana() + amount); }
        public void SpendMana(int amount) { SetMana(GetMana() - amount); }

        private void Update()
        {
            if (!HasCharacter() || inCombat || Time.time < _nextRegen) return;
            _nextRegen = Time.time + Mathf.Max(0.25f, regenInterval);
            int hp = GetHealth(), maxHp = MaxHealth();
            if (hp < maxHp) SetHealth(hp + Mathf.Max(1, maxHp * healthRegenPct / 100));
            int mp = GetMana(), maxMp = MaxMana();
            if (mp < maxMp) SetMana(mp + Mathf.Max(1, maxMp * manaRegenPct / 100));
        }
    }
}
