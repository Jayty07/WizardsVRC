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
        public MonsterDatabase monsterDatabase;

        [Header("World")]
        public Transform hubReturn;
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

        [Header("Shields / blades / traps orbiting each seat")]
        [Tooltip("Index slot * 3 + kind (0 shields, 1 blades, 2 traps).")]
        public Transform[] fxOrbits;
        [Tooltip("Index (slot * 3 + kind) * BattleManager.FX_MAX + k.")]
        public Renderer[] fxIcons;
        [Tooltip("Indexed by School; the last entry is for General (any school).")]
        public Material[] fxMats;

        [Header("Summoned minions (one body per seat, parented to the ring)")]
        public Transform[] minionBodies;
        [Tooltip("Index slot * 2 + part; recoloured to the minion's school (uses fxMats).")]
        public Renderer[] minionSkins;
        public Transform summonFlash;

        [Header("Quests")]
        public QuestManager questManager;

        [Header("School combat traits (indexed by School): % health, + damage, + resist, + accuracy")]
        public int[] schoolHealthPct = { 100, 125, 85, 105, 100, 100, 100 };
        public int[] schoolDamage = { 10, 0, 25, 0, 5, 10, 5 };
        public int[] schoolResist = { 0, 20, 0, 0, 5, 0, 5 };
        public int[] schoolAccuracy = { 0, 0, -10, 10, 0, 0, 5 };

        private int[] _minionShown = new int[BattleManager.SLOTS];
        private int _walkSlot = -1;
        private float _walkStart;

        private float[] _fxRadius = { 0.75f, 0.45f, 0.9f };
        private float[] _fxSpin = { 70f, -110f, 45f };

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

        public bool IsInBattle() { return _inBattle; }

        /// <summary>True when the local player has a character and isn't already fighting (used by wandering monsters).</summary>
        public bool CanFight() { return !_inBattle && playerState != null && playerState.HasCharacter(); }

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
            if (active) { UpdateSeatRings(); UpdateFx(); }
            UpdateMinions(phase);
            for (int m = 0; m < BattleManager.SIDE; m++)
                if (monsterBodies != null && m < monsterBodies.Length && monsterBodies[m] != null)
                    monsterBodies[m].SetActive(phase >= BattleManager.PHASE_ROAMING && manager.Alive(BattleManager.SIDE + m) && !manager.IsSummon(BattleManager.SIDE + m));

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
            // Joined a wandering-monster fight during its join window: the battle id didn't change, so arrive now.
            if (!_inBattle && mySlot >= 0 && manager.wanderMode && phase >= BattleManager.PHASE_JOINING && phase <= BattleManager.PHASE_PLAYBACK) ArriveAtArena(mySlot);
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
            Transform spot = !manager.wanderMode && stagingSpots != null && slot < stagingSpots.Length && stagingSpots[slot] != null ? stagingSpots[slot] : seats[slot];
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
            int sc = (int)c.school;
            manager.RequestJoin(
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_HEALTH) * Trait(schoolHealthPct, sc, 100) / 100, sc, c.level,
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_DAMAGE) + Trait(schoolDamage, sc, 0),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_RESIST) + Trait(schoolResist, sc, 0),
                itemDatabase.GetTotalStat(c, ItemDatabase.STAT_ACCURACY) + Trait(schoolAccuracy, sc, 0));
        }

        private int Trait(int[] arr, int school, int fallback)
        {
            return arr != null && school >= 0 && school < arr.Length ? arr[school] : fallback;
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
            int xp = 0;
            int gold = 0;
            string drops = "";
            for (int s = BattleManager.SIDE; s < BattleManager.SLOTS; s++)
            {
                int id = manager.MonsterAt(s);
                if (id == 0 || monsterDatabase == null) continue;
                xp += monsterDatabase.GetXp(id);
                gold += monsterDatabase.RollGold(id);
                int item = monsterDatabase.GetDropItem(id);
                if (item > 0 && Random.Range(0, 100) < monsterDatabase.GetDropChance(id) && c.AddItem(item, 1))
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
            int len = c.DeckCount(0);
            for (int i = 0; i < len; i++)
            {
                int id = c.deckCards[i];
                if (cardDatabase.IndexOf(id) >= 0) _pile[_pileCount++] = id;
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
            string text = manager.SlotName(actor);
            if (kind == BattleManager.KIND_PASS) text += " passes.";
            else if (kind == BattleManager.KIND_FIZZLE) text += "'s " + cardDatabase.GetName(card) + " fizzles!";
            else if (kind == BattleManager.KIND_DOT) text += " burns for " + manager.actValue[_playIndex].ToString() + " damage!";
            else if (kind == BattleManager.KIND_STUNNED) text += " is stunned and loses a turn!";
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
            EndCast();
            _HideSpellAnimation();
            RefreshLabels();
        }

        /// <summary>Shows the current step's results (damage, heals, buffs) and updates the shown health.</summary>
        private void ApplyStep()
        {
            string text = _stepHeader;
            for (int i = _stepStart; i < _stepEnd && i < manager.actCount; i++)
            {
                int t = manager.actTarget[i];
                int k = manager.actKind[i];
                int v = manager.actValue[i];
                if (k == BattleManager.KIND_DAMAGE) text += "\n" + manager.SlotName(t) + " takes " + v.ToString() + " damage";
                else if (k == BattleManager.KIND_HEAL) text += "\n" + manager.SlotName(t) + " heals " + v.ToString();
                else if (k == BattleManager.KIND_BUFF) text += "\n" + manager.SlotName(t) + BuffText(manager.actCard[i], v);
                if (t >= 0 && t < BattleManager.SLOTS) _shownHp[t] = manager.actHpAfter[i];
            }
            SetBanner(text);
            RefreshLabels();
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
            float scale = from == BattleManager.BOSS_SLOT ? 1.7f : 1f;
            _ringBase = seats[from].position + Vector3.up * 0.05f;
            _glyphBase = seats[from].position + Vector3.up * (2.6f * scale);
            _orbFrom = seats[from].position + Vector3.up * (1.3f * scale);
            _orbTo = seats[to].position + Vector3.up * (to == BattleManager.BOSS_SLOT ? 1.9f : 1.1f);
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
            float scale = _castFrom == BattleManager.BOSS_SLOT ? 1.7f : 1f;
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
                    int[] arr = kind == 0 ? manager.shields : (kind == 1 ? manager.blades : manager.traps);
                    int n = manager.Occupied(s) ? manager.FxCount(arr, s) : 0;
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
                        if (fxMats.Length > 0 && r.sharedMaterial != fxMats[mi]) r.sharedMaterial = fxMats[mi];
                        float ang = k * 360f / n;
                        Quaternion around = Quaternion.Euler(0f, ang, 0f);
                        r.transform.localPosition = around * Vector3.forward * _fxRadius[kind];
                        r.transform.localRotation = kind == 0 ? around * Quaternion.Euler(90f, 0f, 0f) : (kind == 1 ? around : around * Quaternion.Euler(0f, 45f, 0f));
                    }
                }
            }
        }

        private void SpinFx()
        {
            if (fxOrbits == null) return;
            for (int i = 0; i < fxOrbits.Length; i++)
                if (fxOrbits[i] != null) fxOrbits[i].localRotation = Quaternion.Euler(0f, Time.time * _fxSpin[i % 3], 0f);
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
                bool want = active && manager.IsSummon(s) && manager.Alive(s);
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
                Material m = manager.slotIds[s] != 0 && seatSchoolMats != null && sc >= 0 && sc < seatSchoolMats.Length
                    ? seatSchoolMats[sc] : seatEmptyMat;
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
            if (manager != null && manager.phase >= BattleManager.PHASE_JOINING) { UpdateClock(); SpinFx(); AnimateSummon(); }
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
            // Wandering-monster lane: anyone who walks up to the circle during the join window joins the fight.
            if (manager != null && manager.wanderMode && manager.phase == BattleManager.PHASE_JOINING && CanFight() && arenaCenter != null && Time.time >= _nextEngage)
            {
                VRCPlayerApi me = Networking.LocalPlayer;
                Vector3 p = me.GetPosition() - arenaCenter.position;
                p.y = 0f;
                if (p.magnitude <= engageRadius)
                {
                    _nextEngage = Time.time + 1f;
                    manager.RequestEngageWander();
                }
            }
            if (manager != null) AnimateCast();
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
