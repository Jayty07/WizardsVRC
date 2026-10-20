using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Static spell card definitions (placeholder fantasy spells). School cards use id 100 + school * 10 + k;
    /// general cards (any school) are 200+. Parallel arrays indexed by definition index; each school has 7 spells
    /// (rows of 7 below, levels 1/2/3/5/8/12/16) and the last row is the general spells.
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

        /// <summary>Which school a shield/blade/trap applies to: -1 = every school.</summary>
        public const int ANY_SCHOOL = -1;
        private const int ICE = 1;

        /// <summary>Max cards per deck and max copies of one card per deck.</summary>
        public const int DECK_MAX = 20;
        public const int MAX_COPIES = 4;

        // id, school (-1 general), level req, pip cost, accuracy %, power, effect
        [HideInInspector] public int[] ids =
        {
            100, 101, 102, 103, 104, 105, 106,
            110, 111, 112, 113, 114, 115, 116,
            120, 121, 122, 123, 124, 125, 126,
            130, 131, 132, 133, 134, 135, 136,
            140, 141, 142, 143, 144, 145, 146,
            150, 151, 152, 153, 154, 155, 156,
            160, 161, 162, 163, 164, 165, 166,
            200, 201, 202, 203,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            107, 108, 117, 118, 127, 128, 137, 138, 139, 147, 148, 157, 158, 167, 168, 169, 170, 171,
        };
        [HideInInspector] public int[] schools =
        {
            0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4, 4, 4,
            5, 5, 5, 5, 5, 5, 5,
            6, 6, 6, 6, 6, 6, 6,
            -1, -1, -1, -1,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            0, 0, 1, 1, 2, 2, 3, 3, 3, 4, 4, 5, 5, 6, 6, 6, 6, 6,
        };
        [HideInInspector] public int[] levelReqs =
        {
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 5, 8, 12, 16,
            1, 2, 3, 4,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            4, 10, 6, 10, 4, 10, 4, 6, 10, 6, 10, 6, 10, 4, 6, 10, 14, 14,
        };
        [HideInInspector] public int[] pipCosts =
        {
            1, 2, 3, 4, 1, 5, 6,
            1, 2, 3, 4, 1, 5, 6,
            1, 2, 3, 4, 1, 5, 6,
            1, 2, 2, 4, 0, 5, 6,
            1, 2, 3, 4, 1, 5, 6,
            1, 2, 3, 4, 1, 5, 6,
            1, 2, 3, 4, 0, 5, 6,
            0, 1, 0, 0,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
        };
        [HideInInspector] public int[] accuracies =
        {
            75, 75, 75, 75, 100, 75, 75,
            80, 80, 80, 80, 100, 80, 80,
            70, 70, 70, 70, 100, 70, 70,
            90, 90, 90, 90, 100, 90, 90,
            85, 85, 85, 85, 100, 85, 85,
            80, 80, 80, 80, 100, 80, 80,
            85, 85, 85, 85, 100, 85, 85,
            100, 100, 100, 100,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100,
        };
        [HideInInspector] public int[] powers =
        {
            80, 160, 250, 380, 35, 420, 380,
            70, 140, 220, 330, 30, 400, 360,
            100, 200, 300, 450, 35, 560, 480,
            60, 200, 120, 300, 30, 380, 260,
            65, 130, 210, 300, 50, 400, 360,
            75, 150, 230, 340, 50, 430, 370,
            70, 145, 225, 335, 40, 420, 380,
            50, 100, 25, 15,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            50, 30, 35, 30, 50, 30, 50, 35, 30, 35, 30, 35, 30, 50, 35, 30, 35, 35,
        };
        [HideInInspector] public int[] effects =
        {
            0, 0, 0, 0, 3, 0, 0,
            0, 0, 0, 0, 2, 0, 0,
            0, 0, 0, 0, 3, 0, 0,
            0, 1, 1, 0, 5, 0, 1,
            0, 0, 0, 4, 2, 4, 0,
            0, 0, 0, 0, 2, 0, 0,
            0, 0, 0, 0, 5, 0, 4,
            2, 1, 3, 5,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            2, 6, 3, 6, 2, 6, 2, 3, 6, 3, 6, 3, 6, 2, 3, 6, 7, 7,
        };
        /// <summary>1 = hits every enemy (damage) or every ally (heal).</summary>
        [HideInInspector] public int[] aoe =
        {
            0, 0, 1, 0, 0, 0, 1,
            0, 0, 1, 0, 0, 0, 1,
            0, 0, 1, 0, 0, 0, 1,
            0, 0, 1, 0, 0, 0, 1,
            0, 0, 1, 0, 0, 0, 1,
            0, 0, 0, 0, 0, 0, 1,
            0, 0, 1, 0, 0, 0, 0,
            0, 0, 0, 0,
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };
        [HideInInspector] public string[] names =
        {
            "Ember Bolt", "Flame Lash", "Cinder Storm", "Inferno Pillar", "Kindling Rite", "Blazing Wyrm", "Sunfire Cataclysm",
            "Frost Shard", "Rime Spear", "Glacier Fall", "Hoarfrost Nova", "Frostward", "Frozen Colossus", "Winter's Wrath",
            "Spark Jolt", "Arc Lance", "Thunderclap", "Tempest Crown", "Static Charge", "Skyfall Leviathan", "Maelstrom Eye",
            "Thornlash", "Verdant Bloom", "Rejuvenate", "Grove Guardian", "Seedling Blessing", "Wildwood Stampede", "Bloom of Renewal",
            "Shade Touch", "Grave Chill", "Wither", "Soul Siphon", "Veil of Dread", "Bone Dragon", "Requiem of Ash",
            "Phantom Strike", "Mirage Blade", "Golem Fist", "Legend's Echo", "Mirror Image", "Stone Basilisk", "Fabled Hydra",
            "Prism Spark", "Arcane Scale", "Harmonic Pulse", "Equilibrium Ruin", "Equanimity", "Sandstorm Sphinx", "Grand Convergence",
            "Tower Ward", "Mend", "Empower", "Steady Mind",
            // shields, blades, traps and tri-blades (appended so existing ids/indices keep their meaning)
            "Ember Aegis", "Cinder Snare", "Rime Edge", "Frost Snare", "Storm Aegis", "Thunder Snare",
            "Verdant Aegis", "Thorn Edge", "Bramble Snare", "Grave Edge", "Hex Snare", "Fable Edge", "Mirage Snare",
            "Balance Aegis", "Scale Edge", "Sand Snare", "Elemental Tri-Blade", "Spirit Tri-Blade",
        };

        [HideInInspector] public int[] triBladeIds = { 170, 171 };
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

        public bool IsAoe(int cardId) { int i = IndexOf(cardId); return i >= 0 && aoe[i] != 0; }

        /// <summary>True for heals and buffs (target your own side), false for attacks.</summary>
        public bool TargetsAllies(int cardId)
        {
            int e = GetEffect(cardId);
            return e == EFFECT_HEAL || e == EFFECT_WARD || e == EFFECT_EMPOWER || e == EFFECT_FOCUS || e == EFFECT_TRIBLADE;
        }

        /// <summary>Ice shields and General shields block every school; other shields block their own school.</summary>
        public int ShieldSchool(int cardId)
        {
            int s = GetSchool(cardId);
            return s == ICE ? ANY_SCHOOL : s;
        }

        /// <summary>School blades boost that school's attacks; General blades boost any attack.</summary>
        public int BladeSchool(int cardId) { return GetSchool(cardId); }

        public int TrapSchool(int cardId) { return GetSchool(cardId); }

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
            switch (effects[i])
            {
                case EFFECT_DAMAGE: return "Deals " + p.ToString() + " " + SchoolName(schools[i]) + " damage" + (aoe[i] != 0 ? " to all enemies." : ".");
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
                case EFFECT_DRAIN: return "Deals " + p.ToString() + " " + SchoolName(schools[i]) + " damage and heals you for half.";
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
                   SchoolName(schools[i]) + " spell, level " + levelReqs[i].ToString();
        }
    }
}
