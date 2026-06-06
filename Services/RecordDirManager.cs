using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;

namespace OBSPlugin
{
    public class RecordDirManager
    {
        private readonly Configuration _config;
        private readonly IClientState _clientState;
        private readonly IDataManager _data;
        private readonly OBSWebsocket _obs;
        private readonly OutputState _obsRecordStatus;
        private readonly OutputState _obsReplayBufferStatus;
        private readonly IPluginLog _log;

        public RecordDirManager(
            Configuration config,
            IClientState clientState,
            IDataManager data,
            OBSWebsocket obs,
            OutputState obsRecordStatus,
            OutputState obsReplayBufferStatus,
            IPluginLog log)
        {
            _config = config;
            _clientState = clientState;
            _data = data;
            _obs = obs;
            _obsRecordStatus = obsRecordStatus;
            _obsReplayBufferStatus = obsReplayBufferStatus;
            _log = log;
        }

        public void SetRecordingDir()
        {
            if (_config.RecordDir == null || _config.RecordDir.Length == 0) return;
            if (_clientState == null || _clientState.TerritoryType == 0) return;

            var curDir = _config.RecordDir;
            if (_config.IncludeTerritory && _obsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
            {
                var terriIdx = _clientState.TerritoryType;
                string folderName;

                if (_config.UseDutyName)
                {
                    var territory = _data.GetExcelSheet<TerritoryType>().GetRow(terriIdx);
                    var cfcId = territory.ContentFinderCondition.RowId;
                    if (cfcId > 0)
                    {
                        var cfc = _data.GetExcelSheet<ContentFinderCondition>().GetRow(cfcId);
                        folderName = cfc.Name.ToString();
                    }
                    else
                    {
                        folderName = territory.Map.Value.PlaceName.Value.Name.ToString();
                    }
                }
                else
                {
                    var terriName = _data.GetExcelSheet<TerritoryType>().GetRow(terriIdx).Map.Value.PlaceName.Value.Name;
                    folderName = terriName.ToString();
                }

                curDir = Path.Combine(curDir, folderName);
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
                            _log.Warning("Start replay buffer error: {0}", err);
                        }
                        leftTimes -= 1;
                        Thread.Sleep(1000);
                    }
                    if (leftTimes == 0)
                    {
                        _log.Error("Cannot resume replay buffer...");
                    }
                }).Start();
            }
        }
    }
}