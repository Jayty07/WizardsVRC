using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Scenes with more than one voice, and bosses who speak before the fight.
    ///   - Dialogue lines written "Name|words" are spoken by Name, "!Name|words" by an enemy (red rim); the camera cuts to
    ///     that character when they're one of the zone's NPCs, else stays on the scene's own shots (SpeakerLines).
    ///   - A zone encounter with Intro Lines gets a cutscene that plays when you walk up to its ring (EncounterIntro):
    ///     a wide shot of the ring from the way you came, then close on the boss.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>
        /// Splits "Name|words" lines into per-line speakers, stances and shots. 'shots' are the scene's own (a wide shot, then
        /// a close one); a named speaker who is a zone NPC gets their own close shot.
        /// </summary>
        private static void SpeakerLines(Cutscene cs, string defaultSpeaker, Transform[] shots, WorldBuild wb)
        {
            if (cs == null || cs.lines == null) return;
            int n = cs.lines.Length;
            bool any = false;
            foreach (var l in cs.lines) if (l != null && l.IndexOf('|') > 0) any = true;
            if (!any) return;
            var byName = new Dictionary<string, Transform[]>();
            if (wb != null)
                foreach (var kv in wb.npcNames)
                {
                    Transform[] s;
                    if (kv.Value != null && wb.npcShots.TryGetValue(kv.Key, out s) && s != null && s.Length > 0) byName[kv.Value] = s;
                }
            var speakers = new string[n];
            var stances = new int[n];
            var perLine = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                string line = cs.lines[i] ?? "";
                string who = defaultSpeaker;
                bool hostile = false;
                int bar = line.IndexOf('|');
                if (bar > 0 && bar < 64)
                {
                    who = line.Substring(0, bar).Trim();
                    line = line.Substring(bar + 1).Trim();
                    if (who.StartsWith("!")) { hostile = true; who = who.Substring(1).Trim(); }
                }
                cs.lines[i] = line;
                speakers[i] = who;
                stances[i] = hostile ? Cutscene.STANCE_HOSTILE
                    : who.StartsWith("Professor") || who.StartsWith("Headmaster") ? Cutscene.STANCE_ALLY : Cutscene.STANCE_NEUTRAL;
                Transform[] own;
                if (who != defaultSpeaker && byName.TryGetValue(who, out own)) perLine[i] = own[Mathf.Min(1, own.Length - 1)];
                else if (shots != null && shots.Length > 0) perLine[i] = shots[Mathf.Clamp(i == 0 ? 0 : shots.Length - 1, 0, shots.Length - 1)];
            }
            cs.speakers = speakers;
            cs.stances = stances;
            if (shots != null && shots.Length > 0) cs.shots = perLine;
        }

        /// <summary>A boss's walk-up cutscene from its encounter's Intro Lines (null when it has none).</summary>
        private static EncounterIntro BuildEncounterIntro(Transform root, ZoneGrid g, ZoneEncounter e, Vector2Int cell, BattleClient client, QuestManager qm,
            Transform spawn, WorldBuild wb)
        {
            if (e == null || e.introLines == null || !HasLines(e.introLines) || qm == null || qm.cutscenePlayer == null) return null;
            var lines = new List<string>();
            foreach (var l in e.introLines) if (!string.IsNullOrEmpty(l)) lines.Add(l);
            string boss = "";
            if (e.roster != null && e.roster.Count > 0 && _monsterCatalog != null)
            {
                MonsterEntry m = _monsterCatalog.Find(e.roster[0]);
                if (m != null) boss = m.name;
            }
            Vector3 ring = g.Local(cell);
            Vector3 toward = spawn != null ? spawn.localPosition - ring : Vector3.back;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f) toward = Vector3.back;
            toward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            Vector3 look = ring + Vector3.up * 1.6f;
            string name = "Intro_" + e.key;
            var holder = Child(root, name, ring);
            var shots = new[]
            {
                Shot(root, name + "_Wide", ring + toward * 15f + side * 3f + Vector3.up * 5.5f, look),
                Shot(root, name + "_Close", ring + toward * 5f - side * 3.5f + Vector3.up * 2.3f, look),
            };
            var cs = MakeCutscene(wb.cutsceneParent, name, boss, lines.ToArray(), "intro_" + e.key.ToLowerInvariant(), shots);
            cs.cutsceneId = name;
            SpeakerLines(cs, boss, shots, wb);
            // Lines without a name are the boss's own: an enemy.
            if (cs.stances == null || cs.stances.Length != cs.lines.Length)
            {
                cs.stances = new int[cs.lines.Length];
                for (int i = 0; i < cs.stances.Length; i++) cs.stances[i] = Cutscene.STANCE_HOSTILE;
            }
            else
                for (int i = 0; i < cs.stances.Length; i++) if (cs.speakers[i] == boss) cs.stances[i] = Cutscene.STANCE_HOSTILE;
            var intro = UdonSharpUndo.AddComponent<EncounterIntro>(holder.gameObject);
            intro.cutscene = cs;
            intro.cutscenePlayer = qm.cutscenePlayer;
            intro.client = client;
            intro.centre = holder;
            intro.radius = Mathf.Max(14f, DuelCircleRadius + 8f);
            wb.behaviours.Add(cs);
            wb.behaviours.Add(intro);
            return intro;
        }
    }
}
