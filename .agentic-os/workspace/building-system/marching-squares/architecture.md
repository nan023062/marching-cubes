# MarchingSquares Architecture

## 定位

2.5D Marching Squares 地形系统。以离散高度场（`Point.high`）为输入，通过 16-case 查表驱动 prefab tile 拼合，生成可运行时雕刻的地形。分为三层：算法基础层（Runtime/mq）、纯C# 核心层（MqTerrainBuilder）、Unity 组件层（MqTerrain）。

## 内部结构

```
Assets/MarchingCubes/Runtime/MarchingSquares/   — 算法基础层（类比 Runtime/MarchingCubes）
├── Tile.cs          MQ 基础类型定义：TileVertex/TileVertexMask/TileEdge/
│                    TilePoint/TileVertex2D/TileTriangle/ISquareTerrainReceiver
├── MqTable.cs       全局静态映射表：GetMeshCase() / GetTextureCase() / GetTerrainLayers()
├── MqTilePrefab.cs  tile prefab 调试组件（类比 CubedMeshPrefab）：caseIndex/baseHeight/Gizmos
└── TileTerrain.cs   程序化连续 mesh 生成器（类比 CubeMesh）：Rebuild() / RebuildHeightOnly()

Assets/MarchingCubes/Sample/BuildSystem/Terrain/   — 建造系统集成层
├── MqTerrain.cs         MonoBehaviour 薄壳（类比 McStructure）：持有 MqTerrainBuilder
├── MqTerrainBuilder.cs  纯C# 核心（类比 McStructureBuilder）：数据 + 碰撞 Mesh + Tile 生命周期
├── MqMeshConfig.cs      16槽 ScriptableObject：GetPrefab(0-15) / SetPrefab
├── Brush.cs             笔刷 MonoBehaviour：Size / colorBrush / transform.position
├── MSQTerrain.cs        已废弃空壳（保留 meta 引用，由 MqTerrainBuilder 取代）
└── TerrainState.cs      状态机实现（由 BuildingManager 驱动）
```

## MqTable 两类映射（Facts）

### 1. Mesh case 映射（驱动 MqMeshConfig.GetPrefab）

```
输入：四角高度 h0(BL) h1(BR) h2(TR) h3(TL)
base = min(h0, h1, h2, h3)
ci = bit0(h0>base) | bit1(h1>base) | bit2(h2>base) | bit3(h3>base)
输出：ci ∈ [0, 15]  +  baseH
```

### 2. 纹理 case 映射（驱动 shader 纹理混合，GetTextureCase + GetTerrainLayers）

```
输入：四角 terrainType t0~t3，overlayType
ci = bit_i(t_i >= overlayType)   → 16种混合拓扑
GetTerrainLayers：提取 baseType + 最多3层 overlay
```

## MqTerrainBuilder 算法（Facts）

```
Point[length+1, width+1]     格点数组（角点，含边界）
GameObject[length, width]    tile 实例数组（格，不含边界）
colliderMesh                 碰撞用平面网格（只存 Y 高度，无 UV / 颜色）
```

- `Point.high`：sbyte，运行时 clamp 到 [-64, 64]（`SetPointHeightDelta` 硬约束）
- `Point.terrainType`：byte，0~4（泥/草/岩/雪/腐）
- 高度刷绘路径：`BrushMapHigh` → `SetPointHeightDelta` → `ApplyPointHeight`（更新 `_vertices`）→ `EnforceHeightConstraint`（BFS 传播相邻约束）→ `colliderMesh.vertices = _vertices` → `RefreshAffectedTiles`
- 地形类型刷绘路径：`PaintTerrainType` → 更新 `_points[x,z].terrainType` → `UpdateAffectedTileColors`（只更新 MaterialPropertyBlock，不销毁重建 tile）
- tile 实例化：`RefreshTile(x, z)` → `GetCaseIndex` → `MqTable.GetMeshCase` → `MqMeshConfig.GetPrefab` → `Instantiate` → `ApplyTileTerrainColors`（注入 _T0~_T3）

## colliderMesh 顶点格式（Facts）

```
totalVertex = length × width × 2 × 3
index(x,z) = (x + length * z) * 6
idx+0: (x, h(BL), z)      idx+1: (x, h(TL), z+1)    idx+2: (x+1, h(BR), z)
idx+3: (x+1, h(BR), z)    idx+4: (x, h(TL), z+1)    idx+5: (x+1, h(TR), z+1)
```

高度更新时 `ApplyPointHeight(px, pz, newHigh)` 逐一修正受影响的最多 4 个格顶点，不全量重建。

## MqMeshConfig 无 D4 对称设计决策（Facts）

MQ tile 存在 Mesh 几何 + 纹理 UV 双重约束。D4 旋转虽能减少 prefab 数量，但旋转后 UV 方向改变，导致纹理映射错误。因此 MqMeshConfig 存储 16 个独立 prefab slot（index 0~15），美术须为每个 case 单独制作正确 UV 的 mesh。

## TileTerrain vs MqTerrainBuilder（架构对比）

| 维度 | TileTerrain | MqTerrainBuilder |
|------|-------------|-----------------|
| 输出 | 单张连续 Mesh | prefab tile 实例阵列 + colliderMesh |
| 用途 | 快速原型 / 无配置场景 | 建造系统正式美术视觉层 |
| 资产依赖 | 无（程序化） | MqMeshConfig（美术 prefab） |
| 纹理 | UV0 格坐标（shader 采样） | MaterialPropertyBlock _T0~_T3 |
| 类比 | CubeMesh（程序化 mesh） | McStructureBuilder（prefab 管理） |

## 纹理系统历史

| 阶段 | 方案 | 状态 |
|------|------|------|
| 初始 | ITextureLoader + MSQTexture ScriptableObject + tile-atlas UV 查表 | 已废弃 |
| 中间 | SplatmapTerrain shader + MSQTerrain（monolithic） | 已废弃（MSQTerrain 已成空壳） |
| 当前 | MqTerrainBuilder + MqMeshConfig prefab + MaterialPropertyBlock _T0~_T3 | 现行 |
