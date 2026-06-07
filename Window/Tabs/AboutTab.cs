using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using System;
using System.Diagnostics;

namespace OBSPlugin
{
    public class AboutTab
    {
        public AboutTab()
        {
        }

        public void Draw()
        {
            ImGui.Text("你需要在 OBS 中安装模糊插件才能使模糊滤镜正常工作。");

            ImGui.Separator();

            ImGui.Text("对于 OBS v30+：");
            ImGui.BulletText("");
            ImGui.SameLine();
            if (ImGui.Button("OBS Composite Blur"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "https://github.com/FiniteSingularity/obs-composite-blur/releases/tag/v1.0.5",
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Svc.PluginLog.Error(ex, "Could not open OBS Composite Blur url");
                }
            }
            ImGui.SameLine();
            ImGui.Text("下载并安装即可。");

            ImGui.BulletText("");
            ImGui.SameLine();
            ImGui.Text("OBS-websocket 5.3.0");
            ImGui.SameLine();
            ImGui.TextWrapped("这是 OBS v30 内置插件，但你仍需要在 OBS 中设置密码并启用它（工具 -> OBS Websocket Server Settings），" +
                "然后在连接标签页中提供端口和密码。");

            ImGui.NewLine();
            ImGui.Text("如果遇到任何 bug，请在以下位置提交问题：");
            ImGui.SameLine();
            if (ImGui.Button("Github"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "https://github.com/Yun-Shan/Dalamud-OBS",
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Svc.PluginLog.Error(ex, "Could not open OBS-websocket url");
                }
            }
        }
    }
}