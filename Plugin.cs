using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Lumina.Excel.Sheets;
using OBSPlugin.Attributes;
using OBSPlugin.Objects;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using static FFXIVClientStructs.FFXIV.Client.System.String.Utf8String.Delegates;

namespace OBSPlugin
{
    public class Plugin : IDalamudPlugin
    {
        // Static services (injected via attribute)
        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService]
        internal static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService]
        internal static IDutyState DutyState { get; private set; } = null!;
        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;

        // Instance services (injected via constructor)
        internal ICommandManager Commands { get; }
        internal IChatGui Chat { get; }
        internal IClientState ClientState { get; }
        internal IObjectTable ObjectTable { get; }
        internal IFramework Framework { get; }
        internal IGameGui GameGui { get; }
        internal ISigScanner SigScanner { get; }
        internal ICondition Condition { get; }
        internal IDataManager Data { get; }
        internal IGameInteropProvider GameInteropProvider { get; }

        internal readonly PluginCommandManager<Plugin> commandManager;
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

        public Plugin(
            ICommandManager commands,
            IChatGui chat,
            IClientState clientState,
            IObjectTable objectTable,
            IFramework framework,
            IGameGui gameGui,
            ISigScanner sigScanner,
            ICondition condition,
            IDataManager data,
            IGameInteropProvider gameInteropProvider)
        {
            // Assign injected services
            Commands = commands;
            Chat = chat;
            ClientState = clientState;
            ObjectTable = objectTable;
            Framework = framework;
            GameGui = gameGui;
            SigScanner = sigScanner;
            Condition = condition;
            Data = data;
            GameInteropProvider = gameInteropProvider;

            this.config = (Configuration?)PluginInterface.GetPluginConfig() ?? new Configuration();

            obsConnection = new ObsConnection(this.config, Chat, ClientState);

            _replayCommandHandler = new ReplayCommandHandler(obsConnection.OBS, Chat, obsConnection.ObsReplayBufferStatus);
            _streamCommandHandler = new StreamCommandHandler(obsConnection.OBS, Chat, obsConnection.OBS.GetStreamStatus);
            _recordCommandHandler = new RecordCommandHandler(obsConnection.OBS, Chat, obsConnection.OBS.GetRecordStatus);
            _audioCommandHandler = new AudioCommandHandler(obsConnection.OBS, Chat);
            _sceneCommandHandler = new SceneCommandHandler(obsConnection.OBS, Chat);

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
                Framework,
                ObjectTable,
                obsConnection,
                combatState,
                () => this.ui.RecordDirManager.SetRecordingDir(),
                () => this.ui.RecordDirManager.ResetReplayBufferRecordingDir());

            this.stopWatchHook = new OBSPlugin.Services.StopWatchHook(combatState, SigScanner, Condition, GameInteropProvider);

            PluginLog.Information("stopWatchHook");
            this.commandManager = new PluginCommandManager<Plugin>(this, Commands);

            if (config.Password.Length > 0)
            {
                obsConnection.TryConnect(config.Address, config.Password);
            }

            ClientState.TerritoryChanged += tid => autoRecordLogic.OnTerritoryChanged(tid);
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
                    _replayCommandHandler.HandleReplayCommand(commandArgs, Chat);
                    break;

                case "stream":
                    if (!obsConnection.Connected) break;
                    _streamCommandHandler.HandleStreamCommand(commandArgs, Chat);
                    break;

                case "record":
                    if (!obsConnection.Connected) break;
                    _recordCommandHandler.HandleRecordCommand(commandArgs, Chat);
                    break;

                case "audio":
                    if (!obsConnection.Connected) break;
                    _audioCommandHandler.HandleAudioCommand(commandArgs, Chat);
                    break;

                case "scene":
                    if (!obsConnection.Connected) break;
                    _sceneCommandHandler.HandleSceneCommand(commandArgs, Chat);
                    break;

                default:
                    Chat.PrintError($"[OBSPlugin] {args} is not a valid command.");
                    break;
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

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
