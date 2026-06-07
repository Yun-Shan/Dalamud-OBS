using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json.Linq;
using OBSPlugin.Objects;
using OBSPlugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using System.IO;
using System.Threading;

namespace OBSPlugin
{
    public class PluginUI
    {
        private readonly Plugin Plugin;
        private readonly BlurManager _blurManager;
        private readonly UIGameStateTracker _uiGameStateTracker;
        private readonly RecordDirManager _recordDirManager;
        private bool isThreadRunning = true;

        // Tab instances
        private readonly ConnectionTab _connectionTab;
        private readonly StreamTab _streamTab;
        private readonly RecordTab _recordTab;
        private readonly ReplayTab _replayTab;
        private readonly BlurTab _blurTab;
        private readonly AboutTab _aboutTab;
        private readonly DebugTab _debugTab;

        public RecordDirManager RecordDirManager => _recordDirManager;

        public Configuration Config => Plugin.config;

        public bool IsVisible { get; set; }

        public PluginUI(Plugin plugin)
        {
            Plugin = plugin;

            // Create BlurManager
            _blurManager = new BlurManager(Plugin.obsConnection.OBS, Config);

            // Create UIGameStateTracker
            _uiGameStateTracker = new UIGameStateTracker(Config, Svc.GameGui, Svc.ObjectTable, _blurManager);

            // Create RecordDirManager
            _recordDirManager = new RecordDirManager(
                Config,
                Svc.ClientState,
                Plugin.obsConnection.OBS,
                Plugin.obsConnection.ObsRecordStatus,
                Plugin.obsConnection.ObsReplayBufferStatus);

            // Create tab instances
            _connectionTab = new ConnectionTab(Config, Plugin.obsConnection, Svc.Chat);
            _streamTab = new StreamTab(Plugin.obsConnection, Svc.Chat);
            _recordTab = new RecordTab(Config, Plugin.obsConnection, Svc.Chat, _recordDirManager.SetRecordingDir);
            _replayTab = new ReplayTab(Config, Plugin.obsConnection, Svc.Chat);
            _blurTab = new BlurTab(Config, _blurManager);
            _aboutTab = new AboutTab();
            _debugTab = new DebugTab(Config, Svc.Chat);
        }

        public void Draw()
        {
            try
            {
                Plugin.stopWatchHook?.Update();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error at updating stopwatch: {0}", e);
            }

            if (Config.UIDetection)
                _uiGameStateTracker.UpdateGameUI();

            if (!IsVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(530, 450), ImGuiCond.FirstUseEver);
            bool configOpen = IsVisible;
            if (ImGui.Begin("OBS 插件配置", ref configOpen))
            {
                IsVisible = configOpen;
                if (ImGui.BeginTabBar("TabBar"))
                {
                    if (ImGui.BeginTabItem("连接##Tab"))
                    {
                        if (ImGui.BeginChild("Connection##SettingsRegion"))
                        {
                            _connectionTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("直播##Tab"))
                    {
                        if (ImGui.BeginChild("Stream##SettingsRegion"))
                        {
                            _streamTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("录制##Tab"))
                    {
                        if (ImGui.BeginChild("Record##SettingsRegion"))
                        {
                            _recordTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("回放##Tab"))
                    {
                        if (ImGui.BeginChild("Replay##SettingsRegion"))
                        {
                            _replayTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("模糊##Tab"))
                    {
                        if (ImGui.BeginChild("Blur##SettingsRegion"))
                        {
                            _blurTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("关于##Tab"))
                    {
                        if (ImGui.BeginChild("About##SettingsRegion"))
                        {
                            _aboutTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("调试##Tab"))
                    {
                        if (ImGui.BeginChild("Debug##SettingsRegion"))
                        {
                            _debugTab.Draw();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.End();
            }

        }

        public void UpdateGameUI() => _uiGameStateTracker.UpdateGameUI();

        internal void Dispose()
        {
            if (Plugin.obsConnection.Connected)
            {
                foreach (Blur blur in _blurManager.BlurDict.Values)
                {
                    blur.Enabled = false;
                    Svc.PluginLog.Debug("Turn off {0}", blur.Name);
                    Plugin.obsConnection.OBS.RemoveSourceFilter(Config.SourceName, blur.Name);
                }
            }
            isThreadRunning = false;
            _blurManager.Dispose();
        }
    }
}