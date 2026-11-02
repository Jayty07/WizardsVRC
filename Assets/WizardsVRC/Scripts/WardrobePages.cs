using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Wardrobe: every title, aura and spell skin, which you have (and how to earn the rest), and buttons to
    /// wear them. Titles show over your head and auras round you for everyone, in the open world and in duels.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class WardrobePages : UdonSharpBehaviour
    {
        public const int TITLE_ROWS = 14;
        public const int AURA_ROWS = 21;
        public const int SKIN_ROWS = 4;

        public PlayerState playerState;
        public CosmeticsDatabase cosmetics;
        public UiAudio uiAudio;

        public GameObject[] titleRows;
        public Text[] titleTexts;
        public Image[] titleImages;
        public GameObject[] auraRows;
        public Text[] auraTexts;
        public Image[] auraImages;
        public Image[] auraSwatches;
        public GameObject[] skinRows;
        public Text[] skinTexts;
        public Image[] skinImages;
        public Text currentLabel;

        public Color wornColor = new Color(0.35f, 0.6f, 0.3f);
        public Color ownedColor = new Color(0.55f, 0.42f, 0.25f);
        public Color lockedColor = new Color(0.4f, 0.37f, 0.35f);

        public void Open() { Draw(); }

        public void TitleNone() { Wear(0, -1); }
        public void AuraNone() { Wear(1, -1); }
        public void SkinNone() { Wear(2, -1); }
        public void Title0() { Wear(0, 0); }
        public void Title1() { Wear(0, 1); }
        public void Title2() { Wear(0, 2); }
        public void Title3() { Wear(0, 3); }
        public void Title4() { Wear(0, 4); }
        public void Title5() { Wear(0, 5); }
        public void Title6() { Wear(0, 6); }
        public void Title7() { Wear(0, 7); }
        public void Title8() { Wear(0, 8); }
        public void Title9() { Wear(0, 9); }
        public void Title10() { Wear(0, 10); }
        public void Title11() { Wear(0, 11); }
        public void Title12() { Wear(0, 12); }
        public void Title13() { Wear(0, 13); }
        public void Aura0() { Wear(1, 0); }
        public void Aura1() { Wear(1, 1); }
        public void Aura2() { Wear(1, 2); }
        public void Aura3() { Wear(1, 3); }
        public void Aura4() { Wear(1, 4); }
        public void Aura5() { Wear(1, 5); }
        public void Aura6() { Wear(1, 6); }
        public void Aura7() { Wear(1, 7); }
        public void Aura8() { Wear(1, 8); }
        public void Aura9() { Wear(1, 9); }
        public void Aura10() { Wear(1, 10); }
        public void Aura11() { Wear(1, 11); }
        public void Aura12() { Wear(1, 12); }
        public void Aura13() { Wear(1, 13); }
        public void Aura14() { Wear(1, 14); }
        public void Aura15() { Wear(1, 15); }
        public void Aura16() { Wear(1, 16); }
        public void Aura17() { Wear(1, 17); }
        public void Aura18() { Wear(1, 18); }
        public void Aura19() { Wear(1, 19); }
        public void Aura20() { Wear(1, 20); }
        public void Skin0() { Wear(2, 0); }
        public void Skin1() { Wear(2, 1); }
        public void Skin2() { Wear(2, 2); }
        public void Skin3() { Wear(2, 3); }

        private void Wear(int kind, int i)
        {
            if (cosmetics == null || playerState == null || !playerState.HasCharacter()) return;
            bool ok = i < 0 || (kind == 0 ? cosmetics.HasTitle(i) : kind == 1 ? cosmetics.HasAura(i) : cosmetics.HasSkin(i));
            if (!ok) { if (uiAudio != null) uiAudio.PlayCancel(); return; }
            if (kind == 0) cosmetics.WearTitle(i);
            else if (kind == 1) cosmetics.WearAura(i);
            else cosmetics.WearSkin(i);
            if (uiAudio != null) uiAudio.PlayOk();
            Draw();
        }

        private void Draw()
        {
            if (cosmetics == null || playerState == null || !playerState.HasCharacter()) return;
            CharacterData c = playerState.current;
            for (int i = 0; i < TITLE_ROWS; i++)
            {
                bool has = cosmetics.titleNames != null && i < cosmetics.titleNames.Length;
                if (titleRows[i] != null && titleRows[i].activeSelf != has) titleRows[i].SetActive(has);
                if (!has) continue;
                bool own = cosmetics.HasTitle(i), worn = c.equippedTitle == i + 1;
                titleTexts[i].text = cosmetics.titleNames[i] + (own ? (worn ? "   <b>(worn)</b>" : "") : "\n<size=18>" + cosmetics.titleHints[i] + "</size>");
                titleImages[i].color = worn ? wornColor : own ? ownedColor : lockedColor;
            }
            for (int i = 0; i < AURA_ROWS; i++)
            {
                bool has = cosmetics.auraNames != null && i < cosmetics.auraNames.Length;
                if (auraRows[i] != null && auraRows[i].activeSelf != has) auraRows[i].SetActive(has);
                if (!has) continue;
                bool own = cosmetics.HasAura(i), worn = c.equippedAura == i + 1;
                auraTexts[i].text = cosmetics.auraNames[i] + (own ? (worn ? "   <b>(worn)</b>" : "") : "\n<size=18>" + cosmetics.auraHints[i] + "</size>");
                auraImages[i].color = worn ? wornColor : own ? ownedColor : lockedColor;
                if (auraSwatches != null && auraSwatches[i] != null) auraSwatches[i].color = cosmetics.auraColors[i];
            }
            for (int i = 0; i < SKIN_ROWS; i++)
            {
                bool has = cosmetics.skinNames != null && i < cosmetics.skinNames.Length;
                if (skinRows[i] != null && skinRows[i].activeSelf != has) skinRows[i].SetActive(has);
                if (!has) continue;
                bool own = cosmetics.HasSkin(i), worn = c.equippedSkin == i + 1;
                skinTexts[i].text = cosmetics.skinNames[i] + (own ? (worn ? "   <b>(worn)</b>" : "") : "\n<size=18>Starshard Shop</size>");
                skinImages[i].color = worn ? wornColor : own ? ownedColor : lockedColor;
            }
            if (currentLabel != null)
                currentLabel.text = "Title: " + (c.equippedTitle > 0 ? cosmetics.TitleName(c.equippedTitle) : "none") +
                                    "     Aura: " + (c.equippedAura > 0 && c.equippedAura <= cosmetics.auraNames.Length ? cosmetics.auraNames[c.equippedAura - 1] : "none") +
                                    "     Spell skin: " + (c.equippedSkin > 0 && c.equippedSkin <= cosmetics.skinNames.Length ? cosmetics.skinNames[c.equippedSkin - 1] : "school");
        }
    }
}
