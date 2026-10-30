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
        public float followSpeed = 8f;
        public bool hudEnabled = true;

        private float _nextRefresh;
        private float _xpShown;
        private int _xpLevel;
        private float _xpScale = -1f;
        private float _xpFlashUntil;
        private bool _wasShown;

        public void ToggleHud()
        {
            hudEnabled = !hudEnabled;
        }

        private void Update()
        {
            bool show = hudEnabled && playerState != null && playerState.HasCharacter();
            if (hudRoot != null && hudRoot.activeSelf != show) hudRoot.SetActive(show);
            if (!show) { _wasShown = false; return; }
            // Coming back (new character, HUD toggled): the bar snaps to the current XP instead of filling up.
            if (!_wasShown) { _wasShown = true; _xpLevel = 0; }

            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            VRCPlayerApi.TrackingData head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 targetPos = head.position + head.rotation * headOffset;
            Quaternion targetRot = head.rotation;
            float t = Mathf.Clamp01(Time.deltaTime * followSpeed);
            Transform tr = hudRoot.transform;
            tr.position = Vector3.Lerp(tr.position, targetPos, t);
            tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, t);

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + 0.5f;
                Refresh();
            }
            UpdateXpBar();
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
                nameLabel.text = c.characterName + "  Lv " + c.level.ToString();
                nameLabel.color = c.signatureColor > 0 && lookPalette != null && c.signatureColor < lookPalette.Length ? lookPalette[c.signatureColor] : Color.white;
            }
            if (healthLabel != null) healthLabel.text = "Health " + playerState.DisplayHealth().ToString() + " / " + playerState.MaxHealth().ToString();
            if (manaLabel != null) manaLabel.text = "Mana " + playerState.GetMana().ToString() + " / " + playerState.MaxMana().ToString();
            if (goldLabel != null) goldLabel.text = "Gold " + c.gold.ToString() + "    Training Points " + c.trainingPoints.ToString();
            if (xpLabel != null)
                xpLabel.text = c.level >= CharacterData.MAX_LEVEL ? "XP  MAX"
                    : "XP " + c.xp.ToString() + " / " + c.XpToNextLevel().ToString();
            if (questLabel != null) questLabel.text = questManager != null ? questManager.GetTrackerText() : "";
        }
    }
}
