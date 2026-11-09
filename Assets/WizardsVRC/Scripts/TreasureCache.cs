using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A treasure cache tucked at the end of a side alley (the payoff for exploring a dead end): click it to open the lid
    /// and take the gold inside. Purely local: it's open (empty) only for you, and fills again after refillSeconds, so a
    /// street is worth coming back to. Not in a fight, and only once you have a wizard.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TreasureCache : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public UiAudio uiAudio;
        public NotificationCenter notifications;
        [Tooltip("Name shown in the notice, e.g. Smuggler's stash.")]
        public string cacheName = "Treasure cache";
        public int goldMin = 20;
        public int goldMax = 40;
        [Tooltip("Seconds until it fills again after you empty it.")]
        public float refillSeconds = 1200f;
        [Tooltip("The lid: swung open while the cache is empty.")]
        public Transform lid;
        public Vector3 lidOpenEuler = new Vector3(-110f, 0f, 0f);
        [Tooltip("The glint over a full cache (hidden while it's empty).")]
        public GameObject glint;
        public Text label;

        private bool _empty;
        private Quaternion _lidShut;
        private float _swing;

        private void Start()
        {
            if (lid != null) _lidShut = lid.localRotation;
            ShowLabel();
        }

        public override void Interact()
        {
            if (_empty || playerState == null || !playerState.HasCharacter() || playerState.inCombat) return;
            int gold = Random.Range(goldMin, goldMax + 1);
            playerState.current.gold += gold;
            playerState.MarkDirty();
            _empty = true;
            _swing = 0f;
            if (glint != null) glint.SetActive(false);
            if (uiAudio != null) uiAudio.PlayDiscovery();
            if (notifications != null) notifications.PostQuiet(NotificationCenter.KIND_INFO, cacheName, "+" + gold.ToString() + " gold", 0);
            ShowLabel();
            SendCustomEventDelayedSeconds(nameof(Refill), Mathf.Max(30f, refillSeconds));
        }

        public void Refill()
        {
            _empty = false;
            _swing = 0f;
            if (glint != null) glint.SetActive(true);
            ShowLabel();
        }

        private void Update()
        {
            if (lid == null || _swing >= 1f) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && (me.GetPosition() - transform.position).sqrMagnitude > 2500f) { _swing = 1f; Snap(); return; }
            _swing = Mathf.Min(1f, _swing + Time.deltaTime * 2.5f);
            float t = _empty ? _swing : 1f - _swing;
            t = t * t * (3f - 2f * t);
            lid.localRotation = _lidShut * Quaternion.Euler(lidOpenEuler * t);
        }

        private void Snap()
        {
            if (lid != null) lid.localRotation = _empty ? _lidShut * Quaternion.Euler(lidOpenEuler) : _lidShut;
        }

        private void ShowLabel()
        {
            if (label != null) label.text = _empty ? cacheName + "\n(empty: check back later)" : cacheName;
        }
    }
}
