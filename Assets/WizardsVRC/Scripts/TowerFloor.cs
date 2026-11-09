using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// One floor of a boss tower (Wizard101's Golem Tower): a chamber with a duel ring and the wave that guards it. When
    /// you arrive (entering the tower, or coming up from the floor below), a cutscene plays: the wave's leader across the
    /// ring speaks to you, their portrait rimmed red (or, without a cutscene, a floating line with their portrait);
    /// run into one of them to fight. Win and you're taken up to the next floor (BattleClient.victoryReturn); the top
    /// floor's boss is the tower's last fight, and beating it marks the tower cleared on your wizard (titles, Starshards).
    /// Local: each player sees their own greeting.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TowerFloor : UdonSharpBehaviour
    {
        public BattleClient client;
        [Tooltip("The middle of the chamber (the ring).")]
        public Transform centre;
        public float radius = 16f;
        [Header("The leader's greeting")]
        public GameObject dialogRoot;
        public Text dialogName;
        public Text dialogLine;
        public string speaker = "";
        [TextArea(1, 4)] public string line = "";
        public float dialogSeconds = 9f;
        [Tooltip("The leader's cutscene, played when you arrive (falls back to the floating line without one).")]
        public Cutscene cutscene;
        public CutscenePlayer cutscenePlayer;
        [Tooltip("The leader standing across the ring until the fight starts (the duel shows its own bodies).")]
        public GameObject idleBody;
        [Tooltip("The floor's number shown to you on arrival.")]
        public string floorTitle = "";
        [Header("Top floor")]
        public bool isTop;
        public string towerName = "";
        [Tooltip("Bits set in CharacterData.towersCleared when the top floor is won (bit 0 = any tower).")]
        public int clearBits = 1;
        public int clearShards = 5;

        private bool _here;

        /// <summary>True while the leader's greeting is showing to this player (MusicDirector picks a theme by its speaker).</summary>
        public bool IsTalking()
        {
            if (dialogRoot != null && dialogRoot.activeSelf) return true;
            return cutscene != null && cutscenePlayer != null && cutscenePlayer.IsPlaying() && _cutsceneOn;
        }
        private bool _cutsceneOn;
        private float _shownAt = -100f;
        private float _next;

        private void Start()
        {
            if (dialogRoot != null) dialogRoot.SetActive(false);
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || centre == null) return;
            Vector3 d = me.GetPosition() - centre.position;
            bool here = Mathf.Abs(d.y) < 6f && d.x * d.x + d.z * d.z < radius * radius;
            BattleManager m = client != null ? client.manager : null;
            bool idle = m == null || m.phase == BattleManager.PHASE_IDLE || m.phase == BattleManager.PHASE_QUEUE;
            if (idleBody != null && idleBody.activeSelf != idle) idleBody.SetActive(idle);
            if (here && !_here && idle) Greet();
            _here = here;
            if (_cutsceneOn && (cutscenePlayer == null || !cutscenePlayer.IsPlaying())) _cutsceneOn = false;
            if (dialogRoot == null || !dialogRoot.activeSelf) return;
            if (!here || !idle || Time.time - _shownAt > dialogSeconds) { dialogRoot.SetActive(false); return; }
            // The greeting turns to face you (upright).
            Vector3 look = dialogRoot.transform.position - me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) dialogRoot.transform.rotation = Quaternion.LookRotation(look);
        }

        private void Greet()
        {
            // A cutscene of the leader speaking, unless something else is already playing.
            if (cutscene != null && cutscenePlayer != null)
            {
                if (cutscenePlayer.IsPlaying() || cutscenePlayer.IsFilming()) return;
                cutscenePlayer.Play(cutscene);
                _cutsceneOn = true;
                return;
            }
            if (dialogRoot == null) return;
            if (dialogName != null) dialogName.text = speaker + (floorTitle.Length > 0 ? "   <color=#C9B48A>" + floorTitle + "</color>" : "");
            if (dialogLine != null) dialogLine.text = "\"" + line + "\"";
            dialogRoot.SetActive(true);
            _shownAt = Time.time;
        }

        /// <summary>BattleClient: you won this floor's fight (and are still standing).</summary>
        public void LocalCleared()
        {
            if (!isTop || client == null || client.playerState == null || !client.playerState.HasCharacter()) return;
            CharacterData c = client.playerState.current;
            bool first = (c.towersCleared & clearBits) != clearBits;
            c.towersCleared |= clearBits;
            int shards = first ? clearShards * 2 : clearShards;
            c.starshards += shards;
            client.playerState.MarkDirty();
            client.playerState.SaveNow();
            if (client.notifications != null)
                client.notifications.Post(NotificationCenter.KIND_INFO, towerName + " cleared!",
                    (first ? "First climb to the top: " : "Another climb to the top: ") + "+" + shards.ToString() + " Starshards.", 0);
        }
    }
}
