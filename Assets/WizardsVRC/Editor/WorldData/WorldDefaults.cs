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
    public static partial class WorldDefaults
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
                // Mend (small heal) instead of Verdant Bloom: he's the first boss most wizards meet, and out-healing a new
                // wizard's damage made the fight drag on.
                M(11, "Old Briarwick", School.Life, 3, 650, true, 300, new[] { 136, 201, 138 }, 120, 30, 60, 61, 40, 1.5f, new Color(0.35f, 0.42f, 0.2f),
                  "Sunpetal Gardens boss in the briar patch. Drops the Scholar's Deck. Heals with Mend only: first boss for new wizards."),
                M(12, "Training Dummy", School.Balance, 1, 120, false, 0, new int[0], 15, 1, 3, 0, 0, 1f, new Color(0.7f, 0.55f, 0.35f),
                  "Starfall Castle practice ring. Never attacks (no spells), so new wizards can learn the duel ring safely."),
            };
            foreach (var m in cat.monsters) ApplyDefaultRank(m);
            cat.monsters.AddRange(World1Monsters());
        }

        /// <summary>
        /// World 1's later streets (ids 13-25, see the World 1 blueprint): Tidewatch Docks, Kiln Street, Thornhollow Grove
        /// and the Hollow Keep. The Dominion's own soldiers only appear in the last two streets.
        /// </summary>
        public static List<MonsterEntry> World1Monsters()
        {
            return new List<MonsterEntry>
            {
                R(M(13, "Dock Crab", School.Ice, 7, 450, false, 0, new[] { 112, 113 }, 55, 10, 20, 10, 15, 0.8f, new Color(0.55f, 0.35f, 0.3f),
                    "Tidewatch Docks piers. Tough shell, weak hits."), 1, "Beast", false),
                R(M(14, "Smuggler Thug", School.Storm, 8, 500, false, 0, new[] { 124, 126 }, 60, 12, 24, 11, 15, 1f, new Color(0.35f, 0.32f, 0.3f),
                    "Tidewatch Docks warehouses. Drops shipping manifests (quest 19)."), 2, "Rogue", false),
                R(M(15, "Captain Marrow Grell", School.Death, 8, 1500, true, 600, new[] { 148, 149, 150, 200 }, 330, 60, 110, 0, 0, 1.3f, new Color(0.22f, 0.2f, 0.26f),
                    "Tidewatch Docks boss in the smugglers' warehouse. Carries the coded ledger (quest 20)."), 3, "Rogue", false),
                R(M(16, "Cinder Imp", School.Fire, 8, 420, false, 0, new[] { 100, 101 }, 60, 10, 22, 10, 15, 0.6f, new Color(0.9f, 0.4f, 0.15f),
                    "Kiln Street. Escaped from the kilns: fast, fragile, burns."), 1, "Spirit", false),
                R(M(17, "Hollowed Sentry", School.Death, 9, 600, false, 0, new[] { 148, 149 }, 70, 12, 26, 11, 15, 1.1f, new Color(0.4f, 0.42f, 0.46f),
                    "Kiln Street, Cole's back forge. Empty Dominion armor moved by a spark of stolen light. Drops glowing cores (quest 24)."), 2, "Hollowed", false),
                R(M(18, "Captain Vesper", School.Fire, 10, 2000, true, 700, new[] { 100, 101, 102, 104, 200 }, 380, 80, 140, 0, 0, 1.2f, new Color(0.55f, 0.12f, 0.1f),
                    "Kiln Street boss in the sealed furnace hall. Lantern Corps engineer; breaks off and escapes (quest 25)."), 3, "Dominion", false),
                R(M(19, "Thornback Boar", School.Life, 10, 620, false, 0, new[] { 136, 138 }, 70, 12, 26, 10, 15, 1f, new Color(0.4f, 0.32f, 0.2f),
                    "Thornhollow Grove. Maddened by the survey stakes; charges in pairs."), 1, "Beast", false),
                R(M(20, "Dominion Surveyor", School.Balance, 10, 640, false, 0, new[] { 172, 173, 175 }, 75, 14, 28, 11, 15, 1f, new Color(0.45f, 0.42f, 0.36f),
                    "Thornhollow Grove. The first Dominion soldiers a wizard meets. Drop survey maps (quest 31)."), 2, "Dominion", false),
                R(M(21, "Dominion Legionnaire", School.Ice, 11, 760, false, 0, new[] { 112, 114, 200 }, 80, 16, 30, 10, 15, 1.1f, new Color(0.38f, 0.4f, 0.44f),
                    "Thornhollow Grove and the Hollow Keep. Shield and blade, fights beside a sergeant."), 2, "Dominion", true),
                R(M(22, "Sergeant Halvek Roan", School.Ice, 11, 2200, true, 800, new[] { 112, 113, 114, 116, 200 }, 420, 90, 150, 0, 0, 1.3f, new Color(0.3f, 0.33f, 0.4f),
                    "Thornhollow Grove boss in the clearing camp, with two legionnaires."), 3, "Dominion", false),
                R(M(23, "Merchant Selwyn", School.Myth, 11, 1900, true, 700, new[] { 160, 161, 163, 165 }, 350, 80, 140, 0, 0, 1f, new Color(0.3f, 0.28f, 0.34f),
                    "The Silent Office spy from the Bazaar, caught in the Tidewatch warehouse (quest 34)."), 3, "Dominion", false),
                R(M(24, "Hollowed Knight", School.Death, 12, 850, false, 0, new[] { 148, 149, 150 }, 85, 16, 32, 11, 15, 1.2f, new Color(0.3f, 0.3f, 0.36f),
                    "The Hollow Keep. Morvane's guard: heavier Hollowed."), 3, "Hollowed", true),
                R(M(25, "Morvane the Hollow King", School.Death, 13, 3000, true, 1000, new[] { 149, 150, 152, 153, 154 }, 520, 150, 250, 0, 0, 1.6f, new Color(0.15f, 0.12f, 0.2f),
                    "World 1's last boss, in the Hollow Keep's throne hall, with two Hollowed Knights (quest 38)."), 4, "Undead King", false),
            };
        }

        private static MonsterEntry R(MonsterEntry m, int rank, string type, bool elite)
        {
            m.rank = rank;
            m.creatureType = type;
            m.isElite = elite;
            return m;
        }

        // Rank and creature type of the default monsters, by id (index 0 unused).
        private static readonly int[] DefaultRanks = { 0, 3, 2, 1, 1, 2, 2, 3, 1, 1, 1, 2, 1 };
        private static readonly string[] DefaultTypes =
        {
            "", "Undead", "Undead", "Spirit", "Beast", "Spirit", "Goblin", "Spirit", "Beast", "Plant", "Insect", "Plant", "Construct",
        };

        /// <summary>Sets a default monster's rank and creature type (ids 1-12); others are left alone.</summary>
        public static bool ApplyDefaultRank(MonsterEntry m)
        {
            if (m == null || m.id <= 0 || m.id >= DefaultRanks.Length) return false;
            m.rank = DefaultRanks[m.id];
            m.creatureType = DefaultTypes[m.id];
            return true;
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
            z.textureStyle = ZoneTextureStyle.Town;
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
            "######fffffffffffffffffffffffff##\n" +
            "#####...#.6.##.##.3.##.#######f##\n" +
            "#####.7.#...##.##...##.#######f##\n" +
            "##ggggggg#....ccccccccc....###f##\n" +
            "##g#####g#.###c....#..c###.#..f##\n" +
            "##g#^,G#g#.###c..3.#..c###...6f##\n" +
            "##g#,,,#g#.###c......3c###.#..f##\n" +
            "##g##.##ggg###ccccccccc###.###f##\n" +
            "##gggg########.#####.#####.#..f##\n" +
            "######........L#####.........6f##\n" +
            "######.7.####.######.#######..f##\n" +
            "#........####.######.#########f##\n" +
            "#.#####.#####bbbbbbbb#####,,,#f##\n" +
            "#.#,,C#.###..b......b.####,K,#f##\n" +
            "#.#,,,#.....2b......b..###.,,#f##\n" +
            "#.##.##.###..b...2..b2.###.###f##\n" +
            "#....##.#####b......b......###f##\n" +
            "######..#####b......b.#.#######.#\n" +
            "#####...#####bbbbbbbb.........L.#\n" +
            "#####.5.############.1.######.#.#\n" +
            "##eeeeeee#####....aaaaaaaaaa.##.#\n" +
            "##e#####e####...##a#####.###.##.#\n" +
            "##e#^G,#e####.5.##a#####.###....#\n" +
            "##e#,,J.eeeeeee.##a#####.###.####\n" +
            "##e#,,,.5.####e###a......###.####\n" +
            "##e...#...####e#..a#####.###.####\n" +
            "##e5.....#####e..1a#####.....####\n" +
            "##...#####....e...a##############\n" +
            "##########....e...a##############\n" +
            "##########........a##############\n" +
            "######............a##############\n" +
            "######..L......1..a##############\n" +
            "######..T..1......a##############\n" +
            "######.aaaaaaaaaaaa##############\n" +
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
            // Dusk over the lamplit street.
            z.skyTopColor = new Color(0.13f, 0.11f, 0.36f);
            z.skyHorizonColor = new Color(0.9f, 0.48f, 0.42f);
            z.fogColor = new Color(0.45f, 0.32f, 0.44f);
            z.horizonDistance = 1150f;
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
                new ZoneEncounter { ringSymbol = "1", key = "Wisps", roster = new List<int> { 5, 5 }, wanderers = 4, patrolSpeed = 1.3f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "2", key = "Snatchers", roster = new List<int> { 6, 5, 6 }, wanderers = 3, patrolSpeed = 1.5f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "3", key = "SnatcherGang", roster = new List<int> { 6, 6 }, wanderers = 3, patrolSpeed = 1.5f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "4", key = "Snuffer", roster = new List<int> { 7, 5, 5 }, wanderers = 1, patrolSpeed = 0.8f, respawnSeconds = 30, scaleWithParty = true },
                new ZoneEncounter { ringSymbol = "5", key = "WispPack", roster = new List<int> { 5, 5, 5 }, wanderers = 3, patrolSpeed = 1.2f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "6", key = "LampLighters", roster = new List<int> { 6, 5 }, wanderers = 5, patrolSpeed = 1.4f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "7", key = "AlleyWisps", roster = new List<int> { 5, 5 }, wanderers = 3, patrolSpeed = 1.3f, respawnSeconds = 20 },
            };
        }

        /// <summary>Quest texts as world data v11 had them (gates in the south wall) and what they say now (tunnels).</summary>
        public static readonly string[,] TunnelTextsV12 =
        {
            { "Gatekeeper Bramwell says the lamps of Lantern Row are going dark one by one. Take the Lantern Row gate in the castle's south wall and find Watchwoman Tilda.",
              "Gatekeeper Bramwell says the lamps of Lantern Row are going dark one by one. Take the Lantern Row tunnel at the end of the west avenue, past the Bazaar, and find Watchwoman Tilda." },
            { "Talk to Watchwoman Tilda on Lantern Row (gate in the castle's south wall)",
              "Talk to Watchwoman Tilda on Lantern Row (tunnel past the Bazaar, west avenue)" },
            { "Gatekeeper Bramwell has opened the gate to Sunpetal Gardens. The weeds out there have come alive, and the old gardener needs a hand.",
              "Gatekeeper Bramwell has raised the portcullis on the tunnel to Sunpetal Gardens. The weeds out there have come alive, and the old gardener needs a hand." },
            { "Holloway says you held your own. I've opened the gate to Sunpetal Gardens, in the south wall.",
              "Holloway says you held your own. I've raised the portcullis on the Sunpetal Gardens tunnel, at the end of the south avenue." },
            { "Find Gardener Moss in Sunpetal Gardens (gate in the south wall)",
              "Find Gardener Moss in Sunpetal Gardens (tunnel at the end of the south avenue)" },
            { "You've done the castle proud. I'll send word to the Headmaster, and Bramwell will open the Lantern Row gate for you.",
              "You've done the castle proud. I'll send word to the Headmaster, and Bramwell will open the Lantern Row tunnel for you." },
        };

        public const string LanternRowIntro =
            "Gatekeeper Bramwell says the lamps of Lantern Row are going dark one by one. Take the Lantern Row tunnel at the end of the west avenue, past the Bazaar, and find Watchwoman Tilda.";
        public const string LanternRowIntroStep = "Talk to Watchwoman Tilda on Lantern Row (tunnel past the Bazaar, west avenue)";
        // What older world data folders were created with, so the one-time upgrade only changes values nobody edited.
        public const string OldLanternRowIntro =
            "The Headmaster has heard that the lamps of Lantern Row are going dark one by one. Cross the south bridge and find Watchwoman Tilda at the street gate.";
        public const string OldLanternRowIntroStep = "Talk to Watchwoman Tilda on Lantern Row (door past the south bridge)";

        public static void FillLanternRowQuests(Questline ql)
        {
            // Quest 2's dialogue reuses Tilda's recorded intro lines (Audio/Voice/lr_tilda_00-03.wav).
            ql.includeInBuild = true;
            ql.storylineName = "The First Lantern";
            ql.storylineEnd = 5;
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
                            "Look at the shards, though. There's a maker's mark scratched inside: a little 'V' and a lantern. Somebody built this thing.",
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
        /// The hub, laid out in rings like Wizard101's Ravenwood: a round island inside a round curtain wall. In the middle,
        /// the Crystal Tap (a round walk-in tavern round the Starfall fountain) in a sunken round plaza with the arrival
        /// point; a ring of gardens and the inner ring road; the seven school halls and the library on a circle, each
        /// turned to face the centre (walk-in classrooms), with the four great avenues between them; the outer ring road;
        /// and beyond it the Headmaster's tower on its high terrace (north) with a waterfall into a pond, the observatory
        /// hill, the training grounds and arena pad (east), the duelling circle for PvP (south-east), the street gates
        /// (south), the inn, bakery and gazebo garden (south-west) and the bazaar (west). Red and blue orbs on the ring
        /// roads top up health and mana.
        /// </summary>
        public static void FillStarfallCastle(ZoneDefinition z)
        {
            z.zoneKey = "StarfallCastle";
            z.displayName = "Starfall Castle";
            z.levelRange = "Hub";
            z.isHub = true;
            z.castleStyle = true;
            z.roundCastle = true;
            z.height = 0f;
            z.cellSize = 4f;
            z.map = HubMapV13;
            // Ground level is 1 (1.5 m); the central plaza and the pond are sunk to 0, the duelling circle and the gazebo
            // garden raised to 2, the Headmaster's terrace to 3 (with a landing at 2) and the observatory hill to 4.
            z.heightStep = 1.5f;
            z.cornerRadius = 1.8f;
            z.railings = RailingMode.DropsAndPaths;
            z.heightMap = HubHeightsV12;
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
                new ZoneNpc
                {
                    symbol = "@", key = "Mabel", displayName = "Mabel Brightcask", robeColor = new Color(0.62f, 0.32f, 0.2f), hatColor = new Color(0.35f, 0.18f, 0.12f),
                    facing = 270f,
                    defaultDialogue = new[]
                    {
                        "Welcome to the Crystal Tap! Grab a mug, pull up a stool, and stay as long as you like.",
                        "Ring the bell on the bar when the mugs go walkabout. They always come home.",
                        "Sailors in last night swore they saw ships past the reef. Grey ships, no lights. More ale, I said.",
                    },
                },
                new ZoneNpc
                {
                    symbol = "u", key = "Wick", displayName = "Stargazer Tobias Wick", robeColor = new Color(0.12f, 0.15f, 0.35f), hatColor = new Color(0.7f, 0.65f, 0.3f),
                    facing = -1f,
                    defaultDialogue = new[] { "The stars are moving wrong this season. Not wrong. Just... not the way they did." },
                },
                new ZoneNpc
                {
                    symbol = "x", key = "Selwyn", displayName = "Merchant Selwyn", robeColor = new Color(0.32f, 0.3f, 0.36f), hatColor = new Color(0.2f, 0.18f, 0.22f),
                    facing = -1f, hideAfterQuest = 34,
                    defaultDialogue = new[] { "Maps, charts, curios from far shores! Every road on the isle, drawn true. Ask me anything about the castle." },
                },
                new ZoneNpc
                {
                    symbol = "<", key = "Herald", displayName = "Dominion Herald", robeColor = new Color(0.38f, 0.4f, 0.44f), hatColor = new Color(0.55f, 0.08f, 0.1f),
                    facing = 0f, showFromQuest = 16, hideAfterQuest = 16,
                    defaultDialogue = new[] { "I carry the Grand General's word, under a white flag. I am not here to fight you. Not today." },
                },
            };

            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "w", key = "SchoolGreen", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 4f },
                new ZonePoint { symbol = "y", key = "Bazaar", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 5f },
                new ZonePoint { symbol = "z", key = "TrainingGrounds", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 3f },
                new ZonePoint { symbol = "k", key = "SupplyCrate", kind = ZonePointKind.Interact, look = ZonePointLook.Crate, label = "Supply crate" },
                new ZonePoint { symbol = "t", key = "SpellTome", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Spell tome" },
                new ZonePoint { symbol = "l", key = "Lens", kind = ZonePointKind.Interact, look = ZonePointLook.StreetLamp, label = "Brass lens" },
                new ZonePoint { symbol = "C", key = "CipherWheel", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Cipher wheel" },
                new ZonePoint { symbol = "v", key = "OldRoster", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Old class roster" },
                new ZonePoint { symbol = "s", key = "BazaarStall", kind = ZonePointKind.Interact, look = ZonePointLook.Crate, label = "Stall crate" },
                new ZonePoint { symbol = "(", key = "StarfallCrystal", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Touch the Starfall Crystal" },
            };

            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "TrainingDummies", roster = new List<int> { 12, 12 }, wanderers = 2, patrolSpeed = 0.5f, respawnSeconds = 10, evenFight = true },
            };

            z.landmarks = HubLandmarks();
        }

        /// <summary>The hub's landmarks (world data v11): halls, library and shops turn to face the centre.</summary>
        public static List<ZoneLandmark> HubLandmarks()
        {
            var list = new List<ZoneLandmark>
            {
                Hall("F", School.Fire), Hall("I", School.Ice), Hall("W", School.Storm), Hall("V", School.Life),
                Hall("D", School.Death), Hall("M", School.Myth), Hall("B", School.Balance),
                new ZoneLandmark { symbol = "K", kind = LandmarkKind.Keep, label = "Headmaster's Tower (click door)" },
                new ZoneLandmark { symbol = "Q", kind = LandmarkKind.Fountain, label = "The Starfall Crystal" },
                new ZoneLandmark { symbol = "O", kind = LandmarkKind.Library, label = "Library", doorSide = DoorSide.Centre },
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
                new ZoneLandmark
                {
                    symbol = "$", kind = LandmarkKind.StreetGate, targetZone = "TidewatchDocks", requiredQuest = 5,
                    lockedHint = "Locked: help Watchwoman Tilda on Lantern Row first",
                },
                new ZoneLandmark
                {
                    symbol = "!", kind = LandmarkKind.StreetGate, targetZone = "KilnStreet", requiredQuest = 20,
                    lockedHint = "Locked: deal with the smugglers at Tidewatch Docks first",
                },
                new ZoneLandmark
                {
                    symbol = "%", kind = LandmarkKind.StreetGate, targetZone = "ThornhollowGrove", requiredQuest = 25,
                    lockedHint = "Locked: find out what is happening on Kiln Street first",
                },
                new ZoneLandmark
                {
                    symbol = "?", kind = LandmarkKind.StreetGate, targetZone = "HollowKeep", requiredQuest = 32,
                    lockedHint = "Locked: clear the Dominion camp in Thornhollow Grove first",
                },
                new ZoneLandmark { symbol = "Z", kind = LandmarkKind.ArenaPad, requiredQuest = 15, lockedHint = "help Gardener Moss first" },
                new ZoneLandmark { symbol = "P", kind = LandmarkKind.BackpackPedestal },
                new ZoneLandmark { symbol = "N", kind = LandmarkKind.NoticeBoard },
                new ZoneLandmark { symbol = "T", kind = LandmarkKind.Tower, label = "Observatory" },
                new ZoneLandmark { symbol = "X", kind = LandmarkKind.Waterfall },
                new ZoneLandmark { symbol = "Y", kind = LandmarkKind.Gazebo },
                ShopAt("m", "Hats & Robes"),
                ShopAt("j", "Wands & Staves"),
                ShopAt("n", "Potions & Remedies"),
                ShopAt("o", "The Golden Crumb Bakery"),
                ShopAt("q", "Familiars & Pets"),
                ShopAt("p", "The Fallen Star Inn"),
                ShopAt("r", "Cards & Curios"),
                new ZoneLandmark { symbol = "L", kind = LandmarkKind.Tavern, label = "The Crystal Tap" },
                new ZoneLandmark { symbol = "&", kind = LandmarkKind.DuelCircle, label = "Duelling Circle" },
                new ZoneLandmark { symbol = "+", kind = LandmarkKind.HealthOrb },
                new ZoneLandmark { symbol = "*", kind = LandmarkKind.ManaOrb },
            };
            foreach (var l in list) if (l.kind == LandmarkKind.SchoolHall) l.doorSide = DoorSide.Centre;
            return list;
        }

        /// <summary>
        /// Brings an upgraded hub's lists in line with the current layout: adds landmarks, NPCs and points it is missing
        /// (by symbol), turns the halls, library and shops to face the centre as the round layout needs, and drops the old
        /// testing chest. Labels and other settings you changed are kept.
        /// </summary>
        public static void SyncHubLandmarks(ZoneDefinition z)
        {
            var fresh = ScriptableObject.CreateInstance<ZoneDefinition>();
            FillStarfallCastle(fresh);
            if (z.landmarks == null) z.landmarks = new List<ZoneLandmark>();
            if (z.npcs == null) z.npcs = new List<ZoneNpc>();
            if (z.points == null) z.points = new List<ZonePoint>();
            z.landmarks.RemoveAll(l => l != null && l.kind == LandmarkKind.BankChest);
            foreach (var l in fresh.landmarks)
            {
                var have = z.FindLandmark(l.Symbol);
                if (have != null) { if (have.kind == l.kind) have.doorSide = l.doorSide; continue; }
                if (z.FindNpc(l.Symbol) == null && z.FindPoint(l.Symbol) == null) z.landmarks.Add(l);
            }
            foreach (var n in fresh.npcs)
                if (z.FindNpc(n.Symbol) == null && z.FindLandmark(n.Symbol) == null && z.FindPoint(n.Symbol) == null) z.npcs.Add(n);
            foreach (var p in fresh.points)
                if (z.FindPoint(p.Symbol) == null && z.FindLandmark(p.Symbol) == null && z.FindNpc(p.Symbol) == null) z.points.Add(p);
        }

        private static ZoneLandmark ShopAt(string symbol, string label)
        {
            return new ZoneLandmark { symbol = symbol, kind = LandmarkKind.Shop, label = label, doorSide = DoorSide.Centre };
        }

        /// <summary>
        /// The round hub (world data v12), 72 x 72 cells of 4 m: the south avenue ends at the Sunpetal Gardens tunnel (G)
        /// and the west avenue runs past the Bazaar to the Lantern Row tunnel (R), both through the curtain wall.
        /// </summary>
        public const string HubMapV12 =
            "########################################################################\n" +
            "########################################################################\n" +
            "########################################################################\n" +
            "##############################,,,,,,,,,,,,##############################\n" +
            "##########################,,,,KKKKKKKKKKKK,,,,##########################\n" +
            "#######################,,,,,,,KKKKKKKKKKKK,,,,,,,#######################\n" +
            "#####################,,,,,,^,,KKKKKKKKKKKK,,,^,,,,,#####################\n" +
            "###################,,,,,,,,,,,KKKKKKKKKKKK,,,,,,^,,,,###################\n" +
            "##################,,,,,,,,...,KKKKKKKKKKKK,,,,,,,,,^,,##################\n" +
            "################,,,,,,,,...................,,^,,,,,,,,,,################\n" +
            "###############,,,,,,,....,,,,,,,,....,,,,,,,,,,^,X~~~,,,###############\n" +
            "##############,,,,,,....,,,,,,,..........,,,,,,,,,X~~~~,,,##############\n" +
            "#############,,,,,,...,,,,,..................,,,,,~~~~~~,,,#############\n" +
            "############,,,,,...,,,,,......,,,....,,,......,,,~~~~~~~,,,############\n" +
            "###########,,,,,...,,,,.....,,,,,,....,,,,,,.....,,~~~~~~~,,,###########\n" +
            "##########,,,TTT..,,,,....,,,,,,,,....,,,,,,,,....,,~~~~~~~,,,##########\n" +
            "#########,,,,TTT,,,,....,,,,,,,O,,....,,W,,,,,,,....,~~~~~,,,,,#########\n" +
            "#########,,,,TTT,,,...,,,^,,,OOO,,....,,WWW,,,,,,,...,~~~rr,,,,#########\n" +
            "########,,,,,,,,,,...,,,,,,OOOOOO,....,WWWWWW,,,,,,...,,rrrr,,,,########\n" +
            "#######,,,,,^,,,,..*,,,,,OOOOOOOO,....,WWWWWWWW,^,,,*..,rrrrr,,,,#######\n" +
            "#######,,,,,,,,,...,,^,,,OOOOOOOO,....,WWWWWWWW,,,,,,....rrrr,,,,#######\n" +
            "######,,,,,,,,,,..,,,,,,,,OOOOOOOO....WWWWWWWW,,,^,,,,...,rr,,,^,,######\n" +
            "######,,,nn..,,..,,,,,^,,,OOOOOOOO....WWWWWWWW,,,,,,,,,..,,,,,,,,,######\n" +
            "#####,,,,nnn.....,,,,,,,,,OOOOOO,,....,,WWWWWW,,,,^,,,,...,,,,,,,,,#####\n" +
            "#####,,,nnnnU...,,,,,,,^,,,OOO.J,......,..WWW,,,,,,,,,,,..,,qqqq,,,#####\n" +
            "#####,,,nnn.....,,,DD,,,,,,O,t............,,W,,,,,,II,,,...,,qqq,,,#####\n" +
            "####,,,,,nn....,,,,DDDDD,,,,,...,,....,,...,,,,,IIIII,,,,....qqq,,,,####\n" +
            "####,,,jj..U...,,,DDDDDDDD,,..,,,,....,,,,..,,IIIIIIII,,,...,qqqq,,,####\n" +
            "####,,jjjj....,,,,DDDDDDD,,.+,,,........,,,+.,,IIIIIII,,,,..,,,,,,^,####\n" +
            "####,,jjjj....,,,DDDDDDDD,..,,,..........,,,..,IIIIIIII,,,..,,,,,,,,####\n" +
            "###,,,jjjj....,,,DDDDDDD...,,,....LLLL....,,,...IIIIIII,,H...........###\n" +
            "###.,,,jj.U..,,,DDDDDDDD...,,...LLLLLLLL...,,...IIIIIIII,............###\n" +
            "###...,,.....,,,,,DDDDD,,.,,...LLLLLLLLLL...,,.,,IIIII,,,..aaaaaaaaa.###\n" +
            "###...,,..k..,,,,,,,,DD,..,,...LLLLLLLLLL...,,..,II,,,,,,..a.......a.###\n" +
            "###...........................LLLLLLLLLLLL.................a.......a.###\n" +
            "###...........................LLLLLQQLL@LL.................a.......a.###\n" +
            "###R.....yE.......w...........LLLLLQQLLLLL...............z.a...1...a.###\n" +
            "###...........................LLLLLLLLLLLL.................a.......a.###\n" +
            "###..mmm..U..,,,,,,,,FF,..,,...LLLLLLLLLL...,,..,VV,,,,,,..a.......a.###\n" +
            "###..mmmm....,^,,,FFFFF,,.,,...LLLLLLLLLL...,,.,,VVVVV,,,..a.......a.###\n" +
            "###.,mmmm....,,,FFFFFFFF...,,...LLLLLLLL...,,...VVVVVVVV,..aaaaaaaaa.###\n" +
            "###,,mmmm.....,,,FFFFFFF...,,,....LLLL..A.,,,...VVVVVVV,,............###\n" +
            "####,,mmm.U...,,,FFFFFFFF,..,,,..N..S.P..,,,..,VVVVVVVV,,Z..........####\n" +
            "####,,,,,.....,,,,FFFFFFF,,.+,,,........,,,+.,,VVVVVVV,,,,..,,,,,,,,####\n" +
            "####,,^,,ooo...,,,FFFFFFFF,,..,,,,....,,,,..,,VVVVVVVV,^,...,,,,,,,,####\n" +
            "####,,,,oooo...,,,,FFFFF,,,,,...,,....,,...,,,,,VVVVV,,,,..,,,,^,,,,####\n" +
            "#####,,,,ooo....,,,FF,,,,,,M,,............,,B,,,,,,VV,,,...,,,,,,,,#####\n" +
            "#####,^,,oooo,..,,,,,,,,,,,MMM..,......,..BBB,,,,,,,,,,,.*,,^,,,,,,#####\n" +
            "#####,,,,ooo,,...,,,,,,,^,MMMMMM,,....,,BBBBBB,,,,,,,,,...,,,,,^,,,#####\n" +
            "######,,,,,,,,,..,,^,,,,,,MMMMMMMM....BBBBBBBB,,,,,,^,,..,,,,,,,,,######\n" +
            "######,,,,,,,,,,..,,,,,,,,MMMMMMMM....BBBBBBBB,,,,,,,,..,,,,^,,,,,######\n" +
            "#######,,^,,,,,,...,,,,,,MMMMMMMM,....,BBBBBBBB,,,,,,...,,,,,,,^,#######\n" +
            "#######,,,,,^,,,,..*,,,,,MMMMMMMM,....,BBBBBBBB,^,,,*..,,^,,,,,,,#######\n" +
            "########,,,,,,,,,....,,,,,,MMMMMM,....,BBBBBB,,,,,,...,,,,,,^,,,########\n" +
            "#########,,,,,,,......,,,,,,,MMM,,....,,BBB,,,,,,,.......,,,,,,#########\n" +
            "#########,,,^,,...,,....,,,,,,,M,,....,,B,,,^,,,....&&&&..,,,,,#########\n" +
            "##########,,,,,..,,,,,....,,,,,,,,....,,,,,,,,.....&&&&&&.,,,,##########\n" +
            "###########,,,Y,,,^,,,,.....,,,,,,....,,,,,,.....,.&&&&&&.,,,###########\n" +
            "############,,,,,,,,,^,,,......,,,....,,,...+..,,,.&&&&&&.,,############\n" +
            "#############,,^,,,,,,,ppp...................,,,,,.&&&&&&.,#############\n" +
            "##############,,,,^,,,,ppppp,..............,,,,,,,..&&&&..##############\n" +
            "###############,,,,,,^,ppppp,..............,,,,,^,,......###############\n" +
            "################,,,,,,,ppppp................,,,,,,,,,,,,################\n" +
            "##################,,,,,,,ppp................,^,,,,,,,,##################\n" +
            "###################,,,,,,,,,................,,,,^,,,,###################\n" +
            "#####################,,,^,,,................,,,,,,,#####################\n" +
            "#######################,,,,,,,............,,,,,,,#######################\n" +
            "##########################,,,,,..........,,,,,##########################\n" +
            "##############################,.....G....,##############################\n" +
            "########################################################################\n" +
            "########################################################################\n" +
            "########################################################################\n";

        /// <summary>The round hub with the World 1 gates (world data v13): Tidewatch Docks east ($), Kiln Street south-east (!),
        /// Thornhollow Grove south-west (%), the Hollow Keep north-west (?); Stargazer Wick (u), Merchant Selwyn (x), the Dominion
        /// herald for the finale (&lt;) and quest points: Observatory lenses (l) and cipher wheel (C), the old roster (v), Bazaar stalls (s), the Starfall Crystal by the fountain (().</summary>
        public const string HubMapV13 =
            "########################################################################\n" +
            "########################################################################\n" +
            "########################################################################\n" +
            "##############################,,,,,,,,,,,,##############################\n" +
            "##########################,,,,KKKKKKKKKKKK,,,,##########################\n" +
            "#######################,,,,,,,KKKKKKKKKKKK,,,,,,,#######################\n" +
            "#####################,,,,,,^,,KKKKKKKKKKKK,,,^,,,,,#####################\n" +
            "###################,,,,,,,,,,,KKKKKKKKKKKK,,,,,,^,,,,###################\n" +
            "##################,?.........,KKKKKKKKKKKK,,,,,,,,,^,,##################\n" +
            "################,,,,,,,,...................,,^,,,,,,,,,,################\n" +
            "###############,,,,,,,....,,,,,,,,....,,,,,,,,,,^,X~~~,,,###############\n" +
            "##############,,,,,,....,,,,,,,..........,,,,,,,,,X~~~~,,,##############\n" +
            "#############,,,,,,...,,,,,..................,,,,,~~~~~~,,,#############\n" +
            "############,,,l,...,,,,,......,,,....,,,......,,,~~~~~~~,,,############\n" +
            "###########,,C,,...,,,,.....,,,,,,....,,,,,,.....,,~~~~~~~,,,###########\n" +
            "##########,,,TTTu.,,,,....,,,,,,,,....,,,,,,,,....,,~~~~~~~,,,##########\n" +
            "#########,,,lTTT,,,,....,,,,,,,O,,....,,W,,,,,,,....,~~~~~,,,,,#########\n" +
            "#########,,,,TTT,,,...,,,^,,,OOO,,....,,WWW,,,,,,,...,~~~rr,,,,#########\n" +
            "########,,,,,,l,,,...,,,,,,OOOOOO,....,WWWWWW,,,,,,...,,rrrr,,,,########\n" +
            "#######,,,,,^,,,,..*,,,,,OOOOOOOO,....,WWWWWWWW,^,,,*..,rrrrr,,,,#######\n" +
            "#######,,,,,,,,,...,,^,,,OOOOOOOO,....,WWWWWWWW,,,,,,....rrrr,,,,#######\n" +
            "######,,,,,,,,,,..,,,,,,,,OOOOOOOO....WWWWWWWW,,,^,,,,...,rr,,,^,,######\n" +
            "######,,,nn..,,..,,,,,^,,,OOOOOOOO....WWWWWWWW,,,,,,,,,..,,,,,,,,,######\n" +
            "#####,,,,nnn.....,,,,,,,,,OOOOOO,,....,,WWWWWW,,,,^,,,,...,,,,,,,,,#####\n" +
            "#####,,,nnnnUs..,,,,,,,^,,,OOO.J,......,..WWW,,,,,,,,,,,..,,qqqq,,,#####\n" +
            "#####,,,nnn.....,,,DD,,,,,,O,t............,,W,,,,,,II,,,...,,qqq,,,#####\n" +
            "####,,,,,nn....,,,,DDDDD,,,,,v..,,....,,...,,,,,IIIII,,,,....qqq,,,,####\n" +
            "####,,,jj..Us..,,,DDDDDDDD,,..,,,,....,,,,..,,IIIIIIII,,,...,qqqq,,,####\n" +
            "####,,jjjj....,,,,DDDDDDD,,.+,,,........,,,+.,,IIIIIII,,,,..,,,,,,^,####\n" +
            "####,,jjjj....,,,DDDDDDDD,..,,,..........,,,..,IIIIIIII,,,..,,,,,,,,####\n" +
            "###,,,jjjj....,,,DDDDDDD...,,,....LLLL....,,,...IIIIIII,,H...........###\n" +
            "###.,,,jj.Us.,,,DDDDDDDD...,,...LLLLLLLL...,,...IIIIIIII,............###\n" +
            "###...,,.....,,,,,DDDDD,,.,,...LLLLLLLLLL...,,.,,IIIII,,,..aaaaaaaaa.###\n" +
            "###...,,..k..,,,,,,,,DD,..,,...LLLLLLLLLL...,,..,II,,,,,,..a.......a.###\n" +
            "###...........................LLLLLLLLLLLL.................a.......a.###\n" +
            "###...........................LLLLLQQLL@LL.................a.......a.###\n" +
            "###R.....yE.......w...........LLLLLQQLLLLL...............z.a...1...a$###\n" +
            "###...........................LLLLLL(LLLLL.................a.......a.###\n" +
            "###..mmm..Ux.,,,,,,,,FF,..,,...LLLLLLLLLL...,,..,VV,,,,,,..a.......a.###\n" +
            "###..mmmm....,^,,,FFFFF,,.,,...LLLLLLLLLL...,,.,,VVVVV,,,..a.......a.###\n" +
            "###.,mmmm....,,,FFFFFFFF...,,...LLLLLLLL...,,...VVVVVVVV,..aaaaaaaaa.###\n" +
            "###,,mmmm.....,,,FFFFFFF...,,,....LLLL..A.,,,...VVVVVVV,,............###\n" +
            "####,,mmm.U...,,,FFFFFFFF,..,,,..N..S.P..,,,..,VVVVVVVV,,Z..........####\n" +
            "####,,,,,.....,,,,FFFFFFF,,.+,,,........,,,+.,,VVVVVVV,,,,..,,,,,,,,####\n" +
            "####,,^,,ooo...,,,FFFFFFFF,,..,,,,....,,,,..,,VVVVVVVV,^,...,,,,,,,,####\n" +
            "####,,,,oooo...,,,,FFFFF,,,,,...,,....,,...,,,,,VVVVV,,,,..,,,,^,,,,####\n" +
            "#####,,,,ooo....,,,FF,,,,,,M,,............,,B,,,,,,VV,,,...,,,,,,,,#####\n" +
            "#####,^,,oooo,..,,,,,,,,,,,MMM..,......,..BBB,,,,,,,,,,,.*,,^,,,,,,#####\n" +
            "#####,,,,ooo,,...,,,,,,,^,MMMMMM,,....,,BBBBBB,,,,,,,,,...,,,,,^,,,#####\n" +
            "######,,,,,,,,,..,,^,,,,,,MMMMMMMM....BBBBBBBB,,,,,,^,,..,,,,,,,,,######\n" +
            "######,,,,,,,,,,..,,,,,,,,MMMMMMMM....BBBBBBBB,,,,,,,,..,,,,^,,,,,######\n" +
            "#######,,^,,,,,,...,,,,,,MMMMMMMM,....,BBBBBBBB,,,,,,...,,,,,,,^,#######\n" +
            "#######,,,,,^,,,,..*,,,,,MMMMMMMM,....,BBBBBBBB,^,,,*..,,^,,,,,,,#######\n" +
            "########,,,,,,,,,....,,,,,,MMMMMM,....,BBBBBB,,,,,,...,,,,,,^,,,########\n" +
            "#########,,,,,,,......,,,,,,,MMM,,....,,BBB,,,,,,,.......,,,,,,#########\n" +
            "#########,,,^,,...,,....,,,,,,,M,,....,,B,,,^,,,....&&&&..,,,,,#########\n" +
            "##########,,,,,..,,,,,....,,,,,,,,....,,,,,,,,.....&&&&&&.,,,,##########\n" +
            "###########,,,Y.,,^,,,,.....,,,,,,....,,,,,,.....,.&&&&&&.,,,###########\n" +
            "############,%..,,,,,^,,,......,,,....,,,...+..,,,.&&&&&&..!############\n" +
            "#############,,^,,,,,,,ppp...................,,,,,.&&&&&&.,#############\n" +
            "##############,,,,^,,,,ppppp,..............,,,,,,,..&&&&..##############\n" +
            "###############,,,,,,^,ppppp,..............,,,,,^,,......###############\n" +
            "################,,,,,,,ppppp................,,,,,,,,,,,,################\n" +
            "##################,,,,,,,ppp................,^,,,,,,,,##################\n" +
            "###################,,,,,,,,,................,,,,^,,,,###################\n" +
            "#####################,,,^,,,................,,,,,,,#####################\n" +
            "#######################,,,,,,,........<...,,,,,,,#######################\n" +
            "##########################,,,,,..........,,,,,##########################\n" +
            "##############################,.....G....,##############################\n" +
            "########################################################################\n" +
            "########################################################################\n" +
            "########################################################################\n";

        /// <summary>Height map of the round hub (world data v12).</summary>
        public const string HubHeightsV12 =
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111333333333333111111111111111111111111111111\n" +
            "111111111111111111111111113333333333333333333311111111111111111111111111\n" +
            "111111111111111111111113333333333333333333333333311111111111111111111111\n" +
            "111111111111111111111333333333333333333333333333333111111111111111111111\n" +
            "111111111111111111133333333333333333333333333333333331111111111111111111\n" +
            "111111111111111111333333333333333333333333333333333333111111111111111111\n" +
            "111111111111111144333333333333333333333333333333333333111111111111111111\n" +
            "111111111111111444433333333333222222222222333333333000111111111111111111\n" +
            "111111111111114444433333333222211111111112222333333000011111111111111111\n" +
            "111111111111144444443333222111111111111111111222330000001111111111111111\n" +
            "111111111111444444444322211111111111111111111112220000000111111111111111\n" +
            "111111111114444444441221111111111111111111111111122000000011111111111111\n" +
            "111111111144444444111111111111111111111111111111111100000001111111111111\n" +
            "111111111444444441111111111111111111111111111111111110000011111111111111\n" +
            "111111111444444411111111111111111111111111111111111111000111111111111111\n" +
            "111111114444444111111111111111111111111111111111111111111111111111111111\n" +
            "111111144444444111111111111111111111111111111111111111111111111111111111\n" +
            "111111144444441111111111111111111111111111111111111111111111111111111111\n" +
            "111111111444411111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111110000000011111111111111111111111111111111\n" +
            "111111111111111111111111111111100000000001111111111111111111111111111111\n" +
            "111111111111111111111111111111000000000000111111111111111111111111111111\n" +
            "111111111111111111111111111110000000000000011111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111110000000000000011111111111111111111111111111\n" +
            "111111111111111111111111111111000000000000111111111111111111111111111111\n" +
            "111111111111111111111111111111100000000001111111111111111111111111111111\n" +
            "111111111111111111111111111111110000000011111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111112221111111111111111111111111111111111111111111111111111111111111\n" +
            "111111222221111111111111111111111111111111111111111111111111111111111111\n" +
            "111111122222111111111111111111111111111111111111111111111111111111111111\n" +
            "111111122222211111111111111111111111111111111111111111111111111111111111\n" +
            "111111112222221111111111111111111111111111111111111111111111111111111111\n" +
            "111111111222221111111111111111111111111111111111111112222111111111111111\n" +
            "111111111222222111111111111111111111111111111111111122222211111111111111\n" +
            "111111111122222211111111111111111111111111111111112222222211111111111111\n" +
            "111111111112222221111111111111111111111111111111112222222211111111111111\n" +
            "111111111111222222211111111111111111111111111111112222222211111111111111\n" +
            "111111111111122222221111111111111111111111111111112222222211111111111111\n" +
            "111111111111112222222111111111111111111111111111112222222211111111111111\n" +
            "111111111111111222222221111111111111111111111111111222222111111111111111\n" +
            "111111111111111122222221111111111111111111111111111111111111111111111111\n" +
            "111111111111111111222221111111111111111111111111111111111111111111111111\n" +
            "111111111111111111122211111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111211111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n";

        /// <summary>The round hub as world data v11 made it (both gates at the south), before the tunnels.</summary>
        public const string HubMapV11 =
            "########################################################################\n" +
            "########################################################################\n" +
            "########################################################################\n" +
            "##############################,,,,,,,,,,,,##############################\n" +
            "##########################,,,,KKKKKKKKKKKK,,,,##########################\n" +
            "#######################,,,,,,,KKKKKKKKKKKK,,,,,,,#######################\n" +
            "#####################,,,,,,^,,KKKKKKKKKKKK,,,^,,,,,#####################\n" +
            "###################,,,,,,,,,,,KKKKKKKKKKKK,,,,,,^,,,,###################\n" +
            "##################,,,,,,,,...,KKKKKKKKKKKK,,,,,,,,,^,,##################\n" +
            "################,,,,,,,,...................,,^,,,,,,,,,,################\n" +
            "###############,,,,,,,....,,,,,,,,....,,,,,,,,,,^,X~~~,,,###############\n" +
            "##############,,,,,,....,,,,,,,..........,,,,,,,,,X~~~~,,,##############\n" +
            "#############,,,,,,...,,,,,..................,,,,,~~~~~~,,,#############\n" +
            "############,,,,,...,,,,,......,,,....,,,......,,,~~~~~~~,,,############\n" +
            "###########,,,,,...,,,,.....,,,,,,....,,,,,,.....,,~~~~~~~,,,###########\n" +
            "##########,,,TTT..,,,,....,,,,,,,,....,,,,,,,,....,,~~~~~~~,,,##########\n" +
            "#########,,,,TTT,,,,....,,,,,,,O,,....,,W,,,,,,,....,~~~~~,,,,,#########\n" +
            "#########,,,,TTT,,,...,,,^,,,OOO,,....,,WWW,,,,,,,...,~~~rr,,,,#########\n" +
            "########,,,,,,,,,,...,,,,,,OOOOOO,....,WWWWWW,,,,,,...,,rrrr,,,,########\n" +
            "#######,,,,,^,,,,..*,,,,,OOOOOOOO,....,WWWWWWWW,^,,,*..,rrrrr,,,,#######\n" +
            "#######,,,,,,,,,...,,^,,,OOOOOOOO,....,WWWWWWWW,,,,,,....rrrr,,,,#######\n" +
            "######,,,^,,,,,,..,,,,,,,,OOOOOOOO....WWWWWWWW,,,^,,,,...,rr,,,^,,######\n" +
            "######,,,,,..,,..,,,,,^,,,OOOOOOOO....WWWWWWWW,,,,,,,,,..,,,,,,,,,######\n" +
            "#####,,,nn,......,,,,,,,,,OOOOOO,,....,,WWWWWW,,,,^,,,,...,,,,,,,,,#####\n" +
            "#####,,nnnn.U...,,,,,,,^,,,OOO.J,......,..WWW,,,,,,,,,,,..,,qqqq,,,#####\n" +
            "#####,,nnnn.....,,,DD,,,,,,O,t............,,W,,,,,,II,,,...,,qqq,,,#####\n" +
            "####,,,nnnn....,,,,DDDDD,,,,,...,,....,,...,,,,,IIIII,,,,....qqq,,,,####\n" +
            "####,,,,nn.U...,,,DDDDDDDD,,..,,,,....,,,,..,,IIIIIIII,,,...,qqqq,,,####\n" +
            "####,,,,,.....,,,,DDDDDDD,,.+,,,........,,,+.,,IIIIIII,,,,..,,,,,,^,####\n" +
            "####,,^,,k....,,,DDDDDDDD,..,,,..........,,,..,IIIIIIII,,,..,,,,,,,,####\n" +
            "###,,,,,,.....,,,DDDDDDD...,,,....LLLL....,,,...IIIIIII,,H...........###\n" +
            "###,,,,,..U..,,,DDDDDDDD...,,...LLLLLLLL...,,...IIIIIIII,............###\n" +
            "###,,jjjj....,,,,,DDDDD,,.,,...LLLLLLLLLL...,,.,,IIIII,,,..aaaaaaaaa.###\n" +
            "###,,jjjj....,,,,,,,,DD,..,,...LLLLLLLLLL...,,..,II,,,,,,..a.......a.###\n" +
            "###,,jjjj.....................LLLLLLLLLLLL.................a.......a.###\n" +
            "###,,jjjj.....................LLLLLQQLL@LL.................a.......a.###\n" +
            "###,,,,,.yE.......w...........LLLLLQQLLLLL...............z.a...1...a.###\n" +
            "###,,,,,......................LLLLLLLLLLLL.................a.......a.###\n" +
            "###,,,^,..U..,,,,,,,,FF,..,,...LLLLLLLLLL...,,..,VV,,,,,,..a.......a.###\n" +
            "###,,,,,.....,^,,,FFFFF,,.,,...LLLLLLLLLL...,,.,,VVVVV,,,..a.......a.###\n" +
            "###,,,mmm....,,,FFFFFFFF...,,...LLLLLLLL...,,...VVVVVVVV,..aaaaaaaaa.###\n" +
            "###,,mmmm.....,,,FFFFFFF...,,,....LLLL..A.,,,...VVVVVVV,,............###\n" +
            "####,,mmm.U...,,,FFFFFFFF,..,,,..N..S.P..,,,..,VVVVVVVV,,Z..........####\n" +
            "####,,mmmm....,,,,FFFFFFF,,.+,,,........,,,+.,,VVVVVVV,,,,..,,,,,,,,####\n" +
            "####,,mmmooo...,,,FFFFFFFF,,..,,,,....,,,,..,,VVVVVVVV,^,...,,,,,,,,####\n" +
            "####,,,,oooo...,,,,FFFFF,,,,,...,,....,,...,,,,,VVVVV,,,,..,,,,^,,,,####\n" +
            "#####,,,,ooo....,,,FF,,,,,,M,,............,,B,,,,,,VV,,,...,,,,,,,,#####\n" +
            "#####,^,,oooo,..,,,,,,,,,,,MMM..,......,..BBB,,,,,,,,,,,.*,,^,,,,,,#####\n" +
            "#####,,,,ooo,,...,,,,,,,^,MMMMMM,,....,,BBBBBB,,,,,,,,,...,,,,,^,,,#####\n" +
            "######,,,,,,,,,..,,^,,,,,,MMMMMMMM....BBBBBBBB,,,,,,^,,..,,,,,,,,,######\n" +
            "######,,,,,,,,,,..,,,,,,,,MMMMMMMM....BBBBBBBB,,,,,,,,..,,,,^,,,,,######\n" +
            "#######,,^,,,,,,...,,,,,,MMMMMMMM,....,BBBBBBBB,,,,,,...,,,,,,,^,#######\n" +
            "#######,,,,,^,,,,..*,,,,,MMMMMMMM,....,BBBBBBBB,^,,,*..,,^,,,,,,,#######\n" +
            "########,,,,,,,,,....,,,,,,MMMMMM,....,BBBBBB,,,,,,...,,,,,,^,,,########\n" +
            "#########,,,,,,,......,,,,,,,MMM,,....,,BBB,,,,,,,.......,,,,,,#########\n" +
            "#########,,,^,,...,,....,,,,,,,M,,....,,B,,,^,,,....&&&&..,,,,,#########\n" +
            "##########,,,,,..,,,,,....,,,,,,,,....,,,,,,,,.....&&&&&&.,,,,##########\n" +
            "###########,,,Y,,,^,,,,.....,,,,,,....,,,,,,.....,.&&&&&&.,,,###########\n" +
            "############,,,,,,,,,^,,,......,,,....,,,...+..,,,.&&&&&&.,,############\n" +
            "#############,,^,,,,,,,ppp...................,,,,,.&&&&&&.,#############\n" +
            "##############,,,,^,,,,ppppp,..............,,,,,,,..&&&&..##############\n" +
            "###############,,,,,,^,ppppp,..............,,,,,^,,......###############\n" +
            "################,,,,,,,ppppp................,,,,,,,,,,,,################\n" +
            "##################,,,,,,,ppp................,^,,,,,,,,##################\n" +
            "###################,,,,,,,,,................,,,,^,,,,###################\n" +
            "#####################,,,^,,,................,,,,,,,#####################\n" +
            "#######################,,,,....R........G....,,,,#######################\n" +
            "##########################,..................,##########################\n" +
            "##############################,,........,,##############################\n" +
            "########################################################################\n" +
            "########################################################################\n" +
            "########################################################################\n";

        /// <summary>Height map of the round hub (world data v11).</summary>
        public const string HubHeightsV11 =
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111333333333333111111111111111111111111111111\n" +
            "111111111111111111111111113333333333333333333311111111111111111111111111\n" +
            "111111111111111111111113333333333333333333333333311111111111111111111111\n" +
            "111111111111111111111333333333333333333333333333333111111111111111111111\n" +
            "111111111111111111133333333333333333333333333333333331111111111111111111\n" +
            "111111111111111111333333333333333333333333333333333333111111111111111111\n" +
            "111111111111111144333333333333333333333333333333333333111111111111111111\n" +
            "111111111111111444433333333333222222222222333333333000111111111111111111\n" +
            "111111111111114444433333333222211111111112222333333000011111111111111111\n" +
            "111111111111144444443333222111111111111111111222330000001111111111111111\n" +
            "111111111111444444444322211111111111111111111112220000000111111111111111\n" +
            "111111111114444444441221111111111111111111111111122000000011111111111111\n" +
            "111111111144444444111111111111111111111111111111111100000001111111111111\n" +
            "111111111444444441111111111111111111111111111111111110000011111111111111\n" +
            "111111111444444411111111111111111111111111111111111111000111111111111111\n" +
            "111111114444444111111111111111111111111111111111111111111111111111111111\n" +
            "111111144444444111111111111111111111111111111111111111111111111111111111\n" +
            "111111144444441111111111111111111111111111111111111111111111111111111111\n" +
            "111111111444411111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111110000000011111111111111111111111111111111\n" +
            "111111111111111111111111111111100000000001111111111111111111111111111111\n" +
            "111111111111111111111111111111000000000000111111111111111111111111111111\n" +
            "111111111111111111111111111110000000000000011111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111100000000000000001111111111111111111111111111\n" +
            "111111111111111111111111111110000000000000011111111111111111111111111111\n" +
            "111111111111111111111111111111000000000000111111111111111111111111111111\n" +
            "111111111111111111111111111111100000000001111111111111111111111111111111\n" +
            "111111111111111111111111111111110000000011111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111112221111111111111111111111111111111111111111111111111111111111111\n" +
            "111111222221111111111111111111111111111111111111111111111111111111111111\n" +
            "111111122222111111111111111111111111111111111111111111111111111111111111\n" +
            "111111122222211111111111111111111111111111111111111111111111111111111111\n" +
            "111111112222221111111111111111111111111111111111111111111111111111111111\n" +
            "111111111222221111111111111111111111111111111111111112222111111111111111\n" +
            "111111111222222111111111111111111111111111111111111122222211111111111111\n" +
            "111111111122222211111111111111111111111111111111112222222211111111111111\n" +
            "111111111112222221111111111111111111111111111111112222222211111111111111\n" +
            "111111111111222222211111111111111111111111111111112222222211111111111111\n" +
            "111111111111122222221111111111111111111111111111112222222211111111111111\n" +
            "111111111111112222222111111111111111111111111111112222222211111111111111\n" +
            "111111111111111222222221111111111111111111111111111222222111111111111111\n" +
            "111111111111111122222221111111111111111111111111111111111111111111111111\n" +
            "111111111111111111222221111111111111111111111111111111111111111111111111\n" +
            "111111111111111111122211111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111211111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n" +
            "111111111111111111111111111111111111111111111111111111111111111111111111\n";

        /// <summary>The hub map as world data v10 made it (the Crystal Tap in the square hub), before the round hub.</summary>
        public const string HubMapV10 =
            "##################################################\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,^TT,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,,,^,,,#\n" +
            "#,,TT,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,^,,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,^,,,^,#\n" +
            "#,,..,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,..,,,,,,...,,,,,,,KKKKKKKK,,,...,,,...,,,,,,,,#\n" +
            "#,,......................................,,,,,,,,#\n" +
            "#,,,,,,,,.......XX.......................,,,,,,,,#\n" +
            "#,FFFFF,,.,,,~~~~~~~~,,,..,,,,,,,,,,,,,,.,,VVVVV,#\n" +
            "#,FFFFF...,,,~~~~~~~~,,,..........Y,^,,,...VVVVV,#\n" +
            "#,FFFFF...,,,~~~~~~~~^,,..,,,,,,,,,,,,,,...VVVVV,#\n" +
            "#,FFFFF,,.,w,,,,,,,,,,,,..,,,,,,,^,,,,,,.,,VVVVV,#\n" +
            "#,,,,,,,,.,,,,^,,,,,,,,,..,,,,,,,,,,,,,,.,,,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,.,mmm,,,,,,,,,..,,,,,,,,,jjj,.,,,,,,,,,#\n" +
            "#,,,,,,,,,.,mmm,,,,^,,,,..,,,,^,,,,jjj,.,,,,,,,,,#\n" +
            "#,IIIII,,,.,,^,,,,,,,,,,..,,,,,,,,,,^,,.,,,DDDDD,#\n" +
            "#,IIIII..,.,,,,,,,..............,,,,,,,....DDDDD,#\n" +
            "#,IIIII..,.,,,,,^,...LLLLLLLL...,^,,,,,....DDDDD,#\n" +
            "#,IIIII,,,.,,,,,,,..CLLLLLLLLP..,,,,,,,.,,,DDDDD,#\n" +
            "#,nnn,,,,,.,,,,,,,...LLLLLLLL...,,,,,,,.,,,,,,,,,#\n" +
            "#,nnn,,,,,.,,,,,,,...LLLQQL@L...,,,,,,,.,,,,,,,,,#\n" +
            "#,...,...............LLLQQLLL...,...............,#\n" +
            "#,,,,,,,,,....E.k....LLLLLLLL...,.....aaaaaaa...,#\n" +
            "#,,U,,U,,,...........ALLLLLLN...,..H..a.....a...,#\n" +
            "#,,..............y...LLLLLLLL...,z....a..1..a...,#\n" +
            "#,,.....................S.......,.....a.....a...,#\n" +
            "#,,U,,U,,,......................,.....aaaaaaa...,#\n" +
            "#,,,,,,,,,..U..U..,,,,,,..,,,,,,,...............,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,.....,,,,,,,,,,,..,,,,,,,,,,,,,,,,,,..,,,#\n" +
            "#,OOOOOO.....,,,,,,,====..====,,,,,,,,,,^,......,#\n" +
            "#,OOOOOO...t.,,,,,,,====..====,,,,,^,,,,,,......,#\n" +
            "#,OOOOOO.J...,,,,,,^,,,,.....,,,,,,,,,,,,,...Z..,#\n" +
            "#,OOOOOO.....,,,,,,,,,,,..,,,,,,,,,,,^,,,,......,#\n" +
            "#,OOOOOO.....,,,,,,,,^,,..,,,,^,,,ppppp,,,......,#\n" +
            "#,,,,,,,.....,ooo^qqq,,,..,,,,,,^,ppppp,rrr,,,,,,#\n" +
            "#,,,,,,,,,,,,,ooo,qqq,,,..,,,,,,,,ppppp,rrr,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,,,.R.,,,,,,,,,..,,,,,,,,,.G.,,,,,,,,,,,#\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "##################################################\n";

        /// <summary>The hub map as world data v9 made it (before the round tavern).</summary>
        public const string HubMapV9 =
            "##################################################\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,^TT,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,,,^,,,#\n" +
            "#,,TT,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,^,,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,^,,,^,#\n" +
            "#,,..,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,..,,,,,,...,,,,,,,KKKKKKKK,,,...,,,...,,,,,,,,#\n" +
            "#,,......................................,,,,,,,,#\n" +
            "#,,,,,,,,.......XX.......................,,,,,,,,#\n" +
            "#,FFFFF,,.,,,~~~~~~~~,,,..,,,,,,,,,,,,,,.,,VVVVV,#\n" +
            "#,FFFFF...,,,~~~~~~~~,,,..........Y,^,,,...VVVVV,#\n" +
            "#,FFFFF...,,,~~~~~~~~^,,..,,,,,,,,,,,,,,...VVVVV,#\n" +
            "#,FFFFF,,.,w,,,,,,,,,,,,..,,,,,,,^,,,,,,.,,VVVVV,#\n" +
            "#,,,,,,,,.,,,,^,,,,,,,,,..,,,,,,,,,,,,,,.,,,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,.,mmm,,,,,,,,,..,,,,,,,,,jjj,.,,,,,,,,,#\n" +
            "#,,,,,,,,,.,mmm,,,,^,,,,..,,,,^,,,,jjj,.,,,,,,,,,#\n" +
            "#,IIIII,,,.,,^,,,,,,,,,,..,,,,,,,,,,^,,.,,,DDDDD,#\n" +
            "#,IIIII..,.,,,,,,,..............,,,,,,,....DDDDD,#\n" +
            "#,IIIII..,.,,,,,^,..............,^,,,,,....DDDDD,#\n" +
            "#,IIIII,,,.,,,,,,,..C........P..,,,,,,,.,,,DDDDD,#\n" +
            "#,nnn,,,,,.,,,,,,,.....QQQQ.....,,,,,,,.,,,,,,,,,#\n" +
            "#,nnn,,,,,.,,,,,,,.....QQQQ.....,,,,,,,.,,,,,,,,,#\n" +
            "#,...,.................QQQQ.....,...............,#\n" +
            "#,,,,,,,,,....E.k......QQQQ.....,.....aaaaaaa...,#\n" +
            "#,,U,,U,,,...........A......N...,..H..a.....a...,#\n" +
            "#,,..............y..............,z....a..1..a...,#\n" +
            "#,,.....................S.......,.....a.....a...,#\n" +
            "#,,U,,U,,,......................,.....aaaaaaa...,#\n" +
            "#,,,,,,,,,..U..U..,,,,,,..,,,,,,,...............,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,.....,,,,,,,,,,,..,,,,,,,,,,,,,,,,,,..,,,#\n" +
            "#,OOOOOO.....,,,,,,,====..====,,,,,,,,,,^,......,#\n" +
            "#,OOOOOO...t.,,,,,,,====..====,,,,,^,,,,,,......,#\n" +
            "#,OOOOOO.J...,,,,,,^,,,,.....,,,,,,,,,,,,,...Z..,#\n" +
            "#,OOOOOO.....,,,,,,,,,,,..,,,,,,,,,,,^,,,,......,#\n" +
            "#,OOOOOO.....,,,,,,,,^,,..,,,,^,,,ppppp,,,......,#\n" +
            "#,,,,,,,.....,ooo^qqq,,,..,,,,,,^,ppppp,rrr,,,,,,#\n" +
            "#,,,,,,,,,,,,,ooo,qqq,,,..,,,,,,,,ppppp,rrr,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,,,.R.,,,,,,,,,..,,,,,,,,,.G.,,,,,,,,,,,#\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "##################################################\n";

        /// <summary>The hub map and height map as world data v8 made them (a hub still holding exactly these gets the v9 layout).</summary>
        public const string HubMapV8 =
            "##################################################\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "#,^TT,,,,,WWWWW^^^^^,KKKKKKKK,,BBBBB,MMMMM,,,^,,,#\n" +
            "#,,TT,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,^,,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,^,,,^,#\n" +
            "#,,..,,,,,WWWWW^,,^^,KKKKKKKK,,BBBBB,MMMMM,,,,,,,#\n" +
            "#,,..,,,,,,...,,,,,,,KKKKKKKK,,,...,,,...,,,,,,,,#\n" +
            "#,,......................................,,,,,,,,#\n" +
            "#,,,,,,,,.......XX.......................,,,,,,,,#\n" +
            "#,FFFFF,,.,,,~~~~~~~~,,,..,,,,,,,,,,,,,,.,,VVVVV,#\n" +
            "#,FFFFF...,,,~~~~~~~~,,,..........Y,^,,,...VVVVV,#\n" +
            "#,FFFFF...,,,~~~~~~~~^,,..,,,,,,,,,,,,,,...VVVVV,#\n" +
            "#,FFFFF,,.,w,,,,,,,,,,,,..,,,,,,,^,,,,,,.,,VVVVV,#\n" +
            "#,,,,,,,,.,,,,^,,,,,,,,,..,,,,,,,,,,,,,,.,,,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,.,mmm,,,,,,,,,..,,,,,,,,,jjj,.,,,,,,,,,#\n" +
            "#,,,,,,,,,.,mmm,,,,^,,,,..,,,,^,,,,jjj,.,,,,,,,,,#\n" +
            "#,IIIII,,,.,,^,,,,,,,,,,..,,,,,,,,,,^,,.,,,DDDDD,#\n" +
            "#,IIIII..,.,,,,,,,..............,,,,,,,....DDDDD,#\n" +
            "#,IIIII..,.,,,,,^,..............,^,,,,,....DDDDD,#\n" +
            "#,IIIII,,,.,,,,,,,..C........P..,,,,,,,.,,,DDDDD,#\n" +
            "#,nnn,,,,,.,,,,,,,.....QQQQ.....,,,,,,,.,,,,,,,,,#\n" +
            "#,nnn,,,,,.,,,,,,,.....QQQQ.....,,,,,,,.,,,,,,,,,#\n" +
            "#,...,.................QQQQ.....,...............,#\n" +
            "#,,,,,,,,,....E.k......QQQQ.....,.....aaaaaaa...,#\n" +
            "#,,U,,U,,,......................,..H..a.....a...,#\n" +
            "#,,..............y...A......N...,z....a..1..a...,#\n" +
            "#,,.....................S.......,.....a.....a...,#\n" +
            "#,,U,,U,,,......................,.....aaaaaaa...,#\n" +
            "#,,,,,,,,,..U..U..,,,,,,..,,,,,,,...............,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,.....,,,,,,,,,,,..,,,,,,,,,,,,,,,,,,..,,,#\n" +
            "#,OOOOOO.....,,,,,,,====..====,,,,,,,,,,^,......,#\n" +
            "#,OOOOOO...t.,,,,,,,====..====,,,,,^,,,,,,......,#\n" +
            "#,OOOOOO.J...,,,,,,^,,,,.....,,,,,,,,,,,,,...Z..,#\n" +
            "#,OOOOOO.....,,,,,,,,,,,..,,,,,,,,,,,^,,,,......,#\n" +
            "#,OOOOOO.....,,,,,,,,^,,..,,,,^,,,ppppp,,,......,#\n" +
            "#,,,,,,,.....,ooo^qqq,,,..,,,,,,^,ppppp,rrr,,,,,,#\n" +
            "#,,,,,,,,,,,,,ooo,qqq,,,..,,,,,,,,ppppp,rrr,,,,,,#\n" +
            "#,..............................................,#\n" +
            "#,..............................................,#\n" +
            "#,,,,,,,,,,,.R.,,,,,,,,,..,,,,,,,,,.G.,,,,,,,,,,,#\n" +
            "#,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,#\n" +
            "##################################################\n";
        public const string HubHeightsV8 =
            "00000000000000000000000000000000000000000000000000\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "02222222222222222222222222222222222222222222222220\n" +
            "0000000000000bbbbbbbb00000000000000000000000000000\n" +
            "0000000000000bbbbbbbb00000000000000000000000000000\n" +
            "0000000000000bbbbbbbb00000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "000000000000000000aaaaaaaaaaaaaa000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "01111111111110000000000000111111111111100111111110\n" +
            "01111111111110000000000000111111111111100111111110\n" +
            "01111111111110000000000000111111111111100111111110\n" +
            "01111111111110000000000000111111111111100111111110\n" +
            "01111111111110000000000000111111111111100111111110\n" +
            "01111111111110000000000000000000000000000111111110\n" +
            "01111111111110000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n" +
            "00000000000000000000000000000000000000000000000000\n";

        /// <summary>The hub map as world data v2-v7 made it (a hub still holding exactly this gets the v8 layout).</summary>
        public const string HubMapV2 =
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
            z.textureStyle = ZoneTextureStyle.Garden;
            z.zoneKey = "SunpetalGardens";
            z.displayName = "Sunpetal Gardens";
            z.levelRange = "Lv 1-3";
            z.height = 700f;
            z.cellSize = 8f;
            z.map =
            "#############################\n" +
            "#^,,,,,,,,,,,^,,,,,,,,,,,,,^#\n" +
            "#,==========,,,,==========,,#\n" +
            "#,=,,,,,,,,=.....=ccccccc=,,#\n" +
            "#,=,ddd,,,,=..Y..=c,....c=,,#\n" +
            "#,=,d4d,,,,=..X..=c,....c=,,#\n" +
            "#,=,ddd,,,,.......c,.3..c=,,#\n" +
            "#,=,,,,,,,,=.....=c,....c=,,#\n" +
            "#,=====.====..=..=c,....c=,,#\n" +
            "#,,,,,,.,,,,..=..=ccccccc=,,#\n" +
            "#,^,,,,.,,,,..=..====.====,,#\n" +
            "#,,.........2...............#\n" +
            "#,,bbbbbbbbbbbbbbbbbbbbbbb..#\n" +
            "#,,b,,,,,,=,,,.,,,=,,,,2,b,,#\n" +
            "#^,b,,^,,,=,,,.,,,=,,^,,,b,^#\n" +
            "#,,b,,,,,,=,,,.,,,=,,,,,,b,,#\n" +
            "#,,b,,,,,,=====.====,,,,,b,,#\n" +
            "#,2b.....................b,,#\n" +
            "#,,b,,,,,,,,,,,2,,,,,,,,.b,,#\n" +
            "#,,b,,,,^,,,,.....,,,,^,2b,,#\n" +
            "#,,b,,,,,,,,,.....,,,,,,.b,,#\n" +
            "#,,b,,,,2,,,,,,.,,,2,,,,.b,,#\n" +
            "#,,bbbbbbbbbbbbbbbbbbbbbbb..#\n" +
            "#,,.........................#\n" +
            "#,,,,,,==,==,1,.,,,==,==,,,,#\n" +
            "#,^,,,,=,,,=,,,.,,,=,,,=,,^,#\n" +
            "#,,,,,,=,,,aaaaaaaa=,,,=,,,,#\n" +
            "#,,,,,,=,,,a,,,,,,a=,,,=,,,,#\n" +
            "#,,,,,,=,,,a,,1,,,a=,,,=,,,,#\n" +
            "#,,,,,,=,,,a,,,,,,a=,,,=,,,,#\n" +
            "#,,,,,,====a......a====,,,,,#\n" +
            "#,aaaaaaaaaa,,.,,,aaaaaaaaa,#\n" +
            "#,,^,,,1,,,,M..,,,1,,,^,,,,,#\n" +
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
            // A bright morning over the gardens.
            z.skyTopColor = new Color(0.34f, 0.55f, 0.96f);
            z.skyHorizonColor = new Color(0.98f, 0.86f, 0.7f);
            z.fogColor = new Color(0.88f, 0.84f, 0.78f);
            z.horizonDistance = 1000f;
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
                new ZoneEncounter { ringSymbol = "1", key = "Sprouts", roster = new List<int> { 9, 9 }, wanderers = 4, patrolSpeed = 1f, respawnSeconds = 20, evenFight = true },
                new ZoneEncounter { ringSymbol = "2", key = "Newts", roster = new List<int> { 8, 8 }, wanderers = 6, patrolSpeed = 1.1f, respawnSeconds = 20, evenFight = true },
                new ZoneEncounter { ringSymbol = "3", key = "Moths", roster = new List<int> { 10, 9 }, wanderers = 3, patrolSpeed = 1.3f, respawnSeconds = 20, evenFight = true },
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
            ql.storylineName = "A Castle Tour";
            ql.storylineEnd = 12;
            ql.quests = new List<QuestEntry>
            {
                new QuestEntry
                {
                    saveId = 8, title = "Finding Your Way", prerequisite = QuestManager.QUEST_PROPHECY, minLevel = 1, xp = 50, gold = 20,
                    description = "Gatekeeper Bramwell keeps watch over Starfall Castle. He wants to show you around the grounds before you go anywhere dangerous.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Bramwell", "Meet Gatekeeper Bramwell outside the Crystal Tap", "Gatekeeper Bramwell",
                            "So you're the Headmaster's new star! Welcome, welcome.",
                            "I stood on these walls in the last war, out at the Everweave's edge. Quiet times since. Mostly.",
                            "Before anyone lets you near a monster, you ought to know your way round the castle.",
                            "Have a look at the School Green down the west avenue, the Bazaar past it, and the Training Grounds to the east. Then come back and tell me what you saw."),
                        Point(QuestStepKind.Explore, "SchoolGreen", 1, "Visit the School Green, down the west avenue", "School Green"),
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
                            "Your backpack holds everything you carry: hats, robes, boots, wands, athames, amulets, rings and decks. Press B, or click the book on the pedestal by the tavern door.",
                            "Open the supply crate beside my stall. There's a healing potion in there with your name on it."),
                        Point(QuestStepKind.Interact, "SupplyCrate", 1, "Open the supply crate beside Pell's stall", "Supply crate"),
                        Talk("Pell", "Tell Quartermaster Pell what you found", "Quartermaster Pell",
                            "That's the one. Drink a potion from your backpack to heal, or use one as your turn in a duel.",
                            "Gear you equip makes you tougher and your spells stronger. Better gear drops from monsters and chests.",
                            "Now, Librarian Wren is expecting you at the Library, north-east of here among the school halls."),
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
                            "The Headmaster's asked me to drill the castle watch as well. Says he wants us ready. For what, he won't say.",
                            "Take this athame; it'll steady your aim. I'll tell the Gatekeeper you're ready for the gardens."),
                    },
                },
                new QuestEntry
                {
                    saveId = 12, title = "Out Into the World", prerequisite = 11, minLevel = 1, xp = 50, gold = 25,
                    description = "Gatekeeper Bramwell has raised the portcullis on the tunnel to Sunpetal Gardens. The weeds out there have come alive, and the old gardener needs a hand.",
                    steps = new List<QuestStepEntry>
                    {
                        Talk("Bramwell", "Talk to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                            "Holloway says you held your own. I've raised the portcullis on the Sunpetal Gardens tunnel, at the end of the south avenue.",
                            "The weeds have got up and started walking, and Gardener Moss is at his wit's end. Go and help him.",
                            "Gardens first, mind. Lantern Row stays shut until you've proven yourself out there."),
                        Talk("Moss", "Find Gardener Moss in Sunpetal Gardens (tunnel at the end of the south avenue)", "Gardener Moss",
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
            ql.storylineName = "The Briar Heart";
            ql.storylineEnd = 15;
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
                            "And look what I dug out of the briars: an iron stake, driven deep, stamped with a gauntlet closing on a star. That's no gardener's mark.",
                            "You've done the castle proud. I'll send word to the Headmaster, and Bramwell will open the Lantern Row tunnel for you.",
                            "Mind yourself out there. Whatever woke Briarwick isn't finished yet."),
                    },
                },
            };
        }

        // ------------------------------------------------------------------ World 1: Starfall Isle

        public const string World1Goal =
            "The Starfall Crystal is fading. Find out what is draining its light across Starfall Isle, and rekindle it.";

        /// <summary>
        /// World 1. The Headmaster sets the goal out in The Prophecy; every storyline (the castle tour, Sunpetal Gardens,
        /// Lantern Row, and the streets still to come) leads to the finale, which needs all of them.
        /// </summary>
        public static void FillWorld1(WorldArc w, List<Questline> storylines)
        {
            w.includeInBuild = true;
            w.order = 1;
            w.displayName = "Starfall Isle";
            w.levelRange = "Lv 1-13 (ends around Lv 10-15)";
            w.goal = World1Goal;
            w.goalQuest = QuestManager.QUEST_PROPHECY;
            w.finaleQuest = 16;
            w.storylines = new List<Questline>(storylines);
            w.zones = new List<string> { "StarfallCastle", "SunpetalGardens", "LanternRow", "TidewatchDocks", "KilnStreet", "ThornhollowGrove", "HollowKeep" };
        }

        /// <summary>The World 1 finale (save id 16). The builder adds every storyline's end to its requirements.</summary>
        public static void FillWorld1Finale(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Finale: Rekindle the Crystal";
            ql.quests = new List<QuestEntry> { World1FinaleQuest() };
        }

        /// <summary>
        /// Quest 16: the crystal is rekindled, a Dominion herald brings the Grand General's proclamation under a white flag,
        /// and the first arch in the portal room wakes (World 2's).
        /// </summary>
        public static QuestEntry World1FinaleQuest()
        {
            return new QuestEntry
            {
                saveId = 16, title = "Rekindle the Starfall Crystal", prerequisite = QuestManager.QUEST_PROPHECY, minLevel = 1, xp = 600, gold = 250,
                description = "Every corner of Starfall Isle has been set right, and the Starfall Crystal is ready to burn again. Gatekeeper Bramwell and the Headmaster want to see you.",
                steps = new List<QuestStepEntry>
                {
                    Talk("Bramwell", "Talk to Gatekeeper Bramwell outside the Crystal Tap", "Gatekeeper Bramwell",
                        "Can you feel it? The crystal hasn't burned this bright in months.",
                        "The docks, the forges, the grove, the keep... you've had a hand in all of it. Go on. Put your hand on the crystal."),
                    Point(QuestStepKind.Interact, "StarfallCrystal", 1, "Rekindle the Starfall Crystal in the Crystal Tap", "The Starfall Crystal"),
                    Talk("Herald", "Meet the stranger at the south gate", "Dominion Herald",
                        "Wizard of Starfall. I bring the word of the Grand General of the Dominion, under a white flag.",
                        "'The Everweave will be unified. Every world will burn as one light, under one banner. Starfall Isle has been noted.'",
                        "That is all. I am to return now. We will meet again, I think, on worse terms."),
                    Talk("Headmaster", "Report to Headmaster Greymantle in his study", "Headmaster Greymantle",
                        "The Dominion. So they have come back, as I feared they would.",
                        "The crystal burns again, and in its light the first of the old arches has woken. The way to the next world is open.",
                        "When you are ready, it waits in the portal room beneath my tower. And so, I am afraid, does a great deal more."),
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
