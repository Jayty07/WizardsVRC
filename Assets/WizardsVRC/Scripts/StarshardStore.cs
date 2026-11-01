using UdonSharp;
using UnityEngine;
using VRC.Economy;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The VRChat Creator Economy side of the Starshard Shop. Two kinds of product, both made on the VRChat creator site
    /// and dropped into 'products' (see README, "Starshard Shop"):
    ///  - Starshard bundles (productShards &gt; 0): each purchase adds that many Starshards to the wizard you're playing,
    ///    once (how many you've claimed per product is kept in your VRChat player data).
    ///  - Supporter perks (productShards = 0): owning it unlocks the cosmetics that list it (CosmeticsDatabase,
    ///    UNLOCK_PRODUCT) on every wizard.
    /// With no products set up (or before your world is in the Creator Economy) the shop simply runs on earned Starshards.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class StarshardStore : UdonSharpBehaviour
    {
        public UdonProduct[] products;
        [Tooltip("Starshards each purchase of the product gives (0 = a permanent perk).")]
        public int[] productShards;
        public string[] productLabels;
        public PlayerState playerState;
        public NotificationCenter notifications;

        private bool[] _owned;
        private int[] _quantity;
        private bool _loaded;

        public bool HasProducts() { return products != null && products.Length > 0; }
        public int ProductCount() { return products != null ? products.Length : 0; }
        public string ProductLabel(int i) { return productLabels != null && i >= 0 && i < productLabels.Length ? productLabels[i] : ""; }
        public bool OwnsProduct(int i) { return _owned != null && i >= 0 && i < _owned.Length && _owned[i]; }

        private void Start()
        {
            int n = ProductCount();
            _owned = new bool[n];
            _quantity = new int[n];
        }

        /// <summary>Opens this world's store page in VRChat (where the products are bought).</summary>
        public void OpenStore()
        {
            if (!HasProducts()) return;
            Store.OpenWorldStorePage();
        }

        public override void OnPurchasesLoaded(IProduct[] result, VRCPlayerApi player)
        {
            if (player == null || !player.isLocal) return;
            _loaded = true;
            for (int k = 0; k < result.Length; k++) Mark(result[k], 1);
            ClaimShards();
        }

        public override void OnPurchaseConfirmedMultiple(IProduct result, VRCPlayerApi player, bool purchased, int quantity)
        {
            if (player == null || !player.isLocal) return;
            Mark(result, quantity);
            if (purchased && notifications != null) notifications.Post(NotificationCenter.KIND_SHOP, "Thank you!", result.Name, 0);
            ClaimShards();
        }

        public override void OnPurchaseExpired(IProduct result, VRCPlayerApi player)
        {
            if (player == null || !player.isLocal) return;
            int i = IndexOf(result);
            if (i >= 0) _owned[i] = false;
        }

        private int IndexOf(IProduct p)
        {
            if (p == null || products == null) return -1;
            for (int i = 0; i < products.Length; i++)
                if (products[i] != null && p.Equals(products[i])) return i;
            return -1;
        }

        private void Mark(IProduct p, int quantity)
        {
            int i = IndexOf(p);
            if (i < 0) return;
            _owned[i] = true;
            _quantity[i] = Mathf.Max(_quantity[i], Mathf.Max(1, quantity));
        }

        /// <summary>Gives the active wizard any bundle Starshards not yet claimed (call when a character becomes active).</summary>
        public void ClaimShards()
        {
            if (!_loaded || playerState == null || !playerState.HasCharacter()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            for (int i = 0; i < ProductCount(); i++)
            {
                if (!_owned[i] || productShards == null || i >= productShards.Length || productShards[i] <= 0) continue;
                string key = "wz_shard_claim_" + i.ToString();
                int claimed = 0;
                PlayerData.TryGetInt(me, key, out claimed);
                int due = _quantity[i] - claimed;
                if (due <= 0) continue;
                int shards = due * productShards[i];
                playerState.current.starshards += shards;
                playerState.MarkDirty();
                playerState.SaveNow();
                PlayerData.SetInt(key, _quantity[i]);
                if (notifications != null) notifications.Post(NotificationCenter.KIND_SHOP, "+" + shards.ToString() + " Starshards", ProductLabel(i), 0);
            }
        }

        private int _slot = -1;

        private void Update()
        {
            if (playerState == null || !playerState.HasCharacter()) { _slot = -1; return; }
            if (_slot == playerState.activeSlot) return;
            _slot = playerState.activeSlot;
            ClaimShards();
        }
    }
}
