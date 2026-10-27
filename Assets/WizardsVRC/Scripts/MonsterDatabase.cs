using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Static monster definitions (original placeholder monsters), parallel arrays indexed by definition index like
    /// <see cref="CardDatabase"/>. Each monster has up to <see cref="SPELLS_PER_MONSTER"/> spells from the spell trees,
    /// XP and gold rewards, and one drop (item id + chance %) rolled per defeated monster.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MonsterDatabase : UdonSharpBehaviour
    {
        public const int SPELLS_PER_MONSTER = 4;

        // Training Yard: 1 Gloomfang the Warden (boss), 2 Bone Thrall, 3 Marsh Sprite, 4 Courtyard Rat
        [HideInInspector] public int[] ids = { 1, 2, 3, 4 };
        [HideInInspector] public string[] names = { "Gloomfang the Warden", "Bone Thrall", "Marsh Sprite", "Courtyard Rat" };
        [HideInInspector] public int[] schools = { 4, 5, 3, 2 };
        [HideInInspector] public int[] levels = { 5, 3, 2, 1 };
        [HideInInspector] public int[] hps = { 900, 320, 240, 180 };
        /// <summary>SPELLS_PER_MONSTER entries per monster, 0 = none.</summary>
        [HideInInspector] public int[] spells =
        {
            148, 149, 152, 154,
            160, 161, 0, 0,
            136, 137, 0, 0,
            124, 125, 0, 0,
        };
        [HideInInspector] public int[] xps = { 120, 45, 35, 25 };
        [HideInInspector] public int[] goldMin = { 40, 10, 8, 5 };
        [HideInInspector] public int[] goldMax = { 80, 25, 18, 12 };
        /// <summary>Item id dropped (0 = none) and the chance in percent.</summary>
        [HideInInspector] public int[] dropItems = { 25, 10, 11, 10 };
        [HideInInspector] public int[] dropChances = { 60, 25, 25, 20 };
        /// <summary>Body size multiplier (1 = wizard sized).</summary>
        [HideInInspector] public float[] bodyScales = { 1.7f, 1f, 0.8f, 0.7f };
        [HideInInspector] public int[] isBoss = { 1, 0, 0, 0 };
        /// <summary>Extra health per extra wizard in the fight (bosses only).</summary>
        [HideInInspector] public int[] hpPerExtraPlayer = { 450, 0, 0, 0 };

        public int Count() { return ids.Length; }

        public int IndexOf(int monsterId)
        {
            if (monsterId <= 0) return -1;
            for (int i = 0; i < ids.Length; i++) if (ids[i] == monsterId) return i;
            return -1;
        }

        public string GetName(int id) { int i = IndexOf(id); return i < 0 ? "Monster" : names[i]; }
        public int GetSchool(int id) { int i = IndexOf(id); return i < 0 ? 0 : schools[i]; }
        public int GetLevel(int id) { int i = IndexOf(id); return i < 0 ? 1 : levels[i]; }
        public int GetHp(int id) { int i = IndexOf(id); return i < 0 ? 100 : hps[i]; }
        public int GetXp(int id) { int i = IndexOf(id); return i < 0 ? 0 : xps[i]; }
        public int RollGold(int id) { int i = IndexOf(id); return i < 0 ? 0 : Random.Range(goldMin[i], goldMax[i] + 1); }
        public int GetDropItem(int id) { int i = IndexOf(id); return i < 0 ? 0 : dropItems[i]; }
        public int GetDropChance(int id) { int i = IndexOf(id); return i < 0 ? 0 : dropChances[i]; }
        public float GetBodyScale(int id) { int i = IndexOf(id); return i < 0 ? 1f : bodyScales[i]; }
        public bool IsBoss(int id) { int i = IndexOf(id); return i >= 0 && isBoss[i] != 0; }
        public int GetHpPerExtraPlayer(int id) { int i = IndexOf(id); return i < 0 ? 0 : hpPerExtraPlayer[i]; }

        /// <summary>Spell k (0..SPELLS_PER_MONSTER-1) of the monster, 0 = none.</summary>
        public int GetSpell(int id, int k)
        {
            int i = IndexOf(id);
            if (i < 0 || k < 0 || k >= SPELLS_PER_MONSTER) return 0;
            int idx = i * SPELLS_PER_MONSTER + k;
            return idx < spells.Length ? spells[idx] : 0;
        }
    }
}
