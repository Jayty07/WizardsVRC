using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// Placeholder reward source for testing the backpack: interact to receive a random item and some gold.
    /// Will be replaced by combat/quest rewards later.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LootChest : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public Text messageLabel;
        public int[] lootTable = { 10, 11, 20, 21, 22, 23, 24, 25, 26, 27, 28, 30, 31, 40, 41, 50, 51 };
        public int minGold = 5;
        public int maxGold = 25;
        public float cooldownSeconds = 2f;

        private float _nextUse;

        private void Start()
        {
            SetMessage("Practice Chest\n(click to open)");
        }

        public override void Interact()
        {
            if (Time.time < _nextUse) return;
            if (playerState == null || !playerState.HasCharacter() || itemDatabase == null)
            {
                SetMessage("Choose a character first.");
                return;
            }
            _nextUse = Time.time + cooldownSeconds;

            CharacterData c = playerState.current;
            int itemId = lootTable[Random.Range(0, lootTable.Length)];
            int gold = Random.Range(minGold, maxGold + 1);
            c.gold += gold;
            string msg = "+" + gold.ToString() + " gold";
            if (c.AddItem(itemId, 1)) msg = "Found " + itemDatabase.GetName(itemId) + "!\n" + msg;
            else msg = "Backpack full!\n" + msg;
            playerState.MarkDirty();
            SetMessage(msg);
        }

        private void SetMessage(string text)
        {
            if (messageLabel != null) messageLabel.text = text;
        }
    }
}
