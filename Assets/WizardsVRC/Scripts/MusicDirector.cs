using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Background music, local to each player: one track per situation, crossfaded as you move between them.
    ///   - Combat: while you're in a duel (any street, the arena). Starts from the top every fight.
    ///   - Character creation: on the character select screen and in the character creator.
    ///   - Hub: in Starfall Castle's grounds and the keep, except inside the tavern (its jukebox plays there).
    /// The awakening dream has its own sounds, so nothing plays until its creator opens. The hub and creation tracks
    /// pause when faded out and carry on where they left off. The player's music volume (book settings) scales it all.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MusicDirector : UdonSharpBehaviour
    {
        public const int NONE = -1, HUB = 0, COMBAT = 1, CREATION = 2;

        [Header("Tracks (HUB, COMBAT, CREATION)")]
        public AudioSource[] sources;
        [Tooltip("Each track's volume at full music volume (evens out how loud the recordings are).")]
        public float[] trackVolume;
        [Tooltip("Start from the beginning each time the track comes back (otherwise it carries on).")]
        public bool[] restart;

        [Header("What's going on")]
        public PlayerState playerState;
        public CharacterCreator creator;
        public AwakeningSequence awakening;

        [Header("Where the hub track plays")]
        [Tooltip("World-space boxes: the castle grounds, the keep.")]
        public Vector3[] hubMin;
        public Vector3[] hubMax;
        [Tooltip("The tavern's floor centre: no hub music inside (its jukebox plays there).")]
        public Transform quietCenter;
        public float quietRadius = 18f;
        public float quietHeight = 18f;

        public float fadeInSeconds = 2f;
        public float fadeOutSeconds = 1.2f;

        private float[] _level;
        private bool[] _started;
        private int _want = NONE;
        private float _next;
        private float _master = 1f;

        private void Start()
        {
            int n = sources != null ? sources.Length : 0;
            _level = new float[n];
            _started = new bool[n];
            for (int i = 0; i < n; i++)
                if (sources[i] != null) { sources[i].volume = 0f; sources[i].loop = true; }
        }

        /// <summary>The player's music volume, 0 to 1 (InventoryBook's setting).</summary>
        public void SetMasterVolume(float v)
        {
            _master = Mathf.Clamp01(v);
        }

        private void Update()
        {
            if (sources == null || _level == null) return;
            if (Time.time >= _next)
            {
                _next = Time.time + 0.25f;
                _want = Choose();
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = sources[i];
                if (s == null) continue;
                bool on = i == _want && _master > 0.001f;
                float target = on ? 1f : 0f;
                float rate = on ? 1f / Mathf.Max(0.05f, fadeInSeconds) : 1f / Mathf.Max(0.05f, fadeOutSeconds);
                _level[i] = Mathf.MoveTowards(_level[i], target, rate * dt);
                if (on && !s.isPlaying)
                {
                    if (_started[i] && !Restarts(i)) s.UnPause();
                    else s.Play();
                    _started[i] = true;
                }
                float tv = trackVolume != null && i < trackVolume.Length ? trackVolume[i] : 0.3f;
                s.volume = _level[i] * tv * _master;
                if (!on && _level[i] <= 0f && s.isPlaying)
                {
                    if (Restarts(i)) { s.Stop(); _started[i] = false; }
                    else s.Pause();
                }
            }
        }

        private bool Restarts(int i)
        {
            return restart != null && i < restart.Length && restart[i];
        }

        private int Choose()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return NONE;
            if (playerState != null && playerState.inCombat) return COMBAT;
            if (creator != null && creator.IsOpen()) return CREATION;
            if (awakening != null && awakening.IsRunning()) return NONE;
            if (playerState != null && !playerState.HasCharacter()) return CREATION;
            Vector3 p = me.GetPosition();
            if (InQuiet(p)) return NONE;
            if (InHub(p)) return HUB;
            return NONE;
        }

        private bool InHub(Vector3 p)
        {
            if (hubMin == null || hubMax == null) return false;
            for (int i = 0; i < hubMin.Length && i < hubMax.Length; i++)
            {
                Vector3 a = hubMin[i], b = hubMax[i];
                if (p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z) return true;
            }
            return false;
        }

        private bool InQuiet(Vector3 p)
        {
            if (quietCenter == null) return false;
            Vector3 d = p - quietCenter.position;
            if (d.y < -2f || d.y > quietHeight) return false;
            return d.x * d.x + d.z * d.z < quietRadius * quietRadius;
        }
    }
}
