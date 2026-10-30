using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// First join: once the saves have loaded, a player with no characters gets the opening (the awakening, the
    /// Headmaster's visions and the character creator: <see cref="AwakeningSequence"/>). Returning players stay at the
    /// character select in the Headmaster's study; New Character there plays the opening too. Without an awakening
    /// sequence the old school picker is used.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class IntroFlow : UdonSharpBehaviour
    {
        public AwakeningSequence awakening;
        public CharacterSelectUI selectUI;
        public SaveManager saveManager;
        [Tooltip("Seconds after joining before the opening starts (lets the world finish loading).")]
        public float startDelay = 2f;

        private bool _decided;

        private void Start()
        {
            SendCustomEventDelayedSeconds(nameof(_Check), startDelay);
        }

        public void _Check()
        {
            if (_decided) return;
            if (saveManager == null || selectUI == null) return;
            if (!saveManager.isReady)
            {
                SendCustomEventDelayedSeconds(nameof(_Check), 0.5f);
                return;
            }
            _decided = true;
            if (!selectUI.IsNewPlayer()) return;
            int slot = selectUI.FirstEmptySlot();
            if (slot < 0) return;
            if (awakening != null) awakening.Begin(slot);
            else selectUI.PromptSchool();
        }
    }
}
