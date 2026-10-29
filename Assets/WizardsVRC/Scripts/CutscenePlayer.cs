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
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CutscenePlayer : UdonSharpBehaviour
    {
        public Camera filmCamera;
        [Tooltip("Plays the dialogue pop-up sound when a cutscene starts.")]
        public UiAudio uiAudio;
        public GameObject screenRoot;
        public Text speakerLabel;
        public Text subtitleLabel;
        public GameObject skipButton;
        public AudioSource voiceSource;
        public float screenDistance = 1.6f;
        public float linePadding = 0.6f;

        private Cutscene _current;
        private int _line;
        private float _lineEnd;
        private float _shotStart;
        private Transform _shot;

        public bool IsPlaying() { return _current != null; }

        private void Start()
        {
            if (screenRoot != null) screenRoot.SetActive(false);
            if (filmCamera != null) filmCamera.enabled = false;
        }

        public void Play(Cutscene cutscene)
        {
            if (cutscene == null || cutscene.LineCount() == 0) return;
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
            if (screenRoot != null) screenRoot.SetActive(true);
            if (filmCamera != null) filmCamera.enabled = true;
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
            if (_current != null && _current.skippable) Stop(true);
        }

        private void ShowLine()
        {
            string speaker = _current.SpeakerAt(_line);
            string text = _current.LineAt(_line);
            if (speakerLabel != null) speakerLabel.text = speaker;
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
