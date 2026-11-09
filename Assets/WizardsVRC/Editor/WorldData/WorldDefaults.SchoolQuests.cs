using System.Collections.Generic;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// The school quests (save ids 90-124): every school's professor sends their own wizards out to prove themselves for
    /// the five quest spells besides the trial (L7 trap, L12 bubble, L18 minion, L28 minion, L48 signature spell). The
    /// trial itself (quest 1, L38) is built in. Each quest is offered by the professor in their school hall once you reach
    /// its level and the street it sends you to is open (its prerequisite is that street's gate quest): talk to them, fight
    /// and gather in that street, then bring what you found back. School s, slot k (0-4) has save id 90 + s * 5 + k.
    /// The L28 and L48 quests use Lonesome Bend (the toughest street so far); point them at later worlds' streets as
    /// those are built.
    /// </summary>
    public static partial class WorldDefaults
    {
        public const int SchoolQuestFirstId = 90;
        public const int SchoolQuestsPerSchool = 5;

        /// <summary>The professors, by School (as in the castle's school halls).</summary>
        private static readonly string[] ProfessorTitles =
        {
            "Professor Ignatia Embercrest", "Professor Halvard Rimeholt", "Professor Zephyra Voltwind", "Professor Briar Meadowsong",
            "Professor Mortimer Gloamsby", "Professor Orsolya Fablewright", "Professor Sahra Dunewise",
        };

        private static readonly string[] SchoolKeyNames = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };

        // Where each slot sends you: its level, the gate quest that opens the street, and the street.
        private static readonly int[] SlotLevels = { 7, 12, 18, 28, 48 };
        private static readonly int[] SlotGates = { 5, 32, 55, 63, 77 };

        // Monster ids used below.
        private const int DockCrab = 13, SmugglerThug = 14, Legionnaire = 21, HollowedKnight = 24;
        private const int Trooper = 39, Engineer = 40, BoilerHollowed = 41;
        private const int Prospector = 48, PhantomOutlaw = 49, Driller = 50;

        public static void FillSchoolQuests(Questline ql)
        {
            ql.includeInBuild = true;
            ql.storylineName = "School Quests";
            ql.storylineEnd = -1;
            ql.quests = SchoolQuestList();
        }

        public static List<QuestEntry> SchoolQuestList()
        {
            var list = new List<QuestEntry>();

            // ---------------------------------------------------------------- Fire
            list.Add(ProfQuest(School.Fire, 0, 103, "Smoke and Snares",
                "Professor Embercrest wants proof you can be patient as well as fierce. The smugglers of Tidewatch Docks soak their ropes in tar.",
                new[] { "Fire burns hot and fast, and most young Fire wizards forget it can wait, too.", "Go to Tidewatch Docks. The smugglers soak their ropes in tar; bring me three and I'll show you a fire that lies in wait." },
                Fight(QuestStepKind.Defeat, SmugglerThug, 4, "Defeat Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                Fight(QuestStepKind.Collect, SmugglerThug, 3, "Take tar-soaked rope from Smuggler Thugs", "Smuggler Thugs"),
                "Tar and rope. Coil it so, breathe an ember into it, and it waits. Step on it and it catches.", "That's Cinder Snare. Lay it on your enemy and your next Fire spell bites deeper."));
            list.Add(ProfQuest(School.Fire, 1, 210, "A Sky on Fire",
                "Professor Embercrest says a true Fire wizard can warm a whole battlefield. The Hollow Keep's cold lantern-wicks will teach you how.",
                new[] { "The Hollow Keep is the coldest place on Starfall Isle. Even its lanterns have gone dark.", "Bring me the dead wicks the Hollowed Knights carry, and clear out the Dominion's Legionnaires while you're there." },
                Fight(QuestStepKind.Collect, HollowedKnight, 4, "Take cold lantern-wicks from Hollowed Knights in the Hollow Keep", "Hollowed Knights"),
                Fight(QuestStepKind.Defeat, Legionnaire, 3, "Defeat Dominion Legionnaires in the Hollow Keep", "Dominion Legionnaires"),
                "Four dead wicks. Now watch: one spark, and they all catch at once, and the air itself goes warm.", "Kindled Sky. While it burns, every Fire spell in the duel hits harder. Anyone's. Use it when it's yours to use."));
            list.Add(ProfQuest(School.Fire, 2, 211, "The Whelp in the Boiler",
                "Professor Embercrest has heard of a little fire spirit trapped in the Iron Line's boilers. Free its embers from the Railyard.",
                new[] { "The Dominion's engines burn something that isn't coal. A little fire spirit, caught and fed into the boilers.", "Go to the Iron Line Railyard. Beat their Lantern Corps engineers and bring me the ember cores from the Boiler Hollowed." },
                Fight(QuestStepKind.Defeat, Engineer, 4, "Defeat Lantern Corps Engineers at the Iron Line Railyard", "Lantern Corps Engineers"),
                Fight(QuestStepKind.Collect, BoilerHollowed, 3, "Take ember cores from Boiler Hollowed", "Boiler Hollowed"),
                "Three cores, still warm, and something in them still wriggling. There. Out you come, little one.", "It's chosen you. Call it in a duel: Cinder Whelp. Small, but it bites."));
            list.Add(ProfQuest(School.Fire, 3, 107, "Hound of the Deep Coals",
                "Professor Embercrest wants brimstone from the Dominion's drills at Lonesome Bend, to call up a hound of the deep coals.",
                new[] { "Deep under Dustwind the coals never cool, and the hounds that live there answer to brimstone.", "The Dominion Drillers at Lonesome Bend dig right down to it. Take their brimstone, and deal with the outlaws haunting the town." },
                Fight(QuestStepKind.Collect, Driller, 4, "Take brimstone from Dominion Drillers at Lonesome Bend", "Dominion Drillers"),
                Fight(QuestStepKind.Defeat, PhantomOutlaw, 5, "Defeat Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                "Brimstone. Smell it? So can they. Hold it in your hand and call.", "Magma Hound. Loyal, hungry and very warm. Feed it enemies."));
            list.Add(ProfQuest(School.Fire, 4, 111, "The Wyrm's Ember",
                "Professor Embercrest has one last lesson: the sun-fire of a wyrm. It needs molten heart-iron from the Dominion's fortress at Lonesome Bend.",
                new[] { "You've done everything I've asked, and more. There's one spell left. My own teacher taught it to me.", "Its fire comes from a wyrm that sleeps in the sun. Wake it with heart-iron, molten from the Dominion's drills at Lonesome Bend." },
                Fight(QuestStepKind.Defeat, Driller, 6, "Defeat Dominion Drillers at the fortress (Lonesome Bend)", "Dominion Drillers"),
                Fight(QuestStepKind.Collect, PhantomOutlaw, 5, "Take scorched heart-iron from Phantom Outlaws", "Phantom Outlaws"),
                "Heart-iron, still glowing. Hold it up to the light... There. Do you feel it looking back?", "Sunfire Wyrm. The greatest fire I know. Every enemy burns. You've earned it, wizard."));

            // ---------------------------------------------------------------- Ice
            list.Add(ProfQuest(School.Ice, 0, 115, "Thin Ice",
                "Professor Rimeholt wants frost-rimed shells from Tidewatch Docks' crabs, to teach you a snare of thin ice.",
                new[] { "Ice is patient. It waits under the surface until someone steps where they shouldn't.", "The Dock Crabs at Tidewatch Docks grow frost on their shells. Bring me three, and clear the smugglers off my old pier." },
                Fight(QuestStepKind.Collect, DockCrab, 3, "Take frost-rimed shells from Dock Crabs at Tidewatch Docks", "Dock Crabs"),
                Fight(QuestStepKind.Defeat, SmugglerThug, 4, "Defeat Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                "Good shells. Grind the frost off, so, and spread it thin. Thinner. That's it.", "Frost Snare. Your enemy won't see it until your next Ice spell cracks right through."));
            list.Add(ProfQuest(School.Ice, 1, 212, "The Long Hush",
                "Professor Rimeholt says winter's deepest magic is a silence that settles over a whole battle. The Hollow Keep is full of it.",
                new[] { "Have you heard snow fall at night? That hush is a spell, if you know how to hold it.", "The Legionnaires in the Hollow Keep wear plumes rimed with that hush. Bring me four, and quiet a few Hollowed Knights for me." },
                Fight(QuestStepKind.Collect, Legionnaire, 4, "Take rime-crusted plumes from Dominion Legionnaires in the Hollow Keep", "Dominion Legionnaires"),
                Fight(QuestStepKind.Defeat, HollowedKnight, 3, "Defeat Hollowed Knights in the Hollow Keep", "Hollowed Knights"),
                "Listen. Even the plumes are quiet. Now let that quiet spread over everything.", "Glacial Hush. While it holds, every Ice spell in the duel is stronger. A wise Ice wizard chooses the moment."));
            list.Add(ProfQuest(School.Ice, 2, 213, "A Pup Lost in the Yard",
                "Professor Rimeholt has word of a frost pup chained in the Iron Line Railyard. Free it from the Dominion's troopers.",
                new[] { "The Dominion caught a frost pup in the mountains and chained it in their railyard to cool the engines.", "Take the frost collars off their troopers and put out a few of those boilers. The pup will find you." },
                Fight(QuestStepKind.Collect, Trooper, 3, "Take frost collars from Dominion Troopers at the Iron Line Railyard", "Dominion Troopers"),
                Fight(QuestStepKind.Defeat, BoilerHollowed, 4, "Defeat Boiler Hollowed at the Iron Line Railyard", "Boiler Hollowed"),
                "It followed you all the way here? Of course it did. It knows who freed it.", "Call it in a duel: Rime Pup. Not much of a bite, but it can take a beating."));
            list.Add(ProfQuest(School.Ice, 3, 119, "Sentinel of the Pass",
                "Professor Rimeholt wants glacier cores from the Dominion's drills at Lonesome Bend, to wake an ancient sentinel of ice.",
                new[] { "Long ago, sentinels of living ice guarded the mountain passes. One still sleeps in every glacier.", "The Dominion Drillers at Lonesome Bend have been boring into old ice. Take their glacier cores and put the restless dead there to rest." },
                Fight(QuestStepKind.Collect, Driller, 4, "Take glacier cores from Dominion Drillers at Lonesome Bend", "Dominion Drillers"),
                Fight(QuestStepKind.Defeat, Prospector, 5, "Defeat Restless Prospectors at Lonesome Bend", "Restless Prospectors"),
                "Four cores. Hold them together and feel it stir.", "Rime Sentinel. It will stand in front of you until it shatters. Treat it well."));
            list.Add(ProfQuest(School.Ice, 4, 123, "The Heart of Winter",
                "Professor Rimeholt has one last lesson: winter's wrath. It needs frozen echoes from the Dominion's fortress at Lonesome Bend.",
                new[] { "I have taught you everything but one thing, and that one thing is winter itself.", "In the Dominion's fortress at Lonesome Bend the drills have opened old frozen halls. Clear the drillers out and bring me the frozen echoes the outlaws stole." },
                Fight(QuestStepKind.Defeat, Driller, 6, "Defeat Dominion Drillers at the fortress (Lonesome Bend)", "Dominion Drillers"),
                Fight(QuestStepKind.Collect, PhantomOutlaw, 5, "Take frozen echoes from Phantom Outlaws", "Phantom Outlaws"),
                "Each one is a sound from an age of ice. Listen to them all at once.", "Winter's Wrath. The whole storm of winter on every enemy. Use it gravely."));

            // ---------------------------------------------------------------- Storm
            list.Add(ProfQuest(School.Storm, 0, 127, "Bottled Thunder",
                "Professor Voltwind wants the storm-glass floats the Tidewatch smugglers use, to teach you a snare of bottled thunder.",
                new[] { "You've been throwing lightning around like it's free. It isn't. Sometimes you bottle it.", "The smugglers at Tidewatch Docks float their cargo on storm-glass. Bring me three floats, and clear the crabs off the piers." },
                Fight(QuestStepKind.Collect, SmugglerThug, 3, "Take storm-glass floats from Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                Fight(QuestStepKind.Defeat, DockCrab, 4, "Defeat Dock Crabs at Tidewatch Docks", "Dock Crabs"),
                "Storm-glass. Catch a little spark in it, cork it, and set it down. Then wait.", "Thunder Snare. When your next Storm spell lands, the bottle breaks. Loudly."));
            list.Add(ProfQuest(School.Storm, 1, 214, "Gathering Clouds",
                "Professor Voltwind says a Storm wizard should be able to bring the clouds with them. The Hollow Keep's banners are charged with it.",
                new[] { "Before the lightning, there are clouds. Most Storm wizards never learn to call them.", "The Dominion's Legionnaires in the Hollow Keep pin their banners with charged iron. Take four pins, and thin out the Hollowed Knights." },
                Fight(QuestStepKind.Defeat, HollowedKnight, 4, "Defeat Hollowed Knights in the Hollow Keep", "Hollowed Knights"),
                Fight(QuestStepKind.Collect, Legionnaire, 3, "Take charged banner-pins from Dominion Legionnaires", "Dominion Legionnaires"),
                "Feel them buzz? Hold them up and the clouds come running.", "Thunderhead. Under it every Storm spell in the duel hits harder. Every one, so mind who else is throwing lightning."));
            list.Add(ProfQuest(School.Storm, 2, 215, "Sparks Fly",
                "Professor Voltwind wants loose sparks from the Iron Line's boilers, to make you a little sprite of lightning.",
                new[] { "The Dominion's boilers throw off sparks that never go out. Little lightning sprites, if you're quick.", "Go to the Iron Line Railyard. Catch three sparks from the Boiler Hollowed and knock down a few troopers while you're there." },
                Fight(QuestStepKind.Collect, BoilerHollowed, 3, "Catch loose sparks from Boiler Hollowed at the Iron Line Railyard", "Boiler Hollowed"),
                Fight(QuestStepKind.Defeat, Trooper, 4, "Defeat Dominion Troopers at the Iron Line Railyard", "Dominion Troopers"),
                "Three sparks, and they're already chasing each other round the jar. Let them out. Gently!", "Spark Sprite. Fragile as a soap bubble and twice as fast. It hits like a hammer."));
            list.Add(ProfQuest(School.Storm, 3, 131, "Iron and Lightning",
                "Professor Voltwind wants drill coils from the Dominion at Lonesome Bend, to wind into a golem of lightning.",
                new[] { "Iron conducts. The Dominion knows it. Their drills are wound with coils that drink lightning.", "Take four coils from the Dominion Drillers at Lonesome Bend, and put down some of those Phantom Outlaws." },
                Fight(QuestStepKind.Collect, Driller, 4, "Take drill coils from Dominion Drillers at Lonesome Bend", "Dominion Drillers"),
                Fight(QuestStepKind.Defeat, PhantomOutlaw, 5, "Defeat Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                "Wind them, so. And now a bolt right through the middle...", "Thunder Golem. All crackle and fury. Don't stand too close."));
            list.Add(ProfQuest(School.Storm, 4, 135, "Eye of the Storm",
                "Professor Voltwind has one last lesson: the eye of the storm. It needs storm-struck spikes from the Dominion's fortress at Lonesome Bend.",
                new[] { "There's a spell I've only ever cast twice. It needs a storm with an eye, and an eye needs a center.", "The spikes the Dominion drove at Lonesome Bend have been struck by lightning a hundred times. Bring me five." },
                Fight(QuestStepKind.Defeat, PhantomOutlaw, 6, "Defeat Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                Fight(QuestStepKind.Collect, Driller, 5, "Take storm-struck spikes from Dominion Drillers at the fortress", "Dominion Drillers"),
                "Five spikes in a ring, and the storm turns round them. There it is. The eye.", "Maelstrom Eye. Every enemy at once. Try not to break the arena."));

            // ---------------------------------------------------------------- Life
            list.Add(ProfQuest(School.Life, 0, 139, "Brambles by the Sea",
                "Professor Meadowsong wants sea-holly from the Dock Crabs at Tidewatch Docks, to grow a snare of brambles.",
                new[] { "Life isn't only gentle, dear. A bramble protects its garden with thorns.", "Sea-holly grows on the Dock Crabs at Tidewatch Docks. Bring me three sprigs, and shoo the smugglers away from the beach." },
                Fight(QuestStepKind.Collect, DockCrab, 3, "Gather sea-holly from Dock Crabs at Tidewatch Docks", "Dock Crabs"),
                Fight(QuestStepKind.Defeat, SmugglerThug, 4, "Defeat Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                "Sea-holly. Prickly little thing. Plant it at your enemy's feet and let it grow.", "Bramble Snare. Your next Life spell will find them tangled."));
            list.Add(ProfQuest(School.Life, 1, 216, "Under the Canopy",
                "Professor Meadowsong says the Hollow Keep was an orchard once. Its seeds can grow a canopy over a whole battle.",
                new[] { "Before it was a keep it was an orchard. The Hollowed Knights still carry its seeds without knowing it.", "Bring me four of those seeds from the Hollow Keep, and send off a few Legionnaires." },
                Fight(QuestStepKind.Collect, HollowedKnight, 4, "Gather old orchard seeds from Hollowed Knights in the Hollow Keep", "Hollowed Knights"),
                Fight(QuestStepKind.Defeat, Legionnaire, 3, "Defeat Dominion Legionnaires in the Hollow Keep", "Dominion Legionnaires"),
                "Still alive after all those years. Now let them reach up and spread out over everyone.", "Verdant Canopy. Under it every Life spell, heals and all, is stronger. Even your friends' heals."));
            list.Add(ProfQuest(School.Life, 2, 217, "Seeds in the Cinders",
                "Professor Meadowsong wants the seedlings the Iron Line trampled in the Railyard, to raise a sproutling of your own.",
                new[] { "The Iron Line went straight through a meadow. The Dominion's troopers stamped everything flat.", "Go to the Iron Line Railyard. Save three seedlings from the troopers, and stop the engineers from paving any more." },
                Fight(QuestStepKind.Collect, Trooper, 3, "Rescue trampled seedlings from Dominion Troopers at the Iron Line Railyard", "Dominion Troopers"),
                Fight(QuestStepKind.Defeat, Engineer, 4, "Defeat Lantern Corps Engineers at the Iron Line Railyard", "Lantern Corps Engineers"),
                "Oh, the poor things. A little water, a little sun... Look! That one's waving at you.", "Sproutling. It'll fight beside you and grow braver every day."));
            list.Add(ProfQuest(School.Life, 3, 143, "The Warden Tree",
                "Professor Meadowsong wants ironwood acorns from the graveyard at Lonesome Bend, to wake a warden tree.",
                new[] { "There's an ironwood growing in the graveyard at Lonesome Bend. It's watched over that town for two hundred years.", "The Restless Prospectors have been picking its acorns. Lay them to rest and bring me four." },
                Fight(QuestStepKind.Defeat, Prospector, 5, "Lay Restless Prospectors to rest at Lonesome Bend", "Restless Prospectors"),
                Fight(QuestStepKind.Collect, Prospector, 4, "Take ironwood acorns from Restless Prospectors", "Restless Prospectors"),
                "Ironwood. Hard as stone and twice as patient. Plant one and call.", "Treant Warden. It'll stand between you and harm for as long as it can."));
            list.Add(ProfQuest(School.Life, 4, 147, "The Titan Sleeps",
                "Professor Meadowsong has one last lesson: the titan of the wild wood. It needs heartwood shards from the Dominion's fortress at Lonesome Bend.",
                new[] { "Every forest has a heart, and every heart has a titan sleeping in it. The Dominion has been cutting one down.", "Go to their fortress at Lonesome Bend. Stop the drillers and bring me the heartwood shards the outlaws carried off." },
                Fight(QuestStepKind.Defeat, Driller, 6, "Defeat Dominion Drillers at the fortress (Lonesome Bend)", "Dominion Drillers"),
                Fight(QuestStepKind.Collect, PhantomOutlaw, 5, "Take heartwood shards from Phantom Outlaws", "Phantom Outlaws"),
                "Five shards of one heart. Hold them together. Can you feel it breathing?", "Verdant Titan. The whole wild wood, walking. I'm so proud of you."));

            // ---------------------------------------------------------------- Death
            list.Add(ProfQuest(School.Death, 0, 151, "A Hex on the Harbor",
                "Professor Gloamsby wants drowned coins from the Tidewatch smugglers, to teach you a hex.",
                new[] { "A coin that's been under the sea carries a little of everyone who lost it. Very useful.", "The Smuggler Thugs at Tidewatch Docks fish drowned coins from the harbor. Bring me three, and do something about those crabs." },
                Fight(QuestStepKind.Collect, SmugglerThug, 3, "Take drowned coins from Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                Fight(QuestStepKind.Defeat, DockCrab, 4, "Defeat Dock Crabs at Tidewatch Docks", "Dock Crabs"),
                "Cold, aren't they? Press one into your enemy's shadow and the hex holds.", "Hex Snare. The next Death spell to touch them goes deeper."));
            list.Add(ProfQuest(School.Death, 1, 218, "Into the Gloaming",
                "Professor Gloamsby says dusk itself is a Death spell. The Hollowed Knights of the Hollow Keep are made of it.",
                new[] { "The hour between day and night belongs to us. The gloaming.", "The Hollowed Knights in the Hollow Keep are stitched together with threads of it. Unpick four of them and bring me the threads." },
                Fight(QuestStepKind.Defeat, HollowedKnight, 4, "Defeat Hollowed Knights in the Hollow Keep", "Hollowed Knights"),
                Fight(QuestStepKind.Collect, HollowedKnight, 3, "Take shadow-threads from Hollowed Knights", "Hollowed Knights"),
                "Shadow-thread. Unravel it slowly and the light starts to fade.", "Gloaming. While the dusk lasts, every Death spell in the duel is stronger. Everyone's, mind."));
            list.Add(ProfQuest(School.Death, 2, 219, "Bones in the Boilers",
                "Professor Gloamsby wants the scorched knucklebones the Boiler Hollowed carry, to raise a little scuttler of bone.",
                new[] { "The Boiler Hollowed in the Iron Line Railyard were miners once. Their bones remember digging.", "Bring me three knucklebones from them, and keep the Dominion's troopers busy while you're at it." },
                Fight(QuestStepKind.Collect, BoilerHollowed, 3, "Take scorched knucklebones from Boiler Hollowed at the Iron Line Railyard", "Boiler Hollowed"),
                Fight(QuestStepKind.Defeat, Trooper, 4, "Defeat Dominion Troopers at the Iron Line Railyard", "Dominion Troopers"),
                "Three knucklebones and a whisper. There. It scuttles.", "Bone Scuttler. Not pretty, but it keeps coming."));
            list.Add(ProfQuest(School.Death, 3, 155, "Raising the Thrall",
                "Professor Gloamsby wants the tarnished badges of the Phantom Outlaws at Lonesome Bend, to raise a thrall that serves you.",
                new[] { "A badge is a promise to serve. Even a ghost's badge.", "The Phantom Outlaws at Lonesome Bend still wear theirs. Take four, and lay the Restless Prospectors back in their graves." },
                Fight(QuestStepKind.Collect, PhantomOutlaw, 4, "Take tarnished badges from Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                Fight(QuestStepKind.Defeat, Prospector, 5, "Defeat Restless Prospectors at Lonesome Bend", "Restless Prospectors"),
                "Four promises. Hold them and call, and something will answer.", "Bone Thrall. It serves until it falls apart. Be a kind master."));
            list.Add(ProfQuest(School.Death, 4, 159, "A Requiem",
                "Professor Gloamsby has one last lesson: a requiem of ash. It needs the ashen bells rung over Lonesome Bend's dead.",
                new[] { "There is a song for the end of all things. I have only sung it once.", "At Lonesome Bend the Restless Prospectors carry the funeral bells. Bring me five, after you've silenced the Phantom Outlaws for good." },
                Fight(QuestStepKind.Defeat, PhantomOutlaw, 6, "Defeat Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                Fight(QuestStepKind.Collect, Prospector, 5, "Take ashen bells from Restless Prospectors", "Restless Prospectors"),
                "Ring them together. Listen to the silence after.", "Requiem of Ash. Sing it over every enemy at once. And then let them rest."));

            // ---------------------------------------------------------------- Myth
            list.Add(ProfQuest(School.Myth, 0, 163, "Tall Tales of the Tide",
                "Professor Fablewright wants the smugglers' false maps from Tidewatch Docks, to teach you a snare made of a story.",
                new[] { "A good lie is a trap. A good story is a better one.", "The Smuggler Thugs at Tidewatch Docks draw false maps to fool the harbor watch. Bring me three, and chase off the crabs." },
                Fight(QuestStepKind.Collect, SmugglerThug, 3, "Take false maps from Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                Fight(QuestStepKind.Defeat, DockCrab, 4, "Defeat Dock Crabs at Tidewatch Docks", "Dock Crabs"),
                "Look, they've drawn a sea serpent here. Let's make it true, just for a moment.", "Mirage Snare. They'll believe it right up until your next Myth spell."));
            list.Add(ProfQuest(School.Myth, 1, 220, "The Twice-Told Tale",
                "Professor Fablewright says every story is stronger the second time it's told. The Hollow Keep's tapestries tell an old one.",
                new[] { "The tapestries in the Hollow Keep tell the story of the first king of Starfall. The Hollowed Knights tore them down.", "Bring me four scraps, and send a few Legionnaires running." },
                Fight(QuestStepKind.Collect, HollowedKnight, 4, "Take torn tapestry scraps from Hollowed Knights in the Hollow Keep", "Hollowed Knights"),
                Fight(QuestStepKind.Defeat, Legionnaire, 3, "Defeat Dominion Legionnaires in the Hollow Keep", "Dominion Legionnaires"),
                "Put them side by side. Now tell it again, out loud, and watch the duel listen.", "Twice-Told Tale. While it's told, every Myth spell in the duel is stronger. Everyone's."));
            list.Add(ProfQuest(School.Myth, 2, 164, "Clay and Coal",
                "Professor Fablewright wants kiln clay from the Lantern Corps engineers in the Iron Line Railyard, to shape a golemling.",
                new[] { "Every child shapes a little clay figure once and wishes it could walk. Myth wizards don't have to wish.", "The Lantern Corps engineers at the Iron Line Railyard line their furnaces with good kiln clay. Bring me three lumps, and get past their troopers." },
                Fight(QuestStepKind.Collect, Engineer, 3, "Take kiln clay from Lantern Corps Engineers at the Iron Line Railyard", "Lantern Corps Engineers"),
                Fight(QuestStepKind.Defeat, Trooper, 4, "Defeat Dominion Troopers at the Iron Line Railyard", "Dominion Troopers"),
                "Pinch it, roll it, give it a face. A kind face. There.", "Clay Golemling. It's small, and it'll never stop trying."));
            list.Add(ProfQuest(School.Myth, 3, 167, "The Colossus Wakes",
                "Professor Fablewright wants the carved runestones the Dominion dug up at Lonesome Bend, to wake a colossus of stone.",
                new[] { "The old stories say a colossus of stone sleeps under Dustwind. The Dominion's drills dug up its runes.", "Take four runestones from the Dominion Drillers at Lonesome Bend, and deal with the Phantom Outlaws." },
                Fight(QuestStepKind.Collect, Driller, 4, "Take carved runestones from Dominion Drillers at Lonesome Bend", "Dominion Drillers"),
                Fight(QuestStepKind.Defeat, PhantomOutlaw, 5, "Defeat Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                "Read them in order. Slowly. Hear that? Stone, shifting.", "Stone Colossus. It's slow and it's enormous. Point it the right way."));
            list.Add(ProfQuest(School.Myth, 4, 171, "The Last Legend",
                "Professor Fablewright has one last lesson: the titan from the very first story. Its pages were scattered at Lonesome Bend.",
                new[] { "Every story ever told starts with one older story. A titan walks through it.", "Its pages were scattered when the Dominion stormed Lonesome Bend. The outlaws took them. Bring me five, and clear the fortress." },
                Fight(QuestStepKind.Defeat, Driller, 6, "Defeat Dominion Drillers at the fortress (Lonesome Bend)", "Dominion Drillers"),
                Fight(QuestStepKind.Collect, PhantomOutlaw, 5, "Take pages of the first story from Phantom Outlaws", "Phantom Outlaws"),
                "All five. Now, once upon a time...", "Titan of Legends. The first story, walking. Every enemy will hear it."));

            // ---------------------------------------------------------------- Balance
            list.Add(ProfQuest(School.Balance, 0, 175, "Sand in the Nets",
                "Professor Dunewise wants the sand the Dock Crabs drag up at Tidewatch Docks, to teach you a snare of sand.",
                new[] { "Sand is the most patient thing in the world. It gets into everything, eventually.", "The Dock Crabs at Tidewatch Docks drag up sand from the deep harbor. Bring me three handfuls, and settle the smugglers." },
                Fight(QuestStepKind.Collect, DockCrab, 3, "Gather deep-harbor sand from Dock Crabs at Tidewatch Docks", "Dock Crabs"),
                Fight(QuestStepKind.Defeat, SmugglerThug, 4, "Defeat Smuggler Thugs at Tidewatch Docks", "Smuggler Thugs"),
                "Pour it in a ring around your enemy. Every grain counts.", "Sand Snare. Whatever school your next spell is, it'll find them stuck."));
            list.Add(ProfQuest(School.Balance, 1, 222, "Day and Night",
                "Professor Dunewise says Balance can hold day and night in the same hand. The Hollow Keep has medallions of both.",
                new[] { "At the equinox, day and night are equal. For one moment the whole world is in balance.", "The Hollowed Knights in the Hollow Keep wear sun-and-moon medallions. Bring me three, and weigh in against a few Legionnaires." },
                Fight(QuestStepKind.Defeat, Legionnaire, 3, "Defeat Dominion Legionnaires in the Hollow Keep", "Dominion Legionnaires"),
                Fight(QuestStepKind.Collect, HollowedKnight, 3, "Take sun-and-moon medallions from Hollowed Knights", "Hollowed Knights"),
                "Sun on one side, moon on the other. Spin it, and hold the moment.", "Equinox. While it holds, every Balance spell in the duel is stronger. Everyone's. Balance, after all."));
            list.Add(ProfQuest(School.Balance, 2, 223, "The Scarab's Shell",
                "Professor Dunewise wants brass scarab charms from the Lantern Corps engineers in the Iron Line Railyard.",
                new[] { "The scarab rolls the sun across the sky every day. Small, and stubborn, and never tired.", "The Lantern Corps engineers at the Iron Line Railyard carry brass scarab charms for luck. Bring me three, and cool down some Boiler Hollowed." },
                Fight(QuestStepKind.Collect, Engineer, 3, "Take brass scarab charms from Lantern Corps Engineers at the Iron Line Railyard", "Lantern Corps Engineers"),
                Fight(QuestStepKind.Defeat, BoilerHollowed, 4, "Defeat Boiler Hollowed at the Iron Line Railyard", "Boiler Hollowed"),
                "Brass outside, and inside... sand. Warm sand. It's moving.", "Dune Scarab. It'll roll into any fight you point it at."));
            list.Add(ProfQuest(School.Balance, 3, 179, "Sand and Iron",
                "Professor Dunewise wants the ironsand the Dominion drilled up at Lonesome Bend, to raise a golem of sand.",
                new[] { "Sand and iron, mixed just right, makes a golem that can't be broken, only scattered and gathered again.", "The Dominion Drillers at Lonesome Bend bring up ironsand. Take four sacks, and lay the Restless Prospectors to rest." },
                Fight(QuestStepKind.Collect, Driller, 4, "Take ironsand from Dominion Drillers at Lonesome Bend", "Dominion Drillers"),
                Fight(QuestStepKind.Defeat, Prospector, 5, "Defeat Restless Prospectors at Lonesome Bend", "Restless Prospectors"),
                "Equal parts. Exactly equal. And now it stands.", "Sand Golem. Strong, steady and patient, like a good Balance wizard."));
            list.Add(ProfQuest(School.Balance, 4, 183, "The Grand Convergence",
                "Professor Dunewise has one last lesson: the convergence of every school. It needs balance stones from the Dominion's fortress at Lonesome Bend.",
                new[] { "Every school draws from the same well. Once in a lifetime a Balance wizard learns to draw from all of it at once.", "The Dominion's drills at Lonesome Bend cut through stones that hold every school's magic in balance. Bring me five, and clear the outlaws away." },
                Fight(QuestStepKind.Defeat, PhantomOutlaw, 6, "Defeat Phantom Outlaws at Lonesome Bend", "Phantom Outlaws"),
                Fight(QuestStepKind.Collect, Driller, 5, "Take balance stones from Dominion Drillers at the fortress", "Dominion Drillers"),
                "Seven colors in each stone, all perfectly even. Lay them out in a circle.", "Grand Convergence. Every school, every enemy, at once. You've earned it."));

            return list;
        }

        /// <summary>
        /// One school quest: offered by the school's professor (to their own wizards) from the slot's level once its street
        /// is open; two fights or gathers there, then back to the professor, who teaches the spell.
        /// </summary>
        private static QuestEntry ProfQuest(School school, int slot, int spell, string title, string description, string[] offer,
            QuestStepEntry first, QuestStepEntry second, params string[] thanks)
        {
            int s = (int)school;
            int level = SlotLevels[slot];
            string prof = ProfessorTitles[s];
            string key = SchoolKeyNames[s];
            int xp = (level * 100 + level * level * 10) / 4;
            var q = Offered(Q(SchoolQuestFirstId + s * SchoolQuestsPerSchool + slot, title, SlotGates[slot], level, xp, 20 + level * 4, 0,
                description + " Talk to " + prof + " in the " + key + " school hall.",
                first, second,
                Talk(key, "Return to " + prof, prof, thanks)), key, offer);
            q.onlyForSchool = true;
            q.school = school;
            q.rewardSpell = spell;
            return q;
        }
    }
}
