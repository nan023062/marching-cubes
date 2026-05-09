# Structure Changelog

## [2026-05-09 00:00:00]
type: decision
title: 重构：McStructure 职责收缩为薄壳 + workspace 路径迁移 + 交互逻辑迁出到 BuildState

**McStructure 职责边界调整**：
- 重构前：McStructure 实现 IMeshStore 接口，自身处理点击交互和 config 切换 UI
- 重构后：McStructure 只做参数配置 + 委托桥（SetBuildHandlers 注入 clickHandler/onConfigChanged）+ GetMesh 内联实现
- IMeshStore 接口及 BlockMesh.cs 移除，功能等价但不声明接口

**McStructureBuilder 依赖变化**：
- MeshStore 字段类型从 IMeshStore 改为 McStructure（直接引用，减少间接层）

**交互逻辑迁移**：
- SetPointStatus / 点击处理 / config UI 全部迁移到 BuildState
- BuildState 持有 McStructureBuilder，响应 McStructure.OnClicked 委托

**workspace 路径迁移**：
- Sample/McStructure/mc/ → Sample/BuildSystem/Structure/

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
