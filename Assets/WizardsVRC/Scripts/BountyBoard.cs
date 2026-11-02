using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Archrock's bounty board (Dustwind, from level 17): three wanted posters at a time, each a number of one monster
    /// to defeat anywhere. Bounties count as you win battles (BattleClient), and a finished poster is turned in at the
    /// board for gold and Starshards; a fresh one goes up in its place. Turned-in bounties unlock the bounty titles
    /// (Bounty Hunter, Lawman, Legend of the West). Each player has their own posters (CharacterData.bountyIds).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class BountyBoard : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public NotificationCenter notifications;
        public UiAudio uiAudio;
        public CardDatabase cardDatabase;
        public int minLevel = 17;

        [Header("The bounties (same order)")]
        public string[] titles;
        public int[] monsters;
        public int[] counts;
        public int[] golds;
        public int[] shards;
        [Tooltip("Lowest level a bounty is posted for.")]
        public int[] levels;

        [Header("The board's panel")]
        public GameObject panelRoot;
        public Text headerLabel;
        public Text[] posterTitles;
        public Text[] posterBodies;
        public GameObject[] turnInButtons;
        public Text statusLabel;
        public float openDistance = 1.3f;

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private int Count() { return titles == null ? 0 : titles.Length; }

        public void Open()
        {
            if (playerState == null || !playerState.HasCharacter()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (Utilities.IsValid(me) && panelRoot != null)
            {
                VRCPlayerApi.TrackingData head = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                Vector3 f = head.rotation * Vector3.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
                f.Normalize();
                panelRoot.transform.SetPositionAndRotation(head.position + f * openDistance + Vector3.down * 0.1f, Quaternion.LookRotation(f, Vector3.up));
            }
            Fill();
            if (panelRoot != null) panelRoot.SetActive(true);
            if (uiAudio != null) uiAudio.PlayBookOpen();
            if (statusLabel != null) statusLabel.text = "";
            Draw();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        /// <summary>Posts a bounty in every empty slot (one for your level that isn't already up).</summary>
        private void Fill()
        {
            CharacterData c = playerState.current;
            if (c.level < minLevel || Count() == 0) return;
            bool changed = false;
            for (int s = 0; s < CharacterData.BOUNTY_SLOTS; s++)
            {
                if (c.bountyIds[s] > 0) continue;
                int pick = Pick(c, s);
                if (pick < 0) continue;
                c.bountyIds[s] = pick + 1;
                c.bountyProgress[s] = 0;
                changed = true;
            }
            if (changed) playerState.MarkDirty();
        }

        private int Pick(CharacterData c, int slot)
        {
            int n = Count();
            // Start somewhere new each time (by bounties done and slot), then take the first eligible one not already up.
            int start = (c.bountiesDone * 7 + slot * 3 + c.level) % n;
            for (int k = 0; k < n; k++)
            {
                int i = (start + k) % n;
                if (levels != null && i < levels.Length && levels[i] > c.level) continue;
                bool up = false;
                for (int s = 0; s < CharacterData.BOUNTY_SLOTS; s++) if (c.bountyIds[s] == i + 1) up = true;
                if (!up) return i;
            }
            return -1;
        }

        private void Draw()
        {
            CharacterData c = playerState.current;
            bool open = c.level >= minLevel;
            if (headerLabel != null)
                headerLabel.text = open ? "WANTED\nBounties served: " + c.bountiesDone.ToString() : "WANTED\nThe marshal posts bounties for deputies of level " + minLevel.ToString() + " and up.";
            for (int s = 0; s < CharacterData.BOUNTY_SLOTS; s++)
            {
                int i = c.bountyIds[s] - 1;
                bool has = open && i >= 0 && i < Count();
                bool done = has && c.bountyProgress[s] >= counts[i];
                if (posterTitles != null && s < posterTitles.Length && posterTitles[s] != null) posterTitles[s].text = has ? titles[i] : "";
                if (posterBodies != null && s < posterBodies.Length && posterBodies[s] != null)
                    posterBodies[s].text = has
                        ? "Defeat: " + MonsterName(monsters[i]) + " x" + counts[i].ToString() + "\n" + Mathf.Min(c.bountyProgress[s], counts[i]).ToString() + " / " + counts[i].ToString() +
                          "\nReward: " + golds[i].ToString() + " gold, " + shards[i].ToString() + " Starshards"
                        : (open ? "No poster here today." : "");
                if (turnInButtons != null && s < turnInButtons.Length && turnInButtons[s] != null) turnInButtons[s].SetActive(done);
            }
        }

        private string MonsterName(int id)
        {
            return cardDatabase != null ? cardDatabase.MonsterName(id) : "monsters";
        }

        public void TurnIn0() { TurnIn(0); }
        public void TurnIn1() { TurnIn(1); }
        public void TurnIn2() { TurnIn(2); }

        private void TurnIn(int s)
        {
            if (playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            int i = c.bountyIds[s] - 1;
            if (i < 0 || i >= Count() || c.bountyProgress[s] < counts[i]) return;
            c.gold += golds[i];
            c.starshards += shards[i];
            c.bountiesDone++;
            c.bountyIds[s] = 0;
            c.bountyProgress[s] = 0;
            playerState.MarkDirty();
            playerState.SaveNow();
            if (uiAudio != null) uiAudio.PlayOk();
            if (statusLabel != null) statusLabel.text = titles[i] + ": served! +" + golds[i].ToString() + " gold, +" + shards[i].ToString() + " Starshards.";
            if (notifications != null)
                notifications.Post(NotificationCenter.KIND_INFO, "Bounty served", titles[i] + ": +" + golds[i].ToString() + " gold, +" + shards[i].ToString() + " Starshards.", 0);
            Fill();
            Draw();
        }

        /// <summary>BattleClient: you won a battle; count its monsters toward your posted bounties.</summary>
        public void OnBattleWon(BattleManager battle)
        {
            if (battle == null || playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            if (c.level < minLevel) return;
            bool changed = false;
            for (int m = BattleManager.SIDE; m < BattleManager.SLOTS; m++)
            {
                int id = battle.MonsterAt(m);
                if (id == 0) continue;
                for (int s = 0; s < CharacterData.BOUNTY_SLOTS; s++)
                {
                    int i = c.bountyIds[s] - 1;
                    if (i < 0 || i >= Count() || monsters[i] != id || c.bountyProgress[s] >= counts[i]) continue;
                    c.bountyProgress[s]++;
                    changed = true;
                    if (c.bountyProgress[s] == counts[i] && notifications != null)
                        notifications.Post(NotificationCenter.KIND_INFO, "Bounty complete", titles[i] + ": turn it in at Archrock's bounty board.", 0);
                }
            }
            if (changed) playerState.MarkDirty();
        }
    }
}
