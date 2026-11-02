using UdonSharp;
using UnityEngine;
using VRC.Economy;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The VRChat Creator Economy side of the Starshard Shop. Two kinds of product, both made on the VRChat creator site;
    /// the builder picks up their UdonProduct assets from Assets/WizardsVRC/Store (see README, "Starshard Shop"):
    ///  - Starshard bundles (productShards &gt; 0, an asset named like "Shards_500"): every purchase adds that many
    ///    Starshards (times the quantity bought) to the wizard you're playing. Sell them as Instant listings with
    ///    quantity purchases on, so players can buy them again and again. Shards bought while no wizard is loaded wait
    ///    in your VRChat player data until you pick one. A Permanent bundle bought from the world's page outside the
    ///    world pays out once, the next time you're here.
    ///  - Supporter perks (productShards = 0): owning it unlocks the cosmetics that list it (CosmeticsDatabase,
    ///    UNLOCK_PRODUCT) on every wizard.
    /// With no products set up (or before your world is in the Creator Economy) the shop simply runs on earned Starshards.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class StarshardStore : UdonSharpBehaviour
    {
        private const string PendingKey = "wz_shard_pending";

        public UdonProduct[] products;
        [Tooltip("Starshards each purchase of the product gives (0 = a permanent perk).")]
        public int[] productShards;
        public string[] productLabels;
        public PlayerState playerState;
        public NotificationCenter notifications;

        private bool[] _owned;
        private bool _loaded;

        public bool HasProducts() { return products != null && products.Length > 0; }
        public int ProductCount() { return products != null ? products.Length : 0; }
        public string ProductLabel(int i) { return productLabels != null && i >= 0 && i < productLabels.Length ? productLabels[i] : ""; }
        public bool OwnsProduct(int i) { return _owned != null && i >= 0 && i < _owned.Length && _owned[i]; }
        private int ShardsOf(int i) { return productShards != null && i >= 0 && i < productShards.Length ? productShards[i] : 0; }

        private void Start()
        {
            _owned = new bool[ProductCount()];
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
            for (int k = 0; k < result.Length; k++) Owned(result[k]);
            ClaimShards();
        }

        public override void OnPurchaseConfirmedMultiple(IProduct result, VRCPlayerApi player, bool purchased, int quantity)
        {
            if (player == null || !player.isLocal) return;
            _loaded = true;
            int i = IndexOf(result);
            if (i < 0) return;
            if (!purchased) { Owned(result); ClaimShards(); return; }
            // A new purchase, just now: every bundle bought counts (Instant listings can be bought over and over).
            _owned[i] = true;
            int shards = ShardsOf(i);
            if (shards > 0)
            {
                AddPending(shards * Mathf.Max(1, quantity));
                PlayerData.SetInt(ClaimKey(i), 1);   // a Permanent bundle won't pay out again when its ownership loads
            }
            if (notifications != null) notifications.Post(NotificationCenter.KIND_SHOP, "Thank you!", result.Name, 0);
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

        private string ClaimKey(int i) { return "wz_shard_claim_" + i.ToString(); }

        /// <summary>A product you already own (loaded when you arrive): a Permanent bundle bought elsewhere pays out once.</summary>
        private void Owned(IProduct p)
        {
            int i = IndexOf(p);
            if (i < 0) return;
            _owned[i] = true;
            int shards = ShardsOf(i);
            if (shards <= 0) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            int claimed = 0;
            PlayerData.TryGetInt(me, ClaimKey(i), out claimed);
            if (claimed > 0) return;
            AddPending(shards);
            PlayerData.SetInt(ClaimKey(i), 1);
        }

        /// <summary>Starshards bought but not yet given to a wizard, kept in your VRChat player data until they are.</summary>
        private void AddPending(int shards)
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || shards <= 0) return;
            int pending = 0;
            PlayerData.TryGetInt(me, PendingKey, out pending);
            PlayerData.SetInt(PendingKey, pending + shards);
        }

        /// <summary>Gives the wizard you're playing any Starshards bought and not yet given (when a character becomes active, and after each purchase).</summary>
        public void ClaimShards()
        {
            if (!_loaded || playerState == null || !playerState.HasCharacter()) return;
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me)) return;
            int pending = 0;
            PlayerData.TryGetInt(me, PendingKey, out pending);
            if (pending <= 0) return;
            PlayerData.SetInt(PendingKey, 0);
            playerState.current.starshards += pending;
            playerState.MarkDirty();
            playerState.SaveNow();
            if (notifications != null) notifications.Post(NotificationCenter.KIND_SHOP, "+" + pending.ToString() + " Starshards", "Thank you for supporting the world!", 0);
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
