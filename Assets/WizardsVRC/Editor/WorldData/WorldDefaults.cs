using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// The starting world data (the game's original monsters, a maze-style Lantern Row and its questline). Only used to
    /// create the assets in Assets/WizardsVRC/WorldData the first time; after that, the assets are what you edit.
    /// </summary>
    public static class WorldDefaults
    {
        public const string StarterMap =
            "###########\n" +
            "#^,,...,,^#\n" +
            "#,...1...,#\n" +
            "#,.aaaaa.,#\n" +
            "#..a...a..#\n" +
            "#..a...a..#\n" +
            "#..aaaaa..#\n" +
            "#....S....#\n" +
            "###########\n";

        // ------------------------------------------------------------------ monsters

        public static void FillMonsters(MonsterCatalog cat)
        {
            cat.monsters = new List<MonsterEntry>
            {
                M(1, "Gloomfang the Warden", School.Death, 5, 900, true, 450, new[] { 148, 150, 152, 154 }, 120, 40, 80, 25, 60, 1.7f, new Color(0.25f, 0.2f, 0.3f),
                  "Training Yard boss."),
                M(2, "Bone Thrall", School.Myth, 3, 320, false, 0, new[] { 160, 161, 162 }, 45, 10, 25, 10, 25, 1f, new Color(0.8f, 0.78f, 0.7f), "Training Yard."),
                M(3, "Marsh Sprite", School.Life, 2, 240, false, 0, new[] { 136, 137 }, 35, 8, 18, 11, 25, 0.8f, new Color(0.4f, 0.75f, 0.45f), "Training Yard lane; heals its friends."),
                M(4, "Courtyard Rat", School.Storm, 1, 180, false, 0, new[] { 124, 125 }, 25, 5, 12, 10, 20, 0.7f, new Color(0.5f, 0.45f, 0.4f), "Training Yard lane."),
                M(5, "Gloom Wisp", School.Death, 1, 180, false, 0, new[] { 148 }, 30, 5, 12, 10, 20, 0.6f, new Color(0.35f, 0.4f, 0.55f), "Lantern Row. Hovers in pairs."),
                M(6, "Lantern Snatcher", School.Myth, 2, 260, false, 0, new[] { 160, 161 }, 40, 8, 18, 11, 25, 0.9f, new Color(0.45f, 0.35f, 0.25f),
                  "Lantern Row. Goblin-like thieves carrying stolen lamp-flames."),
                M(7, "Ashwick the Snuffer", School.Death, 4, 900, true, 450, new[] { 148, 149, 150, 154 }, 150, 50, 90, 61, 40, 1.6f, new Color(0.2f, 0.22f, 0.3f),
                  "Lantern Row boss in the Snuffer's Tower. Drops the Scholar's Deck."),
            };
        }

        private static MonsterEntry M(int id, string name, School school, int level, int hp, bool boss, int hpExtra, int[] spells, int xp, int gMin, int gMax,
            int drop, int dropPct, float scale, Color color, string notes)
        {
            return new MonsterEntry
            {
                id = id, name = name, school = school, level = level, health = hp, isBoss = boss, healthPerExtraWizard = hpExtra,
                spells = new List<int>(spells), xp = xp, goldMin = gMin, goldMax = gMax, dropItem = drop, dropChance = dropPct,
                bodyScale = scale, bodyColor = color, notes = notes,
            };
        }

        // ------------------------------------------------------------------ Lantern Row

        /// <summary>
        /// Lantern Row as a Wizard101-style street: wide cobbled roads, crossroads, loops round the house blocks, dead-end
        /// courtyards, ring plazas with patrols circling them, and the Snuffer's Tower courtyard at the far (north) end.
        /// </summary>
        public static void FillLanternRow(ZoneDefinition z)
        {
            z.zoneKey = "LanternRow";
            z.displayName = "Lantern Row";
            z.levelRange = "Lv 1-3";
            z.height = 600f;
            z.cellSize = 8f;
            z.map =
            "#################################\n" +
            "###########^,,,,,,,,,^###########\n" +
            "###########,.........,###########\n" +
            "###########,.........,###########\n" +
            "###########,...ddd...,###########\n" +
            "###########,...d4d...,###########\n" +
            "###########,...ddd...,###########\n" +
            "###########,.........,###########\n" +
            "###########==,.....,==###########\n" +
            "#############=..X..=#############\n" +
            "######.........................##\n" +
            "######.###.###.###.###.#######.##\n" +
            "######.###.###.###.###.#######.##\n" +
            "##.......#.....ccccccc.....###.##\n" +
            "##.#####.#.###.c.3.#.c.###.###.##\n" +
            "##.#^,G#.#.###.c...#.c.###.....##\n" +
            "##.#,,,#.#.###.ccccccc.###.###.##\n" +
            "##.##.##...###.........###.###.##\n" +
            "##....########.#####.#####.###.##\n" +
            "#######.......L#####...........##\n" +
            "#######.#####.######.#########.##\n" +
            "#.......#####.######.#########.##\n" +
            "#.#####.#####........#####,,,#.##\n" +
            "#.#,,C#.#####.bbbbbbb.####,K,#.##\n" +
            "#.#,,,#.......b.....b.####.,,#.##\n" +
            "#.##.##.#####.b..2..b.####.###.##\n" +
            "#....##.#####.b.....b.#....###.##\n" +
            "######..#####.bbbbbbb.#.#######.#\n" +
            "######.######.........#.......L.#\n" +
            "######.##############.#######.#.#\n" +
            "##.......#####...............##.#\n" +
            "##.#####.#####.###.#####.###.##.#\n" +
            "##.#^G,#.#####.###.#####.###....#\n" +
            "##.#,,J#.......###.#####.###.####\n" +
            "##.#,,,#.#####.###.......###.####\n" +
            "##.##.##.#####.###.#####.###.####\n" +
            "##.......#####.eee.#####.....####\n" +
            "##########.....e5e.##############\n" +
            "##########.....eee.##############\n" +
            "##########...aaaaa.##############\n" +
            "######.......a...a.##############\n" +
            "######..L....a.1.a.##############\n" +
            "######..T....a...a.##############\n" +
            "######.......aaaaa.##############\n" +
            "##########...S...################\n" +
            "#################################\n";
            z.groundColor = new Color(0.13f, 0.16f, 0.13f);
            z.roadColor = new Color(0.24f, 0.22f, 0.27f);
            z.grassColor = new Color(0.15f, 0.2f, 0.15f);
            z.buildingColor = new Color(0.33f, 0.27f, 0.36f);
            z.roofColor = new Color(0.16f, 0.1f, 0.2f);
            z.lowWallColor = new Color(0.28f, 0.25f, 0.3f);
            z.treeColor = new Color(0.12f, 0.2f, 0.14f);
            z.accentColor = new Color(1f, 0.65f, 0.3f);
            z.buildingHeight = new Vector2(7f, 13f);
            z.lampDensity = 0.3f;
            z.hubDoorPosition = new Vector3(0f, 2.5f, -81f);
            z.hubDoorYaw = 0f;
            z.hubReturnPosition = new Vector3(0f, 0.1f, -72f);
            z.hubReturnYaw = 180f;

            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "T", key = "Tilda", displayName = "Watchwoman Tilda", robeColor = new Color(0.25f, 0.3f, 0.2f), hatColor = new Color(0.15f, 0.15f, 0.12f),
                    facing = -1f,
                    defaultDialogue = new[]
                    {
                        "Keep your wand handy out there. The Wisps drift in from the alleys after dark.",
                    },
                },
                new ZoneNpc
                {
                    symbol = "K", key = "Marla", displayName = "Marla the Candlemaker", robeColor = new Color(0.55f, 0.35f, 0.2f), hatColor = new Color(0.35f, 0.2f, 0.12f),
                    facing = -1f,
                    defaultDialogue = new[] { "Candles, tapers, lantern wicks! Not much trade since the lamps went dark, mind you." },
                },
                new ZoneNpc
                {
                    symbol = "J", key = "Pip", displayName = "Pip", robeColor = new Color(0.3f, 0.45f, 0.65f), hatColor = new Color(0.2f, 0.3f, 0.45f),
                    facing = -1f,
                    defaultDialogue = new[] { "When I grow up I'm going to be a wizard too. A Storm wizard. The loud kind." },
                },
            };

            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "L", key = "Lamp", kind = ZonePointKind.Interact, look = ZonePointLook.StreetLamp, label = "Dead street lamp" },
                new ZonePoint { symbol = "X", key = "TowerDoorstep", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 3f },
                new ZonePoint { symbol = "C", key = "Whiskers", kind = ZonePointKind.Explore, look = ZonePointLook.GlowingItem, label = "Whiskers the cat", triggerSizeCells = 1f },
                new ZonePoint { symbol = "G", key = "Cog", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Brass cog" },
            };

            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "Wisps", roster = new List<int> { 5, 5 }, wanderers = 2, patrolSpeed = 1.3f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "2", key = "Snatchers", roster = new List<int> { 6, 5, 6 }, wanderers = 3, patrolSpeed = 1.5f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "3", key = "SnatcherGang", roster = new List<int> { 6, 6 }, wanderers = 2, patrolSpeed = 1.5f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "4", key = "Snuffer", roster = new List<int> { 7, 5, 5 }, wanderers = 1, patrolSpeed = 0.8f, respawnSeconds = 30, scaleWithParty = true },
                new ZoneEncounter { ringSymbol = "5", key = "WispPack", roster = new List<int> { 5, 5, 5 }, wanderers = 2, patrolSpeed = 1.2f, respawnSeconds = 20 },
            };
        }

        public static void FillLanternRowQuests(Questline ql)
        {
            // Quest 2's dialogue reuses Tilda's recorded intro lines (Audio/Voice/lr_tilda_00-03.wav).
            ql.includeInBuild = true;
            ql.quests = new List<QuestEntry>
            {
                new QuestEntry
                {
                    saveId = 2, title = "Trouble on Lantern Row", prerequisite = QuestManager.QUEST_PROPHECY, minLevel = 1, xp = 40, gold = 20,
                    description = "The Headmaster has heard that the lamps of Lantern Row are going dark one by one. Cross the south bridge and find Watchwoman Tilda at the street gate.",
                    steps = new List<QuestStepEntry>
                    {
                        Voiced(Talk("Tilda", "Talk to Watchwoman Tilda on Lantern Row (door past the south bridge)", "Watchwoman Tilda",
                            "Halt! Oh, a wizard from the castle. Thank the stars.",
                            "I'm Tilda, I keep watch over Lantern Row. Or I did, until the lamps started going out.",
                            "Gloom Wisps snuff the flames, and hooded Snatchers carry them off to the old tower at the end of the street.",
                            "Something in that tower is drinking the light. Help me take the street back, one lamp at a time."), "lr_tilda"),
                    },
                },
                new QuestEntry
                {
                    saveId = 3, title = "Lights Out", prerequisite = 2, minLevel = 1, xp = 80, gold = 30,
                    description = "Gloom Wisps have been snuffing out the street lamps. Thin their numbers and relight the lamps.",
                    steps = new List<QuestStepEntry>
                    {
                        Fight(QuestStepKind.Defeat, 5, 3, "Defeat Gloom Wisps", "Gloom Wisps"),
                        Point(QuestStepKind.Interact, "Lamp", 3, "Relight the dead street lamps", "Dead street lamp"),
                        Talk("Tilda", "Return to Watchwoman Tilda", "Watchwoman Tilda",
                            "Look at that! Three lamps burning again. The Wisps won't like it one bit.",
                            "But the flames that were stolen are still missing. The Snatchers took them somewhere."),
                    },
                },
                new QuestEntry
                {
                    saveId = 4, title = "Stolen Light", prerequisite = 3, minLevel = 2, xp = 120, gold = 40,
                    description = "Lantern Snatchers are carrying the stolen lamp-flames toward the tower. Take them back.",
                    steps = new List<QuestStepEntry>
                    {
                        Fight(QuestStepKind.Collect, 6, 4, "Recover stolen lamp-flames from Lantern Snatchers", "Lantern Snatchers"),
                        Talk("Tilda", "Bring the lamp-flames to Watchwoman Tilda", "Watchwoman Tilda",
                            "Four flames, still warm. Every one of them was headed north, toward the Snuffer's Tower.",
                            "Ashwick the Snuffer lives up there. If something is drinking the light, it's in that tower."),
                    },
                },
                new QuestEntry
                {
                    saveId = 5, title = "The Snuffer's Tower", prerequisite = 4, minLevel = 3, xp = 250, gold = 100, rewardItem = 26,
                    description = "Every stolen flame leads to the tower at the end of the street, where Ashwick the Snuffer guards one of Vesper's Hollow Lanterns. Defeat him and destroy it.",
                    steps = new List<QuestStepEntry>
                    {
                        Point(QuestStepKind.Explore, "TowerDoorstep", 1, "Reach the Snuffer's Tower at the north end of Lantern Row", "Snuffer's Tower"),
                        Fight(QuestStepKind.Boss, 7, 1, "Defeat Ashwick the Snuffer", "Ashwick the Snuffer"),
                        Talk("Tilda", "Tell Watchwoman Tilda the lantern is destroyed", "Watchwoman Tilda",
                            "You beat Ashwick? And the Hollow Lantern... shattered? The whole street is brighter already.",
                            "Take this, with my thanks. And tell the Headmaster: there are more of those lanterns out there."),
                    },
                },
                new QuestEntry
                {
                    saveId = 6, title = "Whiskers Is Missing", startsAutomatically = false, giverNpc = "Marla", prerequisite = 2, minLevel = 1, xp = 60, gold = 25,
                    description = "Marla the Candlemaker's cat ran off when the lamps went out. She thinks he's hiding in one of the courtyards to the west.",
                    offerDialogue = new[]
                    {
                        "Oh, a wizard! Have you seen a grey cat? Whiskers bolted the night the lamps died.",
                        "He likes the quiet courtyards off the west alleys. Would you look for him?",
                    },
                    steps = new List<QuestStepEntry>
                    {
                        Point(QuestStepKind.Explore, "Whiskers", 1, "Find Whiskers in the west courtyards", "Whiskers the cat"),
                        Talk("Marla", "Tell Marla that Whiskers is safe", "Marla the Candlemaker",
                            "You found him? Curled up asleep in the old courtyard, of course he was.",
                            "Here, for your trouble. Mind the Wisps on your way back."),
                    },
                },
                new QuestEntry
                {
                    saveId = 7, title = "Pip's Broken Toy", startsAutomatically = false, giverNpc = "Pip", prerequisite = 2, minLevel = 1, xp = 60, gold = 15, rewardItem = 10,
                    description = "Pip's clockwork owl lost two brass cogs when a Snatcher knocked it out of his hands. They rolled off into the courtyards.",
                    offerDialogue = new[]
                    {
                        "A Snatcher bumped me and my clockwork owl broke! Two cogs fell out and rolled away.",
                        "One went into the courtyard over there, and one went way up north. Can you find them?",
                    },
                    steps = new List<QuestStepEntry>
                    {
                        Point(QuestStepKind.Interact, "Cog", 2, "Find Pip's brass cogs", "Brass cog"),
                        Talk("Pip", "Bring the cogs back to Pip", "Pip",
                            "My cogs! Look, she's flapping again!",
                            "Mum gave me this potion for emergencies. I think you need it more than me."),
                    },
                },
            };
        }

        private static QuestStepEntry Voiced(QuestStepEntry step, string voicePrefix)
        {
            step.voicePrefix = voicePrefix;
            return step;
        }

        private static QuestStepEntry Talk(string npc, string objective, string guide, params string[] dialogue)
        {
            return new QuestStepEntry { kind = QuestStepKind.Talk, npc = npc, objective = objective, guideLabel = guide, dialogue = dialogue, count = 1 };
        }

        private static QuestStepEntry Fight(QuestStepKind kind, int monster, int count, string objective, string guide)
        {
            return new QuestStepEntry { kind = kind, monsterId = monster, count = count, objective = objective, guideLabel = guide, dialogue = new string[0] };
        }

        private static QuestStepEntry Point(QuestStepKind kind, string point, int count, string objective, string guide)
        {
            return new QuestStepEntry { kind = kind, point = point, count = count, objective = objective, guideLabel = guide, dialogue = new string[0] };
        }
    }
}
