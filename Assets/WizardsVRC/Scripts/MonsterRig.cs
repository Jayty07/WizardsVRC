using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// A monster's animated body (built by WizardsVRCSceneBuilder.Rigs.cs): an Animator with keyframed Idle, Walk, Hit,
    /// Die and Cast clips, and the monster's voice for casting, getting hit and dying. Battle bodies stand idle and
    /// BattleClient calls Cast / Hit / Die as the round plays back; patrol bodies just walk. Switching the body off and on
    /// again (between fights) puts it back to its first state.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MonsterRig : UdonSharpBehaviour
    {
        public Animator animator;
        public AudioSource voice;
        public AudioClip castClip;
        public AudioClip hitClip;
        public AudioClip dieClip;
        [Tooltip("A little pitch variety per monster (and per sound).")]
        public float pitch = 1f;

        public void Cast()
        {
            if (animator != null) animator.SetTrigger("Cast");
            Say(castClip);
        }

        public void Hit()
        {
            if (animator != null) animator.SetTrigger("Hit");
            Say(hitClip);
        }

        /// <summary>A flinch without the voice (a spell's waves landing before its finisher).</summary>
        public void Flinch()
        {
            if (animator != null) animator.SetTrigger("Hit");
        }

        public void Die()
        {
            if (animator != null) animator.SetTrigger("Die");
            Say(dieClip);
        }

        private void Say(AudioClip clip)
        {
            if (voice == null || clip == null || !voice.gameObject.activeInHierarchy) return;
            voice.pitch = pitch * Random.Range(0.94f, 1.06f);
            voice.PlayOneShot(clip);
        }
    }
}
