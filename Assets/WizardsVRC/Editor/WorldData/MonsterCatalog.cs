using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// Every monster in the world: stats, school, spell list and loot. Saved as a text asset in
    /// Assets/WizardsVRC/WorldData so it is versioned with the repo; the scene builder copies it into CardDatabase's
    /// monster tables on every rebuild. Zones refer to monsters by id.
    /// </summary>
    [CreateAssetMenu(fileName = "Monsters", menuName = "WizardsVRC/Monster Catalog")]
    public class MonsterCatalog : ScriptableObject
    {
        public List<MonsterEntry> monsters = new List<MonsterEntry>();

        public MonsterEntry Find(int id)
        {
            foreach (var m in monsters) if (m != null && m.id == id) return m;
            return null;
        }
    }

    [Serializable]
    public class MonsterEntry
    {
        [Tooltip("Unique id (1+). Zones, rosters and quests refer to the monster by this. Don't change it once quests use it.")]
        public int id = 1;
        public string name = "New Monster";
        public School school = School.Death;
        [Range(1, 100)] public int level = 1;
        [Tooltip("Power tier shown in the duel as \"Rank 2 Life Plant\" (Wizard101-style: roughly +1 per area; bosses a little higher).")]
        [Range(1, 20)] public int rank = 1;
        [Tooltip("Creature type shown after the school in the duel, e.g. Beast, Plant, Insect, Spirit, Undead, Goblin, Construct.")]
        public string creatureType = "";
        [Tooltip("Health in battle.")]
        public int health = 200;
        [Tooltip("Bosses: shown with a bigger body in the boss seat and get extra health per extra wizard.")]
        public bool isBoss;
        [Tooltip("Bosses only: extra health for each wizard beyond the first.")]
        public int healthPerExtraWizard;

        [Tooltip("Spell card ids this monster can cast (up to 6). Use the spell picker in the inspector. " +
                 "The AI heals hurt allies, sometimes blades/shields itself, otherwise casts its biggest affordable attack.")]
        public List<int> spells = new List<int>();

        [Header("Rewards")]
        public int xp = 30;
        public int goldMin = 5;
        public int goldMax = 15;
        [Tooltip("Item id dropped on a win (0 = none). See ItemDatabase.")]
        public int dropItem;
        [Range(0, 100)] public int dropChance;

        [Header("Look")]
        [Tooltip("Optional model (a prefab) used for this monster on patrol and in the duel ring. Its feet should be at " +
                 "its pivot and it should face +Z. Empty = a coloured placeholder body. Avoid non-Udon scripts on it.")]
        public GameObject modelPrefab;
        [Tooltip("Body size (1 = wizard sized). Also scales the model.")]
        public float bodyScale = 1f;
        [Tooltip("Placeholder body colour (used when there is no model).")]
        public Color bodyColor = new Color(0.5f, 0.5f, 0.55f);

        [TextArea(1, 4)] public string notes = "";
    }
}
