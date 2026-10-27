using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Story quests for the local player. Each quest's stage lives in <see cref="CharacterData.questProgress"/>[questId]
    /// (0 = not started), so progress saves with the character. NPCs call the Talk* events through an <see cref="EventRelay"/>.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestManager : UdonSharpBehaviour
    {
        public const int QUEST_PROPHECY = 0;
        public const int STAGE_FIND_PROFESSOR = 1;
        public const int STAGE_REPORT = 2;
        public const int STAGE_DONE = 3;
        /// <summary>School trial (level 35): earns the school's quest-only spell (CardDatabase.QUEST_TIER).</summary>
        public const int QUEST_TRIAL = 1;
        public const int TRIAL_TALK = 1;
        public const int TRIAL_WIN = 2;
        public const int TRIAL_RETURN = 3;
        public const int TRIAL_DONE = 4;
        public const int TRIAL_LEVEL = 35;
        public const int QUEST_COUNT = 2;

        public PlayerState playerState;
        public CutscenePlayer cutscenePlayer;
        public PlayerHUD hud;
        public CardDatabase cardDatabase;
        public NewSpellPopup popup;
        [Tooltip("Where the trial's 'win a battle' step points the guide (the arena teleporter pad).")]
        public Transform arenaTarget;

        [Header("Cutscenes")]
        /// <summary>Indexed by <see cref="School"/>.</summary>
        public Cutscene[] professorCutscenes;
        public Cutscene headmasterReport;
        public Cutscene headmasterHint;
        public Cutscene headmasterIdle;

        [Header("Names (indexed by School)")]
        public string[] schoolNames;
        public string[] professorNames;

        [Header("Rewards: The Prophecy")]
        public int prophecyXp = 100;
        public int prophecyGold = 50;

        [Header("Quest log (indexed by quest id)")]
        public string[] questTitles;
        public string[] questDescriptions;
        public string[] questRewards;

        [Header("Guide targets")]
        /// <summary>Indexed by <see cref="School"/>.</summary>
        public Transform[] professorTargets;
        public Transform headmasterTarget;

        private int _talkingTo = -1;

        // ------------------------------------------------------------------ hooks

        /// <summary>Sent by CharacterSelectUI after a character is created or loaded.</summary>
        public void OnCharacterActivated()
        {
            if (!HasCharacter()) return;
            if (GetStage(QUEST_PROPHECY) == 0) SetStage(QUEST_PROPHECY, STAGE_FIND_PROFESSOR);
            CheckTrialStart();
        }

        /// <summary>Starts the school trial once the character reaches <see cref="TRIAL_LEVEL"/>.</summary>
        public void CheckTrialStart()
        {
            if (!HasCharacter() || GetStage(QUEST_TRIAL) != 0 || playerState.current.level < TRIAL_LEVEL) return;
            SetStage(QUEST_TRIAL, TRIAL_TALK);
        }

        /// <summary>Sent by BattleClient when the local player wins a fight.</summary>
        public void OnBattleWon()
        {
            if (!HasCharacter()) return;
            CheckTrialStart();
            if (GetStage(QUEST_TRIAL) == TRIAL_WIN) SetStage(QUEST_TRIAL, TRIAL_RETURN);
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
            int stage = HasCharacter() ? GetStage(QUEST_PROPHECY) : 0;
            if (stage == STAGE_REPORT) cutscenePlayer.Play(headmasterReport);
            else if (stage == STAGE_FIND_PROFESSOR) cutscenePlayer.Play(headmasterHint);
            else cutscenePlayer.Play(headmasterIdle);
        }

        // ------------------------------------------------------------------ cutscene callbacks

        public void OnProfessorFinished()
        {
            int school = _talkingTo;
            _talkingTo = -1;
            if (!HasCharacter()) return;
            if (GetStage(QUEST_PROPHECY) == STAGE_FIND_PROFESSOR && (int)playerState.current.school == school)
                SetStage(QUEST_PROPHECY, STAGE_REPORT);
            if ((int)playerState.current.school != school) return;
            CheckTrialStart();
            int trial = GetStage(QUEST_TRIAL);
            if (trial == TRIAL_TALK) SetStage(QUEST_TRIAL, TRIAL_WIN);
            else if (trial == TRIAL_RETURN) FinishTrial(school);
        }

        private void FinishTrial(int school)
        {
            int spell = cardDatabase != null ? cardDatabase.QuestSpell(school) : 0;
            CharacterData c = playerState.current;
            bool learned = spell > 0 && c.Learn(spell);
            SetStage(QUEST_TRIAL, TRIAL_DONE);
            if (learned && popup != null) popup.Show(spell);
        }

        public void OnReportFinished()
        {
            if (!HasCharacter() || GetStage(QUEST_PROPHECY) != STAGE_REPORT) return;
            CharacterData c = playerState.current;
            c.gold += prophecyGold;
            c.AddXp(prophecyXp);
            SetStage(QUEST_PROPHECY, STAGE_DONE);
        }

        // ------------------------------------------------------------------ quest log

        public int QuestCount() { return QUEST_COUNT; }

        public bool IsStarted(int questId) { return GetStage(questId) > 0; }

        public bool IsComplete(int questId)
        {
            if (questId == QUEST_PROPHECY) return GetStage(questId) >= STAGE_DONE;
            if (questId == QUEST_TRIAL) return GetStage(questId) >= TRIAL_DONE;
            return false;
        }

        public bool IsActive(int questId) { return IsStarted(questId) && !IsComplete(questId); }

        public string GetTitle(int questId) { return NameAt(questTitles, questId); }
        public string GetDescription(int questId) { return NameAt(questDescriptions, questId); }
        public string GetRewardText(int questId) { return NameAt(questRewards, questId); }

        /// <summary>The current step of a quest, e.g. "Return to Headmaster Greymantle's office".</summary>
        public string GetObjectiveText(int questId)
        {
            if (IsComplete(questId)) return "Complete!";
            if (questId == QUEST_PROPHECY)
            {
                int stage = GetStage(questId);
                if (stage == STAGE_FIND_PROFESSOR)
                {
                    int s = (int)playerState.current.school;
                    return "Find " + NameAt(professorNames, s) + " in the " + NameAt(schoolNames, s) + " classroom (Hall of Schools)";
                }
                if (stage == STAGE_REPORT) return "Return to Headmaster Greymantle's office";
            }
            if (questId == QUEST_TRIAL)
            {
                int stage = GetStage(questId);
                int s = (int)playerState.current.school;
                if (stage == TRIAL_TALK) return "Speak with " + NameAt(professorNames, s) + " about your trial";
                if (stage == TRIAL_WIN) return "Win a battle in the arena (Training Yard)";
                if (stage == TRIAL_RETURN) return "Return to " + NameAt(professorNames, s);
            }
            return "";
        }

        /// <summary>Where the current step happens (used by the guide arrow); null if there is no place to go.</summary>
        public Transform GetObjectiveTarget(int questId)
        {
            if (!IsActive(questId)) return null;
            if (questId == QUEST_PROPHECY)
            {
                int stage = GetStage(questId);
                if (stage == STAGE_FIND_PROFESSOR)
                {
                    int s = (int)playerState.current.school;
                    return professorTargets != null && s >= 0 && s < professorTargets.Length ? professorTargets[s] : null;
                }
                if (stage == STAGE_REPORT) return headmasterTarget;
            }
            if (questId == QUEST_TRIAL)
            {
                int stage = GetStage(questId);
                if (stage == TRIAL_WIN) return arenaTarget;
                int s = (int)playerState.current.school;
                return professorTargets != null && s >= 0 && s < professorTargets.Length ? professorTargets[s] : null;
            }
            return null;
        }

        public string GetObjectiveTargetName(int questId)
        {
            if (questId == QUEST_PROPHECY)
            {
                int stage = GetStage(questId);
                if (stage == STAGE_FIND_PROFESSOR) return NameAt(professorNames, (int)playerState.current.school);
                if (stage == STAGE_REPORT) return "Headmaster Greymantle";
            }
            if (questId == QUEST_TRIAL)
                return GetStage(questId) == TRIAL_WIN ? "Arena teleporter" : NameAt(professorNames, (int)playerState.current.school);
            return "";
        }

        // ------------------------------------------------------------------ tracking

        /// <summary>The tracked quest if it is still active, otherwise <see cref="CharacterData.NO_TRACKED_QUEST"/>.</summary>
        public int GetTrackedQuest()
        {
            if (!HasCharacter()) return CharacterData.NO_TRACKED_QUEST;
            int q = playerState.current.trackedQuest;
            return q >= 0 && q < QUEST_COUNT && IsActive(q) ? q : CharacterData.NO_TRACKED_QUEST;
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

        // ------------------------------------------------------------------ helpers

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
            bool trackedActive = tracked >= 0 && tracked < QUEST_COUNT && IsActive(tracked);
            if (IsActive(questId) && !trackedActive) c.trackedQuest = questId;
            else if (!trackedActive) c.trackedQuest = FirstActiveQuest();
            playerState.MarkDirty();
            playerState.SaveNow();
            if (hud != null) hud.Refresh();
        }

        private int FirstActiveQuest()
        {
            for (int q = 0; q < QUEST_COUNT; q++)
                if (IsActive(q)) return q;
            return CharacterData.NO_TRACKED_QUEST;
        }

        private string NameAt(string[] names, int i)
        {
            return names != null && i >= 0 && i < names.Length ? names[i] : "?";
        }
    }
}
