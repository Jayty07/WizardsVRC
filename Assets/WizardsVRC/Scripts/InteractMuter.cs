using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace WizardsVRC
{
    /// <summary>
    /// Keeps pop-up panels (dialogue screen, NPC menu, spell trainer, new-spell pop-up, book) clickable when they open
    /// right next to something clickable. A panel opens in front of your face, so standing close to an NPC or a door can
    /// put the panel inside that thing's click box, and the click box then catches your pointer before the buttons do.
    /// While any panel is open, clickable colliders between your eyes and the panel (and just round it) are switched off;
    /// they come back when the last panel closes. Local only.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InteractMuter : UdonSharpBehaviour
    {
        [Tooltip("The panels' root objects: while one is shown, clickables in front of it are switched off.")]
        public GameObject[] panels;
        [Tooltip("Extra room (m) round the line from your eyes to the panel.")]
        public float margin = 0.7f;

        private bool[] _was;
        private Collider[] _muted = new Collider[48];
        private int _count;

        private void Start()
        {
            _was = new bool[panels == null ? 0 : panels.Length];
        }

        private void Update()
        {
            if (panels == null || _was == null) return;
            bool any = false;
            for (int i = 0; i < panels.Length && i < _was.Length; i++)
            {
                GameObject p = panels[i];
                bool on = p != null && p.activeInHierarchy;
                if (on && !_was[i]) MuteFor(p.transform);
                _was[i] = on;
                any |= on;
            }
            if (!any && _count > 0) Unmute();
        }

        private void MuteFor(Transform panel)
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            Vector3 eye = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Vector3 mid = (eye + panel.position) * 0.5f;
            float radius = Vector3.Distance(eye, panel.position) * 0.5f + margin;
            Collider[] hits = Physics.OverlapSphere(mid, radius, -1, QueryTriggerInteraction.Ignore);
            if (hits == null) return;
            for (int i = 0; i < hits.Length && _count < _muted.Length; i++)
            {
                Collider c = hits[i];
                if (c == null || !c.enabled || OnAPanel(c.transform)) continue;
                if (c.GetComponent(typeof(UdonBehaviour)) == null) continue;
                c.enabled = false;
                _muted[_count] = c;
                _count++;
            }
        }

        private bool OnAPanel(Transform t)
        {
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null && t.IsChildOf(panels[i].transform)) return true;
            return false;
        }

        private void Unmute()
        {
            for (int i = 0; i < _count; i++)
            {
                if (_muted[i] != null) _muted[i].enabled = true;
                _muted[i] = null;
            }
            _count = 0;
        }
    }
}
