using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The Starshard Shop's try-on stage. A camera films your own avatar (VRChat's mirror copy of it on the
    /// MirrorReflection layer, as the book's portrait does) wearing whatever you've picked, and the shop shows the film
    /// in its preview frame, swinging gently round you so you see it from the sides too.
    ///   Aura: a real copy of the aura players wear, round your feet. A school Surge plays a duel on loop: it swells
    ///         pip by pip, blasts outward when you "cast", and erupts at full pips (the label under the film says which).
    ///   Title: over your head exactly as other players see it.
    ///   Spell skin: your school's casting symbol drawn stroke by stroke in front of you in the skin's colour, flaring as
    ///         the spell is released, like in a duel.
    /// Everything here is on the MirrorReflection layer, so it never shows in your own view, and it is local to you.
    /// It runs only while the shop window is open on an aura, title or spell skin (ShopPages calls Show and Hide).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ShopPreview : UdonSharpBehaviour
    {
        public const int KIND_TITLE = 0;
        public const int KIND_AURA = 1;
        public const int KIND_SKIN = 2;

        [Tooltip("Everything on the stage (camera, auras, title, symbol); switched off when nothing is being tried on.")]
        public GameObject stageRoot;
        [Tooltip("The shop window: when it closes (book closed, other tab) the stage switches off.")]
        public GameObject watchView;
        public Camera previewCamera;

        [Header("Aura")]
        [Tooltip("Follows your feet; holds one particle system per aura, built like the ones players wear.")]
        public Transform feet;
        public ParticleSystem[] auras;
        public ParticleSystem[] auraFlares;
        public ParticleSystem[] auraMaxFx;
        public int maxPips = 7;
        public GameObject pipPlate;
        public Text pipLabel;

        [Header("Title")]
        public Transform titleRoot;
        public Text titleLabel;
        public float titleHeight = 0.55f;

        [Header("Spell skin")]
        public Renderer glyph;
        [Tooltip("The drawn symbols (WizardsVRC/GlyphDraw data maps): one per school, then the general one.")]
        public Texture2D[] glyphTex;
        public float glyphSize = 1.1f;

        [Header("Camera")]
        [Tooltip("Distance from you, in avatar eye heights.")]
        public float distance = 2.1f;
        public float orbitDegrees = 35f;
        public float orbitSpeed = 0.45f;

        private bool _on;
        private int _kind = -1;
        private int _index = -1;
        private float _start;
        private float _lastCycle;
        private float _castAt = -10f;
        private Material _glyphMat;
        private Vector3 _camPos;
        private Vector3 _dir = Vector3.forward;
        private float _eye = 1.6f;

        private void Start()
        {
            if (!_on) Hide();
        }

        /// <summary>
        /// Shows item (kind, index) on you: kind 0 title, 1 aura, 2 spell skin. Returns false for anything that can't be
        /// tried on (elixirs), and the shop shows its picture instead. Calling it again with the same item keeps the loop.
        /// </summary>
        public bool Show(int kind, int index, int school, Color skinColor, string titleText)
        {
            if (kind < KIND_TITLE || kind > KIND_SKIN || stageRoot == null) { Hide(); return false; }
            bool changed = !_on || kind != _kind || index != _index;
            if (!_on)
            {
                _on = true;
                stageRoot.SetActive(true);
                enabled = true;
            }
            if (changed)
            {
                _kind = kind;
                _index = index;
                _start = Time.time;
                _lastCycle = 0f;
                _castAt = -10f;
                Dress(school, skinColor, titleText);
            }
            Place();
            Animate();
            return true;
        }

        /// <summary>Switches the stage off (the shop closed, or an elixir is picked).</summary>
        public void Hide()
        {
            if (!_on && (stageRoot == null || !stageRoot.activeSelf)) { enabled = false; return; }
            StopAuras();
            if (stageRoot != null && stageRoot.activeSelf) stageRoot.SetActive(false);
            _on = false;
            _kind = -1;
            _index = -1;
            enabled = false;
        }

        private void Update()
        {
            if (!_on) { enabled = false; return; }
            if (watchView == null || !watchView.activeInHierarchy) { Hide(); return; }
            Place();
            Animate();
        }

        // ------------------------------------------------------------------ dressing up

        private void Dress(int school, Color skinColor, string titleText)
        {
            // The aura: only the picked one plays, at its normal size.
            if (auras != null)
                for (int i = 0; i < auras.Length; i++)
                {
                    ParticleSystem a = auras[i];
                    if (a == null) continue;
                    bool on = _kind == KIND_AURA && i == _index;
                    if (on)
                    {
                        a.transform.localScale = Vector3.one;
                        a.Clear();
                        a.Play();
                    }
                    else
                    {
                        if (a.isPlaying) a.Stop();
                        a.Clear();
                    }
                    if (auraMaxFx != null && i < auraMaxFx.Length && auraMaxFx[i] != null && auraMaxFx[i].isPlaying) auraMaxFx[i].Stop();
                }
            bool surge = Surge();
            if (pipPlate != null && pipPlate.activeSelf != surge) pipPlate.SetActive(surge);

            // The title.
            bool title = _kind == KIND_TITLE;
            if (titleRoot != null && titleRoot.gameObject.activeSelf != title) titleRoot.gameObject.SetActive(title);
            if (title && titleLabel != null) titleLabel.text = titleText.Length > 0 ? "« " + titleText + " »" : "";

            // The casting symbol, in the skin's colour (same school style as in a duel).
            bool skin = _kind == KIND_SKIN && glyph != null;
            if (glyph != null && glyph.gameObject.activeSelf != skin) glyph.gameObject.SetActive(skin);
            if (skin)
            {
                if (_glyphMat == null) _glyphMat = glyph.material;
                int n = glyphTex != null ? glyphTex.Length : 0;
                int gi = school >= 0 && school < n - 1 ? school : n - 1;
                if (gi >= 0 && glyphTex[gi] != null) _glyphMat.SetTexture("_MainTex", glyphTex[gi]);
                _glyphMat.SetFloat("_Style", gi == n - 1 ? 0f : gi + 1f);
                _glyphMat.SetColor("_Color", skinColor);
                _glyphMat.SetColor("_EdgeColor", Color.Lerp(skinColor, Color.white, 0.55f));
                _glyphMat.SetFloat("_Reveal", 0f);
                _glyphMat.SetFloat("_Fade", 1f);
                _glyphMat.SetFloat("_Flare", 0f);
            }
        }

        private void StopAuras()
        {
            if (auras != null)
                for (int i = 0; i < auras.Length; i++)
                    if (auras[i] != null) { if (auras[i].isPlaying) auras[i].Stop(); auras[i].Clear(); }
            if (auraMaxFx != null)
                for (int i = 0; i < auraMaxFx.Length; i++)
                    if (auraMaxFx[i] != null && auraMaxFx[i].isPlaying) auraMaxFx[i].Stop();
        }

        /// <summary>The picked item is a school Surge (it has a full-pips eruption).</summary>
        private bool Surge()
        {
            return _kind == KIND_AURA && auraMaxFx != null && _index >= 0 && _index < auraMaxFx.Length && auraMaxFx[_index] != null;
        }

        // ------------------------------------------------------------------ framing

        /// <summary>Keeps the stage on you: auras at your feet, the title over your head, the camera swinging round in front.</summary>
        private void Place()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            float eye = me.GetAvatarEyeHeightAsMeters();
            if (eye < 0.2f) eye = 1.6f;
            _eye = eye;
            Vector3 at = me.GetPosition();
            Vector3 fwd = me.GetRotation() * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            if (feet != null)
            {
                feet.position = at;
                feet.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }

            float swing = Mathf.Sin(Time.time * orbitSpeed) * orbitDegrees;
            _dir = Quaternion.Euler(0f, swing, 0f) * fwd;
            Vector3 target = at + Vector3.up * (eye * 0.72f);
            _camPos = target + _dir * (eye * distance) + Vector3.up * (eye * 0.06f);
            if (previewCamera != null)
            {
                Transform ct = previewCamera.transform;
                ct.position = _camPos;
                ct.rotation = Quaternion.LookRotation(target - _camPos, Vector3.up);
            }

            if (_kind == KIND_TITLE && titleRoot != null)
            {
                Vector3 head = me.GetBonePosition(HumanBodyBones.Head);
                if (head == Vector3.zero) head = at + Vector3.up * eye;
                titleRoot.position = head + Vector3.up * titleHeight;
                Face(titleRoot);
            }
            if (_kind == KIND_SKIN && glyph != null)
            {
                float s = eye / 1.6f;
                Transform gt = glyph.transform;
                gt.position = at + Vector3.up * (eye * 0.7f) + _dir * (0.55f * s);
                Face(gt);
            }
        }

        /// <summary>Turns t to face the camera, staying upright (readable from the front, like titles and symbols in the world).</summary>
        private void Face(Transform t)
        {
            Vector3 d = t.position - _camPos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0004f) t.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        // ------------------------------------------------------------------ the loops

        private void Animate()
        {
            float t = Time.time - _start;
            if (Surge()) AnimateSurge(t);
            if (_kind == KIND_SKIN && _glyphMat != null) AnimateGlyph(t);
        }

        /// <summary>
        /// A Surge through a duel, every 9 seconds: one pip a second (the aura grows from a little under to well over its
        /// quiet size, as PlayerCosmetics does), a cast at 3.5 s (the outward blast), and at 7 pips the eruption.
        /// </summary>
        private void AnimateSurge(float t)
        {
            float ct = t % 9f;
            int pips = Mathf.Min(maxPips, Mathf.FloorToInt(ct * maxPips / 7f));
            float fill = pips / (float)Mathf.Max(1, maxPips);
            float size = 0.75f + 0.75f * fill;
            ParticleSystem a = auras[_index];
            if (a != null) a.transform.localScale = new Vector3(size, size, size);

            bool max = pips >= maxPips;
            ParticleSystem m = auraMaxFx[_index];
            if (max && !m.isPlaying) m.Play();
            else if (!max && m.isPlaying) m.Stop();

            bool wrapped = ct < _lastCycle;
            if ((wrapped || _lastCycle < 3.5f) && ct >= 3.5f && ct < 4.5f && Time.time - _castAt > 2f)
            {
                Flare();
                _castAt = Time.time;
            }
            _lastCycle = ct;

            if (pipLabel != null)
            {
                string held = "", empty = "";
                for (int k = 0; k < maxPips; k++) { if (k < pips) held += "●"; else empty += "●"; }
                string text = "<color=#FFD36A>" + held + "</color><color=#41517F>" + empty + "</color>   ";
                if (max) text += "<color=#FFE9A0><b>Full pips: it erupts!</b></color>";
                else if (Time.time - _castAt < 0.9f) text += "<color=#9FE8FF><b>Cast!</b></color>";
                else text += pips.ToString() + " / " + maxPips.ToString() + " pips";
                pipLabel.text = text;
            }
        }

        /// <summary>The outward blast when a Surge's wearer casts (as PlayerCosmetics.Flare).</summary>
        private void Flare()
        {
            if (auraFlares == null || _index < 0 || _index >= auraFlares.Length || auraFlares[_index] == null) return;
            ParticleSystem f = auraFlares[_index];
            f.Emit(70);
            ParticleSystem ring = f.transform.childCount > 0 ? f.transform.GetChild(0).GetComponent<ParticleSystem>() : null;
            if (ring != null) ring.Emit(48);
        }

        /// <summary>
        /// The symbol, every 3.6 seconds: traced over 1.4 s, held, flaring as the spell is released at 1.6 s, faded out by
        /// 3.3 s (BattleClient.AnimateGlyph's timing, without the pen).
        /// </summary>
        private void AnimateGlyph(float t)
        {
            float gt = t % 3.6f;
            float reveal = Mathf.Clamp01(gt / 1.4f);
            float flare = gt >= 1.6f ? Mathf.Max(0f, 2.2f - (gt - 1.6f) * 6f) : 0f;
            float fade = gt < 2.8f ? 1f : Mathf.Clamp01(1f - (gt - 2.8f) / 0.5f);
            _glyphMat.SetFloat("_Reveal", reveal < 1f ? reveal : 1.05f);
            _glyphMat.SetFloat("_Fade", fade);
            _glyphMat.SetFloat("_Flare", flare);
            float swell = 1f + Mathf.Min(0.12f, flare * 0.06f);
            glyph.transform.localScale = Vector3.one * (glyphSize * (_eye / 1.6f) * swell);
        }
    }
}
