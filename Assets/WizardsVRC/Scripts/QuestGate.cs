using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// A door to another zone that stays shut until the local player has finished a quest (Wizard101's streets open
    /// as the story reaches them). Clicking it uses <see cref="teleporter"/> when open, or shows why it's locked.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestGate : UdonSharpBehaviour
    {
        public QuestManager questManager;
        public PlayerState playerState;
        [Tooltip("Save id of the quest to finish first (-1 = always open).")]
        public int requiredQuest = -1;
        [Tooltip("Also open once the player has started any of these quests (quests with steps behind this gate), so saves " +
                 "from before the gate existed aren't locked out.")]
        public int[] openIfStarted;
        public TeleportHelper teleporter;
        public Text label;
        public string openText = "";
        public string lockedText = "Locked";

        private float _next;
        private float _flashUntil;

        public bool IsOpen()
        {
            if (requiredQuest < 0) return true;
            if (questManager == null) return false;
            if (questManager.IsComplete(requiredQuest)) return true;
            if (openIfStarted != null)
                for (int i = 0; i < openIfStarted.Length; i++) if (questManager.IsStarted(openIfStarted[i])) return true;
            return false;
        }

        public override void Interact()
        {
            if (playerState != null && playerState.inCombat) return;
            if (IsOpen())
            {
                if (teleporter != null) teleporter.TeleportToDefault();
                return;
            }
            _flashUntil = Time.time + 3f;
            if (label != null) label.text = lockedText + "\n(not yet!)";
        }

        private void Update()
        {
            if (label == null || Time.time < _next || Time.time < _flashUntil) return;
            _next = Time.time + 1f;
            string t = IsOpen() ? openText : lockedText;
            if (label.text != t) label.text = t;
        }
    }
}
