using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>"New spell!" popup: shows the learned card in front of the player until OK is pressed or it times out.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NewSpellPopup : UdonSharpBehaviour
    {
        public CardDatabase cardDatabase;
        public GameObject popupRoot;
        public Image cardFrame;
        public Text cardName;
        public Text cardSchool;
        public Text cardEffect;
        public Text cardPips;
        public Text cardAccuracy;
        public RawImage cardArt;
        public Text congratsLabel;
        public AudioSource chime;

        public float distance = 1.0f;
        public float autoHideSeconds = 10f;

        private int _token;

        private void Start()
        {
            if (popupRoot != null) popupRoot.SetActive(false);
        }

        public void Show(int cardId)
        {
            if (popupRoot == null || cardDatabase == null) return;
            cardDatabase.FillCard(cardId, cardFrame, cardName, cardPips, cardArt, cardAccuracy, cardEffect, cardSchool, false);
            congratsLabel.text = "Congratulations!\nYou learned " + cardDatabase.GetName(cardId) + "!\nAdd it to a deck from the Deck tab of your book (B).";

            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                // Slightly closer than the trainer panel so it sits in front of it.
                popupRoot.transform.position = head.position + forward * distance;
                popupRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            popupRoot.SetActive(true);
            if (chime != null) chime.Play();
            _token++;
            SendCustomEventDelayedSeconds(nameof(_AutoHide), autoHideSeconds);
        }

        public void _AutoHide()
        {
            _token--;
            if (_token <= 0) { _token = 0; Hide(); }
        }

        public void Hide()
        {
            if (popupRoot != null) popupRoot.SetActive(false);
        }
    }
}
