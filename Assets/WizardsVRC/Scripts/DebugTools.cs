using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Debug buttons for testing, on a panel opened from the book's Settings page (its Debug button only shows while debug
    /// buttons are enabled). Enable them with WizardsVRC > Options > Debug Buttons before a rebuild, or tick Debug Enabled
    /// on this object in the scene. With Allowed Names filled in, only those VRChat display names get them.
    /// Everything works on your own wizard only and is saved at once: levels, gold, Starshards, training points, full
    /// health and mana, learning spells, treasure cards, and moving the tracked quest on.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DebugTools : UdonSharpBehaviour
    {
        [Tooltip("Show the debug buttons (the builder sets this from WizardsVRC > Options > Debug Buttons). Turn off before publishing.")]
        public bool debugEnabled;
        [Tooltip("Only these VRChat display names get the debug buttons (empty = everyone, while enabled).")]
        public string[] allowedNames = new string[0];

        public PlayerState playerState;
        public QuestManager questManager;
        public CardDatabase cardDatabase;
        public PlayerHUD hud;
        public UiAudio uiAudio;

        [Header("Panel")]
        public GameObject panelRoot;
        public Text statusLabel;
        public Text infoLabel;
        public float openDistance = 0.9f;

        private bool _open;
        private Vector3 _openPos;

        /// <summary>The local player may use the debug buttons.</summary>
        public bool Allowed()
        {
            if (!debugEnabled) return false;
            if (allowedNames == null || allowedNames.Length == 0) return true;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return false;
            string n = me.displayName;
            for (int i = 0; i < allowedNames.Length; i++) if (allowedNames[i] == n) return true;
            return false;
        }

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public void Open()
        {
            if (!Allowed() || panelRoot == null) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me))
            {
                VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 f = head.rotation * Vector3.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
                f.Normalize();
                panelRoot.transform.SetPositionAndRotation(head.position + f * openDistance + Vector3.down * 0.1f, Quaternion.LookRotation(f, Vector3.up));
                _openPos = me.GetPosition();
            }
            _open = true;
            panelRoot.SetActive(true);
            Status("");
        }

        public void Close()
        {
            _open = false;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Update()
        {
            if (!_open) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            // Closes if you walk away from it (or debug buttons get turned off).
            if (!Allowed() || (Utilities.IsValid(me) && Vector3.Distance(me.GetPosition(), _openPos) > 6f)) Close();
        }

        // ------------------------------------------------------------------ the buttons

        public void LevelUp() { GainLevels(1); }
        public void LevelUp5() { GainLevels(5); }
        public void MaxLevel() { GainLevels(CharacterData.MAX_LEVEL); }

        public void AddGold()
        {
            if (!Ready()) return;
            playerState.current.gold += 1000;
            Done("+1,000 gold.");
        }

        public void AddStarshards()
        {
            if (!Ready()) return;
            playerState.current.starshards += 100;
            Done("+100 Starshards.");
        }

        public void AddTrainingPoints()
        {
            if (!Ready()) return;
            playerState.current.trainingPoints += 5;
            Done("+5 training points.");
        }

        public void FullHeal()
        {
            if (!Ready()) return;
            playerState.SetHealth(playerState.MaxHealth());
            playerState.SetMana(playerState.MaxMana());
            Done("Health and mana full.");
        }

        public void LearnSchool()
        {
            if (!Ready() || cardDatabase == null) return;
            CharacterData c = playerState.current;
            int n = 0;
            for (int i = 0; i < cardDatabase.Count(); i++)
            {
                int id = cardDatabase.IdAt(i);
                if (cardDatabase.GetSchool(id) == (int)c.school && c.Learn(id)) n++;
            }
            Done("Learned " + n.ToString() + " " + cardDatabase.SchoolName((int)c.school) + " spells.");
        }

        public void LearnAll()
        {
            if (!Ready() || cardDatabase == null) return;
            CharacterData c = playerState.current;
            int n = 0;
            for (int i = 0; i < cardDatabase.Count(); i++)
            {
                int id = cardDatabase.IdAt(i);
                if (cardDatabase.GetSchool(id) != CardDatabase.GENERAL && c.Learn(id)) n++;
            }
            Done("Learned " + n.ToString() + " spells (every school, quest, Tech and Starshard spell).");
        }

        public void AddTreasureCards()
        {
            if (!Ready() || cardDatabase == null) return;
            CharacterData c = playerState.current;
            int n = 0;
            for (int i = 0; i < cardDatabase.Count(); i++)
            {
                int id = cardDatabase.IdAt(i);
                if (cardDatabase.IsHiddenSchool(cardDatabase.GetSchool(id)) || cardDatabase.IsStarshardSpell(id)) continue;
                n += c.AddTreasure(id, 3);
            }
            Done("+" + n.ToString() + " treasure cards (3 of each). Put them in a sideboard on the Deck page.");
        }

        /// <summary>Finishes the tracked quest's current step (or starts the next quest you can take if nothing's tracked).</summary>
        public void AdvanceQuest()
        {
            if (!Ready() || questManager == null) return;
            int q = questManager.GetTrackedQuest();
            if (q < 0 || q >= questManager.QuestCount() || !questManager.IsActive(q)) { Done("No quest is being tracked."); return; }
            questManager.DebugAdvance(q);
            Done(questManager.IsComplete(q) ? "Finished: " + questManager.GetTitle(q) + "." : "Next step of " + questManager.GetTitle(q) + ".");
        }

        /// <summary>Finishes every remaining step of the tracked quest (its rewards are given as usual).</summary>
        public void FinishQuest()
        {
            if (!Ready() || questManager == null) return;
            int q = questManager.GetTrackedQuest();
            if (q < 0 || q >= questManager.QuestCount() || !questManager.IsActive(q)) { Done("No quest is being tracked."); return; }
            string title = questManager.GetTitle(q);
            for (int k = 0; k < QuestManager.MAX_STEPS + 1 && questManager.IsActive(q); k++) questManager.DebugAdvance(q);
            Done("Finished: " + title + ".");
        }

        // ------------------------------------------------------------------ helpers

        private void GainLevels(int levels)
        {
            if (!Ready()) return;
            CharacterData c = playerState.current;
            int before = c.level;
            for (int k = 0; k < levels && c.level < CharacterData.MAX_LEVEL; k++) c.AddXp(Mathf.Max(1, c.XpToNextLevel() - c.xp));
            playerState.SetHealth(playerState.MaxHealth());
            playerState.SetMana(playerState.MaxMana());
            Done("Level " + before.ToString() + " -> " + c.level.ToString() + ".");
        }

        private bool Ready()
        {
            if (!Allowed()) return false;
            if (playerState == null || !playerState.HasCharacter() || playerState.current == null) { Status("Load a wizard first."); return false; }
            if (playerState.inCombat) { Status("Not in the middle of a duel."); return false; }
            return true;
        }

        private void Done(string message)
        {
            playerState.MarkDirty();
            playerState.SaveNow();
            if (hud != null) hud.Refresh();
            if (uiAudio != null) uiAudio.PlayOk();
            Status(message);
        }

        private void Status(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
            if (infoLabel != null && playerState != null && playerState.HasCharacter() && playerState.current != null)
            {
                CharacterData c = playerState.current;
                infoLabel.text = c.characterName + "   Level " + c.level.ToString() + "   " + c.gold.ToString() + " gold   " + c.starshards.ToString() +
                                 " Starshards   " + c.trainingPoints.ToString() + " training points";
            }
        }
    }
}
