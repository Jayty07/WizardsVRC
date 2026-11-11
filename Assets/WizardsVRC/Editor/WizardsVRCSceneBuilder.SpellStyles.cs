using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Every staged spell's own six beats (CardDatabase.cineStyles and cineBeats, played by SpellCinematic; the formula is
    /// SPELL_ANIMATIONS.md). Cast: the flourish round the caster as they charge. Arrival: how its power arrives. Display:
    /// how it shows off. Attack: how it travels, and the number, shape, handedness and arc of its bolts. Result: how it
    /// lands. Exit: how it leaves. And how the film shoots it. Each is chosen for the spell, and no two share how they
    /// travel and land together; the build stops with an error if two do. Every attack spell is staged, and so are the
    /// heals and the dispel (a heal is marked as helping its target: it lands gently). A new attack spell missing from
    /// the table gets the first pair nobody uses and beats from its id.
    /// Creature spells send their creature along the path (walkers and beasts keep to the ground on lances, zigzags,
    /// runners, waves, boomerangs and beams; anything can leap a meteor or go through portals).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const int Orb = 0, Shard = 1, Disc = 2;
        // Arrival: up out of the rune, down out of the sky, drawn in from all round, through a gate, wound up out of sparks.
        private const int ArErupt = CardDatabase.ARRIVE_ERUPT, ArDescend = CardDatabase.ARRIVE_DESCEND, ArConverge = CardDatabase.ARRIVE_CONVERGE,
                          ArRift = CardDatabase.ARRIVE_RIFT, ArKindle = CardDatabase.ARRIVE_KINDLE;
        // Display: pulses throwing rings of sparks, rings spinning up round it, a crown of bolts, crackling at the floor, a column of light.
        private const int ShFlare = CardDatabase.SHOW_FLARE, ShHalo = CardDatabase.SHOW_HALO, ShCrown = CardDatabase.SHOW_CROWN,
                          ShStorm = CardDatabase.SHOW_STORM, ShAscend = CardDatabase.SHOW_ASCEND;
        // Exit: embers drifting up, blown apart, a column of sparks into the sky, sinking into the floor, a last pair of rings.
        private const int ExFade = CardDatabase.EXIT_FADE, ExScatter = CardDatabase.EXIT_SCATTER, ExAscend = CardDatabase.EXIT_ASCEND,
                          ExSink = CardDatabase.EXIT_SINK, ExEcho = CardDatabase.EXIT_ECHO;

        // id: delivery, impact, cast flourish, camera, bolts (0 = the delivery's own), shape, mirrored, arc height (0-3),
        //     arrival, display, exit, helps its target (1 = a heal)
        private static readonly Dictionary<int, int[]> SpellStyleTable = new Dictionary<int, int[]>
        {
            // ---- Fire
            { 100, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Shard, 0, 0, ArErupt, ShFlare, ExFade, 0 } },   // Ember Lance: a spear of flame
            { 101, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_LOW, 1, Disc, 0, 1, ArKindle, ShHalo, ExSink, 0 } },   // Smoldering Brand: swings round and brands the rune on
            { 104, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_CRANE, 12, Orb, 0, 1, ArKindle, ShAscend, ExScatter, 0 } },         // Cinder Rain: cinders out of a ring in the sky
            { 106, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 2, ArDescend, ShFlare, ExAscend, 0 } },    // Pyre Hawk: climbs out of sight and dives in a column of fire
            { 108, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_REVERSE, 5, Orb, 1, 0, ArConverge, ShStorm, ExEcho, 0 } },    // Stoke the Embers: a gust that draws the burn in and bursts it
            { 109, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 3, Disc, 0, 1, ArKindle, ShHalo, ExAscend, 0 } },  // Wildfire Oath: three discs corkscrew into a fire whirl
            { 110, new[] { CardDatabase.CINE_GROUND, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_LOW, 3, Shard, 0, 0, ArErupt, ShStorm, ExSink, 0 } },     // Molten Behemoth: tramps in through the floor, magma spikes
            { 111, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_CRANE, 1, Orb, 1, 2, ArRift, ShFlare, ExScatter, 0 } },         // Sunfire Wyrm: its own staging (SpellSpecialTable); this is its charge, its nova, and a minion's cast of it
            // ---- Ice
            { 112, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_STANDARD, 3, Shard, 1, 0, ArConverge, ShCrown, ExFade, 0 } }, // Frost Shard: three shards fan out and shatter
            { 116, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_CRANE, 7, Shard, 0, 0, ArDescend, ShStorm, ExScatter, 0 } },   // Hoarfrost Gale: a wall of sleet
            { 118, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_FOLLOW, 4, Shard, 1, 1, ArDescend, ShHalo, ExAscend, 0 } },      // Frost Wyvern: circles you with its shards, then dives, ice spikes
            { 121, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 1, Orb, 0, 0, ArErupt, ShAscend, ExSink, 0 } },     // Winter's Bastion: a freezing ray, a column of frost
            { 122, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_LOW, 1, Shard, 0, 0, ArErupt, ShFlare, ExEcho, 0 } },        // Frozen Colossus: a straight charge, the ground ringing
            { 123, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_REVERSE, 12, Shard, 1, 3, ArConverge, ShCrown, ExSink, 0 } },   // Winter's Wrath: icicles rain, the field bristles
            // ---- Storm
            { 124, new[] { CardDatabase.CINE_ZIGZAG, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 0, ArKindle, ShStorm, ExFade, 0 } },      // Spark Lash: a jagged crack of lightning
            { 125, new[] { CardDatabase.CINE_ZIGZAG, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_LOW, 2, Orb, 1, 0, ArRift, ShStorm, ExEcho, 0 } },       // Thunder Hare: darts left and right, thunder rings
            { 128, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 8, Shard, 0, 2, ArDescend, ShFlare, ExScatter, 0 } },     // Thunderclap: bolts from a storm ring, a flash
            { 130, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_REVERSE, 1, Orb, 0, 1, ArRift, ShHalo, ExAscend, 0 } },    // Skyfall Serpent: out of a storm gate, a cyclone
            { 132, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_CRANE, 2, Disc, 1, 0, ArConverge, ShHalo, ExEcho, 0 } },       // Gale Mark: twin wind discs spiral in and mark the rune
            { 134, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 2, ArDescend, ShStorm, ExScatter, 0 } },          // Skyfall Leviathan: coils in, a thunderous nova
            { 135, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_CRANE, 7, Disc, 0, 2, ArKindle, ShAscend, ExSink, 0 } },      // Maelstrom Eye: a ring of discs round you, then the maelstrom
            // ---- Life
            { 136, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_STANDARD, 4, Shard, 0, 1, ArErupt, ShCrown, ExFade, 0 } }, // Thorn Volley: thorns fly, brambles burst up
            { 142, new[] { CardDatabase.CINE_GROUND, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_LOW, 1, Orb, 1, 0, ArErupt, ShFlare, ExAscend, 0 } },          // Grove Stag: charges along a line of springing leaves
            { 146, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_REVERSE, 5, Orb, 0, 0, ArConverge, ShStorm, ExEcho, 0 } },          // Wildwood Stampede: a line abreast, a column of green
            { 147, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_SPIKES, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 1, Orb, 1, 3, ArErupt, ShAscend, ExSink, 0 } },        // Verdant Titan: leaps to the sky and lands, roots erupting
            // ---- Death
            { 148, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Orb, 0, 0, ArRift, ShHalo, ExFade, 0 } },     // Shade Siphon: a siphoning ray that sucks the life in
            { 149, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_LOW, 1, Orb, 1, 0, ArErupt, ShStorm, ExSink, 0 } },      // Grave Ghast: through a grave gate, the soul pulled in
            { 152, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_CRANE, 8, Orb, 1, 1, ArConverge, ShHalo, ExScatter, 0 } },         // Soul Swarm: souls circle you, then pour in
            { 154, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_FOLLOW, 1, Orb, 1, 1, ArRift, ShFlare, ExEcho, 0 } },        // Night Leech: coils in and drinks
            { 157, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_REVERSE, 1, Orb, 1, 0, ArDescend, ShAscend, ExSink, 0 } },        // Dark Tithe: a beam that seals the rune of the debt
            { 158, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Shard, 1, 2, ArRift, ShCrown, ExScatter, 0 } }, // Bone Dragon: sweeps round the flank, bone shards
            { 159, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_LOW, 10, Disc, 0, 2, ArKindle, ShStorm, ExAscend, 0 } },         // Requiem of Ash: ash falls and whirls
            // ---- Myth
            { 160, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Disc, 0, 0, ArRift, ShFlare, ExFade, 0 } }, // Mirage Dart: a spinning disc that curves in
            { 161, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_FOLLOW, 1, Shard, 1, 0, ArRift, ShCrown, ExEcho, 0 } },  // Mirage Lancer: a lance charge that shatters shields
            { 165, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_LOW, 5, Orb, 1, 1, ArConverge, ShStorm, ExScatter, 0 } },               // Chimera's Roar: the roar rolls out abreast
            { 166, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_REVERSE, 1, Orb, 0, 0, ArErupt, ShHalo, ExSink, 0 } },        // Stone Basilisk: its petrifying gaze
            { 169, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 3, Disc, 0, 1, ArKindle, ShCrown, ExEcho, 0 } },       // Riddle of Iron: riddles through a gate, the rune binds
            { 170, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_STANDARD, 3, Orb, 1, 1, ArErupt, ShFlare, ExAscend, 0 } },      // Fabled Hydra: three heads twisting in
            { 171, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_LOW, 1, Orb, 0, 3, ArDescend, ShAscend, ExScatter, 0 } },             // Titan of Legends: a legendary leap and landing
            // ---- Balance
            { 172, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_LOW, 1, Orb, 1, 0, ArConverge, ShHalo, ExFade, 0 } },            // Prism Ray: a prismatic ray
            { 173, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_STANDARD, 3, Orb, 0, 0, ArKindle, ShStorm, ExScatter, 0 } },    // Sand Djinn: whirls round you before it strikes
            { 176, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_CRANE, 6, Disc, 1, 0, ArKindle, ShHalo, ExSink, 0 } },          // Sandstorm: a wall of sand that becomes a vortex
            { 178, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_REVERSE, 1, Orb, 1, 2, ArRift, ShAscend, ExEcho, 0 } },     // Sphinx of Scales: steps through a gate, a column of judgement
            { 182, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Disc, 1, 2, ArDescend, ShFlare, ExSink, 0 } },      // Sandstorm Sphinx: bounds up and lands, rings of sand
            { 183, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 7, Orb, 0, 2, ArConverge, ShCrown, ExAscend, 0 } },          // Grand Convergence: seven orbs converge, a nova
            // ---- The trial spells (6 pips)
            { 230, new[] { CardDatabase.CINE_METEOR, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 3, ArErupt, ShFlare, ExAscend, 0 } },      // Phoenix Ascendant: out of its ashes, up and down in a whirl of fire
            { 231, new[] { CardDatabase.CINE_GROUND, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_LOW, 3, Shard, 1, 0, ArDescend, ShHalo, ExSink, 0 } },         // Aurora Glacier: grinds across the floor and breaks up
            { 232, new[] { CardDatabase.CINE_BOOMERANG, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 3, ArDescend, ShStorm, ExScatter, 0 } },   // Skybreaker Roc: wheels round the flank, a thunderclap
            { 234, new[] { CardDatabase.CINE_PORTAL, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_REVERSE, 1, Orb, 1, 1, ArRift, ShHalo, ExSink, 0 } },            // Eclipse Wraith: out of an eclipse, rings of shadow
            { 236, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 7, Orb, 0, 2, ArConverge, ShCrown, ExEcho, 0 } },         // Astral Scales: seven stars weighed out, a nova
            // ---- Heals (they help their target: no flinch, no shake, a gentle landing) and the dispel
            { 137, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_FOUNTAIN, CardDatabase.CAM_CRANE, 8, Orb, 0, 0, ArKindle, ShHalo, ExAscend, 1 } },          // Verdant Bloom: a soft rain of petals, the rune of life
            { 140, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_CRANE, 5, Orb, 0, 0, ArErupt, ShFlare, ExFade, 1 } },             // Rejuvenate: a wave of green light over every ally
            { 144, new[] { CardDatabase.CINE_ORBIT, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_CRANE, 6, Orb, 1, 1, ArConverge, ShAscend, ExEcho, 1 } },       // Wellspring: motes circle, then pour into every ally
            { 145, new[] { CardDatabase.CINE_HELIX, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_STANDARD, 2, Orb, 0, 1, ArKindle, ShAscend, ExFade, 1 } },        // Bloom of Renewal: two strands twine in, a column of blossom
            { 201, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 1, Orb, 0, 0, ArConverge, ShFlare, ExFade, 1 } },     // Mend: one mote of light
            { 204, new[] { CardDatabase.CINE_LANCE, CardDatabase.IMPACT_VORTEX, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_LOW, 1, Disc, 1, 0, ArKindle, ShHalo, ExEcho, 1 } },            // Second Wind: a breath falls on you and whirls up
            { 233, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 12, Orb, 1, 2, ArDescend, ShCrown, ExAscend, 1 } },        // Celestial Bloom: starlight rains on every ally
            { 168, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_NOVA, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_REVERSE, 1, Orb, 0, 0, ArRift, ShStorm, ExScatter, 0 } },            // Sundering Gaze: the gaze sweeps them, a flash strips them
            // ---- Tech (The Dominion's school)
            { 300, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_BURST, CardDatabase.CHARGE_STREAM, CardDatabase.CAM_STANDARD, 3, Shard, 0, 0, ArKindle, ShFlare, ExFade, 0 } },    // Sidearm Burst: three quick shots
            { 301, new[] { CardDatabase.CINE_ZIGZAG, CardDatabase.IMPACT_IMPLODE, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_FOLLOW, 1, Orb, 0, 0, ArConverge, ShStorm, ExEcho, 0 } },     // Taser Dart: a crackling dart that seizes up
            { 304, new[] { CardDatabase.CINE_ZIGZAG, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_LOW, 2, Disc, 1, 0, ArRift, ShHalo, ExSink, 0 } },          // Firewall Breach: code skitters in, the breach rune stamps
            { 306, new[] { CardDatabase.CINE_BEAM, CardDatabase.IMPACT_SHATTER, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_STANDARD, 1, Orb, 0, 0, ArConverge, ShAscend, ExScatter, 0 } },    // Railgun: one blinding slug, armour shatters
            { 307, new[] { CardDatabase.CINE_VOLLEY, CardDatabase.IMPACT_RINGS, CardDatabase.CHARGE_GYRO, CardDatabase.CAM_LOW, 12, Shard, 0, 0, ArKindle, ShCrown, ExScatter, 0 } },        // Gatling Storm: a hail of rounds
            { 312, new[] { CardDatabase.CINE_WAVE, CardDatabase.IMPACT_SIGIL, CardDatabase.CHARGE_SPIRAL, CardDatabase.CAM_CRANE, 6, Disc, 0, 0, ArRift, ShStorm, ExSink, 0 } },        // Zero-Day Exploit: a wave of code over every enemy
            { 313, new[] { CardDatabase.CINE_RAIN, CardDatabase.IMPACT_PILLAR, CardDatabase.CHARGE_COLUMN, CardDatabase.CAM_CRANE, 15, Shard, 0, 3, ArDescend, ShAscend, ExEcho, 0 } },     // Orbital Strike: fire from orbit
        };

        /// <summary>
        /// Spells with staging all their own (CardDatabase.SPECIAL_*): the recipe above still gives them their charge's
        /// flourish, how their hit lands, how they leave and which way they turn; the rest is their own rig's.
        /// </summary>
        private static readonly Dictionary<int, int> SpellSpecialTable = new Dictionary<int, int>
        {
            { SunfireWyrmCard, CardDatabase.SPECIAL_WYRM },   // an eastern dragon out of the caster: round them three times, through its targets, fire
        };

        private static int PackStyle(int[] s)
        {
            return (s[0] & 15) | ((s[1] & 15) << 4) | ((s[2] & 15) << 8) | ((s[3] & 15) << 12) | ((s[4] & 15) << 16) | ((s[5] & 3) << 20) |
                   ((s[6] & 1) << 22) | ((s[7] & 3) << 23);
        }

        private static int PackBeats(int[] s)
        {
            if (s.Length < 12) return 0;
            return (s[8] & 15) | ((s[9] & 15) << 4) | ((s[10] & 15) << 8) | (s[11] != 0 ? CardDatabase.BEATS_FRIENDLY : 0);
        }

        /// <summary>Spells that go through the six beats: every attack, the heals and the dispel.</summary>
        private static bool IsStagedEffect(int e)
        {
            return IsAttackEffect(e) || e == CardDatabase.EFFECT_HEAL || e == CardDatabase.EFFECT_SELFHEAL || e == CardDatabase.EFFECT_DISPEL;
        }

        private static bool IsAttackEffect(int e)
        {
            return e == CardDatabase.EFFECT_DAMAGE || e == CardDatabase.EFFECT_DRAIN || e == CardDatabase.EFFECT_DOT || e == CardDatabase.EFFECT_STUN ||
                   e == CardDatabase.EFFECT_DETONATE || e == CardDatabase.EFFECT_SHATTER || e == CardDatabase.EFFECT_STRIKEFX ||
                   e == CardDatabase.EFFECT_BARRAGE || e == CardDatabase.EFFECT_HACK;
        }

        /// <summary>Fills CardDatabase.cineStyles: every attack spell's recipe, each one's delivery and impact its own.</summary>
        private static void AssignSpellStyles(CardDatabase db)
        {
            int n = db.ids.Length;
            db.cineStyles = new int[n];
            db.cineBeats = new int[n];
            db.cineSpecials = new int[n];
            for (int i = 0; i < n; i++)
            {
                int special;
                if (SpellSpecialTable.TryGetValue(db.ids[i], out special)) db.cineSpecials[i] = special;
            }
            var pairs = new Dictionary<int, string>();
            var missing = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!IsStagedEffect(db.effects[i])) continue;
                int[] s;
                if (!SpellStyleTable.TryGetValue(db.ids[i], out s))
                {
                    // A heal or dispel with no recipe keeps its plain animation; an attack spell gets one made up below.
                    if (IsAttackEffect(db.effects[i])) missing.Add(i);
                    continue;
                }
                int pair = s[0] * 16 + s[1];
                string other;
                if (pairs.TryGetValue(pair, out other))
                    Debug.LogError("[WizardsVRC] " + db.names[i] + " travels and lands just like " + other + ": give one of them its own (WizardsVRCSceneBuilder.SpellStyles.cs).");
                else pairs[pair] = db.names[i];
                db.cineStyles[i] = PackStyle(s);
                db.cineBeats[i] = PackBeats(s);
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
                db.cineBeats[i] = (id % 5) | (((id / 5) % 5) << 4) | (((id / 25) % 5) << 8);
                Debug.Log("[WizardsVRC] " + db.names[i] + " has no choreography of its own in SpellStyles.cs yet: it travels by " + s[0] + " and lands by " + s[1] + ".");
            }
            Debug.Log("[WizardsVRC] Spell staging: " + pairs.Count + " spells (attacks, heals and the dispel), each with its own six beats.");
        }
    }
}
