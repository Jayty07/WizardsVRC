using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Static spell card definitions (original placeholder spells). Each school has a 12-spell tree, tier k having
    /// id 100 + school * 12 + k; each tier needs the one before it (<see cref="prereqs"/>). General cards are 200+.
    /// <see cref="aux"/> is effect-specific: pierce % for damage, heal % for drain, burn total for DoT, rounds for taunt,
    /// minion action power, and 1 = "any school" for shields/blades/traps. Damage spells hit for a random amount from
    /// <see cref="powers"/> (min) to <see cref="powerMax"/> (max); powerMax 0 = a fixed value.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CardDatabase : UdonSharpBehaviour
    {
        public const int GENERAL = -1;

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

        // One row per school (tiers 0-11, id 100 + school * 12 + tier), then the general spells. Generated table.
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
        };
        [HideInInspector] public int[] levelReqs =
        {
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 5, 10, 12, 16, 22, 26, 28, 33, 35, 42, 48,
            1, 2, 3, 4,
        };
        [HideInInspector] public int[] pipCosts =
        {
            1, 2, 0, 0, 3, 0, 4, 4, 1, 0, 5, 7,
            1, 1, 0, 0, 3, 0, 4, 4, 1, 2, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 1, 0, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 3, 3, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 1, 4, 5, 7,
            1, 2, 0, 0, 3, 1, 4, 4, 0, 3, 5, 7,
            1, 2, 0, 0, 3, 0, 4, 4, 1, 1, 5, 7,
            0, 1, 0, 0,
        };
        [HideInInspector] public int[] accuracies =
        {
            75, 75, 100, 100, 75, 100, 75, 100, 75, 100, 75, 75,
            80, 100, 100, 100, 80, 100, 80, 100, 100, 100, 80, 80,
            70, 70, 100, 100, 70, 100, 70, 100, 100, 100, 70, 70,
            90, 90, 100, 100, 90, 100, 90, 100, 90, 90, 90, 90,
            85, 85, 100, 100, 85, 100, 85, 100, 100, 85, 85, 85,
            80, 80, 100, 100, 100, 100, 80, 100, 100, 80, 80, 80,
            85, 85, 100, 100, 85, 100, 85, 100, 100, 100, 85, 85,
            100, 100, 100, 100,
        };
        [HideInInspector] public int[] powers =
        {
            65, 0, 35, 30, 50, 50, 145, 400, 40, 60, 290, 335,
            50, 25, 35, 30, 120, 30, 210, 700, 30, 60, 350, 385,
            90, 175, 35, 30, 210, 50, 385, 300, 30, 70, 575, 625,
            55, 250, 35, 30, 150, 50, 225, 500, 320, 900, 370, 415,
            55, 110, 35, 30, 120, 50, 240, 450, 60, 400, 385, 415,
            70, 130, 35, 30, 300, 0, 225, 650, 50, 95, 400, 480,
            65, 130, 25, 25, 160, 50, 265, 500, 35, 35, 415, 450,
            50, 100, 25, 15,
        };
        [HideInInspector] public int[] effects =
        {
            0, 8, 3, 6, 8, 2, 8, 13, 9, 3, 8, 8,
            0, 10, 3, 6, 0, 2, 0, 13, 2, 10, 0, 0,
            0, 0, 3, 6, 0, 2, 0, 13, 6, 3, 0, 0,
            0, 1, 3, 6, 1, 2, 0, 13, 1, 1, 0, 0,
            4, 4, 3, 6, 4, 2, 4, 13, 6, 4, 4, 4,
            0, 12, 3, 6, 13, 14, 11, 13, 2, 11, 0, 0,
            0, 0, 3, 6, 0, 2, 0, 13, 7, 7, 0, 0,
            2, 1, 3, 5,
        };
        [HideInInspector] public int[] aoe =
        {
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1,
            0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
            0, 0, 0, 0,
        };
        [HideInInspector] public string[] names =
        {
            "Ember Imp", "Smoldering Brand", "Ember Edge", "Cinder Snare", "Cinder Rain", "Ember Aegis", "Pyre Hawk", "Magma Hound", "Stoke the Embers", "Wildfire Oath", "Molten Behemoth", "Sunfire Wyrm",
            "Frost Mite", "Rime Challenge", "Rime Edge", "Frost Snare", "Hoarfrost Gale", "Frostward", "Frost Wyvern", "Rime Sentinel", "Permafrost Bulwark", "Winter's Bastion", "Frozen Colossus", "Winter's Wrath",
            "Spark Sprite", "Thunder Hare", "Static Charge", "Thunder Snare", "Thunderclap", "Storm Aegis", "Skyfall Serpent", "Thunder Golem", "Gale Mark", "Overcharge", "Skyfall Leviathan", "Maelstrom Eye",
            "Thornling", "Verdant Bloom", "Thorn Edge", "Bramble Snare", "Rejuvenate", "Verdant Aegis", "Grove Stag", "Treant Warden", "Wellspring", "Bloom of Renewal", "Wildwood Stampede", "Verdant Titan",
            "Shade Wisp", "Grave Ghast", "Grave Edge", "Hex Snare", "Soul Swarm", "Veil of Dread", "Night Leech", "Bone Thrall", "Dread Mark", "Dark Tithe", "Bone Dragon", "Requiem of Ash",
            "Mire Imp", "Mirage Lancer", "Fable Edge", "Mirage Snare", "Clay Golemling", "Unweave", "Stone Basilisk", "Stone Colossus", "Mirror Image", "Riddle of Iron", "Fabled Hydra", "Titan of Legends",
            "Prism Spark", "Sand Djinn", "Accord Edge", "Sand Snare", "Sandstorm", "Balance Aegis", "Sphinx of Scales", "Sand Golem", "Elemental Tri-Blade", "Spirit Tri-Blade", "Sandstorm Sphinx", "Grand Convergence",
            "Tower Ward", "Mend", "Empower", "Steady Mind",
        };
        [HideInInspector] public int[] prereqs =
        {
            0, 100, 101, 102, 103, 104, 105, 106, 107, 108, 108, 110,
            0, 112, 113, 114, 115, 116, 117, 118, 119, 120, 120, 122,
            0, 124, 125, 126, 127, 128, 129, 130, 131, 132, 132, 134,
            0, 136, 137, 138, 139, 140, 141, 142, 143, 144, 144, 146,
            0, 148, 149, 150, 151, 152, 153, 154, 155, 156, 156, 158,
            0, 160, 161, 162, 163, 164, 165, 166, 167, 168, 168, 170,
            0, 172, 173, 174, 175, 176, 177, 178, 179, 180, 180, 182,
            0, 0, 0, 0,
        };
        [HideInInspector] public int[] aux =
        {
            0, 240, 0, 0, 150, 0, 240, 90, 0, 0, 300, 300,
            0, 2, 0, 0, 0, 0, 0, 60, 0, 3, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 150, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 120, 0, 0, 0, 0,
            50, 50, 0, 0, 30, 0, 50, 90, 0, 100, 50, 30,
            0, 0, 0, 0, 80, 0, 0, 130, 0, 0, 30, 20,
            0, 0, 1, 1, 0, 0, 0, 100, 0, 0, 0, 0,
            0, 0, 0, 0,
        };
        [HideInInspector] public int[] powerMax =
        {
            95, 0, 0, 0, 70, 0, 215, 0, 0, 0, 430, 505,
            80, 0, 0, 0, 180, 0, 310, 0, 0, 0, 530, 575,
            130, 265, 0, 0, 310, 0, 575, 0, 0, 0, 865, 935,
            85, 0, 0, 0, 0, 0, 335, 0, 0, 0, 550, 625,
            85, 170, 0, 0, 180, 0, 360, 0, 0, 600, 575, 625,
            100, 190, 0, 0, 0, 0, 335, 0, 0, 145, 600, 720,
            95, 200, 0, 0, 240, 0, 395, 0, 0, 0, 625, 670,
            0, 0, 0, 0,
        };

        [HideInInspector] public int[] triBladeIds = { 180, 181 };
        [HideInInspector] public int[] triBladeFirstSchools = { 0, 3 };

        [HideInInspector] public string[] schoolNames = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
        [HideInInspector] public Color[] schoolColors =
        {
            new Color(0.85f, 0.3f, 0.15f), new Color(0.45f, 0.7f, 0.95f), new Color(0.55f, 0.35f, 0.85f),
            new Color(0.35f, 0.75f, 0.35f), new Color(0.3f, 0.3f, 0.3f), new Color(0.85f, 0.7f, 0.2f),
            new Color(0.75f, 0.6f, 0.4f),
        };
        /// <summary>
        /// Optional cast cinematic per card (same index as <see cref="ids"/>; null = default spell orb). The scene builder fills
        /// this from prefabs in Assets/WizardsVRC/SpellAnimations named "&lt;id&gt;.prefab" or "&lt;Spell Name&gt;.prefab".
        /// </summary>
        public GameObject[] spellAnimations;

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
        public int BasicSpell(int school) { return 100 + school * TIERS; }
        public int TierOf(int cardId) { return GetSchool(cardId) == GENERAL ? -1 : (cardId - 100) % TIERS; }
        public bool IsQuestSpell(int cardId) { return GetSchool(cardId) != GENERAL && TierOf(cardId) == QUEST_TIER; }
        public int QuestSpell(int school) { return 100 + school * TIERS + QUEST_TIER; }

        public bool IsAoe(int cardId) { int i = IndexOf(cardId); return i >= 0 && aoe[i] != 0; }

        /// <summary>True for heals and buffs (target your own side), false for attacks.</summary>
        public bool TargetsAllies(int cardId)
        {
            int e = GetEffect(cardId);
            return e == EFFECT_HEAL || e == EFFECT_WARD || e == EFFECT_EMPOWER || e == EFFECT_FOCUS || e == EFFECT_TRIBLADE ||
                   e == EFFECT_TAUNT || e == EFFECT_MINION;
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
                    return (p > 0 ? "Deals " + range + dmg + ", then burns" : "Burns" + (aoe[i] != 0 ? " all enemies" : "")) + " for " +
                           x.ToString() + " more over " + DOT_ROUNDS.ToString() + " rounds.";
                case EFFECT_DETONATE: return "Sets off all of a target's burns at once, for +" + p.ToString() + "% damage.";
                case EFFECT_TAUNT:
                    return "Taunt: monsters must attack you for " + x.ToString() + " rounds, and you gain a " + p.ToString() + "% shield against every school.";
                case EFFECT_STUN: return "Deals " + range + dmg + ". Targets hit lose their next turn.";
                case EFFECT_SHATTER: return "Breaks every shield on the target, then deals " + range + dmg + ".";
                case EFFECT_DISPEL: return "Removes every shield and blade on an enemy.";
                case EFFECT_MINION:
                    return "Summons a " + SchoolName(schools[i]) + " minion with " + p.ToString() + " health that fights beside you (" + x.ToString() + " power).";
                case EFFECT_HEAL: return "Heals " + p.ToString() + " health" + (aoe[i] != 0 ? " to all allies." : ".");
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
                default: return "";
            }
        }

        /// <summary>Multi-line details for the spell details box.</summary>
        public string GetDetails(int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return "";
            return GetEffectText(cardId) + "\n" +
                   "Cost: " + pipCosts[i].ToString() + " pips    Accuracy: " + accuracies[i].ToString() + "%\n" +
                   SchoolName(schools[i]) + " spell, level " + levelReqs[i].ToString() +
                   (prereqs[i] > 0 ? "\nRequires: " + GetName(prereqs[i]) : "") +
                   (IsQuestSpell(cardId) ? "\nLearned from the " + SchoolName(schools[i]) + " trial quest" : "");
        }

        // ------------------------------------------------------------------ monsters
        // Monster definitions live here too (one shared database behaviour); see BattleManager.roster.

        public const int MONSTER_SPELLS = 4;

        // Training Yard: 1 Gloomfang the Warden (boss), 2 Bone Thrall, 3 Marsh Sprite, 4 Courtyard Rat
        [HideInInspector] public int[] monsterIds = { 1, 2, 3, 4 };
        [HideInInspector] public string[] monsterNames = { "Gloomfang the Warden", "Bone Thrall", "Marsh Sprite", "Courtyard Rat" };
        [HideInInspector] public int[] monsterSchools = { 4, 5, 3, 2 };
        [HideInInspector] public int[] monsterLevels = { 5, 3, 2, 1 };
        [HideInInspector] public int[] monsterHps = { 900, 320, 240, 180 };
        /// <summary>MONSTER_SPELLS entries per monster, 0 = none.</summary>
        [HideInInspector] public int[] monsterSpells =
        {
            148, 149, 152, 154,
            160, 161, 0, 0,
            136, 137, 0, 0,
            124, 125, 0, 0,
        };
        [HideInInspector] public int[] monsterXps = { 120, 45, 35, 25 };
        [HideInInspector] public int[] monsterGoldMin = { 40, 10, 8, 5 };
        [HideInInspector] public int[] monsterGoldMax = { 80, 25, 18, 12 };
        /// <summary>Item id dropped (0 = none) and the chance in percent.</summary>
        [HideInInspector] public int[] monsterDropItems = { 25, 10, 11, 10 };
        [HideInInspector] public int[] monsterDropChances = { 60, 25, 25, 20 };
        /// <summary>Body size multiplier (1 = wizard sized).</summary>
        [HideInInspector] public float[] monsterBodyScales = { 1.7f, 1f, 0.8f, 0.7f };
        [HideInInspector] public int[] monsterIsBoss = { 1, 0, 0, 0 };
        /// <summary>Extra health per extra wizard in the fight (bosses only).</summary>
        [HideInInspector] public int[] monsterHpPerExtraPlayer = { 450, 0, 0, 0 };

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
        public int MonsterHp(int id) { int i = MonsterIndexOf(id); return i < 0 ? 100 : monsterHps[i]; }
        public int MonsterXp(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterXps[i]; }
        public int RollMonsterGold(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : Random.Range(monsterGoldMin[i], monsterGoldMax[i] + 1); }
        public int MonsterDropItem(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterDropItems[i]; }
        public int MonsterDropChance(int id) { int i = MonsterIndexOf(id); return i < 0 ? 0 : monsterDropChances[i]; }
        public float MonsterBodyScale(int id) { int i = MonsterIndexOf(id); return i < 0 ? 1f : monsterBodyScales[i]; }
        public bool IsBossMonster(int id) { int i = MonsterIndexOf(id); return i >= 0 && monsterIsBoss[i] != 0; }
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
