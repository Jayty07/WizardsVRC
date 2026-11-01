using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Small head-following panel showing name, level, health, mana, gold and an XP bar for the active character.
    /// Lives on an always-active object; <see cref="hudRoot"/> is the canvas that gets shown/hidden.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerHUD : UdonSharpBehaviour
    {
        public PlayerState playerState;
        [Tooltip("The character creator's palette (index 0 = no signature colour: the name stays white).")]
        public Color[] lookPalette;
        public ItemDatabase itemDatabase;
        public GameObject hudRoot;
        public Text nameLabel;
        public Text healthLabel;
        public Text manaLabel;
        public Text goldLabel;
        public QuestManager questManager;
        public Text questLabel;

        [Header("Health and mana bars (left-pivoted fills; their X scale is the share left)")]
        public RectTransform healthFill;
        public RectTransform manaFill;
        [Tooltip("Shows how much was just lost (a pale trail that catches up).")]
        public RectTransform healthTrail;
        public Text levelLabel;
        public Text pointsLabel;

        [Header("Level up")]
        public ParticleSystem levelUpBurst;
        public ParticleSystem levelUpRise;
        public AudioSource levelUpAudio;
        [Tooltip("'LEVEL UP!' banner on the HUD (shown for a few seconds).")]
        public GameObject levelUpBanner;
        public Text levelUpText;
        public float levelUpSeconds = 3.5f;

        [Header("XP bar")]
        [Tooltip("Left-pivoted fill inside the bar; its X scale is the share of XP towards the next level.")]
        public RectTransform xpFill;
        public Image xpFillImage;
        public Text xpLabel;
        [Tooltip("The fill moves in whole segments (PSX look). Match the tick marks the builder draws.")]
        public int xpSegments = 20;
        [Tooltip("Share of the bar per second the fill moves when you gain XP.")]
        public float xpFillSpeed = 0.6f;
        public Color xpColor = new Color(0.72f, 0.48f, 1f);
        public Color xpFlashColor = new Color(1f, 1f, 0.85f);

        [Tooltip("Offset from the head in head space (x right, y up, z forward), metres.")]
        public Vector3 headOffset = new Vector3(-0.28f, -0.22f, 0.7f);
        [Tooltip("How quickly it turns to follow your head (it always keeps up with walking and running).")]
        public float followSpeed = 14f;
        public bool hudEnabled = true;

        private float _nextRefresh;
        private float _xpShown;
        private int _xpLevel;
        private float _xpScale = -1f;
        private float _xpFlashUntil;
        private bool _wasShown;
        private Quaternion _rot = Quaternion.identity;
        private bool _hasRot;
        private int _knownLevel;
        private int _knownSlot = -1;
        private float _bannerUntil;
        private float _hpShown = -1f, _hpTrail = -1f, _mpShown = -1f;

        public void ToggleHud()
        {
            hudEnabled = !hudEnabled;
        }

        private void Update()
        {
            bool show = hudEnabled && playerState != null && playerState.HasCharacter();
            if (hudRoot != null && hudRoot.activeSelf != show) hudRoot.SetActive(show);
            if (!show) { _wasShown = false; _hasRot = false; return; }
            // Coming back (new character, HUD toggled): the bar snaps to the current XP instead of filling up.
            if (!_wasShown) { _wasShown = true; _xpLevel = 0; }

            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            // It moves with your head exactly (no lag when you walk or run, so it never falls behind you); only turning
            // your head is eased a little, for comfort.
            Transform tr = hudRoot.transform;
            float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
            Quaternion rot = _hasRot ? Quaternion.Slerp(_rot, head.rotation, t) : head.rotation;
            _rot = rot;
            _hasRot = true;
            tr.SetPositionAndRotation(head.position + rot * headOffset, rot);

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 0.5f;
                Refresh();
            }
            UpdateXpBar();
            UpdateBars();
            CheckLevelUp();
        }

        /// <summary>The health and mana fills glide to their value; a pale trail shows health just lost.</summary>
        private void UpdateBars()
        {
            int maxHp = Mathf.Max(1, playerState.MaxHealth()), maxMp = Mathf.Max(1, playerState.MaxMana());
            float hp = Mathf.Clamp01((float)playerState.DisplayHealth() / maxHp);
            float mp = Mathf.Clamp01((float)playerState.GetMana() / maxMp);
            float k = Mathf.Clamp01(Time.deltaTime * 6f);
            _hpShown = _hpShown < 0f ? hp : Mathf.Lerp(_hpShown, hp, k);
            _mpShown = _mpShown < 0f ? mp : Mathf.Lerp(_mpShown, mp, k);
            _hpTrail = _hpTrail < 0f || hp > _hpTrail ? hp : Mathf.MoveTowards(_hpTrail, hp, Time.deltaTime * 0.25f);
            if (healthFill != null) healthFill.localScale = new Vector3(_hpShown, 1f, 1f);
            if (manaFill != null) manaFill.localScale = new Vector3(_mpShown, 1f, 1f);
            if (healthTrail != null) healthTrail.localScale = new Vector3(_hpTrail, 1f, 1f);
        }

        /// <summary>A level gained on this character: sparkle burst and rising light round you, the fanfare, a banner.</summary>
        private void CheckLevelUp()
        {
            CharacterData c = playerState.current;
            if (_knownSlot != playerState.activeSlot) { _knownSlot = playerState.activeSlot; _knownLevel = c.level; }
            if (c.level > _knownLevel && _knownLevel > 0)
            {
                _knownLevel = c.level;
                PlayLevelUp(c.level);
            }
            else if (c.level < _knownLevel) _knownLevel = c.level;
            if (levelUpBanner != null && levelUpBanner.activeSelf && Time.time > _bannerUntil) levelUpBanner.SetActive(false);
        }

        public void PlayLevelUp(int level)
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            Vector3 feet = Utilities.IsValid(me) ? me.GetPosition() : transform.position;
            if (levelUpBurst != null)
            {
                levelUpBurst.transform.position = feet + Vector3.up * 1.1f;
                levelUpBurst.Emit(70);
            }
            if (levelUpRise != null)
            {
                levelUpRise.transform.position = feet;
                levelUpRise.Play();
            }
            if (levelUpAudio != null)
            {
                levelUpAudio.transform.position = feet;
                levelUpAudio.Play();
            }
            if (levelUpText != null) levelUpText.text = "LEVEL UP!\nLevel " + level.ToString();
            if (levelUpBanner != null) levelUpBanner.SetActive(true);
            _bannerUntil = Time.time + levelUpSeconds;
        }

        /// <summary>
        /// XP fills in towards the next level. A level-up fills the bar to the end, flashes, and starts again from empty
        /// (one wrap per level gained).
        /// </summary>
        private void UpdateXpBar()
        {
            if (xpFill == null) return;
            CharacterData c = playerState.current;
            bool max = c.level >= CharacterData.MAX_LEVEL;
            int need = c.XpToNextLevel();
            float target = max ? 1f : (need > 0 ? Mathf.Clamp01((float)c.xp / need) : 0f);
            float step = xpFillSpeed * Time.deltaTime;
            if (_xpLevel <= 0 || c.level < _xpLevel)
            {
                _xpLevel = c.level;
                _xpShown = target;
            }
            else if (c.level > _xpLevel)
            {
                _xpShown = Mathf.MoveTowards(_xpShown, 1f, step * 2f);
                if (_xpShown >= 1f)
                {
                    _xpLevel++;
                    _xpShown = 0f;
                    _xpFlashUntil = Time.time + 0.35f;
                }
            }
            else _xpShown = Mathf.MoveTowards(_xpShown, target, step);

            int seg = Mathf.Max(1, xpSegments);
            float q = _xpShown >= 1f ? 1f : Mathf.Floor(_xpShown * seg) / seg;
            if (Time.time < _xpFlashUntil) q = 1f;
            if (q != _xpScale)
            {
                _xpScale = q;
                xpFill.localScale = new Vector3(q, 1f, 1f);
            }
            if (xpFillImage != null)
            {
                Color want = Time.time < _xpFlashUntil ? xpFlashColor : xpColor;
                if (xpFillImage.color != want) xpFillImage.color = want;
            }
        }

        public void Refresh()
        {
            if (playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            if (nameLabel != null)
            {
                nameLabel.text = c.characterName;
                nameLabel.color = c.signatureColor > 0 && lookPalette != null && c.signatureColor < lookPalette.Length ? lookPalette[c.signatureColor] : Color.white;
            }
            if (levelLabel != null) levelLabel.text = c.level.ToString();
            if (healthLabel != null) healthLabel.text = playerState.DisplayHealth().ToString() + " / " + playerState.MaxHealth().ToString();
            if (manaLabel != null) manaLabel.text = playerState.GetMana().ToString() + " / " + playerState.MaxMana().ToString();
            if (goldLabel != null) goldLabel.text = c.gold.ToString();
            if (pointsLabel != null) pointsLabel.text = c.trainingPoints.ToString() + " TP";
            if (xpLabel != null)
                xpLabel.text = c.level >= CharacterData.MAX_LEVEL ? "XP  MAX"
                    : "XP " + c.xp.ToString() + " / " + c.XpToNextLevel().ToString();
            if (questLabel != null) questLabel.text = questManager != null ? questManager.GetTrackerText() : "";
        }
    }
}
