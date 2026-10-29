using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Local-only area culling: every big area (the castle hub, the boss arena, each street zone) is switched off for this
    /// player unless they are inside its bounds, so nothing of it renders, collides or runs its scripts. Nothing synced
    /// lives under these roots (battle managers, quest state and cutscenes are under Systems), so switching them off is safe.
    /// Teleports call <see cref="Prepare"/> first, so the destination's ground is there before the player lands.
    /// The keep interior (spawn) and the cutscene film sets are never culled.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AreaCuller : UdonSharpBehaviour
    {
        public GameObject[] areas;
        [Tooltip("World-space bounds of each area (same order as Areas).")]
        public Vector3[] boundsMin;
        public Vector3[] boundsMax;
        [Tooltip("Seconds between checks of where the player is.")]
        public float checkInterval = 0.25f;
        [Tooltip("After a teleport, nothing is switched off for this long (the player's position takes a moment to update).")]
        public float teleportHold = 1.5f;

        private float _next = 1f;
        private float _holdUntil;

        /// <summary>Switches on the area containing a position the player is about to be moved to.</summary>
        public void Prepare(Vector3 position)
        {
            _holdUntil = Time.time + teleportHold;
            if (areas == null) return;
            for (int i = 0; i < areas.Length; i++)
                if (Contains(i, position)) SetArea(i, true);
        }

        private bool Contains(int i, Vector3 p)
        {
            if (boundsMin == null || boundsMax == null || i >= boundsMin.Length || i >= boundsMax.Length) return true;
            Vector3 a = boundsMin[i], b = boundsMax[i];
            return p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z;
        }

        private void SetArea(int i, bool on)
        {
            GameObject go = areas[i];
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        private void Update()
        {
            if (Time.time < _next || areas == null) return;
            _next = Time.time + checkInterval;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            Vector3 p = local.GetPosition();
            bool hold = Time.time < _holdUntil;
            for (int i = 0; i < areas.Length; i++)
            {
                if (Contains(i, p)) SetArea(i, true);
                else if (!hold) SetArea(i, false);
            }
        }
    }
}
