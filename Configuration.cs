using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using OBSPlugin.Services;
using System;

namespace OBSPlugin
{
    public class Configuration : IPluginConfiguration
    {
        #region Connection Settings
        public bool Enabled = true;
        public bool UIDetection = true;
        public string SourceName = "FFXIV";
        public string Address = "ws://127.0.0.1:4455/"; // Default port was updated by obs-websocket.
        public string Password = "";
        #endregion

        #region Blur Settings
        public bool EnableBlur = false;
        public int BlurSize = 3;
        public bool BlurAsync = true;
        public bool DrawBlurRect = false;
        public bool ChatLogBlur = true;
        public bool PartyListBlur = true;
        public bool TargetBlur = false;
        public bool TargetTargetBlur = true;
        public bool FocusTargetBlur = false;
        public bool NamePlateBlur = false;
        public bool CharacterBlur = false;
        public bool FriendListBlur = false;
        public bool HotbarBlur = false;
        public bool CastBarBlur = false;
        public int MaxNamePlateCount = 1;
        public int[] BlurredHotbars = Array.Empty<int>();
        #endregion

        #region Record Settings
        public string RecordDir = "";
        public bool IncludeTerritory = true;
        public bool ZoneAsSuffix = false;
        public bool StartRecordOnCountDown = false;
        public bool StopRecordOnCountDownCancel = true;
        public bool StopRecordOnZoneExit = false;
        public bool UseDutyName = false;
        public bool StartReplayBufferOnRecord = false;
        public bool StartRecordOnCombat = false;
        public bool StopRecordOnCombat = false;
        public bool CancelStopRecordOnResume = true;
        public int StopRecordOnCombatDelay = 5;
        public bool DontStopInCutscene = true;
        public SubFolderModeType SubFolderMode = SubFolderModeType.Territory;
        public FileNameModeType FileNameMode = FileNameModeType.TerritorySuffix;
        public BitSet SelectedContents = new(0);
        public bool ShowAllFilters = false;
        #endregion

        #region Debug Settings
        public bool EnableDebug = false;
        public bool ResetReplayBufferDirByTerritory = false;
        public bool SaveReplayBufferOnCombat = false;
        public int SaveReplayBufferOnCombatDelay = 0;
        #endregion

        #region Version
        public int Version { get; set; }
        #endregion

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }

        public enum SubFolderModeType
        {
            None = 0,
            ContentName = 1,
            ContentType = 2,
            Territory = 3,
        }

        public enum FileNameModeType
        {
            None = 0,
            ContentNameSuffix = 1,
            ContentNamePrefix = 2,
            TerritorySuffix = 3,
            TerritoryPrefix = 4,
        }

        public enum TriState
        {
            Unchecked = 0,
            Checked = 1,
            Indeterminate = -1,
        }
    }
}