using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// World 3, Bigdig (see "The Everweave: Main Story Plan"): the kobolds' homeland, a city of neon, brass and gold
    /// that runs under everything. The town of Bigdig (Lodestone Square) and six boroughs, each a gang's turf: Sprocket
    /// Row (the Bigdig Prospecting Company), the Rustworks (the Rustclaw Diggers' Union), the Fusewick Canals (the
    /// Brassknuckle Syndicate), the Glimmer Mile (the Glimmerfang Gang), the Deep Seams (where the Dominion's siphons
    /// drain the Mother Lode; a dungeon) and Highroller Heights, whose Golden Seam Casino is the last dungeon: the
    /// Assayer and Marshal Brannoch, the Iron Wall, on the casino roof over the whole city, above the Mother Lode. Monsters 60-88,
    /// storylines and finale 130-166, the world arc. Its arch in the portal room opens with Dustwind's finale (quest 77).
    /// </summary>
    public static partial class WorldDefaults
    {
        public const string World3Goal =
            "Someone is draining the Mother Lode, the singing gold under Bigdig, while the kobold gangs blame each other. Find out who, and make the gangs see it before Bigdig goes dark.";

        // ================================================================== monsters

        /// <summary>Bigdig's monsters (ids 60-88): regulars rank 5, elites 6, bosses 6-7. Dominion troops fight with the Tech School.</summary>
        public static List<MonsterEntry> World3Monsters()
        {
            return new List<MonsterEntry>
            {
                // Sprocket Row
                R(M(60, "Scrap Rat", School.Storm, 25, 1650, false, 0, new[] { 124, 125, 128, 130 }, 180, 30, 52, 260, 15, 0.8f, new Color(0.55f, 0.5f, 0.45f),
                    "Sprocket Row. Rats that nest in scrap heaps and chew through copper wire. Drop scrap (quest 162)."), 5, "Beast", false),
                R(M(61, "Company Collector", School.Myth, 25, 1700, false, 0, new[] { 160, 161, 164, 165 }, 185, 32, 54, 261, 15, 1f, new Color(0.75f, 0.62f, 0.25f),
                    "Sprocket Row. Madame Ozza's debt collectors, some of them on the take."), 5, "Kobold", false),
                R(M(62, "Brassknuckle Bruiser", School.Fire, 26, 1800, false, 0, new[] { 100, 101, 104, 106 }, 190, 34, 56, 260, 15, 1.15f, new Color(0.6f, 0.5f, 0.2f),
                    "Sprocket Row. Syndicate muscle muscling in on Company turf."), 5, "Kobold", false),
                R(M(63, "Copperjaw Grubb", School.Fire, 27, 7600, true, 1900, new[] { 100, 101, 104, 106, 107, 200 }, 1350, 270, 420, 253, 40, 1.35f, new Color(0.7f, 0.45f, 0.2f),
                    "Sprocket Row boss at the top of the Row, paid in Reach scrip (quest 136)."), 6, "Kobold", false),
                // The Rustworks
                R(M(64, "Slag Elemental", School.Fire, 27, 1900, false, 0, new[] { 100, 104, 106, 107 }, 195, 34, 58, 260, 15, 1.2f, new Color(0.85f, 0.35f, 0.12f),
                    "The Rustworks. Molten slag that crawled out of a furnace and kept going."), 5, "Construct", false),
                R(M(65, "Rogue Drill-Bot", School.Tech, 27, 1850, false, 0, new[] { 300, 301, 304, 308 }, 195, 34, 58, 261, 15, 0.9f, new Color(0.5f, 0.55f, 0.6f),
                    "The Rustworks. Dominion drilling drones, sold to the Union and turned loose."), 5, "Construct", false),
                R(M(66, "Dominion Sapper", School.Tech, 28, 2200, false, 0, new[] { 300, 301, 303, 304, 306 }, 220, 38, 62, 260, 15, 1.05f, new Color(0.42f, 0.42f, 0.48f),
                    "The Rustworks. Vanguard engineers who lay charges under the furnaces. Elite."), 6, "Dominion", true),
                R(M(67, "Drillmaster Vane", School.Tech, 28, 8200, true, 2000, new[] { 300, 301, 304, 306, 310, 200 }, 1450, 280, 440, 254, 40, 1.25f, new Color(0.4f, 0.38f, 0.44f),
                    "The Rustworks boss, with the Siphon Drill, in the old smelter yard (quest 140)."), 6, "Dominion", false),
                R(M(68, "The Siphon Drill", School.Tech, 28, 3000, false, 0, new[] { 300, 304, 306, 307 }, 320, 0, 0, 0, 0, 1.3f, new Color(0.3f, 0.3f, 0.34f),
                    "Vane's walking drill. Fights beside him."), 6, "Construct", true),
                // The Fusewick Canals
                R(M(69, "Sludge Newt", School.Death, 28, 2000, false, 0, new[] { 148, 149, 152, 154 }, 200, 36, 60, 260, 15, 0.95f, new Color(0.35f, 0.45f, 0.3f),
                    "The Fusewick Canals. Newts as long as a barge pole that live in the sludge."), 5, "Beast", false),
                R(M(70, "Syndicate Fuse-Runner", School.Storm, 29, 2050, false, 0, new[] { 124, 125, 128, 130 }, 205, 36, 60, 261, 15, 1f, new Color(0.55f, 0.6f, 0.3f),
                    "The Fusewick Canals. Brassknuckle smugglers with very short fuses."), 5, "Kobold", false),
                R(M(71, "Threadfleet Marine", School.Tech, 29, 2150, false, 0, new[] { 300, 301, 304, 306 }, 215, 38, 62, 260, 15, 1.1f, new Color(0.3f, 0.36f, 0.5f),
                    "The Fusewick Canals. Admiral Kell's marines, guarding the Syndicate's barges."), 5, "Dominion", false),
                R(M(72, "Canal Gator", School.Life, 30, 2500, false, 0, new[] { 136, 137, 140, 142, 143 }, 240, 42, 68, 261, 15, 1.3f, new Color(0.3f, 0.5f, 0.32f),
                    "The Fusewick Canals, by the locks. Elite. Drop teeth (quest 163)."), 6, "Beast", true),
                R(M(73, "Lieutenant Sorrel", School.Tech, 30, 8800, true, 2100, new[] { 300, 301, 304, 306, 307, 200 }, 1550, 300, 460, 255, 40, 1.2f, new Color(0.28f, 0.32f, 0.46f),
                    "The Fusewick Canals boss on the Threadfleet barge in the locks (quest 144)."), 6, "Dominion", false),
                // The Glimmer Mile
                R(M(74, "Neon Wisp", School.Storm, 30, 2100, false, 0, new[] { 124, 125, 126, 128, 130 }, 210, 38, 62, 260, 15, 0.85f, new Color(0.95f, 0.35f, 0.85f),
                    "The Glimmer Mile. Stray sparks from the signs, given a mind of their own."), 5, "Spirit", false),
                R(M(75, "Glimmerfang Lampthief", School.Myth, 30, 2150, false, 0, new[] { 160, 161, 164, 165 }, 215, 40, 64, 261, 15, 0.95f, new Color(0.55f, 0.3f, 0.7f),
                    "The Glimmer Mile. Young Glimmerfangs stealing neon for someone else's coin. Drop dice (quest 165)."), 5, "Kobold", false),
                R(M(76, "Silent Office Shadow", School.Death, 31, 2600, false, 0, new[] { 148, 149, 152, 154, 155 }, 250, 44, 70, 260, 15, 1.05f, new Color(0.2f, 0.2f, 0.26f),
                    "The Glimmer Mile rooftops. The Dominion's spies. Elite."), 6, "Dominion", true),
                R(M(77, "Agent Nightglass", School.Death, 31, 9400, true, 2200, new[] { 148, 149, 152, 154, 157, 200 }, 1650, 320, 480, 256, 40, 1.15f, new Color(0.15f, 0.15f, 0.22f),
                    "The Glimmer Mile boss on the arcade roof: the Silent Office's handler in Bigdig (quest 148)."), 6, "Dominion", false),
                // The Deep Seams
                R(M(78, "Seam Crawler", School.Death, 31, 2300, false, 0, new[] { 148, 149, 150, 152 }, 225, 40, 66, 260, 15, 1.1f, new Color(0.42f, 0.36f, 0.3f),
                    "The Deep Seams. Blind, many-legged things that follow the warmth of the gold."), 5, "Beast", false),
                R(M(79, "Hollowed Digger", School.Death, 32, 2400, false, 0, new[] { 148, 149, 152, 155 }, 235, 42, 68, 261, 15, 1.05f, new Color(0.4f, 0.38f, 0.36f),
                    "The Deep Seams. Empty kobold digging rigs, run on stolen light."), 5, "Hollowed", false),
                R(M(80, "Lantern Corps Siphoneer", School.Tech, 32, 2450, false, 0, new[] { 300, 301, 304, 306, 308 }, 240, 42, 68, 260, 15, 1f, new Color(0.6f, 0.25f, 0.18f),
                    "The Deep Seams. Engineers who tend the siphons."), 5, "Dominion", false),
                R(M(81, "Siphon Colossus", School.Tech, 33, 3600, false, 0, new[] { 300, 304, 306, 307, 311 }, 360, 50, 80, 261, 20, 1.6f, new Color(0.35f, 0.35f, 0.4f),
                    "The siphon works. A walking siphon engine. Elite."), 6, "Construct", true),
                R(M(82, "Chief Engineer Strake", School.Tech, 33, 10200, true, 2400, new[] { 301, 304, 306, 310, 311, 200 }, 1800, 340, 520, 257, 40, 1.2f, new Color(0.55f, 0.2f, 0.15f),
                    "The Deep Seams boss at the heart of the siphon works (quest 152)."), 7, "Dominion", false),
                // Highroller Heights and the Golden Seam Casino
                R(M(83, "Casino Bouncer", School.Balance, 33, 2700, false, 0, new[] { 172, 173, 176, 178 }, 260, 46, 72, 260, 15, 1.25f, new Color(0.45f, 0.3f, 0.4f),
                    "Highroller Heights. The casino's doormen; nobody gets in without a chip. Carry marked cards (quest 166)."), 6, "Kobold", false),
                R(M(84, "Jackpot Golem", School.Balance, 34, 3800, false, 0, new[] { 172, 173, 176, 178, 179 }, 380, 54, 84, 261, 20, 1.5f, new Color(0.9f, 0.75f, 0.3f),
                    "The Golden Seam Casino. A golem of coins and slot-machine parts. Elite."), 6, "Construct", true),
                R(M(85, "Lightforged Legionnaire", School.Tech, 34, 2900, false, 0, new[] { 300, 301, 302, 304, 306 }, 280, 48, 76, 260, 15, 1.15f, new Color(0.5f, 0.5f, 0.56f),
                    "The Golden Seam Casino. Brannoch's line soldiers in light-charged iron."), 6, "Dominion", false),
                R(M(86, "The Assayer", School.Myth, 34, 10800, true, 2500, new[] { 160, 161, 165, 166, 169, 200 }, 1900, 360, 560, 258, 40, 1f, new Color(0.8f, 0.75f, 0.55f),
                    "The Golden Seam Casino, on the gaming floor: Quibb Silvernose, the broker who sold Bigdig's gold (quest 161)."), 7, "Kobold", false),
                R(M(87, "Marshal Brannoch, the Iron Wall", School.Tech, 35, 14000, true, 3000, new[] { 300, 302, 304, 306, 307, 313 }, 2400, 450, 700, 259, 40, 1.5f, new Color(0.32f, 0.3f, 0.34f),
                    "Bigdig's last boss, in the high-roller vault over the Mother Lode, with his shieldbearers (quest 161)."), 7, "Dominion", false),
                R(M(88, "Iron Wall Shieldbearer", School.Ice, 35, 3200, false, 0, new[] { 112, 113, 116, 117, 118 }, 300, 0, 0, 0, 0, 1.25f, new Color(0.4f, 0.42f, 0.5f),
                    "Brannoch's shield wall. Fights beside him."), 6, "Dominion", true),
            };
        }

        // ================================================================== zones

        /// <summary>Bigdig's look: soot-dark stone and brass under a cavern sky, lit by neon and furnaces (each borough tweaks its own).</summary>
        private static void UndercityLook(ZoneDefinition z)
        {
            z.world = 3;
            z.textureStyle = ZoneTextureStyle.Undercity;
            z.cellSize = 8f;
            z.groundColor = new Color(0.36f, 0.33f, 0.32f);
            z.roadColor = new Color(0.46f, 0.42f, 0.4f);
            z.grassColor = new Color(0.32f, 0.38f, 0.3f);
            z.buildingColor = new Color(0.46f, 0.38f, 0.34f);
            z.roofColor = new Color(0.3f, 0.26f, 0.24f);
            z.lowWallColor = new Color(0.4f, 0.36f, 0.32f);
            z.treeColor = new Color(0.3f, 0.36f, 0.28f);
            z.accentColor = new Color(1f, 0.78f, 0.3f);
            z.buildingHeight = new Vector2(8f, 14f);
            z.lampDensity = 0.45f;
            // A cavern sky: the roof far overhead, lit warm by the city's furnaces.
            z.skyTopColor = new Color(0.08f, 0.06f, 0.08f);
            z.skyHorizonColor = new Color(0.55f, 0.32f, 0.2f);
            z.fogColor = new Color(0.26f, 0.18f, 0.16f);
            z.horizonDistance = 1000f;
            z.railings = RailingMode.Drops;
            z.heightStep = 1.5f;
        }

        private static ZoneLandmark Tunnel(string symbol, string target, int quest, string hint)
        {
            return new ZoneLandmark { symbol = symbol, kind = LandmarkKind.StreetGate, targetZone = target, requiredQuest = quest, lockedHint = hint };
        }

        /// <summary>
        /// Bigdig (the town, cells of 6 m): a star of narrow, winding lanes lined with crooked buildings, all meeting at
        /// Lodestone Square, the market in the middle (the Deep Bell, the Deepwarden, the assayer's stall and the four shops
        /// round it). The west lane runs to the portal arch home (S); the others wind out to the six tunnels, and each
        /// borough stands just past its tunnel in the same cavern (Open World): Highroller Heights high above to the north,
        /// the Glimmer Mile to the north-east, Sprocket Row to the east, the Rustworks to the south, the Deep Seams far
        /// below to the south-west and the Fusewick Canals to the north-west. The Gilded Rivet Hotel stands on the west
        /// lane; a short lane south-east leads to the Brawl Pit's yard and the training yard behind it.
        /// </summary>
        public static void FillBigdig(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "Bigdig";
            z.displayName = "Bigdig";
            z.levelRange = "Lv 25-35";
            z.musicTrack = "Music_Zone_KilnStreet";
            z.isTown = true;
            z.height = 2000f;
            z.cellSize = 6f;
            // One open world: the boroughs stand round the town in the same great cavern, past its tunnels.
            z.openWorld = true;
            z.openWorldGap = 50f;
            z.lampDensity = 0.55f;
            z.map =
            "#############################################\n" +
            "########################.O.##################\n" +
            "########################...##################\n" +
            "########################...############.....#\n" +
            "########################...############....C#\n" +
            "########################...###########......#\n" +
            "#######################....#########....#####\n" +
            "#...##################....##########...######\n" +
            "#M.......#############...##########....######\n" +
            "#.............########...#########...K#######\n" +
            "######........#########X.########....########\n" +
            "###########....########...######....#########\n" +
            "#############....######...###.......#########\n" +
            "#########........##oo#....###....############\n" +
            "#########.........#oo....#nn...+#############\n" +
            "#########....K............nn..###############\n" +
            "#########.............A......################\n" +
            "#.....######.......G.....X...################\n" +
            "#S.w.X..###..................################\n" +
            "#.............*.......W..........############\n" +
            "######......####............E.........#######\n" +
            "#######LLLLLL####...B...P....#.......X......#\n" +
            "#######LLLLLL####............######........R#\n" +
            "#######LLQLLL###..............#########.....#\n" +
            "#######LLLLLL#....#mm...jj##..KUUUU....######\n" +
            "#######LLLLLL#...##mm..#jj###..UUUU....######\n" +
            "#############....###...######..UUUU.D..######\n" +
            "############....####+..######..UUUU....######\n" +
            "###########....#####...######..........######\n" +
            "##########....#######...######=====,,,=######\n" +
            "#########.K...########...#####=,,,,,,,=######\n" +
            "#######.....##########...#####=,aaaaa,=######\n" +
            "######.....###########...#####=,a,,,a,=######\n" +
            "#####.....############..######=,a,1,a,=######\n" +
            "#.......#############...######=,a,,,a,=######\n" +
            "#Y.....##############...######=,aaaaa,=######\n" +
            "#.....###############...######=,,,,,,,=######\n" +
            "#####################.N.######=========######\n" +
            "#############################################\n";
            z.npcs = new List<ZoneNpc>
            {
                Npc("A", "Bruma", "Deepwarden Bruma Stonehum", new Color(0.36f, 0.3f, 0.5f), new Color(0.85f, 0.7f, 0.3f), false,
                    "Bigdig was built on the singing gold, wizard. Every street, every lamp, every quarrel. And now it's going quiet.",
                    "The gangs won't listen to an old Deepwarden anymore. Maybe they'll listen to you."),
                Npc("E", "RunnerPip", "Runner Pip Quickfuse", new Color(0.7f, 0.4f, 0.15f), new Color(0.45f, 0.25f, 0.1f), false,
                    "Need a message run? A parcel? A tunnel nobody else knows? Pip's your kobold. Fastest legs under the world!"),
                Npc("D", "Krunk", "Krunk, Brawl Pit Boss", new Color(0.5f, 0.2f, 0.18f), new Color(0.3f, 0.12f, 0.1f), true,
                    "The Brawl Pit's open to anyone with a wand and a death wish. The dummies take bets. Kidding. Mostly."),
                Npc("Q", "Mags", "Mags, Manager of the Gilded Rivet Hotel", new Color(0.62f, 0.45f, 0.2f), new Color(0.4f, 0.28f, 0.12f), false,
                    "Welcome to the Gilded Rivet, finest hotel under the world. First fizz is free for the wizard. The rooms are not. The lift is broken. Don't ask."),
                Npc("G", "Grindle", "Grandpa Grindle", new Color(0.45f, 0.42f, 0.5f), new Color(0.3f, 0.28f, 0.34f), true,
                    "When I was a pup, the gold sang so loud you couldn't sleep. Now? I sleep fine. That's what scares me."),
                new ZoneNpc
                {
                    symbol = "B", key = "Quibb", displayName = "Assayer Quibb Silvernose", robeColor = new Color(0.8f, 0.75f, 0.6f), hatColor = new Color(0.25f, 0.22f, 0.2f),
                    facing = -1f, hideAfterQuest = 154,
                    defaultDialogue = new[] { "Gold weighed, gold valued, gold bought. Fair prices for honest kobolds. And for the others, fairer prices." },
                },
            };
            z.points = new List<ZonePoint>
            {
                Pt("W", "DeepBell", ZonePointKind.Interact, ZonePointLook.StandingStone, "The Deep Bell"),
                Pt("w", "BigdigArrival", ZonePointKind.Explore, ZonePointLook.None, "", 3f),
                Pt("K", "BigdigBarrels", ZonePointKind.Decoration, ZonePointLook.BlastBarrel, ""),
                Pt("X", "SquareNeon", ZonePointKind.Decoration, ZonePointLook.NeonSign, ""),
                Pt("P", "LodeSpout", ZonePointKind.Decoration, ZonePointLook.GoldPile, ""),
            };
            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "PitDummies", roster = new List<int> { 12 }, wanderers = 2, patrolSpeed = 0.5f, respawnSeconds = 10, evenFight = true },
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "L", kind = LandmarkKind.Hotel, label = "The Gilded Rivet Hotel" },
                new ZoneLandmark { symbol = "U", kind = LandmarkKind.DuelCircle, label = "The Brawl Pit" },
                // Shops, in the order of their wares: robes and hats, wands, charms and rings, potions and decks.
                new ZoneLandmark { symbol = "o", kind = LandmarkKind.Shop, label = "Gearwright's Outfitters", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "n", kind = LandmarkKind.Shop, label = "Sparkwick Wands", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "j", kind = LandmarkKind.Shop, label = "Glitter & Gold", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "m", kind = LandmarkKind.Shop, label = "Fuse & Fizz Potions", doorSide = DoorSide.Centre },
                new ZoneLandmark { symbol = "+", kind = LandmarkKind.HealthOrb },
                new ZoneLandmark { symbol = "*", kind = LandmarkKind.ManaOrb },
                Tunnel("R", "SprocketRow", 132, "Locked: the Deepwarden wants a word first"),
                Tunnel("N", "Rustworks", 136, "Locked: sort out the Company's trouble on Sprocket Row first"),
                Tunnel("M", "FusewickCanals", 140, "Locked: help the Rustclaw Union first"),
                Tunnel("C", "GlimmerMile", 144, "Locked: follow the smugglers on the Fusewick Canals first"),
                Tunnel("Y", "DeepSeams", 148, "Locked: find out who's arming the Glimmerfangs first"),
                Tunnel("O", "HighrollerHeights", 156, "Locked: the gangs have to stand together first"),
            };
        }

        /// <summary>Sprocket Row (Lv 25-27, east of the town): the Company's bazaar, one long street snaking back and forth through the market, stalls and wagons all along it; Copperjaw's lot where the first pass turns, Collectors' Corner, the Bruiser Lot and the Scrap Yard between the passes, and Madame Ozza's counting house terrace at the far end.</summary>
        public static void FillSprocketRow(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "SprocketRow";
            z.displayName = "Sprocket Row";
            z.levelRange = "Lv 25-27";
            z.musicTrack = "Music_Zone_KilnStreet";
            z.height = 2100f;
            z.accentColor = new Color(1f, 0.8f, 0.25f);
            z.fogColor = new Color(0.32f, 0.21f, 0.18f);
            z.skyHorizonColor = new Color(0.67f, 0.36f, 0.19f);
            z.skyTopColor = new Color(0.1f, 0.07f, 0.1f);
            z.map =
            "##############################\n" +
            "##############################\n" +
            "#...K...N.....W##########$..X#\n" +
            "#..............##########....#\n" +
            "#S.....1.aaaa..##########....#\n" +
            "#...........a..#####.........#\n" +
            "#...H.......a.K#####.........#\n" +
            "##########.....#####........##\n" +
            "##########.....#####..T#######\n" +
            "####..........N#####...#######\n" +
            "####........2..#####...#######\n" +
            "####......K......W..N......W##\n" +
            "####...###..b...............##\n" +
            "####...###..bbb..6.ffff.....##\n" +
            "####..T###..................##\n" +
            "####...###W....N.......N...K##\n" +
            "####...################.....##\n" +
            "####...################..4..T#\n" +
            "##M......M#############.....##\n" +
            "##...c....#############P.d.N##\n" +
            "##...c....#############..d..##\n" +
            "##........#############..d..##\n" +
            "##...3....#######..K.........#\n" +
            "##........#######...........Z#\n" +
            "##........#######............#\n" +
            "##........#######............#\n" +
            "##M..$...M#######......5.....#\n" +
            "#################............#\n" +
            "#################............#\n" +
            "#################W...N......C#\n" +
            "##############################\n" +
            "##############################\n";
            z.heightMap =
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000001111111111110\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n";
            // Its place in Bigdig's open world: metres above (+) or below (-) the town.
            z.openWorldRise = 2f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("Z", "Ozza", "Madame Ozza Goldtongue", new Color(0.85f, 0.65f, 0.15f), new Color(0.5f, 0.12f, 0.12f), false,
                    "The Bigdig Prospecting Company has dug, sold and shipped gold under every world there is, darling. Mostly legally."),
                Npc("H", "Fenna", "Fenna Sprocketsnap, Market Fixer", new Color(0.4f, 0.5f, 0.35f), new Color(0.25f, 0.32f, 0.22f), false,
                    "You want it, I can get it. You want it real? That costs extra."),
                Npc("P", "Pobble", "Pobble the Shoeshine", new Color(0.55f, 0.4f, 0.3f), new Color(0.4f, 0.28f, 0.2f), false,
                    "Shine, wizard? Two coppers! I'll even do the toes. You got toes in there, right?"),
            };
            z.points = new List<ZonePoint>
            {
                Pt("C", "ScripStash", ZonePointKind.Interact, ZonePointLook.Crate, "Copperjaw's strongbox"),
                Pt("X", "BackAlley", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("T", "ScripTrail", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Spilled scrip"),
                Pt("M", "CounterfeitCrate", ZonePointKind.Interact, ZonePointLook.Crate, "Crate of 'gold'"),
                Pt("W", "OreWagon", ZonePointKind.Decoration, ZonePointLook.Wagon, ""),
                Pt("K", "RowBarrels", ZonePointKind.Decoration, ZonePointLook.BlastBarrel, ""),
                Pt("N", "RowNeon", ZonePointKind.Decoration, ZonePointLook.NeonSign, ""),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "GateRats", 3, 1.2f, 60, 60),
                Enc("2", "CollectorsCorner", 3, 1.1f, 61, 60),
                Enc("3", "CountingHouse", 3, 1.1f, 61, 61),
                Enc("4", "BruiserLot", 3, 1.2f, 62, 61, 62),
                Boss("5", "Copperjaw", 63, 62, 62),
                Enc("6", "ScrapYard", 3, 1.1f, 62, 60),
            };
            // Treasure caches at the ends of side alleys.
            z.landmarks = new List<ZoneLandmark> { new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" } };
        }

        /// <summary>The Rustworks (Lv 27-28, south of the town): a rigid factory grid. Conveyor Avenue runs right across the top, with the Union Hall court and Vane's drill yard off it; below, works blocks with narrow alleys and yards cut into them, and the Union Smokestack over the great furnace at the bottom.</summary>
        public static void FillRustworks(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "Rustworks";
            z.displayName = "The Rustworks";
            z.levelRange = "Lv 27-28";
            z.musicTrack = "Music_Zone_KilnStreet";
            z.height = 2200f;
            z.buildingColor = new Color(0.5f, 0.34f, 0.26f);
            z.accentColor = new Color(1f, 0.5f, 0.2f);
            z.fogColor = new Color(0.32f, 0.18f, 0.12f);
            z.map =
            "##################################\n" +
            "###############..S..##############\n" +
            "###############.....##############\n" +
            "###K...........E....##############\n" +
            "###.................##############\n" +
            "###..aaaaaa.1.......##############\n" +
            "###..a..............##############\n" +
            "###..a.............K##############\n" +
            "###.....##########################\n" +
            "###............V....X#############\n" +
            "###................$O#############\n" +
            "###..2..##########################\n" +
            "###K...W........W.....K....#######\n" +
            "###..b.....................#######\n" +
            "###..bbb...3.cccc...6.fff..#######\n" +
            "###.....................f..#######\n" +
            "###rrrrrrrrrrrrrrrrrrrrrfrr#######\n" +
            "########...###########....r#######\n" +
            "########...###########....K#######\n" +
            "########...###########..4.r#######\n" +
            "##$..........G########....r#######\n" +
            "##............########..d..#######\n" +
            "##............#...K.....d....U####\n" +
            "##........U...#...............####\n" +
            "##............#.............W.####\n" +
            "##YYY.........#......5........####\n" +
            "##YYY.........#..............J####\n" +
            "##YYY..G.....G#.U...W.........####\n" +
            "##################################\n" +
            "##################################\n";
            // Its place in Bigdig's open world: metres above (+) or below (-) the town.
            z.openWorldRise = -3f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("E", "Hesk", "Grandmaw Hesk Rustclaw", new Color(0.62f, 0.32f, 0.16f), new Color(0.4f, 0.2f, 0.1f), false,
                    "The Rustclaw Union has dug by hand for three hundred years. Pick, lamp and spit. We don't need fancy machines."),
                Npc("V", "Vix", "Smelter Vix", new Color(0.45f, 0.3f, 0.25f), new Color(0.3f, 0.2f, 0.15f), true,
                    "Furnace six has been coughing blue all week. Blue's bad. Blue means somebody's been messing with my flues."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("r", "ConveyorRails", ZonePointKind.Decoration, ZonePointLook.Rails, ""),
                Pt("U", "SlagTanks", ZonePointKind.Decoration, ZonePointLook.WaterTower, ""),
                Pt("W", "OreCarts", ZonePointKind.Decoration, ZonePointLook.OreCart, ""),
                Pt("J", "SiphonDrillRig", ZonePointKind.Decoration, ZonePointLook.DrillRig, ""),
                Pt("K", "WorksBarrels", ZonePointKind.Decoration, ZonePointLook.BlastBarrel, ""),
                Pt("G", "FurnaceValve", ZonePointKind.Interact, ZonePointLook.Signpost, "Furnace valve"),
                Pt("X", "UnionHall", ZonePointKind.Explore, ZonePointLook.None, "", 1.5f),
                Pt("O", "UnionLedger", ZonePointKind.Interact, ZonePointLook.Signpost, "Union ledger"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "GateSlag", 3, 1f, 64, 64),
                Enc("2", "MiddleWorks", 3, 1.1f, 65, 64),
                Enc("3", "FoundryLine", 3, 1f, 64, 65, 64),
                Enc("4", "FurnaceWatch", 3, 1f, 66, 65),
                Boss("5", "DrillmasterVane", 67, 68, 66),
                Enc("6", "EastSmelters", 3, 1.1f, 65, 66),
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" },
                new ZoneLandmark { symbol = "Y", kind = LandmarkKind.Tower, label = "Union Smokestack" },
            };
        }

        /// <summary>The Fusewick Canals (Lv 28-30, north-west of the town, a little lower): wharf quarters cut apart by water. The Grand Canal runs north under the Threadfleet's pier to the canal mouth; two cross canals split the wharfs into quarters joined by causeway bridges; the old drained basin lies sunk below the south-west wharf.</summary>
        public static void FillFusewickCanals(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "FusewickCanals";
            z.displayName = "The Fusewick Canals";
            z.levelRange = "Lv 28-30";
            z.musicTrack = "Music_Zone_TidewatchDocks";
            z.height = 2300f;
            z.cornerRadius = 1.5f;
            z.grassColor = new Color(0.3f, 0.36f, 0.26f);
            z.accentColor = new Color(0.55f, 1f, 0.45f);
            z.fogColor = new Color(0.16f, 0.2f, 0.13f);
            z.skyHorizonColor = new Color(0.46f, 0.25f, 0.13f);
            z.skyTopColor = new Color(0.07f, 0.05f, 0.07f);
            z.map =
            "################################\n" +
            "##############~~~###############\n" +
            "##...........#~~~#$..........X##\n" +
            "##...........#~~~#............##\n" +
            "##...........#~~~#.........N..##\n" +
            "##....5.....F#~~~#.....7.ggg..##\n" +
            "##...........#~~~#.........g.U##\n" +
            "##...........#~~~#.........g..##\n" +
            "##...........#~~~#............##\n" +
            "#####.....####~~~#G...........##\n" +
            "#####..c..####~~~#########...###\n" +
            "#####..c.K####~~~#########...###\n" +
            "#####.....####~~~########K...###\n" +
            "#####..3..####~~~#########...###\n" +
            "#####.....####~~~#########...###\n" +
            "#####....N####~~~#########...###\n" +
            "#####.....####~~~########G...###\n" +
            "###~~.....~~~~~~~~~~~~~~~~...###\n" +
            "###~~.....~~~~~~~~~~~~~~~~...###\n" +
            "#####.....####~~~#########...###\n" +
            "#####.....###G~~~#########...###\n" +
            "#####K.d..........N......#...###\n" +
            "#####..d.................#...###\n" +
            "#####..ddd.4........fff..#...###\n" +
            "#####.................f......###\n" +
            "#####N................f......###\n" +
            "########..C###~~~###K.f......###\n" +
            "########...###~~~###.....#######\n" +
            "########...###~~~###..6..#######\n" +
            "###C........M#~~~###.....#######\n" +
            "###..........#~~~###.....#######\n" +
            "###..........#~~~###.....#######\n" +
            "###....2.bb..#~~~###..a...N....#\n" +
            "###..........#~~~###..a........#\n" +
            "###..........#~~~###..aa.1....S#\n" +
            "###..........#~~~###...........#\n" +
            "###$........C#~~~###W...K...B..#\n" +
            "##############~~~###############\n" +
            "##############~~~###############\n" +
            "################################\n";
            // Its place in Bigdig's open world: metres above (+) or below (-) the town.
            z.openWorldRise = -6f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("B", "Pinch", "\"Big Pinch\" Brassknuckle", new Color(0.55f, 0.6f, 0.3f), new Color(0.3f, 0.32f, 0.15f), true,
                    "The Brassknuckle Syndicate moves anything, anywhere, no questions asked. You're asking questions. I don't love it."),
                Npc("M", "SludgeMarla", "Sludge-Pilot Marla", new Color(0.35f, 0.45f, 0.5f), new Color(0.22f, 0.3f, 0.34f), false,
                    "I've piloted these canals since I could hold a pole. Lately the barges ride low, and they don't come back."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("G", "LockGate", ZonePointKind.Interact, ZonePointLook.Signpost, "Lock gate wheel"),
                Pt("X", "TheLocks", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("C", "SmuggledCrate", ZonePointKind.Interact, ZonePointLook.Crate, "Smuggled crate"),
                Pt("F", "CanalMouth", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("W", "PumpHouse", ZonePointKind.Decoration, ZonePointLook.Windmill, ""),
                Pt("U", "CanalTank", ZonePointKind.Decoration, ZonePointLook.WaterTower, ""),
                Pt("K", "CanalBarrels", ZonePointKind.Decoration, ZonePointLook.BlastBarrel, ""),
                Pt("N", "CanalNeon", ZonePointKind.Decoration, ZonePointLook.NeonSign, ""),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "WharfNewts", 2, 1.2f, 69, 69),
                Enc("2", "RunnerDen", 3, 1.3f, 70, 69, 70),
                Enc("3", "WestWharf", 3, 1.1f, 70, 69),
                Enc("4", "MarineWatch", 3, 1f, 71, 70),
                Boss("5", "LieutenantSorrel", 73, 71, 72),
                Enc("6", "EastWharf", 3, 1.1f, 71, 71),
                Enc("7", "CanalBed", 3, 1.1f, 72, 69),
            };
            // Treasure caches at the ends of side alleys.
            z.landmarks = new List<ZoneLandmark> { new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" } };
        }

        /// <summary>The Glimmer Mile (Lv 30-31, north-east of the town): one long neon boulevard running north, raised terraces of clubs on both sides, the Middle Arcade halfway, and the Glimmerfangs' rooftop perch at the far end where Agent Nightglass waits.</summary>
        public static void FillGlimmerMile(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "GlimmerMile";
            z.displayName = "The Glimmer Mile";
            z.levelRange = "Lv 30-31";
            z.musicTrack = "Music_Zone_LanternRow";
            z.height = 2400f;
            z.cornerRadius = 2f;
            z.lampDensity = 0.7f;
            z.buildingColor = new Color(0.36f, 0.3f, 0.42f);
            z.roofColor = new Color(0.22f, 0.18f, 0.28f);
            z.accentColor = new Color(1f, 0.35f, 0.85f);
            z.skyHorizonColor = new Color(0.55f, 0.2f, 0.5f);
            z.fogColor = new Color(0.22f, 0.12f, 0.26f);
            z.map =
            "########################\n" +
            "########################\n" +
            "########################\n" +
            "###.........K..#########\n" +
            "###...........N#########\n" +
            "###......5.....#########\n" +
            "###............#########\n" +
            "###............#########\n" +
            "###............#########\n" +
            "###............#########\n" +
            "###N...........#########\n" +
            "###.....################\n" +
            "###.....##....F..X######\n" +
            "###...............######\n" +
            "###...............######\n" +
            "###.....##..W....$######\n" +
            "###.....################\n" +
            "###..d..################\n" +
            "###N.d..################\n" +
            "###............B....W###\n" +
            "###..4..............$###\n" +
            "###.....################\n" +
            "###.....################\n" +
            "###.....################\n" +
            "###..c..################\n" +
            "###..c.....N.....#######\n" +
            "###..c..........K#######\n" +
            "###..cc.3..ff....#######\n" +
            "###..............#######\n" +
            "###....K......6..#######\n" +
            "############....W#######\n" +
            "############.....#######\n" +
            "############..b..#######\n" +
            "############..b.N#######\n" +
            "############.....#######\n" +
            "############..2..#######\n" +
            "#...N.....K......#######\n" +
            "#................#######\n" +
            "#S....1.aaa......#######\n" +
            "#................#######\n" +
            "#..O........N....#######\n" +
            "########################\n" +
            "########################\n";
            z.heightMap =
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "444444444444444444444444\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "222222222222222222222222\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n" +
            "000000000000000000000000\n";
            z.heightStep = 2f;
            // Its place in Bigdig's open world: metres above (+) or below (-) the town.
            z.openWorldRise = 4f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("O", "Tixi", "Tixi Glimmerfang", new Color(0.55f, 0.25f, 0.75f), new Color(0.3f, 0.12f, 0.42f), false,
                    "The Glimmer Mile never sleeps, wizard. Well. It naps. Mostly in the afternoons."),
                Npc("B", "Flick", "Doc Flick, Arcade Owner", new Color(0.3f, 0.5f, 0.6f), new Color(0.2f, 0.32f, 0.4f), true,
                    "Every sign on the Mile runs on a sliver of singing gold. When the gold goes quiet, the lights go out. Simple as that."),
                Npc("F", "Lou", "Lucky Lou", new Color(0.6f, 0.2f, 0.2f), new Color(0.15f, 0.15f, 0.15f), true,
                    "Lost my lucky dice to some Glimmerfang pup. Haven't won a hand since. Coincidence? I think not."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("W", "DarkNeon", ZonePointKind.Interact, ZonePointLook.StreetLamp, "Dark neon lamp"),
                Pt("N", "MileNeon", ZonePointKind.Decoration, ZonePointLook.NeonSign, ""),
                Pt("K", "MileBarrels", ZonePointKind.Decoration, ZonePointLook.BlastBarrel, ""),
                Pt("X", "Rooftop", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "LowerWisps", 3, 1.4f, 74, 74),
                Enc("2", "LowerThieves", 3, 1.2f, 75, 74),
                Enc("3", "WestTerrace", 3, 1.1f, 75, 75),
                Enc("4", "EastTerrace", 3, 1.2f, 76, 74),
                Boss("5", "AgentNightglass", 77, 76, 75),
                Enc("6", "MiddleArcade", 3, 1.1f, 76, 75),
            };
            // Treasure caches at the ends of side alleys.
            z.landmarks = new List<ZoneLandmark> { new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" } };
        }

        /// <summary>The Deep Seams (Lv 31-33, south-west of the town, far below it): a mine stepping down in ledges, switching back and forth: the diggers' camp, the West Seam, the deep gallery with the East and Main Seams and Bigdig's dead ward, and the Siphon Works at the very bottom.</summary>
        public static void FillDeepSeams(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "DeepSeams";
            z.displayName = "The Deep Seams";
            z.levelRange = "Lv 31-33";
            z.musicTrack = "Music_Zone_CopperbottomMine";
            z.height = 2500f;
            z.lampDensity = 0.4f;
            z.groundColor = new Color(0.3f, 0.27f, 0.26f);
            z.buildingColor = new Color(0.38f, 0.32f, 0.3f);
            z.accentColor = new Color(1f, 0.85f, 0.4f);
            z.fogColor = new Color(0.14f, 0.1f, 0.1f);
            z.map =
            "##############################\n" +
            "##############################\n" +
            "##$.N...O###............K....#\n" +
            "##.......###.................#\n" +
            "##.......###....aaaa.1......S#\n" +
            "##.......###.................#\n" +
            "##.......###.................#\n" +
            "##...............#############\n" +
            "##...............#############\n" +
            "##...............#############\n" +
            "##K.....K###......############\n" +
            "############.....#############\n" +
            "####..........2..#############\n" +
            "####.............#############\n" +
            "####.....bbbb....#############\n" +
            "####.............#############\n" +
            "####.............#############\n" +
            "####..3..#########M.......W###\n" +
            "####.....#########.........###\n" +
            "####..c..#########....6....###\n" +
            "####..c..#########.........###\n" +
            "####................fff....###\n" +
            "####M.............M.......$###\n" +
            "####.....#####################\n" +
            "####.....................K####\n" +
            "####......................####\n" +
            "####..............4..dddd.####\n" +
            "####.....{................####\n" +
            "####................K.....####\n" +
            "#############|||##############\n" +
            "#############...##############\n" +
            "###.........;.}............###\n" +
            "###.......eeeeeeeee........###\n" +
            "###.F.....e.......e...J....###\n" +
            "###.......e...5...e........###\n" +
            "###.......e.......e........###\n" +
            "###.......eeeeeeeee.....J..###\n" +
            "###........................###\n" +
            "##############################\n" +
            "##############################\n";
            z.heightMap =
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n" +
            "0000000000bbbbbbbb000000000000\n" +
            "0000000000bbbbbbbb000000000000\n" +
            "0000000000bbbbbbbb000000000000\n" +
            "bbbbbbbbbbbbbbbbbb000000000000\n" +
            "bbbbbbbbbbbbbbbbbb000000000000\n" +
            "bbbbbbbbbbbbbbbbbb000000000000\n" +
            "bbbbbbbbbbbbbbbbbb000000000000\n" +
            "bbbbbbbbbbbbbbbbbb000000000000\n" +
            "bbbbbbbbdddddddddddddddddddd00\n" +
            "bbbbbbbbdddddddddddddddddddd00\n" +
            "bbbbbbbbdddddddddddddddddddd00\n" +
            "bbbbbbbbdddddddddddddddddddd00\n" +
            "dddddddddddddddddddddddddddddd\n" +
            "dddddddddddddddddddddddddddddd\n" +
            "dddddddddddddddddddddddddddddd\n" +
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeee\n" +
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeee\n" +
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeee\n" +
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeee\n" +
            "eeeeeeeeeeeeeeeeeeeeeeeeeeeeee\n" +
            "0000000000000eee00000000000000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000eeeeeeeeeeeeeeeeeeeeeeee000\n" +
            "000000000000000000000000000000\n" +
            "000000000000000000000000000000\n";
            z.heightStep = 3f;
            // Its place in Bigdig's open world: metres above (+) or below (-) the town.
            z.openWorldRise = -30f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("N", "Mott", "Old Digger Mott", new Color(0.42f, 0.36f, 0.3f), new Color(0.85f, 0.7f, 0.25f), true,
                    "Sixty years down here. I know every seam by its song. Lately they're all humming the same sad note."),
                Npc("O", "Kib", "Surveyor Kib", new Color(0.35f, 0.4f, 0.5f), new Color(0.24f, 0.28f, 0.36f), false,
                    "There are runes in the deep tunnels older than Bigdig. Nobody knows what they're for. I intend to."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("K", "SeamCarts", ZonePointKind.Decoration, ZonePointLook.OreCart, ""),
                Pt("F", "SiphonEngine", ZonePointKind.Decoration, ZonePointLook.DrillRig, ""),
                Pt("M", "WardRune", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Ward rune"),
                Pt("W", "SiphonWorks", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("J", "SiphonValve", ZonePointKind.Interact, ZonePointLook.Crate, "Siphon valve"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "CampCrawlers", 3, 1f, 78, 78),
                Enc("2", "CampDiggers", 3, 1f, 79, 78),
                Enc("3", "WestSeam", 3, 0.9f, 79, 79),
                Enc("4", "EastSeam", 3, 0.9f, 80, 79),
                new ZoneEncounter
                {
                    ringSymbol = "5", key = "ChiefEngineerStrake", roster = new List<int> { 82, 81, 80 }, wanderers = 1, patrolSpeed = 0.7f, respawnSeconds = 30, scaleWithParty = true,
                    introLines = new[]
                    {
                        "!Chief Engineer Strake|A wizard. In my works. Do you have any idea what this machine is worth?",
                        "!Chief Engineer Strake|Every seam in Bigdig flows through here. Every note of that precious gold, bottled and shipped to the Crucible.",
                        "!Chief Engineer Strake|The kobolds can sing for themselves. The Reach needs it more. Colossus! Remove the guest.",
                    },
                },
                Enc("6", "MainSeam", 3, 0.9f, 80, 81),
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" },
                new ZoneLandmark { symbol = "{", kind = LandmarkKind.DungeonSigil, label = "The Siphon Works" },
                new ZoneLandmark { symbol = "}", kind = LandmarkKind.DungeonStart },
                new ZoneLandmark { symbol = ";", kind = LandmarkKind.DungeonExit },
                new ZoneLandmark { symbol = "|", kind = LandmarkKind.DungeonGate },
            };
        }

        /// <summary>Highroller Heights (Lv 33-35, north of the town, high above it): a plateau of mansions round the Strip, with the Golden Seam Casino along its south edge, its gold facade and tower looking out over all of Bigdig.</summary>
        public static void FillHighrollerHeights(ZoneDefinition z)
        {
            UndercityLook(z);
            z.zoneKey = "HighrollerHeights";
            z.displayName = "Highroller Heights";
            z.levelRange = "Lv 33-35";
            z.musicTrack = "Music_Zone_LanternRow";
            z.height = 2600f;
            z.lampDensity = 0.65f;
            z.groundColor = new Color(0.3f, 0.24f, 0.28f);
            z.roadColor = new Color(0.5f, 0.38f, 0.32f);
            z.buildingColor = new Color(0.4f, 0.28f, 0.3f);
            z.roofColor = new Color(0.55f, 0.42f, 0.18f);
            z.accentColor = new Color(1f, 0.82f, 0.3f);
            z.skyHorizonColor = new Color(0.28f, 0.15f, 0.06f);
            z.fogColor = new Color(0.12f, 0.08f, 0.07f);
            z.skyTopColor = new Color(0.04f, 0.03f, 0.04f);
            z.map =
            "####################################\n" +
            "####################################\n" +
            "####################################\n" +
            "####################################\n" +
            "####################################\n" +
            "####################################\n" +
            "###$...L...L.T....N........L.....K##\n" +
            "###...............................##\n" +
            "###X..bb.2...aa.1.....ccc.3.......##\n" +
            "###...............................##\n" +
            "###O...D.N..K..L.........T...N...N##\n" +
            "###################.....#####.....##\n" +
            "###################.....#####..d..##\n" +
            "###eeeeeeefffffffL#.....N####K.d.L##\n" +
            "###eZ...Tef.....f.#......####..d..##\n" +
            "###e..5..ef..6..fD#......####..d..##\n" +
            "###e.....ef.....f.#..{...####N....##\n" +
            "###eeeeeeefffffff.|.....M####..4..##\n" +
            "###.......ggggggg}|......####.....##\n" +
            "###L.....Lg.....g.|......####....D##\n" +
            "###...T...g..7..g.#N.....####.....##\n" +
            "###.V..D..g.....g;###########P....##\n" +
            "###L.....Lggggggg.#..............N##\n" +
            "###################....H..........##\n" +
            "###################.S.............##\n" +
            "####################################\n";
            z.heightMap =
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "000000000000000000000000000000000000\n" +
            "0000000000000000000000000000bbbbbbb0\n" +
            "0000000000000000000bbbbbbbbbbbbbbbb0\n" +
            "0000000000000000000bbbbbbbbbbbbbbbb0\n" +
            "0000000000000000000bbbbbbbbbbbbbbbb0\n" +
            "0000000000000000000bbbbbbbbbbbbbbbb0\n" +
            "000000000000000000000000000000000000\n";
            // Its place in Bigdig's open world: metres above (+) or below (-) the town.
            z.openWorldRise = 45f;
            z.npcs = new List<ZoneNpc>
            {
                Npc("H", "Snick", "Snick, Casino Doorman", new Color(0.2f, 0.2f, 0.24f), new Color(0.85f, 0.7f, 0.25f), true,
                    "The Golden Seam welcomes all high rollers. Low rollers, medium rollers and wizards, please form an orderly line. Over there. Far over there."),
                Npc("M", "Lulu", "Lulu, Croupier", new Color(0.7f, 0.15f, 0.2f), new Color(0.15f, 0.12f, 0.12f), false,
                    "Place your bets, sweetie. The house always wins. I should know. I'm the house's favourite niece."),
                Npc("P", "Pockets", "Old Pockets, Retired Cardsharp", new Color(0.4f, 0.35f, 0.25f), new Color(0.25f, 0.2f, 0.15f), true,
                    "Forty years at those tables and I never once cheated. Well. Never once got caught. Same thing, legally."),
            };
            z.points = new List<ZonePoint>
            {
                Pt("L", "SlotMachines", ZonePointKind.Decoration, ZonePointLook.SlotMachine, ""),
                Pt("T", "CardTables", ZonePointKind.Decoration, ZonePointLook.CardTable, ""),
                Pt("D", "RouletteWheels", ZonePointKind.Decoration, ZonePointLook.Roulette, ""),
                Pt("O", "Jackpot", ZonePointKind.Decoration, ZonePointLook.GoldPile, ""),
                Pt("N", "StripNeon", ZonePointKind.Decoration, ZonePointLook.NeonSign, ""),
                Pt("K", "LuckyChip", ZonePointKind.Interact, ZonePointLook.GlowingItem, "Lucky chip"),
                Pt("X", "CasinoBackDoor", ZonePointKind.Explore, ZonePointLook.None, "", 2f),
                Pt("V", "VaultLever", ZonePointKind.Interact, ZonePointLook.Crate, "Vault lever"),
                Pt("Z", "MotherLode", ZonePointKind.Interact, ZonePointLook.GlowingItem, "The Mother Lode"),
            };
            z.encounters = new List<ZoneEncounter>
            {
                Enc("1", "StripBouncers", 3, 1f, 83, 83),
                Enc("2", "WestStrip", 3, 1f, 83, 84),
                Enc("3", "EastStrip", 3, 1f, 84, 83),
                Enc("4", "BackLot", 3, 0.9f, 83, 83, 84),
                new ZoneEncounter
                {
                    ringSymbol = "5", key = "MarshalBrannoch", roster = new List<int> { 87, 88, 88, 85 }, wanderers = 1, patrolSpeed = 0.6f, respawnSeconds = 30, scaleWithParty = true,
                    arena = "Rooftop",   // fought on the casino's roof, over the whole city (the lift is in the high-roller vault)
                    introLines = new[]
                    {
                        "!Marshal Brannoch, the Iron Wall|Look at it, wizard. All of Bigdig, every lamp in it, and every one burning on gold the Reach has already bought.",
                        "!Marshal Brannoch, the Iron Wall|So. The little wizard, and every gang in Bigdig behind you. The Assayer said you were trouble. For once he undersold something.",
                        "Madame Ozza Goldtongue|That's our gold under your boots, Marshal. Every last singing ounce of it.",
                        "!Marshal Brannoch, the Iron Wall|Your gold? It's the Reach's warmth. Its bread. Its children's light. You kobolds bet it on dice.",
                        "Grandmaw Hesk Rustclaw|We dug it. We sang to it. We'll be the ones keeping it.",
                        "!Marshal Brannoch, the Iron Wall|The Grand General asked me for one thing: hold this house. I have never lost a position I was told to hold.",
                        "!Marshal Brannoch, the Iron Wall|Shields up! Let them break on the Iron Wall.",
                    },
                },
                Enc("6", "CasinoFloorWest", 3, 1f, 85, 84),
                new ZoneEncounter
                {
                    ringSymbol = "7", key = "TheAssayer", roster = new List<int> { 86, 85, 84 }, wanderers = 1, patrolSpeed = 0.7f, respawnSeconds = 30, scaleWithParty = true,
                    introLines = new[]
                    {
                        "!The Assayer|Ah. The wizard. You're early. I hadn't finished counting.",
                        "!The Assayer|Do you know what the Reach pays for an ounce of singing gold? More than Bigdig ever paid me for honesty.",
                        "!The Assayer|Let's make a wager. If you win, you get the vault. If I win, I get to weigh you. Spin the wheel!",
                    },
                },
            };
            z.landmarks = new List<ZoneLandmark>
            {
                new ZoneLandmark { symbol = "$", kind = LandmarkKind.Cache, label = "" },
                new ZoneLandmark { symbol = "{", kind = LandmarkKind.DungeonSigil, label = "The Golden Seam Casino" },
                new ZoneLandmark { symbol = "}", kind = LandmarkKind.DungeonStart },
                new ZoneLandmark { symbol = ";", kind = LandmarkKind.DungeonExit },
                new ZoneLandmark { symbol = "|", kind = LandmarkKind.DungeonGate },
            };
        }

        // ================================================================== storylines
        // A dialogue line "Name|words" is spoken by Name (another character in the scene); "!Name|words" by an enemy.

        /// <summary>Under Everything (Bigdig, quests 130-132): through the third arch, the Deepwarden, the gangs and the quiet gold.</summary>
        public static void FillUnderEverything(Questline ql)
        {
            Line(ql, "Under Everything", 132,
                Q(130, "The Third Arch", 77, 25, 1600, 320, 0,
                    "With the Heartstone sealed, the third arch in the portal room has woken, and a kobold runner came up through the old door under Copperbottom Mine asking for you. Step through.",
                    Point(QuestStepKind.Explore, "BigdigArrival", 1, "Step through the World 3 arch in the portal room, beneath the Headmaster's tower", "Bigdig"),
                    Talk("RunnerPip", "Find the runner who came looking for you", "Runner Pip Quickfuse",
                        "You came! I ran all the way up to that dusty mine to fetch you and you actually came! Pip Quickfuse, fastest legs under the world.",
                        "Welcome to Bigdig, wizard. Mind the neon, mind the pickpockets, and mind your wallet. Actually, mind your wallet twice.",
                        "The Deepwarden sent me. She's at her hall at the top of the square. Come on, come on!"),
                    Talk("Bruma", "Meet the Deepwarden at the top of Lodestone Square", "Deepwarden Bruma Stonehum",
                        "So you're the wizard from the dream-tales. Marshal Quickwater's letters say you stopped a railroad. Let's see if you can stop a city.",
                        "Bigdig runs on gold. Not coins: the singing gold, the Mother Lode under all our feet. It lights our lamps and warms our forges.",
                        "And it's going quiet, seam by seam. Every gang in the city blames every other. I need someone none of them owns.")),
                Q(131, "Gang Colours", 130, 25, 1100, 240, 0,
                    "Four gangs carve up Bigdig, and the Deepwarden wants you to know who's who before you go poking around their turf.",
                    Talk("Mags", "Talk to Mags at the Gilded Rivet Hotel", "Mags, Manager of the Gilded Rivet Hotel",
                        "Gangs? Sweetie, Bigdig IS gangs. Gold for the Company, rust for the Union, brass for the Syndicate and neon for the Glimmerfangs.",
                        "They used to just argue over claims. Now there are fights every night, and everybody swears somebody else started it."),
                    Talk("Krunk", "Talk to Krunk at the Brawl Pit", "Krunk, Brawl Pit Boss",
                        "You want to walk the boroughs, you'd better be able to throw a spell. Show the dummies what you've got."),
                    Fight(QuestStepKind.Defeat, 12, 2, "Knock down the Brawl Pit dummies", "Brawl Pit dummies"),
                    Talk("Krunk", "Report to Krunk", "Krunk, Brawl Pit Boss",
                        "Not bad! You'll last at least a week out there. That's a compliment. Most last a day.")),
                Q(132, "The Quiet Gold", 131, 25, 1300, 280, 242,
                    "Grandpa Grindle says the old Deep Bell in the square used to ring by itself when the gold sang. Try it, then tell the Deepwarden what you heard.",
                    Talk("Grindle", "Talk to Grandpa Grindle in the square", "Grandpa Grindle",
                        "The Deep Bell? Cast from the first gold ever dug in Bigdig. When the Mother Lode sang, the bell sang back. Day and night.",
                        "Go on. Strike it."),
                    Point(QuestStepKind.Interact, "DeepBell", 1, "Strike the Deep Bell in Lodestone Square", "The Deep Bell"),
                    Talk("Bruma", "Tell the Deepwarden what you heard", "Deepwarden Bruma Stonehum",
                        "Nothing. A dull clank. I feared as much. The gold isn't running out, wizard. Something is drinking it.",
                        "Start with Sprocket Row. Madame Ozza of the Company swears the Syndicate is passing fake gold on her turf. The tunnel's on the east side of the square.",
                        "Take this lamp. It was lit from the Mother Lode the year I became Deepwarden. Let's hope it stays lit.")));
        }

        /// <summary>Company Business (Sprocket Row, quests 133-136): fake gold, Reach scrip and Copperjaw Grubb.</summary>
        public static void FillCompanyBusiness(Questline ql)
        {
            Line(ql, "Company Business", 136,
                Q(133, "The Company Line", 132, 25, 1400, 300, 0,
                    "Madame Ozza Goldtongue runs the Bigdig Prospecting Company from Sprocket Row. She says Brassknuckle muscle is shaking down her market.",
                    Talk("Ozza", "Find Madame Ozza on Sprocket Row", "Madame Ozza Goldtongue",
                        "The Deepwarden's wizard! Darling, you have impeccable timing. Brassknuckle bruisers are shaking down every stall on my Row.",
                        "Persuade them to leave. Persuasively. I'll pay. Well. I'll consider paying."),
                    Fight(QuestStepKind.Defeat, 62, 4, "Defeat Brassknuckle Bruisers on Sprocket Row", "Brassknuckle Bruisers"),
                    Talk("Ozza", "Return to Madame Ozza", "Madame Ozza Goldtongue",
                        "Lovely work. Now, the real problem. Someone's flooding my market with gold that doesn't sing. Fenna will tell you.")),
                Q(134, "Fool's Gold", 133, 26, 1450, 310, 0,
                    "Fenna Sprocketsnap, the market fixer, knows where the fake gold is coming in.",
                    Talk("Fenna", "Talk to Fenna Sprocketsnap", "Fenna Sprocketsnap, Market Fixer",
                        "Fake gold, real problem. Painted lead in Company crates, sold as the real thing. Kobolds are losing their life savings.",
                        "Crack open the crates round the Row. Let's see what's really inside."),
                    Point(QuestStepKind.Interact, "CounterfeitCrate", 4, "Open the crates of 'gold' around Sprocket Row", "Crate of 'gold'"),
                    Talk("Fenna", "Bring Fenna what you found", "Fenna Sprocketsnap, Market Fixer",
                        "Lead, lead, lead... and this. Paper money with a black gauntlet closing on a star. That's not kobold scrip.",
                        "Somebody's paying for fake gold with foreign money. And somebody's buying the real stuff.")),
                Q(135, "Follow the Scrip", 134, 26, 1500, 320, 0,
                    "A trail of that foreign scrip leads up the Row. Follow it, and deal with the Company collectors who've been taking it.",
                    Point(QuestStepKind.Interact, "ScripTrail", 3, "Follow the trail of spilled scrip up the Row", "Spilled scrip"),
                    Point(QuestStepKind.Explore, "BackAlley", 1, "Find where the trail ends, in the back alley at the top of the Row", "The back alley"),
                    Fight(QuestStepKind.Defeat, 61, 3, "Defeat the Company Collectors who were taking the scrip", "Company Collectors"),
                    Talk("Ozza", "Tell Madame Ozza her own collectors were on the take", "Madame Ozza Goldtongue",
                        "My own collectors? Taking foreign paper? I'm not angry, darling. I'm disappointed. Then I'm angry.",
                        "The trail ends at Copperjaw Grubb's lot. Syndicate muscle. Of course it's the Syndicate. It's always the Syndicate.")),
                Q(136, "Copperjaw", 135, 27, 2400, 520, 243,
                    "Copperjaw Grubb holds the lot at the top of Sprocket Row, with a strongbox nobody's allowed to touch.",
                    Fight(QuestStepKind.Boss, 63, 1, "Defeat Copperjaw Grubb at the top of the Row", "Copperjaw Grubb"),
                    Point(QuestStepKind.Interact, "ScripStash", 1, "Open Copperjaw's strongbox", "Copperjaw's strongbox"),
                    Talk("Ozza", "Bring the strongbox's secret to Madame Ozza", "Madame Ozza Goldtongue",
                        "Stacks of that gauntlet scrip, and a note: 'Buy all the singing gold. Pay any price. The Assayer will weigh it.' The Assayer!",
                        "Quibb Silvernose weighs every ounce in Bigdig. If he's buying for foreigners... no. No, it has to be the Syndicate.",
                        "Here, the Company signet. You've earned a share. A small share. Rustclaw Hesk says her furnaces are being sabotaged; go and gloat at her for me.")));
        }

        /// <summary>Union Strong (the Rustworks, quests 137-140): the Rustclaw Union, sabotaged furnaces, Dominion drills and Drillmaster Vane.</summary>
        public static void FillUnionStrong(Questline ql)
        {
            Line(ql, "Union Strong", 140,
                Q(137, "Rustclaw Hall", 136, 27, 1550, 330, 0,
                    "Grandmaw Hesk Rustclaw runs the Diggers' Union in the Rustworks. Slag is crawling out of her furnaces, and she blames the Company.",
                    Talk("Hesk", "Find Grandmaw Hesk in the Rustworks", "Grandmaw Hesk Rustclaw",
                        "Ozza sent you? Ha! Ozza's been trying to buy my Union for forty years. Now somebody's wrecking my furnaces. Guess who I suspect.",
                        "First, the slag. It's crawling out of the furnaces on its own. Put it back where it belongs."),
                    Fight(QuestStepKind.Defeat, 64, 4, "Defeat Slag Elementals in the Rustworks", "Slag Elementals"),
                    Talk("Hesk", "Return to Grandmaw Hesk", "Grandmaw Hesk Rustclaw",
                        "Good. Now go and see Vix at the smelters. Furnace trouble's her trouble.")),
                Q(138, "Strike the Furnace", 137, 27, 1600, 340, 0,
                    "Smelter Vix thinks someone has been tampering with the furnace flues. Shut the valves and catch whatever's loose in the works.",
                    Talk("Vix", "Talk to Smelter Vix", "Smelter Vix",
                        "Blue flame, wizard. Blue! Somebody's opened the deep flues to pull heat downward. Shut the valves round the smokestack."),
                    Point(QuestStepKind.Interact, "FurnaceValve", 3, "Shut the furnace valves round the Union Smokestack", "Furnace valve"),
                    Fight(QuestStepKind.Defeat, 65, 3, "Defeat the Rogue Drill-Bots that come running", "Rogue Drill-Bots"),
                    Talk("Vix", "Return to Smelter Vix", "Smelter Vix",
                        "Drill-bots? We don't own drill-bots. We dig by hand! Somebody brought those into the works.")),
                Q(139, "New Partners", 138, 28, 1700, 360, 0,
                    "Hesk's grandson signed a contract with 'new partners' for drilling machines. The Union ledger will say who.",
                    Point(QuestStepKind.Explore, "UnionHall", 1, "Find the Union hall by the conveyor lines", "The Union hall"),
                    Point(QuestStepKind.Interact, "UnionLedger", 1, "Read the Union ledger", "Union ledger"),
                    Fight(QuestStepKind.Defeat, 66, 2, "Defeat the Dominion Sappers laying charges under the furnaces", "Dominion Sappers"),
                    Talk("Hesk", "Tell Grandmaw Hesk what the ledger says", "Grandmaw Hesk Rustclaw",
                        "'Ten siphon drills, on loan, from the office of Marshal Brannoch.' Who in the deep dark is Marshal Brannoch?",
                        "And those were Dominion soldiers under my furnaces. Not the Company. Not this time. Hm."),
                    Talk("Hesk", "Hear what else Hesk has to say", "Grandmaw Hesk Rustclaw",
                        "Their drillmaster is in the old smelter yard with the biggest drill I've ever seen. Pulling our heat and our gold downward.")),
                Q(140, "Drillmaster Vane", 139, 28, 2600, 560, 244,
                    "Drillmaster Vane and his Siphon Drill are in the old smelter yard. Shut them down.",
                    Fight(QuestStepKind.Boss, 67, 1, "Defeat Drillmaster Vane and the Siphon Drill in the smelter yard", "Drillmaster Vane"),
                    Talk("Hesk", "Return to Grandmaw Hesk", "Grandmaw Hesk Rustclaw",
                        "The drill's quiet. And the gold in the furnace walls is humming again. Faintly. But humming.",
                        "Take my old pick, sharpened into a blade. And wizard: the Syndicate's barges have been riding low on the canals. Big Pinch knows why. He always knows why.")));
        }

        /// <summary>Smuggler's Run (the Fusewick Canals, quests 141-144): Big Pinch, smuggled gold, the Threadfleet's marines and Lieutenant Sorrel.</summary>
        public static void FillSmugglersRun(Questline ql)
        {
            Line(ql, "Smuggler's Run", 144,
                Q(141, "Brassknuckle Business", 140, 28, 1750, 370, 0,
                    "\"Big Pinch\" Brassknuckle runs the Syndicate from the Fusewick Canals. Everyone says he's behind everything. He says the same about everyone else.",
                    Talk("Pinch", "Find Big Pinch on the Fusewick Canals", "\"Big Pinch\" Brassknuckle",
                        "The Company says I'm faking gold. The Union says I'm wrecking furnaces. You know what I'm actually doing? Fighting newts.",
                        "The sludge newts have gone mad. Clear the wharves and maybe I'll talk."),
                    Fight(QuestStepKind.Defeat, 69, 4, "Defeat Sludge Newts on the wharves", "Sludge Newts"),
                    Talk("Pinch", "Return to Big Pinch", "\"Big Pinch\" Brassknuckle",
                        "Fine. Talk to Marla. She pilots the canal. And don't touch my crates.")),
                Q(142, "Contraband", 141, 29, 1800, 380, 0,
                    "Sludge-Pilot Marla says the Syndicate's own barges are carrying something Pinch doesn't know about. Check the crates.",
                    Talk("SludgeMarla", "Talk to Sludge-Pilot Marla", "Sludge-Pilot Marla",
                        "Pinch's runners load the barges at night, and a foreign boat tows them up through the locks. Pinch thinks it's ordinary contraband.",
                        "Look in the crates. Then tell me it's ordinary."),
                    Point(QuestStepKind.Interact, "SmuggledCrate", 3, "Open the smuggled crates on the wharves", "Smuggled crate"),
                    Fight(QuestStepKind.Defeat, 70, 3, "Defeat the Syndicate Fuse-Runners guarding them", "Syndicate Fuse-Runners"),
                    Talk("SludgeMarla", "Tell Marla what's in the crates", "Sludge-Pilot Marla",
                        "Singing gold. Crates of it, packed in straw, stamped with that gauntlet. Pinch's own runners are shipping Bigdig out from under him.")),
                Q(143, "Through the Locks", 142, 29, 1850, 390, 0,
                    "The foreign boat waits beyond the locks, guarded by marines. Get past them and open the lock gates.",
                    Fight(QuestStepKind.Defeat, 71, 3, "Defeat the Threadfleet Marines on the wharves", "Threadfleet Marines"),
                    Point(QuestStepKind.Explore, "TheLocks", 1, "Reach the locks at the top of the canal", "The locks"),
                    Point(QuestStepKind.Interact, "LockGate", 3, "Turn the three lock gate wheels", "Lock gate wheel"),
                    Talk("SludgeMarla", "Return to Marla", "Sludge-Pilot Marla",
                        "The gates are open and the barge is stuck in the lock. Its lieutenant is in a temper. Go and spoil it further.")),
                Q(144, "Lieutenant Sorrel", 143, 30, 2800, 600, 245,
                    "Lieutenant Sorrel of the Threadfleet commands the barge in the locks. Stop the shipment.",
                    Fight(QuestStepKind.Boss, 73, 1, "Defeat Lieutenant Sorrel on the barge in the locks", "Lieutenant Sorrel"),
                    Point(QuestStepKind.Explore, "CanalMouth", 1, "Watch the barge sink in the canal mouth", "The canal mouth"),
                    Talk("Pinch", "Tell Big Pinch what his runners were really moving", "\"Big Pinch\" Brassknuckle",
                        "My runners. My barges. Shipping Bigdig's gold to some foreign admiral, and I never knew. That's... that's embarrassing.",
                        "Kell. The lieutenant kept saying 'Admiral Kell's fleet'. Whoever she is, she's not paying the Syndicate. Somebody is, though.",
                        "Take these. Smuggler's boots, never leave a print. And try the Glimmer Mile; the Glimmerfang pups have money they shouldn't.")));
        }

        /// <summary>Neon Nights (the Glimmer Mile, quests 145-148): Tixi Glimmerfang, stolen neon, the Silent Office and Agent Nightglass.</summary>
        public static void FillNeonNights(Questline ql)
        {
            Line(ql, "Neon Nights", 148,
                Q(145, "Glimmerfang", 144, 30, 1900, 400, 0,
                    "Tixi Glimmerfang runs the Glimmer Mile. Her lights are going out, and wisps of loose neon are running wild.",
                    Talk("Tixi", "Find Tixi Glimmerfang on the Glimmer Mile", "Tixi Glimmerfang",
                        "The old gangs send a wizard? Cute. The Mile's lights are dying and the neon's gone feral. You want to help, catch some sparks."),
                    Fight(QuestStepKind.Defeat, 74, 4, "Defeat Neon Wisps on the lower terrace", "Neon Wisps"),
                    Talk("Tixi", "Return to Tixi", "Tixi Glimmerfang",
                        "Okay, okay. You're not useless. Doc Flick at the arcade can tell you why the lights are dying.")),
                Q(146, "Lights Out", 145, 30, 1950, 410, 0,
                    "Doc Flick says the signs run on slivers of singing gold, and somebody's been stealing them. Relight the dark lamps and catch the thieves.",
                    Talk("Flick", "Talk to Doc Flick on the middle terrace", "Doc Flick, Arcade Owner",
                        "Every sign on the Mile has a sliver of singing gold in its heart. Somebody's prying them out and leaving the lamps dark.",
                        "Put fresh slivers in the dark lamps, and catch whoever's doing the prying."),
                    Point(QuestStepKind.Interact, "DarkNeon", 3, "Relight the dark neon lamps", "Dark neon lamp"),
                    Fight(QuestStepKind.Defeat, 75, 3, "Defeat the Glimmerfang Lampthieves", "Glimmerfang Lampthieves"),
                    Talk("Flick", "Return to Doc Flick", "Doc Flick, Arcade Owner",
                        "Glimmerfang pups. Tixi's own kids. Paid in that funny foreign paper by someone up on the roofs.")),
                Q(147, "The Shadow on the Roof", 146, 31, 2000, 420, 0,
                    "Someone on the arcade roof has been paying Tixi's pups to steal. Get up there.",
                    Fight(QuestStepKind.Defeat, 76, 2, "Defeat the Silent Office Shadows on the terraces", "Silent Office Shadows"),
                    Point(QuestStepKind.Explore, "Rooftop", 1, "Climb to the arcade roof at the top of the Mile", "The arcade roof"),
                    Talk("Tixi", "Tell Tixi who's on her roof", "Tixi Glimmerfang",
                        "Spies. On MY roof. Paying MY pups. Nobody pays my pups but me.",
                        "Their handler calls herself Nightglass. She's up there right now, counting her little helpers. Go and ruin her night.")),
                Q(148, "Agent Nightglass", 147, 31, 3000, 640, 246,
                    "Agent Nightglass of the Silent Office runs the Dominion's spies in Bigdig from the arcade roof.",
                    Fight(QuestStepKind.Boss, 77, 1, "Defeat Agent Nightglass on the arcade roof", "Agent Nightglass"),
                    Talk("Tixi", "Return to Tixi", "Tixi Glimmerfang",
                        "Her ledger. Every gang in Bigdig, paid off, played off, and every ounce of singing gold sold to the same broker.",
                        "The Assayer. Old Quibb Silvernose, who weighs everyone's gold so fairly. He's been weighing it for the Dominion.",
                        "Here, my jacket. Neon-lined; you'll never get lost in the dark. And wizard? Old Mott in the Deep Seams has been asking for you.")));
        }

        /// <summary>The Deep Seams (quests 149-152): the dead ward's runes, the siphon works and Chief Engineer Strake.</summary>
        public static void FillDeepSeamsLine(Questline ql)
        {
            Line(ql, "The Deep Seams", 152,
                Q(149, "Down the Deep", 148, 31, 2050, 430, 0,
                    "Old Digger Mott keeps the camp at the top of the Deep Seams. Something is crawling up from below.",
                    Talk("Mott", "Find Old Digger Mott at the seam camp", "Old Digger Mott",
                        "You're the Deepwarden's wizard? Good. The crawlers are coming up the seams, following the warmth. There's less warmth below than there used to be.",
                        "Push them back down, would you? My knees can't."),
                    Fight(QuestStepKind.Defeat, 78, 4, "Defeat Seam Crawlers around the camp", "Seam Crawlers"),
                    Talk("Mott", "Return to Old Digger Mott", "Old Digger Mott",
                        "Better. Now, Surveyor Kib's found something in the deep tunnels. Runes, she says. Go in through the sigil at the seam mouth.")),
                Q(150, "The Dead Ward", 149, 32, 2100, 440, 0,
                    "Surveyor Kib has found runes in the deep tunnels older than Bigdig. Find them inside the seams.",
                    Talk("Kib", "Talk to Surveyor Kib", "Surveyor Kib",
                        "Three runes, deep in the seams, in a ring. I've read about this. Bigdig once had a ward, a great dome of light over the whole city.",
                        "It went out, centuries ago. Nobody remembers why. Touch the runes, wizard; see if they remember."),
                    Point(QuestStepKind.Interact, "WardRune", 3, "Find the ward runes inside the Deep Seams (sigil at the seam mouth)", "Ward rune"),
                    Talk("Kib", "Tell Surveyor Kib what the runes showed you", "Surveyor Kib",
                        "Drained? From underneath? Then a ward can be killed. Slowly, seam by seam, by draining what feeds it.",
                        "Starfall has a ward, doesn't it? Fed by your crystal? Oh, wizard. Somebody knows exactly how to do this.")),
                Q(151, "Siphon Works", 150, 32, 2150, 450, 0,
                    "The Dominion's siphon works lie deep in the seams. Find them and break the valves.",
                    Fight(QuestStepKind.Defeat, 79, 4, "Defeat Hollowed Diggers in the seams", "Hollowed Diggers"),
                    Point(QuestStepKind.Explore, "SiphonWorks", 1, "Find the siphon works in the main seam", "The siphon works"),
                    Point(QuestStepKind.Interact, "SiphonValve", 2, "Break the siphon valves", "Siphon valve"),
                    Talk("Mott", "Tell Old Digger Mott what you found", "Old Digger Mott",
                        "Pipes, all down the seams, drinking the gold and pumping it somewhere. Their chief engineer runs it all from the bottom.")),
                Q(152, "Chief Engineer Strake", 151, 33, 3300, 700, 247,
                    "Chief Engineer Strake runs the siphon works from the heart of the Deep Seams. Shut it down.",
                    Fight(QuestStepKind.Defeat, 81, 1, "Defeat the Siphon Colossus in the works", "Siphon Colossus"),
                    Fight(QuestStepKind.Boss, 82, 1, "Defeat Chief Engineer Strake at the heart of the siphon works", "Chief Engineer Strake"),
                    Talk("Bruma", "Tell the Deepwarden in Bigdig", "Deepwarden Bruma Stonehum",
                        "The siphons are cut. But their main line runs up under the city, to Highroller Heights. Under the Golden Seam Casino.",
                        "The Assayer's casino. And beneath it, the Mother Lode itself. Brannoch is sitting on our heart, wizard.",
                        "Here, a pendant made from one of those old ward runes. Then we do the hardest thing in Bigdig: get the gangs in one room.")));
        }

        /// <summary>The Parley (Bigdig, quests 153-156): the gang bosses in the Moot Hall, the Assayer unmasked, the silence and the handshake.</summary>
        public static void FillParley(Questline ql)
        {
            Line(ql, "The Parley", 156,
                Q(153, "Calling the Bosses", 152, 33, 2200, 460, 0,
                    "The Deepwarden is calling a Moot, the first in a hundred years. Every gang boss must come. Carry the invitations.",
                    Talk("Bruma", "Talk to the Deepwarden", "Deepwarden Bruma Stonehum",
                        "A Moot. All four bosses, one room, no wands drawn. They'll come if you ask. You're the only one they've all owed."),
                    Talk("Ozza", "Invite Madame Ozza (Sprocket Row)", "Madame Ozza Goldtongue",
                        "A Moot? With Hesk and that oaf Pinch and the neon brat? Darling, I'd sooner eat a fuse. ...What time?"),
                    Talk("Hesk", "Invite Grandmaw Hesk (the Rustworks)", "Grandmaw Hesk Rustclaw",
                        "I'll come. I'll sit by the door. And I'll leave the minute Ozza starts smiling."),
                    Talk("Pinch", "Invite Big Pinch (the Fusewick Canals)", "\"Big Pinch\" Brassknuckle",
                        "If it's a trap I'm bringing my lads. If it's not a trap I'm still bringing my lads."),
                    Talk("Tixi", "Invite Tixi Glimmerfang (the Glimmer Mile)", "Tixi Glimmerfang",
                        "A boring old Moot with boring old bosses? ...Fine. But I'm wearing the loud jacket."),
                    Talk("Bruma", "Return to the Deepwarden", "Deepwarden Bruma Stonehum",
                        "All four. Wizard, I haven't seen that in my lifetime. Come to the Moot when you're ready.")),
                Q(154, "The Moot", 153, 33, 2400, 480, 0,
                    "The four gang bosses sit in one room for the first time in a hundred years. Lay out the proof, and see who squirms.",
                    // A directed scene (CUTSCENES.md): the four bosses stand in for themselves round the Deepwarden, each
                    // turns on whoever spoke last, and the wizard is asked what they found.
                    Talk("Bruma", "Join the Moot at the Deepwarden's hall", "Deepwarden Bruma Stonehum",
                        "@cast Hesk, Pinch, Ozza, Tixi",
                        "@card The Deepwarden's hall\nThe first Moot in a hundred years",
                        "[group, bow] Bosses. Thank you for coming. Let's not draw wands until at least the second sentence.",
                        "Madame Ozza Goldtongue|[to Bruma] Fake gold on my Row, paid for in Syndicate silver. I want an apology and a refund. In that order.",
                        "\"Big Pinch\" Brassknuckle|[angry, to Ozza] Syndicate silver? It was Dominion paper, and your own collectors were taking it!",
                        "Grandmaw Hesk Rustclaw|[to Pinch] And my furnaces? I suppose the newts wrecked those.",
                        "Tixi Glimmerfang|[shrug, to Hesk] Ugh, listen to you. My pups were paid to steal my own lights. By spies. On my roof.",
                        "@shot group 1.6",
                        "Deepwarden Bruma Stonehum|[angry, close, to you] Enough. The wizard has walked every borough. Wizard, tell them what you found.",
                        "@choice What do you tell the bosses?|The Dominion is in every borough.|Someone has been buying all your gold.",
                        "1> @emote Pinch surprise",
                        "1> @shot close Pinch 1.4",
                        "2> @emote Ozza surprise",
                        "2> @shot close Ozza 1.4",
                        "Deepwarden Bruma Stonehum|[point, to all] Dominion scrip on the Row. Dominion drills in the Rustworks. Dominion marines on the canals. Dominion spies on the Mile.",
                        "Deepwarden Bruma Stonehum|[close, to all] And every ounce of singing gold sold to one broker. Who weighs it for all of you.",
                        "@emote Ozza angry", "@emote Hesk angry", "@emote Pinch angry", "@emote Tixi surprise",
                        "@shot group 2.4"),
                    Talk("Quibb", "Confront the Assayer at his counting house off the square", "Assayer Quibb Silvernose",
                        "Ah. The wizard. And all four bosses glaring at my door. How very bad for business.",
                        "[shrug] Yes, I sold it. The Reach pays in gold that doesn't sing and paper that doesn't burn, and it pays more than Bigdig ever did.",
                        "[bow] You're too late, of course. The Marshal is already in my vault. Do excuse me. I have a casino to run."),
                    Talk("Bruma", "Return to the Deepwarden", "Deepwarden Bruma Stonehum",
                        "He's gone to ground in the Golden Seam. And the bosses are already shouting about whose fault it is that nobody noticed.")),
                Q(155, "The Silence", 154, 34, 2500, 500, 0,
                    "The bosses are arguing again. Strike the Deep Bell, and let them hear what Bigdig sounds like now.",
                    Point(QuestStepKind.Interact, "DeepBell", 1, "Strike the Deep Bell in front of the bosses", "The Deep Bell"),
                    // Directed: one word, and then nothing at all for a while.
                    Talk("Bruma", "Stand with the bosses at the bell", "Deepwarden Bruma Stonehum",
                        "@cast Hesk, Pinch, Ozza, Tixi",
                        "[close, still] Listen.",
                        "@wait 2.2",
                        "@shot group 2",
                        "Tixi Glimmerfang|[think, to Bruma] ...I can't hear anything.",
                        "Grandmaw Hesk Rustclaw|[sad, to Tixi] That's the point, pup. When I was your age that bell sang so loud we stuffed our ears with wool.",
                        "\"Big Pinch\" Brassknuckle|[sad, to all] We did this. All of us. We were so busy watching each other we let them walk off with the whole mine.",
                        "@wait 1",
                        "Madame Ozza Goldtongue|[close, sad, to all] ...No deal is worth a dark mine. I never thought I'd hear myself say that. Don't tell anyone I said it.",
                        "@shot group 2.4")),
                Q(156, "The Handshake", 155, 34, 2600, 520, 248,
                    "The bosses are ready to stand together, if someone makes them shake on it.",
                    // Directed: Hesk stands beside Ozza (the cast order puts them shoulder to shoulder), and when she asks
                    // for her hand the two of them reach for each other in a shot of their own.
                    Talk("Bruma", "Ask the bosses to shake on it", "Deepwarden Bruma Stonehum",
                        "@cast Hesk, Pinch, Ozza, Tixi",
                        "[shrug, to all] Well? The old way. Spit and shake, and the deal is binding till the gold stops singing.",
                        "@wait 1.2",
                        "\"Big Pinch\" Brassknuckle|[nod, to Bruma] Syndicate's in. Every barge, every fuse.",
                        "Tixi Glimmerfang|[cheer, to Bruma] Glimmerfangs are in. We'll light the way.",
                        "Madame Ozza Goldtongue|[shrug, to Bruma] The Company is in. At cost. Don't get used to it.",
                        "@shot close Hesk 1.6",
                        "Grandmaw Hesk Rustclaw|[to Ozza] ...Three hundred years the Union's stood alone. Oh, give me your hand, Ozza. The Rustclaws are in.",
                        "@look Ozza at Hesk",
                        "@emote Hesk point",
                        "@emote Ozza point",
                        "@shot wide Hesk with Ozza 2.4",
                        "@emote Pinch cheer", "@emote Tixi cheer",
                        "Deepwarden Bruma Stonehum|[group, to you] Then Bigdig stands together for the first time in living memory. The tunnel to Highroller Heights is open, wizard. Take this ring; every boss spat on it.")));
        }

        /// <summary>All In (Highroller Heights, quests 157-159): the casino's doors, the lucky chips and the way into the vault.</summary>
        public static void FillAllIn(Questline ql)
        {
            Line(ql, "Highroller Heights", 159,
                Q(157, "High Rollers Only", 156, 33, 2500, 520, 0,
                    "Highroller Heights is the Assayer's street, and his bouncers have the Golden Seam locked tight. Snick the doorman might be persuaded.",
                    Talk("Snick", "Talk to Snick, the casino doorman", "Snick, Casino Doorman",
                        "No chip, no entry. No exceptions. Except for whoever clears the bouncers off my steps; they've been very rude to me."),
                    Fight(QuestStepKind.Defeat, 83, 4, "Defeat Casino Bouncers on the strip", "Casino Bouncers"),
                    Talk("Snick", "Return to Snick", "Snick, Casino Doorman",
                        "Much better. Now, the back way in needs a chip. Lulu deals at the side tables; she owes me a favour.")),
                Q(158, "Chips Down", 157, 34, 2550, 530, 0,
                    "Lulu the croupier says the Assayer's lucky chips open the casino's back door. They're scattered round the street.",
                    Talk("Lulu", "Talk to Lulu the croupier", "Lulu, Croupier",
                        "Quibb's lucky chips. He drops them everywhere when he's nervous, and he's never been more nervous. Find three."),
                    Point(QuestStepKind.Interact, "LuckyChip", 3, "Find the Assayer's lucky chips around Highroller Heights", "Lucky chip"),
                    Talk("Lulu", "Bring the chips to Lulu", "Lulu, Croupier",
                        "Three chips. Press them into the sigil in the back lot and the house lets you in. The house always wins, sweetie. Tonight, you're the house.")),
                Q(159, "The House Always Wins", 158, 34, 2700, 560, 251,
                    "Get into the Golden Seam Casino through the back lot and break the Assayer's golems before the gangs arrive.",
                    Point(QuestStepKind.Explore, "CasinoBackDoor", 1, "Find the casino's back door, in the lot behind the strip", "The back door"),
                    Fight(QuestStepKind.Defeat, 84, 2, "Defeat the Jackpot Golems on the casino floor (sigil in the back lot)", "Jackpot Golems"),
                    Fight(QuestStepKind.Defeat, 85, 3, "Defeat Lightforged Legionnaires in the casino", "Lightforged Legionnaires"),
                    Talk("Lulu", "Report to Lulu", "Lulu, Croupier",
                        "Iron soldiers, on the gaming floor! In my casino! Well, Quibb's casino. Soon to be nobody's casino, if you've anything to say about it.",
                        "Here. A high roller's hat. Wear it when you walk into that vault.")));
        }

        /// <summary>Odd Jobs (quests 162-164): Runner Pip's errands across Bigdig, alongside the main story.</summary>
        public static void FillOddJobs(Questline ql)
        {
            Line(ql, "Odd Jobs", 164,
                Offered(Q(162, "Scrap Run", 132, 26, 1200, 260, 0,
                    "Runner Pip is building something out of scrap, and needs the copper wire Sprocket Row's Scrap Rats keep chewing.",
                    Fight(QuestStepKind.Collect, 60, 3, "Take copper scrap from Scrap Rats on Sprocket Row", "Scrap Rats"),
                    Talk("RunnerPip", "Bring the scrap to Pip", "Runner Pip Quickfuse",
                        "Perfect! Well, chewed. But perfect! Next I need teeth. Big ones. From the canal gators.")),
                    "RunnerPip",
                    "Wizard! Wizard wizard wizard. I'm building the fastest mine-cart in Bigdig and I need parts.",
                    "Scrap Rats on Sprocket Row chew copper wire. Get me some? I'd go, but they bite."),
                Also(Q(163, "Gator Teeth", 162, 30, 1900, 400, 0,
                    "Pip needs canal gator teeth for the mine-cart's brakes. Don't ask how.",
                    Fight(QuestStepKind.Collect, 72, 2, "Take teeth from Canal Gators on the Fusewick Canals", "Canal Gators"),
                    Talk("RunnerPip", "Bring the teeth to Pip", "Runner Pip Quickfuse",
                        "Gator-tooth brakes. They grip like nothing else. Last thing: a spark core. The Siphon Colossus in the Deep Seams has one.")), 141),
                Also(Q(164, "Fastest Cart Under the World", 163, 33, 2600, 540, 250,
                    "Pip's mine-cart needs one last part: a spark core from a Siphon Colossus deep in the seams.",
                    Fight(QuestStepKind.Collect, 81, 1, "Take a spark core from a Siphon Colossus in the Deep Seams", "Siphon Colossus"),
                    Talk("RunnerPip", "Bring the core to Pip", "Runner Pip Quickfuse",
                        "It's done! The fastest mine-cart in Bigdig! It has no seat and no brakes that work and it's PERFECT.",
                        "Here, my old running boots. I've got a cart now. Go fast, wizard!")), 151));
        }

        /// <summary>Lucky Streak (quests 165-166): Lucky Lou's stolen dice and Old Pockets' marked deck.</summary>
        public static void FillLuckyStreak(Questline ql)
        {
            Line(ql, "Lucky Streak", 166,
                Offered(Q(165, "Lucky Dice", 145, 30, 1900, 400, 0,
                    "Lucky Lou on the Glimmer Mile lost his lucky dice to a Glimmerfang pup, and hasn't won a hand since.",
                    Fight(QuestStepKind.Collect, 75, 2, "Take Lou's lucky dice back from Glimmerfang Lampthieves", "Glimmerfang Lampthieves"),
                    Talk("Lou", "Bring the dice to Lucky Lou", "Lucky Lou",
                        "My dice! Feel that? Warm. Lucky. You know who taught me to roll? Old Pockets, in Highroller Heights. If you ever get up there, say hi.")),
                    "Lou",
                    "You look lucky. I used to look lucky. Then a Glimmerfang pup nicked my dice.",
                    "Get them back and I'll owe you. Well. I'll owe you luck."),
                Also(Q(166, "The Marked Deck", 165, 34, 2600, 540, 249,
                    "Old Pockets says the casino bouncers carry marked cards, and he'd dearly love to prove the house cheats.",
                    Talk("Pockets", "Talk to Old Pockets in Highroller Heights", "Old Pockets, Retired Cardsharp",
                        "Lou sent you? That rascal. Listen: every bouncer here carries a marked deck. Bring me some and I'll show the whole Heights the house cheats."),
                    Fight(QuestStepKind.Collect, 83, 3, "Take marked cards from Casino Bouncers", "Casino Bouncers"),
                    Talk("Pockets", "Bring the cards to Old Pockets", "Old Pockets, Retired Cardsharp",
                        "Marked. Every one. Forty years I suspected. Here, Lou's spare dice, made into a ring. Roll them when you need a little luck.")), 157));
        }

        /// <summary>The finale (quests 160-161): the united gangs storm the Golden Seam, the Assayer, Brannoch and the Mother Lode.</summary>
        public static void FillWorld3Finale(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Finale: All In";
            ql.quests = new List<QuestEntry>
            {
                Q(160, "Ante Up", 159, 34, 2600, 520, 0,
                    "The gangs are ready to storm the Golden Seam with you. Rally them at their turf, and meet at the casino.",
                    Talk("Bruma", "Talk to the Deepwarden", "Deepwarden Bruma Stonehum",
                        "This is it, wizard. Every gang in Bigdig waiting on your word. Go and give it."),
                    Talk("Ozza", "Rally Madame Ozza (Sprocket Row)", "Madame Ozza Goldtongue",
                        "The Company's collectors will hold the strip. And darling, if there's a jackpot in there, I'm counting it."),
                    Talk("Hesk", "Rally Grandmaw Hesk (the Rustworks)", "Grandmaw Hesk Rustclaw",
                        "Union picks will open every wall in that place. We've been wanting to dig through a casino for years."),
                    Talk("Pinch", "Rally Big Pinch (the Fusewick Canals)", "\"Big Pinch\" Brassknuckle",
                        "Syndicate fuses on every door. When you say go, the Golden Seam gets a lot more doors."),
                    Talk("Tixi", "Rally Tixi Glimmerfang (the Glimmer Mile)", "Tixi Glimmerfang",
                        "We'll cut the casino's lights and give them ours. Pink. Very pink. They'll hate it."),
                    // Directed: the Deepwarden and the four bosses walk up one by one before the doorman speaks.
                    Talk("Snick", "Meet the gangs at the casino door (Highroller Heights)", "Snick, Casino Doorman",
                        "@cast Bruma, Hesk, Pinch, Ozza, Tixi",
                        "@shot wide 1.2",
                        "@enter Bruma 0.7", "@enter Hesk 0.7", "@enter Pinch 0.7", "@enter Ozza 0.7", "@enter Tixi 0.7",
                        "@shot group 1.4",
                        "[group] Every gang in Bigdig on my steps, and all of them on the guest list. I've never been so proud. Go on in, wizard. The house is yours.",
                        "@emote Bruma cheer", "@emote Hesk cheer", "@emote Pinch cheer", "@emote Ozza cheer", "@emote Tixi cheer",
                        "@shot group 2.2")),
                World3FinaleQuest(),
            };
        }

        /// <summary>Quest 161, the World 3 finale (the builder adds every storyline's end to its requirements).</summary>
        public static QuestEntry World3FinaleQuest()
        {
            return Q(161, "All In", 160, 34, 4800, 1000, 252,
                "With every gang in Bigdig behind you, storm the Golden Seam Casino: the Assayer on the gaming floor, Marshal Brannoch on the casino roof over the whole city, and the Mother Lode beneath.",
                Fight(QuestStepKind.Boss, 86, 1, "Defeat the Assayer on the gaming floor (sigil in the back lot)", "The Assayer"),
                Fight(QuestStepKind.Boss, 87, 1, "Take the lift in the high-roller vault up to the casino roof and defeat Marshal Brannoch, the Iron Wall", "Marshal Brannoch, the Iron Wall"),
                Point(QuestStepKind.Interact, "VaultLever", 1, "Throw the vault lever and shut the main siphon line", "Vault lever"),
                Point(QuestStepKind.Interact, "MotherLode", 1, "Put your hand on the Mother Lode", "The Mother Lode"),
                // Directed: the bosses round the Deepwarden one last time, the camera on the wizard as the news turns, and a
                // fade to black on the promise.
                Talk("Bruma", "Return to the Deepwarden in Bigdig", "Deepwarden Bruma Stonehum",
                    "@cast Hesk, Pinch, Ozza, Tixi",
                    "[cheer] Listen. Listen! The Deep Bell. It's ringing by itself.",
                    "Madame Ozza Goldtongue|[laugh, to you] It's singing. Oh, darling, it's singing again.",
                    "\"Big Pinch\" Brassknuckle|[to you] Brannoch's gone. Ran for the canals with what was left of his shield wall. Kell's ships took him off.",
                    "Tixi Glimmerfang|[cheer, to you] And every sign on the Mile just lit up at once. Every one!",
                    "Grandmaw Hesk Rustclaw|[bow, to you] Bigdig owes you, wizard. And a Rustclaw always pays her debts.",
                    "@shot you 1.6",
                    "Deepwarden Bruma Stonehum|[sad, close, to you] But Brannoch said something as he fled. 'The vault was a test. Starfall's ward is next.' Go home, wizard. Your crystal needs you now.",
                    "@wait 0.8",
                    "Deepwarden Bruma Stonehum|[group, to you] And when the Dominion comes for Starfall, Bigdig will dig its way to your door. All of us. Together.",
                    "@emote Hesk nod", "@emote Pinch cheer", "@emote Ozza nod", "@emote Tixi cheer",
                    "@shot group 2.4",
                    "@fade out 1.2"));
        }

        // ================================================================== the world

        /// <summary>Bigdig's storylines, in story order (the finale is its own questline).</summary>
        public static readonly string[] World3StorylineNames =
        {
            "Under Everything", "Company Business", "Union Strong", "Smuggler's Run", "Neon Nights", "The Deep Seams",
            "The Parley", "Highroller Heights", "Odd Jobs", "Lucky Streak",
        };

        /// <summary>World 3: the Deepwarden sets the goal out in The Quiet Gold (132); the finale (161) needs every storyline.</summary>
        public static void FillWorld3(WorldArc w, List<Questline> storylines)
        {
            w.includeInBuild = true;
            w.order = 3;
            w.displayName = "Bigdig";
            w.dialogueMusicTrack = "Music_Dialogue_Dominion";
            w.dialogueMusicSpeakers = new List<string> { "Marshal Brannoch, the Iron Wall", "Chief Engineer Strake", "The Assayer" };
            w.levelRange = "Lv 25-35";
            w.goal = World3Goal;
            w.goalQuest = 132;
            w.finaleQuest = 161;
            w.storylines = new List<Questline>(storylines);
            w.zones = new List<string> { "Bigdig", "SprocketRow", "Rustworks", "FusewickCanals", "GlimmerMile", "DeepSeams", "HighrollerHeights" };
        }
    }
}
