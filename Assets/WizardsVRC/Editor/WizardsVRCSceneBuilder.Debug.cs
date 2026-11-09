using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Debug buttons (DebugTools): a panel of testing shortcuts opened from the book's Settings page. Off unless
    /// WizardsVRC > Options > Debug Buttons is ticked when you rebuild (or Debug Enabled is ticked on Systems/DebugTools in
    /// the scene). Allowed Names on that object limits them to certain VRChat display names. Turn them off before you
    /// publish: anyone they're shown to can give themselves levels, gold and Starshards.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        private const string DebugButtonsPref = "WizardsVRC.DebugButtons";
        private const string DebugButtonsMenu = "WizardsVRC/Options/Debug Buttons";
        private static GameObject DebugPanel;

        private static bool DebugButtonsOn { get { return EditorPrefs.GetBool(DebugButtonsPref, false); } }

        [MenuItem(DebugButtonsMenu)]
        private static void ToggleDebugButtons()
        {
            EditorPrefs.SetBool(DebugButtonsPref, !DebugButtonsOn);
            Debug.Log("[WizardsVRC] Debug buttons: " + (DebugButtonsOn ? "on from the next rebuild (or tick Debug Enabled on Systems/DebugTools now)." : "off from the next rebuild."));
        }

        [MenuItem(DebugButtonsMenu, true)]
        private static bool ToggleDebugButtonsCheck()
        {
            Menu.SetChecked(DebugButtonsMenu, DebugButtonsOn);
            return true;
        }

        /// <summary>The debug panel (a world-space window like the shops') and its DebugTools.</summary>
        private static DebugTools BuildDebugTools(Transform systems, PlayerState playerState, QuestManager questManager, CardDatabase cardDb, PlayerHUD hud, InventoryBook book)
        {
            var tools = UdonSharpUndo.AddComponent<DebugTools>(Child(systems, "DebugTools", Vector3.zero).gameObject);
            tools.debugEnabled = DebugButtonsOn;
            tools.playerState = playerState;
            tools.questManager = questManager;
            tools.cardDatabase = cardDb;
            tools.hud = hud;
            book.debugTools = tools;
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(tools);

            var go = new GameObject("DebugPanel", typeof(RectTransform));
            go.transform.SetParent(systems, false);
            MakeWorldCanvas(go, new Vector2(1200, 1000), Vector3.zero, 0.0006f);
            var rt = (RectTransform)go.transform;
            tools.panelRoot = go;
            DebugPanel = go;
            Panel(rt, "Background", Vector2.zero, new Vector2(1200, 1000), new Color(0.12f, 0.1f, 0.16f, 0.97f));
            Panel(rt, "Stripe", new Vector2(0, 440), new Vector2(1200, 120), new Color(0.55f, 0.18f, 0.12f));
            Text title = Label(rt, "Title", new Vector2(0, 452), new Vector2(1100, 70), "Debug Buttons", 52, TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.9f, 0.6f);
            Label(rt, "Warning", new Vector2(0, 405), new Vector2(1100, 34), "Testing only: these change your wizard and save at once. Turn debug buttons off before publishing.", 20, TextAnchor.MiddleCenter)
                .color = new Color(1f, 0.75f, 0.6f);
            tools.infoLabel = Label(rt, "Info", new Vector2(0, 345), new Vector2(1120, 44), "", 24, TextAnchor.MiddleCenter);
            tools.infoLabel.color = new Color(0.8f, 0.9f, 1f);

            // label, event, colour group (0 progress, 1 currency, 2 spells, 3 quests)
            string[,] buttons =
            {
                { "Level +1", nameof(DebugTools.LevelUp), "0" }, { "Level +5", nameof(DebugTools.LevelUp5), "0" },
                { "Max level (50)", nameof(DebugTools.MaxLevel), "0" }, { "Training points +5", nameof(DebugTools.AddTrainingPoints), "0" },
                { "Gold +1,000", nameof(DebugTools.AddGold), "1" }, { "Starshards +100", nameof(DebugTools.AddStarshards), "1" },
                { "Full health and mana", nameof(DebugTools.FullHeal), "1" }, { "Treasure cards (3 of each)", nameof(DebugTools.AddTreasureCards), "1" },
                { "Learn my school's spells", nameof(DebugTools.LearnSchool), "2" }, { "Learn every spell", nameof(DebugTools.LearnAll), "2" },
                { "Next step of tracked quest", nameof(DebugTools.AdvanceQuest), "3" }, { "Finish tracked quest", nameof(DebugTools.FinishQuest), "3" },
            };
            Color[] groups = { new Color(0.25f, 0.42f, 0.62f), new Color(0.6f, 0.45f, 0.15f), new Color(0.45f, 0.3f, 0.62f), new Color(0.25f, 0.5f, 0.32f) };
            int n = buttons.GetLength(0);
            for (int k = 0; k < n; k++)
            {
                int col = k % 2, row = k / 2;
                GameObject b = Button(rt, "Debug" + k, new Vector2(col == 0 ? -280 : 280, 250 - row * 104), new Vector2(520, 88), buttons[k, 0],
                    groups[int.Parse(buttons[k, 2])], target, buttons[k, 1]);
                b.GetComponentInChildren<Text>().fontSize = 28;
            }
            tools.statusLabel = Label(rt, "Status", new Vector2(0, -375), new Vector2(1100, 50), "", 28, TextAnchor.MiddleCenter);
            tools.statusLabel.color = new Color(1f, 0.92f, 0.6f);
            Button(rt, "Close", new Vector2(0, -445), new Vector2(260, 66), "Close", new Color(0.45f, 0.25f, 0.15f), target, nameof(DebugTools.Close));
            go.SetActive(false);
            ExtraBehaviours.Add(tools);
            if (tools.debugEnabled)
                Debug.LogWarning("[WizardsVRC] Debug buttons are ON (book > Settings > Debug). Anyone they're shown to can give themselves levels, gold and " +
                                 "Starshards: untick WizardsVRC > Options > Debug Buttons and rebuild (or untick Debug Enabled on Systems/DebugTools) before publishing.");
            return tools;
        }
    }
}
