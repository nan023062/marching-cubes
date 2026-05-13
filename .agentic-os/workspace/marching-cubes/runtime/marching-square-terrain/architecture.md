# Runtime/MarchingSquares Architecture

## 定位

2.5D Marching Squares 算法基础层（叶子模块）。纯算法数据层，无 MonoBehaviour 依赖（除 TilePrefab 调试组件外）。提供查表、核心类型和工具组件，供上层应用（`sample/build-system/terrain`）构建完整地形系统。

## 内部结构

```
Runtime/MarchingSquares/
├── Tile.cs           ← 核心类型定义：TileVertex / TileVertexMask / TileEdge / TilePoint / TileVertex2D / TileTriangle / ITileTerrainReceiver
├── TileTable.cs      ← 81 槽 base-3 编码查表：GetMeshCase / IsValidCase / GetTextureCase / GetTerrainLayers / GetAtlasCase / GetAtlasCell
├── TileTerrain.cs    ← 程序化连续 mesh 生成器（快速原型用）：Rebuild / RebuildHeightOnly
└── TilePrefab.cs     ← tile prefab 纯数据组件（MonoBehaviour）：caseIndex / baseHeight（Gizmos 已下放到 Terrain 层统一渲染，详见 sample/build-system/terrain）
```

## TileTable 两类映射（Facts）

### 1. Mesh case 映射（驱动 TileCaseConfig.GetPrefab）

```
输入：四角高度 h0(BL) h1(BR) h2(TR) h3(TL)
base = min(h0, h1, h2, h3)
r_i = h_i - base  ∈ {0, 1, 2}
case_idx = r0 + r1*3 + r2*9 + r3*27
输出：case_idx ∈ [0, 80]  +  baseH
```

**为什么是 65 真实几何 + 16 死槽 = 81 槽**：
- `r_i ∈ {0,1,2}^4` 共 3^4 = 81 组合
- 16 个不可达组合（所有 `r_i ≥ 1`，即 `r_i ∈ {1,2}^4`）应该被 base 再下降一级归约，永远不会从 GetMeshCase 产出
- 真实有效几何 = 65 个

死槽换零查表：`prefabs[GetMeshCase(...)]` 直接索引，不需要 lookup 表，代价是 16 个 null 槽位。

### 2. 纹理 case 映射（驱动 shader 纹理混合）

```
输入：四角 terrainType t0~t3，overlayType
ci = bit_i(t_i >= overlayType)   → 16 种混合拓扑
GetTerrainLayers：提取 baseType + 最多 3 层 overlay（0~3 层）
GetAtlasCase：4 角 mask + 单 bit type → atlas case_idx (0~15)
```

注：mesh case (81 槽 base-3) 与 atlas overlay case (16 槽 4-bit mask) **完全解耦**——前者是高度组合，后者是 terrainType bitmask；两者编码不同、容量不同、用途不同。

## TileCaseConfig 无 D4 对称设计决策（Facts）

MQ tile 存在 Mesh 几何 + 纹理 UV 双重约束。D4 旋转虽能减少 prefab 数量，但旋转后 UV 方向改变导致纹理映射错误。因此 TileCaseConfig 存储 81 个独立 prefab slot（其中 65 个有效），美术须为每个 case 单独制作正确 UV 的 mesh。

（对比：MC 的 D4FbxCaseConfig 只有 Mesh 几何约束，可做 53→255 对称归约）

## TileTerrain vs TerrainBuilder（定位对比）

| 维度 | TileTerrain（本模块） | TerrainBuilder（terrain 层） |
|------|-----------------------|-------------------------------|
| 层级 | Runtime（无 MonoBehaviour 依赖） | Sample 应用层（纯 C#） |
| 输出 | 单张连续 Mesh | prefab tile 实例阵列 + colliderMesh |
| 用途 | 快速原型 / 无配置场景 | 建造系统正式美术视觉层 |
| 资产依赖 | 无（程序化） | TileCaseConfig（美术 prefab） |
| 纹理 | UV0 格坐标（shader 采样） | per-tile MaterialPropertyBlock _TileMsIdx / _TileMsIdx4 |

## 与上层的边界

本模块不含业务逻辑：
- **不含** `TerrainBuilder`（地形格点 + colliderMesh 管理）→ 在 `sample/build-system/terrain`
- **不含** `Terrain`（MonoBehaviour 薄壳）→ 在 `sample/build-system/terrain`
- **不含** `TileCaseConfig`（81 槽 prefab 配置）→ 在 `sample/build-system/terrain`

`TileTerrain` 是快速原型工具，适合不需要 prefab 的场景（直接生成连续 mesh）。

## 悬崖系统下线（[2026-05-11] 重大决策）

本模块原含悬崖 tile 支持：`CliffD4Map[16]` / `CliffCanonicalCases[5]` / `TileType.Cliff` / `CliffEdge` / `CliffEdgeMask` 等类型与查表数据。

新 base-3 编码下，同格 4 角高差 ≤ 2 全部由 65 坡面 case 完整表达，**不再需要独立悬崖 tile**。本次改造：
- TileTable：删 `CliffD4Map` / `CliffCanonicalCases` / `CliffCaseCount`
- Tile.cs：删 `enum TileType { Terrain, Cliff }`、`enum CliffEdge`、`[Flags] enum CliffEdgeMask`
- TilePrefab.cs：删 `tileType` 字段、`CliffEdgeBottom[]`、`DrawCliffGizmos()`；`DrawTerrainGizmos()` 改 base-3 r∈{0,1,2} 三档可视化

详见 `changelogs/changelog.md` [2026-05-11] decision。
