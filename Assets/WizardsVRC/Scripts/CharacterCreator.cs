using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The character creator the awakening intro opens in the Headmaster's study, in four steps: a name picked from
    /// lists (first name + two-part surname, like Wizard101, so nobody can type a rude one), the school, the signature
    /// colour and the pip aura colour, then a last look before you begin. Begin creates and saves the character
    /// through <see cref="CharacterSelectUI"/> and hands back to the <see cref="AwakeningSequence"/>.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CharacterCreator : UdonSharpBehaviour
    {
        public const int STEP_NAME = 0;
        public const int STEP_SCHOOL = 1;
        public const int STEP_COLORS = 2;
        public const int STEP_CONFIRM = 3;

        [Header("Hand-off")]
        public CharacterSelectUI selectUI;
        public AwakeningSequence sequence;

        [Header("Panels")]
        public GameObject root;
        public GameObject[] steps;
        public Text stepLabel;

        [Header("Name")]
        public string[] firstNames;
        public string[] surnameStarts;
        public string[] surnameEnds;
        public Text firstLabel;
        public Text startLabel;
        public Text endLabel;
        public Text namePreview;

        [Header("School")]
        public string[] schoolNames;
        [TextArea] public string[] schoolBlurbs;
        public Color[] schoolColors;
        public GameObject[] schoolMarks;
        public Text schoolTitle;
        public Text schoolBlurb;
        public GameObject schoolNext;

        [Header("Colours (index 0 = the school's colour)")]
        public Color[] palette;
        public string[] colorNames;
        public GameObject[] sigMarks;
        public GameObject[] auraMarks;
        public Image[] sigSwatches;
        public Image[] auraSwatches;
        public Text sigLabel;
        public Text auraLabel;
        public Text colorPreviewName;
        public Image auraPreview;

        [Header("Confirm")]
        public Text summaryName;
        public Text summaryDetails;
        public Image summaryAura;

        private int _slot = -1;
        private int _step;
        private int _first, _start, _end;
        private int _school = -1;
        private int _sig, _aura;

        public bool IsOpen() { return root != null && root.activeSelf; }

        /// <summary>Opens on the name step with a random name, no school yet and both colours on the school's.</summary>
        public void Open(int slot)
        {
            _slot = slot;
            _school = -1;
            _sig = 0;
            _aura = 0;
            RandomName();
            if (root != null) root.SetActive(true);
            ShowStep(STEP_NAME);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        private void ShowStep(int step)
        {
            _step = step;
            if (steps != null)
                for (int i = 0; i < steps.Length; i++)
                    if (steps[i] != null) steps[i].SetActive(i == step);
            if (stepLabel != null) stepLabel.text = "Step " + (step + 1) + " of 4";
            if (step == STEP_SCHOOL) UpdateSchool();
            if (step == STEP_COLORS) UpdateColors();
            if (step == STEP_CONFIRM) UpdateSummary();
            Click();
        }

        // ------------------------------------------------------------------ name

        public void FirstPrev() { _first = Wrap(_first - 1, firstNames); UpdateName(); }
        public void FirstNext() { _first = Wrap(_first + 1, firstNames); UpdateName(); }
        public void StartPrev() { _start = Wrap(_start - 1, surnameStarts); UpdateName(); }
        public void StartNext() { _start = Wrap(_start + 1, surnameStarts); UpdateName(); }
        public void EndPrev() { _end = Wrap(_end - 1, surnameEnds); UpdateName(); }
        public void EndNext() { _end = Wrap(_end + 1, surnameEnds); UpdateName(); }

        public void RandomName()
        {
            _first = Random.Range(0, Len(firstNames));
            _start = Random.Range(0, Len(surnameStarts));
            _end = Random.Range(0, Len(surnameEnds));
            UpdateName();
        }

        public void NameNext() { ShowStep(STEP_SCHOOL); }

        public string FullName()
        {
            return At(firstNames, _first) + " " + At(surnameStarts, _start) + At(surnameEnds, _end);
        }

        private void UpdateName()
        {
            if (firstLabel != null) firstLabel.text = At(firstNames, _first);
            if (startLabel != null) startLabel.text = At(surnameStarts, _start);
            if (endLabel != null) endLabel.text = At(surnameEnds, _end).ToLower();
            if (namePreview != null) namePreview.text = FullName();
        }

        // ------------------------------------------------------------------ school

        public void School0() { PickSchool(0); }
        public void School1() { PickSchool(1); }
        public void School2() { PickSchool(2); }
        public void School3() { PickSchool(3); }
        public void School4() { PickSchool(4); }
        public void School5() { PickSchool(5); }
        public void School6() { PickSchool(6); }

        private void PickSchool(int s)
        {
            _school = s;
            UpdateSchool();
            Click();
        }

        public void SchoolBack() { ShowStep(STEP_NAME); }
        public void SchoolNext() { if (_school >= 0) ShowStep(STEP_COLORS); }

        private void UpdateSchool()
        {
            if (schoolMarks != null)
                for (int i = 0; i < schoolMarks.Length; i++)
                    if (schoolMarks[i] != null) schoolMarks[i].SetActive(i == _school);
            if (schoolNext != null) schoolNext.SetActive(_school >= 0);
            if (_school < 0)
            {
                if (schoolTitle != null) schoolTitle.text = "Which magic answers you?";
                if (schoolBlurb != null) schoolBlurb.text = "Pick a school to hear what it is about.";
                return;
            }
            if (schoolTitle != null)
            {
                schoolTitle.text = At(schoolNames, _school);
                schoolTitle.color = SchoolColor(_school);
            }
            if (schoolBlurb != null) schoolBlurb.text = At(schoolBlurbs, _school);
        }

        // ------------------------------------------------------------------ colours

        public void Sig0() { PickSig(0); }
        public void Sig1() { PickSig(1); }
        public void Sig2() { PickSig(2); }
        public void Sig3() { PickSig(3); }
        public void Sig4() { PickSig(4); }
        public void Sig5() { PickSig(5); }
        public void Sig6() { PickSig(6); }
        public void Sig7() { PickSig(7); }
        public void Sig8() { PickSig(8); }
        public void Sig9() { PickSig(9); }
        public void Sig10() { PickSig(10); }
        public void Sig11() { PickSig(11); }

        public void Aura0() { PickAura(0); }
        public void Aura1() { PickAura(1); }
        public void Aura2() { PickAura(2); }
        public void Aura3() { PickAura(3); }
        public void Aura4() { PickAura(4); }
        public void Aura5() { PickAura(5); }
        public void Aura6() { PickAura(6); }
        public void Aura7() { PickAura(7); }
        public void Aura8() { PickAura(8); }
        public void Aura9() { PickAura(9); }
        public void Aura10() { PickAura(10); }
        public void Aura11() { PickAura(11); }

        private void PickSig(int i) { _sig = i; UpdateColors(); Click(); }
        private void PickAura(int i) { _aura = i; UpdateColors(); Click(); }

        public void ColorsBack() { ShowStep(STEP_SCHOOL); }
        public void ColorsNext() { ShowStep(STEP_CONFIRM); }

        /// <summary>The colour an index stands for: 0 = this wizard's school colour.</summary>
        public Color ColorFor(int index, int school)
        {
            if (index <= 0 || palette == null || index >= palette.Length) return SchoolColor(school);
            return palette[index];
        }

        private void UpdateColors()
        {
            // The first swatch of each row shows the school's colour.
            Color sc = SchoolColor(_school);
            if (sigSwatches != null && sigSwatches.Length > 0 && sigSwatches[0] != null) sigSwatches[0].color = sc;
            if (auraSwatches != null && auraSwatches.Length > 0 && auraSwatches[0] != null) auraSwatches[0].color = sc;
            if (sigMarks != null)
                for (int i = 0; i < sigMarks.Length; i++)
                    if (sigMarks[i] != null) sigMarks[i].SetActive(i == _sig);
            if (auraMarks != null)
                for (int i = 0; i < auraMarks.Length; i++)
                    if (auraMarks[i] != null) auraMarks[i].SetActive(i == _aura);
            if (sigLabel != null) sigLabel.text = "Signature color: " + ColorName(_sig);
            if (auraLabel != null) auraLabel.text = "Pip aura: " + ColorName(_aura);
            if (colorPreviewName != null)
            {
                colorPreviewName.text = FullName();
                colorPreviewName.color = ColorFor(_sig, _school);
            }
            if (auraPreview != null) auraPreview.color = Faded(ColorFor(_aura, _school));
        }

        private string ColorName(int i)
        {
            if (i == 0) return "School color (" + At(schoolNames, _school) + ")";
            return At(colorNames, i);
        }

        // ------------------------------------------------------------------ confirm

        public void ConfirmBack() { ShowStep(STEP_COLORS); }

        private void UpdateSummary()
        {
            if (summaryName != null)
            {
                summaryName.text = FullName();
                summaryName.color = ColorFor(_sig, _school);
            }
            if (summaryDetails != null)
                summaryDetails.text = "Wizard of " + At(schoolNames, _school) + "\nSignature color: " + ColorName(_sig) + "\nPip aura: " + ColorName(_aura);
            if (summaryAura != null) summaryAura.color = Faded(ColorFor(_aura, _school));
        }

        /// <summary>Creates and saves the character, then lets the Headmaster carry on.</summary>
        public void Begin()
        {
            if (_school < 0) { ShowStep(STEP_SCHOOL); return; }
            if (selectUI == null) return;
            string name = FullName();
            if (!selectUI.CreateFromCreator(_slot, _school, name, _sig, _aura)) return;
            Close();
            if (sequence != null) sequence.OnCharacterCreated(name, _school);
        }

        // ------------------------------------------------------------------ helpers

        private Color SchoolColor(int s)
        {
            if (schoolColors == null || s < 0 || s >= schoolColors.Length) return Color.white;
            return schoolColors[s];
        }

        private Color Faded(Color c)
        {
            c.a = 0.85f;
            return c;
        }

        private int Len(string[] list) { return list == null ? 0 : list.Length; }

        private int Wrap(int i, string[] list)
        {
            int n = Len(list);
            if (n == 0) return 0;
            return ((i % n) + n) % n;
        }

        private string At(string[] list, int i)
        {
            if (list == null || i < 0 || i >= list.Length) return "";
            return list[i];
        }

        /// <summary>Buttons get their click sounds from the scene builder (WireUiSounds); nothing extra here.</summary>
        private void Click() { }
    }
}
