using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Local side of combat: renders the synced <see cref="BattleManager"/> state (ring, monsters, labels, round playback)
    /// and, for participants, handles teleporting, the 7-card hand drawn from Deck 1, target picking and rewards.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BattleClient : UdonSharpBehaviour
    {
        public const int HAND_SIZE = 7;

        public BattleManager manager;
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public CardDatabase cardDatabase;

        [Header("World")]
        public Transform hubReturn;
        [Tooltip("Where a defeated wizard is sent (e.g. the zone's entrance). Empty = hubReturn.")]
        public Transform defeatReturn;
        [Tooltip("The encounter's root (ring, seats, labels, hand panel). Moved to the manager's ring spot when a fight starts.")]
        public Transform encounterRoot;
        [Tooltip("Body size per seat (the builder fills it from each seat's monster) for cast effects over their heads.")]
        public float[] seatScales;
        public Transform[] seats;
        public GameObject ring;
        public GameObject[] monsterBodies;
        public Text[] overheadLabels;
        public Text bannerLabel;
        public Text padLabel;
        public Transform orb;
        public Renderer orbRenderer;

        [Header("School cast effect (parented to the ring)")]
        public Transform castRing;
        public Renderer castRingRenderer;
        public Renderer castGlyph;
        [Tooltip("Indexed by School; the last entry is for General spells.")]
        public Material[] castGlyphMats;
        public Transform impactBurst;
        public Renderer impactRenderer;
        public AudioSource castAudio;
        [Tooltip("School-coloured particle burst off the caster, indexed by School; the last entry is for General spells.")]
        public ParticleSystem[] castBursts;
        [Tooltip("Indexed by School. General spells play no cast sound.")]
        public AudioClip[] castSounds;
        public Transform[] stagingSpots;
        public Transform arenaCenter;
        public float engageRadius = 14f;
        public Transform clockHand;
        public Text clockLabel;
        [Tooltip("One glowing ring per seat, coloured by the school of whoever sits there.")]
        public Renderer[] seatRings;
        [Tooltip("Indexed by School.")]
        public Material[] seatSchoolMats;
        public Material seatEmptyMat;
        [Tooltip("Empty seats on the monsters' half (tinted so the two sides read clearly). Empty = seatEmptyMat.")]
        public Material seatEmptyMonsterMat;

        [Header("Shields / blades / traps orbiting each seat")]
        [Tooltip("Index slot * 3 + kind (0 shields, 1 blades, 2 traps).")]
        public Transform[] fxOrbits;
        [Tooltip("Index (slot * 3 + kind) * BattleManager.FX_MAX + k.")]
        public Renderer[] fxIcons;
        [Tooltip("Indexed by School; the last entry is for General (any school).")]
        public Material[] fxMats;
        [Tooltip("Icon per kind and school: index kind * 8 + school (7 = General). Shields, blades, traps.")]
        public Material[] fxIconMats;
        [Tooltip("Bright versions of fxIconMats, blinked when one breaks.")]
        public Material[] fxIconFlashMats;
        [Tooltip("Same icons as textures, for the floating text.")]
        public Texture[] fxIconTextures;

        [Header("Duel circle layers and the start flash")]
        [Tooltip("Inner sigil disc (turns slowly, glow pulses).")]
        public Transform circleSigil;
        [Tooltip("Outer rune band (turns the other way).")]
        public Transform circleRunes;
        [Tooltip("Sigil material; its glow pulses.")]
        public Material circleSigilMat;
        public Color circleSigilGlow = new Color(0.35f, 0.35f, 0.45f);
        [Tooltip("White copy of the circle shown when a fight starts; it settles into the real circle.")]
        public Transform circleFlash;
        public Material circleFlashMat;
        public Transform flashPillar;
        public Light flashLight;
        public ParticleSystem startSparks;
        public AudioSource circleAudio;
        public AudioClip circleSound;
        public float startFlashSeconds = 1.1f;
        [Tooltip("PSX look: the circle turns in steps of this many degrees, a few times a second.")]
        public float circleStepDegrees = 4f;
        public float sigilDegreesPerSecond = 9f;
        public float runeDegreesPerSecond = -6f;

        [Header("Pips at each seat (Wizard101 style)")]
        [Tooltip("Index slot * BattleManager.MAX_PIPS + k, in an arc in front of the seat.")]
        public Renderer[] pipIcons;
        public Material pipMat;
        public Material powerPipMat;
        [Tooltip("White motes swirling round each combatant, more the more pips they hold (one per seat).")]
        public ParticleSystem[] pipSwirls;
        [Tooltip("Gold motes for power pips (one per seat).")]
        public ParticleSystem[] powerSwirls;
        public float swirlInterval = 0.4f;

        [Header("Effect breaks and floating combat text")]
        [Tooltip("Shards that flash and burst where a shield / blade / trap breaks.")]
        public Transform[] breakPops;
        public Renderer[] breakPopRenderers;
        public Transform[] floaters;
        public Text[] floaterTexts;
        public RawImage[] floaterIcons;
        [Tooltip("Indexed by School; the last entry is the General icon.")]
        public Texture[] schoolIcons;
        public float floaterSeconds = 1.9f;

        [Header("Sounds")]
        [Tooltip("Positional source for hits, heals and defeats (moved to whoever it happens to).")]
        public AudioSource hitAudio;
        [Tooltip("Played when a wizard takes damage (one picked at random).")]
        public AudioClip[] wizardHitSounds;
        public AudioClip healSound;
        public AudioClip defeatSound;
        [Tooltip("Local interface sounds (cards).")]
        public UiAudio uiAudio;

        [Header("Summoned minions (one body per seat, parented to the ring)")]
        public Transform[] minionBodies;
        [Tooltip("Index slot * 2 + part; recoloured to the minion's school (uses fxMats).")]
        public Renderer[] minionSkins;
        public Transform summonFlash;

        [Header("Quests")]
        public QuestManager questManager;

        [Header("School combat traits (indexed by School): + damage, + resist, + accuracy (health: PlayerState.schoolHealthPct)")]
        public int[] schoolDamage = { 10, 0, 25, 0, 5, 10, 5 };
        public int[] schoolResist = { 0, 20, 0, 0, 5, 0, 5 };
        public int[] schoolAccuracy = { 0, 0, -10, 10, 0, 0, 5 };

        [Header("Defeat")]
        [Tooltip("After the killing hit lands, how long the defeated monster stays before it starts to vanish.")]
        public float defeatDelay = 0.45f;
        [Tooltip("How long a defeated monster takes to squash down into the floor.")]
        public float defeatFadeSeconds = 0.6f;

        private int[] _minionShown = new int[BattleManager.SLOTS];
        // Time the shown health of each seat hit 0 during playback (0 = not dying); its body vanishes after that.
        private float[] _dieStart = new float[BattleManager.SLOTS];
        private Vector3[] _bodyBase;
        private int _walkSlot = -1;
        private float _walkStart;

        private float[] _fxRadius = { 0.75f, 0.45f, 0.9f };
        private float[] _fxSpin = { 70f, -110f, 45f };
        /// <summary>Degrees between stacked effects of one kind: each new one sits right next to the last.</summary>
        private float[] _fxStackDeg = { 42f, 30f, 38f };

        // What the circle shows: the synced state, except during round playback, when it changes step by step.
        private int[] _fxSh = new int[BattleManager.SLOTS * BattleManager.FX_MAX];
        private int[] _fxBl = new int[BattleManager.SLOTS * BattleManager.FX_MAX];
        private int[] _fxTr = new int[BattleManager.SLOTS * BattleManager.FX_MAX];
        private int[] _shownPips = new int[BattleManager.SLOTS];
        private int[] _shownPower = new int[BattleManager.SLOTS];
        private float _flashStart = -100f;
        private int _flashBattle = -1;
        private float _nextSwirl;
        private float[] _popStart;
        private int _popNext;
        private float[] _floatStart;
        private Vector3[] _floatBase;
        private int _floatNext;
        private int[] _stack = new int[BattleManager.SLOTS];
        private Vector3[] _pipBase;

        [Header("Hand panel")]
        public GameObject handRoot;
        [Tooltip("Card frames (coloured by school); the card buttons themselves.")]
        public Image[] cardImages;
        public Text[] cardNames;
        public Text[] cardInfos;
        [Tooltip("Parchment face inside each card frame (dimmed when the card can't be cast).")]
        public Image[] cardFaces;
        public Text[] cardPips;
        public Text[] cardAccuracies;
        public Text[] cardSchools;
        [Tooltip("Card frame per school (index School, 7 = General): design 1 \"Classic\".")]
        public Sprite[] cardFrames;
        [Tooltip("Art window per card: shields / blades / traps show their icon; other spells stay blank until they have art.")]
        public RawImage[] cardArts;
        [Tooltip("Hovered cards grow by this much and rise toward you (Wizard101 style).")]
        public float hoverScale = 1.45f;
        public float hoverRaise = 135f;
        public Text statusLabel;
        public Text partyLabel;
        public GameObject[] targetButtons;
        public Text[] targetLabels;
        public Image discardImage;

        [Header("Rewards")]
        public int[] lootTable = { 10, 11, 20, 21, 22, 23, 24, 25, 26, 27, 28, 30, 31, 40, 41, 50, 51, 61, 62 };

        [Header("Defeat")]
        [Tooltip("Health (% of max) you are sent home with after being defeated.")]
        public int defeatHealthPct = 25;

        [Header("Fizzles")]
        [Tooltip("Off (Wizard101): a fizzled card goes back into your deck at a random spot. On: it goes to the end of your hand instead.")]
        public bool fizzleReturnsToHand;
        [Tooltip("Potion button label (optional).")]
        public Text potionLabel;

        private bool _inBattle;

        public bool IsInBattle() { return _inBattle; }

        /// <summary>True when the local player has a character and isn't already fighting (used by wandering monsters).</summary>
        public bool CanFight() { return !_inBattle && playerState != null && playerState.HasCharacter() && !playerState.inCombat; }

        private int _slot = -1;
        private bool _seated;
        private float _nextEngage;
        private int _clockActor = -1;
        private int _battleSeen;
        private int _roundSeen;
        private int _phaseSeen;
        /// <summary>Health/mana were already written back to the character for this fight (victory, defeat or flee).</summary>
        private bool _vitalsDone;
        private bool _leaving;
        private bool _defeated;
        private float _rejoinBlockUntil;

        /// <summary>The deck plus room for item cards from gear.</summary>
        private int[] _pile = new int[CharacterData.DECK_SIZE + 32];
        private int _pileCount;
        private int[] _hand = new int[HAND_SIZE];
        private int _handCount;
        private int _selHand = -1;
        private bool _discardMode;
        private bool _submitted;
        private string _note = "";

        private int[] _shownHp = new int[BattleManager.SLOTS];
        private bool _playing;
        private int _playIndex;
        private float _orbStart;
        private Vector3 _orbFrom;
        private Vector3 _orbTo;
        private bool _casting;
        private bool _impacting;
        private bool _burstDone;
        private int _castSchool;
        private float _castLen;
        private float _castPeak;
        private float _impactStart;
        private int _stepStart;
        private int _stepEnd;
        private string _stepHeader = "";
        private int _castFrom;
        private int _castTo;
        private Vector3 _ringBase;
        private Vector3 _glyphBase;
        private GameObject _spellAnim;

        private Color _cardDim = new Color(0.35f, 0.35f, 0.35f);
        private Color _faceOn = new Color(0.93f, 0.87f, 0.72f);
        private Color _faceOff = new Color(0.55f, 0.53f, 0.5f);

        // ------------------------------------------------------------------ uGUI events

        public void Card0() { ClickCard(0); }
        public void Card1() { ClickCard(1); }
        public void Card2() { ClickCard(2); }
        public void Card3() { ClickCard(3); }
        public void Card4() { ClickCard(4); }
        public void Card5() { ClickCard(5); }
        public void Card6() { ClickCard(6); }

        // Pointer enter / exit on each card (EventTrigger). VRChat can send these twice, so they're idempotent.
        public void HoverCard0() { SetHover(0); }
        public void HoverCard1() { SetHover(1); }
        public void HoverCard2() { SetHover(2); }
        public void HoverCard3() { SetHover(3); }
        public void HoverCard4() { SetHover(4); }
        public void HoverCard5() { SetHover(5); }
        public void HoverCard6() { SetHover(6); }
        public void UnhoverCard0() { ClearHover(0); }
        public void UnhoverCard1() { ClearHover(1); }
        public void UnhoverCard2() { ClearHover(2); }
        public void UnhoverCard3() { ClearHover(3); }
        public void UnhoverCard4() { ClearHover(4); }
        public void UnhoverCard5() { ClearHover(5); }
        public void UnhoverCard6() { ClearHover(6); }

        public void Target0() { ClickTarget(0); }
        public void Target1() { ClickTarget(1); }
        public void Target2() { ClickTarget(2); }
        public void Target3() { ClickTarget(3); }
        public void Target4() { ClickTarget(4); }
        public void Target5() { ClickTarget(5); }
        public void Target6() { ClickTarget(6); }
        public void Target7() { ClickTarget(7); }

        public void Pass()
        {
            if (!CanAct()) return;
            Submit(0, -1, "You pass.");
        }

        public void ToggleDiscard()
        {
            if (!CanAct()) return;
            _discardMode = !_discardMode;
            _selHand = -1;
            _note = _discardMode ? "Click cards to discard them. Click Discard again when done." : "";
            RefreshHand();
        }

        /// <summary>Leave the fight now. Like Wizard101 you keep your health but lose all of your mana.</summary>
        public void Flee()
        {
            if (!_inBattle || _leaving || _slot < 0 || !manager.Alive(_slot)) return;
            int ph = manager.phase;
            if (ph != BattleManager.PHASE_PLANNING && ph != BattleManager.PHASE_JOINING) return;
            if (playerState != null && playerState.HasCharacter())
            {
                playerState.SetHealth(BattleToCharacterHealth(manager.hp[_slot]));
                playerState.SetMana(0);
            }
            _vitalsDone = true;
            manager.RequestFlee();
            _LeaveBattle();
        }

        /// <summary>Drink a potion as this round's action: a health potion if you're hurt, otherwise a mana potion.</summary>
        public void Potion()
        {
            if (!CanAct() || playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            bool hurt = manager.hp[_slot] < manager.maxHp[_slot];
            bool drained = playerState.GetMana() < playerState.MaxMana();
            int inv = FindPotion(c, hurt, false);
            if (inv < 0) inv = FindPotion(c, false, drained);
            if (inv < 0)
            {
                _note = hurt || drained ? "You have no potions that would help." : "You're already at full health and mana.";
                RefreshHand();
                return;
            }
            int itemId = c.inventoryItemIds[inv];
            int heal = itemDatabase.GetPotionHealth(itemId);
            int mana = itemDatabase.GetPotionMana(itemId);
            c.RemoveItemAt(inv, 1);
            if (mana > 0) playerState.RestoreMana(mana);
            playerState.MarkDirty();
            Submit(BattleManager.PICK_POTION, heal, "You drink a " + itemDatabase.GetName(itemId) + ".");
        }

        /// <summary>Inventory index of a potion restoring health (wantHealth) or mana (wantMana), or -1.</summary>
        private int FindPotion(CharacterData c, bool wantHealth, bool wantMana)
        {
            if (itemDatabase == null || (!wantHealth && !wantMana)) return -1;
            int used = c.UsedInventorySlots();
            for (int i = 0; i < used; i++)
            {
                int id = c.inventoryItemIds[i];
                if (id == 0 || c.inventoryCounts[i] == 0 || !itemDatabase.IsPotion(id)) continue;
                if (wantHealth && itemDatabase.GetPotionHealth(id) > 0) return i;
                if (wantMana && itemDatabase.GetPotionMana(id) > 0) return i;
            }
            return -1;
        }

        private int CountPotions()
        {
            if (playerState == null || !playerState.HasCharacter() || itemDatabase == null) return 0;
            CharacterData c = playerState.current;
            int used = c.UsedInventorySlots();
            int n = 0;
            for (int i = 0; i < used; i++)
                if (c.inventoryItemIds[i] != 0 && itemDatabase.IsPotion(c.inventoryItemIds[i])) n += c.inventoryCounts[i];
            return n;
        }

        // ------------------------------------------------------------------ entry points

        [Tooltip("Switches areas on before this client teleports the player (arena, ring seat, way home).")]
        public AreaCuller culler;

        [Tooltip("Boss arena queue pad: save id of the quest the player must finish before it works (-1 = always).")]
        public int queueRequiredQuest = -1;
        [Tooltip("Shown on the pad while it's locked.")]
        public string queueLockedText = "Boss Arena Teleporter\n(locked: keep following the story)";

        private bool QueueUnlocked()
        {
            // The trial quest always gets you in, so older saves already on it aren't locked out.
            return queueRequiredQuest < 0 || (questManager != null &&
                   (questManager.IsComplete(queueRequiredQuest) || questManager.IsStarted(QuestManager.QUEST_TRIAL)));
        }

        public void LocalQueue(bool enter)
        {
            if (enter && (playerState == null || !playerState.HasCharacter())) return;
            if (enter && !QueueUnlocked()) return;
            if (manager != null) manager.RequestQueue(enter);
        }

        private void Start()
        {
            CacheCardLayout();
            if (handRoot != null) handRoot.SetActive(false);
            _popStart = new float[breakPops != null ? breakPops.Length : 0];
            for (int i = 0; i < _popStart.Length; i++) _popStart[i] = -100f;
            _floatStart = new float[floaters != null ? floaters.Length : 0];
            _floatBase = new Vector3[_floatStart.Length];
            for (int i = 0; i < _floatStart.Length; i++) _floatStart[i] = -100f;
            _pipBase = new Vector3[pipIcons != null ? pipIcons.Length : 0];
            for (int i = 0; i < _pipBase.Length; i++) if (pipIcons[i] != null) _pipBase[i] = pipIcons[i].transform.localPosition;
            OnBattleState();
        }

        // ------------------------------------------------------------------ card fan (hover / select animation)

        private Vector2[] _cardBasePos = new Vector2[HAND_SIZE];
        private Quaternion[] _cardBaseRot = new Quaternion[HAND_SIZE];
        private int[] _cardBaseSibling = new int[HAND_SIZE];
        private int _hover = -1;
        private bool _layoutCached;

        private void CacheCardLayout()
        {
            if (cardImages == null) return;
            for (int i = 0; i < HAND_SIZE && i < cardImages.Length; i++)
            {
                if (cardImages[i] == null) continue;
                RectTransform rt = cardImages[i].rectTransform;
                _cardBasePos[i] = rt.anchoredPosition;
                _cardBaseRot[i] = rt.localRotation;
                _cardBaseSibling[i] = rt.GetSiblingIndex();
            }
            _layoutCached = true;
        }

        private void SetHover(int i)
        {
            if (_hover == i) return;
            _hover = i;
            if (i >= 0 && i < _handCount && uiAudio != null) uiAudio.PlayCardHover();
            RestoreCardOrder();
            if (i >= 0 && i < _handCount && cardImages != null && i < cardImages.Length && cardImages[i] != null)
                cardImages[i].rectTransform.SetAsLastSibling();
        }

        private void ClearHover(int i)
        {
            if (_hover != i) return;
            _hover = -1;
            RestoreCardOrder();
        }

        private void RestoreCardOrder()
        {
            if (!_layoutCached || cardImages == null) return;
            for (int i = 0; i < HAND_SIZE && i < cardImages.Length; i++)
                if (cardImages[i] != null) cardImages[i].rectTransform.SetSiblingIndex(_cardBaseSibling[i]);
        }

        /// <summary>Eases every card toward its pose: hovered = big, upright and raised; selected = slightly raised.</summary>
        private void AnimateCards()
        {
            if (!_layoutCached || cardImages == null || handRoot == null || !handRoot.activeSelf) return;
            float t = Mathf.Clamp01(Time.deltaTime * 14f);
            for (int i = 0; i < HAND_SIZE && i < cardImages.Length; i++)
            {
                if (cardImages[i] == null || !cardImages[i].gameObject.activeSelf) continue;
                RectTransform rt = cardImages[i].rectTransform;
                Vector2 pos = _cardBasePos[i];
                Quaternion rot = _cardBaseRot[i];
                float scale = 1f;
                if (i == _hover) { pos += new Vector2(0f, hoverRaise); rot = Quaternion.identity; scale = hoverScale; }
                else if (i == _selHand) { pos += new Vector2(0f, hoverRaise * 0.45f); scale = 1.1f; }
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, pos, t);
                rt.localRotation = Quaternion.Slerp(rt.localRotation, rot, t);
                rt.localScale = Vector3.Lerp(rt.localScale, new Vector3(scale, scale, 1f), t);
            }
        }

        /// <summary>Called on every client after the synced battle state changes.</summary>
        public void OnBattleState()
        {
            if (manager == null) return;
            int phase = manager.phase;
            if (phase != BattleManager.PHASE_IDLE) PlaceRing();
            bool active = phase >= BattleManager.PHASE_JOINING;
            if (ring != null) ring.SetActive(active);
            // Outside round playback the circle shows the synced shields, pips etc. as they are.
            if (!_playing && phase != BattleManager.PHASE_PLAYBACK) SyncShownState();
            if (active && manager.battleId != _flashBattle) StartFlash();
            if (active) { UpdateSeatRings(); UpdateFx(); UpdatePips(); }
            RefreshBodies(phase);

            VRCPlayerApi local = Networking.LocalPlayer;
            int mySlot = Utilities.IsValid(local) ? manager.FindWizardSlot(local.playerId) : -1;

            if ((phase == BattleManager.PHASE_ROAMING || phase == BattleManager.PHASE_JOINING) && manager.battleId != _battleSeen)
            {
                _battleSeen = manager.battleId;
                _roundSeen = 0;
                _clockActor = -1;
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
                ClearDefeats();
                RefreshBodies(phase);
                if (mySlot >= 0) ArriveAtArena(mySlot);
                SetBanner("");
            }
            // Joined a wandering-monster fight during its join window: the battle id didn't change, so arrive now.
            // (Not right after leaving: after fleeing our seat is still listed until the owner processes it.)
            if (!_inBattle && mySlot >= 0 && manager.wanderMode && phase >= BattleManager.PHASE_JOINING && phase <= BattleManager.PHASE_PLAYBACK &&
                Time.time >= _rejoinBlockUntil) ArriveAtArena(mySlot);
            if (phase >= BattleManager.PHASE_JOINING && phase <= BattleManager.PHASE_PLAYBACK && _inBattle && !_seated) TakeSeat();

            if (phase == BattleManager.PHASE_PLANNING && manager.round != _roundSeen)
            {
                _roundSeen = manager.round;
                _playing = false;
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
                if (_inBattle)
                {
                    DrawToFull();
                    _submitted = false;
                    _selHand = -1;
                    _discardMode = false;
                    _note = "Pick a card, or Pass.";
                    if (_slot >= 0 && manager.stunned[_slot] != 0)
                    {
                        _submitted = true;
                        _note = "You are stunned and lose this turn.";
                    }
                }
                SetBanner("Round " + manager.round.ToString() + (manager.firstSide == 0 ? "  -  Wizards act first" : "  -  Monsters act first"));
            }

            if (phase == BattleManager.PHASE_PLAYBACK && _phaseSeen != BattleManager.PHASE_PLAYBACK)
            {
                if (_inBattle) { SpendCastMana(); ReturnFizzledCards(); }
                _playIndex = 0;
                if (!_playing)
                {
                    _playing = true;
                    SendCustomEventDelayedSeconds(nameof(_PlayStep), 0.3f);
                }
            }

            if ((phase == BattleManager.PHASE_VICTORY || phase == BattleManager.PHASE_DEFEAT) && _phaseSeen != phase)
            {
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
                bool won = phase == BattleManager.PHASE_VICTORY;
                // A wizard who fell in the final round goes home defeated even if the party won.
                bool iWon = won && _slot >= 0 && manager.hp[_slot] > 0;
                string msg = won ? "Victory!" : "Defeat...";
                if (_inBattle && iWon)
                {
                    if (playerState != null) playerState.SetHealth(Mathf.Max(1, BattleToCharacterHealth(manager.hp[_slot])));
                    _vitalsDone = true;
                    msg += "\n" + GrantRewards();
                }
                else if (_inBattle)
                {
                    ApplyDefeat();
                    msg += "\nYou were defeated and return home to recover.";
                }
                SetBanner(msg);
                _note = msg;
                if (_inBattle) SendCustomEventDelayedSeconds(nameof(_LeaveBattle), Mathf.Max(2, manager.endSeconds - 1));
            }

            if (phase == BattleManager.PHASE_IDLE && _inBattle) _LeaveBattle();
            if (_inBattle && !_leaving && mySlot < 0 && phase != BattleManager.PHASE_IDLE)
            {
                // Our seat was vacated: with 0 health that means we were defeated mid-fight (Wizard101 sends you home).
                if (_slot >= 0 && manager.hp[_slot] <= 0)
                {
                    ApplyDefeat();
                    _note = "You were defeated!";
                    _leaving = true;
                    if (handRoot != null) handRoot.SetActive(false);
                    SendCustomEventDelayedSeconds(nameof(_LeaveBattle), 1.5f);
                }
                else _LeaveBattle();
            }

            _phaseSeen = phase;
            RefreshLabels();
            RefreshHand();
            UpdateHandVisible();
        }

        /// <summary>The hand panel only shows while you're choosing (hidden while the spells play out, like Wizard101).</summary>
        private void UpdateHandVisible()
        {
            if (handRoot == null) return;
            int ph = manager.phase;
            bool show = _inBattle && _seated && !_leaving && (ph == BattleManager.PHASE_JOINING || ph == BattleManager.PHASE_PLANNING);
            if (handRoot.activeSelf != show) handRoot.SetActive(show);
        }

        /// <summary>Moves the whole encounter (ring, seats, labels, hand) to the fight's ring spot.</summary>
        private void PlaceRing()
        {
            if (encounterRoot == null) return;
            Transform spot = manager.CurrentRingSpot();
            if (spot == null) return;
            if ((encounterRoot.position - spot.position).sqrMagnitude < 0.0001f) return;
            encounterRoot.SetPositionAndRotation(spot.position, spot.rotation);
        }

        private float SeatScale(int s)
        {
            return seatScales != null && s >= 0 && s < seatScales.Length && seatScales[s] > 0f ? seatScales[s] : 1f;
        }

        // ------------------------------------------------------------------ battle enter / leave

        private void ArriveAtArena(int slot)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || playerState == null || !playerState.HasCharacter()) return;
            _inBattle = true;
            _seated = false;
            _leaving = false;
            _defeated = false;
            _vitalsDone = false;
            _slot = slot;
            playerState.inCombat = true;
            if (manager.phase == BattleManager.PHASE_PLANNING || manager.phase == BattleManager.PHASE_PLAYBACK)
            {
                // Walked into a fight in progress: sit out the current round, act from the next one.
                _battleSeen = manager.battleId;
                _roundSeen = manager.round;
                _submitted = true;
                _note = "You join the duel! You'll act next round.";
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
            }
            Transform spot = !manager.wanderMode && stagingSpots != null && slot < stagingSpots.Length && stagingSpots[slot] != null ? stagingSpots[slot] : seats[slot];
            if (culler != null) culler.Prepare(spot.position);
            local.TeleportTo(spot.position, spot.rotation);
        }

        private void TakeSeat()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || playerState == null || !playerState.HasCharacter()) return;
            _seated = true;
            CharacterData c = playerState.current;

            Transform seat = seats[_slot];
            if (culler != null) culler.Prepare(seat.position);
            local.TeleportTo(seat.position, seat.rotation);
            local.Immobilize(true);

            float eye = local.GetAvatarEyeHeightAsMeters();
            if (eye < 0.2f) eye = 1.6f;
            if (handRoot != null)
            {
                Transform h = handRoot.transform;
                h.position = seat.position + seat.forward * 0.75f + Vector3.up * (eye * 0.78f);
                h.rotation = seat.rotation * Quaternion.Euler(25f, 0f, 0f);
                handRoot.SetActive(true);
            }

            BuildPile(c);
            _handCount = 0;
            if (!_submitted || manager.phase == BattleManager.PHASE_JOINING) _note = "Waiting for the other wizards...";
            int sc = (int)c.school;
            // Health carries over between fights (the school health trait is already part of MaxHealth).
            int maxHealth = playerState.MaxHealth();
            int curHealth = Mathf.Max(1, playerState.GetHealth());
            playerState.combatHealth = -1;
            int statsA = manager.PackStats(
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_DAMAGE) + Trait(schoolDamage, sc, 0),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_RESIST) + Trait(schoolResist, sc, 0),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_ACCURACY) + Trait(schoolAccuracy, sc, 0) + 128, 0);
            int statsB = manager.PackStats(
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_CRITICAL),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_BLOCK),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_PIERCE),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_POWERPIP));
            manager.RequestJoin(maxHealth, curHealth, sc, c.level, statsA, statsB);
        }

        /// <summary>In-battle health is on the same scale as the character's health.</summary>
        private int BattleToCharacterHealth(int battleHp) { return battleHp; }

        /// <summary>Feeds the HUD the health the duel is showing, so it drops as each hit lands.</summary>
        private void UpdateHudHealth()
        {
            if (playerState == null || !playerState.HasCharacter() || _slot < 0 || !_seated) return;
            // Until our stats reach the referee the seat holds placeholder health; don't show that.
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || manager.FindWizardSlot(me.playerId) != _slot || manager.maxHp[_slot] != playerState.MaxHealth()) return;
            playerState.combatHealth = _shownHp[_slot];
        }

        /// <summary>Wizard101 sends a defeated wizard home with a little health; nothing else is lost.</summary>
        private void ApplyDefeat()
        {
            _defeated = true;
            if (_vitalsDone || playerState == null || !playerState.HasCharacter()) return;
            _vitalsDone = true;
            playerState.SetHealth(Mathf.Max(1, playerState.MaxHealth() * defeatHealthPct / 100));
            playerState.SaveNow();
        }

        /// <summary>Each pip a spell costs uses 1 mana when it is cast, even if it fizzles (Wizard101 rule).</summary>
        /// <summary>A fizzled card isn't lost: it goes back into the deck (random spot), or to the end of the hand.</summary>
        private void ReturnFizzledCards()
        {
            if (_slot < 0) return;
            for (int i = 0; i < manager.actCount; i++)
            {
                if (manager.actActor[i] != _slot || manager.actKind[i] != BattleManager.KIND_FIZZLE) continue;
                int card = manager.actCard[i];
                if (card <= 0) continue;
                if (fizzleReturnsToHand && _handCount < HAND_SIZE) _hand[_handCount++] = card;
                else if (_pileCount < _pile.Length)
                {
                    // Cards are drawn from the end of the pile, so insert at a random position.
                    int at = Random.Range(0, _pileCount + 1);
                    for (int k = _pileCount; k > at; k--) _pile[k] = _pile[k - 1];
                    _pile[at] = card;
                    _pileCount++;
                }
            }
        }

        private void SpendCastMana()
        {
            if (_slot < 0 || playerState == null || !playerState.HasCharacter()) return;
            int spent = 0;
            for (int i = 0; i < manager.actCount; i++)
            {
                if (i > 0 && manager.actGroup[i] == manager.actGroup[i - 1]) continue;
                if (manager.actActor[i] != _slot) continue;
                int k = manager.actKind[i];
                int card = manager.actCard[i];
                if (card <= 0 || k == BattleManager.KIND_PASS || k == BattleManager.KIND_DOT || k == BattleManager.KIND_STUNNED || k == BattleManager.KIND_POTION) continue;
                spent += cardDatabase.GetPipCost(card);
            }
            if (spent > 0) playerState.SpendMana(spent);
        }

        private int Trait(int[] arr, int school, int fallback)
        {
            return arr != null && school >= 0 && school < arr.Length ? arr[school] : fallback;
        }

        public void _LeaveBattle()
        {
            if (!_inBattle) return;
            if (!_vitalsDone && _slot >= 0 && playerState != null && playerState.HasCharacter())
            {
                // Left some other way (fight reset, timeout): keep whatever health we had.
                if (manager.hp[_slot] > 0) playerState.SetHealth(BattleToCharacterHealth(manager.hp[_slot]));
            }
            _vitalsDone = true;
            _inBattle = false;
            _seated = false;
            _leaving = false;
            _slot = -1;
            _rejoinBlockUntil = Time.time + 5f;
            _nextEngage = Time.time + 5f;
            if (playerState != null) { playerState.inCombat = false; playerState.combatHealth = -1; }
            if (handRoot != null) handRoot.SetActive(false);
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                local.Immobilize(false);
                Transform back = _defeated && defeatReturn != null ? defeatReturn : hubReturn;
                if (back != null)
                {
                    if (culler != null) culler.Prepare(back.position);
                    local.TeleportTo(back.position, back.rotation);
                }
            }
            if (playerState != null && playerState.HasCharacter()) playerState.SaveNow();
        }

        private string GrantRewards()
        {
            if (playerState == null || !playerState.HasCharacter()) return "";
            CharacterData c = playerState.current;
            int xp = 0;
            int gold = 0;
            string drops = "";
            for (int s = BattleManager.SIDE; s < BattleManager.SLOTS; s++)
            {
                int id = manager.MonsterAt(s);
                if (id == 0 || cardDatabase == null) continue;
                xp += cardDatabase.MonsterXp(id);
                gold += cardDatabase.RollMonsterGold(id);
                int item = cardDatabase.MonsterDropItem(id);
                if (item > 0 && Random.Range(0, 100) < cardDatabase.MonsterDropChance(id) && c.AddItem(item, 1))
                    drops += "\nLoot: " + itemDatabase.GetName(item);
            }
            if (xp == 0) { xp = 60 + 25 * manager.MonsterCount(); gold = Random.Range(30, 61); }
            c.gold += gold;
            string msg = "+" + xp.ToString() + " XP   +" + gold.ToString() + " gold" + drops;
            int levels = c.AddXp(xp);
            if (questManager != null) questManager.OnBattleWon(manager);
            if (levels > 0) msg += "\nLevel up! You are now level " + c.level.ToString() + ". +" + levels.ToString() + " Training Point" + (levels > 1 ? "s" : "") + ".";
            if (drops.Length == 0 && lootTable.Length > 0 && Random.value < 0.3f)
            {
                int itemId = lootTable[Random.Range(0, lootTable.Length)];
                if (c.AddItem(itemId, 1)) msg += "\nLoot: " + itemDatabase.GetName(itemId);
            }
            playerState.MarkDirty();
            playerState.SaveNow();
            return msg;
        }

        // ------------------------------------------------------------------ hand

        private void BuildPile(CharacterData c)
        {
            _pileCount = 0;
            // Battles draw from the equipped deck item's card list.
            int list = c.ActiveDeckList();
            int len = list >= 0 ? c.DeckCount(list) : 0;
            for (int i = 0; i < len && _pileCount < _pile.Length; i++)
            {
                int id = c.deckCards[list * CharacterData.DECK_SIZE + i];
                if (cardDatabase.IndexOf(id) >= 0) _pile[_pileCount++] = id;
            }
            // Item cards (Wizard101): spells from equipped gear join the deck for the fight, whatever your school.
            if (itemDatabase != null && itemDatabase.itemCardItems != null && itemDatabase.itemCardIds != null)
                for (int s = 0; s < CharacterData.EQUIP_SLOTS; s++)
                {
                    int item = c.equipped[s];
                    if (item == 0) continue;
                    for (int k = 0; k < itemDatabase.itemCardItems.Length && k < itemDatabase.itemCardIds.Length && _pileCount < _pile.Length; k++)
                    {
                        int card = itemDatabase.itemCardIds[k];
                        if (itemDatabase.itemCardItems[k] == item && cardDatabase.IndexOf(card) >= 0) _pile[_pileCount++] = card;
                    }
                }
            if (_pileCount == 0)
            {
                int basic = cardDatabase.BasicSpell((int)c.school);
                for (int i = 0; i < 4; i++) _pile[_pileCount++] = basic;
            }
            for (int i = _pileCount - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int t = _pile[i]; _pile[i] = _pile[j]; _pile[j] = t;
            }
        }

        private void DrawToFull()
        {
            while (_handCount < HAND_SIZE && _pileCount > 0) _hand[_handCount++] = _pile[--_pileCount];
        }

        private void RemoveFromHand(int i)
        {
            SetHover(-1);
            for (int k = i; k < _handCount - 1; k++) _hand[k] = _hand[k + 1];
            _handCount--;
        }

        private bool CanAct()
        {
            return _inBattle && !_submitted && manager.phase == BattleManager.PHASE_PLANNING && manager.Alive(_slot);
        }

        private void ClickCard(int i)
        {
            if (!CanAct() || i >= _handCount) return;
            if (uiAudio != null) uiAudio.PlayCardClick();
            int card = _hand[i];
            if (_discardMode)
            {
                RemoveFromHand(i);
                _note = "Discarded " + cardDatabase.GetName(card) + ".";
                RefreshHand();
                return;
            }
            if (!manager.CanAfford(_slot, card))
            {
                _note = "Not enough pips for " + cardDatabase.GetName(card) + ".";
                RefreshHand();
                return;
            }
            if (!HasManaFor(card))
            {
                _note = "Not enough mana for " + cardDatabase.GetName(card) + ". Drink a potion or flee.";
                RefreshHand();
                return;
            }
            if (cardDatabase.IsAoe(card))
            {
                RemoveFromHand(i);
                Submit(card, -1, "Casting " + cardDatabase.GetName(card) + ".");
                return;
            }
            _selHand = i;
            _note = cardDatabase.GetName(card) + ": pick a " + (cardDatabase.TargetsAllies(card) ? "wizard." : "monster.");
            RefreshHand();
        }

        /// <summary>Spells cost 1 mana per pip (0-pip spells are free).</summary>
        private bool HasManaFor(int card)
        {
            return playerState == null || !playerState.HasCharacter() || cardDatabase.GetPipCost(card) <= playerState.GetMana();
        }

        private void ClickTarget(int target)
        {
            if (!CanAct() || _selHand < 0 || _selHand >= _handCount) return;
            int card = _hand[_selHand];
            RemoveFromHand(_selHand);
            Submit(card, target, "Casting " + cardDatabase.GetName(card) + " on " + manager.SlotName(target) + ".");
        }

        private void Submit(int card, int target, string note)
        {
            manager.RequestPick(card, target);
            if (uiAudio != null && card > 0) uiAudio.PlaySpellSelect();
            _submitted = true;
            _selHand = -1;
            _discardMode = false;
            _note = note + " Waiting for the round...";
            RefreshHand();
        }

        // ------------------------------------------------------------------ playback

        public void _PlayStep()
        {
            if (manager.phase != BattleManager.PHASE_PLAYBACK || _playIndex >= manager.actCount)
            {
                FinishPlayback();
                return;
            }

            _stepStart = _playIndex;
            int group = manager.actGroup[_playIndex];
            int actor = manager.actActor[_playIndex];
            _clockActor = actor;
            int card = manager.actCard[_playIndex];
            int kind = manager.actKind[_playIndex];
            int firstTargetSlot = manager.actTarget[_playIndex];
            if (actor >= 0 && actor < BattleManager.SLOTS)
            {
                _shownPips[actor] = manager.pips[actor];
                _shownPower[actor] = manager.powerPips[actor];
                UpdatePips();
            }
            string text = manager.SlotName(actor);
            if (kind == BattleManager.KIND_PASS) text += " passes.";
            else if (kind == BattleManager.KIND_FIZZLE) text += "'s " + cardDatabase.GetName(card) + " fizzles!";
            else if (kind == BattleManager.KIND_DOT) text += " burns for " + manager.actValue[_playIndex].ToString() + " damage!";
            else if (kind == BattleManager.KIND_STUNNED) text += " is stunned and loses a turn!";
            else if (kind == BattleManager.KIND_POTION)
                text += " drinks a potion" + (manager.actValue[_playIndex] > 0 ? " and recovers " + manager.actValue[_playIndex].ToString() + " health!" : ".");
            else if (kind == BattleManager.KIND_SUMMON)
                text += firstTargetSlot >= 0 ? " summons " + cardDatabase.GetName(card) + "!" : " tries to summon, but there's no free seat!";
            else text += " casts " + cardDatabase.GetName(card) + "!";
            _stepHeader = text;
            while (_playIndex < manager.actCount && manager.actGroup[_playIndex] == group) _playIndex++;
            _stepEnd = _playIndex;

            if (!manager.IsCastAct(_stepStart))
            {
                if (kind == BattleManager.KIND_SUMMON && firstTargetSlot >= 0) StartSummon(firstTargetSlot);
                ApplyStep();
                SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.actionSeconds);
                return;
            }

            // Wind-up first (cast sound, ring, glyph, burst at the sound's peak); the effect lands when it finishes.
            SetBanner(text);
            RefreshLabels();
            int target = FirstTarget(actor);
            GameObject anim = cardDatabase.GetAnimation(card);
            if (anim != null)
            {
                PlaySpellAnimation(anim, actor, target);
                ApplyStep();
                SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.StepSeconds(_stepStart));
                return;
            }
            int school = cardDatabase.GetSchool(card);
            StartCast(actor, target, school);
            SendCustomEventDelayedSeconds(nameof(_LandStep), manager.CastSeconds(school));
        }

        /// <summary>End of the wind-up: the orb flies, the effect lands, then the next caster goes.</summary>
        public void _LandStep()
        {
            if (manager.phase != BattleManager.PHASE_PLAYBACK) { FinishPlayback(); return; }
            StartImpact();
            ApplyStep();
            SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.impactSeconds);
        }

        private void FinishPlayback()
        {
            _playing = false;
            for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
            SyncShownState();
            UpdateFx();
            UpdatePips();
            EndCast();
            _HideSpellAnimation();
            RefreshBodies(manager.phase);
            RefreshLabels();
        }

        /// <summary>Shows the current step's results (damage, heals, buffs) and updates the shown health.</summary>
        private void ApplyStep()
        {
            string text = _stepHeader;
            for (int s = 0; s < BattleManager.SLOTS; s++) _stack[s] = 0;
            for (int i = _stepStart; i < _stepEnd && i < manager.actCount; i++)
            {
                int t = manager.actTarget[i];
                int k = manager.actKind[i];
                int v = manager.actValue[i];
                int f = manager.actFlags[i];
                if (k == BattleManager.KIND_DAMAGE) text += "\n" + manager.SlotName(t) + " takes " + v.ToString() + " damage" + FlagText(f);
                else if (k == BattleManager.KIND_HEAL) text += "\n" + manager.SlotName(t) + " heals " + v.ToString() + FlagText(f);
                else if (k == BattleManager.KIND_BUFF) text += "\n" + manager.SlotName(t) + BuffText(manager.actCard[i], v);
                // Shields / blades / traps this hit used up break first, then the result floats up.
                BreakLoggedFx(i);
                FloatAct(i);
                AddShownFx(i);
                int before = t >= 0 && t < BattleManager.SLOTS ? _shownHp[t] : 0;
                if (t >= 0 && t < BattleManager.SLOTS) _shownHp[t] = manager.actHpAfter[i];
                // The hit that defeats a monster has landed: only now does it start to vanish.
                if (before > 0 && t >= 0 && t < BattleManager.SLOTS && _shownHp[t] <= 0) StartDefeat(t);
                ActSound(i, before);
            }
            SetBanner(text);
            RefreshLabels();
            UpdateFx();
        }

        private string FlagText(int f)
        {
            string t = "";
            if ((f & BattleManager.FLAG_CRIT) != 0) t += "  CRITICAL!";
            if ((f & BattleManager.FLAG_BLOCKED) != 0) t += "  (critical blocked)";
            if ((f & BattleManager.FLAG_WEAK) != 0) t += "  (weak to it!)";
            if ((f & BattleManager.FLAG_RESIST) != 0) t += "  (resisted)";
            return t;
        }

        private int FirstTarget(int actor)
        {
            for (int i = _stepStart; i < _stepEnd && i < manager.actCount; i++)
            {
                int k = manager.actKind[i];
                int t = manager.actTarget[i];
                if ((k == BattleManager.KIND_DAMAGE || k == BattleManager.KIND_BUFF) && t != actor) return t;
            }
            return actor;
        }

        private string BuffText(int card, int v)
        {
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_DOT) return " is set ablaze (" + v.ToString() + " burn damage over " + CardDatabase.DOT_ROUNDS.ToString() + " rounds)";
            if (e == CardDatabase.EFFECT_DISPEL) return " loses every shield and blade";
            if (e == CardDatabase.EFFECT_TAUNT) return " taunts the monsters and gains a " + v.ToString() + "% shield";
            return " gains " + BuffName(card) + " (" + v.ToString() + "%)";
        }

        private string BuffName(int card)
        {
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_WARD) return "a shield";
            if (e == CardDatabase.EFFECT_EMPOWER) return "a blade";
            if (e == CardDatabase.EFFECT_TRIBLADE) return "3 blades";
            if (e == CardDatabase.EFFECT_TRAP) return "a trap";
            return "Focus";
        }

        private void PlaySpellAnimation(GameObject anim, int from, int to)
        {
            _HideSpellAnimation();
            if (seats == null || from < 0) return;
            Vector3 pos = seats[from].position;
            Vector3 aim = to >= 0 && to != from ? seats[to].position : (arenaCenter != null ? arenaCenter.position : pos + seats[from].forward);
            Vector3 dir = aim - pos;
            dir.y = 0f;
            anim.transform.position = pos;
            if (dir.sqrMagnitude > 0.001f) anim.transform.rotation = Quaternion.LookRotation(dir);
            anim.SetActive(true);
            _spellAnim = anim;
        }

        public void _HideSpellAnimation()
        {
            if (_spellAnim != null) _spellAnim.SetActive(false);
            _spellAnim = null;
        }

        /// <summary>
        /// School cast effect. Wind-up (the cast sound's length): the caster's seat ring flares and rises and the school glyph
        /// floats over their head; at the sound's peak a school-coloured particle burst explodes off the caster. Impact
        /// (<see cref="BattleManager.impactSeconds"/>): the orb flies to the target, then bursts on it.
        /// </summary>
        private void StartCast(int from, int to, int school)
        {
            if (seats == null || from < 0 || from >= seats.Length) return;
            EndCast();
            if (to < 0 || to >= seats.Length) to = from;
            _casting = true;
            _impacting = false;
            _burstDone = false;
            _castFrom = from;
            _castTo = to;
            _castSchool = school;
            _orbStart = Time.time;
            _castLen = manager.CastSeconds(school);
            _castPeak = manager.CastPeakSeconds(school);
            int mi = fxMats == null || fxMats.Length == 0 ? -1 : (school >= 0 && school < fxMats.Length - 1 ? school : fxMats.Length - 1);
            float scale = SeatScale(from);
            _ringBase = seats[from].position + Vector3.up * 0.15f;
            _glyphBase = seats[from].position + Vector3.up * (2.6f * scale);
            _orbFrom = seats[from].position + Vector3.up * (1.3f * scale);
            _orbTo = seats[to].position + Vector3.up * (1.1f * SeatScale(to));
            if (castRing != null)
            {
                if (castRingRenderer != null && seatSchoolMats != null && school >= 0 && school < seatSchoolMats.Length)
                    castRingRenderer.sharedMaterial = seatSchoolMats[school];
                castRing.position = _ringBase;
                castRing.gameObject.SetActive(true);
            }
            if (castGlyph != null && castGlyphMats != null && castGlyphMats.Length > 0)
            {
                int gi = school >= 0 && school < castGlyphMats.Length - 1 ? school : castGlyphMats.Length - 1;
                castGlyph.sharedMaterial = castGlyphMats[gi];
                castGlyph.transform.position = _glyphBase;
                castGlyph.transform.localScale = Vector3.zero;
                castGlyph.gameObject.SetActive(true);
            }
            if (mi >= 0)
            {
                if (orbRenderer != null) orbRenderer.sharedMaterial = fxMats[mi];
                if (impactRenderer != null) impactRenderer.sharedMaterial = fxMats[mi];
            }
            if (orb != null) orb.gameObject.SetActive(false);
            if (impactBurst != null) impactBurst.gameObject.SetActive(false);
            PlayCastSound(from, school);
        }

        private void PlayCastSound(int from, int school)
        {
            if (castAudio == null) return;
            castAudio.Stop();
            if (castSounds == null || school < 0 || school >= castSounds.Length || castSounds[school] == null) return;
            castAudio.transform.position = seats[from].position + Vector3.up * 1.2f;
            castAudio.clip = castSounds[school];
            castAudio.Play();
        }

        private void StartImpact()
        {
            if (!_casting) return;
            if (!_burstDone) CastBurst();
            _impacting = true;
            _impactStart = Time.time;
            if (castRing != null) castRing.gameObject.SetActive(false);
            if (castGlyph != null) castGlyph.gameObject.SetActive(false);
        }

        /// <summary>School-coloured particle explosion off the caster.</summary>
        private void CastBurst()
        {
            _burstDone = true;
            if (castBursts == null || castBursts.Length == 0) return;
            int i = _castSchool >= 0 && _castSchool < castBursts.Length - 1 ? _castSchool : castBursts.Length - 1;
            ParticleSystem ps = castBursts[i];
            if (ps == null) return;
            float scale = SeatScale(_castFrom);
            ps.transform.position = seats[_castFrom].position + Vector3.up * (1.2f * scale);
            ps.Play();
        }

        private void EndCast()
        {
            _casting = false;
            _impacting = false;
            if (orb != null) orb.gameObject.SetActive(false);
            if (castRing != null) castRing.gameObject.SetActive(false);
            if (castGlyph != null) castGlyph.gameObject.SetActive(false);
            if (impactBurst != null) impactBurst.gameObject.SetActive(false);
        }

        private void AnimateCast()
        {
            if (!_casting) return;
            if (_impacting) { AnimateImpact(); return; }

            float elapsed = Time.time - _orbStart;
            if (!_burstDone && elapsed >= _castPeak) CastBurst();
            float w = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, _castLen));
            if (castRing != null)
            {
                castRing.position = _ringBase + Vector3.up * (w * 1.6f);
                float rs = 1.9f + Mathf.Min(1f, w * 2f) * 1.2f;
                castRing.localScale = new Vector3(rs, rs, 1f);
                castRing.rotation = Quaternion.Euler(90f, Time.time * 160f, 0f);
            }
            if (castGlyph != null)
            {
                // Pop in with a little overshoot, hold, and shrink away just before the effect lands.
                float pop = Mathf.Min(0.35f, _castLen * 0.2f);
                float size = elapsed < pop ? Mathf.Lerp(0f, 1.25f, elapsed / pop)
                    : (elapsed < pop * 1.5f ? Mathf.Lerp(1.25f, 1f, (elapsed - pop) / (pop * 0.5f))
                    : (w < 0.9f ? 1f : Mathf.Lerp(1f, 0f, (w - 0.9f) / 0.1f)));
                Transform gt = castGlyph.transform;
                gt.position = _glyphBase + Vector3.up * (w * 0.4f);
                gt.localScale = Vector3.one * (size * 1.1f);
                VRCPlayerApi me = Networking.LocalPlayer;
                Vector3 eye = Utilities.IsValid(me) ? me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position : gt.position - Vector3.forward;
                Vector3 look = gt.position - eye;
                if (look.sqrMagnitude > 0.001f) gt.rotation = Quaternion.LookRotation(look) * Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * Mathf.PI) * 8f);
            }
        }

        private void AnimateImpact()
        {
            float t = (Time.time - _impactStart) / Mathf.Max(0.1f, manager.impactSeconds);
            if (t >= 0.95f) { EndCast(); return; }
            if (orb != null)
            {
                bool on = t < 0.6f;
                if (orb.gameObject.activeSelf != on) orb.gameObject.SetActive(on);
                if (on)
                {
                    float o = t / 0.6f;
                    float arc = _castTo == _castFrom ? 1.4f : 0.8f;
                    orb.position = Vector3.Lerp(_orbFrom, _orbTo, o) + Vector3.up * Mathf.Sin(o * Mathf.PI) * arc;
                }
            }
            if (impactBurst != null)
            {
                bool on = t >= 0.6f;
                if (impactBurst.gameObject.activeSelf != on) impactBurst.gameObject.SetActive(on);
                if (on)
                {
                    float b = (t - 0.6f) / 0.35f;
                    impactBurst.position = _orbTo;
                    impactBurst.localScale = Vector3.one * (b < 0.5f ? Mathf.Lerp(0.3f, 1.6f, b * 2f) : Mathf.Lerp(1.6f, 0f, (b - 0.5f) * 2f));
                }
            }
        }

        private void UpdateFx()
        {
            if (fxIcons == null || fxOrbits == null || fxMats == null || fxMats.Length == 0) return;
            int max = BattleManager.FX_MAX;
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                for (int kind = 0; kind < 3; kind++)
                {
                    int[] arr = kind == 0 ? _fxSh : (kind == 1 ? _fxBl : _fxTr);
                    int n = manager.Occupied(s) ? ShownCount(arr, s) : 0;
                    for (int k = 0; k < max; k++)
                    {
                        int idx = (s * 3 + kind) * max + k;
                        if (idx >= fxIcons.Length || fxIcons[idx] == null) continue;
                        Renderer r = fxIcons[idx];
                        bool on = k < n;
                        if (r.gameObject.activeSelf != on) r.gameObject.SetActive(on);
                        if (!on) continue;
                        int f = manager.FxSchool(arr[s * max + k]);
                        int mi = f < 0 || f >= fxMats.Length - 1 ? fxMats.Length - 1 : f;
                        Material want = IconMat(fxIconMats, kind, f);
                        if (want == null) want = fxMats[mi];
                        if (r.sharedMaterial != want) r.sharedMaterial = want;
                        // Stacked effects sit side by side (the newest next to the last), and the group circles the combatant.
                        float ang = (k - (n - 1) * 0.5f) * _fxStackDeg[kind];
                        Quaternion around = Quaternion.Euler(0f, ang, 0f);
                        r.transform.localPosition = around * Vector3.forward * _fxRadius[kind];
                    }
                }
            }
        }

        private void SpinFx()
        {
            if (fxOrbits == null) return;
            for (int i = 0; i < fxOrbits.Length; i++)
                if (fxOrbits[i] != null) fxOrbits[i].localRotation = Quaternion.Euler(0f, Time.time * _fxSpin[i % 3], 0f);
            // The icons circle the combatant but always face whoever is watching (like Wizard101's).
            if (fxIcons == null) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            for (int i = 0; i < fxIcons.Length; i++)
            {
                Renderer r = fxIcons[i];
                if (r == null || !r.gameObject.activeSelf) continue;
                Vector3 look = r.transform.position - head;
                if (look.sqrMagnitude > 0.0001f) r.transform.rotation = Quaternion.LookRotation(look);
            }
        }

        /// <summary>Icon material for an effect kind and school (-1 / out of range = General), or null.</summary>
        private Material IconMat(Material[] mats, int kind, int school)
        {
            int i = FxIconIndex(kind, school);
            return mats != null && i < mats.Length ? mats[i] : null;
        }

        private int FxIconIndex(int kind, int school)
        {
            return kind * 8 + (school >= 0 && school < 7 ? school : 7);
        }

        // ------------------------------------------------------------------ bodies and defeat

        /// <summary>
        /// Shows or hides each monster's body. The synced health is already the end-of-round result while the round
        /// plays back, so a monster defeated this round stays until the hit that defeats it is shown (and then
        /// squashes away, see <see cref="AnimateDefeats"/>).
        /// </summary>
        private void RefreshBodies(int phase)
        {
            UpdateMinions(phase);
            if (monsterBodies == null) return;
            for (int m = 0; m < BattleManager.SIDE && m < monsterBodies.Length; m++)
            {
                if (monsterBodies[m] == null) continue;
                int s = BattleManager.SIDE + m;
                // Each monster seat's body is built from the encounter's roster (seat m = roster[m]), so it always
                // shows the monster actually sitting there.
                bool show = phase >= BattleManager.PHASE_ROAMING && !manager.IsSummon(s) && KeepBody(s);
                if (monsterBodies[m].activeSelf != show) monsterBodies[m].SetActive(show);
            }
        }

        private bool KeepBody(int s)
        {
            if (!manager.Occupied(s)) return false;
            if (manager.hp[s] > 0 || _dieStart[s] > 0f) return true;
            bool playback = _playing || manager.phase == BattleManager.PHASE_PLAYBACK;
            return playback && _shownHp[s] > 0;
        }

        private Transform BodyOf(int s)
        {
            if (s < 0 || s >= BattleManager.SLOTS) return null;
            if (manager.IsSummon(s)) return minionBodies != null && s < minionBodies.Length ? minionBodies[s] : null;
            int m = s - BattleManager.SIDE;
            if (m < 0 || monsterBodies == null || m >= monsterBodies.Length || monsterBodies[m] == null) return null;
            return monsterBodies[m].transform;
        }

        private Vector3 BodyBaseScale(int s)
        {
            if (manager.IsSummon(s)) return Vector3.one;
            if (_bodyBase == null && monsterBodies != null)
            {
                // Captured before any defeat changed them (the builder sizes each seat's body to its monster).
                _bodyBase = new Vector3[monsterBodies.Length];
                for (int i = 0; i < monsterBodies.Length; i++)
                    _bodyBase[i] = monsterBodies[i] != null ? monsterBodies[i].transform.localScale : Vector3.one;
            }
            int m = s - BattleManager.SIDE;
            return _bodyBase != null && m >= 0 && m < _bodyBase.Length ? _bodyBase[m] : Vector3.one;
        }

        private void StartDefeat(int s)
        {
            Transform body = BodyOf(s);
            if (body == null || !body.gameObject.activeSelf) return;
            BodyBaseScale(s);
            _dieStart[s] = Time.time;
        }

        /// <summary>A defeated monster holds for a moment after the hit, then squashes flat into the floor in PSX steps.</summary>
        private void AnimateDefeats()
        {
            if (manager == null) return;
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                if (_dieStart[s] <= 0f) continue;
                Transform body = BodyOf(s);
                if (body == null) { _dieStart[s] = 0f; continue; }
                float t = Time.time - _dieStart[s] - defeatDelay;
                if (t < 0f) continue;
                Vector3 b = BodyBaseScale(s);
                float k = Mathf.Floor(Mathf.Clamp01(t / Mathf.Max(0.05f, defeatFadeSeconds)) * 8f) / 8f;
                if (k >= 1f)
                {
                    _dieStart[s] = 0f;
                    body.gameObject.SetActive(false);
                    body.localScale = b;
                    continue;
                }
                body.localScale = new Vector3(b.x * (1f + 0.35f * k), b.y * (1f - k), b.z * (1f + 0.35f * k));
            }
        }

        /// <summary>New fight: stop any defeat in progress and put the bodies back to their normal size.</summary>
        private void ClearDefeats()
        {
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                if (_dieStart[s] <= 0f) continue;
                _dieStart[s] = 0f;
                Transform body = BodyOf(s);
                if (body != null) body.localScale = BodyBaseScale(s);
            }
        }

        // ------------------------------------------------------------------ summoned minions

        private void UpdateMinions(int phase)
        {
            if (minionBodies == null) return;
            bool active = phase >= BattleManager.PHASE_JOINING;
            for (int s = 0; s < BattleManager.SLOTS && s < minionBodies.Length; s++)
            {
                Transform body = minionBodies[s];
                if (body == null) continue;
                bool want = active && manager.IsSummon(s) && KeepBody(s);
                if (!want)
                {
                    _minionShown[s] = 0;
                    if (_walkSlot == s) EndWalk();
                    if (body.gameObject.activeSelf) body.gameObject.SetActive(false);
                    continue;
                }
                // During playback a new minion stays hidden until its summon step plays (StartSummon).
                if (_minionShown[s] != manager.slotIds[s] && phase != BattleManager.PHASE_PLAYBACK) ShowMinionAtSeat(s);
            }
        }

        private void ColorMinion(int s)
        {
            if (minionSkins == null || fxMats == null || fxMats.Length == 0) return;
            int sc = cardDatabase.GetSchool(manager.SummonCard(s));
            Material m = fxMats[sc >= 0 && sc < fxMats.Length - 1 ? sc : fxMats.Length - 1];
            for (int p = 0; p < 2; p++)
            {
                int i = s * 2 + p;
                if (i < minionSkins.Length && minionSkins[i] != null) minionSkins[i].sharedMaterial = m;
            }
        }

        private void ShowMinionAtSeat(int s)
        {
            Transform body = minionBodies[s];
            _minionShown[s] = manager.slotIds[s];
            ColorMinion(s);
            body.SetPositionAndRotation(seats[s].position, seats[s].rotation);
            body.localScale = Vector3.one;
            body.gameObject.SetActive(true);
        }

        /// <summary>The minion appears at the ring's centre in a flash, then walks to its seat.</summary>
        private void StartSummon(int s)
        {
            if (minionBodies == null || s < 0 || s >= minionBodies.Length || minionBodies[s] == null || arenaCenter == null) return;
            if (_walkSlot >= 0) EndWalk();
            Transform body = minionBodies[s];
            _minionShown[s] = manager.slotIds[s];
            ColorMinion(s);
            body.position = arenaCenter.position;
            body.localScale = Vector3.zero;
            body.gameObject.SetActive(true);
            _walkSlot = s;
            _walkStart = Time.time;
            if (summonFlash != null)
            {
                summonFlash.position = arenaCenter.position + Vector3.up * 1f;
                summonFlash.gameObject.SetActive(true);
            }
        }

        private void EndWalk()
        {
            int s = _walkSlot;
            _walkSlot = -1;
            if (summonFlash != null) summonFlash.gameObject.SetActive(false);
            if (s >= 0 && minionBodies[s] != null && minionBodies[s].gameObject.activeSelf)
            {
                minionBodies[s].SetPositionAndRotation(seats[s].position, seats[s].rotation);
                minionBodies[s].localScale = Vector3.one;
            }
        }

        private void AnimateSummon()
        {
            if (_walkSlot < 0) return;
            Transform body = minionBodies[_walkSlot];
            float dur = manager.actionSeconds * 0.95f;
            float t = (Time.time - _walkStart) / dur;
            if (t >= 1f || body == null) { EndWalk(); return; }
            Vector3 from = arenaCenter.position;
            Vector3 to = seats[_walkSlot].position;
            float grow = 0.35f;
            if (t < grow)
            {
                float g = t / grow;
                body.position = from;
                body.localScale = Vector3.one * g;
                body.rotation = Quaternion.Euler(0f, g * 720f, 0f);
                if (summonFlash != null) summonFlash.localScale = Vector3.one * (0.5f + 2.5f * g);
                return;
            }
            if (summonFlash != null && summonFlash.gameObject.activeSelf) summonFlash.gameObject.SetActive(false);
            float w = (t - grow) / (1f - grow);
            Vector3 dir = to - from;
            dir.y = 0f;
            body.localScale = Vector3.one;
            body.position = Vector3.Lerp(from, to, w) + Vector3.up * Mathf.Abs(Mathf.Sin(w * Mathf.PI * 6f)) * 0.12f;
            if (dir.sqrMagnitude > 0.001f) body.rotation = Quaternion.LookRotation(dir);
        }

        private void UpdateSeatRings()
        {
            if (seatRings == null) return;
            for (int s = 0; s < seatRings.Length && s < BattleManager.SLOTS; s++)
            {
                Renderer r = seatRings[s];
                if (r == null) continue;
                int sc = manager.school[s];
                Material empty = s >= BattleManager.SIDE && seatEmptyMonsterMat != null ? seatEmptyMonsterMat : seatEmptyMat;
                Material m = manager.slotIds[s] != 0 && seatSchoolMats != null && sc >= 0 && sc < seatSchoolMats.Length
                    ? seatSchoolMats[sc] : empty;
                if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        private void UpdateClock()
        {
            if (clockHand == null || arenaCenter == null) return;
            int phase = manager.phase;
            float target;
            string label;
            if (phase == BattleManager.PHASE_PLANNING)
            {
                float left = Mathf.Clamp01((manager.phaseEndMs - manager.Now()) / (manager.planningSeconds * 1000f));
                target = 360f * (1f - left);
                label = "Choose your spells\n" + manager.SecondsLeft().ToString() + "s";
            }
            else if (phase == BattleManager.PHASE_PLAYBACK && _clockActor >= 0 && _clockActor < seats.Length)
            {
                Vector3 d = arenaCenter.InverseTransformPoint(seats[_clockActor].position);
                target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                label = manager.SlotName(_clockActor) + "'s turn";
            }
            else
            {
                target = 0f;
                label = phase == BattleManager.PHASE_JOINING ? "Battle!" : "";
            }
            Quaternion want = Quaternion.Euler(0f, target, 0f);
            clockHand.localRotation = phase == BattleManager.PHASE_PLANNING ? want : Quaternion.RotateTowards(clockHand.localRotation, want, 540f * Time.deltaTime);
            if (clockLabel != null) clockLabel.text = label;
        }

        private void Update()
        {
            if (manager != null && manager.phase >= BattleManager.PHASE_JOINING) { UpdateClock(); SpinFx(); AnimateSummon(); AnimateCircle(); AnimatePips(); }
            AnimateFlash();
            AnimateDefeats();
            AnimatePops();
            AnimateFloaters();
            if (_inBattle && manager != null) { UpdateHudHealth(); AnimateCards(); }
            if (_inBattle && !_seated && manager != null && manager.phase == BattleManager.PHASE_ROAMING && arenaCenter != null && Time.time >= _nextEngage)
            {
                VRCPlayerApi me = Networking.LocalPlayer;
                Vector3 p = me.GetPosition() - arenaCenter.position;
                p.y = 0f;
                if (p.magnitude <= engageRadius)
                {
                    _nextEngage = Time.time + 1f;
                    manager.RequestEngage();
                }
            }
            // Wandering-monster lane: like Wizard101, anyone who walks into the circle joins the fight if a seat is free,
            // during the join window or in any later round.
            if (manager != null && manager.wanderMode && manager.phase >= BattleManager.PHASE_JOINING && manager.phase <= BattleManager.PHASE_PLAYBACK &&
                manager.HasFreeWizardSeat() && CanFight() && arenaCenter != null && Time.time >= _nextEngage)
            {
                VRCPlayerApi me = Networking.LocalPlayer;
                Vector3 p = me.GetPosition() - arenaCenter.position;
                p.y = 0f;
                if (p.magnitude <= engageRadius)
                {
                    _nextEngage = Time.time + 1f;
                    manager.RequestEngageWander(-1);
                }
            }
            if (manager != null) AnimateCast();
            if (manager == null) return;
            if (padLabel != null)
            {
                int phase = manager.phase;
                if (!QueueUnlocked()) padLabel.text = queueLockedText;
                else if (phase == BattleManager.PHASE_IDLE) padLabel.text = "Boss Arena Teleporter\nStep on the pad (up to 4 wizards)";
                else if (phase == BattleManager.PHASE_QUEUE)
                    padLabel.text = "Teleporting in " + manager.SecondsLeft().ToString() + "s\nWizards: " + manager.QueueCount().ToString() + " / 4";
                else padLabel.text = "Battle in progress\nPlease wait";
            }
            if (_inBattle && statusLabel != null) statusLabel.text = StatusText();
        }

        // ------------------------------------------------------------------ drawing

        private string StatusText()
        {
            string s = "Round " + manager.round.ToString();
            if (_slot >= 0) s += "    " + PipGlyphs(_slot);
            if (playerState != null && playerState.HasCharacter()) s += "    Mana " + playerState.GetMana().ToString() + " / " + playerState.MaxMana().ToString();
            if (manager.phase == BattleManager.PHASE_PLANNING) s += "    Time " + manager.SecondsLeft().ToString() + "s";
            s += "    Deck " + _pileCount.ToString();
            if (_note.Length > 0) s += "\n" + _note;
            return s;
        }

        /// <summary>Pips as Wizard101-style dots: white pips, then power pips, then empty slots.</summary>
        private string PipGlyphs(int s)
        {
            string g = "";
            for (int i = 0; i < manager.pips[s]; i++) g += "\u25CF";
            for (int i = 0; i < manager.powerPips[s]; i++) g += "\u25C6";
            for (int i = manager.pips[s] + manager.powerPips[s]; i < BattleManager.MAX_PIPS; i++) g += "\u25CB";
            return "Pips " + g + "  (" + manager.PipText(s) + ")";
        }

        private void RefreshLabels()
        {
            if (manager == null) return;
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                if (overheadLabels == null || s >= overheadLabels.Length || overheadLabels[s] == null) continue;
                overheadLabels[s].text = manager.Occupied(s) && manager.phase >= (s < BattleManager.SIDE ? BattleManager.PHASE_JOINING : BattleManager.PHASE_ROAMING) ? SlotLine(s, false) : "";
            }
            if (partyLabel != null)
            {
                string w = "WIZARDS";
                string m = "MONSTERS";
                for (int i = 0; i < BattleManager.SIDE; i++)
                {
                    if (manager.Occupied(i)) w += "\n" + SlotLine(i, true);
                    if (manager.Occupied(BattleManager.SIDE + i)) m += "\n" + SlotLine(BattleManager.SIDE + i, true);
                }
                partyLabel.text = w + "\n\n" + m;
            }
        }

        private string SlotLine(int s, bool compact)
        {
            string line = manager.SlotName(s) + (compact ? "  " : "\n") + _shownHp[s].ToString() + " / " + manager.maxHp[s].ToString() + " HP";
            line += "  " + manager.PipText(s);
            if (!compact && manager.IsCreature(s)) line += "\n" + manager.MatchupText(s);
            int sh = manager.FxCount(manager.shields, s), bl = manager.FxCount(manager.blades, s), tr = manager.FxCount(manager.traps, s);
            if (sh > 0) line += "  Shields " + sh.ToString();
            if (bl > 0) line += "  Blades " + bl.ToString();
            if (tr > 0) line += "  Traps " + tr.ToString();
            if (manager.dotLeft[s] > 0) line += "  Burning " + manager.dotLeft[s].ToString();
            if (manager.stunned[s] != 0) line += "  Stunned";
            if (manager.taunt[s] > 0) line += "  Taunting";
            if (s < BattleManager.SIDE && manager.phase == BattleManager.PHASE_PLANNING && manager.Alive(s)) line += manager.locked[s] != 0 ? "  (ready)" : "  (choosing)";
            if (_shownHp[s] <= 0) line += "  (defeated)";
            return line;
        }

        private void RefreshHand()
        {
            if (!_inBattle || manager == null) return;
            for (int i = 0; i < HAND_SIZE; i++)
            {
                bool has = i < _handCount;
                if (cardImages[i] != null) cardImages[i].gameObject.SetActive(has);
                if (!has) continue;
                int card = _hand[i];
                bool usable = manager.CanAfford(_slot, card) && HasManaFor(card);
                int sc = cardDatabase.GetSchool(card);
                Sprite frame = cardFrames != null && cardFrames.Length > 0 ? cardFrames[sc >= 0 && sc < 7 && sc < cardFrames.Length ? sc : cardFrames.Length - 1] : null;
                Color col;
                if (frame != null)
                {
                    // The frame art carries the school colour; tint only for state.
                    if (cardImages[i].sprite != frame) cardImages[i].sprite = frame;
                    col = Color.white;
                    if (!usable && !_discardMode) col = new Color(0.45f, 0.45f, 0.48f);
                    if (i == _selHand) col = new Color(1f, 1f, 0.72f);
                }
                else
                {
                    col = cardDatabase.GetColor(card);
                    if (!usable && !_discardMode) col = Color.Lerp(col, _cardDim, 0.7f);
                    if (i == _selHand) col = Color.Lerp(col, Color.white, 0.55f);
                }
                if (_discardMode) col = Color.Lerp(col, new Color(0.9f, 0.5f, 0.2f), 0.35f);
                cardImages[i].color = col;
                if (cardArts != null && i < cardArts.Length && cardArts[i] != null)
                {
                    Texture art = CardArt(card);
                    cardArts[i].texture = art;
                    cardArts[i].color = usable || _discardMode ? Color.white : new Color(0.5f, 0.5f, 0.52f);
                    if (cardArts[i].gameObject.activeSelf != (art != null)) cardArts[i].gameObject.SetActive(art != null);
                }
                if (cardFaces != null && i < cardFaces.Length && cardFaces[i] != null)
                    cardFaces[i].color = usable || _discardMode ? _faceOn : _faceOff;
                cardNames[i].text = cardDatabase.GetName(card);
                cardInfos[i].text = cardDatabase.GetEffectText(card);
                if (cardPips != null && i < cardPips.Length && cardPips[i] != null) cardPips[i].text = cardDatabase.GetPipCost(card).ToString();
                if (cardAccuracies != null && i < cardAccuracies.Length && cardAccuracies[i] != null) cardAccuracies[i].text = cardDatabase.GetAccuracy(card).ToString() + "%";
                if (cardSchools != null && i < cardSchools.Length && cardSchools[i] != null) cardSchools[i].text = cardDatabase.SchoolName(cardDatabase.GetSchool(card));
            }

            bool picking = CanAct() && _selHand >= 0 && _selHand < _handCount;
            bool allies = picking && cardDatabase.TargetsAllies(_hand[_selHand]);
            for (int t = 0; t < BattleManager.SLOTS; t++)
            {
                if (targetButtons == null || t >= targetButtons.Length || targetButtons[t] == null) continue;
                bool show = picking && manager.Alive(t) && (manager.SideOf(t) == 0) == allies;
                targetButtons[t].SetActive(show);
                if (show) targetLabels[t].text = manager.SlotName(t) + "\n" + _shownHp[t].ToString() + " / " + manager.maxHp[t].ToString();
            }
            if (potionLabel != null) potionLabel.text = "Potion (" + CountPotions().ToString() + ")";
            if (discardImage != null) discardImage.color = _discardMode ? new Color(0.8f, 0.55f, 0.2f) : new Color(0.4f, 0.3f, 0.25f);
            if (statusLabel != null) statusLabel.text = StatusText();
        }

        // ------------------------------------------------------------------ shown state (shields, pips) during playback

        /// <summary>Copies the synced shields / blades / traps and pips into what the circle shows.</summary>
        private void SyncShownState()
        {
            if (manager == null) return;
            int n = BattleManager.SLOTS * BattleManager.FX_MAX;
            for (int i = 0; i < n; i++)
            {
                _fxSh[i] = manager.shields[i];
                _fxBl[i] = manager.blades[i];
                _fxTr[i] = manager.traps[i];
            }
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                _shownPips[s] = manager.pips[s];
                _shownPower[s] = manager.powerPips[s];
            }
        }

        private int ShownCount(int[] arr, int s)
        {
            int n = 0;
            for (int k = 0; k < BattleManager.FX_MAX; k++) if (arr[s * BattleManager.FX_MAX + k] != 0) n++;
            return n;
        }

        /// <summary>Same rule as BattleManager.AddFx: append, dropping the oldest when full.</summary>
        private void AddShown(int[] arr, int s, int packed)
        {
            if (s < 0 || s >= BattleManager.SLOTS) return;
            int max = BattleManager.FX_MAX, b = s * max;
            int n = ShownCount(arr, s);
            if (n >= max)
            {
                for (int k = 0; k < max - 1; k++) arr[b + k] = arr[b + k + 1];
                n = max - 1;
            }
            arr[b + n] = packed;
        }

        /// <summary>Clears the first entry equal to packed (without closing the gap, see CompactShown); returns its index or -1.</summary>
        private int RemoveShown(int[] arr, int s, int packed)
        {
            int max = BattleManager.FX_MAX, b = s * max;
            for (int k = 0; k < max; k++)
                if (arr[b + k] == packed) { arr[b + k] = 0; return k; }
            return -1;
        }

        /// <summary>Closes the gaps left by RemoveShown (the circle lays effects out from index 0).</summary>
        private void CompactShown(int[] arr)
        {
            int max = BattleManager.FX_MAX;
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                int w = 0;
                for (int k = 0; k < max; k++)
                {
                    int v = arr[s * max + k];
                    arr[s * max + k] = 0;
                    if (v != 0) arr[s * max + w++] = v;
                }
            }
        }

        /// <summary>A shield / blade / trap cast this step appears now (not when the round's result arrived).</summary>
        private void AddShownFx(int i)
        {
            if (manager.actKind[i] != BattleManager.KIND_BUFF) return;
            int card = manager.actCard[i];
            int t = manager.actTarget[i];
            int v = manager.actValue[i];
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_WARD) AddShown(_fxSh, t, manager.PackFx(card, cardDatabase.ShieldSchool(card), v));
            else if (e == CardDatabase.EFFECT_TAUNT) AddShown(_fxSh, t, manager.PackFx(card, CardDatabase.ANY_SCHOOL, v));
            else if (e == CardDatabase.EFFECT_EMPOWER) AddShown(_fxBl, t, manager.PackFx(card, cardDatabase.BladeSchool(card), v));
            else if (e == CardDatabase.EFFECT_TRAP) AddShown(_fxTr, t, manager.PackFx(card, cardDatabase.TrapSchool(card), v));
            else if (e == CardDatabase.EFFECT_TRIBLADE)
            {
                int first = cardDatabase.TriBladeFirstSchool(card);
                for (int j = 0; j < 3; j++) AddShown(_fxBl, t, manager.PackFx(card, first + j, v));
            }
        }

        /// <summary>Every shield / blade / trap the referee logged against act i flashes, breaks and shows its percent.</summary>
        private void BreakLoggedFx(int i)
        {
            int max = BattleManager.FX_MAX;
            for (int j = 0; j < manager.fxLogCount && j < BattleManager.MAX_FX_LOG; j++)
            {
                if (manager.fxLogAct[j] != i) continue;
                int slot = manager.fxLogSlot[j];
                int kind = manager.fxLogKind[j];
                int v = manager.fxLogValue[j];
                if (slot < 0 || slot >= BattleManager.SLOTS || kind < 0 || kind > 2) continue;
                int[] arr = kind == BattleManager.FX_SHIELD ? _fxSh : (kind == BattleManager.FX_BLADE ? _fxBl : _fxTr);
                int fxSchool = manager.FxSchool(v);
                Vector3 at = seats[slot].position + Vector3.up * (1.2f * SeatScale(slot));
                int k = RemoveShown(arr, slot, v);
                if (k >= 0 && fxIcons != null)
                {
                    int idx = (slot * 3 + kind) * max + k;
                    if (idx < fxIcons.Length && fxIcons[idx] != null && fxIcons[idx].gameObject.activeInHierarchy) at = fxIcons[idx].transform.position;
                }
                SpawnPop(at, FxIconIndex(kind, fxSchool), FxMat(fxSchool));
                int pct = manager.FxPct(v);
                string txt = (kind == BattleManager.FX_SHIELD ? "-" : "+") + pct.ToString() + "%";
                FloatTex(slot, txt, SchoolTextColor(fxSchool), FxTexture(kind, fxSchool));
            }
            CompactShown(_fxSh);
            CompactShown(_fxBl);
            CompactShown(_fxTr);
        }

        private Material FxMat(int school)
        {
            if (fxMats == null || fxMats.Length == 0) return null;
            return fxMats[school >= 0 && school < fxMats.Length - 1 ? school : fxMats.Length - 1];
        }

        // ------------------------------------------------------------------ pips at the seats

        private void UpdatePips()
        {
            if (pipIcons == null) return;
            int max = BattleManager.MAX_PIPS;
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                bool live = manager.Occupied(s) && _shownHp[s] > 0;
                int p = live ? _shownPower[s] : 0;
                int n = live ? _shownPips[s] : 0;
                for (int k = 0; k < max; k++)
                {
                    int idx = s * max + k;
                    if (idx >= pipIcons.Length || pipIcons[idx] == null) continue;
                    Renderer r = pipIcons[idx];
                    bool on = k < p + n;
                    if (r.gameObject.activeSelf != on) r.gameObject.SetActive(on);
                    if (!on) continue;
                    Material m = k < p ? powerPipMat : pipMat;
                    if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
                }
            }
        }

        /// <summary>Pips bob and turn (in PSX-style steps); motes swirl round each combatant, more for more pips.</summary>
        private void AnimatePips()
        {
            float tq = Mathf.Floor(Time.time * 15f) / 15f;
            if (pipIcons != null && _pipBase != null)
                for (int i = 0; i < pipIcons.Length && i < _pipBase.Length; i++)
                {
                    Renderer r = pipIcons[i];
                    if (r == null || !r.gameObject.activeSelf) continue;
                    Transform t = r.transform;
                    t.localPosition = _pipBase[i] + Vector3.up * (Mathf.Sin(tq * 3f + i * 0.7f) * 0.05f);
                    t.localRotation = Quaternion.Euler(0f, tq * 120f + i * 25f, 0f);
                }
            int ph = manager.phase;
            if (Time.time < _nextSwirl || ph < BattleManager.PHASE_JOINING || ph > BattleManager.PHASE_PLAYBACK) return;
            _nextSwirl = Time.time + swirlInterval;
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                if (!manager.Occupied(s) || _shownHp[s] <= 0) continue;
                if (pipSwirls != null && s < pipSwirls.Length && pipSwirls[s] != null && _shownPips[s] > 0) pipSwirls[s].Emit(_shownPips[s]);
                if (powerSwirls != null && s < powerSwirls.Length && powerSwirls[s] != null && _shownPower[s] > 0) powerSwirls[s].Emit(_shownPower[s] * 2);
            }
        }

        // ------------------------------------------------------------------ circle: start flash and idle animation

        /// <summary>Wizard101's duel start: the circle appears in a white flash and settles into its normal look.</summary>
        private void StartFlash()
        {
            _flashBattle = manager.battleId;
            _flashStart = Time.time;
            if (circleFlash != null) circleFlash.gameObject.SetActive(true);
            if (flashPillar != null) flashPillar.gameObject.SetActive(true);
            if (flashLight != null) flashLight.enabled = true;
            if (startSparks != null) startSparks.Play();
            if (circleAudio != null && circleSound != null && circleAudio.gameObject.activeInHierarchy) circleAudio.PlayOneShot(circleSound);
            AnimateFlash();
        }

        private void AnimateFlash()
        {
            float since = Time.time - _flashStart;
            float dur = Mathf.Max(0.1f, startFlashSeconds);
            if (since > dur * 1.5f) return;
            float t = since / dur;
            if (t >= 1f)
            {
                if (circleFlash != null) circleFlash.gameObject.SetActive(false);
                if (flashPillar != null) flashPillar.gameObject.SetActive(false);
                if (flashLight != null) flashLight.enabled = false;
                return;
            }
            // PSX: the settle happens in visible steps.
            float tq = Mathf.Floor(t * 24f) / 24f;
            float settle = tq < 0.12f ? 0f : (tq - 0.12f) / 0.88f;
            settle = 1f - (1f - settle) * (1f - settle);
            if (circleFlash != null)
            {
                float sc = tq < 0.12f ? Mathf.Lerp(1.3f, 1.2f, tq / 0.12f) : Mathf.Lerp(1.2f, 1f, settle);
                circleFlash.localScale = new Vector3(sc, 1f, sc);
                if (circleFlashMat != null) circleFlashMat.SetColor("_EmissionColor", Color.Lerp(Color.white * 3f, circleSigilGlow, settle));
            }
            if (flashPillar != null)
            {
                float h = tq < 0.5f ? 1f - tq / 0.5f : 0f;
                float r = Mathf.Lerp(0.35f, 1f, Mathf.Min(1f, tq / 0.3f));
                if (h <= 0f) { if (flashPillar.gameObject.activeSelf) flashPillar.gameObject.SetActive(false); }
                else flashPillar.localScale = new Vector3(r, h, r);
            }
            if (flashLight != null) flashLight.intensity = 3.5f * (1f - tq);
        }

        /// <summary>The sigil and rune band turn opposite ways in PSX-style steps; the sigil's glow pulses.</summary>
        private void AnimateCircle()
        {
            float since = Time.time - _flashStart;
            float step = Mathf.Max(0.5f, circleStepDegrees);
            if (circleSigil != null) circleSigil.localRotation = Quaternion.Euler(0f, Mathf.Floor(since * sigilDegreesPerSecond / step) * step, 0f);
            if (circleRunes != null) circleRunes.localRotation = Quaternion.Euler(0f, Mathf.Floor(since * runeDegreesPerSecond / step) * step, 0f);
            if (circleSigilMat != null)
            {
                float tq = Mathf.Floor(Time.time * 8f) / 8f;
                circleSigilMat.SetColor("_EmissionColor", circleSigilGlow * (1.1f + 0.6f * Mathf.Sin(tq * 2.2f)));
            }
        }

        // ------------------------------------------------------------------ breaking effects

        private Material[] _popMats;
        private Material[] _popFlashMats;

        /// <summary>The broken effect's icon blinks bright, swells and vanishes where it was.</summary>
        private void SpawnPop(Vector3 at, int iconIndex, Material fallback)
        {
            if (breakPops == null || breakPops.Length == 0 || _popStart == null || _popStart.Length == 0) return;
            if (_popMats == null || _popMats.Length != breakPops.Length) { _popMats = new Material[breakPops.Length]; _popFlashMats = new Material[breakPops.Length]; }
            int i = _popNext;
            _popNext = (_popNext + 1) % breakPops.Length;
            if (breakPops[i] == null) return;
            Material m = fxIconMats != null && iconIndex >= 0 && iconIndex < fxIconMats.Length && fxIconMats[iconIndex] != null ? fxIconMats[iconIndex] : fallback;
            Material fm = fxIconFlashMats != null && iconIndex >= 0 && iconIndex < fxIconFlashMats.Length && fxIconFlashMats[iconIndex] != null ? fxIconFlashMats[iconIndex] : pipMat;
            _popMats[i] = m;
            _popFlashMats[i] = fm;
            _popStart[i] = Time.time;
            breakPops[i].position = at;
            breakPops[i].localScale = new Vector3(0.5f, 0.5f, 1f);
            breakPops[i].gameObject.SetActive(true);
        }

        /// <summary>A breaking effect blinks white / school colour, swells and shatters away.</summary>
        private void AnimatePops()
        {
            if (breakPops == null || _popStart == null) return;
            for (int i = 0; i < breakPops.Length && i < _popStart.Length; i++)
            {
                Transform p = breakPops[i];
                if (p == null || !p.gameObject.activeSelf) continue;
                float age = Time.time - _popStart[i];
                float t = age / 0.5f;
                if (t >= 1f) { p.gameObject.SetActive(false); continue; }
                Renderer r = breakPopRenderers != null && i < breakPopRenderers.Length ? breakPopRenderers[i] : null;
                if (r != null)
                {
                    bool flash = ((int)(age / 0.06f)) % 2 == 0;
                    Material m = flash && _popFlashMats != null ? _popFlashMats[i] : (_popMats != null ? _popMats[i] : null);
                    if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
                }
                // Holds and blinks, then swells and shrinks away, turning a little as it breaks.
                float sc = t < 0.3f ? 1f : (t < 0.55f ? Mathf.Lerp(1f, 1.7f, (t - 0.3f) / 0.25f) : Mathf.Lerp(1.7f, 0f, (t - 0.55f) / 0.45f));
                p.localScale = new Vector3(0.5f * sc, 0.5f * sc, 1f);
                VRCPlayerApi me = Networking.LocalPlayer;
                if (Utilities.IsValid(me))
                {
                    Vector3 look = p.position - me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                    if (look.sqrMagnitude > 0.0001f) p.rotation = Quaternion.LookRotation(look) * Quaternion.Euler(0f, 0f, t > 0.55f ? (t - 0.55f) * 120f : 0f);
                }
            }
        }

        // ------------------------------------------------------------------ floating combat text

        public Color[] schoolTextColors =
        {
            new Color(1f, 0.45f, 0.2f), new Color(0.55f, 0.85f, 1f), new Color(0.8f, 0.55f, 1f), new Color(0.45f, 1f, 0.45f),
            new Color(0.75f, 0.75f, 0.8f), new Color(1f, 0.85f, 0.3f), new Color(1f, 0.7f, 0.45f),
        };

        private Color SchoolTextColor(int school)
        {
            return schoolTextColors != null && school >= 0 && school < schoolTextColors.Length ? schoolTextColors[school] : new Color(0.95f, 0.95f, 0.95f);
        }

        /// <summary>The floating text for one act entry (damage numbers, heals, fizzles, buffs...).</summary>
        private void FloatAct(int i)
        {
            int k = manager.actKind[i];
            int t = manager.actTarget[i];
            int a = manager.actActor[i];
            int v = manager.actValue[i];
            int f = manager.actFlags[i];
            int card = manager.actCard[i];
            int sc = card > 0 ? cardDatabase.GetSchool(card) : -1;
            if (k == BattleManager.KIND_DAMAGE)
            {
                bool crit = (f & BattleManager.FLAG_CRIT) != 0;
                Float(t, (crit ? "CRITICAL!\n" : "") + "-" + v.ToString(), crit ? new Color(1f, 0.9f, 0.2f) : SchoolTextColor(sc), sc);
            }
            else if (k == BattleManager.KIND_HEAL) { if (v > 0) Float(t, "+" + v.ToString(), new Color(0.45f, 1f, 0.45f), sc); }
            else if (k == BattleManager.KIND_POTION) { if (v > 0) Float(a, "+" + v.ToString(), new Color(0.45f, 1f, 0.45f), -2); }
            else if (k == BattleManager.KIND_FIZZLE) Float(a, "Fizzled!", new Color(0.8f, 0.8f, 0.85f), -2);
            else if (k == BattleManager.KIND_DOT) Float(a, "-" + v.ToString(), new Color(1f, 0.5f, 0.15f), 0);
            else if (k == BattleManager.KIND_STUNNED) Float(a, "Stunned!", new Color(1f, 0.95f, 0.4f), -2);
            else if (k == BattleManager.KIND_BUFF)
            {
                string b = BuffFloat(card, v);
                if (b.Length == 0) return;
                int e = cardDatabase.GetEffect(card);
                Texture fx = null;
                if (e == CardDatabase.EFFECT_WARD) fx = FxTexture(BattleManager.FX_SHIELD, cardDatabase.ShieldSchool(card));
                else if (e == CardDatabase.EFFECT_TAUNT) fx = FxTexture(BattleManager.FX_SHIELD, CardDatabase.ANY_SCHOOL);
                else if (e == CardDatabase.EFFECT_EMPOWER) fx = FxTexture(BattleManager.FX_BLADE, cardDatabase.BladeSchool(card));
                else if (e == CardDatabase.EFFECT_TRIBLADE) fx = FxTexture(BattleManager.FX_BLADE, CardDatabase.ANY_SCHOOL);
                else if (e == CardDatabase.EFFECT_TRAP) fx = FxTexture(BattleManager.FX_TRAP, cardDatabase.TrapSchool(card));
                if (fx != null) FloatTex(t, b, SchoolTextColor(sc), fx);
                else Float(t, b, SchoolTextColor(sc), sc);
            }
        }

        private string BuffFloat(int card, int v)
        {
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_WARD) return "Shield " + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_EMPOWER) return "Blade +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_TRIBLADE) return "3 Blades +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_TRAP) return "Trap +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_DOT) return "Burning!";
            if (e == CardDatabase.EFFECT_DISPEL) return "Dispelled!";
            if (e == CardDatabase.EFFECT_TAUNT) return "Taunt!";
            if (e == CardDatabase.EFFECT_FOCUS) return "Focus +" + v.ToString() + "%";
            return "";
        }

        /// <summary>
        /// Floats text (with a school icon: 0-6 a school, -1 General, -2 none) up from a combatant's head. Several in one
        /// step stack upward and start a moment apart.
        /// </summary>
        private void Float(int slot, string text, Color color, int icon)
        {
            Texture tex = null;
            if (icon != -2 && schoolIcons != null && schoolIcons.Length > 0)
            {
                int n = schoolIcons.Length;
                tex = schoolIcons[icon >= 0 && icon < n - 1 ? icon : n - 1];
            }
            FloatTex(slot, text, color, tex);
        }

        private Texture FxTexture(int kind, int school)
        {
            int i = FxIconIndex(kind, school);
            return fxIconTextures != null && i < fxIconTextures.Length ? fxIconTextures[i] : null;
        }

        /// <summary>Floats text with an icon (null = none).</summary>
        private void FloatTex(int slot, string text, Color color, Texture iconTex)
        {
            if (floaters == null || floaters.Length == 0 || _floatStart == null || slot < 0 || slot >= BattleManager.SLOTS || seats == null) return;
            int i = _floatNext;
            _floatNext = (_floatNext + 1) % floaters.Length;
            if (floaters[i] == null) return;
            int st = _stack[slot]++;
            _floatBase[i] = seats[slot].position + Vector3.up * (1.95f * SeatScale(slot) + st * 0.42f);
            _floatStart[i] = Time.time + st * 0.12f;
            if (floaterTexts != null && i < floaterTexts.Length && floaterTexts[i] != null)
            {
                floaterTexts[i].text = text;
                floaterTexts[i].color = color;
            }
            if (floaterIcons != null && i < floaterIcons.Length && floaterIcons[i] != null)
            {
                RawImage img = floaterIcons[i];
                bool show = iconTex != null;
                if (show)
                {
                    img.texture = iconTex;
                    img.color = Color.white;
                }
                img.gameObject.SetActive(show);
            }
            floaters[i].position = _floatBase[i];
            floaters[i].localScale = Vector3.zero;
            floaters[i].gameObject.SetActive(true);
        }

        private void AnimateFloaters()
        {
            if (floaters == null || _floatStart == null) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            bool haveHead = Utilities.IsValid(me);
            Vector3 head = haveHead ? me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position : Vector3.zero;
            for (int i = 0; i < floaters.Length && i < _floatStart.Length; i++)
            {
                Transform f = floaters[i];
                if (f == null || !f.gameObject.activeSelf) continue;
                float t = (Time.time - _floatStart[i]) / Mathf.Max(0.2f, floaterSeconds);
                if (t < 0f) { f.localScale = Vector3.zero; continue; }
                if (t >= 1f) { f.gameObject.SetActive(false); continue; }
                float pop = t < 0.07f ? Mathf.Lerp(0.2f, 1.35f, t / 0.07f) : (t < 0.15f ? Mathf.Lerp(1.35f, 1f, (t - 0.07f) / 0.08f) : 1f);
                f.localScale = Vector3.one * pop;
                f.position = _floatBase[i] + Vector3.up * (Mathf.Floor(t * 30f) / 30f * 0.8f);
                Vector3 look = f.position - head;
                if (haveHead && look.sqrMagnitude > 0.001f) f.rotation = Quaternion.LookRotation(look);
                float alpha = t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
                if (floaterTexts != null && i < floaterTexts.Length && floaterTexts[i] != null)
                {
                    Color c = floaterTexts[i].color;
                    c.a = alpha;
                    floaterTexts[i].color = c;
                }
                if (floaterIcons != null && i < floaterIcons.Length && floaterIcons[i] != null) floaterIcons[i].color = new Color(1f, 1f, 1f, alpha);
            }
        }

        // ------------------------------------------------------------------ combat sounds

        /// <summary>Hit, heal and defeat sounds where they happen (each client plays its own playback).</summary>
        private void ActSound(int i, int hpBefore)
        {
            if (hitAudio == null || !hitAudio.gameObject.activeInHierarchy || seats == null) return;
            int k = manager.actKind[i];
            int t = manager.actTarget[i];
            int v = manager.actValue[i];
            if (t < 0 || t >= BattleManager.SLOTS) return;
            AudioClip clip = null;
            if ((k == BattleManager.KIND_DAMAGE || k == BattleManager.KIND_DOT) && v > 0 && t < BattleManager.SIDE && wizardHitSounds != null && wizardHitSounds.Length > 0)
                clip = wizardHitSounds[Random.Range(0, wizardHitSounds.Length)];
            if ((k == BattleManager.KIND_HEAL || k == BattleManager.KIND_POTION) && v > 0) clip = healSound;
            if (t < BattleManager.SIDE && hpBefore > 0 && manager.actHpAfter[i] <= 0 && defeatSound != null) clip = defeatSound;
            if (clip == null) return;
            hitAudio.transform.position = seats[t].position + Vector3.up * 1.2f;
            hitAudio.PlayOneShot(clip);
        }

        /// <summary>Card art: shields, blades and traps show their icon (they just flash and appear); other spells have none yet.</summary>
        private Texture CardArt(int card)
        {
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_WARD) return FxTexture(BattleManager.FX_SHIELD, cardDatabase.ShieldSchool(card));
            if (e == CardDatabase.EFFECT_TAUNT) return FxTexture(BattleManager.FX_SHIELD, CardDatabase.ANY_SCHOOL);
            if (e == CardDatabase.EFFECT_EMPOWER) return FxTexture(BattleManager.FX_BLADE, cardDatabase.BladeSchool(card));
            if (e == CardDatabase.EFFECT_TRIBLADE) return FxTexture(BattleManager.FX_BLADE, CardDatabase.ANY_SCHOOL);
            if (e == CardDatabase.EFFECT_TRAP) return FxTexture(BattleManager.FX_TRAP, cardDatabase.TrapSchool(card));
            return null;
        }

        private void SetBanner(string text)
        {
            if (bannerLabel != null) bannerLabel.text = text;
        }
    }
}
