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
        private readonly OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>> _dutyTree;

        private readonly Configuration _config;
        private readonly ObsConnection _obsConnection;
        private readonly Action _setRecordingDir;

        public RecordTab(Configuration config, ObsConnection obsConnection, Action setRecordingDir)
        {
            _config = config;
            _obsConnection = obsConnection;
            _setRecordingDir = setRecordingDir;

            _dutyTree = ContentFinderConditionExtensions.BuildDutyTree(Svc.DataManager);
        }

        private CheckboxStatus GetL2State(string l1Key, string l2Key)
        {
            if (!_dutyTree.TryGetValue(l1Key, out var l2Dict)) return CheckboxStatus.Unchecked;
            if (!l2Dict.TryGetValue(l2Key, out var entries)) return CheckboxStatus.Unchecked;

            bool hasChecked = false;
            bool hasUnchecked = false;

            foreach (var entry in entries)
            {
                if (_config.SelectedContents.Contains(entry.RowId))
                    hasChecked = true;
                else
                    hasUnchecked = true;

                if (hasChecked && hasUnchecked) return CheckboxStatus.Indeterminate;
            }

            return hasChecked ? CheckboxStatus.Checked : CheckboxStatus.Unchecked;
        }

        private CheckboxStatus GetL1State(string l1Key)
        {
            if (!_dutyTree.TryGetValue(l1Key, out var l2Dict)) return CheckboxStatus.Unchecked;

            bool hasChecked = false;
            bool hasUnchecked = false;

            foreach (var l2 in l2Dict)
            {
                var state = GetL2State(l1Key, l2.Key);
                if (state == CheckboxStatus.Checked) hasChecked = true;
                if (state == CheckboxStatus.Unchecked) hasUnchecked = true;
                if (hasChecked && hasUnchecked) return CheckboxStatus.Indeterminate;
            }

            if (hasChecked && !hasUnchecked) return CheckboxStatus.Checked;
            if (!hasChecked && hasUnchecked) return CheckboxStatus.Unchecked;
            return CheckboxStatus.Indeterminate;
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

            var enableFilter = true;
            if (ImGui.Checkbox("启用筛选", ref enableFilter))
                _config.Save();
            if (enableFilter && ImGui.CollapsingHeader("录制筛选"))
            {
                if (ImGui.Checkbox("显示所有筛选", ref _config.ShowAllFilters))
                    _config.Save();

                DrawContentTree();
            }
        }

        private void DrawContentTree()
        {
            foreach (var l1 in _dutyTree)
            {
                bool isToggleable = l1.Key != "大型任务" && l1.Key != "绝境战";
                if (isToggleable && !_config.ShowAllFilters)
                    continue;

                var l1State = GetL1State(l1.Key);
                string l1CheckboxId = $"##l1_{l1.Key}";
                if (UiHelper.Checkbox(l1CheckboxId, ref l1State))
                {
                    CascadeL1State(l1.Key, l1State != CheckboxStatus.Checked);
                    _config.Save();
                }
                ImGui.SameLine();

                if (ImGui.TreeNode(l1.Key))
                {
                    foreach (var l2 in l1.Value)
                    {
                        var l2State = GetL2State(l1.Key, l2.Key);
                        string l2CheckboxId = $"##l2_{l1.Key}_{l2.Key}";
                        if (UiHelper.Checkbox(l2CheckboxId, ref l2State))
                        {
                            CascadeL2State(l1.Key, l2.Key, l2State != CheckboxStatus.Checked);
                            _config.Save();
                        }
                        ImGui.SameLine();

                        if (ImGui.TreeNode(l2.Key))
                        {
                            foreach (var entry in l2.Value)
                            {
                                bool selected = _config.SelectedContents.Contains(entry.RowId);
                                if (ImGui.Checkbox(entry.Name, ref selected))
                                {
                                    if (selected)
                                        _config.SelectedContents.Add(entry.RowId);
                                    else
                                        _config.SelectedContents.Remove(entry.RowId);
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

        private void CascadeL1State(string l1Key, bool check)
        {
            if (!_dutyTree.TryGetValue(l1Key, out var l2Dict)) return;
            foreach (var l2 in l2Dict)
            {
                CascadeL2State(l1Key, l2.Key, check);
            }
        }

        private void CascadeL2State(string l1Key, string l2Key, bool check)
        {
            if (!_dutyTree.TryGetValue(l1Key, out var l2Dict)) return;
            if (!l2Dict.TryGetValue(l2Key, out var entries)) return;

            if (check)
            {
                foreach (var entry in entries)
                    _config.SelectedContents.Add(entry.RowId);
            }
            else
            {
                foreach (var entry in entries)
                    _config.SelectedContents.Remove(entry.RowId);
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