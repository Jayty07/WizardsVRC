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
    ///   - Begin, for a new player: your eyes close, you're taken to the Headmaster's study and the opening plays (IntroFlow).
    ///   - Begin, for a returning player: the character select opens right here on the stage, in place of the menu.
    ///       Select: the whole scene fades out slowly (picture, music and ambience: SceneFade) and fades back in where that
    ///         character last logged off (LocationKeeper), or in the castle.
    ///       New Character: your eyes close and the opening plays for that slot. Delete works in place.
    ///   - Change Character (the book's settings) brings you back here, straight to the character select.
    /// The title stage switches itself off whenever you leave it. Local to each player; late joiners get it too.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TitleScreen : UdonSharpBehaviour
    {
        public IntroFlow introFlow;
        public AwakeningSequence awakening;
        public SaveManager saveManager;
        public TeleportHelper teleporter;
        public UiAudio uiAudio;
        public CharacterSelectUI selectUI;
        [Tooltip("Fades the scene and brings a chosen character back to where they last logged off.")]
        public LocationKeeper locations;
        public SceneFade fade;

        [Header("Places")]
        [Tooltip("Where you stand on the title stage, facing the logo.")]
        public Transform titleSpot;
        [Tooltip("Where Begin takes you (the Headmaster's study).")]
        public Transform studySpot;
        [Tooltip("Everything on the title stage: switched off once you've left it.")]
        public GameObject stageRoot;

        [Header("Menu")]
        [Tooltip("The title menu (Begin, the loading line, the hint): hidden while the character select is up.")]
        public GameObject menuRoot;
        [Tooltip("The character select panel on the stage: shown once a returning player presses Begin.")]
        public GameObject selectRoot;
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
        private bool _selecting;
        private int _openingSlot = -1;
        private bool _toOpening;
        private float _shownAt;

        public bool IsShowing() { return _showing; }
        /// <summary>On the way out (eyes closing or the scene fading): the select screen's buttons do nothing.</summary>
        public bool IsLeaving() { return _leaving; }

        private void Start()
        {
            if (stageRoot != null) stageRoot.SetActive(true);
            if (beginButton != null) beginButton.SetActive(false);
            if (selectRoot != null) selectRoot.SetActive(false);
            if (menuRoot != null) menuRoot.SetActive(true);
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
            bool canBegin = ready && !_leaving && !_selecting;
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

        /// <summary>The Begin button: the character select for a returning player, the opening for a new one.</summary>
        public void Begin()
        {
            if (!_showing || _leaving || _selecting || saveManager == null || !saveManager.isReady) return;
            if (beginSound != null) beginSound.Play();
            else if (uiAudio != null) uiAudio.PlayDiscovery();
            if (selectUI != null && selectRoot != null && !selectUI.IsNewPlayer())
            {
                OpenSelect();
                return;
            }
            _openingSlot = -1;
            _toOpening = false;
            CloseEyesAndGo();
        }

        private void OpenSelect()
        {
            _selecting = true;
            if (menuRoot != null) menuRoot.SetActive(false);
            if (selectRoot != null) selectRoot.SetActive(true);
            if (selectUI != null) selectUI.RefreshSlots();
        }

        /// <summary>
        /// Back to the title stage's character select from anywhere (Change Character, or a respawn with no character): a
        /// cut to black, then the stage fades in.
        /// </summary>
        public void ShowSelect()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            if (fade != null) fade.CutToBlack();
            if (stageRoot != null) stageRoot.SetActive(true);
            _done = false;
            _leaving = false;
            _showing = true;
            _shownAt = Time.time;
            OpenSelect();
            if (teleporter != null && titleSpot != null) teleporter.TeleportTo(titleSpot);
            me.Immobilize(true);
            if (awakening != null) awakening.EndBlackout();
            SendCustomEventDelayedSeconds(nameof(_RevealStage), 0.45f);
        }

        public void _RevealStage()
        {
            if (fade != null) fade.FadeIn(1.2f);
        }

        /// <summary>
        /// The select screen's Select (the character is already the active one): the scene fades out slowly and back in
        /// where they last logged off. Without a LocationKeeper, the old way: eyes close and you're put at 'spot'.
        /// </summary>
        public void LeaveWithCharacter(Transform spot)
        {
            if (!_showing || _leaving) return;
            _leaving = true;
            if (uiAudio != null) uiAudio.PlayDiscovery();
            if (locations != null)
            {
                locations.ArriveAtLastPlace();   // calls _Departed once it's dark
                return;
            }
            _leaveSpot = spot;
            _openingSlot = -1;
            _toOpening = false;
            _skipDecide = true;
            if (awakening != null) awakening.StartBlackout(departDelay * 0.85f);
            SendCustomEventDelayedSeconds(nameof(_Depart), departDelay);
        }

        /// <summary>The select screen's New Character: eyes close, then the opening plays for that slot.</summary>
        public void LeaveForOpening(int slot)
        {
            if (!_showing || _leaving) return;
            _openingSlot = slot;
            _toOpening = true;
            if (uiAudio != null) uiAudio.PlayDiscovery();
            CloseEyesAndGo();
        }

        private Transform _leaveSpot;
        private bool _skipDecide;

        private void CloseEyesAndGo()
        {
            _leaving = true;
            _leaveSpot = studySpot;
            _skipDecide = false;
            if (beginButton != null) beginButton.SetActive(false);
            if (awakening != null) awakening.StartBlackout(departDelay * 0.85f);
            SendCustomEventDelayedSeconds(nameof(_Depart), departDelay);
        }

        public void _Depart()
        {
            _done = true;
            _showing = false;
            _selecting = false;
            VRCPlayerApi me = Networking.LocalPlayer;
            Transform to = _leaveSpot != null ? _leaveSpot : studySpot;
            if (teleporter != null && to != null) teleporter.TeleportTo(to);
            if (Utilities.IsValid(me)) me.Immobilize(false);
            SendCustomEventDelayedSeconds(nameof(_Decide), 0.4f);
        }

        public void _Decide()
        {
            if (stageRoot != null) stageRoot.SetActive(false);
            if (_toOpening && _openingSlot >= 0 && awakening != null) awakening.Begin(_openingSlot);
            else if (!_skipDecide && introFlow != null) introFlow.Decide();
            else if (awakening != null) awakening.EndBlackout();
            _toOpening = false;
            _openingSlot = -1;
            _leaving = false;
        }

        /// <summary>LocationKeeper: the scene has gone dark after a character was chosen; the stage is done with.</summary>
        public void _Departed()
        {
            _done = true;
            _showing = false;
            _selecting = false;
            _leaving = false;
            if (stageRoot != null) stageRoot.SetActive(false);
        }
    }
}
