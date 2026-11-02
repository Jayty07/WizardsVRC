using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Titles, auras, spell skins and elixirs (local; the builder fills the tables). Titles and auras are earned by
    /// playing (levels, monsters, bosses, towers, the Kraken) or bought in the Starshard Shop; what you wear shows to
    /// everyone through your PlayerCosmetics object. Elixirs are timed stat boosts that tick down while you play and
    /// never apply in PvP duels.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CosmeticsDatabase : UdonSharpBehaviour
    {
        // How a title or aura is unlocked (value in the matching *UnlockValue).
        public const int UNLOCK_LEVEL = 0;
        public const int UNLOCK_MONSTERS = 1;
        public const int UNLOCK_BOSSES = 2;
        public const int UNLOCK_BATTLES = 3;
        public const int UNLOCK_FLAG = 4;      // bit 'value' of CharacterData.towersCleared (towers; 30 = the Kraken)
        public const int UNLOCK_SHOP = 5;      // bought with Starshards
        public const int UNLOCK_PRODUCT = 6;   // owned VRChat product (StarshardStore product index 'value')
        public const int UNLOCK_SCHOOL = 7;    // your own school's aura at level 'value'
        public const int UNLOCK_QUEST = 8;     // quest (save id 'value') finished
        public const int UNLOCK_BOUNTIES = 9;  // 'value' bounties turned in at the bounty board
        public const int UNLOCK_HIGHNOON = 10; // 'value' High Noon duels won

        public const int ELIXIR_SPEED = 100;
        public const int ELIXIR_XP = 101;

        public PlayerState playerState;
        public NotificationCenter notifications;
        public StarshardStore store;
        public QuestManager questManager;
        [Tooltip("Your own PlayerCosmetics (the per-player object registers itself when it spawns).")]
        [HideInInspector] public PlayerCosmetics localCosmetics;

        [Header("Titles")]
        public string[] titleNames;
        public int[] titleUnlockKind;
        public int[] titleUnlockValue;
        public int[] titlePrice;
        public string[] titleHints;

        [Header("Auras")]
        public string[] auraNames;
        public Color[] auraColors;
        public int[] auraUnlockKind;
        public int[] auraUnlockValue;
        public int[] auraPrice;
        public string[] auraHints;

        [Header("Spell skins (tint the casting glyph and the spell flashes)")]
        public string[] skinNames;
        public Color[] skinColors;
        public int[] skinPrice;

        [Header("Elixirs (timed boosts; ItemDatabase stat ids, or ELIXIR_SPEED / ELIXIR_XP)")]
        public string[] elixirNames;
        public int[] elixirStat;
        public int[] elixirAmount;
        public int[] elixirPrice;
        public int elixirMinutes = 30;
        public int elixirMaxMinutes = 180;

        private float _nextCheck;
        private float _tickAccum;
        private int _slot = -1;
        private float _appliedSpeed = -1f;

        // ------------------------------------------------------------------ unlocks

        private bool Met(CharacterData c, int kind, int value)
        {
            switch (kind)
            {
                case UNLOCK_LEVEL: return c.level >= value;
                case UNLOCK_MONSTERS: return c.monstersDefeated >= value;
                case UNLOCK_BOSSES: return c.bossesDefeated >= value;
                case UNLOCK_BATTLES: return c.battlesWon >= value;
                case UNLOCK_FLAG: return c.HasBit(c.towersCleared, value);
                case UNLOCK_PRODUCT: return store != null && store.OwnsProduct(value);
                case UNLOCK_QUEST: return questManager != null && questManager.IsComplete(value);
                case UNLOCK_BOUNTIES: return c.bountiesDone >= value;
                case UNLOCK_HIGHNOON: return c.highNoonWins >= value;
                default: return false;
            }
        }

        public bool HasTitle(int i)
        {
            if (!HasCharacter() || i < 0 || i >= titleNames.Length) return false;
            CharacterData c = playerState.current;
            return c.HasBit(c.titleBits, i) || Met(c, titleUnlockKind[i], titleUnlockValue[i]);
        }

        public bool HasAura(int i)
        {
            if (!HasCharacter() || i < 0 || i >= auraNames.Length) return false;
            CharacterData c = playerState.current;
            if (c.HasBit(c.auraBits, i)) return true;
            if (auraUnlockKind[i] == UNLOCK_SCHOOL) return i == (int)c.school && c.level >= auraUnlockValue[i];
            return Met(c, auraUnlockKind[i], auraUnlockValue[i]);
        }

        public bool HasSkin(int i)
        {
            return HasCharacter() && i >= 0 && i < skinNames.Length && playerState.current.HasBit(playerState.current.skinBits, i);
        }

        /// <summary>Earned titles and auras become yours for good (and you get a notice) the moment you qualify.</summary>
        private void CheckUnlocks()
        {
            CharacterData c = playerState.current;
            for (int i = 0; i < titleNames.Length && i < 31; i++)
            {
                if (c.HasBit(c.titleBits, i) || titleUnlockKind[i] == UNLOCK_SHOP || titleUnlockKind[i] == UNLOCK_PRODUCT) continue;
                if (!Met(c, titleUnlockKind[i], titleUnlockValue[i])) continue;
                c.titleBits |= 1 << i;
                playerState.MarkDirty();
                if (notifications != null) notifications.Post(NotificationCenter.KIND_INFO, "Title earned: " + titleNames[i], "Wear it from the Wardrobe in your book.", 0);
            }
            for (int i = 0; i < auraNames.Length && i < 31; i++)
            {
                if (c.HasBit(c.auraBits, i) || auraUnlockKind[i] == UNLOCK_SHOP || auraUnlockKind[i] == UNLOCK_PRODUCT) continue;
                bool ok = auraUnlockKind[i] == UNLOCK_SCHOOL ? i == (int)c.school && c.level >= auraUnlockValue[i] : Met(c, auraUnlockKind[i], auraUnlockValue[i]);
                if (!ok) continue;
                c.auraBits |= 1 << i;
                playerState.MarkDirty();
                if (notifications != null) notifications.Post(NotificationCenter.KIND_INFO, "Aura earned: " + auraNames[i], "Wear it from the Wardrobe in your book.", 0);
            }
        }

        // ------------------------------------------------------------------ wearing

        public void WearTitle(int i)
        {
            if (!HasCharacter()) return;
            playerState.current.equippedTitle = i >= 0 && HasTitle(i) ? i + 1 : 0;
            playerState.MarkDirty();
            PushLook();
        }

        public void WearAura(int i)
        {
            if (!HasCharacter()) return;
            playerState.current.equippedAura = i >= 0 && HasAura(i) ? i + 1 : 0;
            playerState.MarkDirty();
            PushLook();
        }

        public void WearSkin(int i)
        {
            if (!HasCharacter()) return;
            playerState.current.equippedSkin = i >= 0 && HasSkin(i) ? i + 1 : 0;
            playerState.MarkDirty();
            PushLook();
        }

        /// <summary>Sends what you're wearing to your PlayerCosmetics (everyone sees it).</summary>
        public void PushLook()
        {
            if (localCosmetics == null) return;
            if (!HasCharacter()) { localCosmetics.SetLook(0, 0, 0); return; }
            CharacterData c = playerState.current;
            localCosmetics.SetLook(c.equippedTitle, c.equippedAura, c.equippedSkin);
        }

        public string TitleName(int index1) { return index1 > 0 && index1 <= titleNames.Length ? titleNames[index1 - 1] : ""; }
        public Color AuraColor(int index1) { return index1 > 0 && index1 <= auraColors.Length ? auraColors[index1 - 1] : Color.white; }
        public Color SkinColor(int index1) { return index1 > 0 && index1 <= skinColors.Length ? skinColors[index1 - 1] : Color.white; }

        // ------------------------------------------------------------------ buying with Starshards

        /// <summary>Spends Starshards (kind 0 title, 1 aura, 2 skin, 3 elixir). Returns a message for the shop page.</summary>
        public string Buy(int kind, int i)
        {
            if (!HasCharacter()) return "";
            CharacterData c = playerState.current;
            int price = kind == 0 ? SafeAt(titlePrice, i) : kind == 1 ? SafeAt(auraPrice, i) : kind == 2 ? SafeAt(skinPrice, i) : SafeAt(elixirPrice, i);
            if (price <= 0) return "That isn't for sale.";
            if (kind == 0 && HasTitle(i)) return "You already have that title.";
            if (kind == 1 && HasAura(i)) return "You already have that aura.";
            if (kind == 2 && HasSkin(i)) return "You already have that spell skin.";
            if (kind == 3 && c.elixirSeconds[i] >= (elixirMaxMinutes - elixirMinutes) * 60 + 1) return "That elixir can't stack any longer.";
            if (c.starshards < price) return "You need " + (price - c.starshards).ToString() + " more Starshards.";
            c.starshards -= price;
            string what;
            if (kind == 0) { c.titleBits |= 1 << i; what = titleNames[i]; }
            else if (kind == 1) { c.auraBits |= 1 << i; what = auraNames[i]; }
            else if (kind == 2) { c.skinBits |= 1 << i; what = skinNames[i]; }
            else
            {
                c.elixirSeconds[i] = Mathf.Min(elixirMaxMinutes * 60, c.elixirSeconds[i] + elixirMinutes * 60);
                what = elixirNames[i];
                ApplySpeed();
            }
            playerState.MarkDirty();
            playerState.SaveNow();
            if (notifications != null) notifications.Post(NotificationCenter.KIND_SHOP, "Bought: " + what, "", 0);
            return "Bought " + what + "!";
        }

        private int SafeAt(int[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : 0; }

        // ------------------------------------------------------------------ elixirs

        /// <summary>Total bonus to a stat from active elixirs (0 in PvP: callers pass pvp).</summary>
        public int ElixirBonus(int stat, bool pvp)
        {
            if (pvp || !HasCharacter() || elixirStat == null) return 0;
            CharacterData c = playerState.current;
            int total = 0;
            for (int i = 0; i < elixirStat.Length && i < CharacterData.ELIXIRS; i++)
                if (elixirStat[i] == stat && c.elixirSeconds[i] > 0) total += elixirAmount[i];
            return total;
        }

        public int ElixirMinutesLeft(int i)
        {
            if (!HasCharacter() || i < 0 || i >= CharacterData.ELIXIRS) return 0;
            return (playerState.current.elixirSeconds[i] + 59) / 60;
        }

        private void ApplySpeed()
        {
            float want = 1f + ElixirBonus(ELIXIR_SPEED, false) / 100f;
            if (Mathf.Abs(want - _appliedSpeed) < 0.001f) return;
            _appliedSpeed = want;
            playerState.SetSpeedMultiplier(want);
        }

        private void Update()
        {
            if (!HasCharacter()) { _slot = -1; return; }
            CharacterData c = playerState.current;
            if (_slot != playerState.activeSlot)
            {
                _slot = playerState.activeSlot;
                _appliedSpeed = -1f;
                PushLook();
            }
            // Elixirs tick down while you play (whole seconds).
            _tickAccum += Time.deltaTime;
            if (_tickAccum >= 1f)
            {
                int whole = (int)_tickAccum;
                _tickAccum -= whole;
                bool any = false;
                for (int i = 0; i < CharacterData.ELIXIRS; i++)
                    if (c.elixirSeconds[i] > 0)
                    {
                        c.elixirSeconds[i] = Mathf.Max(0, c.elixirSeconds[i] - whole);
                        any = true;
                        if (c.elixirSeconds[i] == 0 && notifications != null && elixirNames != null && i < elixirNames.Length)
                            notifications.Post(NotificationCenter.KIND_INFO, elixirNames[i] + " wore off", "", 0);
                    }
                if (any) { ApplySpeed(); playerState.MarkDirty(); }
            }
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 2f;
            CheckUnlocks();
            ApplySpeed();
        }

        private bool HasCharacter() { return playerState != null && playerState.HasCharacter() && playerState.current != null; }
    }
}
