using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// World 2, Dustwind (see the World 2 plan): the town of Archrock and six streets (Tumbleweed Row, Coyote Gulch,
    /// Copperbottom Mine, the Iron Line Railyard, Thunder Mesa, Lonesome Bend), monsters 28-53, storylines 41-85 and the
    /// world arc. Its arch in the portal room opens once Starfall Isle's finale (quest 16) is done.
    /// </summary>
    public static partial class WorldDefaults
    {
        public const string World2Goal =
            "The Dominion's Iron Line is crossing Dustwind to drain the Heartstone under Thunder Mesa. Stop it before the last spike is driven.";

        // ================================================================== monsters

        /// <summary>Dustwind's monsters (ids 28-53): regulars rank 4-5, elites 5-6, bosses 5-7.</summary>
        public static List<MonsterEntry> World2Monsters()
        {
            return new List<MonsterEntry>
            {
                // Tumbleweed Row
                R(M(28, "Tumbleweed Sprite", School.Life, 13, 880, false, 0, new[] { 136, 137, 138 }, 90, 16, 30, 188, 15, 0.85f, new Color(0.75f, 0.62f, 0.38f),
                    "Tumbleweed Row. A tumbleweed with a grudge; rolls in pairs and patches its friends up."), 4, "Tumbleweed", false),
                R(M(29, "Penny Gang Rustler", School.Myth, 13, 920, false, 0, new[] { 160, 161, 162 }, 95, 18, 32, 189, 15, 1f, new Color(0.52f, 0.36f, 0.26f),
                    "Tumbleweed Row. Bad Penny's wand-slingers. Drop pump washers (quest 68)."), 4, "Outlaw", false),
                R(M(30, "Bad Penny", School.Myth, 14, 3300, true, 1100, new[] { 160, 161, 163, 165, 200 }, 600, 120, 200, 181, 40, 1.15f, new Color(0.72f, 0.42f, 0.22f),
                    "Tumbleweed Row boss in the Penny Gang's hideout. Paid in Dominion silver (quest 47)."), 5, "Outlaw", false),
                // Coyote Gulch
                R(M(31, "Coyote Bandit", School.Fire, 14, 960, false, 0, new[] { 100, 101, 102 }, 100, 18, 34, 188, 15, 0.95f, new Color(0.72f, 0.5f, 0.32f),
                    "Coyote Gulch. Gone wild since the creek dried up. Carry off songbook pages (quest 75)."), 4, "Beast", false),
                R(M(32, "Cactus Brute", School.Life, 15, 1100, false, 0, new[] { 136, 138, 140 }, 105, 18, 34, 189, 15, 1.2f, new Color(0.36f, 0.56f, 0.3f),
                    "Coyote Gulch. A thirsty, prickly walking cactus."), 4, "Cactus", false),
                R(M(33, "Sand Scorpion", School.Death, 15, 1250, false, 0, new[] { 148, 149, 150, 152 }, 125, 22, 40, 188, 15, 1.1f, new Color(0.62f, 0.45f, 0.25f),
                    "Coyote Gulch, by the dam. Elite."), 5, "Beast", true),
                R(M(34, "Old Mesquite", School.Life, 16, 3800, true, 1200, new[] { 136, 137, 138, 140, 142 }, 680, 130, 220, 182, 40, 1.7f, new Color(0.33f, 0.5f, 0.26f),
                    "Coyote Gulch boss below the Dominion dam: a giant cactus driven wild by thirst (quest 51)."), 5, "Cactus", false),
                // Copperbottom Mine
                R(M(35, "Tommyknocker", School.Myth, 16, 1050, false, 0, new[] { 160, 161, 164 }, 110, 20, 36, 189, 15, 0.7f, new Color(0.55f, 0.45f, 0.35f),
                    "Copperbottom Mine camp. Little tunnel-folk who knock on the props before a cave-in."), 4, "Spirit", false),
                R(M(36, "Hollowed Miner", School.Death, 17, 1150, false, 0, new[] { 148, 149, 150 }, 115, 20, 38, 188, 15, 1.05f, new Color(0.42f, 0.4f, 0.38f),
                    "Copperbottom Mine. Empty miner's gear driven by light-ore. Drops copper pipe (quest 69)."), 4, "Hollowed", false),
                R(M(37, "Ore Golem", School.Balance, 17, 1400, false, 0, new[] { 172, 173, 174, 176 }, 135, 24, 42, 189, 15, 1.35f, new Color(0.55f, 0.45f, 0.36f),
                    "Copperbottom Mine galleries. Elite."), 5, "Construct", true),
                R(M(38, "Foreman Grist", School.Fire, 18, 4300, true, 1300, new[] { 100, 101, 102, 104, 106 }, 760, 140, 240, 183, 40, 1.3f, new Color(0.7f, 0.45f, 0.15f),
                    "Copperbottom Mine boss in the smelter at the bottom of the mine (quest 55)."), 5, "Dominion", false),
                // Iron Line Railyard
                R(M(39, "Dominion Trooper", School.Ice, 18, 1250, false, 0, new[] { 112, 113, 114, 116 }, 125, 22, 40, 188, 15, 1.1f, new Color(0.4f, 0.42f, 0.48f),
                    "Iron Line Railyard. The Dominion's rail guards."), 5, "Dominion", false),
                R(M(40, "Lantern Corps Engineer", School.Fire, 19, 1250, false, 0, new[] { 100, 101, 102, 104 }, 130, 22, 40, 189, 15, 1f, new Color(0.6f, 0.22f, 0.15f),
                    "Iron Line Railyard. Vesper's engineers. Drop pressure valves (quest 70)."), 5, "Dominion", false),
                R(M(41, "Boiler Hollowed", School.Storm, 19, 1550, false, 0, new[] { 124, 125, 126, 128 }, 150, 26, 46, 188, 15, 1.3f, new Color(0.35f, 0.33f, 0.4f),
                    "Iron Line Railyard. Hollowed built round a boiler. Elite."), 5, "Hollowed", true),
                R(M(42, "Captain Vesper", School.Fire, 20, 4800, true, 1400, new[] { 100, 101, 102, 104, 106, 200 }, 850, 160, 260, 184, 40, 1.2f, new Color(0.55f, 0.12f, 0.1f),
                    "Iron Line Railyard boss, in command of the Juggernaut; escapes again (quest 59)."), 6, "Dominion", false),
                R(M(43, "The Juggernaut", School.Storm, 20, 2400, false, 0, new[] { 124, 125, 128, 130 }, 300, 40, 80, 0, 0, 1f, new Color(0.22f, 0.22f, 0.26f),
                    "Vesper's armored locomotive, and Calloway's at the end of the line. Fights beside them."), 6, "Locomotive", true),
                // Thunder Mesa
                R(M(44, "Stormhawk", School.Storm, 20, 1300, false, 0, new[] { 124, 125, 126, 128 }, 135, 24, 42, 188, 15, 0.9f, new Color(0.45f, 0.5f, 0.62f),
                    "Thunder Mesa. Great grey hawks that ride the storm clouds."), 5, "Beast", false),
                R(M(45, "Mesa Warden", School.Balance, 21, 1500, false, 0, new[] { 172, 173, 174, 177 }, 140, 24, 44, 189, 15, 1.3f, new Color(0.62f, 0.5f, 0.4f),
                    "Thunder Mesa. Stone guardians who think you're with the railroad."), 5, "Construct", false),
                R(M(46, "Hail Devil", School.Ice, 21, 1650, false, 0, new[] { 112, 113, 114, 116, 118 }, 160, 28, 48, 188, 15, 1f, new Color(0.7f, 0.82f, 0.95f),
                    "Thunder Mesa's middle shelf. A whirl of hailstones. Elite."), 5, "Spirit", true),
                R(M(47, "Stone Warden Prime", School.Balance, 22, 5400, true, 1500, new[] { 172, 173, 174, 176, 178 }, 940, 180, 280, 185, 40, 1.8f, new Color(0.58f, 0.48f, 0.38f),
                    "Thunder Mesa boss on the summit: the Wardens' trial (quest 63)."), 6, "Construct", false),
                // Lonesome Bend and the drill fortress
                R(M(48, "Restless Prospector", School.Death, 22, 1450, false, 0, new[] { 148, 149, 150, 152 }, 145, 26, 46, 188, 15, 1f, new Color(0.55f, 0.6f, 0.62f),
                    "Lonesome Bend. A ghost still looking for the silver he never found."), 5, "Undead", false),
                R(M(49, "Phantom Outlaw", School.Death, 23, 1800, false, 0, new[] { 148, 149, 150, 152, 154 }, 175, 30, 52, 189, 15, 1.1f, new Color(0.4f, 0.5f, 0.55f),
                    "Lonesome Bend. The ghosts of the old Jackrabbit Gang. Elite."), 6, "Undead", true),
                R(M(50, "Dominion Driller", School.Ice, 23, 1600, false, 0, new[] { 112, 113, 114, 116, 118 }, 160, 28, 50, 188, 15, 1.15f, new Color(0.38f, 0.4f, 0.46f),
                    "Calloway's drill fortress at the end of the Iron Line."), 5, "Dominion", false),
                R(M(51, "Railmaster Ironhand Calloway", School.Storm, 25, 7200, true, 1800, new[] { 124, 125, 126, 128, 130, 200 }, 1300, 260, 400, 186, 40, 1.4f, new Color(0.3f, 0.28f, 0.32f),
                    "Dustwind's last boss, in the drill fortress, with the Juggernaut (quest 77)."), 7, "Dominion", false),
                R(M(52, "The Midnight Express", School.Death, 23, 6200, true, 1600, new[] { 148, 149, 150, 152, 153, 154 }, 1100, 220, 340, 187, 40, 1f, new Color(0.3f, 0.62f, 0.56f),
                    "Secret boss at Lonesome Bend's old station (quest 85, level 22; ring the station bell)."), 7, "GhostTrain", false),
                R(M(53, "Ghost Conductor", School.Death, 23, 1400, false, 0, new[] { 148, 149, 154 }, 160, 26, 44, 0, 0, 1f, new Color(0.55f, 0.85f, 0.8f),
                    "Rides the Midnight Express, punching tickets nobody bought."), 5, "Undead", false),
            };
        }

        // ================================================================== zones

        private static ZoneNpc Npc(string symbol, string key, string name, Color robe, Color hat, bool beard, params string[] lines)
        {
            return new ZoneNpc { symbol = symbol, key = key, displayName = name, robeColor = robe, hatColor = hat, beard = beard, facing = -1f, defaultDialogue = lines };
        }

        private static ZonePoint Pt(string symbol, string key, ZonePointKind kind, ZonePointLook look, string label, float size = 1f)
        {
            return new ZonePoint { symbol = symbol, key = key, kind = kind, look = look, label = label, triggerSizeCells = size };
        }

        private static ZoneEncounter Enc(string ring, string key, int wanderers, float speed, params int[] roster)
        {
            return new ZoneEncounter { ringSymbol = ring, key = key, roster = new List<int>(roster), wanderers = wanderers, patrolSpeed = speed, respawnSeconds = 20 };
        }

        private static ZoneEncounter Boss(string ring, string key, params int[] roster)
        {
            return new ZoneEncounter { ringSymbol = ring, key = key, roster = new List<int>(roster), wanderers = 1, patrolSpeed = 0.7f, respawnSeconds = 30, scaleWithParty = true };
        }

        /// <summary>Dustwind's warm desert palette (each street then tweaks its own).</summary>
        private static void DesertLook(ZoneDefinition z, ZoneTextureStyle style)
        {
            z.world = 2;
            z.textureStyle = style;
            z.cellSize = 8f;
            z.groundColor = new Color(0.62f, 0.45f, 0.31f);
            z.roadColor = new Color(0.74f, 0.58f, 0.4f);
            z.grassColor = new Color(0.78f, 0.62f, 0.42f);
            z.buildingColor = new Color(0.66f, 0.5f, 0.37f);
            z.roofColor = new Color(0.44f, 0.29f, 0.2f);
            z.lowWallColor = new Color(0.55f, 0.4f, 0.28f);
            z.treeColor = new Color(0.34f, 0.52f, 0.3f);
            z.accentColor = new Color(1f, 0.74f, 0.38f);
            z.buildingHeight = new Vector2(6f, 10f);
            z.lampDensity = 0.15f;
            // A big orange-to-violet desert sky.
            z.skyTopColor = new Color(0.34f, 0.42f, 0.85f);
            z.skyHorizonColor = new Color(1f, 0.68f, 0.42f);
            z.fogColor = new Color(0.95f, 0.72f, 0.52f);
            z.horizonDistance = 1300f;
            z.railings = RailingMode.Drops;
            z.heightStep = 1.5f;
        }

        /// <summary>
        /// Archrock (Dustwind's town, cells of 6 m): Main Street runs north from the sandstone arch home (S) past the town
        /// square (the pump, the High Noon circle, the Dusty Lantern Saloon to the west, the corral to the east) to High
        /// Street (the Sheriff's Office and bounty board, the depot). Front Street crosses the south end between the shops.
        /// Six trails leave town, one per street, opened in story order.
        /// </summary>
        public static void FillArchrock(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Boomtown);
            z.zoneKey = "Archrock";
            z.displayName = "Archrock";
            z.levelRange = "Lv 13-25";
            z.isTown = true;
            z.height = 1200f;
            z.cellSize = 6f;
            z.lampDensity = 0.3f;
            z.map =
            "#####################################\n" +
            "#########O#################Y#########\n" +
            "#########.#################.#########\n" +
            "#########.#######...#######.#########\n" +
            "#########.#######...........#########\n" +
            "#########.#######...###..T..#########\n" +
            "##......................H..........##\n" +
            "#M.................................C#\n" +
            "##...#######........#################\n" +
            "####.#######..B.....#################\n" +
            "####.......#####A...#######========##\n" +
            "####.......................=,,,,,,=##\n" +
            "####.LLLLL..........UUUU..D=,aaaaa=##\n" +
            "####.LQLLL..........UUUU....,a,,,a=##\n" +
            "####.LLLLL..........UUUU....,a,1,a=##\n" +
            "####.LLLLL..K.......UUUU....,a,,,a=##\n" +
            "####.......G......P.........,a,,,a=##\n" +
            "####............E.......+..=,aaaaa=##\n" +
            "####.######................=,,,,,,=##\n" +
            "####.#######.mm......jj...#========##\n" +
            "####.#######.mm......jj...###########\n" +
            "#N..........*......................R#\n" +
            "##............................+....##\n" +
            "############.oo......nn.#############\n" +
            "############.oo......nn.#############\n" +
            "############.##......##.#############\n" +
            "############.##....w.##.#############\n" +
            "#################...#################\n" +
            "#################...#################\n" +
            "#################.S.#################\n" +
            "#####################################\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("A", "Ada", "Marshal Ada Quickwater", new Color(0.42f, 0.32f, 0.24f), new Color(0.3f, 0.22f, 0.16f), false,
                    "Archrock's a good town, deputy. Dry as a bone since the railroad came, but good.",
                    "You see trouble, you tell me. You see the Dominion, you tell me twice."),
                Npc("E", "Ezra", "Ezra Pike, Pumpwright", new Color(0.36f, 0.42f, 0.5f), new Color(0.24f, 0.28f, 0.34f), true,
                    "That pump fed this whole town for forty years. Now it coughs up dust. Breaks my heart."),
                Npc("D", "Dusty", "Dusty Mae Callahan", new Color(0.62f, 0.4f, 0.28f), new Color(0.48f, 0.3f, 0.2f), false,
                    "The corral's open to anyone who wants to practice. The straw dummies don't hit back. Much."),
                Npc("H", "Hobbs", "Stationmaster Hobbs", new Color(0.2f, 0.26f, 0.42f), new Color(0.14f, 0.18f, 0.3f), true,
                    "Trains used to stop here twice a day. Now they only run for the Dominion. I'll get the line running for folks again, you'll see."),
                Npc("Q", "Sal", "Sal Mesquite", new Color(0.55f, 0.18f, 0.16f), new Color(0.32f, 0.12f, 0.1f), true,
                    "Sarsaparilla's on the house for the deputy. Just don't tell the marshal I said so."),
                Npc("G", "Juniper", "Grandma Juniper", new Color(0.5f, 0.42f, 0.6f), new Color(0.36f, 0.3f, 0.44f), false,
                    "I've walked every trail out of this town, back when they had names instead of numbers on a Dominion map."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("P", "TownPump", ZonePointKind.Interact, ZonePointLook.WaterPump, "Town pump"),
                Pt("w", "ArchrockArrival", ZonePointKind.Explore, ZonePointLook.None, "", 3f),
                Pt("K", "ArchrockCrates", ZonePointKind.Decoration, ZonePointLook.Barrel, ""),
            };
            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "StrawDummies", roster = new List<int> { 12 }, wanderers = 2, patrolSpeed = 0.5f, respawnSeconds = 10, evenFight = true },
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "L", kind = LandmarkKind.Saloon, label = "The Dusty Lantern Saloon" },
                new ZoneLandmark { symbol = "U", kind = LandmarkKind.DuelCircle, label = "High Noon Circle" },
                new ZoneLandmark { symbol = "T", kind = LandmarkKind.TrainDepot, label = "Archrock" },
                new ZoneLandmark { symbol = "B", kind = LandmarkKind.BountyBoard, label = "Wanted" },
                // Shops, in the order of their wares: robes and hats, wands, charms and rings, potions and decks.
                new ZoneLandmark { symbol = "o", kind = LandmarkKind.Shop, label = "Saddler & Outfitter", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "n", kind = LandmarkKind.Shop, label = "Wandsmith", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "j", kind = LandmarkKind.Shop, label = "Assay Office", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "m", kind = LandmarkKind.Shop, label = "Quickwater General Store", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "+", kind = LandmarkKind.HealthOrb },
                new ZoneLandmark { symbol = "*", kind = LandmarkKind.ManaOrb },
                Trail("R", "TumbleweedRow", 43, "Locked: Marshal Quickwater wants a word first"),
                Trail("N", "CoyoteGulch", 47, "Locked: deal with the Penny Gang on Tumbleweed Row first"),
                Trail("M", "CopperbottomMine", 51, "Locked: find out why Coyote Gulch ran dry first"),
                Trail("C", "IronLineRailyard", 55, "Locked: help the miners of Copperbottom first"),
                Trail("Y", "ThunderMesa", 59, "Locked: stop the Juggernaut at the railyard first"),
                Trail("O", "LonesomeBend", 63, "Locked: earn the Wardens' trust on Thunder Mesa first"),
            };
        }

        private static ZoneLandmark Trail(string symbol, string target, int quest, string hint)
        {
            return new ZoneLandmark { symbol = symbol, kind = LandmarkKind.StreetGate, targetZone = target, requiredQuest = quest, lockedHint = hint };
        }

        /// <summary>Tumbleweed Row (Lv 13-14): boardwalks, false-front shops, the stagecoach stop, the bank and the Penny Gang's hideout.</summary>
        public static void FillTumbleweedRow(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Boomtown);
            z.zoneKey = "TumbleweedRow";
            z.displayName = "Tumbleweed Row";
            z.levelRange = "Lv 13-14";
            z.height = 1300f;
            z.lampDensity = 0.25f;
            z.map =
            "###############################\n" +
            "###############################\n" +
            "######################^########\n" +
            "#################...........###\n" +
            "#################...........^##\n" +
            "#################.....5.....###\n" +
            "#################...........###\n" +
            "################^...........###\n" +
            "#################...........###\n" +
            "###############^#...........###\n" +
            "###########....$##^#.....^#####\n" +
            "#######^......X.####..f..######\n" +
            "########........####M.f..######\n" +
            "########..T.....####..f..######\n" +
            "########.....T..^###.....######\n" +
            "########...#^######^..6..######\n" +
            "########T..#########.....######\n" +
            "########...^########.....^#####\n" +
            "#######^...###^#####..d..######\n" +
            "###^............M.....d..######\n" +
            "####..................d..######\n" +
            "####..bbb...3.cccc..4.d..######\n" +
            "####..b..................######\n" +
            "####..b..................######\n" +
            "####.....##^######^######^#####\n" +
            "####..2............L###########\n" +
            "###^..........M....C###########\n" +
            "####P....####^######^##########\n" +
            "####..a..M........#############\n" +
            "####..a...........#############\n" +
            "####..aaa.1.......#############\n" +
            "####..............^############\n" +
            "####.........H...W#############\n" +
            "###^######^##.....#############\n" +
            "#############..S..#############\n" +
            "###############################\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("H", "Hattie", "Hattie Wells, Express Agent", new Color(0.3f, 0.36f, 0.5f), new Color(0.2f, 0.24f, 0.34f), false,
                    "The Archrock Express has never lost a letter. Not until those rustlers showed up, anyway."),
                Npc("L", "Ledbetter", "Banker Ledbetter", new Color(0.24f, 0.24f, 0.28f), new Color(0.12f, 0.12f, 0.14f), true,
                    "Money's money, I always say. But Dominion money? That makes my teeth itch."),
                Npc("P", "Pete", "Pete the Stable Boy", new Color(0.55f, 0.45f, 0.3f), new Color(0.4f, 0.3f, 0.2f), false,
                    "When I grow up I'm gonna be a deputy too. Or a stagecoach driver. Or both!"),
            };
            z.points = new List<ZonePoint>
            {
                Pt("W", "Stagecoach", ZonePointKind.Decoration, ZonePointLook.Stagecoach, ""),
                Pt("M", "MailSack", ZonePointKind.Interact, ZonePointLook.Crate, "Dropped mail sack"),
                Pt("T", "WagonTracks", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Wagon tracks"),
                Pt("X", "Hideout", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("C", "SilverChest", ZonePointKind.Interact, ZonePointLook.Crate, "Strongbox"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "StageSprites", 3, 1.1f, 28, 28),
                Enc("2", "CorralGang", 3, 1.2f, 29, 28),
                Enc("3", "BankRobbers", 3, 1.2f, 29, 29),
                Enc("4", "BackLot", 3, 1.2f, 29, 28, 29),
                Boss("5", "BadPenny", 30, 29, 29),
                Enc("6", "EastYard", 3, 1.1f, 28, 29),
            };
            // Treasure caches at the ends of side alleys.
            z.landmarks = new List<ZoneLandmark> { new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" } };
        }

        /// <summary>Coyote Gulch (Lv 14-16): Rosa's ranch, a dry creek bed (sunk a step) with adobe ruins and the water tower on its banks, and the Dominion dam at the top.</summary>
        public static void FillCoyoteGulch(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Desert);
            z.zoneKey = "CoyoteGulch";
            z.displayName = "Coyote Gulch";
            z.levelRange = "Lv 14-16";
            z.height = 1400f;
            z.lampDensity = 0.1f;
            z.cornerRadius = 1.5f;
            z.map =
            "#################################\n" +
            "#############~~~~~~~~~~~~~#######\n" +
            "############^~~~~~~~~~~~~~#######\n" +
            "#############...........X.#######\n" +
            "#############.............#######\n" +
            "#############......5......#######\n" +
            "#############.............#######\n" +
            "#############.............^######\n" +
            "#############.............#######\n" +
            "##$......C^######..f..^##^#######\n" +
            "##........######^..f.G###########\n" +
            "##...2....#######.....###########\n" +
            "##........^######..6..F##########\n" +
            "##...b....#######..........^#####\n" +
            "#^...b....#######..........######\n" +
            "##...b....#######.....ddd..^#####\n" +
            "#^##.b.##########.......d..######\n" +
            "####.b.##########..........######\n" +
            "####...##########^##^#..4......##\n" +
            "####...^##############.........##\n" +
            "####...######^##^##^##.........##\n" +
            "####...##^##....G.......c..#...##\n" +
            "####.......#............c..^...##\n" +
            "####.......#..ggg...3.ccc..#...##\n" +
            "####.......#...............^..G##\n" +
            "########...^C.7.......C....#...##\n" +
            "########...#.....##^##^##^##...##\n" +
            "########...#.....###########...^#\n" +
            "###^.............##########.....#\n" +
            "####K............##########.....#\n" +
            "####....aaaa.....##########....U#\n" +
            "###^..1..........##########W....#\n" +
            "####K............##########.....#\n" +
            "####....R##################.P..$#\n" +
            "####.....########################\n" +
            "####.....########################\n" +
            "####..S..########################\n" +
            "#################################\n";
            z.heightMap =
            "000000000000000000000000000000000\n" +
            "00000000000a111111111111111000000\n" +
            "00000000000a111111111111111000000\n" +
            "00000000000a000000000000000000000\n" +
            "00000000000a000000000000000000000\n" +
            "00000000000a000000000000000000000\n" +
            "00000000000a000000000000000000000\n" +
            "00000000000a000000000000000000000\n" +
            "00000000000a000000000000000000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "00000000000aaaaaaaaaaaaaaa0000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n" +
            "000000000000000000000000000000000\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("R", "Rosa", "Rosa Calder, Rancher", new Color(0.62f, 0.3f, 0.22f), new Color(0.44f, 0.3f, 0.18f), false,
                    "Three generations of Calders have ranched this gulch. I'm not going to be the one who gives up on it."),
                Npc("P", "Gus", "Prospector Gus", new Color(0.5f, 0.42f, 0.32f), new Color(0.36f, 0.3f, 0.22f), true,
                    "Forty years panning this creek and the one year I strike color, the water up and leaves. Typical."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("W", "GulchWindmill", ZonePointKind.Decoration, ZonePointLook.Windmill, ""),
                Pt("U", "GulchWaterTower", ZonePointKind.Decoration, ZonePointLook.WaterTower, ""),
                Pt("K", "GulchBarrels", ZonePointKind.Decoration, ZonePointLook.Barrel, ""),
                Pt("C", "CreekStone", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Cracked creek stone"),
                Pt("G", "SluiceGate", ZonePointKind.Interact, ZonePointLook.Signpost, "Sluice gate"),
                Pt("X", "TheDam", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("F", "FlowingCreek", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "RanchCoyotes", 2, 1.3f, 31, 31),
                Enc("2", "BanditCamp", 3, 1.3f, 31, 31, 32),
                Enc("3", "AdobeRuins", 3, 1.1f, 32, 31),
                Enc("4", "ScorpionFlats", 3, 1f, 33, 32),
                Boss("5", "OldMesquite", 34, 32, 33),
                Enc("6", "WaterTower", 3, 1.1f, 32, 32),
                Enc("7", "CreekBed", 3, 1.2f, 31, 32),
            };
            // Treasure caches at the ends of side alleys.
            z.landmarks = new List<ZoneLandmark> { new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" } };
        }

        /// <summary>
        /// Copperbottom Mine (Lv 16-18): the mining camp outside, and the mine itself as a dungeon (the sigil at the mine
        /// mouth, the barred gate): timber galleries, the ore-to-armor works and Foreman Grist's smelter at the bottom.
        /// </summary>
        public static void FillCopperbottomMine(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Mine);
            z.zoneKey = "CopperbottomMine";
            z.musicTrack = "Music_Zone_CopperbottomMine";
            z.displayName = "Copperbottom Mine";
            z.levelRange = "Lv 16-18";
            z.height = 1500f;
            z.lampDensity = 0.45f;
            z.groundColor = new Color(0.42f, 0.34f, 0.28f);
            z.roadColor = new Color(0.5f, 0.42f, 0.34f);
            z.grassColor = new Color(0.46f, 0.38f, 0.32f);
            z.buildingColor = new Color(0.5f, 0.4f, 0.32f);
            z.accentColor = new Color(1f, 0.7f, 0.3f);
            // Dusty amber light at the mine mouth.
            z.skyTopColor = new Color(0.4f, 0.36f, 0.5f);
            z.skyHorizonColor = new Color(0.92f, 0.6f, 0.38f);
            z.fogColor = new Color(0.55f, 0.42f, 0.32f);
            z.map =
            "#################################\n" +
            "#################################\n" +
            "#########.F...........;.#########\n" +
            "#########.eeeeeeeeeeeee.#########\n" +
            "#########.e...........e.#########\n" +
            "#########.e.....5.....e.#########\n" +
            "#########.e...........e.#########\n" +
            "#########.eeeeeeeeeeeee.#########\n" +
            "#########...............#########\n" +
            "##########..J.......J..##########\n" +
            "##########.............##########\n" +
            "##########.............##########\n" +
            "##############.....##############\n" +
            "###ccccccc####.....####ddddddd###\n" +
            "###c.....c####.....####d.....d###\n" +
            "###c.....c####.....####d.....d###\n" +
            "###c..3..c####.....####d..4..d###\n" +
            "###c.....c####..W..####d.....d###\n" +
            "###c.....c####.....####d.....d###\n" +
            "###ccccccc####.....####ddddddd###\n" +
            "###.......####.....####.......###\n" +
            "###M.....M####.....####......M###\n" +
            "####........fffffffff........####\n" +
            "####........f...6...f........####\n" +
            "####........f.......f........####\n" +
            "####........fffffffff........####\n" +
            "###############...###############\n" +
            "###############...###############\n" +
            "###############.}.###############\n" +
            "###############;..###############\n" +
            "#############==|||==#############\n" +
            "#############O.....N.###K....$###\n" +
            "##....K.........{....###......###\n" +
            "##...................###......###\n" +
            "##..bbbbb.2..........####...#####\n" +
            "##..b...............K####...#####\n" +
            "##..b............########...#####\n" +
            "##.....##################...#####\n" +
            "##..a..............######...#####\n" +
            "##..a.......................#####\n" +
            "##..aaaaa.1.................#####\n" +
            "##..........................#####\n" +
            "##..............S..##############\n" +
            "#################################\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("N", "Nell", "Shift Boss Nell Harlan", new Color(0.4f, 0.32f, 0.22f), new Color(0.8f, 0.65f, 0.2f), false,
                    "Copperbottom's been dug by honest hands for sixty years. Then the Corps showed up with their contracts and their armor."),
                Npc("O", "Cobb", "Old Cobb", new Color(0.36f, 0.34f, 0.32f), new Color(0.26f, 0.24f, 0.22f), true,
                    "Hear that knocking? Tommyknockers. They knock before a cave-in. Lately they won't stop."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("K", "OreCart", ZonePointKind.Decoration, ZonePointLook.OreCart, ""),
                Pt("F", "Smelter", ZonePointKind.Decoration, ZonePointLook.DrillRig, ""),
                Pt("M", "TrappedMiner", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Trapped miner"),
                Pt("W", "OreWorks", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("J", "ArmorRack", ZonePointKind.Interact, ZonePointLook.Crate, "Armor rack"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "CampKnockers", 3, 1f, 35, 35),
                Enc("2", "CampHollowed", 3, 1f, 36, 35),
                Enc("3", "WestGallery", 3, 0.9f, 36, 36),
                Enc("4", "EastGallery", 3, 0.9f, 37, 36),
                Boss("5", "ForemanGrist", 38, 36, 37),
                Enc("6", "MainGallery", 3, 0.9f, 36, 35, 36),
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" },
                new ZoneLandmark { symbol = "{", kind = LandmarkKind.DungeonSigil, label = "Copperbottom Mine" },
                new ZoneLandmark { symbol = "}", kind = LandmarkKind.DungeonStart },
                new ZoneLandmark { symbol = ";", kind = LandmarkKind.DungeonExit },
                new ZoneLandmark { symbol = "|", kind = LandmarkKind.DungeonGate },
            };
        }

        /// <summary>The Iron Line Railyard (Lv 18-20): rail lines, water towers, cattle cars, the signal tower and the Juggernaut's roundhouse.</summary>
        public static void FillIronLineRailyard(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Boomtown);
            z.zoneKey = "IronLineRailyard";
            z.displayName = "Iron Line Railyard";
            z.levelRange = "Lv 18-20";
            z.height = 1600f;
            z.lampDensity = 0.2f;
            z.roadColor = new Color(0.6f, 0.5f, 0.4f);
            z.buildingColor = new Color(0.5f, 0.42f, 0.38f);
            z.roofColor = new Color(0.3f, 0.3f, 0.34f);
            z.accentColor = new Color(1f, 0.55f, 0.3f);
            // Coal smoke over the yard.
            z.skyTopColor = new Color(0.36f, 0.36f, 0.5f);
            z.skyHorizonColor = new Color(0.9f, 0.62f, 0.45f);
            z.fogColor = new Color(0.7f, 0.58f, 0.5f);
            z.map =
            "#################################\n" +
            "#################################\n" +
            "#################################\n" +
            "#################rJJJJJJJrrrrr###\n" +
            "#################.............###\n" +
            "#################......5......###\n" +
            "#################............K###\n" +
            "#################.O...........###\n" +
            "#################U............###\n" +
            "#####################....r#######\n" +
            "############YYY######....r#######\n" +
            "#######..G..YYY...$##..f.r#######\n" +
            "#######.....YYY....##..f.r#######\n" +
            "#######............##..f.r#######\n" +
            "#######............##..f.r#######\n" +
            "#######G..........G##....r#######\n" +
            "##########...########..6.r#######\n" +
            "##########...########....r#######\n" +
            "##########...########....r#######\n" +
            "##########...########....r#######\n" +
            "#####rrrrrrrrrrrrrrrrrrdrr#######\n" +
            "#####r.................d..#######\n" +
            "#####r.bbb..3.ccc..4.ddd..#######\n" +
            "#####r.b..................#######\n" +
            "#####r.b.K...........U....#######\n" +
            "#####r....#######################\n" +
            "#####r.2................X########\n" +
            "#####r.....rWrrrWrrrWrrr$########\n" +
            "#####r....#######################\n" +
            "#####r.a.U.........##############\n" +
            "#####r.a...........##############\n" +
            "#####r.aaa.1.......##############\n" +
            "#####r.............##############\n" +
            "#####r........E...K##############\n" +
            "##############.....##############\n" +
            "##############..S..##############\n" +
            "#################################\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("E", "Eli", "Switchman Eli Barnes", new Color(0.3f, 0.34f, 0.44f), new Color(0.6f, 0.15f, 0.12f), true,
                    "Keep your head down. Half this yard thinks I work for the Dominion. The other half knows I don't."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("r", "Rails", ZonePointKind.Decoration, ZonePointLook.Rails, ""),
                Pt("U", "YardWaterTower", ZonePointKind.Decoration, ZonePointLook.WaterTower, ""),
                Pt("W", "CattleCars", ZonePointKind.Decoration, ZonePointLook.Wagon, ""),
                Pt("J", "Juggernaut", ZonePointKind.Decoration, ZonePointLook.Locomotive, ""),
                Pt("K", "YardBarrels", ZonePointKind.Decoration, ZonePointLook.Barrel, ""),
                Pt("G", "SignalLever", ZonePointKind.Interact, ZonePointLook.Signpost, "Signal lever"),
                Pt("X", "CattleCar", ZonePointKind.Explore, ZonePointLook.None, "", 1.5f),
                Pt("O", "DominionOrders", ZonePointKind.Interact, ZonePointLook.Signpost, "Dominion orders"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "GateGuards", 3, 1.2f, 39, 39),
                Enc("2", "MiddleYard", 3, 1.2f, 40, 39),
                Enc("3", "FreightLine", 3, 1.1f, 39, 40, 39),
                Enc("4", "SignalWatch", 3, 1f, 41, 40),
                Boss("5", "Juggernaut", 42, 43, 40),
                Enc("6", "EastSidings", 3, 1.1f, 40, 41),
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" },
                new ZoneLandmark { symbol = "Y", kind = LandmarkKind.Tower, label = "Signal Tower" },
            };
        }

        /// <summary>Thunder Mesa (Lv 20-22): three shelves climbing to the summit, the Warden stones and the sealed Heartstone gate.</summary>
        public static void FillThunderMesa(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Desert);
            z.zoneKey = "ThunderMesa";
            z.displayName = "Thunder Mesa";
            z.levelRange = "Lv 20-22";
            z.height = 1700f;
            z.lampDensity = 0.05f;
            z.cornerRadius = 2f;
            z.groundColor = new Color(0.58f, 0.38f, 0.3f);
            z.roadColor = new Color(0.68f, 0.5f, 0.4f);
            z.grassColor = new Color(0.62f, 0.44f, 0.34f);
            z.buildingColor = new Color(0.62f, 0.4f, 0.3f);
            z.accentColor = new Color(0.75f, 0.85f, 1f);
            // Storm greys over the mesa.
            z.skyTopColor = new Color(0.26f, 0.28f, 0.38f);
            z.skyHorizonColor = new Color(0.62f, 0.6f, 0.62f);
            z.fogColor = new Color(0.5f, 0.5f, 0.54f);
            z.map =
            "###############################\n" +
            "###############################\n" +
            "###############################\n" +
            "##########^.....X........######\n" +
            "###########.............K######\n" +
            "###########..............^#####\n" +
            "###########.....5........######\n" +
            "###########..............######\n" +
            "##########^..............######\n" +
            "###########..............######\n" +
            "###########..............^#####\n" +
            "####################W....######\n" +
            "##^^^^^^^^^^^^^^^###.....######\n" +
            "###$...........W####..d..######\n" +
            "###.....3.ccc...####..d..######\n" +
            "###.............####.........^#\n" +
            "###.............^###..4......##\n" +
            "#########...########.........^#\n" +
            "####.....................#...##\n" +
            "###^.....................#...##\n" +
            "####....bbbb...6.ffff....#...##\n" +
            "####.....................^...##\n" +
            "####W.2..................#...^#\n" +
            "####.....#################...##\n" +
            "####.....#################...##\n" +
            "####.....################.....#\n" +
            "####...................##.....#\n" +
            "####...................##..B.$#\n" +
            "####.....aaaa.1........^^^^^^^#\n" +
            "####...................########\n" +
            "####K.............O....^#######\n" +
            "##################.....########\n" +
            "##################..S..########\n" +
            "###############################\n";
            z.heightMap =
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "4444444444444444444444444444444\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "2222222222222222222222222222222\n" +
            "0000000000000000000000000222220\n" +
            "0000000000000000000000000222220\n" +
            "0000000000000000000000000222220\n" +
            "0000000000000000000000000000000\n" +
            "0000000000000000000000000000000\n" +
            "0000000000000000000000000000000\n" +
            "0000000000000000000000000000000\n" +
            "0000000000000000000000000000000\n" +
            "0000000000000000000000000000000\n";
            z.heightStep = 2f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("O", "Orla", "Skywatcher Orla", new Color(0.3f, 0.36f, 0.55f), new Color(0.22f, 0.26f, 0.4f), false,
                    "I came up here to count the storms. Now I count Dominion survey stakes instead."),
                Npc("B", "Basalt", "Elder Warden Basalt", new Color(0.55f, 0.5f, 0.46f), new Color(0.42f, 0.38f, 0.34f), true,
                    "You smell of iron and coal smoke, little wizard. The mesa does not trust that smell."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("W", "WardenStone", ZonePointKind.Interact, ZonePointLook.StandingStone, "Warden stone"),
                Pt("K", "MesaStones", ZonePointKind.Decoration, ZonePointLook.StandingStone, ""),
                Pt("X", "HeartstoneGate", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "LowerHawks", 3, 1.4f, 44, 44),
                Enc("2", "LowerWardens", 3, 0.9f, 45, 44),
                Enc("3", "WestShelf", 3, 1f, 45, 45),
                Enc("4", "EastShelf", 3, 1.2f, 46, 44),
                Boss("5", "WardenPrime", 47, 45, 46),
                Enc("6", "MiddleCircle", 3, 1.1f, 46, 45),
            };
            // Treasure caches at the ends of side alleys.
            z.landmarks = new List<ZoneLandmark> { new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" } };
        }

        /// <summary>
        /// Lonesome Bend (Lv 22-25): a ghost town at dusk, the chapel and graveyard, the old station (the Midnight Express),
        /// and the hidden trail to Calloway's drill fortress (a dungeon: the sigil at the trail's end, the barred gate).
        /// </summary>
        public static void FillLonesomeBend(ZoneDefinition z)
        {
            DesertLook(z, ZoneTextureStyle.Boomtown);
            z.zoneKey = "LonesomeBend";
            z.displayName = "Lonesome Bend";
            z.levelRange = "Lv 22-25";
            z.height = 1800f;
            z.lampDensity = 0.2f;
            z.groundColor = new Color(0.42f, 0.36f, 0.36f);
            z.roadColor = new Color(0.52f, 0.46f, 0.44f);
            z.grassColor = new Color(0.46f, 0.42f, 0.4f);
            z.buildingColor = new Color(0.46f, 0.4f, 0.38f);
            z.roofColor = new Color(0.26f, 0.22f, 0.26f);
            z.accentColor = new Color(0.55f, 0.95f, 0.85f);
            // Dusk purples.
            z.skyTopColor = new Color(0.2f, 0.14f, 0.36f);
            z.skyHorizonColor = new Color(0.78f, 0.45f, 0.5f);
            z.fogColor = new Color(0.42f, 0.32f, 0.44f);
            z.map =
            "###################################\n" +
            "###################################\n" +
            "####........eeeeeZeeeee........####\n" +
            "####.D......eV........e......;.####\n" +
            "####........e....5....e........####\n" +
            "####........e.........e........####\n" +
            "####========e.........e========####\n" +
            "####........eeeeeeeeeee........####\n" +
            "####.fffffff...........ggggggg.####\n" +
            "####.f.....f...........g.....D.####\n" +
            "####.f..6..f...........g..7..g.####\n" +
            "####.f.....f...........g.....g.####\n" +
            "####.fffffff...........ggggggg.####\n" +
            "###############.....###############\n" +
            "###^$.........#.....###############\n" +
            "####..........#..}..###############\n" +
            "####........O.#.;...###############\n" +
            "###^....{.....==|||==##############\n" +
            "####.................##############\n" +
            "####.................##############\n" +
            "####.................##############\n" +
            "####.................##############\n" +
            "######.....#^##^##^##^##^##^#######\n" +
            "######.....###.+..+..M.+..+.#######\n" +
            "######.....##^..............#######\n" +
            "######.....###K............$#######\n" +
            "######.....##^##^##...#############\n" +
            "#####^..d..########...^##^##^##^###\n" +
            "######..d......................r###\n" +
            "##^##^..d......................r###\n" +
            "##......dd.4...bbbbb.2....ccc..r###\n" +
            "##..........................c..r^##\n" +
            "##.........................Pc..r###\n" +
            "##...#####################..c..r###\n" +
            "##...#####################..c..r###\n" +
            "##...^####################....Qr###\n" +
            "##...#####################..3..r###\n" +
            "##...^####################.....r###\n" +
            "##..K#####################.....r###\n" +
            "##...^##########^##^##^##^.....r###\n" +
            "##...#########^.............a.Kr###\n" +
            "##...##########.............a..r###\n" +
            "##...#########^.......1.aaaaa..r###\n" +
            "##$X.##########................r###\n" +
            "###############H...............r###\n" +
            "###############.....###############\n" +
            "###############..S..^##############\n" +
            "###################################\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("H", "Hollis", "Deputy Hollis (a ghost)", new Color(0.5f, 0.7f, 0.68f), new Color(0.36f, 0.52f, 0.5f), false,
                    "Somebody's got to keep the peace in Lonesome Bend. Even if there's only ghosts left to keep it for."),
                Npc("M", "Mae", "Widow Mae (a ghost)", new Color(0.55f, 0.6f, 0.7f), new Color(0.4f, 0.44f, 0.54f), false,
                    "I keep the porch light on. Somebody's bound to come home eventually."),
                Npc("P", "Abel", "Parson Abel (a ghost)", new Color(0.3f, 0.3f, 0.36f), new Color(0.2f, 0.2f, 0.24f), true,
                    "The bell in my chapel hasn't rung since the survey came through. I'd dearly love to hear it again."),
                Npc("O", "Silas", "Old Silas, Stationmaster (a ghost)", new Color(0.32f, 0.42f, 0.5f), new Color(0.2f, 0.28f, 0.36f), true,
                    "The midnight train's running late. It's been running late for thirty years."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("+", "Gravestones", ZonePointKind.Decoration, ZonePointLook.Gravestone, ""),
                Pt("r", "OldRails", ZonePointKind.Decoration, ZonePointLook.Rails, ""),
                Pt("D", "DrillRig", ZonePointKind.Decoration, ZonePointLook.DrillRig, ""),
                Pt("K", "Keepsake", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Lost keepsake"),
                Pt("X", "HiddenTrail", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("V", "VesperChains", ZonePointKind.Interact, ZonePointLook.Crate, "Vesper's chains"),
                Pt("Z", "Heartstone", ZonePointKind.Interact, ZonePointLook.GlowingItem, "The Heartstone"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "MainStreetGhosts", 3, 0.9f, 48, 48),
                Enc("2", "WestEnd", 3, 0.9f, 48, 49),
                Enc("3", "StationYard", 3, 1f, 49, 48),
                Enc("4", "Graveyard", 3, 0.8f, 48, 48, 49),
                Boss("5", "Calloway", 51, 43, 50),
                Enc("6", "FortressWest", 3, 1f, 50, 50),
                Enc("7", "FortressEast", 3, 1f, 50, 49),
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" },
                new ZoneLandmark { symbol = "{", kind = LandmarkKind.DungeonSigil, label = "Calloway's Drill Fortress" },
                new ZoneLandmark { symbol = "}", kind = LandmarkKind.DungeonStart },
                new ZoneLandmark { symbol = ";", kind = LandmarkKind.DungeonExit },
                new ZoneLandmark { symbol = "|", kind = LandmarkKind.DungeonGate },
            };
            z.secretBoss = MidnightExpressSecret();
        }

        /// <summary>The Midnight Express at Lonesome Bend's old station: ring the bell (Q) once quest 85 (Old Silas, level 22) is started.</summary>
        public static ZoneSecretBoss MidnightExpressSecret()
        {
            return new ZoneSecretBoss
            {
                enabled = true, displayName = "The Midnight Express", entranceLabel = "Station Bell", questId = 85, entranceSymbol = "Q",
                lockedHint = "Its rope is tied up. Old Silas might know why.",
                roster = new List<int> { 52, 53, 53, 53 },
                greeting = "Tickets, please. No ticket? Then you ride with us, forever, through the long dark night.",
                clearBit = 29,
            };
        }

        // ================================================================== storylines

        private static Questline Line(Questline ql, string name, int end, params QuestEntry[] quests)
        {
            ql.includeInBuild = true;
            ql.storylineName = name;
            ql.storylineEnd = end;
            ql.quests = new List<QuestEntry>(quests);
            return ql;
        }

        /// <summary>Adds Dustwind's school side quests (by save id) to a storyline.</summary>
        private static void Side(Questline ql, params int[] ids)
        {
            foreach (var q in World2SchoolQuests())
                if (System.Array.IndexOf(ids, q.saveId) >= 0) ql.quests.Add(q);
        }

        /// <summary>Deputized (Archrock, quests 41-43): through the arch, Marshal Quickwater's badge, a tour of a thirsty town.</summary>
        public static void FillDeputized(Questline ql)
        {
            Line(ql, "Deputized", 43,
                Q(41, "Through the Old Arch", 16, 12, 400, 120, 0,
                    "The rekindled crystal woke the first old arch in the portal room beneath the Headmaster's tower. Step through it and see where it leads.",
                    Point(QuestStepKind.Explore, "ArchrockArrival", 1, "Step through the World 2 arch in the portal room, beneath the Headmaster's tower", "Archrock"),
                    Talk("Ada", "Find whoever's in charge of this town", "Marshal Ada Quickwater",
                        "Well, I'll be. A wizard, stepping out of the old arch like it's a barn door. Nobody's done that since my granddaddy's day.",
                        "Name's Ada Quickwater. I'm the marshal here in Archrock, for my sins. Our sheriff lit out a month ago, and I could use a hand.",
                        "You look like you can handle yourself. How'd you like to be a deputy?")),
                Q(42, "Tin Star", 41, 12, 300, 80, 180,
                    "Marshal Quickwater won't pin a badge on anyone she hasn't seen duel. Show her at the corral.",
                    Talk("Dusty", "Talk to Dusty Mae at the corral", "Dusty Mae Callahan",
                        "The marshal sent you? Then you'll want the straw dummies. They're slow, they're stuffed, and they've never once complained.",
                        "Knock a couple down so Ada can see you mean business."),
                    Fight(QuestStepKind.Defeat, 12, 2, "Defeat the straw dummies at the corral", "Straw dummies"),
                    Talk("Ada", "Report to Marshal Quickwater", "Marshal Ada Quickwater",
                        "Not bad. Not bad at all. Raise your right hand. Well, your wand hand. Close enough.",
                        "By the power vested in me by the good folks of Archrock, you're a deputy. Wear that star where folks can see it.")),
                Q(43, "A Thirsty Town", 42, 12, 360, 100, 0,
                    "Archrock has troubles: a dry pump, a railroad that won't stop for townsfolk, and outlaws on the roads. Get the lay of the land.",
                    Talk("Ezra", "Talk to Ezra the pumpwright in the town square", "Ezra Pike, Pumpwright",
                        "Deputy! Look at this. Forty years she's pumped sweet water, and the week the Dominion's railroad came through, she ran dry.",
                        "Give her a try yourself, if you don't believe me."),
                    Point(QuestStepKind.Interact, "TownPump", 1, "Try the town pump", "Town pump"),
                    Talk("Hobbs", "Talk to Stationmaster Hobbs at the depot on High Street", "Stationmaster Hobbs",
                        "The Iron Line, they call it. Dominion rails, Dominion trains, Dominion everything. They don't stop for Archrock folk anymore.",
                        "They're laying track clear across Dustwind, toward Thunder Mesa. Nobody knows why the mesa."),
                    Talk("Sal", "Talk to Sal behind the bar of the Dusty Lantern Saloon", "Sal Mesquite",
                        "You hear things, pouring sarsaparilla. The stagecoaches on Tumbleweed Row have been robbed three weeks running.",
                        "The Penny Gang, folks say. Funny thing is, they never take the gold. Only the mail and the deeds."),
                    Talk("Ada", "Report back to Marshal Quickwater", "Marshal Ada Quickwater",
                        "Water gone, trains gone, and outlaws stealing land deeds. Somebody wants folks gone from Dustwind, and I'd bet my boots it's the Dominion.",
                        "There's an old story about a sun-crystal under Thunder Mesa, the Heartstone, that keeps this whole land alive. I'm starting to think it's more than a story.",
                        "Start with Tumbleweed Row. The trail's on the east side of Front Street.")));
        }

        /// <summary>The Stagecoach Robberies (Tumbleweed Row, quests 44-47): the Penny Gang, the stolen mail, Bad Penny and her Dominion silver.</summary>
        public static void FillStagecoachRobberies(Questline ql)
        {
            Line(ql, "The Stagecoach Robberies", 47,
                Q(44, "Held Up", 43, 13, 420, 110, 0,
                    "The Penny Gang has been robbing the Archrock Express on Tumbleweed Row. Hattie Wells, the express agent, needs help.",
                    Talk("Hattie", "Find Hattie Wells at the stagecoach stop on Tumbleweed Row", "Hattie Wells, Express Agent",
                        "You're the new deputy? Thank goodness. They hit the noon coach again. Rustlers, wands drawn, bold as brass.",
                        "Run them off the Row and I'll owe you a lifetime of free postage."),
                    Fight(QuestStepKind.Defeat, 29, 4, "Defeat Penny Gang Rustlers", "Penny Gang Rustlers"),
                    Talk("Hattie", "Return to Hattie Wells", "Hattie Wells, Express Agent",
                        "That'll make them think twice. Not three times, mind you. Penny's lot aren't great thinkers.")),
                Q(45, "Return to Sender", 44, 13, 460, 120, 170,
                    "The rustlers dumped the sacks of stolen mail all over Tumbleweed Row. Find them before the wind does.",
                    Point(QuestStepKind.Interact, "MailSack", 4, "Recover the dropped mail sacks", "Mail sack"),
                    Talk("Hattie", "Bring the mail back to Hattie Wells", "Hattie Wells, Express Agent",
                        "Letters, parcels... but look. Every land deed is gone. They took the deeds and left the money orders. That's no ordinary robbery.",
                        "Here, the Express Office ring. We give it to anyone who saves the mail. You've earned it twice over.")),
                Q(46, "Tracks in the Dust", 45, 13, 500, 130, 0,
                    "Banker Ledbetter thinks the deeds are going somewhere. Follow the wagon tracks to the Penny Gang's hideout.",
                    Talk("Ledbetter", "Talk to Banker Ledbetter outside the bank", "Banker Ledbetter",
                        "Deeds? Gone? Then mark my words, someone means to buy up this whole Row the minute folks give up and leave.",
                        "The gang's wagon is heavy. Silver, maybe. Follow its tracks north and you'll find their nest."),
                    Point(QuestStepKind.Interact, "WagonTracks", 3, "Follow the heavy wagon tracks", "Wagon tracks"),
                    Point(QuestStepKind.Explore, "Hideout", 1, "Find the Penny Gang's hideout at the north end of the Row", "The hideout"),
                    Talk("Ledbetter", "Tell Banker Ledbetter what you found", "Banker Ledbetter",
                        "A box canyon, and Bad Penny herself guarding a strongbox? Then that's where the answers are. Go carefully, deputy.")),
                Q(47, "Bad Penny", 46, 14, 900, 260, 0,
                    "Bad Penny guards a strongbox in her hideout. Beat her and see what she's been paid with.",
                    Fight(QuestStepKind.Boss, 30, 1, "Defeat Bad Penny in the hideout", "Bad Penny"),
                    Point(QuestStepKind.Interact, "SilverChest", 1, "Open Bad Penny's strongbox", "Strongbox"),
                    Talk("Ada", "Bring the strongbox's secret to Marshal Quickwater in Archrock", "Marshal Ada Quickwater",
                        "Dominion silver. Stamped with that iron sun. Penny was paid to scare folks off the land the railroad wants.",
                        "So that's how they do it: buy the outlaws, steal the deeds, and lay their track over whatever's left.",
                        "Rosa Calder sent word from Coyote Gulch: the creek's gone dry. The trail's on the west end of Front Street.")));
        }

        /// <summary>Bone Dry (Coyote Gulch, quests 48-51): the dry creek, the wild critters, the Dominion dam and Old Mesquite.</summary>
        public static void FillBoneDry(Questline ql)
        {
            Line(ql, "Bone Dry", 51,
                Q(48, "Dry Creek", 47, 14, 520, 130, 0,
                    "Coyote Gulch's creek has dried up overnight. Rosa Calder, who ranches there, wants to know why.",
                    Talk("Rosa", "Find Rosa Calder at her ranch in Coyote Gulch", "Rosa Calder, Rancher",
                        "Deputy. I've lived here all my life and that creek has never once gone dry. Not even in the drought of '62.",
                        "Walk the creek bed for me. Something's not right about how it went."),
                    Point(QuestStepKind.Interact, "CreekStone", 3, "Look at the cracked stones in the dry creek bed", "Cracked creek stone"),
                    Talk("Rosa", "Tell Rosa what you saw", "Rosa Calder, Rancher",
                        "Cracked from the top down, all of them. That means the water stopped coming, it didn't dry up. Something upstream is holding it back.")),
                Q(49, "Gone Wild", 48, 14, 560, 140, 0,
                    "With no water, the gulch's critters have gone wild. Calm them down so Rosa can get her cattle to the hills.",
                    Fight(QuestStepKind.Defeat, 31, 4, "Defeat Coyote Bandits", "Coyote Bandits"),
                    Fight(QuestStepKind.Defeat, 32, 3, "Defeat Cactus Brutes", "Cactus Brutes"),
                    Talk("Gus", "Talk to Prospector Gus by the water tower", "Prospector Gus",
                        "Wild? They're not wild, they're thirsty. Same as me. I went up the gulch last week to see why. Saw a wall. A big Dominion wall.",
                        "A dam, sonny. Right across the top of the gulch.")),
                Q(50, "The Dam", 49, 15, 620, 150, 0,
                    "The Dominion has dammed the top of Coyote Gulch to send the water to the railhead's boilers. Open the sluice gates.",
                    Fight(QuestStepKind.Defeat, 33, 2, "Defeat the Sand Scorpions guarding the dam", "Sand Scorpions"),
                    Point(QuestStepKind.Explore, "TheDam", 1, "Reach the foot of the Dominion dam", "The dam"),
                    Point(QuestStepKind.Interact, "SluiceGate", 3, "Open the dam's three sluice gates", "Sluice gate"),
                    Talk("Rosa", "Tell Rosa the gates are open", "Rosa Calder, Rancher",
                        "Open? Then why isn't it running? Wait. Listen. Something big is moving down by the dam pool.",
                        "Old Mesquite. The oldest cactus in the gulch. Folks say he's been here longer than the creek. If he's gone wild too...")),
                Q(51, "Old Mesquite", 50, 15, 1000, 280, 171,
                    "Old Mesquite, the giant cactus who has guarded the gulch for centuries, has been driven wild by thirst. Calm him, and let the creek run.",
                    Fight(QuestStepKind.Boss, 34, 1, "Defeat Old Mesquite in the dam pool", "Old Mesquite"),
                    Point(QuestStepKind.Explore, "FlowingCreek", 1, "Watch the creek run again below the dam", "The creek"),
                    Talk("Rosa", "Return to Rosa Calder", "Rosa Calder, Rancher",
                        "Listen to that. Water. Real water, running over the stones. You've saved this ranch, deputy. This whole gulch.",
                        "Take these boots. My father's. They've crossed that creek a thousand times, and they'll want to again.",
                        "Ada will want to hear about the boilers. Where there's a railhead that thirsty, there's something big being built.")));
            Side(ql, 78, 81);
        }

        /// <summary>Light in the Dark (Copperbottom Mine, quests 52-55): the trapped miners, the ore-to-armor works and Foreman Grist.</summary>
        public static void FillLightInTheDark(Questline ql)
        {
            Line(ql, "Light in the Dark", 55,
                Q(52, "Copperbottom", 51, 15, 640, 150, 0,
                    "The Lantern Corps has taken over Copperbottom Mine. Shift Boss Nell Harlan has sent for help.",
                    Talk("Ada", "Talk to Marshal Quickwater in Archrock", "Marshal Ada Quickwater",
                        "A creek turned to boiler water. That railroad's hungrier than I thought.",
                        "And now Nell Harlan writes that the Corps has her miners digging round the clock in Copperbottom. The trail's on the west side of High Street."),
                    Talk("Nell", "Find Shift Boss Nell Harlan at the mining camp", "Shift Boss Nell Harlan",
                        "The Corps showed up with a contract and a lot of armor. Now they've got my crews digging something called light-ore.",
                        "First thing: the Tommyknockers have gone crazy. Get them away from the camp so my folks can work."),
                    Fight(QuestStepKind.Defeat, 35, 4, "Calm the Tommyknockers around the camp", "Tommyknockers"),
                    Talk("Nell", "Return to Nell Harlan", "Shift Boss Nell Harlan",
                        "Quieter. Good. Now Cobb's got something to tell you. He won't tell me; says I'll go in after them.")),
                Q(53, "Trapped Below", 52, 16, 700, 160, 0,
                    "Three of Nell's miners are trapped deep in the mine. Go in through the sigil at the mine mouth and find them.",
                    Talk("Cobb", "Talk to Old Cobb", "Old Cobb",
                        "Three of our crew went down to the lower galleries and never came up. The Corps sealed the mine gate behind them.",
                        "The only way in now is the old sigil at the mine mouth. Take friends. It's dark down there."),
                    Point(QuestStepKind.Interact, "TrappedMiner", 3, "Find the trapped miners inside the mine (sigil at the mine mouth)", "Trapped miner"),
                    Talk("Nell", "Tell Nell her miners are safe", "Shift Boss Nell Harlan",
                        "All three? Alive? I could kiss you, deputy. I won't. But I could.",
                        "They say there's a works down there where the Corps pours the ore into armor. Empty armor. That walks.")),
                Q(54, "The Ore Works", 53, 16, 760, 170, 0,
                    "The Lantern Corps pours light-ore into empty armor in the mine, the way it did on Starfall Isle. Find the works.",
                    Fight(QuestStepKind.Defeat, 36, 4, "Defeat Hollowed Miners", "Hollowed Miners"),
                    Point(QuestStepKind.Explore, "OreWorks", 1, "Find the ore-to-armor works in the mine shaft", "The ore works"),
                    Point(QuestStepKind.Interact, "ArmorRack", 2, "Smash the racks of empty armor", "Armor rack"),
                    Talk("Cobb", "Tell Old Cobb what you found", "Old Cobb",
                        "Armor racks, all down the shaft, waiting for a spark of stolen light. Same as the Hollowed on that island of yours, I'll bet.",
                        "Foreman Grist runs it all from the smelter at the very bottom. Big man. Bigger temper.")),
                Q(55, "Foreman Grist", 54, 17, 1100, 300, 172,
                    "Foreman Grist runs the Corps' works from the smelter at the bottom of Copperbottom Mine. Shut it down.",
                    Fight(QuestStepKind.Defeat, 37, 2, "Defeat the Ore Golems in the galleries", "Ore Golems"),
                    Fight(QuestStepKind.Boss, 38, 1, "Defeat Foreman Grist in the smelter", "Foreman Grist"),
                    Talk("Nell", "Tell Nell the mine is free", "Shift Boss Nell Harlan",
                        "Grist is gone and the smelter's cold. Copperbottom belongs to the folks who dig it again.",
                        "This lamp's lit our way out of a dozen cave-ins. Take it. And deputy: Grist's papers say all that armor was headed for the railyard. For somebody called Vesper.")));
            Side(ql, 83, 84);
        }

        /// <summary>Full Steam (Iron Line Railyard, quests 56-59): the signal tower, the cattle car, Captain Vesper and the Juggernaut.</summary>
        public static void FillFullSteam(Questline ql)
        {
            Line(ql, "Full Steam", 59,
                Q(56, "End of the Line", 55, 17, 780, 170, 0,
                    "Grist's armor was bound for the Iron Line Railyard, and for Captain Vesper. Stationmaster Hobbs has a friend on the inside.",
                    Talk("Hobbs", "Talk to Stationmaster Hobbs at Archrock's depot", "Stationmaster Hobbs",
                        "Vesper? The Corps captain with the red coat? She runs the railyard now. Has some great iron engine there under guard.",
                        "Eli Barnes, the switchman, is a friend of mine. He'll help you. The railyard trail is on the east end of High Street."),
                    Talk("Eli", "Find Switchman Eli Barnes in the railyard", "Switchman Eli Barnes",
                        "Hobbs sent you? Then you're welcome, and you're in danger. The troopers at the gate check everybody.",
                        "Thin them out a little and we can talk."),
                    Fight(QuestStepKind.Defeat, 39, 4, "Defeat Dominion Troopers", "Dominion Troopers"),
                    Talk("Eli", "Return to Eli Barnes", "Switchman Eli Barnes",
                        "That's bought us some time. The engine's called the Juggernaut. Armored, armed, and Vesper's pride and joy.")),
                Q(57, "Signals Crossed", 56, 18, 840, 180, 0,
                    "Throw the signal tower's levers so no train can leave the railyard, then deal with the engineers who come to fix them.",
                    Point(QuestStepKind.Interact, "SignalLever", 3, "Throw the three signal levers round the signal tower", "Signal lever"),
                    Fight(QuestStepKind.Defeat, 40, 3, "Defeat the Lantern Corps Engineers who come running", "Lantern Corps Engineers"),
                    Talk("Eli", "Return to Eli Barnes", "Switchman Eli Barnes",
                        "Every signal in the yard says stop. They're furious. Good. Now, about finding out what they're really up to...")),
                Q(58, "Riding the Cattle Car", 57, 18, 900, 190, 0,
                    "Hide in a cattle car to get close to the engine shed, and read the Dominion's orders.",
                    Point(QuestStepKind.Explore, "CattleCar", 1, "Hide in the cattle car by the engine shed", "Cattle car"),
                    Point(QuestStepKind.Interact, "DominionOrders", 1, "Read the Dominion orders pinned on the engine shed", "Dominion orders"),
                    Fight(QuestStepKind.Defeat, 41, 2, "Defeat the Boiler Hollowed that spot you", "Boiler Hollowed"),
                    Talk("Eli", "Tell Eli what the orders say", "Switchman Eli Barnes",
                        "'The last spike is to be driven into the Heartstone itself. By order of Railmaster Calloway.' Into the Heartstone? It would drain the whole land dry!",
                        "Vesper takes the Juggernaut out at dawn. You have to stop her tonight.")),
                Q(59, "The Juggernaut", 58, 19, 1300, 340, 173,
                    "Captain Vesper and the Juggernaut wait under steam in the roundhouse. Stop them before the engine leaves the yard.",
                    Fight(QuestStepKind.Boss, 42, 1, "Defeat Captain Vesper and the Juggernaut in the roundhouse", "Captain Vesper"),
                    Talk("Eli", "Find Eli Barnes", "Switchman Eli Barnes",
                        "She jumped the Juggernaut onto the main line and was gone, laughing like it was all a game.",
                        "Here, my signal blade. Painted like a signal arm: stop. You've earned it."),
                    Talk("Ada", "Report to Marshal Quickwater", "Marshal Ada Quickwater",
                        "The Heartstone. So the old stories are true, and the Dominion means to drive a spike right into it.",
                        "Thunder Mesa, then. The Wardens there have guarded the Heartstone since before there was a town. Mind you, they don't take kindly to strangers. The trail's in the north-east.")));
            Side(ql, 80);
        }

        /// <summary>The Wardens' Trust (Thunder Mesa, quests 60-63): proving you're not with the railroad, three Warden stones and Stone Warden Prime's trial.</summary>
        public static void FillWardensTrust(Questline ql)
        {
            Line(ql, "The Wardens' Trust", 63,
                Q(60, "Up the Mesa", 59, 19, 960, 200, 0,
                    "The Heartstone lies under Thunder Mesa, guarded by stone Wardens. Skywatcher Orla knows the mesa better than anyone.",
                    Talk("Orla", "Find Skywatcher Orla at the foot of Thunder Mesa", "Skywatcher Orla",
                        "A deputy from Archrock? Then you're here about the survey stakes. The Dominion has been marking out the mesa for weeks.",
                        "The Stormhawks have been mad as hornets since. Help me calm them, and I'll take you to the Wardens."),
                    Fight(QuestStepKind.Defeat, 44, 4, "Defeat Stormhawks", "Stormhawks"),
                    Talk("Orla", "Return to Skywatcher Orla", "Skywatcher Orla",
                        "Thank you. Now, the Wardens: Elder Basalt will speak for them. Up on the middle shelf. Be polite. Be very polite.")),
                Q(61, "Not With the Railroad", 60, 20, 1020, 210, 0,
                    "Elder Warden Basalt thinks you're with the railroad. Prove otherwise by clearing the Hail Devils the survey stirred up.",
                    Talk("Basalt", "Talk to Elder Warden Basalt on the middle shelf", "Elder Warden Basalt",
                        "Iron. Smoke. Spikes in the stone. That is what the strangers from the east have brought us. And now you.",
                        "If you are not with them, prove it. The Hail Devils their digging woke are tearing at our circle."),
                    Fight(QuestStepKind.Defeat, 46, 2, "Defeat the Hail Devils", "Hail Devils"),
                    Talk("Basalt", "Return to Elder Warden Basalt", "Elder Warden Basalt",
                        "Hm. You fight for the mesa, not against it. The stone remembers that.")),
                Q(62, "Three Stones", 61, 20, 1080, 220, 0,
                    "Three Warden stones have been knocked dark by the Dominion's survey. Wake them, and quiet the Wardens who still mistrust you.",
                    Point(QuestStepKind.Interact, "WardenStone", 3, "Wake the three Warden stones", "Warden stone"),
                    Fight(QuestStepKind.Defeat, 45, 3, "Defeat the Mesa Wardens who still think you're with the railroad", "Mesa Wardens"),
                    Talk("Basalt", "Return to Elder Warden Basalt", "Elder Warden Basalt",
                        "The stones sing again. Then there is one trial left: Stone Warden Prime, on the summit, by the Heartstone gate.")),
                Q(63, "The Prime's Trial", 62, 21, 1500, 360, 174,
                    "Stone Warden Prime will test you on the summit of Thunder Mesa. Win, and the Wardens will show you how to seal the Heartstone.",
                    Fight(QuestStepKind.Boss, 47, 1, "Pass Stone Warden Prime's trial on the summit", "Stone Warden Prime"),
                    Point(QuestStepKind.Explore, "HeartstoneGate", 1, "Stand before the sealed Heartstone gate", "Heartstone gate"),
                    Talk("Basalt", "Return to Elder Warden Basalt", "Elder Warden Basalt",
                        "The Prime has judged you, and the stone has judged the Prime's judgment. You are trusted.",
                        "If the railroad's spike reaches the Heartstone, a hand must be on the stone to seal it. A trusted hand. Yours.",
                        "The railroad's last camp is past Lonesome Bend, where the ghosts walk. They know a hidden way. Take this ring; the stone goes with you.")));
            Side(ql, 79);
        }

        /// <summary>Ghosts of Lonesome Bend (quests 64-67): the ghosts' unfinished business, and the hidden trail to the fortress.</summary>
        public static void FillGhostsOfLonesomeBend(Questline ql)
        {
            Line(ql, "Ghosts of Lonesome Bend", 67,
                Q(64, "Lonesome Bend", 63, 21, 1100, 230, 0,
                    "Lonesome Bend was emptied by the Dominion's first survey. Its ghosts know a hidden way to Calloway's drill fortress.",
                    Talk("Ada", "Talk to Marshal Quickwater in Archrock", "Marshal Ada Quickwater",
                        "Lonesome Bend. My granddaddy's town, before the first survey came and everybody left. Folks say it's haunted.",
                        "The trail's on the north-west side. If anyone knows a back way to Calloway's camp, it's whoever's still there."),
                    Talk("Hollis", "Find the ghost of Deputy Hollis in Lonesome Bend", "Deputy Hollis (a ghost)",
                        "A live deputy! Well, ain't that a sight. I'm Hollis. I kept the peace here, once, and I'm still keeping it.",
                        "The restless prospectors have been haunting the street worse than ever since the drilling started. Settle them down?"),
                    Fight(QuestStepKind.Defeat, 48, 4, "Settle the Restless Prospectors", "Restless Prospectors"),
                    Talk("Hollis", "Return to Deputy Hollis", "Deputy Hollis (a ghost)",
                        "Much obliged. The hidden trail? Oh, I know it. But the townsfolk would want me to ask: help them first, and they'll help you.")),
                Q(65, "Unfinished Business", 64, 22, 1160, 240, 0,
                    "Widow Mae can't rest until her family's keepsakes are found. They're scattered all over town.",
                    Talk("Mae", "Talk to the ghost of Widow Mae on her porch", "Widow Mae (a ghost)",
                        "When the survey men came, we ran so fast we left everything. My husband's watch. My mother's brooch. My boy's harmonica.",
                        "Could you find them? Then maybe I could stop waiting by this light."),
                    Point(QuestStepKind.Interact, "Keepsake", 3, "Find Widow Mae's lost keepsakes", "Lost keepsake"),
                    Talk("Mae", "Bring the keepsakes to Widow Mae", "Widow Mae (a ghost)",
                        "Oh. Oh, they're all here. Thank you, dear. I think I can turn the porch light off now.")),
                Q(66, "Last Rites", 65, 22, 1220, 250, 0,
                    "Parson Abel wants the Phantom Outlaws of the old Jackrabbit Gang laid to rest, so his chapel bell can ring again.",
                    Talk("Abel", "Talk to the ghost of Parson Abel by the chapel", "Parson Abel (a ghost)",
                        "The Jackrabbit Gang. Rotten in life, worse after. They won't let anyone near my chapel.",
                        "Show them the way out of this world, deputy. Gently, if you can. Firmly, if you must."),
                    Fight(QuestStepKind.Defeat, 49, 3, "Lay the Phantom Outlaws to rest", "Phantom Outlaws"),
                    Talk("Abel", "Return to Parson Abel", "Parson Abel (a ghost)",
                        "Listen... the bell. Thirty years, and it rings. Go to Hollis. He's ready to show you the trail.")),
                Q(67, "The Hidden Trail", 66, 22, 1400, 300, 175,
                    "Deputy Hollis will show you the hidden trail to Calloway's drill fortress at the end of the Iron Line.",
                    Talk("Hollis", "Talk to Deputy Hollis", "Deputy Hollis (a ghost)",
                        "You've done right by Lonesome Bend. The trail's up past the west end of Dust Street, behind the old rocks. Only the dead remember it.",
                        "Take this lantern too. It was mine. It never casts a shadow, so nobody sees you coming."),
                    Point(QuestStepKind.Explore, "HiddenTrail", 1, "Find the hidden trail behind the rocks north-west of town", "The hidden trail"),
                    Talk("Ada", "Tell Marshal Quickwater about the trail", "Marshal Ada Quickwater",
                        "A way into Calloway's fortress. Then we've got him. But I'm not sending you in alone.",
                        "When you're ready, come see me. We're raising a posse.")));
            Side(ql, 82);
            ql.quests.Add(MidnightExpressQuest());
        }

        /// <summary>Water for Archrock (quests 68-70): fixing the town pump with parts from three streets, alongside the main story.</summary>
        public static void FillWaterForArchrock(Questline ql)
        {
            Line(ql, "Water for Archrock", 70,
                Offered(Q(68, "A Dry Pump", 43, 13, 380, 90, 0,
                    "Ezra Pike thinks the town pump can be fixed, if he can get the parts. First: leather washers, which the Penny Gang stole.",
                    Fight(QuestStepKind.Collect, 29, 2, "Take pump washers from Penny Gang Rustlers on Tumbleweed Row", "Penny Gang Rustlers"),
                    Talk("Ezra", "Bring the washers to Ezra", "Ezra Pike, Pumpwright",
                        "That's them! Good leather. Now I need copper pipe, and there's none better than Copperbottom's.")),
                    "Ezra",
                    "Deputy, I've been thinking. The water's not gone, it's just deeper. A better pump could reach it.",
                    "The Penny Gang took my spare washers in the last robbery. Get them back and we'll start."),
                Also(Q(69, "Pipe Dreams", 68, 16, 640, 140, 0,
                    "Ezra needs copper pipe for the pump. The Hollowed Miners in Copperbottom Mine carry plenty.",
                    Fight(QuestStepKind.Collect, 36, 2, "Take copper pipe from Hollowed Miners in Copperbottom Mine", "Hollowed Miners"),
                    Talk("Ezra", "Bring the pipe to Ezra", "Ezra Pike, Pumpwright",
                        "Beautiful. Last thing: pressure valves. The Corps engineers at the railyard carry them on their belts.")), 52),
                Also(Q(70, "Primed and Pumping", 69, 18, 900, 200, 176,
                    "Ezra needs pressure valves for the pump, from the Lantern Corps Engineers in the Iron Line Railyard.",
                    Fight(QuestStepKind.Collect, 40, 2, "Take pressure valves from Lantern Corps Engineers in the railyard", "Lantern Corps Engineers"),
                    Talk("Ezra", "Bring the valves to Ezra", "Ezra Pike, Pumpwright",
                        "That's everything. Now, the moment of truth. Give her a pump, deputy."),
                    Point(QuestStepKind.Interact, "TownPump", 1, "Work the mended town pump", "Town pump"),
                    Talk("Ezra", "Talk to Ezra", "Ezra Pike, Pumpwright",
                        "Water! Cold, sweet water! Archrock will drink tonight!",
                        "Take my hat. It's seen forty years of pumps, and never one as fine as this.")), 56));
        }

        /// <summary>Wanted (quests 71-73): a bounty-hunting chain that ends with the Jackrabbit Gang's leader.</summary>
        public static void FillWanted(Questline ql)
        {
            Line(ql, "Wanted", 73,
                Offered(Q(71, "Wanted Posters", 51, 17, 700, 160, 0,
                    "Marshal Quickwater has a stack of wanted posters and nobody to serve them. Start with the Coyote Bandits.",
                    Fight(QuestStepKind.Defeat, 31, 6, "Defeat Coyote Bandits in Coyote Gulch", "Coyote Bandits"),
                    Talk("Ada", "Report to Marshal Quickwater", "Marshal Ada Quickwater",
                        "Six bounties served. You've a knack for this. The bounty board out front will keep you busy, too.")),
                    "Ada",
                    "Deputy, every critter and crook in Dustwind has a price on its head these days, and I've only got two hands.",
                    "Fancy a little bounty hunting? Start in Coyote Gulch."),
                Also(Q(72, "Bigger Fish", 71, 18, 900, 200, 0,
                    "The marshal has bigger bounties: the Sand Scorpions of Coyote Gulch and the Ore Golems of Copperbottom.",
                    Fight(QuestStepKind.Defeat, 33, 3, "Defeat Sand Scorpions in Coyote Gulch", "Sand Scorpions"),
                    Fight(QuestStepKind.Defeat, 37, 3, "Defeat Ore Golems in Copperbottom Mine", "Ore Golems"),
                    Talk("Ada", "Report to Marshal Quickwater", "Marshal Ada Quickwater",
                        "Well, I'll be. One more, deputy. The biggest bounty in Dustwind: Gentleman Jack Rabbit and his phantom gang.")), 55),
                Also(Q(73, "Gentleman Jack", 72, 22, 1400, 320, 177,
                    "The phantom Jackrabbit Gang haunts Lonesome Bend. Serve the oldest bounty in Dustwind.",
                    Fight(QuestStepKind.Defeat, 49, 4, "Defeat Phantom Outlaws of the Jackrabbit Gang in Lonesome Bend", "Phantom Outlaws"),
                    Talk("Ada", "Report to Marshal Quickwater", "Marshal Ada Quickwater",
                        "Gentleman Jack, served at last. That bounty's older than I am.",
                        "This duster belonged to the best bounty hunter Archrock ever had. Seems right it goes to the next one.")), 64));
        }

        /// <summary>The Missing Songbook (the Dusty Lantern Saloon, quests 74-75): find the player piano's lost songs.</summary>
        public static void FillMissingSongbook(Questline ql)
        {
            Line(ql, "The Missing Songbook", 75,
                Offered(Q(74, "The Missing Songbook", 43, 13, 360, 90, 0,
                    "Sal's player-piano songbook blew out the saloon doors in a dust storm. The Tumbleweed Sprites rolled off with its pages.",
                    Fight(QuestStepKind.Collect, 28, 3, "Take songbook pages from Tumbleweed Sprites on Tumbleweed Row", "Tumbleweed Sprites"),
                    Talk("Sal", "Bring the pages to Sal", "Sal Mesquite",
                        "My old songs! Half of them, anyway. The rest went west, toward the gulch.")),
                    "Sal",
                    "That piano's played the same three songs for a month. My songbook blew out the door in the last dust storm.",
                    "The tumbleweeds took the pages, I swear it. Rolled right off with them."),
                Also(Q(75, "Encore", 74, 15, 560, 140, 0,
                    "The rest of Sal's songbook pages ended up in Coyote Gulch, in the Coyote Bandits' dens.",
                    Fight(QuestStepKind.Collect, 31, 2, "Take the last songbook pages from Coyote Bandits", "Coyote Bandits"),
                    Talk("Sal", "Bring the last pages to Sal", "Sal Mesquite",
                        "The whole book! Tonight the Dusty Lantern plays every song in it. First sarsaparilla's on me, deputy. Well, every one is.")), 48));
        }

        /// <summary>The finale (quests 76-77): gather the posse, storm Calloway's drill fortress, free Vesper and seal the Heartstone.</summary>
        public static void FillWorld2Finale(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Finale: Last Spike";
            ql.quests = new List<QuestEntry>
            {
                Q(76, "Gather the Posse", 67, 22, 1000, 200, 0,
                    "Marshal Quickwater is raising a posse to storm Calloway's drill fortress. Rally the friends you've made across Dustwind.",
                    Talk("Ada", "Talk to Marshal Quickwater", "Marshal Ada Quickwater",
                        "Everybody you've helped owes you one, deputy. Time to call it in.",
                        "Rosa, Nell, Eli. Ask them to ride with us."),
                    Talk("Rosa", "Ask Rosa Calder to ride with the posse (Coyote Gulch)", "Rosa Calder, Rancher",
                        "Ride with you? I'd ride through a dust storm backwards. My hands will hold the trail."),
                    Talk("Nell", "Ask Nell Harlan to ride with the posse (Copperbottom Mine)", "Shift Boss Nell Harlan",
                        "My crews know rock better than any Dominion driller. We'll keep their tunnels busy."),
                    Talk("Eli", "Ask Eli Barnes to ride with the posse (the railyard)", "Switchman Eli Barnes",
                        "I'll switch every track in Dustwind against them. No train is getting to that fortress."),
                    Talk("Ada", "Report to Marshal Quickwater", "Marshal Ada Quickwater",
                        "That's a posse. Now go through the hidden trail out of Lonesome Bend. The fortress sigil is at its end. We'll hold the ways out.")),
                World2FinaleQuest(),
            };
        }

        /// <summary>Quest 77, the World 2 finale (the builder adds every storyline's end to its requirements).</summary>
        public static QuestEntry World2FinaleQuest()
        {
            return Q(77, "Last Spike", 76, 22, 2400, 600, 179,
                "With Archrock's posse holding the ways out, storm Calloway's drill fortress, stop the last spike and seal the Heartstone.",
                Fight(QuestStepKind.Defeat, 50, 3, "Fight through the Dominion Drillers in the drill fortress (sigil at the hidden trail's end)", "Dominion Drillers"),
                Fight(QuestStepKind.Boss, 51, 1, "Defeat Railmaster Ironhand Calloway at the drill", "Railmaster Ironhand Calloway"),
                Point(QuestStepKind.Interact, "VesperChains", 1, "Free Captain Vesper from the overloading drill", "Vesper's chains"),
                Point(QuestStepKind.Interact, "Heartstone", 1, "Put your hand on the Heartstone and seal it", "The Heartstone"),
                Talk("Ada", "Return to Marshal Quickwater in Archrock", "Marshal Ada Quickwater",
                    "You did it. The drill's cold, the Heartstone's sealed, and Calloway's spike never touched it.",
                    "And Vesper. Chained to the drill by her own Railmaster and left to it. She's talking now, deputy. Says the Grand General is already marching on the next world.",
                    "She'll stay in Archrock's jail a good while. But I've a feeling you'll need what she knows. Here, the last spike, hammered into a wand. Dustwind thanks you."));
        }

        /// <summary>
        /// Dustwind's school side quests (78-84): one per school, each teaching a utility spell early (like Moss's Tonic).
        /// They live in the storylines of the streets where they're given.
        /// </summary>
        public static List<QuestEntry> World2SchoolQuests()
        {
            return new List<QuestEntry>
            {
                SchoolQuest(78, School.Fire, "Campfire Stories", 48, 15, 108, "Gus", 31, 4, "Coyote Bandits", "Prospector Gus",
                    "Gus says a Fire wizard who tends embers right can make every spell burn hotter. He needs coals from the Coyote Bandits' campfires.",
                    new[] { "You've got a fire wizard's look about you. Restless.", "There's a prospector's trick for stoking embers. Bring me coals from the Coyote Bandits' fires and I'll show you." },
                    "Collect smoldering coals from Coyote Bandits", "Now, breathe on them just so... There. Stoke the embers before you strike, and it'll burn twice as long."),
                SchoolQuest(79, School.Ice, "Hail and Farewell", 60, 20, 120, "Orla", 46, 3, "Hail Devils", "Skywatcher Orla",
                    "Orla knows a Warden ward that turns hail into armor. She needs the frozen hearts of Hail Devils.",
                    new[] { "An Ice wizard, up on my mesa? Then you should learn the Wardens' winter ward.", "Bring me the frozen hearts of three Hail Devils." },
                    "Collect frozen hearts from Hail Devils", "Feel the cold settle round you like a coat of stone? That's Permafrost Bulwark. It'll turn a lot of blows."),
                SchoolQuest(80, School.Storm, "Lightning in a Bottle", 56, 18, 132, "Eli", 41, 3, "Boiler Hollowed", "Switchman Eli Barnes",
                    "Eli once saw a Storm wizard mark a whole engine for the lightning. He'll show you how, for the sparks from three Boiler Hollowed.",
                    new[] { "Storm wizard, eh? I once saw one of you mark a runaway locomotive so every bolt found it.", "Get me sparks from three Boiler Hollowed and I'll show you how it's done." },
                    "Collect boiler sparks from Boiler Hollowed", "Set the spark on your enemy, like so, and the storm will always know where to strike. That's Gale Mark."),
                SchoolQuest(81, School.Life, "Water in the Desert", 49, 15, 144, "Rosa", 32, 4, "Cactus Brutes", "Rosa Calder, Rancher",
                    "Rosa's grandmother knew a Life charm that could water a whole herd. It needs the sweet water inside Cactus Brutes.",
                    new[] { "You've a healer's touch. My grandmother had it too.", "She'd make a wellspring for a whole herd from cactus water. Bring me some from the Cactus Brutes." },
                    "Collect cactus water from Cactus Brutes", "There. Let it well up and spill over everyone near you. Wellspring, she called it."),
                SchoolQuest(82, School.Death, "Marked for the Grave", 64, 22, 156, "Abel", 48, 4, "Restless Prospectors", "Parson Abel (a ghost)",
                    "Parson Abel knows a mark the dead use to find their way. He needs grave dust from four Restless Prospectors.",
                    new[] { "A Death wizard. The dead notice you, you know. They're curious.", "Bring me grave dust from the Restless Prospectors and I'll teach you the mark they follow." },
                    "Collect grave dust from Restless Prospectors", "Mark an enemy so, and every blow finds it more easily. Dread Mark. Use it gently."),
                SchoolQuest(83, School.Myth, "Tall Tales", 52, 16, 168, "Juniper", 35, 4, "Tommyknockers", "Grandma Juniper",
                    "Grandma Juniper tells a tall tale about a gaze that tears down any shield. She needs the Tommyknockers' little hammers to prove it.",
                    new[] { "Myth, is it? Then you'll like my story about the Tommyknocker king and his gaze that broke every shield.", "Bring me four of their hammers from Copperbottom and I'll show you it's true." },
                    "Collect tiny hammers from Tommyknockers", "Look at your enemy, really look, and watch their shields crack. Sundering Gaze. Every word of it true."),
                SchoolQuest(84, School.Balance, "Weighed and Measured", 53, 17, 180, "Dusty", 37, 3, "Ore Golems", "Dusty Mae Callahan",
                    "Dusty Mae weighs every horse in the corral with a Balance trick: three blades at once. She needs ore cores from Ore Golems.",
                    new[] { "Balance, huh? My daddy was Balance. He could sharpen three wands with one spell.", "Bring me ore cores from three Ore Golems and I'll show you." },
                    "Collect ore cores from Ore Golems", "Three blades, one spell. Elemental Tri-Blade. Daddy would be proud."),
            };
        }

        private static QuestEntry SchoolQuest(int id, School school, string title, int prereq, int level, int spell, string giver, int monster, int count,
            string monsters, string giverName, string description, string[] offer, string objective, string thanks)
        {
            var q = Offered(Q(id, title, prereq, level, 300 + level * 20, 60 + level * 3, 0, description,
                Fight(QuestStepKind.Collect, monster, count, objective, monsters),
                Talk(giver, "Return to " + giverName, giverName, thanks)), giver, offer);
            q.onlyForSchool = true;
            q.school = school;
            q.rewardSpell = spell;
            return q;
        }

        /// <summary>Quest 85, offered by Old Silas from level 22: ring the station bell and beat the Midnight Express.</summary>
        public static QuestEntry MidnightExpressQuest()
        {
            return Offered(Q(85, "The Midnight Express", 64, 22, 1600, 360, 0,
                    "Old Silas, the ghost stationmaster of Lonesome Bend, says the midnight train still comes if you ring the station bell. It has never once left with an empty seat.",
                    Fight(QuestStepKind.Boss, 52, 1, "Ring the station bell and defeat the Midnight Express", "Station Bell"),
                    Talk("Silas", "Tell Old Silas the train is stopped", "Old Silas, Stationmaster (a ghost)",
                        "It's gone quiet. Thirty years that whistle's blown at midnight, and tonight it didn't.",
                        "You've given every soul on that train a ticket home. Here, this is from all of us.")),
                "Silas",
                "You look like someone who doesn't scare easy. Good. Listen.",
                "Every night at midnight, the old express comes through. A ghost train, full of folks who never got off.",
                "I've untied the bell rope. Ring it, and it'll stop for you. Then it's you or the train.");
        }

        // ================================================================== the world

        /// <summary>Dustwind's storylines, in story order (the finale is its own questline).</summary>
        public static readonly string[] World2StorylineNames =
        {
            "Deputized", "The Stagecoach Robberies", "Bone Dry", "Light in the Dark", "Full Steam", "The Wardens' Trust",
            "Ghosts of Lonesome Bend", "Water for Archrock", "Wanted", "The Missing Songbook",
        };

        /// <summary>World 2: Marshal Quickwater sets the goal out in A Thirsty Town (43); the finale (77) needs every storyline.</summary>
        public static void FillWorld2(WorldArc w, List<Questline> storylines)
        {
            w.includeInBuild = true;
            w.order = 2;
            w.displayName = "Dustwind";
            w.dialogueMusicTrack = "Music_Dialogue_Dominion";
            w.dialogueMusicSpeakers = new List<string> { "Railmaster Ironhand Calloway" };
            w.levelRange = "Lv 13-25";
            w.goal = World2Goal;
            w.goalQuest = 43;
            w.finaleQuest = 77;
            w.storylines = new List<Questline>(storylines);
            w.zones = new List<string> { "Archrock", "TumbleweedRow", "CoyoteGulch", "CopperbottomMine", "IronLineRailyard", "ThunderMesa", "LonesomeBend" };
        }
    }
}
