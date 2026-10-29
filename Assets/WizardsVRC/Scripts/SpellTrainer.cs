using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A professor's spell list: shows the school's spells and lets the local player learn them.
    /// Own-school spells are free; other schools cost 1 training point. The level requirement and the previous spell in
    /// the tree (CardDatabase.prereqs) always apply; the quest-tier spell is only earned from the school's trial quest.
    /// One shared panel, placed in front of the player when opened.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SpellTrainer : UdonSharpBehaviour
    {
        public const int ROWS = 12;

        public PlayerState playerState;
        public CardDatabase cardDatabase;
        public NewSpellPopup popup;
        public PlayerHUD hud;

        public GameObject panelRoot;
        public Text titleLabel;
        public Text pointsLabel;
        [Tooltip("Each spell is a small card (frame, name, pip cost, art) with its status under it.")]
        public GameObject[] rowRoots;
        public Image[] rowIcons;
        public Text[] rowNames;
        public Text[] rowInfo;
        public Text[] rowPips;
        public RawImage[] rowArts;
        public GameObject detailsPanel;
        public Text detailName;
        public Text detailBody;
        [Header("The selected spell as a full card")]
        public Image detailFrame;
        public Text detailCardName;
        public Text detailPips;
        public RawImage detailArt;
        public Text detailAccuracy;
        public Text detailInfo;
        public Text detailSchool;
        public GameObject learnButton;
        public Text learnLabel;
        public Text statusLabel;

        [Header("Names (indexed by School)")]
        public string[] professorNames;
        public string[] schoolNames;

        public float panelDistance = 1.3f;
        public float closeDistance = 4f;

        private int _school = -1;
        private int[] _ids = new int[ROWS];
        private int _count;
        private int _selected = -1;
        private Vector3 _openPos;

        public void OpenFire() { Open(0); }
        public void OpenIce() { Open(1); }
        public void OpenStorm() { Open(2); }
        public void OpenLife() { Open(3); }
        public void OpenDeath() { Open(4); }
        public void OpenMyth() { Open(5); }
        public void OpenBalance() { Open(6); }

        public void Row0() { Select(0); }
        public void Row1() { Select(1); }
        public void Row2() { Select(2); }
        public void Row3() { Select(3); }
        public void Row4() { Select(4); }
        public void Row5() { Select(5); }
        public void Row6() { Select(6); }
        public void Row7() { Select(7); }
        public void Row8() { Select(8); }
        public void Row9() { Select(9); }
        public void Row10() { Select(10); }
        public void Row11() { Select(11); }

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Open(int school)
        {
            if (!HasCharacter() || cardDatabase == null || panelRoot == null) return;
            _school = school;
            _selected = -1;
            _count = 0;
            int n = cardDatabase.Count();
            for (int i = 0; i < n && _count < ROWS; i++)
            {
                int id = cardDatabase.IdAt(i);
                if (cardDatabase.GetSchool(id) == school) _ids[_count++] = id;
            }

            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                panelRoot.transform.position = head.position + forward * panelDistance + Vector3.down * 0.15f;
                panelRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                _openPos = local.GetPosition();
            }
            SetStatus("");
            panelRoot.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            _school = -1;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public bool IsOpen() { return _school >= 0; }

        private void Update()
        {
            if (_school < 0) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!HasCharacter() || (Utilities.IsValid(local) && Vector3.Distance(local.GetPosition(), _openPos) > closeDistance)) Close();
        }

        private void Select(int row)
        {
            if (row < 0 || row >= _count) return;
            _selected = _ids[row];
            SetStatus("");
            Refresh();
        }

        public void LearnSelected()
        {
            if (!HasCharacter() || _selected < 0) return;
            CharacterData c = playerState.current;
            int id = _selected;
            if (c.HasLearned(id)) { SetStatus("You already know that spell."); return; }
            if (cardDatabase.IsQuestSpell(id)) { SetStatus("This spell is earned from the " + NameAt(schoolNames, _school) + " trial quest, not taught here."); return; }
            if (c.level < cardDatabase.GetLevelReq(id)) { SetStatus("You must be level " + cardDatabase.GetLevelReq(id).ToString() + " to learn that."); return; }
            int pre = cardDatabase.GetPrereq(id);
            if (pre > 0 && !c.HasLearned(pre)) { SetStatus("You must learn " + cardDatabase.GetName(pre) + " first."); return; }
            int cost = cardDatabase.LearnCost(c, id);
            if (c.trainingPoints < cost) { SetStatus("You need a Training Point. You earn one each level."); return; }
            if (!c.Learn(id)) return;
            c.trainingPoints -= cost;
            playerState.MarkDirty();
            playerState.SaveNow();
            if (hud != null) hud.Refresh();
            SetStatus("");
            Refresh();
            if (popup != null) popup.Show(id);
        }

        private void Refresh()
        {
            if (!HasCharacter() || _school < 0) return;
            CharacterData c = playerState.current;
            bool own = (int)c.school == _school;
            if (titleLabel != null) titleLabel.text = NameAt(professorNames, _school) + "\n" + NameAt(schoolNames, _school) + " Spells";
            if (pointsLabel != null)
                pointsLabel.text = "Training Points: " + c.trainingPoints.ToString() + (own ? "    (your school's spells are free)" : "    (1 point per spell)");

            for (int r = 0; r < ROWS; r++)
            {
                bool has = r < _count;
                if (rowRoots != null && r < rowRoots.Length && rowRoots[r] != null) rowRoots[r].SetActive(has);
                if (!has) continue;
                int id = _ids[r];
                // Spells you can't learn yet are greyed out; the selected one is highlighted.
                Image frame = rowIcons != null && r < rowIcons.Length ? rowIcons[r] : null;
                cardDatabase.FillCard(id, frame, rowNames[r], rowPips != null && r < rowPips.Length ? rowPips[r] : null,
                    rowArts != null && r < rowArts.Length ? rowArts[r] : null, null, null, null, !c.HasLearned(id) && !CanLearnNow(c, id));
                if (frame != null && id == _selected) frame.color = new Color(1f, 1f, 0.72f);
                rowInfo[r].text = RowStatus(c, id);
            }

            bool sel = _selected >= 0;
            if (detailsPanel != null) detailsPanel.SetActive(sel);
            if (!sel) return;
            detailName.text = cardDatabase.GetName(_selected);
            detailBody.text = cardDatabase.GetDetails(_selected);
            cardDatabase.FillCard(_selected, detailFrame, detailCardName, detailPips, detailArt, detailAccuracy, detailInfo, detailSchool, false);
            bool canTry = !c.HasLearned(_selected) && !cardDatabase.IsQuestSpell(_selected);
            if (learnButton != null) learnButton.SetActive(canTry);
            if (learnLabel != null)
                learnLabel.text = cardDatabase.LearnCost(c, _selected) == 0 ? "Learn (free)" : "Learn (1 point)";
        }

        /// <summary>Could be learned right now (level, previous spell and training points all OK).</summary>
        private bool CanLearnNow(CharacterData c, int id)
        {
            if (cardDatabase.IsQuestSpell(id) || c.level < cardDatabase.GetLevelReq(id)) return false;
            int pre = cardDatabase.GetPrereq(id);
            if (pre > 0 && !c.HasLearned(pre)) return false;
            return c.trainingPoints >= cardDatabase.LearnCost(c, id);
        }

        private string RowStatus(CharacterData c, int id)
        {
            if (c.HasLearned(id)) return "Learned";
            int req = cardDatabase.GetLevelReq(id);
            if (cardDatabase.IsQuestSpell(id)) return "Lv " + req.ToString() + " - Trial quest reward";
            if (c.level < req) return "Requires level " + req.ToString();
            int pre = cardDatabase.GetPrereq(id);
            if (pre > 0 && !c.HasLearned(pre)) return "Needs " + cardDatabase.GetName(pre);
            return cardDatabase.LearnCost(c, id) == 0 ? "Lv " + req.ToString() + " - Free" : "Lv " + req.ToString() + " - 1 Training Point";
        }

        private void SetStatus(string text)
        {
            if (statusLabel != null) statusLabel.text = text;
        }

        private bool HasCharacter()
        {
            return playerState != null && playerState.HasCharacter() && playerState.current != null;
        }

        private string NameAt(string[] names, int i)
        {
            return names != null && i >= 0 && i < names.Length ? names[i] : "";
        }
    }
}
