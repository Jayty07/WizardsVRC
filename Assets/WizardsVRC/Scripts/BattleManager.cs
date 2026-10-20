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
        public const int PHASE_JOINING = 2;
        public const int PHASE_PLANNING = 3;
        public const int PHASE_PLAYBACK = 4;
        public const int PHASE_VICTORY = 5;
        public const int PHASE_DEFEAT = 6;

        public const int SIDE = 4;
        public const int SLOTS = 8;
        public const int BOSS_SLOT = 4;
        public const int MAX_ACTIONS = 40;
        public const int MAX_PIPS = 7;

        public const int KIND_PASS = 0;
        public const int KIND_FIZZLE = 1;
        public const int KIND_DAMAGE = 2;
        public const int KIND_HEAL = 3;
        public const int KIND_BUFF = 4;

        public CardDatabase cardDatabase;
        public BattleClient client;

        [Header("Timing (seconds)")]
        public int queueSeconds = 15;
        public int joinSeconds = 8;
        public int planningSeconds = 30;
        public int endSeconds = 8;
        public float actionSeconds = 1.6f;

        [Header("Test boss encounter (placeholder monsters)")]
        public string bossName = "Gloomfang the Warden";
        public int bossSchool = 4;
        public int bossLevel = 5;
        public int bossHealth = 900;
        public int bossHealthPerExtraPlayer = 450;
        public int[] bossSpells = { 140, 141, 142, 143 };
        public string minionName = "Bone Thrall";
        public int minionSchool = 5;
        public int minionLevel = 3;
        public int minionHealth = 320;
        public int[] minionSpells = { 150, 151 };

        [UdonSynced] public int phase;
        [UdonSynced] public int phaseEndMs;
        [UdonSynced] public int battleId;
        [UdonSynced] public int round;
        [UdonSynced] public int firstSide;
        [UdonSynced] public int[] queueIds = new int[SIDE];

        /// <summary>Wizard slots: player id. Monster slots: 1 = boss, 2 = minion. 0 = empty.</summary>
        [UdonSynced] public int[] slotIds = new int[SLOTS];
        [UdonSynced] public int[] hp = new int[SLOTS];
        [UdonSynced] public int[] maxHp = new int[SLOTS];
        [UdonSynced] public int[] pips = new int[SLOTS];
        [UdonSynced] public int[] school = new int[SLOTS];
        [UdonSynced] public int[] level = new int[SLOTS];
        [UdonSynced] public int[] dmgPct = new int[SLOTS];
        [UdonSynced] public int[] resistPct = new int[SLOTS];
        [UdonSynced] public int[] accPct = new int[SLOTS];
        [UdonSynced] public int[] charm = new int[SLOTS];
        [UdonSynced] public int[] ward = new int[SLOTS];
        [UdonSynced] public int[] focus = new int[SLOTS];
        [UdonSynced] public int[] locked = new int[SLOTS];

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

        // ------------------------------------------------------------------ helpers (all clients)

        public int Now() { return Networking.GetServerTimeInMilliseconds(); }

        public int SecondsLeft()
        {
            int ms = phaseEndMs - Now();
            return ms <= 0 ? 0 : (ms + 999) / 1000;
        }

        public bool Occupied(int s) { return s >= 0 && s < SLOTS && slotIds[s] != 0; }
        public bool Alive(int s) { return Occupied(s) && hp[s] > 0; }
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

        public int MonsterCount()
        {
            int n = 0;
            for (int s = SIDE; s < SLOTS; s++) if (Occupied(s)) n++;
            return n;
        }

        public string SlotName(int s)
        {
            if (!Occupied(s)) return "";
            if (s >= SIDE) return slotIds[s] == 1 ? bossName : minionName;
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
            accPct[s] = Mathf.Clamp(acc, 0, 100);
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
            for (int s = 0; s < SIDE; s++) if (Alive(s) && locked[s] == 0) return false;
            return true;
        }

        private bool SideAlive(int side)
        {
            for (int i = 0; i < SIDE; i++) if (Alive(side * SIDE + i)) return true;
            return false;
        }

        private void StartBattle()
        {
            int n = 0;
            for (int s = 0; s < SLOTS; s++)
            {
                slotIds[s] = 0; hp[s] = 0; maxHp[s] = 0; pips[s] = 0; school[s] = 0; level[s] = 1;
                dmgPct[s] = 0; resistPct[s] = 0; accPct[s] = 0; charm[s] = 0; ward[s] = 0; focus[s] = 0; locked[s] = 0;
            }
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

            SetMonster(BOSS_SLOT, 1, bossSchool, bossLevel, bossHealth + bossHealthPerExtraPlayer * (n - 1));
            int minions = Mathf.Min(n, SIDE - 1);
            for (int m = 0; m < minions; m++) SetMonster(BOSS_SLOT + 1 + m, 2, minionSchool, minionLevel, minionHealth);

            battleId++;
            round = 0;
            actCount = 0;
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
            else if (!SideAlive(0)) phase = PHASE_DEFEAT;
            else { BeginRound(); return; }
            phaseEndMs = Now() + endSeconds * 1000;
            Publish();
        }

        // ------------------------------------------------------------------ resolution

        private void Resolve()
        {
            for (int s = SIDE; s < SLOTS; s++) if (Alive(s)) PickForMonster(s);

            actCount = 0;
            int group = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int side = pass == 0 ? firstSide : 1 - firstSide;
                for (int i = 0; i < SIDE; i++)
                {
                    int s = side * SIDE + i;
                    if (!Alive(s)) continue;
                    if (!SideAlive(1 - side)) break;
                    Cast(s, _pickCard[s], _pickTarget[s], group);
                    group++;
                }
            }
            phase = PHASE_PLAYBACK;
            phaseEndMs = Now() + (int)(group * actionSeconds * 1000f) + 1200;
            Publish();
        }

        private void PickForMonster(int s)
        {
            int[] spells = slotIds[s] == 1 ? bossSpells : minionSpells;
            int best = 0;
            int affordable = 0;
            for (int i = 0; i < spells.Length; i++)
            {
                if (cardDatabase.GetPipCost(spells[i]) > pips[s]) continue;
                affordable++;
                // Prefer the strongest affordable spell, but sometimes hold pips for a bigger one later.
                if (best == 0 || cardDatabase.GetPipCost(spells[i]) > cardDatabase.GetPipCost(best)) best = spells[i];
            }
            if (affordable > 0 && Random.value < 0.25f && pips[s] < MAX_PIPS) best = 0;
            _pickCard[s] = best;
            _pickTarget[s] = RandomAlive(0);
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

            if (effect == CardDatabase.EFFECT_DAMAGE || effect == CardDatabase.EFFECT_DRAIN)
            {
                int enemySide = 1 - mySide;
                int n = CollectTargets(aoe, target, enemySide);
                int total = 0;
                for (int k = 0; k < n; k++)
                {
                    int t = _targets[k];
                    int amount = power * Random.Range(90, 111) / 100;
                    amount = amount * (100 + dmgPct[s] + charm[s]) / 100;
                    amount = amount * (100 - ward[t]) / 100;
                    amount = amount * (100 - resistPct[t]) / 100;
                    if (amount < 1) amount = 1;
                    ward[t] = 0;
                    hp[t] = Mathf.Max(0, hp[t] - amount);
                    total += amount;
                    AddAct(group, s, t, cardId, KIND_DAMAGE, amount);
                }
                charm[s] = 0;
                if (effect == CardDatabase.EFFECT_DRAIN && total > 0)
                {
                    int heal = Mathf.Min(total / 2, maxHp[s] - hp[s]);
                    hp[s] += heal;
                    AddAct(group, s, s, cardId, KIND_HEAL, heal);
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
                    if (effect == CardDatabase.EFFECT_WARD) ward[t] = Mathf.Min(90, power);
                    else if (effect == CardDatabase.EFFECT_EMPOWER) charm[t] = Mathf.Min(200, charm[t] + power);
                    else focus[t] = Mathf.Min(100, focus[t] + power);
                    AddAct(group, s, t, cardId, KIND_BUFF, power);
                }
            }
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
