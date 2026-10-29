using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    /// <summary>
    /// A world (Wizard101 style): a goal set out in the area, at least ten storylines (questlines) that all build toward
    /// it, and a finale that needs every one of them. Finishing the finale is what lets players progress to the next
    /// world (its gates and first quests require it). Saved in Assets/WizardsVRC/WorldData like the other world data.
    /// </summary>
    [CreateAssetMenu(fileName = "World", menuName = "WizardsVRC/World")]
    public class WorldArc : ScriptableObject
    {
        /// <summary>Storylines a world should have before players move on (checked, not enforced).</summary>
        public const int MinStorylines = 10;

        [Tooltip("Untick to leave this world out of the build.")]
        public bool includeInBuild = true;
        [Tooltip("1 = the first world, 2 = the next... Each world's storylines must come after the previous world's finale.")]
        public int order = 1;
        public string displayName = "New World";
        public string levelRange = "Lv 1-10";

        [Header("Goal")]
        [TextArea(2, 5)]
        [Tooltip("What the whole world builds toward, as the quest log shows it, e.g. \"Find what is draining the Starfall Crystal and rekindle it.\"")]
        public string goal = "";
        [Tooltip("Save id of the quest that sets the goal out in the area (an NPC there explains the threat). Every storyline should start after it. -1 = none.")]
        public int goalQuest = -1;
        [Tooltip("Save id of the world's finale. It automatically needs the end of every storyline below. The next world's gates and first quests should require it.")]
        public int finaleQuest = -1;

        [Header("Storylines (10 or more)")]
        [Tooltip("The questlines that lead up to the goal. Each counts as finished at its Storyline End quest. Side quests can live in these too.")]
        public List<Questline> storylines = new List<Questline>();
        [Tooltip("Zone keys of the areas in this world (for the checks).")]
        public List<string> zones = new List<string>();
    }
}
