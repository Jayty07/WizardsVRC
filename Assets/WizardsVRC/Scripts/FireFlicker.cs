using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A stylised, PS1-style fire: a few layered flame shapes (dark orange outside, yellow, a white-hot core) that
    /// stretch, sway and twist with noise, rising embers, and a light that flickers with them. Local and cosmetic; it
    /// lives under its area's root, so it stops when the area is switched off for you.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FireFlicker : UdonSharpBehaviour
    {
        public Transform[] flames;
        [Tooltip("How much each flame stretches and squashes (share of its height).")]
        public float stretch = 0.25f;
        [Tooltip("How quickly the fire flickers.")]
        public float speed = 6f;
        public Light fireLight;
        public ParticleSystem embers;
        [Tooltip("Embers per second (emitted from here, so the system can stay off when you're elsewhere).")]
        public float emberRate = 10f;
        [Tooltip("Only animates while you're this close (m); checked twice a second.")]
        public float activeRange = 35f;

        private Vector3[] _scale;
        private Quaternion[] _rot;
        private float _baseIntensity;
        private float _emberDebt;
        private bool _near = true;
        private float _nextCheck;

        private void Start()
        {
            int n = flames != null ? flames.Length : 0;
            _scale = new Vector3[n];
            _rot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                if (flames[i] == null) continue;
                _scale[i] = flames[i].localScale;
                _rot[i] = flames[i].localRotation;
            }
            if (fireLight != null) _baseIntensity = fireLight.intensity;
        }

        private void Update()
        {
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 0.5f;
                VRCPlayerApi me = Networking.LocalPlayer;
                _near = me == null || (me.GetPosition() - transform.position).sqrMagnitude < activeRange * activeRange;
            }
            if (!_near) return;
            float t = Time.time * speed;
            if (flames != null && _scale != null)
                for (int i = 0; i < flames.Length; i++)
                {
                    Transform f = flames[i];
                    if (f == null) continue;
                    float n1 = Mathf.PerlinNoise(t, i * 3.7f) - 0.5f;
                    float n2 = Mathf.PerlinNoise(i * 5.1f, t * 1.3f) - 0.5f;
                    Vector3 s = _scale[i];
                    f.localScale = new Vector3(s.x * (1f - n1 * stretch * 0.6f), s.y * (1f + n1 * stretch * 2f), s.z * (1f - n1 * stretch * 0.6f));
                    f.localRotation = _rot[i] * Quaternion.Euler(n2 * 14f, t * 25f * (i % 2 == 0 ? 1f : -1f), n1 * 10f);
                }
            if (fireLight != null)
                fireLight.intensity = _baseIntensity * (0.8f + 0.45f * Mathf.PerlinNoise(t * 1.7f, 9.3f));
            if (embers != null && emberRate > 0f)
            {
                _emberDebt += emberRate * Time.deltaTime;
                int k = (int)_emberDebt;
                if (k > 0)
                {
                    _emberDebt -= k;
                    embers.Emit(k);
                }
            }
        }
    }
}
