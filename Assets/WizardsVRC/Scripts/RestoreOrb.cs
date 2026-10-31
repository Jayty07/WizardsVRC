using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A floating orb that tops you up when you walk into it: red orbs restore health, blue orbs restore mana. It drifts
    /// around the area along a wandering loop (its position comes from the server clock, so everyone sees it in the same
    /// place), or floats in place if it has no route. Purely local otherwise: an orb you take vanishes only for you and
    /// comes back after respawnSeconds (somewhere further along its loop). You only take it if it would actually help
    /// (not at full, not in a fight).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RestoreOrb : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public UiAudio uiAudio;
        [Tooltip("True for a blue mana orb, false for a red health orb.")]
        public bool mana;
        [Tooltip("Share of the maximum restored, in percent.")]
        public int percent = 25;
        [Tooltip("At least this much is restored, however low the maximum.")]
        public int minimum = 10;
        public float respawnSeconds = 30f;
        [Tooltip("The floating orb (bobs and spins). Hidden while the orb is respawning.")]
        public Transform visual;
        public float bobHeight = 0.18f;
        public float spinSpeed = 70f;

        [Header("Wandering")]
        [Tooltip("Corners of the loop it drifts round (empty = it stays where it is).")]
        public Transform[] path;
        [Tooltip("Metres per second.")]
        public float speed = 0.8f;
        [Tooltip("Start distance along the loop (m), so orbs sharing a loop are spread out.")]
        public float phaseOffset;
        [Tooltip("You take it within this distance (m) of the orb's base.")]
        public float pickupRadius = 1.4f;

        private Vector3 _home;
        private bool _taken;
        private float _phase;
        private float[] _cum;
        private float _length;
        private float _nextCheck;

        private void Start()
        {
            if (visual != null) _home = visual.localPosition;
            _phase = (transform.position.x * 0.37f + transform.position.z * 0.71f) % 6.283f;
            BuildRoute();
        }

        private void BuildRoute()
        {
            _length = 0f;
            if (path == null || path.Length < 2) return;
            int n = path.Length;
            _cum = new float[n + 1];
            for (int i = 1; i <= n; i++)
            {
                Transform a = path[i - 1], b = path[i % n];
                _cum[i] = _cum[i - 1] + (a != null && b != null ? Vector3.Distance(a.position, b.position) : 0f);
            }
            _length = _cum[n];
        }

        private void Update()
        {
            if (_length > 0.01f) Drift();
            if (_taken || visual == null) return;
            float t = Time.time * 2.1f + _phase;
            visual.localPosition = _home + Vector3.up * (Mathf.Sin(t) * bobHeight);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.15f;
                CheckPickup();
            }
        }

        /// <summary>Where the orb is on its loop now (server seconds: the same for everyone).</summary>
        private void Drift()
        {
            double wrapped = (Networking.GetServerTimeInSeconds() * speed + phaseOffset) % _length;
            if (wrapped < 0) wrapped += _length;
            float dist = (float)wrapped;
            int n = path.Length;
            int seg = 0;
            while (seg < n - 1 && dist > _cum[seg + 1]) seg++;
            Transform a = path[seg], b = path[(seg + 1) % n];
            if (a == null || b == null) return;
            float len = _cum[seg + 1] - _cum[seg];
            float f = len > 0.001f ? Mathf.Clamp01((dist - _cum[seg]) / len) : 0f;
            // Eased between corners so it drifts rather than marches.
            f = f * f * (3f - 2f * f);
            transform.position = Vector3.Lerp(a.position, b.position, f);
        }

        private void CheckPickup()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 d = me.GetPosition() - transform.position;
            if (d.y < -1.2f || d.y > 2.4f) return;
            d.y = 0f;
            if (d.magnitude > pickupRadius) return;
            TryTake();
        }

        private void TryTake()
        {
            if (playerState == null || !playerState.HasCharacter() || playerState.inCombat) return;
            int max = mana ? playerState.MaxMana() : playerState.MaxHealth();
            int cur = mana ? playerState.GetMana() : playerState.GetHealth();
            if (max <= 0 || cur >= max) return;
            int amount = Mathf.Max(minimum, max * percent / 100);
            if (mana) playerState.SetMana(cur + amount);
            else playerState.SetHealth(cur + amount);
            if (uiAudio != null) uiAudio.PlayHeal();
            _taken = true;
            if (visual != null) visual.gameObject.SetActive(false);
            SendCustomEventDelayedSeconds(nameof(Respawn), Mathf.Max(1f, respawnSeconds));
        }

        public void Respawn()
        {
            _taken = false;
            if (visual != null)
            {
                visual.localPosition = _home;
                visual.gameObject.SetActive(true);
            }
        }
    }
}
