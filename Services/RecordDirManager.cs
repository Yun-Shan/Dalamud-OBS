using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static OBSPlugin.Configuration;

namespace OBSPlugin
{
    public class RecordDirManager
    {
        private readonly Configuration _config;
        private readonly IClientState _clientState;
        private readonly OBSWebsocket _obs;
        private readonly OutputState _obsRecordStatus;
        private readonly OutputState _obsReplayBufferStatus;

        public RecordDirManager(
            Configuration config,
            IClientState clientState,
            OBSWebsocket obs,
            OutputState obsRecordStatus,
            OutputState obsReplayBufferStatus)
        {
            _config = config;
            _clientState = clientState;
            _obs = obs;
            _obsRecordStatus = obsRecordStatus;
            _obsReplayBufferStatus = obsReplayBufferStatus;
        }

        public void SetRecordingDir()
        {
            if (_config.RecordDir == null || _config.RecordDir.Length == 0) return;
            if (_clientState == null || _clientState.TerritoryType == 0) return;

            var curDir = _config.RecordDir;
            if (_obsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
            {
                var terriIdx = _clientState.TerritoryType;
                string? folderName = null;
                switch (_config.SubFolderMode)
                {
                    case SubFolderModeType.ContentName:
                        if (Svc.DutyState.ContentFinderCondition.IsValid)
                        {
                            folderName = Svc.DutyState.ContentFinderCondition.Value.Name.ToString();
                            if (string.IsNullOrEmpty(folderName)) folderName = "未知副本";
                        }
                        else
                        {
                            folderName = "未知副本";
                        }
                        break;
                    case SubFolderModeType.ContentType:
                        if (Svc.DutyState.ContentFinderCondition.IsValid)
                        {
                            folderName = Svc.DutyState.ContentFinderCondition.Value.ContentType.ToString();
                            if (string.IsNullOrEmpty(folderName)) folderName = "未知副本类型";
                        }
                        else
                        {
                            folderName = "未知副本类型";
                        }
                        break;
                    case SubFolderModeType.Territory:
                        var terrName = Svc.DataManager.GetExcelSheet<TerritoryType>().GetRow(Svc.ClientState.TerritoryType).Map.Value.PlaceName.Value.Name.ToString();
                        if (string.IsNullOrEmpty(terrName)) terrName = "未知地区";
                        folderName = terrName;
                        break;
                    default: break;
                }

                if (folderName != null)
                {
                    curDir = Path.Combine(curDir, folderName);
                }
            }

            if (!Directory.Exists(curDir))
            {
                Directory.CreateDirectory(curDir);
            }

            _obs.SetRecordDirectory(curDir);
        }

        public void ResetReplayBufferRecordingDir()
        {
            if (!_config.Enabled) return;
            bool needToResume = false;
            if (_obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                needToResume = true;
                _obs.StopReplayBuffer();
            }
            SetRecordingDir();
            if (needToResume)
            {
                new Task(() =>
                {
                    var leftTimes = 5;
                    while (leftTimes > 0)
                    {
                        if (_obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                        {
                            break;
                        }
                        try
                        {
                            _obs.StartReplayBuffer();
                        }
                        catch (ErrorResponseException err)
                        {
                            Svc.PluginLog.Warning("Start replay buffer error: {0}", err);
                        }
                        leftTimes -= 1;
                        Thread.Sleep(1000);
                    }
                    if (leftTimes == 0)
                    {
                        Svc.PluginLog.Error("Cannot resume replay buffer...");
                    }
                }).Start();
            }
        }
    }
}