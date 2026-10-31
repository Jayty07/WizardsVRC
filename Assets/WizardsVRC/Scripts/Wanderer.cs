using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A wandering monster. With a <see cref="path"/> it walks a patrol route (a loop, or back and forth along a line);
    /// without one it circles its group's home spot. Its position comes from the server clock, so every client sees it in
    /// the same place without syncing. Touching it (walking within aggroRadius) pulls you into a duel with it: into its
    /// group's fight if one is going on nearby with a free seat (it joins that fight too), otherwise into a new fight at
    /// the nearest free ring. A group can have several fights at once (one per lane). It leaves its patrol for the fight,
    /// walks over to its seat and comes back a while afterwards; the rest of the patrol keeps walking (out of sight while
    /// they pass through one of the group's rings).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Wanderer : UdonSharpBehaviour
    {
        [Tooltip("Lane 0 of the group (the others are in Lane Managers / Lane Clients).")]
        public BattleManager manager;
        public BattleClient client;
        [Tooltip("Every fight lane of this monster group: a lane is one fight that can be going on at once.")]
        public BattleManager[] laneManagers;
        public BattleClient[] laneClients;
        [Tooltip("Touching it joins the group's fight in progress if its ring is within this distance (m) and has a free seat.")]
        public float joinNearRadius = 30f;
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
        [Tooltip("When a fight forms it walks over to its seat in the ring at this speed (m/s), for at most walkSeconds.")]
        public float walkSpeed = 7f;
        public float walkSeconds = 1.7f;

        private float _nextTry;
        private float[] _cum;
        private float _length;
        private Vector3 _pose;
        private Vector3 _heading;
        private Vector3 _bodyScale = Vector3.one;
        private Vector3 _bodyPos;
        private bool _wasHidden;
        private float _fadeStart = -100f;
        private int[] _contactBattle;
        private string _name = "";
        private float _walkStart = -100f;
        private int _walkLane;

        private void Start()
        {
            if (cardDatabase != null) _name = cardDatabase.MonsterName(monsterId) + "\nLevel " + cardDatabase.MonsterLevel(monsterId).ToString();
            if (label != null) label.text = _name;
            if (body != null) { _bodyScale = body.transform.localScale; _bodyPos = body.transform.localPosition; }
            if (laneManagers == null || laneManagers.Length == 0) { laneManagers = new BattleManager[] { manager }; laneClients = new BattleClient[] { client }; }
            _contactBattle = new int[laneManagers.Length];
            for (int i = 0; i < _contactBattle.Length; i++) _contactBattle[i] = -1;
            BuildRoute();
        }

        /// <summary>The lane whose fight this monster has left its patrol for (or is waiting to respawn from); -1 = none.</summary>
        private int HiddenLane()
        {
            for (int l = 0; l < laneManagers.Length; l++)
                if (laneManagers[l] != null && laneManagers[l].WandererHidden(wandererIndex)) return l;
            return -1;
        }

        private bool Fighting(BattleManager m)
        {
            return m != null && m.phase >= BattleManager.PHASE_JOINING && m.phase <= BattleManager.PHASE_PLAYBACK;
        }

        private float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b;
            d.y = 0f;
            return d.magnitude;
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

        /// <summary>Walking to the ring after a fight formed. False once there (or out of time): it then hides.</summary>
        private bool WalkToRing()
        {
            if (Time.time - _walkStart > walkSeconds || body == null) return false;
            Transform to = SeatFor();
            if (to == null) return false;
            Vector3 d = to.position - transform.position;
            d.y = 0f;
            if (d.magnitude < 0.6f) { _walkStart = -100f; return false; }
            if (!body.activeSelf) body.SetActive(true);
            Vector3 step = Vector3.MoveTowards(transform.position, to.position, walkSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(step, Quaternion.LookRotation(d.normalized, Vector3.up));
            return true;
        }

        /// <summary>The ring seat holding a monster like this one (the boss seat if none does yet).</summary>
        private Transform SeatFor()
        {
            if (_walkLane < 0 || _walkLane >= laneClients.Length) return null;
            BattleClient c = laneClients[_walkLane];
            BattleManager m = laneManagers[_walkLane];
            if (c == null || m == null) return null;
            Transform[] seats = c.seats;
            if (seats == null || seats.Length < BattleManager.SLOTS) return c.arenaCenter;
            for (int s = BattleManager.SIDE; s < BattleManager.SLOTS; s++)
                if (m.slotIds[s] == monsterId && seats[s] != null) return seats[s];
            return seats[BattleManager.BOSS_SLOT] != null ? seats[BattleManager.BOSS_SLOT] : c.arenaCenter;
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
            if (manager == null || laneManagers == null) return;
            int lane = HiddenLane();
            bool hidden = lane >= 0;
            // A patrol monster that just left its patrol for a fight walks over to its seat in that fight's ring, then
            // steps out of sight as the seated monster appears.
            if (hidden && !_wasHidden && laneManagers[lane].phase != BattleManager.PHASE_IDLE) { _walkStart = Time.time; _walkLane = lane; _fadeStart = -100f; }
            if (!hidden && _wasHidden && body != null)
            {
                body.transform.localScale = _bodyScale;
                body.transform.localPosition = _bodyPos;
                body.transform.localRotation = Quaternion.identity;
            }
            _wasHidden = hidden;
            if (hidden && WalkToRing())
            {
                if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }
            if (hidden && AnimateFade())
            {
                if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }
            if (hidden)
            {
                if (body != null && body.activeSelf) body.SetActive(false);
                if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }

            // Server time in seconds (a double, always counting up). The millisecond clock is an int that wraps and is
            // often negative in published instances, which froze every patrol at its first waypoint.
            double t = Networking.GetServerTimeInSeconds();
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
                double turn = Mathf.PI * 2f;
                double a = (t * speed / Mathf.Max(1f, radius)) % turn;
                if (a < 0) a += turn;
                float ang = phaseOffset + (float)a;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                pos = center.position + dir * radius;
                facing = new Vector3(-dir.z, 0f, dir.x);
            }
            if (facing.sqrMagnitude < 0.0001f) facing = transform.forward;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing, Vector3.up));

            // While one of the group's fights is on, the others keep walking but step out of sight as they pass through
            // its ring. In even fights, the fighting wizards report a monster that reaches their ring (it joins if a
            // wizard is unmatched, or fades away); the referee decides, once per monster.
            bool inCircle = false;
            for (int l = 0; l < laneManagers.Length; l++)
            {
                BattleManager m = laneManagers[l];
                BattleClient c = l < laneClients.Length ? laneClients[l] : null;
                if (m == null || c == null || c.arenaCenter == null || m.phase == BattleManager.PHASE_IDLE) continue;
                float dist = FlatDistance(pos, c.arenaCenter.position);
                if (!m.evenFight && dist <= contactRadius) inCircle = true;
                if (m.evenFight && Fighting(m) && c.IsInBattle() && _contactBattle[l] != m.battleId && dist <= contactRadius)
                {
                    _contactBattle[l] = m.battleId;
                    m.RequestWandererContact(wandererIndex);
                }
            }
            if (body != null && body.activeSelf == inCircle) body.SetActive(!inCircle);
            if (label != null && label.gameObject.activeSelf == inCircle) label.gameObject.SetActive(!inCircle);

            if (inCircle || Time.time < _nextTry || client == null || !client.CanFight()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 mine = me.GetPosition();
            if (FlatDistance(mine, pos) > aggroRadius) return;
            _nextTry = Time.time + 1.5f;
            int to = PickLane(mine);
            if (to >= 0) laneManagers[to].RequestEngageWander(wandererIndex);
        }

        /// <summary>
        /// Where touching this monster takes you: the group's nearest fight in progress within joinNearRadius that has a
        /// free seat (the monster joins it with you), otherwise a lane with no fight on (a new duel at the nearest free
        /// ring). -1 when every lane is busy and none near has room.
        /// </summary>
        private int PickLane(Vector3 mine)
        {
            int best = -1;
            float bestD = joinNearRadius;
            for (int l = 0; l < laneManagers.Length; l++)
            {
                BattleManager m = laneManagers[l];
                BattleClient c = l < laneClients.Length ? laneClients[l] : null;
                if (!Fighting(m) || c == null || c.arenaCenter == null || !m.HasFreeWizardSeat()) continue;
                float d = FlatDistance(mine, c.arenaCenter.position);
                if (d <= bestD) { bestD = d; best = l; }
            }
            if (best >= 0) return best;
            for (int l = 0; l < laneManagers.Length; l++)
                if (laneManagers[l] != null && laneManagers[l].phase == BattleManager.PHASE_IDLE) return l;
            return -1;
        }

        /// <summary>Position and heading on the route at time t: loops wrap around, lines turn back at the ends.</summary>
        private void PatrolPose(double t)
        {
            // Wrap in double precision (t is large), then work in floats along the route.
            double span = loop ? _length : _length * 2.0;
            double wrapped = (t * speed + phaseOffset) % span;
            if (wrapped < 0) wrapped += span;
            float dist = (float)wrapped;
            bool back = false;
            if (!loop && dist > _length) { dist = _length * 2f - dist; back = true; }
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
