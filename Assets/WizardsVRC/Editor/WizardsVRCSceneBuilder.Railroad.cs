using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Dustwind's four mechanics, in the order they were asked for: the railroad (Archrock's depot and a whistle stop by
    /// each street's way in, the passenger car you ride in), the bounty board, High Noon at the town's duelling circle,
    /// and dust storms on the streets. The zones record their depots, boards and circles as they're built; WireDustwind
    /// (called from WireExtras) makes the systems and wires everything once the player state and notices exist.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private class DepotRecord
        {
            public int index;
            public string name;
            public Transform arrival;
            public List<GameObject> clicks = new List<GameObject>();
            public List<GameObject> triggers = new List<GameObject>();
        }

        private static readonly Dictionary<int, DepotRecord> _depots = new Dictionary<int, DepotRecord>();
        private static readonly List<GameObject> _bountyClicks = new List<GameObject>();
        private static readonly List<BattleClient> _highNoonClients = new List<BattleClient>();
        private static readonly List<Text> _highNoonSigns = new List<Text>();
        private static readonly List<GameObject> _highNoonVisuals = new List<GameObject>();
        private static readonly List<Vector2> _stormBands = new List<Vector2>();
        /// <summary>The railroad's and the bounty board's panels (InteractMuter keeps them clickable; they draw over the world).</summary>
        private static GameObject RailroadPanel, BountyPanel;
        private static readonly Vector3 RideOrigin = new Vector3(-3200f, -600f, -3200f);

        private static void ClearDustwindRecords()
        {
            _depots.Clear();
            _bountyClicks.Clear();
            _highNoonClients.Clear();
            _highNoonSigns.Clear();
            _highNoonVisuals.Clear();
            _stormBands.Clear();
            RailroadPanel = BountyPanel = null;
        }

        /// <summary>The railroad stop index for a zone: its place in its world's zone list (the town first), or -1.</summary>
        private static int StopIndex(ZoneDefinition z, WorldDataStore.Data data)
        {
            if (z == null || z.world < 2 || data == null) return -1;
            foreach (var w in data.worlds)
            {
                if (w == null || !w.includeInBuild || w.order != z.world) continue;
                int i = w.zones.IndexOf(z.zoneKey);
                return i >= 0 && i < Railroad.MAX_STOPS ? i : -1;
            }
            return -1;
        }

        private static DepotRecord Depot(int index, string name)
        {
            DepotRecord d;
            if (!_depots.TryGetValue(index, out d)) { d = new DepotRecord { index = index, name = name }; _depots[index] = d; }
            return d;
        }

        // ================================================================== landmarks (built with the zones)

        /// <summary>The TrainDepot landmark: a boarded platform, a ticket booth with the station's sign, a bench and a lamp.</summary>
        private static void TrainDepotLandmark(Transform root, ZoneGrid g, ZoneDefinition z, ZoneLandmark l, WorldDataStore.Data data)
        {
            var cells = g.Find(l.Symbol);
            if (cells.Count == 0) return;
            int index = StopIndex(z, data);
            if (index < 0) { Debug.LogWarning("[WizardsVRC World] " + z.displayName + ": its Train Depot needs the zone in a world (World 2 on) list; not built."); return; }
            Vector3 p = g.Local(cells[0]);
            Vector3 front = OpenFront(g, cells[0]);
            Quaternion face = Quaternion.LookRotation(front);
            var t = Child(root, "TrainDepot", p);
            t.localRotation = face;
            var timber = TimberMat();
            var planks = PsxTexMat("SaloonFloor", new Color(0.66f, 0.46f, 0.3f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 2.4f);
            var paint = PsxTexMat("DepotPaint", new Color(0.62f, 0.66f, 0.55f), TileTexture(StreetTexDir + "/Clapboard.png"), 2f);
            var roof = PsxTexMat("ShopShingles", new Color(0.45f, 0.3f, 0.22f), TileTexture(StreetTexDir + "/Shingles.png"), 2.6f);
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            float cs = g.cell;
            Deco(t, PrimitiveType.Cube, "Platform", new Vector3(0, 0.15f, -cs * 0.15f), new Vector3(cs * 0.95f, 0.3f, cs * 0.6f), planks, Quaternion.identity, false);
            // The ticket booth at the back of the platform, its window facing the road.
            var booth = Deco(t, PrimitiveType.Cube, "TicketBooth", new Vector3(0, 1.6f, -cs * 0.35f), new Vector3(2.4f, 3f, 1.6f), paint, Quaternion.identity, true);
            Deco(t, PrimitiveType.Cube, "BoothRoof", new Vector3(0, 3.25f, -cs * 0.33f), new Vector3(2.9f, 0.25f, 2.1f), roof, Quaternion.Euler(-8f, 0, 0), false);
            Deco(t, PrimitiveType.Cube, "TicketWindow", new Vector3(0, 1.8f, -cs * 0.35f + 0.82f), new Vector3(1.2f, 0.8f, 0.05f), glow, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "TicketLedge", new Vector3(0, 1.35f, -cs * 0.35f + 0.95f), new Vector3(1.4f, 0.08f, 0.35f), timber, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cube, "Bench", new Vector3(cs * 0.3f, 0.55f, -cs * 0.2f), new Vector3(1.8f, 0.12f, 0.5f), timber, Quaternion.identity, false);
            Deco(t, PrimitiveType.Cylinder, "LampPost", new Vector3(-cs * 0.38f, 1.6f, -cs * 0.1f), new Vector3(0.14f, 1.6f, 0.14f), timber, Quaternion.identity, false);
            Shape(t, "Lamp", GemMesh(), new Vector3(-cs * 0.38f, 3.3f, -cs * 0.1f), new Vector3(0.35f, 0.5f, 0.35f), glow, false, Quaternion.identity);
            var sign = WorldText(t, "DepotSign", new Vector3(0, 4.2f, -cs * 0.35f + 0.3f), Quaternion.Euler(0, 180f, 0), new Vector2(1200, 220), 0.004f,
                (string.IsNullOrEmpty(l.label) ? z.displayName : l.label).ToUpperInvariant() + " DEPOT\n(click the ticket window)", 52);
            sign.color = new Color(1f, 0.9f, 0.6f);
            DressSign(sign, 1100f, 210f);
            Transform arrival = Child(root, "DepotArrival", p + front * (cs * 0.2f) + new Vector3(0, 0.1f, 0));
            arrival.localRotation = Quaternion.LookRotation(-front);
            var d = Depot(index, string.IsNullOrEmpty(l.label) ? z.displayName : l.label);
            d.arrival = arrival;
            d.clicks.Add(booth);
            var trig = Child(t, "DepotTrigger", new Vector3(0, 1.5f, 0));
            trig.gameObject.layer = 2;
            var box = trig.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(cs * 1.5f, 3f, cs * 1.5f);
            d.triggers.Add(trig.gameObject);
        }

        /// <summary>
        /// A whistle stop by a Dustwind street's way in (its S): a signpost with the stop's name. Walking in reaches it;
        /// clicking it rides you back to town (or anywhere else you've reached).
        /// </summary>
        private static void WhistleStop(Transform root, ZoneGrid g, ZoneDefinition z, Vector2Int start, Vector3 openDir, Transform spawn, WorldDataStore.Data data)
        {
            int index = StopIndex(z, data);
            if (index <= 0) return;
            float cs = g.cell;
            Vector3 side = Vector3.Cross(Vector3.up, openDir);
            Vector3 at = g.Local(start) + openDir * (cs * 0.9f) + side * (cs * 0.38f);
            var t = Child(root, "WhistleStop", at);
            t.localRotation = Quaternion.LookRotation(-side);
            var timber = TimberMat();
            Deco(t, PrimitiveType.Cylinder, "StopPost", new Vector3(0, 1.6f, 0), new Vector3(0.2f, 1.6f, 0.2f), timber, Quaternion.identity, false);
            var board = Deco(t, PrimitiveType.Cube, "StopBoard", new Vector3(0, 2.6f, 0), new Vector3(2.2f, 0.9f, 0.12f), PMat("StopWhite", new Color(0.9f, 0.88f, 0.8f), 1f), Quaternion.identity, true);
            Deco(t, PrimitiveType.Cube, "StopCross", new Vector3(0, 2.6f, 0.08f), new Vector3(2.4f, 0.15f, 0.06f), PMat("CoachRed", new Color(0.62f, 0.14f, 0.12f), 1f), Quaternion.Euler(0, 0, 20f), false);
            var sign = WorldText(t, "StopSign", new Vector3(0, 3.6f, 0.1f), Quaternion.Euler(0, 180f, 0), new Vector2(1100, 200), 0.0035f, "Whistle Stop\n(click to ride the train)", 48);
            sign.color = new Color(1f, 0.92f, 0.65f);
            DressSign(sign, 1000f, 190f);
            var d = Depot(index, z.displayName);
            d.arrival = spawn;
            d.clicks.Add(board);
            var trig = Child(root, "WhistleStopTrigger", g.Local(start) + openDir * (cs * 0.5f) + new Vector3(0, 1.5f, 0));
            trig.gameObject.layer = 2;
            var box = trig.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(cs * 1.2f, 3f, cs * 1.2f);
            d.triggers.Add(trig.gameObject);
        }

        /// <summary>The BountyBoard landmark: a plank board on two posts under a little roof, three wanted posters on it.</summary>
        private static void BountyBoardLandmark(Transform root, ZoneGrid g, ZoneLandmark l)
        {
            var cells = g.Find(l.Symbol);
            if (cells.Count == 0) return;
            Vector3 p = g.Local(cells[0]);
            Vector3 front = OpenFront(g, cells[0]);
            var t = Child(root, "BountyBoard", p);
            t.localRotation = Quaternion.LookRotation(front);
            var timber = TimberMat();
            var paper = PMat("WantedPaper", new Color(0.92f, 0.85f, 0.68f), 1f);
            var ink = PMat("WantedInk", new Color(0.25f, 0.15f, 0.08f), 1f);
            foreach (float s in new[] { -1f, 1f })
                Deco(t, PrimitiveType.Cube, "BoardPost", new Vector3(s * 1.7f, 1.4f, 0), new Vector3(0.22f, 2.8f, 0.22f), timber, Quaternion.identity, true);
            var board = Deco(t, PrimitiveType.Cube, "Board", new Vector3(0, 1.75f, 0), new Vector3(3.6f, 1.9f, 0.12f), timber, Quaternion.identity, true);
            Deco(t, PrimitiveType.Cube, "BoardRoof", new Vector3(0, 2.95f, 0.1f), new Vector3(4f, 0.12f, 0.8f), timber, Quaternion.Euler(-15f, 0, 0), false);
            for (int k = 0; k < 3; k++)
            {
                float x = (k - 1) * 1.15f;
                Deco(t, PrimitiveType.Cube, "Poster", new Vector3(x, 1.7f, 0.07f), new Vector3(0.95f, 1.3f, 0.02f), paper, Quaternion.Euler(0, 0, (k - 1) * 3f), false);
                Deco(t, PrimitiveType.Cube, "PosterFace", new Vector3(x, 1.75f, 0.085f), new Vector3(0.55f, 0.55f, 0.01f), ink, Quaternion.Euler(0, 0, (k - 1) * 3f), false);
            }
            var sign = WorldText(t, "BountySign", new Vector3(0, 3.5f, 0.2f), Quaternion.Euler(0, 180f, 0), new Vector2(900, 200), 0.004f,
                (string.IsNullOrEmpty(l.label) ? "Wanted" : l.label).ToUpperInvariant() + "\n(click the board)", 52);
            sign.color = new Color(1f, 0.88f, 0.55f);
            DressSign(sign, 850f, 190f);
            _bountyClicks.Add(board);
        }

        /// <summary>A town's duelling circles run High Noon: a sign over each, and the duels it starts pay the noon bonus.</summary>
        private static void HighNoonCircles(Transform root, ZoneGrid g, ZoneDefinition z, List<BattleClient> encounters, int before)
        {
            for (int i = before; i < encounters.Count; i++)
                if (encounters[i] != null && encounters[i].manager != null && encounters[i].manager.pvpMode) _highNoonClients.Add(encounters[i]);
            if (z.landmarks == null) return;
            foreach (var l in z.landmarks)
            {
                if (l == null || l.kind != LandmarkKind.DuelCircle) continue;
                var fp = g.Footprint(l.Symbol);
                if (fp.width == 0) continue;
                Vector3 c = RectCenter(g, fp);
                var holder = Child(root, "HighNoon_" + l.symbol, c);
                // A tall sign on a post at the circle's north edge, readable from the square.
                Vector3 post = new Vector3(0, 0, DuelCircleRadius + 2.2f);
                Deco(holder, PrimitiveType.Cylinder, "NoonPost", post + new Vector3(0, 2.6f, 0), new Vector3(0.25f, 2.6f, 0.25f), TimberMat(), Quaternion.identity, true);
                var sign = WorldText(holder, "NoonSign", post + new Vector3(0, 6.2f, 0), Quaternion.identity, new Vector2(1400, 260), 0.005f, "High Noon Circle", 54);
                sign.color = new Color(1f, 0.9f, 0.55f);
                DressSign(sign, 1300f, 250f);
                _highNoonSigns.Add(sign);
                // The noon sun: a blazing disc over the circle, only while it's High Noon.
                var sun = Shape(holder, "NoonSun", BlobMesh(), new Vector3(0, 16f, 0), new Vector3(3f, 3f, 3f), PMat("NoonSun", new Color(1f, 0.9f, 0.5f), 1f, new Color(1f, 0.85f, 0.35f) * 1.6f), false, Quaternion.identity);
                sun.isStatic = false;
                sun.SetActive(false);
                _highNoonVisuals.Add(sun);
            }
        }

        // ================================================================== the systems

        private static void WireDustwind(Transform systems, PlayerState playerState, NotificationCenter notices, UiAudio uiAudio, CardDatabase cardDb, List<BattleClient> encounters)
        {
            // ---- the railroad
            if (_depots.Count > 0 && _depots.ContainsKey(0))
            {
                var rr = UdonSharpUndo.AddComponent<Railroad>(Child(systems, "Railroad", Vector3.zero).gameObject);
                rr.playerState = playerState;
                rr.notifications = notices;
                rr.uiAudio = uiAudio;
                int n = 0;
                foreach (var k in _depots.Keys) n = Mathf.Max(n, k + 1);
                var names = new string[n];
                var arrivals = new Transform[n];
                for (int i = 0; i < n; i++)
                {
                    DepotRecord d;
                    names[i] = _depots.TryGetValue(i, out d) ? d.name : "(no stop)";
                    arrivals[i] = d != null ? d.arrival : null;
                }
                rr.stopNames = names;
                rr.stopArrivals = arrivals;
                rr.rideSpot = BuildRideCar();
                BuildRailroadPanel(systems, rr, n);
                foreach (var d in _depots.Values)
                {
                    foreach (var go in d.clicks)
                    {
                        var stop = UdonSharpUndo.AddComponent<DepotStop>(go);
                        stop.railroad = rr;
                        stop.index = d.index;
                        SetInteractText(stop, d.index == 0 ? "Buy a ticket" : "Ride the train");
                        ExtraBehaviours.Add(stop);
                    }
                    foreach (var go in d.triggers)
                    {
                        var stop = UdonSharpUndo.AddComponent<DepotStop>(go);
                        stop.railroad = rr;
                        stop.index = d.index;
                        ExtraBehaviours.Add(stop);
                    }
                }
                ExtraBehaviours.Add(rr);
                Debug.Log("[WizardsVRC World] Railroad: " + string.Join(", ", names) + ".");
            }

            // ---- the bounty board
            if (_bountyClicks.Count > 0)
            {
                var bb = UdonSharpUndo.AddComponent<BountyBoard>(Child(systems, "BountyBoard", Vector3.zero).gameObject);
                bb.playerState = playerState;
                bb.notifications = notices;
                bb.uiAudio = uiAudio;
                bb.cardDatabase = cardDb;
                FillBounties(bb);
                BuildBountyPanel(systems, bb);
                foreach (var go in _bountyClicks)
                {
                    var relay = UdonSharpUndo.AddComponent<EventRelay>(go);
                    relay.target = bb;
                    relay.eventName = nameof(BountyBoard.Open);
                    SetInteractText(relay, "Read the wanted posters");
                    ExtraBehaviours.Add(relay);
                }
                foreach (var ec in encounters) if (ec != null && (ec.manager == null || !ec.manager.pvpMode)) ec.bounties = bb;
                ExtraBehaviours.Add(bb);
            }

            // ---- High Noon
            if (_highNoonClients.Count > 0)
            {
                var hn = UdonSharpUndo.AddComponent<HighNoon>(Child(systems, "HighNoon", Vector3.zero).gameObject);
                hn.playerState = playerState;
                hn.notifications = notices;
                hn.label = _highNoonSigns.Count > 0 ? _highNoonSigns[0] : null;
                hn.noonVisual = _highNoonVisuals.Count > 0 ? _highNoonVisuals[0] : null;
                foreach (var ec in _highNoonClients) ec.highNoon = hn;
                ExtraBehaviours.Add(hn);
            }

            // ---- dust storms
            if (_stormBands.Count > 0)
            {
                var ds = UdonSharpUndo.AddComponent<DustStorm>(Child(systems, "DustStorm", Vector3.zero).gameObject);
                ds.minY = new float[_stormBands.Count];
                ds.maxY = new float[_stormBands.Count];
                for (int i = 0; i < _stormBands.Count; i++) { ds.minY[i] = _stormBands[i].x; ds.maxY[i] = _stormBands[i].y; }
                var sand = Motes(ds.transform, "BlowingSand", Vector3.zero, new Vector3(36f, 6f, 36f), new Color(0.9f, 0.68f, 0.42f), 160f, 1.2f, 2.2f,
                    new Vector2(0.15f, 0.5f), new Vector3(9f, 0.4f, 3f), new Vector3(2f, 0.6f, 2f), 0f);
                var main = sand.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                sand.gameObject.SetActive(false);
                ds.sand = sand.gameObject;
                ExtraBehaviours.Add(ds);
            }
        }

        /// <summary>Bounties: one monster each, from the town streets out to Lonesome Bend (the board shows those for your level).</summary>
        private static void FillBounties(BountyBoard bb)
        {
            object[,] list =
            {
                { "Tumbleweed Trouble", 28, 8, 140, 2, 17 }, { "Rustler Roundup", 29, 8, 150, 2, 17 }, { "Coyote Pack", 31, 8, 160, 2, 17 },
                { "A Prickly Problem", 32, 6, 170, 2, 17 }, { "Sting Operation", 33, 5, 200, 3, 17 }, { "Knock It Off", 35, 8, 180, 2, 17 },
                { "Empty Armor", 36, 8, 200, 3, 18 }, { "Rock Solid", 37, 5, 230, 3, 18 }, { "Rail Guards", 39, 8, 230, 3, 19 },
                { "Corps Engineers", 40, 8, 240, 3, 19 }, { "Boiling Point", 41, 5, 260, 4, 19 }, { "Feathers and Thunder", 44, 8, 260, 3, 20 },
                { "Stone Cold", 45, 6, 280, 4, 21 }, { "Hailstorm", 46, 5, 300, 4, 21 }, { "Restless Nights", 48, 8, 300, 4, 22 },
                { "The Jackrabbit Gang", 49, 5, 340, 5, 23 }, { "Drill Sergeants", 50, 6, 330, 4, 23 },
            };
            int n = list.GetLength(0);
            bb.titles = new string[n];
            bb.monsters = new int[n];
            bb.counts = new int[n];
            bb.golds = new int[n];
            bb.shards = new int[n];
            bb.levels = new int[n];
            for (int i = 0; i < n; i++)
            {
                bb.titles[i] = (string)list[i, 0];
                bb.monsters[i] = (int)list[i, 1];
                bb.counts[i] = (int)list[i, 2];
                bb.golds[i] = (int)list[i, 3];
                bb.shards[i] = (int)list[i, 4];
                bb.levels[i] = (int)list[i, 5];
            }
        }

        /// <summary>The timetable: a parchment panel with a row (a button) per stop, and Close.</summary>
        private static void BuildRailroadPanel(Transform systems, Railroad rr, int stops)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(rr);
            var go = new GameObject("RailroadPanel", typeof(RectTransform));
            go.transform.SetParent(systems, false);
            MakeWorldCanvas(go, new Vector2(900, 1000), Vector3.zero, 0.0006f);
            var rt = (RectTransform)go.transform;
            Panel(rt, "Background", Vector2.zero, new Vector2(900, 1000), Parchment);
            rr.panelRoot = go;
            rr.titleLabel = InkLabel(rt, "Title", new Vector2(0, 420), new Vector2(840, 120), "", 40, TextAnchor.MiddleCenter);
            rr.statusLabel = InkLabel(rt, "Status", new Vector2(0, -360), new Vector2(840, 70), "", 24, TextAnchor.MiddleCenter);
            var rows = new GameObject[Railroad.MAX_STOPS];
            var labels = new Text[Railroad.MAX_STOPS];
            for (int i = 0; i < Railroad.MAX_STOPS; i++)
            {
                if (i >= stops) break;
                var b = Button(rt, "Stop" + i, new Vector2(0, 300 - i * 82), new Vector2(700, 72), "", new Color(0.82f, 0.68f, 0.45f), target, "Ride" + i);
                rows[i] = b;
                labels[i] = b.transform.Find("Label").GetComponent<Text>();
                labels[i].color = new Color(0.2f, 0.12f, 0.05f);
            }
            rr.rowRoots = rows;
            rr.rowLabels = labels;
            Button(rt, "Close", new Vector2(0, -440), new Vector2(240, 66), "Close", new Color(0.45f, 0.25f, 0.15f), target, nameof(Railroad.Close));
            go.SetActive(false);
            RailroadPanel = go;
        }

        /// <summary>The bounty board's panel: three posters side by side, each with its Turn in button.</summary>
        private static void BuildBountyPanel(Transform systems, BountyBoard bb)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(bb);
            var go = new GameObject("BountyPanel", typeof(RectTransform));
            go.transform.SetParent(systems, false);
            MakeWorldCanvas(go, new Vector2(1300, 900), Vector3.zero, 0.0006f);
            var rt = (RectTransform)go.transform;
            Panel(rt, "Background", Vector2.zero, new Vector2(1300, 900), new Color(0.45f, 0.32f, 0.2f));
            bb.panelRoot = go;
            bb.headerLabel = InkLabel(rt, "Header", new Vector2(0, 370), new Vector2(1200, 130), "", 44, TextAnchor.MiddleCenter);
            bb.headerLabel.color = new Color(1f, 0.9f, 0.65f);
            bb.posterTitles = new Text[CharacterData.BOUNTY_SLOTS];
            bb.posterBodies = new Text[CharacterData.BOUNTY_SLOTS];
            bb.turnInButtons = new GameObject[CharacterData.BOUNTY_SLOTS];
            for (int s = 0; s < CharacterData.BOUNTY_SLOTS; s++)
            {
                float x = (s - 1) * 410f;
                var poster = Panel(rt, "Poster" + s, new Vector2(x, 0), new Vector2(380, 560), Parchment);
                InkLabel(poster, "Wanted", new Vector2(0, 230), new Vector2(360, 60), "WANTED", 44, TextAnchor.MiddleCenter);
                bb.posterTitles[s] = InkLabel(poster, "Title", new Vector2(0, 150), new Vector2(350, 90), "", 30, TextAnchor.MiddleCenter);
                bb.posterBodies[s] = InkLabel(poster, "Body", new Vector2(0, -20), new Vector2(340, 240), "", 24, TextAnchor.MiddleCenter);
                bb.turnInButtons[s] = Button(poster, "TurnIn", new Vector2(0, -220), new Vector2(240, 66), "Turn in", new Color(0.25f, 0.5f, 0.3f), target, "TurnIn" + s);
            }
            bb.statusLabel = InkLabel(rt, "Status", new Vector2(0, -330), new Vector2(1200, 50), "", 24, TextAnchor.MiddleCenter);
            bb.statusLabel.color = new Color(1f, 0.92f, 0.7f);
            Button(rt, "Close", new Vector2(0, -400), new Vector2(240, 66), "Close", new Color(0.3f, 0.18f, 0.1f), target, nameof(BountyBoard.Close));
            go.SetActive(false);
            BountyPanel = go;
        }

        /// <summary>
        /// The passenger car you ride in (a set far from everything, never switched off): benches, oil lamps and windows
        /// with the desert sliding past outside. Returns where you sit.
        /// </summary>
        private static Transform BuildRideCar()
        {
            var root = new GameObject("RailroadCar").transform;
            root.position = RideOrigin;
            var wood = PsxTexMat("SaloonDarkWood", new Color(0.34f, 0.2f, 0.12f), TileTexture(TerrainTexDir + "/WoodPlanks.png"), 1.6f);
            var velvet = PsxTexMat("TavernVelvet", new Color(0.78f, 0.14f, 0.2f), TileTexture(StreetTexDir + "/Velvet.png"), 1f);
            var glow = PMat("TavernLantern", new Color(1f, 0.78f, 0.4f), 1f, new Color(1f, 0.65f, 0.25f));
            var sand = PsxTexMat("DesertFloor", new Color(0.85f, 0.62f, 0.42f), TileTexture(TerrainTexDir + "/DesertSand.png"), 6f);
            var rock = PsxTexMat("ButteRock", new Color(0.86f, 0.52f, 0.36f), TileTexture(TerrainTexDir + "/RedRock.png"), 7f);
            var cactus = PsxTexMat("RideCactus", new Color(0.34f, 0.52f, 0.3f), TileTexture(StreetTexDir + "/Cactus.png"), 1.4f);
            const float W = 3.4f, L = 14f, H = 3f;
            Block(root, "CarFloor", new Vector3(0, -0.1f, 0), new Vector3(W, 0.2f, L), wood, Quaternion.identity);
            Block(root, "CarCeiling", new Vector3(0, H + 0.1f, 0), new Vector3(W, 0.2f, L), wood, Quaternion.identity);
            foreach (float s in new[] { -1f, 1f })
            {
                Block(root, "CarEnd", new Vector3(0, H / 2f, s * L / 2f), new Vector3(W, H, 0.2f), wood, Quaternion.identity);
                // Walls below and above a long open window strip (you see out through the gap).
                Block(root, "CarWallLow", new Vector3(s * W / 2f, 0.5f, 0), new Vector3(0.2f, 1f, L), wood, Quaternion.identity);
                Block(root, "CarWallHigh", new Vector3(s * W / 2f, 2.6f, 0), new Vector3(0.2f, 0.8f, L), wood, Quaternion.identity);
                for (float zz = -L / 2f + 1f; zz <= L / 2f - 1f; zz += 2.4f)
                {
                    Block(root, "WindowPost", new Vector3(s * W / 2f, 1.6f, zz), new Vector3(0.22f, 1.2f, 0.25f), wood, Quaternion.identity);
                    Deco(root, PrimitiveType.Cube, "Seat", new Vector3(s * (W / 2f - 0.55f), 0.45f, zz + 1.2f), new Vector3(0.9f, 0.15f, 1.6f), velvet, Quaternion.identity, false);
                    Deco(root, PrimitiveType.Cube, "SeatBack", new Vector3(s * (W / 2f - 0.15f), 0.8f, zz + 1.2f), new Vector3(0.12f, 0.8f, 1.6f), velvet, Quaternion.identity, false);
                }
            }
            for (int k = 0; k < 3; k++)
            {
                Shape(root, "CarLamp", GemMesh(), new Vector3(0, H - 0.4f, -4f + k * 4f), new Vector3(0.3f, 0.45f, 0.3f), glow, false, Quaternion.identity);
                PointLight(root, new Vector3(0, H - 0.6f, -4f + k * 4f), new Color(1f, 0.8f, 0.55f), 6f, 1f);
            }
            Block(root, "DesertBelow", new Vector3(0, -2.5f, 0), new Vector3(500f, 1f, 500f), sand, Quaternion.identity);
            const float span = 300f, seconds = 12f;
            for (int copy = 0; copy < 2; copy++)
            {
                var strip = Child(root, "RideScenery" + copy, new Vector3(0, -3f, copy * span));
                var rng = new System.Random(5150);
                float Rr(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                for (int k = 0; k < 40; k++)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    Vector3 at = new Vector3(side * Rr(10f, 140f), 0, Rr(-span / 2f, span / 2f));
                    if (rng.NextDouble() < 0.55) Saguaro(strip, at, Rr(1f, 1.8f), (float)rng.NextDouble(), cactus, false);
                    else Butte(strip, at, Rr(5f, 14f), Rr(5f, 16f), rock, sand, Rr(0f, 360f));
                }
                foreach (var t in strip.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
                LoopAnimator(strip.gameObject, ScrollClip("RailroadScenery" + copy, copy * span, span, seconds), AnimatorCullingMode.CullUpdateTransforms);
            }
            var sit = Child(root, "RideSeat", new Vector3(0, 0.05f, -2f));
            sit.localRotation = Quaternion.identity;
            return sit;
        }
    }
}
