using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// World 1 streets (see the World 1 plan). Each street is its own set far from the castle, reached by a door, and its
    /// root starts switched off: doors switch it on for the local player on the way in and off on the way out, so nothing
    /// of a street renders for players who aren't in it.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Vector3 LanternRowOrigin = new Vector3(0f, 600f, 0f);

        /// <summary>Lantern Row (Death, Lv 1-3): the tutorial street. Door on the islet past the castle's south bridge.</summary>
        private static List<UdonSharpBehaviour> BuildLanternRow(Transform systems, Transform hubRoot, QuestManager qm, CutscenePlayer player,
            CardDatabase cardDb, ItemDatabase itemDb, PlayerState playerState, List<BattleClient> encounters)
        {
            var list = new List<UdonSharpBehaviour>();
            var root = new GameObject("Street_LanternRow").transform;
            root.position = LanternRowOrigin;

            Material cobble = Mat("LR_Cobble", new Color(0.24f, 0.22f, 0.27f));
            Material wall = Mat("LR_Wall", new Color(0.33f, 0.27f, 0.36f));
            Material roof = Mat("LR_Roof", new Color(0.16f, 0.1f, 0.2f));
            Material wood = Mat("LR_Wood", new Color(0.3f, 0.2f, 0.14f));
            Material glow = PMat("LR_Glow", new Color(0.5f, 1f, 0.6f), 1f, new Color(0.4f, 1f, 0.5f));
            Material dead = Mat("LR_DeadLamp", new Color(0.12f, 0.12f, 0.14f));
            Material door = PMat("LR_Door", new Color(0.35f, 0.2f, 0.12f), 2f);

            // ---- the street: a long winding cobbled path through a walled, overgrown old quarter, gate (z 0) -> Snuffer's Tower (z ~330)
            Material moss = Mat("LR_Moss", new Color(0.13f, 0.16f, 0.13f));
            Vector3[] path = LanternRowPath;
            Vector3 min = path[0], max = path[0];
            foreach (var w in path) { min = Vector3.Min(min, w); max = Vector3.Max(max, w); }
            min -= new Vector3(40, 0, 14); max += new Vector3(40, 0, 20);
            Vector3 mid = (min + max) / 2f, size = max - min;
            Block(root, "Ground", new Vector3(mid.x, -0.25f, mid.z), new Vector3(size.x, 0.5f, size.z), moss);
            Block(root, "WallW", new Vector3(min.x, 4, mid.z), new Vector3(1, 8, size.z), wall);
            Block(root, "WallE", new Vector3(max.x, 4, mid.z), new Vector3(1, 8, size.z), wall);
            Block(root, "WallS", new Vector3(mid.x, 4, min.z), new Vector3(size.x, 8, 1), wall);
            Block(root, "WallN", new Vector3(mid.x, 4, max.z), new Vector3(size.x, 8, 1), wall);
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector3 a = path[i], b = path[i + 1], d = b - a;
                Block(root, "Path" + i, (a + b) / 2f + new Vector3(0, 0.03f, 0), new Vector3(7f, 0.06f, d.magnitude + 7f), cobble, Quaternion.LookRotation(d));
            }
            foreach (var c in LanternRowCircles)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Plaza";
                disc.transform.SetParent(root, false);
                disc.transform.localPosition = c + new Vector3(0, 0.02f, 0);
                disc.transform.localScale = new Vector3(DuelCircleRadius * 2.6f, 0.03f, DuelCircleRadius * 2.6f);
                disc.GetComponent<Renderer>().sharedMaterial = cobble;
                Object.DestroyImmediate(disc.GetComponent<Collider>());
            }

            // leaning houses lining both sides of the path, kept clear of the path and the fight plazas
            int house = 0;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector3 a = path[i], b = path[i + 1], d = b - a;
                Vector3 side = Vector3.Cross(Vector3.up, d.normalized);
                for (float t = 6f; t < d.magnitude; t += 12f)
                    foreach (float sgn in new[] { -1f, 1f })
                    {
                        Vector3 p = a + d.normalized * t + side * sgn * 11f;
                        if (DistToPath(p, path) < 9f || NearCircle(p, DuelCircleRadius + 6f)) continue;
                        float h = 6f + (house * 7) % 4;
                        var rot = Quaternion.LookRotation(d) * Quaternion.Euler(0, 0, sgn * (2f + house % 3));
                        Block(root, "House", p + new Vector3(0, h / 2f, 0), new Vector3(9, h, 8), wall, rot);
                        Block(root, "Roof", p + new Vector3(0, h + 0.6f, 0), new Vector3(10, 1.2f, 9), roof, rot);
                        house++;
                    }
            }

            // ---- Snuffer's Tower at the far end
            Vector3 end = path[path.Length - 1];
            Block(root, "TowerBack", end + new Vector3(0, 9, 12), new Vector3(14, 18, 4), roof);
            WorldText(root, "TowerSign", end + new Vector3(0, 12f, 9.8f), Quaternion.identity, new Vector2(1400, 200), 0.006f, "The Snuffer's Tower", 80).color = new Color(0.6f, 1f, 0.65f);

            // ---- doors: islet (hub) <-> street gate
            Transform streetSpawn = Child(root, "StreetSpawn", new Vector3(0, 0.05f, min.z + 4f));
            Transform hubSpot = Child(hubRoot, "LanternRowReturn", new Vector3(0, 0.1f, -72f));
            hubSpot.rotation = Quaternion.Euler(0, 180f, 0);
            var inDoor = MakeDoor(hubRoot, "LanternRowDoor", new Vector3(0, 2.5f, -81f), door, "Lantern Row\n(click door)  Lv 1-3");
            var toStreet = UdonSharpUndo.AddComponent<TeleportHelper>(inDoor);
            toStreet.defaultTarget = streetSpawn;
            toStreet.showZones = new[] { root.gameObject };
            list.Add(toStreet);
            var outDoor = MakeDoor(root, "GateDoor", new Vector3(0, 2.5f, min.z + 0.6f), door, "Starfall Castle\n(click door)");
            outDoor.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var toHub = UdonSharpUndo.AddComponent<TeleportHelper>(outDoor);
            toHub.defaultTarget = hubSpot;
            toHub.hideZones = new[] { root.gameObject };
            list.Add(toHub);

            // ---- Watchwoman Tilda at the gate
            Transform tilda = Wizard(root, "WatchwomanTilda", new Vector3(4f, 0, 2f), 200f, new Color(0.25f, 0.3f, 0.2f), new Color(0.15f, 0.15f, 0.12f), false, "Watchwoman Tilda");
            Vector3 face = tilda.localPosition + new Vector3(0, 2f, 0);
            var shots = new[] { Shot(root, "TildaShotWide", new Vector3(0.5f, 2.6f, 7.5f), face), Shot(root, "TildaShotClose", new Vector3(2.8f, 2.05f, 4.6f), face) };
            var tildaCs = MakeCutscene(Child(systems, "StreetCutscenes", Vector3.zero), "Tilda", "Watchwoman Tilda", TildaLines, "lr_tilda", shots);
            tildaCs.onFinished = qm;
            tildaCs.onFinishedEvent = nameof(QuestManager.OnStreetNpcFinished);
            qm.streetNpcCutscenes = new[] { tildaCs };
            list.Add(tildaCs);
            list.Add(Relay(tilda.gameObject, qm, nameof(QuestManager.TalkTilda)));

            // ---- encounters: two wandering lanes and the boss circle in the tower
            var wisps = MakeEncounter(systems, "LR_Wisps", cardDb, itemDb, playerState, streetSpawn, new[] { 5, 5 }, true, encounters);
            var snatchers = MakeEncounter(systems, "LR_Snatchers", cardDb, itemDb, playerState, streetSpawn, new[] { 6, 5, 6 }, true, encounters);
            var snatchers2 = MakeEncounter(systems, "LR_Snatchers2", cardDb, itemDb, playerState, streetSpawn, new[] { 6, 6 }, true, encounters);
            var boss = MakeEncounter(systems, "LR_Snuffer", cardDb, itemDb, playerState, streetSpawn, new[] { 7, 5, 5 }, true, encounters);
            var circles = LanternRowCircles;
            BuildEncounter(root, circles[0], "LR_Wisps", wisps, 3);
            BuildEncounter(root, circles[1], "LR_Snatchers", snatchers, 3);
            BuildEncounter(root, circles[2], "LR_Snatchers2", snatchers2, 3);
            BuildEncounter(root, circles[3], "LR_Snuffer", boss, 1);
            foreach (var ec in new[] { wisps, snatchers, snatchers2, boss }) { ec.questManager = qm; ec.manager.respawnSeconds = 20; }
            boss.manager.respawnSeconds = 30;

            // ---- quest targets: three dead street lamps to relight, and the tower doorstep
            Transform[] lamps = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = LanternRowLamps[i];
                Block(root, "LampPost" + i, p + new Vector3(0, 2f, 0), new Vector3(0.25f, 4f, 0.25f), wood);
                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "DeadLamp" + i;
                head.transform.SetParent(root, false);
                head.transform.localPosition = p + new Vector3(0, 4.3f, 0);
                head.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                head.GetComponent<Renderer>().sharedMaterial = dead;
                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "Flame";
                flame.transform.SetParent(head.transform, false);
                flame.transform.localScale = Vector3.one * 0.9f;
                flame.GetComponent<Renderer>().sharedMaterial = glow;
                Object.DestroyImmediate(flame.GetComponent<Collider>());
                flame.SetActive(false);
                var qt = UdonSharpUndo.AddComponent<QuestTarget>(head);
                qt.questManager = qm;
                qt.targetId = 101;
                qt.usedVisual = flame;
                list.Add(qt);
                lamps[i] = head.transform;
            }
            var step = new GameObject("TowerDoorstep");
            step.transform.SetParent(root, false);
            step.transform.localPosition = end + new Vector3(0, 1.5f, -38f);
            var box = step.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(30f, 3f, 3f);
            var stepTarget = UdonSharpUndo.AddComponent<QuestTarget>(step);
            stepTarget.questManager = qm;
            stepTarget.targetId = 102;
            stepTarget.isTrigger = true;
            list.Add(stepTarget);

            // ---- quests (ids 2-5), chained after The Prophecy
            const int T = QuestManager.STEP_TALK, D = QuestManager.STEP_DEFEAT, C = QuestManager.STEP_COLLECT,
                I = QuestManager.STEP_INTERACT, E = QuestManager.STEP_EXPLORE, B = QuestManager.STEP_BOSS, NT = QuestManager.NPC_TILDA;
            int q2 = AddQuest(qm, "Trouble on Lantern Row", "The Headmaster has heard that the lamps of Lantern Row are going dark one by one. "
                + "Cross the south bridge and find Watchwoman Tilda at the street gate.", 40, 20, 0, 0, QuestManager.QUEST_PROPHECY, 1);
            SetStep(qm, q2, 0, T, NT, 1, "Talk to Watchwoman Tilda on Lantern Row (door past the south bridge)", "Watchwoman Tilda", tilda);
            int q3 = AddQuest(qm, "Lights Out", "Gloom Wisps have been snuffing out the street lamps. Thin their numbers and relight the lamps.", 80, 30, 0, 0, q2, 1);
            SetStep(qm, q3, 0, D, 5, 3, "Defeat Gloom Wisps", "Gloom Wisps", wisps.arenaCenter);
            SetStep(qm, q3, 1, I, 101, 3, "Relight the dead street lamps", "Dead street lamp", lamps[0]);
            SetStep(qm, q3, 2, T, NT, 1, "Return to Watchwoman Tilda", "Watchwoman Tilda", tilda);
            int q4 = AddQuest(qm, "Stolen Light", "Lantern Snatchers are carrying the stolen lamp-flames toward the tower. Take them back.", 120, 40, 0, 0, q3, 2);
            SetStep(qm, q4, 0, C, 6, 4, "Recover stolen lamp-flames from Lantern Snatchers", "Lantern Snatchers", snatchers.arenaCenter);
            SetStep(qm, q4, 1, T, NT, 1, "Bring the lamp-flames to Watchwoman Tilda", "Watchwoman Tilda", tilda);
            int q5 = AddQuest(qm, "The Snuffer's Tower", "Every stolen flame leads to the tower at the end of the street, where Ashwick the Snuffer "
                + "guards one of Vesper's Hollow Lanterns. Defeat him and destroy it.", 250, 100, 26, 0, q4, 3);
            SetStep(qm, q5, 0, E, 102, 1, "Reach the Snuffer's Tower at the end of Lantern Row", "Snuffer's Tower", step.transform);
            SetStep(qm, q5, 1, B, 7, 1, "Defeat Ashwick the Snuffer", "Ashwick the Snuffer", boss.arenaCenter);
            SetStep(qm, q5, 2, T, NT, 1, "Tell Watchwoman Tilda the lantern is destroyed", "Watchwoman Tilda", tilda);

            root.gameObject.SetActive(false);
            return list;
        }

        // Winding path waypoints (street-local XZ); the last point is the tower.
        private static readonly Vector3[] LanternRowPath =
        {
            new Vector3(0, 0, -4), new Vector3(0, 0, 30), new Vector3(25, 0, 60), new Vector3(25, 0, 95), new Vector3(-10, 0, 130),
            new Vector3(-30, 0, 160), new Vector3(-30, 0, 190), new Vector3(0, 0, 215), new Vector3(30, 0, 240), new Vector3(30, 0, 266),
            new Vector3(0, 0, 290), new Vector3(0, 0, 330),
        };
        // Fight circles sit on the path: Wisps, Snatchers, Snatchers, Ashwick (in front of the tower).
        private static readonly Vector3[] LanternRowCircles = { new Vector3(25, 0, 78), new Vector3(-30, 0, 175), new Vector3(30, 0, 253), new Vector3(0, 0, 314) };
        private static readonly Vector3[] LanternRowLamps = { new Vector3(5, 0, 18), new Vector3(-2, 0, 108), new Vector3(-5, 0, 202) };

        private static float DistToPath(Vector3 p, Vector3[] path)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector3 a = path[i], ab = path[i + 1] - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }

        private static bool NearCircle(Vector3 p, float r)
        {
            foreach (var c in LanternRowCircles) if (Vector3.Distance(p, c) < r) return true;
            return false;
        }

        private static readonly string[] TildaLines =
        {
            "Halt! Oh, a wizard from the castle. Thank the stars.",
            "I'm Tilda, I keep watch over Lantern Row. Or I did, until the lamps started going out.",
            "Gloom Wisps snuff the flames, and hooded Snatchers carry them off to the old tower at the end of the street.",
            "Something in that tower is drinking the light. Help me take the street back, one lamp at a time.",
        };

        private static GameObject MakeDoor(Transform parent, string name, Vector3 localPos, Material mat, string label)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = name;
            d.transform.SetParent(parent, false);
            d.transform.localPosition = localPos;
            d.transform.localScale = new Vector3(3f, 5f, 0.3f);
            d.GetComponent<Renderer>().sharedMaterial = mat;
            WorldText(d.transform.parent, name + "Label", localPos + new Vector3(0, 3.3f, 0), Quaternion.identity, new Vector2(1000, 220), 0.004f, label, 64).color = new Color(1f, 0.85f, 0.4f);
            return d;
        }

        /// <summary>Appends a quest to the QuestManager tables and returns its id. Steps are filled with SetStep.</summary>
        private static int AddQuest(QuestManager qm, string title, string description, int xp, int gold, int item, int spell, int prereq, int minLevel)
        {
            int q = qm.questTitles.Length;
            int ms = QuestManager.MAX_STEPS;
            qm.questTitles = Grow(qm.questTitles, title);
            qm.questDescriptions = Grow(qm.questDescriptions, description);
            qm.questRewards = Grow(qm.questRewards, xp + " XP, " + gold + " gold" + (item > 0 ? ", a reward item" : ""));
            qm.questXp = Grow(qm.questXp, xp);
            qm.questGold = Grow(qm.questGold, gold);
            qm.questItems = Grow(qm.questItems, item);
            qm.questSpells = Grow(qm.questSpells, spell);
            qm.questPrereqs = Grow(qm.questPrereqs, prereq);
            qm.questMinLevels = Grow(qm.questMinLevels, minLevel);
            qm.questAutoStart = Grow(qm.questAutoStart, 1);
            qm.questGivers = Grow(qm.questGivers, QuestManager.NPC_TILDA);
            qm.questStepCounts = Grow(qm.questStepCounts, 0);
            for (int k = 0; k < ms; k++)
            {
                qm.stepTypes = Grow(qm.stepTypes, 0);
                qm.stepParams = Grow(qm.stepParams, 0);
                qm.stepCounts = Grow(qm.stepCounts, 1);
                qm.stepTexts = Grow(qm.stepTexts, "");
                qm.stepTargetNames = Grow(qm.stepTargetNames, "");
                qm.stepTargets = Grow(qm.stepTargets, (Transform)null);
            }
            return q;
        }

        private static T[] Grow<T>(T[] a, T v)
        {
            var r = new T[(a == null ? 0 : a.Length) + 1];
            if (a != null) System.Array.Copy(a, r, a.Length);
            r[r.Length - 1] = v;
            return r;
        }
    }
}
