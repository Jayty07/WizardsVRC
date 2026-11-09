using UdonSharp;

namespace WizardsVRC
{
    /// <summary>
    /// The casting tools (local VRC Pickups, not synced): the wand pickup (which shows the wand, sabre, baton or lantern
    /// for the equipped wand's casting style) and the Dominion sidearm. Tells CastPattern when one is taken from your hip
    /// and let go (it goes back to the holster), and when the sidearm's trigger is pulled.
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

        public override void OnPickupUseDown()
        {
            if (pattern != null) pattern._WandFire();
        }
    }
}
