# Dalamud-OBS 重构计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将项目代码按职责拆分到独立文件，非必要尽量不修改代码逻辑。

**架构:** 按功能领域拆分 — OBS 连接管理、自动录制逻辑、模糊处理、UI 选项卡各自独立。

**Tech Stack:** C# / .NET 10 / Dalamud SDK

---

## 文件结构映射

### 现有文件 → 重构后文件

```
当前:                              重构后:
Plugin.cs (853行)                  →  Plugin.cs (精简后的入口)
                                   →  Services/ObsConnection.cs (OBS连接、keep-alive、版本检查)
                                   →  Services/AutoRecordLogic.cs (战斗/倒计时触发录制逻辑)
                                   →  Commands/ReplayCommandHandler.cs
                                   →  Commands/StreamCommandHandler.cs
                                   →  Commands/RecordCommandHandler.cs
                                   →  Commands/AudioCommandHandler.cs
                                   →  Commands/SceneCommandHandler.cs

PluginUI.cs (1824行)               →  PluginUI.cs (UI主控制器，组合各选项卡)
                                   →  Services/BlurManager.cs (模糊队列处理、OBS模糊增删)
                                   →  Window/Tabs/ConnectionTab.cs
                                   →  Window/Tabs/StreamTab.cs
                                   →  Window/Tabs/RecordTab.cs
                                   →  Window/Tabs/ReplayTab.cs
                                   →  Window/Tabs/BlurTab.cs
                                   →  Window/Tabs/AboutTab.cs
                                   →  Window/Tabs/DebugTab.cs

Configuration.cs (61行)           →  Configuration.cs (按 region 分组，允许字段重排)

Objects/StopWatchHook.cs           →  Services/StopWatchHook.cs (移动到 Services)
Objects/CombatState.cs             →  Objects/CombatState.cs (保持不变)
Objects/Blur.cs                    →  Objects/Blur.cs (保持不变)

Attributes/                        →  Attributes/ (保持不变)
PluginCommandManager.cs           →  PluginCommandManager.cs (保持不变)
```

---

### Task 1: 创建目录结构

**Files:**
- Create: `Services/`
- Create: `Commands/`
- Create: `Window/Tabs/`

- [ ] 创建空目录 `Services/`、`Commands/`、`Window/Tabs/`

---

### Task 2: 拆分 Plugin.cs — 提取 ObsConnection

**Files:**
- Create: `Services/ObsConnection.cs`
- Modify: `Plugin.cs`

**提取内容:**
- `OBSWebsocket obs` 字段
- `Connected`、`ConnectionFailed`、`versionInfo`、`streamStats` 字段
- `obsStreamStatus`、`obsRecordStatus`、`obsReplayBufferStatus` 字段
- `TryConnect()` 方法
- `onConnect()` 方法
- `onDisconnect()` 方法
- `onStreamingStateChange()`、`onRecordingStateChange()`、`onReplayBufferStateChange()` 方法
- `UpdateStreamStats()` 方法
- keep-alive task (`statPollKeepAlive`)
- `keepAliveTokenSource`、`keepAliveInterval`

- [ ] 创建 `Services/ObsConnection.cs`，包含上述字段和方法
- [ ] 在 `Plugin.cs` 中将 `ObsConnection` 作为字段保留，通过构造函数传入
- [ ] 确保构建通过

---

### Task 3: 拆分 Plugin.cs — 提取自动录制逻辑

**Files:**
- Create: `Services/AutoRecordLogic.cs`
- Modify: `Plugin.cs`

**提取内容:**
- `combatState` 字段
- `lastCountdownValue` 字段
- `StartRecordingWithReplayBuffer()` 方法
- `StopRecordingAsync()` 方法
- `combatState.InCombatChanged` 事件处理逻辑 (匿名方法)
- `combatState.CountingDownChanged` 事件处理逻辑 (匿名方法)
- `onTerritoryChanged()` 方法
- `_cts`、`_stoppingRecord` 字段

- [ ] 创建 `Services/AutoRecordLogic.cs`，封装所有自动录制相关逻辑
- [ ] `Plugin.cs` 中保留对 `AutoRecordLogic` 的引用
- [ ] 确保构建通过

---

### Task 4: 拆分 Plugin.cs — 提取命令处理器

