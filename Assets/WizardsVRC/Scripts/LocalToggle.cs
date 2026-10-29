using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>Click to switch objects on or off for yourself only (mirrors, lights, music...).</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LocalToggle : UdonSharpBehaviour
    {
        public GameObject[] targets;

        public override void Interact()
        {
            if (targets == null) return;
            foreach (GameObject t in targets)
                if (t != null) t.SetActive(!t.activeSelf);
        }
    }
}
