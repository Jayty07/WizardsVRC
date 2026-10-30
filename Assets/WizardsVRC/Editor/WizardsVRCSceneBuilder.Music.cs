using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Background music (MusicDirector): the hub theme in the castle, the combat theme in duels and the character
    /// creation theme on the select screen and in the creator. Tracks live in Audio/Music; swap a file (same name) to
    /// change a track. Music_Hub_Temp is a placeholder until the real hub theme is in.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string MusicDir = "Assets/WizardsVRC/Audio/Music";
        // Same order as MusicDirector.HUB, COMBAT, CREATION. Volumes even out the recordings (the combat track is ~4 dB quieter).
        private static readonly string[] MusicTracks = { "Music_Hub_Temp", "Music_Combat_World1", "Music_CharacterCreation" };
        private static readonly float[] MusicVolumes = { 0.22f, 0.36f, 0.24f };

        private static MusicDirector BuildMusic(Transform systems, PlayerState playerState, Bounds hubBounds, Transform interiorRoot, InventoryBook book)
        {
            var go = Child(systems, "MusicDirector", Vector3.zero).gameObject;
            var music = UdonSharpUndo.AddComponent<MusicDirector>(go);
            music.sources = new AudioSource[MusicTracks.Length];
            music.trackVolume = (float[])MusicVolumes.Clone();
            music.restart = new[] { false, true, false };
            int found = 0;
            for (int i = 0; i < MusicTracks.Length; i++)
            {
                var holder = Child(go.transform, MusicTracks[i], Vector3.zero).gameObject;
                var src = holder.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0f;
                src.priority = 0;
                src.clip = MusicClip(MusicTracks[i]);
                if (src.clip != null) found++;
                music.sources[i] = src;
            }
            if (found < MusicTracks.Length) Debug.LogWarning("[WizardsVRC] Music: " + found + " of " + MusicTracks.Length + " tracks found in " + MusicDir + ".");

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

            // Quiet inside the tavern, where the jukebox plays (the same circle it's heard in).
            var jukebox = Object.FindObjectOfType<TavernJukebox>(true);
            if (jukebox != null && jukebox.center != null)
            {
                music.quietCenter = jukebox.center;
                music.quietRadius = jukebox.radius;
                music.quietHeight = jukebox.height;
            }
            if (book != null) book.music = music;
            Debug.Log("[WizardsVRC] Music: hub, combat and character creation themes" + (jukebox != null ? " (quiet in the tavern)" : "") + ".");
            return music;
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
