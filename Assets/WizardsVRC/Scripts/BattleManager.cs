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
        [UdonSynced] public int[] pips = new int[SLOTS];
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

        // owner-only scratch
        private bool[] _joined = new bool[SIDE];
        private int[] _pickCard = new int[SLOTS];
        private int[] _pickTarget = new int[SLOTS];
        private int[] _targets = new int[SIDE];
        private int[] _usedSrc = new int[FX_MAX];

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

        public void RequestJoin(int maxHealth, int playerSchool, int playerLevel, int dmg, int resist, int acc)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetJoin), local.playerId, maxHealth, playerSchool, playerLevel, dmg, resist, acc);
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

        /// <summary>Wandering-monster lane: a wizard touched a monster (starts the fight) or walked up during the join window.</summary>
        public void RequestEngageWander()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetEngageWander), local.playerId);
        }

        [NetworkCallable]
        public void NetEngageWander(int playerId)
        {
            if (!Networking.IsOwner(gameObject) || !wanderMode || playerId <= 0) return;
            if (phase == PHASE_IDLE)
            {
                if (WanderersHidden()) return;
                for (int i = 0; i < SIDE; i++) queueIds[i] = 0;
                queueIds[0] = playerId;
                StartBattle();
                if (phase != PHASE_ROAMING) return;
                phase = PHASE_JOINING;
                phaseEndMs = Now() + joinSeconds * 1000;
                Publish();
                return;
            }
            if (phase != PHASE_JOINING || FindWizardSlot(playerId) >= 0) return;
            for (int s = 0; s < SIDE; s++)
            {
                if (Occupied(s)) continue;
                slotIds[s] = playerId;
                maxHp[s] = 400; hp[s] = 400;
                _joined[s] = false;
                phaseEndMs = Mathf.Max(phaseEndMs, Now() + 4000);
                Publish();
                return;
            }
        }

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
        public void NetJoin(int playerId, int maxHealth, int playerSchool, int playerLevel, int dmg, int resist, int acc)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_JOINING) return;
            int s = FindWizardSlot(playerId);
            if (s < 0) return;
            maxHp[s] = Mathf.Clamp(maxHealth, 1, 99999);
            hp[s] = maxHp[s];
            school[s] = playerSchool;
            level[s] = playerLevel;
            dmgPct[s] = Mathf.Clamp(dmg, 0, 200);
            resistPct[s] = Mathf.Clamp(resist, 0, 80);
            accPct[s] = Mathf.Clamp(acc, -50, 100);
            _joined[s] = true;
            Publish();
        }

        [NetworkCallable]
        public void NetPick(int playerId, int cardId, int target)
        {
            if (!Networking.IsOwner(gameObject) || phase != PHASE_PLANNING) return;
            int s = FindWizardSlot(playerId);
            if (!Alive(s) || locked[s] != 0) return;
            if (cardId != 0 && (cardDatabase.IndexOf(cardId) < 0 || cardDatabase.GetPipCost(cardId) > pips[s])) cardId = 0;
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
                if (slotIds[i] == id) { slotIds[i] = 0; hp[i] = 0; changed = true; }
            }
            if (phase == PHASE_QUEUE && QueueCount() == 0) phase = PHASE_IDLE;
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
                slotIds[s] = 0; hp[s] = 0; maxHp[s] = 0; pips[s] = 0; school[s] = 0; level[s] = 1;
                dmgPct[s] = 0; resistPct[s] = 0; accPct[s] = 0; focus[s] = 0; locked[s] = 0;
                dotLeft[s] = 0; dotTurns[s] = 0; stunned[s] = 0; taunt[s] = 0;
            }
            for (int k = 0; k < SLOTS * FX_MAX; k++) { shields[k] = 0; blades[k] = 0; traps[k] = 0; }
            for (int i = 0; i < SIDE; i++)
            {
                if (queueIds[i] == 0) continue;
                slotIds[n] = queueIds[i];
                maxHp[n] = 400; hp[n] = 400;
                _joined[n] = false;
                n++;
                queueIds[i] = 0;
            }
            if (n == 0) { phase = PHASE_IDLE; Publish(); return; }

            int count = roster == null ? 0 : Mathf.Min(roster.Length, SIDE);
            if (scaleRosterWithParty) count = Mathf.Min(count, 1 + n);
            for (int m = 0; m < count; m++)
            {
                int id = roster[m];
                int health = cardDatabase.MonsterHp(id) + (cardDatabase.IsBossMonster(id) ? cardDatabase.MonsterHpPerExtraPlayer(id) * (n - 1) : 0);
                SetMonster(BOSS_SLOT + m, id, cardDatabase.MonsterSchool(id), cardDatabase.MonsterLevel(id), health);
            }
            if (count == 0) { phase = PHASE_IDLE; Publish(); return; }

            battleId++;
            round = 0;
            actCount = 0;
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
                int gain = level[s] >= 10 && Random.value < 0.4f ? 2 : 1;
                pips[s] = Mathf.Min(MAX_PIPS, pips[s] + gain);
            }
            phase = PHASE_PLANNING;
            phaseEndMs = Now() + planningSeconds * 1000;
            Publish();
        }

        private void AfterPlayback()
        {
            if (!SideAlive(1)) phase = PHASE_VICTORY;
            else if (!PlayersAlive()) phase = PHASE_DEFEAT;
            else { BeginRound(); return; }
            phaseEndMs = Now() + endSeconds * 1000;
            Publish();
        }

        // ------------------------------------------------------------------ resolution

        private void Resolve()
        {
            for (int s = SIDE; s < SLOTS; s++) if (Alive(s) && !IsSummon(s)) PickForMonster(s);
            for (int s = 0; s < SLOTS; s++) if (taunt[s] > 0) taunt[s]--;

            actCount = 0;
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

        private void PickForMonster(int s)
        {
            int monster = slotIds[s];
            int best = 0;
            int affordable = 0;
            for (int i = 0; i < CardDatabase.MONSTER_SPELLS; i++)
            {
                int spell = cardDatabase.MonsterSpell(monster, i);
                if (spell == 0 || cardDatabase.GetPipCost(spell) > pips[s]) continue;
                affordable++;
                // Prefer the strongest affordable spell, but sometimes hold pips for a bigger one later.
                if (best == 0 || cardDatabase.GetPipCost(spell) > cardDatabase.GetPipCost(best)) best = spell;
            }
            if (affordable > 0 && Random.value < 0.25f && pips[s] < MAX_PIPS) best = 0;
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

        /// <summary>One hit from slot s (power already rolled): damage bonus, blade multiplier, the target's traps/shields and resist.
        /// pierce% of the shield block and resist is ignored.</summary>
        private int Hit(int s, int t, int power, int spellSchool, int bladeMul, int pierce)
        {
            int amount = power * (100 + dmgPct[s]) / 100;
            amount = amount * bladeMul / 100;
            amount = amount * UseFx(traps, t, spellSchool, true) / 100;
            int shieldMul = UseFx(shields, t, spellSchool, false);
            shieldMul = 100 - (100 - shieldMul) * (100 - pierce) / 100;
            amount = amount * shieldMul / 100;
            amount = amount * (100 - resistPct[t] * (100 - pierce) / 100) / 100;
            if (amount < 1) amount = 1;
            hp[t] = Mathf.Max(0, hp[t] - amount);
            return amount;
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
            slotIds[seat] = -cardId;
            school[seat] = cardDatabase.GetSchool(cardId);
            level[seat] = level[s];
            maxHp[seat] = power;
            hp[seat] = power;
            pips[seat] = 0;
            dmgPct[seat] = 0; resistPct[seat] = 0; accPct[seat] = 0; focus[seat] = 0; locked[seat] = 1;
            dotLeft[seat] = 0; dotTurns[seat] = 0; stunned[seat] = 0; taunt[seat] = 0;
            ClearFx(shields, seat); ClearFx(blades, seat); ClearFx(traps, seat);
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
            int cost = cardDatabase.GetPipCost(cardId);
            if (cardId == 0 || cardDatabase.IndexOf(cardId) < 0 || cost > pips[s])
            {
                AddAct(group, s, s, 0, KIND_PASS, 0);
                return;
            }
            pips[s] -= cost;

            int accuracy = cardDatabase.GetAccuracy(cardId) + accPct[s] + focus[s];
            focus[s] = 0;
            if (Random.Range(0, 100) >= accuracy)
            {
                AddAct(group, s, s, cardId, KIND_FIZZLE, 0);
                return;
            }

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
                int pierce = effect == CardDatabase.EFFECT_DAMAGE ? Mathf.Clamp(auxVal, 0, 100) : 0;
                // Blades are used once per cast (even when it hits every enemy); traps and shields once per target hit.
                int bladeMul = power > 0 ? UseFx(blades, s, spellSchool, true) : 100;
                int total = 0;
                for (int k = 0; k < n; k++)
                {
                    int t = _targets[k];
                    if (effect == CardDatabase.EFFECT_SHATTER) ClearFx(shields, t);
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
                        ClearFx(shields, t);
                        ClearFx(blades, t);
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
                    int heal = Mathf.Min(power, maxHp[t] - hp[t]);
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
        private int UseFx(int[] arr, int s, int spellSchool, bool boost)
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
            if (actCount >= MAX_ACTIONS) return;
            actGroup[actCount] = group;
            actActor[actCount] = actor;
            actTarget[actCount] = target;
            actCard[actCount] = cardId;
            actKind[actCount] = kind;
            actValue[actCount] = value;
            actHpAfter[actCount] = target >= 0 ? hp[target] : 0;
            actCount++;
        }
    }
}
