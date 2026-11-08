using UdonSharp;

namespace WizardsVRC
{
    /// <summary>
    /// The casting wand (a local VRC Pickup, not synced): tells CastPattern when it's taken from your hip and let go
    /// (it goes back to the holster).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PatternWand : UdonSharpBehaviour
    {
        public CastPattern pattern;

        public override void OnPickup()
        {
            if (pattern != null) pattern._WandGrabbed();
        }

        public override void OnDrop()
        {
            if (pattern != null) pattern._WandDropped();
        }
    }
}
