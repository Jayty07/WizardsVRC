using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>Ring the bell: every tavern mug goes back where it started (for everyone, through its VRCObjectSync).</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MugReset : UdonSharpBehaviour
    {
        public GameObject[] mugs;
        public AudioSource bell;

        public override void Interact()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || mugs == null) return;
            if (bell != null) bell.Play();
            foreach (GameObject m in mugs)
            {
                if (m == null) continue;
                VRCObjectSync sync = m.GetComponent<VRCObjectSync>();
                if (sync == null) continue;
                Networking.SetOwner(me, m);
                sync.Respawn();
            }
        }
    }
}
