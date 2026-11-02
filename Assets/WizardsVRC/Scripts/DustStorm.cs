using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Dust storms on Dustwind's streets: every so often (on the server clock, so everyone on a street sees the same
    /// storm) a storm rolls through one street for about a minute, the fog thickening to orange and sand blowing round
    /// you. Each street has its own turn. Purely a look: nothing is harder while it blows. Local.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DustStorm : UdonSharpBehaviour
    {
        [Tooltip("Each street's height band (zone height - 50 to + 60, like the quest guide's areas).")]
        public float[] minY;
        public float[] maxY;
        [Tooltip("Minutes between storms on a street.")]
        public int everyMinutes = 9;
        public int stormSeconds = 70;
        public Color stormFog = new Color(0.85f, 0.55f, 0.3f);
        [Tooltip("How far you can see at the storm's height (fog end, metres).")]
        public float stormSight = 55f;
        [Tooltip("Blowing sand, kept round you while a storm's on.")]
        public GameObject sand;
        public AudioSource wind;
        [Tooltip("Puts the street's own fog back when a storm ends.")]
        public AreaCuller culler;

        private float _k;
        private bool _active;
        private Color _baseFog;
        private float _baseEnd;
        private float _baseStart;
        private float _next;
        private int _target;
        private int _street = -1;

        private int StreetAt(float y)
        {
            if (minY == null || maxY == null) return -1;
            for (int i = 0; i < minY.Length && i < maxY.Length; i++) if (y >= minY[i] && y <= maxY[i]) return i;
            return -1;
        }

        /// <summary>True while street i's storm is blowing.</summary>
        public bool Blowing(int i)
        {
            int period = Mathf.Max(120, everyMinutes * 60);
            int t = Networking.GetServerTimeInMilliseconds() / 1000 + i * (period / Mathf.Max(1, minY.Length));
            t = t % period;
            if (t < 0) t += period;
            return t < stormSeconds;
        }

        private void Update()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            if (Time.time >= _next)
            {
                _next = Time.time + 0.5f;
                int street = StreetAt(me.GetPosition().y);
                // Left the street mid-storm (a tunnel, a teleport): the storm stays behind at once.
                if (_active && street != _street) EndNow();
                _street = street;
                _target = street >= 0 && Blowing(street) ? 1 : 0;
                if (sand != null)
                {
                    bool on = _k > 0.3f;
                    if (sand.activeSelf != on) sand.SetActive(on);
                    if (on) sand.transform.position = me.GetPosition();
                }
            }
            float goal = _target;
            if (Mathf.Approximately(_k, goal) && !_active) return;
            if (!_active && goal > 0f)
            {
                // Remember the street's own fog to come back to.
                _baseFog = RenderSettings.fogColor;
                _baseEnd = RenderSettings.fogEndDistance;
                _baseStart = RenderSettings.fogStartDistance;
                _active = true;
                if (wind != null) wind.Play();
            }
            _k = Mathf.MoveTowards(_k, goal, Time.deltaTime / 6f);
            if (!_active) return;
            RenderSettings.fogColor = Color.Lerp(_baseFog, stormFog, _k);
            RenderSettings.fogEndDistance = Mathf.Lerp(_baseEnd, stormSight, _k);
            RenderSettings.fogStartDistance = Mathf.Lerp(_baseStart, 2f, _k);
            if (wind != null) wind.volume = _k * 0.6f;
            if (_k <= 0f && goal <= 0f) EndNow();
        }

        private void EndNow()
        {
            RenderSettings.fogColor = _baseFog;
            RenderSettings.fogEndDistance = _baseEnd;
            RenderSettings.fogStartDistance = _baseStart;
            _active = false;
            _k = 0f;
            if (wind != null) wind.Stop();
            if (sand != null) sand.SetActive(false);
            if (culler != null) culler.RefreshSky();
        }
    }
}
