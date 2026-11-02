using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// High Noon in Archrock: the town's days run on the server clock (one every dayMinutes), and for the few minutes
    /// round each noon the High Noon Circle's duels pay a bonus. Win a duel there at noon for extra Starshards (and High
    /// Noon wins toward the High Noon Duelist title). Everyone sees the same clock; the bonus is your own.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class HighNoon : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public NotificationCenter notifications;
        [Tooltip("Real minutes in one Archrock day.")]
        public int dayMinutes = 20;
        [Tooltip("Minutes the noon bonus lasts.")]
        public int noonMinutes = 3;
        public int bonusShards = 5;
        [Tooltip("The sign by the circle.")]
        public Text label;
        [Tooltip("Switched on while it's High Noon (sun glare, banners).")]
        public GameObject noonVisual;

        private float _next;
        private bool _wasNoon;

        private int SecondsIntoDay()
        {
            int day = Mathf.Max(60, dayMinutes * 60);
            int t = Networking.GetServerTimeInMilliseconds() / 1000;
            t = t % day;
            if (t < 0) t += day;
            return t;
        }

        /// <summary>True during the noon window (it starts at the top of each day).</summary>
        public bool IsNoon() { return SecondsIntoDay() < noonMinutes * 60; }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 1f;
            int t = SecondsIntoDay();
            bool noon = t < noonMinutes * 60;
            if (noonVisual != null && noonVisual.activeSelf != noon) noonVisual.SetActive(noon);
            if (label != null)
            {
                if (noon)
                {
                    int left = noonMinutes * 60 - t;
                    label.text = "IT'S HIGH NOON!\nWin a duel in the circle: +" + bonusShards.ToString() + " Starshards (" + Clock(left) + " left)";
                }
                else
                {
                    int until = Mathf.Max(60, dayMinutes * 60) - t;
                    label.text = "High Noon Circle\nNext High Noon in " + Clock(until);
                }
            }
            if (noon && !_wasNoon && notifications != null && playerState != null && playerState.HasCharacter() && playerState.current.level >= 13)
                notifications.Post(NotificationCenter.KIND_INFO, "High Noon in Archrock", "Duels in the High Noon Circle pay +" + bonusShards.ToString() + " Starshards for the next " + noonMinutes.ToString() + " minutes.", 0);
            _wasNoon = noon;
        }

        private string Clock(int seconds)
        {
            int s = seconds % 60;
            return (seconds / 60).ToString() + ":" + (s < 10 ? "0" : "") + s.ToString();
        }

        /// <summary>BattleClient: you won a duel in the High Noon Circle. Returns the line for the result (blank when it isn't noon).</summary>
        public string DuelWon()
        {
            if (!IsNoon() || playerState == null || !playerState.HasCharacter()) return "";
            CharacterData c = playerState.current;
            c.highNoonWins++;
            c.starshards += bonusShards;
            playerState.MarkDirty();
            return "\nHigh Noon bonus: +" + bonusShards.ToString() + " Starshards!";
        }
    }
}
