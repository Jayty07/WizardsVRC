using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// The handle under the battle hand of cards (a local VRC Pickup, not synced): grab it and the hand follows; let go
    /// and it stays there, relative to you, for every fight after (BattleClient.PlaceHud).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HudGrip : UdonSharpBehaviour
    {
        public BattleClient client;

        public override void OnPickup()
        {
            if (client != null) client._GripStart();
        }

        public override void OnDrop()
        {
            if (client != null) client._GripEnd();
        }
    }
}
