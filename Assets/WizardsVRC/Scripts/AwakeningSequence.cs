using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The opening for a new character, played around the player rather than on a screen.
    /// In the dark, whispers. Your eyes flicker open on a dream: visions of a past you don't remember (the Starfall
    /// Crystal under falling stars, a rift torn in the sky, an iron legion on the march, a figure in grey iron over a
    /// shattered crystal), each in a white flash, then the title. A voice pulls you out of it and you wake, blinking,
    /// in the Headmaster's study. Greymantle tells you of his visions: a darkness beyond the edge of the Everweave that
    /// swallows every world, in every dream but one, where a wizard he had never met stood with them. Then he asks
    /// your name and the <see cref="CharacterCreator"/> opens; once you have made your wizard he finishes and sends
    /// you to your professor.
    ///
    /// The script is data: one row per beat (subtitle, speaker, seconds, how open your eyes are, which vision shows,
    /// where you are, sounds and flags), filled in by the scene builder (WizardsVRCSceneBuilder.Awakening.cs).
    /// Everything here is local to the player watching it. Skip jumps to the Headmaster's question.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AwakeningSequence : UdonSharpBehaviour
    {
        public const int PLACE_DREAM = 0;
        public const int PLACE_STUDY = 1;
        public const int STYLE_WHISPER = 0;
        public const int STYLE_SPEECH = 1;
        public const int STYLE_TITLE = 2;
        public const int FLAG_FLASH = 1;
        public const int FLAG_FLUTTER = 2;
        public const int FLAG_DRUMS_ON = 4;
        public const int FLAG_DRUMS_OFF = 8;
        public const int FLAG_HEARTBEAT = 16;
        public const int FLAG_DREAM_SOUND_OFF = 32;

        [Header("Hand-off")]
        public TeleportHelper teleporter;
        public CharacterSelectUI selectUI;
        public CharacterCreator creator;
        [Tooltip("The select screen in the study: hidden while this plays.")]
        public Canvas selectCanvas;
        public PlayerHUD hud;

        [Header("Places")]
        public Transform dreamSpot;
        public Transform wakeSpot;

        [Header("Eyes and words")]
        [Tooltip("A small inside-out sphere with the PSXEyelids material; it follows your head.")]
        public Transform lids;
        public Renderer lidRenderer;
        public Transform subtitleRoot;
        public Text speakerLabel;
        public Text subtitleLabel;
        public Text titleLabel;
        public GameObject skipButton;
        public Color whisperColor = new Color(0.78f, 0.74f, 0.95f, 1f);
        public Color speechColor = Color.white;

        [Header("Visions (shown one at a time in the dream)")]
        public GameObject[] visions;
        [Tooltip("How fast a vision drifts towards you while it is shown (m/s).")]
        public float visionDrift = 0.25f;

        [Header("Sound")]
        public AudioSource droneSource;
        public AudioSource[] whisperSources;
        public AudioSource drumSource;
        public AudioSource sfxSource;
        public AudioSource voiceSource;
        public AudioClip flashClip;
        public AudioClip heartbeatClip;

        [Header("The script (one entry per beat)")]
        public string[] beatSpeaker;
        [TextArea] public string[] beatText;
        public float[] beatSeconds;
        [Tooltip("How open your eyes are during the beat: 0 shut, 1 wide open.")]
        public float[] beatOpen;
        [Tooltip("Vision shown (-1 none).")]
        public int[] beatVision;
        public int[] beatPlace;
        public int[] beatStyle;
        public int[] beatFlags;
        public AudioClip[] beatVoice;
        [Tooltip("The beat that asks your name: the creator opens when it has been read.")]
        public int askNameBeat;
        [Tooltip("First beat after the creator (the Headmaster's answer). Use {name} and {school} in its text.")]
        public int outroStart;
        public string[] schoolNames;

        private bool _running;
        private int _slot = -1;
        private int _beat = -1;
        private float _beatStart, _beatEnd;
        private bool _waitingCreator;
        private int _place = -1;
        private float _open, _openTarget;
        private float _flash;
        private float _tint;
        private float _vignette;
        private int _vision = -1;
        private Vector3[] _visionBase;
        private float _whisperVolume, _whisperTarget;
        private float _dreamVolume = 1f;
        private float _textAlpha;
        private string _name = "";
        private int _school = -1;
        private bool _snapWords;
        private Material _lidMat;

        public bool IsRunning() { return _running; }

        private void Start()
        {
            SetActive(lids, false);
            SetActive(subtitleRoot, false);
            if (visions != null)
            {
                _visionBase = new Vector3[visions.Length];
                for (int i = 0; i < visions.Length; i++)
                {
                    if (visions[i] == null) continue;
                    _visionBase[i] = visions[i].transform.position;
                    visions[i].SetActive(false);
                }
            }
        }

        /// <summary>Plays the whole opening for a new character that will be saved in this slot.</summary>
        public void Begin(int slot)
        {
            if (_running) return;
            _slot = slot;
            _running = true;
            _waitingCreator = false;
            _name = "";
            _school = -1;
            _beat = -1;
            _place = -1;
            _open = 0f;
            _openTarget = 0f;
            _flash = 0f;
            _tint = 0.3f;
            _vignette = 0.8f;
            _vision = -1;
            _whisperVolume = 0f;
            _whisperTarget = 0f;
            _dreamVolume = 1f;
            if (lidRenderer != null && _lidMat == null) _lidMat = lidRenderer.material;
            ApplyLids();
            SetActive(lids, true);
            SetActive(subtitleRoot, true);
            if (skipButton != null) skipButton.SetActive(false);
            ShowWords("", "", STYLE_SPEECH);
            if (selectCanvas != null) selectCanvas.enabled = false;

            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me)) me.Immobilize(true);
            GoTo(PLACE_DREAM);
            if (droneSource != null) { droneSource.volume = 0f; droneSource.Play(); }
            if (whisperSources != null)
                for (int i = 0; i < whisperSources.Length; i++)
                    if (whisperSources[i] != null)
                    {
                        whisperSources[i].volume = 0f;
                        whisperSources[i].Play();
                        if (whisperSources[i].clip != null) whisperSources[i].time = whisperSources[i].clip.length * (0.31f * i % 1f);
                    }
            _beatEnd = Time.time + 1.2f;
            SendCustomEventDelayedSeconds(nameof(_ShowSkip), 3f);
        }

        public void _ShowSkip()
        {
            if (_running && !_waitingCreator && skipButton != null) skipButton.SetActive(true);
        }

        /// <summary>Skip: from the dream or the Headmaster's speech straight to his question; after the creator, to the end.</summary>
        public void Skip()
        {
            if (!_running || _waitingCreator) return;
            if (_beat >= outroStart && _school >= 0) { Finish(); return; }
            if (_beat >= askNameBeat) return;
            _beat = askNameBeat - 1;
            _open = 0f;
            Next();
            _open = 1f;
            _flash = 0f;
        }

        /// <summary>Sent by the creator once the character exists.</summary>
        public void OnCharacterCreated(string name, int school)
        {
            _name = name;
            _school = school;
            _waitingCreator = false;
            if (!_running) return;
            if (skipButton != null) skipButton.SetActive(true);
            _beat = outroStart - 1;
            Next();
        }

        private void Next()
        {
            _beat++;
            int n = beatText == null ? 0 : beatText.Length;
            if (_beat >= n || (_beat >= outroStart && _school < 0))
            {
                if (_school < 0) { OpenCreator(); return; }
                Finish();
                return;
            }
            int flags = IntAt(beatFlags, _beat);
            int place = IntAt(beatPlace, _beat);
            if (place != _place) GoTo(place);
            _openTarget = FloatAt(beatOpen, _beat);
            ShowVision(IntAt(beatVision, _beat));
            if ((flags & FLAG_FLASH) != 0)
            {
                _flash = 1f;
                PlaySfx(flashClip);
            }
            if ((flags & FLAG_HEARTBEAT) != 0) PlaySfx(heartbeatClip);
            if ((flags & FLAG_DRUMS_ON) != 0 && drumSource != null) { drumSource.volume = 1f; drumSource.Play(); }
            if ((flags & FLAG_DRUMS_OFF) != 0 && drumSource != null) drumSource.Stop();
            if ((flags & FLAG_DREAM_SOUND_OFF) != 0) _dreamVolume = 0f;

            int style = IntAt(beatStyle, _beat);
            _whisperTarget = style == STYLE_WHISPER && place == PLACE_DREAM ? 1f : (place == PLACE_DREAM ? 0.35f : 0f);
            string text = Fill(StringAt(beatText, _beat));
            ShowWords(StringAt(beatSpeaker, _beat), text, style);

            float seconds = Mathf.Max(1f, FloatAt(beatSeconds, _beat));
            AudioClip voice = beatVoice != null && _beat < beatVoice.Length ? beatVoice[_beat] : null;
            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (voice != null)
                {
                    voiceSource.clip = voice;
                    voiceSource.Play();
                    seconds = Mathf.Max(seconds, voice.length + 0.5f);
                }
            }
            _beatStart = Time.time;
            _beatEnd = Time.time + seconds;
        }

        private void OpenCreator()
        {
            _waitingCreator = true;
            _beat = askNameBeat;
            if (skipButton != null) skipButton.SetActive(false);
            _openTarget = 1f;
            if (creator != null) creator.Open(_slot);
            else Finish();
        }

        private void Finish()
        {
            _running = false;
            _waitingCreator = false;
            ShowVision(-1);
            SetActive(lids, false);
            SetActive(subtitleRoot, false);
            StopSound(droneSource);
            StopSound(drumSource);
            StopSound(voiceSource);
            if (whisperSources != null)
                for (int i = 0; i < whisperSources.Length; i++) StopSound(whisperSources[i]);
            if (creator != null) creator.Close();
            if (selectCanvas != null) selectCanvas.enabled = true;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me)) me.Immobilize(false);
            if (_place != PLACE_STUDY && wakeSpot != null && teleporter != null) teleporter.TeleportTo(wakeSpot);
            if (hud != null) hud.Refresh();
        }

        private void GoTo(int place)
        {
            _place = place;
            Transform to = place == PLACE_DREAM ? dreamSpot : wakeSpot;
            if (to != null && teleporter != null) teleporter.TeleportTo(to);
            _snapWords = true;
        }

        private void ShowVision(int v)
        {
            if (v == _vision) return;
            if (visions != null)
                for (int i = 0; i < visions.Length; i++)
                    if (visions[i] != null && visions[i].activeSelf != (i == v))
                    {
                        visions[i].SetActive(i == v);
                        if (i == v && _visionBase != null && i < _visionBase.Length) visions[i].transform.position = _visionBase[i];
                    }
            _vision = v;
        }

        private void ShowWords(string speaker, string text, int style)
        {
            _textAlpha = 0f;
            if (titleLabel != null) titleLabel.text = style == STYLE_TITLE ? text : "";
            if (speakerLabel != null) speakerLabel.text = style == STYLE_SPEECH ? speaker : "";
            if (subtitleLabel != null)
            {
                subtitleLabel.text = style == STYLE_TITLE ? "" : text;
                subtitleLabel.fontStyle = style == STYLE_WHISPER ? FontStyle.Italic : FontStyle.Normal;
            }
        }

        private string Fill(string s)
        {
            string school = schoolNames != null && _school >= 0 && _school < schoolNames.Length ? schoolNames[_school] : "";
            return s.Replace("{name}", _name).Replace("{school}", school);
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            if (!_running) return;
            float dt = Time.deltaTime;
            if (!_waitingCreator && Time.time >= _beatEnd) Next();
            if (!_running) return;

            // Eyes: ease towards the beat's openness; a flutter beat blinks a few times on the way.
            float target = _openTarget;
            if ((IntAt(beatFlags, _beat) & FLAG_FLUTTER) != 0 && !_waitingCreator)
            {
                float t = Time.time - _beatStart;
                if (t < 3.2f) target *= 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 2.4f));
            }
            float speed = target < _open ? 2.2f : 0.55f;
            _open = Mathf.MoveTowards(_open, target, speed * dt);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 1.6f);
            bool dream = _place == PLACE_DREAM;
            _tint = Mathf.MoveTowards(_tint, dream ? 0.3f : 0f, dt * 0.12f);
            _vignette = Mathf.MoveTowards(_vignette, dream ? 0.8f : (_waitingCreator ? 0f : 0.35f), dt * 0.15f);
            ApplyLids();

            // Sound: the dream's drone and whispers fade with it.
            float dreamLevel = dream ? _dreamVolume : 0f;
            if (droneSource != null) droneSource.volume = Mathf.MoveTowards(droneSource.volume, dreamLevel * 0.55f, dt * 0.4f);
            _whisperVolume = Mathf.MoveTowards(_whisperVolume, _whisperTarget * dreamLevel, dt * 0.5f);
            if (whisperSources != null)
                for (int i = 0; i < whisperSources.Length; i++)
                    if (whisperSources[i] != null) whisperSources[i].volume = _whisperVolume * 0.7f;
            if (drumSource != null && !dream) drumSource.volume = Mathf.MoveTowards(drumSource.volume, 0f, dt);

            // The vision on show drifts slowly towards you.
            if (_vision >= 0 && visions != null && _vision < visions.Length && visions[_vision] != null && dreamSpot != null)
            {
                Transform v = visions[_vision].transform;
                Vector3 toYou = dreamSpot.position - v.position;
                toYou.y = 0f;
                if (toYou.magnitude > 3.5f) v.position += toYou.normalized * visionDrift * dt;
            }

            // Words fade in, and fade out near the end of a beat.
            float left = _beatEnd - Time.time;
            float want = _waitingCreator || left > 0.6f ? 1f : Mathf.Clamp01(left / 0.6f);
            _textAlpha = Mathf.MoveTowards(_textAlpha, want, dt * 2.5f);
            Color sc = IntAt(beatStyle, _beat) == STYLE_WHISPER ? whisperColor : speechColor;
            sc.a = _textAlpha;
            if (subtitleLabel != null) subtitleLabel.color = sc;
            if (titleLabel != null)
            {
                Color tc = titleLabel.color;
                tc.a = _textAlpha;
                titleLabel.color = tc;
            }
            FollowHead(dt);
        }

        /// <summary>Lids sit on your head; the words float ahead of you and follow lazily, always upright.</summary>
        private void FollowHead(float dt)
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            if (lids != null) lids.SetPositionAndRotation(head.position, head.rotation);
            if (subtitleRoot == null) return;
            Vector3 fwd = head.rotation * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 want = head.position + fwd * 1.7f + Vector3.down * 0.2f;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);
            if (_snapWords || (subtitleRoot.position - want).sqrMagnitude > 25f)
            {
                subtitleRoot.SetPositionAndRotation(want, rot);
                _snapWords = false;
            }
            else
            {
                float k = 1f - Mathf.Exp(-dt * 2.5f);
                subtitleRoot.position = Vector3.Lerp(subtitleRoot.position, want, k);
                subtitleRoot.rotation = Quaternion.Slerp(subtitleRoot.rotation, rot, k);
            }
        }

        private void ApplyLids()
        {
            if (_lidMat == null) return;
            _lidMat.SetFloat("_Open", _open);
            _lidMat.SetFloat("_Flash", _flash);
            _lidMat.SetFloat("_Vignette", _vignette);
            Color tint = _lidMat.GetColor("_Tint");
            tint.a = _tint;
            _lidMat.SetColor("_Tint", tint);
        }

        // ------------------------------------------------------------------ helpers

        private void PlaySfx(AudioClip clip)
        {
            if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
        }

        private void StopSound(AudioSource a)
        {
            if (a != null) a.Stop();
        }

        private void SetActive(Transform t, bool on)
        {
            if (t != null && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        private int IntAt(int[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : 0; }
        private float FloatAt(float[] a, int i) { return a != null && i >= 0 && i < a.Length ? a[i] : 0f; }
        private string StringAt(string[] a, int i) { return a != null && i >= 0 && i < a.Length && a[i] != null ? a[i] : ""; }
    }
}
