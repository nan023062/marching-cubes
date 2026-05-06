# MarchingSquares Architecture

## 定位

2.5D Marching Squares 地形系统。以离散高度场（Point.high）为输入，生成平铺地形网格（mesh）和悬崖壁面网格（cliffMesh），支持运行时高度刷绘与地形类型混合渲染。

## 内部结构

```
MarchingSquares/
├── Runtime/                   ← 算法核心
│   ├── MSQTerrain.cs          ← 地形主类（partial）：构造、UV 初始化、刷绘、网格更新
│   ├── Chunk.cs               ← MSQTerrain partial：Point 结构（high + terrainType）
│   ├── MSQTexture.cs          ← 已废弃（历史注释，说明从 tile-atlas 迁移到 splatmap）
│   ├── MSQMesh.cs             ← ScriptableObject Mesh 容器（已弃用）
│   ├── Brush.cs               ← 笔刷结构（半径 + colorBrush 模式）
│   └── Util.cs                ← 工具函数
│
├── Shaders/                   ← 渲染着色器（新）
│   ├── SplatmapTerrain.shader ← 地形混合着色器（4 UV 通道 weight-based blend）
│   └── CliffWall.shader       ← 悬崖壁面着色器
│
├── Editor/
│   ├── MSQTextureGenerator.cs ← 编辑器工具（生成纹理资产）
│   └── MarchingSquareSplitter.cs
│
└── Sample/
    ├── MarchingQuad25Sample.cs ← 交互编辑器（射线检测 + 笔刷 + CliffWalls 子物体）
    └── Resources/
        ├── SplatmapTerrain.mat ← 地形材质
        ├── CliffWall.mat       ← 悬崖材质
        └── *.asset             ← 纹理资产
```

## 关键设计约束

**地形网格（mesh）布局**：每格由 2 个三角形组成（6 顶点，不共享）。顶点位置预分配在构造时固定，刷绘时只更新 uv/color，`mesh.SetVertices` 不再调用。

**splatmap 渲染方案**：
- `_uv0`：每格格坐标（x,z），供 shader 采样底层纹理
- `_uv1~_uv3`：地形类型混合权重通道（推断，待确认 shader 实现）
- `_colors (Color32)`：vertex color，编码地形类型权重（EncodeType = type * 51）

**悬崖网格（cliffMesh）**：运行时动态构建（`_cliffVertices / _cliffTriangles / _cliffUVs` List），与 mesh 分开，需独立 GameObject + MeshRenderer 挂载。

**CliffWalls 子物体**：由 `MarchingQuad25Sample.Awake()` 在运行时动态创建，不在 Prefab 中。

**地形类型编码**：`TerrainTypeCount = 5`，`EncodeType(type) = (byte)(type * 51)`，对应 0/51/102/153/204，均匀分布 byte 范围。

## 纹理系统迁移历史（推断）

| 阶段 | 方案 | 状态 |
|------|------|------|
| 旧 | ITextureLoader + MSQTexture ScriptableObject + tile-atlas UV | 已废弃 |
| 新 | SplatmapTerrain shader + 4 UV 通道 + Color32 vertex weights | 当前（开发中） |

## Facts（逆向提取）

- `MSQTerrain` 构造时预分配 `totalVertex = width * length * 2 * 3` 个顶点（每格 6 顶点）
- `TileUVSize = (0.25f, 0.25f)` — 预留给 4×4 tile-atlas，可能在新 shader 中调整
- `Point.high` 为 `sbyte`（-128~127），`Point.terrainType` 为 `byte`（0~255）
