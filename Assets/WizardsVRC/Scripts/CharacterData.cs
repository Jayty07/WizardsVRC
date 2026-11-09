using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// One character's full state plus a compact binary codec.
    ///
    /// Blob layout (all integers little-endian):
    ///   [0..1]  magic 'W','Z'
    ///   [2]     format version
    ///   [3..4]  payload length (u16)
    ///   [5..]   payload
    ///   [last 4] Adler-32 of bytes [0 .. 5+payloadLength)
    ///
    /// Payload v1:
    ///   nameLen u8, name chars u16 * nameLen
    ///   school u8, level u8, xp u32, gold u32, maxHealth u16, maxMana u16
    ///   equipped u16 * EQUIP_SLOTS
    ///   invUsed u8, then invUsed * (itemId u16, count u8)
    ///   per deck: cardCount u8, then cardCount * cardId u16
    ///   questUsed u8, then questUsed * progress u8
    /// Payload v2 appends:
    ///   trainingPoints u16, learnedUsed u8, then learnedUsed * learned-spell bitmask byte (bit n = card id 100 + n)
/// Payload v3 appends:
///   trackedQuest u8 (NO_TRACKED_QUEST = nothing tracked)
/// v4: same layout; spell ids moved to the 12-tier trees (100 + school * 12 + tier), so older saves are migrated.
/// Payload v5 appends:
///   counterUsed u8, then counterUsed * questCounters byte (step progress per quest)
/// Payload v6 appends:
///   currentHealth u16, currentMana u16 (FULL = 65535 = topped up; clamped to the gear-adjusted max when used)
/// v7: equipped has 8 slots (slot 7 = deck), there are 6 deck card lists (was 3), and the payload appends
///   deckOwners u16 * DECK_COUNT (the deck item each card list belongs to, 0 = free)
/// v8: 512 quest slots (room for 10+ storylines per world, several worlds): questUsed, trackedQuest and counterUsed
///   are u16 (NO_TRACKED_QUEST = 65535). Older saves load as before.
/// v9: signature / aura colours. v10: starshards, titles, auras, skins, records, elixirs.
/// v11 appends: rewardLevel u8, BOUNTY_SLOTS * (bountyId u8, progress u8), bountiesDone u16, depotBits u16, highNoonWins u16.
/// v12 appends: discoveredA u32, discoveredB u32 (places found, AreaDiscovery).
    /// Typical saves are 1-2 KB raw; the worst case (every quest slot used) is about 2.6 KB raw (budget: 4 KB).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CharacterData : UdonSharpBehaviour
    {
        public const int FORMAT_VERSION = 12;
        public const int HEADER_SIZE = 5;
        public const int CHECKSUM_SIZE = 4;

        public const int MAX_NAME_LENGTH = 24;
        public const int EQUIP_SLOTS = 8;
        public const int INVENTORY_SLOTS = 100;
        /// <summary>Card lists in the save: each belongs to one deck item (see deckOwners), so up to 6 decks keep their cards.</summary>
        public const int DECK_COUNT = 6;
        /// <summary>Save layouts before v7 had 7 equip slots and 3 decks.</summary>
        private const int OLD_EQUIP_SLOTS = 7;
        private const int OLD_DECK_COUNT = 3;
        /// <summary>Item id of the Apprentice Deck every wizard starts with (see ItemDatabase).</summary>
        public const int STARTER_DECK_ITEM = 60;
        public const int DECK_SIZE = 64;
        /// <summary>Quest save ids 0-511 (0 and 1 are built in). A world needs room for 10+ storylines of several quests.</summary>
        public const int QUEST_SLOTS = 512;
        public const int MAX_LEVEL = 50;
        public const int LEARNED_BYTES = 32;
        public const int LEARNED_BASE_ID = 100;
        public const int HEALTH_PER_LEVEL = 15;
        public const int MANA_PER_LEVEL = 2;
        public const int STARTER_MANA = 20;
        /// <summary>currentHealth / currentMana value meaning "full" (always clamped to the real max).</summary>
        public const int FULL = 65535;
        public const int NO_TRACKED_QUEST = 65535;
        /// <summary>"Nothing tracked" in saves before v8 (one byte).</summary>
        private const int OLD_NO_TRACKED_QUEST = 255;
        /// <summary>Must match CardDatabase.TIERS / QUEST_TIER and the tier level requirements.</summary>
        public const int SPELL_TIERS = 12;
        public const int SPELL_QUEST_TIER = 9;

        public const int EQUIP_HEAD = 0;
        public const int EQUIP_BODY = 1;
        public const int EQUIP_FEET = 2;
        public const int EQUIP_WEAPON = 3;
        public const int EQUIP_OFFHAND = 4;
        public const int EQUIP_AMULET = 5;
        public const int EQUIP_RING = 6;
        /// <summary>The equipped deck item: battles draw from its card list.</summary>
        public const int EQUIP_DECK = 7;

        private const int MAX_BLOB_SIZE = 4096;

        [Header("Identity")]
        public string characterName = "";
        public School school = School.Fire;
        [Tooltip("Signature colour (index into the creator's palette; 0 = the school's colour). Tints your name, your duel seat, your casting ring and your HUD.")]
        public int signatureColor;
        [Tooltip("Pip aura colour (index into the same palette; 0 = the school's colour): the aura round you when you hold a lot of pips.")]
        public int auraColor;
        /// <summary>Palette size shared by both colours (see the character creator).</summary>
        public const int COLOR_CHOICES = 12;

        [Header("Progression")]
        public int level = 1;
        public int xp;
        public int gold;
        public int maxHealth = 100;
        public int maxMana = STARTER_MANA;
        [Tooltip("Health carried between fights (Wizard101 style). FULL = topped up. Use PlayerState.GetHealth().")]
        public int currentHealth = FULL;
        [Tooltip("Mana carried between fights; each pip a spell costs uses 1 mana. Use PlayerState.GetMana().")]
        public int currentMana = FULL;

        [Header("Items")]
        public ushort[] equipped = new ushort[EQUIP_SLOTS];
        public ushort[] inventoryItemIds = new ushort[INVENTORY_SLOTS];
        public byte[] inventoryCounts = new byte[INVENTORY_SLOTS];

        [Header("Decks (flattened: deck d, card c => d * DECK_SIZE + c)")]
        public ushort[] deckCards = new ushort[DECK_COUNT * DECK_SIZE];
        public byte[] deckLengths = new byte[DECK_COUNT];
        [Tooltip("Deck item id that owns each card list (0 = free).")]
        public ushort[] deckOwners = new ushort[DECK_COUNT];

        [Header("Quests (progress/stage per quest id)")]
        public byte[] questProgress = new byte[QUEST_SLOTS];
        [Tooltip("Quest id shown on the HUD and guided to, or NO_TRACKED_QUEST.")]
        public int trackedQuest;
        [Tooltip("Progress within the current step of each quest (e.g. 2 of 3 defeated).")]
        public byte[] questCounters = new byte[QUEST_SLOTS];

        [Header("Spells (bit n of learnedSpells = card id 100 + n)")]
        public int trainingPoints;
        public byte[] learnedSpells = new byte[LEARNED_BYTES];

        [Header("Extras (v10): premium currency, titles, auras, spell skins, records")]
        [Tooltip("Starshards: the rare currency for the Starshard Shop (earned from bosses, towers and quests).")]
        public int starshards;
        [Tooltip("Bit n = title n unlocked (see CosmeticsDatabase).")]
        public int titleBits;
        [Tooltip("Shown above your head: title index + 1 (0 = none).")]
        public int equippedTitle;
        public int auraBits;
        [Tooltip("Aura index + 1 (0 = none).")]
        public int equippedAura;
        public int skinBits;
        [Tooltip("Spell skin index + 1 (0 = the school's own look).")]
        public int equippedSkin;
        public int battlesWon;
        public int monstersDefeated;
        public int bossesDefeated;
        [Tooltip("Bit n = tower n cleared to the top.")]
        public int towersCleared;
        [Tooltip("Highest level whose 'new spells at your professor' notice has been shown.")]
        public int spellNoticeLevel;
        public const int ELIXIRS = 6;
        [Tooltip("Seconds of play left on each elixir (see CosmeticsDatabase).")]
        public int[] elixirSeconds = new int[ELIXIRS];

        [Header("Dustwind (v11): level rewards, bounties, the railroad, High Noon")]
        [Tooltip("Highest level whose level-up reward has been given (LevelRewards).")]
        public int rewardLevel = 1;
        public const int BOUNTY_SLOTS = 3;
        [Tooltip("The bounty board's three posted bounties (bounty index + 1, 0 = none) and progress on each.")]
        public int[] bountyIds = new int[BOUNTY_SLOTS];
        public int[] bountyProgress = new int[BOUNTY_SLOTS];
        [Tooltip("Bounties turned in (titles).")]
        public int bountiesDone;
        [Tooltip("Bit n = railroad depot n reached (you can ride to it from any other depot).")]
        public int depotBits;
        public int highNoonWins;
        /// <summary>Places discovered (AreaDiscovery): bits 0-30 of A are places 0-30, of B places 31-61.</summary>
        public int discoveredA, discoveredB;

        [HideInInspector] public string lastError = "";

        private byte[] _buf;
        private int _pos;
        private int _end;

        // ------------------------------------------------------------------ defaults / copy

        public void ResetToDefaults(School newSchool, string newName)
        {
            characterName = SanitizeName(newName);
            school = newSchool;
            signatureColor = 0;
            auraColor = 0;
            level = 1;
            xp = 0;
            gold = 50;
            maxHealth = GetStarterHealth(newSchool);
            maxMana = STARTER_MANA;
            currentHealth = FULL;
            currentMana = FULL;

            EnsureArrays();
            for (int i = 0; i < EQUIP_SLOTS; i++) equipped[i] = 0;
            for (int i = 0; i < INVENTORY_SLOTS; i++) { inventoryItemIds[i] = 0; inventoryCounts[i] = 0; }
            for (int i = 0; i < deckCards.Length; i++) deckCards[i] = 0;
            for (int i = 0; i < DECK_COUNT; i++) { deckLengths[i] = 0; deckOwners[i] = 0; }
            for (int i = 0; i < QUEST_SLOTS; i++) { questProgress[i] = 0; questCounters[i] = 0; }
            for (int i = 0; i < LEARNED_BYTES; i++) learnedSpells[i] = 0;
            trainingPoints = 0;
            trackedQuest = 0;
            ClearExtras();

            // Starter kit (see ItemDatabase): staff + robe equipped; potions, hat and boots in the backpack.
            equipped[EQUIP_WEAPON] = 1;
            equipped[EQUIP_BODY] = 2;
            inventoryItemIds[0] = 10;
            inventoryCounts[0] = 3;
            inventoryItemIds[1] = 3;
            inventoryCounts[1] = 1;
            inventoryItemIds[2] = 4;
            inventoryCounts[2] = 1;

            // Starter deck: four copies of the school's basic spell (tier 0, card id = 100 + school * 12).
            ushort basicSpell = (ushort)(100 + (int)newSchool * SPELL_TIERS);
            for (int c = 0; c < 4; c++) deckCards[c] = basicSpell;
            deckLengths[0] = 4;
            equipped[EQUIP_DECK] = STARTER_DECK_ITEM;
            deckOwners[0] = STARTER_DECK_ITEM;
            Learn(basicSpell);

            lastError = "";
        }

        private void ClearExtras()
        {
            starshards = 0; titleBits = 0; equippedTitle = 0; auraBits = 0; equippedAura = 0; skinBits = 0; equippedSkin = 0;
            battlesWon = 0; monstersDefeated = 0; bossesDefeated = 0; towersCleared = 0; spellNoticeLevel = 1;
            rewardLevel = 1; bountiesDone = 0; depotBits = 0; highNoonWins = 0; discoveredA = 0; discoveredB = 0;
            if (bountyIds == null || bountyIds.Length != BOUNTY_SLOTS) bountyIds = new int[BOUNTY_SLOTS];
            if (bountyProgress == null || bountyProgress.Length != BOUNTY_SLOTS) bountyProgress = new int[BOUNTY_SLOTS];
            for (int i = 0; i < BOUNTY_SLOTS; i++) { bountyIds[i] = 0; bountyProgress[i] = 0; }
            if (elixirSeconds == null || elixirSeconds.Length != ELIXIRS) elixirSeconds = new int[ELIXIRS];
            for (int i = 0; i < ELIXIRS; i++) elixirSeconds[i] = 0;
        }

        public bool HasBit(int bits, int n) { return n >= 0 && n < 31 && (bits & (1 << n)) != 0; }

        /// <summary>Place n (0-61) has been discovered.</summary>
        public bool IsDiscovered(int n) { return n < 31 ? HasBit(discoveredA, n) : HasBit(discoveredB, n - 31); }

        public void SetDiscovered(int n)
        {
            if (n < 0 || n > 61) return;
            if (n < 31) discoveredA |= 1 << n;
            else discoveredB |= 1 << (n - 31);
        }

        public int GetStarterHealth(School s)
        {
            switch (s)
            {
                case School.Fire: return 415;
                case School.Ice: return 500;
                case School.Storm: return 400;
                case School.Life: return 460;
                case School.Death: return 450;
                case School.Myth: return 425;
                default: return 480;
            }
        }

        public void CopyFrom(CharacterData other)
        {
            if (other == null) return;
            EnsureArrays();
            other.EnsureArrays();
            characterName = other.characterName;
            school = other.school;
            signatureColor = other.signatureColor;
            auraColor = other.auraColor;
            level = other.level;
            xp = other.xp;
            gold = other.gold;
            maxHealth = other.maxHealth;
            maxMana = other.maxMana;
            currentHealth = other.currentHealth;
            currentMana = other.currentMana;
            System.Array.Copy(other.equipped, equipped, EQUIP_SLOTS);
            System.Array.Copy(other.inventoryItemIds, inventoryItemIds, INVENTORY_SLOTS);
            System.Array.Copy(other.inventoryCounts, inventoryCounts, INVENTORY_SLOTS);
            System.Array.Copy(other.deckCards, deckCards, DECK_COUNT * DECK_SIZE);
            System.Array.Copy(other.deckLengths, deckLengths, DECK_COUNT);
            System.Array.Copy(other.deckOwners, deckOwners, DECK_COUNT);
            System.Array.Copy(other.questProgress, questProgress, QUEST_SLOTS);
            System.Array.Copy(other.questCounters, questCounters, QUEST_SLOTS);
            System.Array.Copy(other.learnedSpells, learnedSpells, LEARNED_BYTES);
            trainingPoints = other.trainingPoints;
            trackedQuest = other.trackedQuest;
            starshards = other.starshards; titleBits = other.titleBits; equippedTitle = other.equippedTitle;
            auraBits = other.auraBits; equippedAura = other.equippedAura; skinBits = other.skinBits; equippedSkin = other.equippedSkin;
            battlesWon = other.battlesWon; monstersDefeated = other.monstersDefeated; bossesDefeated = other.bossesDefeated;
            towersCleared = other.towersCleared; spellNoticeLevel = other.spellNoticeLevel;
            rewardLevel = other.rewardLevel; bountiesDone = other.bountiesDone; depotBits = other.depotBits; highNoonWins = other.highNoonWins; discoveredA = other.discoveredA; discoveredB = other.discoveredB;
            if (bountyIds == null || bountyIds.Length != BOUNTY_SLOTS) bountyIds = new int[BOUNTY_SLOTS];
            if (bountyProgress == null || bountyProgress.Length != BOUNTY_SLOTS) bountyProgress = new int[BOUNTY_SLOTS];
            for (int i = 0; i < BOUNTY_SLOTS; i++)
            {
                bountyIds[i] = other.bountyIds != null && i < other.bountyIds.Length ? other.bountyIds[i] : 0;
                bountyProgress[i] = other.bountyProgress != null && i < other.bountyProgress.Length ? other.bountyProgress[i] : 0;
            }
            if (elixirSeconds == null || elixirSeconds.Length != ELIXIRS) elixirSeconds = new int[ELIXIRS];
            if (other.elixirSeconds != null)
                for (int i = 0; i < ELIXIRS && i < other.elixirSeconds.Length; i++) elixirSeconds[i] = other.elixirSeconds[i];
        }

        public void EnsureArrays()
        {
            if (equipped == null || equipped.Length != EQUIP_SLOTS) equipped = new ushort[EQUIP_SLOTS];
            if (inventoryItemIds == null || inventoryItemIds.Length != INVENTORY_SLOTS) inventoryItemIds = new ushort[INVENTORY_SLOTS];
            if (inventoryCounts == null || inventoryCounts.Length != INVENTORY_SLOTS) inventoryCounts = new byte[INVENTORY_SLOTS];
            if (deckCards == null || deckCards.Length != DECK_COUNT * DECK_SIZE) deckCards = new ushort[DECK_COUNT * DECK_SIZE];
            if (deckLengths == null || deckLengths.Length != DECK_COUNT) deckLengths = new byte[DECK_COUNT];
            if (deckOwners == null || deckOwners.Length != DECK_COUNT) deckOwners = new ushort[DECK_COUNT];
            if (questProgress == null || questProgress.Length != QUEST_SLOTS) questProgress = new byte[QUEST_SLOTS];
            if (questCounters == null || questCounters.Length != QUEST_SLOTS) questCounters = new byte[QUEST_SLOTS];
            if (learnedSpells == null || learnedSpells.Length != LEARNED_BYTES) learnedSpells = new byte[LEARNED_BYTES];
        }

        public string SanitizeName(string raw)
        {
            if (raw == null) return "";
            string trimmed = raw.Trim();
            int len = Mathf.Min(trimmed.Length, MAX_NAME_LENGTH);
            char[] chars = new char[len];
            for (int i = 0; i < len; i++)
            {
                char c = trimmed[i];
                // '|' and ',' are separators in the "meta" key.
                chars[i] = (c == '|' || c == ',' || c < ' ') ? '_' : c;
            }
            return new string(chars);
        }

        // ------------------------------------------------------------------ inventory helpers

        /// <summary>Inventory index holding itemId, or -1.</summary>
        public int FindItem(int itemId)
        {
            EnsureArrays();
            if (itemId <= 0) return -1;
            for (int i = 0; i < INVENTORY_SLOTS; i++)
                if (inventoryItemIds[i] == itemId && inventoryCounts[i] > 0) return i;
            return -1;
        }

        /// <summary>Number of inventory entries in use (entries are kept packed at the front).</summary>
        public int UsedInventorySlots()
        {
            EnsureArrays();
            int used = 0;
            for (int i = 0; i < INVENTORY_SLOTS; i++)
                if (inventoryItemIds[i] != 0 && inventoryCounts[i] > 0) used = i + 1;
            return used;
        }

        /// <summary>Adds to an existing stack (max 255) or the first free entry. False if full.</summary>
        public bool AddItem(int itemId, int count)
        {
            EnsureArrays();
            if (itemId <= 0 || itemId > 65535 || count <= 0) return false;
            int existing = FindItem(itemId);
            if (existing >= 0 && inventoryCounts[existing] + count <= 255)
            {
                inventoryCounts[existing] = (byte)(inventoryCounts[existing] + count);
                return true;
            }
            for (int i = 0; i < INVENTORY_SLOTS; i++)
            {
                if (inventoryItemIds[i] == 0 || inventoryCounts[i] == 0)
                {
                    inventoryItemIds[i] = (ushort)itemId;
                    inventoryCounts[i] = (byte)Mathf.Min(count, 255);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Removes count from the entry at index, packing the list when an entry empties.</summary>
        public bool RemoveItemAt(int index, int count)
        {
            EnsureArrays();
            if (index < 0 || index >= INVENTORY_SLOTS || inventoryCounts[index] < count || count <= 0) return false;
            int left = inventoryCounts[index] - count;
            if (left > 0)
            {
                inventoryCounts[index] = (byte)left;
                return true;
            }
            for (int i = index; i < INVENTORY_SLOTS - 1; i++)
            {
                inventoryItemIds[i] = inventoryItemIds[i + 1];
                inventoryCounts[i] = inventoryCounts[i + 1];
            }
            inventoryItemIds[INVENTORY_SLOTS - 1] = 0;
            inventoryCounts[INVENTORY_SLOTS - 1] = 0;
            return true;
        }

        // ------------------------------------------------------------------ progression / spell helpers

        /// <summary>XP needed to go from the current level to the next (xp counts up from 0 within each level).</summary>
        /// <summary>
        /// XP from this level to the next: 100 x L + 10 x L^2, so each level takes noticeably more than the last (level 2
        /// needs 110, level 13 needs 2,640). World 1 ends around level 13 (see the World 1 blueprint).
        /// </summary>
        public int XpToNextLevel()
        {
            return level * 100 + level * level * 10;
        }

        /// <summary>
        /// Whether reaching this level gives a training point: every 4th level up to 19 (4, 8, 12, 16), then every 5th
        /// from 20 (20, 25, 30...).
        /// </summary>
        public bool GivesTrainingPoint(int lv)
        {
            return lv < 20 ? lv % 4 == 0 : lv % 5 == 0;
        }

        /// <summary>Training points a character has earned by this level.</summary>
        public int TrainingPointsAtLevel(int lv)
        {
            int n = 0;
            for (int l = 2; l <= lv; l++) if (GivesTrainingPoint(l)) n++;
            return n;
        }

        /// <summary>Training points from the last AddXp call's level-ups.</summary>
        [System.NonSerialized] public int lastPointsGained;

        /// <summary>Adds XP and applies level-ups (+health, +mana, training points at GivesTrainingPoint levels). Returns the number of levels gained.</summary>
        public int AddXp(int amount)
        {
            xp += amount;
            int gained = 0;
            lastPointsGained = 0;
            while (level < MAX_LEVEL && xp >= XpToNextLevel())
            {
                xp -= XpToNextLevel();
                level++;
                maxHealth += HEALTH_PER_LEVEL;
                maxMana += MANA_PER_LEVEL;
                if (GivesTrainingPoint(level)) { trainingPoints++; lastPointsGained++; }
                // Levelling up refills health and mana, as in Wizard101.
                currentHealth = FULL;
                currentMana = FULL;
                gained++;
            }
            return gained;
        }

        public bool HasLearned(int cardId)
        {
            EnsureArrays();
            int bit = cardId - LEARNED_BASE_ID;
            if (bit < 0 || bit >= LEARNED_BYTES * 8) return false;
            return (learnedSpells[bit >> 3] & (1 << (bit & 7))) != 0;
        }

        /// <summary>Marks the spell learned. False if it was already learned or the id is out of range.</summary>
        public bool Learn(int cardId)
        {
            if (HasLearned(cardId)) return false;
            int bit = cardId - LEARNED_BASE_ID;
            if (bit < 0 || bit >= LEARNED_BYTES * 8) return false;
            learnedSpells[bit >> 3] = (byte)(learnedSpells[bit >> 3] | (1 << (bit & 7)));
            return true;
        }

        /// <summary>
        /// Saves before v4 used the old spell ids: grant the own-school professor spells up to the character's level (not the
        /// school-quest spells), refund every training point and rebuild Deck 1 from the basic spell.
        /// </summary>
        private void MigrateSpellTree()
        {
            for (int i = 0; i < LEARNED_BYTES; i++) learnedSpells[i] = 0;
            // The professor tiers' levels (CardDatabase.levelReqs); tiers 3, 7, 9 and 11 are school-quest spells, not granted.
            int[] tierLevels = new int[] { 1, 5, 26, 0, 16, 10, 22, 0, 42, 0, 33, 0 };
            for (int k = 0; k < SPELL_TIERS; k++)
                if (k != SPELL_QUEST_TIER && tierLevels[k] > 0 && level >= tierLevels[k]) Learn(LEARNED_BASE_ID + (int)school * SPELL_TIERS + k);
            trainingPoints = TrainingPointsAtLevel(level);
            for (int i = 0; i < deckCards.Length; i++) deckCards[i] = 0;
            for (int d = 0; d < DECK_COUNT; d++) deckLengths[d] = 0;
            ushort basicSpell = (ushort)(LEARNED_BASE_ID + (int)school * SPELL_TIERS);
            for (int c = 0; c < 4; c++) deckCards[c] = basicSpell;
            deckLengths[0] = 4;
        }

        // ------------------------------------------------------------------ deck items

        /// <summary>Before v7 decks weren't items: Deck 1 becomes the contents of an equipped Apprentice Deck.</summary>
        private void MigrateDecks(int version)
        {
            if (version >= 7) return;
            equipped[EQUIP_DECK] = STARTER_DECK_ITEM;
            deckOwners[0] = STARTER_DECK_ITEM;
            for (int d = 1; d < DECK_COUNT; d++) { deckOwners[d] = 0; deckLengths[d] = 0; }
        }

        /// <summary>True if the character has this item equipped or in the backpack.</summary>
        public bool OwnsItem(int itemId)
        {
            EnsureArrays();
            if (itemId <= 0) return false;
            for (int s = 0; s < EQUIP_SLOTS; s++) if (equipped[s] == itemId) return true;
            return FindItem(itemId) >= 0;
        }

        /// <summary>
        /// The card list belonging to a deck item, or -1. With claim, a deck without one gets a free list (or the list of
        /// a deck the character no longer owns), starting empty; -1 if all DECK_COUNT lists are taken.
        /// </summary>
        public int DeckListFor(int deckItemId, bool claim)
        {
            EnsureArrays();
            if (deckItemId <= 0) return -1;
            for (int d = 0; d < DECK_COUNT; d++) if (deckOwners[d] == deckItemId) return d;
            if (!claim) return -1;
            int free = -1;
            for (int d = 0; d < DECK_COUNT && free < 0; d++) if (deckOwners[d] == 0) free = d;
            for (int d = 0; d < DECK_COUNT && free < 0; d++) if (!OwnsItem(deckOwners[d])) free = d;
            if (free < 0) return -1;
            deckOwners[free] = (ushort)deckItemId;
            deckLengths[free] = 0;
            for (int c = 0; c < DECK_SIZE; c++) deckCards[free * DECK_SIZE + c] = 0;
            return free;
        }

        /// <summary>True when a deck without a card list could get one (a list is free or its deck is gone).</summary>
        public bool CanClaimDeckList()
        {
            EnsureArrays();
            for (int d = 0; d < DECK_COUNT; d++) if (deckOwners[d] == 0 || !OwnsItem(deckOwners[d])) return true;
            return false;
        }

        /// <summary>The equipped deck's card list (-1 if no deck is equipped).</summary>
        public int ActiveDeckList()
        {
            EnsureArrays();
            return DeckListFor(equipped[EQUIP_DECK], true);
        }

        // ------------------------------------------------------------------ deck helpers

        public int DeckCount(int deck)
        {
            EnsureArrays();
            if (deck < 0 || deck >= DECK_COUNT) return 0;
            return Mathf.Min(deckLengths[deck], DECK_SIZE);
        }

        public int CountInDeck(int deck, int cardId)
        {
            int len = DeckCount(deck);
            int n = 0;
            for (int i = 0; i < len; i++)
                if (deckCards[deck * DECK_SIZE + i] == cardId) n++;
            return n;
        }

        public bool AddToDeck(int deck, int cardId)
        {
            int len = DeckCount(deck);
            if (deck < 0 || deck >= DECK_COUNT || len >= DECK_SIZE || cardId <= 0 || cardId > 65535) return false;
            deckCards[deck * DECK_SIZE + len] = (ushort)cardId;
            deckLengths[deck] = (byte)(len + 1);
            return true;
        }

        /// <summary>Removes the last copy of cardId from the deck, keeping the list packed.</summary>
        public bool RemoveFromDeck(int deck, int cardId)
        {
            int len = DeckCount(deck);
            int baseIndex = deck * DECK_SIZE;
            for (int i = len - 1; i >= 0; i--)
            {
                if (deckCards[baseIndex + i] != cardId) continue;
                for (int j = i; j < len - 1; j++) deckCards[baseIndex + j] = deckCards[baseIndex + j + 1];
                deckCards[baseIndex + len - 1] = 0;
                deckLengths[deck] = (byte)(len - 1);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ serialize

        public byte[] Serialize()
        {
            EnsureArrays();
            _buf = new byte[MAX_BLOB_SIZE];
            _pos = HEADER_SIZE;

            string n = SanitizeName(characterName);
            WriteU8(n.Length);
            for (int i = 0; i < n.Length; i++) WriteU16(n[i]);

            WriteU8((int)school);
            WriteU8(Mathf.Clamp(level, 1, MAX_LEVEL));
            WriteU32(xp);
            WriteU32(gold);
            WriteU16(maxHealth);
            WriteU16(maxMana);

            for (int i = 0; i < EQUIP_SLOTS; i++) WriteU16(equipped[i]);

            int invUsed = 0;
            for (int i = 0; i < INVENTORY_SLOTS; i++)
                if (inventoryItemIds[i] != 0 && inventoryCounts[i] != 0) invUsed = i + 1;
            WriteU8(invUsed);
            for (int i = 0; i < invUsed; i++)
            {
                WriteU16(inventoryItemIds[i]);
                WriteU8(inventoryCounts[i]);
            }

            for (int d = 0; d < DECK_COUNT; d++)
            {
                int count = Mathf.Min(deckLengths[d], DECK_SIZE);
                WriteU8(count);
                for (int c = 0; c < count; c++) WriteU16(deckCards[d * DECK_SIZE + c]);
            }

            int questUsed = 0;
            for (int i = 0; i < QUEST_SLOTS; i++)
                if (questProgress[i] != 0) questUsed = i + 1;
            WriteU16(questUsed);
            for (int i = 0; i < questUsed; i++) WriteU8(questProgress[i]);

            WriteU16(Mathf.Clamp(trainingPoints, 0, 65535));
            int learnedUsed = 0;
            for (int i = 0; i < LEARNED_BYTES; i++)
                if (learnedSpells[i] != 0) learnedUsed = i + 1;
            WriteU8(learnedUsed);
            for (int i = 0; i < learnedUsed; i++) WriteU8(learnedSpells[i]);

            WriteU16(trackedQuest >= 0 && trackedQuest < QUEST_SLOTS ? trackedQuest : NO_TRACKED_QUEST);
            int counterUsed = 0;
            for (int i = 0; i < QUEST_SLOTS; i++) if (questCounters[i] != 0) counterUsed = i + 1;
            WriteU16(counterUsed);
            for (int i = 0; i < counterUsed; i++) WriteU8(questCounters[i]);

            WriteU16(Mathf.Clamp(currentHealth, 0, FULL));
            WriteU16(Mathf.Clamp(currentMana, 0, FULL));
            for (int d = 0; d < DECK_COUNT; d++) WriteU16(deckOwners[d]);
            // v9: the creator's colour choices
            WriteU8(Mathf.Clamp(signatureColor, 0, COLOR_CHOICES - 1));
            WriteU8(Mathf.Clamp(auraColor, 0, COLOR_CHOICES - 1));
            // v10: starshards, titles, auras, skins, records
            WriteU32(starshards);
            WriteU32(titleBits);
            WriteU8(Mathf.Clamp(equippedTitle, 0, 255));
            WriteU32(auraBits);
            WriteU8(Mathf.Clamp(equippedAura, 0, 255));
            WriteU32(skinBits);
            WriteU8(Mathf.Clamp(equippedSkin, 0, 255));
            WriteU16(Mathf.Clamp(battlesWon, 0, 65535));
            WriteU32(monstersDefeated);
            WriteU16(Mathf.Clamp(bossesDefeated, 0, 65535));
            WriteU32(towersCleared);
            WriteU8(Mathf.Clamp(spellNoticeLevel, 0, 255));
            for (int i = 0; i < ELIXIRS; i++) WriteU16(Mathf.Clamp(elixirSeconds != null && i < elixirSeconds.Length ? elixirSeconds[i] : 0, 0, 65535));
            // v11: level rewards, bounties, depots, High Noon
            WriteU8(Mathf.Clamp(rewardLevel, 1, MAX_LEVEL));
            for (int i = 0; i < BOUNTY_SLOTS; i++)
            {
                WriteU8(Mathf.Clamp(bountyIds != null && i < bountyIds.Length ? bountyIds[i] : 0, 0, 255));
                WriteU8(Mathf.Clamp(bountyProgress != null && i < bountyProgress.Length ? bountyProgress[i] : 0, 0, 255));
            }
            WriteU16(Mathf.Clamp(bountiesDone, 0, 65535));
            WriteU16(depotBits & 0xFFFF);
            WriteU16(Mathf.Clamp(highNoonWins, 0, 65535));
            // v12: places discovered
            WriteU32(discoveredA & 0x7FFFFFFF);
            WriteU32(discoveredB & 0x7FFFFFFF);

            int payloadLength = _pos - HEADER_SIZE;
            _buf[0] = (byte)'W';
            _buf[1] = (byte)'Z';
            _buf[2] = (byte)FORMAT_VERSION;
            _buf[3] = (byte)(payloadLength & 0xFF);
            _buf[4] = (byte)((payloadLength >> 8) & 0xFF);

            int checksum = Adler32(_buf, _pos);
            WriteU32(checksum);

            byte[] result = new byte[_pos];
            System.Array.Copy(_buf, result, _pos);
            _buf = null;
            return result;
        }

        public string ToBase64()
        {
            return System.Convert.ToBase64String(Serialize());
        }

        // ------------------------------------------------------------------ deserialize

        /// <summary>Decodes a base64 blob into this object. On failure returns false, sets lastError and leaves fields untouched-or-partial; decode into a scratch instance and CopyFrom on success.</summary>
        public bool FromBase64(string encoded)
        {
            if (!IsValidBase64(encoded))
            {
                lastError = "not valid base64";
                return false;
            }
            return Deserialize(System.Convert.FromBase64String(encoded));
        }

        public bool Deserialize(byte[] data)
        {
            lastError = "";
            if (data == null || data.Length < HEADER_SIZE + CHECKSUM_SIZE) { lastError = "blob too short"; return false; }
            if (data[0] != (byte)'W' || data[1] != (byte)'Z') { lastError = "bad magic"; return false; }

            int version = data[2];
            if (version < 1 || version > FORMAT_VERSION) { lastError = "unsupported version " + version; return false; }

            int payloadLength = data[3] | (data[4] << 8);
            int dataEnd = HEADER_SIZE + payloadLength;
            if (dataEnd + CHECKSUM_SIZE != data.Length) { lastError = "length mismatch"; return false; }

            int stored = data[dataEnd] | (data[dataEnd + 1] << 8) | (data[dataEnd + 2] << 16) | (data[dataEnd + 3] << 24);
            if (stored != Adler32(data, dataEnd)) { lastError = "checksum mismatch"; return false; }

            _buf = data;
            _pos = HEADER_SIZE;
            _end = dataEnd;
            bool ok = ReadPayload(version);
            _buf = null;
            return ok;
        }

        private bool ReadPayload(int version)
        {
            EnsureArrays();
            signatureColor = 0;
            auraColor = 0;
            ClearExtras();

            if (!Need(1)) return false;
            int nameLen = ReadU8();
            if (nameLen > MAX_NAME_LENGTH || !Need(nameLen * 2)) { lastError = "bad name"; return false; }
            char[] chars = new char[nameLen];
            for (int i = 0; i < nameLen; i++) chars[i] = (char)ReadU16();
            string newName = new string(chars);

            int equipSlots = version >= 7 ? EQUIP_SLOTS : OLD_EQUIP_SLOTS;
            int deckLists = version >= 7 ? DECK_COUNT : OLD_DECK_COUNT;
            if (!Need(1 + 1 + 4 + 4 + 2 + 2 + equipSlots * 2)) return false;
            int newSchool = ReadU8();
            if (newSchool > (int)School.Balance) { lastError = "bad school"; return false; }
            int newLevel = ReadU8();
            int newXp = ReadU32();
            int newGold = ReadU32();
            int newHealth = ReadU16();
            int newMana = ReadU16();

            characterName = newName;
            school = (School)newSchool;
            level = Mathf.Clamp(newLevel, 1, MAX_LEVEL);
            xp = newXp;
            gold = newGold;
            maxHealth = newHealth;
            maxMana = newMana;
            currentHealth = FULL;
            currentMana = FULL;
            // v6 made mana a real resource (1 per pip): older characters get the new mana curve.
            if (version < 6) maxMana = Mathf.Max(maxMana, STARTER_MANA + MANA_PER_LEVEL * (level - 1));

            for (int i = 0; i < EQUIP_SLOTS; i++) equipped[i] = i < equipSlots ? (ushort)ReadU16() : (ushort)0;
            for (int d = 0; d < DECK_COUNT; d++) deckOwners[d] = 0;

            if (!Need(1)) return false;
            int invUsed = ReadU8();
            if (invUsed > INVENTORY_SLOTS || !Need(invUsed * 3)) { lastError = "bad inventory"; return false; }
            for (int i = 0; i < INVENTORY_SLOTS; i++)
            {
                if (i < invUsed)
                {
                    inventoryItemIds[i] = (ushort)ReadU16();
                    inventoryCounts[i] = (byte)ReadU8();
                }
                else
                {
                    inventoryItemIds[i] = 0;
                    inventoryCounts[i] = 0;
                }
            }

            for (int d = 0; d < DECK_COUNT; d++)
            {
                if (d >= deckLists)
                {
                    deckLengths[d] = 0;
                    for (int c = 0; c < DECK_SIZE; c++) deckCards[d * DECK_SIZE + c] = 0;
                    continue;
                }
                if (!Need(1)) return false;
                int count = ReadU8();
                if (count > DECK_SIZE || !Need(count * 2)) { lastError = "bad deck"; return false; }
                deckLengths[d] = (byte)count;
                for (int c = 0; c < DECK_SIZE; c++)
                    deckCards[d * DECK_SIZE + c] = c < count ? (ushort)ReadU16() : (ushort)0;
            }

            bool wide = version >= 8;
            if (!Need(wide ? 2 : 1)) return false;
            int questUsed = wide ? ReadU16() : ReadU8();
            if (questUsed > QUEST_SLOTS || !Need(questUsed)) { lastError = "bad quests"; return false; }
            for (int i = 0; i < QUEST_SLOTS; i++)
                questProgress[i] = i < questUsed ? (byte)ReadU8() : (byte)0;

            trackedQuest = 0;
            if (version < 2)
            {
                MigrateSpellTree();
                MigrateDecks(version);
                return true;
            }
            if (!Need(3)) return false;
            trainingPoints = ReadU16();
            int learnedUsed = ReadU8();
            if (learnedUsed > LEARNED_BYTES || !Need(learnedUsed)) { lastError = "bad spells"; return false; }
            for (int i = 0; i < LEARNED_BYTES; i++)
                learnedSpells[i] = i < learnedUsed ? (byte)ReadU8() : (byte)0;

            if (version >= 3)
            {
                if (!Need(wide ? 2 : 1)) return false;
                trackedQuest = wide ? ReadU16() : ReadU8();
                if ((!wide && trackedQuest == OLD_NO_TRACKED_QUEST) || trackedQuest >= QUEST_SLOTS) trackedQuest = NO_TRACKED_QUEST;
            }
            if (version < 4) MigrateSpellTree();
            for (int i = 0; i < QUEST_SLOTS; i++) questCounters[i] = 0;
            if (version >= 5)
            {
                if (!Need(wide ? 2 : 1)) return false;
                int counterUsed = wide ? ReadU16() : ReadU8();
                if (counterUsed > QUEST_SLOTS || !Need(counterUsed)) { lastError = "bad counters"; return false; }
                for (int i = 0; i < counterUsed; i++) questCounters[i] = (byte)ReadU8();
            }
            if (version >= 6)
            {
                if (!Need(4)) return false;
                currentHealth = ReadU16();
                currentMana = ReadU16();
            }
            if (version >= 7)
            {
                if (!Need(DECK_COUNT * 2)) return false;
                for (int d = 0; d < DECK_COUNT; d++) deckOwners[d] = (ushort)ReadU16();
            }
            if (version >= 9)
            {
                if (!Need(2)) return false;
                signatureColor = Mathf.Clamp(ReadU8(), 0, COLOR_CHOICES - 1);
                auraColor = Mathf.Clamp(ReadU8(), 0, COLOR_CHOICES - 1);
            }
            if (version >= 10)
            {
                if (!Need(4 + 4 + 1 + 4 + 1 + 4 + 1 + 2 + 4 + 2 + 4 + 1)) return false;
                starshards = ReadU32();
                titleBits = ReadU32();
                equippedTitle = ReadU8();
                auraBits = ReadU32();
                equippedAura = ReadU8();
                skinBits = ReadU32();
                equippedSkin = ReadU8();
                battlesWon = ReadU16();
                monstersDefeated = ReadU32();
                bossesDefeated = ReadU16();
                towersCleared = ReadU32();
                spellNoticeLevel = ReadU8();
                if (!Need(ELIXIRS * 2)) return false;
                for (int i = 0; i < ELIXIRS; i++) elixirSeconds[i] = ReadU16();
            }
            else spellNoticeLevel = level;   // older saves: don't announce spells they've already had the chance to learn
            if (version >= 11)
            {
                if (!Need(1 + BOUNTY_SLOTS * 2 + 2 + 2 + 2)) return false;
                rewardLevel = Mathf.Clamp(ReadU8(), 1, MAX_LEVEL);
                for (int i = 0; i < BOUNTY_SLOTS; i++)
                {
                    bountyIds[i] = ReadU8();
                    bountyProgress[i] = ReadU8();
                }
                bountiesDone = ReadU16();
                depotBits = ReadU16();
                highNoonWins = ReadU16();
            }
            else rewardLevel = level;   // older saves: the levels they already have don't pay out again
            if (version >= 12)
            {
                if (!Need(8)) return false;
                discoveredA = ReadU32() & 0x7FFFFFFF;
                discoveredB = ReadU32() & 0x7FFFFFFF;
            }
            MigrateDecks(version);
            return true;
        }

        // ------------------------------------------------------------------ helpers

        public int Adler32(byte[] data, int length)
        {
            int a = 1;
            int b = 0;
            for (int i = 0; i < length; i++)
            {
                a = (a + data[i]) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        /// <summary>Convert.FromBase64String throws on bad input, which would halt the Udon behaviour, so validate first.</summary>
        public bool IsValidBase64(string s)
        {
            if (s == null) return false;
            int len = s.Length;
            if (len == 0 || len % 4 != 0) return false;
            for (int i = 0; i < len; i++)
            {
                char c = s[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/';
                if (!ok)
                {
                    if (c != '=' || i < len - 2) return false;
                    if (i == len - 2 && s[len - 1] != '=') return false;
                }
            }
            return true;
        }

        private bool Need(int count)
        {
            if (_pos + count <= _end) return true;
            lastError = "truncated payload";
            return false;
        }

        private void WriteU8(int v)
        {
            _buf[_pos++] = (byte)(v & 0xFF);
        }

        private void WriteU16(int v)
        {
            _buf[_pos++] = (byte)(v & 0xFF);
            _buf[_pos++] = (byte)((v >> 8) & 0xFF);
        }

        private void WriteU32(int v)
        {
            _buf[_pos++] = (byte)(v & 0xFF);
            _buf[_pos++] = (byte)((v >> 8) & 0xFF);
            _buf[_pos++] = (byte)((v >> 16) & 0xFF);
            _buf[_pos++] = (byte)((v >> 24) & 0xFF);
        }

        private int ReadU8()
        {
            return _buf[_pos++];
        }

        private int ReadU16()
        {
            int v = _buf[_pos] | (_buf[_pos + 1] << 8);
            _pos += 2;
            return v;
        }

        private int ReadU32()
        {
            int v = _buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24);
            _pos += 4;
            return v;
        }
    }
}
