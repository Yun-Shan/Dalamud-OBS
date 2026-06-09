using Dalamud.IoC;
using Dalamud.Plugin;
using OBSPlugin.Attributes;
using OBSPlugin.Objects;
using System;

namespace OBSPlugin
{
    public class Plugin : IDalamudPlugin
    {
        // Only PluginInterface stays here
        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        internal Configuration config { get; private set; }
        internal readonly PluginUI ui;
        internal readonly ObsConnection obsConnection;

        private readonly ReplayCommandHandler _replayCommandHandler;
        private readonly StreamCommandHandler _streamCommandHandler;
        private readonly RecordCommandHandler _recordCommandHandler;
        private readonly AudioCommandHandler _audioCommandHandler;
        private readonly SceneCommandHandler _sceneCommandHandler;

        internal readonly OBSPlugin.Services.StopWatchHook stopWatchHook;
        internal CombatState combatState;
        internal readonly AutoRecordLogic autoRecordLogic;

        public string Name => "OBS Plugin";

        public Plugin()
        {
            PluginInterface.Create<Svc>();

            this.config = (Configuration?)PluginInterface.GetPluginConfig() ?? new Configuration();

            obsConnection = new ObsConnection(this.config, Svc.Chat, Svc.ClientState);

            _replayCommandHandler = new ReplayCommandHandler(obsConnection.OBS, obsConnection.ObsReplayBufferStatus);
            _streamCommandHandler = new StreamCommandHandler(obsConnection.OBS, obsConnection.OBS.GetStreamStatus);
            _recordCommandHandler = new RecordCommandHandler(obsConnection.OBS, obsConnection.OBS.GetRecordStatus);
            _audioCommandHandler = new AudioCommandHandler(obsConnection.OBS);
            _sceneCommandHandler = new SceneCommandHandler(obsConnection.OBS);

            this.ui = new PluginUI(this);
            PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
            PluginInterface.UiBuilder.DisableAutomaticUiHide = true;
            PluginInterface.UiBuilder.DisableGposeUiHide = true;
            PluginInterface.UiBuilder.DisableUserUiHide = true;
            PluginInterface.UiBuilder.Draw += this.ui.Draw;
            PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;


            combatState = new CombatState();
            autoRecordLogic = new AutoRecordLogic(
                config,
                Svc.Framework,
                Svc.ObjectTable,
                obsConnection,
                combatState,
                () => this.ui.RecordDirManager.SetRecordingDir(),
                () => this.ui.RecordDirManager.ResetReplayBufferRecordingDir());

            this.stopWatchHook = new OBSPlugin.Services.StopWatchHook(combatState, Svc.SigScanner, Svc.Condition, Svc.GameInteropProvider);

            Svc.PluginLog.Information("stopWatchHook");
            var commandManager = new PluginCommandManager<Plugin>(this);

            if (config.Password.Length > 0)
            {
                obsConnection.TryConnect(config.Address, config.Password);
            }

            Svc.ClientState.TerritoryChanged += tid => autoRecordLogic.OnTerritoryChanged(tid);
        }
        private void OpenConfigUi()
        {
            this.ui.IsVisible = true;
        }

        private void OpenMainUi()
        {
            this.ui.IsVisible = true;
        }

        [Command("/obs")]
        [HelpMessage("Open OBSPlugin config panel.")]
        public unsafe void ObsCommand(string command, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                this.ui.IsVisible = !this.ui.IsVisible;
                return;
            }

            string[] commandParts = args.Split(' ', 2);
            string mainCommand = commandParts[0];
            string commandArgs = commandParts.Length > 1 ? commandParts[1] : "";

            // Switching to a switch statement makes adding new main-commands easier and neater.
            switch (mainCommand)
            {
                case "config":
                    this.ui.IsVisible = !this.ui.IsVisible;
                    break;

                case "on":
                    this.config.Enabled = true;
                    this.config.Save();
                    break;

                case "off":
                    this.config.Enabled = false;
                    this.config.Save();
                    break;

                case "toggle":
                    this.config.Enabled = !this.config.Enabled;
                    this.config.Save();
                    break;

                case "update":
                    this.ui.UpdateGameUI();
                    break;

                case "replay":
                    if (!obsConnection.Connected) break;
                    _replayCommandHandler.HandleReplayCommand(commandArgs);
                    break;

                case "stream":
                    if (!obsConnection.Connected) break;
                    _streamCommandHandler.HandleStreamCommand(commandArgs);
                    break;

                case "record":
                    if (!obsConnection.Connected) break;
                    _recordCommandHandler.HandleRecordCommand(commandArgs);
                    break;

                case "audio":
                    if (!obsConnection.Connected) break;
                    _audioCommandHandler.HandleAudioCommand(commandArgs);
                    break;

                case "scene":
                    if (!obsConnection.Connected) break;
                    _sceneCommandHandler.HandleSceneCommand(commandArgs);
                    break;

                default:
                    Svc.Chat.PrintError($"[OBSPlugin] {args} is not a valid command.");
                    break;
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.stopWatchHook.Dispose();

            this.autoRecordLogic.Dispose();

            PluginInterface.SavePluginConfig(this.config);

            PluginInterface.UiBuilder.Draw -= this.ui.Draw;
            PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;

            this.ui.Dispose();

            this.obsConnection.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
