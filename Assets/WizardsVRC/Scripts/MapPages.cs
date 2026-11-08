using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// The book's Map bookmark: storybook maps of every zone and every world, drawn at build time from the world data
    /// (WizardsVRCSceneBuilder.Maps.cs). Opens on the zone you're in, with a red marker where you stand, turned to your
    /// heading. The parchment tags on a zone map name its exits and landmarks; an exit's tag ("Kiln Street >") opens that
    /// zone's map. World Map shows the zones of this zone's world as floating islands (click one to open it), Next World
    /// steps through the worlds, and Where am I? comes back to the zone you're in.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MapPages : UdonSharpBehaviour
    {
        public const int LABELS = 24;

        [Header("The map view")]
        public RawImage mapImage;
        [Tooltip("The map's rectangle: the tags and the marker are placed in it by the map's 0..1 positions.")]
        public RectTransform mapRect;
        public Text titleLabel;
        public Text subtitleLabel;
        public GameObject[] labelButtons;
        public Text[] labelTexts;
        public RectTransform hereMarker;
        public Text worldButtonLabel;
        public UiAudio uiAudio;

        [Header("Maps (filled by the builder): the zones', then the worlds'")]
        public Texture[] maps;
        public string[] mapTitles;
        public string[] mapSubtitles;
        [Tooltip("The heights each zone spans (where you are is the zone whose band holds you); a world map has min > max.")]
        public float[] mapMinY;
        public float[] mapMaxY;
        [Tooltip("How a world position lands on each zone's map: u = dot(xyz, pos) + w, and the same for v.")]
        public Vector4[] mapProjU;
        public Vector4[] mapProjV;
        [Tooltip("Each zone's world map (-1 for world maps), and where the zone's island sits on it.")]
        public int[] mapWorld;
        public Vector2[] mapOnWorld;
        [Tooltip("The map to show when you aren't in any zone (the castle).")]
        public int homeMap;
        [Tooltip("The tags: which map each is on, its words, its middle (0..1), and the map it opens (-1 for none).")]
        public int[] labelMap;
        public string[] labelText;
        public Vector2[] labelPos;
        public int[] labelTarget;

        private int _shown = -1;
        private int[] _slotTarget = new int[LABELS];
        private float _nextMarker;
        private bool _markerOn;

        // ------------------------------------------------------------------ uGUI events

        /// <summary>Called by InventoryBook when the Map bookmark opens: the zone you're in (or the castle).</summary>
        public void Open()
        {
            int here = CurrentZone();
            ShowMap(here >= 0 ? here : homeMap);
        }

        /// <summary>On a zone map: its world's map. On a world map: the next world's.</summary>
        public void ShowWorld()
        {
            if (maps == null || maps.Length == 0) return;
            if (_shown >= 0 && !IsWorld(_shown) && mapWorld != null && _shown < mapWorld.Length && mapWorld[_shown] >= 0)
            {
                Click();
                ShowMap(mapWorld[_shown]);
                return;
            }
            // Next world map after the one shown (or the first).
            int from = _shown < 0 ? -1 : _shown;
            for (int k = 1; k <= maps.Length; k++)
            {
                int i = (from + k + maps.Length) % maps.Length;
                if (IsWorld(i)) { Click(); ShowMap(i); return; }
            }
        }

        public void ShowHere()
        {
            Click();
            int here = CurrentZone();
            ShowMap(here >= 0 ? here : homeMap);
        }

        public void Label0() { ClickLabel(0); }
        public void Label1() { ClickLabel(1); }
        public void Label2() { ClickLabel(2); }
        public void Label3() { ClickLabel(3); }
        public void Label4() { ClickLabel(4); }
        public void Label5() { ClickLabel(5); }
        public void Label6() { ClickLabel(6); }
        public void Label7() { ClickLabel(7); }
        public void Label8() { ClickLabel(8); }
        public void Label9() { ClickLabel(9); }
        public void Label10() { ClickLabel(10); }
        public void Label11() { ClickLabel(11); }
        public void Label12() { ClickLabel(12); }
        public void Label13() { ClickLabel(13); }
        public void Label14() { ClickLabel(14); }
        public void Label15() { ClickLabel(15); }
        public void Label16() { ClickLabel(16); }
        public void Label17() { ClickLabel(17); }
        public void Label18() { ClickLabel(18); }
        public void Label19() { ClickLabel(19); }
        public void Label20() { ClickLabel(20); }
        public void Label21() { ClickLabel(21); }
        public void Label22() { ClickLabel(22); }
        public void Label23() { ClickLabel(23); }

        private void ClickLabel(int slot)
        {
            if (slot < 0 || slot >= LABELS) return;
            int t = _slotTarget[slot];
            if (t < 0) return;
            Click();
            ShowMap(t);
        }

        private void Click()
        {
            if (uiAudio != null) uiAudio.PlayTab();
        }

        // ------------------------------------------------------------------ showing a map

        private bool IsWorld(int i)
        {
            return mapMinY != null && mapMaxY != null && i >= 0 && i < mapMinY.Length && i < mapMaxY.Length && mapMinY[i] > mapMaxY[i];
        }

        /// <summary>The zone whose height band holds you (the narrowest when bands overlap), or -1.</summary>
        private int CurrentZone()
        {
            VRCPlayerApi me = Networking.LocalPlayer;
            if (!Utilities.IsValid(me) || mapMinY == null || mapMaxY == null) return -1;
            float y = me.GetPosition().y;
            int best = -1;
            float bestSpan = float.MaxValue;
            for (int i = 0; i < mapMinY.Length && i < mapMaxY.Length; i++)
            {
                if (mapMinY[i] > mapMaxY[i] || y < mapMinY[i] || y > mapMaxY[i]) continue;
                float span = mapMaxY[i] - mapMinY[i];
                if (span < bestSpan) { best = i; bestSpan = span; }
            }
            return best;
        }

        private void ShowMap(int i)
        {
            if (maps == null || i < 0 || i >= maps.Length) return;
            _shown = i;
            if (mapImage != null) mapImage.texture = maps[i];
            if (titleLabel != null) titleLabel.text = mapTitles != null && i < mapTitles.Length ? mapTitles[i] : "";
            if (subtitleLabel != null) subtitleLabel.text = mapSubtitles != null && i < mapSubtitles.Length ? mapSubtitles[i] : "";
            float w = mapRect != null ? mapRect.rect.width : 1500f, h = mapRect != null ? mapRect.rect.height : 1000f;
            int slot = 0;
            if (labelMap != null && labelButtons != null)
                for (int k = 0; k < labelMap.Length && slot < LABELS && slot < labelButtons.Length; k++)
                {
                    if (labelMap[k] != i) continue;
                    GameObject b = labelButtons[slot];
                    if (b == null) { slot++; continue; }
                    if (labelTexts != null && slot < labelTexts.Length && labelTexts[slot] != null) labelTexts[slot].text = labelText[k];
                    var rt = b.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2((labelPos[k].x - 0.5f) * w, (labelPos[k].y - 0.5f) * h);
                    _slotTarget[slot] = labelTarget[k];
                    if (!b.activeSelf) b.SetActive(true);
                    slot++;
                }
            if (labelButtons != null)
                for (int k = slot; k < labelButtons.Length && k < LABELS; k++)
                {
                    _slotTarget[k] = -1;
                    if (labelButtons[k] != null && labelButtons[k].activeSelf) labelButtons[k].SetActive(false);
                }
            if (worldButtonLabel != null) worldButtonLabel.text = IsWorld(i) ? "Next World" : "World Map";
            PlaceMarker();
        }

        private Vector2 Project(int i, Vector3 p)
        {
            Vector4 u = mapProjU[i], v = mapProjV[i];
            return new Vector2(u.x * p.x + u.y * p.y + u.z * p.z + u.w, v.x * p.x + v.y * p.y + v.z * p.z + v.w);
        }

        /// <summary>You are here: on your zone's map where you stand, turned your way; on its world map, on its island.</summary>
        private void PlaceMarker()
        {
            if (hereMarker == null) return;
            bool show = false;
            Vector2 uv = Vector2.zero;
            float angle = 0f;
            int here = CurrentZone();
            VRCPlayerApi me = Networking.LocalPlayer;
            float w = mapRect != null ? mapRect.rect.width : 1500f, h = mapRect != null ? mapRect.rect.height : 1000f;
            if (here >= 0 && Utilities.IsValid(me) && _shown >= 0)
            {
                if (_shown == here && mapProjU != null && mapProjV != null && here < mapProjU.Length && here < mapProjV.Length)
                {
                    Vector3 p = me.GetPosition();
                    uv = Project(here, p);
                    Vector3 fwd = me.GetRotation() * Vector3.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                    Vector2 ahead = Project(here, p + fwd.normalized * 4f) - uv;
                    angle = Mathf.Atan2(ahead.y * h, ahead.x * w) * Mathf.Rad2Deg - 90f;
                    show = true;
                }
                else if (IsWorld(_shown) && mapWorld != null && here < mapWorld.Length && mapWorld[here] == _shown && mapOnWorld != null && here < mapOnWorld.Length)
                {
                    uv = mapOnWorld[here];
                    show = true;
                }
            }
            if (show && (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)) show = false;
            if (show != _markerOn || hereMarker.gameObject.activeSelf != show)
            {
                _markerOn = show;
                hereMarker.gameObject.SetActive(show);
            }
            if (!show) return;
            hereMarker.anchoredPosition = new Vector2((uv.x - 0.5f) * w, (uv.y - 0.5f) * h);
            hereMarker.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            // While the map is open, the marker follows you (ten times a second).
            if (mapImage == null || !mapImage.gameObject.activeInHierarchy || Time.time < _nextMarker) return;
            _nextMarker = Time.time + 0.1f;
            PlaceMarker();
        }
    }
}
