using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    // Spell learning: the professors' spell list (SpellTrainer), the "new spell" card popup and the shared NPC
    // interaction menu. All three are single world-space panels that move in front of the player when opened.
    public static partial class WizardsVRCSceneBuilder
    {
        private static readonly Color TrainerBg = new Color(0.1f, 0.08f, 0.16f, 0.95f);
        private static readonly Color TrainerButton = new Color(0.35f, 0.25f, 0.5f);

        private static List<UdonSharpBehaviour> BuildSpellLearning(Transform systems, PlayerState playerState, CardDatabase cardDb, PlayerHUD hud,
            out SpellTrainer trainer, out InteractionMenu menu)
        {
            var list = new List<UdonSharpBehaviour>();

            // ---- new spell popup
            var popup = UdonSharpUndo.AddComponent<NewSpellPopup>(Child(systems, "NewSpellPopup", Vector3.zero).gameObject);
            popup.cardDatabase = cardDb;
            var popupGo = new GameObject("NewSpellPopupCanvas", typeof(RectTransform));
            popupGo.transform.SetParent(popup.transform, false);
            MakeWorldCanvas(popupGo, new Vector2(900, 700), Vector3.zero, 0.001f);
            popup.popupRoot = popupGo;
            RectTransform pr = (RectTransform)popupGo.transform;
            UdonBehaviour popupTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(popup);
            Panel(pr, "Background", Vector2.zero, new Vector2(900, 700), new Color(0.12f, 0.09f, 0.2f, 0.96f));
            Label(pr, "Header", new Vector2(0, 300), new Vector2(860, 70), "NEW SPELL!", 52, TextAnchor.MiddleCenter).color = new Color(1f, 0.85f, 0.35f);
            // The new spell as a card, with the same art as in battle.
            var card = CardFace(pr, "Card", new Vector2(-220, -10), 1.7f, false, null, null);
            popup.cardFrame = card.frame;
            popup.cardName = card.name;
            popup.cardPips = card.pips;
            popup.cardArt = card.art;
            popup.cardAccuracy = card.accuracy;
            popup.cardSchool = card.school;
            popup.cardEffect = card.info;
            popup.congratsLabel = Label(pr, "Congrats", new Vector2(190, 40), new Vector2(440, 300), "", 30, TextAnchor.MiddleLeft);
            Button(pr, "OK", new Vector2(190, -220), new Vector2(240, 80), "OK", TrainerButton, popupTarget, nameof(NewSpellPopup.Hide));
            popupGo.SetActive(false);
            list.Add(popup);

            // ---- professor spell list
            trainer = UdonSharpUndo.AddComponent<SpellTrainer>(Child(systems, "SpellTrainer", Vector3.zero).gameObject);
            trainer.playerState = playerState;
            trainer.cardDatabase = cardDb;
            trainer.popup = popup;
            trainer.hud = hud;
            trainer.professorNames = (string[])ProfessorNames.Clone();
            trainer.schoolNames = (string[])SchoolLabels.Clone();
            var tGo = new GameObject("SpellTrainerCanvas", typeof(RectTransform));
            tGo.transform.SetParent(trainer.transform, false);
            MakeWorldCanvas(tGo, new Vector2(1400, 1000), Vector3.zero, 0.001f);
            trainer.panelRoot = tGo;
            RectTransform tr = (RectTransform)tGo.transform;
            UdonBehaviour tTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(trainer);
            Panel(tr, "Background", Vector2.zero, new Vector2(1400, 1000), TrainerBg);
            trainer.titleLabel = Label(tr, "Title", new Vector2(0, 430), new Vector2(1300, 110), "", 40, TextAnchor.MiddleCenter);
            trainer.titleLabel.color = new Color(1f, 0.85f, 0.4f);
            trainer.pointsLabel = Label(tr, "Points", new Vector2(0, 350), new Vector2(1300, 45), "", 28, TextAnchor.MiddleCenter);

            // The school's spells as cards (the same art as in battle), four to a row, each with its status under it:
            // greyed out until you can learn it, highlighted when picked.
            int rows = SpellTrainer.ROWS;
            trainer.rowRoots = new GameObject[rows];
            trainer.rowIcons = new Image[rows];
            trainer.rowNames = new Text[rows];
            trainer.rowInfo = new Text[rows];
            trainer.rowPips = new Text[rows];
            trainer.rowArts = new RawImage[rows];
            for (int r = 0; r < rows; r++)
            {
                int col = r % 4, row = r / 4;
                Vector2 at = new Vector2(-600 + col * 152, 210 - row * 225);
                var card = CardFace(tr, "Row" + r, at, 0.64f, true, tTarget, "Row" + r);
                // The status sits under the card, as part of it (so it hides with the card).
                Text info = Label(card.root.transform, "Info", new Vector2(0, -172), new Vector2(230, 56), "", 26, TextAnchor.UpperCenter);
                info.raycastTarget = false;
                trainer.rowRoots[r] = card.root;
                trainer.rowIcons[r] = card.frame;
                trainer.rowNames[r] = card.name;
                trainer.rowPips[r] = card.pips;
                trainer.rowArts[r] = card.art;
                trainer.rowInfo[r] = info;
            }

            RectTransform details = Panel(tr, "Details", new Vector2(345, 30), new Vector2(620, 560), new Color(0.2f, 0.16f, 0.3f));
            trainer.detailsPanel = details.gameObject;
            var big = CardFace(details, "SpellCard", new Vector2(-170, 60), 1.3f, false, null, null);
            trainer.detailFrame = big.frame;
            trainer.detailCardName = big.name;
            trainer.detailPips = big.pips;
            trainer.detailArt = big.art;
            trainer.detailAccuracy = big.accuracy;
            trainer.detailInfo = big.info;
            trainer.detailSchool = big.school;
            trainer.detailName = Label(details, "SpellName", new Vector2(150, 220), new Vector2(300, 60), "", 30, TextAnchor.MiddleLeft);
            trainer.detailBody = Label(details, "SpellBody", new Vector2(150, 40), new Vector2(300, 300), "", 20, TextAnchor.UpperLeft);
            trainer.learnButton = Button(details, "Learn", new Vector2(0, -225), new Vector2(320, 80), "Learn", new Color(0.25f, 0.5f, 0.3f), tTarget, nameof(SpellTrainer.LearnSelected));
            trainer.learnLabel = trainer.learnButton.GetComponentInChildren<Text>();
            details.gameObject.SetActive(false);

            trainer.statusLabel = Label(tr, "Status", new Vector2(345, -320), new Vector2(620, 60), "", 26, TextAnchor.MiddleCenter);
            trainer.statusLabel.color = new Color(1f, 0.6f, 0.5f);
            Button(tr, "Close", new Vector2(345, -420), new Vector2(240, 75), "Close", new Color(0.45f, 0.25f, 0.2f), tTarget, nameof(SpellTrainer.Close));
            tGo.SetActive(false);
            list.Add(trainer);

            // ---- interaction menu
            menu = UdonSharpUndo.AddComponent<InteractionMenu>(Child(systems, "InteractionMenu", Vector3.zero).gameObject);
            var mGo = new GameObject("InteractionMenuCanvas", typeof(RectTransform));
            mGo.transform.SetParent(menu.transform, false);
            MakeWorldCanvas(mGo, new Vector2(600, 520), Vector3.zero, 0.001f);
            menu.menuRoot = mGo;
            RectTransform mr = (RectTransform)mGo.transform;
            UdonBehaviour mTarget = UdonSharpEditorUtility.GetBackingUdonBehaviour(menu);
            Panel(mr, "Background", Vector2.zero, new Vector2(600, 520), TrainerBg);
            menu.titleLabel = Label(mr, "Title", new Vector2(0, 205), new Vector2(560, 90), "", 30, TextAnchor.MiddleCenter);
            menu.titleLabel.color = new Color(1f, 0.85f, 0.4f);
            int n = InteractionMenu.MAX_OPTIONS;
            menu.optionButtons = new GameObject[n];
            menu.optionLabels = new Text[n];
            for (int i = 0; i < n; i++)
            {
                GameObject b = Button(mr, "Option" + i, new Vector2(0, 100 - i * 85), new Vector2(460, 72), "", TrainerButton, mTarget, "Option" + i);
                menu.optionButtons[i] = b;
                menu.optionLabels[i] = b.GetComponentInChildren<Text>();
            }
            Button(mr, "Leave", new Vector2(0, -215), new Vector2(300, 60), "Leave", new Color(0.45f, 0.25f, 0.2f), mTarget, nameof(InteractionMenu.Close));
            mGo.SetActive(false);
            list.Add(menu);
            return list;
        }

        private static NpcInteractions Interactions(GameObject go, string title, InteractionMenu menu, string[] labels, UdonSharpBehaviour[] targets, string[] events)
        {
            var npc = UdonSharpUndo.AddComponent<NpcInteractions>(go);
            npc.title = title;
            npc.menu = menu;
            npc.labels = labels;
            npc.targets = targets;
            npc.events = events;
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(npc);
            if (backing != null) backing.interactText = "Talk";
            return npc;
        }
    }
}
