# SelectedContents HashSet Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `SelectedContents` 从 `BitSet` 迁移到 `HashSet<uint>`，简化存储逻辑并提升可读性。

**Architecture:** 核心变更：字段类型从 `BitSet` 改为 `HashSet<uint>`，状态计算逻辑从位运算改为遍历集合。级联逻辑保持，但实现方式调整为 HashSet 操作。

**Tech Stack:** C#, Dalamud API, Newtonsoft.Json

---

## File Structure

```
Window/UiHelper.cs          # 添加 CheckboxStatus enum，修改 Checkbox() 签名
Configuration.cs            # 字段改 HashSet<uint>，移除 TriState，迁移逻辑
RecordTab.cs                # 重写状态计算和级联逻辑
Services/BitSet.cs          # 删除
```

---

## Task 1: Update UiHelper with CheckboxStatus

**Files:**
- Modify: `Window/UiHelper.cs:1-38`

- [ ] **Step 1: 添加 CheckboxStatus enum 并修改 Checkbox 方法**

```csharp
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace OBSPlugin.Window
{
    internal enum CheckboxStatus
    {
        Unchecked = 0,
        Checked = 1,
        Indeterminate = -1
    }

    internal class UiHelper
    {
        private static readonly uint checkBoxIntermediateColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.7f, 1f));

        public static bool Checkbox(ImU8String label, ref CheckboxStatus status)
        {
            const int boxGap = 2;
            bool ret;

            if (status == CheckboxStatus.Indeterminate)
            {
                bool b = false;
                ret = ImGui.Checkbox(label, ref b);
                if (ret) status = CheckboxStatus.Checked;

                var itemMin = ImGui.GetItemRectMin();
                itemMin.X += boxGap;
                itemMin.Y += boxGap;
                var itemMax = ImGui.GetItemRectMax();
                itemMax.X -= boxGap;
                itemMax.Y -= boxGap;
                ImGui.GetWindowDrawList().AddRectFilled(itemMin, itemMax, checkBoxIntermediateColor, 2);
            }
            else
            {
                bool b = status == CheckboxStatus.Checked;
                ret = ImGui.Checkbox(label, ref b);
                if (ret) status = b ? CheckboxStatus.Checked : CheckboxStatus.Unchecked;
            }

            return ret;
        }
    }
}
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build OBSPlugin.csproj -c Debug -p:Platform=x64`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add Window/UiHelper.cs
git commit -m "refactor: add CheckboxStatus enum, update Checkbox signature"
```

---

## Task 2: Update Configuration.cs

**Files:**
- Modify: `Configuration.cs:1-98`

- [ ] **Step 1: 修改 Configuration.cs**

移除 `BitSet` 字段和 `TriState` enum，改为 `HashSet<uint>`：

```csharp
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace OBSPlugin
{
    public class Configuration : IPluginConfiguration
    {
        #region Connection Settings
        public bool Enabled = true;
        public bool UIDetection = true;
        public string SourceName = "FFXIV";
        public string Address = "ws://127.0.0.1:4455/";
        public string Password = "";
        #endregion

        #region Blur Settings
        public bool EnableBlur = false;
        public int BlurSize = 3;
        public bool BlurAsync = true;
        public bool DrawBlurRect = false;
        public bool ChatLogBlur = true;
        public bool PartyListBlur = true;
        public bool TargetBlur = false;
        public bool TargetTargetBlur = true;
        public bool FocusTargetBlur = false;
        public bool NamePlateBlur = false;
        public bool CharacterBlur = false;
        public bool FriendListBlur = false;
        public bool HotbarBlur = false;
        public bool CastBarBlur = false;
        public int MaxNamePlateCount = 1;
        public int[] BlurredHotbars = Array.Empty<int>();
        #endregion

        #region Record Settings
        public string RecordDir = "";
        public bool IncludeTerritory = true;
        public bool ZoneAsSuffix = false;
        public bool StartRecordOnCountDown = false;
        public bool StopRecordOnCountDownCancel = true;
        public bool StopRecordOnZoneExit = false;
        public bool UseDutyName = false;
        public bool StartReplayBufferOnRecord = false;
        public bool StartRecordOnCombat = false;
        public bool StopRecordOnCombat = false;
        public bool CancelStopRecordOnResume = true;
        public int StopRecordOnCombatDelay = 5;
        public bool DontStopInCutscene = true;
        public SubFolderModeType SubFolderMode = SubFolderModeType.Territory;
        public FileNameModeType FileNameMode = FileNameModeType.TerritorySuffix;
        public HashSet<uint> SelectedContents = new();
        public bool ShowAllFilters = false;
        #endregion

        #region Debug Settings
        public bool EnableDebug = false;
        public bool ResetReplayBufferDirByTerritory = false;
        public bool SaveReplayBufferOnCombat = false;
        public int SaveReplayBufferOnCombatDelay = 0;
        #endregion

        #region Version
        public int Version { get; set; }
        #endregion

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }

        public enum SubFolderModeType
        {
            None = 0,
            ContentName = 1,
            ContentType = 2,
            Territory = 3,
        }

        public enum FileNameModeType
        {
            None = 0,
            ContentNameSuffix = 1,
            ContentNamePrefix = 2,
            TerritorySuffix = 3,
            TerritoryPrefix = 4,
        }
    }
}
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build OBSPlugin.csproj -c Debug -p:Platform=x64`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add Configuration.cs
git commit -m "refactor: change SelectedContents to HashSet<uint>, remove TriState"
```

---

## Task 3: Rewrite RecordTab.cs

**Files:**
- Modify: `RecordTab.cs:1-283`

- [ ] **Step 1: 完全重写 RecordTab.cs**

```csharp
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

                // L1 tri-state checkbox
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
                        // L2 tri-state checkbox
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
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build OBSPlugin.csproj -c Debug -p:Platform=x64`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add Window/Tabs/RecordTab.cs
git commit -m "refactor: rewrite SelectedContents logic with HashSet<uint>"
```

---

## Task 4: Delete BitSet.cs

**Files:**
- Delete: `Services/BitSet.cs`

- [ ] **Step 1: 删除 BitSet.cs**

```bash
git rm Services/BitSet.cs
```

- [ ] **Step 2: 验证构建**

Run: `dotnet build OBSPlugin.csproj -c Debug -p:Platform=x64`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git commit -m "refactor: remove BitSet, use HashSet<uint> for SelectedContents"
```

---

## Task 5: Update all Checkbox usages to use CheckboxStatus

**Files:**
- Modify: `Window/Tabs/BlurTab.cs`
- Modify: `Window/Tabs/ConnectionTab.cs`
- Modify: `Window/Tabs/ReplayTab.cs`

- [ ] **Step 1: 检查并更新其他 Tab 文件中的 Checkbox 调用**

搜索所有 `UiHelper.Checkbox` 调用，将 `ref int` 改为 `ref CheckboxStatus`。

- [ ] **Step 2: 验证构建**

Run: `dotnet build OBSPlugin.csproj -c Debug -p:Platform=x64`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: 提交**

```bash
git add Window/Tabs/BlurTab.cs Window/Tabs/ConnectionTab.cs Window/Tabs/ReplayTab.cs
git commit -m "refactor: update Checkbox calls to use CheckboxStatus"
```

---

## Verification

1. 构建成功
2. 启动游戏测试配置保存/加载
3. 验证 L1/L2/L3 级联勾选正常工作
4. 验证旧配置迁移（如有）

---

## Execution Options

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?