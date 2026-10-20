using UdonSharp;
using UnityEngine;

namespace WizardsVRC
{
    /// <summary>
    /// Click an NPC: with one option the event fires directly; with several, the shared <see cref="InteractionMenu"/>
    /// lists them (e.g. a professor's "Talk" and "Learn Spells"). Option i sends events[i] to targets[i].
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NpcInteractions : UdonSharpBehaviour
    {
        public string title = "";
        public InteractionMenu menu;
        public string[] labels;
        public UdonSharpBehaviour[] targets;
        public string[] events;

        public int OptionCount()
        {
            if (labels == null || targets == null || events == null) return 0;
            return Mathf.Min(Mathf.Min(labels.Length, targets.Length), Mathf.Min(events.Length, InteractionMenu.MAX_OPTIONS));
        }

        public string LabelAt(int i)
        {
            return i >= 0 && i < OptionCount() ? labels[i] : "";
        }

        public override void Interact()
        {
            int n = OptionCount();
            if (n == 1 || (n > 1 && menu == null)) Choose(0);
            else if (n > 1) menu.Open(this);
        }

        public void Choose(int i)
        {
            if (i < 0 || i >= OptionCount() || targets[i] == null || events[i] == "") return;
            targets[i].SendCustomEvent(events[i]);
        }
    }
}
