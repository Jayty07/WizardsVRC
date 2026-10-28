using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Small head-following panel showing name, level, health, mana and gold of the active character.
    /// Lives on an always-active object; <see cref="hudRoot"/> is the canvas that gets shown/hidden.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerHUD : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public GameObject hudRoot;
        public Text nameLabel;
        public Text healthLabel;
        public Text manaLabel;
        public Text goldLabel;
        public QuestManager questManager;
        public Text questLabel;

        [Tooltip("Offset from the head in head space (x right, y up, z forward), metres.")]
        public Vector3 headOffset = new Vector3(-0.28f, -0.22f, 0.7f);
        public float followSpeed = 8f;
        public bool hudEnabled = true;

        private float _nextRefresh;

        public void ToggleHud()
        {
            hudEnabled = !hudEnabled;
        }

        private void Update()
        {
            bool show = hudEnabled && playerState != null && playerState.HasCharacter();
            if (hudRoot != null && hudRoot.activeSelf != show) hudRoot.SetActive(show);
            if (!show) return;

            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 targetPos = head.position + head.rotation * headOffset;
            Quaternion targetRot = head.rotation;
            float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
            Transform tr = hudRoot.transform;
            tr.position = Vector3.Lerp(tr.position, targetPos, t);
            tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, t);

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 0.5f;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            if (nameLabel != null) nameLabel.text = c.characterName + "  Lv " + c.level.ToString();
            if (healthLabel != null) healthLabel.text = "Health " + playerState.DisplayHealth().ToString() + " / " + playerState.MaxHealth().ToString();
            if (manaLabel != null) manaLabel.text = "Mana " + playerState.GetMana().ToString() + " / " + playerState.MaxMana().ToString();
            if (goldLabel != null) goldLabel.text = "Gold " + c.gold.ToString() + "    Training Points " + c.trainingPoints.ToString();
            if (questLabel != null) questLabel.text = questManager != null ? questManager.GetTrackerText() : "";
        }
    }
}
