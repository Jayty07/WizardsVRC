using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The title screen: where every player arrives. A dais of starlit stone floating over a sea of moonlit clouds under
    /// the aurora, the WIZARDS VRC wordmark hanging ahead of you, the seven schools' symbols drawing themselves in the air
    /// around it, Starfall Isle's castle on its island beyond with its crystal turning, shooting stars and drifting motes.
    /// Its own pieces animate themselves (Animators); this script only runs the visit:
    ///   - On joining you're brought here (eyes shut until you arrive, AwakeningSequence's blackout) and held still.
    ///   - "Opening your spellbook..." until your saves have loaded, then Begin lights up.
    ///   - Begin: your eyes close, you're taken to the Headmaster's study and IntroFlow decides what comes next (the opening
    ///     for a new player, the character select for a returning one). The title stage then switches itself off.
    /// Local to each player; late joiners get it too.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TitleScreen : UdonSharpBehaviour
    {
        public IntroFlow introFlow;
        public AwakeningSequence awakening;
        public SaveManager saveManager;
        public TeleportHelper teleporter;
        public UiAudio uiAudio;

        [Header("Places")]
        [Tooltip("Where you stand on the title stage, facing the logo.")]
        public Transform titleSpot;
        [Tooltip("Where Begin takes you (the Headmaster's study).")]
        public Transform studySpot;
        [Tooltip("Everything on the title stage: switched off once you've left it.")]
        public GameObject stageRoot;

        [Header("Menu")]
        public GameObject beginButton;
        public Text beginLabel;
        public Text statusLabel;
        [Tooltip("The Begin button's glow (pulses once it can be pressed).")]
        public Image beginGlow;
        public AudioSource beginSound;

        [Tooltip("Seconds after joining before you're brought to the title stage (lets the world settle).")]
        public float arriveDelay = 0.5f;
        [Tooltip("Seconds the eyes take to close after Begin before you're moved.")]
        public float departDelay = 1.1f;

        private bool _showing;
        private bool _leaving;
        private bool _done;
        private float _shownAt;

        public bool IsShowing() { return _showing; }

        private void Start()
        {
            if (stageRoot != null) stageRoot.SetActive(true);
            if (beginButton != null) beginButton.SetActive(false);
            SendCustomEventDelayedSeconds(nameof(_Arrive), arriveDelay);
        }

        public void _Arrive()
        {
            if (_done) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) { SendCustomEventDelayedSeconds(nameof(_Arrive), 0.5f); return; }
            if (teleporter != null && titleSpot != null) teleporter.TeleportTo(titleSpot);
            me.Immobilize(true);
            _showing = true;
            _shownAt = Time.time;
            // Open your eyes once you're really here (the teleport lands next frame).
            SendCustomEventDelayedSeconds(nameof(_OpenEyes), 0.35f);
        }

        public void _OpenEyes()
        {
            if (awakening != null) awakening.EndBlackout();
        }

        private void Update()
        {
            if (!_showing) return;
            bool ready = saveManager != null && saveManager.isReady;
            bool canBegin = ready && !_leaving;
            if (beginButton != null && beginButton.activeSelf != canBegin) beginButton.SetActive(canBegin);
            if (statusLabel != null)
            {
                if (_leaving) statusLabel.text = "";
                else if (!ready)
                {
                    int dots = (int)((Time.time - _shownAt) * 2f) % 4;
                    statusLabel.text = "Opening your spellbook" + (dots == 0 ? "" : dots == 1 ? "." : dots == 2 ? ".." : "...");
                }
                else statusLabel.text = "";
            }
            if (beginGlow != null && canBegin)
            {
                Color g = beginGlow.color;
                g.a = 0.35f + 0.3f * Mathf.Sin(Time.time * 3f);
                beginGlow.color = g;
            }
            // Keep you on your spot (a desktop player bumped off it, a VR player who walked in their room).
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && titleSpot != null && !_leaving)
            {
                Vector3 off = me.GetPosition() - titleSpot.position;
                if (off.sqrMagnitude > 36f && teleporter != null) teleporter.TeleportTo(titleSpot);
            }
        }

        /// <summary>The Begin button.</summary>
        public void Begin()
        {
            if (!_showing || _leaving || saveManager == null || !saveManager.isReady) return;
            _leaving = true;
            if (beginSound != null) beginSound.Play();
            else if (uiAudio != null) uiAudio.PlayDiscovery();
            if (beginButton != null) beginButton.SetActive(false);
            if (awakening != null) awakening.StartBlackout(departDelay * 0.85f);
            SendCustomEventDelayedSeconds(nameof(_Depart), departDelay);
        }

        public void _Depart()
        {
            _done = true;
            _showing = false;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (teleporter != null && studySpot != null) teleporter.TeleportTo(studySpot);
            if (Utilities.IsValid(me)) me.Immobilize(false);
            SendCustomEventDelayedSeconds(nameof(_Decide), 0.4f);
        }

        public void _Decide()
        {
            if (stageRoot != null) stageRoot.SetActive(false);
            if (introFlow != null) introFlow.Decide();
            else if (awakening != null) awakening.EndBlackout();
        }
    }
}
