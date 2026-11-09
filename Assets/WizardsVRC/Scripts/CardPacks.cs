using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Starshard card packs (the Starshard Shop's Packs category), like Wizard101's packs: buy one and it opens on the spot,
    /// turning over its cards one by one, each a random pull of a rarity (Common, Uncommon, Rare, Ultra-Rare). What each
    /// card can be is set by the pack's slots (the builder fills them, WizardsVRCSceneBuilder.Packs.cs):
    ///   - SLOT_TREASURE: a treasure card, pips slotA..slotB, of slotSchool (-2 = any school or General). Cheaper spells are
    ///     common, big ones rare (rarityWeights by CardDatabase.RarityOf).
    ///   - SLOT_GEAR: a piece of gear for your level (slotA levels below to slotB above), for your school or any school;
    ///     slotSchool is the gear slot it must be (CharacterData.EQUIP_*, e.g. EQUIP_WEAPON for a wand) or -2 for any.
    ///   - SLOT_GOLD: slotA..slotB gold.
    ///   - Any card can instead, ludicrously rarely (slotSpecial in 10,000), be a Starshard spell you learn for good
    ///     (CardDatabase.starshardSpellIds, one you don't know yet): result SLOT_SPELL, rarity Starshard.
    /// Treasure cards go to your collection (put them in a deck's sideboard on the book's Deck page); gear to your backpack
    /// (if it's full, you get its worth in gold instead).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CardPacks : UdonSharpBehaviour
    {
        public const int SLOT_TREASURE = 0;
        public const int SLOT_GEAR = 1;
        public const int SLOT_GOLD = 2;
        /// <summary>A result only: a Starshard spell learned.</summary>
        public const int SLOT_SPELL = 3;
        public const int MAX_CARDS = 6;
        public const int ANY_SCHOOL = -2;

        public PlayerState playerState;
        public CardDatabase cardDatabase;
        public ItemDatabase itemDatabase;

        [Header("Packs (the builder fills these)")]
        public string[] packNames;
        public int[] packPrices;
        public string[] packBlurbs;
        public Color[] packColors;
        [Tooltip("Per pack: its cards are slots[packSlotStart .. + packSlotCount).")]
        public int[] packSlotStart;
        public int[] packSlotCount;
        public int[] slotKinds;
        public int[] slotA;
        public int[] slotB;
        public int[] slotSchool;
        [Tooltip("Per slot: chance in 10,000 that the card is a Starshard spell you learn instead.")]
        public int[] slotSpecial;
        [Tooltip("How likely each rarity of treasure card is (Common, Uncommon, Rare, Ultra-Rare), per spell.")]
        public int[] rarityWeights = { 60, 28, 10, 2 };

        [Header("The last pack opened")]
        [HideInInspector] public int resultCount;
        [HideInInspector] public int[] resultKinds = new int[MAX_CARDS];
        /// <summary>Spell id (treasure card), item id (gear) or gold amount.</summary>
        [HideInInspector] public int[] resultIds = new int[MAX_CARDS];
        [HideInInspector] public int[] resultRarity = new int[MAX_CARDS];
        /// <summary>Gold given instead (a full backpack or a full stack), 0 = none.</summary>
        [HideInInspector] public int[] resultGold = new int[MAX_CARDS];

        public int PackCount() { return packNames != null ? packNames.Length : 0; }
        public string PackName(int p) { return packNames != null && p >= 0 && p < packNames.Length ? packNames[p] : ""; }
        public int PackPrice(int p) { return packPrices != null && p >= 0 && p < packPrices.Length ? packPrices[p] : 0; }
        public string PackBlurb(int p) { return packBlurbs != null && p >= 0 && p < packBlurbs.Length ? packBlurbs[p] : ""; }
        public Color PackColor(int p) { return packColors != null && p >= 0 && p < packColors.Length ? packColors[p] : Color.white; }

        /// <summary>
        /// Buys and opens pack p: spends its Starshards, rolls every card and gives them to you (saved at once). "" when it
        /// opened (see the result arrays), otherwise why not.
        /// </summary>
        public string Open(int p)
        {
            if (playerState == null || !playerState.HasCharacter() || cardDatabase == null) return "No wizard loaded.";
            if (p < 0 || p >= PackCount()) return "No such pack.";
            CharacterData c = playerState.current;
            int price = PackPrice(p);
            if (c.starshards < price) return "You need " + (price - c.starshards).ToString() + " more Starshards.";
            c.starshards -= price;
            resultCount = 0;
            int a = packSlotStart[p];
            int n = Mathf.Min(packSlotCount[p], MAX_CARDS);
            for (int k = 0; k < n; k++)
            {
                int s = a + k;
                int kind = slotKinds[s];
                resultKinds[k] = kind;
                resultGold[k] = 0;
                int special = slotSpecial != null && s < slotSpecial.Length ? slotSpecial[s] : 0;
                int spell = special > 0 && Random.Range(0, 10000) < special ? RollStarshardSpell(c) : 0;
                if (spell > 0)
                {
                    resultKinds[k] = SLOT_SPELL;
                    resultIds[k] = spell;
                    resultRarity[k] = CardDatabase.RARITY_STARSHARD;
                    c.Learn(spell);
                }
                else if (kind == SLOT_TREASURE)
                {
                    int id = RollTreasure(slotA[s], slotB[s], slotSchool[s]);
                    if (id <= 0) id = cardDatabase.BasicSpell(Mathf.Max(0, (int)c.school));
                    resultIds[k] = id;
                    resultRarity[k] = cardDatabase.RarityOf(id);
                    if (c.AddTreasure(id, 1) == 0) { resultGold[k] = cardDatabase.TreasurePrice(id) / 2; c.gold += resultGold[k]; }
                }
                else if (kind == SLOT_GEAR)
                {
                    int item = RollGear(c, slotA[s], slotB[s], slotSchool[s]);
                    if (item <= 0)
                    {
                        // Nothing fits (no gear at this level yet): gold instead.
                        resultKinds[k] = SLOT_GOLD;
                        resultIds[k] = 100 + c.level * 15;
                        resultRarity[k] = 1;
                        c.gold += resultIds[k];
                    }
                    else
                    {
                        resultIds[k] = item;
                        resultRarity[k] = itemDatabase.GetLevelReq(item) >= c.level ? 3 : itemDatabase.GetLevelReq(item) >= c.level - 2 ? 2 : 1;
                        if (!c.AddItem(item, 1)) { resultGold[k] = 40 + itemDatabase.GetLevelReq(item) * 12; c.gold += resultGold[k]; }
                    }
                }
                else
                {
                    int lo = slotA[s];
                    int hi = Mathf.Max(slotA[s], slotB[s]);
                    int gold = Random.Range(lo, hi + 1);
                    resultIds[k] = gold;
                    resultRarity[k] = gold >= (lo + hi) * 3 / 4 ? 1 : 0;
                    c.gold += gold;
                }
                resultCount++;
            }
            playerState.MarkDirty();
            playerState.SaveNow();
            return "";
        }

        /// <summary>A random treasure card (pips lo..hi, of the school or any), weighted by rarity; 0 if none fits.</summary>
        private int RollTreasure(int lo, int hi, int school)
        {
            int total = 0;
            int n = cardDatabase.Count();
            for (int i = 0; i < n; i++)
            {
                int id = cardDatabase.IdAt(i);
                if (Eligible(id, lo, hi, school)) total += Weight(id);
            }
            if (total <= 0) return 0;
            int roll = Random.Range(0, total);
            for (int i = 0; i < n; i++)
            {
                int id = cardDatabase.IdAt(i);
                if (!Eligible(id, lo, hi, school)) continue;
                roll -= Weight(id);
                if (roll < 0) return id;
            }
            return 0;
        }

        private bool Eligible(int id, int lo, int hi, int school)
        {
            int sc = cardDatabase.GetSchool(id);
            // Every school's spells and the General ones; never the hidden Tech School.
            if (cardDatabase.IsHiddenSchool(sc) || cardDatabase.IsStarshardSpell(id)) return false;
            if (school != ANY_SCHOOL && sc != school) return false;
            int pips = cardDatabase.GetPipCost(id);
            return pips >= lo && pips <= hi;
        }

        private int Weight(int id)
        {
            int r = cardDatabase.RarityOf(id);
            return rarityWeights != null && r >= 0 && r < rarityWeights.Length ? rarityWeights[r] : 1;
        }

        /// <summary>A random piece of gear for the wizard: levels (level - below) to (level + above), their school or any; 0 if none.</summary>
        /// <summary>A Starshard spell the wizard doesn't know yet (0 when they know them all).</summary>
        private int RollStarshardSpell(CharacterData c)
        {
            int[] all = cardDatabase.starshardSpellIds;
            if (all == null) return 0;
            int unknown = 0;
            for (int i = 0; i < all.Length; i++) if (!c.HasLearned(all[i])) unknown++;
            if (unknown == 0) return 0;
            int pick = Random.Range(0, unknown);
            for (int i = 0; i < all.Length; i++)
            {
                if (c.HasLearned(all[i])) continue;
                if (pick-- == 0) return all[i];
            }
            return 0;
        }

        private int RollGear(CharacterData c, int below, int above, int slot)
        {
            if (itemDatabase == null || itemDatabase.ids == null) return 0;
            int lo = Mathf.Max(1, c.level - below);
            int hi = c.level + above;
            int total = 0;
            for (int i = 0; i < itemDatabase.ids.Length; i++) if (GearFits(c, itemDatabase.ids[i], lo, hi, slot)) total++;
            if (total == 0) return 0;
            int pick = Random.Range(0, total);
            for (int i = 0; i < itemDatabase.ids.Length; i++)
            {
                int id = itemDatabase.ids[i];
                if (!GearFits(c, id, lo, hi, slot)) continue;
                if (pick-- == 0) return id;
            }
            return 0;
        }

        private bool GearFits(CharacterData c, int id, int lo, int hi, int slot)
        {
            if (slot >= 0 && itemDatabase.GetCategory(id) != slot) return false;
            // Not the starter kit, decks or quest/boss items (those are earned, never pulled from a pack).
            if (id <= 4 || id == CharacterData.STARTER_DECK_ITEM || !itemDatabase.IsEquippable(id) || itemDatabase.IsUnique(id) || itemDatabase.IsDeck(id)) return false;
            int lv = itemDatabase.GetLevelReq(id);
            if (lv < lo || lv > hi) return false;
            int sc = itemDatabase.GetSchoolReq(id);
            return sc == ItemDatabase.ANY_SCHOOL || sc == (int)c.school;
        }
    }
}
