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
        public Transform[] seats;
        public GameObject ring;
        public GameObject[] monsterBodies;
        public Text[] overheadLabels;
        public Text bannerLabel;
        public Text padLabel;
        public Transform orb;
        public Transform[] stagingSpots;
        public Transform arenaCenter;
        public float engageRadius = 11f;
        public Transform clockHand;
        public Text clockLabel;

        [Header("Hand panel")]
        public GameObject handRoot;
        public Image[] cardImages;
        public Text[] cardNames;
        public Text[] cardInfos;
        public Text statusLabel;
        public Text partyLabel;
        public GameObject[] targetButtons;
        public Text[] targetLabels;
        public Image discardImage;

        [Header("Rewards")]
        public int[] lootTable = { 10, 11, 20, 21, 22, 23, 24, 25, 26, 27, 28, 30, 31, 40, 41, 50, 51 };

        private bool _inBattle;
        private int _slot = -1;
        private bool _seated;
        private float _nextEngage;
        private int _clockActor = -1;
        private int _battleSeen;
        private int _roundSeen;
        private int _phaseSeen;

        private int[] _pile = new int[CharacterData.DECK_SIZE];
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
        private GameObject _spellAnim;

        private Color _cardDim = new Color(0.35f, 0.35f, 0.35f);

        // ------------------------------------------------------------------ uGUI events

        public void Card0() { ClickCard(0); }
        public void Card1() { ClickCard(1); }
        public void Card2() { ClickCard(2); }
        public void Card3() { ClickCard(3); }
        public void Card4() { ClickCard(4); }
        public void Card5() { ClickCard(5); }
        public void Card6() { ClickCard(6); }

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

        // ------------------------------------------------------------------ entry points

        public void LocalQueue(bool enter)
        {
            if (enter && (playerState == null || !playerState.HasCharacter())) return;
            if (manager != null) manager.RequestQueue(enter);
        }

        private void Start()
        {
            if (handRoot != null) handRoot.SetActive(false);
            OnBattleState();
        }

        /// <summary>Called on every client after the synced battle state changes.</summary>
        public void OnBattleState()
        {
            if (manager == null) return;
            int phase = manager.phase;
            bool active = phase >= BattleManager.PHASE_JOINING;
            if (ring != null) ring.SetActive(active);
            for (int m = 0; m < BattleManager.SIDE; m++)
                if (monsterBodies != null && m < monsterBodies.Length && monsterBodies[m] != null)
                    monsterBodies[m].SetActive(phase >= BattleManager.PHASE_ROAMING && manager.Alive(BattleManager.SIDE + m));

            VRCPlayerApi local = Networking.LocalPlayer;
            int mySlot = Utilities.IsValid(local) ? manager.FindWizardSlot(local.playerId) : -1;

            if ((phase == BattleManager.PHASE_ROAMING || phase == BattleManager.PHASE_JOINING) && manager.battleId != _battleSeen)
            {
                _battleSeen = manager.battleId;
                _roundSeen = 0;
                _clockActor = -1;
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
                if (mySlot >= 0) ArriveAtArena(mySlot);
                SetBanner("");
            }
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
                }
                SetBanner("Round " + manager.round.ToString() + (manager.firstSide == 0 ? "  -  Wizards act first" : "  -  Monsters act first"));
            }

            if (phase == BattleManager.PHASE_PLAYBACK && _phaseSeen != BattleManager.PHASE_PLAYBACK)
            {
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
                string msg = won ? "Victory!" : "Defeat...";
                if (_inBattle && won) msg += "\n" + GrantRewards();
                if (_inBattle && !won) msg += "\nYou return to the hub to recover.";
                SetBanner(msg);
                _note = msg;
                if (_inBattle) SendCustomEventDelayedSeconds(nameof(_LeaveBattle), Mathf.Max(2, manager.endSeconds - 1));
            }

            if (phase == BattleManager.PHASE_IDLE && _inBattle) _LeaveBattle();
            if (_inBattle && mySlot < 0 && phase != BattleManager.PHASE_IDLE) _LeaveBattle();

            _phaseSeen = phase;
            RefreshLabels();
            RefreshHand();
        }

        // ------------------------------------------------------------------ battle enter / leave

        private void ArriveAtArena(int slot)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || playerState == null || !playerState.HasCharacter()) return;
            _inBattle = true;
            _seated = false;
            _slot = slot;
            Transform spot = stagingSpots != null && slot < stagingSpots.Length && stagingSpots[slot] != null ? stagingSpots[slot] : seats[slot];
            local.TeleportTo(spot.position, spot.rotation);
        }

        private void TakeSeat()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || playerState == null || !playerState.HasCharacter()) return;
            _seated = true;
            CharacterData c = playerState.current;

            Transform seat = seats[_slot];
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
            _note = "Waiting for the other wizards...";
            manager.RequestJoin(
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_HEALTH), (int)c.school, c.level,
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_DAMAGE),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_RESIST),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_ACCURACY));
        }

        public void _LeaveBattle()
        {
            if (!_inBattle) return;
            _inBattle = false;
            _seated = false;
            _slot = -1;
            if (handRoot != null) handRoot.SetActive(false);
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                local.Immobilize(false);
                if (hubReturn != null) local.TeleportTo(hubReturn.position, hubReturn.rotation);
            }
            if (playerState != null && playerState.HasCharacter()) playerState.SaveNow();
        }

        private string GrantRewards()
        {
            if (playerState == null || !playerState.HasCharacter()) return "";
            CharacterData c = playerState.current;
            int xp = 60 + 25 * manager.MonsterCount();
            int gold = Random.Range(30, 61);
            c.gold += gold;
            string msg = "+" + xp.ToString() + " XP   +" + gold.ToString() + " gold";
            int levels = c.AddXp(xp);
            if (levels > 0) msg += "\nLevel up! You are now level " + c.level.ToString() + ". +" + levels.ToString() + " Training Point" + (levels > 1 ? "s" : "") + ".";
            if (lootTable.Length > 0 && Random.value < 0.6f)
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
            int len = c.DeckCount(0);
            for (int i = 0; i < len; i++)
            {
                int id = c.deckCards[i];
                if (cardDatabase.IndexOf(id) >= 0) _pile[_pileCount++] = id;
            }
            if (_pileCount == 0)
            {
                int basic = 100 + (int)c.school * 10;
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
            int card = _hand[i];
            if (_discardMode)
            {
                RemoveFromHand(i);
                _note = "Discarded " + cardDatabase.GetName(card) + ".";
                RefreshHand();
                return;
            }
            if (cardDatabase.GetPipCost(card) > manager.pips[_slot])
            {
                _note = "Not enough pips for " + cardDatabase.GetName(card) + ".";
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
                _playing = false;
                for (int s = 0; s < BattleManager.SLOTS; s++) _shownHp[s] = manager.hp[s];
                if (orb != null) orb.gameObject.SetActive(false);
                _HideSpellAnimation();
                RefreshLabels();
                return;
            }

            int group = manager.actGroup[_playIndex];
            int actor = manager.actActor[_playIndex];
            _clockActor = actor;
            int card = manager.actCard[_playIndex];
            int kind = manager.actKind[_playIndex];
            string text = manager.SlotName(actor);
            if (kind == BattleManager.KIND_PASS) text += " passes.";
            else if (kind == BattleManager.KIND_FIZZLE) text += "'s " + cardDatabase.GetName(card) + " fizzles!";
            else text += " casts " + cardDatabase.GetName(card) + "!";

            int firstTarget = actor;
            while (_playIndex < manager.actCount && manager.actGroup[_playIndex] == group)
            {
                int t = manager.actTarget[_playIndex];
                int k = manager.actKind[_playIndex];
                int v = manager.actValue[_playIndex];
                if (k == BattleManager.KIND_DAMAGE) { text += "\n" + manager.SlotName(t) + " takes " + v.ToString() + " damage"; if (firstTarget == actor) firstTarget = t; }
                else if (k == BattleManager.KIND_HEAL) text += "\n" + manager.SlotName(t) + " heals " + v.ToString();
                else if (k == BattleManager.KIND_BUFF) { text += "\n" + manager.SlotName(t) + " gains " + BuffName(manager.actCard[_playIndex]) + " (" + v.ToString() + "%)"; if (firstTarget == actor) firstTarget = t; }
                if (t >= 0 && t < BattleManager.SLOTS) _shownHp[t] = manager.actHpAfter[_playIndex];
                _playIndex++;
            }
            SetBanner(text);
            if (kind != BattleManager.KIND_PASS && kind != BattleManager.KIND_FIZZLE)
            {
                GameObject anim = cardDatabase.GetAnimation(card);
                if (anim != null) PlaySpellAnimation(anim, actor, firstTarget);
                else LaunchOrb(actor, firstTarget);
            }
            RefreshLabels();
            SendCustomEventDelayedSeconds(nameof(_PlayStep), manager.actionSeconds);
        }

        private string BuffName(int card)
        {
            int e = cardDatabase.GetEffect(card);
            if (e == CardDatabase.EFFECT_WARD) return "a Ward";
            if (e == CardDatabase.EFFECT_EMPOWER) return "Empower";
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

        private void LaunchOrb(int from, int to)
        {
            if (orb == null || seats == null || from < 0 || to < 0) return;
            _orbFrom = seats[from].position + Vector3.up * 1.3f;
            _orbTo = seats[to].position + Vector3.up * 1.1f;
            _orbStart = Time.time;
            orb.position = _orbFrom;
            orb.gameObject.SetActive(true);
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
            if (manager != null && manager.phase >= BattleManager.PHASE_JOINING) UpdateClock();
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
            if (orb != null && orb.gameObject.activeSelf)
            {
                float t = (Time.time - _orbStart) / (manager.actionSeconds * 0.6f);
                if (t >= 1f) orb.gameObject.SetActive(false);
                else orb.position = Vector3.Lerp(_orbFrom, _orbTo, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
            }
            if (manager == null) return;
            if (padLabel != null)
            {
                int phase = manager.phase;
                if (phase == BattleManager.PHASE_IDLE) padLabel.text = "Boss Arena Teleporter\nStep on the pad (up to 4 wizards)";
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
            if (_slot >= 0) s += "    Pips " + manager.pips[_slot].ToString() + " / " + BattleManager.MAX_PIPS.ToString();
            if (manager.phase == BattleManager.PHASE_PLANNING) s += "    Time " + manager.SecondsLeft().ToString() + "s";
            s += "    Deck " + _pileCount.ToString();
            if (_note.Length > 0) s += "\n" + _note;
            return s;
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
            line += "  " + manager.pips[s].ToString() + " pips";
            if (manager.ward[s] > 0) line += "  Ward";
            if (manager.charm[s] > 0) line += "  Empowered";
            if (s < BattleManager.SIDE && manager.phase == BattleManager.PHASE_PLANNING && manager.Alive(s)) line += manager.locked[s] != 0 ? "  (ready)" : "  (choosing)";
            if (_shownHp[s] <= 0) line += "  (defeated)";
            return line;
        }

        private void RefreshHand()
        {
            if (!_inBattle || manager == null) return;
            int myPips = _slot >= 0 ? manager.pips[_slot] : 0;
            for (int i = 0; i < HAND_SIZE; i++)
            {
                bool has = i < _handCount;
                if (cardImages[i] != null) cardImages[i].gameObject.SetActive(has);
                if (!has) continue;
                int card = _hand[i];
                bool usable = cardDatabase.GetPipCost(card) <= myPips;
                Color col = cardDatabase.GetColor(card);
                if (!usable && !_discardMode) col = _cardDim;
                if (i == _selHand) col = Color.Lerp(col, Color.white, 0.45f);
                cardImages[i].color = col;
                cardNames[i].text = cardDatabase.GetName(card);
                cardInfos[i].text = cardDatabase.GetPipCost(card).ToString() + " pips  " + cardDatabase.GetAccuracy(card).ToString() + "%\n" + cardDatabase.GetEffectText(card);
            }

            bool picking = CanAct() && _selHand >= 0 && _selHand < _handCount;
            bool allies = picking && cardDatabase.TargetsAllies(_hand[_selHand]);
            for (int t = 0; t < BattleManager.SLOTS; t++)
            {
                if (targetButtons == null || t >= targetButtons.Length || targetButtons[t] == null) continue;
                bool show = picking && manager.Alive(t) && (manager.SideOf(t) == 0) == allies;
                targetButtons[t].SetActive(show);
                if (show) targetLabels[t].text = manager.SlotName(t);
            }
            if (discardImage != null) discardImage.color = _discardMode ? new Color(0.8f, 0.55f, 0.2f) : new Color(0.4f, 0.3f, 0.25f);
            if (statusLabel != null) statusLabel.text = StatusText();
        }

        private void SetBanner(string text)
        {
            if (bannerLabel != null) bannerLabel.text = text;
        }
    }
}
