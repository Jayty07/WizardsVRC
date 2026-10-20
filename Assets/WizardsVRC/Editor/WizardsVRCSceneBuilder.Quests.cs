using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;

namespace WizardsVRC.EditorTools
{
    // Quest log tab in the book (QuestPages) and the objective guide arrow + beacon (QuestGuide).
    public static partial class WizardsVRCSceneBuilder
    {
        private static void BuildQuestPages(RectTransform root, InventoryBook book, QuestPages pages)
        {
            UdonBehaviour target = UdonSharpEditorUtility.GetBackingUdonBehaviour(pages);
            Color brown = new Color(0.45f, 0.3f, 0.18f);

            RectTransform view = Rect(root, "QuestView", Vector2.zero, new Vector2(1800, 1100));
            book.questView = view.gameObject;
            RectTransform left = Panel(view, "LeftPage", new Vector2(-445, 0), new Vector2(850, 1020), Parchment);
            RectTransform right = Panel(view, "RightPage", new Vector2(445, 0), new Vector2(850, 1020), Parchment);

            // ---- left page: quest list
            InkLabel(left, "Title", new Vector2(0, 450), new Vector2(800, 55), "Quests", 40, TextAnchor.MiddleCenter);
            InkLabel(left, "Hint", new Vector2(0, 395), new Vector2(800, 40), "Click a quest to read it.  > = tracked", 24, TextAnchor.MiddleCenter);
            int rows = QuestPages.ROWS;
            pages.rowRoots = new GameObject[rows];
            pages.rowImages = new Image[rows];
            pages.rowLabels = new Text[rows];
            for (int r = 0; r < rows; r++)
            {
                GameObject b = Button(left, "Quest" + r, new Vector2(0, 320 - r * 92), new Vector2(760, 80), "", Color.gray, target, "Row" + r);
                Text t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.offsetMin = new Vector2(20, 0);
                t.fontSize = 28;
                t.color = Ink;
                pages.rowRoots[r] = b;
                pages.rowImages[r] = b.GetComponent<Image>();
                pages.rowLabels[r] = t;
            }
            pages.emptyLabel = InkLabel(left, "Empty", new Vector2(0, 150), new Vector2(760, 100), "You have no quests yet.", 30, TextAnchor.MiddleCenter);
            Button(left, "PrevPage", new Vector2(-300, -440), new Vector2(150, 60), "<", brown, target, nameof(QuestPages.PrevPage));
            pages.pageLabel = InkLabel(left, "Page", new Vector2(0, -440), new Vector2(400, 50), "", 26, TextAnchor.MiddleCenter);
            Button(left, "NextPage", new Vector2(300, -440), new Vector2(150, 60), ">", brown, target, nameof(QuestPages.NextPage));

            // ---- right page: details
            RectTransform details = Rect(right, "Details", Vector2.zero, new Vector2(850, 1020));
            pages.detailsRoot = details.gameObject;
            pages.detailTitle = InkLabel(details, "QuestTitle", new Vector2(0, 440), new Vector2(800, 60), "", 40, TextAnchor.MiddleCenter);
            pages.detailStatus = InkLabel(details, "Status", new Vector2(0, 385), new Vector2(800, 40), "", 26, TextAnchor.MiddleCenter);
            pages.detailBody = InkLabel(details, "Story", new Vector2(0, 220), new Vector2(760, 260), "", 26, TextAnchor.UpperLeft);
            pages.detailObjective = InkLabel(details, "Objective", new Vector2(0, 20), new Vector2(760, 130), "", 28, TextAnchor.UpperLeft);
            pages.detailRewards = InkLabel(details, "Rewards", new Vector2(0, -100), new Vector2(760, 50), "", 26, TextAnchor.MiddleLeft);
            pages.trackButton = Button(details, "Track", new Vector2(0, -220), new Vector2(320, 80), "Track", new Color(0.3f, 0.5f, 0.3f), target, nameof(QuestPages.ToggleTrack));
            pages.trackLabel = pages.trackButton.GetComponentInChildren<Text>();
            GameObject guide = Button(right, "GuideToggle", new Vector2(0, -420), new Vector2(380, 70), "Guide arrow: On", brown, target, nameof(QuestPages.ToggleGuide));
            pages.guideLabel = guide.GetComponentInChildren<Text>();
            view.gameObject.SetActive(false);
        }

        private static QuestGuide BuildQuestGuide(Transform systems, PlayerState playerState, QuestManager qm, CutscenePlayer cutscenes,
            BattleClient battleClient, Transform interiorExit, Transform keepEntrance)
        {
            var guide = UdonSharpUndo.AddComponent<QuestGuide>(Child(systems, "QuestGuide", Vector3.zero).gameObject);
            guide.playerState = playerState;
            guide.questManager = qm;
            guide.cutscenePlayer = cutscenes;
            guide.battleClient = battleClient;
            guide.interiorExit = interiorExit;
            guide.keepEntrance = keepEntrance;
            guide.interiorMinY = InteriorOrigin.y * 0.5f;

            Material glow = PMat("QuestGuide", new Color(1f, 0.85f, 0.3f), 1f, new Color(1f, 0.75f, 0.2f));

            Transform arrow = Child(systems, "QuestArrow", Vector3.zero);
            GuidePiece(arrow, "Shaft", new Vector3(0, 0, -0.03f), Quaternion.identity, new Vector3(0.025f, 0.025f, 0.14f), glow);
            GuidePiece(arrow, "Head", new Vector3(0, 0, 0.05f), Quaternion.Euler(0, 45f, 0), new Vector3(0.08f, 0.025f, 0.08f), glow);
            guide.arrow = arrow;

            Transform labelRoot = Child(systems, "QuestArrowLabel", Vector3.zero);
            guide.label = WorldText(labelRoot, "Label", Vector3.zero, Quaternion.identity, new Vector2(600, 120), 0.0006f, "", 40);
            guide.label.color = new Color(1f, 0.9f, 0.5f);
            guide.labelRoot = labelRoot;

            Transform beacon = Child(systems, "QuestBeacon", Vector3.zero);
            GuidePiece(beacon, "Beam", new Vector3(0, 6f, 0), Quaternion.identity, new Vector3(0.3f, 6f, 0.3f), glow, PrimitiveType.Cylinder);
            guide.beacon = beacon;

            arrow.gameObject.SetActive(false);
            labelRoot.gameObject.SetActive(false);
            beacon.gameObject.SetActive(false);
            return guide;
        }

        private static void GuidePiece(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat,
            PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
