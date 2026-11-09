using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Treasure caches (Cache landmarks): the payoff at the end of a side alley. A chest on a low plinth with its back to
    /// the alley's end wall, a glint turning over it and its name on a little sign; click it for gold (TreasureCache). Each
    /// world's caches look like its streets: an iron-bound chest in Starfall, a strongbox in Dustwind, a riveted kobold
    /// lockbox with a neon tag in Bigdig.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly string[] CacheNamesW1 = { "Hidden chest", "Forgotten coffer", "Lost satchel" };
        private static readonly string[] CacheNamesW2 = { "Buried strongbox", "Outlaw's stash", "Prospector's cache" };
        private static readonly string[] CacheNamesW3 = { "Smuggler's lockbox", "Kobold nest egg", "Forgotten payroll" };

        private static void BuildCaches(Transform root, ZoneGrid g, ZoneDefinition z, PlayerState playerState, WorldBuild wb, Material wood, Material glow)
        {
            if (z.landmarks == null) return;
            int n = 0;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.Cache) continue;
                foreach (var cell in g.Find(l.Symbol))
                {
                    string[] names = z.world >= 3 ? CacheNamesW3 : z.world == 2 ? CacheNamesW2 : CacheNamesW1;
                    string name = string.IsNullOrEmpty(l.label) ? names[(cell.x * 7 + cell.y * 3) % names.Length] : l.label;
                    BuildCache(root, g, z, cell, name, n++, playerState, wb, wood, glow);
                }
            }
        }

        private static void BuildCache(Transform root, ZoneGrid g, ZoneDefinition z, Vector2Int cell, string name, int index, PlayerState playerState,
            WorldBuild wb, Material wood, Material glow)
        {
            Vector3 open = OpenFront(g, cell);
            Vector3 p = g.Local(cell) - open * (g.cell * 0.28f);
            var site = Child(root, "Cache" + index, p);
            site.localRotation = Quaternion.LookRotation(open);   // the chest's front (local +Z) faces the way you came
            bool undercity = z.textureStyle == ZoneTextureStyle.Undercity;
            bool desert = z.world == 2;
            var band = undercity ? PMat("CacheBrass", new Color(0.8f, 0.6f, 0.25f), 1f) : PMat("Iron", new Color(0.25f, 0.22f, 0.22f), 1f);
            var body = undercity ? PMat("CacheSteel", new Color(0.32f, 0.33f, 0.36f), 1f)
                : desert ? PMat("CacheStrongbox", new Color(0.36f, 0.24f, 0.14f), 2f) : PMat("CacheOak", new Color(0.48f, 0.3f, 0.16f), 2f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var stone = PMat("CachePlinth", Color.Lerp(z.lowWallColor, new Color(0.45f, 0.43f, 0.42f), 0.5f), 3f);

            Block(site, "Plinth", new Vector3(0, 0.15f, 0), new Vector3(1.9f, 0.3f, 1.3f), stone);
            Block(site, "Chest", new Vector3(0, 0.62f, 0), new Vector3(1.4f, 0.64f, 0.85f), body);
            foreach (float x in new[] { -0.5f, 0.5f })
                Block(site, "ChestBand", new Vector3(x, 0.62f, 0), new Vector3(0.1f, 0.68f, 0.9f), band);
            Block(site, "Coins", new Vector3(0, 0.95f, 0), new Vector3(1.2f, 0.08f, 0.7f), gold);
            // The lid hinges at the back edge.
            var lid = Child(site, "LidHinge", new Vector3(0, 0.94f, -0.43f));
            Block(lid, "Lid", new Vector3(0, 0.12f, 0.43f), new Vector3(1.46f, 0.24f, 0.9f), body);
            foreach (float x in new[] { -0.5f, 0.5f })
                Block(lid, "LidBand", new Vector3(x, 0.12f, 0.43f), new Vector3(0.1f, 0.28f, 0.95f), band);
            Block(lid, "Lock", new Vector3(0, 0.02f, 0.9f), new Vector3(0.22f, 0.26f, 0.08f), gold);
            if (undercity)
                Block(site, "NeonTag", new Vector3(0.55f, 1.05f, 0.44f), new Vector3(0.36f, 0.12f, 0.03f), NeonMat(index));
            var glint = Shape(site, "Glint", GemMesh(), new Vector3(0, 1.75f, 0), new Vector3(0.32f, 0.5f, 0.32f), glow, false, Quaternion.identity);
            var hit = site.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, 0.7f, 0);
            hit.size = new Vector3(1.9f, 1.4f, 1.3f);

            var label = WorldText(site, "CacheSign", new Vector3(0, 2.45f, 0), Quaternion.identity, new Vector2(900, 200), 0.0035f, name, 42);
            DressSign(label, 0f, 160f, false);

            var cache = UdonSharpUndo.AddComponent<TreasureCache>(site.gameObject);
            cache.playerState = playerState;
            cache.cacheName = name;
            // About two or three of the street's fights' worth of gold.
            int lo = z.world >= 3 ? 90 : z.world == 2 ? 50 : 20;
            cache.goldMin = lo;
            cache.goldMax = lo * 2;
            cache.refillSeconds = 1200f;
            cache.lid = lid;
            cache.glint = glint;
            cache.label = label;
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(cache);
            if (backing != null) backing.interactText = "Open the " + name.ToLowerInvariant();
            wb.behaviours.Add(cache);
            wb.caches.Add(cache);
        }
    }
}
