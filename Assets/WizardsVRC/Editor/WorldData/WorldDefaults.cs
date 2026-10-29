using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// The starting world data: the monsters, the Starfall Castle hub map (a quarter for every school, tutorial NPCs),
    /// Sunpetal Gardens (the gentle first street, Lv 1-3), the maze-style Lantern Row (Lv 4-6) and their questlines.
    /// Only used to create the assets in Assets/WizardsVRC/WorldData (and to upgrade older folders once, see
    /// WorldDataStore); after that, the assets are what you edit.
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
                M(5, "Gloom Wisp", School.Death, 4, 300, false, 0, new[] { 148 }, 45, 8, 16, 10, 20, 0.6f, new Color(0.35f, 0.4f, 0.55f), "Lantern Row. Hovers in pairs."),
                M(6, "Lantern Snatcher", School.Myth, 5, 400, false, 0, new[] { 160, 161 }, 60, 10, 22, 11, 25, 0.9f, new Color(0.45f, 0.35f, 0.25f),
                  "Lantern Row. Goblin-like thieves carrying stolen lamp-flames."),
                M(7, "Ashwick the Snuffer", School.Death, 6, 1200, true, 500, new[] { 148, 149, 150, 154 }, 220, 60, 110, 62, 40, 1.6f, new Color(0.2f, 0.22f, 0.3f),
                  "Lantern Row boss in the Snuffer's Tower. Drops the Ironbound Deck."),
                M(8, "Puddle Newt", School.Ice, 1, 170, false, 0, new[] { 112 }, 20, 4, 10, 0, 0, 0.6f, new Color(0.45f, 0.7f, 0.85f), "Sunpetal Gardens ponds."),
                M(9, "Thistle Sprout", School.Life, 1, 150, false, 0, new[] { 136 }, 18, 3, 9, 0, 0, 0.6f, new Color(0.45f, 0.75f, 0.3f), "Sunpetal Gardens. Walking weeds; the first monsters most wizards fight."),
                M(10, "Mischief Moth", School.Myth, 2, 190, false, 0, new[] { 160 }, 28, 5, 12, 11, 15, 0.7f, new Color(0.8f, 0.7f, 0.95f), "Sunpetal Gardens rose beds. Drops moth dust (quest 14)."),
                M(11, "Old Briarwick", School.Life, 3, 650, true, 300, new[] { 136, 137, 138 }, 120, 30, 60, 61, 40, 1.5f, new Color(0.35f, 0.42f, 0.2f),
                  "Sunpetal Gardens boss in the briar patch. Drops the Scholar's Deck."),
                M(12, "Training Dummy", School.Balance, 1, 120, false, 0, new int[0], 15, 1, 3, 0, 0, 1f, new Color(0.7f, 0.55f, 0.35f),
                  "Starfall Castle practice ring. Never attacks (no spells), so new wizards can learn the duel ring safely."),
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
            z.levelRange = "Lv 4-6";
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

        public const string LanternRowIntro =
            "Gatekeeper Bramwell says the lamps of Lantern Row are going dark one by one. Take the Lantern Row gate in the castle's south wall and find Watchwoman Tilda.";
        public const string LanternRowIntroStep = "Talk to Watchwoman Tilda on Lantern Row (gate in the castle's south wall)";
        // What older world data folders were created with, so the one-time upgrade only changes values nobody edited.
        public const string OldLanternRowIntro =
            "The Headmaster has heard that the lamps of Lantern Row are going dark one by one. Cross the south bridge and find Watchwoman Tilda at the street gate.";
        public const string OldLanternRowIntroStep = "Talk to Watchwoman Tilda on Lantern Row (door past the south bridge)";

        public static void FillLanternRowQuests(Questline ql)
        {
            // Quest 2's dialogue reuses Tilda's recorded intro lines (Audio/Voice/lr_tilda_00-03.wav).
            ql.includeInBuild = true;
            ql.quests = new List<QuestEntry>
            {
                new QuestEntry
                {
                    saveId = 2, title = "Trouble on Lantern Row", prerequisite = 15, minLevel = 3, xp = 40, gold = 20,
                    description = LanternRowIntro,
                    steps = new List<QuestStepEntry>
                    {
                        Voiced(Talk("Tilda", LanternRowIntroStep, "Watchwoman Tilda",
                            "Halt! Oh, a wizard from the castle. Thank the stars.",
                            "I'm Tilda, I keep watch over Lantern Row. Or I did, until the lamps started going out.",
                            "Gloom Wisps snuff the flames, and hooded Snatchers carry them off to the old tower at the end of the street.",
                            "Something in that tower is drinking the light. Help me take the street back, one lamp at a time."), "lr_tilda"),
                    },
                },
                new QuestEntry
                {
                    saveId = 3, title = "Lights Out", prerequisite = 2, minLevel = 4, xp = 80, gold = 30,
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
                    saveId = 4, title = "Stolen Light", prerequisite = 3, minLevel = 4, xp = 120, gold = 40,
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
                    saveId = 5, title = "The Snuffer's Tower", prerequisite = 4, minLevel = 5, xp = 250, gold = 100, rewardItem = 26,
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
                    saveId = 6, title = "Whiskers Is Missing", startsAutomatically = false, giverNpc = "Marla", prerequisite = 2, minLevel = 3, xp = 60, gold = 25,
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
                    saveId = 7, title = "Pip's Broken Toy", startsAutomatically = false, giverNpc = "Pip", prerequisite = 2, minLevel = 3, xp = 60, gold = 15, rewardItem = 10,
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

        // ------------------------------------------------------------------ Starfall Castle (the hub)

        /// <summary>
        /// The hub, laid out like Wizard City's Ravenwood and Commons: the Starfall fountain and arrival point in the
        /// middle, the Headmaster's tower to the north with Storm, Balance and Myth beside it, Fire and Ice on the west
        /// School Green, Life and Death on the east one, the Bazaar and Library to the west, the Training Grounds (practice
        /// ring and boss arena pad) to the east, and the street gates in the south wall.
        /// </summary>
        public static void FillStarfallCastle(ZoneDefinition z)
        {
            z.zoneKey = "StarfallCastle";
            z.displayName = "Starfall Castle";
            z.levelRange = "Hub";
            z.isHub = true;
            z.castleStyle = true;
            z.height = 0f;
            z.cellSize = 6f;
            z.map =
            "##################################################\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,^,,,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,,,^,,,#\n" +
            "#,,,,,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,^,,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,^,,,^,#\n" +
            "#,,,,,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,,,,,,,,,,,,,,,,,,,KKKKKKKK,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,,,,,,,,................................,,,,,,,,#\n" +
            "#,,,,,,,,................................,,,,,,,,#\n" +
            "#,FFFFF,,.,,,,,,,,,,,,,,..,,,,,,,,,,,,,,.,,VVVVV,#\n" +
            "#,FFFFF,,.,,^,,,,^,,,,,,..,,,,,^,,,,^,,,.,,VVVVV,#\n" +
            "#,FFFFF,,.,,,w,,,,,,,,,,..,,,,,,,,,,,,,,.,,VVVVV,#\n" +
            "#,FFFFF,,.,,,,,^,,,,,,,,..,,,,,,,^,,,,,,.,,VVVVV,#\n" +
            "#,,,,,,,,.,,,,,,,,,,,,,,..,,,,,,,,,,,,,,.,,,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,.,,,,,,,,,,,,,..,,,,,,,,,,,,,.,,,,,,,,,#\n" +
            "#,,,,,,,,,.,,,,,,,,^,,,,..,,,,^,,,,,,,,.,,,,,,,,,#\n" +
            "#,IIIII,,,.,,^,,,,,,,,,,..,,,,,,,,,,^,,.,,,DDDDD,#\n" +
            "#,IIIII,,,.,,,,,,,..............,,,,,,,.,,,DDDDD,#\n" +
            "#,IIIII,,,.,,,,,^,..............,^,,,,,.,,,DDDDD,#\n" +
            "#,IIIII,,,.,,,,,,,..C........P..,,,,,,,.,,,DDDDD,#\n" +
            "#,,,,,,,,,.,,,,,,,.....QQQQ.....,,,,,,,.,,,,,,,,,#\n" +
            "#,,,,,,,,,.,,,,,,,.....QQQQ.....,,,,,,,.,,,,,,,,,#\n" +
            "#,,,,,,,,,.............QQQQ.....,...............,#\n" +
            "#,,,,,,,,,....E.k......QQQQ.....,.....aaaaaaa...,#\n" +
            "#,,U,,U,,,......................,..H..a.....a...,#\n" +
            "#,,..............y...A......N...,z....a..1..a...,#\n" +
            "#,,.....................S.......,.....a.....a...,#\n" +
            "#,,U,,U,,,......................,.....aaaaaaa...,#\n" +
            "#,,,,,,,,,..U..U..,,,,,,..,,,,,,,...............,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,.....,,,,,,,,,,,..,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,OOOOOO.....,,,,,,,====..====,,,,,,,,,,^,......,#\n" +
            "#,OOOOOO...t.,,,,,,,====..====,,,,,^,,,,,,......,#\n" +
            "#,OOOOOO.J...,,,,,,^,,,,..,,^,,,,,,,,,,,,,...Z..,#\n" +
            "#,OOOOOO.....,,,,,,,,,,,..,,,,,,,,,,,^,,,,......,#\n" +
            "#,OOOOOO.....,,,,,,,,^,,..,,,,^,,,,,,,,,,,......,#\n" +
            "#,,,,,,,.....,,,,^,,,,,,..,,,,,,^,,,,,,,,,,,,,,,,#\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,..,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,,,.R.,,,,,,,,,..,,,,,,,,,.G.,,,,,,,,,,,#\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "##################################################\n";
            z.groundColor = new Color(0.36f, 0.72f, 0.26f);
            z.roadColor = new Color(0.82f, 0.72f, 0.56f);
            z.grassColor = new Color(0.42f, 0.78f, 0.3f);
            z.buildingColor = new Color(0.66f, 0.62f, 0.7f);
            z.roofColor = new Color(0.45f, 0.25f, 0.82f);
            z.lowWallColor = new Color(0.2f, 0.5f, 0.24f);
            z.treeColor = new Color(0.2f, 0.6f, 0.25f);
            z.accentColor = new Color(1f, 0.8f, 0.4f);
            z.buildingHeight = new Vector2(8f, 12f);
            z.lampDensity = 0.35f;

            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "A", key = "Bramwell", displayName = "Gatekeeper Bramwell", robeColor = new Color(0.25f, 0.32f, 0.6f), hatColor = new Color(0.15f, 0.2f, 0.4f),
                    beard = true, facing = -1f,
                    defaultDialogue = new[] { "The gates open as you prove yourself. Ask me if you're ever lost; I know every stone of this castle." },
                },
                new ZoneNpc
                {
                    symbol = "E", key = "Pell", displayName = "Quartermaster Pell", robeColor = new Color(0.6f, 0.4f, 0.2f), hatColor = new Color(0.4f, 0.25f, 0.12f),
                    facing = -1f,
                    defaultDialogue = new[] { "Hats, robes, boots, wands, decks, potions. If a wizard carries it, I've sold one." },
                },
                new ZoneNpc
                {
                    symbol = "J", key = "Wren", displayName = "Librarian Wren", robeColor = new Color(0.35f, 0.25f, 0.5f), hatColor = new Color(0.22f, 0.15f, 0.32f),
                    facing = -1f,
                    defaultDialogue = new[] { "Every spell ever cast is written down somewhere in this library. Most of them twice." },
                },
                new ZoneNpc
                {
                    symbol = "H", key = "Holloway", displayName = "Duel Master Holloway", robeColor = new Color(0.55f, 0.15f, 0.15f), hatColor = new Color(0.3f, 0.08f, 0.08f),
                    beard = true, facing = -1f,
                    defaultDialogue = new[] { "Practice ring's open whenever you like. The arena pad over there is for when you're good and ready." },
                },
            };

            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "w", key = "SchoolGreen", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 3f },
                new ZonePoint { symbol = "y", key = "Bazaar", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 3f },
                new ZonePoint { symbol = "z", key = "TrainingGrounds", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 2f },
                new ZonePoint { symbol = "k", key = "SupplyCrate", kind = ZonePointKind.Interact, look = ZonePointLook.Crate, label = "Supply crate" },
                new ZonePoint { symbol = "t", key = "SpellTome", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Spell tome" },
            };

            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "TrainingDummies", roster = new List<int> { 12, 12 }, wanderers = 2, patrolSpeed = 0.5f, respawnSeconds = 10, evenFight = true },
            };

            z.landmarks = new List<ZoneLandmark>
            {
                Hall("F", School.Fire), Hall("I", School.Ice), Hall("W", School.Storm), Hall("V", School.Life),
                Hall("D", School.Death), Hall("M", School.Myth), Hall("B", School.Balance),
                new ZoneLandmark { symbol = "K", kind = LandmarkKind.Keep, label = "Headmaster's Tower (click door)" },
                new ZoneLandmark { symbol = "Q", kind = LandmarkKind.Fountain, label = "The Starfall Crystal" },
                new ZoneLandmark { symbol = "O", kind = LandmarkKind.Library, label = "Library" },
                new ZoneLandmark { symbol = "U", kind = LandmarkKind.ShopStall, label = "" },
                new ZoneLandmark
                {
                    symbol = "G", kind = LandmarkKind.StreetGate, targetZone = "SunpetalGardens", requiredQuest = 11,
                    lockedHint = "Locked: finish Duel Master Holloway's lesson first",
                },
                new ZoneLandmark
                {
                    symbol = "R", kind = LandmarkKind.StreetGate, targetZone = "LanternRow", requiredQuest = 15,
                    lockedHint = "Locked: help Gardener Moss in Sunpetal Gardens first",
                },
                new ZoneLandmark { symbol = "Z", kind = LandmarkKind.ArenaPad, requiredQuest = 15, lockedHint = "help Gardener Moss first" },
                new ZoneLandmark { symbol = "C", kind = LandmarkKind.BankChest },
                new ZoneLandmark { symbol = "P", kind = LandmarkKind.BackpackPedestal },
                new ZoneLandmark { symbol = "N", kind = LandmarkKind.NoticeBoard },
            };
        }

        private static ZoneLandmark Hall(string symbol, School school)
        {
            return new ZoneLandmark { symbol = symbol, kind = LandmarkKind.SchoolHall, school = school, label = "" };
        }

        // ------------------------------------------------------------------ Sunpetal Gardens (first street)

        /// <summary>
        /// The gentle first street: open lawns and hedge rooms with weak monsters (Sprouts near the gate, Newts by the
        /// ponds, Moths in the rose beds) and Old Briarwick in the briar patch, north-west, by the overgrown gazebo.
        /// </summary>
        public static void FillSunpetalGardens(ZoneDefinition z)
        {
            z.zoneKey = "SunpetalGardens";
            z.displayName = "Sunpetal Gardens";
            z.levelRange = "Lv 1-3";
            z.height = 700f;
            z.cellSize = 8f;
            z.map =
            "#############################\n" +
            "#^,,,,,,,,,,,^,,,,,,,,,,,,,^#\n" +
            "#,==========,,,,==========,,#\n" +
            "#,=,,,,,,,,=.....=,,,,,,,=,,#\n" +
            "#,=,ddd,,,,=..Y..=,,ccccc=,,#\n" +
            "#,=,d4d,,,,=..X..=,,c...c=,,#\n" +
            "#,=,ddd,,,,.......,,c.3.c=,,#\n" +
            "#,=,,,,,,,,=.....=,,c...c=,,#\n" +
            "#,=====.====..=..=,,ccccc=,,#\n" +
            "#,,,,,,.,,,,..=..=,,,,,,,=,,#\n" +
            "#,^,,,,.,,,,..=..====.====,,#\n" +
            "#,,.........................#\n" +
            "#,,.........................#\n" +
            "#,,.,,,,,,=,,,.,,,=,,,,,,.,,#\n" +
            "#^,.,,^,,,=,,,.,,,=,,^,,,.,^#\n" +
            "#,,.,,,,,,=,,,.,,,=,,,,,,.,,#\n" +
            "#,,.,,,,,,=====.====,,,,,.,,#\n" +
            "#,,.......bbbbbbbbbbbb....,,#\n" +
            "#,,.,,,,,,,,,,,2,,,,,,,,..,,#\n" +
            "#,,.,,,,^,,,,.....,,,,^,..,,#\n" +
            "#,,.,,,,,,,,,.....,,,,,,..,,#\n" +
            "#,,.,,,,,,,,,,,.,,,,,,,,..,,#\n" +
            "#,,.........................#\n" +
            "#,,.........................#\n" +
            "#,,,,,,==,==,,,.,,,==,==,,,,#\n" +
            "#,^,,,,=,,,=,,,.,,,=,,,=,,^,#\n" +
            "#,,,,,,=,,,aaaaaaaa=,,,=,,,,#\n" +
            "#,,,,,,=,,,a,,,,,,a=,,,=,,,,#\n" +
            "#,,,,,,=,,,a,,1,,,a=,,,=,,,,#\n" +
            "#,,,,,,=,,,a,,,,,,a=,,,=,,,,#\n" +
            "#,,,,,,====aaaaaaaa====,,,,,#\n" +
            "#,,,,,,,,,,,,,.,,,,,,,,,,,,,#\n" +
            "#,,^,,,,,,,,M..,,,,,,,^,,,,,#\n" +
            "#,,,,,,,,,,,,.S.,,,,,,,,,,,,#\n" +
            "#############################\n";
            z.groundColor = new Color(0.25f, 0.42f, 0.2f);
            z.roadColor = new Color(0.7f, 0.6f, 0.45f);
            z.grassColor = new Color(0.35f, 0.62f, 0.28f);
            z.buildingColor = new Color(0.6f, 0.55f, 0.5f);
            z.roofColor = new Color(0.55f, 0.3f, 0.25f);
            z.lowWallColor = new Color(0.22f, 0.45f, 0.2f);
            z.treeColor = new Color(0.25f, 0.55f, 0.25f);
            z.accentColor = new Color(1f, 0.6f, 0.75f);
            z.buildingHeight = new Vector2(6f, 9f);
            z.lampDensity = 0.15f;
            z.hubDoorPosition = new Vector3(20f, 2.5f, -81f);
            z.hubReturnPosition = new Vector3(20f, 0.1f, -72f);
            z.hubReturnYaw = 180f;

            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "M", key = "Moss", displayName = "Gardener Moss", robeColor = new Color(0.3f, 0.45f, 0.2f), hatColor = new Color(0.45f, 0.35f, 0.15f),
                    beard = true, facing = -1f,
                    defaultDialogue = new[] { "Mind the flowerbeds. The ones that aren't trying to bite you, anyway." },
                },
            };
            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "X", key = "OvergrownGazebo", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 2f },
            };
            z.encounters = new List<ZoneEncounter>
            {
                // Even fights throughout: a new wizard only ever faces one monster at a time (one more per extra wizard).
                new ZoneEncounter { ringSymbol = "1", key = "Sprouts", roster = new List<int> { 9, 9 }, wanderers = 2, patrolSpeed = 1f, respawnSeconds = 20, evenFight = true },
                new ZoneEncounter { ringSymbol = "2", key = "Newts", roster = new List<int> { 8, 8 }, wanderers = 2, patrolSpeed = 1.1f, respawnSeconds = 20, evenFight = true },
                new ZoneEncounter { ringSymbol = "3", key = "Moths", roster = new List<int> { 10, 9 }, wanderers = 2, patrolSpeed = 1.3f, respawnSeconds = 20, evenFight = true },
                new ZoneEncounter { ringSymbol = "4", key = "Briarwick", roster = new List<int> { 11, 9, 9 }, wanderers = 1, patrolSpeed = 0.6f, respawnSeconds = 30, scaleWithParty = true, evenFight = true },
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "Y", kind = LandmarkKind.Gazebo, label = "" },
            };
        }

        // ------------------------------------------------------------------ tutorial and gardens questlines

        /// <summary>Quests 8-12: the castle tour that teaches the backpack, potions, decks and the duel ring before any real fight.</summary>
        public static void FillTutorialQuests(Questline ql)
        {
            ql.includeInBuild = true;
            ql.quests = new List<QuestEntry>
            {
                new QuestEntry
                {
                    saveId = 8, title = "Finding Your Way", prerequisite = QuestManager.QUEST_PROPHECY, minLevel = 1, xp = 50, gold = 20,
                    description = "Gatekeeper Bramwell keeps watch over Starfall Castle. He wants to show you around the grounds before you go anywhere dangerous.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Bramwell", "Meet Gatekeeper Bramwell by the Starfall fountain", "Gatekeeper Bramwell",
                            "So you're the Headmaster's new star! Welcome, welcome.",
                            "Before anyone lets you near a monster, you ought to know your way round the castle.",
                            "Have a look at the School Green to the west, the Bazaar, and the Training Grounds to the east. Then come back and tell me what you saw."),
                        Point(QuestStepKind.Explore, "SchoolGreen", 1, "Visit the School Green, west of the fountain", "School Green"),
                        Point(QuestStepKind.Explore, "Bazaar", 1, "Visit the Bazaar", "Bazaar"),
                        Point(QuestStepKind.Explore, "TrainingGrounds", 1, "Visit the Training Grounds", "Training Grounds"),
                        Talk("Bramwell", "Report back to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                            "Seen it all? Good. Every school has its own quarter, and your professor will teach you new spells as you level up.",
                            "Quartermaster Pell in the Bazaar wants a word. She equips every new wizard."),
                    },
                },
                new QuestEntry
                {
                    saveId = 9, title = "Tools of the Trade", prerequisite = 8, minLevel = 1, xp = 50, gold = 25, rewardItem = 10,
                    description = "Quartermaster Pell runs the Bazaar. She'll show you your backpack: equipment, potions and decks.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Pell", "Talk to Quartermaster Pell in the Bazaar", "Quartermaster Pell",
                            "Another new wizard! Let's get you kitted out.",
                            "Your backpack holds everything you carry: hats, robes, boots, wands, athames, amulets, rings and decks. Press B, or click the book on the pedestal by the fountain.",
                            "Open the supply crate beside my stall. There's a healing potion in there with your name on it."),
                        Point(QuestStepKind.Interact, "SupplyCrate", 1, "Open the supply crate beside Pell's stall", "Supply crate"),
                        Talk("Pell", "Tell Quartermaster Pell what you found", "Quartermaster Pell",
                            "That's the one. Drink a potion from your backpack to heal, or use one as your turn in a duel.",
                            "Gear you equip makes you tougher and your spells stronger. Better gear drops from monsters and chests.",
                            "Now, Librarian Wren is expecting you at the Library, south-west of here."),
                    },
                },
                new QuestEntry
                {
                    saveId = 10, title = "The Spellbook", prerequisite = 9, minLevel = 1, xp = 50, gold = 20, rewardItem = 11,
                    description = "Librarian Wren keeps the castle library. She knows more about spell cards than anyone alive.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Wren", "Visit Librarian Wren at the Library", "Librarian Wren",
                            "Ah, the new student. Mind the stacks; some of the older books bite.",
                            "In a duel you cast spells from the cards in your deck. Each card costs pips, and you gain pips every round, so big spells take patience.",
                            "Spells of your own school are strongest in your hands. Fetch the spell tome from the reading stand and I'll show you."),
                        Point(QuestStepKind.Interact, "SpellTome", 1, "Read the spell tome in the library court", "Spell tome"),
                        Talk("Wren", "Return the tome to Librarian Wren", "Librarian Wren",
                            "See the number on each card? That's its chance to land. A spell that fizzles goes back into your deck.",
                            "Open the Deck page of your backpack to choose which cards you carry. Your professor teaches new ones every few levels.",
                            "Duel Master Holloway runs the Training Grounds. Go and show him what you've learned."),
                    },
                },
                new QuestEntry
                {
                    saveId = 11, title = "First Duel", prerequisite = 10, minLevel = 1, xp = 70, gold = 30, rewardItem = 30,
                    description = "Duel Master Holloway trains every young wizard on the practice ring before they're allowed beyond the castle walls.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Holloway", "Report to Duel Master Holloway at the Training Grounds", "Duel Master Holloway",
                            "Holloway. Duel Master. Pleased to meet you.",
                            "Walk into a monster and a duel ring forms: wizards on one side, monsters on the other, and everyone picks a card at the same time.",
                            "These training dummies won't hit back. Beat them, and keep an eye on your pips and your health while you do it."),
                        Fight(QuestStepKind.Defeat, 12, 2, "Defeat the training dummies on the practice ring", "Training dummies"),
                        Talk("Holloway", "Return to Duel Master Holloway", "Duel Master Holloway",
                            "Not bad at all! Real monsters hit back, mind. If a fight goes badly you can Flee, or drink a potion as your turn.",
                            "Take this athame; it'll steady your aim. I'll tell the Gatekeeper you're ready for the gardens."),
                    },
                },
                new QuestEntry
                {
                    saveId = 12, title = "Out Into the World", prerequisite = 11, minLevel = 1, xp = 50, gold = 25,
                    description = "Gatekeeper Bramwell has opened the gate to Sunpetal Gardens. The weeds out there have come alive, and the old gardener needs a hand.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Bramwell", "Talk to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                            "Holloway says you held your own. I've opened the gate to Sunpetal Gardens, in the south wall.",
                            "The weeds have got up and started walking, and Gardener Moss is at his wit's end. Go and help him.",
                            "Gardens first, mind. Lantern Row stays shut until you've proven yourself out there."),
                        Talk("Moss", "Find Gardener Moss in Sunpetal Gardens (gate in the south wall)", "Gardener Moss",
                            "Oh, thank goodness. Help from the castle!",
                            "A week ago the thistles got up and walked off. Now there are newts in the ponds and moths eating my roses.",
                            "Something deeper in the garden is stirring them up, I'm sure of it."),
                    },
                },
            };
        }

        /// <summary>Quests 13-15 in Sunpetal Gardens; finishing 15 opens Lantern Row and the boss arena.</summary>
        public static void FillGardenQuests(Questline ql)
        {
            ql.includeInBuild = true;
            ql.quests = new List<QuestEntry>
            {
                new QuestEntry
                {
                    saveId = 13, title = "Weeds in the Garden", prerequisite = 12, minLevel = 1, xp = 70, gold = 30, rewardItem = 50,
                    description = "Thistle Sprouts have uprooted themselves and are wandering the lawns by the gate. Thin them out for Gardener Moss.",
                    steps = new List<QuestStepEntry>
                    {
                        Fight(QuestStepKind.Defeat, 9, 3, "Defeat Thistle Sprouts", "Thistle Sprouts"),
                        Talk("Moss", "Return to Gardener Moss", "Gardener Moss",
                            "Three fewer weeds! You've got a real green thumb. Well, a magic one.",
                            "The ponds are next. Newts have been splashing about, and the moths won't leave my roses be."),
                    },
                },
                new QuestEntry
                {
                    saveId = 14, title = "Pest Control", prerequisite = 13, minLevel = 2, xp = 100, gold = 40, rewardItem = 40,
                    description = "Puddle Newts are churning up the ponds and Mischief Moths are eating the roses. Moss wants a look at the strange dust on the moths' wings.",
                    steps = new List<QuestStepEntry>
                    {
                        Fight(QuestStepKind.Defeat, 8, 2, "Defeat Puddle Newts by the ponds", "Puddle Newts"),
                        Fight(QuestStepKind.Collect, 10, 3, "Collect moth dust from Mischief Moths", "Mischief Moths"),
                        Talk("Moss", "Bring the moth dust to Gardener Moss", "Gardener Moss",
                            "Moth dust... and it's glowing green. That's no ordinary garden magic.",
                            "It's the old briar patch up by the gazebo. Something has taken root in there, something old."),
                    },
                },
                new QuestEntry
                {
                    saveId = 15, title = "The Briar Heart", prerequisite = 14, minLevel = 3, xp = 150, gold = 80, rewardItem = 31,
                    description = "The trouble in the gardens comes from the briar patch beside the overgrown gazebo, where something ancient has woken up.",
                    steps = new List<QuestStepEntry>
                    {
                        Point(QuestStepKind.Explore, "OvergrownGazebo", 1, "Reach the overgrown gazebo at the north end of the gardens", "Overgrown gazebo"),
                        Fight(QuestStepKind.Boss, 11, 1, "Defeat Old Briarwick in the briar patch", "Old Briarwick"),
                        Talk("Moss", "Tell Gardener Moss the garden is safe", "Gardener Moss",
                            "Old Briarwick? He's slept in that patch since before the castle was built. Something woke him.",
                            "You've done the castle proud. I'll send word to the Headmaster, and Bramwell will open the Lantern Row gate for you.",
                            "Mind yourself out there. Whatever woke Briarwick isn't finished yet."),
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
