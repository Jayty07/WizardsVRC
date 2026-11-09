using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Casting a spell by drawing its rune (local, one shared by every duel circle). A spell under 100% accuracy isn't sent
    /// the moment it's picked: a board of glowing points along the school's symbol (the same one the caster draws in the
    /// air, Tools/make_glyph_draw.py, without its seal ring) opens in front of you, and you draw it. How you draw it
    /// depends on the wand you have equipped (its casting style, ItemDatabase.castStyleItems):
    ///
    ///   Wand     (any ordinary wand)   VR: take the wand from your hip and touch each point with its tip, in order.
    ///                                  Desktop: click the points in order.
    ///   Blade    (Runecutter Saber)    the rune's strokes (point to point) are cut one at a time. VR: draw the sabre and
    ///                                  slash it through the board along each glowing stroke, fast. Desktop: swing your
    ///                                  view fast across each stroke.
    ///   Sidearm  (Dominion Sidearm)    the points pop up one at a time as targets that fade. VR: draw the pistol (aim
    ///                                  laser) and pull the trigger on each. Desktop: click each before it fades. A
    ///                                  missed shot eats into the target's time.
    ///   Baton    (Conductor's Baton)   a metronome counts in, then each point comes due on its own beat. VR: touch it
    ///                                  with the baton on the beat. Desktop: click it on the beat. Early or late misses.
    ///   Lantern  (Wisp Lantern)        a wisp runs the rune's path. VR: keep the lantern in its glow. Desktop: keep your
    ///                                  view on it. The share of the run spent in its glow is the quality.
    ///
    /// More pips and a harder spell (lower accuracy) mean more points, smaller targets and less time. When it's drawn, time
    /// runs out or the round is about to start, the share drawn is the pattern's quality, and BattleClient sends the pick
    /// with it: BattleManager takes that share of patternFizzleCut (90%) off the spell's chance to fizzle.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CastPattern : UdonSharpBehaviour
    {
        public const int MAX_NODES = 10;
        public const int STYLE_WAND = 0;
        public const int STYLE_BLADE = 1;
        public const int STYLE_PISTOL = 2;
        public const int STYLE_BATON = 3;
        public const int STYLE_LANTERN = 4;
        public const int STYLE_COUNT = 5;
        private const int MAX_PATH = 128;

        [Header("Board (a world canvas on the Walkthrough layer, clickable)")]
        public Transform board;
        [Tooltip("Canvas units from the middle of the board to its edge, for the symbol's 0-1 texture coordinates.")]
        public float boardUnits = 230f;
        [Tooltip("The board's scale in VR (m per canvas unit) and how far in front of your chest it opens.")]
        public float vrScale = 0.0011f;
        public float vrDistance = 0.48f;
        public float vrDrop = 0.24f;
        [Tooltip("Desktop: bigger, a little further, about eye level.")]
        public float desktopScale = 0.0013f;
        public float desktopDistance = 0.8f;
        public float desktopDrop = 0.05f;
        [Tooltip("A glowing point per node (children of the board).")]
        public Transform[] nodes;
        public Renderer[] nodeRenderers;
        [Tooltip("Desktop: a numbered button on each point (events Dot0 - Dot9).")]
        public GameObject[] dotButtons;
        public Text[] dotLabels;
        public Material nodeIdleMat;
        public Material nodeNextMat;
        public Material nodeMissMat;
        [Tooltip("Done points take the school's colour (indexed by School, the last for General).")]
        public Material[] schoolMats;
        [Tooltip("The symbol, faint (a guide) and bright (drawn so far), on the WizardsVRC/GlyphDraw shader.")]
        public Renderer ghostGlyph;
        public Renderer drawnGlyph;
        public Texture2D[] glyphDrawTex;
        public Color[] glyphColors;
        public Color[] glyphEdgeColors;
        [Tooltip("A faint line through every point, and a bright one through those done (and on to the wand tip).")]
        public LineRenderer guideLine;
        public LineRenderer traceLine;
        public Text titleLabel;
        public Text promptLabel;
        public Image timerFill;

        [Header("Pen path per symbol (Editor/GlyphPenData.cs, same as BattleClient's)")]
        public float[] penU;
        public float[] penV;
        public int[] penJump;
        public int penSamples = 64;

        [Header("Wand (VR): a local pickup holstered at your hip while a pattern is up")]
        public Transform wand;
        public Transform wandTip;
        public VRC_Pickup wandPickup;
        public Renderer wandGem;
        public ParticleSystem tipSparks;
        [Tooltip("Holster at your left hip instead (the book's Settings: Wand hand).")]
        public bool leftHip;
        public float hipSide = 0.17f;
        public float hipForward = 0.07f;
        public float hipDrop = 0.04f;

        [Header("Casting styles (the wand pickup holds a model per style; the sidearm is a pickup of its own)")]
        [Tooltip("Children of the wand pickup, by style (Wand, Blade, -, Baton, Lantern); only the one in use is shown.")]
        public GameObject[] styleModels;
        [Tooltip("The working end of each model: the wand's gem, the sabre's point, the baton's tip, the lantern's flame.")]
        public Transform[] styleTips;
        [Tooltip("Parts tinted in the spell's school colour, by style (the gem, the sabre's runes, the lantern's flame...).")]
        public Renderer[] styleGems;
        [Tooltip("The wand pickup's grab box (resized for each model).")]
        public BoxCollider wandCollider;
        [Tooltip("Blade: the hilt end of the sabre's blade (the cut is where the blade crosses the board).")]
        public Transform bladeBase;
        [Tooltip("Sidearm: its own pickup (Auto Hold, the trigger fires: PatternWand relays it), its muzzle and its cell.")]
        public Transform pistol;
        public Transform pistolMuzzle;
        public VRC_Pickup pistolPickup;
        public Renderer pistolGem;
        [Tooltip("The sidearm's aim laser and shot tracer, the sabre's slash trail (world space).")]
        public LineRenderer fxLine;
        [Tooltip("Lantern: the wisp that runs the rune (a child of the board).")]
        public Transform wisp;
        [Tooltip("Desktop: where your view meets the board (a child of the board), for the blade and the lantern.")]
        public Transform crosshair;
        public AudioClip slashSound;
        public AudioClip shotSound;
        public AudioClip beatSound;
        public AudioClip wispSound;
        [Tooltip("Blade: how fast the cut must move across the board (m/s), in VR and on desktop (view swing).")]
        public float bladeSpeed = 0.9f;
        public float desktopSlashSpeed = 0.45f;
        [Tooltip("Blade: how far off a stroke's line a cut may pass (m), before scaling for the spell's pips.")]
        public float bladeTolerance = 0.075f;
        [Tooltip("Sidearm: how long each target lasts (s).")]
        public float pistolTargetSeconds = 1.8f;
        public float desktopTargetSeconds = 1.4f;
        [Tooltip("Baton: seconds a beat, and how early or late a touch may be (s).")]
        public float batonBeat = 0.8f;
        public float batonWindow = 0.24f;
        [Tooltip("Lantern: seconds of the wisp's run for each point, and how long it waits before setting off.")]
        public float lanternSecondsPerNode = 0.6f;
        public float lanternLead = 1.2f;

        [Header("Difficulty")]
        [Tooltip("Points: 2, plus one per pip, plus one per this much accuracy under 100%, from 3 to MAX_NODES.")]
        public int accuracyPerNode = 15;
        [Tooltip("How close the wand tip must come to a point (m), before scaling for the spell's pips.")]
        public float hitRadius = 0.06f;
        [Tooltip("How far in front of or behind the board the tip may be (m).")]
        public float hitDepth = 0.15f;
        public float vrGrabSeconds = 2.5f;
        public float vrNodeSeconds = 0.9f;
        public float desktopNodeSeconds = 0.75f;
        public float baseSeconds = 1.5f;

        [Header("Sound")]
        public AudioSource sfx;
        public AudioClip grabSound;
        public AudioClip tickSound;
        public AudioClip missSound;
        public AudioClip doneSound;
        public AudioClip failSound;

        private BattleClient _caller;
        private bool _active;
        private bool _vr;
        private bool _held;
        private int _style;
        private int _school;
        private int _glyph;
        private int _count;
        private int _targets;
        private int _next;
        private int _hits;
        private int _quality;
        private float _radius;
        private float _start;
        private float _limit;
        private float _hideAt = -1f;
        private Vector3[] _local = new Vector3[MAX_NODES];
        private float[] _reveal = new float[MAX_NODES];
        private int[] _nodeK = new int[MAX_NODES];
        private bool[] _hit = new bool[MAX_NODES];
        private Vector3[] _lineBuf = new Vector3[MAX_NODES + 2];
        private Material _ghostMat;
        private Material _drawnMat;
        private Transform _tip;
        // Where you're pointing on the board (board units, z = 0): Pointer() sets it.
        private Vector3 _ptr;
        private bool _hadPtr;
        private Vector3 _prevPtr;
        private float _prevPtrTime;
        // Blade: the slash under way.
        private bool _slashing;
        private Vector3 _slashFrom;
        private float _fxUntil;
        // Sidearm / baton / lantern: their clocks start once you're ready (VR: the tool in hand).
        private bool _engaged;
        private float _targetStart;
        private float _targetLife;
        private float _beat;
        private float _beatStart;
        private int _lastTick = -99;
        private float _wispDur;
        private float _wispStart;
        private float _onTime;
        private float _runTime;
        private float _segOn;
        private float _segAll;
        private int _pathN;
        private Vector3[] _path = new Vector3[MAX_PATH];
        private bool[] _pathJump = new bool[MAX_PATH];
        private Vector3 _wispPos;
        private bool _wispGone;

        public bool IsActive() { return _active; }

        /// <summary>Share of the rune drawn (0-100), once the pattern is done.</summary>
        public int Quality() { return _quality; }

        /// <summary>How many points a spell's pattern has.</summary>
        public int NodeCount(int pips, int accuracy)
        {
            int n = 2 + Mathf.Max(0, pips) + Mathf.Max(0, 100 - accuracy) / Mathf.Max(1, accuracyPerNode);
            return Mathf.Clamp(n, 3, MAX_NODES);
        }

        private void Start()
        {
            if (board != null) board.gameObject.SetActive(false);
            if (wand != null) wand.gameObject.SetActive(false);
            if (pistol != null) pistol.gameObject.SetActive(false);
            if (fxLine != null) fxLine.gameObject.SetActive(false);
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
        /// Opens the board for a spell of 'school' (-1 = General) costing 'pips' at 'accuracy' (with the caster's bonuses),
        /// drawn in casting style 'style' (STYLE_*, from the equipped wand); 'maxSeconds' is how long the round still gives.
        /// Calls caller._PatternDone() when it's finished.
        /// </summary>
        public void Begin(BattleClient caller, int school, int pips, int accuracy, float maxSeconds, string spellName, int style)
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || board == null) return;
            _caller = caller;
            _vr = me.IsUserInVR();
            _style = style >= 0 && style < STYLE_COUNT ? style : STYLE_WAND;
            _school = school;
            int symbols = glyphDrawTex != null ? glyphDrawTex.Length : 8;
            _glyph = LookOf(school, symbols);
            _count = NodeCount(pips, accuracy);
            _targets = _style == STYLE_BLADE ? _count - 1 : _count;
            _next = 0;
            _hits = 0;
            _quality = 0;
            _held = false;
            _hideAt = -1f;
            _hadPtr = false;
            _slashing = false;
            _fxUntil = 0f;
            _engaged = false;
            _lastTick = -99;
            _onTime = 0f;
            _runTime = 0f;
            _segOn = 0f;
            _segAll = 0f;
            _wispGone = false;
            // Bigger spells: smaller targets, less time.
            int p = Mathf.Clamp(pips, 0, 7);
            _radius = hitRadius * (1.15f - 0.05f * p);
            float speedUp = 1f - 0.03f * p;
            float grab = _vr ? vrGrabSeconds : 0f;
            _targetLife = (_vr ? pistolTargetSeconds : desktopTargetSeconds) * speedUp;
            _beat = batonBeat * speedUp;
            _wispDur = lanternSecondsPerNode * _count * speedUp;
            float perNode = _vr ? vrNodeSeconds : desktopNodeSeconds * speedUp;
            if (_style == STYLE_PISTOL) _limit = baseSeconds + grab + _targetLife * _count;
            else if (_style == STYLE_BATON) _limit = baseSeconds + grab + _beat * (_count + 2.5f);
            else if (_style == STYLE_LANTERN) _limit = baseSeconds + grab + lanternLead + _wispDur;
            else if (_style == STYLE_BLADE) _limit = baseSeconds + grab + perNode * 1.15f * _targets;
            else _limit = baseSeconds + perNode * _count + grab;
            if (maxSeconds > 0f && _limit > maxSeconds)
            {
                // Not enough of the round left: everything runs quicker to fit.
                float fit = Mathf.Max(2f, maxSeconds) / _limit;
                _targetLife *= fit;
                _beat *= fit;
                _wispDur *= fit;
                _limit = Mathf.Max(2f, maxSeconds);
            }
            _start = Time.time;
            for (int i = 0; i < MAX_NODES; i++) _hit[i] = false;

            PlaceNodes();
            PlacePath();
            PlaceBoard(me);
            StyleBoard(spellName);
            board.gameObject.SetActive(true);
            ReadyTool(me);
            if (wisp != null)
            {
                wisp.gameObject.SetActive(_style == STYLE_LANTERN);
                _wispPos = _pathN > 0 ? _path[0] : _local[0];
                wisp.localPosition = new Vector3(_wispPos.x, _wispPos.y, -16f);
            }
            if (crosshair != null) crosshair.gameObject.SetActive(false);
            if (fxLine != null) fxLine.gameObject.SetActive(false);
            _active = true;
            // Desktop: no tool to pick up, so the clocks start now (after the lantern's wait).
            if (!_vr) Engage();
            ShowNodes();
        }

        /// <summary>Closes the board without a result (the round moved on, or you left the fight).</summary>
        public void Cancel()
        {
            _active = false;
            _caller = null;
            HideAll();
        }

        /// <summary>The board's Cancel button: the card goes back into your hand.</summary>
        public void CancelPattern()
        {
            if (!_active) return;
            BattleClient c = _caller;
            _active = false;
            _caller = null;
            HideAll();
            if (c != null) c._PatternCancelled();
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Spreads the points evenly along the symbol's strokes (not its seal ring, the last stroke).</summary>
        private void PlaceNodes()
        {
            int n = penSamples;
            int b = _glyph * n;
            bool ok = PenOk();
            int last = LastSample();
            for (int i = 0; i < _count; i++)
            {
                float f = _count > 1 ? i / (float)(_count - 1) : 0f;
                int k = Mathf.Clamp(Mathf.RoundToInt(f * last), 0, n - 1);
                Vector2 uv = ok ? new Vector2(penU[b + k], penV[b + k]) : new Vector2(0.5f + 0.4f * Mathf.Cos(f * 6.283f), 0.5f + 0.4f * Mathf.Sin(f * 6.283f));
                _local[i] = new Vector3((uv.x - 0.5f) * 2f * boardUnits, (uv.y - 0.5f) * 2f * boardUnits, -12f);
                _nodeK[i] = k;
                // How much of the symbol is drawn once this point is reached (GlyphDraw _Reveal runs over the seal too).
                _reveal[i] = n > 1 ? k / (float)(n - 1) : 1f;
            }
        }

        private bool PenOk()
        {
            int n = penSamples;
            int b = _glyph * n;
            return penU != null && penV != null && n >= 2 && b + n <= penU.Length && b + n <= penV.Length;
        }

        /// <summary>The last pen sample before the seal ring (the symbol's own strokes end there).</summary>
        private int LastSample()
        {
            int n = penSamples;
            int b = _glyph * n;
            int last = n - 1;
            if (PenOk() && penJump != null)
                for (int k = n - 2; k >= 0; k--)
                    if (b + k < penJump.Length && penJump[b + k] != 0) { last = k; break; }
            return last;
        }

        /// <summary>The wisp's run (lantern): the pen path up to the seal, and where the pen lifts between strokes.</summary>
        private void PlacePath()
        {
            _pathN = 0;
            if (!PenOk())
            {
                for (int i = 0; i < _count && i < MAX_PATH; i++) { _path[i] = _local[i]; _pathJump[i] = false; _pathN++; }
                return;
            }
            int n = penSamples;
            int b = _glyph * n;
            int last = LastSample();
            for (int k = 0; k <= last && k < MAX_PATH; k++)
            {
                _path[k] = new Vector3((penU[b + k] - 0.5f) * 2f * boardUnits, (penV[b + k] - 0.5f) * 2f * boardUnits, 0f);
                // A jump after sample k: the pen lifts to start the next stroke there.
                _pathJump[k] = penJump != null && b + k < penJump.Length && penJump[b + k] != 0;
                _pathN++;
            }
        }

        /// <summary>In front of your chest (VR, within reach) or of your eyes (desktop), facing you.</summary>
        private void PlaceBoard(VRCPlayerApi me)
        {
            VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 fwd = head.rotation * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = me.GetRotation() * Vector3.forward;
            fwd.Normalize();
            float dist = _vr ? vrDistance : desktopDistance;
            float drop = _vr ? vrDrop : desktopDrop;
            // The sabre needs room to swing: its board opens a little further out.
            if (_vr && _style == STYLE_BLADE) dist += 0.12f;
            board.SetPositionAndRotation(head.position + fwd * dist + Vector3.down * drop, Quaternion.LookRotation(fwd, Vector3.up));
            float sc = _vr ? vrScale : desktopScale;
            board.localScale = new Vector3(sc, sc, sc);
        }

        private void StyleBoard(string spellName)
        {
            Color col = glyphColors != null && _glyph < glyphColors.Length ? glyphColors[_glyph] : Color.white;
            Color edge = glyphEdgeColors != null && _glyph < glyphEdgeColors.Length ? glyphEdgeColors[_glyph] : Color.white;
            Texture2D tex = glyphDrawTex != null && _glyph < glyphDrawTex.Length ? glyphDrawTex[_glyph] : null;
            if (ghostGlyph != null)
            {
                if (_ghostMat == null) _ghostMat = ghostGlyph.material;
                if (tex != null) _ghostMat.SetTexture("_MainTex", tex);
                _ghostMat.SetColor("_Color", col);
                _ghostMat.SetColor("_EdgeColor", edge);
                _ghostMat.SetFloat("_Style", _glyph == 7 ? 0f : _glyph + 1f);
                _ghostMat.SetFloat("_Reveal", 1.05f);
                _ghostMat.SetFloat("_Fade", 0.16f);
                _ghostMat.SetFloat("_Flare", 0f);
            }
            if (drawnGlyph != null)
            {
                if (_drawnMat == null) _drawnMat = drawnGlyph.material;
                if (tex != null) _drawnMat.SetTexture("_MainTex", tex);
                _drawnMat.SetColor("_Color", col);
                _drawnMat.SetColor("_EdgeColor", edge);
                _drawnMat.SetFloat("_Style", _glyph == 7 ? 0f : _glyph + 1f);
                _drawnMat.SetFloat("_Reveal", 0f);
                _drawnMat.SetFloat("_Fade", 1f);
                _drawnMat.SetFloat("_Flare", 0f);
            }
            if (titleLabel != null)
            {
                titleLabel.text = spellName;
                titleLabel.color = Color.Lerp(col, Color.white, 0.35f);
            }
            if (promptLabel != null) promptLabel.text = Prompt(false);
            if (guideLine != null)
            {
                guideLine.positionCount = _count;
                for (int i = 0; i < _count; i++) guideLine.SetPosition(i, board.TransformPoint(_local[i]));
                guideLine.gameObject.SetActive(_style != STYLE_PISTOL);
            }
            if (traceLine != null)
            {
                traceLine.positionCount = 0;
                traceLine.gameObject.SetActive(true);
            }
            if (timerFill != null) timerFill.fillAmount = 1f;
        }

        /// <summary>What to do, for the style (and once the tool is in hand).</summary>
        private string Prompt(bool inHand)
        {
            string hip = (leftHip ? "left" : "right") + " hip";
            switch (_style)
            {
                case STYLE_BLADE:
                    if (!_vr) return "Swing your view fast across each glowing stroke, in order";
                    return inHand ? "Slash through each glowing stroke, fast, in order" : "Draw your sabre from your " + hip + " and slash each glowing stroke";
                case STYLE_PISTOL:
                    if (!_vr) return "Click each target before it fades";
                    return inHand ? "Shoot each target before it fades" : "Draw the sidearm from your " + hip + " and shoot each target";
                case STYLE_BATON:
                    if (!_vr) return "Click each point on its beat";
                    return inHand ? "Touch each point on its beat" : "Take your baton from your " + hip + ": touch each point on its beat";
                case STYLE_LANTERN:
                    if (!_vr) return "Keep your view on the wisp as it runs the rune";
                    return inHand ? "Keep the lantern in the wisp's glow" : "Take your lantern from your " + hip + " and follow the wisp";
            }
            if (!_vr) return "Click the points in order: " + PointList();
            return inHand ? "Touch the glowing points in order" : "Take your wand from your " + hip + " and touch the points in order";
        }

        private string PointList()
        {
            string s = "";
            for (int i = 0; i < _count; i++) s += (i > 0 ? " " : "") + (i + 1).ToString();
            return s;
        }

        // ------------------------------------------------------------------ the tool

        /// <summary>Shows the style's tool (VR) at your hip, tinted for the school: the sidearm, or the wand pickup's model.</summary>
        private void ReadyTool(VRCPlayerApi me)
        {
            Material gemMat = schoolMats != null && schoolMats.Length > 0 ? schoolMats[Mathf.Min(_glyph, schoolMats.Length - 1)] : null;
            bool gun = _style == STYLE_PISTOL;
            if (pistol != null) pistol.gameObject.SetActive(_vr && gun);
            if (wand != null) wand.gameObject.SetActive(_vr && !gun);
            if (styleModels != null)
                for (int i = 0; i < styleModels.Length; i++)
                    if (styleModels[i] != null) styleModels[i].SetActive(i == _style || (gun && i == STYLE_WAND));
            _tip = gun ? pistolMuzzle : wandTip;
            if (!gun && styleTips != null && _style < styleTips.Length && styleTips[_style] != null) _tip = styleTips[_style];
            if (gemMat != null)
            {
                if (wandGem != null) wandGem.sharedMaterial = gemMat;
                if (pistolGem != null) pistolGem.sharedMaterial = gemMat;
                if (styleGems != null)
                    for (int i = 0; i < styleGems.Length; i++) if (styleGems[i] != null) styleGems[i].sharedMaterial = gemMat;
            }
            if (wandCollider != null)
            {
                // The grab box fits the model: a long one for the sabre.
                float len = _style == STYLE_BLADE ? 0.95f : _style == STYLE_BATON ? 0.48f : _style == STYLE_LANTERN ? 0.46f : 0.4f;
                wandCollider.size = new Vector3(0.06f, 0.06f, len);
                wandCollider.center = new Vector3(0f, 0f, len * 0.5f - 0.12f);
            }
            if (_vr) Holster(me);
        }

        /// <summary>The tool waits at your hip until you take it (the sabre hangs point down and back, like a scabbard).</summary>
        private void Holster(VRCPlayerApi me)
        {
            Transform t = _style == STYLE_PISTOL ? pistol : wand;
            if (t == null) return;
            Vector3 hips = me.GetBonePosition(HumanBodyBones.Hips);
            Quaternion rot = me.GetRotation();
            if (hips.sqrMagnitude < 0.0001f)
            {
                float eye = me.GetAvatarEyeHeightAsMeters();
                hips = me.GetPosition() + Vector3.up * ((eye > 0.2f ? eye : 1.6f) * 0.55f);
            }
            Vector3 side = rot * Vector3.right * (leftHip ? -1f : 1f);
            Vector3 at = hips + side * hipSide + rot * Vector3.forward * hipForward + Vector3.down * hipDrop;
            float pitch = _style == STYLE_BLADE ? 150f : _style == STYLE_PISTOL ? 90f : 70f;
            t.SetPositionAndRotation(at, rot * Quaternion.Euler(pitch, 0f, leftHip ? 12f : -12f));
        }

        /// <summary>The style's clocks start: the sidearm's first target, the baton's count-in, the wisp's wait.</summary>
        private void Engage()
        {
            if (_engaged) return;
            _engaged = true;
            float now = Time.time;
            _targetStart = now + (_vr ? 0.3f : 0.6f);
            _beatStart = now + _beat * 2f;   // two beats of count-in, then point 1
            _lastTick = -99;
            _wispStart = now + (_vr ? 0.8f : lanternLead);
        }

        private void Haptic(float duration, float amplitude)
        {
            if (!_vr) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            VRC_Pickup pk = _style == STYLE_PISTOL ? pistolPickup : wandPickup;
            if (pk == null || !pk.IsHeld) return;
            me.PlayHapticEventInHand(pk.currentHand, duration, amplitude, 320f);
        }

        private void Play(AudioClip clip, float pitch)
        {
            if (sfx == null || clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip);
        }

        // ------------------------------------------------------------------ the points

        /// <summary>
        /// The points: the next one pulses bright (the blade's next stroke lights both its ends), done ones glow in the
        /// school colour, missed ones go grey.
        /// </summary>
        private void ShowNodes()
        {
            if (nodes == null) return;
            Material done = schoolMats != null && schoolMats.Length > 0 ? schoolMats[Mathf.Min(_glyph, schoolMats.Length - 1)] : nodeIdleMat;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                bool on = i < _count;
                if (nodes[i].gameObject.activeSelf != on) nodes[i].gameObject.SetActive(on);
                bool btn = on && !_vr && DesktopButton(i);
                if (dotButtons != null && i < dotButtons.Length && dotButtons[i] != null && dotButtons[i].activeSelf != btn) dotButtons[i].SetActive(btn);
                if (!on) continue;
                nodes[i].localPosition = _local[i];
                if (dotButtons != null && i < dotButtons.Length && dotButtons[i] != null)
                {
                    RectTransform rt = dotButtons[i].GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(_local[i].x, _local[i].y);
                }
                if (dotLabels != null && i < dotLabels.Length && dotLabels[i] != null) dotLabels[i].text = (i + 1).ToString();
                int state = NodeState(i);
                Material m = state == 1 ? nodeNextMat : state == 2 ? done : state == 3 ? nodeMissMat : nodeIdleMat;
                if (nodeRenderers != null && i < nodeRenderers.Length && nodeRenderers[i] != null && m != null && nodeRenderers[i].sharedMaterial != m)
                    nodeRenderers[i].sharedMaterial = m;
            }
        }

        /// <summary>Desktop: which points have a button (all for the wand and baton, the live target for the sidearm, none to swing or follow).</summary>
        private bool DesktopButton(int i)
        {
            if (_style == STYLE_BLADE || _style == STYLE_LANTERN) return false;
            if (_style == STYLE_PISTOL) return _active && i == _next && _engaged;
            return true;
        }

        /// <summary>0 waiting, 1 next, 2 done, 3 missed.</summary>
        private int NodeState(int i)
        {
            if (_style == STYLE_BLADE)
            {
                // Point i ends stroke i - 1 and starts stroke i.
                bool before = i - 1 >= 0 && i - 1 < _next, after = i < _next && i < _targets;
                if ((before && _hit[i - 1]) || (after && _hit[i])) return 2;
                if (_active && (i == _next || i == _next + 1) && _next < _targets) return 1;
                if (before || after) return 3;
                return 0;
            }
            if (_style == STYLE_LANTERN)
            {
                if (i < _next) return _hit[i] ? 2 : 3;
                return 0;
            }
            if (i < _next) return _hit[i] ? 2 : 3;
            return i == _next ? 1 : 0;
        }

        // ------------------------------------------------------------------ playing

        private void Update()
        {
            if (_hideAt > 0f && Time.time >= _hideAt) { _hideAt = -1f; if (!_active) HideAll(); }
            if (fxLine != null && fxLine.gameObject.activeSelf && Time.time > _fxUntil && !(_active && _style == STYLE_PISTOL && _held))
                fxLine.gameObject.SetActive(false);
            if (!_active) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            float left = _limit - (Time.time - _start);
            if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(left / Mathf.Max(0.1f, _limit));
            if (_vr && !_held) Holster(me);
            switch (_style)
            {
                case STYLE_BLADE: UpdateBlade(); break;
                case STYLE_PISTOL: UpdatePistol(); break;
                case STYLE_BATON: UpdateBaton(); break;
                case STYLE_LANTERN: UpdateLantern(); break;
                default: if (_vr && _held) TraceWand(); break;
            }
            if (!_active) return;
            AnimateNodes();
            UpdateTrace();
            if (_active && left <= 0f) Finish();
        }

        /// <summary>
        /// Where you're pointing on the board, into _ptr (board units, flat on it); false when nowhere. VR: the tool's tip
        /// near the board (the sabre: where its blade crosses the board). Desktop: where your view meets it.
        /// </summary>
        private bool Pointer()
        {
            float sc = Mathf.Max(0.00001f, board.lossyScale.x);
            float edge = boardUnits * 1.35f;
            if (_vr)
            {
                if (!_held || _tip == null) return false;
                if (_style == STYLE_BLADE && bladeBase != null)
                {
                    Vector3 a = board.InverseTransformPoint(bladeBase.position), b = board.InverseTransformPoint(_tip.position);
                    if ((a.z > 0f) == (b.z > 0f)) return false;
                    float t = a.z / (a.z - b.z);
                    _ptr = Vector3.Lerp(a, b, t);
                }
                else
                {
                    Vector3 lp = board.InverseTransformPoint(_tip.position);
                    if (Mathf.Abs(lp.z * sc) > hitDepth) return false;
                    _ptr = lp;
                }
            }
            else
            {
                VRCPlayerApi me = Networking.LocalPlayer;
                VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 o = board.InverseTransformPoint(head.position);
                Vector3 d = board.InverseTransformDirection(head.rotation * Vector3.forward);
                if (Mathf.Abs(d.z) < 0.0001f) return false;
                float t = -o.z / d.z;
                if (t <= 0f) return false;
                _ptr = o + d * t;
            }
            _ptr.z = 0f;
            return Mathf.Abs(_ptr.x) < edge && Mathf.Abs(_ptr.y) < edge;
        }

        /// <summary>The wand tip touching the next point (or the one after it: the skipped one counts as missed).</summary>
        private void TraceWand()
        {
            if (_tip == null || _next >= _count) return;
            Sparks(_tip.position, 1);
            float sc = Mathf.Max(0.00001f, board.lossyScale.x);
            Vector3 lp = board.InverseTransformPoint(_tip.position);
            if (Mathf.Abs(lp.z * sc) > hitDepth) return;
            if (Near(lp, _next, sc, 1f)) { Reach(_next, true); return; }
            if (_next + 1 < _count && Near(lp, _next + 1, sc, 1f))
            {
                Reach(_next, false);
                Reach(_next, true);
            }
        }

        private bool Near(Vector3 lp, int i, float sc, float grow)
        {
            float dx = (lp.x - _local[i].x) * sc, dy = (lp.y - _local[i].y) * sc;
            float r = _radius * grow;
            return dx * dx + dy * dy <= r * r;
        }

        private void Sparks(Vector3 at, int n)
        {
            if (tipSparks == null) return;
            tipSparks.transform.position = at;
            tipSparks.Emit(n);
        }

        // ---- blade

        /// <summary>
        /// A slash is a run of fast movement across the board (the blade's cut in VR, your view on desktop). Whenever the
        /// slash so far lies along the next stroke and covers it end to end, that stroke is cut (or the one after it, and
        /// the skipped one is lost); the slash then carries on from there, so one long sweep can cut a few strokes.
        /// </summary>
        private void UpdateBlade()
        {
            float now = Time.time;
            float sc = Mathf.Max(0.00001f, board.lossyScale.x);
            bool has = Pointer();
            if (_vr && _held && _tip != null) Sparks(_tip.position, 1);
            ShowCrosshair(has && !_vr);
            if (!has)
            {
                if (_slashing) _slashing = false;
                _hadPtr = false;
                return;
            }
            float need = _vr ? bladeSpeed : desktopSlashSpeed;
            float speed = 0f;
            if (_hadPtr)
            {
                float dt = Mathf.Max(0.001f, now - _prevPtrTime);
                speed = (_ptr - _prevPtr).magnitude * sc / dt;
            }
            if (!_slashing)
            {
                if (_hadPtr && speed >= need)
                {
                    _slashing = true;
                    _slashFrom = _prevPtr;
                }
            }
            else if (speed < need * 0.3f) _slashing = false;
            if (_slashing)
            {
                ShowFx(board.TransformPoint(new Vector3(_slashFrom.x, _slashFrom.y, -14f)), board.TransformPoint(new Vector3(_ptr.x, _ptr.y, -14f)), 0.15f);
                if (_next < _targets)
                {
                    if (Cuts(_slashFrom, _ptr, _next, sc))
                    {
                        CutHit(_next);
                        _slashFrom = _ptr;
                    }
                    else if (_next + 1 < _targets && Cuts(_slashFrom, _ptr, _next + 1, sc))
                    {
                        Reach(_next, false);
                        CutHit(_next);
                        _slashFrom = _ptr;
                    }
                }
            }
            _prevPtr = _ptr;
            _prevPtrTime = now;
            _hadPtr = true;
        }

        private void CutHit(int k)
        {
            Play(slashSound, 0.95f + 0.05f * _hits);
            Haptic(0.12f, 0.8f);
            Reach(k, true);
        }

        /// <summary>Does the slash a..b (board units) lie along stroke k (point k to point k + 1) and cover it?</summary>
        private bool Cuts(Vector3 a, Vector3 b, int k, float sc)
        {
            Vector3 s = b - a;
            float len = s.magnitude;
            if (len * sc < 0.04f) return false;
            Vector3 u = s / len;
            Vector3 p = _local[k], q = _local[k + 1];
            p.z = 0f;
            q.z = 0f;
            Vector3 v = q - p;
            float vl = v.magnitude;
            if (vl < 0.001f) return true;
            if (Mathf.Abs(Vector3.Dot(u, v / vl)) < 0.8f) return false;
            float tol = Mathf.Max(bladeTolerance, _radius * 1.3f) / sc;
            Vector3 ap = p - a, aq = q - a;
            float tp = Vector3.Dot(ap, u), tq = Vector3.Dot(aq, u);
            float dp = (ap - u * tp).magnitude, dq = (aq - u * tq).magnitude;
            if (dp > tol || dq > tol) return false;
            float lo = -0.3f * len - tol, hi = 1.3f * len + tol;
            return tp >= lo && tp <= hi && tq >= lo && tq <= hi;
        }

        // ---- sidearm

        /// <summary>The live target fades: miss it and the next one pops up. The laser shows where you're aiming.</summary>
        private void UpdatePistol()
        {
            if (!_engaged || _next >= _count) return;
            float now = Time.time;
            if (_vr && _held && pistolMuzzle != null && now > _fxUntil)
            {
                // The aim laser: from the muzzle to the board (or a little way past it).
                Vector3 hitAt;
                Vector3 o = pistolMuzzle.position, d = pistolMuzzle.forward;
                Vector3 lo = board.InverseTransformPoint(o), ld = board.InverseTransformDirection(d);
                float t = Mathf.Abs(ld.z) > 0.0001f ? -lo.z / ld.z : -1f;
                hitAt = t > 0f ? board.TransformPoint(lo + ld * t) : o + d * 1.5f;
                ShowFx(o, hitAt, 0f);
            }
            if (now < _targetStart) return;
            if (now - _targetStart > _targetLife)
            {
                Reach(_next, false);
                _targetStart = now + 0.15f;
                ShowNodes();
            }
        }

        /// <summary>The sidearm's trigger (PatternWand relays OnPickupUseDown): a rune-shot along the barrel.</summary>
        public void _WandFire()
        {
            if (!_active || _style != STYLE_PISTOL || pistolMuzzle == null) return;
            Vector3 o = pistolMuzzle.position, d = pistolMuzzle.forward;
            Play(shotSound, 1f);
            Sparks(o, 14);
            Haptic(0.1f, 1f);
            Vector3 lo = board.InverseTransformPoint(o), ld = board.InverseTransformDirection(d);
            float t = Mathf.Abs(ld.z) > 0.0001f ? -lo.z / ld.z : -1f;
            if (t <= 0f) { ShowFx(o, o + d * 4f, 0.08f); return; }
            Vector3 hit = lo + ld * t;
            ShowFx(o, board.TransformPoint(hit), 0.08f);
            if (!_engaged || _next >= _count || Time.time < _targetStart) return;
            float sc = Mathf.Max(0.00001f, board.lossyScale.x);
            hit.z = 0f;
            if (Near(hit, _next, sc, 1.3f))
            {
                Reach(_next, true);
                _targetStart = Time.time + 0.12f;
                ShowNodes();
            }
            else
            {
                // A wild shot costs the target some of its time.
                _targetStart -= _targetLife * 0.3f;
                Play(missSound, 1.2f);
            }
        }

        // ---- baton

        /// <summary>The metronome ticks; each point is due on its beat and lost once its window passes.</summary>
        private void UpdateBaton()
        {
            if (!_engaged) return;
            float now = Time.time;
            // Ticks from the count-in on (beat -2), stronger on a point's beat.
            int beat = Mathf.FloorToInt((now - _beatStart) / Mathf.Max(0.05f, _beat) + 0.0001f);
            if (beat != _lastTick && beat >= -2 && beat < _count)
            {
                _lastTick = beat;
                Play(beatSound, beat < 0 ? 0.8f : 1f);
            }
            if (_vr && _held && _tip != null) Sparks(_tip.position, 1);
            if (_next >= _count) return;
            float due = _beatStart + _next * _beat;
            if (now > due + batonWindow)
            {
                Reach(_next, false);
                return;
            }
            if (_vr && _held && _tip != null && Mathf.Abs(now - due) <= batonWindow)
            {
                float sc = Mathf.Max(0.00001f, board.lossyScale.x);
                Vector3 lp = board.InverseTransformPoint(_tip.position);
                if (Mathf.Abs(lp.z * sc) <= hitDepth && Near(lp, _next, sc, 1.1f))
                {
                    Haptic(0.06f, 0.6f);
                    Reach(_next, true);
                }
            }
        }

        // ---- lantern

        /// <summary>
        /// The wisp runs the path at an even pace (jumping where the pen lifts); time spent with the lantern (or your view)
        /// in its glow counts. Each point it passes is lit if you kept up since the last one.
        /// </summary>
        private void UpdateLantern()
        {
            bool has = Pointer();
            ShowCrosshair(has && !_vr);
            if (_vr && _held && _tip != null) Sparks(_tip.position, 1);
            if (!_engaged) return;
            float now = Time.time;
            if (now < _wispStart)
            {
                if (wisp != null)
                {
                    float s = 30f + 8f * Mathf.Sin(now * 10f);
                    wisp.localScale = new Vector3(s, s, s);
                }
                return;
            }
            if (!_wispGone) { _wispGone = true; Play(wispSound, 1f); }
            int last = Mathf.Max(1, _pathN - 1);
            float f = Mathf.Clamp01((now - _wispStart) / Mathf.Max(0.1f, _wispDur));
            float at = f * last;
            int k = Mathf.Min(last - 1, Mathf.FloorToInt(at));
            float frac = at - k;
            bool jumping = k >= 0 && k < _pathN && _pathJump[k];
            _wispPos = jumping ? _path[Mathf.Min(k + 1, _pathN - 1)] : Vector3.Lerp(_path[k], _path[Mathf.Min(k + 1, _pathN - 1)], frac);
            if (wisp != null)
            {
                wisp.localPosition = new Vector3(_wispPos.x, _wispPos.y, -16f);
                float s = 34f + 6f * Mathf.Sin(now * 14f);
                wisp.localScale = new Vector3(s, s, s);
            }
            float dt = Time.deltaTime;
            if (!jumping)
            {
                float sc = Mathf.Max(0.00001f, board.lossyScale.x);
                bool on = has && (_ptr - new Vector3(_wispPos.x, _wispPos.y, 0f)).magnitude * sc <= _radius * 1.5f;
                _runTime += dt;
                _segAll += dt;
                if (on)
                {
                    _onTime += dt;
                    _segOn += dt;
                    if (_vr) Haptic(0.03f, 0.25f);
                }
            }
            if (_drawnMat != null) _drawnMat.SetFloat("_Reveal", Mathf.Lerp(0f, _reveal[_count - 1], f));
            // Points passed.
            while (_next < _count && at + 0.001f >= _nodeK[_next])
            {
                // The first point: at the wisp as it sets off; the rest: in its glow most of the way since the last.
                bool kept;
                if (_next == 0) kept = has && (_ptr - new Vector3(_local[0].x, _local[0].y, 0f)).magnitude * Mathf.Max(0.00001f, board.lossyScale.x) <= _radius * 2.5f;
                else kept = _segAll <= 0f || _segOn >= _segAll * 0.6f;
                _segOn = 0f;
                _segAll = 0f;
                ReachQuiet(_next, kept);
            }
            if (f >= 1f) Finish();
        }

        /// <summary>The lantern's points: lit or lost as the wisp passes, with a soft tick.</summary>
        private void ReachQuiet(int i, bool hit)
        {
            if (i != _next || _next >= _count) return;
            _hit[i] = hit;
            if (hit) _hits++;
            _next++;
            if (hit) Play(tickSound, 1f + 0.06f * _hits);
            ShowNodes();
        }

        private void ShowCrosshair(bool on)
        {
            if (crosshair == null) return;
            if (crosshair.gameObject.activeSelf != on) crosshair.gameObject.SetActive(on);
            if (on) crosshair.localPosition = new Vector3(_ptr.x, _ptr.y, -18f);
        }

        private void ShowFx(Vector3 a, Vector3 b, float hold)
        {
            if (fxLine == null) return;
            if (!fxLine.gameObject.activeSelf) fxLine.gameObject.SetActive(true);
            fxLine.positionCount = 2;
            fxLine.SetPosition(0, a);
            fxLine.SetPosition(1, b);
            if (hold > 0f) _fxUntil = Time.time + hold;
        }

        /// <summary>Target _next is done: hit (bright tick, higher each time) or missed.</summary>
        private void Reach(int i, bool hit)
        {
            if (!_active || i != _next || _next >= _targets) return;
            _hit[i] = hit;
            if (hit) _hits++;
            _next++;
            if (_style != STYLE_BLADE || !hit) Play(hit ? tickSound : missSound, hit ? 1f + 0.06f * _hits : 1f);
            if (_drawnMat != null)
            {
                // The blade's stroke k ends at point k + 1.
                int r = _style == STYLE_BLADE ? Mathf.Min(i + 1, _count - 1) : i;
                _drawnMat.SetFloat("_Reveal", _reveal[r]);
            }
            ShowNodes();
            if (_next >= _targets) Finish();
        }

        private void AnimateNodes()
        {
            if (nodes == null) return;
            float now = Time.time;
            float pulse = 1f + 0.25f * Mathf.Sin(now * 9f);
            for (int i = 0; i < _count && i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                float s = i < _next ? (_hit[i] ? 30f : 20f) : 24f;
                if (_style == STYLE_BLADE)
                {
                    int st = NodeState(i);
                    s = st == 1 ? 32f * pulse : st == 2 ? 30f : st == 3 ? 20f : 24f;
                }
                else if (_style == STYLE_PISTOL)
                {
                    if (i == _next)
                    {
                        // The target shrinks as it fades (and waits, small, before it pops up).
                        float age = now - _targetStart;
                        s = !_engaged || age < 0f ? 14f : Mathf.Lerp(46f, 18f, Mathf.Clamp01(age / Mathf.Max(0.1f, _targetLife)));
                    }
                    else if (i > _next) s = 10f;
                }
                else if (_style == STYLE_BATON)
                {
                    if (i == _next && _engaged)
                    {
                        // Swells as its beat comes, flashes on it.
                        float due = _beatStart + i * _beat;
                        float until = due - now;
                        s = Mathf.Abs(until) <= batonWindow ? 40f : Mathf.Lerp(40f, 18f, Mathf.Clamp01(until / Mathf.Max(0.05f, _beat * 2f)));
                    }
                }
                else if (_style == STYLE_LANTERN)
                {
                    if (i >= _next) s = 16f;
                }
                else if (i == _next) s = 34f * pulse;
                nodes[i].localScale = new Vector3(s, s, s);
            }
        }

        private void UpdateTrace()
        {
            if (traceLine == null) return;
            int n = 0;
            if (_style == STYLE_LANTERN)
            {
                for (int i = 0; i < _next && i < _count; i++) _lineBuf[n++] = board.TransformPoint(_local[i]);
                if (_engaged && Time.time >= _wispStart && n > 0) _lineBuf[n++] = board.TransformPoint(new Vector3(_wispPos.x, _wispPos.y, -12f));
            }
            else
            {
                int upTo = _style == STYLE_BLADE ? (_next > 0 ? _next + 1 : 0) : _next;
                for (int i = 0; i < upTo && i < _count; i++) _lineBuf[n++] = board.TransformPoint(_local[i]);
                bool tipLine = _style == STYLE_WAND || _style == STYLE_BATON;
                if (_vr && _held && tipLine && _tip != null && n > 0 && _next < _count) _lineBuf[n++] = _tip.position;
            }
            traceLine.positionCount = n;
            for (int i = 0; i < n; i++) traceLine.SetPosition(i, _lineBuf[i]);
        }

        private void Finish()
        {
            if (!_active) return;
            _active = false;
            if (_style == STYLE_LANTERN)
            {
                // Points the wisp never reached are lost; the quality is the share of its run spent in its glow.
                while (_next < _count) { _hit[_next] = false; _next++; }
                _quality = _runTime > 0f ? Mathf.Clamp(Mathf.RoundToInt(_onTime * 100f / _runTime), 0, 100) : 0;
            }
            else _quality = _targets > 0 ? Mathf.Clamp(_hits * 100 / _targets, 0, 100) : 0;
            bool perfect = _style == STYLE_LANTERN ? _quality >= 95 : _hits >= _targets;
            if (_drawnMat != null)
            {
                _drawnMat.SetFloat("_Reveal", perfect ? 1.05f : _drawnMat.GetFloat("_Reveal"));
                _drawnMat.SetFloat("_Flare", perfect ? 2.2f : 0f);
            }
            if (promptLabel != null)
            {
                string what = _style == STYLE_BLADE ? " strokes" : _style == STYLE_PISTOL ? " targets" : _style == STYLE_BATON ? " beats" : " points";
                if (perfect) promptLabel.text = "Perfect rune!";
                else if (_style == STYLE_LANTERN) promptLabel.text = "In the glow " + _quality.ToString() + "% of the way";
                else promptLabel.text = _hits.ToString() + " of " + _targets.ToString() + what;
            }
            Play(_quality >= 50 ? doneSound : failSound, 1f);
            ShowNodes();
            if (traceLine != null)
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    bool lit = _style == STYLE_BLADE ? (i > 0 && _hit[i - 1]) || (i < _targets && _hit[i]) : _hit[i];
                    if (lit) _lineBuf[n++] = board.TransformPoint(_local[i]);
                }
                traceLine.positionCount = n;
                for (int i = 0; i < n; i++) traceLine.SetPosition(i, _lineBuf[i]);
            }
            if (crosshair != null) crosshair.gameObject.SetActive(false);
            if (wisp != null) wisp.gameObject.SetActive(false);
            // Let go of the tool; the board lingers a moment to show the result.
            DropTools();
            _hideAt = Time.time + 0.9f;
            BattleClient c = _caller;
            _caller = null;
            if (c != null) c._PatternDone();
        }

        private void DropTools()
        {
            if (wandPickup != null && wandPickup.IsHeld) wandPickup.Drop();
            if (pistolPickup != null && pistolPickup.IsHeld) pistolPickup.Drop();
            _held = false;
            if (wand != null) wand.gameObject.SetActive(false);
            if (pistol != null) pistol.gameObject.SetActive(false);
            if (fxLine != null) fxLine.gameObject.SetActive(false);
        }

        private void HideAll()
        {
            DropTools();
            _hideAt = -1f;
            if (crosshair != null) crosshair.gameObject.SetActive(false);
            if (wisp != null) wisp.gameObject.SetActive(false);
            if (board != null) board.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ input

        /// <summary>The wand (or sidearm) was taken (PatternWand relays the pickup's events): the style's clocks start.</summary>
        public void _WandGrabbed()
        {
            _held = true;
            if (_active)
            {
                if (promptLabel != null) promptLabel.text = Prompt(true);
                Engage();
            }
            Play(grabSound, 1f);
        }

        /// <summary>Let go: it flies back to your hip.</summary>
        public void _WandDropped()
        {
            _held = false;
            _slashing = false;
            _hadPtr = false;
        }

        private void ClickDot(int i)
        {
            if (!_active || _vr || i >= _count) return;
            if (_style == STYLE_PISTOL)
            {
                // A click is a shot at the live target.
                if (i != _next || !_engaged || Time.time < _targetStart) return;
                Play(shotSound, 1f);
                Reach(_next, true);
                _targetStart = Time.time + 0.12f;
                ShowNodes();
                return;
            }
            if (_style == STYLE_BATON)
            {
                if (i != _next || !_engaged) return;
                float due = _beatStart + _next * _beat;
                // On the beat, or the point is lost (early or late).
                Reach(_next, Mathf.Abs(Time.time - due) <= batonWindow);
                return;
            }
            if (_style != STYLE_WAND) return;
            if (i == _next) { Reach(i, true); return; }
            if (i <= _next) return;
            // Skipping ahead costs the point you should have clicked (the one clicked counts if it was the very next).
            Reach(_next, false);
            if (i == _next) Reach(_next, true);
        }

        public void Dot0() { ClickDot(0); }
        public void Dot1() { ClickDot(1); }
        public void Dot2() { ClickDot(2); }
        public void Dot3() { ClickDot(3); }
        public void Dot4() { ClickDot(4); }
        public void Dot5() { ClickDot(5); }
        public void Dot6() { ClickDot(6); }
        public void Dot7() { ClickDot(7); }
        public void Dot8() { ClickDot(8); }
        public void Dot9() { ClickDot(9); }
    }
}
