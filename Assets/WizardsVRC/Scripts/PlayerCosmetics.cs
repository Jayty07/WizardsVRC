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
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerCosmetics : UdonSharpBehaviour
    {
        public CosmeticsDatabase database;
        public Transform titleRoot;
        public Text titleLabel;
        [Tooltip("One particle system per aura (index = aura), under this object; only the worn one plays.")]
        public ParticleSystem[] auras;
        public float titleHeight = 0.55f;
        [Tooltip("Titles and auras are drawn for players within this distance of you.")]
        public float drawRange = 45f;

        [UdonSynced] public int title;   // index + 1, 0 = none
        [UdonSynced] public int aura;
        [UdonSynced] public int skin;

        private VRCPlayerApi _owner;
        private int _shownTitle = -1;
        private int _shownAura = -1;
        private bool _registered;

        private void Start()
        {
            _owner = Networking.GetOwner(gameObject);
            Register();
            Apply();
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
            _owner = player;
            Register();
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
                if (titleRoot != null) titleRoot.gameObject.SetActive(t.Length > 0);
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
            }
        }

        /// <summary>Colour this wizard's spells are drawn in (white = their school's own look).</summary>
        public Color SkinColor() { return database != null && skin > 0 ? database.SkinColor(skin) : Color.white; }
        public bool HasSkin() { return skin > 0; }

        private void LateUpdate()
        {
            if (_owner == null || !Utilities.IsValid(_owner)) { _owner = Networking.GetOwner(gameObject); if (_owner == null) return; }
            if (title == 0 && aura == 0) return;
            Vector3 feet = _owner.GetPosition();
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && !_owner.isLocal && (me.GetPosition() - feet).sqrMagnitude > drawRange * drawRange)
            {
                if (titleRoot != null && titleRoot.gameObject.activeSelf) titleRoot.gameObject.SetActive(false);
                return;
            }
            transform.position = feet;
            if (titleRoot != null && title > 0)
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
