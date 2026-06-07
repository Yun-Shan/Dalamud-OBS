using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using System;
using System.Numerics;

namespace OBSPlugin
{
    public class ConnectionTab
    {
        private readonly Configuration _config;
        private readonly ObsConnection _obsConnection;
        private readonly IPluginLog _log;
        private readonly IChatGui _chat;

        public ConnectionTab(Configuration config, ObsConnection obsConnection, IPluginLog log, IChatGui chat)
        {
            _config = config;
            _obsConnection = obsConnection;
            _log = log;
            _chat = chat;
        }

        public void Draw()
        {
            if (ImGui.Checkbox("启用", ref _config.Enabled))
            {
                _config.Save();
            }
            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(_obsConnection.Connected ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                _obsConnection.Connected ? "已连接" : "未连接");
            var address = _config.Address;
            if (ImGui.InputText("服务器地址", ref address, 128, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (int.TryParse(address, out int port))
                {
                    address = $"127.0.0.1:{port}";
                }
                if(!(address.StartsWith("ws://") || address.StartsWith("wss://")))
                {
                    address = "ws://" + address;
                }
                _config.Address = address;
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("按回车确认");

            if (ImGui.InputText("密码", ref _config.Password, 128, ImGuiInputTextFlags.Password))
            {
                _config.Save();
            }
            string connectionButtonText = _obsConnection.Connected ? "断开连接" : "连接";
            if (ImGui.Button(connectionButtonText))
            {
                if (_obsConnection.Connected)
                {
                    _obsConnection.OBS.Disconnect();
                }
                else
                {
                    _obsConnection.TryConnect(_config.Address, _config.Password);
                }
            }
            if (_obsConnection.ConnectionFailed)
            {
                ImGui.SameLine();
                ImGui.Text("认证失败，请检查地址和密码！");
            }
            if (_obsConnection.Connected)
            {
                ImGui.Separator();
                ImGui.Text("OBS 插件版本：" + (_obsConnection.VersionInfo?.PluginVersion ?? "未知"));
                ImGui.Text("OBS 版本：" + (_obsConnection.VersionInfo?.OBSStudioVersion ?? "未知"));
            }
        }
    }
}