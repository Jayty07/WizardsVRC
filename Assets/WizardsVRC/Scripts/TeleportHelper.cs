using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>Moves the local player to a target transform. Optionally works as an interactable pad.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TeleportHelper : UdonSharpBehaviour
    {
        [Tooltip("Default destination used by Interact() and TeleportToDefault().")]
        public Transform defaultTarget;

        public void TeleportTo(Transform target)
        {
            if (target == null) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            local.TeleportTo(target.position, target.rotation);
        }

        public void TeleportToDefault()
        {
            TeleportTo(defaultTarget);
        }

        public override void Interact()
        {
            TeleportToDefault();
        }
    }
}
