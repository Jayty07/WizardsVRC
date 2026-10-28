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
        public Text marker;

        private float _next;

        public override void Interact()
        {
            if (questManager != null) questManager.TalkNpc(npcId);
        }

        private void Update()
        {
            if (marker == null || questManager == null || Time.time < _next) return;
            _next = Time.time + 1f;
            int m = questManager.NpcMarker(npcId);
            string t = m == QuestManager.MARKER_NEW ? "!" : (m == QuestManager.MARKER_TALK ? "?" : "");
            if (marker.text != t) marker.text = t;
        }
    }
}
