using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// The book's new bookmarks (Stats, Notices, Wardrobe, Starshard Shop), notifications, item / spell tooltips with
    /// gear comparison, titles and auras (shown to everyone through a VRChat player object), elixirs, the Starshard
    /// Shop with its VRChat store bridge, and the hub's gold shops.
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        // Made while the book is built, wired once the rest of the systems exist (WireExtras).
        private static Transform BookTabBadge;
        private static WardrobePages BookWardrobe;
        private static ShopPages BookShop;
        private static NotificationCenter BookNotices;
        private static readonly List<UdonSharpBehaviour> ExtraBehaviours = new List<UdonSharpBehaviour>();
        /// <summary>Shop counters made while the hub is built (their relay is pointed at the gold shop later).</summary>
        private static readonly List<EventRelay> ShopCounters = new List<EventRelay>();
        private static readonly List<string> ShopCounterNames = new List<string>();
        /// <summary>Per counter: its kind (ShopNames index) and the item levels it stocks (each world's shops sell that world's gear).</summary>
        private static readonly List<Vector3Int> ShopCounterStock = new List<Vector3Int>();
        /// <summary>Item levels the shops being built now stock (set per zone: Starfall Isle 1-12, Dustwind 13-40).</summary>
        private static Vector2Int _shopBand = new Vector2Int(1, 12);
        private static GameObject GoldShopPanel, TooltipRoot;

        /// <summary>Clears what the last build left in the statics above (called at the start of every build).</summary>
        private static void ResetExtras()
        {
            BookTabBadge = null; BookWardrobe = null; BookShop = null; BookNotices = null;
            GoldShopPanel = null; TooltipRoot = null;
            ExtraBehaviours.Clear();
            ShopCounters.Clear();
            ShopCounterNames.Clear();
            ShopCounterStock.Clear();
            _shopBand = new Vector2Int(1, 12);
            ResetTowers();
            ResetRigs();
            ResetNpcModels();
            ResetSpellFx();
            _monsterRigs.Clear();
        }

        // ================================================================== tables

        private static readonly string[] TitleNames =
        {
            "Novice", "Apprentice", "Adept", "Magus", "Archmage",
            "Monster Hunter", "Monster Bane", "Boss Slayer", "Tower Climber", "Krakenbane",
            "the Radiant", "Starborn", "Shardlord", "Patron of Starfall",
            // Dustwind
            "Deputy", "Trailblazer", "Sheriff of Dustwind", "Bounty Hunter", "Lawman", "Legend of the West", "Ghost Rider",
            "High Noon Duelist", "Keeper of the Heartstone",
        };
        private static readonly int[,] TitleUnlocks =
        {
            { CosmeticsDatabase.UNLOCK_LEVEL, 1 }, { CosmeticsDatabase.UNLOCK_LEVEL, 10 }, { CosmeticsDatabase.UNLOCK_LEVEL, 20 },
            { CosmeticsDatabase.UNLOCK_LEVEL, 30 }, { CosmeticsDatabase.UNLOCK_LEVEL, 50 },
            { CosmeticsDatabase.UNLOCK_MONSTERS, 100 }, { CosmeticsDatabase.UNLOCK_MONSTERS, 500 }, { CosmeticsDatabase.UNLOCK_BOSSES, 10 },
            { CosmeticsDatabase.UNLOCK_FLAG, 0 }, { CosmeticsDatabase.UNLOCK_FLAG, 30 },
            { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 },
            { CosmeticsDatabase.UNLOCK_PRODUCT, 0 },
            { CosmeticsDatabase.UNLOCK_QUEST, 43 }, { CosmeticsDatabase.UNLOCK_LEVEL, 20 }, { CosmeticsDatabase.UNLOCK_LEVEL, 25 },
            { CosmeticsDatabase.UNLOCK_BOUNTIES, 3 }, { CosmeticsDatabase.UNLOCK_BOUNTIES, 10 }, { CosmeticsDatabase.UNLOCK_BOUNTIES, 25 },
            { CosmeticsDatabase.UNLOCK_FLAG, 29 }, { CosmeticsDatabase.UNLOCK_HIGHNOON, 5 }, { CosmeticsDatabase.UNLOCK_QUEST, 77 },
        };
        private static readonly int[] TitlePrices = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 40, 60, 120, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly string[] AuraNames =
        {
            "Ember Motes", "Frost Mist", "Storm Sparks", "Life Petals", "Death Wisps", "Myth Glitter", "Balance Sands",
            "Hunter's Glow", "Starfall", "Void Flame", "Golden Halo", "Patron's Radiance", "Sunstone",
            // The school Surge auras (WizardsVRCSceneBuilder.Auras.cs): they power up with your pips in a duel.
            "Inferno Surge", "Rimefrost Surge", "Tempest Surge", "Verdant Surge", "Gloom Surge", "Legend Surge", "Equinox Surge",
        };
        private static readonly Color[] AuraColors =
        {
            new Color(1f, 0.5f, 0.15f), new Color(0.65f, 0.9f, 1f), new Color(0.75f, 0.55f, 1f), new Color(0.5f, 1f, 0.5f),
            new Color(0.55f, 1f, 0.7f), new Color(1f, 0.85f, 0.3f), new Color(0.95f, 0.8f, 0.55f),
            new Color(1f, 0.35f, 0.3f), new Color(0.6f, 0.75f, 1f), new Color(0.65f, 0.2f, 1f), new Color(1f, 0.92f, 0.5f), new Color(1f, 0.6f, 0.9f),
            new Color(1f, 0.62f, 0.25f),
            new Color(1f, 0.42f, 0.08f), new Color(0.45f, 0.82f, 1f), new Color(0.62f, 0.35f, 1f), new Color(0.35f, 1f, 0.42f),
            new Color(0.5f, 0.2f, 0.75f), new Color(1f, 0.78f, 0.2f), new Color(1f, 0.62f, 0.25f),
        };
        // Index 0..6: your own school's aura from level 10; 7: 10 bosses; the rest are bought or a VRChat perk.
        private static readonly int[,] AuraUnlocks =
        {
            { CosmeticsDatabase.UNLOCK_SCHOOL, 10 }, { CosmeticsDatabase.UNLOCK_SCHOOL, 10 }, { CosmeticsDatabase.UNLOCK_SCHOOL, 10 },
            { CosmeticsDatabase.UNLOCK_SCHOOL, 10 }, { CosmeticsDatabase.UNLOCK_SCHOOL, 10 }, { CosmeticsDatabase.UNLOCK_SCHOOL, 10 },
            { CosmeticsDatabase.UNLOCK_SCHOOL, 10 }, { CosmeticsDatabase.UNLOCK_BOSSES, 10 },
            { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 },
            { CosmeticsDatabase.UNLOCK_PRODUCT, 0 }, { CosmeticsDatabase.UNLOCK_LEVEL, 25 },
            { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 },
            { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 }, { CosmeticsDatabase.UNLOCK_SHOP, 0 },
        };
        // School auras for other schools can also be bought, so a Fire wizard can wear Frost Mist.
        private static readonly int[] AuraPrices = { 50, 50, 50, 50, 50, 50, 50, 0, 80, 100, 150, 0, 0, 140, 140, 140, 140, 140, 140, 140 };

        private static readonly string[] SkinNames = { "Gilded", "Shadowflame", "Prismatic", "Moonlit" };
        private static readonly Color[] SkinColors = { new Color(1f, 0.82f, 0.3f), new Color(0.6f, 0.2f, 0.9f), new Color(0.4f, 1f, 0.9f), new Color(0.75f, 0.85f, 1f) };
        private static readonly int[] SkinPrices = { 60, 80, 100, 60 };

        private static readonly string[] ElixirNames =
            { "Elixir of Fury", "Elixir of Warding", "Elixir of Focus", "Elixir of Fortune", "Elixir of Swiftness", "Elixir of Wisdom" };
        private static readonly int[] ElixirStats =
        {
            ItemDatabase.STAT_DAMAGE, ItemDatabase.STAT_RESIST, ItemDatabase.STAT_ACCURACY, ItemDatabase.STAT_CRITICAL,
            CosmeticsDatabase.ELIXIR_SPEED, CosmeticsDatabase.ELIXIR_XP,
        };
        private static readonly int[] ElixirAmounts = { 10, 10, 10, 5, 15, 25 };
        private static readonly int[] ElixirPrices = { 8, 8, 6, 6, 5, 10 };
        private static readonly string[] ElixirBlurbs =
        {
            "+10% damage for 30 min of play", "+10% resist for 30 min of play", "+10% accuracy for 30 min of play",
            "+5% critical for 30 min of play", "Move 15% faster for 30 min of play", "+25% XP from battles for 30 min of play",
        };

        private static string UnlockHint(int kind, int value)
        {
            switch (kind)
            {
                case CosmeticsDatabase.UNLOCK_LEVEL: return "Reach level " + value;
                case CosmeticsDatabase.UNLOCK_MONSTERS: return "Defeat " + value + " monsters";
                case CosmeticsDatabase.UNLOCK_BOSSES: return "Defeat " + value + " bosses";
                case CosmeticsDatabase.UNLOCK_BATTLES: return "Win " + value + " battles";
                case CosmeticsDatabase.UNLOCK_FLAG: return value == 30 ? "Defeat the Kraken beneath Tidewatch" : value == 29 ? "Defeat the Midnight Express" : "Clear a boss tower";
                case CosmeticsDatabase.UNLOCK_QUEST: return value == 43 ? "Become a deputy in Archrock" : value == 77 ? "Seal the Heartstone (Dustwind's finale)" : "Finish quest " + value;
                case CosmeticsDatabase.UNLOCK_BOUNTIES: return "Turn in " + value + " bounties at Archrock's bounty board";
                case CosmeticsDatabase.UNLOCK_HIGHNOON: return "Win " + value + " High Noon duels in Archrock";
                case CosmeticsDatabase.UNLOCK_SHOP: return "Starshard Shop";
                case CosmeticsDatabase.UNLOCK_PRODUCT: return "Supporter perk (world store)";
                case CosmeticsDatabase.UNLOCK_SCHOOL: return "Your school's aura at level " + value + " (or the Starshard Shop)";
                default: return "";
            }
        }

        // ================================================================== book pages

        private static GameObject BookView(RectTransform root, string name, out RectTransform left, out RectTransform right)
        {
            RectTransform view = Rect(root, name, Vector2.zero, new Vector2(1800, 1100));
            left = Panel(view, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            right = Panel(view, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);
            view.gameObject.SetActive(false);
            return view.gameObject;
        }

        private static GameObject BuildStatsPages(RectTransform root, InventoryBook book)
        {
            RectTransform left, right;
            var view = BookView(root, "StatsView", out left, out right);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            InkLabel(left, "Title", new Vector2(0, 450), new Vector2(800, 60), "Your Wizard", 44, TextAnchor.MiddleCenter);
            book.statsOverview = InkLabel(left, "Overview", new Vector2(0, -20), new Vector2(720, 820), "", 30, TextAnchor.UpperLeft);
            book.statsOverview.supportRichText = true;
            Button(left, "Close", new Vector2(0, -460), new Vector2(260, 65), "Close", new Color(0.45f, 0.25f, 0.15f), target, nameof(InventoryBook.CloseBook));
            InkLabel(right, "Title", new Vector2(0, 450), new Vector2(800, 60), "Stats", 44, TextAnchor.MiddleCenter);
            book.statsCombat = InkLabel(right, "Combat", new Vector2(0, 120), new Vector2(720, 560), "", 28, TextAnchor.UpperLeft);
            book.statsRecords = InkLabel(right, "Records", new Vector2(0, -300), new Vector2(720, 300), "", 28, TextAnchor.UpperLeft);
            return view;
        }

        private static GameObject BuildNoticesPages(RectTransform root, InventoryBook book)
        {
            RectTransform left, right;
            var view = BookView(root, "NoticesView", out left, out right);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            var notices = UdonSharpUndo.AddComponent<NotificationCenter>(Child(book.transform.parent, "Notifications", Vector3.zero).gameObject);
            BookNotices = notices;
            ExtraBehaviours.Add(notices);
            InkLabel(left, "Title", new Vector2(0, 450), new Vector2(800, 60), "Notices", 44, TextAnchor.MiddleCenter);
            InkLabel(right, "Title", new Vector2(0, 450), new Vector2(800, 60), "", 44, TextAnchor.MiddleCenter);
            const int perPage = 4;
            var rows = new GameObject[perPage * 2];
            var titles = new Text[rows.Length];
            var bodies = new Text[rows.Length];
            var icons = new Image[rows.Length];
            var glyphs = new Text[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform page = i < perPage ? left : right;
                int k = i % perPage;
                RectTransform row = Panel(page, "Notice" + i, new Vector2(0, 300 - k * 210), new Vector2(780, 195), new Color(0.82f, 0.74f, 0.58f));
                icons[i] = UiImage(row, "Icon", new Vector2(-330, 40), new Vector2(84, 84), "PortraitBack", Color.white, 1.3f);
                glyphs[i] = Label(icons[i].transform, "Glyph", Vector2.zero, new Vector2(84, 84), "!", 44, TextAnchor.MiddleCenter);
                titles[i] = InkLabel(row, "Title", new Vector2(50, 60), new Vector2(660, 50), "", 28, TextAnchor.MiddleLeft);
                titles[i].fontStyle = FontStyle.Bold;
                bodies[i] = InkLabel(row, "Body", new Vector2(50, -30), new Vector2(660, 130), "", 22, TextAnchor.UpperLeft);
                bodies[i].resizeTextForBestFit = true;
                bodies[i].resizeTextMinSize = 14;
                bodies[i].resizeTextMaxSize = 22;
                bodies[i].verticalOverflow = VerticalWrapMode.Truncate;
                rows[i] = row.gameObject;
                row.gameObject.SetActive(false);
                ApplyCombatFont(icons[i].transform);
            }
            notices.rows = rows;
            notices.rowTitles = titles;
            notices.rowBodies = bodies;
            notices.rowIcons = icons;
            notices.rowIconGlyphs = glyphs;
            notices.emptyLabel = InkLabel(left, "Empty", new Vector2(0, 200), new Vector2(700, 120), "Nothing new. Notices about new gear, spells and quests appear here.", 26, TextAnchor.MiddleCenter);
            notices.kindColors = new[] { new Color(0.95f, 0.75f, 0.3f), new Color(0.55f, 0.75f, 1f), new Color(1f, 0.9f, 0.4f), new Color(0.75f, 0.95f, 0.75f), new Color(1f, 0.6f, 0.9f) };
            notices.kindHex = new[] { "#F2BF4D", "#8CBFFF", "#FFE666", "#BFF2BF", "#FF99E6" };
            notices.kindGlyphs = new[] { "", "", "!", "", "" };
            Button(left, "Close", new Vector2(0, -460), new Vector2(260, 65), "Close", new Color(0.45f, 0.25f, 0.15f), bookTarget, nameof(InventoryBook.CloseBook));
            // A badge with the unread count on the Notices bookmark.
            if (BookTabBadge != null)
            {
                var badge = UiImage(BookTabBadge, "Badge", new Vector2(30, 48), new Vector2(42, 42), "NotifyBadge", Color.white, 1.3f);
                var count = Label(badge.transform, "Count", new Vector2(0, 0), new Vector2(42, 42), "", 22, TextAnchor.MiddleCenter);
                badge.GetComponent<Image>().raycastTarget = false;
                // The badge art has its own '!': cover it with the number in a red disc.
                count.gameObject.AddComponent<Outline>().effectColor = new Color(0.4f, 0f, 0f);
                badge.gameObject.SetActive(false);
                notices.badges = new[] { badge.gameObject };
                notices.badgeCounts = new[] { count };
            }
            return view;
        }

        private static GameObject BuildWardrobePages(RectTransform root, InventoryBook book)
        {
            RectTransform left, right;
            var view = BookView(root, "WardrobeView", out left, out right);
            var pages = UdonSharpUndo.AddComponent<WardrobePages>(Child(book.transform.parent, "WardrobePages", Vector3.zero).gameObject);
            BookWardrobe = pages;
            ExtraBehaviours.Add(pages);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(pages);
            UdonBehaviour bookTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            Color brown = new Color(0.55f, 0.42f, 0.25f);

            InkLabel(left, "Title", new Vector2(0, 450), new Vector2(800, 60), "Titles", 44, TextAnchor.MiddleCenter);
            Button(left, "TitleNone", new Vector2(300, 450), new Vector2(150, 50), "None", new Color(0.4f, 0.3f, 0.25f), target, nameof(WardrobePages.TitleNone));
            int tr = WardrobePages.TITLE_ROWS;
            pages.titleRows = new GameObject[tr];
            pages.titleTexts = new Text[tr];
            pages.titleImages = new Image[tr];
            for (int i = 0; i < tr; i++)
            {
                int col = i % 2, row = i / 2;
                GameObject b = Button(left, "Title" + i, new Vector2(-195 + col * 390, 350 - row * 108), new Vector2(375, 98), "", brown, target, "Title" + i);
                Text t = b.GetComponentInChildren<Text>();
                t.fontSize = 24;
                pages.titleRows[i] = b;
                pages.titleTexts[i] = t;
                pages.titleImages[i] = b.GetComponent<Image>();
            }
            pages.currentLabel = InkLabel(left, "Current", new Vector2(0, -400), new Vector2(800, 60), "", 22, TextAnchor.MiddleCenter);
            Button(left, "Close", new Vector2(0, -465), new Vector2(240, 60), "Close", new Color(0.45f, 0.25f, 0.15f), bookTarget, nameof(InventoryBook.CloseBook));

            InkLabel(right, "Title", new Vector2(0, 450), new Vector2(800, 60), "Auras", 44, TextAnchor.MiddleCenter);
            Button(right, "AuraNone", new Vector2(300, 450), new Vector2(150, 50), "None", new Color(0.4f, 0.3f, 0.25f), target, nameof(WardrobePages.AuraNone));
            int ar = WardrobePages.AURA_ROWS;
            pages.auraRows = new GameObject[ar];
            pages.auraTexts = new Text[ar];
            pages.auraImages = new Image[ar];
            pages.auraSwatches = new Image[ar];
            for (int i = 0; i < ar; i++)
            {
                // Three columns, so every aura (Surges included) has a button.
                int col = i % 3, row = i / 3;
                GameObject b = Button(right, "Aura" + i, new Vector2(-262 + col * 262, 360 - row * 84), new Vector2(255, 78), "", brown, target, "Aura" + i);
                Text t = b.GetComponentInChildren<Text>();
                t.fontSize = 18;
                ((RectTransform)t.transform).anchoredPosition = new Vector2(16, 0);
                pages.auraSwatches[i] = Panel(b.transform, "Swatch", new Vector2(-108, 0), new Vector2(24, 24), Color.white).GetComponent<Image>();
                pages.auraRows[i] = b;
                pages.auraTexts[i] = t;
                pages.auraImages[i] = b.GetComponent<Image>();
            }
            InkLabel(right, "SkinTitle", new Vector2(-120, -275), new Vector2(500, 50), "Spell skins", 32, TextAnchor.MiddleLeft);
            Button(right, "SkinNone", new Vector2(300, -275), new Vector2(150, 50), "School", new Color(0.4f, 0.3f, 0.25f), target, nameof(WardrobePages.SkinNone));
            int sr = WardrobePages.SKIN_ROWS;
            pages.skinRows = new GameObject[sr];
            pages.skinTexts = new Text[sr];
            pages.skinImages = new Image[sr];
            for (int i = 0; i < sr; i++)
            {
                GameObject b = Button(right, "Skin" + i, new Vector2(-292 + i * 195, -370), new Vector2(185, 100), "", brown, target, "Skin" + i);
                Text t = b.GetComponentInChildren<Text>();
                t.fontSize = 20;
                pages.skinRows[i] = b;
                pages.skinTexts[i] = t;
                pages.skinImages[i] = b.GetComponent<Image>();
            }
            InkLabel(right, "Hint", new Vector2(0, -460), new Vector2(800, 50), "Everyone sees your title and aura, out in the world and in duels.", 20, TextAnchor.MiddleCenter);
            return view;
        }

        private const string StoreProductDir = "Assets/WizardsVRC/Store";

        /// <summary>
        /// The VRChat store products, from the UdonProduct assets in Assets/WizardsVRC/Store (so a rebuild never loses them):
        /// an asset named with a number of Starshards ("Shards_500", "500 Starshards") is a bundle of that many; any other
        /// is a supporter perk. Perks come first, in name order (the Patron cosmetics unlock with perk 0), then bundles,
        /// smallest first.
        /// </summary>
        private static void FillStoreProducts(StarshardStore store)
        {
            var perks = new List<KeyValuePair<string, VRC.Economy.UdonProduct>>();
            var bundles = new List<KeyValuePair<int, VRC.Economy.UdonProduct>>();
            if (AssetDatabase.IsValidFolder(StoreProductDir))
                foreach (string guid in AssetDatabase.FindAssets("t:UdonProduct", new[] { StoreProductDir }))
                {
                    var p = AssetDatabase.LoadAssetAtPath<VRC.Economy.UdonProduct>(AssetDatabase.GUIDToAssetPath(guid));
                    if (p == null) continue;
                    var m = System.Text.RegularExpressions.Regex.Match(p.name, @"\d+");
                    int shards = m.Success ? int.Parse(m.Value) : 0;
                    if (shards > 0) bundles.Add(new KeyValuePair<int, VRC.Economy.UdonProduct>(shards, p));
                    else perks.Add(new KeyValuePair<string, VRC.Economy.UdonProduct>(p.name, p));
                }
            perks.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            bundles.Sort((x, y) => x.Key.CompareTo(y.Key));
            var products = new List<VRC.Economy.UdonProduct>();
            var shardsList = new List<int>();
            var labels = new List<string>();
            foreach (var kv in perks) { products.Add(kv.Value); shardsList.Add(0); labels.Add(kv.Key); }
            foreach (var kv in bundles) { products.Add(kv.Value); shardsList.Add(kv.Key); labels.Add(kv.Key + " Starshards"); }
            store.products = products.ToArray();
            store.productShards = shardsList.ToArray();
            store.productLabels = labels.ToArray();
            Debug.Log("[WizardsVRC] Starshard Shop: " + perks.Count + " supporter perk(s) and " + bundles.Count + " Starshard bundle(s) from " + StoreProductDir +
                      (products.Count == 0 ? " (none: the shop runs on earned Starshards)" : "") + ".");
        }

        // ================================================================== tooltip, gold shop panel

        private static ItemTooltip BuildTooltip(Transform systems)
        {
            var go = new GameObject("ItemTooltip", typeof(RectTransform));
            go.transform.SetParent(systems, false);
            var canvas = MakeWorldCanvas(go, new Vector2(520, 620), Vector3.zero, 0.0006f);
            canvas.sortingOrder = 40;
            Object.DestroyImmediate(go.GetComponent<VRC.SDK3.Components.VRCUiShape>());
            Object.DestroyImmediate(go.GetComponent<GraphicRaycaster>());
            var rt = (RectTransform)go.transform;
            var tip = UdonSharpUndo.AddComponent<ItemTooltip>(Child(systems, "ItemTooltipLogic", Vector3.zero).gameObject);
            UiImage(rt, "Back", Vector2.zero, new Vector2(520, 620), "Tooltip", new Color(1f, 1f, 1f, 0.97f), 2f).raycastTarget = false;
            tip.nameLabel = Label(rt, "Name", new Vector2(0, 245), new Vector2(470, 100), "", 30, TextAnchor.MiddleCenter);
            tip.bodyLabel = Label(rt, "Body", new Vector2(0, -60), new Vector2(470, 480), "", 21, TextAnchor.UpperLeft);
            tip.bodyLabel.resizeTextForBestFit = true;
            tip.bodyLabel.resizeTextMinSize = 14;
            tip.bodyLabel.resizeTextMaxSize = 21;
            tip.bodyLabel.verticalOverflow = VerticalWrapMode.Truncate;
            var cmp = UiImage(rt, "Compare", new Vector2(460, 80), new Vector2(380, 420), "PanelOrnate", new Color(1f, 1f, 1f, 0.95f), 2f);
            cmp.raycastTarget = false;
            tip.compareRoot = cmp.gameObject;
            tip.compareName = Label(cmp.transform, "Name", new Vector2(0, 150), new Vector2(340, 90), "", 24, TextAnchor.MiddleCenter);
            tip.compareName.color = new Color(1f, 0.85f, 0.45f);
            tip.compareBody = Label(cmp.transform, "Body", new Vector2(0, -50), new Vector2(330, 290), "", 21, TextAnchor.UpperLeft);
            foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            tip.root = go;
            TooltipRoot = go;
            ApplyCombatFont(rt);
            go.SetActive(false);
            ExtraBehaviours.Add(tip);
            return tip;
        }

        private static readonly string[] ShopNames = { "Robes & Hats", "Wands & Blades", "Charms & Rings", "Potions & Decks" };
        private static readonly string[] ShopGreetings =
        {
            "\"Fine robes for every school! Try something on.\"", "\"A good wand finds its wizard. Have a look!\"",
            "\"Amulets, rings, a little luck on a chain.\"", "\"Potions brewed this morning, decks bound this week.\"",
        };
        private static readonly int[][] ShopCategories =
        {
            new[] { 0, 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 }, new[] { 8, 7 },
        };

        private static GoldShop BuildGoldShop(Transform systems, ItemDatabase itemDb)
        {
            var shop = UdonSharpUndo.AddComponent<GoldShop>(Child(systems, "GoldShop", Vector3.zero).gameObject);
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(shop);
            var go = new GameObject("GoldShopPanel", typeof(RectTransform));
            go.transform.SetParent(systems, false);
            MakeWorldCanvas(go, new Vector2(1200, 1000), Vector3.zero, 0.0006f);
            var rt = (RectTransform)go.transform;
            shop.panelRoot = go;
            GoldShopPanel = go;
            Panel(rt, "Background", Vector2.zero, new Vector2(1200, 1000), Parchment);
            shop.titleLabel = InkLabel(rt, "Title", new Vector2(0, 440), new Vector2(1100, 70), "", 48, TextAnchor.MiddleCenter);
            shop.greetingLabel = InkLabel(rt, "Greeting", new Vector2(0, 380), new Vector2(1100, 50), "", 24, TextAnchor.MiddleCenter);
            shop.goldLabel = InkLabel(rt, "Gold", new Vector2(380, 440), new Vector2(360, 60), "", 30, TextAnchor.MiddleRight);
            ApplyCombatFont(shop.goldLabel.transform);
            shop.goldLabel.color = new Color(0.55f, 0.38f, 0.05f);
            int rows = GoldShop.ROWS;
            shop.rowRoots = new GameObject[rows];
            shop.rowIcons = new Image[rows];
            shop.rowNames = new Text[rows];
            shop.rowStats = new Text[rows];
            shop.rowPrices = new Text[rows];
            for (int r = 0; r < rows; r++)
            {
                RectTransform row = Panel(rt, "Row" + r, new Vector2(0, 290 - r * 92), new Vector2(1120, 86), new Color(0.82f, 0.74f, 0.58f));
                shop.rowIcons[r] = Panel(row, "Icon", new Vector2(-515, 0), new Vector2(60, 60), Color.gray).GetComponent<Image>();
                shop.rowNames[r] = InkLabel(row, "Name", new Vector2(-150, 18), new Vector2(660, 44), "", 26, TextAnchor.MiddleLeft);
                shop.rowNames[r].fontStyle = FontStyle.Bold;
                shop.rowStats[r] = InkLabel(row, "Stats", new Vector2(-150, -20), new Vector2(660, 40), "", 19, TextAnchor.MiddleLeft);
                shop.rowPrices[r] = InkLabel(row, "Price", new Vector2(290, 0), new Vector2(150, 60), "", 28, TextAnchor.MiddleRight);
                Label(row, "Coin", new Vector2(385, 0), new Vector2(34, 40), "●", 26, TextAnchor.MiddleCenter).color = new Color(0.8f, 0.6f, 0.1f);
                Button(row, "Buy", new Vector2(480, 0), new Vector2(110, 66), "Buy", new Color(0.25f, 0.5f, 0.3f), target, "Buy" + r);
                // Pointing at the row shows the item next to what you wear in that slot.
                var img = row.GetComponent<Image>();
                img.raycastTarget = true;
                AddHoverEvents(row.gameObject, target, "Hover" + r, nameof(GoldShop.Unhover));
                shop.rowRoots[r] = row.gameObject;
            }
            Button(rt, "PrevPage", new Vector2(-300, -440), new Vector2(140, 60), "<", new Color(0.45f, 0.3f, 0.18f), target, nameof(GoldShop.PrevPage));
            shop.pageLabel = InkLabel(rt, "Page", new Vector2(-150, -440), new Vector2(160, 50), "", 26, TextAnchor.MiddleCenter);
            Button(rt, "NextPage", new Vector2(0, -440), new Vector2(140, 60), ">", new Color(0.45f, 0.3f, 0.18f), target, nameof(GoldShop.NextPage));
            shop.statusLabel = InkLabel(rt, "Status", new Vector2(0, -375), new Vector2(1000, 50), "", 26, TextAnchor.MiddleCenter);
            Button(rt, "Close", new Vector2(400, -440), new Vector2(220, 64), "Close", new Color(0.45f, 0.25f, 0.15f), target, nameof(GoldShop.Close));
            foreach (Transform row in rt) if (row.name.StartsWith("Row")) ApplyCombatFont(row.Find("Coin"));
            go.SetActive(false);

            // Each shop sells its categories' gear (everything below level 40 that isn't a starter item, quest reward or boss drop).
            var start = new List<int>();
            var counts = new List<int>();
            var items = new List<int>();
            int nShops = Mathf.Max(ShopNames.Length, ShopCounters.Count);
            var names = new string[nShops];
            var greet = new string[nShops];
            for (int s = 0; s < nShops; s++)
            {
                int kind = s < ShopCounterStock.Count ? ShopCounterStock[s].x % ShopNames.Length : s % ShopNames.Length;
                int minLv = s < ShopCounterStock.Count ? ShopCounterStock[s].y : 1;
                int maxLv = s < ShopCounterStock.Count ? ShopCounterStock[s].z : 12;
                names[s] = s < ShopCounterNames.Count && !string.IsNullOrEmpty(ShopCounterNames[s]) ? ShopCounterNames[s] + " - " + ShopNames[kind] : ShopNames[kind];
                greet[s] = ShopGreetings[kind];
                start.Add(items.Count);
                foreach (int cat in ShopCategories[kind])
                    for (int i = 0; i < itemDb.ids.Length; i++)
                    {
                        int id = itemDb.ids[i];
                        // Not the starter kit, and not quest rewards or boss drops (those are earned, never sold).
                        if (itemDb.categories[i] != cat || id <= 4 || id == CharacterData.STARTER_DECK_ITEM || itemDb.IsUnique(id)) continue;
                        if (itemDb.levelReqs[i] > maxLv || itemDb.levelReqs[i] < minLv) continue;
                        items.Add(id);
                    }
                counts.Add(items.Count - start[s]);
            }
            shop.shopNames = names;
            shop.shopGreetings = greet;
            shop.shopStart = start.ToArray();
            shop.shopCount = counts.ToArray();
            shop.shopItems = items.ToArray();
            shop.shopPrices = new int[items.Count];
            for (int k = 0; k < ShopCounters.Count; k++)
            {
                ShopCounters[k].target = shop;
                if (k >= GoldShop.MAX_SHOPS) Debug.LogWarning("[WizardsVRC] More than " + GoldShop.MAX_SHOPS + " shop counters; the extra ones open the last shop.");
                ShopCounters[k].eventName = "OpenShop" + Mathf.Min(k, GoldShop.MAX_SHOPS - 1);
                ExtraBehaviours.Add(ShopCounters[k]);
            }
            ExtraBehaviours.Add(shop);
            return shop;
        }

        /// <summary>A shop counter in front of a hub shop: a wooden counter with goods on it; click it to shop.</summary>
        private static void ShopCounter(Transform root, Vector3 face, Vector3 front, string label, Material wood, Material gold, int kind)
        {
            Vector3 side = Vector3.Cross(Vector3.up, front);
            Quaternion rot = Quaternion.LookRotation(front);
            Vector3 at = face + front * 2.6f + side * 2.3f;
            Block(root, "ShopCounter", at + new Vector3(0, 0.55f, 0), new Vector3(2.2f, 1.1f, 0.8f), wood, rot);
            Block(root, "ShopCounterTop", at + new Vector3(0, 1.14f, 0), new Vector3(2.4f, 0.08f, 1f), gold, rot);
            Shape(root, "ShopGoods", BlobMesh(), at + new Vector3(-0.5f, 1.35f, 0) , new Vector3(0.4f, 0.35f, 0.4f), PMat("PotionRed", new Color(0.85f, 0.2f, 0.25f), 1f, new Color(0.5f, 0.05f, 0.1f)), false, Quaternion.identity);
            Shape(root, "ShopGoods", GemMesh(), at + new Vector3(0.5f, 1.4f, 0), new Vector3(0.3f, 0.45f, 0.3f), gold, false, Quaternion.identity);
            var click = Child(root, "ShopClick", at + new Vector3(0, 1f, 0));
            click.localRotation = rot;
            var box = click.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(2.4f, 2f, 1.4f);
            box.isTrigger = false;
            var relay = UdonSharpUndo.AddComponent<EventRelay>(click.gameObject);
            SetInteractText(relay, "Shop: " + (string.IsNullOrEmpty(label) ? "browse wares" : label));
            ShopCounters.Add(relay);
            ShopCounterNames.Add(label);
            ShopCounterStock.Add(new Vector3Int(kind, _shopBand.x, _shopBand.y));
            WorldText(root, "ShopCounterSign", at + new Vector3(0, 2.1f, 0), rot * Quaternion.Euler(0, 180f, 0), new Vector2(600, 120), 0.004f, "Shop ●", 64).color = new Color(1f, 0.85f, 0.35f);
        }

        // ================================================================== cosmetics, player objects, wiring

        private static void FillCosmetics(CosmeticsDatabase db)
        {
            db.titleNames = TitleNames;
            int n = TitleNames.Length;
            db.titleUnlockKind = new int[n];
            db.titleUnlockValue = new int[n];
            db.titleHints = new string[n];
            for (int i = 0; i < n; i++)
            {
                db.titleUnlockKind[i] = TitleUnlocks[i, 0];
                db.titleUnlockValue[i] = TitleUnlocks[i, 1];
                db.titleHints[i] = UnlockHint(TitleUnlocks[i, 0], TitleUnlocks[i, 1]);
            }
            db.titlePrice = TitlePrices;
            int a = AuraNames.Length;
            db.auraNames = AuraNames;
            db.auraColors = AuraColors;
            db.auraUnlockKind = new int[a];
            db.auraUnlockValue = new int[a];
            db.auraHints = new string[a];
            for (int i = 0; i < a; i++)
            {
                db.auraUnlockKind[i] = AuraUnlocks[i, 0];
                db.auraUnlockValue[i] = AuraUnlocks[i, 1];
                db.auraHints[i] = UnlockHint(AuraUnlocks[i, 0], AuraUnlocks[i, 1]);
            }
            db.auraPrice = AuraPrices;
            db.skinNames = SkinNames;
            db.skinColors = SkinColors;
            db.skinPrice = SkinPrices;
            db.elixirNames = ElixirNames;
            db.elixirStat = ElixirStats;
            db.elixirAmount = ElixirAmounts;
            db.elixirPrice = ElixirPrices;
        }

        /// <summary>
        /// The per-player cosmetics object: VRChat makes one copy per player (VRCPlayerObject), owned by them. It carries
        /// their title (a label over their head) and one particle system per aura round their feet.
        /// </summary>
        private static PlayerCosmetics BuildPlayerCosmetics(Transform systems, CosmeticsDatabase db)
        {
            var go = Child(systems, "PlayerCosmetics", Vector3.zero).gameObject;
            var pc = UdonSharpUndo.AddComponent<PlayerCosmetics>(go);
            var playerObjectType = System.Type.GetType("VRC.SDK3.Components.VRCPlayerObject, VRC.SDK3");
            if (playerObjectType == null)
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    playerObjectType = asm.GetType("VRC.SDK3.Components.VRCPlayerObject");
                    if (playerObjectType != null) break;
                }
            if (playerObjectType != null) go.AddComponent(playerObjectType);
            else Debug.LogWarning("[WizardsVRC] VRCPlayerObject not found (needs VRChat SDK 3.7+): titles and auras will only show for one player.");
            pc.database = db;

            var titleRoot = Child(go.transform, "Title", new Vector3(0, 2.2f, 0));
            var label = WorldText(titleRoot, "Label", Vector3.zero, Quaternion.identity, new Vector2(900, 120), 0.0045f, "", 48);
            label.fontStyle = FontStyle.BoldAndItalic;
            pc.titleLabel = label;
            pc.titleRoot = titleRoot;
            // The world creator's title: 3D letters bobbing in a wave over their head (WizardsVRCSceneBuilder.CreatorTitle.cs).
            pc.creatorRoot = CreatorLetters(titleRoot, CreatorTitleText).gameObject;
            pc.creatorRoot.SetActive(false);
            titleRoot.gameObject.SetActive(false);

            ParticleSystem[] flares, maxFxs;
            pc.auras = BuildAuraSet(go.transform, out flares, out maxFxs);
            pc.auraFlares = flares;
            pc.auraMaxFx = maxFxs;
            ExtraBehaviours.Add(pc);
            return pc;
        }

        /// <summary>
        /// Every aura under 'root', one particle system each (index = aura), none playing: what a player wears, and the
        /// Starshard Shop's try-on copy. The school Surges also get their cast Flare and full-pips eruption (null for the
        /// other auras).
        /// </summary>
        private static ParticleSystem[] BuildAuraSet(Transform root, out ParticleSystem[] flares, out ParticleSystem[] maxFxs)
        {
            var auras = new ParticleSystem[AuraNames.Length];
            flares = new ParticleSystem[AuraNames.Length];
            maxFxs = new ParticleSystem[AuraNames.Length];
            for (int i = 0; i < auras.Length; i++)
            {
                if (i >= SurgeFirst)
                {
                    // A school Surge: it grows with your pips in a duel, flares when you cast and erupts at full pips.
                    ParticleSystem flare, maxFx;
                    auras[i] = SurgeAura(root, i, i - SurgeFirst, out flare, out maxFx);
                    flares[i] = flare;
                    maxFxs[i] = maxFx;
                    continue;
                }
                var ag = Child(root, "Aura" + i, Vector3.zero).gameObject;
                var ps = ag.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, i >= 8 ? 0.16f : 0.11f);
                main.startColor = new ParticleSystem.MinMaxGradient(AuraColors[i], Color.Lerp(AuraColors[i], Color.white, 0.5f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;
                main.cullingMode = ParticleSystemCullingMode.Pause;
                var em = ps.emission;
                em.rateOverTime = i >= 8 ? 26f : 18f;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.55f;
                sh.radiusThickness = 0.2f;
                sh.rotation = new Vector3(-90f, 0f, 0f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                bool falling = i == 1 || i == 3;   // frost mist and petals drift down from above
                vel.x = new ParticleSystem.MinMaxCurve(0f);
                vel.y = new ParticleSystem.MinMaxCurve(0f);
                vel.z = new ParticleSystem.MinMaxCurve(falling ? 0.15f : 0.5f, falling ? 0.3f : 1.1f);
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(i == 10 ? 2.5f : 1.2f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                if (i == 10) { ag.transform.localPosition = new Vector3(0, 2.05f, 0); sh.radius = 0.28f; vel.z = new ParticleSystem.MinMaxCurve(0f, 0.05f); }
                var pr = ag.GetComponent<ParticleSystemRenderer>();
                pr.sharedMaterial = SparkleMat("Aura" + i, AuraColors[i]);
                pr.renderMode = ParticleSystemRenderMode.Billboard;
                auras[i] = ps;
            }
            return auras;
        }

        /// <summary>Makes the cosmetics and shop systems and connects the book pages, notifications and tooltip to them.</summary>
        private static void WireExtras(Transform systems, PlayerState playerState, ItemDatabase itemDb, CardDatabase cardDb, InventoryBook book,
            PlayerHUD hud, DeckPages deck, QuestManager questManager, UiAudio uiAudio, List<BattleClient> encounters)
        {
            var db = UdonSharpUndo.AddComponent<CosmeticsDatabase>(Child(systems, "Cosmetics", Vector3.zero).gameObject);
            FillCosmetics(db);
            var store = UdonSharpUndo.AddComponent<StarshardStore>(Child(systems, "StarshardStore", Vector3.zero).gameObject);
            store.playerState = playerState;
            // Products are made on the VRChat creator site and dropped into the StarshardStore by hand (README).
            FillStoreProducts(store);
            ExtraBehaviours.Add(db);
            ExtraBehaviours.Add(store);
            db.playerState = playerState;
            db.store = store;
            db.questManager = questManager;
            BuildPlayerCosmetics(systems, db);

            var notices = BookNotices;
            notices.playerState = playerState;
            notices.cardDatabase = cardDb;
            notices.questManager = questManager;
            notices.uiAudio = uiAudio;
            db.notifications = notices;
            store.notifications = notices;
            book.notifications = notices;
            book.questManager = questManager;
            // HUD: a badge by the name and a one-line toast under the plate when something arrives.
            if (hud.hudRoot != null)
            {
                var hr = (RectTransform)hud.hudRoot.transform;
                var badge = UiImage(hr, "NoticeBadge", new Vector2(190, 160), new Vector2(40, 40), "NotifyBadge", Color.white, 1.3f);
                var count = Label(badge.transform, "Count", Vector2.zero, new Vector2(40, 40), "", 20, TextAnchor.MiddleCenter);
                count.gameObject.AddComponent<Outline>().effectColor = new Color(0.4f, 0f, 0f);
                badge.gameObject.SetActive(false);
                var list = new List<GameObject>(notices.badges ?? new GameObject[0]) { badge.gameObject };
                var counts = new List<Text>(notices.badgeCounts ?? new Text[0]) { count };
                notices.badges = list.ToArray();
                notices.badgeCounts = counts.ToArray();
                var toast = UiImage(hr, "Toast", new Vector2(0, -200), new Vector2(430, 56), "Tooltip", Color.white, 2f);
                notices.toastText = Label(toast.transform, "Text", Vector2.zero, new Vector2(410, 50), "", 18, TextAnchor.MiddleCenter);
                notices.toastText.color = new Color(1f, 0.92f, 0.7f);
                notices.toast = toast.gameObject;
                toast.gameObject.SetActive(false);
                ApplyCombatFont(hr);
            }

            BookWardrobe.playerState = playerState;
            BookWardrobe.cosmetics = db;
            BookWardrobe.uiAudio = uiAudio;
            BookShop.playerState = playerState;
            BookShop.cosmetics = db;
            BookShop.store = store;
            BookShop.uiAudio = uiAudio;

            var tip = BuildTooltip(systems);
            tip.playerState = playerState;
            tip.itemDatabase = itemDb;
            tip.cardDatabase = cardDb;
            book.tooltip = tip;
            deck.tooltip = tip;

            var shop = BuildGoldShop(systems, itemDb);
            shop.playerState = playerState;
            shop.itemDatabase = itemDb;
            shop.uiAudio = uiAudio;
            shop.notifications = notices;
            shop.tooltip = tip;

            foreach (var ec in encounters) { ec.notifications = notices; ec.cosmetics = db; }
            HookBookHovers(book, deck);
            // Something for every level up to 25 (WizardsVRCSceneBuilder.Rewards.cs).
            BuildLevelRewards(systems, playerState, itemDb, notices, db);
            // Dustwind's railroad, bounty board, High Noon and dust storms (WizardsVRCSceneBuilder.Railroad.cs).
            WireDustwind(systems, playerState, notices, uiAudio, cardDb, encounters);
        }

        /// <summary>Pointing at a backpack cell, a gear slot or a spell card shows its tooltip.</summary>
        private static void HookBookHovers(InventoryBook book, DeckPages deck)
        {
            UdonBehaviour bt = UdonSharpEditorUtility.GetBackingUdonBehaviour(book);
            UdonBehaviour dt = UdonSharpEditorUtility.GetBackingUdonBehaviour(deck);
            if (book.cellRoots != null)
                for (int i = 0; i < book.cellRoots.Length; i++)
                    if (book.cellRoots[i] != null) AddHoverEvents(book.cellRoots[i], bt, "HoverCell" + i, nameof(InventoryBook.Unhover));
            if (book.slotIcons != null)
                for (int i = 0; i < book.slotIcons.Length; i++)
                    if (book.slotIcons[i] != null) AddHoverEvents(book.slotIcons[i].gameObject, bt, "HoverSlot" + i, nameof(InventoryBook.Unhover));
            if (deck.deckCellRoots != null)
                for (int i = 0; i < deck.deckCellRoots.Length && i < 20; i++)
                    if (deck.deckCellRoots[i] != null) AddHoverEvents(deck.deckCellRoots[i], dt, "HoverDeck" + i, nameof(DeckPages.Unhover));
            if (deck.bookCellRoots != null)
                for (int i = 0; i < deck.bookCellRoots.Length && i < 12; i++)
                    if (deck.bookCellRoots[i] != null) AddHoverEvents(deck.bookCellRoots[i], dt, "HoverBook" + i, nameof(DeckPages.Unhover));
        }
    }
}
