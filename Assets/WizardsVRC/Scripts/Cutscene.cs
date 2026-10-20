using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Data for one voiced cutscene: per line a speaker, subtitle, optional voice clip and camera shot.
    /// Played by <see cref="CutscenePlayer"/>; when it ends (or is skipped) <see cref="onFinishedEvent"/> is sent to <see cref="onFinished"/>.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Cutscene : UdonSharpBehaviour
    {
        public string cutsceneId = "";
        public bool skippable = true;
        public string[] speakers;
        [TextArea] public string[] lines;
        /// <summary>Voice clip per line; empty entries fall back to a reading-time delay.</summary>
        public AudioClip[] voices;
        /// <summary>Camera transform per line; lines past the end reuse the last shot.</summary>
        public Transform[] shots;
        /// <summary>Metres per second the camera drifts forward during a shot.</summary>
        public float dollySpeed = 0.15f;

        public UdonSharpBehaviour onFinished;
        public string onFinishedEvent = "";

        public int LineCount() { return lines == null ? 0 : lines.Length; }
        public string SpeakerAt(int i) { return speakers != null && i < speakers.Length ? speakers[i] : ""; }
        public string LineAt(int i) { return lines != null && i < lines.Length ? lines[i] : ""; }
        public AudioClip VoiceAt(int i) { return voices != null && i < voices.Length ? voices[i] : null; }

        public Transform ShotAt(int i)
        {
            if (shots == null || shots.Length == 0) return null;
            return shots[Mathf.Clamp(i, 0, shots.Length - 1)];
        }
    }
}
