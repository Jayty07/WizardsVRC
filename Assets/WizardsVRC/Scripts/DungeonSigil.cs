using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace WizardsVRC
{
    /// <summary>
    /// A Wizard101-style dungeon entrance: a sigil on the ground with four pads. Step onto a pad and a countdown starts;
    /// others can step onto the free pads until it runs out, then everyone on a pad goes in together. While that party is
    /// inside, the dungeon is theirs: the sigil shows it's in use and nobody else can enter (the way in is barred; the
    /// only way in is the sigil). It opens again once the whole party has left: through an exit, by going home, by
    /// leaving the instance. The referee is the owner; everyone reports their own pad and their own leaving.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DungeonSigil : UdonSharpBehaviour
    {
        public const int PADS = 4;
        public const int STATE_OPEN = 0, STATE_COUNTDOWN = 1, STATE_INSIDE = 2;

        [Header("Sigil")]
        public string dungeonName = "the dungeon";
        [Tooltip("The four pads' centres.")]
        public Transform[] pads;
        public float padRadius = 1.1f;
        public Renderer[] padRenderers;
        public Material padIdle;
        public Material padTaken;
        public Text statusLabel;
        public int countdownSeconds = 10;

        [Header("Inside")]
        [Tooltip("Where the party arrives inside (they're spread out sideways from it).")]
        public Transform start;
        [Tooltip("Where you come out (by the sigil).")]
        public Transform outside;
        [Tooltip("World-space box round the dungeon's inside: a party member found outside it has left.")]
        public Vector3 boundsMin;
        public Vector3 boundsMax;

        [UdonSynced] public int state;
        [UdonSynced] public int countdownEndMs;
        [UdonSynced] public int[] padIds = new int[PADS];
        [UdonSynced] public int[] party = new int[PADS];
        [UdonSynced] public int entryId;

        private int _myPad = -1;
        private float _nextCheck;
        private int _enteredId = -1;
        private float _enteredAt = -100f;
        private bool _leaveSent;
        private string _lastText = "";

        private int Now() { return Networking.GetServerTimeInMilliseconds(); }

        private void Start()
        {
            if (padIds == null || padIds.Length != PADS) padIds = new int[PADS];
            if (party == null || party.Length != PADS) party = new int[PADS];
            RefreshVisuals();
        }

        // ------------------------------------------------------------------ local player

        private void Update()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            if (Networking.IsOwner(gameObject)) RefereeTick();
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.2f;
            Vector3 pos = me.GetPosition();
            int id = me.playerId;

            if (state == STATE_INSIDE && InParty(id))
            {
                // Arrived (or still there)? Left by any route other than the exit (going home, a teleport)?
                if (_enteredId == entryId && !_leaveSent && Time.time - _enteredAt > 4f && !Inside(pos))
                {
                    _leaveSent = true;
                    SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetLeave), id);
                }
                return;
            }

            int pad = -1;
            if (state != STATE_INSIDE && pads != null)
                for (int i = 0; i < pads.Length && i < PADS; i++)
                {
                    if (pads[i] == null) continue;
                    Vector3 d = pos - pads[i].position;
                    if (Mathf.Abs(d.y) < 2f && d.x * d.x + d.z * d.z < padRadius * padRadius) { pad = i; break; }
                }
            if (pad != _myPad)
            {
                _myPad = pad;
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetPad), id, pad);
            }
            UpdateLabel();
        }

        private bool Inside(Vector3 p)
        {
            return p.x >= boundsMin.x && p.x <= boundsMax.x && p.y >= boundsMin.y && p.y <= boundsMax.y && p.z >= boundsMin.z && p.z <= boundsMax.z;
        }

        private bool InParty(int id)
        {
            if (id <= 0 || party == null) return false;
            for (int i = 0; i < party.Length; i++) if (party[i] == id) return true;
            return false;
        }

        /// <summary>The exit's door (EventRelay): out to the sigil.</summary>
        public void LeaveDungeon()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            if (outside != null) me.TeleportTo(outside.position, outside.rotation);
            if (!_leaveSent && InParty(me.playerId))
            {
                _leaveSent = true;
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(NetLeave), me.playerId);
            }
        }

        public override void OnDeserialization()
        {
            CheckEntry();
            RefreshVisuals();
        }

        /// <summary>The party has just been formed with me in it: in we go.</summary>
        private void CheckEntry()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || state != STATE_INSIDE || _enteredId == entryId || !InParty(me.playerId) || start == null) return;
            _enteredId = entryId;
            _enteredAt = Time.time;
            _leaveSent = false;
            _myPad = -1;
            int slot = 0;
            for (int i = 0; i < party.Length; i++) if (party[i] == me.playerId) slot = i;
            Vector3 side = start.right * ((slot - 1.5f) * 1.6f);
            me.TeleportTo(start.position + side, start.rotation);
        }

        // ------------------------------------------------------------------ referee (owner)

        [NetworkCallable]
        public void NetPad(int playerId, int pad)
        {
            if (!Networking.IsOwner(gameObject) || state == STATE_INSIDE) return;
            for (int i = 0; i < PADS; i++) if (padIds[i] == playerId) padIds[i] = 0;
            if (pad >= 0 && pad < PADS && padIds[pad] == 0) padIds[pad] = playerId;
            bool any = false;
            for (int i = 0; i < PADS; i++) any |= padIds[i] != 0;
            if (any && state == STATE_OPEN)
            {
                state = STATE_COUNTDOWN;
                countdownEndMs = Now() + countdownSeconds * 1000;
            }
            else if (!any) state = STATE_OPEN;
            Publish();
        }

        [NetworkCallable]
        public void NetLeave(int playerId)
        {
            if (!Networking.IsOwner(gameObject)) return;
            RemoveFromParty(playerId);
            Publish();
        }

        private void RemoveFromParty(int playerId)
        {
            bool anyLeft = false;
            for (int i = 0; i < PADS; i++)
            {
                if (party[i] == playerId) party[i] = 0;
                if (padIds[i] == playerId) padIds[i] = 0;
                anyLeft |= party[i] != 0;
            }
            if (state == STATE_INSIDE && !anyLeft) state = STATE_OPEN;
        }

        private void RefereeTick()
        {
            if (state != STATE_COUNTDOWN || Now() - countdownEndMs < 0) return;
            int n = 0;
            for (int i = 0; i < PADS; i++)
            {
                party[i] = padIds[i];
                if (padIds[i] != 0) n++;
                padIds[i] = 0;
            }
            if (n == 0) state = STATE_OPEN;
            else
            {
                state = STATE_INSIDE;
                entryId++;
            }
            Publish();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !Networking.IsOwner(gameObject)) return;
            RemoveFromParty(player.playerId);
            bool any = false;
            for (int i = 0; i < PADS; i++) any |= padIds[i] != 0;
            if (state == STATE_COUNTDOWN && !any) state = STATE_OPEN;
            Publish();
        }

        private void Publish()
        {
            RequestSerialization();
            CheckEntry();
            RefreshVisuals();
        }

        // ------------------------------------------------------------------ looks

        private void RefreshVisuals()
        {
            if (padRenderers != null)
                for (int i = 0; i < padRenderers.Length && i < PADS; i++)
                {
                    if (padRenderers[i] == null) continue;
                    Material want = state != STATE_INSIDE && padIds != null && padIds[i] != 0 ? padTaken : padIdle;
                    if (want != null && padRenderers[i].sharedMaterial != want) padRenderers[i].sharedMaterial = want;
                }
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (statusLabel == null) return;
            string t;
            if (state == STATE_INSIDE)
            {
                int n = 0;
                for (int i = 0; i < PADS; i++) if (party[i] != 0) n++;
                t = dungeonName + "\nA party of " + n + (n == 1 ? " wizard is" : " wizards are") + " inside. Wait for them to come out.";
            }
            else if (state == STATE_COUNTDOWN)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt((countdownEndMs - Now()) / 1000f));
                t = dungeonName + "\nEntering in " + left + "...  Step onto a free pad to join.";
            }
            else t = dungeonName + "\nStand on the sigil's pads to enter (up to 4 wizards).";
            if (t != _lastText)
            {
                _lastText = t;
                statusLabel.text = t;
            }
        }
    }
}
