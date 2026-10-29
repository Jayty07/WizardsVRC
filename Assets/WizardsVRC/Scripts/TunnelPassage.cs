using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The far half of a Z-shaped tunnel's middle stretch. Walking into it moves you to the same spot, turned round, in
    /// the matching tunnel at the other end (the two tunnels are identical and the move happens out of sight between
    /// two bends), so you simply walk on and come out in the other place: no portal, no fade. Each tunnel's middle
    /// frame (<see cref="here"/>, <see cref="there"/>) sits at the middle of its middle stretch, +Z pointing along the
    /// stretch away from its own mouth. Purely local.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TunnelPassage : UdonSharpBehaviour
    {
        public Transform here;
        public Transform there;
        [Tooltip("Optional lock (the castle end's portcullis).")]
        public TunnelGate gate;
        [Tooltip("Switches the destination area on before the player is moved there.")]
        public AreaCuller culler;

        private float _blockUntil;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal || here == null || there == null) return;
            if (Time.time < _blockUntil) return;
            if (gate != null && !gate.IsOpen()) return;
            _blockUntil = Time.time + 0.5f;
            // Same place relative to the middle, turned half round: walking on along this tunnel becomes walking on
            // toward the other tunnel's mouth.
            Quaternion flip = Quaternion.Euler(0f, 180f, 0f);
            Vector3 local = here.InverseTransformPoint(player.GetPosition());
            Quaternion localRot = Quaternion.Inverse(here.rotation) * player.GetRotation();
            Vector3 pos = there.TransformPoint(flip * local);
            Quaternion rot = there.rotation * flip * localRot;
            if (culler != null) culler.Prepare(pos);
            player.TeleportTo(pos, rot, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, false);
        }
    }
}
