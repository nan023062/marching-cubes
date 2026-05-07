# MarchingCubes Architecture

## 定位

3D Marching Cubes 等值面提取算法实现。核心提供两种网格构建器（平面/平滑），通过查表驱动，支持任意 iso 场输入；Sample 目录提供多个可运行的交互场景。

## 子模块

| 子模块 | 路径 | 职责 |
|--------|------|------|
| art-mesh-blender | `Blender/ArtMesh/` | Blender Python 工具：DCC 侧建模参考生成 + 批量 FBX 导出，衔接 ArtMesh Unity 工作流 |

子模块关系：art-mesh-blender 是 marching-cubes 的工具扩展，单向依赖（Blender 工具产出 FBX → Unity ArtMesh 消费），无反向依赖。

## 内部结构

```
MarchingCubes/
├── Runtime/                  ← 算法核心（无 Unity 场景依赖）
│   ├── CubeTable.cs          ← 256 种 edgeTable + triTable + 工具函数（纯静态）
│   ├── Cube.cs               ← 数据结构：Point、Edge、Vertex、Triangle、IMarchingCubeReceiver
│   ├── CubeMesh.cs           ← 平面网格构建器（每 cube 独立三角面）
│   ├── CubeMeshSmooth.cs     ← 平滑网格构建器（Edge long 哈希共享顶点，消除接缝）
│   ├── CubedMeshPrefab.cs    ← Prefab 标记组件（挂 CubeVertexMask）
│   └── MeshUtility.cs        ← 编辑器工具：生成 256 种预制体资产
│
└── Sample/                   ← 可运行示例（依赖 Runtime）
    ├── MarchingCubeBuilding/ ← 交互式建造（BlockBuilding + PointElement 点位操控）
    ├── Real-timeTerrain/     ← 实时 2D 地形（Perlin Noise，5×5 Chunk 视域）
    ├── ReattimeWorld/        ← 实时 3D 世界（iso 场，球形雕刻，2×2×2 Chunk 视域）
    ├── PolygonDrawer/        ← 256 种 cube 形态可视化
    ├── SmoothCubeMesh/       ← 平滑球体示例
    ├── GenerateSphere/       ← 球体生成示例
    └── Character/            ← TPS 角色控制（与算法无关）
```

## 关键设计约束

**CubeTable**：纯静态只读，存储 256 种 cube 形态的边掩码（edgeTable）和三角面索引（triTable）。顶点和边定义遵循固定约定（见 Cube.cs 注释图），不可改变。

**两种网格构建器的差异**：
- `CubeMesh`：每个 cube 独立分配三角形顶点，顶点不共享 → 法线不连续，有明显接缝，适合块状风格
- `CubeMeshSmooth`：用 `Edge`（long 哈希）为每条边全局唯一标识，相邻 cube 共享同一顶点 → 法线连续，平滑过渡

**IMarchingCubeReceiver**：解耦 iso 阈值来源与重建回调，允许不同 Sample 场景自定义 iso 语义（`GetIsoLevel()` + `IsoPass()` + `OnRebuildCompleted()`）。

**BlockBuilding（Sample）中的坐标约定**：`SetPointStatus(x,y,z)` 更新范围为 `[x-1, x] × [y-1, y] × [z-1, z]`，因为点 (x,y,z) 作为 cube 的某个顶点，其所在 cube 坐标比点坐标小 1。

## Facts（逆向提取，已验证）

- `CubeTable.CubeKind = 256`，`VertexCount = 8`，`EdgeCount = 12`
- `Edge` 结构：`StructLayout(Explicit)`，`_index: long`（offset 0）与 `x/y/z: sbyte` + `axis: Axis`（byte）共用内存
- `CubeMeshSmooth` 在 `Rebuild()` 中清空 `_edgeVertices` 字典，每帧完整重建顶点缓存
- `RealtimeTerrain`：ChunkCell=32, ChunkHeight=16, CellSize=0.5, Offset=5（ViewSize=11×11）
- `RealtimeWorld`：ChunkCellNum=32, Size=0.25, Offset=2（ViewSize=5×5×5），使用 `short` ChunkId 支持最大 ±halfCapacity 范围
