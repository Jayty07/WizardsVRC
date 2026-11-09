using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Static item definitions (World 1's gear, potions and decks). Parallel arrays indexed by definition index;
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

        // World 1's, Dustwind's and Bigdig's items: every stat comes from a level budget for a full set, split by slot (Tools/make_items.py).
        // Sorted by level, so the shops list them in order. Quest rewards and boss drops are unique (never sold).
        [HideInInspector] public int[] ids = { 3, 2, 4, 1, 30, 50, 60, 10, 11, 40, 23, 25, 24, 20, 51, 61, 21, 22, 26, 80, 81, 82, 83, 31, 41, 79, 117, 28, 84, 85, 86, 87, 88, 89, 264, 62, 114, 115, 27, 90, 91, 92, 93, 94, 95, 97, 101, 96, 100, 98, 108, 71, 74, 70, 118, 72, 265, 75, 73, 109, 113, 63, 106, 107, 76, 104, 105, 102, 111, 116, 103, 110, 77, 119, 78, 120, 112, 121, 122, 123, 124, 125, 180, 126, 188, 189, 127, 128, 129, 130, 131, 132, 133, 162, 266, 170, 181, 163, 190, 171, 182, 164, 134, 183, 135, 165, 136, 137, 138, 139, 172, 140, 141, 142, 143, 144, 145, 146, 147, 176, 184, 166, 173, 191, 177, 167, 148, 149, 150, 185, 151, 152, 153, 154, 174, 155, 156, 157, 158, 159, 160, 161, 175, 187, 168, 169, 179, 186, 242, 260, 261, 200, 201, 202, 203, 267, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 253, 243, 262, 254, 244, 250, 214, 215, 255, 216, 245, 217, 218, 219, 220, 256, 221, 222, 223, 224, 225, 226, 227, 246, 249, 228, 229, 230, 231, 232, 233, 247, 257, 234, 248, 263, 235, 236, 237, 238, 239, 240, 241, 251, 258, 259, 252 };
        [HideInInspector] public int[] categories = { 0, 1, 2, 3, 4, 6, 7, 8, 8, 5, 0, 1, 2, 3, 6, 7, 3, 3, 3, 3, 3, 3, 3, 4, 5, 5, 7, 0, 0, 0, 0, 0, 0, 0, 3, 7, 8, 8, 1, 1, 1, 1, 1, 1, 1, 2, 3, 4, 5, 6, 0, 2, 5, 6, 7, 0, 3, 1, 3, 4, 6, 7, 0, 1, 2, 2, 3, 4, 5, 5, 6, 6, 5, 7, 0, 0, 1, 1, 2, 3, 4, 5, 5, 6, 8, 8, 3, 3, 3, 3, 3, 3, 3, 3, 3, 6, 6, 0, 7, 2, 3, 7, 0, 0, 1, 1, 2, 3, 4, 5, 5, 6, 1, 1, 1, 1, 1, 1, 1, 0, 1, 2, 4, 7, 1, 5, 0, 1, 2, 2, 3, 4, 5, 6, 6, 0, 0, 0, 0, 0, 0, 0, 5, 5, 6, 7, 3, 4, 5, 8, 8, 0, 1, 2, 3, 3, 4, 5, 6, 3, 3, 3, 3, 3, 3, 3, 4, 6, 7, 3, 4, 2, 0, 1, 1, 2, 2, 3, 4, 5, 6, 0, 1, 1, 1, 1, 1, 1, 1, 1, 6, 0, 1, 2, 3, 4, 5, 5, 5, 6, 6, 7, 0, 0, 0, 0, 0, 0, 0, 0, 6, 2, 3 };
        [HideInInspector] public int[] levelReqs = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 9, 9, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 16, 16, 16, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 19, 19, 20, 20, 20, 20, 20, 21, 21, 22, 22, 22, 22, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 24, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26, 26, 26, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28, 28, 29, 30, 30, 30, 30, 30, 30, 30, 30, 30, 31, 31, 31, 31, 31, 31, 31, 31, 31, 32, 33, 33, 33, 33, 33, 33, 33, 33, 33, 33, 33, 34, 34, 34, 34, 34, 34, 34, 34, 34, 35, 35 };
        [HideInInspector] public int[] schoolReqs = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, 4, 0, 1, 2, 3, 5, 6, -1, -1, -1, -1, 3, 0, 1, 2, 4, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, -1, -1, -1, -1 };
        [HideInInspector] public int[] healthBonus = { 10, 20, 15, 0, 5, 5, 0, 0, 0, 15, 20, 30, 25, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 5, 15, 20, 0, 30, 25, 40, 25, 35, 30, 30, 0, 0, 0, 0, 60, 45, 65, 45, 50, 50, 55, 45, 0, 10, 25, 10, 50, 55, 35, 10, 0, 50, 0, 85, 0, 15, 15, 0, 60, 85, 80, 70, 0, 15, 55, 45, 15, 20, 55, 0, 80, 70, 130, 105, 90, 0, 20, 50, 60, 20, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 25, 90, 0, 125, 0, 0, 100, 125, 150, 170, 125, 0, 25, 75, 85, 25, 135, 200, 130, 185, 150, 145, 160, 130, 215, 160, 30, 0, 205, 100, 130, 190, 160, 200, 0, 30, 95, 30, 35, 115, 170, 110, 155, 130, 120, 135, 115, 130, 40, 0, 0, 50, 130, 0, 0, 160, 240, 200, 0, 0, 40, 120, 40, 0, 0, 0, 0, 0, 0, 0, 50, 45, 0, 0, 50, 250, 185, 280, 345, 230, 260, 0, 45, 140, 45, 240, 245, 360, 230, 330, 275, 260, 285, 320, 55, 205, 305, 255, 0, 50, 155, 170, 190, 50, 55, 0, 180, 265, 170, 245, 200, 190, 210, 235, 65, 340, 0 };
        [HideInInspector] public int[] manaBonus = { 1, 3, 0, 0, 0, 2, 0, 0, 0, 2, 2, 4, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 8, 6, 6, 6, 6, 6, 6, 0, 0, 0, 3, 3, 4, 0, 4, 4, 0, 4, 0, 9, 0, 0, 5, 0, 4, 9, 0, 0, 0, 0, 6, 4, 4, 6, 5, 0, 6, 5, 13, 10, 0, 0, 0, 5, 6, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 7, 7, 0, 0, 0, 0, 7, 9, 14, 15, 0, 0, 0, 7, 8, 7, 14, 14, 14, 19, 14, 14, 14, 9, 19, 0, 0, 0, 18, 9, 8, 17, 0, 0, 0, 0, 8, 8, 9, 9, 9, 9, 11, 9, 9, 9, 10, 11, 10, 0, 0, 0, 11, 0, 0, 10, 20, 0, 0, 0, 0, 10, 10, 0, 0, 0, 0, 0, 0, 0, 0, 11, 0, 0, 0, 0, 11, 23, 28, 0, 0, 0, 0, 11, 11, 15, 23, 23, 23, 30, 23, 23, 23, 26, 13, 12, 25, 0, 0, 0, 12, 14, 16, 12, 14, 0, 13, 13, 13, 17, 13, 13, 13, 14, 16, 0, 0 };
        [HideInInspector] public int[] damageBonus = { 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 2, 1, 2, 0, 0, 2, 1, 3, 2, 2, 2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 3, 4, 3, 5, 4, 4, 3, 1, 3, 2, 0, 0, 0, 2, 0, 0, 0, 0, 4, 5, 5, 4, 0, 0, 0, 5, 3, 2, 5, 3, 0, 0, 0, 0, 0, 0, 0, 0, 8, 6, 3, 5, 4, 0, 0, 0, 0, 0, 7, 5, 8, 5, 6, 7, 5, 6, 6, 0, 0, 0, 0, 4, 8, 0, 0, 0, 8, 9, 4, 7, 5, 0, 0, 0, 11, 7, 13, 8, 10, 10, 8, 0, 12, 4, 6, 0, 11, 0, 0, 10, 4, 5, 9, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 8, 0, 0, 0, 0, 12, 5, 10, 10, 7, 0, 0, 14, 9, 16, 9, 12, 12, 10, 9, 0, 0, 14, 8, 6, 0, 14, 17, 6, 7, 12, 8, 0, 0, 0, 19, 11, 21, 12, 16, 16, 14, 16, 0, 0, 15, 7, 13, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 15 };
        [HideInInspector] public int[] resistBonus = { 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 2, 1, 1, 1, 2, 3, 0, 0, 0, 1, 2, 3, 0, 2, 0, 2, 0, 2, 0, 0, 2, 0, 2, 2, 4, 4, 0, 0, 0, 0, 2, 2, 0, 0, 2, 2, 3, 2, 4, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 0, 6, 0, 0, 3, 4, 3, 3, 6, 0, 0, 0, 0, 3, 2, 4, 2, 3, 3, 3, 4, 3, 4, 7, 0, 0, 4, 0, 3, 3, 7, 9, 0, 0, 0, 3, 4, 3, 5, 3, 4, 4, 4, 4, 0, 0, 4, 0, 0, 0, 0, 0, 0, 4, 4, 8, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 10, 5, 5, 6, 9, 10, 0, 0, 0, 5, 6, 4, 7, 4, 5, 5, 5, 6, 5, 5, 5, 5, 10, 0, 0, 0, 0, 0, 5, 6, 0, 4, 7, 4, 5, 5, 5, 6, 6, 6, 13, 0 };
        [HideInInspector] public int[] accuracyBonus = { 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 2, 1, 1, 1, 0, 0, 0, 0, 2, 2, 2, 1, 2, 2, 2, 2, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0, 2, 0, 0, 1, 2, 0, 0, 1, 0, 2, 2, 2, 2, 0, 2, 0, 2, 2, 0, 0, 2, 0, 0, 0, 2, 2, 0, 0, 3, 3, 2, 2, 0, 3, 0, 0, 0, 2, 0, 0, 3, 3, 2, 4, 3, 3, 3, 3, 3, 2, 2, 3, 0, 0, 4, 0, 3, 4, 2, 2, 0, 3, 0, 0, 0, 2, 2, 2, 2, 3, 2, 2, 3, 4, 3, 0, 0, 0, 3, 0, 4, 3, 0, 0, 4, 0, 0, 3, 3, 4, 4, 3, 5, 4, 4, 5, 0, 0, 3, 0, 5, 0, 0, 0, 0, 5, 3, 0, 5, 5, 0, 0, 3, 5, 5, 4, 6, 5, 5, 6, 0, 4, 0, 6, 0, 0, 5, 4, 5, 0, 0, 5, 0, 0, 4, 7, 4, 4, 3, 5, 4, 4, 4, 4, 4, 6, 4, 0, 6, 0, 0, 0, 0, 4, 4, 0, 6, 6, 5, 8, 6, 6, 7, 7, 5, 0, 7 };
        [HideInInspector] public int[] critBonus = { 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 2, 3, 2, 2, 2, 2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 3, 0, 2, 0, 0, 3, 1, 0, 1, 0, 0, 0, 4, 0, 0, 0, 1, 0, 3, 2, 0, 0, 3, 0, 5, 5, 0, 0, 0, 2, 0, 3, 3, 0, 0, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 5, 0, 0, 3, 0, 6, 7, 0, 0, 0, 2, 0, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 4, 7, 0, 0, 0, 3, 0, 4, 0, 0, 9, 7, 7, 7, 8, 7, 7, 5, 5, 0, 0, 3, 0, 5, 0, 0, 8, 0, 0, 3, 3, 0, 5, 0, 4, 3, 3, 3, 4, 3, 3, 0, 0, 0, 4, 0, 0, 9, 0, 0, 0, 0, 4, 0, 5, 0, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 4, 0, 6, 7, 7, 0, 0, 0, 13, 10, 10, 10, 11, 10, 10, 11, 0, 0, 5 };
        [HideInInspector] public int[] blockBonus = { 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 1, 0, 2, 0, 0, 0, 0, 0, 2, 3, 0, 0, 0, 2, 2, 0, 2, 0, 0, 2, 3, 0, 0, 0, 0, 0, 0, 2, 0, 2, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 4, 0, 0, 3, 0, 0, 0, 0, 0, 0, 3, 0, 3, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 0, 0, 0, 0, 0, 3, 4, 0, 3, 0, 4, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 5, 0, 0, 0, 0, 0, 4, 0, 0, 4, 0, 5, 0, 0, 0, 0, 0, 0, 0, 5, 6, 0, 0, 4, 5, 0, 0, 0, 4, 5, 0, 4, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 5, 0, 5, 0, 0, 0, 6, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 6, 0 };
        [HideInInspector] public int[] pierceBonus = { 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 2, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 0, 2, 2, 2, 2, 3, 2, 2, 2, 2, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 3, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 4, 0, 0, 0, 0, 0, 0, 3, 3, 3, 0, 0, 3, 3, 3, 3, 7, 3, 3, 4, 0, 0, 4, 4, 0, 0, 0, 0, 0, 0, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5 };
        [HideInInspector] public int[] powerPipBonus = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 4, 4, 4, 4, 5, 4, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 8, 7, 0, 0, 8, 0, 0, 0, 10, 8, 0, 0, 0, 8, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11, 12, 0, 0, 0, 11, 12, 0, 12, 12, 12, 12, 12, 15, 12, 0, 15, 0, 0, 0, 14, 14, 0, 14, 0, 0, 0, 0, 14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 16, 18, 0, 0, 0, 0, 17, 0, 0, 0, 16, 0, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 19, 23, 0, 0, 0, 0, 19, 0, 0, 19, 19, 19, 19, 19, 25, 19, 22, 0, 0, 21, 0, 0, 0, 21, 23, 26, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        /// <summary>Quest rewards and boss drops: never sold in shops or rolled as random loot.</summary>
        [HideInInspector] public int[] uniqueItems = { 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 0, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 };
        /// <summary>Decks only: how many spells the deck holds, and how many copies of one spell.</summary>
        [HideInInspector] public int[] deckCapacity = { 0, 0, 0, 0, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0, 0, 25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 27, 0, 0, 0, 0, 0, 0, 0, 0, 30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 33, 0, 0, 0, 0, 0, 0, 40, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 38, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 42, 0, 0, 45, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 48, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 52, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 55, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        [HideInInspector] public int[] deckMaxCopies = { 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        /// <summary>Potions only: health / mana restored when drunk (outside battle from the backpack, or in battle as your turn).</summary>
        [HideInInspector] public int[] potionHealth = { 0, 0, 0, 0, 0, 0, 0, 150, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 300, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 500, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 900, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        [HideInInspector] public int[] potionMana = { 0, 0, 0, 0, 0, 0, 0, 0, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 24, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 36, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 60, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        [HideInInspector] public string[] names =
        {
            "Apprentice Hat",
            "Apprentice Robe",
            "Apprentice Boots",
            "Training Staff",
            "Bone Athame",
            "Copper Ring",
            "Apprentice Deck",
            "Minor Healing Potion",
            "Mana Draught",
            "Moonstone Amulet",
            "Scholar's Hat",
            "Silkweave Robe",
            "Traveler's Boots",
            "Oakwood Wand",
            "Ring of Warding",
            "Scholar's Deck",
            "Ember Wand",
            "Frost-Etched Wand",
            "Thunderhorn Wand",
            "Thornbriar Wand",
            "Gravebone Wand",
            "Fablewood Wand",
            "Sandglass Wand",
            "Silver Athame",
            "Sunstone Amulet",
            "Ashwick's Lantern",
            "Traveler's Deck",
            "Gravewarden Cowl",
            "Flamewarden Hood",
            "Rimeguard Hood",
            "Stormcaller Hat",
            "Briarleaf Hat",
            "Fabler's Cap",
            "Equinox Hat",
            "Runecutter Saber",
            "Ironbound Deck",
            "Healing Potion",
            "Mana Tonic",
            "Verdant Vestments",
            "Emberweave Robe",
            "Frostmantle Robe",
            "Tempest Robe",
            "Gloamshroud Robe",
            "Legendweave Robe",
            "Meridian Robe",
            "Kilnstone Boots",
            "Ashwood Wand",
            "Tidecaller Athame",
            "Lamplight Pendant",
            "Brass Signet",
            "Grell's Tricorn",
            "Sea-Grey Boots",
            "Starglass Amulet",
            "Harbormaster's Ring",
            "Seafarer's Deck",
            "Forgewarden's Cap",
            "Conductor's Baton",
            "Grove Warden Robe",
            "Kilnfire Wand",
            "Corps Lantern-Blade",
            "Kraken-Eye Ring",
            "Starfall Deck",
            "Archivist's Hat",
            "Starweave Robe",
            "Lightfoot Boots",
            "Keepwatch Boots",
            "Crystalline Wand",
            "Wardsteel Athame",
            "Cipher Locket",
            "Starlight Locket",
            "Starfall Signet",
            "Legion Bulwark Ring",
            "Hollowbane Amulet",
            "Warden's Deck",
            "Hollow Crown",
            "Drover's Hat",
            "Hollow King's Mantle",
            "Prairie Robe",
            "Boardwalk Boots",
            "Mesquite Wand",
            "Frontier Athame",
            "Brass Compass Pendant",
            "Tin Star Badge",
            "Horseshoe Ring",
            "Greater Healing Potion",
            "Strong Mana Tonic",
            "Cinderspur Wand",
            "Hailstone Wand",
            "Thunderhead Wand",
            "Saguaro Wand",
            "Rattlebone Wand",
            "Tall-Tale Wand",
            "Dust Devil Wand",
            "Frontier Sixwand",
            "Wisp Lantern",
            "Express Office Ring",
            "Bad Penny's Lucky Coin",
            "Trail Hat",
            "Drifter's Deck",
            "Creekwater Boots",
            "Mesquite Heartwood Wand",
            "Saddlebag Deck",
            "Wide-Brim Hat",
            "Grist's Hard Hat",
            "Trailworn Robe",
            "Frontier Duster",
            "Snakeskin Boots",
            "Ironwood Wand",
            "Railspike Athame",
            "Lantern-Ore Pendant",
            "Miner's Lamp Pendant",
            "Silver Dollar Ring",
            "Ember Duster",
            "Frostline Duster",
            "Stormrider Duster",
            "Bloomfield Duster",
            "Grim Drifter Duster",
            "Storyteller's Duster",
            "Sundial Duster",
            "Pumpwright's Hat",
            "Vesper's Engine Coat",
            "Spurred Boots",
            "Signalman's Athame",
            "Wrangler's Deck",
            "Bounty Hunter's Duster",
            "Lodestone Amulet",
            "Mesa Hat",
            "Canyon Robe",
            "High Desert Boots",
            "Warden's Stoneplate Boots",
            "Thunderwood Wand",
            "Wardenstone Athame",
            "Sunset Locket",
            "Prospector's Signet",
            "Warden-Stone Ring",
            "Blazebrim Hat",
            "Frostbrim Hat",
            "Stormbrim Hat",
            "Bloombrim Hat",
            "Gravebrim Hat",
            "Talebrim Hat",
            "Sunbrim Hat",
            "Ghostlight Lantern",
            "Conductor's Pocketwatch",
            "Silver Spur Ring",
            "Lawbringer's Deck",
            "Last Spike Wand",
            "Calloway's Iron Spike",
            "Deepwarden's Lamp",
            "Superior Healing Potion",
            "Fizzing Mana Bomb",
            "Tunneler's Cap",
            "Coalcloth Robe",
            "Hobnail Boots",
            "Sparkplug Wand",
            "Dominion Sidearm",
            "Rivet Athame",
            "Gear Pendant",
            "Nut-and-Bolt Ring",
            "Furnace Wand",
            "Coolant Wand",
            "Arc-Coil Wand",
            "Mossbulb Wand",
            "Gravedigger Wand",
            "Tall-Fuse Wand",
            "Gyroscope Wand",
            "Copperjaw's Knuckles",
            "Company Signet",
            "Tinker's Deck",
            "Vane's Drill-Bit Wand",
            "Rustclaw Pick Athame",
            "Runner's Boots",
            "Neon Brim Hat",
            "Velvet Gambler's Robe",
            "Sorrel's Marine Coat",
            "Brass-Toe Boots",
            "Smuggler's Boots",
            "Lucky-Strike Wand",
            "Card-Edge Athame",
            "Glowbulb Amulet",
            "Loaded Die Ring",
            "Nightglass Monocle",
            "Smelter's Robe",
            "Coldforge Robe",
            "Dynamo Robe",
            "Mosslined Robe",
            "Undertunnel Robe",
            "Showman's Robe",
            "Assayer's Robe",
            "Glimmerfang Jacket",
            "Lucky Lou's Dice Ring",
            "Highroller Top Hat",
            "Gilded Waistcoat Robe",
            "Velvet Boots",
            "Golden Seam Wand",
            "Vault Key Athame",
            "Jackpot Locket",
            "Ward-Rune Pendant",
            "Strake's Siphon Core",
            "Mother Lode Ring",
            "Handshake Ring",
            "Highroller's Deck",
            "Furnace Goggles Hat",
            "Frostlamp Hat",
            "Tesla Crown",
            "Lantern-Moss Hat",
            "Pit Boss Hat",
            "Ringmaster's Hat",
            "Scales Hat",
            "High Roller's Hat",
            "The Assayer's Scales",
            "Iron Wall Greaves",
            "All In Wand",
        };

        [HideInInspector] public string[] descriptions =
        {
            "A pointed felt hat. Mostly keeps the rain off.",
            "Sturdy, if unflattering, academy robes.",
            "Comfortable boots for long walks between classes.",
            "A plain practice staff every apprentice is handed on day one. It holds one basic spell from every school.",
            "A ritual blade carved from a beast's tooth. Duel Master Holloway's prize for your first duel.",
            "A simple band. Helps steady your aim.",
            "A plain leather deck every apprentice is given. Equip a deck to fight with the spells you put in it.",
            "A small red vial. Drink it from your backpack, or in battle (uses your turn).",
            "A swirling blue tonic. Spells cost 1 mana per pip; this tops you back up.",
            "A pale stone that glows softly at night.",
            "A tall hat stitched with tiny star charts.",
            "Light robes woven with faint protective runes. Gloomfang's guards wore them.",
            "Well-worn boots that never seem to get muddy.",
            "A straight oak wand with a carved grip.",
            "Etched with a small warding sigil.",
            "A scholar's tooled-leather deck with room for a few more spells. Old Briarwick guards one.",
            "Warm to the touch; sparks drift from the tip.",
            "Rimed with frost that never melts.",
            "Crackles with static whenever it is drawn.",
            "A living briar wand that sprouts a new leaf every morning.",
            "Pale bone bound in black thread. It hums near graveyards.",
            "Carved with tiny scenes from old stories that seem to move.",
            "A slim hourglass of shifting sand set in a brass grip.",
            "A polished blade that turns aside hostile magic. Taken from the Briar Heart.",
            "Holds a sliver of trapped sunlight.",
            "The Snuffer's own lantern, relit. It burns without oil.",
            "A canvas-bound deck with a strap for the road.",
            "A dark hood worn by the keepers of old crypts.",
            "A hood of ember-red felt that never quite stops smoking.",
            "Lined with frost-white fur; snow settles on it even indoors.",
            "A wide-brimmed hat that makes your hair stand on end.",
            "Woven from living leaves that turn toward the sun.",
            "A storyteller's cap with a long feather that writes on its own.",
            "Half day-gold, half night-blue, stitched down the middle.",
            "A duelling sabre with a rune-etched blade. Cast by slashing the rune's strokes through the air, quickly, one cut at a time.",
            "Iron-cornered and heavy; it can hold five copies of a favourite spell.",
            "A full red flask. Restores much more than a minor potion.",
            "A deep blue tonic that fizzes on the tongue.",
            "Leafy green robes that smell faintly of spring.",
            "Woven with threads of banked fire; warm in any weather.",
            "Heavy blue robes that crackle with rime when you move.",
            "Violet robes whose hem flutters in a wind no one else feels.",
            "Grey-black robes that drink in lamplight.",
            "Embroidered with heroes and beasts from old tales.",
            "Sand-coloured robes marked with the four winds.",
            "Hobnailed boots with soles of fired clay. They don't burn.",
            "Pale ash wood, oiled and balanced. A good wand for any school.",
            "A curved blade of sea-glass that hums like a shell.",
            "A tiny lantern on a chain, lit by Lantern Row's candlemakers.",
            "A heavy brass ring from a Kiln Street smith.",
            "Captain Marrow Grell's battered hat. There's a coded note still tucked in the band.",
            "Oiled sealskin boots that never slip on a wet pier.",
            "A sliver of the Observatory's old lens, set in silver.",
            "Quill's own signet ring, worn smooth by salt and rope.",
            "Waxed against the salt; its pages never curl.",
            "A soot-stained leather cap with a brass brim. Smells of hot iron.",
            "An ebony baton with a silver tip. Cast in rhythm: touch each point of the rune on the beat of its metronome.",
            "Moss-green robes of the Thornhollow wardens, patched with bark.",
            "Quenched in a Kiln Street furnace; the grip is still warm.",
            "Captain Vesper's blade, its edge lit by a caged spark of stolen light.",
            "A cold gold ring set with one of the Kraken's smaller eyes. It blinks.",
            "Embossed with the Starfall crest. A deck for a wizard who knows exactly what they want.",
            "Librarian Wren's design: a hat with pockets for notes.",
            "Robes woven with silver thread that glints like the night sky.",
            "Light enough to catch a spy who runs.",
            "Tall boots worn by the castle's night watch.",
            "Grown, not carved, from Starfall crystal.",
            "Castle-forged steel etched with the Starfall crest.",
            "Selwyn's locket. Inside is a cipher wheel instead of a portrait.",
            "A locket that holds a pinch of the rekindled crystal's light.",
            "A silver ring set with a chip of fallen star.",
            "Sergeant Roan's ring, stamped with the Dominion's iron sun.",
            "A shard of a shattered Hollow Lantern, its stolen light set free.",
            "Bound in Thornhollow bark; the castle's best deck for coin.",
            "Morvane's crown. It is lighter than it looks, and colder.",
            "A wide felt hat with a sweat-stained band. Keeps the desert sun off your nose.",
            "Morvane's mantle, heavy with the light he stole and no longer holds.",
            "Sun-faded robes of tough canvas, cut for long days in the saddle.",
            "Heeled boots that ring out on every plank in Archrock.",
            "Knotty desert hardwood, smoothed by a thousand hands.",
            "A broad camp blade with an antler grip.",
            "Always points back to Archrock, whichever way you're facing.",
            "Archrock's deputy badge. Wear it with pride, partner.",
            "A tiny bent horseshoe, worn points-up so the luck doesn't run out.",
            "A big red bottle with a cork stopper. Tastes like cherries and gunpowder smoke.",
            "A dark blue tonic. Fizzes so hard the cork pops.",
            "Its tip glows like a coal in a campfire.",
            "Cold as a desert night, and twice as clear.",
            "Smells of rain before a storm breaks over the mesa.",
            "Carved from a fallen cactus rib; it still flowers once a year.",
            "Rattles softly when trouble is near.",
            "Every story told about it is bigger than the last.",
            "A little whirl of sand follows it everywhere.",
            "Six notches on the grip, one for every spell school but yours. Marshal Quickwater's gift.",
            "A little brass lantern on a rod. Its wisp runs the rune's path; keep the lantern in its glow all the way.",
            "Given to anyone who saves the mail. Stamped 'Archrock Express'.",
            "A bent copper penny on a ring. Bad Penny says it was never lucky for her.",
            "Dusty, dented and perfect.",
            "A deck in a worn leather pouch, strung on a cord.",
            "Still damp from the creek you set flowing again.",
            "Heartwood from Old Mesquite himself, given back once he calmed down.",
            "Room for more spells than you'll ever need. Then you'll need them.",
            "Brim wide enough to shade a friend.",
            "Foreman Grist's dented hat, its lamp still burning stolen light.",
            "Patched and re-patched on the long trail out of town.",
            "The long coat of a frontier wizard, cut to swirl when you draw your wand.",
            "Shed by a rattlesnake, not taken from one. The bootmaker swears it.",
            "Desert ironwood: so dense it sinks in water.",
            "Forged from a spike pried out of the Iron Line.",
            "A chip of light-ore, freed from the Corps and glowing gently.",
            "A tiny brass mine lamp that lights the way for those who are lost.",
            "A silver coin hammered round a finger. It still shows the eagle.",
            "A long coat that trails sparks instead of dust.",
            "Frost creeps along its hem even at high noon.",
            "Its tails snap like a flag in a gale.",
            "Wildflowers sprout from its pockets after rain.",
            "Faded black, and quiet as an empty street.",
            "Every patch on it comes with a tale.",
            "Its shadow always falls the right way for the hour.",
            "Oil-stained and proud of it. Archrock's water flows because of you.",
            "Captain Vesper's soot-black coat with brass buttons. It smells of coal smoke.",
            "Their spurs jingle with every step. Sneaking is out of the question.",
            "Its edge is painted red and white like a signal arm.",
            "Stitched with rawhide; it holds a herd of spells.",
            "Every outlaw in Dustwind knows the sound of this coat.",
            "A magnetic stone that pulls stray magic back to you.",
            "A tall crowned hat the color of red rock at sunset.",
            "Layered in the banded colors of the canyon walls.",
            "Built for scree, sand and the long climb up Thunder Mesa.",
            "Heavy as the mesa and just as steady.",
            "Cut from a tree the lightning chose twice.",
            "Chipped from a fallen Warden stone, with its blessing.",
            "Holds the last light of a desert evening.",
            "Its seal is a pick crossed with a pan.",
            "The Wardens' trust, set in stone.",
            "A brim that never stops smoldering.",
            "Rime on the brim, even in July.",
            "The band crackles when clouds gather.",
            "A hatband of desert blossoms that never wilt.",
            "Worn low, by those who walk at dusk.",
            "A feather in its band that once belonged to a legend, apparently.",
            "Half gold, half shadow, like the mesa at noon.",
            "A ghost's lantern, handed on with thanks. It never casts a shadow.",
            "The Ghost Conductor's watch. It always reads midnight.",
            "A ring shaped like a tiny spur. Its rowel still turns.",
            "Bound in a marshal's tooled leather, with a tin star on the cover.",
            "Hammered from the Iron Line's last spike, which was never driven.",
            "Ironhand Calloway's golden spike, meant for the Heartstone.",
            "Lit from the Mother Lode the year Bruma Stonehum became Deepwarden.",
            "A fizzing red bottle from Fuse & Fizz. Tastes like cinnamon and sparks.",
            "A blue sphere with a fuse. Drink it before it goes off.",
            "A padded leather cap with a lamp clip. Mind your head down there.",
            "Woven from fireproof coalcloth; soot slides right off.",
            "Iron-studded soles that grip wet stone and spark on cobbles.",
            "A brass wand with a crackling gap at its tip.",
            "A captured Dominion service pistol, re-chambered by Bigdig tinkers for rune-shot. Cast by shooting each target as it appears.",
            "Hammered from a single giant rivet. Surprisingly sharp.",
            "A little cog that turns all by itself.",
            "A nut, a bolt, and a kobold's idea of romance.",
            "Hot enough to light a forge, if you hold it the wrong way round.",
            "Frost beads on it in the hottest smelter.",
            "A copper coil that hums with stored lightning.",
            "Glowing cave-moss grows along its grip.",
            "Carved from a deep-seam pit prop. It still smells of the dark.",
            "Its fuse burns forever and never reaches the end.",
            "Three brass rings spin round its tip, always level.",
            "Copperjaw Grubb's brass knuckles, filed into a blade.",
            "The Bigdig Prospecting Company's seal: a nugget in a grinning mouth. A small share, Madame Ozza insists.",
            "A deck in a riveted brass case. It clicks shut very satisfyingly.",
            "The tip of the Siphon Drill, still spinning slowly.",
            "Grandmaw Hesk's old pick, sharpened into a blade.",
            "Runner Pip's old boots, retired now that Pip has a cart.",
            "Its band glows pink in the dark, so your friends can find you on the Glimmer Mile.",
            "Deep pockets. Very deep. Some say bottomless.",
            "A Threadfleet officer's coat, blue and grey, salt-stained.",
            "Brass toecaps, for kicking doors and the odd dice.",
            "Big Pinch's own. They never leave a footprint.",
            "Strikes like a match against any surface. Usually lights.",
            "A blade as thin as a playing card, and just as tricky.",
            "A little bulb of captured neon on a chain.",
            "A tiny die set in brass. It always lands on six. Always.",
            "Agent Nightglass's monocle on a dark hood. It sees in the dark.",
            "A heavy leather robe scorched black at the hem.",
            "Rime creeps along its seams even beside a furnace.",
            "Copper threads hum faintly when you move.",
            "Lined with soft glowing moss from the deep seams.",
            "The colour of tunnels where no lamp has ever been.",
            "Spangled, sequinned and impossible to ignore.",
            "A neat grey robe with a set of tiny scales embroidered on the breast.",
            "Neon-lined, loud, and impossible to lose in the dark. Tixi's own.",
            "Lou's spare dice, set in brass. Roll them when you need a little luck.",
            "A tall black top hat with a gold band. Doors open for it.",
            "Gold thread, velvet lapels, and a pocket watch that's never wrong.",
            "Soft as a whisper on a casino carpet.",
            "Cut from a seam of singing gold. It hums when you cast.",
            "A key so big it doubles as a blade. It opens most things.",
            "Open it and three cherries line up inside.",
            "Cut from one of Bigdig's old ward runes. It remembers the light.",
            "The humming heart of the siphon works, turned to better use.",
            "A ring of raw gold that sings, very faintly, at night.",
            "Every gang boss in Bigdig spat on it. Binding till the gold stops singing.",
            "Gilt-edged cards in a velvet box. The house's own.",
            "Smoked-glass goggles on a heat-proof cap.",
            "A miner's lamp that burns cold blue.",
            "Little lightning arcs between its copper points.",
            "Glowing moss grows thick along the brim.",
            "Black felt, low brim, seen in the deepest seams.",
            "A tall striped hat for a spectacular entrance.",
            "A neat hat with a tiny balance on its crown, always level.",
            "Lulu's gift. Wear it when you walk into the vault.",
            "Quibb Silvernose's tiny scales. They weigh true now.",
            "Marshal Brannoch's greaves. They have never taken a step back.",
            "A wand of singing gold from the Mother Lode itself. Bigdig thanks you.",
        };

        [HideInInspector] public Color[] iconColors =
        {
            new Color(0.35f, 0.35f, 0.6f),
            new Color(0.35f, 0.35f, 0.6f),
            new Color(0.45f, 0.3f, 0.2f),
            new Color(0.55f, 0.4f, 0.25f),
            new Color(0.85f, 0.8f, 0.7f),
            new Color(0.75f, 0.45f, 0.25f),
            new Color(0.5f, 0.35f, 0.22f),
            new Color(0.8f, 0.2f, 0.2f),
            new Color(0.2f, 0.4f, 0.85f),
            new Color(0.8f, 0.85f, 0.95f),
            new Color(0.3f, 0.3f, 0.7f),
            new Color(0.75f, 0.75f, 0.85f),
            new Color(0.5f, 0.35f, 0.2f),
            new Color(0.6f, 0.45f, 0.25f),
            new Color(0.5f, 0.6f, 0.75f),
            new Color(0.55f, 0.25f, 0.2f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.8f, 0.8f, 0.85f),
            new Color(0.95f, 0.75f, 0.2f),
            new Color(1.0f, 0.65f, 0.3f),
            new Color(0.5f, 0.45f, 0.3f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.75f, 0.85f, 0.95f),
            new Color(0.4f, 0.42f, 0.45f),
            new Color(0.85f, 0.15f, 0.2f),
            new Color(0.15f, 0.35f, 0.9f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.55f, 0.35f, 0.25f),
            new Color(0.75f, 0.65f, 0.5f),
            new Color(0.45f, 0.7f, 0.75f),
            new Color(1.0f, 0.8f, 0.45f),
            new Color(0.8f, 0.65f, 0.3f),
            new Color(0.25f, 0.22f, 0.28f),
            new Color(0.45f, 0.5f, 0.55f),
            new Color(0.7f, 0.8f, 1.0f),
            new Color(0.7f, 0.65f, 0.55f),
            new Color(0.3f, 0.45f, 0.55f),
            new Color(0.5f, 0.32f, 0.18f),
            new Color(0.2f, 0.18f, 0.22f),
            new Color(0.3f, 0.5f, 0.25f),
            new Color(0.95f, 0.45f, 0.15f),
            new Color(0.9f, 0.35f, 0.2f),
            new Color(0.35f, 0.75f, 0.7f),
            new Color(0.3f, 0.3f, 0.6f),
            new Color(0.45f, 0.35f, 0.6f),
            new Color(0.25f, 0.3f, 0.6f),
            new Color(0.6f, 0.55f, 0.45f),
            new Color(0.35f, 0.3f, 0.4f),
            new Color(0.6f, 0.8f, 1.0f),
            new Color(0.7f, 0.72f, 0.8f),
            new Color(0.45f, 0.4f, 0.6f),
            new Color(0.75f, 0.85f, 1.0f),
            new Color(0.65f, 0.75f, 1.0f),
            new Color(0.5f, 0.55f, 0.65f),
            new Color(0.6f, 0.5f, 0.9f),
            new Color(0.3f, 0.4f, 0.25f),
            new Color(0.25f, 0.22f, 0.32f),
            new Color(0.55f, 0.42f, 0.28f),
            new Color(0.2f, 0.18f, 0.3f),
            new Color(0.7f, 0.6f, 0.45f),
            new Color(0.45f, 0.3f, 0.18f),
            new Color(0.6f, 0.4f, 0.22f),
            new Color(0.75f, 0.72f, 0.65f),
            new Color(0.85f, 0.65f, 0.3f),
            new Color(0.75f, 0.75f, 0.78f),
            new Color(0.6f, 0.6f, 0.62f),
            new Color(0.9f, 0.12f, 0.18f),
            new Color(0.12f, 0.3f, 0.85f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.55f, 0.5f, 0.45f),
            new Color(0.55f, 0.95f, 0.85f),
            new Color(0.7f, 0.55f, 0.35f),
            new Color(0.75f, 0.45f, 0.25f),
            new Color(0.5f, 0.38f, 0.25f),
            new Color(0.5f, 0.38f, 0.25f),
            new Color(0.35f, 0.5f, 0.6f),
            new Color(0.4f, 0.6f, 0.3f),
            new Color(0.55f, 0.35f, 0.2f),
            new Color(0.42f, 0.32f, 0.22f),
            new Color(0.85f, 0.7f, 0.2f),
            new Color(0.55f, 0.45f, 0.35f),
            new Color(0.5f, 0.4f, 0.3f),
            new Color(0.5f, 0.45f, 0.3f),
            new Color(0.35f, 0.28f, 0.22f),
            new Color(0.5f, 0.5f, 0.55f),
            new Color(1.0f, 0.75f, 0.35f),
            new Color(1.0f, 0.85f, 0.45f),
            new Color(0.8f, 0.82f, 0.85f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.4f, 0.45f, 0.5f),
            new Color(0.22f, 0.2f, 0.24f),
            new Color(0.35f, 0.25f, 0.18f),
            new Color(0.85f, 0.25f, 0.2f),
            new Color(0.45f, 0.3f, 0.2f),
            new Color(0.3f, 0.25f, 0.22f),
            new Color(0.35f, 0.35f, 0.4f),
            new Color(0.65f, 0.3f, 0.2f),
            new Color(0.72f, 0.42f, 0.28f),
            new Color(0.4f, 0.3f, 0.25f),
            new Color(0.6f, 0.55f, 0.5f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.7f, 0.6f, 0.5f),
            new Color(1.0f, 0.55f, 0.35f),
            new Color(0.85f, 0.7f, 0.35f),
            new Color(0.7f, 0.62f, 0.5f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.6f, 0.9f, 0.85f),
            new Color(0.65f, 0.75f, 0.8f),
            new Color(0.82f, 0.84f, 0.88f),
            new Color(0.4f, 0.35f, 0.3f),
            new Color(0.9f, 0.75f, 0.4f),
            new Color(0.85f, 0.7f, 0.3f),
            new Color(1.0f, 0.82f, 0.4f),
            new Color(0.95f, 0.15f, 0.2f),
            new Color(0.15f, 0.35f, 0.95f),
            new Color(0.45f, 0.35f, 0.25f),
            new Color(0.3f, 0.28f, 0.3f),
            new Color(0.35f, 0.28f, 0.22f),
            new Color(0.8f, 0.65f, 0.3f),
            new Color(0.35f, 0.38f, 0.45f),
            new Color(0.6f, 0.55f, 0.5f),
            new Color(0.8f, 0.7f, 0.4f),
            new Color(0.65f, 0.62f, 0.6f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.8f, 0.5f, 0.25f),
            new Color(0.9f, 0.7f, 0.2f),
            new Color(0.7f, 0.55f, 0.3f),
            new Color(0.5f, 0.5f, 0.55f),
            new Color(0.65f, 0.35f, 0.2f),
            new Color(0.7f, 0.4f, 0.15f),
            new Color(0.9f, 0.3f, 0.75f),
            new Color(0.45f, 0.15f, 0.3f),
            new Color(0.25f, 0.3f, 0.45f),
            new Color(0.7f, 0.55f, 0.3f),
            new Color(0.35f, 0.4f, 0.25f),
            new Color(0.85f, 0.3f, 0.2f),
            new Color(0.85f, 0.85f, 0.9f),
            new Color(0.4f, 0.95f, 0.85f),
            new Color(0.95f, 0.95f, 0.9f),
            new Color(0.15f, 0.15f, 0.22f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.6f, 0.25f, 0.8f),
            new Color(0.95f, 0.95f, 0.92f),
            new Color(0.15f, 0.12f, 0.12f),
            new Color(0.75f, 0.6f, 0.2f),
            new Color(0.4f, 0.12f, 0.2f),
            new Color(0.95f, 0.8f, 0.3f),
            new Color(0.8f, 0.7f, 0.45f),
            new Color(0.9f, 0.2f, 0.25f),
            new Color(0.6f, 0.85f, 1.0f),
            new Color(0.55f, 0.2f, 0.15f),
            new Color(1.0f, 0.85f, 0.35f),
            new Color(0.85f, 0.75f, 0.5f),
            new Color(0.6f, 0.15f, 0.2f),
            new Color(0.95f, 0.35f, 0.15f),
            new Color(0.55f, 0.85f, 1.0f),
            new Color(0.65f, 0.4f, 0.95f),
            new Color(0.4f, 0.8f, 0.35f),
            new Color(0.45f, 0.4f, 0.55f),
            new Color(0.95f, 0.78f, 0.25f),
            new Color(0.85f, 0.6f, 0.4f),
            new Color(0.2f, 0.15f, 0.15f),
            new Color(0.85f, 0.8f, 0.65f),
            new Color(0.35f, 0.33f, 0.38f),
            new Color(1.0f, 0.85f, 0.3f),
        };

        public int IndexOf(int itemId)
        {
            if (itemId <= 0) return -1;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == itemId) return i;
            return -1;
        }

        public bool Exists(int itemId) { return IndexOf(itemId) >= 0; }

        [Header("Item cards (spells granted by gear)")]
        [Tooltip("Wizard101 item cards: while the item in itemCardItems[i] is equipped, the spell itemCardIds[i] is added to " +
                 "your battle deck once (on top of your deck, not using its space), and anyone can cast it whatever their school. " +
                 "List an item several times to give several spells.")]
        public int[] itemCardItems = { 1, 1, 1, 1, 1, 1, 1 };
        [Tooltip("The Training Staff (item 1) gives one basic spell of every school: Fire, Ice, Storm, Life, Death, Myth, Balance.")]
        public int[] itemCardIds = { 100, 112, 124, 136, 148, 160, 172 };
        [Tooltip("Every wand with no item cards of its own (all but the Training Staff) adds this many copies of a basic " +
                 "attack to the battle deck: its school's basic spell, or for a wand any school can use, the wizard's own.")]
        public int wandBasicCopies = 7;
        [Tooltip("For listing item cards by name.")]
        public CardDatabase cardDatabase;

        [Header("Casting styles (how a wand draws a spell's rune: CastPattern.STYLE_*)")]
        [Tooltip("While the wand castStyleItems[i] is equipped, runes are drawn in style castStyleKinds[i] (1 Runecutter blade: " +
                 "slash the strokes; 2 Dominion sidearm: shoot the targets; 3 Conductor's baton: touch the points on the beat; " +
                 "4 Wisp lantern: follow the wisp). Any other wand draws them the usual way (0: touch the points in order).")]
        public int[] castStyleItems = { 264, 267, 265, 266 };
        public int[] castStyleKinds = { 1, 2, 3, 4 };

        /// <summary>How a wand draws runes (CastPattern.STYLE_*; 0 for an ordinary wand or no wand).</summary>
        public int CastStyle(int itemId)
        {
            if (itemId == 0 || castStyleItems == null || castStyleKinds == null) return 0;
            for (int k = 0; k < castStyleItems.Length && k < castStyleKinds.Length; k++)
                if (castStyleItems[k] == itemId) return castStyleKinds[k];
            return 0;
        }

        /// <summary>"Casting style: ..." for a style wand's details (empty for an ordinary one).</summary>
        public string CastStyleText(int itemId)
        {
            switch (CastStyle(itemId))
            {
                case 1: return "Casting style: Blade. Slash each stroke of the rune, fast, in order.";
                case 2: return "Casting style: Sidearm. Shoot each target of the rune before it fades.";
                case 3: return "Casting style: Baton. Touch each point of the rune on the beat.";
                case 4: return "Casting style: Lantern. Keep the lantern in the wisp's glow as it runs the rune.";
            }
            return "";
        }

        /// <summary>This item has item cards of its own in itemCardItems.</summary>
        public bool HasItemCards(int itemId)
        {
            if (itemCardItems == null || itemId == 0) return false;
            for (int k = 0; k < itemCardItems.Length; k++) if (itemCardItems[k] == itemId) return true;
            return false;
        }

        /// <summary>
        /// The basic attack a wand adds wandBasicCopies of (0 for none): any wand without item cards of its own gives its
        /// school's basic spell, or for a wand of any school, the wielder's ('wielderSchool'; -1 when unknown gives 0).
        /// </summary>
        public int WandBasicSpell(int itemId, int wielderSchool)
        {
            if (wandBasicCopies <= 0 || itemId == 0 || GetCategory(itemId) != CharacterData.EQUIP_WEAPON || HasItemCards(itemId)) return 0;
            int school = GetSchoolReq(itemId);
            if (school < 0) school = wielderSchool;
            if (school < 0 || school > 6) return 0;
            return cardDatabase != null ? cardDatabase.BasicSpell(school) : 100 + school * CardDatabase.TIERS;
        }

        /// <summary>How many item cards all of the character's equipped gear adds to the battle deck.</summary>
        public int EquippedItemCardCount(CharacterData c)
        {
            if (c == null) return 0;
            int n = 0;
            for (int s = 0; s < CharacterData.EQUIP_SLOTS; s++)
            {
                int item = c.equipped[s];
                if (item == 0) continue;
                if (itemCardItems != null && itemCardIds != null)
                    for (int k = 0; k < itemCardItems.Length && k < itemCardIds.Length; k++) if (itemCardItems[k] == item) n++;
                if (WandBasicSpell(item, (int)c.school) > 0) n += wandBasicCopies;
            }
            return n;
        }

        /// <summary>
        /// Fills 'into' with every item spell the character's equipped gear adds to the battle deck, each copy on its own
        /// (the Deck page's item row); returns how many it put in (at most into.Length).
        /// </summary>
        public int EquippedItemCards(CharacterData c, int[] into)
        {
            if (c == null || into == null) return 0;
            int n = 0;
            for (int s = 0; s < CharacterData.EQUIP_SLOTS; s++)
            {
                int item = c.equipped[s];
                if (item == 0) continue;
                if (itemCardItems != null && itemCardIds != null)
                    for (int k = 0; k < itemCardItems.Length && k < itemCardIds.Length; k++)
                        if (itemCardItems[k] == item && n < into.Length) into[n++] = itemCardIds[k];
                int basic = WandBasicSpell(item, (int)c.school);
                if (basic > 0)
                    for (int k = 0; k < wandBasicCopies && n < into.Length; k++) into[n++] = basic;
            }
            return n;
        }

        /// <summary>"Item spells: Ember Lance, Frost Shard..." for an item's details (empty when it gives none).</summary>
        public string ItemCardText(int itemId) { return ItemCardTextFor(itemId, -1); }

        /// <summary>
        /// The same, for a wizard of school 'wielderSchool' (-1 when unknown): a wand's basic attacks are named for the school
        /// they'll be ("7 x Ember Lance"), or "your school's basic attack" when that depends on a wielder we don't know.
        /// </summary>
        public string ItemCardTextFor(int itemId, int wielderSchool)
        {
            string t = "";
            if (itemCardItems != null && itemCardIds != null)
                for (int k = 0; k < itemCardItems.Length && k < itemCardIds.Length; k++)
                {
                    if (itemCardItems[k] != itemId) continue;
                    string n = cardDatabase != null ? cardDatabase.GetName(itemCardIds[k]) : itemCardIds[k].ToString();
                    t += (t.Length == 0 ? "Item spells (added to your deck in battle): " : ", ") + n;
                }
            if (wandBasicCopies > 0 && GetCategory(itemId) == CharacterData.EQUIP_WEAPON && !HasItemCards(itemId))
            {
                int basic = WandBasicSpell(itemId, wielderSchool);
                string what = basic > 0 && cardDatabase != null ? cardDatabase.GetName(basic) : "your school's basic attack";
                t = "Item spells (added to your deck in battle): " + wandBasicCopies.ToString() + " x " + what;
            }
            string style = CastStyleText(itemId);
            if (style.Length > 0) t += (t.Length > 0 ? "\n" : "") + style;
            return t;
        }

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

        /// <summary>A quest reward or boss drop: not sold in shops and not rolled as random loot.</summary>
        public bool IsUnique(int itemId) { return SafeGet(uniqueItems, IndexOf(itemId)) != 0; }

        public bool IsPotion(int itemId) { return GetCategory(itemId) == CATEGORY_POTION; }
        public bool IsDeck(int itemId) { return GetCategory(itemId) == CATEGORY_DECK; }
        /// <summary>Spells the deck holds (falls back to CardDatabase.DECK_MAX).</summary>
        public int GetDeckCapacity(int itemId) { int v = SafeGet(deckCapacity, IndexOf(itemId)); return v > 0 ? v : CardDatabase.DECK_MAX; }
        public int GetDeckMaxCopies(int itemId) { int v = SafeGet(deckMaxCopies, IndexOf(itemId)); return v > 0 ? v : CardDatabase.MAX_COPIES; }
        /// <summary>Treasure cards the deck's sideboard holds (Wizard101): about a third of its spells, 6 to CharacterData.SIDE_SIZE.</summary>
        public int GetDeckTreasureCapacity(int itemId) { return Mathf.Clamp(GetDeckCapacity(itemId) * 3 / 10, 6, CharacterData.SIDE_SIZE); }
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
            if (IsDeck(itemId)) s += "Holds " + GetDeckCapacity(itemId).ToString() + " spells, up to " + GetDeckMaxCopies(itemId).ToString() + " copies of each\n" +
                                     "Sideboard: " + GetDeckTreasureCapacity(itemId).ToString() + " treasure cards\n";
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
