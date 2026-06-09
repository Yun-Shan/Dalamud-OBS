# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 提供在此代码仓库中工作的指导。

## 项目概述

Dalamud-OBS 是一个 [Dalamud](https://github.com/goatcorp/Dalamud) 插件，通过 WebSocket 控制 OBS Studio。主要功能：
- 通过 obs-websocket 协议管理 OBS 连接
- 通过 obs-composite-blur 插件对 UI 元素进行模糊处理
- 自动录制触发（战斗、倒计时、区域切换）
- 回放缓冲区管理

## 构建命令

```bash
# Debug 构建（输出到 bin\Debug\）
dotnet build OBSPlugin.csproj -c Debug -p:Platform=x64

# Release 构建（输出到 bin\Release\）
dotnet build OBSPlugin.csproj -c Release -p:Platform=x64

# 清理构建
dotnet clean OBSPlugin.csproj -c Debug
```

使用 .NET SDK 10.0 和 Dalamud.NET.Sdk/15.0.0（配置在 global.json 中）。

## 架构

```
Plugin.cs # 入口点，管理 OBS WebSocket 连接和命令分发
PluginUI.cs # ImGui 配置窗口，模糊管理，UI 元素检测
Configuration.cs       # IPluginConfiguration 可序列化设置
PluginCommandManager.cs# 通过反射属性自动注册命令的管理器
Objects/
  CombatState.cs       # InCombat/CountingDown 状态及事件处理
  StopWatchHook.cs     # 倒计时器相关逻辑
  Blur.cs              # 模糊滤镜描述符（位置、大小、启用状态）
Attributes/
  CommandAttribute.cs  # 标记方法为 /obs 子命令
  AliasesAttribute.cs
  HelpMessageAttribute.cs
  DoNotShowInHelpAttribute.cs
```

### 关键模式

**命令注册**：带 `[Command]` 属性的方法由 `PluginCommandManager` 自动注册：
```csharp
[Command("/obs")]
[HelpMessage("Open OBSPlugin config panel.")]
public void ObsCommand(string command, string args) { ... }
```

**静态服务注入**：Dalamud 服务通过 `[PluginService]` 属性注入到 `Plugin.cs` 的静态字段。

**约束：PluginService 静态字段必须直接访问**。装饰为 `[PluginService]` 的静态字段（`PluginInterface`、`PluginLog`、`DutyState`、`DataManager`）是单例，在整个插件生命周期内可用。**禁止**通过构造函数参数传递这些服务，必须直接在需要的地方使用 `Plugin.PluginLog`、`Plugin.DutyState` 等静态引用。

**模糊异步管道**：`PluginUI` 使用 `BlockingCollection<Blur>` 队列进行添加/删除操作，由专用线程消费，与 OBS 异步通信。

**OBS通信**：使用 `lib/obs-websocket-dotnet` 子模块中的 `OBSWebsocket`。

**约束：禁止修改 `lib/obs-websocket-dotnet` 子模块**。该子模块为外部依赖，仅通过 git submodule引用。如需修改OBS协议行为，应在包装层（`Services/ObsService.cs` 或类似）进行，而非直接修改子模块。

### OBS WebSocket 事件

`Plugin.cs` 订阅： `Connected`、`Disconnected`、`StreamStateChanged`、`RecordStateChanged`、`ReplayBufferStateChanged`。

## 依赖

- `lib/obs-websocket-dotnet/obs-websocket-dotnet/obs-websocket-dotnet.csproj` — OBS WebSocket 协议客户端（**禁止修改子模块内容**）
- Dalamud SDK 包（通过 Dalamud.NET.Sdk 引入）
- FFXIVClientStructs 用于访问游戏结构体