using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// First join: once the saves have loaded, a player with no characters gets the opening (the awakening, the
    /// Headmaster's visions and the character creator: <see cref="AwakeningSequence"/>). Without an awakening sequence
    /// the old school picker is used. With a title screen, nothing happens until its Begin is pressed: a new player is
    /// taken to the study and it calls <see cref="Decide"/>; a returning player gets the character select on the title
    /// stage itself (New Character there plays the opening too) and never comes through here.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class IntroFlow : UdonSharpBehaviour
    {
        public AwakeningSequence awakening;
        public CharacterSelectUI selectUI;
        public SaveManager saveManager;
        [Tooltip("The title screen: when set, the decision waits for its Begin button.")]
        public TitleScreen titleScreen;
        [Tooltip("Seconds after joining before the opening starts (lets the world finish loading).")]
        public float startDelay = 2f;

        private bool _decided;

        private void Start()
        {
            if (titleScreen != null) return;
            SendCustomEventDelayedSeconds(nameof(_Check), startDelay);
        }

        /// <summary>The title screen's Begin (you're in the study now): the opening or the character select.</summary>
        public void Decide()
        {
            _Check();
        }

        public void _Check()
        {
            if (_decided) return;
            if (saveManager == null || selectUI == null) { if (awakening != null) awakening.EndBlackout(); return; }
            if (!saveManager.isReady)
            {
                SendCustomEventDelayedSeconds(nameof(_Check), 0.5f);
                return;
            }
            _decided = true;
            int slot = selectUI.IsNewPlayer() ? selectUI.FirstEmptySlot() : -1;
            if (slot < 0)
            {
                // Returning: nothing to play, open your eyes on the character select.
                if (awakening != null) awakening.EndBlackout();
                return;
            }
            if (awakening != null) awakening.Begin(slot);
            else selectUI.PromptSchool();
        }
    }
}
