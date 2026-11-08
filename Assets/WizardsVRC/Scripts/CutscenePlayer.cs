using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Local "video player": a screen that appears in front of the player and shows a hidden film camera's
    /// RenderTexture while a <see cref="Cutscene"/> plays its voiced lines. Next advances a line (Space on desktop),
    /// Skip ends the whole cutscene. The player is held still while it plays.
    ///
    /// The same screen plays a spell's film (<see cref="PlayFilm"/>, driven by SpellCinematic): the caster's name and
    /// spell instead of a speaker's line, shots set every frame from outside, and the camera then sees players too (your
    /// own avatar through the MirrorReflection layer) so the film shows you casting. Skip just closes the screen.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CutscenePlayer : UdonSharpBehaviour
    {
        public Camera filmCamera;
        [Tooltip("Plays the dialogue pop-up sound when a cutscene starts.")]
        public UiAudio uiAudio;
        public GameObject screenRoot;
        public Text speakerLabel;
        [Header("Speaker portrait: a medallion whose rim says who they are to you")]
        public Image portraitRing;
        [Tooltip("Ally (blue), neutral (yellow), hostile (red) rims.")]
        public Sprite[] portraitRings;
        [Tooltip("The speaker's initial in the medallion.")]
        public Text portraitInitial;
        public Color[] stanceColors;
        public Text subtitleLabel;
        public GameObject skipButton;
        public AudioSource voiceSource;
        public float screenDistance = 1.6f;
        public float linePadding = 0.6f;
        [Tooltip("Hidden during a spell's film (there are no lines to step through).")]
        public GameObject nextButton;
        [Tooltip("What the film camera sees during a spell's film: players as well (not the screen or your own head-less avatar).")]
        public int filmCullingMask = -1;
        [Tooltip("The screen a spell's film plays on is this far in front of you.")]
        public float filmScreenDistance = 1.5f;

        private int _storyMask = -1;
        private bool _film;

        private Cutscene _current;
        private int _line;
        private float _lineEnd;
        private float _shotStart;
        private Transform _shot;

        public bool IsPlaying() { return _current != null; }

        /// <summary>Who is speaking right now ("" when no cutscene is playing). MusicDirector picks a theme by it.</summary>
        public string CurrentSpeaker() { return _current != null ? _current.SpeakerAt(_line) : ""; }

        private void Start()
        {
            if (screenRoot != null) screenRoot.SetActive(false);
            if (filmCamera != null) { filmCamera.enabled = false; _storyMask = filmCamera.cullingMask; }
        }

        public void Play(Cutscene cutscene)
        {
            if (cutscene == null || cutscene.LineCount() == 0) return;
            if (_film) StopFilm();
            if (_current != null) Stop(false);
            _current = cutscene;
            _line = 0;

            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local))
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                if (screenRoot != null)
                {
                    screenRoot.transform.position = head.position + forward * screenDistance;
                    screenRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                }
                local.Immobilize(true);
            }
            if (skipButton != null) skipButton.SetActive(cutscene.skippable);
            if (nextButton != null) nextButton.SetActive(true);
            if (screenRoot != null) screenRoot.SetActive(true);
            if (filmCamera != null) { if (_storyMask != -1) filmCamera.cullingMask = _storyMask; filmCamera.enabled = true; }
            if (uiAudio != null) uiAudio.PlayPopup();
            ShowLine();
        }

        public void Next()
        {
            if (_current == null) return;
            _line++;
            if (_line >= _current.LineCount()) Stop(true);
            else ShowLine();
        }

        public void Skip()
        {
            if (_film) { StopFilm(); return; }
            if (_current != null && _current.skippable) Stop(true);
        }

        // ------------------------------------------------------------------ spell films (SpellCinematic)

        public bool IsFilming() { return _film; }

        /// <summary>
        /// Opens the screen in front of you for a spell's film: 'title' (the caster) over 'caption' (the spell), the
        /// medallion showing the caster's initial in 'color'. Shots come from <see cref="SetFilmShot"/>.
        /// </summary>
        public void PlayFilm(string title, string caption, Color color)
        {
            if (_current != null || filmCamera == null) return;
            _film = true;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && screenRoot != null)
            {
                VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 forward = head.rotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();
                screenRoot.transform.position = head.position + forward * filmScreenDistance;
                screenRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            if (speakerLabel != null) { speakerLabel.text = title; speakerLabel.color = color; }
            if (subtitleLabel != null) subtitleLabel.text = caption;
            if (portraitRing != null && portraitRings != null && portraitRings.Length > 0) portraitRing.sprite = portraitRings[0];
            if (portraitInitial != null)
            {
                portraitInitial.text = title.Length > 0 ? title.Substring(0, 1).ToUpper() : "?";
                portraitInitial.color = color;
            }
            if (skipButton != null) skipButton.SetActive(true);
            if (nextButton != null) nextButton.SetActive(false);
            if (filmCullingMask != -1) filmCamera.cullingMask = filmCullingMask;
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
            if (_film && subtitleLabel != null) subtitleLabel.text = caption;
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
            if (nextButton != null) nextButton.SetActive(true);
        }

        private void ShowLine()
        {
            string speaker = _current.SpeakerAt(_line);
            string text = _current.LineAt(_line);
            if (speakerLabel != null) speakerLabel.text = speaker;
            int stance = Mathf.Clamp(_current.StanceAt(_line), 0, 2);
            if (portraitRing != null && portraitRings != null && stance < portraitRings.Length) portraitRing.sprite = portraitRings[stance];
            if (portraitInitial != null)
            {
                string name = speaker.StartsWith("Professor ") ? speaker.Substring(10) : (speaker.StartsWith("Headmaster ") ? speaker.Substring(11) : speaker);
                portraitInitial.text = name.Length > 0 ? name.Substring(0, 1).ToUpper() : "?";
                if (stanceColors != null && stance < stanceColors.Length) portraitInitial.color = stanceColors[stance];
            }
            if (speakerLabel != null && stanceColors != null && stance < stanceColors.Length) speakerLabel.color = stanceColors[stance];
            if (subtitleLabel != null) subtitleLabel.text = text;

            _shot = _current.ShotAt(_line);
            _shotStart = Time.time;
            if (_shot != null && filmCamera != null) filmCamera.transform.SetPositionAndRotation(_shot.position, _shot.rotation);

            AudioClip clip = _current.VoiceAt(_line);
            float duration = 2f + text.Length * 0.06f;
            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (clip != null)
                {
                    voiceSource.clip = clip;
                    voiceSource.Play();
                    duration = clip.length + linePadding;
                }
            }
            _lineEnd = Time.time + duration;
        }

        private void Update()
        {
            if (_current == null) return;
            if (_shot != null && filmCamera != null)
                filmCamera.transform.position = _shot.position + _shot.forward * ((Time.time - _shotStart) * _current.dollySpeed);
            if (Input.GetKeyDown(KeyCode.Space) || Time.time >= _lineEnd) Next();
        }

        private void Stop(bool notify)
        {
            Cutscene done = _current;
            _current = null;
            if (voiceSource != null) voiceSource.Stop();
            if (filmCamera != null) filmCamera.enabled = false;
            if (screenRoot != null) screenRoot.SetActive(false);
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local)) local.Immobilize(false);
            if (notify && done != null && done.onFinished != null && done.onFinishedEvent.Length > 0)
                done.onFinished.SendCustomEvent(done.onFinishedEvent);
        }
    }
}
