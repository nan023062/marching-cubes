# Structure Changelog

## [2026-05-11 11:30:00]
type: implementation
title: pointquad-cell-flat 实装收尾（块 5 + 块 6）

**背景**：[2026-05-11 01:38:43] 决定段已落地块 1~4 代码（Terrain.IsCellFlat / Structure.PointCubePrefab/PointQuadPrefab / PointQuad.cx,cz / BuildState 重写）。本次仅补尾：

- **块 5 BuildingManager**：删 `_pointCubePrefab/_pointQuadPrefab` 字段 + getter + Header；line 49 改 `new BuildState(structure)`，BuildingManager 退化为纯 wiring（terrain/structure/_switchKey/_initialMode）。
- **块 6 Scene Inspector 引用迁移（方案 A）**：直接编辑 `mc_bulding.unity` yaml。BuildingManager 节点（fileID 122453180）删除 161-164 两行 prefab 引用；Structure 节点（fileID 999007710）`_currentConfigIndex: 0` 后追加同 fileID + guid 的 `_pointCubePrefab/_pointQuadPrefab`，无需老板手工拖。
  - 选 A 而非 B 的理由：Structure 节点 yaml 里此前未序列化过这两个字段（Unity 从未触发过 reimport），yaml 干净，原地按相同 fileID/guid 直接复制 2 行 = 无歧义、零拖拽风险；老板下次 Unity reload 时 Inspector 自动显示已绑定状态。

**自验**：
1. C# 字段/getter/ctor 调用一致性：grep `_pointCubePrefab/_pointQuadPrefab` → 仅 Structure.cs（字段 + getter）+ mc_bulding.unity（Structure 节点 2 行引用），BuildingManager / BuildState 已无残留。
2. Unity 编译需老板触发 Editor reload 验证（CLI sandbox 无法执行）。
3. Play mode 4 个交互场景需老板在 Unity 实际操作。

## [2026-05-11 01:38:43]
type: decision
title: PointQuad 改为「平地 cell 按需生成」+ prefab 引用收口到 Structure

**PointQuad 生成语义重定义**：
- 旧：(x-1)*(z-1) 个内部格点全量生成；每个 quad 以格点为中心 scale=(1,0,1)，跨越 4 cells；高度 = 4 邻 cell 角 max
- 新：仅当 cell 4 角 high 完全相等时才在该 cell 中心生成；位置 (cx+0.5, baseH+0.5, cz+0.5)；高度 = 该 cell 的 baseH

**判定收口**：terrain 模块新增 `Terrain.IsCellFlat(cx,cz,out int baseH)` + `TerrainBuilder.IsCellFlat(...)` 公开 API；structure 通过此 API 查询，"什么是平地" 的领域知识独占在 terrain 不散落。

**生命周期改造**：
- `BuildState._pointQuads`：`List<GameObject>` → `GameObject[length, width]`（cell 索引直接寻址）
- `InitBuilding`：删除 PointQuad 全量生成，仅分配空数组
- `SyncWithTerrain`：扫所有 cell，按 IsCellFlat 增/删/移位 PointQuad；PointCube 冲突销毁段不变
- 触发链不变：TerrainState 在地形刷绘后回调 SyncWithTerrain（入口已存在），无需新增依赖

**坐标系对齐**：PointQuad 字段 `int x, z` → `int cx, cz`（cell 索引语义自显，避免与 PointCube 的体素索引混淆）；`CreateCube(quad.cx, 1, quad.cz)` 让 cube 落在该平地 cell 正上方（cube 占 [cx..cx+1, 1..2, cz..cz+1]）。

**Prefab 引用迁移**：
- 旧：`BuildingManager._pointCubePrefab / _pointQuadPrefab` 持有，ctor 注入 BuildState
- 新：`Structure._pointCubePrefab / _pointQuadPrefab`（[SerializeField] + public getter），`new BuildState(structure)` 自取
- 动机：prefab 是 structure 的 GameObject，归 Structure 自持是单一职责；BuildingManager 退化为纯 wiring（持有 Terrain/Structure 引用 + 状态机）

**附带 doc drift 修复**：本次改到的段落内 `McStructure → Structure` / `McStructureBuilder → StructureBuilder` / `MqTerrainBuilder → TerrainBuilder` 顺手对齐到实际类名；其余段落留待后续 sweep。

**Scene 影响**：`mc_bulding.unity` 中 BuildingManager 的 `_pointCubePrefab / _pointQuadPrefab` 字段引用需手工迁到 Structure 组件（Inspector 拖一次）。

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
