using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// One scene of the story (CUTSCENES.md): a cast and a list of beats, played for one player by
    /// <see cref="CutscenePlayer"/>. Local: only whoever is in the scene sees its cast act.
    ///
    /// A beat is one entry across the arrays below. Most are lines of speech (who says it, their words, an optional
    /// voice clip, how they act it out and how it is framed); the rest are directions: a wait, a fade, a card of text
    /// over black, a question put to the player, someone gesturing, turning, arriving or leaving, a cutaway.
    /// The scene builder writes them from the story's dialogue (WizardsVRCSceneBuilder.Cutscenes.cs): plain lines,
    /// "Name|words" for someone else in the scene, [tags] and @directions.
    ///
    /// The cast are bodies in the world (the character you clicked, anyone else who speaks and stands nearby) and
    /// stand-ins: copies of characters who belong elsewhere, kept out of sight and brought on at marks beside the first
    /// of the cast for as long as the scene lasts. When the scene ends (or is skipped) <see cref="onFinishedEvent"/> is
    /// sent to <see cref="onFinished"/>.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Cutscene : UdonSharpBehaviour
    {
        // What a beat is.
        public const int BEAT_SAY = 0, BEAT_WAIT = 1, BEAT_FADE_OUT = 2, BEAT_FADE_IN = 3, BEAT_CARD = 4, BEAT_CHOICE = 5,
                         BEAT_EMOTE = 6, BEAT_LOOK = 7, BEAT_ENTER = 8, BEAT_EXIT = 9, BEAT_SHOT = 10;
        // How a beat is framed (0 = the director picks).
        public const int SHOT_AUTO = 0, SHOT_WIDE = 1, SHOT_MEDIUM = 2, SHOT_CLOSE = 3, SHOT_SHOULDER = 4, SHOT_YOU = 5, SHOT_GROUP = 6;
        // How a speaker acts a line out (0 = the director picks from the words; 1 = they keep still).
        public const int EMOTE_AUTO = 0, EMOTE_NONE = 1, EMOTE_TALK = 2, EMOTE_NOD = 3, EMOTE_SHAKE = 4, EMOTE_THINK = 5, EMOTE_POINT = 6,
                         EMOTE_BOW = 7, EMOTE_SURPRISE = 8, EMOTE_CHEER = 9, EMOTE_LAUGH = 10, EMOTE_ANGRY = 11, EMOTE_SHRUG = 12,
                         EMOTE_WAVE = 13, EMOTE_SAD = 14;
        public const int EMOTE_COUNT = 15;
        // Who a beat is about, when it isn't one of the cast.
        public const int ACTOR_NOBODY = -1, ACTOR_PLAYER = -2;

        public const int STANCE_ALLY = 0;
        public const int STANCE_NEUTRAL = 1;
        public const int STANCE_HOSTILE = 2;

        public string cutsceneId = "";
        public bool skippable = true;

        [Header("The beats (one entry each)")]
        public string[] speakers;
        [Tooltip("Per line: 0 ally (blue), 1 neutral (yellow), 2 hostile (red); empty = guessed from the speaker's name.")]
        public int[] stances;
        [Tooltip("A line's words; a card's text; a choice's question and answers, split by |.")]
        [TextArea] public string[] lines;
        /// <summary>Voice clip per line; empty entries are read at the player's pace (or timed, on Auto).</summary>
        public AudioClip[] voices;
        [Tooltip("BEAT_*: empty = every entry is a line of speech.")]
        public int[] kinds;
        [Tooltip("Seconds: how long a wait, a fade, a card or a cutaway lasts.")]
        public float[] amounts;
        [Tooltip("0 = always played; 1-3 = only after that answer to the last question.")]
        public int[] branches;
        [Tooltip("EMOTE_*: how the speaker acts the line out, or the gesture of an emote beat.")]
        public int[] emotes;
        [Tooltip("SHOT_*: how the beat is framed.")]
        public int[] framings;
        [Tooltip("Who each beat is about: an index into the cast, -1 = nobody on stage, -2 = the player.")]
        public int[] lineActors;
        [Tooltip("Who a look beat turns them towards (the same numbering; -1 = back the way they stood).")]
        public int[] targets;

        [Header("The camera")]
        [Tooltip("The director frames each beat from where the cast and the player stand. Off: the placed shots below, a line each, as before.")]
        public bool directed;
        /// <summary>Placed camera transform per line (lines past the end reuse the last). With the director on these are what it falls back on when it can't see.</summary>
        public Transform[] shots;
        /// <summary>Metres per second the camera drifts forward during a shot.</summary>
        public float dollySpeed = 0.15f;

        [Header("The cast")]
        [Tooltip("Each body's root (the first is whoever the scene is with).")]
        public Transform[] actors;
        public string[] actorNames;
        [Tooltip("Their Animators (Talk and the emote triggers).")]
        public Animator[] actorAnimators;
        [Tooltip("How high their face is above their feet.")]
        public float[] actorFaces;
        [Tooltip("True for a stand-in: kept hidden, brought on beside the first of the cast for the scene.")]
        public bool[] actorStandIns;
        [Tooltip("True for one who walks on later (an @enter beat) rather than being there from the start.")]
        public bool[] actorLate;
        [Tooltip("A stand-in's real self (their body's Model, wherever they belong): out of sight while the stand-in is on, so there are never two of them.")]
        public GameObject[] actorOriginals;

        public UdonSharpBehaviour onFinished;
        public string onFinishedEvent = "";

        public int LineCount() { return lines == null ? 0 : lines.Length; }
        public string SpeakerAt(int i) { return speakers != null && i >= 0 && i < speakers.Length ? speakers[i] : ""; }

        public int StanceAt(int i)
        {
            if (stances != null && i >= 0 && i < stances.Length) return stances[i];
            string s = SpeakerAt(i);
            if (s.StartsWith("Professor") || s.StartsWith("Headmaster") || s.StartsWith("You")) return STANCE_ALLY;
            return STANCE_NEUTRAL;
        }

        public string LineAt(int i) { return lines != null && i >= 0 && i < lines.Length ? lines[i] : ""; }
        public AudioClip VoiceAt(int i) { return voices != null && i >= 0 && i < voices.Length ? voices[i] : null; }
        public int KindAt(int i) { return kinds != null && i >= 0 && i < kinds.Length ? kinds[i] : BEAT_SAY; }
        public float AmountAt(int i) { return amounts != null && i >= 0 && i < amounts.Length ? amounts[i] : 0f; }
        public int BranchAt(int i) { return branches != null && i >= 0 && i < branches.Length ? branches[i] : 0; }
        public int EmoteAt(int i) { return emotes != null && i >= 0 && i < emotes.Length ? emotes[i] : EMOTE_AUTO; }
        public int FramingAt(int i) { return framings != null && i >= 0 && i < framings.Length ? framings[i] : SHOT_AUTO; }
        public int TargetAt(int i) { return targets != null && i >= 0 && i < targets.Length ? targets[i] : ACTOR_NOBODY; }

        /// <summary>Who beat i is about: an index into the cast, ACTOR_NOBODY or ACTOR_PLAYER. (No table: the first of the cast speaks every line.)</summary>
        public int ActorAt(int i)
        {
            if (lineActors != null && i >= 0 && i < lineActors.Length) return lineActors[i];
            return ActorCount() > 0 ? 0 : ACTOR_NOBODY;
        }

        public int ActorCount() { return actors == null ? 0 : actors.Length; }
        public Transform ActorBody(int a) { return actors != null && a >= 0 && a < actors.Length ? actors[a] : null; }
        public Animator ActorAnimator(int a) { return actorAnimators != null && a >= 0 && a < actorAnimators.Length ? actorAnimators[a] : null; }
        public float ActorFace(int a) { return actorFaces != null && a >= 0 && a < actorFaces.Length && actorFaces[a] > 0.1f ? actorFaces[a] : 1.6f; }
        public bool IsStandIn(int a) { return actorStandIns != null && a >= 0 && a < actorStandIns.Length && actorStandIns[a]; }
        public bool IsLate(int a) { return actorLate != null && a >= 0 && a < actorLate.Length && actorLate[a]; }
        public GameObject ActorOriginal(int a) { return actorOriginals != null && a >= 0 && a < actorOriginals.Length ? actorOriginals[a] : null; }

        public Transform ShotAt(int i)
        {
            if (shots == null || shots.Length == 0) return null;
            return shots[Mathf.Clamp(i, 0, shots.Length - 1)];
        }
    }
}
