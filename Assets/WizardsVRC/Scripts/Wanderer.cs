using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A wandering monster. With a <see cref="path"/> it walks a patrol route (a loop, or back and forth along a line);
    /// without one it circles its encounter's duel circle. Its position comes from the server clock, so every client sees
    /// it in the same place without syncing. Touching it (walking within aggroRadius) starts, or joins, the encounter's
    /// fight; it hides while the fight is on and for a while afterwards.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Wanderer : UdonSharpBehaviour
    {
        public BattleManager manager;
        public BattleClient client;
        public CardDatabase cardDatabase;
        public int monsterId;

        [Header("Patrol route (zones)")]
        [Tooltip("Waypoints of the patrol route. Leave empty to circle 'center' instead.")]
        public Transform[] path;
        [Tooltip("True: the route is a closed loop. False: walk to the end and back.")]
        public bool loop = true;

        [Header("Circle mode (no path)")]
        public Transform center;
        public float radius = 12f;

        [Tooltip("Metres per second.")]
        public float speed = 1.2f;
        [Tooltip("Circle mode: start angle (radians). Patrol mode: start distance along the route (metres).")]
        public float phaseOffset;
        public float aggroRadius = 2.5f;
        public GameObject body;
        public Text label;
        [Tooltip("This patrol monster's number in its encounter (BattleManager.wandererMonsters).")]
        public int wandererIndex;
        [Tooltip("Even fights: within this distance of the duel circle's centre it has reached the circle.")]
        public float contactRadius = 10f;
        [Tooltip("Seconds it takes to fade away (shrinks and rises) when it leaves its patrol.")]
        public float fadeSeconds = 0.7f;

        private float _nextTry;
        private float[] _cum;
        private float _length;
        private Vector3 _pose;
        private Vector3 _heading;
        private Vector3 _bodyScale = Vector3.one;
        private Vector3 _bodyPos;
        private bool _wasHidden;
        private float _fadeStart = -100f;
        private int _contactBattle = -1;

        private void Start()
        {
            if (label != null && cardDatabase != null)
                label.text = cardDatabase.MonsterName(monsterId) + "\nLevel " + cardDatabase.MonsterLevel(monsterId).ToString();
            if (body != null) { _bodyScale = body.transform.localScale; _bodyPos = body.transform.localPosition; }
            BuildRoute();
        }

        /// <summary>Leaving the patrol mid-fight (joining the duel or fading away): shrinks and rises, then hides.</summary>
        private bool AnimateFade()
        {
            float t = (Time.time - _fadeStart) / Mathf.Max(0.1f, fadeSeconds);
            if (t >= 1f || body == null) return false;
            float tq = Mathf.Floor(t * 15f) / 15f;
            if (!body.activeSelf) body.SetActive(true);
            body.transform.localScale = _bodyScale * (1f - tq);
            body.transform.localPosition = _bodyPos + Vector3.up * (tq * 0.8f);
            body.transform.localRotation = Quaternion.Euler(0f, tq * 540f, 0f);
            return true;
        }

        private void BuildRoute()
        {
            _length = 0f;
            if (path == null || path.Length < 2) return;
            int n = path.Length;
            _cum = new float[n + 1];
            _cum[0] = 0f;
            for (int i = 1; i <= n; i++)
            {
                if (i == n && !loop) { _cum[i] = _cum[i - 1]; break; }
                Transform a = path[i - 1];
                Transform b = path[i % n];
                float d = a != null && b != null ? Vector3.Distance(a.position, b.position) : 0f;
                _cum[i] = _cum[i - 1] + d;
            }
            _length = loop ? _cum[n] : _cum[n - 1];
        }

        private void Update()
        {
            if (manager == null) return;
            bool hidden = manager.WandererHidden(wandererIndex);
            // Even fights: a patrol monster that just left (joined the duel or faded away) fades out where it stood.
            if (hidden && !_wasHidden && manager.evenFight && manager.phase != BattleManager.PHASE_IDLE) _fadeStart = Time.time;
            if (!hidden && _wasHidden && body != null)
            {
                body.transform.localScale = _bodyScale;
                body.transform.localPosition = _bodyPos;
                body.transform.localRotation = Quaternion.identity;
            }
            _wasHidden = hidden;
            if (hidden && AnimateFade())
            {
                if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }
            if (body != null && body.activeSelf == hidden) body.SetActive(!hidden);
            if (label != null && label.gameObject.activeSelf == hidden) label.gameObject.SetActive(!hidden);
            if (hidden) return;

            float t = (Networking.GetServerTimeInMilliseconds() % 3600000) / 1000f;
            Vector3 pos;
            Vector3 facing;
            if (_length > 0.01f)
            {
                PatrolPose(t);
                pos = _pose;
                facing = _heading;
            }
            else
            {
                if (center == null) return;
                float ang = phaseOffset + t * speed / Mathf.Max(1f, radius);
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                pos = center.position + dir * radius;
                facing = new Vector3(-dir.z, 0f, dir.x);
            }
            if (facing.sqrMagnitude < 0.0001f) facing = transform.forward;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing, Vector3.up));

            // Even fights: walking into the duel circle during a fight, it joins (if a wizard is unmatched) or fades away.
            // The wizards in the fight report it; the referee decides (once per monster).
            int ph = manager.phase;
            if (manager.evenFight && ph >= BattleManager.PHASE_JOINING && ph <= BattleManager.PHASE_PLAYBACK && client != null && client.IsInBattle() &&
                client.arenaCenter != null && _contactBattle != manager.battleId)
            {
                Vector3 c = pos - client.arenaCenter.position;
                c.y = 0f;
                if (c.magnitude <= contactRadius)
                {
                    _contactBattle = manager.battleId;
                    manager.RequestWandererContact(wandererIndex);
                }
            }

            if (Time.time < _nextTry || client == null || !client.CanFight()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 d = me.GetPosition() - pos;
            d.y = 0f;
            if (d.magnitude > aggroRadius) return;
            _nextTry = Time.time + 1.5f;
            manager.RequestEngageWander(wandererIndex);
        }

        /// <summary>Position and heading on the route at time t: loops wrap around, lines turn back at the ends.</summary>
        private void PatrolPose(float t)
        {
            float dist = t * speed + phaseOffset;
            bool back = false;
            if (loop) dist = dist % _length;
            else
            {
                dist = dist % (_length * 2f);
                if (dist > _length) { dist = _length * 2f - dist; back = true; }
            }
            int n = path.Length;
            int seg = 0;
            int segs = loop ? n : n - 1;
            while (seg < segs - 1 && dist > _cum[seg + 1]) seg++;
            Vector3 a = path[seg].position;
            Vector3 b = path[(seg + 1) % n].position;
            float segLen = _cum[seg + 1] - _cum[seg];
            float f = segLen > 0.001f ? Mathf.Clamp01((dist - _cum[seg]) / segLen) : 0f;
            _pose = Vector3.Lerp(a, b, f);
            Vector3 h = b - a;
            h.y = 0f;
            _heading = back ? -h : h;
        }
    }
}
