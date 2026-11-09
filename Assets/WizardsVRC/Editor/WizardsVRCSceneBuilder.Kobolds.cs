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
    /// They talk like fast-talking, money-mad hustlers (every visitor's a customer, everything costs extra, blasting
    /// rates on request), and what they say carries hints for World 3: the old gold of Bigdig "sings" and has gone quiet; the elders say it and the
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
                    "Customer! No, wait, wizard. Same thing, if you've got coin! Grizzik Tallowlamp, Bigdig Prospecting, at your service, for a reasonable fee.",
                    "Gold, pal! The old stuff, hums when you hold it to your ear. Back home the seams have gone dead quiet, and quiet gold don't pay.",
                    "Elders say the singing gold and your fancy crystal came off the same star. Me? I say a crystal that size could fund a whole new tunnel. Just saying!",
                    "Now beat it, I'm on the clock! Every second you stand there is a nugget I ain't digging!",
                },
            },
            new KoboldCrewInfo
            {
                zone = "TidewatchDocks", foremanKey = "KoboldNell", foremanName = "Prospector Nell Brineknuckle", claim = "Claim No. 12",
                diggers = new[] { "Digger Murk" },
                lines = new[]
                {
                    "Watch the hole, sweetie! Fall in, you're paying for the rope. Rope's extra.",
                    "We follow the hum, and today the hum says: under the fish market. Smells awful. Smells like profit!",
                    "Old kobold saying: when the deep gold goes silent, something's drinking the light from the top down. Bad for business. Very bad for business.",
                    "Shipped six crates home this month! Six! And the Deepwarden writes back 'not enough'. Not enough! I oughta charge him postage.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "KilnStreet", foremanKey = "KoboldPib", foremanName = "Overseer Pib Cindertooth", claim = "Claim No. 9",
                diggers = new[] { "Digger Tamm", "Digger Ozzle" },
                lines = new[]
                {
                    "Heh heh! Stand back, I'm about to try something with a very short fuse. Kidding! Mostly. Pib Cindertooth, overseer, demolitions, and fine accounting.",
                    "Kilns run hot, ground runs hotter, and hot ground means gold. Also means my tail's smoking. Small price!",
                    "That glowing dome over your isles? Lovely work. Gran says Bigdig had one too, underground. Then it went out. Nobody got a refund.",
                    "Find gold that glows in the dark, you bring it straight to a kobold, got it? Don't spend it. I'll give you a great price. Terrific price!",
                },
            },
            new KoboldCrewInfo
            {
                zone = "ThornhollowGrove", foremanKey = "KoboldWix", foremanName = "Digmistress Wix Mossjaw", claim = "Claim No. 21",
                diggers = new[] { "Digger Fenna" },
                lines = new[]
                {
                    "Ooh, a big one with a wand! You buying, selling, or just blocking my light? Blocking costs extra.",
                    "Roots everywhere! Gold hides under roots, everybody knows that. Roots got good taste.",
                    "Bigdig's way down, friend. Past the bottom of the bottom. You'll get there one day. Bring a lamp. I rent lamps!",
                    "Somebody's buying up every scrap of singing gold in the worlds, and it ain't us. Undercutting us! Can you believe the nerve?",
                },
            },
            // ---------------- World 2 (Dustwind)
            new KoboldCrewInfo
            {
                zone = "TumbleweedRow", foremanKey = "KoboldKrel", foremanName = "Boss Krel Dustgrin", claim = "Claim No. 31",
                diggers = new[] { "Digger Rasp", "Digger Ninn" },
                lines = new[]
                {
                    "Hey hey, big spender! Krel Dustgrin, boss of this here claim. Want a share? Shares are cheap today. Today only!",
                    "Dustwind gold's got grit in it. Grit, and a pinch of starlight if you're lucky. Grit I can sell. Starlight I can sell for more.",
                    "Lizard folk keep their gold in banks. Kobolds keep ours in pockets. Faster withdrawals, no fees!",
                    "Word from Bigdig: the Deep Seam went dark three more tunnels this week. Nobody's saying why. Somebody's gonna pay for this, and it ain't gonna be me!",
                },
            },
            new KoboldCrewInfo
            {
                zone = "CoyoteGulch", foremanKey = "KoboldRatchet", foremanName = "Prospector Ratchet Pebblewhisk", claim = "Claim No. 34",
                diggers = new[] { "Digger Bix" },
                lines = new[]
                {
                    "Easy, easy, don't touch the cart! That's company gold. Touching's a silver piece. Looking's free. For now.",
                    "Gulch sang to me last night. Gold sings, if you're a kobold. Mostly it sings 'dig here, Ratchet, you genius'.",
                    "Tell the lizard folk we ain't stealing. We're borrowing! From the ground! Long-term loan, very generous terms.",
                    "Elders sent us every which way, isles to desert, hunting gold that still sings. Find some, you know where I am. Same hole. Bigger, by Tuesday.",
                },
            },
            new KoboldCrewInfo
            {
                zone = "CopperbottomMine", foremanKey = "KoboldMog", foremanName = "Chief Digger Mog Brasswhisker", claim = "Claim No. 40",
                diggers = new[] { "Digger Gub", "Digger Tilly" },
                lines = new[]
                {
                    "Ha! A wizard! Bet you've got pockets. Everybody's got pockets. Mog Brasswhisker, chief digger, ask me about our blasting rates!",
                    "Copper, copper, copper. Copper's for pots. We want the gold behind the copper, and the gold behind that!",
                    "Down past the bottom shaft there's a door that ain't on any map. Kobold-made, old as dirt. Locked from the other side. Who locks a door from the other side? Bad for business, that's who.",
                    "When the mine goes quiet you can hear it: tapping, deep down. Not us. We stopped tapping an hour ago. I'm not paid enough to be scared. Pay me more and I'll be terrified!",
                },
            },
            new KoboldCrewInfo
            {
                zone = "ThunderMesa", foremanKey = "KoboldZizi", foremanName = "Surveyor Zizi Sparkfang", claim = "Claim No. 38",
                diggers = new[] { "Digger Wort" },
                lines = new[]
                {
                    "Whoa, stand clear! Lightning's due any minute and I've got money riding on where it lands. Zizi Sparkfang, surveyor, bookie, genius.",
                    "Lightning finds gold, see? Strikes where the ground's full of it. So we stand here and wait. Bravely! Profitably!",
                    "Storms out here hum the same note as the gold back home. Same note as your crystal, too. That's either a coincidence or an investment opportunity.",
                    "If the deep gold ever stops humming altogether... well. Then I'd sell my shares. Quick. Hey, you wanna buy some shares?",
                },
            },
            new KoboldCrewInfo
            {
                zone = "LonesomeBend", foremanKey = "KoboldFennick", foremanName = "Old Digger Fennick Rustclaw", claim = "Claim No. 1",
                diggers = new[] { "Digger Quill", "Digger Sprat" },
                lines = new[]
                {
                    "Heh. Another tall one. Fennick Rustclaw. Forty years digging, and I still charge by the hour, so talk fast.",
                    "Forty years, and the gold's never been this quiet. Quiet gold's cheap gold, and cheap gold's no gold at all.",
                    "Bigdig's under everything, kid. Every world's got a cellar, every cellar's got a door down to us, if you dig deep enough and don't mind the paperwork.",
                    "Somebody's gonna have to go down there and see what's gone wrong. Somebody tall. With spells. I'd go myself, but who'd mind the gold?",
                },
            },
        };

        /// <summary>What a digger says between swings (two of these each).</summary>
        private static readonly string[] DiggerBarks =
        {
            "Yeah, yeah, what? I'm digging here! Digging's money!",
            "You want a hole? I can dig you a hole. Ten silver. Fifteen if you want it deep.",
            "Don't tell the boss I stopped. Every second's a nugget!",
            "Stand back, big feet! You're on company dirt.",
            "Gold! Gold! ...No, rock. Rock again. Rock's not paying!",
            "Got a light? Not for a pipe. For a fuse. Never mind!",
            "We're not lost, we're prospecting. Totally different. One pays.",
            "Hey, you look rich. You rich? Wanna invest in a hole?",
            "Boss says if I find singing gold I get a bonus. Boss says that a lot.",
            "Shh! Hear that hum? ...Nope, that was my stomach. Lunch costs extra.",
            "Bigdig's a long way down, pal. Long way. Charge by the foot.",
            "Ow, my tail! Who left a pick there? Oh. Me. Billing myself.",
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

            // The diggers, at work round the hole and the cart: talk to one and you get a word or two between swings.
            for (int k = 0; k < crew.diggers.Length; k++)
            {
                Vector3 local = k == 0 ? new Vector3(0.35f, 0f, -0.1f) : new Vector3(1.55f, 0f, 1.05f);
                Vector3 front = facing * (k == 0 ? new Vector3(-0.5f, 0f, -0.86f) : new Vector3(0.87f, 0f, 0.5f));
                string key = crew.foremanKey + "Digger" + k;
                int bark = StableSeed(key);
                var digger = new ZoneNpc
                {
                    symbol = "\u0001", key = key, displayName = crew.diggers[k], facing = 0f,
                    robeColor = new Color(0.4f, 0.32f, 0.2f), hatColor = new Color(0.3f, 0.24f, 0.16f),
                    defaultDialogue = new[] { DiggerBarks[bark % DiggerBarks.Length], DiggerBarks[(bark / 13 + 5) % DiggerBarks.Length] },
                };
                _npcLookOverrides[key] = KoboldLook(seed + 7 * (k + 1), false, k);
                BuildNpc(root, g, z, digger, qm, wb, at + facing * local, front);
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
