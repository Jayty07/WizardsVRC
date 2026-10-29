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

        private float _next;

        public override void Interact()
        {
            if (questManager != null) questManager.TalkNpc(npcId);
        }

        private void Update()
        {
            if (questManager == null || Time.time < _next) return;
            _next = Time.time + 1f;
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
