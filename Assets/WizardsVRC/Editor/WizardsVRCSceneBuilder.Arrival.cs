using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Coming and going:
    ///   - SceneFade: a black veil round the player's head (WizardsVRC/ScreenFade, over everything), which also fades the
    ///     music and every ambient sound. Used when a character is chosen on the title stage and for Teleport to Mark.
    ///   - LocationKeeper: remembers where each character last stood (so choosing them brings you back there), and
    ///     Mark Location / Teleport to Mark with its glowing ring. It's told which boxes of the world you can be
    ///     remembered in (the castle, the keep, every street and town) and which you can't (dungeons).
    /// Both are made early (the book's Map page has their buttons) and wired once the zones, the culler and the music exist.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>The build's LocationKeeper (the Map page's Mark / Teleport buttons call it).</summary>
        private static LocationKeeper BookLocations;
        /// <summary>Labels that show what music is playing (the HUD's, the map's, the title stage's): MusicDirector fills them.</summary>
        private static readonly List<Text> NowPlayingLabels = new List<Text>();

        private static void ResetArrival()
        {
            BookLocations = null;
            NowPlayingLabels.Clear();
        }

        /// <summary>A small now-playing line (MusicDirector writes it): one row running on past its box, or wrapped inside it.</summary>
        private static Text NowPlayingLabel(Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color,
            bool outline = true, bool wrap = false)
        {
            Text t = Label(parent, "NowPlaying", pos, size, "", fontSize, anchor);
            t.color = color;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline) t.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            NowPlayingLabels.Add(t);
            return t;
        }

        private static SceneFade BuildSceneFade(Transform systems)
        {
            var fade = UdonSharpUndo.AddComponent<SceneFade>(Child(systems, "SceneFade", Vector3.zero).gameObject);
            var veil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            veil.name = "SceneFadeVeil";
            veil.transform.SetParent(systems, false);
            veil.transform.localScale = Vector3.one * 0.3f;
            Object.DestroyImmediate(veil.GetComponent<Collider>());
            var r = veil.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Directory.CreateDirectory(MaterialsDir);
            string path = MaterialsDir + "/ScreenFade.mat";
            var shader = Shader.Find("WizardsVRC/ScreenFade");
            if (shader == null) Debug.LogError("[WizardsVRC] Shader WizardsVRC/ScreenFade is missing; scenes will cut instead of fading.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader ?? Shader.Find("Unlit/Color"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (shader != null) mat.shader = shader;
            mat.SetColor("_Color", Color.black);
            mat.SetFloat("_Alpha", 0f);
            EditorUtility.SetDirty(mat);
            r.sharedMaterial = mat;
            veil.SetActive(false);
            fade.veil = veil.transform;
            fade.veilRenderer = r;
            ExtraBehaviours.Add(fade);
            return fade;
        }

        private static LocationKeeper BuildLocationKeeper(Transform systems, PlayerState playerState, SaveManager saveManager, TeleportHelper teleporter,
            SceneFade fade, Transform hubSpawn)
        {
            var keeper = UdonSharpUndo.AddComponent<LocationKeeper>(Child(systems, "LocationKeeper", Vector3.zero).gameObject);
            keeper.playerState = playerState;
            keeper.saveManager = saveManager;
            keeper.teleporter = teleporter;
            keeper.fade = fade;
            keeper.fallback = hubSpawn;
            keeper.spot = Child(systems, "TravelSpot", Vector3.zero);

            // The mark: a ring of runes on the ground and a little blue crystal turning over it (only you see yours). The
            // keeper moves the ring itself; the turning is on a pivot inside it, so the two don't fight.
            var ring = Child(systems, "MarkRing", Vector3.zero);
            var spin = Child(ring, "MarkSpin", Vector3.zero);
            Color blue = new Color(0.45f, 0.75f, 1f);
            FlatDecal(spin, "MarkRunes", Vector3.zero, 2.4f, CutoutMat("MarkRingRunes", PixelTexture(TexturesDir + "/DuelCircleRunes.png"), blue, blue * 0.9f));
            FlatDecal(spin, "MarkCore", new Vector3(0, 0.012f, 0), 1.2f, CutoutMat("MarkRingCore", PixelTexture(TexturesDir + "/DuelCircleSigil.png"), blue, blue * 0.7f));
            Shape(spin, "MarkGem", GemMesh(), new Vector3(0, 1.9f, 0), new Vector3(0.28f, 0.44f, 0.28f),
                PMat("Crystal_MarkBlue", blue, 1f, blue * 0.8f), false, Quaternion.identity);
            foreach (var t in ring.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
            foreach (var r in ring.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Directory.CreateDirectory(TitleAnimDir);
            var clip = NewClip("MarkRing", true);
            // A steady turn (straight-line keys: no easing at the loop) and the crystal bobbing.
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAnglesRaw.y"), AnimationCurve.Linear(0f, 0f, 14f, 360f));
            Wave(clip, "MarkGem", "posy", 14f, 4, 1.9f, 0.12f);
            Animate(spin.gameObject, "MarkRing", clip);
            ring.gameObject.SetActive(false);
            keeper.markRing = ring;

            BookLocations = keeper;
            ExtraBehaviours.Add(keeper);
            return keeper;
        }

        /// <summary>Once everything else is built: where you can be remembered, what the fade quietens, who to tell.</summary>
        private static void WireArrival(LocationKeeper keeper, SceneFade fade, MusicDirector music, AreaCuller culler, InventoryBook book, WorldBuild wb,
            HubLayout hubPlan, Transform interiorRoot, Transform systems, Scene scene)
        {
            keeper.culler = culler;
            keeper.book = book;
            keeper.notifications = BookNotices;
            keeper.awakening = systems.GetComponentInChildren<AwakeningSequence>(true);
            keeper.titleScreen = systems.GetComponentInChildren<TitleScreen>(true);

            // Safe boxes: the castle grounds, the keep's rooms, every street and town.
            var min = new List<Vector3>();
            var max = new List<Vector3>();
            var names = new List<string>();
            string hubName = hubPlan != null && hubPlan.zone != null ? hubPlan.zone.displayName : "Starfall Castle";
            Bounds hub = hubPlan != null ? hubPlan.cullBounds : new Bounds(Vector3.zero, new Vector3(240f, 210f, 260f));
            min.Add(hub.min); max.Add(hub.max); names.Add(hubName);
            if (interiorRoot != null)
            {
                bool any = false;
                Bounds keep = new Bounds(interiorRoot.position, Vector3.zero);
                foreach (var r in interiorRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { keep = r.bounds; any = true; }
                    else keep.Encapsulate(r.bounds);
                }
                if (any)
                {
                    keep.Expand(new Vector3(2f, 2f, 2f));
                    min.Add(keep.min); max.Add(keep.max); names.Add(hubName);
                }
            }
            for (int i = 0; i < wb.safeBounds.Count; i++)
            {
                min.Add(wb.safeBounds[i].min); max.Add(wb.safeBounds[i].max); names.Add(wb.safeNames[i]);
            }
            keeper.safeMin = min.ToArray();
            keeper.safeMax = max.ToArray();
            keeper.safeNames = names.ToArray();

            // Not inside a dungeon (its party goes in together by the sigil; you'd be in there on your own).
            var bmin = new List<Vector3>();
            var bmax = new List<Vector3>();
            foreach (var s in Object.FindObjectsOfType<DungeonSigil>(true))
            {
                if (s.boundsMax.x <= s.boundsMin.x) continue;
                bmin.Add(s.boundsMin);
                bmax.Add(s.boundsMax);
            }
            keeper.blockedMin = bmin.ToArray();
            keeper.blockedMax = bmax.ToArray();

            // The fade quietens the music and every ambient sound (the areas' beds, fires, waterfalls).
            fade.music = music;
            var sources = new List<AudioSource>();
            var volumes = new List<float>();
            foreach (var go in scene.GetRootGameObjects())
                foreach (var src in go.GetComponentsInChildren<AudioSource>(true))
                {
                    if (src.name != "Ambience" && src.name != "AmbientSound") continue;
                    sources.Add(src);
                    volumes.Add(src.volume);
                }
            fade.ambience = sources.ToArray();
            fade.ambienceVolume = volumes.ToArray();
            Debug.Log("[WizardsVRC] Arrival: characters are remembered in " + min.Count + " places (not in " + bmin.Count + " dungeons and towers); the scene fade quietens " +
                      sources.Count + " ambient sounds and the music.");
        }
    }
}
