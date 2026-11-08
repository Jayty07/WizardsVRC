using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>Shared "what do you want to do?" menu for NPCs with several interactions (see <see cref="NpcInteractions"/>).</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InteractionMenu : UdonSharpBehaviour
    {
        public const int MAX_OPTIONS = 4;

        public GameObject menuRoot;
        public Text titleLabel;
        public GameObject[] optionButtons;
        public Text[] optionLabels;
        public float distance = 1.2f;
        public float closeDistance = 4f;

        [Tooltip("In VR the menu opens at most this far away (m), within easy reach of a controller.")]
        public float vrDistance = 0.7f;

        private NpcInteractions _npc;
        private Vector3 _openPos;

        public void Option0() { Pick(0); }
        public void Option1() { Pick(1); }
        public void Option2() { Pick(2); }
        public void Option3() { Pick(3); }

        private Vector3 _baseScale = Vector3.zero;

        private void Start()
        {
            if (menuRoot != null) { _baseScale = menuRoot.transform.localScale; menuRoot.SetActive(false); }
        }

        public void Open(NpcInteractions npc)
        {
            if (npc == null || menuRoot == null) return;
            _npc = npc;
            if (titleLabel != null) titleLabel.text = npc.title;
            int n = npc.OptionCount();
            for (int i = 0; i < MAX_OPTIONS; i++)
            {
                bool has = i < n;
                optionButtons[i].SetActive(has);
                if (has) optionLabels[i].text = npc.LabelAt(i);
            }

            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                menuRoot.SetActive(false);   // not in its own way
                float d = ClearDistance(local, head.position, forward, distance);
                menuRoot.transform.position = head.position + forward * d + Vector3.down * 0.2f * d / distance;
                // Nearer, smaller: it looks the same size wherever it ends up.
                if (_baseScale == Vector3.zero) _baseScale = menuRoot.transform.localScale;
                menuRoot.transform.localScale = _baseScale * (d / Mathf.Max(0.1f, distance));
                menuRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                _openPos = local.GetPosition();
            }
            menuRoot.SetActive(true);
        }


        // Everything solid except players, UI and walk-through layers.
        private const int MenuBlockMask = ~((1 << 2) | (1 << 5) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 17) | (1 << 18));

        /// <summary>
        /// How far ahead of your head a panel can float with nothing in the way: a table, a wall or the person you're
        /// talking to between you and it blocks a VR controller's pointer (and in VR it starts closer, within easy reach).
        /// Checked from your eyes and from hand height.
        /// </summary>
        private float ClearDistance(VRCPlayerApi local, Vector3 head, Vector3 forward, float want)
        {
            if (local.IsUserInVR()) want = Mathf.Min(want, vrDistance);
            float d = want;
            RaycastHit hit;
            if (Physics.SphereCast(head, 0.18f, forward, out hit, want + 0.3f, MenuBlockMask, QueryTriggerInteraction.Ignore)) d = Mathf.Min(d, hit.distance - 0.12f);
            Vector3 hands = head + Vector3.down * 0.55f;
            if (Physics.SphereCast(hands, 0.12f, forward, out hit, want + 0.3f, MenuBlockMask, QueryTriggerInteraction.Ignore)) d = Mathf.Min(d, hit.distance - 0.1f);
            return Mathf.Max(0.35f, d);
        }

        public void Close()
        {
            _npc = null;
            if (menuRoot != null) menuRoot.SetActive(false);
        }

        private void Update()
        {
            if (_npc == null) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && Vector3.Distance(local.GetPosition(), _openPos) > closeDistance) Close();
        }

        private void Pick(int i)
        {
            NpcInteractions npc = _npc;
            Close();
            if (npc != null) npc.Choose(i);
        }
    }
}
