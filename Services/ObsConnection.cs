using Dalamud.IoC;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OBSPlugin
{
    public class ObsConnection : IDisposable
    {
        private readonly Configuration _config;
        private readonly IChatGui _chat;
        private readonly IClientState _clientState;

        private readonly OBSWebsocket _obs;
        private bool _connected;
        private bool _connectionFailed;
        private ObsVersion? _versionInfo;
        private OutputStatus? _streamStats;
        private OutputState _obsStreamStatus = OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED;
        private OutputState _obsRecordStatus = OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED;
        private OutputState _obsReplayBufferStatus = OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED;
        private bool _connectLock;
        private CancellationTokenSource? _keepAliveTokenSource;
        private readonly int _keepAliveInterval = 500;

        internal string MinimumPluginVersion { get; } = "5.3.0";

        public OBSWebsocket OBS => _obs;
        public bool Connected => _connected;
        public bool ConnectionFailed => _connectionFailed;
        public ObsVersion? VersionInfo => _versionInfo;
        public OutputStatus? StreamStats => _streamStats;
        public OutputState ObsStreamStatus => _obsStreamStatus;
        public OutputState ObsRecordStatus => _obsRecordStatus;
        public OutputState ObsReplayBufferStatus => _obsReplayBufferStatus;

        public ObsConnection(
            Configuration config,
            IChatGui chat,
            IClientState clientState)
        {
            _config = config;
            _chat = chat;
            _clientState = clientState;

            _obs = new OBSWebsocket();
            _obs.Connected += OnConnect;
            _obs.Disconnected += OnDisconnect;
            _obs.StreamStateChanged += OnStreamingStateChange;
            _obs.RecordStateChanged += OnRecordingStateChange;
            _obs.ReplayBufferStateChanged += OnReplayBufferStateChange;
        }

        public void TryConnect(string url, string password)
        {
            if (_connectLock)
            {
                return;
            }
            try
            {
                _connectLock = true;
                _obs.ConnectAsync(url, password);
                _connectionFailed = false;
            }
            catch (AuthFailureException)
            {
                _ = Task.Run(() => _obs.Disconnect());
                _connectionFailed = true;
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Connection error {0}", e);
            }
            finally
            {
                _connectLock = false;
            }
        }

        private void OnConnect(object? sender, EventArgs e)
        {
            _connected = true;
            Svc.PluginLog.Information("OBS connected: {0}", _config.Address);
            _versionInfo = _obs.GetVersion();
            var pluginVersion = _versionInfo.PluginVersion;
            var pVersion = new Version(pluginVersion);
            if (pVersion < new Version(MinimumPluginVersion))
            {
                string errMsg = $"Invalid obs-websocket-plugin version, needs {MinimumPluginVersion}, having {pluginVersion}";
                Svc.PluginLog.Error(errMsg);
                _chat.PrintError($"[OBSPlugin] {errMsg}");
                _obs.Disconnect();
                return;
            }
            var streamStatus = _obs.GetStreamStatus();
            if (streamStatus.IsActive)
                OnStreamingStateChange(_obs, new StreamStateChangedEventArgs(new OutputStateChanged() { IsActive = true, StateStr = nameof(OutputState.OBS_WEBSOCKET_OUTPUT_STARTED) }));
            else
                OnStreamingStateChange(_obs, new StreamStateChangedEventArgs(new OutputStateChanged() { IsActive = false, StateStr = nameof(OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED) }));
            var recordStatus = _obs.GetRecordStatus();
            if (recordStatus.IsRecording)
                OnRecordingStateChange(_obs, new RecordStateChangedEventArgs(new RecordStateChanged() { IsActive = true, StateStr = nameof(OutputState.OBS_WEBSOCKET_OUTPUT_STARTED) }));
            else
                OnRecordingStateChange(_obs, new RecordStateChangedEventArgs(new RecordStateChanged() { IsActive = false, StateStr = nameof(OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED) }));
            try
            {
                var replayBufferActive = _obs.GetReplayBufferStatus();
                if (replayBufferActive)
                    OnReplayBufferStateChange(_obs, new ReplayBufferStateChangedEventArgs(new OutputStateChanged() { IsActive = true, StateStr = nameof(OutputState.OBS_WEBSOCKET_OUTPUT_STARTED) }));
                else
                    OnReplayBufferStateChange(_obs, new ReplayBufferStateChangedEventArgs(new OutputStateChanged() { IsActive = false, StateStr = nameof(OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED) }));
            }
            catch (ErrorResponseException)
            {
                // Replay buffer not available/enabled in OBS - ignore
                Svc.PluginLog.Debug("Replay buffer not available in OBS");
            }
            if (_config.RecordDir.Equals(String.Empty))
            {
                var recordDir = _obs.GetRecordDirectory();
                _config.RecordDir = recordDir;
                _config.Save();
            }

            _keepAliveTokenSource = new CancellationTokenSource();
            CancellationToken keepAliveToken = _keepAliveTokenSource.Token;
            Task statPollKeepAlive = Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Thread.Sleep(_keepAliveInterval);
                    try
                    {
                        if (!_obs.IsConnected)
                        {
                            continue;
                        }
                        if (keepAliveToken.IsCancellationRequested)
                        {
                            break;
                        }
                        UpdateStreamStats(_obs.GetStreamStatus());
                    }
                    catch (Exception ex)
                    {
                        Svc.PluginLog.Error("Error getting obs streaming status", ex);
                    }
                }
            }, keepAliveToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void UpdateStreamStats(OutputStatus data)
        {
            _streamStats = data;
        }

        private void OnDisconnect(object? sender, ObsDisconnectionInfo e)
        {
            Svc.PluginLog.Information("OBS disconnected: {0}", _config.Address);
            _connected = false;
        }

        private void OnStreamingStateChange(object? sender, StreamStateChangedEventArgs newState)
        {
            _obsStreamStatus = newState.OutputState.State;
        }

        private void OnRecordingStateChange(object? sender, RecordStateChangedEventArgs newState)
        {
            _obsRecordStatus = newState.OutputState.State;
        }

        private void OnReplayBufferStateChange(object? sender, ReplayBufferStateChangedEventArgs newState)
        {
            _obsReplayBufferStatus = newState.OutputState.State;
        }

        public void Dispose()
        {
            if (_keepAliveTokenSource != null)
            {
                _keepAliveTokenSource.Cancel();
                _keepAliveTokenSource.Dispose();
                _keepAliveTokenSource = null;
            }

            if (_obs != null && _connected)
            {
                if (_config.RecordDir.Length > 0)
                    _obs.SetRecordDirectory(_config.RecordDir);
                _obs.Disconnect();
            }
        }
    }
}
