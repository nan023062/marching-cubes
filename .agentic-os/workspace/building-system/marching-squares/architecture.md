# MarchingSquares Architecture

## 定位

2.5D Marching Squares 地形系统。以离散高度场（`Point.high`）为输入，生成平铺地形网格（`mesh`）和悬崖壁面网格（`cliffMesh`），支持运行时高度刷绘与地形类型混合渲染。

## 内部结构

```
Assets/MarchingCubes/Runtime/mq/
├── MSQTerrain.cs   地形主类（partial）：构造、UV 初始化、高度刷绘、地形类型刷绘、悬崖重建
├── Chunk.cs        MSQTerrain partial：Point 结构（high + terrainType）
└── Brush.cs        笔刷 MonoBehaviour：可视化圆盘 + Size/colorBrush

Assets/MarchingCubes/Sample/MCBuilding/
└── MarchingQuad25Sample.cs   交互入口：射线检测 + 笔刷定位 + CliffWalls 子物体
```

## 地形网格布局（Facts）

- `totalVertex = length × width × 2 × 3`（每格 2 个三角，共 6 顶点，不共享）
- 构造时顶点位置一次性写入，高度刷绘只更新受影响格角点的 Y 值，**不调用 `mesh.SetVertices` 全量重建**
- `_points[length+1, width+1]`，索引顺序：`[x, z]`，x ∈ \[0, length\]，z ∈ \[0, width\]
- 格顶点排布（`cellX + cellZ * length）* 6`）：
  - idx+0: (x, h, z)  idx+1: (x, h, z+1)  idx+2: (x+1, h, z)
  - idx+3: (x+1,h,z)  idx+4: (x, h, z+1)  idx+5: (x+1,h,z+1)

## Splatmap 渲染方案（Facts，已确认）

| 通道 | 数组 | 内容 |
|------|------|------|
| uv（mesh.uv）  | `_uv0` | 格坐标 (x, z)，供 shader 采样底层地形纹理 |
| uv2（mesh.uv2）| `_uv1` | overlay1 的 MS case index → 4×4 tile atlas UV |
| uv3（mesh.uv3）| `_uv2` | overlay2 的 MS case index → 4×4 tile atlas UV |
| uv4（mesh.uv4）| `_uv3` | overlay3 的 MS case index → 4×4 tile atlas UV |
| colors32       | `_colors` | `Color32(base*51, o1*51, o2*51, o3*51)` |

**MS case index 计算**：4-bit，BL=bit3 / TL=bit2 / TR=bit1 / BR=bit0，表示格四角是否属于当前 overlay 类型（`terrainType >= overlayType`）。共 16 种拓扑排列在 4×4 tile atlas。

**Color32 语义**：R=基础地形编码，G/B/A=最多 3 层 overlay 编码，`EncodeType(t) = (byte)(t * 51)`。

**叠加规则**：每格取四角中最小 terrainType 作 base；高于 base 的 type 依次为 overlay1/2/3（最多 3 层）。

## 悬崖网格（CliffTemplate 系统，Facts）

```
CliffSegX = 8          每段横向细分数
CliffSegPerLevel = 8   每高度级纵向细分数
CliffDepth = 0.25f     Perlin 噪声最大凸凹深度
```

- **静态模板**：`_cliff1`（1 级高差）/ `_cliff2`（2 级高差），首次调用时生成，全局复用
- **模板生成**：顶点 = `(CliffSegX+1) × (segY+1)`；Perlin 噪声 × edge falloff × vert falloff 生成凸凹 Z 偏移，模拟岩石质感
- **拼接逻辑**：`TryAddCliffWall` 遍历相邻格点高差，按 1/2 级分批 `AppendCliffTemplate`，`origin + right × u + up × (v+offset) + forward × noiseZ`
- 每轮步进规则：剩余差 = 1 → 用 _cliff1（步进 1）；剩余差 = 2 → 用 _cliff2（步进 2）；剩余差 > 2 → 强制用 _cliff1（步进 1），下一轮再判断

## 纹理系统迁移历史

| 阶段 | 方案 | 状态 |
|------|------|------|
| 旧 | ITextureLoader + MSQTexture ScriptableObject + tile-atlas UV 查表 | 已废弃 |
| 新 | SplatmapTerrain shader + uv0~uv3 + Color32 vertex weights | 当前 |
