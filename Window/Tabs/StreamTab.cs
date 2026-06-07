using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Numerics;

namespace OBSPlugin
{
    public class StreamTab
    {
        private readonly ObsConnection _obsConnection;

        public StreamTab(ObsConnection obsConnection)
        {
            _obsConnection = obsConnection;
        }

        public void Draw()
        {
            string obsButtonText;

            switch (_obsConnection.ObsStreamStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    obsButtonText = "直播开始中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    obsButtonText = "停止直播";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    obsButtonText = "直播停止中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    obsButtonText = "开始直播";
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
                    _obsConnection.OBS.ToggleStream();
                }
                catch (Exception e)
                {
                    Svc.PluginLog.Error("Error on toggle streaming: {0}", e);
                    Svc.Chat.PrintError("[OBSPlugin] Error on toggle streaming, check log for details.");
                }
            }

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(_obsConnection.ObsStreamStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                _obsConnection.ObsStreamStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "直播中" : "已停止");

            if (_obsConnection.StreamStats != null && _obsConnection.StreamStats.IsActive)
            {
                ImGui.Text($"直播中：{_obsConnection.StreamStats.IsActive}");
                ImGui.Text($"重新连接中：{_obsConnection.StreamStats.IsReconnecting}");
                ImGui.Text($"直播时间：{_obsConnection.StreamStats.TimeCode}");
                ImGui.Text($"拥塞：{_obsConnection.StreamStats.Congestion}");
                ImGui.Text($"总帧数：{_obsConnection.StreamStats.TotalFrames}");
                ImGui.Text($"丢帧：{_obsConnection.StreamStats.SkippedFrames}");
                ImGui.Text($"已发送字节：{_obsConnection.StreamStats.BytesSent}");
            }
        }
    }
}