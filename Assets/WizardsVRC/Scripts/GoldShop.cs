using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The hub's shops: click a shop's counter and its wares open in front of you (gear, potions, decks for gold).
    /// One panel serves every shop; each shop's list is a slice of shopItems. Prices come from the item (level and
    /// bonuses) unless set in the list. Hovering a row shows the item next to what you're wearing in that slot.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class GoldShop : UdonSharpBehaviour
    {
        public const int ROWS = 8;
        /// <summary>Counters that can each open their own shop (OpenShop0 .. OpenShop15).</summary>
        public const int MAX_SHOPS = 16;

        public PlayerState playerState;
        public ItemDatabase itemDatabase;
        public UiAudio uiAudio;
        public NotificationCenter notifications;
        public ItemTooltip tooltip;
        public GameObject panelRoot;
        public float openDistance = 0.9f;

        [Header("Shops (flattened: shop s sells shopItems[shopStart[s] .. + shopCount[s]])")]
        public string[] shopNames;
        public string[] shopGreetings;
        public int[] shopStart;
        public int[] shopCount;
        public int[] shopItems;
        [Tooltip("Price per entry of shopItems (0 = worked out from the item).")]
        public int[] shopPrices;

        [Header("Panel")]
        public Text titleLabel;
        public Text greetingLabel;
        public Text goldLabel;
        public Text statusLabel;
        public Text pageLabel;
        public GameObject[] rowRoots;
        public Image[] rowIcons;
        public Text[] rowNames;
        public Text[] rowStats;
        public Text[] rowPrices;

        private int _shop = -1;
        private int _page;
        private bool _open;

        public void OpenShop0() { Open(0); }
        public void OpenShop1() { Open(1); }
        public void OpenShop2() { Open(2); }
        public void OpenShop3() { Open(3); }
        public void OpenShop4() { Open(4); }
        public void OpenShop5() { Open(5); }
        public void OpenShop6() { Open(6); }
        public void OpenShop7() { Open(7); }
        public void OpenShop8() { Open(8); }
        public void OpenShop9() { Open(9); }
        public void OpenShop10() { Open(10); }
        public void OpenShop11() { Open(11); }
        public void OpenShop12() { Open(12); }
        public void OpenShop13() { Open(13); }
        public void OpenShop14() { Open(14); }
        public void OpenShop15() { Open(15); }

        public void Buy0() { Buy(0); }
        public void Buy1() { Buy(1); }
        public void Buy2() { Buy(2); }
        public void Buy3() { Buy(3); }
        public void Buy4() { Buy(4); }
        public void Buy5() { Buy(5); }
        public void Buy6() { Buy(6); }
        public void Buy7() { Buy(7); }

        public void Hover0() { Hover(0); }
        public void Hover1() { Hover(1); }
        public void Hover2() { Hover(2); }
        public void Hover3() { Hover(3); }
        public void Hover4() { Hover(4); }
        public void Hover5() { Hover(5); }
        public void Hover6() { Hover(6); }
        public void Hover7() { Hover(7); }
        public void Unhover() { if (tooltip != null) tooltip.Hide(); }

        private void Start()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Open(int shop)
        {
            if (playerState == null || !playerState.HasCharacter() || shopStart == null || shop >= shopStart.Length) return;
            _shop = shop;
            _page = 0;
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
            _open = true;
            if (panelRoot != null) panelRoot.SetActive(true);
            if (uiAudio != null) uiAudio.PlayBookOpen();
            SetStatus("");
            Draw();
        }

        public void Close()
        {
            _open = false;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (tooltip != null) tooltip.Hide();
        }

        public void PrevPage() { if (_page > 0) { _page--; Draw(); } }
        public void NextPage() { if ((_page + 1) * ROWS < Count()) { _page++; Draw(); } }

        private int Count() { return _shop >= 0 ? shopCount[_shop] : 0; }

        private int EntryAt(int row)
        {
            int k = _page * ROWS + row;
            if (_shop < 0 || k >= Count()) return -1;
            return shopStart[_shop] + k;
        }

        public int PriceOf(int entry)
        {
            if (shopPrices != null && entry < shopPrices.Length && shopPrices[entry] > 0) return shopPrices[entry];
            int id = shopItems[entry];
            if (itemDatabase.IsPotion(id)) return 20 + itemDatabase.GetPotionHealth(id) / 10 + itemDatabase.GetPotionMana(id) * 2;
            int sum = 0;
            for (int s = 0; s < ItemDatabase.STAT_COUNT; s++)
            {
                int v = itemDatabase.GetStat(id, s);
                sum += s <= ItemDatabase.STAT_MANA ? v / 4 : v * 3;
            }
            if (itemDatabase.IsDeck(id)) sum += itemDatabase.GetDeckCapacity(id) * 4;
            return Mathf.Max(15, 25 + itemDatabase.GetLevelReq(id) * 18 + sum * 4);
        }

        private void Draw()
        {
            if (!_open || _shop < 0) return;
            CharacterData c = playerState.current;
            if (titleLabel != null) titleLabel.text = shopNames != null && _shop < shopNames.Length ? shopNames[_shop] : "Shop";
            if (greetingLabel != null) greetingLabel.text = shopGreetings != null && _shop < shopGreetings.Length ? shopGreetings[_shop] : "";
            if (goldLabel != null) goldLabel.text = "● " + c.gold.ToString() + " gold";
            for (int r = 0; r < ROWS; r++)
            {
                int e = EntryAt(r);
                bool has = e >= 0;
                if (rowRoots != null && r < rowRoots.Length && rowRoots[r] != null && rowRoots[r].activeSelf != has) rowRoots[r].SetActive(has);
                if (!has) continue;
                int id = shopItems[e];
                string reason = itemDatabase.IsEquippable(id) ? itemDatabase.CheckCanEquip(c, id) : "";
                if (rowIcons != null && rowIcons[r] != null) rowIcons[r].color = itemDatabase.GetIconColor(id);
                if (rowNames != null && rowNames[r] != null) rowNames[r].text = itemDatabase.GetName(id) + "  <size=20>(" + itemDatabase.GetCategoryName(itemDatabase.GetCategory(id)) + ")</size>";
                if (rowStats != null && rowStats[r] != null)
                    rowStats[r].text = reason.Length > 0 ? "<color=#B03020>" + reason + "</color>" : itemDatabase.GetStatSummary(id).Replace("\n", "   ");
                int price = PriceOf(e);
                if (rowPrices != null && rowPrices[r] != null)
                    rowPrices[r].text = (c.gold >= price ? "" : "<color=#B03020>") + price.ToString() + (c.gold >= price ? "" : "</color>");
            }
            int pages = Mathf.Max(1, (Count() + ROWS - 1) / ROWS);
            if (pageLabel != null) pageLabel.text = (_page + 1).ToString() + " / " + pages.ToString();
        }

        private void Buy(int row)
        {
            int e = EntryAt(row);
            if (e < 0 || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            int id = shopItems[e];
            int price = PriceOf(e);
            if (c.gold < price) { SetStatus("Not enough gold."); if (uiAudio != null) uiAudio.PlayCancel(); return; }
            if (!c.AddItem(id, 1)) { SetStatus("Your backpack is full."); if (uiAudio != null) uiAudio.PlayCancel(); return; }
            c.gold -= price;
            playerState.MarkDirty();
            playerState.SaveNow();
            if (uiAudio != null) uiAudio.PlayOk();
            SetStatus("Bought " + itemDatabase.GetName(id) + ".");
            if (notifications != null) notifications.Post(NotificationCenter.KIND_GEAR, "Bought", itemDatabase.GetName(id), id);
            Draw();
        }

        private void Hover(int row)
        {
            int e = EntryAt(row);
            if (e < 0 || tooltip == null) return;
            tooltip.ShowItem(shopItems[e], rowRoots[row].transform);
            if (uiAudio != null) uiAudio.PlayHover();
        }

        private void SetStatus(string s) { if (statusLabel != null) statusLabel.text = s; }

        private void Update()
        {
            if (_open && (playerState == null || !playerState.HasCharacter())) Close();
        }
    }
}
