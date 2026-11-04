using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Background music (MusicDirector): the hub theme in the castle, the combat theme in duels, the character creation
    /// theme on the select screen and in the creator, and each zone's own track (Zone > Music, or its World's) inside the
    /// zone, its boss tower and its secret boss place. Tracks live in Audio/Music; swap a file (same name) to change a
    /// track. Music_Hub_Temp is a placeholder until the real hub theme is in (the hub zone's Music Track replaces it).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string MusicDir = "Assets/WizardsVRC/Audio/Music";
        // Same order as MusicDirector.HUB, COMBAT, CREATION. Volumes even out the recordings (the combat track is ~4 dB quieter).
        private static readonly string[] MusicTracks = { "Music_Hub_Temp", "Music_Combat_World1", "Music_CharacterCreation" };
        private static readonly float[] MusicVolumes = { 0.22f, 0.36f, 0.24f };

        /// <summary>Volume for a zone's own track (at full music volume), like the hub theme's.</summary>
        private const float AreaMusicVolume = 0.24f;

        private static MusicDirector BuildMusic(Transform systems, PlayerState playerState, Bounds hubBounds, Transform interiorRoot, InventoryBook book, WorldBuild wb)
        {
            var go = Child(systems, "MusicDirector", Vector3.zero).gameObject;
            var music = UdonSharpUndo.AddComponent<MusicDirector>(go);

            // The fixed three, then one source per distinct zone track (zones sharing a file share the source, so the
            // music carries on across them instead of starting over).
            var names = new System.Collections.Generic.List<string>(MusicTracks);
            var volumes = new System.Collections.Generic.List<float>(MusicVolumes);
            var restarts = new System.Collections.Generic.List<bool> { false, true, false };
            string hubTrack = HubMusicTrack(wb);
            if (hubTrack.Length > 0) names[MusicDirector.HUB] = hubTrack;
            var trackIndex = new System.Collections.Generic.Dictionary<string, int>();
            var areaMin = new System.Collections.Generic.List<Vector3>();
            var areaMax = new System.Collections.Generic.List<Vector3>();
            var areaTrack = new System.Collections.Generic.List<int>();
            var areaNames = new System.Collections.Generic.List<string>();
            var missing = new System.Collections.Generic.HashSet<string>();
            if (wb != null)
                for (int i = 0; i < wb.musicBounds.Count; i++)
                {
                    string t = wb.musicTracks[i];
                    int idx;
                    if (!trackIndex.TryGetValue(t, out idx))
                    {
                        if (MusicClip(t) == null)
                        {
                            if (missing.Add(t)) Debug.LogWarning("[WizardsVRC] Music: no " + t + ".mp3 / .ogg / .wav in " + MusicDir + " (wanted by " + wb.musicNames[i] + "); nothing plays there.");
                            continue;
                        }
                        idx = names.Count;
                        trackIndex[t] = idx;
                        names.Add(t);
                        volumes.Add(AreaMusicVolume);
                        restarts.Add(wb.musicRestarts[i]);
                    }
                    areaMin.Add(wb.musicBounds[i].min);
                    areaMax.Add(wb.musicBounds[i].max);
                    areaTrack.Add(idx);
                    areaNames.Add(wb.musicNames[i] + ": " + t);
                }
            music.areaMin = areaMin.ToArray();
            music.areaMax = areaMax.ToArray();
            music.areaTrack = areaTrack.ToArray();
            music.areaNames = areaNames.ToArray();

            music.sources = new AudioSource[names.Count];
            music.trackVolume = volumes.ToArray();
            music.restart = restarts.ToArray();
            int found = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var holder = Child(go.transform, names[i], Vector3.zero).gameObject;
                var src = holder.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.priority = 0;
                src.clip = MusicClip(names[i]);
                if (src.clip != null) found++;
                music.sources[i] = src;
            }
            if (found < names.Count) Debug.LogWarning("[WizardsVRC] Music: " + found + " of " + names.Count + " tracks found in " + MusicDir + ".");

            music.playerState = playerState;
            music.creator = systems.GetComponentInChildren<CharacterCreator>(true);
            music.awakening = systems.GetComponentInChildren<AwakeningSequence>(true);

            // The hub track: the castle grounds and the keep (its rooms, the study, the classrooms and portal room).
            Bounds keep = new Bounds(interiorRoot != null ? interiorRoot.position : Vector3.zero, Vector3.zero);
            bool any = false;
            if (interiorRoot != null)
                foreach (var r in interiorRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { keep = r.bounds; any = true; }
                    else keep.Encapsulate(r.bounds);
                }
            keep.Expand(new Vector3(4f, 6f, 4f));
            music.hubMin = any ? new[] { hubBounds.min, keep.min } : new[] { hubBounds.min };
            music.hubMax = any ? new[] { hubBounds.max, keep.max } : new[] { hubBounds.max };

            // Quiet inside every jukebox room (the castle tavern, Archrock's saloon), in the same circle the jukebox is heard in.
            var centers = new System.Collections.Generic.List<Transform>();
            var radii = new System.Collections.Generic.List<float>();
            var heights = new System.Collections.Generic.List<float>();
            foreach (var jb in Object.FindObjectsOfType<TavernJukebox>(true))
            {
                if (jb.center == null) continue;
                centers.Add(jb.center);
                radii.Add(jb.radius);
                heights.Add(jb.height);
            }
            music.quietCenters = centers.ToArray();
            music.quietRadii = radii.ToArray();
            music.quietHeights = heights.ToArray();
            if (book != null) book.music = music;
            Debug.Log("[WizardsVRC] Music: hub, combat and character creation themes, " + (names.Count - MusicTracks.Length) +
                      " zone track(s) in " + areaMin.Count + " area(s) (quiet in " + centers.Count + " jukebox room(s)).");
            return music;
        }

        /// <summary>The hub zone's own Music Track (Zone > Music), or "" to keep the castle theme file.</summary>
        private static string HubMusicTrack(WorldBuild wb)
        {
            if (wb == null || wb.data == null || wb.data.zones == null) return "";
            foreach (var z in wb.data.zones)
                if (z != null && z.isHub && z.includeInBuild)
                {
                    string t = MusicTrackName(z.musicTrack);
                    if (t.Length == 0) return "";
                    if (MusicClip(t) != null) return t;
                    Debug.LogWarning("[WizardsVRC] Music: no " + t + ".mp3 / .ogg / .wav in " + MusicDir + " (the hub's Music Track); keeping " + MusicTracks[MusicDirector.HUB] + ".");
                    return "";
                }
            return "";
        }

        /// <summary>Loads a track, setting it up to stream (they're long) the first time.</summary>
        private static AudioClip MusicClip(string name)
        {
            string path = MusicDir + "/" + name + ".mp3";
            if (!System.IO.File.Exists(path)) path = MusicDir + "/" + name + ".ogg";
            if (!System.IO.File.Exists(path)) path = MusicDir + "/" + name + ".wav";
            if (!System.IO.File.Exists(path)) return null;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var s = importer.defaultSampleSettings;
                if (s.loadType != AudioClipLoadType.Streaming || s.compressionFormat != AudioCompressionFormat.Vorbis || importer.loadInBackground != true)
                {
                    s.loadType = AudioClipLoadType.Streaming;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    importer.defaultSampleSettings = s;
                    importer.loadInBackground = true;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
