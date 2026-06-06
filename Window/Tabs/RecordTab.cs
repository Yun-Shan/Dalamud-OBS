using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Numerics;

namespace OBSPlugin
{
    public class RecordTab
    {
        private readonly Configuration _config;
        private readonly ObsConnection _obsConnection;
        private readonly IPluginLog _log;
        private readonly IChatGui _chat;
        private readonly Action _setRecordingDir;

        public RecordTab(Configuration config, ObsConnection obsConnection, IPluginLog log, IChatGui chat, Action setRecordingDir)
        {
            _config = config;
            _obsConnection = obsConnection;
            _log = log;
            _chat = chat;
            _setRecordingDir = setRecordingDir;
        }

        public void Draw()
        {
            string obsButtonText;

            switch (_obsConnection.ObsRecordStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    obsButtonText = "录制开始中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    obsButtonText = "停止录制";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    obsButtonText = "录制停止中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    obsButtonText = "开始录制";
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
                    if (_obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
                    {
                        _setRecordingDir();
                    }
                    _obsConnection.OBS.ToggleRecord();
                }
                catch (Exception e)
                {
                    _log.Error("Error on toggle recording: {0}", e);
                    _chat.PrintError("[OBSPlugin] Error on toggle recording, check log for details.");
                }
            }

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(_obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "录制中" : "已停止");

            if (ImGui.InputText("录制目录", ref _config.RecordDir, 256, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                _config.Save();
                if (_obsConnection.Connected)
                {
                    _obsConnection.OBS.SetRecordDirectory(_config.RecordDir);
                    _log.Information("Recording directory set to {0}", _config.RecordDir);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("按回车保存");
            if (_config.UseDutyName)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Checkbox("区域作为子文件夹", ref _config.IncludeTerritory))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，录制将保存到以当前区域名命名的子文件夹。");
            if (_config.UseDutyName)
            {
                ImGui.EndDisabled();
            }
            ImGui.SameLine(ImGui.GetColumnWidth() - 400);
            if (ImGui.Checkbox("副本名称作为子文件夹", ref _config.UseDutyName))
            {
                if (_config.UseDutyName)
                {
                    _config.IncludeTerritory = true;
                }
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果在副本中，使用副本名称代替区域名作为子文件夹。");

            if (ImGui.Checkbox("区域作为后缀", ref _config.ZoneAsSuffix))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，将以当前区域名作为录制文件的后缀。");

            if (ImGui.Checkbox("战斗开始时自动录制", ref _config.StartRecordOnCombat))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，战斗开始时将自动开始录制。");

            if (ImGui.Checkbox("倒计时开始时自动录制", ref _config.StartRecordOnCountDown))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，倒计时开始时将自动开始录制。");

            ImGui.SameLine(ImGui.GetColumnWidth() - 350);
            if (ImGui.Checkbox("倒计时取消时停止录制", ref _config.StopRecordOnCountDownCancel))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，倒计时取消时将自动停止录制。");

            if (ImGui.Checkbox("战斗结束后停止录制", ref _config.StopRecordOnCombat))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，战斗结束后将自动停止录制。");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragInt("", ref _config.StopRecordOnCombatDelay, 1, 0, 300, "%d 秒"))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("战斗结束后停止录制的延迟时间（秒）。");

            if (ImGui.Checkbox("离开区域时停止录制", ref _config.StopRecordOnZoneExit))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，离开区域时将自动停止录制。");

            if (_config.StopRecordOnCombat && ImGui.Checkbox("过场时不要停止录制", ref _config.DontStopInCutscene))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，观看过场动画时不会停止录制。");
            if (_config.StopRecordOnCombat && ImGui.Checkbox("战斗恢复时取消停止录制", ref _config.CancelStopRecordOnResume))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，在停止倒计时前有新的战斗则不停止录制。");
        }
    }
}