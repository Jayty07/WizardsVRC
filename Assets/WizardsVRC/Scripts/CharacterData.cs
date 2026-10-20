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
    /// Worst case is about 0.9 KB raw / 1.2 KB base64 (budget: 2 KB).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CharacterData : UdonSharpBehaviour
    {
        public const int FORMAT_VERSION = 1;
        public const int HEADER_SIZE = 5;
        public const int CHECKSUM_SIZE = 4;

        public const int MAX_NAME_LENGTH = 24;
        public const int EQUIP_SLOTS = 7;
        public const int INVENTORY_SLOTS = 100;
        public const int DECK_COUNT = 3;
        public const int DECK_SIZE = 64;
        public const int QUEST_SLOTS = 128;
        public const int MAX_LEVEL = 100;

        public const int EQUIP_HEAD = 0;
        public const int EQUIP_BODY = 1;
        public const int EQUIP_FEET = 2;
        public const int EQUIP_WEAPON = 3;
        public const int EQUIP_OFFHAND = 4;
        public const int EQUIP_AMULET = 5;
        public const int EQUIP_RING = 6;

        private const int MAX_BLOB_SIZE = 2048;

        [Header("Identity")]
        public string characterName = "";
        public School school = School.Fire;

        [Header("Progression")]
        public int level = 1;
        public int xp;
        public int gold;
        public int maxHealth = 100;
        public int maxMana = 10;

        [Header("Items")]
        public ushort[] equipped = new ushort[EQUIP_SLOTS];
        public ushort[] inventoryItemIds = new ushort[INVENTORY_SLOTS];
        public byte[] inventoryCounts = new byte[INVENTORY_SLOTS];

        [Header("Decks (flattened: deck d, card c => d * DECK_SIZE + c)")]
        public ushort[] deckCards = new ushort[DECK_COUNT * DECK_SIZE];
        public byte[] deckLengths = new byte[DECK_COUNT];

        [Header("Quests (progress/stage per quest id)")]
        public byte[] questProgress = new byte[QUEST_SLOTS];

        [HideInInspector] public string lastError = "";

        private byte[] _buf;
        private int _pos;
        private int _end;

        // ------------------------------------------------------------------ defaults / copy

        public void ResetToDefaults(School newSchool, string newName)
        {
            characterName = SanitizeName(newName);
            school = newSchool;
            level = 1;
            xp = 0;
            gold = 50;
            maxHealth = GetStarterHealth(newSchool);
            maxMana = 10;

            EnsureArrays();
            for (int i = 0; i < EQUIP_SLOTS; i++) equipped[i] = 0;
            for (int i = 0; i < INVENTORY_SLOTS; i++) { inventoryItemIds[i] = 0; inventoryCounts[i] = 0; }
            for (int i = 0; i < deckCards.Length; i++) deckCards[i] = 0;
            for (int i = 0; i < DECK_COUNT; i++) deckLengths[i] = 0;
            for (int i = 0; i < QUEST_SLOTS; i++) questProgress[i] = 0;

            // Starter kit (see ItemDatabase): staff + robe equipped; potions, hat and boots in the backpack.
            equipped[EQUIP_WEAPON] = 1;
            equipped[EQUIP_BODY] = 2;
            inventoryItemIds[0] = 10;
            inventoryCounts[0] = 3;
            inventoryItemIds[1] = 3;
            inventoryCounts[1] = 1;
            inventoryItemIds[2] = 4;
            inventoryCounts[2] = 1;

            // Starter deck: four copies of the school's basic spell (card id = 100 + school * 10).
            ushort basicSpell = (ushort)(100 + (int)newSchool * 10);
            for (int c = 0; c < 4; c++) deckCards[c] = basicSpell;
            deckLengths[0] = 4;

            lastError = "";
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
            level = other.level;
            xp = other.xp;
            gold = other.gold;
            maxHealth = other.maxHealth;
            maxMana = other.maxMana;
            System.Array.Copy(other.equipped, equipped, EQUIP_SLOTS);
            System.Array.Copy(other.inventoryItemIds, inventoryItemIds, INVENTORY_SLOTS);
            System.Array.Copy(other.inventoryCounts, inventoryCounts, INVENTORY_SLOTS);
            System.Array.Copy(other.deckCards, deckCards, DECK_COUNT * DECK_SIZE);
            System.Array.Copy(other.deckLengths, deckLengths, DECK_COUNT);
            System.Array.Copy(other.questProgress, questProgress, QUEST_SLOTS);
        }

        public void EnsureArrays()
        {
            if (equipped == null || equipped.Length != EQUIP_SLOTS) equipped = new ushort[EQUIP_SLOTS];
            if (inventoryItemIds == null || inventoryItemIds.Length != INVENTORY_SLOTS) inventoryItemIds = new ushort[INVENTORY_SLOTS];
            if (inventoryCounts == null || inventoryCounts.Length != INVENTORY_SLOTS) inventoryCounts = new byte[INVENTORY_SLOTS];
            if (deckCards == null || deckCards.Length != DECK_COUNT * DECK_SIZE) deckCards = new ushort[DECK_COUNT * DECK_SIZE];
            if (deckLengths == null || deckLengths.Length != DECK_COUNT) deckLengths = new byte[DECK_COUNT];
            if (questProgress == null || questProgress.Length != QUEST_SLOTS) questProgress = new byte[QUEST_SLOTS];
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
            WriteU8(questUsed);
            for (int i = 0; i < questUsed; i++) WriteU8(questProgress[i]);

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
            bool ok = ReadPayloadV1();
            _buf = null;
            return ok;
        }

        private bool ReadPayloadV1()
        {
            EnsureArrays();

            if (!Need(1)) return false;
            int nameLen = ReadU8();
            if (nameLen > MAX_NAME_LENGTH || !Need(nameLen * 2)) { lastError = "bad name"; return false; }
            char[] chars = new char[nameLen];
            for (int i = 0; i < nameLen; i++) chars[i] = (char)ReadU16();
            string newName = new string(chars);

            if (!Need(1 + 1 + 4 + 4 + 2 + 2 + EQUIP_SLOTS * 2)) return false;
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

            for (int i = 0; i < EQUIP_SLOTS; i++) equipped[i] = (ushort)ReadU16();

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
                if (!Need(1)) return false;
                int count = ReadU8();
                if (count > DECK_SIZE || !Need(count * 2)) { lastError = "bad deck"; return false; }
                deckLengths[d] = (byte)count;
                for (int c = 0; c < DECK_SIZE; c++)
                    deckCards[d * DECK_SIZE + c] = c < count ? (ushort)ReadU16() : (ushort)0;
            }

            if (!Need(1)) return false;
            int questUsed = ReadU8();
            if (questUsed > QUEST_SLOTS || !Need(questUsed)) { lastError = "bad quests"; return false; }
            for (int i = 0; i < QUEST_SLOTS; i++)
                questProgress[i] = i < questUsed ? (byte)ReadU8() : (byte)0;

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
