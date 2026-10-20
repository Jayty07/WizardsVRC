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

        private NpcInteractions _npc;
        private Vector3 _openPos;

        public void Option0() { Pick(0); }
        public void Option1() { Pick(1); }
        public void Option2() { Pick(2); }
        public void Option3() { Pick(3); }

        private void Start()
        {
            if (menuRoot != null) menuRoot.SetActive(false);
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
                menuRoot.transform.position = head.position + forward * distance + Vector3.down * 0.2f;
                menuRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                _openPos = local.GetPosition();
            }
            menuRoot.SetActive(true);
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
