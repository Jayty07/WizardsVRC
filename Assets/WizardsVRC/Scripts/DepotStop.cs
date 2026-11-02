using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A railroad stop (Archrock's depot, or a street's whistle stop by its way in): walk past it and it's reached for good
    /// (Railroad.Reach); click it for the timetable. Local.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DepotStop : UdonSharpBehaviour
    {
        public Railroad railroad;
        public int index;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (railroad != null && Utilities.IsValid(player) && player.isLocal) railroad.Reach(index);
        }

        public override void Interact()
        {
            if (railroad == null) return;
            railroad.Reach(index);
            railroad.OpenAt(index);
        }
    }
}
