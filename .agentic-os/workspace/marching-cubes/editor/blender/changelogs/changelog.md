# Blender Changelog

## [2026-05-10 17:30:00]
type: decision
title: 删 noise.py + 法线贴图烘焙 + 悬崖 operators；MQ 19 → 65 case base-3 编码
editor: architect；过渡产物（owner 治理前代写 append-only）

**触发**：terrain 模块 case 系统重设计（详见 `terrain/changelogs/changelog.md` [2026-05-10 17:30:00]）。本模块作为 DCC 端 mesh 与法线贴图烘焙的产出方，需同步删法线整套 + 改 MQ 65 case 编码。

**删除清单**：
- `noise.py`（整文件）
- `test_noise.py`（如存在）
- `mq_mesh.py` 内嵌 bake 段（`MQ_OT_ExportAllCases.execute` 中 `noise_field` / `bake_normal_map` 相关）
- `mq_mesh.py` 中 `MQProperties` 的 noise_* 字段（bake_normal_maps / noise_seed / noise_octaves / noise_amplitude / noise_frequency / normal_map_resolution）
- `mq_mesh.py` Panel 的 box5（法线贴图烘焙 UI）
- `mq_mesh.py` 悬崖部分：`CLIFF_CANONICAL` / `CLIFF_CASE_NAMES` / `_CLIFF_WALLS` / `_build_cliff_bm` / `MQ_OT_SetupCliffCases` / `MQ_OT_ExportCliffCases`
- `mq_mesh.py` `MQ_OT_ExportAll` 改为只导出地形 65 case
- `mq_mesh.py` Panel box1 的"悬崖"按钮 + box4 的悬崖文案
- `mq_mesh.py` `DIAGONAL2_HEIGHTS` 硬编码表（base-3 编码自然包含对角差=2 情形，无须特例）
- `build_zip.py`：剔除 noise.py / test_noise.py 文件名

**改造清单**：
- `mq_mesh.py` `ALL_CASES`：`list(range(19))` → 用 `is_valid_case(ci)` 过滤 `range(81)`，保留 65 个 base-3 有效 case_idx
- `mq_mesh.py` 高度数组 `h`：`[1.0 if (ci & (1 << i)) else 0.0 for i in range(4)]` → `[(ci // (3**i)) % 3 for i in range(4)]`（base-3 解码）
- `mq_mesh.py` `CASE_NAMES`：删 0~18 硬编码，改为程序化生成 case_idx → "r0r1r2r3" 字符串
- `mq_mesh.py` `MQ_OT_SetupAllCases` / `MQ_OT_GenerateTerrain`：循环范围 19 → 65，h 数组按 base-3 解码
- `mq_mesh.py` `MQ_OT_ExportAllCases`：循环范围 + h 解码同上
- `mq_mesh.py` `GRID_COLS`：4 → 9（容纳 65 case）

**保留清单**：
- `cube_table.py` / `mc_mesh.py`（MC 部分不动）
- `bilinear_arc(u, v, h, arc_s, flat)` 函数（mesh 几何生成器，原 19 case 与新 65 case 共用，零修改）
- FBX 导出参数（axis_forward='Y' / axis_up='Z' / mesh_smooth_type='FACE'）
- `MQ_OT_ValidateMesh`（验证 [0,1]³ 范围，与 case 数无关）

**与 Unity 端的对账要求**（硬约束）：
- Python `case_idx = r0 + r1*3 + r2*9 + r3*27` 与 C# `TileTable.GetMeshCase` 公式必须**字节级一致**
- FBX 命名 `mq_case_<case_idx>.fbx` 与 Unity 端 `MQMeshConfigEditor` 扫描正则 `mq_case_(\d+).fbx` 必须对齐

**审计意图**：mq-normalmaps task 已 cancelled，本次改造由 worker subagent 闭环实装。
