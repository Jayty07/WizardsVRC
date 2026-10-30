using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// A quest NPC placed by a zone (see Assets/WizardsVRC/WorldData). Clicking talks to them through
    /// <see cref="QuestManager.TalkNpc"/>, which picks the right dialogue for the player's quests. A floating marker
    /// shows "!" when they have a new quest for you and "?" when one of your quests wants you to talk to them.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestNpc : UdonSharpBehaviour
    {
        public QuestManager questManager;
        public int npcId;
        [Tooltip("Shown when this NPC has a new quest for you (a billboard \"!\").")]
        public GameObject markerNew;
        [Tooltip("Shown when one of your quests wants you to talk to this NPC (a billboard \"?\").")]
        public GameObject markerTalk;
        [Tooltip("Optional text marker (older scenes): set to \"!\" or \"?\".")]
        public Text marker;
        [Tooltip("Only here once this quest is started or done (-1 = always), e.g. a herald who arrives for the finale.")]
        public int showFromQuest = -1;
        [Tooltip("Gone once this quest is done (-1 = never), e.g. a spy who flees.")]
        public int hideAfterQuest = -1;
        [Tooltip("The NPC's visible parts (hidden while away).")]
        public GameObject[] parts;
        public Collider clickCollider;

        private int _shown = -1;

        private float _next;

        public override void Interact()
        {
            if (questManager != null && Present()) questManager.TalkNpc(npcId);
        }

        /// <summary>Whether the NPC is here for this player's story so far.</summary>
        public bool Present()
        {
            if (questManager == null) return true;
            if (showFromQuest >= 0 && !questManager.IsStarted(showFromQuest)) return false;
            if (hideAfterQuest >= 0 && questManager.IsComplete(hideAfterQuest)) return false;
            return true;
        }

        private void Update()
        {
            if (questManager == null || Time.time < _next) return;
            _next = Time.time + 1f;
            bool here = Present();
            if ((here ? 1 : 0) != _shown)
            {
                _shown = here ? 1 : 0;
                if (parts != null)
                    for (int i = 0; i < parts.Length; i++)
                        if (parts[i] != null) parts[i].SetActive(here);
                if (clickCollider != null) clickCollider.enabled = here;
            }
            if (!here)
            {
                if (markerNew != null && markerNew.activeSelf) markerNew.SetActive(false);
                if (markerTalk != null && markerTalk.activeSelf) markerTalk.SetActive(false);
                return;
            }
            int m = questManager.NpcMarker(npcId);
            bool isNew = m == QuestManager.MARKER_NEW, isTalk = m == QuestManager.MARKER_TALK;
            if (markerNew != null && markerNew.activeSelf != isNew) markerNew.SetActive(isNew);
            if (markerTalk != null && markerTalk.activeSelf != isTalk) markerTalk.SetActive(isTalk);
            if (marker != null)
            {
                string t = isNew ? "!" : (isTalk ? "?" : "");
                if (marker.text != t) marker.text = t;
            }
        }
    }
}
