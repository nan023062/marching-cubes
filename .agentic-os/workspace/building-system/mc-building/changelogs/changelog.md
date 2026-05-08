# MC-Building Changelog

## [2026-05-08 00:00:00]
type: decision
title: 逆向建档初始化，从 marching-cubes Sample 独立为 building-system 子模块

基于现有代码逆向提取 module.json / architecture.md / contract.md。
范围：Assets/MarchingCubes/Sample/MCBuilding/mc/ 下的建造系统核心。
独立原因：MCBuilding 是完整的建造玩法层，与 marching-cubes 底层算法库职责不同，归入 building-system 父模块更合理。

关键设计事实：
- BlockBuilding 是纯 C# 核心，无 MonoBehaviour 依赖
- Point.iso 阈值 0.5f（大于则实心）
- CasePrefabConfig 抽象基类统一 D4FbxCaseConfig 和 IosMeshCaseConfig 两种配置格式
- MarchingCubeBuilding.unit 与 MSQTerrain.unit 是跨模块对齐约束
