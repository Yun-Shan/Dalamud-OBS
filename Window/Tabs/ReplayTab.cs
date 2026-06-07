using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Numerics;

namespace OBSPlugin
{
    public class ReplayTab
    {
        private readonly Configuration _config;
        private readonly ObsConnection _obsConnection;
        private readonly IChatGui _chat;

        public ReplayTab(Configuration config, ObsConnection obsConnection, IChatGui chat)
        {
            _config = config;
            _obsConnection = obsConnection;
            _chat = chat;
        }

        public void Draw()
        {
            string obsButtonText;

            switch (_obsConnection.ObsReplayBufferStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    obsButtonText = "回放缓存启动中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    obsButtonText = "停止回放缓存";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    obsButtonText = "回放缓存停止中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    obsButtonText = "启动回放缓存";
                    break;

                default:
                    obsButtonText = "状态未知";
                    break;
            }

            if (ImGui.Button(obsButtonText))
            {
                if (!_obsConnection.Connected) return;
                try
                {
                    _obsConnection.OBS.ToggleReplayBuffer();
                }
                catch (Exception e)
                {
                    Svc.PluginLog.Error("Error on toggle replay buffer: {0}", e);
                    _chat.PrintError("[OBSPlugin] Error on toggle replay buffer, check log for details.");
                }
            }
            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(_obsConnection.ObsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                _obsConnection.ObsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "回放中" : "已停止");

            if (ImGui.Checkbox("自动录制时启动回放缓存", ref _config.StartReplayBufferOnRecord))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，自动录制开始时将自动启动回放缓存。");

            if (ImGui.Checkbox("区域作为子文件夹", ref _config.ResetReplayBufferDirByTerritory))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，将自动为不同区域设置回放缓存目录。\n" +
                    "这会使回放缓存在切换区域时自动停止和重启。\n" +
                    "否则所有回放缓存将保存到它启动时的文件夹。");

            if (ImGui.Checkbox("战斗结束后保存回放缓存", ref _config.SaveReplayBufferOnCombat))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，战斗结束后将自动保存回放缓存。");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragInt("", ref _config.SaveReplayBufferOnCombatDelay, 1, 0, 300, "%d 秒"))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("战斗结束后保存回放缓存的延迟时间（秒）。");

            if (_obsConnection.ObsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("保存回放"))
            {
                if (!_obsConnection.Connected) return;
                try
                {
                    _obsConnection.OBS.SaveReplayBuffer();
                }
                catch (Exception e)
                {
                    Svc.PluginLog.Error("Error on save replay buffer: {0}", e);
                    _chat.PrintError("[OBSPlugin] Error on save replay buffer, check log for details.");
                }
            }
            if (_obsConnection.ObsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                ImGui.EndDisabled();
            }
        }
    }
}