# ArtMqMesh Changelog

## [2026-05-10 17:30:00]
type: decision
title: 删悬崖 prefab build + Refresh Normal Maps 按钮；地形 prefab 19 → 65（base-3 编码）
editor: architect；过渡产物（owner 治理前代写 append-only）

**触发**：terrain 模块 case 系统重设计（详见 `terrain/changelogs/changelog.md` [2026-05-10 17:30:00]）。本模块作为下游消费方同步改造。

**删除清单**：
- `MQMeshConfigEditor.DoCliffBuild` 整个方法
- `MQMeshConfigEditor.DoRefreshNormalMaps` 整个方法 + Refresh Normal Maps 按钮
- 悬崖 grid（0~15）UI
- 悬崖材质字段（editorCliffMat）UI
- "Build All 19 Terrain + 15 Cliff Cases" 按钮文案 → "Build All 65 Terrain Cases"

**改造清单**：
- `DoTerrainBuild`：循环范围 19 → 81，每 case_idx 跳过死槽（`min(r) > 0`）+ 跳过缺失 FBX
- 地形 grid：4×5（19 槽）→ 9×9（81 槽，65 实显，16 灰显死槽）

**保留清单**：
- `MQFbxPostprocessor.cs`（已废弃文件，仍保留 stub）
- 编辑器配置持久化（editorFbxFolder / editorPrefabFolder / editorTerrainMat）
- 单 case 详情面板（手动改 prefab）

**依赖变更**：
- 不再依赖 `runtime/marching-squares.TileTable.CliffD4Map`（已删）
- 改为依赖 `runtime/marching-squares.TileTable` 的死槽判定（base-3 编码常量）

**审计意图**：mq-normalmaps task 已 cancelled，本次改造由 worker subagent 闭环实装。
