using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    /// <summary>
    /// Starshard card packs (CardPacks): the catalogue below, the Starshard Shop's Packs category (ShopPages kind 4) and
    /// the pack-opening view, where the pulled cards lie face down and turn over one by one with their rarity glowing
    /// behind them. Pack art: Tools/make_pack_art.py (Textures/Shop/PackBody.png, tinted per pack, and PackTrim.png).
    /// </summary>
    public static partial class WizardsVRCSceneBuilder
    {
        /// <summary>One pack: its name, price in Starshards, colour, blurb and cards (kind, a, b, school or gear slot, Starshard-spell chance in 10,000).</summary>
        private class PackDef
        {
            public string name;
            public int price;
            public Color color;
            public string blurb;
            public List<int[]> slots = new List<int[]>();
        }

        // Starshard-spell chance (in 10,000) on an ordinary card, and on a pack's best card.
        private const int PackSpecialLow = 3;
        private const int PackSpecialHigh = 12;

        private static int[] TcSlot(int lo, int hi, int school, int special) { return new[] { CardPacks.SLOT_TREASURE, lo, hi, school, special }; }
        private static int[] GearSlot(int below, int above, int slot, int special) { return new[] { CardPacks.SLOT_GEAR, below, above, slot, special }; }
        private static int[] GoldSlot(int lo, int hi) { return new[] { CardPacks.SLOT_GOLD, lo, hi, CardPacks.ANY_SCHOOL, PackSpecialLow }; }

        private const string SpecialNote = "\nLudicrously rarely, any card can be a Starshard spell, yours to keep for good.";

        private static List<PackDef> PackCatalogue()
        {
            int any = CardPacks.ANY_SCHOOL;
            int wand = CharacterData.EQUIP_WEAPON;
            var list = new List<PackDef>();
            var p = new PackDef { name = "Spellbinder's Pack", price = 12, color = new Color(0.55f, 0.45f, 0.95f),
                blurb = "Five treasure cards of any school. Cheap spells are common; big ones are rare." + SpecialNote };
            for (int k = 0; k < 5; k++) p.slots.Add(TcSlot(0, 7, any, PackSpecialLow));
            list.Add(p);

            p = new PackDef { name = "Wanderer's Pack", price = 20, color = new Color(0.35f, 0.7f, 0.45f),
                blurb = "Three treasure cards, a piece of gear for your level and a wand for your level (your school's or any school's)." + SpecialNote };
            for (int k = 0; k < 3; k++) p.slots.Add(TcSlot(0, 7, any, PackSpecialLow));
            p.slots.Add(GearSlot(4, 1, any, PackSpecialLow));
            p.slots.Add(GearSlot(4, 2, wand, PackSpecialLow));
            list.Add(p);

            p = new PackDef { name = "Lorekeeper's Pack", price = 28, color = new Color(0.2f, 0.55f, 0.85f),
                blurb = "Three treasure cards, one more of 3 pips or more, and a piece of gear for your level." + SpecialNote };
            for (int k = 0; k < 3; k++) p.slots.Add(TcSlot(0, 7, any, PackSpecialLow));
            p.slots.Add(TcSlot(3, 7, any, PackSpecialHigh));
            p.slots.Add(GearSlot(3, 2, any, PackSpecialLow));
            list.Add(p);

            p = new PackDef { name = "Arcane Hoard Pack", price = 45, color = new Color(0.95f, 0.7f, 0.2f),
                blurb = "Six cards: two treasure cards, one of 5 pips or more, a wand and a piece of gear at or above your level, and a purse of gold." + SpecialNote };
            for (int k = 0; k < 2; k++) p.slots.Add(TcSlot(0, 7, any, PackSpecialLow));
            p.slots.Add(TcSlot(5, 7, any, PackSpecialHigh));
            p.slots.Add(GearSlot(1, 3, wand, PackSpecialLow));
            p.slots.Add(GearSlot(1, 3, any, PackSpecialLow));
            p.slots.Add(GoldSlot(150, 450));
            list.Add(p);

            string[] schoolPacks = { "Ember Pack", "Frost Pack", "Tempest Pack", "Grove Pack", "Gloom Pack", "Fable Pack", "Dune Pack" };
            for (int s = 0; s < schoolPacks.Length; s++)
            {
                p = new PackDef { name = schoolPacks[s], price = 15, color = SchoolColors[s],
                    blurb = "Five " + SchoolLabels[s] + " treasure cards, one of them 4 pips or more. Anyone can cast them." + SpecialNote };
                for (int k = 0; k < 4; k++) p.slots.Add(TcSlot(0, 7, s, PackSpecialLow));
                p.slots.Add(TcSlot(4, 7, s, PackSpecialHigh));
                list.Add(p);
            }
            return list;
        }

        private static CardPacks BuildCardPacks(Transform systems, PlayerState playerState, CardDatabase cardDb, ItemDatabase itemDb)
        {
            var packs = UdonSharpUndo.AddComponent<CardPacks>(Child(systems, "CardPacks", Vector3.zero).gameObject);
            packs.playerState = playerState;
            packs.cardDatabase = cardDb;
            packs.itemDatabase = itemDb;
            var defs = PackCatalogue();
            var names = new List<string>();
            var prices = new List<int>();
            var blurbs = new List<string>();
            var colors = new List<Color>();
            var starts = new List<int>();
            var counts = new List<int>();
            var kinds = new List<int>();
            var a = new List<int>();
            var b = new List<int>();
            var school = new List<int>();
            var special = new List<int>();
            foreach (var d in defs)
            {
                names.Add(d.name);
                prices.Add(d.price);
                blurbs.Add(d.blurb);
                colors.Add(d.color);
                starts.Add(kinds.Count);
                counts.Add(Mathf.Min(d.slots.Count, CardPacks.MAX_CARDS));
                foreach (var s in d.slots)
                {
                    kinds.Add(s[0]);
                    a.Add(s[1]);
                    b.Add(s[2]);
                    school.Add(s[3]);
                    special.Add(s[4]);
                }
            }
            packs.packNames = names.ToArray();
            packs.packPrices = prices.ToArray();
            packs.packBlurbs = blurbs.ToArray();
            packs.packColors = colors.ToArray();
            packs.packSlotStart = starts.ToArray();
            packs.packSlotCount = counts.ToArray();
            packs.slotKinds = kinds.ToArray();
            packs.slotA = a.ToArray();
            packs.slotB = b.ToArray();
            packs.slotSchool = school.ToArray();
            packs.slotSpecial = special.ToArray();
            ExtraBehaviours.Add(packs);
            return packs;
        }

        /// <summary>
        /// The pack-opening view over the shop's grid and preview: the pack's name, a row of up to six cards (each a back in
        /// the pack's colour and a face: frame, art or icon, name, what it is and its rarity, with a glow behind), a line
        /// about where things went, and Open Another / Done.
        /// </summary>
        private static void BuildPackReveal(RectTransform view, ShopPages pages, UdonBehaviour target)
        {
            Image backdrop = ShopImage(view, "PackReveal", new Vector2(197, -62), new Vector2(1530, 930), "GlassPanel", 3f, new Color(0.8f, 0.85f, 1f, 1f));
            backdrop.raycastTarget = true;   // nothing behind it can be pressed while a pack is open
            RectTransform root = backdrop.rectTransform;
            pages.revealRoot = root.gameObject;
            pages.revealTitle = ShopLabel(root, "Title", new Vector2(0, 390), new Vector2(1200, 80), "", 52, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.6f));
            pages.revealNote = ShopLabel(root, "Note", new Vector2(0, -300), new Vector2(1400, 60), "", 24, TextAnchor.MiddleCenter, ShopCyan);
            pages.revealNote.supportRichText = true;
            pages.revealAgainButton = ShopButton(root, "Again", new Vector2(-220, -390), new Vector2(400, 88), "Open Another", "BuyButton", target, nameof(ShopPages.RevealAgain), 30);
            pages.revealAgainLabel = pages.revealAgainButton.GetComponentInChildren<Text>();
            ShopButton(root, "Done", new Vector2(220, -390), new Vector2(300, 88), "Done", "BackButton", target, nameof(ShopPages.RevealDone), 32);

            int n = CardPacks.MAX_CARDS;
            pages.revealCards = new RectTransform[n];
            pages.revealBacks = new GameObject[n];
            pages.revealFronts = new GameObject[n];
            pages.revealFrames = new Image[n];
            pages.revealArts = new RawImage[n];
            pages.revealIcons = new Image[n];
            pages.revealNames = new Text[n];
            pages.revealKinds = new Text[n];
            pages.revealRarities = new Text[n];
            pages.revealGlows = new Image[n];
            pages.revealBackTints = new Image[n];
            Color outline = new Color(0.12f, 0.09f, 0.14f);
            for (int k = 0; k < n; k++)
            {
                RectTransform card = Rect(root, "Card" + k, new Vector2((k - (n - 1) / 2f) * 232f, -10f), new Vector2(214, 306));
                pages.revealCards[k] = card;
                Image glow = ShopImage(card, "Glow", new Vector2(0, 30), new Vector2(330, 430), "Glow", 6f, Color.white);
                glow.gameObject.SetActive(false);
                pages.revealGlows[k] = glow;

                // Face down: the pack's colour in a dark border, its seal in the middle.
                RectTransform back = Panel(card, "Back", new Vector2(0, 30), new Vector2(214, 306), new Color(0.08f, 0.07f, 0.16f));
                back.GetComponent<Image>().raycastTarget = false;
                RectTransform tint = Panel(back, "Tint", Vector2.zero, new Vector2(196, 288), Color.white);
                tint.GetComponent<Image>().raycastTarget = false;
                pages.revealBackTints[k] = tint.GetComponent<Image>();
                ShopImage(back, "Seal", Vector2.zero, new Vector2(170, 170), "PackTrim", 1f, Color.white);
                pages.revealBacks[k] = back.gameObject;

                // Face up.
                RectTransform front = Rect(card, "Front", Vector2.zero, new Vector2(214, 306));
                RectTransform frame = Panel(front, "Frame", new Vector2(0, 30), new Vector2(214, 306), Color.white);
                Image frameImg = frame.GetComponent<Image>();
                frameImg.raycastTarget = false;
                frameImg.type = Image.Type.Simple;
                pages.revealFrames[k] = frameImg;
                var artGo = new GameObject("Art", typeof(RectTransform));
                artGo.transform.SetParent(frame, false);
                ((RectTransform)artGo.transform).anchoredPosition = new Vector2(0, 41f);
                ((RectTransform)artGo.transform).sizeDelta = new Vector2(155, 104);
                var art = artGo.AddComponent<RawImage>();
                art.raycastTarget = false;
                pages.revealArts[k] = art;
                RectTransform icon = Panel(frame, "Icon", new Vector2(0, 30), new Vector2(110, 110), Color.white);
                icon.GetComponent<Image>().raycastTarget = false;
                pages.revealIcons[k] = icon.GetComponent<Image>();
                Text nm = Label(frame, "Name", new Vector2(0, 120), new Vector2(150, 32), "", 20, TextAnchor.MiddleCenter);
                nm.color = new Color(1f, 0.94f, 0.63f);
                nm.fontStyle = FontStyle.Bold;
                nm.resizeTextForBestFit = true;
                nm.resizeTextMinSize = 10;
                nm.resizeTextMaxSize = 20;
                nm.gameObject.AddComponent<Outline>().effectColor = outline;
                pages.revealNames[k] = nm;
                pages.revealKinds[k] = ShopLabel(front, "Kind", new Vector2(0, -170), new Vector2(226, 70), "", 18, TextAnchor.UpperCenter, ShopText);
                pages.revealKinds[k].resizeTextForBestFit = true;
                pages.revealKinds[k].resizeTextMinSize = 12;
                pages.revealKinds[k].resizeTextMaxSize = 18;
                pages.revealRarities[k] = ShopLabel(front, "Rarity", new Vector2(0, -228), new Vector2(226, 36), "", 26, TextAnchor.MiddleCenter, Color.white);
                pages.revealRarities[k].fontStyle = FontStyle.Bold;
                front.gameObject.SetActive(false);
                pages.revealFronts[k] = front.gameObject;
                card.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(false);
        }
    }
}
