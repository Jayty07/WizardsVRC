using UnityEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Ambient sound (Audio/Ambient, made by Tools/make_ambient_sounds.py): every area has a looping bed under the music
    /// that suits it (breeze and birds in the castle and town streets, waves at the docks, the forge's roar and clanks,
    /// the forest's birds and rustling leaves, cold wind at the keep, desert wind, the mine's drips, the railyard's steam,
    /// thunder on the mesa, crickets and owls at Lonesome Bend, torches in the towers, the deep for the Kraken, the ghost
    /// train's clatter), and fires and waterfalls have their own sound you hear as you come near. A bed sits on its area's
    /// root, so it plays only while you're there (the area culling switches it off with the rest).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string AmbientDir = "Assets/WizardsVRC/Audio/Ambient";
        private const float AmbientBedVolume = 0.3f;

        /// <summary>Loads an ambient loop, set up the first time as a compressed, in-memory clip (they're short loops).</summary>
        private static AudioClip AmbientClip(string name)
        {
            string path = AmbientDir + "/" + name + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return null;
            var s = importer.defaultSampleSettings;
            if (s.loadType != AudioClipLoadType.CompressedInMemory || s.compressionFormat != AudioCompressionFormat.Vorbis || !importer.loadInBackground)
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.45f;
                importer.defaultSampleSettings = s;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>A looping 2D bed on an area's root (heard everywhere in the area, only while it's switched on).</summary>
        private static AudioSource AmbientBed(Transform root, string clip, float volume = AmbientBedVolume)
        {
            var c = AmbientClip(clip);
            if (root == null || c == null) return null;
            var go = Child(root, "Ambience", Vector3.zero).gameObject;
            go.isStatic = false;
            var src = go.AddComponent<AudioSource>();
            src.clip = c;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0f;
            src.volume = volume;
            src.priority = 200;
            return src;
        }

        /// <summary>A looping 3D sound at a spot (a fire, falling water): full within 'near' metres, fading out by 'far'.</summary>
        private static AudioSource AmbientPoint(Transform parent, Vector3 localPos, string clip, float volume, float near, float far)
        {
            var c = AmbientClip(clip);
            if (parent == null || c == null) return null;
            var go = Child(parent, "AmbientSound", localPos).gameObject;
            go.isStatic = false;
            var src = go.AddComponent<AudioSource>();
            src.clip = c;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 1f;
            src.volume = volume;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = near;
            src.maxDistance = far;
            src.dopplerLevel = 0f;
            src.priority = 180;
            return src;
        }

        /// <summary>The bed for a zone, by where it is and how it looks.</summary>
        private static string AmbientFor(ZoneDefinition z)
        {
            switch (z.zoneKey)
            {
                case "LonesomeBend": return "Amb_NightDusk";
                case "ThunderMesa": return "Amb_MesaStorm";
                case "IronLineRailyard": return "Amb_Railyard";
            }
            switch (z.textureStyle)
            {
                case ZoneTextureStyle.Mine: return "Amb_Mine";
                case ZoneTextureStyle.Boomtown: return "Amb_Boomtown";
                case ZoneTextureStyle.Desert: return "Amb_DesertWind";
                case ZoneTextureStyle.Docks: return "Amb_Sea";
                case ZoneTextureStyle.Forge: return "Amb_Forge";
                case ZoneTextureStyle.Forest: return "Amb_Forest";
                case ZoneTextureStyle.Keep: return "Amb_ColdWind";
                default: return "Amb_Breeze";
            }
        }
    }
}
