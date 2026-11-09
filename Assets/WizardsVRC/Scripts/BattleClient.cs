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
        [Tooltip("The book's notifications (new gear).")]
        public NotificationCenter notifications;
        [Tooltip("Dustwind's bounty board: battles you win count toward your posted bounties.")]
        public BountyBoard bounties;
        [Tooltip("Archrock's High Noon (duelling circles there only): duels won at noon pay a bonus.")]
        public HighNoon highNoon;
        [Tooltip("Elixir bonuses and spell skins.")]
        public CosmeticsDatabase cosmetics;
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public CardDatabase cardDatabase;

        [Header("World")]
        public Transform hubReturn;
        [Tooltip("Where a defeated wizard is sent (e.g. the zone's entrance). Empty = hubReturn.")]
        public Transform defeatReturn;
        [Tooltip("Where a wizard who won is sent (a boss tower's next floor). Empty = hubReturn.")]
        public Transform victoryReturn;
        [Tooltip("A boss tower floor: told when you win here.")]
        public TowerFloor towerFloor;
        private bool _wonLast;
        [Tooltip("The encounter's root (ring, seats, labels, hand panel). Moved to the manager's ring spot when a fight starts.")]
        public Transform encounterRoot;
        [Tooltip("Body size per seat (the builder fills it from each seat's monster) for cast effects over their heads.")]
        public float[] seatScales;
        public Transform[] seats;
        public GameObject ring;
        public GameObject[] monsterBodies;
        [Tooltip("Each monster seat's animated body (same order as Monster Bodies): it casts, flinches and falls as the round plays.")]
        public MonsterRig[] monsterRigs;
        public Text[] overheadLabels;
        public Text bannerLabel;
        [Tooltip("Seconds the Victory / Defeat text stays over the ring before it clears.")]
        public float resultSeconds = 6f;
        private float _bannerClearAt = -1f;
        public Text padLabel;
        [Tooltip("Turned to face the viewer (upright) every frame: the duelling circle's sign, board and all.")]
        public Transform padPivot;
        [Tooltip("A boss tower floor's pad text when idle (the countdown and 'in progress' follow it).")]
        public string padTitle = "";
        public Transform orb;
        public Renderer orbRenderer;

        [Header("School cast effect (parented to the ring)")]
        public Transform castRing;
        public Renderer castRingRenderer;
        public Renderer castGlyph;
        [Tooltip("Old badge glyphs (unused: casters now draw their symbol, see Glyph Draw Tex).")]
        public Material[] castGlyphMats;

        [Header("Drawn symbol: the caster traces their school's symbol in the air in front of them")]
        [Tooltip("Data maps from Tools/make_glyph_draw.py (WizardsVRC/GlyphDraw shader), indexed by School; the last is General.")]
        public Texture2D[] glyphDrawTex;
        [Tooltip("Energy colour and pen-tip colour per symbol (same order).")]
        public Color[] glyphColors;
        public Color[] glyphEdgeColors;
        [Tooltip("Pen path per symbol (Editor/GlyphPenData.cs): glyphPenSamples points each, in texture coordinates.")]
        public float[] glyphPenU;
        public float[] glyphPenV;
        public int[] glyphPenJump;
        public int glyphPenSamples = 64;
        [Tooltip("The glowing pen tip, and each symbol's sparks trailing off it (same order).")]
        public Transform penTip;
        public Renderer penTipRenderer;
        public ParticleSystem[] glyphTrails;
        [Tooltip("Seconds to draw the symbol (at most about half the wind-up).")]
        public float glyphDrawSeconds = 1.6f;
        [Tooltip("Symbol size (m), how far in front of the caster and how high it's drawn (scaled with big monsters).")]
        public float glyphSize = 1.35f;
        public float glyphForward = 1.15f;
        public float glyphHeight = 1.55f;

        [Header("Fizzle")]
        public AudioClip fizzleSound;
        [Tooltip("Grey puff of smoke where the symbol was when a spell fizzles.")]
        public ParticleSystem fizzlePuff;
        public Transform impactBurst;
        public Renderer impactRenderer;
        public AudioSource castAudio;
        [Tooltip("School-coloured particle burst off the caster, indexed by School; the last entry is for General spells.")]
        public ParticleSystem[] castBursts;
        [Tooltip("Indexed by School. General spells play no cast sound.")]
        public AudioClip[] castSounds;
        [Tooltip("Cast sound for General (school-less) spells.")]
        public AudioClip generalCastSound;
        public Transform[] stagingSpots;
        public Transform arenaCenter;
        public float engageRadius = 14f;
        [Tooltip("Walking into the circle only counts this close to its height (m): a tower's rings are stacked one per floor.")]
        public float engageHeight = 5f;
        public Transform clockHand;
        public Text clockLabel;
        [Tooltip("The round timer: the seconds left to choose, big over the banner (red for the last ten).")]
        public Text timerLabel;
        [Tooltip("Ticks once a second for the last ten seconds of choosing (clockTick; clockTickLast for the last three).")]
        public AudioSource clockAudio;
        public AudioClip clockTick;
        public AudioClip clockTickLast;
        private int _tickedSecond = -1;
        private float _timerPulse;
        private bool _centreHidden;
        [Tooltip("The clock dial set into the ring (DuelClock shader): its rim of segments shows the time left to choose.")]
        public Renderer clockFace;
        private Material _clockMat;
        private float _clockShown = -1f;
        [Tooltip("One glowing ring per seat, coloured by the school of whoever sits there.")]
        public Renderer[] seatRings;
        [Tooltip("Indexed by School.")]
        public Material[] seatSchoolMats;
        public Material seatEmptyMat;
        [Tooltip("Empty seats on the monsters' half (tinted so the two sides read clearly). Empty = seatEmptyMat.")]
        public Material seatEmptyMonsterMat;

        [Header("Seat health gauge (WizardsVRC/PSXGauge on an occupied seat's ring)")]
        [Tooltip("Copied per seat at runtime. Empty seats keep the plain ring.")]
        public Material seatGaugeMat;
        [Tooltip("Gauge fill colour by School (bright, so Death still reads on the dark floor).")]
        public Color[] gaugeSchoolColors;
        [Tooltip("At or under this share of health the gauge blinks to gaugeLowColor.")]
        [Range(0f, 1f)] public float gaugeLowShare = 0.25f;
        public Color gaugeLowColor = new Color(1f, 0.15f, 0.1f);
        public Color gaugeFrameWizard = new Color(0.08f, 0.12f, 0.26f);
        public Color gaugeFrameMonster = new Color(0.26f, 0.07f, 0.05f);
        [Tooltip("How long health just lost stays lit before it drains away.")]
        public float gaugeTrailHold = 0.4f;
        [Tooltip("Share of the gauge per second that lost health drains, and that heals fill back in.")]
        public float gaugeTrailSpeed = 0.6f;
        public float gaugeHealSpeed = 0.8f;

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
        [Tooltip("PSX look: the circle's idle turning updates at this many frames per second (not every rendered frame).")]
        public float circleFps = 15f;
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
        [Header("Creator colours (signature, pip aura)")]
        [Tooltip("The character creator's palette; index 0 means the wizard's school colour.")]
        public Color[] lookPalette;
        public Color[] schoolColors;
        [Tooltip("Aura motes round each seat, shown while it holds at least auraPipThreshold pips (one per seat).")]
        public ParticleSystem[] auraSwirls;
        public Renderer[] auraRenderers;
        public int auraPipThreshold = 5;

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

        [Header("Shields, blades and traps")]
        [Tooltip("They're 3D models (shield, sword, spiked trap) in their school's colour, not flat icons.")]
        public bool fx3D = true;
        [Tooltip("When a shield, blade or trap is used, it springs into action for this long before the hit lands.")]
        public float fxUseSeconds = 0.5f;
        // Effects springing into action before a hit (index into fxIcons; one entry per piece).
        private const int MAX_FX_USE = 16;
        private int[] _useIdx = new int[MAX_FX_USE];
        private int[] _useKind = new int[MAX_FX_USE];
        private Vector3[] _useFrom = new Vector3[MAX_FX_USE];
        private Vector3[] _useTo = new Vector3[MAX_FX_USE];
        private Vector3[] _useAim = new Vector3[MAX_FX_USE];
        private int _useCount;
        private float _useStart = -100f;
        private bool[] _fxUsing;

        private float[] _fxRadius = { 0.75f, 0.45f, 0.9f };
        /// <summary>Each kind's model scale as built (read from the first piece of each kind at start).</summary>
        private float[] _fxBaseScale = { 0.62f, 0.75f, 0.6f };
        private float[] _fxSpin = { 70f, -110f, 45f };
        /// <summary>Degrees between stacked effects of one kind: each new one sits right next to the last.</summary>
        private float[] _fxStackDeg = { 42f, 30f, 38f };

        // What the circle shows: the synced state, except during round playback, when it changes step by step.
        private int[] _fxSh = new int[BattleManager.SLOTS * BattleManager.FX_MAX];
        private int[] _fxBl = new int[BattleManager.SLOTS * BattleManager.FX_MAX];
        private int[] _fxTr = new int[BattleManager.SLOTS * BattleManager.FX_MAX];
        private int[] _shownPips = new int[BattleManager.SLOTS];
        // Which wizard's Surge aura each seat is powering, and with how many pips (SyncAuras).
        private int[] _auraIds = new int[BattleManager.SLOTS];
        private int[] _auraPips = new int[BattleManager.SLOTS];
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
        [Tooltip("Confirmation box shown over the hand when Flee is pressed (Flee / Stay).")]
        public GameObject fleeConfirmRoot;
        [Header("Choosing a potion")]
        [Tooltip("The Potion button's menu: one button per kind of potion you carry (its name, how many, what it restores); " +
                 "pick one to drink it as this round's action.")]
        public GameObject potionMenuRoot;
        public GameObject[] potionChoiceButtons;
        public Text[] potionChoiceLabels;
        public Text potionMenuNote;
        public const int POTION_CHOICES = 6;
        private int[] _potionChoices = new int[POTION_CHOICES];
        private int[] _potionCounts = new int[POTION_CHOICES];
        private int _potionChoiceCount;
        [Header("Waiting for the others")]
        [Tooltip("Over the cards once your spell is chosen while other wizards are still choosing: your spell, who you're " +
                 "waiting on, the time left, and Back (take your spell back and choose again). The action tray stays usable.")]
        public GameObject waitRoot;
        public Text waitLabel;
        public GameObject waitBackButton;
        private bool _pickSent;          // a spell (or pass, or potion) went to the ring this round
        private int _sentCard;           // what: a card, 0 for a pass, PICK_POTION
        private int _sentTarget = -1;
        private int _unpickCard;         // a card taken back (Back) that the round might have started with anyway
        private int _unpickRound = -1;
        // Treasure cards (Wizard101): single-use cards from the equipped deck's sideboard. In the hand a treasure card is
        // its spell id + TC_FLAG, so it keeps its gold ribbon and is used up (taken out of the sideboard for good) when cast,
        // fizzled or discarded. Treasure cards drawn but never cast stay in the sideboard.
        public const int TC_FLAG = 100000;
        [Header("Treasure cards")]
        [Tooltip("The action tray's Treasure button: draws one treasure card from your deck's sideboard (once a round).")]
        public GameObject treasureButton;
        public Text treasureLabel;
        [Tooltip("Per hand card: the gold TREASURE ribbon shown on treasure cards.")]
        public GameObject[] cardTreasureTags;
        private int[] _tcPool = new int[CharacterData.SIDE_SIZE];
        private int _tcPoolCount;
        private int _tcDrawRound = -1;
        private int _sentRaw;            // the sent card as it was in the hand (a treasure card keeps its flag)
        private int _unpickRaw;
        private int _castRaw;            // the hand card being cast right now
        private int _patRaw;
        private int _tcSent;             // spell id of a treasure card sent this round (used up once it plays)
        private int _tcFizzle;           // a treasure card that fizzled this round: used up, not shuffled back
        private float _nextWaitText;
        public Text fleeConfirmLabel;
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
        [Header("Seat targeting: with a spell picked, the seats you can cast it at glow (green allies, red enemies); click one")]
        [Tooltip("Per seat: a click box (EventRelay sending Target<n>), on only while that seat can be picked.")]
        public GameObject[] seatPickers;
        [Tooltip("Per seat: the glow ring on the floor.")]
        public Renderer[] seatGlows;
        public Material seatGlowAllyMat;
        public Material seatGlowEnemyMat;
        private bool _glowsOn;
        public Image discardImage;

        [Header("Rewards")]
        [Tooltip("Random loot after a win (30%). Empty = any item a shop could sell that's within a few levels of the wizard " +
                 "(gear from 4 levels below up to their level, potions of their tier); quest rewards and boss drops never roll.")]
        public int[] lootTable = new int[0];
        [Tooltip("No wand drops (a monster's own drop or random loot) for a wizard below this level: they keep the wand they " +
                 "start with until then. Shops still sell wands.")]
        public int wandDropLevel = 8;

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

        /// <summary>This encounter is a wizard-against-wizard duel (no monsters, rewards or defeat penalties).</summary>
        private bool Pvp() { return manager != null && manager.pvpMode; }

        private int Elixir(int stat, bool pvp) { return cosmetics != null ? cosmetics.ElixirBonus(stat, pvp) : 0; }

        /// <summary>Our half of the circle (0 = the wizards' / Blue team's, 1 = the monsters' / Red team's).</summary>
        private int MySide() { return _slot >= 0 && manager != null ? manager.SideOf(_slot) : 0; }

        private string TeamName(int side) { return side == 0 ? "Blue team" : "Red team"; }

        /// <summary>True when the local player has a character and isn't already fighting (used by wandering monsters).</summary>
        public bool CanFight() { return !_inBattle && playerState != null && playerState.HasCharacter() && !playerState.inCombat; }

        private int _slot = -1;
        private bool _seated;
        private float _nextEngage;
        private int _clockActor = -1;
        private int _battleSeen;
        [Tooltip("Street fights: the seated monsters appear this long after the ring forms (the patrol monster walks over first).")]
        public float bodyRevealDelay = 1.8f;
        private float _revealAt = -100f;
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
        private float _burstAt = -100f;
        private bool _fizzling;
        private float _fizzleStart;
        private Material _glyphMat;
        private int _glyphIndex;
        private float _drawLen = 1.6f;
        private bool _penOn;
        private int _castSchool;
        private float _castLen;
        private float _castPeak;
        private float _impactStart;
        private int _stepStart;
        private int _stepEnd;
        private string _stepHeader = "";
        private int _castFrom;
        private int _castTo;
        private Vector3[] _aoeTargets = new Vector3[BattleManager.SIDE];
        private Vector3 _ringBase;
        private Vector3 _glyphBase;
        private GameObject _spellAnim;
        private GameObject _stepAnim;
        private int _stepCard;
        private bool _animImpact;
        private int _fxMode;
        private float _fxStart, _fxStrike, _fxSeconds;
        private Vector3 _fxFrom, _fxTo, _fxAreaPos;
        private Transform _fxSummon, _fxBolt, _fxArea;
        // The creature's beats (PlaySpellAnimation / AnimateCreature): the circle it rises from, its pillar of light, the
        // sparkle bursts, its Animator, its kind of body, where it's summoned and where it strikes.
        private Transform _fxCircle, _fxBeam, _fxBurstT;
        private ParticleSystem _fxBurst;
        private Animator _fxRigAnim;
        private int _fxKind, _fxCue;
        private bool _fxAoe;
        private Vector3 _fxPad, _fxGoal;
        private const int RIG_BIPED = 0, RIG_BEAST = 1, RIG_FLYER = 2, RIG_PLANT = 3;
        // A creature riding its spell's path (SpellCinematic moves it, not AnimateCreature).
        private bool _fxDriven;

        [Header("Casting: patterns and spell choreography (shared by every duel circle)")]
        [Tooltip("Drawing a spell's rune before it's sent, for spells under 100% accuracy.")]
        public CastPattern castPattern;
        [Tooltip("Every attack spell's own choreography, and a wizard's attack spell as a cinematic.")]
        public SpellCinematic spellCinematic;

        // The spell waiting on its rune (CastPattern).
        private bool _patterning;
        private int _patCard;
        private int _patTarget;
        private string _patNote = "";

        // This step is an attack spell played by SpellCinematic; a cinematic one is cast from the middle of the ring.
        private bool _stepStyled;
        private bool _stepCine;
        private int _stepActor;
        private int _stepTarget;
        private int _stepSchool;
        private bool _castAtOn;
        private int _castAtSlot = -1;
        private Vector3 _castAt;
        private Vector3 _castAtFwd;

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

        /// <summary>The Flee button: asks first (Flee / Stay) before leaving the fight.</summary>
        public void Flee()
        {
            if (!CanFlee()) return;
            if (fleeConfirmRoot == null) { FleeNow(); return; }
            if (fleeConfirmLabel != null)
            {
                int others = 0;
                int first = MySide() * BattleManager.SIDE;
                for (int i = first; i < first + BattleManager.SIDE; i++) if (i != _slot && manager.Alive(i) && !manager.IsSummon(i)) others++;
                if (Pvp())
                    fleeConfirmLabel.text = "Leave the duel? Duels cost nothing.\n" +
                        (others > 0 ? "Your team will fight on without you." : "You are your team's last wizard, so the other team wins.");
                else
                    fleeConfirmLabel.text = "You keep your health, but you lose all of your mana.\n" +
                        (others > 0 ? "Your party will fight on without you." : "You are fighting alone, so the duel ends.");
            }
            fleeConfirmRoot.SetActive(true);
        }

        public void ConfirmFlee()
        {
            CloseFleeConfirm();
            if (CanFlee()) FleeNow();
        }

        public void CancelFlee()
        {
            CloseFleeConfirm();
        }

        private void CloseFleeConfirm()
        {
            if (fleeConfirmRoot != null && fleeConfirmRoot.activeSelf) fleeConfirmRoot.SetActive(false);
        }

        private bool CanFlee()
        {
            if (!_inBattle || _leaving || _slot < 0 || !manager.Alive(_slot)) return false;
            int ph = manager.phase;
            return ph == BattleManager.PHASE_PLANNING || ph == BattleManager.PHASE_JOINING;
        }

        /// <summary>Leave the fight now. Like Wizard101 you keep your health but lose all of your mana.</summary>
        private void FleeNow()
        {
            if (!Pvp() && playerState != null && playerState.HasCharacter())
            {
                playerState.SetHealth(BattleToCharacterHealth(manager.hp[_slot]));
                playerState.SetMana(0);
            }
            _vitalsDone = true;
            _fledBattle = manager.battleId;   // walking into this circle again won't pull you back into it
            manager.RequestFlee();
            _LeaveBattle();
        }

        /// <summary>
        /// The Potion button: a menu of the potions you carry, one button per kind (name, how many, what it restores); pick
        /// one to drink it as this round's action. Without the menu (older scenes), the best one for you is drunk.
        /// </summary>
        public void Potion()
        {
            if (!CanAct() || playerState == null || !playerState.HasCharacter()) return;
            if (potionMenuRoot == null) { DrinkBestPotion(); return; }
            BuildPotionChoices(playerState.current);
            if (_potionChoiceCount == 0)
            {
                _note = "You have no potions.";
                RefreshHand();
                return;
            }
            for (int i = 0; i < POTION_CHOICES; i++)
            {
                bool on = i < _potionChoiceCount;
                GameObject b = potionChoiceButtons != null && i < potionChoiceButtons.Length ? potionChoiceButtons[i] : null;
                if (b != null && b.activeSelf != on) b.SetActive(on);
                if (!on || potionChoiceLabels == null || i >= potionChoiceLabels.Length || potionChoiceLabels[i] == null) continue;
                int id = _potionChoices[i];
                int heal = itemDatabase.GetPotionHealth(id);
                int mana = itemDatabase.GetPotionMana(id);
                string what = heal > 0 && mana > 0 ? "+" + heal.ToString() + " health, +" + mana.ToString() + " mana"
                    : heal > 0 ? "+" + heal.ToString() + " health" : "+" + mana.ToString() + " mana";
                potionChoiceLabels[i].text = itemDatabase.GetName(id) + "  x" + _potionCounts[i].ToString() + "\n<size=22>" + what + "</size>";
            }
            if (potionMenuNote != null)
                potionMenuNote.text = "Health " + manager.hp[_slot].ToString() + " / " + manager.maxHp[_slot].ToString() +
                    (Pvp() ? "" : "     Mana " + playerState.GetMana().ToString() + " / " + playerState.MaxMana().ToString());
            if (uiAudio != null) uiAudio.PlayPopup();
            potionMenuRoot.SetActive(true);
        }

        public void PotionChoice0() { ChoosePotion(0); }
        public void PotionChoice1() { ChoosePotion(1); }
        public void PotionChoice2() { ChoosePotion(2); }
        public void PotionChoice3() { ChoosePotion(3); }
        public void PotionChoice4() { ChoosePotion(4); }
        public void PotionChoice5() { ChoosePotion(5); }

        public void ClosePotionMenu()
        {
            if (potionMenuRoot != null && potionMenuRoot.activeSelf) potionMenuRoot.SetActive(false);
        }

        /// <summary>Each kind of potion in your pack (stacks added up), in pack order, up to POTION_CHOICES.</summary>
        private void BuildPotionChoices(CharacterData c)
        {
            _potionChoiceCount = 0;
            if (itemDatabase == null) return;
            int used = c.UsedInventorySlots();
            for (int i = 0; i < used; i++)
            {
                int id = c.inventoryItemIds[i];
                if (id == 0 || c.inventoryCounts[i] == 0 || !itemDatabase.IsPotion(id)) continue;
                if (itemDatabase.GetPotionHealth(id) <= 0 && itemDatabase.GetPotionMana(id) <= 0) continue;
                int at = -1;
                for (int k = 0; k < _potionChoiceCount; k++) if (_potionChoices[k] == id) at = k;
                if (at >= 0) { _potionCounts[at] += c.inventoryCounts[i]; continue; }
                if (_potionChoiceCount >= POTION_CHOICES) continue;
                _potionChoices[_potionChoiceCount] = id;
                _potionCounts[_potionChoiceCount] = c.inventoryCounts[i];
                _potionChoiceCount++;
            }
        }

        /// <summary>A potion picked from the menu: drunk as this round's action (unless it couldn't do anything).</summary>
        private void ChoosePotion(int i)
        {
            if (!CanAct() || playerState == null || !playerState.HasCharacter() || i >= _potionChoiceCount) { ClosePotionMenu(); return; }
            CharacterData c = playerState.current;
            int id = _potionChoices[i];
            int heal = itemDatabase.GetPotionHealth(id);
            int mana = itemDatabase.GetPotionMana(id);
            bool hurt = manager.hp[_slot] < manager.maxHp[_slot];
            bool drained = !Pvp() && playerState.GetMana() < playerState.MaxMana();
            if ((heal <= 0 || !hurt) && (mana <= 0 || !drained))
            {
                if (potionMenuNote != null) potionMenuNote.text = heal > 0 && mana <= 0 ? "You're already at full health." : mana > 0 && heal <= 0 ? "Your mana is already full." : "You're already at full health and mana.";
                return;
            }
            int inv = c.FindItem(id);
            if (inv < 0) { ClosePotionMenu(); return; }
            ClosePotionMenu();
            DrinkPotionAt(c, inv);
        }

        /// <summary>No menu: a health potion if you're hurt, otherwise a mana potion.</summary>
        private void DrinkBestPotion()
        {
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
            DrinkPotionAt(c, inv);
        }

        /// <summary>Drinks the potion in pack slot 'inv' as this round's action: mana now, health when the round plays out.</summary>
        private void DrinkPotionAt(CharacterData c, int inv)
        {
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
            if (enter && (playerState == null || !playerState.HasCharacter() || playerState.inCombat || _inBattle)) return;
            if (enter && !QueueUnlocked()) return;
            if (manager != null) manager.RequestQueue(enter);
        }

        /// <summary>
        /// Called now and then while the local player stands in a duel zone: joins the countdown if they aren't in it
        /// (they walked in while a duel was on, or right after one ended).
        /// </summary>
        public void LocalStillInside()
        {
            if (manager == null || _inBattle) return;
            int ph = manager.phase;
            if (ph != BattleManager.PHASE_IDLE && ph != BattleManager.PHASE_QUEUE) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || manager.IsQueued(me.playerId) || Time.time < _rejoinBlockUntil) return;
            LocalQueue(true);
        }

        private void Start()
        {
            CacheCardLayout();
            if (fxIcons != null)
                for (int kind = 0; kind < 3; kind++)
                {
                    int i = kind * BattleManager.FX_MAX;
                    if (i < fxIcons.Length && fxIcons[i] != null) _fxBaseScale[kind] = fxIcons[i].transform.localScale.x;
                }
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
            if (_patterning && phase != BattleManager.PHASE_PLANNING) DropPattern();
            if (phase != BattleManager.PHASE_IDLE) PlaceRing();
            bool active = phase >= BattleManager.PHASE_JOINING;
            if (ring != null) ring.SetActive(active);
            // Outside round playback the circle shows the synced shields, pips etc. as they are.
            if (!_playing && phase != BattleManager.PHASE_PLAYBACK)
            {
                SyncShownState();
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
            }
            if (active && manager.battleId != _flashBattle) StartFlash();
            if (active) { UpdateSeatRings(); UpdateFx(); UpdatePips(); }
            else SyncAuras();   // the duel's over: everyone's Surge aura goes quiet
            RefreshBodies(phase);

            VRCPlayerApi local = Networking.LocalPlayer;
            int mySlot = Utilities.IsValid(local) ? manager.FindWizardSlot(local.playerId) : -1;

            if ((phase == BattleManager.PHASE_ROAMING || phase == BattleManager.PHASE_JOINING) && manager.battleId != _battleSeen)
            {
                _battleSeen = manager.battleId;
                if (manager.wanderMode && manager.round == 0)
                {
                    _revealAt = Time.time + bodyRevealDelay;
                    SendCustomEventDelayedSeconds(nameof(_RevealBodies), bodyRevealDelay + 0.05f);
                }
                _roundSeen = 0;
                _clockActor = -1;
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
                ClearDefeats();
                RefreshBodies(phase);
                if (mySlot >= 0 && !InOtherFight()) ArriveAtArena(mySlot);
                SetBanner("");
            }
            // Joined a wandering-monster fight during its join window: the battle id didn't change, so arrive now.
            // (Not right after leaving: after fleeing our seat is still listed until the owner processes it.)
            if (!_inBattle && mySlot >= 0 && manager.wanderMode && phase >= BattleManager.PHASE_JOINING && phase <= BattleManager.PHASE_PLAYBACK &&
                Time.time >= _rejoinBlockUntil && !InOtherFight()) ArriveAtArena(mySlot);
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
                    _pickSent = false;
                    _sentCard = 0;
                    _sentRaw = 0;
                    _sentTarget = -1;
                    _unpickCard = 0;
                    _unpickRaw = 0;
                    _tcSent = 0;
                    _selHand = -1;
                    _discardMode = false;
                    _note = "Pick a card, or Pass.";
                    if (_slot >= 0 && manager.stunned[_slot] != 0)
                    {
                        _submitted = true;
                        _note = "You are stunned and lose this turn.";
                    }
                }
                if (Pvp())
                {
                    SetBanner("Round " + manager.round.ToString() + "  -  " + TeamName(manager.firstSide) + " acts first");
                    if (_inBattle && _slot >= 0 && !manager.Alive(_slot)) _note = "You're knocked out. Watch the rest of the duel!";
                }
                else SetBanner("Round " + manager.round.ToString() + (manager.firstSide == 0 ? "  -  Wizards act first" : "  -  Monsters act first"));
            }

            if (phase == BattleManager.PHASE_PLAYBACK && _phaseSeen != BattleManager.PHASE_PLAYBACK)
            {
                if (_inBattle) { ReconcileUnpick(); SpendCastMana(); UseTreasureCard(); ReturnFizzledCards(); }
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
                if (Pvp())
                {
                    // Duels are friendly: whoever wins, health, mana and gear are just as they were before.
                    int w = manager.winnerSide;
                    string result = w < 0 ? "The duel is over." : TeamName(w) + " wins!";
                    if (_inBattle) result = w < 0 ? "The duel is over." : (w == MySide() ? "Victory! " : "Defeat... ") + TeamName(w) + " wins.";
                    if (_inBattle && w >= 0 && w == MySide() && highNoon != null) result += highNoon.DuelWon();
                    if (_inBattle) result += "\nDuels cost nothing: your health and mana are as they were.";
                    _vitalsDone = true;
                    ShowResult(result);
                    _note = result;
                    if (_inBattle) SendCustomEventDelayedSeconds(nameof(_LeaveBattle), Mathf.Max(2, manager.endSeconds - 1));
                    _phaseSeen = phase;
                    RefreshLabels();
                    RefreshHand();
                    UpdateHandVisible();
                    return;
                }
                bool won = phase == BattleManager.PHASE_VICTORY;
                // A wizard who fell in the final round goes home defeated even if the party won.
                bool iWon = won && _slot >= 0 && manager.hp[_slot] > 0;
                string msg = won ? "Victory!" : "Defeat...";
                if (_inBattle && iWon)
                {
                    if (playerState != null) playerState.SetHealth(Mathf.Max(1, BattleToCharacterHealth(manager.hp[_slot])));
                    _vitalsDone = true;
                    msg += "\n" + GrantRewards();
                    _wonLast = true;
                    if (towerFloor != null) towerFloor.LocalCleared();
                    if (victoryReturn != null) msg += "\nOnward and upward...";
                }
                else if (_inBattle)
                {
                    ApplyDefeat();
                    msg += "\nYou were defeated and return home to recover.";
                }
                ShowResult(msg);
                _note = msg;
                if (_inBattle) SendCustomEventDelayedSeconds(nameof(_LeaveBattle), Mathf.Max(2, manager.endSeconds - 1));
            }

            // Nothing stays written over an empty ring.
            if (phase == BattleManager.PHASE_IDLE && bannerLabel != null && bannerLabel.text.Length > 0) SetBanner("");
            if (phase == BattleManager.PHASE_IDLE && _inBattle) _LeaveBattle();
            if (_inBattle && !_leaving && mySlot < 0 && phase != BattleManager.PHASE_IDLE)
            {
                // Our seat was vacated: with 0 health that means we were defeated mid-fight (Wizard101 sends you home).
                if (!Pvp() && _slot >= 0 && manager.hp[_slot] <= 0)
                {
                    ApplyDefeat();
                    _note = "You were defeated!";
                    _leaving = true;
                    if (handRoot != null) handRoot.SetActive(false);
                    SendCustomEventDelayedSeconds(nameof(_LeaveBattle), 1.5f);
                }
                else _LeaveBattle();
            }

            // The confirmation only makes sense while you could still flee (it closes when the round plays out).
            if (!CanFlee()) CloseFleeConfirm();

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
            bool show = _inBattle && _seated && !_leaving && !_patterning && (ph == BattleManager.PHASE_JOINING || ph == BattleManager.PHASE_PLANNING);
            if (handRoot.activeSelf != show) { handRoot.SetActive(show); if (show) _hudSnap = true; }
        }

        // ------------------------------------------------------------------ the battle HUD in front of you, and its grip

        [Header("Battle HUD placement")]
        [Tooltip("The handle under the hand of cards (a VRC Pickup with HudGrip): grab it to move the hand anywhere; it stays " +
                 "there, relative to you, for every fight after.")]
        public Transform hudGrip;
        public VRC_Pickup hudGripPickup;
        [Tooltip("Where the hand floats relative to your head (x right, y up, z ahead) until you move it. VR: low, like a " +
                 "lectern, so pointing across the ring passes over the cards.")]
        public Vector3 hudOffsetVR = new Vector3(0f, -0.5f, 0.42f);
        public Vector3 hudOffsetDesktop = new Vector3(0f, -0.2f, 0.75f);
        [Tooltip("The hand only swings round to follow you once you turn this far away from it (degrees).")]
        public float hudFollowAngle = 35f;

        private bool _hudSnap;
        private bool _gripHeld;
        private float _hudYaw;

        /// <summary>HudGrip: the handle was picked up. The hand of cards follows it until it's let go.</summary>
        public void _GripStart() { _gripHeld = true; }

        /// <summary>HudGrip: let go. Where the hand is now (relative to your head) is where it stays from now on.</summary>
        public void _GripEnd()
        {
            _gripHeld = false;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (handRoot == null || playerState == null || !Utilities.IsValid(me)) return;
            VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Quaternion yaw = Quaternion.Euler(0f, _hudYaw, 0f);
            Vector3 local = Quaternion.Inverse(yaw) * (handRoot.transform.position - head.position);
            local.z = Mathf.Max(0.25f, local.z);   // never behind or inside your head
            playerState.battleHudOffset = local;
            playerState.battleHudSet = true;
        }

        private Vector3 HudOffset(VRCPlayerApi me)
        {
            if (playerState != null && playerState.battleHudSet) return playerState.battleHudOffset;
            return me.IsUserInVR() ? hudOffsetVR : hudOffsetDesktop;
        }

        /// <summary>
        /// Every frame the hand is up: it floats in front of you (lazily turning to follow you), facing you, or follows the
        /// grip while you hold it. The grip sits under its bottom edge.
        /// </summary>
        private void PlaceHud()
        {
            bool up = handRoot != null && handRoot.activeSelf;
            if (hudGrip != null && hudGrip.gameObject.activeSelf != up)
            {
                if (!up && hudGripPickup != null && _gripHeld) hudGripPickup.Drop();
                hudGrip.gameObject.SetActive(up);
            }
            if (!up) { _gripHeld = false; return; }
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Transform h = handRoot.transform;
            RectTransform rt = handRoot.GetComponent<RectTransform>();
            float halfH = rt != null ? rt.rect.height * 0.5f * h.lossyScale.y : 0.3f;

            if (_gripHeld && hudGrip != null)
            {
                Vector3 look = hudGrip.position - head.position;
                if (look.sqrMagnitude < 0.0001f) look = head.rotation * Vector3.forward;
                Quaternion rot = Quaternion.LookRotation(look, Vector3.up);
                h.SetPositionAndRotation(hudGrip.position + rot * Vector3.up * (halfH + 0.05f), rot);
                return;
            }

            float headYaw = head.rotation.eulerAngles.y;
            if (_hudSnap) { _hudYaw = headYaw; }
            else
            {
                float off = Mathf.DeltaAngle(_hudYaw, headYaw);
                if (Mathf.Abs(off) > hudFollowAngle) _hudYaw = Mathf.MoveTowardsAngle(_hudYaw, headYaw, (Mathf.Abs(off) - hudFollowAngle * 0.5f) * Time.deltaTime * 4f);
            }
            Vector3 want = head.position + Quaternion.Euler(0f, _hudYaw, 0f) * HudOffset(me);
            Vector3 dir = want - head.position;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            Quaternion wantRot = Quaternion.LookRotation(dir, Vector3.up);
            if (_hudSnap) { h.SetPositionAndRotation(want, wantRot); _hudSnap = false; }
            else
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 10f);
                h.SetPositionAndRotation(Vector3.Lerp(h.position, want, k), Quaternion.Slerp(h.rotation, wantRot, k));
            }
            if (hudGrip != null && !_gripHeld) hudGrip.SetPositionAndRotation(h.position - h.rotation * Vector3.up * (halfH + 0.05f), h.rotation);
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

        private float _nextDecline;
        private int _fledBattle = -1;

        /// <summary>
        /// Listed in this fight while already seated in another (a tower floor's monster run into in the same moment as
        /// another floor's, say): a wizard fights one duel at a time, so instead of arriving here (and being teleported
        /// between fights, then defeated in the ones you can't play), we give this seat up.
        /// </summary>
        private bool InOtherFight()
        {
            if (_inBattle || playerState == null || !playerState.inCombat) return false;
            if (Time.time >= _nextDecline)
            {
                _nextDecline = Time.time + 2f;
                // A frame later: we're inside OnBattleState, and the referee may be us.
                SendCustomEventDelayedFrames(nameof(_DeclineFight), 1);
            }
            return true;
        }

        public void _DeclineFight()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (manager == null || _inBattle || !Utilities.IsValid(me) || manager.FindWizardSlot(me.playerId) < 0) return;
            manager.RequestFlee();
        }

        private void ArriveAtArena(int slot)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || playerState == null || !playerState.HasCharacter()) return;
            _inBattle = true;
            _seated = false;
            _leaving = false;
            _defeated = false;
            _wonLast = false;
            // Duels never touch the character's health or mana, so there is nothing to write back afterwards.
            _vitalsDone = Pvp();
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
            QueueMove(spot, 0);
        }

        // ------------------------------------------------------------------ moving the local player

        private bool _moveHas;
        private Vector3 _movePos;
        private Quaternion _moveRot;
        private int _moveHold;
        private bool _moveQueued;

        /// <summary>
        /// Teleports the local player to 'target' (null: stay put) a frame from now, and with hold 1 immobilizes them once
        /// they're there (-1 frees them first). Everything that seats or unseats a wizard runs from OnBattleState, which is
        /// called inside network updates (OnDeserialization, network events), and VRChat says never to teleport there: the
        /// avatar can be left behind its own view, so you see a copy of yourself standing where you were, and everyone
        /// else sees you there too instead of in your seat. Later calls in the same frame win, so arriving at the ring and
        /// taking the seat in one update is a single teleport.
        /// </summary>
        private void QueueMove(Transform target, int hold)
        {
            if (target != null) QueueMoveTo(target.position, target.rotation, hold);
            else
            {
                _moveHas = false;
                _moveHold = hold;
                QueueApply();
            }
        }

        /// <summary>QueueMove to a point rather than a marker.</summary>
        private void QueueMoveTo(Vector3 pos, Quaternion rot, int hold)
        {
            _moveHas = true;
            _movePos = pos;
            _moveRot = rot;
            _moveHold = hold;
            if (culler != null) culler.Prepare(pos);
            QueueApply();
        }

        private void QueueApply()
        {
            if (_moveQueued) return;
            _moveQueued = true;
            SendCustomEventDelayedFrames(nameof(_ApplyMove), 1);
        }

        /// <summary>
        /// Where a wizard who fled (or whose fight ended without a win or a loss) goes when the ring has no return marker:
        /// a few steps out of the circle behind their own seat, facing away from it, so they're out of the fight (and not
        /// pulled straight back in by walking into the circle) but still right where they were.
        /// </summary>
        [Tooltip("Fleeing a ring with no return marker steps you out to this distance from its middle (m).")]
        public float stepOutRadius = 11f;
        // Everything solid except players, UI, triggers' own layers and walk-through decor.
        private const int StepOutMask = ~((1 << 2) | (1 << 5) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 17) | (1 << 18));

        private void StepOutOfRing(int slot)
        {
            if (arenaCenter == null || seats == null || slot < 0 || slot >= seats.Length || seats[slot] == null) return;
            Vector3 c = arenaCenter.position;
            Vector3 out_ = seats[slot].position - c;
            out_.y = 0f;
            if (out_.sqrMagnitude < 0.01f) out_ = -seats[slot].forward;
            out_.Normalize();
            // Out past the ring's edge, but never through a wall: stop short of anything solid on the way.
            Vector3 from = seats[slot].position + Vector3.up;
            float go = Mathf.Max(0f, stepOutRadius - Vector3.Distance(new Vector3(from.x, c.y, from.z), c));
            RaycastHit hit;
            if (Physics.Raycast(from, out_, out hit, go + 0.6f, StepOutMask, QueryTriggerInteraction.Ignore)) go = Mathf.Max(0f, hit.distance - 0.6f);
            Vector3 pos = seats[slot].position + out_ * go;
            QueueMoveTo(pos, Quaternion.LookRotation(out_, Vector3.up), -1);
        }

        public void _ApplyMove()
        {
            _moveQueued = false;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            if (_moveHold < 0) { local.Immobilize(false); SetSeated(false); }
            if (_moveHas)
            {
                if (culler != null) culler.Prepare(_movePos);
                // The player themselves on the spot (not their playspace: a VR player standing off the middle of their
                // room was put off their seat, toward the middle of the ring).
                local.TeleportTo(_movePos, _moveRot, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
            }
            // Held to the seat only after the teleport has taken effect.
            if (_moveHold > 0) SendCustomEventDelayedFrames(nameof(_ApplyHold), 2);
            _moveHas = false;
            if (_areaAsleep && !_inBattle) enabled = false;
        }

        [Tooltip("A seated wizard can move about this far from their seat (m); a step further and they're eased back.")]
        public float seatRoam = 0.9f;
        private bool _held;

        /// <summary>
        /// In a seat you're no longer frozen: you can turn, lean and shuffle about slowly (PlayerState.seatedSpeed) within
        /// seatRoam of the seat (KeepNearSeat), and no jumping.
        /// </summary>
        private void SetSeated(bool on)
        {
            _held = on;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (playerState != null) { playerState.seated = on; playerState.ApplySpeed(); }
            if (Utilities.IsValid(local)) local.SetJumpImpulse(0f);
            if (!on && Utilities.IsValid(local))
            {
                // Jumping comes back unless the jump button is the book button (VR).
                if (!local.IsUserInVR()) local.SetJumpImpulse(3f);
            }
        }

        private void KeepNearSeat()
        {
            if (!_held || !_seated || _slot < 0 || seats == null || _slot >= seats.Length || seats[_slot] == null || _moveQueued) return;
            // Away in the middle of the ring for your spell's cinematic.
            if (spellCinematic != null && spellCinematic.HoldsPlayer()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 seat = seats[_slot].position;
            Vector3 p = me.GetPosition();
            Vector3 off = p - seat;
            off.y = 0f;
            float limit = seatRoam * SeatScale(_slot);
            if (off.magnitude <= limit) return;
            Vector3 back = seat + off.normalized * (limit * 0.9f);
            back.y = p.y;
            me.TeleportTo(back, me.GetRotation(), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, true);
        }

        public void _ApplyHold()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && _inBattle && _seated && !_moveQueued) SetSeated(true);
        }

        private void TakeSeat()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || playerState == null || !playerState.HasCharacter()) return;
            _seated = true;
            CharacterData c = playerState.current;

            Transform seat = seats[_slot];
            QueueMove(seat, 1);

            float eye = local.GetAvatarEyeHeightAsMeters();
            if (eye < 0.2f) eye = 1.6f;
            if (handRoot != null)
            {
                Transform h = handRoot.transform;
                if (local.IsUserInVR())
                {
                    // VR: lower and laid back like a lectern at your waist, so pointing a controller at a monster or
                    // an ally (their seat lights up and can be clicked) passes over the cards instead of into them.
                    h.position = seat.position + seat.forward * 0.45f + Vector3.up * (eye * 0.45f);
                    h.rotation = seat.rotation * Quaternion.Euler(60f, 0f, 0f);
                }
                else
                {
                    h.position = seat.position + seat.forward * 0.75f + Vector3.up * (eye * 0.78f);
                    h.rotation = seat.rotation * Quaternion.Euler(25f, 0f, 0f);
                }
                handRoot.SetActive(true);
                _hudSnap = true;   // PlaceHud puts it in front of you from the next frame
            }

            BuildPile(c);
            _handCount = 0;
            if (!_submitted || manager.phase == BattleManager.PHASE_JOINING) _note = "Waiting for the other wizards...";
            int sc = (int)c.school;
            // Health carries over between fights (the school health trait is already part of MaxHealth).
            int maxHealth = playerState.MaxHealth();
            // Duels start everyone at full health.
            int curHealth = Pvp() ? maxHealth : Mathf.Max(1, playerState.GetHealth());
            playerState.combatHealth = -1;
            // Elixirs from the Starshard Shop add on top (never in PvP duels).
            bool pvp = Pvp();
            int statsA = manager.PackStats(
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_DAMAGE) + Trait(schoolDamage, sc, 0) + Elixir(ItemDatabase.STAT_DAMAGE, pvp),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_RESIST) + Trait(schoolResist, sc, 0) + Elixir(ItemDatabase.STAT_RESIST, pvp),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_ACCURACY) + Trait(schoolAccuracy, sc, 0) + Elixir(ItemDatabase.STAT_ACCURACY, pvp) + 128, 0);
            int statsB = manager.PackStats(
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_CRITICAL) + Elixir(ItemDatabase.STAT_CRITICAL, pvp),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_BLOCK),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_PIERCE),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_POWERPIP));
            // The creator colours ride above the level: level | signature << 8 | aura << 16.
            int look = Mathf.Clamp(c.signatureColor, 0, 255) | (Mathf.Clamp(c.auraColor, 0, 255) << 8) | (Mathf.Clamp(c.equippedSkin, 0, 15) << 16);
            manager.RequestJoin(maxHealth, curHealth, sc, Mathf.Clamp(c.level, 1, 255) | (look << 8), statsA, statsB);
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
                // A treasure card that fizzles is gone, like any treasure card that's cast.
                if (_tcFizzle > 0 && card == _tcFizzle) { _tcFizzle = 0; continue; }
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
            if (Pvp() || _slot < 0 || playerState == null || !playerState.HasCharacter()) return;
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
            CloseFleeConfirm();
            if (!_inBattle) return;
            DropPattern();
            if (spellCinematic != null && spellCinematic.IsFor(this)) spellCinematic.Abort(false);
            if (!_vitalsDone && _slot >= 0 && playerState != null && playerState.HasCharacter())
            {
                // Left some other way (fight reset, timeout): keep whatever health we had.
                if (manager.hp[_slot] > 0) playerState.SetHealth(BattleToCharacterHealth(manager.hp[_slot]));
            }
            _vitalsDone = true;
            _inBattle = false;
            _seated = false;
            _leaving = false;
            int leftSlot = _slot;
            _slot = -1;
            _rejoinBlockUntil = Time.time + 5f;
            _nextEngage = Time.time + 5f;
            if (playerState != null) { playerState.inCombat = false; playerState.combatHealth = -1; }
            if (handRoot != null) handRoot.SetActive(false);
            bool won = _wonLast && !_defeated;
            Transform back = _defeated && defeatReturn != null ? defeatReturn : won && victoryReturn != null ? victoryReturn : hubReturn;
            _wonLast = false;
            // Freed, then sent on (a frame later: this can run inside a network update). Fled from a ring with nowhere
            // set to go: a few steps out of the circle rather than standing in your old seat.
            if (back == null && !_defeated && !won) StepOutOfRing(leftSlot);
            else QueueMove(back, -1);
            if (playerState != null && playerState.HasCharacter()) playerState.SaveNow();
            // (If the ring's area is asleep, _ApplyMove switches this off once the move is done.)
        }

        // ------------------------------------------------------------------ sleeping with its area

        private bool _areaAsleep;

        /// <summary>
        /// AreaCuller: this ring's area was switched off (you're elsewhere). Nothing of it is on screen, so its every-frame
        /// animation stops until you come back; the fight itself (the synced BattleManager) carries on regardless, and the
        /// state it sends still arrives. Never while you're in this fight.
        /// </summary>
        public void _AreaSleep()
        {
            _areaAsleep = true;
            if (!_inBattle && !_moveQueued) enabled = false;   // a move still to make switches it off when done
        }

        /// <summary>AreaCuller: the area is back on. Catch up with the fight's current state and animate again.</summary>
        public void _AreaWake()
        {
            _areaAsleep = false;
            if (enabled) return;
            enabled = true;
            OnBattleState();
        }

        /// <summary>
        /// A random item for a wizard of this level: gear a shop could sell from 4 levels below up to their level, or a
        /// potion of their tier (minor potions until level 8). Never a quest reward, a boss drop, a deck or the starter
        /// kit. 0 when nothing fits.
        /// </summary>
        private int RollLoot(int level)
        {
            if (itemDatabase == null || itemDatabase.ids == null || itemDatabase.ids.Length == 0) return 0;
            // Any gear or potion near your level (never starter kit, decks, quest rewards or boss drops), picked evenly.
            int n = itemDatabase.ids.Length;
            int count = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int pick = pass == 1 ? Random.Range(0, count) : -1;
                int seen = 0;
                for (int i = 0; i < n; i++)
                {
                    int id = itemDatabase.ids[i];
                    if (id <= 4 || itemDatabase.IsUnique(id) || itemDatabase.IsDeck(id) || !CanDrop(id, level)) continue;
                    int req = itemDatabase.GetLevelReq(id);
                    int floor = itemDatabase.IsPotion(id) ? level - 7 : level - 4;
                    if (req > level || req < floor) continue;
                    if (pass == 0) count++;
                    else if (seen++ == pick) return id;
                }
                if (count == 0) return 0;
            }
            return 0;
        }

        /// <summary>Whether this item may drop for a wizard of this level: no wands below wandDropLevel.</summary>
        private bool CanDrop(int itemId, int level)
        {
            if (itemId <= 0) return false;
            return level >= wandDropLevel || itemDatabase == null || itemDatabase.GetCategory(itemId) != CharacterData.EQUIP_WEAPON;
        }

        /// <summary>A random entry of this ring's loot table that may drop for the wizard (0 if none may).</summary>
        private int PickFromTable(int level)
        {
            int count = 0;
            for (int i = 0; i < lootTable.Length; i++) if (CanDrop(lootTable[i], level)) count++;
            if (count == 0) return 0;
            int pick = Random.Range(0, count);
            for (int i = 0; i < lootTable.Length; i++)
                if (CanDrop(lootTable[i], level) && pick-- == 0) return lootTable[i];
            return 0;
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
                if (CanDrop(item, c.level) && Random.Range(0, 100) < cardDatabase.MonsterDropChance(id) && c.AddItem(item, 1))
                    drops += "\nLoot: " + itemDatabase.GetName(item);
            }
            if (xp == 0) { xp = 60 + 25 * manager.MonsterCount(); gold = Random.Range(30, 61); }
            xp += xp * Elixir(CosmeticsDatabase.ELIXIR_XP, Pvp()) / 100;
            c.gold += gold;
            // Records for the book's Stats page, and Starshards (the rare currency) from bosses and elites.
            int shards = 0;
            c.battlesWon++;
            for (int s = BattleManager.SIDE; s < BattleManager.SLOTS; s++)
            {
                int id = manager.MonsterAt(s);
                if (id == 0 || cardDatabase == null) continue;
                c.monstersDefeated++;
                if (cardDatabase.IsBossMonster(id)) { c.bossesDefeated++; shards += 3; }
                else if (cardDatabase.IsEliteMonster(id)) shards += 1;
            }
            c.starshards += shards;
            string msg = "+" + xp.ToString() + " XP   +" + gold.ToString() + " gold" + (shards > 0 ? "   +" + shards.ToString() + " Starshards" : "") + drops;
            if (notifications != null && drops.Length > 0) notifications.Post(NotificationCenter.KIND_GEAR, "New loot", drops.Trim(), 0);
            int levels = c.AddXp(xp);
            if (questManager != null) questManager.OnBattleWon(manager);
            if (bounties != null) bounties.OnBattleWon(manager);
            if (levels > 0)
            {
                msg += "\nLevel up! You are now level " + c.level.ToString() + ".";
                if (c.lastPointsGained > 0) msg += " +" + c.lastPointsGained.ToString() + " Training Point" + (c.lastPointsGained > 1 ? "s" : "") + ".";
            }
            if (drops.Length == 0 && Random.value < 0.3f)
            {
                int itemId = lootTable != null && lootTable.Length > 0 ? PickFromTable(c.level) : RollLoot(c.level);
                if (itemId > 0 && c.AddItem(itemId, 1))
                {
                    msg += "\nLoot: " + itemDatabase.GetName(itemId);
                    if (notifications != null) notifications.Post(NotificationCenter.KIND_GEAR, "New loot", itemDatabase.GetName(itemId), itemId);
                }
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
            // Any other wand: copies of a basic attack (its school's, or yours for a wand of any school).
            if (itemDatabase != null)
            {
                int basicCard = itemDatabase.WandBasicSpell(c.equipped[CharacterData.EQUIP_WEAPON], (int)c.school);
                if (basicCard > 0 && cardDatabase.IndexOf(basicCard) >= 0)
                    for (int k = 0; k < itemDatabase.wandBasicCopies && _pileCount < _pile.Length; k++) _pile[_pileCount++] = basicCard;
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
            BuildTreasurePool(c);
        }

        // ------------------------------------------------------------------ treasure cards

        /// <summary>The equipped deck's sideboard, shuffled: what the Treasure button draws from this fight.</summary>
        private void BuildTreasurePool(CharacterData c)
        {
            _tcPoolCount = 0;
            _tcDrawRound = -1;
            _tcSent = 0;
            _tcFizzle = 0;
            int list = c.ActiveDeckList();
            int n = list >= 0 ? c.SideCount(list) : 0;
            for (int i = 0; i < n && _tcPoolCount < _tcPool.Length; i++)
            {
                int id = c.SideCard(list, i);
                if (cardDatabase.IndexOf(id) >= 0) _tcPool[_tcPoolCount++] = id;
            }
            for (int i = _tcPoolCount - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int t = _tcPool[i]; _tcPool[i] = _tcPool[j]; _tcPool[j] = t;
            }
        }

        private bool CanDrawTreasure()
        {
            return CanAct() && !_discardMode && _tcPoolCount > 0 && _handCount < HAND_SIZE && _tcDrawRound != manager.round;
        }

        /// <summary>The Treasure button (Wizard101's Draw): one treasure card from your sideboard into your hand, once a round. It doesn't use your turn.</summary>
        public void DrawTreasure()
        {
            if (!CanAct()) return;
            if (_tcPoolCount <= 0) { _note = "No treasure cards left in your deck's sideboard."; RefreshHand(); return; }
            if (_handCount >= HAND_SIZE) { _note = "Your hand is full. Discard a card to draw a treasure card."; RefreshHand(); return; }
            if (_tcDrawRound == manager.round) { _note = "You can draw one treasure card a round."; RefreshHand(); return; }
            if (_discardMode) _discardMode = false;
            int id = _tcPool[--_tcPoolCount];
            _hand[_handCount++] = id + TC_FLAG;
            _tcDrawRound = manager.round;
            _note = "Drew a treasure card: " + cardDatabase.GetName(id) + ".";
            if (uiAudio != null) uiAudio.PlayCardClick();
            RefreshHand();
        }

        /// <summary>A treasure card leaves the sideboard for good (cast, fizzled or discarded).</summary>
        private void SpendTreasure(int id)
        {
            if (playerState == null || !playerState.HasCharacter() || id <= 0) return;
            CharacterData c = playerState.current;
            int list = c.ActiveDeckList();
            if (list >= 0 && c.TakeFromSide(list, id, false))
            {
                playerState.MarkDirty();
                playerState.SaveNow();
            }
        }

        /// <summary>The round is playing: a treasure card we cast this round is used up (and doesn't go back if it fizzled).</summary>
        private void UseTreasureCard()
        {
            int id = _tcSent;
            _tcSent = 0;
            _tcFizzle = 0;
            if (id <= 0 || _slot < 0) return;
            for (int i = 0; i < manager.actCount; i++)
            {
                if (manager.actActor[i] != _slot || manager.actCard[i] != id) continue;
                int k = manager.actKind[i];
                if (k == BattleManager.KIND_PASS || k == BattleManager.KIND_DOT || k == BattleManager.KIND_STUNNED || k == BattleManager.KIND_POTION) continue;
                SpendTreasure(id);
                if (k == BattleManager.KIND_FIZZLE) _tcFizzle = id;
                return;
            }
        }

        /// <summary>A hand entry's spell id (a treasure card carries TC_FLAG).</summary>
        private int HandCard(int raw) { return raw >= TC_FLAG ? raw - TC_FLAG : raw; }

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
            return _inBattle && !_submitted && !_patterning && manager.phase == BattleManager.PHASE_PLANNING && manager.Alive(_slot);
        }

        private void ClickCard(int i)
        {
            if (!CanAct() || i >= _handCount) return;
            if (uiAudio != null) uiAudio.PlayCardClick();
            int raw = _hand[i];
            int card = HandCard(raw);
            if (_discardMode)
            {
                RemoveFromHand(i);
                // Wizard101: a discarded treasure card is gone for good.
                if (raw >= TC_FLAG) SpendTreasure(card);
                _note = "Discarded " + cardDatabase.GetName(card) + (raw >= TC_FLAG ? " (the treasure card is used up)." : ".");
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
                _castRaw = raw;
                Submit(card, -1, "Casting " + cardDatabase.GetName(card) + ".");
                return;
            }
            if (cardDatabase.SelfOnly(card))
            {
                RemoveFromHand(i);
                _castRaw = raw;
                Submit(card, _slot, "Casting " + cardDatabase.GetName(card) + " on yourself.");
                return;
            }
            _selHand = i;
            if (Pvp()) _note = cardDatabase.GetName(card) + ": pick " + (cardDatabase.TargetsAllies(card) ? "a teammate." : "an opponent.");
            else _note = cardDatabase.GetName(card) + ": pick a " + (cardDatabase.TargetsAllies(card) ? "wizard." : "monster.");
            RefreshHand();
        }

        /// <summary>Spells cost 1 mana per pip (0-pip spells are free).</summary>
        private bool HasManaFor(int card)
        {
            return Pvp() || playerState == null || !playerState.HasCharacter() || cardDatabase.GetPipCost(card) <= playerState.GetMana();
        }

        private void ClickTarget(int target)
        {
            if (!CanAct() || _selHand < 0 || _selHand >= _handCount) return;
            int raw = _hand[_selHand];
            int card = HandCard(raw);
            RemoveFromHand(_selHand);
            _castRaw = raw;
            Submit(card, target, "Casting " + cardDatabase.GetName(card) + " on " + manager.SlotName(target) + ".");
        }

        private void Submit(int card, int target, string note)
        {
            // A spell that could fizzle is cast by drawing its rune first (CastPattern): the better it's drawn, the less
            // likely it fizzles. It's sent once the pattern is done.
            if (card > 0 && card != BattleManager.PICK_POTION && castPattern != null && _slot >= 0 && manager.FizzleChance(_slot, card, 0) > 0)
            {
                _patterning = true;
                _patCard = card;
                _patRaw = HandCard(_castRaw) == card ? _castRaw : card;
                _patTarget = target;
                _patNote = note;
                _selHand = -1;
                _discardMode = false;
                if (uiAudio != null) uiAudio.PlaySpellSelect();
                int accuracy = 100 - manager.FizzleChance(_slot, card, 0);
                castPattern.Begin(this, cardDatabase.GetSchool(card), cardDatabase.GetPipCost(card), accuracy, manager.SecondsLeft() - 1.5f, cardDatabase.GetName(card));
                _note = "Draw the rune to cast " + cardDatabase.GetName(card) + "...";
                RefreshHand();
                UpdateHandVisible();
                return;
            }
            SendPick(card, target, note, 0);
            if (uiAudio != null && card > 0) uiAudio.PlaySpellSelect();
        }

        private void SendPick(int card, int target, string note, int quality)
        {
            manager.RequestPickCast(card, target, quality);
            _submitted = true;
            _pickSent = true;
            _sentCard = card;
            _sentRaw = card > 0 && card != BattleManager.PICK_POTION && HandCard(_castRaw) == card ? _castRaw : card;
            _castRaw = 0;
            _tcSent = _sentRaw >= TC_FLAG ? card : 0;
            _sentTarget = target;
            _nextWaitText = 0f;
            _selHand = -1;
            _discardMode = false;
            _note = note + " Waiting for the round...";
            RefreshHand();
        }

        /// <summary>
        /// The waiting box's Back: your spell is taken back from the ring (BattleManager.NetUnpick), the card returns to your
        /// hand and you choose again. Not for a potion (already drunk), and only while the round hasn't started.
        /// </summary>
        public void BackToSpells()
        {
            if (!_inBattle || _leaving || !_pickSent || _slot < 0 || manager.phase != BattleManager.PHASE_PLANNING || !manager.Alive(_slot)) return;
            if (_sentCard == BattleManager.PICK_POTION) return;
            manager.RequestUnpick();
            if (_sentCard > 0 && _handCount < _hand.Length) _hand[_handCount++] = _sentRaw > 0 ? _sentRaw : _sentCard;
            _unpickCard = _sentCard;
            _unpickRaw = _sentRaw > 0 ? _sentRaw : _sentCard;
            _unpickRound = manager.round;
            _pickSent = false;
            _sentCard = 0;
            _sentRaw = 0;
            _tcSent = 0;
            _sentTarget = -1;
            _submitted = false;
            _selHand = -1;
            _discardMode = false;
            _note = "Pick a card, or Pass.";
            if (uiAudio != null) uiAudio.PlayCancel();
            RefreshHand();
            UpdateWaitPanel();
        }

        /// <summary>Other living wizards (either side in a duel) who haven't chosen yet this round.</summary>
        private int OthersChoosing()
        {
            int n = 0;
            for (int s = 0; s < BattleManager.SLOTS; s++)
                if (s != _slot && manager.IsPlayerSeat(s) && !manager.IsSummon(s) && manager.Alive(s) && manager.locked[s] == 0) n++;
            return n;
        }

        /// <summary>
        /// The waiting box: up while your spell is chosen and someone else is still choosing (not when you're alone: the
        /// round starts at once). Its words refresh a few times a second.
        /// </summary>
        private void UpdateWaitPanel()
        {
            if (waitRoot == null) return;
            bool planning = manager.phase == BattleManager.PHASE_PLANNING;
            // The ring heard Back (you're unlocked): the card taken back is surely yours again.
            if (planning && _unpickCard != 0 && _slot >= 0 && !_pickSent && manager.locked[_slot] == 0) _unpickCard = 0;
            bool show = _inBattle && _seated && !_leaving && !_patterning && _submitted && planning && _slot >= 0 && OthersChoosing() > 0;
            if (waitRoot.activeSelf != show) { waitRoot.SetActive(show); _nextWaitText = 0f; }
            if (!show || Time.time < _nextWaitText) return;
            _nextWaitText = Time.time + 0.25f;
            string who = "";
            for (int s = 0; s < BattleManager.SLOTS; s++)
                if (s != _slot && manager.IsPlayerSeat(s) && !manager.IsSummon(s) && manager.Alive(s) && manager.locked[s] == 0)
                    who += (who.Length > 0 ? ", " : "") + manager.SlotName(s);
            string mine;
            if (!_pickSent) mine = manager.stunned[_slot] != 0 ? "You're stunned this round." : "Your turn is set.";
            else if (_sentCard == 0) mine = "You pass this round.";
            else if (_sentCard == BattleManager.PICK_POTION) mine = "You're drinking a potion this round.";
            else mine = "Your spell: " + cardDatabase.GetName(_sentCard) + (_sentTarget >= 0 && _sentTarget < BattleManager.SLOTS ? " on " + manager.SlotName(_sentTarget) : "");
            if (waitLabel != null) waitLabel.text = mine + "\nStill choosing: " + who + "\nThe round starts in " + manager.SecondsLeft().ToString() + "s";
            bool back = _pickSent && _sentCard != BattleManager.PICK_POTION && manager.Alive(_slot);
            if (waitBackButton != null && waitBackButton.activeSelf != back) waitBackButton.SetActive(back);
        }

        /// <summary>
        /// The round started just as Back was pressed (before the ring heard it): if the spell taken back was cast after all,
        /// it leaves the hand again, and a spell chosen after it (which the ring never took) comes back.
        /// </summary>
        private void ReconcileUnpick()
        {
            int old = _unpickCard;
            _unpickCard = 0;
            if (old <= 0 || _unpickRound != manager.round || _slot < 0 || _sentCard == old) return;
            bool castOld = false;
            bool castNew = false;
            for (int i = 0; i < manager.actCount; i++)
            {
                if (i > 0 && manager.actGroup[i] == manager.actGroup[i - 1]) continue;
                if (manager.actActor[i] != _slot) continue;
                int k = manager.actKind[i];
                if (k == BattleManager.KIND_PASS || k == BattleManager.KIND_DOT || k == BattleManager.KIND_STUNNED || k == BattleManager.KIND_POTION) continue;
                if (manager.actCard[i] == old) castOld = true;
                if (_sentCard > 0 && manager.actCard[i] == _sentCard) castNew = true;
            }
            if (!castOld) return;
            int oldRaw = _unpickRaw > 0 ? _unpickRaw : old;
            for (int h = 0; h < _handCount; h++)
                if (_hand[h] == oldRaw) { RemoveFromHand(h); break; }
            if (_sentCard > 0 && !castNew && _handCount < _hand.Length) _hand[_handCount++] = _sentRaw > 0 ? _sentRaw : _sentCard;
            // The card that really played is the one used up if it was a treasure card.
            _tcSent = oldRaw >= TC_FLAG ? old : 0;
            _note = "Too late to take it back: " + cardDatabase.GetName(old) + " was cast.";
        }

        /// <summary>CastPattern: the rune is drawn (or time ran out). The spell goes with how well it was drawn.</summary>
        public void _PatternDone()
        {
            if (!_patterning) return;
            _patterning = false;
            int q = castPattern != null ? castPattern.Quality() : 0;
            if (_inBattle && !_submitted && manager.phase == BattleManager.PHASE_PLANNING && manager.Alive(_slot))
            {
                int fizzle = manager.FizzleChance(_slot, _patCard, q);
                _castRaw = _patRaw;
                SendPick(_patCard, _patTarget, _patNote + " Rune " + q.ToString() + "% drawn: " + fizzle.ToString() + "% chance to fizzle.", q);
            }
            else if (_inBattle && _handCount < _hand.Length) _hand[_handCount++] = PatRaw();   // too late: the card stays yours
            UpdateHandVisible();
        }

        /// <summary>CastPattern's Cancel button: the card goes back into the hand.</summary>
        public void _PatternCancelled()
        {
            if (!_patterning) return;
            _patterning = false;
            if (_handCount < _hand.Length) _hand[_handCount++] = PatRaw();
            _note = "Pick a card, or Pass.";
            RefreshHand();
            UpdateHandVisible();
        }

        /// <summary>The round moved on (or the fight ended) while a rune was being drawn: it closes, nothing is sent, the card is kept.</summary>
        private void DropPattern()
        {
            if (!_patterning) return;
            _patterning = false;
            if (castPattern != null) castPattern.Cancel();
            if (_handCount < _hand.Length) _hand[_handCount++] = PatRaw();
        }

        /// <summary>The card a rune was being drawn for, as it was in the hand.</summary>
        private int PatRaw() { return HandCard(_patRaw) == _patCard ? _patRaw : _patCard; }

        // ------------------------------------------------------------------ playback

        public void _PlayStep()
        {
            if (manager.phase != BattleManager.PHASE_PLAYBACK || _playIndex >= manager.actCount)
            {
                FinishPlayback();
                return;
            }

            _stepStart = _playIndex;
            _stepStyled = false;
            _stepCine = false;
            _castAtOn = false;
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
            // A monster acting rears up / raises its arms and calls out.
            if (manager.IsCastAct(_playIndex))
            {
                MonsterRig caster = RigOf(actor);
                if (caster != null) caster.Cast();
                // A wizard casting: their Surge aura (if they wear one) blasts outward.
                if (cosmetics != null && actor >= 0 && actor < manager.PlayerSeats() && manager.slotIds[actor] > 0)
                {
                    PlayerCosmetics pc = cosmetics.CosmeticsOf(manager.slotIds[actor]);
                    if (pc != null) pc.Flare();
                }
            }
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

            // A fizzle starts like any cast (the symbol being drawn, the cast sound), then sputters out part-way.
            if (kind == BattleManager.KIND_FIZZLE && card > 0)
            {
                int fs = cardDatabase.GetSchool(card);
                SetBanner(manager.SlotName(actor) + " casts " + cardDatabase.GetName(card) + "...");
                RefreshLabels();
                StartCast(actor, actor, fs);
                _burstDone = true;   // no release burst: it never gets that far
                SendCustomEventDelayedSeconds(nameof(_FizzleStep), manager.FizzleCut(fs));
                return;
            }

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
            // The spell's animation (if it has one) plays after the wind-up, in place of the plain orb (_LandStep).
            _stepAnim = cardDatabase.GetAnimation(card);
            _stepCard = card;
            // An attack spell plays its own choreography (SpellCinematic); a wizard's as a cinematic.
            if (cardDatabase.CineStyle(card) != 0 && spellCinematic != null && seats != null && actor >= 0 && actor < seats.Length)
            {
                PlayStyled(actor, target, card);
                return;
            }
            if (_stepAnim != null && cardDatabase.AnimMode(card) == 4)
            {
                // A hand-made prefab: it plays from the caster as the spell lands.
                PlaySpellAnimation(_stepAnim, actor, target);
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
            if (_stepAnim != null)
            {
                // The spell's own animation: the creature charges, the bolt flies, the rain falls; the effect lands at its strike.
                StartImpact();
                _animImpact = true;
                PlaySpellAnimation(_stepAnim, _castFrom, _castTo);
                SendCustomEventDelayedSeconds(nameof(_AnimStrike), Mathf.Max(0.05f, cardDatabase.AnimStrike(_stepCard)));
                SendCustomEventDelayedSeconds(nameof(_PlayStep), Mathf.Max(0.3f, cardDatabase.AnimSeconds(_stepCard)));
                return;
            }
            StartImpact();
            // Blades, traps and shields this hit uses spring into action first; the hit lands a moment later.
            if (StartFxUse()) SendCustomEventDelayedSeconds(nameof(_ApplyLanded), Mathf.Min(fxUseSeconds, manager.impactSeconds * 0.8f));
            else ApplyStep();
            SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.impactSeconds);
        }

        /// <summary>
        /// An attack spell's turn, played by SpellCinematic from its recipe (the same beats on every client): a cinematic,
        /// cast from the middle of the ring to the target. A wizard caster is taken there and back by their own client, a
        /// monster's body is moved there on every client, and everyone else in the fight watches it on the film screen.
        /// BattleClient still draws the symbol and plays the cast sound through the wind-up, lands the results at the
        /// strike and sends a summoned creature along the spell's path.
        /// </summary>
        private void PlayStyled(int actor, int target, int card)
        {
            int school = cardDatabase.GetSchool(card);
            _stepStyled = true;
            _stepCine = manager.IsCineAct(_stepStart) && arenaCenter != null;
            _stepActor = actor;
            _stepTarget = target;
            _stepSchool = school;
            Vector3 aim = StepAim(actor, target, card);
            Vector3 from = seats[actor].position;
            if (_stepCine)
            {
                from = new Vector3(arenaCenter.position.x, seats[actor].position.y, arenaCenter.position.z);
                Vector3 f = aim - from;
                f.y = 0f;
                _castAt = from;
                _castAtFwd = f.sqrMagnitude > 0.001f ? f.normalized : seats[actor].forward;
                _castAtSlot = actor;
                _castAtOn = true;
            }
            Vector3 fwd = aim - from;
            VRCPlayerApi local = Networking.LocalPlayer;
            // The caster (a wizard on this client) gets the fade and the move; everyone else in the fight watches the film.
            bool me = _stepCine && Utilities.IsValid(local) && manager.IsPlayerSeat(actor) && manager.slotIds[actor] == local.playerId;
            bool watch = _stepCine && !me && _inBattle && _seated && !_leaving;
            // A monster steps into the middle of the ring to cast too.
            Transform body = !manager.IsPlayerSeat(actor) && manager.MonsterAt(actor) > 0 ? BodyOf(actor) : null;
            if (body != null && !body.gameObject.activeInHierarchy) body = null;
            bool creature = cardDatabase.AnimMode(card) == 1 && _stepAnim != null;
            float targetScale = target >= 0 && target < BattleManager.SLOTS && target != actor ? SeatScale(target) : 1f;
            float inSec = _stepCine ? manager.cineInSeconds : 0f;
            float after = _stepCine ? manager.CineAfter(card) : manager.plainAfterSeconds;
            float outSec = _stepCine ? manager.cineOutSeconds : 0f;
            spellCinematic.Prepare(this, me, watch, _stepCine, seats[actor], body, creature, targetScale);
            // An area spell: every living combatant on the side it hits, so its bolts and bursts land on each of them.
            if (cardDatabase.IsAoe(card) && target >= 0 && target < BattleManager.SLOTS && target != actor)
            {
                int side = manager.SideOf(target) * BattleManager.SIDE;
                int n = 0;
                for (int s = side; s < side + BattleManager.SIDE && n < _aoeTargets.Length; s++)
                    if (manager.Occupied(s) && manager.Alive(s) && s < seats.Length) _aoeTargets[n++] = TargetPoint(s);
                spellCinematic.SetTargets(_aoeTargets, n);
            }
            spellCinematic.Begin(from, fwd, aim, school, cardDatabase.CineStyle(card), cardDatabase.GetPipCost(card),
                cardDatabase.IsAoe(card), SeatScale(actor), manager.SlotName(actor), cardDatabase.GetName(card),
                inSec, manager.CastSeconds(school), cardDatabase.CineStrike(card), cardDatabase.CineRelease(card), after, outSec);
            if (inSec > 0f) SendCustomEventDelayedSeconds(nameof(_StyledWindUp), inSec);
            else _StyledWindUp();
            SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.StepSeconds(_stepStart));
        }

        /// <summary>
        /// Where a combatant really stands: a monster's (or minion's) body when it's out, else its seat, on the seat's floor.
        /// Spells and their impacts are aimed here.
        /// </summary>
        private Vector3 TargetPoint(int s)
        {
            Vector3 seat = seats[s].position;
            Transform body = !manager.IsPlayerSeat(s) ? BodyOf(s) : null;
            if (body == null || !body.gameObject.activeInHierarchy) return seat;
            Vector3 p = body.position;
            Vector3 d = p - seat;
            d.y = 0f;
            // A body far from its seat is on the move (walking to the middle to cast, or a minion still arriving): use the seat.
            return d.sqrMagnitude < 9f ? new Vector3(p.x, seat.y, p.z) : seat;
        }

        /// <summary>Where an attack spell lands: its target, or the middle of the side an area spell hits.</summary>
        private Vector3 StepAim(int actor, int target, int card)
        {
            if (target < 0 || target >= seats.Length || target == actor)
                return arenaCenter != null ? arenaCenter.position + seats[actor].forward * 3f : seats[actor].position + seats[actor].forward * 4f;
            if (!cardDatabase.IsAoe(card) || target >= BattleManager.SLOTS) return TargetPoint(target);
            int side = manager.SideOf(target) * BattleManager.SIDE;
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int s = side; s < side + BattleManager.SIDE; s++)
                if (manager.Occupied(s) && manager.Alive(s) && s < seats.Length) { sum += seats[s].position; n++; }
            return n > 0 ? sum / n : seats[target].position;
        }

        public void _StyledWindUp()
        {
            if (!_playing || !_stepStyled || manager.phase != BattleManager.PHASE_PLAYBACK) return;
            // A monster in the middle of the ring rears up / raises its arms again as it casts there.
            if (_stepCine)
            {
                MonsterRig rig = RigOf(_stepActor);
                if (rig != null) rig.Cast();
            }
            StartCast(_stepActor, _stepTarget, _stepSchool);
            SendCustomEventDelayedSeconds(nameof(_StyledRelease), manager.CastSeconds(_stepSchool));
        }

        /// <summary>The wind-up's done: the symbol goes, the rig carries the spell (a creature rides its path) to the strike.</summary>
        public void _StyledRelease()
        {
            if (!_playing || !_stepStyled || manager.phase != BattleManager.PHASE_PLAYBACK) return;
            StartImpact();
            _animImpact = true;   // no plain orb: the rig carries it
            int mode = cardDatabase.AnimMode(_stepCard);
            if (_stepAnim != null && (mode == 1 || mode == 4))
            {
                PlaySpellAnimation(_stepAnim, _castFrom, _castTo);
                if (mode == 1)
                {
                    _fxDriven = true;
                    _fxSeconds = cardDatabase.CineRelease(_stepCard) + 0.1f;
                    if (_fxCircle != null) _fxCircle.gameObject.SetActive(false);
                    if (_fxArea != null) _fxArea.gameObject.SetActive(false);
                    spellCinematic.Carry(_fxSummon, _fxRigAnim, _fxKind == RIG_FLYER);
                }
            }
            SendCustomEventDelayedSeconds(nameof(_StyledStrike), Mathf.Max(0.05f, cardDatabase.CineStrike(_stepCard)));
        }

        /// <summary>The attack spell hits: its shields / blades / traps move in, then the results land.</summary>
        public void _StyledStrike()
        {
            if (!_stepStyled || manager.phase != BattleManager.PHASE_PLAYBACK) return;
            if (StartFxUse()) SendCustomEventDelayedSeconds(nameof(_ApplyLanded), fxUseSeconds * 0.5f);
            else ApplyStep();
        }

        /// <summary>Where a caster casts from: their seat, or the middle of the ring during their cinematic.</summary>
        private Vector3 CasterPos(int from) { return _castAtOn && from == _castAtSlot ? _castAt : seats[from].position; }

        private Vector3 CasterFwd(int from) { return _castAtOn && from == _castAtSlot ? _castAtFwd : seats[from].forward; }

        /// <summary>The animated spell strikes: its effect bursts on the target and the step's results land.</summary>
        public void _AnimStrike()
        {
            if (manager.phase != BattleManager.PHASE_PLAYBACK) return;
            if (_fxArea != null && (_fxMode == 1 || _fxMode == 2))
            {
                _fxArea.position = _fxAreaPos;
                _fxArea.gameObject.SetActive(true);
            }
            if (_fxBolt != null) _fxBolt.gameObject.SetActive(false);
            if (StartFxUse()) SendCustomEventDelayedSeconds(nameof(_ApplyLanded), fxUseSeconds * 0.5f);
            else ApplyStep();
        }

        /// <summary>The hit lands after its shields / blades / traps have moved in (they break as it lands).</summary>
        public void _ApplyLanded()
        {
            EndFxUse();
            if (manager.phase == BattleManager.PHASE_PLAYBACK) ApplyStep();
        }

        /// <summary>
        /// Every shield, blade and trap logged against this step's hits moves into place: a blade lunges from its owner
        /// toward the target, a shield swings in front of its owner facing the attacker, a trap leaps up over its owner's
        /// head. Returns false when the step uses none.
        /// </summary>
        private bool StartFxUse()
        {
            _useCount = 0;
            if (!fx3D || fxIcons == null || seats == null) return false;
            if (_fxUsing == null || _fxUsing.Length != fxIcons.Length) _fxUsing = new bool[fxIcons.Length];
            int max = BattleManager.FX_MAX;
            for (int j = 0; j < manager.fxLogCount && j < BattleManager.MAX_FX_LOG && _useCount < MAX_FX_USE; j++)
            {
                int act = manager.fxLogAct[j];
                if (act < _stepStart || act >= _stepEnd) continue;
                int slot = manager.fxLogSlot[j], kind = manager.fxLogKind[j], v = manager.fxLogValue[j];
                if (slot < 0 || slot >= BattleManager.SLOTS || kind < 0 || kind > 2) continue;
                int[] arr = kind == BattleManager.FX_SHIELD ? _fxSh : (kind == BattleManager.FX_BLADE ? _fxBl : _fxTr);
                int k = -1;
                for (int q = 0; q < max; q++)
                {
                    int ix = (slot * 3 + kind) * max + q;
                    if (arr[slot * max + q] == v && ix < _fxUsing.Length && !_fxUsing[ix]) { k = q; break; }
                }
                if (k < 0) continue;
                int idx = (slot * 3 + kind) * max + k;
                if (idx >= fxIcons.Length || fxIcons[idx] == null || !fxIcons[idx].gameObject.activeInHierarchy) continue;
                int caster = manager.actActor[act], target = manager.actTarget[act];
                int other = kind == BattleManager.FX_BLADE ? target : caster;
                if (other < 0 || other >= seats.Length || other == slot) other = -1;
                Vector3 home = seats[slot].position;
                Vector3 dir = other >= 0 ? seats[other].position - home : seats[slot].forward;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
                float sc = SeatScale(slot);
                Vector3 to;
                if (kind == BattleManager.FX_BLADE) to = home + Vector3.up * (1.4f * sc) + dir * (0.9f * sc);
                else if (kind == BattleManager.FX_SHIELD) to = home + Vector3.up * (1.2f * sc) + dir * (0.8f * sc);
                else to = home + Vector3.up * (2.4f * sc);
                _fxUsing[idx] = true;
                _useIdx[_useCount] = idx;
                _useKind[_useCount] = kind;
                _useFrom[_useCount] = fxIcons[idx].transform.position;
                _useTo[_useCount] = to;
                _useAim[_useCount] = dir;
                _useCount++;
            }
            _useStart = Time.time;
            return _useCount > 0;
        }

        private void EndFxUse()
        {
            if (_fxUsing != null)
                for (int u = 0; u < _useCount; u++)
                    if (_useIdx[u] < _fxUsing.Length) _fxUsing[_useIdx[u]] = false;
            _useCount = 0;
        }

        private void AnimateFxUse()
        {
            if (_useCount == 0 || fxIcons == null) return;
            float t = Mathf.Clamp01((Time.time - _useStart) / Mathf.Max(0.1f, fxUseSeconds));
            float e = 1f - (1f - t) * (1f - t);
            for (int u = 0; u < _useCount; u++)
            {
                Renderer r = fxIcons[_useIdx[u]];
                if (r == null) continue;
                Transform tr = r.transform;
                Vector3 aim = _useAim[u];
                if (_useKind[u] == BattleManager.FX_BLADE)
                {
                    // The sword swings round to point at its target and lunges.
                    tr.position = Vector3.Lerp(_useFrom[u], _useTo[u], e);
                    tr.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, aim, e));
                }
                else if (_useKind[u] == BattleManager.FX_SHIELD)
                {
                    // The shield swings in front of its owner, facing the attacker, and flares up.
                    tr.position = Vector3.Lerp(_useFrom[u], _useTo[u], e);
                    tr.rotation = Quaternion.LookRotation(aim, Vector3.up);
                }
                else
                {
                    // The trap leaps up over its owner, spinning, and snaps shut.
                    Vector3 p = Vector3.Lerp(_useFrom[u], _useTo[u], e);
                    tr.position = p + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.3f;
                    tr.rotation = Quaternion.Euler(0f, Time.time * 720f, 0f);
                }
                float grow = 1f + 0.5f * Mathf.Sin(t * Mathf.PI * 0.5f);
                if (_useKind[u] == BattleManager.FX_TRAP && t > 0.8f) grow *= 1f - (t - 0.8f) * 2.5f;
                tr.localScale = Vector3.one * (_fxBaseScale[_useKind[u]] * grow);
            }
        }

        /// <summary>The fizzle: the cast sound cuts off, the fizzle sound plays, the symbol sputters out in a puff of smoke.</summary>
        public void _FizzleStep()
        {
            if (manager.phase != BattleManager.PHASE_PLAYBACK) { FinishPlayback(); return; }
            if (castAudio != null)
            {
                castAudio.Stop();
                if (fizzleSound != null)
                {
                    castAudio.clip = fizzleSound;
                    castAudio.Play();
                }
            }
            if (_casting)
            {
                _fizzling = true;
                _fizzleStart = Time.time;
                SetPen(false);
                if (fizzlePuff != null && castGlyph != null)
                {
                    fizzlePuff.transform.position = castGlyph.transform.position;
                    fizzlePuff.Play();
                }
            }
            ApplyStep();
            SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.fizzleAfterSeconds);
        }

        private void FinishPlayback()
        {
            _playing = false;
            _stepStyled = false;
            _castAtOn = false;
            if (spellCinematic != null && spellCinematic.IsFor(this)) spellCinematic.Abort(_inBattle);
            EndFxUse();
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
            if ((f & BattleManager.FLAG_STUNNED) != 0) t += "  STUNNED!";
            if ((f & BattleManager.FLAG_STUNBLOCK) != 0) t += "  (stun blocked)";
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
            if (e == CardDatabase.EFFECT_HACK) return " hacks in and steals " + v.ToString() + (v == 1 ? " charm" : " charms");
            if (e == CardDatabase.EFFECT_FIELD) return " raises the " + cardDatabase.GetName(card) + ": " + cardDatabase.SchoolName(cardDatabase.GetSchool(card)) + " spells +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_TAUNT) return " taunts the monsters and gains a " + v.ToString() + "% shield";
            if (e == CardDatabase.EFFECT_CURSE || e == CardDatabase.EFFECT_TRITRAP) return " is trapped (+" + v.ToString() + "%)";
            if (e == CardDatabase.EFFECT_STRIKEFX && cardDatabase.GetAux(card) / 1000 == 2) return " is trapped (+" + v.ToString() + "%)";
            return " gains " + BuffName(card) + " (" + v.ToString() + "%)";
        }

        private string BuffName(int card)
        {
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_WARD) return "a shield";
            if (e == CardDatabase.EFFECT_EMPOWER) return "a blade";
            if (e == CardDatabase.EFFECT_TRIBLADE) return "3 blades";
            if (e == CardDatabase.EFFECT_TRAP) return "a trap";
            if (e == CardDatabase.EFFECT_MULTIWARD) return Mathf.Clamp(cardDatabase.GetAux(card), 1, 3).ToString() + " shields";
            if (e == CardDatabase.EFFECT_TRISHIELD) return "3 shields";
            if (e == CardDatabase.EFFECT_HEALBLADE) return "a healing charm";
            if (e == CardDatabase.EFFECT_STRIKEFX) return cardDatabase.GetAux(card) / 1000 == 0 ? "a shield" : "a blade";
            return "Focus";
        }

        private void PlaySpellAnimation(GameObject anim, int from, int to)
        {
            _HideSpellAnimation();
            if (seats == null || from < 0 || from >= seats.Length) return;
            Vector3 pos = CasterPos(from);
            Vector3 aim = to >= 0 && to < seats.Length && to != from ? seats[to].position : (arenaCenter != null ? arenaCenter.position : pos + CasterFwd(from));
            Vector3 dir = aim - pos;
            dir.y = 0f;
            anim.transform.position = pos;
            if (dir.sqrMagnitude > 0.001f) anim.transform.rotation = Quaternion.LookRotation(dir);
            _fxMode = cardDatabase.AnimMode(_stepCard);
            _fxStrike = Mathf.Max(0.2f, cardDatabase.AnimStrike(_stepCard));
            _fxSeconds = Mathf.Max(_fxStrike + 0.2f, cardDatabase.AnimSeconds(_stepCard));
            _fxStart = Time.time;
            _fxFrom = pos;
            _fxTo = to >= 0 && to < seats.Length ? seats[to].position : aim;
            // An area spell lands on the middle of the side it hits.
            _fxAreaPos = _fxTo;
            if (to >= 0 && to < BattleManager.SLOTS && cardDatabase.IsAoe(_stepCard))
            {
                int side = manager.SideOf(to) * BattleManager.SIDE;
                Vector3 sum = Vector3.zero;
                int n = 0;
                for (int s = side; s < side + BattleManager.SIDE; s++)
                    if (manager.Occupied(s) && s < seats.Length) { sum += seats[s].position; n++; }
                if (n > 0) _fxAreaPos = sum / n;
            }
            Transform t = anim.transform;
            _fxSummon = t.Find("Summon");
            _fxBolt = t.Find("Projectile");
            _fxArea = t.Find("Area");
            _fxCircle = t.Find("Circle");
            _fxBeam = _fxCircle != null ? _fxCircle.Find("Beam") : null;
            _fxBurstT = t.Find("Burst");
            _fxBurst = _fxBurstT != null ? _fxBurstT.GetComponent<ParticleSystem>() : null;
            Transform rig = _fxSummon != null ? _fxSummon.Find("Rig") : null;
            _fxRigAnim = rig != null ? rig.GetComponent<Animator>() : null;
            _fxKind = cardDatabase.AnimKind(_stepCard);
            _fxAoe = cardDatabase.IsAoe(_stepCard);
            _fxCue = 0;
            // A creature is summoned a little in front of its caster and strikes its target (the middle of the side, for
            // spells that hit everyone).
            Vector3 fwd = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            _fxPad = pos + fwd * 1.8f;
            _fxGoal = _fxAoe ? _fxAreaPos : _fxTo;
            if (_fxSummon != null) { _fxSummon.gameObject.SetActive(_fxMode == 1); _fxSummon.position = _fxPad + Vector3.down * 2f; _fxSummon.rotation = Quaternion.LookRotation(fwd); _fxSummon.localScale = Vector3.one * 0.01f; }
            if (_fxBolt != null) { _fxBolt.gameObject.SetActive(_fxMode == 2); _fxBolt.position = pos + Vector3.up * 2.3f; _fxBolt.localScale = Vector3.one * 0.01f; }
            if (_fxArea != null)
            {
                _fxArea.gameObject.SetActive(_fxMode == 3);
                _fxArea.position = _fxAreaPos;
                _fxArea.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : Vector3.forward);
            }
            // The circle: under the creature as it's summoned, or marking where an area spell is about to land.
            if (_fxCircle != null)
            {
                _fxCircle.gameObject.SetActive(_fxMode == 1 || _fxMode == 3);
                _fxCircle.position = _fxMode == 1 ? _fxPad : _fxAreaPos;
                _fxCircle.localScale = Vector3.one * 0.01f;
            }
            if (_fxBeam != null) _fxBeam.localScale = new Vector3(1f, 0.01f, 1f);
            anim.SetActive(true);
            _spellAnim = anim;
        }

        /// <summary>Plays the spell's animation out frame by frame: the creature's beats, the bolt's flight, the circle.</summary>
        private void AnimateSpellFx()
        {
            float t = Time.time - _fxStart;
            if (_fxMode == 1 && _fxSummon != null && !_fxDriven) AnimateCreature(t);
            if (_fxMode == 2 && _fxBolt != null && _fxBolt.gameObject.activeSelf) AnimateBolt(t);
            if (_fxCircle != null && _fxCircle.gameObject.activeSelf) AnimateSummonCircle(t);
            if (t > _fxSeconds + 0.2f) _HideSpellAnimation();
        }

        private float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void Cue(string trigger)
        {
            if (_fxRigAnim != null) _fxRigAnim.SetTrigger(trigger);
        }

        private void Sparkle(Vector3 at, int count)
        {
            if (_fxBurst == null) return;
            _fxBurstT.position = at;
            _fxBurst.Emit(count);
        }

        /// <summary>
        /// A summoned creature's attack, in beats:
        ///   0 - 0.55 s   the circle opens in a pillar of light and the creature rises out of it, growing to full size
        ///   - 0.95 s     it rears up (its cast pose) and draws back
        ///   - strike     the attack, by body: walkers run in and swing, beasts dash and pounce, flyers climb and dive
        ///   strike       it hits (the spell's burst on the target, the results land)
        ///   - end        it's thrown back off the target, hangs a moment, and breaks apart into sparkles
        /// </summary>
        private void AnimateCreature(float t)
        {
            float S = _fxStrike, T = _fxSeconds;
            Vector3 flat = _fxGoal - _fxPad;
            flat.y = 0f;
            float len = flat.magnitude;
            Vector3 dir = len > 0.01f ? flat / len : Vector3.forward;
            float size = _fxAoe ? 1.3f : 1f;
            Vector3 from = _fxPad - dir * 0.35f;
            Vector3 stop = _fxGoal - dir * Mathf.Min(1.4f, len * 0.35f);
            stop.y = _fxGoal.y;

            // One-off cues as each beat starts.
            if (_fxCue == 0 && t >= 0.15f) { _fxCue = 1; Sparkle(_fxPad + Vector3.up * 0.3f, 45); }
            if (_fxCue == 1 && t >= 0.55f) { _fxCue = 2; Cue("Cast"); }
            if (_fxCue == 2 && t >= 0.95f) { _fxCue = 3; if (_fxKind != RIG_FLYER) Cue("Walk"); }
            if (_fxCue == 3 && t >= S - 0.3f) { _fxCue = 4; Cue("Cast"); }
            if (_fxCue == 4 && t >= S) { _fxCue = 5; Cue("Hit"); Sparkle(_fxGoal + Vector3.up * 1.1f, 35); }
            if (_fxCue == 5 && t >= T - 0.55f) { _fxCue = 6; Sparkle(_fxSummon.position + Vector3.up * 0.9f * size, 60); }

            Vector3 p;
            Vector3 face = dir;
            float scale = size;
            if (t < 0.55f)
            {
                float r = Ease((t - 0.15f) / 0.4f);
                p = _fxPad + Vector3.up * Mathf.Lerp(-1.4f, 0f, r) * size;
                scale = Mathf.Lerp(0.45f, 1f, r) * size;
                if (t < 0.15f) scale = 0.01f;
            }
            else if (t < 0.95f)
            {
                float r = (t - 0.55f) / 0.4f;
                p = _fxPad - dir * (Mathf.Sin(r * Mathf.PI * 0.5f) * 0.35f);
            }
            else if (t < S)
            {
                float r = Mathf.Clamp01((t - 0.95f) / Mathf.Max(0.1f, S - 0.95f));
                if (_fxKind == RIG_FLYER)
                {
                    // Up into the air above the field, then a dive at the target.
                    Vector3 high = Vector3.Lerp(from, stop, 0.3f) + Vector3.up * 3.2f * size;
                    Vector3 end = stop + Vector3.up * 0.5f;
                    if (r < 0.4f)
                    {
                        p = Vector3.Lerp(from, high, Ease(r / 0.4f));
                        face = dir;
                    }
                    else
                    {
                        float a = (r - 0.4f) / 0.6f;
                        p = Vector3.Lerp(high, end, a * a);
                        face = (end - high).normalized;
                    }
                }
                else if (_fxKind == RIG_BEAST)
                {
                    // A dash, then a pounce.
                    Vector3 leap = Vector3.Lerp(from, stop, 0.5f);
                    if (r < 0.6f)
                    {
                        float a = r / 0.6f;
                        p = Vector3.Lerp(from, leap, a * a);
                    }
                    else
                    {
                        float a = (r - 0.6f) / 0.4f;
                        p = Vector3.Lerp(leap, stop, a) + Vector3.up * (Mathf.Sin(a * Mathf.PI) * 1.8f * size);
                    }
                }
                else
                {
                    // A charge, bounding, gathering speed into the blow.
                    p = Vector3.Lerp(from, stop, r * r) + Vector3.up * (Mathf.Abs(Mathf.Sin(r * Mathf.PI * 3f)) * 0.15f);
                }
            }
            else
            {
                // Thrown back off the target, then gone in a burst of sparkles.
                float r = Ease((t - S) / 0.35f);
                p = stop - dir * (1.1f * r) + Vector3.up * (Mathf.Sin(Mathf.Clamp01((t - S) / 0.35f) * Mathf.PI) * 0.4f);
                float vanish = Mathf.Clamp01((t - (T - 0.55f)) / 0.5f);
                scale = size * (1f - vanish);
                p += Vector3.up * (vanish * 0.8f);
            }
            if (_fxKind != RIG_FLYER) face.y = 0f;
            _fxSummon.position = p;
            if (face.sqrMagnitude > 0.0001f) _fxSummon.rotation = Quaternion.LookRotation(face);
            _fxSummon.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        /// <summary>A bolt gathers over its caster's head, then streaks in, faster and faster, to burst on the target.</summary>
        private void AnimateBolt(float t)
        {
            float charge = Mathf.Min(_fxStrike * 0.42f, 0.5f);
            Vector3 start = _fxFrom + Vector3.up * 2.3f;
            Vector3 end = _fxTo + Vector3.up * 1.1f;
            if (t < charge)
            {
                _fxBolt.position = start;
                _fxBolt.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.35f, Ease(t / charge));
                if (_fxCue == 0) { _fxCue = 1; Sparkle(start, 30); }
            }
            else
            {
                float o = Mathf.Clamp01((t - charge) / Mathf.Max(0.1f, _fxStrike - charge));
                _fxBolt.position = Vector3.Lerp(start, end, o * o) + Vector3.up * (Mathf.Sin(o * Mathf.PI) * 0.5f);
                _fxBolt.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, o);
            }
            Vector3 d = end - start;
            if (d.sqrMagnitude > 0.001f) _fxBolt.rotation = Quaternion.LookRotation(d);
        }

        /// <summary>
        /// The summoning circle: it spins open with a pillar of light, then closes once the creature is out. Under an area
        /// spell it marks where it will land until the strike.
        /// </summary>
        private void AnimateSummonCircle(float t)
        {
            float open = _fxMode == 1 ? 0.15f : 0.35f;
            float close0 = _fxMode == 1 ? 0.6f : _fxStrike;
            float close1 = close0 + (_fxMode == 1 ? 0.35f : 0.25f);
            float s = t < open ? Ease(t / open) : t < close0 ? 1f : 1f - Ease((t - close0) / (close1 - close0));
            if (t >= close1) { _fxCircle.gameObject.SetActive(false); return; }
            float pulse = 1f + 0.04f * Mathf.Sin(t * 18f);
            _fxCircle.localScale = new Vector3(s * pulse * (_fxAoe ? 1.4f : 1f), 1f, s * pulse * (_fxAoe ? 1.4f : 1f));
            _fxCircle.rotation = Quaternion.Euler(0f, t * 140f, 0f);
            if (_fxBeam != null)
            {
                // The pillar shoots up as the creature starts to rise and thins away as it comes out.
                float h = _fxMode == 1 ? Mathf.Clamp01((t - 0.05f) / 0.15f) * 2.2f : 0.01f;
                float w = _fxMode == 1 ? 1f - Ease((t - 0.25f) / 0.3f) : 0f;
                _fxBeam.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, h), Mathf.Max(0.01f, w));
            }
        }

        public void _HideSpellAnimation()
        {
            if (_spellAnim != null) _spellAnim.SetActive(false);
            if (_fxSummon != null) _fxSummon.gameObject.SetActive(false);
            if (_fxBolt != null) _fxBolt.gameObject.SetActive(false);
            if (_fxArea != null) _fxArea.gameObject.SetActive(false);
            if (_fxCircle != null) _fxCircle.gameObject.SetActive(false);
            _spellAnim = null;
            _animImpact = false;
            _fxDriven = false;
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
            _fizzling = false;
            _burstAt = -100f;
            int mi = fxMats == null || fxMats.Length == 0 ? -1 : (school >= 0 && school < fxMats.Length - 1 ? school : fxMats.Length - 1);
            float scale = SeatScale(from);
            Vector3 at = CasterPos(from);
            _ringBase = at + Vector3.up * 0.15f;
            // The symbol is drawn in the air in front of the caster (seats face the middle of the ring), at chest height.
            _glyphBase = at + CasterFwd(from) * (glyphForward * scale) + Vector3.up * (glyphHeight * scale);
            _drawLen = Mathf.Min(glyphDrawSeconds, _castLen * 0.55f);
            _orbFrom = at + Vector3.up * (1.3f * scale);
            _orbTo = seats[to].position + Vector3.up * (1.1f * SeatScale(to));
            if (castRing != null)
            {
                if (castRingRenderer != null && seatSchoolMats != null && school >= 0 && school < seatSchoolMats.Length)
                    castRingRenderer.sharedMaterial = seatSchoolMats[school];
                // A wizard's casting ring is drawn in their signature colour.
                int sig = SigIndex(from);
                if (castRingRenderer != null && sig > 0)
                {
                    if (_castCustom == null) _castCustom = castRingRenderer.material;
                    else castRingRenderer.sharedMaterial = _castCustom;
                    Color c = LookColor(sig, school);
                    _castCustom.SetColor("_Color", c);
                    _castCustom.SetColor("_EmissionColor", c * 1.6f);
                }
                castRing.position = _ringBase;
                castRing.gameObject.SetActive(true);
            }
            if (castGlyph != null && glyphDrawTex != null && glyphDrawTex.Length > 0)
            {
                int gi = school >= 0 && school < glyphDrawTex.Length - 1 ? school : glyphDrawTex.Length - 1;
                _glyphIndex = gi;
                if (_glyphMat == null) _glyphMat = castGlyph.material;
                _glyphMat.SetTexture("_MainTex", glyphDrawTex[gi]);
                if (glyphColors != null && gi < glyphColors.Length) _glyphMat.SetColor("_Color", glyphColors[gi]);
                if (glyphEdgeColors != null && gi < glyphEdgeColors.Length) _glyphMat.SetColor("_EdgeColor", glyphEdgeColors[gi]);
                // Styles: 0 = General, 1-7 = the schools (see the GlyphDraw shader).
                _glyphMat.SetFloat("_Style", gi == glyphDrawTex.Length - 1 ? 0f : gi + 1f);
                // A spell skin from the Starshard Shop draws the symbol in its colour (same school style).
                int skin = SkinIndex(from);
                if (skin > 0 && cosmetics != null)
                {
                    Color sc = cosmetics.SkinColor(skin);
                    _glyphMat.SetColor("_Color", sc);
                    _glyphMat.SetColor("_EdgeColor", Color.Lerp(sc, Color.white, 0.55f));
                }
                _glyphMat.SetFloat("_Reveal", 0f);
                _glyphMat.SetFloat("_Fade", 1f);
                _glyphMat.SetFloat("_Flare", 0f);
                castGlyph.transform.position = _glyphBase;
                castGlyph.transform.localScale = Vector3.one * (glyphSize * scale);
                castGlyph.gameObject.SetActive(true);
                if (penTipRenderer != null && mi >= 0) penTipRenderer.sharedMaterial = fxMats[mi];
                SetPen(true);
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
            AudioClip clip = school < 0 ? generalCastSound : castSounds != null && school < castSounds.Length ? castSounds[school] : null;
            if (clip == null) return;
            castAudio.transform.position = CasterPos(from) + Vector3.up * 1.2f;
            castAudio.clip = clip;
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
            SetPen(false);
        }

        private void SetPen(bool on)
        {
            _penOn = on;
            if (penTip != null && penTip.gameObject.activeSelf != on) penTip.gameObject.SetActive(on);
        }

        /// <summary>Where the pen is (texture coordinates) when 'r' of the symbol has been drawn; it hops between strokes.</summary>
        private Vector2 PenUV(float r)
        {
            int n = glyphPenSamples;
            int b = _glyphIndex * n;
            if (glyphPenU == null || glyphPenV == null || n < 2 || b + n > glyphPenU.Length || b + n > glyphPenV.Length) return new Vector2(0.5f, 0.5f);
            float f = Mathf.Clamp01(r) * (n - 1);
            int i = Mathf.Min(n - 2, (int)f);
            float a = f - i;
            if (glyphPenJump != null && b + i < glyphPenJump.Length && glyphPenJump[b + i] != 0) a = a < 0.5f ? 0f : 1f;
            return new Vector2(Mathf.Lerp(glyphPenU[b + i], glyphPenU[b + i + 1], a), Mathf.Lerp(glyphPenV[b + i], glyphPenV[b + i + 1], a));
        }

        /// <summary>School-coloured particle explosion off the caster.</summary>
        private void CastBurst()
        {
            _burstDone = true;
            _burstAt = Time.time;
            if (castBursts == null || castBursts.Length == 0) return;
            int i = _castSchool >= 0 && _castSchool < castBursts.Length - 1 ? _castSchool : castBursts.Length - 1;
            ParticleSystem ps = castBursts[i];
            if (ps == null) return;
            float scale = SeatScale(_castFrom);
            ps.transform.position = CasterPos(_castFrom) + Vector3.up * (1.2f * scale);
            ps.Play();
        }

        private void EndCast()
        {
            _casting = false;
            _impacting = false;
            _fizzling = false;
            SetPen(false);
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
                // A fizzle: the casting ring collapses.
                if (_fizzling) rs *= Mathf.Max(0f, 1f - (Time.time - _fizzleStart) / 0.5f);
                castRing.localScale = new Vector3(rs, rs, 1f);
                castRing.rotation = Quaternion.Euler(90f, Time.time * 160f, 0f);
            }
            if (castGlyph != null && _glyphMat != null && castGlyph.gameObject.activeSelf) AnimateGlyph(elapsed, w);
        }

        /// <summary>
        /// The symbol: traced stroke by stroke by a glowing pen tip trailing the school's sparks, then held glowing; it
        /// flares when the spell is released and fades as it lands. A fizzle makes it sputter, grey and sag away instead.
        /// It always faces the viewer, staying upright.
        /// </summary>
        private void AnimateGlyph(float elapsed, float w)
        {
            Transform gt = castGlyph.transform;
            VRCPlayerApi me = Networking.LocalPlayer;
            Vector3 eye = Utilities.IsValid(me) ? me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position : gt.position - Vector3.forward;
            if (_fizzling)
            {
                float ft = (Time.time - _fizzleStart) / 0.6f;
                if (ft >= 1f)
                {
                    castGlyph.gameObject.SetActive(false);
                    if (castRing != null) castRing.gameObject.SetActive(false);
                    return;
                }
                bool on = Random.value > 0.3f + ft * 0.4f;
                _glyphMat.SetFloat("_Fade", on ? (1f - ft) : 0.08f);
                Color c = glyphColors != null && _glyphIndex < glyphColors.Length ? glyphColors[_glyphIndex] : Color.white;
                _glyphMat.SetColor("_Color", Color.Lerp(c, new Color(0.3f, 0.3f, 0.32f), ft));
                _glyphMat.SetColor("_EdgeColor", Color.Lerp(c, new Color(0.35f, 0.35f, 0.38f), ft));
                gt.position = _glyphBase - Vector3.up * (ft * ft * 0.5f);
                gt.rotation = Upright(gt.position - eye, gt.rotation);
                return;
            }

            float reveal = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, _drawLen));
            float fade = w < 0.9f ? 1f : Mathf.Lerp(1f, 0f, (w - 0.9f) / 0.1f);
            float flare = Mathf.Max(0f, 2.2f - (Time.time - _burstAt) * 6f);
            _glyphMat.SetFloat("_Reveal", reveal < 1f ? reveal : 1.05f);
            _glyphMat.SetFloat("_Fade", fade);
            _glyphMat.SetFloat("_Flare", flare);
            // A slight swell as it's released.
            float swell = 1f + Mathf.Min(0.12f, flare * 0.06f);
            gt.position = _glyphBase;
            gt.localScale = Vector3.one * (glyphSize * SeatScale(_castFrom) * swell);
            gt.rotation = Upright(gt.position - eye, gt.rotation);

            if (reveal >= 1f) { if (_penOn) SetPen(false); return; }
            Vector2 uv = PenUV(reveal);
            // Just in front of the symbol (toward the viewer), so the tip and its sparks aren't hidden in the strokes.
            Vector3 p = gt.TransformPoint(new Vector3(uv.x - 0.5f, uv.y - 0.5f, -0.02f));
            if (penTip != null) penTip.position = p;
            if (glyphTrails != null && _glyphIndex < glyphTrails.Length && glyphTrails[_glyphIndex] != null)
            {
                ParticleSystem tr = glyphTrails[_glyphIndex];
                tr.transform.position = p;
                tr.Emit(2);
            }
        }

        private void AnimateImpact()
        {
            float t = (Time.time - _impactStart) / Mathf.Max(0.1f, manager.impactSeconds);
            if (t >= 0.95f || _animImpact) { EndCast(); return; }
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
                        if (_fxUsing != null && idx < _fxUsing.Length && _fxUsing[idx]) continue;
                        int f = manager.FxSchool(arr[s * max + k]);
                        Material want = fx3D ? FxMat(f) : IconMat(fxIconMats, kind, f);
                        if (want == null) want = FxMat(f);
                        if (r.sharedMaterial != want) r.sharedMaterial = want;
                        if (fx3D) r.transform.localScale = Vector3.one * _fxBaseScale[kind];
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
            if (fxIcons == null) return;
            AnimateFxUse();
            if (fx3D)
            {
                // 3D: shields face outward as they circle, swords stand point-up and twirl, traps lie flat and turn.
                for (int i = 0; i < fxIcons.Length; i++)
                {
                    Renderer r = fxIcons[i];
                    if (r == null || !r.gameObject.activeSelf || (_fxUsing != null && i < _fxUsing.Length && _fxUsing[i])) continue;
                    Transform tr = r.transform;
                    int kind = (i / BattleManager.FX_MAX) % 3;
                    float bob = Mathf.Sin(Time.time * 2.4f + i) * 0.05f;
                    Vector3 lp = tr.localPosition;
                    lp.y = bob;
                    tr.localPosition = lp;
                    if (kind == BattleManager.FX_SHIELD)
                    {
                        Vector3 outward = new Vector3(lp.x, 0f, lp.z);
                        tr.localRotation = outward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(outward) * Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.7f + i) * 12f, 0f) : Quaternion.identity;
                    }
                    else if (kind == BattleManager.FX_BLADE) tr.localRotation = Quaternion.Euler(0f, Time.time * 160f + i * 40f, 0f);
                    else tr.localRotation = Quaternion.Euler(0f, Time.time * 60f + i * 25f, 0f);
                }
                return;
            }
            // Flat icons circle the combatant but always face whoever is watching (like Wizard101's).
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            for (int i = 0; i < fxIcons.Length; i++)
            {
                Renderer r = fxIcons[i];
                if (r == null || !r.gameObject.activeSelf) continue;
                r.transform.rotation = Upright(r.transform.position - head, r.transform.rotation);
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
            if (school == CardDatabase.HEAL_SCHOOL) school = 3;   // a healing charm looks like a Life blade
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
                bool show = !Pvp() && phase >= BattleManager.PHASE_ROAMING && !manager.IsSummon(s) && KeepBody(s) &&
                            !(manager.round == 0 && Time.time < _revealAt);
                if (monsterBodies[m].activeSelf != show) monsterBodies[m].SetActive(show);
            }
        }

        /// <summary>The patrol monster has walked over to the ring: show the seated monsters.</summary>
        public void _RevealBodies()
        {
            if (manager != null) RefreshBodies(manager.phase);
        }

        private bool KeepBody(int s)
        {
            if (!manager.Occupied(s)) return false;
            if (manager.hp[s] > 0 || _dieStart[s] > 0f) return true;
            bool playback = _playing || manager.phase == BattleManager.PHASE_PLAYBACK;
            return playback && _shownHp[s] > 0;
        }

        /// <summary>The animated body of the monster in seat s (null for wizards, summons and bodies without one).</summary>
        private MonsterRig RigOf(int s)
        {
            if (s < BattleManager.SIDE || s >= BattleManager.SLOTS || monsterRigs == null || manager.IsSummon(s)) return null;
            int m = s - BattleManager.SIDE;
            if (m >= monsterRigs.Length || monsterRigs[m] == null || !monsterRigs[m].gameObject.activeInHierarchy) return null;
            return monsterRigs[m];
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
                bool occupied = manager.slotIds[s] != 0;
                if (occupied && seatGaugeMat != null)
                {
                    // Occupied: the ring becomes this seat's own health gauge (a per-seat copy of the material).
                    if (_gaugeMats[s] == null)
                    {
                        r.sharedMaterial = seatGaugeMat;
                        _gaugeMats[s] = r.material;
                        _gaugeMats[s].SetColor("_FrameColor", s >= BattleManager.SIDE ? gaugeFrameMonster : gaugeFrameWizard);
                    }
                    // The seat's frame takes a wizard's signature colour.
                    int frameKey = manager.slotIds[s] * 16 + SigIndex(s);
                    if (_frameKey[s] != frameKey)
                    {
                        _frameKey[s] = frameKey;
                        int sig = SigIndex(s);
                        _gaugeMats[s].SetColor("_FrameColor", sig > 0 ? LookColor(sig, 0) * 0.85f : (s >= BattleManager.SIDE ? gaugeFrameMonster : gaugeFrameWizard));
                    }
                    if (r.sharedMaterial != _gaugeMats[s]) r.sharedMaterial = _gaugeMats[s];
                    // A new occupant starts with the gauge at their health (no drain from whoever sat here before).
                    if (_gaugeOwner[s] != manager.slotIds[s] || _gaugeBattle[s] != manager.battleId)
                    {
                        _gaugeOwner[s] = manager.slotIds[s];
                        _gaugeBattle[s] = manager.battleId;
                        _gFill[s] = HealthShare(s);
                        _gTrail[s] = _gFill[s];
                        _gSent[s] = -1f;
                        _gColor[s] = -1;
                    }
                    continue;
                }
                _gaugeOwner[s] = 0;
                Material m = occupied && seatSchoolMats != null && sc >= 0 && sc < seatSchoolMats.Length ? seatSchoolMats[sc] : empty;
                if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        private Material[] _gaugeMats = new Material[BattleManager.SLOTS];
        private int[] _frameKey = new int[BattleManager.SLOTS];
        private Material[] _auraMats = new Material[BattleManager.SLOTS];
        private int[] _auraKey = new int[BattleManager.SLOTS];
        private Material _castCustom;

        // ------------------------------------------------------------------ creator colours

        /// <summary>A wizard seat's signature colour index (0 = none chosen, or not a wizard).</summary>
        private int SigIndex(int s)
        {
            if (manager == null || !manager.IsPlayerSeat(s) || manager.slotIds[s] <= 0 || manager.look == null || s >= manager.look.Length) return 0;
            return manager.look[s] & 0xFF;
        }

        /// <summary>A wizard seat's spell skin (index + 1; 0 = their school's own look).</summary>
        private int SkinIndex(int s)
        {
            if (manager == null || !manager.IsPlayerSeat(s) || manager.slotIds[s] <= 0 || manager.look == null || s >= manager.look.Length) return 0;
            return (manager.look[s] >> 16) & 0xF;
        }

        private int AuraIndex(int s)
        {
            if (manager == null || !manager.IsPlayerSeat(s) || manager.slotIds[s] <= 0 || manager.look == null || s >= manager.look.Length) return 0;
            return (manager.look[s] >> 8) & 0xFF;
        }

        /// <summary>A palette colour; index 0 (or out of range) gives the school's colour.</summary>
        private Color LookColor(int index, int school)
        {
            if (index > 0 && lookPalette != null && index < lookPalette.Length) return lookPalette[index];
            if (schoolColors != null && school >= 0 && school < schoolColors.Length) return schoolColors[school];
            return Color.white;
        }

        /// <summary>Colours a seat's aura motes for whoever sits there (once per change).</summary>
        private void TintAura(int s)
        {
            if (auraRenderers == null || s >= auraRenderers.Length || auraRenderers[s] == null) return;
            int key = manager.slotIds[s] * 4096 + AuraIndex(s) * 16 + manager.school[s] + 1;
            if (_auraKey[s] == key) return;
            _auraKey[s] = key;
            if (_auraMats[s] == null) _auraMats[s] = auraRenderers[s].material;
            Color c = LookColor(AuraIndex(s), manager.school[s]);
            _auraMats[s].SetColor("_Color", c);
            _auraMats[s].SetColor("_EmissionColor", c * 2.2f);
        }
        private int[] _gaugeOwner = new int[BattleManager.SLOTS];
        private int[] _gaugeBattle = new int[BattleManager.SLOTS];
        private float[] _gFill = new float[BattleManager.SLOTS];
        private float[] _gTrail = new float[BattleManager.SLOTS];
        private float[] _gHold = new float[BattleManager.SLOTS];
        private float[] _gSent = new float[BattleManager.SLOTS];
        private int[] _gColor = new int[BattleManager.SLOTS];

        private float HealthShare(int s)
        {
            int max = manager.maxHp[s];
            return max > 0 ? Mathf.Clamp01((float)_shownHp[s] / max) : 0f;
        }

        /// <summary>
        /// Seat gauges follow the shown health: damage drops the fill at once and leaves the lost part lit for a moment
        /// before it drains; heals fill back in. Low health blinks red, in PSX steps.
        /// </summary>
        private void AnimateGauges()
        {
            if (seatRings == null) return;
            float dt = Time.deltaTime;
            bool blinkOn = Mathf.FloorToInt(Time.time * 4f) % 2 == 0;
            for (int s = 0; s < BattleManager.SLOTS && s < seatRings.Length; s++)
            {
                Material g = _gaugeMats[s];
                if (g == null || _gaugeOwner[s] == 0) continue;
                float target = HealthShare(s);
                if (target < _gFill[s])
                {
                    _gFill[s] = target;
                    _gHold[s] = Time.time + gaugeTrailHold;
                }
                else if (target > _gFill[s]) _gFill[s] = Mathf.Min(target, _gFill[s] + gaugeHealSpeed * dt);
                if (_gTrail[s] < _gFill[s]) _gTrail[s] = _gFill[s];
                else if (Time.time >= _gHold[s]) _gTrail[s] = Mathf.Max(_gFill[s], _gTrail[s] - gaugeTrailSpeed * dt);

                // Only touch the material when something visible changed.
                float key = _gFill[s] * 1000f + _gTrail[s];
                if (key != _gSent[s])
                {
                    _gSent[s] = key;
                    g.SetFloat("_Fill", _gFill[s]);
                    g.SetFloat("_Trail", _gTrail[s]);
                }
                int sc = manager.school[s];
                bool low = target > 0f && target <= gaugeLowShare;
                int colorKey = low && blinkOn ? -2 : sc;
                if (colorKey != _gColor[s])
                {
                    _gColor[s] = colorKey;
                    Color c = colorKey == -2 ? gaugeLowColor
                        : (gaugeSchoolColors != null && sc >= 0 && sc < gaugeSchoolColors.Length ? gaugeSchoolColors[sc] : Color.white);
                    g.SetColor("_FillColor", c);
                }
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
                label = "Choose your spells";
                UpdateTimer(manager.SecondsLeft(), true);
            }
            else if (phase == BattleManager.PHASE_PLAYBACK && _clockActor >= 0 && _clockActor < seats.Length)
            {
                Vector3 d = arenaCenter.InverseTransformPoint(seats[_clockActor].position);
                target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                label = manager.SlotName(_clockActor) + "'s turn";
                UpdateTimer(0, false);
            }
            else
            {
                UpdateTimer(0, false);
                target = 0f;
                label = phase == BattleManager.PHASE_JOINING ? "Battle!" : "";
            }
            // The dial's rim: full while the turn plays out, draining as the planning time runs down, dark when idle.
            float remaining = phase == BattleManager.PHASE_PLANNING
                ? Mathf.Clamp01((manager.phaseEndMs - manager.Now()) / (manager.planningSeconds * 1000f))
                : 1f;
            if (clockFace != null && Mathf.Abs(remaining - _clockShown) > 0.002f)
            {
                if (_clockMat == null) _clockMat = clockFace.material;   // this client's own copy
                _clockMat.SetFloat("_Remaining", remaining);
                _clockShown = remaining;
            }
            Quaternion want = Quaternion.Euler(0f, target, 0f);
            clockHand.localRotation = phase == BattleManager.PHASE_PLANNING ? want : Quaternion.RotateTowards(clockHand.localRotation, want, 540f * Time.deltaTime);
            if (clockLabel != null) clockLabel.text = label;
        }

        /// <summary>
        /// The round timer over the banner: the seconds left while spells are chosen, gold, then red for the last ten, when
        /// each new second ticks (a brighter tick for the last three) and the number pops. Blank otherwise.
        /// </summary>
        private void UpdateTimer(int secs, bool on)
        {
            if (timerLabel == null) return;
            string want = on ? secs.ToString() : "";
            if (timerLabel.text != want) timerLabel.text = want;
            if (!on) { _tickedSecond = -1; _timerPulse = 0f; return; }
            bool late = secs <= 10;
            timerLabel.color = late ? new Color(1f, 0.34f, 0.26f) : new Color(1f, 0.86f, 0.45f);
            if (secs != _tickedSecond)
            {
                // A new second in the last ten (not the first one seen, so arriving late doesn't tick).
                if (_tickedSecond >= 0 && late && secs > 0)
                {
                    _timerPulse = 1f;
                    AudioClip clip = secs <= 3 && clockTickLast != null ? clockTickLast : clockTick;
                    if (clockAudio != null && clip != null && clockAudio.gameObject.activeInHierarchy) clockAudio.PlayOneShot(clip);
                }
                _tickedSecond = secs;
            }
            _timerPulse = Mathf.MoveTowards(_timerPulse, 0f, Time.deltaTime * 3f);
            float sc = 1f + 0.3f * _timerPulse * _timerPulse;
            timerLabel.transform.localScale = new Vector3(sc, sc, 1f);
        }

        /// <summary>The target glows turn slowly and breathe; all off once you're out of the fight.</summary>
        private void AnimateSeatGlows()
        {
            if (seatGlows == null) return;
            if (!_inBattle && _glowsOn)
            {
                _glowsOn = false;
                for (int t = 0; t < seatGlows.Length; t++)
                {
                    if (seatGlows[t] != null && seatGlows[t].gameObject.activeSelf) seatGlows[t].gameObject.SetActive(false);
                    if (seatPickers != null && t < seatPickers.Length && seatPickers[t] != null && seatPickers[t].activeSelf) seatPickers[t].SetActive(false);
                }
                return;
            }
            if (!_glowsOn) return;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 5f);
            Quaternion spin = Quaternion.Euler(90f, Time.time * 40f, 0f);
            for (int t = 0; t < seatGlows.Length; t++)
            {
                Renderer g = seatGlows[t];
                if (g == null || !g.gameObject.activeSelf) continue;
                Transform gt = g.transform;
                gt.localRotation = spin;
                float s = 2.6f * SeatScale(t) * pulse;
                gt.localScale = new Vector3(s, s, 1f);
            }
        }

        private void Update()
        {
            AnimateSeatGlows();
            if (manager != null && manager.phase >= BattleManager.PHASE_JOINING) { UpdateClock(); SpinFx(); AnimateSummon(); AnimateCircle(); AnimatePips(); AnimateGauges(); }
            else
            {
                if (_clockMat != null && _clockShown != 1f) { _clockMat.SetFloat("_Remaining", 1f); _clockShown = 1f; }   // idle ring: the dial at rest, full
                if (timerLabel != null && timerLabel.text.Length > 0) UpdateTimer(0, false);
                if (_centreHidden) { _centreHidden = false; ShowCentreText(true); }
            }
            if (manager != null && manager.phase >= BattleManager.PHASE_JOINING) FaceViewer();
            AnimateFlash();
            AnimateDefeats();
            AnimatePops();
            AnimateFloaters();
            if (_inBattle && manager != null) { UpdateHudHealth(); AnimateCards(); KeepNearSeat(); UpdateWaitPanel(); }
            if (potionMenuRoot != null && potionMenuRoot.activeSelf && (!_inBattle || manager == null || !CanAct())) potionMenuRoot.SetActive(false);
            if (handRoot != null && (handRoot.activeSelf || (hudGrip != null && hudGrip.gameObject.activeSelf))) PlaceHud();
            if (_inBattle && !_seated && manager != null && manager.phase == BattleManager.PHASE_ROAMING && arenaCenter != null && Time.time >= _nextEngage)
            {
                VRCPlayerApi me = Networking.LocalPlayer;
                Vector3 p = me.GetPosition() - arenaCenter.position;
                bool level = Mathf.Abs(p.y) <= engageHeight;
                p.y = 0f;
                if (level && p.magnitude <= engageRadius)
                {
                    _nextEngage = Time.time + 1f;
                    manager.RequestEngage();
                }
            }
            // Wandering-monster lane: like Wizard101, anyone who walks into the circle joins the fight if a seat is free,
            // during the join window or in any later round.
            if (manager != null && manager.wanderMode && manager.phase >= BattleManager.PHASE_JOINING && manager.phase <= BattleManager.PHASE_PLAYBACK &&
                manager.HasFreeWizardSeat() && CanFight() && arenaCenter != null && Time.time >= _nextEngage && manager.battleId != _fledBattle)
            {
                VRCPlayerApi me = Networking.LocalPlayer;
                Vector3 p = me.GetPosition() - arenaCenter.position;
                bool level = Mathf.Abs(p.y) <= engageHeight;
                p.y = 0f;
                if (level && p.magnitude <= engageRadius)
                {
                    _nextEngage = Time.time + 1f;
                    manager.RequestEngageWander(-1);
                }
            }
            if (manager != null) AnimateCast();
            if (_spellAnim != null) AnimateSpellFx();
            if (manager == null) return;
            if (padPivot != null)
            {
                VRCPlayerApi viewer = Networking.LocalPlayer;
                if (Utilities.IsValid(viewer)) Billboard(padPivot, viewer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position);
            }
            if (padLabel != null)
            {
                int phase = manager.phase;
                if (Pvp())
                {
                    string circle = padTitle.Length > 0 ? padTitle : "Duelling Circle";
                    if (phase == BattleManager.PHASE_IDLE) padLabel.text = circle + "\nStep into the circle to duel (2-8 wizards)";
                    else if (manager.WaitingForOpponent()) padLabel.text = circle + "\nWaiting for an opponent to step in...";
                    else if (phase == BattleManager.PHASE_QUEUE)
                        padLabel.text = "Duel starts in " + manager.SecondsLeft().ToString() + "s\nWizards in the circle: " + manager.QueueCount().ToString() + " / 8";
                    else padLabel.text = "Duel in progress";
                }
                else if (!QueueUnlocked()) padLabel.text = queueLockedText;
                else if (padTitle.Length > 0)
                {
                    if (phase == BattleManager.PHASE_IDLE) padLabel.text = padTitle;
                    else if (phase == BattleManager.PHASE_QUEUE) padLabel.text = "The fight starts in " + manager.SecondsLeft().ToString() + "s\nWizards in the ring: " + manager.QueueCount().ToString() + " / 4";
                    else padLabel.text = "Battle in progress";
                }
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
                overheadLabels[s].text = manager.Occupied(s) && manager.phase >= (manager.IsPlayerSeat(s) ? BattleManager.PHASE_JOINING : BattleManager.PHASE_ROAMING) ? SlotLine(s, false) : "";
                // A wizard's name shows in their signature colour.
                int sig = SigIndex(s);
                Color want = sig > 0 ? LookColor(sig, 0) : Color.white;
                if (overheadLabels[s].color != want) overheadLabels[s].color = want;
            }
            if (partyLabel != null)
            {
                string w = Pvp() ? "BLUE TEAM" : "WIZARDS";
                string m = Pvp() ? "RED TEAM" : "MONSTERS";
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
            if (manager.stunBlock[s] > 0) line += "  Stun Shield";
            if (manager.taunt[s] > 0) line += "  Taunting";
            if (manager.IsPlayerSeat(s) && !manager.IsSummon(s) && manager.phase == BattleManager.PHASE_PLANNING && manager.Alive(s)) line += manager.locked[s] != 0 ? "  (ready)" : "  (choosing)";
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
                bool tc = has && _hand[i] >= TC_FLAG;
                if (cardTreasureTags != null && i < cardTreasureTags.Length && cardTreasureTags[i] != null && cardTreasureTags[i].activeSelf != tc)
                    cardTreasureTags[i].SetActive(tc);
                if (!has) continue;
                int card = HandCard(_hand[i]);
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
                    if (art != null) cardArts[i].uvRect = art.width == art.height ? new Rect(-0.25f, 0f, 1.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
                    cardArts[i].color = usable || _discardMode ? Color.white : new Color(0.5f, 0.5f, 0.52f);
                    if (cardArts[i].gameObject.activeSelf != (art != null)) cardArts[i].gameObject.SetActive(art != null);
                }
                if (cardFaces != null && i < cardFaces.Length && cardFaces[i] != null)
                    cardFaces[i].color = usable || _discardMode ? _faceOn : _faceOff;
                cardNames[i].text = cardDatabase.GetName(card);
                cardInfos[i].text = cardDatabase.GetCardText(card);
                if (cardPips != null && i < cardPips.Length && cardPips[i] != null) cardPips[i].text = cardDatabase.GetPipCost(card).ToString();
                if (cardAccuracies != null && i < cardAccuracies.Length && cardAccuracies[i] != null) cardAccuracies[i].text = cardDatabase.GetAccuracy(card).ToString() + "%";
                if (cardSchools != null && i < cardSchools.Length && cardSchools[i] != null) cardSchools[i].text = cardDatabase.SchoolName(cardDatabase.GetSchool(card));
            }

            bool picking = CanAct() && _selHand >= 0 && _selHand < _handCount;
            bool allies = picking && cardDatabase.TargetsAllies(HandCard(_hand[_selHand]));
            _glowsOn = false;
            for (int t = 0; t < BattleManager.SLOTS; t++)
            {
                bool show = picking && manager.Alive(t) && (manager.SideOf(t) == MySide()) == allies;
                // The seat itself glows and can be clicked (your own seat glows too; pick yourself from the hand).
                if (seatGlows != null && t < seatGlows.Length && seatGlows[t] != null)
                {
                    if (show) seatGlows[t].sharedMaterial = allies ? seatGlowAllyMat : seatGlowEnemyMat;
                    if (seatGlows[t].gameObject.activeSelf != show) seatGlows[t].gameObject.SetActive(show);
                }
                if (seatPickers != null && t < seatPickers.Length && seatPickers[t] != null && seatPickers[t].activeSelf != show) seatPickers[t].SetActive(show);
                if (show) _glowsOn = true;
                if (targetButtons == null || t >= targetButtons.Length || targetButtons[t] == null) continue;
                targetButtons[t].SetActive(show);
                if (show) targetLabels[t].text = manager.SlotName(t) + "\n" + _shownHp[t].ToString() + " / " + manager.maxHp[t].ToString();
            }
            if (potionLabel != null) potionLabel.text = "\uE014 Potion (" + CountPotions().ToString() + ")";
            if (treasureLabel != null) treasureLabel.text = "Treasure (" + _tcPoolCount.ToString() + ")";
            if (treasureButton != null)
            {
                Image tb = treasureButton.GetComponent<Image>();
                if (tb != null) tb.color = CanDrawTreasure() ? new Color(0.62f, 0.45f, 0.12f) : new Color(0.32f, 0.28f, 0.22f);
            }
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
            int sc = cardDatabase.GetSchool(card);
            if (e == CardDatabase.EFFECT_WARD) AddShown(_fxSh, t, manager.PackFx(card, cardDatabase.ShieldSchool(card), v));
            else if (e == CardDatabase.EFFECT_TAUNT) AddShown(_fxSh, t, manager.PackFx(card, CardDatabase.ANY_SCHOOL, v));
            else if (e == CardDatabase.EFFECT_MULTIWARD)
            {
                for (int j = 0; j < Mathf.Clamp(cardDatabase.GetAux(card), 1, 3); j++) AddShown(_fxSh, t, manager.PackFx(card, cardDatabase.ShieldSchool(card), v));
            }
            else if (e == CardDatabase.EFFECT_TRISHIELD)
            {
                int first = cardDatabase.TriBladeFirstSchool(card);
                for (int j = 0; j < 3; j++) AddShown(_fxSh, t, manager.PackFx(card, first + j, v));
            }
            else if (e == CardDatabase.EFFECT_EMPOWER) AddShown(_fxBl, t, manager.PackFx(card, cardDatabase.BladeSchool(card), v));
            else if (e == CardDatabase.EFFECT_HEALBLADE) AddShown(_fxBl, t, manager.PackFx(card, CardDatabase.HEAL_SCHOOL, v));
            else if (e == CardDatabase.EFFECT_TRAP) AddShown(_fxTr, t, manager.PackFx(card, cardDatabase.TrapSchool(card), v));
            else if (e == CardDatabase.EFFECT_CURSE) AddShown(_fxTr, t, manager.PackFx(card, CardDatabase.ANY_SCHOOL, v));
            else if (e == CardDatabase.EFFECT_TRITRAP)
            {
                int first = cardDatabase.TriBladeFirstSchool(card);
                for (int j = 0; j < 3; j++) AddShown(_fxTr, t, manager.PackFx(card, first + j, v));
            }
            else if (e == CardDatabase.EFFECT_STRIKEFX)
            {
                int fk = cardDatabase.GetAux(card) / 1000;
                if (fk == 0) AddShown(_fxSh, t, manager.PackFx(card, CardDatabase.ANY_SCHOOL, v));
                else if (fk == 1) AddShown(_fxBl, t, manager.PackFx(card, sc, v));
                else AddShown(_fxTr, t, manager.PackFx(card, sc, v));
            }
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
            if (school == CardDatabase.HEAL_SCHOOL) school = 3;
            return fxMats[school >= 0 && school < fxMats.Length - 1 ? school : fxMats.Length - 1];
        }

        // ------------------------------------------------------------------ pips at the seats

        /// <summary>
        /// Each wizard in this duel powers their Surge aura with the pips shown at their seat (everyone watching sees the
        /// same); when they leave the duel, or it ends, theirs goes quiet again.
        /// </summary>
        private void SyncAuras()
        {
            if (cosmetics == null || manager == null) return;
            bool fight = manager.phase >= BattleManager.PHASE_JOINING;
            int seats = manager.PlayerSeats();
            for (int s = 0; s < BattleManager.SLOTS; s++)
            {
                int id = fight && s < seats && manager.Occupied(s) && _shownHp[s] > 0 ? manager.slotIds[s] : 0;
                if (id < 0) id = 0;
                if (id != _auraIds[s])
                {
                    if (_auraIds[s] > 0)
                    {
                        PlayerCosmetics gone = cosmetics.CosmeticsOf(_auraIds[s]);
                        if (gone != null) gone.SetPower(-1, BattleManager.MAX_PIPS);
                    }
                    _auraIds[s] = id;
                    _auraPips[s] = -99;
                }
                if (id <= 0) continue;
                int held = _shownPips[s] + _shownPower[s];
                if (held == _auraPips[s]) continue;
                _auraPips[s] = held;
                PlayerCosmetics pc = cosmetics.CosmeticsOf(id);
                if (pc != null) pc.SetPower(held, BattleManager.MAX_PIPS);
            }
        }

        private void UpdatePips()
        {
            SyncAuras();
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
                // Holding a lot of pips: an aura rises round you in your pip aura colour.
                int held = _shownPips[s] + _shownPower[s];
                if (auraSwirls != null && s < auraSwirls.Length && auraSwirls[s] != null && held >= auraPipThreshold)
                {
                    TintAura(s);
                    auraSwirls[s].Emit(3 + (held - auraPipThreshold) * 2);
                }
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
            float fps = Mathf.Max(1f, circleFps);
            float since = Mathf.Floor((Time.time - _flashStart) * fps) / fps;
            if (circleSigil != null) circleSigil.localRotation = Quaternion.Euler(0f, since * sigilDegreesPerSecond, 0f);
            if (circleRunes != null) circleRunes.localRotation = Quaternion.Euler(0f, since * runeDegreesPerSecond, 0f);
            if (circleSigilMat != null)
            {
                float tq = Mathf.Floor(Time.time * fps) / fps;
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
                // Holds and blinks, then swells and shrinks away.
                float sc = t < 0.3f ? 1f : (t < 0.55f ? Mathf.Lerp(1f, 1.7f, (t - 0.3f) / 0.25f) : Mathf.Lerp(1.7f, 0f, (t - 0.55f) / 0.45f));
                p.localScale = new Vector3(0.5f * sc, 0.5f * sc, 1f);
                VRCPlayerApi me = Networking.LocalPlayer;
                if (Utilities.IsValid(me))
                {
                    p.rotation = Upright(p.position - me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, p.rotation);
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
                if ((f & BattleManager.FLAG_STUNNED) != 0) Float(t, "Stunned!", new Color(1f, 0.95f, 0.4f), -2);
                if ((f & BattleManager.FLAG_STUNBLOCK) != 0) Float(t, "Stun blocked!", new Color(0.75f, 0.85f, 1f), -2);
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
                if (e == CardDatabase.EFFECT_WARD || e == CardDatabase.EFFECT_MULTIWARD) fx = FxTexture(BattleManager.FX_SHIELD, cardDatabase.ShieldSchool(card));
                else if (e == CardDatabase.EFFECT_TAUNT || e == CardDatabase.EFFECT_TRISHIELD) fx = FxTexture(BattleManager.FX_SHIELD, CardDatabase.ANY_SCHOOL);
                else if (e == CardDatabase.EFFECT_EMPOWER) fx = FxTexture(BattleManager.FX_BLADE, cardDatabase.BladeSchool(card));
                else if (e == CardDatabase.EFFECT_HEALBLADE) fx = FxTexture(BattleManager.FX_BLADE, CardDatabase.HEAL_SCHOOL);
                else if (e == CardDatabase.EFFECT_TRIBLADE) fx = FxTexture(BattleManager.FX_BLADE, CardDatabase.ANY_SCHOOL);
                else if (e == CardDatabase.EFFECT_TRAP) fx = FxTexture(BattleManager.FX_TRAP, cardDatabase.TrapSchool(card));
                else if (e == CardDatabase.EFFECT_TRITRAP || e == CardDatabase.EFFECT_CURSE) fx = FxTexture(BattleManager.FX_TRAP, CardDatabase.ANY_SCHOOL);
                else if (e == CardDatabase.EFFECT_STRIKEFX)
                {
                    int fk = cardDatabase.GetAux(card) / 1000;
                    fx = FxTexture(fk == 0 ? BattleManager.FX_SHIELD : fk == 1 ? BattleManager.FX_BLADE : BattleManager.FX_TRAP, fk == 0 ? CardDatabase.ANY_SCHOOL : sc);
                }
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
            if (e == CardDatabase.EFFECT_HACK) return "Hacked! +" + v.ToString();
            if (e == CardDatabase.EFFECT_FIELD) return cardDatabase.FieldWord(card) + " +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_TAUNT) return "Taunt!";
            if (e == CardDatabase.EFFECT_FOCUS) return "Focus +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_MULTIWARD) return Mathf.Clamp(cardDatabase.GetAux(card), 1, 3).ToString() + " Shields " + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_TRISHIELD) return "3 Shields " + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_TRITRAP) return "3 Traps +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_CURSE) return "Trap +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_HEALBLADE) return "Healing +" + v.ToString() + "%";
            if (e == CardDatabase.EFFECT_STRIKEFX)
            {
                int fk = cardDatabase.GetAux(card) / 1000;
                return fk == 0 ? "Shield " + v.ToString() + "%" : fk == 1 ? "Blade +" + v.ToString() + "%" : "Trap +" + v.ToString() + "%";
            }
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

        /// <summary>
        /// Billboards the circle's words (the name / health line over every seat, the round banner and the clock's label)
        /// so they always face this player, staying upright. Floating numbers turn themselves (AnimateFloaters).
        /// </summary>
        private void FaceViewer()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            if (overheadLabels != null)
                for (int i = 0; i < overheadLabels.Length; i++)
                    if (overheadLabels[i] != null && overheadLabels[i].text.Length > 0) Billboard(overheadLabels[i].transform.parent, head);
            if (bannerLabel != null && bannerLabel.text.Length > 0) Billboard(bannerLabel.transform.parent, head);
            if (clockLabel != null && clockLabel.text.Length > 0) Billboard(clockLabel.transform.parent, head);
            if (timerLabel != null && timerLabel.text.Length > 0) Billboard(timerLabel.transform.parent, head);
            // The clock's words, the banner and the timer stand over the middle of the ring: hidden for you while you're
            // there yourself (casting a spell's cinematic), so they're never inside your head.
            bool near = false;
            if (arenaCenter != null)
            {
                Vector3 d = head - arenaCenter.position;
                d.y = 0f;
                near = d.sqrMagnitude < 2.6f;
            }
            if (near != _centreHidden) { _centreHidden = near; ShowCentreText(!near); }
        }

        private void ShowCentreText(bool on)
        {
            if (bannerLabel != null) bannerLabel.transform.parent.gameObject.SetActive(on);
            if (clockLabel != null) clockLabel.transform.parent.gameObject.SetActive(on);
            if (timerLabel != null) timerLabel.transform.parent.gameObject.SetActive(on);
        }

        private void Billboard(Transform t, Vector3 head)
        {
            if (t != null) t.rotation = Upright(t.position - head, t.rotation);
        }

        /// <summary>
        /// The facing for every word and icon that turns toward the viewer: turned only about the vertical axis, so it
        /// stays standing straight up and never tilts left or right (or leans back), however the viewer stands or tilts
        /// their head. Keeps the old facing when the viewer is directly above or below.
        /// </summary>
        private Quaternion Upright(Vector3 look, Quaternion keep)
        {
            look.y = 0f;
            return look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look, Vector3.up) : keep;
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
                if (haveHead) f.rotation = Upright(f.position - head, f.rotation);
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
            // The monster that's hit flinches, or falls if this hit beats it.
            int hk = manager.actKind[i];
            int ht = manager.actTarget[i];
            if ((hk == BattleManager.KIND_DAMAGE || hk == BattleManager.KIND_DOT) && manager.actValue[i] > 0)
            {
                MonsterRig hurt = RigOf(ht);
                if (hurt != null)
                {
                    if (hpBefore > 0 && manager.actHpAfter[i] <= 0) hurt.Die();
                    else hurt.Hit();
                }
            }
            if (hitAudio == null || !hitAudio.gameObject.activeInHierarchy || seats == null) return;
            int k = manager.actKind[i];
            int t = manager.actTarget[i];
            int v = manager.actValue[i];
            if (t < 0 || t >= BattleManager.SLOTS) return;
            AudioClip clip = null;
            bool wizard = manager.IsPlayerSeat(t);
            if ((k == BattleManager.KIND_DAMAGE || k == BattleManager.KIND_DOT) && v > 0 && wizard && wizardHitSounds != null && wizardHitSounds.Length > 0)
                clip = wizardHitSounds[Random.Range(0, wizardHitSounds.Length)];
            if ((k == BattleManager.KIND_HEAL || k == BattleManager.KIND_POTION) && v > 0) clip = healSound;
            if (wizard && hpBefore > 0 && manager.actHpAfter[i] <= 0 && defeatSound != null) clip = defeatSound;
            if (clip == null) return;
            hitAudio.transform.position = seats[t].position + Vector3.up * 1.2f;
            hitAudio.PlayOneShot(clip);
        }

        /// <summary>Card art: shields, blades and traps show their icon (they just flash and appear); other spells have none yet.</summary>
        private Texture CardArt(int card)
        {
            // The card's own art (rendered from its spell animation), when it has some.
            Texture own = cardDatabase.ArtFor(card);
            if (own != null) return own;
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
            _bannerClearAt = -1f;
            if (bannerLabel != null) bannerLabel.text = text;
        }

        /// <summary>The Victory / Defeat text: shown over the ring, then cleared after resultSeconds.</summary>
        private void ShowResult(string text)
        {
            SetBanner(text);
            _bannerClearAt = Time.time + resultSeconds;
            SendCustomEventDelayedSeconds(nameof(_ClearResult), resultSeconds + 0.05f);
        }

        public void _ClearResult()
        {
            // Skipped if something else has been written there since (a new fight's round banner).
            if (_bannerClearAt < 0f || Time.time < _bannerClearAt) return;
            SetBanner("");
        }
    }
}
