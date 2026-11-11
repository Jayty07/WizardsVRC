using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Every staged spell (the attacks, the heals, the dispel), played through six beats by one formula
    /// (SPELL_ANIMATIONS.md; its numbers are BattleManager's and CardDatabase.Plan*). The caster, a wizard, a monster or a
    /// boss, is the subject: the power comes out of them in the middle of the ring. Local, one rig shared by every duel
    /// circle; BattleClient starts it for each such spell's turn, so every client plays the same beats at the same times
    /// from the synced round. The whole turn is about 13 s at 1 pip to 17 s at 7 (5 s of it the display), plus the expending.
    ///
    ///   cast      the caster vanishes from their seat in a burst and appears in the middle of the ring facing their target
    ///             (a wizard's own client teleports them, their view irising shut and open in their school's colour; a
    ///             monster's body is moved there on every client). They charge: a short gather, then the school's cast
    ///             sound, ending on the release. The school's rune draws itself on the floor under them with the spell's
    ///             own flourish, and each tier of power adds to it:
    ///               tier 1 (1-2 pips)  the rune, the flourish, the light building
    ///               tier 2 (3-4 pips)  + grit lifting off the floor, sparks racing round the rune's rim, a quickening
    ///                                    heartbeat of rings, the hum of the charge
    ///               tier 3 (5-6 pips)  + everything but the magic goes dark, sparks spiral up round them, power crackles
    ///                                    out of them, a column of light, and it's all drawn in just before it goes
    ///               tier 4 (7 pips)    + the school's rune opens in the sky and pours light down on them
    ///   arrival   the release: a shock off the caster, and the spell's subject arrives the spell's own way (a core of
    ///             power over the caster, or a creature spell's creature beside them)
    ///   display   it shows off, the spell's own way
    ///   (expend   the blades, traps and shields the cast uses go, one at a time: BattleClient; the film holds them)
    ///   attack    the spell's own delivery; a spell of 5 pips or more strikes twice (a lighter wave shed from the core,
    ///             which makes the target flinch, then the spell itself)
    ///   result    the finisher's hit: the spell's own kind of impact, and its results land (BattleClient), on every
    ///             target at once for an area spell
    ///   exit      the power leaves the spell's own way, and the caster vanishes back to their seat
    ///
    /// Each spell's recipe picks its flourish, arrival, display, delivery, impact, exit and film (CardDatabase.cineStyles
    /// and cineBeats, WizardsVRCSceneBuilder.SpellStyles.cs), so no two play alike. Everyone else in the fight watches the
    /// whole thing on the film screen; the caster sees the cast with their own eyes and the film from the release. A spell
    /// that helps its target (a heal) lands gently. A summoned minion casts from its seat with no staging at all (one
    /// volley, as the delivery times it).
    ///
    /// A spell can have staging all its own (CardDatabase.SPECIAL_*, SetSpecial): it keeps the six beats and their
    /// times, the charge, how its hit lands and how it leaves, but its arrival, display and attack are played by a rig
    /// of its own and filmed with shots of its own. Sunfire Wyrm is one (SunfireWyrm, AnimateWyrm, WyrmShot).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SpellCinematic : UdonSharpBehaviour
    {
        public const int MAX_HEADS = 12;
        public const int SHAPES = 3;

        public CutscenePlayer cutscenePlayer;

        [Header("Fade (an inside-out sphere on your head, WizardsVRC/SpellFade)")]
        public Transform fadeSphere;
        public Renderer fadeRenderer;

        [Header("Charge")]
        [Tooltip("The school's rune, flat on the floor under the caster (WizardsVRC/GlyphDraw).")]
        public Transform runeCircle;
        public Renderer runeRenderer;
        public Transform runeRing;
        [Tooltip("Energy streaming in (particles that fly inward): to the caster, or to the target before an implosion.")]
        public ParticleSystem chargeAura;
        [Tooltip("A column of light round the caster; its pivot is on the ground and it's 1 m tall at scale 1.")]
        public Transform castPillar;
        [Tooltip("Two rings turning round the caster on tilted axes.")]
        public Transform[] gyroRings;
        [Tooltip("Bursts as the caster vanishes and appears, and the sparks of a spiralling wind-up.")]
        public ParticleSystem portalBurst;

        [Header("Volleys")]
        [Tooltip("A cone of sparks off the core as a volley is let go (it fires along its forward axis).")]
        public ParticleSystem releaseBurst;
        [Tooltip("The spell's bolts (each trailing sparks); a creature takes the first one's place.")]
        public Transform[] heads;
        [Tooltip("Each bolt's shapes, index bolt * 3 + shape (orb, shard, disc): one is shown.")]
        public Renderer[] headShapes;
        [Tooltip("A beam from the caster to the target; its pivot is at its start and it's 1 m long (+Y) at scale 1.")]
        public Transform beam;
        [Tooltip("Rings the spell goes into and comes out of (flat, +Y facing the way through).")]
        public Transform[] portals;

        [Header("Impact")]
        public ParticleSystem impactBurst;
        [Tooltip("Sparks that fly out and fall (a shatter, the chips off spikes).")]
        public ParticleSystem shardBurst;
        public Transform impactFlash;
        public Transform[] shockRings;
        [Tooltip("A column of light on the target; pivot on the ground, 1 m tall at scale 1.")]
        public Transform impactPillar;
        [Tooltip("Spikes that burst out of the ground round the target (pivot at their base, 1 m tall at scale 1).")]
        public Transform[] spikes;
        [Tooltip("The school's rune stamped down on the target (WizardsVRC/GlyphDraw, flat).")]
        public Transform sigilStamp;
        public Renderer sigilRenderer;
        [Tooltip("Embers drifting up (off the target after the hit, or off the floor round the caster as they wind up).")]
        public ParticleSystem embers;
        public Light fxLight;

        [Header("School looks (indexed by School; the last is General)")]
        public Material[] sparkMats;
        public Renderer[] sparkRenderers;
        public Material[] glowMats;
        public Renderer[] glowRenderers;
        public Material[] ringMats;
        public Renderer[] ringRenderers;
        public Material[] pillarMats;
        public Renderer[] pillarRenderers;
        public Material[] coreMats;
        public Renderer[] coreRenderers;
        public Texture2D[] glyphDrawTex;
        public Color[] glyphColors;
        public Color[] glyphEdgeColors;

        [Header("Sound")]
        public AudioSource sfx;
        public AudioClip whooshSound;
        public AudioClip releaseSound;
        public AudioClip impactSound;
        public AudioClip impactBigSound;

        [Header("Shots")]
        [Tooltip("How far the wide shot turns round the ring each second as the spell settles.")]
        public float wideTurnDegrees = 14f;

        [Header("The staging formula's numbers")]
        public CardDatabase cardDatabase;

        [Header("Charge tiers (2: 3+ pips, 3: 5+ pips, 4: 7 pips)")]
        [Tooltip("Tier 2+: grit and motes lifting off the floor round the caster (it emits upward from a disc).")]
        public ParticleSystem debris;
        [Tooltip("Tier 2+: sparks racing round the rune's rim.")]
        public ParticleSystem rimSparks;
        [Tooltip("Tier 3+: crackling streaks lashing out of the caster.")]
        public ParticleSystem arcs;
        [Tooltip("Tier 3+: sparks spiralling up round the caster.")]
        public ParticleSystem vortex;
        [Tooltip("Tier 4: light pouring down from the rune in the sky (it emits downward from a disc).")]
        public ParticleSystem skyfall;
        [Tooltip("Tier 3+: a great sphere round the ring that darkens everything but the magic (WizardsVRC/DuskVeil: its _Color's alpha).")]
        public Transform duskDome;
        public Renderer duskRenderer;
        [Tooltip("Tier 4: the school's rune, huge, turning in the sky over the ring (WizardsVRC/GlyphDraw).")]
        public Transform skySigil;
        public Renderer skySigilRenderer;

        [Header("Unleash")]
        [Tooltip("A ring of sparks racing out along the floor (the release, the creature's arrival, under big hits).")]
        public ParticleSystem shockwave;
        [Tooltip("The gathered power, held over the caster: 1 m across at scale 1. Waves are shed from it; the finisher is it.")]
        public Transform powerCore;
        [Tooltip("Sparks drawn into the core.")]
        public ParticleSystem coreMotes;
        [Tooltip("The rising hum of the charge (it ends on the release).")]
        public AudioSource chargeAudio;
        public AudioClip chargeSound;
        [Tooltip("The boom of the surge (tier 2 and up; smaller spells use the release sound).")]
        public AudioClip surgeSound;

        [Header("The battle aura (the charge: see AnimateAura)")]
        [Tooltip("The flame-shaped shell of light round the caster (WizardsVRC/AuraFlame), and a second, paler one for spells of 5 pips or more.")]
        public Transform auraShell;
        public Renderer auraShellRenderer;
        public Transform auraShellOuter;
        public Renderer auraShellOuterRenderer;
        [Tooltip("Tongues of flame racing up inside the shell, and streaks of wind rushing up round it (3 pips or more).")]
        public ParticleSystem auraFlames;
        public ParticleSystem auraStreaks;
        [Tooltip("The flames' material for each school (as sparkMats), and the renderers that wear it.")]
        public Material[] flameMats;
        public Renderer[] flameRenderers;
        [Tooltip("The aura catching alight.")]
        public AudioClip igniteSound;

        [Header("Spells with staging of their own (CardDatabase.SPECIAL_*)")]
        [Tooltip("Sunfire Wyrm's dragon: it flies the spell's arrival, display and attack (AnimateWyrm).")]
        public SunfireWyrm wyrm;

        // ------------------------------------------------------------------ state
        private bool _on;
        private bool _me;
        private bool _watch;
        private bool _cine;
        private bool _creature;
        private float _tScale = 1f;
        // A monster caster's body, moved to the middle of the ring and back.
        private Transform _casterBody;
        private Vector3 _bodyLocalPos;
        private Quaternion _bodyLocalRot;
        private bool _bodyMoved;
        // Distance scale (lateral swings, arcs) and where the spell meets its target / where a creature pulls up.
        private float _reach = 1f;
        private Vector3 _hit;
        // An area spell's targets (SetTargets): its bolts and impacts land on each of them, not between them.
        private Vector3[] _targets = new Vector3[4];
        private int _targetCount;
        private Vector3 _stop;
        private BattleClient _owner;
        private float _t0;
        private int _cue;
        private bool _teleported;
        private bool _returned;
        private bool _filmOn;

        private Vector3 _seatPos;
        private Quaternion _seatRot;
        // Where an interrupted caster is sent back to (kept apart: the next cast may already have begun).
        private Vector3 _retPos;
        private Quaternion _retRot;
        private Vector3 _from;
        private Vector3 _fwd;
        private Vector3 _right;
        private Vector3 _aim;
        // Where the ground effects of the hit go: the target, or an area spell's first target (not the empty middle of its side).
        private Vector3 _impactAt;
        private Vector3 _hand;
        private float _body;
        private float _ground;

        private int _delivery, _impact, _charge, _cam, _count, _shape, _height;
        private float _flip;
        private int _look;
        private int _pips;
        // The pip cost as the effects see it: it climbs faster than the cost (1, 2, 4, 6, 8, 10, 13 for 1 to 7 pips), and
        // every size, count and rate below is worked out from it, so a costly spell is far bigger than a cheap one.
        private int _vis;
        private bool _aoe;
        private float _scale;
        private string _caster;
        private string _spell;
        private Color _color;
        // How much power the charge shows: 1 (1-2 pips), 2 (3-4), 3 (5-6), 4 (7).
        private int _tier = 1;
        // The core over the caster, where a summoned creature arrives, and where the volley in the air left from.
        private Vector3 _corePos;
        private Vector3 _summonAt;
        private Vector3 _origin;

        // The six beats, in seconds from the start:
        //   cast     _tHalf (dark: moved to the middle), _tCast (there: the gather begins), _tSound (the cast sound begins)
        //   arrival  _tUnl (the release)        display  _tDisp        (expend  _tExp: blades, traps and shields go)
        //   attack   _tBar (the first volley's slot)     result  _tStrike (the hit)     exit  _tExit
        //   then _tAfter (the unleash is over), _tBack (dark: back to the seat), _tEnd
        private float _tHalf, _tCast, _tSound, _tUnl, _tDisp, _tExp, _tBar, _tStrike, _tExit, _tAfter, _tBack, _tEnd;
        // The spell's own arrival, display and exit (CardDatabase.ARRIVE_*, SHOW_*, EXIT_*), and whether it helps its target.
        private int _arrive, _show, _leave;
        private bool _friendly;
        private int _arrCue, _showCue, _showBeats, _exitCue;
        // Aftershocks of the finisher's hit that have gone off (a spell of 3 pips or more has some).
        private int _afterDone;
        private float _arrAcc, _showAcc, _exitAcc;

        // Volleys: the waves, then the finisher (the last). Each has a slot: it opens (the core gathers), flies, hits, ends.
        private const int MAX_VOLLEYS = 5;
        private int _vCount = 1;
        private int _v = -1;
        private float[] _vStart = new float[MAX_VOLLEYS];
        private float[] _vFly = new float[MAX_VOLLEYS];
        private float[] _vHit = new float[MAX_VOLLEYS];
        private float[] _vEnd = new float[MAX_VOLLEYS];
        private int[] _vDel = new int[MAX_VOLLEYS];
        private bool _vFlown;
        private bool _vStruck;
        // The volley in the air: how it travels, when it left, hits and ends, how heavy it is, which way it leans.
        private int _vd;
        private float _tRel, _tHit, _tRelEnd;
        private float _vSize = 1f;
        private float _vFlip = 1f;
        private bool _vFinal;

        // The waves before each delivery's finisher (index delivery * 4 + wave): lighter cousins that build up to it.
        //   1 lance  2 helix  3 volley  4 meteor  5 rain  6 runner  7 beam  8 orbit  9 zigzag  10 portal  11 wave  12 boomerang
        private int[] _preludes =
        {
            0, 0, 0, 0,
            3, 9, 2, 11,     // a lance:      a volley, a zigzag, a helix, a wave
            1, 3, 8, 11,     // a helix:      a lance, a volley, an orbit, a wave
            1, 11, 2, 5,     // a volley:     a lance, a wave, a helix, a rain
            3, 6, 2, 5,      // a meteor:     a volley, a runner, a helix, a rain
            1, 3, 11, 4,     // a rain:       a lance, a volley, a wave, a meteor
            9, 11, 3, 4,     // a runner:     a zigzag, a wave, a volley, a meteor
            1, 8, 2, 3,      // a beam:       a lance, an orbit, a helix, a volley
            3, 2, 11, 5,     // an orbit:     a volley, a helix, a wave, a rain
            1, 3, 6, 5,      // a zigzag:     a lance, a volley, a runner, a rain
            12, 3, 2, 4,     // portals:      a boomerang, a volley, a helix, a meteor
            6, 3, 1, 5,      // a wave:       a runner, a volley, a lance, a rain
            1, 2, 3, 11,     // a boomerang:  a lance, a helix, a volley, a wave
        };

        private Material _fadeMat, _runeMat, _sigilMat, _skyMat, _duskMat, _auraMat, _auraOuterMat;
        // The battle aura: whether it has caught, when it last swelled (its ignition, each beat), and its emitters' fractions.
        private bool _auraLit;
        private float _auraKick, _flameAcc, _streakAcc;
        // The display: when its next beat is due, when the last one was, and how far its circling sparks have turned.
        private float _showNext, _showKick, _showTurn;
        private float _auraAcc, _emberAcc, _spiralAcc, _trailAcc, _debrisAcc, _rimAcc, _vortexAcc, _skyAcc, _moteAcc;
        private float _runeAngle, _rimAngle;
        private float _nextBeat, _nextArc;
        // When the rune last flared, when power last crackled, and the last jolt (the release, a hit): flashes and shakes.
        private float _flareAt, _arcFlash, _kickAt;

        // Rings that spread from a point and go (the charge's heartbeat, the surge, each wave's hit), until the finisher's.
        private float[] _pulseAt = new float[3];
        private float[] _pulseSize = new float[3];
        private float[] _pulseLen = new float[3];
        private int _pulseNext;

        // Bolts: when each flies (from, to; seconds from the start), its path's control points, whether it has landed.
        private int _headCount;
        private float[] _hFrom = new float[MAX_HEADS];
        private float[] _hTo = new float[MAX_HEADS];
        private float[] _hLaunch = new float[MAX_HEADS];
        private Vector3[] _hA = new Vector3[MAX_HEADS];
        private Vector3[] _hB = new Vector3[MAX_HEADS];
        private Vector3[] _hC = new Vector3[MAX_HEADS];
        private Vector3[] _hD = new Vector3[MAX_HEADS];
        private bool[] _hLanded = new bool[MAX_HEADS];
        private Vector3 _headPos, _headDir;

        // A summoned creature: it arrives beside the caster, roars with each wave, and rides the finisher's first bolt.
        private Transform _carrier;
        private Animator _carrierAnim;
        private bool _carrierFlyer;
        private int _carrierCue;
        private Vector3 _carrierLast;

        private Vector3[] _spikeAt = new Vector3[8];
        private Quaternion[] _spikeRot = new Quaternion[8];
        private float[] _spikeH = new float[8];

        // The film's shot this frame.
        private Vector3 _camPos, _camLook;
        private float _camShake;

        // Staging all the spell's own (CardDatabase.SPECIAL_*; 0 = the formula's), and how far the wyrm's has got:
        // times it has been round the caster, targets it has gone through, its cues, and when it rests, roars and hovers.
        private int _special;
        private int _wyrmLaps, _wyrmPassed, _wyrmCue;
        private float _wyrmRest, _wyrmRoar, _wyrmHover;
        // Where it rests and where it hovers; the middle of whoever it burns: the way there, that turned a quarter (the
        // way it twirls), how far, and how far they spread to either side.
        private Vector3 _wyrmRestAt, _wyrmHoverAt, _wyrmToward, _wyrmSide;
        private float _wyrmDist, _wyrmSpread;

        public bool IsPlaying() { return _on; }

        /// <summary>True while the local player is the caster being shown (BattleClient doesn't pull them back to their seat).</summary>
        public bool HoldsPlayer() { return _on && _me; }

        public bool IsFor(BattleClient client) { return _on && _owner == client; }

        private void Start()
        {
            HideRig();
            if (fadeSphere != null) fadeSphere.gameObject.SetActive(false);
        }

        /// <summary>
        /// First half of starting an attack spell (then <see cref="Begin"/>): who is watching and what moves. 'me' = the
        /// local player is the (wizard) caster: fade, teleport and film from the release; 'watch' = the local player is
        /// in the fight: film from the wind-up. 'cinematic' = cast from the middle of the ring. 'seat' is the caster's
        /// seat, 'body' a monster caster's body (moved to the middle), 'creature' = the spell sends a creature along its
        /// path (it pulls up in front of the target), 'targetScale' the target's size.
        /// </summary>
        public void Prepare(BattleClient owner, bool me, bool watch, bool cinematic, Transform seat, Transform body, bool creature, float targetScale)
        {
            if (_on) Abort(true);
            _owner = owner;
            _cine = cinematic;
            _me = me && cinematic;
            _watch = watch && cinematic && !_me;
            _creature = creature;
            _tScale = Mathf.Clamp(targetScale, 0.5f, 3f);
            _targetCount = 0;
            _special = 0;
            _casterBody = cinematic ? body : null;
            _bodyMoved = false;
            if (seat != null)
            {
                _seatPos = seat.position;
                _seatRot = seat.rotation;
            }
        }

        /// <summary>
        /// Staging all the spell's own (after Prepare, before Begin): CardDatabase.CineSpecial. Only a cinematic cast
        /// plays it (and only if its rig is in the scene); anything else falls back on the spell's recipe.
        /// </summary>
        public void SetSpecial(int special)
        {
            _special = special;
        }

        /// <summary>An area spell's targets (after Prepare, before Begin): up to four seats it hits.</summary>
        public void SetTargets(Vector3[] pts, int n)
        {
            _targetCount = 0;
            if (pts == null) return;
            for (int i = 0; i < n && i < pts.Length && _targetCount < _targets.Length; i++) _targets[_targetCount++] = pts[i];
        }

        /// <summary>Where bolt i lands: the target's front, or for an area spell each of its targets in turn.</summary>
        private Vector3 HitFor(int i)
        {
            if (!_aoe || _targetCount == 0) return _hit;
            Vector3 t = _targets[i % _targetCount];
            Vector3 d = t - _from;
            d.y = 0f;
            Vector3 toward = d.sqrMagnitude > 0.001f ? d.normalized : _fwd;
            return t - toward * 0.35f + Vector3.up * 1.0f;
        }

        /// <summary>A school's look among 'len' (the seven schools, then General at 7, then Tech at 8 when there is one).</summary>
        private int LookOf(int school, int len)
        {
            if (len <= 0) return 0;
            if (school >= 0 && school < 7 && school < len) return school;
            if (school == CardDatabase.TECH && len > 8) return 8;
            return Mathf.Min(7, len - 1);
        }

        /// <summary>
        /// Starts the attack spell's staging (after <see cref="Prepare"/>): cast from 'from' (the middle of the ring for a
        /// cinematic, else the seat) facing 'fwd' at 'aim' (the target, or the middle of the side an area spell hits).
        /// 'style' is CardDatabase.CineStyle and 'beats' CardDatabase.CineBeats; 'body' the caster's size (big monsters cast
        /// from higher). Times: in (vanish and appear), gather (charging before the cast sound), cast (the cast sound's
        /// length: it ends on the release), unleash (the beats after the cast, shared out by CardDatabase.Plan*), expend
        /// (how long its blades, traps and shields take to go, between the display and the attack), out (back to the seat).
        /// </summary>
        public void Begin(Vector3 from, Vector3 fwd, Vector3 aim, int school, int style, int beats, int pips, bool aoe, float body, string caster, string spell,
                          float inSec, float gatherSec, float castSec, float unleashSec, float expendSec, float outSec)
        {
            _from = from;
            _ground = from.y;
            fwd.y = 0f;
            _fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : _seatRot * Vector3.forward;
            _right = Vector3.Cross(Vector3.up, _fwd).normalized;
            _aim = aim;
            _body = Mathf.Clamp(body, 0.5f, 3f);
            int looks = sparkMats != null && sparkMats.Length > 0 ? sparkMats.Length : 8;
            _look = LookOf(school, looks);
            _pips = Mathf.Clamp(pips, 0, 8);
            _vis = _cine ? Mathf.RoundToInt(_pips * (1f + 0.15f * Mathf.Max(0, _pips - 1))) : _pips;
            _aoe = aoe;
            _scale = (0.85f + 0.17f * _vis) * Mathf.Max(1f, _body * 0.8f);
            _caster = caster;
            _spell = spell;
            _color = glyphColors != null && _look < glyphColors.Length ? glyphColors[_look] : Color.white;
            _hand = _from + _fwd * (0.55f * _body) + Vector3.up * (1.35f * _body) + _right * (0.15f * _body);
            // Everything is laid out for the real distance from the middle of the ring to the target, and stops at it:
            // bolts meet the front of the target's body, a creature pulls up a stride short.
            Vector3 flat = _aim - _from;
            flat.y = 0f;
            _reach = Mathf.Clamp(flat.magnitude / 7f, 0.45f, 1.4f);
            float front = _aoe ? 0f : 0.35f * _tScale;
            _hit = _aim - _fwd * front + Vector3.up * (1.0f * (_aoe ? 1f : _tScale));
            _stop = _aim - _fwd * ((_aoe ? 1.6f : 1.3f) * _tScale);
            _impactAt = _aoe && _targetCount > 0 ? new Vector3(_targets[0].x, _aim.y, _targets[0].z) : _aim;

            _delivery = style & 15;
            if (_delivery == 0) _delivery = CardDatabase.CINE_LANCE;
            _impact = (style >> 4) & 15;
            _charge = (style >> 8) & 15;
            _cam = (style >> 12) & 15;
            _count = (style >> 16) & 15;
            _shape = (style >> 20) & 3;
            _flip = ((style >> 22) & 1) != 0 ? -1f : 1f;
            _height = (style >> 23) & 3;
            _arrive = beats & 15;
            _show = (beats >> 4) & 15;
            _leave = (beats >> 8) & 15;
            _friendly = (beats & CardDatabase.BEATS_FRIENDLY) != 0;

            // How much power the charge shows.
            _tier = !_cine ? 1 : _pips >= 7 ? 4 : _pips >= 5 ? 3 : _pips >= 3 ? 2 : 1;
            // The core hangs over the caster; a creature arrives beside them (straight ahead if a beam is to pour out of it).
            // (Higher the bigger it will be, so it never sits on their head.)
            float coreFull = (0.5f + 0.13f * _vis) * Mathf.Max(1f, _body * 0.8f);
            _corePos = _from + Vector3.up * (2.1f * _body + 0.2f + 0.55f * coreFull) + _fwd * (0.45f * _body);
            _summonAt = _delivery == CardDatabase.CINE_BEAM ? _from + _fwd * (1.7f * _body)
                                                             : _from + _fwd * (1.5f * _body) + _right * (1.0f * _flip * _body);
            _origin = _cine ? _corePos : _hand;

            _tHalf = inSec * 0.5f;
            _tCast = inSec;
            _tSound = inSec + Mathf.Max(0f, gatherSec);
            _tUnl = _tSound + Mathf.Max(0.1f, castSec);
            _tAfter = _tUnl + Mathf.Max(0.5f, unleashSec) + Mathf.Max(0f, expendSec);
            _tBack = _tAfter + outSec * 0.5f;
            _tEnd = _tAfter + outSec;
            PlanBeats(unleashSec, Mathf.Max(0f, expendSec));

            _t0 = Time.time;
            _cue = 0;
            _teleported = false;
            _returned = false;
            _filmOn = false;
            _auraAcc = 0f;
            _emberAcc = 0f;
            _spiralAcc = 0f;
            _trailAcc = 0f;
            _debrisAcc = 0f;
            _rimAcc = 0f;
            _vortexAcc = 0f;
            _skyAcc = 0f;
            _moteAcc = 0f;
            _arrAcc = 0f;
            _showAcc = 0f;
            _exitAcc = 0f;
            _afterDone = 0;
            _arrCue = 0;
            _showCue = 0;
            _showBeats = 0;
            _exitCue = 0;
            _runeAngle = 0f;
            _rimAngle = 0f;
            _nextBeat = _tCast + 0.5f;
            _nextArc = _tCast + 0.4f;
            _flareAt = -100f;
            _arcFlash = -100f;
            _kickAt = -100f;
            _auraLit = false;
            _auraKick = -100f;
            _flameAcc = 0f;
            _streakAcc = 0f;
            _showNext = 0f;
            _showKick = -100f;
            _showTurn = 0f;
            _pulseNext = 0;
            _headCount = 0;
            _carrier = null;
            _carrierAnim = null;
            _headPos = _origin;
            _headDir = _fwd;
            HideRig();
            Recolor();
            // Staging of its own: its rig lays its part out now (the times are the formula's).
            if (!_cine || (_special == CardDatabase.SPECIAL_WYRM && wyrm == null)) _special = 0;
            _wyrmLaps = 0;
            _wyrmPassed = 0;
            _wyrmCue = 0;
            if (_special == CardDatabase.SPECIAL_WYRM)
            {
                wyrm.Plan(_from, _fwd, _flip, _body, _aoe ? 1f : _tScale, _aim, _targets, _aoe ? _targetCount : 0);
                wyrm.Schedule(_tUnl, _tDisp, _tExp, _tBar, _tStrike, _tExit, _tAfter);
                _wyrmRest = wyrm.RestTime();
                _wyrmRoar = wyrm.RoarTime();
                _wyrmHover = wyrm.HoverTime();
                _wyrmRestAt = wyrm.Rest();
                _wyrmHoverAt = wyrm.Hover();
                _wyrmToward = wyrm.Toward();
                _wyrmSide = wyrm.Side();
                _wyrmDist = wyrm.Distance();
                _wyrmSpread = wyrm.Spread();
            }
            _on = true;
        }

        /// <summary>
        /// Lays the beats after the cast out by the staging formula (the same on every client): arrival, display, the
        /// expending, attack, result, exit. The attack is the spell's volleys: a wave first for a spell of 5 pips or more,
        /// then the finisher, whose slot is longer; each gathers for the first part of its slot and flies for the rest
        /// (no longer than its delivery's own flight), and the finisher's hit ends the attack. A cast with no staging (a
        /// minion's) is one volley, timed by its delivery.
        /// </summary>
        private void PlanBeats(float unleash, float expend)
        {
            int waves = 0;
            if (_cine && cardDatabase != null)
            {
                _tDisp = _tUnl + cardDatabase.PlanArrival(unleash);
                _tExp = _tDisp + cardDatabase.PlanDisplay(unleash);
                _tBar = _tExp + expend;
                _tStrike = _tBar + Mathf.Max(0.4f, cardDatabase.PlanAttack(unleash));
                _tExit = Mathf.Min(_tAfter - 0.05f, _tStrike + cardDatabase.PlanResult(unleash));
                waves = Mathf.Clamp(cardDatabase.PlanWaves(_pips), 0, MAX_VOLLEYS - 1);
            }
            else
            {
                _tDisp = _tUnl;
                _tExp = _tUnl;
                _tBar = _tUnl;
                _tStrike = Mathf.Min(_tAfter - 0.1f, _tUnl + DeliveryFlight(_delivery));
                _tExit = _tAfter;
            }
            float unit = (_tStrike - _tBar) / (waves + 1.6f);
            // Which of the delivery's four preludes comes first is the spell's own.
            int turn = (_count + _shape + _height + (_flip < 0f ? 1 : 0)) & 3;
            for (int k = 0; k < waves; k++)
            {
                float start = _tBar + k * unit;
                float end = start + unit;
                float hit = end - Mathf.Min(0.3f, unit * 0.2f);
                int d = Prelude(_delivery, k + turn);
                float flight = Mathf.Min(DeliveryFlight(d) * 0.8f, (hit - start) * 0.8f);
                _vStart[k] = start;
                _vFly[k] = hit - flight;
                _vHit[k] = hit;
                _vEnd[k] = end;
                _vDel[k] = d;
            }
            float fStart = _tBar + waves * unit;
            float fFlight = _cine ? Mathf.Min(DeliveryFlight(_delivery), (_tStrike - fStart) * 0.8f) : _tStrike - fStart;
            _vStart[waves] = fStart;
            _vFly[waves] = _tStrike - fFlight;
            _vHit[waves] = _tStrike;
            _vEnd[waves] = Mathf.Max(_tStrike + 0.2f, Mathf.Min(_tAfter - 0.05f, _tStrike + DeliveryTail(_delivery)));
            _vDel[waves] = _delivery;
            _vCount = waves + 1;
            _v = -1;
            _vFlown = false;
            _vStruck = false;
            _vFinal = false;
            _vd = _delivery;
            _vSize = 1f;
            _vFlip = _flip;
            // Nothing is in the air yet.
            _tRel = 1000000f;
            _tHit = 1000000f;
            _tRelEnd = 1000000f;
        }

        private int Prelude(int delivery, int wave)
        {
            int ix = delivery * 4 + (wave & 3);
            return ix >= 0 && ix < _preludes.Length && _preludes[ix] > 0 ? _preludes[ix] : CardDatabase.CINE_LANCE;
        }

        private float DeliveryFlight(int d) { return cardDatabase != null ? cardDatabase.DeliveryFlight(d) : 1.2f; }

        private float DeliveryTail(int d) { return cardDatabase != null ? cardDatabase.DeliveryTail(d) : 0.6f; }

        /// <summary>
        /// A summoned creature (the spell animation's 'Summon'): it arrives beside the caster as the power takes shape,
        /// roars with each wave, and rides the finisher's first bolt in place of a glowing one.
        /// </summary>
        public void Carry(Transform summon, Animator anim, bool flyer)
        {
            if (!_on || summon == null) return;
            _carrier = summon;
            _carrierAnim = anim;
            _carrierFlyer = flyer;
            _carrierCue = 0;
            _carrierLast = _summonAt;
            _carrier.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }

        /// <summary>Stops at once (the round moved on, the fight ended): the film closes, the view clears, the caster goes back.</summary>
        public void Abort(bool returnToSeat)
        {
            if (!_on) return;
            _on = false;
            if (_me)
            {
                if (_filmOn && cutscenePlayer != null) cutscenePlayer.StopFilm();
                SetFade(0f);
                VRCPlayerApi local = Networking.LocalPlayer;
                if (Utilities.IsValid(local)) local.Immobilize(false);
                if (returnToSeat && _teleported && !_returned)
                {
                    _retPos = _seatPos;
                    _retRot = _seatRot;
                    _returned = true;
                    SendCustomEventDelayedFrames(nameof(_ReturnNow), 1);
                }
            }
            if (_watch && _filmOn && cutscenePlayer != null) cutscenePlayer.StopFilm();
            _filmOn = false;
            RestoreBody();
            _carrier = null;
            HideRig();
        }

        /// <summary>A monster caster's body back in its seat.</summary>
        private void RestoreBody()
        {
            if (!_bodyMoved || _casterBody == null) { _bodyMoved = false; return; }
            _casterBody.localPosition = _bodyLocalPos;
            _casterBody.localRotation = _bodyLocalRot;
            _bodyMoved = false;
        }

        public void _ReturnNow()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            local.TeleportTo(_retPos, _retRot, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
        }

        // ------------------------------------------------------------------ beats

        private void Update()
        {
            if (!_on) return;
            float t = Time.time - _t0;
            Cues(t);
            if (!_on) return;
            float dt = Time.deltaTime;
            AnimateRune(t, dt);
            AnimateCharge(t, dt);
            AnimateAura(t, dt);
            AnimateStage(t);
            AnimatePulses(t);
            if (_special == CardDatabase.SPECIAL_WYRM) AnimateWyrm(t, dt);
            else
            {
                AnimateArrival(t, dt);
                AnimateDisplay(t, dt);
                AnimateCore(t, dt);
                AnimateHeads(t, dt);
                AnimateCarrier(t);
                AnimateBeam(t);
                AnimatePortals(t);
            }
            AnimateExit(t, dt);
            AnimateImpact(t, dt);
            AnimateLight(t);
            if (_me || _watch) Film(t);
        }

        public override void PostLateUpdate()
        {
            if (!_on || !_me) return;
            float t = Time.time - _t0;
            float f = 0f;
            if (t < _tHalf) f = t / Mathf.Max(0.01f, _tHalf);
            else if (t < _tCast) f = 1f - (t - _tHalf) / Mathf.Max(0.01f, _tCast - _tHalf);
            else if (t >= _tAfter && t < _tBack) f = (t - _tAfter) / Mathf.Max(0.01f, _tBack - _tAfter);
            else if (t >= _tBack) f = 1f - (t - _tBack) / Mathf.Max(0.01f, _tEnd - _tBack);
            SetFade(Mathf.Clamp01(f));
            if (fadeSphere != null && fadeSphere.gameObject.activeSelf)
            {
                VRCPlayerApi local = Networking.LocalPlayer;
                if (Utilities.IsValid(local))
                {
                    VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                    fadeSphere.SetPositionAndRotation(head.position, head.rotation);
                }
            }
        }

        private void Cues(float t)
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (_cue == 0)
            {
                _cue = 1;
                if (_cine)
                {
                    Burst(portalBurst, _seatPos + Vector3.up * 1f, 50);
                    Sound(whooshSound, _seatPos + Vector3.up);
                    if (_me && Utilities.IsValid(local)) local.Immobilize(true);
                }
            }
            if (_cue == 1 && t >= _tHalf)
            {
                _cue = 2;
                if (_cine)
                {
                    // At the darkest moment the caster is taken to the middle of the ring, facing their target.
                    if (_me && Utilities.IsValid(local))
                    {
                        local.TeleportTo(_from, Quaternion.LookRotation(_fwd, Vector3.up), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
                        _teleported = true;
                    }
                    // A monster's body steps into the middle of the ring too.
                    if (_casterBody != null && !_bodyMoved)
                    {
                        _bodyLocalPos = _casterBody.localPosition;
                        _bodyLocalRot = _casterBody.localRotation;
                        _casterBody.SetPositionAndRotation(_from, Quaternion.LookRotation(_fwd, Vector3.up));
                        _bodyMoved = true;
                    }
                    Burst(portalBurst, _from + Vector3.up * 1f, 60);
                }
            }
            if (_cue == 2 && t >= _tCast)
            {
                // The charge begins.
                _cue = 3;
                if (runeCircle != null)
                {
                    runeCircle.position = _from + Vector3.up * 0.06f;
                    runeCircle.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    runeCircle.gameObject.SetActive(true);
                }
                if (fxLight != null) { fxLight.color = _color; fxLight.intensity = 0f; fxLight.range = 6f * _scale; fxLight.gameObject.SetActive(true); }
                StartHum();
                // Everyone else in the fight watches the whole cast on the film screen.
                if (_watch && cutscenePlayer != null && !cutscenePlayer.IsPlaying())
                {
                    cutscenePlayer.PlayFilm(_caster, _spell + "!", Color.Lerp(_color, Color.white, 0.35f));
                    _filmOn = cutscenePlayer.IsFilming();
                    if (_filmOn) Film(t);
                }
            }
            if (_cue == 3 && t >= _tUnl)
            {
                _cue = 4;
                Unleash(t);
            }
            if (_cue == 4)
            {
                // (A spell with staging of its own has no volleys: its rig is the attack. Its hit still lands here.)
                if (_special == 0) StepVolleys(t);
                if (t >= _tStrike)
                {
                    _cue = 5;
                    _kickAt = t;
                    Strike();
                }
            }
            if (_cue == 5 && t >= _tAfter)
            {
                _cue = 6;
                if (_filmOn && cutscenePlayer != null) cutscenePlayer.StopFilm();
                _filmOn = false;
                if (!_cine) { Finish(); return; }
            }
            if (_cue == 6 && t >= _tBack)
            {
                _cue = 7;
                Burst(portalBurst, _from + Vector3.up * 1f, 50);
                Burst(portalBurst, _seatPos + Vector3.up * 1f, 50);
                Sound(whooshSound, _seatPos + Vector3.up);
                if (_me && _teleported && !_returned && Utilities.IsValid(local))
                {
                    _returned = true;
                    local.TeleportTo(_seatPos, _seatRot, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
                }
                RestoreBody();
            }
            if (_cue == 7 && t >= _tEnd) Finish();
        }

        private void Finish()
        {
            _cue = 8;
            if (_me)
            {
                VRCPlayerApi local = Networking.LocalPlayer;
                if (Utilities.IsValid(local)) local.Immobilize(false);
            }
            SetFade(0f);
            if (_filmOn && cutscenePlayer != null) cutscenePlayer.StopFilm();
            _filmOn = false;
            RestoreBody();
            _on = false;
            _carrier = null;
            HideRig();
        }

        // ------------------------------------------------------------------ the unleash: surge, form, waves, finisher

        /// <summary>The release. The charge goes off round the caster (the surge); from here the caster watches the film too.</summary>
        private void Unleash(float t)
        {
            if (chargeAudio != null && chargeAudio.isPlaying) chargeAudio.Stop();
            _flareAt = t;
            if (_runeMat != null) _runeMat.SetFloat("_Flare", 2.5f);
            if (_cine)
            {
                _kickAt = t;
                Vector3 chest = _from + Vector3.up * (1.2f * _body);
                // A ring of sparks races out along the floor, a ring of light after it, and a burst off the caster.
                Shock(_from + Vector3.up * 0.12f, 40 + 26 * _tier + 6 * _vis, 0.7f + 0.3f * _tier);
                if (_pips >= 6) Shock(_from + Vector3.up * 0.12f, 30 + 5 * _vis, 1.2f + 0.3f * _tier);
                Pulse(_from, (4.5f + 1.6f * _tier) * Mathf.Max(1f, _body * 0.8f), 0.75f);
                Burst(portalBurst, chest, 25 + 20 * _tier);
                if (_tier >= 2 && embers != null)
                {
                    embers.transform.position = _from + Vector3.up * 0.2f;
                    embers.transform.localScale = Vector3.one * _scale;
                    embers.Emit(16 * _tier);
                }
                if (_tier >= 3 && arcs != null)
                {
                    arcs.transform.position = chest;
                    arcs.transform.localScale = Vector3.one * _scale;
                    arcs.Emit(10 + 6 * _tier);
                }
                Sound(_tier >= 2 && surgeSound != null ? surgeSound : releaseSound, chest);
            }
            if (_me && !_filmOn && cutscenePlayer != null && !cutscenePlayer.IsPlaying())
            {
                cutscenePlayer.PlayFilm(_caster, _spell + "!", Color.Lerp(_color, Color.white, 0.35f));
                _filmOn = cutscenePlayer.IsFilming();
                if (_filmOn) Film(t);
            }
        }

        /// <summary>
        /// The volleys take their turns: each one's slot opens (the core gathers and the film sets up, while the last of
        /// the volley before it settles), then it flies, then it hits.
        /// </summary>
        private void StepVolleys(float t)
        {
            if (t < _tBar) return;
            if (_v + 1 < _vCount && t >= _vStart[_v + 1])
            {
                _v++;
                _vFlown = false;
                _vStruck = false;
            }
            if (_v < 0) return;
            if (!_vFlown && t >= _vFly[_v])
            {
                _vFlown = true;
                FlyVolley(_v, t);
            }
            // A wave's hit (the finisher's is the spell's: Strike).
            if (_vFlown && !_vStruck && _v < _vCount - 1 && t >= _vHit[_v])
            {
                _vStruck = true;
                MiniStrike(t);
            }
        }

        /// <summary>
        /// A volley leaves: whatever is left of the one before it is cleared away, this one's delivery, lean and weight
        /// are set, sparks fly off where it leaves from, and its bolts set off on their paths.
        /// </summary>
        private void FlyVolley(int k, float t)
        {
            HideAll(heads);
            _headCount = 0;
            if (beam != null) beam.gameObject.SetActive(false);
            HideAll(portals);
            _vFinal = k == _vCount - 1;
            _vd = _vDel[k];
            // Waves lean left and right in turn; the finisher leans the spell's own way.
            _vFlip = _vFinal ? _flip : ((k & 1) == 0 ? -_flip : _flip);
            _tRel = _vFly[k];
            _tHit = _vHit[k];
            _tRelEnd = _vEnd[k];
            // Waves are lighter than the finisher, each a little heavier than the one before.
            int waves = _vCount - 1;
            _vSize = _vFinal ? 1f : 0.55f + 0.3f * (waves > 1 ? k / (float)(waves - 1) : 0.5f);
            _origin = Origin();
            _flareAt = t;
            Vector3 dir = _hit - _origin;
            if (releaseBurst != null)
            {
                releaseBurst.transform.position = _origin;
                if (dir.sqrMagnitude > 0.001f) releaseBurst.transform.rotation = Quaternion.LookRotation(dir);
                releaseBurst.transform.localScale = Vector3.one * (_scale * (0.5f + 0.5f * _vSize));
                releaseBurst.Emit(_vFinal ? 25 + 12 * _vis : 10 + 4 * _vis);
            }
            SoundAt(releaseSound, _origin, _vFinal ? 1f : 0.55f + 0.25f * _vSize);
            _headPos = _origin;
            _headDir = dir.sqrMagnitude > 0.001f ? dir.normalized : _fwd;
            PlotHeads();
            if (_vd == CardDatabase.CINE_PORTAL && portals != null)
                for (int i = 0; i < portals.Length && i < 2; i++) if (portals[i] != null) { portals[i].localScale = new Vector3(0.01f, 0.01f, 0.01f); portals[i].gameObject.SetActive(true); }
            if (_vd == CardDatabase.CINE_RAIN && portals != null && portals.Length > 1 && portals[1] != null)
            {
                // The rain falls out of a ring opened high over the target.
                portals[1].position = _aim + Vector3.up * (8.5f + _height);
                portals[1].rotation = Quaternion.identity;
                portals[1].localScale = new Vector3(0.01f, 0.01f, 0.01f);
                portals[1].gameObject.SetActive(true);
            }
            if (_vd == CardDatabase.CINE_BEAM && beam != null)
            {
                beam.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                beam.gameObject.SetActive(true);
            }
            if (_cine)
            {
                // The finisher goes with a jolt; anything driven into the floor throws up a ring of sparks.
                if (_vFinal) _kickAt = t;
                if (_vd == CardDatabase.CINE_GROUND || _vd == CardDatabase.CINE_WAVE)
                    Shock(_from + _fwd * (0.8f * _body) + Vector3.up * 0.12f, _vFinal ? 30 + 8 * _vis : 14, 0.5f + 0.4f * _vSize);
                // A creature roars as each wave goes.
                if (!_vFinal && _carrier != null && _carrierAnim != null) _carrierAnim.SetTrigger("Cast");
            }
        }

        /// <summary>Where the volleys leave from: the core over the caster, a summoned creature, or (no staging) the caster's hand.</summary>
        private Vector3 Origin()
        {
            if (!_cine) return _hand;
            if (_creature) return _summonAt + Vector3.up * (1.2f * Mathf.Max(1f, _body * 0.8f));
            return _corePos;
        }

        /// <summary>A wave lands (no damage yet): sparks on each target it reached, a ring of light under it, and they flinch.</summary>
        private void MiniStrike(float t)
        {
            _kickAt = t;
            bool many = _aoe && _targetCount > 0;
            int n = many ? _targetCount : 1;
            float big = ImpactSize() * (0.45f + 0.35f * _vSize);
            for (int b = 0; b < n; b++)
            {
                Vector3 at = many ? HitFor(b) : _hit;
                if (impactBurst != null)
                {
                    impactBurst.transform.position = at;
                    impactBurst.transform.localScale = Vector3.one * big;
                    impactBurst.Emit(14 + 6 * _vis);
                }
                // Every other wave breaks into shards as it lands.
                if ((_v & 1) == 1 && shardBurst != null)
                {
                    shardBurst.transform.position = at;
                    shardBurst.transform.localScale = Vector3.one * big;
                    shardBurst.Emit(8 + 3 * _vis);
                }
                Pulse(new Vector3(at.x, _aim.y, at.z), (2.2f + 0.25f * _vis) * _tScale * (0.6f + 0.4f * _vSize), 0.5f);
            }
            Vector3 first = many ? HitFor(0) : _hit;
            if (_tier >= 2) Shock(new Vector3(first.x, _aim.y + 0.12f, first.z), 14 + 5 * _vis, 0.35f + 0.25f * _vSize);
            SoundAt(_friendly ? whooshSound : impactSound, first, _friendly ? 0.4f : 0.45f + 0.3f * _vSize);
            if (_owner != null && !_friendly) _owner.CineFlinch();
        }

        /// <summary>A ring of light spreading from a point on the floor ('size' across at its widest, gone after 'len' seconds).</summary>
        private void Pulse(Vector3 at, float size, float len)
        {
            if (shockRings == null || shockRings.Length == 0) return;
            int i = _pulseNext % Mathf.Min(3, shockRings.Length);
            _pulseNext++;
            Transform ring = shockRings[i];
            if (ring == null) return;
            _pulseAt[i] = Time.time - _t0;
            _pulseSize[i] = size;
            _pulseLen[i] = Mathf.Max(0.15f, len);
            ring.position = at + Vector3.up * (0.07f + 0.02f * i);
            ring.rotation = Quaternion.identity;
            ring.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            ring.gameObject.SetActive(true);
        }

        private void AnimatePulses(float t)
        {
            // From the finisher's hit on, the rings are its impact's (AnimateImpact).
            if (shockRings == null || t >= _tStrike) return;
            for (int i = 0; i < shockRings.Length && i < 3; i++)
            {
                Transform ring = shockRings[i];
                if (ring == null || !ring.gameObject.activeSelf) continue;
                float k = (t - _pulseAt[i]) / _pulseLen[i];
                if (k >= 1f) { ring.gameObject.SetActive(false); continue; }
                float sz = Mathf.Lerp(0.4f, _pulseSize[i], 1f - (1f - k) * (1f - k));
                ring.localScale = new Vector3(sz, Mathf.Max(0.01f, 1f - k), sz);
            }
        }

        /// <summary>A ring of sparks racing out along the floor from a point.</summary>
        private void Shock(Vector3 at, int count, float scale)
        {
            if (shockwave == null || count <= 0) return;
            shockwave.transform.position = at;
            shockwave.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
            shockwave.Emit(count);
        }

        // ------------------------------------------------------------------ arrival, display, exit: each spell's own

        /// <summary>Where the spell's subject belongs: the core's place over the caster, or the spot its creature stands on.</summary>
        private Vector3 SubjectHome()
        {
            return _creature ? _summonAt : _corePos;
        }

        /// <summary>
        /// Where the subject is 'k' of the way through its arrival, the spell's own way: up out of the rune, down out of
        /// the sky, out through a gate behind it; or (drawn in from all round, wound up out of sparks) where it belongs.
        /// </summary>
        private Vector3 ArrivePos(Vector3 home, float k)
        {
            float e = 1f - (1f - k) * (1f - k);
            if (_arrive == CardDatabase.ARRIVE_ERUPT) return new Vector3(home.x, Mathf.Lerp(_ground - (_creature ? 1.6f * _body : 0.3f), home.y, e), home.z);
            if (_arrive == CardDatabase.ARRIVE_DESCEND) return home + Vector3.up * ((1f - k) * (1f - k) * (_creature ? 7f : 6f));
            if (_arrive == CardDatabase.ARRIVE_RIFT) return home - _fwd * ((1f - e) * 1.4f);
            return home;
        }

        /// <summary>
        /// The arrival (from the release to the display): the particles of the spell's own way of arriving, round its
        /// subject (the core, or its creature). ERUPT: a geyser of sparks out of the rune. DESCEND: streaks of light
        /// pouring down ahead of it, a burst as it lands. CONVERGE: sparks streaking in from all round, knotting together.
        /// RIFT: a gate opening behind it, sparks trailing off it as it comes through. KINDLE: sparks winding up from the
        /// floor in a spiral that tightens on it.
        /// </summary>
        private void AnimateArrival(float t, float dt)
        {
            if (!_cine || t < _tUnl) return;
            if (t >= _tDisp)
            {
                // The gate shuts behind it.
                if (_arrive == CardDatabase.ARRIVE_RIFT && _arrCue < 2)
                {
                    _arrCue = 2;
                    if (portals != null && portals.Length > 0 && portals[0] != null) portals[0].gameObject.SetActive(false);
                }
                return;
            }
            float k = Mathf.Clamp01((t - _tUnl) / Mathf.Max(0.05f, _tDisp - _tUnl));
            Vector3 home = SubjectHome();
            Vector3 mid = _creature ? Vector3.up * (1.0f * _body) : Vector3.zero;
            Vector3 at = ArrivePos(home, k) + mid;
            Vector3 floor = new Vector3(home.x, _ground + 0.12f, home.z);
            int n;
            if (_arrive == CardDatabase.ARRIVE_ERUPT)
            {
                if (_arrCue == 0) { _arrCue = 1; Shock(floor, 24 + 6 * _vis, 0.5f); }
                if (embers != null)
                {
                    embers.transform.position = floor;
                    embers.transform.localScale = Vector3.one * (_scale * 0.7f);
                    _arrAcc += dt * (70f + 22f * _vis);
                    n = (int)_arrAcc;
                    if (n > 0) { embers.Emit(n); _arrAcc -= n; }
                }
            }
            else if (_arrive == CardDatabase.ARRIVE_DESCEND)
            {
                if (skyfall != null)
                {
                    skyfall.transform.position = home + mid + Vector3.up * 7f;
                    skyfall.transform.localScale = Vector3.one * (0.35f * _scale);
                    _arrAcc += dt * (80f + 22f * _vis);
                    n = (int)_arrAcc;
                    if (n > 0) { skyfall.Emit(n); _arrAcc -= n; }
                }
                if (_arrCue == 0 && k >= 0.85f)
                {
                    _arrCue = 1;
                    Burst(portalBurst, at, 30 + 8 * _vis);
                    if (_creature) Shock(floor, 26, 0.5f);
                }
            }
            else if (_arrive == CardDatabase.ARRIVE_CONVERGE)
            {
                if (coreMotes != null)
                {
                    coreMotes.transform.position = at;
                    coreMotes.transform.localScale = Vector3.one * (1.6f + 0.2f * _vis);
                    _arrAcc += dt * (100f + 28f * _vis);
                    n = (int)_arrAcc;
                    if (n > 0) { coreMotes.Emit(n); _arrAcc -= n; }
                }
                if (_arrCue == 0 && k >= 0.9f) { _arrCue = 1; Burst(portalBurst, at, 24 + 6 * _vis); }
            }
            else if (_arrive == CardDatabase.ARRIVE_RIFT)
            {
                Vector3 gate = home + mid - _fwd * 1.4f;
                if (portals != null && portals.Length > 0 && portals[0] != null)
                {
                    Transform g = portals[0];
                    float open = Mathf.Min(Ease(k / 0.3f), 1f - Ease((k - 0.75f) / 0.25f));
                    float gs = (1.4f + 0.12f * _vis) * (_creature ? Mathf.Max(1f, _body) * 1.5f : 1f) * open;
                    g.SetPositionAndRotation(gate, Quaternion.FromToRotation(Vector3.up, _fwd));
                    g.localScale = new Vector3(Mathf.Max(0.01f, gs), 1f, Mathf.Max(0.01f, gs));
                    if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
                }
                if (_arrCue == 0) { _arrCue = 1; Burst(portalBurst, gate, 20 + 5 * _vis); }
                _arrAcc += dt * (50f + 14f * _vis);
                n = (int)_arrAcc;
                if (n > 0) { Burst(rimSparks, at, n); _arrAcc -= n; }
            }
            else
            {
                // KINDLE
                if (vortex != null)
                {
                    vortex.transform.position = floor;
                    vortex.transform.localScale = Vector3.one * (0.6f * _scale);
                    _arrAcc += dt * (80f + 22f * _vis);
                    n = (int)_arrAcc;
                    if (n > 0) { vortex.Emit(n); _arrAcc -= n; }
                }
                _showAcc += dt * 60f;
                while (_showAcc >= 1f)
                {
                    _showAcc -= 1f;
                    for (int w = 0; w < 2; w++)
                    {
                        float ang = (t * 720f * _flip + w * 180f) * Mathf.Deg2Rad;
                        float r = (1f - k) * 1.2f + 0.15f;
                        Vector3 p = new Vector3(home.x, Mathf.Lerp(_ground + 0.1f, at.y, k), home.z) + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * r;
                        Burst(rimSparks, p, 1);
                    }
                }
                if (_arrCue == 0 && k >= 0.9f) { _arrCue = 1; Burst(portalBurst, at, 20 + 6 * _vis); }
            }
        }

        /// <summary>
        /// The display (after the arrival; what it leaves stays up while the blades, traps and shields go, until the
        /// attack): the long beat, where the spell shows off round its subject (CardDatabase.cineDisplaySeconds, 5 s).
        /// It runs in three parts whatever the spell. It opens (a creature rears, the hum starts to climb again). It
        /// builds: beats come slowly at first and then quicker and quicker (about ten in all), each one swelling the
        /// spell's subject and doing the spell's own thing. And near the end it peaks: a ring and a spray of sparks
        /// from the subject, a boom, a flash, and the caster's aura jumps. The spell's own thing:
        ///   FLARE   every beat throws a ring of sparks and a ring of light, bigger each time;
        ///   HALO    rings spin round it (AnimateCharge's) with sparks circling it, faster and in more strands as it
        ///           builds, their circle breathing in and out; every beat a ring of light spreads from it;
        ///   CROWN   a crown of bolts fans out behind the caster over the first third and hangs there swaying; every
        ///           beat the next bolt along flares, and at the peak they all do;
        ///   STORM   it crackles, thinly at first and then without a pause, sparks jumping from the floor further and
        ///           further out; every beat a bolt strikes round it;
        ///   ASCEND  a column of light through it (AnimateCharge's), sparks spiralling up ever thicker, and every beat
        ///           a ring of light climbing the column.
        /// </summary>
        private void AnimateDisplay(float t, float dt)
        {
            if (!_cine || t < _tDisp || t >= _tBar) return;
            bool showing = t < _tExp;
            float k = Mathf.Clamp01((t - _tDisp) / Mathf.Max(0.05f, _tExp - _tDisp));
            Vector3 at = Focus();
            if (_showCue == 0)
            {
                _showCue = 1;
                _showNext = t + 0.15f;
                if (_carrier != null && _carrierAnim != null) _carrierAnim.SetTrigger("Cast");
                ShowHum(at);
            }
            // Its beats, and its one peak.
            bool beat = false, peak = false;
            if (showing && t >= _showNext)
            {
                _showNext = t + Mathf.Lerp(0.85f, 0.26f, k);
                _showBeats++;
                _showKick = t;
                beat = true;
            }
            if (showing && _showCue == 1 && k >= 0.86f)
            {
                _showCue = 2;
                _showKick = t;
                _kickAt = t;
                _arcFlash = t;
                _auraKick = t;
                peak = true;
                Pulse(at, (3f + 0.6f * _tier) * _scale, 0.7f);
                Shock(at, 30 + 10 * _vis, 0.8f + 0.2f * _tier);
                Burst(portalBurst, at, 20 + 6 * _vis);
                SoundAt(_tier >= 2 && surgeSound != null ? surgeSound : releaseSound, at, 0.65f);
            }
            // How far the beats have built (they stop growing after eight).
            float rise = Mathf.Min(8, _showBeats);
            int n;
            if (_show == CardDatabase.SHOW_FLARE)
            {
                if (beat)
                {
                    _kickAt = t;
                    Shock(at, 14 + 5 * _vis, 0.3f + 0.06f * rise);
                    Pulse(at, (1.4f + 0.2f * rise) * _scale, 0.5f);
                    Burst(portalBurst, at, 8 + 3 * _vis);
                    SoundAt(releaseSound, at, Mathf.Min(0.5f, 0.18f + 0.04f * rise));
                }
            }
            else if (_show == CardDatabase.SHOW_HALO)
            {
                int arms = k < 0.4f ? 2 : 3;
                _showTurn += dt * 420f * (1f + 1.2f * k) * _flip;
                _showAcc += dt * (40f + 10f * _vis) * (showing ? 0.7f + 0.8f * k : 0.4f);
                n = (int)_showAcc;
                _showAcc -= n;
                for (int w = 0; w < n; w++)
                {
                    float ang = (_showTurn + w * (360f / arms)) * Mathf.Deg2Rad;
                    float r = (0.9f + 0.08f * _vis) * Mathf.Max(1f, _body * 0.8f) * (1f + 0.22f * Mathf.Sin(t * 3.1f));
                    Burst(rimSparks, at + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * r + Vector3.up * (0.25f * Mathf.Sin(ang * 2f + t)), 1);
                }
                if (beat) Pulse(at, (1.6f + 0.2f * rise) * _scale, 0.55f);
            }
            else if (_show == CardDatabase.SHOW_CROWN)
            {
                if (heads == null) return;
                int crown = Mathf.Min(5, heads.Length);
                int up = showing ? Mathf.Min(crown, 1 + (int)(k / 0.34f * crown)) : crown;
                float sway = Mathf.Sin(t * 1.3f) * 7f;
                float throb = 0.35f * Mathf.Exp(-Mathf.Max(0f, t - _showKick) * 6f);
                for (int i = 0; i < up; i++)
                {
                    Transform h = heads[i];
                    if (h == null) continue;
                    float a = ((i - (crown - 1) * 0.5f) * 26f + sway) * Mathf.Deg2Rad;
                    Vector3 off = _right * (Mathf.Sin(a) * 1.7f * _body) + Vector3.up * (Mathf.Cos(a) * 1.1f * _body);
                    Vector3 p = _from + Vector3.up * (1.7f * _body) - _fwd * (0.5f * _body) + off + Vector3.up * (0.08f * Mathf.Sin(t * 4f + i));
                    if (!h.gameObject.activeSelf)
                    {
                        h.position = p;
                        ShowShape(i, _shape);
                        h.gameObject.SetActive(true);
                        Burst(portalBurst, p, 6 + _vis);
                    }
                    // The bolt whose beat it is swells and flares; at the peak they all do.
                    bool mine = peak || i == _showBeats % up;
                    float hs = (0.5f + 0.05f * _vis) * (1f + (mine ? throb : 0f));
                    h.position = p;
                    h.rotation = Quaternion.LookRotation(off.normalized + _fwd * 0.2f);
                    h.localScale = new Vector3(hs, hs, hs);
                    if (mine && (beat || peak)) Burst(portalBurst, p, 5 + _vis);
                }
                if (beat) Shock(_from + Vector3.up * 0.12f, 6 + 2 * _vis, 0.3f + 0.04f * rise);
            }
            else if (_show == CardDatabase.SHOW_STORM)
            {
                if (showing && t >= _nextArc)
                {
                    _nextArc = t + Mathf.Lerp(0.22f, 0.06f, k);
                    _arcFlash = t;
                    if (arcs != null)
                    {
                        arcs.transform.position = at;
                        arcs.transform.localScale = Vector3.one * _scale;
                        arcs.Emit(5 + _tier);
                    }
                    float ang = Random.value * 6.283f;
                    float r = 1.2f + Random.value * (1.2f + 2.2f * k);
                    Burst(impactBurst, _from + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * r + Vector3.up * 0.1f, 4);
                }
                if (beat)
                {
                    _kickAt = t;
                    if (arcs != null)
                    {
                        arcs.transform.position = at;
                        arcs.transform.localScale = Vector3.one * (_scale * 1.5f);
                        arcs.Emit(12 + 3 * _tier);
                    }
                    for (int b = 0; b < 2 + _tier; b++)
                    {
                        float strikeAng = Random.value * 6.283f;
                        float strikeR = 1.5f + Random.value * 2.5f;
                        Burst(impactBurst, _from + (_right * Mathf.Cos(strikeAng) + _fwd * Mathf.Sin(strikeAng)) * strikeR + Vector3.up * 0.1f, 6 + _vis);
                    }
                    SoundAt(impactSound, at, Mathf.Min(0.4f, 0.15f + 0.03f * rise));
                }
            }
            else
            {
                // ASCEND
                if (showing && vortex != null)
                {
                    vortex.transform.position = new Vector3(at.x, _ground + 0.1f, at.z);
                    vortex.transform.localScale = Vector3.one * (0.55f * _scale);
                    _showAcc += dt * (70f + 20f * _vis) * (0.5f + 1.1f * k);
                    n = (int)_showAcc;
                    if (n > 0) { vortex.Emit(n); _showAcc -= n; }
                }
                if (beat)
                {
                    Vector3 ring = new Vector3(at.x, _ground + 0.3f + 0.9f * (_showBeats % 5), at.z);
                    Pulse(ring, (1.2f + 0.15f * rise) * _scale, 0.5f);
                    Burst(portalBurst, ring, 6 + 2 * _vis);
                }
            }
        }

        /// <summary>The hum climbs again under the display, timed to top out as it ends.</summary>
        private void ShowHum(Vector3 at)
        {
            if (chargeAudio == null || chargeSound == null) return;
            float len = chargeSound.length;
            float show = _tExp - _tDisp;
            chargeAudio.Stop();
            chargeAudio.transform.position = at;
            chargeAudio.clip = chargeSound;
            chargeAudio.volume = 0.16f + 0.08f * _tier;
            chargeAudio.Play();
            if (len > show + 0.05f) chargeAudio.time = len - show;
        }

        /// <summary>
        /// The exit (after the result): the spell's own way of leaving. FADE: embers drift up off the caster as the rune
        /// closes. SCATTER: what's left is blown apart, a ring of sparks from the caster and one from the hit. ASCEND: a
        /// column of sparks winds up into the sky from where it hit. SINK: sparks all round the rune's rim as it shuts
        /// (AnimateRune closes it early). ECHO: a ring of sparks from the hit, then one from the caster.
        /// </summary>
        private void AnimateExit(float t, float dt)
        {
            if (!_cine || t < _tExit || t >= _tAfter) return;
            float k = Mathf.Clamp01((t - _tExit) / Mathf.Max(0.05f, _tAfter - _tExit));
            Vector3 hitFloor = _impactAt + Vector3.up * 0.12f;
            Vector3 castFloor = _from + Vector3.up * 0.12f;
            int n;
            if (_exitCue == 0)
            {
                _exitCue = 1;
                if (_leave == CardDatabase.EXIT_SCATTER)
                {
                    Shock(castFloor, 30 + 6 * _vis, 0.6f);
                    Shock(hitFloor, 30 + 6 * _vis, 0.6f);
                    Burst(portalBurst, _impactAt + Vector3.up, 30);
                }
                else if (_leave == CardDatabase.EXIT_ECHO) Shock(hitFloor, 26 + 5 * _vis, 0.5f);
                else if (_leave == CardDatabase.EXIT_SINK)
                {
                    for (int w = 0; w < 14; w++)
                    {
                        float ang = w * 25.7f * Mathf.Deg2Rad;
                        Burst(rimSparks, castFloor + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * (1.2f * _scale), 2);
                    }
                }
            }
            if (_leave == CardDatabase.EXIT_ECHO && _exitCue == 1 && k >= 0.45f)
            {
                _exitCue = 2;
                Shock(castFloor, 26 + 5 * _vis, 0.5f);
            }
            if (_leave == CardDatabase.EXIT_ASCEND && vortex != null)
            {
                vortex.transform.position = hitFloor;
                vortex.transform.localScale = Vector3.one * (0.5f * _scale);
                _exitAcc += dt * (70f + 18f * _vis) * (1f - k);
                n = (int)_exitAcc;
                if (n > 0) { vortex.Emit(n); _exitAcc -= n; }
            }
            if (_leave == CardDatabase.EXIT_FADE && embers != null)
            {
                embers.transform.position = castFloor;
                embers.transform.localScale = Vector3.one * (_scale * 0.8f);
                _exitAcc += dt * (26f + 8f * _vis) * (1f - k);
                n = (int)_exitAcc;
                if (n > 0) { embers.Emit(n); _exitAcc -= n; }
            }
        }

        /// <summary>
        /// The gathered power over the caster (the subject of a spell with no creature). It arrives the spell's own way
        /// (ArrivePos), grows through the display, holds while the blades, traps and shields go, and is shed from in the
        /// attack: a wave knocks it back; then it swells hard and shudders before the finisher and goes into it: thrown,
        /// driven into the floor (a runner, a wave), flung up into the sky (a rain), or held as the mouth of a beam.
        /// </summary>
        private void AnimateCore(float t, float dt)
        {
            if (powerCore == null) return;
            int last = _vCount - 1;
            bool beamCore = _delivery == CardDatabase.CINE_BEAM;
            float gone = beamCore ? _vEnd[last] : _vFly[last] + 0.16f;
            bool show = _cine && !_creature && t >= _tUnl && t < gone;
            if (powerCore.gameObject.activeSelf != show) powerCore.gameObject.SetActive(show);
            if (!show) return;
            float full = (0.5f + 0.13f * _vis) * Mathf.Max(1f, _body * 0.8f);
            float fStart = _vStart[last];
            // Over half by the end of its arrival, nearly full by the end of its display, full as the finisher gathers.
            float arr = Mathf.Clamp01((t - _tUnl) / Mathf.Max(0.05f, _tDisp - _tUnl));
            float grow = 0.6f * Ease(arr);
            if (t > _tDisp) grow = Mathf.Lerp(0.6f, 0.9f, Ease((t - _tDisp) / Mathf.Max(0.05f, _tExp - _tDisp)));
            if (t > _tBar) grow = Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((t - _tBar) / Mathf.Max(0.2f, fStart - _tBar)));
            // A wave shed from it knocks it back; it recovers.
            float knock = 1f;
            if (_v >= 0 && _v < last && _vFlown) knock = 1f - 0.28f * Mathf.Exp(-(t - _vFly[_v]) * 5f);
            float swell = 1f, fade = 1f;
            Vector3 pos = ArrivePos(_corePos, arr);
            // Every beat of the display swells it (a FLARE's most); an ASCEND lifts it and sets it down again.
            if (t >= _tDisp && t < _tExp)
            {
                float sk = (t - _tDisp) / Mathf.Max(0.05f, _tExp - _tDisp);
                swell = 1f + (_show == CardDatabase.SHOW_FLARE ? 0.3f : 0.16f) * Mathf.Exp(-Mathf.Max(0f, t - _showKick) * 6f);
                if (_show == CardDatabase.SHOW_ASCEND) pos += Vector3.up * (0.7f * Mathf.Sin(sk * Mathf.PI));
            }
            if (t >= fStart)
            {
                float fly = _vFly[last];
                float a = Mathf.Clamp01((t - fStart) / Mathf.Max(0.1f, fly - fStart));
                swell = 1f + 0.35f * a * a + 0.05f * a * Mathf.Sin(t * 45f);
                if (beamCore) fade = 1f - Ease((t - (gone - 0.3f)) / 0.3f);
                else if (t >= fly) fade = 1f - Ease((t - fly) / 0.14f);
                if (_delivery == CardDatabase.CINE_GROUND || _delivery == CardDatabase.CINE_WAVE)
                    pos = Vector3.Lerp(_corePos, _from + _fwd * (0.8f * _body) + Vector3.up * 0.2f, Ease((t - (fly - 0.22f)) / 0.22f));
                else if (_delivery == CardDatabase.CINE_RAIN)
                    pos = _corePos + Vector3.up * (3f * Ease((t - (fly - 0.3f)) / 0.3f));
            }
            float s = full * grow * knock * swell * fade * (1f + 0.04f * Mathf.Sin(t * 9f));
            powerCore.position = pos + Vector3.up * (0.06f * Mathf.Sin(t * 2.3f));
            powerCore.localScale = Vector3.one * Mathf.Max(0.01f, s);
            powerCore.rotation = Quaternion.Euler(t * 40f, t * 70f * _flip, 0f);
            // Sparks are drawn into it from its display on: more as it grows, a rush as the finisher gathers.
            if (coreMotes != null && fade > 0.5f && t >= _tDisp)
            {
                coreMotes.transform.position = pos;
                coreMotes.transform.localScale = Vector3.one * (0.6f + 0.5f * full * grow);
                _moteAcc += dt * (10f + 7f * _vis) * (0.5f + grow) * (swell > 1.02f ? 2.5f : 1f);
                int n = (int)_moteAcc;
                if (n > 0) { coreMotes.Emit(n); _moteAcc -= n; }
            }
        }

        // ------------------------------------------------------------------ plotting each bolt's flight

        private int DefaultCount(int d)
        {
            if (d == CardDatabase.CINE_HELIX) return 2;
            if (d == CardDatabase.CINE_VOLLEY) return Mathf.Max(3, _vis);
            if (d == CardDatabase.CINE_RAIN) return 10;
            if (d == CardDatabase.CINE_ORBIT) return Mathf.Max(3, _vis);
            if (d == CardDatabase.CINE_WAVE) return 5;
            return 1;
        }

        /// <summary>How many bolts the volley in the air has: the spell's own for the finisher; a wave has a few, one more each time.</summary>
        private int VolleyBolts()
        {
            if (_vFinal) return _count > 0 ? _count : DefaultCount(_vd);
            int d = _vd;
            int n = d == CardDatabase.CINE_HELIX ? 2 : d == CardDatabase.CINE_VOLLEY || d == CardDatabase.CINE_ORBIT || d == CardDatabase.CINE_WAVE ? 3 :
                    d == CardDatabase.CINE_RAIN ? 5 : 1;
            return n + Mathf.Min(2, Mathf.Max(0, _v));
        }

        /// <summary>
        /// Plots the volley in the air (FlyVolley): its bolts' paths from where it left (_origin) to the target, by its
        /// delivery (_vd), timed to fly from _tRel and hit at _tHit, leaning _vFlip's way, _vSize of full weight.
        /// </summary>
        private void PlotHeads()
        {
            int n = VolleyBolts();
            if (_vd == CardDatabase.CINE_ORBIT) n = Mathf.Min(n, 8);
            n = Mathf.Clamp(n, 1, heads != null ? Mathf.Min(heads.Length, MAX_HEADS) : 0);
            _headCount = heads != null ? n : 0;
            float strike = _tHit - _tRel;
            float relLen = _tRelEnd - _tRel;
            Vector3 up = Vector3.up;
            float k = _reach;                       // swings and arcs sized to the distance from the middle of the ring
            float spread = (_aoe ? 2.4f : 0.9f) * Mathf.Min(1f, k + 0.2f);
            float lift = 1f + 0.5f * _height;
            for (int i = 0; i < _headCount; i++)
            {
                float from = 0f, to = strike, launch = 0f;
                // The main bolt meets the front of the target; a creature riding it pulls up a stride short.
                Vector3 hit = i == 0 && _creature && _vFinal ? _stop + up * 1.0f : HitFor(i);
                Vector3 A = _origin, B = _origin, C = hit, D = hit;
                float side = (i % 2 == 0 ? 1f : -1f) * _vFlip;
                int pair = (i + 1) / 2;
                int d = _vd;
                if (d == CardDatabase.CINE_LANCE)
                {
                    Vector3 off = _right * ((i - (n - 1) * 0.5f) * 0.4f);
                    A = _origin + off;
                    D = hit + off * (_aoe ? 2.5f : 0.3f);
                    from = 0.06f * i;
                }
                else if (d == CardDatabase.CINE_HELIX)
                {
                    A = _origin;
                    D = hit;
                }
                else if (d == CardDatabase.CINE_VOLLEY)
                {
                    float sw = side * (0.7f + 0.35f * pair) * k;
                    A = _origin + _right * (side * 0.12f);
                    D = hit;
                    if (_aoe || i > 0) D += _right * (side * 0.3f * pair * (_aoe ? 2.2f : 1f)) - _fwd * (_aoe ? 0f : 0.1f * pair);
                    B = Vector3.Lerp(A, D, 0.3f) + _right * sw + up * ((0.7f * lift + 0.2f * pair) * k);
                    C = Vector3.Lerp(A, D, 0.7f) + _right * (sw * 0.55f) + up * (0.5f * lift * k);
                    from = i == 0 ? 0f : 0.07f * i;
                    to = i == 0 ? strike : Mathf.Min(relLen - 0.05f, strike + 0.05f * i);
                }
                else if (d == CardDatabase.CINE_METEOR)
                {
                    Vector3 land = hit;
                    if (i > 0 && !(_aoe && _targetCount > 0)) land = _aim + (_right * Mathf.Cos(i * 2.4f) + _fwd * Mathf.Sin(i * 2.4f)) * spread + up * 0.9f;
                    A = _origin;
                    B = _from + up * ((8f + 1.5f * _height) * Mathf.Max(0.8f, k)) + _fwd * (0.8f * k) + _right * (side * 0.6f * pair);
                    C = land + up * ((9f + 1.5f * _height) * Mathf.Max(0.8f, k)) - _fwd * (1.2f * k);
                    D = land;
                    from = 0.09f * i;
                    to = i == 0 ? strike : Mathf.Min(relLen - 0.05f, strike + 0.08f * i);
                }
                else if (d == CardDatabase.CINE_RAIN)
                {
                    float ang = i * 137.5f * Mathf.Deg2Rad;
                    float r = i == 0 ? 0f : spread * (0.35f + 0.65f * ((i * 0.618f) % 1f));
                    Vector3 land = i == 0 || (_aoe && _targetCount > 0 && i < _targetCount * 2) ? new Vector3(hit.x, _aim.y + 0.2f, hit.z) + (_aoe && i >= _targetCount ? (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * 0.6f : Vector3.zero)
                        : _aim + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * r + up * 0.2f;
                    A = land + up * (8.5f + _height) - _fwd * (0.5f * _vFlip);
                    D = land;
                    float span = Mathf.Max(0.3f, strike - 0.3f);
                    float fall = 0.55f;
                    if (i == 0) { from = strike - fall; to = strike; }
                    else
                    {
                        from = 0.15f + span * ((i - 1) / (float)Mathf.Max(1, n - 1));
                        to = Mathf.Min(relLen - 0.05f, from + fall);
                    }
                }
                else if (d == CardDatabase.CINE_GROUND)
                {
                    Vector3 off = i == 0 ? Vector3.zero : _right * (side * 0.7f * pair * k);
                    A = new Vector3(_from.x, _ground + 0.12f, _from.z) + _fwd * (0.7f * _body) + off * 0.3f;
                    D = new Vector3(hit.x, _aim.y + 0.12f, hit.z) + off;
                    from = 0.06f * i;
                }
                else if (d == CardDatabase.CINE_BEAM)
                {
                    A = _origin;
                    D = _origin;
                    from = 0f;
                    to = relLen - 0.1f;
                }
                else if (d == CardDatabase.CINE_ORBIT)
                {
                    float fly = 0.4f;
                    launch = strike - fly - 0.11f * i;
                    launch = Mathf.Max(0.3f, launch);
                    A = OrbitPos(i, n, launch);
                    D = hit;
                    if (_aoe && i > 0) D += (_right * Mathf.Cos(i * 2.1f) + _fwd * Mathf.Sin(i * 2.1f)) * (spread * 0.7f);
                    from = 0f;
                    to = launch + fly;
                }
                else if (d == CardDatabase.CINE_ZIGZAG)
                {
                    A = _origin;
                    D = hit;
                    if (i > 0) D += _right * (side * 0.45f * pair * (_aoe ? 3f : 1f));
                    from = 0.05f * i;
                }
                else if (d == CardDatabase.CINE_PORTAL)
                {
                    // Into a ring at the hand, out of one high over the front of the target, and down on to it.
                    A = _origin;
                    B = _origin + _fwd * 0.7f;
                    C = new Vector3(hit.x, _aim.y, hit.z) - _fwd * (0.5f + 0.2f * i) + _right * (side * 0.5f * pair) + up * (3.4f + 0.4f * _height);
                    D = hit;
                    if (portals != null && portals.Length > 1)
                    {
                        if (portals[0] != null) portals[0].SetPositionAndRotation(B, Quaternion.FromToRotation(up, _fwd));
                        Vector3 down = (D - C).normalized;
                        if (portals[1] != null) portals[1].SetPositionAndRotation(C, Quaternion.FromToRotation(up, down));
                    }
                    from = 0.07f * i;
                }
                else if (d == CardDatabase.CINE_WAVE)
                {
                    float gap = (_aoe ? 1.2f : 0.7f) * Mathf.Min(1f, k + 0.2f);
                    float off = i == 0 ? 0f : side * gap * pair;
                    A = new Vector3(_from.x, _ground + 0.55f, _from.z) + _fwd * (0.9f * _body) + _right * off;
                    D = new Vector3(hit.x, _aim.y + 0.7f, hit.z) + _right * (off * (_aoe ? 0.9f : 0.4f));
                }
                else // CINE_BOOMERANG
                {
                    float sw = (i == 0 ? 1f : -1f) * _vFlip * 3.0f * k;
                    A = _origin;
                    B = _origin + _right * sw + _fwd * (0.4f * k) + up * (0.4f * lift);
                    C = hit + _right * (sw * 0.9f) - _fwd * (1.4f * k) + up * (0.5f * lift);
                    D = hit;
                    from = 0.16f * i;
                }
                _hA[i] = A; _hB[i] = B; _hC[i] = C; _hD[i] = D;
                _hFrom[i] = _tRel + from;
                _hTo[i] = _tRel + Mathf.Max(from + 0.15f, to);
                _hLaunch[i] = _tRel + launch;
                _hLanded[i] = false;
                Transform h = heads[i];
                if (h == null) continue;
                h.gameObject.SetActive(false);
                h.position = HeadPos(i, 0f, _hFrom[i]);
                float size = i == 0 && !(_creature && _vFinal) ? 1f : 0.55f;
                if (d == CardDatabase.CINE_RAIN) size = i == 0 ? 0.8f : 0.45f;
                if (d == CardDatabase.CINE_BEAM) size = 0.9f;
                size *= (0.75f + 0.08f * _vis) * _vSize;
                h.localScale = new Vector3(size, size, size);
                ShowShape(i, _vFinal && i == 0 && (_carrier != null || _creature) ? -1 : _shape);
            }
        }

        /// <summary>An orb circling the caster (ORBIT) at time t (seconds from the start).</summary>
        private Vector3 OrbitPos(int i, int n, float tRel)
        {
            float ang = (_vFlip * tRel * 320f + i * 360f / Mathf.Max(1, n)) * Mathf.Deg2Rad;
            float r = (1.1f + 0.06f * n) * _body;
            Vector3 ring = (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * r;
            return _from + ring + Vector3.up * ((1.5f + 0.25f * Mathf.Sin(tRel * 5f + i)) * _body);
        }

        private Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u)
        {
            float v = 1f - u;
            return a * (v * v * v) + b * (3f * v * v * u) + c * (3f * v * u * u) + d * (u * u * u);
        }

        /// <summary>Where bolt i is 'u' of the way along its flight ('t' seconds from the start, for orbiting and spinning paths).</summary>
        private Vector3 HeadPos(int i, float u, float t)
        {
            Vector3 A = _hA[i], B = _hB[i], C = _hC[i], D = _hD[i];
            int d = _vd;
            Vector3 up = Vector3.up;
            if (d == CardDatabase.CINE_LANCE) return Vector3.Lerp(A, D, Mathf.Pow(u, 1.4f)) + up * (Mathf.Sin(u * 30f + i) * 0.03f);
            if (d == CardDatabase.CINE_HELIX)
            {
                float n = Mathf.Max(1, _headCount);
                float th = _vFlip * u * Mathf.PI * (3f + _height) + i * Mathf.PI * 2f / n;
                float r = 0.75f * (Mathf.Sin(u * Mathf.PI) * 0.85f + 0.15f * (1f - u));
                return Vector3.Lerp(A, D, u) + (_right * Mathf.Cos(th) + up * Mathf.Sin(th)) * r;
            }
            if (d == CardDatabase.CINE_VOLLEY) return Bezier(A, B, C, D, u);
            if (d == CardDatabase.CINE_BOOMERANG) return Bezier(A, B, C, D, u);
            if (d == CardDatabase.CINE_METEOR)
            {
                // A slow climb into the sky, then a fast fall.
                float w = u < 0.45f ? (u / 0.45f) * 0.4f : 0.4f + 0.6f * Mathf.Pow((u - 0.45f) / 0.55f, 1.8f);
                return Bezier(A, B, C, D, w);
            }
            if (d == CardDatabase.CINE_RAIN) return Vector3.Lerp(A, D, u * u);
            if (d == CardDatabase.CINE_GROUND) return Vector3.Lerp(A, D, u);
            if (d == CardDatabase.CINE_BEAM) return A;
            if (d == CardDatabase.CINE_ORBIT)
            {
                if (t < _hLaunch[i]) return OrbitPos(i, _headCount, t - _tRel);
                float k = Mathf.Clamp01((t - _hLaunch[i]) / Mathf.Max(0.05f, _hTo[i] - _hLaunch[i]));
                return Vector3.Lerp(A, D, k * k);
            }
            if (d == CardDatabase.CINE_ZIGZAG)
            {
                // Six jagged legs, swinging less as it closes in.
                int K = 6;
                float f = Mathf.Clamp01(Mathf.Pow(u, 1.15f)) * K;
                int k = Mathf.Min(K - 1, (int)f);
                float a = f - k;
                return Vector3.Lerp(ZigJoint(i, k, A, D, K), ZigJoint(i, k + 1, A, D, K), a);
            }
            if (d == CardDatabase.CINE_PORTAL)
            {
                if (u < 0.3f) return Vector3.Lerp(A, B, u / 0.3f);
                if (u < 0.55f) return C;
                float k = (u - 0.55f) / 0.45f;
                return Vector3.Lerp(C, D, k * k);
            }
            // CINE_WAVE: abreast, rolling forward with a swell.
            return Vector3.Lerp(A, D, u) + up * (Mathf.Sin(u * Mathf.PI * 3f + i) * 0.12f);
        }

        private Vector3 ZigJoint(int i, int k, Vector3 A, Vector3 D, int K)
        {
            float f = k / (float)K;
            Vector3 p = Vector3.Lerp(A, D, f);
            if (k == 0 || k == K) return p;
            float swing = ((k % 2 == 0) ? 1f : -1f) * _vFlip * (i % 2 == 0 ? 1f : -1f) * 0.9f * _reach * (1f - f * 0.6f);
            return p + _right * swing + Vector3.up * (Mathf.Sin(k * 2.1f + i) * 0.35f);
        }

        private bool HeadHidden(int i, float u)
        {
            return _vd == CardDatabase.CINE_PORTAL && u >= 0.3f && u < 0.55f;
        }

        private void AnimateHeads(float t, float dt)
        {
            if (t < _tRel || heads == null) return;
            for (int i = 0; i < _headCount; i++)
            {
                Transform h = heads[i];
                if (h == null || _hLanded[i] || t < _hFrom[i]) continue;
                float u = Mathf.Clamp01((t - _hFrom[i]) / Mathf.Max(0.05f, _hTo[i] - _hFrom[i]));
                Vector3 p = HeadPos(i, u, t);
                Vector3 ahead = HeadPos(i, Mathf.Min(1f, u + 0.03f), t + 0.03f);
                Vector3 dir = ahead - p;
                bool hidden = HeadHidden(i, u);
                if (!h.gameObject.activeSelf && !hidden)
                {
                    h.position = p;
                    h.gameObject.SetActive(true);
                }
                else if (h.gameObject.activeSelf && hidden) h.gameObject.SetActive(false);
                h.position = p;
                if (dir.sqrMagnitude > 0.00001f)
                {
                    Quaternion face = Quaternion.LookRotation(dir);
                    h.rotation = _shape == 2 ? face * Quaternion.Euler(0f, 0f, t * 900f * _vFlip) : face;
                }
                if (i == 0) { _headPos = p; if (dir.sqrMagnitude > 0.00001f) _headDir = dir.normalized; }
                // A runner under the floor throws up sparks as it goes.
                if (_vd == CardDatabase.CINE_GROUND && impactBurst != null)
                {
                    _trailAcc += dt * 40f;
                    if (_trailAcc >= 1f) { int k = (int)_trailAcc; _trailAcc -= k; Burst(impactBurst, p, Mathf.Min(4, k)); }
                }
                if (u >= 1f && _vd != CardDatabase.CINE_BEAM)
                {
                    // It's there: the head goes out (its trail lingers) and sparks fly off; the main one is the spell's hit.
                    _hLanded[i] = true;
                    ShowShape(i, -1);
                    if (i > 0) Burst(impactBurst, p, 8 + 2 * _vis);
                }
                if (_vd == CardDatabase.CINE_BEAM && t >= _hTo[i]) { _hLanded[i] = true; h.gameObject.SetActive(false); }
            }
        }

        /// <summary>One of bolt i's shapes on (-1 = none: a creature carries it, or it has landed).</summary>
        private void ShowShape(int i, int shape)
        {
            if (headShapes == null) return;
            for (int k = 0; k < SHAPES; k++)
            {
                int ix = i * SHAPES + k;
                if (ix < headShapes.Length && headShapes[ix] != null) headShapes[ix].enabled = k == shape;
            }
        }

        /// <summary>
        /// A summoned creature (the subject of a creature spell): it arrives beside the caster the spell's own way
        /// (ArrivePos), rears for its display (AnimateDisplay), roars as a wave goes (FlyVolley), then rides the finisher's
        /// first bolt (flying or running the route), strikes, is thrown back and breaks up. One that pours out a beam
        /// stands before the caster for the whole of it.
        /// </summary>
        private void AnimateCarrier(float t)
        {
            if (_carrier == null || t < _tUnl) return;
            int last = _vCount - 1;
            float fly = _vFly[last];
            bool beamStand = _delivery == CardDatabase.CINE_BEAM;
            bool flat = !_carrierFlyer && (_delivery == CardDatabase.CINE_LANCE || _delivery == CardDatabase.CINE_ZIGZAG || _delivery == CardDatabase.CINE_GROUND ||
                                           _delivery == CardDatabase.CINE_WAVE || _delivery == CardDatabase.CINE_BOOMERANG || _delivery == CardDatabase.CINE_BEAM);
            Vector3 p;
            Vector3 face = _fwd;
            // It grows as it arrives (quicker when it has somewhere to come from).
            float arr = Mathf.Clamp01((t - _tUnl) / Mathf.Max(0.05f, _tDisp - _tUnl));
            bool travels = _arrive == CardDatabase.ARRIVE_ERUPT || _arrive == CardDatabase.ARRIVE_DESCEND || _arrive == CardDatabase.ARRIVE_RIFT;
            float scale = _cine ? Ease(arr * (travels ? 2.5f : 1.2f)) : Ease((t - _tUnl) / 0.45f);
            if (beamStand || t < fly)
            {
                p = _cine ? ArrivePos(_summonAt, arr) : _summonAt;
                if (_carrierFlyer) p += Vector3.up * (0.9f + 0.12f * Mathf.Sin(t * 3f));
                if (beamStand && t > _vEnd[last] - 0.4f) scale *= 1f - Ease((t - (_vEnd[last] - 0.4f)) / 0.35f);
                if (_carrierCue == 0)
                {
                    _carrierCue = 1;
                    Burst(portalBurst, p + Vector3.up * 0.8f, 30);
                }
                if (beamStand && _carrierCue == 1 && t >= fly)
                {
                    _carrierCue = 2;
                    if (_carrierAnim != null) _carrierAnim.SetTrigger("Cast");
                }
            }
            else if (t < _tStrike)
            {
                // The finisher: it goes with the spell.
                p = _headPos - Vector3.up * 0.9f;
                Vector3 d = _headPos - _carrierLast;
                if (d.sqrMagnitude > 0.00001f) face = d.normalized;
                if (_carrierCue < 2)
                {
                    // (No staging: it grows out of the caster's hand as it goes.)
                    if (_carrierCue == 0) Burst(portalBurst, _origin, 30);
                    _carrierCue = 2;
                    if (_carrierAnim != null) _carrierAnim.SetTrigger(flat ? "Walk" : "Cast");
                }
            }
            else
            {
                // Thrown back off the target, then gone in a burst of sparkles.
                float r = t - _tStrike;
                Vector3 back = _stop - _fwd * (0.9f * Ease(r / 0.3f));
                p = back + Vector3.up * (Mathf.Sin(Mathf.Clamp01(r / 0.3f) * Mathf.PI) * 0.4f);
                if (_carrierFlyer) p += Vector3.up * 0.4f;
                float vanish = Mathf.Clamp01((r - 0.35f) / 0.4f);
                scale = 1f - vanish;
                p += Vector3.up * (vanish * 0.7f);
                if (_carrierCue < 3)
                {
                    _carrierCue = 3;
                    if (_carrierAnim != null) _carrierAnim.SetTrigger("Hit");
                }
                if (_carrierCue == 3 && r >= 0.35f) { _carrierCue = 4; Burst(portalBurst, p + Vector3.up, 45); }
            }
            if (flat && t >= fly) p.y = _aim.y;
            if (!_carrierFlyer) face.y = 0f;
            _carrier.position = p;
            if (face.sqrMagnitude > 0.0001f) _carrier.rotation = Quaternion.LookRotation(face);
            _carrier.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            _carrierLast = _headPos;
        }

        // ------------------------------------------------------------------ Sunfire Wyrm (CardDatabase.SPECIAL_WYRM)

        /// <summary>
        /// Sunfire Wyrm's own arrival, display and attack, round its dragon. SunfireWyrm flies it, sheds its fire and
        /// gives it its voice; this is the stage's part. It comes out of the caster on the release. Each time it comes
        /// round them a ring of light spreads under them. It settles behind them (a blast along the floor) and roars at
        /// the display's peak: a ring, sparks along the floor, the aura jumping, the dark lifting for a flash. As the
        /// attack starts it goes, with another blast. Each target bursts as its head goes through them (sparks, shards,
        /// its flames, a ring under them; the first makes them flinch). Then it pulls up and breathes fire on them
        /// until the exit, and the spell's hit lands in the middle of that (Strike, as for any spell).
        /// </summary>
        private void AnimateWyrm(float t, float dt)
        {
            if (wyrm == null) return;
            wyrm.Animate(t, dt);
            if (t < _tUnl || t >= _tAfter) return;
            Vector3 up = Vector3.up;
            Vector3 floor = _from + up * 0.12f;
            _headPos = wyrm.Head();
            // The hum climbs again under its display, as under any spell's.
            if (_showCue == 0 && t >= _tDisp)
            {
                _showCue = 1;
                ShowHum(_from + up * (1.6f * _body));
            }
            if (t < _tBar)
            {
                // A ring of light under the caster each time it comes round them.
                int laps = wyrm.Laps();
                if (laps > _wyrmLaps)
                {
                    _wyrmLaps = laps;
                    _showKick = t;
                    _auraKick = t;
                    Pulse(_from, (1.6f + 0.5f * laps) * _scale, 0.55f);
                    Shock(floor, 10 + 3 * _vis, 0.3f + 0.1f * laps);
                    SoundAt(whooshSound, _from + up * (1.2f * _body), 0.3f);
                }
            }
            if (_wyrmCue == 0 && t >= _wyrmRest)
            {
                // It settles behind them.
                _wyrmCue = 1;
                _kickAt = t;
                Shock(floor, 30 + 4 * _vis, 0.7f);
            }
            if (_wyrmCue == 1 && t >= _wyrmRoar)
            {
                // The display's peak: it roars.
                _wyrmCue = 2;
                _showKick = t;
                _kickAt = t;
                _arcFlash = t;
                _auraKick = t;
                Pulse(_from, (3f + 0.6f * _tier) * _scale, 0.7f);
                Shock(floor, 30 + 10 * _vis, 0.8f + 0.2f * _tier);
                Burst(portalBurst, _headPos, 20 + 6 * _vis);
            }
            if (_wyrmCue == 2 && t >= _tBar)
            {
                // It goes.
                _wyrmCue = 3;
                _kickAt = t;
                Shock(floor, 40, 0.8f);
                SoundAt(whooshSound, _headPos, 0.8f);
            }
            if (_wyrmCue != 3) return;
            // Through each of them in turn: a burst on each as its head goes through.
            int passed = wyrm.Passed();
            while (_wyrmPassed < passed)
            {
                int k = _wyrmPassed;
                _wyrmPassed++;
                _kickAt = t;
                Vector3 at = WyrmHit(k);
                float big = ImpactSize() * 0.7f;
                if (impactBurst != null)
                {
                    impactBurst.transform.position = at;
                    impactBurst.transform.localScale = Vector3.one * big;
                    impactBurst.Emit(14 + 6 * _vis);
                }
                if (shardBurst != null)
                {
                    shardBurst.transform.position = at;
                    shardBurst.transform.localScale = Vector3.one * big;
                    shardBurst.Emit(8 + 3 * _vis);
                }
                wyrm.Scorch(at);
                Pulse(new Vector3(at.x, _aim.y, at.z), 2.2f + 0.25f * _vis, 0.5f);
                Shock(new Vector3(at.x, _aim.y + 0.12f, at.z), 14 + 5 * _vis, 0.5f);
                SoundAt(impactSound, at, k == 0 ? 0.7f : 0.45f);
                if (k == 0 && _owner != null && !_friendly) _owner.CineFlinch();
            }
            if (t >= _wyrmHover)
            {
                // It pulls up before them and breathes.
                _wyrmCue = 4;
                _kickAt = t;
            }
        }

        /// <summary>Where the wyrm's head meets the k-th target it goes through (HitFor, in its order, not the seats').</summary>
        private Vector3 WyrmHit(int k)
        {
            if (!_aoe || _targetCount == 0) return _hit;
            Vector3 target = wyrm.TargetAt(k);
            Vector3 d = target - _from;
            d.y = 0f;
            Vector3 toward = d.sqrMagnitude > 0.001f ? d.normalized : _fwd;
            return target - toward * 0.35f + Vector3.up * 1.0f;
        }

        private void AnimateBeam(float t)
        {
            if (beam == null || !beam.gameObject.activeSelf) return;
            if (t >= _tRelEnd) { beam.gameObject.SetActive(false); return; }
            Vector3 start = _origin;
            Vector3 end = _hit;
            Vector3 d = end - start;
            float len = d.magnitude;
            float k = t - _tRel;
            float grow = Ease(k / 0.35f) * Mathf.Min(1f, Mathf.Max(0.05f, (t - _tRel) / Mathf.Max(0.05f, _tHit - _tRel)));
            float w = (0.3f + 0.05f * _vis) * (1f + 0.15f * Mathf.Sin(t * 40f)) * (_aoe ? 1.6f : 1f);
            w *= 1f - Ease((t - (_tRelEnd - 0.3f)) / 0.3f);
            beam.position = start;
            if (len > 0.001f) beam.rotation = Quaternion.FromToRotation(Vector3.up, d / len) * Quaternion.Euler(0f, t * 200f, 0f);
            beam.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, len * grow), Mathf.Max(0.01f, w));
            _headPos = Vector3.Lerp(start, end, Mathf.Min(1f, grow));
            _headDir = len > 0.001f ? d / len : _fwd;
            if (t >= _tHit && impactBurst != null && Random.value < 0.5f) Burst(impactBurst, end, 2);
        }

        private void AnimatePortals(float t)
        {
            // (Before the attack a portal can only be the gate a RIFT arrival comes through: AnimateArrival's.)
            if (portals == null || t < _tBar) return;
            for (int i = 0; i < portals.Length && i < 2; i++)
            {
                Transform p = portals[i];
                if (p == null || !p.gameObject.activeSelf) continue;
                float open, close;
                if (_vd == CardDatabase.CINE_RAIN)
                {
                    open = Ease((t - _tRel) / 0.35f);
                    close = 1f - Ease((t - (_tHit + 0.2f)) / 0.4f);
                }
                else
                {
                    float strike = _tHit - _tRel;
                    float t0 = i == 0 ? _tRel : _tRel + strike * 0.3f;
                    float t1 = i == 0 ? _tRel + strike * 0.45f : _tHit + 0.1f;
                    open = Ease((t - t0) / 0.2f);
                    close = 1f - Ease((t - t1) / 0.25f);
                }
                float s = Mathf.Max(0f, Mathf.Min(open, close)) * (_vd == CardDatabase.CINE_RAIN ? (_aoe ? 6f : 3.5f) : 1.6f + 0.1f * _vis);
                if (s <= 0.01f && t > _tRel + 0.3f && close <= 0f) { p.gameObject.SetActive(false); continue; }
                p.localScale = new Vector3(Mathf.Max(0.01f, s), 1f, Mathf.Max(0.01f, s));
            }
        }

        // ------------------------------------------------------------------ the finisher's impact

        /// <summary>The finisher hits (the spell's results land with it): each kind of impact its own way, everything sized by the spell.</summary>
        private void Strike()
        {
            Vector3 at = _creature ? Vector3.Lerp(_stop + Vector3.up * 1.0f, _hit, 0.6f) : _hit;
            // Where the main bolt really is as it lands (so the burst is on it, whatever path it took).
            if (!_aoe && heads != null && _headCount > 0 && heads[0] != null && heads[0].gameObject.activeInHierarchy)
            {
                Vector3 h0 = heads[0].position;
                Vector3 d = h0 - at;
                if (d.sqrMagnitude < 4f) at = h0;
            }
            float big = ImpactSize();
            int im = _impact;
            float burstMul = im == CardDatabase.IMPACT_NOVA ? 1.6f : im == CardDatabase.IMPACT_IMPLODE ? 1.4f :
                             im == CardDatabase.IMPACT_BURST ? 1f : im == CardDatabase.IMPACT_SHATTER ? 0.4f : 0.55f;
            // An area spell bursts on every target it hits (the flash and the ground effects on the first).
            bool many = _aoe && _targetCount > 0;
            if (many) { at = HitFor(0); big = _scale; }
            int bursts = many ? _targetCount : 1;
            for (int b = 0; b < bursts; b++)
            {
                Vector3 bat = many ? HitFor(b) : at;
                if (impactBurst != null)
                {
                    impactBurst.transform.position = bat;
                    impactBurst.transform.localScale = Vector3.one * big;
                    impactBurst.Emit((int)((35 + 22 * _vis) * burstMul));
                }
                if (shardBurst != null && (im == CardDatabase.IMPACT_SHATTER || im == CardDatabase.IMPACT_SPIKES))
                {
                    shardBurst.transform.position = bat;
                    shardBurst.transform.localScale = Vector3.one * big;
                    shardBurst.Emit(im == CardDatabase.IMPACT_SHATTER ? 40 + 25 * _vis : 12 + 6 * _vis);
                }
            }
            if (many) _impactAt = new Vector3(_targets[0].x, _aim.y, _targets[0].z);
            else _impactAt = new Vector3(at.x, _aim.y, at.z);
            // The flash is on the target itself (an area spell's bursts and rings are on each of its targets instead).
            if (impactFlash != null && !many && im != CardDatabase.IMPACT_RINGS && im != CardDatabase.IMPACT_VORTEX)
            {
                impactFlash.position = at;
                impactFlash.gameObject.SetActive(true);
            }
            if (shockRings != null)
                for (int i = 0; i < shockRings.Length; i++)
                {
                    if (shockRings[i] == null) continue;
                    bool on = i == 0 || im == CardDatabase.IMPACT_RINGS || im == CardDatabase.IMPACT_VORTEX || (i == 1 && (im == CardDatabase.IMPACT_NOVA || _pips >= 5 || _aoe));
                    // One ring under each of an area spell's targets (up to three), else all on the target.
                    Vector3 under = _aoe && _targetCount > 0 ? new Vector3(_targets[i % _targetCount].x, _aim.y, _targets[i % _targetCount].z) : _impactAt;
                    if (_aoe && _targetCount > 1) on = i < _targetCount;
                    shockRings[i].position = under + Vector3.up * (0.08f + 0.02f * i);
                    shockRings[i].rotation = Quaternion.identity;
                    shockRings[i].localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    shockRings[i].gameObject.SetActive(on);
                }
            if (impactPillar != null && im == CardDatabase.IMPACT_PILLAR)
            {
                impactPillar.position = _impactAt;
                impactPillar.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                impactPillar.gameObject.SetActive(true);
            }
            if (spikes != null && im == CardDatabase.IMPACT_SPIKES)
            {
                float r0 = (_aoe ? 1.4f : 0.7f) * _tScale, r1 = (_aoe ? 2.8f : 1.5f) * _tScale;
                for (int i = 0; i < spikes.Length && i < 8; i++)
                {
                    if (spikes[i] == null) continue;
                    float ang = (i * 45f + 20f * _flip) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(r0, r1, (i * 0.618f) % 1f);
                    Vector3 out_ = _right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang);
                    _spikeAt[i] = _impactAt + out_ * r;
                    _spikeRot[i] = Quaternion.AngleAxis(15f + 12f * ((i * 0.37f) % 1f), Vector3.Cross(Vector3.up, out_));
                    _spikeH[i] = (0.9f + 0.9f * ((i * 0.53f) % 1f)) * big;
                    spikes[i].SetPositionAndRotation(_spikeAt[i], _spikeRot[i]);
                    spikes[i].localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    spikes[i].gameObject.SetActive(true);
                }
            }
            // The more power, the harder it lands: a ring of sparks along the floor, crackling off the hit, and for the
            // biggest the rune in the sky comes apart in a rain of light.
            if (_cine)
            {
                if (_tier >= 2) Shock(_impactAt + Vector3.up * 0.12f, 30 + 20 * _tier, (0.6f + 0.3f * _tier) * Mathf.Max(1f, _tScale * 0.8f));
                if (_tier >= 3 && arcs != null && !_friendly)
                {
                    arcs.transform.position = at;
                    arcs.transform.localScale = Vector3.one * big;
                    arcs.Emit(10 + 5 * _tier);
                }
                if (_tier >= 4 && skyfall != null)
                {
                    skyfall.transform.position = _impactAt + Vector3.up * SkyHeight();
                    skyfall.transform.localScale = Vector3.one * (_scale * 1.3f);
                    skyfall.Emit(140);
                }
            }
            // A spell that helps lands with a rush of air, not a boom (the heal's own sound comes with its number).
            if (_friendly) SoundAt(whooshSound, at, 0.8f);
            else Sound(_pips >= 4 && impactBigSound != null ? impactBigSound : impactSound, at);
        }

        /// <summary>How big the impact is: the spell's pips and the target's size (not the caster's: a huge boss's spell still lands on a wizard-sized target).</summary>
        private float ImpactSize()
        {
            return (0.85f + 0.17f * _vis) * Mathf.Max(1f, _tScale * 0.8f) * (_aoe ? 1.25f : 1f);
        }

        private float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void AnimateImpact(float t, float dt)
        {
            int im = _impact;
            float big = ImpactSize();
            // Before an implosion the school's energy is drawn in to the target; before a rune stamp the rune comes down.
            if (im == CardDatabase.IMPACT_IMPLODE && chargeAura != null && t >= _tStrike - 0.55f && t < _tStrike)
            {
                chargeAura.transform.position = _impactAt + Vector3.up * 1.0f;
                chargeAura.transform.localScale = Vector3.one * big;
                _auraAcc += dt * (40f + 15f * _vis);
                int n = (int)_auraAcc;
                if (n > 0) { chargeAura.Emit(n); _auraAcc -= n; }
            }
            if (im == CardDatabase.IMPACT_SIGIL && sigilStamp != null && t >= _tStrike - 0.3f)
            {
                float k = t - (_tStrike - 0.3f);
                if (k >= 1.3f) sigilStamp.gameObject.SetActive(false);
                else
                {
                    if (!sigilStamp.gameObject.activeSelf) sigilStamp.gameObject.SetActive(true);
                    float fall = Mathf.Clamp01(k / 0.3f);
                    float y = Mathf.Lerp(3.2f, 0.09f, fall * fall);
                    float s = 2.4f * big * (fall < 1f ? 1f : 1f + 0.15f * Ease((k - 0.3f) / 0.2f));
                    sigilStamp.SetPositionAndRotation(_impactAt + Vector3.up * y, Quaternion.Euler(0f, t * 30f * _flip, 0f));
                    sigilStamp.localScale = new Vector3(s, s, s);
                    if (_sigilMat != null)
                    {
                        _sigilMat.SetFloat("_Flare", fall >= 1f ? Mathf.Max(0f, 2.6f - (k - 0.3f) * 3f) : 0.4f);
                        _sigilMat.SetFloat("_Fade", 1f - Ease((k - 0.6f) / 0.7f));
                    }
                }
            }
            if (t < _tStrike) return;
            float r = t - _tStrike;
            // A costly spell's hit goes on going off: aftershocks round the target, one after another (one at 3 pips, five at 7).
            int shocks = _cine && !_friendly ? Mathf.Clamp(_pips - 2, 0, 5) : 0;
            if (_afterDone < shocks && r >= 0.16f * (_afterDone + 1))
            {
                _afterDone++;
                _kickAt = t;
                float ang = (_afterDone * 137.5f + 40f * _flip) * Mathf.Deg2Rad;
                float rad = (0.9f + 0.45f * _afterDone) * _tScale;
                Vector3 at = _impactAt + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * rad + Vector3.up * (0.6f + 0.25f * _afterDone);
                if (impactBurst != null)
                {
                    impactBurst.transform.position = at;
                    impactBurst.transform.localScale = Vector3.one * (big * 0.6f);
                    impactBurst.Emit(20 + 8 * _vis);
                }
                Shock(new Vector3(at.x, _aim.y + 0.12f, at.z), 12 + 3 * _vis, 0.3f + 0.05f * _vis);
                SoundAt(impactSound, at, 0.35f);
            }
            if (impactFlash != null && impactFlash.gameObject.activeSelf)
            {
                float peak = im == CardDatabase.IMPACT_NOVA ? 5.2f : im == CardDatabase.IMPACT_IMPLODE ? 3.2f : 2.4f;
                float len = im == CardDatabase.IMPACT_NOVA ? 0.6f : 0.3f;
                float s = r < 0.12f ? Mathf.Lerp(0.3f, peak, r / 0.12f) : Mathf.Lerp(peak, 0f, (r - 0.12f) / len);
                s *= big;
                if (s <= 0.01f) impactFlash.gameObject.SetActive(false);
                else impactFlash.localScale = new Vector3(s, s, s);
            }
            if (shockRings != null)
                for (int i = 0; i < shockRings.Length; i++)
                {
                    Transform ring = shockRings[i];
                    if (ring == null || !ring.gameObject.activeSelf) continue;
                    if (im == CardDatabase.IMPACT_VORTEX)
                    {
                        // A funnel: the rings climb, turning and narrowing, then thin away.
                        float vk = (r - 0.1f * i) / 1.3f;
                        if (vk < 0f) continue;
                        if (vk >= 1f) { ring.gameObject.SetActive(false); continue; }
                        float size = Mathf.Lerp(3.2f, 1.0f, vk) * big * (1f - 0.15f * i);
                        ring.position = _impactAt + Vector3.up * (0.2f + vk * (2.5f + 0.6f * i) * big);
                        ring.rotation = Quaternion.Euler(8f * Mathf.Sin(t * 6f + i), t * (400f + 120f * i) * _flip, 0f);
                        ring.localScale = new Vector3(size, Mathf.Max(0.01f, 1f - vk), size);
                        continue;
                    }
                    float gap = im == CardDatabase.IMPACT_RINGS ? 0.16f : 0.12f;
                    float rk = (r - gap * i) / (im == CardDatabase.IMPACT_RINGS ? 0.8f : 0.65f);
                    if (rk < 0f) continue;
                    if (rk >= 1f) { ring.gameObject.SetActive(false); continue; }
                    // Sized to the ring: they spread round the target without swamping the whole circle.
                    float far = (im == CardDatabase.IMPACT_NOVA ? 6f + 0.45f * _vis : im == CardDatabase.IMPACT_RINGS ? 3f + 1.2f * i + 0.35f * _vis : 3.5f + 0.4f * _vis) * _tScale;
                    float sz = Mathf.Lerp(0.6f, far * (_aoe ? 1.4f : 1f), 1f - (1f - rk) * (1f - rk));
                    ring.localScale = new Vector3(sz, Mathf.Max(0.01f, 1f - rk), sz);
                }
            if (impactPillar != null && impactPillar.gameObject.activeSelf)
            {
                float pk = r / 0.95f;
                if (pk >= 1f) impactPillar.gameObject.SetActive(false);
                else
                {
                    float h = Ease(pk / 0.25f) * (6f + 1.2f * _vis);
                    float w = (1.3f + 0.15f * _vis) * (1f - Ease((pk - 0.3f) / 0.7f)) * (_aoe ? 1.6f : 1f);
                    impactPillar.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, h), Mathf.Max(0.01f, w));
                    impactPillar.rotation = Quaternion.Euler(0f, t * 90f, 0f);
                }
            }
            if (spikes != null && im == CardDatabase.IMPACT_SPIKES)
            {
                float rise = Ease(r / 0.14f);
                float sink = 1f - Ease((t - (_tAfter - 0.45f)) / 0.4f);
                for (int i = 0; i < spikes.Length && i < 8; i++)
                {
                    if (spikes[i] == null || !spikes[i].gameObject.activeSelf) continue;
                    float h = _spikeH[i] * rise * sink;
                    float w = (0.22f + 0.03f * _vis) * (0.6f + 0.4f * rise);
                    spikes[i].localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, h), Mathf.Max(0.01f, w));
                    if (sink <= 0.01f) spikes[i].gameObject.SetActive(false);
                }
            }
            // Embers drift up off the target while the shots hold (a nova and a vortex throw up more).
            if (embers != null && t < _tAfter)
            {
                embers.transform.position = _impactAt + Vector3.up * 0.6f;
                embers.transform.localScale = Vector3.one * big;
                float fall = 1f - Mathf.Clamp01(r / Mathf.Max(0.5f, _tAfter - _tStrike));
                float rate = (18f + 8f * _vis) * (im == CardDatabase.IMPACT_NOVA || im == CardDatabase.IMPACT_VORTEX ? 1.7f : 1f);
                _emberAcc += dt * rate * fall;
                int n = (int)_emberAcc;
                if (n > 0) { embers.Emit(n); _emberAcc -= n; }
            }
        }

        // ------------------------------------------------------------------ the charge

        /// <summary>How far through the charge (0 as the caster arrives, 1 on the release).</summary>
        private float Build(float t)
        {
            return Mathf.Clamp01((t - _tCast) / Mathf.Max(0.1f, _tUnl - _tCast));
        }

        /// <summary>
        /// The rune opens under the caster and draws itself through the charge, turning faster as the power builds; it
        /// flares on the release and each time a volley goes, jumps with it, and closes as the unleash ends.
        /// </summary>
        private void AnimateRune(float t, float dt)
        {
            if (runeCircle == null || !runeCircle.gameObject.activeSelf) return;
            float open = Ease((t - _tCast) / 0.45f);
            // It closes as the unleash ends; a SCATTER or SINK exit shuts it as the exit begins.
            bool early = _cine && (_leave == CardDatabase.EXIT_SCATTER || _leave == CardDatabase.EXIT_SINK);
            float shut = early ? Mathf.Max(0.15f, (_tAfter - _tExit) * 0.6f) : 0.35f;
            float close = 1f - Ease((t - (early ? _tExit : _tAfter - shut)) / shut);
            float jump = 0.08f * Mathf.Exp(-Mathf.Max(0f, t - _flareAt) * 6f);
            float s = 2.6f * _scale * Mathf.Min(open, close) * (1f + 0.03f * Mathf.Sin(t * 14f) + jump);
            runeCircle.localScale = new Vector3(Mathf.Max(0.01f, s), Mathf.Max(0.01f, s), Mathf.Max(0.01f, s));
            float spin = (40f + 8f * _vis) * (t < _tUnl ? 0.5f + 1.5f * Build(t) : 1f);
            _runeAngle += dt * spin * _flip;
            runeCircle.rotation = Quaternion.Euler(0f, _runeAngle, 0f);
            if (runeRing != null) runeRing.localRotation = Quaternion.Euler(0f, -_runeAngle * 2.2f, 0f);
            if (_runeMat != null)
            {
                float draw = Mathf.Clamp01((t - _tCast) / Mathf.Max(0.3f, (_tUnl - _tCast) * 0.6f));
                _runeMat.SetFloat("_Reveal", draw < 1f ? draw : 1.05f);
                // It flickers as power crackles out of the caster, and flares when it's let go.
                float flare = t >= _tUnl ? Mathf.Max(0f, 2.5f - (t - _flareAt) * 3f) : 0.6f * Mathf.Exp(-Mathf.Max(0f, t - _arcFlash) * 9f);
                _runeMat.SetFloat("_Flare", flare);
            }
        }

        /// <summary>
        /// The heart of every charge: a battle aura. Power streams in to the caster from all round, then the aura catches
        /// with a blast along the floor: a flame-shaped shell of light in the school's colour, white-hot at its root, its
        /// outline licking and flickering (the WizardsVRC/AuraFlame shader), tongues of flame racing up inside it and wind
        /// rushing up round it. It burns taller and wilder as the charge builds, swells with every beat, flares to half as
        /// big again just before the release, and bursts outward as the spell goes; a thin glow of it stays on the caster
        /// until their spell lands (and jumps again at the display's peak).
        /// How big it is comes from the pip cost: about 2.6 m tall round a wizard for a 1-pip spell, nearly 6 m for a 7-pip
        /// one. From 3 pips the wind streaks join in and grit lifts off the floor; from 5 a second, paler shell burns
        /// outside the first and power crackles out of it (ChargeTiers); at 7 the sky's rune pours light down into it.
        /// </summary>
        private void AnimateAura(float t, float dt)
        {
            if (auraShell == null || !_cine) return;
            float charge = Mathf.Max(0.1f, _tUnl - _tCast);
            float ignite = _tCast + Mathf.Min(0.5f, charge * 0.18f);
            float build = Build(t);
            // Before it catches, power streams in to the caster.
            if (t >= _tCast && t < ignite && chargeAura != null)
            {
                chargeAura.transform.position = _from + Vector3.up * (1.2f * _body);
                chargeAura.transform.localScale = Vector3.one * _scale;
                _auraAcc += dt * (50f + 16f * _vis);
                int g = (int)_auraAcc;
                if (g > 0) { chargeAura.Emit(g); _auraAcc -= g; }
            }
            // How brightly it burns: full through the charge, then a glow that lasts until the strike.
            float lit = 0f;
            if (t >= ignite && t < _tUnl) lit = Mathf.Clamp01((t - ignite) / 0.1f);
            else if (t >= _tUnl && t < _tStrike + 0.3f)
                lit = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01((t - _tUnl) / 0.4f)) * (1f - Mathf.Clamp01((t - _tStrike) / 0.3f));
            bool on = lit > 0.001f;
            if (auraShell.gameObject.activeSelf != on) auraShell.gameObject.SetActive(on);
            bool outer = on && _tier >= 3 && auraShellOuter != null;
            if (auraShellOuter != null && auraShellOuter.gameObject.activeSelf != outer) auraShellOuter.gameObject.SetActive(outer);

            // It catches: a ring and a spray of sparks along the floor, a jolt of light, its sound.
            if (!_auraLit && t >= ignite && t < _tUnl)
            {
                _auraLit = true;
                _auraKick = t;
                _arcFlash = t;
                _kickAt = t;
                Pulse(_from, (3f + 0.6f * _tier) * _scale, 0.5f);
                Shock(_from + Vector3.up * 0.12f, 10 + 3 * _vis, 0.5f + 0.2f * _tier);
                SoundAt(igniteSound != null ? igniteSound : releaseSound, _from + Vector3.up * (1.2f * _body), 0.45f + 0.12f * _tier);
            }
            if (!on) return;

            float tall = 2.1f * _body + 0.2f + 0.27f * _vis;
            float wide = tall * 0.6f + 0.3f * _body;
            float flare = t < _tUnl ? Mathf.Clamp01((build - 0.82f) / 0.18f) : 1f;
            float beat = Mathf.Exp(-Mathf.Max(0f, t - _auraKick) * 7f);
            float size = Mathf.Lerp(0.82f, 1.12f, build) * (1f + 0.5f * flare * flare);
            if (t >= _tUnl)
            {
                // The release: it bursts outward as the spell goes, then settles close round the caster.
                float after = t - _tUnl;
                size *= after < 0.3f ? 1f + 0.6f * (after / 0.3f) : Mathf.Lerp(1.6f, 0.55f, Mathf.Clamp01((after - 0.3f) / 0.35f));
            }
            size *= 1f + 0.16f * beat;
            float shimmer = 1f + 0.05f * Mathf.Sin(t * 23f);
            Vector3 foot = _from - Vector3.up * 0.05f;
            float power = lit * (0.75f + 0.55f * build + (t < _tUnl ? 0.9f * flare : 0f) + 0.5f * beat) * (0.85f + 0.15f * _tier);
            auraShell.position = foot;
            auraShell.rotation = Quaternion.Euler(0f, t * 140f * _flip, 0f);
            auraShell.localScale = new Vector3(wide * size * shimmer, tall * size, wide * size * shimmer);
            if (_auraMat != null)
            {
                _auraMat.SetFloat("_Power", power);
                _auraMat.SetFloat("_Flicker", 8f + 8f * build + 6f * flare);
                _auraMat.SetFloat("_Wobble", 0.12f + 0.08f * build + 0.08f * flare);
            }
            if (outer)
            {
                auraShellOuter.position = foot;
                auraShellOuter.rotation = Quaternion.Euler(0f, -t * 95f * _flip, 0f);
                auraShellOuter.localScale = new Vector3(wide * size * 1.3f, tall * size * 1.18f, wide * size * 1.3f);
                if (_auraOuterMat != null)
                {
                    _auraOuterMat.SetFloat("_Power", power * 0.5f);
                    _auraOuterMat.SetFloat("_Flicker", 6f + 6f * build);
                    _auraOuterMat.SetFloat("_Wobble", 0.18f + 0.1f * build);
                }
            }

            // The flames inside it (built for a wizard's 1-pip aura, scaled up with it): thick through the charge, a
            // rush at the flare, a trickle while the glow lasts.
            float fit = tall / 2.57f * size;
            if (auraFlames != null)
            {
                auraFlames.transform.position = _from + Vector3.up * 0.05f;
                auraFlames.transform.localScale = Vector3.one * fit;
                float rate = t < _tUnl ? (38f + 9f * _vis) * (0.55f + 0.6f * build + 1.2f * flare) : (10f + 3f * _vis) * lit;
                _flameAcc += dt * rate;
                int f = (int)_flameAcc;
                if (f > 0) { auraFlames.Emit(f); _flameAcc -= f; }
            }
            if (t >= _tUnl) return;
            if (auraStreaks != null && _tier >= 2)
            {
                auraStreaks.transform.position = _from + Vector3.up * 0.05f;
                auraStreaks.transform.localScale = Vector3.one * fit;
                _streakAcc += dt * (10f + 5f * _vis) * (0.3f + build + flare);
                int w = (int)_streakAcc;
                if (w > 0) { auraStreaks.Emit(w); _streakAcc -= w; }
            }
            // A small spell has no tier to beat for it or lift the grit (ChargeTiers): the aura does both, gently.
            if (_tier >= 2) return;
            if (t >= _nextBeat)
            {
                _nextBeat = t + Mathf.Lerp(0.9f, 0.4f, build);
                _auraKick = t;
                Pulse(_from, 2.2f * _scale * (0.7f + 0.5f * build), 0.5f);
            }
            if (debris != null)
            {
                debris.transform.position = _from + Vector3.up * 0.05f;
                debris.transform.localScale = Vector3.one * (_scale * 0.8f);
                _debrisAcc += dt * 7f * (0.3f + build);
                int b = (int)_debrisAcc;
                if (b > 0) { debris.Emit(b); _debrisAcc -= b; }
            }
        }

        /// <summary>
        /// Round the aura: the spell's own flourish (energy streaming in, rings turning round the caster, a fountain of
        /// embers, a spiral of sparks, or a column of light), and whatever the spell's power adds (ChargeTiers).
        /// </summary>
        private void AnimateCharge(float t, float dt)
        {
            bool winding = t >= _tCast && t < _tUnl;
            float build = Build(t);
            Vector3 chest = _from + Vector3.up * (1.2f * _body);
            // Just before a big spell goes, everything is drawn in.
            float crush = _tier >= 3 ? Mathf.Clamp01((t - (_tUnl - 0.5f)) / 0.5f) : 0f;
            // Energy streams in for a STREAM flourish, and under every other one for big spells (4+ pips).
            bool stream = _charge == CardDatabase.CHARGE_STREAM || _pips >= 4;
            if (winding && stream && chargeAura != null)
            {
                chargeAura.transform.position = chest;
                chargeAura.transform.localScale = Vector3.one * _scale;
                float rate = (25f + 14f * _vis) * (_charge == CardDatabase.CHARGE_STREAM ? 1f : 0.45f);
                _auraAcc += dt * rate * (0.4f + build) * (1f + 3f * crush);
                int n = (int)_auraAcc;
                if (n > 0) { chargeAura.Emit(n); _auraAcc -= n; }
            }
            if (gyroRings != null)
                for (int i = 0; i < gyroRings.Length; i++)
                {
                    Transform g = gyroRings[i];
                    if (g == null) continue;
                    bool on = (_charge == CardDatabase.CHARGE_GYRO || _tier >= 4) && t >= _tCast && t < _tUnl + 0.3f;
                    // A HALO display: the same rings spin up round the spell's subject, and stay until the attack.
                    bool halo = _cine && _show == CardDatabase.SHOW_HALO && t >= _tDisp && t < _tBar + 0.25f;
                    if (g.gameObject.activeSelf != (on || halo)) g.gameObject.SetActive(on || halo);
                    if (halo)
                    {
                        float hs = (1.3f + 0.1f * _vis) * Mathf.Max(1f, _body * 0.8f) * Ease((t - _tDisp) / 0.35f) * (1f - Ease((t - _tBar) / 0.25f));
                        g.position = Focus();
                        g.rotation = i == 0 ? Quaternion.Euler(70f, t * 420f * _flip, 0f) : Quaternion.Euler(-55f, -t * 340f * _flip, 35f);
                        g.localScale = new Vector3(Mathf.Max(0.01f, hs), 1f, Mathf.Max(0.01f, hs));
                        continue;
                    }
                    if (!on) continue;
                    float s = (1.5f + 0.12f * _vis) * _body * Ease((t - _tCast) / 0.4f) * (1f - Ease((t - _tUnl) / 0.3f)) * (1f - 0.25f * crush);
                    float turn = 0.6f + 1.2f * build;
                    g.position = chest;
                    g.rotation = i == 0 ? Quaternion.Euler(70f, t * 200f * turn * _flip, 0f) : Quaternion.Euler(-55f, -t * 160f * turn * _flip, 35f);
                    g.localScale = new Vector3(Mathf.Max(0.01f, s), 1f, Mathf.Max(0.01f, s));
                }
            bool fountain = _charge == CardDatabase.CHARGE_FOUNTAIN;
            if (winding && (fountain || _tier >= 3) && embers != null)
            {
                embers.transform.position = _from + Vector3.up * 0.1f;
                embers.transform.localScale = Vector3.one * _scale;
                _emberAcc += dt * (30f + 10f * _vis) * (0.5f + build) * (fountain ? 1f : 0.4f);
                int n = (int)_emberAcc;
                if (n > 0) { embers.Emit(n); _emberAcc -= n; }
            }
            if (winding && _charge == CardDatabase.CHARGE_SPIRAL && portalBurst != null)
            {
                _spiralAcc += dt * 50f;
                while (_spiralAcc >= 1f)
                {
                    _spiralAcc -= 1f;
                    for (int s = 0; s < 2; s++)
                    {
                        float ang = (t * 540f * _flip + s * 180f) * Mathf.Deg2Rad;
                        float h = ((t - _tCast) * 2.4f) % 2.4f;
                        Vector3 p = _from + (_right * Mathf.Cos(ang) + _fwd * Mathf.Sin(ang)) * (0.9f * _scale) + Vector3.up * (h * _body);
                        Burst(portalBurst, p, 1);
                    }
                }
            }
            if (castPillar != null)
            {
                // A COLUMN flourish from early on; any big spell stands in a thinner one for the back half of its charge.
                bool column = _charge == CardDatabase.CHARGE_COLUMN;
                float t0 = _tCast + (_tUnl - _tCast) * (column ? 0.3f : 0.55f);
                bool on = (column || _tier >= 3) && t >= t0 && t < _tUnl + 0.45f;
                // An ASCEND display: a column of light through the spell's subject.
                bool ascend = _cine && _show == CardDatabase.SHOW_ASCEND && t >= _tDisp && t < _tExp + 0.3f;
                if (castPillar.gameObject.activeSelf != (on || ascend)) castPillar.gameObject.SetActive(on || ascend);
                if (ascend)
                {
                    Vector3 under = Focus();
                    float rise = Ease((t - _tDisp) / 0.3f);
                    float thin = 1f - Ease((t - _tExp) / 0.3f);
                    float aw = (0.8f + 0.1f * _vis) * thin * (1f + 0.08f * Mathf.Sin(t * 22f));
                    castPillar.position = new Vector3(under.x, _ground, under.z);
                    castPillar.localScale = new Vector3(Mathf.Max(0.01f, aw), Mathf.Max(0.01f, rise * (5f + 0.8f * _vis)), Mathf.Max(0.01f, aw));
                    castPillar.rotation = Quaternion.Euler(0f, t * 90f, 0f);
                }
                else if (on)
                {
                    float grow = Ease((t - t0) / 0.5f);
                    float fade = 1f - Ease((t - _tUnl) / 0.45f);
                    float w = (1.1f + 0.12f * _vis) * _body * fade * (1f + 0.08f * Mathf.Sin(t * 22f)) * (column ? 1f : 0.6f);
                    castPillar.position = _from;
                    castPillar.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, grow * (4f + 0.8f * _vis)), Mathf.Max(0.01f, w));
                    castPillar.rotation = Quaternion.Euler(0f, t * 60f, 0f);
                }
            }
            if (winding && _tier >= 2) ChargeTiers(t, dt, build, chest);
        }

        /// <summary>
        /// What a spell's power adds to its charge. Tier 2: grit lifts off the floor, sparks race round the rune's rim and
        /// rings beat out from the caster, quicker and quicker. Tier 3: sparks spiral up round them and power crackles out
        /// of them. Tier 4: light pours down on them from the rune in the sky. (The dark and the sky's rune: AnimateStage.)
        /// </summary>
        private void ChargeTiers(float t, float dt, float build, Vector3 chest)
        {
            if (debris != null)
            {
                debris.transform.position = _from + Vector3.up * 0.05f;
                debris.transform.localScale = Vector3.one * (_scale * (0.8f + 0.2f * _tier));
                _debrisAcc += dt * (8f + 5f * _vis) * (0.3f + build);
                int n = (int)_debrisAcc;
                if (n > 0) { debris.Emit(n); _debrisAcc -= n; }
            }
            if (rimSparks != null)
            {
                _rimAngle += dt * (160f + 380f * build) * _flip;
                _rimAcc += dt * (30f + 30f * build);
                int points = Mathf.Min(4, _tier);
                float radius = 1.2f * _scale * Ease((t - _tCast) / 0.45f);
                while (_rimAcc >= 1f)
                {
                    _rimAcc -= 1f;
                    for (int k = 0; k < points; k++)
                    {
                        float a = (_rimAngle + k * 360f / points) * Mathf.Deg2Rad;
                        Burst(rimSparks, _from + (_right * Mathf.Cos(a) + _fwd * Mathf.Sin(a)) * radius + Vector3.up * 0.12f, 1);
                    }
                }
            }
            if (t >= _nextBeat)
            {
                _nextBeat = t + Mathf.Lerp(0.95f, 0.32f, build);
                _auraKick = t;
                Pulse(_from, (2.4f + 0.5f * _tier) * _scale * (0.7f + 0.5f * build), 0.6f);
                // From 4 pips each beat throws a ring of sparks along the floor too.
                if (_pips >= 4) Shock(_from + Vector3.up * 0.12f, (int)((8 + 2 * _vis) * (0.4f + build)), 0.4f + 0.15f * _tier);
            }
            if (_tier < 3) return;
            if (vortex != null)
            {
                vortex.transform.position = _from + Vector3.up * 0.1f;
                vortex.transform.localScale = Vector3.one * _scale;
                _vortexAcc += dt * (18f + 6f * _vis) * (0.3f + build);
                int n = (int)_vortexAcc;
                if (n > 0) { vortex.Emit(n); _vortexAcc -= n; }
            }
            if (arcs != null && t >= _nextArc)
            {
                _nextArc = t + Mathf.Lerp(0.6f, 0.1f, build) * (0.6f + 0.8f * Random.value);
                arcs.transform.position = chest;
                arcs.transform.localScale = Vector3.one * _scale;
                arcs.Emit(4 + 2 * _tier);
                _arcFlash = t;
            }
            if (_tier < 4 || skyfall == null) return;
            skyfall.transform.position = _from + Vector3.up * SkyHeight();
            skyfall.transform.localScale = Vector3.one * _scale;
            _skyAcc += dt * (20f + 60f * build);
            int m = (int)_skyAcc;
            if (m > 0) { skyfall.Emit(m); _skyAcc -= m; }
        }

        private float SkyHeight() { return 8f + 1.5f * _body; }

        /// <summary>
        /// The stage, for big spells. Tier 3 and up: everything but the magic goes dark as the charge builds (a veil in the
        /// school's colour that lifts for a flash on the release and each hit) and clears in the afterglow. Tier 4: the
        /// school's rune draws itself in the sky over the ring, flares with each jolt, and fades after the hit.
        /// </summary>
        private void AnimateStage(float t)
        {
            if (duskDome != null)
            {
                bool on = _tier >= 3 && t >= _tCast && t < _tAfter;
                if (duskDome.gameObject.activeSelf != on) duskDome.gameObject.SetActive(on);
                if (on)
                {
                    float deep = _tier >= 4 ? 0.7f : 0.5f;
                    float a = deep * Mathf.Min(Mathf.Pow(Build(t), 0.7f), 1f - Ease((t - (_tAfter - 0.8f)) / 0.8f));
                    float flash = Mathf.Exp(-Mathf.Max(0f, t - _kickAt) * 5f);
                    Color c = Color.Lerp(Color.black, _color, 0.12f + 0.45f * flash);
                    c.a = a * (1f - 0.4f * flash);
                    duskDome.position = _from;
                    if (_duskMat != null) _duskMat.SetColor("_Color", c);
                }
            }
            if (skySigil != null)
            {
                bool on = _tier >= 4 && t >= _tCast && t < _tAfter;
                if (skySigil.gameObject.activeSelf != on) skySigil.gameObject.SetActive(on);
                if (on)
                {
                    float open = Ease((t - _tCast) / 0.8f);
                    float close = Mathf.Clamp01(1f - Ease((t - (_tStrike + 0.3f)) / 0.9f));
                    float s = 7.5f * _scale * Mathf.Min(open, 0.6f + 0.4f * close);
                    skySigil.position = _from + Vector3.up * SkyHeight();
                    skySigil.rotation = Quaternion.Euler(0f, -_runeAngle * 0.5f, 0f);
                    skySigil.localScale = new Vector3(Mathf.Max(0.01f, s), Mathf.Max(0.01f, s), Mathf.Max(0.01f, s));
                    if (_skyMat != null)
                    {
                        float draw = Mathf.Clamp01((t - _tCast) / Mathf.Max(0.5f, (_tUnl - _tCast) * 0.8f));
                        _skyMat.SetFloat("_Reveal", draw < 1f ? draw : 1.05f);
                        _skyMat.SetFloat("_Flare", Mathf.Max(0f, 2.2f - Mathf.Max(0f, t - _kickAt) * 2.5f));
                        _skyMat.SetFloat("_Fade", close);
                    }
                }
            }
        }

        /// <summary>
        /// The school's light: it builds round the caster (flickering as power crackles out of a big one), sits with the
        /// core or follows the volley in the air through the unleash, flares on the hit and dies away.
        /// </summary>
        private void AnimateLight(float t)
        {
            if (fxLight == null || !fxLight.gameObject.activeSelf) return;
            float peak = 1.4f + 0.35f * _vis;
            if (t < _tUnl)
            {
                float flick = 0.5f * Mathf.Exp(-Mathf.Max(0f, t - _arcFlash) * 9f);
                fxLight.transform.position = _from + Vector3.up * (1.6f * _body);
                fxLight.intensity = peak * (Ease(Build(t)) + flick);
                fxLight.range = 6f * _scale * (1f + 0.25f * (_tier - 1));
            }
            else if (t < _tStrike)
            {
                bool flying = _special != 0 || (_v >= 0 && _vFlown && t < _tHit);
                fxLight.transform.position = flying ? _headPos : _origin;
                fxLight.intensity = peak * (1f + 1.2f * Mathf.Exp(-Mathf.Max(0f, t - _kickAt) * 4f));
            }
            else
            {
                float k = t - _tStrike;
                float flare = _impact == CardDatabase.IMPACT_NOVA ? 4f : 2.2f;
                fxLight.transform.position = _impactAt + Vector3.up * 1.4f;
                fxLight.intensity = (peak * flare) * Mathf.Exp(-k * 2.5f);
                fxLight.range = (6f + 1.2f * _vis) * (_aoe ? 1.4f : 1f);
            }
        }

        /// <summary>The hum of the charge (tier 2 and up): played so that it ends on the release, louder for bigger spells.</summary>
        private void StartHum()
        {
            if (chargeAudio == null || chargeSound == null || !_cine) return;
            float len = chargeSound.length;
            float charge = _tUnl - _tCast;
            chargeAudio.Stop();
            chargeAudio.transform.position = _from + Vector3.up * 1.2f;
            chargeAudio.clip = chargeSound;
            chargeAudio.volume = 0.2f + 0.2f * _tier;
            chargeAudio.Play();
            // Only its last 'charge' seconds are heard.
            if (len > charge + 0.05f) chargeAudio.time = len - charge;
        }

        // ------------------------------------------------------------------ the film

        /// <summary>
        /// The film, act by act: the charge (watchers only: the caster sees it with their own eyes), the surge, the power
        /// taking shape, then each volley as it gathers, flies and lands (the waves from one shoulder then the other, each
        /// travelling its own way; the finisher the spell's own way), and a wide turn round the ring as it settles.
        /// </summary>
        private void Film(float t)
        {
            if (!_filmOn || cutscenePlayer == null || t < _tCast || t >= _tAfter) return;
            if (!cutscenePlayer.IsFilming()) { _filmOn = false; return; }
            _camShake = 0f;
            if (t < _tUnl) ChargeShot(t);
            else if (t >= _tExp && t < _tBar) ExpendShot(t);
            else if (_special == CardDatabase.SPECIAL_WYRM) WyrmShot(t);
            else if (t < _tDisp) ArrivalShot(t);
            else if (t < _tExp) DisplayShot(t);
            else VolleyShot(t);
            if (_friendly) _camShake *= 0.15f;
            Vector3 pos = _camPos;
            if (_camShake > 0.0001f) pos += new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 53f + 1.3f), Mathf.Sin(t * 61f + 2.1f)) * _camShake;
            Vector3 d = _camLook - pos;
            if (d.sqrMagnitude < 0.0001f) d = _fwd;
            cutscenePlayer.SetFilmShot(pos, Quaternion.LookRotation(d, Vector3.up));
        }

        /// <summary>How far back the film stands: further for a big caster, and for a big spell.</summary>
        private float CamK()
        {
            return Mathf.Max(1f, _body * 0.9f) * (0.85f + 0.15f * _scale / Mathf.Max(1f, _body * 0.8f));
        }

        /// <summary>What the film looks at over the caster: the core, or the creature beside them.</summary>
        private Vector3 Focus()
        {
            return _creature ? _summonAt + Vector3.up * (1.1f * _body) : _corePos;
        }

        /// <summary>
        /// The charge. Big spells open wide and high (the dark coming down, the sparks round the rune), the biggest then
        /// look up from the floor past the caster at the rune in the sky; every spell ends circling the caster, drawing in
        /// closer as the power gathers, shaking as a big one nears its release.
        /// </summary>
        private void ChargeShot(float t)
        {
            Vector3 up = Vector3.up;
            float bodyK = CamK();
            float w = Build(t);
            if (_tier >= 3 && w < 0.3f)
            {
                float k = w / 0.3f;
                Vector3 round = Quaternion.Euler(0f, (-40f + 30f * k) * _flip, 0f) * (_fwd * (8.5f * bodyK));
                _camPos = _from + round + up * (5.2f - 1.2f * k);
                _camLook = _from + up * (2.2f * _body);
            }
            else if (_tier >= 4 && w < 0.5f)
            {
                float k = (w - 0.3f) / 0.2f;
                Vector3 round = Quaternion.Euler(0f, (150f + 25f * k) * _flip, 0f) * (_fwd * (3.4f * bodyK));
                _camPos = _from + round + up * 0.35f;
                _camLook = _from + up * (3.2f + 2.2f * k);
            }
            else
            {
                float w0 = _tier >= 4 ? 0.5f : _tier >= 3 ? 0.3f : 0f;
                float k = Mathf.Clamp01((w - w0) / (1f - w0));
                Vector3 round = Quaternion.Euler(0f, (35f + 70f * k) * _flip, 0f) * (_fwd * (4.2f - 1.4f * k) * bodyK);
                // It sinks as it closes in, to look up at the caster through the flames.
                _camPos = _from + round + up * ((0.55f + 1.1f * (1f - k)) * _body);
                _camLook = _from + up * ((1.25f + 0.3f * k) * _body);
            }
            // The picture shakes more as the aura builds, and jumps with each of its beats.
            _camShake = (0.003f * _tier + (_tier >= 3 ? 0.012f * (_tier - 2) : 0f)) * w * w
                        + 0.02f * (0.4f + 0.2f * _tier) * Mathf.Exp(-Mathf.Max(0f, t - _auraKick) * 8f);
        }

        /// <summary>The arrival: close on the caster as it bursts out of them, thrown back by it and turning up to what arrives.</summary>
        private void ArrivalShot(float t)
        {
            Vector3 up = Vector3.up;
            float bodyK = CamK();
            float k = Mathf.Clamp01((t - _tUnl) / Mathf.Max(0.05f, _tDisp - _tUnl));
            float e = 1f - (1f - k) * (1f - k);
            _camPos = _from + _fwd * ((2.0f + 2.4f * e) * bodyK) + _right * (0.9f * _flip * bodyK) + up * ((0.7f + 0.7f * e) * _body);
            _camLook = Vector3.Lerp(_from + up * (1.35f * _body), ArrivePos(SubjectHome(), k) + (_creature ? up * (1.0f * _body) : Vector3.zero), 0.55f * e);
            _camShake = 0.03f * (1f + 0.5f * _tier) * Mathf.Exp(-(t - _tUnl) * 5f);
        }

        /// <summary>
        /// The display, in three shots: round the caster and up to what has arrived; then close under the spell's subject,
        /// turning slowly round it as it shows off; then back and high behind the caster, the subject over them and the
        /// target beyond, closing in as it peaks. Each of its beats jolts the picture.
        /// </summary>
        private void DisplayShot(float t)
        {
            Vector3 up = Vector3.up;
            float bodyK = CamK();
            float k = Mathf.Clamp01((t - _tDisp) / Mathf.Max(0.05f, _tExp - _tDisp));
            Vector3 focus = Focus();
            if (k < 0.3f)
            {
                float e = Ease(k / 0.3f);
                Vector3 round = Quaternion.Euler(0f, (120f - 55f * e) * _flip, 0f) * (_fwd * (3.8f * bodyK));
                _camPos = _from + round + up * ((1.0f + 1.0f * e) * _body);
                _camLook = Vector3.Lerp(_from + up * (1.3f * _body), focus, 0.4f + 0.5f * e);
            }
            else if (k < 0.68f)
            {
                float e = (k - 0.3f) / 0.38f;
                Vector3 round = Quaternion.Euler(0f, (-35f + 80f * e) * _flip, 0f) * (_fwd * ((2.3f + 0.25f * _tier) * bodyK));
                _camPos = new Vector3(focus.x, Mathf.Max(_ground + 0.4f, focus.y - 0.9f * _body), focus.z) + round;
                _camLook = focus + up * (0.15f * _body);
            }
            else
            {
                float e = Ease((k - 0.68f) / 0.32f);
                Vector3 to = _aim - _from;
                to.y = 0f;
                Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : _fwd;
                Vector3 side = Vector3.Cross(up, dir) * _flip;
                _camPos = _from - dir * ((5.6f - 1.2f * e) * bodyK) + side * (1.6f * bodyK) + up * ((2.9f - 0.5f * e) * _body);
                _camLook = Vector3.Lerp(focus, _aim + up * 1.2f, 0.3f + 0.15f * e);
            }
            _camShake = 0.02f * (0.5f + 0.25f * _tier) * Mathf.Exp(-Mathf.Max(0f, t - _showKick) * 6f);
        }

        /// <summary>
        /// The blades, traps and shields going: from the side, back far enough to hold the caster's seat (their blades
        /// circle it) and the target (its traps and shields), drifting in.
        /// </summary>
        private void ExpendShot(float t)
        {
            Vector3 up = Vector3.up;
            float k = Mathf.Clamp01((t - _tExp) / Mathf.Max(0.05f, _tBar - _tExp));
            Vector3 mid = (_seatPos + _aim) * 0.5f;
            Vector3 along = _aim - _seatPos;
            along.y = 0f;
            float span = along.magnitude;
            Vector3 side = span > 0.5f ? Vector3.Cross(up, along / span) * _flip : _right * _flip;
            _camPos = mid + side * (4.5f + span * 0.75f - 0.8f * k) + up * (2.6f + span * 0.12f);
            _camLook = mid + up * 1.3f;
        }

        /// <summary>The volley whose slot it is: it gathers, it flies, it lands; after the finisher's hit, the wide turn.</summary>
        private void VolleyShot(float t)
        {
            Vector3 up = Vector3.up;
            Vector3 mid = (_from + _aim) * 0.5f;
            int k = Mathf.Clamp(_v, 0, _vCount - 1);
            bool final = k == _vCount - 1;
            float start = _vStart[k], fly = _vFly[k], hit = _vHit[k];
            // Waves are shot from one side then the other, each with the next kind of travelling shot; the finisher the spell's way.
            float side = final ? _flip : ((k & 1) == 0 ? -_flip : _flip);
            int cam = final ? _cam : (_cam + 1 + k) % 5;
            float bodyK = CamK();
            Vector3 focus = Focus();
            if (t < fly)
            {
                float a = Mathf.Clamp01((t - start) / Mathf.Max(0.05f, fly - start));
                if (!final)
                {
                    // A wave gathers: over the caster's shoulder, the target beyond.
                    _camPos = _from - _fwd * (2.7f * bodyK) + _right * (1.15f * side * bodyK) + up * ((1.9f + 0.25f * a) * _body);
                    _camLook = Vector3.Lerp(focus, _aim + up * 1.0f, 0.3f + 0.3f * a);
                    return;
                }
                // The finisher gathers: the spell's own shot of its caster, pushing in as the core swells and shudders.
                if (cam == CardDatabase.CAM_LOW)
                {
                    _camPos = _from + _fwd * (2.3f * _body) + _right * (0.9f * side) + up * 0.35f;
                    _camLook = Vector3.Lerp(_from + up * (1.5f * _body), focus, 0.6f);
                }
                else if (cam == CardDatabase.CAM_CRANE)
                {
                    _camPos = _from + up * ((5.2f - 0.8f * a) * _body) + _fwd * (1.6f * _body) + _right * (1.0f * side);
                    _camLook = _from + up * (1.4f * _body);
                }
                else if (cam == CardDatabase.CAM_REVERSE)
                {
                    _camPos = _aim + _fwd * 3.2f + _right * (1.2f * side) + up * 1.6f;
                    _camLook = Vector3.Lerp(_from + up * (1.4f * _body), focus, 0.5f);
                }
                else
                {
                    _camPos = _from + _fwd * ((3.4f - 0.9f * a) * _body) + _right * (1.3f * side * _body) + up * (1.25f * _body);
                    _camLook = Vector3.Lerp(_from + up * (1.35f * _body), focus, 0.55f);
                }
                _camShake = 0.006f * _tier * a * a;
                return;
            }
            if (t < hit)
            {
                // In the air.
                float u = Mathf.Clamp01((t - fly) / Mathf.Max(0.05f, hit - fly));
                if (cam == CardDatabase.CAM_LOW)
                {
                    _camPos = mid + _right * (2.4f * side) + up * 0.3f;
                    _camLook = _headPos;
                }
                else if (cam == CardDatabase.CAM_CRANE)
                {
                    _camPos = mid + up * (9f - 2f * u) - _fwd * 1.5f;
                    _camLook = _headPos;
                }
                else if (cam == CardDatabase.CAM_FOLLOW)
                {
                    _camPos = _headPos - _headDir * 2.3f + up * 0.7f;
                    _camLook = _headPos + _headDir * 2.5f;
                }
                else if (cam == CardDatabase.CAM_REVERSE)
                {
                    _camPos = _aim + _fwd * 3.2f + _right * (1.2f * side) + up * 1.6f;
                    _camLook = _headPos;
                }
                else
                {
                    _camPos = _from - _fwd * 2.2f + _right * (0.85f * side) + up * 1.85f + _fwd * (0.8f * u);
                    _camLook = Vector3.Lerp(_from + _fwd * 2f + up * 1.2f, _aim + up * 1.0f, Ease(u));
                }
                return;
            }
            float r = t - hit;
            // The result: the film holds on the target until the exit.
            float hold = final ? Mathf.Max(0.3f, _tExit - _tStrike) : 100f;
            if (r < hold)
            {
                // It lands: on the target, shaken (a wave's lightly).
                Vector3 look = _aim + up * 1.0f;
                Vector3 pos;
                if (cam == CardDatabase.CAM_LOW) { pos = _aim - _fwd * 2.6f + _right * (1.4f * side) + up * 0.3f; look = _aim + up * 1.6f; }
                else if (cam == CardDatabase.CAM_CRANE) { pos = _aim + up * 5f - _fwd * 2.5f + _right * 1.5f; look = _aim; }
                else if (cam == CardDatabase.CAM_FOLLOW) pos = _aim - _headDir * 3.2f + up * 1.4f;
                else if (cam == CardDatabase.CAM_REVERSE) pos = _aim + _fwd * 2.4f + _right * (1.6f * side) + up * 1.4f;
                else pos = _aim - _fwd * 3.6f + _right * (2.6f * side) + up * 1.5f;
                // An area spell: back far enough to keep every target (and the number over it) in the shot.
                if (_aoe) pos += (pos - look).normalized * (1.5f + 0.8f * Mathf.Max(0, _targetCount - 1));
                _camPos = pos;
                _camLook = look;
                _camShake = 0.035f * (1f + 0.6f * _pips) * Mathf.Exp(-r * 6f) * (final ? (_impact == CardDatabase.IMPACT_NOVA ? 1.6f : 1f) : 0.4f);
                // Each aftershock jolts it again.
                if (final) _camShake += 0.03f * Mathf.Exp(-Mathf.Max(0f, t - _kickAt) * 8f);
                return;
            }
            // Wide, turning slowly round the ring as it settles (low, high or at eye height to match the shots).
            float w = r - hold;
            float dist = Vector3.Distance(_from, _aim);
            float radius = Mathf.Max(6f, dist * 0.9f);
            float height = cam == CardDatabase.CAM_LOW ? 1.2f : cam == CardDatabase.CAM_CRANE ? 7f : 3.8f;
            Quaternion turn = Quaternion.Euler(0f, wideTurnDegrees * w * _flip, 0f);
            _camPos = mid + turn * (_right * (radius * _flip) - _fwd * 1.5f) + up * height;
            _camLook = mid + up * 0.8f;
        }

        /// <summary>
        /// Sunfire Wyrm's own shots (Tools/preview_sunfire_wyrm.py draws the film from these). The arrival: before the
        /// caster, off to the side the dragon swings away from, so it comes out at the lens. The display: wide, drifting
        /// round against its twirls as they climb; then low and to one side as it sweeps down behind the caster and
        /// rears up; then low before the caster, looking up at them both, pushing in on its roar. The attack: wide
        /// (one target: side on, the caster at one end of the picture and the target at the other, its loop over them
        /// in full; several: from high behind the caster, the whole far side in the picture), following its head a
        /// little; then its fire, from beyond whoever it burns, looking back past them at it and the caster under it.
        /// The hit: wide again, shaken; then the wide turn round the ring as it burns away.
        /// </summary>
        private void WyrmShot(float t)
        {
            Vector3 up = Vector3.up;
            float bodyK = CamK();
            Vector3 side0 = _right * _flip;      // the side it first turns towards
            Vector3 head = _headPos;
            if (t < _tDisp)
            {
                // Out of the caster.
                float k = Mathf.Clamp01((t - _tUnl) / Mathf.Max(0.05f, _tDisp - _tUnl));
                float e = 1f - (1f - k) * (1f - k);
                _camPos = _from + _fwd * ((2.4f + 2.0f * e) * bodyK) - side0 * (1.5f * bodyK) + up * ((1.15f + 0.35f * e) * _body);
                _camLook = Vector3.Lerp(_from + up * (1.3f * _body), head, 0.35f + 0.3f * e);
                _camShake = 0.03f * (1f + 0.5f * _tier) * Mathf.Exp(-(t - _tUnl) * 5f);
                return;
            }
            if (t < _tExp)
            {
                float k = Mathf.Clamp01((t - _tDisp) / Mathf.Max(0.05f, _tExp - _tDisp));
                if (k < 0.5f)
                {
                    // The twirls: wide, drifting round against them, so its coils cross the picture as they climb.
                    float e = k / 0.5f;
                    float a = (-20f + 60f * e) * Mathf.Deg2Rad;
                    _camPos = _from + (_fwd * Mathf.Cos(a) - side0 * Mathf.Sin(a)) * ((5.2f - 0.4f * e) * bodyK) + up * ((1.2f + 0.7f * e) * _body + 0.3f * e);
                    _camLook = _from + up * (1.45f * _body + 0.6f * e);
                }
                else if (k < 0.8f)
                {
                    // Down behind them and up: from in front and to one side, low, the caster before it.
                    float e = Ease((k - 0.5f) / 0.3f);
                    _camPos = _from + _fwd * ((5.2f - 0.8f * e) * bodyK) - side0 * ((2.6f - 0.6f * e) * bodyK) + up * ((1.3f - 0.3f * e) * _body);
                    _camLook = Vector3.Lerp(_from + up * (1.7f * _body), Vector3.Lerp(_from + up * (1.5f * _body), _wyrmRestAt, 0.6f), e);
                }
                else
                {
                    // At rest over their shoulder, and the roar: from low before the caster, looking up at them both.
                    float roar = Mathf.Clamp01((t - _wyrmRoar) / 0.3f);
                    _camPos = _from + _fwd * ((3.4f - 0.5f * roar) * bodyK) - side0 * (1.1f * bodyK) + up * (0.75f * _body);
                    _camLook = Vector3.Lerp(_from + up * (1.5f * _body), _wyrmRestAt, 0.6f);
                    if (t >= _wyrmRoar) _camShake = 0.012f;
                }
                _camShake += 0.02f * (0.5f + 0.25f * _tier) * Mathf.Exp(-Mathf.Max(0f, t - _showKick) * 6f);
                return;
            }
            Vector3 toward = _wyrmToward;
            Vector3 side = _wyrmSide;
            float dist = _wyrmDist;
            float spread = _wyrmSpread;
            Vector3 widePos, wideLook;
            if (spread < 0.5f)
            {
                // One target: side on, the caster at one end of the picture and the target at the other.
                Vector3 mid = _from + toward * (0.5f * dist);
                widePos = mid - side * (0.75f * dist + 2.2f) - toward * 0.8f + up * 2.6f;
                wideLook = mid + up * 1.9f;
            }
            else
            {
                // Several: from high behind the caster, the whole of the far side in the picture.
                widePos = _from - toward * (4.6f + 0.43f * spread) + up * (3.2f + 0.5f * spread);
                wideLook = _from + toward * (0.5f * dist + 1.1f) + up * 0.6f;
            }
            if (t < _wyrmHover - 0.1f)
            {
                // It goes, through them and round: the wide shot, following it a little.
                float follow = 0.2f + 0.25f * Ease((t - _tBar) / Mathf.Max(0.2f, _wyrmHover - _tBar));
                _camPos = widePos;
                _camLook = Vector3.Lerp(wideLook, head, follow);
                _camShake = 0.02f * Mathf.Exp(-Mathf.Max(0f, t - _kickAt) * 6f);
                return;
            }
            if (t < _tStrike)
            {
                // The fire: from beyond whoever it burns, looking back past them at it and the caster under it.
                Vector3 aim = wyrm.BreathAim(t);
                if (spread < 0.5f)
                {
                    float big = Mathf.Max(1f, _tScale * 0.8f);
                    _camPos = wyrm.TargetAt(0) + (toward * 1.6f - side * 2.6f) * big + up * 0.6f;
                    _camLook = Vector3.Lerp(_wyrmHoverAt, aim, 0.35f);
                }
                else
                {
                    _camPos = _from + toward * (0.55f * dist) + side * (0.9f * spread + 3f) + up * 3.4f;
                    _camLook = Vector3.Lerp(_wyrmHoverAt, aim, 0.3f);
                }
                _camShake = 0.01f + 0.02f * Mathf.Exp(-Mathf.Max(0f, t - _kickAt) * 6f);
                return;
            }
            float r = t - _tStrike;
            float hold = Mathf.Max(0.3f, _tExit - _tStrike);
            if (r < hold)
            {
                // It lands: wide again, every one of them burning, shaken.
                _camPos = widePos + up * 0.4f - toward * 0.6f;
                _camLook = wideLook;
                _camShake = 0.035f * (1f + 0.6f * _pips) * Mathf.Exp(-r * 6f) * 1.6f + 0.03f * Mathf.Exp(-Mathf.Max(0f, t - _kickAt) * 8f);
                return;
            }
            // Wide, turning slowly round the ring as it burns away (VolleyShot's last shot).
            float w = r - hold;
            Vector3 centre = (_from + _aim) * 0.5f;
            float radius = Mathf.Max(6f, Vector3.Distance(_from, _aim) * 0.9f);
            float height = _cam == CardDatabase.CAM_LOW ? 1.2f : _cam == CardDatabase.CAM_CRANE ? 7f : 3.8f;
            Quaternion turn = Quaternion.Euler(0f, wideTurnDegrees * w * _flip, 0f);
            _camPos = centre + turn * (_right * (radius * _flip) - _fwd * 1.5f) + up * height;
            _camLook = centre + up * 0.8f;
        }

        // ------------------------------------------------------------------ helpers

        private void SetFade(float f)
        {
            if (fadeSphere == null) return;
            bool on = f > 0.001f;
            if (fadeSphere.gameObject.activeSelf != on) fadeSphere.gameObject.SetActive(on);
            if (!on || fadeRenderer == null) return;
            if (_fadeMat == null) _fadeMat = fadeRenderer.material;
            _fadeMat.SetFloat("_Fade", f);
            _fadeMat.SetColor("_Glow", _color);
        }

        private void Burst(ParticleSystem ps, Vector3 at, int count)
        {
            if (ps == null || count <= 0) return;
            ps.transform.position = at;
            ps.Emit(count);
        }

        private void Sound(AudioClip clip, Vector3 at)
        {
            SoundAt(clip, at, 1f);
        }

        private void SoundAt(AudioClip clip, Vector3 at, float volume)
        {
            if (sfx == null || clip == null) return;
            sfx.transform.position = at;
            sfx.PlayOneShot(clip, volume);
        }

        /// <summary>Every part in the spell's school colours, and the school's rune on the floor and the stamp.</summary>
        private void Recolor()
        {
            Swap(sparkRenderers, sparkMats);
            Swap(glowRenderers, glowMats);
            Swap(flameRenderers, flameMats);
            Swap(ringRenderers, ringMats);
            Swap(pillarRenderers, pillarMats);
            Swap(coreRenderers, coreMats);
            if (runeRenderer != null)
            {
                if (_runeMat == null) _runeMat = runeRenderer.material;
                StyleGlyph(_runeMat);
            }
            if (sigilRenderer != null)
            {
                if (_sigilMat == null) _sigilMat = sigilRenderer.material;
                StyleGlyph(_sigilMat);
                _sigilMat.SetFloat("_Reveal", 1.05f);
            }
            if (skySigilRenderer != null)
            {
                if (_skyMat == null) _skyMat = skySigilRenderer.material;
                StyleGlyph(_skyMat);
            }
            if (duskRenderer != null && _duskMat == null) _duskMat = duskRenderer.material;
            // The aura: the school's colour, white-hot at its root; the outer shell paler and plainer.
            if (auraShellRenderer != null)
            {
                if (_auraMat == null) _auraMat = auraShellRenderer.material;
                _auraMat.SetColor("_Color", _color);
                _auraMat.SetColor("_Core", Color.Lerp(_color, Color.white, 0.8f));
                _auraMat.SetFloat("_Power", 0f);
            }
            if (auraShellOuterRenderer != null)
            {
                if (_auraOuterMat == null) _auraOuterMat = auraShellOuterRenderer.material;
                _auraOuterMat.SetColor("_Color", Color.Lerp(_color, Color.white, 0.3f));
                _auraOuterMat.SetColor("_Core", _color);
                _auraOuterMat.SetFloat("_Power", 0f);
            }
        }

        private void StyleGlyph(Material m)
        {
            if (glyphDrawTex != null && _look < glyphDrawTex.Length && glyphDrawTex[_look] != null) m.SetTexture("_MainTex", glyphDrawTex[_look]);
            m.SetColor("_Color", _color);
            if (glyphEdgeColors != null && _look < glyphEdgeColors.Length) m.SetColor("_EdgeColor", glyphEdgeColors[_look]);
            // 0 = General, 1-7 the schools, 9 = Tech (the GlyphDraw shader).
            m.SetFloat("_Style", _look == 7 ? 0f : _look + 1f);
            m.SetFloat("_Reveal", 0f);
            m.SetFloat("_Fade", 1f);
            m.SetFloat("_Flare", 0f);
        }

        private void Swap(Renderer[] rs, Material[] mats)
        {
            if (rs == null || mats == null || mats.Length == 0) return;
            Material m = mats[Mathf.Min(_look, mats.Length - 1)];
            if (m == null) return;
            for (int i = 0; i < rs.Length; i++) if (rs[i] != null && rs[i].sharedMaterial != m) rs[i].sharedMaterial = m;
        }

        private void HideRig()
        {
            if (runeCircle != null) runeCircle.gameObject.SetActive(false);
            if (castPillar != null) castPillar.gameObject.SetActive(false);
            if (impactFlash != null) impactFlash.gameObject.SetActive(false);
            if (impactPillar != null) impactPillar.gameObject.SetActive(false);
            if (fxLight != null) fxLight.gameObject.SetActive(false);
            if (beam != null) beam.gameObject.SetActive(false);
            if (sigilStamp != null) sigilStamp.gameObject.SetActive(false);
            if (powerCore != null) powerCore.gameObject.SetActive(false);
            if (duskDome != null) duskDome.gameObject.SetActive(false);
            if (skySigil != null) skySigil.gameObject.SetActive(false);
            if (auraShell != null) auraShell.gameObject.SetActive(false);
            if (auraShellOuter != null) auraShellOuter.gameObject.SetActive(false);
            if (chargeAudio != null && chargeAudio.isPlaying) chargeAudio.Stop();
            if (wyrm != null) wyrm.Hide();
            HideAll(shockRings);
            HideAll(heads);
            HideAll(portals);
            HideAll(spikes);
            HideAll(gyroRings);
            _headCount = 0;
        }

        private void HideAll(Transform[] ts)
        {
            if (ts == null) return;
            for (int i = 0; i < ts.Length; i++) if (ts[i] != null && ts[i].gameObject.activeSelf) ts[i].gameObject.SetActive(false);
        }
    }
}
