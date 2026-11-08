using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The tavern's video player. Everyone hears and sees the same song at the same point: the link, the server time
    /// it started at and whether it is paused are synced, and each player seeks to match. Anyone can paste a link
    /// (it takes over for everyone), go back to the default song, or pause and resume. The music is only heard inside
    /// the tavern: the speaker fades out as you step out of the doors and back in as you return. Mute and volume are
    /// just for you.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TavernJukebox : UdonSharpBehaviour
    {
        [Header("Player")]
        public BaseVRCVideoPlayer player;
        public AudioSource speaker;
        [Tooltip("Played when the instance opens and by the Default Song button.")]
        public VRCUrl defaultUrl;

        [Header("Controls")]
        public VRCUrlInputField urlField;
        public Text statusLabel;
        public Text pauseLabel;
        public Text muteLabel;
        public Text volumeLabel;

        [Header("Where it can be heard")]
        [Tooltip("The middle of the tavern's floor.")]
        public Transform center;
        [Tooltip("The hall's inner radius (m): outside it the music fades out.")]
        public float radius = 19f;
        [Tooltip("How high above the floor still counts as inside (m).")]
        public float height = 18f;
        [Tooltip("A rectangular hall (the saloon): half its width (x) and length (y) in the centre's own frame. 0 = round (radius).")]
        public Vector2 boxHalf;
        [Header("Player piano (optional)")]
        [Tooltip("Piano keys that bob while the music plays.")]
        public Transform[] pianoKeys;
        private Vector3[] _keyRest;
        [Header("Rooms (optional)")]
        [Tooltip("Only load the video while you're within this distance (m) of the centre (0 = always): the tavern's rooms each " +
                 "have their own player, and VRChat only allows one video load every few seconds, so a room's video loads when " +
                 "you come near and unloads when you leave.")]
        public float loadRange;
        [Tooltip("Quiet while you're inside any of these (the rooms upstairs, which play their own).")]
        public TavernJukebox[] mutedInside;
        private float _nextNear;
        private bool _near = true;
        [Range(0f, 1f)] public float volume = 0.45f;
        public float fadeSeconds = 0.6f;

        [Header("Big screen")]
        [Tooltip("The screen and its frame: moved and scaled between the normal and the big (cinema) size.")]
        public Transform screenRig;
        public Vector3 smallPosition;
        public Vector3 bigPosition;
        public float bigScale = 2.2f;
        public Text sizeLabel;
        [Tooltip("Seconds the screen takes to grow or shrink.")]
        public float resizeSeconds = 0.8f;
        private float _sizeShown;

        [UdonSynced] private bool _bigScreen;
        [UdonSynced] private VRCUrl _url;
        [UdonSynced] private int _version;
        [UdonSynced] private double _startTime;
        [UdonSynced] private bool _paused;
        [UdonSynced] private float _pausedAt;

        private int _loadedVersion = -1;
        private bool _ready;
        private bool _muted;
        private float _nextResync;
        private bool _retryQueued;

        private void Start()
        {
            UpdateLabels();
            if (_version == 0) SendCustomEventDelayedSeconds(nameof(InitIfEmpty), Networking.IsOwner(gameObject) ? 0.5f : 3f);
        }

        private void OnEnable()
        {
            // The hub is switched off while you are out in a street; catch up with the song when you come back.
            _loadedVersion = -1;
            _ready = false;
            if (_version > 0) Load();
        }

        /// <summary>Nobody has started anything yet (a new instance): start the default song for everyone.</summary>
        public void InitIfEmpty()
        {
            if (_version != 0 || defaultUrl == null || string.IsNullOrEmpty(defaultUrl.Get())) return;
            StartForEveryone(defaultUrl);
        }

        // ------------------------------------------------------------------ controls

        /// <summary>The link field's Enter, or its Play button.</summary>
        public void OnUrlEntered()
        {
            if (urlField == null) return;
            VRCUrl u = urlField.GetUrl();
            if (u == null || string.IsNullOrEmpty(u.Get())) return;
            urlField.SetUrl(VRCUrl.Empty);
            StartForEveryone(u);
        }

        public void PlayDefault()
        {
            if (defaultUrl != null && !string.IsNullOrEmpty(defaultUrl.Get())) StartForEveryone(defaultUrl);
        }

        public void TogglePause()
        {
            if (_version == 0 || player == null) return;
            TakeOwnership();
            if (!_paused)
            {
                _pausedAt = player.GetTime();
                _paused = true;
            }
            else
            {
                _startTime = Networking.GetServerTimeInSeconds() - _pausedAt;
                _paused = false;
            }
            RequestSerialization();
            ApplyState();
        }

        /// <summary>The Big Screen button: the stage screen grows (and comes down in front of the curtains) for everyone, or goes back.</summary>
        public void ToggleScreenSize()
        {
            TakeOwnership();
            _bigScreen = !_bigScreen;
            RequestSerialization();
            UpdateLabels();
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            UpdateLabels();
        }

        public void VolumeUp()
        {
            volume = Mathf.Clamp01(volume + 0.1f);
            UpdateLabels();
        }

        public void VolumeDown()
        {
            volume = Mathf.Clamp01(volume - 0.1f);
            UpdateLabels();
        }

        /// <summary>The Stop button (the rooms): nothing plays, for everyone in the room.</summary>
        public void StopForEveryone()
        {
            TakeOwnership();
            _url = VRCUrl.Empty;
            _version++;
            _paused = false;
            RequestSerialization();
            Load();
            UpdateLabels();
        }

        /// <summary>True when a world position is inside where this jukebox is heard.</summary>
        public bool Contains(Vector3 worldPos)
        {
            if (center == null) return false;
            Vector3 p = worldPos - center.position;
            if (boxHalf.x > 0f)
            {
                Vector3 lp = Quaternion.Inverse(center.rotation) * p;
                return Mathf.Abs(lp.x) < boxHalf.x && Mathf.Abs(lp.z) < boxHalf.y && lp.y > -1.5f && lp.y < height;
            }
            return p.x * p.x + p.z * p.z < radius * radius && p.y > -1.5f && p.y < height;
        }

        private bool NearEnough()
        {
            if (loadRange <= 0f || center == null) return true;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return false;
            return Vector3.Distance(me.GetPosition(), center.position) < loadRange + (_near ? 2f : 0f);
        }

        private void StartForEveryone(VRCUrl u)
        {
            TakeOwnership();
            _url = u;
            _version++;
            _startTime = Networking.GetServerTimeInSeconds();
            _paused = false;
            _pausedAt = 0f;
            RequestSerialization();
            Load();
        }

        private void TakeOwnership()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && !Networking.IsOwner(me, gameObject)) Networking.SetOwner(me, gameObject);
        }

        // ------------------------------------------------------------------ playback

        public override void OnDeserialization()
        {
            if (_version != _loadedVersion) Load();
            else ApplyState();
            UpdateLabels();
        }

        private void Load()
        {
            if (player == null) return;
            if (_url == null || string.IsNullOrEmpty(_url.Get()))
            {
                // Stopped (or nothing yet).
                if (_version > 0) { player.Stop(); _ready = false; _loadedVersion = _version; SetStatus("Nothing playing"); }
                return;
            }
            if (!NearEnough()) { _loadedVersion = -1; return; }   // a room you're not in: it loads when you come near
            _loadedVersion = _version;
            _ready = false;
            player.Stop();
            player.LoadURL(_url);
            SetStatus("Loading...");
        }

        /// <summary>VRChat allows one video load every few seconds per world; try again shortly after.</summary>
        public void RetryLoad()
        {
            _retryQueued = false;
            _loadedVersion = -1;
            Load();
        }

        public override void OnVideoReady()
        {
            _ready = true;
            ApplyState();
        }

        public override void OnVideoStart()
        {
            Seek(true);
            UpdateLabels();
        }

        public override void OnVideoError(VideoError videoError)
        {
            _ready = false;
            if (videoError == VideoError.RateLimited)
            {
                SetStatus("Waiting to load...");
                if (!_retryQueued)
                {
                    _retryQueued = true;
                    SendCustomEventDelayedSeconds(nameof(RetryLoad), 5.5f);
                }
                return;
            }
            if (videoError == VideoError.InvalidURL) SetStatus("That link didn't work. Try another one.");
            else if (videoError == VideoError.AccessDenied) SetStatus("Turn on 'Allow Untrusted URLs' in VRChat's settings to play that link.");
            else SetStatus("Couldn't play that video.");
        }

        private void ApplyState()
        {
            if (player == null || !_ready) return;
            if (_paused)
            {
                if (player.IsPlaying) player.Pause();
                player.SetTime(_pausedAt);
            }
            else
            {
                if (!player.IsPlaying) player.Play();
                Seek(true);
            }
            UpdateLabels();
        }

        /// <summary>Jumps to where everyone else is (the song loops, so wrap round its length).</summary>
        private void Seek(bool force)
        {
            if (player == null || _paused || !_ready) return;
            float duration = player.GetDuration();
            if (duration <= 1f || duration > 86400f) return; // a live stream: nothing to line up
            double since = Networking.GetServerTimeInSeconds() - _startTime;
            if (since < 0) since = 0;
            float want = (float)(since % duration);
            float off = Mathf.Abs(player.GetTime() - want);
            if (force ? off > 0.5f : (off > 2f && off < duration - 2f)) player.SetTime(want);
        }

        private void Update()
        {
            // A room's video loads as you come near and unloads when you've gone (loadRange).
            if (loadRange > 0f && Time.time >= _nextNear)
            {
                _nextNear = Time.time + 0.5f;
                bool near = NearEnough();
                _near = near;
                if (near && _version > 0 && _loadedVersion != _version) Load();
                else if (!near && _loadedVersion >= 0 && player != null && _url != null && !string.IsNullOrEmpty(_url.Get()))
                {
                    player.Stop();
                    _ready = false;
                    _loadedVersion = -1;
                }
            }
            // Only heard inside the tavern: fade the speaker with where you stand.
            if (speaker != null)
            {
                bool inside = false;
                VRCPlayerApi me = Networking.LocalPlayer;
                if (Utilities.IsValid(me) && center != null)
                {
                    Vector3 mine = me.GetPosition();
                    inside = Contains(mine);
                    if (inside && mutedInside != null)
                        for (int i = 0; i < mutedInside.Length; i++)
                            if (mutedInside[i] != null && mutedInside[i].Contains(mine)) { inside = false; break; }
                }
                float target = inside && !_muted ? volume : 0f;
                float step = Time.deltaTime / Mathf.Max(0.05f, fadeSeconds);
                speaker.volume = Mathf.MoveTowards(speaker.volume, target, step);
            }
            // The player piano's keys dance while a song plays (only drawn near the saloon anyway).
            if (pianoKeys != null && pianoKeys.Length > 0)
            {
                if (_keyRest == null || _keyRest.Length != pianoKeys.Length)
                {
                    _keyRest = new Vector3[pianoKeys.Length];
                    for (int i = 0; i < pianoKeys.Length; i++) if (pianoKeys[i] != null) _keyRest[i] = pianoKeys[i].localPosition;
                }
                bool playing = _ready && !_paused && player != null && player.IsPlaying && speaker != null && speaker.volume > 0.01f;
                float tm = Time.time;
                for (int i = 0; i < pianoKeys.Length; i++)
                {
                    if (pianoKeys[i] == null) continue;
                    float dip = 0f;
                    if (playing)
                    {
                        float w = Mathf.Sin(tm * (5.3f + (i % 5) * 1.7f) + i * 2.1f) * Mathf.Sin(tm * 1.3f + i * 0.7f);
                        dip = w > 0.55f ? -0.025f : 0f;
                    }
                    pianoKeys[i].localPosition = _keyRest[i] + new Vector3(0f, dip, 0f);
                }
            }
            // The screen eases between its normal and its big size.
            if (screenRig != null)
            {
                float want = _bigScreen ? 1f : 0f;
                if (_sizeShown != want)
                {
                    _sizeShown = Mathf.MoveTowards(_sizeShown, want, Time.deltaTime / Mathf.Max(0.05f, resizeSeconds));
                    float e = _sizeShown * _sizeShown * (3f - 2f * _sizeShown);
                    screenRig.localPosition = Vector3.Lerp(smallPosition, bigPosition, e);
                    screenRig.localScale = Vector3.one * Mathf.Lerp(1f, bigScale, e);
                }
            }
            if (Time.time >= _nextResync)
            {
                _nextResync = Time.time + 15f;
                if (_ready && player != null && player.IsPlaying) Seek(false);
            }
        }

        // ------------------------------------------------------------------ labels

        private void SetStatus(string s)
        {
            if (statusLabel != null) statusLabel.text = s;
        }

        private void UpdateLabels()
        {
            if (pauseLabel != null) pauseLabel.text = _paused ? "Resume" : "Pause";
            if (muteLabel != null) muteLabel.text = _muted ? "Unmute (you)" : "Mute (you)";
            if (volumeLabel != null) volumeLabel.text = "Your volume: " + Mathf.RoundToInt(volume * 100f) + "%";
            if (sizeLabel != null) sizeLabel.text = _bigScreen ? "Normal screen" : "Big screen";
            if (statusLabel == null) return;
            if (_version == 0) statusLabel.text = "Nothing playing";
            else if (!_ready) return;
            else if (_paused) statusLabel.text = "Paused";
            else statusLabel.text = "Now playing: " + ShortUrl(_url);
        }

        private string ShortUrl(VRCUrl u)
        {
            string s = u == null ? "" : u.Get();
            if (s.StartsWith("https://")) s = s.Substring(8);
            if (s.StartsWith("www.")) s = s.Substring(4);
            if (s.Length > 48) s = s.Substring(0, 45) + "...";
            return s;
        }
    }
}
