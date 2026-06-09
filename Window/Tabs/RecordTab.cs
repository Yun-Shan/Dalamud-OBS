using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using OBSPlugin.Services;
using OBSPlugin.Window;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OBSPlugin
{
    public class RecordTab
    {
        private readonly DutyTreeNode _dutyTreeRoot;
        private readonly Action _setRecordingDir;

        private readonly Configuration _config;
        private readonly ObsConnection _obsConnection;

        public RecordTab(Configuration config, ObsConnection obsConnection, Action setRecordingDir)
        {
            _config = config;
            _obsConnection = obsConnection;
            _setRecordingDir = setRecordingDir;

            _dutyTreeRoot = ContentFinderConditionExtensions.BuildDutyTree(Svc.DataManager);
            SyncCheckStatusFromConfig();
        }

        private void SyncCheckStatusFromConfig()
        {
            foreach (var contentTypeNode in _dutyTreeRoot.Children)
            {
                foreach (var uiCategoryNode in contentTypeNode.Children)
                {
                    foreach (var entryNode in uiCategoryNode.Children)
                    {
                        entryNode.UpdateCheckStatus(_config.FilterDuty.Contains(entryNode.RowId));
                    }
                }
            }
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
                if (_obsConnection.ObsRecordStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED) return;
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
                    Svc.PluginLog.Error("Error on toggle recording: {0}", e);
                    Svc.Chat.PrintError("[OBSPlugin] Error on toggle recording, check log for details.");
                }
            }

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(_obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "录制中" : "已停止");

            DrawRecordingFileGroup();
            DrawAutoRecordTriggerGroup();
            DrawAutoStopRecordGroup();
        }

        private void DrawRecordingFileGroup()
        {
            ImGui.TextColored(new Vector4(0.54f, 0.71f, 0.97f, 1f), "录制文件");

            ImGui.Text("储存目录");
            ImGui.SameLine(100);
            ImGui.InputText("##RecordDir", ref _config.RecordDir);

            ImGui.Text("子文件夹");
            ImGui.SameLine(100);
            if (ImGui.BeginCombo("##SubFolderMode", _config.SubFolderMode.ToString()))
            {
                foreach (Configuration.SubFolderModeType mode in Enum.GetValues<Configuration.SubFolderModeType>())
                {
                    if (ImGui.Selectable(mode.ToString(), _config.SubFolderMode == mode))
                    {
                        _config.SubFolderMode = mode;
                        _config.Save();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Text("文件名");
            ImGui.SameLine(100);
            if (ImGui.BeginCombo("##FileNameMode", _config.FileNameMode.ToString()))
            {
                foreach (Configuration.FileNameModeType mode in Enum.GetValues<Configuration.FileNameModeType>())
                {
                    if (ImGui.Selectable(mode.ToString(), _config.FileNameMode == mode))
                    {
                        _config.FileNameMode = mode;
                        _config.Save();
                    }
                }
                ImGui.EndCombo();
            }
        }

        private void DrawAutoRecordTriggerGroup()
        {
            ImGui.TextColored(new Vector4(0.51f, 0.78f, 0.52f, 1f), "自动录制触发");

            if (ImGui.Checkbox("战斗开始时自动录制", ref _config.StartRecordOnCombat))
                _config.Save();

            if (ImGui.Checkbox("倒计时开始时自动录制", ref _config.StartRecordOnCountDown))
                _config.Save();

            if (ImGui.Checkbox("倒计时取消时停止录制", ref _config.StopRecordOnCountDownCancel))
                _config.Save();

            if (ImGui.Checkbox("启用筛选", ref _config.UseDutyFilter))
                _config.Save();
            if (_config.UseDutyFilter && ImGui.CollapsingHeader("录制筛选"))
            {
                if (ImGui.Checkbox("显示所有筛选", ref _config.ShowAllFilters))
                    _config.Save();

                DrawContentTree();
            }
        }

        private void DrawContentTree()
        {
            foreach (var contentTypeNode in _dutyTreeRoot.Children)
            {
                bool isToggleable = contentTypeNode.Name != "大型任务" && contentTypeNode.Name != "绝境战";
                if (isToggleable && !_config.ShowAllFilters)
                    continue;

                string checkboxId = $"##l1_{contentTypeNode.Name}";
                CheckboxStatus contentTypeStatus = contentTypeNode.CheckStatus;
                if (UiHelper.Checkbox(checkboxId, ref contentTypeStatus))
                {
                    contentTypeNode.UpdateCheckStatus(contentTypeStatus != CheckboxStatus.Unchecked);
                    SyncConfigFromTree();
                    _config.Save();
                }
                ImGui.SameLine();

                if (ImGui.TreeNode(contentTypeNode.Name))
                {
                    foreach (var uiCategoryNode in contentTypeNode.Children)
                    {
                        checkboxId = $"##l2_{uiCategoryNode.Name}";
                        CheckboxStatus uiCategoryStatus = uiCategoryNode.CheckStatus;
                        if (UiHelper.Checkbox(checkboxId, ref uiCategoryStatus))
                        {
                            uiCategoryNode.UpdateCheckStatus(uiCategoryStatus != CheckboxStatus.Unchecked);
                            SyncConfigFromTree();
                            _config.Save();
                        }
                        ImGui.SameLine();

                        if (ImGui.TreeNode(uiCategoryNode.Name))
                        {
                            foreach (var entryNode in uiCategoryNode.Children)
                            {
                                bool selected = entryNode.CheckStatus == CheckboxStatus.Checked;
                                if (ImGui.Checkbox(entryNode.Name, ref selected))
                                {
                                    entryNode.UpdateCheckStatus(selected);
                                    SyncConfigFromTree();
                                    _config.Save();
                                }
                            }
                            ImGui.TreePop();
                        }
                    }
                    ImGui.TreePop();
                }
            }
        }

        private void SyncConfigFromTree()
        {
            _config.FilterDuty.Clear();
            foreach (var contentTypeNode in _dutyTreeRoot.Children)
            {
                foreach (var uiCategoryNode in contentTypeNode.Children)
                {
                    foreach (var entryNode in uiCategoryNode.Children)
                    {
                        if (entryNode.CheckStatus == CheckboxStatus.Checked)
                            _config.FilterDuty.Add(entryNode.RowId);
                    }
                }
            }
        }

        private void DrawAutoStopRecordGroup()
        {
            ImGui.TextColored(new Vector4(1f, 0.72f, 0.3f, 1f), "自动停止录制");

            if (ImGui.Checkbox("战斗结束后停止录制", ref _config.StopRecordOnCombat))
                _config.Save();

            if (_config.StopRecordOnCombat)
            {
                ImGui.Indent();
                ImGui.SetNextItemWidth(100);
                if (ImGui.DragInt("延迟秒数", ref _config.StopRecordOnCombatDelay, 1, 0, 300))
                    _config.Save();

                ImGui.Checkbox("过场时不要停止录制", ref _config.DontStopInCutscene);
                ImGui.Checkbox("战斗恢复时取消停止录制", ref _config.CancelStopRecordOnResume);
                ImGui.Unindent();
            }

            if (ImGui.Checkbox("离开区域时停止录制", ref _config.StopRecordOnZoneExit))
                _config.Save();
        }
    }
}