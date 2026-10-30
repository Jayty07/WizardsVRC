using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Dungeons, Wizard101 style: a street can wall off part of itself as a dungeon. Paint a Dungeon Sigil landmark
    /// (the four-pad sigil, outside), Dungeon Gate cells across the way in (barred for good: the sigil is the only way in),
    /// a Dungeon Start cell inside (where the party arrives) and any number of Dungeon Exit cells inside (doors out). The
    /// inside is everything you can walk to from the start without crossing the gate. Wizards defeated inside go back to
    /// the start, not out to the street.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        // Set while a street is being built (its encounters look them up for where defeated wizards go).
        private static Transform _dungeonSystems;
        private static Transform _dungeonStart;
        private static HashSet<Vector2Int> _dungeonCells;

        private static void DungeonLandmark(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark sigilMark, List<UdonSharpBehaviour> behaviours,
            Material stone, Material gold)
        {
            var sigilCells = g.Find(sigilMark.Symbol);
            Vector2Int startCell = new Vector2Int(-1, -1);
            var exits = new List<Vector2Int>();
            var gates = new HashSet<Vector2Int>();
            foreach (var l in z.landmarks)
            {
                if (l == null) continue;
                if (l.kind == LandmarkKind.DungeonStart) { var c = g.Find(l.Symbol); if (c.Count > 0) startCell = c[0]; }
                else if (l.kind == LandmarkKind.DungeonExit) exits.AddRange(g.Find(l.Symbol));
                else if (l.kind == LandmarkKind.DungeonGate) foreach (var c in g.Find(l.Symbol)) gates.Add(c);
            }
            if (sigilCells.Count == 0 || startCell.x < 0)
            {
                Debug.LogWarning("[WizardsVRC World] Zone '" + z.zoneKey + "' has a Dungeon Sigil but no Dungeon Start cell; no dungeon built.");
                return;
            }
            string name = string.IsNullOrEmpty(sigilMark.label) ? z.displayName : sigilMark.label;
            float cs = g.cell;

            // ---- the inside: everything walkable from the start without crossing the gate
            var inside = new HashSet<Vector2Int> { startCell };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(startCell);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in ZoneGrid.Dirs)
                {
                    var n = p + d;
                    if (n.x < 0 || n.y < 0 || n.x >= g.width || n.y >= g.height || inside.Contains(n) || gates.Contains(n) || !g.Walkable(n.x, n.y)) continue;
                    inside.Add(n);
                    queue.Enqueue(n);
                }
            }
            if (inside.Contains(sigilCells[0]))
                Debug.LogWarning("[WizardsVRC World] Zone '" + z.zoneKey + "': the Dungeon Sigil can be walked to from the Dungeon Start without crossing a " +
                                 "Dungeon Gate cell; paint gate cells right across the way in.");
            Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
            foreach (var c in inside)
            {
                Vector3 p = g.Local(c);
                lo = Vector3.Min(lo, p - new Vector3(cs / 2f, 4f, cs / 2f));
                hi = Vector3.Max(hi, p + new Vector3(cs / 2f, 30f, cs / 2f));
            }
            _dungeonCells = inside;

            // ---- the barred way in
            Material iron = PMat("RailingIron", new Color(0.17f, 0.16f, 0.21f), 1f);
            foreach (var c in gates)
            {
                Vector3 p = g.Local(c);
                // The bars run across the way through: along X if the passage runs north-south, along Z otherwise.
                bool ns = g.Walkable(c.x, c.y - 1) || g.Walkable(c.x, c.y + 1);
                Quaternion rot = Quaternion.Euler(0, ns ? 0f : 90f, 0);
                Transform bar = Child(root, "DungeonGate", p);
                bar.localRotation = rot;
                var wall = bar.gameObject.AddComponent<BoxCollider>();
                wall.center = new Vector3(0, 2.5f, 0);
                wall.size = new Vector3(cs + 0.2f, 5f, 0.6f);
                for (float x = -cs / 2f + 0.3f; x < cs / 2f; x += 0.45f)
                    Deco(bar, PrimitiveType.Cube, "Bar", new Vector3(x, 2.4f, 0), new Vector3(0.09f, 4.8f, 0.09f), iron, Quaternion.identity, false);
                foreach (float y in new[] { 0.9f, 2.6f, 4.4f })
                    Deco(bar, PrimitiveType.Cube, "Rail", new Vector3(0, y, 0), new Vector3(cs, 0.12f, 0.12f), iron, Quaternion.identity, false);
            }

            // ---- the start inside, facing away from the gate
            Vector3 startPos = g.Local(startCell);
            Vector3 gateMid = Vector3.zero;
            foreach (var c in gates) gateMid += g.Local(c);
            Vector3 away = gates.Count > 0 ? startPos - gateMid / gates.Count : Vector3.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            Transform start = Child(root, "DungeonStart", startPos + new Vector3(0, 0.1f, 0));
            start.localRotation = Quaternion.LookRotation(away.normalized);
            _dungeonStart = start;

            // ---- the sigil: a glowing rune circle with four pads round it (north, east, south, west)
            Vector3 sc = g.Local(sigilCells[0]);
            Transform sigilRoot = Child(root, "DungeonSigil", sc + new Vector3(0, RingLift, 0));
            Color purple = new Color(0.65f, 0.4f, 1f);
            FlatDecal(sigilRoot, "SigilRunes", Vector3.zero, 5.4f,
                CutoutMat("DungeonSigilRunes", PixelTexture(TexturesDir + "/DuelCircleRunes.png"), purple, purple * 0.8f));
            FlatDecal(sigilRoot, "SigilCore", new Vector3(0, 0.015f, 0), 3.2f,
                CutoutMat("DungeonSigilCore", PixelTexture(TexturesDir + "/DuelCircleSigil.png"), purple, purple * 0.6f));
            Texture2D padTex = PixelTexture(TexturesDir + "/DuelSeatRing.png");
            Material idle = CutoutMat("DungeonPadIdle", padTex, new Color(0.6f, 0.55f, 0.75f), new Color(0.2f, 0.15f, 0.3f));
            Material taken = CutoutMat("DungeonPadTaken", padTex, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.3f) * 1.4f);
            var pads = new Transform[DungeonSigil.PADS];
            var padRenderers = new Renderer[DungeonSigil.PADS];
            for (int i = 0; i < DungeonSigil.PADS; i++)
            {
                Vector3 dir = Quaternion.Euler(0, i * 90f, 0) * Vector3.forward;
                pads[i] = Child(sigilRoot, "Pad" + i, dir * 2.4f);
                padRenderers[i] = FlatDecal(pads[i], "PadRing", new Vector3(0, 0.03f, 0), 1.8f, idle).GetComponent<Renderer>();
            }
            Vector3 toGate = gates.Count > 0 ? gateMid / gates.Count - sc : -away;
            toGate.y = 0f;
            Vector3 face = toGate.sqrMagnitude > 0.01f ? toGate.normalized : Vector3.forward;
            // Canvas +Z = the way the viewer looks: someone at the sigil looking at the gate.
            Text status = WorldText(root, "DungeonStatus", sc + new Vector3(0, 3.4f, 0) + face * 1.5f, Quaternion.LookRotation(face), new Vector2(1500, 260), 0.0045f, name, 44);
            status.color = new Color(0.85f, 0.75f, 1f);
            Transform outside = Child(root, "DungeonOutside", sc - face * 4f + new Vector3(0, 0.1f, 0));
            outside.localRotation = Quaternion.LookRotation(face);

            // ---- the referee: under Systems (synced things stay on when the street is switched off for you)
            var holder = Child(_dungeonSystems != null ? _dungeonSystems : root, "Dungeon_" + z.zoneKey, Vector3.zero);
            var sigil = UdonSharpUndo.AddComponent<DungeonSigil>(holder.gameObject);
            sigil.dungeonName = name;
            sigil.pads = pads;
            sigil.padRenderers = padRenderers;
            sigil.padIdle = idle;
            sigil.padTaken = taken;
            sigil.statusLabel = status;
            sigil.start = start;
            sigil.outside = outside;
            Vector3 rootPos = root.position;
            sigil.boundsMin = rootPos + lo;
            sigil.boundsMax = rootPos + hi;
            behaviours.Add(sigil);

            // ---- exits: a glowing doorway; click it to leave
            Material glow = PMat("DungeonExitGlow", purple, 1f, purple * 1.2f);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(sigil);
            foreach (var c in exits)
            {
                Vector3 p = g.Local(c);
                Transform door = Child(root, "DungeonExit", p);
                door.localRotation = Quaternion.LookRotation(-away.normalized);
                Deco(door, PrimitiveType.Cube, "PostL", new Vector3(-1.3f, 1.6f, 0), new Vector3(0.4f, 3.2f, 0.4f), stone, Quaternion.identity, true);
                Deco(door, PrimitiveType.Cube, "PostR", new Vector3(1.3f, 1.6f, 0), new Vector3(0.4f, 3.2f, 0.4f), stone, Quaternion.identity, true);
                Deco(door, PrimitiveType.Cube, "Lintel", new Vector3(0, 3.4f, 0), new Vector3(3f, 0.4f, 0.5f), gold, Quaternion.identity, false);
                var veil = Deco(door, PrimitiveType.Cube, "ExitVeil", new Vector3(0, 1.6f, 0), new Vector3(2.2f, 3.2f, 0.1f), glow, Quaternion.identity, true);
                var relay = UdonSharpUndo.AddComponent<EventRelay>(veil);
                relay.target = sigil;
                relay.eventName = nameof(DungeonSigil.LeaveDungeon);
                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(relay);
                if (backing != null) backing.interactText = "Leave " + name;
                behaviours.Add(relay);
                WorldText(door, "ExitSign", new Vector3(0, 4.2f, -0.3f), Quaternion.identity, new Vector2(1000, 160), 0.004f, "Leave " + name + " (click)", 44)
                    .color = new Color(0.85f, 0.75f, 1f);
            }
            if (target == null) Debug.LogWarning("[WizardsVRC World] Couldn't wire the dungeon exits in '" + z.zoneKey + "'.");
        }
    }
}
