using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Where your wizard is, was, and wants to get back to (local to each player; saved per character slot by SaveManager):
    ///   - The last place: every few seconds, while you stand somewhere you could come back to (the castle, the keep, a
    ///     street or a town; not in a duel, a dungeon, a boss's tower or any other place of its own), where you are is
    ///     remembered. Choosing that character on the title stage fades the scene out and back in right there
    ///     (ArriveAtLastPlace). If the spot is no longer good (the street was rebuilt and it's inside a wall or over
    ///     nothing), you arrive in the castle instead.
    ///   - Mark Location and Teleport to Mark, like Wizard101's: marking costs a little mana and remembers the spot (one
    ///     mark per character; a new mark replaces the old); teleporting to it is free and works from anywhere outside a
    ///     duel, with a quick fade. A rune ring glows at the mark, and the map shows it. On desktop, Page Down marks and
    ///     End teleports; everyone has the two buttons on the book's Map page.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LocationKeeper : UdonSharpBehaviour
    {
        // Solid things to stand on and not be inside of: everything but triggers' layers, players, UI and pickups.
        private const int SolidMask = ~((1 << 2) | (1 << 4) | (1 << 5) | (1 << 9) | (1 << 10) | (1 << 12) | (1 << 13) | (1 << 17) | (1 << 18));

        public PlayerState playerState;
        public SaveManager saveManager;
        public TeleportHelper teleporter;
        public AreaCuller culler;
        public SceneFade fade;
        public NotificationCenter notifications;
        public UiAudio uiAudio;
        public InventoryBook book;
        public AwakeningSequence awakening;
        [Tooltip("Told when the scene has gone dark after a character was chosen (it switches the title stage off).")]
        public TitleScreen titleScreen;

        [Header("Places")]
        [Tooltip("Moved to wherever you're being sent.")]
        public Transform spot;
        [Tooltip("Where you arrive when there's no last place (a new save) or it's no longer good: the castle.")]
        public Transform fallback;
        [Tooltip("World-space boxes you can be remembered in and mark in (the castle, the keep, each street and town), and their names.")]
        public Vector3[] safeMin;
        public Vector3[] safeMax;
        public string[] safeNames;
        [Tooltip("World-space boxes inside those where you can't (dungeons: the party goes in by the sigil).")]
        public Vector3[] blockedMin;
        public Vector3[] blockedMax;

        [Header("Marking")]
        [Tooltip("Marking costs this share of your maximum mana (%), at least 1.")]
        public int markCostPercent = 10;
        [Tooltip("The glowing ring shown at your mark.")]
        public Transform markRing;
        public KeyCode markKey = KeyCode.PageDown;
        public KeyCode teleportKey = KeyCode.End;

        [Header("Timing (seconds)")]
        [Tooltip("Choosing a character: the scene fades out this slowly, and back in this slowly.")]
        public float arriveFadeOut = 2.6f;
        public float arriveFadeIn = 2.8f;
        public float markFadeOut = 0.6f;
        public float markFadeIn = 0.9f;
        [Tooltip("How often where you stand is remembered.")]
        public float rememberEvery = 5f;

        private int _slot = -1;
        private bool _hasMark;
        private Vector3 _markPos;
        private float _markYaw;

        private float _nextRemember;
        private Vector3 _remembered;
        private bool _hasRemembered;

        private bool _travelling;
        private bool _toMark;
        private bool _hasDest;
        private Vector3 _dest;
        private float _destYaw;
        private float _fadeIn;

        public bool IsTravelling() { return _travelling; }
        public bool HasMark() { SyncSlot(); return _hasMark; }
        public Vector3 MarkPosition() { return _markPos; }

        private void Start()
        {
            if (markRing != null) markRing.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ which character

        /// <summary>The active character changed: its mark is loaded (or cleared).</summary>
        private void SyncSlot()
        {
            int slot = playerState != null ? playerState.activeSlot : -1;
            if (slot == _slot) return;
            _slot = slot;
            _hasRemembered = false;
            _hasMark = slot >= 0 && saveManager != null && saveManager.LoadPlace(slot, SaveManager.PLACE_MARK);
            if (_hasMark)
            {
                _markPos = saveManager.PlacePosition();
                _markYaw = saveManager.PlaceYaw();
            }
            ShowRing();
        }

        private void ShowRing()
        {
            if (markRing == null) return;
            if (_hasMark) markRing.SetPositionAndRotation(_markPos + Vector3.up * 0.06f, Quaternion.Euler(0f, _markYaw, 0f));
            if (markRing.gameObject.activeSelf != _hasMark) markRing.gameObject.SetActive(_hasMark);
        }

        // ------------------------------------------------------------------ where you can be remembered

        /// <summary>The safe box holding a position (-1: nowhere you can come back to, or inside a dungeon).</summary>
        public int PlaceIndex(Vector3 p)
        {
            if (safeMin == null || safeMax == null) return -1;
            int found = -1;
            for (int i = 0; i < safeMin.Length && i < safeMax.Length; i++)
            {
                Vector3 a = safeMin[i], b = safeMax[i];
                if (p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z) { found = i; break; }
            }
            if (found < 0) return -1;
            if (blockedMin != null && blockedMax != null)
                for (int i = 0; i < blockedMin.Length && i < blockedMax.Length; i++)
                {
                    Vector3 a = blockedMin[i], b = blockedMax[i];
                    if (p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z) return -1;
                }
            return found;
        }

        public string PlaceName(Vector3 p)
        {
            int i = PlaceIndex(p);
            return i >= 0 && safeNames != null && i < safeNames.Length ? safeNames[i] : "";
        }

        private bool Busy()
        {
            return playerState == null || !playerState.HasCharacter() || playerState.inCombat || _travelling || (awakening != null && awakening.IsRunning());
        }

        private void Update()
        {
            SyncSlot();
            if (_slot < 0) return;
            if (Input.GetKeyDown(markKey)) Mark();
            else if (Input.GetKeyDown(teleportKey)) TeleportToMark();
            if (Time.time < _nextRemember) return;
            _nextRemember = Time.time + Mathf.Max(1f, rememberEvery);
            Remember();
        }

        /// <summary>Remembers where you stand as the character's last place, if it's somewhere you can come back to.</summary>
        public void Remember()
        {
            if (Busy() || saveManager == null) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || !me.IsPlayerGrounded()) return;
            Vector3 p = me.GetPosition();
            if (PlaceIndex(p) < 0) return;
            if (_hasRemembered && (p - _remembered).sqrMagnitude < 1f) return;
            _remembered = p;
            _hasRemembered = true;
            saveManager.SetPlace(_slot, SaveManager.PLACE_LAST, p, me.GetRotation().eulerAngles.y);
        }

        // ------------------------------------------------------------------ mark and teleport

        /// <summary>Mark Location (the Map page's button, Page Down).</summary>
        public void Mark()
        {
            SyncSlot();
            if (_slot < 0 || saveManager == null) return;
            if (Busy()) { Say("Mark Location", "Not now."); return; }
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 p = me.GetPosition();
            if (!me.IsPlayerGrounded() || PlaceIndex(p) < 0) { Say("Mark Location", "You can't mark a location here."); return; }
            int cost = Mathf.Max(1, playerState.MaxMana() * Mathf.Max(0, markCostPercent) / 100);
            if (playerState.GetMana() < cost) { Say("Mark Location", "Not enough mana (it takes " + cost.ToString() + ")."); return; }
            playerState.SpendMana(cost);
            _markPos = p;
            _markYaw = me.GetRotation().eulerAngles.y;
            _hasMark = true;
            saveManager.SetPlace(_slot, SaveManager.PLACE_MARK, _markPos, _markYaw);
            ShowRing();
            if (uiAudio != null) uiAudio.PlayOk();
            string where = PlaceName(p);
            if (notifications != null)
                notifications.PostQuiet(NotificationCenter.KIND_INFO, "Location marked", (where.Length > 0 ? where + " " : "") + "(-" + cost.ToString() + " mana)", 0);
        }

        /// <summary>Teleport to Mark (the Map page's button, End).</summary>
        public void TeleportToMark()
        {
            SyncSlot();
            if (_slot < 0) return;
            if (!_hasMark) { Say("Teleport to Mark", "You haven't marked a location yet."); return; }
            if (playerState != null && playerState.inCombat) { Say("Teleport to Mark", "Not during a duel."); return; }
            if (Busy()) return;
            if (book != null) book.CloseBook();
            if (uiAudio != null) uiAudio.PlayDiscovery();
            _toMark = true;
            BeginTravel(true, _markPos, _markYaw, markFadeOut, markFadeIn);
        }

        private void Say(string title, string body)
        {
            if (uiAudio != null) uiAudio.PlayCancel();
            if (notifications != null) notifications.PostQuiet(NotificationCenter.KIND_INFO, title, body, 0);
        }

        // ------------------------------------------------------------------ arriving

        /// <summary>
        /// A character has just been chosen (it's the active one): the scene fades out slowly, picture, music and
        /// ambience together, and back in where that character last logged off (or in the castle).
        /// </summary>
        public void ArriveAtLastPlace()
        {
            SyncSlot();
            bool has = _slot >= 0 && saveManager != null && saveManager.LoadPlace(_slot, SaveManager.PLACE_LAST);
            Vector3 pos = has ? saveManager.PlacePosition() : Vector3.zero;
            float yaw = has ? saveManager.PlaceYaw() : 0f;
            if (has && PlaceIndex(pos) < 0) has = false;
            _travelling = false;   // a new arrival overrides whatever was under way
            _toMark = false;
            BeginTravel(has, pos, yaw, arriveFadeOut, arriveFadeIn);
        }

        private void BeginTravel(bool hasDest, Vector3 pos, float yaw, float outSeconds, float inSeconds)
        {
            if (_travelling) return;
            _travelling = true;
            _hasDest = hasDest;
            _dest = pos;
            _destYaw = yaw;
            _fadeIn = inSeconds;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me)) me.Immobilize(true);
            if (fade != null) fade.FadeOut(outSeconds);
            SendCustomEventDelayedSeconds(nameof(_TravelDark), outSeconds + 0.1f);
        }

        /// <summary>It's dark: switch the destination on (its ground needs to be there before it's checked).</summary>
        public void _TravelDark()
        {
            if (!_travelling) return;
            if (_toMark && playerState != null && playerState.inCombat)
            {
                // Pulled into a duel as the fade began: stay for it.
                _travelling = false;
                if (fade != null) fade.FadeIn(0.5f);
                return;
            }
            if (_hasDest && culler != null) culler.Prepare(_dest);
            SendCustomEventDelayedSeconds(nameof(_TravelLand), 0.5f);
        }

        public void _TravelLand()
        {
            if (!_travelling) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            bool good = _hasDest && GoodSpot(_dest);
            if (good && spot != null)
            {
                spot.SetPositionAndRotation(_dest + Vector3.up * 0.05f, Quaternion.Euler(0f, _destYaw, 0f));
                if (teleporter != null) teleporter.TeleportTo(spot);
            }
            else if (_toMark)
            {
                // The mark is somewhere that isn't there any more (the street was rebuilt): it's dropped, and you stay put.
                _hasMark = false;
                if (saveManager != null && _slot >= 0) saveManager.ClearPlace(_slot, SaveManager.PLACE_MARK);
                ShowRing();
                if (notifications != null) notifications.PostQuiet(NotificationCenter.KIND_INFO, "Teleport to Mark", "That spot is gone; your mark was lost.", 0);
            }
            else if (teleporter != null && fallback != null) teleporter.TeleportTo(fallback);
            // The title stage is done with once you've left it (not before: you're standing on it until now).
            if (!_toMark && titleScreen != null) titleScreen._Departed();
            if (Utilities.IsValid(me)) me.Immobilize(false);
            _hasRemembered = false;
            SendCustomEventDelayedSeconds(nameof(_TravelReveal), 0.6f);
        }

        public void _TravelReveal()
        {
            _travelling = false;
            _toMark = false;
            if (fade != null) fade.FadeIn(_fadeIn);
        }

        /// <summary>Solid ground just under the spot, and nothing solid where you'd stand.</summary>
        private bool GoodSpot(Vector3 p)
        {
            RaycastHit hit;
            if (!Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out hit, 3f, SolidMask, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - p.y) > 0.9f) return false;
            Collider[] inside = Physics.OverlapSphere(p + Vector3.up * 1.1f, 0.2f, SolidMask, QueryTriggerInteraction.Ignore);
            return inside == null || inside.Length == 0;
        }
    }
}
