using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A world object quests point at: click it (Interact step) or walk into its trigger (Explore step). Reports to
    /// <see cref="QuestManager"/> with its id; only the local player's progress changes.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestTarget : UdonSharpBehaviour
    {
        public QuestManager questManager;
        public int targetId;
        [Tooltip("True: walking into the trigger counts (Explore). False: clicking counts (Interact).")]
        public bool isTrigger;
        public Text label;
        [Tooltip("Switched on once this target has been used (e.g. a relit lamp's flame).")]
        public GameObject usedVisual;
        [Tooltip("Count only once per visit (clicking the same lamp twice doesn't count twice).")]
        public bool singleUse = true;

        private bool _used;

        public override void Interact()
        {
            if (isTrigger || questManager == null || (singleUse && _used)) return;
            _used = true;
            if (usedVisual != null) usedVisual.SetActive(true);
            questManager.OnInteract(targetId);
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (isTrigger && Utilities.IsValid(player) && player.isLocal && questManager != null) questManager.OnExplore(targetId);
        }
    }
}
