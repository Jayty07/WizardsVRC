using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Every attack spell's own choreography, and a wizard's attack spell played as a cinematic (local, one rig shared by
    /// every duel circle; BattleClient starts it for each attack spell's turn, so every client plays the same beats at the
    /// same times from the synced round).
    ///
    /// Each attack spell has its own recipe (CardDatabase.cineStyles, chosen in WizardsVRCSceneBuilder.SpellStyles.cs so no
    /// two share one): how it travels (a lance, a corkscrew, a fanned volley, a meteor up into the sky and down, a rain from
    /// above, a runner under the floor, a beam, orbs circling the caster before they fly, a jagged lightning path, a pair of
    /// portals, a wave abreast, a boomerang round the flank), how it lands (a burst, an implosion, a column of light, a
    /// cascade of rings, spikes out of the ground, a vortex, a nova, a shatter of shards, the school's rune stamped down), the
    /// flourish round the caster as they wind up, how the film shoots it, and the shape and number of its bolts. A spell that
    /// summons a creature sends the creature along the same path. Everything grows with the spell's pips.
    ///
    /// Every attack spell is a cinematic, a wizard's or a monster's:
    ///   in        the caster vanishes from their seat in a burst and appears in the middle of the ring facing their target
    ///             (a wizard's own client teleports them, with their view irising shut and open in their school's colour;
    ///             a monster's body is moved there on every client)
    ///   wind-up   the school's rune draws itself on the floor under them while the wind-up flourish plays (BattleClient
    ///             draws the symbol in the air in front of them and plays the cast sound, as ever). The caster sees it in
    ///             first person; everyone else in the fight already watches it on the film screen, the camera circling them
    ///   release   the caster's screen cuts to film too (CutscenePlayer, the NPC cutscene screen) as the spell flies from
    ///             the middle of the ring to its target, stopping at the target (a creature pulls up in front of it)
    ///   strike    the hit, shaken; then the shots hold while it settles (longer for bigger spells)
    ///   out       the caster vanishes back to their seat (a wizard's view irises shut and open again)
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

        [Header("Wind-up")]
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

        [Header("Release")]
        [Tooltip("A cone of sparks off the caster's hand as the spell is let go (it fires along its forward axis).")]
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
        [Tooltip("Pips from which a standard or following film opens on the caster letting go.")]
        public int heroShotPips = 2;
        public float wideTurnDegrees = 14f;

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
        private bool _aoe;
        private float _scale;
        private string _caster;
        private string _spell;
        private Color _color;

        // Beats, in seconds from the start.
        private float _tHalf, _tCast, _tRel, _tStrike, _tRelEnd, _tAfter, _tBack, _tEnd;

        private Material _fadeMat, _runeMat, _sigilMat;
        private float _auraAcc, _emberAcc, _spiralAcc, _trailAcc;

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

        // A summoned creature riding the first bolt's path.
        private Transform _carrier;
        private Animator _carrierAnim;
        private bool _carrierFlyer;
        private int _carrierCue;
        private Vector3 _carrierLast;

        private Vector3[] _spikeAt = new Vector3[8];
        private Quaternion[] _spikeRot = new Quaternion[8];
        private float[] _spikeH = new float[8];

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
            _casterBody = cinematic ? body : null;
            _bodyMoved = false;
            if (seat != null)
            {
                _seatPos = seat.position;
                _seatRot = seat.rotation;
            }
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

        /// <summary>
        /// Starts the attack spell's choreography (after <see cref="Prepare"/>): cast from 'from' (the middle of the ring
        /// for a cinematic, else the seat) facing 'fwd' at 'aim' (the target, or the middle of the side an area spell
        /// hits). 'style' is CardDatabase.CineStyle; 'body' the caster's size (big monsters cast from higher). Times: in
        /// (vanish and appear), wind (the wind-up), strike (into the release, when it hits), release (its whole flight),
        /// after (the shots of it landing), out (back to the seat).
        /// </summary>
        public void Begin(Vector3 from, Vector3 fwd, Vector3 aim, int school, int style, int pips, bool aoe, float body, string caster, string spell,
                          float inSec, float windSec, float strikeSec, float releaseSec, float afterSec, float outSec)
        {
            _from = from;
            _ground = from.y;
            fwd.y = 0f;
            _fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : _seatRot * Vector3.forward;
            _right = Vector3.Cross(Vector3.up, _fwd).normalized;
            _aim = aim;
            _body = Mathf.Clamp(body, 0.5f, 3f);
            int looks = sparkMats != null && sparkMats.Length > 0 ? sparkMats.Length : 8;
            _look = school >= 0 && school < looks - 1 ? school : looks - 1;
            _pips = Mathf.Clamp(pips, 0, 8);
            _aoe = aoe;
            _scale = (0.85f + 0.17f * _pips) * Mathf.Max(1f, _body * 0.8f);
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

            _tHalf = inSec * 0.5f;
            _tCast = inSec;
            _tRel = inSec + windSec;
            _tStrike = _tRel + Mathf.Max(0.05f, strikeSec);
            _tRelEnd = _tRel + Mathf.Max(strikeSec + 0.1f, releaseSec);
            _tAfter = _tRelEnd + afterSec;
            _tBack = _tAfter + outSec * 0.5f;
            _tEnd = _tAfter + outSec;

            _t0 = Time.time;
            _cue = 0;
            _teleported = false;
            _returned = false;
            _filmOn = false;
            _auraAcc = 0f;
            _emberAcc = 0f;
            _spiralAcc = 0f;
            _trailAcc = 0f;
            _headCount = 0;
            _carrier = null;
            _carrierAnim = null;
            _headPos = _hand;
            _headDir = _fwd;
            HideRig();
            Recolor();
            _on = true;
        }

        /// <summary>A summoned creature (the spell animation's 'Summon') rides the first bolt's path instead of a glowing bolt.</summary>
        public void Carry(Transform summon, Animator anim, bool flyer)
        {
            if (!_on || summon == null) return;
            _carrier = summon;
            _carrierAnim = anim;
            _carrierFlyer = flyer;
            _carrierCue = 0;
            _carrierLast = _hand;
            _carrier.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            if (heads != null && heads.Length > 0) ShowShape(0, -1);
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
            AnimateRune(t);
            AnimateCharge(t, dt);
            AnimateHeads(t, dt);
            AnimateCarrier(t);
            AnimateBeam(t);
            AnimatePortals(t);
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
                _cue = 3;
                if (runeCircle != null)
                {
                    runeCircle.position = _from + Vector3.up * 0.06f;
                    runeCircle.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    runeCircle.gameObject.SetActive(true);
                }
                if (fxLight != null) { fxLight.color = _color; fxLight.intensity = 0f; fxLight.range = 6f * _scale; fxLight.gameObject.SetActive(true); }
                // Everyone else in the fight watches the whole cast on the film screen.
                if (_watch && cutscenePlayer != null && !cutscenePlayer.IsPlaying())
                {
                    cutscenePlayer.PlayFilm(_caster, _spell + "!", Color.Lerp(_color, Color.white, 0.35f));
                    _filmOn = cutscenePlayer.IsFilming();
                    if (_filmOn) Film(t);
                }
            }
            if (_cue == 3 && t >= _tRel)
            {
                _cue = 4;
                Release();
            }
            if (_cue == 4 && t >= _tStrike)
            {
                _cue = 5;
                Strike();
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

        // ------------------------------------------------------------------ release: plotting each bolt's flight

        /// <summary>The spell is let go: sparks off the hand, its bolts set off on their paths, and (for the caster) the film.</summary>
        private void Release()
        {
            Vector3 dir = _hit - _hand;
            if (releaseBurst != null)
            {
                releaseBurst.transform.position = _hand;
                if (dir.sqrMagnitude > 0.001f) releaseBurst.transform.rotation = Quaternion.LookRotation(dir);
                releaseBurst.transform.localScale = Vector3.one * _scale;
                releaseBurst.Emit(25 + 12 * _pips);
            }
            Sound(releaseSound, _hand);
            if (_runeMat != null) _runeMat.SetFloat("_Flare", 2.5f);
            PlotHeads();
            if (_delivery == CardDatabase.CINE_PORTAL && portals != null)
                for (int i = 0; i < portals.Length && i < 2; i++) if (portals[i] != null) { portals[i].localScale = new Vector3(0.01f, 0.01f, 0.01f); portals[i].gameObject.SetActive(true); }
            if (_delivery == CardDatabase.CINE_RAIN && portals != null && portals.Length > 1 && portals[1] != null)
            {
                // The rain falls out of a ring opened high over the target.
                portals[1].position = _aim + Vector3.up * (8.5f + _height);
                portals[1].rotation = Quaternion.identity;
                portals[1].localScale = new Vector3(0.01f, 0.01f, 0.01f);
                portals[1].gameObject.SetActive(true);
            }
            if (_delivery == CardDatabase.CINE_BEAM && beam != null)
            {
                beam.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                beam.gameObject.SetActive(true);
            }
            if (_me && !_filmOn && cutscenePlayer != null && !cutscenePlayer.IsPlaying())
            {
                cutscenePlayer.PlayFilm(_caster, _spell + "!", Color.Lerp(_color, Color.white, 0.35f));
                _filmOn = cutscenePlayer.IsFilming();
                if (_filmOn) Film(_tRel);
            }
        }

        private int DefaultCount()
        {
            int d = _delivery;
            if (d == CardDatabase.CINE_HELIX) return 2;
            if (d == CardDatabase.CINE_VOLLEY) return Mathf.Max(3, _pips);
            if (d == CardDatabase.CINE_RAIN) return 10;
            if (d == CardDatabase.CINE_ORBIT) return Mathf.Max(3, _pips);
            if (d == CardDatabase.CINE_WAVE) return 5;
            return 1;
        }

        private void PlotHeads()
        {
            int n = _count > 0 ? _count : DefaultCount();
            if (_delivery == CardDatabase.CINE_ORBIT) n = Mathf.Min(n, 8);
            n = Mathf.Clamp(n, 1, heads != null ? Mathf.Min(heads.Length, MAX_HEADS) : 0);
            _headCount = heads != null ? n : 0;
            float strike = _tStrike - _tRel;
            float relLen = _tRelEnd - _tRel;
            Vector3 up = Vector3.up;
            float k = _reach;                       // swings and arcs sized to the distance from the middle of the ring
            float spread = (_aoe ? 2.4f : 0.9f) * Mathf.Min(1f, k + 0.2f);
            float lift = 1f + 0.5f * _height;
            for (int i = 0; i < _headCount; i++)
            {
                float from = 0f, to = strike, launch = 0f;
                // The main bolt meets the front of the target; a creature riding it pulls up a stride short.
                Vector3 hit = i == 0 && _creature ? _stop + up * 1.0f : HitFor(i);
                Vector3 A = _hand, B = _hand, C = hit, D = hit;
                float side = (i % 2 == 0 ? 1f : -1f) * _flip;
                int pair = (i + 1) / 2;
                int d = _delivery;
                if (d == CardDatabase.CINE_LANCE)
                {
                    Vector3 off = _right * ((i - (n - 1) * 0.5f) * 0.4f);
                    A = _hand + off;
                    D = hit + off * (_aoe ? 2.5f : 0.3f);
                    from = 0.06f * i;
                }
                else if (d == CardDatabase.CINE_HELIX)
                {
                    A = _hand;
                    D = hit;
                }
                else if (d == CardDatabase.CINE_VOLLEY)
                {
                    float sw = side * (0.7f + 0.35f * pair) * k;
                    A = _hand + _right * (side * 0.12f);
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
                    A = _hand;
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
                    A = land + up * (8.5f + _height) - _fwd * (0.5f * _flip);
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
                    A = _hand;
                    D = _hand;
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
                    A = _hand;
                    D = hit;
                    if (i > 0) D += _right * (side * 0.45f * pair * (_aoe ? 3f : 1f));
                    from = 0.05f * i;
                }
                else if (d == CardDatabase.CINE_PORTAL)
                {
                    // Into a ring at the hand, out of one high over the front of the target, and down on to it.
                    A = _hand;
                    B = _hand + _fwd * 0.7f;
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
                    float sw = (i == 0 ? 1f : -1f) * _flip * 3.0f * k;
                    A = _hand;
                    B = _hand + _right * sw + _fwd * (0.4f * k) + up * (0.4f * lift);
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
                float size = i == 0 && !_creature ? 1f : 0.55f;
                if (d == CardDatabase.CINE_RAIN) size = i == 0 ? 0.8f : 0.45f;
                if (d == CardDatabase.CINE_BEAM) size = 0.9f;
                size *= 0.75f + 0.08f * _pips;
                h.localScale = new Vector3(size, size, size);
                ShowShape(i, i == 0 && (_carrier != null || _creature) ? -1 : _shape);
            }
        }

        /// <summary>An orb circling the caster (ORBIT) at time t (seconds from the start).</summary>
        private Vector3 OrbitPos(int i, int n, float tRel)
        {
            float ang = (_flip * tRel * 320f + i * 360f / Mathf.Max(1, n)) * Mathf.Deg2Rad;
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
            int d = _delivery;
            Vector3 up = Vector3.up;
            if (d == CardDatabase.CINE_LANCE) return Vector3.Lerp(A, D, Mathf.Pow(u, 1.4f)) + up * (Mathf.Sin(u * 30f + i) * 0.03f);
            if (d == CardDatabase.CINE_HELIX)
            {
                float n = Mathf.Max(1, _headCount);
                float th = _flip * u * Mathf.PI * (3f + _height) + i * Mathf.PI * 2f / n;
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
            float swing = ((k % 2 == 0) ? 1f : -1f) * _flip * (i % 2 == 0 ? 1f : -1f) * 0.9f * _reach * (1f - f * 0.6f);
            return p + _right * swing + Vector3.up * (Mathf.Sin(k * 2.1f + i) * 0.35f);
        }

        private bool HeadHidden(int i, float u)
        {
            return _delivery == CardDatabase.CINE_PORTAL && u >= 0.3f && u < 0.55f;
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
                    h.rotation = _shape == 2 ? face * Quaternion.Euler(0f, 0f, t * 900f * _flip) : face;
                }
                if (i == 0) { _headPos = p; if (dir.sqrMagnitude > 0.00001f) _headDir = dir.normalized; }
                // A runner under the floor throws up sparks as it goes.
                if (_delivery == CardDatabase.CINE_GROUND && impactBurst != null)
                {
                    _trailAcc += dt * 40f;
                    if (_trailAcc >= 1f) { int k = (int)_trailAcc; _trailAcc -= k; Burst(impactBurst, p, Mathf.Min(4, k)); }
                }
                if (u >= 1f && _delivery != CardDatabase.CINE_BEAM)
                {
                    // It's there: the head goes out (its trail lingers) and sparks fly off; the main one is the spell's hit.
                    _hLanded[i] = true;
                    ShowShape(i, -1);
                    if (i > 0) Burst(impactBurst, p, 8 + 2 * _pips);
                }
                if (_delivery == CardDatabase.CINE_BEAM && t >= _hTo[i]) { _hLanded[i] = true; h.gameObject.SetActive(false); }
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

        /// <summary>A summoned creature rides the first bolt: it grows out of the caster's hand, flies or runs the path, strikes, is thrown back and breaks up.</summary>
        private void AnimateCarrier(float t)
        {
            if (_carrier == null || t < _tRel) return;
            bool flat = !_carrierFlyer && (_delivery == CardDatabase.CINE_LANCE || _delivery == CardDatabase.CINE_ZIGZAG || _delivery == CardDatabase.CINE_GROUND ||
                                           _delivery == CardDatabase.CINE_WAVE || _delivery == CardDatabase.CINE_BOOMERANG || _delivery == CardDatabase.CINE_BEAM);
            float k = t - _tRel;
            Vector3 p;
            Vector3 face = _fwd;
            float scale = 1f;
            if (_delivery == CardDatabase.CINE_BEAM)
            {
                // It stands before the caster and the beam pours out of it.
                p = _from + _fwd * (1.7f * _body);
                scale = Ease(k / 0.3f);
                if (t > _tRelEnd - 0.4f) scale *= 1f - Ease((t - (_tRelEnd - 0.4f)) / 0.35f);
            }
            else if (t < _tStrike)
            {
                p = _headPos - Vector3.up * 0.9f;
                Vector3 d = _headPos - _carrierLast;
                if (d.sqrMagnitude > 0.00001f) face = d.normalized;
                scale = Ease(k / 0.25f);
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
                if (_carrierCue == 3 && r >= 0.35f) { _carrierCue = 4; Burst(portalBurst, p + Vector3.up, 45); }
            }
            if (flat) p.y = _aim.y;
            if (!_carrierFlyer) face.y = 0f;
            _carrier.position = p;
            if (face.sqrMagnitude > 0.0001f) _carrier.rotation = Quaternion.LookRotation(face);
            _carrier.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            _carrierLast = _headPos;
            if (_carrierAnim != null)
            {
                if (_carrierCue == 0) { _carrierCue = 1; _carrierAnim.SetTrigger("Cast"); Burst(portalBurst, _hand, 30); }
                if (_carrierCue == 1 && k >= 0.3f) { _carrierCue = 2; if (flat) _carrierAnim.SetTrigger("Walk"); }
                if (_carrierCue == 2 && t >= _tStrike) { _carrierCue = 3; _carrierAnim.SetTrigger("Hit"); }
            }
            else if (_carrierCue < 3 && t >= _tStrike) _carrierCue = 3;
        }

        private void AnimateBeam(float t)
        {
            if (beam == null || !beam.gameObject.activeSelf) return;
            if (t >= _tRelEnd) { beam.gameObject.SetActive(false); return; }
            Vector3 start = _carrier != null ? _from + _fwd * (1.7f * _body) + Vector3.up * (1.2f * _body) : _hand;
            Vector3 end = _hit;
            Vector3 d = end - start;
            float len = d.magnitude;
            float k = t - _tRel;
            float grow = Ease(k / 0.35f) * Mathf.Min(1f, Mathf.Max(0.05f, (t - _tRel) / Mathf.Max(0.05f, _tStrike - _tRel)));
            float w = (0.3f + 0.05f * _pips) * (1f + 0.15f * Mathf.Sin(t * 40f)) * (_aoe ? 1.6f : 1f);
            w *= 1f - Ease((t - (_tRelEnd - 0.3f)) / 0.3f);
            beam.position = start;
            if (len > 0.001f) beam.rotation = Quaternion.FromToRotation(Vector3.up, d / len) * Quaternion.Euler(0f, t * 200f, 0f);
            beam.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, len * grow), Mathf.Max(0.01f, w));
            _headPos = Vector3.Lerp(start, end, Mathf.Min(1f, grow));
            _headDir = len > 0.001f ? d / len : _fwd;
            if (t >= _tStrike && impactBurst != null && Random.value < 0.5f) Burst(impactBurst, end, 2);
        }

        private void AnimatePortals(float t)
        {
            if (portals == null) return;
            for (int i = 0; i < portals.Length && i < 2; i++)
            {
                Transform p = portals[i];
                if (p == null || !p.gameObject.activeSelf) continue;
                float open, close;
                if (_delivery == CardDatabase.CINE_RAIN)
                {
                    open = Ease((t - _tRel) / 0.35f);
                    close = 1f - Ease((t - (_tStrike + 0.2f)) / 0.4f);
                }
                else
                {
                    float strike = _tStrike - _tRel;
                    float t0 = i == 0 ? _tRel : _tRel + strike * 0.3f;
                    float t1 = i == 0 ? _tRel + strike * 0.45f : _tStrike + 0.1f;
                    open = Ease((t - t0) / 0.2f);
                    close = 1f - Ease((t - t1) / 0.25f);
                }
                float s = Mathf.Max(0f, Mathf.Min(open, close)) * (_delivery == CardDatabase.CINE_RAIN ? (_aoe ? 6f : 3.5f) : 1.6f + 0.1f * _pips);
                if (s <= 0.01f && t > _tRel + 0.3f && close <= 0f) { p.gameObject.SetActive(false); continue; }
                p.localScale = new Vector3(Mathf.Max(0.01f, s), 1f, Mathf.Max(0.01f, s));
            }
        }

        // ------------------------------------------------------------------ impact

        /// <summary>It hits, each kind of impact its own way, everything sized by the spell.</summary>
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
                    impactBurst.Emit((int)((35 + 22 * _pips) * burstMul));
                }
                if (shardBurst != null && (im == CardDatabase.IMPACT_SHATTER || im == CardDatabase.IMPACT_SPIKES))
                {
                    shardBurst.transform.position = bat;
                    shardBurst.transform.localScale = Vector3.one * big;
                    shardBurst.Emit(im == CardDatabase.IMPACT_SHATTER ? 40 + 25 * _pips : 12 + 6 * _pips);
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
            Sound(_pips >= 4 && impactBigSound != null ? impactBigSound : impactSound, at);
        }

        /// <summary>How big the impact is: the spell's pips and the target's size (not the caster's: a huge boss's spell still lands on a wizard-sized target).</summary>
        private float ImpactSize()
        {
            return (0.85f + 0.17f * _pips) * Mathf.Max(1f, _tScale * 0.8f) * (_aoe ? 1.25f : 1f);
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
                _auraAcc += dt * (40f + 15f * _pips);
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
                    float far = (im == CardDatabase.IMPACT_NOVA ? 6f + 0.45f * _pips : im == CardDatabase.IMPACT_RINGS ? 3f + 1.2f * i + 0.35f * _pips : 3.5f + 0.4f * _pips) * _tScale;
                    float sz = Mathf.Lerp(0.6f, far * (_aoe ? 1.4f : 1f), 1f - (1f - rk) * (1f - rk));
                    ring.localScale = new Vector3(sz, Mathf.Max(0.01f, 1f - rk), sz);
                }
            if (impactPillar != null && impactPillar.gameObject.activeSelf)
            {
                float pk = r / 0.95f;
                if (pk >= 1f) impactPillar.gameObject.SetActive(false);
                else
                {
                    float h = Ease(pk / 0.25f) * (6f + 1.2f * _pips);
                    float w = (1.3f + 0.15f * _pips) * (1f - Ease((pk - 0.3f) / 0.7f)) * (_aoe ? 1.6f : 1f);
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
                    float w = (0.22f + 0.03f * _pips) * (0.6f + 0.4f * rise);
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
                float rate = (18f + 8f * _pips) * (im == CardDatabase.IMPACT_NOVA || im == CardDatabase.IMPACT_VORTEX ? 1.7f : 1f);
                _emberAcc += dt * rate * fall;
                int n = (int)_emberAcc;
                if (n > 0) { embers.Emit(n); _emberAcc -= n; }
            }
        }

        // ------------------------------------------------------------------ wind-up

        /// <summary>The rune opens under the caster, draws itself through the wind-up, turns, flares on release, closes after.</summary>
        private void AnimateRune(float t)
        {
            if (runeCircle == null || !runeCircle.gameObject.activeSelf) return;
            float open = Ease((t - _tCast) / 0.45f);
            float close = 1f - Ease((t - _tAfter + 0.35f) / 0.35f);
            float s = 2.6f * _scale * Mathf.Min(open, close) * (1f + 0.03f * Mathf.Sin(t * 14f));
            runeCircle.localScale = new Vector3(Mathf.Max(0.01f, s), Mathf.Max(0.01f, s), Mathf.Max(0.01f, s));
            runeCircle.rotation = Quaternion.Euler(0f, t * (40f + 8f * _pips) * _flip, 0f);
            if (runeRing != null) runeRing.localRotation = Quaternion.Euler(0f, -t * 90f * _flip, 0f);
            if (_runeMat != null)
            {
                float draw = Mathf.Clamp01((t - _tCast) / Mathf.Max(0.3f, (_tRel - _tCast) * 0.6f));
                _runeMat.SetFloat("_Reveal", draw < 1f ? draw : 1.05f);
                if (t >= _tRel) _runeMat.SetFloat("_Flare", Mathf.Max(0f, 2.5f - (t - _tRel) * 3f));
            }
        }

        /// <summary>The wind-up flourish: energy streaming in, rings turning round the caster, a fountain of embers, a spiral of sparks, or a column of light.</summary>
        private void AnimateCharge(float t, float dt)
        {
            bool winding = t >= _tCast && t < _tRel;
            float build = Mathf.Clamp01((t - _tCast) / Mathf.Max(0.1f, _tRel - _tCast));
            Vector3 chest = _from + Vector3.up * (1.2f * _body);
            // Energy streams in for a STREAM wind-up, and under every other one for big spells (4+ pips).
            bool stream = _charge == CardDatabase.CHARGE_STREAM || _pips >= 4;
            if (winding && stream && chargeAura != null)
            {
                chargeAura.transform.position = chest;
                chargeAura.transform.localScale = Vector3.one * _scale;
                float rate = (25f + 14f * _pips) * (_charge == CardDatabase.CHARGE_STREAM ? 1f : 0.45f);
                _auraAcc += dt * rate * (0.4f + build);
                int n = (int)_auraAcc;
                if (n > 0) { chargeAura.Emit(n); _auraAcc -= n; }
            }
            if (gyroRings != null)
                for (int i = 0; i < gyroRings.Length; i++)
                {
                    Transform g = gyroRings[i];
                    if (g == null) continue;
                    bool on = _charge == CardDatabase.CHARGE_GYRO && t >= _tCast && t < _tRel + 0.3f;
                    if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
                    if (!on) continue;
                    float s = (1.5f + 0.12f * _pips) * _body * Ease((t - _tCast) / 0.4f) * (1f - Ease((t - _tRel) / 0.3f));
                    g.position = chest;
                    g.rotation = i == 0 ? Quaternion.Euler(70f, t * 200f * _flip, 0f) : Quaternion.Euler(-55f, -t * 160f * _flip, 35f);
                    g.localScale = new Vector3(Mathf.Max(0.01f, s), 1f, Mathf.Max(0.01f, s));
                }
            if (winding && _charge == CardDatabase.CHARGE_FOUNTAIN && embers != null)
            {
                embers.transform.position = _from + Vector3.up * 0.1f;
                embers.transform.localScale = Vector3.one * _scale;
                _emberAcc += dt * (30f + 10f * _pips) * (0.5f + build);
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
                float t0 = _tCast + (_tRel - _tCast) * 0.3f;
                bool on = _charge == CardDatabase.CHARGE_COLUMN && t >= t0 && t < _tRel + 0.45f;
                if (castPillar.gameObject.activeSelf != on) castPillar.gameObject.SetActive(on);
                if (on)
                {
                    float grow = Ease((t - t0) / 0.5f);
                    float fade = 1f - Ease((t - _tRel) / 0.45f);
                    float w = (1.1f + 0.12f * _pips) * _body * fade * (1f + 0.08f * Mathf.Sin(t * 22f));
                    castPillar.position = _from;
                    castPillar.localScale = new Vector3(Mathf.Max(0.01f, w), Mathf.Max(0.01f, grow * (4f + 0.8f * _pips)), Mathf.Max(0.01f, w));
                    castPillar.rotation = Quaternion.Euler(0f, t * 60f, 0f);
                }
            }
        }

        /// <summary>The school's light: it builds round the caster, follows the spell, flares on the hit and dies away.</summary>
        private void AnimateLight(float t)
        {
            if (fxLight == null || !fxLight.gameObject.activeSelf) return;
            float peak = 1.4f + 0.35f * _pips;
            if (t < _tRel)
            {
                fxLight.transform.position = _from + Vector3.up * (1.6f * _body);
                fxLight.intensity = peak * Ease((t - _tCast) / Mathf.Max(0.1f, _tRel - _tCast));
            }
            else if (t < _tStrike)
            {
                fxLight.transform.position = _headPos;
                fxLight.intensity = peak;
            }
            else
            {
                float k = t - _tStrike;
                float flare = _impact == CardDatabase.IMPACT_NOVA ? 4f : 2.2f;
                fxLight.transform.position = _impactAt + Vector3.up * 1.4f;
                fxLight.intensity = (peak * flare) * Mathf.Exp(-k * 2.5f);
                fxLight.range = (6f + 1.2f * _pips) * (_aoe ? 1.4f : 1f);
            }
        }

        // ------------------------------------------------------------------ the film (the caster's client only)

        private void Film(float t)
        {
            if (!_filmOn || cutscenePlayer == null || t < _tCast || t >= _tAfter) return;
            if (!cutscenePlayer.IsFilming()) { _filmOn = false; return; }
            if (t < _tRel)
            {
                // The wind-up (watchers): the camera circles the caster in the middle of the ring as the rune draws itself,
                // drawing in closer as the spell gathers.
                float w = Mathf.Clamp01((t - _tCast) / Mathf.Max(0.1f, _tRel - _tCast));
                float ang = (35f + 70f * w) * _flip;
                Vector3 round = Quaternion.Euler(0f, ang, 0f) * (_fwd * (4.2f - 1.4f * w) * Mathf.Max(1f, _body * 0.9f));
                Vector3 wpos = _from + round + Vector3.up * ((1.1f + 0.9f * (1f - w)) * _body);
                Vector3 wlook = _from + Vector3.up * (1.25f * _body);
                cutscenePlayer.SetFilmShot(wpos, Quaternion.LookRotation(wlook - wpos, Vector3.up));
                return;
            }
            float u = t - _tRel;
            float strike = _tStrike - _tRel;
            bool alwaysHero = _cam == CardDatabase.CAM_LOW || _cam == CardDatabase.CAM_CRANE || _cam == CardDatabase.CAM_REVERSE;
            float hero = alwaysHero || _pips >= heroShotPips ? Mathf.Clamp(strike * 0.45f, 0.3f, 0.9f) : 0f;
            float hold = Mathf.Clamp((_tAfter - _tStrike) * 0.55f, 0.5f, 1.4f);
            Vector3 up = Vector3.up;
            Vector3 mid = (_from + _aim) * 0.5f;
            Vector3 pos, look;
            int cam = _cam;
            if (u < hero)
            {
                float k = u / hero;
                if (cam == CardDatabase.CAM_LOW)
                {
                    pos = _from + _fwd * (1.9f * _body) + _right * (0.9f * _flip) + up * 0.35f;
                    look = _from + up * (1.5f * _body);
                }
                else if (cam == CardDatabase.CAM_CRANE)
                {
                    pos = _from + up * ((4.5f - 0.6f * k) * _body) + _fwd * (1.2f * _body) + _right * 1.0f;
                    look = _from + up * (1.0f * _body);
                }
                else if (cam == CardDatabase.CAM_REVERSE)
                {
                    pos = _aim + _fwd * 3.2f + _right * (1.2f * _flip) + up * 1.6f;
                    look = _from + up * (1.4f * _body);
                }
                else
                {
                    look = _from + up * (1.35f * _body) + _fwd * 0.3f;
                    pos = Vector3.Lerp(_from + _fwd * (2.4f * _body) + _right * (1.3f * _flip * _body) + up * (1.25f * _body), look, 0.15f * k);
                }
            }
            else if (u < strike)
            {
                float k = (u - hero) / Mathf.Max(0.05f, strike - hero);
                if (cam == CardDatabase.CAM_LOW)
                {
                    pos = mid + _right * (2.4f * _flip) + up * 0.3f;
                    look = _headPos;
                }
                else if (cam == CardDatabase.CAM_CRANE)
                {
                    pos = mid + up * (9f - 2f * k) - _fwd * 1.5f;
                    look = _headPos;
                }
                else if (cam == CardDatabase.CAM_FOLLOW)
                {
                    pos = _headPos - _headDir * 2.3f + up * 0.7f;
                    look = _headPos + _headDir * 2.5f;
                }
                else if (cam == CardDatabase.CAM_REVERSE)
                {
                    pos = _aim + _fwd * 3.2f + _right * (1.2f * _flip) + up * 1.6f;
                    look = _headPos;
                }
                else
                {
                    pos = _from - _fwd * 2.2f + _right * (0.85f * _flip) + up * 1.85f + _fwd * (0.8f * k);
                    look = Vector3.Lerp(_from + _fwd * 2f + up * 1.2f, _aim + up * 1.0f, Ease(k));
                }
            }
            else if (u < strike + hold)
            {
                float k = u - strike;
                look = _aim + up * 1.0f;
                if (cam == CardDatabase.CAM_LOW) { pos = _aim - _fwd * 2.6f + _right * (1.4f * _flip) + up * 0.3f; look = _aim + up * 1.6f; }
                else if (cam == CardDatabase.CAM_CRANE) { pos = _aim + up * 5f - _fwd * 2.5f + _right * 1.5f; look = _aim; }
                else if (cam == CardDatabase.CAM_FOLLOW) pos = _aim - _headDir * 3.2f + up * 1.4f;
                else if (cam == CardDatabase.CAM_REVERSE) pos = _aim + _fwd * 2.4f + _right * (1.6f * _flip) + up * 1.4f;
                else pos = _aim - _fwd * 3.6f + _right * (2.6f * _flip) + up * 1.5f;
                if (_aoe) pos += (pos - look).normalized * 1.5f;
                float amp = 0.035f * (1f + 0.6f * _pips) * Mathf.Exp(-k * 6f) * (_impact == CardDatabase.IMPACT_NOVA ? 1.6f : 1f);
                pos += new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 53f + 1.3f), Mathf.Sin(t * 61f + 2.1f)) * amp;
            }
            else
            {
                // Wide, turning slowly round the ring as it settles (low, high or at eye height to match the shots).
                float k = u - strike - hold;
                float dist = Vector3.Distance(_from, _aim);
                float radius = Mathf.Max(6f, dist * 0.9f);
                float height = cam == CardDatabase.CAM_LOW ? 1.2f : cam == CardDatabase.CAM_CRANE ? 7f : 3.8f;
                Quaternion turn = Quaternion.Euler(0f, wideTurnDegrees * k * _flip, 0f);
                pos = mid + turn * (_right * (radius * _flip) - _fwd * 1.5f) + up * height;
                look = mid + up * 0.8f;
            }
            Vector3 d = look - pos;
            if (d.sqrMagnitude < 0.0001f) d = _fwd;
            cutscenePlayer.SetFilmShot(pos, Quaternion.LookRotation(d, up));
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
            if (sfx == null || clip == null) return;
            sfx.transform.position = at;
            sfx.PlayOneShot(clip);
        }

        /// <summary>Every part in the spell's school colours, and the school's rune on the floor and the stamp.</summary>
        private void Recolor()
        {
            Swap(sparkRenderers, sparkMats);
            Swap(glowRenderers, glowMats);
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
        }

        private void StyleGlyph(Material m)
        {
            if (glyphDrawTex != null && _look < glyphDrawTex.Length && glyphDrawTex[_look] != null) m.SetTexture("_MainTex", glyphDrawTex[_look]);
            m.SetColor("_Color", _color);
            if (glyphEdgeColors != null && _look < glyphEdgeColors.Length) m.SetColor("_EdgeColor", glyphEdgeColors[_look]);
            m.SetFloat("_Style", _look >= 7 ? 0f : _look + 1f);
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
