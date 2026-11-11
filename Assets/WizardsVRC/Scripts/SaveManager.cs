using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace WizardsVRC
{
    /// <summary>
    /// Persists up to <see cref="SLOT_COUNT"/> characters in VRChat PlayerData for the local player.
    ///
    /// Keys:
    ///   char_N      base64 CharacterData blob (empty string = deleted)
    ///   char_N_bak  previous good blob, used for rollback when char_N fails its checksum
    ///   meta        "1|school,level,name|school,level,name|..." (empty entry = free slot)
    ///
    /// PlayerData has no delete call, so deleting writes empty strings.
    /// Data can only be read after OnPlayerRestored fires for the local player.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SaveManager : UdonSharpBehaviour
    {
        public const int SLOT_COUNT = 4;
        public const string META_KEY = "meta";
        private const string META_VERSION = "1";

        [Tooltip("Scratch instance used to validate blobs without touching live data.")]
        public CharacterData scratch;

        [Tooltip("Receives OnSaveDataReady and OnSaveMetaChanged events.")]
        public UdonSharpBehaviour listener;

        [HideInInspector] public bool isReady;

        private bool[] _slotUsed = new bool[SLOT_COUNT];
        private int[] _slotSchool = new int[SLOT_COUNT];
        private int[] _slotLevel = new int[SLOT_COUNT];
        private string[] _slotName = new string[SLOT_COUNT];

        // ------------------------------------------------------------------ VRChat events

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || !player.isLocal) return;
            isReady = true;
            ReloadMeta();
            Notify("OnSaveDataReady");
        }

#if VRC_ENABLE_PLAYER_PERSISTENCE
        public override void OnPlayerDataUpdated(VRCPlayerApi player, PlayerData.Info[] infos)
        {
            if (!isReady || !Utilities.IsValid(player) || !player.isLocal) return;
            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i].Key == META_KEY)
                {
                    ReloadMeta();
                    Notify("OnSaveMetaChanged");
                    return;
                }
            }
        }
