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

        public PlayerState playerState;
        public CutscenePlayer cutscenePlayer;
        public PlayerHUD hud;

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

        private int _talkingTo = -1;

        // ------------------------------------------------------------------ hooks

        /// <summary>Sent by CharacterSelectUI after a character is created or loaded.</summary>
        public void OnCharacterActivated()
        {
            if (!HasCharacter()) return;
            if (GetStage(QUEST_PROPHECY) == 0) SetStage(QUEST_PROPHECY, STAGE_FIND_PROFESSOR);
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
        }

        public void OnReportFinished()
        {
            if (!HasCharacter() || GetStage(QUEST_PROPHECY) != STAGE_REPORT) return;
            CharacterData c = playerState.current;
            c.gold += prophecyGold;
            c.AddXp(prophecyXp);
            SetStage(QUEST_PROPHECY, STAGE_DONE);
        }

        // ------------------------------------------------------------------ tracker

        /// <summary>Text for the HUD quest tracker; empty when there is nothing to show.</summary>
        public string GetTrackerText()
        {
            if (!HasCharacter()) return "";
            int stage = GetStage(QUEST_PROPHECY);
            if (stage == STAGE_FIND_PROFESSOR)
            {
                int s = (int)playerState.current.school;
                return "The Prophecy\n- Find " + NameAt(professorNames, s) + " in the " + NameAt(schoolNames, s) + " classroom (Hall of Schools)";
            }
            if (stage == STAGE_REPORT) return "The Prophecy\n- Return to Headmaster Greymantle's office";
            return "";
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
            playerState.current.questProgress[questId] = (byte)stage;
            playerState.MarkDirty();
            playerState.SaveNow();
            if (hud != null) hud.Refresh();
        }

        private string NameAt(string[] names, int i)
        {
            return names != null && i >= 0 && i < names.Length ? names[i] : "?";
        }
    }
}
