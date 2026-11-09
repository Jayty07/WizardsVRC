using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Making Rebuild MainWorld faster, and seeing where its time goes:
    ///  - Every rebuild logs how long each stage took ("[WizardsVRC] Rebuild took ..."), slowest first.
    ///  - Generated pictures (card art, map pages, skylines) are only written and re-imported when they actually changed;
    ///    an unchanged picture keeps its imported (and compressed) copy, so most rebuilds import almost nothing.
    ///  - The hub's occlusion bake (StaticOcclusionCulling.Compute, minutes on its own) is off by default: tick
    ///    WizardsVRC > Options > Bake Occlusion On Rebuild, or run WizardsVRC > Bake Hub Occlusion once before you upload.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string BakeOcclusionPref = "WizardsVRC.BakeOcclusionOnRebuild";
        private const string BakeOcclusionMenu = "WizardsVRC/Options/Bake Occlusion On Rebuild";

        /// <summary>Bake the hub's occlusion culling at the end of every rebuild (slow; off unless ticked in the menu).</summary>
        private static bool BakeOcclusionOnRebuild { get { return EditorPrefs.GetBool(BakeOcclusionPref, false); } }

        [MenuItem(BakeOcclusionMenu)]
        private static void ToggleBakeOcclusion()
        {
            EditorPrefs.SetBool(BakeOcclusionPref, !BakeOcclusionOnRebuild);
            Debug.Log("[WizardsVRC] Bake occlusion on rebuild: " + (BakeOcclusionOnRebuild ? "on" : "off (run WizardsVRC > Bake Hub Occlusion before uploading)") + ".");
        }

        [MenuItem(BakeOcclusionMenu, true)]
        private static bool ToggleBakeOcclusionCheck()
        {
            Menu.SetChecked(BakeOcclusionMenu, BakeOcclusionOnRebuild);
            return true;
        }

        // ------------------------------------------------------------------ timing

        private static readonly System.Diagnostics.Stopwatch _buildClock = new System.Diagnostics.Stopwatch();
        private static readonly List<KeyValuePair<string, double>> _buildLaps = new List<KeyValuePair<string, double>>();
        private static double _lastLap;
        private static int _pngsWritten, _pngsKept;

        private static void StartBuildTimer()
        {
            _buildLaps.Clear();
            _buildClock.Reset();
            _buildClock.Start();
            _lastLap = 0;
            _pngsWritten = 0;
            _pngsKept = 0;
        }

        /// <summary>Marks the end of a build stage (its time since the last mark is recorded).</summary>
        private static void Lap(string stage)
        {
            double now = _buildClock.Elapsed.TotalSeconds;
            _buildLaps.Add(new KeyValuePair<string, double>(stage, now - _lastLap));
            _lastLap = now;
        }

        /// <summary>Logs the whole rebuild's time and each stage's, slowest first.</summary>
        private static void ReportBuildTimer()
        {
            _buildClock.Stop();
            var laps = new List<KeyValuePair<string, double>>(_buildLaps);
            laps.Sort((a, b) => b.Value.CompareTo(a.Value));
            var parts = new List<string>();
            foreach (var l in laps) parts.Add(l.Key + " " + l.Value.ToString("0.0") + "s");
            Debug.Log("[WizardsVRC] Rebuild took " + _buildClock.Elapsed.TotalSeconds.ToString("0.0") + "s. By stage, slowest first: " + string.Join(", ", parts) +
                      ". Generated pictures: " + _pngsWritten + " changed, " + _pngsKept + " unchanged (not re-imported)." +
                      (BakeOcclusionOnRebuild ? "" : " Occlusion wasn't baked (WizardsVRC > Bake Hub Occlusion before uploading)."));
        }

        // ------------------------------------------------------------------ pictures

        /// <summary>
        /// Writes a generated PNG only when its bytes differ from the file already there (or there's no file, or Unity hasn't
        /// imported it). Returns true when it was written and imported; false means the imported copy is already right.
        /// </summary>
        private static bool WritePngIfChanged(string path, byte[] png, ImportAssetOptions options = ImportAssetOptions.Default)
        {
            if (File.Exists(path) && AssetImporter.GetAtPath(path) != null)
            {
                byte[] old = File.ReadAllBytes(path);
                if (SameBytes(old, png)) { _pngsKept++; return false; }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, options);
            _pngsWritten++;
            return true;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
