using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Trigger zone that feeds a battle queue: stepping in joins it, stepping out leaves it. The hub's arena teleporter
    /// pad, and the duelling circle (Keep Queued: anyone standing in the zone is put back in the countdown, so wizards
    /// who walked in during a duel join the next one).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BattleQueuePad : UdonSharpBehaviour
    {
        public BattleClient client;
        [Tooltip("While the local player is inside, keep asking to be queued (duel zones).")]
        public bool keepQueued;
        [Tooltip("Keep Queued: the zone's radius round this object (a missed exit, e.g. after a teleport, is caught by it).")]
        public float zoneRadius = 10f;

        private bool _inside;
        private float _next;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal || client == null) return;
            _inside = true;
            _next = Time.time + 1.5f;
            client.LocalQueue(true);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal || client == null) return;
            _inside = false;
            client.LocalQueue(false);
        }

        private void Update()
        {
            if (!keepQueued || !_inside || client == null || Time.time < _next) return;
            _next = Time.time + 1.5f;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 d = me.GetPosition() - transform.position;
            d.y = 0f;
            if (d.magnitude > zoneRadius) { _inside = false; return; }
            client.LocalStillInside();
        }
    }
}
