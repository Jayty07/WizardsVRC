using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace WizardsVRC
{
    /// <summary>
    /// Networked referee for the boss arena: queue pad countdown, battle setup and turn resolution.
    /// The object owner (instance master by default) is authoritative: clients send requests with
    /// [NetworkCallable] events, the owner updates the synced state, and every client's <see cref="BattleClient"/>
    /// renders it. Slots 0-3 are wizards, 4-7 monsters (4 = boss). In PvP mode both halves hold wizards: everyone in the
    /// duel zone when the countdown ends is split into two teams (slots 0-3 and 4-7) and nobody gains or loses anything.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BattleManager : UdonSharpBehaviour
    {
        public const int PHASE_IDLE = 0;
        public const int PHASE_QUEUE = 1;
        /// <summary>Party is at the arena edge; the fight starts when a wizard walks into the monsters' detection range.</summary>
        public const int PHASE_ROAMING = 2;
        public const int PHASE_JOINING = 3;
        public const int PHASE_PLANNING = 4;
        public const int PHASE_PLAYBACK = 5;
        public const int PHASE_VICTORY = 6;
        public const int PHASE_DEFEAT = 7;

        public const int SIDE = 4;
        public const int SLOTS = 8;
        public const int BOSS_SLOT = 4;
        public const int MAX_ACTIONS = 40;
        /// <summary>Shields / blades / traps used up in one round (see fxLogAct).</summary>
        public const int MAX_FX_LOG = 48;
        public const int FX_SHIELD = 0;
        public const int FX_BLADE = 1;
        public const int FX_TRAP = 2;
        public const int MAX_PIPS = 7;
        public const int FX_MAX = 6;

        public const int KIND_PASS = 0;
        public const int KIND_FIZZLE = 1;
        public const int KIND_DAMAGE = 2;
        public const int KIND_HEAL = 3;
        public const int KIND_BUFF = 4;
        /// <summary>Burn damage ticking at the start of a round (actor = the burning slot).</summary>
        public const int KIND_DOT = 5;
        /// <summary>The actor was stunned and loses this turn.</summary>
        public const int KIND_STUNNED = 6;
        /// <summary>A minion appears in seat target (-1 = no free seat).</summary>
        public const int KIND_SUMMON = 7;
        /// <summary>The actor drank a potion (uses their turn) and healed value.</summary>
        public const int KIND_POTION = 8;

        /// <summary>Pick sent instead of a card id when a wizard drinks a potion; the pick's target is the heal amount.</summary>
        public const int PICK_POTION = -1;

        /// <summary><see cref="actFlags"/> bits.</summary>
        public const int FLAG_CRIT = 1;
        public const int FLAG_WEAK = 2;
        public const int FLAG_RESIST = 4;
        public const int FLAG_BLOCKED = 8;
        /// <summary>This hit stunned the target (it loses its next turn, and gains a stun shield).</summary>
        public const int FLAG_STUNNED = 16;
        /// <summary>This hit's stun was stopped by the target's stun shield.</summary>
        public const int FLAG_STUNBLOCK = 32;
        /// <summary>
        /// On the first entry of an attack spell that went off (a wizard's or a monster's): the cast plays as a cinematic
        /// (the caster is taken to the middle of the ring and casts from there, everyone in the fight watches it on the
        /// film screen, the caster goes back to their seat).
        /// </summary>
        public const int FLAG_CINE = 64;

        public CardDatabase cardDatabase;
        public BattleClient client;

        [Header("Timing (seconds)")]
        public int queueSeconds = 15;
        public int joinSeconds = 8;
        [Tooltip("The first round never starts sooner than this after the ring forms, so everyone pulled in arrives together.")]
        public float startGraceSeconds = 3f;
        public int roamSeconds = 300;
        public int planningSeconds = 30;
        public int endSeconds = 8;
        public float actionSeconds = 1.6f;
        [Tooltip("Cast wind-up per School (the cast sound's length): the spell's effect lands after it.")]
        public float[] castSeconds = { 2.9f, 3.77f, 3.46f, 3.17f, 3.0f, 3.67f, 3.06f };
        [Tooltip("When the school-coloured burst explodes off the caster, in seconds into the cast sound (indexed by School).")]
        public float[] castPeakSeconds = { 2f, 2f, 2f, 2f, 2f, 2f, 2f };
        [Tooltip("Wind-up for General spells (no cast sound).")]
        public float generalCastSeconds = 1.2f;
        [Tooltip("When General spells' cast sound peaks (the particle burst), seconds.")]
        public float generalCastPeakSeconds = 0.72f;
        [Tooltip("Orb flight + impact after the wind-up, before the next caster.")]
        public float impactSeconds = 1.0f;
        [Tooltip("A fizzle plays the cast's wind-up this long, then cuts off (the fizzle sound and puff of smoke).")]
        public float fizzleCutSeconds = 2.0f;
        [Tooltip("After a fizzle cuts off, this long before the next caster.")]
        public float fizzleAfterSeconds = 1.2f;

        [Header("Spell cinematics (every attack spell: see FLAG_CINE and SpellCinematic)")]
        [Tooltip("Fading to black, being taken to the middle of the ring and fading back in, before the wind-up.")]
        public float cineInSeconds = 0.9f;
        [Tooltip("Fading out, back to the seat and fading in again, after the spell has landed.")]
        public float cineOutSeconds = 0.8f;
        [Tooltip("How long the shots of the spell landing hold after its effect, plus this much more per pip.")]
        public float cineAfterSeconds = 1.0f;
        public float cineAfterPerPip = 0.25f;
        [Tooltip("A monster's attack spell (no cinematic): how long it settles after its release, before the next caster.")]
        public float plainAfterSeconds = 0.4f;

        [Header("Cast patterns (CastPattern): a spell under 100% accuracy is cast by drawing its rune")]
        [Tooltip("A perfect pattern takes this % off the spell's chance to fizzle (a part-done one takes its share of it).")]
        [Range(0, 100)] public int patternFizzleCut = 90;

        [Header("Encounter")]
        [Tooltip("Monster ids (CardDatabase monster table) in this fight; roster[0] takes the boss seat.")]
        public int[] roster = { 1, 2, 2, 2 };
        [Tooltip("How many monsters the encounter has for a solo wizard (the roster may be padded out to four seats with more " +
                 "of its followers). The fight's limit is this plus one per extra wizard, never more than four. 0 = the roster's length.")]
        public int rosterSize;
        [Tooltip("Spawn roster[0] plus one more monster per wizard (the Training Yard boss fight) instead of the whole roster.")]
        public bool scaleRosterWithParty = true;
        [Tooltip("Wandering-monster lane: fights start when a wizard touches a monster and the party sits down at once.")]
        public bool wanderMode;
        [Tooltip("Seconds after a fight before the lane's wandering monsters come back.")]
        public int respawnSeconds = 20;
        [Tooltip("Even fight (for new players): the fight starts with only the monster that was touched. Each extra wizard " +
                 "lets one more patrol monster join when it reaches the ring; other patrol monsters that reach the ring fade " +
                 "away. With no patrol monster left to come, the next roster monster joins at once. In every fight, once one " +
                 "of its monsters is defeated no other monster joins (no replacements).")]
        public bool evenFight;
        [Tooltip("Monster id of each patrol monster (index = Wanderer.wandererIndex).")]
        public int[] wandererMonsters = new int[0];

        [Header("PvP")]
        [Tooltip("Wizard against wizard: the queue countdown starts when the first wizard enters the zone, everyone in it when " +
                 "it ends fights (at least 2, split into two teams), there are no monsters, rewards or defeat penalties.")]
        public bool pvpMode;
        /// <summary>PvP: the side that won the last duel (0 or 1, -1 = nobody, e.g. everyone left).</summary>
        [UdonSynced] public int winnerSide = -1;

        /// <summary>Even fights: bit w set = patrol monster w is in this fight or faded away (hidden until it respawns).</summary>
        [UdonSynced] public int wandererGone;

        [Header("Wizard101-style rules")]
        [Tooltip("Level from which every combatant gets the base power pip chance (gear adds to it at any level).")]
        public int powerPipLevel = 10;
        [Tooltip("Base % chance that a round's pip is a power pip (worth 2 pips for spells of the caster's own school).")]
        public int basePowerPipChance = 40;
        [Tooltip("% less damage a creature takes from its own school.")]
        public int creatureSchoolResist = 30;
        [Tooltip("% more damage a creature takes from the school opposite its own (Fire/Ice, Storm/Myth, Life/Death).")]
        public int creatureSchoolWeakness = 30;
        [Tooltip("Critical hits deal (and critical heals restore) this % of the normal amount.")]
        public int critMultiplier = 200;
        [Tooltip("Critical chance % of boss monsters.")]
        public int bossCritPct = 5;
        [Header("Boss healing (a boss is never sure to heal)")]
        [Tooltip("A boss may heal itself or an ally once one is under this % of its health.")]
        [Range(1, 100)] public int bossHealBelowPct = 60;
        [Tooltip("Chance % a boss heals in a round when the hurt one is just under that, rising to bossHealChanceLow as it nears 0.")]
        [Range(0, 100)] public int bossHealChance = 25;
        [Range(0, 100)] public int bossHealChanceLow = 55;
        [Tooltip("Rounds a boss waits after any heal before it can heal again (other monsters wait 2 after healing themselves).")]
        public int bossHealGraceRounds = 4;
        [Tooltip("Zone encounters: places the duel ring may appear (shared by every monster group in the street). A fight " +
                 "forms at the free spot nearest the wizard who touched the monster. Empty = the ring stays where it was built.")]
        public Transform[] ringSpots;
        [Tooltip("Every fight lane in the street (all monster groups): a spot one of them is fighting on isn't free.")]
        public BattleManager[] peers;
        [Tooltip("This monster group's fight lanes (this one included): a patrol monster in one lane's fight can't start another.")]
        public BattleManager[] groupLanes;

        /// <summary>Patrol monster w is in (or waiting to respawn from) a fight on any of the group's lanes.</summary>
        public bool WandererHiddenAnyLane(int w)
        {
            if (WandererHidden(w)) return true;
            if (groupLanes == null) return false;
            for (int k = 0; k < groupLanes.Length; k++)
                if (groupLanes[k] != null && groupLanes[k] != this && groupLanes[k].WandererHidden(w)) return true;
            return false;
        }
        /// <summary>Index into ringSpots of the current fight's ring.</summary>
        [UdonSynced] public int ringSpot;

        /// <summary>Server time when the last fight ended (wanderers respawn respawnSeconds later).</summary>
        [UdonSynced] public int lastEndMs;

        [UdonSynced] public int phase;
        /// <summary>Server time the current fight's ring formed (the start grace counts from it).</summary>
        [UdonSynced] public int formedMs;
        [UdonSynced] public int phaseEndMs;
        [UdonSynced] public int battleId;
        [UdonSynced] public int round;
        [UdonSynced] public int firstSide;
        [UdonSynced] public int[] queueIds = new int[SLOTS];

        /// <summary>Wizard slots: player id. Monster slots: monster id (CardDatabase monster table). Summoned minions (either side): -cardId. 0 = empty.</summary>
        [UdonSynced] public int[] slotIds = new int[SLOTS];
        [UdonSynced] public int[] hp = new int[SLOTS];
        [UdonSynced] public int[] maxHp = new int[SLOTS];
        /// <summary>Normal (white) pips. Together with <see cref="powerPips"/> at most MAX_PIPS.</summary>
        [UdonSynced] public int[] pips = new int[SLOTS];
        /// <summary>Power pips: worth 2 pips for spells of the slot's own school, 1 otherwise.</summary>
        [UdonSynced] public int[] powerPips = new int[SLOTS];
        [UdonSynced] public int[] powerPipPct = new int[SLOTS];
        [UdonSynced] public int[] critPct = new int[SLOTS];
        [UdonSynced] public int[] blockPct = new int[SLOTS];
        [UdonSynced] public int[] piercePct = new int[SLOTS];
        [UdonSynced] public int[] school = new int[SLOTS];
        [UdonSynced] public int[] level = new int[SLOTS];
        /// <summary>A wizard's creator colours: signature colour index | pip aura index &lt;&lt; 8 (0 = their school's colour).</summary>
        [UdonSynced] public int[] look = new int[SLOTS];
        [UdonSynced] public int[] dmgPct = new int[SLOTS];
        [UdonSynced] public int[] resistPct = new int[SLOTS];
        [UdonSynced] public int[] accPct = new int[SLOTS];
        /// <summary>
        /// Per-slot shields, blades and traps: FX_MAX entries per slot (slot * FX_MAX + k), packed by <see cref="PackFx"/>,
        /// kept compacted (empties at the end, oldest first). 0 = empty.
        /// </summary>
        [UdonSynced] public int[] shields = new int[SLOTS * FX_MAX];
        [UdonSynced] public int[] blades = new int[SLOTS * FX_MAX];
        [UdonSynced] public int[] traps = new int[SLOTS * FX_MAX];
        [UdonSynced] public int[] focus = new int[SLOTS];
        [UdonSynced] public int[] locked = new int[SLOTS];
        /// <summary>Burn damage still to come and the rounds it is spread over.</summary>
        [UdonSynced] public int[] dotLeft = new int[SLOTS];
        [UdonSynced] public int[] dotTurns = new int[SLOTS];
        /// <summary>1 = loses its next cast.</summary>
        [UdonSynced] public int[] stunned = new int[SLOTS];
        /// <summary>The duel's school bubble or grid (EFFECT_FIELD: each school's L12 bubble, Tech's Neon Grid): its school, +damage/heal % and rounds left (0 = none).</summary>
        [UdonSynced] public int bubbleSchool = -1;
        [UdonSynced] public int bubblePct;
        [UdonSynced] public int bubbleRounds;
        /// <summary>Rounds left in which monsters must attack this slot.</summary>
        [UdonSynced] public int[] taunt = new int[SLOTS];
        /// <summary>Stun shields: each blocks one stun. A slot gains one whenever it's stunned, so stuns can't chain.</summary>
        [UdonSynced] public int[] stunBlock = new int[SLOTS];

        /// <summary>Last round's results, played back by every client. Entries sharing a group are one cast.</summary>
        [UdonSynced] public int actCount;
        [UdonSynced] public int[] actGroup = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actActor = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actTarget = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actCard = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actKind = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actValue = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actHpAfter = new int[MAX_ACTIONS];
        [UdonSynced] public int[] actFlags = new int[MAX_ACTIONS];
        /// <summary>
        /// Shields, blades and traps used up (or broken) this round, so clients can break them as the hit lands: the act
        /// entry they belong to, whose they were, which kind (FX_SHIELD / FX_BLADE / FX_TRAP) and the packed value.
        /// </summary>
        [UdonSynced] public int fxLogCount;
        [UdonSynced] public int[] fxLogAct = new int[MAX_FX_LOG];
        [UdonSynced] public int[] fxLogSlot = new int[MAX_FX_LOG];
        [UdonSynced] public int[] fxLogKind = new int[MAX_FX_LOG];
        [UdonSynced] public int[] fxLogValue = new int[MAX_FX_LOG];

        // owner-only scratch
        private int _nextFlags;
        private bool[] _joined = new bool[SLOTS];
        private int[] _pickCard = new int[SLOTS];
        private int[] _pickTarget = new int[SLOTS];
        /// <summary>How well each wizard drew this round's cast pattern (0-100; 0 = none).</summary>
        private int[] _pickQuality = new int[SLOTS];
        private int[] _targets = new int[SIDE];
        private int[] _usedSrc = new int[FX_MAX];
        private int _startWanderer = -1;
        /// <summary>Round in which a monster joined mid-fight (it sits that round out); -1 = from the start.</summary>
        private int[] _joinRound = new int[SLOTS];
        /// <summary>Monsters: the first round they may heal again (a monster that heals itself waits 2 rounds).</summary>
        private int[] _healReady = new int[SLOTS];
        // Effects used since the last act entry; attached to the next one (the hit they belong to).
        private int _pendFx;
        private int[] _pendSlot = new int[MAX_FX_LOG];
        private int[] _pendKind = new int[MAX_FX_LOG];
        private int[] _pendValue = new int[MAX_FX_LOG];

        // ------------------------------------------------------------------ helpers (all clients)

        public int Now() { return Networking.GetServerTimeInMilliseconds(); }

        /// <summary>True when the action at act index i is a real cast (damage, heal or buff) with a wind-up.</summary>
        public bool IsCastAct(int i)
        {
            int k = actKind[i];
            return k == KIND_DAMAGE || k == KIND_HEAL || k == KIND_BUFF;
        }

        public float CastSeconds(int school)
        {
            return school >= 0 && castSeconds != null && school < castSeconds.Length ? castSeconds[school] : generalCastSeconds;
        }

        public float CastPeakSeconds(int school)
        {
            float len = CastSeconds(school);
            float p = school < 0 ? generalCastPeakSeconds : castPeakSeconds != null && school < castPeakSeconds.Length ? castPeakSeconds[school] : len * 0.6f;
            return Mathf.Clamp(p, 0f, len);
        }

        /// <summary>Playback time for the cast starting at act index i (same on every client).</summary>
        public float StepSeconds(int i)
        {
            if (actKind[i] == KIND_FIZZLE && actCard[i] > 0) return FizzleCut(cardDatabase.GetSchool(actCard[i])) + fizzleAfterSeconds;
            if (!IsCastAct(i)) return actionSeconds;
            // A spell with an animation plays it out instead of the plain orb (CardDatabase.AnimSeconds).
            int card = actCard[i];
            float wind = CastSeconds(cardDatabase.GetSchool(card));
            // An attack spell plays its own choreography (SpellCinematic): a wizard's as a cinematic, a monster's from its seat.
            if (cardDatabase.CineStyle(card) != 0)
            {
                float release = cardDatabase.CineRelease(card);
                if (IsCineAct(i)) return cineInSeconds + wind + release + CineAfter(card) + cineOutSeconds;
                return wind + release + plainAfterSeconds;
            }
            float anim = cardDatabase.AnimSeconds(card);
            return wind + (anim > 0f ? anim : impactSeconds);
        }

        /// <summary>An attack spell that plays as a cinematic (see FLAG_CINE).</summary>
        public bool IsCineAct(int i) { return i >= 0 && i < actCount && (actFlags[i] & FLAG_CINE) != 0 && IsCastAct(i); }

        /// <summary>How long the shots of a cinematic spell hold after it lands (longer for bigger spells).</summary>
        public float CineAfter(int cardId) { return cineAfterSeconds + cineAfterPerPip * Mathf.Clamp(cardDatabase.GetPipCost(cardId), 0, 14); }

        /// <summary>
        /// Chance (0-100) a spell fizzles for this caster: the card's accuracy with their bonuses and Focus, and a cast
        /// pattern 'quality' % complete takes that share of patternFizzleCut off it.
        /// </summary>
        public int FizzleChance(int s, int cardId, int quality)
        {
            int accuracy = cardDatabase.GetAccuracy(cardId) + (s >= 0 && s < SLOTS ? accPct[s] + focus[s] : 0);
            int miss = Mathf.Clamp(100 - accuracy, 0, 100);
            if (miss > 0 && quality > 0) miss = Mathf.RoundToInt(miss * (1f - patternFizzleCut * Mathf.Clamp(quality, 0, 100) / 10000f));
            return miss;
        }

        /// <summary>How far into the wind-up a fizzle cuts off (never past the cast's own length).</summary>
        public float FizzleCut(int school)
        {
            return Mathf.Min(fizzleCutSeconds, CastSeconds(school) * 0.9f);
        }

        public int SecondsLeft()
        {
            int ms = phaseEndMs - Now();
            return ms <= 0 ? 0 : (ms + 999) / 1000;
        }

        public bool Occupied(int s) { return s >= 0 && s < SLOTS && slotIds[s] != 0; }
        public bool Alive(int s) { return Occupied(s) && hp[s] > 0; }
        public bool IsSummon(int s) { return Occupied(s) && slotIds[s] < 0; }
        public int SummonCard(int s) { return IsSummon(s) ? -slotIds[s] : 0; }
        public int SideOf(int s) { return s < SIDE ? 0 : 1; }

        /// <summary>Seats players can sit in: the wizards' half, or every seat in PvP.</summary>
        public int PlayerSeats() { return pvpMode ? SLOTS : SIDE; }

        /// <summary>A seat that holds (or could hold) a player rather than a monster.</summary>
        public bool IsPlayerSeat(int s) { return s >= 0 && s < PlayerSeats(); }

        /// <summary>Most wizards the queue takes: a party of 4, or two teams of 4 in PvP.</summary>
        public int QueueCapacity() { return pvpMode ? SLOTS : SIDE; }

        public int FindWizardSlot(int playerId)
        {
            if (playerId <= 0) return -1;
            int n = PlayerSeats();
            for (int s = 0; s < n; s++) if (slotIds[s] == playerId) return s;
            return -1;
        }

        public bool IsQueued(int playerId)
        {
            for (int i = 0; i < queueIds.Length; i++) if (queueIds[i] == playerId && playerId > 0) return true;
            return false;
        }

        public int QueueCount()
        {
            int n = 0;
            for (int i = 0; i < queueIds.Length; i++) if (queueIds[i] != 0) n++;
            return n;
        }

        /// <summary>PvP: in the zone but the countdown is on hold until a second wizard steps in.</summary>
        public bool WaitingForOpponent() { return pvpMode && phase == PHASE_QUEUE && phaseEndMs == 0; }

        /// <summary>Monster id in a monster seat (0 for empty seats and summons; always 0 in PvP).</summary>
        public int MonsterAt(int s) { return !pvpMode && s >= SIDE && s < SLOTS && slotIds[s] > 0 ? slotIds[s] : 0; }

        /// <summary>True while the lane's wandering monsters should be hidden: a fight is on, or one just ended.</summary>
        public bool WanderersHidden()
        {
            return phase != PHASE_IDLE || (lastEndMs != 0 && Now() - lastEndMs < respawnSeconds * 1000);
        }

        /// <summary>
        /// Whether patrol monster w is hidden: only the one that was touched (and, in even fights, the ones that joined or
        /// faded away) leaves its patrol; the rest keep walking. They come back respawnSeconds after the fight.
        /// </summary>
        public bool WandererHidden(int w)
        {
            bool gone = w >= 0 && w < 31 && (wandererGone & (1 << w)) != 0;
            if (phase != PHASE_IDLE) return gone;
            return gone && lastEndMs != 0 && Now() - lastEndMs < respawnSeconds * 1000;
        }

        public int MonsterCount()
        {
            int n = 0;
            for (int s = SIDE; s < SLOTS; s++) if (Occupied(s)) n++;
            return n;
        }

        // ------------------------------------------------------------------ shields / blades / traps

        /// <summary>Packs one shield/blade/trap: source card id, school it applies to (-1 = any), percent.</summary>
        public int PackFx(int cardId, int fxSchool, int pct) { return cardId * 10000 + (fxSchool + 1) * 1000 + Mathf.Clamp(pct, 0, 999); }
        public int FxCard(int v) { return v / 10000; }
        public int FxSchool(int v) { return (v / 1000) % 10 - 1; }
        public int FxPct(int v) { return v % 1000; }

        public int FxCount(int[] arr, int s)
        {
            int n = 0;
            for (int k = 0; k < FX_MAX; k++) if (arr[s * FX_MAX + k] != 0) n++;
            return n;
        }

        // ------------------------------------------------------------------ pips

        /// <summary>True when the spell is the slot's own school, so each power pip counts as 2.</summary>
        public bool OnSchool(int s, int cardId)
        {
            int sc = cardDatabase.GetSchool(cardId);
            return sc != CardDatabase.GENERAL && s >= 0 && s < SLOTS && sc == school[s];
        }

        /// <summary>Pip value the slot can spend on this card (power pips count double on its own school).</summary>
        public int PipValue(int s, int cardId)
        {
            if (s < 0 || s >= SLOTS) return 0;
            return pips[s] + powerPips[s] * (OnSchool(s, cardId) ? 2 : 1);
        }

        public bool CanAfford(int s, int cardId)
        {
            if (cardId <= 0) return true;
            return cardDatabase.GetPipCost(cardId) <= PipValue(s, cardId);
        }

        /// <summary>
        /// Pays for the card like Wizard101: own-school spells spend power pips first (2 each, a white pip for an odd
        /// remainder); other spells spend white pips first and power pips as 1 each.
        /// </summary>
        private void SpendPips(int s, int cardId)
        {
            int left = cardDatabase.GetPipCost(cardId);
            if (left <= 0) return;
            if (OnSchool(s, cardId))
            {
                int use = Mathf.Min(powerPips[s], left / 2);
                powerPips[s] -= use;
                left -= use * 2;
                int w = Mathf.Min(pips[s], left);
                pips[s] -= w;
                left -= w;
                if (left > 0 && powerPips[s] > 0) { powerPips[s]--; left = 0; }
            }
            else
            {
                int w = Mathf.Min(pips[s], left);
                pips[s] -= w;
                left -= w;
                int p = Mathf.Min(powerPips[s], left);
                powerPips[s] -= p;
            }
        }

        /// <summary>"3 pips + 2 power" style text.</summary>
        public string PipText(int s)
        {
            if (s < 0 || s >= SLOTS) return "";
            string t = pips[s].ToString() + " pip" + (pips[s] == 1 ? "" : "s");
            if (powerPips[s] > 0) t += " + " + powerPips[s].ToString() + " power";
            return t;
        }

        // ------------------------------------------------------------------ schools

        /// <summary>Fire/Ice, Storm/Myth and Life/Death are opposites; Balance (and General) has none (-1).</summary>
        public int OppositeSchool(int sc)
        {
            switch (sc)
            {
                case 0: return 1;
                case 1: return 0;
                case 2: return 5;
                case 5: return 2;
                case 3: return 4;
                case 4: return 3;
                default: return -1;
            }
        }

        /// <summary>Monsters and summoned minions have school resist/weakness; wizards rely on gear.</summary>
        public bool IsCreature(int s) { return Occupied(s) && ((s >= SIDE && !pvpMode) || IsSummon(s)); }

        /// <summary>
        /// The line under a monster in the duel, Wizard101-style: "Rank 2 &lt;school icon&gt;", with " Elite" or " Boss"
        /// after it for those; a summoned minion shows "&lt;school icon&gt; Minion". The icon is a glyph in the combat
        /// pixel font, coloured as rich text (CardDatabase.SchoolIcon).
        /// </summary>
        public string MatchupText(int s)
        {
            if (!IsCreature(s) || cardDatabase == null) return "";
            string icon = cardDatabase.SchoolIcon(school[s]);
            if (slotIds[s] < 0) return (icon + " Minion").Trim();
            string text = "Rank " + cardDatabase.MonsterRank(slotIds[s]).ToString();
            if (icon.Length > 0) text += " " + icon;
            if (cardDatabase.IsBossMonster(slotIds[s])) text += " Boss";
            else if (cardDatabase.IsEliteMonster(slotIds[s])) text += " Elite";
            return text;
        }

        public string SlotName(int s)
        {
            if (!Occupied(s)) return "";
            if (slotIds[s] < 0) return cardDatabase.GetName(-slotIds[s]);
            if (s >= SIDE && !pvpMode) return cardDatabase != null ? cardDatabase.MonsterName(slotIds[s]) : "Monster";
            VRCPlayerApi p = VRCPlayerApi.GetPlayerById(slotIds[s]);
            return Utilities.IsValid(p) ? p.displayName : "Wizard";
        }

        // ------------------------------------------------------------------ requests (any client -> owner)

        public void RequestQueue(bool enter)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, enter ? nameof(NetQueueEnter) : nameof(NetQueueLeave), local.playerId);
        }

        /// <summary>
        /// Sends the local wizard's combat stats. Percent stats are packed to stay within the network event's
        /// parameter limit: statsA = damage | resist &lt;&lt; 8 | (accuracy + 128) &lt;&lt; 16,
        /// statsB = critical | block &lt;&lt; 8 | pierce &lt;&lt; 16 | power pip &lt;&lt; 24 (see <see cref="PackStats"/>).
        /// </summary>
        public void RequestJoin(int maxHealth, int curHealth, int playerSchool, int playerLevel, int statsA, int statsB)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetJoin), local.playerId, maxHealth, curHealth, playerSchool, playerLevel, statsA, statsB);
        }

        public int PackStats(int a, int b, int c, int d)
        {
            return Mathf.Clamp(a, 0, 255) | (Mathf.Clamp(b, 0, 255) << 8) | (Mathf.Clamp(c, 0, 255) << 16) | (Mathf.Clamp(d, 0, 100) << 24);
        }

        private int Unpack(int packed, int k) { return (packed >> (k * 8)) & 0xFF; }

        /// <summary>Leave the fight at once (planning only). Like Wizard101, fleeing costs all of your mana (BattleClient).</summary>
        public void RequestFlee()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetFlee), local.playerId);
        }

        [NetworkCallable]
        public void NetFlee(int playerId)
        {
            if (!Networking.IsOwner(gameObject)) return;
            int s = FindWizardSlot(playerId);
            if (s < 0) return;
            VacateSeat(s);
            CheckFightOver();
            Publish();
        }

        /// <summary>Frees a wizard seat (fled, left or defeated). Health stays as it was so the client can tell why.</summary>
        private void VacateSeat(int s)
        {
            slotIds[s] = 0;
            locked[s] = 0;
            pips[s] = 0; powerPips[s] = 0;
            focus[s] = 0; dotLeft[s] = 0; dotTurns[s] = 0; stunned[s] = 0; taunt[s] = 0; stunBlock[s] = 0;
            ClearFx(shields, s); ClearFx(blades, s); ClearFx(traps, s);
        }

        /// <summary>After a wizard leaves mid-fight: ends it when a side has nobody left (PvP) or the party is gone.</summary>
        private void CheckFightOver()
        {
            if (phase < PHASE_JOINING || phase > PHASE_PLAYBACK) return;
            if (pvpMode)
            {
                bool a = SidePlayersAlive(0), b = SidePlayersAlive(1);
                if (a && b) return;
                winnerSide = a ? 0 : b ? 1 : -1;
                EndFight(PHASE_VICTORY);
            }
            else if (!PlayersAlive()) EndFight(PHASE_DEFEAT);
        }

        private void EndFight(int endPhase)
        {
            phase = endPhase;
            phaseEndMs = Now() + endSeconds * 1000;
        }

        /// <summary>Clears a seat's combat state and gives it a placeholder health until the wizard's NetJoin arrives.</summary>
        private void ResetSeat(int s, int id)
        {
            slotIds[s] = id;
            maxHp[s] = 400; hp[s] = 400;
            pips[s] = 0; powerPips[s] = 0; powerPipPct[s] = 0; critPct[s] = 0; blockPct[s] = 0; piercePct[s] = 0;
            school[s] = 0; level[s] = 1; look[s] = 0;
            dmgPct[s] = 0; resistPct[s] = 0; accPct[s] = 0; focus[s] = 0; locked[s] = 0;
            dotLeft[s] = 0; dotTurns[s] = 0; stunned[s] = 0; taunt[s] = 0; stunBlock[s] = 0;
            ClearFx(shields, s); ClearFx(blades, s); ClearFx(traps, s);
            _joined[s] = id < 0;
            _healReady[s] = 0;
        }

        public void RequestPick(int cardId, int target) { RequestPickCast(cardId, target, 0); }

        /// <summary>A pick with how well its cast pattern was drawn (0-100; 0 = no pattern), which lowers its fizzle chance.</summary>
        public void RequestPickCast(int cardId, int target, int quality)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetPick), local.playerId, cardId, target, Mathf.Clamp(quality, 0, 100));
        }

        /// <summary>Take back this wizard's spell for the round (the waiting box's Back): they choose again.</summary>
        public void RequestUnpick()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetUnpick), local.playerId);
        }

        [NetworkCallable]
        public void NetQueueEnter(int playerId)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (phase != PHASE_IDLE && phase != PHASE_QUEUE) return;
            if (IsQueued(playerId)) return;
            int cap = Mathf.Min(QueueCapacity(), queueIds.Length);
            for (int i = 0; i < cap; i++)
            {
                if (queueIds[i] != 0) continue;
                queueIds[i] = playerId;
                // The countdown starts with the first wizard in (PvP: it restarts once an opponent arrives).
                if (phase == PHASE_IDLE || WaitingForOpponent())
                {
                    phase = PHASE_QUEUE;
                    phaseEndMs = Now() + queueSeconds * 1000;
                }
                Publish();
                return;
            }
        }

        public void RequestEngage()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetEngage), local.playerId);
        }

        /// <summary>
        /// Wandering-monster lane: a wizard touched patrol monster <paramref name="wanderer"/> (starts or joins the fight) or
        /// walked into the circle (wanderer -1).
        /// </summary>
        public void RequestEngageWander(int wanderer)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetEngageWander), local.playerId, wanderer);
        }

        /// <summary>Patrol monster w reached the circle during the fight (sent by the fighting wizards' clients).</summary>
        public void RequestWandererContact(int wanderer)
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetWandererContact), wanderer);
        }

        [NetworkCallable]
        public void NetWandererContact(int w)
        {
            if (!Networking.IsOwner(gameObject) || !wanderMode || w < 0 || w >= 31 || w >= wandererMonsters.Length) return;
            if (phase < PHASE_JOINING || phase > PHASE_PLAYBACK || (wandererGone & (1 << w)) != 0) return;
            if (evenFight)
            {
                // Even fights: it joins while a wizard is still unmatched, or fades away; either way it's gone until the respawn.
                wandererGone |= 1 << w;
                if (EvenMonsterCount() < EvenWizardCount() && !AnyMonsterDefeated()) AddMonster(wandererMonsters[w]);
                Publish();
                return;
            }
            // Like Wizard101: a monster that wanders into the circle takes a free seat and fights from the next round.
            // With no seat free, or once one of the fight's monsters has been defeated, it just walks on through.
            if (!MonsterSeatFree()) return;
            wandererGone |= 1 << w;
            AddMonster(wandererMonsters[w]);
            Publish();
        }

        [NetworkCallable]
        public void NetEngageWander(int playerId, int wanderer)
        {
            if (!Networking.IsOwner(gameObject) || !wanderMode || playerId <= 0) return;
            if (phase == PHASE_IDLE)
            {
                if (wanderer >= 0 && WandererHiddenAnyLane(wanderer)) return;
                int spot = NearestRingSpot(playerId);
                if (spot < 0) return;   // every ring nearby is in use
                for (int i = 0; i < queueIds.Length; i++) queueIds[i] = 0;
                queueIds[0] = playerId;
                ringSpot = spot;
                // Even fights start with just the monster that was touched.
                _startWanderer = wanderer;
                StartBattle();
                if (phase != PHASE_ROAMING) return;
                phase = PHASE_JOINING;
                phaseEndMs = Now() + joinSeconds * 1000;
                Publish();
                return;
            }
            if (phase < PHASE_ROAMING || phase > PHASE_PLAYBACK) return;
            if (FindWizardSlot(playerId) >= 0)
            {
                // Already in this fight and ran into another of the group's monsters (two at once): it comes too, if a
                // seat is free.
                if (wanderer >= 0 && wanderer < 31 && wanderer < wandererMonsters.Length && (wandererGone & (1 << wanderer)) == 0 &&
                    !WandererHiddenAnyLane(wanderer) && !evenFight && MonsterSeatFree())
                {
                    wandererGone |= 1 << wanderer;
                    AddMonster(wandererMonsters[wanderer]);
                    Publish();
                }
                return;
            }
            // Like Wizard101, anyone can walk into a circle with a free seat, even mid-fight; they act from the next round.
            if (phase < PHASE_JOINING) return;
            for (int s = 0; s < SIDE; s++)
            {
                if (!SeatFree(s)) continue;
                ResetSeat(s, playerId);
                _joinRound[s] = -1;
                if (phase == PHASE_JOINING) phaseEndMs = Mathf.Max(phaseEndMs, Now() + 4000);
                else
                {
                    // Joining a fight in progress: they sit this round out and can't be targeted until they're really in
                    // (from the next round), like Wizard101.
                    locked[s] = 1;
                    _joinRound[s] = round;
                }
                // The patrol monster they ran into comes with them into the fight (it leaves its patrol); in an even fight
                // only while a wizard is unmatched, otherwise whenever a monster seat is free.
                bool touched = wanderer >= 0 && wanderer < 31 && wanderer < wandererMonsters.Length && !WandererHiddenAnyLane(wanderer);
                if (evenFight)
                {
                    if (touched)
                    {
                        wandererGone |= 1 << wanderer;
                        if (EvenMonsterCount() < EvenWizardCount() && !AnyMonsterDefeated()) AddMonster(wandererMonsters[wanderer]);
                    }
                    if (EvenMonsterCount() < EvenWizardCount() && !AnyWandererLeft() && !AnyMonsterDefeated()) AddMonster(NextRosterMonster());
                }
                // A boss fight: each wizard who joins brings in one more of the boss's followers (up to its roster).
                if (!evenFight && IsBossFight() && FollowerCount() < WizardCount() && MonsterSeatFree())
                {
                    int follower = NextRosterMonster();
                    if (follower != roster[0]) AddMonster(follower);   // never a second boss
                }
                if (!evenFight && touched && MonsterSeatFree())
                {
                    wandererGone |= 1 << wanderer;
                    AddMonster(wandererMonsters[wanderer]);
                }
                Publish();
                return;
            }
        }

        /// <summary>The encounter's first monster is a boss (its followers come one per wizard).</summary>
        private bool IsBossFight()
        {
            return roster != null && roster.Length > 0 && cardDatabase != null && cardDatabase.IsBossMonster(roster[0]);
        }

        private int WizardCount()
        {
            int n = 0;
            for (int s = 0; s < SIDE; s++) if (Occupied(s) && !IsSummon(s)) n++;
            return n;
        }

        /// <summary>Monsters seated (the boss and its followers, not summons).</summary>
        private int FollowerCount()
        {
            int n = 0;
            for (int s = SIDE; s < SLOTS; s++) if (Occupied(s) && !IsSummon(s)) n++;
            return n;
        }

        /// <summary>The encounter's size for a solo wizard (rosterSize, or the roster's length).</summary>
        private int BaseSize()
        {
            int len = roster == null ? 0 : roster.Length;
            return Mathf.Max(1, rosterSize > 0 ? Mathf.Min(rosterSize, len > 0 ? len : rosterSize) : len);
        }

        /// <summary>
        /// The most monsters this fight may hold now: the encounter's size plus one per extra wizard, at most four, and the
        /// same party rules as the start of a fight (StartBattle): a boss and one follower per extra wizard, or with
        /// scaleRosterWithParty (tower floors) two for a solo wizard and one more per extra wizard. Monsters that wander in
        /// or are run into mid-fight are held to it too.
        /// </summary>
        private int MonsterCap()
        {
            int wizards = Mathf.Max(1, WizardCount());
            int cap = Mathf.Min(SIDE, BaseSize() + wizards - 1);
            if (IsBossFight()) cap = Mathf.Min(cap, wizards);
            else if (scaleRosterWithParty) cap = Mathf.Min(cap, 1 + wizards);
            return cap;
        }

        /// <summary>
        /// One of this fight's monsters (not a summon) has been defeated. Defeated monsters keep their seats until the fight
        /// ends, so this reads straight from the synced seats (and holds if the fight changes owner).
        /// </summary>
        private bool AnyMonsterDefeated()
        {
            for (int s = SIDE; s < SLOTS; s++) if (Occupied(s) && !IsSummon(s) && hp[s] <= 0) return true;
            return false;
        }

        /// <summary>
        /// A monster seat is free for one more of the group's monsters: the fight holds at most the encounter's size plus
        /// one per extra wizard (summons aside), and never more than the four seats. No replacements: once one of the
        /// fight's monsters has been defeated, no other monster joins it (it walks on through the circle).
        /// </summary>
        private bool MonsterSeatFree()
        {
            if (AnyMonsterDefeated()) return false;
            int cap = MonsterCap();
            int n = 0;
            bool free = false;
            for (int s = SIDE; s < SLOTS; s++)
            {
                if (!Occupied(s)) { free = true; continue; }
                if (!IsSummon(s)) n++;
            }
            return free && n < cap;
        }

        // ------------------------------------------------------------------ even fights

        private int EvenWizardCount()
        {
            int n = 0;
            for (int s = 0; s < SIDE; s++) if (Alive(s) && !IsSummon(s)) n++;
            return n;
        }

        private int EvenMonsterCount()
        {
            int n = 0;
            for (int s = SIDE; s < SLOTS; s++) if (Alive(s) && !IsSummon(s)) n++;
            return n;
        }

        private bool AnyWandererLeft()
        {
            for (int w = 0; w < wandererMonsters.Length && w < 31; w++) if ((wandererGone & (1 << w)) == 0) return true;
            return false;
        }

        /// <summary>A roster monster for a boss room with no patrol left: the first roster entry not already seated.</summary>
        private int NextRosterMonster()
        {
            if (roster == null || roster.Length == 0) return 0;
            for (int m = 0; m < roster.Length && m < SIDE; m++) if (!Occupied(BOSS_SLOT + m)) return roster[m];
            return roster[roster.Length - 1];
        }

        /// <summary>
        /// Seats a monster mid-fight, preferring the seat whose roster entry is that monster (each seat shows its roster
        /// monster's model). It acts from the next round, like a wizard who walks in.
        /// </summary>
        private void AddMonster(int id)
        {
            if (id <= 0 || cardDatabase.MonsterIndexOf(id) < 0) return;
            int seat = -1;
            for (int m = 0; m < SIDE; m++)
                if (!Occupied(BOSS_SLOT + m) && roster != null && m < roster.Length && roster[m] == id) { seat = BOSS_SLOT + m; break; }
            if (seat < 0)
                for (int m = 0; m < SIDE; m++) if (!Occupied(BOSS_SLOT + m)) { seat = BOSS_SLOT + m; break; }
            if (seat < 0) return;
            ResetSeat(seat, 0);
            SetMonster(seat, id, cardDatabase.MonsterSchool(id), cardDatabase.MonsterLevel(id), cardDatabase.MonsterHp(id));
            _joinRound[seat] = round;
            if (phase == PHASE_PLANNING) locked[seat] = 1;
        }

        /// <summary>
        /// The free ring spot closest to the player (one no other fight in the street is using); -1 if every spot is taken,
        /// 0 when there are no spots or the player is unknown.
        /// </summary>
        public int NearestRingSpot(int playerId)
        {
            if (ringSpots == null || ringSpots.Length == 0) return 0;
            VRCPlayerApi p = VRCPlayerApi.GetPlayerById(playerId);
            if (!Utilities.IsValid(p)) return 0;
            Vector3 pos = p.GetPosition();
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < ringSpots.Length; i++)
            {
                if (ringSpots[i] == null) continue;
                Vector3 v = ringSpots[i].position - pos;
                // Height counts extra: a ring on the terrace above isn't "near".
                float d = v.x * v.x + v.z * v.z + v.y * v.y * 9f;
                if (d < bestD && !SpotTaken(ringSpots[i])) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Another monster group in the street has a fight on at this spot.</summary>
        private bool SpotTaken(Transform spot)
        {
            if (peers == null) return false;
            for (int k = 0; k < peers.Length; k++)
            {
                BattleManager m = peers[k];
                if (m == null || m == this || m.phase == PHASE_IDLE) continue;
                if (m.CurrentRingSpot() == spot) return true;
            }
            return false;
        }

        /// <summary>The current fight's ring spot, or null when this encounter has none.</summary>
        public Transform CurrentRingSpot()
        {
            if (ringSpots == null || ringSpots.Length == 0) return null;
            return ringSpots[Mathf.Clamp(ringSpot, 0, ringSpots.Length - 1)];
        }

        /// <summary>True when a wizard seat is free (for walking into a fight in progress).</summary>
        public bool HasFreeWizardSeat()
        {
            for (int s = 0; s < SIDE; s++) if (SeatFree(s)) return true;
            return false;
        }

        /// <summary>Empty, or holding a defeated minion.</summary>
        public bool SeatFree(int s) { return !Occupied(s) || (IsSummon(s) && hp[s] <= 0); }

        [NetworkCallable]
        public void NetEngage(int playerId)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_ROAMING || FindWizardSlot(playerId) < 0) return;
            formedMs = Now();
            phase = PHASE_JOINING;
            phaseEndMs = Now() + joinSeconds * 1000;
            Publish();
        }

        [NetworkCallable]
        public void NetQueueLeave(int playerId)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_QUEUE) return;
            for (int i = 0; i < queueIds.Length; i++) if (queueIds[i] == playerId) queueIds[i] = 0;
            if (QueueCount() == 0) phase = PHASE_IDLE;
            Publish();
        }

        [NetworkCallable]
        public void NetJoin(int playerId, int maxHealth, int curHealth, int playerSchool, int playerLevel, int statsA, int statsB)
        {
            if (!Networking.IsOwner(gameObject) || phase < PHASE_JOINING || phase > PHASE_PLAYBACK) return;
            int s = FindWizardSlot(playerId);
            if (s < 0 || _joined[s]) return;
            maxHp[s] = Mathf.Clamp(maxHealth, 1, 99999);
            // Wizard101 health carries over between fights: you fight with what you walked in with.
            hp[s] = Mathf.Clamp(curHealth, 1, maxHp[s]);
            school[s] = Mathf.Clamp(playerSchool, 0, 6);
            // playerLevel carries the creator colours above the level (see BattleClient's join): level | look << 8.
            level[s] = Mathf.Clamp(playerLevel & 0xFF, 1, 255);
            look[s] = (playerLevel >> 8) & 0xFFFFFF;   // signature | aura << 8 | spell skin << 16
            dmgPct[s] = Mathf.Clamp(Unpack(statsA, 0), 0, 200);
            resistPct[s] = Mathf.Clamp(Unpack(statsA, 1), 0, 80);
            accPct[s] = Mathf.Clamp(Unpack(statsA, 2) - 128, -50, 100);
            critPct[s] = Mathf.Clamp(Unpack(statsB, 0), 0, 100);
            blockPct[s] = Mathf.Clamp(Unpack(statsB, 1), 0, 100);
            piercePct[s] = Mathf.Clamp(Unpack(statsB, 2), 0, 100);
            powerPipPct[s] = Mathf.Clamp(Unpack(statsB, 3) + (level[s] >= powerPipLevel ? basePowerPipChance : 0), 0, 100);
            _joined[s] = true;
            Publish();
        }

        [NetworkCallable]
        public void NetPick(int playerId, int cardId, int target, int quality)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_PLANNING) return;
            int s = FindWizardSlot(playerId);
            if (!Alive(s) || locked[s] != 0) return;
            if (cardId != 0 && cardId != PICK_POTION && (cardDatabase.IndexOf(cardId) < 0 || !CanAfford(s, cardId))) cardId = 0;
            _pickCard[s] = cardId;
            _pickTarget[s] = target;
            _pickQuality[s] = Mathf.Clamp(quality, 0, 100);
            locked[s] = 1;
            Publish();
        }

        /// <summary>
        /// A wizard took their spell back before the round started: unlocked, nothing picked, so they can choose again
        /// (too late once the round has started; a potion already drunk can't be taken back; a stunned wizard has nothing to take back).
        /// </summary>
        [NetworkCallable]
        public void NetUnpick(int playerId)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_PLANNING) return;
            int s = FindWizardSlot(playerId);
            if (!Alive(s) || locked[s] == 0 || stunned[s] != 0 || _pickCard[s] == PICK_POTION) return;
            _pickCard[s] = 0;
            _pickTarget[s] = -1;
            _pickQuality[s] = 0;
            locked[s] = 0;
            Publish();
        }

        // ------------------------------------------------------------------ owner loop

        private void Update()
        {
            if (phase == PHASE_IDLE || !Networking.IsOwner(gameObject)) return;   // the cheap check first (most rings are idle)
            bool timeUp = phaseEndMs - Now() <= 0;
            if (phase == PHASE_QUEUE)
            {
                if (timeUp && !WaitingForOpponent()) StartBattle();
            }
            else if (phase == PHASE_JOINING)
            {
                // Everyone pulled in gets a moment to arrive and sit down before the first round.
                bool graceOver = Now() - formedMs >= (int)(startGraceSeconds * 1000f);
                if (timeUp || (AllJoined() && graceOver)) BeginRound();
            }
            else if (phase == PHASE_PLANNING)
            {
                if (timeUp || AllLocked()) Resolve();
            }
            else if (phase == PHASE_PLAYBACK)
            {
                if (timeUp) AfterPlayback();
            }
            else if (timeUp)
            {
                for (int s = 0; s < SLOTS; s++) slotIds[s] = 0;
                phase = PHASE_IDLE;
                lastEndMs = Now();
                Publish();
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Networking.IsOwner(gameObject) || !Utilities.IsValid(player)) return;
            int id = player.playerId;
            bool changed = false;
            for (int i = 0; i < queueIds.Length; i++)
                if (queueIds[i] == id) { queueIds[i] = 0; changed = true; }
            int seats = PlayerSeats();
            for (int i = 0; i < seats; i++)
                if (slotIds[i] == id) { VacateSeat(i); hp[i] = 0; changed = true; }
            if (phase == PHASE_QUEUE && QueueCount() == 0) phase = PHASE_IDLE;
            if (changed) CheckFightOver();
            if (changed) Publish();
        }

        public override void OnDeserialization()
        {
            if (client != null) client.OnBattleState();
        }

        private void Publish()
        {
            RequestSerialization();
            if (client != null) client.OnBattleState();
        }

        private bool AllJoined()
        {
            int n = PlayerSeats();
            for (int s = 0; s < n; s++) if (Occupied(s) && !_joined[s]) return false;
            return true;
        }

        private bool AllLocked()
        {
            int n = PlayerSeats();
            for (int s = 0; s < n; s++) if (Alive(s) && !IsSummon(s) && locked[s] == 0) return false;
            return true;
        }

        /// <summary>A wizard (not just a summoned minion) is still standing on this side.</summary>
        private bool SidePlayersAlive(int side)
        {
            for (int i = 0; i < SIDE; i++)
            {
                int s = side * SIDE + i;
                if (Alive(s) && !IsSummon(s)) return true;
            }
            return false;
        }

        private bool SideAlive(int side)
        {
            for (int i = 0; i < SIDE; i++) if (Alive(side * SIDE + i)) return true;
            return false;
        }

        private bool PlayersAlive()
        {
            for (int s = 0; s < SIDE; s++) if (Alive(s) && !IsSummon(s)) return true;
            return false;
        }

        private void StartBattle()
        {
            bubbleRounds = 0;
            bubblePct = 0;
            bubbleSchool = -1;
            if (pvpMode) { StartDuel(); return; }
            int n = 0;
            for (int s = 0; s < SLOTS; s++)
            {
                ResetSeat(s, 0);
                hp[s] = 0; maxHp[s] = 0;
            }
            for (int i = 0; i < queueIds.Length; i++)
            {
                if (queueIds[i] == 0) continue;
                if (n < SIDE) { ResetSeat(n, queueIds[i]); n++; }
                queueIds[i] = 0;
            }
            if (n == 0) { phase = PHASE_IDLE; Publish(); return; }

            for (int s = 0; s < SLOTS; s++) _joinRound[s] = -1;
            // Round 0 before any monster is seated: AddMonster marks a seat as joining in the current round, and a stale
            // round number from the last fight would make a starting monster sit out its first turn.
            round = 0;
            // Patrol monsters from the last fight that haven't respawned yet stay gone.
            if (!(lastEndMs != 0 && Now() - lastEndMs < respawnSeconds * 1000)) wandererGone = 0;
            // The patrol monster that was touched leaves its route for the fight (the rest of the patrol keeps walking).
            if (_startWanderer >= 0 && _startWanderer < 31) wandererGone |= 1 << _startWanderer;
            int count = roster == null ? 0 : Mathf.Min(roster.Length, SIDE);
            bool bossFight = IsBossFight();
            // The limit grows by one per extra wizard: the encounter's size plus (wizards - 1), at most the four seats.
            count = Mathf.Min(count, BaseSize() + n - 1);
            // Bosses: a solo wizard faces the boss alone; each extra wizard brings one of its followers.
            if (bossFight) count = Mathf.Min(count, n);
            else if (scaleRosterWithParty) count = Mathf.Min(count, 1 + n);
            if (wanderMode && !bossFight && count > 0)
            {
                // Street fights start with just the monster that was run into (more join as they're run into or wander
                // into the circle, while a seat is free). Bosses still come with their followers.
                int w = _startWanderer;
                int first = w >= 0 && w < wandererMonsters.Length && w < 31 ? wandererMonsters[w] : roster[0];
                if (w >= 0 && w < 31) wandererGone |= 1 << w;
                AddMonster(first);
                if (MonsterCount() == 0) AddMonster(roster[0]);
                count = MonsterCount();
            }
            else if (evenFight && count > 0)
            {
                // One monster per wizard: the patrol monster that was touched (it leaves its patrol).
                int w = _startWanderer;
                int first = w >= 0 && w < wandererMonsters.Length && w < 31 ? wandererMonsters[w] : roster[0];
                if (w >= 0 && w < 31) wandererGone |= 1 << w;
                AddMonster(first);
                if (MonsterCount() == 0) AddMonster(roster[0]);
                count = MonsterCount();
            }
            else
                for (int m = 0; m < count; m++)
                {
                    int id = roster[m];
                    int health = cardDatabase.MonsterHp(id) + (cardDatabase.IsBossMonster(id) ? cardDatabase.MonsterHpPerExtraPlayer(id) * (n - 1) : 0);
                    SetMonster(BOSS_SLOT + m, id, cardDatabase.MonsterSchool(id), cardDatabase.MonsterLevel(id), health);
                }
            _startWanderer = -1;
            if (count == 0) { phase = PHASE_IDLE; Publish(); return; }

            battleId++;
            formedMs = Now();
            round = 0;
            actCount = 0;
            fxLogCount = 0;
            _pendFx = 0;
            firstSide = Random.Range(0, 2);
            phase = PHASE_ROAMING;
            phaseEndMs = Now() + roamSeconds * 1000;
            Publish();
        }

        /// <summary>
        /// PvP: everyone queued in the zone is dealt into two teams, alternating (first in = Blue team's first seat,
        /// second = Red team's first seat...), and the duel starts at once. With only one wizard the countdown waits.
        /// </summary>
        private void StartDuel()
        {
            int queued = 0;
            for (int i = 0; i < queueIds.Length; i++)
            {
                if (queueIds[i] == 0) continue;
                // Drop anyone who left the instance during the countdown.
                VRCPlayerApi p = VRCPlayerApi.GetPlayerById(queueIds[i]);
                if (!Utilities.IsValid(p)) { queueIds[i] = 0; continue; }
                queued++;
            }
            if (queued == 0) { phase = PHASE_IDLE; Publish(); return; }
            if (queued < 2)
            {
                // Hold the countdown until an opponent steps in (NetQueueEnter restarts it).
                phaseEndMs = 0;
                Publish();
                return;
            }
            for (int s = 0; s < SLOTS; s++)
            {
                ResetSeat(s, 0);
                hp[s] = 0; maxHp[s] = 0;
                _joinRound[s] = -1;
            }
            int n = 0;
            for (int i = 0; i < queueIds.Length; i++)
            {
                if (queueIds[i] == 0) continue;
                int seat = (n % 2) * SIDE + n / 2;
                if (seat < SLOTS) ResetSeat(seat, queueIds[i]);
                n++;
                queueIds[i] = 0;
            }
            winnerSide = -1;
            battleId++;
            formedMs = Now();
            round = 0;
            actCount = 0;
            fxLogCount = 0;
            _pendFx = 0;
            firstSide = Random.Range(0, 2);
            phase = PHASE_JOINING;
            phaseEndMs = Now() + joinSeconds * 1000;
            Publish();
        }

        private void SetMonster(int s, int kind, int monsterSchool, int monsterLevel, int health)
        {
            slotIds[s] = kind;
            school[s] = monsterSchool;
            level[s] = monsterLevel;
            maxHp[s] = health;
            hp[s] = health;
            powerPipPct[s] = monsterLevel >= powerPipLevel ? basePowerPipChance : 0;
            critPct[s] = cardDatabase.IsBossMonster(kind) ? bossCritPct : 0;
        }

        private void BeginRound()
        {
            round++;
            for (int s = 0; s < SLOTS; s++)
            {
                locked[s] = 0;
                _pickCard[s] = 0;
                _pickTarget[s] = -1;
                _pickQuality[s] = 0;
                if (!Alive(s)) continue;
                // Minions act on their own and stunned wizards sit this round out.
                if (IsSummon(s) || stunned[s] != 0) locked[s] = 1;
                // One pip per round into a free pip slot; it's a power pip on a power pip roll.
                if (IsSummon(s) || pips[s] + powerPips[s] >= MAX_PIPS) continue;
                if (Random.Range(0, 100) < powerPipPct[s]) powerPips[s]++;
                else pips[s]++;
            }
            phase = PHASE_PLANNING;
            phaseEndMs = Now() + planningSeconds * 1000;
            Publish();
        }

        private void AfterPlayback()
        {
            if (pvpMode)
            {
                // Knocked-out wizards stay in their seats to watch; the duel ends when a team has nobody standing.
                bool a = SidePlayersAlive(0), b = SidePlayersAlive(1);
                if (a && b) { BeginRound(); return; }
                winnerSide = a ? 0 : b ? 1 : -1;
                phase = PHASE_VICTORY;
                phaseEndMs = Now() + endSeconds * 1000;
                Publish();
                return;
            }
            if (!SideAlive(1)) phase = PHASE_VICTORY;
            else if (!PlayersAlive()) phase = PHASE_DEFEAT;
            else
            {
                // Defeated wizards are sent home at once (BattleClient sees its seat vacated with 0 health), which frees
                // their seat for someone else to walk in.
                for (int s = 0; s < SIDE; s++) if (Occupied(s) && !IsSummon(s) && hp[s] <= 0) VacateSeat(s);
                BeginRound();
                return;
            }
            phaseEndMs = Now() + endSeconds * 1000;
            Publish();
        }

        // ------------------------------------------------------------------ resolution

        private void Resolve()
        {
            for (int s = SIDE; s < SLOTS && !pvpMode; s++)
            {
                if (!Alive(s) || IsSummon(s)) continue;
                // A monster that joined this round sits it out (it acts from the next one).
                if (_joinRound[s] == round && round > 0) { _pickCard[s] = 0; _pickTarget[s] = -1; continue; }
                PickForMonster(s);
            }
            for (int s = 0; s < SLOTS; s++) if (taunt[s] > 0) taunt[s]--;

            actCount = 0;
            fxLogCount = 0;
            _pendFx = 0;
            int group = 0;
            if (bubbleRounds > 0) bubbleRounds--;
            for (int s = 0; s < SLOTS; s++)
            {
                if (!Alive(s) || dotLeft[s] <= 0 || dotTurns[s] <= 0) continue;
                int tick = dotLeft[s] / dotTurns[s];
                dotLeft[s] -= tick;
                dotTurns[s]--;
                if (dotTurns[s] <= 0) dotLeft[s] = 0;
                hp[s] = Mathf.Max(0, hp[s] - tick);
                AddAct(group, s, s, 0, KIND_DOT, tick);
                group++;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                int side = pass == 0 ? firstSide : 1 - firstSide;
                for (int i = 0; i < SIDE; i++)
                {
                    int s = side * SIDE + i;
                    if (!Alive(s)) continue;
                    if (!SideAlive(1 - side)) break;
                    if (stunned[s] != 0) { stunned[s] = 0; AddAct(group, s, s, 0, KIND_STUNNED, 0); }
                    else if (IsSummon(s)) MinionAct(s, group);
                    else Cast(s, _pickCard[s], _pickTarget[s], group);
                    group++;
                }
            }
            phase = PHASE_PLAYBACK;
            float total = 0f;
            for (int i = 0; i < actCount; i++)
                if (i == 0 || actGroup[i] != actGroup[i - 1]) total += StepSeconds(i);
            phaseEndMs = Now() + (int)(total * 1000f) + 1500;
            Publish();
        }

        /// <summary>
        /// Monster AI: heal a badly hurt ally, sometimes blade/shield itself first, otherwise attack with the biggest
        /// affordable spell (occasionally holding pips for a bigger one, like Wizard101 monsters do).
        /// </summary>
        private void PickForMonster(int s)
        {
            int monster = slotIds[s];
            int mySide = SideOf(s);
            int attack = 0, heal = 0, buff = 0, affordable = 0;
            for (int i = 0; i < CardDatabase.MONSTER_SPELLS; i++)
            {
                int spell = cardDatabase.MonsterSpell(monster, i);
                if (spell == 0 || !CanAfford(s, spell)) continue;
                affordable++;
                int e = cardDatabase.GetEffect(spell);
                if (e == CardDatabase.EFFECT_HEAL) { if (heal == 0 || cardDatabase.GetPower(spell) > cardDatabase.GetPower(heal)) heal = spell; }
                else if (e == CardDatabase.EFFECT_EMPOWER || e == CardDatabase.EFFECT_WARD || e == CardDatabase.EFFECT_TRIBLADE ||
                         e == CardDatabase.EFFECT_TRISHIELD || e == CardDatabase.EFFECT_MULTIWARD)
                {
                    int[] arr = e == CardDatabase.EFFECT_WARD || e == CardDatabase.EFFECT_TRISHIELD || e == CardDatabase.EFFECT_MULTIWARD ? shields : blades;
                    if (FxCount(arr, s) == 0) buff = spell;
                }
                else if (e == CardDatabase.EFFECT_FIELD) { if (bubbleRounds <= 0 || bubbleSchool != cardDatabase.GetSchool(spell)) buff = spell; }
                else if (!cardDatabase.TargetsAllies(spell) && (attack == 0 || cardDatabase.GetPipCost(spell) > cardDatabase.GetPipCost(attack))) attack = spell;
            }

            bool boss = cardDatabase.IsBossMonster(monster);
            int below = boss ? bossHealBelowPct : 45;
            int hurt = -1;
            for (int i = 0; i < SIDE; i++)
            {
                int a = mySide * SIDE + i;
                if (Alive(a) && hp[a] * 100 < maxHp[a] * below && (hurt < 0 || hp[a] * 1000 / maxHp[a] < hp[hurt] * 1000 / maxHp[hurt])) hurt = a;
            }
            if (heal != 0 && hurt >= 0 && round >= _healReady[s])
            {
                // A boss only might heal: likelier the lower the hurt one's health (bossHealChance just under the line,
                // bossHealChanceLow near 0). Other monsters always heal a badly hurt ally.
                bool go = true;
                if (boss)
                {
                    float low = 1f - Mathf.Clamp01(hp[hurt] * 100f / Mathf.Max(1, maxHp[hurt]) / Mathf.Max(1, below));
                    int chance = Mathf.RoundToInt(Mathf.Lerp(bossHealChance, bossHealChanceLow, low));
                    go = Random.Range(0, 100) < chance;
                }
                if (go)
                {
                    _pickCard[s] = heal;
                    _pickTarget[s] = hurt;
                    // Healing grace: a boss waits bossHealGraceRounds after any heal; another monster 2 after healing itself.
                    if (boss) _healReady[s] = round + 1 + Mathf.Max(0, bossHealGraceRounds);
                    else if (hurt == s) _healReady[s] = round + 3;
                    return;
                }
            }
            if (buff != 0 && Random.value < 0.4f) { _pickCard[s] = buff; _pickTarget[s] = s; return; }

            int best = attack;
            if (affordable > 0 && Random.value < 0.25f && pips[s] + powerPips[s] < MAX_PIPS) best = 0;
            _pickCard[s] = best;
            _pickTarget[s] = EnemyTarget(s);
        }

        /// <summary>A taunting enemy if there is one, otherwise a random living enemy.</summary>
        private int EnemyTarget(int s)
        {
            int enemySide = 1 - SideOf(s);
            for (int i = 0; i < SIDE; i++)
            {
                int t = enemySide * SIDE + i;
                if (Targetable(t) && taunt[t] > 0) return t;
            }
            return RandomAlive(enemySide);
        }

        /// <summary>A summoned minion's automatic turn, based on its school.</summary>
        private void MinionAct(int s, int group)
        {
            int card = SummonCard(s);
            int sc = cardDatabase.GetSchool(card);
            int power = cardDatabase.GetAux(card);
            int mySide = SideOf(s);
            if (sc == 3)
            {
                int best = -1;
                for (int i = 0; i < SIDE; i++)
                {
                    int a = mySide * SIDE + i;
                    if (Alive(a) && (best < 0 || hp[a] * 1000 / maxHp[a] < hp[best] * 1000 / maxHp[best])) best = a;
                }
                if (best < 0) { AddAct(group, s, s, 0, KIND_PASS, 0); return; }
                int heal = Mathf.Min(power, maxHp[best] - hp[best]);
                hp[best] += heal;
                AddAct(group, s, best, card, KIND_HEAL, heal);
                return;
            }
            if (sc == 1) taunt[s] = 1;
            int t = EnemyTarget(s);
            if (t < 0) { AddAct(group, s, s, 0, KIND_PASS, 0); return; }
            if (sc == CardDatabase.TECH)
            {
                // A drone, turret or mech fires a three-shot burst (each a hit of its own, like a barrage).
                for (int h = 0; h < 3 && hp[t] > 0; h++)
                {
                    int shot = Hit(s, t, power * Random.Range(30, 40) / 100, sc, 100, 0);
                    AddAct(group, s, t, card, KIND_DAMAGE, shot);
                }
                return;
            }
            int amount = Hit(s, t, power * Random.Range(90, 111) / 100, sc, 100, 0);
            AddAct(group, s, t, card, KIND_DAMAGE, amount);
            if (sc == 4 && amount > 0)
            {
                int drain = Mathf.Min(amount / 2, maxHp[s] - hp[s]);
                hp[s] += drain;
                AddAct(group, s, s, card, KIND_HEAL, drain);
            }
        }

        /// <summary>
        /// One hit from slot s (power already rolled), in Wizard101 order: damage bonus, blades, the target's traps,
        /// shields, resist and creature school matchup, then a critical roll (which the target may block). pierce% of
        /// the shield block and resist is ignored. Sets the flags for the next <see cref="AddAct"/>.
        /// </summary>
        private int Hit(int s, int t, int power, int spellSchool, int bladeMul, int pierce)
        {
            _nextFlags = 0;
            pierce = Mathf.Clamp(pierce + piercePct[s], 0, 100);
            int amount = power * (100 + dmgPct[s]) / 100;
            amount = amount * bladeMul / 100;
            // The school bubble (or grid) boosts every spell of its school, whoever casts it.
            if (bubbleRounds > 0 && spellSchool >= 0 && spellSchool == bubbleSchool) amount = amount * (100 + bubblePct) / 100;
            amount = amount * UseFx(traps, FX_TRAP, t, spellSchool, true) / 100;
            int shieldMul = UseFx(shields, FX_SHIELD, t, spellSchool, false);
            shieldMul = 100 - (100 - shieldMul) * (100 - pierce) / 100;
            amount = amount * shieldMul / 100;
            amount = amount * (100 - resistPct[t] * (100 - pierce) / 100) / 100;
            if (IsCreature(t) && spellSchool >= 0)
            {
                if (spellSchool == school[t])
                {
                    amount = amount * (100 - creatureSchoolResist * (100 - pierce) / 100) / 100;
                    _nextFlags |= FLAG_RESIST;
                }
                else if (spellSchool == OppositeSchool(school[t]))
                {
                    amount = amount * (100 + creatureSchoolWeakness) / 100;
                    _nextFlags |= FLAG_WEAK;
                }
            }
            amount = Critical(s, t, amount);
            if (amount < 1) amount = 1;
            hp[t] = Mathf.Max(0, hp[t] - amount);
            return amount;
        }

        /// <summary>Rolls the caster's critical chance; the target's block chance can stop it. Adds to the act flags.</summary>
        private int Critical(int s, int t, int amount)
        {
            if (critPct[s] <= 0 || Random.Range(0, 100) >= critPct[s]) return amount;
            if (t >= 0 && blockPct[t] > 0 && Random.Range(0, 100) < blockPct[t]) { _nextFlags |= FLAG_BLOCKED; return amount; }
            _nextFlags |= FLAG_CRIT;
            return amount * critMultiplier / 100;
        }

        /// <summary>
        /// Tech's hack: every blade on t (and its shields, when 'shields') moves onto s. The target's are shown breaking;
        /// the caster gains them (a buff act with how many).
        /// </summary>
        private void Hack(int s, int t, bool alsoShields, int cardId, int group)
        {
            if (t < 0 || t == s) return;
            int stolen = 0;
            int b = t * FX_MAX;
            for (int k = 0; k < FX_MAX; k++)
                if (blades[b + k] != 0) { AddFx(blades, s, blades[b + k]); stolen++; }
            if (stolen > 0) BreakFx(blades, FX_BLADE, t);
            if (alsoShields)
            {
                int took = 0;
                for (int k = 0; k < FX_MAX; k++)
                    if (shields[b + k] != 0) { AddFx(shields, s, shields[b + k]); took++; }
                if (took > 0) BreakFx(shields, FX_SHIELD, t);
                stolen += took;
            }
            if (stolen > 0) AddAct(group, s, s, cardId, KIND_BUFF, stolen);
        }

        /// <summary>Remembers a used-up effect for the next act entry (the hit it belongs to).</summary>
        private void PendFx(int s, int kind, int v)
        {
            if (_pendFx >= MAX_FX_LOG) return;
            _pendSlot[_pendFx] = s;
            _pendKind[_pendFx] = kind;
            _pendValue[_pendFx] = v;
            _pendFx++;
        }

        /// <summary>Clears every entry (shatter / dispel), logging each so clients can show it breaking.</summary>
        private void BreakFx(int[] arr, int kind, int s)
        {
            for (int k = 0; k < FX_MAX; k++)
                if (arr[s * FX_MAX + k] != 0) PendFx(s, kind, arr[s * FX_MAX + k]);
            ClearFx(arr, s);
        }

        private void ClearFx(int[] arr, int s)
        {
            for (int k = 0; k < FX_MAX; k++) arr[s * FX_MAX + k] = 0;
        }

        private int FreeSeat(int side)
        {
            for (int i = 0; i < SIDE; i++) if (!Occupied(side * SIDE + i) || hp[side * SIDE + i] <= 0 && IsSummon(side * SIDE + i)) return side * SIDE + i;
            return -1;
        }

        private void Summon(int s, int cardId, int power, int group)
        {
            int seat = FreeSeat(SideOf(s));
            if (seat < 0) { AddAct(group, s, -1, cardId, KIND_SUMMON, 0); return; }
            ResetSeat(seat, -cardId);
            school[seat] = cardDatabase.GetSchool(cardId);
            level[seat] = level[s];
            maxHp[seat] = power;
            hp[seat] = power;
            locked[seat] = 1;
            AddAct(group, s, seat, cardId, KIND_SUMMON, power);
        }

        /// <summary>
        /// A wizard who walked into the fight this round isn't in it yet: they sit the round out and nothing can target
        /// them (single-target or area spells) until the next round.
        /// </summary>
        private bool Entering(int s)
        {
            return s >= 0 && s < PlayerSeats() && !IsSummon(s) && round > 0 && _joinRound[s] == round;
        }

        /// <summary>Alive and in the fight (not still walking in).</summary>
        private bool Targetable(int s) { return Alive(s) && !Entering(s); }

        private int RandomAlive(int side)
        {
            int count = 0;
            for (int i = 0; i < SIDE; i++) if (Targetable(side * SIDE + i)) count++;
            if (count == 0) return -1;
            int k = Random.Range(0, count);
            for (int i = 0; i < SIDE; i++)
            {
                int s = side * SIDE + i;
                if (!Targetable(s)) continue;
                if (k == 0) return s;
                k--;
            }
            return -1;
        }

        private int ValidTarget(int target, int side)
        {
            if (Targetable(target) && SideOf(target) == side) return target;
            for (int i = 0; i < SIDE; i++) if (Targetable(side * SIDE + i)) return side * SIDE + i;
            return -1;
        }

        private void Cast(int s, int cardId, int target, int group)
        {
            int first = actCount;
            CastSpell(s, cardId, target, group);
            // An attack spell that went off plays as a cinematic, a wizard's or a monster's (every client times it from this flag).
            if (actCount > first && IsCastAct(first) && Occupied(s) && !IsSummon(s) && cardDatabase.CineStyle(cardId) != 0)
                actFlags[first] |= FLAG_CINE;
        }

        private void CastSpell(int s, int cardId, int target, int group)
        {
            if (cardId == PICK_POTION)
            {
                // Drinking a potion uses the turn; target carries the heal amount (the client spent the potion).
                int drink = Mathf.Min(Mathf.Clamp(target, 0, 99999), maxHp[s] - hp[s]);
                hp[s] += drink;
                AddAct(group, s, s, 0, KIND_POTION, drink);
                return;
            }
            if (cardId == 0 || cardDatabase.IndexOf(cardId) < 0 || !CanAfford(s, cardId))
            {
                AddAct(group, s, s, 0, KIND_PASS, 0);
                return;
            }

            // A drawn cast pattern takes its share off the chance to fizzle (CastPattern; monsters never draw one).
            int miss = FizzleChance(s, cardId, _pickQuality[s]);
            focus[s] = 0;
            if (Random.Range(0, 100) < miss)
            {
                // Wizard101: a fizzle keeps your pips (the card and its mana are spent).
                AddAct(group, s, s, cardId, KIND_FIZZLE, 0);
                return;
            }
            SpendPips(s, cardId);

            int effect = cardDatabase.GetEffect(cardId);
            int power = cardDatabase.GetPower(cardId);
            int mySide = SideOf(s);
            bool aoe = cardDatabase.IsAoe(cardId);

            int auxVal = cardDatabase.GetAux(cardId);
            if (effect == CardDatabase.EFFECT_FIELD)
            {
                // One bubble or grid at a time: a new one replaces the last.
                bubbleSchool = cardDatabase.GetSchool(cardId);
                bubblePct = power;
                bubbleRounds = Mathf.Max(1, auxVal) + 1;   // +1: this round's tick has passed; it lasts aux full rounds
                AddAct(group, s, s, cardId, KIND_BUFF, power);
                return;
            }
            if (effect == CardDatabase.EFFECT_DAMAGE || effect == CardDatabase.EFFECT_DRAIN || effect == CardDatabase.EFFECT_DOT ||
                effect == CardDatabase.EFFECT_STUN || effect == CardDatabase.EFFECT_SHATTER || effect == CardDatabase.EFFECT_STRIKEFX ||
                effect == CardDatabase.EFFECT_BARRAGE || effect == CardDatabase.EFFECT_HACK)
            {
                int n = CollectTargets(aoe, target, 1 - mySide);
                int spellSchool = cardDatabase.GetSchool(cardId);
                int pierce = effect == CardDatabase.EFFECT_DAMAGE || effect == CardDatabase.EFFECT_STUN ? Mathf.Clamp(auxVal, 0, 100) : 0;
                // Blades are used once per cast (even when it hits every enemy); traps and shields once per target hit.
                // A burn spell always lands a hit (its own, or a share of its burn), and its blade boosts the burn too.
                bool dot = effect == CardDatabase.EFFECT_DOT;
                int bladeMul = power > 0 || dot ? UseFx(blades, FX_BLADE, s, spellSchool, true) : 100;
                int total = 0;
                for (int k = 0; k < n; k++)
                {
                    int t = _targets[k];
                    if (effect == CardDatabase.EFFECT_SHATTER && power > 0) BreakFx(shields, FX_SHIELD, t);
                    else if (effect == CardDatabase.EFFECT_SHATTER) ClearFx(shields, t);
                    if (effect == CardDatabase.EFFECT_BARRAGE && power > 0)
                    {
                        // Every shot is its own hit (each uses up a shield and shows its own number); the blade boosts them all.
                        int shots = Mathf.Clamp(auxVal, 1, 12);
                        for (int h = 0; h < shots && hp[t] > 0; h++)
                        {
                            int shot = Hit(s, t, cardDatabase.RollPower(cardId), spellSchool, bladeMul, 0);
                            total += shot;
                            AddAct(group, s, t, cardId, KIND_DAMAGE, shot);
                        }
                    }
                    else if (power > 0 || dot)
                    {
                        int amount = Hit(s, t, dot ? cardDatabase.DotHit(cardId, true) : cardDatabase.RollPower(cardId), spellSchool, bladeMul, pierce);
                        total += amount;
                        AddAct(group, s, t, cardId, KIND_DAMAGE, amount);
                    }
                    if (effect == CardDatabase.EFFECT_HACK && hp[s] > 0) Hack(s, t, auxVal == 1, cardId, group);
                    if (dot && hp[t] > 0)
                    {
                        // The burn: boosted, then by the blade and the caster's damage bonus, less the target's resist.
                        int burn = cardDatabase.DotTotal(cardId) * bladeMul / 100;
                        burn = burn * (100 + dmgPct[s]) / 100;
                        burn = burn * (100 - resistPct[t]) / 100;
                        burn = Mathf.Max(1, burn);
                        dotLeft[t] += burn;
                        dotTurns[t] = CardDatabase.DOT_ROUNDS;
                        AddAct(group, s, t, cardId, KIND_BUFF, burn);
                    }
                    if (effect == CardDatabase.EFFECT_STUN && hp[t] > 0)
                    {
                        // A stun shield (gained from the last stun) stops it; otherwise the target is stunned and gains one.
                        int flag;
                        if (stunBlock[t] > 0) { stunBlock[t]--; flag = FLAG_STUNBLOCK; }
                        else { stunned[t] = 1; stunBlock[t] = 1; flag = FLAG_STUNNED; }
                        if (actCount > 0 && actTarget[actCount - 1] == t) actFlags[actCount - 1] |= flag;
                    }
                    if (effect == CardDatabase.EFFECT_STRIKEFX && auxVal / 1000 == 2 && hp[t] > 0)
                    {
                        AddFx(traps, t, PackFx(cardId, spellSchool, auxVal % 1000));
                        AddAct(group, s, t, cardId, KIND_BUFF, auxVal % 1000);
                    }
                }
                if (effect == CardDatabase.EFFECT_STRIKEFX && auxVal / 1000 != 2 && hp[s] > 0)
                {
                    // The caster's own reward for the strike: a shield (any school) or a blade of the spell's school.
                    if (auxVal / 1000 == 0) AddFx(shields, s, PackFx(cardId, CardDatabase.ANY_SCHOOL, auxVal % 1000));
                    else AddFx(blades, s, PackFx(cardId, spellSchool, auxVal % 1000));
                    AddAct(group, s, s, cardId, KIND_BUFF, auxVal % 1000);
                }
                if (effect == CardDatabase.EFFECT_DRAIN && total > 0)
                {
                    int pct = auxVal > 0 ? auxVal : 50;
                    int heal = Mathf.Min(total * pct / 100, maxHp[s] - hp[s]);
                    hp[s] += heal;
                    AddAct(group, s, s, cardId, KIND_HEAL, heal);
                }
                return;
            }

            if (effect == CardDatabase.EFFECT_DETONATE || effect == CardDatabase.EFFECT_DISPEL)
            {
                int dn = CollectTargets(aoe, target, 1 - mySide);
                for (int k = 0; k < dn; k++)
                {
                    int t = _targets[k];
                    if (effect == CardDatabase.EFFECT_DISPEL)
                    {
                        BreakFx(shields, FX_SHIELD, t);
                        BreakFx(blades, FX_BLADE, t);
                        AddAct(group, s, t, cardId, KIND_BUFF, 0);
                        continue;
                    }
                    int burst = dotLeft[t] * (100 + power) / 100;
                    dotLeft[t] = 0;
                    dotTurns[t] = 0;
                    hp[t] = Mathf.Max(0, hp[t] - burst);
                    AddAct(group, s, t, cardId, KIND_DAMAGE, burst);
                }
                return;
            }

            if (effect == CardDatabase.EFFECT_MINION) { Summon(s, cardId, power, group); return; }

            if (effect == CardDatabase.EFFECT_TAUNT)
            {
                taunt[s] = auxVal;
                AddFx(shields, s, PackFx(cardId, CardDatabase.ANY_SCHOOL, power));
                AddAct(group, s, s, cardId, KIND_BUFF, power);
                return;
            }

            if (effect == CardDatabase.EFFECT_TRAP || effect == CardDatabase.EFFECT_TRITRAP || effect == CardDatabase.EFFECT_CURSE)
            {
                int tn = CollectTargets(aoe, target, 1 - mySide);
                for (int k = 0; k < tn; k++)
                {
                    int t = _targets[k];
                    if (effect == CardDatabase.EFFECT_TRITRAP)
                    {
                        int first = cardDatabase.TriBladeFirstSchool(cardId);
                        for (int j = 0; j < 3; j++) AddFx(traps, t, PackFx(cardId, first + j, power));
                    }
                    else AddFx(traps, t, PackFx(cardId, effect == CardDatabase.EFFECT_CURSE ? CardDatabase.ANY_SCHOOL : cardDatabase.TrapSchool(cardId), power));
                    AddAct(group, s, t, cardId, KIND_BUFF, power);
                }
                // Dread Mark's price: the caster is trapped too.
                if (effect == CardDatabase.EFFECT_CURSE && tn > 0)
                {
                    AddFx(traps, s, PackFx(cardId, CardDatabase.ANY_SCHOOL, auxVal));
                    AddAct(group, s, s, cardId, KIND_BUFF, auxVal);
                }
                return;
            }

            // Self-only heals (Second Wind) always land on the caster.
            if (effect == CardDatabase.EFFECT_SELFHEAL) { target = s; aoe = false; }
            int count = CollectTargets(aoe, target < 0 ? s : target, mySide);
            // A healing charm on the caster boosts this cast's heals (used once per cast).
            int healMul = effect == CardDatabase.EFFECT_HEAL || effect == CardDatabase.EFFECT_SELFHEAL ? UseHealBlades(s) : 100;
            for (int k = 0; k < count; k++)
            {
                int t = _targets[k];
                if (effect == CardDatabase.EFFECT_HEAL || effect == CardDatabase.EFFECT_SELFHEAL)
                {
                    // Heals can critical too (never blocked).
                    _nextFlags = 0;
                    int amount = power * healMul / 100;
                    // A school bubble boosts that school's heals too (Life's Verdant Canopy).
                    if (bubbleRounds > 0 && bubbleSchool >= 0 && cardDatabase.GetSchool(cardId) == bubbleSchool) amount = amount * (100 + bubblePct) / 100;
                    if (critPct[s] > 0 && Random.Range(0, 100) < critPct[s]) { amount = amount * critMultiplier / 100; _nextFlags = FLAG_CRIT; }
                    int heal = Mathf.Min(amount, maxHp[t] - hp[t]);
                    hp[t] += heal;
                    AddAct(group, s, t, cardId, KIND_HEAL, heal);
                }
                else
                {
                    if (effect == CardDatabase.EFFECT_WARD) AddFx(shields, t, PackFx(cardId, cardDatabase.ShieldSchool(cardId), power));
                    else if (effect == CardDatabase.EFFECT_MULTIWARD)
                    {
                        for (int j = 0; j < Mathf.Clamp(auxVal, 1, 3); j++) AddFx(shields, t, PackFx(cardId, cardDatabase.ShieldSchool(cardId), power));
                    }
                    else if (effect == CardDatabase.EFFECT_TRISHIELD)
                    {
                        int first = cardDatabase.TriBladeFirstSchool(cardId);
                        for (int j = 0; j < 3; j++) AddFx(shields, t, PackFx(cardId, first + j, power));
                    }
                    else if (effect == CardDatabase.EFFECT_EMPOWER) AddFx(blades, t, PackFx(cardId, cardDatabase.BladeSchool(cardId), power));
                    else if (effect == CardDatabase.EFFECT_HEALBLADE) AddFx(blades, t, PackFx(cardId, CardDatabase.HEAL_SCHOOL, power));
                    else if (effect == CardDatabase.EFFECT_TRIBLADE)
                    {
                        int first = cardDatabase.TriBladeFirstSchool(cardId);
                        for (int j = 0; j < 3; j++) AddFx(blades, t, PackFx(cardId, first + j, power));
                    }
                    else focus[t] = Mathf.Min(100, focus[t] + power);
                    AddAct(group, s, t, cardId, KIND_BUFF, power);
                }
            }
        }

        /// <summary>Adds an entry to the slot's list; when it's full the oldest entry is dropped.</summary>
        private void AddFx(int[] arr, int s, int packed)
        {
            int b = s * FX_MAX;
            int n = FxCount(arr, s);
            if (n >= FX_MAX)
            {
                for (int k = 0; k < FX_MAX - 1; k++) arr[b + k] = arr[b + k + 1];
                n = FX_MAX - 1;
            }
            arr[b + n] = packed;
        }

        /// <summary>
        /// Uses every entry on slot s that applies to spellSchool, at most one per source spell (two copies of the same
        /// shield don't stack, two different shields do). Returns the combined damage multiplier in percent:
        /// boosts (blades, traps) multiply by (100 + pct), shields by (100 - pct).
        /// </summary>
        private int UseFx(int[] arr, int kind, int s, int spellSchool, bool boost)
        {
            int b = s * FX_MAX;
            int mul = 100;
            int used = 0;
            for (int k = 0; k < FX_MAX; k++)
            {
                int v = arr[b + k];
                if (v == 0) continue;
                int f = FxSchool(v);
                if (f != CardDatabase.ANY_SCHOOL && f != spellSchool) continue;
                // Healing charms (blade list) only ever boost heals (UseHealBlades).
                if (f == CardDatabase.HEAL_SCHOOL) continue;
                int src = FxCard(v);
                bool dup = false;
                for (int u = 0; u < used; u++) if (_usedSrc[u] == src) dup = true;
                if (dup) continue;
                _usedSrc[used++] = src;
                mul = boost ? mul * (100 + FxPct(v)) / 100 : mul * Mathf.Max(0, 100 - FxPct(v)) / 100;
                PendFx(s, kind, v);
                arr[b + k] = 0;
            }
            int w = 0;
            for (int k = 0; k < FX_MAX; k++)
            {
                int v = arr[b + k];
                arr[b + k] = 0;
                if (v != 0) arr[b + w++] = v;
            }
            return mul;
        }

        /// <summary>Uses the caster's healing charms (one per source spell); returns the heal multiplier in percent.</summary>
        private int UseHealBlades(int s)
        {
            int b = s * FX_MAX;
            int mul = 100;
            int used = 0;
            for (int k = 0; k < FX_MAX; k++)
            {
                int v = blades[b + k];
                if (v == 0 || FxSchool(v) != CardDatabase.HEAL_SCHOOL) continue;
                int src = FxCard(v);
                bool dup = false;
                for (int u = 0; u < used; u++) if (_usedSrc[u] == src) dup = true;
                if (dup) continue;
                _usedSrc[used++] = src;
                mul = mul * (100 + FxPct(v)) / 100;
                PendFx(s, FX_BLADE, v);
                blades[b + k] = 0;
            }
            int w = 0;
            for (int k = 0; k < FX_MAX; k++)
            {
                int v = blades[b + k];
                blades[b + k] = 0;
                if (v != 0) blades[b + w++] = v;
            }
            return mul;
        }

        private int CollectTargets(bool aoe, int target, int side)
        {
            if (!aoe)
            {
                int t = ValidTarget(target, side);
                if (t < 0) return 0;
                _targets[0] = t;
                return 1;
            }
            int n = 0;
            for (int i = 0; i < SIDE; i++) if (Targetable(side * SIDE + i)) _targets[n++] = side * SIDE + i;
            return n;
        }

        private void AddAct(int group, int actor, int target, int cardId, int kind, int value)
        {
            if (actCount >= MAX_ACTIONS) { _nextFlags = 0; _pendFx = 0; return; }
            actGroup[actCount] = group;
            actActor[actCount] = actor;
            actTarget[actCount] = target;
            actCard[actCount] = cardId;
            actKind[actCount] = kind;
            actValue[actCount] = value;
            actHpAfter[actCount] = target >= 0 ? hp[target] : 0;
            actFlags[actCount] = _nextFlags;
            _nextFlags = 0;
            for (int i = 0; i < _pendFx && fxLogCount < MAX_FX_LOG; i++)
            {
                fxLogAct[fxLogCount] = actCount;
                fxLogSlot[fxLogCount] = _pendSlot[i];
                fxLogKind[fxLogCount] = _pendKind[i];
                fxLogValue[fxLogCount] = _pendValue[i];
                fxLogCount++;
            }
            _pendFx = 0;
            actCount++;
        }
    }
}
