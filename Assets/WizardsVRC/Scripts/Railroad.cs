using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Dustwind's railroad (fast travel): Archrock's depot sells rides to every street whose whistle stop you've reached
    /// (DepotStop, by the street's way in; CharacterData.depotBits). A ride is a few seconds in a passenger car with the
    /// desert rolling past the windows, then you step off at the stop. Any whistle stop can take you back. Local: every
    /// player rides on their own.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Railroad : UdonSharpBehaviour
    {
        public const int MAX_STOPS = 8;

        public PlayerState playerState;
        public NotificationCenter notifications;
        public AreaCuller culler;
        public UiAudio uiAudio;
        [Tooltip("Stop names (0 = Archrock, the depot in town; then the streets).")]
        public string[] stopNames;
        [Tooltip("Where you step off at each stop (same order).")]
        public Transform[] stopArrivals;

        [Header("The ride")]
        [Tooltip("Inside the passenger car (a set far away that's never switched off).")]
        public Transform rideSpot;
        public float rideSeconds = 6f;
        public AudioSource whistle;

        [Header("Timetable (the depot panel)")]
        public GameObject panelRoot;
        public Text titleLabel;
        public Text statusLabel;
        public GameObject[] rowRoots;
        public Text[] rowLabels;
        public float openDistance = 1.3f;

        private int _dest = -1;
        private bool _riding;
        private int _from;

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private int Count() { return stopNames == null ? 0 : Mathf.Min(stopNames.Length, MAX_STOPS); }

        /// <summary>True once the stop has been reached (Archrock always).</summary>
        public bool Reached(int i)
        {
            if (i == 0) return true;
            if (playerState == null || !playerState.HasCharacter()) return false;
            return (playerState.current.depotBits & (1 << i)) != 0;
        }

        /// <summary>DepotStop: you walked past a street's whistle stop.</summary>
        public void Reach(int i)
        {
            if (i <= 0 || i >= Count() || playerState == null || !playerState.HasCharacter() || Reached(i)) return;
            playerState.current.depotBits |= 1 << i;
            playerState.MarkDirty();
            if (notifications != null)
                notifications.Post(NotificationCenter.KIND_INFO, "Whistle stop: " + stopNames[i],
                    "You can ride the train here from Archrock's depot now, and back from here to town.", 0);
        }

        /// <summary>Clicking a depot or whistle stop: the timetable, in front of you.</summary>
        public void OpenAt(int from)
        {
            if (_riding || playerState == null || !playerState.HasCharacter()) return;
            _from = from;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && panelRoot != null)
            {
                VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 f = head.rotation * Vector3.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
                f.Normalize();
                panelRoot.transform.SetPositionAndRotation(head.position + f * openDistance + Vector3.down * 0.1f, Quaternion.LookRotation(f, Vector3.up));
            }
            if (panelRoot != null) panelRoot.SetActive(true);
            if (uiAudio != null) uiAudio.PlayBookOpen();
            if (statusLabel != null) statusLabel.text = "";
            Draw();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Draw()
        {
            if (titleLabel != null) titleLabel.text = (_from >= 0 && _from < Count() ? stopNames[_from] : "") + " Depot\nWhere to, partner?";
            if (rowRoots == null) return;
            for (int i = 0; i < rowRoots.Length; i++)
            {
                bool show = i < Count() && i != _from;
                if (rowRoots[i] != null) rowRoots[i].SetActive(show);
                if (!show || rowLabels == null || i >= rowLabels.Length || rowLabels[i] == null) continue;
                rowLabels[i].text = Reached(i) ? stopNames[i] : stopNames[i] + "  (not reached yet)";
                rowLabels[i].color = Reached(i) ? new Color(0.2f, 0.12f, 0.05f) : new Color(0.45f, 0.4f, 0.35f);
            }
        }

        public void Ride0() { Ride(0); }
        public void Ride1() { Ride(1); }
        public void Ride2() { Ride(2); }
        public void Ride3() { Ride(3); }
        public void Ride4() { Ride(4); }
        public void Ride5() { Ride(5); }
        public void Ride6() { Ride(6); }
        public void Ride7() { Ride(7); }

        private void Ride(int i)
        {
            if (_riding || i < 0 || i >= Count() || stopArrivals == null || i >= stopArrivals.Length || stopArrivals[i] == null) return;
            if (!Reached(i))
            {
                if (statusLabel != null) statusLabel.text = "You haven't reached " + stopNames[i] + "'s whistle stop yet. Walk there once first.";
                if (uiAudio != null) uiAudio.PlayCancel();
                return;
            }
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Close();
            if (uiAudio != null) uiAudio.PlayOk();
            _dest = i;
            _riding = true;
            if (rideSpot != null)
            {
                if (culler != null) culler.Prepare(rideSpot.position);
                me.TeleportTo(rideSpot.position, rideSpot.rotation);
                if (whistle != null) whistle.Play();
                SendCustomEventDelayedSeconds(nameof(_Arrive), rideSeconds);
            }
            else _Arrive();
        }

        public void _Arrive()
        {
            _riding = false;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || _dest < 0 || _dest >= Count()) return;
            Transform t = stopArrivals[_dest];
            if (culler != null) culler.Prepare(t.position);
            me.TeleportTo(t.position, t.rotation);
            if (notifications != null) notifications.Post(NotificationCenter.KIND_INFO, "All off for " + stopNames[_dest] + "!", "", 0);
            _dest = -1;
        }
    }
}
