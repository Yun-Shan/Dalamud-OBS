# 录制页面优化实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化录制设置页面，按功能分为四个分组，新增录制筛选树形图

**Architecture:** 使用 BitSet 存储 L3 选中状态，L1/L2 状态由 BitSet 推导计算。DutyTree 在插件启动时构建一次并缓存。

**Tech Stack:** C#, Dalamud ImGui, JSON serialization with base64

---

## 文件结构

| 文件 | 职责 |
|------|------|
| `Services/BitSet.cs` | 通用位数组类，支持自动扩容和 base64 序列化 |
| `Services/ContentFinderConditionExtensions.cs` | 抽取 DutyTree 构建逻辑 |
| `Configuration.cs` | 新增枚举、BitSet 字段、TriState |
| `Window/Tabs/RecordTab.cs` | 重构 UI，四个分组 + 筛选树 |
| `Window/Tabs/DebugTab.cs` | 调用公共方法获取 DutyTree |

---

## Task 1: BitSet 类

**Files:**
- Create: `Services/BitSet.cs`

- [ ] **Step 1: 创建 BitSet.cs 文件**

```csharp
using System;
using System.Linq;
using Newtonsoft.Json;

namespace OBSPlugin.Services
{
    public class BitSet
    {
        private long[] _bits;
        private int _size;

        public BitSet(int capacity = 0)
        {
            _size = 0;
            _bits = capacity > 0 ? new long[(capacity + 63) / 64] : Array.Empty<long>();
        }

        public int Size => _size;

        public void Set(int index, bool value)
        {
            if (index >= _size)
            {
                // 自动扩容
                int newSize = index + 1;
                int newArraySize = (newSize + 63) / 64;
                Array.Resize(ref _bits, newArraySize);
                _size = newSize;
            }

            int arrayIndex = index / 64;
            int bitIndex = index % 64;

            if (value)
                _bits[arrayIndex] |= (1L << bitIndex);
            else
                _bits[arrayIndex] &= ~(1L << bitIndex);
        }

        public bool Get(int index)
        {
            if (index >= _size) return false;
            int arrayIndex = index / 64;
            int bitIndex = index % 64;
            return (_bits[arrayIndex] & (1L << bitIndex)) != 0;
        }

        public void SetRange(int startIndex, int count, bool value)
        {
            for (int i = 0; i < count; i++)
            {
                Set(startIndex + i, value);
            }
        }

        public int CountRange(int startIndex, int count)
        {
            int count_ = 0;
            for (int i = 0; i < count; i++)
            {
                if (Get(startIndex + i)) count_++;
            }
            return count_;
        }

        public long[] ToArray() => _bits;

        public void LoadFrom(long[] array)
        {
            _bits = array ?? Array.Empty<long>();
            _size = _bits.Length * 64;
        }
    }

    public class BitSetConverter : JsonConverter<BitSet>
    {
        public override BitSet ReadJson(JsonReader reader, Type objectType, BitSet existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            string base64 = reader.Value as string;
            if (string.IsNullOrEmpty(base64))
            {
                return new BitSet(0);
            }

            byte[] bytes = Convert.FromBase64String(base64);
            int longCount = bytes.Length / 8;
            long[] longs = new long[longCount];
            Buffer.BlockCopy(bytes, 0, longs, 0, bytes.Length);

            var bitSet = new BitSet(0);
            bitSet.LoadFrom(longs);
            return bitSet;
        }

        public override void WriteJson(JsonWriter writer, BitSet value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            long[] bits = value.ToArray();
            int byteCount = bits.Length * 8;
            byte[] bytes = new byte[byteCount];
            Buffer.BlockCopy(bits, 0, bytes, 0, byteCount);

            string base64 = Convert.ToBase64String(bytes);
            writer.WriteValue(base64);
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Services/BitSet.cs
git commit -m "feat: add BitSet class with base64 serialization"
```

---

## Task 2: 抽取 DutyTree 构建逻辑

**Files:**
- Create: `Services/ContentFinderConditionExtensions.cs`
- Modify: `Window/Tabs/DebugTab.cs`

- [ ] **Step 1: 创建 ContentFinderConditionExtensions.cs**

