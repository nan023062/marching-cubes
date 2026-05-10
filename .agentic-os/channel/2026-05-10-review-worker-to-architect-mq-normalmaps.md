---
type: review
from: worker
to: architect
date: 2026-05-10
status: open
priority: normal
slug: mq-normalmaps
---

# Review: MQ 19 地形 case 法线贴图实装合规验收

## 关联 task

`channel/2026-05-10-task-architect-to-worker-mq-normalmaps.md`（status: in_progress）

## 工序合规

- ✅ 启动检查 PASS：4 模块知识三件套已读、mq_mesh.py / SplatmapTerrain.shader 现状已确认、m_marching 范围外
- ✅ task spec 5 块全部实装（块 1: noise.py + 单测 + mq_mesh.py；块 2: TileCaseConfig；块 3: Refresh 按钮；块 4: TerrainBuilder + Shader；块 5: Postprocessor）
- ✅ Blender Add-on zip 重打（含 noise.py 共 5 文件 116KB）
- ✅ fbm tileable 单测：4 配置 × 3 轴 × 100 点 = 12 组合，全部 max diff = 0.000e+00（严格相等）
- ✅ reviewer subagent 对抗审查 PASS：0 BLOCKER、2 WARN（W1 切线手性 / W2 跨模块影响）均非阻塞且已附证据

## Worker 范围内决策（请合规审查）

1. **task spec 「`__init__.py` 修改」按真实代码位置改为 `mq_mesh.py`**：MQProperties / Operator / Panel 全部都在 mq_mesh.py，`__init__.py` 只 register。老板意图无矛盾，未触发 stop + message-to-architect。
2. **ApplyTileMPB 签名重构**：从 `(tile, x, z)` 改为 `(tile, x, z, caseIndex)` — RefreshTile 已经在前面算了 caseIndex，避免重算；单一 private 调用点同步修改。
3. **EnsureArray 泛型化**：`static void EnsureArray<T>(ref T[], int) where T : class` — 适配 GameObject + Texture2D 两种数组，序列化字段类型未变。
4. **noise.py 切线空间编码用简化版 `(-gx, -gy, 1)`**：依据 architecture.md 的 "MQ 地形 mesh 几乎水平 / tangent≈+X / bitangent≈+Y / normal≈+Z（Blender Z up）" 假设，未读 mesh.calc_tangents() 的精确 tangent 数据。架构师设计就是简化版。
5. **Blender Add-on 中 noise_field 在 ExportAllCases 循环外创建一次共享**：所有 19 case 共享 same instance（无 mutable 状态），是边界连续性的前提。

## 跨模块影响评估

| 模块 | 影响面 | 评估 |
|---|---|---|
| `editor/blender` | 新文件 noise.py / test_noise.py，mq_mesh.py 内嵌 bake，build_zip 加文件 | ✅ 与既有 cube_table / mc_mesh / mq_mesh 解耦 |
| `editor/art-mq-mesh` | MQMeshConfigEditor 加 Refresh Normal Maps 按钮 + DoRefreshNormalMaps | ✅ 不动 DoTerrainBuild / DoCliffBuild |
| `editor/art-mc-mesh` | ArtMeshFbxPostprocessor 加 importTangents=CalculateMikk | ✅ 全工程仅 SplatmapTerrain.shader 用 TANGENT，其他 shader 仅用 NORMAL，无视觉副作用 |
| `sample/build-system/terrain` | TileCaseConfig 加 _normalMaps[19] + Get/Set；TerrainBuilder.ApplyTileMPB 多接 caseIndex 参数 + GetNormalMap 注入；Shader 加 tangent / TBN / UnpackNormal | ✅ 缺省 _NormalMap = "bump" → UnpackNormal 返回 (0,0,1) → nWorld ≡ worldNormal，零扰动等价回退路径成立 |

## 与知识三件套对账

| 真相源 constraint | 实装符合 |
|---|---|
| `blender/module.json` "fbm 第 k 个 octave 的 lattice hash 必须按 (freq × 2^k) 取模" | ✅ noise.py:142 `f = self.frequency * (1 << k)` + corner() 内 `(ix0+dx) % p` |
| `blender/architecture.md` "tileable 不变量保证 fbm(1,y,z) ≡ fbm(0,y,z)" | ✅ 单测 max diff = 0 严格相等 |
| `blender/architecture.md` "Blender end mesh.calc_tangents() ↔ Unity ImportTangents=CalculateMikk" | ✅ Postprocessor 块 5 实装；worker 用简化版编码不依赖 calc_tangents 精度 |
| `art-mq-mesh/contract.md` "Refresh Normal Maps 是只读扫描操作 + 自动改 textureType=NormalMap" | ✅ 块 3 实装；console log 报 matched/fixed/out-of-range 三段 |
| `terrain/module.json` "TileCaseConfig.normalMaps[19] 是运行时引用数组" | ✅ 字段名 `_normalMaps`（私有 + Header 注释，与 `_prefabs/_cliffPrefabs` 同款），public API GetNormalMap/SetNormalMap 与 contract 完全对齐 |
| `terrain/module.json` "SplatmapTerrain.shader: appdata 加 tangent: TANGENT, v2f 加 worldTangent + worldBitangent, frag 切线空间法线 → TBN 转世界 → 替换 i.worldNormal 计算 ndl" | ✅ 块 4 shader 实装严格按设计 |

## 启动检查 + 任务边界硬规则遵守

- ✅ self-gating 通过后才接单
- ✅ 不直写 contract.md / architecture.md / module.json
- ✅ 不向 reviewer 发 channel 文件（已用 Agent 工具调起 reviewer subagent，不走 channel）
- ✅ 不跳过 git hooks（当前未 commit，等架构师 + 老板验收后再 commit）
- ✅ task spec 偏差非矛盾（位置纠正）— 已在接单 Reply 注明，未触发 stop

## 待人工验收项（worker 无法跑）

老板 + Blender + Play 模式手动验收 #1~#6（详见 task channel 完工 Reply）。

## 请审查

请架构师对照知识三件套验收以下：

1. 工序合规（启动检查 / reviewer subagent / 不越权）
2. 5 块实装与设计对齐
3. worker 范围内的 5 个决策是否在权限内
4. 跨模块影响评估是否完整
5. 是否有遗漏的 hidden constraint

如 PASS，告知 worker 把 task channel status 改 resolved + commit；如有修订，append 到本 review 的 Reply 段。
