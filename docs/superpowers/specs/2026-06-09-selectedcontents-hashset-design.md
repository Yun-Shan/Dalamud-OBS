# SelectedContents BitSet → HashSet Migration Design

## Overview

将 `Configuration.SelectedContents` 从 `BitSet`（按索引位存储）改为 `HashSet<uint>`（直接存储 RowId）。

## CheckboxStatus Enum

位置：`Window/UiHelper.cs`

```csharp
internal enum CheckboxStatus
{
    Unchecked = 0,
    Checked = 1,
    Indeterminate = -1
}
```

`UiHelper.Checkbox()` 参数从 `ref int val` 改为 `ref CheckboxStatus status`。

## Cascade Logic

| 当前状态 | 点击后 `checked_` |
|----------|------------------|
| Unchecked | `true` |
| Indeterminate | `true` |
| Checked | `false` |

实现：`checked_ = state != CheckboxStatus.Checked`

## Core Data Change

| 变更项 | Before | After |
|--------|--------|-------|
| 字段类型 | `BitSet SelectedContents` | `HashSet<uint> SelectedContents` |
| L2 状态计算 | `CountRange(startIndex, count)` | 遍历 L2 entries，检查 `HashSet.Contains(entry.RowId)` |
| L3 勾选 | `Set(rowId, bool)` | `Add(rowId)` / `Remove(rowId)` |
| L2 级联 | `SetRange(start, count, bool)` | 遍历 L2 entries，`AddAll` / `Clear` |
| L1 级联 | 同上 | 遍历 L1 下所有 entries，`AddAll` / `Clear` |

## TriState Migration

- 从 `Configuration.TriState` 移至 `Window.UiHelper.CheckboxStatus`
- `RecordTab.cs` 中更新类型引用

## Data Migration

`Configuration.Load()` 时检测旧 BitSet 格式（base64 string 序列化的 BitSet），解析后转为 HashSet。

## Delete

- `Services/BitSet.cs` — 删除
- `Services/BitSetConverter` — 删除（合并在 BitSet.cs 中）

## Affected Files

| 文件 | 变更 |
|------|------|
| `Window/UiHelper.cs` | 添加 `CheckboxStatus` enum，修改 `Checkbox()` 参数 |
| `Configuration.cs` | 字段改为 `HashSet<uint>`，移除 `TriState`，添加迁移逻辑 |
| `RecordTab.cs` | 使用 `CheckboxStatus`，重写状态计算和级联逻辑 |
| `Services/BitSet.cs` | **删除** |

## Implementation Order

1. `UiHelper.cs` — 添加 `CheckboxStatus` enum，修改 `Checkbox()` 方法签名
2. `Configuration.cs` — 改字段类型，移除 `TriState`，添加迁移逻辑
3. `RecordTab.cs` — 重写状态计算和级联逻辑
4. 删除 `Services/BitSet.cs`