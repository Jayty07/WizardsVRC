using UdonSharp;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>A chair, stool or bench seat: click it to sit (uses the VRCStation on the same object).</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Seat : UdonSharpBehaviour
    {
        public override void Interact()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me)) me.UseAttachedStation();
        }
    }
}
