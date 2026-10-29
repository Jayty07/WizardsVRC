using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardsVRC.EditorTools.WorldData
{
    public enum QuestStepKind
    {
        /// <summary>Talk to an NPC (npc key). Optional dialogue plays when the step is handed in.</summary>
        Talk,
        /// <summary>Win fights that include monster id, count times (each monster in the fight counts once).</summary>
        Defeat,
        /// <summary>Quest items that drop from monster id (60% per kill, guaranteed on the last one needed).</summary>
        Collect,
        /// <summary>Click zone points with this key, count times (e.g. relight 3 lamps).</summary>
        Interact,
        /// <summary>Walk into the zone point with this key.</summary>
        Explore,
        /// <summary>Win the fight against boss monster id.</summary>
        Boss,
    }

    /// <summary>
    /// A chain of quests (a questline), saved as a text asset in Assets/WizardsVRC/WorldData. Make as many as you like:
    /// one per street, side quests, etc. The scene builder loads every Questline asset in that folder.
    /// </summary>
    [CreateAssetMenu(fileName = "Questline", menuName = "WizardsVRC/Questline")]
    public class Questline : ScriptableObject
    {
        [Tooltip("Untick to leave this questline out of the build (handy while you're still writing it).")]
        public bool includeInBuild = true;
        [Tooltip("Name shown for this storyline (blank = the asset's name).")]
        public string storylineName = "";
        [Tooltip("Save id of the quest that finishes this storyline (its part of the world's goal). -1 = the last quest in the list. " +
                 "Quests after it, or that don't lead to it, are side quests.")]
        public int storylineEnd = -1;
        public List<QuestEntry> quests = new List<QuestEntry>();

        public string StorylineName() { return string.IsNullOrEmpty(storylineName) ? name : storylineName; }

        /// <summary>The storyline's last quest (save id), or -1 when the questline is empty.</summary>
        public int StorylineEnd()
        {
            if (storylineEnd >= 0) return storylineEnd;
            for (int i = quests.Count - 1; i >= 0; i--) if (quests[i] != null) return quests[i].saveId;
            return -1;
        }
    }

    [Serializable]
    public class QuestEntry
    {
        [Tooltip("Where the player's progress is saved (2-511, unique across all questlines). 0 = The Prophecy and " +
                 "1 = Trial of the School are built in. Never reuse or change a save id once players have played it.")]
        public int saveId = 2;
        public string title = "New Quest";
        [TextArea(2, 6)] public string description = "";

        [Header("Starting")]
        [Tooltip("On: starts by itself once the prerequisite and level are met. Off: starts when you talk to the giver.")]
        public bool startsAutomatically = true;
        [Tooltip("NPC that offers the quest when it doesn't start automatically (zone NPC key, Headmaster, OwnProfessor or a school name).")]
        public string giverNpc = "";
        [TextArea(1, 4)]
        [Tooltip("What the giver says when offering the quest, one line per row (optional; blank = their default lines).")]
        public string[] offerDialogue = new string[0];
        [Tooltip("Optional voice clips for the offer dialogue: Audio/Voice/<prefix>_00.wav, _01...")]
        public string offerVoicePrefix = "";
        [Tooltip("Save id of the quest that must be finished first (-1 = none, 0 = The Prophecy).")]
        public int prerequisite = -1;
        [Tooltip("More quests that must all be finished first (save ids). A world's finale gets every storyline added automatically.")]
        public List<int> alsoRequires = new List<int>();
        [Range(1, 100)] public int minLevel = 1;

        [Header("Rewards")]
        public int xp = 50;
        public int gold = 20;
        [Tooltip("Item id given on completion (0 = none).")]
        public int rewardItem;
        [Tooltip("Spell card id learned on completion (0 = none, -1 = the player's own school's quest spell).")]
        public int rewardSpell;

        [Tooltip("Up to 8 steps, done in order.")]
        public List<QuestStepEntry> steps = new List<QuestStepEntry>();
    }

    [Serializable]
    public class QuestStepEntry
    {
        public QuestStepKind kind = QuestStepKind.Talk;
        [Tooltip("Talk: zone NPC key (e.g. Tilda), Headmaster, OwnProfessor, or a school name (Fire...Balance) for that professor.")]
        public string npc = "";
        [Tooltip("Defeat / Collect / Boss: monster id from the Monster Catalog.")]
        public int monsterId;
        [Tooltip("Interact / Explore: zone point key (e.g. Lamp).")]
        public string point = "";
        [Min(1)] public int count = 1;
        [Tooltip("Objective shown in the quest log and HUD, e.g. \"Defeat Gloom Wisps\" (the count is added for you).")]
        public string objective = "";
        [Tooltip("Name the guide arrow shows (blank = picked for you).")]
        public string guideLabel = "";
        [TextArea(1, 4)]
        [Tooltip("Talk steps: what the NPC says when you hand this step in, one line per row (blank = their default lines).")]
        public string[] dialogue = new string[0];
        [Tooltip("Optional voice clips for the dialogue: Audio/Voice/<prefix>_00.wav, _01... (blank = none).")]
        public string voicePrefix = "";
    }
}
