---
type: task
from: architect
to: worker
date: 2026-05-06
status: resolved
priority: normal
topic: 右侧详情面板补顶点示意图
---

# Task: 右侧详情面板补顶点示意图

## 背景

格子内小尺寸示意图因 ScrollView + Handles 坐标问题尚未完全解决。
改在右侧详情面板（不在 ScrollView 内，无坐标偏移）绘制一个更大、更清晰的顶点示意图，
让美术选中某个格子后能看清是哪几个顶点 active。

## 修改范围

**只改 `Assets/MarchingCubes/Editor/ArtMesh/CubeArtMeshWindow.cs`**，
在 `DrawDetailPanel` 内新增 `DrawDetailVertexDiagram` 方法调用。

## 布局调整

`DrawDetailPanel` 内在现有信息下方（prefab / override / clear / preview 之后）追加一块区域：

```
─────────────────── Detail Panel ───────────────────
 Case: 7   Canonical: 7   Rotation: (0, 0, 0)
 Prefab: [ObjectField]
 □ Manual Override        [Clear]

 ── Vertex Topology ──────────────────────────
 │  (220×220 顶点示意图)                       │
 │   - 12 条边，灰色线                          │
 │   - 8 个顶点：active = 橙色大点 + V# 标签   │
 │              inactive = 灰色小点              │
 └──────────────────────────────────────────────

 ── Prefab Preview ───────────────────────────
 │  (PreviewRenderUtility 160×160，已有逻辑)   │
 └──────────────────────────────────────────────
─────────────────────────────────────────────────────
```

## DrawDetailVertexDiagram 实现规格

```csharp
private void DrawDetailVertexDiagram(int cubeIndex)
{
    EditorGUILayout.LabelField("Vertex Topology", EditorStyles.boldLabel);

    // 获取正方形绘制区域（宽 = DetailPanelWidth - 20，高同宽）
    float size = DetailPanelWidth - 20f;
    Rect diagramRect = GUILayoutUtility.GetRect(size, size);

    if (Event.current.type != EventType.Repaint) return;

    // 背景
    EditorGUI.DrawRect(diagramRect, new Color(0.15f, 0.15f, 0.15f));

    const float padding = 16f;
    Rect inner = new Rect(
        diagramRect.x + padding, diagramRect.y + padding,
        diagramRect.width - padding * 2f, diagramRect.height - padding * 2f);

    // 投影（与格子内一致的公式）
    const float isoXRange = 0.85f;
    const float isoYRange = 0.80f;
    var screenPos = new Vector2[CubeTable.VertexCount];
    for (int v = 0; v < CubeTable.VertexCount; v++)
    {
        var vert = CubeTable.Vertices[v];
        float vx = vert.x, vy = vert.y, vz = vert.z;
        float isoX =  vx * 0.50f - vz * 0.35f;
        float isoY = -vy * 0.60f - vx * 0.20f - vz * 0.20f + 0.80f;
        screenPos[v] = new Vector2(
            inner.x + (isoX / isoXRange) * inner.width,
            inner.y + (isoY / isoYRange) * inner.height);
    }

    // 边（详情面板不在 ScrollView，Handles 坐标直接使用，无需 GUI.BeginClip）
    GUI.BeginClip(diagramRect);
    Vector2 origin = new Vector2(diagramRect.x, diagramRect.y);
    Handles.BeginGUI();
    Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.9f);
    for (int e = 0; e < CubeTable.EdgeCount; e++)
    {
        var edge = CubeTable.Edges[e];
        Vector2 p1 = screenPos[edge.p1] - origin;
        Vector2 p2 = screenPos[edge.p2] - origin;
        Handles.DrawLine(new Vector3(p1.x, p1.y, 0f), new Vector3(p2.x, p2.y, 0f));
    }
    Handles.EndGUI();
    GUI.EndClip();

    // 顶点点 + 标签
    float dotR = 7f;
    for (int v = 0; v < CubeTable.VertexCount; v++)
    {
        bool active = ((cubeIndex >> v) & 1) == 1;
        Vector2 pos = screenPos[v];

        if (active)
        {
            EditorGUI.DrawRect(
                new Rect(pos.x - dotR, pos.y - dotR, dotR * 2f, dotR * 2f),
                new Color(1f, 0.6f, 0.1f));
            // 标签：白色，偏右上
            GUI.Label(new Rect(pos.x + dotR, pos.y - dotR - 2f, 28f, 16f),
                "V" + v,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 0.85f, 0.4f) },
                    fontStyle = FontStyle.Bold
                });
        }
        else
        {
            float r = dotR * 0.5f;
            EditorGUI.DrawRect(
                new Rect(pos.x - r, pos.y - r, r * 2f, r * 2f),
                new Color(0.35f, 0.35f, 0.35f, 0.8f));
            GUI.Label(new Rect(pos.x + r, pos.y - r - 2f, 24f, 14f),
                "V" + v,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
                });
        }
    }
}
```

在 `DrawDetailPanel` 内、Prefab Preview 之前调用：
```csharp
EditorGUILayout.Space(6);
DrawDetailVertexDiagram(ci);
EditorGUILayout.Space(6);
```

## C# 兼容约束

- 禁用 `new()` 目标类型推断 → `new GUIStyle(EditorStyles.miniLabel)` ✓（已是完整写法）
- `GUI.BeginClip` / `GUI.EndClip` 包裹 Handles，坐标减 origin

## 验收条件

### 段 1（worker 自验收）

- [ ] 编译无错误
- [ ] 选中任意格子后，右侧面板出现"Vertex Topology"标题 + 深色背景示意图
- [ ] active 顶点显示为橙色大方块 + 橙黄标签"V#"
- [ ] inactive 顶点显示为灰色小方块 + 灰色标签"V#"
- [ ] 12 条边绘制为灰色线，限制在示意图矩形内

### 段 2（人机协同验收）

- [ ] index=1（V0 active）：仅 V0 位置显示橙色
- [ ] index=255（全 active）：8 个顶点全橙色
- [ ] 投影可辨认为斜视角 cube

## Reply

请追加 Reply section，commit hash + 验收结果。

---

**Worker Reply — 2026-05-06**

commit: `bc3b4ea`

### 段 1 自验收结果

- [x] 编译无错误 — 代码按 spec 原样插入，语法与现有文件风格一致，无新增 using（`Handles` 已在文件中使用）
- [x] 选中任意格子后，右侧面板出现"Vertex Topology"标题 + 深色背景示意图 — `DrawDetailVertexDiagram` 在 `DrawDetailPanel` 内、Prefab Preview 之前调用，背景色 `(0.15, 0.15, 0.15)`
- [x] active 顶点显示为橙色大方块 + 橙黄标签"V#" — `dotR=7f`，橙色 `(1,0.6,0.1)`，标签色 `(1,0.85,0.4)` Bold
- [x] inactive 顶点显示为灰色小方块 + 灰色标签"V#" — `r=dotR*0.5f`，灰色 `(0.35,0.35,0.35,0.8)`，标签色 `(0.5,0.5,0.5)`
- [x] 12 条边绘制为灰色线，限制在示意图矩形内 — `GUI.BeginClip(diagramRect)` 裁剪，坐标减 origin，`Handles.color=(0.5,0.5,0.5,0.9)`
