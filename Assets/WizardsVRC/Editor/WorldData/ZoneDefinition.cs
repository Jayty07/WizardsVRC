using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    public enum ZonePointKind
    {
        /// <summary>Click it: advances Interact quest steps with this point's key.</summary>
        Interact,
        /// <summary>Walk into it: advances Explore quest steps with this point's key.</summary>
        Explore,
        /// <summary>Just scenery (and a guide-arrow target if a quest step names it).</summary>
        Decoration,
    }

    public enum ZonePointLook { StreetLamp, GlowingItem, Crate, Signpost, None }

    /// <summary>
    /// One explorable area (a street, a dungeon...). The layout is a text map: each character is one square cell
    /// (see the legend in the inspector). Saved as a text asset in Assets/WizardsVRC/WorldData so it is versioned
    /// with the repo; the scene builder generates the whole zone from it on every rebuild.
    /// </summary>
    [CreateAssetMenu(fileName = "Zone", menuName = "WizardsVRC/Zone")]
    public class ZoneDefinition : ScriptableObject
    {
        public const string Legend =
            "#  building (solid)        =  low wall (solid)       ^  tree (solid)\n" +
            ".  road                    ,  grass / courtyard      S  entrance (spawn + door back to the castle)\n" +
            "1-9  battle ring spot for encounter 1-9 (needs a 3x3 open plaza)\n" +
            "a-i  patrol route of encounter 1-9 (a = 1, b = 2...): a 1-cell-wide line or loop of letters\n" +
            "Other letters/symbols: NPCs and points from the lists below (each stands on road).";

        [Tooltip("Unique key, used in names and by quests.")]
        public string zoneKey = "NewZone";
        public string displayName = "New Zone";
        public string levelRange = "Lv 1-3";
        [Tooltip("Untick to leave this zone out of the build.")]
        public bool includeInBuild = true;

        [Header("Placement")]
        [Tooltip("Height the zone is built at, far from the castle (0), keep interior (200) and arena (400). " +
                 "Give every zone its own height at least 100 apart: 600, 700, 800...")]
        public float height = 600f;
        [Tooltip("Size of one map cell in metres. 8 makes 1-cell roads 8 m wide and 3x3 ring plazas 24 m across.")]
        [Range(4f, 16f)] public float cellSize = 8f;

        [Header("Layout")]
        [TextArea(12, 60)] public string map = "#####\n#.S.#\n#####";

        [Header("Look")]
        public Color groundColor = new Color(0.13f, 0.16f, 0.13f);
        public Color roadColor = new Color(0.24f, 0.22f, 0.27f);
        public Color grassColor = new Color(0.18f, 0.24f, 0.16f);
        public Color buildingColor = new Color(0.33f, 0.27f, 0.36f);
        public Color roofColor = new Color(0.16f, 0.1f, 0.2f);
        public Color lowWallColor = new Color(0.3f, 0.28f, 0.3f);
        public Color treeColor = new Color(0.12f, 0.25f, 0.14f);
        [Tooltip("Lamp glow and other accents.")]
        public Color accentColor = new Color(1f, 0.7f, 0.35f);
        [Tooltip("Random building height range (metres).")]
        public Vector2 buildingHeight = new Vector2(7f, 14f);
        [Tooltip("Decorative street lamps along the roads (0 = none, 1 = lots).")]
        [Range(0f, 1f)] public float lampDensity = 0.25f;

        [Header("Door from the castle")]
        [Tooltip("Door position in the castle (Hub-local coordinates).")]
        public Vector3 hubDoorPosition = new Vector3(0f, 2.5f, -81f);
        public float hubDoorYaw;
        [Tooltip("Where you appear in the castle when you come back (Hub-local).")]
        public Vector3 hubReturnPosition = new Vector3(0f, 0.1f, -72f);
        public float hubReturnYaw = 180f;

        [Header("Contents")]
        public List<ZoneNpc> npcs = new List<ZoneNpc>();
        public List<ZonePoint> points = new List<ZonePoint>();
        public List<ZoneEncounter> encounters = new List<ZoneEncounter>();

        /// <summary>Map rows, padded to the same width with '#'.</summary>
        public string[] Rows()
        {
            string[] raw = (map ?? "").Replace("\r", "").Split('\n');
            var rows = new List<string>();
            foreach (string r in raw) if (r.Trim().Length > 0) rows.Add(r.TrimEnd());
            int w = 0;
            foreach (string r in rows) w = Mathf.Max(w, r.Length);
            for (int i = 0; i < rows.Count; i++) rows[i] = rows[i].PadRight(w, '#');
            return rows.ToArray();
        }

        public ZoneNpc FindNpc(char c)
        {
            foreach (var n in npcs) if (n != null && n.Symbol == c) return n;
            return null;
        }

        public ZonePoint FindPoint(char c)
        {
            foreach (var p in points) if (p != null && p.Symbol == c) return p;
            return null;
        }

        public ZoneEncounter FindEncounter(char ringDigit)
        {
            foreach (var e in encounters) if (e != null && e.Symbol == ringDigit) return e;
            return null;
        }
    }

    [Serializable]
    public class ZoneNpc
    {
        [Tooltip("Map character that places this NPC (one cell).")]
        public string symbol = "T";
        [Tooltip("Key quests use for Talk steps and givers, e.g. Tilda.")]
        public string key = "NewNpc";
        public string displayName = "New NPC";
        public Color robeColor = new Color(0.3f, 0.3f, 0.5f);
        public Color hatColor = new Color(0.15f, 0.15f, 0.3f);
        public bool beard;
        [Tooltip("Facing in degrees (0 = north/up the map). -1 = face the nearest road.")]
        public float facing = -1f;
        [TextArea(1, 4)]
        [Tooltip("What they say when no quest needs them, one line per row.")]
        public string[] defaultDialogue = new[] { "Hello there, young wizard." };
        [Tooltip("Optional voice clips: Audio/Voice/<prefix>_00.wav, _01... (blank = none).")]
        public string voicePrefix = "";

        public char Symbol { get { return string.IsNullOrEmpty(symbol) ? '\0' : symbol[0]; } }
    }

    [Serializable]
    public class ZonePoint
    {
        [Tooltip("Map character that places this point. Several cells with the same character = several of them.")]
        public string symbol = "L";
        [Tooltip("Key quests use for Interact / Explore steps, e.g. Lamp.")]
        public string key = "NewPoint";
        public ZonePointKind kind = ZonePointKind.Interact;
        public ZonePointLook look = ZonePointLook.GlowingItem;
        [Tooltip("Text floating over it (blank = none).")]
        public string label = "";
        [Tooltip("Explore triggers: size in cells (1 = one cell).")]
        [Min(0.25f)] public float triggerSizeCells = 1f;

        public char Symbol { get { return string.IsNullOrEmpty(symbol) ? '\0' : symbol[0]; } }
    }

    [Serializable]
    public class ZoneEncounter
    {
        [Tooltip("Ring digit 1-9 in the map. Its patrol route uses the matching letter (1 = a, 2 = b ... 9 = i).")]
        public string ringSymbol = "1";
        [Tooltip("Unique name for this encounter.")]
        public string key = "Encounter";
        [Tooltip("Monster ids in the fight. The first takes the boss seat.")]
        public List<int> roster = new List<int> { 1 };
        [Tooltip("How many monsters walk the patrol route.")]
        [Range(0, 6)] public int wanderers = 2;
        [Tooltip("Patrol walking speed (m/s).")]
        public float patrolSpeed = 1.4f;
        [Tooltip("Seconds after a fight before the patrol comes back.")]
        public int respawnSeconds = 20;
        [Tooltip("Spawn only the first monster plus one per wizard (good for bosses with adds).")]
        public bool scaleWithParty;

        public char Symbol { get { return string.IsNullOrEmpty(ringSymbol) ? '\0' : ringSymbol[0]; } }
        public char PatrolSymbol { get { char d = Symbol; return d >= '1' && d <= '9' ? (char)('a' + (d - '1')) : '\0'; } }
    }
}
