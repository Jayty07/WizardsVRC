using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The portal room beneath the Headmaster's tower: a round stone chamber with five arches, one per world (see the
    /// story plan: travel between worlds goes through here; zones inside a world are reached on foot). Starfall Isle's
    /// arch always glows. World 2's arch is dark until the World 1 finale (quest 16), then glows ("coming soon" until
    /// World 2 exists). The other three stay dark. Reached by a door on the west wall of the Headmaster's study.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 PortalRoomCenter = new Vector3(0f, 0f, -40f); // in the keep interior's space
        private const float PortalRoomRadius = 10f;

        private static UdonSharpBehaviour[] BuildPortalRoom(Interior info, QuestManager qm, PlayerState playerState)
        {
            var behaviours = new List<UdonSharpBehaviour>();
            Transform root = info.root;
            var hall = Child(root, "PortalRoom", PortalRoomCenter);
            var stone = PMat("PortalStone", new Color(0.42f, 0.4f, 0.5f), 3f);
            var floor = PMat("PortalFloor", new Color(0.3f, 0.28f, 0.38f), 6f);
            var gold = PMat("Gold", new Color(1f, 0.78f, 0.2f), 1f, new Color(0.25f, 0.18f, 0f));
            var dark = PMat("PortalDark", new Color(0.04f, 0.03f, 0.07f), 1f);
            var veil = PMat("PortalVeil", new Color(0.7f, 0.6f, 1f), 1f, new Color(0.6f, 0.5f, 1f) * 1.6f);
            var door = PMat("Door", new Color(0.35f, 0.2f, 0.12f), 2f);
            const float h = 8f;

            // ---- the round chamber
            Shape(hall, "Floor", CylMesh(), new Vector3(0, -0.25f, 0), new Vector3(PortalRoomRadius * 2.2f, 0.25f, PortalRoomRadius * 2.2f), floor, true, Quaternion.identity);
            Shape(hall, "Ceiling", CylMesh(), new Vector3(0, h + 0.25f, 0), new Vector3(PortalRoomRadius * 2.2f, 0.25f, PortalRoomRadius * 2.2f), stone, true, Quaternion.identity);
            Shape(hall, "Inlay", CylMesh(), new Vector3(0, 0.02f, 0), new Vector3(6f, 0.02f, 6f), gold, false, Quaternion.identity);
            const int segs = 20;
            for (int i = 0; i < segs; i++)
            {
                float a = (i + 0.5f) * 360f / segs;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                float chord = 2f * PortalRoomRadius * Mathf.Sin(Mathf.PI / segs) + 0.3f;
                Block(hall, "Wall", dir * PortalRoomRadius + new Vector3(0, h / 2f, 0), new Vector3(chord, h, 0.6f), stone, Quaternion.LookRotation(dir));
            }
            PointLight(hall, new Vector3(0, h - 1.5f, 0), new Color(0.85f, 0.8f, 1f), 18f, 1.3f);
            Shape(hall, "Star", GemMesh(), new Vector3(0, h - 1.2f, 0), new Vector3(1f, 1.4f, 1f), gold, false, Quaternion.identity);

            // ---- five arches round the north half, facing the middle
            string[] names = { "Starfall Isle", "World 2", "World 3", "World 4", "World 5" };
            float[] angles = { 0f, -40f, 40f, -80f, 80f };
            for (int i = 0; i < 5; i++)
            {
                Vector3 dir = Quaternion.Euler(0, angles[i], 0) * Vector3.forward;
                Quaternion face = Quaternion.LookRotation(-dir);
                Vector3 c = dir * (PortalRoomRadius - 1.2f);
                Transform arch = Child(hall, "Arch_" + i, c);
                arch.localRotation = face;
                Block(arch, "PillarL", new Vector3(-1.5f, 2.2f, 0), new Vector3(0.6f, 4.4f, 0.8f), stone, Quaternion.identity);
                Block(arch, "PillarR", new Vector3(1.5f, 2.2f, 0), new Vector3(0.6f, 4.4f, 0.8f), stone, Quaternion.identity);
                Block(arch, "Lintel", new Vector3(0, 4.7f, 0), new Vector3(3.8f, 0.7f, 0.9f), stone, Quaternion.identity);
                Shape(arch, "Keystone", GemMesh(), new Vector3(0, 5.3f, 0.45f), new Vector3(0.35f, 0.5f, 0.2f), gold, false, Quaternion.identity);
                var shut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shut.name = "DarkVeil";
                shut.transform.SetParent(arch, false);
                shut.transform.localPosition = new Vector3(0, 2.2f, 0.05f);
                shut.transform.localScale = new Vector3(2.4f, 4.4f, 0.1f);
                shut.GetComponent<Renderer>().sharedMaterial = dark;
                Object.DestroyImmediate(shut.GetComponent<Collider>());
                var lit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lit.name = "GlowingVeil";
                lit.transform.SetParent(arch, false);
                lit.transform.localPosition = new Vector3(0, 2.2f, 0.05f);
                lit.transform.localScale = new Vector3(2.4f, 4.4f, 0.1f);
                lit.GetComponent<Renderer>().sharedMaterial = veil;
                Object.DestroyImmediate(lit.GetComponent<Collider>());
                // Canvas +Z = the way the viewer looks: towards the arch (outwards).
                Text label = WorldText(arch, "Label", new Vector3(0, 6.3f, 0.3f), Quaternion.Euler(0, 180f, 0), new Vector2(900, 200), 0.004f, "", 52);
                label.color = new Color(1f, 0.86f, 0.5f);
                if (i == 0)
                {
                    label.text = names[i] + "\n(you are here)";
                    shut.SetActive(false);
                    continue;
                }
                if (i == 1)
                {
                    // Click the arch: it says it's dark, or that the way is open.
                    var hit = arch.gameObject.AddComponent<BoxCollider>();
                    hit.center = new Vector3(0, 2.2f, 0);
                    hit.size = new Vector3(2.6f, 4.4f, 0.4f);
                    var gate = UdonSharpUndo.AddComponent<QuestGate>(arch.gameObject);
                    gate.questManager = qm;
                    gate.playerState = playerState;
                    gate.requiredQuest = 16;
                    gate.label = label;
                    gate.openText = "World 2\n(the way is open: coming soon)";
                    gate.lockedText = "A dark arch";
                    gate.openVisual = lit;
                    gate.closedVisual = shut;
                    behaviours.Add(gate);
                    lit.SetActive(false);
                    label.text = gate.lockedText;
                    continue;
                }
                label.text = "A dark arch";
                lit.SetActive(false);
            }

            // ---- doors: the study's west wall <-> the chamber's south side
            var hallSpawn = Child(hall, "PortalRoomSpawn", new Vector3(0, 0.05f, -PortalRoomRadius + 2.5f));
            var studySpot = Child(root, "PortalRoomReturn", new Vector3(-5.4f, 0.05f, -8f));
            studySpot.localRotation = Quaternion.Euler(0, 90f, 0);
            behaviours.Add(TeleportDoor(root, "PortalRoomDoor", new Vector3(-6.85f, 1.8f, -8f), new Vector3(0.2f, 3.6f, 2.2f), door, hallSpawn,
                new Vector3(-6.6f, 4.1f, -8f), Quaternion.Euler(0, -90f, 0), "To the Portal Room (click door)"));
            behaviours.Add(TeleportDoor(hall, "StudyDoor", new Vector3(0, 1.8f, -PortalRoomRadius + 0.45f), new Vector3(2.4f, 3.6f, 0.2f), door, studySpot,
                new Vector3(0, 4.3f, -PortalRoomRadius + 0.6f), Quaternion.Euler(0, 180f, 0), "To the Headmaster's study (click door)"));
            return behaviours.ToArray();
        }

        /// <summary>A clickable door that moves you to 'to' (TeleportHelper), with a sign over it.</summary>
        private static TeleportHelper TeleportDoor(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Transform to,
            Vector3 signPos, Quaternion signRot, string sign)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = name;
            d.transform.SetParent(parent, false);
            d.transform.localPosition = pos;
            d.transform.localScale = size;
            d.GetComponent<Renderer>().sharedMaterial = mat;
            var t = UdonSharpUndo.AddComponent<TeleportHelper>(d);
            t.defaultTarget = to;
            WorldText(parent, name + "Sign", signPos, signRot, new Vector2(1100, 160), 0.004f, sign, 46).color = new Color(1f, 0.86f, 0.5f);
            return t;
        }
    }
}
