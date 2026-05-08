# BuildingSystem Changelog

## [2026-05-08 00:00:00]
type: decision
title: 建造系统父模块立项

将 MQ 地形改造（marching-squares）与 MC 3D 建造（mc-building）整合为统一父模块 building-system。
原因：两者目标是同一个游戏玩法层（地形改造 + 建造），缺少统一边界导致 unit 对齐等跨模块约束散落无处记录。
marching-squares 从 workspace 根迁入本模块；mc-building 从 marching-cubes Sample 中独立为子模块。
