using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Fades the whole scene out and back in for the local player: the picture (a black veil round your head, drawn over
    /// everything), the music (MusicDirector's duck) and every ambient sound (the areas' beds, fires, waterfalls). Used when
    /// a character is chosen on the title stage (a slow fade, then you wake where you last logged off) and for travelling
    /// to a marked location (a quick one). FadeOut, do the move while it's dark, FadeIn.
    /// If nothing asks for the fade back within maxDarkSeconds it comes back by itself, so nobody is left in the dark.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SceneFade : UdonSharpBehaviour
    {
        [Tooltip("A small sphere with the WizardsVRC/ScreenFade material; it follows your head while the fade is on.")]
        public Transform veil;
        public Renderer veilRenderer;
        public MusicDirector music;
        [Tooltip("Every ambient AudioSource, and the volume each plays at when nothing is faded.")]
        public AudioSource[] ambience;
        public float[] ambienceVolume;
        [Tooltip("Longest the scene stays dark before fading back in by itself (seconds).")]
        public float maxDarkSeconds = 12f;

        private Material _mat;
        private float _level;        // 0 = clear, 1 = dark
        private float _target;
        private float _speed = 1f;
        private bool _active;
        private float _darkSince;

        public bool IsDark() { return _level >= 0.999f; }
        public bool IsClear() { return !_active; }

        private void Start()
        {
            if (veil != null) veil.gameObject.SetActive(false);
        }

        /// <summary>Fade to black (and silence) over this many seconds.</summary>
        public void FadeOut(float seconds)
        {
            _target = 1f;
            _speed = 1f / Mathf.Max(0.05f, seconds);
            Wake();
            Apply();   // at once, so the music holds its track from this frame (not from the next one)
        }

        /// <summary>Fade back in over this many seconds.</summary>
        public void FadeIn(float seconds)
        {
            _target = 0f;
            _speed = 1f / Mathf.Max(0.05f, seconds);
            Wake();
            Apply();
        }

        /// <summary>Straight to black, no fade (before a cut).</summary>
        public void CutToBlack()
        {
            _target = 1f;
            _level = 1f;
            Wake();
            Apply();
        }

        private void Wake()
        {
            if (veilRenderer != null && _mat == null) _mat = veilRenderer.material;
            if (!_active) _darkSince = Time.time;
            _active = true;
            if (veil != null && !veil.gameObject.activeSelf) veil.gameObject.SetActive(true);
            Follow();
        }

        private void Update()
        {
            if (!_active) return;
            if (_level < 1f) _darkSince = Time.time;
            _level = Mathf.MoveTowards(_level, _target, _speed * Time.deltaTime);
            // Left in the dark too long (whatever was meant to bring you back didn't): come back anyway.
            if (_target >= 1f && _level >= 1f && Time.time - _darkSince > maxDarkSeconds) FadeIn(1.5f);
            Apply();
            if (_level <= 0f && _target <= 0f)
            {
                _active = false;
                if (veil != null) veil.gameObject.SetActive(false);
            }
        }

        private void Follow()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || veil == null) return;
            VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            veil.SetPositionAndRotation(head.position, head.rotation);
        }

        private void Apply()
        {
            Follow();
            // Eased, so it starts and ends gently.
            float a = _level * _level * (3f - 2f * _level);
            if (_mat != null) _mat.SetFloat("_Alpha", a);
            float heard = 1f - a;
            if (music != null) music.SetDuck(heard, _target >= 1f);
            if (ambience != null && ambienceVolume != null)
                for (int i = 0; i < ambience.Length && i < ambienceVolume.Length; i++)
                    if (ambience[i] != null) ambience[i].volume = ambienceVolume[i] * heard;
        }
    }
}
