using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>Trigger zone on the hub's arena teleporter: stepping on it joins the battle queue, stepping off leaves it.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BattleQueuePad : UdonSharpBehaviour
    {
        public BattleClient client;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal && client != null) client.LocalQueue(true);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal && client != null) client.LocalQueue(false);
        }
    }
}
