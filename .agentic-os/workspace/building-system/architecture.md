# BuildingSystem Architecture

## 定位

地形改造（MQ）与 3D 建造（MC）的统一建造系统父模块。两个子系统基于各自的 Marching 算法独立运行，通过 BuildingManager 状态机和共同的 BuildingConst.Unit 在运行时协同。

## 子模块清单与关系

```
building-system/
├── marching-squares/   MQ 地形改造 — 高度场笔刷雕刻 + prefab tile 管理 + splatmap 地形类型混合
└── mc-building/        MC 3D 建造  — 离散格点建造 + prefab case 拼合
```

关系：两子模块**平级，无代码依赖**。均以 `marching-cubes` 模块为底层算法库（Association）。
协同由 `BuildingManager` 负责：持有 `MqTerrain` + `McStructure` 引用，通过 `TerrainState` / `BuildState` 状态机切换交互模式。

## 诞生背景

MarchingSquares 地形（MQ）与 McStructure 建造（MC）原各自以独立 Sample 存在。建造系统将两者整合为统一的游戏玩法层：玩家先用笔刷改造地形地貌，再在地形上用 MC 建造结构物。  
本次大规模重构（2026-05）引入 BuildingManager 状态机，将原散落在各 MonoBehaviour 的交互逻辑集中管理；同时将 Runtime/mq 层（Tile/MqTable/TileTerrain）从无到有建立为 marching-squares 的算法基础层，与 Runtime/mc 完全对称。

## 涌现性洞察

- **BuildingConst.Unit 是唯一真相源**：`MqTerrainBuilder.unit = 1f / Unit`，`McStructure.unit = Unit`，任何 unit 漂移都从这里改，不从两端分别改。
- **坐标系不共享**：`MqTerrainBuilder` 持有自己的 `localToWorld` 矩阵（用 unit 缩放），`McStructureBuilder` 也有独立矩阵，BuildingManager 须确保两者场景根节点对齐。
- **状态机是协同门**：TerrainState → BuildState 的切换时，`BuildState.SyncWithTerrain(MqTerrainBuilder)` 是唯一的跨子模块数据传递点（地形高度 → 建造底面）。
- **两层 MQ mesh**：`TileTerrain`（程序化连续 mesh）与 `MqTerrainBuilder`（prefab tile 管理）是并列的视觉方案，前者用于快速原型，后者是建造系统正式美术视觉层。