**Files:**
- Create: `Commands/ReplayCommandHandler.cs`
- Create: `Commands/StreamCommandHandler.cs`
- Create: `Commands/RecordCommandHandler.cs`
- Create: `Commands/AudioCommandHandler.cs`
- Create: `Commands/SceneCommandHandler.cs`
- Modify: `Plugin.cs`

**提取内容 (各自独立文件):**
- `HandleReplayCommand(string args)` → `ReplayCommandHandler`
- `HandleStreamCommand(string args)` → `StreamCommandHandler`
- `HandleRecordCommand(string args)` → `RecordCommandHandler`
- `HandleAudioCommand(string args)` → `AudioCommandHandler`
- `HandleSceneCommand(string args)` → `SceneCommandHandler`

- [ ] 创建 `Commands/` 下的5个处理器文件
- [ ] 在 `Plugin.cs` 的 `ObsCommand` 中通过这些 handler 处理各子命令
- [ ] 确保构建通过

---

### Task 5: 拆分 PluginUI.cs — 提取 BlurManager

**Files:**
- Create: `Services/BlurManager.cs`
- Modify: `PluginUI.cs`

**提取内容:**
- `BlurItemsToAdd`、`BlurItemsToRemove` 队列
- `BlurDict` 字典
- `InitAddConsuming()`、`InitRemoveConsuming()` 线程启动
- `OBSAddOrUpdateBlur()`、`OBSRemoveBlur()`、`OBSRemoveBlurs()` 方法
- `UpdateBlur()` 方法
- `Dispose()` 中的队列清理逻辑

- [ ] 创建 `Services/BlurManager.cs`
- [ ] `PluginUI.cs` 中保留 `BlurManager` 实例
- [ ] 确保构建通过

---

### Task 6: 拆分 PluginUI.cs — 提取 UI 选项卡

**Files:**
- Create: `Window/Tabs/ConnectionTab.cs`
- Create: `Window/Tabs/StreamTab.cs`
- Create: `Window/Tabs/RecordTab.cs`
- Create: `Window/Tabs/ReplayTab.cs`
- Create: `Window/Tabs/BlurTab.cs`
- Create: `Window/Tabs/AboutTab.cs`
- Create: `Window/Tabs/DebugTab.cs`
- Modify: `PluginUI.cs`

**提取内容 (每个选项卡独立文件):**
- `DrawConnectionSettings()` → `ConnectionTab`
- `DrawStream()` → `StreamTab`
- `DrawRecord()` → `RecordTab`
- `DrawReplay()` → `ReplayTab`
- `DrawBlurSettings()` → `BlurTab`
- `DrawAbout()` → `AboutTab`
- `DrawDebug()` + `CacheDutyTree()` + `InitDutyEventHandlers()` → `DebugTab`

**注意:** `PluginUI.cs` 保留 `Draw()` 主方法，组合所有选项卡。

- [ ] 创建 `Window/Tabs/` 下的7个选项卡文件
- [ ] 修改 `PluginUI.cs` 的 `Draw()` 方法，通过各 `Tab` 类渲染内容
- [ ] 确保构建通过

---

### Task 7: 移动 StopWatchHook 到 Services

**Files:**
- Create: `Services/StopWatchHook.cs`
- Delete: `Objects/StopWatchHook.cs`

- [ ] 移动文件，更新命名空间
- [ ] 更新所有引用（`Plugin.cs`）
- [ ] 确保构建通过

---

### Task 8: 配置分离验证

**Files:**
- Modify: `Configuration.cs`
- Create: `docs/superpowers/plans/2026-06-06-dalamud-obs-refactor.md` (本文件)

**内容:**
- 为 `Configuration.cs` 的字段加 `#region` 分组（Blur Settings、Record Settings、Debug Settings）
- 允许重排字段顺序，但 region 分组必须正确

- [ ] 为 `Configuration.cs` 添加 region 分组
- [ ] 最终构建验证

---

## 验收标准

1. `dotnet build` 在重构前后都通过
2. 所有功能（连接、录制、模糊、命令）与重构前行为一致
3. 每个文件不超过 500 行（除 PluginUI.cs 主文件可能略超）
4. 文件按职责分组，代码引用关系清晰