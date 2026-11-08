using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Every attack spell's own choreography (CardDatabase.cineStyles, played by SpellCinematic): how it travels, how it
    /// lands, the flourish round the caster as they wind up, how the film shoots it, and the number, shape, handedness and
    /// arc of its bolts. Each is chosen for the spell, and no two attack spells share how they travel and land together;
    /// the build stops with an error if two do. A new attack spell missing from the table gets the first pair nobody uses.
    /// Creature spells send their creature along the path (walkers and beasts keep to the ground on lances, zigzags,
    /// runners, waves, boomerangs and beams; anything can leap a meteor or go through portals).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const int Orb = 0, Shard = 1, Disc = 2;

        // id: delivery, impact, wind-up, camera, bolts (0 = the delivery's own), shape, mirrored, arc height (0-3)
        private static readonly Dictionary<int, int[]> SpellStyleTable = new Dictionary<int, int[]>
        {
            // ---- Fire
            { 100, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Shard, 0, 0 } },   // Ember Lance: a spear of flame
            { 101, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_LOW, 1, Disc, 0, 1 } },   // Smoldering Brand: swings round and brands the rune on
            { 104, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_CRANE, 12, Orb, 0, 1 } },         // Cinder Rain: cinders out of a ring in the sky
            { 106, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 2 } },    // Pyre Hawk: climbs out of sight and dives in a column of fire
            { 108, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_REVERSE, 5, Orb, 1, 0 } },    // Stoke the Embers: a gust that draws the burn in and bursts it
            { 109, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 3, Disc, 0, 1 } },  // Wildfire Oath: three discs corkscrew into a fire whirl
            { 110, new[] { CardDatabase.CINE_GROUND, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_LOW, 3, Shard, 0, 0 } },     // Molten Behemoth: tramps in through the floor, magma spikes
            { 111, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_CRANE, 1, Orb, 1, 2 } },         // Sunfire Wyrm: through a sun gate, a nova
            // ---- Ice
            { 112, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_STANDARD, 3, Shard, 1, 0 } }, // Frost Shard: three shards fan out and shatter
            { 116, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_CRANE, 7, Shard, 0, 0 } },   // Hoarfrost Gale: a wall of sleet
            { 118, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_FOLLOW, 4, Shard, 1, 1 } },      // Frost Wyvern: circles you with its shards, then dives, ice spikes
            { 121, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 1, Orb, 0, 0 } },     // Winter's Bastion: a freezing ray, a column of frost
            { 122, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_LOW, 1, Shard, 0, 0 } },        // Frozen Colossus: a straight charge, the ground ringing
            { 123, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_REVERSE, 12, Shard, 1, 3 } },   // Winter's Wrath: icicles rain, the field bristles
            // ---- Storm
            { 124, new[] { CardDatabase.CINE_ZIGZAG, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 0 } },      // Spark Lash: a jagged crack of lightning
            { 125, new[] { CardDatabase.CINE_ZIGZAG, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_LOW, 2, Orb, 1, 0 } },       // Thunder Hare: darts left and right, thunder rings
            { 128, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 8, Shard, 0, 2 } },     // Thunderclap: bolts from a storm ring, a flash
            { 130, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_REVERSE, 1, Orb, 0, 1 } },    // Skyfall Serpent: out of a storm gate, a cyclone
            { 132, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_CRANE, 2, Disc, 1, 0 } },       // Gale Mark: twin wind discs spiral in and mark the rune
            { 134, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 2 } },          // Skyfall Leviathan: coils in, a thunderous nova
            { 135, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_CRANE, 7, Disc, 0, 2 } },      // Maelstrom Eye: a ring of discs round you, then the maelstrom
            // ---- Life
            { 136, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_STANDARD, 4, Shard, 0, 1 } }, // Thorn Volley: thorns fly, brambles burst up
            { 142, new[] { CardDatabase.CINE_GROUND, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_LOW, 1, Orb, 1, 0 } },          // Grove Stag: charges along a line of springing leaves
            { 146, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_REVERSE, 5, Orb, 0, 0 } },          // Wildwood Stampede: a line abreast, a column of green
            { 147, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 1, Orb, 1, 3 } },        // Verdant Titan: leaps to the sky and lands, roots erupting
            // ---- Death
            { 148, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Orb, 0, 0 } },     // Shade Siphon: a siphoning ray that sucks the life in
            { 149, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_LOW, 1, Orb, 1, 0 } },      // Grave Ghast: through a grave gate, the soul pulled in
            { 152, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_CRANE, 8, Orb, 1, 1 } },         // Soul Swarm: souls circle you, then pour in
            { 154, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_FOLLOW, 1, Orb, 1, 1 } },        // Night Leech: coils in and drinks
            { 157, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_REVERSE, 1, Orb, 1, 0 } },        // Dark Tithe: a beam that seals the rune of the debt
            { 158, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Shard, 1, 2 } }, // Bone Dragon: sweeps round the flank, bone shards
            { 159, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_LOW, 10, Disc, 0, 2 } },         // Requiem of Ash: ash falls and whirls
            // ---- Myth
            { 160, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Disc, 0, 0 } }, // Mirage Dart: a spinning disc that curves in
            { 161, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_FOLLOW, 1, Shard, 1, 0 } },  // Mirage Lancer: a lance charge that shatters shields
            { 165, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_LOW, 5, Orb, 1, 1 } },               // Chimera's Roar: the roar rolls out abreast
            { 166, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_REVERSE, 1, Orb, 0, 0 } },        // Stone Basilisk: its petrifying gaze
            { 169, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 3, Disc, 0, 1 } },       // Riddle of Iron: riddles through a gate, the rune binds
            { 170, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_STANDARD, 3, Orb, 1, 1 } },      // Fabled Hydra: three heads twisting in
            { 171, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_LOW, 1, Orb, 0, 3 } },             // Titan of Legends: a legendary leap and landing
            // ---- Balance
            { 172, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_LOW, 1, Orb, 1, 0 } },            // Prism Ray: a prismatic ray
            { 173, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_STANDARD, 3, Orb, 0, 0 } },    // Sand Djinn: whirls round you before it strikes
            { 176, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_CRANE, 6, Disc, 1, 0 } },          // Sandstorm: a wall of sand that becomes a vortex
            { 178, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_REVERSE, 1, Orb, 1, 2 } },     // Sphinx of Scales: steps through a gate, a column of judgement
            { 182, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Disc, 1, 2 } },      // Sandstorm Sphinx: bounds up and lands, rings of sand
            { 183, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 7, Orb, 0, 2 } },          // Grand Convergence: seven orbs converge, a nova
        };

        private static int PackStyle(int[] s)
        {
            return (s[0] & 15) | ((s[1] & 15) << 4) | ((s[2] & 15) << 8) | ((s[3] & 15) << 12) | ((s[4] & 15) << 16) | ((s[5] & 3) << 20) |
                   ((s[6] & 1) << 22) | ((s[7] & 3) << 23);
        }

        private static bool IsAttackEffect(int e)
        {
            return e == CardDatabase.EFFECT_DAMAGE || e == CardDatabase.EFFECT_DRAIN || e == CardDatabase.EFFECT_DOT || e == CardDatabase.EFFECT_STUN ||
                   e == CardDatabase.EFFECT_DETONATE || e == CardDatabase.EFFECT_SHATTER || e == CardDatabase.EFFECT_STRIKEFX;
        }

        /// <summary>Fills CardDatabase.cineStyles: every attack spell's recipe, each one's delivery and impact its own.</summary>
        private static void AssignSpellStyles(CardDatabase db)
        {
            int n = db.ids.Length;
            db.cineStyles = new int[n];
            var pairs = new Dictionary<int, string>();
            var missing = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!IsAttackEffect(db.effects[i])) continue;
                int[] s;
                if (!SpellStyleTable.TryGetValue(db.ids[i], out s)) { missing.Add(i); continue; }
                int pair = s[0] * 16 + s[1];
                string other;
                if (pairs.TryGetValue(pair, out other))
                    Debug.LogError("[WizardsVRC] " + db.names[i] + " travels and lands just like " + other + ": give one of them its own (WizardsVRCSceneBuilder.SpellStyles.cs).");
                else pairs[pair] = db.names[i];
                db.cineStyles[i] = PackStyle(s);
            }
            // Attack spells not in the table: the first delivery and impact nobody has, the rest from the id.
            foreach (int i in missing)
            {
                int id = db.ids[i];
                int[] s = null;
                for (int k = 0; k < 13 * 9 && s == null; k++)
                {
                    int d = 1 + (id + k) % 12, im = (id / 3 + k) % 9;
                    if (pairs.ContainsKey(d * 16 + im)) continue;
                    s = new[] { d, im, id % 5, (id / 5) % 5, 0, id % 3, (id / 7) % 2, (id / 11) % 4 };
                    pairs[d * 16 + im] = db.names[i];
                }
                if (s == null) s = new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_BURST, 0, 0, 0, 0, 0, 0 };
                db.cineStyles[i] = PackStyle(s);
                Debug.Log("[WizardsVRC] " + db.names[i] + " has no choreography of its own in SpellStyles.cs yet: it travels by " + s[0] + " and lands by " + s[1] + ".");
            }
            Debug.Log("[WizardsVRC] Spell choreography: " + pairs.Count + " attack spells, each travelling and landing its own way.");
        }
    }
}
