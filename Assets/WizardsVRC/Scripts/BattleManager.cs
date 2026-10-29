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
    /// renders it. Slots 0-3 are wizards, 4-7 monsters (4 = boss).
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

        public CardDatabase cardDatabase;
        public BattleClient client;

        [Header("Timing (seconds)")]
        public int queueSeconds = 15;
        public int joinSeconds = 8;
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
        [Tooltip("Orb flight + impact after the wind-up, before the next caster.")]
        public float impactSeconds = 1.0f;

        [Header("Encounter")]
        [Tooltip("Monster ids (CardDatabase monster table) in this fight; roster[0] takes the boss seat.")]
        public int[] roster = { 1, 2, 2, 2 };
        [Tooltip("Spawn roster[0] plus one more monster per wizard (the Training Yard boss fight) instead of the whole roster.")]
        public bool scaleRosterWithParty = true;
        [Tooltip("Wandering-monster lane: fights start when a wizard touches a monster and the party sits down at once.")]
        public bool wanderMode;
        [Tooltip("Seconds after a fight before the lane's wandering monsters come back.")]
        public int respawnSeconds = 20;
        [Tooltip("Even fight (for new players): the fight starts with only the monster that was touched. Each extra wizard " +
                 "lets one more patrol monster join when it reaches the ring; other patrol monsters that reach the ring fade " +
                 "away. With no patrol monster left to come, the next roster monster joins at once.")]
        public bool evenFight;
        [Tooltip("Monster id of each patrol monster (index = Wanderer.wandererIndex).")]
        public int[] wandererMonsters = new int[0];

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
        [Tooltip("Zone encounters: places the duel ring may appear. A fight forms at the spot nearest the wizard who " +
                 "touched the monster (Wizard101 style). Empty = the ring stays where it was built.")]
        public Transform[] ringSpots;
        /// <summary>Index into ringSpots of the current fight's ring.</summary>
        [UdonSynced] public int ringSpot;

        /// <summary>Server time when the last fight ended (wanderers respawn respawnSeconds later).</summary>
        [UdonSynced] public int lastEndMs;

        [UdonSynced] public int phase;
        [UdonSynced] public int phaseEndMs;
        [UdonSynced] public int battleId;
        [UdonSynced] public int round;
        [UdonSynced] public int firstSide;
        [UdonSynced] public int[] queueIds = new int[SIDE];

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
        /// <summary>Rounds left in which monsters must attack this slot.</summary>
        [UdonSynced] public int[] taunt = new int[SLOTS];

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
        private bool[] _joined = new bool[SIDE];
        private int[] _pickCard = new int[SLOTS];
        private int[] _pickTarget = new int[SLOTS];
        private int[] _targets = new int[SIDE];
        private int[] _usedSrc = new int[FX_MAX];
        private int _startWanderer = -1;
        /// <summary>Round in which a monster joined mid-fight (it sits that round out); -1 = from the start.</summary>
        private int[] _joinRound = new int[SLOTS];
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
            float p = school >= 0 && castPeakSeconds != null && school < castPeakSeconds.Length ? castPeakSeconds[school] : len * 0.6f;
            return Mathf.Clamp(p, 0f, len);
        }

        /// <summary>Playback time for the cast starting at act index i (same on every client).</summary>
        public float StepSeconds(int i)
        {
            if (!IsCastAct(i)) return actionSeconds;
            return CastSeconds(cardDatabase.GetSchool(actCard[i])) + impactSeconds;
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

        public int FindWizardSlot(int playerId)
        {
            if (playerId <= 0) return -1;
            for (int s = 0; s < SIDE; s++) if (slotIds[s] == playerId) return s;
            return -1;
        }

        public bool IsQueued(int playerId)
        {
            for (int i = 0; i < SIDE; i++) if (queueIds[i] == playerId && playerId > 0) return true;
            return false;
        }

        public int QueueCount()
        {
            int n = 0;
            for (int i = 0; i < SIDE; i++) if (queueIds[i] != 0) n++;
            return n;
        }

        /// <summary>Monster id in a monster seat (0 for empty seats and summons).</summary>
        public int MonsterAt(int s) { return s >= SIDE && s < SLOTS && slotIds[s] > 0 ? slotIds[s] : 0; }

        /// <summary>True while the lane's wandering monsters should be hidden: a fight is on, or one just ended.</summary>
        public bool WanderersHidden()
        {
            return phase != PHASE_IDLE || (lastEndMs != 0 && Now() - lastEndMs < respawnSeconds * 1000);
        }

        /// <summary>
        /// Whether patrol monster w is hidden. Normal encounters hide every patrol monster during a fight. Even fights only
        /// hide the ones in the fight or faded away (the rest keep walking), until they respawn after the fight.
        /// </summary>
        public bool WandererHidden(int w)
        {
            if (!evenFight) return WanderersHidden();
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
        public bool IsCreature(int s) { return Occupied(s) && (s >= SIDE || IsSummon(s)); }

        /// <summary>"Weak to Ice, resists Fire" for a creature, or "".</summary>
        public string MatchupText(int s)
        {
            if (!IsCreature(s) || cardDatabase == null) return "";
            int sc = school[s];
            int opp = OppositeSchool(sc);
            if (sc < 0) return "";
            if (opp < 0) return "Resists " + cardDatabase.SchoolName(sc);
            return "Weak to " + cardDatabase.SchoolName(opp) + ", resists " + cardDatabase.SchoolName(sc);
        }

        public string SlotName(int s)
        {
            if (!Occupied(s)) return "";
            if (slotIds[s] < 0) return cardDatabase.GetName(-slotIds[s]);
            if (s >= SIDE) return cardDatabase != null ? cardDatabase.MonsterName(slotIds[s]) : "Monster";
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
            if (phase >= PHASE_JOINING && phase <= PHASE_PLAYBACK && !PlayersAlive()) EndFight(PHASE_DEFEAT);
            Publish();
        }

        /// <summary>Frees a wizard seat (fled, left or defeated). Health stays as it was so the client can tell why.</summary>
        private void VacateSeat(int s)
        {
            slotIds[s] = 0;
            locked[s] = 0;
            pips[s] = 0; powerPips[s] = 0;
            focus[s] = 0; dotLeft[s] = 0; dotTurns[s] = 0; stunned[s] = 0; taunt[s] = 0;
            ClearFx(shields, s); ClearFx(blades, s); ClearFx(traps, s);
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
            school[s] = 0; level[s] = 1;
            dmgPct[s] = 0; resistPct[s] = 0; accPct[s] = 0; focus[s] = 0; locked[s] = 0;
            dotLeft[s] = 0; dotTurns[s] = 0; stunned[s] = 0; taunt[s] = 0;
            ClearFx(shields, s); ClearFx(blades, s); ClearFx(traps, s);
            if (s < SIDE) _joined[s] = id < 0;
        }

        public void RequestPick(int cardId, int target)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetPick), local.playerId, cardId, target);
        }

        [NetworkCallable]
        public void NetQueueEnter(int playerId)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (phase != PHASE_IDLE && phase != PHASE_QUEUE) return;
            if (IsQueued(playerId)) return;
            for (int i = 0; i < SIDE; i++)
            {
                if (queueIds[i] != 0) continue;
                queueIds[i] = playerId;
                if (phase == PHASE_IDLE)
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

        /// <summary>Even fights: patrol monster w reached the circle during the fight (sent by the fighting wizards' clients).</summary>
        public void RequestWandererContact(int wanderer)
        {
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetWandererContact), wanderer);
        }

        [NetworkCallable]
        public void NetWandererContact(int w)
        {
            if (!Networking.IsOwner(gameObject) || !evenFight || w < 0 || w >= 31 || w >= wandererMonsters.Length) return;
            if (phase < PHASE_JOINING || phase > PHASE_PLAYBACK || (wandererGone & (1 << w)) != 0) return;
            // It either joins (a wizard is still unmatched) or fades away; either way it's gone until the respawn.
            wandererGone |= 1 << w;
            if (EvenMonsterCount() < EvenWizardCount()) AddMonster(wandererMonsters[w]);
            Publish();
        }

        [NetworkCallable]
        public void NetEngageWander(int playerId, int wanderer)
        {
            if (!Networking.IsOwner(gameObject) || !wanderMode || playerId <= 0) return;
            if (phase == PHASE_IDLE)
            {
                if (evenFight ? WandererHidden(wanderer) : WanderersHidden()) return;
                for (int i = 0; i < SIDE; i++) queueIds[i] = 0;
                queueIds[0] = playerId;
                ringSpot = NearestRingSpot(playerId);
                // Even fights start with just the monster that was touched.
                _startWanderer = wanderer;
                StartBattle();
                if (phase != PHASE_ROAMING) return;
                phase = PHASE_JOINING;
                phaseEndMs = Now() + joinSeconds * 1000;
                Publish();
                return;
            }
            // Like Wizard101, anyone can walk into a circle with a free seat, even mid-fight; they act from the next round.
            if (phase < PHASE_JOINING || phase > PHASE_PLAYBACK || FindWizardSlot(playerId) >= 0) return;
            for (int s = 0; s < SIDE; s++)
            {
                if (!SeatFree(s)) continue;
                ResetSeat(s, playerId);
                if (phase == PHASE_JOINING) phaseEndMs = Mathf.Max(phaseEndMs, Now() + 4000);
                else locked[s] = 1;
                if (evenFight)
                {
                    // The patrol monster they touched comes with them; otherwise one joins when it reaches the circle.
                    if (wanderer >= 0 && wanderer < 31 && wanderer < wandererMonsters.Length && (wandererGone & (1 << wanderer)) == 0)
                    {
                        wandererGone |= 1 << wanderer;
                        if (EvenMonsterCount() < EvenWizardCount()) AddMonster(wandererMonsters[wanderer]);
                    }
                    if (EvenMonsterCount() < EvenWizardCount() && !AnyWandererLeft()) AddMonster(NextRosterMonster());
                }
                Publish();
                return;
            }
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

        /// <summary>The ring spot closest to the player (0 when there are no spots or the player is unknown).</summary>
        public int NearestRingSpot(int playerId)
        {
            if (ringSpots == null || ringSpots.Length == 0) return 0;
            VRCPlayerApi p = VRCPlayerApi.GetPlayerById(playerId);
            if (!Utilities.IsValid(p)) return 0;
            Vector3 pos = p.GetPosition();
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < ringSpots.Length; i++)
            {
                if (ringSpots[i] == null) continue;
                float d = (ringSpots[i].position - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
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
            phase = PHASE_JOINING;
            phaseEndMs = Now() + joinSeconds * 1000;
            Publish();
        }

        [NetworkCallable]
        public void NetQueueLeave(int playerId)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_QUEUE) return;
            for (int i = 0; i < SIDE; i++) if (queueIds[i] == playerId) queueIds[i] = 0;
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
            level[s] = playerLevel;
            dmgPct[s] = Mathf.Clamp(Unpack(statsA, 0), 0, 200);
            resistPct[s] = Mathf.Clamp(Unpack(statsA, 1), 0, 80);
            accPct[s] = Mathf.Clamp(Unpack(statsA, 2) - 128, -50, 100);
            critPct[s] = Mathf.Clamp(Unpack(statsB, 0), 0, 100);
            blockPct[s] = Mathf.Clamp(Unpack(statsB, 1), 0, 100);
            piercePct[s] = Mathf.Clamp(Unpack(statsB, 2), 0, 100);
            powerPipPct[s] = Mathf.Clamp(Unpack(statsB, 3) + (playerLevel >= powerPipLevel ? basePowerPipChance : 0), 0, 100);
            _joined[s] = true;
            Publish();
        }

        [NetworkCallable]
        public void NetPick(int playerId, int cardId, int target)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_PLANNING) return;
            int s = FindWizardSlot(playerId);
            if (!Alive(s) || locked[s] != 0) return;
            if (cardId != 0 && cardId != PICK_POTION && (cardDatabase.IndexOf(cardId) < 0 || !CanAfford(s, cardId))) cardId = 0;
            _pickCard[s] = cardId;
            _pickTarget[s] = target;
            locked[s] = 1;
            Publish();
        }

        // ------------------------------------------------------------------ owner loop

        private void Update()
        {
            if (!Networking.IsOwner(gameObject) || phase == PHASE_IDLE) return;
            bool timeUp = phaseEndMs - Now() <= 0;
            if (phase == PHASE_QUEUE)
            {
                if (timeUp) StartBattle();
            }
            else if (phase == PHASE_JOINING)
            {
                if (timeUp || AllJoined()) BeginRound();
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
            for (int i = 0; i < SIDE; i++)
            {
                if (queueIds[i] == id) { queueIds[i] = 0; changed = true; }
                if (slotIds[i] == id) { VacateSeat(i); hp[i] = 0; changed = true; }
            }
            if (phase == PHASE_QUEUE && QueueCount() == 0) phase = PHASE_IDLE;
            if (changed && phase >= PHASE_JOINING && phase <= PHASE_PLAYBACK && !PlayersAlive()) EndFight(PHASE_DEFEAT);
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
            for (int s = 0; s < SIDE; s++) if (Occupied(s) && !_joined[s]) return false;
            return true;
        }

        private bool AllLocked()
        {
            for (int s = 0; s < SIDE; s++) if (Alive(s) && !IsSummon(s) && locked[s] == 0) return false;
            return true;
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
            int n = 0;
            for (int s = 0; s < SLOTS; s++)
            {
                ResetSeat(s, 0);
                hp[s] = 0; maxHp[s] = 0;
            }
            for (int i = 0; i < SIDE; i++)
            {
                if (queueIds[i] == 0) continue;
                ResetSeat(n, queueIds[i]);
                n++;
                queueIds[i] = 0;
            }
            if (n == 0) { phase = PHASE_IDLE; Publish(); return; }

            for (int s = 0; s < SLOTS; s++) _joinRound[s] = -1;
            // Patrol monsters from the last fight that haven't respawned yet stay gone.
            if (!(lastEndMs != 0 && Now() - lastEndMs < respawnSeconds * 1000)) wandererGone = 0;
            int count = roster == null ? 0 : Mathf.Min(roster.Length, SIDE);
            if (scaleRosterWithParty) count = Mathf.Min(count, 1 + n);
            if (evenFight && count > 0)
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
            round = 0;
            actCount = 0;
            fxLogCount = 0;
            _pendFx = 0;
            firstSide = Random.Range(0, 2);
            phase = PHASE_ROAMING;
            phaseEndMs = Now() + roamSeconds * 1000;
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
            for (int s = SIDE; s < SLOTS; s++)
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
                else if (e == CardDatabase.EFFECT_EMPOWER || e == CardDatabase.EFFECT_WARD || e == CardDatabase.EFFECT_TRIBLADE)
                {
                    int[] arr = e == CardDatabase.EFFECT_WARD ? shields : blades;
                    if (FxCount(arr, s) == 0) buff = spell;
                }
                else if (!cardDatabase.TargetsAllies(spell) && (attack == 0 || cardDatabase.GetPipCost(spell) > cardDatabase.GetPipCost(attack))) attack = spell;
            }

            int hurt = -1;
            for (int i = 0; i < SIDE; i++)
            {
                int a = mySide * SIDE + i;
                if (Alive(a) && hp[a] * 100 < maxHp[a] * 45 && (hurt < 0 || hp[a] * 1000 / maxHp[a] < hp[hurt] * 1000 / maxHp[hurt])) hurt = a;
            }
            if (heal != 0 && hurt >= 0) { _pickCard[s] = heal; _pickTarget[s] = hurt; return; }
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
                if (Alive(t) && taunt[t] > 0) return t;
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

        private int RandomAlive(int side)
        {
            int count = 0;
            for (int i = 0; i < SIDE; i++) if (Alive(side * SIDE + i)) count++;
            if (count == 0) return -1;
            int k = Random.Range(0, count);
            for (int i = 0; i < SIDE; i++)
            {
                int s = side * SIDE + i;
                if (!Alive(s)) continue;
                if (k == 0) return s;
                k--;
            }
            return -1;
        }

        private int ValidTarget(int target, int side)
        {
            if (Alive(target) && SideOf(target) == side) return target;
            for (int i = 0; i < SIDE; i++) if (Alive(side * SIDE + i)) return side * SIDE + i;
            return -1;
        }

        private void Cast(int s, int cardId, int target, int group)
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

            int accuracy = cardDatabase.GetAccuracy(cardId) + accPct[s] + focus[s];
            focus[s] = 0;
            if (Random.Range(0, 100) >= accuracy)
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
            if (effect == CardDatabase.EFFECT_DAMAGE || effect == CardDatabase.EFFECT_DRAIN || effect == CardDatabase.EFFECT_DOT ||
                effect == CardDatabase.EFFECT_STUN || effect == CardDatabase.EFFECT_SHATTER)
            {
                int n = CollectTargets(aoe, target, 1 - mySide);
                int spellSchool = cardDatabase.GetSchool(cardId);
                int pierce = effect == CardDatabase.EFFECT_DAMAGE || effect == CardDatabase.EFFECT_STUN ? Mathf.Clamp(auxVal, 0, 100) : 0;
                // Blades are used once per cast (even when it hits every enemy); traps and shields once per target hit.
                int bladeMul = power > 0 ? UseFx(blades, FX_BLADE, s, spellSchool, true) : 100;
                int total = 0;
                for (int k = 0; k < n; k++)
                {
                    int t = _targets[k];
                    if (effect == CardDatabase.EFFECT_SHATTER && power > 0) BreakFx(shields, FX_SHIELD, t);
                    else if (effect == CardDatabase.EFFECT_SHATTER) ClearFx(shields, t);
                    if (power > 0)
                    {
                        int amount = Hit(s, t, cardDatabase.RollPower(cardId), spellSchool, bladeMul, pierce);
                        total += amount;
                        AddAct(group, s, t, cardId, KIND_DAMAGE, amount);
                    }
                    if (effect == CardDatabase.EFFECT_DOT && hp[t] > 0)
                    {
                        dotLeft[t] += auxVal;
                        dotTurns[t] = CardDatabase.DOT_ROUNDS;
                        AddAct(group, s, t, cardId, KIND_BUFF, auxVal);
                    }
                    if (effect == CardDatabase.EFFECT_STUN && hp[t] > 0) stunned[t] = 1;
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

            if (effect == CardDatabase.EFFECT_TRAP)
            {
                int tn = CollectTargets(aoe, target, 1 - mySide);
                for (int k = 0; k < tn; k++)
                {
                    AddFx(traps, _targets[k], PackFx(cardId, cardDatabase.TrapSchool(cardId), power));
                    AddAct(group, s, _targets[k], cardId, KIND_BUFF, power);
                }
                return;
            }

            int count = CollectTargets(aoe, target < 0 ? s : target, mySide);
            for (int k = 0; k < count; k++)
            {
                int t = _targets[k];
                if (effect == CardDatabase.EFFECT_HEAL)
                {
                    // Heals can critical too (never blocked).
                    _nextFlags = 0;
                    int amount = power;
                    if (critPct[s] > 0 && Random.Range(0, 100) < critPct[s]) { amount = amount * critMultiplier / 100; _nextFlags = FLAG_CRIT; }
                    int heal = Mathf.Min(amount, maxHp[t] - hp[t]);
                    hp[t] += heal;
                    AddAct(group, s, t, cardId, KIND_HEAL, heal);
                }
                else
                {
                    if (effect == CardDatabase.EFFECT_WARD) AddFx(shields, t, PackFx(cardId, cardDatabase.ShieldSchool(cardId), power));
                    else if (effect == CardDatabase.EFFECT_EMPOWER) AddFx(blades, t, PackFx(cardId, cardDatabase.BladeSchool(cardId), power));
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
            for (int i = 0; i < SIDE; i++) if (Alive(side * SIDE + i)) _targets[n++] = side * SIDE + i;
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