#endif

        // ------------------------------------------------------------------ slot queries

        public bool HasSlot(int slot) { return IsSlotIndex(slot) && _slotUsed[slot]; }
        public int GetSlotSchool(int slot) { return IsSlotIndex(slot) ? _slotSchool[slot] : 0; }
        public int GetSlotLevel(int slot) { return IsSlotIndex(slot) ? _slotLevel[slot] : 0; }
        public string GetSlotName(int slot) { return IsSlotIndex(slot) && _slotName[slot] != null ? _slotName[slot] : ""; }

        public int FirstFreeSlot()
        {
            for (int i = 0; i < SLOT_COUNT; i++) if (!_slotUsed[i]) return i;
            return -1;
        }

        // ------------------------------------------------------------------ save / load / delete

        /// <summary>Writes data to slot, rotating the previous good blob into char_N_bak first.</summary>
        public bool SaveSlot(int slot, CharacterData data)
        {
            if (!isReady || !IsSlotIndex(slot) || data == null) return false;

            string blob = data.ToBase64();
            string key = SlotKey(slot);
            VRCPlayerApi local = Networking.LocalPlayer;

            string previous;
            if (PlayerData.TryGetString(local, key, out previous) && previous != blob && IsGoodBlob(previous))
                PlayerData.SetString(BackupKey(slot), previous);

            PlayerData.SetString(key, blob);

            _slotUsed[slot] = true;
            _slotSchool[slot] = (int)data.school;
            _slotLevel[slot] = data.level;
            _slotName[slot] = data.characterName;
            WriteMeta();
            return true;
        }

        /// <summary>Loads slot into target. Falls back to (and restores from) the backup if the main blob is corrupt.</summary>
        public bool LoadSlot(int slot, CharacterData target)
        {
            if (!isReady || !IsSlotIndex(slot) || target == null || scratch == null) return false;

            VRCPlayerApi local = Networking.LocalPlayer;
            string key = SlotKey(slot);
            string blob;
            if (PlayerData.TryGetString(local, key, out blob) && IsGoodBlob(blob))
            {
                target.CopyFrom(scratch);
                RefreshMetaFrom(slot, target, true);
                return true;
            }

            Debug.LogWarning("[SaveManager] " + key + " failed to load (" + scratch.lastError + "), trying backup");

            string backup;
            if (PlayerData.TryGetString(local, BackupKey(slot), out backup) && IsGoodBlob(backup))
            {
                target.CopyFrom(scratch);
                PlayerData.SetString(key, backup);
                RefreshMetaFrom(slot, target, true);
                Debug.LogWarning("[SaveManager] " + key + " rolled back to backup");
                return true;
            }

            Debug.LogError("[SaveManager] " + key + " and its backup are unreadable");
            return false;
        }

        public void DeleteSlot(int slot)
        {
            if (!isReady || !IsSlotIndex(slot)) return;
            PlayerData.SetString(SlotKey(slot), "");
            PlayerData.SetString(BackupKey(slot), "");
            _slotUsed[slot] = false;
            _slotSchool[slot] = 0;
            _slotLevel[slot] = 0;
            _slotName[slot] = "";
            WriteMeta();
            ClearPlace(slot, PLACE_LAST);
            ClearPlace(slot, PLACE_MARK);
        }

        // ------------------------------------------------------------------ places (LocationKeeper)

        /// <summary>Where the character in a slot last stood (PLACE_LAST) and their marked location (PLACE_MARK).</summary>
        public const int PLACE_LAST = 0, PLACE_MARK = 1;

        private Vector3 _placePos;
        private float _placeYaw;

        public string PlaceKey(int slot, int kind)
        {
            return (kind == PLACE_MARK ? "mark_" : "place_") + slot.ToString();
        }

        /// <summary>
        /// Remembers a place for a slot's character, on its own small key (so it can be written often without saving the
        /// whole character): whole centimetres and degrees, "x|y|z|yaw".
        /// </summary>
        public void SetPlace(int slot, int kind, Vector3 pos, float yaw)
        {
            if (!isReady || !IsSlotIndex(slot)) return;
            int deg = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f));
            PlayerData.SetString(PlaceKey(slot, kind), Mathf.RoundToInt(pos.x * 100f).ToString() + "|" + Mathf.RoundToInt(pos.y * 100f).ToString() + "|" +
                                                        Mathf.RoundToInt(pos.z * 100f).ToString() + "|" + deg.ToString());
        }

        public void ClearPlace(int slot, int kind)
        {
            if (!isReady || !IsSlotIndex(slot)) return;
            PlayerData.SetString(PlaceKey(slot, kind), "");
        }

        /// <summary>True if the slot has that place saved; then PlacePosition() and PlaceYaw() hold it.</summary>
        public bool LoadPlace(int slot, int kind)
        {
            if (!isReady || !IsSlotIndex(slot)) return false;
            string text;
            if (!PlayerData.TryGetString(Networking.LocalPlayer, PlaceKey(slot, kind), out text) || string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('|');
            if (parts.Length != 4) return false;
            int x, y, z, yaw;
            if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out y) || !int.TryParse(parts[2], out z) || !int.TryParse(parts[3], out yaw)) return false;
            _placePos = new Vector3(x * 0.01f, y * 0.01f, z * 0.01f);
            _placeYaw = yaw;
            return true;
        }

        public Vector3 PlacePosition() { return _placePos; }
        public float PlaceYaw() { return _placeYaw; }

        // ------------------------------------------------------------------ meta

        public void ReloadMeta()
        {
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slotUsed[i] = false;
                _slotSchool[i] = 0;
                _slotLevel[i] = 0;
                _slotName[i] = "";
            }

            string meta;
            if (PlayerData.TryGetString(Networking.LocalPlayer, META_KEY, out meta) && ParseMeta(meta))
                return;

            // Missing or corrupt meta: rebuild it from the blobs that still validate.
            bool any = false;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                _slotUsed[i] = false;
                _slotName[i] = "";
                string blob;
                bool good = PlayerData.TryGetString(Networking.LocalPlayer, SlotKey(i), out blob) && IsGoodBlob(blob);
                if (!good)
                    good = PlayerData.TryGetString(Networking.LocalPlayer, BackupKey(i), out blob) && IsGoodBlob(blob);
                if (good)
                {
                    RefreshMetaFrom(i, scratch, false);
                    any = true;
                }
            }
            if (any) WriteMeta();
        }

        private bool ParseMeta(string meta)
        {
            if (string.IsNullOrEmpty(meta)) return false;
            string[] parts = meta.Split('|');
            if (parts.Length != SLOT_COUNT + 1 || parts[0] != META_VERSION) return false;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                string entry = parts[i + 1];
                if (entry.Length == 0) continue;
                string[] fields = entry.Split(',');
                if (fields.Length != 3) return false;
                int school;
                int level;
                if (!int.TryParse(fields[0], out school) || !int.TryParse(fields[1], out level)) return false;
                _slotUsed[i] = true;
                _slotSchool[i] = school;
                _slotLevel[i] = level;
                _slotName[i] = fields[2];
            }
            return true;
        }

        private void WriteMeta()
        {
            string meta = META_VERSION;
            for (int i = 0; i < SLOT_COUNT; i++)
            {
                meta += "|";
                if (_slotUsed[i])
                    meta += _slotSchool[i].ToString() + "," + _slotLevel[i].ToString() + "," + _slotName[i];
            }
            PlayerData.SetString(META_KEY, meta);
        }

        private void RefreshMetaFrom(int slot, CharacterData data, bool write)
        {
            bool changed = !_slotUsed[slot] || _slotSchool[slot] != (int)data.school
                || _slotLevel[slot] != data.level || _slotName[slot] != data.characterName;
            _slotUsed[slot] = true;
            _slotSchool[slot] = (int)data.school;
            _slotLevel[slot] = data.level;
            _slotName[slot] = data.characterName;
            if (write && changed) WriteMeta();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Decodes into scratch; true if the blob is non-empty and passes version/checksum checks.</summary>
        private bool IsGoodBlob(string blob)
        {
            if (string.IsNullOrEmpty(blob) || scratch == null) return false;
            return scratch.FromBase64(blob);
        }

        private bool IsSlotIndex(int slot)
        {
            return slot >= 0 && slot < SLOT_COUNT;
        }

        public string SlotKey(int slot)
        {
            return "char_" + slot.ToString();
        }

        public string BackupKey(int slot)
        {
            return "char_" + slot.ToString() + "_bak";
        }

        private void Notify(string eventName)
        {
            if (listener != null) listener.SendCustomEvent(eventName);
        }
    }
}
