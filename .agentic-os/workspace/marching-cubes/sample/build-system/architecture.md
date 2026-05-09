# BuildSystem Architecture

## 定位

地形改造 + 3D 建造综合演示（父模块）。是唯一同时使用 MC 和 MQ 双算法的 Sample 案例。通过 BuildingManager 状态机将两套完全独立的交互逻辑（TerrainState / BuildState）统一管理。

## 子模块清单与关系

```
build-system/
├── terrain/    ← MQ 地形层（MqTerrain + MqTerrainBuilder + TerrainState + Brush + MqMeshConfig）
└── structure/  ← MC 建造层（McStructure + McStructureBuilder + BuildState + PointElement 系列）
```

根目录文件：`BuildingConst.cs`、`BuildingManager.cs`、`IBuildState.cs`

依赖关系：
- `terrain` → `runtime/marching-squares`（MqTable + Tile 类型）
- `structure` → `runtime/marching-cubes`（CubeTable + MC 核心类型）
- `terrain` ↔ `structure`：无直接代码依赖；协同通过 BuildingManager + 地形同步回调

## 诞生背景

需要验证 MQ 地形改造与 MC 3D 建造能在同一场景中协同工作：地形高度影响建造起始位置，地形改变时建造结构需同步调整。BuildingManager 作为协调者，而非直接在两套系统间建立依赖。

## 涌现性洞察

- **Unit 对齐是核心约束**：MqTerrainBuilder 以 `1f / Unit` 为格子尺寸，McStructure 以 `Unit` 为整数格子数，`BuildingConst.Unit` 是唯一真相源，任一子模块擅自修改都会导致坐标系错位
- **地形同步回调**：TerrainState 在地形刷绘完成后调用 `buildState.SyncWithTerrain(builder)`，McStructure 层检查坐标冲突并销毁被地形覆盖的 PointCube，这是两套系统唯一的数据交叉点
