using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Local (2D) interface sounds. Buttons call these from their click / pointer-enter events (wired by the scene
    /// builder from each button's name), and scripts call them for the book, cards, dialogue and so on.
    /// Each sound plays only for the player who triggered it.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UiAudio : UdonSharpBehaviour
    {
        public AudioSource source;
        [Range(0f, 1f)] public float volume = 0.7f;
        [Tooltip("Hover sounds closer together than this are skipped (pointer events can repeat).")]
        public float hoverGap = 0.06f;

        [Header("Buttons")]
        public AudioClip click;
        public AudioClip hover;
        public AudioClip ok;
        public AudioClip cancel;
        public AudioClip delete;
        public AudioClip nextClick;
        public AudioClip nextHover;
        public AudioClip tab;
        public AudioClip hudHover;

        [Header("Game")]
        public AudioClip bookOpen;
        public AudioClip cardHover;
        public AudioClip cardClick;
        public AudioClip spellSelect;
        public AudioClip nameSpin;
        public AudioClip popup;
        public AudioClip heal;

        private float _lastHover;

        private void Play(AudioClip clip)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip, volume);
        }

        private void PlayHoverClip(AudioClip clip)
        {
            if (Time.time - _lastHover < hoverGap) return;
            _lastHover = Time.time;
            Play(clip);
        }

        public void PlayClick() { Play(click); }
        public void PlayHover() { PlayHoverClip(hover); }
        public void PlayHudHover() { PlayHoverClip(hudHover); }
        public void PlayOk() { Play(ok); }
        public void PlayCancel() { Play(cancel); }
        public void PlayDelete() { Play(delete); }
        public void PlayNextClick() { Play(nextClick); }
        public void PlayNextHover() { PlayHoverClip(nextHover); }
        public void PlayTab() { Play(tab); }
        public void PlayBookOpen() { Play(bookOpen); }
        public void PlayCardHover() { PlayHoverClip(cardHover); }
        public void PlayCardClick() { Play(cardClick); }
        public void PlaySpellSelect() { Play(spellSelect); }
        public void PlayNameSpin() { Play(nameSpin); }
        public void PlayPopup() { Play(popup); }
        public void PlayHeal() { Play(heal); }
    }
}
