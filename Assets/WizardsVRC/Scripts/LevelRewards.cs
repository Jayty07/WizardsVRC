using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Wizard101-style level rewards: every level up to 25 gives something (an item, gold, Starshards, an elixir), on top
    /// of the training points and new spells the level already brings. Starfall Isle's track covers levels 2-13 and
    /// Dustwind's picks up from 14. Each reward is given once (CharacterData.rewardLevel), even if a fight gives you
    /// several levels at once; saves from before this existed don't get the levels they already had.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LevelRewards : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public NotificationCenter notifications;
        public CosmeticsDatabase cosmetics;

        [Header("One row per level that gives something (filled by the scene builder)")]
        public int[] levels;
        [Tooltip("Item id given (0 = none).")]
        public int[] items;
        public int[] itemCounts;
        public int[] gold;
        public int[] shards;
        [Tooltip("Elixir index + 1 (CosmeticsDatabase), 0 = none.")]
        public int[] elixirs;
        [Tooltip("Extra words for the notice (\"Bounty board opens\", a title...).")]
        public string[] notes;

        private float _next;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.75f;
            if (playerState == null || !playerState.HasCharacter() || levels == null) return;
            CharacterData c = playerState.current;
            if (c.rewardLevel >= c.level) return;
            for (int lv = c.rewardLevel + 1; lv <= c.level; lv++) Give(c, lv);
            c.rewardLevel = c.level;
            playerState.MarkDirty();
        }

        private void Give(CharacterData c, int lv)
        {
            int row = -1;
            for (int i = 0; i < levels.Length; i++) if (levels[i] == lv) { row = i; break; }
            if (row < 0) return;
            string body = "";
            int iconItem = 0;
            int item = items[row];
            if (item > 0)
            {
                int n = Mathf.Max(1, itemCounts[row]);
                string name = itemDatabase != null ? itemDatabase.GetName(item) : "an item";
                if (c.AddItem(item, n))
                {
                    body = n > 1 ? n.ToString() + " x " + name : name;
                    iconItem = item;
                }
                else
                {
                    // A full backpack: the item's worth in gold instead, so nothing is lost.
                    int worth = 40 * lv * n;
                    c.gold += worth;
                    body = "Backpack full: " + worth.ToString() + " gold instead of " + name;
                }
            }
            if (gold[row] > 0)
            {
                c.gold += gold[row];
                body = Add(body, gold[row].ToString() + " gold");
            }
            if (shards[row] > 0)
            {
                c.starshards += shards[row];
                body = Add(body, shards[row].ToString() + " Starshards");
            }
            int e = elixirs[row] - 1;
            if (e >= 0 && c.elixirSeconds != null && e < c.elixirSeconds.Length)
            {
                int max = cosmetics != null ? cosmetics.elixirMaxMinutes * 60 : 10800;
                int add = cosmetics != null ? cosmetics.elixirMinutes * 60 : 1800;
                c.elixirSeconds[e] = Mathf.Min(max, c.elixirSeconds[e] + add);
                string en = cosmetics != null && cosmetics.elixirNames != null && e < cosmetics.elixirNames.Length ? cosmetics.elixirNames[e] : "an elixir";
                body = Add(body, en + " (working now)");
            }
            if (notes[row] != null && notes[row].Length > 0) body = Add(body, notes[row]);
            if (notifications != null && body.Length > 0)
                notifications.Post(iconItem > 0 ? NotificationCenter.KIND_GEAR : NotificationCenter.KIND_INFO, "Level " + lv.ToString() + " reward", body, iconItem);
        }

        private string Add(string a, string b)
        {
            return a.Length == 0 ? b : a + ", " + b;
        }

        /// <summary>The reward for a level as one line (the book's Stats page shows what's coming next).</summary>
        public string Describe(int lv)
        {
            if (levels == null) return "";
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] != lv) continue;
                string s = "";
                if (items[i] > 0 && itemDatabase != null) s = (itemCounts[i] > 1 ? itemCounts[i].ToString() + " x " : "") + itemDatabase.GetName(items[i]);
                if (gold[i] > 0) s = Add(s, gold[i].ToString() + " gold");
                if (shards[i] > 0) s = Add(s, shards[i].ToString() + " Starshards");
                int e = elixirs[i] - 1;
                if (e >= 0 && cosmetics != null && cosmetics.elixirNames != null && e < cosmetics.elixirNames.Length) s = Add(s, cosmetics.elixirNames[e]);
                if (notes[i] != null && notes[i].Length > 0) s = Add(s, notes[i]);
                return s;
            }
            return "";
        }
    }
}
