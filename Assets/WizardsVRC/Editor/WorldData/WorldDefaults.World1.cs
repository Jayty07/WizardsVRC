using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// World 1's later streets and storylines (see the World 1 blueprint): Tidewatch Docks (Smugglers' Tide), Kiln Street
    /// (Cold Iron), Thornhollow Grove (Straight Lines), the Hollow Keep (The Hollow King), and the castle storylines Stars
    /// and Ciphers, The Quiet Merchant and The Lost Name. Save ids 17-38.
    /// </summary>
    public static partial class WorldDefaults
    {
        // ================================================================== zones

        /// <summary>Tidewatch Docks (Lv 7-9): a grey harbor, the quay along the sea, two piers, warehouses and Grell's yard.</summary>
        public static void FillTidewatchDocks(ZoneDefinition z)
        {
            z.textureStyle = ZoneTextureStyle.Docks;
            z.zoneKey = "TidewatchDocks";
            z.displayName = "Tidewatch Docks";
            z.levelRange = "Lv 7-9";
            z.height = 800f;
            z.cellSize = 8f;
            z.map =
            "########################~~~~~~~~~\n" +
            "########################~~~~~~~~~\n" +
            "##fffff.##..eeeee...####~~~~~~~~~\n" +
            "##f...f.##..e...e...#.b.~~~~~~~~~\n" +
            "##f.6.f.....e.5.e...#.b.~~~~~~~~~\n" +
            "##f...f.##..e...e...#.bN~~~~~~~~~\n" +
            "##fffff.##..eeeee.....b.~~~~~~~~~\n" +
            "##########...........2b.~~~~~~~~~\n" +
            "#############.W.......b.~~~~~~~~~\n" +
            "###.ddddddd..########.b.~~~~~~~~~\n" +
            "###.d.....d4.########Bb.~~~~~~~~~\n" +
            "##..d.....d..########.b.~~~~~~~~~\n" +
            "##.4d..4..d.#.........b.~~~~~~~~~\n" +
            "##..d.....d..########.b......~~~~\n" +
            "###.d.....d4.########.b2...2.~~~~\n" +
            "###.ddddddd..########.bbbbbb.~~~~\n" +
            "######...############.g......~~~~\n" +
            "######.3.############.g......~~~~\n" +
            "###.cccccc.##########.g.~~~~~~~~~\n" +
            "###.c....c.##########Bg.~~~~~~~~~\n" +
            "###.c....c.##########.g.~~~~~~~~~\n" +
            "###.c....c.gggggggggggg.~~~~~~~~~\n" +
            "###.c.3..c3.########.7.N~~~~~~~~~\n" +
            "###.c....c..########....~~~~~~~~~\n" +
            "###.c....c.##########...~~~~~~~~~\n" +
            "###.c....c.##########........~~~~\n" +
            "###.cccccc.##########...1....~~~~\n" +
            "######,,,############.aaaaaa.~~~~\n" +
            "######,,,############Fa......~~~~\n" +
            "######,,,############.a......~~~~\n" +
            "######,,,############Ba.~~~~~~~~~\n" +
            "##########............a.~~~~~~~~~\n" +
            "##########...........1a.~~~~~~~~~\n" +
            "##########.Q..........a.~~~~~~~~~\n" +
            "##########.......1....a.~~~~~~~~~\n" +
            "##########^..aaaaaaaaaaN~~~~~~~~~\n" +
            "###########,,,#...###...~~~~~~~~~\n" +
            "###########,^,#...######~~~~~~~~~\n" +
            "###############.S.######~~~~~~~~~\n" +
            "########################~~~~~~~~~\n";
            z.groundColor = new Color(0.3f, 0.34f, 0.3f);
            z.roadColor = new Color(0.52f, 0.5f, 0.47f);
            z.grassColor = new Color(0.32f, 0.42f, 0.3f);
            z.buildingColor = new Color(0.48f, 0.45f, 0.42f);
            z.roofColor = new Color(0.24f, 0.3f, 0.38f);
            z.lowWallColor = new Color(0.4f, 0.4f, 0.38f);
            z.treeColor = new Color(0.25f, 0.36f, 0.26f);
            z.accentColor = new Color(0.95f, 0.82f, 0.5f);
            z.buildingHeight = new Vector2(6f, 11f);
            z.lampDensity = 0.25f;
            // Grey sea mist.
            z.skyTopColor = new Color(0.42f, 0.5f, 0.6f);
            z.skyHorizonColor = new Color(0.8f, 0.82f, 0.84f);
            z.fogColor = new Color(0.62f, 0.66f, 0.7f);
            z.horizonDistance = 1050f;
            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "Q", key = "Quill", displayName = "Harbormaster Odalys Quill", robeColor = new Color(0.2f, 0.28f, 0.4f), hatColor = new Color(0.12f, 0.16f, 0.24f),
                    facing = -1f,
                    defaultDialogue = new[] { "Tide's out, crabs are in, and somebody's running cargo past my office at night. Some days I hate this harbor." },
                },
                new ZoneNpc
                {
                    symbol = "F", key = "Fenwick", displayName = "Net-mender Fenwick", robeColor = new Color(0.45f, 0.4f, 0.3f), hatColor = new Color(0.3f, 0.25f, 0.18f),
                    beard = true, facing = -1f,
                    defaultDialogue = new[] { "Forty years mending nets on this pier. I know every boat that ties up here. Mostly." },
                },
            };
            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "B", key = "FishBarrel", kind = ZonePointKind.Interact, look = ZonePointLook.Crate, label = "Fish barrel" },
                new ZonePoint { symbol = "N", key = "LostNet", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Lost net" },
                new ZonePoint { symbol = "W", key = "Warehouse", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 2f },
            };
            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "PierCrabs", roster = new List<int> { 13, 13 }, wanderers = 3, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "2", key = "FarPierCrabs", roster = new List<int> { 13, 13, 13 }, wanderers = 3, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "3", key = "Thugs", roster = new List<int> { 14, 14 }, wanderers = 3, patrolSpeed = 1.3f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "4", key = "ThugGang", roster = new List<int> { 14, 14, 13 }, wanderers = 3, patrolSpeed = 1.3f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "5", key = "Grell", roster = new List<int> { 15, 14, 14 }, wanderers = 1, patrolSpeed = 0.8f, respawnSeconds = 30, scaleWithParty = true },
                new ZoneEncounter { ringSymbol = "6", key = "Selwyn", roster = new List<int> { 23 }, wanderers = 1, patrolSpeed = 0.7f, respawnSeconds = 30 },
                new ZoneEncounter { ringSymbol = "7", key = "QuayThugs", roster = new List<int> { 14, 13 }, wanderers = 2, patrolSpeed = 1.2f, respawnSeconds = 20 },
            };
        }

        /// <summary>Kiln Street (Lv 8-10): the smoky forge quarter, Cole's yard and back forge, slag heaps and the sealed furnace hall.</summary>
        public static void FillKilnStreet(ZoneDefinition z)
        {
            z.textureStyle = ZoneTextureStyle.Forge;
            z.zoneKey = "KilnStreet";
            z.displayName = "Kiln Street";
            z.levelRange = "Lv 8-10";
            z.height = 900f;
            z.cellSize = 8f;
            z.map =
            "#################################\n" +
            "#################################\n" +
            "###########...eeeee...###########\n" +
            "###########...e...e...###########\n" +
            "###########...e.5.e...###########\n" +
            "###########...e...e...###########\n" +
            "###########...eeeee...###########\n" +
            "##.ccccccc#...........###########\n" +
            "##.c.....c#####.E.###############\n" +
            "##.c.....c#####.g.###############\n" +
            "##.c..3..c#####.g.##ddddddddd####\n" +
            "##.c.....c#####.g..#d.......d####\n" +
            "##.c.....c#####.g7..d.......d####\n" +
            "##.ccccccc#####.g...d...4...d####\n" +
            "####...fffffff#.g.##d.......d####\n" +
            "####.X.f.X...f#.g.##d.......d####\n" +
            "####...f.....f..g.##d.......d####\n" +
            "####...f..6.Cf.7g.##ddddddddd####\n" +
            "####..Xf.....f..g.###############\n" +
            "####...f.....f#.g.###############\n" +
            "####..6fffffff#.g.###############\n" +
            "#####....######.g.####bbbbbbbbb##\n" +
            "###############.g.####b.......b##\n" +
            "###############.g.##..b.......b##\n" +
            "########,,,,,,,......2b...2...b##\n" +
            "########,......1a.##..b.......b##\n" +
            "########,.,,,,,.a.####b.......b##\n" +
            "##.a......#####.a.####bbbbbbbbb##\n" +
            "##.a1.....#####.a.#####..,,,,,^##\n" +
            "##.a......#####.a.#####,^T,^,,,##\n" +
            "##.a......#####.a.#####,,,,,,^,##\n" +
            "##.a......#####.a......,,H,,,,,##\n" +
            "##.a...1........a......##########\n" +
            "##.aaaaaaaaaaaaaa......##########\n" +
            "##########...I.1.......##########\n" +
            "##########.............##########\n" +
            "##########.^.........^.##########\n" +
            "###############...###############\n" +
            "################S################\n" +
            "#################################\n";
            z.groundColor = new Color(0.25f, 0.2f, 0.18f);
            z.roadColor = new Color(0.42f, 0.35f, 0.31f);
            z.grassColor = new Color(0.3f, 0.28f, 0.22f);
            z.buildingColor = new Color(0.45f, 0.3f, 0.25f);
            z.roofColor = new Color(0.2f, 0.16f, 0.16f);
            z.lowWallColor = new Color(0.35f, 0.28f, 0.25f);
            z.treeColor = new Color(0.2f, 0.17f, 0.15f);
            z.accentColor = new Color(1f, 0.5f, 0.2f);
            z.buildingHeight = new Vector2(8f, 14f);
            z.lampDensity = 0.3f;
            // A smoky orange sky over the chimneys.
            z.skyTopColor = new Color(0.35f, 0.18f, 0.12f);
            z.skyHorizonColor = new Color(0.95f, 0.55f, 0.25f);
            z.fogColor = new Color(0.55f, 0.38f, 0.3f);
            z.horizonDistance = 1150f;
            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "I", key = "Ingrid", displayName = "Forgewarden Ingrid Hale", robeColor = new Color(0.5f, 0.25f, 0.15f), hatColor = new Color(0.3f, 0.2f, 0.12f),
                    facing = -1f,
                    defaultDialogue = new[] { "Honest iron, honest prices. That's Kiln Street. That's how it was, anyway." },
                },
                new ZoneNpc
                {
                    symbol = "C", key = "Cole", displayName = "Forge-master Bertrand Cole", robeColor = new Color(0.32f, 0.28f, 0.26f), hatColor = new Color(0.2f, 0.18f, 0.16f),
                    beard = true, facing = -1f,
                    defaultDialogue = new[] { "Busy. Always busy. Orders don't fill themselves." },
                },
                new ZoneNpc
                {
                    symbol = "T", key = "Tam", displayName = "Apprentice Tam", robeColor = new Color(0.55f, 0.45f, 0.3f), hatColor = new Color(0.4f, 0.3f, 0.2f),
                    facing = -1f,
                    defaultDialogue = new[] { "One day I'll forge a wizard's wand. A proper one. With sparks." },
                },
            };
            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "X", key = "StrangeFrame", kind = ZonePointKind.Interact, look = ZonePointLook.Crate, label = "Strange frame" },
                new ZonePoint { symbol = "H", key = "TamsHammer", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Tam's hammer" },
                new ZonePoint { symbol = "E", key = "FurnaceHall", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 2f },
            };
            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "Imps", roster = new List<int> { 16, 16 }, wanderers = 4, patrolSpeed = 1.6f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "2", key = "KilnImps", roster = new List<int> { 16, 16, 16 }, wanderers = 3, patrolSpeed = 1.6f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "3", key = "Sentries", roster = new List<int> { 17, 17 }, wanderers = 3, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "4", key = "AlleySentries", roster = new List<int> { 17, 16 }, wanderers = 4, patrolSpeed = 1f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "5", key = "Vesper", roster = new List<int> { 18, 17, 17 }, wanderers = 1, patrolSpeed = 0.8f, respawnSeconds = 30, scaleWithParty = true },
                new ZoneEncounter { ringSymbol = "6", key = "FrameWatch", roster = new List<int> { 17, 16 }, wanderers = 3, patrolSpeed = 1f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "7", key = "ForgeLane", roster = new List<int> { 16, 17 }, wanderers = 2, patrolSpeed = 1.5f, respawnSeconds = 20 },
            };
        }

        /// <summary>Thornhollow Grove (Lv 10-11): old forest cut through by a dead-straight survey line, with a Dominion camp at its head.</summary>
        public static void FillThornhollowGrove(ZoneDefinition z)
        {
            z.textureStyle = ZoneTextureStyle.Forest;
            z.zoneKey = "ThornhollowGrove";
            z.displayName = "Thornhollow Grove";
            z.levelRange = "Lv 10-11";
            z.height = 1000f;
            z.cellSize = 8f;
            z.map =
            "#################################\n" +
            "#^^^^^^^^^^^,,,,eeeee,,,,,^^^^^^#\n" +
            "#^^^^^^^^^^^,,,,e...e,,,,,^^^^^^#\n" +
            "#^^^^^^^^^^^,,,,e.5.e,,,,,^^^^^^#\n" +
            "#^^^^^^^^^^^,,,,e...e,,,4,^^^^^^#\n" +
            "#^^^^^^^^^^^,,,,eeeee,dddddddd^^#\n" +
            "#^^^^^^^^^^^,,,,,,,,X,d,,,,,,d^^#\n" +
            "#^^^^^^^^^^^^^^^^^^^.,d,,...,d^^#\n" +
            "#^^^^^^^^^^^^^^^^^^^.4d.,.4.,d^^#\n" +
            "#^^^^^^^^^^^^^^^^^^^.,d,,...,d^^#\n" +
            "#^^^^^^^^^^^^^^^^^^^K^d,,,,,,d^^#\n" +
            "#^^^^^^^^^^^^ccccccc.,dddddddd^^#\n" +
            "#^^^^^^^^^^^.c,,,,,c3,^^^^^^^^^^#\n" +
            "#^ffffffff^^.c,...,c.,^^^^^^^^^^#\n" +
            "#^f,,,,,6f^,.c,.3.,c.^^^^^^^^^^^#\n" +
            "#^f,,,,,,f^,3c,...,c.,^^^^^^^^^^#\n" +
            "#^f6,O...f...c,,,,,c3,^^^^^^^^^^#\n" +
            "#^f,,,,,,f^^^ccccccc.,^^^^^^^^^^#\n" +
            "#^f,K,,6,f^^^^^^^^^^.^^^^^^^^^^^#\n" +
            "#^ffffffff^^^^^^^^^^.^^bbbbbbbb^#\n" +
            "#^^^^^^^^^^^^^^^^^^^.^^b,,,,,,b^#\n" +
            "#^^^^^^^^^^^^^^^^^^^.^^b,...,,b^#\n" +
            "#^^^^^^^^^^^^^^^^^^^...b,.2.,,b^#\n" +
            "#^^^^^^^^^^^^^^^^^^^K^^b,...,,b^#\n" +
            "#^,a,,,,,,,^^^^^^^^,.,,b,,,,,,b^#\n" +
            "#^,a,,,,,,,^^^^^^^^,.2,bbbbbbbb^#\n" +
            "#,,a...,,,,,^^^^^^^,.,,^^^^^^^^^#\n" +
            "#,1a.,1,,,1,^^^^^^^,.,^^^^^^^^^^#\n" +
            "#,,a...,.aaaaaa^^^^,.,^^^^^^^^^^#\n" +
            "#^,a,,,,,a,^^^a^^^^,.,^^^^^^^^^^#\n" +
            "#^,aaaaaaa,^^^a^^^^,K,^^^^^^^^^^#\n" +
            "#^^^^^^^^,,,,,a,,,,,.,,,^^^^^^^^#\n" +
            "#^^^^^^^^,1,,,,,,,...,,,^^^^^^^^#\n" +
            "#^^^^^^^^,,,..B......,,,^^^^^^^^#\n" +
            "#^^^^^^^^,,,.......,,,,,^^^^^^^^#\n" +
            "#^^^^^^^^,,,.......,,,,,^^^^^^^^#\n" +
            "#^^^^^^^^,,,.......,,,,,^^^^^^^^#\n" +
            "#^^^^^^^^,,,.......,,,,,^^^^^^^^#\n" +
            "#^^^^^^^^^^^^^^#S#^^^^^^^^^^^^^^#\n" +
            "#################################\n";
            z.groundColor = new Color(0.18f, 0.32f, 0.15f);
            z.roadColor = new Color(0.47f, 0.4f, 0.3f);
            z.grassColor = new Color(0.22f, 0.42f, 0.18f);
            z.buildingColor = new Color(0.4f, 0.35f, 0.3f);
            z.roofColor = new Color(0.25f, 0.3f, 0.15f);
            z.lowWallColor = new Color(0.3f, 0.36f, 0.22f);
            z.treeColor = new Color(0.12f, 0.38f, 0.14f);
            z.accentColor = new Color(0.75f, 0.9f, 0.4f);
            z.buildingHeight = new Vector2(6f, 9f);
            z.lampDensity = 0.08f;
            // Green shade under the canopy.
            z.skyTopColor = new Color(0.26f, 0.45f, 0.38f);
            z.skyHorizonColor = new Color(0.76f, 0.86f, 0.66f);
            z.fogColor = new Color(0.45f, 0.58f, 0.45f);
            z.horizonDistance = 1100f;
            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "B", key = "Briony", displayName = "Ranger Briony Ashdown", robeColor = new Color(0.25f, 0.4f, 0.22f), hatColor = new Color(0.35f, 0.28f, 0.15f),
                    facing = -1f,
                    defaultDialogue = new[] { "Stay on the paths. The grove remembers people who don't." },
                },
                new ZoneNpc
                {
                    symbol = "O", key = "Oak", displayName = "Grandfather Oak", robeColor = new Color(0.35f, 0.26f, 0.15f), hatColor = new Color(0.22f, 0.4f, 0.16f),
                    beard = true, facing = -1f,
                    defaultDialogue = new[] { "Mmm. Little wizard. You walk loudly, but kindly. That is enough." },
                },
            };
            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "K", key = "SurveyStake", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Survey stake" },
                new ZonePoint { symbol = "X", key = "Camp", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 3f },
            };
            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "Boars", roster = new List<int> { 19, 19 }, wanderers = 3, patrolSpeed = 1.4f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "2", key = "ThicketBoars", roster = new List<int> { 19, 19, 19 }, wanderers = 3, patrolSpeed = 1.4f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "3", key = "Surveyors", roster = new List<int> { 20, 20 }, wanderers = 3, patrolSpeed = 1f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "4", key = "LineGuard", roster = new List<int> { 21, 20, 20 }, wanderers = 3, patrolSpeed = 1f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "5", key = "Roan", roster = new List<int> { 22, 21, 21 }, wanderers = 1, patrolSpeed = 0.8f, respawnSeconds = 30, scaleWithParty = true },
                new ZoneEncounter { ringSymbol = "6", key = "WestBoars", roster = new List<int> { 19, 20 }, wanderers = 3, patrolSpeed = 1.3f, respawnSeconds = 20 },
            };
        }

        /// <summary>The Hollow Keep (Lv 11-13): Morvane's black keep on its crag, the outer bailey, the hall of lanterns and the throne hall.</summary>
        public static void FillHollowKeep(ZoneDefinition z)
        {
            z.textureStyle = ZoneTextureStyle.Keep;
            z.zoneKey = "HollowKeep";
            z.displayName = "The Hollow Keep";
            z.levelRange = "Lv 11-13";
            z.height = 1100f;
            z.cellSize = 8f;
            z.map =
            "#################################\n" +
            "#################################\n" +
            "###########...eeeee...###########\n" +
            "###########...e...e...###########\n" +
            "###########...e.5.e...###########\n" +
            "###########...e...e...###########\n" +
            "###########...eeeee...###########\n" +
            "###########...........###########\n" +
            "##.ddddddd#...........###########\n" +
            "##.d.....d#####.X.###############\n" +
            "##.d..4..d#####...###############\n" +
            "##.d.....d....#...###############\n" +
            "##.d.....d#####...###############\n" +
            "##.ddddddd#####.3.###############\n" +
            "##.........L.........L.##########\n" +
            "##########ccccccccccc..##########\n" +
            "##########c.........c..##########\n" +
            "##########c....3....c..##########\n" +
            "##########c3........c3.##########\n" +
            "##########c.........c..##########\n" +
            "##########cccccccccccL.##########\n" +
            "##=============...=============##\n" +
            "##.aaaaaaaaaaa.....bbbbbbbbbbb.##\n" +
            "##.a........1a.....b........2b.##\n" +
            "##.a.........a.....b.........b.##\n" +
            "##.a1...1....a....2b....2....b.##\n" +
            "##.a.........a.....b.........b.##\n" +
            "##.a.........a.....b.........b.##\n" +
            "##.a........1a.....b........2b.##\n" +
            "##.aaaaaaaaaaa.....bbbbbbbbbbb.##\n" +
            "##........====.....====........##\n" +
            "###############...###############\n" +
            "#########,,,,,,,,,,,,,,,#########\n" +
            "#########,^,,.......,,,,#########\n" +
            "#########,,,,..R....,,,,#########\n" +
            "#########,,,,.......,,,,#########\n" +
            "#########,^,,.......,,^,#########\n" +
            "#########,,,,.......,,,,#########\n" +
            "################S################\n" +
            "#################################\n";
            z.groundColor = new Color(0.16f, 0.15f, 0.19f);
            z.roadColor = new Color(0.32f, 0.3f, 0.36f);
            z.grassColor = new Color(0.22f, 0.22f, 0.24f);
            z.buildingColor = new Color(0.24f, 0.22f, 0.3f);
            z.roofColor = new Color(0.1f, 0.08f, 0.14f);
            z.lowWallColor = new Color(0.28f, 0.25f, 0.33f);
            z.treeColor = new Color(0.16f, 0.14f, 0.16f);
            z.accentColor = new Color(0.72f, 0.52f, 1f);
            z.buildingHeight = new Vector2(10f, 18f);
            z.lampDensity = 0.2f;
            // Cold violet dusk.
            z.skyTopColor = new Color(0.12f, 0.08f, 0.25f);
            z.skyHorizonColor = new Color(0.5f, 0.36f, 0.6f);
            z.fogColor = new Color(0.3f, 0.24f, 0.38f);
            z.horizonDistance = 1250f;
            z.npcs = new List<ZoneNpc>
            {
                new ZoneNpc
                {
                    symbol = "R", key = "Rhona", displayName = "Watch-Captain Rhona Vale", robeColor = new Color(0.3f, 0.32f, 0.5f), hatColor = new Color(0.55f, 0.5f, 0.3f),
                    facing = -1f,
                    defaultDialogue = new[] { "My watch holds the crag. Nobody comes down from that keep without us knowing." },
                },
            };
            z.points = new List<ZonePoint>
            {
                new ZonePoint { symbol = "L", key = "HollowLantern", kind = ZonePointKind.Interact, look = ZonePointLook.GlowingItem, label = "Hollow Lantern" },
                new ZonePoint { symbol = "X", key = "ThroneHall", kind = ZonePointKind.Explore, look = ZonePointLook.None, label = "", triggerSizeCells = 2f },
            };
            z.encounters = new List<ZoneEncounter>
            {
                new ZoneEncounter { ringSymbol = "1", key = "BaileyWest", roster = new List<int> { 24, 21 }, wanderers = 4, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "2", key = "BaileyEast", roster = new List<int> { 24, 24 }, wanderers = 4, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "3", key = "LanternHall", roster = new List<int> { 24, 21, 21 }, wanderers = 4, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "4", key = "Chapel", roster = new List<int> { 24, 24 }, wanderers = 3, patrolSpeed = 0.9f, respawnSeconds = 20 },
                new ZoneEncounter { ringSymbol = "5", key = "Morvane", roster = new List<int> { 25, 24, 24 }, wanderers = 1, patrolSpeed = 0.6f, respawnSeconds = 30, scaleWithParty = true },
            };
        }

        // ================================================================== storylines

        private static QuestEntry Q(int id, string title, int prereq, int minLevel, int xp, int gold, int reward, string description, params QuestStepEntry[] steps)
        {
            return new QuestEntry
            {
                saveId = id, title = title, prerequisite = prereq, minLevel = minLevel, xp = xp, gold = gold, rewardItem = reward,
                description = description, steps = new List<QuestStepEntry>(steps),
            };
        }

        private static QuestEntry Offered(QuestEntry q, string giver, params string[] offer)
        {
            q.startsAutomatically = false;
            q.giverNpc = giver;
            q.offerDialogue = offer;
            return q;
        }

        private static QuestEntry Also(QuestEntry q, params int[] more)
        {
            q.alsoRequires = new List<int>(more);
            return q;
        }

        /// <summary>Smugglers' Tide (Tidewatch Docks, quests 17-21): iron parts in fish barrels lead to Grell's coded ledger.</summary>
        public static void FillSmugglersTide(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Smugglers' Tide";
            ql.storylineEnd = 20;
            ql.quests = new List<QuestEntry>
            {
                Q(17, "Low Tide", 5, 6, 110, 40, 0,
                    "Gatekeeper Bramwell has word from Tidewatch Docks: crabs are swarming the piers and the harbormaster can't land a boat. The docks tunnel is at the end of the east avenue.",
                    Talk("Bramwell", "Talk to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                        "Tidewatch Docks has been asking for a wizard. The harbormaster, Odalys Quill, says her piers are crawling.",
                        "The tunnel at the end of the east avenue will take you there. Mind the crabs; they pinch."),
                    Talk("Quill", "Find Harbormaster Quill at Tidewatch Docks (tunnel at the end of the east avenue)", "Harbormaster Quill",
                        "A wizard! Good. Dock Crabs have taken the piers and nobody's landed a catch in a week.",
                        "Clear them off and I'll stand you a hot meal."),
                    Fight(QuestStepKind.Defeat, 13, 4, "Defeat Dock Crabs on the piers", "Dock Crabs"),
                    Talk("Quill", "Return to Harbormaster Quill", "Harbormaster Quill",
                        "That's the piers clear. Funny thing: crabs don't swarm like that unless something's churned up the shallows.",
                        "Something big has been coming and going at night. I'd like to know what.")),
                Q(18, "Something Fishy", 17, 7, 120, 40, 10,
                    "Quill suspects someone is moving cargo through the docks at night. Search the fish barrels stacked along the quay.",
                    Point(QuestStepKind.Interact, "FishBarrel", 3, "Search the fish barrels on the quay", "Fish barrel"),
                    Talk("Quill", "Bring what you found to Harbormaster Quill", "Harbormaster Quill",
                        "Iron? In fish barrels? These are cast pieces, bolts as long as my arm. Nobody on this isle makes iron like that.",
                        "Somebody's smuggling. Fenwick by the south pier sees every boat that comes in.")),
                Q(19, "Night Shipment", 18, 8, 150, 50, 71,
                    "Smugglers unload between the warehouses at night. Take their shipping manifests and show them to Net-mender Fenwick.",
                    Fight(QuestStepKind.Defeat, 14, 4, "Defeat Smuggler Thugs between the warehouses", "Smuggler Thugs"),
                    Fight(QuestStepKind.Collect, 14, 3, "Take shipping manifests from Smuggler Thugs", "Smuggler Thugs"),
                    Talk("Fenwick", "Show the manifests to Net-mender Fenwick", "Net-mender Fenwick",
                        "Manifests, are they? Fish, fish, fish... and every crate signed by the same captain. Marrow Grell.",
                        "Grell keeps the big warehouse at the north end of the quay. Locked tight, always.",
                        "And these numbers aren't fish weights. That's a code, or I'm a herring.")),
                Q(20, "The Coded Ledger", 19, 8, 260, 90, 70,
                    "Captain Marrow Grell runs the smugglers from the big warehouse at the north end of the quay. Break in and find out who he works for.",
                    Point(QuestStepKind.Explore, "Warehouse", 1, "Reach the smugglers' warehouse at the north end of the quay", "Smugglers' warehouse"),
                    Fight(QuestStepKind.Boss, 15, 1, "Defeat Captain Marrow Grell", "Captain Marrow Grell"),
                    Talk("Quill", "Bring Grell's ledger to Harbormaster Quill", "Harbormaster Quill",
                        "Grell said he was paid in silver by 'the iron men'? And this ledger... every page of it is in cipher.",
                        "Take my ring, you've earned it. And take that ledger to the castle. There's a stargazer on the observatory hill who loves a puzzle.",
                        "Kiln Street has been buying odd iron too, they say. Bramwell will want to hear about that.")),
                Offered(Q(21, "Lost Lines", 17, 6, 80, 30, 0,
                    "The crabs tangled Net-mender Fenwick's best nets and dragged them off along the quay.",
                    Point(QuestStepKind.Interact, "LostNet", 3, "Gather Fenwick's lost nets along the quay", "Lost net"),
                    Talk("Fenwick", "Bring the nets back to Net-mender Fenwick", "Net-mender Fenwick",
                        "All three! Hardly a hole in them. Here, for your trouble.")),
                    "Fenwick",
                    "Those crabs tangled my good nets and dragged them off along the quay.",
                    "Three of them. Bring them back and I'll see you right."),
            };
        }

        /// <summary>Cold Iron (Kiln Street, quests 22-26): Cole's secret orders, the first Hollowed, and Captain Vesper.</summary>
        public static void FillColdIron(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Cold Iron";
            ql.storylineEnd = 25;
            ql.quests = new List<QuestEntry>
            {
                Q(22, "Sparks on Kiln Street", 20, 7, 120, 45, 0,
                    "Kiln Street has been buying odd iron, and now its kilns are spitting out Cinder Imps. Forgewarden Ingrid Hale needs a hand. The tunnel is in the south-east wall, past the duelling circle.",
                    Talk("Bramwell", "Talk to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                        "Quill's ledger, eh? I'll hold my questions for now.",
                        "Kiln Street is the other half of it. Forgewarden Hale writes that her kilns are coughing up imps. The tunnel is past the duelling circle."),
                    Talk("Ingrid", "Find Forgewarden Ingrid Hale on Kiln Street (tunnel past the duelling circle)", "Forgewarden Ingrid Hale",
                        "You're from the castle? Good. Cinder Imps, everywhere. Somebody's been running the furnaces hotter than any honest smith would.",
                        "Put out the worst of them, would you?"),
                    Fight(QuestStepKind.Defeat, 16, 4, "Defeat Cinder Imps", "Cinder Imps"),
                    Talk("Ingrid", "Return to Forgewarden Ingrid", "Forgewarden Ingrid Hale",
                        "Better. Now, the hottest furnace on the street is Bertrand Cole's. He's been working nights for months.",
                        "Bertrand's a good man. Or he was.")),
                Q(23, "Odd Orders", 22, 9, 150, 50, 72,
                    "Forge-master Bertrand Cole has been working through the night. Find out what he's making.",
                    Talk("Cole", "Talk to Forge-master Bertrand Cole", "Forge-master Bertrand Cole",
                        "Orders, that's all. Paid in good silver. I don't ask what they're for.",
                        "...Look in the yard if you must. I can't stop you."),
                    Point(QuestStepKind.Interact, "StrangeFrame", 3, "Inspect the strange frames in Cole's yard", "Strange frame"),
                    Talk("Ingrid", "Tell Forgewarden Ingrid what you found", "Forgewarden Ingrid Hale",
                        "Housings. Iron housings for something that burns very, very hot, with a lens where a heart would go.",
                        "And the same mark on every one: a gauntlet closing on a star. Bertrand didn't draw that. Somebody gave him a pattern.",
                        "Wear this cap; you'll want it near those furnaces.")),
                Q(24, "Empty Armor", 23, 9, 190, 60, 10,
                    "Something walks Cole's back forge at night. Ingrid wants to know what, and what it runs on.",
                    Fight(QuestStepKind.Defeat, 17, 3, "Defeat the Hollowed Sentries in Cole's back forge", "Hollowed Sentries"),
                    Fight(QuestStepKind.Collect, 17, 2, "Take glowing cores from Hollowed Sentries", "Hollowed Sentries"),
                    Talk("Ingrid", "Bring the glowing cores to Forgewarden Ingrid", "Forgewarden Ingrid Hale",
                        "Empty. Armor with nobody inside, moving on a spark of... that's light. Stolen light, like the lanterns on Lantern Row.",
                        "Bertrand finally talked. They threatened his family. The one who gives the orders comes to the sealed furnace hall at the top of the street.",
                        "A woman, he says. Calls herself Captain. Signs her patterns with a V.")),
                Q(25, "The Lantern Corps", 24, 9, 300, 110, 73,
                    "The captain who gives Cole his orders works in the sealed furnace hall at the top of Kiln Street. End it.",
                    Point(QuestStepKind.Explore, "FurnaceHall", 1, "Reach the sealed furnace hall at the top of Kiln Street", "Sealed furnace hall"),
                    Fight(QuestStepKind.Boss, 18, 1, "Defeat Captain Vesper", "Captain Vesper"),
                    Talk("Ingrid", "Tell Forgewarden Ingrid what happened", "Forgewarden Ingrid Hale",
                        "She ran? Smashed through the back wall and was gone, laughing. 'The Lantern Corps doesn't lose,' she said. 'It retreats with what it learned.'",
                        "Lantern Corps. Captain. A corps of whose army?",
                        "Here, the best wand Kiln Street ever made. Take it to the castle. Somebody there needs to start asking the right questions.")),
                Offered(Q(26, "The Apprentice's Hammer", 22, 7, 70, 25, 0,
                    "Apprentice Tam dropped Master Cole's best hammer in the slag heaps when the imps came.",
                    Point(QuestStepKind.Interact, "TamsHammer", 1, "Find Tam's hammer in the slag heaps", "Tam's hammer"),
                    Talk("Tam", "Bring the hammer back to Apprentice Tam", "Apprentice Tam",
                        "You found it! Master Cole will never know. Thank you, thank you!")),
                    "Tam",
                    "I dropped Master Cole's best hammer in the slag heaps when the imps came. If he finds out...",
                    "Could you look? It's out past the kiln yard."),
            };
        }

        /// <summary>Stars and Ciphers (the Observatory, quests 27-28): Wick reads the ledger and Bramwell names the Dominion.</summary>
        public static void FillStarsAndCiphers(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Stars and Ciphers";
            ql.storylineEnd = 28;
            ql.quests = new List<QuestEntry>
            {
                Q(27, "Reading the Stars", 20, 8, 150, 50, 74,
                    "Harbormaster Quill said the stargazer on the observatory hill loves a puzzle. Take Grell's coded ledger to Tobias Wick.",
                    Talk("Wick", "Bring the coded ledger to Stargazer Wick on the observatory hill", "Stargazer Tobias Wick",
                        "A cipher! A real one. Oh, this is wonderful. And terrible, I suspect, but wonderful.",
                        "The key is in the stars. Old navigators' ciphers used star bearings. Help me set the lenses and we'll see what this sky says."),
                    Point(QuestStepKind.Interact, "Lens", 3, "Align the brass lenses round the Observatory", "Brass lens"),
                    Talk("Wick", "Return to Stargazer Wick", "Stargazer Tobias Wick",
                        "There. Three bearings... but these aren't our stars. These are stars seen from outside the Everweave.",
                        "Whoever wrote this ledger learned to navigate somewhere else entirely. I need the navigators' cipher book from the Library.",
                        "Take this amulet; it's made from the lens I broke last spring.")),
                Q(28, "A Name in Cipher", 27, 9, 200, 60, 11,
                    "Wick needs the navigators' cipher book from Librarian Wren to finish reading the ledger.",
                    Talk("Wren", "Ask Librarian Wren for the navigators' cipher book", "Librarian Wren",
                        "The navigators' cipher book? Third shelf, behind the dragon atlases. Nobody's asked for it in forty years.",
                        "Do bring it back. And tell Tobias he still owes me two books."),
                    Point(QuestStepKind.Interact, "CipherWheel", 1, "Turn the cipher wheel at the Observatory", "Cipher wheel"),
                    Talk("Wick", "Read the decoded ledger with Stargazer Wick", "Stargazer Tobias Wick",
                        "It's an order. 'Survey complete. Siphons in place. The isle's heart is marked.' And it's signed for... the Dominion.",
                        "I don't know that word. But I know someone who might. Show this to Bramwell."),
                    Talk("Bramwell", "Show the decoded ledger to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                        "...Where did you get this?",
                        "The Dominion. I fought them once, at the edge of the Everweave. Iron, and drums, and they never stopped coming.",
                        "We drove them out. We thought they were gone for good. Say nothing of this yet. Not until I've spoken with the Headmaster.")),
            };
        }

        /// <summary>Straight Lines (Thornhollow Grove, quests 29-32): survey stakes, the first Dominion soldiers and Sergeant Roan's camp.</summary>
        public static void FillStraightLines(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "Straight Lines";
            ql.storylineEnd = 32;
            ql.quests = new List<QuestEntry>
            {
                Q(29, "The Quiet Grove", 25, 9, 160, 55, 0,
                    "Ranger Briony Ashdown says the animals of Thornhollow Grove have gone mad. The grove tunnel is in the south-west wall, past the gazebo garden.",
                    Talk("Bramwell", "Talk to Gatekeeper Bramwell", "Gatekeeper Bramwell",
                        "Ranger Ashdown in Thornhollow Grove sent a hawk. The boars are charging anything that moves, and someone's been cutting the trees in straight lines.",
                        "Straight lines. In a forest. Go and see. The tunnel is past the gazebo garden."),
                    Talk("Briony", "Find Ranger Briony Ashdown in Thornhollow Grove (tunnel past the gazebo garden)", "Ranger Briony Ashdown",
                        "Careful. The boars have gone wild, and I can't calm them.",
                        "Thin them out before someone gets gored."),
                    Fight(QuestStepKind.Defeat, 19, 4, "Defeat maddened Thornback Boars", "Thornback Boars"),
                    Talk("Briony", "Return to Ranger Briony", "Ranger Briony Ashdown",
                        "Thank you. They weren't angry; they were frightened. Something's driven iron into the ground all through the grove, and it hurts them.",
                        "Grandfather Oak will know more. He feels every root.")),
                Q(30, "Cut Lines", 29, 10, 190, 60, 75,
                    "Iron stakes have been driven along a straight line through the grove. Pull them up and ask Grandfather Oak what they're for.",
                    Point(QuestStepKind.Interact, "SurveyStake", 4, "Pull up the survey stakes", "Survey stake"),
                    Talk("Oak", "Speak with Grandfather Oak in his glade", "Grandfather Oak",
                        "Mmm. Better. Each stake was a splinter in my roots.",
                        "They are measuring. A line from the grove to the castle's heart, straight as an arrow. Soldiers walk it at night, writing numbers.",
                        "Little wizard. Wear the warden's robe. The grove remembers who helped it.")),
                Q(31, "Iron in the Woods", 30, 10, 230, 70, 10,
                    "Soldiers in grey iron are surveying the line. Stop them and take their maps.",
                    Fight(QuestStepKind.Defeat, 20, 4, "Defeat Dominion Surveyors", "Dominion Surveyors"),
                    Fight(QuestStepKind.Collect, 20, 3, "Take survey maps from Dominion Surveyors", "Dominion Surveyors"),
                    Talk("Briony", "Bring the survey maps to Ranger Briony", "Ranger Briony Ashdown",
                        "Every map shows the same thing: the castle, the crystal in the middle, and lines drawn to it from everywhere on the isle.",
                        "And this badge: a gauntlet closing on a star. They have a camp at the head of the line, north. A sergeant runs it.")),
                Q(32, "The Sergeant's Camp", 31, 10, 340, 120, 63,
                    "The surveyors answer to a sergeant camped at the head of the line, north of the grove. Break the camp.",
                    Point(QuestStepKind.Explore, "Camp", 1, "Reach the camp at the head of the survey line", "Dominion camp"),
                    Fight(QuestStepKind.Boss, 22, 1, "Defeat Sergeant Halvek Roan", "Sergeant Halvek Roan"),
                    Talk("Briony", "Tell Ranger Briony the camp is broken", "Ranger Briony Ashdown",
                        "Roan said something as he fell? 'The General will have this isle by winter.' What general?",
                        "Whatever this is, it's bigger than the grove. Take this deck; it was my mother's. And go to the castle.")),
            };
        }

        /// <summary>The Quiet Merchant (the Bazaar, quests 33-34): Mabel spots a spy, and he's caught at the docks.</summary>
        public static void FillQuietMerchant(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "The Quiet Merchant";
            ql.storylineEnd = 34;
            ql.quests = new List<QuestEntry>
            {
                Also(Offered(Q(33, "Loose Talk", 25, 10, 180, 60, 0,
                    "Mabel has noticed a map-seller in the Bazaar asking her regulars about you. She'd like you to have a quiet look.",
                    Talk("Pell", "Ask Quartermaster Pell about Merchant Selwyn", "Quartermaster Pell",
                        "Selwyn? Arrived last spring. Sells maps of the isle, very good ones. Too good.",
                        "He's bought every plan of the castle I had. I took him for a collector."),
                    Point(QuestStepKind.Interact, "BazaarStall", 3, "Search the stall crates in the Bazaar", "Stall crate"),
                    Talk("Mabel", "Tell Mabel what you found", "Mabel Brightcask",
                        "Castle plans, guard rotas, and a list of everyone who comes and goes from the Headmaster's tower. And a little iron pin: a gauntlet and a star.",
                        "That's no collector, love. That's a spy.")),
                    "Mabel",
                    "You've been everywhere, love. Docks, forges... so tell me why a map-seller in the Bazaar keeps asking my regulars about you.",
                    "Selwyn, he calls himself. Pays in silver. Nobody pays in silver. Have a quiet look, would you?"), 28),
                Q(34, "The Quiet Merchant", 33, 10, 280, 100, 76,
                    "Merchant Selwyn is a spy. Confront him before he gets away.",
                    Talk("Selwyn", "Confront Merchant Selwyn in the Bazaar", "Merchant Selwyn",
                        "...Ah. Mabel talks too much. A shame; I liked this isle.",
                        "You'll find I'm not where you think I am, wizard. Not for long."),
                    Fight(QuestStepKind.Boss, 23, 1, "Catch Merchant Selwyn in the Tidewatch warehouse", "Merchant Selwyn"),
                    Talk("Bramwell", "Tell Gatekeeper Bramwell about the spy", "Gatekeeper Bramwell",
                        "Silent Office. That's what they called their spies, in the war. If they've had a man inside the castle since spring...",
                        "Then they know everything we do. Put those boots on; you'll need to be quick from here on.")),
            };
        }

        /// <summary>The Lost Name (the Library, quest 35): an old class roster with the player's own name on it.</summary>
        public static void FillLostName(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "The Lost Name";
            ql.storylineEnd = 35;
            ql.quests = new List<QuestEntry>
            {
                Offered(Q(35, "An Old Roster", 28, 9, 180, 40, 0,
                    "Librarian Wren found an old Starfall class roster while fetching the cipher book. Something on it troubles her.",
                    Point(QuestStepKind.Interact, "OldRoster", 1, "Read the old class roster in the library court", "Old class roster"),
                    Talk("Wren", "Ask Librarian Wren about the roster", "Librarian Wren",
                        "One name is scratched out so hard it tore the page. And the one beside it, marked 'lost'... that's your name. Exactly your name.",
                        "A coincidence, surely. Ask the Headmaster; that's his signature at the bottom."),
                    Talk("Headmaster", "Ask Headmaster Greymantle about the roster", "Headmaster Greymantle",
                        "An old roster? Many names come and go in sixty years, young wizard.",
                        "...Leave it with me. There are things I will tell you, when it is time. It is not yet time.")),
                    "Wren",
                    "While I was fetching that cipher book I found something odd: an old Starfall class roster, sixty years old at least.",
                    "Come and look. One of the names is... well. Come and look."),
            };
        }

        /// <summary>The Hollow King (the Hollow Keep, quests 36-38): the keep falls and Morvane confesses.</summary>
        public static void FillHollowKing(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "The Hollow King";
            ql.storylineEnd = 38;
            ql.quests = new List<QuestEntry>
            {
                Also(Q(36, "North to the Keep", 32, 11, 260, 80, 0,
                    "With the Dominion's spy caught and its camp broken, the Headmaster has decided: Morvane's keep in the north must fall. The tunnel is in the north-west wall, past the observatory hill.",
                    Talk("Headmaster", "Report to Headmaster Greymantle", "Headmaster Greymantle",
                        "Smugglers, siphons, surveyors, spies. And behind them all, one word: Dominion.",
                        "Morvane did not do this alone, but he is the door they came through. His keep is in the north. Captain Vale is waiting at the crag.",
                        "Go carefully. And come back."),
                    Talk("Rhona", "Find Watch-Captain Rhona Vale below the Hollow Keep (tunnel past the observatory hill)", "Watch-Captain Rhona Vale",
                        "The Headmaster's wizard. Good. My watch can hold the crag, but we can't take the keep.",
                        "The bailey is full of Hollowed Knights: empty armor, like Kiln Street's, only bigger. Thin them out and we'll talk."),
                    Fight(QuestStepKind.Defeat, 24, 4, "Defeat Hollowed Knights in the outer bailey", "Hollowed Knights"),
                    Talk("Rhona", "Return to Watch-Captain Rhona", "Watch-Captain Rhona Vale",
                        "Every one of them burned with that same stolen light. The glow comes from inside the keep: a hall of lanterns, my scouts say.")), 34),
                Q(37, "The Hall of Lanterns", 36, 12, 300, 90, 77,
                    "Hollow Lanterns burn in the keep's inner ward, feeding the Hollowed. Shatter them.",
                    Point(QuestStepKind.Interact, "HollowLantern", 3, "Shatter the Hollow Lanterns in the inner ward", "Hollow Lantern"),
                    Fight(QuestStepKind.Defeat, 24, 3, "Defeat the Hollowed Knights guarding the hall", "Hollowed Knights"),
                    Talk("Rhona", "Return to Watch-Captain Rhona", "Watch-Captain Rhona Vale",
                        "The light's going out all over the keep. Some of it is even drifting south, back toward the crystal.",
                        "Morvane is in the throne hall at the top. This is it, wizard. Wear this; it's cut from one of his lanterns.")),
                Q(38, "Morvane's Fall", 37, 12, 500, 200, 78,
                    "Morvane the Hollow King waits in his throne hall at the top of the keep. End his reign.",
                    Point(QuestStepKind.Explore, "ThroneHall", 1, "Reach the throne hall at the top of the keep", "Throne hall"),
                    Fight(QuestStepKind.Boss, 25, 1, "Defeat Morvane the Hollow King", "Morvane the Hollow King"),
                    Talk("Rhona", "Tell Watch-Captain Rhona the Hollow King has fallen", "Watch-Captain Rhona Vale",
                        "He spoke, before the end?",
                        "'I sold them the isle for a crown,' he said? 'And they will sell you all for less.' ...Take his crown. Nobody else should wear it."),
                    Talk("Headmaster", "Report to Headmaster Greymantle", "Headmaster Greymantle",
                        "Morvane is beaten, and the light he stole is flowing home. The crystal is answering already.",
                        "But he was only a door, and the ones who came through it are still out there. Rest now, and come back when the crystal is ready.")),
            };
        }
    }
}
