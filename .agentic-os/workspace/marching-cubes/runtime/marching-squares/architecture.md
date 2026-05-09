# Runtime/MarchingSquares Architecture

## 定位

2.5D Marching Squares 算法基础层（叶子模块）。纯算法数据层，无 MonoBehaviour 依赖。提供查表、核心类型和工具组件，供上层应用（`sample/build-system/terrain`）构建完整地形系统。

## 内部结构

```
Runtime/MarchingSquares/
├── Tile.cs           ← 核心类型定义：TileVertex/TileVertexMask/TileEdge/TilePoint/TileVertex2D/TileTriangle/ISquareTerrainReceiver
├── MqTable.cs        ← 16-case 查表：GetMeshCase / GetTextureCase / GetTerrainLayers
├── TileTerrain.cs    ← 程序化连续 mesh 生成器（快速原型，类比 CubeMesh）：Rebuild / RebuildHeightOnly
└── MqTilePrefab.cs   ← tile prefab 调试组件（MonoBehaviour）：caseIndex / baseHeight / Gizmos
```

## MqTable 两类映射（Facts）

### 1. Mesh case 映射（驱动 MqMeshConfig.GetPrefab）

```
输入：四角高度 h0(BL) h1(BR) h2(TR) h3(TL)
base = min(h0, h1, h2, h3)
ci = bit0(h0>base) | bit1(h1>base) | bit2(h2>base) | bit3(h3>base)
输出：ci ∈ [0, 15]  +  baseH
```

### 2. 纹理 case 映射（驱动 shader 纹理混合）

```
输入：四角 terrainType t0~t3，overlayType
ci = bit_i(t_i >= overlayType)   → 16 种混合拓扑
GetTerrainLayers：提取 baseType + 最多 3 层 overlay（0~3 层）
```

## MqMeshConfig 无 D4 对称设计决策（Facts）

MQ tile 存在 Mesh 几何 + 纹理 UV 双重约束。D4 旋转虽能减少 prefab 数量，但旋转后 UV 方向改变导致纹理映射错误。因此 MqMeshConfig 存储 16 个独立 prefab slot（index 0~15），美术须为每个 case 单独制作正确 UV 的 mesh。  
（对比：MC 的 D4FbxCaseConfig 只有 Mesh 几何约束，可做 53→255 对称归约）

## TileTerrain vs MqTerrainBuilder（定位对比）

| 维度 | TileTerrain（本模块） | MqTerrainBuilder（terrain 层） |
|------|-----------------------|-------------------------------|
| 层级 | Runtime（无 MonoBehaviour 依赖） | Sample 应用层（纯 C#） |
| 输出 | 单张连续 Mesh | prefab tile 实例阵列 + colliderMesh |
| 用途 | 快速原型 / 无配置场景 | 建造系统正式美术视觉层 |
| 资产依赖 | 无（程序化） | MqMeshConfig（美术 prefab） |
| 纹理 | UV0 格坐标（shader 采样） | MaterialPropertyBlock _T0~_T3 |
| 类比 | CubeMesh（程序化 mesh） | McStructureBuilder（prefab 管理） |

## 纹理系统历史（Facts）

| 阶段 | 方案 | 状态 |
|------|------|------|
| 初始 | ITextureLoader + MSQTexture ScriptableObject + tile-atlas UV 查表 | 已废弃 |
| 中间 | SplatmapTerrain shader + MSQTerrain（monolithic）+ uv0~uv3 + Color32 权重 | 已废弃（MSQTerrain 成空壳）|
| 当前 | MqTerrainBuilder + MqMeshConfig prefab + MaterialPropertyBlock _T0~_T3 | 现行 |

## 与上层的边界

本模块不含业务逻辑：
- **不含** `MqTerrainBuilder`（地形格点 + colliderMesh 管理）→ 在 `sample/build-system/terrain`
- **不含** `MqTerrain`（MonoBehaviour 薄壳）→ 在 `sample/build-system/terrain`
- **不含** `MqMeshConfig`（16 槽 prefab 配置）→ 在 `sample/build-system/terrain`

`TileTerrain` 是快速原型工具，适合不需要 prefab 的场景（直接生成连续 mesh）。
