using System.Collections.Generic;
using System.Globalization;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Story scenes (CUTSCENES.md): turns the story's dialogue into the beats a <see cref="Cutscene"/> plays, gives every
    /// scene its cast, and builds the screen the director (<see cref="CutscenePlayer"/>) plays them on.
    ///
    ///   The script. A scene is a list of rows, written wherever dialogue is written (an NPC's default lines, a quest's
    ///   offer, a Talk step, a boss's intro lines). A row is a line of speech or a direction:
    ///     words                        spoken by the scene's own character
    ///     Name|words                   spoken by Name ("!Name|words": an enemy, red; "You|words": your own line)
    ///     Name|[nod, close] words      [tags] before the words: how it is acted (still, talk, nod, shake, think, point,
    ///                                  bow, surprise, cheer, laugh, angry, shrug, wave, sad), how it is framed (wide,
    ///                                  medium, close, shoulder, you, group) and who it is said to ([to Name], [to you])
    ///     @wait 1.5                    a pause
    ///     @fade out 1 / @fade in 1     to black and back
    ///     @card Words over black       a title card (the next line or shot fades back in)
    ///     @cast Name, Name, Name       who else is in the scene, in the order they stand (speakers join by themselves;
    ///                                  this is for the order, and for those who never speak)
    ///     @shot close Name 2           a cutaway ("@shot wide Name with Other": the two of them)
    ///     @emote Name laugh 1.5        someone gestures without speaking
    ///     @look Name at Other          someone turns (at you; away = back the way they stood)
    ///     @enter Name / @exit Name     someone walks on, or off
    ///     @choice Prompt|A|B|C         a question for the player; rows starting "1> ", "2> ", "3> " play only after
    ///                                  that answer, rows without go on for everyone
    ///   A line with no [acting] tag gets one picked from its words (PickEmote); one with no framing is framed by the
    ///   director as it plays. Voice clips are numbered by spoken line (prefix_00.wav, _01...): directions don't count.
    ///
    ///   The cast. Every NPC body registers under its name (NpcModel). When the scene's own character has a body, the
    ///   scene is directed: each speaker (and anyone a direction names) who has a body joins the cast. One who stands
    ///   within a few steps of the first plays the part where they stand; anyone further off, or in another area, is
    ///   played by a stand-in: a copy kept out of sight and brought on beside the first for as long as the scene lasts
    ///   (the real one, if in view, steps out meanwhile). Scenes whose own character has no body (a monster speaking
    ///   from its duel ring) keep their placed shots.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>A body within this far of the scene's first character (and in the same area) plays its own part.</summary>
        private const float CastNearby = 14f;
        private const int CastLimit = 8;

        private class NpcBody
        {
            public string title, key;
            public Transform root;
            public NpcLook look;
            public float faceY;
        }

        private class SceneBeat
        {
            public int kind = Cutscene.BEAT_SAY;
            public string speaker = "";
            public bool hostile;
            public string text = "";
            public float amount;
            public int branch;
            public int emote = Cutscene.EMOTE_AUTO;
            public int framing = Cutscene.SHOT_AUTO;
            /// <summary>Who the beat is about (a line's speaker, a direction's subject), and who they speak or turn to.</summary>
            public string who = "", whom = "";
        }

        private class SceneScript
        {
            public string name, lead;
            public Cutscene cutscene;
            public List<SceneBeat> beats;
            /// <summary>The names an "@cast" row lists, in order.</summary>
            public List<string> cast = new List<string>();
        }

        private static readonly Dictionary<string, NpcBody> NpcBodies = new Dictionary<string, NpcBody>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, NpcBody> StandIns = new Dictionary<string, NpcBody>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly List<SceneScript> SceneScripts = new List<SceneScript>();
        private static Transform _standInRoot;

        /// <summary>Called at the start of a rebuild.</summary>
        private static void ResetCutscenes()
        {
            NpcBodies.Clear();
            StandIns.Clear();
            SceneScripts.Clear();
            _standInRoot = null;
        }

        /// <summary>The layer the film camera never draws (and nothing bumps into): the screen itself, name tags, markers.</summary>
        private static int OffCameraLayer()
        {
            int layer = LayerMask.NameToLayer("Walkthrough");
            return layer < 0 ? 17 : layer;
        }

        /// <summary>Keeps a floating tag or marker out of the films (you still see it with your own eyes).</summary>
        private static void OffCamera(GameObject go)
        {
            if (go == null) return;
            int layer = OffCameraLayer();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>Every NPC body, by the name it speaks under (NpcModel calls this).</summary>
        private static void RegisterNpcBody(string title, string objectName, Transform root, NpcLook look, float faceY)
        {
            if (string.IsNullOrEmpty(title) || NpcBodies.ContainsKey(title)) return;
            string key = objectName != null && objectName.StartsWith("NPC_") ? objectName.Substring(4) : objectName;
            NpcBodies[title] = new NpcBody { title = title, key = key, root = root, look = look, faceY = faceY };
        }

        // ================================================================== the script

        private static readonly Dictionary<string, int> EmoteNames = new Dictionary<string, int>
        {
            { "still", Cutscene.EMOTE_NONE }, { "none", Cutscene.EMOTE_NONE }, { "talk", Cutscene.EMOTE_TALK }, { "nod", Cutscene.EMOTE_NOD },
            { "shake", Cutscene.EMOTE_SHAKE }, { "think", Cutscene.EMOTE_THINK }, { "point", Cutscene.EMOTE_POINT }, { "bow", Cutscene.EMOTE_BOW },
            { "surprise", Cutscene.EMOTE_SURPRISE }, { "surprised", Cutscene.EMOTE_SURPRISE }, { "cheer", Cutscene.EMOTE_CHEER },
            { "laugh", Cutscene.EMOTE_LAUGH }, { "angry", Cutscene.EMOTE_ANGRY }, { "shrug", Cutscene.EMOTE_SHRUG }, { "wave", Cutscene.EMOTE_WAVE },
            { "sad", Cutscene.EMOTE_SAD },
        };

        private static readonly Dictionary<string, int> FramingNames = new Dictionary<string, int>
        {
            { "wide", Cutscene.SHOT_WIDE }, { "medium", Cutscene.SHOT_MEDIUM }, { "close", Cutscene.SHOT_CLOSE }, { "shoulder", Cutscene.SHOT_SHOULDER },
            { "you", Cutscene.SHOT_YOU }, { "group", Cutscene.SHOT_GROUP },
        };

        /// <summary>A scene's rows as beats (see the class comment for how rows are written); "@cast" rows add to 'cast'.</summary>
        private static List<SceneBeat> ParseScene(string scene, string lead, string[] rows, List<string> cast)
        {
            var beats = new List<SceneBeat>();
            if (rows == null) return beats;
            string lastSpeaker = null;
            int turn = 0;
            foreach (string raw in rows)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string row = raw.Trim();
                var b = new SceneBeat();
                // "2> ...": only after the second answer to the last question.
                if (row.Length > 2 && row[0] >= '1' && row[0] <= '3' && row[1] == '>')
                {
                    b.branch = row[0] - '0';
                    row = row.Substring(2).TrimStart();
                }
                if (row.ToLowerInvariant().StartsWith("@cast "))
                {
                    foreach (string name in row.Substring(6).Split(','))
                        if (name.Trim().Length > 0 && cast != null) cast.Add(name.Trim());
                    continue;
                }
                if (row.StartsWith("@"))
                {
                    if (ParseDirection(scene, row, b)) beats.Add(b);
                    continue;
                }
                string who = lead ?? "";
                int bar = row.IndexOf('|');
                if (bar > 0 && bar < 64)
                {
                    who = row.Substring(0, bar).Trim();
                    row = row.Substring(bar + 1).Trim();
                    if (who.StartsWith("!"))
                    {
                        b.hostile = true;
                        who = who.Substring(1).Trim();
                    }
                }
                b.kind = Cutscene.BEAT_SAY;
                b.text = ReadTags(scene, row, b);
                b.speaker = who;
                b.who = who;
                if (who == lastSpeaker) turn++; else turn = 0;
                lastSpeaker = who;
                if (b.emote == Cutscene.EMOTE_AUTO) b.emote = PickEmote(b.text, b.hostile, turn);
                beats.Add(b);
            }
            return beats;
        }

        /// <summary>Takes the [tags] off the front of a line's words and applies them to its beat. Returns the words.</summary>
        private static string ReadTags(string scene, string words, SceneBeat b)
        {
            words = words.TrimStart();
            bool any = false;
            while (words.StartsWith("["))
            {
                int end = words.IndexOf(']');
                if (end < 0) break;
                string[] parts = words.Substring(1, end - 1).Split(',');
                // Words that happen to open with a bracket ("[The door creaks.]") are words, not tags.
                bool tags = false;
                foreach (string part in parts)
                {
                    string low = part.Trim().ToLowerInvariant();
                    if (low.StartsWith("to ") || EmoteNames.ContainsKey(low) || FramingNames.ContainsKey(low)) tags = true;
                }
                if (!tags && !any) break;
                foreach (string part in parts)
                {
                    string tag = part.Trim(), low = tag.ToLowerInvariant();
                    if (tag.Length == 0) continue;
                    if (low.StartsWith("to ")) b.whom = tag.Substring(3).Trim();
                    else if (EmoteNames.ContainsKey(low)) b.emote = EmoteNames[low];
                    else if (FramingNames.ContainsKey(low)) b.framing = FramingNames[low];
                    else Debug.LogWarning("[WizardsVRC Cutscenes] " + scene + ": unknown tag [" + tag + "] (left out). See CUTSCENES.md.");
                }
                any = true;
                words = words.Substring(end + 1).TrimStart();
            }
            return words;
        }

        /// <summary>A number at the end of a direction (its seconds), taken off it; 'fallback' when it has none.</summary>
        private static float TakeSeconds(ref string rest, float fallback)
        {
            rest = rest.Trim();
            int sp = rest.LastIndexOf(' ');
            string last = sp < 0 ? rest : rest.Substring(sp + 1);
            float v;
            if (last.Length == 0 || !float.TryParse(last, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return fallback;
            rest = sp < 0 ? "" : rest.Substring(0, sp).Trim();
            return Mathf.Max(0f, v);
        }

        /// <summary>An "@..." row. False (with a warning) when it can't be read.</summary>
        private static bool ParseDirection(string scene, string row, SceneBeat b)
        {
            string body = row.Substring(1).Trim();
            int sp = body.IndexOf(' ');
            string verb = (sp < 0 ? body : body.Substring(0, sp)).ToLowerInvariant();
            string rest = sp < 0 ? "" : body.Substring(sp + 1).Trim();
            b.speaker = "";
            switch (verb)
            {
                case "wait":
                    b.kind = Cutscene.BEAT_WAIT;
                    b.amount = TakeSeconds(ref rest, 1f);
                    return true;

                case "fade":
                {
                    string low = rest.ToLowerInvariant();
                    bool fadeIn = low.StartsWith("in");
                    if (!fadeIn && !low.StartsWith("out")) break;
                    b.kind = fadeIn ? Cutscene.BEAT_FADE_IN : Cutscene.BEAT_FADE_OUT;
                    rest = rest.Substring(fadeIn ? 2 : 3);
                    b.amount = TakeSeconds(ref rest, 0f);
                    return true;
                }

                case "card":
                    if (rest.Length == 0) break;
                    b.kind = Cutscene.BEAT_CARD;
                    b.text = rest.Replace("\\n", "\n");   // "\n" typed into a row is a second row of the card
                    return true;

                case "choice":
                case "ask":
                {
                    var parts = new List<string>();
                    foreach (string part in rest.Split('|'))
                        if (part.Trim().Length > 0 || parts.Count == 0) parts.Add(part.Trim());
                    if (parts.Count < 2) break;
                    if (parts.Count > 4)
                    {
                        Debug.LogWarning("[WizardsVRC Cutscenes] " + scene + ": a question takes three answers at most; the rest are dropped.");
                        parts.RemoveRange(4, parts.Count - 4);
                    }
                    b.kind = Cutscene.BEAT_CHOICE;
                    b.text = string.Join("|", parts);
                    return true;
                }

                case "shot":
                {
                    int sp2 = rest.IndexOf(' ');
                    string how = (sp2 < 0 ? rest : rest.Substring(0, sp2)).ToLowerInvariant();
                    if (!FramingNames.ContainsKey(how)) break;
                    b.kind = Cutscene.BEAT_SHOT;
                    b.framing = FramingNames[how];
                    rest = sp2 < 0 ? "" : rest.Substring(sp2 + 1);
                    b.amount = TakeSeconds(ref rest, 0f);
                    int pairAt = rest.ToLowerInvariant().IndexOf(" with ");
                    if (pairAt > 0)
                    {
                        b.whom = rest.Substring(pairAt + 6).Trim();
                        rest = rest.Substring(0, pairAt).Trim();
                    }
                    b.who = b.framing == Cutscene.SHOT_YOU ? "you" : rest;
                    return true;
                }

                case "emote":
                {
                    b.amount = TakeSeconds(ref rest, 0f);
                    int sp2 = rest.LastIndexOf(' ');
                    string how = (sp2 < 0 ? rest : rest.Substring(sp2 + 1)).ToLowerInvariant();
                    if (!EmoteNames.ContainsKey(how)) break;
                    b.kind = Cutscene.BEAT_EMOTE;
                    b.emote = EmoteNames[how];
                    b.who = sp2 < 0 ? "" : rest.Substring(0, sp2).Trim();
                    return true;
                }

                case "look":
                {
                    b.amount = TakeSeconds(ref rest, 0f);
                    string low = rest.ToLowerInvariant();
                    int at = low.IndexOf(" at ");
                    b.kind = Cutscene.BEAT_LOOK;
                    if (at > 0)
                    {
                        b.who = rest.Substring(0, at).Trim();
                        b.whom = rest.Substring(at + 4).Trim();
                        return true;
                    }
                    if (low.EndsWith(" away") || low.EndsWith(" back")) { b.who = rest.Substring(0, rest.Length - 5).Trim(); return true; }
                    break;
                }

                case "enter":
                case "exit":
                    b.amount = TakeSeconds(ref rest, 0f);
                    if (rest.Length == 0) break;
                    b.kind = verb == "enter" ? Cutscene.BEAT_ENTER : Cutscene.BEAT_EXIT;
                    b.who = rest;
                    return true;
            }
            Debug.LogWarning("[WizardsVRC Cutscenes] " + scene + ": can't read the direction \"" + row + "\" (left out). See CUTSCENES.md.");
            return false;
        }

        private static bool StartsAny(string s, params string[] starts)
        {
            foreach (string t in starts) if (s.StartsWith(t)) return true;
            return false;
        }

        private static bool HasAny(string s, params string[] bits)
        {
            foreach (string t in bits) if (s.Contains(t)) return true;
            return false;
        }

        /// <summary>
        /// How a line is acted out when its row doesn't say: read off its words. A refusal shakes its head, thanks bow, a
        /// greeting waves, bad news hangs its head, musing tilts it, directions point the way, a short cry starts back, a
        /// question opens its hands; anything else is an open hand, then a nod, turn about. ('turn': how many lines this
        /// speaker has already spoken in a row.) The rules are cautious on purpose: a wrong gesture is worse than a plain
        /// one, and a row can always say [sad] for itself. Tools/cutscene_model.py runs the same rules over the whole
        /// story, to read the picks against the lines.
        /// </summary>
        private static int PickEmote(string words, bool hostile, int turn)
        {
            string text = words.Trim();
            string low = " " + text.ToLowerInvariant() + " ";
            string head = text.ToLowerInvariant().TrimStart('.', ' ', '"', '\'');
            bool shout = text.EndsWith("!"), ask = text.EndsWith("?"), trails = text.EndsWith("...");
            if (hostile) return shout || turn == 0 ? Cutscene.EMOTE_ANGRY : (turn % 2 == 1 ? Cutscene.EMOTE_POINT : Cutscene.EMOTE_TALK);
            if (StartsAny(head, "no,", "no.", "no!", "no deal", "never", "nay", "nope", "don't", "do not", "you can't", "can't"))
                return Cutscene.EMOTE_SHAKE;
            if (HasAny(low, " thank you", " thanks", " my thanks", " grateful", " bless you", " in your debt", " honoured", " honored", " i owe you"))
                return Cutscene.EMOTE_BOW;
            if (HasAny(low, " ha ha", " haha", " ha! ", " heh", " hah", " ho ho", " hee hee")) return Cutscene.EMOTE_LAUGH;
            if (StartsAny(head, "welcome", "hello", "greetings", "well met", "hail", "good day", "good morning", "good evening", "howdy", "ahoy",
                    "farewell", "goodbye", "safe travels", "wizard!", "you came", "you're back", "back already", "ah, wizard", "ah. the wizard", "there you are"))
                return Cutscene.EMOTE_WAVE;
            if (HasAny(low, " sorry", " alas", " i fear ", " i'm afraid", " mourn", " grief", " died", " went out", " going dark", " dark mine", " never thought"))
                return Cutscene.EMOTE_SAD;
            if (StartsAny(head, "yes", "aye", "indeed", "very well", "good.", "good,", "right.", "right,", "agreed", "of course", "exactly",
                    "well done", "better.", "perfect", "much better", "not bad", "all four", "it's done", "done."))
                return Cutscene.EMOTE_NOD;
            if (StartsAny(head, "hmm", "hm.", "hm,", "strange", "curious", "odd.", "odd,") || HasAny(low, " i wonder", " perhaps", " strange ", " strange.",
                    " curious ", " curious.", " puzzl", " riddle", " i've read", " they say", " folks say", " legend"))
                return Cutscene.EMOTE_THINK;
            if (StartsAny(head, "find ", "follow ", "go ", "head ", "take the ", "look for ", "bring me ") || HasAny(low, " go to ", " head to ", " head for ",
                    " over there", " that way", " north", " south", " east", " west", " through the ", " past the ", " take the ", ". find ", " and find ",
                    " go and find ", " look for ", " bring me ", " follow its ", " follow the ", " go on in", " step through"))
                return Cutscene.EMOTE_POINT;
            if (shout && text.Length <= 36) return Cutscene.EMOTE_SURPRISE;
            if (ask) return Cutscene.EMOTE_SHRUG;
            if (trails) return Cutscene.EMOTE_THINK;
            return turn % 2 == 0 ? Cutscene.EMOTE_TALK : Cutscene.EMOTE_NOD;
        }

        /// <summary>
        /// A scene from its rows (see the class comment). 'speaker' is the scene's own character: rows without a name are
        /// theirs, and when they have a body the scene is filmed by the director (WireCutsceneCasts), with 'shots' to fall
        /// back on; otherwise it plays on 'shots'. Voice clips: Audio/Voice/&lt;voicePrefix&gt;_00.wav, _01... by spoken line.
        /// </summary>
        private static Cutscene MakeCutscene(Transform parent, string name, string speaker, string[] lines, string voicePrefix, Transform[] shots)
        {
            var cs = UdonSharpUndo.AddComponent<Cutscene>(Child(parent, name, Vector3.zero).gameObject);
            cs.cutsceneId = voicePrefix;
            var script = new SceneScript { name = name, lead = speaker, cutscene = cs };
            List<SceneBeat> beats = ParseScene(name, speaker, lines, script.cast);
            script.beats = beats;
            int n = beats.Count;
            cs.lines = new string[n];
            cs.speakers = new string[n];
            cs.stances = new int[n];
            cs.voices = new AudioClip[n];
            cs.kinds = new int[n];
            cs.amounts = new float[n];
            cs.branches = new int[n];
            cs.emotes = new int[n];
            cs.framings = new int[n];
            int spoken = 0;
            for (int i = 0; i < n; i++)
            {
                SceneBeat b = beats[i];
                cs.kinds[i] = b.kind;
                cs.lines[i] = b.text;
                cs.speakers[i] = b.kind == Cutscene.BEAT_SAY ? b.speaker : "";
                cs.stances[i] = b.hostile ? Cutscene.STANCE_HOSTILE
                    : b.speaker.StartsWith("Professor") || b.speaker.StartsWith("Headmaster") || b.speaker == "You" ? Cutscene.STANCE_ALLY : Cutscene.STANCE_NEUTRAL;
                cs.amounts[i] = b.amount;
                cs.branches[i] = b.branch;
                cs.emotes[i] = b.emote;
                cs.framings[i] = b.framing;
                if (b.kind != Cutscene.BEAT_SAY) continue;
                cs.voices[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + "/" + voicePrefix + "_" + spoken.ToString("00") + ".wav");
                spoken++;
            }
            cs.shots = shots;
            SceneScripts.Add(script);
            return cs;
        }

        // ================================================================== the cast

        private static bool IsPlayerName(string name)
        {
            string low = name.Trim().ToLowerInvariant();
            return low == "you" || low == "player" || low == "wizard" || low == "the wizard" || low == "all" || low == "everyone";
        }

        /// <summary>
        /// The body that goes by a name: its full name; or ('loose', for directions) its NPC key, or the one name that
        /// has it as a word ("Ozza" is Madame Ozza Goldtongue).
        /// </summary>
        private static NpcBody FindBody(string name, bool loose)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Trim();
            NpcBody body;
            if (NpcBodies.TryGetValue(name, out body)) return body;
            if (!loose) return null;
            foreach (var kv in NpcBodies)
                if (kv.Value.key != null && string.Equals(kv.Value.key, name, System.StringComparison.OrdinalIgnoreCase)) return kv.Value;
            NpcBody found = null;
            foreach (var kv in NpcBodies)
            {
                foreach (string word in kv.Key.Split(' ', ',', '"'))
                {
                    if (!string.Equals(word, name, System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (found != null && found != kv.Value) return null;   // two people answer to it
                    found = kv.Value;
                }
            }
            return found;
        }

        /// <summary>A name's place in a scene's cast (joining it if they have a body), ACTOR_PLAYER or ACTOR_NOBODY.</summary>
        private static int CastIndex(string name, bool loose, List<NpcBody> cast, string scene)
        {
            if (string.IsNullOrWhiteSpace(name)) return Cutscene.ACTOR_NOBODY;
            if (IsPlayerName(name)) return Cutscene.ACTOR_PLAYER;
            NpcBody body = FindBody(name, loose);
            if (body == null && loose)
            {
                // Among those already in the scene, a first name is enough.
                foreach (NpcBody c in cast)
                    if (c.title.IndexOf(name.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0) { body = c; break; }
            }
            if (body == null)
            {
                if (loose) Debug.LogWarning("[WizardsVRC Cutscenes] " + scene + ": nobody called \"" + name + "\" has a body in the world; that direction does nothing.");
                return Cutscene.ACTOR_NOBODY;
            }
            int at = cast.IndexOf(body);
            if (at >= 0) return at;
            if (cast.Count >= CastLimit)
            {
                Debug.LogWarning("[WizardsVRC Cutscenes] " + scene + ": more than " + CastLimit + " characters; " + body.title + " is heard but not seen.");
                return Cutscene.ACTOR_NOBODY;
            }
            cast.Add(body);
            return cast.Count - 1;
        }

        /// <summary>A copy of a character to play their part away from home: no name tag, no click box, kept switched off.</summary>
        private static NpcBody StandInFor(NpcBody original)
        {
            NpcBody copy;
            if (StandIns.TryGetValue(original.title, out copy)) return copy;
            if (_standInRoot == null)
            {
                _standInRoot = new GameObject("CutsceneStandIns").transform;
                _standInRoot.position = new Vector3(0f, RespawnHeight - 80f, 0f);
            }
            string safe = "";
            foreach (char c in original.title) if (char.IsLetterOrDigit(c)) safe += c;
            float top, face;
            Transform root = NpcModel(_standInRoot, "StandIn_" + safe, Vector3.zero, 0f, original.look, original.title, "", out top, out face, true);
            root.gameObject.SetActive(false);
            copy = new NpcBody { title = original.title, key = original.key, root = root, look = original.look, faceY = face };
            StandIns[original.title] = copy;
            return copy;
        }

        private static Animator BodyAnimator(Transform root)
        {
            Transform model = root != null ? root.Find("Model") : null;
            return model != null ? model.GetComponent<Animator>() : null;
        }

        /// <summary>
        /// Gives every scene whose own character has a body its cast, once all the bodies exist (see the class comment).
        /// Returns how many scenes the director films.
        /// </summary>
        private static int WireCutsceneCasts()
        {
            int directed = 0, shared = 0;
            foreach (SceneScript sc in SceneScripts)
            {
                Cutscene cs = sc.cutscene;
                if (cs == null) continue;
                NpcBody lead = FindBody(sc.lead, false);
                if (lead == null) continue;
                var cast = new List<NpcBody> { lead };
                // "@cast": in the order they stand (the first on your right of the scene's own character, the next on your
                // left, then further right, further left: CutscenePlayer.Mark).
                foreach (string name in sc.cast) CastIndex(name, true, cast, sc.name);
                int n = sc.beats.Count;
                var about = new int[n];
                var towards = new int[n];
                for (int i = 0; i < n; i++)
                {
                    SceneBeat b = sc.beats[i];
                    bool line = b.kind == Cutscene.BEAT_SAY;
                    // A line's speaker goes by their full name; directions (and "[to ...]") can use a first name.
                    about[i] = line ? CastIndex(b.who, false, cast, sc.name) : (b.who.Length == 0 ? Cutscene.ACTOR_NOBODY : CastIndex(b.who, true, cast, sc.name));
                    towards[i] = CastIndex(b.whom, true, cast, sc.name);
                    if (line && b.speaker == "You") about[i] = Cutscene.ACTOR_PLAYER;
                    // A gesture with no name is the scene's own character's.
                    if (b.kind == Cutscene.BEAT_EMOTE && b.who.Length == 0) about[i] = 0;
                }
                int m = cast.Count;
                cs.actors = new Transform[m];
                cs.actorNames = new string[m];
                cs.actorAnimators = new Animator[m];
                cs.actorFaces = new float[m];
                cs.actorStandIns = new bool[m];
                cs.actorLate = new bool[m];
                cs.actorOriginals = new GameObject[m];
                for (int k = 0; k < m; k++)
                {
                    NpcBody body = cast[k];
                    bool near = k == 0 || (body.root.root == lead.root.root && Vector3.Distance(body.root.position, lead.root.position) <= CastNearby);
                    NpcBody plays = near ? body : StandInFor(body);
                    cs.actors[k] = plays.root;
                    cs.actorNames[k] = body.title;
                    cs.actorAnimators[k] = BodyAnimator(plays.root);
                    cs.actorFaces[k] = plays.faceY;
                    cs.actorStandIns[k] = !near;
                    if (!near)
                    {
                        Transform model = body.root.Find("Model");
                        cs.actorOriginals[k] = model != null ? model.gameObject : null;
                    }
                }
                // Whoever a row brings on isn't there when the scene opens.
                for (int i = 0; i < n; i++)
                    if (sc.beats[i].kind == Cutscene.BEAT_ENTER && about[i] > 0) cs.actorLate[about[i]] = true;
                cs.lineActors = about;
                cs.targets = towards;
                cs.directed = true;
                UdonSharpEditorUtility.CopyProxyToUdon(cs);
                EditorUtility.SetDirty(cs);
                directed++;
                if (m > 1) shared++;
            }
            Debug.Log("[WizardsVRC] Cutscenes: " + SceneScripts.Count + " scenes, " + directed + " filmed by the director (" + shared + " with a cast of several, "
                + StandIns.Count + " stand-ins); the rest play on their placed shots.");
            return directed;
        }

        // ================================================================== the screen

        /// <summary>
        /// The cutscene screen: the film (16:9, letterboxed), a fade and a card over it, the talk window across the bottom
        /// of the picture with the speaker's name plate and medallion (a question's answers take its place), and Auto
        /// and Skip under it. The whole picture is one big button (go on). All of it on the layer the film camera
        /// doesn't draw.
        /// </summary>
        private static GameObject BuildCutsceneScreen(CutscenePlayer player, RenderTexture film)
        {
            const float filmW = 1600f, filmH = 900f, filmY = 50f, bar = 64f;
            var screenGo = new GameObject("CutsceneScreen", typeof(RectTransform));
            MakeWorldCanvas(screenGo, new Vector2(1600, 1000), Vector3.zero, 0.001f);
            RectTransform root = (RectTransform)screenGo.transform;
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(player);
            Panel(root, "Frame", Vector2.zero, new Vector2(1600, 1000), new Color(0.03f, 0.025f, 0.05f, 1f));

            // ---- the picture
            var filmGo = new GameObject("Film", typeof(RectTransform), typeof(RawImage));
            filmGo.transform.SetParent(root, false);
            var filmRect = (RectTransform)filmGo.transform;
            filmRect.anchoredPosition = new Vector2(0, filmY);
            filmRect.sizeDelta = new Vector2(filmW, filmH);
            var filmImage = filmGo.GetComponent<RawImage>();
            filmImage.texture = film;
            filmImage.raycastTarget = false;
            // Clicking the picture goes on: an (all but) invisible catcher over it, under everything else you can click.
            // (Not a Button: those all get a hover sound, and this one is under your pointer the whole time.)
            RectTransform area = Panel(root, "AdvanceArea", new Vector2(0, filmY), new Vector2(filmW, filmH), new Color(1f, 1f, 1f, 0.004f));
            area.GetComponent<Image>().raycastTarget = true;
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            UnityEventTools.AddStringPersistentListener(click.callback, new UnityAction<string>(target.SendCustomEvent), nameof(CutscenePlayer.Next));
            var trigger = area.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry> { click };
            player.nextButton = area.gameObject;

            player.fadeImage = Panel(root, "FadeFill", new Vector2(0, filmY), new Vector2(filmW, filmH), new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
            // The letterbox: two bars that slide in from the picture's top and bottom edges as a scene opens.
            player.barTop = Panel(root, "LetterboxBarTop", Vector2.zero, new Vector2(filmW, bar), Color.black);
            player.barTop.pivot = new Vector2(0.5f, 1f);
            player.barTop.anchoredPosition = new Vector2(0, filmY + filmH / 2f);
            player.barBottom = Panel(root, "LetterboxBarBottom", Vector2.zero, new Vector2(filmW, bar), Color.black);
            player.barBottom.pivot = new Vector2(0.5f, 0f);
            player.barBottom.anchoredPosition = new Vector2(0, filmY - filmH / 2f);
            player.barHeight = bar;
            player.cardLabel = Label(root, "Card", new Vector2(0, filmY + 30), new Vector2(1200, 420), "", 48, TextAnchor.MiddleCenter);
            player.cardLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            player.cardLabel.gameObject.SetActive(false);
            // A spell film's caster and spell, on the lower bar.
            player.filmLabel = Label(root, "FilmCaption", new Vector2(0, filmY - filmH / 2f + bar / 2f), new Vector2(1500, bar), "", 36, TextAnchor.MiddleCenter);
            player.filmLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            player.filmLabel.gameObject.SetActive(false);

            // ---- the talk window, across the bottom of the picture
            RectTransform talk = Rect(root, "TalkWindow", new Vector2(0, -290), new Vector2(1400, 215));
            UiImage(talk, "TalkBack", Vector2.zero, new Vector2(1400, 215), "PanelOrnate", new Color(1f, 1f, 1f, 0.93f), 3f).raycastTarget = false;
            // Who's speaking: a medallion with their initial, its rim blue (ally), yellow (neutral) or red (hostile),
            // and their name on a plate at the window's corner in the same colour (WizardsVRCSceneBuilder.UiStyle.cs has the art).
            RectTransform portrait = Rect(talk, "Portrait", new Vector2(-600, -4), new Vector2(150, 150));
            UiImage(portrait, "PortraitBack", Vector2.zero, new Vector2(150, 150), "PortraitBack", Color.white, 2.3f).preserveAspect = true;
            player.portraitRing = UiImage(portrait, "PortraitRing", Vector2.zero, new Vector2(150, 150), "PortraitNeutral", Color.white, 2.3f);
            player.portraitRing.preserveAspect = true;
            player.portraitRings = new[] { UiSprite("PortraitAlly"), UiSprite("PortraitNeutral"), UiSprite("PortraitHostile") };
            player.portraitInitial = Label(portrait, "PortraitInitial", Vector2.zero, new Vector2(150, 150), "", 74, TextAnchor.MiddleCenter);
            player.portraitInitial.gameObject.AddComponent<Outline>().effectColor = Color.black;
            ApplyCombatFont(player.portraitInitial.transform);
            player.portraitRoot = portrait.gameObject;
            player.stanceColors = new[] { new Color(0.5f, 0.75f, 1f), new Color(1f, 0.85f, 0.35f), new Color(1f, 0.45f, 0.4f) };
            Image plate = UiImage(talk, "NamePlate", new Vector2(-385, 118), new Vector2(520, 62), "SignBoard", new Color(0.75f, 0.72f, 0.85f), 2f);
            plate.raycastTarget = false;
            player.namePlate = plate.gameObject;
            player.speakerLabel = Label(plate.transform, "Speaker", new Vector2(0, 2), new Vector2(490, 56), "", 32, TextAnchor.MiddleCenter);
            player.speakerLabel.color = new Color(1f, 0.85f, 0.4f);
            player.speakerLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            player.speakerLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0.08f, 0.05f, 0.04f, 0.95f);
            player.subtitleLabel = Label(talk, "Subtitle", new Vector2(95, -12), new Vector2(1140, 160), "", 33, TextAnchor.UpperLeft);
            player.subtitleLabel.supportRichText = true;
            player.subtitleLabel.lineSpacing = 1.05f;
            // The mark that bobs at the window's corner when the line is all there: a small gold diamond.
            RectTransform mark = Panel(talk, "AdvanceMark", new Vector2(652, -76), new Vector2(20, 20), new Color(1f, 0.82f, 0.35f));
            mark.localRotation = Quaternion.Euler(0, 0, 45f);
            mark.gameObject.SetActive(false);
            player.advanceMark = mark;
            player.talkWindow = talk.gameObject;

            // ---- a question: the prompt and up to three answers, across the bottom of the picture where the talk window
            // was (your own face, which the camera turns to, stays in view above it)
            RectTransform ask = Rect(root, "ChoicePanel", new Vector2(0, -242), new Vector2(1100, 310));
            UiImage(ask, "ChoiceBack", Vector2.zero, new Vector2(1100, 310), "PanelOrnate", new Color(1f, 1f, 1f, 0.95f), 3f).raycastTarget = false;
            player.choicePrompt = Label(ask, "Prompt", new Vector2(0, 118), new Vector2(1000, 54), "", 33, TextAnchor.MiddleCenter);
            player.choicePrompt.color = new Color(1f, 0.9f, 0.55f);
            player.choiceButtons = new GameObject[3];
            player.choiceLabels = new Text[3];
            string[] events = { nameof(CutscenePlayer.Choose0), nameof(CutscenePlayer.Choose1), nameof(CutscenePlayer.Choose2) };
            for (int k = 0; k < 3; k++)
            {
                player.choiceButtons[k] = Button(ask, "Answer" + (k + 1), new Vector2(0, 46 - k * 74), new Vector2(1000, 64), "", new Color(0.26f, 0.3f, 0.5f), target, events[k]);
                player.choiceLabels[k] = player.choiceButtons[k].GetComponentInChildren<Text>();
                player.choiceLabels[k].fontSize = 30;
            }
            ask.gameObject.SetActive(false);
            player.choicePanel = ask.gameObject;

            // ---- under the picture: how to go on, Auto, Skip
            player.hintLabel = Label(root, "Hint", new Vector2(-360, -450), new Vector2(820, 60), "", 26, TextAnchor.MiddleLeft);
            player.hintLabel.color = new Color(0.72f, 0.7f, 0.8f);
            player.autoButton = Button(root, "Auto", new Vector2(390, -450), new Vector2(240, 68), "Auto: On", new Color(0.3f, 0.35f, 0.55f), target, nameof(CutscenePlayer.ToggleAuto));
            player.autoLabel = player.autoButton.GetComponentInChildren<Text>();
            player.skipButton = Button(root, "Skip", new Vector2(650, -450), new Vector2(240, 68), "Skip", new Color(0.5f, 0.25f, 0.25f), target, nameof(CutscenePlayer.Skip));

            var voice = screenGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            player.voiceSource = voice;
            player.screenRoot = screenGo;
            // Only ever switched on for the local player, so nobody else sees it; and on its own layer (still clickable,
            // no collisions), which the film camera doesn't draw.
            int layer = OffCameraLayer();
            foreach (Transform t in screenGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            screenGo.SetActive(false);
            return screenGo;
        }

        /// <summary>
        /// The dark that falls round you while a scene plays: a sphere round your head (WizardsVRC/DuskVeil) that dims the
        /// world behind the screen, so the screen is what you watch. The film camera doesn't draw it.
        /// </summary>
        private static void BuildCutsceneVeil(CutscenePlayer player, Transform parent)
        {
            var shader = Shader.Find("WizardsVRC/DuskVeil");
            if (shader == null) return;
            System.IO.Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/CutsceneVeil.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_Color", new Color(0f, 0f, 0f, 0f));
            EditorUtility.SetDirty(mat);
            var veil = new GameObject("CutsceneVeil");
            veil.transform.SetParent(parent, false);
            veil.transform.localScale = Vector3.one * 14f;
            veil.AddComponent<MeshFilter>().sharedMesh = InsideSphereMesh();
            var r = veil.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            veil.layer = OffCameraLayer();
            veil.SetActive(false);
            player.veil = veil.transform;
            player.veilRenderer = r;
        }

        /// <summary>
        /// What the film camera draws: for a story scene everything but the screen's own layer, other players, your
        /// head-less first-person avatar and the game's own interface (you are in the picture: your full avatar is drawn
        /// from its mirror copy); for a spell's film, other players too.
        /// </summary>
        private static void SetFilmMasks(CutscenePlayer player, Camera cam)
        {
            int screen = 1 << OffCameraLayer();
            int hidden = screen;
            foreach (string layerName in new[] { "Player", "PlayerLocal", "UI", "UiMenu", "InternalUI" })
            {
                int l = LayerMask.NameToLayer(layerName);
                if (l >= 0) hidden |= 1 << l;
            }
            cam.cullingMask = ~hidden;
            int filmHidden = screen;
            int local = LayerMask.NameToLayer("PlayerLocal");
            if (local >= 0) filmHidden |= 1 << local;
            player.filmCullingMask = ~filmHidden;
        }
    }
}
