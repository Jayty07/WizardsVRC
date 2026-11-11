using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The director (CUTSCENES.md): plays a <see cref="Cutscene"/> for one player the way a story scene plays in a
    /// role-playing game. Nothing here is synced: the cast turn, gesture and step on or off only for whoever is in the
    /// scene, and everyone else goes on seeing the street as it was.
    ///
    /// A screen opens in front of you (you are held still) showing a hidden film camera, letterboxed. The scene's cast
    /// are bodies in the world: whoever you clicked, anyone else in it who stands nearby, and stand-ins for those who
    /// belong elsewhere, brought on at marks beside the first of the cast. You are in it too: the camera sees your own
    /// avatar and nobody else's.
    ///   - Each line is typed into a talk window under its speaker's name plate. Click the picture (or Space) to finish
    ///     the line, and again to go on; with Auto on it goes on by itself once you've had time to read it (or the voice
    ///     clip has ended). Skip ends the scene.
    ///   - Whoever speaks turns to whoever they're speaking to and acts the line out (a nod, a shrug, a bow...); the
    ///     rest turn to listen.
    ///   - The camera cuts with the lines: a wide shot to open, the speaker waist-up, over the listener's shoulder,
    ///     close for a short sharp line, wide again to close; or as the line's own tag says. Each shot is worked out
    ///     from where everyone really stands, checked for a clear view, tried from the other side and closer, and kept
    ///     on one side of the pair so they don't swap places on screen. With no clear view anywhere it falls back on
    ///     the scene's placed shots.
    ///   - Between lines a scene can wait, fade, show a card of words over black, cut away, have someone gesture, turn,
    ///     walk on or leave, and ask you something: up to three answers, and the lines that follow can depend on which.
    /// A scene with no cast (a boss speaking from its duel ring) plays on its placed shots, with the same window.
    ///
    /// The same screen plays a spell's film (<see cref="PlayFilm"/>, driven by SpellCinematic): the caster and the spell
    /// on the lower bar, shots set every frame from outside, and the camera then sees every player. Skip just closes it.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CutscenePlayer : UdonSharpBehaviour
    {
        public Camera filmCamera;
        [Tooltip("Plays the dialogue pop-up sound when a scene starts and the page sound as it goes on.")]
        public UiAudio uiAudio;
        public GameObject screenRoot;

        [Header("The talk window")]
        public GameObject talkWindow;
        [Tooltip("The speaker's name plate (hidden for a line nobody speaks).")]
        public GameObject namePlate;
        public Text speakerLabel;
        [Tooltip("The medallion beside the words (hidden for a line nobody speaks).")]
        public GameObject portraitRoot;
        public Image portraitRing;
        [Tooltip("Ally (blue), neutral (yellow), hostile (red) rims.")]
        public Sprite[] portraitRings;
        [Tooltip("The speaker's initial in the medallion.")]
        public Text portraitInitial;
        public Color[] stanceColors;
        public Text subtitleLabel;
        [Tooltip("Bobs at the corner of the window once the line is all there.")]
        public RectTransform advanceMark;
        [Tooltip("The picture itself: click it to finish the line, or go on.")]
        public GameObject nextButton;
        public GameObject skipButton;
        public GameObject autoButton;
        public Text autoLabel;
        public Text hintLabel;

        [Header("Questions")]
        public GameObject choicePanel;
        public Text choicePrompt;
        public GameObject[] choiceButtons;
        public Text[] choiceLabels;

        [Header("Fades, cards and the letterbox")]
        public Image fadeImage;
        public Text cardLabel;
        public RectTransform barTop;
        public RectTransform barBottom;
        public float barHeight = 64f;
        [Tooltip("A spell film's caption, on the lower bar.")]
        public Text filmLabel;

        [Header("The dark round you while a scene plays")]
        [Tooltip("A sphere round your head (WizardsVRC/DuskVeil) that dims the world behind the screen. Optional.")]
        public Transform veil;
        public Renderer veilRenderer;
        [Range(0f, 0.9f)] public float veilDarkness = 0.5f;

        [Header("Pace")]
        public AudioSource voiceSource;
        [Tooltip("How far in front of you the screen opens in VR.")]
        public float screenDistance = 1.6f;
        [Tooltip("...and on a desktop, where it should nearly fill your view.")]
        public float desktopScreenDistance = 1.05f;
        [Tooltip("Seconds a voiced line waits after its clip ends.")]
        public float linePadding = 0.6f;
        [Tooltip("Letters typed per second.")]
        public float charsPerSecond = 45f;
        [Tooltip("Lines go on by themselves once read (the Auto button).")]
        public bool autoAdvance = true;
        [Tooltip("What the film camera sees during a spell's film: every player as well (not the screen or your own head-less avatar).")]
        public int filmCullingMask = -1;
        [Tooltip("The screen a spell's film plays on is this far in front of you.")]
        public float filmScreenDistance = 1.5f;

        private const int MAX_CAST = 8;
        // Everything solid: not players, the interface, pickups or the walk-through layers.
        private const int SolidMask = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 17) | (1 << 18));
        /// <summary>How far off their mark someone starts when they walk on (and ends when they leave).</summary>
        private const float StepAside = 1.5f;
        /// <summary>
        /// Where a face sits in a shot of one person: this much of half the picture's height above its middle (0.26 puts
        /// the eyes 63% of the way up: clear of the talk window across the bottom, with room for a hat under the top bar).
        /// </summary>
        private const float EyeLine = 0.26f;

        private int _storyMask = -1;
        private float _baseFov = 50f;
        private bool _film;
        private string _filmTitle = "";
        private Vector3 _screenScale;
        private Vector2 _markBase;
        private Material _veilMat;
        private float _veil;

        private Cutscene _current;
        private bool _directed;
        private int _beat;
        private int _kind;
        private int _answer;
        private int _choices;
        private float _beatStart;
        private float _beatEnd;
        private float _lastNext;
        private string _speaker = "";

        // The line on screen.
        private string _text = "";
        private int _typed;
        private float _typeClock;
        private bool _typedAll;
        private float _autoAt;
        private float _voiceEnd;

        // Fades and the letterbox.
        private float _fade;
        private float _fadeTarget;
        private float _fadeSpeed = 2f;
        private float _bars;
        private bool _card;

        // The stage: where you stand, and the line from the first of the cast to you.
        private Vector3 _feet;
        private Vector3 _head;
        private float _size = 1f;
        private Vector3 _axis;
        private Vector3 _right;
        private float _side = 1f;

        // The cast.
        private int _castCount;
        private Transform[] _cast = new Transform[MAX_CAST];
        private Animator[] _castAnim = new Animator[MAX_CAST];
        private GameObject[] _castOriginal = new GameObject[MAX_CAST];
        private float[] _castFace = new float[MAX_CAST];
        private bool[] _castOn = new bool[MAX_CAST];
        private bool[] _castStandIn = new bool[MAX_CAST];
        private bool[] _castHid = new bool[MAX_CAST];
        private bool[] _castOriginalHid = new bool[MAX_CAST];
        private bool[] _castTurns = new bool[MAX_CAST];
        private Quaternion[] _castRestRot = new Quaternion[MAX_CAST];
        private Vector3[] _castRestPos = new Vector3[MAX_CAST];
        private Vector3[] _castMark = new Vector3[MAX_CAST];
        private Vector3[] _castAway = new Vector3[MAX_CAST];
        private Vector3[] _castLook = new Vector3[MAX_CAST];
        private float[] _castWalk = new float[MAX_CAST];
        private float[] _castWalkTo = new float[MAX_CAST];
        private bool[] _slotTaken = new bool[12];
        private Vector3 _ground;
        private float _settle;

        // The camera.
        private bool _hasShot;
        private Transform _placed;
        private Vector3 _camPos;
        private Quaternion _camRot;
        private Vector3 _camDrift;
        private float _camSpeed;
        private float _camReach;
        private float _shotStart;
        private int _shotFraming;
        private int _shotSubject;
        private int _shotOther;
        private Vector3 _tryPos;
        private Vector3 _tryLook;
        private float _tryFov;
        private int _tryFraming;
        private int _says;
        private int _cycle;
        private int _lastSpeaker;
        private int _lastListener;
        // The cut from a scene's opening wide shot in to its first speaker.
        private float _cutAt;
        private int _cutWho;
        private int _cutTo;

        public bool IsPlaying() { return _current != null; }

        /// <summary>Who spoke last in the scene that's playing ("" when none is). MusicDirector picks a theme by it.</summary>
        public string CurrentSpeaker() { return _current != null ? _speaker : ""; }

        /// <summary>Your answer to the last question a scene asked (1-3; 0 = it asked none).</summary>
        public int LastAnswer() { return _answer; }

        private void Start()
        {
            if (screenRoot != null) screenRoot.SetActive(false);
            if (filmCamera != null)
            {
                filmCamera.enabled = false;
                _storyMask = filmCamera.cullingMask;
                _baseFov = filmCamera.fieldOfView;
            }
            if (veil != null) veil.gameObject.SetActive(false);
            if (advanceMark != null) _markBase = advanceMark.anchoredPosition;
        }

        // ------------------------------------------------------------------ a scene

        public void Play(Cutscene cutscene)
        {
            if (cutscene == null || cutscene.LineCount() == 0) return;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            if (_film) StopFilm();
            if (_current != null) Stop(false);
            FinishSettle();

            _current = cutscene;
            _beat = -1;
            _kind = Cutscene.BEAT_WAIT;
            _answer = 0;
            _says = 0;
            _cycle = 0;
            _lastSpeaker = Cutscene.ACTOR_NOBODY;
            _lastListener = Cutscene.ACTOR_PLAYER;
            _speaker = "";
            _hasShot = false;
            _placed = null;
            _shotFraming = Cutscene.SHOT_AUTO;
            _card = false;
            _bars = 0f;
            // Every scene opens out of black.
            _fade = 1f;
            _fadeTarget = 0f;
            _fadeSpeed = 2.5f;

            VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            bool vr = local.IsUserInVR();
            _feet = local.GetPosition();
            _head = head.position;
            _size = Mathf.Clamp((_head.y - _feet.y) / 1.6f, 0.45f, 1.7f);
            PlaceScreen(head.position, head.rotation, vr ? screenDistance : desktopScreenDistance, !vr);
            local.Immobilize(true);
            SetStage(head.rotation);

            if (talkWindow != null) talkWindow.SetActive(false);
            if (choicePanel != null) choicePanel.SetActive(false);
            if (cardLabel != null) cardLabel.gameObject.SetActive(false);
            if (filmLabel != null) filmLabel.gameObject.SetActive(false);
            if (advanceMark != null) advanceMark.gameObject.SetActive(false);
            if (skipButton != null) skipButton.SetActive(cutscene.skippable);
            if (nextButton != null) nextButton.SetActive(true);
            if (autoButton != null) autoButton.SetActive(true);
            if (hintLabel != null)
            {
                hintLabel.gameObject.SetActive(true);
                hintLabel.text = vr ? "Click the picture to go on" : "Click the picture or press Space to go on";
            }
            ShowAuto();
            ShowScreen();
            if (screenRoot != null) screenRoot.SetActive(true);
            if (filmCamera != null)
            {
                if (_storyMask != -1) filmCamera.cullingMask = _storyMask;
                filmCamera.fieldOfView = _baseFov;
                filmCamera.enabled = true;
            }
            if (veil != null && veilDarkness > 0.01f)
            {
                if (veilRenderer != null && _veilMat == null) _veilMat = veilRenderer.material;
                veil.position = _head;
                _veil = 0f;
                ShowVeil();
                veil.gameObject.SetActive(true);
            }
            if (uiAudio != null) uiAudio.PlayPopup();
            if (!Advance()) Stop(true);
        }

        /// <summary>The picture was clicked (or Space pressed): finish typing the line, or go on to the next beat.</summary>
        public void Next()
        {
            if (_current == null || Time.time - _lastNext < 0.12f) return;
            _lastNext = Time.time;
            switch (_kind)
            {
                case Cutscene.BEAT_SAY:
                    if (!_typedAll) { FinishTyping(); return; }
                    break;
                case Cutscene.BEAT_CHOICE:
                case Cutscene.BEAT_FADE_OUT:
                case Cutscene.BEAT_FADE_IN:
                    return;
                default:
                    // A wait, a card, a cutaway: a moment to take it in, then a click moves it along.
                    if (Time.time - _beatStart < 0.4f) return;
                    break;
            }
            if (uiAudio != null) uiAudio.PlayNextClick();
            Step();
        }

        public void Skip()
        {
            if (_film) { StopFilm(); return; }
            if (_current != null && _current.skippable) Stop(true);
        }

        /// <summary>The Auto button: lines go on by themselves, or wait for a click.</summary>
        public void ToggleAuto()
        {
            autoAdvance = !autoAdvance;
            ShowAuto();
            if (autoAdvance && _current != null && _kind == Cutscene.BEAT_SAY && _typedAll) _autoAt = Mathf.Max(_autoAt, Time.time + 0.8f);
        }

        public void Choose0() { Choose(0); }
        public void Choose1() { Choose(1); }
        public void Choose2() { Choose(2); }

        private void Choose(int k)
        {
            if (_current == null || _kind != Cutscene.BEAT_CHOICE || k < 0 || k >= _choices) return;
            _answer = k + 1;
            if (choicePanel != null) choicePanel.SetActive(false);
            Step();
        }

        private void ShowAuto()
        {
            if (autoLabel != null) autoLabel.text = autoAdvance ? "Auto: On" : "Auto: Off";
        }

        /// <summary>
        /// On to the next beat that plays (beats of an answer you didn't give are passed over). False when the scene has
        /// run out of beats.
        /// </summary>
        private bool Advance()
        {
            Cutscene cs = _current;
            if (cs == null) return true;
            int count = cs.LineCount();
            for (int guard = 0; guard <= count; guard++)
            {
                _beat++;
                if (_beat >= count) break;
                int branch = cs.BranchAt(_beat);
                if (branch != 0 && branch != _answer) continue;
                if (Begin(_beat)) return true;
            }
            return false;
        }

        /// <summary>
        /// The next beat, or the end of the scene. (The end is reached from here, with Advance already returned, because
        /// what a scene's ending does may be to start the next scene, which advances in its turn.)
        /// </summary>
        private void Step()
        {
            if (!Advance()) Stop(true);
        }

        /// <summary>Starts beat i. True when it takes time (a line, a wait, a question); false when it is done at once.</summary>
        private bool Begin(int i)
        {
            Cutscene cs = _current;
            int kind = cs.KindAt(i);
            float amount = cs.AmountAt(i);
            int actor = cs.ActorAt(i);
            int who = Who(actor);
            int framing = cs.FramingAt(i);
            _kind = kind;
            _beatStart = Time.time;
            _beatEnd = 0f;
            _cutAt = 0f;
            if (_card)
            {
                _card = false;
                if (cardLabel != null) cardLabel.gameObject.SetActive(false);
            }
            if (choicePanel != null && choicePanel.activeSelf) choicePanel.SetActive(false);
            if (advanceMark != null) advanceMark.gameObject.SetActive(false);

            switch (kind)
            {
                case Cutscene.BEAT_SAY:
                    Say(i, who);
                    return true;

                case Cutscene.BEAT_CHOICE:
                    return Ask(i, framing);

                case Cutscene.BEAT_WAIT:
                    Hold(amount > 0f ? amount : 1f);
                    return true;

                case Cutscene.BEAT_FADE_OUT:
                case Cutscene.BEAT_FADE_IN:
                {
                    float seconds = amount > 0f ? amount : 0.6f;
                    _fadeTarget = kind == Cutscene.BEAT_FADE_OUT ? 1f : 0f;
                    _fadeSpeed = 1f / Mathf.Max(0.05f, seconds);
                    if (Mathf.Abs(_fade - _fadeTarget) < 0.01f) return false;
                    Hold(seconds);
                    return true;
                }

                case Cutscene.BEAT_CARD:
                {
                    // Words over black: the picture goes dark under them and stays dark until the next line or shot.
                    string words = cs.LineAt(i);
                    _fadeTarget = 1f;
                    _fadeSpeed = 2.5f;
                    _card = true;
                    if (cardLabel != null)
                    {
                        cardLabel.text = words;
                        cardLabel.color = new Color(1f, 1f, 1f, 0f);
                        cardLabel.gameObject.SetActive(true);
                    }
                    ShowScreen();
                    Hold(amount > 0f ? amount : 2.4f + words.Length * 0.05f);
                    return true;
                }

                case Cutscene.BEAT_SHOT:
                    if (!_directed) return false;
                    Reveal();
                    if (framing == Cutscene.SHOT_AUTO) framing = Cutscene.SHOT_MEDIUM;
                    if (actor == Cutscene.ACTOR_PLAYER || framing == Cutscene.SHOT_YOU) Shoot(framing, Cutscene.ACTOR_PLAYER, _lastSpeaker, i);
                    else
                    {
                        int beside = Who(cs.TargetAt(i));
                        Shoot(framing, who, beside != Cutscene.ACTOR_NOBODY ? beside : Facing(who), i);
                    }
                    Hold(amount > 0f ? amount : 1.6f);
                    return true;

                case Cutscene.BEAT_EMOTE:
                    if (!_directed || who < 0) return false;
                    Emote(who, cs.EmoteAt(i));
                    if (framing != Cutscene.SHOT_AUTO) Shoot(framing, who, Facing(who), i);
                    if (amount <= 0f) return false;
                    Hold(amount);
                    return true;

                case Cutscene.BEAT_LOOK:
                {
                    if (!_directed || who < 0) return false;
                    int at = Who(cs.TargetAt(i));
                    // (Nobody to look at: back the way they stood before the scene.)
                    if (at == Cutscene.ACTOR_NOBODY) Face(who, _cast[who].position + _castRestRot[who] * Vector3.back * 3f);
                    else Face(who, StandOf(at));
                    if (amount <= 0f) return false;
                    Hold(amount);
                    return true;
                }

                case Cutscene.BEAT_ENTER:
                    if (!_directed || !Enter(actor)) return false;
                    Reveal();
                    Shoot(framing == Cutscene.SHOT_AUTO ? Cutscene.SHOT_MEDIUM : framing, actor, Cutscene.ACTOR_PLAYER, i);
                    Hold(amount > 0f ? amount : 1.1f);
                    return true;

                case Cutscene.BEAT_EXIT:
                    if (!_directed || who < 0) return false;
                    Exit(who);
                    if (framing != Cutscene.SHOT_AUTO) Shoot(framing, who, Cutscene.ACTOR_PLAYER, i);
                    Hold(amount > 0f ? amount : 1.1f);
                    return true;
            }
            return false;
        }

        /// <summary>A beat with no words holds for this long (a click cuts a wait, a card or a cutaway short).</summary>
        private void Hold(float seconds)
        {
            _beatEnd = Time.time + seconds;
            if (talkWindow != null && seconds >= 0.4f) talkWindow.SetActive(false);
        }

        /// <summary>Out of black, if the picture is dark (a line or a shot after a card or a fade).</summary>
        private void Reveal()
        {
            if (_fadeTarget <= 0f) return;
            _fadeTarget = 0f;
            _fadeSpeed = 2.2f;
        }

        // ------------------------------------------------------------------ lines and questions

        private void Say(int i, int who)
        {
            Cutscene cs = _current;
            string speaker = cs.SpeakerAt(i);
            int listener = Listener(i, who);
            _text = cs.LineAt(i);
            Reveal();
            ShowTalk(speaker, Mathf.Clamp(cs.StanceAt(i), 0, 2));

            _typed = 0;
            _typeClock = 0f;
            _typedAll = false;
            _voiceEnd = 0f;
            if (subtitleLabel != null) subtitleLabel.text = "";
            AudioClip clip = cs.VoiceAt(i);
            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (clip != null)
                {
                    voiceSource.clip = clip;
                    voiceSource.Play();
                    _voiceEnd = Time.time + clip.length + linePadding;
                }
            }
            if (_text.Length == 0) FinishTyping();

            if (_directed)
            {
                Address(who, listener);
                int emote = cs.EmoteAt(i);
                // Whoever you clicked is already gesturing as the scene opens: that is their first line's.
                bool opening = _says == 0 && who == 0 && (emote == Cutscene.EMOTE_AUTO || emote == Cutscene.EMOTE_TALK);
                if (!opening) Emote(who, emote);
                int framing = SayFraming(i, who, listener);
                if (framing == Cutscene.SHOT_YOU) Shoot(framing, Cutscene.ACTOR_PLAYER, who == Cutscene.ACTOR_PLAYER ? listener : who, i);
                else Shoot(framing, who, listener, i);
                // Establish, then go in: a scene's opening line starts on the wide shot and, a moment later, cuts to
                // whoever is speaking (so even a one-line scene is seen from afar and from close by).
                if (_says == 0 && who >= 0 && cs.FramingAt(i) == Cutscene.SHOT_AUTO)
                {
                    _cutAt = Time.time + 1.8f;
                    _cutWho = who;
                    _cutTo = listener;
                    _cycle = 1;
                }
            }
            else Placed(i);

            _lastSpeaker = who;
            _lastListener = listener;
            _says++;
            _speaker = speaker;
        }

        /// <summary>A question: "Prompt|Answer|Answer|Answer". False (passed over) when there is nothing to pick.</summary>
        private bool Ask(int i, int framing)
        {
            if (choicePanel == null || choiceButtons == null) return false;
            string[] parts = _current.LineAt(i).Split('|');
            int n = Mathf.Min(parts.Length - 1, choiceButtons.Length);
            if (n <= 0) return false;
            _choices = n;
            _answer = 0;
            Reveal();
            if (talkWindow != null) talkWindow.SetActive(false);
            if (choicePrompt != null) choicePrompt.text = parts[0];
            for (int k = 0; k < choiceButtons.Length; k++)
            {
                if (choiceButtons[k] != null) choiceButtons[k].SetActive(k < n);
                if (k < n && choiceLabels != null && k < choiceLabels.Length && choiceLabels[k] != null) choiceLabels[k].text = parts[k + 1];
            }
            choicePanel.SetActive(true);
            if (_directed)
            {
                // Everyone waits on you, and the camera with them.
                Address(Cutscene.ACTOR_PLAYER, _lastSpeaker);
                Shoot(framing == Cutscene.SHOT_AUTO ? Cutscene.SHOT_YOU : framing, Cutscene.ACTOR_PLAYER, _lastSpeaker, i);
            }
            return true;
        }

        private void ShowTalk(string speaker, int stance)
        {
            if (talkWindow != null && !talkWindow.activeSelf) talkWindow.SetActive(true);
            bool named = speaker.Length > 0;
            if (namePlate != null) namePlate.SetActive(named);
            if (portraitRoot != null) portraitRoot.SetActive(named);
            bool tinted = stanceColors != null && stance < stanceColors.Length;
            if (speakerLabel != null)
            {
                speakerLabel.text = speaker;
                if (tinted) speakerLabel.color = stanceColors[stance];
            }
            if (portraitRing != null && portraitRings != null && stance < portraitRings.Length) portraitRing.sprite = portraitRings[stance];
            if (portraitInitial != null)
            {
                string name = speaker.StartsWith("Professor ") ? speaker.Substring(10) : (speaker.StartsWith("Headmaster ") ? speaker.Substring(11) : speaker);
                // (Past an opening quote: "Big Pinch" is a B.)
                int k = 0;
                while (k < name.Length - 1 && (name[k] == '"' || name[k] == '\'' || name[k] == ' ')) k++;
                portraitInitial.text = name.Length > 0 ? name.Substring(k, 1).ToUpper() : "?";
                if (tinted) portraitInitial.color = stanceColors[stance];
            }
        }

        /// <summary>
        /// Types the line. The rest of it is there too, invisible, so the words never jump to the next row as they fill in.
        /// </summary>
        private void Type(float dt)
        {
            _typeClock += dt * charsPerSecond;
            int n = Mathf.Min(_text.Length, Mathf.FloorToInt(_typeClock));
            if (n == _typed) return;
            _typed = n;
            if (n >= _text.Length) { FinishTyping(); return; }
            if (subtitleLabel != null) subtitleLabel.text = _text.Substring(0, n) + "<color=#00000000>" + _text.Substring(n) + "</color>";
        }

        private void FinishTyping()
        {
            _typed = _text.Length;
            _typedAll = true;
            if (subtitleLabel != null) subtitleLabel.text = _text;
            if (advanceMark != null) advanceMark.gameObject.SetActive(true);
            // Auto: a voiced line goes on when its clip has ended, a silent one once it has been read.
            _autoAt = _voiceEnd > 0f ? Mathf.Max(_voiceEnd, Time.time + 0.5f) : Time.time + 1.4f + _text.Length * 0.035f;
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_settle > 0f) Settle(dt);
            if (_current == null) return;

            float bars = Mathf.MoveTowards(_bars, 1f, dt / 0.4f);
            float fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * dt);
            if (bars != _bars || fade != _fade)
            {
                _bars = bars;
                _fade = fade;
                ShowScreen();
            }
            if (_veilMat != null && _veil < 1f)
            {
                _veil = Mathf.MoveTowards(_veil, 1f, dt / 0.5f);
                ShowVeil();
            }
            MoveCast(dt);
            if (filmCamera != null && _hasShot)
                filmCamera.transform.SetPositionAndRotation(_camPos + _camDrift * Mathf.Min((Time.time - _shotStart) * _camSpeed, _camReach), _camRot);

            // Walked off, respawned or been moved: the scene is over.
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && (local.GetPosition() - _feet).sqrMagnitude > 16f) { Stop(true); return; }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) { Next(); return; }
            if (_kind == Cutscene.BEAT_CHOICE)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Choose(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Choose(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Choose(2);
                return;
            }
            if (_kind == Cutscene.BEAT_SAY)
            {
                if (_cutAt > 0f && Time.time >= _cutAt)
                {
                    _cutAt = 0f;
                    Shoot(Cutscene.SHOT_MEDIUM, _cutWho, _cutTo, _beat);
                }
                if (!_typedAll) { Type(dt); return; }
                if (advanceMark != null) advanceMark.anchoredPosition = _markBase + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 7f);
                if (autoAdvance && Time.time >= _autoAt) Step();
                return;
            }
            if (_beatEnd > 0f && Time.time >= _beatEnd) Step();
        }

        /// <summary>The letterbox bars, the fade and a card's words, as they stand.</summary>
        private void ShowScreen()
        {
            float open = _bars * _bars * (3f - 2f * _bars);
            if (barTop != null) barTop.sizeDelta = new Vector2(barTop.sizeDelta.x, barHeight * open);
            if (barBottom != null) barBottom.sizeDelta = new Vector2(barBottom.sizeDelta.x, barHeight * open);
            if (fadeImage != null) fadeImage.color = new Color(0f, 0f, 0f, _fade);
            if (_card && cardLabel != null) cardLabel.color = new Color(1f, 1f, 1f, Mathf.Clamp01((_fade - 0.55f) / 0.45f));
        }

        private void ShowVeil()
        {
            if (_veilMat != null) _veilMat.SetColor("_Color", new Color(0f, 0f, 0f, veilDarkness * _veil));
        }

        /// <summary>
        /// The screen, in front of your eyes: level in VR, where you're looking on a desktop. It comes nearer (and smaller,
        /// so it looks the same size) when something solid is in the way: whoever you're talking to, a counter, a wall.
        /// </summary>
        private void PlaceScreen(Vector3 eye, Quaternion gaze, float want, bool tilt)
        {
            if (screenRoot == null) return;
            Transform tr = screenRoot.transform;
            if (_screenScale == Vector3.zero) _screenScale = tr.localScale;
            Vector3 look = gaze * Vector3.forward;
            Vector3 flat = new Vector3(look.x, 0f, look.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 dir = flat;
            if (tilt)
            {
                float y = Mathf.Clamp(look.y, -0.45f, 0.45f);
                dir = (flat * Mathf.Sqrt(1f - y * y) + Vector3.up * y).normalized;
            }
            float d = want;
            RaycastHit hit;
            if (Physics.SphereCast(eye, 0.2f, dir, out hit, want + 0.3f, SolidMask, QueryTriggerInteraction.Ignore)) d = Mathf.Clamp(hit.distance - 0.1f, 0.45f, want);
            tr.SetPositionAndRotation(eye + dir * d, Quaternion.LookRotation(dir, Vector3.up));
            tr.localScale = _screenScale * (d / Mathf.Max(0.1f, want));
        }

        // ------------------------------------------------------------------ the cast

        private bool On(int a) { return a >= 0 && a < _castCount && _cast[a] != null && _castOn[a]; }

        /// <summary>Who a beat is about, as far as the stage goes: someone on it, you, or nobody.</summary>
        private int Who(int actor)
        {
            if (actor == Cutscene.ACTOR_PLAYER) return actor;
            return On(actor) ? actor : Cutscene.ACTOR_NOBODY;
        }

        /// <summary>The first of the cast who is on stage.</summary>
        private int Lead()
        {
            for (int a = 0; a < _castCount; a++)
                if (On(a)) return a;
            return Cutscene.ACTOR_NOBODY;
        }

        /// <summary>Where they stand (their mark, even while they are still walking to it); you, where you stand.</summary>
        private Vector3 StandOf(int who)
        {
            if (who >= 0 && who < MAX_CAST && _cast[who] != null) return _castMark[who];
            return _feet;
        }

        private Vector3 FaceOf(int who)
        {
            if (who >= 0 && who < MAX_CAST && _cast[who] != null) return _castMark[who] + Vector3.up * _castFace[who];
            return _head;
        }

        /// <summary>How big they are next to a grown wizard (1): shots stand further back from the tall, nearer the small.</summary>
        private float SizeOf(int who)
        {
            if (who >= 0 && who < MAX_CAST && _cast[who] != null) return Mathf.Clamp(_castFace[who] / 1.6f, 0.55f, 1.6f);
            return _size;
        }

        /// <summary>The cast take their places: bodies that are there turn to you, stand-ins come on at marks round you.</summary>
        private void SetStage(Quaternion gaze)
        {
            Cutscene cs = _current;
            Transform lead = cs.directed && cs.ActorCount() > 0 ? cs.ActorBody(0) : null;
            if (lead != null)
            {
                // The scene's own character has to be there, and near you: if not, it plays on its placed shots.
                Animator leadAnim = cs.ActorAnimator(0);
                GameObject leadModel = leadAnim != null ? leadAnim.gameObject : lead.gameObject;
                Vector3 off = lead.position - _feet;
                off.y = 0f;
                if (!leadModel.activeInHierarchy || off.sqrMagnitude > 196f) lead = null;
            }
            _directed = lead != null;
            _castCount = _directed ? Mathf.Min(cs.ActorCount(), MAX_CAST) : 0;
            Vector3 fwd = gaze * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            _axis = -fwd.normalized;
            if (lead != null)
            {
                Vector3 d = _feet - lead.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.04f) _axis = d.normalized;
            }
            _right = Vector3.Cross(Vector3.up, _axis);
            _side = 1f;
            for (int s = 0; s < _slotTaken.Length; s++) _slotTaken[s] = false;

            for (int a = 0; a < MAX_CAST; a++)
            {
                _cast[a] = null;
                _castAnim[a] = null;
                _castOriginal[a] = null;
                _castOn[a] = false;
                _castHid[a] = false;
                _castOriginalHid[a] = false;
                _castTurns[a] = false;
                _castWalk[a] = 0f;
                _castWalkTo[a] = 0f;
                if (a >= _castCount) continue;
                Transform body = cs.ActorBody(a);
                if (body == null) continue;
                _cast[a] = body;
                _castAnim[a] = cs.ActorAnimator(a);
                _castFace[a] = cs.ActorFace(a);
                _castRestRot[a] = body.rotation;
                _castRestPos[a] = body.position;
                _castMark[a] = body.position;
                _castStandIn[a] = cs.IsStandIn(a);
                bool late = cs.IsLate(a);
                if (_castStandIn[a])
                {
                    // Out of sight until now. If the real one stands in view somewhere, they step out for the scene.
                    GameObject original = cs.ActorOriginal(a);
                    if (original != null && original.activeSelf)
                    {
                        original.SetActive(false);
                        _castOriginal[a] = original;
                        _castOriginalHid[a] = true;
                    }
                    _castMark[a] = Mark(lead);
                    body.position = _castMark[a];
                    Vector3 toYou = _feet - _castMark[a];
                    toYou.y = 0f;
                    if (toYou.sqrMagnitude > 0.01f) body.rotation = Quaternion.LookRotation(-toYou, Vector3.up);
                    body.gameObject.SetActive(!late);
                    _castOn[a] = !late;
                }
                else
                {
                    GameObject model = _castAnim[a] != null ? _castAnim[a].gameObject : body.gameObject;
                    bool there = model.activeInHierarchy;
                    if (there && late)
                    {
                        model.SetActive(false);
                        _castHid[a] = true;
                        there = false;
                    }
                    _castOn[a] = there;
                }
                _castAway[a] = Away(_castMark[a]);
                if (_castOn[a])
                {
                    Face(a, _feet);
                    if (_castAnim[a] != null) _castAnim[a].SetBool("Scene", true);
                }
            }
        }

        /// <summary>The way off from a mark: away from you.</summary>
        private Vector3 Away(Vector3 mark)
        {
            Vector3 d = mark - _feet;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : -_axis;
        }

        /// <summary>
        /// A mark for a stand-in: on a ring round you as far off as the first of the cast, fanned out either side of them
        /// (nearest first), on solid ground, with room to stand and in your sight. Failing all that, at their shoulder.
        /// </summary>
        private Vector3 Mark(Transform lead)
        {
            Vector3 centre = lead != null ? lead.position : _feet - _axis * 2.4f;
            Vector3 toLead = centre - _feet;
            toLead.y = 0f;
            float d = Mathf.Clamp(toLead.magnitude, 2.2f, 6f);
            float step = Mathf.Clamp(2f * Mathf.Asin(Mathf.Clamp01(0.65f / d)) * Mathf.Rad2Deg, 14f, 40f);
            int first = -1;
            for (int s = 0; s < _slotTaken.Length; s++)
            {
                if (_slotTaken[s]) continue;
                if (first < 0) first = s;
                float turn = (s % 2 == 0 ? 1f : -1f) * step * (s / 2 + 1);
                if (Mathf.Abs(turn) > 150f) continue;
                Vector3 p = _feet + Quaternion.Euler(0f, turn, 0f) * (-_axis) * d;
                p.y = centre.y;
                if (!GoodMark(p)) continue;
                _slotTaken[s] = true;
                return _ground;
            }
            // Nowhere on the ring is any good: at the first one's shoulder instead (the same side as their place on the ring:
            // _right is your left as you face them).
            if (first < 0) first = 0;
            _slotTaken[first] = true;
            return centre - _right * ((first % 2 == 0 ? 1f : -1f) * 1.1f * (first / 2 + 1)) + _axis * 0.25f;
        }

        /// <summary>Solid ground just under the spot (left in _ground), nothing solid where they'd stand, and you can see them.</summary>
        private bool GoodMark(Vector3 p)
        {
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 1.3f, Vector3.down, out hit, 2.6f, SolidMask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - p.y) > 0.7f) return false;
            _ground = hit.point;
            if (!Free(_ground + Vector3.up * 1.05f, 0.4f)) return false;
            return Sees(_head, _ground + Vector3.up * 1.4f);
        }

        /// <summary>
        /// Who 'a' is turned to (whoever stands where they are looking: you, when it's nobody on stage), so a cutaway to
        /// them is taken from in front of their face, not from wherever the last line was spoken.
        /// </summary>
        private int Facing(int a)
        {
            if (a < 0 || a >= _castCount || _cast[a] == null || !_castTurns[a]) return Cutscene.ACTOR_PLAYER;
            for (int k = 0; k < _castCount; k++)
            {
                if (k == a || !On(k)) continue;
                Vector3 d = _castLook[a] - _castMark[k];
                d.y = 0f;
                if (d.sqrMagnitude < 0.09f) return k;
            }
            return Cutscene.ACTOR_PLAYER;
        }

        /// <summary>'a' turns (over a moment) to face a point.</summary>
        private void Face(int a, Vector3 point)
        {
            if (a < 0 || a >= MAX_CAST || _cast[a] == null) return;
            _castLook[a] = point;
            _castTurns[a] = true;
        }

        /// <summary>Whoever speaks turns to whoever they speak to; everyone else turns to listen.</summary>
        private void Address(int who, int listener)
        {
            for (int a = 0; a < _castCount; a++)
            {
                if (!On(a)) continue;
                if (a == who)
                {
                    if (listener != Cutscene.ACTOR_NOBODY) Face(a, StandOf(listener));
                }
                else if (who != Cutscene.ACTOR_NOBODY) Face(a, StandOf(who));
            }
        }

        /// <summary>
        /// Who a line is spoken to: whoever its tag says; else whoever they were already speaking to; else whoever spoke
        /// before them; else you.
        /// </summary>
        private int Listener(int i, int who)
        {
            int to = Who(_current.TargetAt(i));
            if (to != Cutscene.ACTOR_NOBODY && to != who) return to;
            if (who == _lastSpeaker && _lastListener != who && (_lastListener == Cutscene.ACTOR_PLAYER || On(_lastListener))) return _lastListener;
            if (_lastSpeaker != who && (_lastSpeaker == Cutscene.ACTOR_PLAYER || On(_lastSpeaker))) return _lastSpeaker;
            return who == Cutscene.ACTOR_PLAYER ? Lead() : Cutscene.ACTOR_PLAYER;
        }

        private void Emote(int who, int emote)
        {
            if (who < 0 || who >= MAX_CAST || _castAnim[who] == null) return;
            string trigger = EmoteTrigger(emote);
            if (trigger.Length > 0) _castAnim[who].SetTrigger(trigger);
        }

        /// <summary>The Animator trigger for an emote (WizardsVRCSceneBuilder.Npcs.cs makes a clip for each).</summary>
        private string EmoteTrigger(int emote)
        {
            switch (emote)
            {
                case Cutscene.EMOTE_NONE: return "";
                case Cutscene.EMOTE_NOD: return "Nod";
                case Cutscene.EMOTE_SHAKE: return "Shake";
                case Cutscene.EMOTE_THINK: return "Think";
                case Cutscene.EMOTE_POINT: return "Point";
                case Cutscene.EMOTE_BOW: return "Bow";
                case Cutscene.EMOTE_SURPRISE: return "Surprise";
                case Cutscene.EMOTE_CHEER: return "Cheer";
                case Cutscene.EMOTE_LAUGH: return "Laugh";
                case Cutscene.EMOTE_ANGRY: return "Angry";
                case Cutscene.EMOTE_SHRUG: return "Shrug";
                case Cutscene.EMOTE_WAVE: return "Wave";
                case Cutscene.EMOTE_SAD: return "Sad";
            }
            return "Talk";
        }

        /// <summary>Someone who wasn't there walks on to their mark. False when they are there already (or can't be).</summary>
        private bool Enter(int a)
        {
            if (a < 0 || a >= _castCount || _cast[a] == null || _castOn[a]) return false;
            if (_castStandIn[a]) _cast[a].gameObject.SetActive(true);
            else
            {
                // Only someone this scene sent off (or held back) comes on: a body its own story has hidden stays hidden.
                if (!_castHid[a] || _castAnim[a] == null) return false;
                _castAnim[a].gameObject.SetActive(true);
                _castHid[a] = false;
            }
            _castOn[a] = true;
            _castWalk[a] = 1f;
            _castWalkTo[a] = 0f;
            _cast[a].position = _castMark[a] + _castAway[a] * StepAside;
            Vector3 toYou = _feet - _castMark[a];
            toYou.y = 0f;
            if (toYou.sqrMagnitude > 0.01f) _cast[a].rotation = Quaternion.LookRotation(-toYou, Vector3.up);
            Face(a, _feet);
            if (_castAnim[a] != null) _castAnim[a].SetBool("Scene", true);
            return true;
        }

        /// <summary>Someone turns and walks off; they are gone from the scene from here on.</summary>
        private void Exit(int a)
        {
            _castOn[a] = false;
            _castWalkTo[a] = 1f;
            Face(a, _castMark[a] + _castAway[a] * 4f);
        }

        /// <summary>Turns everyone towards what they're facing, and walks on (or off) whoever is on their way.</summary>
        private void MoveCast(float dt)
        {
            float turn = 1f - Mathf.Exp(-7f * dt);
            for (int a = 0; a < _castCount; a++)
            {
                Transform body = _cast[a];
                if (body == null) continue;
                bool walking = _castWalk[a] != _castWalkTo[a];
                if (walking)
                {
                    float w = Mathf.MoveTowards(_castWalk[a], _castWalkTo[a], dt / 0.9f);
                    _castWalk[a] = w;
                    if (w >= 1f && _castWalkTo[a] >= 1f)
                    {
                        // Off: out of sight until the scene ends.
                        if (_castStandIn[a]) body.gameObject.SetActive(false);
                        else if (_castAnim[a] != null)
                        {
                            _castAnim[a].gameObject.SetActive(false);
                            _castHid[a] = true;
                        }
                        continue;
                    }
                    float ease = w * w * (3f - 2f * w);
                    body.position = _castMark[a] + _castAway[a] * (StepAside * ease) + Vector3.up * (Mathf.Abs(Mathf.Sin(w * 9.42f)) * 0.035f);
                }
                if ((!_castOn[a] && !walking) || !_castTurns[a]) continue;
                Vector3 d = _castLook[a] - body.position;
                d.y = 0f;
                if (d.sqrMagnitude < 0.01f) continue;
                // (The models face their own -Z.)
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(-d, Vector3.up), turn);
            }
        }

        /// <summary>The scene is over: stand-ins go, anyone hidden comes back, and the rest are left to turn back.</summary>
        private void ReleaseCast()
        {
            for (int a = 0; a < MAX_CAST; a++)
            {
                Transform body = _cast[a];
                if (body == null) continue;
                Animator anim = _castAnim[a];
                if (_castOriginalHid[a] && _castOriginal[a] != null) _castOriginal[a].SetActive(true);
                _castOriginalHid[a] = false;
                _castOriginal[a] = null;
                if (_castStandIn[a])
                {
                    body.gameObject.SetActive(false);
                    body.SetPositionAndRotation(_castRestPos[a], _castRestRot[a]);
                    _cast[a] = null;
                    continue;
                }
                if (_castHid[a] && anim != null) anim.gameObject.SetActive(true);
                _castHid[a] = false;
                body.position = _castRestPos[a];
                if (anim != null && anim.gameObject.activeInHierarchy) anim.SetBool("Scene", false);
            }
            _castCount = 0;
            _settle = 0.9f;
        }

        /// <summary>After a scene: its cast turn back the way they stood, and the dark round you lifts.</summary>
        private void Settle(float dt)
        {
            _settle -= dt;
            if (_settle <= 0f) { FinishSettle(); return; }
            float turn = 1f - Mathf.Exp(-6f * dt);
            for (int a = 0; a < MAX_CAST; a++)
                if (_cast[a] != null) _cast[a].rotation = Quaternion.Slerp(_cast[a].rotation, _castRestRot[a], turn);
            if (_veil > 0f)
            {
                _veil = Mathf.MoveTowards(_veil, 0f, dt / 0.45f);
                ShowVeil();
            }
        }

        private void FinishSettle()
        {
            for (int a = 0; a < MAX_CAST; a++)
            {
                if (_cast[a] == null) continue;
                _cast[a].rotation = _castRestRot[a];
                _cast[a] = null;
            }
            _settle = 0f;
            _veil = 0f;
            if (veil != null) veil.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ the camera

        /// <summary>
        /// How a line is framed when its tag doesn't say: the scene opens wide (everyone, when there are several) and then
        /// goes in to its first speaker, a new speaker gets a waist-up shot, someone who goes on talking is cut between
        /// over the listener's shoulder, close, waist-up and wide, a short sharp line comes in close, a question put to
        /// you is seen over your shoulder, and a scene of any length closes wide.
        /// </summary>
        private int SayFraming(int i, int who, int listener)
        {
            int framing = _current.FramingAt(i);
            if (framing != Cutscene.SHOT_AUTO) return framing;
            if (who == Cutscene.ACTOR_PLAYER) return Cutscene.SHOT_YOU;
            int onStage = 0;
            for (int a = 0; a < _castCount; a++)
                if (On(a)) onStage++;
            if (who == Cutscene.ACTOR_NOBODY) return onStage >= 2 ? Cutscene.SHOT_GROUP : Cutscene.SHOT_WIDE;
            if (_says == 0)
            {
                _cycle = 0;
                return onStage >= 2 ? Cutscene.SHOT_GROUP : Cutscene.SHOT_WIDE;
            }
            if (who != _lastSpeaker)
            {
                _cycle = 1;
                framing = Cutscene.SHOT_MEDIUM;
            }
            else
            {
                int k = _cycle % 5;
                _cycle++;
                framing = k == 1 ? Cutscene.SHOT_SHOULDER : (k == 2 ? Cutscene.SHOT_CLOSE : (k == 4 ? Cutscene.SHOT_WIDE : Cutscene.SHOT_MEDIUM));
            }
            if (framing != Cutscene.SHOT_CLOSE && _text.Length <= 48 && _text.EndsWith("!")) framing = Cutscene.SHOT_CLOSE;
            else if (framing == Cutscene.SHOT_MEDIUM && listener == Cutscene.ACTOR_PLAYER && _text.EndsWith("?")) framing = Cutscene.SHOT_SHOULDER;
            if (_says >= 3 && _shotFraming != Cutscene.SHOT_WIDE && LastLine(i)) framing = Cutscene.SHOT_WIDE;
            return framing;
        }

        /// <summary>Whether no line follows beat i (on the path your answers have taken so far).</summary>
        private bool LastLine(int i)
        {
            Cutscene cs = _current;
            int count = cs.LineCount();
            for (int j = i + 1; j < count; j++)
            {
                if (cs.KindAt(j) != Cutscene.BEAT_SAY) continue;
                int branch = cs.BranchAt(j);
                if (branch == 0 || branch == _answer) return false;
            }
            return true;
        }

        /// <summary>
        /// Cuts to a shot of 'subject' with 'other' (who they're speaking to): the framing asked for if it has a clear view,
        /// else the nearest framing that has, else the scene's placed shot for this beat, else it stays where it is.
        /// </summary>
        private void Shoot(int framing, int subject, int other, int beat)
        {
            if (filmCamera == null) return;
            if (subject == Cutscene.ACTOR_NOBODY) subject = Lead();
            if (subject == Cutscene.ACTOR_NOBODY) subject = Cutscene.ACTOR_PLAYER;
            if (other == subject || (other >= 0 && !On(other)) || other == Cutscene.ACTOR_NOBODY)
                other = subject == Cutscene.ACTOR_PLAYER ? Lead() : Cutscene.ACTOR_PLAYER;
            // The same shot again: let it run.
            if (_hasShot && _placed == null && framing == _shotFraming && subject == _shotSubject && other == _shotOther) return;

            bool found = Find(framing, subject, other);
            for (int k = 0; k < 3 && !found; k++) found = Find(NextBest(framing, k), subject, other);
            if (!found)
            {
                Transform placed = _current.ShotAt(beat);
                if (placed != null)
                {
                    if (placed != _placed || !_hasShot) Cut(placed.position, placed.rotation, _baseFov, 1f, 2.5f);
                    _placed = placed;
                    return;
                }
                if (_hasShot) return;
                // Nowhere has a clear view and nothing was placed: the plain shot, whatever is in the way.
                if (!Compose(framing, subject, other, 1f, 1f)) Compose(Cutscene.SHOT_MEDIUM, subject, other, 1f, 1f);
            }
            _placed = null;
            _shotFraming = framing;
            _shotSubject = subject;
            _shotOther = other;
            Vector3 aim = _tryLook - _tryPos;
            if (aim.sqrMagnitude < 0.0001f) aim = -_axis;
            // A slow push in: least on a close shot, most on a wide one.
            bool wide = framing == Cutscene.SHOT_WIDE || framing == Cutscene.SHOT_GROUP;
            bool tight = framing == Cutscene.SHOT_CLOSE;
            Cut(_tryPos, Quaternion.LookRotation(aim, Vector3.up), _tryFov, wide ? 0.7f : (tight ? 0.17f : 0.3f), wide ? 0.6f : (tight ? 0.12f : 0.25f));
        }

        /// <summary>The framings to fall back on, nearest in kind first, when one has no clear view.</summary>
        private int NextBest(int framing, int k)
        {
            switch (framing)
            {
                case Cutscene.SHOT_WIDE: return k == 0 ? Cutscene.SHOT_MEDIUM : (k == 1 ? Cutscene.SHOT_SHOULDER : Cutscene.SHOT_CLOSE);
                case Cutscene.SHOT_GROUP: return k == 0 ? Cutscene.SHOT_WIDE : (k == 1 ? Cutscene.SHOT_MEDIUM : Cutscene.SHOT_CLOSE);
                case Cutscene.SHOT_CLOSE: return k == 0 ? Cutscene.SHOT_MEDIUM : (k == 1 ? Cutscene.SHOT_SHOULDER : Cutscene.SHOT_WIDE);
                case Cutscene.SHOT_SHOULDER: return k == 0 ? Cutscene.SHOT_MEDIUM : (k == 1 ? Cutscene.SHOT_CLOSE : Cutscene.SHOT_WIDE);
                case Cutscene.SHOT_YOU: return k == 0 ? Cutscene.SHOT_CLOSE : (k == 1 ? Cutscene.SHOT_WIDE : Cutscene.SHOT_GROUP);
            }
            return k == 0 ? Cutscene.SHOT_CLOSE : (k == 1 ? Cutscene.SHOT_SHOULDER : Cutscene.SHOT_WIDE);
        }

        /// <summary>
        /// A clear view for a framing: from the side the scene has been shot from, then the other side, then both again
        /// from closer in. Crossing to the other side moves the scene there, so later shots stay with it.
        /// </summary>
        private bool Find(int framing, int subject, int other)
        {
            for (int v = 0; v < 4; v++)
            {
                float flip = v % 2 == 0 ? 1f : -1f;
                if (!Compose(framing, subject, other, flip, v < 2 ? 1f : 0.72f)) continue;
                if (!Clear(subject, other)) continue;
                if (flip < 0f) _side = -_side;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Where the camera stands and what it looks at for a framing (left in _tryPos, _tryLook, _tryFov). 'flip' -1 puts
        /// it on the other side of the pair; 'near' under 1 brings it in. Subjects sit high in the picture, clear of the
        /// talk window across the bottom of it.
        /// </summary>
        private bool Compose(int framing, int subject, int other, float flip, float near)
        {
            Vector3 fs = FaceOf(subject);
            Vector3 fo = FaceOf(other);
            float s = SizeOf(subject);
            float so = SizeOf(other);
            float floor = Mathf.Min(StandOf(subject).y, _feet.y);
            // The line from whoever is in shot to whoever they're speaking to, and the side of it the camera keeps to.
            Vector3 u = fo - fs;
            u.y = 0f;
            float gap = u.magnitude;
            if (gap < 0.2f)
            {
                u = subject == Cutscene.ACTOR_PLAYER ? -_axis : _axis;
                gap = 0.2f;
            }
            else u /= gap;
            Vector3 r = Vector3.Cross(Vector3.up, u);
            float sgn = (Vector3.Dot(r, _right) * _side >= 0f ? 1f : -1f) * flip;
            // Two who stand close together: a shot of one face from between them would be side-on, so it is taken past
            // the other's shoulder instead.
            bool single = framing == Cutscene.SHOT_MEDIUM || framing == Cutscene.SHOT_CLOSE || framing == Cutscene.SHOT_YOU;
            if (single && other != Cutscene.ACTOR_NOBODY && gap < 1.5f * s) framing = Cutscene.SHOT_SHOULDER;
            _tryFraming = framing;

            if (framing == Cutscene.SHOT_GROUP)
            {
                // Everyone, from behind your shoulder: you in the foreground, the cast facing you.
                Vector3 sum = _head;
                int n = 1;
                for (int a = 0; a < _castCount; a++)
                {
                    if (!On(a)) continue;
                    sum += FaceOf(a);
                    n++;
                }
                Vector3 centre = sum / n;
                float reach = Flat(_head - centre);
                for (int a = 0; a < _castCount; a++)
                    if (On(a)) reach = Mathf.Max(reach, Flat(FaceOf(a) - centre));
                float back = Mathf.Max(3.1f, (reach + 1.1f) / 0.72f) * near;
                _tryFov = 50f;
                _tryPos = centre + (_axis * 0.82f + _right * (0.57f * _side * flip)) * back + Vector3.up * (0.35f + 0.1f * back);
                _tryLook = centre - Vector3.up * 0.8f;
            }
            else if (framing == Cutscene.SHOT_WIDE)
            {
                // The pair, head to foot, from the side: a little round towards the listener, so the speaker's face shows.
                float big = Mathf.Max(s, so);
                Vector3 mid = (fs + fo) * 0.5f;
                float back = Mathf.Max(3.3f * big, gap * 0.62f + 1.6f) * near;
                _tryFov = 50f;
                _tryPos = mid + (r * (0.94f * sgn) + u * 0.34f) * back - Vector3.up * (0.25f * big);
                _tryLook = mid - Vector3.up * (0.85f * big);
            }
            else if (framing == Cutscene.SHOT_SHOULDER)
            {
                // Past the listener's shoulder at the speaker.
                if (other == Cutscene.ACTOR_NOBODY) return false;
                _tryFov = 40f;
                // (A little lower when the speaker is the taller, so the listener's head still shows in the corner.)
                _tryPos = fo + u * (0.8f * so * near) + r * (0.55f * so * sgn) + Vector3.up * (0.12f * so - 0.25f * Mathf.Max(0f, fs.y - fo.y));
                // Aimed a little to the listener's side of the speaker: the shoulder fills one third of the picture and
                // the speaker's face sits in the opposite one.
                float reach = Vector3.Distance(_tryPos, fs);
                _tryLook = fs - r * (0.17f * reach * sgn) - Vector3.up * (EyeLine * reach * Mathf.Tan(_tryFov * 0.5f * Mathf.Deg2Rad));
            }
            else
            {
                // One face: waist-up (medium, and your own reaction shot) or head and shoulders (close), from in front and
                // a little to the side, on the listener's side of them but never from behind the listener's head.
                bool close = framing == Cutscene.SHOT_CLOSE;
                _tryFov = close ? 34f : 40f;
                float back = (close ? 1.37f : 1.98f) * s * near;
                float angle = (close ? 20f : 27f) * Mathf.Deg2Rad;
                float room = gap - 0.5f;
                if (back * Mathf.Cos(angle) > room)
                {
                    // Come in a little, then swing round to the side.
                    back = Mathf.Max(back * 0.75f, room / Mathf.Cos(angle));
                    if (back * Mathf.Cos(angle) > room) angle = Mathf.Acos(Mathf.Clamp(room / back, 0.2f, 1f));
                }
                _tryPos = fs + (u * Mathf.Cos(angle) + r * (Mathf.Sin(angle) * sgn)) * back - Vector3.up * (0.03f * s);
                _tryLook = fs - Vector3.up * (EyeLine * back * Mathf.Tan(_tryFov * 0.5f * Mathf.Deg2Rad));
            }
            if (_tryPos.y < floor + 0.35f) _tryPos.y = floor + 0.35f;
            return true;
        }

        private float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>Whether the shot just composed has room for the camera and sees who it should.</summary>
        private bool Clear(int subject, int other)
        {
            int framing = _tryFraming;   // (what Compose made of the framing asked for)
            if (!Free(_tryPos, 0.22f)) return false;
            if (framing == Cutscene.SHOT_GROUP)
            {
                // Everyone's face in sight, past the scenery and past you (they may well overlap each other).
                for (int a = 0; a < _castCount; a++)
                    if (On(a) && (!Sees(FaceOf(a), _tryPos) || Blocked(_tryPos, FaceOf(a), a, a, false))) return false;
                return Sees(_head, _tryPos);
            }
            bool pair = framing == Cutscene.SHOT_WIDE || framing == Cutscene.SHOT_SHOULDER;
            if (!Sees(FaceOf(subject), _tryPos) || Blocked(_tryPos, FaceOf(subject), subject, pair ? other : subject, true)) return false;
            if (!pair || other == Cutscene.ACTOR_NOBODY) return true;
            if (!Sees(FaceOf(other), _tryPos)) return false;
            return framing != Cutscene.SHOT_WIDE || !Blocked(_tryPos, FaceOf(other), subject, other, true);
        }

        /// <summary>
        /// Whether somebody stands between the camera and a face it should see: you, and ('cast') anyone on stage, other
        /// than 'a' and 'b' (who are meant to be in the shot). Bodies are taken as posts a third of a metre round: the
        /// cast's click boxes may be switched off while the screen is up, and stand-ins have none.
        /// </summary>
        private bool Blocked(Vector3 cam, Vector3 face, int a, int b, bool cast)
        {
            float dx = face.x - cam.x;
            float dz = face.z - cam.z;
            float span = dx * dx + dz * dz;
            if (span < 0.0001f) return false;
            int last = cast ? _castCount : 0;
            for (int k = -1; k < last; k++)
            {
                int who = k < 0 ? Cutscene.ACTOR_PLAYER : k;
                if (who == a || who == b || (k >= 0 && !On(k))) continue;
                Vector3 foot = StandOf(who);
                float size = SizeOf(who);
                // Where the view line passes nearest their post, seen from above.
                float t = Mathf.Clamp01(((foot.x - cam.x) * dx + (foot.z - cam.z) * dz) / span);
                if (t > 0.97f) continue;   // (beside or behind whoever is in shot)
                float px = cam.x + dx * t - foot.x;
                float pz = cam.z + dz * t - foot.z;
                if (px * px + pz * pz > 0.34f * 0.34f * size * size) continue;
                float y = cam.y + (face.y - cam.y) * t;
                if (y > foot.y - 0.1f && y < FaceOf(who).y + 0.3f * size) return true;
            }
            return false;
        }

        /// <summary>Nothing solid within 'radius' of a point (the cast's own click boxes aside).</summary>
        private bool Free(Vector3 point, float radius)
        {
            Collider[] hits = Physics.OverlapSphere(point, radius, SolidMask, QueryTriggerInteraction.Ignore);
            if (hits == null) return true;
            for (int i = 0; i < hits.Length; i++)
                if (hits[i] != null && !OfCast(hits[i].transform)) return false;
            return true;
        }

        /// <summary>Nothing solid between two points (the cast's own click boxes aside).</summary>
        private bool Sees(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float left = d.magnitude;
            if (left < 0.02f) return true;
            Vector3 dir = d / left;
            Vector3 at = from;
            RaycastHit hit;
            for (int n = 0; n < 5; n++)
            {
                if (!Physics.Raycast(at, dir, out hit, left, SolidMask, QueryTriggerInteraction.Ignore)) return true;
                if (hit.collider == null || !OfCast(hit.collider.transform)) return false;
                // One of the cast's click boxes: on through it (a ray that starts inside a box doesn't hit that box).
                float step = hit.distance + 0.06f;
                at += dir * step;
                left -= step;
                if (left <= 0.02f) return true;
            }
            return false;
        }

        private bool OfCast(Transform t)
        {
            for (int a = 0; a < MAX_CAST; a++)
            {
                Transform body = _cast[a];
                if (body != null && (t == body || t.IsChildOf(body))) return true;
            }
            return false;
        }

        /// <summary>The camera cuts to a shot and drifts forward along it: 'pace' times the scene's dolly speed, up to 'reach' metres.</summary>
        private void Cut(Vector3 position, Quaternion rotation, float fov, float pace, float reach)
        {
            _camPos = position;
            _camRot = rotation;
            _camDrift = rotation * Vector3.forward;
            _camSpeed = (_current != null ? _current.dollySpeed : 0.15f) * pace;
            _camReach = reach;
            _shotStart = Time.time;
            _hasShot = true;
            if (filmCamera == null) return;
            filmCamera.fieldOfView = fov;
            filmCamera.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>A scene with no director: the shot placed for this beat (the one before goes on running if it's the same).</summary>
        private void Placed(int i)
        {
            Transform shot = _current.ShotAt(i);
            if (shot == null || (shot == _placed && _hasShot)) return;
            _placed = shot;
            Cut(shot.position, shot.rotation, _baseFov, 1f, 2.5f);
        }

        private void Stop(bool notify)
        {
            Cutscene done = _current;
            if (done == null) return;
            _current = null;
            _hasShot = false;
            if (voiceSource != null) voiceSource.Stop();
            if (filmCamera != null)
            {
                filmCamera.enabled = false;
                filmCamera.fieldOfView = _baseFov;
            }
            if (screenRoot != null) screenRoot.SetActive(false);
            ReleaseCast();
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local)) local.Immobilize(false);
            // (Last of all: what a scene's ending does may well be to start the next one.)
            if (notify && done.onFinished != null && done.onFinishedEvent.Length > 0)
                done.onFinished.SendCustomEvent(done.onFinishedEvent);
        }

        // ------------------------------------------------------------------ spell films (SpellCinematic)

        public bool IsFilming() { return _film; }

        /// <summary>
        /// Opens the screen in front of you for a spell's film: 'title' (the caster) and 'caption' (the spell) on the lower
        /// bar in 'color'. Shots come from <see cref="SetFilmShot"/>.
        /// </summary>
        public void PlayFilm(string title, string caption, Color color)
        {
            if (_current != null || filmCamera == null) return;
            _film = true;
            _filmTitle = title;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && screenRoot != null)
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                if (_screenScale == Vector3.zero) _screenScale = screenRoot.transform.localScale;
                screenRoot.transform.position = head.position + forward * filmScreenDistance;
                screenRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                screenRoot.transform.localScale = _screenScale;
            }
            // No talk window over a film: the picture is the whole of it.
            if (talkWindow != null) talkWindow.SetActive(false);
            if (choicePanel != null) choicePanel.SetActive(false);
            if (cardLabel != null) cardLabel.gameObject.SetActive(false);
            if (nextButton != null) nextButton.SetActive(false);
            if (autoButton != null) autoButton.SetActive(false);
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            if (skipButton != null) skipButton.SetActive(true);
            if (filmLabel != null)
            {
                filmLabel.text = caption.Length > 0 ? title + "  -  " + caption : title;
                filmLabel.color = color;
                filmLabel.gameObject.SetActive(true);
            }
            _card = false;
            _bars = 1f;
            _fade = 0f;
            _fadeTarget = 0f;
            ShowScreen();
            if (filmCullingMask != -1) filmCamera.cullingMask = filmCullingMask;
            filmCamera.fieldOfView = _baseFov;
            filmCamera.enabled = true;
            if (screenRoot != null) screenRoot.SetActive(true);
        }

        /// <summary>Where the film camera is this frame.</summary>
        public void SetFilmShot(Vector3 position, Quaternion rotation)
        {
            if (_film && filmCamera != null) filmCamera.transform.SetPositionAndRotation(position, rotation);
        }

        public void SetFilmCaption(string caption)
        {
            if (_film && filmLabel != null) filmLabel.text = caption.Length > 0 ? _filmTitle + "  -  " + caption : _filmTitle;
        }

        public void StopFilm()
        {
            if (!_film) return;
            _film = false;
            if (filmCamera != null)
            {
                filmCamera.enabled = false;
                if (_storyMask != -1) filmCamera.cullingMask = _storyMask;
            }
            if (screenRoot != null) screenRoot.SetActive(false);
            if (filmLabel != null) filmLabel.gameObject.SetActive(false);
        }
    }
}
