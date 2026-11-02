using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Later worlds (World 2 on): each has a town (a zone with Is Town, e.g. Archrock) that plays the castle's part for
    /// that world. You reach it through its arch in the portal room beneath the Headmaster's tower (open once the world
    /// before is finished), and an arch at the town's S takes you back. The town's Street Gates are tunnels out to the
    /// world's streets, built like the castle's, so those streets' tunnels home lead to the town instead.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class TownLink
        {
            public ZoneDefinition zone;
            public Transform spawn;
            public GameObject root;
        }

        /// <summary>Street key -> the town tunnel that leads there, and the town it's in.</summary>
        private static readonly Dictionary<string, HubGate> _townGates = new Dictionary<string, HubGate>();
        private static readonly Dictionary<string, ZoneDefinition> _townOf = new Dictionary<string, ZoneDefinition>();
        /// <summary>Town key -> its quest-guide area index.</summary>
        private static readonly Dictionary<string, int> _townArea = new Dictionary<string, int>();
        /// <summary>World number -> its town.</summary>
        private static readonly Dictionary<int, TownLink> _worldTowns = new Dictionary<int, TownLink>();
        /// <summary>The keep interior (the portal room is in it), for the arches between worlds.</summary>
        private static Transform _interiorRoot;
        private static readonly Dictionary<int, Transform> _archFronts = new Dictionary<int, Transform>();

        private static readonly float[] PortalArchAngles = { 0f, -40f, 40f, -80f, 80f };

        /// <summary>Direction (in the portal room) from its middle to world n's arch.</summary>
        private static Vector3 PortalArchDir(int world)
        {
            int i = Mathf.Clamp(world - 1, 0, PortalArchAngles.Length - 1);
            return Quaternion.Euler(0, PortalArchAngles[i], 0) * Vector3.forward;
        }

        /// <summary>Where you step out in the portal room, in front of world n's arch and facing into the room.</summary>
        private static Transform PortalArchFront(int world)
        {
            if (_interiorRoot == null) return null;
            Transform t;
            if (_archFronts.TryGetValue(world, out t) && t != null) return t;
            Vector3 dir = PortalArchDir(world);
            t = Child(_interiorRoot, "World" + world + "ArchFront", PortalRoomCenter + dir * (PortalRoomRadius - 3.6f) + new Vector3(0, 0.05f, 0));
            t.localRotation = Quaternion.LookRotation(-dir);
            _archFronts[world] = t;
            return t;
        }

        /// <summary>
        /// The arch home at a town's S: two banded sandstone pillars with a rough lintel and a glowing veil between them.
        /// Click it to go back to the portal room. 'inward' points into the town. Returns the arch (guide arrows aim at it).
        /// </summary>
        private static GameObject TownPortalArch(Transform root, Vector3 at, Vector3 inward, Transform arrival, GameObject townRoot, List<UdonSharpBehaviour> behaviours)
        {
            inward.y = 0f;
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.forward;
            Quaternion face = Quaternion.LookRotation(inward);
            var rock = PsxTexMat("ArchSandstone", new Color(0.78f, 0.45f, 0.3f), TileTexture(TerrainTexDir + "/RedRock.png"), 2.5f);
            var veil = PMat("TownArchVeil", new Color(1f, 0.7f, 0.4f), 1f, new Color(1f, 0.55f, 0.25f) * 1.5f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            Transform arch = Child(root, "PortalArchHome", at);
            arch.localRotation = face;
            foreach (float s in new[] { -1f, 1f })
            {
                Block(arch, "ArchPillar", new Vector3(s * 2.6f, 2.8f, 0), new Vector3(1.4f, 5.6f, 1.6f), rock, Quaternion.identity);
                Block(arch, "ArchPillarBase", new Vector3(s * 2.6f, 0.35f, 0), new Vector3(1.9f, 0.7f, 2f), rock, Quaternion.identity);
            }
            Block(arch, "ArchLintel", new Vector3(0, 6.1f, 0), new Vector3(7.4f, 1.3f, 1.8f), rock, Quaternion.identity);
            Block(arch, "ArchCap", new Vector3(0.4f, 7f, 0.1f), new Vector3(4.6f, 0.6f, 1.5f), rock, Quaternion.Euler(0, 0, 3f));
            Shape(arch, "ArchKeystone", GemMesh(), new Vector3(0, 6.1f, 0.95f), new Vector3(0.45f, 0.7f, 0.25f), gold, false, Quaternion.identity);
            var lit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lit.name = "ArchVeil";
            lit.transform.SetParent(arch, false);
            lit.transform.localPosition = new Vector3(0, 2.7f, 0);
            lit.transform.localScale = new Vector3(3.8f, 5.4f, 0.1f);
            lit.GetComponent<Renderer>().sharedMaterial = veil;
            Object.DestroyImmediate(lit.GetComponent<Collider>());
            PointLight(arch, new Vector3(0, 3f, 1.2f), new Color(1f, 0.7f, 0.45f), 10f, 1.2f);
            // Read from inside the town, looking at the arch (the canvas faces the way the reader looks: -inward).
            var label = WorldText(arch, "ArchSign", new Vector3(0, 8f, 0.2f), Quaternion.Euler(0, 180f, 0), new Vector2(1100, 200), 0.005f,
                "Starfall Isle\n(click the arch)", 54);
            label.color = new Color(1f, 0.86f, 0.55f);
            DressSign(label, 1000f, 190f);
            if (arrival != null)
            {
                var click = Child(arch, "ArchClick", new Vector3(0, 2.7f, 0));
                var box = click.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 5.4f, 0.6f);
                var home = UdonSharpUndo.AddComponent<TeleportHelper>(click.gameObject);
                home.defaultTarget = arrival;
                home.hideZones = new[] { townRoot };
                SetInteractText(home, "Step through to Starfall's portal room");
                behaviours.Add(home);
            }
            return arch.gameObject;
        }

        /// <summary>Plan rectangles (zone-local) taken by a town's gate tunnels, so its houses and edge walls keep clear of them.</summary>
        private static List<UnityEngine.Rect> TownTunnelRects(ZoneGrid g, ZoneDefinition z, WorldDataStore.Data data)
        {
            var list = new List<UnityEngine.Rect>();
            if (z.landmarks == null) return list;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.StreetGate || GateTarget(l, data) == null) continue;
                var cells = g.Find(l.Symbol);
                if (cells.Count == 0) continue;
                Vector3 mouth, outward;
                TunnelMouth(g, z, cells[0], out mouth, out outward);
                Vector3 right = Vector3.Cross(Vector3.up, outward);
                float hwT = TunW / 2f + TunT + 1f;
                float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
                foreach (float a in new[] { -hwT, TunL2 + hwT })
                    foreach (float b in new[] { -1f, TunL1 + TunL3 + 1f })
                    {
                        Vector3 c = mouth + right * a + outward * b;
                        x0 = Mathf.Min(x0, c.x); x1 = Mathf.Max(x1, c.x); z0 = Mathf.Min(z0, c.z); z1 = Mathf.Max(z1, c.z);
                    }
                list.Add(UnityEngine.Rect.MinMaxRect(x0, z0, x1, z1));
            }
            return list;
        }

        /// <summary>The town for a world (null if it has none in this build).</summary>
        private static TownLink TownOfWorld(int world)
        {
            TownLink t;
            return _worldTowns.TryGetValue(world, out t) ? t : null;
        }
    }
}
