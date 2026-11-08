using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Exploring pays (World of Warcraft style): the first time your wizard walks into a street, town, boss tower or
    /// secret place, a harp-like chime plays, "Discovered: (its name)" comes up on the HUD and in the book's notices, and
    /// you get a little XP. Remembered per wizard in the save (CharacterData.discoveredA/B), so each place counts once.
    /// Local: only your own wizard hears and sees it.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AreaDiscovery : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public UiAudio uiAudio;
        public NotificationCenter notifications;

        [Header("Places (same order everywhere; at most 62)")]
        public string[] placeNames;
        [Tooltip("World-space boxes, one per place.")]
        public Vector3[] boundsMin;
        public Vector3[] boundsMax;

        [Header("Reward")]
        [Tooltip("XP for a discovery: this, plus XP Per Level for each level the wizard has.")]
        public int baseXp = 20;
        public int xpPerLevel = 10;
        [Tooltip("Seconds between checks of where you are.")]
        public float checkInterval = 0.5f;
        [Tooltip("Nothing is discovered in the first seconds after a wizard is loaded (spawning, a teleport home).")]
        public float settleSeconds = 4f;

        private float _next;
        private float _readySince = -1f;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + checkInterval;
            if (playerState == null || !playerState.HasCharacter() || playerState.inCombat || placeNames == null || boundsMin == null || boundsMax == null)
            {
                _readySince = -1f;
                return;
            }
            if (_readySince < 0f) _readySince = Time.time;
            if (Time.time - _readySince < settleSeconds) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 p = me.GetPosition();
            CharacterData c = playerState.current;
            int n = Mathf.Min(62, Mathf.Min(placeNames.Length, Mathf.Min(boundsMin.Length, boundsMax.Length)));
            for (int i = 0; i < n; i++)
            {
                if (c.IsDiscovered(i)) continue;
                Vector3 a = boundsMin[i], b = boundsMax[i];
                if (p.x < a.x || p.y < a.y || p.z < a.z || p.x > b.x || p.y > b.y || p.z > b.z) continue;
                Discover(c, i);
                return;   // one at a time
            }
        }

        private void Discover(CharacterData c, int i)
        {
            c.SetDiscovered(i);
            int xp = Mathf.Max(0, baseXp + xpPerLevel * c.level);
            if (xp > 0) c.AddXp(xp);
            playerState.MarkDirty();
            playerState.SaveNow();
            if (uiAudio != null) uiAudio.PlayDiscovery();
            if (notifications != null)
                notifications.PostQuiet(NotificationCenter.KIND_INFO, "Discovered: " + placeNames[i], xp > 0 ? "+" + xp.ToString() + " XP for exploring." : "", 0);
        }
    }
}
