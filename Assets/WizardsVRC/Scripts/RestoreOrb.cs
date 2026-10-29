using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A floating orb that tops you up when you walk through it: red orbs restore health, blue orbs restore mana.
    /// Purely local - everyone has their own orbs - so an orb you take vanishes only for you and comes back after
    /// respawnSeconds. You only take it if it would actually help (not at full, not in a fight).
    /// The visual child bobs and spins; the trigger collider stays put on this object.
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

        private Vector3 _home;
        private bool _taken;
        private float _phase;

        private void Start()
        {
            if (visual != null) _home = visual.localPosition;
            _phase = (transform.position.x * 0.37f + transform.position.z * 0.71f) % 6.283f;
        }

        private void Update()
        {
            if (_taken || visual == null) return;
            float t = Time.time * 2.1f + _phase;
            visual.localPosition = _home + Vector3.up * (Mathf.Sin(t) * bobHeight);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (_taken || !Utilities.IsValid(player) || !player.isLocal) return;
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
