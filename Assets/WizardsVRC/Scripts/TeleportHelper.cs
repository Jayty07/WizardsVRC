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
        [Tooltip("Zone roots switched on for the local player when this door is used (the zone being entered).")]
        public GameObject[] showZones;
        [Tooltip("Zone roots switched off for the local player when this door is used, so nothing of them renders.")]
        public GameObject[] hideZones;
        [Tooltip("Local area culling: the destination's area is switched on before the player is moved there.")]
        public AreaCuller culler;

        public void TeleportTo(Transform target)
        {
            if (target == null) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            if (culler != null) culler.Prepare(target.position);
            local.TeleportTo(target.position, target.rotation);
        }

        public void TeleportToDefault()
        {
            SwitchZones();
            TeleportTo(defaultTarget);
        }

        /// <summary>Street sets are unloaded locally while the player is elsewhere (see the World 1 plan).</summary>
        public void SwitchZones()
        {
            if (hideZones != null)
                for (int i = 0; i < hideZones.Length; i++) if (hideZones[i] != null) hideZones[i].SetActive(false);
            if (showZones != null)
                for (int i = 0; i < showZones.Length; i++) if (showZones[i] != null) showZones[i].SetActive(true);
        }

        public override void Interact()
        {
            TeleportToDefault();
        }
    }
}
