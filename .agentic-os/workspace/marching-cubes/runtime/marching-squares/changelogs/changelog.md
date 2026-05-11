# Runtime/MarchingSquares Changelog

## [2026-05-11 12:30:00]
type: decision
title: TilePrefab 简化为纯数据组件，Gizmos 责任下放到 Terrain 层
editor: architect；过渡产物（owner 治理前代写 append-only）

**触发**：terrain 模块 [2026-05-11 12:30:00] 决策（DrawGizmos 重做为 WC3 风格点阵 grid + 顶点 type 球，统一在 Terrain 层渲染）的下游传导。

**改动**：
- **删除** `OnEnable` / `OnDrawGizmos` / `DrawTerrainGizmos` / `_labelStyle` / `_cornerStyle` / `using UnityEditor`
- **保留** `caseIndex` / `baseHeight` 两个字段 + `[ExecuteAlways]` 属性
- TilePrefab 退化为纯数据 MonoBehaviour，仅供 Inspector 查看 case_idx / baseHeight 调试

**根据 C2 单一职责原则的责任划分**：
| 组件 | 职责 |
|------|------|
| TilePrefab（runtime 层） | 持有 case_idx / baseHeight 数据；不渲染任何视觉 |
| TerrainBuilder.DrawGizmos（sample/terrain 层） | 渲染整张地形的 WC3 风格点阵 grid |

**收益**：
- 65 case 全场铺开后视觉清爽（以往每 prefab 各画 4 角高度球 + V0~V3 标签 → 视觉拥挤）
- 性能：N×N tile 各自调 OnDrawGizmos → 改为 1 次 TerrainBuilder.DrawGizmos
- TilePrefab 不再依赖 TileTable.Corners（C3 单向依赖更清）

## [2026-05-11 09:00:00]
type: decision
title: 算法基础层同步 base-3 编码改造：TileTable 81 槽 + 删悬崖类型与查表数据 + Tile.cs / TilePrefab.cs 同步清理
editor: architect；过渡产物（owner 治理前代写 append-only）
supersedes: 本模块旧版"19-case + 16-case 悬崖查表"全部内容

**触发**：terrain 模块 [2026-05-10 17:30:00] 决策（19 → 65 case base-3 编码 + 悬崖整套下线 + 法线贴图整套下线）的下游传导。架构师在 2026-05-10 派单时**漏改**本模块知识三件套，导致 worker 实装完成后发现 TileType.Cliff / CliffEdge / TilePrefab.DrawCliffGizmos 等悬崖语义残留——本次 [2026-05-11] 补完。

**架构师疏漏责任声明**：
- 2026-05-10 第一轮派单只列 3 个模块（terrain / art-mq-mesh / blender），漏 runtime/marching-squares
- worker 严格按 spec + changelog 执行，无错；偏差全部由架构师设计阶段未覆盖造成
- 本条 changelog 同时承担"漂移修正"职责，禁止后续以"runtime/marching-squares 不在改造范围"为由保留任何悬崖残留

**TileTable.cs 改造**：
- `GetMeshCase(h0,h1,h2,h3, out baseH)`：原 4-bit 编码 + 4 个对角差=2 特殊分支（case 15~18）→ 改 base-3 编码 `case_idx = r0 + r1*3 + r2*9 + r3*27`，r_i = h_i - base ∈ {0,1,2}
- 新增 `IsValidCase(caseIdx)`：解出 r0..r3，返回 `min(r) == 0`；离线工具 / Editor grid 用它判定死槽
- 新增 `BaseCaseCount = 81` 常量
- **保留** `CornerCount = 4` / `CaseCount = 16`（atlas overlay 4-bit mask case 数，与 mesh case 完全解耦，不能混淆）/ `Corners[]` / `GetTextureCase` / `GetTerrainLayers` / `GetAtlasCase` 三个重载 / `GetAtlasCell`
- **删除** `CliffCaseCount` / `CliffD4Map[]` / `CliffCanonicalCases[]`

**Tile.cs 改造**：
- **删除** `enum TileType { Terrain, Cliff }`
- **删除** `enum CliffEdge { S, E, N, W }`
- **删除** `[Flags] enum CliffEdgeMask`
- **保留** `TileVertex` / `TileVertexMask` / `TileEdge` / `TilePoint` / `TileVertex2D` / `TileTriangle` / `ITileTerrainReceiver`

**TilePrefab.cs 改造**：
- **删除** `tileType` 字段（不再有 Terrain/Cliff 区分）
- **删除** `CliffEdgeBottom[]` 数组
- **删除** `DrawCliffGizmos()` 方法
- 改 `DrawTerrainGizmos()`：解出 r_i = (caseIndex / 3^i) % 3，按 r ∈ {0,1,2} 三档高度（Y = 0/1/2 单位）可视化角点；颜色梯度灰/橙/红
- 改 `caseIndex` 注释：0~18 → 0~80（base-3 编码）；删 Cliff 0~15 描述
- 改 OnDrawGizmos：标签 `bits` 不再 PadLeft 4-bit；改成 `r=({r0},{r1},{r2},{r3})` 直观显示三档高度

**命名漂移修正**（额外清理本次发现的旧文档残留，与本次改造范围对齐）：
- 旧 `MqTable` → `TileTable`（已早期完成，仅 module.json / architecture.md / contract.md 文字残留）
- 旧 `MqMeshConfig` → `TileCaseConfig`（同上）
- 旧 `MqTerrainBuilder` → `TerrainBuilder`（同上）
- 旧 `ISquareTerrainReceiver` → `ITileTerrainReceiver`（同上）
- 旧 `MqTilePrefab` → `TilePrefab`（同上）

**审计意图**：
- 本次改造由 architect 派第二轮 worker subagent 闭环实装
- 未来若发现"基础层算法变更影响上层应用"的同类决策，必须按 MKO 双向自检（identity § 自检原则）：子改 → 父改 / 上游改 → 下游改的传导清单要明示
