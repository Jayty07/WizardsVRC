using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Static item definitions (placeholder fantasy gear). Parallel arrays indexed by definition index;
    /// look up by item id with <see cref="IndexOf"/>. Item id 0 means "no item".
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ItemDatabase : UdonSharpBehaviour
    {
        /// <summary>Decks: equipped in CharacterData.EQUIP_DECK; each holds its own card list (see deckCapacity).</summary>
        public const int CATEGORY_DECK = 7;
        /// <summary>Category for non-equippable stackables. Gear categories reuse CharacterData.EQUIP_* (0..7).</summary>
        public const int CATEGORY_POTION = 8;
        public const int CATEGORY_COUNT = 9;

        public const int STAT_HEALTH = 0;
        public const int STAT_MANA = 1;
        public const int STAT_DAMAGE = 2;
        public const int STAT_RESIST = 3;
        public const int STAT_ACCURACY = 4;
        /// <summary>% chance for a hit (or heal) to be critical: double damage unless the target blocks it.</summary>
        public const int STAT_CRITICAL = 5;
        /// <summary>% chance to block an enemy's critical hit.</summary>
        public const int STAT_BLOCK = 6;
        /// <summary>% of the target's shields and resist that your attacks ignore.</summary>
        public const int STAT_PIERCE = 7;
        /// <summary>% added to the chance that a round's pip is a power pip.</summary>
        public const int STAT_POWERPIP = 8;
        public const int STAT_COUNT = 9;

        public const int ANY_SCHOOL = -1;

        [HideInInspector] public string[] categoryNames = { "Hat", "Robe", "Boots", "Wand", "Athame", "Amulet", "Ring", "Deck", "Potion" };
        [HideInInspector] public string[] statNames = { "Health", "Mana", "Damage", "Resist", "Accuracy", "Critical", "Block", "Pierce", "Power Pip" };

        // id, category, level req, school req (-1 any), health, mana, damage %, resist %, accuracy %
        [HideInInspector] public int[] ids =         { 1, 2, 3, 4, 10, 11, 20, 21, 22, 23, 24, 25, 26, 27, 28, 30, 31, 40, 41, 50, 51, 60, 61, 62, 63 };
        [HideInInspector] public int[] categories =  { 3, 1, 0, 2, 8, 8, 3, 3, 3, 0, 2, 1, 3, 1, 0, 4, 4, 5, 5, 6, 6, 7, 7, 7, 7 };
        [HideInInspector] public int[] levelReqs =   { 1, 1, 1, 1, 1,  1,  3,  2,  2,  2,  2,  3,  4,  5,  5,  1,  3,  2,  4,  1,  3, 1, 3, 6, 10 };
        [HideInInspector] public int[] schoolReqs =  { -1,-1,-1,-1,-1, -1, -1, 0,  1,  -1, -1, -1, 2,  3,  4,  -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        [HideInInspector] public int[] healthBonus = { 0, 20, 10, 10, 0,  0,  0,  0,  15, 25, 20, 40, 0,  60, 30, 0,  10, 15, 0,  0,  10, 0, 10, 20, 35 };
        [HideInInspector] public int[] manaBonus =   { 0, 5,  0,  0,  0,  0,  2,  0,  0,  3,  0,  10, 4,  8,  5,  0,  0,  5,  2,  0,  0, 0, 2, 4, 6 };
        [HideInInspector] public int[] damageBonus = { 2, 0,  0,  0,  0,  0,  8,  10, 6,  0,  0,  3,  12, 0,  4,  3,  0,  0,  3,  0,  0, 0, 0, 0, 2 };
        [HideInInspector] public int[] resistBonus = { 0, 1,  1,  1,  0,  0,  0,  0,  2,  3,  2,  2,  0,  4,  3,  0,  4,  0,  0,  0,  3, 0, 0, 1, 2 };
        [HideInInspector] public int[] accuracyBonus={ 0, 0,  0,  0,  0,  0,  2,  0,  0,  0,  0,  0,  3,  2,  1,  1,  0,  0,  0,  2,  0, 0, 1, 1, 2 };
        [HideInInspector] public int[] critBonus =   { 0, 0,  0,  0,  0,  0,  0,  2,  0,  0,  0,  0,  4,  0,  0,  3,  0,  0,  2,  0,  0, 0, 0, 0, 1 };
        [HideInInspector] public int[] blockBonus =  { 0, 0,  0,  0,  0,  0,  0,  0,  2,  0,  0,  0,  0,  0,  2,  0,  3,  0,  0,  0,  2, 0, 0, 1, 1 };
        [HideInInspector] public int[] pierceBonus = { 0, 0,  0,  0,  0,  0,  1,  0,  0,  0,  0,  0,  2,  0,  0,  2,  0,  0,  0,  1,  0, 0, 0, 0, 0 };
        [HideInInspector] public int[] powerPipBonus={ 0, 0,  0,  0,  0,  0,  0,  0,  0,  2,  0,  3,  0,  3,  0,  0,  0,  5,  0,  0,  0, 0, 0, 2, 4 };
        /// <summary>Decks only: how many spells the deck holds, and how many copies of one spell.</summary>
        [HideInInspector] public int[] deckCapacity =  { 0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 20, 25, 30, 40 };
        [HideInInspector] public int[] deckMaxCopies = { 0, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 4, 4, 5, 6 };
        /// <summary>Potions only: health / mana restored when drunk (outside battle from the backpack, or in battle as your turn).</summary>
        [HideInInspector] public int[] potionHealth ={ 0, 0,  0,  0,  150,0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0 };
        [HideInInspector] public int[] potionMana =  { 0, 0,  0,  0,  0,  12, 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 0, 0, 0, 0 };

        [HideInInspector] public string[] names =
        {
            "Training Staff", "Apprentice Robe", "Apprentice Hat", "Apprentice Boots",
            "Minor Healing Potion", "Mana Draught",
            "Oakwood Wand", "Ember Wand", "Frost-Etched Wand", "Scholar's Hat", "Traveler's Boots", "Silkweave Robe",
            "Thunderhorn Wand", "Verdant Vestments", "Gravewarden Cowl",
            "Bone Athame", "Silver Athame", "Moonstone Amulet", "Sunstone Amulet", "Copper Ring", "Ring of Warding",
            "Apprentice Deck", "Scholar's Deck", "Ironbound Deck", "Starfall Deck",
        };

        [HideInInspector] public string[] descriptions =
        {
            "A plain practice staff every apprentice is handed on day one.",
            "Sturdy, if unflattering, academy robes.",
            "A pointed felt hat. Mostly keeps the rain off.",
            "Comfortable boots for long walks between classes.",
            "A small red vial. Drink it from your backpack, or in battle (uses your turn).",
            "A swirling blue tonic. Spells cost 1 mana per pip; this tops you back up.",
            "A straight oak wand with a carved grip.",
            "Warm to the touch; sparks drift from the tip.",
            "Rimed with frost that never melts.",
            "A tall hat stitched with tiny star charts.",
            "Well-worn boots that never seem to get muddy.",
            "Light robes woven with faint protective runes.",
            "Crackles with static whenever it is drawn.",
            "Leafy green robes that smell faintly of spring.",
            "A dark hood worn by the keepers of old crypts.",
            "A ritual blade carved from a beast's tooth.",
            "A polished blade that turns aside hostile magic.",
            "A pale stone that glows softly at night.",
            "Holds a sliver of trapped sunlight.",
            "A simple band. Helps steady your aim.",
            "Etched with a small warding sigil.",
            "A plain leather deck every apprentice is given. Equip a deck to fight with the spells you put in it.",
            "A scholar's tooled-leather deck with room for a few more spells.",
            "Iron-cornered and heavy; it can hold five copies of a favourite spell.",
            "Embossed with the Starfall crest. A deck for a wizard who knows exactly what they want.",
        };

        [HideInInspector] public Color[] iconColors =
        {
            new Color(0.55f, 0.4f, 0.25f), new Color(0.35f, 0.35f, 0.6f), new Color(0.35f, 0.35f, 0.6f), new Color(0.45f, 0.3f, 0.2f),
            new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.85f),
            new Color(0.6f, 0.45f, 0.25f), new Color(0.85f, 0.35f, 0.1f), new Color(0.45f, 0.75f, 0.95f), new Color(0.3f, 0.3f, 0.7f),
            new Color(0.5f, 0.35f, 0.2f), new Color(0.75f, 0.75f, 0.85f),
            new Color(0.6f, 0.35f, 0.85f), new Color(0.3f, 0.7f, 0.3f), new Color(0.25f, 0.25f, 0.25f),
            new Color(0.85f, 0.8f, 0.7f), new Color(0.8f, 0.8f, 0.85f), new Color(0.8f, 0.85f, 0.95f), new Color(0.95f, 0.75f, 0.2f),
            new Color(0.75f, 0.45f, 0.25f), new Color(0.5f, 0.6f, 0.75f),
            new Color(0.5f, 0.35f, 0.22f), new Color(0.55f, 0.25f, 0.2f), new Color(0.4f, 0.42f, 0.45f), new Color(0.3f, 0.3f, 0.6f),
        };

        public int IndexOf(int itemId)
        {
            if (itemId <= 0) return -1;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == itemId) return i;
            return -1;
        }

        public bool Exists(int itemId) { return IndexOf(itemId) >= 0; }

        public string GetName(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? "Unknown Item" : names[i];
        }

        public string GetDescription(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? "" : descriptions[i];
        }

        public int GetCategory(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? -1 : categories[i];
        }

        public bool IsEquippable(int itemId)
        {
            int c = GetCategory(itemId);
            return c >= 0 && c < CharacterData.EQUIP_SLOTS;
        }

        public int GetLevelReq(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? 1 : levelReqs[i];
        }

        public int GetSchoolReq(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? ANY_SCHOOL : schoolReqs[i];
        }

        public Color GetIconColor(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? Color.gray : iconColors[i];
        }

        public string GetCategoryName(int category)
        {
            return category >= 0 && category < categoryNames.Length ? categoryNames[category] : "";
        }

        public int GetStat(int itemId, int stat)
        {
            int i = IndexOf(itemId);
            if (i < 0) return 0;
            switch (stat)
            {
                case STAT_HEALTH: return healthBonus[i];
                case STAT_MANA: return manaBonus[i];
                case STAT_DAMAGE: return damageBonus[i];
                case STAT_RESIST: return resistBonus[i];
                case STAT_ACCURACY: return accuracyBonus[i];
                case STAT_CRITICAL: return SafeGet(critBonus, i);
                case STAT_BLOCK: return SafeGet(blockBonus, i);
                case STAT_PIERCE: return SafeGet(pierceBonus, i);
                case STAT_POWERPIP: return SafeGet(powerPipBonus, i);
                default: return 0;
            }
        }

        /// <summary>Arrays added after a scene was generated can be shorter than ids until the scene is rebuilt.</summary>
        private int SafeGet(int[] arr, int i) { return arr != null && i >= 0 && i < arr.Length ? arr[i] : 0; }

        private string[] _statNamesFallback = { "Health", "Mana", "Damage", "Resist", "Accuracy", "Critical", "Block", "Pierce", "Power Pip" };

        public string StatName(int stat)
        {
            if (statNames != null && stat >= 0 && stat < statNames.Length) return statNames[stat];
            return stat >= 0 && stat < _statNamesFallback.Length ? _statNamesFallback[stat] : "";
        }

        public bool IsPotion(int itemId) { return GetCategory(itemId) == CATEGORY_POTION; }
        public bool IsDeck(int itemId) { return GetCategory(itemId) == CATEGORY_DECK; }
        /// <summary>Spells the deck holds (falls back to CardDatabase.DECK_MAX).</summary>
        public int GetDeckCapacity(int itemId) { int v = SafeGet(deckCapacity, IndexOf(itemId)); return v > 0 ? v : CardDatabase.DECK_MAX; }
        public int GetDeckMaxCopies(int itemId) { int v = SafeGet(deckMaxCopies, IndexOf(itemId)); return v > 0 ? v : CardDatabase.MAX_COPIES; }
        public int GetPotionHealth(int itemId) { return SafeGet(potionHealth, IndexOf(itemId)); }
        public int GetPotionMana(int itemId) { return SafeGet(potionMana, IndexOf(itemId)); }

        /// <summary>Base stat (health/mana from the character) plus bonuses from all equipped gear.</summary>
        public int GetTotalStat(CharacterData c, int stat)
        {
            if (c == null) return 0;
            c.EnsureArrays();
            int total = stat == STAT_HEALTH ? c.maxHealth : stat == STAT_MANA ? c.maxMana : 0;
            for (int s = 0; s < CharacterData.EQUIP_SLOTS; s++) total += GetStat(c.equipped[s], stat);
            return total;
        }

        /// <summary>Empty string if the character may equip the item, otherwise the reason it can't.</summary>
        public string CheckCanEquip(CharacterData c, int itemId)
        {
            if (c == null || !IsEquippable(itemId)) return "That can't be equipped.";
            if (c.level < GetLevelReq(itemId)) return "Requires level " + GetLevelReq(itemId).ToString() + ".";
            int school = GetSchoolReq(itemId);
            if (school != ANY_SCHOOL && (int)c.school != school) return "Only " + SchoolName(school) + " wizards can use this.";
            return "";
        }

        /// <summary>Multi-line stat summary for the item details panel, e.g. "+20 Health\n+3% Resist".</summary>
        public string GetStatSummary(int itemId)
        {
            string s = "";
            for (int stat = 0; stat < STAT_COUNT; stat++)
            {
                int v = GetStat(itemId, stat);
                if (v == 0) continue;
                bool percent = stat >= STAT_DAMAGE;
                s += "+" + v.ToString() + (percent ? "% " : " ") + StatName(stat) + "\n";
            }
            if (IsDeck(itemId)) s += "Holds " + GetDeckCapacity(itemId).ToString() + " spells, up to " + GetDeckMaxCopies(itemId).ToString() + " copies of each\n";
            if (GetPotionHealth(itemId) > 0) s += "Restores " + GetPotionHealth(itemId).ToString() + " Health\n";
            if (GetPotionMana(itemId) > 0) s += "Restores " + GetPotionMana(itemId).ToString() + " Mana\n";
            int lvl = GetLevelReq(itemId);
            if (lvl > 1) s += "Requires level " + lvl.ToString() + "\n";
            int school = GetSchoolReq(itemId);
            if (school != ANY_SCHOOL) s += SchoolName(school) + " only\n";
            return s;
        }

        public string SchoolName(int school)
        {
            switch (school)
            {
                case 0: return "Fire";
                case 1: return "Ice";
                case 2: return "Storm";
                case 3: return "Life";
                case 4: return "Death";
                case 5: return "Myth";
                case 6: return "Balance";
                default: return "?";
            }
        }
    }
}
