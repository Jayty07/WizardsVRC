using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using WizardsVRC.EditorTools.WorldData;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Scenes with more than one voice, and bosses who speak before the fight.
    ///   - Dialogue lines written "Name|words" are spoken by Name, "!Name|words" by an enemy (red rim). Where the scene's
    ///     own character has a body the director films it with its whole cast (WizardsVRCSceneBuilder.Cutscenes.cs);
    ///     a scene on placed shots cuts to a named speaker's own shot when they're one of the zone's NPCs (SpeakerLines).
    ///   - A zone encounter with Intro Lines gets a cutscene that plays when you walk up to its ring (EncounterIntro):
    ///     a wide shot of the ring from the way you came, then close on the boss.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>
        /// The placed shots of a scene with more than one voice, a beat each: 'shots' are the scene's own (a wide shot,
        /// then a close one); a named speaker who is a zone NPC gets their own close shot. (The lines themselves were
        /// split into speakers, stances and directions when the scene was made: WizardsVRCSceneBuilder.Cutscenes.cs.)
        /// A scene the director films only falls back on these where it can't find a clear view.
        /// </summary>
        private static void SpeakerLines(Cutscene cs, string defaultSpeaker, Transform[] shots, WorldBuild wb)
        {
            if (cs == null || cs.lines == null || shots == null || shots.Length == 0) return;
            int n = cs.lines.Length;
            bool any = false;
            for (int i = 0; i < n; i++)
                if (cs.KindAt(i) == Cutscene.BEAT_SAY && cs.SpeakerAt(i) != defaultSpeaker) any = true;
            if (!any) return;
            var byName = new Dictionary<string, Transform[]>();
            if (wb != null)
                foreach (var kv in wb.npcNames)
                {
                    Transform[] s;
                    if (kv.Value != null && wb.npcShots.TryGetValue(kv.Key, out s) && s != null && s.Length > 0) byName[kv.Value] = s;
                }
            var perBeat = new Transform[n];
            Transform shot = shots[0];
            bool first = true;
            for (int i = 0; i < n; i++)
            {
                if (cs.KindAt(i) == Cutscene.BEAT_SAY)
                {
                    string who = cs.SpeakerAt(i);
                    Transform[] own;
                    if (who != defaultSpeaker && byName.TryGetValue(who, out own)) shot = own[Mathf.Min(1, own.Length - 1)];
                    else shot = shots[first ? 0 : shots.Length - 1];
                    first = false;
                }
                // (A wait, a card or a cutaway stays on the shot before it.)
                perBeat[i] = shot;
            }
            cs.shots = perBeat;
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
