using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// A wandering monster that walks a loop around its encounter's duel circle. Its position comes from the server
    /// clock, so every client sees it in the same place without syncing. Touching it (walking within aggroRadius)
    /// starts, or joins, the encounter's fight; it hides while the fight is on and for a while afterwards.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Wanderer : UdonSharpBehaviour
    {
        public BattleManager manager;
        public BattleClient client;
        public MonsterDatabase monsterDatabase;
        public int monsterId;
        public Transform center;
        public float radius = 12f;
        public float speed = 1.2f;
        public float phaseOffset;
        public float aggroRadius = 2.5f;
        public GameObject body;
        public Text label;

        private float _nextTry;

        private void Start()
        {
            if (label != null && monsterDatabase != null)
                label.text = monsterDatabase.GetName(monsterId) + "\nLevel " + monsterDatabase.GetLevel(monsterId).ToString();
        }

        private void Update()
        {
            if (manager == null || center == null) return;
            bool hidden = manager.WanderersHidden();
            if (body != null && body.activeSelf == hidden) body.SetActive(!hidden);
            if (label != null && label.gameObject.activeSelf == hidden) label.gameObject.SetActive(!hidden);
            if (hidden) return;

            float t = (Networking.GetServerTimeInMilliseconds() % 3600000) / 1000f;
            float ang = phaseOffset + t * speed / Mathf.Max(1f, radius);
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 pos = center.position + dir * radius;
            Vector3 tangent = new Vector3(-dir.z, 0f, dir.x);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent, Vector3.up));

            if (Time.time < _nextTry || client == null || !client.CanFight()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 d = me.GetPosition() - pos;
            d.y = 0f;
            if (d.magnitude > aggroRadius) return;
            _nextTry = Time.time + 1.5f;
            manager.RequestEngageWander();
        }
    }
}
