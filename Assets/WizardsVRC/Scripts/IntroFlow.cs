using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// First-join sequence: school showcase + title cutscene, then the Headmaster's greeting,
    /// then the character select asks the player to pick a school.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class IntroFlow : UdonSharpBehaviour
    {
        public CutscenePlayer player;
        public Cutscene introCutscene;
        public Cutscene headmasterCutscene;
        public CharacterSelectUI selectUI;
        public float startDelay = 2f;

        private void Start()
        {
            SendCustomEventDelayedSeconds(nameof(_StartIntro), startDelay);
        }

        public void _StartIntro()
        {
            if (player != null && introCutscene != null) player.Play(introCutscene);
            else OnIntroFinished();
        }

        public void OnIntroFinished()
        {
            if (player != null && headmasterCutscene != null) player.Play(headmasterCutscene);
            else OnHeadmasterFinished();
        }

        public void OnHeadmasterFinished()
        {
            if (selectUI != null) selectUI.PromptSchool();
        }
    }
}
