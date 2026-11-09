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

        [Header("Areas (keep interior, zones): each is a height band reached through a door from the castle")]
        public float[] areaMinY;
        public float[] areaMaxY;
        [Tooltip("Optional, per area: its ground rectangle (x, z). An open world's districts share a height band, so they're told " +
                 "apart by where they lie; leave empty (or min > max) to go by height alone.")]
        public Vector2[] areaMinXZ;
        public Vector2[] areaMaxXZ;
        [Tooltip("Door in the castle that leads into the area.")]
        public Transform[] areaEntrances;
        [Tooltip("Door inside the area that leads back to the castle.")]
        public Transform[] areaExits;
        public string[] areaEntranceNames;
        public string[] areaExitNames;
        [Tooltip("Area each area's entrance stands in (-1 = the castle). A later world's streets are entered from its town, " +
                 "and the town from the portal room, so the arrow leads you there step by step.")]
        public int[] areaParents;

        [Header("Legacy single interior (used only when no areas are set)")]
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

        private float _nextFind;
        private bool _hasGoal;
        private Vector3 _goal;
        private string _goalName = "";
        private int _shownMetres = -1;
        private string _shownName = "";

        private void Update()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            // Where to point is worked out a few times a second (finding the nearest quest object and the way between
            // areas walks a lot of data); the arrow itself follows your head every frame.
            if (Time.time >= _nextFind)
            {
                _nextFind = Time.time + 0.25f;
                _hasGoal = FindGoal(local);
            }
            if (!_hasGoal)
            {
                Show(arrow, false);
                Show(labelRoot, false);
                Show(beacon, false);
                return;
            }
            Vector3 feet = local.GetPosition();
            Vector3 goal = _goal;
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
            // The words only change when the metres do.
            int metres = Mathf.RoundToInt(dist);
            if (label != null && (metres != _shownMetres || _goalName != _shownName))
            {
                _shownMetres = metres;
                _shownName = _goalName;
                label.text = _goalName + "\n" + metres.ToString() + " m";
            }
        }

        /// <summary>The point the arrow leads to right now (the target, or the door on the way to its area). False = none.</summary>
        private bool FindGoal(VRCPlayerApi local)
        {
            Transform target = FindTarget();
            if (target == null) return false;
            int q = questManager.GetTrackedQuest();
            Vector3 goal = target.position;
            string goalName = questManager.GetObjectiveTargetName(q);
            Vector3 feet = local.GetPosition();
            if (areaMinY != null && areaMinY.Length > 0)
            {
                // Different areas: leave yours through its exit, or (from the castle) head for the goal area's door.
                int pa = AreaOf(feet);
                int ga = AreaOf(goal);
                if (pa != ga)
                {
                    // The goal's area, or the one on the way to it, whose entrance is in your area: head for that door.
                    // Otherwise leave yours (its exit leads back toward the castle).
                    int step = ga;
                    int guard = 0;
                    while (step >= 0 && ParentOf(step) != pa && guard < 8) { step = ParentOf(step); guard++; }
                    if (step >= 0 && ParentOf(step) == pa && Has(areaEntrances, step)) { goal = areaEntrances[step].position; goalName = NameAt(areaEntranceNames, step); }
                    else if (pa >= 0 && Has(areaExits, pa)) { goal = areaExits[pa].position; goalName = NameAt(areaExitNames, pa); }
                    else if (ga >= 0 && Has(areaEntrances, ga)) { goal = areaEntrances[ga].position; goalName = NameAt(areaEntranceNames, ga); }
                }
            }
            else
            {
                bool playerInside = feet.y > interiorMinY;
                bool goalInside = goal.y > interiorMinY;
                if (playerInside && !goalInside && interiorExit != null) { goal = interiorExit.position; goalName = interiorExitName; }
                else if (!playerInside && goalInside && keepEntrance != null) { goal = keepEntrance.position; goalName = keepEntranceName; }
            }

            _goal = goal;
            _goalName = goalName;
            return true;
        }

        private Transform FindTarget()
        {
            if (!guideEnabled || questManager == null || playerState == null || !playerState.HasCharacter()) return null;
            if (cutscenePlayer != null && cutscenePlayer.IsPlaying()) return null;
            // In a fight (asking each of the world's rings would be hundreds of calls).
            if (playerState.inCombat) return null;
            int q = questManager.GetTrackedQuest();
            if (q == CharacterData.NO_TRACKED_QUEST) return null;
            return questManager.GetObjectiveTarget(q);
        }

        private int ParentOf(int area)
        {
            return areaParents != null && area >= 0 && area < areaParents.Length ? areaParents[area] : -1;
        }

        /// <summary>Index of the area containing a position (its height band, and its ground rectangle if it has one), or -1 for the castle.</summary>
        private int AreaOf(Vector3 p)
        {
            for (int i = 0; i < areaMinY.Length; i++)
            {
                if (areaMaxY == null || i >= areaMaxY.Length || p.y < areaMinY[i] || p.y >= areaMaxY[i]) continue;
                if (areaMinXZ != null && areaMaxXZ != null && i < areaMinXZ.Length && i < areaMaxXZ.Length && areaMinXZ[i].x <= areaMaxXZ[i].x &&
                    (p.x < areaMinXZ[i].x || p.x > areaMaxXZ[i].x || p.z < areaMinXZ[i].y || p.z > areaMaxXZ[i].y)) continue;
                return i;
            }
            return -1;
        }

        private bool Has(Transform[] arr, int i) { return arr != null && i >= 0 && i < arr.Length && arr[i] != null; }

        private string NameAt(string[] arr, int i) { return arr != null && i >= 0 && i < arr.Length ? arr[i] : ""; }

        private void Show(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }
    }
}
