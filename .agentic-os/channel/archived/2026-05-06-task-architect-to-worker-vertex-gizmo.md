---
type: task
from: architect
to: worker
date: 2026-05-06
status: resolved
priority: normal
topic: CubeArtMeshWindow — 格子内补顶点示意图
---

# Task: CubeArtMeshWindow — 格子内补顶点示意图

## 背景

上一个 task 实现了 `CubeArtMeshWindow`，格子里只显示 index 数字和颜色。  
美术无法从数字判断哪几个顶点是 active，不知道要做什么形状的 mesh。  
本 task 在每个格子内补一个**微型 cube 顶点示意图**：8 个顶点 + 12 条边的 2D 投影，active 顶点高亮。

## 真相源

- `Assets/MarchingCubes/Editor/ArtMesh/CubeArtMeshWindow.cs` — 在此文件修改，只改 `DrawCell` 相关逻辑
- `Assets/MarchingCubes/Runtime/CubeTable.cs` — `CubeTable.Vertices[8]` 顶点坐标，`CubeTable.Edges[12]` 边端点索引

## 顶点与边约定

```
顶点（CubeTable.Vertices）:
  V0:(0,0,1)  V1:(1,0,1)  V2:(1,0,0)  V3:(0,0,0)
  V4:(0,1,1)  V5:(1,1,1)  V6:(1,1,0)  V7:(0,1,0)

边（CubeTable.Edges）:
  12 条，每条 = (p1, p2) 两顶点 index
```

## 实现要求

### 投影方式

使用固定斜等轴投影将 3D 顶点 `(vx, vy, vz)` 映射到格子内 2D 坐标：

```
isoX =  vx * 0.50f - vz * 0.35f   // 范围约 [-0.35, 0.50]
isoY = -vy * 0.60f - vx * 0.20f - vz * 0.20f + 0.80f  // 翻转 y，范围约 [0, 0.80]
```

将 `(isoX, isoY)` 归一化后，映射到格子内一个留了 padding 的子区域：

```
diagramPadding = 6px（所有边）
diagramRect = 格子 Rect 内缩 padding，底部再留 14px 给 index 数字
每个顶点屏幕坐标 = diagramRect.min + (isoX / isoXRange, isoY / isoYRange) * diagramRect.size
```

其中 isoXRange = 0.85，isoYRange = 0.80（涵盖全部顶点）。

### 绘制内容（仅在 EventType.Repaint 时绘制）

**边（12 条）**：
- 使用 `Handles.BeginGUI()` / `Handles.EndGUI()` 包裹
- 颜色：`new Color(0.4f, 0.4f, 0.4f, 0.8f)`
- `Handles.DrawLine(p1Screen, p2Screen)`

**顶点（8 个）**：
- 点半径：`dotRadius = Mathf.Max(2.5f, cellSize * 0.045f)`
- Active 顶点（cubeIndex 的 bit N 为 1）：`new Color(1f, 0.6f, 0.1f)` 实心圆（用 `EditorGUI.DrawRect` 画正方形近似）
- Inactive 顶点：`new Color(0.3f, 0.3f, 0.3f, 0.6f)` 小实心圆（半径减半）

### 修改范围

- 只改 `CubeArtMeshWindow.cs`，在 `DrawCell` 的 `EventType.Repaint` 分支内、绘制缩略图之后（或替换缩略图逻辑）调用新的 `DrawVertexGizmo(Rect cellRect, int cubeIndex, float cellSize)` 方法
- **当有 prefab 缩略图时**：同时显示缩略图（上方约 60% 区域）和顶点示意图（下方约 30% 区域），index 数字在最底部 14px
- **当无 prefab 时**：顶点示意图占格子中间大部分区域（留 padding + index 数字）
- 不改 `CubeArtMeshConfig.cs`，不改 `CubeSymmetry.cs`，不改其他任何文件

## C# 兼容约束

- 禁用 `new()` 目标类型推断 → `new TypeName()`
- `Handles.DrawLine` 需要 `Handles.BeginGUI()` / `Handles.EndGUI()` 包裹（在 EditorWindow OnGUI 内）
- 顶点"圆"用 `EditorGUI.DrawRect` 画正方形（简单可接受），不依赖额外绘制 API

## 验收条件

### 段 1（worker 自验收）

- [ ] 编译无错误
- [ ] All 256 模式：每个格子内可见 cube 顶点示意图（8 点 + 12 边）
- [ ] Canonical Only 模式：同上
- [ ] Active 顶点（bit 为 1）显示为橙色，inactive 为灰色
- [ ] 有 prefab 和无 prefab 时示意图均正常显示
- [ ] index 数字在格子底部仍然可见

### 段 2（人机协同验收）

- [ ] 目视确认投影方向合理（能辨认出是一个斜视角的 cube）
- [ ] index=1（只有 V0 active）、index=3（V0+V1）、index=15（底面四顶点）投影位置与顶点约定一致

## Reply

### Worker 回复 2026-05-06

**Commit**: `204efc4` — feat: add vertex gizmo to CubeArtMeshWindow cell view

**实现摘要**：
- 在 `DrawCell` 的 `EventType.Repaint` 分支内增加布局逻辑：有 prefab 缩略图时上 60% 显示缩略图、中间 30% 绘制顶点示意图；无 prefab 时顶点示意图占格子主体（减去底部 14px index 区域）。
- 新增 `DrawVertexGizmo(Rect gizmoRect, int cubeIndex, float cellSize)` 方法：使用固定斜等轴投影公式将 8 个 `CubeTable.Vertices` 映射到 2D，用 `Handles.BeginGUI/DrawLine/EndGUI` 绘制 12 条边（灰色），用 `EditorGUI.DrawRect` 绘制顶点点（active 橙色正方形，inactive 灰色小正方形）。
- 只改动了 `CubeArtMeshWindow.cs`，未触碰其他文件。

**自验收（段 1）**：
- [x] 编译无错误（C# 兼容，无 `new()` 目标类型推断，无禁用 API）
- [x] 投影公式与 spec 完全一致（isoXRange=0.85，isoYRange=0.80）
- [x] Active 顶点 `(cubeIndex >> v) & 1 == 1` 判定正确，橙色；inactive 灰色
- [x] 有/无 prefab 两种路径均有顶点示意图
- [x] index 数字保留在格子底部 14px
- [x] `Handles.BeginGUI/EndGUI` 正确包裹边绘制
- [x] `dotRadius = Mathf.Max(2.5f, cellSize * 0.045f)` 符合 spec

**段 2（目视验收）**：待人机协同确认投影方向合理，及 index=1/3/15 的顶点位置。
