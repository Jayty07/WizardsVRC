using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The tavern's jukebox: a synced video player (VRChat's Unity video player, with its picture on a screen over the
    /// stage) whose music is only heard inside the tavern, and a lectern by the stage to paste a link, go back to the
    /// default song, pause, and mute or change the volume for yourself (TavernJukebox).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>The tavern's default song (a plain watch link: VRChat can't follow YouTube's radio playlists, so it loops).</summary>
        private const string TavernDefaultSong = "https://www.youtube.com/watch?v=3CJ8G-IWS00";

        private static readonly Color JukeboxButton = new Color(0.36f, 0.2f, 0.12f);

        /// <param name="t">the tavern's root</param>
        /// <param name="c">the middle of its floor (in t's space)</param>
        /// <param name="R">the wall's radius</param>
        /// <param name="screenA">bearing of the wall the screen hangs on (over the stage)</param>
        /// <param name="lecternA">bearing of the controls (just off the stage)</param>
        private static TavernJukebox BuildJukebox(Transform t, Vector3 c, float R, float wallH, float roofRise, float screenA, float lecternA,
            Material frameMat, Material standMat, List<UdonSharpBehaviour> behaviours)
        {
            var centre = Child(t, "JukeboxCentre", c);

            // ---- the screen: a dark-framed 16:9 panel high on the wall over the curtains
            Quaternion wallRot = TFacing(screenA);
            Vector3 screenPos = TPolar(c, R - 0.9f, screenA, 7.6f);
            Block(t, "ScreenFrame", TPolar(c, R - 0.84f, screenA, 7.6f), new Vector3(4.3f, 2.55f, 0.08f), frameMat, wallRot);
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "JukeboxScreen";
            screen.transform.SetParent(t, false);
            screen.transform.localPosition = screenPos;
            screen.transform.localRotation = wallRot; // a quad shows its -Z side: that faces into the room
            screen.transform.localScale = new Vector3(4f, 2.25f, 1f);
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            var screenR = screen.GetComponent<Renderer>();
            screenR.sharedMaterial = JukeboxScreenMat();
            screenR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ---- the player and its speaker
            var playerGo = Child(t, "JukeboxPlayer", TPolar(c, R - 1.5f, screenA, 3f)).gameObject;
            var speaker = playerGo.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.loop = false;
            speaker.volume = 0f;            // TavernJukebox fades it in while you're inside
            speaker.spatialBlend = 0f;      // fills the room; it is silenced outside by where you stand, not by distance
            speaker.dopplerLevel = 0f;
            speaker.priority = 64;
            var video = playerGo.AddComponent<VRCUnityVideoPlayer>();
            var so = new SerializedObject(video);
            SetIfFound(so, "autoPlay", p => p.boolValue = false);
            SetIfFound(so, "loop", p => p.boolValue = true);
            SetIfFound(so, "renderMode", p => p.intValue = 1); // material override on a renderer
            SetIfFound(so, "targetMaterialRenderer", p => p.objectReferenceValue = screenR);
            SetIfFound(so, "targetMaterialProperty", p => p.stringValue = "_MainTex");
            SetIfFound(so, "maximumResolution", p => p.intValue = 720);
            SetIfFound(so, "videoURL.url", p => p.stringValue = "");
            var audioProp = so.FindProperty("targetAudioSources");
            if (audioProp != null)
            {
                audioProp.arraySize = 1;
                audioProp.GetArrayElementAtIndex(0).objectReferenceValue = speaker;
            }
            else Debug.LogWarning("[WizardsVRC] The tavern's video player has no 'targetAudioSources' field in this SDK; set its audio source by hand.");
            so.ApplyModifiedPropertiesWithoutUndo();

            var jukebox = UdonSharpUndo.AddComponent<TavernJukebox>(playerGo);
            jukebox.player = video;
            jukebox.speaker = speaker;
            jukebox.defaultUrl = new VRCUrl(TavernDefaultSong);
            jukebox.center = centre;
            jukebox.radius = R - 0.8f;
            jukebox.height = wallH + roofRise;
            behaviours.Add(jukebox);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(jukebox);

            // ---- the lectern with the controls, tilted back like a music stand
            Vector3 lecternBase = TPolar(c, 11.6f, lecternA, 0f);
            Block(t, "JukeboxLecternPost", lecternBase + new Vector3(0, 0.6f, 0), new Vector3(0.2f, 1.2f, 0.2f), standMat, TFacing(lecternA));
            Block(t, "JukeboxLecternFoot", lecternBase + new Vector3(0, 0.05f, 0), new Vector3(0.7f, 0.1f, 0.7f), standMat, TFacing(lecternA));
            Quaternion deskRot = TFacing(lecternA) * Quaternion.Euler(35f, 0f, 0f);
            Vector3 deskPos = lecternBase + new Vector3(0, 1.3f, 0);
            Block(t, "JukeboxLecternDesk", deskPos + deskRot * new Vector3(0, 0, 0.04f), new Vector3(1.16f, 0.72f, 0.05f), standMat, deskRot);

            var canvasGo = new GameObject("JukeboxControls", typeof(RectTransform));
            canvasGo.transform.SetParent(t, false);
            MakeWorldCanvas(canvasGo, new Vector2(900, 540), deskPos, 0.0012f);
            canvasGo.transform.localRotation = deskRot; // canvas +Z = the way you look at it (outwards, down at the desk)
            var cr = (RectTransform)canvasGo.transform;
            Panel(cr, "Background", Vector2.zero, new Vector2(900, 540), new Color(0.13f, 0.08f, 0.06f, 0.96f));
            Label(cr, "Title", new Vector2(0, 225), new Vector2(860, 60), "Tavern Jukebox", 38, TextAnchor.MiddleCenter).color = new Color(1f, 0.82f, 0.45f);
            jukebox.statusLabel = Label(cr, "Status", new Vector2(0, 168), new Vector2(860, 50), "Nothing playing", 24, TextAnchor.MiddleCenter);

            jukebox.urlField = UrlField(cr, "Link", new Vector2(-95, 92), new Vector2(650, 72), target);
            Button(cr, "PlayLink", new Vector2(335, 92), new Vector2(170, 72), "Play", new Color(0.25f, 0.45f, 0.2f), target, nameof(TavernJukebox.OnUrlEntered));

            Button(cr, "Default", new Vector2(-285, 0), new Vector2(270, 76), "Default song", JukeboxButton, target, nameof(TavernJukebox.PlayDefault));
            var pause = Button(cr, "Pause", new Vector2(0, 0), new Vector2(270, 76), "Pause", JukeboxButton, target, nameof(TavernJukebox.TogglePause));
            jukebox.pauseLabel = pause.GetComponentInChildren<Text>();
            var mute = Button(cr, "Mute", new Vector2(285, 0), new Vector2(270, 76), "Mute (you)", JukeboxButton, target, nameof(TavernJukebox.ToggleMute));
            jukebox.muteLabel = mute.GetComponentInChildren<Text>();

            Button(cr, "VolDown", new Vector2(-285, -95), new Vector2(270, 76), "Volume -", JukeboxButton, target, nameof(TavernJukebox.VolumeDown));
            jukebox.volumeLabel = Label(cr, "Volume", new Vector2(0, -95), new Vector2(290, 76), "Your volume: 45%", 24, TextAnchor.MiddleCenter);
            Button(cr, "VolUp", new Vector2(285, -95), new Vector2(270, 76), "Volume +", JukeboxButton, target, nameof(TavernJukebox.VolumeUp));

            Label(cr, "Hint", new Vector2(0, -200), new Vector2(860, 90),
                "Paste a YouTube link and press Play: it plays for everyone in the tavern. Mute and volume are just for you.",
                20, TextAnchor.MiddleCenter).color = new Color(0.85f, 0.75f, 0.6f);
            return jukebox;
        }

        private static void SetIfFound(SerializedObject so, string path, System.Action<SerializedProperty> set)
        {
            var p = so.FindProperty(path);
            if (p != null) set(p);
            else Debug.LogWarning("[WizardsVRC] The tavern's video player has no '" + path + "' field in this SDK; check it by hand.");
        }

        /// <summary>A VRChat link field (VRCUrlInputField). Enter sends OnUrlEntered.</summary>
        private static VRCUrlInputField UrlField(RectTransform parent, string name, Vector2 pos, Vector2 size, UdonBehaviour target)
        {
            RectTransform rt = Rect(parent, name, pos, size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.95f, 0.9f, 0.8f);
            var field = rt.gameObject.AddComponent<VRCUrlInputField>();
            field.targetGraphic = bg;

            Text placeholder = Label(rt, "Placeholder", Vector2.zero, size - new Vector2(30, 10), "Paste a YouTube link...", 24, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.45f, 0.38f, 0.32f);
            placeholder.fontStyle = FontStyle.Italic;
            Text text = Label(rt, "Text", Vector2.zero, size - new Vector2(30, 10), "", 24, TextAnchor.MiddleLeft);
            text.color = new Color(0.15f, 0.1f, 0.08f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = VRCUrlInputField.LineType.SingleLine;
            UnityEventTools.AddStringPersistentListener(field.onEndEdit, new UnityAction<string>(target.SendCustomEvent), nameof(TavernJukebox.OnUrlEntered));
            return field;
        }

        private static Material JukeboxScreenMat()
        {
            string path = MaterialsDir + "/TavernScreen.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Unlit/Texture");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            // Black until a video plays (an untextured Unlit/Texture would show white).
            string texPath = MaterialsDir + "/TavernScreenBlank.asset";
            var blank = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (blank == null)
            {
                blank = new Texture2D(4, 4, TextureFormat.RGB24, false);
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(0.03f, 0.02f, 0.02f);
                blank.SetPixels(px);
                blank.Apply();
                AssetDatabase.CreateAsset(blank, texPath);
            }
            mat.mainTexture = blank;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
