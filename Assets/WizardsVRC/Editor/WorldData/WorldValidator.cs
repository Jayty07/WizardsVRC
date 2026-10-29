using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>Checks world data for mistakes before a rebuild (also shown live in the inspectors).</summary>
    public static class WorldValidator
    {
        public class Report
        {
            public readonly List<string> errors = new List<string>();
            public readonly List<string> warnings = new List<string>();

            public void Error(string s) { errors.Add(s); }
            public void Warn(string s) { warnings.Add(s); }
            public bool Ok { get { return errors.Count == 0; } }

            public void Log()
            {
                foreach (var e in errors) Debug.LogError("[WizardsVRC World] " + e);
                foreach (var w in warnings) Debug.LogWarning("[WizardsVRC World] " + w);
            }

            public string Summary()
            {
                if (errors.Count == 0 && warnings.Count == 0) return "World data looks good.";
                var sb = new StringBuilder();
                sb.Append(errors.Count).Append(" error(s), ").Append(warnings.Count).Append(" warning(s). See the Console for details.");
                for (int i = 0; i < Mathf.Min(6, errors.Count); i++) sb.Append("\n- ").Append(errors[i]);
                return sb.ToString();
            }
        }

        public static readonly string[] SchoolKeys = { "Fire", "Ice", "Storm", "Life", "Death", "Myth", "Balance" };

        /// <summary>NPC id for a built-in key (Headmaster, OwnProfessor, school name), or int.MinValue.</summary>
        public static int BuiltInNpc(string key)
        {
            if (string.IsNullOrEmpty(key)) return int.MinValue;
            if (key == "Headmaster") return QuestManager.NPC_HEADMASTER;
            if (key == "OwnProfessor") return QuestManager.NPC_OWN_PROFESSOR;
            for (int i = 0; i < SchoolKeys.Length; i++) if (key == SchoolKeys[i]) return i;
            return int.MinValue;
        }

        /// <summary>Spell ids and names from the open scene's CardDatabase (null when no scene is built yet).</summary>
        public static Dictionary<int, string> KnownCards()
        {
            var db = Object.FindObjectOfType<CardDatabase>();
            if (db == null || db.ids == null) return null;
            var d = new Dictionary<int, string>();
            for (int i = 0; i < db.ids.Length; i++)
            {
                string school = db.schools[i] >= 0 && db.schools[i] < SchoolKeys.Length ? SchoolKeys[db.schools[i]] : "General";
                d[db.ids[i]] = db.names[i] + "  (" + school + ", " + db.pipCosts[i] + " pip" + (db.pipCosts[i] == 1 ? "" : "s") + ")";
            }
            return d;
        }

        public static Report ValidateAll(WorldDataStore.Data data, Dictionary<int, string> cards)
        {
            var r = new Report();
            if (data.monsters == null) r.Error("No Monster Catalog found in " + WorldDataStore.Folder + ".");
            else ValidateMonsters(data.monsters, cards, r);
            var heights = new Dictionary<float, string>();
            var zoneKeys = new HashSet<string>();
            int hubs = 0;
            var schools = new HashSet<School>();
            foreach (var z in data.zones)
            {
                if (!z.includeInBuild) continue;
                if (!zoneKeys.Add(z.zoneKey)) r.Error("Two zones use the key '" + z.zoneKey + "'.");
                if (z.isHub) hubs++;
                else
                {
                    foreach (var kv in heights)
                        if (Mathf.Abs(kv.Key - z.height) < 100f) r.Error("Zones '" + kv.Value + "' and '" + z.zoneKey + "' are less than 100 m apart in height.");
                    heights[z.height] = z.zoneKey;
                }
                if (z.landmarks != null)
                    foreach (var l in z.landmarks)
                        if (l != null && l.kind == LandmarkKind.SchoolHall && !schools.Add(l.school)) r.Error("There are two School Halls for " + l.school + ".");
                ValidateZone(z, data.monsters, r);
            }
            if (hubs > 1) r.Error("Only one zone can be the hub (Is Hub).");
            // Street gates must lead somewhere real; quest locks must name real quests.
            var questIds = new HashSet<int> { QuestManager.QUEST_PROPHECY, QuestManager.QUEST_TRIAL };
            foreach (var ql in data.questlines) if (ql != null && ql.includeInBuild) foreach (var q in ql.quests) if (q != null) questIds.Add(q.saveId);
            foreach (var z in data.zones)
            {
                if (!z.includeInBuild || z.landmarks == null) continue;
                foreach (var l in z.landmarks)
                {
                    if (l == null) continue;
                    if (l.kind == LandmarkKind.StreetGate)
                    {
                        ZoneDefinition target = null;
                        foreach (var t in data.zones) if (t != null && t.includeInBuild && t.zoneKey == l.targetZone) target = t;
                        if (target == null) r.Error("Zone '" + z.zoneKey + "': gate '" + l.symbol + "' leads to zone '" + l.targetZone + "', which doesn't exist (or isn't included).");
                        else if (target.isHub) r.Error("Zone '" + z.zoneKey + "': gate '" + l.symbol + "' can't lead to the hub.");
                    }
                    if (l.requiredQuest >= 0 && !questIds.Contains(l.requiredQuest))
                        r.Error("Zone '" + z.zoneKey + "': landmark '" + l.symbol + "' needs quest " + l.requiredQuest + ", which doesn't exist.");
                }
            }
            ValidateQuests(data, r);
            ValidateWorlds(data, r);
            return r;
        }

        public static void ValidateMonsters(MonsterCatalog cat, Dictionary<int, string> cards, Report r)
        {
            var ids = new HashSet<int>();
            foreach (var m in cat.monsters)
            {
                if (m == null) continue;
                string who = "Monster " + m.id + " (" + m.name + ")";
                if (m.id <= 0) r.Error(who + ": id must be 1 or more.");
                if (!ids.Add(m.id)) r.Error(who + ": id is used twice.");
                if (m.health <= 0) r.Error(who + ": health must be above 0.");
                if (m.goldMax < m.goldMin) r.Warn(who + ": gold max is below gold min.");
                if (m.spells.Count == 0) r.Warn(who + ": has no spells, so it will only pass.");
                if (m.spells.Count > CardDatabase.MONSTER_SPELLS) r.Warn(who + ": only the first " + CardDatabase.MONSTER_SPELLS + " spells are used.");
                if (cards != null)
                    foreach (int s in m.spells)
                        if (s != 0 && !cards.ContainsKey(s)) r.Error(who + ": unknown spell id " + s + ".");
            }
        }

        public static void ValidateZone(ZoneDefinition z, MonsterCatalog cat, Report r)
        {
            string zn = "Zone '" + z.zoneKey + "'";
            var g = new ZoneGrid(z);
            if (g.height == 0) { r.Error(zn + ": the map is empty."); return; }
            if (!z.isHub && z.height >= 100f && z.height < 400f) r.Error(zn + ": height " + z.height + " overlaps the keep interior (100-300). Use 600 or more.");
            if (!z.isHub && z.height < 100f) r.Error(zn + ": height must be far above the castle (600 or more).");

            var starts = g.Find('S');
            if (starts.Count == 0) r.Error(zn + ": the map needs an S (entrance).");
            if (starts.Count > 1) r.Warn(zn + ": several S cells; the first one (top-left) is the entrance.");

            // symbols
            var unknown = new HashSet<char>();
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    char c = g.At(x, y);
                    if (!ZoneGrid.IsBuiltIn(c) && !g.IsCustom(c)) unknown.Add(c);
                }
            foreach (char c in unknown) r.Error(zn + ": map symbol '" + c + "' isn't defined (add it to NPCs or Points, or use the legend).");

            // reachability and edges
            var reach = g.Reachable();
            int unreachable = 0;
            Vector2Int firstUnreachable = Vector2Int.zero;
            for (int y = 0; y < g.height; y++)
                for (int x = 0; x < g.width; x++)
                {
                    if (!g.Walkable(x, y)) continue;
                    if (!reach.Contains(new Vector2Int(x, y)) && starts.Count > 0) { if (unreachable++ == 0) firstUnreachable = new Vector2Int(x, y); }
                    if (x == 0 || y == 0 || x == g.width - 1 || y == g.height - 1) r.Warn(zn + ": open cell on the map edge at column " + x + ", row " + y + " (the edge gets an invisible wall).");
                }
            if (unreachable > 0) r.Warn(zn + ": " + unreachable + " open cell(s) can't be reached from S (first at column " + firstUnreachable.x + ", row " + firstUnreachable.y + ").");

            // NPCs and points
            var symbols = new HashSet<char>();
            var keys = new HashSet<string>();
            foreach (var n in z.npcs)
            {
                if (n == null) continue;
                CheckSymbol(z, n.Symbol, "NPC '" + n.key + "'", symbols, r);
                if (!keys.Add(n.key)) r.Error(zn + ": NPC key '" + n.key + "' is used twice.");
                if (BuiltInNpc(n.key) != int.MinValue) r.Error(zn + ": NPC key '" + n.key + "' is reserved.");
                int count = g.Find(n.Symbol).Count;
                if (count == 0) r.Warn(zn + ": NPC '" + n.key + "' (symbol " + n.symbol + ") isn't on the map.");
                if (count > 1) r.Error(zn + ": NPC '" + n.key + "' is placed " + count + " times; NPCs go on one cell.");
            }
            foreach (var p in z.points)
            {
                if (p == null) continue;
                CheckSymbol(z, p.Symbol, "Point '" + p.key + "'", symbols, r);
                if (g.Find(p.Symbol).Count == 0) r.Warn(zn + ": point '" + p.key + "' (symbol " + p.symbol + ") isn't on the map.");
            }
            if (z.landmarks != null)
                foreach (var l in z.landmarks)
                {
                    if (l == null) continue;
                    string ln = zn + ": " + l.kind + " '" + l.symbol + "'";
                    CheckSymbol(z, l.Symbol, l.kind + " landmark", symbols, r);
                    var cells = g.Find(l.Symbol);
                    if (cells.Count == 0) { r.Warn(ln + " isn't on the map."); continue; }
                    var fp = g.Footprint(l.Symbol);
                    if (l.IsBuilding)
                    {
                        if (cells.Count != fp.width * fp.height) r.Warn(ln + " doesn't fill a rectangle; it's built over its whole bounding box.");
                        if (g.FrontSide(fp) == Vector2Int.zero) r.Error(ln + " has no open cell next to it for its door.");
                    }
                    else if (!l.Solid && cells.Count > 1) r.Warn(ln + " is placed " + cells.Count + " times; only the first is used.");
                    if (!z.isHub && (l.kind == LandmarkKind.Keep || l.kind == LandmarkKind.StreetGate || l.kind == LandmarkKind.ArenaPad ||
                                     l.kind == LandmarkKind.BankChest || l.kind == LandmarkKind.BackpackPedestal || l.kind == LandmarkKind.NoticeBoard))
                        r.Warn(ln + ": this landmark only works in the hub (Is Hub), so it is left out here.");
                    if (l.kind == LandmarkKind.StreetGate && string.IsNullOrEmpty(l.targetZone)) r.Error(ln + ": set Target Zone (the zone key it leads to).");
                }

            // encounters, rings and patrols
            var ringSymbols = new HashSet<char>();
            foreach (var e in z.encounters)
            {
                if (e == null) continue;
                string en = zn + " encounter '" + e.key + "'";
                if (!ZoneGrid.IsRingDigit(e.Symbol)) { r.Error(en + ": ring symbol must be a digit 1-9."); continue; }
                if (!ringSymbols.Add(e.Symbol)) r.Error(en + ": ring digit " + e.Symbol + " is used by two encounters.");
                var spots = g.Find(e.Symbol);
                if (spots.Count == 0) r.Error(en + ": ring digit " + e.Symbol + " isn't on the map.");
                foreach (var spot in spots)
                {
                    var blockers = g.RingBlockers(spot);
                    if (blockers.Count > 0)
                        r.Error(en + ": the ring at column " + spot.x + ", row " + spot.y + " is too close to a wall at column " + blockers[0].x + ", row " + blockers[0].y
                                + " (rings need about " + ZoneGrid.RingClearance + " m of open space all round).");
                }
                if (e.roster == null || e.roster.Count == 0) r.Error(en + ": the roster is empty.");
                else
                {
                    if (e.roster.Count > BattleManager.SIDE) r.Warn(en + ": only the first " + BattleManager.SIDE + " monsters fit in a ring.");
                    if (cat != null) foreach (int id in e.roster) if (cat.Find(id) == null) r.Error(en + ": monster id " + id + " isn't in the Monster Catalog.");
                }
                if (e.wanderers > 0)
                {
                    bool loop, ok;
                    var route = g.PatrolRoute(e.PatrolSymbol, out loop, out ok);
                    if (route == null) r.Warn(en + ": no patrol cells '" + e.PatrolSymbol + "' on the map; its monsters will circle the ring.");
                    else if (!ok) r.Warn(en + ": patrol '" + e.PatrolSymbol + "' isn't a single 1-cell-wide line or loop; the route may cut corners.");
                }
            }
            foreach (char d in "123456789")
                if (g.Find(d).Count > 0 && !ringSymbols.Contains(d)) r.Error(zn + ": ring digit " + d + " is on the map but no encounter uses it.");
            foreach (char l in "abcdefghi")
                if (g.Find(l).Count > 0 && !ringSymbols.Contains((char)('1' + (l - 'a')))) r.Warn(zn + ": patrol '" + l + "' has no encounter " + (char)('1' + (l - 'a')) + ".");
        }

        // ------------------------------------------------------------------ worlds

        /// <summary>
        /// Worlds: a goal set out in the area (goal quest), 10+ storylines that all come after it and lead to the finale
        /// (which needs every storyline), and the next world only opening after the previous finale.
        /// </summary>
        public static void ValidateWorlds(WorldDataStore.Data data, Report r)
        {
            if (data.worlds == null || data.worlds.Count == 0) return;
            var deps = new Dictionary<int, List<int>>();
            var exists = new HashSet<int> { QuestManager.QUEST_PROPHECY, QuestManager.QUEST_TRIAL };
            deps[QuestManager.QUEST_TRIAL] = new List<int> { QuestManager.QUEST_PROPHECY };
            foreach (var ql in data.questlines)
            {
                if (ql == null || !ql.includeInBuild) continue;
                foreach (var q in ql.quests)
                {
                    if (q == null) continue;
                    exists.Add(q.saveId);
                    var d = new List<int>();
                    if (q.prerequisite >= 0) d.Add(q.prerequisite);
                    if (q.alsoRequires != null) d.AddRange(q.alsoRequires);
                    deps[q.saveId] = d;
                }
            }
            var worlds = new List<WorldArc>();
            foreach (var w in data.worlds) if (w != null && w.includeInBuild) worlds.Add(w);
            // The builder makes each finale need every storyline; mirror that here.
            foreach (var w in worlds)
            {
                if (w.finaleQuest < 0 || !deps.ContainsKey(w.finaleQuest)) continue;
                foreach (var sl in w.storylines) if (sl != null && sl.includeInBuild && sl.StorylineEnd() >= 0) deps[w.finaleQuest].Add(sl.StorylineEnd());
            }

            var orders = new HashSet<int>();
            var owner = new Dictionary<Questline, string>();
            WorldArc prev = null;
            foreach (var w in worlds)
            {
                string wn = "World '" + w.displayName + "'";
                if (!orders.Add(w.order)) r.Error(wn + ": another world also has order " + w.order + ".");
                if (string.IsNullOrEmpty(w.goal)) r.Warn(wn + ": write its goal (what every storyline builds toward).");
                if (w.goalQuest < 0) r.Warn(wn + ": set Goal Quest, the quest where the goal is set out in the area.");
                else if (!exists.Contains(w.goalQuest)) r.Error(wn + ": goal quest " + w.goalQuest + " doesn't exist.");
                if (w.finaleQuest < 0) r.Warn(wn + ": set Finale Quest, the quest that completes the goal (it will need every storyline).");
                else if (!exists.Contains(w.finaleQuest)) r.Error(wn + ": finale quest " + w.finaleQuest + " doesn't exist.");

                int count = 0;
                foreach (var sl in w.storylines)
                {
                    if (sl == null || !sl.includeInBuild) continue;
                    count++;
                    string sn = wn + " storyline '" + sl.StorylineName() + "'";
                    string other;
                    if (owner.TryGetValue(sl, out other)) r.Error(sn + " is also in " + other + ".");
                    else owner[sl] = wn;
                    int end = sl.StorylineEnd();
                    bool inside = false;
                    foreach (var q in sl.quests) if (q != null && q.saveId == end) inside = true;
                    if (end < 0) { r.Error(sn + " has no quests."); continue; }
                    if (!inside) r.Error(sn + ": its Storyline End " + end + " isn't one of its quests.");
                    if (end == w.finaleQuest) r.Error(sn + ": the finale can't be a storyline's end (it comes after all of them).");
                    if (w.goalQuest >= 0 && !DependsOn(end, w.goalQuest, deps))
                        r.Warn(sn + " doesn't come after the goal quest " + w.goalQuest + "; every storyline should follow from the goal set out in the area.");
                    if (prev != null && prev.finaleQuest >= 0 && !DependsOn(end, prev.finaleQuest, deps))
                        r.Error(sn + " can be played before '" + prev.displayName + "' is finished; its first quest should require that world's finale (" + prev.finaleQuest + ").");
                }
                if (count < WorldArc.MinStorylines)
                    r.Warn(wn + " has " + count + " storyline(s); a world should have at least " + WorldArc.MinStorylines + " leading up to its goal before players move on.");

                foreach (string key in w.zones)
                {
                    bool found = false;
                    foreach (var z in data.zones) if (z != null && z.zoneKey == key) found = true;
                    if (!found) r.Warn(wn + ": zone '" + key + "' doesn't exist.");
                    if (prev == null || prev.finaleQuest < 0) continue;
                    // The next world's gates should stay shut until the previous world's finale.
                    foreach (var z in data.zones)
                        if (z != null && z.landmarks != null)
                            foreach (var l in z.landmarks)
                                if (l != null && l.kind == LandmarkKind.StreetGate && l.targetZone == key && (l.requiredQuest < 0 || !DependsOn(l.requiredQuest, prev.finaleQuest, deps)))
                                    r.Warn(wn + ": the gate to '" + key + "' opens before '" + prev.displayName + "' is finished; set its Required Quest to " + prev.finaleQuest + " (or later).");
                }
                prev = w;
            }
            foreach (var ql in data.questlines)
                if (ql != null && ql.includeInBuild && !owner.ContainsKey(ql) && !IsFinaleLine(ql, worlds))
                    r.Warn("Questline '" + ql.StorylineName() + "' isn't a storyline of any world (add it to a World's Storylines).");
        }

        private static bool IsFinaleLine(Questline ql, List<WorldArc> worlds)
        {
            foreach (var w in worlds) foreach (var q in ql.quests) if (q != null && q.saveId == w.finaleQuest) return true;
            return false;
        }

        /// <summary>True when quest q needs target first (directly or through its requirements).</summary>
        private static bool DependsOn(int q, int target, Dictionary<int, List<int>> deps)
        {
            if (q == target) return true;
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(q);
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                if (!seen.Add(c)) continue;
                List<int> d;
                if (!deps.TryGetValue(c, out d)) continue;
                foreach (int p in d)
                {
                    if (p == target) return true;
                    stack.Push(p);
                }
            }
            return false;
        }

        private static void CheckSymbol(ZoneDefinition z, char c, string who, HashSet<char> used, Report r)
        {
            string zn = "Zone '" + z.zoneKey + "'";
            if (c == '\0') { r.Error(zn + ": " + who + " has no map symbol."); return; }
            if (ZoneGrid.IsBuiltIn(c) || char.IsWhiteSpace(c)) r.Error(zn + ": " + who + " uses symbol '" + c + "', which is reserved by the legend.");
            if (!used.Add(c)) r.Error(zn + ": symbol '" + c + "' is used by two NPCs/points.");
        }

        public static void ValidateQuests(WorldDataStore.Data data, Report r)
        {
            var npcKeys = new HashSet<string>();
            var points = new Dictionary<string, ZonePointKind>();
            var rosterMonsters = new HashSet<int>();
            foreach (var z in data.zones)
            {
                if (!z.includeInBuild) continue;
                foreach (var n in z.npcs) if (n != null) npcKeys.Add(n.key);
                foreach (var p in z.points) if (p != null && !points.ContainsKey(p.key)) points[p.key] = p.kind;
                foreach (var e in z.encounters) if (e != null && e.roster != null) foreach (int id in e.roster) rosterMonsters.Add(id);
            }
            var saveIds = new HashSet<int> { QuestManager.QUEST_PROPHECY, QuestManager.QUEST_TRIAL };
            var all = new List<QuestEntry>();
            foreach (var ql in data.questlines)
            {
                if (!ql.includeInBuild) continue;
                foreach (var q in ql.quests)
                {
                    if (q == null) continue;
                    all.Add(q);
                    string qn = "Quest " + q.saveId + " '" + q.title + "' (" + ql.name + ")";
                    if (q.saveId < 2 || q.saveId >= CharacterData.QUEST_SLOTS) r.Error(qn + ": save id must be 2-" + (CharacterData.QUEST_SLOTS - 1) + ".");
                    else if (!saveIds.Add(q.saveId)) r.Error(qn + ": save id " + q.saveId + " is used twice.");
                }
            }
            var known = new HashSet<int>(saveIds);
            foreach (var q in all)
            {
                string qn = "Quest " + q.saveId + " '" + q.title + "'";
                if (q.prerequisite >= 0 && !known.Contains(q.prerequisite)) r.Error(qn + ": prerequisite " + q.prerequisite + " doesn't exist.");
                if (q.prerequisite == q.saveId) r.Error(qn + ": a quest can't require itself.");
                if (q.alsoRequires != null)
                    foreach (int req in q.alsoRequires)
                    {
                        if (req == q.saveId) r.Error(qn + ": a quest can't require itself.");
                        else if (!known.Contains(req)) r.Error(qn + ": required quest " + req + " doesn't exist.");
                    }
                if (!q.startsAutomatically && !NpcExists(q.giverNpc, npcKeys)) r.Error(qn + ": giver NPC '" + q.giverNpc + "' doesn't exist.");
                if (q.steps.Count == 0) r.Error(qn + ": needs at least one step.");
                if (q.steps.Count > QuestManager.MAX_STEPS) r.Error(qn + ": at most " + QuestManager.MAX_STEPS + " steps.");
                if (!q.startsAutomatically && q.steps.Count > 0 && q.steps[0].kind == QuestStepKind.Talk && q.steps[0].npc == q.giverNpc)
                    r.Warn(qn + ": step 1 is talking to the giver you just talked to; players will have to talk twice.");
                for (int i = 0; i < q.steps.Count; i++)
                {
                    var s = q.steps[i];
                    string sn = qn + " step " + (i + 1);
                    switch (s.kind)
                    {
                        case QuestStepKind.Talk:
                            if (!NpcExists(s.npc, npcKeys)) r.Error(sn + ": NPC '" + s.npc + "' doesn't exist.");
                            break;
                        case QuestStepKind.Defeat:
                        case QuestStepKind.Collect:
                        case QuestStepKind.Boss:
                            if (data.monsters != null && data.monsters.Find(s.monsterId) == null) r.Error(sn + ": monster id " + s.monsterId + " isn't in the Monster Catalog.");
                            else if (!rosterMonsters.Contains(s.monsterId)) r.Warn(sn + ": no zone encounter has monster " + s.monsterId + " (only the Training Yard might).");
                            break;
                        case QuestStepKind.Interact:
                        case QuestStepKind.Explore:
                            ZonePointKind kind;
                            if (!points.TryGetValue(s.point, out kind)) r.Error(sn + ": zone point '" + s.point + "' doesn't exist.");
                            else if (s.kind == QuestStepKind.Interact && kind != ZonePointKind.Interact) r.Error(sn + ": point '" + s.point + "' is " + kind + ", not Interact.");
                            else if (s.kind == QuestStepKind.Explore && kind != ZonePointKind.Explore) r.Error(sn + ": point '" + s.point + "' is " + kind + ", not Explore.");
                            break;
                    }
                    if (string.IsNullOrEmpty(s.objective)) r.Warn(sn + ": has no objective text.");
                    if (s.count > 255) r.Error(sn + ": count must be 255 or less.");
                }
            }
        }

        private static bool NpcExists(string key, HashSet<string> zoneNpcs)
        {
            return BuiltInNpc(key) != int.MinValue || (key != null && zoneNpcs.Contains(key));
        }
    }
}
