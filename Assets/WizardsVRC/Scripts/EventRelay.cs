using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>Interact (click/trigger) on this object sends <see cref="eventName"/> to <see cref="target"/>.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EventRelay : UdonSharpBehaviour
    {
        public UdonSharpBehaviour target;
        public string eventName = "";

        public override void Interact()
        {
            if (target != null && eventName != "") target.SendCustomEvent(eventName);
        }
    }
}
