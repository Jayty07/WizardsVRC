using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// The Sunfire Wyrm: the eastern dragon that spell summons, and the whole of its flight (SPELL_ANIMATIONS.md).
    /// SpellCinematic stages the spell round it (the charge, the film, the bursts on whoever it hits); this is the
    /// dragon itself. Local, one rig shared by every duel circle, like the rest of the spell rig.
    ///
    ///   arrival   it comes out of the caster's chest, jaws wide, in a gout of flame
    ///   display   it twirls round them three times, climbing; sweeps out and down to the floor behind them; and rears
    ///             up over their shoulder, where it comes to rest and roars
    ///   attack    it flies at its target and through it (through every target in turn round the rim of the ring, for
    ///             a cast that hits them all), loops round, pulls up before them and breathes fire on them
    ///   result    the fire goes on pouring over them as the spell's hit lands
    ///   exit      it burns away from the tail up
    /// and from the moment it is out until it is gone it sheds fire: flames off its back the whole way along, embers
    /// with them, more from its mane.
    ///
    /// How it flies: every place it goes is a waypoint (Plan), the waypoints are joined by a smooth curve and walked
    /// into points a fixed distance apart, each with an 'up' carried along the curve so the body never flips (it
    /// rolls upright at a steady rate and leans into its turns), and the head's distance along the curve is a
    /// function of time (HeadDistance). Each body segment sits a fixed distance behind the head on the same curve,
    /// swaying, facing the one ahead. Tools/sunfire_wyrm_flight.py is this file in Python, to check the numbers and
    /// to draw it (Tools/preview_sunfire_wyrm.py).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SunfireWyrm : UdonSharpBehaviour
    {
        private const int MAX_WAY = 96;
        private const int DENSE = 1500;
        private const int SUB = 8;
        private const int MAX_TARGETS = 4;
        private const int TWIRLS = 3;

        [Header("The dragon (built by WizardsVRCSceneBuilder.Wyrm.cs; +Z is forward for every part)")]
        [Tooltip("Everything that is the dragon: switched on while it is out.")]
        public GameObject dragon;
        [Tooltip("Its head: the pivot is the back of the skull.")]
        public Transform head;
        [Tooltip("Its lower jaw (a child of the head): swung open about its X axis.")]
        public Transform jaw;
        [Tooltip("Its body, neck to tail.")]
        public Transform[] segments;
        [Tooltip("Each segment's scale across and along at size 1, and its radius (where the flames on its back start).")]
        public float[] segmentAcross;
        public float[] segmentAlong;
        public float[] segmentRadius;
        [Tooltip("In the head (at size 1): where its breath leaves, and where its mane burns.")]
        public Vector3 mouthPoint = new Vector3(0f, -0.03f, 0.86f);
        public Vector3 manePoint = new Vector3(0f, 0.3f, -0.25f);

        [Header("How it is put together")]
        [Tooltip("Metres between body segments, and from the head's pivot to the first, at size 1.")]
        public float spacing = 0.38f;
        public float neck = 0.34f;
        [Tooltip("How far its jaw swings open (degrees).")]
        public float jawOpenDegrees = 38f;
        [Tooltip("How fast it rights itself (degrees of roll for each metre flown), and how far it leans into a turn.")]
        public float rollPerMetre = 70f;
        public float bank = 0.55f;

        [Header("Its fire (particle systems that only emit when told)")]
        [Tooltip("The flames it sheds all the time, and that whoever it burns goes up in.")]
        public ParticleSystem flames;
        public ParticleSystem embers;
        [Tooltip("Its breath: a cone of flame along the emitter's forward axis, and a brighter, faster jet inside it.")]
        public ParticleSystem breath;
        public ParticleSystem breathCore;
        [Tooltip("Flames a second, the whole dragon out, at size 1.")]
        public float flameRate = 150f;

        [Header("Its voice")]
        public AudioSource voice;
        public AudioClip roarSound;
        public AudioClip breathSound;

        // ------------------------------------------------------------------ the curve
        private Vector3[] _way = new Vector3[MAX_WAY];
        private float[] _wayS = new float[MAX_WAY];
        private int _wayCount;
        private Vector3[] _pt = new Vector3[DENSE];
        private Vector3[] _upv = new Vector3[DENSE];
        private int _count;
        private float _step = 0.15f;
        // The walk along the spline (a few waypoints a frame, through the charge).
        private int _bi;
        private Vector3 _bPrev, _bUp, _bTan;
        private float _bCarry;
        private bool _planned, _built;
        // Waypoints that matter: where it rests, each target, where it hovers; each lap of the caster.
        private int _markRest, _markHover;
        private int[] _markTarget = new int[MAX_TARGETS];
        private int[] _markLap = new int[TWIRLS];
        private float _sRest, _sHover, _sEnd;
        private float[] _sTarget = new float[MAX_TARGETS];
        private float[] _sLap = new float[TWIRLS];

        // ------------------------------------------------------------------ the cast
        private Vector3 _o, _f, _r, _toward, _side, _centre, _rest, _hover;
        private float _flip = 1f, _body = 1f, _wrap = 1f, _size = 1f, _chest = 1f, _spread, _dist;
        private Vector3[] _target = new Vector3[MAX_TARGETS];
        private float[] _targetAng = new float[MAX_TARGETS];
        private float[] _targetRad = new float[MAX_TARGETS];
        private int _targetCount;
        private float _tUnl, _tDisp, _tExp, _tBar, _tStrike, _tExit, _tAfter, _tRest, _tRoar, _tHover, _draw;

        // ------------------------------------------------------------------ as it flies
        private bool _shown;
        private float _sHead;
        private Vector3 _headAt, _headFwd = Vector3.forward, _mouthAt;
        private bool[] _segOn;
        private Vector3[] _segAt;
        private Vector3[] _segUp;
        private Vector3[] _segScale;
        private float[] _segBack;
        private int _segCount;
        private int _cue;
        private int _emitIx, _burnNext;
        private float _shedAcc, _maneAcc, _breathAcc, _coreAcc, _roarAcc;
        private bool[] _lit = new bool[MAX_TARGETS];
        private float[] _burnAcc = new float[MAX_TARGETS];

        private void Start()
        {
            Hide();
        }

        // ------------------------------------------------------------------ what SpellCinematic asks of it

        /// <summary>Where its head is.</summary>
        public Vector3 Head() { return _headAt; }
        /// <summary>Where it comes to rest behind the caster, and where it pulls up to breathe.</summary>
        public Vector3 Rest() { return _rest; }
        public Vector3 Hover() { return _hover; }
        /// <summary>The middle of whoever it burns, the flat direction to there from the caster, and that turned a quarter (the way it twirls).</summary>
        public Vector3 Centre() { return _centre; }
        public Vector3 Toward() { return _toward; }
        public Vector3 Side() { return _side; }
        /// <summary>How far its targets spread to either side of the line to their middle, and how far off that middle is.</summary>
        public float Spread() { return _spread; }
        public float Distance() { return _dist; }
        public float RestTime() { return _tRest; }
        public float RoarTime() { return _tRoar; }
        public float HoverTime() { return _tHover; }
        /// <summary>Its targets in the order it goes through them.</summary>
        public Vector3 TargetAt(int k) { return _target[Mathf.Clamp(k, 0, Mathf.Max(0, _targetCount - 1))]; }

        /// <summary>How many times it has been round the caster.</summary>
        public int Laps()
        {
            if (!_built) return 0;
            int n = 0;
            for (int j = 0; j < TWIRLS; j++) if (_sHead >= _sLap[j]) n++;
            return n;
        }

        /// <summary>How many of its targets its head has gone through.</summary>
        public int Passed()
        {
            if (!_built) return 0;
            int n = 0;
            for (int k = 0; k < _targetCount; k++) if (_sHead >= _sTarget[k]) n++;
            return n;
        }

        /// <summary>A burst of its flames at a point (SpellCinematic: on a target as it goes through them).</summary>
        public void Scorch(Vector3 at)
        {
            if (flames == null) return;
            flames.transform.position = at;
            flames.transform.localScale = Vector3.one * (1.4f * _size);
            flames.Emit(10);
        }

        public void Hide()
        {
            _planned = false;
            _built = false;
            _shown = false;
            if (dragon != null && dragon.activeSelf) dragon.SetActive(false);
            if (voice != null && voice.isPlaying) voice.Stop();
        }

        // ------------------------------------------------------------------ laying out the flight

        /// <summary>
        /// Lays the whole flight out for a cast: 'origin' the caster's feet in the middle of the ring, 'fwd' the way they
        /// face, 'flip' which way it twirls (+1 or -1), 'body' the caster's size, 'targetScale' how big whoever it hits
        /// is, 'targets' the floor points of everyone an area cast hits ('count' of them; none = just 'aim').
        /// </summary>
        public void Plan(Vector3 origin, Vector3 fwd, float flip, float body, float targetScale, Vector3 aim, Vector3[] targets, int count)
        {
            Vector3 up = Vector3.up;
            _o = origin;
            fwd.y = 0f;
            _f = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
            _flip = flip < 0f ? -1f : 1f;
            _r = Vector3.Cross(up, _f) * _flip;                  // the side it turns towards
            _body = Mathf.Clamp(body, 0.5f, 3f);
            _wrap = Mathf.Max(1f, 0.5f + 0.5f * _body);          // how wide it has to go to get round the caster
            _size = Mathf.Min(_wrap, 1.5f);                      // how big the dragon itself is
            _chest = 1.0f * Mathf.Clamp(targetScale, 0.5f, 3f);
            _targetCount = 0;
            if (targets != null)
                for (int i = 0; i < count && i < targets.Length && _targetCount < MAX_TARGETS; i++) _target[_targetCount++] = targets[i];
            if (_targetCount == 0) { _target[0] = aim; _targetCount = 1; }
            float size = _size, wrap = _wrap;
            _wayCount = 0;

            // ---- out of the caster's chest, forward, and round into the first coil
            float r0 = 1.55f * wrap, r1 = 1.7f * wrap;
            float h0 = 0.75f * size, h1 = 1.9f * _body + 1.3f * size;
            float a0 = 60f;
            float lift = 1.42f * _body;
            Way(_o + up * (1.25f * _body) + _f * (0.2f * _body));
            Way(_o + _f * (0.85f * wrap) + _r * (0.08f * wrap) + up * lift);
            Way(_o + _f * (1.3f * wrap) + _r * (0.5f * wrap) + up * (0.65f * lift + 0.35f * h0));
            Way(Polar(_f, _r, 38f, 1.5f * wrap, 0.3f * lift + 0.7f * h0));
            // ---- three turns round them, climbing
            int steps = TWIRLS * 12;
            for (int k = 0; k <= steps; k++)
            {
                float f = k / (float)steps;
                if (k > 0 && k % 12 == 0) _markLap[k / 12 - 1] = _wayCount;
                Way(Polar(_f, _r, a0 + 360f * TWIRLS * f, Mathf.Lerp(r0, r1, f), Mathf.Lerp(h0, h1, f)));
            }
            // ---- on round to the back, sweeping out and down to the floor behind them
            float a1 = a0 + 360f * TWIRLS;
            float coil = 1.15f * size;                           // the floor coil's radius
            float back = r1 + 1.6f * size;                       // how far behind the caster its middle is
            float low = 0.34f * size;
            Vector3 c = _o - _f * back;
            for (int k = 1; k <= 6; k++)
            {
                float f = k / 6f;
                Way(Polar(_f, _r, a1 + 120f * f, r1 + (back + coil - r1) * Mathf.Pow(f, 1.3f), Mathf.Lerp(h1, low, Ease(f))));
            }
            // ---- a quarter of a coil on the floor, coming round to face the caster's back
            Way(c + (_f * Mathf.Cos(225f * Mathf.Deg2Rad) + _r * Mathf.Sin(225f * Mathf.Deg2Rad)) * coil + up * low);
            Way(c + (_f * Mathf.Cos(270f * Mathf.Deg2Rad) + _r * Mathf.Sin(270f * Mathf.Deg2Rad)) * coil + up * low);
            // ---- and it rears up over their shoulder: there it rests
            Way(c - _r * (coil * 0.95f) + _f * (0.75f * size) + up * (low + 0.55f * size));
            Vector3 high = c - _r * (coil * 0.7f) + _f * (1.25f * size) + up * (low + 1.6f * size);
            Way(high);
            _rest = _o - _f * (0.55f * wrap + 0.55f * size) - _r * (0.3f * size) + up * (1.9f * _body + 1.35f * size);
            Way(_rest * 0.45f + high * 0.55f + up * (0.25f * size) - _f * (0.1f * size));
            _markRest = _wayCount;
            Way(_rest);
            float restH = _rest.y - _o.y;

            // ---- the attack
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < _targetCount; k++) sum += _target[k];
            _centre = sum / (float)_targetCount;
            Vector3 to = _centre - _o;
            to.y = 0f;
            _dist = to.magnitude;
            _toward = _dist > 0.5f ? to / _dist : _f;
            _side = Vector3.Cross(up, _toward) * _flip;
            _spread = 0f;
            for (int k = 0; k < _targetCount; k++) _spread = Mathf.Max(_spread, Mathf.Abs(Vector3.Dot(_target[k] - _o, _side)));
            float hoverH = Mathf.Max(2.6f, 1.9f * _body + 1.0f);
            float top = hoverH + 1.4f;
            if (_targetCount == 1)
            {
                Vector3 t = _target[0];
                _hover = _o + _toward * Mathf.Max(1.5f * size, _dist - 4.4f) + up * hoverH;
                Way(_o + _toward * (1.3f * wrap) + up * (restH - 0.15f));
                Way(t - _toward * 2.8f + up * (_chest + 0.7f));
                _markTarget[0] = _wayCount;
                Way(t + up * _chest);
                // over the top of them and back, then a turn down on to its mark
                Way(t + _toward * 1.2f + up * (_chest + 0.9f));
                Way(t + _toward * 1.5f + _side * 0.25f + up * (_chest + 2.5f));
                Way(t + _toward * 0.4f + _side * 0.6f + up * (_chest + 3.9f));
                Way(t - _toward * 1.9f + _side * 1.4f + up * (_chest + 4.0f));
                Way(_hover + (-_toward * 0.2f + _side * 2.2f + up * 1.4f) * size);
                Way(_hover + (-_toward * 0.98f + _side * 1.88f + up * 1.05f) * size);
                Way(_hover + (-_toward * 1.3f + _side * 1.1f + up * 0.7f) * size);
                Way(_hover + (-_toward * 0.98f + _side * 0.32f + up * 0.35f) * size);
            }
            else
            {
                // Round the rim through each of them in turn, the way it twirled, then spiralling in over the caster.
                for (int k = 0; k < _targetCount; k++)
                {
                    Vector3 d = _target[k] - _o;
                    d.y = 0f;
                    _targetAng[k] = Signed(_toward, d, up) * _flip;
                    _targetRad[k] = d.magnitude;
                }
                for (int i = 1; i < _targetCount; i++)
                    for (int j = i; j > 0 && _targetAng[j] < _targetAng[j - 1]; j--)
                    {
                        float fa = _targetAng[j]; _targetAng[j] = _targetAng[j - 1]; _targetAng[j - 1] = fa;
                        float fr = _targetRad[j]; _targetRad[j] = _targetRad[j - 1]; _targetRad[j - 1] = fr;
                        Vector3 fv = _target[j]; _target[j] = _target[j - 1]; _target[j - 1] = fv;
                    }
                _hover = _o + _toward * (1.5f * size) + up * hoverH;
                float ang0 = _targetAng[0], rad0 = _targetRad[0];
                Way(Polar(_toward, _side, ang0 * 0.75f - 15f, 1.2f * wrap, restH - 0.1f));
                Way(Polar(_toward, _side, ang0 - 30f, rad0 * 0.5f, 0.55f * restH + 0.45f * _chest));
                Way(Polar(_toward, _side, ang0 - 9f, Mathf.Max(1f, rad0 - 1f), _chest + 0.6f));
                for (int k = 0; k < _targetCount; k++)
                {
                    _markTarget[k] = _wayCount;
                    Way(_target[k] + up * _chest);
                    if (k + 1 < _targetCount && _targetAng[k + 1] - _targetAng[k] > 28f)
                        Way(Polar(_toward, _side, 0.5f * (_targetAng[k] + _targetAng[k + 1]), 0.5f * (_targetRad[k] + _targetRad[k + 1]), _chest + 0.2f));
                }
                float aL = _targetAng[_targetCount - 1], rL = _targetRad[_targetCount - 1];
                Way(Polar(_toward, _side, aL + 16f, rL - 0.3f, _chest + 1.0f));
                Way(Polar(_toward, _side, aL + 42f, rL * 0.74f, _chest + 1.0f + (top - _chest - 1.0f) * 0.6f));
                Way(Polar(_toward, _side, aL + 85f, rL * 0.52f, top));
                Way(Polar(_toward, _side, aL + 130f, rL * 0.36f, top - 0.2f));
                Way(Polar(_toward, _side, aL + 175f, Mathf.Max(rL * 0.27f, 1.7f * wrap), hoverH + 0.7f));
            }
            _markHover = _wayCount;
            Way(_hover);
            // ---- and away: up off its mark as it burns out
            Way(_hover + _toward * 1.0f + up * 0.5f);
            Way(_hover + _toward * 1.3f + up * 1.9f);
            Way(_hover + _toward * 0.6f + up * 3.2f);

            // ---- its body: how far behind the head each segment rides, and how big it is
            _segCount = segments != null ? segments.Length : 0;
            if (_segOn == null || _segOn.Length != _segCount)
            {
                _segOn = new bool[_segCount];
                _segAt = new Vector3[_segCount];
                _segUp = new Vector3[_segCount];
                _segScale = new Vector3[_segCount];
                _segBack = new float[_segCount];
            }
            for (int i = 0; i < _segCount; i++)
            {
                float across = segmentAcross != null && i < segmentAcross.Length ? segmentAcross[i] : 0.5f;
                float along = segmentAlong != null && i < segmentAlong.Length ? segmentAlong[i] : 0.59f;
                _segScale[i] = new Vector3(across, across, along) * size;
                _segBack[i] = (neck + i * spacing) * size;
                _segOn[i] = false;
                _segAt[i] = _o;
                _segUp[i] = up;
                if (segments[i] != null) segments[i].gameObject.SetActive(false);
            }
            if (head != null) head.gameObject.SetActive(false);

            BeginWalk();
            _planned = true;
            _shown = false;
            _cue = 0;
            _sHead = 0f;
            _headAt = _o + up * (1.25f * _body);
            _headFwd = _f;
            _mouthAt = _headAt;
            _emitIx = 0;
            _burnNext = _segCount - 1;
            _shedAcc = 0f;
            _maneAcc = 0f;
            _breathAcc = 0f;
            _coreAcc = 0f;
            _roarAcc = 0f;
            for (int k = 0; k < MAX_TARGETS; k++) { _lit[k] = false; _burnAcc[k] = 0f; }
        }

        /// <summary>
        /// When it does what (seconds from the start of the cast): the release, the display's start and end, the attack's
        /// start, the hit, the exit, and the end of the unleash (SpellCinematic's beats).
        /// </summary>
        public void Schedule(float tUnl, float tDisp, float tExp, float tBar, float tStrike, float tExit, float tAfter)
        {
            _tUnl = tUnl;
            _tDisp = tDisp;
            _tExp = tExp;
            _tBar = tBar;
            _tStrike = tStrike;
            _tExit = tExit;
            _tAfter = tAfter;
            float show = tExp - tDisp;
            _tRest = tDisp + 0.80f * show;
            _tRoar = tDisp + 0.84f * show;
            float attack = tStrike - tBar;
            _tHover = tStrike - Mathf.Min(0.9f, Mathf.Max(0.45f, 0.24f * attack));
            // It draws its head back just before it goes (if it has had time to settle).
            _draw = tBar - _tRest > 0.5f ? 0.45f * _size : 0f;
        }

        private void Way(Vector3 p)
        {
            if (_wayCount < MAX_WAY) _way[_wayCount++] = p;
        }

        /// <summary>A point 'r' out from the caster at 'deg' round from 'ahead' towards 'side', 'h' above their feet.</summary>
        private Vector3 Polar(Vector3 ahead, Vector3 side, float deg, float r, float h)
        {
            float a = deg * Mathf.Deg2Rad;
            return _o + (ahead * Mathf.Cos(a) + side * Mathf.Sin(a)) * r + Vector3.up * h;
        }

        private void BeginWalk()
        {
            float est = 0f;
            for (int i = 0; i + 1 < _wayCount; i++) est += Vector3.Distance(_way[i], _way[i + 1]);
            _step = Mathf.Max(0.15f, est * 1.08f / (DENSE - 8));
            _pt[0] = _way[0];
            _upv[0] = Vector3.up;
            _count = 1;
            for (int i = 0; i < _wayCount; i++) _wayS[i] = 0f;
            _bi = 0;
            _bPrev = _way[0];
            _bCarry = 0f;
            _bUp = Vector3.up;
            _bTan = (_way[1] - _way[0]).normalized;
            _built = false;
        }

        /// <summary>Walks the next 'segs' waypoints' worth of the curve into points '_step' apart.</summary>
        private void BuildSome(int segs)
        {
            int n = _wayCount;
            while (segs > 0 && _bi < n - 1)
            {
                int i = _bi;
                Vector3 p0 = _way[i > 0 ? i - 1 : 0], p1 = _way[i], p2 = _way[i + 1], p3 = _way[i + 2 < n ? i + 2 : n - 1];
                // A Catmull-Rom span from p1 to p2.
                Vector3 a = (p3 - p0) * 0.5f + (p1 - p2) * 1.5f;
                Vector3 b = p0 - p1 * 2.5f + p2 * 2f - p3 * 0.5f;
                Vector3 c = (p2 - p0) * 0.5f;
                for (int k = 1; k <= SUB; k++)
                {
                    float u = k / (float)SUB;
                    Vector3 q = ((a * u + b) * u + c) * u + p1;
                    float d = Vector3.Distance(q, _bPrev);
                    while (_bCarry + d >= _step && d > 0.00001f && _count < DENSE)
                    {
                        _bPrev = Vector3.Lerp(_bPrev, q, (_step - _bCarry) / d);
                        AddPoint(_bPrev);
                        d = Vector3.Distance(q, _bPrev);
                        _bCarry = 0f;
                    }
                    _bCarry += d;
                    _bPrev = q;
                }
                _wayS[i + 1] = (_count - 1) * _step + _bCarry;
                _bi++;
                segs--;
            }
            if (_bi < n - 1) return;
            _built = true;
            _sRest = _wayS[_markRest];
            _sHover = _wayS[_markHover];
            _sEnd = (_count - 1) * _step;
            for (int k = 0; k < _targetCount; k++) _sTarget[k] = _wayS[_markTarget[k]];
            for (int j = 0; j < TWIRLS; j++) _sLap[j] = _wayS[_markLap[j]];
        }

        /// <summary>One more point on the walked curve, and its 'up': the last one's carried along, rolling upright as it goes.</summary>
        private void AddPoint(Vector3 p)
        {
            Vector3 tan = (p - _pt[_count - 1]).normalized;
            Vector3 u = _bUp - tan * Vector3.Dot(_bUp, tan);
            u = u.sqrMagnitude > 0.00000001f ? u.normalized : _bUp;
            float level = 1f - Mathf.Abs(tan.y);
            if (level > 0.12f)
            {
                // Upright, leaning into the turn; it rolls there at a steady rate (not at all where it climbs or dives).
                Vector3 want = Vector3.up - tan * tan.y + (tan - _bTan) * (bank / _step);
                want -= tan * Vector3.Dot(want, tan);
                if (want.sqrMagnitude > 0.00000001f)
                {
                    float ang = Signed(u, want, tan);
                    float lim = rollPerMetre * _step * Mathf.Min(1f, (level - 0.12f) * 3f);
                    u = (Quaternion.AngleAxis(Mathf.Clamp(ang, -lim, lim), tan) * u).normalized;
                }
            }
            _bUp = u;
            _bTan = tan;
            _pt[_count] = p;
            _upv[_count] = u;
            _count++;
        }

        private Vector3 At(float s)
        {
            float x = s / _step;
            int k = Mathf.FloorToInt(x);
            if (k > _count - 2) k = _count - 2;
            if (k < 0) k = 0;
            return Vector3.Lerp(_pt[k], _pt[k + 1], x - k);
        }

        private Vector3 UpAt(float s)
        {
            int k = Mathf.FloorToInt(s / _step + 0.5f);
            if (k > _count - 1) k = _count - 1;
            if (k < 0) k = 0;
            return _upv[k];
        }

        /// <summary>Degrees from a to b about 'axis'.</summary>
        private float Signed(Vector3 a, Vector3 b, Vector3 axis)
        {
            float ang = Vector3.Angle(a, b);
            return Vector3.Dot(Vector3.Cross(a, b), axis) < 0f ? -ang : ang;
        }

        private float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>How far along (0-1) after 'k' of the time: speeding up over the first 'a', steady, slowing over the last 'd'.</summary>
        private float Trap(float k, float a, float d)
        {
            k = Mathf.Clamp01(k);
            float area = 1f - a * 0.5f - d * 0.5f;
            if (k < a) return k * k / (2f * a) / area;
            if (k < 1f - d) return (k - a * 0.5f) / area;
            return 1f - (1f - k) * (1f - k) / (2f * d) / area;
        }

        /// <summary>How far along the curve its head is at 't'.</summary>
        private float HeadDistance(float t)
        {
            if (t <= _tUnl) return 0f;
            if (t < _tRest) return _sRest * Trap((t - _tUnl) / Mathf.Max(0.05f, _tRest - _tUnl), 0.10f, 0.30f);
            if (t < _tBar) return _sRest - _draw * Ease((t - (_tBar - 0.35f)) / 0.35f);
            if (t < _tHover)
            {
                float s0 = _sRest - _draw;
                return s0 + (_sHover - s0) * Trap((t - _tBar) / Mathf.Max(0.05f, _tHover - _tBar), 0.30f, 0.35f);
            }
            if (t < _tExit) return _sHover;
            float k = Mathf.Clamp01((t - _tExit) / Mathf.Max(0.05f, _tAfter - _tExit));
            return _sHover + (_sEnd - _sHover) * (1f - (1f - k) * (1f - k));
        }

        /// <summary>Where its fire goes: swept along its targets and back (one target: round it a little).</summary>
        public Vector3 BreathAim(float t)
        {
            if (_targetCount <= 1)
            {
                float w = (t - _tHover) * 5f;
                return _target[0] + Vector3.up * (_chest * (0.8f + 0.25f * Mathf.Sin(w * 1.3f))) + _side * (0.35f * Mathf.Sin(w));
            }
            float span = Mathf.Max(0.2f, _tExit - _tHover);
            float u = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * 1.5f * (t - _tHover) / span);
            float x = u * (_targetCount - 1);
            int k = Mathf.Min(Mathf.FloorToInt(x), _targetCount - 2);
            if (k < 0) k = 0;
            return Vector3.Lerp(_target[k], _target[k + 1], x - k) + Vector3.up * (_chest * 0.9f);
        }

        /// <summary>What's left of segment i (-1 = the head) once the fire has got 'burn' segments along from the tail.</summary>
        private float Burnt(int i, float burn)
        {
            if (burn <= 0f) return 1f;
            return Mathf.Clamp01(1f - (burn - (_segCount - 1 - i)) / 3f);
        }

        // ------------------------------------------------------------------ every frame

        /// <summary>
        /// Called by SpellCinematic every frame of the cast: 't' seconds from its start. Through the charge it works its
        /// curve out, a little each frame; from the release it flies.
        /// </summary>
        public void Animate(float t, float dt)
        {
            if (!_planned) return;
            if (!_built)
            {
                BuildSome(t >= _tUnl - 0.1f ? 1000 : 6);
                if (!_built) return;
            }
            if (t < _tUnl || t >= _tAfter)
            {
                if (_shown)
                {
                    _shown = false;
                    if (dragon != null) dragon.SetActive(false);
                }
                return;
            }
            Vector3 up = Vector3.up;
            float size = _size;
            if (!_shown)
            {
                _shown = true;
                if (dragon != null) dragon.SetActive(true);
                if (head != null) head.gameObject.SetActive(true);
            }

            // ---- the head
            float s = HeadDistance(t);
            _sHead = s;
            Vector3 p = At(s);
            Vector3 d = At(s + 0.7f * size) - p;
            if (d.sqrMagnitude < 0.0025f) d = p - At(s - 0.3f * size);
            d = d.sqrMagnitude > 0.00000001f ? d.normalized : _headFwd;
            Vector3 u = UpAt(s);
            float open = 0.06f;
            // Out of the chest: jaws wide as it bursts out.
            open = Mathf.Max(open, 0.7f * (1f - Ease((t - _tUnl) / 0.7f)));
            float hold = 0f;
            Vector3 aim = d;
            if (t >= _tRest && t < _tBar)
            {
                // Come to rest: it looks out over the caster at whoever it is about to burn, and roars.
                hold = Ease((t - _tRest) / 0.4f) * (1f - Ease((t - (_tBar - 0.2f)) / 0.2f));
                Vector3 look = (_centre + up * 1.2f - p).normalized;
                float roar = t >= _tRoar ? Ease((t - _tRoar) / 0.18f) * (1f - Ease((t - (_tExp - 0.22f)) / 0.22f)) : 0f;
                aim = (look + up * (0.55f * roar)).normalized;
                open = Mathf.Max(open, roar);
                p += up * (0.07f * size * Mathf.Sin(t * 2.6f)) + _r * (0.05f * size * Mathf.Sin(t * 1.7f));
            }
            else if (t >= _tBar && t < _tHover)
            {
                // Jaws open as it goes through them.
                for (int k = 0; k < _targetCount; k++)
                    open = Mathf.Max(open, 0.75f * Mathf.Exp(-Mathf.Abs(s - _sTarget[k]) / (1.6f * size)));
            }
            else if (t >= _tHover)
            {
                hold = Ease((t - _tHover) / 0.25f);
                if (t < _tExit)
                {
                    aim = (BreathAim(t) - p).normalized;
                    open = Mathf.Max(open, 0.85f + 0.12f * Mathf.Sin(t * 31f));
                    p += up * (0.06f * size * Mathf.Sin(t * 3.1f)) - aim * (0.05f * size * Mathf.Sin(t * 23f));
                }
                else
                {
                    // Burning away: its head lifts to the sky.
                    float k = Mathf.Clamp01((t - _tExit) / Mathf.Max(0.05f, _tAfter - _tExit));
                    aim = ((BreathAim(_tExit) - p).normalized * (1f - k) + up * (1.4f * k)).normalized;
                    open = Mathf.Max(open, 0.5f * (1f - k));
                }
            }
            if (hold > 0f) d = Vector3.Lerp(d, aim, hold).normalized;
            Vector3 level = up - d * d.y;
            if (hold > 0f && level.sqrMagnitude > 0.04f) u = Quaternion.AngleAxis(Signed(u, level, d) * hold, d) * u;
            u -= d * Vector3.Dot(u, d);
            u = u.sqrMagnitude > 0.00000001f ? u.normalized : up;
            float burn = t < _tExit ? 0f : (t - _tExit) / Mathf.Max(0.05f, _tAfter - _tExit) * (_segCount + 7);
            float headScale = Mathf.Clamp(s / (0.7f * size), 0.05f, 1f) * Burnt(-1, burn);
            Quaternion headRot = Quaternion.LookRotation(d, u);
            _headAt = p;
            _headFwd = d;
            _mouthAt = p + headRot * (mouthPoint * (size * headScale));
            if (head != null)
            {
                bool headOn = headScale > 0.01f;
                if (head.gameObject.activeSelf != headOn) head.gameObject.SetActive(headOn);
                head.SetPositionAndRotation(p, headRot);
                head.localScale = Vector3.one * Mathf.Max(0.01f, size * headScale);
            }
            if (jaw != null) jaw.localRotation = Quaternion.Euler(open * jawOpenDegrees, 0f, 0f);
            if (voice != null) voice.transform.position = p;

            // ---- the body: each segment a fixed distance behind the head on the curve, swaying, looking at the one ahead
            Vector3 ahead = p;
            int showing = 0;
            for (int i = 0; i < _segCount; i++)
            {
                Transform seg = segments[i];
                if (seg == null) continue;
                float si = s - _segBack[i];
                float k = si > 0f ? Mathf.Min(1f, si / (0.7f * size)) * Burnt(i, burn) : 0f;
                bool on = k > 0.01f;
                if (_segOn[i] != on)
                {
                    _segOn[i] = on;
                    seg.gameObject.SetActive(on);
                }
                if (si <= 0f) continue;
                Vector3 sp = At(si);
                Vector3 su = UpAt(si);
                Vector3 sd = At(si + 0.2f) - sp;
                Vector3 across = Vector3.Cross(su, sd).normalized;
                float sway = (0.03f + 0.11f * (i + 1f) / _segCount) * size;
                float ph = t * 5.5f - i * 0.55f;
                sp += across * (sway * Mathf.Sin(ph)) + su * (sway * 0.5f * Mathf.Cos(ph * 0.8f));
                Vector3 dir = ahead - sp;
                dir = dir.sqrMagnitude > 0.00000001f ? dir.normalized : d;
                if (hold > 0f && i < 6)
                {
                    // While its head is still, its neck straightens up behind it.
                    Vector3 lv = up - dir * dir.y;
                    if (lv.sqrMagnitude > 0.04f) su = Quaternion.AngleAxis(Signed(su, lv, dir) * hold * (1f - i / 6f), dir) * su;
                }
                su -= dir * Vector3.Dot(su, dir);
                su = su.sqrMagnitude > 0.00000001f ? su.normalized : up;
                ahead = sp;
                _segAt[i] = sp;
                _segUp[i] = su;
                if (!on) continue;
                if (k > 0.3f) showing++;
                seg.SetPositionAndRotation(sp, Quaternion.LookRotation(dir, su));
                seg.localScale = _segScale[i] * k;
            }

            Cues(t);
            Shed(t, dt, p, headRot, headScale, showing, burn);
            Breathe(t, dt);
        }

        /// <summary>Its voice, and the gouts of flame as it bursts out and as it roars.</summary>
        private void Cues(float t)
        {
            if (_cue == 0)
            {
                // It bursts out of the caster: a gout of flame ahead of it.
                _cue = 1;
                Gout(_o + Vector3.up * (1.25f * _body), Quaternion.LookRotation(_f + Vector3.up * 0.2f), 0.5f * _size, 40);
                if (voice != null && roarSound != null) voice.PlayOneShot(roarSound, 0.5f);
            }
            if (_cue == 1 && t >= _tRoar)
            {
                _cue = 2;
                if (voice != null && roarSound != null) voice.PlayOneShot(roarSound, 1f);
            }
            if (_cue == 2 && t >= _tHover)
            {
                _cue = 3;
                if (voice != null && breathSound != null) voice.PlayOneShot(breathSound, 0.9f);
            }
        }

        private void Gout(Vector3 at, Quaternion rot, float scale, int count)
        {
            if (breath == null || count <= 0) return;
            breath.transform.SetPositionAndRotation(at, rot);
            breath.transform.localScale = Vector3.one * scale;
            breath.Emit(count);
        }

        /// <summary>
        /// It is always shedding fire: flames off its back the whole way along (each in turn, so the whole of it burns
        /// however few a frame brings), embers with them, more from its mane; and as it burns away each length of it
        /// goes up in a puff.
        /// </summary>
        private void Shed(float t, float dt, Vector3 p, Quaternion headRot, float headScale, int showing, float burn)
        {
            if (flames == null) return;
            float size = _size;
            Vector3 maneAt = p + headRot * (manePoint * size);
            _shedAcc += dt * flameRate * size * (showing + 1) / (_segCount + 1f);
            int n = Mathf.FloorToInt(_shedAcc);
            _shedAcc -= n;
            if (n > 40) n = 40;
            for (int e = 0; e < n; e++)
            {
                _emitIx = (_emitIx + 7) % (_segCount + 1);
                int i = _emitIx;
                if (i == _segCount)
                {
                    if (headScale > 0.3f)
                    {
                        flames.transform.position = maneAt;
                        flames.transform.localScale = Vector3.one * size;
                        flames.Emit(1);
                    }
                    continue;
                }
                if (!_segOn[i]) continue;
                float r = segmentRadius != null && i < segmentRadius.Length ? segmentRadius[i] : 0.2f;
                flames.transform.position = _segAt[i] + _segUp[i] * (r * size * 0.9f);
                flames.transform.localScale = Vector3.one * (size * (0.55f + 1.7f * r));
                flames.Emit(1);
                if (i % 3 == 0 && embers != null)
                {
                    embers.transform.position = _segAt[i];
                    embers.transform.localScale = Vector3.one * size;
                    embers.Emit(1);
                }
            }
            if (headScale > 0.3f)
            {
                _maneAcc += dt * 30f;
                int m = Mathf.FloorToInt(_maneAcc);
                _maneAcc -= m;
                if (m > 0)
                {
                    flames.transform.position = maneAt;
                    flames.transform.localScale = Vector3.one * size;
                    flames.Emit(m);
                }
            }
            while (_burnNext >= 0 && Burnt(_burnNext, burn) < 0.5f)
            {
                flames.transform.position = _segAt[_burnNext];
                flames.transform.localScale = Vector3.one * (size * 1.2f);
                flames.Emit(5);
                if (embers != null)
                {
                    embers.transform.position = _segAt[_burnNext];
                    embers.transform.localScale = Vector3.one * size;
                    embers.Emit(3);
                }
                _burnNext--;
            }
        }

        /// <summary>
        /// Its roar (a gout from its jaws for as long as it lasts), and its breath: from when it pulls up before its
        /// targets until the exit, a cone of flame from its mouth to wherever BreathAim points, sized to reach; whoever
        /// the fire has reached burns until it stops.
        /// </summary>
        private void Breathe(float t, float dt)
        {
            float size = _size;
            if (t >= _tRoar && t < _tExp && breath != null)
            {
                _roarAcc += dt * 70f;
                int n = Mathf.FloorToInt(_roarAcc);
                _roarAcc -= n;
                Gout(_mouthAt, Quaternion.LookRotation(_headFwd), 0.32f * size, n);
            }
            if (t < _tHover || t >= _tExit) return;
            Vector3 aim = BreathAim(t);
            Vector3 to = aim - _mouthAt;
            float reach = to.magnitude;
            if (reach < 0.1f) return;
            Quaternion rot = Quaternion.LookRotation(to / reach);
            if (breath != null)
            {
                _breathAcc += dt * 200f;
                int n = Mathf.FloorToInt(_breathAcc);
                _breathAcc -= n;
                Gout(_mouthAt, rot, reach / 5.8f, n);
            }
            if (breathCore != null)
            {
                _coreAcc += dt * 80f;
                int n = Mathf.FloorToInt(_coreAcc);
                _coreAcc -= n;
                if (n > 0)
                {
                    breathCore.transform.SetPositionAndRotation(_mouthAt, rot);
                    breathCore.transform.localScale = Vector3.one * (reach / 5.4f);
                    breathCore.Emit(n);
                }
            }
            if (flames == null) return;
            bool arrived = t - _tHover > 0.3f;
            for (int k = 0; k < _targetCount; k++)
            {
                if (arrived && !_lit[k])
                {
                    Vector3 off = aim - _target[k];
                    off.y = 0f;
                    if (off.sqrMagnitude < 2.56f) _lit[k] = true;
                }
                if (!_lit[k]) continue;
                _burnAcc[k] += dt * 34f;
                int n = Mathf.FloorToInt(_burnAcc[k]);
                _burnAcc[k] -= n;
                for (int e = 0; e < n; e++)
                {
                    flames.transform.position = _target[k] + new Vector3(Random.Range(-0.45f, 0.45f), 0.2f + Random.value * 1.5f, Random.Range(-0.45f, 0.45f));
                    flames.transform.localScale = Vector3.one * 1.25f;
                    flames.Emit(1);
                }
            }
        }
    }
}
