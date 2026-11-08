using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The Crystal Tap's hall, after the Aureion Collegium concept sheet: a radial tower round the Starfall Crystal.
    ///   - The middle is a theatre: two rings of velvet seats (the back one raised) face a four-sided screen hung round the
    ///     fountain under the crystal; it's the tavern's video player (TavernJukebox), controls on a lectern by the bar.
    ///   - Seven school nooks round the ground floor, under the gallery: Life's garden, Storm's arcane lounge, Ice's crystal
    ///     study, Death's quiet reliquary, Fire's hearth lounge, Myth's archive and Balance's concord (with the mirror),
    ///     each with its banner, carpet, light and curios; the bar stays on the east side, the doors at north, west, south.
    ///   - A gallery (mezzanine) ring on columns, reached by two stairs, with a railing over the theatre, school banners
    ///     hanging from its edge, and ten rooms off it: a bed, a desk and chair, a window with sunlight coming in, and
    ///     its own screen and video player (loaded only while you're near, heard only in the room).
    ///   - Sunbeams come down through the open roof and the windows; a ring chandelier hangs over the theatre.
    /// Angles: 0 = east, 90 = north (as the rest of the tavern).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const float MezzY = 5.8f, MezzThick = 0.35f, WalkIn = 11f, RoomIn = 13.6f, RoomH = 3.2f;
        private const int TavernRooms = 10;
        /// <summary>The school nooks: school (SchoolLabels index), first and last bearing.</summary>
        private static readonly Vector3[] TavernNooks =
        {
            new Vector3(3, 34f, 82f),     // Life: the garden nook
            new Vector3(2, 98f, 135f),    // Storm: the arcane lounge
            new Vector3(1, 135f, 172f),   // Ice: the crystal study
            new Vector3(4, 188f, 225f),   // Death: the quiet reliquary
            new Vector3(0, 225f, 262f),   // Fire: the hearth lounge
            new Vector3(5, 278f, 302f),   // Myth: the archive
            new Vector3(6, 302f, 326f),   // Balance: the concord (and the mirror)
        };
        /// <summary>The two stairs up to the gallery: their middle bearing and which way they climb (+1 toward higher bearings).</summary>
        private static readonly Vector2[] TavernStairs = { new Vector2(52f, 1f), new Vector2(308f, -1f) };
        private const float StairD = 10.1f, StairHalfRun = 4.25f, StairW = 1.3f;

        private static Mesh _shaftMesh;

        /// <summary>The whole hall inside the walls (the bar, walls, floor and roof are TavernLandmark's).</summary>
        private static void TavernHall(Transform t, Vector3 c, float R, List<UdonSharpBehaviour> behaviours, List<GameObject> mugs, List<Vector3> avoid,
            Material stone, Material gold, Material wood, Material darkWood, Material planks, Material ceiling, Material beam, Material window,
            Material plaster, Material velvet, Material glow, Material flame, Material pewter, Material foam)
        {
            float wallIn = R - 0.8f;
            var plum = PsxTexMat("TavernPlumVelvet", new Color(0.52f, 0.24f, 0.66f), TileTexture(StreetTexDir + "/Velvet.png"), 1f);
            var sun = ShaftMat("TavernSunShaft", new Color(1f, 0.8f, 0.5f, 0.32f));

            TavernGallery(t, c, R, stone, gold, darkWood, planks, ceiling, plaster);
            var rooms = new List<TavernJukebox>();
            for (int k = 0; k < TavernRooms; k++)
                rooms.Add(TavernRoom(t, c, R, k, behaviours, gold, wood, darkWood, plaster, window, glow, flame, sun, plum));
            TavernJukebox theatre = TavernTheatre(t, c, R, behaviours, avoid, gold, darkWood, plum, velvet, glow);
            theatre.mutedInside = rooms.ToArray();
            foreach (var n in TavernNooks)
                TavernNook(t, c, R, (int)n.x, n.y, n.z, behaviours, mugs, gold, wood, darkWood, stone, velvet, plum, glow, flame, pewter, foam, window, sun);

            // ---- school banners hanging from the gallery's edge over the theatre, one by each nook (Life's, Myth's and
            // Balance's moved along, clear of the stairs that climb under the gallery's edge there)
            foreach (var n in TavernNooks)
            {
                int s = (int)n.x;
                float a = (n.y + n.z) / 2f;
                if (s == 3) a = 86f;
                else if (s == 5) a = 276f;
                else if (s == 6) a = 338f;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "GalleryBanner";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(t, false);
                q.transform.localPosition = TPolar(c, WalkIn - 0.25f, a, MezzY - MezzThick - 1.45f);
                q.transform.localRotation = TFacing(a);   // its face (-Z) turned to the middle
                q.transform.localScale = new Vector3(1.2f, 2.1f, 1f);
                q.GetComponent<Renderer>().sharedMaterial = ClassMat("ClassBanner" + SchoolLabels[s], "Banner" + SchoolLabels[s] + ".png", Vector2.one, 0.5f);
                Deco(t, PrimitiveType.Cube, "GalleryBannerRod", TPolar(c, WalkIn - 0.25f, a, MezzY - MezzThick - 0.35f), new Vector3(1.4f, 0.07f, 0.07f), gold, TFacing(a), false);
            }

            // ---- the ring chandelier over the theatre, and lanterns under the gallery between the columns
            const float chR = 8.6f, chY = 10.6f;
            for (int i = 0; i < 32; i++)
            {
                float a = i * 11.25f;
                Deco(t, PrimitiveType.Cube, "ChandelierRing", TPolar(c, chR, a, chY), new Vector3(2f * chR * Mathf.Sin(5.625f * Mathf.Deg2Rad) + 0.05f, 0.12f, 0.22f), gold, TFacing(a), false);
                if (i % 2 == 0)
                {
                    Shape(t, "ChandelierCandle", CylMesh(), TPolar(c, chR, a, chY + 0.22f), new Vector3(0.1f, 0.12f, 0.1f), PMat("CandleWax", new Color(0.9f, 0.88f, 0.8f), 1f), false, Quaternion.identity);
                    Shape(t, "ChandelierFlame", BlobMesh(), TPolar(c, chR, a, chY + 0.45f), new Vector3(0.09f, 0.16f, 0.09f), flame, false, Quaternion.identity);
                }
            }
            float roofAtCh = TavernRoofY(R, chR);
            for (int k = 0; k < 4; k++)
            {
                float a = 45f + 90f * k;
                Deco(t, PrimitiveType.Cube, "ChandelierChain", TPolar(c, chR, a, (chY + roofAtCh) / 2f), new Vector3(0.06f, roofAtCh - chY, 0.06f), beam, Quaternion.identity, false);
                PointLight(t, TPolar(c, chR, a, chY - 0.6f), new Color(1f, 0.82f, 0.6f), 16f, 1.2f);
            }
            for (int k = 0; k < 12; k++)
            {
                float a = 30f * k;
                float ceilY = MezzY - MezzThick;
                Deco(t, PrimitiveType.Cube, "ArcadeChain", TPolar(c, 12.6f, a, ceilY - 0.3f), new Vector3(0.05f, 0.6f, 0.05f), beam, Quaternion.identity, false);
                Shape(t, "ArcadeLantern", GemMesh(), TPolar(c, 12.6f, a, ceilY - 0.85f), new Vector3(0.4f, 0.6f, 0.4f), glow, false, Quaternion.identity);
            }

            // ---- sunbeams down through the open roof onto the theatre's west side
            foreach (float a in new[] { 150f, 195f, 240f })
            {
                Vector3 top = TPolar(c, 4.2f, a, TavernWallH + TavernRoofRise - 0.4f);   // from the opening's edge, clear of the crystal
                Vector3 bottom = TPolar(c, 9.4f, a, TavernFloorY);
                LightShaft(t, "RoofSunbeam", top, bottom, 2.4f, sun);
            }
        }

        /// <summary>How high the inside of the roof is at radius r (it rises from the wall's top to the open middle).</summary>
        private static float TavernRoofY(float R, float r)
        {
            float r0 = R + 1.4f, r1 = 4.6f, yr0 = TavernWallH + 0.35f;
            return yr0 + (r0 - r) / (r0 - r1) * TavernRoofRise - 0.35f;
        }

        // ================================================================== the gallery

        private static void TavernGallery(Transform t, Vector3 c, float R, Material stone, Material gold, Material darkWood, Material planks, Material ceiling, Material plaster)
        {
            float wallIn = R - 0.8f;
            float under = MezzY - MezzThick;
            var top = new MeshBuilder();
            var bottom = new MeshBuilder();
            SectorBlock(top, c, WalkIn, wallIn, 0f, 360f, under, MezzY, 72, true, false, true);
            SectorBlock(bottom, c, WalkIn, wallIn, 0f, 360f, under, MezzY, 72, false, true, false);
            TavernMesh(t, "GalleryFloor", top, planks, true);
            // A landing just past the top of each stair, closing the sliver between its end and the gallery's edge.
            foreach (var st in TavernStairs)
            {
                Vector3 along = TDir(st.x + 90f) * st.y;
                Vector3 end = TPolar(c, StairD, st.x, 0f) + along * StairHalfRun;
                Block(t, "GalleryLanding", end + along * 0.45f + new Vector3(0, MezzY - MezzThick / 2f, 0), new Vector3(StairW + 0.5f, MezzThick, 0.9f), planks, Quaternion.LookRotation(along));
            }
            TavernMesh(t, "GalleryCeiling", bottom, ceiling, false);
            var fascia = new MeshBuilder();
            SectorBlock(fascia, c, WalkIn - 0.14f, WalkIn + 0.02f, 0f, 360f, under - 0.5f, MezzY + 0.02f, 72, true, true, true);
            TavernMesh(t, "GalleryFascia", fascia, darkWood, false);
            var trim = new MeshBuilder();
            SectorBlock(trim, c, WalkIn - 0.2f, WalkIn - 0.12f, 0f, 360f, under - 0.56f, under - 0.44f, 72, true, true, true);
            SectorBlock(trim, c, WalkIn - 0.2f, WalkIn - 0.12f, 0f, 360f, MezzY - 0.1f, MezzY + 0.04f, 72, true, true, true);
            TavernMesh(t, "GalleryTrim", trim, gold, false);

            // ---- columns under the gallery's edge (none where a stair arrives)
            for (int k = 0; k < 12; k++)
            {
                float a = 15f + 30f * k;
                if (AngleGap(a, 75f) < 5f || AngleGap(a, 285f) < 5f) continue;
                Vector3 b = TPolar(c, WalkIn + 0.35f, a, 0f);
                float h = under - TavernFloorY;
                Shape(t, "GalleryColumn", CylMesh(), b + new Vector3(0, TavernFloorY + h / 2f, 0), new Vector3(0.62f, h / 2f, 0.62f), stone, true, Quaternion.identity);
                Shape(t, "GalleryColumnBase", CylMesh(), b + new Vector3(0, TavernFloorY + 0.15f, 0), new Vector3(0.86f, 0.15f, 0.86f), gold, false, Quaternion.identity);
                Shape(t, "GalleryColumnCap", CylMesh(), b + new Vector3(0, under - 0.18f, 0), new Vector3(0.9f, 0.18f, 0.9f), gold, false, Quaternion.identity);
            }

            // ---- the railing round the gallery's edge: posts, a handrail, a bottom rail, and an invisible wall to lean on
            var rail = new MeshBuilder();
            var guard = new MeshBuilder();
            var gaps = new List<Vector2>();
            foreach (var st in TavernStairs)
            {
                float topA = st.x + st.y * Mathf.Atan2(StairHalfRun, StairD) * Mathf.Rad2Deg;
                gaps.Add(new Vector2(topA - 6f, topA + 6f));
            }
            var spans = ArcSpans(0f, 360f, gaps);
            foreach (var sp in spans)
            {
                int segs = Mathf.Max(2, Mathf.CeilToInt((sp.y - sp.x) / 5f));
                SectorBlock(rail, c, WalkIn - 0.02f, WalkIn + 0.12f, sp.x, sp.y, MezzY + 0.95f, MezzY + 1.05f, segs, true, true, true);
                SectorBlock(rail, c, WalkIn + 0.01f, WalkIn + 0.09f, sp.x, sp.y, MezzY + 0.1f, MezzY + 0.16f, segs, true, true, true);
                SectorBlock(guard, c, WalkIn - 0.05f, WalkIn + 0.15f, sp.x, sp.y, MezzY, MezzY + 1.3f, segs, false, false, true);
                for (float a = sp.x + 1f; a < sp.y - 0.5f; a += 2.4f)
                    SectorBlock(rail, c, WalkIn + 0.02f, WalkIn + 0.08f, a, a + 0.3f, MezzY + 0.16f, MezzY + 0.95f, 1, false, false, true);
            }
            TavernMesh(t, "GalleryRail", rail, gold, false);
            TavernCollider(t, "GalleryRailGuard", guard);

            // ---- the stairs: straight flights along the gallery's inner edge, rising toward the north and south doors
            foreach (var st in TavernStairs)
            {
                Vector3 mid = TPolar(c, StairD, st.x, 0f);
                Vector3 along = TDir(st.x + 90f) * st.y;
                Vector3 bottomP = mid - along * StairHalfRun + new Vector3(0, TavernFloorY, 0);
                Vector3 topP = mid + along * StairHalfRun + new Vector3(0, MezzY, 0);
                Stairs(t, "GalleryStair", bottomP, topP, StairW, darkWood);
                // A handrail on the open (inner) side, and gold nosing at the foot.
                Vector3 inner = -TDir(st.x) * (StairW / 2f + 0.05f);
                Vector3 r0 = bottomP + inner + new Vector3(0, 0.95f, 0), r1 = topP + inner + new Vector3(0, 0.95f, 0);
                var hand = Deco(t, PrimitiveType.Cube, "StairRail", (r0 + r1) / 2f, new Vector3(0.1f, 0.1f, (r1 - r0).magnitude), gold, Quaternion.LookRotation(r1 - r0), true);
                for (int k = 0; k <= 6; k++)
                {
                    Vector3 p = Vector3.Lerp(bottomP, topP, k / 6f) + inner;
                    Deco(t, PrimitiveType.Cube, "StairPost", p + new Vector3(0, 0.48f, 0), new Vector3(0.08f, 0.95f, 0.08f), gold, Quaternion.identity, false);
                }
            }
        }

        /// <summary>The parts of a0..a1 (degrees) outside the gaps.</summary>
        private static List<Vector2> ArcSpans(float a0, float a1, List<Vector2> gaps)
        {
            var spans = new List<Vector2> { new Vector2(a0, a1) };
            foreach (var g in gaps)
            {
                var next = new List<Vector2>();
                foreach (var s in spans)
                {
                    if (g.y <= s.x || g.x >= s.y) { next.Add(s); continue; }
                    if (g.x > s.x) next.Add(new Vector2(s.x, g.x));
                    if (g.y < s.y) next.Add(new Vector2(g.y, s.y));
                }
                spans = next;
            }
            return spans;
        }

        // ================================================================== the theatre

        private static TavernJukebox TavernTheatre(Transform t, Vector3 c, float R, List<UdonSharpBehaviour> behaviours, List<Vector3> avoid,
            Material gold, Material darkWood, Material plum, Material velvet, Material glow)
        {
            // ---- seats: a ring on the floor and a raised ring behind it, facing the middle; aisles at the doors and the bar
            var dais = new MeshBuilder();
            const float r1 = 7.0f, r2 = 8.55f, daisH = 0.42f;
            foreach (float aisle in new[] { 0f, 90f, 180f, 270f })
            {
                float a0 = aisle + 7f, a1 = aisle + 83f;
                SectorBlock(dais, c, 7.85f, 9.25f, a0, a1, 0f, TavernFloorY + daisH, 16, true, false, true);
                for (float a = a0 + 6f; a <= a1 - 5.9f; a += 12f) TheatreSeat(t, c, r1, a, TavernFloorY, avoid, plum, darkWood, gold, behaviours);
                for (float a = a0 + 5f; a <= a1 - 4.9f; a += 10f) TheatreSeat(t, c, r2, a, TavernFloorY + daisH, avoid, plum, darkWood, gold, behaviours);
            }
            TavernMesh(t, "TheatreDais", dais, PsxTexMat("TheatreCarpet", new Color(0.42f, 0.16f, 0.5f), TileTexture(StreetTexDir + "/Velvet.png"), 1.2f), true);
            var daisEdge = new MeshBuilder();
            foreach (float aisle in new[] { 0f, 90f, 180f, 270f })
                SectorBlock(daisEdge, c, 7.83f, 7.9f, aisle + 7f, aisle + 83f, TavernFloorY + daisH - 0.06f, TavernFloorY + daisH + 0.02f, 16, true, true, true);
            TavernMesh(t, "TheatreDaisEdge", daisEdge, gold, false);

            // ---- the screens: four faces round the fountain, under the crystal, tipped down toward the seats
            RenderTexture rt = TheatreRenderTexture();
            Material screenMat = TheatreScreenMat(rt);
            const float half = 3.95f, screenY = 4.6f, tip = 15f;
            var screens = Child(t, "TheatreScreens", c + new Vector3(0, screenY, 0));
            for (int k = 0; k < 4; k++)
            {
                float a = 45f + 90f * k;
                Vector3 outDir = TDir(a);
                Vector3 fwd = -outDir * Mathf.Cos(tip * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(tip * Mathf.Deg2Rad);
                var face = Child(screens, "Screen" + k, outDir * half);
                face.localRotation = Quaternion.LookRotation(fwd, Vector3.up);
                Block(face, "ScreenFrame", new Vector3(0f, 0f, 0.07f), new Vector3(6.35f, 3.72f, 0.1f), darkWood);
                foreach (float sy in new[] { -1f, 1f }) Block(face, "ScreenTrim", new Vector3(0f, sy * 1.86f, 0.02f), new Vector3(6.5f, 0.1f, 0.08f), gold);
                foreach (float sx in new[] { -1f, 1f }) Block(face, "ScreenTrim", new Vector3(sx * 3.2f, 0f, 0.02f), new Vector3(0.1f, 3.8f, 0.08f), gold);
                Shape(face, "ScreenGem", GemMesh(), new Vector3(0f, 2.2f, 0f), new Vector3(0.45f, 0.6f, 0.2f), glow, false, Quaternion.identity);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "TheatreScreen";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(face, false);
                q.transform.localScale = new Vector3(6f, 3.375f, 1f);   // a quad shows its -Z side: out toward the seats
                var qr = q.GetComponent<Renderer>();
                qr.sharedMaterial = screenMat;
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var col in face.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            }
            // Chains from the corners up into the roof.
            float cornerR = half * 1.41f;
            float roofY = TavernRoofY(R, cornerR);
            for (int k = 0; k < 4; k++)
            {
                float a = 90f * k;
                float y0 = screenY + 1.9f;
                Deco(t, PrimitiveType.Cube, "ScreenChain", TPolar(c, cornerR, a, (y0 + roofY) / 2f), new Vector3(0.07f, roofY - y0, 0.07f), darkWood, Quaternion.identity, false);
                Shape(t, "ScreenFinial", GemMesh(), TPolar(c, cornerR, a, y0), new Vector3(0.35f, 0.5f, 0.35f), gold, false, Quaternion.identity);
            }

            // ---- the player: heard everywhere in the tavern except the rooms; controls on a lectern in the bar aisle
            var centre = Child(t, "JukeboxCentre", c);
            AudioSource speaker;
            var video = TavernVideoPlayer(t, "TheatrePlayer", c + new Vector3(0, screenY, 0), null, rt, out speaker);
            var jukebox = UdonSharpUndo.AddComponent<TavernJukebox>(video.gameObject);
            jukebox.player = video;
            jukebox.speaker = speaker;
            jukebox.defaultUrl = new VRCUrl(TavernDefaultSong);
            jukebox.center = centre;
            jukebox.radius = R - 0.8f;
            jukebox.height = TavernWallH + TavernRoofRise;
            behaviours.Add(jukebox);
            Vector3 lectern = TPolar(c, 10.3f, 0f, TavernFloorY);
            TavernVideoControls(t, jukebox, lectern, TFacing(180f), false, "The Crystal Tap Theatre",
                "Paste a YouTube link and press Play: it plays on the theatre screens for everyone in the tavern (not in the rooms upstairs). Mute and volume are just for you.",
                darkWood);
            return jukebox;
        }

        private static void TheatreSeat(Transform t, Vector3 c, float r, float a, float floorY, List<Vector3> avoid, Material cushion, Material frame, Material gold,
            List<UdonSharpBehaviour> behaviours)
        {
            Vector3 p = TPolar(c, r, a, floorY);
            if (avoid != null)
                foreach (var v in avoid)
                {
                    Vector3 d = v - p;
                    d.y = 0f;
                    if (d.sqrMagnitude < 1.6f * 1.6f) return;
                }
            MakeChair(t, "TheatreSeat", p, -TDir(a), cushion, frame, gold, behaviours, 0.72f, true);
        }

        private static RenderTexture TheatreRenderTexture()
        {
            string path = MaterialsDir + "/TheatreScreen.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt == null)
            {
                rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
                AssetDatabase.CreateAsset(rt, path);
            }
            return rt;
        }

        private static Material TheatreScreenMat(RenderTexture rt)
        {
            string path = MaterialsDir + "/TheatreScreen.mat";
            var shader = Shader.Find("Unlit/Texture");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = rt;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// A VRChat video player with its speaker (fills the room, faded in by TavernJukebox): onto 'screen' (material
        /// override) or into 'rt' (every screen showing that texture). Not autoplaying; it loops.
        /// </summary>
        private static VRCUnityVideoPlayer TavernVideoPlayer(Transform parent, string name, Vector3 pos, Renderer screen, RenderTexture rt, out AudioSource speaker)
        {
            var go = Child(parent, name, pos).gameObject;
            speaker = go.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.loop = false;
            speaker.volume = 0f;
            speaker.spatialBlend = 0f;
            speaker.dopplerLevel = 0f;
            speaker.priority = 64;
            var video = go.AddComponent<VRCUnityVideoPlayer>();
            var so = new SerializedObject(video);
            SetIfFound(so, "autoPlay", p => p.boolValue = false);
            SetIfFound(so, "loop", p => p.boolValue = true);
            if (rt != null)
            {
                SetIfFound(so, "renderMode", p => p.intValue = 0);   // into a render texture
                SetIfFound(so, "targetTexture", p => p.objectReferenceValue = rt);
            }
            else
            {
                SetIfFound(so, "renderMode", p => p.intValue = 1);   // material override on a renderer
                SetIfFound(so, "targetMaterialRenderer", p => p.objectReferenceValue = screen);
                SetIfFound(so, "targetMaterialProperty", p => p.stringValue = "_MainTex");
            }
            SetIfFound(so, "maximumResolution", p => p.intValue = 720);
            SetIfFound(so, "videoURL.url", p => p.stringValue = "");
            var audioProp = so.FindProperty("targetAudioSources");
            if (audioProp != null)
            {
                audioProp.arraySize = 1;
                audioProp.GetArrayElementAtIndex(0).objectReferenceValue = speaker;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return video;
        }

        /// <summary>
        /// The controls: a link field and Play, Pause, Stop (the rooms) or Default song (the theatre), Mute and volume, on a
        /// panel at 'pos' (+Z of 'rot' = the way you look at it). 'compact' is the small desk panel in a room.
        /// </summary>
        private static void TavernVideoControls(Transform t, TavernJukebox jukebox, Vector3 pos, Quaternion rot, bool compact, string title, string hint, Material standMat)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(jukebox);
            Vector3 deskPos = pos;
            Quaternion deskRot = rot;
            float scale = compact ? 0.0006f : 0.0012f;
            if (!compact)
            {
                Block(t, "JukeboxLecternPost", pos + new Vector3(0, 0.6f, 0), new Vector3(0.2f, 1.2f, 0.2f), standMat, rot);
                Block(t, "JukeboxLecternFoot", pos + new Vector3(0, 0.05f, 0), new Vector3(0.7f, 0.1f, 0.7f), standMat, rot);
                deskRot = rot * Quaternion.Euler(35f, 0f, 0f);
                deskPos = pos + new Vector3(0, 1.3f, 0);
                Block(t, "JukeboxLecternDesk", deskPos + deskRot * new Vector3(0, 0, 0.04f), new Vector3(1.16f, 0.72f, 0.05f), standMat, deskRot);
            }
            var canvasGo = new GameObject(compact ? "RoomVideoControls" : "JukeboxControls", typeof(RectTransform));
            canvasGo.transform.SetParent(t, false);
            MakeWorldCanvas(canvasGo, new Vector2(900, 540), deskPos, scale);
            canvasGo.transform.localRotation = deskRot;
            var cr = (RectTransform)canvasGo.transform;
            Panel(cr, "Background", Vector2.zero, new Vector2(900, 540), new Color(0.13f, 0.08f, 0.06f, 0.96f));
            Label(cr, "Title", new Vector2(0, 225), new Vector2(860, 60), title, 38, TextAnchor.MiddleCenter).color = new Color(1f, 0.82f, 0.45f);
            jukebox.statusLabel = Label(cr, "Status", new Vector2(0, 168), new Vector2(860, 50), "Nothing playing", 24, TextAnchor.MiddleCenter);
            jukebox.urlField = UrlField(cr, "Link", new Vector2(-95, 92), new Vector2(650, 72), target);
            Button(cr, "PlayLink", new Vector2(335, 92), new Vector2(170, 72), "Play", new Color(0.25f, 0.45f, 0.2f), target, nameof(TavernJukebox.OnUrlEntered));
            if (compact) Button(cr, "Stop", new Vector2(-285, 0), new Vector2(270, 76), "Stop", JukeboxButton, target, nameof(TavernJukebox.StopForEveryone));
            else Button(cr, "Default", new Vector2(-285, 0), new Vector2(270, 76), "Default song", JukeboxButton, target, nameof(TavernJukebox.PlayDefault));
            var pause = Button(cr, "Pause", new Vector2(0, 0), new Vector2(270, 76), "Pause", JukeboxButton, target, nameof(TavernJukebox.TogglePause));
            jukebox.pauseLabel = pause.GetComponentInChildren<Text>();
            var mute = Button(cr, "Mute", new Vector2(285, 0), new Vector2(270, 76), "Mute (you)", JukeboxButton, target, nameof(TavernJukebox.ToggleMute));
            jukebox.muteLabel = mute.GetComponentInChildren<Text>();
            Button(cr, "VolDown", new Vector2(-285, -95), new Vector2(270, 76), "Volume -", JukeboxButton, target, nameof(TavernJukebox.VolumeDown));
            jukebox.volumeLabel = Label(cr, "Volume", new Vector2(0, -95), new Vector2(290, 76), "Your volume: 45%", 24, TextAnchor.MiddleCenter);
            Button(cr, "VolUp", new Vector2(285, -95), new Vector2(270, 76), "Volume +", JukeboxButton, target, nameof(TavernJukebox.VolumeUp));
            Label(cr, "Hint", new Vector2(0, -205), new Vector2(860, 90), hint, 18, TextAnchor.MiddleCenter).color = new Color(0.85f, 0.75f, 0.6f);
        }

        // ================================================================== the rooms upstairs

        /// <summary>
        /// Room k off the gallery (36 degrees of the ring): partition walls, the wall to the gallery with a doorway and the
        /// room's number, a ceiling, a bed against the outer wall with a nightstand, a desk and chair under a window with
        /// sunlight streaming in, a rug, a lamp, and its own screen and video player with the controls on the desk.
        /// </summary>
        private static TavernJukebox TavernRoom(Transform t, Vector3 c, float R, int k, List<UdonSharpBehaviour> behaviours, Material gold, Material wood,
            Material darkWood, Material plaster, Material window, Material glow, Material flame, Material sun, Material cushion)
        {
            float wallIn = R - 0.8f;
            float span = 360f / TavernRooms;
            float a0 = k * span, a1 = a0 + span, ac = a0 + span / 2f;
            float rc = (RoomIn + wallIn) / 2f, depth = wallIn - RoomIn;

            // ---- walls: a partition on its first side (the next room builds the other), the gallery wall with a doorway
            Block(t, "RoomWall", TPolar(c, rc, a0, MezzY + RoomH / 2f), new Vector3(0.25f, RoomH, depth + 0.1f), plaster, TFacing(a0));
            const int segs = 7;
            float dA = span / segs;
            for (int j = 0; j < segs; j++)
            {
                float a = a0 + dA * (j + 0.5f);
                float chord = 2f * RoomIn * Mathf.Sin(dA * 0.5f * Mathf.Deg2Rad) + 0.06f;
                if (j == segs / 2)
                {
                    Block(t, "RoomLintel", TPolar(c, RoomIn, a, MezzY + 2.5f + (RoomH - 2.5f) / 2f), new Vector3(chord, RoomH - 2.5f, 0.3f), plaster, TFacing(a));
                    foreach (float s in new[] { -1f, 1f })
                        Deco(t, PrimitiveType.Cube, "RoomDoorPost", TPolar(c, RoomIn - 0.02f, a + s * dA * 0.5f, MezzY + 1.25f), new Vector3(0.14f, 2.5f, 0.4f), darkWood, TFacing(a), false);
                    Deco(t, PrimitiveType.Cube, "RoomDoorBeam", TPolar(c, RoomIn - 0.02f, a, MezzY + 2.55f), new Vector3(chord + 0.2f, 0.14f, 0.4f), gold, TFacing(a), false);
                }
                else Block(t, "RoomFront", TPolar(c, RoomIn, a, MezzY + RoomH / 2f), new Vector3(chord, RoomH, 0.3f), plaster, TFacing(a));
            }
            // The room's number over its door, read from the gallery.
            WorldText(t, "RoomNumber", TPolar(c, RoomIn - 0.2f, ac, MezzY + 2.9f), TFacing(ac), new Vector2(400, 90), 0.004f, "Room " + (k + 1), 60)
                .color = new Color(1f, 0.85f, 0.45f);
            // Ceiling (seen from inside, plaster on top under the roof).
            var ceil = new MeshBuilder();
            SectorBlock(ceil, c, RoomIn - 0.16f, wallIn + 0.3f, a0, a1, MezzY + RoomH, MezzY + RoomH + 0.18f, 6, true, true, false);
            TavernMesh(t, "RoomCeiling", ceil, plaster, false);

            // ---- inside, in the room's own frame: origin on the floor in the middle, +Z out toward the window
            var room = Child(t, "Room" + (k + 1), TPolar(c, rc, ac, MezzY));
            room.localRotation = TFacing(ac);
            float zOut0 = RoomWallZ(wallIn, rc, 0f);
            int s7 = k % 7;
            Color tint = SchoolColors[s7];
            var blanket = PMat("RoomBlanket" + SchoolLabels[s7], Color.Lerp(tint, new Color(0.25f, 0.15f, 0.3f), 0.45f), 1f);
            var linen = PMat("RoomLinen", new Color(0.93f, 0.9f, 0.82f), 1f);
            FlatDecal(room, "RoomRug", new Vector3(0f, 0.03f, 0.1f), 3.2f, ClassMat("ClassCarpet" + SchoolLabels[s7], "Carpet" + SchoolLabels[s7] + ".png", Vector2.one, 0f));

            // Bed: head against the outer wall on the left.
            float bx = -2.9f, bz = RoomWallZ(wallIn, rc, bx) - 1.15f;
            Block(room, "BedFrame", new Vector3(bx, 0.2f, bz), new Vector3(1.7f, 0.4f, 2.2f), darkWood);
            Block(room, "BedMattress", new Vector3(bx, 0.51f, bz + 0.02f), new Vector3(1.56f, 0.22f, 2.06f), linen);
            Deco(room, PrimitiveType.Cube, "BedBlanket", new Vector3(bx, 0.64f, bz - 0.35f), new Vector3(1.62f, 0.06f, 1.4f), blanket, Quaternion.identity, false);
            Deco(room, PrimitiveType.Cube, "BedPillow", new Vector3(bx, 0.7f, bz + 0.8f), new Vector3(1.2f, 0.16f, 0.42f), linen, Quaternion.identity, false);
            Block(room, "BedHead", new Vector3(bx, 0.7f, bz + 1.14f), new Vector3(1.82f, 1.4f, 0.1f), darkWood);
            Deco(room, PrimitiveType.Cube, "BedHeadTrim", new Vector3(bx, 1.42f, bz + 1.14f), new Vector3(1.9f, 0.08f, 0.14f), gold, Quaternion.identity, false);
            // Nightstand with a candle.
            Vector3 ns = new Vector3(bx + 1.3f, 0f, bz + 0.85f);
            Block(room, "Nightstand", ns + new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.45f), wood);
            Shape(room, "Candle", CylMesh(), ns + new Vector3(0, 0.72f, 0), new Vector3(0.08f, 0.12f, 0.08f), PMat("CandleWax", new Color(0.9f, 0.88f, 0.8f), 1f), false, Quaternion.identity);
            Shape(room, "CandleFlame", BlobMesh(), ns + new Vector3(0, 0.9f, 0), new Vector3(0.07f, 0.13f, 0.07f), flame, false, Quaternion.identity);

            // Desk and chair on the right, against the outer wall.
            float dx = 2.9f, dz = RoomWallZ(wallIn, rc, dx) - 0.45f;
            Block(room, "DeskTop", new Vector3(dx, 0.78f, dz), new Vector3(1.6f, 0.06f, 0.72f), wood);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Deco(room, PrimitiveType.Cube, "DeskLeg", new Vector3(dx + sx * 0.72f, 0.38f, dz + sz * 0.3f), new Vector3(0.07f, 0.76f, 0.07f), darkWood, Quaternion.identity, false);
            Shape(room, "DeskLamp", GemMesh(), new Vector3(dx + 0.6f, 1.0f, dz + 0.15f), new Vector3(0.18f, 0.28f, 0.18f), glow, false, Quaternion.identity);
            MakeChair(room, "DeskChair", new Vector3(dx, 0f, dz - 0.9f), Vector3.forward, cushion, darkWood, gold, behaviours, 0.6f, true);

            // Window over the middle of the outer wall (inside and out), and the sunlight coming through it.
            Deco(room, PrimitiveType.Cube, "RoomWindow", new Vector3(0f, 1.75f, zOut0 - 0.03f), new Vector3(1.3f, 1.7f, 0.05f), window, Quaternion.identity, false);
            Deco(room, PrimitiveType.Cube, "RoomWindowBar", new Vector3(0f, 1.75f, zOut0 - 0.06f), new Vector3(0.07f, 1.7f, 0.04f), darkWood, Quaternion.identity, false);
            Deco(room, PrimitiveType.Cube, "RoomWindowBar", new Vector3(0f, 1.75f, zOut0 - 0.06f), new Vector3(1.3f, 0.07f, 0.04f), darkWood, Quaternion.identity, false);
            Deco(room, PrimitiveType.Cube, "RoomWindowSill", new Vector3(0f, 0.86f, zOut0 - 0.1f), new Vector3(1.5f, 0.08f, 0.22f), wood, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "Window", TPolar(c, R + 0.03f, ac, MezzY + 1.75f), new Vector3(1.3f, 1.7f, 0.08f), window, TFacing(ac), false);
            LightShaft(room, "WindowSunbeam", new Vector3(0f, 2.45f, zOut0 - 0.12f), new Vector3(0.35f, 0f, zOut0 - 3.1f), 1.3f, sun);

            // Lamp light.
            PointLight(room, new Vector3(0f, 2.6f, 0f), new Color(1f, 0.84f, 0.62f), 7f, 1.1f);
            Shape(room, "CeilingLamp", GemMesh(), new Vector3(0f, RoomH - 0.45f, 0f), new Vector3(0.45f, 0.6f, 0.45f), glow, false, Quaternion.identity);

            // ---- the screen on the gallery wall, facing the bed, and its own player
            float sx0 = bx, sz0 = RoomWallZ(RoomIn + 0.15f, rc, sx0) + 0.04f;
            var frame = Child(room, "RoomScreen", new Vector3(sx0, 1.95f, sz0));
            Block(frame, "ScreenFrame", new Vector3(0f, 0f, -0.02f), new Vector3(2.56f, 1.5f, 0.05f), darkWood);
            foreach (float yy in new[] { -0.76f, 0.76f }) Deco(frame, PrimitiveType.Cube, "ScreenTrim", new Vector3(0f, yy, 0.01f), new Vector3(2.62f, 0.05f, 0.04f), gold, Quaternion.identity, false);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "RoomScreenPicture";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(frame, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // its face (-Z) turned into the room (+Z)
            q.transform.localScale = new Vector3(2.4f, 1.35f, 1f);
            var qr = q.GetComponent<Renderer>();
            qr.sharedMaterial = JukeboxScreenMat();
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var tr in frame.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = false;

            AudioSource speaker;
            var video = TavernVideoPlayer(room, "RoomPlayer", new Vector3(0f, 1.6f, 0f), qr, null, out speaker);
            var jukebox = UdonSharpUndo.AddComponent<TavernJukebox>(video.gameObject);
            jukebox.player = video;
            jukebox.speaker = speaker;
            jukebox.defaultUrl = new VRCUrl("");
            jukebox.center = room;
            jukebox.boxHalf = new Vector2(rc * Mathf.Tan(span * 0.5f * Mathf.Deg2Rad) - 0.2f, depth / 2f - 0.05f);
            jukebox.height = RoomH;
            jukebox.radius = 3.5f;          // only the music's quiet circle uses this (the box decides where it's heard)
            jukebox.loadRange = 7f;
            behaviours.Add(jukebox);
            // Controls standing at the back of the desk, facing the chair.
            TavernVideoControls(room, jukebox, new Vector3(dx - 0.15f, 1.12f, dz + 0.18f), Quaternion.Euler(15f, 0f, 0f), true, "Room " + (k + 1),
                "Paste a link and press Play: it plays on this room's screen for everyone in the room.", darkWood);
            return jukebox;
        }

        /// <summary>In a room's frame (origin at radius rc), how far out (z) a wall at radius r is at sideways offset x.</summary>
        private static float RoomWallZ(float r, float rc, float x)
        {
            return Mathf.Sqrt(Mathf.Max(0f, r * r - x * x)) - rc;
        }

        // ================================================================== the school nooks

        private static float _nookWallIn, _nookRc;

        /// <summary>In the nook being built: how far out (z) the outer wall is at sideways offset x.</summary>
        private static float NZ(float x) { return RoomWallZ(_nookWallIn, _nookRc, x); }

        private static void TavernNook(Transform t, Vector3 c, float R, int s, float a0, float a1, List<UdonSharpBehaviour> behaviours, List<GameObject> mugs,
            Material gold, Material wood, Material darkWood, Material stone, Material velvet, Material plum, Material glow, Material flame,
            Material pewter, Material foam, Material window, Material sun)
        {
            float wallIn = R - 0.8f;
            float ac = (a0 + a1) / 2f, rc = (RoomIn + wallIn) / 2f;
            float hw = rc * Mathf.Sin((a1 - a0) * 0.5f * Mathf.Deg2Rad);
            string sn = SchoolLabels[s];
            Color col = SchoolColors[s];
            var nook = Child(t, "Nook" + sn, TPolar(c, rc, ac, TavernFloorY));
            nook.localRotation = TFacing(ac);
            _nookWallIn = wallIn;
            _nookRc = rc;
            float zOut = RoomWallZ(wallIn, rc, 0f);
            var schoolGlow = PMat("NookGlow" + sn, Color.Lerp(col, Color.white, 0.3f), 1f, col * 0.7f);
            var cushion = PMat("NookCushion" + sn, Color.Lerp(col, new Color(0.3f, 0.15f, 0.35f), 0.35f), 1f);

            // Carpet, banner, a light in the school's colour, sconces either side.
            // (Fire has the hearth's rug, and its banners either side of the fireplace; Balance has the mirror instead.)
            if (s != 0) FlatDecal(nook, "NookCarpet", new Vector3(0f, 0.025f, -0.2f), Mathf.Min(4.4f, hw * 2f - 0.8f), ClassMat("ClassCarpet" + sn, "Carpet" + sn + ".png", Vector2.one, 0f));
            if (s == 0) foreach (float sx in new[] { -1f, 1f }) NookBanner(nook, sn, new Vector3(sx * 3.4f, 3.3f, NZ(3.4f) - 0.08f), gold);
            else if (s != 6) NookBanner(nook, sn, new Vector3(0f, 3.3f, zOut - 0.06f), gold);
            PointLight(nook, new Vector3(0f, 4.3f, 0.4f), Color.Lerp(col, Color.white, 0.4f), 9f, 1.25f);
            foreach (float sx in new[] { -1f, 1f })
                Shape(nook, "NookSconce", GemMesh(), new Vector3(sx * (hw - 0.6f), 3.1f, RoomWallZ(wallIn, rc, sx * (hw - 0.6f)) - 0.2f), new Vector3(0.3f, 0.45f, 0.3f), schoolGlow, false, Quaternion.identity);

            switch (s)
            {
                case 0: NookFire(t, c, R, ac, nook, behaviours, gold, darkWood, stone, velvet); break;
                case 1: NookIce(nook, hw, zOut, behaviours, mugs, gold, wood, darkWood, cushion, schoolGlow, pewter, foam); break;
                case 2: NookStorm(nook, hw, zOut, behaviours, gold, darkWood, cushion, schoolGlow); break;
                case 3: NookLife(nook, hw, zOut, behaviours, gold, wood, darkWood, cushion); break;
                case 4: NookDeath(nook, hw, zOut, behaviours, gold, darkWood, stone, cushion, schoolGlow, flame); break;
                case 5: NookMyth(nook, hw, zOut, behaviours, gold, wood, darkWood, cushion, schoolGlow); break;
                case 6: NookBalance(t, nook, hw, zOut, behaviours, mugs, gold, wood, darkWood, cushion, schoolGlow, pewter, foam); break;
            }

            // A window or two in the outer wall beside it, with a sunbeam slanting in.
            foreach (float sx in new[] { -1f, 1f })
            {
                if (hw < 4f || s == 0) break;
                float wx = sx * (hw - 1.4f);
                float wz = RoomWallZ(wallIn, rc, wx);
                Deco(nook, PrimitiveType.Cube, "NookWindow", new Vector3(wx, 3.1f, wz - 0.03f), new Vector3(1.1f, 1.6f, 0.05f), window, Quaternion.identity, false);
                LightShaft(nook, "NookSunbeam", new Vector3(wx, 3.7f, wz - 0.1f), new Vector3(wx * 0.8f, 0f, wz - 3.6f), 1.2f, sun);
                // The same window seen from outside.
                Vector3 p = nook.localPosition + nook.localRotation * new Vector3(wx, 0f, wz);
                float bearing = Mathf.Atan2(p.z - c.z, p.x - c.x) * Mathf.Rad2Deg;
                Deco(t, PrimitiveType.Cube, "Window", TPolar(c, R + 0.03f, bearing, TavernFloorY + 3.1f), new Vector3(1.1f, 1.6f, 0.08f), window, TFacing(bearing), false);
            }
        }

        private static void NookBanner(Transform nook, string sn, Vector3 at, Material gold)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "NookBanner";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(nook, false);
            q.transform.localPosition = at;
            q.transform.localScale = new Vector3(1.5f, 2.6f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = ClassMat("ClassBanner" + sn, "Banner" + sn + ".png", Vector2.one, 0.5f);
            Deco(nook, PrimitiveType.Cube, "NookBannerRod", at + new Vector3(0f, 1.38f, -0.02f), new Vector3(1.8f, 0.08f, 0.08f), gold, Quaternion.identity, false);
        }

        /// <summary>Fire's hearth lounge: the tavern's great fireplace (its chimney up into the gallery floor), a rug and armchairs.</summary>
        private static void NookFire(Transform t, Vector3 c, float R, float hearthA, Transform nook, List<UdonSharpBehaviour> behaviours, Material gold,
            Material darkWood, Material stone, Material velvet)
        {
            Vector3 hearthOut = TDir(hearthA), hearthSide = Vector3.Cross(Vector3.up, hearthOut);
            Vector3 hb = TPolar(c, R - 1.6f, hearthA, 0f);
            foreach (float s in new[] { -1f, 1f })
                Block(t, "HearthPillar", hb + hearthSide * s * 1.75f + new Vector3(0, 1.8f, 0), new Vector3(0.9f, 3.6f, 1.6f), stone, TFacing(hearthA));
            Block(t, "HearthLintel", hb + new Vector3(0, 3.2f, 0), new Vector3(4.4f, 0.8f, 1.6f), stone, TFacing(hearthA));
            Block(t, "HearthBack", hb + hearthOut * 0.6f + new Vector3(0, 1.4f, 0), new Vector3(2.6f, 2.8f, 0.3f), PMat("Soot", new Color(0.12f, 0.1f, 0.1f), 1f), TFacing(hearthA));
            Deco(t, PrimitiveType.Cube, "Mantel", hb - hearthOut * 0.15f + new Vector3(0, 3.75f, 0), new Vector3(5f, 0.3f, 1.9f), darkWood, TFacing(hearthA), false);
            float chimneyTop = MezzY - MezzThick;
            Block(t, "Chimney", TPolar(c, R - 1.4f, hearthA, (3.9f + chimneyTop) / 2f), new Vector3(2.2f, chimneyTop - 3.9f, 1.4f), stone, TFacing(hearthA));
            PointLight(t, hb - hearthOut * 1.5f + new Vector3(0, 1.4f, 0), new Color(1f, 0.55f, 0.2f), 14f, 2f);
            Light hearthLight = t.GetChild(t.childCount - 1).GetComponent<Light>();
            hearthLight.gameObject.name = "HearthLight";
            behaviours.Add(HearthFire(t, hb + new Vector3(0, TavernFloorY, 0), hearthSide, Quaternion.LookRotation(-hearthOut), hearthLight));
            Shape(t, "HearthRug", CylMesh(), TPolar(c, R - 5.2f, hearthA, TavernFloorY + 0.035f), new Vector3(5.2f, 0.02f, 5.2f), PMat("TavernCurtain", new Color(0.62f, 0.1f, 0.14f), 1f), false, Quaternion.identity);
            foreach (float da in new[] { -11f, 0f, 11f })
            {
                Vector3 sp = TPolar(c, R - 5.6f, hearthA + da, TavernFloorY);
                Vector3 toFire = hb - sp;
                toFire.y = 0f;
                MakeChair(t, "HearthChair", sp, toFire.normalized, velvet, darkWood, gold, behaviours, 0.85f, true);
            }
        }

        /// <summary>Ice's crystal study: frost crystals in the corners, bookshelves by the banner, a study table, shards hovering over it.</summary>
        private static void NookIce(Transform nook, float hw, float zOut, List<UdonSharpBehaviour> behaviours, List<GameObject> mugs, Material gold, Material wood,
            Material darkWood, Material cushion, Material iceGlow, Material pewter, Material foam)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (hw - 0.9f);
                Shape(nook, "IceCrystal", GemMesh(), new Vector3(x, 0.9f, NZ(x) - 0.7f), new Vector3(0.7f, 1.8f, 0.7f), iceGlow, true, Quaternion.Euler(0, 20f * sx, 8f * sx));
                Shape(nook, "IceCrystal", GemMesh(), new Vector3(x - sx * 0.6f, 0.5f, NZ(x) - 0.5f), new Vector3(0.45f, 1f, 0.45f), iceGlow, false, Quaternion.Euler(0, -15f * sx, -14f * sx));
                Bookshelf(nook, "IceShelf", new Vector3(sx * 1.6f, 0f, NZ(sx * 2.3f) - 0.25f), 1.4f, 2.6f, darkWood, 11 + (int)sx);
            }
            StudyTable(nook, new Vector3(0f, 0f, -0.3f), wood, darkWood, mugs, pewter, foam);
            foreach (float sx in new[] { -1f, 1f })
                MakeChair(nook, "IceChair", new Vector3(sx * 1.25f, 0f, -0.3f), new Vector3(-sx, 0f, 0f), cushion, darkWood, gold, behaviours, 0.65f, true);
            for (int i = 0; i < 3; i++)
                Shape(nook, "IceShard", GemMesh(), new Vector3(-0.5f + i * 0.5f, 2.1f + (i % 2) * 0.3f, -0.3f), new Vector3(0.18f, 0.38f, 0.18f), iceGlow, false, Quaternion.Euler(0, i * 40f, 0));
        }

        /// <summary>Storm's arcane lounge: an orrery crackling with sparks on a pedestal, couches round it.</summary>
        private static void NookStorm(Transform nook, float hw, float zOut, List<UdonSharpBehaviour> behaviours, Material gold, Material darkWood, Material cushion, Material sparkGlow)
        {
            Vector3 o = new Vector3(0f, 0f, 0.6f);
            Shape(nook, "OrreryPedestal", CylMesh(), o + new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.5f, 0.7f), darkWood, true, Quaternion.identity);
            Shape(nook, "OrreryCore", BlobMesh(), o + new Vector3(0, 1.6f, 0), new Vector3(0.5f, 0.5f, 0.5f), sparkGlow, false, Quaternion.identity);
            Shape(nook, "OrreryRod", CylMesh(), o + new Vector3(0, 1.25f, 0), new Vector3(0.06f, 0.3f, 0.06f), gold, false, Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                Vector3 d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                float len = 0.6f + i * 0.25f;
                Deco(nook, PrimitiveType.Cube, "OrreryArm", o + new Vector3(0, 1.6f, 0) + d * (len / 2f), new Vector3(0.04f, 0.04f, len), gold, Quaternion.LookRotation(d), false);
                Shape(nook, "SparkOrb", BlobMesh(), o + new Vector3(0, 1.6f + (i - 1) * 0.15f, 0) + d * len, new Vector3(0.16f, 0.16f, 0.16f), sparkGlow, false, Quaternion.identity);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 p = new Vector3(sx * 1.6f, 0f, -0.9f);
                Vector3 face = o - p;
                face.y = 0f;
                MakeChair(nook, "StormCouch", p, face.normalized, cushion, darkWood, gold, behaviours, 1.3f, true);
            }
            Shape(nook, "StormCrystal", GemMesh(), new Vector3(hw - 0.9f, 0.8f, NZ(hw - 0.9f) - 0.7f), new Vector3(0.5f, 1.6f, 0.5f), sparkGlow, true, Quaternion.Euler(0, 15f, 10f));
            Shape(nook, "StormCrystal", GemMesh(), new Vector3(-(hw - 0.9f), 0.8f, NZ(hw - 0.9f) - 0.7f), new Vector3(0.5f, 1.6f, 0.5f), sparkGlow, true, Quaternion.Euler(0, -15f, -10f));
        }

        /// <summary>Life's garden nook: a little blossoming tree, planters of flowers along the wall, benches, leaves drifting.</summary>
        private static void NookLife(Transform nook, float hw, float zOut, List<UdonSharpBehaviour> behaviours, Material gold, Material wood, Material darkWood, Material cushion)
        {
            var bark = PMat("TreeBark", new Color(0.4f, 0.27f, 0.16f), 1f);
            var leaves = PMat("TreeLeaves", new Color(0.3f, 0.62f, 0.25f), 1f);
            var blossom = PMat("Blossom", new Color(1f, 0.62f, 0.8f), 1f, new Color(0.25f, 0.08f, 0.15f));
            var soil = PMat("Soil", new Color(0.25f, 0.17f, 0.1f), 1f);
            Vector3 tr = new Vector3(0f, 0f, 1.0f);
            Shape(nook, "TreeTrunk", CylMesh(), tr + new Vector3(0, 1.3f, 0), new Vector3(0.45f, 1.3f, 0.45f), bark, true, Quaternion.identity);
            Shape(nook, "TreeBase", CylMesh(), tr + new Vector3(0, 0.25f, 0), new Vector3(1.6f, 0.25f, 1.6f), PMat("GardenStone", new Color(0.62f, 0.6f, 0.55f), 1f), true, Quaternion.identity);
            var rng = new System.Random(77);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 51f * Mathf.Deg2Rad;
                Vector3 p = tr + new Vector3(Mathf.Cos(a) * 1.0f, 3.2f + (float)rng.NextDouble() * 0.9f, Mathf.Sin(a) * 0.8f);
                Shape(nook, "TreeCanopy", BlobMesh(), p, new Vector3(1.4f, 1.0f, 1.4f), leaves, false, Quaternion.identity);
                Shape(nook, "TreeBlossom", BlobMesh(), p + new Vector3(0.3f, 0.45f, -0.2f), new Vector3(0.45f, 0.35f, 0.45f), blossom, false, Quaternion.identity);
            }
            for (int i = 0; i < 4; i++)
                Shape(nook, "Leaf", GemMesh(), tr + new Vector3(-1.2f + i * 0.8f, 2.2f + (i % 2) * 0.4f, -0.8f), new Vector3(0.14f, 0.05f, 0.22f), blossom, false, Quaternion.Euler(0, i * 50f, 20f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 pl = new Vector3(sx * (hw - 1.4f), 0f, NZ(hw - 0.5f) - 0.45f);
                Block(nook, "Planter", pl + new Vector3(0, 0.35f, 0), new Vector3(1.8f, 0.7f, 0.6f), darkWood);
                Deco(nook, PrimitiveType.Cube, "PlanterSoil", pl + new Vector3(0, 0.71f, 0), new Vector3(1.7f, 0.04f, 0.5f), soil, Quaternion.identity, false);
                for (int f = 0; f < 4; f++)
                {
                    Vector3 fp = pl + new Vector3(-0.6f + f * 0.4f, 0.95f, 0f);
                    Shape(nook, "Flower", BlobMesh(), fp, new Vector3(0.3f, 0.3f, 0.3f), f % 2 == 0 ? blossom : leaves, false, Quaternion.identity);
                }
                Vector3 bench = new Vector3(sx * 1.7f, 0f, -0.8f);
                Vector3 face = tr - bench;
                face.y = 0f;
                MakeChair(nook, "GardenBench", bench, face.normalized, cushion, wood, gold, behaviours, 1.3f, false);
            }
        }

        /// <summary>Death's quiet reliquary: a candlelit altar with a skull, violet crystals, a reading lectern, a skull drifting overhead.</summary>
        private static void NookDeath(Transform nook, float hw, float zOut, List<UdonSharpBehaviour> behaviours, Material gold, Material darkWood, Material stone,
            Material cushion, Material violet, Material flame)
        {
            var bone = PMat("Bone", new Color(0.9f, 0.87f, 0.78f), 1f);
            var cloth = PMat("AltarCloth", new Color(0.32f, 0.16f, 0.42f), 1f);
            Vector3 al = new Vector3(0f, 0f, zOut - 0.9f);
            Block(nook, "Altar", al + new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1f, 0.8f), stone);
            Deco(nook, PrimitiveType.Cube, "AltarCloth", al + new Vector3(0, 1.02f, 0), new Vector3(1.9f, 0.05f, 0.9f), cloth, Quaternion.identity, false);
            Shape(nook, "AltarSkull", BlobMesh(), al + new Vector3(0, 1.25f, 0), new Vector3(0.32f, 0.36f, 0.34f), bone, false, Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                Vector3 cp = al + new Vector3(-0.75f + i * 0.375f, 1.04f, 0.25f * (i % 2 == 0 ? 1f : -1f));
                if (i == 2) continue;
                Shape(nook, "Candle", CylMesh(), cp + new Vector3(0, 0.15f + 0.05f * (i % 2), 0), new Vector3(0.08f, 0.15f + 0.05f * (i % 2), 0.08f), PMat("CandleWax", new Color(0.9f, 0.88f, 0.8f), 1f), false, Quaternion.identity);
                Shape(nook, "CandleFlame", BlobMesh(), cp + new Vector3(0, 0.38f + 0.1f * (i % 2), 0), new Vector3(0.07f, 0.13f, 0.07f), flame, false, Quaternion.identity);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Shape(nook, "DeathCrystal", GemMesh(), new Vector3(sx * (hw - 0.9f), 0.8f, NZ(hw - 0.9f) - 0.6f), new Vector3(0.6f, 1.6f, 0.6f), violet, true, Quaternion.Euler(0, 25f * sx, 9f * sx));
                MakeChair(nook, "ReliquaryChair", new Vector3(sx * 1.2f, 0f, -0.9f), new Vector3(-sx * 0.3f, 0f, 1f), cushion, darkWood, gold, behaviours, 0.7f, true);
            }
            Lectern(nook, new Vector3(-(hw - 1.5f), 0f, 0.2f), darkWood, PMat("BookPage", new Color(0.95f, 0.9f, 0.78f), 1f));
            Shape(nook, "Skull", BlobMesh(), new Vector3(0.8f, 2.6f, 0.2f), new Vector3(0.26f, 0.3f, 0.28f), bone, false, Quaternion.identity);
        }

        /// <summary>Myth's archive: tall bookshelves, a marble bust under the banner, an open book on a lectern, tomes hovering.</summary>
        private static void NookMyth(Transform nook, float hw, float zOut, List<UdonSharpBehaviour> behaviours, Material gold, Material wood, Material darkWood,
            Material cushion, Material goldGlow)
        {
            var marble = PMat("Marble", new Color(0.88f, 0.86f, 0.82f), 1f);
            foreach (float sx in new[] { -1f, 1f })
                Bookshelf(nook, "MythShelf", new Vector3(sx * 1.55f, 0f, NZ(2.2f) - 0.25f), 1.3f, 3.2f, darkWood, 50 + (int)sx);
            Vector3 b = new Vector3(0f, 0f, zOut - 0.6f);
            Block(nook, "BustPedestal", b + new Vector3(0, 0.55f, 0), new Vector3(0.6f, 1.1f, 0.6f), marble);
            Shape(nook, "BustShoulders", BlobMesh(), b + new Vector3(0, 1.3f, 0), new Vector3(0.6f, 0.3f, 0.34f), marble, false, Quaternion.identity);
            Shape(nook, "BustHead", BlobMesh(), b + new Vector3(0, 1.62f, 0), new Vector3(0.3f, 0.38f, 0.32f), marble, false, Quaternion.identity);
            Lectern(nook, new Vector3(0f, 0f, -0.6f), darkWood, PMat("BookPage", new Color(0.95f, 0.9f, 0.78f), 1f));
            for (int i = 0; i < 2; i++)
                Shape(nook, "Tome", GemMesh(), new Vector3(-0.7f + i * 1.4f, 2.4f, 0f), new Vector3(0.3f, 0.08f, 0.4f), goldGlow, false, Quaternion.Euler(0, 30f + i * 60f, 0));
            MakeChair(nook, "ArchiveChair", new Vector3(-(hw - 1.1f), 0f, -0.6f), new Vector3(1f, 0f, 0.3f), cushion, darkWood, gold, behaviours, 0.8f, true);
        }

        /// <summary>Balance's concord: the mirror (off until you switch it on), golden scales over it, a round table with chairs.</summary>
        private static void NookBalance(Transform t, Transform nook, float hw, float zOut, List<UdonSharpBehaviour> behaviours, List<GameObject> mugs, Material gold,
            Material wood, Material darkWood, Material cushion, Material sandGlow, Material pewter, Material foam)
        {
            // ---- the mirror, as before (mirrors are heavy; each player chooses)
            float mw = Mathf.Min(3.4f, hw * 2f - 0.8f);
            Block(nook, "MirrorFrame", new Vector3(0f, 2.15f, zOut - 0.07f), new Vector3(mw + 0.4f, 3.4f, 0.12f), gold);
            var mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = "Mirror";
            Object.DestroyImmediate(mirror.GetComponent<Collider>());
            mirror.transform.SetParent(nook, false);
            mirror.transform.localPosition = new Vector3(0f, 2.15f, zOut - 0.15f);
            mirror.transform.localRotation = Quaternion.identity;   // its face (-Z) to the room
            mirror.transform.localScale = new Vector3(mw, 3.0f, 1f);
            var mirrorShader = Shader.Find("FX/MirrorReflection");
            if (mirrorShader != null)
            {
                var mirrorMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/TavernMirror.mat");
                if (mirrorMat == null)
                {
                    mirrorMat = new Material(mirrorShader);
                    AssetDatabase.CreateAsset(mirrorMat, MaterialsDir + "/TavernMirror.mat");
                }
                mirror.GetComponent<Renderer>().sharedMaterial = mirrorMat;
            }
            else Debug.LogWarning("[WizardsVRC] Shader FX/MirrorReflection not found (VRChat SDK); the tavern mirror has no material.");
            mirror.AddComponent<VRCMirrorReflection>();
            mirror.SetActive(false);
            var mirrorButton = Shape(nook, "MirrorToggle", GemMesh(), new Vector3(mw / 2f + 0.45f, 1.5f, zOut - 0.25f), new Vector3(0.32f, 0.46f, 0.32f),
                PMat("MirrorGem", new Color(0.6f, 0.9f, 1f), 1f, new Color(0.3f, 0.55f, 0.7f)), false, Quaternion.identity);
            mirrorButton.isStatic = false;
            mirrorButton.AddComponent<SphereCollider>().radius = 0.6f;
            WorldText(nook, "MirrorLabel", new Vector3(mw / 2f + 0.45f, 2.05f, zOut - 0.3f), Quaternion.identity, new Vector2(500, 100), 0.004f, "Mirror", 60).color = new Color(0.8f, 0.95f, 1f);
            var mirrorToggle = UdonSharpUndo.AddComponent<LocalToggle>(mirrorButton);
            mirrorToggle.targets = new[] { mirror };
            SetInteractText(mirrorToggle, "Mirror on/off (just for you)");
            behaviours.Add(mirrorToggle);

            // ---- golden scales over the mirror, a ring hovering, a round table with chairs in front
            Vector3 sc = new Vector3(0f, 4.35f, zOut - 0.3f);
            Deco(nook, PrimitiveType.Cube, "ScalesBeam", sc, new Vector3(1.8f, 0.07f, 0.07f), gold, Quaternion.identity, false);
            Deco(nook, PrimitiveType.Cube, "ScalesPost", sc + new Vector3(0, -0.3f, 0), new Vector3(0.07f, 0.6f, 0.07f), gold, Quaternion.identity, false);
            foreach (float sx in new[] { -1f, 1f })
            {
                Deco(nook, PrimitiveType.Cube, "ScalesCord", sc + new Vector3(sx * 0.85f, -0.25f, 0), new Vector3(0.03f, 0.5f, 0.03f), gold, Quaternion.identity, false);
                Shape(nook, "ScalesPan", CylMesh(), sc + new Vector3(sx * 0.85f, -0.52f, 0), new Vector3(0.5f, 0.03f, 0.5f), gold, false, Quaternion.identity);
            }
            Shape(nook, "BalanceRing", GemMesh(), new Vector3(0f, 4.9f, zOut - 0.3f), new Vector3(0.3f, 0.3f, 0.06f), sandGlow, false, Quaternion.identity);
            StudyTable(nook, new Vector3(0f, 0f, -1.5f), wood, darkWood, mugs, pewter, foam);
            foreach (float sx in new[] { -1f, 1f })
                MakeChair(nook, "ConcordChair", new Vector3(sx * 1.2f, 0f, -1.5f), new Vector3(-sx, 0f, 0f), cushion, darkWood, gold, behaviours, 0.65f, true);
        }

        // ================================================================== furniture

        /// <summary>
        /// An upholstered chair (or a bench without a back, or a couch when wide) you can sit in: a VRCStation facing 'facing'
        /// and a Seat behaviour, like MakeSeat's stools.
        /// </summary>
        private static void MakeChair(Transform parent, string name, Vector3 floorPos, Vector3 facing, Material cushion, Material frame, Material gold,
            List<UdonSharpBehaviour> behaviours, float width, bool back)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = Child(parent, name, floorPos);
            go.localRotation = Quaternion.LookRotation(facing.normalized);
            const float seatH = 0.48f;
            Deco(go, PrimitiveType.Cube, "ChairBase", new Vector3(0, seatH * 0.5f - 0.06f, 0), new Vector3(width, seatH - 0.12f, 0.6f), frame, Quaternion.identity, false);
            Deco(go, PrimitiveType.Cube, "ChairCushion", new Vector3(0, seatH - 0.05f, 0.02f), new Vector3(width - 0.06f, 0.14f, 0.56f), cushion, Quaternion.identity, false);
            if (back)
            {
                Deco(go, PrimitiveType.Cube, "ChairBack", new Vector3(0, seatH + 0.38f, -0.27f), new Vector3(width, 0.82f, 0.14f), cushion, Quaternion.identity, false);
                Deco(go, PrimitiveType.Cube, "ChairBackTrim", new Vector3(0, seatH + 0.81f, -0.27f), new Vector3(width + 0.04f, 0.06f, 0.18f), gold, Quaternion.identity, false);
                foreach (float sx in new[] { -1f, 1f })
                    Deco(go, PrimitiveType.Cube, "ChairArm", new Vector3(sx * (width / 2f + 0.05f), seatH + 0.1f, -0.02f), new Vector3(0.1f, 0.3f, 0.58f), frame, Quaternion.identity, false);
            }
            var box = go.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, back ? 0.5f : 0.25f, -0.04f);
            box.size = new Vector3(width + 0.2f, back ? 1.0f : 0.5f, 0.66f);
            var enter = Child(go, "SitHere", new Vector3(0, seatH, 0.04f));
            var exit = Child(go, "StandHere", new Vector3(0, 0.05f, 0.8f));
            var station = go.gameObject.AddComponent<VRC.SDK3.Components.VRCStation>();
            station.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
            station.seated = true;
            station.disableStationExit = false;
            station.canUseStationFromStation = true;
            station.stationEnterPlayerLocation = enter;
            station.stationExitPlayerLocation = exit;
            var seat = UdonSharpUndo.AddComponent<Seat>(go.gameObject);
            SetInteractText(seat, "Sit");
            behaviours.Add(seat);
        }

        private static void Bookshelf(Transform parent, string name, Vector3 floorPos, float width, float height, Material wood, int seed)
        {
            var books = new[]
            {
                PMat("BookRed", new Color(0.8f, 0.2f, 0.2f), 1f), PMat("BookBlue", new Color(0.2f, 0.35f, 0.85f), 1f),
                PMat("BookGreen", new Color(0.2f, 0.65f, 0.3f), 1f), PMat("BookPurple", new Color(0.5f, 0.25f, 0.7f), 1f),
                PMat("BookGold", new Color(0.85f, 0.65f, 0.25f), 1f),
            };
            Block(parent, name, floorPos + new Vector3(0, height / 2f, 0), new Vector3(width, height, 0.4f), wood);
            var rng = new System.Random(seed);
            int rows = Mathf.Max(2, Mathf.FloorToInt(height / 0.6f) - 1);
            for (int r = 0; r < rows; r++)
            {
                float y = 0.35f + r * (height - 0.5f) / rows;
                float x = -width / 2f + 0.1f;
                while (x < width / 2f - 0.12f)
                {
                    float w = 0.06f + (float)rng.NextDouble() * 0.06f, h = 0.3f + (float)rng.NextDouble() * 0.14f;
                    Deco(parent, PrimitiveType.Cube, "Book", floorPos + new Vector3(x + w / 2f, y + h / 2f, -0.22f), new Vector3(w, h, 0.28f), books[rng.Next(books.Length)], Quaternion.identity, false);
                    x += w + 0.01f;
                }
            }
        }

        private static void StudyTable(Transform parent, Vector3 floorPos, Material wood, Material darkWood, List<GameObject> mugs, Material pewter, Material foam)
        {
            Shape(parent, "TableTop", CylMesh(), floorPos + new Vector3(0, 0.82f, 0), new Vector3(1.3f, 0.04f, 1.3f), wood, true, Quaternion.identity);
            Shape(parent, "TableLeg", CylMesh(), floorPos + new Vector3(0, 0.4f, 0), new Vector3(0.2f, 0.4f, 0.2f), darkWood, true, Quaternion.identity);
            Shape(parent, "TableFoot", CylMesh(), floorPos + new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.04f, 0.7f), darkWood, false, Quaternion.identity);
            if (mugs == null) return;
            // Mugs are pickups in the tavern's own space (MakeMug parents them; the bell brings them back here).
            Vector3 world = parent.TransformPoint(floorPos);
            Transform tavern = parent.parent;
            Vector3 local = tavern.InverseTransformPoint(world);
            mugs.Add(MakeMug(tavern, local + new Vector3(0.3f, 0.86f, 0.1f), pewter, foam));
            mugs.Add(MakeMug(tavern, local + new Vector3(-0.3f, 0.86f, -0.1f), pewter, foam));
        }

        private static void Lectern(Transform parent, Vector3 floorPos, Material wood, Material page)
        {
            Block(parent, "LecternPost", floorPos + new Vector3(0, 0.55f, 0), new Vector3(0.18f, 1.1f, 0.18f), wood);
            Deco(parent, PrimitiveType.Cube, "LecternDesk", floorPos + new Vector3(0, 1.15f, 0), new Vector3(0.7f, 0.06f, 0.5f), wood, Quaternion.Euler(-25f, 0, 0), false);
            Deco(parent, PrimitiveType.Cube, "LecternBook", floorPos + new Vector3(0, 1.2f, 0), new Vector3(0.6f, 0.04f, 0.4f), page, Quaternion.Euler(-25f, 0, 0), false);
        }

        // ================================================================== light: shafts and the slab mesh

        /// <summary>An additive material for sunbeams and the crystal's beam (LightShaft.png: soft sides, fading down its length).</summary>
        private static Material ShaftMat(string name, Color tint)
        {
            string texPath = TexturesDir + "/LightShaft.png";
            var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear || !importer.alphaIsTransparency))
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) Debug.LogWarning("[WizardsVRC] Missing " + texPath + " (run Tools/make_light_shaft.py).");
            string path = MaterialsDir + "/" + name + ".mat";
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = sh;
            mat.mainTexture = tex;
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
            else mat.color = tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// A shaft of light from 'from' to 'to', 'width' across: three soft additive planes crossing along its length (so it
        /// reads from any side), brightest where it starts. Positions in parent's space.
        /// </summary>
        private static GameObject LightShaft(Transform parent, string name, Vector3 from, Vector3 to, float width, Material mat)
        {
            if (_shaftMesh == null)
            {
                var v = new List<Vector3>();
                var uv = new List<Vector2>();
                var tri = new List<int>();
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 60f * Mathf.Deg2Rad;
                    Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f;
                    int i = v.Count;
                    v.Add(-side); v.Add(side); v.Add(side + Vector3.down); v.Add(-side + Vector3.down);
                    uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(0f, 0f));
                    tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                }
                _shaftMesh = new Mesh { name = "LightShaft" };
                _shaftMesh.SetVertices(v);
                _shaftMesh.SetUVs(0, uv);
                _shaftMesh.SetTriangles(tri, 0);
                _shaftMesh.RecalculateNormals();
                _shaftMesh.RecalculateBounds();
            }
            Vector3 d = to - from;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
            go.transform.localScale = new Vector3(width, d.magnitude, width);
            go.AddComponent<MeshFilter>().sharedMesh = _shaftMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A solid ring sector r0..r1, a0..a1 (degrees), y0..y1 above c: its top, bottom and sides (inner, outer and the ends when not a full ring), as asked.</summary>
        private static void SectorBlock(MeshBuilder mb, Vector3 c, float r0, float r1, float a0, float a1, float y0, float y1, int segs, bool top, bool bottom, bool sides)
        {
            bool full = a1 - a0 >= 359.9f;
            for (int i = 0; i < segs; i++)
            {
                float aa = Mathf.Lerp(a0, a1, i / (float)segs), ab = Mathf.Lerp(a0, a1, (i + 1) / (float)segs);
                Vector3 i0 = TPolar(c, r0, aa, y1), i1 = TPolar(c, r0, ab, y1), o0 = TPolar(c, r1, aa, y1), o1 = TPolar(c, r1, ab, y1);
                Vector3 d = new Vector3(0, y1 - y0, 0);
                if (top) mb.Quad(i0, o0, o1, i1, Vector3.up);
                if (bottom) mb.Quad(i0 - d, o0 - d, o1 - d, i1 - d, Vector3.down);
                if (sides)
                {
                    mb.Quad(i0 - d, i1 - d, i1, i0, -TDir((aa + ab) / 2f));
                    mb.Quad(o0 - d, o1 - d, o1, o0, TDir((aa + ab) / 2f));
                }
            }
            if (!sides || full) return;
            foreach (float a in new[] { a0, a1 })
            {
                Vector3 p0 = TPolar(c, r0, a, y0), p1 = TPolar(c, r1, a, y0);
                Vector3 d = new Vector3(0, y1 - y0, 0);
                Vector3 side = Vector3.Cross(Vector3.up, TDir(a)) * (a == a0 ? 1f : -1f);
                mb.Quad(p0, p1, p1 + d, p0 + d, side);
            }
        }

        /// <summary>An invisible collider from a mesh (railings to lean on).</summary>
        private static void TavernCollider(Transform parent, string name, MeshBuilder mb)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshCollider>().sharedMesh = mb.ToMesh(name);
        }
    }
}
