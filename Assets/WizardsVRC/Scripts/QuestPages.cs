using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Quests tab. Left page: started quests (active first, then completed).
    /// Right page: the selected quest's story, current step and rewards, with Track / Stop Tracking and a guide-arrow toggle.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestPages : UdonSharpBehaviour
    {
        public const int ROWS = 8;

        public PlayerState playerState;
        public QuestManager questManager;
        public QuestGuide questGuide;

        [Header("Left page")]
        public GameObject[] rowRoots;
        public Image[] rowImages;
        public Text[] rowLabels;
        public Text pageLabel;
        public Text emptyLabel;

        [Header("Right page")]
        public GameObject detailsRoot;
        public Text detailTitle;
        public Text detailStatus;
        public Text detailBody;
        public Text detailObjective;
        public Text detailRewards;
        public GameObject trackButton;
        public Text trackLabel;
        public Text guideLabel;
        [Tooltip("The current world, its goal and how many of its storylines are finished.")]
        public Text worldLabel;

        private Color _rowOff = new Color(0.78f, 0.7f, 0.55f);
        private Color _rowOn = new Color(0.95f, 0.8f, 0.45f);
        private Color _rowDone = new Color(0.62f, 0.62f, 0.58f);

        private int[] _list = new int[CharacterData.QUEST_SLOTS];
        private int _count;
        private int _page;
        private int _selected = -1;

        public void Row0() { SelectRow(0); }
        public void Row1() { SelectRow(1); }
        public void Row2() { SelectRow(2); }
        public void Row3() { SelectRow(3); }
        public void Row4() { SelectRow(4); }
        public void Row5() { SelectRow(5); }
        public void Row6() { SelectRow(6); }
        public void Row7() { SelectRow(7); }

        public void Open()
        {
            if (!Ready()) return;
            BuildList();
            int tracked = questManager.GetTrackedQuest();
            _selected = tracked != CharacterData.NO_TRACKED_QUEST ? tracked : (_count > 0 ? _list[0] : -1);
            _page = 0;
            for (int i = 0; i < _count; i++)
                if (_list[i] == _selected) _page = i / ROWS;
            Refresh();
        }

        public void PrevPage()
        {
            if (_page > 0) _page--;
            Refresh();
        }

        public void NextPage()
        {
            if (_page < PageCount() - 1) _page++;
            Refresh();
        }

        /// <summary>Track the selected quest, or stop tracking it if it already is.</summary>
        public void ToggleTrack()
        {
            if (!Ready() || _selected < 0 || !questManager.IsActive(_selected)) return;
            bool tracked = questManager.GetTrackedQuest() == _selected;
            questManager.SetTracked(tracked ? CharacterData.NO_TRACKED_QUEST : _selected);
            Refresh();
        }

        public void ToggleGuide()
        {
            if (questGuide != null) questGuide.ToggleGuide();
            Refresh();
        }

        private void SelectRow(int row)
        {
            int i = _page * ROWS + row;
            if (i >= _count) return;
            _selected = _list[i];
            Refresh();
        }

        private void BuildList()
        {
            _count = 0;
            int n = questManager.QuestCount();
            for (int q = 0; q < n; q++)
                if (questManager.IsActive(q)) _list[_count++] = q;
            for (int q = 0; q < n; q++)
                if (questManager.IsComplete(q)) _list[_count++] = q;
        }

        private int PageCount()
        {
            return Mathf.Max(1, (_count + ROWS - 1) / ROWS);
        }

        public void Refresh()
        {
            if (!Ready()) return;
            BuildList();
            _page = Mathf.Clamp(_page, 0, PageCount() - 1);
            int tracked = questManager.GetTrackedQuest();

            for (int r = 0; r < ROWS; r++)
            {
                int i = _page * ROWS + r;
                bool has = i < _count;
                if (rowRoots[r] != null) rowRoots[r].SetActive(has);
                if (!has) continue;
                int q = _list[i];
                bool done = questManager.IsComplete(q);
                string mark = q == tracked ? "> " : "   ";
                rowLabels[r].text = mark + questManager.GetTitle(q) + (done ? "  (Complete)" : "");
                rowImages[r].color = q == _selected ? _rowOn : (done ? _rowDone : _rowOff);
            }
            if (pageLabel != null) pageLabel.text = "Page " + (_page + 1).ToString() + " / " + PageCount().ToString();
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(_count == 0);
            if (guideLabel != null)
                guideLabel.text = "Guide arrow: " + (questGuide != null && questGuide.IsGuideEnabled() ? "On" : "Off");
            if (worldLabel != null && questManager != null) worldLabel.text = questManager.WorldSummary();

            bool show = _selected >= 0 && _selected < questManager.QuestCount() && questManager.IsStarted(_selected);
            if (detailsRoot != null) detailsRoot.SetActive(show);
            if (!show) return;
            bool active = questManager.IsActive(_selected);
            detailTitle.text = questManager.GetTitle(_selected);
            detailStatus.text = active ? (tracked == _selected ? "Active  -  Tracking" : "Active") : "Complete";
            detailBody.text = questManager.GetDescription(_selected);
            detailObjective.text = active ? "Current step:\n" + questManager.GetObjectiveText(_selected) : "You finished this quest.";
            detailRewards.text = "Rewards: " + questManager.GetRewardText(_selected);
            if (trackButton != null) trackButton.SetActive(active);
            if (trackLabel != null) trackLabel.text = tracked == _selected ? "Stop Tracking" : "Track";
        }

        private bool Ready()
        {
            return questManager != null && playerState != null && playerState.HasCharacter() && playerState.current != null;
        }
    }
}
