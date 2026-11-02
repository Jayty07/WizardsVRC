using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// One per player (a VRChat player object): what that wizard is wearing (title, aura, spell skin), synced so everyone
    /// sees it. Draws the title over their head (turned to face you, upright) and their aura round their feet, in the
    /// open world and in duels. The owner's CosmeticsDatabase sets it with SetLook.
    /// The world's creator (by VRChat display name, checked on each player's own client, never synced or saved) always
    /// has their creator title floating over their head, 3D letters bobbing in a wave (built by
    /// WizardsVRCSceneBuilder.CreatorTitle.cs), above any title they wear.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerCosmetics : UdonSharpBehaviour
    {
        public CosmeticsDatabase database;
        public Transform titleRoot;
        public Text titleLabel;
        [Tooltip("One particle system per aura (index = aura), under this object; only the worn one plays.")]
        public ParticleSystem[] auras;
        [Tooltip("Per aura: the burst when its wearer casts (school Surges only; empty for the rest).")]
        public ParticleSystem[] auraFlares;
        [Tooltip("Per aura: the eruption at full pips (school Surges only).")]
        public ParticleSystem[] auraMaxFx;
        public float titleHeight = 0.55f;
        [Tooltip("Titles and auras are drawn for players within this distance of you.")]
        public float drawRange = 45f;

        [Header("The world's creator")]
        [Tooltip("VRChat display name that gets the creator title (exact match).")]
        public string creatorName = "Jayty07";
        [Tooltip("The creator's floating 3D letters (under Title Root, so they turn to face each viewer too).")]
        public GameObject creatorRoot;

        [UdonSynced] public int title;   // index + 1, 0 = none
        [UdonSynced] public int aura;
        [UdonSynced] public int skin;

        private VRCPlayerApi _owner;
        private int _shownTitle = -1;
        private int _shownAura = -1;
        private bool _registered;
        private bool _creator;
        private int _pips = -1;      // pips held in the duel you're in (-1 = not in one); local to each viewer
        private int _maxPips = 7;
        private bool _listed;

        private void Start()
        {
            SetOwner(Networking.GetOwner(gameObject));
            Register();
            if (database != null && !_listed) { _listed = true; database.ListCosmetics(this); }
            Apply();
        }

        /// <summary>The player wearing this (0 = nobody yet).</summary>
        public int OwnerId() { return Utilities.IsValid(_owner) ? _owner.playerId : 0; }

        // ------------------------------------------------------------------ Surge auras in duels (local to each viewer)

        /// <summary>
        /// The duel's pips for this wizard (BattleClient, on every client watching): a Surge aura grows with them and
        /// erupts at full pips. pips &lt; 0 = out of the duel (back to its quiet form).
        /// </summary>
        public void SetPower(int pips, int maxPips)
        {
            _pips = pips;
            _maxPips = Mathf.Max(1, maxPips);
            ApplyPower();
        }

        /// <summary>This wizard just cast a spell: their Surge aura blasts outward.</summary>
        public void Flare()
        {
            int i = aura - 1;
            if (auraFlares == null || i < 0 || i >= auraFlares.Length || auraFlares[i] == null) return;
            auraFlares[i].Emit(70);
            ParticleSystem ring = auraFlares[i].transform.childCount > 0 ? auraFlares[i].transform.GetChild(0).GetComponent<ParticleSystem>() : null;
            if (ring != null) ring.Emit(48);
        }

        private void ApplyPower()
        {
            if (auras == null) return;
            int worn = aura - 1;
            float fill = _pips < 0 ? -1f : Mathf.Clamp01(_pips / (float)_maxPips);
            for (int i = 0; i < auras.Length; i++)
            {
                bool surge = auraMaxFx != null && i < auraMaxFx.Length && auraMaxFx[i] != null;
                if (!surge) continue;
                // Quiet outside a duel; in one it swells from a little under to well over its quiet size.
                float size = fill < 0f ? 0.8f : 0.75f + 0.75f * fill;
                if (auras[i] != null) auras[i].transform.localScale = new Vector3(size, size, size);
                bool max = i == worn && fill >= 1f;
                ParticleSystem m = auraMaxFx[i];
                if (max && !m.isPlaying) m.Play();
                else if (!max && m.isPlaying) m.Stop();
            }
        }

        /// <summary>Who wears this, and whether that's the world's creator.</summary>
        private void SetOwner(VRCPlayerApi player)
        {
            _owner = player;
            bool creator = Utilities.IsValid(player) && creatorName.Length > 0 && player.displayName == creatorName;
            if (creator != _creator)
            {
                _creator = creator;
                _shownTitle = -1;   // redraw the title
                Apply();
            }
        }

        private void Register()
        {
            if (_registered || database == null || !Networking.IsOwner(gameObject)) return;
            _registered = true;
            database.localCosmetics = this;
            database.PushLook();
        }

        public void SetLook(int newTitle, int newAura, int newSkin)
        {
            if (!Networking.IsOwner(gameObject)) return;
            if (title == newTitle && aura == newAura && skin == newSkin) return;
            title = newTitle;
            aura = newAura;
            skin = newSkin;
            RequestSerialization();
            Apply();
        }

        public override void OnDeserialization() { Apply(); }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            SetOwner(player);
            Register();
            _pips = -1;
            ApplyPower();
        }

        private void Apply()
        {
            if (_shownTitle != title)
            {
                _shownTitle = title;
                string t = database != null ? database.TitleName(title) : "";
                if (titleLabel != null)
                {
                    titleLabel.text = t.Length > 0 ? "« " + t + " »" : "";
                    titleLabel.color = new Color(1f, 0.86f, 0.45f);
                }
                // The creator's letters float over the worn title (or on their own).
                if (creatorRoot != null && creatorRoot.activeSelf != _creator) creatorRoot.SetActive(_creator);
                if (titleRoot != null) titleRoot.gameObject.SetActive(t.Length > 0 || _creator);
            }
            if (_shownAura != aura)
            {
                _shownAura = aura;
                if (auras != null)
                    for (int i = 0; i < auras.Length; i++)
                    {
                        if (auras[i] == null) continue;
                        bool on = i == aura - 1;
                        if (on && !auras[i].isPlaying) auras[i].Play();
                        else if (!on && auras[i].isPlaying) auras[i].Stop();
                    }
                ApplyPower();
            }
        }

        /// <summary>Colour this wizard's spells are drawn in (white = their school's own look).</summary>
        public Color SkinColor() { return database != null && skin > 0 ? database.SkinColor(skin) : Color.white; }
        public bool HasSkin() { return skin > 0; }

        private void LateUpdate()
        {
            if (_owner == null || !Utilities.IsValid(_owner)) { SetOwner(Networking.GetOwner(gameObject)); if (_owner == null) return; }
            if (title == 0 && aura == 0 && !_creator) return;
            Vector3 feet = _owner.GetPosition();
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && !_owner.isLocal && (me.GetPosition() - feet).sqrMagnitude > drawRange * drawRange)
            {
                if (titleRoot != null && titleRoot.gameObject.activeSelf) titleRoot.gameObject.SetActive(false);
                return;
            }
            transform.position = feet;
            if (titleRoot != null && (title > 0 || _creator))
            {
                if (!titleRoot.gameObject.activeSelf) titleRoot.gameObject.SetActive(true);
                Vector3 head = _owner.GetBonePosition(HumanBodyBones.Head);
                if (head == Vector3.zero) head = feet + Vector3.up * 1.6f;
                titleRoot.position = head + Vector3.up * titleHeight;
                if (Utilities.IsValid(me))
                {
                    Vector3 d = titleRoot.position - me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0004f) titleRoot.rotation = Quaternion.LookRotation(d, Vector3.up);
                }
            }
        }
    }
}
