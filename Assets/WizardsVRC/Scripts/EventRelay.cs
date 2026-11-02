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

        [Tooltip("The NPC's body: it nods and gestures (its Talk trigger) when you click it.")]
        public Animator talkAnimator;

        public override void Interact()
        {
            if (talkAnimator != null) talkAnimator.SetTrigger("Talk");
            if (target != null && eventName != "") target.SendCustomEvent(eventName);
        }
    }
}
