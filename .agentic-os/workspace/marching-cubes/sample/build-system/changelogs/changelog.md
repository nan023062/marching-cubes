# BuildSystem Changelog

## [2026-05-09 00:00:00]
type: decision
title: 大规模重构：命名规范统一 + BuildingManager 状态机引入 + workspace 路径迁移

MCBuilding/BlockBuilding → McStructureBuilder；MarchingCubeBuilding/MCBuilding → McStructure；
MSQTerrain（原 monolithic） → MqTerrain（薄壳）+ MqTerrainBuilder（核心）；
MQTerrain → MqTerrain；MQTerrainBuilder → MqTerrainBuilder。

新增 BuildingManager：持有 MqTerrain + McStructure 引用，通过 TerrainState / BuildState 状态机切换交互模式。
新增 BuildingConst：Unit=1 为两子模块 unit 对齐唯一真相源（MqTerrainBuilder.unit=1f/Unit，McStructure.unit=Unit）。

workspace 路径从 Sample/McStructure 整体迁移到 Sample/BuildSystem。

## [2026-05-08 00:00:00]
type: decision
title: 建造系统父模块立项

将 MQ 地形改造（marching-squares）与 MC 3D 建造（mc-building）整合为统一父模块 building-system。
原因：两者目标是同一个游戏玩法层（地形改造 + 建造），缺少统一边界导致 unit 对齐等跨模块约束散落无处记录。
marching-squares 从 workspace 根迁入本模块；mc-building 从 marching-cubes Sample 中独立为子模块。
