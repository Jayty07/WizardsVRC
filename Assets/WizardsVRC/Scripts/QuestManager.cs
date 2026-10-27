using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Data-driven quests for the local player. The scene builder fills the quest tables (one entry per quest, steps
    /// flattened as quest * MAX_STEPS + step). A quest's stage lives in <see cref="CharacterData.questProgress"/>[questId]:
    /// 0 = not started, 1..stepCount = the current step (1-based), stepCount + 1 = complete. Step progress ("3 of 5")
    /// lives in <see cref="CharacterData.questCounters"/>. Both save with the character.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestManager : UdonSharpBehaviour
    {
        public const int MAX_STEPS = 8;

        public const int STEP_TALK = 0;      // param = npc id
        public const int STEP_DEFEAT = 1;    // param = monster id, count = how many
        public const int STEP_COLLECT = 2;   // param = monster id that drops the quest item, count = how many
        public const int STEP_INTERACT = 3;  // param = QuestTarget id, count = how many of them
        public const int STEP_EXPLORE = 4;   // param = QuestTarget id (trigger)
        public const int STEP_BOSS = 5;      // param = monster id of the boss to defeat

        /// <summary>NPC ids: 0-6 the professors (by School), 7 the Headmaster, NPC_OWN_PROFESSOR = the player's own professor.</summary>
        public const int NPC_HEADMASTER = 7;
        public const int NPC_OWN_PROFESSOR = 100;
        /// <summary>Street NPCs start at 8 (index into streetNpcCutscenes = id - NPC_STREET_FIRST).</summary>
        public const int NPC_STREET_FIRST = 8;
        public const int NPC_TILDA = 8;
        /// <summary>questSpell value meaning "the player's own school's quest-tier spell".</summary>
        public const int SPELL_OWN_SCHOOL_QUEST = -1;

        public const int QUEST_PROPHECY = 0;
        public const int QUEST_TRIAL = 1;
        public const int TRIAL_LEVEL = 35;

        public PlayerState playerState;
        public CutscenePlayer cutscenePlayer;
        public PlayerHUD hud;
        public CardDatabase cardDatabase;
        public NewSpellPopup popup;

        [Header("Cutscenes")]
        /// <summary>Indexed by <see cref="School"/>.</summary>
        public Cutscene[] professorCutscenes;
        public Cutscene headmasterReport;
        public Cutscene headmasterHint;
        public Cutscene headmasterIdle;
        /// <summary>One cutscene per street NPC (index = npc id - NPC_STREET_FIRST).</summary>
        public Cutscene[] streetNpcCutscenes;

        [Header("Names (indexed by School)")]
        public string[] schoolNames;
        public string[] professorNames;

        [Header("Quest table (indexed by quest id)")]
        public string[] questTitles;
        public string[] questDescriptions;
        public string[] questRewards;
        public int[] questXp;
        public int[] questGold;
        public int[] questItems;
        /// <summary>Spell learned on completion (0 = none, SPELL_OWN_SCHOOL_QUEST = own school's quest spell).</summary>
        public int[] questSpells;
        /// <summary>Quest that must be complete first (-1 = none).</summary>
        public int[] questPrereqs;
        public int[] questMinLevels;
        /// <summary>1 = starts by itself once available; 0 = starts when you talk to questGivers[q].</summary>
        public int[] questAutoStart;
        public int[] questGivers;
        public int[] questStepCounts;

        [Header("Steps (quest * MAX_STEPS + step)")]
        public int[] stepTypes;
        public int[] stepParams;
        public int[] stepCounts;
        public string[] stepTexts;
        public string[] stepTargetNames;
        public Transform[] stepTargets;

        [Header("Guide targets")]
        /// <summary>Indexed by <see cref="School"/>.</summary>
        public Transform[] professorTargets;
        public Transform headmasterTarget;

        private int _talkingTo = -1;
        private int _talkingNpc = -1;

        // ------------------------------------------------------------------ hooks

        /// <summary>Sent by CharacterSelectUI after a character is created or loaded.</summary>
        public void OnCharacterActivated()
        {
            if (!HasCharacter()) return;
            CheckAutoStarts();
            if (hud != null) hud.Refresh();
        }

        /// <summary>Sent by BattleClient when the local player wins a fight: counts defeats, boss kills and quest-item drops.</summary>
        public void OnBattleWon(BattleManager battle)
        {
            if (!HasCharacter()) return;
            CheckAutoStarts();
            if (battle == null) return;
            for (int s = BattleManager.SIDE; s < BattleManager.SLOTS; s++)
            {
                int monster = battle.MonsterAt(s);
                if (monster == 0) continue;
                for (int q = 0; q < QuestCount(); q++)
                {
                    if (!IsActive(q)) continue;
                    int i = StepIndex(q);
                    int type = stepTypes[i];
                    if (stepParams[i] != monster) continue;
                    if (type == STEP_DEFEAT || type == STEP_BOSS) Advance(q, 1);
                    else if (type == STEP_COLLECT)
                    {
                        // Quest items only drop from the monster: 60% per kill, always on the last one needed.
                        int have = playerState.current.questCounters[q];
                        if (have >= stepCounts[i] - 1 || Random.Range(0, 100) < 60) Advance(q, 1);
                    }
                }
            }
        }

        /// <summary>A QuestTarget was clicked.</summary>
        public void OnInteract(int targetId) { Progress(STEP_INTERACT, targetId); }

        /// <summary>The player walked into a QuestTarget trigger.</summary>
        public void OnExplore(int targetId) { Progress(STEP_EXPLORE, targetId); }

        /// <summary>Finished talking to an NPC: hands in Talk steps and starts quests that NPC gives.</summary>
        public void OnTalk(int npcId)
        {
            if (!HasCharacter()) return;
            int own = (int)playerState.current.school;
            for (int q = 0; q < QuestCount(); q++)
            {
                if (IsActive(q))
                {
                    int i = StepIndex(q);
                    if (stepTypes[i] == STEP_TALK && NpcMatches(stepParams[i], npcId, own)) Advance(q, 1);
                }
                else if (!IsStarted(q) && Available(q) && questAutoStart[q] == 0 && NpcMatches(questGivers[q], npcId, own)) SetStage(q, 1);
            }
        }

        public void TalkFire() { TalkProfessor(0); }
        public void TalkIce() { TalkProfessor(1); }
        public void TalkStorm() { TalkProfessor(2); }
        public void TalkLife() { TalkProfessor(3); }
        public void TalkDeath() { TalkProfessor(4); }
        public void TalkMyth() { TalkProfessor(5); }
        public void TalkBalance() { TalkProfessor(6); }

        public void TalkHeadmaster()
        {
            if (Busy()) return;
            int stage = GetStage(QUEST_PROPHECY);
            if (IsActive(QUEST_PROPHECY) && stepTypes[StepIndex(QUEST_PROPHECY)] == STEP_TALK && stepParams[StepIndex(QUEST_PROPHECY)] == NPC_HEADMASTER)
                cutscenePlayer.Play(headmasterReport);
            else if (stage > 0 && IsActive(QUEST_PROPHECY)) cutscenePlayer.Play(headmasterHint);
            else cutscenePlayer.Play(headmasterIdle);
        }

        // ------------------------------------------------------------------ cutscene callbacks

        public void OnProfessorFinished()
        {
            int school = _talkingTo;
            _talkingTo = -1;
            if (school >= 0) OnTalk(school);
        }

        public void OnHeadmasterFinished() { OnTalk(NPC_HEADMASTER); }

        public void TalkTilda() { TalkStreetNpc(NPC_TILDA); }

        public void OnStreetNpcFinished()
        {
            int npc = _talkingNpc;
            _talkingNpc = -1;
            if (npc >= 0) OnTalk(npc);
        }

        private void TalkStreetNpc(int npc)
        {
            int i = npc - NPC_STREET_FIRST;
            if (Busy() || streetNpcCutscenes == null || i < 0 || i >= streetNpcCutscenes.Length) return;
            _talkingNpc = npc;
            cutscenePlayer.Play(streetNpcCutscenes[i]);
        }

        // ------------------------------------------------------------------ quest log

        public int QuestCount() { return questTitles != null ? questTitles.Length : 0; }

        public bool IsStarted(int questId) { return GetStage(questId) > 0; }

        public bool IsComplete(int questId)
        {
            return questId >= 0 && questId < QuestCount() && GetStage(questId) > questStepCounts[questId];
        }

        public bool IsActive(int questId) { return IsStarted(questId) && !IsComplete(questId); }

        /// <summary>Prerequisite done and level reached.</summary>
        public bool Available(int questId)
        {
            if (!HasCharacter() || questId < 0 || questId >= QuestCount()) return false;
            int pre = questPrereqs[questId];
            if (pre >= 0 && !IsComplete(pre)) return false;
            return playerState.current.level >= questMinLevels[questId];
        }

        public string GetTitle(int questId) { return NameAt(questTitles, questId); }
        public string GetDescription(int questId) { return NameAt(questDescriptions, questId); }
        public string GetRewardText(int questId) { return NameAt(questRewards, questId); }

        /// <summary>The current step, e.g. "Defeat Gloom Wisps (2 / 3)".</summary>
        public string GetObjectiveText(int questId)
        {
            if (IsComplete(questId)) return "Complete!";
            if (!IsActive(questId)) return "";
            int i = StepIndex(questId);
            string text = Fill(stepTexts[i]);
            if (stepCounts[i] > 1) text += " (" + playerState.current.questCounters[questId].ToString() + " / " + stepCounts[i].ToString() + ")";
            return text;
        }

        /// <summary>Where the current step happens (used by the guide arrow); null if there is no place to go.</summary>
        public Transform GetObjectiveTarget(int questId)
        {
            if (!IsActive(questId)) return null;
            int i = StepIndex(questId);
            if (stepTypes[i] == STEP_TALK)
            {
                int npc = stepParams[i];
                if (npc == NPC_OWN_PROFESSOR) npc = (int)playerState.current.school;
                if (npc == NPC_HEADMASTER) return headmasterTarget;
                if (npc >= 0 && professorTargets != null && npc < professorTargets.Length) return professorTargets[npc];
            }
            return stepTargets != null && i < stepTargets.Length ? stepTargets[i] : null;
        }

        public string GetObjectiveTargetName(int questId)
        {
            if (!IsActive(questId)) return "";
            int i = StepIndex(questId);
            return Fill(stepTargetNames != null && i < stepTargetNames.Length ? stepTargetNames[i] : "");
        }

        // ------------------------------------------------------------------ tracking

        /// <summary>The tracked quest if it is still active, otherwise <see cref="CharacterData.NO_TRACKED_QUEST"/>.</summary>
        public int GetTrackedQuest()
        {
            if (!HasCharacter()) return CharacterData.NO_TRACKED_QUEST;
            int q = playerState.current.trackedQuest;
            return q >= 0 && q < QuestCount() && IsActive(q) ? q : CharacterData.NO_TRACKED_QUEST;
        }

        /// <summary>Track a quest (or pass <see cref="CharacterData.NO_TRACKED_QUEST"/> to stop tracking).</summary>
        public void SetTracked(int questId)
        {
            if (!HasCharacter()) return;
            playerState.current.trackedQuest = questId;
            playerState.MarkDirty();
            playerState.SaveNow();
            if (hud != null) hud.Refresh();
        }

        /// <summary>Text for the HUD quest tracker; empty when nothing is tracked.</summary>
        public string GetTrackerText()
        {
            int q = GetTrackedQuest();
            if (q == CharacterData.NO_TRACKED_QUEST) return "";
            return GetTitle(q) + "\n- " + GetObjectiveText(q);
        }

        // ------------------------------------------------------------------ progression

        private void Progress(int type, int param)
        {
            if (!HasCharacter()) return;
            for (int q = 0; q < QuestCount(); q++)
            {
                if (!IsActive(q)) continue;
                int i = StepIndex(q);
                if (stepTypes[i] == type && stepParams[i] == param) Advance(q, 1);
            }
        }

        /// <summary>Adds to the current step's counter; moves to the next step (or completes the quest) when it is full.</summary>
        private void Advance(int q, int amount)
        {
            CharacterData c = playerState.current;
            int i = StepIndex(q);
            int have = c.questCounters[q] + amount;
            if (have < stepCounts[i])
            {
                c.questCounters[q] = (byte)have;
                playerState.MarkDirty();
                playerState.SaveNow();
                if (hud != null) hud.Refresh();
                return;
            }
            c.questCounters[q] = 0;
            int stage = GetStage(q) + 1;
            if (stage > questStepCounts[q]) Complete(q, stage);
            else SetStage(q, stage);
        }

        private void Complete(int q, int stage)
        {
            CharacterData c = playerState.current;
            c.gold += questGold[q];
            if (questItems[q] > 0) c.AddItem(questItems[q], 1);
            int spell = questSpells[q];
            if (spell == SPELL_OWN_SCHOOL_QUEST && cardDatabase != null) spell = cardDatabase.QuestSpell((int)c.school);
            bool learned = spell > 0 && c.Learn(spell);
            c.AddXp(questXp[q]);
            SetStage(q, stage);
            CheckAutoStarts();
            if (learned && popup != null) popup.Show(spell);
        }

        private void CheckAutoStarts()
        {
            for (int q = 0; q < QuestCount(); q++)
                if (!IsStarted(q) && questAutoStart[q] != 0 && Available(q)) SetStage(q, 1);
        }

        // ------------------------------------------------------------------ helpers

        private int StepIndex(int q) { return q * MAX_STEPS + Mathf.Clamp(GetStage(q) - 1, 0, MAX_STEPS - 1); }

        private bool NpcMatches(int wanted, int npcId, int ownSchool)
        {
            return wanted == npcId || (wanted == NPC_OWN_PROFESSOR && npcId == ownSchool);
        }

        /// <summary>Replaces {professor} and {school} with the player's own professor / school.</summary>
        private string Fill(string text)
        {
            if (text == null) return "";
            int s = (int)playerState.current.school;
            if (text.IndexOf("{professor}") >= 0) text = text.Replace("{professor}", NameAt(professorNames, s));
            if (text.IndexOf("{school}") >= 0) text = text.Replace("{school}", NameAt(schoolNames, s));
            return text;
        }

        private void TalkProfessor(int school)
        {
            if (Busy() || professorCutscenes == null || school < 0 || school >= professorCutscenes.Length) return;
            _talkingTo = school;
            cutscenePlayer.Play(professorCutscenes[school]);
        }

        private bool Busy()
        {
            return cutscenePlayer == null || cutscenePlayer.IsPlaying();
        }

        private bool HasCharacter()
        {
            return playerState != null && playerState.HasCharacter() && playerState.current != null;
        }

        public int GetStage(int questId)
        {
            if (!HasCharacter() || questId < 0 || questId >= CharacterData.QUEST_SLOTS) return 0;
            return playerState.current.questProgress[questId];
        }

        private void SetStage(int questId, int stage)
        {
            CharacterData c = playerState.current;
            c.questProgress[questId] = (byte)stage;
            int tracked = c.trackedQuest;
            bool trackedActive = tracked >= 0 && tracked < QuestCount() && IsActive(tracked);
            if (IsActive(questId) && !trackedActive) c.trackedQuest = questId;
            else if (!trackedActive) c.trackedQuest = FirstActiveQuest();
            playerState.MarkDirty();
            playerState.SaveNow();
            if (hud != null) hud.Refresh();
        }

        private int FirstActiveQuest()
        {
            for (int q = 0; q < QuestCount(); q++)
                if (IsActive(q)) return q;
            return CharacterData.NO_TRACKED_QUEST;
        }

        private string NameAt(string[] names, int i)
        {
            return names != null && i >= 0 && i < names.Length ? names[i] : "?";
        }
    }
}
