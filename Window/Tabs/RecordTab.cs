using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSPlugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OBSPlugin
{
    public class RecordTab
    {
        private static OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>>? _dutyTree;
        private static Dictionary<string, (int StartIndex, int Count)>? _bitRanges;

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

        public static void Initialize(IDataManager data)
        {
            if (_dutyTree != null) return;

            _dutyTree = ContentFinderConditionExtensions.BuildDutyTree(data);
            _bitRanges = new Dictionary<string, (int, int)>();

            foreach (var l1 in _dutyTree)
            {
                foreach (var l2 in l1.Value)
                {
                    if (l2.Value.Count > 0)
                    {
                        uint firstRowId = l2.Value[0].RowId;
                        _bitRanges[$"{l1.Key}/{l2.Key}"] = ((int)firstRowId, l2.Value.Count);
                    }
                }
            }
        }

        private Configuration.TriState GetL2State(string l1Key, string l2Key)
        {
            if (_bitRanges == null || !_bitRanges.TryGetValue($"{l1Key}/{l2Key}", out var range)) return Configuration.TriState.Unchecked;
            int count = _config.SelectedContents.CountRange(range.StartIndex, range.Count);
            if (count == 0) return Configuration.TriState.Unchecked;
            if (count == range.Count) return Configuration.TriState.Checked;
            return Configuration.TriState.Indeterminate;
        }

        private Configuration.TriState GetL1State(string l1Key)
        {
            if (_dutyTree == null || !_dutyTree.TryGetValue(l1Key, out var l2Dict)) return Configuration.TriState.Unchecked;

            bool hasChecked = false;
            bool hasUnchecked = false;

            foreach (var l2 in l2Dict)
            {
                var state = GetL2State(l1Key, l2.Key);
                if (state == Configuration.TriState.Checked) hasChecked = true;
                if (state == Configuration.TriState.Unchecked) hasUnchecked = true;
                if (hasChecked && hasUnchecked) return Configuration.TriState.Indeterminate;
            }

            if (hasChecked && !hasUnchecked) return Configuration.TriState.Checked;
            if (!hasChecked && hasUnchecked) return Configuration.TriState.Unchecked;
            return Configuration.TriState.Indeterminate;
        }

        public void Draw()
        {
            // Recording control button + status
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

            DrawRecordingFileGroup();
            DrawAutoRecordTriggerGroup();
            DrawAutoStopRecordGroup();
        }

        private void DrawRecordingFileGroup()
        {
            ImGui.TextColored(new Vector4(0.54f, 0.71f, 0.97f, 1f), "录制文件");

            // 子文件夹
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

            // 文件名
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

            if (ImGui.CollapsingHeader("录制筛选"))
            {
                ImGui.Indent();

                if (ImGui.Checkbox("显示所有筛选", ref _config.ShowAllFilters))
                    _config.Save();

                DrawContentTree();

                ImGui.Unindent();
            }
        }

        private void DrawContentTree()
        {
            if (_dutyTree == null) return;
            foreach (var l1 in _dutyTree)
            {
                bool isToggleable = l1.Key != "副本" && l1.Key != "多人内容";
                if (isToggleable && !_config.ShowAllFilters)
                    continue;

                ImGui.Indent();

                // L1 tri-state checkbox
                var l1State = GetL1State(l1.Key);
                bool l1CheckboxValue = l1State == Configuration.TriState.Checked;
                string l1CheckboxId = $"##l1_{l1.Key}";

                // For indeterminate, draw a special checkbox with a bullet
                if (l1State == Configuration.TriState.Indeterminate)
                {
                    // Draw checkbox with mixed state indicator
                    ImGui.Checkbox(l1CheckboxId, ref l1CheckboxValue);
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "[部分]");

                    // Handle click on the partial indicator to cycle through states
                    if (ImGui.IsItemClicked())
                    {
                        CascadeL1State(l1.Key, true);  // Set all to checked
                        _config.Save();
                    }
                }
                else
                {
                    if (ImGui.Checkbox(l1CheckboxId, ref l1CheckboxValue))
                    {
                        CascadeL1State(l1.Key, l1CheckboxValue);
                        _config.Save();
                    }
                    ImGui.SameLine();
                }

                if (ImGui.TreeNode(l1.Key))
                {
                    foreach (var l2 in l1.Value)
                    {
                        ImGui.Indent();

                        // L2 tri-state checkbox
                        var l2State = GetL2State(l1.Key, l2.Key);
                        bool l2CheckboxValue = l2State == Configuration.TriState.Checked;
                        string l2CheckboxId = $"##l2_{l1.Key}_{l2.Key}";

                        if (l2State == Configuration.TriState.Indeterminate)
                        {
                            ImGui.Checkbox(l2CheckboxId, ref l2CheckboxValue);
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "[部分]");

                            if (ImGui.IsItemClicked())
                            {
                                CascadeL2State(l1.Key, l2.Key, true);
                                _config.Save();
                            }
                        }
                        else
                        {
                            if (ImGui.Checkbox(l2CheckboxId, ref l2CheckboxValue))
                            {
                                CascadeL2State(l1.Key, l2.Key, l2CheckboxValue);
                                _config.Save();
                            }
                            ImGui.SameLine();
                        }

                        if (ImGui.TreeNode(l2.Key))
                        {
                            foreach (var entry in l2.Value)
                            {
                                bool selected = _config.SelectedContents.Get((int)entry.RowId);
                                if (ImGui.Checkbox(entry.Name, ref selected))
                                {
                                    _config.SelectedContents.Set((int)entry.RowId, selected);
                                    _config.Save();
                                }
                            }
                            ImGui.TreePop();
                        }
                        ImGui.Unindent();
                    }
                    ImGui.TreePop();
                }
                ImGui.Unindent();
            }
        }

        private void CascadeL1State(string l1Key, bool checked_)
        {
            if (_dutyTree == null || !_dutyTree.TryGetValue(l1Key, out var l2Dict)) return;
            foreach (var l2 in l2Dict)
            {
                CascadeL2State(l1Key, l2.Key, checked_);
            }
        }

        private void CascadeL2State(string l1Key, string l2Key, bool checked_)
        {
            if (_dutyTree == null || !_dutyTree.TryGetValue(l1Key, out var l2Dict)) return;
            if (!l2Dict.TryGetValue(l2Key, out var entries)) return;
            foreach (var entry in entries)
            {
                _config.SelectedContents.Set((int)entry.RowId, checked_);
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