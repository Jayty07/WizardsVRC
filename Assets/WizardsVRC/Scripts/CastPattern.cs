using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Casting a spell by drawing its rune (local, one shared by every duel circle). A spell under 100% accuracy isn't sent
    /// the moment it's picked: a board of glowing points along the school's symbol (the same one the caster draws in the
    /// air, Tools/make_glyph_draw.py, without its seal ring) opens in front of you, and you trace the points in order.
    ///
    ///   VR       a wand waits at your right hip (left with the book's setting): take it and touch each point with its tip
    ///   desktop  click the points in order
    ///
    /// More pips and a harder spell (lower accuracy) mean more points, smaller targets in VR and less time per point on
    /// desktop. When the last point is touched, time runs out or the round is about to start, the share of points hit is
    /// the pattern's quality, and BattleClient sends the pick with it: BattleManager takes that share of patternFizzleCut
    /// (90%) off the spell's chance to fizzle. Missing a point (touching the one after it, or clicking the wrong one) loses
    /// that point but keeps you going.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CastPattern : UdonSharpBehaviour
    {
        public const int MAX_NODES = 10;

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
        private int _school;
        private int _glyph;
        private int _count;
        private int _next;
        private int _hits;
        private int _quality;
        private float _radius;
        private float _start;
        private float _limit;
        private float _hideAt = -1f;
        private Vector3[] _local = new Vector3[MAX_NODES];
        private float[] _reveal = new float[MAX_NODES];
        private bool[] _hit = new bool[MAX_NODES];
        private Vector3[] _lineBuf = new Vector3[MAX_NODES + 1];
        private Material _ghostMat;
        private Material _drawnMat;

        public bool IsActive() { return _active; }

        /// <summary>Share of the points hit (0-100), once the pattern is done.</summary>
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
        /// Opens the board for a spell of 'school' (-1 = General) costing 'pips' at 'accuracy' (with the caster's bonuses);
        /// 'maxSeconds' is how long the round still gives. Calls caller._PatternDone() when it's finished.
        /// </summary>
        public void Begin(BattleClient caller, int school, int pips, int accuracy, float maxSeconds, string spellName)
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || board == null) return;
            _caller = caller;
            _vr = me.IsUserInVR();
            _school = school;
            int symbols = glyphDrawTex != null ? glyphDrawTex.Length : 8;
            _glyph = LookOf(school, symbols);
            _count = NodeCount(pips, accuracy);
            _next = 0;
            _hits = 0;
            _quality = 0;
            _held = false;
            _hideAt = -1f;
            // Bigger spells: smaller targets (VR), less time a point (desktop).
            int p = Mathf.Clamp(pips, 0, 7);
            _radius = hitRadius * (1.15f - 0.05f * p);
            float perNode = _vr ? vrNodeSeconds : desktopNodeSeconds * (1f - 0.03f * p);
            _limit = baseSeconds + perNode * _count + (_vr ? vrGrabSeconds : 0f);
            if (maxSeconds > 0f) _limit = Mathf.Min(_limit, Mathf.Max(2f, maxSeconds));
            _start = Time.time;
            for (int i = 0; i < MAX_NODES; i++) _hit[i] = false;

            PlaceNodes();
            PlaceBoard(me);
            StyleBoard(spellName);
            board.gameObject.SetActive(true);
            if (wand != null)
            {
                wand.gameObject.SetActive(_vr);
                if (wandGem != null && schoolMats != null && schoolMats.Length > 0) wandGem.sharedMaterial = schoolMats[Mathf.Min(_glyph, schoolMats.Length - 1)];
                if (_vr) Holster(me);
            }
            _active = true;
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
            bool ok = penU != null && penV != null && n >= 2 && b + n <= penU.Length && b + n <= penV.Length;
            int last = n - 1;
            if (ok && penJump != null)
                for (int k = n - 2; k >= 0; k--)
                    if (b + k < penJump.Length && penJump[b + k] != 0) { last = k; break; }
            for (int i = 0; i < _count; i++)
            {
                float f = _count > 1 ? i / (float)(_count - 1) : 0f;
                int k = Mathf.Clamp(Mathf.RoundToInt(f * last), 0, n - 1);
                Vector2 uv = ok ? new Vector2(penU[b + k], penV[b + k]) : new Vector2(0.5f + 0.4f * Mathf.Cos(f * 6.283f), 0.5f + 0.4f * Mathf.Sin(f * 6.283f));
                _local[i] = new Vector3((uv.x - 0.5f) * 2f * boardUnits, (uv.y - 0.5f) * 2f * boardUnits, -12f);
                // How much of the symbol is drawn once this point is reached (GlyphDraw _Reveal runs over the seal too).
                _reveal[i] = n > 1 ? k / (float)(n - 1) : 1f;
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
            if (promptLabel != null)
                promptLabel.text = _vr ? "Take your wand from your " + (leftHip ? "left" : "right") + " hip and touch the points in order"
                                       : "Click the points in order: " + PointList();
            if (guideLine != null)
            {
                guideLine.positionCount = _count;
                for (int i = 0; i < _count; i++) guideLine.SetPosition(i, board.TransformPoint(_local[i]));
                guideLine.gameObject.SetActive(true);
            }
            if (traceLine != null)
            {
                traceLine.positionCount = 0;
                traceLine.gameObject.SetActive(true);
            }
            if (timerFill != null) timerFill.fillAmount = 1f;
        }

        private string PointList()
        {
            string s = "";
            for (int i = 0; i < _count; i++) s += (i > 0 ? " " : "") + (i + 1).ToString();
            return s;
        }

        /// <summary>The points: the next one pulses bright, done ones glow in the school colour, missed ones go grey.</summary>
        private void ShowNodes()
        {
            if (nodes == null) return;
            Material done = schoolMats != null && schoolMats.Length > 0 ? schoolMats[Mathf.Min(_glyph, schoolMats.Length - 1)] : nodeIdleMat;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                bool on = i < _count;
                if (nodes[i].gameObject.activeSelf != on) nodes[i].gameObject.SetActive(on);
                bool btn = on && !_vr;
                if (dotButtons != null && i < dotButtons.Length && dotButtons[i] != null && dotButtons[i].activeSelf != btn) dotButtons[i].SetActive(btn);
                if (!on) continue;
                nodes[i].localPosition = _local[i];
                if (dotButtons != null && i < dotButtons.Length && dotButtons[i] != null)
                {
                    RectTransform rt = dotButtons[i].GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(_local[i].x, _local[i].y);
                }
                if (dotLabels != null && i < dotLabels.Length && dotLabels[i] != null) dotLabels[i].text = (i + 1).ToString();
                Material m = i < _next ? (_hit[i] ? done : nodeMissMat) : (i == _next ? nodeNextMat : nodeIdleMat);
                if (nodeRenderers != null && i < nodeRenderers.Length && nodeRenderers[i] != null && m != null && nodeRenderers[i].sharedMaterial != m)
                    nodeRenderers[i].sharedMaterial = m;
            }
        }

        // ------------------------------------------------------------------ playing

        private void Update()
        {
            if (_hideAt > 0f && Time.time >= _hideAt) { _hideAt = -1f; if (!_active) HideAll(); }
            if (!_active) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            float left = _limit - (Time.time - _start);
            if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(left / Mathf.Max(0.1f, _limit));
            if (_vr)
            {
                if (!_held) Holster(me);
                else TraceWand();
            }
            AnimateNodes();
            UpdateTrace();
            if (_active && left <= 0f) Finish();
        }

        /// <summary>The wand waits at your hip, tip down, until you take it.</summary>
        private void Holster(VRCPlayerApi me)
        {
            if (wand == null) return;
            Vector3 hips = me.GetBonePosition(HumanBodyBones.Hips);
            Quaternion rot = me.GetRotation();
            if (hips.sqrMagnitude < 0.0001f)
            {
                float eye = me.GetAvatarEyeHeightAsMeters();
                hips = me.GetPosition() + Vector3.up * ((eye > 0.2f ? eye : 1.6f) * 0.55f);
            }
            Vector3 side = rot * Vector3.right * (leftHip ? -1f : 1f);
            Vector3 at = hips + side * hipSide + rot * Vector3.forward * hipForward + Vector3.down * hipDrop;
            wand.SetPositionAndRotation(at, rot * Quaternion.Euler(70f, 0f, leftHip ? 12f : -12f));
        }

        /// <summary>The wand tip touching the next point (or the one after it: the skipped one counts as missed).</summary>
        private void TraceWand()
        {
            if (wandTip == null || _next >= _count) return;
            Vector3 tip = wandTip.position;
            if (tipSparks != null)
            {
                tipSparks.transform.position = tip;
                tipSparks.Emit(1);
            }
            float sc = Mathf.Max(0.00001f, board.lossyScale.x);
            Vector3 lp = board.InverseTransformPoint(tip);
            if (Mathf.Abs(lp.z * sc) > hitDepth) return;
            if (Near(lp, _next, sc)) { Reach(_next, true); return; }
            if (_next + 1 < _count && Near(lp, _next + 1, sc))
            {
                Reach(_next, false);
                Reach(_next, true);
            }
        }

        private bool Near(Vector3 lp, int i, float sc)
        {
            float dx = (lp.x - _local[i].x) * sc, dy = (lp.y - _local[i].y) * sc;
            return dx * dx + dy * dy <= _radius * _radius;
        }

        /// <summary>Point _next is done: hit (bright tick, higher each time) or missed.</summary>
        private void Reach(int i, bool hit)
        {
            if (!_active || i != _next || _next >= _count) return;
            _hit[i] = hit;
            if (hit) _hits++;
            _next++;
            if (sfx != null)
            {
                AudioClip clip = hit ? tickSound : missSound;
                if (clip != null)
                {
                    sfx.pitch = hit ? 1f + 0.06f * _hits : 1f;
                    sfx.PlayOneShot(clip);
                }
            }
            if (_drawnMat != null) _drawnMat.SetFloat("_Reveal", _reveal[i]);
            ShowNodes();
            if (_next >= _count) Finish();
        }

        private void AnimateNodes()
        {
            if (nodes == null) return;
            float pulse = 1f + 0.25f * Mathf.Sin(Time.time * 9f);
            for (int i = 0; i < _count && i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                float s = i == _next ? 34f * pulse : (i < _next ? (_hit[i] ? 30f : 20f) : 24f);
                nodes[i].localScale = new Vector3(s, s, s);
            }
        }

        private void UpdateTrace()
        {
            if (traceLine == null) return;
            int n = 0;
            for (int i = 0; i < _next && i < _count; i++) _lineBuf[n++] = board.TransformPoint(_local[i]);
            if (_vr && _held && wandTip != null && n > 0 && _next < _count) _lineBuf[n++] = wandTip.position;
            traceLine.positionCount = n;
            for (int i = 0; i < n; i++) traceLine.SetPosition(i, _lineBuf[i]);
        }

        private void Finish()
        {
            if (!_active) return;
            _active = false;
            _quality = _count > 0 ? Mathf.Clamp(_hits * 100 / _count, 0, 100) : 0;
            bool perfect = _hits >= _count;
            if (_drawnMat != null)
            {
                _drawnMat.SetFloat("_Reveal", perfect ? 1.05f : _drawnMat.GetFloat("_Reveal"));
                _drawnMat.SetFloat("_Flare", perfect ? 2.2f : 0f);
            }
            if (promptLabel != null)
                promptLabel.text = perfect ? "Perfect rune!" : _hits.ToString() + " of " + _count.ToString() + " points";
            if (sfx != null)
            {
                sfx.pitch = 1f;
                AudioClip clip = _hits * 2 >= _count ? doneSound : failSound;
                if (clip != null) sfx.PlayOneShot(clip);
            }
            ShowNodes();
            if (traceLine != null)
            {
                int n = 0;
                for (int i = 0; i < _count; i++) if (_hit[i]) _lineBuf[n++] = board.TransformPoint(_local[i]);
                traceLine.positionCount = n;
                for (int i = 0; i < n; i++) traceLine.SetPosition(i, _lineBuf[i]);
            }
            // Let go of the wand; the board lingers a moment to show the result.
            if (_held && wandPickup != null) wandPickup.Drop();
            _held = false;
            if (wand != null) wand.gameObject.SetActive(false);
            _hideAt = Time.time + 0.9f;
            BattleClient c = _caller;
            _caller = null;
            if (c != null) c._PatternDone();
        }

        private void HideAll()
        {
            if (_held && wandPickup != null) wandPickup.Drop();
            _held = false;
            _hideAt = -1f;
            if (wand != null) wand.gameObject.SetActive(false);
            if (board != null) board.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ input

        /// <summary>The wand was taken (PatternWand relays the pickup's events).</summary>
        public void _WandGrabbed()
        {
            _held = true;
            if (_active && promptLabel != null) promptLabel.text = "Touch the glowing points in order";
            if (sfx != null && grabSound != null) { sfx.pitch = 1f; sfx.PlayOneShot(grabSound); }
        }

        /// <summary>Let go: it flies back to your hip.</summary>
        public void _WandDropped()
        {
            _held = false;
        }

        private void ClickDot(int i)
        {
            if (!_active || _vr || i >= _count) return;
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