```csharp
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

namespace OBSPlugin.Services
{
    public struct ContentEntry
    {
        public string Name;
        public uint RowId;
    }

    public static class ContentFinderConditionExtensions
    {
        public static OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>> BuildDutyTree(IDataManager data)
        {
            var tree = new OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>>();

            var sheet = data.GetExcelSheet<ContentFinderCondition>();
            if (sheet == null) return tree;

            var sortedRows = sheet
                .OrderBy(row => row.ContentType.Value.RowId)
                .ThenBy(row => row.ContentUICategory.Value.RowId)
                .ThenBy(row => row.RowId);

            foreach (var row in sortedRows)
            {
                var contentType = string.IsNullOrEmpty(row.ContentType.Value.Name.ToString()) ? "未知" : row.ContentType.Value.Name.ToString();
                var uiCategory = string.IsNullOrEmpty(row.ContentUICategory.Value.Name.ToString()) ? "未知" : row.ContentUICategory.Value.Name.ToString();
                var name = string.IsNullOrEmpty(row.Name.ToString()) ? "未知" : row.Name.ToString();

                if (!tree.ContainsKey(contentType))
                    tree[contentType] = new OrderedDictionary<string, List<ContentEntry>>();

                var uiCategoryDict = tree[contentType];

                if (!uiCategoryDict.ContainsKey(uiCategory))
                    uiCategoryDict[uiCategory] = new List<ContentEntry>();

                var nameList = uiCategoryDict[uiCategory];
                nameList.Add(new ContentEntry { Name = name, RowId = row.RowId });
            }

            return tree;
        }
    }
}
```

- [ ] **Step 2: 修改 DebugTab.cs，使用公共方法**

修改 `DebugTab.cs` 中的 `CacheDutyTree()` 方法，改为调用公共方法：

```csharp
private void CacheDutyTree()
{
    if (_debugDutyTreeCached) return;
    _debugDutyTree = ContentFinderConditionExtensions.BuildDutyTree(_data);
    _debugDutyTreeCached = true;
}
```

- [ ] **Step 3: 提交**

```bash
git add Services/ContentFinderConditionExtensions.cs Window/Tabs/DebugTab.cs
git commit -m "refactor: extract DutyTree building to shared method"
```

---

## Task 3: 更新 Configuration.cs

**Files:**
- Modify: `Configuration.cs`

- [ ] **Step 1: 添加枚举和字段**

在 `Configuration.cs` 中添加：

```csharp
public enum SubFolderMode
{
    None = 0,
    ContentName = 1,
    ContentType = 2,
    Territory = 3,
}

public enum FileNameMode
{
    None = 0,
    ContentNameSuffix = 1,
    ContentNamePrefix = 2,
    TerritorySuffix = 3,
    TerritoryPrefix = 4,
}

public enum TriState
{
    Unchecked = 0,
    Checked = 1,
    Indeterminate = 2,
}
```

在 `Record Settings` 区域添加：

```csharp
public SubFolderMode SubFolderMode = SubFolderMode.Territory;
public FileNameMode FileNameMode = FileNameMode.TerritorySuffix;
public BitSet SelectedContents;
public bool ShowAllFilters = false;
```

- [ ] **Step 2: 添加 JsonConverter 属性**

在 `Configuration` 类上添加：

```csharp
[JsonConverter(typeof(BitSetConverter))]
public BitSet SelectedContents;
```

- [ ] **Step 3: 提交**

```bash
git add Configuration.cs
git commit -m "feat: add enums and BitSet field for content filter"
```

---

## Task 4: 重构 RecordTab

**Files:**
- Modify: `Window/Tabs/RecordTab.cs`

- [ ] **Step 1: 重写 RecordTab.cs**

完整重写 `RecordTab.cs`，包含：
- 四个分组：录制文件、自动录制触发、自动停止录制、手动录制控制
- 录制筛选折叠树，带级联选择
- TriState 状态计算逻辑

