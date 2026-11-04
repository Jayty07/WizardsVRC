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
    ///   - Areas: any street, town, tower or secret-boss place with its own track (Zone > Music in the world data).
    ///     Walking into its box fades its track in and the old one out, with the same crossfade as the rest.
    ///   - Dialogue: while a cutscene line or a tower leader's greeting is spoken by someone on the list (the Dominion),
    ///     their theme plays instead of the area's, then the area's comes back.
    /// The awakening dream has its own sounds, so nothing plays until its creator opens. Tracks pause when faded out and
    /// carry on where they left off, unless marked to restart. The player's music volume (book settings) scales it all.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MusicDirector : UdonSharpBehaviour
    {
        public const int NONE = -1, HUB = 0, COMBAT = 1, CREATION = 2, FIRST_AREA = 3;

        [Header("Tracks (HUB, COMBAT, CREATION, then one per area track)")]
        public AudioSource[] sources;
        [Tooltip("Each track's volume at full music volume (evens out how loud the recordings are).")]
        public float[] trackVolume;
        [Tooltip("Start from the beginning each time the track comes back (otherwise it carries on).")]
        public bool[] restart;

        [Header("What's going on")]
        public PlayerState playerState;
        public CharacterCreator creator;
        public AwakeningSequence awakening;
        public CutscenePlayer cutscenes;
        [Tooltip("Every boss tower floor (their leader's greeting counts as dialogue).")]
        public TowerFloor[] towerFloors;

        [Header("Speakers with their own theme")]
        [Tooltip("Speaker names as they appear over dialogue (e.g. Dominion Herald).")]
        public string[] dialogueSpeakers;
        [Tooltip("Which track (index into Sources) plays while each of them speaks.")]
        public int[] dialogueTrack;

        [Header("Where the hub track plays")]
        [Tooltip("World-space boxes: the castle grounds, the keep.")]
        public Vector3[] hubMin;
        public Vector3[] hubMax;
        [Tooltip("Each jukebox room's floor centre (the tavern, the saloon): no music inside, its jukebox plays there.")]
        public Transform[] quietCenters;
        public float[] quietRadii;
        public float[] quietHeights;

        [Header("Areas with their own track")]
        [Tooltip("World-space boxes (a street, its boss tower, a town...). The first box you're in wins.")]
        public Vector3[] areaMin;
        public Vector3[] areaMax;
        [Tooltip("Which track (index into Sources) plays in each box.")]
        public int[] areaTrack;
        [Tooltip("What each box is, for the Inspector.")]
        public string[] areaNames;

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
                if (sources[i] != null) { sources[i].volume = 0f; sources[i].loop = true; sources[i].spatialBlend = 0f; }
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
            int talk = DialogueTrack();
            if (talk >= 0) return talk;
            if (creator != null && creator.IsOpen()) return CREATION;
            if (awakening != null && awakening.IsRunning()) return NONE;
            if (playerState != null && !playerState.HasCharacter()) return CREATION;
            Vector3 p = me.GetPosition();
            if (InQuiet(p)) return NONE;
            int area = AreaTrack(p);
            if (area >= 0) return area;
            if (InHub(p)) return HUB;
            return NONE;
        }

        /// <summary>The theme of whoever is speaking to the player right now, or -1.</summary>
        private int DialogueTrack()
        {
            if (dialogueSpeakers == null || dialogueTrack == null || dialogueSpeakers.Length == 0) return -1;
            if (cutscenes != null && cutscenes.IsPlaying())
            {
                int t = SpeakerTrack(cutscenes.CurrentSpeaker());
                if (t >= 0) return t;
            }
            if (towerFloors != null)
                for (int i = 0; i < towerFloors.Length; i++)
                {
                    TowerFloor f = towerFloors[i];
                    if (f == null || !f.IsTalking()) continue;
                    int t = SpeakerTrack(f.speaker);
                    if (t >= 0) return t;
                }
            return -1;
        }

        private int SpeakerTrack(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return -1;
            for (int i = 0; i < dialogueSpeakers.Length && i < dialogueTrack.Length; i++)
            {
                if (dialogueSpeakers[i] != speaker) continue;
                int t = dialogueTrack[i];
                if (t >= 0 && sources != null && t < sources.Length) return t;
            }
            return -1;
        }

        /// <summary>The track of the first area box the player stands in, or -1.</summary>
        private int AreaTrack(Vector3 p)
        {
            if (areaMin == null || areaMax == null || areaTrack == null) return -1;
            for (int i = 0; i < areaMin.Length && i < areaMax.Length && i < areaTrack.Length; i++)
            {
                Vector3 a = areaMin[i], b = areaMax[i];
                if (p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z)
                {
                    int t = areaTrack[i];
                    if (t >= 0 && sources != null && t < sources.Length) return t;
                    return -1;
                }
            }
            return -1;
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
            if (quietCenters == null) return false;
            for (int i = 0; i < quietCenters.Length; i++)
            {
                Transform c = quietCenters[i];
                if (c == null) continue;
                float r = quietRadii != null && i < quietRadii.Length ? quietRadii[i] : 18f;
                float h = quietHeights != null && i < quietHeights.Length ? quietHeights[i] : 18f;
                Vector3 d = p - c.position;
                if (d.y < -2f || d.y > h) continue;
                if (d.x * d.x + d.z * d.z < r * r) return true;
            }
            return false;
        }
    }
}
