using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The portcullis at a tunnel mouth in the castle: down (blocking the way) until the local player has finished the
    /// tunnel's quest, like Wizard101's streets opening as the story reaches them. Purely local.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TunnelGate : UdonSharpBehaviour
    {
        public QuestManager questManager;
        [Tooltip("Save id of the quest to finish first (-1 = always open).")]
        public int requiredQuest = -1;
        [Tooltip("Also open once the player has started any of these quests (quests with steps beyond this tunnel), so " +
                 "saves from before the tunnel existed aren't locked out.")]
        public int[] openIfStarted;
        [Tooltip("The portcullis (with its collider): shown while locked.")]
        public GameObject barrier;
        public Text label;
        public string openText = "";
        public string lockedText = "Locked";

        private float _next;

        public bool IsOpen()
        {
            if (requiredQuest < 0) return true;
            if (questManager == null) return false;
            if (questManager.IsComplete(requiredQuest)) return true;
            if (openIfStarted != null)
                for (int i = 0; i < openIfStarted.Length; i++) if (questManager.IsStarted(openIfStarted[i])) return true;
            return false;
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 1f;
            bool open = IsOpen();
            if (barrier != null && barrier.activeSelf == open) barrier.SetActive(!open);
            if (label != null)
            {
                string t = open ? openText : lockedText;
                if (label.text != t) label.text = t;
            }
        }
    }
}