```csharp
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSPlugin.Services;
using System;
using System.Collections.Generic;
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

        private static OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>> _dutyTree;
        private static Dictionary<string, (int StartIndex, int Count)> _bitRanges;
        private static int _totalContentCount;

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

            int index = 0;
            foreach (var l1 in _dutyTree)
            {
                foreach (var l2 in l1.Value)
                {
                    _bitRanges[$"{l1.Key}/{l2.Key}"] = (index, l2.Value.Count);
                    index += l2.Value.Count;
                }
            }
            _totalContentCount = index;
        }

        private TriState GetL2State(string l1Key, string l2Key)
        {
            if (!_bitRanges.TryGetValue($"{l1Key}/{l2Key}", out var range)) return TriState.Unchecked;
            int count = _config.SelectedContents.CountRange(range.StartIndex, range.Count);
            if (count == 0) return TriState.Unchecked;
            if (count == range.Count) return TriState.Checked;
            return TriState.Indeterminate;
        }

        private TriState GetL1State(string l1Key)
        {
            if (!_dutyTree.TryGetValue(l1Key, out var l2Dict)) return TriState.Unchecked;

            bool hasChecked = false;
            bool hasUnchecked = false;

            foreach (var l2 in l2Dict)
            {
                var state = GetL2State(l1Key, l2.Key);
                if (state == TriState.Checked) hasChecked = true;
                if (state == TriState.Unchecked) hasUnchecked = true;
                if (hasChecked && hasUnchecked) return TriState.Indeterminate;
            }

            if (hasChecked && !hasUnchecked) return TriState.Checked;
            if (!hasChecked && hasUnchecked) return TriState.Unchecked;
            return TriState.Indeterminate;
        }

        public void Draw()
        {
            // 录制控制
            DrawRecordingControl();

            ImGui.Separator();

            // 录制文件分组
            DrawRecordingFileGroup();

            ImGui.Separator();

            // 自动录制触发分组
            DrawAutoRecordTriggerGroup();

            ImGui.Separator();

            // 自动停止录制分组
            DrawAutoStopRecordGroup();
        }

        private void DrawRecordingControl()
        {
            string buttonText;
            switch (_obsConnection.ObsRecordStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    buttonText = "录制开始中...";
                    break;
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    buttonText = "停止录制";
                    break;
                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    buttonText = "录制停止中...";
                    break;
                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    buttonText = "开始录制";
                    break;
                default:
                    buttonText = "状态未知";
                    break;
            }

            if (ImGui.Button(buttonText))
            {
                if (!_obsConnection.Connected) return;
                try
                {
                    if (_obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
                        _setRecordingDir();
                    _obsConnection.OBS.ToggleRecord();
                }
                catch (Exception e)
                {
                    _log.Error("Error on toggle recording: {0}", e);
                    _chat.PrintError("[OBSPlugin] Error on toggle recording, check log for details.");
                }
            }

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(
                _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED
                    ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                _obsConnection.ObsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED
                    ? "录制中" : "已停止");
        }

        private void DrawRecordingFileGroup()
        {
            ImGui.TextColored(new Vector4(0.54f, 0.71f, 0.97f, 1f), "录制文件");

            // 子文件夹
            ImGui.Text("子文件夹");
            ImGui.SameLine(100);
            if (ImGui.BeginCombo("##SubFolderMode", _config.SubFolderMode.ToString()))
            {
                foreach (SubFolderMode mode in Enum.GetValues<SubFolderMode>())
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
                foreach (FileNameMode mode in Enum.GetValues<FileNameMode>())
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

            // 开关选项
            if (ImGui.Checkbox("战斗开始时自动录制", ref _config.StartRecordOnCombat))
                _config.Save();

            if (ImGui.Checkbox("倒计时开始时自动录制", ref _config.StartRecordOnCountDown))
                _config.Save();

            if (ImGui.Checkbox("倒计时取消时停止录制", ref _config.StopRecordOnCountDownCancel))
                _config.Save();

            // 录制筛选折叠树
            if (ImGui.CollapsingHeader("录制筛选"))
            {
                ImGui.Indent();

                // 显示所有筛选
                if (ImGui.Checkbox("显示所有筛选", ref _config.ShowAllFilters))
                    _config.Save();

                DrawContentTree();

                ImGui.Unindent();
            }
        }

        private void DrawContentTree()
        {
            foreach (var l1 in _dutyTree)
            {
                // 判断是否可隐藏的分组
                bool isToggleable = l1.Key != "副本" && l1.Key != "多人内容";
                if (isToggleable && !_config.ShowAllFilters)
                    continue;

                var l1State = GetL1State(l1.Key);

                ImGui.Indent();
                if (ImGui.TreeNode(l1.Key))
                {
                    foreach (var l2 in l1.Value)
                    {
                        var l2State = GetL2State(l1.Key, l2.Key);

                        ImGui.Indent();
                        if (ImGui.TreeNode(l2.Key))
                        {
                            foreach (var entry in l2.Value)
                            {
                                bool selected = _config.SelectedContents.Get(entry.RowId);
                                if (ImGui.Checkbox(entry.Name, ref selected))
                                {
                                    _config.SelectedContents.Set(entry.RowId, selected);
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

- [ ] **Step 2: 修改 PluginUI.cs，初始化静态数据**

在 `PluginUI` 构造时调用 `RecordTab.Initialize(data)`:

```csharp
// 在 PluginUI 构造函数中添加
RecordTab.Initialize(Plugin.Data);
```

- [ ] **Step 3: 提交**

```bash
git add Window/Tabs/RecordTab.cs PluginUI.cs
git commit -m "refactor: rewrite RecordTab with grouped layout and content filter tree"
```

---

## 实现顺序

1. Task 1: BitSet 类
2. Task 2: 抽取 DutyTree 构建逻辑
3. Task 3: 更新 Configuration.cs
4. Task 4: 重构 RecordTab

---

## 注意事项

- `BitSet` 的 `Set()` 方法在 index 超出范围时自动扩容
- `BitSet` 使用 base64 序列化，通过 `BitSetConverter` 自动处理
- `DutyTree` 在插件启动时由 `PluginUI` 构造函数调用 `RecordTab.Initialize()` 构建一次，缓存供后续使用
- L1/L2 状态由 L3 的 BitSet 推导计算，不直接存储

---

**Plan complete.** 两个执行选项：

**1. Subagent-Driven (recommended)** - 每次 dispatch 一个 subagent 执行单个 Task，Task 间有检查点

**2. Inline Execution** - 在当前 session 内按 Task 顺序执行，有检查点

选择哪个？