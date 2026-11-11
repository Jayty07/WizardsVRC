using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// Static spell card definitions (original placeholder spells). Each school has 14 spells: the 12-spell tree (tier k has
    /// id 100 + school * 12 + k) and two newer ones (ids 210-223). Professor spells each need the professor spell before them
    /// (<see cref="prereqs"/>); school-quest spells have none. General cards are 200-204.
    /// <see cref="aux"/> is effect-specific: pierce % for damage and stuns, heal % for drain, burn total for DoT, rounds for taunt,
    /// minion action power, and 1 = "any school" for shields/blades/traps. Damage spells hit for a random amount from
    /// <see cref="powers"/> (min) to <see cref="powerMax"/> (max); powerMax 0 = a fixed value.
    /// </summary>
    /// <remarks>
    /// School roles: Storm = glass cannon (highest damage, low accuracy/health); Fire = damage over time + detonate;
    /// Ice = tank (any-school shields, taunts, high health/resist, low damage); Life = healer (single/group heals, best
    /// accuracy); Death = drain/self-sustain; Myth = minions + tactical counter-play (shatter, dispel, stuns, pierce);
    /// Balance = support (universal blades/traps, tri-blades). Every school's levels follow one format. Taught by the
    /// professor once you reach the level: L1 starter, L5 minor attack or utility, L10 shield, L16 3-pip attack, L22 mid-tier
    /// attack or manipulation, L26 blade, L33 advanced single-target attack, L42 big hit or unique utility. Earned from the
    /// professor's school quests: L7 trap, L12 school bubble (+25%), L18 low-rank minion, L28 mid-tier minion, L38 the trial
    /// spell, L48 the signature 7-pip spell. (Dustwind's school side quests still teach each L42 spell early.)
    /// </remarks>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CardDatabase : UdonSharpBehaviour
    {
        public const int GENERAL = -1;
        /// <summary>
        /// The Tech School, The Dominion's own (School.Tech): machine guns, hacking, cyber attacks and armour-piercing shots.
        /// Its gimmick: barrages (many small hits, each stripping a shield, all boosted by one blade), hacks (stealing a
        /// target's blades and shields), drones and turrets that fire bursts, and the Neon Grid. Hidden: nobody teaches it
        /// yet, and the book's Deck page only shows its tab once you know one of its spells. Code 8 (7 is the healing-charm code).
        /// </summary>
        public const int TECH = 8;

        public const int EFFECT_DAMAGE = 0;
        public const int EFFECT_HEAL = 1;
        /// <summary>Shield: blocks power% of the next matching spell that hits the target (see <see cref="ShieldSchool"/>).</summary>
        public const int EFFECT_WARD = 2;
        /// <summary>Blade: the target's next matching attack deals +power% (see <see cref="BladeSchool"/>).</summary>
        public const int EFFECT_EMPOWER = 3;
        public const int EFFECT_DRAIN = 4;
        public const int EFFECT_FOCUS = 5;
        /// <summary>Trap, cast on an enemy: the next spell of the card's school (any school for General) that hits them deals +power%.</summary>
        public const int EFFECT_TRAP = 6;
        /// <summary>Three blades at once, one per school starting at <see cref="TriBladeFirstSchool"/>.</summary>
        public const int EFFECT_TRIBLADE = 7;
        /// <summary>Burn: power damage now, then aux more split over <see cref="DOT_ROUNDS"/> rounds.</summary>
        public const int EFFECT_DOT = 8;
        /// <summary>Deals all of the target's remaining burn damage at once, +power%.</summary>
        public const int EFFECT_DETONATE = 9;
        /// <summary>Taunt: the caster gets a power% any-school shield and monsters must attack them for aux rounds.</summary>
        public const int EFFECT_TAUNT = 10;
        /// <summary>Damage, and every target hit skips its next cast.</summary>
        public const int EFFECT_STUN = 11;
        /// <summary>Removes every shield on the target, then deals damage.</summary>
        public const int EFFECT_SHATTER = 12;
        /// <summary>Summons a minion with power health into a free seat on the caster's side; it acts with aux power each round.</summary>
        public const int EFFECT_MINION = 13;
        /// <summary>Removes every shield and blade on the target.</summary>
        public const int EFFECT_DISPEL = 14;
        /// <summary>Three shields at once (power% each), one per school starting at <see cref="TriBladeFirstSchool"/>.</summary>
        public const int EFFECT_TRISHIELD = 15;
        /// <summary>Three traps on an enemy at once (power% each), one per school starting at <see cref="TriBladeFirstSchool"/>.</summary>
        public const int EFFECT_TRITRAP = 16;
        /// <summary>aux shields of power% on one ally (each blocks a separate hit; school per <see cref="ShieldSchool"/>).</summary>
        public const int EFFECT_MULTIWARD = 17;
        /// <summary>
        /// Damage (power..powerMax), then an extra: aux = kind * 1000 + percent; kind 0 = a shield (any school) on the caster,
        /// 1 = a blade of the spell's school on the caster, 2 = a trap of the spell's school on each target hit.
        /// </summary>
        public const int EFFECT_STRIKEFX = 18;
        /// <summary>A power% trap (any school) on the enemy, and an aux% trap (any school) on the caster too.</summary>
        public const int EFFECT_CURSE = 19;
        /// <summary>Healing blade: the ally's next heal heals power% more.</summary>
        public const int EFFECT_HEALBLADE = 20;
        /// <summary>Heals the caster only, for power.</summary>
        public const int EFFECT_SELFHEAL = 21;
        /// <summary>
        /// Tech's barrage (machine guns and the like): aux hits of power..powerMax each on every target. Each bullet is its
        /// own hit, so each one uses up a shield (barrages tear through stacked shields), and the cast's blade boosts them all.
        /// </summary>
        public const int EFFECT_BARRAGE = 22;
        /// <summary>
        /// Tech's hack: power..powerMax damage (if any), then the target's blades are stolen onto the caster; aux 1 steals
        /// its shields too.
        /// </summary>
        public const int EFFECT_HACK = 23;
        /// <summary>A bubble over the whole duel: every spell of the card's school deals power% more (everyone's) for aux rounds. One at a time.</summary>
        public const int EFFECT_FIELD = 24;

        /// <summary>The school code a healing blade carries in the blade list (only heals use it).</summary>
        public const int HEAL_SCHOOL = 7;

        public const int TIERS = 12;
        /// <summary>Tier learned from the school's trial quest instead of a professor.</summary>
        public const int QUEST_TIER = 9;
        public const int DOT_ROUNDS = 3;

        /// <summary>Which school a shield/blade/trap applies to: -1 = every school.</summary>
        public const int ANY_SCHOOL = -1;
        private const int ICE = 1;

        /// <summary>Max cards per deck and max copies of one card per deck.</summary>
        public const int DECK_MAX = 20;
        public const int MAX_COPIES = 4;

        // One row per school (tiers 0-11, id 100 + school * 12 + tier), then the general spells, the extras, the Tech School
        // and each school's two newer spells (ids 210-223). Every school follows the same 14-spell format:
        //   Professor (taught free once you reach the level): L1 starter, L5 minor attack or utility, L10 shield,
        //     L16 3-pip attack, L22 mid-tier attack or manipulation, L26 blade, L33 advanced attack, L42 big hit or utility.
        //   School quests (your professor sends you to prove yourself): L7 trap, L12 school bubble, L18 low-rank minion,
        //     L28 mid-tier minion, L38 the trial spell (tier 9), L48 the signature 7-pip spell.
        // Tiers by level: 0 L1, 1 L5, 5 L10, 4 L16, 6 L22, 2 L26, 10 L33, 8 L42 (professor); 3 L7, 7 L28, 9 L38, 11 L48
        // (quest), and ids 210 + school * 2 (L12 bubble) and 211 + school * 2 (L18 minion; Myth's is Clay Golemling, tier 4,
        // and 221 is Myth's L10 shield). Ids never change: they're in every save.
        [HideInInspector] public int[] ids =
        {
            100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111,
            112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123,
            124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135,
            136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147,
            148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159,
            160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171,
            172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183,
            200, 201, 202, 203,
            184, 185, 186, 187, 188, 204,
            300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313,
            210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223,
            230, 231, 232, 233, 234, 235, 236,
        };
        [HideInInspector] public int[] schools =
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
            5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
            -1, -1, -1, -1,
            6, 6, 6, 6, 3, -1,
            8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
            0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6,
            0, 1, 2, 3, 4, 5, 6,
        };
        [HideInInspector] public int[] levelReqs =
        {
            1, 5, 26, 7, 16, 10, 22, 28, 42, 38, 33, 48,
            1, 5, 26, 7, 16, 10, 22, 28, 42, 38, 33, 48,
            1, 5, 26, 7, 16, 10, 22, 28, 42, 38, 33, 48,
            1, 5, 26, 7, 16, 10, 22, 28, 42, 38, 33, 48,
            1, 5, 26, 7, 16, 10, 22, 28, 42, 38, 33, 48,
            1, 5, 26, 7, 18, 16, 22, 28, 42, 38, 33, 48,
            1, 5, 26, 7, 16, 10, 22, 28, 42, 38, 33, 48,
            1, 2, 3, 4,
            24, 30, 18, 20, 6, 5,
            1, 5, 10, 16, 22, 26, 33, 42, 7, 12, 18, 28, 38, 48,
            12, 18, 12, 18, 12, 18, 12, 18, 12, 18, 12, 10, 12, 18,
            1, 1, 1, 1, 1, 1, 1,
        };
        [HideInInspector] public int[] pipCosts =
        {
            1, 2, 0, 0, 3, 0, 4, 4, 1, 3, 5, 7,
            1, 1, 0, 0, 3, 0, 4, 4, 2, 3, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 2, 0, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 3, 3, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 1, 4, 5, 7,
            1, 2, 0, 0, 3, 3, 4, 4, 2, 3, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 1, 1, 5, 7,
            0, 1, 0, 0,
            1, 1, 1, 1, 0, 2,
            1, 2, 0, 0, 2, 0, 4, 5, 2, 1, 3, 4, 3, 7,
            1, 3, 1, 3, 1, 3, 1, 3, 1, 3, 1, 0, 1, 3,
            6, 6, 6, 5, 6, 5, 6,
        };
        [HideInInspector] public int[] accuracies =
        {
            75, 75, 100, 100, 75, 100, 75, 100, 75, 75, 75, 75,
            80, 100, 100, 100, 80, 100, 80, 100, 100, 85, 80, 80,
            70, 70, 100, 100, 70, 100, 70, 100, 75, 100, 70, 70,
            90, 90, 100, 100, 90, 100, 90, 100, 90, 90, 90, 90,
            85, 85, 100, 100, 85, 100, 85, 100, 100, 85, 85, 85,
            80, 80, 100, 100, 100, 80, 80, 100, 100, 80, 80, 80,
            85, 85, 100, 100, 85, 100, 85, 100, 100, 100, 85, 85,
            100, 100, 100, 100,
            100, 100, 100, 100, 100, 100,
            85, 85, 100, 100, 85, 100, 85, 85, 100, 100, 100, 100, 85, 85,
            100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
            75, 80, 70, 90, 85, 100, 85,
        };
        [HideInInspector] public int[] powers =
        {
            65, 0, 35, 30, 50, 80, 145, 400, 40, 300, 290, 335,
            50, 25, 35, 30, 120, 30, 210, 700, 40, 260, 350, 385,
            90, 175, 35, 30, 210, 80, 385, 300, 190, 70, 575, 625,
            55, 250, 35, 30, 150, 80, 225, 500, 320, 900, 370, 415,
            55, 110, 35, 30, 120, 80, 240, 450, 70, 400, 385, 415,
            70, 130, 35, 30, 300, 170, 225, 650, 0, 95, 400, 480,
            65, 130, 25, 25, 160, 80, 265, 500, 35, 35, 415, 450,
            50, 100, 15, 15,
            50, 50, 25, 25, 35, 420,
            22, 80, 45, 35, 60, 40, 360, 70, 220, 25, 380, 650, 120, 110,
            25, 260, 25, 420, 25, 200, 25, 330, 25, 300, 25, 80, 25, 320,
            260, 400, 720, 700, 560, 1100, 430,
        };
        [HideInInspector] public int[] effects =
        {
            0, 8, 3, 6, 8, 2, 8, 13, 9, 18, 8, 8,
            0, 10, 3, 6, 0, 2, 0, 13, 17, 18, 0, 0,
            0, 0, 3, 6, 0, 2, 0, 13, 18, 3, 0, 0,
            0, 1, 3, 6, 1, 2, 0, 13, 1, 1, 0, 0,
            4, 4, 3, 6, 4, 2, 4, 13, 19, 4, 4, 4,
            0, 12, 3, 6, 13, 0, 11, 13, 14, 11, 11, 0,
            0, 0, 3, 6, 0, 2, 0, 13, 7, 7, 0, 0,
            2, 1, 3, 5,
            15, 15, 16, 16, 20, 21,
            EFFECT_BARRAGE, EFFECT_STUN, EFFECT_WARD, EFFECT_TRAP, EFFECT_HACK, EFFECT_EMPOWER, EFFECT_DAMAGE, EFFECT_BARRAGE, EFFECT_MINION, EFFECT_FIELD, EFFECT_MINION, EFFECT_MINION, EFFECT_HACK, EFFECT_BARRAGE,
            EFFECT_FIELD, EFFECT_MINION, EFFECT_FIELD, EFFECT_MINION, EFFECT_FIELD, EFFECT_MINION, EFFECT_FIELD, EFFECT_MINION, EFFECT_FIELD, EFFECT_MINION, EFFECT_FIELD, EFFECT_WARD, EFFECT_FIELD, EFFECT_MINION,
            EFFECT_DOT, EFFECT_DAMAGE, EFFECT_DAMAGE, EFFECT_HEAL, EFFECT_DRAIN, EFFECT_MINION, EFFECT_DAMAGE,
        };
        [HideInInspector] public int[] aoe =
        {
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 1, 0, 0, 1,
        };
        [HideInInspector] public string[] names =
        {
            "Ember Lance", "Smoldering Brand", "Ember Edge", "Cinder Snare", "Cinder Rain", "Ember Aegis", "Pyre Hawk", "Magma Hound", "Stoke the Embers", "Wildfire Oath", "Molten Behemoth", "Sunfire Wyrm",
            "Frost Shard", "Rime Challenge", "Rime Edge", "Frost Snare", "Hoarfrost Gale", "Frostward", "Frost Wyvern", "Rime Sentinel", "Permafrost Bulwark", "Winter's Bastion", "Frozen Colossus", "Winter's Wrath",
            "Spark Lash", "Thunder Hare", "Static Charge", "Thunder Snare", "Thunderclap", "Storm Aegis", "Skyfall Serpent", "Thunder Golem", "Gale Mark", "Overcharge", "Skyfall Leviathan", "Maelstrom Eye",
            "Thorn Volley", "Verdant Bloom", "Thorn Edge", "Bramble Snare", "Rejuvenate", "Verdant Aegis", "Grove Stag", "Treant Warden", "Wellspring", "Bloom of Renewal", "Wildwood Stampede", "Verdant Titan",
            "Shade Siphon", "Grave Ghast", "Grave Edge", "Hex Snare", "Soul Swarm", "Veil of Dread", "Night Leech", "Bone Thrall", "Dread Mark", "Dark Tithe", "Bone Dragon", "Requiem of Ash",
            "Mirage Dart", "Mirage Lancer", "Fable Edge", "Mirage Snare", "Clay Golemling", "Chimera's Roar", "Stone Basilisk", "Stone Colossus", "Sundering Gaze", "Riddle of Iron", "Fabled Hydra", "Titan of Legends",
            "Prism Ray", "Sand Djinn", "Accord Edge", "Sand Snare", "Sandstorm", "Balance Aegis", "Sphinx of Scales", "Sand Golem", "Elemental Tri-Blade", "Spirit Tri-Blade", "Sandstorm Sphinx", "Grand Convergence",
            "Tower Ward", "Mend", "Empower", "Steady Mind",
            "Elemental Tri-Shield", "Spirit Tri-Shield", "Elemental Tri-Trap", "Spirit Tri-Trap", "Wellspring Charm", "Second Wind",
            "Sidearm Burst", "Taser Dart", "Kevlar Plating", "Proximity Mine", "Firewall Breach", "Overclock", "Railgun", "Gatling Storm", "Scout Drone", "Neon Grid", "Sentry Turret", "Assault Mech", "Zero-Day Exploit", "Orbital Strike",
            "Kindled Sky", "Cinder Whelp", "Glacial Hush", "Rime Pup", "Thunderhead", "Spark Sprite", "Verdant Canopy", "Sproutling", "Gloaming", "Bone Scuttler", "Twice-Told Tale", "Fable Aegis", "Equinox", "Dune Scarab",
            "Phoenix Ascendant", "Aurora Glacier", "Skybreaker Roc", "Celestial Bloom", "Eclipse Wraith", "Starforged Golem", "Astral Scales",
        };
        [HideInInspector] public int[] prereqs =
        {
            0, 100, 106, 0, 105, 101, 104, 0, 110, 0, 102, 0,
            0, 112, 118, 0, 117, 113, 116, 0, 122, 0, 114, 0,
            0, 124, 130, 0, 129, 125, 128, 0, 134, 0, 126, 0,
            0, 136, 142, 0, 141, 137, 140, 0, 146, 0, 138, 0,
            0, 148, 154, 0, 153, 149, 152, 0, 158, 0, 150, 0,
            0, 160, 166, 0, 0, 221, 165, 0, 170, 0, 162, 0,
            0, 172, 178, 0, 177, 173, 176, 0, 182, 0, 174, 0,
            0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 300, 301, 302, 303, 304, 305, 306, 300, 308, 309, 310, 311, 312,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 161, 0, 0,
            0, 0, 0, 0, 0, 0, 0,
        };
        [HideInInspector] public int[] aux =
        {
            0, 240, 0, 0, 150, 0, 240, 90, 0, 1040, 300, 300,
            0, 2, 0, 0, 0, 0, 0, 60, 2, 50, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 150, 2040, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 120, 0, 0, 0, 0,
            50, 50, 0, 0, 30, 0, 50, 90, 30, 100, 50, 30,
            10, 0, 0, 0, 80, 0, 20, 130, 0, 15, 30, 20,
            0, 0, 1, 1, 0, 0, 0, 100, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            3, 0, 1, 0, 1, 0, 30, 8, 60, 4, 90, 140, 1, 5,
            4, 100, 4, 55, 4, 125, 4, 75, 4, 80, 4, 0, 4, 75,
            600, 0, 0, 0, 60, 200, 0,
        };
        [HideInInspector] public int[] powerMax =
        {
            95, 0, 0, 0, 70, 0, 215, 0, 0, 380, 430, 505,
            80, 0, 0, 0, 180, 0, 310, 0, 0, 340, 530, 575,
            130, 265, 0, 0, 310, 0, 575, 0, 260, 0, 865, 935,
            85, 0, 0, 0, 0, 0, 335, 0, 0, 0, 550, 625,
            85, 170, 0, 0, 180, 0, 360, 0, 0, 600, 575, 625,
            100, 190, 0, 0, 0, 230, 335, 0, 0, 145, 600, 720,
            95, 200, 0, 0, 240, 0, 395, 0, 0, 0, 625, 670,
            0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            34, 120, 0, 0, 90, 0, 520, 95, 0, 0, 0, 0, 170, 150,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            360, 560, 980, 0, 760, 0, 610,
        };

        // Tri-blades, tri-shields and tri-traps: Elemental = Fire, Ice, Storm; Spirit = Life, Death, Myth.
        [HideInInspector] public int[] triBladeIds = { 180, 181, 184, 185, 186, 187 };
        [HideInInspector] public int[] triBladeFirstSchools = { 0, 3, 0, 3, 0, 3 };
        /// <summary>School spells outside the 12-tier trees (ids 184+): not taught in order, no prerequisites.</summary>
        public const int FIRST_EXTRA_ID = 184;
        /// <summary>
        /// Spells only ever earned from a quest (besides each school's trial spell, tier 9): each school's L7 trap, L12
        /// bubble, L18 minion, L28 minion and L48 signature spell (its professor's school quests), Life's Wellspring Charm,
        /// then the Tech School's quest spells (for when Tech is taught, later in the game). Professors never list them.
        /// </summary>
        [HideInInspector] public int[] questOnlyIds =
        {
            103, 210, 211, 107, 111,
            115, 212, 213, 119, 123,
            127, 214, 215, 131, 135,
            139, 216, 217, 143, 147,
            151, 218, 219, 155, 159,
            163, 220, 164, 167, 171,
            175, 222, 223, 179, 183,
            188,
            308, 309, 310, 311, 312, 313,
            230, 231, 232, 233, 234, 235, 236,
        };
        /// <summary>
        /// The Starshard spells (ids 230-236, one per school): never taught, never sold as treasure cards; a Starshard card
        /// pack teaches one, ludicrously rarely (CardPacks.slotSpecial). Once learned they're yours to put in any deck.
        /// </summary>
        [HideInInspector] public int[] starshardSpellIds = { 230, 231, 232, 233, 234, 235, 236 };

        public bool IsStarshardSpell(int cardId)
        {
            for (int i = 0; i < starshardSpellIds.Length; i++) if (starshardSpellIds[i] == cardId) return true;
            return false;
        }
        /// <summary>Per card (by index): 1 = quest-only, built from questOnlyIds and the trial tier on first use.</summary>
        private int[] _questFlags;

        // Index 7 is the healing-charm code (no school); 8 is Tech.
        [HideInInspector] public string[] schoolNames = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance", "General", "Tech" };
        [HideInInspector] public Color[] schoolColors =
        {
            new Color(0.85f, 0.3f, 0.15f), new Color(0.45f, 0.7f, 0.95f), new Color(0.55f, 0.35f, 0.85f),
            new Color(0.35f, 0.75f, 0.35f), new Color(0.3f, 0.3f, 0.3f), new Color(0.85f, 0.7f, 0.2f),
            new Color(0.75f, 0.6f, 0.4f), new Color(0.6f, 0.6f, 0.65f), new Color(0.2f, 0.85f, 0.8f),
        };
        /// <summary>
        /// Optional cast cinematic per card (same index as <see cref="ids"/>; null = default spell orb). The scene builder fills
        /// this from prefabs in Assets/WizardsVRC/SpellAnimations named "&lt;id&gt;.prefab" or "&lt;Spell Name&gt;.prefab".
        /// </summary>
        public GameObject[] spellAnimations;
        [Tooltip("How each spell's animation plays (0 none, 1 a creature charges, 2 a bolt flies, 3 an effect on the target, 4 a hand-made prefab).")]
        [HideInInspector] public int[] animModes;
        [Tooltip("Seconds into the animation when the spell's effect lands.")]
        [HideInInspector] public float[] animStrikes;
        [Tooltip("How long the animation lasts.")]
        [HideInInspector] public float[] animSeconds;
        [Tooltip("A creature spell's body: 0 biped, 1 four-legged, 2 flying, 3 plant, 4 other (-1 = no creature). How it attacks.")]
        [HideInInspector] public int[] animKinds;
        [Tooltip("Each spell's card art (rendered from its animation by the builder).")]
        public Texture[] cardArt;
        /// <summary>
        /// Every attack spell's own choreography (SpellCinematic), packed: delivery (bits 0-3, CINE_*), impact (4-7,
        /// IMPACT_*), wind-up (8-11, CHARGE_*), film camera (12-15, CAM_*), bolt count (16-19, 0 = the delivery's own),
        /// bolt shape (20-21: orb, shard, disc), mirrored (22) and arc height (23-24). 0 = not an attack spell (it keeps its
        /// plain animation). The scene builder fills it (WizardsVRCSceneBuilder.SpellStyles.cs) and makes sure no two
        /// attack spells share a delivery and impact.
        /// </summary>
        [HideInInspector] public int[] cineStyles;

        // How an attack spell travels from its caster to its target (a summoned creature rides the same path).
        public const int CINE_LANCE = 1, CINE_HELIX = 2, CINE_VOLLEY = 3, CINE_METEOR = 4, CINE_RAIN = 5, CINE_GROUND = 6,
                         CINE_BEAM = 7, CINE_ORBIT = 8, CINE_ZIGZAG = 9, CINE_PORTAL = 10, CINE_WAVE = 11, CINE_BOOMERANG = 12;
        // How it lands.
        public const int IMPACT_BURST = 0, IMPACT_IMPLODE = 1, IMPACT_PILLAR = 2, IMPACT_RINGS = 3, IMPACT_SPIKES = 4,
                         IMPACT_VORTEX = 5, IMPACT_NOVA = 6, IMPACT_SHATTER = 7, IMPACT_SIGIL = 8;
        // The flourish round the caster through the wind-up.
        public const int CHARGE_STREAM = 0, CHARGE_GYRO = 1, CHARGE_FOUNTAIN = 2, CHARGE_SPIRAL = 3, CHARGE_COLUMN = 4;
        // How the film shoots it (a wizard's cast).
        public const int CAM_STANDARD = 0, CAM_LOW = 1, CAM_CRANE = 2, CAM_FOLLOW = 3, CAM_REVERSE = 4;

        // Per delivery (index = CINE_*): seconds into the release when it hits, and how long the release lasts.
        private float[] _cineStrikes = { 1.4f, 0.8f, 1.3f, 1.4f, 2.2f, 1.9f, 1.5f, 0.7f, 2.0f, 0.85f, 1.7f, 1.6f, 1.75f };
        private float[] _cineReleases = { 2.0f, 1.4f, 1.9f, 2.0f, 2.8f, 2.7f, 2.1f, 2.1f, 2.6f, 1.4f, 2.3f, 2.2f, 2.35f };

        private Color _generalColor = new Color(0.6f, 0.6f, 0.65f);

        public int Count() { return ids.Length; }

        public int IdAt(int index) { return ids[index]; }

        public int IndexOf(int cardId)
        {
            if (cardId <= 0) return -1;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == cardId) return i;
            return -1;
        }

        public string GetName(int cardId)
        {
            int i = IndexOf(cardId);
            return i < 0 ? "Unknown Spell" : names[i];
        }

        public int GetSchool(int cardId)
        {
            int i = IndexOf(cardId);
            return i < 0 ? GENERAL : schools[i];
        }

        public int GetLevelReq(int cardId)
        {
            int i = IndexOf(cardId);
            return i < 0 ? 1 : levelReqs[i];
        }

        public int GetPipCost(int cardId) { int i = IndexOf(cardId); return i < 0 ? 0 : pipCosts[i]; }
        public int GetAccuracy(int cardId) { int i = IndexOf(cardId); return i < 0 ? 0 : accuracies[i]; }
        public int GetPower(int cardId) { int i = IndexOf(cardId); return i < 0 ? 0 : powers[i]; }
        public int GetEffect(int cardId) { int i = IndexOf(cardId); return i < 0 ? EFFECT_DAMAGE : effects[i]; }
        public int AnimMode(int cardId) { int i = IndexOf(cardId); return i < 0 || animModes == null || i >= animModes.Length || GetAnimation(cardId) == null ? 0 : animModes[i]; }
        public int AnimKind(int cardId) { int i = IndexOf(cardId); return i < 0 || animKinds == null || i >= animKinds.Length ? -1 : animKinds[i]; }
        public float AnimStrike(int cardId) { int i = IndexOf(cardId); return i < 0 || animStrikes == null || i >= animStrikes.Length ? 0f : animStrikes[i]; }
        /// <summary>Seconds the spell's animation takes after its wind-up (0 = it has none).</summary>
        public float AnimSeconds(int cardId) { int i = IndexOf(cardId); return AnimMode(cardId) == 0 || animSeconds == null || i >= animSeconds.Length ? 0f : animSeconds[i]; }

        /// <summary>The attack spell's packed choreography (0 = none: not an attack spell). See <see cref="cineStyles"/>.</summary>
        public int CineStyle(int cardId) { int i = IndexOf(cardId); return i < 0 || cineStyles == null || i >= cineStyles.Length ? 0 : cineStyles[i]; }
        public int CineDelivery(int cardId) { return CineStyle(cardId) & 15; }

        /// <summary>Seconds into an attack spell's release when it hits (the same on every client).</summary>
        public float CineStrike(int cardId)
        {
            int d = CineDelivery(cardId);
            return d >= 0 && d < _cineStrikes.Length ? _cineStrikes[d] : 1f;
        }

        /// <summary>How long an attack spell's release lasts, from leaving the caster to settling on the target.</summary>
        public float CineRelease(int cardId)
        {
            int d = CineDelivery(cardId);
            return d >= 0 && d < _cineReleases.Length ? _cineReleases[d] : 1.4f;
        }

        /// <summary>A delivery's own flight: seconds from leaving the caster to hitting.</summary>
        public float DeliveryFlight(int delivery) { return delivery >= 0 && delivery < _cineStrikes.Length ? _cineStrikes[delivery] : 1f; }

        /// <summary>How long a delivery carries on after it hits (a beam holds, the rest of a rain falls).</summary>
        public float DeliveryTail(int delivery)
        {
            return delivery >= 0 && delivery < _cineStrikes.Length && delivery < _cineReleases.Length ? Mathf.Max(0.3f, _cineReleases[delivery] - _cineStrikes[delivery]) : 0.6f;
        }

        // ------------------------------------------------------------------ the staging formula (SPELL_ANIMATIONS.md)
        //
        // Every staged spell (attacks, heals, the dispel) goes through six beats, like Wizard101's:
        //   cast      the caster goes to the middle of the ring and charges (a short gather, then the school's cast sound)
        //   arrival   the release: the power arrives, the spell's own way (a core over the caster, or its creature)
        //   display   it shows off, the spell's own way
        //   (expend   each blade, trap and shield the cast uses goes, one at a time: BattleClient)
        //   attack    the spell's own delivery flies; a spell of 5 pips or more strikes twice
        //   result    the hit lands, the spell's own kind of impact, and the numbers
        //   exit      the power leaves, the spell's own way, and the caster goes back to their seat
        // The whole turn lasts 9 s at 1 pip up to 12 s at 7 (BattleManager), plus the expending (2 s at most): never more
        // than 14 s. What's left after going to the middle, the cast and going back is the 'unleash', shared out below, so
        // every client lays a spell out the same from that one number.

        /// <summary>A staged spell's power: its pip cost, 1 to 7.</summary>
        public int CinePower(int cardId) { return Mathf.Clamp(GetPipCost(cardId), 1, 7); }

        /// <summary>The arrival's share of the unleash.</summary>
        public float PlanArrival(float unleash) { return unleash * 0.16f; }
        /// <summary>The display's share.</summary>
        public float PlanDisplay(float unleash) { return unleash * 0.16f; }
        /// <summary>The attack's share: from the first bolt leaving to the hit.</summary>
        public float PlanAttack(float unleash) { return unleash * 0.40f; }
        /// <summary>The result's share: the hit and the hold on it. The exit gets the rest (11%).</summary>
        public float PlanResult(float unleash) { return unleash * 0.17f; }
        /// <summary>Waves before the finisher: one for a spell of 5 pips or more.</summary>
        public int PlanWaves(int pips) { return pips >= 5 ? 1 : 0; }
        /// <summary>Seconds from the release to the hit, when the spell's results land ('expend' = how long its blades, traps and shields take to go).</summary>
        public float PlanStrike(float unleash, float expend) { return PlanArrival(unleash) + PlanDisplay(unleash) + Mathf.Max(0f, expend) + PlanAttack(unleash); }

        /// <summary>
        /// Each staged spell's other beats, packed (same index as <see cref="ids"/>): how its power arrives (bits 0-3,
        /// ARRIVE_*), how it shows off (4-7, SHOW_*), how it leaves (8-11, EXIT_*), and bit 12 set for a spell that
        /// helps its target (a heal): no flinch, no shake, a gentle landing. Filled by WizardsVRCSceneBuilder.SpellStyles.cs.
        /// </summary>
        [HideInInspector] public int[] cineBeats;
        // How the power arrives: up out of the rune, down out of the sky, drawn in from all round, through a gate, wound up out of sparks.
        public const int ARRIVE_ERUPT = 0, ARRIVE_DESCEND = 1, ARRIVE_CONVERGE = 2, ARRIVE_RIFT = 3, ARRIVE_KINDLE = 4;
        // How it shows off: pulses throwing rings, rings spinning up round it, a crown of bolts, crackling at the floor, a column through it.
        public const int SHOW_FLARE = 0, SHOW_HALO = 1, SHOW_CROWN = 2, SHOW_STORM = 3, SHOW_ASCEND = 4;
        // How it leaves: embers drifting up, blown apart, a column of sparks into the sky, sinking into the floor, a last pair of rings.
        public const int EXIT_FADE = 0, EXIT_SCATTER = 1, EXIT_ASCEND = 2, EXIT_SINK = 3, EXIT_ECHO = 4;
        public const int BEATS_FRIENDLY = 4096;

        public int CineBeats(int cardId) { int i = IndexOf(cardId); return i < 0 || cineBeats == null || i >= cineBeats.Length ? 0 : cineBeats[i]; }

        /// <summary>
        /// A spell that only places a charm (a blade), a shield or a trap: it has no animation at all. The effect just
        /// appears round its target with a sound (BattleClient).
        /// </summary>
        public bool IsCharmSpell(int cardId)
        {
            int e = GetEffect(cardId);
            return e == EFFECT_WARD || e == EFFECT_EMPOWER || e == EFFECT_TRAP || e == EFFECT_TRIBLADE || e == EFFECT_TAUNT || e == EFFECT_TRISHIELD ||
                   e == EFFECT_TRITRAP || e == EFFECT_MULTIWARD || e == EFFECT_CURSE || e == EFFECT_HEALBLADE || e == EFFECT_FOCUS;
        }

        public GameObject GetAnimation(int cardId)
        {
            int i = IndexOf(cardId);
            return i < 0 || spellAnimations == null || i >= spellAnimations.Length ? null : spellAnimations[i];
        }

        public int GetPrereq(int cardId) { int i = IndexOf(cardId); return i < 0 ? 0 : prereqs[i]; }
        public int GetPowerMax(int cardId) { int i = IndexOf(cardId); return i < 0 ? 0 : powerMax[i]; }

        /// <summary>A random amount between the card's min and max power (inclusive), or its fixed power.</summary>
        public int RollPower(int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return 0;
            return powerMax[i] > powers[i] ? Random.Range(powers[i], powerMax[i] + 1) : powers[i];
        }

        public int GetAux(int cardId) { int i = IndexOf(cardId); return i < 0 ? 0 : aux[i]; }

        [Header("Burns (damage over time)")]
        [Tooltip("Every burn spell's damage over time, as a percentage of the amount it lists (before blades and the caster's damage bonus).")]
        public int dotBoostPct = 150;
        [Tooltip("A burn spell without a hit of its own still hits for this percentage of its (boosted) burn when it lands.")]
        public int dotInitialPct = 25;

        /// <summary>A burn spell's burn over its rounds (boosted), before blades and the caster's bonus.</summary>
        public int DotTotal(int cardId)
        {
            int i = IndexOf(cardId);
            return i < 0 ? 0 : Mathf.Max(0, aux[i] * dotBoostPct / 100);
        }

        /// <summary>A burn spell's hit when it lands: its own when it has one, else a share of its burn (at least 1).</summary>
        public int DotHit(int cardId, bool roll)
        {
            int i = IndexOf(cardId);
            if (i < 0) return 0;
            if (powers[i] > 0) return roll ? RollPower(cardId) : powers[i];
            return Mathf.Max(1, DotTotal(cardId) * dotInitialPct / 100);
        }
        public int BasicSpell(int school) { return 100 + school * TIERS; }
        public int TierOf(int cardId) { return GetSchool(cardId) == GENERAL || cardId >= FIRST_EXTRA_ID ? -1 : (cardId - 100) % TIERS; }

        /// <summary>Learned from a quest rather than a professor: each school's six school-quest spells (the trial's among them), and a few side-quest spells.</summary>
        public bool IsQuestSpell(int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return false;
            if (_questFlags == null || _questFlags.Length != ids.Length)
            {
                _questFlags = new int[ids.Length];
                for (int k = 0; k < ids.Length; k++)
                {
                    if (schools[k] == GENERAL) continue;
                    if (TierOf(ids[k]) == QUEST_TIER) { _questFlags[k] = 1; continue; }
                    for (int q = 0; q < questOnlyIds.Length; q++) if (questOnlyIds[q] == ids[k]) { _questFlags[k] = 1; break; }
                }
            }
            return _questFlags[i] != 0;
        }

        /// <summary>The trial spell (tier 9) rather than a side-quest one.</summary>
        public bool IsTrialSpell(int cardId) { return GetSchool(cardId) != GENERAL && TierOf(cardId) == QUEST_TIER; }

        /// <summary>
        /// A school's blades and traps (and Life's healing charm) are only for wizards of that school: other schools can't
        /// learn them with training points.
        /// </summary>
        public bool SchoolOnly(int cardId)
        {
            int e = GetEffect(cardId);
            return GetSchool(cardId) != GENERAL && (e == EFFECT_EMPOWER || e == EFFECT_TRAP || e == EFFECT_TRIBLADE || e == EFFECT_TRITRAP ||
                                                    e == EFFECT_CURSE || e == EFFECT_HEALBLADE);
        }

        /// <summary>This character may learn the spell at all (school-only spells need the same school).</summary>
        public bool SchoolAllows(CharacterData c, int cardId)
        {
            return c != null && (!SchoolOnly(cardId) || GetSchool(cardId) == (int)c.school);
        }

        /// <summary>
        /// The spell that must be learned first, for this character: a school-only spell they can't learn is skipped over
        /// (another school's wizard goes from the tier before it straight to the tier after).
        /// </summary>
        public int PrereqFor(CharacterData c, int cardId)
        {
            int pre = GetPrereq(cardId);
            int guard = 0;
            while (pre > 0 && !SchoolAllows(c, pre) && guard++ < TIERS) pre = GetPrereq(pre);
            return pre;
        }

        // ------------------------------------------------------------------ treasure cards

        /// <summary>Rarity names for treasure cards and pack pulls (by RarityOf), and their colours.</summary>
        [HideInInspector] public string[] rarityNames = { "Common", "Uncommon", "Rare", "Ultra-Rare", "Starshard" };
        [HideInInspector] public Color[] rarityColors =
        {
            new Color(0.82f, 0.82f, 0.82f), new Color(0.45f, 0.9f, 0.45f), new Color(0.4f, 0.65f, 1f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.45f, 1f),
        };
        public const int RARITY_STARSHARD = 4;

        /// <summary>A treasure card's rarity, from its pip cost: 0-1 Common, 2-3 Uncommon, 4-5 Rare, 6+ Ultra-Rare.</summary>
        public int RarityOf(int cardId)
        {
            int p = GetPipCost(cardId);
            return p >= 6 ? 3 : p >= 4 ? 2 : p >= 2 ? 1 : 0;
        }

        public string RarityName(int rarity) { return rarityNames != null && rarity >= 0 && rarity < rarityNames.Length ? rarityNames[rarity] : ""; }
        public Color RarityColor(int rarity) { return rarityColors != null && rarity >= 0 && rarity < rarityColors.Length ? rarityColors[rarity] : Color.white; }

        /// <summary>A treasure card's price in gold at the Library: the more pips it costs, the more it costs (25 + 15 per pip + 15 per pip squared).</summary>
        public int TreasurePrice(int cardId)
        {
            int p = GetPipCost(cardId);
            return 25 + 15 * p + 15 * p * p;
        }

        /// <summary>
        /// What a treasure card is: a single-use copy of the spell. Anyone can cast it (any school, any level, without
        /// knowing the spell); it lives in a deck's sideboard and is used up when cast, fizzled or discarded.
        /// </summary>
        public string TreasureNote() { return "Treasure card: cast it once, whatever your school or level. Goes in a deck's sideboard; used up when cast, fizzled or discarded."; }

        /// <summary>What a school's duel-wide boost (EFFECT_FIELD) is called: Tech's is a grid, the magic schools' a bubble.</summary>
        public string FieldWord(int cardId) { return GetSchool(cardId) == TECH ? "Grid" : "Bubble"; }

        /// <summary>Heals that target the caster only (no picking).</summary>
        public bool SelfOnly(int cardId) { int e = GetEffect(cardId); return e == EFFECT_SELFHEAL || e == EFFECT_FIELD; }

        /// <summary>A school players can't see or learn yet (Tech): the Deck page shows it only once you know one of its spells.</summary>
        public bool IsHiddenSchool(int school) { return school == TECH; }

        /// <summary>True if the character knows any spell of the school.</summary>
        public bool KnowsAnyOf(CharacterData c, int school)
        {
            for (int i = 0; i < ids.Length; i++) if (schools[i] == school && Knows(c, ids[i])) return true;
            return false;
        }
        public int QuestSpell(int school) { return 100 + school * TIERS + QUEST_TIER; }

        public bool IsAoe(int cardId) { int i = IndexOf(cardId); return i >= 0 && aoe[i] != 0; }

        /// <summary>True for heals and buffs (target your own side), false for attacks.</summary>
        public bool TargetsAllies(int cardId)
        {
            int e = GetEffect(cardId);
            return e == EFFECT_HEAL || e == EFFECT_WARD || e == EFFECT_EMPOWER || e == EFFECT_FOCUS || e == EFFECT_TRIBLADE ||
                   e == EFFECT_TAUNT || e == EFFECT_MINION || e == EFFECT_TRISHIELD || e == EFFECT_MULTIWARD || e == EFFECT_HEALBLADE ||
                   e == EFFECT_SELFHEAL || e == EFFECT_FIELD;
        }

        /// <summary>Ice shields and General shields block every school; other shields block their own school.</summary>
        public int ShieldSchool(int cardId)
        {
            int s = GetSchool(cardId);
            return s == ICE || GetAux(cardId) == 1 ? ANY_SCHOOL : s;
        }

        /// <summary>School blades boost that school's attacks; General blades boost any attack.</summary>
        public int BladeSchool(int cardId) { return GetAux(cardId) == 1 ? ANY_SCHOOL : GetSchool(cardId); }

        public int TrapSchool(int cardId) { return GetAux(cardId) == 1 ? ANY_SCHOOL : GetSchool(cardId); }

        public int TriBladeFirstSchool(int cardId)
        {
            for (int i = 0; i < triBladeIds.Length; i++)
                if (triBladeIds[i] == cardId) return triBladeFirstSchools[i];
            return 0;
        }

        private string SchoolPhrase(int school, string anyText)
        {
            return school == ANY_SCHOOL ? anyText : SchoolName(school);
        }

        public string SchoolName(int school)
        {
            return school >= 0 && school < schoolNames.Length ? schoolNames[school] : "General";
        }

        // School icons: glyphs U+E000-U+E006 in the combat pixel font (Fonts/PsxNameFont.txt -> PsxPixel.ttf), coloured.
        private string[] _schoolIconGlyphs = { "\uE000", "\uE001", "\uE002", "\uE003", "\uE004", "\uE005", "\uE006" };
        private string[] _schoolIconHex = { "#F25926", "#8CD9FF", "#A666F2", "#66E666", "#B3A6C7", "#F2CC40", "#D99966" };
        // The Tech School's icon: a cog (U+E007 in the pixel font), in electric teal.
        private const string TechIconGlyph = "\uE007";
        private const string TechIconHex = "#3CD7CD";
        private const string TechCardHex = "#1E9C94";

        /// <summary>The school's icon as rich text (its glyph in the school's colour), for labels in the combat font.</summary>
        public string SchoolIcon(int school)
        {
            if (school == TECH) return "<color=" + TechIconHex + ">" + TechIconGlyph + "</color>";
            if (school < 0 || school >= _schoolIconGlyphs.Length) return "";
            return "<color=" + _schoolIconHex[school] + ">" + _schoolIconGlyphs[school] + "</color>";
        }

        // ------------------------------------------------------------------ card faces (book, professors, new spell popup)

        [Header("Card art (filled by the builder)")]
        [Tooltip("Card frame per school (index School, 7 = General): the same art as the cards in your hand.")]
        public Sprite[] cardFrames;
        [Tooltip("Shield / blade / trap icons for the art window: index kind * 8 + school (7 = General / any school).")]
        public Texture[] effectIcons;

        /// <summary>Looks per school (card frames, effect icons, casting symbols, effect colours): the seven schools, General, Tech.</summary>
        public const int LOOKS = 9;

        /// <summary>Which look a school uses: 0-6 the schools, 7 General (and anything else), 8 the Tech School.</summary>
        public int LookIndex(int school) { return school >= 0 && school < 7 ? school : school == TECH ? 8 : 7; }

        /// <summary>The look for a school in an array of 'len' looks (an older array without Tech falls back to General).</summary>
        public int LookIn(int school, int len)
        {
            int i = LookIndex(school);
            return i < len ? i : Mathf.Max(0, Mathf.Min(7, len - 1));
        }

        /// <summary>Frame art for the card's school (null when there is none).</summary>
        public Sprite FrameFor(int cardId)
        {
            if (cardFrames == null || cardFrames.Length == 0) return null;
            return cardFrames[LookIn(GetSchool(cardId), cardFrames.Length)];
        }

        /// <summary>Art for the card's picture window: its painted picture; without one, a shield, blade or trap shows its icon.</summary>
        public Texture ArtFor(int cardId)
        {
            int ai = IndexOf(cardId);
            if (ai >= 0 && cardArt != null && ai < cardArt.Length && cardArt[ai] != null) return cardArt[ai];
            int e = GetEffect(cardId);
            int kind = -1, sc = GENERAL;
            if (e == EFFECT_WARD || e == EFFECT_MULTIWARD) { kind = 0; sc = ShieldSchool(cardId); }
            else if (e == EFFECT_TAUNT || e == EFFECT_TRISHIELD) { kind = 0; sc = ANY_SCHOOL; }
            else if (e == EFFECT_EMPOWER) { kind = 1; sc = BladeSchool(cardId); }
            else if (e == EFFECT_TRIBLADE) { kind = 1; sc = ANY_SCHOOL; }
            else if (e == EFFECT_HEALBLADE) { kind = 1; sc = 3; }
            else if (e == EFFECT_TRAP) { kind = 2; sc = TrapSchool(cardId); }
            else if (e == EFFECT_TRITRAP || e == EFFECT_CURSE) { kind = 2; sc = ANY_SCHOOL; }
            if (kind < 0 || effectIcons == null) return null;
            int i = kind * LOOKS + LookIndex(sc);
            return i < effectIcons.Length ? effectIcons[i] : null;
        }

        /// <summary>
        /// Draws a spell as a card, like the ones in your hand: school frame, name, pip cost, art, accuracy, effect and
        /// school. Any part may be null (small cards leave some out). Dim greys it out (can't use / learn it yet).
        /// </summary>
        public void FillCard(int cardId, Image frame, Text cardName, Text pips, RawImage art, Text accuracy, Text info, Text school, bool dim)
        {
            if (frame != null)
            {
                Sprite f = FrameFor(cardId);
                if (f != null)
                {
                    if (frame.sprite != f) frame.sprite = f;
                    frame.color = dim ? new Color(0.5f, 0.5f, 0.53f) : Color.white;
                }
                else frame.color = dim ? Color.Lerp(GetColor(cardId), new Color(0.35f, 0.35f, 0.35f), 0.7f) : GetColor(cardId);
            }
            if (cardName != null) cardName.text = GetName(cardId);
            if (pips != null) pips.text = GetPipCost(cardId).ToString();
            if (art != null)
            {
                Texture t = ArtFor(cardId);
                art.texture = t;
                // Card pictures fill the wide window; a square icon (a spell without a picture) sits in its middle.
                if (t != null) art.uvRect = t.width == t.height ? new Rect(-0.25f, 0f, 1.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
                art.color = dim ? new Color(0.5f, 0.5f, 0.52f) : Color.white;
                if (art.gameObject.activeSelf != (t != null)) art.gameObject.SetActive(t != null);
            }
            if (accuracy != null) accuracy.text = GetAccuracy(cardId).ToString() + "%";
            if (info != null) info.text = GetCardText(cardId);
            if (school != null) school.text = SchoolName(GetSchool(cardId));
        }

        public Color GetColor(int cardId)
        {
            int s = GetSchool(cardId);
            return s >= 0 && s < schoolColors.Length ? schoolColors[s] : _generalColor;
        }

        /// <summary>True if the character can use the card: general spells by level, school spells once learned from a professor.</summary>
        public bool Knows(CharacterData c, int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0 || c == null) return false;
            if (schools[i] == GENERAL) return c.level >= levelReqs[i];
            return c.HasLearned(cardId);
        }

        /// <summary>Training points needed to learn a school spell: free for the character's own school, 1 otherwise.</summary>
        public int LearnCost(CharacterData c, int cardId)
        {
            return c != null && GetSchool(cardId) == (int)c.school ? 0 : 1;
        }

        // ------------------------------------------------------------------ card text with icons
        // Glyphs U+E010-U+E01B in the combat pixel font (Fonts/PsxNameFont.txt): damage, shield, blade, trap, heal,
        // stun, over time, accuracy, minion, break, any school, pips. On the cards they're coloured dark enough to read
        // on the parchment; school icons there use deeper copies of the school colours for the same reason.
        public const int ICON_DAMAGE = 0;
        public const int ICON_SHIELD = 1;
        public const int ICON_BLADE = 2;
        public const int ICON_TRAP = 3;
        public const int ICON_HEAL = 4;
        public const int ICON_STUN = 5;
        public const int ICON_DOT = 6;
        public const int ICON_ACCURACY = 7;
        public const int ICON_MINION = 8;
        public const int ICON_BREAK = 9;
        public const int ICON_ANY = 10;
        public const int ICON_PIP = 11;
        private string[] _iconGlyphs = { "\uE010", "\uE011", "\uE012", "\uE013", "\uE014", "\uE015", "\uE016", "\uE017", "\uE018", "\uE019", "\uE01A", "\uE01B" };
        private string[] _iconHex = { "#B3261E", "#1F6FC2", "#C47A00", "#7B3BA8", "#2A8A3A", "#C99700", "#C2560F", "#3A4A5C", "#6B4A2E", "#5A5A5A", "#A07818", "#2D7FB0" };
        private string[] _cardSchoolHex = { "#D2401A", "#2585BF", "#7438C8", "#2E9638", "#5B4F72", "#B88A0A", "#9E6234" };

        /// <summary>An effect icon as rich text (its glyph in its card colour).</summary>
        public string Icon(int icon)
        {
            if (icon < 0 || icon >= _iconGlyphs.Length) return "";
            return "<color=" + _iconHex[icon] + ">" + _iconGlyphs[icon] + "</color>";
        }

        /// <summary>A school's icon for card text (deeper colour, for the parchment); any school shows a sparkle.</summary>
        public string CardSchoolIcon(int school)
        {
            if (school == TECH) return "<color=" + TechCardHex + ">" + TechIconGlyph + "</color>";
            if (school < 0 || school >= _schoolIconGlyphs.Length) return Icon(ICON_ANY);
            return "<color=" + _cardSchoolHex[school] + ">" + _schoolIconGlyphs[school] + "</color>";
        }

        private string Num(string n) { return "<b>" + n + "</b>"; }

        /// <summary>
        /// The short text on a card's face, Wizard101 style: numbers with school and effect icons, a few words at most,
        /// so it always fits the parchment (the details box keeps the full sentence, GetEffectText).
        /// </summary>
        public string GetCardText(int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return "";
            int p = powers[i];
            int x = aux[i];
            int sc = schools[i];
            string range = Num(powerMax[i] > p ? p.ToString() + "-" + powerMax[i].ToString() : p.ToString());
            string all = aoe[i] != 0 ? " all" : "";
            string hit = range + " " + CardSchoolIcon(sc) + Icon(ICON_DAMAGE) + all;
            string pierce = x > 0 ? "\nIgnores " + Num(x.ToString() + "%") + " " + Icon(ICON_SHIELD) : "";
            switch (effects[i])
            {
                case EFFECT_DAMAGE: return hit + pierce;
                case EFFECT_DOT:
                {
                    string first = p > 0 ? hit : Num(DotHit(cardId, false).ToString()) + " " + CardSchoolIcon(sc) + Icon(ICON_DAMAGE) + all;
                    return first + "\n+ " + Num(DotTotal(cardId).ToString()) + " " + CardSchoolIcon(sc) + Icon(ICON_DOT) + " over " + DOT_ROUNDS.ToString() + " rounds";
                }
                case EFFECT_DETONATE: return "Set off " + Icon(ICON_DOT) + " burns\n" + Num("+" + p.ToString() + "%") + " " + Icon(ICON_DAMAGE);
                case EFFECT_TAUNT:
                    return "Taunt " + Num(x.ToString()) + " rounds\n" + Num("-" + p.ToString() + "%") + " " + Icon(ICON_ANY) + Icon(ICON_SHIELD);
                case EFFECT_STUN: return hit + "\n" + Icon(ICON_STUN) + " Stun" + (x > 0 ? ", ignores " + Num(x.ToString() + "%") + " " + Icon(ICON_SHIELD) : "");
                case EFFECT_SHATTER: return Icon(ICON_BREAK) + " all " + Icon(ICON_SHIELD) + "\nthen " + hit;
                case EFFECT_DISPEL: return Icon(ICON_BREAK) + " all " + Icon(ICON_SHIELD) + Icon(ICON_BLADE) + (aoe[i] != 0 ? " on all" : "");
                case EFFECT_MINION:
                    return Icon(ICON_MINION) + " " + CardSchoolIcon(sc) + " minion\n" + Num(p.ToString()) + " " + Icon(ICON_HEAL) + "  " + Num(x.ToString()) + " " + Icon(ICON_DAMAGE);
                case EFFECT_HEAL: return Num(p.ToString()) + " " + Icon(ICON_HEAL) + (aoe[i] != 0 ? " all allies" : "");
                case EFFECT_SELFHEAL: return Num(p.ToString()) + " " + Icon(ICON_HEAL) + " self";
                case EFFECT_WARD: return Num("-" + p.ToString() + "%") + " " + CardSchoolIcon(ShieldSchool(cardId)) + Icon(ICON_SHIELD);
                case EFFECT_MULTIWARD:
                    return Num(x.ToString()) + " x " + Num("-" + p.ToString() + "%") + " " + CardSchoolIcon(ShieldSchool(cardId)) + Icon(ICON_SHIELD);
                case EFFECT_EMPOWER: return Num("+" + p.ToString() + "%") + " " + CardSchoolIcon(BladeSchool(cardId)) + Icon(ICON_BLADE);
                case EFFECT_TRAP: return Num("+" + p.ToString() + "%") + " " + CardSchoolIcon(TrapSchool(cardId)) + Icon(ICON_TRAP);
                case EFFECT_TRIBLADE:
                case EFFECT_TRISHIELD:
                case EFFECT_TRITRAP:
                {
                    int f = TriBladeFirstSchool(cardId);
                    int e = effects[i];
                    string three = CardSchoolIcon(f) + CardSchoolIcon(f + 1) + CardSchoolIcon(f + 2);
                    if (e == EFFECT_TRISHIELD) return Num("-" + p.ToString() + "%") + " " + three + Icon(ICON_SHIELD);
                    if (e == EFFECT_TRITRAP) return Num("+" + p.ToString() + "%") + " " + three + Icon(ICON_TRAP);
                    return Num("+" + p.ToString() + "%") + " " + three + Icon(ICON_BLADE);
                }
                case EFFECT_DRAIN: return hit + "\n" + Num((x > 0 ? x : 50).ToString() + "%") + " " + Icon(ICON_HEAL) + " to you";
                case EFFECT_FOCUS: return Num("+" + p.ToString() + "%") + " " + Icon(ICON_ACCURACY) + " next spell";
                case EFFECT_STRIKEFX:
                {
                    int kind = x / 1000, pct = x % 1000;
                    if (kind == 0) return hit + "\n" + Num("-" + pct.ToString() + "%") + " " + Icon(ICON_ANY) + Icon(ICON_SHIELD) + " on you";
                    if (kind == 1) return hit + "\n" + Num("+" + pct.ToString() + "%") + " " + CardSchoolIcon(sc) + Icon(ICON_BLADE) + " on you";
                    return hit + "\n" + Num("+" + pct.ToString() + "%") + " " + CardSchoolIcon(sc) + Icon(ICON_TRAP) + " on them";
                }
                case EFFECT_CURSE:
                    return Num("+" + p.ToString() + "%") + " " + Icon(ICON_ANY) + Icon(ICON_TRAP) + " on them\n" + Num("+" + x.ToString() + "%") + " " + Icon(ICON_ANY) + Icon(ICON_TRAP) + " on you";
                case EFFECT_HEALBLADE: return Num("+" + p.ToString() + "%") + " " + Icon(ICON_HEAL) + Icon(ICON_BLADE) + " next heal";
                case EFFECT_BARRAGE: return Num(x.ToString()) + " x " + hit;
                case EFFECT_HACK:
                    return (p > 0 ? hit + "\n" : "") + "Steal " + Icon(ICON_BLADE) + (x == 1 ? Icon(ICON_SHIELD) : "") + (aoe[i] != 0 ? " from all" : "");
                case EFFECT_FIELD: return FieldWord(cardId) + ": " + Num("+" + p.ToString() + "%") + " " + CardSchoolIcon(sc) + Icon(ICON_DAMAGE) + "\nall, " + Num(x.ToString()) + " rounds";
                default: return "";
            }
        }

        private const string StackNote = "Copies of the same spell don't stack; different ones do.";

        public string GetEffectText(int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return "";
            int p = powers[i];
            int x = aux[i];
            string range = powerMax[i] > p ? p.ToString() + "-" + powerMax[i].ToString() : p.ToString();
            string dmg = " " + SchoolName(schools[i]) + " damage" + (aoe[i] != 0 ? " to all enemies" : "");
            switch (effects[i])
            {
                case EFFECT_DAMAGE: return "Deals " + range + dmg + (x > 0 ? ", ignoring " + x.ToString() + "% of shields and resist." : ".");
                case EFFECT_DOT:
                    return "Deals " + (p > 0 ? range : DotHit(cardId, false).ToString()) + dmg + ", then burns for " +
                           DotTotal(cardId).ToString() + " more over " + DOT_ROUNDS.ToString() + " rounds (blades boost both).";
                case EFFECT_DETONATE: return "Sets off all of a target's burns at once, for +" + p.ToString() + "% damage.";
                case EFFECT_TAUNT:
                    return "Taunt: monsters must attack you for " + x.ToString() + " rounds, and you gain a " + p.ToString() + "% shield against every school.";
                case EFFECT_STUN:
                    return "Deals " + range + dmg + (x > 0 ? ", ignoring " + x.ToString() + "% of shields and resist" : "") +
                           ". Targets hit lose their next turn. A stunned target gains a stun shield that blocks the next stun.";
                case EFFECT_SHATTER: return "Breaks every shield on the target, then deals " + range + dmg + ".";
                case EFFECT_DISPEL: return "Removes every shield and blade on " + (aoe[i] != 0 ? "all enemies." : "an enemy.");
                case EFFECT_MINION:
                    return "Summons a " + SchoolName(schools[i]) + " minion with " + p.ToString() + " health that fights beside you (" + x.ToString() + " power).";
                case EFFECT_HEAL: return "Heals " + p.ToString() + " health" + (aoe[i] != 0 ? " to all allies." : ".");
                case EFFECT_BARRAGE:
                    return "A barrage of " + x.ToString() + " shots, " + range + dmg + " each. Every shot is its own hit, so each one uses up a shield; a blade boosts every shot.";
                case EFFECT_HACK:
                    return (p > 0 ? "Deals " + range + dmg + ", then steals " : "Steals ") + "every blade" + (x == 1 ? " and shield" : "") +
                           (aoe[i] != 0 ? " from all enemies" : " from the target") + " for yourself.";
                case EFFECT_FIELD:
                    return "A " + (schools[i] == TECH ? "grid" : "bubble") + " over the whole duel: every " + SchoolName(schools[i]) + " spell (anyone's) deals and heals " + p.ToString() +
                           "% more for " + x.ToString() + " rounds. A new grid or bubble replaces it.";
                case EFFECT_WARD:
                    return "Shield: blocks " + p.ToString() + "% of the next " + SchoolPhrase(ShieldSchool(cardId), "spell from any school") +
                           (ShieldSchool(cardId) == ANY_SCHOOL ? "" : " spell") + " that hits. " + StackNote;
                case EFFECT_EMPOWER:
                    return "Blade: the next " + SchoolPhrase(BladeSchool(cardId), "attack") + (BladeSchool(cardId) == ANY_SCHOOL ? "" : " attack") +
                           " deals +" + p.ToString() + "% damage. " + StackNote;
                case EFFECT_TRAP:
                    return "Trap an enemy: the next " + SchoolPhrase(TrapSchool(cardId), "spell") + (TrapSchool(cardId) == ANY_SCHOOL ? "" : " spell") +
                           " that hits them deals +" + p.ToString() + "% damage. " + StackNote;
                case EFFECT_TRIBLADE:
                {
                    int f = TriBladeFirstSchool(cardId);
                    return "Puts 3 blades on a wizard: " + SchoolName(f) + ", " + SchoolName(f + 1) + " and " + SchoolName(f + 2) +
                           " (+" + p.ToString() + "% each).";
                }
                case EFFECT_DRAIN:
                    return "Deals " + range + dmg + " and heals you for " + (x > 0 ? x : 50).ToString() + "% of it.";
                case EFFECT_FOCUS: return "Next spell gains +" + p.ToString() + "% accuracy.";
                case EFFECT_TRISHIELD:
                {
                    int f = TriBladeFirstSchool(cardId);
                    return "Puts 3 shields on a wizard: " + SchoolName(f) + ", " + SchoolName(f + 1) + " and " + SchoolName(f + 2) +
                           " (each blocks " + p.ToString() + "% of the next spell of its school).";
                }
                case EFFECT_TRITRAP:
                {
                    int f = TriBladeFirstSchool(cardId);
                    return "Puts 3 traps on an enemy: " + SchoolName(f) + ", " + SchoolName(f + 1) + " and " + SchoolName(f + 2) +
                           " (+" + p.ToString() + "% to the next spell of each school that hits them).";
                }
                case EFFECT_MULTIWARD:
                    return "Puts " + x.ToString() + " shields on a wizard, each blocking " + p.ToString() + "% of a " +
                           SchoolPhrase(ShieldSchool(cardId), "spell from any school") + (ShieldSchool(cardId) == ANY_SCHOOL ? "" : " spell") + " that hits.";
                case EFFECT_STRIKEFX:
                {
                    int kind = x / 1000, pct = x % 1000;
                    string extra = kind == 0 ? ", and shields you: blocks " + pct.ToString() + "% of the next spell that hits you."
                        : kind == 1 ? ", and your next " + SchoolName(schools[i]) + " attack deals +" + pct.ToString() + "%."
                        : ", and traps them: the next " + SchoolName(schools[i]) + " spell that hits them deals +" + pct.ToString() + "%.";
                    return "Deals " + range + dmg + extra;
                }
                case EFFECT_CURSE:
                    return "Trap an enemy: the next spell that hits them deals +" + p.ToString() + "%. But you're trapped too: the next spell that hits you deals +" +
                           x.ToString() + "%.";
                case EFFECT_HEALBLADE: return "Healing charm: the wizard's next heal heals " + p.ToString() + "% more.";
                case EFFECT_SELFHEAL: return "Heals you (only you) for " + p.ToString() + " health.";
                default: return "";
            }
        }

        /// <summary>Multi-line details for the spell details box (the tree's own requirement).</summary>
        public string GetDetails(int cardId) { return DetailsText(null, cardId); }

        /// <summary>
        /// The details as this character sees them: "Requires" names the spell they actually need first, skipping another
        /// school's blade or trap they can't learn (the same as <see cref="PrereqFor"/>, which the trainer goes by).
        /// </summary>
        public string GetDetailsFor(CharacterData c, int cardId) { return DetailsText(c, cardId); }

        private string DetailsText(CharacterData c, int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return "";
            int pre = c != null ? PrereqFor(c, cardId) : prereqs[i];
            return GetEffectText(cardId) + "\n" +
                   "Cost: " + pipCosts[i].ToString() + " pips    Accuracy: " + accuracies[i].ToString() + "%\n" +
                   SchoolName(schools[i]) + " spell, level " + levelReqs[i].ToString() +
                   (pre > 0 && !IsQuestSpell(cardId) ? "\nRequires: " + GetName(pre) : "") +
                   (IsTrialSpell(cardId) ? "\nLearned from the " + SchoolName(schools[i]) + " trial quest" :
                    IsStarshardSpell(cardId) ? "\nA Starshard spell: found, ludicrously rarely, in Starshard card packs" :
                    IsQuestSpell(cardId) ? "\nLearned from a " + SchoolName(schools[i]) + " school quest" : "") +
                   (SchoolOnly(cardId) ? "\nOnly " + SchoolName(schools[i]) + " wizards can learn it" : "");
        }

        // ------------------------------------------------------------------ monsters
        // Monster definitions live here too (one shared database behaviour); see BattleManager.roster.

        public const int MONSTER_SPELLS = 6;

        // Defaults only: the scene builder overwrites the monster tables from Assets/WizardsVRC/WorldData/Monsters.asset.
        // Training Yard: 1 Gloomfang the Warden (boss), 2 Bone Thrall, 3 Marsh Sprite, 4 Courtyard Rat
        // Lantern Row (Death street): 5 Gloom Wisp, 6 Lantern Snatcher, 7 Ashwick the Snuffer (boss)
        [HideInInspector] public int[] monsterIds = { 1, 2, 3, 4, 5, 6, 7 };
        [HideInInspector] public string[] monsterNames = { "Gloomfang the Warden", "Bone Thrall", "Marsh Sprite", "Courtyard Rat", "Gloom Wisp", "Lantern Snatcher", "Ashwick the Snuffer" };
        [HideInInspector] public int[] monsterSchools = { 4, 5, 3, 2, 4, 4, 4 };
        [HideInInspector] public int[] monsterLevels = { 5, 3, 2, 1, 1, 2, 4 };
        [HideInInspector] public int[] monsterRanks = { 3, 2, 1, 1, 1, 1, 3 };
        [HideInInspector] public string[] monsterTypes = { "Undead", "Undead", "Spirit", "Beast", "Spirit", "Goblin", "Spirit" };
        [HideInInspector] public int[] monsterHps = { 900, 320, 240, 180, 180, 260, 900 };
        /// <summary>MONSTER_SPELLS entries per monster, 0 = none. Monsters use heals on hurt allies and blades/shields on
        /// themselves as well as attacks (see BattleManager.PickForMonster).</summary>
        [HideInInspector] public int[] monsterSpells =
        {
            148, 150, 152, 154, 0, 0,
            160, 161, 162, 0, 0, 0,
            136, 137, 0, 0, 0, 0,
            124, 125, 0, 0, 0, 0,
            148, 0, 0, 0, 0, 0,
            148, 149, 0, 0, 0, 0,
            148, 149, 150, 154, 0, 0,
        };
        [HideInInspector] public int[] monsterXps = { 120, 45, 35, 25, 30, 40, 150 };
        [HideInInspector] public int[] monsterGoldMin = { 40, 10, 8, 5, 5, 8, 50 };
        [HideInInspector] public int[] monsterGoldMax = { 80, 25, 18, 12, 12, 18, 90 };
        /// <summary>Item id dropped (0 = none) and the chance in percent.</summary>
        [HideInInspector] public int[] monsterDropItems = { 25, 10, 11, 10, 10, 11, 26 };
        [HideInInspector] public int[] monsterDropChances = { 60, 25, 25, 20, 20, 25, 60 };
        /// <summary>Body size multiplier (1 = wizard sized).</summary>
        [HideInInspector] public float[] monsterBodyScales = { 1.7f, 1f, 0.8f, 0.7f, 0.6f, 0.9f, 1.6f };
        [HideInInspector] public int[] monsterIsBoss = { 1, 0, 0, 0, 0, 0, 1 };
        /// <summary>1 = elite (a tougher regular enemy): the duel shows "Rank 2 &lt;icon&gt; Elite".</summary>
        [HideInInspector] public int[] monsterIsElite = { 0, 0, 0, 0, 0, 0, 0 };
        /// <summary>Body colour (wandering and ring bodies).</summary>
        [HideInInspector] public Color[] monsterColors =
        {
            new Color(0.25f, 0.2f, 0.3f), new Color(0.8f, 0.78f, 0.7f), new Color(0.4f, 0.75f, 0.45f), new Color(0.5f, 0.45f, 0.4f),
            new Color(0.35f, 0.4f, 0.55f), new Color(0.45f, 0.35f, 0.25f), new Color(0.2f, 0.22f, 0.3f),
        };
        /// <summary>Extra health per extra wizard in the fight (bosses only).</summary>
        [HideInInspector] public int[] monsterHpPerExtraPlayer = { 450, 0, 0, 0, 0, 0, 450 };

        public int MonsterCount() { return monsterIds.Length; }

        public int MonsterIndexOf(int monsterId)
        {
            if (monsterId <= 0) return -1;
            for (int i = 0; i < monsterIds.Length; i++) if (monsterIds[i] == monsterId) return i;
            return -1;
        }

        public string MonsterName(int id) { int i = MonsterIndexOf(id); return i < 0 ? "Monster" : monsterNames[i]; }
        public int MonsterSchool(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterSchools[i]; }
        public int MonsterLevel(int id) { int i = MonsterIndexOf(id); return i < 0 ? 1 : monsterLevels[i]; }
        public int MonsterRank(int id) { int i = MonsterIndexOf(id); return i < 0 || monsterRanks == null || i >= monsterRanks.Length ? 1 : monsterRanks[i]; }
        public string MonsterType(int id) { int i = MonsterIndexOf(id); return i < 0 || monsterTypes == null || i >= monsterTypes.Length ? "" : monsterTypes[i]; }
        public int MonsterHp(int id) { int i = MonsterIndexOf(id); return i < 0 ? 100 : monsterHps[i]; }
        public int MonsterXp(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterXps[i]; }
        public int RollMonsterGold(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : Random.Range(monsterGoldMin[i], monsterGoldMax[i] + 1); }
        public int MonsterDropItem(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterDropItems[i]; }
        public int MonsterDropChance(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterDropChances[i]; }
        public float MonsterBodyScale(int id) { int i = MonsterIndexOf(id); return i < 0 ? 1f : monsterBodyScales[i]; }
        public Color MonsterColor(int id)
        {
            int i = MonsterIndexOf(id);
            return i >= 0 && monsterColors != null && i < monsterColors.Length ? monsterColors[i] : Color.gray;
        }

        public bool IsBossMonster(int id) { int i = MonsterIndexOf(id); return i >= 0 && monsterIsBoss[i] != 0; }
        public bool IsEliteMonster(int id) { int i = MonsterIndexOf(id); return i >= 0 && monsterIsElite != null && i < monsterIsElite.Length && monsterIsElite[i] != 0; }
        public int MonsterHpPerExtraPlayer(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterHpPerExtraPlayer[i]; }

        /// <summary>Spell k (0..MONSTER_SPELLS-1) of the monster, 0 = none.</summary>
        public int MonsterSpell(int id, int k)
        {
            int i = MonsterIndexOf(id);
            if (i < 0 || k < 0 || k >= MONSTER_SPELLS) return 0;
            int idx = i * MONSTER_SPELLS + k;
            return idx < monsterSpells.Length ? monsterSpells[idx] : 0;
        }
    }
}
