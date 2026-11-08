using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The pop-up you get when your mouse or VR pointer rests on an item or spell (backpack, gear slots, shops,
    /// spellbook). Gear also shows what you're wearing in that slot beside it, with each stat's change in green or red,
    /// so two pieces are easy to compare. One tooltip serves every menu: it moves next to whatever you point at.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ItemTooltip : UdonSharpBehaviour
    {
        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public CardDatabase cardDatabase;
        public GameObject root;
        public Text nameLabel;
        public Text bodyLabel;
        public GameObject compareRoot;
        public Text compareName;
        public Text compareBody;
        [Tooltip("How far right of the pointed-at element the pop-up sits (m), and how far towards you.")]
        public float sideOffset = 0.2f;
        public float forwardOffset = 0.03f;
        [Tooltip("The book's canvas: the pop-up grows and shrinks with it (the book opens bigger in VR, smaller when it has to come closer).")]
        public Transform scaleWith;
        private Vector3 _baseScale;
        private float _refScale;

        private void Start()
        {
            if (root != null) _baseScale = root.transform.localScale;
            if (scaleWith != null) _refScale = scaleWith.localScale.x;
            Hide();
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        private void PlaceBeside(Transform anchor)
        {
            if (root == null || anchor == null) return;
            Transform t = root.transform;
            float k = 1f;
            if (scaleWith != null && _refScale > 0f && anchor.IsChildOf(scaleWith)) k = scaleWith.localScale.x / _refScale;
            if (_baseScale != Vector3.zero) t.localScale = _baseScale * k;
            t.rotation = anchor.rotation;
            t.position = anchor.position + anchor.right * sideOffset * k - anchor.forward * forwardOffset * k;
            root.SetActive(true);
        }

        public void ShowItem(int itemId, Transform anchor)
        {
            if (itemDatabase == null || itemId == 0) { Hide(); return; }
            CharacterData c = playerState != null && playerState.HasCharacter() ? playerState.current : null;
            int cat = itemDatabase.GetCategory(itemId);
            if (nameLabel != null)
                nameLabel.text = "<color=#" + Hex(itemDatabase.GetIconColor(itemId)) + ">" + itemDatabase.GetName(itemId) + "</color>\n<size=22>" +
                                 itemDatabase.GetCategoryName(cat) + "</size>";
            string body = itemDatabase.GetDescription(itemId) + "\n\n" + itemDatabase.GetStatSummary(itemId);
            string cards = itemDatabase.ItemCardTextFor(itemId, c != null ? (int)c.school : -1);
            if (cards.Length > 0) body += cards + "\n";
            int worn = 0;
            if (c != null && itemDatabase.IsEquippable(itemId) && cat >= 0 && cat < CharacterData.EQUIP_SLOTS) worn = c.equipped[cat];
            if (c != null && itemDatabase.IsEquippable(itemId))
            {
                string why = itemDatabase.CheckCanEquip(c, itemId);
                if (why.Length > 0) body += "<color=#FF7060>" + why + "</color>\n";
            }
            bool compare = worn != 0 && worn != itemId;
            if (compare)
            {
                // Changes if you swap: green better, red worse.
                string diff = "";
                for (int s = 0; s < ItemDatabase.STAT_COUNT; s++)
                {
                    int d = itemDatabase.GetStat(itemId, s) - itemDatabase.GetStat(worn, s);
                    if (d == 0) continue;
                    bool pct = s >= ItemDatabase.STAT_DAMAGE;
                    diff += (d > 0 ? "<color=#70F070>+" : "<color=#FF7060>") + d.ToString() + (pct ? "%" : "") + " " + itemDatabase.StatName(s) + "</color>\n";
                }
                if (diff.Length > 0) body += "\n<b>If you wear it:</b>\n" + diff;
            }
            if (bodyLabel != null) bodyLabel.text = body;
            if (compareRoot != null) compareRoot.SetActive(compare);
            if (compare)
            {
                if (compareName != null) compareName.text = "<size=22>Wearing now</size>\n" + itemDatabase.GetName(worn);
                if (compareBody != null) compareBody.text = itemDatabase.GetStatSummary(worn);
            }
            PlaceBeside(anchor);
        }

        public void ShowSpell(int cardId, Transform anchor)
        {
            if (cardDatabase == null || cardId == 0) { Hide(); return; }
            if (nameLabel != null)
                nameLabel.text = "<color=#" + Hex(cardDatabase.GetColor(cardId)) + ">" + cardDatabase.GetName(cardId) + "</color>\n<size=22>" +
                                 cardDatabase.SchoolName(cardDatabase.GetSchool(cardId)) + " spell</size>";
            if (bodyLabel != null) bodyLabel.text = cardDatabase.GetCardText(cardId) + "\n\n" +
                                                     (playerState != null && playerState.HasCharacter() ? cardDatabase.GetDetailsFor(playerState.current, cardId) : cardDatabase.GetDetails(cardId));
            if (compareRoot != null) compareRoot.SetActive(false);
            PlaceBeside(anchor);
        }

        private string Hex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(c.r, 1f, 0.35f) * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(c.g, 1f, 0.35f) * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(c.b, 1f, 0.35f) * 255f), 0, 255);
            return Hex2(r) + Hex2(g) + Hex2(b);
        }

        private string Hex2(int v)
        {
            string d = "0123456789ABCDEF";
            return d.Substring(v / 16, 1) + d.Substring(v % 16, 1);
        }
    }
}
