using System.Collections.Generic;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Kobold dig crews from Bigdig (World 3, the kobolds' homeland deep underground), prospecting across World 1 and
    /// World 2. Each crew has a claim against a wall of its street: a dig hole ringed with spoil, a heap of dirt, an ore
    /// cart with gold glinting in it, a gold pan on a crate, a lantern on a post and their company's sign; a foreman you
    /// can talk to and a couple of diggers at work.
    ///
    /// What they say are hints for World 3: the old gold of Bigdig "sings" and has gone quiet; the elders say it and the
    /// Starfall Crystal were struck from the same star; someone is buying up every scrap of it; a kobold-made door below
    /// Copperbottom Mine is locked from the other side; and somebody tall, with spells, will have to go down and see.
    ///
    /// The crews are added by the builder (not in the world data): each street listed in Crews gets one, in an open spot
    /// against a wall, clear of the duel rings, its tower, the way in and the street's own NPCs and points.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class KoboldCrewInfo
        {
            public string zone, foremanKey, foremanName, claim;
            public string[] lines;
            public string[] diggers;
        }

        private static readonly KoboldCrewInfo[] Crews =
        {
            // ---------------- World 1
            new KoboldCrewInfo
            {
                zone = "LanternRow", foremanKey = "KoboldGrizzik", foremanName = "Foreman Grizzik Tallowlamp", claim = "Claim No. 7",
                diggers = new[] { "Digger Snib", "Digger Pell" },
                lines = new[]
                {
                    "Don't mind us, wizard. Bigdig Prospecting Company, all papers in order. Mostly.",
                    "Gold. The old kind, that hums when you hold it to your ear. Back home in Bigdig the seams have gone quiet.",
                    "The elders say the singing gold and your castle's crystal were struck from the same star. Funny thing to dig for under a lamp post, eh?",
                },
            },
            new KoboldCrewInfo
            {
                zone = "TidewatchDocks", foremanKey = "KoboldNell", foremanName = "Prospector Nell Brineknuckle", claim = "Claim No. 12",
                diggers = new[] { "Digger Murk" },
                lines = new[]
                {
                    "We follow the hum. Today the hum says: under the fish market. Don't ask.",
                    "Every kobold in Bigdig knows the old story. When the deep gold goes silent, something is drinking the light from the top down.",
                    "Shipped six crates home this month. Six! And the Deepwarden writes back that it still isn't enough.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "KilnStreet", foremanKey = "KoboldPib", foremanName = "Overseer Pib Cindertooth", claim = "Claim No. 9",
                diggers = new[] { "Digger Tamm", "Digger Ozzle" },
                lines = new[]
                {
                    "Kilns run hot, ground runs hotter. Good sign for gold. Bad sign for my tail.",
                    "That dome of light over your isles? Lovely bit of work. My gran said Bigdig had one too, once. Underground, mind. Then it went out.",
                    "If you ever find gold that glows in the dark, wizard, don't spend it. Bring it to a kobold. Please.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "ThornhollowGrove", foremanKey = "KoboldWix", foremanName = "Digmistress Wix Mossjaw", claim = "Claim No. 21",
                diggers = new[] { "Digger Fenna" },
                lines = new[]
                {
                    "Roots everywhere! Gold hides under roots, everyone knows that. Roots know where the good stuff is.",
                    "Bigdig's a long way down. Past the bottom of the bottom. You'll see it one day, I reckon. Bring a lamp.",
                    "Somebody's been buying up every scrap of singing gold in the worlds. Not us. We'd know. We're us.",
                },
            },
            // ---------------- World 2 (Dustwind)
            new KoboldCrewInfo
            {
                zone = "TumbleweedRow", foremanKey = "KoboldKrel", foremanName = "Boss Krel Dustgrin", claim = "Claim No. 31",
                diggers = new[] { "Digger Rasp", "Digger Ninn" },
                lines = new[]
                {
                    "Dustwind gold's got grit in it. Grit, and a bit of starlight if you're lucky.",
                    "Folks here keep their gold in banks. Kobolds keep ours in pockets. Faster.",
                    "Word from Bigdig: the Deep Seam went dark three more tunnels this week. Nobody down there is saying why.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "CoyoteGulch", foremanKey = "KoboldRatchet", foremanName = "Prospector Ratchet Pebblewhisk", claim = "Claim No. 34",
                diggers = new[] { "Digger Bix" },
                lines = new[]
                {
                    "The gulch sang to me last night. Gold sings, if you're a kobold. Mostly it sings 'dig here'.",
                    "Tell the lizardfolk we're not stealing anything. We're borrowing. From the ground.",
                    "The elders sent us every which way: up to the castle isles, out to the desert. Looking for gold that still sings.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "CopperbottomMine", foremanKey = "KoboldMog", foremanName = "Chief Digger Mog Brasswhisker", claim = "Claim No. 40",
                diggers = new[] { "Digger Gub", "Digger Tilly" },
                lines = new[]
                {
                    "Copper, copper, copper. Copper's for pots. We want the gold behind the copper.",
                    "Down past the bottom shaft there's a door that isn't on any map. Kobold-made, old as old. Locked from the other side.",
                    "When the mine goes quiet you can hear it: tapping, deep down. Not us. We stopped tapping an hour ago.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "ThunderMesa", foremanKey = "KoboldZizi", foremanName = "Surveyor Zizi Sparkfang", claim = "Claim No. 38",
                diggers = new[] { "Digger Wort" },
                lines = new[]
                {
                    "Lightning finds gold, you know. Strikes where the ground's full of it. So we stand here and wait. Bravely.",
                    "The storms out here hum the same note as the gold back home. Same note as your crystal, too. Funny, that.",
                    "If the deep gold ever stops humming altogether, wizard... well. Let's just keep digging.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "LonesomeBend", foremanKey = "KoboldFennick", foremanName = "Old Digger Fennick Rustclaw", claim = "Claim No. 1",
                diggers = new[] { "Digger Quill", "Digger Sprat" },
                lines = new[]
                {
                    "Forty years I've dug, wizard. Forty years, and the gold's never been this quiet.",
                    "Bigdig's under everything. Every world's got a cellar, and every cellar's got a door down to us, if you dig deep enough.",
                    "Somebody's going to have to go down there and find out what's gone wrong. Somebody tall. With spells.",
                },
            },
        };

        /// <summary>The kobold crew for this street, if it has one: the claim, the foreman (talkable) and the diggers.</summary>
        private static void KoboldCrew(Transform root, ZoneGrid g, ZoneDefinition z, QuestManager qm, WorldBuild wb, List<Vector2Int> ringCells, Vector2Int towerSite)
        {
            KoboldCrewInfo crew = null;
            foreach (var c in Crews) if (c.zone == z.zoneKey) { crew = c; break; }
            if (crew == null) return;
            Vector3 at, wall;
            if (!KoboldClaimSpot(g, z, ringCells, towerSite, out at, out wall))
            {
                Debug.Log("[WizardsVRC] No room for the kobold crew in " + z.displayName + " (every open spot is a duel ring or taken).");
                return;
            }
            Vector3 street = -wall;   // the claim faces out into the street, its back to the wall
            Quaternion facing = Quaternion.LookRotation(street, Vector3.up);
            var site = Child(root, "KoboldClaim_" + crew.foremanKey, at);
            site.localRotation = facing;
            KoboldClaim(site, crew, StableSeed(crew.foremanKey));

            // The foreman, at the front of the claim, facing the street (talk to him).
            int seed = StableSeed(crew.foremanKey);
            var npc = new ZoneNpc
            {
                symbol = "\u0001", key = crew.foremanKey, displayName = crew.foremanName, facing = 0f,
                robeColor = new Color(0.4f, 0.32f, 0.2f), hatColor = new Color(0.3f, 0.24f, 0.16f), defaultDialogue = crew.lines,
            };
            _npcLookOverrides[crew.foremanKey] = KoboldLook(seed, true);
            BuildNpc(root, g, z, npc, qm, wb, at + facing * new Vector3(-1.6f, 0f, 1.9f), street);

            // The diggers, at work round the hole and the cart (not talkative: busy).
            for (int k = 0; k < crew.diggers.Length; k++)
            {
                Vector3 local = k == 0 ? new Vector3(0.35f, 0f, -0.1f) : new Vector3(1.55f, 0f, 1.05f);
                float yaw = k == 0 ? 150f : -60f;
                float top, face;
                NpcModel(site, "KoboldDigger" + k, local, yaw, KoboldLook(seed + 7 * (k + 1), false, k), crew.diggers[k], "(busy digging)", out top, out face);
            }
        }

        /// <summary>
        /// A spot for a claim: on an open, plain street cell beside a wall (or anything you can't walk into), at least a
        /// duel ring's width from every ring and well clear of the tower, the way in and the street's NPCs and points.
        /// 'at' is the claim's middle (pushed towards the wall); 'wall' points from it to the wall.
        /// </summary>
        private static bool KoboldClaimSpot(ZoneGrid g, ZoneDefinition z, List<Vector2Int> ringCells, Vector2Int towerSite, out Vector3 at, out Vector3 wall)
        {
            at = Vector3.zero;
            wall = Vector3.forward;
            var avoid = new List<Vector3>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    char c = g.At(x, y);
                    if (c == 'S' || g.IsCustom(c) || ZoneGrid.IsRingDigit(c)) avoid.Add(g.Local(x, y));
                }
            float best = float.MinValue;
            bool found = false;
            for (int y = 1; y < g.height - 1; y++)
                for (int x = 1; x < g.width - 1; x++)
                {
                    char c = g.At(x, y);
                    if (c != '.' && c != ',') continue;
                    // A wall on one side, open street on the other three (so the claim never blocks a lane).
                    int walls = 0;
                    Vector2Int wd = Vector2Int.zero;
                    foreach (var d in ZoneGrid.Dirs)
                    {
                        if (!g.Walkable(x + d.x, y + d.y)) { walls++; wd = d; }
                        else if (g.Level(x + d.x, y + d.y) != g.Level(x, y)) walls += 5;   // not by stairs or drops
                    }
                    if (walls != 1) continue;
                    var back = new Vector2Int(x - wd.x, y - wd.y);
                    if (!g.Walkable(back.x, back.y) || !g.Walkable(back.x - wd.x, back.y - wd.y)) continue;   // room in front
                    Vector3 dir = ZoneGrid.ToLocalDir(wd);
                    Vector3 p = g.Local(x, y) + dir * (g.cell * 0.5f - 2.4f);
                    bool clear = true;
                    foreach (var r in ringCells)
                    {
                        Vector3 dv = g.Local(r) - p; dv.y = 0f;
                        if (dv.magnitude < DuelCircleRadius + 3.5f) { clear = false; break; }
                    }
                    if (!clear) continue;
                    if (towerSite.x >= 0)
                    {
                        Vector3 dv = g.Local(towerSite) - p; dv.y = 0f;
                        if (dv.magnitude < 22f) continue;
                    }
                    float nearest = float.MaxValue;
                    foreach (var a in avoid)
                    {
                        Vector3 dv = a - p; dv.y = 0f;
                        nearest = Mathf.Min(nearest, dv.magnitude);
                    }
                    if (nearest < 14f) continue;
                    // Prefer the middle of a long wall, then the spot furthest from everything else.
                    int run = 0;
                    Vector2Int along = new Vector2Int(wd.y, wd.x);
                    for (int s = -3; s <= 3; s++)
                        if (!g.Walkable(x + wd.x + along.x * s, y + wd.y + along.y * s)) run++;
                    float score = run * 10f + Mathf.Min(nearest, 60f) + (StableSeed(z.zoneKey + x + "," + y) % 100) * 0.01f;
                    if (score > best)
                    {
                        best = score;
                        at = p;
                        wall = dir;
                        found = true;
                    }
                }
            return found;
        }

        /// <summary>A kobold in work clothes: lamp helmet or kerchief, overalls or a vest, pick, pan or lantern.</summary>
        private static NpcLook KoboldLook(int seed, bool foreman, int job = 0)
        {
            Color[] scales =
            {
                new Color(0.62f, 0.38f, 0.22f), new Color(0.4f, 0.5f, 0.32f), new Color(0.36f, 0.44f, 0.66f), new Color(0.7f, 0.5f, 0.3f),
                new Color(0.5f, 0.3f, 0.36f), new Color(0.3f, 0.36f, 0.3f),
            };
            Color[] frills = { new Color(1f, 0.75f, 0.25f), new Color(0.95f, 0.4f, 0.2f), new Color(0.35f, 0.75f, 0.7f), new Color(0.75f, 0.5f, 0.95f) };
            Color[] cloth = { new Color(0.3f, 0.38f, 0.55f), new Color(0.48f, 0.34f, 0.2f), new Color(0.45f, 0.2f, 0.16f), new Color(0.32f, 0.42f, 0.26f) };
            Color skin = scales[seed % scales.Length];
            Color main = cloth[(seed / 7) % cloth.Length];
            NpcItem item = foreman ? NpcItem.Ledger : job == 0 ? NpcItem.Pickaxe : NpcItem.Pan;
            var L = Look(NpcBuild.Small, (seed / 3) % 3 == 0, skin, Color.Lerp(skin, Color.black, 0.3f), NpcHair.Bald, NpcBeard.None,
                foreman ? NpcOutfit.Vest : NpcOutfit.Overalls, main, new Color(0.85f, 0.8f, 0.68f), new Color(0.9f, 0.72f, 0.3f),
                foreman ? NpcHat.FlatCap : (seed % 2 == 0 ? NpcHat.MinerLamp : NpcHat.Kerchief), Color.Lerp(main, Color.black, 0.3f), item,
                foreman ? NpcItem.Lantern : NpcItem.None);
            L.species = NpcSpecies.Kobold;
            L.extra = frills[(seed / 11) % frills.Length];
            L.belly = Color.Lerp(skin, new Color(0.95f, 0.9f, 0.78f), 0.55f);
            L.eyes = new Color(1f, 0.8f, 0.25f);
            L.height = foreman ? 0.86f : 0.8f;
            L.goggles = !foreman && seed % 3 == 1;
            L.gloves = job == 0 && !foreman;
            L.idle = foreman ? 2 : 1;
            return L;
        }

        /// <summary>The claim's props in its own frame (+Z out into the street, the wall behind at -Z).</summary>
        private static void KoboldClaim(Transform site, KoboldCrewInfo crew, int seed)
        {
            var soil = PMat("KoboldSoil", new Color(0.36f, 0.26f, 0.18f), 2f);
            var dark = PMat("KoboldHole", new Color(0.07f, 0.05f, 0.04f), 1f);
            var wood = PMat("KoboldWood", new Color(0.5f, 0.34f, 0.2f), 1f);
            var iron = PMat("KoboldIron", new Color(0.28f, 0.27f, 0.28f), 1f);
            var gold = PMat("KoboldGold", new Color(1f, 0.8f, 0.25f), 1f, new Color(0.55f, 0.4f, 0.08f));
            var canvas = PMat("KoboldCanvas", new Color(0.8f, 0.72f, 0.55f), 1f);
            var glow = PMat("KoboldLantern", new Color(1f, 0.82f, 0.45f), 1f, new Color(1f, 0.7f, 0.3f));
            var rng = new System.Random(seed);
            float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // The hole, ringed with spoil, and a ladder poking out of it.
            Shape(site, "DigHole", CylMesh(), new Vector3(0f, 0.025f, 0f), new Vector3(2.0f, 0.02f, 2.0f), dark, false, Quaternion.identity);
            for (int k = 0; k < 9; k++)
            {
                float a = k * 40f * Mathf.Deg2Rad;
                Shape(site, "Spoil", BlobMesh(), new Vector3(Mathf.Sin(a) * 1.15f, 0.08f, Mathf.Cos(a) * 1.15f), new Vector3(Rr(0.5f, 0.8f), Rr(0.18f, 0.3f), Rr(0.5f, 0.8f)), soil, false, Quaternion.Euler(0, Rr(0, 360), 0));
            }
            foreach (float x in new[] { -0.22f, 0.22f })
                Deco(site, PrimitiveType.Cube, "HoleLadder", new Vector3(-0.3f + x, 0.55f, -0.35f), new Vector3(0.05f, 1.3f, 0.05f), wood, Quaternion.Euler(-18f, 0, 0), false);
            for (int r = 0; r < 3; r++)
                Deco(site, PrimitiveType.Cube, "HoleLadderRung", new Vector3(-0.3f, 0.3f + r * 0.35f, -0.35f + -0.1f - r * 0.11f), new Vector3(0.44f, 0.04f, 0.04f), wood, Quaternion.Euler(-18f, 0, 0), false);

            // The spoil heap against the wall.
            Shape(site, "DirtHeap", BlobMesh(), new Vector3(-1.9f, 0.35f, -1.3f), new Vector3(2.0f, 1.0f, 1.5f), soil, true, Quaternion.Euler(0, 20f, 0));
            Shape(site, "DirtHeap", BlobMesh(), new Vector3(-1.2f, 0.25f, -1.7f), new Vector3(1.3f, 0.7f, 1.0f), soil, false, Quaternion.Euler(0, -15f, 0));
            Deco(site, PrimitiveType.Cube, "Shovel", new Vector3(-1.5f, 0.85f, -1.1f), new Vector3(0.05f, 1.2f, 0.05f), wood, Quaternion.Euler(10f, 0, -20f), false);
            Deco(site, PrimitiveType.Cube, "ShovelBlade", new Vector3(-1.32f, 0.33f, -1.2f), new Vector3(0.26f, 0.32f, 0.03f), iron, Quaternion.Euler(10f, 0, -20f), false);

            // The ore cart, gold glinting in it.
            var cart = Child(site, "OreCart", new Vector3(2.1f, 0f, -0.9f));
            cart.localRotation = Quaternion.Euler(0, 12f, 0);
            Block(cart, "CartBody", new Vector3(0, 0.62f, 0), new Vector3(1.3f, 0.6f, 0.85f), wood);
            Deco(cart, PrimitiveType.Cube, "CartBand", new Vector3(0, 0.85f, 0), new Vector3(1.34f, 0.06f, 0.89f), iron, Quaternion.identity, false);
            foreach (float x in new[] { -0.45f, 0.45f })
                foreach (float zz in new[] { -0.44f, 0.44f })
                    Shape(cart, "CartWheel", CylMesh(), new Vector3(x, 0.22f, zz), new Vector3(0.4f, 0.03f, 0.4f), iron, false, Quaternion.Euler(90f, 0, 0));
            Shape(cart, "CartLoad", BlobMesh(), new Vector3(0, 0.92f, 0), new Vector3(1.15f, 0.3f, 0.72f), soil, false, Quaternion.identity);
            for (int k = 0; k < 7; k++)
                Shape(cart, "GoldNugget", GemMesh(), new Vector3(Rr(-0.45f, 0.45f), 1.02f + Rr(0f, 0.05f), Rr(-0.25f, 0.25f)), Vector3.one * Rr(0.07f, 0.13f), gold, false, Quaternion.Euler(Rr(0, 360), Rr(0, 360), 0));
            Deco(cart, PrimitiveType.Cube, "Pickaxe", new Vector3(0.75f, 0.6f, 0.2f), new Vector3(0.05f, 1.0f, 0.05f), wood, Quaternion.Euler(0, 0, 18f), false);
            Deco(cart, PrimitiveType.Cube, "PickaxeHead", new Vector3(0.6f, 1.08f, 0.2f), new Vector3(0.6f, 0.06f, 0.06f), iron, Quaternion.Euler(0, 0, 18f), false);

            // A crate with a gold pan on it, another crate, a sack.
            Block(site, "Crate", new Vector3(1.05f, 0.3f, 1.6f), new Vector3(0.6f, 0.6f, 0.6f), wood);
            Shape(site, "GoldPan", CylMesh(), new Vector3(1.05f, 0.63f, 1.6f), new Vector3(0.5f, 0.025f, 0.5f), iron, false, Quaternion.identity);
            for (int k = 0; k < 3; k++)
                Shape(site, "PanNugget", GemMesh(), new Vector3(1.05f + Rr(-0.12f, 0.12f), 0.67f, 1.6f + Rr(-0.12f, 0.12f)), Vector3.one * 0.05f, gold, false, Quaternion.Euler(0, Rr(0, 360), 0));
            Block(site, "Crate", new Vector3(2.6f, 0.25f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), wood);
            Shape(site, "Sack", BlobMesh(), new Vector3(2.55f, 0.75f, 0.5f), new Vector3(0.45f, 0.55f, 0.4f), canvas, false, Quaternion.identity);

            // A lantern on a post, and the company's sign facing the street.
            Deco(site, PrimitiveType.Cube, "LanternPost", new Vector3(-2.6f, 1.1f, 0.6f), new Vector3(0.1f, 2.2f, 0.1f), wood, Quaternion.identity, true);
            Deco(site, PrimitiveType.Cube, "LanternArm", new Vector3(-2.38f, 2.12f, 0.6f), new Vector3(0.5f, 0.06f, 0.06f), wood, Quaternion.identity, false);
            Shape(site, "Lantern", BlobMesh(), new Vector3(-2.18f, 1.88f, 0.6f), new Vector3(0.2f, 0.26f, 0.2f), glow, false, Quaternion.identity);
            Deco(site, PrimitiveType.Cube, "SignPost", new Vector3(0.2f, 0.75f, -1.9f), new Vector3(0.1f, 1.5f, 0.1f), wood, Quaternion.identity, false);
            Deco(site, PrimitiveType.Cube, "SignBoard", new Vector3(0.2f, 1.5f, -1.86f), new Vector3(1.7f, 0.7f, 0.06f), wood, Quaternion.identity, false);
            // (A label is read looking along its +Z: from the street, towards the wall.)
            var label = WorldText(site, "ClaimSign", new Vector3(0.2f, 1.5f, -1.82f), Quaternion.Euler(0f, 180f, 0f), new Vector2(820, 320), 0.002f,
                "BIGDIG PROSPECTING CO.\n" + crew.claim + "\nKeep out of the hole", 52);
            label.color = new Color(1f, 0.9f, 0.6f);
            label.alignment = UnityEngine.TextAnchor.MiddleCenter;
        }
    }
}
