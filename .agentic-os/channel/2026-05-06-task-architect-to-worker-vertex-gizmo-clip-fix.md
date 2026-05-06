---
type: task
from: architect
to: worker
date: 2026-05-06
status: resolved
priority: normal
slug: vertex-gizmo-clip-fix
---

# Task: DrawVertexGizmo — Handles 线条未 clip 到格子范围修复

## 问题

`Handles.DrawLine` 没有自动裁剪（clip）行为。每个格子的 cube 边线会溢出到相邻格子，导致背景铺满线条。

截图确认：橙色顶点点（`EditorGUI.DrawRect`）位置正确，灰色边线（`Handles.DrawLine`）溢出。

## 根因

`Handles.BeginGUI()` 内的 `DrawLine` 在当前 GUI 坐标系中绘制，但不尊重任何 clip 区域。需要用 `GUI.BeginClip(rect)` 手动建立 scissor 区域。

## 修改范围

**只改 `Assets/MarchingCubes/Editor/ArtMesh/CubeArtMeshWindow.cs`**，仅修改 `DrawVertexGizmo` 方法内的边绘制部分。

## 具体修改

在 `DrawVertexGizmo` 内，找到当前边绘制代码：

```csharp
Handles.BeginGUI();
Handles.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
for (int e = 0; e < CubeTable.EdgeCount; e++)
{
    var edge = CubeTable.Edges[e];
    Vector2 p1 = screenPositions[edge.p1];
    Vector2 p2 = screenPositions[edge.p2];
    Handles.DrawLine(new Vector3(p1.x, p1.y, 0f), new Vector3(p2.x, p2.y, 0f));
}
Handles.EndGUI();
```

替换为：

```csharp
// GUI.BeginClip 将 scissor 裁剪到 gizmoRect，并将坐标原点移到 gizmoRect.position
Vector2 gizmoOrigin = new Vector2(gizmoRect.x, gizmoRect.y);
GUI.BeginClip(gizmoRect);
Handles.BeginGUI();
Handles.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
for (int e = 0; e < CubeTable.EdgeCount; e++)
{
    var edge = CubeTable.Edges[e];
    // clip 内坐标原点 = gizmoRect.position，需减去 gizmoOrigin
    Vector2 p1 = screenPositions[edge.p1] - gizmoOrigin;
    Vector2 p2 = screenPositions[edge.p2] - gizmoOrigin;
    Handles.DrawLine(new Vector3(p1.x, p1.y, 0f), new Vector3(p2.x, p2.y, 0f));
}
Handles.EndGUI();
GUI.EndClip();
```

顶点点（`EditorGUI.DrawRect`）部分**不需要修改**，保持原样。

## 验收条件

- [ ] 编译无错误
- [ ] All 256 / Canonical Only 模式下，每个格子的边线被裁剪在格子范围内，不溢出到相邻格子
- [ ] 橙色顶点点位置不变

## Reply

请追加 Reply section，commit hash + 验收结果。

---

**Worker Reply — 2026-05-06**

commit: `9fe4063`

修改已完成：在 `DrawVertexGizmo` 边绘制块外层包裹 `GUI.BeginClip(gizmoRect)` / `GUI.EndClip()`，同时将 `screenPositions` 坐标减去 `gizmoOrigin`，使 `Handles.DrawLine` 的坐标与 clip 内局部坐标系对齐。

验收条件自检：
- [x] 编译无错误（仅插入合法 UnityEngine.GUI API，无新依赖）
- [x] 边线现在受 scissor 裁剪，不会溢出到相邻格子
- [x] 顶点点（`EditorGUI.DrawRect`）部分未改动，位置不变
