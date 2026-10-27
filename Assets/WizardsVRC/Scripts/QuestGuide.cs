using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Points the local player at the tracked quest's objective: an arrow floating in front of them with the name and
    /// distance, plus a light beam on the target. If the target is in the other area (keep interior vs. castle), it
    /// guides to the connecting door instead. Hidden during cutscenes and battles.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class QuestGuide : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public QuestManager questManager;
        public CutscenePlayer cutscenePlayer;
        [Tooltip("Every encounter's client; the guide hides while the player is in any fight.")]
        public BattleClient[] battleClients;

        public Transform arrow;
        public Transform labelRoot;
        public Text label;
        public Transform beacon;

        [Header("Areas")]
        [Tooltip("Anything above this height is inside the keep interior.")]
        public float interiorMinY = 100f;
        public Transform interiorExit;
        public string interiorExitName = "Door to the Courtyard";
        public Transform keepEntrance;
        public string keepEntranceName = "Keep door (Hall of Schools)";

        public bool guideEnabled = true;
        public float arriveDistance = 3f;
        public float arrowDistance = 0.8f;
        public float arrowDrop = 0.45f;
        public float followSpeed = 10f;

        public void ToggleGuide() { guideEnabled = !guideEnabled; }
        public bool IsGuideEnabled() { return guideEnabled; }

        private void Start()
        {
            Show(arrow, false);
            Show(labelRoot, false);
            Show(beacon, false);
        }

        private void Update()
        {
            Transform target = FindTarget();
            VRCPlayerApi local = Networking.LocalPlayer;
            if (target == null || !Utilities.IsValid(local))
            {
                Show(arrow, false);
                Show(labelRoot, false);
                Show(beacon, false);
                return;
            }

            int q = questManager.GetTrackedQuest();
            Vector3 goal = target.position;
            string goalName = questManager.GetObjectiveTargetName(q);
            Vector3 feet = local.GetPosition();
            bool playerInside = feet.y > interiorMinY;
            bool goalInside = goal.y > interiorMinY;
            if (playerInside && !goalInside && interiorExit != null) { goal = interiorExit.position; goalName = interiorExitName; }
            else if (!playerInside && goalInside && keepEntrance != null) { goal = keepEntrance.position; goalName = keepEntranceName; }

            if (beacon != null)
            {
                Show(beacon, true);
                beacon.position = goal;
            }

            float dist = Vector3.Distance(feet, goal);
            bool near = dist < arriveDistance;
            Show(arrow, !near);
            Show(labelRoot, !near);
            if (near) return;

            VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 forward = head.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 toGoal = goal - head.position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.001f) toGoal = forward;

            float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
            Vector3 pos = head.position + forward * arrowDistance + Vector3.down * arrowDrop;
            arrow.position = Vector3.Lerp(arrow.position, pos, t);
            arrow.rotation = Quaternion.Slerp(arrow.rotation, Quaternion.LookRotation(toGoal.normalized, Vector3.up), t);
            if (labelRoot != null)
            {
                labelRoot.position = arrow.position + Vector3.up * 0.12f;
                labelRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            if (label != null) label.text = goalName + "\n" + Mathf.RoundToInt(dist).ToString() + " m";
        }

        private Transform FindTarget()
        {
            if (!guideEnabled || questManager == null || playerState == null || !playerState.HasCharacter()) return null;
            if (cutscenePlayer != null && cutscenePlayer.IsPlaying()) return null;
            if (battleClients != null)
                for (int i = 0; i < battleClients.Length; i++)
                    if (battleClients[i] != null && battleClients[i].IsInBattle()) return null;
            int q = questManager.GetTrackedQuest();
            if (q == CharacterData.NO_TRACKED_QUEST) return null;
            return questManager.GetObjectiveTarget(q);
        }

        private void Show(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }
    }
}
