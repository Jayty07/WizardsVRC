using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's notices (local): new gear and loot, a word from your professor when you reach the level for a new
    /// spell, and quests that open up for you (main quests and special side quests). A red badge on the HUD and on the
    /// book's Notices tab shows how many you haven't read; the Notices page lists the latest ones, newest first.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NotificationCenter : UdonSharpBehaviour
    {
        public const int KIND_GEAR = 0;
        public const int KIND_SPELL = 1;
        public const int KIND_QUEST = 2;
        public const int KIND_INFO = 3;
        public const int KIND_SHOP = 4;
        public const int MAX = 24;

        public PlayerState playerState;
        public CardDatabase cardDatabase;
        public QuestManager questManager;
        public UiAudio uiAudio;

        [Header("Badges (the unread count; hidden at 0)")]
        public GameObject[] badges;
        public Text[] badgeCounts;

        [Header("Notices page (rows, newest first)")]
        public GameObject[] rows;
        public Text[] rowTitles;
        public Text[] rowBodies;
        public Image[] rowIcons;
        public Text[] rowIconGlyphs;
        public Text emptyLabel;
        [Tooltip("Icon tint per kind: gear, spell, quest, info, shop.")]
        public Color[] kindColors;
        [Tooltip("Pixel-font glyph per kind.")]
        public string[] kindGlyphs;
        [Tooltip("The same colours as rich-text hex (#RRGGBB).")]
        public string[] kindHex;

        [Header("Toast: a short line on the HUD when a notice arrives")]
        public GameObject toast;
        public Text toastText;
        public float toastSeconds = 4f;

        private int[] _kinds = new int[MAX];
        private string[] _titles = new string[MAX];
        private string[] _bodies = new string[MAX];
        private int _count;
        private int _unread;
        private float _toastUntil;
        private int _slot = -1;
        private float _nextCheck;
        private bool[] _questSeen;
        private bool _questsPrimed;

        /// <summary>Adds a notice (newest first; the oldest drops off past MAX).</summary>
        public void Post(int kind, string title, string body, int itemId)
        {
            for (int i = Mathf.Min(_count, MAX - 1); i > 0; i--)
            {
                _kinds[i] = _kinds[i - 1];
                _titles[i] = _titles[i - 1];
                _bodies[i] = _bodies[i - 1];
            }
            _kinds[0] = kind;
            _titles[0] = title;
            _bodies[0] = body;
            if (_count < MAX) _count++;
            _unread = Mathf.Min(MAX, _unread + 1);
            if (uiAudio != null) uiAudio.PlayPopup();
            if (toast != null)
            {
                if (toastText != null) toastText.text = Glyph(kind) + " " + title + (body.Length > 0 && body.Length < 48 ? ": " + body : "");
                toast.SetActive(true);
                _toastUntil = Time.time + toastSeconds;
            }
            UpdateBadges();
        }

        public int Unread() { return _unread; }

        /// <summary>The book's Notices page opened: everything is read now.</summary>
        public void MarkAllRead()
        {
            _unread = 0;
            UpdateBadges();
            DrawPage();
        }

        private string Glyph(int kind)
        {
            string g = kindGlyphs != null && kind >= 0 && kind < kindGlyphs.Length ? kindGlyphs[kind] : "!";
            if (kindHex == null || kind < 0 || kind >= kindHex.Length) return g;
            return "<color=" + kindHex[kind] + ">" + g + "</color>";
        }

        private void UpdateBadges()
        {
            bool on = _unread > 0;
            if (badges != null)
                for (int i = 0; i < badges.Length; i++)
                    if (badges[i] != null && badges[i].activeSelf != on) badges[i].SetActive(on);
            if (badgeCounts != null)
                for (int i = 0; i < badgeCounts.Length; i++)
                    if (badgeCounts[i] != null) badgeCounts[i].text = _unread > 9 ? "9+" : _unread.ToString();
        }

        public void DrawPage()
        {
            int n = rows != null ? rows.Length : 0;
            for (int i = 0; i < n; i++)
            {
                bool has = i < _count;
                if (rows[i] != null && rows[i].activeSelf != has) rows[i].SetActive(has);
                if (!has) continue;
                if (rowTitles != null && i < rowTitles.Length && rowTitles[i] != null) rowTitles[i].text = _titles[i];
                if (rowBodies != null && i < rowBodies.Length && rowBodies[i] != null) rowBodies[i].text = _bodies[i];
                if (rowIconGlyphs != null && i < rowIconGlyphs.Length && rowIconGlyphs[i] != null)
                    rowIconGlyphs[i].text = kindGlyphs != null && _kinds[i] < kindGlyphs.Length ? kindGlyphs[_kinds[i]] : "!";
                if (rowIcons != null && i < rowIcons.Length && rowIcons[i] != null && kindColors != null && _kinds[i] < kindColors.Length)
                    rowIcons[i].color = kindColors[_kinds[i]];
            }
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(_count == 0);
        }

        private void Update()
        {
            if (toast != null && toast.activeSelf && Time.time > _toastUntil) toast.SetActive(false);
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 1.5f;
            if (playerState == null || !playerState.HasCharacter()) { _slot = -1; return; }
            if (_slot != playerState.activeSlot)
            {
                // A different character: start fresh.
                _slot = playerState.activeSlot;
                _count = 0;
                _unread = 0;
                _questsPrimed = false;
                UpdateBadges();
            }
            CheckSpells();
            CheckQuests();
        }

        /// <summary>
        /// Reaching the level for one of your school's spells: a word from your professor (once per level, saved).
        /// </summary>
        private void CheckSpells()
        {
            CharacterData c = playerState.current;
            if (cardDatabase == null || c.level <= c.spellNoticeLevel) return;
            int school = (int)c.school;
            string names = "";
            int found = 0;
            for (int i = 0; i < cardDatabase.Count(); i++)
            {
                int id = cardDatabase.IdAt(i);
                if (cardDatabase.GetSchool(id) != school || cardDatabase.IsQuestSpell(id) || cardDatabase.IsTrialSpell(id)) continue;
                int req = cardDatabase.GetLevelReq(id);
                if (req > c.spellNoticeLevel && req <= c.level && !c.HasLearned(id))
                {
                    names += (found > 0 ? ", " : "") + cardDatabase.GetName(id);
                    found++;
                }
            }
            c.spellNoticeLevel = c.level;
            playerState.MarkDirty();
            if (found == 0) return;
            string prof = questManager != null && questManager.professorNames != null && school < questManager.professorNames.Length
                ? questManager.professorNames[school] : "Your professor";
            Post(KIND_SPELL, prof, "\"You've grown strong enough to learn " + names + ". Come and see me at the " +
                 cardDatabase.SchoolName(school) + " school!\"", 0);
        }

        /// <summary>Main and special side quests that open up for you while you play (not ones already open when you arrived).</summary>
        private void CheckQuests()
        {
            if (questManager == null) return;
            int n = questManager.QuestCount();
            if (_questSeen == null || _questSeen.Length != n) { _questSeen = new bool[n]; _questsPrimed = false; }
            for (int q = 0; q < n; q++)
            {
                bool open = questManager.Available(q) && !questManager.IsStarted(q);
                if (open && !_questSeen[q] && _questsPrimed)
                    Post(KIND_QUEST, "New quest: " + questManager.GetTitle(q), questManager.GetDescription(q), 0);
                _questSeen[q] = open || questManager.IsStarted(q);
            }
            _questsPrimed = true;
        }
    }
}
