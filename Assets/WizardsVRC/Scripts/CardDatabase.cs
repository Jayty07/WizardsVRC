using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Static spell card definitions (placeholder fantasy spells). School cards use id 100 + school * 10 + k;
    /// general cards (any school) are 200+. Parallel arrays indexed by definition index.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CardDatabase : UdonSharpBehaviour
    {
        public const int GENERAL = -1;

        public const int EFFECT_DAMAGE = 0;
        public const int EFFECT_HEAL = 1;
        public const int EFFECT_WARD = 2;
        public const int EFFECT_EMPOWER = 3;
        public const int EFFECT_DRAIN = 4;
        public const int EFFECT_FOCUS = 5;

        /// <summary>Max cards per deck and max copies of one card per deck.</summary>
        public const int DECK_MAX = 20;
        public const int MAX_COPIES = 4;

        // id, school (-1 general), level req, pip cost, accuracy %, power, effect
        [HideInInspector] public int[] ids =
        {
            100, 101, 102, 103,  110, 111, 112, 113,  120, 121, 122, 123,  130, 131, 132, 133,
            140, 141, 142, 143,  150, 151, 152, 153,  160, 161, 162, 163,  200, 201, 202, 203,
        };
        [HideInInspector] public int[] schools =
        {
            0, 0, 0, 0,  1, 1, 1, 1,  2, 2, 2, 2,  3, 3, 3, 3,
            4, 4, 4, 4,  5, 5, 5, 5,  6, 6, 6, 6,  -1, -1, -1, -1,
        };
        [HideInInspector] public int[] levelReqs =
        {
            1, 2, 3, 5,  1, 2, 3, 5,  1, 2, 3, 5,  1, 2, 3, 5,
            1, 2, 3, 5,  1, 2, 3, 5,  1, 2, 3, 5,  1, 2, 3, 4,
        };
        [HideInInspector] public int[] pipCosts =
        {
            1, 2, 3, 4,  1, 2, 3, 4,  1, 2, 3, 4,  1, 2, 2, 4,
            1, 2, 3, 4,  1, 2, 3, 4,  1, 2, 3, 4,  0, 1, 0, 0,
        };
        [HideInInspector] public int[] accuracies =
        {
            75, 75, 75, 75,  80, 80, 80, 80,  70, 70, 70, 70,  90, 90, 90, 90,
            85, 85, 85, 85,  80, 80, 80, 80,  85, 85, 85, 85,  100, 100, 100, 100,
        };
        [HideInInspector] public int[] powers =
        {
            80, 160, 250, 380,  70, 140, 220, 330,  100, 200, 300, 450,  60, 200, 120, 300,
            65, 130, 210, 300,  75, 150, 230, 340,  70, 145, 225, 335,  30, 100, 25, 15,
        };
        [HideInInspector] public int[] effects =
        {
            0, 0, 0, 0,  0, 0, 0, 0,  0, 0, 0, 0,  0, 1, 1, 0,
            0, 0, 0, 4,  0, 0, 0, 0,  0, 0, 0, 0,  2, 1, 3, 5,
        };
        /// <summary>1 = hits every enemy (damage) or every ally (heal).</summary>
        [HideInInspector] public int[] aoe =
        {
            0, 0, 1, 0,  0, 0, 1, 0,  0, 0, 1, 0,  0, 0, 1, 0,
            0, 0, 1, 0,  0, 0, 0, 0,  0, 0, 1, 0,  0, 0, 0, 0,
        };
        [HideInInspector] public string[] names =
        {
            "Ember Bolt", "Flame Lash", "Cinder Storm", "Inferno Pillar",
            "Frost Shard", "Rime Spear", "Glacier Fall", "Hoarfrost Nova",
            "Spark Jolt", "Arc Lance", "Thunderclap", "Tempest Crown",
            "Thornlash", "Verdant Bloom", "Rejuvenate", "Grove Guardian",
            "Shade Touch", "Grave Chill", "Wither", "Soul Siphon",
            "Phantom Strike", "Mirage Blade", "Golem Fist", "Legend's Echo",
            "Prism Spark", "Arcane Scale", "Harmonic Pulse", "Equilibrium Ruin",
            "Ward", "Mend", "Empower", "Steady Mind",
        };

        [HideInInspector] public string[] schoolNames = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };
        [HideInInspector] public Color[] schoolColors =
        {
            new Color(0.85f, 0.3f, 0.15f), new Color(0.45f, 0.7f, 0.95f), new Color(0.55f, 0.35f, 0.85f),
            new Color(0.35f, 0.75f, 0.35f), new Color(0.3f, 0.3f, 0.3f), new Color(0.85f, 0.7f, 0.2f),
            new Color(0.75f, 0.6f, 0.4f),
        };
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
        public bool IsAoe(int cardId) { int i = IndexOf(cardId); return i >= 0 && aoe[i] != 0; }

        /// <summary>True for heals and buffs (target your own side), false for attacks.</summary>
        public bool TargetsAllies(int cardId)
        {
            int e = GetEffect(cardId);
            return e == EFFECT_HEAL || e == EFFECT_WARD || e == EFFECT_EMPOWER || e == EFFECT_FOCUS;
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

        /// <summary>True if this character has learned the card (own school or general, level high enough).</summary>
        public bool Knows(CharacterData c, int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0 || c == null) return false;
            int s = schools[i];
            return (s == GENERAL || s == (int)c.school) && c.level >= levelReqs[i];
        }

        public string GetEffectText(int cardId)
        {
            int i = IndexOf(cardId);
            if (i < 0) return "";
            int p = powers[i];
            switch (effects[i])
            {
                case EFFECT_DAMAGE: return "Deals " + p.ToString() + " " + SchoolName(schools[i]) + " damage" + (aoe[i] != 0 ? " to all enemies." : ".");
                case EFFECT_HEAL: return "Heals " + p.ToString() + " health" + (aoe[i] != 0 ? " to all allies." : ".");
                case EFFECT_WARD: return "Blocks " + p.ToString() + "% of the next hit taken.";
                case EFFECT_EMPOWER: return "Next attack deals +" + p.ToString() + "% damage.";
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
                   SchoolName(schools[i]) + " spell, learned at level " + levelReqs[i].ToString();
        }
    }
}
