using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Every character's design (see WizardsVRCSceneBuilder.Npcs.cs for the parts): the Headmaster, the seven professors,
    /// and each street NPC by its key. An NPC added in the world data that isn't listed here is dressed from its name
    /// (a marshal gets a hat and badge, a miner a lamp helmet and pick, a ghost glows...) and its robe and hat colours.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private static NpcLook Look(NpcBuild build, bool feminine, Color skin, Color hair, NpcHair style, NpcBeard beard, NpcOutfit outfit,
            Color main, Color second, Color trim, NpcHat hat, Color hatColor, NpcItem right, NpcItem left = NpcItem.None)
        {
            return new NpcLook
            {
                build = build, feminine = feminine, skin = skin, hair = hair, hairStyle = style, beard = beard, outfit = outfit,
                main = main, second = second, trim = trim, hat = hat, hatColor = hatColor, right = right, left = left,
            };
        }

        /// <summary>
        /// Headmaster Greymantle: a tall, upright old scholar in a deep teal robe under a slate-grey mantle lined with stars,
        /// a silver circlet set with a star-blue gem, spectacles, silver hair tied back and a neat short beard; he carries a
        /// silverwood staff with a star crystal floating in a gold ring, a shard of the Starfall Crystal.
        /// </summary>
        private static NpcLook HeadmasterLook()
        {
            var L = Look(NpcBuild.Tall, false, SkinFair, new Color(0.82f, 0.84f, 0.88f), NpcHair.Ponytail, NpcBeard.Short, NpcOutfit.Robe,
                new Color(0.1f, 0.3f, 0.34f), new Color(0.36f, 0.38f, 0.44f), new Color(0.82f, 0.84f, 0.9f), NpcHat.Circlet, new Color(0.82f, 0.84f, 0.9f), NpcItem.StarStaff);
            L.height = 1.04f;
            L.starMantle = true;
            L.highCollar = true;
            L.spectacles = true;
            L.sash = true;
            L.extra = new Color(0.82f, 0.84f, 0.9f);
            L.gem = new Color(0.55f, 0.82f, 1f);
            L.brows = 4f;
            L.idle = 2;
            return L;
        }

        /// <summary>The seven professors (School order: Fire, Ice, Storm, Life, Death, Myth, Balance).</summary>
        private static NpcLook ProfessorLook(int s)
        {
            NpcLook L;
            switch (s)
            {
                case 0:
                    // Ignatia Embercrest, a Draconian: brisk, fierce and always hunched like a dragon over its hoard (her own
                    // slouched idle, her wings lifting as she breathes). Orange scales with a pale gold belly, slit eyes glowing
                    // like coals, a long snout ridged with bone, long horns and cheek fins swept back over a crest of spines, a
                    // long neck carrying her head out in front, great bat wings spread wide, hooked claws on her hands and her
                    // bare feet, a spade-tipped tail; a long green coat open down the front, edged and hemmed in gold, over red.
                    L = Look(NpcBuild.Tall, true, new Color(0.88f, 0.46f, 0.16f), new Color(0.76f, 0.34f, 0.12f), NpcHair.Bald, NpcBeard.None, NpcOutfit.Coat,
                        new Color(0.16f, 0.42f, 0.24f), new Color(0.6f, 0.13f, 0.08f), new Color(0.95f, 0.76f, 0.3f), NpcHat.None, Color.black, NpcItem.None);
                    L.species = NpcSpecies.Draconian; L.belly = new Color(1f, 0.76f, 0.42f); L.horn = new Color(0.66f, 0.32f, 0.13f);
                    L.height = 1.1f; L.coatLength = 1.45f; L.brows = 12f; L.gem = new Color(1f, 0.7f, 0.15f); L.idle = 4;
                    return L;
                case 1:
                    // Halvard Rimeholt: a broad, jolly mountain of a man. White beard, rosy cheeks, a frost-blue greatcoat with
                    // fur collar, cuffs and cap, and a silver staff crowned with icicles.
                    L = Look(NpcBuild.Stout, false, SkinFair, HairWhite, NpcHair.Short, NpcBeard.Long, NpcOutfit.Coat,
                        new Color(0.32f, 0.52f, 0.78f), new Color(0.86f, 0.92f, 1f), new Color(0.8f, 0.9f, 1f), NpcHat.FurCap, new Color(0.25f, 0.4f, 0.62f), NpcItem.IceStaff);
                    L.height = 1.04f; L.furTrim = true; L.gloves = true; L.brows = -6f; L.gem = new Color(0.6f, 0.92f, 1f); L.idle = 0;
                    return L;
                case 2:
                    // Zephyra Voltwind, a kobold from Bigdig: wiry, restless and loud, all big bright eyes and frilled ears edged
                    // in lightning yellow. Slate-blue scales, a crest of violet spines, brass goggles pushed up, a tall-collared
                    // violet coat piped in yellow (a size too big), brass-fingered gloves, a copper coil rod with a crackling spark
                    // gem, and a crate to stand on so she can look her students in the eye.
                    L = Look(NpcBuild.Small, true, new Color(0.36f, 0.44f, 0.66f), new Color(0.6f, 0.38f, 0.9f), NpcHair.Bald, NpcBeard.None, NpcOutfit.Coat,
                        new Color(0.38f, 0.14f, 0.58f), new Color(0.16f, 0.1f, 0.26f), new Color(1f, 0.86f, 0.2f), NpcHat.None, Color.black, NpcItem.SparkRod);
                    L.species = NpcSpecies.Kobold; L.belly = new Color(0.8f, 0.84f, 0.95f); L.extra = new Color(1f, 0.84f, 0.3f); L.eyes = new Color(1f, 0.82f, 0.22f);
                    L.height = 0.8f; L.stand = 0.45f; L.goggles = true; L.highCollar = true; L.gloves = true; L.brows = 6f; L.gem = new Color(0.95f, 0.85f, 1f); L.idle = 1;
                    return L;
                case 3:
                    // Briar Meadowsong, a unicorn: warm and motherly. A pearl-white coat, a flowing mane in blossom pink and mint,
                    // big gentle violet eyes, a spiral horn of pearl and gold that glows softly through her crown of blossoms; a
                    // leaf-green dress with a cream gardener's apron, and a living vine staff that flowers at its tip.
                    L = Look(NpcBuild.Stout, true, new Color(0.97f, 0.95f, 0.99f), new Color(1f, 0.68f, 0.82f), NpcHair.Long, NpcBeard.None, NpcOutfit.Dress,
                        new Color(0.26f, 0.55f, 0.26f), new Color(0.55f, 0.78f, 0.35f), new Color(1f, 0.62f, 0.75f), NpcHat.FlowerCrown, new Color(0.3f, 0.6f, 0.25f), NpcItem.VineStaff);
                    L.species = NpcSpecies.Unicorn; L.mane2 = new Color(0.62f, 0.95f, 0.78f); L.belly = new Color(0.95f, 0.84f, 0.88f); L.horn = new Color(1f, 0.9f, 0.62f);
                    L.eyes = new Color(0.34f, 0.16f, 0.46f); L.height = 1.04f; L.apron = true; L.extra = new Color(0.94f, 0.9f, 0.76f); L.brows = -8f; L.idle = 0;
                    return L;
                case 4:
                    // Mortimer Gloamsby: tall, thin and courteous as an undertaker. Pale, a pencil moustache, a plum-black top
                    // hat, a bone-trimmed high-collared frock coat and cape, spectacles, and a skull-topped ebony cane whose eyes glow.
                    L = Look(NpcBuild.Tall, false, SkinPale, HairBlack, NpcHair.Short, NpcBeard.Mustache, NpcOutfit.Coat,
                        new Color(0.12f, 0.09f, 0.14f), new Color(0.86f, 0.83f, 0.72f), new Color(0.86f, 0.83f, 0.72f), NpcHat.TopHat, new Color(0.18f, 0.1f, 0.2f), NpcItem.SkullCane);
                    L.height = 1.08f; L.highCollar = true; L.cape = true; L.extra = new Color(0.2f, 0.12f, 0.24f); L.spectacles = true; L.gloves = true;
                    L.brows = 3f; L.gem = new Color(0.45f, 1f, 0.6f); L.idle = 2;
                    return L;
                case 5:
                    // Orsolya Fablewright, a minotaur: a storyteller with a regal bearing, broad and towering. Deep brown fur, a
                    // pale muzzle with a gold ring, ivory horns curling up and banded in gold, a long black mane under a gold
                    // laurel; a saffron robe with a russet cape and sash, and an open book of legends floating, glowing, over her palm.
                    L = Look(NpcBuild.Hulking, true, new Color(0.42f, 0.27f, 0.17f), HairBlack, NpcHair.Long, NpcBeard.None, NpcOutfit.Robe,
                        new Color(0.82f, 0.62f, 0.14f), new Color(0.42f, 0.24f, 0.1f), new Color(0.95f, 0.8f, 0.35f), NpcHat.Laurel, new Color(0.95f, 0.78f, 0.3f), NpcItem.None, NpcItem.OpenBook);
                    L.species = NpcSpecies.Minotaur; L.belly = new Color(0.78f, 0.64f, 0.56f); L.horn = new Color(0.94f, 0.88f, 0.72f);
                    L.height = 1.04f; L.cape = true; L.sash = true; L.extra = new Color(0.55f, 0.22f, 0.12f); L.brows = 2f; L.gem = new Color(1f, 0.9f, 0.5f); L.idle = 2;
                    return L;
                default:
                    // Sahra Dunewise: calm and weathered by the desert. A sand-coloured head wrap with a sun gem, layered tan
                    // robes, a saffron scarf and sash, and a staff hung with a pair of balance pans.
                    L = Look(NpcBuild.Slim, true, SkinTan, HairBlack, NpcHair.Short, NpcBeard.None, NpcOutfit.Robe,
                        new Color(0.74f, 0.55f, 0.32f), new Color(0.58f, 0.3f, 0.15f), new Color(0.6f, 0.3f, 0.15f), NpcHat.Turban, new Color(0.88f, 0.74f, 0.5f), NpcItem.BalanceStaff);
                    L.scarf = true; L.sash = true; L.extra = new Color(0.95f, 0.66f, 0.16f); L.brows = -2f; L.gem = new Color(1f, 0.7f, 0.25f); L.idle = 2;
                    return L;
            }
        }

        /// <summary>A street NPC's design: its own if it's listed here, otherwise dressed from its name and colours.</summary>
        private static NpcLook NpcLookFor(ZoneNpc npc)
        {
            NpcLook L = NamedLook(npc.key);
            if (L == null) L = InferredLook(npc);
            if (npc.displayName != null && npc.displayName.ToLowerInvariant().Contains("ghost")) L.ghost = true;
            return L;
        }

        private static NpcLook NamedLook(string key)
        {
            NpcLook L;
            switch (key)
            {
                // ---------------- Starfall Castle
                case "Bramwell":   // Gatekeeper: a big old soldier in a blue tabard over mail, helmet, keys and halberd
                    L = Look(NpcBuild.Hulking, false, SkinBrown, HairGrey, NpcHair.Short, NpcBeard.Short, NpcOutfit.Armor,
                        new Color(0.22f, 0.3f, 0.6f), new Color(0.7f, 0.7f, 0.74f), new Color(0.85f, 0.7f, 0.3f), NpcHat.Helmet, new Color(0.85f, 0.7f, 0.3f), NpcItem.Halberd, NpcItem.Keys);
                    L.brows = -4f; return L;
                case "Pell":   // Quartermaster: round, bustling, spectacles and a bowler, ledger always open
                    L = Look(NpcBuild.Stout, false, SkinLight, HairBrown, NpcHair.Short, NpcBeard.Mustache, NpcOutfit.Vest,
                        new Color(0.6f, 0.4f, 0.2f), new Color(0.92f, 0.88f, 0.78f), new Color(0.85f, 0.7f, 0.3f), NpcHat.Bowler, new Color(0.3f, 0.2f, 0.12f), NpcItem.None, NpcItem.Ledger);
                    L.spectacles = true; L.satchel = true; return L;
                case "Wren":   // Librarian: slight, silver bun, spectacles, a violet robe and scarf, a book and quill
                    L = Look(NpcBuild.Slim, true, SkinFair, HairGrey, NpcHair.Bun, NpcBeard.None, NpcOutfit.Robe,
                        new Color(0.35f, 0.25f, 0.5f), new Color(0.22f, 0.15f, 0.32f), new Color(0.85f, 0.75f, 0.45f), NpcHat.None, Color.black, NpcItem.Quill, NpcItem.Book);
                    L.spectacles = true; L.scarf = true; L.extra = new Color(0.62f, 0.5f, 0.75f); L.idle = 2; return L;
                case "Holloway":   // Duel Master: a straight-backed duellist in a crimson coat with gold epaulettes and a cape
                    L = Look(NpcBuild.Average, false, SkinTan, HairBlack, NpcHair.Short, NpcBeard.Short, NpcOutfit.Coat,
                        new Color(0.55f, 0.13f, 0.13f), new Color(0.15f, 0.1f, 0.1f), new Color(0.95f, 0.78f, 0.3f), NpcHat.None, Color.black, NpcItem.Staff);
                    L.epaulettes = true; L.cape = true; L.extra = new Color(0.25f, 0.06f, 0.06f); L.gloves = true; L.brows = 8f; L.gem = new Color(1f, 0.3f, 0.25f); return L;
                case "Mabel":   // Mabel Brightcask of the Crystal Tap: cheery, blonde bun, apron, a foaming mug
                    L = Look(NpcBuild.Stout, true, SkinLight, HairBlonde, NpcHair.Bun, NpcBeard.None, NpcOutfit.Dress,
                        new Color(0.62f, 0.32f, 0.2f), new Color(0.9f, 0.8f, 0.65f), new Color(0.95f, 0.85f, 0.6f), NpcHat.None, Color.black, NpcItem.Mug);
                    L.apron = true; L.extra = new Color(0.96f, 0.94f, 0.88f); L.brows = -8f; L.idle = 1; return L;
                case "Wick":   // Stargazer Tobias Wick: wild white hair, spectacles, a star-spangled hat and telescope
                    L = Look(NpcBuild.Elder, false, SkinFair, HairWhite, NpcHair.Wild, NpcBeard.None, NpcOutfit.Robe,
                        new Color(0.12f, 0.15f, 0.35f), new Color(0.2f, 0.24f, 0.45f), new Color(0.7f, 0.65f, 0.3f), NpcHat.StarHat, new Color(0.1f, 0.12f, 0.3f), NpcItem.None, NpcItem.Telescope);
                    L.spectacles = true; L.idle = 2; return L;
                case "Selwyn":   // Merchant Selwyn: a travelling map-seller, ponytail, feathered hat, satchel and a rolled chart
                    L = Look(NpcBuild.Average, false, SkinBrown, HairBlack, NpcHair.Ponytail, NpcBeard.Mustache, NpcOutfit.Coat,
                        new Color(0.32f, 0.3f, 0.38f), new Color(0.8f, 0.7f, 0.5f), new Color(0.75f, 0.55f, 0.3f), NpcHat.Ranger, new Color(0.28f, 0.22f, 0.3f), NpcItem.None, NpcItem.Scroll);
                    L.satchel = true; L.idle = 1; return L;
                case "Herald":   // Dominion Herald: grey tabard and plumed helmet in the Dominion's red, a white flag of truce
                    L = Look(NpcBuild.Tall, false, SkinLight, HairBrown, NpcHair.Short, NpcBeard.None, NpcOutfit.Armor,
                        new Color(0.38f, 0.4f, 0.44f), new Color(0.6f, 0.6f, 0.64f), new Color(0.6f, 0.08f, 0.1f), NpcHat.PlumedHelmet, new Color(0.6f, 0.08f, 0.1f), NpcItem.Flag);
                    L.cape = true; L.extra = new Color(0.45f, 0.06f, 0.08f); L.brows = 6f; return L;
                case "Moss":   // Gardener Moss: old, stooped, straw hat and green overalls, a trowel
                    L = Look(NpcBuild.Elder, false, SkinTan, HairGrey, NpcHair.Short, NpcBeard.Short, NpcOutfit.Overalls,
                        new Color(0.3f, 0.45f, 0.2f), new Color(0.88f, 0.84f, 0.7f), new Color(0.45f, 0.35f, 0.15f), NpcHat.Straw, new Color(0.88f, 0.75f, 0.42f), NpcItem.Trowel);
                    L.gloves = true; L.brows = -6f; L.idle = 2; return L;
                // ---------------- Lantern Row
                case "Tilda":   // Watchwoman: hooded green cloak, ponytail, lantern held up against the dark
                    L = Look(NpcBuild.Average, true, SkinLight, HairBrown, NpcHair.Ponytail, NpcBeard.None, NpcOutfit.Coat,
                        new Color(0.25f, 0.32f, 0.2f), new Color(0.6f, 0.5f, 0.35f), new Color(0.75f, 0.6f, 0.3f), NpcHat.Hood, new Color(0.2f, 0.27f, 0.17f), NpcItem.Staff, NpcItem.Lantern);
                    L.cape = true; L.extra = new Color(0.2f, 0.27f, 0.17f); L.badge = true; L.brows = 4f; return L;
                case "Marla":   // Marla the Candlemaker: round, auburn bun under a kerchief, wax-spotted apron, a lit candle
                    L = Look(NpcBuild.Stout, true, SkinFair, HairAuburn, NpcHair.Bun, NpcBeard.None, NpcOutfit.Dress,
                        new Color(0.55f, 0.35f, 0.2f), new Color(0.8f, 0.6f, 0.4f), new Color(0.9f, 0.75f, 0.45f), NpcHat.Kerchief, new Color(0.75f, 0.3f, 0.2f), NpcItem.Candle);
                    L.apron = true; L.extra = new Color(0.92f, 0.88f, 0.78f); L.brows = -6f; return L;
                case "Pip":   // Pip: a small, eager kid in a hat far too big, a storm-purple scarf and a toy wand
                    L = Look(NpcBuild.Small, false, SkinTan, HairBrown, NpcHair.Wild, NpcBeard.None, NpcOutfit.Tunic,
                        new Color(0.3f, 0.45f, 0.65f), new Color(0.8f, 0.75f, 0.6f), new Color(0.6f, 0.38f, 0.9f), NpcHat.Pointed, new Color(0.22f, 0.32f, 0.5f), NpcItem.Wand);
                    L.hatScale = 1.25f; L.scarf = true; L.extra = new Color(0.55f, 0.3f, 0.85f); L.brows = -4f; L.gem = new Color(0.8f, 0.6f, 1f); L.idle = 1; return L;
                // ---------------- Tidewatch Docks
                case "Quill":   // Harbormaster Odalys Quill: navy coat with gold buttons and epaulettes, captain's cap, spyglass
                    L = Look(NpcBuild.Average, true, SkinBrown, HairBlack, NpcHair.Ponytail, NpcBeard.None, NpcOutfit.Coat,
                        new Color(0.16f, 0.24f, 0.4f), new Color(0.88f, 0.86f, 0.8f), new Color(0.95f, 0.78f, 0.3f), NpcHat.Captain, new Color(0.14f, 0.2f, 0.34f), NpcItem.None, NpcItem.Telescope);
                    L.epaulettes = true; L.brows = 6f; return L;
                case "Fenwick":   // Net-mender: weathered old sailor, grey beard, knit cap and a bundle of net with cork floats
                    L = Look(NpcBuild.Elder, false, SkinLight, HairGrey, NpcHair.Short, NpcBeard.Long, NpcOutfit.Vest,
                        new Color(0.45f, 0.4f, 0.3f), new Color(0.75f, 0.72f, 0.66f), new Color(0.5f, 0.42f, 0.3f), NpcHat.FlatCap, new Color(0.28f, 0.32f, 0.42f), NpcItem.None, NpcItem.Net);
                    L.idle = 2; return L;
                // ---------------- Kiln Street
                case "Ingrid":   // Forgewarden Ingrid Hale: strong, red braids, goggles, leather vest and apron, a hammer
                    L = Look(NpcBuild.Hulking, true, SkinLight, HairRed, NpcHair.Braids, NpcBeard.None, NpcOutfit.Vest,
                        new Color(0.45f, 0.24f, 0.14f), new Color(0.8f, 0.7f, 0.58f), new Color(0.75f, 0.5f, 0.25f), NpcHat.None, Color.black, NpcItem.Hammer);
                    L.goggles = true; L.apron = true; L.extra = new Color(0.32f, 0.2f, 0.12f); L.gloves = true; L.brows = 8f; return L;
                case "Cole":   // Forge-master Bertrand Cole: huge, bald, black beard, soot-grey shirt and leather apron, tongs
                    L = Look(NpcBuild.Hulking, false, SkinDeep, HairBlack, NpcHair.Bald, NpcBeard.Short, NpcOutfit.Vest,
                        new Color(0.3f, 0.26f, 0.24f), new Color(0.5f, 0.47f, 0.44f), new Color(0.6f, 0.45f, 0.25f), NpcHat.None, Color.black, NpcItem.Tongs);
                    L.height = 1.04f; L.apron = true; L.extra = new Color(0.35f, 0.22f, 0.12f); L.gloves = true; L.brows = 10f; L.idle = 1; return L;
                case "Tam":   // Apprentice Tam: a gangly teen with goggles on, sooty apron and a little hammer
                    L = Look(NpcBuild.Small, false, SkinLight, HairBrown, NpcHair.Wild, NpcBeard.None, NpcOutfit.Tunic,
                        new Color(0.55f, 0.45f, 0.3f), new Color(0.7f, 0.62f, 0.5f), new Color(0.6f, 0.45f, 0.25f), NpcHat.None, Color.black, NpcItem.Hammer);
                    L.height = 1.15f; L.goggles = true; L.apron = true; L.extra = new Color(0.4f, 0.28f, 0.18f); L.idle = 1; return L;
                // ---------------- Thornhollow Grove
                case "Briony":   // Ranger Briony Ashdown: auburn ponytail, green tunic and brown cloak, feathered ranger hat, staff
                    L = Look(NpcBuild.Slim, true, SkinLight, HairAuburn, NpcHair.Ponytail, NpcBeard.None, NpcOutfit.Tunic,
                        new Color(0.25f, 0.4f, 0.22f), new Color(0.55f, 0.45f, 0.3f), new Color(0.85f, 0.35f, 0.2f), NpcHat.Ranger, new Color(0.35f, 0.28f, 0.15f), NpcItem.Staff);
                    L.cape = true; L.extra = new Color(0.36f, 0.26f, 0.16f); L.satchel = true; L.brows = 4f; L.gem = new Color(0.5f, 0.95f, 0.5f); return L;
                case "Oak":   // Grandfather Oak: the grove's old tree spirit; bark body, roots for feet, a crown of leaves, a beard of moss
                    L = Look(NpcBuild.Hulking, false, new Color(0.42f, 0.3f, 0.18f), new Color(0.3f, 0.45f, 0.2f), NpcHair.Bald, NpcBeard.Long, NpcOutfit.Bark,
                        new Color(0.42f, 0.3f, 0.18f), new Color(0.42f, 0.3f, 0.18f), new Color(0.3f, 0.45f, 0.2f), NpcHat.Leaves, new Color(0.24f, 0.45f, 0.18f), NpcItem.None);
                    L.height = 1.18f; L.beardColor = new Color(0.32f, 0.48f, 0.22f); L.eyes = new Color(0.85f, 0.95f, 0.5f); L.glowEyes = true; L.gem = new Color(0.8f, 1f, 0.5f);
                    L.brows = -6f; L.idle = 2; return L;
                // ---------------- The Hollow Keep
                case "Rhona":   // Watch-Captain Rhona Vale: steel and blue, gold-rimmed helmet, cape, halberd
                    L = Look(NpcBuild.Average, true, SkinTan, HairBlack, NpcHair.Bun, NpcBeard.None, NpcOutfit.Armor,
                        new Color(0.28f, 0.3f, 0.5f), new Color(0.7f, 0.7f, 0.74f), new Color(0.85f, 0.75f, 0.35f), NpcHat.Helmet, new Color(0.85f, 0.72f, 0.3f), NpcItem.Halberd);
                    L.cape = true; L.extra = new Color(0.22f, 0.25f, 0.45f); L.brows = 8f; return L;
                // ---------------- Archrock
                case "Ada":   // Marshal Ada Quickwater: wide hat, star badge, leather vest over a white shirt, red neckerchief
                    L = Look(NpcBuild.Average, true, SkinTan, HairAuburn, NpcHair.Ponytail, NpcBeard.None, NpcOutfit.Vest,
                        new Color(0.42f, 0.3f, 0.2f), new Color(0.92f, 0.9f, 0.84f), new Color(0.75f, 0.6f, 0.3f), NpcHat.Cowboy, new Color(0.32f, 0.22f, 0.14f), NpcItem.None);
                    L.badge = true; L.scarf = true; L.extra = new Color(0.72f, 0.14f, 0.12f); L.gloves = true; L.brows = 6f; return L;
                case "Ezra":   // Ezra Pike, Pumpwright: grey whiskers, flat cap, blue overalls, a big wrench
                    L = Look(NpcBuild.Average, false, SkinFair, HairGrey, NpcHair.Short, NpcBeard.Short, NpcOutfit.Overalls,
                        new Color(0.34f, 0.42f, 0.55f), new Color(0.82f, 0.74f, 0.6f), new Color(0.6f, 0.5f, 0.3f), NpcHat.FlatCap, new Color(0.3f, 0.3f, 0.34f), NpcItem.Wrench);
                    L.brows = -4f; L.idle = 2; return L;
                case "Dusty":   // Dusty Mae Callahan: runs the practice corral; blonde braids, fringed vest, hat, a practice wand
                    L = Look(NpcBuild.Slim, true, SkinLight, HairBlonde, NpcHair.Braids, NpcBeard.None, NpcOutfit.Vest,
                        new Color(0.62f, 0.4f, 0.26f), new Color(0.75f, 0.85f, 0.95f), new Color(0.85f, 0.6f, 0.35f), NpcHat.Cowboy, new Color(0.55f, 0.38f, 0.22f), NpcItem.Wand);
                    L.scarf = true; L.extra = new Color(0.25f, 0.45f, 0.7f); L.brows = -4f; L.idle = 1; return L;
                case "Hobbs":   // Stationmaster Hobbs: portly, bushy handlebar, navy coat, conductor's cap, a timetable
                    L = Look(NpcBuild.Stout, false, SkinLight, HairGrey, NpcHair.Short, NpcBeard.Handlebar, NpcOutfit.Coat,
                        new Color(0.18f, 0.24f, 0.42f), new Color(0.9f, 0.88f, 0.82f), new Color(0.95f, 0.78f, 0.3f), NpcHat.Conductor, new Color(0.14f, 0.18f, 0.32f), NpcItem.None, NpcItem.Ledger);
                    L.brows = 2f; return L;
                case "Sal":   // Sal Mesquite, saloon keeper: bald, handlebar moustache, red vest, white apron, a mug
                    L = Look(NpcBuild.Stout, false, SkinTan, HairBlack, NpcHair.Bald, NpcBeard.Handlebar, NpcOutfit.Vest,
                        new Color(0.58f, 0.16f, 0.14f), new Color(0.94f, 0.92f, 0.86f), new Color(0.85f, 0.7f, 0.3f), NpcHat.Bowler, new Color(0.2f, 0.15f, 0.12f), NpcItem.Mug);
                    L.apron = true; L.extra = new Color(0.96f, 0.95f, 0.9f); L.brows = -6f; L.idle = 1; return L;
                case "Juniper":   // Grandma Juniper: tiny, white bun, bonnet and shawl, lavender dress, a walking stick
                    L = Look(NpcBuild.Elder, true, SkinLight, HairWhite, NpcHair.Bun, NpcBeard.None, NpcOutfit.Dress,
                        new Color(0.5f, 0.42f, 0.6f), new Color(0.75f, 0.7f, 0.82f), new Color(0.9f, 0.85f, 0.95f), NpcHat.Bonnet, new Color(0.62f, 0.55f, 0.72f), NpcItem.Cane);
                    L.height = 0.92f; L.scarf = true; L.extra = new Color(0.62f, 0.38f, 0.42f); L.spectacles = true; L.brows = -8f; L.idle = 2; return L;
                // ---------------- Tumbleweed Row
                case "Hattie":   // Hattie Wells, Express Agent: visor cap, smart blue coat, mail satchel, a bundle of letters
                    L = Look(NpcBuild.Average, true, SkinDeep, HairBlack, NpcHair.Bun, NpcBeard.None, NpcOutfit.Coat,
                        new Color(0.28f, 0.34f, 0.5f), new Color(0.9f, 0.88f, 0.82f), new Color(0.85f, 0.7f, 0.3f), NpcHat.Conductor, new Color(0.22f, 0.26f, 0.38f), NpcItem.None, NpcItem.Scroll);
                    L.satchel = true; L.brows = 4f; return L;
                case "Ledbetter":   // Banker Ledbetter: plump, sideburns, spectacles, charcoal frock coat and top hat, a ledger
                    L = Look(NpcBuild.Stout, false, SkinFair, HairGrey, NpcHair.Short, NpcBeard.Sideburns, NpcOutfit.Coat,
                        new Color(0.22f, 0.22f, 0.26f), new Color(0.65f, 0.55f, 0.35f), new Color(0.85f, 0.7f, 0.3f), NpcHat.TopHat, new Color(0.1f, 0.1f, 0.12f), NpcItem.None, NpcItem.Ledger);
                    L.spectacles = true; L.brows = 6f; L.idle = 2; return L;
                case "Pete":   // Pete the Stable Boy: freckled kid, straw hat, overalls, a pitchfork taller than he is
                    L = Look(NpcBuild.Small, false, SkinLight, HairBlonde, NpcHair.Wild, NpcBeard.None, NpcOutfit.Overalls,
                        new Color(0.4f, 0.5f, 0.68f), new Color(0.85f, 0.42f, 0.32f), new Color(0.6f, 0.45f, 0.25f), NpcHat.Straw, new Color(0.9f, 0.76f, 0.42f), NpcItem.Pitchfork);
                    L.brows = -6f; L.idle = 1; return L;
                // ---------------- Coyote Gulch
                case "Rosa":   // Rosa Calder, Rancher: dark braids, red-brown vest, wide hat, a coiled lasso
                    L = Look(NpcBuild.Average, true, SkinTan, HairBlack, NpcHair.Braids, NpcBeard.None, NpcOutfit.Vest,
                        new Color(0.6f, 0.28f, 0.2f), new Color(0.9f, 0.84f, 0.7f), new Color(0.8f, 0.6f, 0.3f), NpcHat.Cowboy, new Color(0.44f, 0.3f, 0.18f), NpcItem.None, NpcItem.Lasso);
                    L.gloves = true; L.brows = 6f; return L;
                case "Gus":   // Prospector Gus: wild grey hair and beard, battered hat, patched overalls, a gold pan
                    L = Look(NpcBuild.Elder, false, SkinTan, HairGrey, NpcHair.Wild, NpcBeard.Long, NpcOutfit.Overalls,
                        new Color(0.5f, 0.42f, 0.32f), new Color(0.75f, 0.35f, 0.25f), new Color(0.6f, 0.45f, 0.25f), NpcHat.Cowboy, new Color(0.4f, 0.34f, 0.26f), NpcItem.None, NpcItem.Pan);
                    L.brows = -4f; L.idle = 2; return L;
                // ---------------- Copperbottom Mine
                case "Nell":   // Shift Boss Nell Harlan: broad-shouldered, cropped hair, lamp helmet, overalls, a pick
                    L = Look(NpcBuild.Hulking, true, SkinBrown, HairBlack, NpcHair.Short, NpcBeard.None, NpcOutfit.Overalls,
                        new Color(0.4f, 0.32f, 0.22f), new Color(0.6f, 0.55f, 0.48f), new Color(0.8f, 0.65f, 0.2f), NpcHat.MinerLamp, new Color(0.85f, 0.68f, 0.2f), NpcItem.Pickaxe);
                    L.gloves = true; L.brows = 8f; return L;
                case "Cobb":   // Old Cobb: pale from the dark, long white beard, a dented lamp helmet and a lantern
                    L = Look(NpcBuild.Elder, false, SkinPale, HairWhite, NpcHair.Short, NpcBeard.Long, NpcOutfit.Overalls,
                        new Color(0.36f, 0.34f, 0.32f), new Color(0.55f, 0.5f, 0.45f), new Color(0.5f, 0.45f, 0.35f), NpcHat.MinerLamp, new Color(0.4f, 0.36f, 0.3f), NpcItem.None, NpcItem.Lantern);
                    L.brows = -2f; L.idle = 2; return L;
                // ---------------- Iron Line Railyard
                case "Eli":   // Switchman Eli Barnes: red cap, striped overalls, a red signal lamp
                    L = Look(NpcBuild.Average, false, SkinDeep, HairBlack, NpcHair.Short, NpcBeard.Short, NpcOutfit.Overalls,
                        new Color(0.3f, 0.34f, 0.46f), new Color(0.85f, 0.82f, 0.75f), new Color(0.6f, 0.15f, 0.12f), NpcHat.FlatCap, new Color(0.6f, 0.15f, 0.12f), NpcItem.None, NpcItem.SignalLamp);
                    L.gloves = true; L.brows = 4f; L.idle = 1; return L;
                // ---------------- Thunder Mesa
                case "Orla":   // Skywatcher Orla: copper hair blown wild, goggles, a long scarf and blue coat, a telescope
                    L = Look(NpcBuild.Slim, true, SkinLight, new Color(0.75f, 0.4f, 0.2f), NpcHair.Wild, NpcBeard.None, NpcOutfit.Coat,
                        new Color(0.3f, 0.36f, 0.55f), new Color(0.75f, 0.7f, 0.6f), new Color(0.95f, 0.8f, 0.3f), NpcHat.None, Color.black, NpcItem.None, NpcItem.Telescope);
                    L.goggles = true; L.scarf = true; L.extra = new Color(0.9f, 0.75f, 0.25f); L.idle = 0; return L;
                case "Basalt":   // Elder Warden Basalt: a living outcrop of the mesa; stone skin, boulder shoulders, crystals, a stone beard
                    L = Look(NpcBuild.Hulking, false, new Color(0.5f, 0.48f, 0.46f), new Color(0.38f, 0.35f, 0.32f), NpcHair.Bald, NpcBeard.Long, NpcOutfit.Robe,
                        new Color(0.55f, 0.47f, 0.4f), new Color(0.45f, 0.4f, 0.36f), new Color(0.75f, 0.45f, 0.28f), NpcHat.None, Color.black, NpcItem.Staff);
                    L.height = 1.15f; L.stony = true; L.gem = new Color(0.7f, 0.6f, 1f); L.glowEyes = true; L.brows = 10f; L.idle = 2; return L;
                // ---------------- Lonesome Bend (the ghosts)
                case "Hollis":   // Deputy Hollis: still wearing his badge and hat
                    L = Look(NpcBuild.Average, false, SkinLight, HairBrown, NpcHair.Short, NpcBeard.None, NpcOutfit.Vest,
                        new Color(0.5f, 0.42f, 0.3f), new Color(0.85f, 0.85f, 0.8f), new Color(0.75f, 0.6f, 0.3f), NpcHat.Cowboy, new Color(0.4f, 0.32f, 0.22f), NpcItem.None);
                    L.badge = true; L.ghost = true; return L;
                case "Mae":   // Widow Mae: a veiled mourning dress, the porch lantern she keeps lit
                    L = Look(NpcBuild.Slim, true, SkinFair, HairBlack, NpcHair.Bun, NpcBeard.None, NpcOutfit.Dress,
                        new Color(0.2f, 0.2f, 0.26f), new Color(0.35f, 0.35f, 0.42f), new Color(0.6f, 0.6f, 0.7f), NpcHat.Veil, new Color(0.15f, 0.15f, 0.2f), NpcItem.None, NpcItem.Lantern);
                    L.ghost = true; L.brows = -6f; return L;
                case "Abel":   // Parson Abel: tall, black coat, wide flat hat, a prayer book
                    L = Look(NpcBuild.Tall, false, SkinLight, HairGrey, NpcHair.Short, NpcBeard.Short, NpcOutfit.Coat,
                        new Color(0.16f, 0.16f, 0.2f), new Color(0.9f, 0.9f, 0.9f), new Color(0.85f, 0.85f, 0.88f), NpcHat.Ranger, new Color(0.14f, 0.14f, 0.18f), NpcItem.None, NpcItem.Book);
                    L.ghost = true; L.highCollar = true; return L;
                case "Silas":   // Old Silas, Stationmaster: cap and coat from thirty years ago, waiting with his lantern
                    L = Look(NpcBuild.Elder, false, SkinLight, HairWhite, NpcHair.Short, NpcBeard.Long, NpcOutfit.Coat,
                        new Color(0.3f, 0.4f, 0.5f), new Color(0.8f, 0.8f, 0.75f), new Color(0.75f, 0.65f, 0.35f), NpcHat.Conductor, new Color(0.22f, 0.3f, 0.38f), NpcItem.None, NpcItem.Lantern);
                    L.ghost = true; return L;
            }
            return null;
        }

        private static bool Says(string s, params string[] words)
        {
            foreach (var w in words) if (s.Contains(w)) return true;
            return false;
        }

        /// <summary>An NPC with no design of its own: dressed from what its name says it does, in its robe and hat colours.</summary>
        private static NpcLook InferredLook(ZoneNpc npc)
        {
            string n = (npc.displayName ?? npc.key ?? "").ToLowerInvariant();
            int h = Mathf.Abs((npc.key ?? "").GetHashCode());
            Color[] skins = { SkinFair, SkinLight, SkinTan, SkinBrown, SkinDeep };
            Color[] hairs = { HairBrown, HairBlack, HairAuburn, HairBlonde, HairGrey };
            bool fem = Says(n, "woman", "widow", "grandma", "lady", "mother", "aunt", "sister", "queen", "madam", "miss ");
            bool old = Says(n, "old ", "elder", "grand", "ancient");
            var L = Look(old ? NpcBuild.Elder : NpcBuild.Average, fem, skins[h % skins.Length], old ? HairGrey : hairs[(h / 5) % hairs.Length],
                fem ? NpcHair.Bun : NpcHair.Short, npc.beard ? NpcBeard.Long : NpcBeard.None, NpcOutfit.Robe,
                npc.robeColor, Color.Lerp(npc.robeColor, Color.white, 0.5f), Color.Lerp(npc.hatColor, new Color(0.9f, 0.75f, 0.35f), 0.5f),
                NpcHat.Pointed, npc.hatColor, NpcItem.Staff);
            if (Says(n, "marshal", "sheriff", "deputy")) { L.outfit = NpcOutfit.Vest; L.hat = NpcHat.Cowboy; L.badge = true; L.right = NpcItem.None; }
            else if (Says(n, "captain", "guard", "watch", "soldier", "knight", "gatekeeper")) { L.outfit = NpcOutfit.Armor; L.hat = NpcHat.Helmet; L.right = NpcItem.Halberd; L.cape = true; }
            else if (Says(n, "miner", "shift boss", "prospector", "digger")) { L.outfit = NpcOutfit.Overalls; L.hat = NpcHat.MinerLamp; L.right = NpcItem.Pickaxe; }
            else if (Says(n, "station", "switchman", "conductor", "express", "porter")) { L.outfit = NpcOutfit.Coat; L.hat = NpcHat.Conductor; L.right = NpcItem.None; L.left = NpcItem.Lantern; }
            else if (Says(n, "banker", "merchant", "trader", "clerk")) { L.outfit = NpcOutfit.Coat; L.hat = NpcHat.TopHat; L.right = NpcItem.None; L.left = NpcItem.Ledger; }
            else if (Says(n, "smith", "forge", "anvil")) { L.outfit = NpcOutfit.Vest; L.hat = NpcHat.None; L.apron = true; L.right = NpcItem.Hammer; L.build = NpcBuild.Hulking; }
            else if (Says(n, "ranger", "hunter", "scout")) { L.outfit = NpcOutfit.Tunic; L.hat = NpcHat.Ranger; L.cape = true; }
            else if (Says(n, "gardener", "farmer", "rancher", "stable")) { L.outfit = NpcOutfit.Overalls; L.hat = NpcHat.Straw; L.right = NpcItem.Pitchfork; }
            else if (Says(n, "harbor", "sailor", "fisher", "net")) { L.outfit = NpcOutfit.Coat; L.hat = NpcHat.Captain; L.right = NpcItem.None; L.left = NpcItem.Telescope; }
            else if (Says(n, "librarian", "scholar", "scribe", "teacher")) { L.spectacles = true; L.hat = NpcHat.None; L.right = NpcItem.Quill; L.left = NpcItem.Book; }
            else if (Says(n, "keeper", "barkeep", "innkeep", "cook")) { L.outfit = NpcOutfit.Vest; L.hat = NpcHat.None; L.apron = true; L.right = NpcItem.Mug; }
            else if (Says(n, "parson", "priest", "monk")) { L.outfit = NpcOutfit.Coat; L.hat = NpcHat.Ranger; L.right = NpcItem.None; L.left = NpcItem.Book; }
            else if (Says(n, "herald", "messenger")) { L.outfit = NpcOutfit.Tunic; L.hat = NpcHat.PlumedHelmet; L.right = NpcItem.Flag; }
            else if (Says(n, "boy", "girl", "kid", "child", "apprentice")) { L.build = NpcBuild.Small; L.outfit = NpcOutfit.Tunic; L.hat = NpcHat.None; L.right = NpcItem.Wand; }
            else if (Says(n, "skywatcher", "stargazer", "astronomer")) { L.hat = NpcHat.StarHat; L.right = NpcItem.None; L.left = NpcItem.Telescope; }
            if (old && L.right == NpcItem.Staff) L.right = NpcItem.Cane;
            if (Says(n, "spirit", "grandfather oak", "treant")) { L.outfit = NpcOutfit.Bark; L.hat = NpcHat.Leaves; L.skin = new Color(0.42f, 0.3f, 0.18f); L.right = NpcItem.None; }
            return L;
        }
    }
}
