using Dalamud.Plugin.Services;
using OBSPlugin.Objects;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OBSPlugin
{
    #region Legacy
    // OLD STOP RECORDING LOGIC (from InCombatChanged handler - kept for reference)
    // This was the original implementation using Task.Run before refactoring to StopRecordingAsync()
    // new Task(async () =>
    // {
    //     try
    //     {
    //         PluginLog.Information($"Stop recording in {config.StopRecordOnCombatDelay} seconds");
    //         _stoppingRecord = true;
    //         var delay = config.StopRecordOnCombatDelay;
    //         var isViewingCutScene = false;
    //         do
    //         {
    //             _cts.Token.ThrowIfCancellationRequested();
    //             await Task.Delay(1000);
    //             delay -= 1;
    //             isViewingCutScene = await Framework.RunOnFrameworkThread(() => this.ObjectTable.LocalPlayer?.OnlineStatus.RowId == 15);
    //             PluginLog.Information($"isViewingCutScene: {isViewingCutScene}");
    //         } while (delay > 0 || (config.DontStopInCutscene && isViewingCutScene));
    //         PluginLog.Information("Auto stop recording");
    //         // this.ui.SetRecordingDir();
    //         obsConnection.OBS.StopRecord();
    //     }
    //     catch (ErrorResponseException err)
    //     {
    //         PluginLog.Warning("Stop Recording Error: {0}", err);
    //     }
    //     finally
    //     {
    //         _stoppingRecord = false;
    //         _cts.Dispose();
    //         _cts = new();
    //     }
    // }, _cts.Token).Start();
    #endregion
    public class AutoRecordLogic : IDisposable
    {
        private readonly Configuration _config;
        private readonly IFramework _framework;
        private readonly IObjectTable _objectTable;
        private readonly ObsConnection _obsConnection;
        private readonly CombatState _combatState;
        private readonly Action _setRecordingDir;
        private readonly Action _resetReplayBufferRecordingDir;

        private CancellationTokenSource _cts = new();
        private bool _stoppingRecord = false;

        public CombatState CombatState => _combatState;
        public float LastCountdownValue { get; private set; }

        public AutoRecordLogic(
            Configuration config,
            IFramework framework,
            IObjectTable objectTable,
            ObsConnection obsConnection,
            CombatState combatState,
            Action setRecordingDir,
            Action resetReplayBufferRecordingDir)
        {
            _config = config;
            _framework = framework;
            _objectTable = objectTable;
            _obsConnection = obsConnection;
            _combatState = combatState;
            _setRecordingDir = setRecordingDir;
            _resetReplayBufferRecordingDir = resetReplayBufferRecordingDir;

            _combatState.InCombatChanged += new EventHandler((object? sender, EventArgs e) =>
            {
                if (!_obsConnection.Connected)
                {
                    _obsConnection.TryConnect(_config.Address, _config.Password);
                    if (!_obsConnection.Connected) return;
                }
                if (this._combatState.InCombat && _config.StartRecordOnCombat)
                {
                    try
                    {
                        if (_config.CancelStopRecordOnResume && _stoppingRecord)
                        {
                            _cts.Cancel();
                        }
                        else if (_obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
                        {
                            var canStart = true;
                            if (_config.UseDutyFilter)
                            {
                                var content = Svc.DutyState.ContentFinderCondition;
                                canStart = content.IsValid && _config.FilterDuty.Contains(content.RowId);
                            }
                            if (canStart)
                            {
                                Svc.PluginLog.Information("Auto start recording");
                                this._setRecordingDir();
                                StartRecordingWithReplayBuffer();
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        Svc.PluginLog.Warning("Failed to start recording on combat: {0}", err.Message);
                    }
                }
                else if (!this._combatState.InCombat)
                {
                    if (_config.StopRecordOnCombat)
                    {
                        _ = StopRecordingAsync();
                    }
                    if (_config.SaveReplayBufferOnCombat)
                    {
                        new Task(() =>
                        {
                            try
                            {
                                var delay = _config.SaveReplayBufferOnCombatDelay;
                                do
                                {
                                    _cts.Token.ThrowIfCancellationRequested();
                                    Thread.Sleep(1000);
                                    delay -= 1;
                                } while (delay > 0);
                                Svc.PluginLog.Information("Auto save replay buffer");
                                _obsConnection.OBS.SaveReplayBuffer();
                            }
                            catch (ErrorResponseException err)
                            {
                                Svc.PluginLog.Warning("Stop Recording Error: {0}", err);
                            }
                        }).Start();
                    }
                }
            });
            _combatState.CountingDownChanged += new EventHandler((object? sender, EventArgs e) =>
            {
                if (!_obsConnection.Connected)
                {
                    _obsConnection.TryConnect(_config.Address, _config.Password);
                    return;
                }
                // Countdown started (CountingDown became true)
                if (this._combatState.CountingDown && _config.StartRecordOnCountDown && _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
                {
                    var canStart = true;
                    if (_config.UseDutyFilter)
                    {
                        var content = Svc.DutyState.ContentFinderCondition;
                        canStart = content.IsValid && _config.FilterDuty.Contains(content.RowId);
                    }
                    if (canStart)
                    {
                        try
                        {
                            Svc.PluginLog.Information("Countdown started - auto start recording");
                            this._setRecordingDir();
                            StartRecordingWithReplayBuffer();
                        }
                        catch (Exception err)
                        {
                            Svc.PluginLog.Warning("Failed to start recording on countdown: {0}", err.Message);
                        }
                    }
                }
                // Countdown stopped (CountingDown became false)
                else if (!this._combatState.CountingDown && _config.StopRecordOnCountDownCancel && _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                {
                    // If countdown value is > 0.5, it was canceled (not completed naturally)
                    if (LastCountdownValue > 0.5f)
                    {
                        try
                        {
                            Svc.PluginLog.Information("Countdown canceled - auto stop recording");
                            var path = _obsConnection.OBS.StopRecord();
                            File.Delete(path);
                        }
                        catch (ErrorResponseException err)
                        {
                            Svc.PluginLog.Warning("Stop Recording Error: {0}", err);
                        }
                    }
                    else
                    {
                        Svc.PluginLog.Information("Countdown completed (engage) - keeping recording active");
                    }
                }
                LastCountdownValue = this._combatState.CountDownValue;
                Svc.PluginLog.Debug("lastCountdownValue: {0}", LastCountdownValue);
            });
        }

        public void StartRecordingWithReplayBuffer()
        {
            try
            {
                _obsConnection.OBS.StartRecord();

                // Also start replay buffer if configured
                if (_config.StartReplayBufferOnRecord && _obsConnection.ObsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                {
                    try
                    {
                        _obsConnection.OBS.StartReplayBuffer();
                        Svc.PluginLog.Information("Started replay buffer with recording");
                    }
                    catch (ErrorResponseException err)
                    {
                        Svc.PluginLog.Debug("Could not start replay buffer: {0}", err.Message);
                    }
                }
            }
            catch (ErrorResponseException err)
            {
                Svc.PluginLog.Warning("Start Recording Error: {0}", err);
            }
        }

        public async Task StopRecordingAsync()
        {
            try
            {
                Svc.PluginLog.Information($"Stop recording in {_config.StopRecordOnCombatDelay} seconds");
                _stoppingRecord = true;
                var delay = _config.StopRecordOnCombatDelay;
                var isViewingCutScene = false;

                do
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    await Task.Delay(1000);
                    delay -= 1;
                    isViewingCutScene = await _framework.RunOnFrameworkThread(() => this._objectTable.LocalPlayer?.OnlineStatus.RowId == 15);
                    Svc.PluginLog.Information($"isViewingCutScene: {isViewingCutScene}");
                } while (delay > 0 || (_config.DontStopInCutscene && isViewingCutScene));

                Svc.PluginLog.Information("Auto stop recording");
                var path = _obsConnection.OBS.StopRecord();
                if (_config.FileNameMode != Configuration.FileNameModeType.None)
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var fileExt = Path.GetExtension(path);
                    if (_config.FileNameMode == Configuration.FileNameModeType.ContentNamePrefix || _config.FileNameMode == Configuration.FileNameModeType.ContentNameSuffix)
                    {
                        string dutyName;
                        if (Svc.DutyState.ContentFinderCondition.IsValid)
                        {
                            dutyName = Svc.DutyState.ContentFinderCondition.Value.Name.ToString();
                            if (string.IsNullOrEmpty(dutyName)) dutyName = "未知副本";
                        }
                        else
                        {
                            dutyName = "未知副本";
                        }
                        if (_config.FileNameMode == Configuration.FileNameModeType.ContentNamePrefix)
                        {
                            fileName = $"{dutyName}_{fileName}";
                        }
                        else
                        {
                            fileName = $"{fileName}_{dutyName}";
                        }
                    }
                    else if (_config.FileNameMode == Configuration.FileNameModeType.TerritoryPrefix || _config.FileNameMode == Configuration.FileNameModeType.TerritorySuffix)
                    {
                        var terrName = Svc.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRow(Svc.ClientState.TerritoryType).Map.Value.PlaceName.Value.Name.ToString();
                        if (string.IsNullOrEmpty(terrName)) terrName = "未知地区";
                        if (_config.FileNameMode == Configuration.FileNameModeType.TerritoryPrefix)
                        {
                            fileName = $"{terrName}_{fileName}";
                        }
                        else
                        {
                            fileName = $"{fileName}_{terrName}";
                        }
                    }

                    var tryCounter = 15;
                    while (tryCounter-- > 0)
                    {
                        try
                        {
                            await Task.Delay(1000);
                            File.Move(path, Path.Combine(Path.GetDirectoryName(path)!, $"{fileName}.{fileExt}"));
                            break;
                        }
                        catch {}

                    }
                }
            }
            catch (ErrorResponseException err)
            {
                Svc.PluginLog.Warning("Stop Recording Error: {0}", err);
            }
            finally
            {
                _stoppingRecord = false;
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }
        }

        public void OnTerritoryChanged(uint territoryId)
        {
            if (!_obsConnection.Connected || !_config.Enabled) return;

            // Stop recording when leaving a zone (e.g., after killing boss with cutscene)
            if (_config.StopRecordOnZoneExit && _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                try
                {
                    Svc.PluginLog.Information("Zone changed - auto stop recording");
                    _obsConnection.OBS.StopRecord();
                }
                catch (ErrorResponseException err)
                {
                    Svc.PluginLog.Warning("Stop Recording Error: {0}", err);
                }
            }

            if (_config.ResetReplayBufferDirByTerritory && _obsConnection.ObsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                if (_obsConnection.ObsRecordStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                {
                    new Task(() =>
                    {
                        _resetReplayBufferRecordingDir();
                    }).Start();
                }
                else
                {
                    Svc.PluginLog.Debug("Recording is active, cannot reset replay buffer dir.");
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}