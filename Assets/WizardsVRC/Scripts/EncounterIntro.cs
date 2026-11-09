using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A boss's cutscene, played when you walk up to its ring (Bigdig's bosses: Chief Engineer Strake, the Assayer and
    /// Marshal Brannoch): the boss, and anyone with them, speaking before the fight. It plays each time you arrive while
    /// no duel is going on there, never over another cutscene or a spell's film. Local: each player sees it themselves.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class EncounterIntro : UdonSharpBehaviour
    {
        public Cutscene cutscene;
        public CutscenePlayer cutscenePlayer;
        [Tooltip("The fight it introduces (no cutscene while it's under way).")]
        public BattleClient client;
        [Tooltip("The middle of the boss's ring.")]
        public Transform centre;
        [Tooltip("How close (m) you have to come for it to play.")]
        public float radius = 18f;

        private bool _here;
        private float _next;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || centre == null || cutscene == null || cutscenePlayer == null) return;
            Vector3 d = me.GetPosition() - centre.position;
            bool here = Mathf.Abs(d.y) < 8f && d.x * d.x + d.z * d.z < radius * radius;
            if (here && !_here)
            {
                BattleManager m = client != null ? client.manager : null;
                bool idle = m == null || m.phase == BattleManager.PHASE_IDLE || m.phase == BattleManager.PHASE_QUEUE;
                if (idle && !cutscenePlayer.IsPlaying() && !cutscenePlayer.IsFilming()) cutscenePlayer.Play(cutscene);
            }
            _here = here;
        }
    }
}
