using UdonSharp;
using UdonSharpEditor;
using UnityEngine;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The level reward track (LevelRewards): something for every level from 2 to 25. Starfall Isle's part (2-13) is
    /// potions, gold, Starshards and elixirs; Dustwind's (14-25) is the Frontier gear set, two decks and its titles.
    /// Training points and new spells come with their levels as before; the notes just say so.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private struct LevelReward
        {
            public int level, item, count, gold, shards, elixir;
            public string note;
            public LevelReward(int level, int item, int count, int gold, int shards, int elixir, string note)
            {
                this.level = level; this.item = item; this.count = count; this.gold = gold; this.shards = shards; this.elixir = elixir; this.note = note;
            }
        }

        // Elixir + 1: 1 Fury, 2 Warding, 3 Focus, 4 Fortune, 5 Swiftness, 6 Wisdom (Cosmetics.cs ElixirNames).
        private static readonly LevelReward[] LevelRewardTrack =
        {
            // ---- Starfall Isle
            new LevelReward(2, 10, 3, 0, 0, 0, ""),
            new LevelReward(3, 11, 2, 50, 0, 0, ""),
            new LevelReward(4, 0, 0, 0, 2, 0, "a training point"),
            new LevelReward(5, 117, 1, 0, 0, 0, "new spells at your professor"),
            new LevelReward(6, 114, 2, 0, 0, 0, ""),
            new LevelReward(7, 0, 0, 120, 0, 5, ""),
            new LevelReward(8, 0, 0, 0, 3, 0, "a training point"),
            new LevelReward(9, 115, 2, 0, 0, 0, ""),
            new LevelReward(10, 0, 0, 200, 0, 0, "your school's aura and new spells"),
            new LevelReward(11, 0, 0, 0, 0, 6, ""),
            new LevelReward(12, 0, 0, 0, 5, 0, "a training point and new spells"),
            new LevelReward(13, 188, 2, 300, 5, 0, "the portal room's second arch is waiting"),
            // ---- Dustwind
            new LevelReward(14, 162, 1, 0, 0, 0, ""),
            new LevelReward(15, 163, 1, 0, 0, 0, ""),
            new LevelReward(16, 164, 1, 0, 0, 0, "a training point and new spells"),
            new LevelReward(17, 0, 0, 0, 5, 0, "more bounties on Archrock's board"),
            new LevelReward(18, 165, 1, 0, 0, 0, ""),
            new LevelReward(19, 0, 0, 300, 0, 4, ""),
            new LevelReward(20, 166, 1, 0, 0, 0, "the Trailblazer title and a training point"),
            new LevelReward(21, 167, 1, 0, 0, 0, ""),
            new LevelReward(22, 188, 3, 0, 0, 0, "Old Silas at Lonesome Bend has a job for you, and new spells"),
            new LevelReward(23, 168, 1, 0, 10, 0, ""),
            new LevelReward(24, 169, 1, 0, 0, 0, ""),
            new LevelReward(25, 189, 3, 0, 10, 0, "the Sunstone aura, the Sheriff of Dustwind title and a training point"),
        };

        private static LevelRewards BuildLevelRewards(Transform systems, PlayerState playerState, ItemDatabase itemDb, NotificationCenter notices, CosmeticsDatabase cosmetics)
        {
            var lr = UdonSharpUndo.AddComponent<LevelRewards>(Child(systems, "LevelRewards", Vector3.zero).gameObject);
            lr.playerState = playerState;
            lr.itemDatabase = itemDb;
            lr.notifications = notices;
            lr.cosmetics = cosmetics;
            int n = LevelRewardTrack.Length;
            lr.levels = new int[n];
            lr.items = new int[n];
            lr.itemCounts = new int[n];
            lr.gold = new int[n];
            lr.shards = new int[n];
            lr.elixirs = new int[n];
            lr.notes = new string[n];
            for (int i = 0; i < n; i++)
            {
                var r = LevelRewardTrack[i];
                if (r.item > 0 && itemDb.IndexOf(r.item) < 0) Debug.LogWarning("[WizardsVRC] Level " + r.level + "'s reward item " + r.item + " isn't in the ItemDatabase.");
                lr.levels[i] = r.level;
                lr.items[i] = r.item;
                lr.itemCounts[i] = r.count;
                lr.gold[i] = r.gold;
                lr.shards[i] = r.shards;
                lr.elixirs[i] = r.elixir;
                lr.notes[i] = r.note ?? "";
            }
            ExtraBehaviours.Add(lr);
            return lr;
        }
    }
}
