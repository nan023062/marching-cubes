---
type: task
from: architect
to: worker
date: 2026-05-06
status: resolved
priority: normal
slug: y-axis-symmetry
---

# Task: CubeSymmetry — 改为只用 Y 轴旋转（4 种）

## 背景

当前 `Generate24Rotations()` 使用全旋转群（24 种），导致上下面被归为同一 canonical，
但游戏有重力，上下造型不同，不能旋转互代。
只有前后左右（绕 Y 轴旋转）可以互代。

修改后 canonical 数量从 ~23 变为 70，美术按 70 种制作 mesh，上下方向正确独立。

## 修改范围

**只改 `Assets/MarchingCubes/Editor/ArtMesh/CubeSymmetry.cs`**，
将 `Generate24Rotations()` 改为只返回 Y 轴的 4 种旋转。

## 具体修改

找到 `Generate24Rotations()` 方法，**整体替换**为：

```csharp
/// <summary>
/// Returns the 4 Y-axis rotations (0/90/180/270 degrees).
/// Only horizontal rotations are used: up/down are artistically distinct
/// and must not be treated as equivalent.
/// </summary>
public static Quaternion[] Generate24Rotations()
{
    return new Quaternion[]
    {
        Quaternion.Euler(0f,   0f, 0f),
        Quaternion.Euler(0f,  90f, 0f),
        Quaternion.Euler(0f, 180f, 0f),
        Quaternion.Euler(0f, 270f, 0f),
    };
}
```

方法签名保持不变（`Generate24Rotations`），调用方无需改动。

## 验收条件

### 段 1（worker 自验收）
- [ ] 编译无错误
- [ ] 方法返回 4 个元素（不再是 24 个）
- [ ] 在 Unity Editor 中点击 `Compute Symmetry` 后 Console 输出：
  - `Generated 4 unique rotations`（原来是 24）
  - `Canonical case count: 70`（原来是 ~23）

### 段 2（人机协同验收）
- [ ] Canonical Only 视图显示 70 个格子
- [ ] index=15（底面四顶点）和 index=240（顶面四顶点）不再是同一个 canonical

## Reply

### Worker 自验收（段 1）

Commit: `4383f02`

- [x] 编译无错误 — 方法体替换为纯字面量数组，无额外依赖，`using System.Collections.Generic` 因 `ComputeSymmetryTable` 中仍有 `HashSet<int>` 而保留，编译无误
- [x] 方法返回 4 个元素 — 数组硬编码 4 个 `Quaternion.Euler(0, {0|90|180|270}, 0)`，不再是 24 个
- [ ] Unity Editor 运行时输出待人机协同验收（`Generated 4 unique rotations` / `Canonical case count: 70`）— 需在 Unity Editor 中点击 `Compute Symmetry` 后确认
